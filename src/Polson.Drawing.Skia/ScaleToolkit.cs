namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Maps data values onto pixels: the numeric spine of a chart.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <c>Layout</c> answers where a panel goes; this answers where a <i>value</i> goes, which is the
/// one thing a chart cannot be drawn without and the one thing nothing else in the SDK supplies.
/// It draws nothing — the marks are ordinary <c>CanvasPath</c> and <c>ctx.fill</c> work, and the
/// axis labels are ordinary text. Keeping the arithmetic separate from the drawing is what lets the
/// same scale serve a bar chart on a canvas, a sparkline in SVG, and a measurement in a test.
/// </para>
/// <para>
/// The truthfulness rules of Manual 13 are encoded here rather than left to prose where that is
/// possible: <see cref="RadiusFor"/> takes the square root because area is what the eye reads, and
/// a chart that sizes a circle by its value directly overstates a fourfold difference as sixteenfold.
/// </para>
/// </remarks>
public class ScaleToolkit
{
    #region Methods
    /// <summary>
    /// A linear mapping from a data interval onto a pixel interval.
    /// </summary>
    /// <remarks>
    /// The range may run backwards — <c>rangeStart</c> greater than <c>rangeEnd</c> — which is the
    /// normal case for a vertical axis, where larger values sit at smaller y. That is the whole
    /// reason the range is two numbers rather than an origin and a length.
    /// </remarks>
    public LinearScale Linear(double domainStart, double domainEnd, double rangeStart, double rangeEnd) =>
        new(domainStart, domainEnd, rangeStart, rangeEnd);

    /// <summary>
    /// Evenly spaced bands for categories, with padding between them.
    /// </summary>
    /// <remarks>
    /// <paramref name="padding"/> is the share of each step given to the gap, <c>0</c> to <c>1</c>;
    /// Manual 13 puts bars at roughly 0.2–0.4. The band's own width comes back as
    /// <see cref="BandScale.Bandwidth"/> rather than being computed by the caller, because that
    /// division is where a hand-rolled bar chart usually drifts off its axis.
    /// </remarks>
    public BandScale Band(int count, double rangeStart, double rangeEnd, double padding = 0.2) =>
        new(count, rangeStart, rangeEnd, padding);

    /// <summary>
    /// Round numbers spanning an interval — what an axis should actually be labelled with.
    /// </summary>
    /// <remarks>
    /// Steps are 1, 2 or 5 times a power of ten, so ticks land on numbers a reader recognises
    /// (0, 50, 100) rather than on the arithmetic that produced them (0, 47.5, 95). The count is a
    /// target rather than a promise: honouring it exactly is what forces ugly steps.
    /// </remarks>
    public double[] Ticks(double min, double max, int count = 5)
    {
        if (count < 1) count = 1;
        if (!double.IsFinite(min) || !double.IsFinite(max)) return [];
        if (Math.Abs(max - min) < double.Epsilon) return [min];
        if (min > max) (min, max) = (max, min);

        var step = NiceStep((max - min) / count);
        if (step <= 0) return [min];

        var first = Math.Ceiling(min / step) * step;
        var ticks = new List<double>();
        for (var value = first; value <= max + step * 1e-4 && ticks.Count < MaxTicks; value += step)
        {
            // Re-round each step: accumulating a double repeatedly turns 0.3 into 0.30000004.
            ticks.Add(Round(value, step));
        }
        return [.. ticks];
    }

    /// <summary>
    /// Widens an interval outward to the round numbers a tick step would land on.
    /// </summary>
    /// <remarks>
    /// Use it on the axis bounds before building the scale, so the first and last tick sit exactly
    /// at the ends of the plot instead of floating somewhere inside it.
    /// </remarks>
    public Dictionary<string, object> Nice(double min, double max, int count = 5)
    {
        if (min > max) (min, max) = (max, min);
        if (Math.Abs(max - min) < double.Epsilon) return Bounds(min, max);

        var step = NiceStep((max - min) / Math.Max(1, count));
        if (step <= 0) return Bounds(min, max);

        return Bounds(Round(Math.Floor(min / step) * step, step), Round(Math.Ceiling(max / step) * step, step));
    }

    /// <summary>
    /// The radius that makes a circle's <b>area</b> proportional to its value.
    /// </summary>
    /// <remarks>
    /// The eye reads area, not radius. Setting radius proportional to value shows a value four times
    /// larger as sixteen times the ink, which is the commonest quantitative lie in an infographic —
    /// and it is invisible to whoever drew it, because the numbers going in were correct. This is
    /// the same rule behind Manual 13's "area ∝ value, so radius ∝ √value".
    /// </remarks>
    public double RadiusFor(double value, double maxValue, double maxRadius)
    {
        if (maxValue <= 0 || maxRadius <= 0) return 0;
        var share = Math.Clamp(value / maxValue, 0, double.MaxValue);
        return maxRadius * Math.Sqrt(share);
    }

    /// <summary>
    /// The smallest interval containing every number given, for a scale shared across panels.
    /// </summary>
    /// <remarks>
    /// Small multiples must share one scale — panels drawn to their own extents look comparable and
    /// are not, which is a lie told by the layout rather than by any single chart. Feed every
    /// series through this once and build one scale from the result.
    /// </remarks>
    public Dictionary<string, object> Extent(object values)
    {
        var numbers = Numbers(values).Where(double.IsFinite).ToArray();
        return numbers.Length == 0 ? Bounds(0, 0) : Bounds(numbers.Min(), numbers.Max());
    }
    #endregion

