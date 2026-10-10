namespace Polson.Animation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using Polson.Drawing.Skia;

using SkiaSharp;

/// <summary>The value types a <see cref="MotionNode"/> can carry.</summary>
internal enum MotionType
{
    Real,
    Angle,
    Vector,
    Color
}

/// <summary>How a waypoint enters or leaves its neighbouring segment, as Synfig names it.</summary>
internal enum MotionEase
{
    /// <summary>Synfig's TCB spline, written <c>auto</c>.</summary>
    Auto,
    Constant,
    Linear,
    /// <summary>Ease in or out: the tangent is zero at the waypoint. Synfig writes <c>halt</c>.</summary>
    Halt,
    Clamped
}

/// <summary>Conversions between the model's component arrays and what a script reads and writes.</summary>
internal static class MotionTypes
{
    #region Methods
    public static string Name(MotionType type) => type switch
    {
        MotionType.Real => "real",
        MotionType.Angle => "angle",
        MotionType.Vector => "vector",
        _ => "color"
    };

    public static MotionType Parse(string? name, string who) => name?.Trim().ToLowerInvariant() switch
    {
        "real" or "number" => MotionType.Real,
        "angle" => MotionType.Angle,
        "vector" or "point" => MotionType.Vector,
        "color" or "colour" => MotionType.Color,
        _ => throw new ArgumentException($"{who}: '{name}' is not a value type. Use 'real', 'angle', 'vector' or 'color'.")
    };

    public static int Width(MotionType type) => type switch
    {
        MotionType.Vector => 2,
        MotionType.Color => 4,
        _ => 1
    };

    /// <summary>A script value read as one of the model's types; refuses what cannot be one.</summary>
    public static double[] Read(object? value, MotionType type, string who)
    {
        switch (type)
        {
            case MotionType.Real:
            case MotionType.Angle:
                return value switch
                {
                    double d => [d],
                    int i => [i],
                    float f => [f],
                    long l => [l],
                    _ => throw new ArgumentException($"{who} takes a number, not {Describe(value)}.")
                };

            case MotionType.Vector:
                if (Point(value) is { } p) return [p.X, p.Y];
                throw new ArgumentException($"{who} takes a point, [x, y] or {{ x, y }}, not {Describe(value)}.");

            default:
                if (value is string s && SkiaColorParser.TryParse(s, out var c))
                    return [c.Red / 255d, c.Green / 255d, c.Blue / 255d, c.Alpha / 255d];
                throw new ArgumentException($"{who} takes a CSS colour string, not {Describe(value)}.");
        }
    }

    /// <summary>A value as a script reads it back: a number, <c>{ x, y }</c>, or <c>#RRGGBBAA</c>.</summary>
    public static object ToJs(MotionType type, double[] v) => type switch
    {
        MotionType.Vector => new Dictionary<string, object?> { ["x"] = v[0], ["y"] = v[1] },
        MotionType.Color => ToSkColor(v) is var c ? $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}{c.Alpha:X2}" : "",
        _ => v[0]
    };

    public static SKColor ToSkColor(double[] v, double amount = 1d) =>
        new(Byte(v[0]), Byte(v[1]), Byte(v[2]), Byte(v[3] * amount));

    public static string Format(double v) => v.ToString("0.##########", CultureInfo.InvariantCulture);

    public static string Time(double seconds) => $"{Format(seconds)}s";

    private static byte Byte(double v) => (byte)Math.Clamp(Math.Round(v * 255d), 0d, 255d);

    private static (double X, double Y)? Point(object? value)
    {
        if (value is SKPoint sp) return (sp.X, sp.Y);
        if (value is IList list && list.Count == 2 && Num(list[0]) is { } lx && Num(list[1]) is { } ly) return (lx, ly);
        if (JsInterop.AsDict(value) is { } d && Num(d["x"]) is { } dx && Num(d["y"]) is { } dy) return (dx, dy);
        return null;
    }

    private static double? Num(object? v) => v switch
    {
        double d => d,
        int i => i,
        float f => f,
        long l => l,
        _ => null
    };

    private static string Describe(object? v) => v switch
    {
        null => "nothing",
        string s => $"the string '{s}'",
        MotionNode n => $"a {n.Type} node",
        _ => $"a {v.GetType().Name}"
    };
    #endregion
}

