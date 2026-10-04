namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Polson.Animation;
using Polson.Drawing.Svg;

using SkiaSharp;

using Xunit;
using Xunit.Abstractions;

using static MotionCompositionTests;

/// <summary>
/// Blend methods, image layers and cutouts: the formulas against Synfig's renderer where it is present,
/// the refusals, and the SVG route against Svg.Skia.
/// </summary>
public class MotionBlendTests(ITestOutputHelper output) : TestsRuntime
{
    private static readonly string[] Methods =
        ["composite", "onto", "behind", "multiply", "divide", "screen", "overlay", "hardLight",
         "brighten", "darken", "add", "subtract", "difference", "alphaOver"];

    #region Oracle
    /// <summary>
    /// Every ported method, each in a group of its own over a half-transparent block, so both the backdrop's
    /// colour and its alpha reach the formula: inside the block, at its soft edge, and over nothing at all.
    /// </summary>
    [Fact]
    public void SynfigAndSkiaBlendTheSame()
    {
        if (Synfig() is not { } synfig) return;

        var comp = BlendScene();
        var theirs = RenderWithSynfig(synfig, comp, "blend");
        Assert.Equal(comp.FrameCount, theirs.Length);

        for (var i = 0; i < theirs.Length; i++)
        {
            using var ours = comp.Render(i / comp.Fps);
            using var bitmap = theirs[i];
            Dump(ours.SkBitmap, bitmap, $"blend-{i}");

            // Per method, so a failure names the one that disagrees.
            for (var m = 0; m < Methods.Length; m++)
            {
                var cell = SKRectI.Create(m * 60, 0, 60, 120);
                var differing = Differing(ours.SkBitmap, bitmap, 24, cell);
                output.WriteLine($"frame {i} {Methods[m],-10} {differing:P2} differ by more than 24");
                Assert.True(differing < 0.02, $"{Methods[m]} differs in {differing:P2} of its cell at frame {i}");
            }
        }
    }

