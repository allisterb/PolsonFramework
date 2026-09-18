namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// The parameter layer over <c>createLoomisHead</c>, which exists so a character survives a page.
///
/// **Consistency across panels is the whole requirement**, and it reduces to one property: the
/// function is pure. Same head, same seven numbers, same landmarks — in panel 1 and in panel 40, in
/// this script and in the one that redraws it tomorrow. Everything below is that property, or one of
/// the two ways it could quietly stop holding: a parameter that does not move what it names, and a
/// call that mutates the head it was given.
/// </summary>
public class ParametricHeadTests : TestsRuntime
{
    const float H = 240f;

    static Dictionary<string, object?> Head(float yaw = 0f) =>
        new ConstructiveDrawingToolkit().CreateLoomisHead(400f, 120f, H, yaw, 0f);

    static Dictionary<string, object?> Group(Dictionary<string, object?> head, string name) =>
        (Dictionary<string, object?>)head[name]!;

    static float X(Dictionary<string, object?> head, string group, string key) =>
        Convert.ToSingle(((Dictionary<string, object?>)Group(head, group)[key]!)["x"]);

    static float Y(Dictionary<string, object?> head, string group, string key) =>
        Convert.ToSingle(((Dictionary<string, object?>)Group(head, group)[key]!)["y"]);

    /// <summary>
    /// Zero is the canon, exactly — the guard every other parameter depends on.
    /// </summary>
    /// <remarks>
    /// If an unparameterised head differed from a plain Loomis head by even a rounding step, every
    /// comic script already written would silently redraw the moment this shipped. Asserted against
    /// the *whole* structure rather than a few landmarks, because the failure would arrive in
    /// whichever key nobody thought to check.
    /// </remarks>
    [Fact]
    public void TestAHeadWithNoParametersIsTheCanonUnchanged()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();

