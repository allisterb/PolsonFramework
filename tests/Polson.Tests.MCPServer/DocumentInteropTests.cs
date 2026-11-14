namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Polson.ExtendedMind.DocumentProcessing;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// <c>Documents</c> as a script actually reaches it.
/// </summary>
/// <remarks>
/// <para>
/// The C# tests cover the decisions; this covers the boundary, which is where a surface is usually
/// broken in practice: registered under the wrong name, returning an object whose members do not
/// resolve in camelCase, or — the one that matters most — <b>absent</b>, so a script asking for it
/// gets a <c>ReferenceError</c> rather than a refusal it can act on.
/// </para>
/// <para>
/// That last case is why the global is registered even with no key. An agent told "Documents is not
/// defined" has no route but to answer from recall and present it as sourced; one told "not
/// configured, source the figures another way and say where they came from" has one.
/// </para>
/// </remarks>
[Collection(AssetsCollection.Name)]
public class DocumentInteropTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-docint-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public DocumentInteropTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        JsDrawingEngine.Documents = null;
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>The global exists with no key, and refuses in words rather than by being absent.</summary>
    [Fact]
    public async Task TestAnUnconfiguredSurfaceRefusesRatherThanBeingAbsent()
    {
        var result = await Run("""
            const a = await Documents.ask('records.pdf', 'total gross');
            log(typeof Documents + '|' + a.success + '|' + a.failureName + '|' + (a.remedy.length > 0));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("object|false|NotConfigured|true", Logged(result));
    }

    /// <summary>Containment reaches the script as a value, not as a thrown error.</summary>
    /// <remarks>
    /// This is the regression the C# suite found: <c>ProjectPath.Resolve</c> throws, and without
    /// translation a path traversal was the one failure that killed the script instead of being
    /// reported. A script branching on <c>answer.success</c> would never have run its own handler.
    /// </remarks>
    [Fact]
    public async Task TestAPathEscapeIsReturnedRatherThanThrown()
    {
        JsDrawingEngine.Documents = new DocumentProcessor("k", new DocumentBudget(3), root);

        var result = await Run("""
            const a = await Documents.ask('../escaped.pdf', 'anything');
            log(a.success + '|' + a.failureName + '|' + String(a.error.indexOf('outside this project') > 0));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("false|NotFound|true", Logged(result));
    }

    /// <summary>The budget is readable, so an agent can plan instead of hitting a wall.</summary>
    [Fact]
    public async Task TestTheBudgetIsReadableFromAScript()
    {
        JsDrawingEngine.Documents = new DocumentProcessor("k", new DocumentBudget(7), root);

        var result = await Run("""
            const b = Documents.budget;
            log(b.total + '|' + b.remaining + '|' + b.spent + '|' + String(b.canAfford(7)));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("7|7|0|true", Logged(result));
    }

    /// <summary>An options literal binds, so a mimeType override is usable from JS.</summary>
    /// <remarks>
    /// The same binding assumption as <c>MatteOptions</c>: nothing maps a JS object onto the record,
    /// Jint does it, and "it worked for the last one" is not a test.
    /// </remarks>
    [Fact]
    public async Task TestAnOptionsLiteralBinds()
    {
        JsDrawingEngine.Documents = new DocumentProcessor("k", new DocumentBudget(3), root);

        var result = await Run("""
            // Bytes with an explicit type get past the type check and fail later, at the transport.
            // Without binding they would be refused as UnsupportedType, naming mimeType.
            const a = await Documents.ask([1,2,3], 'read it', { mimeType: 'application/pdf' });
            log(a.failureName);
            """);

        Assert.True(result.Success, result.Error);
        Assert.NotEqual("UnsupportedType", Logged(result));
    }

    /// <summary>A <c>pages</c> literal binds: a list that is not one is refused, which only a bound option can be.</summary>
    [Fact]
    public async Task TestAPagesLiteralBinds()
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Revenue was 215,938.");
        JsDrawingEngine.Documents = new Answering(new DocumentBudget(5), root, new DocumentCache(Path.Combine(root, "cache")));

        var result = await Run("""
            const bad = await Documents.ask('notes.txt', 'What was revenue?', { pages: 'three to five' });
            const good = await Documents.ask('notes.txt', 'What was revenue?', { pages: '1' });
            log(bad.failureName + '|' + good.success);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("InvalidRequest|true", Logged(result));
    }

    /// <summary>
    /// Jint 4.9.2: after an await on a host call, a parameter default that reads a property throws a bare
    /// NullReferenceException inside the engine. anthropic_s_1_outside_cc hit it three times with nothing to say which
    /// line; the error now names the pattern and the way round it. When an upgraded Jint fixes it, this test says so.
    /// </summary>
    [Fact]
    public async Task TestAParameterDefaultReadingAPropertyAfterAHostAwaitIsExplained()
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Revenue was 215,938.");
        JsDrawingEngine.Documents = new AnsweringLater(new DocumentBudget(5), root, new DocumentCache(Path.Combine(root, "cache")));

        var result = await Run("""
            const T = { figure: 104 };
            function f(g, size = T.figure) { return size; }
            await Documents.ask('notes.txt', 'What was revenue?');
            log('got ' + f(null));
            """);

        Assert.False(result.Success, "the engine no longer fails here: remove the explanation in JsDrawingEngine.Explain");
        Assert.Contains("Read the default inside the body", result.Error);
    }

    /// <summary><c>Documents.find</c> reaches a script, awaited like <c>ask</c>, and its hits read as an array.</summary>
    [Fact]
    public async Task TestFindIsReachableFromAScript()
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Revenue was 215,938.");
        JsDrawingEngine.Documents = new DocumentProcessor("k", new DocumentBudget(5), root);

        var result = await Run("""
            const f = await Documents.find('notes.txt', 'revenue');
            log(f.success + '|' + f.hits.length + '|' + f.hits[0].page + '|' + f.unsearched.length);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("true|1|1|0", Logged(result));
    }

    /// <summary>
    /// A refusal is its own event, and it is not an asset requisition.
    /// </summary>
    /// <remarks>
    /// <b>Both halves matter.</b> A document read leaving no trace was the state this fixes — the
    /// run record showed the scripts and the renders and was silent about what was read, for the one
    /// surface that sends a client's file to a third party. And filing it under
    /// <c>asset.requisition</c> would have been the other kind of wrong: a run that read three PDFs
    /// and bought nothing would have reported three assets bought.
    /// </remarks>
    [Fact]
    public async Task TestADocumentRefusalIsRecordedAsItsOwnEvent()
    {
        JsDrawingEngine.Documents = new DocumentProcessor("k", new DocumentBudget(3), root);

        await Run("await Documents.ask('missing.pdf', 'anything');");

        var refused = Assert.Single(Events("document.refused"));
        Assert.Equal("document", refused.GetProperty("kind").GetString());
        Assert.Equal("missing.pdf", refused.GetProperty("descriptor").GetString());
        Assert.Contains("no documents at all", refused.GetProperty("reason").GetString()!, StringComparison.Ordinal);

        // Not an asset. The counts a reader sees must mean what they say.
        Assert.Empty(Events("asset.refused"));
        Assert.Empty(Events("asset.requisition"));
    }

    /// <summary>The allowance is snapshotted alongside, so a reader sees where it stood.</summary>
    [Fact]
    public async Task TestTheDocumentBudgetIsSnapshottedIntoTheRecord()
    {
        JsDrawingEngine.Documents = new DocumentProcessor("k", new DocumentBudget(9), root);

        await Run("await Documents.ask('missing.pdf', 'anything');");

        var budget = Assert.Single(Events("budget"));
        Assert.Equal(9, budget.GetProperty("total").GetInt32());
        Assert.Equal(0, budget.GetProperty("spent").GetInt32());   // a local refusal costs nothing
    }

    /// <summary>A script that read nothing writes none of these, and the silence is honest.</summary>
    [Fact]
    public async Task TestDrawingWithoutReadingWritesNothing()
    {
        JsDrawingEngine.Documents = new DocumentProcessor("k", new DocumentBudget(3), root);

        await Run("const p = Snap(20, 20); p.rect(0, 0, 10, 10);");

        Assert.Empty(Events("document.read"));
        Assert.Empty(Events("document.refused"));
        Assert.Empty(Events("budget"));
    }
    #endregion

    #region Methods
    private JsonElement[] Events(string type)
    {
        var path = Path.Combine(root, "events", "server.jsonl");
        return !File.Exists(path)
            ? []
            : [.. File.ReadAllLines(path)
                .Select(l => JsonDocument.Parse(l).RootElement)
                .Where(e => e.GetProperty("type").GetString() == type)];
    }

    /// <summary>The same question twice in one engine, the second from the cache, reads the same members both times.</summary>
    /// <remarks>
    /// nvdareport2 (2026-10-20): a fresh answer's <c>warnings</c> was an array and a cached one's a <c>List</c>; Jint keeps the
    /// first type it saw behind a member, so every re-run of the type's reading threw "Unable to cast … List`1 to
    /// System.Array", uncatchable, and the agent worked round the cache the report type is built on.
    /// </remarks>
    [Fact]
    public async Task TestACachedAnswerReadsLikeAFreshOneInTheSameEngine()
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Revenue was 215,938.");
        JsDrawingEngine.Documents = new Answering(new DocumentBudget(5), root, new DocumentCache(Path.Combine(root, "cache")));

        const string Read = """
            const a = await Documents.ask('notes.txt', 'What was revenue?');
            log(a.provenance.fromCache + '|' + a.warnings.length + '|' + a.evidence.length + '|' + JSON.stringify(a.unverified));
            """;
        var fresh = await Run(Read);
        var cached = await Run(Read);

        Assert.True(fresh.Success, fresh.Error);
        Assert.True(cached.Success, cached.Error);
        Assert.Equal("false|0|0|[]", Logged(fresh));
        Assert.Equal("true|0|0|[]", Logged(cached));
    }

    /// <summary>
    /// With a schema, <c>answer</c> is the parsed object — the documented back-translation reads <c>back.answer.lines</c>.
    /// Before it, the JSON sat in <c>text</c> as a string and two runs (lanternkeeperlang1, 2) read it by eye.
    /// </summary>
    [Fact]
    public async Task TestASchemaAnswerIsParsed()
    {
        File.WriteAllText(Path.Combine(root, "lines.md"), "1. Olá");
        JsDrawingEngine.Documents = new Answering(new DocumentBudget(5), root, new DocumentCache(Path.Combine(root, "cache")),
            """{"lines":[{"n":1,"english":"Hello"}]}""");

        const string Read = """
            const a = await Documents.ask('lines.md', 'Translate.', { schema: { type: 'object', properties: {
                lines: { type: 'array', items: { type: 'object', properties: { n: { type: 'integer' }, english: { type: 'string' } } } } } } });
            log(a.answer.lines.map(l => l.n + '=' + l.english).join(',') + '|' + a.answer.lines.length + '|' + typeof a.text);
            """;
        var fresh = await Run(Read);
        var cached = await Run(Read);

        Assert.True(fresh.Success, fresh.Error);
        Assert.Equal("1=Hello|1|string", Logged(fresh));
        Assert.True(cached.Success, cached.Error);
        Assert.Equal("1=Hello|1|string", Logged(cached));
    }

    /// <summary>Without a schema the answer is prose, and <c>answer</c> is null rather than a guess at parsing it.</summary>
    [Fact]
    public async Task TestAProseAnswerHasNoParsedForm()
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Revenue was 215,938.");
        JsDrawingEngine.Documents = new Answering(new DocumentBudget(5), root, new DocumentCache(Path.Combine(root, "cache")),
            """{"looks":"like json"}""");

        var result = await Run("""
            const a = await Documents.ask('notes.txt', 'What was revenue?');
            log(String(a.answer));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("null", Logged(result));
    }

    /// <summary>
    /// The whole answer stringifies in the documented spelling, nested records too. lanternkeeperlang1 and 2 logged it
    /// as <c>Success</c>, <c>Text</c>, <c>Evidence</c>, <c>Unverified</c> and took the camelCase names to be missing.
    /// </summary>
    [Fact]
    public async Task TestAnAnswerStringifiesInCamelCase()
    {
        File.WriteAllText(Path.Combine(root, "notes.txt"), "Revenue was 215,938.");
        JsDrawingEngine.Documents = new Answering(new DocumentBudget(5), root, new DocumentCache(Path.Combine(root, "cache")));

        var result = await Run("""
            const a = await Documents.ask('notes.txt', 'What was revenue?');
            const back = JSON.parse(JSON.stringify(a));
            log([back.success, back.text, back.provenance.model, Array.isArray(back.unverified), back.Success === undefined,
                 back.provenance.Model === undefined].join('|'));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("true|215,938|fake-model|true|true|true", Logged(result));
    }

    private Task<DrawingExecutionResult> Run(string script) =>
        new DrawingMcpTools(null, null, null, root).ExecuteScript(
            script + " const _c = createCanvas(20,20); _c.getContext('2d').fillRect(0,0,20,20); _c;", 20, 20);

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();
    #endregion

    #region Types
    /// <summary>A processor that answers without a network, so the cache path can be reached from a script.</summary>
    private sealed class Answering(DocumentBudget budget, string root, DocumentCache cache, string answer = "215,938")
        : DocumentProcessor("test-key", budget, root, "fake-model", cache)
    {
        protected override Task<Generation> Generate(
            byte[] bytes, string mime, string query, string model, string? schema, bool images, int[]? pages,
            System.Threading.CancellationToken ct) =>
            Task.FromResult(new Generation(answer, 10, null));
    }

    /// <summary>An answer that arrives later, as a real model's does, so the script is suspended at its await.</summary>
    private sealed class AnsweringLater(DocumentBudget budget, string root, DocumentCache cache)
        : DocumentProcessor("test-key", budget, root, "fake-model", cache)
    {
        protected override async Task<Generation> Generate(
            byte[] bytes, string mime, string query, string model, string? schema, bool images, int[]? pages,
            System.Threading.CancellationToken ct)
        {
            await Task.Delay(50, ct);
            return new Generation("215,938", 10, null);
        }
    }
    #endregion
}
