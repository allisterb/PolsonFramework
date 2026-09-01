namespace Polson;

using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>One requisition attempt, and how it turned out.</summary>
/// <param name="Kind">What was asked for: <c>material</c>, <c>backdrop</c> or <c>matte</c>.</param>
/// <param name="Descriptor">What the script asked for, as it wrote it.</param>
/// <param name="Success">Whether an image came back.</param>
/// <param name="Failure">The failure name when it did not, else null.</param>
/// <param name="Reason">Why it was refused or how it failed, in words. Null on success.</param>
/// <param name="Model">The image model named, whether or not it was reached.</param>
/// <param name="FromCache">Whether the content-addressed cache answered, meaning nothing was spent.</param>
/// <param name="Refused">
/// True when the form-versus-substance classifier turned it away before the network. That is a
/// different event from a failure: nothing was attempted, nothing was spent, and the remedy is to
/// reword rather than to retry.
/// </param>
public sealed record RequisitionRecord(
    string Kind,
    string Descriptor,
    bool Success,
    string? Failure,
    string? Reason,
    string? Model,
    bool FromCache,
    bool Refused);

/// <summary>Where the allowance stood when this execution finished with it.</summary>
public sealed record BudgetSnapshot(int Total, int Spent, int Remaining, int CacheHits, long TokensSpent);

/// <summary>
/// Collects what a script <em>requisitioned</em>, so the record can say what a run bought.
/// </summary>
/// <remarks>
/// Requisition is the one metered surface in the studio, and it was the only thing an agent could do
/// that left no trace at all: <c>docs/project-layout.md</c> specified <c>asset.requisition</c>,
/// <c>asset.refused</c> and <c>budget</c>, and none were written. Answering "what did this painting
/// generate, and what did it draw?" meant reading the cache directory and the scripts — which is the
/// first question anyone asks of a piece that used generation, and the one a provenance file is
/// supposed to answer without being trusted.
/// <para>
/// Structured exactly like <see cref="ProbeScope"/>, and for the same reason: the toolkit lives in
/// <c>Polson.ExtendedMind</c> and must not know what a run event log is. It records into an ambient
/// scope; whoever opened the scope decides what that means. <see cref="AsyncLocal{T}"/> rather than
/// thread-static because every requisition is awaited and resumes wherever it resumes.
/// </para>
/// </remarks>
public sealed class RequisitionScope : IDisposable
{
    #region Constructors
    private RequisitionScope(RequisitionScope? parent) => this.parent = parent;
    #endregion

    #region Properties
    /// <summary>The scope in force, or null when nothing is collecting.</summary>
    public static RequisitionScope? Current => current.Value;

    /// <summary>What was requisitioned during this execution, in order.</summary>
    public IReadOnlyList<RequisitionRecord> Records
    {
        get { lock (gate) { return [.. records]; } }
    }

    /// <summary>How many records were dropped once the cap was reached.</summary>
    public int Dropped
    {
        get { lock (gate) { return dropped; } }
    }

    /// <summary>
    /// The allowance as it stood after the last requisition, or null if none was attempted.
    /// </summary>
    /// <remarks>
    /// Pushed by the toolkit rather than read back by the caller, because the toolkit that ran is not
    /// always the one the caller can see: the engine falls back to a disabled instance of its own when
    /// no requisition surface is configured, and a budget read off the configured-but-unused one would
    /// describe a different object entirely.
    /// </remarks>
    public BudgetSnapshot? Budget
    {
        get { lock (gate) { return budget; } }
    }

    /// <summary>Whether anything was requisitioned, which decides whether an event is worth writing.</summary>
    public bool Any
    {
        get { lock (gate) { return records.Count > 0; } }
    }
    #endregion

    #region Methods
    /// <summary>Starts collecting. Dispose restores whatever was in force before.</summary>
    public static RequisitionScope Begin()
    {
        var scope = new RequisitionScope(current.Value);
        current.Value = scope;
        return scope;
    }

    /// <summary>Records one requisition attempt. A no-op outside a scope.</summary>
    public static void Record(RequisitionRecord record)
    {
        if (current.Value is not { } scope || record is null) return;

        lock (scope.gate)
        {
            if (scope.records.Count >= MaxRecords) scope.dropped += 1;
            else scope.records.Add(record);
        }
    }

    /// <summary>Records where the allowance stands. Last one wins. A no-op outside a scope.</summary>
    public static void RecordBudget(BudgetSnapshot snapshot)
    {
        if (current.Value is not { } scope || snapshot is null) return;

        lock (scope.gate) { scope.budget = snapshot; }
    }

    public void Dispose() => current.Value = parent;
    #endregion

    #region Fields
    /// <summary>
    /// Ceiling per execution. Far above any real requisition pass — the whole run's budget is smaller
    /// — but a cache hit costs nothing and a loop over cached descriptors is otherwise unbounded.
    /// </summary>
    private const int MaxRecords = 64;

    private static readonly AsyncLocal<RequisitionScope?> current = new();

    private readonly RequisitionScope? parent;
    private readonly List<RequisitionRecord> records = [];
    private BudgetSnapshot? budget;
    private int dropped;
    private readonly Lock gate = new();
    #endregion
}
