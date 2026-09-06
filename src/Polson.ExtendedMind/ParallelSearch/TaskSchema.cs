namespace Polson.ExtendedMind.ParallelSearch;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// What a dry run of an output schema found, before any research is commissioned.
/// </summary>
public sealed record SchemaInspection
{
    #region Properties
    /// <summary>Whether the schema is worth spending a run on.</summary>
    public bool Usable { get; init; }

    /// <summary>Top-level fields the schema asks for. An array is one field, however many rows it holds.</summary>
    public int FieldCount { get; init; }

    /// <summary>Roughly what the chosen processor handles well, or <c>0</c> when it is unknown to us.</summary>
    public int Capacity { get; init; }

    /// <summary>Why it was refused. Null when <see cref="Usable"/>.</summary>
    public string? Problem { get; init; }

    /// <summary>What to do about it, phrased for the agent that will read it.</summary>
    public string? Remedy { get; init; }

    /// <summary>The cheapest processor that would hold this many fields, when one would.</summary>
    public string? SuggestedProcessor { get; init; }

    /// <summary>
    /// The schema asks for more top-level fields than the configured processor handles well. A
    /// forecast about quality, not a refusal — see the note in <see cref="TaskSchema.Inspect"/>.
    /// </summary>
    public bool OverCapacity { get; init; }

    /// <summary>Quality problems that do not justify refusing the run.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
    #endregion
}

/// <summary>
/// A dry run over a task's output schema: does it parse, does it ask for anything, and will the
/// chosen processor hold it?
/// </summary>
/// <remarks>
/// <para>
/// Exists because the research allowance is small and the first run carries the whole data
/// requirement. A schema that parses but asks <c>base</c> for fourteen fields comes back thin, and
/// the run is spent — on a mistake that costs nothing to catch here. Every check below is arithmetic
/// on the schema text; none of it touches the network.
/// </para>
/// <para>
/// <b>A structural fault refuses; a capacity overage only warns.</b> Malformed JSON, a non-object
/// schema and a schema declaring no properties are facts, and each would spend a run to learn
/// nothing. Field count against capacity is a forecast — the service says "actual capacity depends on
/// field complexity", and the processor is fixed in configuration rather than chosen per call, so a
/// caller told it has too many fields cannot answer by moving up a tier. It can only nest, which the
/// warning says. Blocking on a heuristic with no escape hatch would refuse legitimate work.
/// </para>
/// </remarks>
public static class TaskSchema
{
    #region Properties
    /// <summary>
    /// Fields each processor handles well, from the service's own selection guidance.
    /// </summary>
    /// <remarks>
    /// A processor absent from this table is <b>not</b> assumed to be small — <see cref="CapacityOf"/>
    /// returns 0 and the capacity check is skipped. New tiers arrive (<c>core2x</c>, <c>ultra8x</c>
    /// both did), and a guessed capacity would refuse legitimate work on a name we simply had not
    /// heard of.
    /// </remarks>
    public static IReadOnlyDictionary<string, int> Capacities { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [TaskProcessor.Lite] = 2,
            [TaskProcessor.LiteFast] = 2,
            [TaskProcessor.Base] = 5,
            [TaskProcessor.BaseFast] = 5,
            [TaskProcessor.Core] = 10,
            [TaskProcessor.CoreFast] = 10,
            ["core2x"] = 10,
            [TaskProcessor.Pro] = 20,
            [TaskProcessor.Ultra] = 20,
            ["ultra2x"] = 25,
            ["ultra4x"] = 25,
            ["ultra8x"] = 25,
        };
    #endregion

    #region Methods
    /// <summary>Fields this processor handles well, or <c>0</c> when the name is not one we know.</summary>
    public static int CapacityOf(string? processor) =>
        processor is not null && Capacities.TryGetValue(processor.Trim(), out var capacity) ? capacity : 0;

    /// <summary>The cheapest known processor holding <paramref name="fields"/>, or null if none does.</summary>
    public static string? SmallestProcessorFor(int fields) =>
        Capacities.Where(p => p.Value >= fields)
                  .OrderBy(p => p.Value)
                  .ThenBy(p => p.Key, StringComparer.Ordinal)
                  .Select(p => p.Key)
                  .FirstOrDefault();

