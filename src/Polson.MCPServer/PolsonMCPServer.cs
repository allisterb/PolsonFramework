namespace Polson.MCPServer;

using System;
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
    #endregion

    #region Methods
    public static async Task RunStdioAsync(IConfigurationRoot? config = null, string? projectDir = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        var registry = new SessionRegistry();
        builder.Services.AddSingleton(registry);
        builder.Services.AddHostedService<IdleSessionSweeper>();

        builder.Logging
            .ClearProviders()
            .AddProvider(loggerProvider)
            .SetMinimumLevel(LogLevel.Trace);

        var mcp = builder.Services.AddMcpServer();
        RegisterToolsAndResources(mcp, registry, projectDir);
        mcp.WithStdioServerTransport();

        var app = builder.Build();

        app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(() =>
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

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("Mcp-Session-Id");
            });
        });

        var mcp = builder.Services.AddMcpServer();
        RegisterToolsAndResources(mcp, registry, projectDir);

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

    private static void RegisterToolsAndResources(IMcpServerBuilder mcp, SessionRegistry registry, string? projectRoot)
    {
        mcp.WithTools(new DrawingMcpTools(new JsDrawingEngine(), registry, new LocalKnowledgeIndex(), projectRoot));
        mcp.WithResources<PolsonResources>();
        mcp.WithResources(PolsonResources.AreaResources(PolsonResources.Docs));
        mcp.WithResources<PolsonManuals>();
        mcp.WithResources(PolsonManuals.ManualResources());
    }
    #endregion
}

