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

    #region A failure no script can cause or fix
    /// <summary>
    /// A missing native library says why, not only which type failed to initialise.
    /// </summary>
    /// <remarks>
    /// A Linux run met exactly this: SkiaSharp's Linux native asset was not in the build, so the
    /// first drawing call threw <c>TypeInitializationException</c>, whose message names the type and
    /// stops. Returning only that message discarded the <c>DllNotFoundException</c> underneath, and
    /// the agent spent <em>27 tool calls and a compaction</em> investigating fonts, Snap versus
    /// Canvas2D, and the SDK documentation — every one of them unable to help, because the file was
    /// simply not on disk.
    /// <para>
    /// Constructed rather than provoked, unlike the statement-cap tests above: the enrichment keys
    /// off the exception <em>type</em> rather than any wording, so the type is the contract, and the
    /// alternative is a platform deliberately broken mid-test.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestAMissingNativeLibraryIsNamedRatherThanHidden()
    {
        var buried = new TypeInitializationException("SkiaSharp.SKImageInfo",
            new DllNotFoundException("Unable to load shared library 'libSkiaSharp'."));

        var error = JsDrawingEngine.Explain(buried);

        // The cause, which the outer message never carries.
        Assert.Contains("libSkiaSharp", error, StringComparison.Ordinal);

        // And that it is nobody's script's fault, so the next agent stops instead of hunting.
        Assert.Contains("native library", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("report it", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A script error says which line it happened on.
    /// </summary>
    /// <remarks>
    /// Jint records a position and we were discarding it, so every script failure arrived with no
    /// way to find it but re-reading the whole program. A live run lost a 97-line composition this
    /// way.
    /// </remarks>
    [Fact]
    public void TestAScriptErrorNamesItsLine()
    {
        var result = new JsDrawingEngine().Execute("""
            const a = 1;
            const b = 2;
            thisIsNotDefined();
            """, 40, 40, null, "png", 100);

        Assert.False(result.Success);
        Assert.Contains("line 3", result.Error ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// An argument Jint cannot bind says what that usually means, since its own message does not.
    /// </summary>
    /// <remarks>
    /// "No public methods with the specified arguments were found" names neither the method nor the
    /// argument. Its commonest cause by far is <c>undefined</c> from a property that does not exist:
    /// the run that prompted this used <c>rect.w</c>, where the toolkit's rectangles carry
    /// <c>width</c>, making the call <c>fillRect(x, y, undefined, undefined)</c>.
    /// </remarks>
    [Fact]
    public void TestAnUnbindableArgumentExplainsWhatUsuallyCausesIt()
    {
        var result = new JsDrawingEngine().Execute("""
            const r = Layout.rect(10, 10, 100, 50);
            const ctx = createCanvas(200, 100).getContext('2d');
            ctx.fillRect(r.x, r.y, r.w, r.h);
            """, 200, 100, null, "png", 100);

        var error = result.Error ?? string.Empty;

        Assert.False(result.Success);
        Assert.Contains("line 3", error, StringComparison.Ordinal);
        Assert.Contains("undefined", error, StringComparison.Ordinal);
        Assert.Contains("width and height", error, StringComparison.Ordinal);
    }

    /// <summary>The cause is found however deep it is buried.</summary>
    [Fact]
    public void TestTheLoadFailureIsFoundThroughNestedExceptions()
    {
        var nested = new InvalidOperationException("script failed",
            new TypeInitializationException("SkiaSharp.SKImageInfo",
                new DllNotFoundException("Unable to load shared library 'libSkiaSharp'.")));

        Assert.Contains("libSkiaSharp", JsDrawingEngine.Explain(nested), StringComparison.Ordinal);
    }

    /// <summary>
    /// An initializer that failed for some other reason still says which, rather than only which
    /// type — the outer message alone is never enough to act on.
    /// </summary>
    [Fact]
    public void TestAnyInitializerFailureReportsItsReason()
    {
        var other = new TypeInitializationException("Some.Type",
            new InvalidOperationException("a config value was missing"));

        var error = JsDrawingEngine.Explain(other);

        Assert.Contains("a config value was missing", error, StringComparison.Ordinal);
        Assert.DoesNotContain("native library", error, StringComparison.OrdinalIgnoreCase);
    }
    #endregion
}
