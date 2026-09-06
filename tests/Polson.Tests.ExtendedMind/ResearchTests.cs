namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using global::Polson.ExtendedMind.ParallelSearch;
using global::Polson.Tests;
using Xunit;

/// <summary>
/// Tests for the research registry and the read-only <c>Research</c> global.
/// </summary>
/// <remarks>
/// The integrity property these exist to protect: a script can <b>read</b> sourced data and can never
/// <b>write</b> it. An agent that could author its own basis could produce a graphic carrying a
/// citation under a number it invented, which is the one failure this whole surface exists to
/// prevent.
/// </remarks>
public class ResearchTests : TestsRuntime
{
    #region Integrity

    /// <summary>
    /// The toolkit is what a script sees. It must expose no way to create, complete or alter a task —
    /// checked by reflection rather than by inspection, so it stays true as the type grows.
    /// </summary>
    [Fact]
    public void TheScriptFacingToolkitExposesNoWayToWriteResearch()
    {
        var writable = typeof(ResearchToolkit)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialGetter())
            .Select(m => m.Name)
            .Where(n => n is not ("Get" or "Find" or "AllComplete" or "ToString" or "Equals" or "GetHashCode" or "GetType"))
            .ToArray();

        Assert.True(writable.Length == 0, $"ResearchToolkit exposes {string.Join(", ", writable)}");

