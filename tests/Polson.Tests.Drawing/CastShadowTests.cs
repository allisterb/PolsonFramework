namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// Cast shadow projection. Every light ray meets the ground line at <c>groundY</c>, so the projected
/// vertices are collinear by construction — a ground plane seen edge-on has no thickness. The footprint
/// is only fillable because <c>groundDepth</c> supplies the recession the 2D projection cannot know.
/// These tests pin both halves: the light still fixes length and direction, and the polygon has area.
/// </summary>
public class CastShadowTests : TestsRuntime
{
    #region Footprint Geometry Tests
    [Fact]
    public void TestBoundsProduceAFootprintWithArea()
    {
        var shadow = Project(Point(250f, 90f), 470f, Bounds(360f, 300f, 180f, 180f));
        var polygon = Polygon(shadow);

        Assert.Equal(4, polygon.Count);
        Assert.True(SpanY(polygon) > 1f, "Footprint has no vertical extent, so it would fill nothing.");
        Assert.True(SpanX(polygon) > 1f);
    }

    [Fact]
    public void TestContactEdgeSitsOnTheGroundLine()
    {
        var polygon = Polygon(Project(Point(250f, 90f), 470f, Bounds(360f, 300f, 180f, 180f)));

        // First two points are the contact edge; they must touch the ground line exactly, or the
        // form floats above its own shadow.
        Assert.Equal(470f, polygon[0].Y, 3);
        Assert.Equal(470f, polygon[1].Y, 3);
        Assert.True(polygon[2].Y > 470f && polygon[3].Y > 470f);
    }

    [Fact]
    public void TestGroundDepthDefaultsFromContactWidth()
    {
        var narrow = Toolkit.ProjectCastShadow(Point(250f, 90f), 470f, Bounds(360f, 300f, 100f, 180f));
        var wide = Toolkit.ProjectCastShadow(Point(250f, 90f), 470f, Bounds(360f, 300f, 400f, 180f));

        Assert.Equal(20f, Convert.ToSingle(narrow["groundDepth"]), 3);
        Assert.Equal(80f, Convert.ToSingle(wide["groundDepth"]), 3);
    }

    [Fact]
    public void TestExplicitGroundDepthOverridesTheDefault()
    {
        var shadow = Toolkit.ProjectCastShadow(
            Point(250f, 90f), 470f, Bounds(360f, 300f, 180f, 180f),
            new Dictionary<string, object?> { ["groundDepth"] = 34f });

        Assert.Equal(34f, Convert.ToSingle(shadow["groundDepth"]), 3);
        Assert.Equal(504f, Polygon(shadow)[2].Y, 3);
    }
    #endregion

    #region Light Response Tests
    [Fact]
    public void TestLowerLightCastsALongerShadow()
    {
        var high = Reach(Project(Point(250f, 20f), 470f, Bounds(360f, 300f, 180f, 180f)));
        var low = Reach(Project(Point(250f, 260f), 470f, Bounds(360f, 300f, 180f, 180f)));

        Assert.True(low > high, $"A lower light must stretch the shadow: high={high:F1} low={low:F1}");
    }

    [Fact]
    public void TestShadowFallsAwayFromTheLight()
    {
        var fromLeft = Polygon(Project(Point(250f, 90f), 470f, Bounds(360f, 300f, 180f, 180f)));
        var fromRight = Polygon(Project(Point(650f, 90f), 470f, Bounds(360f, 300f, 180f, 180f)));

        Assert.True(fromLeft.Max(p => p.X) > 540f, "Light on the left must throw the shadow right.");
        Assert.True(fromRight.Min(p => p.X) < 360f, "Light on the right must throw the shadow left.");
    }

    [Fact]
    public void TestVertexListFollowsTheSilhouette()
    {
        var polygon = Polygon(Project(
            Point(250f, 90f),
            470f,
            new object[] { Point(360f, 300f), Point(450f, 260f), Point(540f, 300f) }));

        // Three silhouette points -> three contact points plus three projected points.
        Assert.Equal(6, polygon.Count);
        Assert.True(SpanY(polygon) > 1f);
    }
    #endregion

    #region Perspective Ground Plane Tests
    [Fact]
    public void TestGridSelectsThePerspectiveModel()
    {
        var line = Toolkit.ProjectCastShadow(Point(250f, 90f), 470f, Bounds(360f, 300f, 180f, 180f));
        var plane = Toolkit.ProjectCastShadow(Point(150f, 250f), Grid(), Box());

        Assert.Equal("groundLine", line["model"]);
        Assert.Equal("perspective", plane["model"]);
    }

    [Fact]
    public void TestPerspectiveFootprintRecedesWithoutAGroundDepthHint()
    {
        var shadow = Toolkit.ProjectCastShadow(Point(150f, 250f), Grid(), Box());
        var polygon = Polygon(shadow);

        // Eight points: four contact, four shadow. No groundDepth is involved at all.
        Assert.Equal(8, polygon.Count);
        Assert.Null(shadow.GetValueOrDefault("groundDepth"));
        Assert.True(SpanY(polygon) > 1f);

        // The shadow vertices lie further from the viewer than the contact points they came from.
        var contactY = polygon.Take(4).Min(p => p.Y);
        Assert.True(polygon.Skip(4).All(p => p.Y < contactY),
            "A light behind the viewer must throw the shadow away from camera, toward the horizon.");
    }

