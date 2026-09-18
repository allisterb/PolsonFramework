namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;

using Polson.Drawing.Skia;

using Xunit;

/// <summary>
/// <c>verifyHeadOrdering</c> — the check that turns a broken caricature into a number.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists because the alternative measured badly.</b> A constant clamp on exaggeration cannot
/// work: the amount a head tolerates before its stations cross varies more than thirty-fold between
/// characters. Measured on 240px heads against their own canon — a mild character holds past λ 12.6,
/// the documented `MORT` to 5.15, the canon never breaks, and a head carrying <c>noseLength: 1</c>
/// breaks at <b>0.40</b>, below the exaggeration Brennan recommends.
/// </para>
/// <para>
/// Those numbers are asserted below rather than only recorded, because they are the argument for the
/// design. If they drift, the reasoning behind reporting instead of clamping drifts with them.
/// </para>
/// </remarks>
public class HeadOrderingTests : TestsRuntime
{
    #region Ordering Tests
    /// <summary>An ordinary head is in order, with room to spare.</summary>
    [Fact]
    public void TestAPlainHeadIsOrdered()
    {
        var result = Toolkit.VerifyHeadOrdering(Head());

        Assert.True((bool)result["ordered"]!);
        Assert.Empty((List<object?>)result["broken"]!);
        Assert.True(Margin(result) > 0f);
    }

    /// <summary>Every character the parameter layer can build is in order before exaggeration.</summary>
    /// <remarks>
    /// The guard on the parameter ranges themselves: a documented setting that produced a disordered
    /// face on its own would be a defect in <c>createParametricHead</c> rather than in the caller.
    /// </remarks>
    [Theory]
    [InlineData("noseLength", 1f)]
    [InlineData("noseLength", -1f)]
    [InlineData("jawShape", 1f)]
    [InlineData("jawShape", -1f)]
    [InlineData("chinShape", 1f)]
    [InlineData("chinShape", -1f)]
    [InlineData("chinLength", 1f)]
    [InlineData("chinLength", -1f)]
    [InlineData("mouthWidth", -1f)]
    [InlineData("eyesSize", 1f)]
    [InlineData("eyesDistance", 1f)]
    [InlineData("eyesOpening", -1f)]
    public void TestEveryParameterExtremeIsStillAFace(string parameter, float value)
    {
        var head = Toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { [parameter] = value });

        var result = Toolkit.VerifyHeadOrdering(head);

