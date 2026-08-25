namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// End-to-end live MCP server protocol tests: an actual MCP client drives the HTTP/SSE transport pipeline
/// hosted on an ephemeral port.
/// </summary>
public class MCPServerProtocolTests : TestsRuntime, IAsyncLifetime
{
    private WebApplication app = null!;
    private string baseUrl = "";

    public async Task InitializeAsync()
    {
        app = PolsonMCPServer.BuildHttpApp(config);
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0"); // Ephemeral port
        await app.StartAsync();
        baseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async Task DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private async Task<McpClient> NewClientAsync()
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(baseUrl) },
            NullLoggerFactory.Instance);
        return await McpClient.CreateAsync(transport);
    }

    private static string Text(CallToolResult r) =>
        string.Concat(r.Content.OfType<TextContentBlock>().Select(c => c.Text));

    #region Tool Invocation Protocol Tests
    [Fact]
    public async Task ExecuteScriptReturnsVisualArtifacts()
    {
        await using var client = await NewClientAsync();

        var script = @"
            var s = Snap(300, 300);
            s.rect(0, 0, 300, 300).attr('fill', '#1e293b');
            s.circle(150, 150, 60).attr('fill', '#f43f5e');
            s;
        ";

        var r = await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = script,
            ["width"] = 300,
            ["height"] = 300
        });

        Assert.True(r.IsError != true, $"CallToolAsync failed: {Text(r)}");
        var text = Text(r);
        Assert.Contains("success", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("imageBytes", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("webp", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteScriptWithCanvas2D()
    {
        await using var client = await NewClientAsync();

        var script = @"
            var c = createCanvas(200, 200);
            var ctx = c.getContext('2d');
            ctx.fillStyle = '#0ea5e9';
            ctx.fillRect(10, 10, 180, 180);
            c;
        ";

        var r = await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = script
        });

        Assert.True(r.IsError != true, $"CallToolAsync failed: {Text(r)}");
        var text = Text(r);
        Assert.Contains("success", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderSvgToolCall()
    {
        await using var client = await NewClientAsync();

        var svg = "<svg width=\"100\" height=\"100\" xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"100\" height=\"100\" fill=\"#10b981\"/></svg>";

        var r = await client.CallToolAsync("RenderSvg", new Dictionary<string, object?>
        {
            ["svgXml"] = svg,
            ["width"] = 100,
            ["height"] = 100
        });

        Assert.NotEqual(true, r.IsError);
        var text = Text(r);
        Assert.Contains("success", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MeasureSvgPathToolCall()
    {
        await using var client = await NewClientAsync();

        var r = await client.CallToolAsync("MeasureSvgPath", new Dictionary<string, object?>
        {
            ["pathData"] = "M0 0 L100 0",
            ["length"] = 50f
        });

        Assert.NotEqual(true, r.IsError);
        var text = Text(r);
        Assert.Contains("totalLength", text);
        Assert.Contains("100", text);
        Assert.Contains("pointAtLength", text);
    }

    [Fact]
    public async Task ConsoleLoggingAndExitThroughExecute()
    {
        await using var client = await NewClientAsync();

        var script = @"
            console.log('first step');
            log('second step');
            exit('done early');
            console.log('should not run');
        ";

        var r = await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = script
        });

        Assert.NotEqual(true, r.IsError);
        var text = Text(r);
        Assert.Contains("first step", text);
        Assert.Contains("second step", text);
        Assert.Contains("done early", text);
        Assert.DoesNotContain("should not run", text);
    }

    [Fact]
    public async Task SessionStoragePersistsAcrossExecuteCalls()
    {
        await using var client = await NewClientAsync();

        // 1. First execution writes to Session dictionary
        var w = await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = "Session['color'] = '#ec4899'; Session['radius'] = 75; log('stored');"
        });
        Assert.True(w.IsError != true, $"Write failed: {Text(w)}");
        Assert.Contains("stored", Text(w));

        // 2. Second execution on the SAME session reads from Session dictionary
        var r = await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = "var s = Snap(200, 200); s.circle(100, 100, Session['radius']).attr('fill', Session['color']); log('radius=' + Session['radius']); s;"
        });
        Assert.True(r.IsError != true, $"Read failed: {Text(r)}");
        var text = Text(r);
        Assert.Contains("radius=75", text);
        Assert.Contains("imageBytes", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionStorageIsIsolatedBetweenSessions()
    {
        await using var client1 = await NewClientAsync();
        await using var client2 = await NewClientAsync();

        await client1.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = "Session['secret'] = 'client1_val';"
        });

        var r = await client2.CallToolAsync("ExecuteScript", new Dictionary<string, object?>
        {
            ["script"] = "log('secret=' + (Session['secret'] === undefined ? 'unset' : Session['secret']));"
        });

        Assert.True(r.IsError != true, $"Client2 check failed: {Text(r)}");
        Assert.Contains("secret=unset", Text(r));
    }

    [Fact]
    public async Task HistoryToolReturnsRecentScripts()
    {
        await using var client = await NewClientAsync();

        var script1 = "var a = 1; log(a);";
        var script2 = "var b = 2; log(b);";
        var script3 = "var c = 3; log(c);";

        await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?> { ["script"] = script1 });
        await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?> { ["script"] = script2 });
        await client.CallToolAsync("ExecuteScript", new Dictionary<string, object?> { ["script"] = script3 });

        // Default: last 1 script
        var h1 = await client.CallToolAsync("History", new Dictionary<string, object?>());
        Assert.True(h1.IsError != true, $"History call failed: {Text(h1)}");
        var text1 = Text(h1);
        Assert.Contains("var c = 3", text1);
        Assert.DoesNotContain("var a = 1", text1);

        // Specific n = 2: last 2 scripts
        var h2 = await client.CallToolAsync("History", new Dictionary<string, object?> { ["n"] = 2 });
        Assert.True(h2.IsError != true, $"History(2) call failed: {Text(h2)}");
        var text2 = Text(h2);
        Assert.Contains("var b = 2", text2);
        Assert.Contains("var c = 3", text2);
    }
    #endregion

    #region Resource Protocol Tests
    [Fact]
    public async Task SdkResourcesAreListedAndReadableThroughProtocol()
    {
        await using var client = await NewClientAsync();

        // 1. List resources
        var list = await client.ListResourcesAsync(new ListResourcesRequestParams());
        var uris = list.Resources.Select(r => r.Uri).ToList();

        Assert.Contains("polson://sdk/index", uris);
        Assert.Contains("polson://sdk/core", uris);
        Assert.Contains("polson://sdk/core/all", uris);
        Assert.Contains("polson://sdk/schema", uris);
        Assert.Contains("polson://sdk/schema/all", uris);
        Assert.Contains("polson://sdk/core/Snap", uris);
        Assert.Contains("polson://sdk/core/Canvas2D", uris);
        Assert.Contains("polson://sdk/core/Skia", uris);
        Assert.Contains("polson://sdk/core/Globals", uris);
        Assert.Contains("polson://sdk/schema/Snap", uris);
        Assert.Contains("polson://sdk/schema/Canvas2D", uris);
        Assert.Contains("polson://sdk/schema/Skia", uris);

        // 2. Read resource: Core Map (Index)
        var indexRes = await client.ReadResourceAsync("polson://sdk/index");
        var indexText = string.Concat(indexRes.Contents.OfType<TextResourceContents>().Select(c => c.Text));
        Assert.Contains("Polson JavaScript SDK", indexText);
        Assert.Contains("ECMAScript 2025", indexText);
        Assert.Contains("polson://sdk/core/Snap", indexText);

        // 3. Read resource: Sliced Core Snap methods
        var snapCore = await client.ReadResourceAsync("polson://sdk/core/Snap");
        var snapText = string.Concat(snapCore.Contents.OfType<TextResourceContents>().Select(c => c.Text));
        Assert.Contains("SnapPaper", snapText);
        Assert.Contains("SnapElement", snapText);

        // 4. Read resource: Sliced Schema Canvas2D
        var canvasSchema = await client.ReadResourceAsync("polson://sdk/schema/Canvas2D");
        var canvasSchemaText = string.Concat(canvasSchema.Contents.OfType<TextResourceContents>().Select(c => c.Text));
        Assert.Contains("TextMetrics", canvasSchemaText);
    }
    #endregion
}

