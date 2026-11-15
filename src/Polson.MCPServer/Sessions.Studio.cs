namespace Polson.MCPServer;

using System;
using System.Collections.Generic;

using Polson.ExtendedMind.ParallelSearch;

/// <summary>The studio's half of <see cref="SessionContext"/>: research, Qloo answers and read-backs.</summary>
/// <remarks>Kept apart so <c>Polson.Engine</c> can link <c>Sessions.cs</c> without ExtendedMind or <c>Verify</c>.</remarks>
public sealed partial class SessionContext
{
    #region Properties
    /// <summary>
    /// Research commissioned during this run, exposed to scripts as the global <c>Research</c>.
    /// </summary>
    /// <remarks>
    /// On the session for the same reason <see cref="Stage"/> is: a task is started by one tool call
    /// and read by a later one, so it has to outlive a single execution. Keyed by the service's run
    /// id, which is durable — a task can be recovered after a session drop by asking for it again.
    /// </remarks>
    public ResearchRegistry Research { get; } = new(
        ResearchBudget,
        string.IsNullOrWhiteSpace(ResearchArchiveDir) ? null : new ResearchArchive(ResearchArchiveDir));

    /// <summary>
    /// Runs allowed per session, set once at startup from <c>Research:Budget</c>.
    /// </summary>
    /// <remarks>
    /// Static because a session is created on demand, deep inside a tool call, with no route for
    /// configuration to reach it — the same arrangement as <see cref="JsDrawingEngine.Assets"/>.
    /// Unlike the asset budget this is <b>per session</b> rather than per server run, because the
    /// research registry is too: a public deployment serving many visitors from one process would
    /// otherwise let the first of them spend everyone's allowance.
    /// </remarks>
    public static int ResearchBudget { get; set; } = Polson.ExtendedMind.ParallelSearch.ResearchRegistry.DefaultBudget;

    /// <summary>
    /// Where a finished research run is filed, or null to file nothing.
    /// </summary>
    /// <remarks>
    /// Static for the reason <see cref="ResearchBudget"/> is: a session is built on demand inside a
    /// tool call, with no route for configuration to reach it.
    /// <para>
    /// Null means a run's figures become unverifiable the moment the session ends — the registry
    /// holds its tasks in memory and the run record carries only the run id — so the CLI sets this
    /// whenever a project is present. It stays nullable for the ad-hoc engine, which has no project
    /// to file into.
    /// </para>
    /// </remarks>
    public static string? ResearchArchiveDir { get; set; }

    /// <summary>
    /// Qloo answers fetched during this session, exposed to scripts as the global <c>Taste</c>.
    /// </summary>
    /// <remarks>
    /// Per session for the reason <see cref="Research"/> is: on a public deployment one process serves many
    /// visitors, and the first must not spend everyone's allowance. The key's pacing and its monthly reserve are
    /// shared all the same — they live on the one client every session is a scope of.
    /// </remarks>
    public Polson.ExtendedMind.Taste.TasteRegistry Taste { get; } =
        new(new Polson.ExtendedMind.Taste.QlooBudget(TasteBudget, TasteMonthlyReserve));

    /// <summary>Qloo requests allowed per session, from <c>Qloo:Budget</c>. Static for the reason <see cref="ResearchBudget"/> is.</summary>
    public static int TasteBudget { get; set; } = Polson.ExtendedMind.Taste.QlooBudget.DefaultTotal;

    /// <summary>Monthly Qloo requests never spent, from <c>Qloo:MonthlyReserve</c>.</summary>
    public static int TasteMonthlyReserve { get; set; } = Polson.ExtendedMind.Taste.QlooBudget.DefaultMonthlyReserve;

    /// <summary>
    /// The latest <c>Verify.film</c> or <c>Verify.image</c>, held so a later script can settle its strays with
    /// <c>Verify.stray</c> once the agent has looked at their frames or crops.
    /// </summary>
    public ReadBackCheck? ReadBack { get; set; }

    /// <summary>Every piece's latest check by its name, so <c>Verify.stray(piece, n, …)</c> settles film B after film C was checked.</summary>
    public Dictionary<string, ReadBackCheck> ReadBacks { get; } = new(StringComparer.OrdinalIgnoreCase);
    #endregion
}
