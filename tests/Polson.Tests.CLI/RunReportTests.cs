namespace Polson.Tests.CLI;

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Polson.CLI;
using Xunit;

/// <summary>
/// <c>polson report</c> — comparing what a run directory claims against what the engine recorded.
/// </summary>
/// <remarks>
/// A run was reviewed where the directory looked complete — seven plausible scripts, four finished
/// SVGs, a written audit concluding "all constraints satisfied" — and the event log contained a
/// single <c>run.start</c>. The agent had written every file itself and never executed anything.
/// Nothing about the directory revealed that, because the only half an agent cannot fabricate is
/// the log the server appends to as it works.
/// <para>
/// These tests pin the comparison rather than any judgement: the report states counts and what does
/// not reconcile, and leaves the reading to the director.
/// </para>
/// </remarks>
[Collection(ConsoleCollection.Name)]
public class RunReportTests : TestsRuntime, IDisposable
{
    #region Constructors
    public RunReportTests()
    {
        root = Path.Combine(Path.GetTempPath(), "polson-report-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "artifacts"));
        Directory.CreateDirectory(Path.Combine(root, "events"));
    }
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private void Script(string name, string body = "// x") =>
        File.WriteAllText(Path.Combine(root, "scripts", name), body);

    private void Artifact(string name) =>
        File.WriteAllText(Path.Combine(root, "artifacts", name), "not really an image");

    private void Events(params string[] lines) =>
        File.WriteAllLines(Path.Combine(root, "events", "server.jsonl"), lines);

    /// <summary>Runs the report and returns its JSON, which is the same data the table prints.</summary>
    private JsonObject Report()
    {
        var writer = new StringWriter();
        var previous = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(0, RunReport.Run(new ReportOptions { ProjectDir = root, Json = true }));
        }
        finally
        {
            Console.SetOut(previous);
        }
        return (JsonObject)JsonNode.Parse(writer.ToString())!;
    }

    private static string[] Warnings(JsonObject report) =>
        [.. (report["warnings"] as JsonArray ?? []).Select(w => w?.ToString() ?? "")];
    #endregion

    #region A run that really happened
    /// <summary>A run whose files all trace to events reconciles, and says so.</summary>
    [Fact]
    public void TestAConsistentRunHasNothingUnaccountedFor()
    {
        Script("0001.js");
        Script("0002.js");
        Artifact("stage1.webp");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"stage.begin","stage":"Blocking"}""",
            """{"type":"note","message":"why"}""",
            """{"type":"script.ok","script":"scripts/0002.js"}""",
            """{"type":"render","script":"scripts/0002.js","artifact":"artifacts/stage1.webp"}""");

        var report = Report();

        Assert.Empty(Warnings(report));
        Assert.Equal(2, report["scriptsExecuted"]!.GetValue<int>());
        Assert.Equal(1, report["renders"]!.GetValue<int>());
        Assert.Equal("Blocking", (report["stages"] as JsonArray)![0]!.ToString());
    }
    #endregion

    #region A run that did not
    /// <summary>
    /// The case this exists for: a full-looking directory and an empty log.
    /// </summary>
    /// <remarks>
    /// All four readings fire together, which is what the pattern looks like — the point is not any
    /// one of them but that they agree.
    /// </remarks>
    [Fact]
    public void TestAHandWrittenRunIsFullyUnaccountedFor()
    {
        Script("01_data.js");
        Script("02_forms.js");
        Artifact("final.svg");
        Events("""{"type":"run.start"}""");

        var report = Report();
        var warnings = Warnings(report);

        Assert.Equal(0, report["scriptsExecuted"]!.GetValue<int>());
        Assert.Equal(0, report["renders"]!.GetValue<int>());
        Assert.Contains(warnings, w => w.Contains("No scripts were executed", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("No renders were recorded", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("written by hand", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("have no render event", StringComparison.Ordinal));
    }

    /// <summary>An artifact with no render event is named, even when the run is otherwise real.</summary>
    /// <remarks>
    /// The partial case is the one worth catching: a run that genuinely drew some things and
    /// hand-placed others reconciles for everything except the file that matters.
    /// </remarks>
    [Fact]
    public void TestAnArtifactWithNoRenderIsReportedInAnOtherwiseRealRun()
    {
        Script("0001.js");
        Artifact("drawn.webp");
        Artifact("smuggled.svg");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"render","script":"scripts/0001.js","artifact":"artifacts/drawn.webp"}""");

        var report = Report();

        Assert.Contains("smuggled.svg",
            (report["artifactsNoRenderProduced"] as JsonArray)!.Select(a => a?.ToString()));
        Assert.DoesNotContain("drawn.webp",
            (report["artifactsNoRenderProduced"] as JsonArray)!.Select(a => a?.ToString()));
    }

    /// <summary>No event log at all is the strongest signal, and stops further guessing.</summary>
    [Fact]
    public void TestAMissingEventLogIsReportedOnItsOwn()
    {
        Script("0001.js");
        Artifact("final.svg");

        var report = Report();
        var warnings = Warnings(report);

        Assert.False(report["hasEventLog"]!.GetValue<bool>());
        Assert.Single(warnings);
        Assert.Contains("never started", warnings[0], StringComparison.Ordinal);
    }
    #endregion

    #region Robustness
    /// <summary>
    /// A truncated final line does not stop the report.
    /// </summary>
    /// <remarks>
    /// A killed run leaves a half-written line, and that is exactly the run someone most wants a
    /// report on. Refusing to parse would withhold the report when it is most needed.
    /// </remarks>
    [Fact]
    public void TestATruncatedLogStillReports()
    {
        Script("0001.js");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"render","script":"scripts""");

        var report = Report();

        Assert.Equal(2, report["events"]!.GetValue<int>());
        Assert.Equal(1, report["scriptsExecuted"]!.GetValue<int>());
    }

    /// <summary>A script run twice counts once, since it is one file.</summary>
    [Fact]
    public void TestRepeatedExecutionsOfOneScriptCountOnce()
    {
        Script("0001.js");
        Events(
            """{"type":"run.start"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""",
            """{"type":"script.ok","script":"scripts/0001.js"}""");

        Assert.Equal(1, Report()["scriptsExecuted"]!.GetValue<int>());
    }

    /// <summary>A missing directory is refused rather than reported as an empty run.</summary>
    [Fact]
    public void TestAMissingDirectoryIsAnError()
    {
        var writer = new StringWriter();
        var previous = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Equal(1, RunReport.Run(new ReportOptions
            {
                ProjectDir = Path.Combine(root, "does-not-exist")
            }));
        }
        finally
        {
            Console.SetOut(previous);
        }
    }
    #endregion

    #region Fields
    private readonly string root;
    #endregion
}