/// <summary>
/// A value that is a function of time: a constant, a list of waypoints, or a formula over other nodes.
/// </summary>
/// <remarks>
/// Synfig's <c>ValueNode</c>, ported for the model only: every layer parameter in a
/// <see cref="MotionComposition"/> is one of these, so a parameter can be keyed, computed, or shared with
/// another parameter. Nodes are immutable and hold no clock, so evaluating one never changes it.
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// </remarks>
public abstract class MotionNode
{
    #region Constructors
    private protected MotionNode(MotionType kind) => Kind = kind;
    #endregion

    #region Properties
    /// <summary>The value type: <c>real</c>, <c>angle</c>, <c>vector</c> or <c>color</c>.</summary>
    public string Type => MotionTypes.Name(Kind);

    internal MotionType Kind { get; }

    internal virtual IEnumerable<MotionNode> Children => [];
    #endregion

    #region Methods
    /// <summary>The node's value at a time in seconds.</summary>
    public object At(double time) => MotionTypes.ToJs(Kind, Evaluate(time));

    internal abstract double[] Evaluate(double time);

    /// <summary>The node's own element, without an id; children are written through <paramref name="sif"/>.</summary>
    internal abstract XElement ToSif(SifWriter sif);
    #endregion
}

/// <summary>A value that does not change.</summary>
/// <remarks>Exposed to the JavaScript sandbox. See <see cref="MotionNode"/>.</remarks>
public sealed class MotionConstant : MotionNode
{
    #region Constructors
    internal MotionConstant(MotionType kind, double[] value) : base(kind) => this.value = value;
    #endregion

    #region Fields
    private readonly double[] value;
    #endregion

    #region Methods
    internal override double[] Evaluate(double time) => value;

    internal override XElement ToSif(SifWriter sif) => SifWriter.Value(Kind, value);
    #endregion
}

/// <summary>A value keyed by waypoints, interpolated exactly as Synfig interpolates it.</summary>
/// <remarks>
/// Ported from <c>valuenode_animatedinterface.cpp</c> (Synfig, GPL-2-or-later): two Hermite curves per
/// segment, one mapping time through temporal tension and one mapping value, with Synfig's rules for
/// each waypoint's tangent — <c>halt</c> zeroes it, <c>linear</c> points it at the neighbour,
/// <c>clamped</c> refuses to overshoot, <c>auto</c> is a TCB spline — and its rescaling of a tangent
/// across segments of unequal length. Exposed to the JavaScript sandbox. See <see cref="MotionNode"/>.
/// </remarks>
public sealed class MotionAnimated : MotionNode
{
    #region Constructors
    internal MotionAnimated(MotionType kind, IReadOnlyList<MotionWaypoint> waypoints) : base(kind)
    {
        if (waypoints.Count == 0)
            throw new ArgumentException("An animated value needs at least one waypoint.");

        this.waypoints = [.. waypoints.OrderBy(w => w.Time)];
        for (var i = 1; i < this.waypoints.Length; i++)
        {
            if (Math.Abs(this.waypoints[i].Time - this.waypoints[i - 1].Time) < TimeEpsilon)
                throw new ArgumentException($"Two waypoints at {MotionTypes.Time(this.waypoints[i].Time)}; one value per time.");
        }

        segments = Build(this.waypoints);
    }
    #endregion

    #region Fields
    /// <summary>Synfig compares times to half a millisecond.</summary>
    internal const double TimeEpsilon = 0.0005;

    private readonly MotionWaypoint[] waypoints;
    private readonly Segment[] segments;
    #endregion

    #region Properties
    /// <summary>How many waypoints it holds.</summary>
    public int Count => waypoints.Length;

    /// <summary>When the first waypoint is, in seconds.</summary>
    public double Start => waypoints[0].Time;

    /// <summary>When the last waypoint is, in seconds.</summary>
    public double End => waypoints[^1].Time;
    #endregion

    #region Methods
    internal override double[] Evaluate(double time) => Search(time);

    internal override XElement ToSif(SifWriter sif) =>
        new("animated", new XAttribute("type", Type),
            waypoints.Select(w => new XElement("waypoint",
                new XAttribute("time", MotionTypes.Time(w.Time)),
                new XAttribute("before", SifName(w.Before)),
                new XAttribute("after", SifName(w.After)),
                w.Tension != 0 ? new XAttribute("tension", MotionTypes.Format(w.Tension)) : null,
                w.Continuity != 0 ? new XAttribute("continuity", MotionTypes.Format(w.Continuity)) : null,
                w.Bias != 0 ? new XAttribute("bias", MotionTypes.Format(w.Bias)) : null,
                w.TemporalTension != 0 ? new XAttribute("temporal-tension", MotionTypes.Format(w.TemporalTension)) : null,
                SifWriter.Value(Kind, w.Value))));

