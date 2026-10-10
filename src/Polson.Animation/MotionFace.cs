namespace Polson.Animation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Polson.Drawing.Skia;

/// <summary>
/// A face as a composition layer: a constructed head whose placement, character and expression are channels, each a
/// value or a node, drawn every frame by the feature drawers.
/// </summary>
/// <remarks>
/// <para>
/// The face is a pure function of its numbers, so it is rebuilt for each frame from them rather than deformed: the head
/// from <c>origin</c>, <c>height</c>, <c>yaw</c> and <c>pitch</c>; then the <c>character</c>; then the <c>channels</c>,
/// named in the toolkit's own vocabulary: Action Units (<c>AU12</c>), named expressions (<c>joy</c>), and Hamm's eye
/// wheel settings (<c>upperLid</c>, <c>browTop</c>, ...), any of them prefixed <c>near.</c> or <c>far.</c> for one side.
/// Any frame renders directly, in any order.
/// </para>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling onto them,
/// so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>. The camelCase form is the one documented in
/// <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// </remarks>
public sealed class MotionFace
{
    #region Constructors
    internal MotionFace(MotionFaceLayer layer) => this.layer = layer;
    #endregion

    #region Fields
    private readonly MotionFaceLayer layer;
    #endregion

    #region Properties
    /// <summary>The channels the face was given, in order.</summary>
    public IReadOnlyList<string> Channels => layer.ChannelNames;
    #endregion

    #region Methods
    /// <summary>The head the face draws at <paramref name="time"/> seconds: every landmark, for placing things on it.</summary>
    public Dictionary<string, object?> HeadAt(double time) => layer.HeadAt(time);

    /// <summary>Every channel's value at <paramref name="time"/> seconds.</summary>
    public Dictionary<string, object?> ChannelsAt(double time) =>
        layer.ChannelNames.Zip(layer.ChannelNodes).ToDictionary(c => c.First, c => (object?)c.Second.Evaluate(time)[0]);
    #endregion
}

/// <summary>The layer behind <see cref="MotionFace"/>.</summary>
internal sealed class MotionFaceLayer : MotionLayer
{
    #region Constructors
    public MotionFaceLayer(MotionOptions o) : base(o)
    {
        construction = o.Raw("construction")?.ToString()?.Trim() ?? "loomis";
        if (construction is not ("loomis" or "doubleCircle"))
            throw new ArgumentException($"{o.Who}'s construction is 'loomis' or 'doubleCircle', not '{construction}'.");
        if (construction == "doubleCircle" && (o.Has("yaw") || o.Has("pitch")))
            throw new ArgumentException($"{o.Who}: the double-circle head is a front view and takes no yaw or pitch; use construction 'loomis' to turn it.");

        origin = o.Node("origin", MotionType.Vector, new[] { 0d, 0d });
        height = o.Node("height", MotionType.Real, 300d);
        // Degrees, as the head takes them: an angle node or a real one.
        MotionNode Degrees(string key) => o.Raw(key) is MotionNode { Kind: MotionType.Angle } angle ? angle : o.Node(key, MotionType.Real, 0d);
        yaw = Degrees("yaw");
        pitch = Degrees("pitch");
        character = ReadReals(o.Raw("character"), $"{o.Who}'s character");
        look = JsInterop.AsDict(o.Raw("look")) ?? (o.Has("look") ? throw new ArgumentException($"{o.Who}'s look is an object of drawing options.") : new Hashtable());
        foreach (var key in look.Keys.Cast<object>().Select(k => k.ToString()!))
            if (!LookKeys.Contains(key))
                throw new ArgumentException($"{o.Who}'s look has no '{key}'. It takes: {string.Join(", ", LookKeys)}.");

        var channels = ReadReals(o.Raw("channels"), $"{o.Who}'s channels");
        foreach (var (name, node) in channels)
        {
            var (side, bare) = SplitSide(name);
            var kind = Classify(bare, o.Who, name);
            parsed.Add((name, side, bare, kind, node));
        }

        // Built once now, so a bad option in the look or the character is refused at the call rather than at the first frame.
        _ = Build(0d, out _);
        using var probe = new SkiaCanvas(4, 4);
        Draw(probe, 0d, 1d);
    }
    #endregion

