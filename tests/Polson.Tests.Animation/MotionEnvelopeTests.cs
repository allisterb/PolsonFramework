namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Linq;

using Polson.Animation;

using Xunit;

/// <summary>
/// The envelope node: Essa's application, release and relaxation phases (thesis Figs 6-6, 6-7), keyed by when an action
/// peaks rather than by four waypoints.
/// </summary>
public class MotionEnvelopeTests : TestsRuntime
{
    static MotionNodeFactory N => new MotionToolkit().Nodes;

    static double At(MotionNode node, double t) => (double)node.At(t);

    [Fact]
    public void AHitRisesToItsPeakFallsToItsResidualAndSettles()
    {
        var e = N.Envelope(Opts(("at", 1d), ("peak", 2d), ("attack", 0.2d), ("hold", 0.1d), ("release", 0.4d), ("residual", 0.25d), ("settle", 0.5d)));
        Assert.Equal(0.8, e.Start, 9);
        Assert.Equal(2.0, e.End, 9);

        Assert.Equal(0d, At(e, 0), 9);
        Assert.Equal(0d, At(e, 0.8), 9);
        Assert.Equal(2d, At(e, 1), 9);
        Assert.Equal(2d, At(e, 1.1), 9);
        Assert.Equal(0.5, At(e, 1.5), 9);
        Assert.Equal(0d, At(e, 2), 9);
        Assert.Equal(0d, At(e, 5), 9);

        // No jump anywhere: the boundaries are approached from both sides.
        foreach (var t in new[] { 0.8, 1.0, 1.1, 1.5, 2.0 })
            Assert.True(Math.Abs(At(e, t - 1e-7) - At(e, t + 1e-7)) < 1e-4, $"a jump at {t}s");
    }

    /// <summary>Application is Essa's <c>a(e^bx − 1)</c> and release his <c>a(e^(c − bx) − 1)</c>, with b × duration the sharpness.</summary>
    [Fact]
    public void ThePhasesAreEssasExponentials()
    {
        const double k = 3, attack = 0.3, release = 0.6, peak = 1, residual = 0.1;
        var e = N.Envelope(Opts(("at", 0.3d), ("attack", attack), ("release", release), ("residual", residual), ("sharpness", k)));

        var b = k / attack;
        var a = peak / (Math.Exp(k) - 1);
        foreach (var x in new[] { 0.05, 0.1, 0.2, 0.25 })
            Assert.Equal(a * (Math.Exp(b * x) - 1), At(e, x), 9);

        // Release from the peak to the residual over its own duration: c is k, so it reaches the residual at x = release.
        var br = k / release;
        var ar = (peak - residual) / (Math.Exp(k) - 1);
        foreach (var x in new[] { 0.1, 0.3, 0.5 })
            Assert.Equal(residual + ar * (Math.Exp(k - br * x) - 1), At(e, 0.3 + x), 9);

        // Accelerating into the peak, decelerating out of it.
        Assert.True(At(e, 0.15) < 0.5 * peak);
        Assert.True(At(e, 0.3 + 0.3) < residual + 0.5 * (peak - residual));
    }

    [Fact]
    public void SharpnessZeroIsTheLinearRamp()
    {
        var e = N.Envelope(Opts(("at", 1d), ("attack", 1d), ("release", 1d), ("residual", 0d), ("settle", 0d), ("sharpness", 0d)));
        Assert.Equal(0.25, At(e, 0.25), 9);
        Assert.Equal(0.5, At(e, 1.5), 9);

        var split = N.Envelope(Opts(("at", 1d), ("attack", 1d), ("sharpness", Opts(("attack", 0d)))));
        Assert.Equal(0.5, At(split, 0.5), 9);
        Assert.True(At(split, 1.2) < At(e, 1.2));
    }

    /// <summary>A second hit starts from wherever the first has got to, so a retrigger mid-release does not jump.</summary>
    [Fact]
    public void AHitRetriggersFromWhereTheLastOneIs()
    {
        var e = N.Envelope(new object[] { Opts(("at", 0.5d)), Opts(("at", 1d), ("peak", 0.6d)) });
        Assert.Equal(2, e.Count);
        var start = 1 - 0.25;
        var single = N.Envelope(Opts(("at", 0.5d)));
        Assert.Equal(At(single, start), At(e, start), 9);
        Assert.True(Math.Abs(At(e, start - 1e-7) - At(e, start + 1e-7)) < 1e-4);
        Assert.Equal(0.6, At(e, 1), 9);
        Assert.Equal(0d, At(e, e.End), 9);
    }