    internal static string SifName(MotionEase ease) => ease switch
    {
        MotionEase.Auto => "auto",
        MotionEase.Constant => "constant",
        MotionEase.Linear => "linear",
        MotionEase.Halt => "halt",
        _ => "clamped"
    };

    /// <summary>
    /// One component of this value as SMIL keys and <c>keySplines</c>, exactly, or false where SMIL cannot
    /// say it: a temporal tension (a warped clock), or a segment whose curve leaves the range between its
    /// own two keys (an overshoot, which a keySpline's control points cannot reach).
    /// </summary>
    /// <remarks>
    /// A segment's value is a cubic in its own normalised time, so with control x at ⅓ and ⅔ a keySpline
    /// is that cubic exactly: <c>y1 = T1 / 3Δ</c>, <c>y2 = 1 − T2 / 3Δ</c>. A constant segment holds until
    /// Synfig's time epsilon before the next key and jumps there.
    /// </remarks>
    internal bool TryKeySplines(int component, out List<(double Time, double Value)> keys, out List<string> splines)
    {
        keys = [(waypoints[0].Time, waypoints[0].Value[component])];
        splines = [];
        const string straight = "0 0 1 1";

        for (var i = 0; i < segments.Length; i++)
        {
            var s = segments[i];
            if (waypoints[i].TemporalTension != 0 || waypoints[i + 1].TemporalTension != 0) return false;

            var p1 = s.P1[component];
            var p2 = s.P2[component];
            var constant = waypoints[i].After == MotionEase.Constant || waypoints[i + 1].Before == MotionEase.Constant;
            if (constant)
            {
                keys.Add((s.S - TimeEpsilon, p1));
                splines.Add(straight);
                keys.Add((s.S, waypoints[i + 1].Value[component]));
                splines.Add(straight);
                continue;
            }

            var delta = p2 - p1;
            var t1 = s.T1[component];
            var t2 = s.T2[component];
            if (Math.Abs(delta) < 1e-12)
            {
                if (Math.Abs(t1) > 1e-9 || Math.Abs(t2) > 1e-9) return false;
                splines.Add(straight);
            }
            else
            {
                var y1 = t1 / (3 * delta);
                var y2 = 1 - t2 / (3 * delta);
                if (y1 < -1e-9 || y1 > 1 + 1e-9 || y2 < -1e-9 || y2 > 1 + 1e-9) return false;
                splines.Add($"{MotionSvgFormat.Number(1d / 3, 6)} {MotionSvgFormat.Number(Math.Clamp(y1, 0, 1), 6)} "
                    + $"{MotionSvgFormat.Number(2d / 3, 6)} {MotionSvgFormat.Number(Math.Clamp(y2, 0, 1), 6)}");
            }

            keys.Add((s.S, p2));
        }

        return true;
    }

    private double[] Search(double time)
    {
        if (waypoints.Length == 1 || time <= waypoints[0].Time) return waypoints[0].Value;
        if (time >= waypoints[^1].Time) return waypoints[^1].Value;

        // Synfig walks to the first segment whose end is later than t, comparing with its time epsilon.
        foreach (var s in segments)
        {
            if (!(time >= s.S - TimeEpsilon)) return s.Resolve(time);
        }

        return waypoints[^1].Value;
    }

