namespace Polson.ExtendedMind.ParallelSearch;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// One piece of commissioned research: what was asked, what came back, and what it rests on.
/// </summary>
/// <remarks>
/// <para>
/// Every member is a plain read. Nothing here touches the network — a task is filled in by the
/// <c>Research</c> MCP tool, which waits for the service outside the JavaScript sandbox where a
/// blocking call is safe. A script only ever reads what has already arrived.
/// </para>
/// <para>
/// That split is forced rather than chosen: <c>JsDrawingEngine.ScriptTimeoutSeconds</c> is 30 and the
/// promise ceiling is 120, while a measured <c>base</c> run took 96 seconds and <c>core</c> takes one
/// to five minutes. A script cannot wait for research and stay inside its own guard.
/// </para>
/// <para>
/// Exposed to the JavaScript sandbox. Members are PascalCase here; Jint resolves the JS camelCase
/// spelling onto them, so a script reading <c>task.isComplete</c> reaches <see cref="IsComplete"/>.
/// </para>
/// </remarks>
public sealed class ResearchTask
{
    #region Properties
    /// <summary>The service's run id. The durable handle: it outlives this session.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>What this research is for, in the requester's words. Written into the run record.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The question put to the service.</summary>
    public string Objective { get; init; } = string.Empty;

    public string Processor { get; init; } = string.Empty;

    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// One of <c>queued</c>, <c>running</c>, <c>completed</c>, <c>failed</c>, <c>cancelled</c>,
    /// <c>action_required</c>. A string rather than a boolean, because a boolean collapses the three
    /// dead states into "not yet" and invites a caller to wait on a run that will never finish.
    /// </summary>
    public string Status { get; internal set; } = "queued";

    /// <summary>Seconds since the task was started. What a caller needs to decide whether to keep waiting.</summary>
    public double ElapsedSeconds => Math.Round((DateTimeOffset.UtcNow - StartedUtc).TotalSeconds, 1);

    /// <summary>
    /// The data, as ordinary JavaScript values — objects, arrays, numbers, strings. Null until the
    /// task completes, and null is not an empty result.
    /// </summary>
    public object? Result { get; internal set; }

    /// <summary>
    /// What each field rests on: reasoning, sources, and confidence. Empty until the task completes.
    /// </summary>
    /// <remarks>
    /// This is why the studio commissions research rather than asking a model. A figure without its
    /// basis is indistinguishable from an invented one, and the whole point is that ours is not.
    /// </remarks>
    public IReadOnlyList<FieldBasis> Basis { get; internal set; } = [];

    /// <summary>Why the research failed, when it did. Null otherwise.</summary>
    public string? Error { get; internal set; }

    /// <summary>
    /// What the codepoint scan found in what this run brought back — hidden characters, or text
    /// addressed to the reader. Empty is the expected result and means nothing was concealed.
    /// </summary>
    /// <remarks>
    /// A finding is not proof the figure is wrong, but it is a reason to look at the source before
    /// citing it: a page that talks to whoever is processing it is not behaving like a source. The
    /// prose has already been stripped of the concealment classes by the time you read it.
    /// </remarks>
    public IReadOnlyList<string> Warnings { get; internal set; } = [];

    /// <summary>Finished, with data. Only then is <see cref="Result"/> meaningful.</summary>
    public bool IsComplete => Status == "completed";

    /// <summary>Finished without data. Waiting longer will not help.</summary>
    public bool IsFailed => Status is "failed" or "cancelled";

    /// <summary>Still going. Neither complete nor failed.</summary>
    public bool IsActive => Status is "queued" or "running" or "cancelling";

    /// <summary>Stalled on something outside the run — not progressing, not finished.</summary>
    public bool NeedsAction => Status == "action_required";
    #endregion

