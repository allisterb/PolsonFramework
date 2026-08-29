namespace Polson.Tests.CLI;

using System;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Builds an option set that creates <paramref name="name"/> inside this test's temp root.
    /// </summary>
    /// <remarks>
    /// The directory argument is the <em>parent</em>: one directory holds many projects, and the id
    /// names the one being made.
    /// </remarks>
    CreateProjectOptions Options(string name, Action<CreateProjectOptions>? configure = null)
    {
        var opts = new CreateProjectOptions { Directory = root, Id = name, Sdk = "agy" };
        configure?.Invoke(opts);
        return opts;
    }

    /// <summary>Every file in a generated project, relative and forward-slashed.</summary>
    string[] FileSet(string name) =>
        Directory.EnumerateFiles(Path.Combine(root, name), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Path.Combine(root, name), f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
    #endregion

    #region Layout Tests
    [Theory]
    [InlineData("project.json")]
    [InlineData("brief.md")]
    [InlineData(".gitignore")]
    public void TestExpectedFileIsWritten(string file)
    {
        Assert.True(ProjectGenerator.Create(Options("layout")));
        Assert.True(File.Exists(Path.Combine(root, "layout", file)), $"missing: {file}");
    }

    /// <summary>
    /// The SDK decides what the host's three files are called, and that naming is the entire
    /// difference between an Antigravity project and a Claude Code one.
    /// </summary>
    [Theory]
    [InlineData("agy", "GEMINI.md")]
    [InlineData("agy", "mcp_config.json")]
    [InlineData("agy", ".agents/mcp_config.json")]
    [InlineData("agy", ".agents/settings.json")]
    [InlineData("claude", "CLAUDE.md")]
    [InlineData("claude", ".mcp.json")]
    [InlineData("claude", ".claude/settings.local.json")]
    public void TestHostFilesAreNamedForTheSdk(string sdk, string file)
    {
        var name = $"host-{sdk}-{file.Replace('/', '_').Replace('.', '_')}";
        Assert.True(ProjectGenerator.Create(Options(name, o => o.Sdk = sdk)));

        Assert.Contains(file, FileSet(name));
    }

    /// <summary>The project is created inside the directory given, named by its id.</summary>
    [Fact]
    public void TestTheProjectIsCreatedInsideTheGivenDirectory()
    {
        Assert.True(ProjectGenerator.Create(Options("nested")));

        Assert.True(File.Exists(Path.Combine(root, "nested", "project.json")));
        Assert.False(File.Exists(Path.Combine(root, "project.json")));
    }

    [Theory]
    [InlineData("artifacts")]
    [InlineData("scripts")]
    [InlineData("events")]
    public void TestExpectedDirectoryIsCreated(string dir)
    {
        Assert.True(ProjectGenerator.Create(Options("dirs")));
        Assert.True(Directory.Exists(Path.Combine(root, "dirs", dir)), $"missing: {dir}");
    }

    /// <summary>
    /// Standalone adds to the managed set and takes nothing away, so the file set itself says which
    /// kind of project this is — which a reader can check, unlike a flag inside a JSON file.
    /// </summary>
    [Fact]
    public void TestStandaloneIsASupersetOfManaged()
    {
        Assert.True(ProjectGenerator.Create(Options("sup-managed")));
        Assert.True(ProjectGenerator.Create(Options("sup-standalone", o => o.Standalone = true)));

        var managed = FileSet("sup-managed");
        var standalone = FileSet("sup-standalone");

        Assert.Empty(managed.Except(standalone));
        Assert.Equal(["agent.config.json"], standalone.Except(managed));
    }

    /// <summary>
    /// The session directories are <c>LocalAgentConfig.save_dir</c> and <c>app_data_dir</c> — a
    /// Python SDK concept, meaningless when a desktop host owns the session.
    /// </summary>
    [Theory]
    [InlineData("session/save")]
    [InlineData("session/appdata")]
    public void TestSessionDirectoriesAreStandaloneOnly(string dir)
    {
        Assert.True(ProjectGenerator.Create(Options("sess-managed")));
        Assert.True(ProjectGenerator.Create(Options("sess-standalone", o => o.Standalone = true)));

        Assert.False(Directory.Exists(Path.Combine(root, "sess-managed", dir)));
        Assert.True(Directory.Exists(Path.Combine(root, "sess-standalone", dir)));
    }

    /// <summary>
    /// The MCP wiring has to name a command that can actually start the server, whichever way the
    /// generator itself was launched.
    /// </summary>
    /// <remarks>
    /// Launched as <c>dotnet Polson.CLI.dll</c>, the process path is the shared host and the
    /// assembly is an argument; taking the process path alone wrote <c>dotnet server</c>, which
    /// starts nothing. The invariant is checkable in any host: if the command is the shared host,
    /// an assembly must lead the arguments.
    /// </remarks>
    [Fact]
    public void TestMcpWiringNamesARunnableCommand()
    {
        Assert.True(ProjectGenerator.Create(Options("mcp", o => o.Sdk = "claude")));

        var wiring = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mcp", ".mcp.json")))
            .RootElement.GetProperty("mcpServers").GetProperty("polson");

        var command = wiring.GetProperty("command").GetString()!;
        var args = wiring.GetProperty("args").EnumerateArray().Select(a => a.GetString()!).ToArray();

        Assert.NotEmpty(command);
        Assert.Contains("server", args);
        Assert.Equal(["--project-dir", "."], args[^2..]);

        if (Path.GetFileNameWithoutExtension(command).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            Assert.EndsWith(".dll", args[0], StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// An unsubstituted placeholder would reach the agent as literal template syntax, which reads
    /// as a broken instruction rather than an instruction it can follow.
    /// </summary>
    [Theory]
    [InlineData("logo", "agy", "GEMINI.md")]
    [InlineData("logo", "claude", "CLAUDE.md")]
    [InlineData("harness", "agy", "GEMINI.md")]
    [InlineData("harness", "claude", "CLAUDE.md")]
    public void TestNoPlaceholderSurvivesRendering(string workflow, string sdk, string instructions)
    {
        var name = $"tokens-{workflow}-{sdk}";
        Assert.True(ProjectGenerator.Create(Options(name, o => { o.Workflow = workflow; o.Sdk = sdk; })));

        foreach (var file in new[] { instructions, "brief.md" })
        {
            var body = File.ReadAllText(Path.Combine(root, name, file));
            Assert.DoesNotContain("{{", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The harness's isolation rule is backed differently on each host, and the instructions must say
    /// which — a harness that claims an enforcement it does not have measures nothing, because nobody
    /// can tell afterwards whether the source really went unread.
    /// </summary>
    /// <remarks>
    /// Claude Code's permission rules take paths; Antigravity's name tools and commands only, so no
    /// rule there can stop a read. See the harness permission traps in the session handoff.
    /// </remarks>
    [Fact]
    public void TestHarnessIsolationTellsTheTruthForEachHost()
    {
        Assert.True(ProjectGenerator.Create(Options("iso-agy", o => o.Workflow = "harness")));
        Assert.True(ProjectGenerator.Create(Options("iso-claude", o => { o.Workflow = "harness"; o.Sdk = "claude"; })));

        var agy = File.ReadAllText(Path.Combine(root, "iso-agy", "GEMINI.md"));
        var claude = File.ReadAllText(Path.Combine(root, "iso-claude", "CLAUDE.md"));

        Assert.Contains("not enforced by a permissions file", agy, StringComparison.Ordinal);
        Assert.DoesNotContain("settings.local.json", agy, StringComparison.Ordinal);

        Assert.Contains("settings.local.json", claude, StringComparison.Ordinal);
        Assert.Contains("does **not** deny reads outside this folder", claude, StringComparison.Ordinal);
    }

    /// <summary>
    /// What makes the harness a harness: the agent is asked to report on the API, not just to draw.
    /// The logo workflow deliberately asks for neither.
    /// </summary>
    [Fact]
    public void TestOnlyTheHarnessWorkflowAsksForFindings()
    {
        Assert.True(ProjectGenerator.Create(Options("f-harness", o => o.Workflow = "harness")));
        Assert.True(ProjectGenerator.Create(Options("f-logo")));

        Assert.Contains("findings.md", File.ReadAllText(Path.Combine(root, "f-harness", "GEMINI.md")));
        Assert.DoesNotContain("findings.md", File.ReadAllText(Path.Combine(root, "f-logo", "GEMINI.md")));
    }

    /// <summary>
    /// The manifest is how the orchestrator knows which wiring file to open, so the sdk it records
    /// has to be the one it was generated for.
    /// </summary>
    [Theory]
    [InlineData("agy", false, "managed")]
    [InlineData("agy", true, "standalone")]
    [InlineData("claude", false, "managed")]
    public void TestManifestRecordsSdkAndProfile(string sdk, bool standalone, string profile)
    {
        var name = $"manifest-{sdk}-{profile}";
        Assert.True(ProjectGenerator.Create(Options(name, o => { o.Sdk = sdk; o.Standalone = standalone; })));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name, "project.json")));
        var r = doc.RootElement;

        Assert.Equal(name, r.GetProperty("id").GetString());
        Assert.Equal("logo", r.GetProperty("workflow").GetString());
        Assert.Equal(sdk, r.GetProperty("sdk").GetString());
        Assert.Equal(profile, r.GetProperty("profile").GetString());
    }

    /// <summary>Resume belongs to the orchestrator, so a managed project carries no slot for it.</summary>
    [Fact]
    public void TestConversationIdIsStandaloneOnly()
    {
        Assert.True(ProjectGenerator.Create(Options("conv-managed")));
        Assert.True(ProjectGenerator.Create(Options("conv-standalone", o => o.Standalone = true)));

        Assert.DoesNotContain("conversationId", File.ReadAllText(Path.Combine(root, "conv-managed", "project.json")));
        Assert.Contains("conversationId", File.ReadAllText(Path.Combine(root, "conv-standalone", "project.json")));
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
    /// The orchestrator's deny list. Image generation is the important one: it bypasses asset
    /// requisition entirely, so an agent holding it can produce a finished picture and the premise
    /// of the studio stops holding. The shell is denied because a public URL must not reach one.
    /// </summary>
    [Theory]
    [InlineData("generate_image")]
    [InlineData("run_command")]
    public void TestStandaloneDeniesTheDangerousTools(string tool)
    {
        Assert.True(ProjectGenerator.Create(Options("deny", o => o.Standalone = true)));

        Assert.Contains(tool, DeniedTools("deny"));
    }

    /// <summary>
    /// A managed project carries no policy file, because its host owns tool policy and ours would be
    /// a file that reads as enforcement while enforcing nothing. The prohibition is carried in the
    /// instructions instead, as a rule the agent must follow.
    /// </summary>
    [Fact]
    public void TestManagedCarriesNoPolicyFile()
    {
        Assert.True(ProjectGenerator.Create(Options("nopolicy")));

        Assert.False(File.Exists(Path.Combine(root, "nopolicy", "agent.config.json")));
    }

    /// <summary>
    /// The host permission file allows the Polson tools by name, read from the server itself so the
    /// list cannot fall behind a tool being added.
    /// </summary>
    [Fact]
    public void TestHostPermissionsAllowEveryServerTool()
    {
        Assert.True(ProjectGenerator.Create(Options("perms", o => o.Sdk = "claude")));

        var allow = File.ReadAllText(Path.Combine(root, "perms", ".claude", "settings.local.json"));
        foreach (var tool in new[] { "Search", "ExecuteScript", "History", "RenderSvg", "MeasureSvgPath" })
        {
            Assert.Contains($"mcp__polson__{tool}", allow, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Antigravity permissions name tools, never paths: a <c>read:C:/...</c> entry does nothing at
    /// all, and an entry the host does not recognise is silently inert — indistinguishable from one
    /// being enforced. Only the two namespaces a working example proves are emitted.
    /// </summary>
    [Fact]
    public void TestAntigravityPermissionsNameToolsRatherThanPaths()
    {
        Assert.True(ProjectGenerator.Create(Options("agyperms")));

        var settings = File.ReadAllText(Path.Combine(root, "agyperms", ".agents", "settings.json"));
        Assert.Contains("mcp:polson:*", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("read:", settings, StringComparison.Ordinal);
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
    [InlineData("")]
    [InlineData("   ")]
    public void TestInvalidIdIsRefused(string id) =>
        Assert.False(ProjectGenerator.Create(Options(id, o => o.Id = id)));

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

    /// <summary>
    /// The SDK decides every host filename, so an unrecognised one cannot be defaulted — guessing
    /// would produce a project whose host silently finds no configuration at all.
    /// </summary>
    [Theory]
    [InlineData("gemini")]
    [InlineData("antigravity")]
    [InlineData("")]
    public void TestUnknownSdkIsRefused(string sdk) =>
        Assert.False(ProjectGenerator.Create(Options("badsdk", o => o.Sdk = sdk)));

    /// <summary>
    /// The orchestrator builds Antigravity SDK configurations only, so this combination is reported
    /// rather than generated half-working — and reported the way every other rejection here is,
    /// with a sentence rather than a stack trace.
    /// </summary>
    [Fact]
    public void TestClaudeStandaloneIsRefused()
    {
        Assert.False(ProjectGenerator.Create(Options("cl-stand", o => { o.Sdk = "claude"; o.Standalone = true; })));

        Assert.False(Directory.Exists(Path.Combine(root, "cl-stand")));
    }

    /// <summary>The SDK name is matched case-insensitively; capitalisation is a typo, not a choice.</summary>
    [Theory]
    [InlineData("AGY")]
    [InlineData("Claude")]
    public void TestSdkNameIsCaseInsensitive(string sdk) =>
        Assert.True(ProjectGenerator.Create(Options("sdkcase-" + sdk, o => o.Sdk = sdk)));

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