    private static Segment[] Build(MotionWaypoint[] w)
    {
        var list = new List<Segment>();
        const double timeadjust = 0.5;

        for (var i = 0; i + 1 < w.Length; i++)
        {
            var iter = w[i];
            var next = w[i + 1];
            var hasAfterNext = i + 2 < w.Length;
            var first = i == 0;
            var c = new Segment(iter.Time, next.Time, iter.Value, next.Value);

            if (iter.After == MotionEase.Constant || next.Before == MotionEase.Constant)
            {
                c.P2 = iter.Value;
                c.T1 = c.T2 = Zero(iter.Value);
            }
            else
            {
                if (iter.After == MotionEase.Auto && !first)
                {
                    if (iter.Before != MotionEase.Auto && list.Count > 0)
                    {
                        c.T1 = list[^1].T2;
                    }
                    else
                    {
                        var (t, cn, b) = (iter.Tension, iter.Continuity, iter.Bias);
                        var pp = list[^1].P1;
                        c.T1 = Add(
                            Mul(Sub(c.P1, pp), (1 - t) * (1 + cn) * (1 + b) / 2),
                            Mul(Sub(c.P2, c.P1), (1 - t) * (1 - cn) * (1 - b) / 2));
                    }
                }
                else if (iter.After is MotionEase.Linear or MotionEase.Halt
                    || (iter.After is MotionEase.Auto or MotionEase.Clamped && first))
                {
                    c.T1 = Sub(c.P2, c.P1);
                }

                if (iter.After == MotionEase.Clamped && !first)
                    c.T1 = Clamped(list[^1].P1, c.P1, c.P2, list[^1].R, iter.Time, next.Time);

                if (iter.Before == MotionEase.Auto && iter.After != MotionEase.Auto && list.Count > 0)
                    list[^1].T2 = c.T1;

                if (next.Before == MotionEase.Auto && hasAfterNext)
                {
                    var (t, cn, b) = (next.Tension, next.Continuity, next.Bias);
                    var pn = w[i + 2].Value;
                    c.T2 = Add(
                        Mul(Sub(c.P2, c.P1), (1 - t) * (1 - cn) * (1 + b) / 2),
                        Mul(Sub(pn, c.P2), (1 - t) * (1 + cn) * (1 - b) / 2));
                }
                else if (next.Before is MotionEase.Linear or MotionEase.Halt
                    || (next.Before is MotionEase.Auto or MotionEase.Clamped && !hasAfterNext))
                {
                    c.T2 = Sub(c.P2, c.P1);
                }

                if (next.Before == MotionEase.Clamped && hasAfterNext)
                    c.T2 = Clamped(c.P1, c.P2, w[i + 2].Value, iter.Time, next.Time, w[i + 2].Time);

                var dt = next.Time - iter.Time;
                if (iter.After == MotionEase.Halt)
                    c.T1 = Zero(c.T1);
                else if (iter.After != MotionEase.Linear && list.Count > 0)
                    c.T1 = Mul(c.T1, dt * (timeadjust + 1) / (dt * timeadjust + (list[^1].S - list[^1].R)));

                if (next.Before == MotionEase.Halt)
                    c.T2 = Zero(c.T2);
                else if (next.Before != MotionEase.Linear && hasAfterNext)
                    c.T2 = Mul(c.T2, dt * (timeadjust + 1) / (dt * timeadjust + (w[i + 2].Time - next.Time)));
            }

            var span = next.Time - iter.Time;
            c.TimeT1 = span * (1 - iter.TemporalTension);
            c.TimeT2 = span * (1 - next.TemporalTension);
            list.Add(c);
        }

        return [.. list];
    }

    /// <summary>Synfig's clamped tangent, per component: no overshoot past either neighbour.</summary>
    private static double[] Clamped(double[] p1, double[] p2, double[] p3, double t1, double t2, double t3)
    {
        var r = new double[p1.Length];
        for (var k = 0; k < r.Length; k++) r[k] = Clamped(p1[k], p2[k], p3[k], t1, t2, t3);
        return r;
    }

    private static double Clamped(double p1, double p2, double p3, double t1, double t2, double t3)
    {
        var pm = p1 + (p3 - p1) * (t2 - t1) / (t3 - t1);
        double bias;

        if (p3 > p1)
        {
            if (p2 >= p3 || p2 <= p1) return 0;
            bias = p2 > pm ? (pm - p2) / (p3 - pm) : p2 < pm ? (pm - p2) / (pm - p1) : 0;
        }
        else if (p1 > p3)
        {
            if (p2 >= p1 || p2 <= p3) return 0;
            bias = p2 > pm ? (pm - p2) / (pm - p1) : p2 < pm ? (pm - p2) / (p3 - pm) : 0;
        }
        else
        {
            return 0;
        }

        return (p2 - p1) * (1 + bias) / 2 + (p3 - p2) * (1 - bias) / 2;
    }

    private static double[] Zero(double[] a) => new double[a.Length];

    private static double[] Add(double[] a, double[] b) => [.. a.Select((v, i) => v + b[i])];

    private static double[] Sub(double[] a, double[] b) => [.. a.Select((v, i) => v - b[i])];

    private static double[] Mul(double[] a, double k) => [.. a.Select(v => v * k)];
    #endregion

    #region Types
    /// <summary>One segment: a Hermite in time feeding a Hermite in value, as Synfig builds them.</summary>
    private sealed class Segment(double r, double s, double[] p1, double[] p2)
    {
        public double R = r, S = s;
        public double[] P1 = p1, P2 = p2;
        public double[] T1 = new double[p1.Length], T2 = new double[p1.Length];
        public double TimeT1, TimeT2;

