namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;

using Polson.Drawing.Skia;

using Xunit;

/// <summary>
/// The six named expressions, now weight tuples over <c>applyActionUnits</c> rather than displacements.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written around what the reimplementation was for.</b> The presets used to move one or two
/// landmarks directly, and several did not do what their own manual entry described — <c>sadness</c>
/// was documented as lifting the inner brow and dropping the mouth corners, and moved only the mouth.
/// A live run asked for it <i>"at 0.22 for the inner-brow lift only"</i>, wrote a careful note about
/// the intensity at which that lift would become a grimace, and received roughly two and a half
/// pixels of a movement it had not asked for.
/// </para>
/// <para>
/// So the first test here is that <c>sadness</c> moves the brow. It is a one-line assertion about a
/// failure that survived review, a manual entry and a live run because nothing ever checked that the
/// name and the displacement agreed.
/// </para>
/// </remarks>
public class ExpressionPresetTests : TestsRuntime
{
    #region Behaviour Tests
    /// <summary>Sadness lifts the inner brow, which is the defect this reimplementation exists for.</summary>
    /// <remarks>
    /// Read at the inner station rather than at <c>head.brow</c>: that landmark is the ball's equator
    /// and no expression moves it any more. The lift is now a <i>net</i> one — AU1 raises the inner
    /// end and AU4 lowers all three — which is what makes the brow oblique rather than merely high.
    /// </remarks>
    [Fact]
    public void TestSadnessLiftsTheBrow()
    {
        var head = Head();

        var sad = Toolkit.ApplyFacialExpression(head, "sadness", 1f);

        Assert.True(BrowY(sad, "inner") < BrowY(head, "inner"), "sadness must raise the inner brow");
    }

    /// <summary>
    /// And the tail goes the other way, which is the oblique brow sadness is named for.
    /// </summary>
    /// <remarks>
    /// <b>The expression this set could not draw until the brow carried stations.</b> The inner end
    /// rising while the tail falls is the whole shape of a sad brow; with one landmark AU1 and AU4
    /// were opposing verticals on the same point and cancelled, so the tuple had to omit AU4 and the
    /// result was a face with no brow movement at all.
    /// </remarks>
    [Fact]
    public void TestSadnessSlantsTheBrowRatherThanRaisingAllOfIt()
    {
        var head = Head();

        var sad = Toolkit.ApplyFacialExpression(head, "sadness", 1f);

        Assert.True(BrowY(sad, "inner") < BrowY(head, "inner"), "the inner end must rise");
        Assert.True(BrowY(sad, "outer") > BrowY(head, "outer"), "and the tail must fall");
    }

    /// <summary>And still drops the mouth corners, which is the half that did work.</summary>
    [Fact]
    public void TestSadnessStillDropsTheMouthCorners()
    {
        var head = Head();

        var sad = Toolkit.ApplyFacialExpression(head, "sadness", 1f);

        Assert.True(CornerY(sad) > CornerY(head), "sadness must drop the mouth corners");
    }

    /// <summary>Every preset moves something at full intensity.</summary>
    /// <remarks>
    /// The cheapest guard against a tuple naming units that cancel. <b>It was a live hazard until
    /// 2026-09-18</b>, when AU1 and AU4 acted on the same single <c>brow</c> point in opposite
    /// directions and a tuple holding both drew nothing — which is why <c>sadness</c> had to omit
    /// AU4. They now act on separate brow stations, so the pair composes into the oblique brow
    /// instead; the guard stays because the next cancelling pair will not announce itself either.
    /// </remarks>
    [Theory]
    [InlineData("joy")]
    [InlineData("anger")]
    [InlineData("fear")]
    [InlineData("sadness")]
    [InlineData("surprise")]
    [InlineData("disgust")]
    public void TestEveryPresetChangesTheFace(string name)
    {
        var head = Head();

        var expressed = Toolkit.ApplyFacialExpression(head, name, 1f);

        Assert.True(TotalShift(head, expressed) > 0.5f, $"'{name}' moved nothing");
    }

