namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>Scene.createSet</c> and <c>Scene.createShot</c> — one space, seen across several panels.
///
/// **What is under test is continuity, and it is the one property a render cannot check.** No
/// measurement in the studio says the table moved between panel 2 and panel 3; a reader simply feels
/// that the room is not the same room. So these assert the thing that makes drift impossible rather
/// than unlikely: there is one table, and a shot is a crop and a scale of it.
///
/// This came out of a live `storyboard_quick` run that declined `createLayeredScene` and hand-rolled
/// its own persistent set — correctly, because a per-panel independent arrangement is the opposite of
/// what a sequence needs.
/// </summary>
public class SetAndShotTests : TestsRuntime
{
    static SceneToolkit Kit => new();
    static LayoutToolkit Lay => new();

    static Dictionary<string, object?> Room() => Kit.CreateSet(Lay.Rect(0f, 0f, 100f, 60f),
        new Dictionary<string, object?>
        {
            ["horizon"] = 0.55f,
            ["elements"] = new List<object?>
            {
                new Dictionary<string, object?> { ["name"] = "window", ["x"] = 68f, ["y"] = 8f, ["width"] = 22f, ["height"] = 20f },
                new Dictionary<string, object?> { ["name"] = "table", ["x"] = 10f, ["y"] = 34f, ["width"] = 26f, ["height"] = 10f }
            }
        });

    static Dictionary<string, object> Panel(float w = 300f, float h = 200f) => Lay.Rect(0f, 0f, w, h);

    static float Of(object? d, string key) => Convert.ToSingle(((IDictionary<string, object>)d!)[key]);

    static float OfN(Dictionary<string, object?> d, string key) => Convert.ToSingle(d[key]!);

    /// <summary>The set lays out its two masses from the horizon, and they tile the frame.</summary>
    [Fact]
    public void TestTheHorizonSplitsTheSet()
    {
        var set = Room();
        var wall = (Dictionary<string, object>)set["wall"]!;
        var floor = (Dictionary<string, object>)set["floor"]!;

        Assert.Equal(33f, OfN(set, "floorY"), 3);
        Assert.Equal(0f, Of(wall, "y"), 3);
        Assert.Equal(33f, Of(wall, "y2"), 3);
        Assert.Equal(33f, Of(floor, "y"), 3);
        Assert.Equal(60f, Of(floor, "y2"), 3);
    }

    /// <summary>
    /// The same element is the same element in every shot — which is what continuity *is*.
    /// </summary>
    /// <remarks>
    /// Not a property of careful drawing: there is one table in one set, and a shot cannot move it
    /// because a shot does not own it. This test would be meaningless against a model that composed
    /// each panel independently, which is exactly why that model was wrong for a sequence.
    /// </remarks>
    [Fact]
    public void TestAnElementCannotDriftBetweenShots()
    {
        var set = Room();
        var shots = new[] { "establishing", "long", "full", "medium", "closeUp" }
            .Select(name => Kit.CreateShot(set, Panel(), new Dictionary<string, object?>
            {
                ["shot"] = name,
                ["focusOn"] = "table"
            })).ToArray();

        // In set coordinates the table is one rectangle, whatever the camera does.
        var table = (Dictionary<string, object>)((IDictionary<string, object?>)set["elements"]!)["table"]!;
        Assert.Equal(10f, Of(table, "x"), 4);

        // On the page it scales, and the scaling is exactly the shot's own scale — never a redrawing.
        foreach (var shot in shots)
        {
            var placed = shot.Element("table")!;
            Assert.Equal(26f * shot.Scale, Of(placed, "width"), 3);
            Assert.Equal(10f * shot.Scale, Of(placed, "height"), 3);
        }
    }

    /// <summary>Closer shots frame less of the set and magnify more.</summary>
    [Fact]
    public void TestTheLadderRunsClosestToWidest()
    {
        var set = Room();
        var ladder = new[] { "extremeCloseUp", "closeUp", "medium", "full", "long", "extremeLong" };
        var scales = ladder.Select(n => Kit.CreateShot(set, Panel(),
            new Dictionary<string, object?> { ["shot"] = n }).Scale).ToArray();

        for (var i = 1; i < scales.Length; i++)
        {
            Assert.True(scales[i] < scales[i - 1],
                $"{ladder[i]} magnifies {scales[i]:F2}, not less than {ladder[i - 1]}'s {scales[i - 1]:F2}");
        }
    }

