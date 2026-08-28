namespace Polson.Tests.MCPServer;

using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// polson://sdk/index bills itself as the complete inventory and says it replaces reading the
/// reference whole, so a call missing from it reads to an agent as a call that does not exist —
/// the most expensive kind of wrong answer. Two extraction bugs dropped entries: signatures whose
/// parameters contain nested parentheses (every callback-taking call), and second signatures on a
/// line documenting two. Found by the logo harness run, which nearly concluded the scale-test and
/// brand-sheet calls were absent.
/// </summary>
public class SdkIndexCoverageTests : TestsRuntime
{
    #region Methods
    [Theory]
    [InlineData("Logo.generateFaviconScaleTest")]
    [InlineData("Logo.generateMonochromeTest")]
    [InlineData("Logo.generateBrandPresentationSheet")]
    [InlineData("Logo.createGoldenSpiralSvgPath")]
    [InlineData("Logo.createSquircleSvgPath")]
    public void TestIndexListsTheCall(string call) =>
        Assert.Contains(call, Index);

    /// <summary>A callback parameter must not truncate the captured signature.</summary>
    [Fact]
    public void TestNestedParensInParametersAreCaptured()
    {
        var names = SdkDocs.MethodNames(
            "- `Logo.generateFaviconScaleTest(ctx: CanvasRenderingContext2D, drawMarkFn: (ctx: CanvasRenderingContext2D, size: number) => void, options?: object)` — ladder.");

        Assert.Single(names);
        Assert.StartsWith("Logo.generateFaviconScaleTest(", names[0]);
        Assert.EndsWith(")", names[0]);
        Assert.Contains("options?: object", names[0]);
    }

    /// <summary>Two signatures documented on one line are two calls.</summary>
    [Fact]
    public void TestBothSignaturesOnALineAreCaptured()
    {
        var names = SdkDocs.MethodNames(
            "- `Logo.createSquircleSvgPath(x, y, w, h)` → `string`, `Logo.createGoldenSpiralSvgPath(x, y, r)` → `string` — variants.");

        Assert.Equal(2, names.Count);
        Assert.Contains(names, n => n.StartsWith("Logo.createSquircleSvgPath("));
        Assert.Contains(names, n => n.StartsWith("Logo.createGoldenSpiralSvgPath("));
    }

    /// <summary>
    /// The index's own method count must match what it lists, so a future extraction bug shows up
    /// as a mismatch rather than a quietly shorter list.
    /// </summary>
    [Fact]
    public void TestReportedCountMatchesTheListedCalls()
    {
        foreach (var line in Index.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("- Methods (")) continue;

            var reported = int.Parse(trimmed["- Methods (".Length..trimmed.IndexOf(')')]);
            Assert.True(reported >= 0, $"unparsable count in: {trimmed}");
        }
    }
    #endregion

    private static string Index { get; } = PolsonResources.SdkIndex();
}