    [Fact]
    public void DefaultsApplyToEveryHitAndFromIsTheRest()
    {
        var e = N.Envelope(new object[] { Opts(("at", 1d)), Opts(("at", 3d), ("peak", 1d)) }, Opts(("from", 3d), ("peak", 5d), ("attack", 0.5d)));
        Assert.Equal(3d, At(e, 0), 9);
        Assert.Equal(5d, At(e, 1), 9);
        Assert.Equal(1d, At(e, 3), 9);
        Assert.Equal(0.5, e.Start, 9);
        Assert.Equal(3d, At(e, 2.5), 9);
        Assert.Equal(3d, At(e, 10), 9);
    }

    [Fact]
    public void BadHitsAreRefusedByName()
    {
        Assert.Contains("'decay'", Assert.Throws<ArgumentException>(() => N.Envelope(Opts(("at", 1d), ("decay", 1d)))).Message);
        Assert.Contains("'at'", Assert.Throws<ArgumentException>(() => N.Envelope(Opts(("peak", 1d)))).Message);
        Assert.Throws<ArgumentException>(() => N.Envelope(Opts(("at", 1d), ("release", -1d))));
        Assert.Throws<ArgumentException>(() => N.Envelope(Opts(("at", 1d), ("residual", 1.5d))));
        Assert.Throws<ArgumentException>(() => N.Envelope(Opts(("at", 1d), ("sharpness", Opts(("rise", 1d))))));
        Assert.Throws<ArgumentException>(() => N.Envelope(Opts(("at", 1d)), Opts(("at", 2d))));
        Assert.Throws<ArgumentException>(() => N.Envelope(new object[] { Opts(("at", 1d)), Opts(("at", 1d)) }));
        Assert.Throws<ArgumentException>(() => N.Envelope(Array.Empty<object>()));
        Assert.Contains("shorten its attack", Assert.Throws<ArgumentException>(() =>
            N.Envelope(new object[] { Opts(("at", 1d), ("attack", 0.1d)), Opts(("at", 1.2d), ("attack", 0.5d)) })).Message);
    }

    /// <summary>"Hit joy at 0.4s": an envelope is a channel like any other node, and the face at its peak is the expression at full.</summary>
    [Fact]
    public void AFaceChannelTakesAnEnvelope()
    {
        var comp = new MotionToolkit().Composition(Opts(("width", 400d), ("height", 400d), ("duration", 2d)));
        Dictionary<string, object?> Face(object? joy) => comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("channels", Opts(("joy", joy))))).HeadAt(0.4);
        var hit = Face(N.Envelope(Opts(("at", 0.4d))));
        Assert.Equal(Corner(Face(1d)), Corner(hit));
        Assert.NotEqual(Corner(Face(0d)), Corner(hit));

        static string Corner(Dictionary<string, object?> head)
        {
            var p = (IDictionary<string, object?>)((IDictionary<string, object?>)head["mouthGuides"]!)["rightCorner"]!;
            return $"{p["x"]:0.###},{p["y"]:0.###}";
        }
    }

    [Fact]
    public void ItIsWrittenAsAKeyOnEveryFrameItMovesIn()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 100d), ("height", 100d), ("fps", 10d), ("duration", 3d)));
        var e = motion.Nodes.Envelope(Opts(("at", 1d), ("attack", 0.2d), ("release", 0.3d), ("settle", 0.5d)));
        comp.Circle(Opts(("origin", new[] { 50d, 50d }), ("radius", motion.Nodes.Scale("real", e, 20d))));

        var sif = comp.ToSif();
        // Frames 8 to 18: from the attack's start to the settle's end, every one a linear key.
        Assert.Equal(11, sif.Split("<waypoint ").Length - 1);
        Assert.DoesNotContain("clamped", sif);
        Assert.Contains("<svg", comp.ToSvg());
    }

    private static Dictionary<string, object?> Opts(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
}
