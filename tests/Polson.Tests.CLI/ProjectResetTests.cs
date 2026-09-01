namespace Polson.Tests.CLI;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Polson.CLI;
using Xunit;

/// <summary>
/// <c>create-project --reset</c> — clearing a run without destroying what a person wrote.
/// </summary>
/// <remarks>
/// The distinction from <c>--force</c> is the whole point. <c>--force</c> regenerates every
/// generated file, <c>brief.md</c> included, which has already cost a hand-written brief once.
/// <c>--reset</c> keeps the two files a person edits and clears what the engine produced, so a
/// project can be run again from clean without retyping the thing the run is about.
/// </remarks>
public class ProjectResetTests : TestsRuntime, IDisposable
{
    #region Constructors
    public ProjectResetTests()
    {
        root = Path.Combine(Path.GetTempPath(), "polson-reset-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
    }
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private CreateProjectOptions Options(string name, Action<CreateProjectOptions>? configure = null)
    {
        var opts = new CreateProjectOptions { Directory = root, Id = name, Sdk = "claude", Prompt = "original brief" };
        configure?.Invoke(opts);
        return opts;
    }

    private string Project(string name) => Path.Combine(root, name);

    /// <summary>Creates a project and dirties it the way a real run would.</summary>
    private void Used(string name)
    {
        Assert.True(ProjectGenerator.Create(Options(name)));

        var dir = Project(name);
        File.AppendAllText(Path.Combine(dir, "brief.md"), "\nHAND-WRITTEN DATA TABLE\n");
        File.AppendAllText(Path.Combine(dir, "CLAUDE.md"), "\nHAND-EDITED INSTRUCTION\n");
        File.WriteAllText(Path.Combine(dir, "events", "server.jsonl"), """{"type":"run.start"}""");
        File.WriteAllText(Path.Combine(dir, "events", "chat-abc.jsonl"), "{}");
        File.WriteAllText(Path.Combine(dir, "scripts", "0001.js"), "var x = 1;");
        File.WriteAllText(Path.Combine(dir, "artifacts", "stage1.webp"), "not really an image");
    }

    private static int Count(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length : 0;
    #endregion

    #region What a reset keeps
    /// <summary>
    /// The brief survives, edits and all.
    /// </summary>
    /// <remarks>
    /// The single most important property. For a data-driven workflow the brief holds the whole data
    /// table, typed by hand; regenerating it would replace the run's subject with an empty template
    /// and the loss would only be noticed later.
    /// </remarks>
    [Fact]
    public void TestTheEditedBriefSurvives()
    {
        Used("keep-brief");
        Assert.True(ProjectGenerator.Create(Options("keep-brief", o => o.Reset = true)));

        Assert.Contains("HAND-WRITTEN DATA TABLE",
            File.ReadAllText(Path.Combine(Project("keep-brief"), "brief.md")), StringComparison.Ordinal);
    }

    /// <summary>
    /// A reset archives the previous run rather than deleting it.
    /// </summary>
    /// <remarks>
    /// Deleting cost a real baseline: a four-agent run whose measurements were the only evidence for
    /// what the next change was worth, gone to a re-run. It came back only because the host keeps its
    /// own transcripts outside the project, which is luck rather than design. Renaming aside keeps
    /// every property the delete had — the new run starts clean, nothing stale is left to mislead it
    /// — and gives up none of the evidence.
    /// </remarks>
    [Fact]
    public void TestTheRunIsArchivedRatherThanDeleted()
    {
        Used("archive");
        var dir = Project("archive");

        // The files the run authored about itself, which a reset used to leave in place — so the
        // project held a report describing a record that had just been deleted.
        File.WriteAllText(Path.Combine(dir, "findings.md"), "PREVIOUS FINDINGS");
        File.WriteAllText(Path.Combine(dir, "critique_log.md"), "PREVIOUS CRITIQUE");
        File.WriteAllText(Path.Combine(dir, "artwork.js"), "// previous artwork");

        Assert.True(ProjectGenerator.Create(Options("archive", o => o.Reset = true)));

        // Gone from where the next run will look.
        Assert.False(File.Exists(Path.Combine(dir, "findings.md")));
        Assert.False(File.Exists(Path.Combine(dir, "critique_log.md")));
        Assert.False(File.Exists(Path.Combine(dir, "artwork.js")));
        Assert.Equal(0, Count(Path.Combine(dir, "scripts")));

        // ...and all of it still on disk, under one timestamped directory that says what it is.
        var archive = Directory.GetDirectories(Path.Combine(dir, "previous")).Single();
        var kept = Directory.GetFiles(archive, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetFileName(p)).ToArray();

        Assert.Contains("findings.md", kept);
        Assert.Contains("critique_log.md", kept);
        Assert.Contains("artwork.js", kept);
        Assert.Contains("server.jsonl", kept);
        Assert.Contains("0001.js", kept);
        Assert.Contains("stage1.webp", kept);
        Assert.Contains("README.md", kept);

        Assert.Equal("PREVIOUS FINDINGS",
            File.ReadAllText(Path.Combine(archive, "findings.md")));
    }

    /// <summary>The archive is ignored by git: a local safety net, not the project's history.</summary>
    [Fact]
    public void TestTheArchiveIsGitIgnored()
    {
        Assert.True(ProjectGenerator.Create(Options("archive-ignored")));

        Assert.Contains("previous/",
            File.ReadAllText(Path.Combine(Project("archive-ignored"), ".gitignore")), StringComparison.Ordinal);
    }

    /// <summary>
    /// The instructions file is <b>rewritten</b>, because it is the system prompt rather than the
    /// director's document.
    /// </summary>
    /// <remarks>
    /// The reason is concrete: an engine-only rule was added to the templates, and while a reset
    /// preserved the instructions, the only way to get that rule into an existing project was
    /// <c>--force</c> — which also discards the brief. A reset that freezes a project on whatever
    /// the template said the day it was created is the opposite of what a reset is for. An edit
    /// worth keeping belongs in the template, where every project gets it.
    /// </remarks>
    [Fact]
    public void TestTheInstructionsAreRegenerated()
    {
        Used("regen-instructions");
        Assert.True(ProjectGenerator.Create(Options("regen-instructions", o => o.Reset = true)));

        var instructions = File.ReadAllText(Path.Combine(Project("regen-instructions"), "CLAUDE.md"));

        Assert.DoesNotContain("HAND-EDITED INSTRUCTION", instructions, StringComparison.Ordinal);
        Assert.Contains("Two things go through the MCP server", instructions, StringComparison.Ordinal);
    }

    /// <summary>A reset picks up template changes made since the project was created.</summary>
    /// <remarks>
    /// The property that matters in practice, stated separately from the mechanism: whatever the
    /// template says now is what the project gets, so a rule tightened after a run reaches the run
    /// being re-done.
    /// </remarks>
    [Fact]
    public void TestAResetPicksUpTheCurrentTemplate()
    {
        Used("current-template");
        var path = Path.Combine(Project("current-template"), "CLAUDE.md");
        File.WriteAllText(path, "a stale instructions file from an older template");

        Assert.True(ProjectGenerator.Create(Options("current-template", o => o.Reset = true)));

        var instructions = File.ReadAllText(path);
        Assert.DoesNotContain("stale instructions file", instructions, StringComparison.Ordinal);
        Assert.Contains("Design Project: current-template", instructions, StringComparison.Ordinal);
    }
    #endregion

    #region What a reset clears
    /// <summary>Everything the engine produced goes.</summary>
    [Fact]
    public void TestTheRunIsCleared()
    {
        Used("clear-run");
        Assert.True(ProjectGenerator.Create(Options("clear-run", o => o.Reset = true)));

        var dir = Project("clear-run");
        Assert.Equal(0, Count(Path.Combine(dir, "events")));
        Assert.Equal(0, Count(Path.Combine(dir, "scripts")));
        Assert.Equal(0, Count(Path.Combine(dir, "artifacts")));
    }

    /// <summary>
    /// <c>scripts/</c> is cleared with the log, not left behind.
    /// </summary>
    /// <remarks>
    /// Those files look like source but are engine output — the server numbers and writes them.
    /// Keeping them while clearing <c>events/</c> would make <c>polson report</c> flag every one as
    /// unaccounted for: the reset would manufacture the exact warning that exists to catch work done
    /// outside the engine.
    /// </remarks>
    [Fact]
    public void TestScriptsAreClearedSoTheReportStillReconciles()
    {
        Used("reconcile");
        Assert.True(ProjectGenerator.Create(Options("reconcile", o => o.Reset = true)));

        Assert.Empty(Directory.GetFiles(Path.Combine(Project("reconcile"), "scripts")));
    }

    /// <summary>The requisition cache is left alone, because refilling it costs money.</summary>
    /// <remarks>
    /// A reset of the <i>work</i> should not be a reset of the <i>spend</i>. The cache is
    /// content-addressed, so anything still wanted is still a hit.
    /// </remarks>
    [Fact]
    public void TestTheAssetCacheIsNotCleared()
    {
        Used("keep-cache");
        var cache = Path.Combine(Project("keep-cache"), ".polson", "assets");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "oak.webp"), "expensive");

        Assert.True(ProjectGenerator.Create(Options("keep-cache", o => o.Reset = true)));

        Assert.True(File.Exists(Path.Combine(cache, "oak.webp")));
    }
    #endregion

