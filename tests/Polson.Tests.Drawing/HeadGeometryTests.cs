namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>createHeadGeometry</c> — the composed head Studio Manual 23 §7 said the toolkit did not have.
///
/// **The gap it closes is specific.** Landmarks placed features correctly and gave them nothing to
/// sit on, so a run drew two faces that read as masks on shoulder-masses. What is tested here is
/// therefore not "does it draw a head" — it is the three properties that make the composed head
/// trustworthy: that the masses agree with the construction they came from, that a parametric face
/// reaches the silhouette, and that the closed-form bounds agree with the paths.
/// </summary>
public class HeadGeometryTests : TestsRuntime
{
    const float H = 240f;
    const float OriginX = 400f;
    const float OriginY = 120f;

    static Dictionary<string, object?> Head(float yaw = 0f) =>
        new ConstructiveDrawingToolkit().CreateLoomisHead(OriginX, OriginY, H, yaw, 0f);

    static Dictionary<string, object?> Geometry(Dictionary<string, object?> head, object? options = null) =>
        new ConstructiveDrawingToolkit().CreateHeadGeometry(head, options);

    static CanvasPath Part(Dictionary<string, object?> geo, string name) =>
        (CanvasPath)((Dictionary<string, object?>)geo["parts"]!)[name]!;

    static SKRect Box(Dictionary<string, object?> geo, string key) => ((CanvasPath)geo[key]!).Path.Bounds;

    static float Bound(Dictionary<string, object?> geo, string key) =>
        Convert.ToSingle(((Dictionary<string, object?>)geo["bounds"]!)[key]!);

    static (float X, float Y) Point(Dictionary<string, object?> head, string group, string key)
    {
        var g = (Dictionary<string, object?>)head[group]!;
        var p = (Dictionary<string, object?>)g[key]!;
        return (Convert.ToSingle(p["x"]), Convert.ToSingle(p["y"]));
    }

    /// <summary>All four masses are drawn, and none of them is an empty path.</summary>
    /// <remarks>
    /// An empty <c>CanvasPath</c> unions away to nothing without complaining, so a mass that silently
    /// failed to build would show up only as a head with no ear — which is exactly the kind of
    /// absence the drawing-1 run proved nobody notices in a render.
    /// </remarks>
    [Fact]
    public void TestEveryMassIsDrawn()
    {
        var geo = Geometry(Head(35f));

        foreach (var name in new[] { "cranium", "jaw", "ear", "neck" })
        {
            var box = Part(geo, name).Path.Bounds;
            Assert.True(box.Width > 1f && box.Height > 1f, $"{name} is empty: {box}");
        }
    }

    /// <summary>
    /// The jaw stations sit exactly on the cranial ball this draws.
    /// </summary>
    /// <remarks>
    /// **This is the claim that the two cannot disagree, tested rather than asserted in a comment.**
    /// <c>createLoomisHead</c> derives the stations from <c>sqrt(ballR² − unit²)</c> about a ball it
    /// never returns; this rebuilds that ball from <c>brow.y − crown.y</c>. If either side ever
    /// changed its mind about the radius, the jaw would hang off a ball of a different size and the
    /// join would open — visibly at a large head, invisibly at panel size.
    /// </remarks>
    [Fact]
    public void TestTheJawHangsOffTheBallTheCraniumDraws()
    {
        var head = Head();                            // yaw 0: no foreshortening to account for
        var crownPt = ((Dictionary<string, object?>)head["crown"]!);
        var browPt = ((Dictionary<string, object?>)head["brow"]!);
        float cx = Convert.ToSingle(crownPt["x"]), cy = Convert.ToSingle(browPt["y"]);
        var ballR = cy - Convert.ToSingle(crownPt["y"]);

        foreach (var station in new[] { "nearStation", "farStation" })
        {
            var p = Point(head, "jaw", station);
            var d = MathF.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
            Assert.True(MathF.Abs(d - ballR) < 0.5f,
                $"{station} sits {d:F2} from the ball centre, ball radius {ballR:F2}");
        }
    }

    /// <summary>The silhouette is one shape covering every mass, not merely the head.</summary>
    [Fact]
    public void TestTheSilhouetteCoversEveryMass()
    {
        var head = Head(35f);
        var geo = Geometry(head);
        var silhouette = ((CanvasPath)geo["silhouette"]!).Path;

        foreach (var name in new[] { "cranium", "jaw", "ear", "neck" })
        {
            var box = Part(geo, name).Path.Bounds;
            Assert.True(silhouette.Contains(box.MidX, box.MidY), $"the silhouette does not cover the {name}");
        }
    }

    /// <summary>The neck hangs below the chin, which is the whole reason the head stops floating.</summary>
    [Fact]
    public void TestTheNeckHangsBelowTheChin()
    {
        var head = Head(35f);
        var chinY = Convert.ToSingle(((Dictionary<string, object?>)head["chin"]!)["y"]);
        var geo = Geometry(head);

        Assert.True(Box(geo, "silhouette").Bottom > chinY + H * 0.2f,
            "the silhouette ends at the chin — nothing is carrying the head");
        Assert.True(Box(geo, "mass").Bottom <= chinY + 1f, "the neck leaked into mass, which is the head alone");
    }

