namespace Polson.Tests.CLI;

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Polson.CLI;
using Xunit;

/// <summary>
/// <c>preserve-chatlog</c> — the hook that brings the host's conversation into the project.
/// </summary>
/// <remarks>
/// Two properties matter more than the copying. First, <b>stdout is the hook's reply channel</b>:
/// Antigravity requires a JSON object there and treats anything else as a malformed hook, so a
/// stray diagnostic on stdout would break every turn. Second, <b>it must never throw</b> — a hook
/// that fails takes the session with it, and losing a run to a failed chat-log copy would be a
/// bad trade.
/// </remarks>
[Collection(ConsoleCollection.Name)]
public class ChatlogPreserverTests : TestsRuntime, IDisposable
{
    #region Constructors
    public ChatlogPreserverTests()
    {
        root = Path.Combine(Path.GetTempPath(), "polson-chatlog-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Events);

        transcript = Path.Combine(root, "transcript.jsonl");
        File.WriteAllLines(transcript,
        [
            """{"type":"user","message":{"role":"user","content":"draw a mark"}}""",
            """{"type":"assistant","message":{"role":"assistant","model":"claude-opus-5","usage":{"input_tokens":1200,"output_tokens":340,"cache_read_input_tokens":8000}}}""",
            """{"type":"assistant","message":{"role":"assistant","model":"claude-opus-5","usage":{"input_tokens":1500,"output_tokens":620,"cache_creation_input_tokens":400}}}""",
        ]);
    }
    #endregion

    #region Methods
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", null);
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private string Events => Path.Combine(root, "events");

    /// <summary>Feeds a payload in and captures both channels, as a host would see them.</summary>
    private (string StdOut, string StdErr) Hook(string payload)
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        var previousOut = Console.Out;
        var previousErr = Console.Error;
        var previousIn = Console.In;

        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        Console.SetIn(new StringReader(payload));
        try
        {
            Assert.Equal(0, ChatlogPreserver.Run());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousErr);
            Console.SetIn(previousIn);
        }
        return (outWriter.ToString(), errWriter.ToString());
    }

    private string Json(string path) => path.Replace("\\", "/");
    #endregion

    #region The reply contract
    /// <summary>
    /// Standard output carries the JSON reply and nothing else, on every path.
    /// </summary>
    /// <remarks>
    /// Including the failure paths: a host parsing stdout does not care why the hook could not do
    /// its job, and a diagnostic leaking there would break the turn rather than the copy.
    /// </remarks>
    [Theory]
    [InlineData("""{"session_id":"s1"}""")]
    [InlineData("""{"transcript_path":"C:/nowhere/missing.jsonl"}""")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void TestStdoutIsAlwaysExactlyTheJsonReply(string payload)
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        var (stdout, _) = Hook(payload);

        Assert.Equal("{}", stdout);
    }

    /// <summary>Diagnostics go to stderr, where a host ignores them.</summary>
    [Fact]
    public void TestDiagnosticsGoToStandardError()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        var (stdout, stderr) = Hook("""{"transcript_path":"C:/nowhere/missing.jsonl"}""");

        Assert.Equal("{}", stdout);
        Assert.Contains("preserve-chatlog", stderr, StringComparison.Ordinal);
    }
    #endregion

    #region Both hosts
    /// <summary>Claude Code's payload: snake_case fields, project directory from the environment.</summary>
    [Fact]
    public void TestClaudePayloadPreservesTheTranscript()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"session_id":"abc-123","transcript_path":"{{Json(transcript)}}","hook_event_name":"SessionEnd"}""");

        var copied = Path.Combine(Events, "chat-abc-123.jsonl");
        Assert.True(File.Exists(copied));
        Assert.Equal(File.ReadAllText(transcript), File.ReadAllText(copied));
    }

    /// <summary>Antigravity's payload: camelCase fields, project directory from `workspacePaths`.</summary>
    [Fact]
    public void TestAntigravityPayloadPreservesTheTranscript()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", null);

        Hook($$"""
            {"invocationNum":1,"conversationId":"8de30750","workspacePaths":["{{Json(root)}}"],
             "transcriptPath":"{{Json(transcript)}}"}
            """);

        Assert.True(File.Exists(Path.Combine(Events, "chat-8de30750.jsonl")));
    }

    /// <summary>
    /// Antigravity's `Stop` carries no transcript path, and still preserves the final turn.
    /// </summary>
    /// <remarks>
    /// The hole in that host's contract: <c>PreInvocation</c> names the transcript but fires before
    /// a turn, and <c>Stop</c> fires after the last turn but names nothing. Without the remembered
    /// path, every session would lose its final exchange — the one most likely to hold the outcome.
    /// </remarks>
    [Fact]
    public void TestStopWithNoTranscriptPathUsesTheRememberedOne()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"conversationId":"8de30750","transcriptPath":"{{Json(transcript)}}"}""");
        File.Delete(Path.Combine(Events, "chat-8de30750.jsonl"));

        // Exactly the documented Stop payload: no path anywhere in it.
        Hook("""{"executionNum":1,"terminationReason":"model_stop","fullyIdle":true,"conversationId":"8de30750"}""");

        Assert.True(File.Exists(Path.Combine(Events, "chat-8de30750.jsonl")));
    }
    #endregion

    #region Repetition and usage
    /// <summary>
    /// Firing every turn overwrites one file rather than accumulating copies.
    /// </summary>
    /// <remarks>
    /// These hooks fire per turn, so a timestamped name would leave one whole-transcript copy per
    /// exchange — the same conversation written N times, growing quadratically with the session.
    /// </remarks>
    [Fact]
    public void TestRepeatedFiringsOverwriteOneFile()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);
        var payload = $$"""{"session_id":"same","transcript_path":"{{Json(transcript)}}"}""";

        Hook(payload);
        Hook(payload);
        Hook(payload);

        Assert.Single(Directory.GetFiles(Events, "chat-*.jsonl"));
    }

    /// <summary>Distinct sessions get distinct files.</summary>
    [Fact]
    public void TestDistinctSessionsAreKeptApart()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"session_id":"one","transcript_path":"{{Json(transcript)}}"}""");
        Hook($$"""{"session_id":"two","transcript_path":"{{Json(transcript)}}"}""");

        Assert.Equal(2, Directory.GetFiles(Events, "chat-*.jsonl").Length);
    }

    /// <summary>Token usage is summed from the transcript's own records.</summary>
    [Fact]
    public void TestTokenUsageIsSummarised()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"session_id":"abc","transcript_path":"{{Json(transcript)}}"}""");

        var summary = (JsonObject)JsonNode.Parse(
            File.ReadAllText(Path.Combine(Events, "tokens-abc.json")))!;

        Assert.Equal(2, summary["turns"]!.GetValue<int>());
        Assert.Equal(2700, summary["inputTokens"]!.GetValue<long>());
        Assert.Equal(960, summary["outputTokens"]!.GetValue<long>());
        Assert.Equal(8000, summary["cacheReadTokens"]!.GetValue<long>());
        Assert.Equal(12060, summary["totalTokens"]!.GetValue<long>());
    }

    /// <summary>
    /// A transcript with no usage records writes no summary rather than a summary of zeros.
    /// </summary>
    /// <remarks>
    /// Not every host reports usage. A file of zeros would read as a measurement — "this session
    /// cost nothing" — where absence reads correctly as "not reported".
    /// </remarks>
    [Fact]
    public void TestATranscriptWithoutUsageWritesNoSummary()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);
        var plain = Path.Combine(root, "plain.jsonl");
        File.WriteAllLines(plain, ["""{"role":"user","text":"hello"}""", """{"role":"model","text":"hi"}"""]);

        Hook($$"""{"session_id":"plain","transcript_path":"{{Json(plain)}}"}""");

        Assert.True(File.Exists(Path.Combine(Events, "chat-plain.jsonl")));
        Assert.Empty(Directory.GetFiles(Events, "tokens-*.json"));
    }

    /// <summary>A session id that is not a legal filename is made into one.</summary>
    [Fact]
    public void TestAnAwkwardSessionIdBecomesALegalFilename()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"session_id":"a/b:c*d","transcript_path":"{{Json(transcript)}}"}""");

        Assert.Single(Directory.GetFiles(Events, "chat-*.jsonl"));
    }
    #endregion

    #region Fields
    private readonly string root;
    private readonly string transcript;
    #endregion
}
