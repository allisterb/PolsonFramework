namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using Xunit;

/// <summary>
/// Stdio protocol tests: a real MCP client launches the shipped <c>bin/cli</c> build as a child
/// process and drives several tool calls down one pipe — which is exactly what an agent host's
/// stdio MCP wiring does.
/// </summary>
/// <remarks>
/// <see cref="MCPServerProtocolTests"/> covers the same tools over HTTP, and session identity is
/// precisely where the two transports differ: an HTTP connection carries a session id, stdio has
/// none, so every call lands on <c>"default"</c>. Both <c>Stage</c> and <c>Session</c> span
/// executions by living on that session, so persistence is only proven for the transport an agent
/// actually uses by testing that transport.
/// <para>
/// This is also the only test that exercises the build in <c>bin/cli</c> — the output every
/// harness's MCP wiring points at, and the one thing a passing in-process test says nothing about.
/// </para>
/// </remarks>
public class StdioTransportTests : TestsRuntime, IDisposable
{
    #region Constants
    /// <summary>Cheap and deterministic — this suite is about the transport, not about drawing.</summary>
    private const string Draw = "const c = createCanvas(64, 64); const x = c.getContext('2d'); x.fillStyle = '#10b981'; x.fillRect(0, 0, 64, 64); c;";
    #endregion

    #region Fields
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string CliDll = Path.Combine(RepoRoot, "bin", "cli", "Polson.CLI.dll");

