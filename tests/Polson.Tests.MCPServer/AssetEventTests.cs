namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.ExtendedMind.ImageGeneration;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Requisition was the only thing an agent could do that left no trace at all.
/// </summary>
/// <remarks>
/// <c>docs/project-layout.md</c> specified <c>asset.requisition</c>, <c>asset.refused</c> and
/// <c>budget</c>, and none were written — so the first question anyone asks of a piece that used
/// generation ("what was generated, and what was drawn?") could only be answered by reading the cache
/// directory and the scripts, which is exactly what a provenance record exists to avoid.
/// <para>
/// The success path needs a live image service, so what is proven here is everything reachable
/// offline: a classifier refusal, a requisition that could not be attempted, the budget snapshot, and
/// silence when nothing was requisitioned. The record's <i>shape</i> for a successful requisition is
/// covered against <see cref="RequisitionScope"/> directly, which needs no network.
/// </para>
/// </remarks>
[Collection(AssetsCollection.Name)]
public class AssetEventTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-assetev-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public AssetEventTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>
    /// A refusal is its own event: nothing was reached, nothing was spent, and the remedy is to
    /// reword rather than retry.
    /// </summary>
    [Fact]
    public async Task TestAFormRequestIsRecordedAsRefused()
    {
        await Run("await Assets.material('a wooden pirate ship at sea');");

        var e = Single("asset.refused");
        Assert.Equal("material", e.GetProperty("kind").GetString());
        Assert.Equal("a wooden pirate ship at sea", e.GetProperty("descriptor").GetString());
        Assert.Contains("'ship' names a thing", e.GetProperty("reason").GetString());

        // Not a requisition. Conflating the two would make a run that reworded its descriptors read
        // like one the service kept turning away.
        Assert.Empty(Events("asset.requisition"));
    }

    /// <summary>An attempt that reached the acquisition path is a requisition, successful or not.</summary>
    [Fact]
    public async Task TestAnAttemptedRequisitionIsRecordedWithItsFailure()
    {
        await Run("await Assets.material('weathered oak planking, deep grain');");

        var e = Single("asset.requisition");
        Assert.False(e.GetProperty("success").GetBoolean());
        Assert.Equal("NotConfigured", e.GetProperty("failure").GetString());
        Assert.False(e.GetProperty("fromCache").GetBoolean());
        Assert.Equal("weathered oak planking, deep grain", e.GetProperty("descriptor").GetString());
    }

    /// <summary>The descriptor is recorded as the script wrote it, not as the elaborated prompt.</summary>
    [Fact]
    public async Task TestTheDescriptorIsTheAgentsWordsNotThePrompt()
    {
        await Run("await Assets.material('rusted corrugated iron sheeting');");

        var descriptor = Single("asset.requisition").GetProperty("descriptor").GetString();
        Assert.Equal("rusted corrugated iron sheeting", descriptor);
        Assert.DoesNotContain("FLAT TEXTURE SAMPLE", descriptor);
    }

    /// <summary>
    /// The budget snapshot comes from the toolkit that ran, which is not always the one configured:
    /// the engine substitutes a disabled instance of its own when none is.
    /// </summary>
    [Fact]
    public async Task TestTheBudgetSnapshotComesFromTheToolkitThatRan()
    {
        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            null, new RequisitionCache(Path.GetTempPath()), new AssetBudget(7), "test");

        await Run("await Assets.material('brushed stainless steel');");

        var e = Single("budget");
        Assert.Equal(7, e.GetProperty("total").GetInt32());
        Assert.Equal(7, e.GetProperty("remaining").GetInt32());
        Assert.Equal(0, e.GetProperty("spent").GetInt32());
    }

    /// <summary>A script that requisitioned nothing writes none of these, and the silence is honest.</summary>
    [Fact]
    public async Task TestDrawingWithoutRequisitioningWritesNothing()
    {
        await Run("const c = createCanvas(20,20); c.getContext('2d').fillRect(0,0,20,20); c;");

        Assert.Empty(Events("asset.requisition"));
        Assert.Empty(Events("asset.refused"));
        Assert.Empty(Events("budget"));
    }

    /// <summary>Every requisition is recorded, not just the last, and refusals interleave with attempts.</summary>
    [Fact]
    public async Task TestEveryRequisitionInAScriptIsRecorded()
    {
        await Run("""
            await Assets.material('a wooden pirate ship');
            await Assets.material('frayed hemp rope fibre');
            await Assets.material('a logo for a coffee shop');
            """);

        Assert.Equal(2, Events("asset.refused").Length);
        Assert.Single(Events("asset.requisition"));
        Assert.Single(Events("budget"));   // one snapshot per execution, not one per call
    }
    #endregion

    #region Tests — the record's shape, without a network
    /// <summary>
    /// A cache hit is a success that cost nothing, and the two facts are separate fields because a
    /// reader needs both: what a run produced, and what it paid for.
    /// </summary>
    [Fact]
    public void TestACachedRequisitionIsSuccessfulAndFree()
    {
        using var scope = RequisitionScope.Begin();

        RequisitionScope.Record(new RequisitionRecord(
            "material", "weathered oak planking", Success: true, Failure: null, Reason: null,
            Model: "gemini-2.5-flash-image", FromCache: true, Refused: false));

        var record = Assert.Single(scope.Records);
        Assert.True(record.Success);
        Assert.True(record.FromCache);
        Assert.False(record.Refused);
    }

    /// <summary>A loop over cached descriptors cannot turn one execution into an unbounded record.</summary>
    [Fact]
    public void TestRecordsAreCappedAndTheCapIsCounted()
    {
        using var scope = RequisitionScope.Begin();

        for (var i = 0; i < 100; i++)
        {
            RequisitionScope.Record(new RequisitionRecord(
                "material", "swatch " + i, true, null, null, "m", true, false));
        }

        Assert.Equal(64, scope.Records.Count);
        Assert.Equal(36, scope.Dropped);
    }

    /// <summary>Outside a scope every call is inert, so the toolkit never depends on one existing.</summary>
    [Fact]
    public void TestRecordingOutsideAScopeIsHarmless()
    {
        RequisitionScope.Record(new RequisitionRecord("material", "x", true, null, null, null, false, false));
        RequisitionScope.RecordBudget(new BudgetSnapshot(1, 0, 1, 0, 0));

        Assert.Null(RequisitionScope.Current);
    }
    #endregion

    #region Methods (private)
    private Task<DrawingExecutionResult> Run(string script) =>
        new DrawingMcpTools(null, null, null, root).ExecuteScript(
            script + " const _c = createCanvas(20,20); _c.getContext('2d').fillRect(0,0,20,20); _c;", 20, 20);

    private JsonElement[] Events(string type) =>
        File.ReadAllLines(Path.Combine(root, "events", "server.jsonl"))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Where(e => e.GetProperty("type").GetString() == type)
            .ToArray();

    private JsonElement Single(string type) => Assert.Single(Events(type));
    #endregion
}

/// <summary>
/// Serialises the classes that install a requisition toolkit against each other.
/// </summary>
/// <remarks>
/// <b>Every one of them writes <c>JsDrawingEngine.Assets</c>, which is a static, and xUnit runs test
/// classes in parallel unless told otherwise.</b> So one class could install a disabled toolkit while
/// another was mid-assertion against a configured one, or null it out in <c>Dispose</c> underneath a
/// run in progress — producing a failure in whichever class happened to lose the race, in a suite
/// that passed on the next attempt.
/// <para>
/// Latent for as long as three classes shared the static and rare enough to look like noise; adding a
/// fourth that installs a <i>working</i> generator, where the others install a null one, made it
/// surface at roughly one run in three. Same reasoning as <c>ResearchCollection</c>.
/// </para>
/// </remarks>
[CollectionDefinition(Name)]
public sealed class AssetsCollection
{
    public const string Name = "Assets";
}
