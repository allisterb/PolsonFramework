namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;

using Polson.Drawing.Skia;

using Xunit;

/// <summary>
/// The muscle layer: <c>applyActionUnits</c>, seven units chosen for what the head can show.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written around the two properties the design actually rests on</b> — that units are additive
/// and therefore order-independent, and that each one moves what its name says and nothing else.
/// Everything a preset layer could later be built from is those two, so they are worth pinning
/// harder than any individual displacement.
/// </para>
/// <para>
/// The magnitudes are deliberately <i>not</i> asserted. They are the studio's, tuned by eye, and
/// neither source supplies a number — Loomis gives directions and a relaxed/contracted table, and
/// Ekman &amp; Friesen score presence rather than amount. A test asserting <c>0.035</c> would be
/// asserting that someone typed what they typed.
/// </para>
/// </remarks>
public class ActionUnitTests : TestsRuntime
{
    #region Composition Tests
    /// <summary>No units is the head unchanged, so the call is safe to leave in a pipeline.</summary>
    [Fact]
    public void TestNoUnitsLeavesTheHeadAlone()
    {
        var head = Head();

        AssertSameShape(head, Toolkit.ApplyActionUnits(head, new Dictionary<string, object?>()), "head");
    }

    /// <summary>A zero weight is the same as not naming the unit at all.</summary>
    [Fact]
    public void TestAZeroWeightChangesNothing()
    {
        var head = Head();

        AssertSameShape(head, Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU12"] = 0f }), "head");
    }

    /// <summary>
    /// Units fold in either order to the same head, which is what makes a small set cover a lot.
    /// </summary>
    /// <remarks>
    /// <b>The property the whole design rests on.</b> If units were applied as assignments rather
    /// than as displacements, the last one named would win and a caller would get a different face
    /// depending on how they happened to spell their dictionary — silently, because both results are
    /// plausible faces.
    /// </remarks>
    [Fact]
    public void TestUnitsComposeInAnyOrder()
    {
        var head = Head();

        var ab = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU4"] = 0.9f, ["AU15"] = 0.6f });
        var ba = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU15"] = 0.6f, ["AU4"] = 0.9f });

        AssertSameShape(ab, ba, "head");
    }

    /// <summary>Two units applied together equal the two applied in sequence.</summary>
    /// <remarks>
    /// Additivity stated as an identity rather than inferred from order-independence, because a
    /// scheme that averaged its units would also be order-independent and would be wrong.
    /// </remarks>
    [Fact]
    public void TestUnitsAreAdditive()
    {
        var head = Head();

        var together = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU1"] = 0.5f, ["AU12"] = 0.5f });
        var sequenced = Toolkit.ApplyActionUnits(
            Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU1"] = 0.5f }),
            new Dictionary<string, object?> { ["AU12"] = 0.5f });

        AssertSameShape(together, sequenced, "head");
    }

    /// <summary>The head handed in is never modified, so one neutral can serve a whole page.</summary>
    [Fact]
    public void TestTheSourceHeadIsLeftAlone()
    {
        var head = Head();
        var before = Head();

        Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU26"] = 1f });

        AssertSameShape(before, head, "head");
    }
    #endregion

    #region Direction Tests
    /// <summary>AU1 raises the brow and AU4 lowers it — antagonists, as the coding system has them.</summary>
    [Fact]
    public void TestTheBrowUnitsOpposeEachOther()
    {
        var head = Head();
        var raised = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU1"] = 1f });
        var lowered = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU4"] = 1f });

        Assert.True(Y(raised, "brow") < Y(head, "brow"), "AU1 must raise the brow");
        Assert.True(Y(lowered, "brow") > Y(head, "brow"), "AU4 must lower the brow");
    }

    /// <summary>AU5 opens the lids and AU7 narrows them.</summary>
    /// <remarks>
    /// Reachable only because <c>drawComicEye</c> now reads <c>height</c>. Before that these two
    /// units would have written a field nothing consumed, and every test here would still pass while
    /// the render never changed — which is the exact failure the aperture work was done to remove.
    /// </remarks>
    [Fact]
    public void TestTheLidUnitsOpposeEachOther()
    {
        var head = Head();
        var wide = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU5"] = 1f });
        var tight = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU7"] = 1f });

        Assert.True(Aperture(wide) > Aperture(head), "AU5 must open the lid");
        Assert.True(Aperture(tight) < Aperture(head), "AU7 must narrow the lid");
    }

    /// <summary>An aperture never goes negative, however hard the lids are tightened.</summary>
    /// <remarks>
    /// A negative aperture would invert the eyelid curve into a shape nothing in the face explains,
    /// and it renders rather than erroring.
    /// </remarks>
    [Fact]
    public void TestTheApertureFloorsAtShut()
    {
        var head = Toolkit.CreateLoomisHead(400f, 120f, 40f, 0f, 0f);

        var shut = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU7"] = 1f });

        Assert.True(Aperture(shut) >= 0f, "a tightened lid must not invert");
    }

    /// <summary>AU12 pulls the corners out AND up, which is Loomis's diagonal rather than a lift.</summary>
    /// <remarks>
    /// The horizontal component is the half most easily dropped, and dropping it is what turns a
    /// smile into a smirk — so it is asserted rather than assumed from the vertical being right.
    /// </remarks>
    [Fact]
    public void TestTheSmileMovesCornersOutwardAsWellAsUp()
    {
        var head = Head();
        var smiling = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU12"] = 1f });

        Assert.True(CornerSpread(smiling) > CornerSpread(head), "AU12 must widen the mouth");
        Assert.True(CornerY(smiling) < CornerY(head), "AU12 must lift the corners");
    }

    /// <summary>AU15 drops the corners without spreading them.</summary>
    [Fact]
    public void TestTheFrownDropsCornersWithoutSpreadingThem()
    {
        var head = Head();
        var frowning = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU15"] = 1f });

        Assert.Equal(CornerSpread(head), CornerSpread(frowning), 3);
        Assert.True(CornerY(frowning) > CornerY(head), "AU15 must drop the corners");
    }

    /// <summary>A jaw drop reaches the chin, so it changes the outline and not only the marks.</summary>
    /// <remarks>
    /// <c>createHeadGeometry</c> builds its jaw polygon through the chin stations, so an open mouth
    /// that left them alone would draw a dropped lip inside a closed face — which reads as a mistake
    /// in the drawing rather than in the expression.
    /// </remarks>
    [Fact]
    public void TestAJawDropMovesTheChinAndTheLowerLip()
    {
        var head = Head();
        var open = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU26"] = 1f });

        Assert.True(Y(open, "chin") > Y(head, "chin"), "AU26 must drop the chin");
        Assert.True(LowerLip(open) > LowerLip(head), "AU26 must open the lower lip");
    }
    #endregion

    #region Refusal Tests
    /// <summary>An unknown unit is refused and the known ones are listed.</summary>
    /// <remarks>
    /// Named rather than ignored for the reason <c>createParametricHead</c> refuses a misspelled
    /// parameter: a key that binds to nothing draws the neutral face, which looks like the unit
    /// having no effect rather than like a typo.
    /// </remarks>
    [Fact]
    public void TestAnUnknownUnitIsRefusedByName()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Toolkit.ApplyActionUnits(Head(), new Dictionary<string, object?> { ["AU99"] = 0.5f }));

