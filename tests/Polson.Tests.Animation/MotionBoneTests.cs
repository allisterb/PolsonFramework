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
/// Bones: the frames worked by hand, the bindings, the refusals, and the whole rig through Synfig's
/// renderer and through the SVG route.
/// </summary>
public class MotionBoneTests(ITestOutputHelper output) : TestsRuntime
{
    #region Frames
    [Fact]
    public void AChildRidesItsParentsTurn()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        var upper = comp.Bone(Opts(("from", new[] { 100d, 100d }), ("to", new[] { 200d, 100d }),
            ("turn", motion.Nodes.Linear("angle", 90, 0))));
        var lower = comp.Bone(Opts(("parent", upper), ("from", new[] { 200d, 100d }), ("to", new[] { 280d, 100d })));

        AssertPoint(lower.Rest, "origin", 200, 100);
        AssertPoint(lower.Rest, "tip", 280, 100);

        // A quarter turn clockwise on screen about the shoulder: the elbow drops below it, the hand with it.
        AssertPoint(upper.At(1), "tip", 100, 200);
        AssertPoint(lower.At(1), "origin", 100, 200);
        AssertPoint(lower.At(1), "tip", 100, 280);
        Assert.Equal(90, (double)((IDictionary<string, object?>)lower.At(1))["angle"]!, 6);
    }

    [Fact]
    public void StretchLengthensTheBoneAndCarriesItsChildrenWithoutScalingThem()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        var upper = comp.Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 100d, 0d }), ("stretch", motion.Nodes.Linear("real", 1, 1))));
        var lower = comp.Bone(Opts(("parent", upper), ("from", new[] { 100d, 0d }), ("to", new[] { 150d, 0d })));

        AssertPoint(upper.At(1), "tip", 200, 0);
        AssertPoint(lower.At(1), "origin", 200, 0);
        AssertPoint(lower.At(1), "tip", 250, 0);   // still 50 long
    }

    [Fact]
    public void AChildOfARotatedParentIsGivenInWorldJoints()
    {
        var comp = new MotionToolkit().Composition();
        var upper = comp.Bone(Opts(("from", new[] { 50d, 50d }), ("to", new[] { 50d, 150d })));   // pointing down
        var lower = comp.Bone(Opts(("parent", upper), ("from", new[] { 50d, 150d }), ("to", new[] { 130d, 210d })));

        AssertPoint(lower.Rest, "origin", 50, 150);
        AssertPoint(lower.Rest, "tip", 130, 210);
        Assert.Equal(100, (double)((IDictionary<string, object?>)lower.Rest)["length"]!, 6);
    }
    #endregion

    #region Bindings
    [Fact]
    public void ABoundGroupFollowsItsBoneFromTheRestPose()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 300d), ("height", 300d)));
        var arm = comp.Bone(Opts(("from", new[] { 100d, 100d }), ("to", new[] { 200d, 100d }),
            ("turn", motion.Nodes.Linear("angle", 90, 0))));
        var g = comp.Group(Opts(("bone", arm)));
        g.Circle(Opts(("origin", new[] { 150d, 100d }), ("radius", 8d), ("color", "#ff0000")));

        using var rest = comp.Render(0);
        using var turned = comp.Render(1);
        var (rx, ry) = Centroid(rest.SkBitmap, c => c.Red > 128 && c.Alpha > 128);
        var (tx, ty) = Centroid(turned.SkBitmap, c => c.Red > 128 && c.Alpha > 128);
        Assert.Equal(150, rx, 0);
        Assert.Equal(100, ry, 0);
        Assert.Equal(100, tx, 0);
        Assert.Equal(150, ty, 0);
    }

    [Fact]
    public void ABoneLinkedPointAndAnEvenInfluenceLandWhereTheyShould()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        var a = comp.Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 100d, 0d }), ("turn", motion.Nodes.Linear("angle", 90, 0))));
        var b = comp.Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 100d, 0d })));

        var linked = motion.Nodes.BoneLink(a, new[] { 100d, 0d });
        var halfway = motion.Nodes.BoneInfluence(new object[] { new object[] { a, 1d }, new object[] { b, 1d } }, new[] { 100d, 0d });

        AssertXY(linked.At(0), 100, 0);
        AssertXY(linked.At(1), 0, 100);
        AssertXY(halfway.At(1), 50, 50);   // the average of the two frames, not a rotation of 45°
    }

    [Fact]
    public void ABoundSplinePointCarriesItsTangentsToo()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 300d), ("height", 300d)));
        var arm = comp.Bone(Opts(("from", new[] { 150d, 150d }), ("to", new[] { 250d, 150d }), ("turn", motion.Nodes.Linear("angle", 90, 0))));
        comp.Outline(Opts(("color", "#000000"), ("width", 4d), ("points", new object[]
        {
            new Dictionary<string, object?> { ["point"] = new[] { 150d, 150d } },
            new Dictionary<string, object?> { ["point"] = new[] { 250d, 150d }, ["t1"] = new[] { 0d, 120d }, ["bone"] = arm },
        })));

        // Turned a quarter, the tip is at (150, 250) and its tangent must turn with it, to (−120, 0): the curve
        // then bulges right, out to x ≈ 168 at y ≈ 224. Left unturned, the tangent would keep it on x = 150.
        using var turned = comp.Render(1);
        var reach = Enumerable.Range(150, 60).Where(x => turned.SkBitmap.GetPixel(x, 224).Alpha > 128).DefaultIfEmpty(0).Max();
        Assert.InRange(reach, 165, 172);
    }
    #endregion

    #region Refusals
    [Fact]
    public void ABoundGroupRefusesItsOwnTransform()
    {
        var comp = new MotionToolkit().Composition();
        var bone = comp.Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 10d, 0d })));
        var e = Assert.Throws<ArgumentException>(() => comp.Group(Opts(("bone", bone), ("angle", 30d))));
        Assert.Contains("drop angle", e.Message);
    }

    [Fact]
    public void JointsAndSynfigsFieldsAreNotMixed()
    {
        var comp = new MotionToolkit().Composition();
        var e = Assert.Throws<ArgumentException>(() =>
            comp.Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 10d, 0d }), ("angle", 30d))));
        Assert.Contains("not both", e.Message);
    }

    [Fact]
    public void ABoneLinkTakesAFixedPoint()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        var bone = comp.Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 10d, 0d })));
        var e = Assert.Throws<ArgumentException>(() => motion.Nodes.BoneLink(bone, motion.Nodes.Linear("vector", new[] { 1d, 0d })));
        Assert.Contains("rest pose", e.Message);
    }

    [Fact]
    public void AParentFromAnotherCompositionIsRefused()
    {
        var motion = new MotionToolkit();
        var other = motion.Composition().Bone(Opts(("from", new[] { 0d, 0d }), ("to", new[] { 10d, 0d })));
        var e = Assert.Throws<ArgumentException>(() =>
            motion.Composition().Bone(Opts(("parent", other), ("from", new[] { 10d, 0d }), ("to", new[] { 20d, 0d }))));
        Assert.Contains("another composition", e.Message);
    }
    #endregion

    #region Oracle
    /// <summary>The whole rig, frame by frame, through Synfig and through us.</summary>
    [Fact]
    public void SynfigAndSkiaPoseTheSameRig()
    {
        if (Synfig() is not { } synfig) return;

        var comp = Rig(new MotionToolkit(), skeleton: false);   // Synfig's skeleton is an editor guide it does not render
        var theirs = RenderWithSynfig(synfig, comp, "rig");
        Assert.Equal(comp.FrameCount, theirs.Length);

        var worst = 0d;
        for (var i = 0; i < theirs.Length; i++)
        {
            using var ours = comp.Render(i / comp.Fps);
            using var bitmap = theirs[i];
            Dump(ours.SkBitmap, bitmap, $"rig-{i}");
            var differing = Differing(ours.SkBitmap, bitmap, 48);
            output.WriteLine($"frame {i}: {differing:P2} differ by more than 48");
            worst = Math.Max(worst, differing);
        }

        Assert.True(worst < 0.015, $"worst frame differs in {worst:P2} of its pixels");
    }

    /// <summary>The skeleton is written as Synfig's guide: excluded from rendering, with only the parameters it has.</summary>
    [Fact]
    public void TheSkeletonIsWrittenAsAGuide()
    {
        var comp = Rig(new MotionToolkit());
        var dir = Path.Combine(Path.GetTempPath(), "polson-sif-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var sif = File.ReadAllText(comp.SaveSif(Path.Combine(dir, "rig.sif")));
            Assert.Contains("type=\"skeleton\" active=\"true\" exclude_from_rendering=\"true\"", sif);
            Assert.Contains("<bone_root type=\"bone_object\"", sif);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sif, "<bone type=\"bone_object\" guid=\"[0-9A-F]{32}\">").Count);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TheExportedSvgPosesTheSameRig()
    {
        var comp = Rig(new MotionToolkit());
        var svg = comp.ToSvg();
        var worst = 0d;
        foreach (var t in new[] { 0, 0.3, 0.75, 1.2, 2 })
        {
            using var ours = comp.Render(t);
            using var theirs = SKBitmap.Decode(SvgRenderPipeline.RenderToImage(svg, comp.Width, comp.Height, "png", 100, null, TimeSpan.FromSeconds(t)));
            Dump(ours.SkBitmap, theirs, $"rigsvg-{t}");
            var differing = Differing(ours.SkBitmap, theirs, 48);
            output.WriteLine($"{t}s: {differing:P2}");
            worst = Math.Max(worst, differing);
        }

        Assert.True(worst < 0.02, $"worst {worst:P2}");
    }
    #endregion

    #region Private
    /// <summary>A shoulder and an elbow with keyed turns; everything that can follow a bone, following one.</summary>
    private static MotionComposition Rig(MotionToolkit motion, bool skeleton = true)
    {
        var n = motion.Nodes;
        var comp = motion.Composition(Opts(("width", 480d), ("height", 300d), ("fps", 6d), ("duration", 2d)));
        comp.Fill(Opts(("color", "#f2efe8")));

        var swing = Animated("angle", (0, -20d, "halt"), (1, 50d, "halt"), (2, -20d, "halt"));
        var bend = Animated("angle", (0, 0d, "halt"), (1, 80d, "halt"), (2, 0d, "halt"));
        var upper = comp.Bone(Opts(("name", "upper"), ("from", new[] { 120d, 90d }), ("to", new[] { 240d, 90d }), ("turn", swing)));
        var lower = comp.Bone(Opts(("name", "lower"), ("parent", upper), ("from", new[] { 240d, 90d }), ("to", new[] { 340d, 90d }), ("turn", bend),
            ("stretch", Animated("real", (0, 1d, "halt"), (1, 1.2d, "halt"), (2, 1d, "halt")))));

        // A sleeve whose points are skinned: the elbow's points follow both bones half and half.
        comp.Region(Opts(("color", "#9b8ec4"), ("points", new object[]
        {
            Bound(new[] { 120d, 78d }, upper), Bound(new[] { 240d, 76d }, new object[] { new object[] { upper, 1d }, new object[] { lower, 1d } }),
            Bound(new[] { 340d, 82d }, lower), Bound(new[] { 340d, 98d }, lower),
            Bound(new[] { 240d, 104d }, new object[] { new object[] { upper, 1d }, new object[] { lower, 1d } }), Bound(new[] { 120d, 102d }, upper),
        })));

        // A bound group, and a cutout piece bound to the forearm.
        var hand = comp.Group(Opts(("bone", lower)));
        hand.Circle(Opts(("origin", new[] { 350d, 90d }), ("radius", 14d), ("color", "#e5a93c")));
        comp.Cutout(Picture(), new object[] { new[] { 250d, 80d }, new[] { 300d, 80d }, new[] { 300d, 100d }, new[] { 250d, 100d } },
            Opts(("tl", new[] { 250d, 70d }), ("br", new[] { 310d, 110d }), ("bone", lower)));

        // A point linked to the upper arm, and the skeleton drawn over everything.
        comp.Circle(Opts(("origin", n.BoneLink(upper, new[] { 180d, 60d })), ("radius", 6d), ("color", "#1f6f8b")));
        if (skeleton) comp.Skeleton(Opts(("color", "#15151a80")));
        return comp;
    }

    private static Dictionary<string, object?> Bound(double[] point, object bone) => new() { ["point"] = point, ["bone"] = bone };

    private static void AssertPoint(object pose, string key, double x, double y) =>
        AssertXY(((IDictionary<string, object?>)pose)[key]!, x, y);

    private static void AssertXY(object point, double x, double y)
    {
        var p = (IDictionary<string, object?>)point;
        Assert.Equal(x, (double)p["x"]!, 6);
        Assert.Equal(y, (double)p["y"]!, 6);
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
