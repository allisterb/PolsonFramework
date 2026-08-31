namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// What this project did in its earlier runs, read back from its own record.
/// </summary>
/// <remarks>
/// The studio already writes everything a memory would hold — the stages an agent declared, the
/// reasoning it wrote into <c>Stage.note</c>, the scripts it ran, the artifacts they produced, and
/// the failures. Nothing read any of it back: a single agent carries its own context, so it never
/// looks, and every run began from nothing. <c>artifacts read back: none</c> across whole runs is
/// the same absence seen from the other side.
/// <para>
/// The unit is a <b>stage within a run</b>, because that is the unit the agent itself declared. A
/// run is bounded by <c>run.start</c>; a stage by <c>stage.begin</c> and the next stage or the end
/// of the run. Splitting any finer would recall a script without the intent behind it, which is
/// exactly the half the record exists to keep.
/// </para>
/// <para>
/// <b>The current run is excluded.</b> Recall is about what happened before; a stage still being
/// written is working memory, and <c>History</c> already serves that. Including it would also let a
/// run rank its own half-finished notes above a finished run's, which reads as insight and is
/// nothing of the kind.
/// </para>
/// </remarks>
public static class EpisodicMemory
{
    #region Constants
    /// <summary>BM25 parameters, matching <see cref="LocalKnowledgeIndex"/> so ranking behaves alike.</summary>
    private const double K1 = 1.2;
    private const double B = 0.75;

    /// <summary>A note is the agent's own account, so it outranks the mechanical fields around it.</summary>
    private const double NoteBoost = 2.0;

    /// <summary>Longest note kept whole in a recalled episode; beyond this it is clipped.</summary>
    private const int MaxNoteChars = 600;
    #endregion

    #region Types
    /// <summary>One stage of one earlier run: what was intended, what it produced, and what failed.</summary>
    public sealed record Episode(
        string Project,
        int Run,
        DateTimeOffset? RunStarted,
        string Stage,
        IReadOnlyList<string> Notes,
        IReadOnlyList<string> Scripts,
        IReadOnlyList<string> Artifacts,
        int Executions,
        int Failures)
    {
        /// <summary>How this episode is referred to, and what a reader would open.</summary>
        public string Uri => $"polson://episode/{Project}/run{Run}/{Stage}";
    }

    /// <summary>A ranked episode.</summary>
    public sealed record Recollection(Episode Episode, double Score);
    #endregion

