namespace Polson.Tests.Drawing;

using System.Collections.Generic;
using System.Linq;

using global::Polson.Drawing.Skia;
using global::Polson.Tests;
using Xunit;

/// <summary>
/// <c>Scale.checkSeries</c> — whether a run of positions can honestly be joined by a line.
/// </summary>
/// <remarks>
/// The failure it exists for is the one no other check catches: every value correct, the picture
/// still wrong. A live infographic run put Apple M4 and NVIDIA B200 both at 2024 and joined them,
/// so the transistor line spiked to 208 billion and dropped back to 28 inside one tick — asserting a
/// collapse that never happened. The audit that followed passed it as "monotonic scaling preserved".
/// </remarks>
public class ScaleSeriesCheckTests : TestsRuntime
{
    #region Fields
    private readonly ScaleToolkit scale = new();
    #endregion

    #region The defect this exists for

    /// <summary>The live case, reduced: two chips released in the same year, joined by a line.</summary>
    [Fact]
    public void TwoValuesAtOnePositionIsRefusedWithTheFormNamed()
    {
        var result = scale.CheckSeries(new[] { 1971, 1978, 1985, 1993, 2006, 2016, 2020, 2024, 2024 });

        Assert.False((bool)result["ok"]);

        var duplicates = (Dictionary<string, object>[])result["duplicates"];
        var duplicate = Assert.Single(duplicates);
        Assert.Equal(2024d, duplicate["value"]);
        Assert.Equal(2, duplicate["count"]);

        var message = (string)result["message"];
        Assert.Contains("2024", message);
        Assert.Contains("within a single position", message);

        // The remedy must be about the FORM. "Deduplicate" would discard a real chip.
        Assert.Contains("separate series", message);
        Assert.Contains("dot chart", message);
        Assert.DoesNotContain("remove", message);
    }

    /// <summary>A path that doubles back through time is the other way to break a trajectory.</summary>
    [Fact]
    public void PositionsGoingBackwardsAreRefusedWithTheIndex()
    {
        var result = scale.CheckSeries(new[] { 1971, 1985, 1978, 1993 });

        Assert.False((bool)result["ok"]);
        Assert.False((bool)result["ascending"]);
        Assert.Equal(2, result["firstDescentIndex"]);
        Assert.Contains("doubles back", (string)result["message"]);
        Assert.Contains("Sort by position", (string)result["message"]);
    }

    [Fact]
    public void BothFaultsAtOnceAreBothReported()
    {
        var result = scale.CheckSeries(new[] { 2000, 2010, 2005, 2005 });

        Assert.False((bool)result["ok"]);
        Assert.Single((Dictionary<string, object>[])result["duplicates"]);
        Assert.False((bool)result["ascending"]);

        var message = (string)result["message"];
        Assert.Contains("more than one value", message);
        Assert.Contains("doubles back", message);
    }
    #endregion

    #region What passes

    [Fact]
    public void AStrictlyAscendingSeriesIsDrawableAsALine()
    {
        var result = scale.CheckSeries(new[] { 1971, 1978, 1985, 1993, 2006, 2016, 2020, 2024 });

        Assert.True((bool)result["ok"]);
        Assert.True((bool)result["ascending"]);
        Assert.Empty((Dictionary<string, object>[])result["duplicates"]);
        Assert.Equal(-1, result["firstDescentIndex"]);
        Assert.Contains("Drawable as a line", (string)result["message"]);
    }

    [Theory]
    [InlineData(new[] { 1.0 })]
    [InlineData(new double[0])]
    public void ASeriesTooShortToHaveAFaultPasses(double[] positions)
    {
        Assert.True((bool)scale.CheckSeries(positions)["ok"]);
    }

    /// <summary>Non-integer positions are ordinary — a timestamp or a fractional year.</summary>
    [Fact]
    public void FractionalPositionsAreHandled()
    {
        var result = scale.CheckSeries(new[] { 2020.25, 2020.5, 2021.0 });

        Assert.True((bool)result["ok"]);
        Assert.Equal(3, result["count"]);
    }
    #endregion

    #region Edges

    /// <summary>
    /// A position that is not a number cannot be placed, so it cannot be part of a trajectory. Counted
    /// separately from the other two faults, because the fix is different.
    /// </summary>
    [Fact]
    public void NonFinitePositionsAreCountedAndRefused()
    {
        var result = scale.CheckSeries(new[] { 1.0, double.NaN, 3.0, double.PositiveInfinity });

        Assert.False((bool)result["ok"]);
        Assert.Equal(2, result["nonFinite"]);
        Assert.Contains("not finite", (string)result["message"]);
    }

    /// <summary>
    /// Three at one position is worse than two, and the count says so rather than just flagging it.
    /// </summary>
    [Fact]
    public void RepeatsBeyondTwoAreCounted()
    {
        var result = scale.CheckSeries(new[] { 2024, 2024, 2024, 2025 });

        var duplicate = Assert.Single((Dictionary<string, object>[])result["duplicates"]);
        Assert.Equal(3, duplicate["count"]);
    }

    /// <summary>
    /// Repeated positions are reported in order and the message names the first few, so a long series
    /// with many collisions is still readable.
    /// </summary>
    [Fact]
    public void ManyDuplicatesAreOrderedAndTheMessageStaysShort()
    {
        var result = scale.CheckSeries(new[] { 2000, 2000, 1990, 1990, 2010, 2010, 2020, 2020 });

        var duplicates = (Dictionary<string, object>[])result["duplicates"];
        Assert.Equal(4, duplicates.Length);
        Assert.Equal(1990d, duplicates[0]["value"]);     // ordered by position, not by appearance
        Assert.Equal(2020d, duplicates[^1]["value"]);
        Assert.True(((string)result["message"]).Length < 500);
    }

    /// <summary>Equal adjacent positions are a duplicate, not a descent — the two are distinct faults.</summary>
    [Fact]
    public void EqualAdjacentPositionsAreNotCountedAsADescent()
    {
        var result = scale.CheckSeries(new[] { 1, 2, 2, 3 });

        Assert.True((bool)result["ascending"]);
        Assert.Equal(-1, result["firstDescentIndex"]);
        Assert.Single((Dictionary<string, object>[])result["duplicates"]);
    }
    #endregion
}
