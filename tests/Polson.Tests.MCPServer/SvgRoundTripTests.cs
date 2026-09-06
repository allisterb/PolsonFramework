namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Polson.MCPServer;

using Xunit;

/// <summary>
/// The file-based route out of a vector document and back in.
/// </summary>
/// <remarks>
/// <para>
/// <c>svgXml</c> used to ride in every tool response, and nothing consumed it there — the CLI and
/// the tools layer both read it server-side, and an agent sees the work through the raster peek
/// rather than through markup. It was harmless while a vector scene was a few KB, and stopped being
/// harmless the moment a bitmap could be inlined: one 400px portrait made it 109,045 characters of
/// a 117,786-character result, on a call that had already asked for <c>outFile</c> and <c>outSvg</c>.
/// </para>
/// <para>
/// Removing it exposed the gap it had been masking — there was no way to reopen a saved SVG except
/// by carrying the markup through context. <c>Snap.load</c> and <c>RenderSvg(file:)</c> close it,
/// and these tests pin both halves plus the absence of the field.
/// </para>
/// </remarks>
public class SvgRoundTripTests : TestsRuntime, IDisposable
{
    #region Snap.load
    /// <summary>outSvg writes it, Snap.load reads it back — the round trip with no markup in context.</summary>
    [Fact]
    public async Task TestSnapLoadReopensWhatOutSvgWrote()
    {
        var tools = Tools();

        var written = await tools.ExecuteScript(
            script: "const paper = Snap(200, 120); paper.circle(60, 60, 40).attr({ fill: '#1f6f8b', id: 'mark' }); paper;",
            outSvg: "artifacts/stage1.svg", outFile: "artifacts/stage1.png");
        Assert.True(written.Success, written.Error);
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "stage1.svg")));

        var reopened = await tools.ExecuteScript(
            script: """
                const paper = Snap.load('artifacts/stage1.svg');
                const marks = paper.selectAll('circle');
                log('circles: ' + marks.length + ', fill: ' + marks[0].attr('fill'));
                paper.rect(120, 20, 60, 80).attr({ fill: '#c9553d' });   // edit what came back
                paper;
                """,
            outSvg: "artifacts/stage2.svg", outFile: "artifacts/stage2.png");

        Assert.True(reopened.Success, reopened.Error);
        Assert.Contains(reopened.Logs, l => l.Contains("circles: 1"));
        Assert.Contains("<rect", File.ReadAllText(Path.Combine(root, "artifacts", "stage2.svg")));
    }

    /// <summary>
    /// An inlined photograph survives the round trip. This is the failure the whole change is
    /// guarding: stage one looks perfect and stage two comes back with an empty frame.
    /// </summary>
    [Fact]
    public async Task TestAnInlinedImageSurvivesTheRoundTrip()
    {
        var tools = Tools();

        var written = await tools.ExecuteScript(
            script: """
                const canvas = createCanvas(16, 16);
                canvas.getContext('2d').fillStyle = '#4080c0';
                canvas.getContext('2d').fillRect(0, 0, 16, 16);
                const paper = Snap(100, 100);
                paper.image(canvas.toBitmap(), 10, 10, 80, 80);
                paper;
                """,
            outSvg: "artifacts/withimage.svg", outFile: "artifacts/withimage.png");
        Assert.True(written.Success, written.Error);

        var reopened = await tools.ExecuteScript(
            script: """
                const paper = Snap.load('artifacts/withimage.svg');
                const images = paper.selectAll('image');
                const href = images.length > 0 ? images[0].attr('href') : '';
                log('images: ' + images.length + ', inlined: ' + href.startsWith('data:'));
                paper;
                """,
            outFile: "artifacts/reopened.png");

        Assert.True(reopened.Success, reopened.Error);
        Assert.Contains(reopened.Logs, l => l.Contains("images: 1, inlined: true"));
    }

    /// <summary>Containment is the same as every other path: outside the project is refused.</summary>
    [Fact]
    public async Task TestSnapLoadIsContainedToTheProject()
    {
        var result = await Tools().ExecuteScript(script: "Snap.load('../../../etc/passwd.svg');");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    /// <summary>A missing file says where it looked, rather than failing as a parse error.</summary>
    [Fact]
    public async Task TestSnapLoadNamesAMissingFile()
    {
        var result = await Tools().ExecuteScript(script: "Snap.load('artifacts/nope.svg');");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region RenderSvg from a file
    /// <summary>Re-rendering a saved document needs no markup in the request.</summary>
    [Fact]
    public async Task TestRenderSvgReadsFromAFile()
    {
        var tools = Tools();
        await tools.ExecuteScript(
            script: "const paper = Snap(120, 120); paper.circle(60, 60, 50).attr({ fill: '#c9553d' }); paper;",
            outSvg: "artifacts/mark.svg", outFile: "artifacts/mark.png");

        var rendered = tools.RenderSvg(file: "artifacts/mark.svg", width: 240, height: 240,
            outFile: "artifacts/mark-large.png");

        Assert.True(rendered.Success, rendered.Error);
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "mark-large.png")));
    }

    /// <summary>Both sources, or neither, is a mistake about which document to draw — refused rather than resolved.</summary>
    [Theory]
    [InlineData("<svg/>", "artifacts/mark.svg")]
    [InlineData(null, null)]
    public void TestRenderSvgNeedsExactlyOneSource(string? svgXml, string? file) =>
        Assert.Throws<ArgumentException>(() => Tools().RenderSvg(svgXml, file));
    #endregion

    #region The field is gone from the wire
    /// <summary>
    /// The point of the exercise: the markup must not reach a tool result. Asserted against the
    /// serialized JSON rather than the object, because the object still carries it for the CLI and
    /// the href scan — it is <c>[JsonIgnore]</c>, not deleted.
    /// </summary>
    [Fact]
    public async Task TestSvgXmlIsNotSerializedIntoTheToolResult()
    {
        var result = await Tools().ExecuteScript(
            script: "const paper = Snap(200, 120); paper.circle(60, 60, 40).attr({ fill: '#1f6f8b' }); paper;",
            outSvg: "artifacts/wire.svg", outFile: "artifacts/wire.png");

        Assert.True(result.Success, result.Error);
        Assert.False(string.IsNullOrEmpty(result.SvgXml));   // still there for the server

        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("svgXml", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<circle", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("svgFilePath", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The size of the win, as a bound rather than as prose: the artifact carries the inlined
    /// picture and the tool result does not, however large the picture gets.
    /// </summary>
    /// <remarks>
    /// Deliberately noisy pixels. A flat fill encodes to a couple of hundred bytes, so a solid
    /// swatch made the artifact 457 bytes and proved nothing about scale — the first version of this
    /// test failed for exactly that reason and the threshold was the thing that was wrong, not the
    /// code. Noise is close to incompressible, so the inlined URI is genuinely large.
    /// </remarks>
    [Fact]
    public async Task TestAnInlinedImageDoesNotInflateTheToolResult()
    {
        var result = await Tools().ExecuteScript(
            script: """
                const canvas = createCanvas(160, 160);
                const ctx = canvas.getContext('2d');
                for (let y = 0; y < 160; y += 2) {
                    for (let x = 0; x < 160; x += 2) {
                        ctx.fillStyle = 'rgb(' + ((x * 7 + y * 13) % 256) + ',' + ((x * 29 + y * 3) % 256) + ',' + ((x * 17 + y * 41) % 256) + ')';
                        ctx.fillRect(x, y, 2, 2);
                    }
                }
                const paper = Snap(200, 200);
                paper.image(canvas.toBitmap(), 10, 10, 180, 180);
                paper;
                """,
            outSvg: "artifacts/big.svg", outFile: "artifacts/big.png");

        Assert.True(result.Success, result.Error);

        var json = JsonSerializer.Serialize(result);
        var svg = File.ReadAllText(Path.Combine(root, "artifacts", "big.svg"));

        Assert.Contains("data:image/", svg);
        Assert.True(svg.Length > 10000, $"the artifact should carry the inlined image; it is {svg.Length} chars");
        Assert.True(json.Length < 2000, $"the tool result should not: it is {json.Length} chars");
        Info("artifact {Svg} chars, tool result {Json} chars", svg.Length, json.Length);
    }
    #endregion

    #region Fixtures
    public SvgRoundTripTests()
    {
        root = Path.Combine(Path.GetTempPath(), $"polson_svgrt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { /* a temp dir that will not go is not a test failure */ }
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(projectRoot: root);

    private readonly string root;
    #endregion
}
