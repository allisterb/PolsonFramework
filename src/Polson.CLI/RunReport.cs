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

        // The host's own transcript, if a hook preserved it. Not part of the reconciliation — it is
        // written by the hook rather than by the engine — but its presence is what tells a reader
        // whether the conversation behind the run is still recoverable.
        var chatLogs = Files(dir, "events")
            .Where(f => f.StartsWith("chat-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var stages = events.Where(e => Type(e) == "stage.begin")
            .Select(e => e["stage"]?.GetValue<string>() ?? "")
            .Where(s => s.Length > 0)
            .ToArray();

        return new JsonObject
        {
            ["hasEventLog"] = hasLog,
            ["events"] = events.Count,
            ["runStarts"] = Count("run.start"),
            ["scriptsExecuted"] = scriptsRun.Length,
            ["scriptsFailed"] = Count("script.error"),
            ["renders"] = Count("render"),
            ["notes"] = Count("note"),
            ["stages"] = new JsonArray([.. stages.Select(s => (JsonNode)s!)]),
            ["scriptFilesOnDisk"] = scriptFiles.Length,
            ["artifactFilesOnDisk"] = artifactFiles.Length,
            ["chatLogs"] = chatLogs.Length,
            ["scriptFilesNeverExecuted"] = new JsonArray([.. scriptsNeverRun.Select(s => (JsonNode)s!)]),
            ["artifactsNoRenderProduced"] = new JsonArray([.. unexplainedArtifacts.Select(s => (JsonNode)s!)]),
            ["warnings"] = new JsonArray([.. Warnings(hasLog, scriptsRun.Length, Count("render"),
                scriptFiles.Length, scriptsNeverRun.Length, unexplainedArtifacts.Length)
                .Select(w => (JsonNode)w!)])
        };
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
        int scriptFiles, int neverRun, int unexplained)
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

        return warnings;
    }

    private static string[] Files(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        return Directory.Exists(path)
            ? [.. Directory.EnumerateFiles(path).Select(Path.GetFileName).Where(f => f is not null).Select(f => f!)]
            : [];
    }

    private static void Print(string dir, JsonObject report)
    {
        int Num(string key) => report[key]?.GetValue<int>() ?? 0;

        Console.WriteLine();
        Console.WriteLine($"  Run report: {dir}");
        Console.WriteLine();

        var rows = new (string Label, string Value)[]
        {
            ("events recorded", Num("events").ToString(CultureInfo.InvariantCulture)),
            ("server sessions", Num("runStarts").ToString(CultureInfo.InvariantCulture)),
            ("scripts executed", Num("scriptsExecuted").ToString(CultureInfo.InvariantCulture)),
            ("scripts failed", Num("scriptsFailed").ToString(CultureInfo.InvariantCulture)),
            ("renders recorded", Num("renders").ToString(CultureInfo.InvariantCulture)),
            ("stage notes", Num("notes").ToString(CultureInfo.InvariantCulture)),
            ("stages declared", string.Join(" -> ", (report["stages"] as JsonArray ?? []).Select(s => s?.ToString() ?? ""))),
            ("files in scripts/", Num("scriptFilesOnDisk").ToString(CultureInfo.InvariantCulture)),
            ("files in artifacts/", Num("artifactFilesOnDisk").ToString(CultureInfo.InvariantCulture)),
            ("chat logs preserved", Num("chatLogs") == 0
                ? "none — the session's conversation was not kept"
                : Num("chatLogs").ToString(CultureInfo.InvariantCulture)),
        };

        foreach (var (label, value) in rows)
        {
            Console.WriteLine($"    {label,-22} {value}");
        }

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
