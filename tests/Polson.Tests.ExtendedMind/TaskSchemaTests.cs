namespace Polson.Tests.ExtendedMind;

using System.Linq;

using global::Polson.ExtendedMind.ParallelSearch;
using global::Polson.Tests;
using Xunit;

/// <summary>
/// The dry run over an output schema, which spends nothing and runs before anything is commissioned.
/// </summary>
/// <remarks>
/// It exists because the allowance is two runs and the first carries the whole data requirement. A
/// schema that parses but overruns its processor comes back thin, and by then the run is gone —
/// so every mistake catchable by arithmetic is caught here instead.
/// </remarks>
public class TaskSchemaTests : TestsRuntime
{
    #region Fields
    private const string GoodSchema = """
        {
          "type": "object",
          "properties": {
            "missions": {
              "type": "array",
              "description": "One entry per crewed Apollo lunar mission.",
              "items": { "type": "object" }
            }
          },
          "required": ["missions"]
        }
        """;
    #endregion

    #region Usable

    [Fact]
    public void AWellFormedSchemaPassesWithNoWarnings()
    {
        var report = TaskSchema.Inspect(GoodSchema, TaskProcessor.Base);

        Assert.True(report.Usable);
        Assert.Equal(1, report.FieldCount);
        Assert.Equal(5, report.Capacity);
        Assert.Empty(report.Warnings);
        Assert.Null(report.Problem);
    }

    /// <summary>No schema means a prose answer, which has no fields to count and nothing to refuse.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoSchemaIsUsable(string? schema)
    {
        var report = TaskSchema.Inspect(schema, TaskProcessor.Base);

        Assert.True(report.Usable);
        Assert.Equal(0, report.FieldCount);
    }

    /// <summary>
    /// The measured case: six missions with three properties each went through as one field on
    /// <c>base</c>. An array is one field however many rows it returns, which is what makes a large
    /// requirement fit in a single run.
    /// </summary>
    [Fact]
    public void AnArrayCountsAsOneFieldHoweverManyRowsItHolds()
    {
        var report = TaskSchema.Inspect(GoodSchema, TaskProcessor.Lite);

        Assert.True(report.Usable);
        Assert.Equal(1, report.FieldCount);
        Assert.Equal(2, report.Capacity);   // even the smallest tier holds it
    }
    #endregion

    #region Refusals

    [Fact]
    public void MalformedJsonIsRefusedWithoutSpending()
    {
        var report = TaskSchema.Inspect("{ not json", TaskProcessor.Base);

        Assert.False(report.Usable);
        Assert.Contains("not valid JSON", report.Problem);
        Assert.Contains("Nothing was spent", report.Remedy);
    }

    [Theory]
    [InlineData("[1,2,3]", "array")]
    [InlineData("\"a string\"", "string")]
    [InlineData("42", "number")]
    public void ANonObjectSchemaIsRefused(string schema, string kind)
    {
        var report = TaskSchema.Inspect(schema, TaskProcessor.Base);

        Assert.False(report.Usable);
        Assert.Contains(kind, report.Problem);
    }

    /// <summary>A schema with no properties asks for nothing, and would spend a run to say so.</summary>
    [Theory]
    [InlineData("""{ "type": "object" }""")]
    [InlineData("""{ "type": "object", "properties": {} }""")]
    [InlineData("""{ "type": "object", "properties": "not an object" }""")]
    public void ASchemaThatAsksForNothingIsRefused(string schema)
    {
        var report = TaskSchema.Inspect(schema, TaskProcessor.Base);

        Assert.False(report.Usable);
        Assert.Contains("asks for nothing", report.Problem);
        Assert.Contains("description", report.Remedy);
    }

    /// <summary>
    /// Too many top-level fields <b>warns and proceeds</b>. It is a forecast about quality, not a
    /// fault: the processor is fixed in configuration, so the caller cannot answer by moving up a
    /// tier — and simple fields may fit regardless. Refusing on a heuristic with no escape hatch
    /// would block legitimate work.
    /// </summary>
    [Fact]
    public void TooManyFieldsForTheProcessorWarnsRatherThanRefusing()
    {
        var report = TaskSchema.Inspect(SchemaWith(14), TaskProcessor.Base);

        Assert.True(report.Usable);
        Assert.True(report.OverCapacity);
        Assert.Equal(14, report.FieldCount);
        Assert.Equal(5, report.Capacity);
        Assert.Null(report.Problem);

        var warning = Assert.Single(report.Warnings, w => w.Contains("14 top-level fields"));
        Assert.Contains("counts as ONE field", warning);
        Assert.Contains("nested object or an array", warning);
        Assert.Contains("check the confidence", warning);
    }

    /// <summary>
    /// The overage warning leads with the count, so it is the first thing read. The quality warnings
    /// that may accompany it are secondary.
    /// </summary>
    [Fact]
    public void TheCapacityWarningComesFirst()
    {
        var report = TaskSchema.Inspect(SchemaWith(14), TaskProcessor.Base);

        Assert.Contains("top-level fields", report.Warnings[0]);
    }

