namespace Polson.Tests.MCPServer;

using System;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// What a script is told when the thing that failed was inside an <c>await</c>.
/// </summary>
/// <remarks>
/// Jint surfaces anything thrown inside an await as a <c>PromiseRejectedException</c> rather than a
/// <c>JavaScriptException</c>, and the catch for it stopped at the message — so the line number and
/// every piece of advice were skipped for <b>every script using top-level await</b>. That is not an
/// edge case: it is every script that requisitions an asset or reads a document, which is the normal
/// shape of an infographic run.
/// <para>
/// Measured on the kubrick3 run: <c>timelineModel.scale(yr)</c> cost a script and got back
/// "Promise was rejected with value TypeError: scale is not a function" — no line, no advice, from a
/// 566-line file. The hint it needed had been written and tested, and was unreachable.
/// </para>
/// <para>
/// Every script here <b>really awaits</b> rather than fabricating the exception, for the reason
/// <c>ExecutionLimitTests</c> gives for overflowing the cap genuinely: the enrichment keys off Jint's
/// own wording and its own rejected-value shape, so a faked exception would keep passing while the
/// guidance silently stopped being attached.
/// </para>
/// </remarks>
public class AwaitedFailureTests : TestsRuntime
{
    #region Methods
    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 40, 40, null, "png", 100);
    #endregion

    #region Tests
    /// <summary>The live failure: a d3-style scale call, inside an await.</summary>
    [Fact]
    public void TestAD3StyleScaleCallInsideAnAwaitStillGetsItsAdvice()
    {
        var result = Run("""
            const scale = Scale.linear(1953, 1999, 0, 800);
            await Promise.resolve();
            scale(1968);
            """);

        Assert.False(result.Success);
        Assert.Contains("scale.map(value)", result.Error, StringComparison.Ordinal);
        Assert.Contains("unlike d3", result.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same call with no await, which is what already worked.
    /// </summary>
    /// <remarks>
    /// Paired with the test above deliberately. Together they say the gap was the <i>await</i> and
    /// not the hint — without this one, a regression that broke both paths would read as the hint
    /// itself having been wrong.
    /// </remarks>
    [Fact]
    public void TestTheSameCallWithoutAnAwaitIsExplainedIdentically()
    {
        var result = Run("""
            const scale = Scale.linear(1953, 1999, 0, 800);
            scale(1968);
            """);

        Assert.False(result.Success);
        Assert.Contains("scale.map(value)", result.Error, StringComparison.Ordinal);
    }

    /// <summary>The message itself survives, unwrapped from Jint's promise wording.</summary>
    /// <remarks>
    /// The wrapping defeats the matching as well as hiding it: every pattern in the engine is
    /// anchored at the start of the message, and the rejected text arrives behind
    /// "Promise was rejected with value TypeError: ".
    /// </remarks>
    [Fact]
    public void TestTheRejectedMessageIsUnwrappedRatherThanNested()
    {
        var result = Run("""
            await Promise.resolve();
            missingThing.title;
            """);

        Assert.False(result.Success);
        Assert.DoesNotContain("Promise was rejected with value", result.Error, StringComparison.Ordinal);
        Assert.Contains("missingThing", result.Error, StringComparison.Ordinal);
    }

    /// <summary>An awaited failure says which line it was on.</summary>
    /// <remarks>
    /// The second failure of the kubrick3 run was "Cannot read properties of undefined (reading
    /// 'title')" with no position, in a 566-line file — a message that names the mistake and gives
    /// no way to find it.
    /// </remarks>
    [Fact]
    public void TestAnAwaitedFailureCarriesItsLineNumber()
    {
        var result = Run("""
            const rows = [{ title: 'The Shining' }];
            await Promise.resolve();
            log('still fine');
            const missing = rows[7].title;
            """);

        Assert.False(result.Success);
        Assert.Contains("(line 4)", result.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rejection that is not an Error still reports something.
    /// </summary>
    /// <remarks>
    /// <c>Promise.reject('gave up')</c> carries no message of its own, so the exception's own text is
    /// the only account of it there is. Reporting nothing would be the worse failure — an empty
    /// error reads as a script that succeeded and drew nothing.
    /// </remarks>
    [Fact]
    public void TestANonErrorRejectionIsStillReported()
    {
        var result = Run("await Promise.reject('gave up');");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Contains("gave up", result.Error, StringComparison.Ordinal);
    }
    #endregion
}
