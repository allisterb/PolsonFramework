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

    /// <summary>
    /// Per-session scratch storage exposed to the JS engine as the global Session object.
    /// </summary>
    public Dictionary<string, object?> Storage { get; } = new();

    /// <summary>
    /// Historical record of scripts executed by the agent in this session.
    /// </summary>
    public List<string> ScriptHistory { get; } = new();

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