    #region Methods
    /// <summary>
    /// A caption-ready source line for one output field, or null when that field has no sources.
    /// </summary>
    /// <remarks>
    /// Array elements are addressed with a dot index as the service reports them — <c>missions.0</c>
    /// — so a per-row citation under a table is a direct lookup rather than a search.
    /// </remarks>
    public string? CiteField(string field)
    {
        var basis = BasisFor(field);
        if (basis?.Citations is not { Count: > 0 } citations) return null;

        return string.Join("; ", citations.Select(c => c.Cite()).Distinct());
    }

    /// <summary>The full basis for one field — reasoning, confidence and sources — or null.</summary>
    public FieldBasis? BasisFor(string field)
    {
        foreach (var entry in Basis)
        {
            if (string.Equals(entry.Field, field, StringComparison.Ordinal)) return entry;
        }

        return null;
    }

    /// <summary>Every distinct source across the whole result, for a combined credit line.</summary>
    public IReadOnlyList<string> Sources() =>
        Basis.SelectMany(b => b.Citations ?? [])
             .Select(c => c.Cite())
             .Where(c => !string.IsNullOrWhiteSpace(c))
             .Distinct()
             .ToArray();

    public override string ToString() =>
        $"{Id} [{Status}] {Description} ({ElapsedSeconds:F0}s)";
    #endregion
}

/// <summary>
/// The <c>Research</c> global: read-only access to research commissioned for this run.
/// </summary>
/// <remarks>
/// <para>
/// There is deliberately no way to <b>start</b> research from a script. Starting one is a metered
/// call that takes minutes, and a script has thirty seconds — so commissioning belongs to the
/// <c>Research</c> MCP tool, which the agent (or, in a multi-agent run, the Facilitator) calls as its
/// own step and waits on.
/// </para>
/// <para>
/// Exposed to the JavaScript sandbox; see the note on <see cref="ResearchTask"/> about naming.
/// </para>
/// </remarks>
public sealed class ResearchToolkit
{
    #region Constructors
    public ResearchToolkit(ResearchRegistry registry) => this.registry = registry;
    #endregion

    #region Properties
    /// <summary>Every task commissioned this run, in the order they were started.</summary>
    public IReadOnlyList<ResearchTask> Tasks => registry.All;

    /// <summary>How many there are. <c>Research.count === 0</c> means none was commissioned.</summary>
    public int Count => registry.All.Count;

    /// <summary>
    /// The most recently commissioned task, or null. The common case is one piece of research per
    /// graphic, and this saves carrying an id between scripts.
    /// </summary>
    public ResearchTask? Latest => registry.All.Count == 0 ? null : registry.All[^1];

    /// <summary>
    /// What remains of this run's research allowance. Read it before planning work that assumes more
    /// figures can still be sourced.
    /// </summary>
    public ResearchBudget Budget => registry.Budget;
    #endregion

    #region Methods
    /// <summary>The task with this id, or null. Ids come from the <c>Research</c> tool.</summary>
    public ResearchTask? Get(string id) => registry.Get(id);

