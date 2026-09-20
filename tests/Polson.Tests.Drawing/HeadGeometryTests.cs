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

    /// <summary>Every mass the call returned, read from the result rather than listed here.</summary>
    /// <remarks>
    /// <b>Named by hand until 2026-09-19, and the omission was invisible in exactly the way these
    /// tests exist to catch.</b> The list read <c>{ cranium, jaw, ear, neck }</c>, so when
    /// <c>nearEar</c> was added the two tests that walk "every mass" walked four of five and still
    /// passed. A list whose whole job is to be exhaustive cannot be kept by hand — the same lesson
    /// <c>TestEveryParameterAtZeroIsAlsoTheCanon</c> learned about the parameter names.
    /// </remarks>
    static IEnumerable<string> PartNames(Dictionary<string, object?> geo) =>
        ((Dictionary<string, object?>)geo["parts"]!).Keys;

    static SKRect Box(Dictionary<string, object?> geo, string key) => ((CanvasPath)geo[key]!).Path.Bounds;

    /// <summary>How far the outline stands off the head's axis at height <paramref name="y"/>.</summary>
    /// <remarks>
    /// Bisected against <c>Contains</c> rather than rasterised, so it is exact and costs nothing. A
    /// ray from the facial axis outward crosses a head's silhouette once, which is what lets a
    /// bisection stand in for a scan.
    /// </remarks>
    static float Reach(CanvasPath path, float y, float dir)
    {
        float inside = OriginX, outside = OriginX + (dir * H * 2f);
        for (var i = 0; i < 40; i++)
        {
            var mid = (inside + outside) * 0.5f;
            if (path.Path.Contains(mid, y)) inside = mid; else outside = mid;
        }

        return MathF.Abs(inside - OriginX);
    }

    /// <summary>The largest single-pixel step INWARD anywhere between the brow and the chin.</summary>
    static float WorstStep(Dictionary<string, object?> head, CanvasPath outline)
    {
        var brow = Convert.ToSingle(((Dictionary<string, object?>)head["brow"]!)["y"]);
        var chin = Convert.ToSingle(((Dictionary<string, object?>)head["chin"]!)["y"]);
        var worst = 0f;
        foreach (var dir in new[] { -1f, 1f })
        {
            var previous = Reach(outline, brow, dir);
            for (var y = brow + 1f; y <= chin; y += 1f)
            {
                var here = Reach(outline, y, dir);
                worst = MathF.Max(worst, previous - here);
                previous = here;
            }
        }

        return worst;
    }

    /// <summary>The head as it was before the cheek: every other mass, unioned.</summary>
    static CanvasPath WithoutCheek(Dictionary<string, object?> geo) =>
        Part(geo, "cranium").Union(Part(geo, "jaw")).Union(Part(geo, "ear")).Union(Part(geo, "nearEar"));

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

        foreach (var name in PartNames(geo))
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

        foreach (var name in PartNames(geo))
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

    /// <summary>A frontal head is symmetric, because it has two ears.</summary>
    /// <remarks>
    /// <para>
    /// <b>The defect this replaced was documented in two places and shipped anyway.</b> The
    /// construction carries one <c>jaw.ear</c> landmark, so every frontal head came back with a bump
    /// on one side and a clean curve on the other; the limits note said so and told the caller to
    /// mirror <c>parts.ear</c> themselves. That is the wrong default twice — it is anatomy rather
    /// than style, so nobody chose it, and the instruction was missed by the author of the toolkit
    /// the first time he drew a frontal sheet with it.
    /// </para>
    /// <para>
    /// Measured on the silhouette rather than on the part, because a part that exists and is
    /// swallowed by the cranium proves nothing about what a reader sees.
    /// </para>
    /// <para>
    /// <b>And measured along the outline rather than at the bounds, because the bounds version of
    /// this test passed for a year over a crooked jaw.</b> A head's width is set by its ears, so the
    /// box is symmetric whatever the jaw does underneath it — and the jaw was doing 7px on a 240px
    /// head, because <c>jaw.nearAngle</c> was computed from the station where <c>jaw.angle</c> was
    /// computed from the ear. One number at the widest point cannot see a shape; walking the edge
    /// can. Fixed 2026-09-19; see the remark on <c>nearAngleX</c> in the toolkit.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestAFrontalHeadIsSymmetricAboutItsOwnAxis()
    {
        var head = Head(0f);
        var geo = Geometry(head);
        var outline = (CanvasPath)geo["silhouette"]!;
        var box = outline.Path.Bounds;

        Assert.Equal(OriginX - box.Left, box.Right - OriginX, 1);

        var brow = Convert.ToSingle(((Dictionary<string, object?>)head["brow"]!)["y"]);
        var chin = Convert.ToSingle(((Dictionary<string, object?>)head["chin"]!)["y"]);
        float worst = 0f, worstY = 0f;
        for (var y = brow; y <= chin; y += 1f)
        {
            var gap = MathF.Abs(Reach(outline, y, -1f) - Reach(outline, y, 1f));
            if (gap > worst) { worst = gap; worstY = y; }
        }

        Assert.True(worst < 0.5f,
            $"a head with no turn in it is {worst:F2}px wider on one side at y {worstY:F0}, on a {H}px head");
    }

    /// <summary>
    /// A frontal head's jaw landmarks mirror each other exactly.
    /// </summary>
    /// <remarks>
    /// <b>The direct regression test for the defect the outline test above could not see.</b>
    /// <c>jaw.angle</c> was derived from the ear landmark (<c>originX - unit x cos</c>) and
    /// <c>jaw.nearAngle</c> from the jaw station (<c>originX + sqrt(ballR^2 - unit^2)</c>) — 1.000
    /// units against 1.118 — so a square-on head measured -0.750 against +0.868, with the chin
    /// corners inheriting it at -0.600 against +0.694. <c>nearAngleX</c> is now written as the far
    /// angle mirrored about the axis rather than as its own formula, so the two cannot restate the
    /// same rule differently again.
    ///
    /// Asserted on the landmarks rather than on the drawn outline because that is where the defect
    /// lives: a rasterised profile carries a pixel or two of its own quantisation, and a tolerance
    /// wide enough to absorb that is wide enough to let a small asymmetry back in.
    /// </remarks>
    [Fact]
    public void TestAFrontalHeadsJawMirrorsItself()
    {
        var head = Head(0f);
        var chin = Point(head, "jaw", "chin");
        Assert.Equal(OriginX, chin.X, 3);          // the chin is on the axis, or nothing below mirrors

        foreach (var (far, near) in new[]
                 { ("angle", "nearAngle"), ("chinFar", "chinNear"), ("farStation", "nearStation") })
        {
            var a = Point(head, "jaw", far);
            var b = Point(head, "jaw", near);
            Assert.Equal(OriginX - a.X, b.X - OriginX, 3);
            Assert.Equal(a.Y, b.Y, 3);
        }
    }

    /// <summary>
    /// The near ear narrows away as the far one widens, and a turned head is unchanged by it.
    /// </summary>
    /// <remarks>
    /// <b>One fact read from both sides</b> — an ear is edge-on frontally and full-face in profile.
    /// The near one is then swallowed by the cranium it is unioned into, which is the physics rather
    /// than a fudge: a frontal ear sits exactly <i>on</i> the ball's silhouette, so any turn toward
    /// it puts it behind the head's own edge. The second assertion is the compatibility one — every
    /// head already drawn at a turn must render exactly as it did.
    /// </remarks>
    [Fact]
    public void TestTheNearEarNarrowsWithTheTurnAndLeavesATurnedOutlineAlone()
    {
        Assert.True(Part(Geometry(Head(55f)), "nearEar").Path.Bounds.Width
                  < Part(Geometry(Head(0f)), "nearEar").Path.Bounds.Width - 1f,
            "the near ear should close as the head turns away from it");

        // At yaw 0 the two ears are the same width; the head is symmetric and neither is 'near'.
        Assert.Equal(Part(Geometry(Head(0f)), "ear").Path.Bounds.Width,
                     Part(Geometry(Head(0f)), "nearEar").Path.Bounds.Width, 2);

        // And a turned head's outline is exactly what it was before the near ear existed.
        var turned = ((CanvasPath)Geometry(Head(30f))["silhouette"]!).Path.Bounds;
        var crownX = Convert.ToSingle(((Dictionary<string, object?>)Head(30f)["crown"]!)["x"]);
        Assert.Equal(crownX - turned.Left, turned.Right - crownX, 1);
    }

    /// <summary>
    /// The cheek closes the step under the ear, and the step is there to close.
    /// </summary>
    /// <remarks>
    /// <b>This is the regression test for a defect three constructions failed to fix, two of them
    /// while appearing to.</b> The limits note called it "a shallow concave step" where the ball's
    /// curve meets the jaw's. Printing the outline row by row said otherwise: on a 480px frontal
    /// head the far edge holds 205-211px off the axis down to y=564 and then reads 153 at y=572 -
    /// a 45px cliff in eight rows, at <b>the foot of the ear</b>, with everything above and below
    /// it already smooth. The ear is a tall narrow ellipse riding a ball that collapses under it.
    ///
    /// <b>Both halves are asserted, and that is the point.</b> A test that only checked the mended
    /// head would pass equally well if the cheek were deleted and the ear shortened, or if some
    /// later change made the step vanish for an unrelated reason. Asserting that the bare masses
    /// still carry the step is what keeps this a test of the cheek.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(10f)]
    [InlineData(20f)]
    public void TestTheCheekClosesTheStepUnderTheEar(float yaw)
    {
        var head = Head(yaw);
        var geo = Geometry(head, new Dictionary<string, object?> { ["neckLength"] = 0f });

        var before = WorstStep(head, WithoutCheek(geo));
        var after = WorstStep(head, (CanvasPath)geo["silhouette"]!);

        Assert.True(before > H * 0.04f,
            $"the bare masses no longer step under the ear ({before:F1}px on a {H}px head) - this test is measuring nothing");
        Assert.True(after < H * 0.015f,
            $"the cheek left a {after:F1}px step on a {H}px head (was {before:F1}px)");
    }

    /// <summary>
    /// The cheek fills inward of the ear and never makes the head wider.
    /// </summary>
    /// <remarks>
    /// It is strung between two things already on the outline - the ear it is tangent to, and the
    /// jaw angle - so it cannot reach past either. That is what makes it safe to add to every head
    /// already drawn: a face stops having a bite taken out of it and keeps the width it had.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(35f)]
    public void TestTheCheekNeverWidensTheHead(float yaw)
    {
        var geo = Geometry(Head(yaw), new Dictionary<string, object?> { ["neckLength"] = 0f });
        var bare = WithoutCheek(geo).Path.Bounds;
        var full = Box(geo, "silhouette");

        Assert.Equal(bare.Left, full.Left, 1);
        Assert.Equal(bare.Right, full.Right, 1);
    }

    /// <summary>
    /// The near cheek empties as the head turns, with nothing applying a turn factor to it.
    /// </summary>
    /// <remarks>
    /// A cheek is at the silhouette when you are looking at the front of a head, and in the middle
    /// of the face once that side has come toward you. Nothing here is told that: the near cheek
    /// hangs off the near ear, which already narrows and rides inboard at <c>Reach x cos</c>, so
    /// the whole triangle falls inside the cranium on its own. Testing the behaviour rather than
    /// the factor is what would catch someone simplifying the near ear and silently taking this
    /// with it.
    /// </remarks>
    [Fact]
    public void TestTheNearCheekEmptiesAsTheHeadTurns()
    {
        static float Gain(float yaw)
        {
            var geo = Geometry(Head(yaw), new Dictionary<string, object?> { ["neckLength"] = 0f });
            var without = Part(geo, "cranium").Union(Part(geo, "jaw")).Union(Part(geo, "ear"))
                                              .Union(Part(geo, "nearEar")).Union(Part(geo, "farCheek"));
            var with = without.Union(Part(geo, "nearCheek"));
            return with.Subtract(without).Path.Bounds.Width;
        }

        Assert.True(Gain(0f) > 1f, "the near cheek should be part of a frontal head's outline");
        Assert.True(Gain(55f) < Gain(0f) * 0.5f,
            "the near cheek should fade as that side turns toward the viewer");
    }

    /// <summary>
    /// A frontal head gets two cheeks, drawn to the same height, and stays symmetric overall.
    /// </summary>
    /// <remarks>
    /// The property the near ear is held to, for the same reason: at yaw 0 neither side is near or
    /// far, so anything that reads the turn has to come out even. A sign error in the tangent solve
    /// - which picks the outer of two roots by which side of the axis it falls on - would show up
    /// here and nowhere else, because on a turned head a wrong cheek simply vanishes inside the
    /// cranium.
    ///
    /// <b>Their widths were not compared when this was written, and the reason is a defect this
    /// test found upstream of the cheek.</b> A frontal head's jaw was not symmetric — <c>jaw.angle</c>
    /// sat at -0.750 units off the axis against <c>jaw.nearAngle</c> at +0.868 — so the two cheeks,
    /// which hang off those angles, differed by exactly as much. That was correct behaviour given a
    /// crooked input, and the crooked input is **fixed as of 2026-09-19**
    /// (<c>TestAFrontalHeadsJawMirrorsItself</c>), so the widths are compared now and this is the
    /// second test that would catch a regression in it.
    /// </remarks>
    [Fact]
    public void TestAFrontalHeadGetsTwoCheeks()
    {
        var geo = Geometry(Head(), new Dictionary<string, object?> { ["neckLength"] = 0f });
        SKRect far = Part(geo, "farCheek").Path.Bounds, near = Part(geo, "nearCheek").Path.Bounds;

        Assert.True(far.Width > 1f && near.Width > 1f, "a frontal head should carry both cheeks");

        // Both are strung from the ear line to the jaw angle line, which share their heights.
        Assert.Equal(far.Height, near.Height, 1);
        Assert.Equal(far.Width, near.Width, 1);

        // And the head as a whole is still square on, which the ears and the cheeks together decide.
        var outline = Box(geo, "silhouette");
        Assert.Equal(OriginX - outline.Left, outline.Right - OriginX, 1);
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
