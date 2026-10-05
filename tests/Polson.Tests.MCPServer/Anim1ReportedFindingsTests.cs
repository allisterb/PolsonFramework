namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Findings from the <c>anim1</c> agent run (the puddle jump, 2026-10-05), the first run of the <c>animation</c>
/// workflow.
/// </summary>
public class Anim1ReportedFindingsTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-anim1-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public Anim1ReportedFindingsTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const c = createCanvas(40, 40); {body} c;", 40, 40, null, "png", 90);
    #endregion

    #region Judged checks
    /// <summary>
    /// A verdict read off a picture is marked as one. The run recorded eleven per-beat critiques as checks, and they
    /// read exactly like its measured arcs and contacts.
    /// </summary>
    [Fact]
    public void AJudgedCheckIsMarkedInTheLogAndTheSummary()
    {
        var result = Execute("""
            Stage.check('the tip reads as a fall', true, 'flipped and blurred, the bow survives', { judged: true });
            Stage.check('the catch reads as a stop', false, 'arms still level', { judged: true });
            Stage.check('feet planted', true, 'largest miss 0.69px');
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l == "[CHECK] PASS (judged) the tip reads as a fall - flipped and blurred, the bow survives");
        Assert.Contains(result.Logs, l => l == "[CHECK] FAIL (judged) the catch reads as a stop - arms still level");
        Assert.Contains(result.Logs, l => l == "[CHECK] PASS feet planted - largest miss 0.69px");

        Assert.Equal(2, result.Checks!.Passed);
        Assert.Equal(1, result.Checks.Failed);
        Assert.Equal(2, result.Checks.JudgedCount);
    }

    [Fact]
    public void AJudgedCheckIsMarkedInTheRecord()
    {
        _ = new DrawingMcpTools(null, null, null, root)
            .ExecuteScript("Stage.check('reads', true, 'looked', { judged: true }); Stage.check('measured', true, '0px'); 'x';", 40, 40)
            .GetAwaiter().GetResult();

        var checks = File.ReadAllLines(Path.Combine(root, "events", "server.jsonl"))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Where(e => e.GetProperty("type").GetString() == "check")
            .ToArray();

        Assert.True(checks[0].GetProperty("judged").GetBoolean());
        Assert.False(checks[1].TryGetProperty("judged", out _));
    }

    [Theory]
    [InlineData("{ judged: 'yes' }", "true or false")]
    [InlineData("{ looked: true }", "judged")]
    public void ABadJudgedOptionIsRefused(string options, string expected)
    {
        var result = Execute($"Stage.check('reads', true, 'looked', {options});");

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void JudgedAndAcceptedCompose()
    {
        var result = Execute("Stage.check('reads as sitting', false, 'looked', { judged: true, accepted: 'the beat is meant to be ambiguous' });");

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Logs, l => l.StartsWith("[CHECK] ACCEPTED (judged) reads as sitting", StringComparison.Ordinal));
        Assert.Equal(1, result.Checks!.AcceptedCount);
        Assert.Equal(1, result.Checks.JudgedCount);
    }
    #endregion
}
