namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Polson.Animation;

using SkiaSharp;

using Xunit;
using Xunit.Abstractions;

using static MotionCompositionTests;

/// <summary>
/// The skeleton deformation layer: what it keeps at rest, how it bends, what it refuses, and the whole
/// thing through Synfig's renderer.
/// </summary>
public class MotionDeformationTests(ITestOutputHelper output) : TestsRuntime
{
    #region Behaviour
    [Fact]
    public void AtRestItKeepsWhatTheBonesReachAndDropsTheRest()
    {
        var (comp, _) = Arm(new MotionToolkit(), bend: 0);
        using var frame = comp.Render(0);

        Assert.Equal(SKColor.Parse("#c9553d"), frame.SkBitmap.GetPixel(148, 150));   // on the upper arm, between stripes, untouched
        Assert.Equal(SKColor.Parse("#e5a93c"), frame.SkBitmap.GetPixel(300, 150));   // on the forearm, untouched
        Assert.Equal(SKColor.Parse("#f2efe8"), frame.SkBitmap.GetPixel(160, 230));   // the stray block, out of reach: gone
    }

    [Fact]
    public void ABentForearmCarriesItsArtwork()
    {
        var (comp, lower) = Arm(new MotionToolkit(), bend: 90);
        using var frame = comp.Render(1);

        // The forearm turns a quarter about the elbow at (220, 150): its middle goes from (280, 150) to (220, 210).
        var tip = (IDictionary<string, object?>)((IDictionary<string, object?>)lower.At(1))["tip"]!;
        Assert.Equal(220, (double)tip["x"]!, 3);
        Assert.Equal(270, (double)tip["y"]!, 3);
        Assert.Equal(SKColor.Parse("#e5a93c"), frame.SkBitmap.GetPixel(220, 220));
        Assert.Equal(SKColor.Parse("#f2efe8"), frame.SkBitmap.GetPixel(300, 150));   // where it was, now empty
    }

    [Fact]
    public void ItRefusesABlendAndSvg()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        comp.Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 50d, 0d })));
        Assert.Throws<ArgumentException>(() => comp.SkeletonDeformation(Opts(("blend", "multiply"))));

        comp.SkeletonDeformation(Opts(("desc", "bend")));
        var e = Assert.Throws<InvalidOperationException>(() => comp.ToSvg());
        Assert.Contains("'bend'", e.Message);
    }

    [Fact]
    public void ItNeedsBones()
    {
        var e = Assert.Throws<ArgumentException>(() => new MotionToolkit().Composition().SkeletonDeformation());
        Assert.Contains("no bones", e.Message);
    }
    #endregion

    #region Oracle
    [Fact]
    public void SynfigAndSkiaBendTheSame()
    {
        if (Synfig() is not { } synfig) return;

        var (comp, _) = Arm(new MotionToolkit(), bend: 80, fps: 4);
        var theirs = RenderWithSynfig(synfig, comp, "bend");
        Assert.Equal(comp.FrameCount, theirs.Length);

        var worst = 0d;
        for (var i = 0; i < theirs.Length; i++)
        {
            using var ours = comp.Render(i / comp.Fps);
            using var bitmap = theirs[i];
            Dump(ours.SkBitmap, bitmap, $"bend-{i}");
            var differing = Differing(ours.SkBitmap, bitmap, 48);
            output.WriteLine($"frame {i}: {differing:P2} differ by more than 48");
            worst = Math.Max(worst, differing);
        }

        Assert.True(worst < 0.02, $"worst frame differs in {worst:P2} of its pixels");
    }
    #endregion

    #region Private
    /// <summary>
    /// A striped two-part arm in a group with a deformation, over a page; a block below it out of the bones'
    /// reach. The forearm bends by <paramref name="bend"/> degrees at one second.
    /// </summary>
    private static (MotionComposition Comp, MotionBone Lower) Arm(MotionToolkit motion, double bend, double fps = 12)
    {
        var comp = motion.Composition(Opts(("width", 420d), ("height", 300d), ("fps", fps), ("duration", 2d)));
        comp.Fill(Opts(("color", "#f2efe8")));

        var upper = comp.Bone(Opts(("name", "upper"), ("from", new[] { 100d, 150d }), ("to", new[] { 220d, 150d }),
            ("width", 34d), ("tipwidth", 34d)));
        var lower = comp.Bone(Opts(("name", "lower"), ("parent", upper), ("from", new[] { 220d, 150d }), ("to", new[] { 340d, 150d }),
            ("width", 34d), ("tipwidth", 30d),
            ("turn", Animated("angle", (0, 0d, "halt"), (1, bend, "halt"), (2, 0d, "halt")))));

        var arm = comp.Group(Opts(("desc", "arm")));
        arm.Rectangle(Opts(("point1", new[] { 96d, 132d }), ("point2", new[] { 224d, 168d }), ("color", "#c9553d")));
        arm.Rectangle(Opts(("point1", new[] { 216d, 134d }), ("point2", new[] { 344d, 166d }), ("color", "#e5a93c")));
        for (var x = 110d; x < 340; x += 24)
            arm.Rectangle(Opts(("point1", new[] { x, 132d }), ("point2", new[] { x + 6, 168d }), ("color", "#15151a")));
        arm.Rectangle(Opts(("point1", new[] { 140d, 215d }), ("point2", new[] { 180d, 245d }), ("color", "#1f6f8b")));
        arm.SkeletonDeformation(Opts(("desc", "bend"), ("xSubdivisions", 40d), ("ySubdivisions", 40d)));
        return (comp, lower);
    }

    private SKBitmap[] RenderWithSynfig(string synfig, MotionComposition comp, string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "polson-synfig-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var sif = comp.SaveSif(Path.Combine(dir, name + ".sif"));
            if (Environment.GetEnvironmentVariable("POLSON_SYNFIG_DUMP") is { Length: > 0 } dump)
            {
                Directory.CreateDirectory(dump);
                File.Copy(sif, Path.Combine(dump, name + ".sif"), true);
            }

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
