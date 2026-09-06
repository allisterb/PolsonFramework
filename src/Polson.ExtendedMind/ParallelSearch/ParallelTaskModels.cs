namespace Polson.ExtendedMind.ParallelSearch;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

#region Processors

/// <summary>
/// The engines that run a task. Latency and field counts are the service's published guidance, and
/// they are the reason a task is started and collected later rather than awaited inline.
/// </summary>
/// <remarks>
/// Strings rather than an enum, deliberately: the tier list grows — <c>core2x</c>, <c>ultra2x</c>,
/// <c>ultra4x</c> and <c>ultra8x</c> all arrived after the original five — and an enum would turn a
/// new processor into a compile error for callers and a parse failure for responses.
/// </remarks>
public static class TaskProcessor
{
    /// <summary>~2 fields. Observed p50 45 s, p90 1.5 min. $5 per 1,000 runs.</summary>
    public const string Lite = "lite";

    /// <summary>~5 fields. Observed p50 50 s, p90 2 min. $10 per 1,000 runs.</summary>
    public const string Base = "base";

    /// <summary>~10 fields with reliable accuracy. Observed p50 1.5 min, p90 3 min. $25 per 1,000.</summary>
    public const string Core = "core";

    /// <summary>~20 fields. Observed p50 3.5 min, p90 7.5 min. $100 per 1,000.</summary>
    public const string Pro = "pro";

    /// <summary>~20 fields, deepest reasoning. Observed p50 4 min, p90 10 min. $300 per 1,000.</summary>
    public const string Ultra = "ultra";

    /// <summary>~2 fields. Advertised 10-20 s. Same price as <see cref="Lite"/>.</summary>
    public const string LiteFast = "lite-fast";

    /// <summary>~5 fields. Advertised 15-50 s, at the same $10 per 1,000 as <see cref="Base"/>.</summary>
    public const string BaseFast = "base-fast";

    /// <summary>
    /// ~10 fields, advertised 15-100 s against <see cref="Core"/>'s observed p50 of 1.5 min, at the
    /// same $25 per 1,000. The tier to reach for when a table outgrows five fields.
    /// </summary>
    public const string CoreFast = "core-fast";

    /// <summary>
    /// The default: <see cref="Base"/>. Cheap, carries the full basis, and sized for the five-or-so
    /// fields a figure in a graphic usually needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately not a fast variant, despite the identical price.</b> If <c>base-fast</c> were
    /// strictly better than <c>base</c> at the same cost, the standard tier would have no reason to
    /// exist — so something is being traded, and the documentation does not say what. It states no
    /// quality trade-off, which is not the same as affirming there is none.
    /// </para>
    /// <para>
    /// Two facts sharpen that. The service's observed-latency table has <b>no row for any fast
    /// variant</b>, so their figures are advertised rather than measured — a different kind of number
    /// from the p50/p90 quoted on the standard tiers, and not comparable with them. And what this
    /// studio sells is data that survives being checked, so an unexplained saving on the research
    /// step is the wrong place to accept an unknown.
    /// </para>
    /// <para>
    /// What we have measured: one <c>base</c> run took <b>96 s</b>, between its p50 of 50 s and its
    /// p90 of 2 min. Size waits against p90, not p50, or one run in ten times out. The fast constants
    /// remain available to name explicitly — revisit them if we ever measure the two side by side on
    /// the same objective and find the basis equally strong.
    /// </para>
    /// </remarks>
    public const string Default = Base;
}
#endregion

#region Task specification

/// <summary>
/// What the task should produce. Build one with <see cref="Text"/>, <see cref="Json"/> or
/// <see cref="Auto"/>.
/// </summary>
/// <remarks>
/// The output schema is what makes this different from Search and Extract: those retrieve, this
/// <b>synthesises to a shape you specify</b> and reports what each field rests on. Field
/// descriptions in a JSON schema are instructions — they determine the form and content of the
/// answer, so they are worth writing carefully.
/// </remarks>
public sealed record TaskSpec
{
    #region Constructors
    private TaskSpec(object outputSchema) => this.Output = outputSchema;
    #endregion

