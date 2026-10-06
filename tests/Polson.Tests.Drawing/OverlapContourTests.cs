namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>createOverlapContour</c>: Hamm's principle of the T (<i>Drawing the Head and Figure</i>, p. 48). Two circles
/// of radius 50 whose centres are 60 apart meet at (30, ±40); each covers 2·asin(0.8) = 106.26° of the other's
/// outline, 92.73 px of arc.
/// </summary>
public class OverlapContourTests : TestsRuntime
{
    const float R = 50f, Hidden = 92.73f, Circumference = 2f * MathF.PI * R;

    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static CanvasPath Circle(float cx, float cy, float r)
    {
        var p = new CanvasPath();
        p.Arc(cx, cy, r, 0f, MathF.PI * 2f);
        p.ClosePath();
        return p;
    }

    static Dictionary<string, object?> Pair(Dictionary<string, object?>? options = null) =>
        Toolkit.CreateOverlapContour(new Dictionary<string, object?> { ["a"] = Circle(0f, 0f, R), ["b"] = Circle(60f, 0f, R) }, options);

    static Dictionary<string, object?> Shape(Dictionary<string, object?> r, string name) =>
        (Dictionary<string, object?>)((Dictionary<string, object?>)r["shapes"]!)[name]!;

    static float F(Dictionary<string, object?> d, string key) => Convert.ToSingle(d[key]);

    static float Length(CanvasPath path)
    {
        using var measure = new SkiaSharp.SKPathMeasure(path.Path, false);
        var total = 0f;
        do total += measure.Length; while (measure.NextContour());
        return total;
    }

    static List<Dictionary<string, object?>> Junctions(Dictionary<string, object?> r) =>
        ((List<object?>)r["junctions"]!).Cast<Dictionary<string, object?>>().ToList();

    [Fact]
    public void TestTheNearShapeIsWholeAndTheFarOneStopsAtIt()
    {
        var r = Pair();
        Assert.Equal(Circumference, F(Shape(r, "a"), "visibleLength"), 0);
        Assert.Equal(0f, F(Shape(r, "a"), "hiddenLength"), 1);
        Assert.Equal(Hidden, F(Shape(r, "b"), "hiddenLength"), 0);
        Assert.Equal(Circumference - Hidden, F(Shape(r, "b"), "visibleLength"), 0);
    }

    [Fact]
    public void TestEachTIsReportedWhereTheOutlinesCross()
    {
        var js = Junctions(Pair());
        Assert.Equal(2, js.Count);
        foreach (var j in js)
        {
            Assert.Equal("a", j["near"]);
            Assert.Equal("b", j["far"]);
            var at = (Dictionary<string, object?>)j["at"]!;
            Assert.Equal(30f, F(at, "x"), 0);
            Assert.Equal(40f, MathF.Abs(F(at, "y")), 0);
        }
    }

    [Fact]
    public void TestOrderPutsANamedShapeInFront()
    {
        var r = Pair(new() { ["order"] = new List<object?> { "b" } });
        Assert.Equal(new List<object?> { "b", "a" }, r["order"]);
        Assert.Equal(Hidden, F(Shape(r, "a"), "hiddenLength"), 0);
        Assert.All(Junctions(r), j => Assert.Equal("b", j["near"]));
    }

    [Fact]
    public void TestTheSilhouetteIsOuterAndTheOverlapIsInner()
    {
        // Which side is the silhouette is judged by probing 1.5 px outward, so the change from silhouette to
        // overlap line lands within about a pixel of each crossing rather than on it. The total agrees with the visible lengths to the measuring error of the cut curves.
        var r = Pair();
        float outer = Length((CanvasPath)r["outer"]!), inner = Length((CanvasPath)r["inner"]!);
        Assert.InRange(outer, 2f * (Circumference - Hidden) - 2f, 2f * (Circumference - Hidden) + 2f);
        Assert.InRange(inner, Hidden - 2f, Hidden + 2f);
        Assert.InRange(outer + inner, 2f * Circumference - Hidden - 1.5f, 2f * Circumference - Hidden + 1.5f);   // remeasuring cut curves
    }

    [Fact]
    public void TestAStandingFiguresChinCrossesItsNeck()
    {
        var fig = Toolkit.CreateMannequinFigure(400f, 100f, 800f);
        var r = Toolkit.CreateOverlapContour(fig);
        Assert.Equal("head", ((List<object?>)r["order"]!)[0]);
        Assert.Equal(2, Junctions(r).Count(j => (string?)j["near"] == "head" && (string?)j["far"] == "torso"));
    }

    [Fact]
    public void TestAFigureNameCoversItsGroups()
    {
        var her = Toolkit.CreateMannequinFigure(400f, 100f, 800f);
        var him = Toolkit.CreateMannequinFigure(470f, 60f, 860f);
        var r = Toolkit.CreateOverlapContour(new Dictionary<string, object?> { ["him"] = him, ["her"] = her },
            new Dictionary<string, object?> { ["order"] = new List<object?> { "her" } });
        var order = ((List<object?>)r["order"]!).Cast<string>().ToList();
        Assert.Equal("her.head", order[0]);
        Assert.True(order.FindLastIndex(n => n.StartsWith("her.")) < order.FindIndex(n => n.StartsWith("him.")));
        Assert.Contains(Junctions(r), j => ((string)j["far"]!).StartsWith("him.") && ((string)j["near"]!).StartsWith("her."));
    }

    [Fact]
    public void TestLinesUnknownNamesAndUnknownOptionsAreRefused()
    {
        var line = new Dictionary<string, object?> { ["x1"] = 0f, ["y1"] = 0f, ["x2"] = 100f, ["y2"] = 0f };
        Assert.Contains("is a line", Assert.Throws<ArgumentException>(() =>
            Toolkit.CreateOverlapContour(new Dictionary<string, object?> { ["a"] = Circle(0f, 0f, R), ["horizon"] = line })).Message);
        Assert.Contains("no such shape", Assert.Throws<ArgumentException>(() =>
            Pair(new() { ["order"] = new List<object?> { "c" } })).Message);
        Assert.Contains("no option 'depth'", Assert.Throws<ArgumentException>(() =>
            Pair(new() { ["depth"] = 1 })).Message);
    }
}
