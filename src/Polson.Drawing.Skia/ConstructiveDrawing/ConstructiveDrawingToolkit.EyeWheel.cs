namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class ConstructiveDrawingToolkit
{
    #region Eye Wheel
    static readonly string[] EyeWheelSettings = ["browTop", "browBottom", "fold", "upperLid", "lowerLid", "lines", "near", "far"];
    static readonly string[] EyeWheelSideSettings = ["browTop", "browBottom", "fold", "upperLid", "lowerLid", "lines"];
    static readonly string[] EyeLineAreas = ["A", "B", "C"];
    static readonly string[] EyeLineOptions = ["inkColor", "weight", "medium", "A", "B", "C"];

    // Hamm's settings, with 3 the normal awake setting in every column (p. 10). The upper lid as a share of its own
    // opening; the lower lid as a share of its own drop below the corners, negative meaning above them; the fold raised
    // by a share of the opening; each brow edge moved by a share of the brow's thickness. The amounts are the studio's
    // reading of his diagram.
    static readonly float[] UpperLidSettings = [1.5f, 1.25f, 1f, 0.65f, 0.3f];
    static readonly float[] LowerLidSettings = [-0.2f, 0.4f, 1f, 1.4f];
    static readonly float[] FoldSettings = [0.55f, 0.28f, 0f];
    const float BrowEdgeStep = 0.6f;
    // Where along the upper lid the fold stands highest, as drawComicEye lays it.
    const float FoldPeakT = 0.16f + (0.81f * 0.44f);

    /// <summary>The value a fractional setting reads off a table indexed from 1.</summary>
    static float Setting(float[] table, float setting) =>
        table[Math.Clamp((int)MathF.Floor(setting) - 1, 0, table.Length - 1)]
        + ((table[Math.Clamp((int)MathF.Floor(setting), 0, table.Length - 1)] - table[Math.Clamp((int)MathF.Floor(setting) - 1, 0, table.Length - 1)])
           * (setting - MathF.Floor(setting)));

    /// <summary>One side's settings, in Hamm's numbers.</summary>
    sealed record EyeWheelSide(float TopInner, float TopOuter, float BottomInner, float BottomOuter, float Fold, float Upper, float Lower,
                               float? LineA, float? LineB, float? LineC);

    /// <summary>
    /// Sets the eyes and brows by Hamm's eye wheel (<i>Drawing the Head and Figure</i>, p. 10): five horizontals that
    /// move up and down, twist and tilt, each with numbered settings and 3 the normal awake setting; and three areas of
    /// skin where lines may appear. Returns a new head.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The horizontals: <c>browTop</c>, the brow's upper edge (1–3, raised to normal); <c>browBottom</c>, its lower edge
    /// (1–5, raised to pulled down); <c>fold</c>, the lid fold (1–3); <c>upperLid</c> (1 wide open to 5 nearly closed);
    /// <c>lowerLid</c> (1 pushed up over the iris to 4 dropped). A brow setting may be one number or
    /// <c>{ inner, outer }</c>, which is the tilt; <c>near</c> and <c>far</c> take a side's own settings, which is the
    /// twist. Fractions are allowed. Settings are read against the head's own normal, so apply the wheel to a neutral
    /// character head.
    /// </para>
    /// <para>
    /// The skin areas: <c>A</c> between the brows, <c>B</c> across the forehead, <c>C</c> at the outer corner of the eye.
    /// Their strength follows the settings (a brow pulled down at its inner end, brows raised, a lower lid pushed up) and
    /// is kept on the head as <c>eyeLines</c> for <c>drawEyeLines</c>; <c>lines: { A, B, C }</c> (0–1) sets it instead.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> EyeWheel(object headObj, object? settings = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("eyeWheel needs a head from Drawing.createLoomisHead(...) or a call that returns one.", nameof(headObj));
        var opt = JsInterop.AsDict(settings);
        RefuseUnknownHeadParameters(opt, EyeWheelSettings, "eyeWheel setting");

        var result = CloneHead(head);
        var record = new Dictionary<string, object?>();
        var lines = new Dictionary<string, object?>();
        foreach (var side in new[] { "near", "far" })
        {
            var own = JsInterop.AsDict(opt?[side]);
            if (opt?[side] is not null && own is null) throw new ArgumentException($"eyeWheel {side} must be an object of settings, such as {{ browTop: 1 }}.");
            RefuseUnknownHeadParameters(own, EyeWheelSideSettings, $"eyeWheel {side} setting");
            var s = ReadEyeWheelSide(opt, own);
            ApplyEyeWheel(result, side, s);
            record[side] = new Dictionary<string, object?>
            {
                ["browTop"] = new Dictionary<string, object?> { ["inner"] = s.TopInner, ["outer"] = s.TopOuter },
                ["browBottom"] = new Dictionary<string, object?> { ["inner"] = s.BottomInner, ["outer"] = s.BottomOuter },
                ["fold"] = s.Fold, ["upperLid"] = s.Upper, ["lowerLid"] = s.Lower
            };
            // The lines follow the settings that make them: the brow's inner end pulled down knits the skin between the
            // brows; brows raised wrinkle the forehead; the lower lid pushed up crinkles the outer corner.
            lines[side] = new Dictionary<string, object?>
            {
                ["A"] = s.LineA ?? Math.Clamp(((MathF.Max(s.BottomInner, s.BottomOuter) - 3f) / 2f), 0f, 1f),
                ["B"] = s.LineB ?? Math.Clamp((3f - MathF.Min(s.TopInner, s.TopOuter)) / 2f, 0f, 1f),
                ["C"] = s.LineC ?? Math.Clamp((3f - s.Lower) / 2f, 0f, 1f)
            };
        }
        result["eyeWheel"] = record;
        result["eyeLines"] = lines;
        return result;
    }

    static EyeWheelSide ReadEyeWheelSide(IDictionary? all, IDictionary? own)
    {
        object? Raw(string key) => own != null && own.Contains(key) && own[key] is not null ? own[key] : all?[key];
        float One(object? raw, string key, float max)
        {
            if (raw is null) return 3f;
            var v = Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture);
            return v >= 1f && v <= max && float.IsFinite(v) ? v
                : throw new ArgumentException($"eyeWheel {key} runs from 1 to {max} (3 is the normal awake setting), not {v}.");
        }
        (float Inner, float Outer) Ends(string key, float max)
        {
            var raw = Raw(key);
            if (JsInterop.AsDict(raw) is { } ends)
            {
                RefuseUnknownHeadParameters(ends, ["inner", "outer"], $"eyeWheel {key} end");
                return (One(ends["inner"], key + ".inner", max), One(ends["outer"], key + ".outer", max));
            }
            var v = One(raw, key, max);
            return (v, v);
        }
        float? Line(string area)
        {
            var given = JsInterop.AsDict(Raw("lines"));
            if (given is null) return null;
            RefuseUnknownHeadParameters(given, EyeLineAreas, "eyeWheel lines area");
            return given[area] is null ? null : Math.Clamp(Num(given, area, 0f), 0f, 1f);
        }

        var (topInner, topOuter) = Ends("browTop", 3f);
        var (bottomInner, bottomOuter) = Ends("browBottom", 5f);
        return new EyeWheelSide(topInner, topOuter, bottomInner, bottomOuter, One(Raw("fold"), "fold", 3f), One(Raw("upperLid"), "upperLid", 5f),
            One(Raw("lowerLid"), "lowerLid", 4f), Line("A"), Line("B"), Line("C"));
    }

    /// <summary>Moves one side's brow, fold and lids to the settings, measured against the head's own normal.</summary>
    static void ApplyEyeWheel(Dictionary<string, object?> head, string side, EyeWheelSide s)
    {
        if (head[side + "Brow"] is Dictionary<string, object?> brow)
        {
            var th = Num(brow, "thickness", 1f);
            var step = BrowEdgeStep * th;
            // Each end's two edges move on their own; the brow follows their middle. Its hair thickens or thins only a
            // little between them: the edges are skin as much as hair.
            (float Dy, float Th) End(float top, float bottom)
            {
                float dyTop = -(3f - top) * step, dyBottom = (bottom - 3f) * step;
                return ((dyTop + dyBottom) / 2f, th + (0.35f * (dyBottom - dyTop)));
            }
            var inner = End(s.TopInner, s.BottomInner);
            var outer = End(s.TopOuter, s.BottomOuter);
            void Move(string key, float dy)
            {
                var p = ExtractPoint(brow[key]);
                brow[key] = ToDict(new Point2D(p.X, p.Y + dy));
            }
            Move("inner", inner.Dy);
            Move("outer", outer.Dy);
            Move("peak", inner.Dy + ((outer.Dy - inner.Dy) * 0.66f));
            brow["thickness"] = MathF.Max(th * 0.3f, (inner.Th + outer.Th) / 2f);
        }
        if (head[side + "Eye"] is Dictionary<string, object?> eye)
        {
            float w = Num(eye, "width", 1f), h = Num(eye, "height", w * CanonOpenness), lift = Num(eye, "lowerLift", 0f), foldLift = Num(eye, "foldLift", 0f);
            float up = w * (h / MathF.Max(1e-3f, w)), drop = MathF.Max(-up * 0.35f, (up * LidFloor) - lift);
            // Where the fold's highest point sits now, measured off the lid curve the eye drawer uses.
            static float LidTop(IDictionary e) { var l = EyeLids(e); return Cubic(l.Inner, l.Cp1, l.Cp2, l.Outer, FoldPeakT).Y; }
            var normalFoldY = LidTop(eye) - ((up * 0.32f) + foldLift);

            var newUp = up * Setting(UpperLidSettings, s.Upper);
            eye["height"] = h * Setting(UpperLidSettings, s.Upper);
            // The lower lid keeps its own place: its drop is set against the corners, whatever the upper lid does.
            var target = drop * Setting(LowerLidSettings, s.Lower);
            eye["lowerLift"] = (newUp * LidFloor) - target;

            // The fold sits where its setting puts it, never onto the lid: a lid opened wide runs up under it. Raised, it
            // comes up toward the brow's lower edge but never into it, or it reads as a second brow; where the head's own
            // fold already sits is always allowed, so the normal setting changes nothing.
            var highest = normalFoldY;
            if (head[side + "Brow"] is Dictionary<string, object?> overBrow)
                highest = MathF.Min(normalFoldY, ExtractPoint(overBrow["peak"]).Y + (Num(overBrow, "thickness", 0f) / 2f) + (w * 0.08f));
            var foldY = MathF.Max(normalFoldY - (up * Setting(FoldSettings, s.Fold)), highest);
            var placed = (LidTop(eye) - foldY) - (newUp * 0.32f);
            eye["foldLift"] = MathF.Abs(placed - foldLift) < 1e-3f ? foldLift : placed;
        }
    }

    /// <summary>
    /// Draws the skin lines of Hamm's eye wheel (p. 10) and <b>returns them</b>: <c>between</c> (area A, between the
    /// brows), <c>forehead</c> (B) and <c>outer</c> (C, at the outer corners).
    /// </summary>
    /// <remarks>
    /// Their strength, 0–1 per side, comes from the head's <c>eyeLines</c>, which <c>eyeWheel</c> sets, or from the
    /// options <c>A</c>, <c>B</c> and <c>C</c>, which set both sides. A strength of 0 draws nothing.
    /// </remarks>
    public Dictionary<string, object?> DrawEyeLines(CanvasRenderingContext2D ctx, object headObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("drawEyeLines needs a head, such as one from Drawing.eyeWheel(...).", nameof(headObj));
        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, EyeLineOptions, "drawEyeLines option");
        var inkColor = opt?["inkColor"]?.ToString() ?? "#0a0a0c";
        var medium = ReadMedium(opt, "drawEyeLines");
        var weight = Fraction(opt, "weight", 1f);
        var stored = JsInterop.AsDict(head["eyeLines"]);

        float Strength(string side, string area) =>
            opt != null && opt.Contains(area) && opt[area] is not null ? Math.Clamp(Num(opt, area, 0f), 0f, 1f)
            : Math.Clamp(Num(JsInterop.AsDict(stored?[side]), area, 0f), 0f, 1f);

        var nearBrow = JsInterop.AsDict(head["nearBrow"]);
        var farBrow = JsInterop.AsDict(head["farBrow"]);
        var axisX = (ExtractPoint(nearBrow?["inner"]).X + ExtractPoint(farBrow?["inner"]).X) / 2f;

        var between = new CanvasPath();
        var forehead = new CanvasPath();
        var outerLines = new CanvasPath();
        var marks = new List<(CanvasPath Mark, float Alpha)>();

        foreach (var side in new[] { "near", "far" })
        {
            var eye = JsInterop.AsDict(head[side + "Eye"]);
            var brow = JsInterop.AsDict(head[side + "Brow"]);
            if (eye is null || brow is null) continue;
            var lids = EyeLids(eye);
            var w = lids.W;
            var ink = Tier(LowerLidTier, w, EyeWidthAt240, weight) * TaperGain;
            Point2D bIn = ExtractPoint(brow["inner"]), bPeak = ExtractPoint(brow["peak"]), bOut = ExtractPoint(brow["outer"]);
            var th = Num(brow, "thickness", w * 0.24f);

            // A: a short vertical crease between the brows, rising from the brow's inner end and leaning in.
            var a = Strength(side, "A");
            if (a > 0.01f)
            {
                var x = bIn.X + ((axisX - bIn.X) * 0.4f);
                Point2D from = new(x, bIn.Y), to = new(x + ((axisX - x) * 0.2f), bIn.Y - th - (w * 0.22f * (0.5f + (0.5f * a))));
                var mark = CreateTaperedStrokePath(from, Lerp(from, to, 1f / 3f), Lerp(from, to, 2f / 3f), to, ink * 0.6f);
                between.AddPath(mark);
                marks.Add((mark, 0.35f + (0.65f * a)));
            }

            // B: lines across the forehead, well up from the brow and nearly level, not echoes of its arch, or the lowest
            // reads as a second brow; more of them as the brows rise.
            var b = Strength(side, "B");
            if (b > 0.01f)
            {
                var count = Math.Max(1, (int)MathF.Ceiling(3f * b));
                var browTop = MathF.Min(bIn.Y, MathF.Min(bPeak.Y, bOut.Y)) - (th / 2f);
                for (var k = 0; k < count; k++)
                {
                    var y = browTop - (w * 0.4f) - (k * w * 0.24f);
                    var x0 = bIn.X + ((axisX - bIn.X) * 0.2f);
                    var x2 = Lerp(bPeak, bOut, 0.5f).X;
                    Point2D p0 = new(x0, y + (w * 0.02f)), p1 = new((x0 + x2) / 2f, y - (w * 0.03f)), p2 = new(x2, y + (w * 0.03f));
                    var cp = ControlThrough(p0, p1, p2);
                    var mark = CreateTaperedStrokePath(p0, Lerp(p0, cp, 2f / 3f), Lerp(p2, cp, 2f / 3f), p2, ink * 0.8f);
                    forehead.AddPath(mark);
                    marks.Add((mark, (0.3f + (0.7f * b)) * (1f - (0.2f * k))));
                }
            }

            // C: crow's feet, short lines fanning out from beyond the outer corner.
            var c = Strength(side, "C");
            if (c > 0.01f)
            {
                var count = 1 + (int)MathF.Round(2f * c);
                var start = new Point2D(lids.Outer.X + (lids.Dir * w * 0.1f), lids.Outer.Y);
                var length = w * 0.28f * (0.6f + (0.4f * c));
                for (var k = 0; k < count; k++)
                {
                    var angle = (count == 1 ? 0f : -30f + (60f * k / (count - 1))) * MathF.PI / 180f;
                    Point2D from = new(start.X, start.Y + (MathF.Sin(angle) * w * 0.05f));
                    Point2D to = new(from.X + (lids.Dir * MathF.Cos(angle) * length), from.Y + (MathF.Sin(angle) * length));
                    var mark = CreateTaperedStrokePath(from, Lerp(from, to, 1f / 3f), Lerp(from, to, 2f / 3f), to, ink * 0.8f);
                    outerLines.AddPath(mark);
                    marks.Add((mark, 0.35f + (0.65f * c)));
                }
            }
        }

        ctx.Save();
        ApplyMedium(ctx, medium);
        ctx.FillStyle = InMedium(medium, inkColor, 0.35f);
        foreach (var (mark, alpha) in marks)
        {
            ctx.GlobalAlpha = Math.Clamp(alpha, 0f, 1f);
            ctx.Fill(mark);
        }
        ctx.Restore();

        return new Dictionary<string, object?> { ["between"] = between, ["forehead"] = forehead, ["outer"] = outerLines };
    }
    #endregion
}
