namespace Polson;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// Counts what a script <em>looked at</em>, as distinct from what it drew.
/// </summary>
/// <remarks>
/// The run record has always been a record of writes — scripts executed, artifacts rendered, stages
/// declared. It says what an agent did and never what it perceived, which leaves out half of the
/// perception–action loop the whole studio is built on. An agent that measures a heading before
/// placing it, samples a pixel to check a colour landed, or reads back the render from a previous
/// stage is doing the part of the work that makes the next action informed rather than blind, and
/// none of it was visible.
/// <para>
/// This closes that gap cheaply. Probes are <em>counted</em> per execution rather than evented
/// individually, because a single <c>getPixel</c> loop can run tens of thousands of times: a tally
/// is the useful reading anyway, and it costs an increment. The one exception is reading a file back
/// out of the project, which is recorded individually with its path — that is not a measurement of
/// the drawing in hand, it is one pass reading what an earlier pass left behind, and it is the only
/// direct evidence the record can carry that coordination happened through the environment.
/// </para>
/// <para>
/// The scope is <see cref="AsyncLocal{T}"/> rather than thread-static because a script may
/// <c>await</c> an asset requisition and resume on another thread. Outside a scope every call here
/// is a null check — nothing in the drawing toolkits depends on one existing.
/// </para>
/// </remarks>
public sealed class ProbeScope : IDisposable
{
    #region Constructors
    private ProbeScope(ProbeScope? parent) => this.parent = parent;
    #endregion

    #region Properties
    /// <summary>The scope in force, or null when nothing is collecting.</summary>
    public static ProbeScope? Current => current.Value;

    /// <summary>Probe counts by kind, in descending order of frequency. Empty when nothing was inspected.</summary>
    public IReadOnlyDictionary<string, int> Counts
    {
        get
        {
            lock (gate)
            {
                return counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                             .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Project-relative paths this execution read back, in the order they were read, without repeats.</summary>
    public IReadOnlyList<string> Reads
    {
        get { lock (gate) { return [.. reads]; } }
    }

    /// <summary>Whether anything at all was observed, which is what decides if an event is worth writing.</summary>
    public bool Any
    {
        get { lock (gate) { return counts.Count > 0 || reads.Count > 0; } }
    }

    /// <summary>Every probe counted, across all kinds.</summary>
    public int Total
    {
        get { lock (gate) { return counts.Values.Sum(); } }
    }
    #endregion

    #region Methods
    /// <summary>Starts collecting. Dispose restores whatever was in force before.</summary>
    public static ProbeScope Begin()
    {
        var scope = new ProbeScope(current.Value);
        current.Value = scope;
        return scope;
    }

    /// <summary>Counts one probe of the given kind. A no-op outside a scope.</summary>
    public static void Record(string kind)
    {
        if (current.Value is not { } scope || string.IsNullOrEmpty(kind)) return;

        lock (scope.gate)
        {
            scope.counts[kind] = scope.counts.TryGetValue(kind, out var n) ? n + 1 : 1;
        }
    }

    /// <summary>
    /// Records that this execution read a file back out of the project, by project-relative path.
    /// </summary>
    /// <remarks>
    /// Kept separately from the counts, and de-duplicated, because the interesting fact is <em>which</em>
    /// artifact was consulted rather than how many times. Also counted under <see cref="Kinds.Read"/>,
    /// so a reader tallying probes does not have to special-case it.
    /// </remarks>
    public static void RecordRead(string path)
    {
        if (current.Value is not { } scope || string.IsNullOrWhiteSpace(path)) return;

        lock (scope.gate)
        {
            scope.counts[Kinds.Read] = scope.counts.TryGetValue(Kinds.Read, out var n) ? n + 1 : 1;
            if (!scope.reads.Contains(path, StringComparer.OrdinalIgnoreCase)) scope.reads.Add(path);
        }
    }

    public void Dispose() => current.Value = parent;
    #endregion

    #region Fields
    private static readonly AsyncLocal<ProbeScope?> current = new();

    private readonly ProbeScope? parent;
    private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    private readonly List<string> reads = [];
    private readonly Lock gate = new();
    #endregion

    #region Types
    /// <summary>
    /// The kinds of looking a script can do.
    /// </summary>
    /// <remarks>
    /// Deliberately few and coarse. These are not API names — several calls map onto one kind — because
    /// what a reader wants to know is whether the agent was measuring, sampling, comparing, checking
    /// what the machine has, or reading back an earlier pass. A finer breakdown is available from the
    /// script itself, which the record already stores.
    /// </remarks>
    public static class Kinds
    {
        /// <summary>Asking the geometry a question: text metrics, bounding boxes, path lengths.</summary>
        public const string Measure = "measure";

        /// <summary>Reading pixels back: one colour, a palette, a row profile.</summary>
        public const string Sample = "sample";

        /// <summary>Holding two renders against each other.</summary>
        public const string Compare = "compare";

        /// <summary>Asking what this machine actually has, rather than assuming — typefaces.</summary>
        public const string Capability = "capability";

        /// <summary>Loading a file back out of the project directory.</summary>
        public const string Read = "read";
    }
    #endregion
}
