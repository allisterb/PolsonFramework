namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Polson.Animation;
using Polson.Drawing.Skia;

using SkiaSharp;

using Xunit;
using Xunit.Abstractions;

/// <summary>
/// SPIKE: Synfig's animation model with Skia drawing it. The unit tests pin the model; the oracle tests
/// render the same composition through <c>synfig.exe</c> and through ours and compare, so the port is
/// checked against Synfig rather than against a reading of its source. The oracle skips when the binary
/// is absent (it is not in the repository); set <c>POLSON_SYNFIG</c> to point at one elsewhere.
/// </summary>
public class MotionCompositionTests(ITestOutputHelper output) : TestsRuntime
{
    #region Model
    [Fact]
    public void HaltStopsAtEachKeyAndLinearRunsStraight()
    {
        var halt = Animated("real", (0, 0, "halt"), (1, 10, "halt"));
        var linear = Animated("real", (0, 0, "linear"), (1, 10, "linear"));

        Assert.Equal(5, (double)linear.At(0.5), 6);
        Assert.Equal(5, (double)halt.At(0.5), 6);
        Assert.True((double)halt.At(0.05) < 0.1, "an eased start barely moves in its first twentieth");
        Assert.True((double)linear.At(0.05) > 0.45);
        Assert.Equal(10, (double)halt.At(3), 6);
        Assert.Equal(0, (double)halt.At(-1), 6);
    }

    [Fact]
    public void ConstantHoldsUntilTheNextKey()
    {
        var hold = Animated("real", (0, 1, "constant"), (1, 5, "constant"), (2, 9, "constant"));

        Assert.Equal(1, (double)hold.At(0.99), 6);
        Assert.Equal(5, (double)hold.At(1), 6);
        Assert.Equal(5, (double)hold.At(1.5), 6);
    }

    [Fact]
    public void ClampedNeverOvershootsWhereAutoDoes()
    {
        // A value that climbs and then holds: a TCB spline swings past the hold, a clamped one does not.
        var clamped = Animated("real", (0, 0, "clamped"), (1, 10, "clamped"), (2, 10, "clamped"));
        var auto = Animated("real", (0, 0, "auto"), (1, 10, "auto"), (2, 10, "auto"));

        var clampedMax = Enumerable.Range(0, 201).Max(i => (double)clamped.At(i / 100d));
        var autoMax = Enumerable.Range(0, 201).Max(i => (double)auto.At(i / 100d));
        Assert.True(clampedMax <= 10 + 1e-9, $"clamped peaked at {clampedMax}");
        Assert.True(autoMax > 10.1, $"auto peaked at {autoMax}");
    }

    [Fact]
    public void FormulasEvaluateAsSynfigDefinesThem()
    {
        var n = new MotionToolkit().Nodes;
        Assert.Equal(13, (double)n.Linear("real", 3, 7).At(2), 9);
        Assert.Equal(2, (double)n.Sine(n.Linear("angle", 90, 0), 2).At(1), 9);

        var p = (IDictionary<string, object?>)n.Composite(n.Linear("real", 10, 0), 5).At(0.5);
        Assert.Equal(5d, p["x"]);
        Assert.Equal(5d, p["y"]);

        Assert.Equal(26, (double)n.Add("real", n.Linear("real", 3, 7), 0, 2).At(2), 9);
        var q = (IDictionary<string, object?>)n.Add("vector", new[] { 1d, 2d }, new[] { 10d, 20d }).At(0);
        Assert.Equal(11d, q["x"]);
        Assert.Equal(22d, q["y"]);
        Assert.Equal(15, (double)n.Scale("real", n.Linear("real", 10, 0), 1.5).At(1), 9);
        Assert.Equal("#40200080", n.Scale("color", "#80400080", 0.5).At(0));   // alpha untouched
    }

    [Theory]
    [InlineData("radious", "has no 'radious'")]
    [InlineData("colour", "has no 'colour'")]
    public void AnUnknownOptionIsRefusedByName(string key, string expected)
    {
        var comp = new MotionToolkit().Composition();
        var e = Assert.Throws<ArgumentException>(() => comp.Circle(new Dictionary<string, object?> { [key] = 4d }));
        Assert.Contains(expected, e.Message);
        Assert.Contains("radius", e.Message);
    }

