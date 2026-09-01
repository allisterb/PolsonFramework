namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The record has to be able to say whether the result was what the agent wanted.
/// </summary>
/// <remarks>
/// Every other event is an <i>action</i> — a script ran, an artifact was written, a stage began — and
/// actions cannot separate a run that measured and was satisfied from one that measured, found the
/// value wrong and redrew four times. Both leave a stage with several renders in it.
/// <para>
/// Two halves close that, and they differ in kind. <c>observe</c> is machine-recorded: the measurement
/// calls now report what they <i>found</i>, because the probe was firing on the way into the call and
/// throwing the result away. <c>expect</c> and <c>check</c> are the agent's own, because nothing else
/// can know what it was aiming at.
/// </para>
/// </remarks>
public class ExpectationRecordingTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-expect-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public ExpectationRecordingTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests — the agent's half
    [Fact]
    public async Task TestAnExpectationIsRecordedBeforeTheRender()
    {
        await Run("Stage.begin('Critique'); Stage.expect('the accent should stay under 15%');");

        var e = Single("expect");
        Assert.Equal("Critique", e.GetProperty("stage").GetString());
        Assert.Equal("the accent should stay under 15%", e.GetProperty("claim").GetString());
    }

    /// <summary>A failed check is the point of the mechanism, not an error.</summary>
    [Fact]
    public async Task TestAFailedCheckIsRecordedWithItsMeasurement()
    {
        var result = await Run("Stage.check('accent under 15%', false, 'measured 22.5%');");

        Assert.True(result.Success, result.Error);

        var e = Single("check");
        Assert.False(e.GetProperty("passed").GetBoolean());
        Assert.Equal("measured 22.5%", e.GetProperty("detail").GetString());
    }

    /// <summary>The verdict comes back, so the call reads as the test it is.</summary>
    [Fact]
    public async Task TestCheckReturnsTheVerdict()
    {
        var result = await Run("log('returned ' + Stage.check('a claim', false) + '/' + Stage.check('another', true));");

        Assert.True(result.Success, result.Error);
        Assert.Contains("returned false/true", result.Logs[0]);
    }

    /// <summary>Detail is optional; its absence must not put an empty field in the record.</summary>
    [Fact]
    public async Task TestACheckWithoutDetailOmitsTheField()
    {
        await Run("Stage.check('a claim', true);");

        Assert.False(Single("check").TryGetProperty("detail", out _));
    }
    #endregion

    #region Tests — the machine's half
    [Fact]
    public async Task TestAComparisonRecordsWhatItFound()
    {
        await Run("""
            function scene(shift) {
                const c = createCanvas(200, 120);
                const x = c.getContext('2d');
                x.fillStyle = '#faf8f4'; x.fillRect(0, 0, 200, 120);
                x.fillStyle = '#1f6f8b'; x.fillRect(20 + shift, 30, 90, 60);
                return c;
            }
            scene(0).toBitmap().diff(scene(8).toBitmap());
            """);

        var e = Observations("compare").Single();
        Assert.False(e.GetProperty("identical").GetBoolean());
        Assert.Equal(960, e.GetProperty("differingPixels").GetInt32());
        Assert.Contains("similar", e.GetProperty("found").GetString());
        // Where it changed is the part a score cannot give.
        Assert.Equal(20, e.GetProperty("bounds").GetProperty("x").GetInt32());
    }

    [Fact]
    public async Task TestAPaletteRecordsItsDominantColour()
    {
        await Run("""
            const c = createCanvas(100, 100);
            const x = c.getContext('2d');
            x.fillStyle = '#faf8f4'; x.fillRect(0, 0, 100, 100);
            x.fillStyle = '#1f6f8b'; x.fillRect(0, 0, 100, 25);
            c.toBitmap().palette(4);
            """);

        var e = Observations("sample").Single(o => o.GetProperty("call").GetString() == "bitmap.palette");
        Assert.Equal("#FAF8F4", e.GetProperty("top").GetString());
        Assert.Equal(0.75, e.GetProperty("topShare").GetDouble(), 2);
    }

    /// <summary>
    /// The case a script cannot see: a profile that matched nothing returns an empty array, so the
    /// loop over it never runs and a colour that was never drawn passes silently.
    /// </summary>
    [Fact]
    public async Task TestAProfileThatMatchedNothingSaysSo()
    {
        await Run("""
            const c = createCanvas(60, 60);
            c.getContext('2d').fillRect(0, 0, 60, 60);
            c.toBitmap().rowProfile('#ff00ff');
            """);

        var e = Observations("sample").Single(o => o.GetProperty("call").GetString() == "bitmap.rowProfile");
        Assert.Equal(0, e.GetProperty("matched").GetInt32());
        Assert.Contains("no row matched", e.GetProperty("found").GetString());
    }

    /// <summary>A script that only drew records no observations, and that silence is meaningful.</summary>
    [Fact]
    public async Task TestDrawingWithoutLookingRecordsNoObservations()
    {
        await Run("const c = createCanvas(40, 40); c.getContext('2d').fillRect(0, 0, 40, 40); c;");

        Assert.Empty(Events("observe"));
    }

    /// <summary>
    /// A loop cannot turn one execution into a megabyte of record, and the cap admits itself.
    /// </summary>
    [Fact]
    public async Task TestOutcomesAreCappedAndTheCapIsReported()
    {
        await Run("""
            const c = createCanvas(40, 40);
            c.getContext('2d').fillRect(0, 0, 40, 40);
            const b = c.toBitmap();
            for (let i = 0; i < 50; i++) b.palette(2);
            """);

        var observations = Events("observe").Length;
        Assert.Equal(32, observations);

        var dropped = Single("inspect").GetProperty("outcomesDropped").GetInt32();
        Assert.Equal(18, dropped);
        Assert.Equal(50, observations + dropped);
    }
    #endregion

    #region Methods (private)
    private Task<DrawingExecutionResult> Run(string script) =>
        new DrawingMcpTools(null, null, null, root).ExecuteScript(script, 60, 60);

    private JsonElement[] Events(string type) =>
        File.ReadAllLines(Path.Combine(root, "events", "server.jsonl"))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Where(e => e.GetProperty("type").GetString() == type)
            .ToArray();

    private JsonElement Single(string type) => Assert.Single(Events(type));

    private JsonElement[] Observations(string kind) =>
        [.. Events("observe").Where(e => e.GetProperty("kind").GetString() == kind)];
    #endregion
}