    [Fact]
    public void TestShadowVanishingPointSitsOnTheHorizonBeneathTheLight()
    {
        var shadow = Toolkit.ProjectCastShadow(Point(150f, 250f), Grid(), Box());
        var vp = (IDictionary)shadow["vpShadow"]!;

        Assert.Equal(150f, Convert.ToSingle(vp["x"]), 3);
        Assert.Equal(200f, Convert.ToSingle(vp["y"]), 3);
        Assert.Equal(200f, Convert.ToSingle(shadow["horizonY"]), 3);
    }

    [Fact]
    public void TestLowerSunStretchesThePerspectiveShadow()
    {
        // Closer to the horizon from below means a lower sun behind the viewer.
        var high = Reach(Toolkit.ProjectCastShadow(Point(150f, 400f), Grid(), Box()));
        var low = Reach(Toolkit.ProjectCastShadow(Point(150f, 250f), Grid(), Box()));

        Assert.True(low > high, $"A lower sun must stretch the shadow: high={high:F1} low={low:F1}");
    }

    [Fact]
    public void TestShadowBeyondTheHorizonIsRefused()
    {
        // A light above the horizon here throws the shadow to infinity rather than onto the ground.
        var error = Assert.Throws<ArgumentException>(() =>
            Toolkit.ProjectCastShadow(Point(250f, 60f), Grid(), Box()));

        Assert.Contains("horizon", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestBarePointListIsRefusedInPerspectiveMode()
    {
        var error = Assert.Throws<ArgumentException>(() => Toolkit.ProjectCastShadow(
            Point(150f, 250f), Grid(), new object[] { Point(360f, 300f), Point(540f, 300f) }));

        Assert.Contains("contact points", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestTopBasePairsAreAcceptedInPerspectiveMode()
    {
        var pairs = new object[]
        {
            new Dictionary<string, object?> { ["top"] = Point(400f, 320f), ["base"] = Point(400f, 460f) },
            new Dictionary<string, object?> { ["top"] = Point(520f, 330f), ["base"] = Point(520f, 440f) }
        };

        var polygon = Polygon(Toolkit.ProjectCastShadow(Point(150f, 250f), Grid(), pairs));

        Assert.Equal(4, polygon.Count);
        Assert.True(SpanY(polygon) > 1f);
    }
    #endregion

    #region Degenerate Input Tests
    [Fact]
    public void TestDrawingAZeroAreaPolygonThrowsRatherThanDrawingNothing()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.GetContext("2d");
        var flat = new object[] { Point(100f, 300f), Point(200f, 300f), Point(300f, 300f) };

        var error = Assert.Throws<ArgumentException>(() => Toolkit.DrawCastShadow(ctx, flat));
        Assert.Contains("degenerate", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("groundDepth", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestDrawingTooFewPointsThrows()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.GetContext("2d");

        Assert.Throws<ArgumentException>(() => Toolkit.DrawCastShadow(ctx, new object[] { Point(10f, 10f) }));
    }
    #endregion

    #region Methods
    private static ConstructiveDrawingToolkit Toolkit { get; } = new();

    private static Dictionary<string, object?> Project(object light, float groundY, object shape) =>
        Toolkit.ProjectCastShadow(light, groundY, shape);

    private static Dictionary<string, object?> Bounds(float x, float y, float width, float height) =>
        new() { ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height };

    private static Dictionary<string, object?> Point(float x, float y) =>
        new() { ["x"] = x, ["y"] = y };

    private static Dictionary<string, object?> Grid() => Toolkit.CreatePerspectiveGrid(
        new Dictionary<string, object?>
        {
            ["type"] = "2point",
            ["horizonY"] = 200f,
            ["centerOfVisionX"] = 450f,
            ["focalLength"] = 900f,
            ["cameraAngleDeg"] = 35f
        });

    private static Dictionary<string, object?> Box() =>
        Toolkit.CreatePerspectiveBox(Grid(), 430f, 470f, 150f, 150f, 170f);

    private static List<(float X, float Y)> Polygon(Dictionary<string, object?> shadow) =>
        [.. ((IList)shadow["shadowPolygon"]!).Cast<object>().Select(p =>
        {
            var dict = (IDictionary)p;
            return (Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]));
        })];

    private static float SpanX(List<(float X, float Y)> polygon) => polygon.Max(p => p.X) - polygon.Min(p => p.X);

    private static float SpanY(List<(float X, float Y)> polygon) => polygon.Max(p => p.Y) - polygon.Min(p => p.Y);

    /// <summary>How far the far edge reaches beyond the object's contact footprint.</summary>
    private static float Reach(Dictionary<string, object?> shadow) => SpanX(Polygon(shadow));
    #endregion
}