    /// <summary>
    /// A schema within capacity says nothing about it, so the warning means something when it appears.
    /// </summary>
    [Fact]
    public void AWithinCapacitySchemaCarriesNoOverageWarning()
    {
        var report = TaskSchema.Inspect(SchemaWith(5), TaskProcessor.Base);

        Assert.True(report.Usable);
        Assert.False(report.OverCapacity);
        Assert.Null(report.SuggestedProcessor);
        Assert.DoesNotContain(report.Warnings, w => w.Contains("top-level fields"));
    }

    /// <summary>
    /// The tier that would have fitted is still reported — not for the agent, which cannot act on it,
    /// but for whoever reads the run record and decides whether to raise <c>Research:Processor</c>.
    /// </summary>
    [Theory]
    [InlineData(3, TaskProcessor.Base)]
    [InlineData(7, TaskProcessor.Core)]
    [InlineData(15, TaskProcessor.Pro)]
    [InlineData(22, "ultra2x")]
    public void TheSuggestedProcessorIsTheCheapestThatFits(int fields, string expected)
    {
        var report = TaskSchema.Inspect(SchemaWith(fields), TaskProcessor.Lite);

        Assert.True(report.Usable);
        Assert.True(report.OverCapacity);
        Assert.Equal(expected, report.SuggestedProcessor);
    }

    /// <summary>Past every tier there is no processor to name, and the advice is to nest.</summary>
    [Fact]
    public void BeyondEveryTierThereIsNoProcessorToSuggest()
    {
        var report = TaskSchema.Inspect(SchemaWith(40), TaskProcessor.Base);

        Assert.True(report.Usable);
        Assert.True(report.OverCapacity);
        Assert.Null(report.SuggestedProcessor);
        Assert.Contains(report.Warnings, w => w.Contains("counts as ONE field"));
    }

    /// <summary>
    /// A processor we do not recognise skips the capacity check rather than guessing at it. New tiers
    /// keep arriving, and refusing legitimate work on an unfamiliar name would be worse than not
    /// checking.
    /// </summary>
    [Fact]
    public void AnUnknownProcessorSkipsTheCapacityCheck()
    {
        var report = TaskSchema.Inspect(SchemaWith(40), "ultra64x-quantum");

        Assert.True(report.Usable);
        Assert.Equal(0, report.Capacity);
        Assert.Equal(40, report.FieldCount);
    }

    [Fact]
    public void CapacityIsKnownForEveryProcessorConstantWeName()
    {
        foreach (var processor in new[]
        {
            TaskProcessor.Lite, TaskProcessor.LiteFast, TaskProcessor.Base, TaskProcessor.BaseFast,
            TaskProcessor.Core, TaskProcessor.CoreFast, TaskProcessor.Pro, TaskProcessor.Ultra,
        })
        {
            Assert.True(TaskSchema.CapacityOf(processor) > 0, $"no capacity known for '{processor}'");
        }

        // A fast variant is the same size as its standard counterpart; only the latency differs.
        Assert.Equal(TaskSchema.CapacityOf(TaskProcessor.Base), TaskSchema.CapacityOf(TaskProcessor.BaseFast));
        Assert.Equal(TaskSchema.CapacityOf(TaskProcessor.Core), TaskSchema.CapacityOf(TaskProcessor.CoreFast));
    }
    #endregion

    #region Warnings

    /// <summary>
    /// Field descriptions are instructions to the service, so an undescribed field is the likeliest
    /// one to come back wrong. Worth saying, not worth refusing over.
    /// </summary>
    [Fact]
    public void AnUndescribedFieldWarnsButDoesNotRefuse()
    {
        var report = TaskSchema.Inspect("""
            {
              "type": "object",
              "properties": {
                "described": { "type": "string", "description": "What it is." },
                "bare": { "type": "string" },
                "blank": { "type": "string", "description": "  " }
              },
              "required": ["described"]
            }
            """, TaskProcessor.Base);

        Assert.True(report.Usable);
        var warning = Assert.Single(report.Warnings, w => w.Contains("No description"));
        Assert.Contains("bare", warning);
        Assert.Contains("blank", warning);
        Assert.DoesNotContain("described,", warning);
    }

    [Fact]
    public void AMissingRequiredArrayWarns()
    {
        var report = TaskSchema.Inspect("""
            { "type": "object", "properties": { "a": { "description": "A." } } }
            """, TaskProcessor.Base);

        Assert.True(report.Usable);
        Assert.Contains(report.Warnings, w => w.Contains("required"));
    }

    [Fact]
    public void ANonObjectRootTypeWarns()
    {
        var report = TaskSchema.Inspect("""
            { "type": "array", "properties": { "a": { "description": "A." } }, "required": ["a"] }
            """, TaskProcessor.Base);

        Assert.True(report.Usable);
        Assert.Contains(report.Warnings, w => w.Contains("'array'"));
    }
    #endregion

    #region Helpers
    /// <summary>A well-formed schema with a given number of described top-level fields.</summary>
    private static string SchemaWith(int fields)
    {
        var properties = string.Join(",\n    ",
            Enumerable.Range(0, fields).Select(i => $"\"field_{i}\": {{ \"type\": \"string\", \"description\": \"Field {i}.\" }}"));
        var required = string.Join(", ", Enumerable.Range(0, fields).Select(i => $"\"field_{i}\""));

        return $"{{\n  \"type\": \"object\",\n  \"properties\": {{\n    {properties}\n  }},\n  \"required\": [{required}]\n}}";
    }
    #endregion
}
