namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// Two defects a comic-studio agent found in stage 1, reproduced from its own report.
/// </summary>
/// <remarks>
/// Both were silent — no exception, no error, a confident wrong answer — which is why they survived
/// until an agent working the API from outside happened to notice. Kept together because what they
/// have in common matters more than what either does: a call that lies is worse than one that fails.
/// </remarks>
public class HarnessReportedBugTests : TestsRuntime
{
    #region Fill Rule Tests
    /// <summary>
    /// Renders a rect-with-a-hole through a clip and returns whether the centre came out filled.
    /// </summary>
    /// <remarks>
    /// Even-odd knocks the inner rectangle out, so the centre is background; non-zero fills straight
    /// through it. One pixel therefore distinguishes the two rules.
    /// </remarks>
    private static bool[] CentreFilledAfter(params string[] rules)
    {
        var calls = new List<string>();
        foreach (var rule in rules)
        {
            var arg = rule is null ? "" : $", '{rule}'";
            calls.Add($$"""
                x.save();
                x.clip(p{{arg}});
                x.fillStyle = '#000000';
                x.fillRect(0, 0, 300, 200);
                x.restore();
                probes.push(c.bitmap.getPixel(150, 100));
                x.fillStyle = '#ffffff';
                x.fillRect(0, 0, 300, 200);
                """);
        }

        var result = new JsDrawingEngine().Execute($$"""
            const c = createCanvas(300, 200);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 300, 200);

            const p = new CanvasPath();
            p.rect(0, 0, 300, 200);
            const q = new CanvasPath();
            q.rect(100, 60, 100, 80);
            p.addPath(q);

            const probes = [];
            {{string.Join('\n', calls)}}
            log(probes.join('|'));
            c;
            """, 300, 200, null, "png", 100);

        Assert.True(result.Success, result.Error);

        var probes = Pixels(result.Logs);
        Assert.Equal(rules.Length, probes.Length);

        // Black centre means the clip let the fill through: non-zero. White means it was knocked out.
        var filled = new bool[probes.Length];
        for (var i = 0; i < probes.Length; i++) filled[i] = probes[i].StartsWith("#00", StringComparison.OrdinalIgnoreCase);
        return filled;
    }

    /// <summary>Pulls the <c>#RRGGBBAA</c> readings out of the log, whatever else a line carries.</summary>
    /// <remarks>
    /// Log entries are prefixed, so splitting the joined text on a separator leaves the prefix glued
    /// to the first reading — which silently corrupted exactly one probe and made two of these tests
    /// fail for a reason that had nothing to do with what they were testing.
    /// </remarks>
    private static string[] Pixels(IEnumerable<string> logs) =>
        System.Text.RegularExpressions.Regex
            .Matches(string.Join('\n', logs), "#[0-9A-Fa-f]{8}")
            .Select(m => m.Value)
            .ToArray();

    /// <summary>
    /// The fill rule is an argument to the call, not a property the path keeps.
    /// </summary>
    /// <remarks>
    /// The agent's reproduction exactly: an even-odd clip followed by a clip with no rule. The second
    /// must be non-zero — the HTML5 default — not "whatever was set last". It used to inherit
    /// even-odd, silently, on any path with a counter.
    /// </remarks>
    [Fact]
    public void TestAnEvenOddClipDoesNotChangeTheNextCall()
    {
        var filled = CentreFilledAfter("evenodd", null!, "nonzero");

        Assert.False(filled[0]);   // evenodd knocks the centre out
        Assert.True(filled[1]);    // no rule means nonzero, regardless of what came before
        Assert.True(filled[2]);    // and explicit nonzero agrees
    }

    /// <summary>The same path, used twice with no rule, behaves the same both times.</summary>
    [Fact]
    public void TestAPathIsReusableAsTheDocsPromise()
    {
        var filled = CentreFilledAfter(null!, "evenodd", null!);

        Assert.True(filled[0]);
        Assert.False(filled[1]);
        Assert.True(filled[2]);
    }

