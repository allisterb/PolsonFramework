namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Animation output in the run record. <c>Motion.save</c>, <c>Motion.sheet</c> and a composition's
/// <c>saveSvg</c>/<c>saveSif</c> wrote files and recorded nothing, so the studio showed only the still a
/// script returned and the run report called the animation an unexplained artifact.
/// </summary>
public class MotionRecordingTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-motion-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public MotionRecordingTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    /// <summary>Six frames of a moving square, held in the Motion buffer.</summary>
    private const string Frames = """
        const c = createCanvas(48, 32);
        const x = c.getContext('2d');
        for (let i = 0; i < 6; i++) {
            x.fillStyle = '#ffffff'; x.fillRect(0, 0, 48, 32);
            x.fillStyle = '#1f6f8b'; x.fillRect(i * 6, 8, 12, 12);
            Motion.frame(c);
        }
        """;

    private JsonElement[] Events()
    {
        var file = Path.Combine(root, "events", "server.jsonl");
        if (!File.Exists(file)) return [];

        return File.ReadAllLines(file)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToArray();
    }

    private JsonElement[] OfType(string type) => [.. Events().Where(e => e.GetProperty("type").GetString() == type)];

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() : null;
    #endregion

    #region Tests
    /// <summary>
    /// An animation and its contact sheet are each a <c>render</c>, tied to the execution and the script
    /// that made them, with the artifact spelled project-relative as every other render is.
    /// </summary>
    [Fact]
    public async Task TestSaveAndSheetAreRecordedAsRenders()
    {
        await Tools().ExecuteScript(Frames + """
            Stage.begin('Timing');
            Motion.save('artifacts/move.webp', { fps: 12 });
            Motion.sheet('artifacts/move-sheet.png', { count: 3, fps: 12 });
            'done';
            """, 48, 32);

        var start = Assert.Single(OfType("script.start"));
        var renders = OfType("render");
        Assert.Equal(2, renders.Length);

        var anim = renders.Single(r => Str(r, "motion") == "animation");
        Assert.Equal("artifacts/move.webp", Str(anim, "artifact"));
        Assert.Equal("webp", Str(anim, "format"));
        Assert.Equal(6, anim.GetProperty("frames").GetInt32());
        Assert.Equal(48, anim.GetProperty("width").GetInt32());
        Assert.Equal(32, anim.GetProperty("height").GetInt32());
        Assert.Equal(500, anim.GetProperty("durationMs").GetDouble(), 3);
        Assert.True(anim.GetProperty("bytes").GetInt64() > 0);

        var sheet = renders.Single(r => Str(r, "motion") == "sheet");
        Assert.Equal("artifacts/move-sheet.png", Str(sheet, "artifact"));
        Assert.Equal("png", Str(sheet, "format"));
        Assert.Equal(3, sheet.GetProperty("cells").GetInt32());

        foreach (var r in renders)
        {
            Assert.Equal("Timing", Str(r, "stage"));
            Assert.Equal(Str(start, "execution"), Str(r, "execution"));
            Assert.Equal(Str(start, "script"), Str(r, "script"));
        }
    }

    /// <summary>The stage is read when the file is written, so a script that moves on records each save under its own.</summary>
    [Fact]
    public async Task TestEachSaveTakesTheStageItWasMadeIn()
    {
        await Tools().ExecuteScript(Frames + """
            Stage.begin('Keys');
            Motion.sheet('artifacts/keys.png', { count: 2 });
            Stage.begin('Deliver');
            Motion.save('artifacts/final.webp');
            'done';
            """, 48, 32);

        var renders = OfType("render");
        Assert.Equal("Keys", Str(renders.Single(r => Str(r, "motion") == "sheet"), "stage"));
        Assert.Equal("Deliver", Str(renders.Single(r => Str(r, "motion") == "animation"), "stage"));
    }

    /// <summary>
    /// The animation is recorded before an <c>outFile</c> still from the same execution, because it is
    /// written during the script and the still after it. The studio relies on that order.
    /// </summary>
    [Fact]
    public async Task TestAnimationIsRecordedBeforeTheReturnedStill()
    {
        await Tools().ExecuteScript(Frames + """
            Motion.save('artifacts/move.webp');
            c;
            """, 48, 32, outFile: "artifacts/still.webp");

        var renders = OfType("render");
        Assert.Equal(2, renders.Length);
        Assert.Equal("animation", Str(renders[0], "motion"));
        Assert.Null(Str(renders[1], "motion"));
        Assert.Equal("artifacts/still.webp", Str(renders[1], "artifact"));
    }

    /// <summary>
    /// <c>outFile</c> naming the file <c>Motion.save</c> just wrote would replace the animation with the
    /// still the script returned, silently. It is not written, and the response says why.
    /// </summary>
    [Fact]
    public async Task TestOutFileDoesNotOverwriteTheAnimation()
    {
        var result = await Tools().ExecuteScript(Frames + """
            Motion.save('output.webp');
            c;
            """, 48, 32, outFile: "output.webp");

        using (var codec = SkiaSharp.SKCodec.Create(Path.Combine(root, "output.webp")))
            Assert.Equal(6, codec.FrameCount);

        Assert.Contains(result.Logs, l => l.Contains("was not written over it", StringComparison.Ordinal));
        Assert.Equal("animation", Str(Assert.Single(OfType("render")), "motion"));
    }

    /// <summary>
    /// An animated SVG plays in a browser, so it is a render. A <c>.sif</c> does not, so it is an export,
    /// and the pictures written beside it are named so nothing it left behind is unexplained.
    /// </summary>
    [Fact]
    public async Task TestCompositionSvgIsARenderAndSifIsAnExport()
    {
        await Tools().ExecuteScript("""
            const n = Motion.nodes;
            const pic = createCanvas(8, 8);
            pic.getContext('2d').fillRect(0, 0, 8, 8);
            const comp = Motion.composition({ width: 64, height: 40, fps: 12, duration: 1 });
            comp.fill({ color: '#f2efe8' });
            comp.circle({ origin: n.animated('vector', [{ time: 0, value: [10, 20] }, { time: 1, value: [54, 20] }]), radius: 6, color: '#1f6f8b' });
            comp.saveSvg('artifacts/dot.svg');

            const withImage = Motion.composition({ width: 64, height: 40, fps: 12, duration: 1 });
            withImage.image({ image: pic, tl: [4, 4], br: [12, 12] });
            withImage.saveSif('artifacts/dot.sif');
            'done';
            """, 64, 40);

        var svg = Assert.Single(OfType("render"));
        Assert.Equal("svg", Str(svg, "motion"));
        Assert.Equal("artifacts/dot.svg", Str(svg, "artifact"));
        Assert.Equal(1000, svg.GetProperty("durationMs").GetDouble(), 3);

        var sif = Assert.Single(OfType("export"));
        Assert.Equal("sif", Str(sif, "format"));
        Assert.Equal("artifacts/dot.sif", Str(sif, "artifact"));
        var pictures = sif.GetProperty("pictures").EnumerateArray().Select(p => p.GetString()).ToArray();
        Assert.Single(pictures);
        Assert.StartsWith("artifacts/dot-image", pictures[0]);
        Assert.True(File.Exists(Path.Combine(root, pictures[0]!)));
    }
    #endregion
}
