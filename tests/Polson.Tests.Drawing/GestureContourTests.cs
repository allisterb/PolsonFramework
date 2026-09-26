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
    [Fact]
    public void TestTheTorsoStretchFollowsTheCurve()
    {
        static string? Side(float turn) =>
            (string?)Part(Kit.CreateGestureContour(Figure(new Dictionary<string, object?>
            {
                ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = "C", ["turnDeg"] = turn }
            })), "torso")["stretchSide"];

        Assert.Equal("right", Side(-30f));
        Assert.Equal("left", Side(30f));
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
