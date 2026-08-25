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
    public void TestDrawingMcpToolsExecuteScript()
    {
        var tools = new DrawingMcpTools();
        var script = @"
            var s = Snap(400, 300);
            s.rect(0, 0, 400, 300).attr('fill', '#0f172a');
            s.circle(200, 150, 80).attr('fill', '#ec4899');
            s;
        ";

        var result = tools.ExecuteScript(script, 400, 300);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.PngBytes);
        Assert.True(result.PngBytes.Length > 0);
        Assert.NotNull(result.SvgXml);
    }

    [Fact]
    public void TestDrawingMcpToolsRenderSvg()
    {
        var tools = new DrawingMcpTools();
        var svg = "<svg width=\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"100\" cy=\"100\" r=\"50\" fill=\"#10b981\"/></svg>";

        var result = tools.RenderSvg(svg, 200, 200);
        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.PngBytes);
        Assert.True(result.PngBytes.Length > 0);
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

