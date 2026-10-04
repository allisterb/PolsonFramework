namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Polson.Drawing.Svg;

using SkiaSharp;

/// <summary>
/// Options for one entry on a <see cref="MotionTimeline"/>.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script writing <c>{ dur: 400 }</c> reaches <see cref="Dur"/>.
/// <para>
/// <b>A typed class rather than a dictionary, and that is load-bearing.</b> A function that is a
/// property of a JS object arrives in .NET as a Jint delegate type, which this assembly must not
/// name; declaring the shape lets Jint convert at the boundary, so <c>easing</c> arrives as a plain
/// <see cref="Func{T, TResult}"/>. Measured 2026-09-05 — see <c>docs/motion-score-api.md</c> §6.1.
/// </para>
/// <para>
/// The cost is that an <b>unknown property is ignored silently</b>: <c>{ durr: 400 }</c> binds and
/// does nothing. Nothing in the binding can report it, so the calls validate what they can and the
/// SDK reference says so.
/// </para>
/// </remarks>
public sealed class MotionEntryOptions
{
    #region Properties
    /// <summary>Where the entry starts: a number of milliseconds, or the position grammar as a string.</summary>
    public object? At { get; set; }

    /// <summary>How long it runs, in milliseconds.</summary>
    public double? Dur { get; set; }

    /// <summary>The easing, a pure function of 0..1. Defaults to linear.</summary>
    public Func<double, double>? Easing { get; set; }

    /// <summary>For <c>stagger</c>: milliseconds between one target and the next.</summary>
    public double? Each { get; set; }

    /// <summary>For <c>show</c>: when the target becomes visible.</summary>
    public double? From { get; set; }

    /// <summary>For <c>show</c>: when it stops being visible.</summary>
    public double? To { get; set; }
    #endregion
}

/// <summary>Defaults a whole timeline starts with.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox; a typed class for the same reason as
/// <see cref="MotionEntryOptions"/> — a dictionary could not carry <see cref="Easing"/>.
/// </remarks>
public sealed class MotionTimelineDefaults
{
    #region Properties
    /// <summary>How long an entry runs when it does not say, in milliseconds.</summary>
    public double? Dur { get; set; }

    /// <summary>How an entry eases when it does not say. Linear when unset.</summary>
    public Func<double, double>? Easing { get; set; }
    #endregion
}

/// <summary>Options for <c>Motion.timeline(...)</c>.</summary>
/// <remarks>Exposed to the JavaScript sandbox. See <see cref="MotionEntryOptions"/>.</remarks>
public sealed class MotionTimelineOptions
{
    #region Properties
    /// <summary>Defaults every entry inherits unless it overrides them.</summary>
    public MotionTimelineDefaults? Defaults { get; set; }
    #endregion
}

/// <summary>
/// A seekable score: entries placed on a timeline, each a pure function of the time asked for.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox as <c>tl</c>, from <c>Motion.timeline()</c>. Members follow
/// .NET naming here; Jint resolves the JS camelCase spelling onto them.
/// <para>
/// <b>The property the whole design rests on: every entry answers for any <c>t</c>, including
/// outside its own window</b> — before its start it applies its <i>from</i> value, after its end it
/// holds its <i>to</i> value. That is what makes <see cref="Seek"/> absolute rather than incremental,
/// so frames can be rendered in any order, re-rendered identically, or resumed. A timeline that only
/// stepped forward could not be previewed, resumed or parallelised.
/// </para>
/// <para>
/// Two consequences worth knowing. Anything mutated <b>outside</b> the score does not revert on a
/// backwards seek — either put every state change in the score, or rebuild the scene per frame.
/// And <see cref="To"/> captures its base value <b>when it is added</b>, not when it first runs,
/// which is the only deterministic choice available to a seekable score.
/// </para>
/// <para>
/// It holds no clock and does not reference the script engine: setters are plain delegates, so the
/// whole class is testable with no JavaScript at all.
/// </para>
/// </remarks>
public sealed class MotionTimeline
{
    #region Constructors
    public MotionTimeline(double defaultDuration = 500d, Func<double, double>? defaultEasing = null)
    {
        this.defaultDuration = defaultDuration > 0d ? defaultDuration
            : throw new ArgumentException("A timeline's default duration must be positive.", nameof(defaultDuration));
        this.defaultEasing = defaultEasing ?? Linear;
    }
    #endregion

