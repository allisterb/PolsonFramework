// SPDX-License-Identifier: MIT
namespace Polson;

using System;
using System.Threading;

/// <summary>
/// The moment the running script's time limit passes, for native work the script started to check.
/// </summary>
/// <remarks>
/// <para>
/// <b>Jint stops a script only between its statements.</b> A long native call — an encode, a whole film rendered
/// frame by frame inside <c>comp.saveVideo</c> — runs straight past the limit, finishes, moves its file into place,
/// and only then does the script throw "timed out", whose message says nothing it drew was kept. A run was told
/// exactly that while a part holding the wrong span had been written under the name it was about to reuse, and a
/// retry then met the still-running encode's partial file, locked.
/// </para>
/// <para>
/// So the engine opens one of these per execution, and a loop that can run for seconds calls
/// <see cref="ThrowIfPassed"/> as it goes and cleans up after itself. <see cref="AsyncLocal{T}"/>, like
/// <see cref="RequisitionScope"/>, because a script's native calls resume wherever its awaits resume.
/// </para>
/// </remarks>
public sealed class ScriptDeadline : IDisposable
{
    #region Constructors
    private ScriptDeadline(ScriptDeadline? parent, DateTime dueUtc, TimeSpan limit)
    {
        this.parent = parent;
        DueUtc = dueUtc;
        Limit = limit;
    }
    #endregion

    #region Properties
    /// <summary>The deadline in force, or null outside a script.</summary>
    public static ScriptDeadline? Current => current.Value;

    /// <summary>When the limit passes, UTC.</summary>
    public DateTime DueUtc { get; }

    /// <summary>The script's whole limit, for messages.</summary>
    public TimeSpan Limit { get; }
    #endregion

    #region Methods
    /// <summary>Opens a deadline <paramref name="limit"/> from now for the current execution.</summary>
    public static ScriptDeadline Begin(TimeSpan limit)
    {
        var scope = new ScriptDeadline(current.Value, DateTime.UtcNow + limit, limit);
        current.Value = scope;
        return scope;
    }

    /// <summary>
    /// Throws a <see cref="TimeoutException"/> naming <paramref name="who"/> when the script's limit has passed. Does
    /// nothing outside a script.
    /// </summary>
    public static void ThrowIfPassed(string who)
    {
        if (current.Value is { } scope && DateTime.UtcNow >= scope.DueUtc)
            throw new TimeoutException($"{who} was stopped at the script's {scope.Limit.TotalSeconds:0}-second limit, and "
                + "nothing it was writing was kept. Encode a shorter span per script.");
    }

    public void Dispose() => current.Value = parent;
    #endregion

    #region Fields
    private static readonly AsyncLocal<ScriptDeadline?> current = new();

    private readonly ScriptDeadline? parent;
    #endregion
}
