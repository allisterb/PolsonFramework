namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The project layout defines <c>events/server.jsonl</c> as the run's durable record — what survives
/// a refresh, a second viewer, a restart, and the end of the session — but nothing wrote to it.
/// These cover the server's half: script execution, renders, and failures.
/// <para>
/// The log must never be able to break a run, so its own failures are swallowed. What is asserted
/// here is the recording, and that a server with no project directory records nothing at all.
/// </para>
/// </summary>
public class RunEventLogTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-events-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public RunEventLogTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    private const string Script = "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); c;";

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
    #endregion

    #region Recording Tests
    [Fact]
    public async Task TestExecutionRecordsStartAndCompletion()
    {
        await Tools().ExecuteScript(Script, 64, 64);

        var types = Events().Select(Type).ToArray();

        Assert.Contains("script.start", types);
        Assert.Contains("script.ok", types);
    }

    /// <summary>Every event carries the four fields a reader merges and orders on.</summary>
    [Fact]
    public async Task TestEveryEventCarriesTheEnvelope()
    {
        await Tools().ExecuteScript(Script, 64, 64);

        foreach (var e in Events())
        {
            Assert.True(e.TryGetProperty("ts", out _), "missing ts");
            Assert.True(e.TryGetProperty("seq", out _), "missing seq");
            Assert.Equal("server", e.GetProperty("src").GetString());
            Assert.False(string.IsNullOrWhiteSpace(Type(e)), "missing type");
        }
    }

    /// <summary>Sequence numbers ascend and never repeat, so (src, seq) can break a timestamp tie.</summary>
    [Fact]
    public async Task TestSequenceNumbersAscendWithoutRepeating()
    {
        var tools = Tools();
        await tools.ExecuteScript(Script, 64, 64);
        await tools.ExecuteScript(Script, 64, 64);

        var seqs = Events().Select(e => e.GetProperty("seq").GetInt64()).ToArray();

        Assert.Equal(seqs.OrderBy(s => s).ToArray(), seqs);
        Assert.Equal(seqs.Distinct().Count(), seqs.Length);
    }

    /// <summary>A fresh log continues the file rather than restarting the sequence from zero.</summary>
    [Fact]
    public async Task TestSequenceContinuesAcrossRestart()
    {
        await Tools().ExecuteScript(Script, 64, 64);
        var before = Events().Select(e => e.GetProperty("seq").GetInt64()).Max();

        await Tools().ExecuteScript(Script, 64, 64);
        var seqs = Events().Select(e => e.GetProperty("seq").GetInt64()).ToArray();

        Assert.Equal(seqs.Distinct().Count(), seqs.Length);
        Assert.True(seqs.Max() > before);
    }
    #endregion

    #region Render Tests
    /// <summary>A render records its artifact by relative path, never by bytes.</summary>
    [Fact]
    public async Task TestRenderRecordsRelativeArtifactPath()
    {
        await Tools().ExecuteScript(Script, 64, 64, outFile: "artifacts/stage1.webp");

        var render = Events().Single(e => Type(e) == "render");

        Assert.Equal("artifacts/stage1.webp", render.GetProperty("artifact").GetString());
        Assert.True(render.GetProperty("bytes").GetInt32() > 0);
    }

    /// <summary>Forward slashes, because the demo site turns these straight into URLs.</summary>
    [Fact]
    public async Task TestArtifactPathUsesForwardSlashes()
    {
        await Tools().ExecuteScript(Script, 64, 64, outFile: "artifacts/nested/deep.webp");

        var render = Events().Single(e => Type(e) == "render");

        Assert.DoesNotContain('\\', render.GetProperty("artifact").GetString()!);
    }

    /// <summary>Pixels must never reach the log — they would destroy it as a readable record.</summary>
    [Fact]
    public async Task TestLogNeverContainsImageBytes()
    {
        await Tools().ExecuteScript(Script, 64, 64, outFile: "artifacts/stage1.webp");

        var text = File.ReadAllText(Path.Combine(root, "events", "server.jsonl"));

        Assert.DoesNotContain("base64", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(text.Length < 4000, $"log is implausibly large ({text.Length} chars) — is it carrying pixels?");
    }
    #endregion

    #region Script Persistence Tests
    /// <summary>The executed script is saved, so a run stays reconstructible without the agent's help.</summary>
    [Fact]
    public async Task TestExecutedScriptIsPersisted()
    {
        await Tools().ExecuteScript(Script, 64, 64);

        var saved = Directory.GetFiles(Path.Combine(root, "scripts"), "*.js");

        Assert.Single(saved);
        Assert.Equal(Script, File.ReadAllText(saved[0]));
    }

    /// <summary>Events name the saved script, so a reader can pair a render with its source.</summary>
    [Fact]
    public async Task TestEventsReferenceTheSavedScript()
    {
        await Tools().ExecuteScript(Script, 64, 64, outFile: "artifacts/stage1.webp");

        var referenced = Events().Single(e => Type(e) == "render").GetProperty("script").GetString()!;

        Assert.StartsWith("scripts/", referenced, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, referenced.Replace('/', Path.DirectorySeparatorChar))));
    }
    #endregion

    #region Failure Tests
    /// <summary>A failing script is recorded as an error, carrying the reason.</summary>
    [Fact]
    public async Task TestFailingScriptRecordsAnError()
    {
        await Tools().ExecuteScript("this is not valid javascript {{{", 64, 64);

        var error = Events().Single(e => Type(e) == "script.error");

        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("error").GetString()));
    }

    /// <summary>
    /// A refused output path throws rather than returning a failed result, so without an explicit
    /// catch the start event would never be closed and the run would read as hung.
    /// </summary>
    [Fact]
    public async Task TestRefusedPathStillClosesTheRun()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Tools().ExecuteScript(Script, 64, 64, outFile: "../escaped.webp"));

        var types = Events().Select(Type).ToArray();

        Assert.Contains("script.start", types);
        Assert.Contains("script.error", types);
    }
    #endregion

    #region Disabled Log Tests
    /// <summary>With no project directory there is nothing to record into, and nothing is written.</summary>
    [Fact]
    public async Task TestNoProjectDirectoryWritesNothing()
    {
        var tools = new DrawingMcpTools();

        Assert.False(tools.Events.Enabled);
        Assert.Null(tools.Events.FilePath);

        await tools.ExecuteScript(Script, 64, 64);

        Assert.Empty(Events());
        Assert.False(Directory.Exists(Path.Combine(root, "scripts")));
    }
    #endregion
}
