namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Polson.ExtendedMind.ParallelSearch;
using Xunit;

/// <summary>
/// A finished research run is filed to disk, so the figures drawn from it stay checkable.
/// </summary>
/// <remarks>
/// <b>What this closes.</b> <c>ResearchRegistry</c> keeps its tasks in a dictionary and the run
/// record carries only the run id, the processor and the elapsed seconds — never the data. So once a
/// session ended, the sole surviving account of where a number on the canvas came from was the
/// agent's own transcription of it into <c>brief.md</c>. Anyone auditing afterwards held the
/// citation's name and no way to read it, which makes "the figures are sourced" a claim about a run
/// rather than about a graphic.
/// </remarks>
public class ResearchArchiveTests : TestsRuntime, IDisposable
{
    #region Constructors
    public ResearchArchiveTests()
    {
        root = Path.Combine(Path.GetTempPath(), "polson-research-" + Guid.NewGuid().ToString("N")[..8]);
    }
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private static ResearchTask Finished(string id = "trun_abc123") => new()
    {
        Id = id,
        Description = "Kubrick filmography",
        Objective = "Every feature film, its release year and runtime.",
        Processor = "base",
        StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
    };

    private static IReadOnlyList<FieldBasis> Basis() =>
    [
        new FieldBasis
        {
            Field = "films.0.runtime",
            Reasoning = "Runtime given as 62 minutes by both cited sources.",
            Confidence = "high",
        },
    ];
    #endregion

    #region Tests
    /// <summary>Completing a run writes it, under its own id.</summary>
    [Fact]
    public void TestCompletingARunFilesIt()
    {
        var archive = new ResearchArchive(root);
        var registry = new ResearchRegistry(2, archive);
        var task = Finished();

        registry.Complete(task, new Dictionary<string, object?> { ["total"] = 1636 }, Basis());

        Assert.True(File.Exists(archive.PathFor("trun_abc123")));
    }

    /// <summary>
    /// The file carries the basis, not only the result.
    /// </summary>
    /// <remarks>
    /// A file holding the result alone would let a reader check that a number was transcribed
    /// faithfully and tell them nothing about whether it was ever true. The citation and the
    /// confidence are what make a figure checkable rather than merely present, so their absence
    /// would leave this archiving the wrong half.
    /// </remarks>
    [Fact]
    public void TestTheBasisIsFiledWithTheResult()
    {
        var archive = new ResearchArchive(root);
        new ResearchRegistry(2, archive).Complete(
            Finished(), new Dictionary<string, object?> { ["total"] = 1636 }, Basis());

        var read = archive.Load("trun_abc123");

        Assert.NotNull(read);
        Assert.Single(read!.Basis);
        Assert.Equal("films.0.runtime", read.Basis[0].Field);
        Assert.Equal("high", read.Basis[0].Confidence);
    }

    /// <summary>The question is filed beside the answer.</summary>
    /// <remarks>
    /// A file naming only its result answers "what did it say" and not "what was it asked", and the
    /// second is where a wrong figure usually starts — a right answer to a question about the wrong
    /// period reads perfectly.
    /// </remarks>
    [Fact]
    public void TestTheObjectiveAndProcessorAreFiledToo()
    {
        var archive = new ResearchArchive(root);
        new ResearchRegistry(2, archive).Complete(Finished(), new Dictionary<string, object?>(), Basis());

        var read = archive.Load("trun_abc123");

        Assert.NotNull(read);
        Assert.Equal("Every feature film, its release year and runtime.", read!.Objective);
        Assert.Equal("base", read.Processor);
        Assert.Equal("completed", read.Status);
    }

    /// <summary>The result survives the round trip as data rather than as a string.</summary>
    [Fact]
    public void TestTheResultIsReadableJson()
    {
        var archive = new ResearchArchive(root);
        new ResearchRegistry(2, archive).Complete(
            Finished(), new Dictionary<string, object?> { ["totalRuntime"] = 1636 }, Basis());

        using var read = JsonDocument.Parse(File.ReadAllText(archive.PathFor("trun_abc123")));

        Assert.Equal(1636, read.RootElement.GetProperty("result").GetProperty("totalRuntime").GetInt32());
    }

    /// <summary>
    /// A registry with no archive still completes its runs.
    /// </summary>
    /// <remarks>
    /// The ad-hoc engine has no project to file into. Archiving is a receipt, and a surface that
    /// cannot work without one would make every scriptable use of research depend on a directory.
    /// </remarks>
    [Fact]
    public void TestARegistryWithNoArchiveStillWorks()
    {
        var registry = new ResearchRegistry(2);
        var task = Finished();

        registry.Complete(task, new Dictionary<string, object?> { ["x"] = 1 }, Basis());

        Assert.Equal("completed", task.Status);
        Assert.NotNull(task.Result);
    }

    /// <summary>
    /// An archive that cannot write does not fail the run.
    /// </summary>
    /// <remarks>
    /// The same discipline <c>EventLog</c> applies to its own writes: a run that dies because its
    /// receipt could not be written is a worse outcome than a run with no receipt. The research is
    /// already paid for and already in memory by this point — losing the drawing over the archive
    /// would be the tail wagging the dog.
    /// </remarks>
    [Fact]
    public void TestAnUnwritableArchiveIsSwallowed()
    {
        // A file where the directory needs to be, so creating it cannot succeed.
        Directory.CreateDirectory(root);
        var blocked = Path.Combine(root, "blocked");
        File.WriteAllText(blocked, "not a directory");

        var task = Finished();
        var registry = new ResearchRegistry(2, new ResearchArchive(blocked));

        registry.Complete(task, new Dictionary<string, object?> { ["x"] = 1 }, Basis());

        Assert.Equal("completed", task.Status);
        Assert.NotNull(task.Result);
    }

    /// <summary>A run id from a third party cannot escape the folder.</summary>
    /// <remarks>
    /// The id comes back from a service, so it is not ours to trust as a path segment however
    /// well-formed every observed one has been. Same reasoning as the upload filename check.
    /// </remarks>
    [Theory]
    [InlineData("../../escape")]
    [InlineData("trun/../../x")]
    public void TestARunIdCannotTraverse(string id)
    {
        var archive = new ResearchArchive(root);
        new ResearchRegistry(2, archive).Complete(
            Finished(id), new Dictionary<string, object?>(), Basis());

        var written = Directory.Exists(root) ? Directory.GetFiles(root) : [];

        Assert.All(written, f => Assert.Equal(Path.GetFullPath(root), Path.GetDirectoryName(Path.GetFullPath(f))));
    }

    /// <summary>Reading a run that was never filed says so rather than throwing.</summary>
    [Fact]
    public void TestLoadingAnAbsentRunReturnsNull()
    {
        Assert.Null(new ResearchArchive(root).Load("trun_never_ran"));
    }
    #endregion

    #region Fields
    private readonly string root;
    #endregion
}
