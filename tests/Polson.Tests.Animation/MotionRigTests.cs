namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Linq;

using Polson.Animation;
using Polson.Drawing.Skia;

using SkiaSharp;

using Xunit;

using static MotionCompositionTests;

/// <summary>
/// <c>rigFromDrawing</c> on a drawn figure with landmarks given by hand, so no detector is needed: the bones it
/// makes, what it reaches, how poses drive it, and what it refuses.
/// </summary>
public class MotionRigTests : TestsRuntime
{
    #region Rigging
    [Fact]
    public void ItMakesTheBodyPartBonesAndReachesTheWholeDrawing()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d)));
        var rig = comp.RigFromDrawing(Figure(), Landmarks());

        Assert.Equal(15, rig.Names.Length);
        Assert.Equal("hips", rig.Names[0]);
        Assert.Contains("leftForearm", rig.Names);
        Assert.Equal(15, comp.Bones.Count);
        Assert.True(rig.Reach > 0.995, $"reach {rig.Reach:P1}");
        Assert.Equal(0, rig.Keyed);   // a cutout: nothing to key
    }

    [Fact]
    public void AtRestItReproducesTheDrawing()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d)));
        var figure = Figure();
        comp.RigFromDrawing(figure, Landmarks());

        using var frame = comp.Render(0);
        int same = 0, total = 0;
        for (var y = 0; y < 400; y++)
        {
            for (var x = 0; x < 300; x++)
            {
                var c = figure.Bitmap.GetPixel(x, y);
                if (c.Alpha < 250) continue;
                total++;
                var r = frame.SkBitmap.GetPixel(x, y);
                if (Math.Abs(c.Red - r.Red) <= 8 && Math.Abs(c.Green - r.Green) <= 8 && Math.Abs(c.Blue - r.Blue) <= 8) same++;
            }
        }

        Assert.True(same / (double)total > 0.995, $"{same}/{total}");
    }

    [Fact]
    public void APoseCarriesTheHandAndKeepsTheBonesNamed()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d), ("duration", 1d)));
        var rig = comp.RigFromDrawing(Figure(), Landmarks(), Opts(("poses", new object[]
        {
            Opts(("time", 0d)),
            Opts(("time", 1d), ("leftUpperArm", -60d)),   // the figure's left arm is on the page's right; negative is anticlockwise: up
        })));

        var restTip = Tip(rig.Bone("leftHand").At(0));
        var raisedTip = Tip(rig.Bone("leftHand").At(1));
        Assert.True(raisedTip.Y < restTip.Y - 40, $"hand rose from {restTip.Y:0} to {raisedTip.Y:0}");
        Assert.Equal(Tip(rig.Bone("rightHand").At(0)), Tip(rig.Bone("rightHand").At(1)));   // left out of the pose: at rest
    }

    [Fact]
    public void MoveCarriesTheWholeFigure()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 300d), ("height", 400d), ("duration", 1d)));
        var rig = comp.RigFromDrawing(Figure(), Landmarks(), Opts(("move", motion.Nodes.Linear("vector", new[] { 40d, 0d }))));
        Assert.Equal(Tip(rig.Bone("leftFoot").At(0)).X + 40, Tip(rig.Bone("leftFoot").At(1)).X, 6);
    }

    [Fact]
    public void AnOpaqueGroundIsKeyed()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d)));
        var rig = comp.RigFromDrawing(Figure(ground: SKColors.White), Landmarks());
        Assert.True(rig.Keyed > 50_000, $"keyed {rig.Keyed}");
        Assert.True(rig.Reach > 0.99);
    }
    #endregion

    #region Following a recorded motion
    [Fact]
    public void AFollowedBonePointsWhereTheTrackDoes()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d), ("duration", 3d)));
        var track = new Track([0, 0.5, 1], (part, f) => part == "leftUpperArm" ? (new[] { -90d, 0d, 60d }[f], 1) : null);
        var rig = comp.RigFromDrawing(Figure(), Landmarks(), Opts(("follow", track), ("start", 1d)));

        var forearmRest = Angle(rig.Bone("leftForearm").At(0)) - Angle(rig.Bone("leftUpperArm").At(0));
        foreach (var (t, want) in new[] { (1d, -90d), (1.5d, 0d), (2d, 60d) })
        {
            Assert.Equal(want, Wrap(Angle(rig.Bone("leftUpperArm").At(t))), 3);
            // A part the track does not carry follows its parent, keeping the drawing's own bend.
            Assert.Equal(forearmRest, Wrap(Angle(rig.Bone("leftForearm").At(t)) - Angle(rig.Bone("leftUpperArm").At(t))), 3);
        }

        Assert.Equal(-90, Wrap(Angle(rig.Bone("leftUpperArm").At(0))), 3);   // before start: the first sample
    }

    [Fact]
    public void APartOutOfThePageIsHeldToItsParent()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d)));
        var track = new Track([0, 1], (part, f) => part switch
        {
            "leftUpperArm" => (-60, 1),
            "leftForearm" => (150, 0.1),   // pointing at the camera: its page angle means nothing
            _ => null,
        });
        var held = comp.RigFromDrawing(Figure(), Landmarks(), Opts(("follow", track)));
        var rest = new MotionToolkit().Composition().RigFromDrawing(Figure(), Landmarks());
        var bend = Angle(rest.Bone("leftForearm").At(0)) - Angle(rest.Bone("leftUpperArm").At(0));
        Assert.Equal(bend, Wrap(Angle(held.Bone("leftForearm").At(1)) - Angle(held.Bone("leftUpperArm").At(1))), 3);
    }

    [Fact]
    public void ItTurnsTheShortWayBetweenSamples()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d), ("duration", 1d)));
        var track = new Track([0, 1], (part, f) => part == "leftUpperArm" ? (f == 0 ? 170d : -170d, 1) : null);
        var rig = comp.RigFromDrawing(Figure(), Landmarks(), Opts(("follow", track)));
        Assert.True(Math.Abs(Math.Abs(Wrap(Angle(rig.Bone("leftUpperArm").At(0.5)))) - 180) < 1, $"{Angle(rig.Bone("leftUpperArm").At(0.5)):0.0}");
    }

    [Fact]
    public void TheRootTravelsInLegLengths()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 300d), ("height", 400d), ("duration", 1d)));
        var track = new Track([0, 1], (_, _) => null, f => (0, f == 0 ? 0 : -0.5));
        var rig = comp.RigFromDrawing(Figure(), Landmarks(), Opts(("follow", track)));
        var legs = Math.Sqrt((5 * 5) + (80 * 80)) + Math.Sqrt((2 * 2) + (60 * 60));   // hip to knee to ankle on the figure
        Assert.Equal(220 - (0.5 * legs), Origin(rig.Bone("hips").At(1)).Y, 4);
        Assert.Equal(220, Origin(rig.Bone("hips").At(0)).Y, 4);
    }

    [Fact]
    public void PosesAndMoveAddToAFollow()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 300d), ("height", 400d), ("duration", 1d)));
        var track = new Track([0, 1], (part, _) => part == "head" ? (-80, 1) : null);
        var rig = comp.RigFromDrawing(Figure(), Landmarks(), Opts(("follow", track),
            ("poses", new object[] { Opts(("time", 0d)), Opts(("time", 1d), ("head", 20d)) }),
            ("move", motion.Nodes.Linear("vector", new[] { 30d, 0d }))));
        Assert.Equal(-80, Wrap(Angle(rig.Bone("head").At(0))), 3);
        Assert.Equal(-60, Wrap(Angle(rig.Bone("head").At(1))), 3);
        Assert.Equal(150 + 30, Origin(rig.Bone("hips").At(1)).X, 4);
    }

    [Fact]
    public void FollowTakesARecordedMotionAndStartNeedsOne()
    {
        var e = Assert.Throws<ArgumentException>(() => new MotionToolkit().Composition().RigFromDrawing(Figure(), Landmarks(), Opts(("follow", "Walk"))));
        Assert.Contains("Character.track", e.Message);
        e = Assert.Throws<ArgumentException>(() => new MotionToolkit().Composition().RigFromDrawing(Figure(), Landmarks(), Opts(("start", 1d))));
        Assert.Contains("no follow", e.Message);
    }
    #endregion

    #region Refusals
    [Fact]
    public void AMissingLandmarkIsNamed()
    {
        var landmarks = Landmarks();
        landmarks.Remove("leftKnee");
        var e = Assert.Throws<ArgumentException>(() => new MotionToolkit().Composition().RigFromDrawing(Figure(), landmarks));
        Assert.Contains("no leftKnee", e.Message);
    }

    [Fact]
    public void APoseNamingNoBoneIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => new MotionToolkit().Composition().RigFromDrawing(Figure(), Landmarks(),
            Opts(("poses", new object[] { Opts(("time", 0d), ("leftElbow", 30d)) }))));
        Assert.Contains("no bone 'leftElbow'", e.Message);
        Assert.Contains("leftForearm", e.Message);
    }

    [Fact]
    public void ABoneGivenTwiceIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => new MotionToolkit().Composition().RigFromDrawing(Figure(), Landmarks(),
            Opts(("turns", Opts(("head", 10d))), ("poses", new object[] { Opts(("time", 0d), ("head", 5d)) }))));
        Assert.Contains("both in turns and in poses", e.Message);
    }

    [Fact]
    public void ASecondRigNeedsAPrefix()
    {
        var comp = new MotionToolkit().Composition();
        comp.RigFromDrawing(Figure(), Landmarks());
        var e = Assert.Throws<ArgumentException>(() => comp.RigFromDrawing(Figure(), Landmarks()));
        Assert.Contains("prefix", e.Message);
        Assert.Equal(15, comp.RigFromDrawing(Figure(), Landmarks(), Opts(("prefix", "b."))).Names.Length);
    }
    #endregion

    #region Private
    private sealed class Track(double[] times, Func<string, int, (double Angle, double InPlane)?> direction, Func<int, (double, double)>? root = null)
        : IPlanarMotion
    {
        public double[] FrameTimes => times;

        public bool TryGetDirection(string part, int frame, out double angleDeg, out double inPlane)
        {
            var d = direction(part, frame);
            (angleDeg, inPlane) = d ?? (0, 0);
            return d is not null;
        }

        public (double X, double Y) RootOffset(int frame) => root?.Invoke(frame) ?? (0, 0);
    }

    private static double Angle(object pose) => (double)((IDictionary<string, object?>)pose)["angle"]!;

    private static (double X, double Y) Origin(object pose)
    {
        var o = (IDictionary<string, object?>)((IDictionary<string, object?>)pose)["origin"]!;
        return ((double)o["x"]!, (double)o["y"]!);
    }

    private static double Wrap(double a) => a - (360 * Math.Round(a / 360));

    /// <summary>A stick figure in thick strokes, arms out: 300 × 400, transparent or on a ground.</summary>
    private static SkiaBitmapWrapper Figure(SKColor? ground = null)
    {
        var bmp = new SKBitmap(new SKImageInfo(300, 400, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var c = new SKCanvas(bmp);
        c.Clear(ground ?? SKColors.Transparent);
        using var p = new SKPaint { IsAntialias = true, StrokeCap = SKStrokeCap.Round, Style = SKPaintStyle.Stroke };
        void Line(float x0, float y0, float x1, float y1, float width, string color)
        {
            p.StrokeWidth = width;
            p.Color = SKColor.Parse(color);
            c.DrawLine(x0, y0, x1, y1, p);
        }

        Line(150, 110, 150, 220, 50, "#3a6ea5");     // body
        Line(150, 110, 70, 115, 18, "#3a6ea5");      // right upper arm (page left)
        Line(70, 115, 20, 120, 14, "#e5a93c");       // right forearm
        Line(150, 110, 230, 115, 18, "#3a6ea5");     // left upper arm (page right)
        Line(230, 115, 280, 120, 14, "#e5a93c");
        Line(135, 220, 130, 300, 20, "#2b2b2b");     // legs
        Line(130, 300, 128, 370, 16, "#2b2b2b");
        Line(165, 220, 170, 300, 20, "#2b2b2b");
        Line(170, 300, 172, 370, 16, "#2b2b2b");
        p.Style = SKPaintStyle.Fill;
        p.Color = SKColor.Parse("#c98b6b");
        c.DrawCircle(150, 70, 28, p);                // head
        return new SkiaBitmapWrapper(bmp);
    }

    private static Dictionary<string, object?> Landmarks() => new()
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

    private static (double X, double Y) Tip(object pose)
    {
        var tip = (IDictionary<string, object?>)((IDictionary<string, object?>)pose)["tip"]!;
        return ((double)tip["x"]!, (double)tip["y"]!);
    }
    #endregion
}
