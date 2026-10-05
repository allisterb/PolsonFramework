namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>reachLeg</c> — the leg angles that put a foot on a point, which is how a figure is planted on a surface
/// that is not level: sketch1's deck rose at 15.6° and its feet were placed by eye. Asserted by rebuilding the
/// figure with the answer and measuring where the foot went.
/// </summary>
public class ReachLegTests : TestsRuntime
{
    static ConstructiveDrawingToolkit Kit => new();

    static readonly Dictionary<string, object?> Torso = new()
    {
        ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = "C", ["turnDeg"] = -40f, ["leanDeg"] = -20f },
    };

    static Dictionary<string, object?> Figure(Dictionary<string, object?>? legs = null)
    {
        var pose = new Dictionary<string, object?>(Torso);
        if (legs != null) foreach (var kv in legs) pose[kv.Key] = kv.Value;
        return Kit.CreateMannequinFigure(300f, 160f, 430f, new Dictionary<string, object?> { ["pose"] = pose });
    }

    static (float X, float Y) P(object? p) => (Convert.ToSingle(((IDictionary)p!)["x"]), Convert.ToSingle(((IDictionary)p!)["y"]));

    static Dictionary<string, object?> Point(float x, float y) => new() { ["x"] = x, ["y"] = y };

    static Dictionary<string, object?> Options(string? to = null, string? bend = null)
    {
        var o = new Dictionary<string, object?>();
        if (to != null) o["to"] = to;
        if (bend != null) o["bend"] = bend;
        return o;
    }

    /// <summary>A point a fraction of the way out to the leg's full span from its hip, at a page angle.</summary>
    static Dictionary<string, object?> Within(string side, float fraction, float deg)
    {
        var leg = (IDictionary)Figure()[side + "Leg"]!;
        var (hx, hy) = P(leg["hip"]);
        float D(string a, string b) { var (ax, ay) = P(leg[a]); var (bx, by) = P(leg[b]); return MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); }
        var span = (D("hip", "knee") + D("knee", "ankle")) * fraction;
        var r = deg * MathF.PI / 180f;
        return Point(hx + MathF.Cos(r) * span, hy + MathF.Sin(r) * span);
    }

    /// <summary>Rebuilt with the answer, the ankle or the end of the foot is on the point, for either leg.</summary>
    [Theory]
    [InlineData("left", "ankle", 0.8f, 110f)]
    [InlineData("left", "foot", 0.85f, 100f)]
    [InlineData("right", "ankle", 0.7f, 60f)]
    [InlineData("right", "foot", 0.75f, 70f)]
    public void TestTheFootLandsOnThePoint(string side, string to, float fraction, float deg)
    {
        var target = Within(side, fraction, deg);
        var (x, y) = P(target);
        var reach = Kit.ReachLeg(Figure(), side, target, Options(to));
        Assert.True((bool)reach["reached"]!);
        Assert.True(((IDictionary)reach["pose"]!).Contains("hipDeg"));

        var leg = (IDictionary)Figure(new Dictionary<string, object?> { [side + "Leg"] = reach["pose"] })[side + "Leg"]!;
        var (px, py) = P(leg[to]);
        Assert.Equal(x, px, 2);
        Assert.Equal(y, py, 2);
    }

    /// <summary>Both feet on a heeled deck: the ends of the feet on the line, whatever its slope.</summary>
    [Fact]
    public void TestBothFeetPlantOnASlopedDeck()
    {
        var slope = MathF.Tan(15.6f * MathF.PI / 180f);
        float DeckY(float x) => 600f - (x - 60f) * slope;
        var back = Kit.ReachLeg(Figure(), "left", Point(220f, DeckY(220f)), Options("foot"));
        var front = Kit.ReachLeg(Figure(), "right", Point(430f, DeckY(430f)), Options("foot"));
        Assert.True((bool)back["reached"]! && (bool)front["reached"]!, $"short by {back["miss"]} and {front["miss"]}");
        var fig = Figure(new Dictionary<string, object?> { ["leftLeg"] = back["pose"], ["rightLeg"] = front["pose"] });

        foreach (var limb in new[] { "leftLeg", "rightLeg" })
        {
            var (fx, fy) = P(((IDictionary)fig[limb]!)["foot"]);
            Assert.Equal(DeckY(fx), fy, 1);
        }
    }

    /// <summary>Left to itself a knee goes out from the body, which is how a bent knee reads from the front.</summary>
    [Fact]
    public void TestTheKneeGoesOutByDefault()
    {
        var target = Within("right", 0.6f, 80f);
        var given = Kit.ReachLeg(Figure(), "right", target);
        var outward = Kit.ReachLeg(Figure(), "right", target, Options(bend: "out"));
        var inward = Kit.ReachLeg(Figure(), "right", target, Options(bend: "in"));
        Assert.Equal(Convert.ToSingle(outward["kneeDeg"]), Convert.ToSingle(given["kneeDeg"]), 3);

        var (pelvisX, _) = P(((IDictionary)Figure()["pelvis"]!)["center"]);
        var (ox, _) = P(outward["knee"]);
        var (ix, _) = P(inward["knee"]);
        Assert.True(MathF.Abs(ox - pelvisX) > MathF.Abs(ix - pelvisX), $"out knee at {ox:0}, in knee at {ix:0}, pelvis at {pelvisX:0}");
    }

    /// <summary>Out of reach, the leg straightens toward the point and says how far short it fell.</summary>
    [Fact]
    public void TestAPointOutOfReachIsReported()
    {
        var reach = Kit.ReachLeg(Figure(), "left", Within("left", 1.5f, 95f));
        Assert.False((bool)reach["reached"]!);
        Assert.True(Convert.ToSingle(reach["miss"]) > 10f);
        Assert.Equal(0f, Convert.ToSingle(reach["kneeDeg"]), 2);
    }

    /// <summary>What a leg does not have is named.</summary>
    [Fact]
    public void TestWhatALegDoesNotHaveIsRefused()
    {
        var target = Within("left", 0.7f, 100f);
        Assert.Contains("'ankle'", Assert.Throws<ArgumentException>(() => Kit.ReachLeg(Figure(), "left", target, Options("palm"))).Message);
        Assert.Contains("'left' or 'right'", Assert.Throws<ArgumentException>(() => Kit.ReachLeg(Figure(), "leftArm", target)).Message);
    }
}
