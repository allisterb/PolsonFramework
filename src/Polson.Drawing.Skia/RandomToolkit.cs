namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Reproducible randomness: a generator whose whole sequence is fixed by its seed.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>This exists because the run record promises a run can be re-rendered, and <c>Math.random</c>
/// breaks that promise silently.</b> The server copies the script that actually ran into
/// <c>scripts/</c> precisely so a reader can run it again — but a script drawing from
/// <c>Math.random</c> produces a different picture every time, with nothing in the record saying so.
/// Procedural composition is the case where that bites hardest, because there the arrangement
/// <i>is</i> the randomness: re-running the saved script gives a different painting rather than the
/// one under discussion.
/// </para>
/// <para>
/// <b>The algorithm is mulberry32, and it is named rather than left to the platform on purpose.</b>
/// <see cref="System.Random"/> is explicitly not guaranteed stable across .NET versions, so a run
/// seeded through it could not be re-rendered next year on a newer runtime — which is the one thing
/// this class is for. Mulberry32 is thirty-two bits of integer state and four operations, identical
/// on every machine and small enough to state here.
/// </para>
/// </remarks>
public class RandomToolkit
{
    #region Methods
    /// <summary>A generator seeded by a number. The same seed always gives the same sequence.</summary>
    /// <remarks>
    /// The seed is truncated to 32 bits, so <c>7</c> and <c>7.9</c> are the same stream and very large
    /// values wrap rather than saturating.
    /// </remarks>
    public SeededRandom Seeded(double seed) => new(SeededRandom.ToState(seed));

    /// <summary>A stable 32-bit hash of a string, for seeding by name rather than by number.</summary>
    /// <remarks>
    /// FNV-1a, so the same text gives the same number on any machine and in any session. Naming a
    /// stream is what keeps one panel's re-render from depending on how many numbers the panels
    /// before it happened to draw — see <see cref="SeededRandom.Fork"/>, which is usually the better
    /// spelling of the same idea.
    /// </remarks>
    public double Hash(string text) => SeededRandom.Fnv(text);
    #endregion
}