    /// <summary>
    /// The task whose description contains <paramref name="text"/>, case-insensitively, or null.
    /// Lets a later stage find research by what it was for rather than by an id it must carry.
    /// </summary>
    public ResearchTask? Find(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : registry.All.FirstOrDefault(
                t => t.Description.Contains(text, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether every commissioned task finished with data. True when none was commissioned.</summary>
    public bool AllComplete() => registry.All.All(t => t.IsComplete);
    #endregion

    #region Fields
    private readonly ResearchRegistry registry;
    #endregion
}

/// <summary>
/// How many pieces of research a run may commission. A hard ceiling, checked before anything is
/// started.
/// </summary>
/// <remarks>
/// <para>
/// Counted in <b>runs</b> rather than in money, for the same reason <c>AssetBudget</c> counts
/// generations: it is the only quantity knowable before the call. Unlike image generation, though,
/// there is no measured figure to set beside it — <b>the Task API returns no usage data at all</b>.
/// Search and Extract both report a <c>usage</c> array; a task envelope carries <c>run</c> and
/// <c>output</c> and nothing else, verified against the live service. So spend can only be inferred
/// from the count and the published price of the processor, and this count is the whole control.
/// </para>
/// <para>
/// A resumed or re-collected task does not spend again — only starting one does.
/// </para>
/// </remarks>
public sealed class ResearchBudget
{
    #region Constructors
    public ResearchBudget(int total) => Total = total;
    #endregion

    #region Properties
    /// <summary>Pieces of research this run may successfully commission.</summary>
    public int Total { get; }

    /// <summary>Runs that succeeded or are still going. A failed run is refunded and does not count.</summary>
    public int Spent { get; internal set; }

    /// <summary>
    /// Every run ever started, refunded or not. The stop on a run that keeps failing.
    /// </summary>
    public int Attempts { get; internal set; }

    public int Remaining => Math.Max(0, Total - Spent);

    /// <summary>
    /// How many extra starts a run may make purely to recover from failures.
    /// </summary>
    /// <remarks>
    /// A failed run is refunded so a transient service fault does not leave a graphic with no data at
    /// all — but a refund that could be earned indefinitely is not a ceiling. Two retries is enough
    /// for a blip and short of a loop.
    /// </remarks>
    public const int RetryAllowance = 2;

    public int MaxAttempts => Total + RetryAllowance;

    public bool Exhausted => Remaining <= 0 || Attempts >= MaxAttempts;
    #endregion

    #region Methods
    public bool CanAfford(int count = 1) => Remaining >= count && Attempts + count <= MaxAttempts;
    #endregion
}

/// <summary>
/// The research commissioned during one run. Held on the session so it spans executions, and keyed
/// by the service's run id so a task can be recovered after a session drop.
/// </summary>
public sealed class ResearchRegistry
{
    #region Constructors
    /// <param name="budget">Runs allowed. Defaults to <see cref="DefaultBudget"/>.</param>
    public ResearchRegistry(int budget = DefaultBudget) => Budget = new ResearchBudget(budget);
    #endregion

    #region Properties
    /// <summary>
    /// Runs allowed when nothing is configured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two, and they are not equal.</b> The first must carry the entire data requirement: one
    /// schema costs one run, pays the latency once, and lets the service reconcile the fields against
    /// each other rather than answering them in isolation. The second exists <b>only to correct</b> —
    /// a field that came back empty, wrong, or at a confidence too low to draw. It is not the second
    /// half of the research, and an agent that plans to use both has already split the requirement,
    /// which is the failure this ceiling exists to prevent.
    /// </para>
    /// <para>
    /// One would have been the tighter rule, and was the first choice. Two is deliberate: an agent
    /// that can see its answer is unusable and cannot act on that judgment is being denied the review
    /// step we would want a person to take. The ceiling is there to keep the work focused, not to
    /// stop it from being checked.
    /// </para>
    /// <para>
    /// The size of a single question is bounded by <b>field capacity, not by length</b> — roughly 5
    /// top-level fields on <c>base</c>, 10 on <c>core</c>, 20 on <c>pro</c>. An array counts as one
    /// field however many rows it holds: a measured run returned six Apollo missions with three
    /// properties each, eighteen values, under a single <c>missions</c> field on <c>base</c>. So the
    /// answer to "it will not fit" is <b>nest it and step up a tier</b>, never split it into a second
    /// run.
    /// </para>
    /// <para>
    /// A failed run is refunded, so these are two <i>successful</i> runs rather than two attempts — a
    /// transient service fault costs nothing. Raise <c>Research:Budget</c> for a piece that genuinely
    /// needs unrelated bodies of data.
    /// </para>
    /// </remarks>
    public const int DefaultBudget = 2;

    public ResearchBudget Budget { get; }

    /// <summary>In the order they were started, which is the order a reader wants them.</summary>
    public IReadOnlyList<ResearchTask> All
    {
        get
        {
            lock (gate) return ordered.ToArray();
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Registers a newly started run. The only way to create a task, so the mutable state stays
    /// behind this type — a script reaches tasks through <see cref="ResearchToolkit"/>, which exposes
    /// no way to write one. An agent must not be able to author its own "sourced" figures.
    /// </summary>
    public ResearchTask Start(
        string id, string description, string objective, string processor, string status = "queued")
    {
        var task = new ResearchTask
        {
            Id = id,
            Description = description,
            Objective = objective,
            Processor = processor,
        };

        task.Status = status;

        // Charged here rather than at the call site, so the count cannot drift from the registry —
        // and only for a genuinely new run, since Add is idempotent on the id.
        lock (gate)
        {
            if (!byId.ContainsKey(id))
            {
                Budget.Spent++;
                Budget.Attempts++;
            }
        }

        return Add(task);
    }

    private ResearchTask Add(ResearchTask task)
    {
        lock (gate)
        {
            if (byId.TryAdd(task.Id, task)) ordered.Add(task);
            return byId[task.Id];
        }
    }

    public ResearchTask? Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (gate) return byId.GetValueOrDefault(id);
    }

    /// <summary>Records a completed run's data and basis.</summary>
    public void Complete(ResearchTask task, object? result, IReadOnlyList<FieldBasis> basis, string? status = null)
    {
        task.Status = status ?? "completed";
        task.Result = result;
        task.Basis = basis;
    }

    /// <summary>
    /// Records that a run ended without data, and refunds it.
    /// </summary>
    /// <remarks>
    /// The refund is what makes a ceiling of one survivable. Without it a single transient service
    /// fault would leave a graphic with no figures and no way to get any — and the agent's only
    /// remaining options would be to ship an empty chart or invent the numbers, which is the outcome
    /// this entire surface exists to prevent. <see cref="ResearchBudget.Attempts"/> is not refunded,
    /// so a run that keeps failing still stops.
    /// </remarks>
    public void Fail(ResearchTask task, string? error)
    {
        lock (gate)
        {
            if (task.Status != "failed" && Budget.Spent > 0) Budget.Spent--;
        }

        task.Status = "failed";
        task.Error = error;
    }

    /// <summary>
    /// Records what a codepoint scan found in this run's returned text. Kept here with the other
    /// mutators so a script can read a warning and never write one.
    /// </summary>
    public void Flag(ResearchTask task, IReadOnlyList<string> warnings) => task.Warnings = warnings;

    /// <summary>Updates a run's status while it is still going.</summary>
    public void SetStatus(ResearchTask task, string status)
    {
        if (!string.IsNullOrWhiteSpace(status)) task.Status = status;
    }
    #endregion

    #region Fields
    private readonly object gate = new();
    private readonly Dictionary<string, ResearchTask> byId = new(StringComparer.Ordinal);
    private readonly List<ResearchTask> ordered = [];
    #endregion
}

/// <summary>
/// Converts parsed JSON into the plain CLR values Jint surfaces as ordinary JavaScript.
/// </summary>
/// <remarks>
/// A <see cref="JsonElement"/> reaches a script as an opaque CLR struct, so <c>result.missions[0]</c>
/// would not work on one. Dictionaries, lists and primitives map to JS objects, arrays and values —
/// which is what "the agent can just read it in JS" requires.
/// <para>
/// Named <c>JsonInterop</c> rather than the more obvious <c>JsonValue</c>, which collides with
/// <see cref="System.Text.Json.Nodes.JsonValue"/> wherever both namespaces are in scope.
/// </para>
/// </remarks>
public static class JsonInterop
{
    #region Methods
    public static object? ToClr(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ToClr(p.Value), StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(ToClr).ToList(),
        JsonValueKind.String => element.GetString(),
        // Integers stay integral so a count reads as 6 rather than 6.0; everything else is a double,
        // which is what JavaScript numbers are anyway. The (object) cast is load-bearing: without it
        // the conditional's two branches unify to double and every integer silently becomes a float.
        JsonValueKind.Number => element.TryGetInt64(out var i) ? (object)i : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    /// <summary>Null-tolerant overload, for an output that may be absent.</summary>
    public static object? ToClr(JsonElement? element) => element is { } e ? ToClr(e) : null;
    #endregion
}
