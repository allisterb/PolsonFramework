namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Asking whether a call exists, before committing to it.
/// </summary>
/// <remarks>
/// <para>
/// A live <c>comic_studio</c> run spent <b>eighteen of its seventy-one renders</b> on probe scripts,
/// because there was no way to ask. Resolution is strict — a member that is not there throws rather
/// than reading as <c>undefined</c> — so an agent could either write a call it could not verify into
/// a 50 KB script, or delete one candidate name at a time and re-run. Its own report named the fix.
/// </para>
/// <para>
/// <c>typeof</c> answers now too — an engine that killed a script for asking was a trap
/// every agent had to learn once. These remain the sharper tools: they answer for the <em>documented</em>
/// surface rather than raw reflection, <c>in</c> is unreliable, and <c>suggest</c> can name the
/// alternative. What leniency costs
/// is that a misspelled <em>read</em> is silent, and what pays for it is that every one is recorded.
/// </para>
/// </remarks>
public class HasProbeTests : TestsRuntime
{
    #region The probe
    [Fact]
    public void TestHasFindsARealMember()
    {
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); log('' + has(ctx, 'fillRect'));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("True", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestHasReportsAMissingMemberWithoutThrowing()
    {
        // The whole point: asking must be safe. Touching the same name is still an error.
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); " +
                         "log('' + has(ctx, 'drawLoomisWireframe'));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("False", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The case the run actually hit: two names, one real, chosen without a failed script.</summary>
    [Fact]
    public void TestAScriptCanBranchOnWhatExists()
    {
        var result = Run("""
            const ctx = createCanvas(20,20).getContext('2d');
            const call = has(ctx, 'drawMannequinWireframe') ? 'drawMannequinWireframe'
                       : has(ctx, 'drawMannequin') ? 'drawMannequin'
                       : 'neither';
            log(call);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("drawMannequin", string.Join(" ", result.Logs), StringComparison.Ordinal);
        Assert.DoesNotContain("Wireframe", string.Join(" ", result.Logs), StringComparison.Ordinal);
    }

    /// <summary>Works on a namespace global too, which is where a foreign API name usually lands.</summary>
    /// <remarks>
    /// `Skia.ColorFilter` is real and `Skia.Effect` is not. Note that `Skia.RuntimeEffect` is also
    /// absent — it lives on `Skia.ColorFilter`, which is exactly the kind of near-miss `suggest`
    /// exists to resolve.
    /// </remarks>
    [Fact]
    public void TestHasWorksOnTheToolkitNamespaces()
    {
        var result = Run("log(has(Skia, 'ColorFilter') + ',' + has(Skia, 'Effect'));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("True,False", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A `Session` key is a key, not a member — and the scratchpad is documented as dynamic.</summary>
    [Fact]
    public void TestHasAnswersForTheSessionScratchpad()
    {
        var result = Run("Session.palette = ['#000']; log(has(Session, 'palette') + ',' + has(Session, 'nope'));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("True,False", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>It answers for the documented surface, not for raw CLR reflection.</summary>
    /// <remarks>
    /// A probe exists to decide what to write, so it must not point at something no script should
    /// call. These are excluded from the symbol manifest and from the "did you mean" suggester too,
    /// and all three agreeing is the point.
    /// </remarks>
    [Fact]
    public void TestHasHidesWhatTheReferenceDoesNotDocument()
    {
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); " +
                         "log(has(ctx, 'getType') + ',' + has(ctx, 'GetHashCode'));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("False,False", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>
    /// A false answer is followed by advice, without having to fail first.
    /// </summary>
    /// <remarks>
    /// `has` alone leaves a script knowing it guessed wrong and not what to write instead — which is
    /// the useful half of the failed access. The suggester was previously reachable only by throwing,
    /// so the cheap way to get advice was to make a mistake.
    /// </remarks>
    [Fact]
    public void TestSuggestNamesTheRealCallWithoutThrowing()
    {
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); " +
                         "log(suggest(ctx, 'drawMannequinWireframe'));");

        Assert.True(result.Success, result.Error);

        var said = string.Join(" ", result.Logs);
        Assert.Contains("drawMannequin", said, StringComparison.Ordinal);
    }

    /// <summary>Asking about a name that is already right says so, rather than saying nothing.</summary>
    /// <remarks>
    /// A caller reaching for advice about a correct name is looking in the wrong place for its bug,
    /// and an empty answer would let it go on looking.
    /// </remarks>
    [Fact]
    public void TestSuggestSaysWhenTheNameIsAlreadyCorrect()
    {
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); log(suggest(ctx, 'fillRect'));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("exists", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The probe and the throw give the same advice, because they share one explainer.</summary>
    [Fact]
    public void TestSuggestAgreesWithTheThrownMessage()
    {
        var asked = Run("const ctx = createCanvas(20,20).getContext('2d'); log(suggest(ctx, 'lineWidht'));");
        var thrown = Run("const ctx = createCanvas(20,20).getContext('2d'); ctx.lineWidht = 3;");

        Assert.True(asked.Success, asked.Error);
        Assert.False(thrown.Success);

        Assert.Contains("lineWidth", string.Join(" ", asked.Logs), StringComparison.Ordinal);
        Assert.Contains("lineWidth", thrown.Error ?? "", StringComparison.Ordinal);
    }
    #endregion

    #region The ordinary idioms work too
    /// <summary>
    /// Every ordinary way of asking answers, rather than killing the script for asking.
    /// </summary>
    /// <remarks>
    /// These used to throw, and an agent had to discover that the expensive way — one measured run
    /// spent 18 of its 71 renders deleting a candidate name and re-running to find out whether it
    /// existed. `has(...)` and `suggest(...)` remain, because they answer for the *documented*
    /// surface and can name the alternative; but no one should have to learn that `typeof` is a trap
    /// before they can use them.
    /// </remarks>
    [Theory]
    [InlineData("typeof ctx.drawLoomisWireframe === 'undefined'")]
    [InlineData("ctx.drawLoomisWireframe === undefined")]
    [InlineData("ctx.drawLoomisWireframe?.name === undefined")]
    public void TestTheJavaScriptIdiomsAnswerForAMissingMember(string probe)
    {
        var result = Run($"const ctx = createCanvas(20,20).getContext('2d'); log('' + ({probe}));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("true", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>...and still say yes for one that is there.</summary>
    [Theory]
    [InlineData("typeof ctx.fillRect === 'function'")]
    [InlineData("ctx.fillRect !== undefined")]
    public void TestTheJavaScriptIdiomsStillFindARealMember(string probe)
    {
        var result = Run($"const ctx = createCanvas(20,20).getContext('2d'); log('' + ({probe}));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("true", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// `in` reports every name as present, which is the price of `typeof` working.
    /// </summary>
    /// <remarks>
    /// The member accessor is a <em>value provider</em>: it can decline to handle a name, in which
    /// case .NET semantics apply and an unknown member throws, or it can answer with a value. There
    /// is no third answer meaning "absent", because .NET has no such state to report — a type's
    /// members are fixed. So answering `undefined` to give JavaScript back its own semantics also
    /// asserts that the property <em>exists</em> and holds `undefined`, and `in` reports that
    /// faithfully. `typeof` reads the value and is right; `in` asks about existence and cannot be.
    /// <para>
    /// Pinned rather than worked around. Overriding `HasProperty` on a custom wrapper would make it
    /// contradict the descriptors the same object hands out, and nothing incorrect follows from it as
    /// it stands: acting on the answer reaches a call, and a call still refuses with a suggestion.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestTheInOperatorReportsEveryNameAsPresent()
    {
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); log('' + ('nope' in ctx));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("true", string.Join(" ", result.Logs), StringComparison.OrdinalIgnoreCase);

        // ...while the idioms the docs do recommend get it right.
        var honest = Run("const ctx = createCanvas(20,20).getContext('2d'); " +
                         "log(typeof ctx.nope + ',' + has(ctx, 'nope'));");
        Assert.Contains("undefined,False", string.Join(" ", honest.Logs), StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region What leniency costs, and what pays for it
    /// <summary>
    /// A misspelled *write* still throws. This is the case that cost real renders.
    /// </summary>
    /// <remarks>
    /// `ctx.fillStlye = 'red'` used to create a JS-side property, leave the fill black, and say
    /// nothing. Reads became lenient; writes did not, because Jint consults the member accessor on
    /// reads only — which is the whole reason both behaviours can coexist.
    /// </remarks>
    [Fact]
    public void TestAMisspelledWriteStillFailsLoudly()
    {
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); ctx.lineWidht = 3;");

        Assert.False(result.Success);
        Assert.Contains("lineWidth", result.Error ?? "", StringComparison.Ordinal);
    }

    /// <summary>Calling something that is not there still explains itself.</summary>
    [Fact]
    public void TestCallingAMissingMemberStillSuggests()
    {
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); ctx.drawLoomisWireframe();");

        Assert.False(result.Success);
        Assert.Contains("drawLoomisWireframe", result.Error ?? "", StringComparison.Ordinal);
        Assert.Contains("Did you mean", result.Error ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// A misspelled read is genuinely silent — and is recorded, which is what makes that acceptable.
    /// </summary>
    /// <remarks>
    /// `ctx.lineWidht * 2` is NaN and nothing stops it. The defence is seeing it rather than
    /// preventing it: every unresolved read lands in the run record as an `absent` probe, so an agent
    /// thrashing on names that do not exist shows up in the trace instead of merely being possible to
    /// notice. If this test ever fails, leniency has stopped paying for itself.
    /// </remarks>
    [Fact]
    public void TestAMisspelledReadIsSilentButRecorded()
    {
        using var probes = ProbeScope.Begin();
        var result = Run("const ctx = createCanvas(20,20).getContext('2d'); log('' + (ctx.lineWidht * 2));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("NaN", string.Join(" ", result.Logs), StringComparison.Ordinal);

        var absent = probes.Outcomes.Where(o => o.Kind == ProbeScope.Kinds.Absent).ToArray();
        Assert.NotEmpty(absent);
        Assert.Contains(absent, o => o.Summary.Contains("lineWidht", StringComparison.Ordinal));
    }
    #endregion

    #region Methods
    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 20, 20, null, "png", 90);
    #endregion
}
