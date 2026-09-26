namespace Polson.Tests.MCPServer;

using System;
using System.Threading;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A function kept in <c>Session</c> runs under the clock of the script that <i>calls</i> it, not of
/// the one that defined it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on a live run, where it cost fourteen minutes and four requisitions.</b> A helper defined
/// in one script as <c>Session.sheetOf = cutout =&gt; ...</c> worked in the next two, then threw
/// <c>The operation has timed out.</c> from every script after — each within a few seconds of
/// starting. The stored function still ran inside the engine that defined it, and that engine's
/// time limit had started with the script that defined it, thirty seconds earlier. The two
/// successes were the two calls made inside those thirty seconds.
/// </para>
/// <para>
/// Nothing in the message pointed at the helper. The agent read it as the network, then as
/// <c>Skia.Image.fromDataUrl</c>, and rewrote the helper around a different decode — which worked
/// only because it redefined the function in the calling script.
/// </para>
/// <para>
/// Non-parallel because it shortens <see cref="JsDrawingEngine.ScriptTimeoutSeconds"/>, which is
/// static and would reach every script in the assembly.
/// </para>
/// </remarks>
[Collection(SessionClockCollection.Name)]
public class SessionFunctionClockTests : TestsRuntime
{
    [Fact]
    public void TestAStoredFunctionStillRunsAfterItsDefiningScriptsClockHasExpired()
    {
        var saved = JsDrawingEngine.ScriptTimeoutSeconds;
        try
        {
            JsDrawingEngine.ScriptTimeoutSeconds = 1;
            var session = new SessionContext();

            var define = Run(session, "Session.twice = n => { let s = 0; for (let i = 0; i < 1000; i++) s += n; return s / 500; };");
            Assert.True(define.Success, define.Error);

            // Past the defining script's whole allowance.
            Thread.Sleep(TimeSpan.FromSeconds(1.6));

            var call = Run(session, "log(String(Session.twice(21)));");
            Assert.True(call.Success, call.Error);
            Assert.Contains("42", call.Logs[^1], StringComparison.Ordinal);

            // And again, from a third script: the fix must not be a single reprieve.
            Thread.Sleep(TimeSpan.FromSeconds(1.6));
            var again = Run(session, "log(String(Session.twice(4)));");
            Assert.True(again.Success, again.Error);
            Assert.Contains("8", again.Logs[^1], StringComparison.Ordinal);
        }
        finally
        {
            JsDrawingEngine.ScriptTimeoutSeconds = saved;
        }
    }

    /// <summary>
    /// The calling script's limit still holds: a stored function cannot be used to escape it.
    /// </summary>
    [Fact]
    public void TestAStoredFunctionThatNeverReturnsIsStillStopped()
    {
        var saved = JsDrawingEngine.ScriptTimeoutSeconds;
        try
        {
            JsDrawingEngine.ScriptTimeoutSeconds = 1;
            var session = new SessionContext();

            Assert.True(Run(session, "Session.spin = () => { while (true) {} };").Success);
            var spun = Run(session, "Session.spin();");
            Assert.False(spun.Success);
            // An empty loop reaches the statement cap before the clock; either limit is a correct stop.
            Assert.Matches("maximum number of statements|second limit", spun.Error);
        }
        finally
        {
            JsDrawingEngine.ScriptTimeoutSeconds = saved;
        }
    }

    /// <summary>
    /// What a stored function sees as <c>Session</c> is the scratchpad of the script that defined it.
    /// </summary>
    /// <remarks>
    /// Pinned because it is documented: the function's globals belong to its home engine, so a value
    /// set by a later script is not visible to it. Pass what it needs as arguments.
    /// </remarks>
    [Fact]
    public void TestAStoredFunctionSeesTheSessionOfTheScriptThatDefinedIt()
    {
        var session = new SessionContext();
        Assert.True(Run(session, "Session.v = 'old'; Session.read = () => Session.v;").Success);
        var later = Run(session, "Session.v = 'new'; log(Session.read());");
        Assert.True(later.Success, later.Error);
        Assert.Contains("old", later.Logs[^1], StringComparison.Ordinal);
    }

    /// <summary>The engine's own timeout says it is the script's limit, not the network.</summary>
    [Fact]
    public void TestATimeoutIsExplainedAsTheScriptsLimit() =>
        Assert.Contains("second limit, not a network timeout", JsDrawingEngine.Explain(new TimeoutException()), StringComparison.Ordinal);

    static DrawingExecutionResult Run(SessionContext session, string script) =>
        new JsDrawingEngine().Execute(script, 20, 20, session, "png", 90, render: false);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public class SessionClockCollection
{
    public const string Name = "Session clock";
}

/// <summary>
/// <c>Stage.elapsedMinutes</c> counts from when the run began, not from the first script.
/// </summary>
/// <remarks>
/// Sessions are built lazily on the first script, and the clock used to start there: a live run
/// that spent five minutes reading before it executed anything read <c>1.9</c> at five minutes in,
/// and stopped trusting the one clock it had.
/// </remarks>
public class SessionStartTests : TestsRuntime
{
    [Fact]
    public void TestTheDefaultSessionStartsWhenTheRunStarted()
    {
        var began = DateTimeOffset.UtcNow.AddMinutes(-5);
        var registry = new SessionRegistry { RunStartedUtc = began };
        Assert.Equal(began, registry.GetOrCreate("default").StartedUtc);
        Assert.Equal(began, registry.GetOrCreate("").StartedUtc);
    }

    [Fact]
    public void TestANamedSessionStartsWhenItIsCreated()
    {
        var registry = new SessionRegistry { RunStartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
        Assert.True(DateTimeOffset.UtcNow - registry.GetOrCreate("abc").StartedUtc < TimeSpan.FromSeconds(5));
        Assert.True(DateTimeOffset.UtcNow - new SessionRegistry().GetOrCreate("default").StartedUtc < TimeSpan.FromSeconds(5));
    }
}