/// <summary>One reproducible stream of numbers.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SeededRandom
{
    #region Constructors
    internal SeededRandom(uint seed)
    {
        Seed = seed;
        start = seed;
        state = seed;
    }
    #endregion

    #region Properties
    /// <summary>The seed this stream started from, after truncation to 32 bits.</summary>
    public double Seed { get; }

    /// <summary>How many numbers have been drawn. Useful in a note when a composition is recorded.</summary>
    public int Count { get; private set; }
    #endregion

    #region Methods
    /// <summary>The next number in the stream, in <c>[0, 1)</c>.</summary>
    public double Next()
    {
        Count++;
        unchecked
        {
            state += 0x6D2B79F5u;
            var z = state;
            z = (z ^ (z >> 15)) * (z | 1u);
            z ^= z + (z ^ (z >> 7)) * (z | 61u);
            return (z ^ (z >> 14)) / 4294967296.0;
        }
    }

    /// <summary>A number in <c>[min, max)</c>.</summary>
    public double Range(double min, double max) => min + Next() * (max - min);

    /// <summary>An integer in <c>[min, max)</c> — the upper bound is excluded, as in <see cref="Range"/>.</summary>
    /// <remarks>
    /// <b>A collapsed interval is allowed and an inverted one is not, and the difference is whether a
    /// caller could have meant it.</b> <c>int(0, items.length)</c> with an empty list collapses, which
    /// is an ordinary data-driven count of zero: it returns the bound and draws nothing. Arguments the
    /// wrong way round cannot arise from data, so they are refused by name rather than quietly
    /// normalised — a normalised <c>int(9, 2)</c> returns plausible numbers forever and the mistake
    /// never surfaces.
    /// </remarks>
    public int Int(double min, double max)
    {
        if (max < min)
        {
            throw new ArgumentException(
                $"rng.int({min.ToString(CultureInfo.InvariantCulture)}, "
                + $"{max.ToString(CultureInfo.InvariantCulture)}): the upper bound is below the lower "
                + "one. The interval is [min, max), so the arguments are the wrong way round.");
        }

        var lo = (int)Math.Floor(min);
        var hi = (int)Math.Floor(max);
        return hi <= lo ? lo : lo + (int)(Next() * (hi - lo));
    }

    /// <summary><c>true</c> with the given probability, defaulting to an even chance.</summary>
    public bool Bool(double probability = 0.5) => Next() < probability;

    /// <summary><c>-1</c> or <c>1</c>, for a flip or a direction.</summary>
    public int Sign() => Next() < 0.5 ? -1 : 1;

    /// <summary>One item from a list, or <c>null</c> when it is empty.</summary>
    public object? Pick(object items)
    {
        var list = Items(items);
        return list.Count == 0 ? null : list[Int(0, list.Count)];
    }

    /// <summary>A shuffled copy. The input is left alone.</summary>
    /// <remarks>
    /// Fisher-Yates, drawing one number per item after the first, so the number of draws is a
    /// function of the list's length alone and a shuffle never desynchronises a stream.
    /// </remarks>
    public object?[] Shuffle(object items)
    {
        var list = Items(items);
        var copy = new object?[list.Count];
        list.CopyTo(copy, 0);
        for (var i = copy.Length - 1; i > 0; i--)
        {
            var j = Int(0, i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }

    /// <summary>A normally distributed number — jitter that clusters rather than spreading evenly.</summary>
    /// <remarks>
    /// Box-Muller, which produces two values per pair of draws. <b>The spare is deliberately
    /// discarded</b> so that every call advances the stream by exactly two: caching it would make the
    /// sequence depend on how many gaussians had been asked for earlier, which is precisely the
    /// reproducibility this class exists to provide.
    /// </remarks>
    public double Gaussian(double mean = 0, double standardDeviation = 1)
    {
        var u = 1.0 - Next();
        var v = Next();
        return mean + standardDeviation * Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v);
    }

    /// <summary>An independent stream named off this one, without consuming this one.</summary>
    /// <remarks>
    /// <b>This is what keeps a composition editable.</b> Draw everything from a single stream and the
    /// numbers are positional: adding one draw to the background shifts every later number, so the
    /// figure moves and the palette changes because you adjusted a smoke trail. A named fork gives
    /// each part its own sequence, derived from this stream's seed and the name, so the parts stop
    /// depending on each other's history.
    /// <code>
    /// const run = Random.seeded(1907);
    /// const back = run.fork('background'), figure = run.fork('figure');
    /// </code>
    /// The parent is not advanced, so forking is order-independent too.
    /// </remarks>
    public SeededRandom Fork(string name) =>
        new(unchecked((start ^ Fnv(name)) * 2654435761u + 0x9E3779B9u));

    /// <summary>Back to the seed, as though nothing had been drawn.</summary>
    public SeededRandom Reset()
    {
        state = start;
        Count = 0;
        return this;
    }
    #endregion

    #region Methods (internal)
    /// <summary>FNV-1a over the string's UTF-16 code units — stable everywhere.</summary>
    internal static uint Fnv(string? text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in text ?? string.Empty)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    /// <summary>A seed number reduced to the 32 bits the generator actually carries.</summary>
    internal static uint ToState(double seed) =>
        double.IsFinite(seed) ? unchecked((uint)(long)(Math.Truncate(seed) % 4294967296.0)) : 0u;
    #endregion

    #region Methods (private)
    /// <summary>A JS array, or any enumerable, as a list. A single value counts as one item.</summary>
    static IList<object?> Items(object items)
    {
        if (items is IEnumerable sequence and not string)
        {
            var list = new List<object?>();
            foreach (var item in sequence) list.Add(item);
            return list;
        }

        return items is null ? [] : [items];
    }
    #endregion

    #region Fields
    readonly uint start;
    uint state;
    #endregion
}
