namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Copies the host's chat transcript into the project, as a hook.
/// </summary>
/// <remarks>
/// The run record already holds what the <i>engine</i> did — every script, render and stage note.
/// What it cannot hold is the conversation that produced them: the director's asks, the agent's
/// reasoning, the turns where a direction was abandoned. That lives in the host's own transcript,
/// outside the project, and disappears with the session. This brings it inside, so a finished run is
/// readable end to end from one directory.
/// <para>
/// Two hosts, two payload shapes, one verb. Claude Code sends <c>transcript_path</c> /
/// <c>session_id</c> and exports <c>CLAUDE_PROJECT_DIR</c>; Antigravity sends <c>transcriptPath</c> /
/// <c>conversationId</c> / <c>workspacePaths</c>. Whichever arrives is used.
/// </para>
/// <para>
/// <b>Standard output is the hook's reply channel, not a log.</b> Antigravity requires a JSON object
/// on stdout and treats anything else as a malformed hook, so stdout carries <c>{}</c> and nothing
/// else — one <c>Console.Out.Write</c>, deliberately not a logger call.
/// </para>
/// <para>
/// <b>Everything else goes to the log file</b>, via <c>Runtime.*</c>. A hook's standard error goes
/// wherever the host decides, which is usually nowhere: writing diagnostics there meant that when
/// this failed, it failed invisibly, and the only way to see why was to run the command by hand.
/// <c>Program</c> gives the hook its own <c>CLI-hook</c> log for exactly this reason. Note that log
/// is file-only, so running the verb manually now prints nothing but the <c>{}</c> — read the log
/// rather than the console.
/// </para>
/// <para>
/// <b>Best-effort, always.</b> A hook that throws takes the session with it, and a lost chat log is
/// worth far less than a lost run. Every failure path here records a line and returns success.
/// </para>
/// </remarks>
internal static class ChatlogPreserver
{
    #region Methods
    internal static int Run(PreserveChatlogOptions? opts = null)
    {
        // Standard output belongs to the host's JSON reply and to nothing else, so it is taken away
        // from everything else for the duration. Not paranoia: the logger writes to the console
        // unless file logging happens to have been configured first, and a single stray line on
        // stdout makes the hook malformed on every turn — a failure that would present as the whole
        // integration being broken rather than as a misdirected log line. Anything that does print
        // lands on stderr, where a person running this by hand can still see it.
        var reply = Console.Out;
        Console.SetOut(Console.Error);

        try
        {
            Preserve(opts?.Host ?? string.Empty, opts?.ProjectDir ?? string.Empty);
        }
        catch (Exception ex)
        {
            Runtime.Error(ex, "preserve-chatlog failed: {0}", ex.Message);
        }
        finally
        {
            Console.SetOut(reply);
        }

        // The reply the host is waiting for. Written whatever happened above.
        reply.Write("{}");
        return 0;
    }
    #endregion

    #region Methods (private)
    private static void Preserve(string host, string projectDir)
    {
        // A piped payload can arrive with a BOM or surrounding whitespace depending on the host.
        var stdin = (Console.In.ReadToEnd() ?? string.Empty).Trim().TrimStart('﻿');
        if (stdin.Length == 0)
        {
            Runtime.Warn("preserve-chatlog: empty payload; nothing to do.");
            return;
        }

        var payload = JsonNode.Parse(stdin) as JsonObject;
        if (payload is null)
        {
            Runtime.Warn("preserve-chatlog: payload was not a JSON object.");
            return;
        }

        var project = ProjectDirectory(payload, projectDir);
        var events = Path.Combine(project, "events");
        Directory.CreateDirectory(events);

        var session = Text(payload, "session_id", "conversationId") ?? "session";
        var (source, how) = Locate(payload, host, session, events);

        // One line per firing, whatever the outcome. Without it "did the hook run?" is
        // unanswerable — stderr goes wherever the host sends it, which is usually nowhere, so a
        // hook that never fired and one that fired and found nothing look identical from the
        // project. This is the only evidence either way, and it is what settles which resolution
        // strategy a given host actually needs.
        Trace(events, payload, host, how, source);

        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            Runtime.Warn("preserve-chatlog: no readable transcript (resolved by {0}: {1}).",
                how, source ?? "none given");
            return;
        }

