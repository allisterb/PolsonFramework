namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;

using Polson.Drawing.Skia;

using Xunit;

/// <summary>
/// <c>blendHead</c> and <c>exaggerateHead</c> — the one operation identity, caricature and
/// expression all reduce to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as identities rather than as arithmetic.</b> A test that recomputes
/// <c>base + amount * (to - from)</c> and compares it to the implementation asserts that two copies
/// of the same expression agree, which they will even when the expression is wrong. The properties
/// below are the ones a caller actually depends on and that a plausible mistake would break:
/// <c>amount = 0</c> changes nothing, <c>amount = 1</c> lands exactly on the other head, blending a
/// head with itself is a no-op at any amount, and the inputs are never modified.
/// </para>
/// <para>
/// The normalisation case is the one worth reading. It is the step a secondary summary of Brennan's
/// method omits, and omitting it produces a blend that runs, looks plausible, and amplifies
/// differences of <i>size</i> rather than of <i>shape</i>.
/// </para>
/// </remarks>
public class HeadBlendTests : TestsRuntime
{
    #region Identity Tests
    /// <summary>Zero applies nothing, which is what makes the call safe to add to a pipeline.</summary>
    [Fact]
    public void TestBlendingByZeroReturnsTheBaseUnchanged()
    {
        var a = Head();
        var b = Character();

        AssertSameShape(a, Toolkit.BlendHead(a, a, b, 0f), "head");
    }

    /// <summary>One lands exactly on the target, so a blend is a real interpolation.</summary>
    /// <remarks>
    /// The property most easily lost to a sign error or a mis-ordered subtraction, and the one that
    /// makes `blendHead(a, a, b, t)` mean what a reader expects at both ends of its range.
    /// </remarks>
    [Fact]
    public void TestBlendingByOneLandsOnTheTarget()
    {
        var a = Head();
        var b = Character();

        AssertSameShape(b, Toolkit.BlendHead(a, a, b, 1f), "head");
    }

    /// <summary>A head blended toward itself is unchanged at any amount, including absurd ones.</summary>
    /// <remarks>
    /// <c>to - from</c> is zero here whatever the amount, so this fails only if normalisation is
    /// asymmetric — if the route out to head-relative terms and back is not its own inverse. That is
    /// a real hazard and is invisible in every other case, because a small drift looks like a blend.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-3f)]
    [InlineData(40f)]
    public void TestBlendingAHeadTowardItselfChangesNothing(float amount)
    {
        var a = Character();

        AssertSameShape(a, Toolkit.BlendHead(a, a, a, amount), "head");
    }

    /// <summary>Halfway between two faces is halfway, which is Brennan's "offspring" region.</summary>
    [Fact]
    public void TestAHalfBlendSitsBetweenTheTwoHeads()
    {
        var a = Head();
        var b = Character();
        var mid = Toolkit.BlendHead(a, a, b, 0.5f);

        // On x, because `mouthWidth` moves the corners outward and leaves their height alone — the
        // first draft of this test asserted on y and failed by finding two identical numbers.
        var xa = X(a, "mouthGuides", "leftCorner");
        var xb = X(b, "mouthGuides", "leftCorner");
        var xm = X(mid, "mouthGuides", "leftCorner");

        Assert.NotEqual(xa, xb, 3);
        Assert.Equal((xa + xb) * 0.5f, xm, 3);
    }

    /// <summary>None of the three arguments is modified, so a canon head can serve a whole page.</summary>
    [Fact]
    public void TestBlendingLeavesEveryArgumentAlone()
    {
        var a = Head();
        var b = Character();
        var beforeA = Head();
        var beforeB = Character();

        Toolkit.BlendHead(a, a, b, 2.5f);

        AssertSameShape(beforeA, a, "base");
        AssertSameShape(beforeB, b, "to");
    }
    #endregion