    #region Fields
    private readonly List<Entry> entries = [];
    private readonly Dictionary<string, double> labels = new(StringComparer.Ordinal);
    private readonly double defaultDuration;
    private readonly Func<double, double> defaultEasing;

    private double previousStart;
    private double previousEnd;
    private bool hasPrevious;
    #endregion

    #region Properties
    /// <summary>The end of the last entry to finish — the max of every <c>at + dur</c>, not their sum.</summary>
    public double Duration => entries.Count == 0 ? 0d : entries.Max(e => e.At + e.Dur);

    /// <summary>How many entries the score holds.</summary>
    public int Count => entries.Count;

    /// <summary>Every label and the millisecond it names.</summary>
    public IDictionary<string, object?> Labels =>
        labels.ToDictionary(p => p.Key, p => (object?)p.Value, StringComparer.Ordinal);
    #endregion

    #region Methods
    /// <summary>
    /// A scalar tween: the setter is called with the eased value, every seek.
    /// </summary>
    /// <remarks>
    /// The case SMIL cannot express — procedural geometry recomputed per frame, rather than an
    /// attribute the renderer knows how to interpolate.
    /// </remarks>
    public MotionTimeline Tween(double from, double to, Action<double>? setter, MotionEntryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(setter, nameof(setter));

        var (at, dur, ease) = Place(options);
        Add(new Entry(at, dur, ease, s => setter(from + (to - from) * s)));
        return this;
    }

    /// <summary>Tweens attributes on an element from their current values to the ones given.</summary>
    public MotionTimeline To(SnapElement? target, object? attributes, MotionEntryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(target, nameof(target));

        var (at, dur, ease) = Place(options);
        Add(BuildAttributeEntry(target, attributes, at, dur, ease, nameof(To)));
        return this;
    }

    /// <summary>
    /// A step: the base value before <c>at</c>, the given value at and after it.
    /// </summary>
    /// <remarks>
    /// Zero duration, so it needs no interpolation and accepts values that could never be tweened —
    /// a font name, a dash pattern, a gradient reference.
    /// </remarks>
    public MotionTimeline Set(SnapElement? target, object? attributes, MotionEntryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(target, nameof(target));

        var (at, _, _) = Place(options);
        var pairs = ReadAttributes(attributes, nameof(Set));
        var steps = pairs.Select(p => (p.Name, Base: target.Attr(p.Name), p.Value)).ToArray();

        Add(new Entry(at, 0d, Linear, s =>
        {
            foreach (var (name, baseValue, value) in steps)
            {
                target.Attr(name, s >= 1d ? value : baseValue);
            }
        }));

        return this;
    }

    /// <summary>
    /// A visibility window: hidden outside it, shown inside, in both seek directions.
    /// </summary>
    /// <remarks>
    /// What a create-in-callback / remove-in-callback pair was doing before, except that this
    /// survives a backwards seek — an element removed by a callback is simply gone, and seeking back
    /// cannot return it.
    /// </remarks>
    public MotionTimeline Show(SnapElement? target, MotionEntryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(target, nameof(target));

        var from = options?.From ?? Resolve(options?.At);
        var to = options?.To ?? double.PositiveInfinity;

        if (to <= from)
        {
            throw new ArgumentException(
                $"show(...) needs `to` after `from`; got from={Format(from)} to={Format(to)}.", nameof(options));
        }

        var shown = target.Attr("display");
        var dur = double.IsPositiveInfinity(to) ? 0d : to - from;

        // Recorded as an entry starting at `from` so the position grammar can refer to it, but it
        // reads the seek time itself rather than a normalised 0..1 — a window is not a tween.
        Add(new EntryAtTime(from, dur, t =>
            target.Attr("display", t >= from && t < to ? shown ?? "inline" : "none")));

        return this;
    }

