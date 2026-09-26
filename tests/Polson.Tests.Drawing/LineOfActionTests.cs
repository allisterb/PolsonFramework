namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>pose.lineOfAction</c> — a C or an S bent through the torso's two flexible parts.
///
/// **Studio Manual 24 §4 measured the gap this fills**: <c>spineDeg</c> rotates the upper body rigidly
/// about the pelvis, so the centre line moved without curving and read 1.1 across five very different
/// poses. The fix follows Stanchfield's solid-flexible construction (<i>Drawn to Life</i>, ch. 25): the
/// head, ribcage and pelvis keep their shape and the neck and waist do the bending. These tests assert
/// against the figure's own stations, since a bend that looks plausible is exactly what a render
/// cannot tell apart from one that is wrong.
/// </summary>
public class LineOfActionTests : TestsRuntime
{
    const float Height = 800f;

    static ConstructiveDrawingToolkit Kit => new();

    static Dictionary<string, object?> Figure(object? pose = null) =>
        Kit.CreateMannequinFigure(400f, 50f, Height, pose == null ? null : new Dictionary<string, object?> { ["pose"] = pose });

    static Dictionary<string, object?> Bent(string shape, float amount, float spineDeg = 0f) =>
        Figure(new Dictionary<string, object?>
        {
            ["spineDeg"] = spineDeg,
            ["lineOfAction"] = new Dictionary<string, object?> { ["shape"] = shape, ["turnDeg"] = amount }
        });

    static IDictionary Line(Dictionary<string, object?> fig) => (IDictionary)fig["lineOfAction"]!;

    static float F(object? v) => Convert.ToSingle(v);

    static float Angle(Dictionary<string, object?> fig, string mass, string key) => F(((IDictionary)fig[mass]!)[key]);

    static (float X, float Y) P(object? p) => (F(((IDictionary)p!)["x"]), F(((IDictionary)p!)["y"]));

    static float Distance(object? a, object? b)
    {
        var (ax, ay) = P(a);
        var (bx, by) = P(b);
        return MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
    }

    /// <summary>The finding the option answers: a rigid lean moves the centre line and does not curve it.</summary>
    [Fact]
    public void TestAHingeLeavesTheCentreLineAsStraightAsStanding()
    {
        var standing = F(Line(Figure())["swing"]);
        var hinged = F(Line(Figure(new Dictionary<string, object?> { ["spineDeg"] = 30f }))["swing"]);

        Assert.Equal(standing, hinged, 3);
    }

    /// <summary>A C of the same thirty degrees bows the centre line severalfold.</summary>
    [Fact]
    public void TestACurveBowsTheCentreLine()
    {
        var standing = F(Line(Figure())["swing"]);
        var curved = F(Line(Bent("C", 30f))["swing"]);

        Assert.True(curved > 3f * standing, $"C 30 swing {curved:0.000} H against standing {standing:0.000} H");
    }

    /// <summary>More turning means more swing, which is what lets a check ask for "more swing".</summary>
    /// <remarks>The acceptance test the session handoff (§15.1) specified before this was built.</remarks>
    [Theory]
    [InlineData("C")]
    [InlineData("S")]
    public void TestSwingGrowsWithTheTurn(string shape)
    {
        var swings = new List<float>();
        foreach (var turn in new[] { 10f, 20f, 40f, 80f, 120f })
            swings.Add(F(Line(Bent(shape, turn))["swing"]));

        for (var i = 1; i < swings.Count; i++)
            Assert.True(swings[i] > swings[i - 1], $"{shape}: " + string.Join(", ", swings));
        Assert.True(swings[0] > F(Line(Figure())["swing"]), "any turn bows more than standing");
    }

    /// <summary>A C splits its amount between waist and neck, and the masses are turned by what moved them.</summary>
    [Fact]
    public void TestACSplitsItsAmountAndOrientsTheMasses()
    {
        var fig = Bent("C", 30f);
        var line = Line(fig);
        var waist = F(line["waistDeg"]);
        var neck = F(line["neckDeg"]);

        Assert.Equal(30f, waist + neck, 3);
        Assert.True(waist > 0f && neck > 0f, $"both bends one way: waist {waist}, neck {neck}");
        Assert.Equal(30f, Angle(fig, "head", "angleDeg"), 3);
        Assert.Equal(-6f + waist, Angle(fig, "ribcage", "tiltDeg"), 3);
        Assert.Equal(6f, Angle(fig, "pelvis", "tiltDeg"), 3);        // the base of the curve, unmoved
    }

    /// <summary>An S bends the neck against the waist, and the head comes back upright.</summary>
    [Fact]
    public void TestAnSBendsTheNeckAgainstTheWaist()
    {
        var fig = Bent("S", 40f);
        var line = Line(fig);
        var waist = F(line["waistDeg"]);
        var neck = F(line["neckDeg"]);

        Assert.True(waist > 0f && neck < 0f, $"opposite bends: waist {waist}, neck {neck}");
        Assert.Equal(40f, MathF.Abs(waist) + MathF.Abs(neck), 3);
        Assert.True(MathF.Abs(Angle(fig, "head", "angleDeg")) < 2f, "the head of an S returns near upright");
    }