    #region Normalisation Tests
    /// <summary>
    /// The same difference applies the same way to a head of a different size.
    /// </summary>
    /// <remarks>
    /// <b>The step a summary of this method drops, and the reason this test exists.</b> Without
    /// normalisation the blend measures in pixels, so a difference taken between 240px heads and
    /// applied to a 480px one arrives at half the intended strength — and nothing errors, because
    /// every number is finite and the face merely expresses less than it was told to. Measured in
    /// head units, the displacement is identical at both sizes.
    /// </remarks>
    [Fact]
    public void TestADifferenceAppliesEquallyAtAnyHeadSize()
    {
        var small = Toolkit.CreateLoomisHead(400f, 120f, 240f, 0f, 0f);
        var large = Toolkit.CreateLoomisHead(90f, 700f, 480f, 0f, 0f);

        var smallChar = Toolkit.CreateParametricHead(small, Mort);
        var largeChar = Toolkit.CreateParametricHead(large, Mort);

        // The same character difference, applied to its own head at each size.
        var blendedSmall = Toolkit.BlendHead(small, small, smallChar, 1f);
        var blendedLarge = Toolkit.BlendHead(large, large, largeChar, 1f);

        Assert.Equal(Offset(small, blendedSmall) / 240f, Offset(large, blendedLarge) / 480f, 3);
    }

    /// <summary>A template built elsewhere applies to a head somewhere else on the page.</summary>
    /// <remarks>
    /// The other half of normalisation: translation. An expression template is built once at a
    /// canonical origin and applied to whatever head needs it, so a blend that forgot to subtract
    /// the origin would drag every face toward the template's own position on the canvas.
    /// </remarks>
    [Fact]
    public void TestATemplateAppliesToAHeadAtAnotherPosition()
    {
        var atOrigin = Toolkit.CreateLoomisHead(0f, 0f, 240f, 0f, 0f);
        var template = Toolkit.CreateParametricHead(atOrigin, Mort);
        var elsewhere = Toolkit.CreateLoomisHead(900f, 610f, 240f, 0f, 0f);

        var applied = Toolkit.BlendHead(elsewhere, atOrigin, template, 1f);

        Assert.Equal(Offset(atOrigin, template), Offset(elsewhere, applied), 3);
    }
    #endregion

    #region Exaggeration Tests
    /// <summary>Exaggeration doubles the distance from the canon, which is Brennan's formula.</summary>
    /// <remarks>
    /// At <c>amount = 1</c> the subject sits exactly twice as far from the reference as it began —
    /// her published 100%, and the setting she names as the best caricature in her own sequence.
    /// </remarks>
    [Fact]
    public void TestExaggeratingByOneDoublesTheDistanceFromTheCanon()
    {
        var canon = Head();
        var character = Toolkit.CreateParametricHead(canon, Mort);

        var pushed = Toolkit.ExaggerateHead(character, 1f, canon);

        Assert.Equal(Offset(canon, character) * 2f, Offset(canon, pushed), 2);
    }

    /// <summary>Exaggerating by zero is a no-op, so the dial has a neutral position.</summary>
    [Fact]
    public void TestExaggeratingByZeroChangesNothing()
    {
        var character = Character();

        AssertSameShape(character, Toolkit.ExaggerateHead(character, 0f), "head");
    }

    /// <summary>Exaggerating by -1 collapses the character onto its reference.</summary>
    /// <remarks>
    /// The far end of Brennan's continuous scale: below the subject it passes through the norm, and
    /// beyond that caricatures the norm with respect to the subject. Worth pinning because a
    /// one-sided dial would be a different and much less useful control.
    /// </remarks>
    [Fact]
    public void TestExaggeratingByMinusOneReachesTheReference()
    {
        var canon = Head();
        var character = Toolkit.CreateParametricHead(canon, Mort);

        AssertSameShape(canon, Toolkit.ExaggerateHead(character, -1f, canon), "head");
    }

    /// <summary>With no reference given, the canon is rebuilt from the head's own frame.</summary>
    /// <remarks>
    /// The convenience the common call depends on: an agent holding a character head does not also
    /// hold the canon it came from, and requiring it would make the dial unusable in a panel loop.
    /// </remarks>
    [Fact]
    public void TestTheDefaultReferenceIsTheHeadsOwnCanon()
    {
        var canon = Head();
        var character = Toolkit.CreateParametricHead(canon, Mort);

        AssertSameShape(
            Toolkit.ExaggerateHead(character, 1f, canon),
            Toolkit.ExaggerateHead(character, 1f),
            "head");
    }
    #endregion

