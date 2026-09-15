namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// The parameter layer over <c>createLoomisHead</c>, which exists so a character survives a page.
///
/// **Consistency across panels is the whole requirement**, and it reduces to one property: the
/// function is pure. Same head, same five numbers, same landmarks — in panel 1 and in panel 40, in
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
        var same = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?>
        {
            ["eyesDistance"] = 0f,
            ["eyesSize"] = 0f,
            ["noseLength"] = 0f,
            ["jawShape"] = 0f,
            ["mouthWidth"] = 0f,
        });

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

    [Fact]
    public void TestJawShapeWidensTheStations()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canon = Head();
        var squared = toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["jawShape"] = 1f });

        var axis = Convert.ToSingle(((Dictionary<string, object?>)canon["chin"]!)["x"]);
        Assert.True(MathF.Abs(X(squared, "jaw", "nearStation") - axis)
                  > MathF.Abs(X(canon, "jaw", "nearStation") - axis));
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
