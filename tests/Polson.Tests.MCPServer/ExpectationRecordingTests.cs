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

    /// <summary>
    /// Swapped arguments are refused — they always were — and the refusal now says so.
    /// </summary>
    /// <remarks>
    /// Found in a live run: five calls written as <c>Stage.check(barW &gt; 0, 'positive baseline')</c>.
    /// Jint's overload resolution rejected them, so nothing false was recorded — but the message was
    /// the generic <i>"No public methods with the specified arguments"</i>, whose advice is to hunt
    /// for a misspelled property, <c>"rectangles from Layout … carry width and height"</c>. The agent
    /// went looking for a typo in <c>Layout</c> and lost the script. The check was caught and
    /// misdiagnosed, which is the part worth fixing.
    /// </remarks>
    [Theory]
    [InlineData("Stage.check(1 > 0, 'a positive baseline');")]
    [InlineData("Stage.check(1 < 0, 'a positive baseline');")]
    [InlineData("Stage.check( measured > 0, 'spaced out' );")]
    public async Task TestSwappedCheckArgumentsSayWhatIsWrong(string script)
    {
        var result = await Run("const measured = 1; " + script);

        Assert.False(result.Success);
        Assert.Contains("claim first", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("width and height", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calling a scale says to use <c>.map(...)</c>, rather than only that it is not a function.
    /// </summary>
    /// <remarks>
    /// From a live run: <c>scaleAlt(alt)</c>, the d3 idiom, where every charting library a script
    /// author has met makes a scale callable. The same script had already used
    /// <c>scaleAlt.isZeroBased</c> correctly two lines earlier — it knew the shape and reverted to
    /// muscle memory for the common call, and lost a 930-line script to "scaleAlt is not a function".
    /// </remarks>
    [Fact]
    public async Task TestCallingAScaleNamesTheRightMember()
    {
        var result = await Run("""
            const scaleAlt = Scale.linear(0, 50000, 400, 100);
            const y = scaleAlt(25000);
            """);

        Assert.False(result.Success);
        Assert.Contains("scaleAlt.map(value)", result.Error, StringComparison.Ordinal);
        Assert.Contains("unlike d3", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestCallingARectangleNamesItsFields()
    {
        var result = await Run("""
            const panel = Layout.rect(0, 0, 100, 100);
            const v = panel(4);
            """);

        Assert.False(result.Success);
        Assert.Contains("panel.width", result.Error, StringComparison.Ordinal);
    }

    /// <summary>An ordinary undefined function must not collect a toolkit hint it has no claim to.</summary>
    [Fact]
    public async Task TestAnUnrelatedCallIsNotGivenScaleAdvice()
    {
        var result = await Run("const helper = 42; helper();");

        Assert.False(result.Success);
        Assert.DoesNotContain("unlike d3", result.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// The generic advice still stands for the case it was written for.
    /// </summary>
    /// <remarks>
    /// The swap hint reads the failing source line, so it must not swallow the <c>undefined</c>
    /// diagnosis that the same Jint message usually means — a live run lost a 97-line composition to
    /// <c>rect.w</c>, and that reading is what recovered it.
    /// </remarks>
    [Fact]
    public async Task TestTheUndefinedArgumentAdviceSurvives()
    {
        var result = await Run("""
            const canvas = createCanvas(80, 80);
            const ctx = canvas.getContext('2d');
            const rect = Layout.rect(0, 0, 40, 40);
            ctx.fillRect(rect.x, rect.y, rect.w, rect.h);
            """);

        Assert.False(result.Success);
        Assert.Contains("width and height", result.Error, StringComparison.OrdinalIgnoreCase);
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