    #region Methods (private)
    private static Dictionary<string, object> Bounds(double min, double max) =>
        new() { ["min"] = min, ["max"] = max, ["span"] = max - min };

    /// <summary>Rounds a step to 1, 2 or 5 times a power of ten.</summary>
    private static double NiceStep(double rough)
    {
        if (rough <= 0 || !double.IsFinite(rough)) return 0;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var normalised = rough / magnitude;

        var factor = normalised switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };
        return factor * magnitude;
    }

    /// <summary>Rounds to the step's own precision, so ticks read as the numbers they are.</summary>
    private static double Round(double value, double step)
    {
        var decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(step)) + 1, 0, 6);
        return Math.Round(value, decimals);
    }

    /// <summary>A backstop, so a pathological domain cannot spin out an unbounded tick list.</summary>
    private const int MaxTicks = 1000;

    internal static double[] Numbers(object values)
    {
        if (values is IEnumerable items and not string)
        {
            var list = new List<double>();
            foreach (var item in items)
            {
                if (item is not null) list.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture));
            }
            return [.. list];
        }
        return values is null ? [] : [Convert.ToDouble(values, CultureInfo.InvariantCulture)];
    }
    #endregion
}

/// <summary>A linear mapping between a data interval and a pixel interval.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class LinearScale
{
    #region Constructors
    internal LinearScale(double domainStart, double domainEnd, double rangeStart, double rangeEnd)
    {
        DomainStart = domainStart;
        DomainEnd = domainEnd;
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
    }
    #endregion

    #region Properties
    public double DomainStart { get; }
    public double DomainEnd { get; }
    public double RangeStart { get; }
    public double RangeEnd { get; }

    /// <summary>Whether the domain starts at zero — the condition a bar or column chart requires.</summary>
    /// <remarks>
    /// A bar's length is its value, so a non-zero baseline makes the ink say something the data does
    /// not. Checking is cheap and the failure is silent, so this is here to be asserted on.
    /// </remarks>
    public bool IsZeroBased => Math.Abs(DomainStart) < double.Epsilon;
    #endregion

    #region Methods
    /// <summary>The pixel position of a value. Values outside the domain map outside the range.</summary>
    /// <remarks>
    /// Deliberately not clamped: a point falling off the plot is a fact about the data or the
    /// domain, and silently pinning it to the axis would hide exactly the outlier worth seeing.
    /// Use <see cref="Clamp"/> where a mark must stay inside.
    /// </remarks>
    public double Map(double value)
    {
        var span = DomainEnd - DomainStart;
        if (Math.Abs(span) < double.Epsilon) return RangeStart;
        return RangeStart + (value - DomainStart) / span * (RangeEnd - RangeStart);
    }

    /// <summary>The pixel position of a value, held inside the range.</summary>
    public double Clamp(double value)
    {
        var mapped = Map(value);
        var low = Math.Min(RangeStart, RangeEnd);
        var high = Math.Max(RangeStart, RangeEnd);
        return Math.Clamp(mapped, low, high);
    }

    /// <summary>The value at a pixel position — the inverse of <see cref="Map"/>.</summary>
    public double Invert(double position)
    {
        var span = RangeEnd - RangeStart;
        if (Math.Abs(span) < double.Epsilon) return DomainStart;
        return DomainStart + (position - RangeStart) / span * (DomainEnd - DomainStart);
    }

    /// <summary>Pixel distance between two values, always positive.</summary>
    /// <remarks>What a bar's length is: the gap between the baseline and the value.</remarks>
    public double Extent(double from, double to) => Math.Abs(Map(to) - Map(from));

    /// <summary>Round tick values across this scale's domain.</summary>
    public double[] Ticks(int count = 5) => new ScaleToolkit().Ticks(DomainStart, DomainEnd, count);
    #endregion
}

/// <summary>Evenly spaced bands for categorical positions.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class BandScale
{
    #region Constructors
    internal BandScale(int count, double rangeStart, double rangeEnd, double padding)
    {
        Count = Math.Max(0, count);
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
        Padding = Math.Clamp(padding, 0, 0.95);

        var span = rangeEnd - rangeStart;
        Step = Count == 0 ? 0 : span / Count;
        Bandwidth = Math.Abs(Step) * (1 - Padding);
    }
    #endregion

    #region Properties
    public int Count { get; }
    public double RangeStart { get; }
    public double RangeEnd { get; }
    public double Padding { get; }

    /// <summary>Distance from one band's start to the next, gap included.</summary>
    public double Step { get; }

    /// <summary>The drawn width of one band, always positive.</summary>
    public double Bandwidth { get; }
    #endregion

    #region Methods
    /// <summary>The start of band <paramref name="index"/>, with its share of the gap already taken.</summary>
    public double Map(int index)
    {
        if (Count == 0) return RangeStart;
        var slotStart = RangeStart + Step * index;
        var inset = (Math.Abs(Step) - Bandwidth) / 2;
        return Step >= 0 ? slotStart + inset : slotStart - Math.Abs(Step) + inset;
    }

    /// <summary>The centre of band <paramref name="index"/> — where its label belongs.</summary>
    public double Center(int index) => Map(index) + Bandwidth / 2;
    #endregion
}
