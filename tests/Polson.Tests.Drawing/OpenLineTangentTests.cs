namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>findTangents</c> with lines as well as shapes: a horizon, a boom, a rail. The sketch1 run's three worst staging
/// faults were all of this kind - the horizon crossing the skipper's jaw, the boom ending on his pointing hand, the rail
/// across her thighs - and all three were found by eye, because the finder judged only closed shapes.
/// </summary>
public class OpenLineTangentTests(ITestOutputHelper output) : TestsRuntime
{
    static ConstructiveDrawingToolkit Kit => new();

    static CanvasPath Circle(float x, float y, float r)
    {
        var p = new CanvasPath();
        p.Arc(x, y, r, 0f, MathF.PI * 2f);
        return p;
    }

    static Dictionary<string, object?> Line(float x1, float y1, float x2, float y2) => new() { ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };

    List<IDictionary> Find(Dictionary<string, object?> shapes)
    {
        var found = ((IEnumerable)Kit.FindTangents(shapes)["tangents"]!).Cast<IDictionary>().ToList();
        foreach (var t in found) output.WriteLine($"{t["kind"]} {t["a"]}/{t["b"]} distance {t["distance"]}");
        return found;
    }

    /// <summary>A horizon just under a head kisses it, a sliver into it grazes it, and one through it is ordinary overlap.</summary>
    [Theory]
    [InlineData(141f, true, false)]   // a pixel below the chin: a kiss
    [InlineData(138f, true, true)]    // two pixels into it: a graze
    [InlineData(100f, false, false)]  // through the middle: decisive, not a tangent
    [InlineData(170f, false, false)]  // well clear
    public void AHorizonGrazingAHeadIsATouch(float y, bool expected, bool overlapping)
    {
        var found = Find(new() { ["head"] = Circle(100, 100, 40), ["horizon"] = Line(-200, y, 400, y) });

        if (!expected) { Assert.Empty(found); return; }
        var touch = Assert.Single(found);
        Assert.Equal("touch", touch["kind"]);
        Assert.Equal("horizon", touch["a"]);
        Assert.Equal("head", touch["b"]);
        Assert.Equal(overlapping, touch["overlapping"]);
    }

    /// <summary>A boom stopping on a hand is an end; one stopping well inside it is hidden behind the hand.</summary>
    [Theory]
    [InlineData(141f, true, false)]   // just short of the outline
    [InlineData(138f, true, true)]    // just past it
    [InlineData(120f, false, false)]  // well inside: the boom runs behind the hand
    [InlineData(160f, false, false)]  // stops clear of it
    public void ABoomEndingOnAHandIsAnEnd(float endX, bool expected, bool inside)
    {
        var found = Find(new() { ["hand"] = Circle(100, 100, 40), ["boom"] = Line(400, 100, endX, 100) });

        if (!expected) { Assert.Empty(found); return; }
        var end = Assert.Single(found);
        Assert.Equal("end", end["kind"]);
        Assert.Equal("boom", end["a"]);
        Assert.Equal("hand", end["b"]);
        Assert.Equal("end", end["end"]);
        Assert.Equal(inside, end["inside"]);
    }

    /// <summary>A rail drawn a few pixels off a shape's edge, parallel to it, runs along it.</summary>
    [Fact]
    public void ARailAlongAnEdgeIsAnAlignment()
    {
        var box = new CanvasPath();
        box.Rect(100, 100, 200, 100);
        var found = Find(new() { ["thighs"] = box, ["rail"] = Line(120, 96, 280, 96) });

        var align = Assert.Single(found);
        Assert.Equal("align", align["kind"]);
        Assert.Equal("rail", align["a"]);
        Assert.True(Convert.ToSingle(align["length"]) > 140f, $"the run should cover most of the rail, measured {align["length"]}");
    }