    /// <summary>
    /// Inspects a schema without spending anything. A null or blank schema is usable — it means a
    /// prose answer, which has no fields to count.
    /// </summary>
    public static SchemaInspection Inspect(string? schemaJson, string? processor)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new SchemaInspection { Usable = true };
        }

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(schemaJson).RootElement;
        }
        catch (JsonException ex)
        {
            return new SchemaInspection
            {
                Usable = false,
                Problem = $"The schema is not valid JSON: {ex.Message}",
                Remedy = "Fix the JSON and call again. Nothing was spent.",
            };
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new SchemaInspection
            {
                Usable = false,
                Problem = $"The schema is a JSON {root.ValueKind.ToString().ToLowerInvariant()}, not an object.",
                Remedy = "An output schema is a JSON Schema object: "
                       + "{\"type\":\"object\",\"properties\":{...},\"required\":[...]}.",
            };
        }

        if (!root.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object ||
            !properties.EnumerateObject().Any())
        {
            return new SchemaInspection
            {
                Usable = false,
                Problem = "The schema declares no properties, so it asks for nothing.",
                Remedy = "Add a \"properties\" object naming each field you need, each with a "
                       + "\"description\" saying what it is — the descriptions are what steer the answer.",
            };
        }

        var fields = properties.EnumerateObject().ToArray();
        var capacity = CapacityOf(processor);
        var warnings = QualityWarnings(root, fields);

        // Over capacity is a WARNING, not a refusal, and the reason is that the caller cannot act on
        // it by changing tier — the processor is a cost lever held in configuration, not something an
        // agent chooses. That leaves count as a prediction about quality rather than a configuration
        // mismatch, and the service says plainly that complexity matters more than count: eight date
        // fields are not eight research questions. Refusing on a heuristic the caller has no escape
        // hatch from would block legitimate work. Structural faults above still refuse, because those
        // are facts rather than forecasts.
        if (capacity > 0 && fields.Length > capacity)
        {
            warnings.Insert(0,
                $"{fields.Length} top-level fields against roughly {capacity} that '{processor}' handles "
                + "well, so thin or low-confidence fields are likely. Group related ones into a nested "
                + "object or an array — an array counts as ONE field however many rows it returns. "
                + "Simple fields (dates, booleans, short strings) cost less capacity than research-heavy "
                + "ones, so this may still be fine; check the confidence on what comes back.");
        }

        return new SchemaInspection
        {
            Usable = true,
            FieldCount = fields.Length,
            Capacity = capacity,
            OverCapacity = capacity > 0 && fields.Length > capacity,
            SuggestedProcessor = capacity > 0 && fields.Length > capacity
                ? SmallestProcessorFor(fields.Length)
                : null,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Problems worth saying but not worth refusing over. Advice, not limits — so they travel with a
    /// run that goes ahead.
    /// </summary>
    private static List<string> QualityWarnings(JsonElement root, JsonProperty[] fields)
    {
        var warnings = new List<string>();

        var undescribed = fields
            .Where(f => f.Value.ValueKind != JsonValueKind.Object
                     || !f.Value.TryGetProperty("description", out var d)
                     || string.IsNullOrWhiteSpace(d.GetString()))
            .Select(f => f.Name)
            .ToArray();

        if (undescribed.Length > 0)
        {
            warnings.Add($"No description on: {string.Join(", ", undescribed)}. Field descriptions are "
                       + "instructions — they determine the form and content of the answer, so an "
                       + "undescribed field is the likeliest one to come back wrong.");
        }

        if (!root.TryGetProperty("required", out var required) ||
            required.ValueKind != JsonValueKind.Array ||
            required.GetArrayLength() == 0)
        {
            warnings.Add("No \"required\" array, so any field may come back absent and you will not be "
                       + "able to tell a missing figure from one that does not exist.");
        }

        if (root.TryGetProperty("type", out var type) &&
            type.ValueKind == JsonValueKind.String &&
            !string.Equals(type.GetString(), "object", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Root \"type\" is '{type.GetString()}' rather than 'object'.");
        }

        return warnings;
    }
    #endregion
}
