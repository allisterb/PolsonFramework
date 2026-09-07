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

    private Task<DrawingExecutionResult> Run(string script) =>
        new DrawingMcpTools(null, null, null, root).ExecuteScript(
            script + " const _c = createCanvas(20,20); _c.getContext('2d').fillRect(0,0,20,20); _c;", 20, 20);

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();
    #endregion
}
