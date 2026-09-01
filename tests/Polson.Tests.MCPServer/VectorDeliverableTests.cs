namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A brief that asks for an SVG must not be answerable with silence.
/// </summary>
/// <remarks>
/// A live run was briefed <i>"an SVG of a seagull riding a bicycle"</i>, built all seven of its
/// scripts on <c>createCanvas</c>, and delivered <c>.webp</c>. Every layer reported success: the
/// scripts ran, the renders were written, the run finished. <c>outSvg</c> was never passed — but had
/// it been, it would have written no file and said nothing, because the tool only acted when
/// <c>SvgXml</c> was non-empty and did nothing at all when it was not.
/// <para>
/// <see cref="SvgRetentionTests"/> covers the other half: that a script which <i>does</i> build a
/// paper keeps its markup even when it returns a canvas. This covers the case where there is no
/// paper — where the honest answer is "you asked for vector and there is none", said out loud.
/// </para>
/// </remarks>
public class VectorDeliverableTests : TestsRuntime, IDisposable
{
    #region Constants
    private const string RasterScript =
        "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); c;";

    private const string VectorScript =
        "const p = Snap(64, 64); p.circle(32, 32, 20).attr({ fill: '#10b981' }); p;";
    #endregion

    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-vector-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public VectorDeliverableTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Asking for vector from a raster script says so, on the response the agent reads.</summary>
    [Fact]
    public async Task TestRequestingSvgFromARasterScriptWarns()
    {
        var result = await Tools().ExecuteScript(RasterScript, 64, 64, outFile: "artifacts/s1.webp", outSvg: "artifacts/s1.svg");

        Assert.True(result.Success, result.Error);
        Assert.False(File.Exists(Path.Combine(root, "artifacts", "s1.svg")));
        Assert.Null(result.SvgFilePath);

        // The whole point: the agent must be able to see this without inspecting the filesystem.
        var warning = result.Logs.FirstOrDefault(l => l.Contains("outSvg", StringComparison.Ordinal));
        Assert.NotNull(warning);
        Assert.Contains("wrote nothing", warning);
        Assert.Contains("Snap(", warning);
    }

    /// <summary>The warning is recorded in the run log too, so the record shows the miss afterwards.</summary>
    [Fact]
    public async Task TestTheMissedVectorDeliverableIsRecorded()
    {
        await Tools().ExecuteScript(RasterScript, 64, 64, outSvg: "artifacts/s1.svg");

        var log = Path.Combine(root, "events", "server.jsonl");
        Assert.True(File.Exists(log), "no run event log was written");
        Assert.Contains("render.novector", File.ReadAllText(log), StringComparison.Ordinal);
    }

    /// <summary>A vector script writes the file and says nothing, which is the quiet path.</summary>
    [Fact]
    public async Task TestAVectorScriptWritesTheSvgWithoutWarning()
    {
        var result = await Tools().ExecuteScript(VectorScript, 64, 64, outFile: "artifacts/s1.webp", outSvg: "artifacts/s1.svg");

        Assert.True(result.Success, result.Error);

        var svg = Path.Combine(root, "artifacts", "s1.svg");
        Assert.True(File.Exists(svg));
        Assert.Contains("<circle", File.ReadAllText(svg), StringComparison.Ordinal);
        Assert.DoesNotContain(result.Logs, l => l.Contains("wrote nothing", StringComparison.Ordinal));
    }

    /// <summary>No <c>outSvg</c> asked for, so no complaint about not producing one.</summary>
    [Fact]
    public async Task TestARasterScriptWithoutOutSvgIsNotWarnedAt()
    {
        var result = await Tools().ExecuteScript(RasterScript, 64, 64, outFile: "artifacts/s1.webp");

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(result.Logs, l => l.Contains("outSvg", StringComparison.Ordinal));
    }
    #endregion

    #region Methods (private)
    private DrawingMcpTools Tools() => new(null, null, null, root);
    #endregion
}
