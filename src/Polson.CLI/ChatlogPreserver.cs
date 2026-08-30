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
/// on stdout and treats anything else as a malformed hook, so every diagnostic here goes to standard
/// error and stdout carries <c>{}</c> and nothing else.
/// </para>
/// <para>
/// <b>Best-effort, always.</b> A hook that throws takes the session with it, and a lost chat log is
/// worth far less than a lost run. Every failure path here writes a line to stderr and returns
/// success.
/// </para>
/// </remarks>
internal static class ChatlogPreserver
{
    #region Methods
    internal static int Run()
    {
        try
        {
            Preserve();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[preserve-chatlog] {ex.Message}");
        }

        // The reply the host is waiting for. Written whatever happened above.
        Console.Out.Write("{}");
        return 0;
    }
    #endregion

    #region Methods (private)
    private static void Preserve()
    {
        // A piped payload can arrive with a BOM or surrounding whitespace depending on the host.
        var stdin = (Console.In.ReadToEnd() ?? string.Empty).Trim().TrimStart('﻿');
        if (stdin.Length == 0)
        {
            Console.Error.WriteLine("[preserve-chatlog] empty payload; nothing to do.");
            return;
        }

        var payload = JsonNode.Parse(stdin) as JsonObject;
        if (payload is null)
        {
            Console.Error.WriteLine("[preserve-chatlog] payload was not a JSON object.");
            return;
        }

        var project = ProjectDirectory(payload);
        var events = Path.Combine(project, "events");
        Directory.CreateDirectory(events);

        var session = Text(payload, "session_id", "conversationId") ?? "session";
        var source = Text(payload, "transcript_path", "transcriptPath") ?? Remembered(events);

        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            Console.Error.WriteLine($"[preserve-chatlog] no readable transcript ({source ?? "none given"}).");
            return;
        }

        Remember(events, source);

        // One file per session, overwritten. These hooks fire every turn, so a timestamped name
        // would leave one whole-transcript copy per turn — the same conversation, N times over.
        var destination = Path.Combine(events, $"chat-{Safe(session)}.jsonl");
        File.Copy(source, destination, overwrite: true);
        Console.Error.WriteLine($"[preserve-chatlog] chat log -> {destination}");

        WriteTokenUsage(source, events, session);
    }

    /// <summary>
    /// Where the project is, asking the host before guessing.
    /// </summary>
    /// <remarks>
    /// The cwd is the last resort rather than the first: a hook runs wherever the host happens to
    /// have started, and writing the transcript into the wrong directory is worse than not writing
    /// it, because it looks like it worked.
    /// </remarks>
    private static string ProjectDirectory(JsonObject payload)
    {
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
            Console.Error.WriteLine($"[preserve-chatlog] could not remember the transcript path: {ex.Message}");
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
    /// </remarks>
    private static void WriteTokenUsage(string transcript, string events, string session)
    {
        try
        {
            long input = 0, output = 0, cacheCreate = 0, cacheRead = 0;
            var turns = 0;
            var models = new Dictionary<string, long>(StringComparer.Ordinal);

            foreach (var line in File.ReadLines(transcript))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonNode? node;
                try { node = JsonNode.Parse(line); }
                catch (JsonException) { continue; }

                if (node?["message"] is not JsonObject message) continue;
                if (message["usage"] is not JsonObject usage) continue;

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
            Console.Error.WriteLine($"[preserve-chatlog] token summary skipped: {ex.Message}");
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
