namespace Polson.Tests.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Animation;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>Character.track(clip)</c>: a recorded clip flattened onto the page, and a drawn rig following it. Needs the
/// stock bodies and clips from <c>tools/bootstrap.py</c>, and fails rather than skips without them.
/// </summary>
[Collection(TomasRig.Name)]
public class CharacterTrackTests : TestsRuntime
{
    readonly ITestOutputHelper output;

    public CharacterTrackTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void TestJumpingJacksReadsAsJumpingJacks()
    {
        var track = new CharacterToolkit(null).Track("Jumping Jacks", new Dictionary<string, object?> { ["fps"] = 12 });
        Assert.Equal(16, track.Frames);
        Assert.Equal(1.25, track.Duration, 3);

        double Part(int f, string p) => (double)((Dictionary<string, object?>)((Dictionary<string, object?>)track.At(f)["parts"]!)[p]!)["angleDeg"]!;
        double Lift(int f) => (double)((Dictionary<string, object?>)track.At(f)["root"]!)["y"]!;

        // Arms overhead at the start, at the sides halfway; the figure's left on the page's right.
        Assert.InRange(Part(0, "leftUpperArm"), -100, -80);
        Assert.InRange(Part(0, "rightUpperArm"), -100, -80);
        var down = Enumerable.Range(0, track.Frames).MaxBy(f => Part(f, "leftUpperArm"));
        Assert.InRange(Part(down, "leftUpperArm"), 30, 90);     // down and out, to page right
        Assert.InRange(Part(down, "rightUpperArm"), 90, 150);   // its mirror
        Assert.True(Enumerable.Range(0, track.Frames).Min(Lift) < -0.1, "the jump lifts the hips");

        var inPlane = track.InPlane;
        output.WriteLine(string.Join(", ", inPlane.Select(kv => $"{kv.Key} {kv.Value}")));
        Assert.True((double)inPlane["leftUpperArm"]! > 0.95);
        Assert.Contains(track.Warnings, w => w.StartsWith("leftFoot"));
        Assert.DoesNotContain(track.Warnings, w => w.Contains("Arm"));
    }

    [Fact]
    public void TestADrawnRigFollowsTheClip()
    {
        var track = new CharacterToolkit(null).Track("Jumping Jacks", new Dictionary<string, object?> { ["fps"] = 12 });
        var comp = new MotionToolkit().Composition(new Dictionary<string, object?> { ["width"] = 300d, ["height"] = 400d, ["duration"] = track.Duration });
        var rig = comp.RigFromDrawing(Figure(), Landmarks(), new Dictionary<string, object?> { ["follow"] = track });

        foreach (var f in new[] { 0, 5, 9, 15 })
        {
            var parts = (Dictionary<string, object?>)track.At(f)["parts"]!;
            foreach (var p in new[] { "spine", "leftUpperArm", "rightForearm", "leftThigh", "rightShin" })
            {
                var want = (double)((Dictionary<string, object?>)parts[p]!)["angleDeg"]!;
                var got = (double)((IDictionary<string, object?>)rig.Bone(p).At(track.Times[f]))["angle"]!;
                var off = got - want - (360 * Math.Round((got - want) / 360));
                Assert.True(Math.Abs(off) < 0.01, $"{p} at frame {f}: {got:0.00} against {want:0.00}");
            }
        }
    }

    [Fact]
    public void TestRefusalsNameTheFix()
    {
        var kit = new CharacterToolkit(null);
        Assert.Contains("Jumping Jacks", Assert.Throws<ArgumentException>(() => kit.Track("Jumping Jack")).Message);
        Assert.Contains("yawDeg", Assert.Throws<ArgumentException>(() => kit.Track("Walk", new Dictionary<string, object?> { ["fsp"] = 12 })).Message);
        Assert.Contains("fps", Assert.Throws<ArgumentException>(() => kit.Track("Walk", new Dictionary<string, object?> { ["fps"] = 0 })).Message);
        Assert.Contains("within", Assert.Throws<ArgumentException>(() => kit.Track("Walk", new Dictionary<string, object?> { ["to"] = 99 })).Message);
    }

    /// <summary>The stick figure <c>MotionRigTests</c> uses, arms out, 300 × 400.</summary>
    static SkiaBitmapWrapper Figure()
    {
        var bmp = new SKBitmap(new SKImageInfo(300, 400, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var c = new SKCanvas(bmp);
        c.Clear(SKColors.Transparent);
        using var p = new SKPaint { IsAntialias = true, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke, Color = SKColors.SteelBlue };
        void Line(float x0, float y0, float x1, float y1, float width) { p.StrokeWidth = width; c.DrawLine(x0, y0, x1, y1, p); }
        Line(150, 110, 150, 220, 50);
        Line(150, 110, 70, 115, 18); Line(70, 115, 20, 120, 14);
        Line(150, 110, 230, 115, 18); Line(230, 115, 280, 120, 14);
        Line(135, 220, 130, 300, 20); Line(130, 300, 128, 370, 16);
        Line(165, 220, 170, 300, 20); Line(170, 300, 172, 370, 16);
        p.Style = SKPaintStyle.Fill;
        c.DrawCircle(150, 70, 28, p);
        return new SkiaBitmapWrapper(bmp);
    }

    static Dictionary<string, object?> Landmarks() => new()
    {
        ["nose"] = new[] { 150d, 75d },
        ["leftShoulder"] = new[] { 170d, 110d }, ["rightShoulder"] = new[] { 130d, 110d },
        ["leftElbow"] = new[] { 230d, 115d }, ["rightElbow"] = new[] { 70d, 115d },
        ["leftWrist"] = new[] { 270d, 119d }, ["rightWrist"] = new[] { 30d, 119d },
        ["leftHip"] = new[] { 165d, 220d }, ["rightHip"] = new[] { 135d, 220d },
        ["leftKnee"] = new[] { 170d, 300d }, ["rightKnee"] = new[] { 130d, 300d },
        ["leftAnkle"] = new[] { 172d, 360d }, ["rightAnkle"] = new[] { 128d, 360d },
        ["leftFootIndex"] = new[] { 172d, 372d }, ["rightFootIndex"] = new[] { 128d, 372d },
    };
}