        Remember(events, source);

        // One file per session, overwritten. These hooks fire every turn, so a timestamped name
        // would leave one whole-transcript copy per turn — the same conversation, N times over.
        var destination = Path.Combine(events, $"chat-{Safe(session)}.jsonl");
        File.Copy(source, destination, overwrite: true);
        Runtime.Info("preserve-chatlog: chat log -> {0} (resolved by {1})", destination, how);

        PreserveUncompacted(source, events, session);

        PreserveSubagents(source, events);

        WriteTokenUsage(source, events, session);
    }

    /// <summary>
    /// Where the project is, asking the host before guessing.
    /// </summary>
    /// <remarks>
    /// The cwd is the last resort rather than the first: a hook runs wherever the host happens to
    /// have started, and writing the transcript into the wrong directory is worse than not writing
    /// it, because it looks like it worked.
    /// <para>
    /// That is not hypothetical. Antigravity sets no project environment variable and its
    /// <c>PostInvocation</c> and <c>Stop</c> payloads carry no <c>workspacePaths</c>, so this fell
    /// through to the cwd and quietly wrote each transcript into whatever directory the desktop had
    /// launched the hook from — the project's <c>events/</c> stayed empty, the command exited 0, and
    /// nothing anywhere said why. Hence <c>--project-dir</c>, baked in at generation time by the
    /// only party that reliably knows the answer.
    /// </para>
    /// </remarks>
    private static string ProjectDirectory(JsonObject payload, string projectDir)
    {
        if (!string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir)) return projectDir;

        var claude = Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR");
        if (!string.IsNullOrWhiteSpace(claude) && Directory.Exists(claude)) return claude;

        if (payload["workspacePaths"] is JsonArray paths)
        {
            foreach (var path in paths)
            {
                var candidate = path?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate)) return candidate;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Copies the other transcript too, when the host keeps one beside the file it named.
    /// </summary>
    /// <remarks>
    /// Antigravity writes <c>transcript_full.jsonl</c> next to <c>transcript.jsonl</c>, and the
    /// difference is not cosmetic: one of them is <i>compacted</i>, so a long session drops earlier
    /// exchanges from it. Preserving only that one would keep exactly the part of a run a reader
    /// already remembers and lose the reasoning from the beginning, which is usually where a
    /// direction was chosen.
    /// <para>
    /// <b>Either file can be the one the payload names</b>, which is why the guard below matters: a
    /// real run was observed where the payload named <c>transcript_full.jsonl</c> itself, and the
    /// two destinations then held byte-identical copies of it while the other transcript was never
    /// preserved at all.
    /// </para>
    /// </remarks>
    private static void PreserveUncompacted(string source, string events, string session)
    {
        var directory = Path.GetDirectoryName(source);
        if (directory is null) return;

        var full = Path.Combine(directory, "transcript_full.jsonl");

        // Compared as canonical paths rather than as strings. The payload names the transcript with
        // forward slashes and Path.Combine joins with the platform separator, so a string comparison
        // never matched and the same file was copied under both names.
        if (!File.Exists(full) || SamePath(full, source)) return;

        try
        {
            var destination = Path.Combine(events, $"chat-{Safe(session)}-full.jsonl");
            File.Copy(full, destination, overwrite: true);
            Runtime.Info("preserve-chatlog: uncompacted -> {0}", destination);
        }
        catch (Exception ex)
        {
            Runtime.Warn("preserve-chatlog: uncompacted copy skipped: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Copies the subagents' own transcripts, which is where a multi-agent run actually happens.
    /// </summary>
    /// <remarks>
    /// A parent transcript records that a subagent was dispatched and what it returned; it does not
    /// record what the subagent <i>did</i>. In a real <c>comic_studio</c> run the parent was 578 KB
    /// and the penciler's own transcript was <b>2.27 MB</b> — every one of the ten scripts the server
    /// recorded was executed there. Preserving only the parent kept the receipt and lost the work.
    /// <para>
    /// Claude Code writes them to <c>&lt;transcript-dir&gt;/&lt;session&gt;/subagents/</c>, beside a
    /// <c>.meta.json</c> naming the <c>agentType</c> — <c>penciler</c>, <c>inker</c> — which is the
    /// actor attribution the run record has never had. The meta file is copied with the transcript
    /// rather than parsed here: this verb's job is to bring the record inside the project, and
    /// deciding what it means belongs to whatever reads it.
    /// </para>
    /// <para>
    /// The host's own layout is mirrored under <c>events/subagents/</c> rather than flattened into
    /// the <c>chat-*</c> naming, so a reader can tell a subagent's transcript from the parent's
    /// without inspecting it — the two are not interchangeable, and one is <c>isSidechain</c>
    /// throughout.
    /// </para>
    /// <para>
    /// A host that keeps no such directory — Antigravity gives a subagent its own conversation
    /// entirely — finds nothing and does nothing. Best-effort like the rest of this file: a
    /// subagent transcript that cannot be copied must not cost the parent's.
    /// </para>
    /// </remarks>
    private static void PreserveSubagents(string source, string events)
    {
        try
        {
            var directory = Path.GetDirectoryName(source);
            if (directory is null) return;

            // "<dir>/<session>.jsonl" -> "<dir>/<session>/subagents". The session's own folder sits
            // beside its transcript and is named for it without the extension.
            var from = Path.Combine(directory, Path.GetFileNameWithoutExtension(source), "subagents");
            if (!Directory.Exists(from)) return;

            var to = Path.Combine(events, "subagents");
            Directory.CreateDirectory(to);

            var copied = 0;
            foreach (var file in Directory.EnumerateFiles(from, "agent-*.*"))
            {
                var name = Path.GetFileName(file);
                if (!name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
                    && !name.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    // Overwritten every firing, exactly as the parent transcript is: these grow while
                    // the subagent works, so the copy is a snapshot that gets better, not an archive.
                    File.Copy(file, Path.Combine(to, name), overwrite: true);
                    copied += 1;
                }
                catch (Exception ex)
                {
                    Runtime.Warn("preserve-chatlog: subagent {0} skipped: {1}", name, ex.Message);
                }
            }

            if (copied > 0) Runtime.Info("preserve-chatlog: {0} subagent file(s) -> {1}", copied, to);
        }
        catch (Exception ex)
        {
            Runtime.Warn("preserve-chatlog: subagent transcripts skipped: {0}", ex.Message);
        }
    }

    /// <summary>Whether two paths name the same file, whatever separators they were written with.</summary>
    /// <remarks>
    /// Falls back to comparing the strings if either path is malformed. This only decides whether to
    /// make a second copy, so it must never be the thing that fails the hook.
    /// </remarks>
    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Finds the transcript, in decreasing order of how much the host told us.
    /// </summary>
    /// <remarks>
    /// The payload is trusted first, because a host that names its transcript is never wrong about
    /// it. Everything after that is inference, and the strategy that succeeded is recorded so a real
    /// run says which one a given host needs rather than leaving it to be guessed at again.
    /// <para>
    /// The <c>--host</c> fallbacks matter because Antigravity's <c>Stop</c> payload carries a
    /// <c>conversationId</c> and no path, and <c>Stop</c> is the only event after the final turn.
    /// Its transcripts live at
    /// <c>&lt;app_data_dir&gt;/brain/&lt;conversationId&gt;/.system_generated/logs/transcript.jsonl</c>;
    /// the directory is the conversation id the desktop shows beside the chat.
    /// </para>
    /// <para>
    /// There are <b>three</b> stores, one per Antigravity surface, and they share a layout: the
    /// desktop writes <c>antigravity/</c>, the CLI <c>antigravity-cli/</c>, and the IDE
    /// <c>antigravity-ide/</c>. Searching only the desktop's was a real miss — a CLI run kept its
    /// transcript at the identical path under a different root, so the fallback reported nothing
    /// found while the file sat there. All three are searched, desktop first.
    /// <para>
    /// <c>~/.gemini/antigravity/brain/</c> is the <b>default</b> <c>app_data_dir</c>, not a fixed
    /// location — the Python SDK documents overriding it on <c>LocalAgentConfig</c>, and nothing in
    /// the hook payload says where it ended up. An override therefore misses, and misses visibly in
    /// the trace rather than silently copying some other conversation.
    /// </para>
    /// <para>
    /// Preserving promptly matters more than it looks: the desktop's <c>sessionRetention</c> setting
    /// deletes transcripts after an age (30 days by default), so the host's copy is not an archive.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Antigravity's per-surface data roots under <c>~/.gemini</c>, in the order to try them.
    /// </summary>
    /// <remarks>
    /// Named by the shipped hook documentation, which lists the surfaces alongside the transcript
    /// path they produce. The layout below each is identical, so only the root differs.
    /// </remarks>
    private static readonly string[] AntigravityStores = ["antigravity", "antigravity-cli", "antigravity-ide"];

    /// <summary>
    /// Where a conversation's transcript could be, one path per Antigravity surface, in search order.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Locate"/> and internal so it can be tested: the home directory comes
    /// from <see cref="Environment.SpecialFolder.UserProfile"/>, which a test cannot move, and the
    /// defect this exists to prevent — searching one store when there are three — is invisible on a
    /// machine that happens to have used the store you did search.
    /// </remarks>
    internal static IEnumerable<string> AntigravityTranscripts(string home, string session) =>
        AntigravityStores.Select(store => Path.Combine(
            home, ".gemini", store, "brain", session, ".system_generated", "logs", "transcript.jsonl"));

    private static (string? Path, string How) Locate(JsonObject payload, string host, string session, string events)
    {
        if (Text(payload, "transcript_path", "transcriptPath") is { } given && File.Exists(given))
        {
            return (given, "payload");
        }

        // Only ever addressed by the session's own id. There is an obvious next fallback — take the
        // most recently written transcript under the host's store — and it is deliberately not here:
        // with any other conversation open it would copy a stranger's session into this run's record
        // and label it as this run. A missing chat log is a gap; a confident wrong one is a lie about
        // what happened, and the trace below would be the only place it was ever contradicted.
        if (host.StartsWith("agy", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var byId in AntigravityTranscripts(Home(), session))
            {
                if (File.Exists(byId)) return (byId, "host:conversationId");
            }
        }

        if (host.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            var projects = Path.Combine(Home(), ".claude", "projects");
            if (Newest(projects, session + ".jsonl") is { } bySession) return (bySession, "host:sessionId");
        }

        return (Remembered(events), "remembered");
    }

    private static string Home() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The most recently written match under a root, or null when there is none.</summary>
    /// <remarks>
    /// A last resort, and a fallible one: with two sessions open it can pick the other one. It earns
    /// its place only because the alternative is preserving nothing, and the trace records when it
    /// was used so a wrong transcript is explicable rather than mysterious.
    /// </remarks>
    private static string? Newest(string root, string pattern)
    {
        if (!Directory.Exists(root)) return null;

        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Appends one line describing this firing: what arrived, what was tried, what happened.
    /// </summary>
    /// <remarks>
    /// The payload's <i>keys</i> are recorded, never its values — the point is to learn a host's
    /// schema, and a transcript's contents do not belong in a second file beside the transcript.
    /// </remarks>
    private static void Trace(string events, JsonObject payload, string host, string how, string? source)
    {
        try
        {
            var line = new JsonObject
            {
                ["ts"] = DateTime.UtcNow.ToString("O"),
                ["host"] = host.Length > 0 ? host : null,
                ["payloadKeys"] = new JsonArray([.. payload.Select(p => (JsonNode)p.Key)]),
                ["resolvedBy"] = how,
                ["transcript"] = source,
                ["found"] = !string.IsNullOrWhiteSpace(source) && File.Exists(source),
            };

            File.AppendAllText(Path.Combine(events, "hooks.jsonl"),
                line.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");
        }
        catch (Exception ex)
        {
            Runtime.Warn("preserve-chatlog: trace skipped: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Remembers the transcript's location for a later event that does not carry it.
    /// </summary>
    /// <remarks>
    /// Antigravity's <c>PreInvocation</c> payload names the transcript; its <c>Stop</c> payload does
    /// not. Since <c>Stop</c> is the only event that fires after the final turn, without this the
    /// last exchange of every session would be missing — the one most likely to hold the conclusion.
    /// </remarks>
    private static void Remember(string events, string source)
    {
        try
        {
            File.WriteAllText(Path.Combine(events, ".chat-source"), source);
        }
        catch (IOException ex)
        {
            Runtime.Warn("preserve-chatlog: could not remember the transcript path: {0}", ex.Message);
        }
    }

    private static string? Remembered(string events)
    {
        var marker = Path.Combine(events, ".chat-source");
        return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
    }

    /// <summary>
    /// Sums the token usage a transcript reports, when it reports any.
    /// </summary>
    /// <remarks>
    /// A Claude Code transcript carries <c>message.usage</c> on each assistant line, which totals to
    /// what the session actually cost. Other hosts may carry nothing of the kind, so an absent or
    /// unfamiliar shape writes no file rather than a file full of zeros — a zero here would read as
    /// a measurement rather than as an absence.
    /// <para>
    /// <b>One assistant message can occupy several lines, and every one of them repeats the same
    /// usage block.</b> Claude Code writes a line per content block — the text, the thinking and each
    /// tool call arrive separately — so summing lines counts one turn two or three times. Measured on
    /// the drawing-1 run: <b>106 lines carrying usage against 60 distinct message ids</b>, a 1.75×
    /// overcount that reported 31.2M tokens for a run that spent 17.9M. Nothing about that figure
    /// looked wrong, which is what made it expensive: it was quoted as a cost, and it was also used
    /// to argue the run had failed to converge over 106 turns when it had drawn 13 renders in 60.
    /// </para>
    /// <para>
    /// So the sum is over <b>message ids</b>, and a line whose id has already been counted is
    /// skipped. A transcript from a host that states no id falls back to counting the line, which is
    /// the old behaviour and is right where one line is one message.
    /// </para>
    /// </remarks>
    private static void WriteTokenUsage(string transcript, string events, string session)
    {
        try
        {
            long input = 0, output = 0, cacheCreate = 0, cacheRead = 0;
            var turns = 0;
            var models = new Dictionary<string, long>(StringComparer.Ordinal);
            var counted = new HashSet<string>(StringComparer.Ordinal);

            foreach (var line in File.ReadLines(transcript))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonNode? node;
                try { node = JsonNode.Parse(line); }
                catch (JsonException) { continue; }

                if (node?["message"] is not JsonObject message) continue;
                if (message["usage"] is not JsonObject usage) continue;

                // The id identifies the model's answer; the line identifies one block of it. Only
                // the first line of a message carries it into the totals.
                var id = message["id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(id) && !counted.Add(id)) continue;

                turns++;
                input += Num(usage, "input_tokens");
                output += Num(usage, "output_tokens");
                cacheCreate += Num(usage, "cache_creation_input_tokens");
                cacheRead += Num(usage, "cache_read_input_tokens");

                var model = message["model"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(model))
                {
                    models[model] = models.GetValueOrDefault(model) + Num(usage, "output_tokens");
                }
            }

            if (turns == 0) return;

            var summary = new JsonObject
            {
                ["session"] = session,
                ["turns"] = turns,
                ["inputTokens"] = input,
                ["outputTokens"] = output,
                ["cacheCreationTokens"] = cacheCreate,
                ["cacheReadTokens"] = cacheRead,
                ["totalTokens"] = input + output + cacheCreate + cacheRead,
                ["outputByModel"] = new JsonObject(models.Select(m =>
                    new KeyValuePair<string, JsonNode?>(m.Key, m.Value))),
            };

            File.WriteAllText(Path.Combine(events, $"tokens-{Safe(session)}.json"),
                summary.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            // Isolated on purpose: a usage summary is a nicety, the chat log is the deliverable.
            Runtime.Warn("preserve-chatlog: token summary skipped: {0}", ex.Message);
        }
    }

    private static long Num(JsonObject usage, string name) =>
        usage[name] is JsonValue value && value.TryGetValue<long>(out var n) ? n : 0;

    private static string? Text(JsonObject payload, params string[] names)
    {
        foreach (var name in names)
        {
            var value = payload[name]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    /// <summary>A session id from a host is not guaranteed to be a legal filename.</summary>
    private static string Safe(string value) =>
        string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
    #endregion
}