        Assert.Contains("AU99", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AU12", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A unit the head has no landmark for is refused rather than silently absent.</summary>
    /// <remarks>
    /// AU2 is a real Action Unit and is deliberately not implemented, because the head carries one
    /// brow point and cannot distinguish an outer arch from an inner lift. Accepting it and doing
    /// nothing would be the worst of the three options.
    /// </remarks>
    [Fact]
    public void TestAUnitTheGeometryCannotShowIsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Toolkit.ApplyActionUnits(Head(), new Dictionary<string, object?> { ["AU2"] = 0.5f }));

        Assert.Contains("AU2", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A negative weight is refused, because the opposing action is its own unit.</summary>
    /// <remarks>
    /// The separation of antagonists into distinct units is the coding system's whole design, so a
    /// negative weight means the caller has the wrong unit rather than the wrong sign — and the
    /// message says which one they probably wanted.
    /// </remarks>
    [Fact]
    public void TestANegativeWeightIsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Toolkit.ApplyActionUnits(Head(), new Dictionary<string, object?> { ["AU4"] = -0.5f }));

        Assert.Contains("AU1", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A weight past 1 is clamped rather than refused, as the parameter layer clamps.</summary>
    [Fact]
    public void TestAWeightPastOneIsClamped()
    {
        var head = Head();

        AssertSameShape(
            Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU12"] = 1f }),
            Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU12"] = 40f }),
            "head");
    }

    /// <summary>Unit names are matched without regard to case, since agents write both.</summary>
    [Fact]
    public void TestUnitNamesAreCaseInsensitive()
    {
        var head = Head();

        AssertSameShape(
            Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU12"] = 0.7f }),
            Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["au12"] = 0.7f }),
            "head");
    }
    #endregion

    #region Interoperation Tests
    /// <summary>
    /// An expression survives the round trip through a blend, which is what makes it a template.
    /// </summary>
    /// <remarks>
    /// The join between this layer and <c>blendHead</c>: a head with units applied is an ordinary
    /// head, so it can be stored once and applied to a character at any weight. That is the economy
    /// the whole design exists for — a performance becomes data.
    /// </remarks>
    [Fact]
    public void TestAUnitHeadWorksAsABlendTemplate()
    {
        var neutral = Head();
        var smiling = Toolkit.ApplyActionUnits(neutral, new Dictionary<string, object?> { ["AU12"] = 1f });

        var half = Toolkit.BlendHead(neutral, neutral, smiling, 0.5f);
        var full = Toolkit.BlendHead(neutral, neutral, smiling, 1f);

        AssertSameShape(smiling, full, "head");
        Assert.True(CornerY(half) < CornerY(neutral), "half a smile still lifts the corners");
        Assert.True(CornerY(half) > CornerY(smiling), "half a smile lifts them less than a whole one");
    }

    /// <summary>A unit applies to a character head without disturbing who they are.</summary>
    /// <remarks>
    /// Identity and expression are separate layers, and this is the assertion that keeps them so: a
    /// character's jaw is theirs, and a smile must not quietly walk it back toward the canon.
    /// </remarks>
    [Fact]
    public void TestAUnitLeavesTheCharacterIntact()
    {
        var canon = Head();
        var mort = Toolkit.CreateParametricHead(canon, new Dictionary<string, object?>
        {
            ["jawShape"] = 0.8f,
            ["eyesDistance"] = -0.4f,
        });

        var smiling = Toolkit.ApplyActionUnits(mort, new Dictionary<string, object?> { ["AU12"] = 1f });

        // The jaw is identity and AU12 is performance, so the jaw must not have moved at all.
        Assert.Equal(JawWidth(mort), JawWidth(smiling), 3);
    }
    #endregion

    #region Helpers
    static ConstructiveDrawingToolkit Toolkit => new();

    static Dictionary<string, object?> Head() =>
        new ConstructiveDrawingToolkit().CreateLoomisHead(400f, 120f, 240f, 0f, 0f);

    static Dictionary<string, object?> G(Dictionary<string, object?> head, string name) =>
        (Dictionary<string, object?>)head[name]!;

    static float Y(Dictionary<string, object?> head, string key) =>
        Convert.ToSingle(((Dictionary<string, object?>)head[key]!)["y"]);

    static float Aperture(Dictionary<string, object?> head) =>
        Convert.ToSingle(G(head, "nearEye")["height"]);

    static float LowerLip(Dictionary<string, object?> head) =>
        Convert.ToSingle(G(head, "mouthGuides")["lowerLipY"]);

    static float CornerSpread(Dictionary<string, object?> head)
    {
        var mouth = G(head, "mouthGuides");
        var l = Convert.ToSingle(((Dictionary<string, object?>)mouth["leftCorner"]!)["x"]);
        var r = Convert.ToSingle(((Dictionary<string, object?>)mouth["rightCorner"]!)["x"]);
        return MathF.Abs(r - l);
    }

    static float CornerY(Dictionary<string, object?> head) =>
        Convert.ToSingle(((Dictionary<string, object?>)G(head, "mouthGuides")["leftCorner"]!)["y"]);

    static float JawWidth(Dictionary<string, object?> head)
    {
        var jaw = G(head, "jaw");
        var near = Convert.ToSingle(((Dictionary<string, object?>)jaw["nearStation"]!)["x"]);
        var far = Convert.ToSingle(((Dictionary<string, object?>)jaw["farStation"]!)["x"]);
        return MathF.Abs(near - far);
    }

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

            Assert.Equal(Convert.ToSingle(value), Convert.ToSingle(actual[key]), 3);
        }
    }
    #endregion
}