        public double[] Resolve(double time)
        {
            var warped = Bezier(R, R + TimeT1 / 3, S - TimeT2 / 3, S, (time - R) / (S - R));
            var u = (warped - R) / (S - R);
            var v = new double[P1.Length];
            for (var k = 0; k < v.Length; k++)
                v[k] = Bezier(P1[k], P1[k] + T1[k] / 3, P2[k] - T2[k] / 3, P2[k], u);
            return v;
        }

        private static double Bezier(double a, double b, double c, double d, double t)
        {
            double Lerp(double x, double y) => x + (y - x) * t;
            return Lerp(Lerp(Lerp(a, b), Lerp(b, c)), Lerp(Lerp(b, c), Lerp(c, d)));
        }
    }
    #endregion
}

/// <summary>One key of a <see cref="MotionAnimated"/>.</summary>
internal sealed record MotionWaypoint(
    double Time, double[] Value, MotionEase Before, MotionEase After,
    double Tension, double Continuity, double Bias, double TemporalTension);

/// <summary><c>offset + slope × t</c>, with either term itself a node.</summary>
/// <remarks>Synfig's <c>linear</c> converter. Exposed to the JavaScript sandbox. See <see cref="MotionNode"/>.</remarks>
public sealed class MotionLinear : MotionNode
{
    #region Constructors
    internal MotionLinear(MotionType kind, MotionNode slope, MotionNode offset) : base(kind) =>
        (this.slope, this.offset) = (slope, offset);
    #endregion

    #region Fields
    private readonly MotionNode slope, offset;
    #endregion

    /// <summary>Whether both terms are constants, so the value is a straight line in time.</summary>
    internal bool IsStraight => slope is MotionConstant && offset is MotionConstant;

    #region Properties
    internal override IEnumerable<MotionNode> Children => [slope, offset];
    #endregion

    #region Methods
    internal override double[] Evaluate(double time)
    {
        var m = slope.Evaluate(time);
        var b = offset.Evaluate(time);
        return [.. m.Select((v, i) => v * time + b[i])];
    }

    internal override XElement ToSif(SifWriter sif) =>
        sif.Linkable("linear", Type, ("slope", slope), ("offset", offset));
    #endregion
}

/// <summary><c>amp × sin(angle)</c>.</summary>
/// <remarks>Synfig's <c>sine</c> converter. Exposed to the JavaScript sandbox. See <see cref="MotionNode"/>.</remarks>
public sealed class MotionSine : MotionNode
{
    #region Constructors
    internal MotionSine(MotionNode angle, MotionNode amp) : base(MotionType.Real) =>
        (this.angle, this.amp) = (angle, amp);
    #endregion

    #region Fields
    private readonly MotionNode angle, amp;
    #endregion

    #region Properties
    internal override IEnumerable<MotionNode> Children => [angle, amp];
    #endregion

    #region Methods
    internal override double[] Evaluate(double time) =>
        [amp.Evaluate(time)[0] * Math.Sin(angle.Evaluate(time)[0] * Math.PI / 180d)];

    internal override XElement ToSif(SifWriter sif) =>
        sif.Linkable("sine", Type, ("angle", angle), ("amp", amp));
    #endregion
}

/// <summary>A point built from two numbers, each its own node.</summary>
/// <remarks>Synfig's <c>composite</c> converter, for vectors. Exposed to the JavaScript sandbox. See <see cref="MotionNode"/>.</remarks>
public sealed class MotionComposite : MotionNode
{
    #region Constructors
    internal MotionComposite(MotionNode x, MotionNode y) : base(MotionType.Vector) => (this.x, this.y) = (x, y);
    #endregion

    #region Fields
    private readonly MotionNode x, y;

    internal MotionNode X => x;

    internal MotionNode Y => y;
    #endregion

    #region Properties
    internal override IEnumerable<MotionNode> Children => [x, y];
    #endregion

    #region Methods
    internal override double[] Evaluate(double time) => [x.Evaluate(time)[0], y.Evaluate(time)[0]];

    internal override XElement ToSif(SifWriter sif) =>
        sif.Linkable("composite", Type, ("x", x), ("y", y));
    #endregion
}

