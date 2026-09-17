namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>Random</c> — the seeded generator a procedural composition has to be built on.
///
/// **What is under test is reproducibility, not randomness quality.** The run record's promise is
/// that the script in `scripts/` can be run again and produce the picture being discussed, and
/// `Math.random` breaks that silently. So these assert the properties that promise rests on: the same
/// seed gives the same sequence, a fork does not disturb its parent, and a call's cost in draws does
/// not depend on what was asked for earlier.
/// </summary>
public class SeededRandomTests : TestsRuntime
{
    static RandomToolkit Kit => new();

    static double[] Take(SeededRandom rng, int n) => [.. Enumerable.Range(0, n).Select(_ => rng.Next())];

    /// <summary>The same seed gives the same sequence. Everything else here depends on this.</summary>
    [Fact]
    public void TestTheSameSeedGivesTheSameSequence()
    {
        Assert.Equal(Take(Kit.Seeded(1907), 32), Take(Kit.Seeded(1907), 32));
        Assert.NotEqual(Take(Kit.Seeded(1907), 32), Take(Kit.Seeded(1908), 32));
    }

    /// <summary>
    /// The stream is pinned to literal values, so a change of algorithm cannot pass silently.
    /// </summary>
    /// <remarks>
    /// **This is the test that makes the promise real across time.** Every other property here would
    /// still hold if the generator were swapped for a different one — the sequence would be
    /// self-consistent and reproducible *within* that version, and every run recorded before the
    /// change would quietly re-render differently. Pinning the first draws is what turns that into a
    /// failing build.
    /// </remarks>
    [Fact]
    public void TestTheStreamIsPinned()
    {
        var first = Take(Kit.Seeded(1907), 4);

        // Computed independently from a reference mulberry32 rather than captured from this
        // implementation, so the pin checks the algorithm and not merely that it is self-consistent.
        Assert.Equal(0.2320150188170373, first[0], 12);
        Assert.Equal(0.1048557036556304, first[1], 12);
        Assert.Equal(0.4907628134824336, first[2], 12);
        Assert.Equal(0.5189962256699800, first[3], 12);
    }

    /// <summary>Every number is in range, over enough draws to catch an off-by-one at either end.</summary>
    [Fact]
    public void TestNumbersStayInRange()
    {
        var rng = Kit.Seeded(3);
        for (var i = 0; i < 5000; i++)
        {
            var n = rng.Next();
            Assert.InRange(n, 0.0, 0.9999999999);

            var r = rng.Range(-4, 9);
            Assert.InRange(r, -4.0, 9.0);

            // The upper bound is excluded, as in `range` — so 7 is never drawn from int(3, 7).
            var k = rng.Int(3, 7);
            Assert.InRange(k, 3, 6);
        }
    }

    /// <summary>
    /// A collapsed interval is allowed; an inverted one is refused.
    /// </summary>
    /// <remarks>
    /// The difference is whether a caller could have meant it. `int(0, items.length)` over an empty
    /// list collapses, which is an ordinary count of zero. Arguments the wrong way round cannot come
    /// from data, and a normalised `int(9, 2)` would return plausible numbers forever with the
    /// mistake never surfacing.
    /// </remarks>
    [Fact]
    public void TestACollapsedIntervalIsAllowedAndAnInvertedOneIsRefused()
    {
        var rng = Kit.Seeded(11);

        Assert.Equal(5, rng.Int(5, 5));
        Assert.Equal(0, rng.Count);          // a collapsed interval draws nothing

        var ex = Assert.Throws<ArgumentException>(() => rng.Int(9, 2));
        Assert.Contains("wrong way round", ex.Message);
    }

    /// <summary>
    /// A fork is independent of its parent and does not advance it.
    /// </summary>
    /// <remarks>
    /// This is what keeps a composition editable: without it, adding one draw to the background
    /// shifts every later number, so the figure moves because a smoke trail was adjusted.
    /// </remarks>
    [Fact]
    public void TestAForkIsIndependentAndDoesNotAdvanceItsParent()
    {
        var parent = Kit.Seeded(1907);
        var before = parent.Count;

        var back = parent.Fork("background");
        var figure = parent.Fork("figure");

        Assert.Equal(before, parent.Count);
        Assert.NotEqual(Take(back, 16), Take(figure, 16));

        // Forking is order-independent: a fork added later changes none of the earlier ones.
        var again = Kit.Seeded(1907);
        var late = again.Fork("figure");
        Assert.Equal(Take(Kit.Seeded(1907).Fork("figure"), 16), Take(late, 16));
    }

    /// <summary>A fork is reproducible from the parent's seed and the name alone.</summary>
    [Fact]
    public void TestAForkIsReproducible()
    {
        Assert.Equal(Take(Kit.Seeded(42).Fork("smoke"), 12),
                     Take(Kit.Seeded(42).Fork("smoke"), 12));
        Assert.NotEqual(Take(Kit.Seeded(42).Fork("smoke"), 12),
                        Take(Kit.Seeded(42).Fork("smoke "), 12));
    }