    #region Fields
    internal static readonly string[] Options =
        ["origin", "height", "yaw", "pitch", "construction", "character", "channels", "look", "amount", "blend", "desc"];

    static readonly string[] LookKeys =
        ["skin", "ink", "outline", "geometry", "planes", "lines", "brow", "eye", "nose", "mouth", "ears"];

    static readonly string[] WheelChannels =
        ["browTop", "browTopInner", "browTopOuter", "browBottom", "browBottomInner", "browBottomOuter", "fold", "upperLid", "lowerLid"];

    static readonly ConstructiveDrawingToolkit Toolkit = new();

    private readonly string construction;
    private readonly MotionNode origin;
    private readonly MotionNode height;
    private readonly MotionNode yaw;
    private readonly MotionNode pitch;
    private readonly List<(string Name, MotionNode Node)> character;
    private readonly IDictionary look;
    private readonly List<(string Name, string? Side, string Bare, ChannelKind Kind, MotionNode Node)> parsed = [];
    #endregion

    #region Properties
    public IReadOnlyList<string> ChannelNames => [.. parsed.Select(p => p.Name)];

    public IReadOnlyList<MotionNode> ChannelNodes => [.. parsed.Select(p => p.Node)];
    #endregion

    #region Methods
    public Dictionary<string, object?> HeadAt(double time) => Build(time, out _);

    public override IEnumerable<MotionNode> Nodes() =>
        new[] { Amount, origin, height, yaw, pitch }.Concat(character.Select(c => c.Node)).Concat(parsed.Select(p => p.Node));

    public override XElement ToSif(SifWriter sif) => throw new InvalidOperationException(
        $"The face layer{(Desc is null ? "" : $" '{Desc}'")} cannot be written as .sif: Synfig cannot run the feature drawers. "
        + "Write the composition without it, or capture it as frames.");

    public override XElement ToSvg(SvgAnimationWriter svg, MotionTimeMap map) => throw SvgAnimationWriter.Refuse("face layer", Desc);

    protected override void Draw(SkiaCanvas surface, double time, double amount)
    {
        var canvas = surface.SkCanvas;
        var depth = canvas.Save();
        if (amount < 1)
        {
            using var fade = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(255, 255, 255, (byte)Math.Round(amount * 255)) };
            canvas.SaveLayer(fade);
        }

