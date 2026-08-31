namespace Polson.Tests.MCPServer;

using System.IO;
using Polson.ExtendedMind.ImageGeneration;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A script names an enum with a string, which is what the SDK reference says it may do.
/// </summary>
/// <remarks>
/// <c>Assets.backdrop(desc, { keepQuiet: 'lowerThird' })</c> is documented exactly that way in
/// <c>docs/Polson.core.md</c> and died with
/// <c>Invalid cast from 'System.String' to '…QuietRegion'</c> — a .NET type the script author has
/// never heard of, about an argument the docs describe as a string.
/// <para>
/// Found by the `painting-2` run, which wanted the lower third kept dark for its galleon silhouette
/// and could only get a plate by dropping the argument. Binding happens before the method body, so
/// these reproduce with no generator configured and therefore no network call and no spend: the
/// failure is a cast before the fix and a clean <c>NotConfigured</c> after it.
/// </para>
/// </remarks>
public class EnumStringInteropTests : TestsRuntime
{
    #region Methods
    private static DrawingExecutionResult Run(string script)
    {
        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            null, new RequisitionCache(Path.GetTempPath()), new AssetBudget(1), "test");
        return new JsDrawingEngine().Execute(script);
    }
    #endregion

    #region Tests
    [Theory]
    [InlineData("lowerThird")]
    [InlineData("upperThird")]
    [InlineData("leftHalf")]
    [InlineData("rightHalf")]
    [InlineData("center")]
    [InlineData("none")]
    public void TestADocumentedQuietRegionStringBinds(string region)
    {
        var result = Run($"const p = await Assets.backdrop('a night sky', {{ keepQuiet: '{region}' }});"
                       + "log(p.failureName); p.failureName;");

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain("Invalid cast", result.Error ?? string.Empty);
        Assert.Contains("NotConfigured", string.Join(" ", result.Logs));
    }

    /// <summary>PascalCase works too: the docs use camelCase, the enum does not, and both are names
    /// for the same member.</summary>
    [Fact]
    public void TestTheEnumsOwnSpellingBindsAsWell()
    {
        var result = Run("const p = await Assets.backdrop('a night sky', { keepQuiet: 'LowerThird' });"
                       + "log(p.failureName);");

        Assert.True(result.Success, result.Error);
    }

    /// <summary>
    /// A name that is not a member is still refused rather than silently becoming the first one,
    /// which would be worse than the original crash: a plate quietly measured against the wrong
    /// region and reported as honoured.
    /// </summary>
    [Fact]
    public void TestAnUnknownRegionIsRefusedRatherThanGuessed()
    {
        var result = Run("await Assets.backdrop('a night sky', { keepQuiet: 'bottomBit' });");

        Assert.False(result.Success);
    }
    #endregion
}
