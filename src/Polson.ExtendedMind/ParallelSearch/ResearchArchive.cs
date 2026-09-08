namespace Polson.ExtendedMind.ParallelSearch;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Writes a finished research run to disk, so the figures drawn from it stay checkable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything a run learned used to die with the session.</b> <see cref="ResearchRegistry"/> holds
/// its tasks in a dictionary, and the run record carries only the run id, the processor and how long
/// it took — never the data. So once a run ended, the only surviving account of where a number on the
/// canvas came from was the agent's own transcription of it into <c>brief.md</c>. A director, a judge
/// or a later pass had the citation's *name* and no way to read it.
/// </para>
/// <para>
/// That matters more here than for any other surface, because "the figures are sourced" is the claim
/// this studio makes hardest. A claim nobody can audit after the fact is a claim about a run rather
/// than about a graphic.
/// </para>
/// <para>
/// <b>Its own folder, beside the caches rather than inside one.</b> <c>assets/</c> and
/// <c>documents/</c> are content-addressed: the filename is a hash and the point is to avoid paying
/// for the same call twice. This is keyed by run id and its point is provenance, not cache hits.
/// Filed together they would make that folder mean two things.
/// </para>
/// <para>
/// <b>The basis is the half that matters.</b> Writing <c>result</c> alone would let a reader check
/// that a number was transcribed faithfully and tell them nothing about whether it was ever true.
/// <c>basis</c> carries the citation and the confidence per field, which is what turns a figure from
/// present into checkable.
/// </para>
/// </remarks>
public sealed class ResearchArchive : Runtime
{
    #region Constructors
    /// <param name="directory">Where runs are filed. Defaults to <c>research</c> beside the caches.</param>
    public ResearchArchive(string? directory = null)
    {
        Directory = directory ?? Path.Combine(CamelDir, "research");
    }
    #endregion

    #region Properties
    /// <summary>Where runs are filed. Created on the first write rather than on construction.</summary>
    public string Directory { get; }
    #endregion

    #region Methods
    /// <summary>The file one run is filed under, whether or not it exists.</summary>
    public string PathFor(string runId) => Path.Combine(Directory, Safe(runId) + ".json");

    /// <summary>
    /// Files a finished run. Never throws.
    /// </summary>
    /// <remarks>
    /// Swallowed for the reason <c>EventLog</c> swallows its own writes: a run that fails because its
    /// receipt could not be written is a worse outcome than a run with no receipt. The research is
    /// already paid for and already in memory; losing the drawing over the archive would be the tail
    /// wagging the dog.
    /// </remarks>
    public bool Save(ResearchTask task)
    {
        if (task is null || string.IsNullOrWhiteSpace(task.Id)) return false;

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(PathFor(task.Id), Serialize(task));
            return true;
        }
        catch (Exception ex)
        {
            Error(ex, "Research run {0} could not be archived: {1}", task.Id, ex.Message);
            return false;
        }
    }

    /// <summary>One run as JSON: the data, and what each field of it rests on.</summary>
    public static string Serialize(ResearchTask task) =>
        JsonSerializer.Serialize(new ArchivedResearch
        {
            Id = task.Id,
            Description = task.Description,
            Objective = task.Objective,
            Processor = task.Processor,
            Status = task.Status,
            StartedUtc = task.StartedUtc,
            ArchivedUtc = DateTimeOffset.UtcNow,
            Error = task.Error,
            Warnings = task.Warnings,
            Result = task.Result,
            Basis = task.Basis,
        }, Options);

    /// <summary>A run read back, or null when it was never filed or cannot be read.</summary>
    /// <remarks>
    /// Reading is deliberately forgiving in the same way writing is: a checker that cannot open one
    /// run should report that figure as unverifiable, not abandon the audit.
    /// </remarks>
    public ArchivedResearch? Load(string runId)
    {
        try
        {
            var path = PathFor(runId);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ArchivedResearch>(File.ReadAllText(path), Options)
                : null;
        }
        catch (Exception ex)
        {
            Error(ex, "Archived research {0} could not be read: {1}", runId, ex.Message);
            return null;
        }
    }
    #endregion

    #region Methods (private)
    /// <summary>
    /// A run id reduced to something that cannot leave the folder.
    /// </summary>
    /// <remarks>
    /// The id comes back from a third-party service, so it is not ours to trust as a path segment
    /// however well-formed every observed one has been. Same reasoning as the upload filename check:
    /// an allowlist, not a hunt for bad characters.
    /// </remarks>
    private static string Safe(string runId)
    {
        var clean = new char[runId.Length];
        for (var i = 0; i < runId.Length; i++)
        {
            var c = runId[i];
            clean[i] = char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_';
        }

        return new string(clean);
    }
    #endregion

    #region Fields
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    #endregion
}

/// <summary>One research run as it is filed on disk.</summary>
/// <remarks>
/// Self-describing on purpose: the objective and processor are here so a reader can tell what was
/// asked and at what tier, without the conversation that asked it. A file naming only its result
/// answers "what did it say" and not "what was it asked", and the second is where a wrong figure
/// usually starts.
/// </remarks>
public sealed record ArchivedResearch
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; init; } = string.Empty;

    [JsonPropertyName("processor")]
    public string Processor { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("startedUtc")]
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>When it was filed, which is not when it ran.</summary>
    [JsonPropertyName("archivedUtc")]
    public DateTimeOffset ArchivedUtc { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>What the codepoint scan found in the text this run brought back. Usually empty.</summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    [JsonPropertyName("result")]
    public object? Result { get; init; }

    /// <summary>What each field rests on: the reasoning, the citations, the confidence.</summary>
    [JsonPropertyName("basis")]
    public IReadOnlyList<FieldBasis> Basis { get; init; } = [];
}
