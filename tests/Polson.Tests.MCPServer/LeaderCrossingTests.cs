namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The leader-crossing check the vector infographic instructions hand an agent to copy.
/// </summary>
/// <remarks>
/// <b>A slanted leader was never the defect; two that cross are.</b> The kubrick3 render was read as
/// "diagonals are bad", which would ban ordinary timeline craft — an angled leader is fine, and a
/// card at the edge of a plot needs one. What a reader actually feels is a crossing, because that is
/// what makes a card ambiguous about which point it belongs to. So the guidance states the
/// measurable property rather than the aesthetic one, and this runs the snippet it ships.
/// <para>
/// Worth pinning because the instructions hand this to an agent as copyable code. It exercises the
/// sandbox as well as the arithmetic: arrow functions, strict <c>!==</c>, and the spread form
/// <c>crosses(...leaders[i], ...leaders[j])</c>, any of which failing would leave a caller with a
/// check that silently answers the same thing every time.
/// </para>
/// </remarks>
public class LeaderCrossingTests : TestsRuntime
{
    #region Methods
    /// <summary>Runs the documented predicate over one case and reports what it answered.</summary>
    private static bool Crosses(string a, string b)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const side = (a, b, c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            const crosses = (p, q, r, t) =>
                (side(r, t, p) > 0) !== (side(r, t, q) > 0) && (side(p, q, r) > 0) !== (side(p, q, t) > 0);
            const P = (x, y) => ({ x, y });

            // Spread exactly as the instructions write it, so the form is tested and not only the maths.
            const leaders = [[{{a}}], [{{b}}]];
            log('verdict=' + crosses(...leaders[0], ...leaders[1]));
            """, 40, 40, null, "png", 100);

        Assert.True(result.Success, result.Error);
        var line = (result.Logs ?? []).FirstOrDefault(l => l.Contains("verdict=", StringComparison.Ordinal));
        Assert.NotNull(line);
        return line!.Contains("verdict=true", StringComparison.Ordinal);
    }
    #endregion

    #region Tests
    /// <summary>Vertical leaders are parallel, so they pass for free.</summary>
    /// <remarks>
    /// The honest argument for the vertical default: parallel segments cannot cross, so a straight
    /// drop never has to be checked at all.
    /// </remarks>
    [Fact]
    public void TestTwoVerticalLeadersNeverCross()
    {
        Assert.False(Crosses("P(10,0), P(10,50)", "P(20,0), P(20,50)"));
    }

    /// <summary>Slanting the same way is a fan, not a tangle.</summary>
    /// <remarks>This is the case that must be allowed, or the check bans the technique outright.</remarks>
    [Fact]
    public void TestLeadersSlantedTheSameWayDoNotCross()
    {
        Assert.False(Crosses("P(10,0), P(30,50)", "P(40,0), P(60,50)"));
    }

    /// <summary>Opposed slants that overlap are the defect, and are caught.</summary>
    [Fact]
    public void TestOpposedSlantsAreReportedAsCrossing()
    {
        Assert.True(Crosses("P(10,0), P(40,50)", "P(40,0), P(10,50)"));
    }

    /// <summary>Touching at a shared endpoint is not crossing.</summary>
    /// <remarks>
    /// Two events at the same time share an axis point, so a test using non-strict comparisons would
    /// report every such pair as tangled and the check would cry wolf on correct work. The strict
    /// <c>&gt; 0</c> in the snippet is what makes this false rather than true.
    /// </remarks>
    [Fact]
    public void TestLeadersSharingAnAxisPointAreNotCrossing()
    {
        Assert.False(Crosses("P(10,0), P(30,50)", "P(10,0), P(-10,50)"));
    }
    #endregion
}