    /// <summary>Two lines meeting end to end, or one stopping on the other, is a tangent; a crossing is not.</summary>
    [Theory]
    [InlineData(101f, 0f, 101f, 100f, true)]    // a corner left a pixel open
    [InlineData(50f, 0.5f, 50f, 100f, true)]    // a T: one stops on the other
    [InlineData(50f, -50f, 50f, 50f, false)]    // a crossing
    [InlineData(150f, 0f, 150f, 100f, false)]   // clear
    public void ALineEndingOnALineIsAnEnd(float x1, float y1, float x2, float y2, bool expected)
    {
        var found = Find(new() { ["mast"] = Line(0, 0, 100, 0), ["stay"] = Line(x1, y1, x2, y2) });

        if (!expected) { Assert.Empty(found); return; }
        var end = Assert.Single(found);
        Assert.Equal("end", end["kind"]);
    }

    /// <summary>Two lines close and parallel run along each other.</summary>
    [Fact]
    public void TwoParallelLinesAlign()
    {
        var found = Find(new() { ["rail"] = Line(0, 0, 300, 0), ["deck"] = Line(20, 4, 280, 4) });

        // The deck's ends lie on the run, so they are part of the alignment rather than two more contacts.
        Assert.Equal("align", Assert.Single(found)["kind"]);
    }

    /// <summary>Every way of writing a line gives the same answer.</summary>
    [Fact]
    public void ALineCanBeWrittenFourWays()
    {
        var open = new CanvasPath();
        open.MoveTo(400, 100);
        open.LineTo(141, 100);
        object?[] forms =
        [
            Line(400, 100, 141, 100),
            new object?[] { new Dictionary<string, object?> { ["x"] = 400f, ["y"] = 100f }, new object?[] { 141f, 100f } },
            "M400 100 L141 100",
            open
        ];

        foreach (var form in forms)
        {
            var found = Find(new() { ["hand"] = Circle(100, 100, 40), ["boom"] = form });
            Assert.Equal("end", Assert.Single(found)["kind"]);
        }
    }

    /// <summary>A figure goes in the same bag as the set, its groups named after it.</summary>
    [Fact]
    public void AFigureAndTheSetGoInOneBag()
    {
        var figure = Kit.CreateFigureGeometry(Kit.CreateMannequinFigure(250f, 30f, 460f, null));
        var head = ((CanvasPath)((IDictionary)figure["groups"]!)["head"]!).Path.Bounds;

        var found = Find(new()
        {
            ["her"] = figure,
            ["boom"] = Line(head.MidX, head.Top - 200f, head.MidX, head.Top - 1f)
        });

        Assert.Contains(found, t => (string?)t["kind"] == "end" && (string?)t["a"] == "boom" && (string?)t["b"] == "her.head");
        Assert.Contains(found, t => ((string?)t["a"])?.StartsWith("her.", StringComparison.Ordinal) == true
                                    && ((string?)t["b"])?.StartsWith("her.", StringComparison.Ordinal) == true);
    }

    /// <summary>A closed path is still a shape, so nothing that worked before changes.</summary>
    [Fact]
    public void AClosedPathIsStillAShape()
    {
        var found = Find(new() { ["a"] = Circle(100, 100, 40), ["b"] = Circle(181, 100, 40) });

        Assert.Equal("touch", Assert.Single(found)["kind"]);
    }

    [Fact]
    public void ALineObjectMissingAnEndIsRefusedByName()
    {
        var ex = Assert.Throws<ArgumentException>(() => Kit.FindTangents(new Dictionary<string, object?>
        {
            ["head"] = Circle(100, 100, 40),
            ["boom"] = new Dictionary<string, object?> { ["x1"] = 0f, ["y1"] = 0f, ["x2"] = 10f }
        }));
        Assert.Contains("boom", ex.Message);
        Assert.Contains("x1, y1, x2, y2", ex.Message);
    }

    #region Attached
    /// <summary>The run's rig: a mast, a boom hinged on it, a stay ending on the boom.</summary>
    static Dictionary<string, object?> Rig(float boomStart = 1f) => new()
    {
        ["mast"] = Line(0, 0, 0, 300),
        ["boom"] = Line(boomStart, 150, 200, 150),
    };

    Dictionary<string, object?> FindAttached(Dictionary<string, object?> shapes, params string[][] pairs)
    {
        var result = Kit.FindTangents(shapes, new Dictionary<string, object?> { ["attached"] = pairs.Select(p => (object)p).ToArray() });
        foreach (var t in ((IEnumerable)result["tangents"]!).Cast<IDictionary>()) output.WriteLine($"tangent {t["kind"]} {t["a"]}/{t["b"]}");
        foreach (var t in ((IEnumerable)result["attached"]!).Cast<IDictionary>()) output.WriteLine($"attached {t["kind"]} {t["a"]}/{t["b"]}");
        return result;
    }