/// <summary><c>(lhs + rhs) × scalar</c>: a keyed offset laid on a formula, or two motions summed.</summary>
/// <remarks>Synfig's <c>add</c> converter. Exposed to the JavaScript sandbox. See <see cref="MotionNode"/>.</remarks>
public sealed class MotionAdd : MotionNode
{
    #region Constructors
    internal MotionAdd(MotionType kind, MotionNode lhs, MotionNode rhs, MotionNode scalar) : base(kind) =>
        (this.lhs, this.rhs, this.scalar) = (lhs, rhs, scalar);
    #endregion

    #region Fields
    private readonly MotionNode lhs, rhs, scalar;
    #endregion

    #region Properties
    internal override IEnumerable<MotionNode> Children => [lhs, rhs, scalar];
    #endregion

    #region Methods
    internal override double[] Evaluate(double time)
    {
        var a = lhs.Evaluate(time);
        var b = rhs.Evaluate(time);
        var s = scalar.Evaluate(time)[0];
        return [.. a.Select((v, i) => (v + b[i]) * s)];
    }

    internal override XElement ToSif(SifWriter sif) =>
        sif.Linkable("add", Type, ("lhs", lhs), ("rhs", rhs), ("scalar", scalar));
    #endregion
}

/// <summary><c>link × scalar</c>. On a colour only red, green and blue are scaled, as in Synfig.</summary>
/// <remarks>Synfig's <c>scale</c> converter. Exposed to the JavaScript sandbox. See <see cref="MotionNode"/>.</remarks>
public sealed class MotionScale : MotionNode
{
    #region Constructors
    internal MotionScale(MotionType kind, MotionNode link, MotionNode scalar) : base(kind) =>
        (this.link, this.scalar) = (link, scalar);
    #endregion

    #region Fields
    private readonly MotionNode link, scalar;
    #endregion

    #region Properties
    internal override IEnumerable<MotionNode> Children => [link, scalar];
    #endregion

    #region Methods
    internal override double[] Evaluate(double time)
    {
        var v = link.Evaluate(time);
        var s = scalar.Evaluate(time)[0];
        return [.. v.Select((x, i) => Kind == MotionType.Color && i == 3 ? x : x * s)];
    }

    internal override XElement ToSif(SifWriter sif) =>
        sif.Linkable("scale", Type, ("link", link), ("scalar", scalar));
    #endregion
}

/// <summary>
/// The weighted average of several values: <c>Σ wᵢ·vᵢ / Σ wᵢ</c>, each value and each weight its own node. With the
/// weights summing to zero it is the plain average.
/// </summary>
/// <remarks>
/// Synfig's <c>weighted_average</c> (<c>valuenode_weightedaverage.cpp</c>, <c>ValueAverage::average_generic</c>; Synfig,
/// GPL-2-or-later): every component of the value is averaged, a colour's alpha with the rest. Exposed to the JavaScript
/// sandbox. See <see cref="MotionNode"/>.
/// </remarks>
public sealed class MotionWeightedAverage : MotionNode
{
    #region Constructors
    internal MotionWeightedAverage(MotionType kind, IReadOnlyList<(MotionNode Value, MotionNode Weight)> items) : base(kind)
    {
        if (items.Count == 0) throw new ArgumentException("Motion.nodes.weightedAverage needs at least one value.");
        this.items = items;
    }
    #endregion

    #region Fields
    private readonly IReadOnlyList<(MotionNode Value, MotionNode Weight)> items;
    #endregion

    #region Properties
    internal override IEnumerable<MotionNode> Children => items.SelectMany(i => new[] { i.Value, i.Weight });
    #endregion

    #region Methods
    internal override double[] Evaluate(double time)
    {
        var weights = items.Select(i => i.Weight.Evaluate(time)[0]).ToArray();
        var total = weights.Sum();
        var plain = total == 0d;
        var scale = plain ? 1d / items.Count : 1d / total;
        var sum = new double[MotionTypes.Width(Kind)];
        for (var i = 0; i < items.Count; i++)
        {
            var v = items[i].Value.Evaluate(time);
            var w = (plain ? 1d : weights[i]) * scale;
            for (var c = 0; c < sum.Length; c++) sum[c] += v[c] * w;
        }
        return sum;
    }

    internal override XElement ToSif(SifWriter sif) =>
        new("weighted_average", new XAttribute("type", "weighted_" + Type),
            items.Select(i => new XElement("entry", sif.Linkable("composite", "weighted_" + Type, ("weight", i.Weight), ("value", i.Value)))));
    #endregion
}

