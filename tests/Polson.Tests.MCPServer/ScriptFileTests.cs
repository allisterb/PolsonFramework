namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Running a script from a file instead of re-sending it.
/// </summary>
/// <remarks>
/// The cost this exists to remove was measured on a four-agent <c>comic_studio</c> run: 850 KB of
/// JavaScript over 55 <c>ExecuteScript</c> calls, the twenty largest taking a mean of three minutes
/// each to emit — against a median engine time of <b>45 ms</b>. Consecutive large scripts shared 71%
/// of their lines, so most of that was retyping a program to change part of it. Nothing was wrong
/// with the agents' behaviour: the workflow tells them to keep one consolidated <c>artwork.js</c>,
/// and re-sending it was the only way to run it.
/// <para>
/// The tests that matter most are the refusals. Passing both sources, or neither, has to fail loudly
/// — a server that guessed would run code the caller did not intend, and the symptom would be an
/// edit that appears not to have taken effect.
/// </para>
/// </remarks>
public class ScriptFileTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-scriptfile-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public ScriptFileTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    private const string Draw =
        "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); c;";

    private string WriteFile(string name, string body)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
        return name;
    }

    private JsonElement[] Events()
    {
        var file = Path.Combine(root, "events", "server.jsonl");
        if (!File.Exists(file)) return [];

        return File.ReadAllLines(file)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToArray();
    }

    private JsonElement[] OfType(string type) =>
        [.. Events().Where(e => e.GetProperty("type").GetString() == type)];
    #endregion

    #region Running from a file
    [Fact]
    public async Task TestAScriptFileRunsAndRenders()
    {
        WriteFile("artwork.js", Draw);

        var result = await Tools().ExecuteScript(scriptFile: "artwork.js", outFile: "artifacts/a.webp");

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "a.webp")));
    }

    /// <summary>
    /// The record keeps a copy of what ran, not a pointer to a file that keeps changing.
    /// </summary>
    /// <remarks>
    /// This matters more with a file source than without one. An inline script exists only in the
    /// call, so saving it is the only way to keep it; a file is still there afterwards, which makes
    /// it tempting to record the path alone — and then a record that says "this execution ran
    /// artwork.js" describes whatever the file says *later*, not what was run.
    /// </remarks>
    [Fact]
    public async Task TestTheExecutedSourceIsCopiedIntoTheRecord()
    {
        WriteFile("artwork.js", Draw);
        await Tools().ExecuteScript(scriptFile: "artwork.js");

        var saved = Directory.GetFiles(Path.Combine(root, "scripts"), "*.js");
        Assert.Single(saved);
        Assert.Equal(Draw, File.ReadAllText(saved[0]));

        // Editing the file afterwards must not change what the record says this execution ran.
        WriteFile("artwork.js", "const c = createCanvas(8, 8); c;");
        Assert.Equal(Draw, File.ReadAllText(saved[0]));
    }

    /// <summary>A file source is named in the record; an inline one has nothing to name.</summary>
    [Fact]
    public async Task TestTheRecordNamesWhereTheSourceCameFrom()
    {
        WriteFile("artwork.js", Draw);
        await Tools().ExecuteScript(scriptFile: "artwork.js");

        var start = Assert.Single(OfType("script.start"));
        Assert.Equal("artwork.js", start.GetProperty("source").GetString());
    }

    [Fact]
    public async Task TestAnInlineScriptRecordsNoSource()
    {
        await Tools().ExecuteScript(Draw);

        var start = Assert.Single(OfType("script.start"));
        Assert.False(start.TryGetProperty("source", out _));
    }

    /// <summary>The point of the parameter: run the same path again and get the new contents.</summary>
    [Fact]
    public async Task TestRunningTheSamePathAgainRunsTheEditedFile()
    {
        var tools = Tools();

        WriteFile("artwork.js", Draw);
        await tools.ExecuteScript(scriptFile: "artwork.js", outFile: "artifacts/first.webp");

        WriteFile("artwork.js",
            "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#ef4444'; x.fillRect(0, 0, 64, 64); c;");
        await tools.ExecuteScript(scriptFile: "artwork.js", outFile: "artifacts/second.webp");

        var saved = Directory.GetFiles(Path.Combine(root, "scripts"), "*.js").OrderBy(p => p).ToArray();
        Assert.Equal(2, saved.Length);
        Assert.Contains("#10b981", File.ReadAllText(saved[0]));
        Assert.Contains("#ef4444", File.ReadAllText(saved[1]));
    }
    #endregion

    #region Refusals
    /// <summary>
    /// Both sources is refused rather than resolved.
    /// </summary>
    /// <remarks>
    /// Either could plausibly be the intended override, so choosing one would run code the caller did
    /// not mean to run — and it would present as the edit not having taken effect, which is among the
    /// most expensive failures to diagnose.
    /// </remarks>
    [Fact]
    public async Task TestGivingBothSourcesIsRefused()
    {
        WriteFile("artwork.js", Draw);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Tools().ExecuteScript(Draw, scriptFile: "artwork.js"));

        Assert.Contains("not both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestGivingNeitherSourceIsRefused()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Tools().ExecuteScript());

        Assert.Contains("scriptFile", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A missing file names the path the caller wrote, not the one it resolved to.</summary>
    [Fact]
    public async Task TestAMissingScriptFileSaysSoInTheCallersTerms()
    {
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => Tools().ExecuteScript(scriptFile: "artwork.js"));

        Assert.Contains("artwork.js", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An empty file is an error, not an empty drawing.
    /// </summary>
    /// <remarks>
    /// It would otherwise execute cleanly, return nothing and render nothing — a success that looks
    /// like a drawing failure. Far likelier to be a write that has not landed than a deliberate no-op.
    /// </remarks>
    [Fact]
    public async Task TestAnEmptyScriptFileIsRefused()
    {
        WriteFile("artwork.js", "   \n");

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Tools().ExecuteScript(scriptFile: "artwork.js"));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The same containment `outFile` gets: a path out of the project is refused.</summary>
    [Theory]
    [InlineData("../escape.js")]
    [InlineData("../../escape.js")]
    [InlineData("artifacts/../../escape.js")]
    public async Task TestAPathOutsideTheProjectIsRefused(string attempt)
    {
        var outside = Path.Combine(Path.GetDirectoryName(root)!, "escape.js");
        File.WriteAllText(outside, Draw);

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => Tools().ExecuteScript(scriptFile: attempt));
        }
        finally
        {
            File.Delete(outside);
        }
    }
    #endregion
}