    private readonly string project = Path.Combine(Path.GetTempPath(), "polson-stdio-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Constructors
    public StdioTransportTests() => Directory.CreateDirectory(project);
    #endregion

    #region Methods
    public void Dispose()
    {
        if (Directory.Exists(project)) Directory.Delete(project, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Walks up from the test binary to the directory holding the solution.</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Polson.sln"))) dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    /// <summary>Starts one server process and connects to it. Disposing the client stops the process.</summary>
    private async Task<McpClient> NewClientAsync()
    {
        Assert.True(File.Exists(CliDll),
            $"The CLI is not built: {CliDll}. bin/cli comes from Polson.CLI's post-build copy, so build the solution first.");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "polson",
            Command = "dotnet",
            Arguments = [CliDll, "server", "--project-dir", project],
            WorkingDirectory = RepoRoot,
        }, NullLoggerFactory.Instance);

        return await McpClient.CreateAsync(transport);
    }

    private static ValueTask<CallToolResult> ExecuteAsync(McpClient client, string script, string? outFile = null)
    {
        var args = new Dictionary<string, object?> { ["script"] = script };
        if (outFile is not null) args["outFile"] = outFile;
        return client.CallToolAsync("ExecuteScript", args);
    }

    private static string Text(CallToolResult r) =>
        string.Concat(r.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private JsonElement[] Events()
    {
        var file = Path.Combine(project, "events", "server.jsonl");
        if (!File.Exists(file)) return [];

        return File.ReadAllLines(file)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToArray();
    }

    private static string Type(JsonElement e) => e.GetProperty("type").GetString()!;

    private static string? Stage(JsonElement e) =>
        e.TryGetProperty("stage", out var s) ? s.GetString() : null;

    private static string? Field(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.GetString() : null;
    #endregion

    #region Session Continuity Tests
    /// <summary>
    /// The load-bearing claim: separate tool calls down one stdio pipe land on one session, so a
    /// stage declared in the first is still in effect in the third.
    /// </summary>
    /// <remarks>
    /// Three calls rather than two, because the interesting behaviours only appear from the second
    /// onwards: re-declaring the stage you are in continues it, and declaring a different one
    /// supersedes it. An agent does both, in that order, in every real run.
    /// </remarks>
    [Fact]
    public async Task TestStagePersistsAcrossCallsOverStdio()
    {
        await using var client = await NewClientAsync();

        var first = await ExecuteAsync(client, "Stage.begin('Blocking'); Session['n'] = 1; log('one=' + Stage.current);");
        Assert.True(first.IsError != true, Text(first));
        Assert.Contains("one=Blocking", Text(first));

        // A separate call, a separate execution: the stage must come back from the session.
        var second = await ExecuteAsync(client, "log('two=' + Stage.current + ' n=' + Session['n']);");
        Assert.True(second.IsError != true, Text(second));
        Assert.Contains("two=Blocking n=1", Text(second));

        // Restating the stage at the top of a script continues it rather than cycling it, and the
        // spelling first recorded is the one kept.
        var third = await ExecuteAsync(client, "log('three=' + Stage.begin('blocking'));");
        Assert.True(third.IsError != true, Text(third));
        Assert.Contains("three=Blocking", Text(third));

        var events = Events();
        var stages = events.Where(e => Type(e).StartsWith("stage.")).Select(Type).ToArray();
        Assert.Equal(["stage.begin", "stage.continue"], stages);

        // Every call reached the same session — the thing that makes the above possible.
        var sessions = events.Where(e => Type(e) == "script.start").Select(e => Field(e, "session")).Distinct().ToArray();
        Assert.Single(sessions);

        // The second and third calls were tagged; the first was untagged when it started, because it
        // had not declared the stage yet.
        var tagged = events.Where(e => Type(e) == "script.start").Select(Stage).ToArray();
        Assert.Equal<IEnumerable<string?>>(["", "Blocking", "Blocking"], tagged.Select(s => s ?? ""));
    }

    /// <summary>A different stage closes the open one, so the record never shows two running at once.</summary>
    [Fact]
    public async Task TestDifferentStageSupersedesAcrossCallsOverStdio()
    {
        await using var client = await NewClientAsync();

        await ExecuteAsync(client, "Stage.begin('Blocking');");
        await ExecuteAsync(client, "Stage.begin('Refine');");

        var stages = Events()
            .Where(e => Type(e).StartsWith("stage."))
            .Select(e => (Type(e), Stage(e), Field(e, "reason")))
            .ToArray();

        Assert.Equal(
        [
            ("stage.begin", "Blocking", null),
            ("stage.end", "Blocking", "superseded"),
            ("stage.begin", "Refine", null),
        ], stages);
    }

    /// <summary>
    /// The payoff for a viewer: an artifact produced in a later call is filed under the stage
    /// declared in an earlier one, which is what makes "show me everything from Blocking" work.
    /// </summary>
    [Fact]
    public async Task TestRenderCarriesTheStageDeclaredInAnEarlierCall()
    {
        await using var client = await NewClientAsync();

        await ExecuteAsync(client, "Stage.begin('Blocking'); Stage.note('what this pass is for');");
        var drawn = await ExecuteAsync(client, Draw, outFile: "artifacts/one.webp");
        Assert.True(drawn.IsError != true, Text(drawn));

        var render = Assert.Single(Events(), e => Type(e) == "render");
        Assert.Equal("Blocking", Stage(render));
        Assert.Equal("artifacts/one.webp", Field(render, "artifact"));
        Assert.True(File.Exists(Path.Combine(project, "artifacts", "one.webp")));

        var note = Assert.Single(Events(), e => Type(e) == "note");
        Assert.Equal("Blocking", Stage(note));
    }

    /// <summary>
    /// The run opens its own record, and every script is closed by exactly one terminator.
    /// </summary>
    /// <remarks>
    /// <c>run.end</c> is deliberately not asserted here. It is written from
    /// <c>ApplicationStopping</c>, which a host that force-kills the process never reaches — and
    /// this client is one: disposing it waits its five-second shutdown timeout and then kills,
    /// leaving no closing bracket. See
    /// <see cref="TestServerClosesItsOwnBracketWhenStdinEnds"/> for the half that is ours.
    /// </remarks>
    [Fact]
    public async Task TestRunOpensItsRecordAndEveryScriptIsClosed()
    {
        await using (var client = await NewClientAsync())
        {
            await ExecuteAsync(client, Draw);
            await ExecuteAsync(client, "log('second');");
        }

        var events = Events();
        Assert.Single(events, e => Type(e) == "run.start");
        Assert.Equal(2, events.Count(e => Type(e) == "script.start"));
        Assert.Equal(2, events.Count(e => Type(e) is "script.ok" or "script.error"));
    }

    /// <summary>
    /// The server shuts down cleanly and closes its own bracket the moment its standard input ends,
    /// which is the shutdown signal the stdio transport actually has.
    /// </summary>
    /// <remarks>
    /// Driven as a bare process rather than through <see cref="McpClient"/> on purpose: the client
    /// does not close the server's stdin on dispose, so it can only ever demonstrate the kill path.
    /// This test isolates the server's own behaviour, so a regression in it is distinguishable from
    /// a host that never gave it the chance.
    /// <para>
    /// Together the two tests say where the responsibility sits: an orchestrator that wants a
    /// complete <c>server.jsonl</c> must close stdin and wait for exit rather than killing, and one
    /// that cannot must record the run's completion in its own file. A missing <c>run.end</c> is
    /// therefore not by itself evidence of a run that died.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task TestServerClosesItsOwnBracketWhenStdinEnds()
    {
        Assert.True(File.Exists(CliDll), $"The CLI is not built: {CliDll}.");

        using var server = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            ArgumentList = { CliDll, "server", "--project-dir", project },
            WorkingDirectory = RepoRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        // Enough of the handshake to open a session, so this is a live server being closed rather
        // than one that never started.
        await server.StandardInput.WriteLineAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""");
        await server.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await server.StandardInput.FlushAsync();
        await server.StandardOutput.ReadLineAsync();   // the initialize response — the session is up

        server.StandardInput.Close();                  // the only shutdown signal stdio has

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
        await server.WaitForExitAsync(cts.Token);

        Assert.Equal(0, server.ExitCode);
        Assert.Single(Events(), e => Type(e) == "run.end");
    }
    #endregion
}
