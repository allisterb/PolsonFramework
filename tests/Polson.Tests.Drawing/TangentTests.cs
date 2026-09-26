namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>findTangents</c> — Stanchfield's two tangents: shapes that touch, and shapes that line up
/// (<i>Drawn to Life</i> vol. 1 ch. 16, 30; vol. 2 ch. 57). Also the three <c>CanvasPath</c> questions
/// it is built on: <c>area</c>, <c>isEmpty</c> and <c>contains</c>.
///
/// Asserted on shapes whose answer is known without the finder: two circles a pixel apart kiss, two
/// circles overlapping by half a radius do not, and a raised arm runs along nothing.
/// </summary>
public class TangentTests : TestsRuntime
{
    static ConstructiveDrawingToolkit Kit => new();

    static CanvasPath Circle(float x, float y, float r)
    {
        var p = new CanvasPath();
        p.Arc(x, y, r, 0f, MathF.PI * 2f);
        return p;
    }

    static CanvasPath Box(float x, float y, float w, float h)
    {
        var p = new CanvasPath();
        p.Rect(x, y, w, h);
        return p;
    }

    static List<IDictionary> Find(object shapes, object? options = null) =>
        ((IEnumerable)Kit.FindTangents(shapes, options)["tangents"]!).Cast<IDictionary>().ToList();

    static Dictionary<string, object?> Pair(CanvasPath a, CanvasPath b) => new() { ["a"] = a, ["b"] = b };

    static Dictionary<string, object?> Figure(Dictionary<string, object?> pose, float height = 460f, float x = 250f) =>
        Kit.CreateFigureGeometry(Kit.CreateMannequinFigure(x, 30f, height, new Dictionary<string, object?> { ["pose"] = pose }));

