namespace Polson.Tests.Drawing;

using Polson.Drawing.Svg;
using Xunit;

/// <summary>
/// <c>Snap.snapTo</c> is documented as snapping to the *closest* candidate, but scanned in array
/// order and returned the first one inside the tolerance. Snapping 43 against every multiple of
/// five gave 40 rather than 45, and reordering the same list changed the answer — which made it
/// unusable for the gridding pass in Manual 10, where a stray angle has to land on the nearest
/// tidy one.
/// </summary>
public class SnapToTests : TestsRuntime
{
    #region Closest Match Tests
    /// <summary>The case that exposed it: 45 is nearer than 40, but 40 came first.</summary>
    [Fact]
    public void TestSnapsToClosestNotFirstInRange()
    {
        var multiplesOfFive = new float[] { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 };

        Assert.Equal(45f, Snap.SnapTo(multiplesOfFive, 43f, 3f));
        Assert.Equal(35f, Snap.SnapTo(multiplesOfFive, 33.46f, 3f));
    }

    /// <summary>Order of the candidate list must not change the answer.</summary>
    [Fact]
    public void TestResultIsIndependentOfCandidateOrder()
    {
        var ascending = new float[] { 0, 40, 45, 90 };
        var descending = new float[] { 90, 45, 40, 0 };

        Assert.Equal(Snap.SnapTo(ascending, 43f, 5f), Snap.SnapTo(descending, 43f, 5f));
    }
    #endregion

    #region Tolerance Tests
    /// <summary>Outside the tolerance the value passes through untouched — a deliberate angle survives.</summary>
    [Theory]
    [InlineData(20f, 20f)]
    [InlineData(37f, 37f)]
    public void TestValueOutsideToleranceIsUnchanged(float input, float expected) =>
        Assert.Equal(expected, Snap.SnapTo([0, 45, 90], input, 3f));

    /// <summary>A candidate exactly at the tolerance boundary still snaps.</summary>
    [Fact]
    public void TestBoundaryDistanceSnaps() => Assert.Equal(45f, Snap.SnapTo([45], 42f, 3f));

    /// <summary>Just beyond it does not.</summary>
    [Fact]
    public void TestBeyondBoundaryDoesNotSnap() => Assert.Equal(41f, Snap.SnapTo([45], 41f, 3f));
    #endregion

    #region Degenerate Input Tests
    [Fact]
    public void TestEmptyCandidateListReturnsValue() => Assert.Equal(17f, Snap.SnapTo([], 17f, 5f));

    [Fact]
    public void TestNullCandidateListReturnsValue() => Assert.Equal(17f, Snap.SnapTo(null!, 17f, 5f));

    /// <summary>Ties resolve to the earlier entry, so repeated calls agree.</summary>
    [Fact]
    public void TestTieResolvesToEarlierCandidate() => Assert.Equal(40f, Snap.SnapTo([40, 50], 45f, 10f));
    #endregion
}
