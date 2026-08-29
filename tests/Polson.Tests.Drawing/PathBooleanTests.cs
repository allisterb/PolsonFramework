namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Boolean operations on <c>CanvasPath</c>: union, subtract, intersect, xor, and simplify.
/// </summary>
/// <remarks>
/// Asked for from two directions. A comic-studio agent wanted the outside of a set of shapes as a
/// clip region and reported there was "no documented way" — the even-odd rect-plus-shapes idiom works
/// but is fragile on self-intersecting contours, and no `Op(Difference)` was reachable. The same gap
/// turned up while surveying what SkiaSharp offers that scripts cannot get at.
/// <para>
/// The operations return new paths rather than mutating the receiver. That is deliberate: the last
/// silent bug in this class was a stored fill rule outliving the call that set it, and an operation
/// that quietly rewrote its operand would be the same mistake somewhere more visible.
/// </para>
/// </remarks>
public class PathBooleanTests : TestsRuntime
{
    #region Methods
    /// <summary>
    /// Fills the path built by <paramref name="build"/> in black on white and probes named points.
    /// </summary>
    private static Dictionary<string, bool> Probe(string build, params (string Name, int X, int Y)[] points)
    {
        var probes = string.Join('\n', points.Select(p => $"log('{p.Name}=' + c.bitmap.getPixel({p.X}, {p.Y}));"));

        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(200, 200);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 200, 200);

            const a = new CanvasPath(); a.rect(20, 20, 100, 100);
            const b = new CanvasPath(); b.rect(80, 80, 100, 100);

            {{build}}

            x.fillStyle = '#000000';
            x.fill(shape);
            {{probes}}
            c;
            """, 200, 200, null, "png", 100);

        Assert.True(result.Success, result.Error);

        var text = string.Join('\n', result.Logs);
        var filled = new Dictionary<string, bool>();

        foreach (var (name, _, _) in points)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, $@"{name}=(#[0-9A-Fa-f]{{8}})");
            Assert.True(match.Success, $"no reading logged for {name}");
            filled[name] = match.Groups[1].Value.StartsWith("#00", StringComparison.OrdinalIgnoreCase);
        }

        return filled;
    }

    // Two overlapping squares: A covers 20..120, B covers 80..180. They share 80..120.
    private static readonly (string, int, int)[] Regions =
    [
        ("onlyA", 40, 40),      // inside A alone
        ("both", 100, 100),     // the overlap
        ("onlyB", 160, 160),    // inside B alone
        ("outside", 190, 20),   // neither
    ];
    #endregion

    #region Boolean Operations
    [Fact]
    public void TestUnionCoversEitherPath()
    {
        var f = Probe("const shape = a.union(b);", Regions);

        Assert.True(f["onlyA"]);
        Assert.True(f["both"]);
        Assert.True(f["onlyB"]);
        Assert.False(f["outside"]);
    }

    /// <summary>
    /// Subtract cuts a real hole — the counter-cutting operation the SDK had no answer for.
    /// </summary>
    [Fact]
    public void TestSubtractCutsTheSecondPathOut()
    {
        var f = Probe("const shape = a.subtract(b);", Regions);

        Assert.True(f["onlyA"]);
        Assert.False(f["both"]);     // cut away
        Assert.False(f["onlyB"]);
        Assert.False(f["outside"]);
    }

    [Fact]
    public void TestIntersectKeepsOnlyTheOverlap()
    {
        var f = Probe("const shape = a.intersect(b);", Regions);

        Assert.False(f["onlyA"]);
        Assert.True(f["both"]);
        Assert.False(f["onlyB"]);
    }

    [Fact]
    public void TestXorKnocksOutTheOverlap()
    {
        var f = Probe("const shape = a.xor(b);", Regions);

        Assert.True(f["onlyA"]);
        Assert.False(f["both"]);
        Assert.True(f["onlyB"]);
    }
    #endregion

    #region Operand Safety
    /// <summary>
    /// An operation leaves both operands as they were, so a path can be combined repeatedly.
    /// </summary>
    /// <remarks>
    /// The property the fill-rule bug did not have. Here it is asserted directly: after subtracting,
    /// filling <c>a</c> alone still covers the region that the subtraction removed.
    /// </remarks>
    [Fact]
    public void TestOperandsAreNotMutated()
    {
        var f = Probe("""
            const cut = a.subtract(b);      // discard the result; a must be untouched
            const shape = a;
            """, Regions);

        Assert.True(f["onlyA"]);
        Assert.True(f["both"], "the overlap should still be filled — subtract must not have altered 'a'");
    }

    /// <summary>The result is a path like any other, so operations chain.</summary>
    [Fact]
    public void TestResultsChain()
    {
        var f = Probe("""
            const cc = new CanvasPath(); cc.rect(0, 0, 60, 60);
            const shape = a.union(b).subtract(cc);
            """, Regions);

        Assert.False(f["onlyA"], "the (40,40) probe sits inside the square that was cut away");
        Assert.True(f["both"]);
        Assert.True(f["onlyB"]);
    }
    #endregion

    #region Simplify
    /// <summary>
    /// Simplify resolves a self-intersecting contour, which is what makes a boolean op on a
    /// hand-built shape reliable.
    /// </summary>
    [Fact]
    public void TestSimplifyResolvesASelfIntersectingContour()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(200, 200);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 200, 200);

            // A bow-tie: the contour crosses itself, so the two lobes wind oppositely.
            const p = new CanvasPath();
            p.moveTo(20, 20); p.lineTo(180, 180); p.lineTo(180, 20); p.lineTo(20, 180); p.closePath();

            const s = p.simplify();
            x.fillStyle = '#000000';
            x.fill(s);
            log('left=' + c.bitmap.getPixel(40, 100) + ' right=' + c.bitmap.getPixel(160, 100));
            c;
            """, 200, 200, null, "png", 100);

        Assert.True(result.Success, result.Error);

        // Both lobes of the bow-tie survive simplification.
        var text = string.Join('\n', result.Logs);
        Assert.Contains("left=#000000", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("right=#000000", text, StringComparison.OrdinalIgnoreCase);
    }
    #endregion
}