    static Dictionary<string, object?> Arms(float left, float right, float leftElbow = 0f, float rightElbow = 0f) => new()
    {
        ["leftArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = left, ["elbowDeg"] = leftElbow },
        ["rightArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = right, ["elbowDeg"] = rightElbow }
    };

    /// <summary>A ring's area is the disc less its hole — the hole is found by nesting, not by winding.</summary>
    [Fact]
    public void TestAreaSubtractsHoles()
    {
        var ring = Circle(100, 100, 50).Subtract(Circle(100, 100, 30));
        Assert.Equal(MathF.PI * (2500 - 900), ring.Area, 1f);
        Assert.False(ring.Contains(100, 100));
        Assert.True(ring.Contains(140, 100));
        Assert.Equal(3000f, Box(0, 0, 60, 50).Area, 0.5f);
    }

    [Fact]
    public void TestDisjointShapesIntersectToNothing()
    {
        Assert.True(Circle(0, 0, 10).Intersect(Circle(100, 0, 10)).IsEmpty);
        Assert.False(Circle(0, 0, 10).Intersect(Circle(15, 0, 10)).IsEmpty);
        Assert.True(new CanvasPath().IsEmpty);
    }

    /// <summary>Clear, kiss, graze, decisive: only the middle two are tangents.</summary>
    [Theory]
    [InlineData(200f, 0, false)]    // well clear
    [InlineData(181f, 1, false)]    // a pixel apart: a kiss
    [InlineData(178f, 1, true)]     // overlapping by a sliver: a graze
    [InlineData(150f, 0, false)]    // overlapping by half a radius: decisive, not a tangent
    public void TestTouchingIsAKissOrASliver(float secondX, int expected, bool overlapping)
    {
        var found = Find(Pair(Circle(100, 100, 40), Circle(secondX, 100, 40)));
        Assert.Equal(expected, found.Count);
        if (expected == 0) return;
        Assert.Equal("touch", found[0]["kind"]);
        Assert.Equal(overlapping, found[0]["overlapping"]);
    }

    /// <summary>Two bars side by side line up; move them apart, or cross them, and they do not.</summary>
    [Fact]
    public void TestParallelEdgesAlignAndCrossedOnesDoNot()
    {
        var side = Find(Pair(Box(0, 0, 40, 200), Box(45, 0, 40, 200)));
        var only = Assert.Single(side);
        Assert.Equal("align", only["kind"]);
        Assert.InRange(Convert.ToSingle(only["length"]), 190f, 205f);
        Assert.Equal(5f, Convert.ToSingle(only["distance"]), 0.5f);

        Assert.Empty(Find(Pair(Box(0, 0, 40, 200), Box(80, 0, 40, 200))));
        Assert.Empty(Find(Pair(Box(0, 80, 200, 40), Box(80, 0, 40, 200))));
    }

    /// <summary>The reported run is the edge that faces the other shape, not the thin shape's far side.</summary>
    [Fact]
    public void TestTheRunIsOnTheFacingEdge()
    {
        var only = Assert.Single(Find(Pair(Box(0, 0, 20, 200), Box(25, 0, 60, 200))));
        var at = (IDictionary)only["at"]!;
        Assert.Equal(20f, Convert.ToSingle(at["x"]), 1f);
    }

    /// <summary>Defaults scale with the shapes, so doubling a scene finds the same tangents, twice as long.</summary>
    [Fact]
    public void TestTheAnswerDoesNotDependOnScale()
    {
        var small = Find(Pair(Box(0, 0, 40, 200), Box(45, 0, 40, 200)));
        var large = Find(Pair(Box(0, 0, 80, 400), Box(90, 0, 80, 400)));
        Assert.Equal(small.Count, large.Count);
        Assert.Equal(2f * Convert.ToSingle(small[0]["length"]), Convert.ToSingle(large[0]["length"]), 6f);
    }

    /// <summary>Arms held out line up with nothing; arms hanging at the sides line up with the body.</summary>
    [Fact]
    public void TestAFigureWithArmsOutHasNoTangentsAndOneWithArmsDownDoes()
    {
        Assert.Empty(Find(Figure(Arms(135f, 45f))));

        var hanging = Find(Figure(Arms(90f, 90f)));
        Assert.Contains(hanging, t => (string)t["kind"]! == "align" && (string)t["b"]! == "torso" && (string)t["a"]! == "leftArm");
        Assert.Contains(hanging, t => (string)t["kind"]! == "align" && (string)t["b"]! == "torso" && (string)t["a"]! == "rightArm");
    }

    /// <summary>A geometry result is read through its groups, and the same pose at any size agrees.</summary>
    [Fact]
    public void TestAFigureGivesTheSamePairsAtAnySize()
    {
        static string Key(IDictionary t) => $"{t["kind"]}:{t["a"]}/{t["b"]}";
        var small = Find(Figure(Arms(90f, 90f), 460f)).Select(Key).OrderBy(k => k);
        var large = Find(Figure(Arms(90f, 90f), 920f, 500f)).Select(Key).OrderBy(k => k);
        Assert.Equal(small, large);
    }

    [Fact]
    public void TestUnknownOptionIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => Kit.FindTangents(Pair(Circle(0, 0, 5), Circle(20, 0, 5)),
            new Dictionary<string, object?> { ["gaps"] = 3 }));
        Assert.Contains("gaps", e.Message);
    }

    [Fact]
    public void TestFewerThanTwoShapesOrANonPathIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Kit.FindTangents(new Dictionary<string, object?> { ["a"] = Circle(0, 0, 5) }));
        var e = Assert.Throws<ArgumentException>(() => Kit.FindTangents(new Dictionary<string, object?> { ["a"] = Circle(0, 0, 5), ["b"] = 3 }));
        Assert.Contains("'b'", e.Message);
    }

    /// <summary>Every tangent carries a mark to stroke, so the finding can be shown on the drawing.</summary>
    [Fact]
    public void TestEveryTangentCarriesAMark()
    {
        foreach (var t in Find(Pair(Box(0, 0, 40, 200), Box(45, 0, 40, 200))).Concat(Find(Pair(Circle(100, 100, 40), Circle(181, 100, 40)))))
            Assert.False(((CanvasPath)t["mark"]!).Path.IsEmpty);
    }
}
