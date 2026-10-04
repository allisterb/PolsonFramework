namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>checkLookPath</c>: whether the line of a figure's look runs clear of its own body and of anything else in the
/// way. Stanchfield's rule, which every run had to write by hand: get the body out of the way of the look.
/// </summary>
public class LookPathTests : TestsRuntime
{
    static ConstructiveDrawingToolkit Kit => new();

    const float X = 250f, Top = 30f, Height = 460f;

    static Dictionary<string, object?> Figure(Dictionary<string, object?>? pose = null) =>
        Kit.CreateMannequinFigure(X, Top, Height, pose == null ? null : new Dictionary<string, object?> { ["pose"] = pose });

    /// <summary>Both arms raised to head height, one out to each side.</summary>
    static Dictionary<string, object?> ArmsUp() => new()
    {
        ["leftArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = -150f, ["elbowDeg"] = 0f },
        ["rightArm"] = new Dictionary<string, object?> { ["shoulderDeg"] = -30f, ["elbowDeg"] = 0f }
    };

    static (float X, float Y) HeadCentre(Dictionary<string, object?> fig)
    {
        var c = (IDictionary)((IDictionary)fig["head"]!)["center"]!;
        return (Convert.ToSingle(c["x"]), Convert.ToSingle(c["y"]));
    }

    static Dictionary<string, object?> Point(float x, float y) => new() { ["x"] = x, ["y"] = y };

    static Dictionary<string, object?> Look(Dictionary<string, object?> fig, float x, float y, object? options = null) =>
        Kit.CheckLookPath(fig, Point(x, y), options);

    [Fact]
    public void ALookUpIsClear()
    {
        var fig = Figure();
        var (hx, hy) = HeadCentre(fig);
        var look = Look(fig, hx, hy - 300f);

        Assert.False((bool)look["blocked"]!);
        Assert.Equal(1f, Convert.ToSingle(look["clear"]), 3);
        Assert.Null(look["firstHit"]);
    }

    /// <summary>The neck joins the head to the body, so a look down is blocked by the chest, not by the neck.</summary>
    [Fact]
    public void ALookAtTheFeetRunsThroughTheTorso()
    {
        var fig = Figure();
        var (hx, hy) = HeadCentre(fig);
        var look = Look(fig, hx, Top + Height - 2f);

        Assert.True((bool)look["blocked"]!);
        var first = (IDictionary)look["firstHit"]!;
        Assert.Equal("torso", first["group"]);
        Assert.NotEqual("neck", first["by"]);
        Assert.True(Convert.ToSingle(look["clear"]) < 0.9f);
    }

    [Theory]
    [InlineData(-400f)]
    [InlineData(400f)]
    public void ARaisedArmBlocksALookToItsSide(float dx)
    {
        var fig = Figure(ArmsUp());
        var (hx, hy) = HeadCentre(fig);

        var look = Look(fig, hx + dx, hy - 40f);
        Assert.True((bool)look["blocked"]!, "an arm raised to head height should be in the way of a look past it");
        Assert.Contains((string?)((IDictionary)look["firstHit"]!)["group"], new[] { "leftArm", "rightArm" });

        var ignored = Look(fig, hx + dx, hy - 40f, new Dictionary<string, object?> { ["ignore"] = new object?[] { "leftArm", "rightArm" } });
        Assert.False((bool)ignored["blocked"]!);
    }

    [Fact]
    public void AnObstacleOnTheSightlineBlocksIt()
    {
        var fig = Figure();
        var (hx, hy) = HeadCentre(fig);
        var mast = new CanvasPath();
        mast.Rect(hx + 150f, hy - 300f, 12f, 600f);

        var look = Look(fig, hx + 400f, hy, new Dictionary<string, object?> { ["obstacles"] = new Dictionary<string, object?> { ["mast"] = mast } });

        Assert.True((bool)look["blocked"]!);
        var hit = (IDictionary)look["firstHit"]!;
        Assert.Equal("mast", hit["by"]);
        Assert.Equal("obstacle", hit["group"]);
        Assert.Equal(12f, Convert.ToSingle(hit["length"]), 1f);
    }

    /// <summary>Another figure's geometry goes in as obstacles, its parts named after it.</summary>
    [Fact]
    public void AnotherFigureCanBlockTheLook()
    {
        var fig = Figure();
        var (hx, hy) = HeadCentre(fig);
        var other = Kit.CreateFigureGeometry(Kit.CreateMannequinFigure(X + 200f, Top, Height, null));

        var look = Look(fig, hx + 400f, hy + 60f, new Dictionary<string, object?> { ["obstacles"] = new Dictionary<string, object?> { ["him"] = other } });

        Assert.True((bool)look["blocked"]!);
        Assert.StartsWith("him.", (string?)((IDictionary)look["firstHit"]!)["by"]);
    }

    [Fact]
    public void GeometryAndFigureGiveTheSameAnswer()
    {
        var fig = Figure(ArmsUp());
        var (hx, hy) = HeadCentre(fig);
        var fromFigure = Look(fig, hx + 400f, hy - 40f);
        var fromGeometry = Kit.CheckLookPath(Kit.CreateFigureGeometry(fig), Point(hx + 400f, hy - 40f),
            new Dictionary<string, object?> { ["from"] = Point(hx, hy) });

        Assert.Equal(fromFigure["blocked"], fromGeometry["blocked"]);
        Assert.Equal(Convert.ToSingle(fromFigure["clear"]), Convert.ToSingle(fromGeometry["clear"]), 3);
    }

    [Fact]
    public void AMisspelledIgnoreIsRefusedByName()
    {
        var fig = Figure();
        var ex = Assert.Throws<ArgumentException>(() => Look(fig, 0f, 0f, new Dictionary<string, object?> { ["ignore"] = new object?[] { "leftArms" } }));
        Assert.Contains("leftArms", ex.Message);
        Assert.Contains("leftArm", ex.Message);
    }

    [Fact]
    public void ALineObstacleIsRefused()
    {
        var fig = Figure();
        var ex = Assert.Throws<ArgumentException>(() => Look(fig, 600f, 60f, new Dictionary<string, object?>
        {
            ["obstacles"] = new Dictionary<string, object?> { ["boom"] = new Dictionary<string, object?> { ["x1"] = 0f, ["y1"] = 0f, ["x2"] = 10f, ["y2"] = 10f } }
        }));
        Assert.Contains("boom", ex.Message);
        Assert.Contains("strokeToPath", ex.Message);
    }

    [Fact]
    public void AnUnknownOptionIsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() => Look(Figure(), 600f, 60f, new Dictionary<string, object?> { ["ignored"] = "leftArm" }));
        Assert.Contains("ignored", ex.Message);
    }
}
