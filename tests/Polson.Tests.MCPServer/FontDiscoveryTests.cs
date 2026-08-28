namespace Polson.Tests.MCPServer;

using Polson.MCPServer;
using Xunit;

/// <summary>
/// Skia substitutes a default face for an unavailable family silently, and nothing on the surface
/// enumerated what was available — so an agent measuring text against a control string discovered
/// by elimination that most of the serifs it wanted did not exist, and picked a typeface it had not
/// chosen on merit. Skia.Font makes that discoverable up front.
/// </summary>
public class FontDiscoveryTests : TestsRuntime
{
    #region Methods
    [Fact]
    public void TestFamiliesEnumeratesInstalledFonts()
    {
        var result = Run("""
            const families = Skia.Font.families();
            log('count=' + families.length);
            log('hasArial=' + families.some(f => f === 'Arial'));
            """);

        Assert.True(result.Success, result.Error);
        var log = string.Join("\n", result.Logs);
        Assert.DoesNotContain("count=0", log);
        Assert.Contains("hasArial=True", log, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestHasDistinguishesRealFromMissingFamilies()
    {
        var result = Run("""
            log('arial=' + Skia.Font.has('Arial'));
            log('nonsense=' + Skia.Font.has('NoSuchFontXYZ'));
            """);

        Assert.True(result.Success, result.Error);
        var log = string.Join("\n", result.Logs);
        Assert.Contains("arial=true", log, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nonsense=false", log, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A fallback must be visible: resolve reports what would really be used.</summary>
    [Fact]
    public void TestResolveRevealsAFallback()
    {
        var result = Run("""
            const asked = 'NoSuchFontXYZ';
            const got = Skia.Font.resolve(asked);
            log('fellBack=' + (got !== asked));
            log('resolvedIsNonEmpty=' + (got.length > 0));
            """);

        Assert.True(result.Success, result.Error);
        var log = string.Join("\n", result.Logs);
        Assert.Contains("fellBack=true", log, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolvedIsNonEmpty=true", log, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every family the enumeration reports must resolve to itself.</summary>
    [Fact]
    public void TestEnumeratedFamiliesAllResolve()
    {
        var result = Run("""
            const families = Skia.Font.families();
            let bad = 0;
            for (let i = 0; i < families.length; i++) {
                if (!Skia.Font.has(families[i])) bad++;
            }
            log('unresolvable=' + bad);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("unresolvable=0", string.Join("\n", result.Logs));
    }
    #endregion

    private static DrawingExecutionResult Run(string script) =>
        new JsDrawingEngine().Execute(script, 64, 64, null, "png", 90);
}