    /// <summary>
    /// A cutout piece turning about its joint, through both renderers: the piece's colour centroid, which
    /// is what placement means, rather than its edges, where Synfig samples a picture half a pixel over.
    /// </summary>
    [Fact]
    public void SynfigAndSkiaCutTheSamePiece()
    {
        if (Synfig() is not { } synfig) return;

        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 240d), ("height", 180d), ("fps", 4d), ("duration", 1d)));
        comp.Fill(Opts(("color", "#ffffff")));
        comp.Cutout(Picture(), new object[] { new[] { 0d, 0d }, new[] { 40d, 0d }, new[] { 40d, 40d }, new[] { 0d, 40d } },
            Opts(("tl", new[] { 0d, 0d }), ("br", new[] { 120d, 80d }), ("origin", new[] { 0d, 0d }),
                 ("offset", new[] { 120d, 60d }), ("angle", motion.Nodes.Linear("angle", 90, 0))));

        var theirs = RenderWithSynfig(synfig, comp, "cut");
        var blue = SKColor.Parse("#2a6f97");
        var orange = SKColor.Parse("#e76f51");
        for (var i = 0; i < theirs.Length; i++)
        {
            using var ours = comp.Render(i / comp.Fps);
            using var bitmap = theirs[i];
            Dump(ours.SkBitmap, bitmap, $"cut-{i}");

            var (ox, oy) = Centroid(ours.SkBitmap, c => Near(c, blue));
            var (sx, sy) = Centroid(bitmap, c => Near(c, blue));
            output.WriteLine($"frame {i}: ours ({ox:0.0}, {oy:0.0}), synfig ({sx:0.0}, {sy:0.0})");
            Assert.InRange(Math.Abs(ox - sx), 0, 1.0);
            Assert.InRange(Math.Abs(oy - sy), 0, 1.0);

            // Only the square at the top-left of the picture survives the cut: the orange column is outside it.
            Assert.False(double.IsFinite(Centroid(ours.SkBitmap, c => Near(c, orange)).X), "the cut left orange in");
            Assert.False(double.IsFinite(Centroid(bitmap, c => Near(c, orange)).X), "synfig's cut left orange in");
        }
    }
    #endregion

    #region Formulas
    [Fact]
    public void MultiplyKeepsTheBackdropsAlphaWhereCssWouldNot()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 20d), ("height", 10d)));
        comp.Rectangle(Opts(("point1", new[] { 0d, 0d }), ("point2", new[] { 10d, 10d }), ("color", "#808080")));
        comp.Fill(Opts(("color", "#ff0000"), ("blend", "multiply")));

        using var frame = comp.Render(0);
        var inside = frame.SkBitmap.GetPixel(5, 5);
        var outside = frame.SkBitmap.GetPixel(15, 5);
        Assert.InRange(inside.Red, 126, 130);
        Assert.InRange(inside.Green, 0, 2);
        Assert.Equal(255, inside.Alpha);
        Assert.Equal(0, outside.Alpha);   // nothing under it, so nothing to multiply
    }

    [Fact]
    public void AlphaOverErasesInProportionToAmount()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 10d), ("height", 10d)));
        comp.Fill(Opts(("color", "#0000ff")));
        comp.Fill(Opts(("color", "#000000"), ("blend", "alphaOver"), ("amount", 0.25)));

        using var frame = comp.Render(0);
        var p = frame.SkBitmap.GetPixel(5, 5);
        Assert.InRange(p.Alpha, 189, 193);
        Assert.Equal(255, p.Blue);
    }

    [Fact]
    public void ABlendStaysInsideItsGroup()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 10d), ("height", 10d)));
        comp.Fill(Opts(("color", "#00ff00")));
        var g = comp.Group();
        g.Fill(Opts(("color", "#000000"), ("blend", "alphaOver")));   // erases only the group's own empty surface

        using var frame = comp.Render(0);
        Assert.Equal(new SKColor(0, 255, 0), frame.SkBitmap.GetPixel(5, 5));
    }

    [Fact]
    public void BehindGoesUnderWhatCameBefore()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 20d), ("height", 10d)));
        comp.Rectangle(Opts(("point1", new[] { 0d, 0d }), ("point2", new[] { 10d, 10d }), ("color", "#ff0000")));
        comp.Fill(Opts(("color", "#0000ff"), ("blend", "behind")));

        using var frame = comp.Render(0);
        Assert.Equal(new SKColor(255, 0, 0), frame.SkBitmap.GetPixel(5, 5));
        Assert.Equal(new SKColor(0, 0, 255), frame.SkBitmap.GetPixel(15, 5));
    }
    #endregion

    #region Refusals
    [Theory]
    [InlineData("multipy", "has no blend 'multipy'")]
    [InlineData("straight", "not available")]
    [InlineData("hue", "not ported yet")]
    public void AnUnknownOrUnportedBlendIsRefusedByName(string blend, string expected)
    {
        var comp = new MotionToolkit().Composition();
        var e = Assert.Throws<ArgumentException>(() => comp.Fill(Opts(("blend", blend))));
        Assert.Contains(expected, e.Message);
        Assert.Contains("alphaOver", e.Message);
    }

    [Fact]
    public void AnImageTakesPixelsNotAFilename()
    {
        var comp = new MotionToolkit().Composition();
        var e = Assert.Throws<ArgumentException>(() => comp.Image(Opts(("image", "artifacts/cell.png"))));
        Assert.Contains("not a filename", e.Message);
    }

    [Fact]
    public void AnImageIsCopiedWhenTheLayerIsMade()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 10d), ("height", 10d)));
        var source = new Polson.Drawing.Skia.SkiaCanvas(10, 10);
        source.SkCanvas.Clear(SKColors.Red);
        comp.Image(Opts(("image", source)));
        source.SkCanvas.Clear(SKColors.Blue);

        using var frame = comp.Render(0);
        Assert.Equal(SKColors.Red, frame.SkBitmap.GetPixel(5, 5));
    }

    [Fact]
    public void ToSifRefusesAnImageAndSaveSifWritesItBeside()
    {
        var comp = new MotionToolkit().Composition();
        comp.Image(Opts(("image", Picture())));
        comp.Cutout(Picture(), new object[] { new[] { 0d, 0d }, new[] { 10d, 0d }, new[] { 0d, 10d } });
        var e = Assert.Throws<InvalidOperationException>(() => comp.ToSif());
        Assert.Contains("saveSif", e.Message);

        var dir = Path.Combine(Path.GetTempPath(), "polson-sif-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var path = comp.SaveSif(Path.Combine(dir, "puppet.sif"));
            Assert.Single(Directory.GetFiles(dir, "*.png"));   // one picture, used twice, written once
            Assert.Contains("puppet-image1.png", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
    #endregion

    #region SVG
    /// <summary>
    /// The SVG route for what it can say: erasing (a cutout) as a mask, behind as reordering, and the
    /// separable methods as CSS blend modes over an opaque backdrop.
    /// </summary>
    [Fact]
    public void TheSvgRouteBlendsWhereItCan()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 300d), ("height", 120d), ("fps", 4d), ("duration", 1d)));
        comp.Fill(Opts(("color", "#d8d2c4")));
        comp.Cutout(Picture(), new object[] { new[] { 0d, 0d }, new[] { 50d, 5d }, new[] { 10d, 38d } },
            Opts(("origin", new[] { 0d, 0d }), ("offset", new[] { 40d, 40d }), ("angle", motion.Nodes.Linear("angle", 60, 0))));
        comp.Circle(Opts(("origin", new[] { 150d, 60d }), ("radius", 30d), ("color", "#2a6f97")));
        comp.Circle(Opts(("origin", new[] { 175d, 60d }), ("radius", 30d), ("color", "#e76f51"), ("blend", "behind")));
        foreach (var (method, x) in new[] { ("multiply", 230d), ("screen", 260d) })
            comp.Rectangle(Opts(("point1", new[] { x - 20, 20d }), ("point2", new[] { x + 10, 100d }), ("color", "#c9553d"), ("blend", method)));

        var svg = comp.ToSvg();
        Assert.Contains("<mask", svg);
        Assert.Contains("mix-blend-mode:multiply", svg);

        foreach (var t in new[] { 0, 0.37, 1 })
        {
            using var ours = comp.Render(t);
            using var theirs = SKBitmap.Decode(SvgRenderPipeline.RenderToImage(svg, comp.Width, comp.Height, "png", 100, null, TimeSpan.FromSeconds(t)));
            Dump(ours.SkBitmap, theirs, $"svg-{t}");
            var differing = MotionCompositionTests.Differing(ours.SkBitmap, theirs, 48);
            output.WriteLine($"{t}s: {differing:P2} differ by more than 48");
            Assert.True(differing < 0.02, $"{differing:P2} differ at {t}s");
        }
    }

    [Fact]
    public void TheSvgRouteRefusesWhatItCannotSay()
    {
        var comp = new MotionToolkit().Composition();
        comp.Fill(Opts(("color", "#ff0000"), ("blend", "add"), ("desc", "glow")));
        var e = Assert.Throws<InvalidOperationException>(() => comp.ToSvg());
        Assert.Contains("'glow'", e.Message);
        Assert.Contains("'add'", e.Message);
    }
    #endregion

    #region Private
    /// <summary>One column per method; a half-transparent block with a soft edge, and a disc over it at 0.8.</summary>
    private static MotionComposition BlendScene()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 60d * Methods.Length), ("height", 120d), ("fps", 2d), ("duration", 0.5)));
        comp.Fill(Opts(("color", "#f2efe8")));
        for (var m = 0; m < Methods.Length; m++)
        {
            var x = m * 60d;
            var g = comp.Group();
            g.Rectangle(Opts(("point1", new[] { x + 8, 10d }), ("point2", new[] { x + 52, 70d }), ("color", "#3a86ffb0")));
            g.Region(Opts(("color", "#ffbe0b"), ("points", new object[] { new[] { x + 8, 70d }, new[] { x + 52, 70d }, new[] { x + 30, 110d } })));
            g.Circle(Opts(("origin", new[] { x + 30, 55d }), ("color", "#e5383bdd"), ("amount", 0.8), ("blend", Methods[m]),
                ("radius", motion.Nodes.Linear("real", 20, 18))));
        }

        return comp;
    }

    private SKBitmap[] RenderWithSynfig(string synfig, MotionComposition comp, string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "polson-synfig-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var sif = comp.SaveSif(Path.Combine(dir, name + ".sif"));
            var start = new ProcessStartInfo(synfig)
            {
                WorkingDirectory = Path.GetDirectoryName(synfig)!,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            foreach (var a in new[] { "-q", sif, "-t", "png", "-o", Path.Combine(dir, name + ".png") }) start.ArgumentList.Add(a);

            using var process = Process.Start(start)!;
            var err = process.StandardError.ReadToEndAsync();
            var outp = process.StandardOutput.ReadToEndAsync();
            Assert.True(process.WaitForExit(120_000), "synfig did not finish in two minutes");
            var log = string.Join("\n", (err.Result + outp.Result).Split('\n')
                .Where(l => !l.Contains("info:") && !l.Contains("Unable to find module") && l.Trim().Length > 0));
            if (log.Length > 0) output.WriteLine(log);
            Assert.True(process.ExitCode == 0, $"synfig exited {process.ExitCode}: {log}");
            Assert.DoesNotContain("error", log, StringComparison.OrdinalIgnoreCase);

            var files = Directory.GetFiles(dir, name + ".*.png").Order(StringComparer.Ordinal).ToArray();
            if (files.Length == 0) files = Directory.GetFiles(dir, name + ".png");
            return [.. files.Select(f => SKBitmap.Decode(f))];
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static double Differing(SKBitmap a, SKBitmap b, int tolerance, SKRectI cell)
    {
        using var ca = new SKBitmap(cell.Width, cell.Height);
        using var cb = new SKBitmap(cell.Width, cell.Height);
        a.ExtractSubset(ca, cell);
        b.ExtractSubset(cb, cell);
        return MotionCompositionTests.Differing(ca, cb, tolerance);
    }

    private static void Dump(SKBitmap ours, SKBitmap theirs, string name)
    {
        if (Environment.GetEnvironmentVariable("POLSON_SYNFIG_DUMP") is not { Length: > 0 } dump) return;
        Directory.CreateDirectory(dump);
        File.WriteAllBytes(Path.Combine(dump, $"ours-{name}.png"), ours.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        File.WriteAllBytes(Path.Combine(dump, $"theirs-{name}.png"), theirs.Encode(SKEncodedImageFormat.Png, 100).ToArray());
    }

    private static string? Synfig()
    {
        var path = Environment.GetEnvironmentVariable("POLSON_SYNFIG");
        if (string.IsNullOrEmpty(path))
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "reference"))) dir = dir.Parent;
            path = dir is null ? null : Path.Combine(dir.FullName, "reference", "apps", "synfig", "bin", "synfig.exe");
        }

        return path is not null && File.Exists(path) ? path : null;
    }
    #endregion
}