    /// <summary>Each preset is distinguishable from the others, so the six are six faces.</summary>
    /// <remarks>
    /// <b>Fear and surprise used to be the pair at risk</b>, because what separates them in life is
    /// AU4 knitting an already-raised brow and the head carried one brow landmark — so they were kept
    /// apart by weighting alone, and this threshold was set deliberately low to assert only that they
    /// were not the same face. They are now separated by the mechanism itself; see
    /// <see cref="TestFearKnitsTheBrowAndSurpriseDoesNot"/>, which asserts the shape rather than the
    /// difference. This one stays as the cheap all-pairs guard it always was.
    /// </remarks>
    [Fact]
    public void TestThePresetsAreDistinguishableFromEachOther()
    {
        var head = Head();
        var names = new[] { "joy", "anger", "fear", "sadness", "surprise", "disgust" };

        for (var i = 0; i < names.Length; i++)
        {
            for (var j = i + 1; j < names.Length; j++)
            {
                var a = Toolkit.ApplyFacialExpression(head, names[i], 1f);
                var b = Toolkit.ApplyFacialExpression(head, names[j], 1f);
                Assert.True(TotalShift(a, b) > 0.5f, $"'{names[i]}' and '{names[j]}' draw the same face");
            }
        }
    }

    /// <summary>Intensity scales the whole tuple, so half an expression is half of each unit.</summary>
    [Fact]
    public void TestIntensityScalesTheWholeTuple()
    {
        var full = Toolkit.ExpressionUnits("anger", 1f);
        var half = Toolkit.ExpressionUnits("anger", 0.5f);

        Assert.Equal(full.Count, half.Count);
        foreach (var (unit, weight) in full)
            Assert.Equal(Convert.ToSingle(weight) * 0.5f, Convert.ToSingle(half[unit]), 4);
    }

    /// <summary>Zero intensity leaves the face alone, so the dial has a neutral position.</summary>
    [Fact]
    public void TestZeroIntensityLeavesTheFaceAlone()
    {
        var head = Head();

        Assert.Equal(0f, TotalShift(head, Toolkit.ApplyFacialExpression(head, "joy", 0f)), 3);
    }

    /// <summary>The head handed in is not modified, deeply — the old shallow clone shared its groups.</summary>
    /// <remarks>
    /// The previous implementation copied the top level only, so the returned head's <c>jaw</c> was
    /// the caller's <c>jaw</c>. Nothing had noticed, because the presets happened never to write to a
    /// group they had not already replaced.
    /// </remarks>
    [Fact]
    public void TestTheSourceHeadIsDeeplyUntouched()
    {
        var head = Head();
        var before = Head();

        var sad = Toolkit.ApplyFacialExpression(head, "sadness", 1f);
        ((Dictionary<string, object?>)sad["jaw"]!)["chin"] = ToPoint(0f, 0f);

        AssertSameShape(before, head, "head");
    }
    #endregion

    #region Tuple Tests
    /// <summary>The tuple is what actually gets applied, so reading it tells you the truth.</summary>
    /// <remarks>
    /// The whole point of exposing it: a caller who disagrees with a preset can read it, change one
    /// unit and pass the result on. If the two routes could diverge, the tuple would be documentation
    /// rather than the mechanism.
    /// </remarks>
    [Fact]
    public void TestApplyingTheTupleEqualsApplyingTheName()
    {
        var head = Head();

        var byName = Toolkit.ApplyFacialExpression(head, "anger", 0.8f);
        var byTuple = Toolkit.ApplyActionUnits(head, Toolkit.ExpressionUnits("anger", 0.8f));

        byName.Remove("expression");       // the marker is the wrapper's, not the units'
        AssertSameShape(byTuple, byName, "head");
    }