/// <summary>Builds nodes from script values: <c>Motion.nodes</c>.</summary>
/// <remarks>
/// Every factory takes the value type first where it is ambiguous, and refuses a value of the wrong
/// type by name rather than coercing it. Exposed to the JavaScript sandbox. Members follow .NET naming
/// here; Jint resolves the JS camelCase spelling onto them.
/// </remarks>
public sealed class MotionNodeFactory
{
    #region Fields
    private static readonly HashSet<string> WaypointKeys =
        ["time", "value", "before", "after", "ease", "tension", "continuity", "bias", "temporalTension"];
    #endregion

    #region Methods
    /// <summary>A value that does not change.</summary>
    public MotionConstant Constant(string type, object? value)
    {
        var kind = MotionTypes.Parse(type, "Motion.nodes.constant");
        return new MotionConstant(kind, MotionTypes.Read(value, kind, $"Motion.nodes.constant('{type}', ...)"));
    }

    /// <summary>
    /// A value keyed by waypoints: <c>[{ time, value, ease?, before?, after?, tension?, ... }]</c>.
    /// </summary>
    /// <remarks>
    /// <c>ease</c> sets both sides at once; <c>before</c> and <c>after</c> set one. The names are Synfig's:
    /// <c>clamped</c> (the default, no overshoot), <c>halt</c> (or <c>ease</c>; the value stops at the key),
    /// <c>linear</c>, <c>constant</c> (a hold), and <c>auto</c> (a TCB spline, which can overshoot).
    /// </remarks>
    public MotionAnimated Animated(string type, object? waypoints)
    {
        var kind = MotionTypes.Parse(type, "Motion.nodes.animated");
        if (waypoints is not IList list || list.Count == 0)
            throw new ArgumentException("Motion.nodes.animated(type, waypoints) takes a non-empty array of { time, value }.");

        var keys = new List<MotionWaypoint>();
        foreach (var item in list)
        {
            var d = JsInterop.AsDict(item)
                ?? throw new ArgumentException("Each waypoint is an object: { time, value, ease? }.");
            foreach (var key in d.Keys.Cast<object>().Select(k => k.ToString()!))
            {
                if (!WaypointKeys.Contains(key))
                    throw new ArgumentException($"A waypoint has no '{key}'. Waypoint keys are: {string.Join(", ", WaypointKeys)}.");
            }

            var time = Real(d["time"], "a waypoint's time (seconds)");
            var who = $"The waypoint at {MotionTypes.Time(time)}";
            var ease = d["ease"] is { } e ? Ease(e, who) : MotionEase.Clamped;
            keys.Add(new MotionWaypoint(
                time,
                MotionTypes.Read(d["value"], kind, $"{who}'s value"),
                d["before"] is { } b ? Ease(b, who) : ease,
                d["after"] is { } a ? Ease(a, who) : ease,
                d["tension"] is { } t ? Real(t, $"{who}'s tension") : 0,
                d["continuity"] is { } c ? Real(c, $"{who}'s continuity") : 0,
                d["bias"] is { } bi ? Real(bi, $"{who}'s bias") : 0,
                d["temporalTension"] is { } tt ? Real(tt, $"{who}'s temporalTension") : 0));
        }

        return new MotionAnimated(kind, keys);
    }

    /// <summary><c>offset + slope × t</c>, t in seconds. Real, angle or vector.</summary>
    public MotionLinear Linear(string type, object? slope, object? offset = null)
    {
        var kind = MotionTypes.Parse(type, "Motion.nodes.linear");
        if (kind == MotionType.Color)
            throw new ArgumentException("Motion.nodes.linear(...) takes 'real', 'angle' or 'vector'.");
        return new MotionLinear(kind,
            Node(slope, kind, "linear's slope"),
            Node(offset ?? (kind == MotionType.Vector ? new[] { 0d, 0d } : 0d), kind, "linear's offset"));
    }

    /// <summary><c>amp × sin(angle)</c>, the angle in degrees.</summary>
    public MotionSine Sine(object? angle, object? amp = null) =>
        new(Node(angle, MotionType.Angle, "sine's angle"), Node(amp ?? 1d, MotionType.Real, "sine's amp"));

    /// <summary>A point from two numbers or two real nodes.</summary>
    public MotionComposite Composite(object? x, object? y) =>
        new(Node(x, MotionType.Real, "composite's x"), Node(y, MotionType.Real, "composite's y"));

