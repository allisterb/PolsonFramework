namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json.Nodes;

using Polson.MCPServer;

using Xunit;

/// <summary>
/// Tests for the <c>CompareImages</c> tool — "has this render actually moved?".
/// </summary>
/// <remarks>
/// <para>
/// It exists for a supervising plugin that watches a role for thrashing, and the thing it has to get
/// right is not the arithmetic but the *semantics*: byte-identity is the wrong question. An encoder
/// can produce different bytes for the same picture, so a file hash reports a change that a viewer
/// would not see, and a watchdog built on hashes never fires. Hence the downscale before comparing —
/// the question is whether the picture changed, not whether any pixel did.
/// </para>
/// <para>
/// The other contract worth pinning is that differently sized images return a **result** rather than
/// throwing. <c>SkiaBitmapWrapper.Diff</c> throws on a size mismatch by design, because a similarity
/// score over a partial overlap would look like an answer. For a caller asking "did this change",
/// though, a size change *is* the answer, and a thrown error would turn the most obvious kind of
/// change into a failed tool call.
/// </para>
/// </remarks>
public class CompareImagesTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-compare-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public CompareImagesTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TestIdenticalScenesAreIdentical()
    {
        var tools = Tools();
        await Render(tools, "a.png", 40, 300, 200);
        await Render(tools, "b.png", 40, 300, 200);

        var result = tools.CompareImages("a.png", "b.png");

        Assert.True((bool)result["comparable"]!);
        Assert.True((bool)result["identical"]!);
        Assert.Equal(1d, (double)result["similarity"]!, 6);
        // Nothing differs, so there is no region to report.
        Assert.Null(result["bounds"]);
    }

    [Fact]
    public async Task TestMovedElementIsNotIdenticalAndReportsWhere()
    {
        var tools = Tools();
        await Render(tools, "a.png", 40, 300, 200);
        await Render(tools, "c.png", 180, 300, 200);

        var result = tools.CompareImages("a.png", "c.png");

        Assert.True((bool)result["comparable"]!);
        Assert.False((bool)result["identical"]!);
        Assert.True((double)result["similarity"]! < 1d);
        Assert.NotNull(result["bounds"]);
        Assert.True((double)result["bounds"]!["width"]! > 0);
    }

    [Fact]
    public async Task TestDifferentSizesReportRatherThanThrow()
    {
        var tools = Tools();
        await Render(tools, "a.png", 40, 300, 200);
        await Render(tools, "tall.png", 40, 300, 400);

        var result = tools.CompareImages("a.png", "tall.png");

        Assert.False((bool)result["comparable"]!);
        Assert.False((bool)result["identical"]!);
        Assert.Contains("different sizes", (string)result["reason"]!);
    }

    [Fact]
    public async Task TestComparesAtTheRequestedScale()
    {
        var tools = Tools();
        await Render(tools, "a.png", 40, 800, 600);
        await Render(tools, "b.png", 40, 800, 600);

        Assert.Equal("128x96", (string)tools.CompareImages("a.png", "b.png", maxDimension: 128)["comparedAt"]!);
        // 0 means full size, which is what a caller wanting bit-level scrutiny asks for.
        Assert.Equal("800x600", (string)tools.CompareImages("a.png", "b.png", maxDimension: 0)["comparedAt"]!);
    }

    [Fact]
    public void TestMissingFileNamesThePathItResolvedTo()
    {
        var error = Assert.Throws<FileNotFoundException>(
            () => Tools().CompareImages("nope.png", "also-nope.png"));

        Assert.Contains("nope.png", error.Message);
        Assert.Contains("relative to the project directory", error.Message);
    }

    [Fact]
    public async Task TestPathOutsideTheProjectIsRefused()
    {
        var tools = Tools();
        await Render(tools, "a.png", 40, 300, 200);

        Assert.ThrowsAny<Exception>(() => tools.CompareImages("a.png", "../escaped.png"));
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    /// <summary>Draws a flat rectangle at <paramref name="x"/> and writes it to the project.</summary>
    private static async Task Render(DrawingMcpTools tools, string outFile, int x, int width, int height)
    {
        var script =
            $"const c = createCanvas({width}, {height});" +
            "const g = c.getContext('2d');" +
            "g.fillStyle = '#223344'; g.fillRect(0, 0, " + width + ", " + height + ");" +
            $"g.fillStyle = '#e2b33c'; g.fillRect({x}, 50, 80, 70); c;";

        var result = await tools.ExecuteScript(script, outFile: outFile, format: "png");
        Assert.True(result.Success, string.Join("; ", result.Logs));
    }
    #endregion
}