    static List<IDictionary> List(Dictionary<string, object?> result, string key) => ((IEnumerable)result[key]!).Cast<IDictionary>().ToList();

    /// <summary>
    /// A boom must end on its mast. Declared attached, the end moves out of the tangents into <c>attached</c>, where
    /// the record still shows it. sketch1's agent filtered these by hand, by name.
    /// </summary>
    [Fact]
    public void AnAttachedEndIsAJoinNotATangent()
    {
        Assert.Equal("end", Assert.Single(Find(Rig()))["kind"]);

        var result = FindAttached(Rig(), ["boom", "mast"]);
        Assert.Empty(List(result, "tangents"));
        Assert.Equal(0, result["count"]);
        Assert.Equal(0, result["ends"]);
        var join = Assert.Single(List(result, "attached"));
        Assert.Equal("end", join["kind"]);
        Assert.Empty(List(result, "apart"));
    }

    /// <summary>The pair may be given either way round.</summary>
    [Fact]
    public void AJoinMatchesEitherWayRound()
    {
        Assert.Empty(List(FindAttached(Rig(), ["mast", "boom"]), "tangents"));
    }

    /// <summary>Running alongside is a tangent whether the two are attached or not.</summary>
    [Fact]
    public void AnAttachedPairThatRunsAlongsideStillAligns()
    {
        var result = FindAttached(new() { ["rail"] = Line(0, 0, 300, 0), ["deck"] = Line(20, 4, 280, 4) }, ["rail", "deck"]);
        Assert.Equal("align", Assert.Single(List(result, "tangents"))["kind"]);
    }

    /// <summary>A declared join that does not meet is apart: the declaration says they should, and the drawing disagrees.</summary>
    [Fact]
    public void ADeclaredJoinThatDoesNotMeetIsApart()
    {
        var result = FindAttached(Rig(boomStart: 30f), ["boom", "mast"]);
        Assert.Empty(List(result, "tangents"));
        var gap = Assert.Single(List(result, "apart"));
        Assert.Equal("boom", gap["a"]);
        Assert.Equal("mast", gap["b"]);
        Assert.Equal(30f, Convert.ToSingle(gap["distance"]), 1.5f);
        Assert.NotNull(gap["mark"]);
    }

    /// <summary>A crossing meets: a rope through two fists is gripped, not apart.</summary>
    [Fact]
    public void ACrossingMeets()
    {
        var result = FindAttached(new() { ["her.hand"] = Circle(100, 100, 20), ["rope"] = Line(0, 100, 300, 100) }, ["her", "rope"]);
        Assert.Empty(List(result, "apart"));
    }

    /// <summary>A name covers every part named under it, so one pair can attach a whole figure.</summary>
    [Fact]
    public void ANameCoversItsParts()
    {
        var shapes = new Dictionary<string, object?> { ["her.hand"] = Circle(100, 100, 40), ["rope"] = Line(400, 100, 141, 100) };
        Assert.Equal("end", Assert.Single(Find(shapes))["kind"]);
        var result = FindAttached(shapes, ["her", "rope"]);
        Assert.Empty(List(result, "tangents"));
        Assert.Single(List(result, "attached"));
    }

    /// <summary>A name that matches nothing is refused, listing what there is: a typo would otherwise exempt nothing.</summary>
    [Fact]
    public void AnUnknownAttachedNameIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => Kit.FindTangents(Rig(),
            new Dictionary<string, object?> { ["attached"] = new object[] { new object[] { "boom", "mats" } } }));
        Assert.Contains("'mats'", e.Message);
        Assert.Contains("mast", e.Message);

        e = Assert.Throws<ArgumentException>(() => Kit.FindTangents(Rig(),
            new Dictionary<string, object?> { ["attached"] = new object[] { "boom" } }));
        Assert.Contains("pairs", e.Message);
    }
    #endregion
}