    /// <summary>The split comes from the chain's own lengths, so it barely depends on figure size.</summary>
    /// <remarks>
    /// Not exactly: <c>spineOffset</c> defaults to 6 <b>pixels</b> rather than head units, so a 120px
    /// figure's chain is proportionally more offset than an 800px one's. Measured, that moves the split
    /// by 0.05 of a degree, which is why the tolerance is a tenth. It moves <c>swing</c> more — 0.25 H
    /// against 0.23 H, since 6px is 0.4 H at that size — so swing is not compared here.
    /// </remarks>
    [Fact]
    public void TestTheSplitIsTheSameAtAnySize()
    {
        var small = Kit.CreateMannequinFigure(0f, 0f, 120f, new Dictionary<string, object?>
        {
            ["pose"] = new Dictionary<string, object?> { ["lineOfAction"] = new Dictionary<string, object?> { ["turnDeg"] = 30f } }
        });

        Assert.Equal(F(Line(Bent("C", 30f))["waistDeg"]), F(Line(small)["waistDeg"]), 0.1f);
        Assert.Equal("C", Line(small)["shape"]);                     // the shape defaults to C
    }

    /// <summary>Bending is only at the flexible parts: every solid keeps its distance from the next.</summary>
    [Fact]
    public void TestTheBendKeepsTheChainItsLengths()
    {
        var standing = Figure();
        var bent = Bent("C", -45f, spineDeg: 10f);

        Assert.Equal(Distance(standing["navel"], standing["neck"]), Distance(bent["navel"], bent["neck"]), 0.01f);
        Assert.Equal(Distance(standing["neck"], ((IDictionary)standing["head"]!)["center"]),
            Distance(bent["neck"], ((IDictionary)bent["head"]!)["center"]), 0.01f);
        Assert.Equal(Distance(standing["navel"], ((IDictionary)standing["pelvis"]!)["center"]),
            Distance(bent["navel"], ((IDictionary)bent["pelvis"]!)["center"]), 0.01f);
    }

    /// <summary>A C bows to one side of its chord; an S crosses it.</summary>
    [Fact]
    public void TestACBowsOneWayAndAnSCrosses()
    {
        static IEnumerable<float> Offsets(Dictionary<string, object?> fig)
        {
            var pts = (IList)Line(fig)["points"]!;
            var (ax, ay) = P(pts[0]);
            var (bx, by) = P(pts[^1]);
            for (var i = 1; i < pts.Count - 1; i++)
            {
                var (px, py) = P(pts[i]);
                yield return ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) / Height;
            }
        }

        var c = new List<float>(Offsets(Bent("C", 40f)));
        var s = new List<float>(Offsets(Bent("S", 60f)));

        Assert.True(c.TrueForAll(o => o > 0f) || c.TrueForAll(o => o < 0f), "C: " + string.Join(", ", c));
        Assert.True(s.Exists(o => o > 0.005f) && s.Exists(o => o < -0.005f), "S: " + string.Join(", ", s));
    }

    /// <summary>Every figure reports its centre line, bent or not, so any pose can be measured.</summary>
    [Fact]
    public void TestAnUnbentFigureStillReportsItsLine()
    {
        var line = Line(Figure());

        Assert.Null(line["shape"]);
        Assert.Equal(0f, F(line["waistDeg"]));
        Assert.Equal(6, ((IList)line["points"]!).Count);
        Assert.StartsWith("M", (string)line["d"]!);
    }

    /// <summary>A bent spine is still one part in the geometry, and it follows the bend through the navel.</summary>
    [Fact]
    public void TestABentSpineIsOnePartThatFollowsTheBend()
    {
        var fig = Bent("C", 50f);
        var geo = Kit.CreateFigureGeometry(fig);
        var parts = (IDictionary<string, object?>)geo["parts"]!;
        var spine = (CanvasPath)parts["spine"]!;
        var (nx, ny) = P(fig["navel"]);

        Assert.False(parts.ContainsKey("waist"));
        Assert.True(spine.Path.Contains(nx, ny));
    }

    /// <summary>What the option cannot honour is refused by name rather than drawn.</summary>
    [Theory]
    [InlineData("Z", 20f, "'C'")]
    [InlineData("C", 150f, "120")]
    [InlineData("C", float.NaN, "finite")]
    public void TestWhatCannotBeBentIsRefused(string shape, float amount, string named)
    {
        var e = Assert.Throws<ArgumentException>(() => Bent(shape, amount));
        Assert.Contains(named, e.Message);
    }

    /// <summary>An unknown key is named, with the two that exist.</summary>
    [Fact]
    public void TestAnUnknownKeyIsRefusedByName()
    {
        var e = Assert.Throws<ArgumentException>(() => Figure(new Dictionary<string, object?>
        {
            ["lineOfAction"] = new Dictionary<string, object?> { ["curve"] = "C", ["turnDeg"] = 20f }
        }));

        Assert.Contains("'curve'", e.Message);
        Assert.Contains("turnDeg", e.Message);
    }
}
