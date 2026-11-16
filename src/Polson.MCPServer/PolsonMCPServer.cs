namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class PolsonMCPServer : Runtime
{
    #region Constants
    public const string CorsPolicyName = "PolsonMcpCors";

    /// <summary>Whether a browser origin is on this machine: <c>localhost</c>, <c>127.0.0.1</c> or <c>[::1]</c>, any port.</summary>
    internal static bool IsLoopbackOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.IsLoopback;
    #endregion

    #region Methods
    public static async Task RunStdioAsync(IConfigurationRoot? config = null, string? projectDir = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        // A host starts this server with its session, so now is when the run began.
        var registry = new SessionRegistry { RunStartedUtc = DateTimeOffset.UtcNow };
        builder.Services.AddSingleton(registry);
        builder.Services.AddHostedService<IdleSessionSweeper>();

        builder.Logging
            .ClearProviders()
            .AddProvider(loggerProvider)
            .SetMinimumLevel(LogLevel.Trace);

        var mcp = builder.Services.AddMcpServer();
        var tools = RegisterToolsAndResources(mcp, registry, projectDir);
        mcp.WithStdioServerTransport();

        var app = builder.Build();

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        BracketRun(lifetime, tools.Events, "stdio", projectDir);
        lifetime.ApplicationStopping.Register(() =>
        {
            registry.Dispose();
        });

        Info("Polson MCP server started in stdio transport mode (project directory: {0}).", projectDir ?? Directory.GetCurrentDirectory());

        await app.RunAsync();
    }

    public static async Task RunHttpAsync(IConfigurationRoot? config = null, int? port = null, string? projectDir = null)
    {
        var app = BuildHttpApp(config, port, projectDir);
        await app.RunAsync();
    }

    public static WebApplication BuildHttpApp(IConfigurationRoot? config = null, int? port = null, string? projectDir = null)
    {
        var builder = WebApplication.CreateBuilder();

        var registry = new SessionRegistry();
        builder.Services.AddSingleton(registry);
        builder.Services.AddHostedService<IdleSessionSweeper>();

        if (port.HasValue)
        {
            builder.WebHost.UseUrls($"http://localhost:{port.Value}");
        }
        else if (config?["Server:HttpPort"] is string portStr && int.TryParse(portStr, out var cfgPort))
        {
            builder.WebHost.UseUrls($"http://localhost:{cfgPort}");
        }

        builder.Logging
            .ClearProviders()
            .AddProvider(loggerProvider)
            .SetMinimumLevel(LogLevel.Trace);

        // The server runs scripts and writes files with no authentication, so only this machine may call it. A browser
        // page from anywhere else could otherwise drive it through CORS (any origin was allowed until 2026-11-15), or
        // through DNS rebinding, where a hostile name resolves to 127.0.0.1 and the Host header still carries the name.
        builder.Services.Configure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
            options.AllowedHosts = ["localhost", "127.0.0.1", "[::1]"]);

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .SetIsOriginAllowed(IsLoopbackOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("Mcp-Session-Id");
            });
        });

        var mcp = builder.Services.AddMcpServer();
        var tools = RegisterToolsAndResources(mcp, registry, projectDir);

#pragma warning disable MCP9004
        mcp.WithHttpTransport(options =>
        {
            options.Stateless = false;
            options.EnableLegacySse = true;
        });
#pragma warning restore MCP9004

        var app = builder.Build();

        app.UseCors(CorsPolicyName);
        app.MapMcp();

        BracketRun(app.Lifetime, tools.Events, "http", projectDir);
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            registry.Dispose();
        });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var addresses = app.Services
                .GetService<IServer>()?
                .Features
                .Get<IServerAddressesFeature>()?
                .Addresses;

            if (addresses == null || addresses.Count == 0)
            {
                Info("Polson MCP server (HTTP transport) started (project directory: {0}).", projectDir ?? Directory.GetCurrentDirectory());
            }
            else
            {
                foreach (var address in addresses)
                {
                    Info("Polson MCP server listening on {0} (Streamable HTTP at '/', legacy SSE at '/sse' and '/message', project directory: {1}).",
                        address, projectDir ?? Directory.GetCurrentDirectory());
                }
            }
        });

        return app;
    }

    /// <summary>
    /// Brackets the run in its own record: <c>run.start</c> now, <c>run.end</c> on shutdown.
    /// </summary>
    /// <remarks>
    /// Without the closing event a reader cannot tell a finished run from one whose process died
    /// mid-script — the log simply stops either way, which the first agent run demonstrated by
    /// leaving its last stage open with nothing after it.
    /// </remarks>
    private static void BracketRun(IHostApplicationLifetime lifetime, RunEventLog events, string transport, string? projectRoot)
    {
        if (!events.Enabled) return;

        events.Append("run.start", fields: new Dictionary<string, object?>
        {
            ["transport"] = transport,
            ["project"] = projectRoot
        });

        lifetime.ApplicationStopping.Register(() => events.Append("run.end"));
    }

    private static DrawingMcpTools RegisterToolsAndResources(IMcpServerBuilder mcp, SessionRegistry registry, string? projectRoot)
    {
        var tools = new DrawingMcpTools(new JsDrawingEngine(), registry, new LocalKnowledgeIndex(), projectRoot);

        // Resources are served by static methods, so this is how they reach the run's event log.
        // Without it the record cannot say which documentation an agent read — and on ADK, where a
        // resource is injected for one turn and never persisted, how OFTEN it was read is the
        // difference between an agent that has the reference and one that had it once.
        PolsonResources.OnRead = uri => tools.Events.Append("doc.read", fields: new Dictionary<string, object?>
        {
            ["uri"] = uri,
            ["via"] = "resource"
        });

        mcp.WithTools(tools);
        mcp.WithResources<PolsonResources>();
        mcp.WithResources(PolsonResources.AreaResources(PolsonResources.Docs));
        // Polson.Engine embeds no manuals, and advertises none.
        if (PolsonManuals.All.Count > 0)
        {
            mcp.WithResources<PolsonManuals>();
            mcp.WithResources(PolsonManuals.ManualResources());
        }

        return tools;
    }
    #endregion
}