        // Nor does it hand out the registry, which is where the mutators live.
        Assert.DoesNotContain(
            typeof(ResearchToolkit).GetProperties(),
            p => p.PropertyType == typeof(ResearchRegistry));
    }

    /// <summary>A task's own state is read-only to anything outside the assembly that owns it.</summary>
    [Theory]
    [InlineData(nameof(ResearchTask.Status))]
    [InlineData(nameof(ResearchTask.Result))]
    [InlineData(nameof(ResearchTask.Basis))]
    [InlineData(nameof(ResearchTask.Error))]
    public void TaskStateHasNoPublicSetter(string property)
    {
        var setter = typeof(ResearchTask).GetProperty(property)!.SetMethod;
        Assert.True(setter is null || !setter.IsPublic, $"{property} is publicly settable");
    }
    #endregion

    #region Registry

    [Fact]
    public void StartRegistersATaskInOrderAndByIdempotentId()
    {
        var registry = new ResearchRegistry();

        var first = registry.Start("trun_1", "Apollo figures", "objective", TaskProcessor.BaseFast);
        var second = registry.Start("trun_2", "Launch sites", "objective", TaskProcessor.BaseFast);
        var again = registry.Start("trun_1", "different description", "objective", TaskProcessor.Core);

        Assert.Equal(2, registry.All.Count);
        Assert.Same(first, again);                      // same id is the same task, not a duplicate
        Assert.Equal("Apollo figures", again.Description);
        Assert.Equal([first, second], registry.All);    // start order preserved
        Assert.Equal("queued", first.Status);
        Assert.True(first.IsActive);
    }

    [Fact]
    public void CompleteRecordsTheDataAndBasis()
    {
        var registry = new ResearchRegistry();
        var task = registry.Start("trun_1", "Apollo figures", "objective", TaskProcessor.BaseFast);

        registry.Complete(task, new Dictionary<string, object?> { ["missions"] = 6 }, [
            new FieldBasis { Field = "missions", Reasoning = "NASA chronology.", Confidence = "high" }
        ]);

        Assert.True(task.IsComplete);
        Assert.False(task.IsActive);
        Assert.False(task.IsFailed);
        Assert.Equal("high", task.BasisFor("missions")!.Confidence);
    }

    [Fact]
    public void FailIsTerminalAndDistinctFromStillRunning()
    {
        var registry = new ResearchRegistry();
        var task = registry.Start("trun_1", "d", "o", TaskProcessor.Lite);

        registry.Fail(task, "No sources found");

        Assert.True(task.IsFailed);
        Assert.False(task.IsActive);
        Assert.False(task.IsComplete);
        Assert.Equal("No sources found", task.Error);
    }

    /// <summary>
    /// <c>action_required</c> is neither active nor finished. A boolean "completed" would have folded
    /// it into "not yet" and invited a caller to poll a run that will never move.
    /// </summary>
    [Fact]
    public void ActionRequiredIsNeitherRunningNorFinished()
    {
        var registry = new ResearchRegistry();
        var task = registry.Start("trun_1", "d", "o", TaskProcessor.Lite);

        registry.SetStatus(task, "action_required");

        Assert.True(task.NeedsAction);
        Assert.False(task.IsActive);
        Assert.False(task.IsComplete);
        Assert.False(task.IsFailed);
    }

    [Fact]
    public void GetIsNullForAnUnknownOrBlankId()
    {
        var registry = new ResearchRegistry();
        registry.Start("trun_1", "d", "o", TaskProcessor.Lite);

        Assert.NotNull(registry.Get("trun_1"));
        Assert.Null(registry.Get("trun_2"));
        Assert.Null(registry.Get(null));
        Assert.Null(registry.Get("  "));
    }
    #endregion

    #region The Research global

    [Fact]
    public void LatestAndFindLocateATaskWithoutCarryingAnId()
    {
        var registry = new ResearchRegistry();
        registry.Start("trun_1", "Apollo mission figures", "o", TaskProcessor.BaseFast);
        registry.Start("trun_2", "Launch site coordinates", "o", TaskProcessor.BaseFast);
        var research = new ResearchToolkit(registry);

        Assert.Equal(2, research.Count);
        Assert.Equal("trun_2", research.Latest!.Id);
        Assert.Equal("trun_1", research.Find("apollo")!.Id);      // case-insensitive
        Assert.Equal("trun_2", research.Find("coordinates")!.Id);
        Assert.Null(research.Find("nothing like this"));
        Assert.Null(research.Find("  "));
    }

    [Fact]
    public void AnEmptyRegistryIsSafeToRead()
    {
        var research = new ResearchToolkit(new ResearchRegistry());

        Assert.Equal(0, research.Count);
        Assert.Null(research.Latest);
        Assert.Empty(research.Tasks);
        Assert.Null(research.Get("anything"));
        Assert.True(research.AllComplete());   // vacuously; nothing was commissioned
    }

    [Fact]
    public void AllCompleteIsFalseWhileAnythingIsOutstanding()
    {
        var registry = new ResearchRegistry();
        var one = registry.Start("trun_1", "d", "o", TaskProcessor.Lite);
        var two = registry.Start("trun_2", "d", "o", TaskProcessor.Lite);
        var research = new ResearchToolkit(registry);

        registry.Complete(one, new Dictionary<string, object?>(), []);
        Assert.False(research.AllComplete());

        registry.Complete(two, new Dictionary<string, object?>(), []);
        Assert.True(research.AllComplete());
    }
    #endregion

    #region Citation

    [Fact]
    public void CiteFieldGivesACaptionLineForOneRow()
    {
        var registry = new ResearchRegistry();
        var task = registry.Start("trun_1", "d", "o", TaskProcessor.BaseFast);

        registry.Complete(task, null, [
            new FieldBasis
            {
                Field = "missions.0",
                Reasoning = "NASA lists Apollo 11's launch as July 16, 1969.",
                Citations = [
                    new TaskCitation { Url = "https://www.nasa.gov/apollo-11", Title = "Apollo 11 - NASA" },
                    new TaskCitation { Url = "https://www.nasa.gov/missions", Title = "Apollo Missions" },
                ],
            },
            new FieldBasis { Field = "missions.1", Reasoning = "No source.", Citations = null },
        ]);

        Assert.Equal("Apollo 11 - NASA — nasa.gov; Apollo Missions — nasa.gov", task.CiteField("missions.0"));

        // A field with no sources says so by returning null, rather than an empty-looking caption.
        Assert.Null(task.CiteField("missions.1"));
        Assert.Null(task.CiteField("no_such_field"));
    }

    [Fact]
    public void SourcesDeduplicatesAcrossEveryField()
    {
        var registry = new ResearchRegistry();
        var task = registry.Start("trun_1", "d", "o", TaskProcessor.BaseFast);
        var nasa = new TaskCitation { Url = "https://www.nasa.gov/a", Title = "NASA" };

        registry.Complete(task, null, [
            new FieldBasis { Field = "a", Reasoning = "r", Citations = [nasa] },
            new FieldBasis { Field = "b", Reasoning = "r", Citations = [nasa] },
            new FieldBasis { Field = "c", Reasoning = "r", Citations = [
                new TaskCitation { Url = "https://worldspaceflight.com/x" }] },
        ]);

        Assert.Equal(["NASA — nasa.gov", "worldspaceflight.com"], task.Sources());
    }
    #endregion

    #region JSON to JavaScript

    /// <summary>
    /// The conversion that makes <c>result.missions[0].mission</c> work in a script. A raw
    /// <see cref="JsonElement"/> would reach Jint as an opaque struct and index into nothing.
    /// </summary>
    [Fact]
    public void JsonBecomesOrdinaryDictionariesAndLists()
    {
        using var document = JsonDocument.Parse("""
            { "missions": [ { "mission": "Apollo 11", "duration_hours": 195.3, "landed": true, "note": null } ],
              "count": 6 }
            """);

        var root = Assert.IsType<Dictionary<string, object?>>(JsonInterop.ToClr(document.RootElement));
        var missions = Assert.IsType<List<object?>>(root["missions"]);
        var first = Assert.IsType<Dictionary<string, object?>>(missions[0]);

        Assert.Equal("Apollo 11", first["mission"]);
        Assert.Equal(195.3, Assert.IsType<double>(first["duration_hours"]), 3);
        Assert.True(Assert.IsType<bool>(first["landed"]));
        Assert.Null(first["note"]);

        // A whole number stays integral, so a count reads as 6 rather than 6.0.
        Assert.Equal(6L, Assert.IsType<long>(root["count"]));
    }

    [Fact]
    public void ANullElementConvertsToNullRatherThanThrowing() =>
        Assert.Null(JsonInterop.ToClr((JsonElement?)null));
    #endregion
}

file static class ReflectionExtensions
{
    /// <summary>Property getters appear among a type's methods; they are not the writable surface.</summary>
    internal static bool IsSpecialGetter(this System.Reflection.MethodInfo method) =>
        method.IsSpecialName && method.Name.StartsWith("get_", StringComparison.Ordinal);
}
