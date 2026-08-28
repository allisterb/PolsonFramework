namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A stage is the agent's declaration of what it is doing, and it has to span many executions —
/// that is what lets a viewer group every artifact produced while drafting under one heading.
/// <para>
/// The persistence is the load-bearing part and the easy thing to get wrong: it comes from
/// <see cref="SessionContext.Stage"/>, re-read on every call, not from the ambient log context. A
/// property pushed inside an async tool handler never propagates back to the dispatcher, so a stage
/// carried that way would silently vanish on the very next request.
/// </para>
/// </summary>
public class StageContextTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-stage-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public StageContextTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    private const string Draw = "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); c;";

    private JsonElement[] Events()
    {
        var file = Path.Combine(root, "events", "server.jsonl");
        if (!File.Exists(file)) return [];

        return File.ReadAllLines(file)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToArray();
    }

    private static string Type(JsonElement e) => e.GetProperty("type").GetString()!;

    private static string? Stage(JsonElement e) =>
        e.TryGetProperty("stage", out var s) ? s.GetString() : null;
    #endregion

    #region Persistence Tests
    /// <summary>
    /// The whole point: a stage declared in one execution tags the next one, which is a different
    /// async operation entirely.
    /// </summary>
    [Fact]
    public async Task TestStagePersistsIntoLaterExecutions()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Concept'); exit('ok');");
        await tools.ExecuteScript(Draw, 64, 64, outFile: "artifacts/a.webp");

        var render = Events().Single(e => Type(e) == "render");

        Assert.Equal("Concept", Stage(render));
    }

    /// <summary>Ending a stage stops the tagging; later work is untagged rather than stale.</summary>
    [Fact]
    public async Task TestEndingAStageStopsTagging()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Concept'); exit('ok');");
        await tools.ExecuteScript("Stage.end(); exit('ok');");
        await tools.ExecuteScript(Draw, 64, 64, outFile: "artifacts/a.webp");

        Assert.Null(Stage(Events().Single(e => Type(e) == "render")));
    }

    /// <summary>Beginning a stage while one is open closes it, so the record never overlaps two.</summary>
    [Fact]
    public async Task TestBeginningASecondStageClosesTheFirst()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Concept'); exit('ok');");
        await tools.ExecuteScript("Stage.begin('Refine'); exit('ok');");

        var ends = Events().Where(e => Type(e) == "stage.end").ToArray();

        Assert.Single(ends);
        Assert.Equal("Concept", Stage(ends[0]));
        Assert.Equal("superseded", ends[0].GetProperty("reason").GetString());
    }

    /// <summary>
    /// Re-declaring the current stage continues it rather than cycling end/begin. The first agent
    /// run declared the same stage at the top of three consecutive scripts, which chopped one stage
    /// into three and buried the real transitions.
    /// </summary>
    [Fact]
    public async Task TestRedeclaringTheCurrentStageContinuesIt()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Stress test'); exit('ok');");
        await tools.ExecuteScript("Stage.begin('Stress test'); exit('ok');");
        await tools.ExecuteScript("Stage.begin('Stress test'); exit('ok');");

        var types = Events().Select(Type).ToArray();

        Assert.Single(types, t => t == "stage.begin");
        Assert.Equal(2, types.Count(t => t == "stage.continue"));
        Assert.DoesNotContain(Events(), e => Type(e) == "stage.end");
    }

    /// <summary>A continuation is still tagged, so the stage keeps grouping the work.</summary>
    [Fact]
    public async Task TestContinuedStageStillTagsRenders()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Ideation'); exit('ok');");
        await tools.ExecuteScript("Stage.begin('Ideation'); exit('ok');");
        await tools.ExecuteScript(Draw, 64, 64, outFile: "artifacts/a.webp");

        Assert.Equal("Ideation", Stage(Events().Single(e => Type(e) == "render")));
    }

    /// <summary>Case alone must not fork the record; the first spelling is kept and returned.</summary>
    [Fact]
    public async Task TestCaseDifferenceContinuesRatherThanForking()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Stress test'); exit('ok');");
        var result = await tools.ExecuteScript("log('got=' + Stage.begin('STRESS TEST')); exit('ok');");

        Assert.Contains(result.Logs, l => l.Contains("got=Stress test", StringComparison.Ordinal));
        Assert.Single(Events(), e => Type(e) == "stage.begin");
    }

    /// <summary>A genuinely different stage still supersedes, so real transitions are unaffected.</summary>
    [Fact]
    public async Task TestDifferentStageStillSupersedes()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Ideation'); exit('ok');");
        await tools.ExecuteScript("Stage.begin('Ideation'); exit('ok');");
        await tools.ExecuteScript("Stage.begin('Archetype'); exit('ok');");

        var ends = Events().Where(e => Type(e) == "stage.end").ToArray();

        Assert.Single(ends);
        Assert.Equal("Ideation", Stage(ends[0]));
    }

    /// <summary>Reopening after an explicit end is a real begin, not a continuation.</summary>
    [Fact]
    public async Task TestReopeningAfterEndIsANewBegin()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Ideation'); Stage.end(); exit('ok');");
        await tools.ExecuteScript("Stage.begin('Ideation'); exit('ok');");

        var types = Events().Select(Type).ToArray();

        Assert.Equal(2, types.Count(t => t == "stage.begin"));
        Assert.DoesNotContain(Events(), e => Type(e) == "stage.continue");
    }

    /// <summary>A stage set mid-script tags that script's own completion, not just later ones.</summary>
    [Fact]
    public async Task TestStageSetMidScriptTagsThatScript()
    {
        await Tools().ExecuteScript("Stage.begin('Concept'); exit('ok');");

        Assert.Equal("Concept", Stage(Events().Single(e => Type(e) == "script.ok")));
    }

    /// <summary>Sessions do not share a stage — one agent declaring one must not tag another.</summary>
    [Fact]
    public void TestStageIsPerSession()
    {
        var registry = new SessionRegistry();
        var a = registry.GetOrCreate("session-a");
        var b = registry.GetOrCreate("session-b");

        a.Stage = "Concept";

        Assert.Null(b.Stage);
    }

    [Fact]
    public async Task TestCurrentReportsTheStageInEffect()
    {
        var tools = Tools();
        await tools.ExecuteScript("Stage.begin('Concept'); exit('ok');");
        var result = await tools.ExecuteScript("log('stage=' + Stage.current); exit('ok');");

        Assert.Contains(result.Logs, l => l.Contains("stage=Concept", StringComparison.Ordinal));
    }
    #endregion

    #region Execution Id Tests
    /// <summary>Every event from one call shares an execution id, so a render ties to its start.</summary>
    [Fact]
    public async Task TestOneCallSharesOneExecutionId()
    {
        await Tools().ExecuteScript(Draw, 64, 64, outFile: "artifacts/a.webp");

        var ids = Events().Select(e => e.GetProperty("execution").GetString()).Distinct().ToArray();

        Assert.Single(ids);
    }

    [Fact]
    public async Task TestSeparateCallsGetSeparateExecutionIds()
    {
        var tools = Tools();
        await tools.ExecuteScript(Draw, 64, 64);
        await tools.ExecuteScript(Draw, 64, 64);

        var ids = Events().Select(e => e.GetProperty("execution").GetString()).Distinct().ToArray();

        Assert.Equal(2, ids.Length);
    }

    /// <summary>The id comes back to the agent, so it can cite a specific render to the director.</summary>
    [Fact]
    public async Task TestExecutionIdIsReturnedToTheCaller()
    {
        var result = await Tools().ExecuteScript(Draw, 64, 64, outFile: "artifacts/a.webp");

        Assert.False(string.IsNullOrWhiteSpace(result.ExecutionId));
        Assert.Equal(result.ExecutionId, Events().Single(e => Type(e) == "render").GetProperty("execution").GetString());
    }
    #endregion

    #region Note Tests
    [Fact]
    public async Task TestNoteIsRecordedUnderTheCurrentStage()
    {
        await Tools().ExecuteScript("Stage.begin('Concept'); Stage.note('the wing, not the bird'); exit('ok');");

        var note = Events().Single(e => Type(e) == "note");

        Assert.Equal("Concept", Stage(note));
        Assert.Equal("the wing, not the bird", note.GetProperty("message").GetString());
    }

    /// <summary>A note without a stage is still worth recording; it just carries no grouping.</summary>
    [Fact]
    public async Task TestNoteWithoutAStageIsStillRecorded()
    {
        await Tools().ExecuteScript("Stage.note('no stage declared'); exit('ok');");

        var note = Events().Single(e => Type(e) == "note");

        Assert.Null(Stage(note));
    }

    [Fact]
    public async Task TestEmptyNoteIsIgnored()
    {
        await Tools().ExecuteScript("Stage.note('   '); exit('ok');");

        Assert.DoesNotContain(Events(), e => Type(e) == "note");
    }
    #endregion

    #region Input Handling Tests
    /// <summary>
    /// A stage name can be influenced by a client brief the agent read, so it gets the same
    /// treatment as any other text from outside: no controls, no invisible characters, bounded.
    /// </summary>
    [Fact]
    public async Task TestStageNameIsStripped()
    {
        await Tools().ExecuteScript("Stage.begin('Con\\u202Ecept\\u200B One\\nTwo'); exit('ok');");

        var begun = Stage(Events().Single(e => Type(e) == "stage.begin"))!;

        Assert.Equal("Concept OneTwo", begun);
    }

    [Fact]
    public async Task TestOverlongStageNameIsTruncated()
    {
        await Tools().ExecuteScript("Stage.begin('x'.repeat(500)); exit('ok');");

        var begun = Stage(Events().Single(e => Type(e) == "stage.begin"))!;

        Assert.True(begun.Length < 200, $"name was not truncated ({begun.Length} chars)");
    }

    /// <summary>An unnamed stage is a mistake worth surfacing, not a stage called nothing.</summary>
    [Theory]
    [InlineData("''")]
    [InlineData("'   '")]
    public async Task TestEmptyStageNameFails(string literal)
    {
        var result = await Tools().ExecuteScript($"Stage.begin({literal});");

        Assert.False(result.Success);
    }

    /// <summary>Ending when nothing is open is harmless, so a script need not track whether one is.</summary>
    [Fact]
    public async Task TestEndingWithNoStageOpenIsHarmless()
    {
        var result = await Tools().ExecuteScript("Stage.end(); Stage.end(); exit('ok');");

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain(Events(), e => Type(e) == "stage.end");
    }
    #endregion

    #region Disabled Log Tests
    /// <summary>
    /// Outside a project there is nothing to record into, but the global must still exist and work —
    /// a script written for a project should not break when run ad hoc.
    /// </summary>
    [Fact]
    public async Task TestStageWorksWithoutAProjectDirectory()
    {
        var result = await new DrawingMcpTools().ExecuteScript(
            "Stage.begin('Concept'); Stage.note('note'); log('stage=' + Stage.current); Stage.end(); exit('ok');");

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.Contains("stage=Concept", StringComparison.Ordinal));
        Assert.Empty(Events());
    }
    #endregion
}
