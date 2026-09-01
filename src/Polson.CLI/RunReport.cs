namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;

/// <summary>
/// Reads a project's event log and says what the engine actually did.
/// </summary>
/// <remarks>
/// A run directory can look complete and be empty of work. An agent can write plausible scripts and
/// finished-looking artifacts with its own file tools, declare stages it never entered, and audit
/// itself in prose — and the directory afterwards is indistinguishable, by eye, from one where every
/// mark was drawn through the engine.
/// <para>
/// The event log is the half that cannot be written by hand: the server appends to it as it executes
/// scripts and saves renders. So the check is a comparison rather than a policy — what the directory
/// claims, against what the log recorded. Nothing here polices an agent; it makes the difference
/// visible to whoever reads the run.
/// </para>
/// <para>
/// This deliberately reports rather than judges. A run with no renders may be an agent that skipped
/// the engine, or a session that stopped after planning; the report states the counts and leaves the
/// reading to the director.
/// </para>
/// </remarks>
internal static class RunReport
{
    #region Methods
    internal static int Run(ReportOptions opts)
    {
        var dir = Path.GetFullPath(opts.ProjectDir);
        if (!Directory.Exists(dir))
        {
            AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape($"No such directory: {dir}")}");
            return 1;
        }

        var log = Path.Combine(dir, "events", "server.jsonl");
        var events = ReadEvents(log);
        var report = Summarise(dir, events, File.Exists(log));