    /// <summary><c>(lhs + rhs) × scalar</c>, the scalar defaulting to 1. Any value type.</summary>
    public MotionAdd Add(string type, object? lhs, object? rhs, object? scalar = null)
    {
        var kind = MotionTypes.Parse(type, "Motion.nodes.add");
        return new MotionAdd(kind, Node(lhs, kind, "add's lhs"), Node(rhs, kind, "add's rhs"),
            Node(scalar ?? 1d, MotionType.Real, "add's scalar"));
    }

    /// <summary><c>link × scalar</c>. Any value type; a colour keeps its alpha.</summary>
    public MotionScale Scale(string type, object? link, object? scalar)
    {
        var kind = MotionTypes.Parse(type, "Motion.nodes.scale");
        return new MotionScale(kind, Node(link, kind, "scale's link"), Node(scalar, MotionType.Real, "scale's scalar"));
    }

    /// <summary>
    /// The weighted average of several values, each a value or a node, each with a weight that may be a node:
    /// <c>[[value, weight], ...]</c> or <c>[{ value, weight }, ...]</c>. Any value type.
    /// </summary>
    public MotionWeightedAverage WeightedAverage(string type, object? items)
    {
        var kind = MotionTypes.Parse(type, "Motion.nodes.weightedAverage");
        if (items is not IList list || list.Count == 0)
            throw new ArgumentException("Motion.nodes.weightedAverage(type, items) takes a non-empty array of [value, weight] or { value, weight }.");
        var pairs = new List<(MotionNode, MotionNode)>();
        for (var i = 0; i < list.Count; i++)
        {
            var who = $"weightedAverage's item {i}";
            (object? value, object? weight) = list[i] switch
            {
                IList pair when pair.Count == 2 => (pair[0], pair[1]),
                var item when JsInterop.AsDict(item) is { } d => (d["value"], d["weight"]),
                _ => throw new ArgumentException($"{who} is [value, weight] or {{ value, weight }}.")
            };
            if (weight is null) throw new ArgumentException($"{who} has no weight.");
            pairs.Add((Node(value, kind, $"{who}'s value"), Node(weight, MotionType.Real, $"{who}'s weight")));
        }
        return new MotionWeightedAverage(kind, pairs);
    }

    /// <summary>
    /// A point that follows a bone: given where it sits in the rest pose, in composition coordinates, it is
    /// carried by the bone's frame from there. Synfig's bone link.
    /// </summary>
    public MotionBoneNode BoneLink(MotionBone? bone, object? point) =>
        bone is null
            ? throw new ArgumentException("Motion.nodes.boneLink(bone, point) takes a bone made by composition.bone(...).")
            : new MotionBoneNode(MotionBinding.Parse(bone, "boneLink"), RestPoint(point, "boneLink"));

    /// <summary>
    /// A point carried by several bones at once, each by its weight: <c>[[bone, weight], ...]</c> or
    /// <c>[{ bone, weight }, ...]</c>. Synfig's bone influence, which is linear blend skinning.
    /// </summary>
    public MotionBoneNode BoneInfluence(object? weights, object? point) =>
        new(MotionBinding.Parse(weights, "boneInfluence"), RestPoint(point, "boneInfluence"));

    private static double[] RestPoint(object? point, string who) => point is MotionNode
        ? throw new ArgumentException($"Motion.nodes.{who}'s point is fixed where it sits in the rest pose; the bones move it, so it is a value, not a node.")
        : MotionTypes.Read(point, MotionType.Vector, $"Motion.nodes.{who}'s point");

    /// <summary>A script value as a node of the given type: a node passes through if its type matches.</summary>
    internal static MotionNode Node(object? value, MotionType kind, string who) => value switch
    {
        MotionNode n when n.Kind == kind => n,
        MotionNode n => throw new ArgumentException($"{who} takes a {MotionTypes.Name(kind)}, and this node is a {n.Type}."),
        _ => new MotionConstant(kind, MotionTypes.Read(value, kind, who))
    };

    private static double Real(object? v, string who) => MotionTypes.Read(v, MotionType.Real, who)[0];

    private static MotionEase Ease(object value, string who) => value.ToString()?.Trim().ToLowerInvariant() switch
    {
        "auto" or "tcb" => MotionEase.Auto,
        "constant" or "hold" => MotionEase.Constant,
        "linear" => MotionEase.Linear,
        "halt" or "ease" => MotionEase.Halt,
        "clamped" => MotionEase.Clamped,
        var s => throw new ArgumentException(
            $"{who}: '{s}' is not an interpolation. Use 'clamped', 'halt' (or 'ease'), 'linear', 'constant' or 'auto'.")
    };
    #endregion
}
