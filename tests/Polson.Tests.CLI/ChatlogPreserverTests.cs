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
    private (string StdOut, string StdErr) Hook(string payload, string host = "", string? projectDir = null)
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
            Assert.Equal(0, ChatlogPreserver.Run(new PreserveChatlogOptions
            {
                Host = host,
                ProjectDir = projectDir ?? string.Empty,
            }));
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

    /// <summary>
    /// Diagnostics never reach standard output, whatever the logging happens to be wired to.
    /// </summary>
    /// <remarks>
    /// This test found the hazard rather than confirming it: with logging unconfigured, the logger
    /// writes to the console, and its lines came out on <c>stdout</c> — which would have made the
    /// hook malformed on every turn and looked like the whole integration failing. Standard output
    /// is now taken away from everything except the reply, so the contract holds regardless of how
    /// logging is set up.
    /// </remarks>
    [Fact]
    public void TestDiagnosticsNeverReachStandardOutput()
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
    /// One message spread over several lines is counted once, not once per line.
    /// </summary>
    /// <remarks>
    /// **Claude Code writes a line per content block** — the text, the thinking and each tool call
    /// arrive separately and every one of them repeats the same <c>usage</c> block. Summing lines
    /// therefore counts a turn two or three times, and nothing about the result looks wrong.
    /// <para>
    /// Measured on the drawing-1 run before the fix: <b>106 lines carrying usage against 60 distinct
    /// message ids</b>, a 1.75× overcount reporting 31.2M tokens for a run that spent 17.9M. The
    /// figure was quoted as a cost and then used to argue the run had failed to converge over 106
    /// turns, when it had drawn 13 renders in 60 — so the defect cost a wrong number and a wrong
    /// conclusion drawn from it.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestOneMessageOverSeveralLinesIsCountedOnce()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);
        var blocks = Path.Combine(root, "blocks.jsonl");

        // msg_a arrives as thinking + text + tool_use; msg_b as text + tool_use. Five lines, two turns.
        const string A = """{"type":"assistant","message":{"id":"msg_a","role":"assistant","model":"claude-opus-5","usage":{"input_tokens":100,"output_tokens":10,"cache_read_input_tokens":9000}}}""";
        const string B = """{"type":"assistant","message":{"id":"msg_b","role":"assistant","model":"claude-opus-5","usage":{"input_tokens":200,"output_tokens":20,"cache_read_input_tokens":9500}}}""";
        File.WriteAllLines(blocks, [A, A, A, B, B]);

        Hook($$"""{"session_id":"blocks","transcript_path":"{{Json(blocks)}}"}""");

        var summary = (JsonObject)JsonNode.Parse(
            File.ReadAllText(Path.Combine(Events, "tokens-blocks.json")))!;

        Assert.Equal(2, summary["turns"]!.GetValue<int>());
        Assert.Equal(300, summary["inputTokens"]!.GetValue<long>());
        Assert.Equal(30, summary["outputTokens"]!.GetValue<long>());
        Assert.Equal(18500, summary["cacheReadTokens"]!.GetValue<long>());
        Assert.Equal(18830, summary["totalTokens"]!.GetValue<long>());

        // Per-model output is summed in the same loop and would double-count with it.
        Assert.Equal(30, ((JsonObject)summary["outputByModel"]!)["claude-opus-5"]!.GetValue<long>());
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

    #region Finding the project
    /// <summary>
    /// The project comes from <c>--project-dir</c>, not from wherever the host launched the hook.
    /// </summary>
    /// <remarks>
    /// The bug that made the whole feature look broken. Antigravity sets no project environment
    /// variable, and its <c>PostInvocation</c> and <c>Stop</c> payloads carry no
    /// <c>workspacePaths</c>, so resolution fell through to the current directory: the hook ran,
    /// exited 0, copied the transcript somewhere nobody was looking, and left the project's
    /// <c>events/</c> empty with no error anywhere.
    /// </remarks>
    [Fact]
    public void TestTheProjectDirectoryBeatsTheWorkingDirectory()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", null);

        var elsewhere = Path.Combine(root, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        var previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(elsewhere);
        try
        {
            // No workspacePaths, no environment variable — exactly Antigravity's Stop payload.
            Hook($$"""{"conversationId":"conv","transcriptPath":"{{Json(transcript)}}"}""", "agy", root);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }

        Assert.True(File.Exists(Path.Combine(Events, "chat-conv.jsonl")));
        Assert.False(Directory.Exists(Path.Combine(elsewhere, "events")));
    }

    /// <summary>A project directory that does not exist is ignored rather than created blindly.</summary>
    /// <remarks>
    /// A stale path in a hook — a project since moved or deleted — should fall through to the other
    /// strategies, not conjure a directory tree at a location nobody expects.
    /// </remarks>
    [Fact]
    public void TestAStaleProjectDirectoryFallsThrough()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);
        var gone = Path.Combine(root, "was-here-once");

        Hook($$"""{"session_id":"s","transcript_path":"{{Json(transcript)}}"}""", "", gone);

        Assert.True(File.Exists(Path.Combine(Events, "chat-s.jsonl")));
        Assert.False(Directory.Exists(gone));
    }
    #endregion

    #region Subagent transcripts
    /// <summary>
    /// A multi-agent run happens inside the subagents, so their transcripts come too.
    /// </summary>
    /// <remarks>
    /// The parent transcript records that a subagent was dispatched and what it returned; it does
    /// not record what the subagent did. Measured on a real <c>comic_studio</c> run: parent 578 KB,
    /// penciler 2.27 MB, and every one of the ten script executions the server recorded happened in
    /// the second file. Preserving only the parent kept the receipt and lost the work.
    /// </remarks>
    [Fact]
    public void TestSubagentTranscriptsArePreservedWithTheirAttribution()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        // The host's layout: a folder named for the session, beside the session's own transcript.
        var subagents = Path.Combine(root, Path.GetFileNameWithoutExtension(transcript), "subagents");
        Directory.CreateDirectory(subagents);
        File.WriteAllText(Path.Combine(subagents, "agent-a7e2.jsonl"),
            """{"uuid":"s1","type":"assistant","isSidechain":true}""");

        // The sidecar is what names the actor, which the run record has never had.
        File.WriteAllText(Path.Combine(subagents, "agent-a7e2.meta.json"),
            """{"agentType":"penciler","spawnDepth":1}""");

        Hook($$"""{"session_id":"multi","transcript_path":"{{Json(transcript)}}"}""");

        var copied = Path.Combine(Events, "subagents");
        Assert.True(File.Exists(Path.Combine(copied, "agent-a7e2.jsonl")));
        Assert.Contains("penciler", File.ReadAllText(Path.Combine(copied, "agent-a7e2.meta.json")));

        // The parent is still preserved; this is an addition, not a replacement.
        Assert.True(File.Exists(Path.Combine(Events, "chat-multi.jsonl")));
    }

    /// <summary>A single-agent run has no such directory, and that is not a failure.</summary>
    [Fact]
    public void TestASoloRunPreservesNoSubagents()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"session_id":"solo","transcript_path":"{{Json(transcript)}}"}""");

        Assert.True(File.Exists(Path.Combine(Events, "chat-solo.jsonl")));
        Assert.False(Directory.Exists(Path.Combine(Events, "subagents")));
    }

    /// <summary>Only transcripts and their sidecars, not whatever else the host leaves there.</summary>
    [Fact]
    public void TestOnlyTranscriptFilesAreCopiedFromTheSubagentDirectory()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        var subagents = Path.Combine(root, Path.GetFileNameWithoutExtension(transcript), "subagents");
        Directory.CreateDirectory(subagents);
        File.WriteAllText(Path.Combine(subagents, "agent-a1.jsonl"), "{}");
        File.WriteAllText(Path.Combine(subagents, "agent-a1.lock"), "held");
        File.WriteAllText(Path.Combine(subagents, "scratch.tmp"), "noise");

        Hook($$"""{"session_id":"multi","transcript_path":"{{Json(transcript)}}"}""");

        var copied = Directory.GetFiles(Path.Combine(Events, "subagents")).Select(Path.GetFileName);
        Assert.Equal(["agent-a1.jsonl"], copied);
    }
    #endregion

    #region The uncompacted transcript
    /// <summary>
    /// The uncompacted transcript is preserved alongside the active one.
    /// </summary>
    /// <remarks>
    /// Not a nicety. Antigravity's active <c>transcript.jsonl</c> is <i>compacted</i> — measured on a
    /// real session at 111 KB against the full file's 222 KB, so half the conversation was already
    /// gone from it. What compaction drops is the earliest exchanges, which is where a direction
    /// gets chosen and therefore the part a reader most needs afterwards.
    /// </remarks>
    [Fact]
    public void TestTheUncompactedTranscriptIsPreservedToo()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);
        var active = Path.Combine(logs, "transcript.jsonl");
        File.WriteAllText(active, """{"step":9,"note":"only the recent part"}""");
        File.WriteAllText(Path.Combine(logs, "transcript_full.jsonl"),
            """{"step":0,"note":"the whole thing"}""");

        Hook($$"""{"conversationId":"conv","transcriptPath":"{{Json(active)}}"}""", "agy");

        var full = Path.Combine(Events, "chat-conv-full.jsonl");
        Assert.True(File.Exists(Path.Combine(Events, "chat-conv.jsonl")));
        Assert.True(File.Exists(full));
        Assert.Contains("the whole thing", File.ReadAllText(full), StringComparison.Ordinal);
    }

    /// <summary>A host that keeps no full transcript is not an error.</summary>
    [Fact]
    public void TestNoUncompactedTranscriptIsFine()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"session_id":"solo","transcript_path":"{{Json(transcript)}}"}""");

        Assert.True(File.Exists(Path.Combine(Events, "chat-solo.jsonl")));
        Assert.Empty(Directory.GetFiles(Events, "*-full.jsonl"));
    }
    #endregion

    #region Locating a transcript the payload did not name
    /// <summary>
    /// Every firing leaves a trace, whether or not it found anything.
    /// </summary>
    /// <remarks>
    /// The instrument that was missing. A hook that never fired and one that fired and found
    /// nothing look identical from the project, because stderr goes wherever the host sends it —
    /// usually nowhere. This line is the only evidence either way, and it records the payload's
    /// <i>keys</i> so a host's real schema can be read off a run instead of inferred from its docs.
    /// </remarks>
    [Fact]
    public void TestEveryFiringLeavesATrace()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook("""{"conversationId":"nope","terminationReason":"model_stop"}""", "agy");

        var trace = File.ReadAllLines(Path.Combine(Events, "hooks.jsonl"));
        var line = (JsonObject)JsonNode.Parse(trace[^1])!;

        Assert.Equal("agy", line["host"]!.GetValue<string>());
        Assert.False(line["found"]!.GetValue<bool>());
        Assert.Contains("conversationId",
            (line["payloadKeys"] as JsonArray)!.Select(k => k?.ToString()));

        // Addressed by id only. An earlier version fell back to the newest transcript under the
        // host's store, and this very test caught it copying an unrelated conversation from the
        // developer's own machine for a session id that does not exist.
        Assert.Equal("remembered", line["resolvedBy"]!.GetValue<string>());
    }

    /// <summary>A payload that names its transcript still wins over any host fallback.</summary>
    /// <remarks>
    /// The host hint exists for the case where nothing was named. Letting it override a path the
    /// host actually gave would replace a fact with a guess.
    /// </remarks>
    [Fact]
    public void TestThePayloadBeatsTheHostFallback()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook($$"""{"session_id":"named","transcript_path":"{{Json(transcript)}}"}""", "agy");

        var line = (JsonObject)JsonNode.Parse(File.ReadAllLines(Path.Combine(Events, "hooks.jsonl"))[^1])!;
        Assert.Equal("payload", line["resolvedBy"]!.GetValue<string>());
    }

    /// <summary>An unknown host resolves nothing rather than searching somewhere arbitrary.</summary>
    [Fact]
    public void TestAnUnknownHostDoesNotGuess()
    {
        Environment.SetEnvironmentVariable("CLAUDE_PROJECT_DIR", root);

        Hook("""{"conversationId":"whatever"}""", "something-else");

        var line = (JsonObject)JsonNode.Parse(File.ReadAllLines(Path.Combine(Events, "hooks.jsonl"))[^1])!;
        Assert.Equal("remembered", line["resolvedBy"]!.GetValue<string>());
        Assert.False(line["found"]!.GetValue<bool>());
    }
    #endregion

    #region Fields
    private readonly string root;
    private readonly string transcript;
    #endregion
    #region Transcript Store Tests
    /// <summary>
    /// Antigravity has three surfaces and three data roots, and they share a layout.
    /// </summary>
    /// <remarks>
    /// Searching only the desktop's <c>antigravity/</c> was a real miss: a run through the
    /// Antigravity CLI kept its transcript at the identical path under <c>antigravity-cli/</c>, so
    /// the fallback reported nothing found while the file sat there. The bug is invisible on a
    /// machine that only ever used the surface you happened to search, which is why this is pinned
    /// rather than left to the integration path.
    /// </remarks>
    [Fact]
    public void TestEveryAntigravitySurfaceIsSearched()
    {
        var paths = ChatlogPreserver.AntigravityTranscripts(@"C:/home", "conv-1").ToArray();

        Assert.Equal(3, paths.Length);
        Assert.Contains(paths, p => p.Contains(Path.Combine(".gemini", "antigravity", "brain"), StringComparison.Ordinal));
        Assert.Contains(paths, p => p.Contains(Path.Combine(".gemini", "antigravity-cli", "brain"), StringComparison.Ordinal));
        Assert.Contains(paths, p => p.Contains(Path.Combine(".gemini", "antigravity-ide", "brain"), StringComparison.Ordinal));
    }

    /// <summary>The desktop is tried first, being the common case; the order is otherwise arbitrary.</summary>
    [Fact]
    public void TestTheDesktopStoreIsTriedFirst()
    {
        var first = ChatlogPreserver.AntigravityTranscripts(@"C:/home", "conv-1").First();

        Assert.Contains(Path.Combine(".gemini", "antigravity", "brain"), first, StringComparison.Ordinal);
    }

    /// <summary>The layout below each root is the one the host writes, conversation id and all.</summary>
    [Fact]
    public void TestTheTranscriptLayoutIsTheHostsOwn()
    {
        var path = ChatlogPreserver.AntigravityTranscripts(@"C:/home", "conv-abc").First();

        Assert.EndsWith(
            Path.Combine("brain", "conv-abc", ".system_generated", "logs", "transcript.jsonl"),
            path, StringComparison.Ordinal);
    }
    #endregion

}