    /// <summary>Drawing after a reset repeats the stream exactly.</summary>
    [Fact]
    public void TestResetRewindsTheStream()
    {
        var rng = Kit.Seeded(77);
        var first = Take(rng, 10);
        rng.Reset();

        Assert.Equal(0, rng.Count);
        Assert.Equal(first, Take(rng, 10));
    }

    /// <summary>
    /// A gaussian costs exactly two draws, however many were asked for before it.
    /// </summary>
    /// <remarks>
    /// The textbook Box-Muller caches the spare value and returns it on the next call, which halves
    /// the cost and makes the stream's position depend on the parity of earlier gaussian calls. That
    /// is the one thing this class exists to prevent, so the spare is discarded.
    /// </remarks>
    [Fact]
    public void TestAGaussianCostsTwoDrawsEveryTime()
    {
        var rng = Kit.Seeded(5);
        for (var i = 1; i <= 8; i++)
        {
            rng.Gaussian();
            Assert.Equal(i * 2, rng.Count);
        }

        // And a run of gaussians leaves the stream where a run of paired `next` calls would.
        var a = Kit.Seeded(5);
        for (var i = 0; i < 8; i++) a.Gaussian();
        var b = Kit.Seeded(5);
        for (var i = 0; i < 16; i++) b.Next();
        Assert.Equal(a.Next(), b.Next(), 12);
    }

    /// <summary>A gaussian is actually centred and spread as asked, over enough samples to tell.</summary>
    [Fact]
    public void TestAGaussianIsCentredAndSpread()
    {
        var rng = Kit.Seeded(9);
        var values = Enumerable.Range(0, 20000).Select(_ => rng.Gaussian(10, 2)).ToArray();
        var mean = values.Average();
        var sd = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());

        Assert.InRange(mean, 9.9, 10.1);
        Assert.InRange(sd, 1.9, 2.1);
    }

    /// <summary>A shuffle is a permutation of a copy, and leaves the input alone.</summary>
    [Fact]
    public void TestShuffleIsAPermutationOfACopy()
    {
        var source = new List<object?> { "a", "b", "c", "d", "e", "f" };
        var rng = Kit.Seeded(21);
        var shuffled = rng.Shuffle(source);

        Assert.Equal(6, shuffled.Length);
        Assert.Equal(new List<object?> { "a", "b", "c", "d", "e", "f" }, source);
        Assert.Equal(source.OrderBy(x => x!.ToString()), shuffled.OrderBy(x => x!.ToString()));

        // One draw per item after the first, so a shuffle never desynchronises a stream.
        Assert.Equal(5, rng.Count);
    }

    /// <summary>An empty list picks to null rather than throwing.</summary>
    [Fact]
    public void TestPickingFromNothingIsNull()
    {
        Assert.Null(Kit.Seeded(1).Pick(new List<object?>()));
        Assert.Equal("only", Kit.Seeded(1).Pick(new List<object?> { "only" }));
    }

    /// <summary>`bool` honours its probability, and the extremes are certain.</summary>
    [Fact]
    public void TestBoolHonoursItsProbability()
    {
        var rng = Kit.Seeded(13);
        var hits = Enumerable.Range(0, 10000).Count(_ => rng.Bool(0.25));
        Assert.InRange(hits, 2300, 2700);

        Assert.False(Kit.Seeded(13).Bool(0));
        Assert.True(Kit.Seeded(13).Bool(1));
    }

    /// <summary>The hash is stable, and differs for text that differs.</summary>
    [Fact]
    public void TestTheHashIsStable()
    {
        Assert.Equal(Kit.Hash("panel-3"), Kit.Hash("panel-3"));
        Assert.NotEqual(Kit.Hash("panel-3"), Kit.Hash("panel-4"));
        Assert.Equal(2166136261d, Kit.Hash(string.Empty));
    }

    /// <summary>The seed is truncated to 32 bits rather than saturating or throwing.</summary>
    [Fact]
    public void TestTheSeedIsTruncatedTo32Bits()
    {
        Assert.Equal(Take(Kit.Seeded(7), 6), Take(Kit.Seeded(7.9), 6));
        Assert.Equal(7d, Kit.Seeded(7).Seed);

        // Non-finite seeds fall back rather than producing a NaN stream.
        Assert.All(Take(Kit.Seeded(double.NaN), 4), n => Assert.InRange(n, 0.0, 1.0));
        Assert.All(Take(Kit.Seeded(double.PositiveInfinity), 4), n => Assert.InRange(n, 0.0, 1.0));
    }

    /// <summary>Counting is honest, because a note in the record may quote it.</summary>
    [Fact]
    public void TestDrawsAreCounted()
    {
        var rng = Kit.Seeded(2);
        rng.Next();
        rng.Range(0, 1);
        rng.Int(0, 10);
        rng.Bool();
        rng.Sign();
        rng.Pick(new List<object?> { "a", "b" });

        Assert.Equal(6, rng.Count);
    }
}