    #region What a reset regenerates
    /// <summary>The supporting files come back, so a stale one cannot survive a reset.</summary>
    [Fact]
    public void TestSupportingFilesAreRegenerated()
    {
        Used("regen");
        var dir = Project("regen");
        File.WriteAllText(Path.Combine(dir, ".mcp.json"), "{ \"broken\": true }");

        Assert.True(ProjectGenerator.Create(Options("regen", o => o.Reset = true)));

        var wiring = File.ReadAllText(Path.Combine(dir, ".mcp.json"));
        Assert.Contains("mcpServers", wiring, StringComparison.Ordinal);
        Assert.DoesNotContain("broken", wiring, StringComparison.Ordinal);
    }

    /// <summary>A reset needs no brief, since it is keeping the one already there.</summary>
    [Fact]
    public void TestResetNeedsNoBrief()
    {
        Used("no-brief");

        Assert.True(ProjectGenerator.Create(Options("no-brief", o =>
        {
            o.Reset = true;
            o.Prompt = string.Empty;
        })));

        Assert.Contains("HAND-WRITTEN DATA TABLE",
            File.ReadAllText(Path.Combine(Project("no-brief"), "brief.md")), StringComparison.Ordinal);
    }
    #endregion

    #region Beside the other flags
    /// <summary>
    /// <c>--force</c> keeps its old meaning: it regenerates the brief.
    /// </summary>
    /// <remarks>
    /// Asserted rather than assumed, because the two flags now sit next to each other and the whole
    /// reason <c>--reset</c> exists is that they differ here.
    /// </remarks>
    [Fact]
    public void TestForceStillOverwritesTheBrief()
    {
        Used("force");

        Assert.True(ProjectGenerator.Create(Options("force", o => o.Force = true)));

        Assert.DoesNotContain("HAND-WRITTEN DATA TABLE",
            File.ReadAllText(Path.Combine(Project("force"), "brief.md")), StringComparison.Ordinal);
    }