    /// <summary>
    /// The widest shot shows everything; the closest shows less.
    /// </summary>
    [Fact]
    public void TestAWideShotHoldsTheWholeSet()
    {
        var set = Room();
        var wide = Kit.CreateShot(set, Panel(), new Dictionary<string, object?> { ["shot"] = "establishing" });
        var tight = Kit.CreateShot(set, Panel(), new Dictionary<string, object?>
        {
            ["shot"] = "extremeCloseUp",
            ["focusOn"] = "table"
        });

        Assert.True(wide.Shows("window"));
        Assert.True(wide.Shows("table"));
        Assert.True(tight.Shows("table"));
        Assert.False(tight.Shows("window"), "an extreme close-up on the table still frames the window");
    }

    /// <summary>
    /// The camera cannot pan off the edge of the described space.
    /// </summary>
    /// <remarks>
    /// A focus near a corner gives a frame pressed against that corner, not a frame half full of
    /// nothing. That is the decision a person makes at a board, and making it structural means a
    /// storyboard cannot accidentally show undescribed space.
    /// </remarks>
    [Fact]
    public void TestTheViewIsClampedInsideTheSet()
    {
        var set = Room();
        var bounds = (Dictionary<string, object>)set["bounds"]!;

        foreach (var corner in new[]
                 {
                     new Dictionary<string, object?> { ["x"] = -400f, ["y"] = -400f },
                     new Dictionary<string, object?> { ["x"] = 900f, ["y"] = 900f }
                 })
        {
            var shot = Kit.CreateShot(set, Panel(), new Dictionary<string, object?>
            {
                ["shot"] = "medium",
                ["subjectAt"] = corner
            });

            Assert.True(Of(shot.View, "x") >= Of(bounds, "x") - 0.01f);
            Assert.True(Of(shot.View, "y") >= Of(bounds, "y") - 0.01f);
            Assert.True(Of(shot.View, "x2") <= Of(bounds, "x2") + 0.01f);
            Assert.True(Of(shot.View, "y2") <= Of(bounds, "y2") + 0.01f);
        }
    }

    /// <summary>Projection agrees with itself: a rectangle placed is its own corners projected.</summary>
    [Fact]
    public void TestPlaceAndPointAgree()
    {
        var shot = Kit.CreateShot(Room(), Panel(), new Dictionary<string, object?>
        {
            ["shot"] = "full",
            ["focusOn"] = "window"
        });

        var box = Lay.Rect(30f, 20f, 12f, 8f);
        var placed = shot.Place(box);
        var corner = shot.Point(30f, 20f);
        var far = shot.Point(42f, 28f);

        Assert.Equal(Convert.ToSingle(corner["x"]!), Of(placed, "x"), 3);
        Assert.Equal(Convert.ToSingle(corner["y"]!), Of(placed, "y"), 3);
        Assert.Equal(Convert.ToSingle(far["x"]!), Of(placed, "x2"), 3);
        Assert.Equal(Convert.ToSingle(far["y"]!), Of(placed, "y2"), 3);
    }

    /// <summary>
    /// Background detail falls out of the distance, carrying Manual 20 §2's rule.
    /// </summary>
    /// <remarks>
    /// *The closer the camera, the less background you should draw* — which cuts against the reflex
    /// to fill the frame, and is therefore worth encoding rather than stating.
    /// </remarks>
    [Fact]
    public void TestBackgroundDetailFollowsTheDistance()
    {
        var set = Room();
        string Detail(string shot) => Kit.CreateShot(set, Panel(),
            new Dictionary<string, object?> { ["shot"] = shot }).BackgroundDetail;

        Assert.Equal("none", Detail("extremeCloseUp"));
        Assert.Equal("none", Detail("closeUp"));
        Assert.Equal("minimal", Detail("medium"));
        Assert.Equal("some", Detail("full"));
        Assert.Equal("full", Detail("long"));
        Assert.Equal("full", Detail("establishing"));
    }