        if (opts.Json)
        {
            Console.WriteLine(report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Print(dir, report);
        return 0;
    }
    #endregion

    #region Methods (private)
    private static List<JsonNode> ReadEvents(string path)
    {
        var events = new List<JsonNode>();
        if (!File.Exists(path)) return events;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                // A truncated final line is normal for a run that was killed rather than closed;
                // skip it rather than refusing to report on everything before it.
                if (JsonNode.Parse(line) is { } node) events.Add(node);
            }
            catch (JsonException)
            {
                // Ignore: see above.
            }
        }
        return events;
    }

    private static JsonObject Summarise(string dir, List<JsonNode> events, bool hasLog)
    {
        string Type(JsonNode e) => e["type"]?.GetValue<string>() ?? "";
        int Count(string type) => events.Count(e => Type(e) == type);

        var scriptsRun = events.Where(e => Type(e) is "script.ok" or "script.error")
            .Select(e => e["script"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rendered = events.Where(e => Type(e) == "render")
            .Select(e => e["artifact"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .Select(s => Path.GetFileName(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scriptFiles = Files(dir, "scripts");
        var artifactFiles = Files(dir, "artifacts");

        // The two comparisons that matter: files the engine never produced.
        var unexplainedArtifacts = artifactFiles.Where(f => !rendered.Contains(f)).ToArray();
        var scriptsNeverRun = scriptFiles
            .Where(f => !scriptsRun.Any(s => Path.GetFileName(s).Equals(f, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        // The conversation behind the run. Never part of the reconciliation — it is written by the
        // host or the orchestrator rather than by the engine — but its presence is what tells a reader
        // whether the session is still recoverable. It arrives by one of two routes, and a run has
        // whichever its profile gives it: under a desktop host the preserve-chatlog hook copies the
        // host's transcript in as events/chat-*.jsonl; under the orchestrator there is no host to copy
        // from, because the orchestrator is itself holding the conversation, and it writes
        // events/agent.jsonl as it consumes the turn. Reporting only the first called every standalone
        // run unrecorded while its transcript sat in the same directory.
        var chatLogs = Files(dir, "events")
            .Where(f => f.StartsWith("chat-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var agentLog = Path.Combine(dir, "events", "agent.jsonl");
        var agentEvents = ReadEvents(agentLog);
        var agentTurns = agentEvents.Count(e => Type(e) == "turn.start");

        // Steps, not events. The run and turn brackets are written whether or not the agent ever said
        // anything, so counting every line would report an empty transcript as a full one — and a
        // transcript that exists but recorded nothing is a distinct state worth seeing.
        var agentSteps = agentEvents.Count(e => Type(e) is not ("run.start" or "run.end" or "turn.start" or "turn.end"));

        // What the run perceived, not just what it produced. An `inspect` event carries a tally per
        // execution; `artifact.read` names a file an earlier pass wrote. The second is the one worth
        // a line of its own: it is the only direct evidence in the record that one pass built on
        // another through the environment rather than from its own context.
        var probes = events.Where(e => Type(e) == "inspect")
            .Sum(e => e["total"]?.GetValue<int>() ?? 0);

        var artifactsRead = events.Where(e => Type(e) == "artifact.read")
            .Select(e => e["artifact"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var stages = events.Where(e => Type(e) == "stage.begin")
            .Select(e => e["stage"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .ToArray();

        // Claims the run made about its own work, and how they turned out. This is the only part of
        // the record that can be *wrong* rather than merely absent, which is what makes it worth
        // reading first: a run with no checks has not verified anything, however much it inspected.
        var checks = events.Where(e => Type(e) == "check").ToArray();
        var checksFailed = checks.Count(e => e["passed"]?.GetValue<bool>() == false);

        var failedClaims = checks
            .Where(e => e["passed"]?.GetValue<bool>() == false)
            .Select(e => e["claim"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .ToArray();

        return new JsonObject
        {
            ["hasEventLog"] = hasLog,
            ["events"] = events.Count,
            ["runStarts"] = Count("run.start"),
            ["sessions"] = new JsonArray([.. Sessions(events)]),
            ["scriptsExecuted"] = scriptsRun.Length,
            ["scriptsFailed"] = Count("script.error"),
            ["renders"] = Count("render"),
            ["notes"] = Count("note"),
            ["inspections"] = Count("inspect"),
            ["probes"] = probes,
            ["observations"] = Count("observe"),
            ["expectations"] = Count("expect"),
            ["checks"] = checks.Length,
            ["checksFailed"] = checksFailed,
            ["claimsNotMet"] = new JsonArray([.. failedClaims.Select(c => (JsonNode)c!)]),
            ["requisitions"] = Count("asset.requisition"),
            ["requisitionsRefused"] = Count("asset.refused"),
            ["artifactsRead"] = new JsonArray([.. artifactsRead.Select(a => (JsonNode)a!)]),
            ["stages"] = new JsonArray([.. stages.Select(s => (JsonNode)s!)]),
            ["scriptFilesOnDisk"] = scriptFiles.Length,
            ["artifactFilesOnDisk"] = artifactFiles.Length,
            ["chatLogs"] = chatLogs.Length,
            ["agentTranscript"] = File.Exists(agentLog),
            ["agentTurns"] = agentTurns,
            ["agentSteps"] = agentSteps,
            ["conversationRecord"] = ConversationRecord(chatLogs.Length, File.Exists(agentLog), agentTurns, agentSteps),
            ["scriptFilesNeverExecuted"] = new JsonArray([.. scriptsNeverRun.Select(s => (JsonNode)s!)]),
            ["artifactsNoRenderProduced"] = new JsonArray([.. unexplainedArtifacts.Select(s => (JsonNode)s!)]),
            ["warnings"] = new JsonArray([.. Warnings(hasLog, scriptsRun.Length, Count("render"),
                scriptFiles.Length, scriptsNeverRun.Length, unexplainedArtifacts.Length,
                Count("expect"), checks.Length, checksFailed)
                .Select(w => (JsonNode)w!)])
        };
    }

    /// <summary>
    /// The log split at each <c>run.start</c>, because a project's log is appended to across runs.
    /// </summary>
    /// <remarks>
    /// Everything else here is a total for the <em>project</em>, which is the right scope for the
    /// reconciliation — an artifact with no render is unaccounted for whenever it was written. The
    /// shape of the work is not: a project run four times reports its stages as
    /// <c>Data -&gt; … -&gt; Encode -&gt; Data -&gt; … -&gt; Encode</c>, which reads as one long confused run
    /// rather than as four ordinary ones.
    /// <para>
    /// A session is opened by <c>run.start</c> and runs until the next one. <c>run.end</c> is
    /// deliberately <em>not</em> the terminator: it is written from <c>ApplicationStopping</c> and is
    /// only reached when the host closes the server's stdin, so a session whose host killed the
    /// process instead has no closing event — and is not thereby incomplete or still running.
    /// </para>
    /// <para>
    /// Anything before the first <c>run.start</c> becomes a leading session with no start time,
    /// rather than being folded into the first real one or silently dropped. A log with no
    /// <c>run.start</c> at all is therefore one session rather than none.
    /// </para>
    /// </remarks>
    private static List<JsonObject> Sessions(List<JsonNode> events)
    {
        var sessions = new List<JsonObject>();
        var current = new List<JsonNode>();
        string? started = null;

        foreach (var e in events)
        {
            if (TypeOf(e) == "run.start")
            {
                // What has been collected belongs to the session before this one — or, at the head of
                // a log, to a stretch that never had a run.start of its own.
                if (current.Count > 0 || started is not null)
                {
                    sessions.Add(Session(sessions.Count + 1, started, current));
                    current = [];
                }
                started = Stamp(e);
            }
            current.Add(e);
        }

        if (current.Count > 0 || started is not null)
        {
            sessions.Add(Session(sessions.Count + 1, started, current));
        }

        return sessions;
    }

    /// <summary>One session's own counts, on the same definitions the project totals use.</summary>
    /// <remarks>
    /// Scripts are counted by distinct path exactly as they are project-wide, and the server numbers
    /// each execution into a new file, so the per-session counts sum back to the total. A breakdown
    /// that did not add up would be worse than no breakdown.
    /// </remarks>
    private static JsonObject Session(int index, string? started, List<JsonNode> events)
    {
        var scripts = events.Where(e => TypeOf(e) is "script.ok" or "script.error")
            .Select(e => e["script"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var stages = events.Where(e => TypeOf(e) == "stage.begin")
            .Select(e => e["stage"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .ToArray();

        // The last event carrying a timestamp, not the last event: a hand-written or older log may
        // have lines without one, and the session still ran for as long as its stamped lines say.
        var ended = events.Select(Stamp).LastOrDefault(t => t is not null);

        return new JsonObject
        {
            ["index"] = index,
            ["started"] = started,
            ["ended"] = ended,
            ["ms"] = Elapsed(started, ended),
            ["events"] = events.Count,
            ["scriptsExecuted"] = scripts,
            ["scriptsFailed"] = events.Count(e => TypeOf(e) == "script.error"),
            ["renders"] = events.Count(e => TypeOf(e) == "render"),
            ["notes"] = events.Count(e => TypeOf(e) == "note"),
            ["closed"] = events.Any(e => TypeOf(e) == "run.end"),
            ["stages"] = new JsonArray([.. stages.Select(s => (JsonNode)s!)])
        };
    }

    private static string TypeOf(JsonNode e) => e["type"]?.GetValue<string>() ?? "";

    private static string? Stamp(JsonNode e) => e["ts"]?.GetValue<string>();

    /// <summary>Milliseconds between two record timestamps, or null if either cannot be read.</summary>
    /// <remarks>
    /// Null rather than zero, because "we could not tell" and "it took no time" are different
    /// readings and only one of them is ever true of a session that ran.
    /// </remarks>
    private static long? Elapsed(string? from, string? to)
    {
        if (from is null || to is null) return null;

        const DateTimeStyles styles = DateTimeStyles.RoundtripKind;
        if (!DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, styles, out var start)) return null;
        if (!DateTimeOffset.TryParse(to, CultureInfo.InvariantCulture, styles, out var end)) return null;

        var ms = (long)(end - start).TotalMilliseconds;
        return ms < 0 ? null : ms;
    }

    /// <summary>
    /// The readings worth surfacing, in the order a director would want them.
    /// </summary>
    /// <remarks>
    /// Each is phrased as an observation with its evidence rather than as a verdict. "0 renders"
    /// is a fact; whether it means the work was done elsewhere or the run stopped early is a
    /// judgement the reader is better placed to make than this is.
    /// </remarks>
    private static List<string> Warnings(bool hasLog, int executed, int renders,
        int scriptFiles, int neverRun, int unexplained, int expectations, int checks, int checksFailed)
    {
        var warnings = new List<string>();

        if (!hasLog)
        {
            warnings.Add("No events/server.jsonl. The MCP server never started in this directory, so nothing here was produced through the engine.");
            return warnings;
        }

        if (executed == 0)
        {
            warnings.Add("No scripts were executed. The server connected but was never asked to run anything.");
        }

        if (renders == 0)
        {
            warnings.Add("No renders were recorded. Nothing was drawn through the engine — any image in artifacts/ was made another way.");
        }

        if (scriptFiles > 0 && neverRun == scriptFiles)
        {
            warnings.Add($"All {scriptFiles} file(s) in scripts/ are unaccounted for in the log. The server numbers what it runs (0001.js, 0002.js); differently named files were written by hand.");
        }
        else if (neverRun > 0)
        {
            warnings.Add($"{neverRun} file(s) in scripts/ do not correspond to an executed script.");
        }

        if (unexplained > 0)
        {
            warnings.Add($"{unexplained} file(s) in artifacts/ have no render event. They were not written by the engine.");
        }

        // A claim stated and never settled is worse than one never made: it reads as verification and
        // is not. Counted rather than matched by text, because the claim in an `expect` and the claim
        // in its `check` are written independently and rarely agree word for word — a count says
        // truthfully that something was left open without pretending to know which.
        if (expectations > checks)
        {
            warnings.Add($"{expectations - checks} of {expectations} stated expectation(s) were never settled with a check. "
                + "An expectation with no verdict looks like verification and is not.");
        }

        // The check that fails is the one that identifies the fault; the check that passes afterwards
        // is the only evidence the fix worked. A run whose every check failed has diagnosed and not
        // demonstrated — which is exactly how a corrected painting came to be indistinguishable from
        // an uncorrected one.
        if (checks > 0 && checksFailed == checks)
        {
            warnings.Add($"All {checks} check(s) failed and none was re-run after a fix. The record shows what was "
                + "wrong and not that anything was put right.");
        }

        return warnings;
    }

    private static string[] Files(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        return Directory.Exists(path)
            ? [.. Directory.EnumerateFiles(path).Select(Path.GetFileName).Where(f => f is not null).Select(f => f!)]
            : [];
    }

    /// <summary>
    /// How the run's conversation was kept, in words.
    /// </summary>
    /// <remarks>
    /// A transcript that exists but recorded nothing is reported as such rather than as absent: the
    /// orchestrator writes the file when the run starts, so its mere presence proves only that a run
    /// was attempted. Both routes are reported when both are present, which is what a managed project
    /// later re-run through the orchestrator looks like.
    /// </remarks>
    private static string ConversationRecord(int chatLogs, bool agentTranscript, int turns, int steps)
    {
        var parts = new List<string>();

        if (chatLogs > 0)
        {
            parts.Add($"{chatLogs} chat log{(chatLogs == 1 ? "" : "s")} copied in by the hook");
        }

        if (steps > 0)
        {
            parts.Add($"events/agent.jsonl — {turns} turn{(turns == 1 ? "" : "s")}, {steps} steps");
        }
        else if (agentTranscript)
        {
            parts.Add("events/agent.jsonl, but it recorded no steps");
        }

        return parts.Count > 0
            ? string.Join("; ", parts)
            : "none — the session's conversation was not kept";
    }

    /// <summary>
    /// Which earlier artifacts this run consulted, which is what stigmergy looks like in the record.
    /// </summary>
    private static string Read(JsonObject report)
    {
        var read = (report["artifactsRead"] as JsonArray ?? []).Select(a => a?.ToString() ?? "").ToArray();
        return read.Length == 0
            ? "none - no pass built on an earlier pass's render"
            : string.Join(", ", read);
    }

    /// <summary>
    /// Each session on its own, when there is more than one to tell apart.
    /// </summary>
    /// <remarks>
    /// Silent for a single-session project: the summary already says everything, and a table of one
    /// row is noise. Two lines per session rather than a column layout, because a stage sequence is
    /// as long as it is and columns would either truncate it or run off the terminal.
    /// </remarks>
    private static void PrintSessions(JsonArray sessions)
    {
        if (sessions.Count < 2) return;

        Console.WriteLine();
        Console.WriteLine("  Sessions");
        Console.WriteLine();

        for (var i = 0; i < sessions.Count; i++)
        {
            if (sessions[i] is not JsonObject session) continue;

            int Num(string key) => session[key]?.GetValue<int>() ?? 0;

            var failed = Num("scriptsFailed");
            var counts = $"{Num("scriptsExecuted")} scripts{(failed > 0 ? $" ({failed} failed)" : "")}, "
                       + $"{Num("renders")} renders, {Num("notes")} notes";

            Console.WriteLine($"    {Num("index"),-2} {Began(session),-23} {Duration(session),-9} {counts}");

            var stages = session["stages"] as JsonArray ?? [];
            Console.WriteLine(stages.Count == 0
                ? "       (no stage declared)"
                : $"       {string.Join(" -> ", stages.Select(s => s?.ToString() ?? ""))}");

            // No trailing blank on the last: the caller writes one before the warnings, and two
            // together read as the report having finished early.
            if (i < sessions.Count - 1) Console.WriteLine();
        }
    }

    /// <summary>When a session opened, to the second. UTC, as the record keeps it.</summary>
    private static string Began(JsonObject session)
    {
        var started = session["started"]?.GetValue<string>();
        if (started is null) return "(before any run.start)";

        return DateTimeOffset.TryParse(started, CultureInfo.InvariantCulture,
                                       DateTimeStyles.RoundtripKind, out var at)
            ? at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : started;
    }

    /// <summary>How long a session ran, in the largest unit that still says something.</summary>
    private static string Duration(JsonObject session)
    {
        if (session["ms"]?.GetValue<long>() is not { } ms) return "-";

        var span = TimeSpan.FromMilliseconds(ms);
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes:00}m";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m {span.Seconds:00}s";
        return $"{span.TotalSeconds:0.0}s";
    }

    private static void Print(string dir, JsonObject report)
    {
        int Num(string key) => report[key]?.GetValue<int>() ?? 0;

        Console.WriteLine();
        Console.WriteLine($"  Run report: {dir}");
        Console.WriteLine();

        var sessions = report["sessions"] as JsonArray ?? [];
        var stages = report["stages"] as JsonArray ?? [];

        var rows = new List<(string Label, string Value)>
        {
            ("events recorded", Num("events").ToString(CultureInfo.InvariantCulture)),
            ("server sessions", Num("runStarts").ToString(CultureInfo.InvariantCulture)),
            ("scripts executed", Num("scriptsExecuted").ToString(CultureInfo.InvariantCulture)),
            ("scripts failed", Num("scriptsFailed").ToString(CultureInfo.InvariantCulture)),
            ("renders recorded", Num("renders").ToString(CultureInfo.InvariantCulture)),
            ("stage notes", Num("notes").ToString(CultureInfo.InvariantCulture)),
            ("looked before drawing", Num("inspections") == 0
                ? "never - no script measured, sampled or read anything back"
                : $"{Num("probes")} probes across {Num("inspections")} of {Num("scriptsExecuted")} scripts"),
            ("what it found", Num("observations") == 0
                ? "nothing recorded - no comparison or palette reached the record"
                : $"{Num("observations")} measurement outcome(s)"),

            // The one row that can be wrong rather than merely absent, so it reads as a sentence
            // instead of a count: "3 stated, 1 settled, 1 failed" is the shape of a half-done audit
            // and should look like one at a glance.
            ("claims about the work", Num("expectations") == 0 && Num("checks") == 0
                ? "none - the run asserted nothing about its own output"
                : $"{Num("expectations")} stated, {Num("checks")} settled, {Num("checksFailed")} failed"),
            ("artifacts read back", Read(report)),
            ("requisitions", Num("requisitions") == 0 && Num("requisitionsRefused") == 0
                ? "none"
                : $"{Num("requisitions")} attempted, {Num("requisitionsRefused")} refused as form"),

            // Concatenated across sessions this row is the misleading one, so it says so and hands
            // the reader to the breakdown instead of printing four runs as one stage sequence.
            ("stages declared", sessions.Count > 1
                ? $"{stages.Count} across {sessions.Count} sessions - listed below"
                : string.Join(" -> ", stages.Select(s => s?.ToString() ?? ""))),
            ("files in scripts/", Num("scriptFilesOnDisk").ToString(CultureInfo.InvariantCulture)),
            ("files in artifacts/", Num("artifactFilesOnDisk").ToString(CultureInfo.InvariantCulture)),
            ("conversation record", report["conversationRecord"]?.ToString() ?? ""),
        };

        foreach (var (label, value) in rows)
        {
            Console.WriteLine($"    {label,-22} {value}");
        }

        PrintSessions(sessions);

        var warnings = report["warnings"] as JsonArray ?? [];
        Console.WriteLine();
        if (warnings.Count == 0)
        {
            Console.WriteLine("    Nothing unaccounted for: every artifact traces to a render, every script to an execution.");
        }
        else
        {
            foreach (var warning in warnings)
            {
                Console.WriteLine($"    !  {warning}");
            }
        }
        Console.WriteLine();
    }
    #endregion
}
