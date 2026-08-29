namespace Polson.Tests.CLI;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

/// <summary>
/// How the shipped <c>bin/cli</c> build behaves when it is invoked, as opposed to what the generator
/// writes once it is running.
/// </summary>
/// <remarks>
/// Driven as a real process because the behaviour under test is argument dispatch in <c>Main</c>, and
/// the failure it guards against — a bare command that never returns — cannot be observed by calling
/// a method.
/// </remarks>
public class CommandLineTests : TestsRuntime
{
    #region Fields
    static readonly string RepoRoot = FindRepoRoot();
    static readonly string CliDll = Path.Combine(RepoRoot, "bin", "cli", "Polson.CLI.dll");
    #endregion

    #region Methods
    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Polson.sln"))) dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    /// <summary>Runs the CLI and returns its output, killing it if it does not finish in time.</summary>
    static async Task<(int ExitCode, string Output)> RunAsync(TimeSpan timeout, params string[] args)
    {
        Assert.True(File.Exists(CliDll), $"The CLI is not built: {CliDll}.");

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        start.ArgumentList.Add(CliDll);
        foreach (var arg in args) start.ArgumentList.Add(arg);

        using var process = Process.Start(start)!;

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"'{string.Join(' ', args)}' did not exit within {timeout.TotalSeconds:0}s.");
        }

        return (process.ExitCode, await stdout + await stderr);
    }

    /// <summary>
    /// A bare invocation prints the verbs and exits, rather than starting the server.
    /// </summary>
    /// <remarks>
    /// <c>server</c> is the default verb because that is how an MCP host launches this, so no
    /// arguments used to mean "start the stdio server" — which waits on standard input forever and,
    /// because stdio mode keeps standard output clear for JSON-RPC framing, prints nothing at all
    /// while doing it. To anyone typing the bare command that is a hang with no diagnostic.
    /// </remarks>
    [Fact]
    public async Task TestNoArgumentsPrintsHelpRatherThanStartingTheServer()
    {
        var (exitCode, output) = await RunAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, exitCode);
        Assert.Contains("create-project", output, StringComparison.Ordinal);
        Assert.Contains("eval", output, StringComparison.Ordinal);
    }

    /// <summary>Asking for help is not a failure, so it keeps exit 0.</summary>
    [Fact]
    public async Task TestAskingForHelpSucceeds()
    {
        var (exitCode, _) = await RunAsync(TimeSpan.FromSeconds(30), "--help");

        Assert.Equal(0, exitCode);
    }

    /// <summary>
    /// A command line that could not be parsed fails the process.
    /// </summary>
    /// <remarks>
    /// The parser already wrote what was wrong; the exit code was what it did not set. Without one, a
    /// mistyped flag and a successful run look identical to a script, and
    /// <c>create-project … || exit 1</c> never fires.
    /// </remarks>
    [Theory]
    [InlineData("create-project")]                                  // required values missing
    [InlineData("create-project", "dir", "id", "agy", "--nope")]     // unknown option
    public async Task TestAnUnparseableCommandLineFails(params string[] args)
    {
        var (exitCode, _) = await RunAsync(TimeSpan.FromSeconds(30), args);

        Assert.NotEqual(0, exitCode);
    }
    #endregion
}
