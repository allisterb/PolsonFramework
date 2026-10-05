namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>reachArm</c> — the arm angles that put a hand on a point. sketch1's agent posed both arms by angle and ran
/// the rope through wherever the hands landed, so a rope under tension bent at the front fist. Asserted by
/// rebuilding the figure with the answer and measuring where the hand went.
/// </summary>
public class ReachArmTests : TestsRuntime
{
    static ConstructiveDrawingToolkit Kit => new();

    static readonly Dictionary<string, object?> Lean = new()
    {
        ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = "C", ["turnDeg"] = -40f, ["leanDeg"] = -20f },
    };

    static Dictionary<string, object?> Figure(Dictionary<string, object?>? arms = null)
    {
        var pose = new Dictionary<string, object?>(Lean);
        if (arms != null) foreach (var kv in arms) pose[kv.Key] = kv.Value;
        return Kit.CreateMannequinFigure(300f, 220f, 430f, new Dictionary<string, object?> { ["pose"] = pose });
    }

    static (float X, float Y) P(object? p) => (Convert.ToSingle(((IDictionary)p!)["x"]), Convert.ToSingle(((IDictionary)p!)["y"]));

    static Dictionary<string, object?> Point(float x, float y) => new() { ["x"] = x, ["y"] = y };

    /// <summary>A point a fraction of the way out to the arm's full span from its shoulder, at a page angle.</summary>
    static Dictionary<string, object?> Within(string side, float fraction, float deg)
    {
        var arm = (IDictionary)Figure()[side + "Arm"]!;
        var (sx, sy) = P(arm["shoulder"]);
        float D(string a, string b) { var (ax, ay) = P(arm[a]); var (bx, by) = P(arm[b]); return MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); }
        var span = (D("shoulder", "elbow") + D("elbow", "wrist")) * fraction;
        var r = deg * MathF.PI / 180f;
        return Point(sx + MathF.Cos(r) * span, sy + MathF.Sin(r) * span);
    }

    static (float X, float Y) Part(Dictionary<string, object?> fig, string limb, string to)
    {
        var arm = (IDictionary)fig[limb]!;
        var (wx, wy) = P(arm["wrist"]);
        var (hx, hy) = P(arm["hand"]);
        var share = to switch { "palm" => 0.5f, "hand" => 1f, _ => 0f };
        return (wx + (hx - wx) * share, wy + (hy - wy) * share);
    }

    /// <summary>Rebuilt with the answer, the chosen part of the hand is on the point, for either arm and any part.</summary>
    [Theory]
    [InlineData("right", "wrist", 0.8f, -20f)]
    [InlineData("right", "palm", 0.8f, -20f)]
    [InlineData("right", "hand", 0.9f, -20f)]
    [InlineData("left", "palm", 0.7f, 10f)]
    [InlineData("left", "wrist", 0.6f, 120f)]
    [InlineData("right", "palm", 0.5f, -100f)]
    public void TestTheHandLandsOnThePoint(string side, string to, float fraction, float deg)
    {
        var target = Within(side, fraction, deg);
        var (x, y) = P(target);
        var reach = Kit.ReachArm(Figure(), side, target, new Dictionary<string, object?> { ["to"] = to });
        Assert.True((bool)reach["reached"]!);

        var limb = side + "Arm";
        var (px, py) = Part(Figure(new Dictionary<string, object?> { [limb] = reach["pose"] }), limb, to);
        Assert.Equal(x, px, 2);
        Assert.Equal(y, py, 2);
    }

    /// <summary>Two hands on one straight rope: the hauling pose the run could not get.</summary>
    [Fact]
    public void TestBothPalmsGoOnOneStraightLine()
    {
        (float X, float Y) block = (760f, 200f), tail = (240f, 280f);
        (float, float) On(float f) => (block.X + (tail.X - block.X) * f, block.Y + (tail.Y - block.Y) * f);
        var (fx, fy) = On(0.55f);
        var (bx, by) = On(0.75f);
        var palm = new Dictionary<string, object?> { ["to"] = "palm" };
        var fig = Figure(new Dictionary<string, object?>
        {
            ["rightArm"] = Kit.ReachArm(Figure(), "right", Point(fx, fy), palm)["pose"],
            ["leftArm"] = Kit.ReachArm(Figure(), "left", Point(bx, by), palm)["pose"],
        });

        foreach (var limb in new[] { "rightArm", "leftArm" })
        {
            var (px, py) = Part(fig, limb, "palm");
            var off = MathF.Abs((tail.X - block.X) * (block.Y - py) - (block.X - px) * (tail.Y - block.Y))
                      / MathF.Sqrt((tail.X - block.X) * (tail.X - block.X) + (tail.Y - block.Y) * (tail.Y - block.Y));
            Assert.True(off < 0.05f, $"{limb}'s palm is {off:0.000}px off the rope");
        }
    }

    /// <summary><c>bend</c> picks which side the elbow goes, and the reported elbow is where the rebuilt figure puts it.</summary>
    [Theory]
    [InlineData("down")]
    [InlineData("up")]
    public void TestBendPicksTheElbowSide(string bend)
    {
        var target = Within("right", 0.7f, -20f);
        var down = Kit.ReachArm(Figure(), "right", target, new Dictionary<string, object?> { ["bend"] = "down" });
        var chosen = Kit.ReachArm(Figure(), "right", target, new Dictionary<string, object?> { ["bend"] = bend });
        var (_, downY) = P(down["elbow"]);
        var (ex, ey) = P(chosen["elbow"]);
        if (bend == "up") Assert.True(ey < downY);

        var (rx, ry) = P(((IDictionary)Figure(new Dictionary<string, object?> { ["rightArm"] = chosen["pose"] })["rightArm"]!)["elbow"]);
        Assert.Equal(ex, rx, 2);
        Assert.Equal(ey, ry, 2);
    }

    /// <summary>Left to itself the elbow hangs down; an arm already bent hard keeps its side.</summary>
    [Fact]
    public void TestTheDefaultBendHangsDownOrKeepsAHardBend()
    {
        var target = Within("right", 0.7f, -20f);
        var down = Kit.ReachArm(Figure(), "right", target, new Dictionary<string, object?> { ["bend"] = "down" });
        var up = Kit.ReachArm(Figure(), "right", target, new Dictionary<string, object?> { ["bend"] = "up" });
        Assert.Equal(Convert.ToSingle(down["elbowDeg"]), Convert.ToSingle(Kit.ReachArm(Figure(), "right", target)["elbowDeg"]), 3);

        // An arm already bent hard keeps its side when solved again for a nearby point.
        var upBend = Convert.ToSingle(up["elbowDeg"]);
        Assert.True(MathF.Abs(upBend) > 30f, $"the up solution bends {upBend:0.0}");
        var bentUp = Figure(new Dictionary<string, object?> { ["rightArm"] = up["pose"] });
        var again = Convert.ToSingle(Kit.ReachArm(bentUp, "right", Within("right", 0.72f, -18f))["elbowDeg"]);
        Assert.True(MathF.Sign(again) == MathF.Sign(upBend), $"solved again it bends {again:0.0}, against {upBend:0.0}");
    }

    /// <summary>Out of reach, the arm straightens toward the point and says how far short it fell.</summary>
    [Fact]
    public void TestAPointOutOfReachIsReportedNotRefused()
    {
        var fig = Figure();
        var arm = (IDictionary)fig["rightArm"]!;
        var (sx, sy) = P(arm["shoulder"]);
        float D(string a, string b) { var (ax, ay) = P(arm[a]); var (bx, by) = P(arm[b]); return MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); }
        var length = D("shoulder", "elbow") + D("elbow", "wrist");

        var reach = Kit.ReachArm(fig, "right", Point(sx + 1000f, sy));
        Assert.False((bool)reach["reached"]!);
        Assert.Equal(1000f - length, Convert.ToSingle(reach["miss"]), 1);
        Assert.Equal(0f, Convert.ToSingle(reach["elbowDeg"]), 2);
        Assert.Equal(0f, Convert.ToSingle(reach["shoulderDeg"]), 2);

        Assert.False((bool)Kit.ReachArm(fig, "right", Point(sx + 1f, sy))["reached"]!);   // too close to fold onto
    }

    /// <summary>What it cannot read is named.</summary>
    [Fact]
    public void TestWhatItCannotReadIsRefused()
    {
        var fig = Figure();
        var target = Point(470f, 250f);
        Assert.Contains("'left' or 'right'", Assert.Throws<ArgumentException>(() => Kit.ReachArm(fig, "middle", target)).Message);
        Assert.Contains("'palm'", Assert.Throws<ArgumentException>(() => Kit.ReachArm(fig, "right", target,
            new Dictionary<string, object?> { ["to"] = "thumb" })).Message);
        Assert.Contains("'down'", Assert.Throws<ArgumentException>(() => Kit.ReachArm(fig, "right", target,
            new Dictionary<string, object?> { ["bend"] = "sideways" })).Message);
        Assert.Contains("'pole'", Assert.Throws<ArgumentException>(() => Kit.ReachArm(fig, "right", target,
            new Dictionary<string, object?> { ["pole"] = 1 })).Message);
        Assert.Contains("{ x, y }", Assert.Throws<ArgumentException>(() => Kit.ReachArm(fig, "right",
            new Dictionary<string, object?> { ["x"] = 4f })).Message);
    }
}
