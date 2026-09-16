namespace Polson.Tests.CLI;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Polson.CLI;
using Xunit;

/// <summary>
/// <c>polson reset</c> — clearing a run without rebuilding the project around it.
/// </summary>
/// <remarks>
/// The verb exists because <c>create-project --reset</c> does two things and only one of them is
/// always wanted. That flag clears the run <em>and regenerates every generated file</em>, which is
/// right when the templates have moved and wrong when the instructions have been edited by hand —
/// and it needs the parent directory, the id and the sdk restated to do it. This clears the run and
/// stops, from the project's own path.
/// <para>
/// So the property that separates them is what survives, and most of what follows tests exactly
/// that.
/// </para>
/// </remarks>
public class ResetVerbTests : TestsRuntime, IDisposable
{
    #region Constructors
    public ResetVerbTests()
    {
        root = Path.Combine(Path.GetTempPath(), "polson-resetverb-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
    }
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    /// <summary>A project that has been run, and edited by hand the way a real one is.</summary>
    private string Used(string name, Action<CreateProjectOptions>? configure = null)
    {
        var opts = new CreateProjectOptions
        {
            Directory = root, Id = name, Sdk = "claude", Prompt = "original brief",
            Workflow = "drawing", Type = "review"
        };
        configure?.Invoke(opts);
        Assert.True(ProjectGenerator.Create(opts));

        var dir = Path.Combine(root, name);
        File.AppendAllText(Path.Combine(dir, "brief.md"), "\nHAND-WRITTEN DATA TABLE\n");
        File.AppendAllText(Path.Combine(dir, "CLAUDE.md"), "\nHAND-EDITED INSTRUCTION\n");

        Directory.CreateDirectory(Path.Combine(dir, "artifacts"));
        Directory.CreateDirectory(Path.Combine(dir, ".polson", "research"));
        File.WriteAllText(Path.Combine(dir, ".polson", "research", "run-1.json"), "{}");
        File.WriteAllText(Path.Combine(dir, "events", "server.jsonl"), """{"type":"run.start"}""");
        File.WriteAllText(Path.Combine(dir, "scripts", "0001.js"), "var x = 1;");
        File.WriteAllText(Path.Combine(dir, "artifacts", "stage1.webp"), "not really an image");
        File.WriteAllText(Path.Combine(dir, "findings.md"), "the previous run's report");
        return dir;
    }

    private static bool Reset(string dir, bool delete = false) =>
        ProjectReset.Run(new ResetOptions { ProjectDir = dir, Delete = delete });

    private static bool Has(string dir, string name) =>
        File.Exists(Path.Combine(dir, name)) || Directory.Exists(Path.Combine(dir, name));
    #endregion

    #region What it clears
    /// <summary>The run leaves the project root, and lands under previous/ rather than nowhere.</summary>
    [Fact]
    public void TestItArchivesTheRunByDefault()
    {
        var dir = Used("archived");

        Assert.True(Reset(dir));

        foreach (var name in ProjectGenerator.RunOutput)
        {
            Assert.False(Has(dir, name), $"{name} is still in the project root");
        }

        var archive = Directory.GetDirectories(Path.Combine(dir, "previous")).Single();
        Assert.True(File.Exists(Path.Combine(archive, "events", "server.jsonl")));
        Assert.True(File.Exists(Path.Combine(archive, "scripts", "0001.js")));
        Assert.True(File.Exists(Path.Combine(archive, "artifacts", "stage1.webp")));
        Assert.True(File.Exists(Path.Combine(archive, "findings.md")));
        Assert.True(File.Exists(Path.Combine(archive, "README.md")), "the archive does not say what it is");
    }

    /// <summary>
    /// <c>--delete</c> removes, and leaves no archive behind to suggest otherwise.
    /// </summary>
    /// <remarks>
    /// The empty-archive case is the one worth asserting: a `previous/&lt;stamp&gt;/` holding nothing
    /// would read as "your run is safe in here" to someone who had just deleted it.
    /// </remarks>
    [Fact]
    public void TestDeleteRemovesTheRunAndLeavesNoArchive()
    {
        var dir = Used("deleted");

        Assert.True(Reset(dir, delete: true));

        foreach (var name in ProjectGenerator.RunOutput)
        {
            Assert.False(Has(dir, name), $"{name} survived --delete");
        }

        Assert.False(Directory.Exists(Path.Combine(dir, "previous")), "--delete still made an archive");
    }
    #endregion

    #region What it keeps
    /// <summary>
    /// Hand-edited instructions survive, which is the whole reason this verb exists beside the flag.
    /// </summary>
    /// <remarks>
    /// <c>create-project --reset</c> regenerates <c>CLAUDE.md</c> from the template, so an edit made
    /// to the instructions for this project is gone. A director who has tuned the instructions and
    /// wants to run the piece again wants this, and had to hand-delete three directories to get it.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestItKeepsEverythingAPersonWroteOrTheGeneratorMade(bool delete)
    {
        var dir = Used(delete ? "keepdel" : "keeparc");

        Assert.True(Reset(dir, delete));

        Assert.Contains("HAND-WRITTEN DATA TABLE", File.ReadAllText(Path.Combine(dir, "brief.md")),
            StringComparison.Ordinal);
        Assert.Contains("HAND-EDITED INSTRUCTION", File.ReadAllText(Path.Combine(dir, "CLAUDE.md")),
            StringComparison.Ordinal);

        // The generated surface, untouched rather than regenerated.
        Assert.True(Has(dir, "project.json"));
        Assert.True(Has(dir, ".mcp.json"));

        // A reset of the work is not a reset of the spend.
        Assert.True(File.Exists(Path.Combine(dir, ".polson", "research", "run-1.json")),
            "the research cache was cleared with the run");
    }

    /// <summary>It does not regenerate, so it cannot change what the project makes.</summary>
    [Fact]
    public void TestItDoesNotRewriteTheManifest()
    {
        var dir = Used("stable");
        var before = File.ReadAllText(Path.Combine(dir, "project.json"));

        Assert.True(Reset(dir));

        Assert.Equal(before, File.ReadAllText(Path.Combine(dir, "project.json")));
        Assert.Equal("drawing", JsonDocument.Parse(before).RootElement.GetProperty("workflow").GetString());
    }
    #endregion

    #region What it refuses
    /// <summary>
    /// A directory that is not a project is refused rather than rearranged.
    /// </summary>
    /// <remarks>
    /// This verb moves and deletes from a path a person typed, so the guard is what stands between a
    /// mistyped argument and somebody's `artifacts/` folder elsewhere on disk.
    /// </remarks>
    [Fact]
    public void TestSomethingThatIsNotAProjectIsRefused()
    {
        var stranger = Path.Combine(root, "stranger");
        Directory.CreateDirectory(Path.Combine(stranger, "events"));
        File.WriteAllText(Path.Combine(stranger, "events", "server.jsonl"), "{}");

        Assert.False(Reset(stranger));
        Assert.True(File.Exists(Path.Combine(stranger, "events", "server.jsonl")), "it cleared a non-project");
    }

    /// <summary>A path that is not there is refused, not created.</summary>
    [Fact]
    public void TestAMissingDirectoryIsRefused()
    {
        var missing = Path.Combine(root, "nowhere");

        Assert.False(Reset(missing));
        Assert.False(Directory.Exists(missing));
    }

    /// <summary>
    /// A file something else holds open does not take its whole directory hostage, and the reset
    /// says it did not finish.
    /// </summary>
    /// <remarks>
    /// <b>Observed on a real project, which is why this test exists.</b> One render was open in a
    /// viewer; <c>Directory.Move</c> is all-or-nothing, so the entire <c>artifacts/</c> folder
    /// stayed while the record and the scripts archived — and the summary still reported a
    /// successful archive and exited 0.
    /// <para>
    /// Half-cleared is the worst of the three states: the next run reads renders belonging to a
    /// record that is no longer there, and nothing downstream can tell. So the directory is retried
    /// entry by entry, everything unlocked still reaches the archive, and what is left is reported
    /// as a failure.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestALockedFileDoesNotStrandItsWholeDirectory()
    {
        var dir = Used("locked");
        File.WriteAllText(Path.Combine(dir, "artifacts", "002_ink.png"), "second render");

        // Exclusive, as a viewer or a server streaming the file would hold it.
        using (var _ = new FileStream(Path.Combine(dir, "artifacts", "stage1.webp"),
                   FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(Reset(dir), "a partial clear reported success");
        }

        var archive = Directory.GetDirectories(Path.Combine(dir, "previous")).Single();

        // The lock cost exactly one file, not the folder and not the run.
        Assert.True(File.Exists(Path.Combine(archive, "artifacts", "002_ink.png")), "the unlocked render did not archive");
        Assert.True(File.Exists(Path.Combine(archive, "events", "server.jsonl")), "the record did not archive");
        Assert.True(File.Exists(Path.Combine(dir, "artifacts", "stage1.webp")), "the locked file vanished");

        // And a second pass finishes the job once the handle is gone.
        Assert.True(Reset(dir));
        Assert.False(Directory.Exists(Path.Combine(dir, "artifacts")));
    }

    /// <summary>
    /// A reset that moved nothing leaves no dated folder behind to look like an archive.
    /// </summary>
    /// <remarks>
    /// Found by running the locked case twice: the failed attempt had already created
    /// <c>previous/&lt;stamp&gt;/</c> before the move threw, so a project waiting on a handle
    /// accumulated empty archives that are indistinguishable from real ones in a directory listing.
    /// </remarks>
    [Fact]
    public void TestAFailedClearLeavesNoEmptyArchive()
    {
        var dir = Used("noempties");

        using (var _ = new FileStream(Path.Combine(dir, "events", "server.jsonl"),
                   FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Reset(dir);
            Reset(dir);
        }

        foreach (var archive in Directory.GetDirectories(Path.Combine(dir, "previous")))
        {
            Assert.NotEmpty(Directory.GetFileSystemEntries(archive));
        }
    }

    /// <summary>
    /// Clearing a project with no run succeeds and makes no empty archive.
    /// </summary>
    /// <remarks>
    /// Idempotent on purpose: the verb states a desired end state, and a project already in it has
    /// not failed. An empty `previous/&lt;stamp&gt;/` per invocation would be the obvious wrong
    /// answer — directories accumulating to record that nothing happened.
    /// </remarks>
    [Fact]
    public void TestResettingAnAlreadyClearProjectIsHarmless()
    {
        var dir = Used("twice");
        Assert.True(Reset(dir));

        var archives = Directory.GetDirectories(Path.Combine(dir, "previous")).Length;

        Assert.True(Reset(dir));
        Assert.Equal(archives, Directory.GetDirectories(Path.Combine(dir, "previous")).Length);
    }
    #endregion

    #region Fields
    private readonly string root;
    #endregion
}
