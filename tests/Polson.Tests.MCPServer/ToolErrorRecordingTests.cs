namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Only <c>ExecuteScript</c> was recorded, so a failure in any other tool left the run silent about
/// it. The first agent run hit an unexplained <c>MeasureSvgPath</c> failure and worked around it;
/// nothing in <c>events/server.jsonl</c> showed it had happened, which is the case a record exists
/// for.
/// <para>
/// Every tool now records a <c>tool.error</c> and rethrows, so the result an agent sees is
/// unchanged — this only observes.
/// </para>
/// </summary>
public class ToolErrorRecordingTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-toolerr-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public ToolErrorRecordingTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    private JsonElement[] Events()
    {
        var file = Path.Combine(root, "events", "server.jsonl");
        if (!File.Exists(file)) return [];

        return File.ReadAllLines(file)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToArray();
    }

    private JsonElement? ToolError() =>
        Events().Cast<JsonElement?>().FirstOrDefault(e => e!.Value.GetProperty("type").GetString() == "tool.error");
    #endregion

    #region Recording Tests
    /// <summary>The tool whose unexplained failure prompted this.</summary>
    [Fact]
    public void TestMeasureSvgPathFailureIsRecorded()
    {
        Assert.Throws<ArgumentNullException>(() => Tools().MeasureSvgPath(null!));

        var error = ToolError();

        Assert.NotNull(error);
        Assert.Equal("MeasureSvgPath", error!.Value.GetProperty("tool").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.Value.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task TestSearchFailureIsRecorded()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Tools().Search(null!));

        Assert.Equal("Search", ToolError()?.GetProperty("tool").GetString());
    }

    [Fact]
    public void TestRenderSvgFailureIsRecorded()
    {
        Assert.Throws<ArgumentNullException>(() => Tools().RenderSvg(null!));

        Assert.Equal("RenderSvg", ToolError()?.GetProperty("tool").GetString());
    }

    /// <summary>
    /// A refused output path on RenderSvg is a real refusal an agent will hit. Unlike the other
    /// tools this one turns a failure into a failed result rather than throwing, so it has to record
    /// the error itself — otherwise the refusal would appear only in the tool response and the run
    /// record would claim nothing went wrong.
    /// </summary>
    [Fact]
    public void TestRenderSvgRefusedPathIsRecordedDespiteNotThrowing()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><rect width='16' height='16'/></svg>";

        var result = Tools().RenderSvg(svg, outFile: "../escaped.webp");

        Assert.False(result.Success);
        Assert.Equal("RenderSvg", ToolError()?.GetProperty("tool").GetString());
    }
    #endregion

    #region Success Tests
    /// <summary>A tool that works records no error, so tool.error means something went wrong.</summary>
    [Fact]
    public void TestSuccessfulToolRecordsNoError()
    {
        Tools().MeasureSvgPath("M0 0 L10 10");

        Assert.Null(ToolError());
    }

    /// <summary>
    /// RenderSvg writes artifacts exactly as ExecuteScript does, so it belongs in the record on the
    /// same terms — a file in artifacts/ the log cannot account for is a hole in it.
    /// </summary>
    [Fact]
    public void TestRenderSvgRecordsItsArtifact()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><rect width='16' height='16'/></svg>";

        Tools().RenderSvg(svg, outFile: "artifacts/vector.webp");

        var render = Events().Single(e => e.GetProperty("type").GetString() == "render");

        Assert.Equal("artifacts/vector.webp", render.GetProperty("artifact").GetString());
        Assert.Equal("RenderSvg", render.GetProperty("tool").GetString());
    }
    #endregion
}