    #region Properties
    /// <summary>Let the service choose a shape. The same as sending no spec at all.</summary>
    public static TaskSpec Auto { get; } = new(new { type = "auto" });

    internal object Output { get; init; }

    internal object? Input { get; init; }
    #endregion

    #region Methods
    /// <summary>Prose describing the answer you want. Returns text output with a single basis entry.</summary>
    public static TaskSpec Text(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new TaskSpec(new { type = "text", description });
    }

    /// <summary>
    /// A JSON Schema object, as a string. Only a subset of JSON Schema is supported by the service.
    /// </summary>
    /// <remarks>
    /// Parsed here so a malformed schema fails at the call site rather than as a 422 minutes later —
    /// a task run is slow enough that a typo should not cost the wait.
    /// </remarks>
    public static TaskSpec Json(string jsonSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonSchema);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(jsonSchema);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The output schema is not valid JSON: {ex.Message}", nameof(jsonSchema), ex);
        }

        if (parsed is not JsonObject)
        {
            throw new ArgumentException("The output schema must be a JSON object.", nameof(jsonSchema));
        }

        return new TaskSpec(new JsonSchemaWire { JsonSchema = parsed });
    }

    /// <summary>Describes the input, when the task takes structured input rather than a question.</summary>
    public TaskSpec WithInputDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return this with { Input = new { type = "text", description } };
    }
    #endregion

    #region Child Types
    private sealed record JsonSchemaWire
    {
        [JsonPropertyName("type")] public string Type => "json";

        [JsonPropertyName("json_schema")] public required JsonNode? JsonSchema { get; init; }
    }
    #endregion
}

/// <summary>Optional settings for a task run.</summary>
public sealed record TaskOptions
{
    /// <summary>See <see cref="TaskProcessor"/>. Defaults to <see cref="TaskProcessor.Default"/>.</summary>
    public string Processor { get; init; } = TaskProcessor.Default;

    public IReadOnlyList<string>? IncludeDomains { get; init; }

    public IReadOnlyList<string>? ExcludeDomains { get; init; }

    public DateOnly? AfterDate { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code for geo-targeted search.</summary>
    public string? Location { get; init; }

    /// <summary>
    /// Stored with the run and returned with its status. Keys are capped at 16 characters and values
    /// at 512. Useful for tying a run back to the stage that started it.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Records progress events for the run. On by default for premium processors, and it
    /// <b>cannot be turned on after the run is created</b>.
    /// </summary>
    public bool? EnableEvents { get; init; }

    /// <summary>An earlier run's <see cref="ParallelTaskRun.InteractionId"/>, to reuse its context.</summary>
    public string? PreviousInteractionId { get; init; }
}
#endregion

#region Basis — what each field rests on

/// <summary>A source supporting one output field.</summary>
public sealed record TaskCitation
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Supporting passages. Only some processors return these.</summary>
    [JsonPropertyName("excerpts")]
    public IReadOnlyList<string>? Excerpts { get; init; }

    /// <summary>A caption-ready citation, the same shape Search and Extract results produce.</summary>
    public string Cite() => ParallelCitation.Format(Title, Url, null);
}

/// <summary>
/// The citations and reasoning behind one field of the output.
/// </summary>
/// <remarks>
/// This is the whole reason to prefer a task over assembling excerpts by hand: provenance is per
/// <b>field</b> rather than per document, so a single figure in a table can be traced to the passage
/// it came from. List fields also get per-element entries with dot-delimited indexes, such as
/// <c>missions.0</c>.
/// </remarks>
public sealed record FieldBasis
{
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; init; } = string.Empty;

    [JsonPropertyName("citations")]
    public IReadOnlyList<TaskCitation>? Citations { get; init; }

    /// <summary>Only some processors report one.</summary>
    [JsonPropertyName("confidence")]
    public string? Confidence { get; init; }
}
#endregion

