namespace Polson.Tests.MCPServer;

using System;
using System.Reflection;
using CommandLine;
using ModelContextProtocol.Server;
using Polson.CLI;
using Polson.MCPServer;
using Xunit;

public class MCPServerTests : TestsRuntime
{
    #region HTTP Application Building Tests
    [Fact]
    public void TestHttpAppBuild()
    {
        var app = PolsonMCPServer.BuildHttpApp(null, 5099, "C:\\test\\project");
        Assert.NotNull(app);
        Assert.NotNull(app.Services);
    }
    #endregion

    #region Tool Reflection and Attribute Tests
    [Fact]
    public void TestMcpServerToolAttributesPresence()
    {
        var methods = typeof(DrawingMcpTools).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var toolMethods = methods.Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null).ToList();
        Assert.NotEmpty(toolMethods);

        var names = toolMethods.Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name ?? m.Name).ToList();
        Assert.Contains("ExecuteScript", names);
        Assert.Contains("RenderSvg", names);
        Assert.Contains("MeasureSvgPath", names);
    }
    #endregion

    #region Tool Execution Direct Tests
    [Fact]
    public async Task TestDrawingMcpToolsExecuteScript()
    {
        var tools = new DrawingMcpTools();
        var script = @"
            var s = Snap(400, 300);
            s.rect(0, 0, 400, 300).attr('fill', '#0f172a');
            s.circle(200, 150, 80).attr('fill', '#ec4899');
            s;
        ";

        var result = await tools.ExecuteScript(script, 400, 300);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 0);
        Assert.NotNull(result.SvgXml);
    }

    [Fact]
    public async Task TestDrawingMcpToolsExecuteScriptWithOutFile()
    {
        var tools = new DrawingMcpTools();
        var tempImg = Path.Combine(Path.GetTempPath(), $"polson_test_{Guid.NewGuid():N}.webp");
        var tempSvg = Path.Combine(Path.GetTempPath(), $"polson_test_{Guid.NewGuid():N}.svg");

        try
        {
            var script = @"
                var c = createCanvas(200, 200);
                var ctx = c.getContext('2d');
                ctx.fillStyle = '#f59e0b';
                ctx.fillRect(0, 0, 200, 200);
                c;
            ";

            var result = await tools.ExecuteScript(script, 200, 200, outFile: tempImg, outSvg: tempSvg);
            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ImageFilePath);
            Assert.True(File.Exists(tempImg));
            Assert.True(new FileInfo(tempImg).Length > 0);
            Assert.Null(result.ImageBytes); // Omitted by default when saved to disk to save tokens
            Assert.True(result.ImageSize > 0);
        }
        finally
        {
            if (File.Exists(tempImg)) File.Delete(tempImg);
            if (File.Exists(tempSvg)) File.Delete(tempSvg);
        }
    }

    [Fact]
    public void TestDrawingMcpToolsRenderSvg()
    {
        var tools = new DrawingMcpTools();
        var svg = "<svg width=\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"100\" cy=\"100\" r=\"50\" fill=\"#10b981\"/></svg>";

        var result = tools.RenderSvg(svg, width: 200, height: 200);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 0);
    }

    [Fact]
    public void TestDrawingMcpToolsRenderSvgWithOutFile()
    {
        var tools = new DrawingMcpTools();
        var tempImg = Path.Combine(Path.GetTempPath(), $"polson_svg_test_{Guid.NewGuid():N}.webp");

        try
        {
            var svg = "<svg width=\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"200\" height=\"200\" fill=\"#3b82f6\"/></svg>";
            var result = tools.RenderSvg(svg, width: 200, height: 200, outFile: tempImg);
            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ImageFilePath);
            Assert.True(File.Exists(tempImg));
            Assert.True(new FileInfo(tempImg).Length > 0);
            Assert.Null(result.ImageBytes);
            Assert.True(result.ImageSize > 0);
        }
        finally
        {
            if (File.Exists(tempImg)) File.Delete(tempImg);
        }
    }

    [Fact]
    public void TestDrawingMcpToolsMeasureSvgPath()
    {
        var tools = new DrawingMcpTools();
        var pathData = "M0 0 L100 0";

        var response = tools.MeasureSvgPath(pathData, 50f);
        Assert.NotNull(response);

        var totalLen = Convert.ToSingle(response["totalLength"]?.ToString());
        Assert.Equal(100f, totalLen);

        var pt = response["pointAtLength"];
        Assert.NotNull(pt);
        Assert.Equal(50f, Convert.ToSingle(pt["x"]?.ToString()));
        Assert.Equal(0f, Convert.ToSingle(pt["y"]?.ToString()));
    }
    #endregion

    #region CLI Options Parsing Tests
    [Fact]
    public void TestCliServerOptionsParsing()
    {
        var args = new[] { "--http", "--port", "8080", "--timeout", "45", "--project-dir", "C:\\projects\\myart", "--debug" };

        var parser = new Parser(with => with.CaseInsensitiveEnumValues = true);
        var result = parser.ParseArguments<ServerOptions>(args);

        Assert.Equal(ParserResultType.Parsed, result.Tag);
        var opts = ((Parsed<ServerOptions>)result).Value;

        Assert.True(opts.Http);
        Assert.Equal(8080, opts.Port);
        Assert.Equal(45, opts.Timeout);
        Assert.Equal("C:\\projects\\myart", opts.ProjectDir);
        Assert.True(opts.Debug);
    }
    #endregion
}