    [Fact]
    public void ANodeOfTheWrongTypeIsRefused()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        var e = Assert.Throws<ArgumentException>(() =>
            comp.Circle(new Dictionary<string, object?> { ["radius"] = motion.Nodes.Composite(1d, 2d) }));
        Assert.Contains("takes a real", e.Message);
    }

    [Fact]
    public void AnUnknownEaseIsRefusedWithTheNames()
    {
        var e = Assert.Throws<ArgumentException>(() => Animated("real", (0, 0, "smooth"), (1, 1, "smooth")));
        Assert.Contains("'smooth'", e.Message);
        Assert.Contains("clamped", e.Message);
    }

    [Fact]
    public void ASharedNodeIsWrittenOnceAndReferenced()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        var bounce = Animated("real", (0, 10, "halt"), (1, 40, "halt"));
        comp.Circle(Opts(("origin", new[] { 100d, 100d }), ("radius", bounce)));
        comp.Circle(Opts(("origin", new[] { 200d, 100d }), ("radius", bounce)));

        var sif = comp.ToSif();
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(sif, "<animated "));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sif, "use=\"v1\"").Count);
    }

    [Fact]
    public void ACircleLandsWhereItsOriginSays()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 200d), ("height", 100d), ("duration", 1d)));
        comp.Circle(Opts(("origin", motion.Nodes.Composite(motion.Nodes.Linear("real", 100, 40), 50d)),
            ("radius", 8d), ("color", "#ff0000")));

        using var frame = comp.Render(0.5);
        var (x, y) = Centroid(frame.SkBitmap, c => c.Red > 128 && c.Alpha > 128);
        Assert.Equal(90, x, 0);
        Assert.Equal(50, y, 0);
    }
    #endregion

    #region Oracle
    /// <summary>
    /// Every layer and node the spike covers, through both renderers, frame by frame. What it measures
    /// is the share of pixels that differ by more than an antialiasing step.
    /// </summary>
    [Fact]
    public void SynfigAndSkiaDrawTheSameFrames()
    {
        if (Synfig() is not { } synfig) return;

        var comp = OracleScene(new MotionToolkit());
        var theirs = RenderWithSynfig(synfig, comp, "scene");
        Assert.Equal(comp.FrameCount, theirs.Length);

        var worst = 0d;
        for (var i = 0; i < theirs.Length; i++)
        {
            using var ours = comp.Render(i / comp.Fps);
            using var bitmap = theirs[i];
            var differing = Differing(ours.SkBitmap, bitmap, 48);
            if (Environment.GetEnvironmentVariable("POLSON_SYNFIG_DUMP") is { Length: > 0 } dump)
            {
                Directory.CreateDirectory(dump);
                File.WriteAllBytes(Path.Combine(dump, $"ours-{i:00}.png"), ours.SkBitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray());
                File.WriteAllBytes(Path.Combine(dump, $"synfig-{i:00}.png"), bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray());
            }
            worst = Math.Max(worst, differing);
            var strict = Differing(ours.SkBitmap, bitmap, 8);
            output.WriteLine($"frame {i,2} ({i / comp.Fps:0.000}s): {differing:P2} differ by more than 48, {strict:P2} by more than 8");
        }

        Assert.True(worst < 0.01, $"worst frame differs in {worst:P2} of its pixels");
    }

    /// <summary>
    /// The interpolation port, read off pixels: one disc per ease, each driven along x by the same keys,
    /// its centre measured in Synfig's render and compared with the value our node gives.
    /// </summary>
    [Fact]
    public void SynfigInterpolatesTheWayOurNodesDo()
    {
        if (Synfig() is not { } synfig) return;

        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 400d), ("height", 300d), ("fps", 12d), ("duration", 3d)));
        comp.Fill(Opts(("color", "#ffffff")));
        string[] eases = ["clamped", "halt", "linear", "auto", "constant"];
        var colors = new[] { "#ff0000", "#00ff00", "#0000ff", "#ff00ff", "#00ffff" };
        var nodes = new List<MotionNode>();

        for (var k = 0; k < eases.Length; k++)
        {
            // Uneven spacing in time and value, so the tangent rescaling between segments is exercised.
            var x = Animated("real", (0, 40, eases[k]), (0.75, 300, eases[k]), (1.25, 320, eases[k]), (3, 60, eases[k]));
            nodes.Add(x);
            comp.Circle(Opts(("origin", motion.Nodes.Composite(x, 40d + k * 55)), ("radius", 9d), ("color", colors[k])));
        }

        var theirs = RenderWithSynfig(synfig, comp, "eases");
        var worst = 0d;
        for (var i = 0; i < theirs.Length; i++)
        {
            using var bitmap = theirs[i];
            for (var k = 0; k < eases.Length; k++)
            {
                var want = SKColor.Parse(colors[k]);
                var (cx, _) = Centroid(bitmap, c => Near(c, want));
                var expected = (double)nodes[k].At(i / comp.Fps);
                worst = Math.Max(worst, Math.Abs(cx - expected));
                if (Math.Abs(cx - expected) > 0.5)
                    output.WriteLine($"{eases[k]} at {i / comp.Fps:0.000}s: synfig {cx:0.00}, ours {expected:0.00}");
            }
        }

        output.WriteLine($"worst centre error {worst:0.000}px over {theirs.Length} frames");
        Assert.True(worst < 0.5, $"worst centre error {worst:0.000}px");
    }
    #endregion

    #region Private
    internal static MotionComposition OracleScene(MotionToolkit motion)
    {
        var n = motion.Nodes;
        var comp = motion.Composition(Opts(("width", 480d), ("height", 270d), ("fps", 12d), ("duration", 2d)));
        comp.Fill(Opts(("color", "#f2efe8")));

        // A ball on a sine path: composite of linear and sine, lowered by a group's offset.
        var lane = comp.Group(Opts(("offset", new[] { 0d, 135d })));
        lane.Circle(Opts(
            ("origin", n.Composite(n.Linear("real", 180, 60), n.Sine(n.Linear("angle", 180, 0), -60))),
            ("radius", 18d), ("color", "#1f6f8b")));

        // A region whose points move: a squash on the way down.
        var top = Animated("vector", (0, new[] { 330d, 60d }, "halt"), (2, new[] { 330d, 120d }, "halt"));
        comp.Region(Opts(("color", "#c9553d"), ("points", new object[]
        {
            Point(top, new[] { 60d, 0d }),
            Point(new[] { 400d, 170d }, new[] { 0d, 60d }),
            Point(new[] { 330d, 230d }, new[] { -60d, 0d }),
            Point(new[] { 260d, 170d }, new[] { 0d, -60d }),
        })));

        // A tapering stroke: Stanchfield's thick and thin, as a width per point.
        comp.Outline(Opts(("color", "#15151a"), ("width", 10d), ("points", new object[]
        {
            new Dictionary<string, object?> { ["point"] = new[] { 40d, 240d }, ["t1"] = new[] { 120d, -90d }, ["width"] = 0.2 },
            new Dictionary<string, object?> { ["point"] = new[] { 200d, 220d }, ["t1"] = new[] { 120d, 30d }, ["width"] = 1.4 },
            new Dictionary<string, object?> { ["point"] = new[] { 300d, 250d }, ["t1"] = new[] { 60d, 60d }, ["width"] = 0.2 },
        })));

        // A keyed offset laid on a formula, a scaled radius and a darkened colour: add and scale.
        var drift = n.Composite(n.Linear("real", 60, 300), 230d);
        var bob = Animated("vector", (0, new[] { 0d, 0d }, "halt"), (1, new[] { 0d, -40d }, "halt"), (2, new[] { 0d, 0d }, "halt"));
        comp.Circle(Opts(
            ("origin", n.Add("vector", drift, bob)),
            ("radius", n.Scale("real", Animated("real", (0, 10d, "linear"), (2, 30d, "linear")), 0.5)),
            ("color", n.Scale("color", "#e5a93c", 0.6))));

        // A bar growing from its baseline, and a rectangle given with its corners the other way round.
        comp.Rectangle(Opts(("point1", new[] { 20d, 260d }), ("color", "#5fb49c"),
            ("point2", Animated("vector", (0.2, new[] { 50d, 260d }, "halt"), (1.6, new[] { 50d, 150d }, "halt")))));
        comp.Rectangle(Opts(("point1", new[] { 120d, 30d }), ("point2", new[] { 70d, 10d }), ("expand", 3d), ("color", "#9b8ec4")));

        // A group that turns about its own centre and fades, holding a smaller pair.
        var g = comp.Group(Opts(("origin", new[] { 420d, 70d }), ("offset", new[] { 420d, 70d }),
            ("angle", n.Linear("angle", 90, 0)), ("scale", new[] { 1d, 0.6d }), ("amount", 0.8)));
        g.Circle(Opts(("origin", new[] { 450d, 70d }), ("radius", 10d), ("color", "#e5a93c")));
        g.Outline(Opts(("color", "#15151a"), ("width", 4d), ("points", new object[]
        {
            new Dictionary<string, object?> { ["point"] = new[] { 380d, 70d } },
            new Dictionary<string, object?> { ["point"] = new[] { 460d, 70d } },
        })));

        // A picture whose bottom-right corner is keyed, so it grows; and the same picture mirrored.
        var picture = Picture();
        comp.Image(Opts(("image", picture), ("tl", new[] { 150d, 20d }),
            ("br", Animated("vector", (0, new[] { 210d, 60d }, "halt"), (2, new[] { 270d, 100d }, "halt")))));
        comp.Image(Opts(("image", picture), ("tl", new[] { 150d, 190d }), ("br", new[] { 90d, 230d }), ("amount", 0.7)));

        // A cutout piece: a triangle of the picture, swinging about its corner like a limb about a joint.
        comp.Cutout(picture, new object[] { new[] { 0d, 0d }, new[] { 60d, 10d }, new[] { 20d, 40d } },
            Opts(("origin", new[] { 0d, 0d }), ("offset", new[] { 160d, 120d }), ("angle", n.Linear("angle", 45, 0)), ("desc", "limb")));

        return comp;
    }

    /// <summary>A small picture with flat blocks, a soft edge and a transparent hole, so placement, alpha and mirroring all show.</summary>
    internal static SkiaBitmapWrapper Picture()
    {
        var bitmap = new SKBitmap(new SKImageInfo(60, 40, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { IsAntialias = false };
        paint.Color = SKColor.Parse("#2a6f97");
        canvas.DrawRect(0, 0, 40, 40, paint);
        paint.Color = SKColor.Parse("#e76f51");
        canvas.DrawRect(40, 0, 20, 25, paint);
        paint.Color = SKColor.Parse("#f4a26180");
        canvas.DrawRect(40, 25, 20, 15, paint);
        paint.Color = SKColors.Transparent;
        paint.BlendMode = SKBlendMode.Src;
        canvas.DrawRect(12, 12, 14, 14, paint);
        return new SkiaBitmapWrapper(bitmap);
    }

    private static Dictionary<string, object?> Point(object point, double[] tangent) =>
        new() { ["point"] = point, ["t1"] = tangent };

    internal static MotionAnimated Animated<T>(string type, params (double Time, T Value, string Ease)[] keys) =>
        new MotionToolkit().Nodes.Animated(type, keys
            .Select(k => (object)new Dictionary<string, object?> { ["time"] = k.Time, ["value"] = k.Value, ["ease"] = k.Ease })
            .ToArray());

    internal static Dictionary<string, object?> Opts(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

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
            return [.. files.Select(f => SKBitmap.Decode(f))];
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    internal static double Differing(SKBitmap a, SKBitmap b, int tolerance)
    {
        Assert.Equal(a.Width, b.Width);
        Assert.Equal(a.Height, b.Height);
        var count = 0;
        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                var p = Flatten(a.GetPixel(x, y));
                var q = Flatten(b.GetPixel(x, y));
                if (Math.Abs(p.Red - q.Red) > tolerance || Math.Abs(p.Green - q.Green) > tolerance || Math.Abs(p.Blue - q.Blue) > tolerance)
                    count++;
            }
        }

        return count / (double)(a.Width * a.Height);
    }

    /// <summary>Composites onto white, so transparent pixels compare as the page they would sit on.</summary>
    private static SKColor Flatten(SKColor c)
    {
        byte Mix(byte v) => (byte)Math.Round(v * c.Alpha / 255d + 255 * (1 - c.Alpha / 255d));
        return new SKColor(Mix(c.Red), Mix(c.Green), Mix(c.Blue));
    }

    internal static bool Near(SKColor c, SKColor want) =>
        c.Alpha > 200 && Math.Abs(c.Red - want.Red) < 40 && Math.Abs(c.Green - want.Green) < 40 && Math.Abs(c.Blue - want.Blue) < 40;

    internal static (double X, double Y) Centroid(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        double sx = 0, sy = 0, n = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (!match(bitmap.GetPixel(x, y))) continue;
                sx += x + 0.5;
                sy += y + 0.5;
                n++;
            }
        }

        return n == 0 ? (double.NaN, double.NaN) : (sx / n, sy / n);
    }
    #endregion
}