        foreach (var parameters in new object?[] { null, new Dictionary<string, object?>() })
        {
            var same = toolkit.CreateParametricHead(Head(), parameters);
            AssertSameShape(canon, same, "root");
        }
    }

    /// <summary>Every parameter explicitly at zero is also the canon, not merely an absent one.</summary>
    [Fact]
    public void TestEveryParameterAtZeroIsAlsoTheCanon()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        // Every one, named. The list drifted to five while the surface grew to seven, and a test
        // called "every parameter" that omits two still passes - an absent parameter defaults to
        // zero, so the omission is invisible in exactly the case the test exists to cover. It is
        // therefore taken from the toolkit's own accepted list rather than retyped, since retyping
        // it is precisely what went wrong: a hand-kept copy of a list has to be remembered, and a
        // list whose whole job is to be exhaustive cannot afford to be.
        var parameters = new Dictionary<string, object?>();
        foreach (var name in AcceptedParameters()) parameters[name] = 0f;
        Assert.True(parameters.Count >= 8, $"only {parameters.Count} parameters discovered");

        var same = toolkit.CreateParametricHead(Head(), parameters);

        AssertSameShape(Head(), same, "root");
    }

    /// <summary>
    /// The same numbers give the same face. This is the panel-to-panel guarantee, stated directly.
    /// </summary>
    [Fact]
    public void TestTheSameParametersGiveTheSameFaceEveryTime()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var character = new Dictionary<string, object?>
        {
            ["eyesDistance"] = -0.4f,
            ["eyesSize"] = 0.3f,
            ["noseLength"] = -0.5f,
            ["jawShape"] = 0.8f,
            ["mouthWidth"] = 0.2f,
        };

        var panel1 = toolkit.CreateParametricHead(Head(), character);
        var panel40 = toolkit.CreateParametricHead(Head(), character);

        AssertSameShape(panel1, panel40, "root");
    }

    /// <summary>
    /// The head handed in is never touched.
    /// </summary>
    /// <remarks>
    /// A shallow copy would share the nested groups, so the second call with the same head would
    /// start from the first call's face — consistency destroyed by the very thing meant to provide
    /// it, and destroyed *silently*, since each panel would still draw something plausible.
    /// </remarks>
    [Fact]
    public void TestTheSourceHeadIsNotMutated()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var source = Head();
        var before = X(source, "mouthGuides", "leftCorner");

        toolkit.CreateParametricHead(source, new Dictionary<string, object?> { ["mouthWidth"] = 1f });

        Assert.Equal(before, X(source, "mouthGuides", "leftCorner"), 4);
    }

    [Fact]
    public void TestEyesDistanceMovesTheInnerCornersApart()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var wide = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["eyesDistance"] = 1f });
        var narrow = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["eyesDistance"] = -1f });

        var axis = Convert.ToSingle(((Dictionary<string, object?>)canon["chin"]!)["x"]);
        float Gap(Dictionary<string, object?> h) => MathF.Abs(X(h, "nearEye", "inner") - axis);

        Assert.True(Gap(wide) > Gap(canon), "+1 should widen the gap from the facial axis");
        Assert.True(Gap(narrow) < Gap(canon), "-1 should narrow it");
    }

    /// <summary>Size scales the eye about its own centre, so it does not undo the spacing.</summary>
    [Fact]
    public void TestEyesSizeDoesNotMoveTheEyeCentre()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var big = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["eyesSize"] = 1f });

        Assert.Equal(X(canon, "nearEye", "center"), X(big, "nearEye", "center"), 3);
        Assert.True(Convert.ToSingle(Group(big, "nearEye")["width"])
                  > Convert.ToSingle(Group(canon, "nearEye")["width"]), "the eye should be wider");
    }

    [Fact]
    public void TestNoseLengthMovesTheApexButNotTheEyeLine()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var longer = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["noseLength"] = 1f });

        Assert.True(Y(longer, "noseWedge", "apex") > Y(canon, "noseWedge", "apex"), "the apex should drop");
        Assert.Equal(Convert.ToSingle(canon["eyeLineY"]), Convert.ToSingle(longer["eyeLineY"]), 4);
    }

    /// <summary>`noseBase` and `noseWedge.apex` name the same feature and must not disagree.</summary>
    [Fact]
    public void TestNoseLengthKeepsTheWireframeAndTheInkAgreeing()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var longer = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["noseLength"] = 1f });

        var moved = Convert.ToSingle(((Dictionary<string, object?>)longer["noseBase"]!)["y"])
                  - Convert.ToSingle(((Dictionary<string, object?>)canon["noseBase"]!)["y"]);
        var apexMoved = Y(longer, "noseWedge", "apex") - Y(canon, "noseWedge", "apex");

        Assert.Equal(apexMoved, moved, 4);
    }

    /// <summary>
    /// A squared jaw widens at the <b>angle</b>, and barely at the station.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This test used to assert the opposite, and the drawing showed why that was wrong.</b> It
    /// was called <c>TestJawShapeWidensTheStations</c> and checked only that the station moved
    /// outward — which it did, at full share, and which produced a sharp lateral spur at every
    /// value above zero.
    /// </para>
    /// <para>
    /// Loomis hangs the jaw off the ball's halfway line station to station, so the station is the
    /// <i>joint</i> where the mandible meets the skull — and a skull does not widen when a
    /// character's jaw does. Measured on a 240px head, the station sits at dx 76.7 against a
    /// cranium reaching 76.6, exactly on it; the old full share took it to 93.1, seventeen pixels
    /// clear of the ball, with the jaw's straight top edge making a corner against nothing.
    /// </para>
    /// <para>
    /// What a square jaw widens is the gonial angle and the chin. Asserted as an <b>ordering</b>
    /// rather than as three magnitudes, because the shares are tuned by eye and the ordering is the
    /// claim about anatomy.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestJawShapeWidensTheAngleMoreThanTheStation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var squared = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["jawShape"] = 1f });

        var axis = Convert.ToSingle(((Dictionary<string, object?>)canon["chin"]!)["x"]);
        float Spread(Dictionary<string, object?> head, string key) =>
            MathF.Abs(X(head, "jaw", key) - axis);

        var station = Spread(squared, "nearStation") - Spread(canon, "nearStation");
        var angle = Spread(squared, "nearAngle") - Spread(canon, "nearAngle");
        var chinCorner = Spread(squared, "chinNear") - Spread(canon, "chinNear");

        Assert.True(station > 0f, "the jaw should still widen everywhere");
        Assert.True(angle > chinCorner, "the gonial angle is what a square jaw widens most");
        Assert.True(chinCorner > station, "the station is a joint and should move least of the three");
    }

    /// <summary>
    /// A squared jaw's station stays on the cranium, so the outline has no lateral spur.
    /// </summary>
    /// <remarks>
    /// The geometric consequence of the test above, asserted where it actually shows: the jaw's top
    /// corner must not stand clear of the ball it hangs from. Measured against the cranium's own
    /// half-width at the station's height, which is what <c>createHeadGeometry</c> holds it to.
    /// </remarks>
    [Fact]
    public void TestASquaredJawStationStaysNearTheCranium()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var squared = toolkit.CreateParametricHead(canon, new Dictionary<string, object?> { ["jawShape"] = 1f });

        var crownX = Convert.ToSingle(((Dictionary<string, object?>)canon["crown"]!)["x"]);
        var reach = MathF.Abs(X(canon, "jaw", "nearStation") - crownX);   // the ball, at that height
        var overhang = MathF.Abs(X(squared, "jaw", "nearStation") - crownX) - reach;

        Assert.True(overhang < reach * 0.06f,
            $"a squared station overhangs the cranium by {overhang:0.0}px of a {reach:0.0}px reach");
    }

    /// <summary>
    /// <c>chinShape</c> is a second axis, not more of the first.
    /// </summary>
    /// <remarks>
    /// <b>The property that justifies the parameter existing.</b> <c>jawShape</c> spreads all six
    /// stations together, so a broad jaw ending in a point — or a narrow one ending square — was
    /// unreachable however the one dial was set. This asserts the two compose: the chin moves and the
    /// jaw's own width, measured at the angle, does not.
    /// </remarks>
    [Fact]
    public void TestChinShapeMovesTheChinAndLeavesTheJawWidthAlone()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var broad = new Dictionary<string, object?> { ["jawShape"] = 0.9f };
        var broadPointed = new Dictionary<string, object?> { ["jawShape"] = 0.9f, ["chinShape"] = -1f };

        var a = toolkit.CreateParametricHead(Head(), broad);
        var b = toolkit.CreateParametricHead(Head(), broadPointed);

        Assert.Equal(Spread(a, "nearAngle"), Spread(b, "nearAngle"), 3);
        Assert.Equal(Spread(a, "nearStation"), Spread(b, "nearStation"), 3);
        Assert.True(Spread(b, "chinNear") < Spread(a, "chinNear"), "a pointed chin narrows at the corners");
    }

    /// <summary>Negative points the chin and drops it; positive squares it and lifts it.</summary>
    /// <remarks>
    /// The vertical move is a quarter of the horizontal one and is anatomical rather than decorative
    /// — a chin coming to a point is longer than one ending square. Asserted as a direction, because
    /// the magnitudes are tuned by eye.
    /// </remarks>
    [Fact]
    public void TestAPointedChinIsNarrowerAndLowerThanASquareOne()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var pointed = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["chinShape"] = -1f });
        var square = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["chinShape"] = 1f });

        Assert.True(Spread(pointed, "chinNear") < Spread(square, "chinNear"), "pointed is narrower");
        Assert.True(ChinY(pointed) > ChinY(square), "pointed hangs lower");
    }

    /// <summary>
    /// <c>chinLength</c> takes the jaw angle down with the chin, which is what makes it a length.
    /// </summary>
    /// <remarks>
    /// <b>The property the parameter exists for, and the one a chin-only move fails.</b> Dropping the
    /// chin alone stretches the last inch of the outline into a spike and leaves the jaw ending where
    /// it did; a mandible that lengthens lengthens in both segments. The angle's half share is
    /// asserted as a proportion rather than a pixel count, since the magnitude is tuned by eye and
    /// the ratio is the claim.
    /// </remarks>
    [Fact]
    public void TestChinLengthTakesTheJawAngleDownWithTheChin()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var lengthened = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["chinLength"] = 1f });

        var chinDrop = ChinY(lengthened) - ChinY(canon);
        var angleDrop = Y(lengthened, "jaw", "angle") - Y(canon, "jaw", "angle");

        Assert.True(chinDrop > 0f, "a positive chinLength must lengthen the lower face");
        Assert.True(angleDrop > 0f, "the angle must follow the chin down, or the jaw ends where it did");
        Assert.True(angleDrop < chinDrop, "and must follow by less, or the whole jaw merely translates");
        Assert.Equal(0.5f, angleDrop / chinDrop, 2);
        Assert.Equal(angleDrop, Y(lengthened, "jaw", "nearAngle") - Y(canon, "jaw", "nearAngle"), 3);
    }

    /// <summary>The stations stay put: they are the joint against a skull that did not get longer.</summary>
    /// <remarks>
    /// The same reasoning that took the stations out of <c>jawShape</c> after they drew a lateral
    /// spur off the cranium. Worth its own test because it is the one part of the jaw a naive
    /// implementation moves, and moving it renders perfectly — as a head whose skull has grown.
    /// </remarks>
    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void TestChinLengthLeavesTheJawStationsOnTheSkull(float amount)
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var head = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["chinLength"] = amount });

        foreach (var station in new[] { "nearStation", "farStation", "ear" })
        {
            Assert.Equal(Y(canon, "jaw", station), Y(head, "jaw", station), 3);
            Assert.Equal(X(canon, "jaw", station), X(head, "jaw", station), 3);
        }
    }

    /// <summary>
    /// Length and shape are separate axes: one moves the chin down, the other moves its corners out.
    /// </summary>
    /// <remarks>
    /// <b>The test that earns a third chin parameter rather than a stronger second one.</b> A long
    /// chin ending square and a short one coming to a point are both reachable, and neither is a
    /// setting of the other. Asserted on the corner <i>spread</i>, which <c>chinLength</c> must leave
    /// exactly alone because it writes <c>y</c> only.
    /// </remarks>
    [Fact]
    public void TestChinLengthAndChinShapeAreIndependent()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var shaped = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["chinShape"] = 1f });
        var both = toolkit.CreateParametricHead(Head(),
            new Dictionary<string, object?> { ["chinShape"] = 1f, ["chinLength"] = 1f });

        Assert.Equal(Spread(shaped, "chinNear"), Spread(both, "chinNear"), 3);
        Assert.Equal(Spread(shaped, "chinFar"), Spread(both, "chinFar"), 3);
        Assert.True(ChinY(both) > ChinY(shaped), "length must still reach the chin through a shaped one");

        // And the other way round, which is the half that would catch an ordering dependency.
        var lengthened = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["chinLength"] = 1f });
        Assert.True(Spread(both, "chinNear") > Spread(lengthened, "chinNear"), "shape must still reach a long chin");
    }

    /// <summary>
    /// Both records of the chin move together, because the construction carries it twice.
    /// </summary>
    /// <remarks>
    /// <c>head.chin</c> is what <c>createHeadGeometry</c> draws from and <c>jaw.chin</c> is read by
    /// nothing — so moving one and not the other would leave a duplicate quietly disagreeing with the
    /// drawing, and surface later as a blend doing something inexplicable.
    /// </remarks>
    [Theory]
    [InlineData("chinShape", -1f)]
    [InlineData("chinShape", 1f)]
    [InlineData("chinLength", -1f)]
    [InlineData("chinLength", 1f)]
    public void TestBothRecordsOfTheChinStayInAgreement(string parameter, float value)
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { [parameter] = value });

        Assert.Equal(ChinY(head), Y(head, "jaw", "chin"), 3);
        Assert.Equal(Convert.ToSingle(((Dictionary<string, object?>)head["chin"]!)["x"]), X(head, "jaw", "chin"), 3);
    }

    [Fact]
    public void TestMouthWidthMovesBothCornersAndKeepsTheLipHeights()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var wide = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["mouthWidth"] = 1f });

        var canonSpan = MathF.Abs(X(canon, "mouthGuides", "rightCorner") - X(canon, "mouthGuides", "leftCorner"));
        var wideSpan = MathF.Abs(X(wide, "mouthGuides", "rightCorner") - X(wide, "mouthGuides", "leftCorner"));

        Assert.True(wideSpan > canonSpan, "the mouth should be wider");
        Assert.Equal(Convert.ToSingle(Group(canon, "mouthGuides")["upperLipY"]),
                     Convert.ToSingle(Group(wide, "mouthGuides")["upperLipY"]), 4);
    }

    /// <summary>Out of range is clamped rather than refused: ±1 is the edge of the range, not of the API.</summary>
    [Fact]
    public void TestValuesBeyondTheRangeAreClamped()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var one = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["mouthWidth"] = 1f });
        var ten = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["mouthWidth"] = 10f });

        Assert.Equal(X(one, "mouthGuides", "leftCorner"), X(ten, "mouthGuides", "leftCorner"), 4);
    }

    /// <summary>
    /// A misspelled parameter is refused by name rather than silently drawing the canon.
    /// </summary>
    /// <remarks>
    /// `{ eyeDistance: 1 }` for `eyesDistance` would otherwise bind to nothing, and the agent would
    /// see a parameter that "has no effect" — the class of failure this project treats as worst.
    /// </remarks>
    [Fact]
    public void TestAMisspelledParameterIsRefusedByName()
    {
        var toolkit = new ConstructiveDrawingToolkit();

        var error = Assert.Throws<ArgumentException>(() =>
            toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["eyeDistance"] = 1f }));

        Assert.Contains("eyeDistance", error.Message);
        Assert.Contains("eyesDistance", error.Message);
    }

    /// <summary>A turned head still parameterises — the near and far eyes move the right ways.</summary>
    /// <remarks>
    /// Displacements are applied after projection, which is the documented limit of this layer. At a
    /// modest yaw the directions must still be correct; past roughly 40 degrees they drift, which is
    /// why the remark on <c>CreateParametricHead</c> says so rather than leaving it to be discovered.
    /// </remarks>
    [Fact]
    public void TestATurnedHeadStillWidensOutward()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head(25f);
        var wide = toolkit.CreateParametricHead(Head(25f), new Dictionary<string, object?> { ["eyesDistance"] = 1f });

        var axis = Convert.ToSingle(((Dictionary<string, object?>)canon["chin"]!)["x"]);
        foreach (var eye in new[] { "nearEye", "farEye" })
        {
            Assert.True(MathF.Abs(X(wide, eye, "inner") - axis) > MathF.Abs(X(canon, eye, "inner") - axis),
                        $"{eye} inner corner should move away from the axis");
        }
    }

    /// <summary>
    /// Every parameter <c>createParametricHead</c> accepts, read off its own refusal message.
    /// </summary>
    /// <remarks>
    /// Reflection over the private list would work and would test less: this reads the <b>documented
    /// surface</b> — the sentence a caller who misspells one actually gets — so a parameter added to
    /// the field and left out of the message would fail here rather than pass.
    /// </remarks>
    static string[] AcceptedParameters()
    {
        var error = Assert.Throws<ArgumentException>(() => new ConstructiveDrawingToolkit()
            .CreateParametricHead(Head(), new Dictionary<string, object?> { ["notAParameter"] = 1f }));

        var listed = error.Message.Split("Accepted:")[1].TrimEnd('.', ' ', '\n', '\r');
        return listed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>How far a jaw station sits from the head's own vertical axis.</summary>
    static float Spread(Dictionary<string, object?> head, string key) =>
        MathF.Abs(X(head, "jaw", key) - Convert.ToSingle(((Dictionary<string, object?>)head["chin"]!)["x"]));

    static float ChinY(Dictionary<string, object?> head) =>
        Convert.ToSingle(((Dictionary<string, object?>)head["chin"]!)["y"]);

    /// <summary>Compares two head dictionaries all the way down, naming the first key that differs.</summary>
    static void AssertSameShape(Dictionary<string, object?> expected, Dictionary<string, object?> actual, string path)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, value) in expected)
        {
            Assert.True(actual.ContainsKey(key), $"{path}.{key} is missing");
            if (value is Dictionary<string, object?> nested)
            {
                AssertSameShape(nested, (Dictionary<string, object?>)actual[key]!, $"{path}.{key}");
                continue;
            }

            Assert.Equal(Convert.ToSingle(value), Convert.ToSingle(actual[key]), 4);
        }
    }
}