    /// <summary>One <see cref="To"/> per target, each offset by <c>each</c> milliseconds.</summary>
    public MotionTimeline Stagger(object? targets, object? attributes, MotionEntryOptions? options = null)
    {
        var list = AsElements(targets);
        if (list.Count == 0) return this;

        var each = options?.Each ?? 100d;
        var (start, dur, ease) = Place(options);

        for (var i = 0; i < list.Count; i++)
        {
            Add(BuildAttributeEntry(list[i], attributes, start + i * each, dur, ease, nameof(Stagger)));
        }

        return this;
    }

    /// <summary>Names a position, so entries can be placed relative to it.</summary>
    public MotionTimeline Label(string? name, object? at = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A label needs a name.", nameof(name));

        labels[name] = at is null ? Duration : Resolve(at);
        return this;
    }

    /// <summary>Applies every entry at <paramref name="ms"/>. Chainable, and order-independent.</summary>
    public MotionTimeline Seek(double ms)
    {
        foreach (var entry in entries) entry.Apply(ms);
        return this;
    }

    /// <summary>Alias for <see cref="Seek"/>, which reads better in a render loop.</summary>
    public MotionTimeline At(double ms) => Seek(ms);
    #endregion

    #region Methods (private)
    static double Linear(double n) => n;

    static string Format(double value) =>
        double.IsPositiveInfinity(value) ? "the end" : value.ToString("0.###", CultureInfo.InvariantCulture);

    void Add(Entry entry)
    {
        entries.Add(entry);
        previousStart = entry.At;
        previousEnd = entry.At + entry.Dur;
        hasPrevious = true;
    }

    /// <summary>Where an entry starts, how long it runs, and how it eases.</summary>
    (double At, double Dur, Func<double, double> Ease) Place(MotionEntryOptions? options)
    {
        var dur = options?.Dur ?? defaultDuration;
        if (dur < 0d || double.IsNaN(dur))
        {
            throw new ArgumentException($"A duration must be zero or more; got {Format(dur)}.", nameof(options));
        }

        return (Resolve(options?.At), dur, options?.Easing ?? defaultEasing);
    }

    /// <summary>
    /// The position grammar. Omitted means the end of the timeline — sequential append.
    /// </summary>
    /// <remarks>
    /// An unknown label <b>throws and names itself</b>. Resolving it to zero would place a beat at
    /// the start of the film and animate happily, which is the failure this project treats as worst:
    /// wrong, silent, and plausible.
    /// </remarks>
    double Resolve(object? at)
    {
        switch (at)
        {
            case null:
                return Duration;

            case string text when !string.IsNullOrWhiteSpace(text):
                return ResolveText(text.Trim());

            case string:
                throw new ArgumentException("A position cannot be an empty string.", nameof(at));

            default:
                var number = Convert.ToDouble(at, CultureInfo.InvariantCulture);
                if (double.IsNaN(number))
                    throw new ArgumentException("A position cannot be NaN.", nameof(at));
                return number;
        }
    }

    double ResolveText(string text)
    {
        // "<" and ">" anchor to the previous entry, with an optional offset: "<+=100", ">-=50".
        if (text[0] is '<' or '>')
        {
            var anchor = !hasPrevious ? 0d : text[0] == '<' ? previousStart : previousEnd;
            var rest = text[1..].Trim();
            return rest.Length == 0 ? anchor : anchor + Offset(rest, text);
        }

        if (text.StartsWith("+=", StringComparison.Ordinal) || text.StartsWith("-=", StringComparison.Ordinal))
        {
            return Duration + Offset(text, text);
        }

        // A label, optionally offset: "chartIn+=200".
        var split = text.IndexOfAny(['+', '-']);
        var name = split < 0 ? text : text[..split].TrimEnd();
        var suffix = split < 0 ? string.Empty : text[split..].Trim();

        if (!labels.TryGetValue(name, out var position))
        {
            var known = labels.Count == 0
                ? "no labels have been defined yet"
                : "known labels: " + string.Join(", ", labels.Keys.OrderBy(k => k, StringComparer.Ordinal));
            throw new ArgumentException(
                $"'{text}' names no label on this timeline — {known}. "
                + "A position is a number, \"+=\"/\"-=\", \"<\"/\">\", or a label you have set with tl.label(...).");
        }

        return suffix.Length == 0 ? position : position + Offset(suffix, text);
    }

