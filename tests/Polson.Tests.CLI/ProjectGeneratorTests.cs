namespace Polson.Tests.CLI;

using System;
using System.IO;
using System.Text.Json;
using Polson.CLI;
using Xunit;

/// <summary>
/// What <c>create-project</c> actually writes to disk, and what it refuses to write.
/// <para>
/// The generator is the only thing standing between a client-supplied string and a directory
/// layout the agent will treat as its instructions, so the refusals matter as much as the output:
/// a project id is a path segment, and a workflow name selects which template becomes the agent
/// prompt. Neither may be taken on trust.
/// </para>
/// </summary>
public class ProjectGeneratorTests : TestsRuntime, IDisposable
{
    #region Fields
    readonly string root = Path.Combine(Path.GetTempPath(), "polson-gen-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Builds an option set pointing at a fresh subdirectory of this test's temp root.</summary>
    CreateProjectOptions Options(string name, Action<CreateProjectOptions>? configure = null)
    {
        var opts = new CreateProjectOptions { Directory = Path.Combine(root, name), Id = name };
        configure?.Invoke(opts);
        return opts;
    }
    #endregion

    #region Layout Tests
    [Theory]
    [InlineData("project.json")]
    [InlineData("brief.md")]
    [InlineData("GEMINI.md")]
    [InlineData(".mcp.json")]
    [InlineData("agent.config.json")]
    [InlineData(".gitignore")]
    public void TestExpectedFileIsWritten(string file)
    {
        Assert.True(ProjectGenerator.Create(Options("layout")));
        Assert.True(File.Exists(Path.Combine(root, "layout", file)), $"missing: {file}");
    }

    [Theory]
    [InlineData("artifacts")]
    [InlineData("scripts")]
    [InlineData("events")]
    [InlineData("session/save")]
    [InlineData("session/appdata")]
    public void TestExpectedDirectoryIsCreated(string dir)
    {
        Assert.True(ProjectGenerator.Create(Options("dirs")));
        Assert.True(Directory.Exists(Path.Combine(root, "dirs", dir)), $"missing: {dir}");
    }

    /// <summary>
    /// An unsubstituted placeholder would reach the agent as literal template syntax, which reads
    /// as a broken instruction rather than an instruction it can follow.
    /// </summary>
    [Theory]
    [InlineData("GEMINI.md")]
    [InlineData("brief.md")]
    public void TestNoPlaceholderSurvivesRendering(string file)
    {
        Assert.True(ProjectGenerator.Create(Options("tokens")));

        var body = File.ReadAllText(Path.Combine(root, "tokens", file));
        Assert.DoesNotContain("{{", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TestManifestRecordsWorkflowAndProfile()
    {
        Assert.True(ProjectGenerator.Create(Options("manifest")));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest", "project.json")));
        var r = doc.RootElement;

        Assert.Equal("manifest", r.GetProperty("id").GetString());
        Assert.Equal("logo", r.GetProperty("workflow").GetString());
        Assert.Equal("standalone", r.GetProperty("profile").GetString());
    }
    #endregion

    #region Brief Quoting Tests
    /// <summary>The brief must land inside the markers, which is what makes it quoted data.</summary>
    [Fact]
    public void TestBriefIsWrittenBetweenTheMarkers()
    {
        Assert.True(ProjectGenerator.Create(Options("brief", o => o.Brief = "A mark for Acme Freight.")));

        var body = File.ReadAllText(Path.Combine(root, "brief", "brief.md"));
        var begin = body.IndexOf("BRIEF-BEGIN", StringComparison.Ordinal);
        var end = body.IndexOf("BRIEF-END", StringComparison.Ordinal);
        var brief = body.IndexOf("A mark for Acme Freight.", StringComparison.Ordinal);

        Assert.True(begin >= 0 && end > begin, "markers missing or out of order");
        Assert.InRange(brief, begin, end);
    }

    /// <summary>A brief given as a file path is read; anything else is taken as the text itself.</summary>
    [Fact]
    public void TestBriefIsReadFromFileWhenPathExists()
    {
        Directory.CreateDirectory(root);
        var briefFile = Path.Combine(root, "supplied-brief.txt");
        File.WriteAllText(briefFile, "Brief supplied from a file.");

        Assert.True(ProjectGenerator.Create(Options("frombrieffile", o => o.Brief = briefFile)));

        var body = File.ReadAllText(Path.Combine(root, "frombrieffile", "brief.md"));
        Assert.Contains("Brief supplied from a file.", body, StringComparison.Ordinal);
        Assert.DoesNotContain(briefFile, body, StringComparison.Ordinal);
    }
    #endregion

    #region Tool Policy Tests
    /// <summary>
    /// Image generation is denied on every profile. It bypasses asset requisition entirely, so an
    /// agent holding it can produce a finished picture and the premise of the studio stops holding.
    /// </summary>
    [Theory]
    [InlineData("standalone")]
    [InlineData("managed")]
    public void TestImageGenerationIsDeniedOnEveryProfile(string profile)
    {
        Assert.True(ProjectGenerator.Create(Options("deny-" + profile, o => o.Profile = profile)));

        Assert.Contains("generate_image", DeniedTools("deny-" + profile));
    }

    /// <summary>A public URL must not reach a shell, so the standalone profile denies more.</summary>
    [Fact]
    public void TestShellIsDeniedOnlyOnStandalone()
    {
        Assert.True(ProjectGenerator.Create(Options("shell-standalone", o => o.Profile = "standalone")));
        Assert.True(ProjectGenerator.Create(Options("shell-managed", o => o.Profile = "managed")));

        Assert.Contains("run_command", DeniedTools("shell-standalone"));
        Assert.DoesNotContain("run_command", DeniedTools("shell-managed"));
    }

    /// <summary>
    /// The managed profile cannot enforce policy, because the desktop host owns it. The emitted
    /// config must say so rather than letting a reader assume otherwise.
    /// </summary>
    [Theory]
    [InlineData("standalone", true)]
    [InlineData("managed", false)]
    public void TestEnforcementIsDeclaredHonestly(string profile, bool expected)
    {
        Assert.True(ProjectGenerator.Create(Options("enforce-" + profile, o => o.Profile = profile)));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "enforce-" + profile, "agent.config.json")));
        Assert.Equal(expected, doc.RootElement.GetProperty("enforced").GetBoolean());
    }

