namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A generated project promises the agent has no filesystem reach outside its own run directory,
/// but <c>outFile</c> was resolved with a bare <c>Path.GetFullPath</c> against the process working
/// directory: an absolute path or a <c>..</c> traversal wrote wherever the process could, and
/// <c>--project-dir</c> was only ever written to the log.
/// <para>
/// Confinement is on whenever a project root is configured, which is every path through the CLI,
/// and off when the tools are constructed directly as a library so tests and ad-hoc use are
/// unaffected.
/// </para>
/// <para>
/// <b>A refusal is now <i>returned</i>, not thrown</b>, and these tests were changed to match — the
/// contract changed deliberately, so the change is recorded here rather than absorbed. A live run
/// found why: the MCP layer turns a thrown exception into <c>"An error occurred invoking
/// 'ExecuteScript'"</c>, so the message naming the project root — which the SDK reference promises —
/// never reached the agent, and a containment refusal was indistinguishable from any other failure.
/// Every other <c>ExecuteScript</c> failure already returned <c>Success == false</c> with something
/// actionable; this one route did not. The security property is unchanged and is still what each
/// test asserts: nothing is written outside the project.
/// </para>
/// </summary>
public class ProjectContainmentTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-contain-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public ProjectContainmentTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Contained() => new(null, null, null, root);

    private const string Script = "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); c;";
    #endregion

    #region Confined Writes
    /// <summary>A relative path resolves against the project root, not the process working directory.</summary>
    [Fact]
    public async Task TestRelativePathLandsInsideTheProject()
    {
        var result = await Contained().ExecuteScript(Script, 64, 64, outFile: "artifacts/stage1.webp");

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "stage1.webp")));
    }

    /// <summary>Missing intermediate directories are created, so the agent need not mkdir first.</summary>
    [Fact]
    public async Task TestNestedDirectoriesAreCreated()
    {
        await Contained().ExecuteScript(Script, 64, 64, outFile: "a/b/c/deep.webp");

        Assert.True(File.Exists(Path.Combine(root, "a", "b", "c", "deep.webp")));
    }

    /// <summary>outSvg is confined on the same terms as outFile.</summary>
    [Fact]
    public async Task TestOutSvgIsAlsoConfined()
    {
        var escape = Path.Combine(Path.GetTempPath(), $"polson_escape_{Guid.NewGuid():N}.svg");

        var result = await Contained().ExecuteScript(
            "const p = Snap(64, 64); p.circle(32, 32, 20); p;", 64, 64, outSvg: escape);

        Assert.False(result.Success);
        Assert.False(File.Exists(escape));
    }
    #endregion

    #region Refused Writes
    /// <summary>Traversal out of the project is refused, and nothing is written.</summary>
    [Theory]
    [InlineData("../escaped.webp")]
    [InlineData("artifacts/../../escaped.webp")]
    [InlineData("./../../escaped.webp")]
    public async Task TestTraversalOutOfProjectIsRefused(string path)
    {
        var result = await Contained().ExecuteScript(Script, 64, 64, outFile: path);

        Assert.False(result.Success);
        var escaped = Path.GetFullPath(Path.Combine(root, path));
        Assert.False(File.Exists(escaped), $"wrote outside the project: {escaped}");
    }

    /// <summary>An absolute path does not bypass the check — Path.Combine returns it outright.</summary>
    [Fact]
    public async Task TestAbsolutePathIsRefused()
    {
        var escape = Path.Combine(Path.GetTempPath(), $"polson_escape_{Guid.NewGuid():N}.webp");

        var result = await Contained().ExecuteScript(Script, 64, 64, outFile: escape);

        Assert.False(result.Success);

        Assert.False(File.Exists(escape));
    }

    /// <summary>
    /// A sibling directory sharing the root's name as a prefix is outside it. Without a trailing
    /// separator in the comparison, "polson-contain-abc" would match "polson-contain-abc-evil".
    /// </summary>
    [Fact]
    public async Task TestSiblingDirectoryWithSharedPrefixIsRefused()
    {
        var sibling = root + "-evil";

        var result = await Contained().ExecuteScript(Script, 64, 64, outFile: Path.Combine(sibling, "x.webp"));

        Assert.False(result.Success);
        Assert.False(Directory.Exists(sibling), "created a directory outside the project");
    }

    /// <summary>The refusal names the path and the root, so the agent can correct without asking.</summary>
    [Fact]
    public async Task TestRefusalMessageIsActionable()
    {
        var result = await Contained().ExecuteScript(Script, 64, 64, outFile: "../escaped.webp");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("outside this project's directory", result.Error!, StringComparison.Ordinal);
        Assert.Contains(root, result.Error!, StringComparison.Ordinal);
        Assert.Contains("artifacts/", result.Error!, StringComparison.Ordinal);
    }

    /// <summary>
    /// A script that draws nothing is refused just the same — and this is the one that was broken.
    /// </summary>
    /// <remarks>
    /// The check used to sit inside <c>if (imageBytes.Length > 0)</c>, so a script producing no image
    /// never had its path examined: <c>outFile: "../escaped.webp"</c> returned <c>success: true</c>.
    /// The evaluation instructions tell an agent to prove the boundary with "any trivial script", so
    /// the prescribed proof could neither pass nor fail, and a live run duly reported the boundary as
    /// untested and apparently open. Paths are now validated before the script runs at all, which
    /// also means a bad one costs no execution.
    /// </remarks>
    [Fact]
    public async Task TestRefusedEvenWhenTheScriptDrawsNothing()
    {
        var result = await Contained().ExecuteScript("log('drew nothing');", 64, 64, outFile: "../escaped.webp");

        Assert.False(result.Success);
        Assert.Contains("outside this project's directory", result.Error!, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(root, "../escaped.webp"))));
    }
    #endregion

    #region Unconfined Library Use
    /// <summary>
    /// With no root configured the tools stay unconstrained, which is how they are constructed
    /// directly in tests and by anything using this as a library.
    /// </summary>
    [Fact]
    public async Task TestNoProjectRootLeavesWritesUnconstrained()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"polson_free_{Guid.NewGuid():N}.webp");

        try
        {
            var result = await new DrawingMcpTools().ExecuteScript(Script, 64, 64, outFile: outside);

            Assert.True(result.Success, result.Error);
            Assert.True(File.Exists(outside));
        }
        finally
        {
            if (File.Exists(outside)) File.Delete(outside);
        }
    }

    /// <summary>
    /// A path written with <c>outFile</c> can be read back with <c>Skia.Image.load</c>.
    /// </summary>
    /// <remarks>
    /// It could not: <c>outFile</c> resolved against the project, <c>Skia.Image.load</c> against the
    /// server's working directory, so the same string meant two places and the second reported the
    /// file missing. A harness agent hit this, worked around it with <c>canvas.toBitmap()</c>, and
    /// mentioned it only in passing — the kind of defect that survives precisely because the
    /// workaround is easy.
    /// </remarks>
    [Fact]
    public async Task TestAPathWrittenWithOutFileCanBeReadBack()
    {
        var tools = Contained();

        var written = await tools.ExecuteScript(Script, outFile: "artifacts/round-trip.webp");
        Assert.True(written.Success, written.Error);

        var read = await tools.ExecuteScript(
            "const b = Skia.Image.load('artifacts/round-trip.webp'); log('loaded ' + b.width + 'x' + b.height);");

        Assert.True(read.Success, read.Error);
        Assert.Contains("loaded 64x64", string.Join('\n', read.Logs));
    }

    /// <summary>Reading is contained too, and refused with the same message shape as a write.</summary>
    /// <remarks>
    /// Reading is the milder direction, but a harness whose premise is that the agent cannot reach
    /// outside its directory does not get an exception for the direction that alarms less.
    /// </remarks>
    [Fact]
    public async Task TestReadingOutsideTheProjectIsRefused()
    {
        var outside = Path.Combine(Path.GetTempPath(), "polson-outside-" + Guid.NewGuid().ToString("N") + ".webp");
        await File.WriteAllBytesAsync(outside, [0x00]);

        try
        {
            var tools = Contained();
            var result = await tools.ExecuteScript($"Skia.Image.load({System.Text.Json.JsonSerializer.Serialize(outside)});");

            Assert.False(result.Success);
            Assert.Contains("outside this project's directory", result.Error ?? "", StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(outside)) File.Delete(outside);
        }
    }

    [Fact]
    public void TestProjectRootIsNullWhenNotConfigured() => Assert.Null(new DrawingMcpTools().ProjectRoot);

    /// <summary>A configured root is stored resolved and without a trailing separator.</summary>
    [Fact]
    public void TestProjectRootIsNormalised()
    {
        var tools = new DrawingMcpTools(null, null, null, root + Path.DirectorySeparatorChar);

        Assert.Equal(Path.TrimEndingDirectorySeparator(root), tools.ProjectRoot);
    }
    #endregion
}
