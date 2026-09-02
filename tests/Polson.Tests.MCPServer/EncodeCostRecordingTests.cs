namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// What a render actually cost, which the record used to leave out.
/// </summary>
/// <remarks>
/// <para>
/// The execution stopwatch stops the instant the script finishes evaluating, before anything is
/// rasterised or encoded — so the one duration the record carried was the smaller half. Measured on
/// a 1200x760 scene: 24 ms of script against 93 ms of render and encode on a canvas, and 17 ms
/// against 109 ms for a Snap paper. A reader of the old record would conclude a render was nearly
/// free, and would look for the time somewhere it was not.
/// </para>
/// <para>
/// The second omission is context rather than milliseconds. Inlined image bytes cross the wire as
/// base64 — a third larger than the image, delivered as text into the caller's window, and not as an
/// image content block. A routine WebP frame is around 126,000 characters. That is worth recording
/// for the same reason the duration is: a run that spent its window this way should say so.
/// </para>
/// </remarks>
public class EncodeCostRecordingTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-encode-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public EncodeCostRecordingTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    /// <summary>A scene heavy enough that rasterising it is unambiguously more than zero.</summary>
    private const string Scene =
        "const c = createCanvas(1200, 760); const x = c.getContext('2d');" +
        "for (let i = 0; i < 240; i++) { x.fillStyle = 'rgba(' + (i * 7 % 255) + ',80,120,0.5)';" +
        "x.beginPath(); x.arc((i * 37) % 1200, (i * 53) % 760, 8 + (i % 40), 0, Math.PI * 2); x.fill(); } c;";

    private JsonElement[] OfType(string type)
    {
        var file = Path.Combine(root, "events", "server.jsonl");
        if (!File.Exists(file)) return [];

        return [.. File.ReadAllLines(file)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Where(e => e.GetProperty("type").GetString() == type)];
    }
    #endregion

    #region Tests
    /// <summary>Render and encode are timed, and reported apart from the script's own time.</summary>
    [Fact]
    public async Task TestEncodeTimeIsMeasuredSeparatelyFromExecution()
    {
        var result = await Tools().ExecuteScript(script: Scene, width: 1200, height: 760, outFile: "out.webp");

        Assert.True(result.Success, result.Error);
        Assert.True(result.EncodeTimeMs > 0,
            "rasterising and encoding a 1200x760 scene cannot honestly be 0 ms");

        // The point of the split: it is its own number, not folded into the script's.
        var recorded = Assert.Single(OfType("script.ok"));
        Assert.Equal(result.ExecutionTimeMs, recorded.GetProperty("ms").GetInt64());
        Assert.Equal(result.EncodeTimeMs, recorded.GetProperty("encodeMs").GetInt64());
    }

    /// <summary>A paper is rasterised before it is encoded, and that is timed too.</summary>
    [Fact]
    public async Task TestAPaperRenderAlsoReportsEncodeTime()
    {
        var result = await Tools().ExecuteScript(
            script: "const p = Snap(1200, 760); p.rect(0, 0, 1200, 760).attr({ fill: '#245' });" +
                    "for (let i = 0; i < 200; i++) p.circle((i * 37) % 1200, (i * 53) % 760, 10 + (i % 30))" +
                    ".attr({ fill: '#fa3', opacity: 0.5 }); p;",
            width: 1200, height: 760, outFile: "paper.webp");

        Assert.True(result.Success, result.Error);
        Assert.True(result.EncodeTimeMs > 0, "rasterising a paper cannot honestly be 0 ms");
    }

    /// <summary>Inlining bytes is recorded, with what it cost the caller's context.</summary>
    /// <remarks>
    /// Recorded rather than refused: there are legitimate uses, and a tool that quietly declined
    /// would be worse than one that says what happened. The character count is the number that
    /// matters, because base64 lands in the window as text.
    /// </remarks>
    [Fact]
    public async Task TestInliningBytesIsRecordedWithItsCost()
    {
        var result = await Tools().ExecuteScript(script: Scene, width: 1200, height: 760, includeBytes: true);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.ImageBytes);

        var inlined = Assert.Single(OfType("script.bytesInlined"));
        Assert.Equal(result.ImageBytes!.Length, inlined.GetProperty("bytes").GetInt32());

        // Base64 is four characters per three bytes, rounded up to the padded block.
        var chars = inlined.GetProperty("base64Chars").GetInt32();
        Assert.Equal((result.ImageBytes.Length + 2) / 3 * 4, chars);
        Assert.True(chars > result.ImageBytes.Length, "base64 is larger than what it encodes");
    }

    /// <summary>Writing to a file inlines nothing, and so records nothing.</summary>
    /// <remarks>
    /// The default that makes the warning actionable: passing <c>outFile</c> is what an agent is told
    /// to do, and it must be the cheap path without also having to pass <c>includeBytes: false</c>.
    /// </remarks>
    [Fact]
    public async Task TestWritingToAFileDoesNotInlineBytes()
    {
        var result = await Tools().ExecuteScript(script: Scene, width: 1200, height: 760, outFile: "quiet.webp");

        Assert.True(result.Success, result.Error);
        Assert.Null(result.ImageBytes);
        Assert.Empty(OfType("script.bytesInlined"));

        // The image still reached disk; the saving is on the wire, not on the work.
        Assert.True(File.Exists(Path.Combine(root, "quiet.webp")));
        Assert.True(result.ImageSize > 0);
    }
    #endregion

    #region Render Opt-Out Tests
    /// <summary>
    /// <c>render: false</c> skips rasterising and encoding altogether.
    /// </summary>
    /// <remarks>
    /// A canvas is rendered whenever the script created one and returned something else, so until
    /// this existed there was no way to run a script that draws but does not encode — and a
    /// measurement pass draws in order to measure. Every probe therefore paid a full render for an
    /// image nothing read: about 150 ms at 1600x1200, on scripts an agent runs often.
    /// </remarks>
    [Fact]
    public async Task TestRenderFalseSkipsEncodingEntirely()
    {
        var tools = Tools();

        var rendered = await tools.ExecuteScript(script: Scene, width: 1200, height: 760);
        Assert.True(rendered.Success, rendered.Error);
        Assert.True(rendered.ImageSize > 0);

        var measured = await tools.ExecuteScript(script: Scene, width: 1200, height: 760, render: false);
        Assert.True(measured.Success, measured.Error);

        Assert.Null(measured.ImageBytes);
        Assert.Equal(0, measured.ImageSize);
        Assert.Equal(0, measured.EncodeTimeMs);

        // The script still ran: the saving is the encode, not the drawing.
        Assert.True(measured.ExecutionTimeMs >= 0);
    }

    /// <summary>A measurement still works with the render suppressed — that is the whole point.</summary>
    [Fact]
    public async Task TestAMeasurementPassStillMeasuresWithoutRendering()
    {
        var result = await Tools().ExecuteScript(script: """
            const c = createCanvas(400, 300);
            const x = c.getContext('2d');
            x.fillStyle = '#3366cc';
            x.fillRect(0, 0, 400, 300);
            log('sampled ' + c.bitmap.getPixel(200, 150));
            'measured';
            """, width: 400, height: 300, render: false);

        Assert.True(result.Success, result.Error);
        Assert.Null(result.ImageBytes);
        Assert.Contains(result.Logs, l => l.Contains("#3366CCFF", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Vector markup is serialized, not rasterized, so <c>outSvg</c> still works.</summary>
    [Fact]
    public async Task TestRenderFalseStillProducesSvgMarkup()
    {
        var result = await Tools().ExecuteScript(
            script: "const p = Snap(200, 150); p.circle(100, 75, 40).attr({ fill: '#c33' }); p;",
            width: 200, height: 150, render: false, outSvg: "mark.svg");

        Assert.True(result.Success, result.Error);
        Assert.Null(result.ImageBytes);
        Assert.Contains("<circle", result.SvgXml, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "mark.svg")));
    }

    /// <summary>Asking to suppress the render and to save it is refused, not reconciled.</summary>
    [Fact]
    public async Task TestRenderFalseWithOutFileIsRefused()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            Tools().ExecuteScript(script: Scene, width: 400, height: 300, render: false, outFile: "no.webp"));

        Assert.Contains("contradict", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(root, "no.webp")));
    }

    /// <summary>
    /// A canvas survives in <c>Session</c> between executions, so a stage can hand off without a file.
    /// </summary>
    /// <remarks>
    /// The cheapest artifact is the one never encoded. Paired with <c>render: false</c> this removes
    /// both halves of a stage-to-stage round trip — no encode on the way out, no decode on the way
    /// back — and `bitmap.diff` then works directly against the earlier bitmap.
    /// </remarks>
    [Fact]
    public async Task TestACanvasCanBeHandedToTheNextExecutionThroughSession()
    {
        var tools = Tools();

        var first = await tools.ExecuteScript(script: """
            const c = createCanvas(300, 200);
            const x = c.getContext('2d');
            x.fillStyle = '#204060';
            x.fillRect(0, 0, 300, 200);
            Session.stage1 = c.toBitmap();
            'stashed';
            """, width: 300, height: 200, render: false);
        Assert.True(first.Success, first.Error);

        var second = await tools.ExecuteScript(script: """
            const prev = Session.stage1;
            const c = createCanvas(300, 200);
            const x = c.getContext('2d');
            x.drawImage(prev, 0, 0);
            x.fillStyle = '#ff0044';
            x.fillRect(100, 60, 60, 40);
            const d = c.bitmap.diff(prev);
            log('changed ' + d.bounds.width + 'x' + d.bounds.height);
            'measured';
            """, width: 300, height: 200, render: false);

        Assert.True(second.Success, second.Error);
        Assert.Contains(second.Logs, l => l.Contains("changed 60x40", StringComparison.Ordinal));
    }
    #endregion
}
