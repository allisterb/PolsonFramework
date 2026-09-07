namespace Polson.Tests.MCPServer;

using System.IO;
using Polson.ExtendedMind.ImageGeneration;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>Assets.classify(...)</c> crashed from a script with
/// <c>Unable to cast … List`1[System.String] … to type 'System.Array'</c>. Found by the Gemini logo
/// run, which classified a material and then an object in one session.
/// <para>
/// The cause was a JS-reachable property whose <em>runtime</em> type alternated: <c>Triggers</c> was
/// declared <c>IReadOnlyList&lt;string&gt;</c>, defaulted to <c>string[]</c> when nothing matched,
/// and held a <c>List&lt;string&gt;</c> when something did. Jint caches a member accessor and the
/// cache assumes a stable type, so the failure appeared only on the <em>third</em> call, after the
/// type had alternated once — which is why the direct C# classification tests never saw it.
/// </para>
/// <para>
/// These exercise it through Jint, because that is the only place the bug exists.
/// </para>
/// </summary>
[Collection(AssetsCollection.Name)]
public class ClassifyInteropTests : TestsRuntime
{
    #region Methods
    private static void ConfigureAssets() =>
        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            null, new RequisitionCache(Path.GetTempPath()), new AssetBudget(1), "test");

    private static DrawingExecutionResult Run(string script)
    {
        ConfigureAssets();
        return new JsDrawingEngine().Execute(script);
    }
    #endregion

    #region Alternating Verdict Tests
    /// <summary>
    /// The original failure, in order: a matching descriptor, then a non-matching one, then a
    /// matching one again. Anything less than three calls does not reproduce it.
    /// </summary>
    [Fact]
    public void TestAlternatingVerdictsSurviveRepeatedCalls()
    {
        var result = Run("""
            const a = Assets.classify('a romantic sailboat cruise ship');
            const b = Assets.classify('weathered oak planking');
            const c = Assets.classify('a wooden ship');
            const d = Assets.classify('coiled hemp rope fibre');
            log(`${a.triggers.length} ${b.triggers.length} ${c.triggers.length} ${d.triggers.length}`);
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("2 0 1 0", System.StringComparison.Ordinal));
    }

    /// <summary>The reverse order too, so the fix does not depend on which type is seen first.</summary>
    [Fact]
    public void TestNonMatchingFirstAlsoSurvives()
    {
        var result = Run("""
            Assets.classify('weathered oak planking');
            Assets.classify('a wooden ship');
            Assets.classify('rough limestone surface');
            const last = Assets.classify('a sailboat');
            log('triggers=' + last.triggers.length);
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("triggers=1", System.StringComparison.Ordinal));
    }
    #endregion

    #region Verdict Shape Tests
    /// <summary>Triggers must behave as a real JS array, not merely be readable.</summary>
    [Fact]
    public void TestTriggersIsARealJsArray()
    {
        var result = Run("""
            const v = Assets.classify('a wooden ship');
            log('isArray=' + Array.isArray(v.triggers) + ' joined=' + v.triggers.join(','));
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("isArray=true", System.StringComparison.Ordinal));
    }

    /// <summary>The readable verdict fields a script is documented to use.</summary>
    [Fact]
    public void TestVerdictExposesReadableFields()
    {
        var result = Run("""
            const v = Assets.classify('a wooden ship');
            log(`${v.className}|${v.allowed}|${v.reason.length > 0}`);
            exit('ok');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("Form|false|true", System.StringComparison.Ordinal));
    }
    #endregion
}
