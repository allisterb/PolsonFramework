namespace Polson.Tests.CLI;

using Xunit;

/// <summary>
/// Test classes that redirect <see cref="System.Console"/>, run one at a time.
/// </summary>
/// <remarks>
/// xUnit runs test classes in parallel, and <c>Console.Out</c> is process-global. Two classes that
/// each call <c>Console.SetOut</c> to capture output will clobber one another: one test's redirect
/// is replaced mid-run, and the other reads an empty string and fails to parse it as JSON. The
/// failure looks nothing like its cause — it surfaces as a malformed report from a test that has
/// nothing to do with the class that stole the stream, and it only appears when the two happen to
/// overlap.
/// <para>
/// Anything here captures console output and must join this collection.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ConsoleCollection
{
    public const string Name = "console-capture";
}