    /// <summary>Neither flag on a used directory is still refused.</summary>
    [Fact]
    public void TestAUsedDirectoryIsStillRefusedWithoutAFlag()
    {
        Used("refused");

        Assert.False(ProjectGenerator.Create(Options("refused")));
    }

    /// <summary>A reset of a directory that does not exist is simply a create.</summary>
    [Fact]
    public void TestResettingAFreshDirectoryJustCreatesIt()
    {
        Assert.True(ProjectGenerator.Create(Options("fresh", o => o.Reset = true)));

        Assert.True(File.Exists(Path.Combine(Project("fresh"), "brief.md")));
        Assert.Contains("original brief",
            File.ReadAllText(Path.Combine(Project("fresh"), "brief.md")), StringComparison.Ordinal);
    }
    #endregion

    #region Fields
    private readonly string root;
    #endregion
    #region Profile Tests
    /// <summary>
    /// A reset clears the run. It does not decide what the project is.
    /// </summary>
    /// <remarks>
    /// Resetting a standalone project without repeating <c>--standalone</c> used to downgrade it to
    /// managed: <c>project.json</c> was rewritten, <c>agent.config.json</c> stopped being generated,
    /// and <c>session/</c> came out of <c>.gitignore</c> — which exposed SDK session state that was
    /// already on disk. The damage was silent and it happened to a real project.
    /// </remarks>
    [Fact]
    public void TestResettingAStandaloneProjectKeepsItStandalone()
    {
        Assert.True(ProjectGenerator.Create(Options("keepme", o => { o.Sdk = "agy"; o.Standalone = true; })));
        Assert.True(ProjectGenerator.Create(Options("keepme", o => { o.Sdk = "agy"; o.Reset = true; })));

        Assert.Equal("standalone", Profile("keepme"));
        Assert.True(File.Exists(Path.Combine(root, "keepme", "agent.config.json")));
        Assert.Contains("session/", File.ReadAllText(Path.Combine(root, "keepme", ".gitignore")),
            StringComparison.Ordinal);
    }

    /// <summary>A managed project stays managed, which is the case that always worked.</summary>
    [Fact]
    public void TestResettingAManagedProjectKeepsItManaged()
    {
        Assert.True(ProjectGenerator.Create(Options("stayput")));
        Assert.True(ProjectGenerator.Create(Options("stayput", o => o.Reset = true)));

        Assert.Equal("managed", Profile("stayput"));
        Assert.False(File.Exists(Path.Combine(root, "stayput", "agent.config.json")));
    }

    /// <summary>
    /// The flag still promotes: asking for standalone is a request, not an accident.
    /// </summary>
    [Fact]
    public void TestTheFlagStillPromotesAManagedProject()
    {
        Assert.True(ProjectGenerator.Create(Options("promote", o => o.Sdk = "agy")));
        Assert.True(ProjectGenerator.Create(Options("promote", o => { o.Sdk = "agy"; o.Reset = true; o.Standalone = true; })));

        Assert.Equal("standalone", Profile("promote"));
        Assert.True(File.Exists(Path.Combine(root, "promote", "agent.config.json")));
    }

    /// <summary>The profile is read from the project's own manifest, so an absent one is not standalone.</summary>
    [Fact]
    public void TestAProjectThatCannotSayWhatItIsGetsTheSaferDefault()
    {
        Assert.True(ProjectGenerator.Create(Options("amnesia", o => { o.Sdk = "agy"; o.Standalone = true; })));
        File.Delete(Path.Combine(root, "amnesia", "project.json"));

        Assert.True(ProjectGenerator.Create(Options("amnesia", o => { o.Sdk = "agy"; o.Reset = true; })));
        Assert.Equal("managed", Profile("amnesia"));
    }

    string Profile(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name, "project.json")))
            .RootElement.GetProperty("profile").GetString()!;
    #endregion

}
