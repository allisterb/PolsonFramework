namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>createGestureContour</c> — Stanchfield's straight line for a stretch and bent line for a squash
/// (<i>Drawn to Life</i>, ch. 13, 19, 24), drawn from a mannequin's own joints.
///
/// Asserted against the geometry of the bend rather than against a picture: a contour with its sides
/// swapped renders as a perfectly plausible drawing of a limb, and only the side it sits on is wrong.
/// </summary>
public class GestureContourTests : TestsRuntime
{
    const float Height = 800f;

    static ConstructiveDrawingToolkit Kit => new();

    static Dictionary<string, object?> Figure(object? pose = null, object? extra = null)
    {
        var options = new Dictionary<string, object?>();
        if (pose != null) options["pose"] = pose;
        if (extra is IDictionary<string, object?> more)
            foreach (var kv in more) options[kv.Key] = kv.Value;
        return Kit.CreateMannequinFigure(400f, 50f, Height, options);
    }

    static readonly Dictionary<string, object?> BentArm = new()
    {
        ["rightArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = 40f, ["elbowDeg"] = -110f }
    };

    static SKPoint P(object? p) => new(Convert.ToSingle(((IDictionary)p!)["x"]), Convert.ToSingle(((IDictionary)p!)["y"]));

    static IDictionary Part(Dictionary<string, object?> contour, string name) =>
        (IDictionary)((IDictionary)contour["parts"]!)[name]!;

    static SKPoint[] Points(IDictionary part, string key) => ((CanvasPath)part[key]!).Path.Points;

    /// <summary>Distance of a point from the chord through the limb's two ends: small is inside the bend.</summary>
    static float FromChord(SKPoint a, SKPoint c, SKPoint p)
    {
        float dx = c.X - a.X, dy = c.Y - a.Y;
        return MathF.Abs(dx * (a.Y - p.Y) - (a.X - p.X) * dy) / MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The straight lines go outside a bend and the curve inside it.</summary>
    [Fact]
    public void TestTheStretchIsOutsideTheBendAndTheSquashInside()
    {
        var fig = Figure(BentArm);
        var arm = (IDictionary)fig["rightArm"]!;
        SKPoint a = P(arm["shoulder"]), b = P(arm["elbow"]), c = P(arm["wrist"]);
        var part = Part(Kit.CreateGestureContour(fig), "rightArm");

        var stretch = Points(part, "stretch");
        var squash = Points(part, "squash");
        var curveMid = new SKPoint(
            0.25f * squash[0].X + 0.5f * squash[1].X + 0.25f * squash[2].X,
            0.25f * squash[0].Y + 0.5f * squash[1].Y + 0.25f * squash[2].Y);

        Assert.Equal(3, stretch.Length);                                    // two straight lines, one angle
        Assert.True(FromChord(a, c, stretch[1]) > FromChord(a, c, b), "the stretch angle sits outside the joint");
        Assert.True(FromChord(a, c, curveMid) < FromChord(a, c, b), "the squash curve sits inside it");
        Assert.Equal(110f, Convert.ToSingle(part["bendDeg"]), 0.5f);
        Assert.False((bool)part["straight"]!);
    }

    /// <summary>The squash curve passes through the joint's inner edge, so the line stays on the mass.</summary>
    [Fact]
    public void TestTheSquashCurvePassesThroughTheJointEdge()
    {
        var fig = Figure(BentArm);
        var elbow = P(((IDictionary)fig["rightArm"]!)["elbow"]);
        var squash = Points(Part(Kit.CreateGestureContour(fig), "rightArm"), "squash");
        var mid = new SKPoint(
            0.25f * squash[0].X + 0.5f * squash[1].X + 0.25f * squash[2].X,
            0.25f * squash[0].Y + 0.5f * squash[1].Y + 0.25f * squash[2].Y);

        Assert.Equal(Height / 8f * 0.16f, SKPoint.Distance(mid, elbow), 0.01f);   // the forearm radius at the elbow
    }

    /// <summary>A straight limb has no inside: both sides are stretch and there is no squash.</summary>
    [Fact]
    public void TestAStraightLimbIsStretchedOnBothSides()
    {
        var part = Part(Kit.CreateGestureContour(Figure()), "leftLeg");

        Assert.True((bool)part["straight"]!);
        Assert.Equal(6, Points(part, "stretch").Length);
        Assert.Empty(Points(part, "squash"));
    }

    /// <summary>The torso's stretch is its longer side, and it follows the line of action.</summary>
    /// <remarks>
    /// What decides it is the ribcage's turn against the pelvis. The default figure stands in contrapposto,
    /// a 12° bend of its own toward the right, so a C the other way must outweigh that before the stretch
    /// moves: at +30 the two nearly cancel and the torso is drawn even.
    /// </remarks>
    [Fact]
    public void TestTheTorsoStretchFollowsTheCurve()
    {
        static string? Side(float turn, bool level) =>
            (string?)Part(Kit.CreateGestureContour(Figure(new Dictionary<string, object?>
            {
                ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = "C", ["turnDeg"] = turn }
            }, level ? new Dictionary<string, object?> { ["shoulderTiltDeg"] = 0f, ["pelvicTiltDeg"] = 0f } : null)), "torso")["stretchSide"];

        Assert.Equal("right", Side(-30f, level: true));
        Assert.Equal("left", Side(30f, level: true));
        Assert.Equal("right", Side(-30f, level: false));
        Assert.Null(Side(30f, level: false));
        Assert.Equal("left", Side(60f, level: false));
    }

    /// <summary>An even torso has no stretch to show, so it is drawn straight on both sides.</summary>
    [Fact]
    public void TestAnEvenTorsoHasNoStretchSide()
    {
        var level = new Dictionary<string, object?> { ["shoulderTiltDeg"] = 0f, ["pelvicTiltDeg"] = 0f };
        var torso = Part(Kit.CreateGestureContour(Figure(null, level)), "torso");

        Assert.Null(torso["stretchSide"]);
        Assert.True(Convert.ToSingle(torso["shortfall"]) < 0.03f);
    }

    /// <summary>The pose sketch1's deckhand was drawn in: a strong C over a rigid lean back, the ribcage at -36°.</summary>
    static readonly Dictionary<string, object?> Deckhand = new()
    {
        ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = "C", ["turnDeg"] = -40f },
        ["spineDeg"] = -10f,
        ["rightArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = -28f, ["elbowDeg"] = 0f },
        ["leftArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = 14f, ["elbowDeg"] = -62f },
    };

    /// <summary>Where a point sits against an ellipse: 1 on the outline, below it inside.</summary>
    static float EllipseValue(IDictionary mass, SKPoint p, float rx, float ry)
    {
        var c = P(mass["center"]);
        var t = Convert.ToSingle(mass["tiltDeg"]) * MathF.PI / 180f;
        float dx = p.X - c.X, dy = p.Y - c.Y;
        float u = dx * MathF.Cos(t) + dy * MathF.Sin(t), v = -dx * MathF.Sin(t) + dy * MathF.Cos(t);
        return MathF.Sqrt(u * u / (rx * rx) + v * v / (ry * ry));
    }

    static float Mass(IDictionary mass, string key, float fallback) =>
        mass.Contains(key) && mass[key] != null ? Convert.ToSingle(mass[key]) : fallback;

    /// <summary>
    /// The torso's sides run from the ribcage's outline to the pelvis's, however far the ribcage turns.
    /// </summary>
    /// <remarks>
    /// sketch1's deckhand found the regression: the sides ran from the shoulder tips, out along the clavicle
    /// line, so with the ribcage tilted 36° one side started in the shoulder knob and folded across the chest,
    /// and the other ran through the arm root. Neither tip is on the ribcage then.
    /// </remarks>
    [Fact]
    public void TestTheTorsoSidesRunFromTheRibcageToThePelvis()
    {
        var fig = Figure(Deckhand, new Dictionary<string, object?> { ["pelvicTiltDeg"] = 10f });
        var torso = Part(Kit.CreateGestureContour(fig), "torso");
        var rib = (IDictionary)fig["ribcage"]!;
        var pel = (IDictionary)fig["pelvis"]!;
        var H = Height / 8f;

        foreach (var key in new[] { "stretch", "squash" })
        {
            var pts = Points(torso, key);
            var top = pts[0];
            var bottom = pts[^1];
            Assert.Equal(1f, EllipseValue(rib, top, Mass(rib, "rx", H * 0.85f), Mass(rib, "ry", H * 0.70f)), 0.01f);
            Assert.Equal(1f, EllipseValue(pel, bottom, Mass(pel, "rx", H * 0.70f), Mass(pel, "ry", H * 0.45f)), 0.01f);
        }
    }

    /// <summary>
    /// The two sides of the torso stay on their own sides of its axis, so they cannot cross, across a sweep of
    /// curves and leans.
    /// </summary>
    [Fact]
    public void TestTheTorsoSidesNeverCross()
    {
        foreach (var shape in new[] { "C", "S" })
            for (var turn = -100f; turn <= 100f; turn += 20f)
                for (var lean = -40f; lean <= 40f; lean += 20f)
                {
                    var fig = Figure(new Dictionary<string, object?>
                    {
                        ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = shape, ["turnDeg"] = turn },
                        ["spineDeg"] = lean,
                    });
                    var torso = Part(Kit.CreateGestureContour(fig), "torso");
                    if (torso["stretchSide"] is null) continue;

                    var a = P(((IDictionary)fig["pelvis"]!)["center"]);
                    var b = P(((IDictionary)fig["ribcage"]!)["center"]);
                    float Side(SKPoint p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

                    var line = Points(torso, "stretch");
                    var curve = Points(torso, "squash");
                    var straight = MathF.Sign(Side(line[0]));
                    var where = $"{shape} {turn}, lean {lean}";
                    Assert.All(line, p => Assert.True(MathF.Sign(Side(p)) == straight, where));
                    for (var t = 0f; t <= 1f; t += 0.05f)
                    {
                        float u = 1 - t;
                        var p = new SKPoint(u * u * curve[0].X + 2 * u * t * curve[1].X + t * t * curve[2].X,
                                            u * u * curve[0].Y + 2 * u * t * curve[1].Y + t * t * curve[2].Y);
                        Assert.True(MathF.Sign(Side(p)) == -straight, $"{where}: the squash crosses the axis at t {t:0.00}");
                    }
                }
    }

    /// <summary>The drawer refuses an option it does not take, as the builder does.</summary>
    [Fact]
    public void TestTheDrawerRefusesAnUnknownOption()
    {
        using var canvas = new SkiaCanvas(400, 400);
        var e = Assert.Throws<ArgumentException>(() => canvas.GetContext("2d").DrawGestureContour(Figure(),
            new Dictionary<string, object?> { ["color"] = "#000" }));
        Assert.Contains("'color'", e.Message);
        Assert.Contains("strokeColor", e.Message);
    }

    /// <summary>Padding moves every line out with the mass, which is how a sleeve is lined.</summary>
    [Fact]
    public void TestPaddingMovesTheLineOut()
    {
        var fig = Figure(BentArm);
        var elbow = P(((IDictionary)fig["rightArm"]!)["elbow"]);
        float Out(float pad) => SKPoint.Distance(elbow, Points(Part(Kit.CreateGestureContour(fig,
            new Dictionary<string, object?> { ["padding"] = pad }), "rightArm"), "stretch")[1]);

        Assert.Equal(Out(0f) + 12f, Out(12f), 0.01f);
    }

    /// <summary>An option it does not take is named rather than ignored.</summary>
    [Fact]
    public void TestAnUnknownOptionIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() =>
            Kit.CreateGestureContour(Figure(), new Dictionary<string, object?> { ["squashDepth"] = 2f }));

        Assert.Contains("'squashDepth'", e.Message);
        Assert.Contains("padding", e.Message);
    }

    /// <summary>The drawer inks the lines and hands back the same geometry.</summary>
    [Fact]
    public void TestTheDrawerInksAndReturnsTheContour()
    {
        using var canvas = new SkiaCanvas(900, 900);
        var ctx = canvas.GetContext("2d");
        var drawn = ctx.DrawGestureContour(Figure(BentArm), new Dictionary<string, object?> { ["strokeColor"] = "#ff0000" });

        Assert.Contains("parts", drawn.Keys);
        var inked = 0;
        for (var y = 0; y < 900; y += 3)
            for (var x = 0; x < 900; x += 3)
                if (canvas.SkBitmap.GetPixel(x, y).Alpha > 0) inked++;
        Assert.True(inked > 100, $"the canvas holds ink: {inked} samples");
    }
}
