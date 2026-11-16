// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// A face moved by channels and drawn whole, on any head: what <c>comp.face</c> does every frame, as two calls a drawing
/// can make on a head it built itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is here and not only in the face layer.</b> <c>comp.face</c> places its own head by origin, height, yaw and
/// pitch, so it cannot follow a figure: a head fitted to a leaning mannequin carries a roll, and the layer has none. A
/// figure drawn every frame in <c>comp.drawn</c> therefore drew its head by hand, and everything the face layer brought
/// (expressions, blinks, the eye wheel, pose blending, the feature looks) was left behind: anim3's keeper got default
/// features and a hand-rolled mouth. <see cref="ApplyFaceChannels"/> and <see cref="DrawFace"/> are the layer's own two
/// steps, so a head from <c>createHeadForFigure</c> gets all of it, and the layer is built from the same calls.
/// </para>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling onto them,
/// so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>. The camelCase form is the one documented in
/// <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// </remarks>
public partial class ConstructiveDrawingToolkit
{
    #region Face Channels
    /// <summary>
    /// Moves a head by named channels: <c>{ AU45: 1, joy: 0.6, upperLid: 2, 'near.AU2': 0.8, O: 1 }</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A channel is an Action Unit (<c>AU12</c>), a named expression (<c>joy</c>), an eye-wheel setting (<c>upperLid</c>,
    /// <c>browTop</c>, ...), or the name of a pose in <c>options.poses</c>, each optionally prefixed <c>near.</c> or
    /// <c>far.</c> for one side. Units and expressions add up per side; a pose adds its values times its weight, and moves
    /// the eye wheel by its weight times its distance from the normal setting, so poses blend as shapes do. Exactly what
    /// <c>comp.face</c> does with its channels at one time.
    /// </para>
    /// <para>
    /// <c>options.poses</c> is <c>{ name: { channel: value } }</c>, for instance <c>line.poses()</c>, Blair's ten mouths:
    /// pass <c>line.weights()</c>'s values as channels and the head speaks.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ApplyFaceChannels(object headObj, object? channels = null, object? options = null)
    {
        const string who = "applyFaceChannels";
        if (JsInterop.AsDict(headObj) is null)
            throw new ArgumentException($"{who} needs a head, from createLoomisHead or createHeadForFigure.", nameof(headObj));
        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, ["poses"], $"{who} option");
        var poses = ReadFacePoses(opt?["poses"], who);