    /// <summary>The frame takes the panel's aspect, so a wide panel is not a squashed square.</summary>
    [Fact]
    public void TestTheFrameTakesThePanelsAspect()
    {
        var set = Room();
        foreach (var (w, h) in new[] { (300f, 200f), (200f, 300f), (400f, 100f) })
        {
            var shot = Kit.CreateShot(set, Panel(w, h), new Dictionary<string, object?> { ["shot"] = "medium" });
            var ratio = Of(shot.View, "width") / Of(shot.View, "height");
            Assert.Equal(w / h, ratio, 2);
        }
    }

    /// <summary>A shot, a focus and an element name are all refused by name when misspelled.</summary>
    [Fact]
    public void TestUnknownNamesAreRefused()
    {
        var set = Room();

        var shot = Assert.Throws<ArgumentException>(() => Kit.CreateShot(set, Panel(),
            new Dictionary<string, object?> { ["shot"] = "midshot" }));
        Assert.Contains("midshot", shot.Message);
        Assert.Contains("medium", shot.Message);

        var focus = Assert.Throws<ArgumentException>(() => Kit.CreateShot(set, Panel(),
            new Dictionary<string, object?> { ["focusOn"] = "doorway" }));
        Assert.Contains("doorway", focus.Message);
        Assert.Contains("window", focus.Message);

        var option = Assert.Throws<ArgumentException>(() => Kit.CreateShot(set, Panel(),
            new Dictionary<string, object?> { ["focussOn"] = "table" }));
        Assert.Contains("focussOn", option.Message);
    }

    /// <summary>Two elements sharing a name is refused rather than resolved last-wins.</summary>
    /// <remarks>
    /// Last-wins would make one of them unreachable by `focusOn` and silently absent from the shot,
    /// which reads as a drawing bug rather than as a naming one.
    /// </remarks>
    [Fact]
    public void TestDuplicateElementNamesAreRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() => Kit.CreateSet(Lay.Rect(0f, 0f, 100f, 60f),
            new Dictionary<string, object?>
            {
                ["elements"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["name"] = "door", ["x"] = 1f, ["y"] = 1f, ["width"] = 5f, ["height"] = 5f },
                    new Dictionary<string, object?> { ["name"] = "door", ["x"] = 9f, ["y"] = 1f, ["width"] = 5f, ["height"] = 5f }
                }
            }));

        Assert.Contains("door", ex.Message);
        Assert.Contains("unique", ex.Message);
    }

    /// <summary>An element without a name is refused, since a shot could never focus on it.</summary>
    [Fact]
    public void TestAnUnnamedElementIsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() => Kit.CreateSet(Lay.Rect(0f, 0f, 100f, 60f),
            new Dictionary<string, object?>
            {
                ["elements"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["x"] = 1f, ["y"] = 1f, ["width"] = 5f, ["height"] = 5f }
                }
            }));

        Assert.Contains("name", ex.Message);
    }

    /// <summary>Asking a shot about an element the set does not have is a question, not an error.</summary>
    [Fact]
    public void TestAskingAboutAnAbsentElementIsNotAnError()
    {
        var shot = Kit.CreateShot(Room(), Panel(), new Dictionary<string, object?> { ["shot"] = "medium" });

        Assert.Null(shot.Element("lamp"));
        Assert.False(shot.Shows("lamp"));
    }

    /// <summary>A set with no elements is a perfectly good empty room.</summary>
    [Fact]
    public void TestASetNeedsNoElements()
    {
        var set = Kit.CreateSet(Lay.Rect(0f, 0f, 100f, 60f));
        var shot = Kit.CreateShot(set, Panel(), new Dictionary<string, object?> { ["shot"] = "long" });

        Assert.Empty((IDictionary<string, object?>)set["elements"]!);
        Assert.True(shot.Scale > 0f);
    }
}