    #region Refusal Tests
    /// <summary>A head missing a landmark is refused by name rather than blended around.</summary>
    /// <remarks>
    /// A skipped landmark is the worst available outcome: the face blends everywhere except one
    /// feature, which reads as a drawing bug and sends the reader looking at the renderer.
    /// </remarks>
    [Fact]
    public void TestAMissingLandmarkIsRefusedByName()
    {
        var full = Head();
        var maimed = Head();
        ((Dictionary<string, object?>)maimed["mouthGuides"]!).Remove("leftCorner");

        var ex = Assert.Throws<ArgumentException>(() => Toolkit.BlendHead(full, full, maimed, 1f));
        Assert.Contains("mouthGuides.leftCorner", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Heads at different turns are refused, because the blend between them is nothing.</summary>
    [Fact]
    public void TestHeadsAtDifferentYawsAreRefused()
    {
        var frontal = Head();
        var turned = Head(35f);

        var ex = Assert.Throws<ArgumentException>(() => Toolkit.BlendHead(frontal, frontal, turned, 1f));
        Assert.Contains("turn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A non-finite amount is refused rather than propagated into every landmark.</summary>
    /// <remarks>
    /// <c>NaN</c> would otherwise reach every coordinate at once and render as an empty frame, which
    /// is indistinguishable from a clip that was never restored.
    /// </remarks>
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void TestANonFiniteAmountIsRefused(float amount)
    {
        var a = Head();

        Assert.Throws<ArgumentException>(() => Toolkit.BlendHead(a, a, a, amount));
    }
    #endregion

    #region Helpers
    static ConstructiveDrawingToolkit Toolkit => new();

    /// <summary>A character distinct enough that every parameter shows in a displacement.</summary>
    static Dictionary<string, object?> Mort => new()
    {
        ["eyesDistance"] = -0.4f,
        ["eyesSize"] = 0.3f,
        ["noseLength"] = -0.5f,
        ["jawShape"] = 0.8f,
        ["mouthWidth"] = 0.2f,
    };

    static Dictionary<string, object?> Head(float yaw = 0f) =>
        new ConstructiveDrawingToolkit().CreateLoomisHead(400f, 120f, 240f, yaw, 0f);

    static Dictionary<string, object?> Character() =>
        new ConstructiveDrawingToolkit().CreateParametricHead(Head(), Mort);

    /// <summary>
    /// Total landmark displacement between two heads, as one number.
    /// </summary>
    /// <remarks>
    /// Summed over every point rather than sampled at one, because a blend that got the direction
    /// right for the jaw and wrong for the eyes would pass a single-landmark check.
    /// </remarks>
    static float Offset(Dictionary<string, object?> a, Dictionary<string, object?> b)
    {
        var total = 0f;
        foreach (var group in new[] { "nearEye", "farEye", "noseWedge", "mouthGuides", "jaw" })
        {
            var ga = (Dictionary<string, object?>)a[group]!;
            var gb = (Dictionary<string, object?>)b[group]!;
            foreach (var (key, value) in ga)
            {
                if (value is not Dictionary<string, object?> pa) continue;
                var pb = (Dictionary<string, object?>)gb[key]!;
                var dx = Convert.ToSingle(pa["x"]) - Convert.ToSingle(pb["x"]);
                var dy = Convert.ToSingle(pa["y"]) - Convert.ToSingle(pb["y"]);
                total += MathF.Sqrt((dx * dx) + (dy * dy));
            }
        }

        return total;
    }

    static float X(Dictionary<string, object?> head, string group, string key) =>
        Convert.ToSingle(((Dictionary<string, object?>)((Dictionary<string, object?>)head[group]!)[key]!)["x"]);

    static float Y(Dictionary<string, object?> head, string group, string key) =>
        Convert.ToSingle(((Dictionary<string, object?>)((Dictionary<string, object?>)head[group]!)[key]!)["y"]);

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