        Assert.True((bool)result["ordered"]!, $"{parameter} at {value}: {result["message"]}");
    }

    /// <summary>A face pushed far enough stops being one, and the report names which pair crossed.</summary>
    [Fact]
    public void TestAnOverExaggeratedHeadIsReportedWithTheBrokenPair()
    {
        var canon = Head();
        var longNose = Toolkit.CreateParametricHead(canon, new Dictionary<string, object?> { ["noseLength"] = 1f });

        var result = Toolkit.VerifyHeadOrdering(Toolkit.ExaggerateHead(longNose, 3f, canon));

        Assert.False((bool)result["ordered"]!);
        Assert.Contains("mouth above nose", (List<object?>)result["broken"]!);
        Assert.True(Margin(result) < 0f, "a broken head must report a negative margin");
    }
    #endregion

    #region Margin Tests
    /// <summary>
    /// The margin shrinks as the exaggeration rises, so the edge is visible before it is crossed.
    /// </summary>
    /// <remarks>
    /// <b>The property that makes this worth having over a boolean.</b> A run can watch the number
    /// approach zero and stop, rather than discovering the limit by rendering past it — which is the
    /// whole difference between a measurement and an alarm.
    /// </remarks>
    [Fact]
    public void TestTheMarginNarrowsAsExaggerationRises()
    {
        var canon = Head();
        var character = Toolkit.CreateParametricHead(canon, new Dictionary<string, object?> { ["noseLength"] = 0.6f });

        var at0 = Margin(Toolkit.VerifyHeadOrdering(character));
        var at1 = Margin(Toolkit.VerifyHeadOrdering(Toolkit.ExaggerateHead(character, 1f, canon)));
        var at2 = Margin(Toolkit.VerifyHeadOrdering(Toolkit.ExaggerateHead(character, 2f, canon)));

        Assert.True(at1 < at0, "the margin must narrow between 0 and 1");
        Assert.True(at2 < at1, "and keep narrowing");
    }

    /// <summary>The margin is scale-free, so the same face reports the same room at any size.</summary>
    /// <remarks>
    /// Normalised by head height for the reason every other measurement here is: a threshold a run
    /// picks on a 240px draft has to mean the same thing on a 900px final.
    /// </remarks>
    [Fact]
    public void TestTheMarginIsIndependentOfHeadSize()
    {
        var small = Toolkit.CreateParametricHead(
            Toolkit.CreateLoomisHead(400f, 120f, 240f, 0f, 0f),
            new Dictionary<string, object?> { ["noseLength"] = 0.7f });
        var large = Toolkit.CreateParametricHead(
            Toolkit.CreateLoomisHead(90f, 700f, 900f, 0f, 0f),
            new Dictionary<string, object?> { ["noseLength"] = 0.7f });

        Assert.Equal(Margin(Toolkit.VerifyHeadOrdering(small)), Margin(Toolkit.VerifyHeadOrdering(large)), 3);
    }
    #endregion

    #region Measured Limit Tests
    /// <summary>
    /// The safe exaggeration varies by more than thirty-fold, which is why nothing is clamped.
    /// </summary>
    /// <remarks>
    /// <b>The measurement the design rests on.</b> A constant clamp would have to be low enough for
    /// the long-nosed character and would then cap the mild one at a twelfth of its usable range —
    /// and it would still not protect the long-nosed one at Brennan's own recommended setting of 1.
    /// </remarks>
    [Fact]
    public void TestTheBreakingPointDependsOnTheCharacter()
    {
        var mild = BreaksAt(new Dictionary<string, object?> { ["jawShape"] = 0.2f, ["noseLength"] = 0.1f });
        var longNose = BreaksAt(new Dictionary<string, object?> { ["noseLength"] = 1f });

        Assert.True(longNose < 1f, $"a long nose should break below Brennan's 1, broke at {longNose}");
        Assert.True(mild > 8f, $"a mild character should hold well past 1, broke at {mild}");
        Assert.True(mild / longNose > 10f, "the spread is what rules a constant clamp out");
    }

    /// <summary>
    /// <c>chinLength</c> does not become the new binding constraint; <c>noseLength</c> stays it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The check that keeps a new parameter from quietly costing every caricature its range.</b>
    /// <c>noseLength</c>'s full scale already spends most of the canon's nose-to-mouth gap, which is
    /// why a head carrying it breaks below Brennan's own recommended exaggeration of 1. A second
    /// parameter as greedy with the mouth-to-chin gap would halve the usable range again, and would
    /// do it invisibly — the head still draws, and only a caricature run notices. Pinned rather than
    /// merely intended, because the constant that decides it is one edit away from being tuned up.
    /// </para>
    /// <para>
    /// <b>Measured on 240px heads: <c>noseLength: 1</c> breaks at 0.40, <c>chinLength: -1</c> at
    /// 2.85, and <c>chinLength: +1</c> never breaks at all.</b> That asymmetry is not luck — a
    /// lengthening chin moves <i>away</i> from the mouth, so it widens the gap the ladder measures
    /// and there is nothing left to cross. Only the shortening half is a constraint, and it is
    /// seven times looser than the nose.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestALongChinLeavesMoreRoomThanALongNose()
    {
        var longNose = BreaksAt(new Dictionary<string, object?> { ["noseLength"] = 1f });
        var shortChin = BreaksAt(new Dictionary<string, object?> { ["chinLength"] = -1f });

        Assert.True(shortChin > 2f,
            $"a short chin must hold well past Brennan's 1, broke at {shortChin}");
        Assert.True(shortChin > longNose * 5f,
            $"chinLength ({shortChin}) must be a far looser constraint than noseLength ({longNose})");

        Assert.True(float.IsPositiveInfinity(BreaksAt(new Dictionary<string, object?> { ["chinLength"] = 1f })),
            "lengthening the chin moves it away from the mouth, so that half cannot break the ladder");
    }

    /// <summary>The canon never breaks, because there is no difference to exaggerate.</summary>
    [Fact]
    public void TestTheCanonHasNoBreakingPoint()
    {
        var canon = Head();

        var far = Toolkit.ExaggerateHead(canon, 50f, canon);

        Assert.True((bool)Toolkit.VerifyHeadOrdering(far)["ordered"]!);
    }
    #endregion

    #region Helpers
    static ConstructiveDrawingToolkit Toolkit => new();

    static Dictionary<string, object?> Head() =>
        new ConstructiveDrawingToolkit().CreateLoomisHead(400f, 120f, 240f, 0f, 0f);

    static float Margin(Dictionary<string, object?> result) =>
        Convert.ToSingle(result["margin"]);

    /// <summary>The lowest exaggeration at which this character stops reading as a face.</summary>
    static float BreaksAt(Dictionary<string, object?> parameters)
    {
        var canon = Head();
        var character = Toolkit.CreateParametricHead(canon, parameters);
        for (var k = 0f; k <= 30f; k += 0.05f)
        {
            if (!(bool)Toolkit.VerifyHeadOrdering(Toolkit.ExaggerateHead(character, k, canon))["ordered"]!) return k;
        }

        return float.PositiveInfinity;
    }
    #endregion
}