    /// <summary>Sadness names AU1 and AU4 together, which is the canonical oblique sad brow.</summary>
    /// <remarks>
    /// <b>This test asserted the opposite until 2026-09-18, and said so on purpose.</b> Its remark
    /// read: <i>"a deliberate departure from the canonical oblique sad brow, made for a geometric
    /// reason rather than an expressive one. If the head ever gains inner and outer brow stations
    /// this test should fail, and the failure is the reminder to revisit the tuple."</i> The head
    /// gained them, the test failed, and this is the revisit — which is the value of writing a
    /// compromise down as an assertion rather than as a comment.
    /// </remarks>
    [Fact]
    public void TestSadnessNamesTheBrowKnitThatMakesItOblique()
    {
        var units = Toolkit.ExpressionUnits("sadness");

        Assert.True(units.ContainsKey("AU1"), "sadness must lift the inner brow");
        Assert.True(units.ContainsKey("AU4"), "and knit it, which no longer cancels the lift");
        Assert.True(Convert.ToSingle(units["AU4"]) < Convert.ToSingle(units["AU1"]),
            "the knit must not overpower the lift");
    }

    /// <summary>
    /// Fear and surprise are two expressions rather than one at two strengths, and AU4 is why.
    /// </summary>
    /// <remarks>
    /// <b>The separation the brow stations were added for.</b> Fear knits the brow while raising it —
    /// frontalis lifting against corrugator, which is what gives fear its strained flat brow — where
    /// surprise arches cleanly with no corrugator at all. Asserted on the tuples <i>and</i> on the
    /// resulting geometry, because two tuples that differ on paper and draw the same face would be
    /// the distinction existing in the data and nowhere a reader can see it.
    /// </remarks>
    [Fact]
    public void TestFearKnitsTheBrowAndSurpriseDoesNot()
    {
        var fear = Toolkit.ExpressionUnits("fear");
        var surprise = Toolkit.ExpressionUnits("surprise");

        Assert.True(fear.ContainsKey("AU4"), "fear must carry the corrugator");
        Assert.False(surprise.ContainsKey("AU4"), "surprise must not");
        Assert.True(Convert.ToSingle(surprise["AU2"]) > Convert.ToSingle(fear["AU2"]),
            "surprise must arch harder than fear");

        var head = Head();
        var afraid = Toolkit.ApplyFacialExpression(head, "fear", 1f);
        var startled = Toolkit.ApplyFacialExpression(head, "surprise", 1f);

        Assert.True(BrowY(startled, "outer") < BrowY(afraid, "outer"),
            "surprise must carry the tail higher than fear, or the two read as one expression");

        var axis = Convert.ToSingle(((Dictionary<string, object?>)head["brow"]!)["x"]);
        Assert.True(MathF.Abs(BrowX(afraid, "inner") - axis) < MathF.Abs(BrowX(startled, "inner") - axis),
            "fear must knit the inner ends in and surprise must leave them out");
    }

    /// <summary>Aliases resolve to the same tuple as their canonical name.</summary>
    [Theory]
    [InlineData("happy", "joy")]
    [InlineData("angry", "anger")]
    [InlineData("scared", "fear")]
    [InlineData("sad", "sadness")]
    public void TestAliasesResolveToTheirCanonicalTuple(string alias, string canonical)
    {
        var a = Toolkit.ExpressionUnits(alias);
        var b = Toolkit.ExpressionUnits(canonical);

        Assert.Equal(b.Count, a.Count);
        foreach (var (unit, weight) in b) Assert.Equal(Convert.ToSingle(weight), Convert.ToSingle(a[unit]), 4);
    }

