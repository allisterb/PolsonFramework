namespace Polson.Tests.CLI;

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Polson.CLI;
using Xunit;

/// <summary>
/// <c>polson report</c> — comparing what a run directory claims against what the engine recorded.
/// </summary>
/// <remarks>
/// A run was reviewed where the directory looked complete — seven plausible scripts, four finished
/// SVGs, a written audit concluding "all constraints satisfied" — and the event log contained a
/// single <c>run.start</c>. The agent had written every file itself and never executed anything.
/// Nothing about the directory revealed that, because the only half an agent cannot fabricate is
/// the log the server appends to as it works.
/// <para>
/// These tests pin the comparison rather than any judgement: the report states counts and what does
/// not reconcile, and leaves the reading to the director.
/// </para>
/// </remarks>
[Collection(ConsoleCollection.Name)]
public class RunReportTests : TestsRuntime, IDisposable
{
    #region Constructors
    public RunReportTests()
    {
        root = Path.Combine(Path.GetTempPath(), "polson-report-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "artifacts"));
        Directory.CreateDirectory(Path.Combine(root, "events"));
    }
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private void Script(string name, string body = "// x") =>
        File.WriteAllText(Path.Combine(root, "scripts", name), body);

    private void Artifact(string name) =>
        File.WriteAllText(Path.Combine(root, "artifacts", name), "not really an image");

    private void Events(params string[] lines) =>
        File.WriteAllLines(Path.Combine(root, "events", "server.jsonl"), lines);

    /// <summary>The orchestrator's own transcript, which is where a standalone run's conversation lives.</summary>
    private void AgentEvents(params string[] lines) =>
        File.WriteAllLines(Path.Combine(root, "events", "agent.jsonl"), lines);

    /// <summary>A transcript copied in by the preserve-chatlog hook, which is where a managed run's lives.</summary>
    private void ChatLog(string name) =>
        File.WriteAllText(Path.Combine(root, "events", name), """{"role":"user"}""");

    /// <summary>Runs the report and returns its JSON, which is the same data the table prints.</summary>
    private JsonObject Report()
    {
        var writer = new StringWriter();
        var previous = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(0, RunReport.Run(new ReportOptions { ProjectDir = root, Json = true }));
        }
        finally
        {
            Console.SetOut(previous);
        }
        return (JsonObject)JsonNode.Parse(writer.ToString())!;
    }

    private static string[] Warnings(JsonObject report) =>
        [.. (report["warnings"] as JsonArray ?? []).Select(w => w?.ToString() ?? "")];

    private static JsonObject[] Sessions(JsonObject report) =>
        [.. (report["sessions"] as JsonArray ?? []).Select(s => (JsonObject)s!)];