    /// <summary>A neck of zero length is omitted rather than drawn as a stub.</summary>
    [Fact]
    public void TestTheNeckCanBeOmitted()
    {
        var geo = Geometry(Head(35f), new Dictionary<string, object?> { ["neckLength"] = 0f });

        Assert.True(Part(geo, "neck").Path.IsEmpty, "neckLength 0 still drew a neck");
        Assert.Equal(Box(geo, "mass").Bottom, Box(geo, "silhouette").Bottom, 1);
    }

    /// <summary>
    /// A squared jaw reaches the silhouette, and a tapered one narrows it.
    /// </summary>
    /// <remarks>
    /// The point of composing the head at all is that <c>createParametricHead</c>'s five numbers stop
    /// being features floating on a generic outline and start changing the shape of the person. If
    /// this ever came apart, every character would share one silhouette and differ only in the marks
    /// inside it — which is the failure the parameter layer was built to prevent.
    /// </remarks>
    [Fact]
    public void TestAParametricJawChangesTheSilhouette()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        Dictionary<string, object?> Shaped(float jawShape) =>
            toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["jawShape"] = jawShape });

        var squaredHead = Shaped(1f);
        var squared = Geometry(squaredHead);
        var tapered = Geometry(Shaped(-1f));

        Assert.True(Part(squared, "jaw").Path.Bounds.Width > Part(tapered, "jaw").Path.Bounds.Width + 2f,
            "jawShape did not reach the jaw mass at all");

        // **The overall width proves nothing here, which is worth knowing before writing this test
        // the obvious way.** At yaw 0 the ball is 3 units across and the stations sit at 1.118 units,
        // so a squared jaw is still inside the cranium's own diameter and the bounding box does not
        // move. The jaw shows on the silhouette lower down, at the angle — so that is where to probe.
        var angle = Point(squaredHead, "jaw", "nearAngle");
        var axisX = Convert.ToSingle(((Dictionary<string, object?>)squaredHead["chin"]!)["x"]);
        var probeX = axisX + (angle.X - axisX) * 0.95f;

        Assert.True(((CanvasPath)squared["silhouette"]!).Path.Contains(probeX, angle.Y),
            "the squared jaw did not reach the silhouette");
        Assert.False(((CanvasPath)tapered["silhouette"]!).Path.Contains(probeX, angle.Y),
            "the tapered jaw reaches as far as the squared one — the two characters share an outline");
    }

    /// <summary>The ear widens with the turn — the opposite of the far eye, and easy to get backwards.</summary>
    [Fact]
    public void TestTheEarWidensAsTheHeadTurns()
    {
        var frontal = Part(Geometry(Head(0f)), "ear").Path.Bounds.Width;
        var turned = Part(Geometry(Head(55f)), "ear").Path.Bounds.Width;

        Assert.True(turned > frontal + 1f, $"ear is {turned:F1} turned against {frontal:F1} frontal — it narrowed");
    }

    /// <summary>
    /// The reported bounds contain the drawn silhouette, and do not wildly overstate it.
    /// </summary>
    /// <remarks>
    /// <c>bounds</c> is accumulated closed-form while the masses are built, never measured from the
    /// paths — the same split <c>createFigureGeometry</c> uses so a caller can size a head without
    /// paying for geometry. Two computations of one rectangle is exactly where they drift apart, so
    /// this pins them together. Containment must be exact; the slack allowance is loose because the
    /// jaw's padding box is a corner box rather than a true offset.
    /// </remarks>
    [Fact]
    public void TestTheReportedBoundsAgreeWithTheDrawnSilhouette()
    {
        foreach (var padding in new[] { 0f, 12f })
        {
            var geo = Geometry(Head(35f), new Dictionary<string, object?> { ["padding"] = padding });
            var drawn = Box(geo, "silhouette");

            Assert.True(Bound(geo, "x") <= drawn.Left + 0.5f, $"bounds cut the left edge at padding {padding}");
            Assert.True(Bound(geo, "y") <= drawn.Top + 0.5f, $"bounds cut the top edge at padding {padding}");
            Assert.True(Bound(geo, "x2") >= drawn.Right - 0.5f, $"bounds cut the right edge at padding {padding}");
            Assert.True(Bound(geo, "y2") >= drawn.Bottom - 0.5f, $"bounds cut the bottom edge at padding {padding}");
            Assert.True(Bound(geo, "width") < drawn.Width * 1.5f + 1f, "bounds overstate the width");
        }
    }

    /// <summary>Padding grows the head outward, which is how a hood or a collar is derived.</summary>
    [Fact]
    public void TestPaddingGrowsTheHead()
    {
        var bare = Box(Geometry(Head(35f)), "mass");
        var padded = Box(Geometry(Head(35f), new Dictionary<string, object?> { ["padding"] = 10f }), "mass");

        Assert.True(padded.Width > bare.Width + 15f, $"padded {padded.Width:F1} against bare {bare.Width:F1}");
        Assert.True(padded.Height > bare.Height + 15f, $"padded {padded.Height:F1} against bare {bare.Height:F1}");
    }

    /// <summary>A misspelled option is refused by name rather than silently ignored.</summary>
    [Fact]
    public void TestAnUnknownOptionIsRefusedByName()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Geometry(Head(), new Dictionary<string, object?> { ["neckLenght"] = 0.4f }));

        Assert.Contains("neckLenght", error.Message);
        Assert.Contains("createHeadGeometry option", error.Message);
        Assert.Contains("neckLength", error.Message);      // and what to write instead
    }

    /// <summary>Something that is not a head is refused by name, pointing at what would be one.</summary>
    [Fact]
    public void TestSomethingThatIsNotAHeadIsRefused()
    {
        var error = Assert.Throws<ArgumentException>(() => Geometry(null!, null));
        Assert.Contains("createLoomisHead", error.Message);
    }

    /// <summary>The head passed in is never modified, so one canon head can serve a crowd.</summary>
    [Fact]
    public void TestTheHeadIsNotModified()
    {
        var head = Head(35f);
        var before = Point(head, "jaw", "nearStation");

        Geometry(head, new Dictionary<string, object?> { ["padding"] = 8f });

        var after = Point(head, "jaw", "nearStation");
        Assert.Equal(before.X, after.X, 4);
        Assert.Equal(before.Y, after.Y, 4);
    }

    /// <summary>
    /// The comic skull is five eye-widths where Loomis's ball is six, and no shorter.
    /// </summary>
    /// <remarks>
    /// Studio Manual 23 §1 measured both schools on one head: 6.0 eye-widths against 5. That is the
    /// only departure from the construction this call makes, it is opt-in, and it narrows — a skull
    /// that also lost height would be a different head rather than a comic one.
    /// </remarks>
    [Fact]
    public void TestTheComicSkullIsNarrowerAndNoShorter()
    {
        var loomis = Part(Geometry(Head(0f)), "cranium").Path.Bounds;
        var comic = Part(Geometry(Head(0f), new Dictionary<string, object?> { ["skull"] = "comic" }), "cranium").Path.Bounds;

        Assert.Equal(loomis.Width * 5f / 6f, comic.Width, 1);
        Assert.Equal(loomis.Height, comic.Height, 1);
    }

    /// <summary>An unknown skull is refused by name rather than quietly drawing Loomis's.</summary>
    [Fact]
    public void TestAnUnknownSkullIsRefusedByName()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            Geometry(Head(), new Dictionary<string, object?> { ["skull"] = "marvel" }));

        Assert.Contains("marvel", error.Message);
        Assert.Contains("comic", error.Message);
    }

    /// <summary>
    /// <c>neckLength</c> is the whole extent below the chin, base cap included.
    /// </summary>
    /// <remarks>
    /// Measured to the capsule's centre instead, the drawn end lands a further half-width down — the
    /// first version of this overshot by a third and rendered as a light-bulb stem. A length option
    /// whose number is not the length is the kind of thing a caller tunes around forever.
    /// </remarks>
    [Theory]
    [InlineData(0.30f)]
    [InlineData(0.55f)]
    public void TestNeckLengthIsTheWholeExtentBelowTheChin(float fraction)
    {
        var head = Head(35f);
        var chinY = Convert.ToSingle(((Dictionary<string, object?>)head["chin"]!)["y"]);
        var geo = Geometry(head, new Dictionary<string, object?> { ["neckLength"] = fraction });

        Assert.Equal(fraction * H, Box(geo, "silhouette").Bottom - chinY, 1);
    }

    /// <summary>
    /// The jaw, not the ball, carries the lower face.
    /// </summary>
    /// <remarks>
    /// The ball runs out half a unit above the chin, so everything below that is jaw or nothing —
    /// which is the whole reason the jaw has to be a mass rather than a set of stations. Asserted at
    /// the chin because that is where "nothing" would be most visible and least explicable.
    /// </remarks>
    [Fact]
    public void TestTheJawCarriesTheLowerFace()
    {
        var head = Head(40f);
        var chinPt = (Dictionary<string, object?>)head["chin"]!;
        float x = Convert.ToSingle(chinPt["x"]), y = Convert.ToSingle(chinPt["y"]) - 4f;
        var geo = Geometry(head);

        Assert.False(Part(geo, "cranium").Path.Contains(x, y), "the ball reaches the chin — it should have run out");
        Assert.True(Part(geo, "jaw").Path.Contains(x, y), "the jaw does not reach its own chin");
        Assert.True(((CanvasPath)geo["silhouette"]!).Path.Contains(x, y), "nothing carries the lower face");
    }

    /// <summary>Same head, same geometry — the consistency the parametric layer depends on.</summary>
    [Fact]
    public void TestItIsDeterministic()
    {
        var a = Box(Geometry(Head(35f)), "silhouette");
        var b = Box(Geometry(Head(35f)), "silhouette");

        Assert.Equal(a.Left, b.Left, 4);
        Assert.Equal(a.Top, b.Top, 4);
        Assert.Equal(a.Right, b.Right, 4);
        Assert.Equal(a.Bottom, b.Bottom, 4);
    }
}