    /// <summary>Filling is subject to the same rule, and equally must not leave a mark on the path.</summary>
    [Fact]
    public void TestAnEvenOddFillDoesNotChangeTheNextFill()
    {
        var result = new JsDrawingEngine().Execute("""
            const c = createCanvas(300, 200);
            const x = c.getContext('2d');
            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 300, 200);

            const p = new CanvasPath();
            p.rect(0, 0, 300, 200);
            const q = new CanvasPath();
            q.rect(100, 60, 100, 80);
            p.addPath(q);

            x.fillStyle = '#000000';
            x.fill(p, 'evenodd');
            const afterEvenOdd = c.bitmap.getPixel(150, 100);

            x.fillStyle = '#ffffff';
            x.fillRect(0, 0, 300, 200);
            x.fillStyle = '#000000';
            x.fill(p);
            const afterDefault = c.bitmap.getPixel(150, 100);

            log(afterEvenOdd + '|' + afterDefault);
            c;
            """, 300, 200, null, "png", 100);

        Assert.True(result.Success, result.Error);

        var probes = Pixels(result.Logs);
        Assert.Equal(2, probes.Length);
        Assert.StartsWith("#FF", probes[0], StringComparison.OrdinalIgnoreCase);   // knocked out
        Assert.StartsWith("#00", probes[1], StringComparison.OrdinalIgnoreCase);   // filled through
    }
    #endregion

    #region Convergence Tests
    private static readonly ConstructiveDrawingToolkit Toolkit = new();

    private static Dictionary<string, object?> Converge(bool topToBottom)
    {
        object Pt(float x, float y) => new Dictionary<string, object?> { ["x"] = x, ["y"] = y };

        var lines = topToBottom
            ? new List<object> { new List<object> { Pt(174, 0), Pt(168, 340) },
                                 new List<object> { Pt(322, 0), Pt(300, 340) } }
            : new List<object> { new List<object> { Pt(168, 340), Pt(174, 0) },
                                 new List<object> { Pt(300, 340), Pt(322, 0) } };

        return Toolkit.VerifyPerspectiveConvergence(lines, Pt(250, -4000), 6f);
    }

    /// <summary>
    /// The verdict does not depend on which end of each line was listed first.
    /// </summary>
    /// <remarks>
    /// The agent's reproduction. Listed top-to-bottom against a vanishing point above the frame, the
    /// check reported 179.9° of drift on lines that converge — and top-to-bottom is the natural way
    /// to write a vertical. A verifier that returns a confident wrong answer is worse than none,
    /// because the whole point of calling it is to not have to trust your eye.
    /// </remarks>
    [Fact]
    public void TestConvergenceIsIndependentOfPointOrder()
    {
        var down = Converge(topToBottom: true);
        var up = Converge(topToBottom: false);

        Assert.True((bool)down["passed"]!, $"top-to-bottom: {down["message"]}");
        Assert.True((bool)up["passed"]!, $"bottom-to-top: {up["message"]}");

        var downError = Convert.ToSingle(down["maxAngularErrorDeg"]);
        var upError = Convert.ToSingle(up["maxAngularErrorDeg"]);

        Assert.Equal(upError, downError, 3);
    }

    /// <summary>A line that genuinely does not converge is still reported as drifting.</summary>
    /// <remarks>
    /// The fold must not become a way of passing everything: folding at 90° means a perpendicular
    /// line is the worst case, and that is exactly what this asserts.
    /// </remarks>
    [Fact]
    public void TestAGenuinelyDivergentLineStillFails()
    {
        object Pt(float x, float y) => new Dictionary<string, object?> { ["x"] = x, ["y"] = y };

        var lines = new List<object> { new List<object> { Pt(0, 100), Pt(300, 100) } };   // horizontal
        var check = Toolkit.VerifyPerspectiveConvergence(lines, Pt(150, -4000), 5f);      // VP straight up

        Assert.False((bool)check["passed"]!);
        Assert.True(Convert.ToSingle(check["maxAngularErrorDeg"]) > 80f,
            $"a perpendicular line should be near the 90° worst case, got {check["maxAngularErrorDeg"]}");
    }
    #endregion
}
