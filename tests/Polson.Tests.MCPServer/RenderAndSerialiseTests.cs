namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Threading.Tasks;

using global::Polson.MCPServer;
using global::Polson.Tests;

using Xunit;

/// <summary>
/// Three silent failures from the lastlight2 run (2026-09-25), each of which reported success.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A helper kept in <c>Session</c> drew a canvas that rendered nothing in a later script, because
/// the canvas was filed under the script that defined the helper.</item>
/// <item><c>outFile</c> with nothing rendered wrote no file and still reported success.</item>
/// <item><c>JSON.stringify</c> on a value holding a list killed the script, past any
/// <c>try/catch</c>, with a suggestion to use <c>Layout.rows</c>.</item>
/// </list>
/// </remarks>
public class RenderAndSerialiseTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-render-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public RenderAndSerialiseTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>A canvas drawn by a helper from an earlier script is rendered by the script calling it.</summary>
    [Fact]
    public async Task TestASessionHelpersCanvasRendersInTheCallingScript()
    {
        var tools = new DrawingMcpTools(null, null, null, root);
        var define = await tools.ExecuteScript("""
            Session.swatch = colour => {
                const c = createCanvas(8, 8), x = c.getContext('2d');
                x.fillStyle = colour; x.fillRect(0, 0, 8, 8);
                return c;
            };
            """, 8, 8);
        Assert.True(define.Success, define.Error);

        var bare = await tools.ExecuteScript("Session.swatch('#ff0000');", 8, 8, outFile: "artifacts/bare.png", format: "png");
        var awaited = await tools.ExecuteScript("await Promise.resolve(); Session.swatch('#00ff00');", 8, 8,
            outFile: "artifacts/awaited.png", format: "png");

        Assert.True(bare.Success, bare.Error);
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "bare.png")));
        Assert.True(awaited.Success, awaited.Error);
        Assert.True(File.Exists(Path.Combine(root, "artifacts", "awaited.png")));
    }

    /// <summary>outFile with nothing rendered is a failure that says why, not a success with no file.</summary>
    [Fact]
    public async Task TestOutFileWithNothingRenderedFails()
    {
        var result = await new DrawingMcpTools(null, null, null, root).ExecuteScript(
            "log('measured, drew nothing');", 8, 8, outFile: "artifacts/none.png");

        Assert.False(result.Success);
        Assert.Contains("no image", result.Error);
        Assert.Contains("return", result.Error);
        Assert.False(File.Exists(Path.Combine(root, "artifacts", "none.png")));
    }

    /// <summary>A list inside a value serialises; it used to kill the script with a suggestion of Layout.rows.</summary>
    /// <remarks>The list cases in full are in <see cref="SdkListTests"/>.</remarks>
    [Fact]
    public async Task TestStringifyingAListWorks()
    {
        var result = await new DrawingMcpTools(null, null, null, root).ExecuteScript(
            "log(JSON.stringify(Snap(10, 10).gradient('l(0,0,1,0)#f00-#00f').stops()));", 8, 8);

        Assert.True(result.Success, result.Error);
        Assert.Contains("\"stop-color\":\"#FF0000\"", string.Join(" ", result.Logs));
    }

    /// <summary>Character.info serialises, warnings and all.</summary>
    [Fact]
    public async Task TestCharacterInfoSerialises()
    {
        Directory.CreateDirectory(Path.Combine(root, "characters", "kit"));
        File.WriteAllText(Path.Combine(root, "characters", "kit", "character.json"),
            """{ "warnings": ["profile dropped", "chest 8% off"], "joints": { "head": "bone_5" } }""");

        var result = await new DrawingMcpTools(null, null, null, root).ExecuteScript("""
            const info = Character.info('kit');
            log(JSON.stringify(info.warnings) + ' ' + Array.isArray(info.warnings) + ' ' + JSON.stringify(info).length);
            """, 8, 8);

        Assert.True(result.Success, result.Error);
        Assert.Contains("[\"profile dropped\",\"chest 8% off\"] true", string.Join(" ", result.Logs));
    }
    #endregion
}