#region Run status and result

/// <summary>Status of a task run, as returned when it is started and whenever it is checked.</summary>
public sealed record ParallelTaskRun : ParallelResponse
{
    #region Properties
    [JsonPropertyName("run_id")]
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// One of <c>queued</c>, <c>action_required</c>, <c>running</c>, <c>completed</c>,
    /// <c>failed</c>, <c>cancelling</c>, <c>cancelled</c>. A string rather than an enum, so a status
    /// added later does not cost the response.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>The service's own view: true while queued, running or cancelling.</summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonPropertyName("processor")]
    public string Processor { get; init; } = string.Empty;

    /// <summary>Pass as <see cref="TaskOptions.PreviousInteractionId"/> to reuse this run's context.</summary>
    [JsonPropertyName("interaction_id")]
    public string InteractionId { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("modified_at")]
    public string? ModifiedAt { get; init; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }

    /// <summary>The run's own error, distinct from a transport failure on the call that fetched it.</summary>
    [JsonPropertyName("error")]
    public TaskRunError? RunError { get; init; }

    /// <summary>Finished successfully. Only then is there a result to collect.</summary>
    [JsonIgnore]
    public bool IsCompleted => Status == "completed";

    /// <summary>Finished without a result. <see cref="RunError"/> says why.</summary>
    [JsonIgnore]
    public bool IsFailed => Status is "failed" or "cancelled";

    /// <summary>
    /// Waiting on something outside the run. Neither finished nor progressing on its own, so polling
    /// forever is the wrong response.
    /// </summary>
    [JsonIgnore]
    public bool NeedsAction => Status == "action_required";
    #endregion
}

/// <summary>An error reported by the run itself.</summary>
public sealed record TaskRunError
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("ref_id")]
    public string RefId { get; init; } = string.Empty;

    [JsonPropertyName("detail")]
    public JsonElement? Detail { get; init; }
}

/// <summary>A completed task run and its output.</summary>
public sealed record ParallelTaskResult : ParallelResponse
{
    #region Properties
    /// <summary>The run, with status <c>completed</c>.</summary>
    [JsonPropertyName("run")]
    public ParallelTaskRun? Run { get; init; }

    [JsonPropertyName("output")]
    public TaskOutput? Output { get; init; }

    /// <summary>Per-field citations and reasoning. Empty rather than null when there is no output.</summary>
    [JsonIgnore]
    public IReadOnlyList<FieldBasis> Basis => Output?.Basis ?? [];

    /// <summary>The text answer, when the spec asked for text. Null for a JSON output.</summary>
    [JsonIgnore]
    public string? Text => Output?.Content is { ValueKind: JsonValueKind.String } c ? c.GetString() : null;

    /// <summary>The structured answer, when the spec asked for JSON. Null for a text output.</summary>
    [JsonIgnore]
    public JsonElement? Json => Output?.Content is { ValueKind: JsonValueKind.Object } c ? c : null;
    #endregion

    #region Methods
    /// <summary>The basis for one field, or null. Matches the field name the schema declared.</summary>
    public FieldBasis? BasisFor(string field)
    {
        foreach (var entry in Basis)
        {
            if (string.Equals(entry.Field, field, StringComparison.Ordinal)) return entry;
        }

        return null;
    }
    #endregion
}

/// <summary>
/// A task's output. <see cref="Content"/> is a <see cref="JsonElement"/> because the service returns
/// a string for a text task and an object for a JSON one, under the same property name — reading it
/// as a raw element avoids a discriminated converter and cannot misparse either shape.
/// </summary>
public sealed record TaskOutput
{
    /// <summary><c>text</c> or <c>json</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public JsonElement Content { get; init; }

    [JsonPropertyName("basis")]
    public IReadOnlyList<FieldBasis> Basis { get; init; } = [];

    /// <summary>Populated only when the task ran with an auto schema.</summary>
    [JsonPropertyName("output_schema")]
    public JsonElement? OutputSchema { get; init; }
}
#endregion