    static double Offset(string text, string whole)
    {
        var signed = text.StartsWith("+=", StringComparison.Ordinal) ? text[2..]
            : text.StartsWith("-=", StringComparison.Ordinal) ? text[2..]
            : throw new ArgumentException(
                $"'{whole}' is not a position — an offset is written \"+=200\" or \"-=200\".");

        if (!double.TryParse(signed.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            throw new ArgumentException($"'{whole}' has no number after its offset.");
        }

        return text[0] == '-' ? -amount : amount;
    }

    Entry BuildAttributeEntry(
        SnapElement target, object? attributes, double at, double dur, Func<double, double> ease, string caller)
    {
        var tracks = ReadAttributes(attributes, caller)
            .Select(pair => Track.Build(target, pair.Name, pair.Value, caller))
            .ToArray();

        return new Entry(at, dur, ease, s =>
        {
            foreach (var track in tracks) target.Attr(track.Name, track.ValueAt(s));
        });
    }

    static List<(string Name, object? Value)> ReadAttributes(object? attributes, string caller)
    {
        var dictionary = JsInterop.AsDict(attributes)
            ?? throw new ArgumentException(
                $"{caller.ToLowerInvariant()}(...) needs an object of attributes, e.g. {{ cx: 200, fill: '#c33' }}.",
                nameof(attributes));

        var pairs = new List<(string, object?)>();
        foreach (DictionaryEntry entry in dictionary)
        {
            var name = entry.Key?.ToString();
            if (!string.IsNullOrWhiteSpace(name)) pairs.Add((name, entry.Value));
        }

        if (pairs.Count == 0)
        {
            throw new ArgumentException($"{caller.ToLowerInvariant()}(...) was given no attributes to change.",
                nameof(attributes));
        }

        return pairs;
    }

    static List<SnapElement> AsElements(object? targets)
    {
        switch (targets)
        {
            case null:
                throw new ArgumentNullException(nameof(targets), "stagger(...) needs elements to stagger.");

            case SnapElement single:
                return [single];

            case IEnumerable sequence and not string:
                var list = new List<SnapElement>();
                foreach (var item in sequence)
                {
                    if (item is SnapElement element) list.Add(element);
                    else if (item is not null)
                    {
                        throw new ArgumentException(
                            $"stagger(...) takes SVG elements; one of them was a {item.GetType().Name}.",
                            nameof(targets));
                    }
                }
                return list;

            default:
                throw new ArgumentException(
                    $"stagger(...) takes an array of SVG elements; got a {targets.GetType().Name}.", nameof(targets));
        }
    }
    #endregion

    #region Child types
    /// <summary>One entry, applied by its normalised and eased status.</summary>
    private class Entry
    {
        internal Entry(double at, double dur, Func<double, double> ease, Action<double> apply)
        {
            At = at;
            Dur = dur;
            this.ease = ease;
            this.apply = apply;
        }

        internal double At { get; }
        internal double Dur { get; }

        private readonly Func<double, double> ease;
        private readonly Action<double> apply;

        /// <summary>
        /// Applies this entry at <paramref name="t"/>, whether or not <paramref name="t"/> is inside it.
        /// </summary>
        /// <remarks>
        /// Clamped rather than skipped, which is the whole of the seek-safety property: before the
        /// entry it applies its start value, after it, its end value. Skipping would leave whatever
        /// the last seek happened to write, and a backwards seek would not reproduce.
        /// </remarks>
        internal virtual void Apply(double t)
        {
            var raw = Dur <= 0d ? (t >= At ? 1d : 0d) : (t - At) / Dur;
            var status = raw < 0d ? 0d : raw > 1d ? 1d : raw;
            apply(Dur <= 0d ? status : ease(status));
        }
    }

    /// <summary>An entry that reads the seek time itself — a window rather than a tween.</summary>
    private sealed class EntryAtTime(double at, double dur, Action<double> apply) : Entry(at, dur, Linear, _ => { })
    {
        private readonly Action<double> apply = apply;

        internal override void Apply(double t) => apply(t);
    }

    /// <summary>One attribute's journey from its base value to its target.</summary>
    private sealed class Track
    {
        private Track(string name, Func<double, object?> valueAt)
        {
            Name = name;
            this.valueAt = valueAt;
        }

        internal string Name { get; }

        private readonly Func<double, object?> valueAt;

        internal object? ValueAt(double status) => valueAt(status);

        /// <summary>
        /// Reads the element's current value and decides how the two ends interpolate.
        /// </summary>
        /// <remarks>
        /// Numbers and colours interpolate; anything else <b>throws, naming the attribute and both
        /// values</b>. Producing <c>NaN</c> instead would write a malformed attribute that the
        /// renderer drops, so the element would simply not move and nothing would say why.
        /// <para>
        /// An unset base value throws too, rather than assuming one. SVG's defaults are per
        /// attribute — <c>opacity</c> starts at 1 and <c>cx</c> at 0 — so a single guess is wrong
        /// half the time, and wrong here means a fade that starts from the opposite end.
        /// </para>
        /// </remarks>
        internal static Track Build(SnapElement target, string name, object? to, string caller)
        {
            var baseValue = target.Attr(name);
            var call = caller.ToLowerInvariant();

            if (AsNumber(to, out var toNumber))
            {
                if (!AsNumber(baseValue, out var fromNumber))
                {
                    throw new ArgumentException(
                        $"{call}(...) cannot tween '{name}' to {toNumber} because the element has no current "
                        + $"value for it{(baseValue is null ? string.Empty : $" ('{baseValue}' is not a number)")}. "
                        + $"Give it one first — element.attr({{ {name}: … }}) or tl.set(...) — because SVG's "
                        + "default differs per attribute and guessing would start the move in the wrong place.",
                        nameof(to));
                }

                return new Track(name, s => fromNumber + (toNumber - fromNumber) * s);
            }

            if (AsColour(to, out var toColour))
            {
                if (!AsColour(baseValue, out var fromColour))
                {
                    throw new ArgumentException(
                        $"{call}(...) cannot tween '{name}' to '{to}' from '{baseValue ?? "unset"}', which is not "
                        + "a colour. Set a starting colour first, or use tl.set(...) to change it in one step.",
                        nameof(to));
                }

                return new Track(name, s => Mix(fromColour, toColour, s));
            }

            throw new ArgumentException(
                $"{call}(...) cannot interpolate '{name}' from '{baseValue ?? "unset"}' "
                + $"to '{to ?? "unset"}' — only numbers and colours tween. "
                + "Use tl.set(...) for a value that changes in one step.", nameof(to));
        }

        static bool AsNumber(object? value, out double number)
        {
            switch (value)
            {
                case null:
                    number = 0d;
                    return false;

                case string text:
                    return double.TryParse(
                        text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);

                case IConvertible and not bool:
                    try
                    {
                        number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        return !double.IsNaN(number);
                    }
                    catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
                    {
                        number = 0d;
                        return false;
                    }

                default:
                    number = 0d;
                    return false;
            }
        }

        static bool AsColour(object? value, out SKColor colour) =>
            SkiaColorParser.TryParse(value as string, out colour);

        /// <summary>Channel-wise, including alpha, emitted in the form SVG reads.</summary>
        static string Mix(SKColor from, SKColor to, double s)
        {
            static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);

            var r = Lerp(from.Red, to.Red, s);
            var g = Lerp(from.Green, to.Green, s);
            var b = Lerp(from.Blue, to.Blue, s);
            var a = Lerp(from.Alpha, to.Alpha, s);

            return a == 255
                ? $"#{r:X2}{g:X2}{b:X2}"
                : $"rgba({r},{g},{b},{(a / 255d).ToString("0.###", CultureInfo.InvariantCulture)})";
        }
    }
    #endregion
}