        var values = new List<FaceChannelValue>();
        if (channels is not null)
        {
            var dict = JsInterop.AsDict(channels) ?? throw new ArgumentException($"{who}'s channels is an object: {{ name: number }}.", nameof(channels));
            foreach (DictionaryEntry e in dict)
            {
                var name = e.Key.ToString()!;
                var (side, bare) = SplitFaceSide(name);
                var kind = poses.ContainsKey(bare) ? FaceChannelKind.Pose : ClassifyFaceChannel(bare, who, name);
                var v = e.Value is null ? 0f : Convert.ToSingle(e.Value, CultureInfo.InvariantCulture);
                if (!float.IsFinite(v)) throw new ArgumentException($"{who}'s channel '{name}' is {v}; it must be a finite number.", nameof(channels));
                values.Add(new FaceChannelValue(side, bare, kind, v));
            }
        }
        return ApplyFaceChannelValues(headObj, values, poses);
    }

    /// <summary>
    /// Draws a whole face on a head: the head and neck filled, the ears, then the eye lines, brows, eyes, nose and mouth
    /// clipped to its mass, then its outline. <c>look</c> is <c>comp.face</c>'s: <c>{ skin, ink, outline, geometry,
    /// planes, lines, brow, eye, nose, mouth, ears }</c>, each feature's options its drawer's, or <c>false</c> to leave it
    /// out. Returns the geometry it drew, <c>{ silhouette, mass }</c>, for whatever sits on the head.
    /// </summary>
    /// <remarks>
    /// Draws in the head's own space: a head fitted to a figure is drawn inside the figure's lean, as Manual 23 §8 does
    /// (<c>fit.pivot</c>, <c>fit.rollDeg</c>). The nose cannot read pitch from the landmarks; pass it as
    /// <c>look.nose.pitchDeg</c> for a head that tips. Then draw the body, which covers the neck's base.
    /// </remarks>
    public Dictionary<string, object?> DrawFace(CanvasRenderingContext2D ctx, object headObj, object? look = null) =>
        DrawFace(ctx, headObj, look, 0f, null);
    #endregion

    #region Face Channels (shared with comp.face)
    /// <summary>The look keys <see cref="DrawFace"/> takes.</summary>
    internal static readonly string[] FaceLookKeys =
        ["skin", "ink", "outline", "geometry", "planes", "lines", "brow", "eye", "nose", "mouth", "ears"];

    /// <summary>The eye-wheel settings a channel can name.</summary>
    internal static readonly string[] FaceWheelChannels =
        ["browTop", "browTopInner", "browTopOuter", "browBottom", "browBottomInner", "browBottomOuter", "fold", "upperLid", "lowerLid"];

    /// <summary>
    /// <see cref="DrawFace"/> with what <c>comp.face</c> knows and a bare head does not: the pitch the nose is handed when its
    /// look does not set one, and the height the outline's default width is taken from (the head's own when null).
    /// </summary>
    internal Dictionary<string, object?> DrawFace(CanvasRenderingContext2D ctx, object headObj, object? lookObj, float pitchDeg, float? outlineHeight)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var head = JsInterop.AsDict(headObj) ?? throw new ArgumentException("drawFace needs a head, from createLoomisHead or createHeadForFigure.", nameof(headObj));
        var look = JsInterop.AsDict(lookObj) ?? (lookObj is null ? new Hashtable() : throw new ArgumentException("drawFace's look is an object of drawing options.", nameof(lookObj)));
        RefuseUnknownHeadParameters(look, FaceLookKeys, "drawFace look");

        var ink = look["ink"]?.ToString() ?? "#15151a";
        Hashtable Feature(string key)
        {
            var own = JsInterop.AsDict(look[key]);
            var options = new Hashtable { ["inkColor"] = ink };
            if (own is not null) foreach (DictionaryEntry e in own) options[e.Key] = e.Value;
            return options;
        }
        bool On(string key) => look[key] is not false;

        var geo = CreateHeadGeometry(head, JsInterop.AsDict(look["geometry"]));
        ctx.FillStyle = look["skin"]?.ToString() ?? "#efe2cf";
        ctx.Fill((CanvasPath)geo["silhouette"]!);

        if (look["planes"] is not null and not false)
            DrawHeadPlanes(ctx, head, JsInterop.AsDict(look["planes"]) is { } given ? new Hashtable(given) : []);

        if (On("ears") && JsInterop.AsDict(geo["ears"]) is { } ears)
        {
            DrawComicEar(ctx, ears["far"]!, true, Feature("ears"));
            DrawComicEar(ctx, ears["near"]!, false, Feature("ears"));
        }

        ctx.Save();
        ctx.Clip((CanvasPath)geo["mass"]!);
        if (On("lines")) DrawEyeLines(ctx, head, Feature("lines"));
        if (On("brow"))
        {
            DrawComicBrow(ctx, head["farBrow"]!, true, Feature("brow"));
            DrawComicBrow(ctx, head["nearBrow"]!, false, Feature("brow"));
        }
        if (On("eye"))
        {
            DrawComicEye(ctx, head["farEye"]!, true, Feature("eye"));
            DrawComicEye(ctx, head["nearEye"]!, false, Feature("eye"));
        }
        if (On("nose"))
        {
            var nose = Feature("nose");
            if (!nose.ContainsKey("pitchDeg") && Math.Abs(pitchDeg) > 1e-6) nose["pitchDeg"] = pitchDeg;
            DrawComicNose(ctx, head["noseWedge"]!, nose);
        }
        if (On("mouth")) DrawComicMouth(ctx, head["mouthGuides"]!, Feature("mouth"));
        ctx.Restore();

        if (On("outline"))
        {
            var outline = JsInterop.AsDict(look["outline"]);
            var h = outlineHeight ?? Num(JsInterop.AsDict(head["unit"]), "H", 200f);
            ctx.StrokeStyle = outline?["color"]?.ToString() ?? ink;
            ctx.LineWidth = outline is not null && outline["width"] is { } w ? Convert.ToSingle(w, CultureInfo.InvariantCulture) : MathF.Max(1f, h / 220f);
            ctx.Stroke((CanvasPath)geo["silhouette"]!);
        }

        return new Dictionary<string, object?> { ["silhouette"] = geo["silhouette"], ["mass"] = geo["mass"] };
    }

    /// <summary>
    /// The channel values applied to a head: units and expressions summed per side and applied, then the eye wheel set by
    /// its channels and moved by each pose's weight times its distance from the normal setting.
    /// </summary>
    internal Dictionary<string, object?> ApplyFaceChannelValues(object headObj, IEnumerable<FaceChannelValue> values,
        IReadOnlyDictionary<string, List<FacePoseEntry>> poses)
    {
        var head = JsInterop.AsDict(headObj) is IDictionary given ? CloneHead(given) : throw new ArgumentException("A face needs a head.", nameof(headObj));

        string[] sides = ["both", "near", "far"];
        var units = sides.ToDictionary(s => s, _ => new Dictionary<string, object?>());
        var wheel = sides.ToDictionary(s => s, _ => new Dictionary<string, float>());
        var offsets = sides.ToDictionary(s => s, _ => new Dictionary<string, float>());
        void AddExpression(Dictionary<string, object?> into, string name, float weight)
        {
            if (weight > 0f)
                foreach (var (unit, w) in ExpressionUnits(name, MathF.Min(1f, weight)))
                    Add(into, unit, Convert.ToSingle(w, CultureInfo.InvariantCulture));
        }
        foreach (var (side, bare, kind, v) in values)
        {
            var key = side ?? "both";
            switch (kind)
            {
                case FaceChannelKind.Unit: Add(units[key], bare, v); break;
                case FaceChannelKind.Expression: AddExpression(units[key], bare, v); break;
                case FaceChannelKind.Wheel: wheel[key][bare] = v; break;
                default:
                    if (v == 0f) break;
                    foreach (var entry in poses[bare])
                    {
                        // A pose set on one side puts its unsided values there; its own sided values go only where they belong.
                        var target = entry.Side is null ? key : key == "both" || key == entry.Side ? entry.Side : null;
                        if (target is null) continue;
                        switch (entry.Kind)
                        {
                            case FaceChannelKind.Unit: Add(units[target], entry.Bare, (float)(v * entry.Value)); break;
                            case FaceChannelKind.Expression: AddExpression(units[target], entry.Bare, (float)(v * entry.Value)); break;
                            default: offsets[target][entry.Bare] = offsets[target].GetValueOrDefault(entry.Bare) + (float)(v * (entry.Value - 3d)); break;
                        }
                    }
                    break;
            }
        }

        // A unit pushed below zero by a negative pose weight is no unit at all.
        foreach (var w in units.Values) foreach (var unit in w.Keys.ToList()) w[unit] = MathF.Max(0f, (float)w[unit]!);
        Dictionary<string, object?> result = head;
        if (units["both"].Count > 0) result = ApplyActionUnits(result, units["both"]);
        foreach (var side in new[] { "near", "far" })
            if (units[side].Count > 0) result = ApplyActionUnits(result, units[side], new Dictionary<string, object?> { ["side"] = side });

        if (sides.Any(s => wheel[s].Count > 0 || offsets[s].Count > 0))
        {
            var settings = FaceWheelSettings(wheel["both"], offsets["both"]);
            foreach (var side in new[] { "near", "far" })
                if (wheel[side].Count > 0 || offsets[side].Count > 0)
                    settings[side] = FaceWheelSettings(
                        wheel["both"].Concat(wheel[side]).GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.Last().Value),
                        offsets["both"].Concat(offsets[side]).GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.Sum(e => e.Value)));
            result = EyeWheel(result, settings);
        }
        return result;

        static void Add(Dictionary<string, object?> into, string unit, float w) =>
            into[unit] = (into.TryGetValue(unit, out var had) && had is float f ? f : 0f) + w;
    }

    /// <summary>A channel name split into its side (<c>near.</c>, <c>far.</c>, or none) and the bare channel.</summary>
    internal static (string? Side, string Bare) SplitFaceSide(string name)
    {
        foreach (var side in new[] { "near", "far" })
            if (name.StartsWith(side + ".", StringComparison.Ordinal)) return (side, name[(side.Length + 1)..]);
        return (null, name);
    }

    /// <summary>What a channel names: a unit, an eye-wheel setting, or an expression; anything else is refused by name.</summary>
    internal FaceChannelKind ClassifyFaceChannel(string bare, string who, string name)
    {
        if (FaceWheelChannels.Contains(bare)) return FaceChannelKind.Wheel;
        if (bare.StartsWith("AU", StringComparison.Ordinal))
        {
            // applyActionUnits refuses a unit it does not know, naming those it does.
            ApplyActionUnits(CreateLoomisHead(0f, 0f, 100f, 0f), new Dictionary<string, object?> { [bare] = 0f });
            return FaceChannelKind.Unit;
        }
        if (IsFaceExpression(bare)) return FaceChannelKind.Expression;
        throw new ArgumentException(
            $"{who}'s channel '{name}' is not a channel. Channels are Action Units (AU1, AU12, ...), expressions "
            + $"(joy, anger, fear, sadness, surprise, disgust), eye-wheel settings ({string.Join(", ", FaceWheelChannels)}), or "
            + "the name of a pose in the face's poses, each optionally prefixed near. or far. for one side.");
    }

    internal bool IsFaceExpression(string name)
    {
        try { ExpressionUnits(name, 0f); return true; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Whether a pose name would shadow a channel, which is refused: a pose's channel must mean only the pose.</summary>
    internal bool FacePoseNameTaken(string poseName) =>
        poseName.Contains('.') || FaceWheelChannels.Contains(poseName) || poseName.StartsWith("AU", StringComparison.Ordinal) || IsFaceExpression(poseName);

    /// <summary>A pose library from a script: <c>{ name: { channel: number } }</c>.</summary>
    Dictionary<string, List<FacePoseEntry>> ReadFacePoses(object? raw, string who)
    {
        var poses = new Dictionary<string, List<FacePoseEntry>>();
        if (raw is null) return poses;
        var library = JsInterop.AsDict(raw) ?? throw new ArgumentException($"{who}'s poses is an object: {{ name: {{ channel: value }} }}.");
        foreach (DictionaryEntry p in library)
        {
            var poseName = p.Key.ToString()!;
            if (FacePoseNameTaken(poseName))
                throw new ArgumentException($"{who}'s pose '{poseName}' takes a name a channel already has; name it something else.");
            var entries = new List<FacePoseEntry>();
            var values = JsInterop.AsDict(p.Value) ?? throw new ArgumentException($"{who}'s pose '{poseName}' is an object: {{ channel: value }}.");
            foreach (DictionaryEntry e in values)
            {
                var name = e.Key.ToString()!;
                var (side, bare) = SplitFaceSide(name);
                entries.Add(new FacePoseEntry(side, bare, ClassifyFaceChannel(bare, $"{who}'s pose '{poseName}'", name),
                    Convert.ToDouble(e.Value, CultureInfo.InvariantCulture)));
            }
            poses[poseName] = entries;
        }
        return poses;
    }

    /// <summary>
    /// The eye wheel's settings from its channels, held inside the wheel's range: a keyed curve that overshoots a setting
    /// is clamped rather than refused, so an <c>auto</c> ease can be used on it.
    /// </summary>
    static Dictionary<string, object?> FaceWheelSettings(IReadOnlyDictionary<string, float> set, IReadOnlyDictionary<string, float> offset)
    {
        float Off(string key) => offset.GetValueOrDefault(key);
        float? Set(string key) => set.TryGetValue(key, out var v) ? v : null;
        var settings = new Dictionary<string, object?>();
        foreach (var (edge, max) in new[] { ("browTop", 3f), ("browBottom", 5f) })
        {
            var both = Set(edge) ?? 3f;
            float End(string end) => Math.Clamp((Set(edge + end) ?? both) + Off(edge + end) + Off(edge), 1f, max);
            settings[edge] = new Dictionary<string, object?> { ["inner"] = End("Inner"), ["outer"] = End("Outer") };
        }
        foreach (var (key, max) in new[] { ("fold", 3f), ("upperLid", 5f), ("lowerLid", 4f) })
            settings[key] = Math.Clamp((Set(key) ?? 3f) + Off(key), 1f, max);
        return settings;
    }
    #endregion

    #region Types
    /// <summary>What a face channel names.</summary>
    internal enum FaceChannelKind { Unit, Expression, Wheel, Pose }

    /// <summary>One channel's value at one time.</summary>
    internal readonly record struct FaceChannelValue(string? Side, string Bare, FaceChannelKind Kind, float Value);

    /// <summary>One channel value held by a pose.</summary>
    internal sealed record FacePoseEntry(string? Side, string Bare, FaceChannelKind Kind, double Value);
    #endregion
}