    string DeniedTools(string name)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name, "agent.config.json")));
        return doc.RootElement.GetProperty("deniedTools").ToString();
    }
    #endregion

    #region Refusal Tests
    /// <summary>
    /// The id becomes a directory name, so anything that could traverse or escape is refused
    /// outright rather than sanitised into something that merely looks safe.
    /// </summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("dollar$sign")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    public void TestInvalidIdIsRefused(string id) =>
        Assert.False(ProjectGenerator.Create(new CreateProjectOptions
        {
            Directory = Path.Combine(root, "refused"),
            Id = id,
        }));

    /// <summary>
    /// An empty id is not an invalid id: it means "none was given", and the directory name is used.
    /// Kept separate from the refusal cases above so the distinction stays deliberate.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TestEmptyIdDefaultsRatherThanFailing(string id) =>
        Assert.True(ProjectGenerator.Create(new CreateProjectOptions
        {
            Directory = Path.Combine(root, "blank" + id.Length),
            Id = id,
        }));

    /// <summary>An id is only defaulted from the directory name when none was given, and is validated the same way.</summary>
    [Fact]
    public void TestIdDefaultsToDirectoryName()
    {
        Assert.True(ProjectGenerator.Create(new CreateProjectOptions { Directory = Path.Combine(root, "derived") }));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "derived", "project.json")));
        Assert.Equal("derived", doc.RootElement.GetProperty("id").GetString());
    }

    /// <summary>
    /// The workflow name selects which template becomes the agent prompt, so an unknown one fails
    /// rather than falling back to a default the caller did not ask for.
    /// </summary>
    [Theory]
    [InlineData("comic")]
    [InlineData("../logo")]
    [InlineData("")]
    public void TestUnknownWorkflowIsRefused(string workflow) =>
        Assert.False(ProjectGenerator.Create(Options("badflow", o => o.Workflow = workflow)));

    [Theory]
    [InlineData("hosted")]
    [InlineData("")]
    public void TestUnknownProfileIsRefused(string profile) =>
        Assert.False(ProjectGenerator.Create(Options("badprofile", o => o.Profile = profile)));

    /// <summary>Generating over existing work needs an explicit instruction to do so.</summary>
    [Fact]
    public void TestNonEmptyDirectoryIsRefusedWithoutForce()
    {
        var target = Path.Combine(root, "occupied");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "existing.txt"), "prior work");

        Assert.False(ProjectGenerator.Create(Options("occupied")));
        Assert.True(ProjectGenerator.Create(Options("occupied", o => o.Force = true)));
        Assert.True(File.Exists(Path.Combine(target, "GEMINI.md")));
    }

    /// <summary>Workflow matching is case-insensitive; a capitalised name is a typo, not a different workflow.</summary>
    [Theory]
    [InlineData("LOGO")]
    [InlineData("Logo")]
    public void TestWorkflowNameIsCaseInsensitive(string workflow) =>
        Assert.True(ProjectGenerator.Create(Options("case-" + workflow, o => o.Workflow = workflow)));
    #endregion
}
