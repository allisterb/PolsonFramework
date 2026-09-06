namespace Polson.MCPServer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Per-MCP-session state: scratch storage dictionary and script history.
/// </summary>
public sealed class SessionContext
{
    #region Fields
    private int _activeCalls;
    #endregion

    #region Properties
    public string SessionId { get; set; } = string.Empty;

    public DateTimeOffset LastAccess { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this session began. The anchor for <c>Stage.elapsedMinutes</c>.</summary>
    /// <remarks>
    /// The session rather than the server process, because a server can outlive one run — under ADK
    /// a single stdio process serves every session of an app, so its own start time would measure
    /// from whenever the app was built rather than from when this run began.
    /// <para>
    /// Set once at construction and never updated; <see cref="LastAccess"/> is the moving one, and
    /// the two must not be confused — an idle sweep touches the second and would silently reset a
    /// run's clock through the first.
    /// </para>
    /// </remarks>
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Per-session scratch storage exposed to the JS engine as the global Session object.
    /// </summary>
    public Dictionary<string, object?> Storage { get; } = new();

    /// <summary>
    /// Historical record of scripts executed by the agent in this session.
    /// </summary>
    public List<string> ScriptHistory { get; } = new();

    /// <summary>
    /// The stage of work the agent says it is in, e.g. "Blocking". Null until it declares one.
    /// </summary>
    /// <remarks>
    /// Held on the session because it has to outlive a single execution — a stage spans many script
    /// calls, and that is the whole point of tagging with it. It cannot live on the ambient log
    /// context: a property pushed inside an async tool handler never propagates back to the
    /// dispatcher, so the next request would not see it. This mirrors how Camel keeps <c>CaseId</c>
    /// on the session and re-pushes it at the top of every call.
    /// </remarks>
    public string? Stage { get; set; }

    /// <summary>
    /// True while one or more tool calls are actively executing on this session.
    /// </summary>
    public bool IsBusy => Volatile.Read(ref _activeCalls) > 0;
    #endregion

    #region Methods
    public void EnterCall() => Interlocked.Increment(ref _activeCalls);

    public void LeaveCall()
    {
        Interlocked.Decrement(ref _activeCalls);
        LastAccess = DateTimeOffset.UtcNow;
    }
    #endregion
}

/// <summary>
/// Thread-safe registry mapping an MCP session ID to its SessionContext.
/// </summary>
public sealed class SessionRegistry : IDisposable
{
    #region Fields
    private readonly int maxSessions;
    private readonly ConcurrentDictionary<string, Lazy<SessionContext>> sessions = new();
    #endregion

    #region Constructors
    public SessionRegistry(int maxSessions = 64)
    {
        this.maxSessions = maxSessions;
    }
    #endregion

    #region Properties
    public int Count => sessions.Count;
    #endregion

    #region Methods
    public SessionContext GetOrCreate(string sessionId)
    {
        var id = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId;
        var lazy = sessions.GetOrAdd(id, key =>
        {
            if (sessions.Count >= maxSessions)
                throw new InvalidOperationException($"Maximum concurrent session limit ({maxSessions}) reached.");

            return new Lazy<SessionContext>(() => new SessionContext { SessionId = key });
        });

        var ctx = lazy.Value;
        ctx.LastAccess = DateTimeOffset.UtcNow;
        return ctx;
    }

    public void End(string sessionId)
    {
        var id = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId;
        sessions.TryRemove(id, out _);
    }

    public void SweepIdle(TimeSpan evictTtl)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in sessions)
        {
            if (!kv.Value.IsValueCreated) continue;
            var ctx = kv.Value.Value;
            if (ctx.IsBusy) continue;

            var idle = now - ctx.LastAccess;
            if (idle > evictTtl)
            {
                End(kv.Key);
            }
        }
    }

    public void Dispose()
    {
        sessions.Clear();
    }
    #endregion
}

/// <summary>
/// Background service that periodically sweeps abandoned idle sessions.
/// </summary>
public sealed class IdleSessionSweeper : BackgroundService
{
    #region Fields
    private readonly SessionRegistry registry;
    private readonly TimeSpan interval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan evictTtl = TimeSpan.FromHours(4);
    #endregion

    #region Constructors
    public IdleSessionSweeper(SessionRegistry registry)
    {
        this.registry = registry;
    }
    #endregion

    #region Methods
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                registry.SweepIdle(evictTtl);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
    #endregion
}