    /// <summary>The table a director actually reads, as text.</summary>
    /// <remarks>
    /// The JSON is the same data, but what is <em>shown</em> is a separate decision — a row that
    /// concatenates four runs' stages is wrong on the page while the array behind it is correct.
    /// </remarks>
    private string Printed()
    {
        var writer = new StringWriter();
        var previous = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(0, RunReport.Run(new ReportOptions { ProjectDir = root }));
        }
        finally
        {
            Console.SetOut(previous);
        }
        return writer.ToString();
    }
    #endregion

    #region A run that really happened
    /// <summary>A run whose files all trace to events reconciles, and says so.</summary>
    [Fact]
    public void TestAConsistentRunHasNothingUnaccountedFor()
    {
        Script("0001.js");
        Script("0002.js");
        Artifact("stage1.webp");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"stage.begin","stage":"Blocking"}""",
            """{"type":"note","message":"why"}""",
            """{"type":"script.ok","script":"scripts/0002.js"}""",
            """{"type":"render","script":"scripts/0002.js","artifact":"artifacts/stage1.webp"}""");

        var report = Report();

        Assert.Empty(Warnings(report));
        Assert.Equal(2, report["scriptsExecuted"]!.GetValue<int>());
        Assert.Equal(1, report["renders"]!.GetValue<int>());
        Assert.Equal("Blocking", (report["stages"] as JsonArray)![0]!.ToString());
    }
    #endregion

    #region Claims a run made about itself
    /// <summary>
    /// A claim stated and never settled reads as verification and is not.
    /// </summary>
    /// <remarks>
    /// Taken from a live critique that stated three expectations — low-key dominance, accent share,
    /// palette gamut — and settled one. Counted rather than matched by text: an <c>expect</c> and its
    /// <c>check</c> are written independently and rarely agree word for word, so a count says
    /// truthfully that something was left open without pretending to know which.
    /// </remarks>
    [Fact]
    public void TestUnsettledExpectationsAreReported()
    {
        Script("0001.js");
        Artifact("critique.webp");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"expect","claim":"low-key dominance over 60%"}""",
            """{"type":"expect","claim":"accent under 15%"}""",
            """{"type":"expect","claim":"palette matches the gamut"}""",
            """{"type":"check","claim":"low-key dominance over 60%","passed":false,"detail":"57.1%"}""",
            """{"type":"check","claim":"low-key dominance over 60%","passed":true,"detail":"62.4%"}""",
            """{"type":"render","script":"scripts/0001.js","artifact":"artifacts/critique.webp"}""");

        var report = Report();

        Assert.Equal(3, report["expectations"]!.GetValue<int>());
        Assert.Equal(2, report["checks"]!.GetValue<int>());
        Assert.Equal(1, report["checksFailed"]!.GetValue<int>());
        Assert.Contains(Warnings(report), w => w.Contains("never settled with a check"));
    }

    /// <summary>
    /// Every check failing means the run diagnosed without demonstrating — one step from the failure
    /// the whole mechanism exists to catch.
    /// </summary>
    [Fact]
    public void TestACritiqueThatOnlyEverFailedIsReported()
    {
        Script("0001.js");
        Artifact("critique.webp");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"expect","claim":"low-key dominance over 60%"}""",
            """{"type":"check","claim":"low-key dominance over 60%","passed":false,"detail":"57.1%"}""",
            """{"type":"render","script":"scripts/0001.js","artifact":"artifacts/critique.webp"}""");

        var report = Report();

        Assert.Contains(Warnings(report), w => w.Contains("none was re-run after a fix"));
        Assert.Contains((JsonArray)report["claimsNotMet"]!,
            c => c!.ToString().Contains("low-key dominance"));
    }

    /// <summary>A critique that stated claims, settled them all, and ended on a pass says nothing.</summary>
    [Fact]
    public void TestASettledCritiqueWarnsAboutNothing()
    {
        Script("0001.js");
        Artifact("critique.webp");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"expect","claim":"low-key dominance over 60%"}""",
            """{"type":"check","claim":"low-key dominance over 60%","passed":false,"detail":"57.1%"}""",
            """{"type":"check","claim":"low-key dominance over 60%","passed":true,"detail":"62.4%"}""",
            """{"type":"render","script":"scripts/0001.js","artifact":"artifacts/critique.webp"}""");

        Assert.Empty(Warnings(Report()));
    }

    /// <summary>A run that made no claims is not nagged about claims it did not make.</summary>
    [Fact]
    public void TestARunWithNoClaimsIsNotWarnedAboutThem()
    {
        Script("0001.js");
        Artifact("stage1.webp");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"render","script":"scripts/0001.js","artifact":"artifacts/stage1.webp"}""");

        var report = Report();

        Assert.Equal(0, report["expectations"]!.GetValue<int>());
        Assert.Empty(Warnings(report));
    }
    #endregion

    #region A run that did not
    /// <summary>
    /// The case this exists for: a full-looking directory and an empty log.
    /// </summary>
    /// <remarks>
    /// All four readings fire together, which is what the pattern looks like — the point is not any
    /// one of them but that they agree.
    /// </remarks>
    [Fact]
    public void TestAHandWrittenRunIsFullyUnaccountedFor()
    {
        Script("01_data.js");
        Script("02_forms.js");
        Artifact("final.svg");
        Events("""{"type":"run.start"}""");

        var report = Report();
        var warnings = Warnings(report);

        Assert.Equal(0, report["scriptsExecuted"]!.GetValue<int>());
        Assert.Equal(0, report["renders"]!.GetValue<int>());
        Assert.Contains(warnings, w => w.Contains("No scripts were executed", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("No renders were recorded", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("written by hand", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("have no render event", StringComparison.Ordinal));
    }

    /// <summary>An artifact with no render event is named, even when the run is otherwise real.</summary>
    /// <remarks>
    /// The partial case is the one worth catching: a run that genuinely drew some things and
    /// hand-placed others reconciles for everything except the file that matters.
    /// </remarks>
    [Fact]
    public void TestAnArtifactWithNoRenderIsReportedInAnOtherwiseRealRun()
    {
        Script("0001.js");
        Artifact("drawn.webp");
        Artifact("smuggled.svg");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"render","script":"scripts/0001.js","artifact":"artifacts/drawn.webp"}""");

        var report = Report();

        Assert.Contains("smuggled.svg",
            (report["artifactsNoRenderProduced"] as JsonArray)!.Select(a => a?.ToString()));
        Assert.DoesNotContain("drawn.webp",
            (report["artifactsNoRenderProduced"] as JsonArray)!.Select(a => a?.ToString()));
    }

    /// <summary>No event log at all is the strongest signal, and stops further guessing.</summary>
    [Fact]
    public void TestAMissingEventLogIsReportedOnItsOwn()
    {
        Script("0001.js");
        Artifact("final.svg");

        var report = Report();
        var warnings = Warnings(report);

        Assert.False(report["hasEventLog"]!.GetValue<bool>());
        Assert.Single(warnings);
        Assert.Contains("never started", warnings[0], StringComparison.Ordinal);
    }
    #endregion

    #region Robustness
    /// <summary>
    /// A truncated final line does not stop the report.
    /// </summary>
    /// <remarks>
    /// A killed run leaves a half-written line, and that is exactly the run someone most wants a
    /// report on. Refusing to parse would withhold the report when it is most needed.
    /// </remarks>
    [Fact]
    public void TestATruncatedLogStillReports()
    {
        Script("0001.js");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"render","script":"scripts""");

        var report = Report();

        Assert.Equal(2, report["events"]!.GetValue<int>());
        Assert.Equal(1, report["scriptsExecuted"]!.GetValue<int>());
    }

    /// <summary>A script run twice counts once, since it is one file.</summary>
    [Fact]
    public void TestRepeatedExecutionsOfOneScriptCountOnce()
    {
        Script("0001.js");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""");

        Assert.Equal(1, Report()["scriptsExecuted"]!.GetValue<int>());
    }

    /// <summary>
    /// A standalone run's conversation is <c>agent.jsonl</c>, and the report must say so.
    /// </summary>
    /// <remarks>
    /// There is no chat log to copy under the orchestrator, because the orchestrator is itself holding
    /// the conversation. Counting only hook-copied files reported every standalone run as unrecorded
    /// while its full transcript sat in the same directory — the report contradicting the run.
    /// </remarks>
    [Fact]
    public void TestAStandaloneRunsTranscriptCountsAsItsConversationRecord()
    {
        Events("""{"type":"run.start"}""");
        AgentEvents(
            """{"type":"run.start"}""",
            """{"type":"turn.start","chars":80}""",
            """{"type":"thinking"}""",
            """{"type":"tool.call","name":"ExecuteScript.json"}""",
            """{"type":"text"}""",
            """{"type":"turn.end","status":"done"}""",
            """{"type":"run.end"}""");

        var report = Report();

        Assert.Equal(0, report["chatLogs"]!.GetValue<int>());
        Assert.True(report["agentTranscript"]!.GetValue<bool>());
        Assert.Equal(1, report["agentTurns"]!.GetValue<int>());

        // Three steps, not seven events: the run and turn brackets are not things the agent said.
        Assert.Equal(3, report["agentSteps"]!.GetValue<int>());

        var record = report["conversationRecord"]!.ToString();
        Assert.Contains("agent.jsonl", record, StringComparison.Ordinal);
        Assert.Contains("1 turn,", record, StringComparison.Ordinal);
        Assert.Contains("3 steps", record, StringComparison.Ordinal);
        Assert.DoesNotContain("not kept", record, StringComparison.Ordinal);
    }

    /// <summary>
    /// A transcript that exists but recorded nothing is a distinct state from one that is absent.
    /// </summary>
    /// <remarks>
    /// The orchestrator opens the file as the run starts, so its presence proves only that a run was
    /// attempted. Reporting that as a kept conversation would hide the case worth seeing: the agent
    /// never got a turn.
    /// </remarks>
    [Fact]
    public void TestAnEmptyTranscriptIsReportedAsEmptyRatherThanAsKept()
    {
        Events("""{"type":"run.start"}""");
        AgentEvents("""{"type":"run.start"}""", """{"type":"run.end","status":"incomplete"}""");

        var report = Report();

        Assert.True(report["agentTranscript"]!.GetValue<bool>());
        Assert.Equal(0, report["agentSteps"]!.GetValue<int>());
        Assert.Contains("recorded no steps", report["conversationRecord"]!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A managed run's hook-copied logs are still counted, and both routes report together.</summary>
    [Fact]
    public void TestHookCopiedLogsAreCountedAndBothRoutesAreReported()
    {
        Events("""{"type":"run.start"}""");
        ChatLog("chat-abc.jsonl");
        ChatLog("chat-abc-full.jsonl");

        var hookOnly = Report();
        Assert.Equal(2, hookOnly["chatLogs"]!.GetValue<int>());
        Assert.False(hookOnly["agentTranscript"]!.GetValue<bool>());
        Assert.Contains("2 chat logs", hookOnly["conversationRecord"]!.ToString(), StringComparison.Ordinal);

        AgentEvents("""{"type":"turn.start"}""", """{"type":"text"}""");

        var both = Report();
        var record = both["conversationRecord"]!.ToString();
        Assert.Contains("2 chat logs", record, StringComparison.Ordinal);
        Assert.Contains("agent.jsonl", record, StringComparison.Ordinal);
    }

    /// <summary>With neither route present, the report says the conversation was not kept.</summary>
    [Fact]
    public void TestNoConversationAtAllIsReportedAsNotKept()
    {
        Events("""{"type":"run.start"}""");

        var report = Report();

        Assert.False(report["agentTranscript"]!.GetValue<bool>());
        Assert.Contains("not kept", report["conversationRecord"]!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A missing directory is refused rather than reported as an empty run.</summary>
    [Fact]
    public void TestAMissingDirectoryIsAnError()
    {
        var writer = new StringWriter();
        var previous = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(1, RunReport.Run(new ReportOptions
            {
                ProjectDir = Path.Combine(root, "does-not-exist")
            }));
        }
        finally
        {
            Console.SetOut(previous);
        }
    }
    #endregion

    #region Sessions within a project
    /// <summary>
    /// A project's log is appended to across runs, so the report splits it at each <c>run.start</c>.
    /// </summary>
    /// <remarks>
    /// Read whole, a project run four times reports its stages as one sequence that repeats — which
    /// reads as a single confused run rather than as four ordinary ones, and hides a session that
    /// connected and did nothing inside the totals of the ones that worked.
    /// </remarks>
    [Fact]
    public void TestTheLogIsSplitAtEachRunStart()
    {
        Events(
            """{"ts":"2026-08-31T09:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T09:00:01.000Z","type":"stage.begin","stage":"Blocking"}""",
            """{"ts":"2026-08-31T09:00:02.000Z","type":"script.ok","script":"scripts/0001.js"}""",
            """{"ts":"2026-08-31T11:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T11:00:01.000Z","type":"stage.begin","stage":"Refine"}""",
            """{"ts":"2026-08-31T11:00:02.000Z","type":"script.ok","script":"scripts/0002.js"}""");

        var sessions = Sessions(Report());

        Assert.Equal(2, sessions.Length);
        Assert.Equal("Blocking", (sessions[0]["stages"] as JsonArray)![0]!.ToString());
        Assert.Equal("Refine", (sessions[1]["stages"] as JsonArray)![0]!.ToString());
        Assert.Equal(1, sessions[0]["scriptsExecuted"]!.GetValue<int>());
        Assert.Equal(1, sessions[1]["scriptsExecuted"]!.GetValue<int>());
    }

    /// <summary>
    /// The property that makes a breakdown worth reading: the parts sum to the whole.
    /// </summary>
    /// <remarks>
    /// A per-session count on a different definition from the project total would be worse than no
    /// breakdown, because the disagreement would look like a finding about the run.
    /// </remarks>
    [Fact]
    public void TestTheSessionsSumToTheProjectTotals()
    {
        Events(
            """{"ts":"2026-08-31T09:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T09:00:01.000Z","type":"script.ok","script":"scripts/0001.js"}""",
            """{"ts":"2026-08-31T09:00:02.000Z","type":"render","artifact":"artifacts/a.webp"}""",
            """{"ts":"2026-08-31T09:00:03.000Z","type":"note","message":"one"}""",
            """{"ts":"2026-08-31T11:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T11:00:01.000Z","type":"script.error","script":"scripts/0002.js"}""",
            """{"ts":"2026-08-31T11:00:02.000Z","type":"note","message":"two"}""",
            """{"ts":"2026-08-31T11:00:03.000Z","type":"note","message":"three"}""");

        var report = Report();
        var sessions = Sessions(report);

        Assert.Equal(report["scriptsExecuted"]!.GetValue<int>(), sessions.Sum(s => s["scriptsExecuted"]!.GetValue<int>()));
        Assert.Equal(report["scriptsFailed"]!.GetValue<int>(), sessions.Sum(s => s["scriptsFailed"]!.GetValue<int>()));
        Assert.Equal(report["renders"]!.GetValue<int>(), sessions.Sum(s => s["renders"]!.GetValue<int>()));
        Assert.Equal(report["notes"]!.GetValue<int>(), sessions.Sum(s => s["notes"]!.GetValue<int>()));
        Assert.Equal(report["events"]!.GetValue<int>(), sessions.Sum(s => s["events"]!.GetValue<int>()));
    }

    /// <summary>
    /// A session that connected and did nothing is its own row rather than absorbed into a neighbour.
    /// </summary>
    [Fact]
    public void TestAnEmptySessionIsVisibleRatherThanAbsorbed()
    {
        Events(
            """{"ts":"2026-08-31T09:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T09:00:01.000Z","type":"script.ok","script":"scripts/0001.js"}""",
            """{"ts":"2026-08-31T10:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T10:00:28.000Z","type":"run.end"}""");

        var sessions = Sessions(Report());

        Assert.Equal(2, sessions.Length);
        Assert.Equal(0, sessions[1]["scriptsExecuted"]!.GetValue<int>());
        Assert.Empty((sessions[1]["stages"] as JsonArray)!);
        Assert.Equal(28_000, sessions[1]["ms"]!.GetValue<long>());
    }

    /// <summary>
    /// <c>run.end</c> is a host guarantee rather than ours, so a session without one still ends.
    /// </summary>
    /// <remarks>
    /// It is written from <c>ApplicationStopping</c>, reached only when the host closes the server's
    /// stdin — and the .NET reference client never does. Terminating a session on <c>run.end</c>
    /// would report every such run as still going.
    /// </remarks>
    [Fact]
    public void TestASessionWithNoRunEndIsStillClosedByTheNextRunStart()
    {
        Events(
            """{"ts":"2026-08-31T09:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T09:00:05.000Z","type":"script.ok","script":"scripts/0001.js"}""",
            """{"ts":"2026-08-31T11:00:00.000Z","type":"run.start"}""");

        var sessions = Sessions(Report());

        Assert.Equal(2, sessions.Length);
        Assert.False(sessions[0]["closed"]!.GetValue<bool>());
        Assert.Equal(5_000, sessions[0]["ms"]!.GetValue<long>());
    }

    /// <summary>Events before the first <c>run.start</c> are their own leading session, not dropped.</summary>
    [Fact]
    public void TestEventsBeforeAnyRunStartAreKeptAsALeadingSession()
    {
        Events(
            """{"ts":"2026-08-31T09:00:00.000Z","type":"note","message":"orphaned"}""",
            """{"ts":"2026-08-31T09:00:01.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T09:00:02.000Z","type":"note","message":"in a session"}""");

        var sessions = Sessions(Report());

        Assert.Equal(2, sessions.Length);
        Assert.Null(sessions[0]["started"]);
        Assert.Equal(1, sessions[0]["notes"]!.GetValue<int>());
    }

    /// <summary>A log with no timestamps still splits; only the durations are unknown.</summary>
    /// <remarks>
    /// Null rather than zero, because "we could not tell" and "it took no time" are different
    /// readings of a session and only one of them can be true of one that ran.
    /// </remarks>
    [Fact]
    public void TestAnUntimedLogReportsNoDurationRatherThanZero()
    {
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"run.start"}""");

        var sessions = Sessions(Report());

        Assert.Equal(2, sessions.Length);
        Assert.Null(sessions[0]["ms"]);
    }

    /// <summary>
    /// One session leaves the stage sequence where it has always been, in the summary.
    /// </summary>
    /// <remarks>
    /// The breakdown answers a question a single-session project does not raise, and a table of one
    /// row would be noise in the common case.
    /// </remarks>
    [Fact]
    public void TestASingleSessionKeepsTheStagesInTheSummary()
    {
        Events(
            """{"ts":"2026-08-31T09:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T09:00:01.000Z","type":"stage.begin","stage":"Blocking"}""",
            """{"ts":"2026-08-31T09:00:02.000Z","type":"stage.begin","stage":"Refine"}""");

        var printed = Printed();

        Assert.Single(Sessions(Report()));
        Assert.Contains("Blocking -> Refine", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("listed below", printed, StringComparison.Ordinal);
    }

    /// <summary>Several sessions replace that row with a pointer, and print the breakdown.</summary>
    [Fact]
    public void TestSeveralSessionsReplaceTheConcatenatedStageRow()
    {
        Events(
            """{"ts":"2026-08-31T09:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T09:00:01.000Z","type":"stage.begin","stage":"Blocking"}""",
            """{"ts":"2026-08-31T11:00:00.000Z","type":"run.start"}""",
            """{"ts":"2026-08-31T11:00:01.000Z","type":"stage.begin","stage":"Blocking"}""");

        var printed = Printed();

        Assert.Contains("2 across 2 sessions", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocking -> Blocking", printed, StringComparison.Ordinal);
        Assert.Contains("Sessions", printed, StringComparison.Ordinal);
    }
    #endregion

    #region Fields
    private readonly string root;
    #endregion

    #region Tests (documents)
    /// <summary>A run that read a document says so in the table, not only in the JSON.</summary>
    /// <remarks>
    /// <b>The regression this guards.</b> <c>documentsRead</c> and <c>documentsRefused</c> were
    /// computed and rendered nowhere, so a live run that sent a client's PDF to a third-party model
    /// four times printed <c>requisitions none</c> and said nothing else. The JSON was right the
    /// whole time, which is exactly the case <c>Printed()</c> exists for: what is <em>shown</em> is a
    /// separate decision from what is counted, and the table is what a director actually reads.
    /// <para>
    /// It matters more here than for any other row, because this is the one surface that sends the
    /// director's own file somewhere else. A record that cannot answer "what left the machine" fails
    /// at the thing it is for.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestDocumentReadsReachThePrintedTable()
    {
        Events(RunStart, DocReadFresh, DocReadFresh);

        Assert.Equal(2, Report()["documentsRead"]!.GetValue<int>());
        Assert.Contains("documents read", Printed(), StringComparison.Ordinal);
        Assert.Contains("2 read", Printed(), StringComparison.Ordinal);
    }

    /// <summary>A cached read is reported apart from a billed one.</summary>
    /// <remarks>
    /// "3 read" and "3 read (2 from cache)" are different answers to what the run cost, and only the
    /// second is true when the cache did the work. The first overstates what was sent away.
    /// </remarks>
    [Fact]
    public void TestCachedReadsAreDistinguishedFromBilledOnes()
    {
        Events(RunStart, DocReadFresh, DocReadCached, DocReadCached);

        Assert.Equal(3, Report()["documentsRead"]!.GetValue<int>());
        Assert.Equal(2, Report()["documentsCached"]!.GetValue<int>());
        Assert.Contains("3 read (2 from cache)", Printed(), StringComparison.Ordinal);
    }

    /// <summary>A refusal is shown too, because a refused read is a fact about the run.</summary>
    [Fact]
    public void TestRefusedReadsAreShown()
    {
        Events(RunStart, DocReadFresh, DocRefused);

        Assert.Contains("1 refused", Printed(), StringComparison.Ordinal);
    }

    /// <summary>A run that read nothing says none rather than omitting the row.</summary>
    /// <remarks>
    /// An absent row and a zero look identical to a reader who does not know the row exists — and a
    /// row nobody knew was missing is the whole defect here. "none" is a statement; silence is not.
    /// </remarks>
    [Fact]
    public void TestARunThatReadNoDocumentsSaysNone()
    {
        Events(RunStart, ScriptOk);

        Assert.Equal(0, Report()["documentsRead"]!.GetValue<int>());
        Assert.Matches(@"documents read\s+none", Printed());
    }
    #endregion

    #region Fields (document fixtures)
    private const string RunStart = """{"type":"run.start"}""";
    private const string ScriptOk = """{"type":"script.ok","script":"scripts/0001.js"}""";

    private const string DocReadFresh =
        """{"type":"document.read","kind":"document","descriptor":"documents/a.pdf","fromCache":false}""";

    private const string DocReadCached =
        """{"type":"document.read","kind":"document","descriptor":"documents/a.pdf","fromCache":true}""";

    private const string DocRefused =
        """{"type":"document.refused","kind":"document","descriptor":"documents/missing.pdf"}""";
    #endregion
}
