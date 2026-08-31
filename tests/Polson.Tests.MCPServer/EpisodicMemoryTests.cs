namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Reading a project's earlier runs back out of its own record.
/// </summary>
/// <remarks>
/// The fixtures are written here rather than pointed at a real project, because a committed run is
/// a moving target — <c>events/server.jsonl</c> is appended to every time anyone opens it — and a
/// memory test that changes when someone runs the studio tells you nothing.
/// </remarks>
public class EpisodicMemoryTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-episodes-" + Guid.NewGuid().ToString("N")[..8]);
    #endregion

    #region Constructors
    public EpisodicMemoryTests() => Directory.CreateDirectory(Path.Combine(root, "events"));

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Methods (private)
    private void Write(params string[] lines) =>
        File.WriteAllLines(Path.Combine(root, "events", "server.jsonl"), lines);

    private static string Event(string type, string? stage = null, string? extra = null) =>
        $$"""{"ts":"2026-08-30T10:00:00.000Z","seq":1,"src":"server","type":"{{type}}"{{(stage is null ? "" : $",\"stage\":\"{stage}\"")}}{{extra ?? ""}}}""";

    /// <summary>Two finished runs and one in progress, which is the shape recall has to handle.</summary>
    private void WriteThreeRuns() => Write(
        Event("run.start"),
        Event("stage.begin", "Blocking"),
        Event("note", "Blocking", ",\"message\":\"massing the aqueduct arcade against the ridge line\""),
        Event("render", "Blocking", ",\"artifact\":\"artifacts/001_blocking.webp\""),
        Event("script.ok", "Blocking", ",\"script\":\"scripts/0001.js\""),
        Event("stage.begin", "Refine"),
        Event("note", "Refine", ",\"message\":\"cypress trees too regular, breaking the rhythm\""),
        Event("script.error", "Refine", ",\"script\":\"scripts/0002.js\",\"error\":\"boom\""),

        Event("run.start"),
        Event("stage.begin", "Blocking"),
        Event("note", "Blocking", ",\"message\":\"reusing the arcade proportions, they read well last time\""),
        Event("script.ok", "Blocking", ",\"script\":\"scripts/0003.js\""),

        Event("run.start"),
        Event("stage.begin", "Blocking"),
        Event("note", "Blocking", ",\"message\":\"this run is still going\""));
    #endregion

    #region Reading
    [Fact]
    public void TestAStageBecomesAnEpisodeCarryingItsIntentAndItsOutput()
    {
        WriteThreeRuns();

        var episode = EpisodicMemory.Read(root).First();

        Assert.Equal("Blocking", episode.Stage);
        Assert.Equal(1, episode.Run);
        Assert.Equal(["massing the aqueduct arcade against the ridge line"], episode.Notes);
        Assert.Equal(["artifacts/001_blocking.webp"], episode.Artifacts);
        Assert.Equal(["scripts/0001.js"], episode.Scripts);
        Assert.Equal(1, episode.Executions);
        Assert.Equal(0, episode.Failures);
    }

    /// <summary>
    /// The run in progress is working memory, and <c>History</c> already serves it. Recalling it
    /// would also let a run rank its own half-written notes above a finished run's.
    /// </summary>
    [Fact]
    public void TestTheCurrentRunIsExcludedUnlessAskedFor()
    {
        WriteThreeRuns();

        Assert.DoesNotContain(EpisodicMemory.Read(root), e => e.Run == 3);
        Assert.Contains(EpisodicMemory.Read(root, includeCurrentRun: true), e => e.Run == 3);
    }

    [Fact]
    public void TestFailuresAreCountedRatherThanDiscarded()
    {
        WriteThreeRuns();

        var refine = EpisodicMemory.Read(root).Single(e => e.Stage == "Refine");

        Assert.Equal(1, refine.Executions);
        Assert.Equal(1, refine.Failures);
    }

    /// <summary>A stage that opened and closed with nothing in it recalls nothing, so it is not an episode.</summary>
    [Fact]
    public void TestAnEmptyStageIsNotAnEpisode()
    {
        Write(Event("run.start"),
              Event("stage.begin", "Empty"),
              Event("stage.end", "Empty"),
              Event("run.start"));

        Assert.Empty(EpisodicMemory.Read(root));
    }

    /// <summary>Work done before any stage was declared is still work, and the name says so.</summary>
    [Fact]
    public void TestUndeclaredWorkIsFiledRatherThanDropped()
    {
        Write(Event("run.start"),
              Event("note", null, ",\"message\":\"drew before declaring anything\""),
              Event("run.start"));

        Assert.Equal("(undeclared)", EpisodicMemory.Read(root).Single().Stage);
    }

    /// <summary>
    /// A half-written last line is what an append-only log looks like when read while it is being
    /// appended to. Losing the whole memory over it would be a poor trade.
    /// </summary>
    [Fact]
    public void TestATruncatedLineDoesNotDestroyTheMemory()
    {
        Write(Event("run.start"),
              Event("note", "Blocking", ",\"message\":\"a real note\""),
              Event("run.start"),
              "{\"ts\":\"2026-08-30T10:00:00.000Z\",\"type\":\"no");

        Assert.Equal("a real note", EpisodicMemory.Read(root).Single().Notes.Single());
    }

    [Fact]
    public void TestAProjectWithNoRecordRecallsNothingRatherThanThrowing()
    {
        Assert.Empty(EpisodicMemory.Read(root));
        Assert.Empty(EpisodicMemory.Read(null));
        Assert.Empty(EpisodicMemory.Read(Path.Combine(root, "nope")));
    }
    #endregion

    #region Ranking
    [Fact]
    public void TestRecallFindsTheEpisodeWhoseNoteMatches()
    {
        WriteThreeRuns();
        var episodes = EpisodicMemory.Read(root);

        var top = EpisodicMemory.Recall(episodes, "cypress trees rhythm", 3).First();

        Assert.Equal("Refine", top.Episode.Stage);
        Assert.True(top.Score > 0);
    }

    /// <summary>Ties break toward the later run: the more recent attempt is the more useful memory.</summary>
    [Fact]
    public void TestTheMoreRecentRunWinsATie()
    {
        Write(Event("run.start"),
              Event("note", "Blocking", ",\"message\":\"arcade proportions\""),
              Event("run.start"),
              Event("note", "Blocking", ",\"message\":\"arcade proportions\""),
              Event("run.start"));

        var top = EpisodicMemory.Recall(EpisodicMemory.Read(root), "arcade proportions", 5).First();

        Assert.Equal(2, top.Episode.Run);
    }

    [Fact]
    public void TestAQueryMatchingNothingRecallsNothing()
    {
        WriteThreeRuns();

        Assert.Empty(EpisodicMemory.Recall(EpisodicMemory.Read(root), "typography kerning ligature", 5));
        Assert.Empty(EpisodicMemory.Recall(EpisodicMemory.Read(root), "   ", 5));
    }

    [Fact]
    public void TestTheEpisodeUriNamesTheProjectRunAndStage()
    {
        WriteThreeRuns();

        var uri = EpisodicMemory.Read(root).First().Uri;

        Assert.StartsWith("polson://episode/", uri, StringComparison.Ordinal);
        Assert.EndsWith("/run1/Blocking", uri, StringComparison.Ordinal);
    }
    #endregion

    #region The Recall tool
    /// <summary>
    /// Three outcomes, and the tool has to tell them apart in words the agent can act on.
    /// </summary>
    /// <remarks>
    /// "Search cannot express a negative" is this project's most expensive retrieval defect — a
    /// query for something absent returns adjacent-but-wrong hits with no signal that the corpus
    /// has nothing, and it once convinced an agent that vector gradients did not exist. Recall must
    /// not repeat it: a project with no past and a past that does not mention the query are
    /// different facts, and neither is "no results".
    /// </remarks>
    [Fact]
    public void TestNoEarlierRunIsSaidPlainlyRatherThanReturnedAsEmpty()
    {
        Write(Event("run.start"), Event("note", "Blocking", ",\"message\":\"only run\""));

        var answer = new DrawingMcpTools(projectRoot: root).Recall("anything at all");

        Assert.Equal(0, answer["runs"]!.GetValue<int>());
        Assert.Contains("no earlier run", answer["hint"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestAPastThatDoesNotMatchIsADifferentAnswerFromNoPast()
    {
        WriteThreeRuns();

        var answer = new DrawingMcpTools(projectRoot: root).Recall("typography kerning ligature");

        Assert.True(answer["runs"]!.GetValue<int>() > 0);
        Assert.Equal(0, answer["count"]!.GetValue<int>());
        Assert.Contains("none of their episodes matched", answer["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void TestAMatchCarriesTheIntentAndWhatItProduced()
    {
        WriteThreeRuns();

        var answer = new DrawingMcpTools(projectRoot: root).Recall("cypress trees rhythm");
        var first = answer["results"]!.AsArray()[0]!;

        Assert.Equal("Refine", first["stage"]!.GetValue<string>());
        Assert.Equal(1, first["run"]!.GetValue<int>());
        Assert.Equal(1, first["failures"]!.GetValue<int>());
        Assert.Contains("cypress", first["notes"]!.AsArray()[0]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>The tool is registered, so a host actually offers it.</summary>
    [Fact]
    public void TestRecallIsAnAdvertisedTool()
    {
        var tool = typeof(DrawingMcpTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttribute<ModelContextProtocol.Server.McpServerToolAttribute>())
            .FirstOrDefault(a => a?.Name == "Recall");

        Assert.NotNull(tool);
    }

    /// <summary>A server with no project directory recalls nothing and says why, rather than throwing.</summary>
    [Fact]
    public void TestAServerWithNoProjectRecallsNothing()
    {
        var answer = new DrawingMcpTools().Recall("anything");

        Assert.Equal(0, answer["runs"]!.GetValue<int>());
        Assert.Null(answer["project"]);
    }
    #endregion
}
