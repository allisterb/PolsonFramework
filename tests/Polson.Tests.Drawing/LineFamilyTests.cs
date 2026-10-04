namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>classifyLines</c>: lines sorted into the families of Stanchfield's <i>Symbols for Poses</i> (ch. 42), and the
/// mood a set adds up to. The sketch1 run declared its mood and then had to write the bucketing to check it.
/// </summary>
public class LineFamilyTests : TestsRuntime
{
    static ConstructiveDrawingToolkit Kit => new();

    static Dictionary<string, object?> Line(float x1, float y1, float x2, float y2) => new() { ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };

    static object?[] Points(params (float X, float Y)[] pts) => [.. pts.Select(p => (object?)new object?[] { p.X, p.Y })];

    static List<IDictionary> Lines(Dictionary<string, object?> result) => ((IEnumerable)result["lines"]!).Cast<IDictionary>().ToList();

    static IDictionary Only(object line)
    {
        var result = Kit.ClassifyLines(new Dictionary<string, object?> { ["l"] = line });
        return Assert.Single(Lines(result));
    }

    [Theory]
    [InlineData(0f, 0f, 100f, 3f, "horizontal", null)]
    [InlineData(0f, 0f, 4f, 100f, "vertical", null)]
    [InlineData(0f, 100f, 100f, 0f, "diagonal", "rising")]
    [InlineData(0f, 0f, 100f, 100f, "diagonal", "falling")]
    [InlineData(100f, 0f, 0f, 100f, "diagonal", "rising")]   // drawn right to left, still a / on the page
    public void AStraightLineIsSortedByItsAngle(float x1, float y1, float x2, float y2, string family, string? lean)
    {
        var line = Only(Line(x1, y1, x2, y2));
        Assert.Equal(family, line["family"]);
        Assert.Equal(lean, line["lean"]);
    }

    [Fact]
    public void AnLIsAHorizontalAndAVertical()
    {
        var lines = Lines(Kit.ClassifyLines(new Dictionary<string, object?> { ["hull"] = Points((0, 100), (200, 100), (200, 0)) }));

        Assert.Equal(new[] { "hull.0", "hull.1" }, lines.Select(l => (string)l["name"]!));
        Assert.Equal(new[] { "horizontal", "vertical" }, lines.Select(l => (string)l["family"]!));
    }

    [Fact]
    public void AZigzagIsAZigzag() =>
        Assert.Equal("zigzag", Only(Points((0, 0), (20, -20), (40, 0), (60, -20), (80, 0), (100, -20)))["family"]);

    [Fact]
    public void AnSCurveIsAWave() =>
        Assert.Equal("wave", Only("M0 100 C 50 0, 100 200, 150 100")["family"]);

    [Fact]
    public void AnArchIsACurve() =>
        Assert.Equal("curve", Only("M0 100 Q 100 0 200 100")["family"]);

    [Fact]
    public void ASpiralIsASpiral()
    {
        var pts = Enumerable.Range(0, 200).Select(i =>
        {
            var t = i / 199f * 4f * MathF.PI;
            var r = 10f + 6f * t;
            return (200f + r * MathF.Cos(t), 200f + r * MathF.Sin(t));
        }).ToArray();
        Assert.Equal("spiral", Only(Points(pts))["family"]);
    }

    /// <summary>sketch1's own set, as its artwork.js drew it: the mood it declared, read back from the lines.</summary>
    [Fact]
    public void Sketch1sSetIsConflictingDiagonals()
    {
        var set = new Dictionary<string, object?>
        {
            ["horizon"] = Line(0, 196, 1200, 150),
            ["gunwale"] = Line(0, 700, 1200, 330),
            ["foredeck"] = Line(300, 800, 1200, 418),
            ["mast"] = Line(596, 690, 650, 0),
            ["boom"] = Line(560, 0, 1200, 230),
            ["rope"] = Line(380, 470, 205, 0)
        };
        for (var i = 0; i < 5; i++)
        {
            float x = 90 + i * 232, y = 54 + (i % 2) * 74;
            set[$"rain{i}"] = Line(x, y, x + 78, y + 132);
        }

        var result = Kit.ClassifyLines(set);
        var mood = (IDictionary)result["mood"]!;
        Assert.Equal("conflicting diagonals", mood["family"]);
        Assert.Equal("conflict, disturbance", mood["feeling"]);
    }

    [Fact]
    public void HorizontalsAreRest()
    {
        var result = Kit.ClassifyLines(new Dictionary<string, object?>
        {
            ["sea"] = Line(0, 400, 1200, 405), ["shore"] = Line(0, 500, 1200, 490), ["cloud"] = Line(100, 100, 700, 110)
        });
        Assert.Equal("horizontals", ((IDictionary)result["mood"]!)["family"]);
    }

    [Fact]
    public void UprightsOnAFloorAreVerticalAgainstHorizontal()
    {
        var result = Kit.ClassifyLines(new Dictionary<string, object?>
        {
            ["floor"] = Line(0, 600, 1000, 600), ["left"] = Line(200, 600, 200, 100), ["right"] = Line(800, 600, 800, 100)
        });
        Assert.Contains(((IEnumerable)result["moods"]!).Cast<IDictionary>(), m => (string?)m["family"] == "vertical against horizontal");
    }

    [Fact]
    public void DiagonalsAllOneWayAreUnsupported()
    {
        var result = Kit.ClassifyLines(new Dictionary<string, object?>
        {
            ["a"] = Line(0, 600, 600, 100), ["b"] = Line(200, 700, 800, 200), ["c"] = Line(400, 800, 1000, 300)
        });
        Assert.Equal("unsupported diagonal", ((IDictionary)result["mood"]!)["family"]);
    }

    [Fact]
    public void AClosedShapeIsRefused()
    {
        var box = new CanvasPath();
        box.Rect(0, 0, 100, 100);
        var ex = Assert.Throws<ArgumentException>(() => Kit.ClassifyLines(new Dictionary<string, object?> { ["box"] = box, ["l"] = Line(0, 0, 10, 10) }));
        Assert.Contains("box", ex.Message);
        Assert.Contains("classifyLines", ex.Message);
    }

    [Fact]
    public void AnUnknownOptionIsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() => Kit.ClassifyLines(new Dictionary<string, object?> { ["l"] = Line(0, 0, 10, 10) },
            new Dictionary<string, object?> { ["tolerence"] = 10f }));
        Assert.Contains("tolerence", ex.Message);
    }
}