    #region Methods
    /// <summary>
    /// Every episode in a project's completed runs, oldest first.
    /// </summary>
    /// <param name="projectRoot">The project directory. Its <c>events/server.jsonl</c> is the source.</param>
    /// <param name="includeCurrentRun">
    /// Include the run in progress. False for recall; true is for reporting on a finished run.
    /// </param>
    /// <remarks>
    /// Never throws on a malformed record. A truncated last line is what a log being appended to
    /// looks like when read concurrently, and losing the whole memory over it would be a poor trade.
    /// </remarks>
    public static IReadOnlyList<Episode> Read(string? projectRoot, bool includeCurrentRun = false)
    {
        var file = EventsFile(projectRoot);
        if (file is null) return [];

        var project = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot!)));
        var episodes = new List<Episode>();

        var run = 0;
        DateTimeOffset? runStarted = null;
        Builder? open = null;

        void Close()
        {
            if (open is { } b && !b.IsEmpty) episodes.Add(b.ToEpisode(project, run, runStarted));
            open = null;
        }

        foreach (var line in ReadLines(file))
        {
            if (Parse(line) is not { } e) continue;

            var type = Text(e, "type");
            if (type == "run.start")
            {
                Close();
                run += 1;
                runStarted = Stamp(e, "ts");
                continue;
            }

            var stage = Text(e, "stage");

            // A stage the agent never declared still produced work; file it under a name that says
            // so rather than dropping it, because an undeclared stage is itself worth recalling.
            stage = string.IsNullOrWhiteSpace(stage) ? "(undeclared)" : stage;

            if (open is null || !string.Equals(open.Stage, stage, StringComparison.OrdinalIgnoreCase))
            {
                Close();
                open = new Builder(stage);
            }

            open.Add(type, e);
        }

        Close();

        // The last run.start opens the run in progress; everything filed under it is working memory.
        return includeCurrentRun || run == 0
            ? episodes
            : [.. episodes.Where(x => x.Run < run)];
    }

    /// <summary>
    /// The episodes most relevant to a query, best first.
    /// </summary>
    /// <remarks>
    /// BM25 over the stage name, its notes and the artifacts it produced, with notes weighted
    /// highest — they are the only part written for a reader rather than for the machine.
    /// </remarks>
    public static IReadOnlyList<Recollection> Recall(IReadOnlyList<Episode> episodes, string query, int k)
    {
        if (episodes.Count == 0 || string.IsNullOrWhiteSpace(query)) return [];

        var terms = LocalKnowledgeIndex.Tokenize(query).Distinct(StringComparer.Ordinal).ToArray();
        if (terms.Length == 0) return [];

        var documents = episodes.Select(Weighted).ToArray();
        var average = documents.Average(d => (double)d.Length);

        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var term in document.Terms.Keys) frequency[term] = frequency.GetValueOrDefault(term) + 1;
        }

        var scored = new List<Recollection>(episodes.Count);
        for (var i = 0; i < documents.Length; i++)
        {
            var score = 0.0;
            foreach (var term in terms)
            {
                if (!documents[i].Terms.TryGetValue(term, out var tf)) continue;

                var df = frequency.GetValueOrDefault(term);
                var idf = Math.Log(1.0 + ((documents.Length - df + 0.5) / (df + 0.5)));
                var norm = tf * (K1 + 1) / (tf + (K1 * (1 - B + (B * documents[i].Length / average))));
                score += idf * norm;
            }

            if (score > 0) scored.Add(new Recollection(episodes[i], Math.Round(score, 3)));
        }

        return [.. scored.OrderByDescending(r => r.Score).ThenByDescending(r => r.Episode.Run).Take(Math.Max(1, k))];
    }
    #endregion

    #region Methods (private)
    /// <summary>The record file, or null when there is no project or it has never been written.</summary>
    private static string? EventsFile(string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) return null;

        var file = Path.Combine(Path.GetFullPath(projectRoot), "events", "server.jsonl");
        return File.Exists(file) ? file : null;
    }

    /// <summary>Read shared, because the live run holds the same file open for appending.</summary>
    private static IEnumerable<string> ReadLines(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line)) yield return line;
        }
    }

    private static JsonElement? Parse(string line)
    {
        try
        {
            return JsonDocument.Parse(line).RootElement.Clone();
        }
        catch (JsonException)
        {
            // A half-written last line, which is what an append-only log looks like mid-write.
            return null;
        }
    }

    private static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static DateTimeOffset? Stamp(JsonElement e, string name) =>
        DateTimeOffset.TryParse(Text(e, name), out var stamp) ? stamp : null;

    /// <summary>Terms and length for one episode, with notes counted twice.</summary>
    private static (Dictionary<string, int> Terms, int Length) Weighted(Episode episode)
    {
        var terms = new Dictionary<string, int>(StringComparer.Ordinal);
        var length = 0;

        void Add(string text, int weight)
        {
            foreach (var term in LocalKnowledgeIndex.Tokenize(text))
            {
                terms[term] = terms.GetValueOrDefault(term) + weight;
                length += weight;
            }
        }

        Add(episode.Stage, 1);
        foreach (var note in episode.Notes) Add(note, (int)NoteBoost);
        foreach (var artifact in episode.Artifacts) Add(artifact, 1);

        return (terms, Math.Max(1, length));
    }
    #endregion

    #region Types (private)
    /// <summary>Accumulates one stage's events until the stage changes.</summary>
    private sealed class Builder(string stage)
    {
        private readonly List<string> notes = [];
        private readonly List<string> scripts = [];
        private readonly List<string> artifacts = [];

        public string Stage { get; } = stage;

        public int Executions { get; private set; }

        public int Failures { get; private set; }

        /// <summary>A stage that only opened and closed is not an episode; it recalls nothing.</summary>
        public bool IsEmpty => notes.Count == 0 && scripts.Count == 0 && artifacts.Count == 0;

        public void Add(string type, JsonElement e)
        {
            switch (type)
            {
                case "note":
                    var message = Text(e, "message");
                    if (message.Length > 0) notes.Add(Clip(message));
                    break;

                case "render":
                    var artifact = Text(e, "artifact");
                    if (artifact.Length > 0 && !artifacts.Contains(artifact)) artifacts.Add(artifact);
                    break;

                case "script.ok":
                case "script.error":
                    var script = Text(e, "script");
                    if (script.Length > 0 && !scripts.Contains(script)) scripts.Add(script);
                    Executions += 1;
                    if (type == "script.error") Failures += 1;
                    break;
            }
        }

        public Episode ToEpisode(string project, int run, DateTimeOffset? started) =>
            new(project, run, started, Stage, notes, scripts, artifacts, Executions, Failures);

        private static string Clip(string text) =>
            text.Length <= MaxNoteChars ? text : text[..MaxNoteChars] + "…";
    }
    #endregion
}
