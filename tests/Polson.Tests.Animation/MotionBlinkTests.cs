namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Linq;

using Polson.Animation;
using Polson.Drawing.Skia;

using SkiaSharp;

using Xunit;

/// <summary>
/// Blinks: the eye's closure (FACS AU45, AU43), irregular blink timing as an envelope, and Synfig's time loop for idle cycles.
/// </summary>
public class MotionBlinkTests : TestsRuntime
{
    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static MotionNodeFactory N => new MotionToolkit().Nodes;

    static float Closure(Dictionary<string, object?> head, string eye) => Convert.ToSingle(((IDictionary<string, object?>)head[eye]!)["closure"]);

    [Fact]
    public void AU45ClosesTheEyesAndOneSideIsAWink()
    {
        var head = Toolkit.CreateLoomisHead(0f, 0f, 300f, 0f);
        Assert.Equal(0f, Closure(head, "nearEye"));

        var blink = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU45"] = 0.6f });
        Assert.Equal(0.6f, Closure(blink, "nearEye"), 5);
        Assert.Equal(0.6f, Closure(blink, "farEye"), 5);

        // Past 1 is clamped, as every unit is; AU43 is the same lid held shut.
        Assert.Equal(1f, Closure(Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU43"] = 1f, ["AU45"] = 1f }), "nearEye"), 5);

        var wink = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU45"] = 1f }, new Dictionary<string, object?> { ["side"] = "near" });
        Assert.Equal(1f, Closure(wink, "nearEye"));
        Assert.Equal(0f, Closure(wink, "farEye"));
    }

    [Fact]
    public void ClosureBlendsAsAFractionNotALength()
    {
        var open = Toolkit.CreateLoomisHead(0f, 0f, 300f, 0f);
        var shut = Toolkit.ApplyActionUnits(open, new Dictionary<string, object?> { ["AU45"] = 1f });
        var half = Toolkit.BlendHead(open, open, shut, 0.5f);
        Assert.Equal(0.5f, Closure(half, "nearEye"), 4);
    }

    /// <summary>A shut eye shows no white: the lids meet, and the interior is not drawn.</summary>
    [Fact]
    public void AShutEyeShowsNoWhite()
    {
        int White(double amount)
        {
            var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 300d), ("duration", 1d)));
            comp.Fill(Opts(("color", "#808080")));
            comp.Face(Opts(("origin", new[] { 150d, 150d }), ("height", 260d), ("channels", Opts(("AU45", amount)))));
            using var frame = comp.Render(0);
            var bitmap = frame.SkBitmap;
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y) is var c && c.Red > 235 && c.Green > 238 && c.Blue > 240) count++;
            return count;
        }

        var open = White(0);
        Assert.True(open > 40,$"an open eye shows {open} white pixels");
        Assert.True(White(0.5) < open);
        Assert.Equal(0, White(1));
    }

    [Fact]
    public void BlinksAreIrregularButTheSameForTheSameSeed()
    {
        var a = N.Blinks(Opts(("end", 60d), ("seed", 7d)));
        var b = N.Blinks(Opts(("end", 60d), ("seed", 7d)));
        var c = N.Blinks(Opts(("end", 60d), ("seed", 8d)));
        Assert.Equal(a.Peaks, b.Peaks);
        Assert.NotEqual(a.Peaks, c.Peaks);

        // About one every four seconds, the gaps varied rather than equal, and every blink back open between them.
        Assert.InRange(a.Count, 10, 22);
        var gaps = a.Peaks.Zip(a.Peaks.Skip(1), (x, y) => y - x).ToArray();
        Assert.True(gaps.Distinct().Count() > gaps.Length / 2);
        Assert.All(gaps, g => Assert.True(g > 0.3 - 1e-9, $"a gap of {g}s is shorter than a blink"));
        Assert.Equal(1d, (double)a.At(a.Peaks[0]), 9);
        Assert.Equal(0d, (double)a.At(a.Peaks[0] + 0.25), 9);
    }

    [Fact]
    public void DeliberateBlinksAreKeptAndAvoidedTimesAreClear()
    {
        var e = N.Blinks(Opts(("end", 30d), ("every", 2d), ("at", new object[] { 5d, 12.5d }), ("avoid", new object[] { new object[] { 15d, 22d } })));
        Assert.Contains(5d, e.Peaks);
        Assert.Contains(12.5d, e.Peaks);
        Assert.DoesNotContain(e.Peaks, p => p > 14.9 && p < 22.1);
        Assert.DoesNotContain(e.Peaks, p => p != 5d && Math.Abs(p - 5d) < 0.4);
    }

    [Fact]
    public void DoubleBlinksFollowStraightAway()
    {
        var e = N.Blinks(Opts(("end", 40d), ("double", 1d)));
        var gaps = e.Peaks.Zip(e.Peaks.Skip(1), (x, y) => y - x).ToArray();
        Assert.Contains(gaps, g => Math.Abs(g - (0.1 + 0.05 + 0.15 + 0.05 + 0.1)) < 1e-9);
    }

    [Fact]
    public void BadBlinksAreRefusedByName()
    {
        Assert.Contains("'rate'", Assert.Throws<ArgumentException>(() => N.Blinks(Opts(("rate", 3d)))).Message);
        Assert.Throws<ArgumentException>(() => N.Blinks(Opts(("jitter", 2d))));
        Assert.Throws<ArgumentException>(() => N.Blinks(Opts(("every", 0.3d))));
        Assert.Throws<ArgumentException>(() => N.Blinks(Opts(("close", -0.1d))));
        Assert.Throws<ArgumentException>(() => N.Blinks(Opts(("avoid", new object[] { 3d }))));
        Assert.Throws<ArgumentException>(() => N.Blinks(Opts(("start", 5d), ("end", 5.01d), ("every", 4d), ("jitter", 0d))));
    }

    /// <summary>The worked table in Synfig's manual: link time 5, duration 3, local time 4, symmetrical.</summary>
    [Fact]
    public void TheTimeLoopIsSynfigs()
    {
        var clock = N.Linear("real", 1, 0);
        var loop = N.TimeLoop(clock, 3d, Opts(("linkTime", 5d), ("localTime", 4d)));
        double[] child = [7, 5, 6, 7, 5, 6, 7, 5, 6, 7, 5];
        for (var t = 0; t <= 10; t++) Assert.Equal(child[t], (double)loop.At(t), 9);

        Assert.Equal(5d, (double)N.TimeLoop(clock, 0d, Opts(("linkTime", 5d))).At(9), 9);
        Assert.Equal(4.5, (double)N.TimeLoop(clock, -2d, Opts(("linkTime", 5d))).At(0.5), 9);
        Assert.Throws<ArgumentException>(() => N.TimeLoop(3d, 1d));
        Assert.Throws<ArgumentException>(() => N.TimeLoop(clock, 1d, Opts(("offset", 1d))));
    }

    [Fact]
    public void AFaceBlinksOnItsAU45Channel()
    {
        var blinks = N.Blinks(Opts(("end", 10d), ("seed", 3d)));
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 300d), ("duration", 10d)));
        var face = comp.Face(Opts(("origin", new[] { 150d, 150d }), ("height", 260d), ("channels", Opts(("AU45", blinks)))));
        Assert.Equal(1f, Closure(face.HeadAt(blinks.Peaks[0]), "nearEye"), 4);
        // A second away: past a double blink's second closing too.
        Assert.Equal(0f, Closure(face.HeadAt(blinks.Peaks[0] + 1), "nearEye"), 4);
    }

    private static Dictionary<string, object?> Opts(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
}
