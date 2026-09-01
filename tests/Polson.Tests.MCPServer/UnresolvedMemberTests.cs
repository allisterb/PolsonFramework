namespace Polson.Tests.MCPServer;

using System;
using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A misspelled member must be an error, not a new property.
/// </summary>
/// <remarks>
/// Jint's default is to let a script invent members on a wrapped .NET object. Assigning
/// <c>ctx.fillStlye</c> created a JS-side property, the fill stayed black, and nothing anywhere said
/// so — the script had to be re-rendered and read by eye before the typo could even be suspected.
/// The same mechanism made <c>canvas.width = 999</c> read back as <c>999</c> on a canvas still 16
/// wide, and let a brush preset report a colour it would never draw.
/// <para>
/// Closed with <c>Options.Interop.ThrowOnUnresolvedMember</c>, plus a member accessor that exempts the
/// few names the JS runtime probes on its own — without which <c>await</c> throws on every .NET
/// result, because awaiting reads <c>then</c> to test for a thenable. Both halves are tested here;
/// the exemption half has no other coverage and its absence broke asset requisition outright.
/// </para>
/// </remarks>
public class UnresolvedMemberTests : TestsRuntime
{
    #region Tests
    /// <summary>The original defect: a typo'd assignment that silently did nothing.</summary>
    [Fact]
    public void TestAMisspelledPropertyAssignmentFails()
    {
        var result = Run("const x = createCanvas(20,20).getContext('2d'); x.fillStlye = '#ff0000';");

        Assert.False(result.Success);
        Assert.Contains("fillStlye", result.Error);
        Assert.Contains("fillStyle", result.Error);   // the suggestion is the point
    }

    [Fact]
    public void TestAMisspelledPropertyReadFails()
    {
        var result = Run("const x = createCanvas(20,20).getContext('2d'); log('' + x.lineWidht);");

        Assert.False(result.Success);
        Assert.Contains("lineWidth", result.Error);
    }

    /// <summary>
    /// Assigning to a real but read-only member says so, rather than claiming the member is missing.
    /// </summary>
    [Fact]
    public void TestAssigningToAReadOnlyMemberSaysItIsReadOnly()
    {
        var result = Run("const c = createCanvas(20,20); c.width = 999;");

        Assert.False(result.Success);
        Assert.Contains("read-only", result.Error);
        Assert.DoesNotContain("has no property or method", result.Error);
    }

    /// <summary>A suggestion must never name something the reference does not document.</summary>
    [Fact]
    public void TestSuggestionsExcludeClrInfrastructure()
    {
        var result = Run("const b = createCanvas(20,20).toBitmap(); b.getPixle(1,1);");

        Assert.False(result.Success);
        Assert.Contains("getPixel", result.Error);
        Assert.DoesNotContain("getType", result.Error);
    }

    /// <summary>
    /// A name extended at the end is the commonest real mistyping, and a length window rejects it.
    /// </summary>
    /// <remarks>
    /// From a live run: the script asked for <c>perlinNoiseFractalNoise</c>. The correct
    /// <c>perlinNoiseFractal</c> is five characters shorter, so the window excluded it, while
    /// <c>perlinNoiseTurbulence</c> — a different function entirely — fell inside and was suggested
    /// instead. A confidently wrong suggestion is worse than none.
    /// </remarks>
    [Fact]
    public void TestAnExtendedNameSuggestsTheNameItExtends()
    {
        var result = Run("Skia.Shader.perlinNoiseFractalNoise(0.4, 0.4, 4, 7);");

        Assert.False(result.Success);
        Assert.Contains("'perlinNoiseFractal'", result.Error);

        // And it comes first: the correct answer must not be buried behind a plausible wrong one.
        var suggestions = result.Error[result.Error.IndexOf("Did you mean", StringComparison.Ordinal)..];
        Assert.True(suggestions.IndexOf("'perlinNoiseFractal'", StringComparison.Ordinal)
                  < suggestions.IndexOf("perlinNoiseTurbulence", StringComparison.Ordinal),
            "the name that was extended should be suggested before a different function: " + suggestions);
    }

    /// <summary>The longest shared prefix wins, so the nearest of several plausible names leads.</summary>
    [Fact]
    public void TestTheClosestOfSeveralCandidatesIsSuggestedFirst()
    {
        var result = Run("createCanvas(9, 9).getContext('2d').fillStlye = '#f00';");

        Assert.False(result.Success);
        var suggestions = result.Error[result.Error.IndexOf("Did you mean", StringComparison.Ordinal)..];
        Assert.True(suggestions.IndexOf("'fillStyle'", StringComparison.Ordinal)
                  < suggestions.IndexOf("'fillRect'", StringComparison.Ordinal),
            "fillStyle diverges latest from fillStlye and should lead: " + suggestions);
    }