        try
        {
            var head = Build(time, out var pitchDeg);
            var ctx = new CanvasRenderingContext2D(surface, canvas.TotalMatrix);
            var ink = look["ink"]?.ToString() ?? "#15151a";
            Hashtable Feature(string key)
            {
                var own = JsInterop.AsDict(look[key]);
                var options = new Hashtable { ["inkColor"] = ink };
                if (own is not null) foreach (DictionaryEntry e in own) options[e.Key] = e.Value;
                return options;
            }
            bool On(string key) => look[key] is not false;

            var geo = Toolkit.CreateHeadGeometry(head, JsInterop.AsDict(look["geometry"]));
            ctx.FillStyle = look["skin"]?.ToString() ?? "#efe2cf";
            ctx.Fill((CanvasPath)geo["silhouette"]!);

            if (look["planes"] is not null and not false)
            {
                var planeOptions = JsInterop.AsDict(look["planes"]) is { } given ? new Hashtable(given) : [];
                Toolkit.DrawHeadPlanes(ctx, head, planeOptions);
            }

            if (On("ears") && JsInterop.AsDict(geo["ears"]) is { } ears)
            {
                Toolkit.DrawComicEar(ctx, ears["far"]!, true, Feature("ears"));
                Toolkit.DrawComicEar(ctx, ears["near"]!, false, Feature("ears"));
            }

            ctx.Save();
            ctx.Clip((CanvasPath)geo["mass"]!);
            if (On("lines")) Toolkit.DrawEyeLines(ctx, head, Feature("lines"));
            if (On("brow"))
            {
                Toolkit.DrawComicBrow(ctx, head["farBrow"]!, true, Feature("brow"));
                Toolkit.DrawComicBrow(ctx, head["nearBrow"]!, false, Feature("brow"));
            }
            if (On("eye"))
            {
                Toolkit.DrawComicEye(ctx, head["farEye"]!, true, Feature("eye"));
                Toolkit.DrawComicEye(ctx, head["nearEye"]!, false, Feature("eye"));
            }
            if (On("nose"))
            {
                // The nose cannot read its own pitch from the landmarks, so the face hands it the pitch channel.
                var nose = Feature("nose");
                if (!nose.ContainsKey("pitchDeg") && Math.Abs(pitchDeg) > 1e-6) nose["pitchDeg"] = pitchDeg;
                Toolkit.DrawComicNose(ctx, head["noseWedge"]!, nose);
            }
            if (On("mouth")) Toolkit.DrawComicMouth(ctx, head["mouthGuides"]!, Feature("mouth"));
            ctx.Restore();

            if (On("outline"))
            {
                var outline = JsInterop.AsDict(look["outline"]);
                ctx.StrokeStyle = outline?["color"]?.ToString() ?? ink;
                ctx.LineWidth = outline is not null && outline["width"] is { } w ? Convert.ToSingle(w) : MathF.Max(1f, (float)height.Evaluate(time)[0] / 220f);
                ctx.Stroke((CanvasPath)geo["silhouette"]!);
            }
        }
        finally
        {
            canvas.RestoreToCount(depth);
        }
    }

    /// <summary>The head at a time: built, given its character, then its expression and its eye-wheel settings.</summary>
    private Dictionary<string, object?> Build(double time, out float pitchDeg)
    {
        var o = origin.Evaluate(time);
        var h = (float)height.Evaluate(time)[0];
        pitchDeg = (float)pitch.Evaluate(time)[0];
        var head = construction == "doubleCircle"
            ? Toolkit.CreateDoubleCircleHead((float)o[0], (float)o[1], h)
            : Toolkit.CreateLoomisHead((float)o[0], (float)o[1], h, (float)yaw.Evaluate(time)[0], pitchDeg);

        if (character.Count > 0)
            head = Toolkit.CreateParametricHead(head, character.ToDictionary(c => c.Name, c => (object?)(float)c.Node.Evaluate(time)[0]));

        // Units and expressions add up per side, as applyActionUnits composes them; a unit past 1 is clamped there.
        var weights = new Dictionary<string, Dictionary<string, object?>> { ["both"] = [], ["near"] = [], ["far"] = [] };
        var wheel = new Dictionary<string, Dictionary<string, object?>> { ["both"] = [], ["near"] = [], ["far"] = [] };
        foreach (var (_, side, bare, kind, node) in parsed)
        {
            var v = (float)node.Evaluate(time)[0];
            var key = side ?? "both";
            switch (kind)
            {
                case ChannelKind.Unit:
                    Add(weights[key], bare, MathF.Max(0f, v));
                    break;
                case ChannelKind.Expression:
                    if (v > 0f)
                        foreach (var (unit, w) in Toolkit.ExpressionUnits(bare, MathF.Min(1f, v)))
                            Add(weights[key], unit, Convert.ToSingle(w));
                    break;
                default:
                    wheel[key][bare] = v;
                    break;
            }
        }

        if (weights["both"].Count > 0) head = Toolkit.ApplyActionUnits(head, weights["both"]);
        foreach (var side in new[] { "near", "far" })
            if (weights[side].Count > 0) head = Toolkit.ApplyActionUnits(head, weights[side], new Dictionary<string, object?> { ["side"] = side });

        if (wheel.Values.Any(w => w.Count > 0))
        {
            var settings = WheelSettings(wheel["both"]);
            foreach (var side in new[] { "near", "far" })
                if (wheel[side].Count > 0) settings[side] = WheelSettings(wheel["both"].Concat(wheel[side]).GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.Last().Value));
            head = Toolkit.EyeWheel(head, settings);
        }
        return head;

        static void Add(Dictionary<string, object?> into, string unit, float w) =>
            into[unit] = (into.TryGetValue(unit, out var had) && had is float f ? f : 0f) + w;
    }

    /// <summary>
    /// The eye wheel's settings from its channels, held inside the wheel's range: a keyed curve that overshoots a
    /// setting is clamped rather than refused, so an <c>auto</c> ease can be used on it.
    /// </summary>
    private static Dictionary<string, object?> WheelSettings(IReadOnlyDictionary<string, object?> channels)
    {
        float Get(string key, float max, float fallback) =>
            channels.TryGetValue(key, out var v) && v is float f ? Math.Clamp(f, 1f, max) : fallback;
        var settings = new Dictionary<string, object?>();
        foreach (var (edge, max) in new[] { ("browTop", 3f), ("browBottom", 5f) })
        {
            var both = Get(edge, max, 3f);
            settings[edge] = new Dictionary<string, object?> { ["inner"] = Get(edge + "Inner", max, both), ["outer"] = Get(edge + "Outer", max, both) };
        }
        settings["fold"] = Get("fold", 3f, 3f);
        settings["upperLid"] = Get("upperLid", 5f, 3f);
        settings["lowerLid"] = Get("lowerLid", 4f, 3f);
        return settings;
    }

    private static (string? Side, string Bare) SplitSide(string name)
    {
        foreach (var side in new[] { "near", "far" })
            if (name.StartsWith(side + ".", StringComparison.Ordinal)) return (side, name[(side.Length + 1)..]);
        return (null, name);
    }

    /// <summary>What a channel names: a unit, an eye-wheel setting, or an expression; anything else is refused by name.</summary>
    private static ChannelKind Classify(string bare, string who, string name)
    {
        if (WheelChannels.Contains(bare)) return ChannelKind.Wheel;
        if (bare.StartsWith("AU", StringComparison.Ordinal))
        {
            // applyActionUnits refuses a unit it does not know, naming those it does.
            Toolkit.ApplyActionUnits(Toolkit.CreateLoomisHead(0f, 0f, 100f, 0f), new Dictionary<string, object?> { [bare] = 0f });
            return ChannelKind.Unit;
        }
        try
        {
            Toolkit.ExpressionUnits(bare, 0f);
            return ChannelKind.Expression;
        }
        catch (ArgumentException)
        {
            throw new ArgumentException(
                $"{who}'s channel '{name}' is not a channel. Channels are Action Units (AU1, AU12, ...), expressions "
                + $"(joy, anger, fear, sadness, surprise, disgust), or eye-wheel settings ({string.Join(", ", WheelChannels)}), "
                + "each optionally prefixed near. or far. for one side.");
        }
    }

    /// <summary>An object of named real values or nodes.</summary>
    private static List<(string, MotionNode)> ReadReals(object? raw, string who)
    {
        if (raw is null) return [];
        // In the order written: a script object keeps its keys' order, and a channel list reads best that way.
        IEnumerable<KeyValuePair<string, object?>> entries = raw is IDictionary<string, object?> ordered ? ordered
            : (JsInterop.AsDict(raw) ?? throw new ArgumentException($"{who} is an object: {{ name: number or node }}."))
                .Cast<DictionaryEntry>().Select(e => new KeyValuePair<string, object?>(e.Key.ToString()!, e.Value));
        return [.. entries.Select(e => (e.Key, MotionNodeFactory.Node(e.Value, MotionType.Real, $"{who}' '{e.Key}'")))];
    }
    #endregion

    #region Types
    internal enum ChannelKind { Unit, Expression, Wheel }
    #endregion
}
