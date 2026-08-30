namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// What a script <em>looked at</em>, which the record used to leave out entirely.
/// </summary>
/// <remarks>
/// Every event the server wrote was a write: scripts executed, artifacts rendered, stages declared.
/// A run therefore recorded what an agent did and never what it perceived — half of the
/// perception–action loop the studio is built on, and the half that says whether an action was
/// informed or blind. An agent that measures a heading before placing it, samples a pixel to check a
/// colour landed, or loads the render from a previous stage is doing work that a reader could not
/// see at all.
/// <para>
/// Two events close it. <c>artifact.read</c> names a file an earlier pass wrote — the only direct
/// evidence in the record that one pass coordinated with another through the environment rather than
/// through its own context. <c>inspect</c> is a tally, because a <c>getPixel</c> loop runs thousands
/// of times and the useful reading is how much looking happened.
/// </para>
/// <para>
/// The tests that matter most here are the negative ones: a script that only draws must produce
/// silence, and internal machinery must never be counted as inspection. A tally inflated by
/// <c>attr()</c> calling <c>getBBox</c> internally would look like a careful agent and mean nothing.
/// </para>
/// </remarks>
public class ProbeRecordingTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-probe-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public ProbeRecordingTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private DrawingMcpTools Tools() => new(null, null, null, root);

    private const string Draw =
        "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); c;";

    private JsonElement[] Events()
    {
        var file = Path.Combine(root, "events", "server.jsonl");
        if (!File.Exists(file)) return [];

        return File.ReadAllLines(file)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToArray();
    }

    private static string Type(JsonElement e) => e.GetProperty("type").GetString()!;

    private JsonElement[] OfType(string type) => [.. Events().Where(e => Type(e) == type)];

    private JsonElement Inspect() => Assert.Single(OfType("inspect"));

    private static int Probe(JsonElement inspect, string kind) =>
        inspect.GetProperty("probes").TryGetProperty(kind, out var n) ? n.GetInt32() : 0;
    #endregion

    #region Silence Tests
    /// <summary>
    /// A script that only draws records nothing. Silence has to mean "never looked".
    /// </summary>
    [Fact]
    public async Task TestDrawingWithoutLookingRecordsNoInspectEvent()
    {
        await Tools().ExecuteScript(Draw, 64, 64, outFile: "artifacts/a.webp");

        Assert.Empty(OfType("inspect"));
        Assert.Empty(OfType("artifact.read"));
        Assert.Single(OfType("render"));
    }

    /// <summary>
    /// <c>attr()</c> and <c>transform()</c> call <c>getBBox</c> internally on every attribute set, and a
    /// group computes its box by recursing over its children. None of that is the agent inspecting
    /// anything, and counting it would make an ordinary drawing script look like a careful one.
    /// </summary>
    [Fact]
    public async Task TestInternalGeometryIsNotCountedAsInspection()
    {
        await Tools().ExecuteScript(
            "const p = Snap(120, 120); " +
            "const g = p.g(); " +
            "for (let i = 0; i < 8; i++) { " +
            "  g.circle(20 + i * 10, 60, 8).attr({ fill: '#333', stroke: '#000' }).transform('t2,2'); } " +
            "p;", 120, 120, outFile: "artifacts/g.webp");

        Assert.Empty(OfType("inspect"));
    }
    #endregion

    #region Probe Tests
    /// <summary>Measuring before placing is the commonest probe, and the one a layout depends on.</summary>
    [Fact]
    public async Task TestMeasuringTextIsRecorded()
    {
        await Tools().ExecuteScript(
            "const c = createCanvas(200, 80); const x = c.getContext('2d'); " +
            "x.font = '20px sans-serif'; " +
            "const m = x.measureText('Polson'); " +
            "x.fillText('Polson', 10, 40); c;", 200, 80, outFile: "artifacts/t.webp");

        Assert.Equal(1, Probe(Inspect(), "measure"));
    }

    /// <summary>A vector layout measures too, and <c>getBBox</c> on the agent's own call must count.</summary>
    [Fact]
    public async Task TestVectorMeasurementIsRecorded()
    {
        await Tools().ExecuteScript(
            "const p = Snap(200, 80); " +
            "const t = p.text(10, 40, 'Polson'); " +
            "const box = t.getBBox(); " +
            "p.line(10, box.y2 + 4, 10 + box.width, box.y2 + 4); p;", 200, 80, outFile: "artifacts/v.webp");

        Assert.True(Probe(Inspect(), "measure") >= 1);
    }

    /// <summary>Reading pixels back is how an agent checks a colour landed rather than assuming it did.</summary>
    [Fact]
    public async Task TestSamplingPixelsIsCountedNotEvented()
    {
        await Tools().ExecuteScript(
            "const c = createCanvas(64, 64); const x = c.getContext('2d'); " +
            "x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); " +
            "const b = c.toBitmap(); " +
            "for (let i = 0; i < 25; i++) { b.getPixel(i, i); } c;", 64, 64, outFile: "artifacts/s.webp");

        // Twenty-five looks, one event. The tally is the reading; an event each would bury the log.
        var inspect = Inspect();
        Assert.Equal(25, Probe(inspect, "sample"));
        Assert.Equal(25, inspect.GetProperty("total").GetInt32());
    }

    /// <summary>Asking what the machine has, rather than assuming a typeface exists.</summary>
    [Fact]
    public async Task TestCapabilityChecksAreRecordedOncePerQuestion()
    {
        await Tools().ExecuteScript(
            "const usable = ['Georgia', 'Arial', 'Nonesuch Display'].filter(f => Skia.Font.has(f)); " +
            "log(usable.join(', ')); exit('checked');");

        // has() delegates to resolve(): three questions, three counts, not six.
        Assert.Equal(3, Probe(Inspect(), "capability"));
    }

    /// <summary>Several kinds in one script are tallied separately.</summary>
    [Fact]
    public async Task TestDifferentKindsAreTalliedApart()
    {
        await Tools().ExecuteScript(
            "const c = createCanvas(64, 64); const x = c.getContext('2d'); " +
            "x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); " +
            "x.font = '12px sans-serif'; x.measureText('a'); x.measureText('b'); " +
            "c.toBitmap().getPixel(1, 1); " +
            "Skia.Font.has('Arial'); c;", 64, 64, outFile: "artifacts/m.webp");

        var inspect = Inspect();
        Assert.Equal(2, Probe(inspect, "measure"));
        Assert.Equal(1, Probe(inspect, "sample"));
        Assert.Equal(1, Probe(inspect, "capability"));
        Assert.Equal(4, inspect.GetProperty("total").GetInt32());
    }
    #endregion

    #region Stigmergy Tests
    /// <summary>
    /// The event the whole addition exists for: one pass reading what an earlier pass left behind.
    /// </summary>
    /// <remarks>
    /// The path has to be spelled exactly as the <c>render</c> event that wrote it, or the two cannot
    /// be joined and the trace shows a read of nothing in particular.
    /// </remarks>
    [Fact]
    public async Task TestReadingBackAPriorRenderJoinsToTheRenderThatWroteIt()
    {
        var tools = Tools();
        await tools.ExecuteScript(Draw, 64, 64, outFile: "artifacts/pass1.webp");
        await tools.ExecuteScript(
            "const prior = Skia.Image.load('artifacts/pass1.webp'); " +
            "const c = createCanvas(64, 64); const x = c.getContext('2d'); " +
            "x.drawImage(prior, 0, 0); c;", 64, 64, outFile: "artifacts/pass2.webp");

        var read = Assert.Single(OfType("artifact.read"));
        var written = OfType("render").First().GetProperty("artifact").GetString();

        Assert.Equal("artifacts/pass1.webp", read.GetProperty("artifact").GetString());
        Assert.Equal(written, read.GetProperty("artifact").GetString());

        // And the read belongs to the execution that did it, not to the one that wrote the file.
        Assert.NotEqual(
            OfType("render").First().GetProperty("execution").GetString(),
            read.GetProperty("execution").GetString());
    }

    /// <summary>Reading one file twice names it once: which artifact was consulted, not how often.</summary>
    [Fact]
    public async Task TestARepeatedReadIsNamedOnce()
    {
        var tools = Tools();
        await tools.ExecuteScript(Draw, 64, 64, outFile: "artifacts/pass1.webp");
        await tools.ExecuteScript(
            "Skia.Image.load('artifacts/pass1.webp'); " +
            "Skia.Image.load('artifacts/pass1.webp'); " +
            "exit('read twice');");

        Assert.Single(OfType("artifact.read"));
        Assert.Equal(2, Probe(Inspect(), "read"));
    }

    /// <summary>Comparing this render against the last is the strongest form of looking back.</summary>
    [Fact]
    public async Task TestComparingAgainstAPriorRenderRecordsBothReadAndCompare()
    {
        var tools = Tools();
        await tools.ExecuteScript(Draw, 64, 64, outFile: "artifacts/pass1.webp");
        await tools.ExecuteScript(
            "const prior = Skia.Image.load('artifacts/pass1.webp'); " +
            "const c = createCanvas(64, 64); const x = c.getContext('2d'); " +
            "x.fillStyle = '#ef4444'; x.fillRect(0, 0, 64, 64); " +
            "const d = c.toBitmap().diff(prior); " +
            "log('differs: ' + d.differingPixels); c;", 64, 64, outFile: "artifacts/pass2.webp");

        var inspect = Inspect();
        Assert.Equal(1, Probe(inspect, "compare"));
        Assert.Equal(1, Probe(inspect, "read"));
        Assert.Single(OfType("artifact.read"));
    }
    #endregion

    #region Failure Tests
    /// <summary>
    /// A script that looked and then failed still reports what it saw.
    /// </summary>
    /// <remarks>
    /// This is the case worth keeping: an agent that measured, disliked the answer, and crashed on
    /// the next line has told the record something. Recording probes only on success would make the
    /// failure look like it came out of nowhere.
    /// </remarks>
    [Fact]
    public async Task TestProbesSurviveAFailedScript()
    {
        await Tools().ExecuteScript(
            "const c = createCanvas(64, 64); const x = c.getContext('2d'); " +
            "x.font = '12px sans-serif'; x.measureText('measured before failing'); " +
            "nope.that.does.not.exist;");

        Assert.Single(OfType("script.error"));
        Assert.Equal(1, Probe(Inspect(), "measure"));
    }

    /// <summary>Each execution's tally is its own; a scope must not leak into the next script.</summary>
    [Fact]
    public async Task TestTalliesDoNotLeakBetweenExecutions()
    {
        var tools = Tools();
        await tools.ExecuteScript(
            "const c = createCanvas(64, 64); const x = c.getContext('2d'); " +
            "x.font = '12px sans-serif'; x.measureText('one'); c;", 64, 64, outFile: "artifacts/a.webp");
        await tools.ExecuteScript(Draw, 64, 64, outFile: "artifacts/b.webp");

        // The second script only drew, so there is still exactly one inspect event in the run.
        Assert.Single(OfType("inspect"));
    }
    #endregion
}
