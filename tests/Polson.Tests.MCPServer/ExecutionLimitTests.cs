namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// What a script is told when it exceeds the sandbox's statement cap.
/// </summary>
/// <remarks>
/// A harness run lost three scripts to this limit and reported that the message named neither the
/// limit nor a way out. The cap is not the problem — an interpreter in a sandbox needs one — but a
/// failure that says only "the maximum number of statements executed have been reached" leaves the
/// author guessing at both how far over they were and what to do differently.
/// <para>
/// The overflow here is genuine rather than simulated with a lowered cap, because the enrichment
/// keys off Jint's own wording: if that wording ever changes, a test that fakes the exception would
/// still pass while the guidance silently stopped being attached.
/// </para>
/// </remarks>
public class ExecutionLimitTests : TestsRuntime
{
    #region Methods
    private static DrawingExecutionResult RunPastTheCap() =>
        new JsDrawingEngine().Execute("""
            let total = 0;
            for (let i = 0; i < 100000000; i++) { total += i; }
            total;
            """, 40, 40, null, "png", 100);
    #endregion

    #region Tests
    /// <summary>Exceeding the cap fails the script rather than truncating the loop.</summary>
    [Fact]
    public void TestExceedingTheStatementCapFailsTheScript()
    {
        var result = RunPastTheCap();

        Assert.False(result.Success);
        Assert.Contains("statements", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The failure names the limit, the consequence, and what to do instead.
    /// </summary>
    /// <remarks>
    /// Each of these carries something the bare message did not: the number, so "how far over?" is
    /// answerable; that nothing was kept, so the author does not go looking for partial output; and
    /// the native calls, so the remedy is a name rather than an exercise.
    /// </remarks>
    [Fact]
    public void TestTheFailureExplainsTheLimitAndTheRemedy()
    {
        var error = RunPastTheCap().Error ?? string.Empty;

        Assert.Contains(JsDrawingEngine.MaxStatements.ToString("N0"), error);
        Assert.Contains("abandoned", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stride", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bitmap.diff", error, StringComparison.Ordinal);
        Assert.Contains("Skia.Shader", error, StringComparison.Ordinal);
    }

    /// <summary>An ordinary error is passed through untouched.</summary>
    /// <remarks>
    /// The guidance is attached to one specific failure. Appending it to every error would bury the
    /// message that actually explains what went wrong.
    /// </remarks>
    [Fact]
    public void TestOrdinaryErrorsAreNotDecorated()
    {
        var result = new JsDrawingEngine().Execute("thisIsNotDefined();", 40, 40, null, "png", 100);

        Assert.False(result.Success);
        Assert.DoesNotContain("stride", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bitmap.diff", result.Error ?? string.Empty, StringComparison.Ordinal);
    }
    #endregion
}