    /// <summary>
    /// A name carried in from another library has no near miss on the receiver it was aimed at, and
    /// the useful answer is on a different one.
    /// </summary>
    /// <remarks>
    /// <c>Skia.RuntimeEffect.make(...)</c> is CanvasKit's spelling. A live run wrote it, and the
    /// message said only that it did not exist — true, and no help. The surface does have
    /// <c>Skia.ColorFilter.runtimeEffect</c>.
    /// </remarks>
    [Fact]
    public void TestAForeignApiNameIsPointedAtTheRealOne()
    {
        var result = Run("Skia.RuntimeEffect.make('half4 main(float2 c) { return half4(1); }');");

        Assert.False(result.Success);
        Assert.Contains("elsewhere on the surface", result.Error);
        Assert.Contains("Skia.ColorFilter.runtimeEffect", result.Error);
    }

    /// <summary>No near miss is not a reason to say the same thing twice.</summary>
    [Fact]
    public void TestAnUnrecognisableMemberStillExplainsItself()
    {
        var result = Run("const x = createCanvas(20,20).getContext('2d'); x.zzzzzz();");

        Assert.False(result.Success);
        Assert.Contains("has no property or method", result.Error);
        Assert.DoesNotContain("Did you mean", result.Error);
    }

    /// <summary>Strict mode: a typo'd variable is an error rather than a new global.</summary>
    [Fact]
    public void TestAnUndeclaredAssignmentFails()
    {
        var result = Run("const c = createCanvas(20,20); resutl = 5;");

        Assert.False(result.Success);
        Assert.Contains("resutl", result.Error);
    }
    #endregion

    #region Tests (what must keep working)
    /// <summary>
    /// <c>await</c> reads <c>then</c> on whatever it resolves. Without the protocol exemption this
    /// throws on every .NET result and requisition stops working entirely.
    /// </summary>
    [Fact]
    public void TestAwaitOnADotNetResultStillWorks()
    {
        // Refused by the classifier before any network call, so this costs nothing to run.
        var result = Run("const r = await Assets.material('a wooden pirate ship'); log(r.failureName);");

        Assert.True(result.Success, result.Error);
        Assert.Contains("RefusedFormRequest", result.Logs[0]);
    }

    /// <summary>The exemption must not shadow a real member of the same name.</summary>
    [Fact]
    public void TestToStringIsStillTheRealMethod()
    {
        var result = Run("const p = Snap(40,40); p.circle(20,20,8); log('' + p.toString().length);");

        Assert.True(result.Success, result.Error);
        Assert.True(int.Parse(result.Logs[0].Replace("[LOG] ", "")) > 50);
    }

    [Fact]
    public void TestJsonStringifyStillWorks()
    {
        var result = Run("const c = createCanvas(20,20); log(JSON.stringify({ w: c.width }));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("{\"w\":20}", result.Logs[0]);
    }

    /// <summary>Plain JS objects keep ordinary JS semantics; this is an interop rule only.</summary>
    [Fact]
    public void TestPlainJavaScriptObjectsAreUnaffected()
    {
        var result = Run("""
            const o = {};
            o.anything = 1;
            log('missing=' + o.nope + ' set=' + o.anything + ' session=' + Session.neverSet);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("missing=undefined set=1 session=undefined", result.Logs[0]);
    }

    /// <summary>Every manual's runnable example still executes; they are the compatibility corpus.</summary>
    [Fact]
    public void TestTheManualCorpusStillRuns()
    {
        var failures = PolsonManuals.All
            .SelectMany(m => System.Text.RegularExpressions.Regex
                .Matches(m.Body, @"^```javascript\r?\n(.*?)^```",
                    System.Text.RegularExpressions.RegexOptions.Multiline |
                    System.Text.RegularExpressions.RegexOptions.Singleline)
                .Select(x => (Manual: m.Id, Script: x.Groups[1].Value)))
            .Select(x => (x.Manual, Result: new JsDrawingEngine().Execute(x.Script, 900, 700)))
            .Where(x => !x.Result.Success)
            .Select(x => $"manual {x.Manual}: {x.Result.Error}")
            .ToArray();

        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }
    #endregion

    #region Methods (private)
    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 20, 20, null, "png", 90);
    #endregion
}
