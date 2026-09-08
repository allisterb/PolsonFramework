namespace Polson.Tests.MCPServer;

using System;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// What a script is told when a call is given the wrong <em>number</em> of arguments.
/// </summary>
/// <remarks>
/// Jint reports "No public methods with the specified arguments were found" for a wrong count and a
/// wrong type alike, and the engine's advice was written for the second: check the spelling of every
/// property on the line, watch for <c>w</c> and <c>h</c> on Layout rectangles. Against a count error
/// that advice is not merely thin — it is <b>misdirecting</b>, and a live run followed it into a wall.
/// <para>
/// Measured on the kubrick5 run: <c>paper.line(cx, cy - 10, cx, cy + 10, cy)</c>, a stray trailing
/// value on a four-argument method. There was no property, no misspelling and no Layout rectangle on
/// that line. The agent checked what it was told to check, found nothing, and <b>re-ran the
/// byte-identical script</b>. Four scripts went before it moved on.
/// </para>
/// <para>
/// Every script here really executes, for the reason <c>ExecutionLimitTests</c> gives for overflowing
/// the cap genuinely: the advice keys off Jint's own wording and off the symbol manifest's own
/// signatures, so a faked exception would keep passing while the guidance quietly stopped attaching.
/// </para>
/// </remarks>
public class ArgumentCountTests : TestsRuntime
{
    #region Methods
    private static string Run(string script) =>
        new JsDrawingEngine().Execute(script, 40, 40, null, "png", 100) is { Success: false } failed
            ? failed.Error ?? ""
            : throw new Xunit.Sdk.XunitException("the script was expected to fail and did not");
    #endregion

    #region Tests
    /// <summary>The live failure, and the number that was missing from it.</summary>
    [Fact]
    public void TestTooManyArgumentsAreCountedAndReported()
    {
        var error = Run("""
            const paper = Snap(200, 200);
            paper.line(10, 20, 10, 40, 20);
            """);

        Assert.Contains("passes 5 arguments", error, StringComparison.Ordinal);
        Assert.Contains("'line'", error, StringComparison.Ordinal);
        Assert.Contains("accepts 4", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the misdirection is gone with it.
    /// </summary>
    /// <remarks>
    /// The point is not only that the count appears — it is that the reader is no longer sent to look
    /// for a misspelled property that was never there. Asserting the absence is what makes this a
    /// test of the diagnosis rather than of the string.
    /// </remarks>
    [Fact]
    public void TestTheSpellingAdviceIsNotGivenForACountError()
    {
        var error = Run("""
            const paper = Snap(200, 200);
            paper.line(10, 20, 10, 40, 20);
            """);

        Assert.DoesNotContain("Check the spelling", error, StringComparison.Ordinal);
        Assert.DoesNotContain("not w and h", error, StringComparison.Ordinal);
    }

    /// <summary>A nested call's commas are its own, not the outer call's.</summary>
    /// <remarks>
    /// Counting every comma would read this as five arguments and report a correct call as wrong,
    /// which is the same class of confident wrong answer the whole change exists to remove.
    /// </remarks>
    [Fact]
    public void TestArgumentsNestedInsideAnotherCallAreNotCountedTwice()
    {
        var error = Run("""
            const paper = Snap(200, 200);
            paper.line(Math.min(10, 20), 20, Math.max(30, 40), 40, 99);
            """);

        Assert.Contains("passes 5 arguments", error, StringComparison.Ordinal);
    }

    /// <summary>An object literal is one argument however many colons it holds.</summary>
    [Fact]
    public void TestAnObjectLiteralCountsAsASingleArgument()
    {
        var error = Run("""
            const paper = Snap(200, 200);
            paper.rect(0, 0, 10, 10).attr({ fill: '#f00', stroke: '#000', 'stroke-width': 2 }, 1, 2);
            """);

        Assert.Contains("passes 3 arguments", error, StringComparison.Ordinal);
    }

    /// <summary>Optional parameters widen the accepted range rather than fixing it.</summary>
    /// <remarks>
    /// <c>paper.rect(x, y, width, height, rx?, ry?)</c> takes four through six, so five is correct and
    /// must not be reported. A rule that only knew the maximum would refuse every call that omitted an
    /// optional argument — most of them.
    /// </remarks>
    [Fact]
    public void TestACallWithinItsOptionalRangeIsNotReported()
    {
        var paper = new JsDrawingEngine().Execute("""
            const paper = Snap(200, 200);
            paper.rect(0, 0, 40, 40, 4);
            paper;
            """, 40, 40, null, "png", 100);

        Assert.True(paper.Success, paper.Error);
    }

    /// <summary>A bare call is left alone, because it is probably the script's own function.</summary>
    /// <remarks>
    /// The script below defines its own <c>line</c> and calls it with three arguments. Matching on the
    /// name alone would announce the SDK's arity at a function that has nothing to do with it.
    /// </remarks>
    [Fact]
    public void TestABareCallIsNotMatchedAgainstTheSurface()
    {
        var result = new JsDrawingEngine().Execute("""
            function line(a, b, c) { return a + b + c; }
            const total = line(1, 2, 3);
            log('total ' + total);
            'done';
            """, 40, 40, null, "png", 100);

        Assert.True(result.Success, result.Error);
    }
    #endregion
}