    /// <summary>Every unit a tuple names is one the toolkit can actually apply.</summary>
    /// <remarks>
    /// The join between the two layers, and the failure it prevents is silent: a tuple naming an
    /// absent unit would throw from inside a preset, which reads as the preset being broken rather
    /// than as the tuple being out of date with the unit set.
    /// </remarks>
    [Theory]
    [InlineData("joy")]
    [InlineData("anger")]
    [InlineData("fear")]
    [InlineData("sadness")]
    [InlineData("surprise")]
    [InlineData("disgust")]
    public void TestEveryTupleNamesOnlyImplementedUnits(string name)
    {
        var head = Head();

        Toolkit.ApplyActionUnits(head, Toolkit.ExpressionUnits(name));
    }
    #endregion

    #region Refusal Tests
    /// <summary>An unknown expression is refused rather than returning the head unchanged.</summary>
    /// <remarks>
    /// The previous implementation fell through its switch and returned a copy, so a misspelled
    /// expression drew the neutral face and looked like the intensity being too low.
    /// </remarks>
    [Fact]
    public void TestAnUnknownExpressionIsRefusedByName()
    {
        var ex = Assert.Throws<ArgumentException>(() => Toolkit.ApplyFacialExpression(Head(), "smug", 1f));

        Assert.Contains("smug", ex.Message, StringComparison.Ordinal);
        Assert.Contains("applyActionUnits", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A negative intensity is refused, with the reason rather than a clamp.</summary>
    [Fact]
    public void TestANegativeIntensityIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Toolkit.ApplyFacialExpression(Head(), "joy", -1f));
    }
    #endregion

    #region Helpers
    static ConstructiveDrawingToolkit Toolkit => new();

    static Dictionary<string, object?> Head() =>
        new ConstructiveDrawingToolkit().CreateLoomisHead(400f, 120f, 240f, 0f, 0f);

    /// <summary>A named station on the near brow — the drawn eyebrow, not the ball's equator.</summary>
    static float BrowY(Dictionary<string, object?> head, string station) =>
        Convert.ToSingle(((Dictionary<string, object?>)
            ((Dictionary<string, object?>)head["nearBrow"]!)[station]!)["y"]);

    static float BrowX(Dictionary<string, object?> head, string station) =>
        Convert.ToSingle(((Dictionary<string, object?>)
            ((Dictionary<string, object?>)head["nearBrow"]!)[station]!)["x"]);

    static Dictionary<string, object?> ToPoint(float x, float y) =>
        new() { ["x"] = x, ["y"] = y };

    static float Y(Dictionary<string, object?> head, string key) =>
        Convert.ToSingle(((Dictionary<string, object?>)head[key]!)["y"]);

    static float CornerY(Dictionary<string, object?> head) =>
        Convert.ToSingle(((Dictionary<string, object?>)
            ((Dictionary<string, object?>)head["mouthGuides"]!)["leftCorner"]!)["y"]);

    /// <summary>How far every landmark moved between two heads, summed.</summary>
    static float TotalShift(Dictionary<string, object?> a, Dictionary<string, object?> b)
    {
        var total = 0f;
        foreach (var (key, value) in a)
        {
            if (key is "unit" or "origin") continue;
            if (value is Dictionary<string, object?> nested)
            {
                if (b.TryGetValue(key, out var other) && other is Dictionary<string, object?> bn)
                    total += nested.ContainsKey("x") ? PointShift(nested, bn) : TotalShift(nested, bn);
                continue;
            }

            if (value is null || !b.TryGetValue(key, out var bv) || bv is null) continue;
            if (value is string) continue;
            total += MathF.Abs(Convert.ToSingle(value) - Convert.ToSingle(bv));
        }

        return total;
    }

    static float PointShift(Dictionary<string, object?> a, Dictionary<string, object?> b) =>
        MathF.Abs(Convert.ToSingle(a["x"]) - Convert.ToSingle(b["x"]))
        + MathF.Abs(Convert.ToSingle(a["y"]) - Convert.ToSingle(b["y"]));

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

            if (value is string s) { Assert.Equal(s, actual[key]); continue; }
            Assert.Equal(Convert.ToSingle(value), Convert.ToSingle(actual[key]), 3);
        }
    }
    #endregion
}
