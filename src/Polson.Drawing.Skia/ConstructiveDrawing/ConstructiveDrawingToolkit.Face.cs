namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Drawing Media
    /// <summary>The medium a feature drawer was asked to draw in, or null for solid ink.</summary>
    static BrushPreset? ReadMedium(IDictionary? opt, string who)
    {
        if (opt is null || !opt.Contains("medium") || opt["medium"] is null) return null;
        return opt["medium"] as BrushPreset
            ?? throw new ArgumentException($"{who}'s medium is a brush from Skia.Brush, such as Skia.Brush.pencil(); it was given {opt["medium"]!.GetType().Name}.");
    }

    /// <summary>A colour as the medium lays it down: its grain in that colour, or the plain colour for solid ink.</summary>
    /// <remarks><paramref name="dryness"/> below 1 lays a denser deposit, for marks only a few pixels wide.</remarks>
    static object InMedium(BrushPreset? medium, string color, float dryness = 1f) => medium?.GrainIn(color, dryness) is { } grain ? grain : color;

    /// <summary>The same, always as a shader, so it can be masked by a gradient.</summary>
    static SKShader ShaderInMedium(BrushPreset? medium, string color) =>
        medium?.GrainIn(color) ?? SKShader.CreateColor(SkiaColorParser.Parse(color));

    /// <summary>Puts the medium's path texture and edge on the context. Call inside the drawer's own save.</summary>
    static void ApplyMedium(CanvasRenderingContext2D ctx, BrushPreset? medium)
    {
        if (medium is null) return;
        ctx.PathEffect = medium.Texture;
        ctx.MaskFilter = medium.Edge;
    }

    /// <summary>
    /// Fills <paramref name="shape"/> with <paramref name="paint"/> at alpha <paramref name="alphaFrom"/> at
    /// <paramref name="from"/>, grading to <paramref name="alphaTo"/> at <paramref name="to"/>.
    /// </summary>
    static void FillGraded(CanvasRenderingContext2D ctx, CanvasPath shape, SKShader paint,
                           Point2D from, Point2D to, float alphaFrom, float alphaTo, float softness = 0f)
    {
        using var ramp = SKShader.CreateLinearGradient(
            new SKPoint(from.X, from.Y), new SKPoint(to.X, to.Y),
            [new SKColor(0, 0, 0, (byte)(Math.Clamp(alphaFrom, 0f, 1f) * 255f)), new SKColor(0, 0, 0, (byte)(Math.Clamp(alphaTo, 0f, 1f) * 255f))],
            null, SKShaderTileMode.Clamp);
        ctx.Save();
        ctx.FillStyle = SKShader.CreateCompose(ramp, paint, SKBlendMode.SrcIn);
        ctx.MaskFilter = softness > 0.05f ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, softness) : null;
        ctx.Fill(shape);
        ctx.Restore();
    }

    static readonly string[] ToneOptions = ["color", "amount", "from", "to", "softness", "medium"];

    /// <summary>
    /// Lays graded tone into a shape: full <c>amount</c> at <c>from</c>, nothing at <c>to</c>, multiplied over
    /// what is there so it darkens rather than covers. Without <c>from</c> and <c>to</c> the tone is flat.
    /// </summary>
    /// <remarks>
    /// Hamm's values are graded — dark at the core of a form, fading with no edge (<i>Drawing the Head and
    /// Figure</i>, p. 7, steps 8–9). <c>softness</c> blurs the shape's own edge by that many pixels, and a
    /// <c>medium</c> puts the tone down as that medium's grain, so pencil tone reads as pencil.
    /// </remarks>
    public CanvasPath DrawTone(CanvasRenderingContext2D ctx, CanvasPath shape, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(shape);
        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, ToneOptions, "drawTone option");

        var medium = ReadMedium(opt, "drawTone");
        var paint = ShaderInMedium(medium, opt?["color"]?.ToString() ?? "#2a2a2a");
        var amount = Math.Clamp(Num(opt, "amount", 0.35f), 0f, 1f);
        var b = shape.Path.TightBounds;
        var hasRamp = opt != null && opt.Contains("from") && opt["from"] is not null && opt.Contains("to") && opt["to"] is not null;
        var from = hasRamp ? ExtractPoint(opt!["from"]) : new Point2D(b.Left, b.MidY);
        var to = hasRamp ? ExtractPoint(opt!["to"]) : new Point2D(b.Right, b.MidY);

        ctx.Save();
        ctx.GlobalCompositeOperation = "multiply";
        FillGraded(ctx, shape, paint, from, to, amount, hasRamp ? 0f : amount, MathF.Max(0f, Num(opt, "softness", 0f)));
        ctx.Restore();
        return shape;
    }

    /// <summary>
    /// The socket between a brow and its eye: under the brow's line, down to the outer corner, and back along the
    /// upper lid. The shape <c>drawTone</c> shades to set an eye into its head.
    /// </summary>
    public CanvasPath CreateEyeSocket(object eyeObj, object browObj)
    {
        if (JsInterop.AsDict(eyeObj) is not IDictionary eye || JsInterop.AsDict(browObj) is not IDictionary brow)
            throw new ArgumentException("createEyeSocket needs an eye and its brow, such as head.nearEye and head.nearBrow.");
        var lids = EyeLids(eye);
        Point2D bi = ExtractPoint(brow["inner"]), bp = ExtractPoint(brow["peak"]), bo = ExtractPoint(brow["outer"]);
        var crest = ControlThrough(bi, bp, bo);

        var socket = new CanvasPath();
        socket.MoveTo(bi.X, bi.Y);
        socket.QuadraticCurveTo(crest.X, crest.Y, bo.X, bo.Y);
        socket.LineTo(lids.Outer.X + lids.Dir * lids.W * 0.08f, lids.Outer.Y);
        socket.BezierCurveTo(lids.Cp2.X, lids.Cp2.Y, lids.Cp1.X, lids.Cp1.Y, lids.Inner.X, lids.Inner.Y);
        socket.ClosePath();
        return socket;
    }

    /// <summary>An eye's lid geometry, shared by the drawer and the socket.</summary>
    readonly record struct EyeLidFrame(Point2D Inner, Point2D Outer, Point2D Center, float W, float Dir, float Up, float LowerDrop,
                                       Point2D Cp1, Point2D Cp2);

    static EyeLidFrame EyeLids(IDictionary eye)
    {
        var inner = ExtractPoint(eye["inner"]);
        var outer = ExtractPoint(eye["outer"]);
        var center = ExtractPoint(eye["center"]);
        var w = MathF.Abs(outer.X - inner.X);
        if (w <= 0.1f) w = 24f;
        var dir = outer.X > inner.X ? 1f : -1f;

        // **The lid aperture, read from the eye rather than fixed here.** `height` is written by
        // CreateLoomisHead and was read by nothing until 2026-09-18, so the whole opening was a
        // hard-coded fraction of the eye's drawn width and no expression could close an eye.
        //
        // Taken as a RATIO against `width` rather than as an absolute, for two reasons. It is
        // scale- and yaw-invariant, so a projected far eye narrows without also closing; and at the
        // canon's own 0.45 it reproduces the previous constants exactly, which is what lets this
        // change render every existing script byte-identically.
        var up = w * Ratio(eye, "height", "width", CanonOpenness);

        // **The lower lid moves on its own** (`lowerLift`, pixels it has risen), which is what Hamm's eye wheel
        // needs and his laugh shows: the lower lid pushes up over the iris while the upper stays (p. 9). It may
        // rise a little past the corners' line, never further.
        var lowerDrop = MathF.Max(-up * 0.35f, (up * LidFloor) - Num(eye, "lowerLift", 0f));
        return new EyeLidFrame(inner, outer, center, w, dir, up, lowerDrop,
            new Point2D(inner.X + dir * w * 0.3f, inner.Y - up), new Point2D(inner.X + dir * w * 0.7f, inner.Y - up * LidCrest));
    }

    static Point2D Cubic(Point2D a, Point2D b, Point2D c, Point2D d, float t)
    {
        var u = 1f - t;
        return new Point2D(u * u * u * a.X + 3f * u * u * t * b.X + 3f * u * t * t * c.X + t * t * t * d.X,
                           u * u * u * a.Y + 3f * u * u * t * b.Y + 3f * u * t * t * c.Y + t * t * t * d.Y);
    }

    static Point2D Quad(Point2D a, Point2D c, Point2D b, float t)
    {
        var u = 1f - t;
        return new Point2D(u * u * a.X + 2f * u * t * c.X + t * t * b.X, u * u * a.Y + 2f * u * t * c.Y + t * t * b.Y);
    }
    #endregion

    #region Comic Feature Drawing Helpers
    /// <summary>
    /// Draws the eye and <b>returns the parts it built</b>, each as a <see cref="CanvasPath"/>:
    /// <c>aperture</c>, <c>iris</c>, <c>pupil</c>, <c>catchlight</c>, <c>upperLid</c>, <c>lowerLid</c>,
    /// <c>fold</c>, <c>lowerRim</c>, <c>innerCorner</c> and <c>lashes</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>aperture</c> is the one worth having: it is the shape the sclera fills and the shape the
    /// interior is clipped to, so it is what a caller clips a highlight, a cast shadow from the brow,
    /// or a reflected window into. Reconstructing it by hand means re-deriving the eyelid curve from
    /// <c>inner</c>, <c>outer</c> and the eye's own width, which is exactly the duplication these
    /// return values exist to prevent.
    /// </para>
    /// <para>
    /// The lids, the fold and the rim are open centre-lines rather than filled shapes, so they can be re-stroked
    /// at a different weight — Studio Manual 03's tier hierarchy is a decision about weight, and an eye drawn at
    /// panel size wants a different one from an eye in close-up.
    /// </para>
    /// <para>
    /// <b>Hamm's construction</b> (<i>Drawing the Head and Figure</i>, p. 7, with pp. 8–9): the fold of the upper
    /// lid (step 10); the lower lid's inner outline faded away so the eye does not look hard (step 6); a small
    /// wedge at the inner corner (step 7); the lower lid's margin, which shows from midway to the outer corner
    /// (p. 8). These are on by default; <c>detail: 0</c> draws the plain eye. <c>lashes</c> (0–1, default 0) adds
    /// sweeping lashes at the outer top and short clusters at the outer bottom, none at the inner corner (steps
    /// 5–6, p. 9). <c>tone</c> (0–1, default 0) adds his values: the iris darkening to its rim, and the shadow of
    /// the upper lid over the iris and the white (steps 8–9).
    /// </para>
    /// <para>
    /// <c>medium</c> takes a brush from <c>Skia.Brush</c>, and every ink line, fill and tone is laid down in it, so
    /// a pencil eye is pencil. The white of the eye and the highlight stay paper.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> DrawComicEye(CanvasRenderingContext2D ctx, object eyeObj, bool isFar = false, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(eyeObj) is not IDictionary eye) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var irisColor = optDict?["irisColor"]?.ToString() ?? "#3b6884";
        var scleraColor = optDict?["scleraColor"]?.ToString() ?? "#f1f4f7";
        var medium = ReadMedium(optDict, "drawComicEye");

        // A multiplier on the feature's own weights rather than a pixel count, as `drawComicBrow`'s
        // `thickness` already is — so a tier chosen here survives a change of head size.
        var weight = Fraction(optDict, "weight", 1f);
        var detail = Math.Clamp(Num(optDict, "detail", 1f), 0f, 1f);
        var foldAmount = detail > 0f ? MathF.Max(0f, Num(optDict, "fold", 1f)) : 0f;
        var lashes = Math.Clamp(Num(optDict, "lashes", 0f), 0f, 1f);
        var tone = Math.Clamp(Num(optDict, "tone", 0f), 0f, 1f);

        var lids = EyeLids(eye);
        Point2D inner = lids.Inner, outer = lids.Outer, center = lids.Center;
        float w = lids.W, dir = lids.Dir, up = lids.Up, lowerDrop = lids.LowerDrop;

        // **The iris, as a settable fraction of the eye's own width, defaulting to the comic canon.**
        // 0.32 is a deliberately large iris and is what every existing script draws; the measured
        // figure for a real face is **0.19**, taken off the iris ring in MediaPipe's canonical face
        // model — iris diameter is 0.1886 of the interpupillary distance there, and this
        // construction places the pupils one eye-width either side of the axis, so IPD is exactly
        // twice `w` and the two ratios are the same number. **Ours is therefore 1.70x life size**,
        // which is the comic idiom rather than an error: Manual 23 §1 measures the comic head as
        // narrower relative to its eye than Loomis's, and a bigger iris is the other half of that.
        // Left as the default for the reason `CanonOpenness` was: moving it would restyle every
        // face already drawn.
        var irisR = w * Fraction(optDict, "irisRatio", CanonIrisRatio);
        var irisX = center.X + dir * w * 0.08f;
        var irisY = center.Y - 1f;

        // The eyelid curve is used three times — filled as the sclera, as the clip for the interior,
        // and stroked as the upper lid — so it is built once.
        var upperLid = new CanvasPath();
        upperLid.MoveTo(inner.X, inner.Y);
        upperLid.BezierCurveTo(lids.Cp1.X, lids.Cp1.Y, lids.Cp2.X, lids.Cp2.Y, outer.X, outer.Y);

        var aperture = new CanvasPath(upperLid);
        aperture.QuadraticCurveTo(center.X, inner.Y + lowerDrop, inner.X, inner.Y);
        aperture.ClosePath();

        var iris = Disc(new Point2D(irisX, irisY), irisR);
        var pupil = Disc(new Point2D(irisX, irisY), irisR * 0.45f);
        var catchlight = Disc(new Point2D(irisX - irisR * 0.25f, irisY - irisR * 0.25f), irisR * 0.22f);

        // The lower lid, its start following the lid as it rises so a lifted lid stays one curve.
        var lowerStart = new Point2D(inner.X + dir * w * 0.2f, inner.Y + lowerDrop * (LowerLidStart / LidFloor));
        var lowerCtrl = new Point2D(center.X, inner.Y + lowerDrop);
        var lowerEnd = new Point2D(outer.X - dir * w * 0.1f, outer.Y);
        var lowerLid = new CanvasPath();
        lowerLid.MoveTo(lowerStart.X, lowerStart.Y);
        lowerLid.QuadraticCurveTo(lowerCtrl.X, lowerCtrl.Y, lowerEnd.X, lowerEnd.Y);

        // Step 10: the fold of the upper lid, a strip just above it — "narrow or wide" — following the lid's own
        // curve, closing in toward the corners and on the lid as the lid lowers.
        var fold = new CanvasPath();
        if (foldAmount > 0f)
        {
            // The gap peaks a little inside the middle and closes toward both ends; the lid is sampled and each
            // point lifted, then the points joined through their midpoints so the fold is one smooth curve.
            var g = up * 0.32f * foldAmount;
            const int n = 14;
            var pts = new Point2D[n];
            for (var i = 0; i < n; i++)
            {
                var s = i / (float)(n - 1);
                var p = Cubic(inner, lids.Cp1, lids.Cp2, outer, 0.16f + 0.81f * s);
                pts[i] = new Point2D(p.X, p.Y - g * (0.35f + 0.65f * MathF.Sin(MathF.PI * MathF.Pow(s, 0.85f))));
            }

            fold.MoveTo(pts[0].X, pts[0].Y);
            for (var i = 1; i < n - 1; i++)
                fold.QuadraticCurveTo(pts[i].X, pts[i].Y, (pts[i].X + pts[i + 1].X) * 0.5f, (pts[i].Y + pts[i + 1].Y) * 0.5f);
            fold.LineTo(pts[n - 1].X, pts[n - 1].Y);
        }

        // p. 8: the lower lid's margin squares off from midway to the outer corner, a light line below the lid.
        var lowerRim = new CanvasPath();
        if (detail > 0f)
        {
            var gap = new Point2D(0f, w * 0.055f);
            Point2D On(float t) { var p = Quad(lowerStart, lowerCtrl, lowerEnd, t); return new Point2D(p.X + gap.X, p.Y + gap.Y); }
            var mid = ControlThrough(On(0.45f), On(0.69f), On(0.93f));
            lowerRim.MoveTo(On(0.45f).X, On(0.45f).Y);
            lowerRim.QuadraticCurveTo(mid.X, mid.Y, On(0.93f).X, On(0.93f).Y);
        }

        // Step 7: a small wedge at the inner corner.
        var innerCorner = new CanvasPath();
        if (detail > 0f)
        {
            innerCorner.MoveTo(inner.X, inner.Y);
            innerCorner.LineTo(inner.X + dir * w * 0.075f, inner.Y - up * 0.12f);
            innerCorner.LineTo(inner.X + dir * w * 0.065f, inner.Y + MathF.Max(up * 0.06f, lowerDrop * 0.2f));
            innerCorner.ClosePath();
        }

        // Steps 5–6 and p. 9: lashes sweep up and out from the outer part of the upper lid and cluster short below
        // the outer part of the lower; none grow at the inner corner.
        var lashMarks = new CanvasPath();
        if (lashes > 0f)
        {
            var thick = w * 0.028f * weight;
            Point2D Away(Point2D from, Point2D tangent, bool upward, float turnDeg)
            {
                var l = MathF.Max(1e-4f, MathF.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y));
                float nx = -tangent.Y / l, ny = tangent.X / l;
                if ((ny < 0f) != upward) (nx, ny) = (-nx, -ny);
                var a = turnDeg * MathF.PI / 180f;
                float rx1 = nx * MathF.Cos(a) - ny * MathF.Sin(a), ry1 = nx * MathF.Sin(a) + ny * MathF.Cos(a);
                float rx2 = nx * MathF.Cos(-a) - ny * MathF.Sin(-a), ry2 = nx * MathF.Sin(-a) + ny * MathF.Cos(-a);
                return rx1 * dir >= rx2 * dir ? new Point2D(rx1, ry1) : new Point2D(rx2, ry2);
            }

            void Lash(Point2D at, Point2D d, float length, float curl)
            {
                var tip = new Point2D(at.X + d.X * length + dir * length * 0.2f, at.Y + d.Y * length + curl * length);
                var mark = CreateTaperedStrokePath(at,
                    new Point2D(at.X + d.X * length * 0.4f, at.Y + d.Y * length * 0.4f),
                    new Point2D(at.X + d.X * length * 0.8f, at.Y + d.Y * length * 0.8f),
                    tip, thick);
                lashMarks.AddPath(mark);
            }

            var upperCount = 4 + (int)MathF.Round(6f * lashes);
            for (var i = 0; i < upperCount; i++)
            {
                var s = upperCount == 1 ? 0.5f : i / (float)(upperCount - 1);
                var t = 0.5f + 0.48f * s;
                var p = Cubic(inner, lids.Cp1, lids.Cp2, outer, t);
                var q = Cubic(inner, lids.Cp1, lids.Cp2, outer, MathF.Min(1f, t + 0.01f));
                var tangent = new Point2D(q.X - p.X, q.Y - p.Y);
                Lash(p, Away(p, tangent, true, 15f + 45f * s), w * (0.07f + 0.13f * s) * (0.6f + 0.4f * lashes), -0.15f);
            }

            var lowerCount = 3 + (int)MathF.Round(3f * lashes);
            for (var i = 0; i < lowerCount; i++)
            {
                // In pairs: Hamm's "abbreviated clusters".
                var s = lowerCount == 1 ? 0.5f : i / (float)(lowerCount - 1);
                var t = 0.58f + 0.36f * s + (i % 2 == 1 ? -0.025f : 0f);
                var p = Quad(lowerStart, lowerCtrl, lowerEnd, t);
                var q = Quad(lowerStart, lowerCtrl, lowerEnd, MathF.Min(1f, t + 0.01f));
                var tangent = new Point2D(q.X - p.X, q.Y - p.Y);
                Lash(p, Away(p, tangent, false, 25f), w * 0.06f * (0.6f + 0.4f * lashes), 0.1f);
            }
        }

        ctx.Save();
        ApplyMedium(ctx, medium);

        // 1. Sclera — paper, in any medium — then the interior, clipped to the lids.
        ctx.Save();
        ctx.FillStyle = scleraColor;
        ctx.Fill(aperture);
        ctx.Clip(aperture);

        // 2. Iris & Pupil. Hamm's values (step 8): the iris darkens toward its rim.
        ctx.FillStyle = InMedium(medium, irisColor);
        ctx.Fill(iris);
        if (tone > 0f)
        {
            using var rim = SKShader.CreateRadialGradient(new SKPoint(irisX, irisY), irisR,
                [SKColors.Transparent, new SKColor(0, 0, 0, (byte)(150f * tone))], [0.45f, 1f], SKShaderTileMode.Clamp);
            ctx.Save();
            ctx.FillStyle = SKShader.CreateCompose(rim, ShaderInMedium(medium, inkColor), SKBlendMode.SrcIn);
            ctx.Fill(iris);
            ctx.Restore();
        }
        ctx.StrokeStyle = InMedium(medium, inkColor);
        ctx.LineWidth = Tier(IrisRimTier, w, EyeWidthAt240, weight);
        ctx.Stroke(iris);                 // dark iris rim

        ctx.FillStyle = InMedium(medium, inkColor);
        ctx.Fill(pupil);

        // Step 9: the upper lid's shadow over the iris and the white, fading downward.
        if (tone > 0f)
        {
            ctx.Save();
            ctx.GlobalCompositeOperation = "multiply";
            FillGraded(ctx, aperture, ShaderInMedium(medium, inkColor),
                new Point2D(center.X, inner.Y - up), new Point2D(center.X, inner.Y - up * 0.05f), 0.55f * tone, 0f);
            ctx.Restore();
        }

        // The highlight stays paper: Hamm puts it in with opaque white or an eraser.
        ctx.FillStyle = "#ffffff";
        ctx.Fill(catchlight);

        ctx.Restore();

        // 3. Thick Inked S-Curve Upper Eyelid, heavier when lashes grow out of it (step 10).
        ctx.StrokeStyle = InMedium(medium, inkColor);
        ctx.LineWidth = Tier(LidTier, w, EyeWidthAt240, weight * (isFar ? FarFeatureWeight : 1f) * (1f + 0.35f * lashes));
        ctx.LineCap = "round";
        ctx.Stroke(upperLid);

        // 4. Delicate Lower Eyelid, fading out toward the inner corner (step 6).
        ctx.LineWidth = Tier(LowerLidTier, w, EyeWidthAt240, weight);
        if (detail > 0f)
        {
            using var fade = SKShader.CreateLinearGradient(
                new SKPoint(lowerStart.X, 0f), new SKPoint(center.X, 0f),
                [new SKColor(0, 0, 0, (byte)(255f * (1f - 0.85f * detail))), SKColors.Black], null, SKShaderTileMode.Clamp);
            ctx.StrokeStyle = SKShader.CreateCompose(fade, ShaderInMedium(medium, inkColor), SKBlendMode.SrcIn);
        }
        ctx.Stroke(lowerLid);

        // 5. Hamm's additions: the fold, the lower lid's margin, the inner corner, the lashes.
        ctx.StrokeStyle = InMedium(medium, inkColor);
        if (foldAmount > 0f)
        {
            ctx.LineWidth = Tier(LowerLidTier, w, EyeWidthAt240, weight * 0.85f);
            ctx.Stroke(fold);
        }
        if (detail > 0f)
        {
            ctx.Save();
            ctx.GlobalAlpha = 0.6f;
            ctx.LineWidth = Tier(IrisRimTier, w, EyeWidthAt240, weight * 0.8f);
            ctx.Stroke(lowerRim);
            ctx.GlobalAlpha = 0.5f;
            ctx.FillStyle = InMedium(medium, inkColor);
            ctx.Fill(innerCorner);
            ctx.Restore();
        }
        if (lashes > 0f)
        {
            ctx.FillStyle = InMedium(medium, inkColor, 0.35f);
            ctx.Fill(lashMarks);
        }

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["aperture"] = aperture,
            ["iris"] = iris,
            ["pupil"] = pupil,
            ["catchlight"] = catchlight,
            ["upperLid"] = upperLid,
            ["lowerLid"] = lowerLid,
            ["fold"] = fold,
            ["lowerRim"] = lowerRim,
            ["innerCorner"] = innerCorner,
            ["lashes"] = lashMarks
        };
    }

    /// <summary>
    /// Draws one eyebrow and <b>returns the parts it built</b>: <c>mass</c> (the filled brow) and
    /// <c>spine</c> (its centre-line, through the peak).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This exists so the brow stations are not another <c>eye.height</c>.</b> That field was
    /// written by <see cref="CreateLoomisHead"/> and read by nothing for as long as it existed, so
    /// every expression that meant to narrow a lid silently did nothing. Inner and outer brow
    /// stations without a call that draws them would be the same defect a second time: AU1 and AU2
    /// would differ in the landmark data and be indistinguishable in every render, which is the one
    /// outcome worse than not separating them at all.
    /// </para>
    /// <para>
    /// <b>The weight is read from the brow's own <c>thickness</c>, not derived from its stations.</b>
    /// A brow foreshortens horizontally and not vertically, so weight taken from the projected span
    /// would return a far brow 45% too thin at the yaw clamp — the same trap <c>drawComicEye</c>
    /// avoids by reading its aperture as a ratio. <b>But the arch is not the fix either</b>, and the
    /// first version of this call used it: <c>|peak.y − inner.y|</c> is vertical, yet it is also a
    /// quantity the Action Units <i>move</i>. AU1 raises the inner end toward the peak, so
    /// <c>sadness</c> flattens the arch — measured on a 760px head it fell from <b>15.2px to
    /// 0.9px</b> and the brow drew as a hairline. A measurement held apart from the stations, exactly
    /// as the eye holds <c>width</c> and <c>height</c>, is immune to the expression and to the turn
    /// alike.
    /// </para>
    /// <para>
    /// <c>isFar</c> thins the mass to match the lighter stroke <see cref="DrawComicEye"/> already
    /// gives a far eye, so a far brow and the eye under it read at one weight.
    /// </para>
    /// <para>
    /// <c>options</c>: <c>inkColor</c>, and <c>thickness</c> as a multiplier on the default rather
    /// than a pixel count, so it survives a change of head size.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> DrawComicBrow(CanvasRenderingContext2D ctx, object browObj, bool isFar = false, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(browObj) is not IDictionary brow) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var medium = ReadMedium(optDict, "drawComicBrow");
        var weight = optDict is not null && optDict.Contains("thickness")
            ? Convert.ToSingle(optDict["thickness"], CultureInfo.InvariantCulture) : 1f;

        var inner = ExtractPoint(brow["inner"]);
        var peak = ExtractPoint(brow["peak"]);
        var outer = ExtractPoint(brow["outer"]);

        // **Read, never derived from the stations.** This was `|peak.y - inner.y| * 0.85` when it was
        // written, on the reasoning that a vertical measure does not foreshorten - true, and beside
        // the point: the arch is a quantity the ACTION UNITS MOVE. AU1 lifts the inner end toward the
        // peak, so `sadness` flattens the arch, and on a 760px head it fell from 15.2px to 0.9px and
        // drew a hairline. Same shape of mistake as the brow units moving the cranium's own landmark:
        // a drawn quantity taken from something an expression displaces.
        //
        // The fallbacks are for a hand-built or legacy brow that carries no measurement, and only
        // there is the arch used - a thin brow beats no brow.
        var thickness = Convert.ToSingle(brow.Contains("thickness") ? brow["thickness"] : 0f,
                                         CultureInfo.InvariantCulture);
        if (thickness <= 0.1f) thickness = MathF.Abs(peak.Y - inner.Y) * 1.7f;
        if (thickness <= 0.1f) thickness = MathF.Abs(outer.X - inner.X) * 0.14f;
        if (thickness <= 0.1f) return [];

        var half = thickness * 0.5f * weight * (isFar ? FarFeatureWeight : 1f);

        // The stations say where the brow goes; these put the curve THROUGH the peak rather than
        // merely toward it. A quadratic's midpoint is a quarter of each end plus half the control,
        // so the control has to overshoot - aimed at the peak instead, the arch reads about half as
        // high as the landmark states and `peak` stops meaning what its name says.
        var crest = ControlThrough(inner, peak, outer);
        var spine = new CanvasPath();
        spine.MoveTo(inner.X, inner.Y);
        spine.QuadraticCurveTo(crest.X, crest.Y, outer.X, outer.Y);

        // The mass tapers to nothing at the tail and stays blunt at the head, which is the shape of a
        // brow rather than of a leaf: both ends pointed reads as a moustache set above the eye.
        var top = ControlThrough(new Point2D(inner.X, inner.Y - half), new Point2D(peak.X, peak.Y - half), outer);
        var under = ControlThrough(outer, new Point2D(peak.X, peak.Y + (half * 0.55f)), new Point2D(inner.X, inner.Y + half));

        var mass = new CanvasPath();
        mass.MoveTo(inner.X, inner.Y - half);
        mass.QuadraticCurveTo(top.X, top.Y, outer.X, outer.Y);
        mass.QuadraticCurveTo(under.X, under.Y, inner.X, inner.Y + half);
        mass.ClosePath();

        // **Hairs**, Hamm's way (Drawing the Head and Figure, p. 8): they grow obliquely away from the nose,
        // nearly upright at the brow's head; past the peak of the arch the top hairs turn down to meet the
        // under hairs, which still slant up. Each hair is placed in the brow's own frame — how far along it,
        // how far across — from a fixed seed, so an expression that moves the brow carries the same hairs with
        // it rather than reshuffling them between frames.
        var hairs = Math.Clamp(Num(optDict, "hairs", 0f), 0f, 1f);
        var hairMarks = new CanvasPath();
        if (hairs > 0f)
        {
            Point2D topInner = new(inner.X, inner.Y - half), lowInner = new(inner.X, inner.Y + half);
            Point2D Top(float s) => Quad(topInner, top, outer, s);
            Point2D Low(float s) => Quad(outer, under, lowInner, 1f - s);
            // A fixed number for the setting, never taken from the brow's current length: an expression that
            // stretches or tilts the brow must keep the same hairs, or a sequence reshuffles them frame to frame.
            var count = Math.Max(8, (int)MathF.Round(80f * hairs));
            var rng = new SeededRandom((uint)Math.Max(1f, Num(optDict, "seed", 1f)));
            var hairWidth = MathF.Max(0.35f, half * 0.12f);

            for (var i = 0; i < count; i++)
            {
                var s = (float)((i + rng.Next()) / count);
                var v = (float)rng.Next();
                Point2D a = Top(s), b = Low(s);
                var c = new Point2D(a.X + (b.X - a.X) * v, a.Y + (b.Y - a.Y) * v);
                var band = MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

                // The brow's direction here, and the normal pointing up off it.
                float tx = 2f * (1f - s) * (crest.X - inner.X) + 2f * s * (outer.X - crest.X);
                float ty = 2f * (1f - s) * (crest.Y - inner.Y) + 2f * s * (outer.Y - crest.Y);
                var tl = MathF.Max(1e-4f, MathF.Sqrt(tx * tx + ty * ty));
                (tx, ty) = (tx / tl, ty / tl);
                float nx = ty, ny = -tx;
                if (ny > 0f) (nx, ny) = (-nx, -ny);

                // Degrees above the brow's line: upright at the head, oblique along the body, and past the peak
                // down for the top hairs and still up for the under ones.
                var angle = s < 0.12f ? 68f - s / 0.12f * 26f
                          : s < 0.5f ? 42f - (s - 0.12f) / 0.38f * 18f
                          : v < 0.5f ? -22f : 18f;
                angle += (float)rng.Range(-8, 8);
                var rad = angle * MathF.PI / 180f;
                float dx = tx * MathF.Cos(rad) + nx * MathF.Sin(rad), dy = ty * MathF.Cos(rad) + ny * MathF.Sin(rad);
                var hairLength = MathF.Max(band, half * 0.4f) * (0.75f - 0.25f * s) * (float)rng.Range(0.8, 1.15);
                var bow = hairLength * 0.08f;

                var from = new Point2D(c.X - dx * hairLength * 0.5f, c.Y - dy * hairLength * 0.5f);
                hairMarks.AddPath(CreateTaperedStrokePath(from,
                    new Point2D(from.X + dx * hairLength / 3f + nx * bow, from.Y + dy * hairLength / 3f + ny * bow),
                    new Point2D(from.X + dx * hairLength * 2f / 3f + nx * bow, from.Y + dy * hairLength * 2f / 3f + ny * bow),
                    new Point2D(c.X + dx * hairLength * 0.5f, c.Y + dy * hairLength * 0.5f), hairWidth));
            }
        }

        ctx.Save();

        ApplyMedium(ctx, medium);
        ctx.FillStyle = InMedium(medium, inkColor);

        // With hairs the mass stays as a faint tone under them, which is how a pencil brow is built up.
        if (hairs > 0f)
        {
            ctx.Save();
            ctx.GlobalAlpha = 1f - 0.8f * hairs;
            ctx.Fill(mass);
            ctx.Restore();
            ctx.FillStyle = InMedium(medium, inkColor, 0.35f);
            ctx.Fill(hairMarks);
        }
        else ctx.Fill(mass);
        ctx.Restore();

        return new Dictionary<string, object?> { ["mass"] = mass, ["spine"] = spine, ["hairs"] = hairMarks };
    }

    /// <summary>
    /// Draws the nose and <b>returns the parts it built</b>: <c>underPlane</c> (the shaded plane
    /// beneath), <c>bridge</c> (the inked centre-line) and <c>nostril</c>.
    /// </summary>
    /// <remarks>
    /// <c>underPlane</c> is the one that composes: it is the plane turned away from the key, so it is
    /// what a caller re-fills when the light moves, or unions with the other shadow shapes on a face
    /// to make one cast-shadow mass. Studio Manual 03's rule is that a heavy line is the beginning of
    /// a shadow — that only becomes actionable when the shadow is a shape you hold.
    /// </remarks>
    public Dictionary<string, object?> DrawComicNose(CanvasRenderingContext2D ctx, object noseObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(noseObj) is not IDictionary nose) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var medium = ReadMedium(optDict, "drawComicNose");
        var shadowColor = optDict?["shadowColor"]?.ToString() ?? "#b06f4c";
        var weight = Fraction(optDict, "weight", 1f);

        var bridgeTop = ExtractPoint(nose["bridgeTop"]);
        var apex = ExtractPoint(nose["apex"]);
        var underNose = ExtractPoint(nose["underNose"]);
        var nearNostril = ExtractPoint(nose["nearNostril"]);

        // A nose built before the far wing existed still draws: without one, the bottom plane falls
        // back to the single-sided triangle it used to be.
        var hasFar = nose.Contains("farNostril") && nose["farNostril"] is not null;
        var farNostril = hasFar ? ExtractPoint(nose["farNostril"]) : underNose;

        // The nose's own length, which is what every weight here is measured against.
        var len = MathF.Abs(underNose.Y - bridgeTop.Y);
        if (len <= 0.1f) len = NoseLengthAt240;

        // **The bridge is a tapered mark, not a constant-width polyline.** Manual 03 §3 says outright
        // that a constant-width stroke reads as a technical drawing rather than as inking, and this
        // call was drawing one. The envelope is heaviest in the middle and vanishes at both ends,
        // which is where a nose actually carries its weight: the ball is the form, the bridge fades
        // into the brow, and the base is picked up by the nostril rather than by a line.
        //
        // The two control points run through the apex, so the mark passes over the ball rather than
        // cutting the corner between the three landmarks.
        // **The mark starts below the brow, not at it.** The landmarks run from `bridgeTop`, which is
        // halfway between brow and eye, and a mark carried all the way up from there reads as a line
        // ruled down the middle of the face — tolerable while it was a 2.4px hairline, and a wedge
        // once the weight scaled with the head. Comic practice inks the lower bridge and the ball and
        // lets the upper bridge be carried by the brow, so the run begins 45% of the way down.
        var markTop = new Point2D(
            bridgeTop.X + ((apex.X - bridgeTop.X) * 0.45f),
            bridgeTop.Y + ((apex.Y - bridgeTop.Y) * 0.45f));

        // **A frontal nose has no bridge shadow, and drawing one gives it a dagger down the middle.**
        // At yaw 0 the three landmarks are collinear and vertical, so the mark can only be a vertical
        // wedge — tolerable while it was a 2.4px hairline, and the first thing a reader saw once the
        // weight scaled. The bridge line is the break between the front plane and the side plane, so
        // it belongs to a turned head; comic practice inks a frontal nose with its nostrils and its
        // under-plane and lets the bridge go.
        //
        // How far the head has turned is recoverable from the nose itself — `apex` carries the lateral
        // offset the construction gave it — so this needs no yaw argument and works on a head that has
        // been through `applyActionUnits` or a blend.
        var turn = Math.Clamp(MathF.Abs(apex.X - underNose.X) / (len * 0.15f), 0f, 1f);
        var thickness = Tier(NoseBridgeTier, len, NoseLengthAt240,
                             weight * TaperGain * (FrontalBridge + ((1f - FrontalBridge) * turn)));
        var bridgeMark = CreateTaperedStrokePath(
            markTop,
            new Point2D(apex.X, markTop.Y + ((apex.Y - markTop.Y) * 0.6f)),
            apex,
            underNose,
            thickness);

        // **`bridge` keeps its meaning — the open centre-line — and the filled mark arrives beside it
        // as `bridgeMark`.** Changing what an existing key holds would break every caller that strokes
        // it at its own tier, which is exactly the thing these returns exist to allow.
        var bridge = new CanvasPath();
        bridge.MoveTo(bridgeTop.X, bridgeTop.Y);
        bridge.LineTo(apex.X, apex.Y);
        bridge.LineTo(underNose.X, underNose.Y);

        // **Two wings, and each lies inside the nose.** A nostril landmark is the *outer edge* of the
        // base — Gautier's one-eye width is measured edge to edge — so the wing's outermost point sits on
        // it and its centre lies inward, toward the septum. Until 2026-10-06 each wing was a 270° hook
        // centred ON the landmark, so half of it hung off the side of the nose like an earring, and on a
        // turned head the far one dangled below the nose.
        //
        // The wing bulges outward from just above the base, round its outer edge, and curls back under
        // toward the septum; the opening sits inside it, low and toward the middle. Its size comes from
        // that side's own half-width, so the far wing foreshortens with the turn by itself.
        var tierR = len * (NostrilRadiusTier / NoseLengthAt240);
        (CanvasPath Wing, CanvasPath Hole, Point2D Centre, Point2D Curl) Wing(Point2D edge)
        {
            var inward = underNose.X >= edge.X ? 1f : -1f;
            var halfSpan = MathF.Abs(underNose.X - edge.X);
            var r = halfSpan > tierR ? halfSpan * 0.26f : MathF.Max(0.5f, halfSpan * 0.4f);
            var c = new Point2D(edge.X + (inward * r), edge.Y);
            var outward = inward > 0f ? MathF.PI : 0f;
            const float up = 75f * MathF.PI / 180f, under = 85f * MathF.PI / 180f;

            var wing = new CanvasPath();
            if (inward < 0f) wing.Arc(c.X, c.Y, r, outward - up, outward + under);
            else wing.Arc(c.X, c.Y, r, outward + up, outward - under, true);

            var hole = new CanvasPath();
            hole.Ellipse(c.X + (inward * r * 0.7f), c.Y + (r * 0.55f), r * 0.5f, r * 0.24f, inward * 0.2f, 0f, MathF.PI * 2f);
            // The opening's inner end, where the bottom of the ball begins.
            return (wing, hole, c, new Point2D(c.X + (inward * r * 1.2f), c.Y + (r * 0.55f)));
        }

        var (nostril, nostrilHole, nearCentre, nearCurl) = Wing(nearNostril);
        var (farNostrilPath, farNostrilHole, farCentre, farCurl) = hasFar
            ? Wing(farNostril)
            : (new CanvasPath(), new CanvasPath(), underNose, underNose);

        // **The bottom plane is a shadow under the ball, between the wings.** Loomis's Plate 26 bounds it by
        // lines from the ball of the nose to the nostrils' lower corners, and Faragasso's front view ends at
        // the corners of the bottom plane; both describe a plane facing down. Seen from the front it is
        // foreshortened to a shallow lens, deepest under the tip and thinning to nothing at the wings, which
        // are lit. Until 2026-10-06 it was the four landmarks filled as a diamond out to the nose's outer
        // edges — a hard brown bowtie under every nose that read as a moustache.
        //
        // The top runs through a point 40% of the way from the tip down to the septum, the underside of the
        // ball; the bottom through the septum. Both pass through their points rather than toward them.
        var ballUnder = new Point2D(apex.X + ((underNose.X - apex.X) * 0.4f), apex.Y + ((underNose.Y - apex.Y) * 0.4f));
        var top = ControlThrough(farCentre, ballUnder, nearCentre);
        var bottom = ControlThrough(nearCentre, underNose, farCentre);
        var underPlane = new CanvasPath();
        underPlane.MoveTo(farCentre.X, farCentre.Y);
        underPlane.QuadraticCurveTo(top.X, top.Y, nearCentre.X, nearCentre.Y);
        underPlane.QuadraticCurveTo(bottom.X, bottom.Y, farCentre.X, farCentre.Y);
        underPlane.ClosePath();

        // The bottom of the ball: one light line under the tip, through the septum — Hamm's front-view "‿".
        // It stops short of the openings: carried out to the wings it closed the nose into a bracket, and
        // carried to the openings it joined them into a dumbbell.
        Point2D Toward(Point2D from, float share) =>
            new(from.X + ((underNose.X - from.X) * share), from.Y + ((underNose.Y - from.Y) * share));
        Point2D baseFar = Toward(farCurl, 0.4f), baseNear = Toward(nearCurl, 0.4f);
        var baseControl = ControlThrough(baseFar, new Point2D(underNose.X, underNose.Y + (len * 0.01f)), baseNear);
        var noseBase = new CanvasPath();
        noseBase.MoveTo(baseFar.X, baseFar.Y);
        noseBase.QuadraticCurveTo(baseControl.X, baseControl.Y, baseNear.X, baseNear.Y);

        ctx.Save();

        ApplyMedium(ctx, medium);

        ctx.FillStyle = InMedium(medium, shadowColor);
        ctx.Fill(underPlane);

        ctx.FillStyle = InMedium(medium, inkColor);
        ctx.Fill(bridgeMark);

        ctx.Fill(nostrilHole);
        if (hasFar) ctx.Fill(farNostrilHole);

        ctx.StrokeStyle = InMedium(medium, inkColor);
        ctx.LineCap = "round";
        if (hasFar)
        {
            ctx.LineWidth = Tier(NostrilTier, len, NoseLengthAt240, weight * 0.6f);
            ctx.Stroke(noseBase);
        }
        ctx.LineWidth = Tier(NostrilTier, len, NoseLengthAt240, weight);
        ctx.Stroke(nostril);
        if (hasFar) ctx.Stroke(farNostrilPath);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["underPlane"] = underPlane,
            ["bridge"] = bridge,
            ["bridgeMark"] = bridgeMark,
            ["nostril"] = nostril,
            ["farNostril"] = farNostrilPath,
            ["nostrilHole"] = nostrilHole,
            ["farNostrilHole"] = farNostrilHole,
            ["base"] = noseBase
        };
    }

    /// <summary>
    /// Draws one ear and <b>returns the parts it built</b>: <c>helix</c>, <c>antihelix</c>,
    /// <c>concha</c> and <c>lobe</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Until 2026-09-19 nothing drew an ear at all.</b> <c>createHeadGeometry</c> has carried
    /// <c>parts.ear</c> and <c>parts.nearEar</c> since the same week, but those are *masses* — padded
    /// ellipses unioned into the silhouette — with no internal structure whatever, so every head this
    /// studio drew wore two blank flaps. Manual 23 §9 named the gap in its own words: what the
    /// composition gives you is shape, and *"it does not shade them"*.
    /// </para>
    /// <para>
    /// <b>Loomis declines to give a canon for the shape, and that decides what this call is.</b> Plate
    /// 26: *"The real problem is much more one of setting them into the construction of the head in
    /// their correct positions than one of drawing the actual details themselves. Noses and ears vary
    /// widely in shape but not a great deal in basic construction."* So there is no measured ear to
    /// implement — the placement half is what <c>createHeadGeometry</c> already does, and this is the
    /// basic construction: an outer rim, the ridge inside it, the bowl between them, and the lobe.
    /// The proportions below are the studio's, by eye, and are not his.
    /// </para>
    /// <para>
    /// Pass <c>geo.ears.far</c> or <c>geo.ears.near</c>. An ear foreshortens the opposite way to an
    /// eye — edge-on frontally, full-face in profile — and the width in that block already carries
    /// the turn, so this call needs no yaw: a narrow ear simply draws narrow.
    /// </para>
    /// <para>
    /// <c>options</c>: <c>inkColor</c>, <c>shadowColor</c>, <c>weight</c> as a multiplier, and
    /// <c>hatch</c> (default <c>true</c>) for the cross-contour arcs inside the bowl.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> DrawComicEar(CanvasRenderingContext2D ctx, object earObj, bool isFar = false, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(earObj) is not IDictionary ear) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var medium = ReadMedium(optDict, "drawComicEar");

        // **A neutral, near-transparent bowl rather than the nose's orange under-plane.** The first
        // version borrowed `#b06f4c` from `drawComicNose` and at panel size the ear read as a bruise:
        // the concha is a hollow catching less light, not a lit plane of its own.
        var shadowColor = optDict?["shadowColor"]?.ToString() ?? "rgba(0,0,0,0.13)";
        var weight = Fraction(optDict, "weight", 1f);
        var hatch = optDict is null || !optDict.Contains("hatch") || Convert.ToBoolean(optDict["hatch"]);

        var center = ExtractPoint(ear["center"]);
        var w = Num(ear, "width", 0f);
        var h = Num(ear, "height", 0f);
        if (w <= 0.5f || h <= 0.5f) return [];

        // **An ear that no longer stands outside the skull is not inked**, because it is behind the
        // head. The masses can be unioned blind — a hidden ear adds nothing to a silhouette — but a
        // drawn one is painted on top, and past about 35 degrees of yaw the near ear's rim and bowl
        // landed on the cheek beside the near eye. Defaults to drawing, so a hand-built ear that
        // carries no such flag still draws.
        if (ear.Contains("visible") && ear["visible"] is not null && !Convert.ToBoolean(ear["visible"]))
            return [];

        // Which way the face lies. Without it the helix would run round the wrong side and the lobe
        // would sit behind the jaw rather than in front of it.
        var faceDir = Num(ear, "faceDir", -1f) >= 0f ? 1f : -1f;
        float rx = w * 0.5f, ry = h * 0.5f;
        var back = -faceDir;                       // away from the face, where the rim is widest

        var tier = isFar ? FarFeatureWeight : 1f;

        // **The helix — the outer rim, from the top of the ear round the back to the lobe.** Tapered,
        // because a rim is a rolled edge: it is fullest where it turns away from the light at the back
        // and thins where it meets the skull at either end.
        var helixTop = new Point2D(center.X + (faceDir * rx * 0.35f), center.Y - ry);
        var helixLobe = new Point2D(center.X + (faceDir * rx * 0.15f), center.Y + ry);
        var helix = CreateTaperedStrokePath(
            helixTop,
            new Point2D(center.X + (back * rx * 1.02f), center.Y - (ry * 0.70f)),
            new Point2D(center.X + (back * rx * 0.90f), center.Y + (ry * 0.52f)),
            helixLobe,
            Tier(HelixTier, h, EarHeightAt240, weight * tier * TaperGain));

        // **The antihelix — the Y-shaped ridge inside the rim**, drawn as its single strong arm. It
        // runs roughly parallel to the helix at about half the radius, which is what gives an ear its
        // depth rather than reading as a flat disc.
        var antihelix = CreateTaperedStrokePath(
            new Point2D(center.X + (faceDir * rx * 0.10f), center.Y - (ry * 0.62f)),
            new Point2D(center.X + (back * rx * 0.58f), center.Y - (ry * 0.42f)),
            new Point2D(center.X + (back * rx * 0.50f), center.Y + (ry * 0.18f)),
            new Point2D(center.X + (faceDir * rx * 0.05f), center.Y + (ry * 0.46f)),
            Tier(AntihelixTier, h, EarHeightAt240, weight * tier * TaperGain));

        // **The ear is three thirds, and each one has a name.** Gautier, p. 29: the top third is where
        // it attaches to the head, *"the second section is the largest opening, the bowl"*, and the
        // bottom third is the lobe. The first version of this call had the bowl at 0.24 of the
        // half-height and the lobe at 0.78 — by eye, and both wrong against a published proportion
        // that was sitting in a book on the shelf. A third of the half-height is 0.333.
        float cx = center.X + (faceDir * rx * 0.22f), cy = center.Y + (ry * 0.02f);
        float crx = rx * 0.34f, cry = ry * Third;
        var concha = new CanvasPath();
        concha.Ellipse(cx, cy, crx, cry, 0f, 0f, MathF.PI * 2f, false);

        // The lobe fills the bottom third, so it is centred at two thirds down rather than at 0.78.
        var lobe = new CanvasPath();
        lobe.Arc(center.X + (faceDir * rx * 0.05f), center.Y + (ry * 2f * Third), rx * 0.34f,
                 MathF.PI * 0.05f, MathF.PI * 0.95f);

        // **The tragus, which nothing drew.** *"That small, hard piece of flesh that covers the hole
        // to the ear, is the midpoint of the ear"* — so it is the one part whose position is exactly
        // stated rather than estimated, and it is what stops the bowl reading as an empty dish.
        var tragus = new CanvasPath();
        tragus.Arc(cx + (faceDir * crx * 0.72f), center.Y, MathF.Max(0.4f, rx * 0.15f),
                   MathF.PI * (faceDir > 0f ? 1.42f : 0.42f), MathF.PI * (faceDir > 0f ? 2.42f : 1.42f));

        ctx.Save();

        ApplyMedium(ctx, medium);

        ctx.FillStyle = InMedium(medium, shadowColor);
        ctx.Fill(concha);

        // **Cross-contour arcs across the bowl, which is what `drawCrossContourHatch` is for.** They
        // run across the form rather than along it, so they state the hollow instead of shading it
        // flat — the same reason Manual 03 §4 hatches a three-quarter head at two different angles.
        // Three arcs rather than four, and only where they are big enough to read: below about eight
        // pixels of bowl they merge into the blob they were drawn to avoid.
        if (hatch && cry > 4f)
            DrawCrossContourHatch(ctx, cx, cy, crx * 0.82f, cry * 0.82f,
                                  MathF.PI * 0.15f, MathF.PI * 0.85f, 3, inkColor,
                                  MathF.Max(0.3f, Tier(ConchaTier, h, EarHeightAt240, weight * tier * 0.8f)));

        ctx.FillStyle = InMedium(medium, inkColor);
        ctx.Fill(helix);
        ctx.Fill(antihelix);

        ctx.StrokeStyle = InMedium(medium, inkColor);
        ctx.LineWidth = MathF.Max(0.3f, Tier(ConchaTier, h, EarHeightAt240, weight * tier));
        ctx.LineCap = "round";
        ctx.Stroke(lobe);
        ctx.Stroke(tragus);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["helix"] = helix,
            ["antihelix"] = antihelix,
            ["concha"] = concha,
            ["lobe"] = lobe,
            ["tragus"] = tragus
        };
    }

    /// <summary>
    /// Draws the mouth and <b>returns the parts it built</b>: <c>cavity</c>, <c>teeth</c>,
    /// <c>lipLine</c> (the inked upper lip, as an open centre-line) and <c>lowerLip</c>.
    /// </summary>
    /// <remarks>
    /// The mouth is the feature Studio Manual 23 §4 says carries expression along with the brows and
    /// the eyes, so it is the one most often redrawn — and <c>lipLine</c> being a centre-line rather
    /// than a filled mark is what lets it be re-stroked heavier, run through
    /// <c>ctx.strokeToPath(...)</c> to be tapered, or clipped against the head's own silhouette.
    /// </remarks>
    public Dictionary<string, object?> DrawComicMouth(CanvasRenderingContext2D ctx, object mouthObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(mouthObj) is not IDictionary mouth) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var medium = ReadMedium(optDict, "drawComicMouth");
        var lipColor = optDict?["lipColor"]?.ToString() ?? "#b84848";
        var teethColor = optDict?["teethColor"]?.ToString() ?? "#fbf8ee";
        var cavityColor = optDict?["cavityColor"]?.ToString() ?? "#3a1215";

        var weight = Fraction(optDict, "weight", 1f);

        var center = ExtractPoint(mouth["center"]);
        var left = ExtractPoint(mouth["leftCorner"]);
        var right = ExtractPoint(mouth["rightCorner"]);

        // **The mouth's GEOMETRY was in absolute pixels too, not just its ink** — the cavity 12px
        // deep, the lower lip a 6px disc 16px below centre, the teeth inset 3px from each corner.
        // So a mouth on a 600px head had a cavity a fortieth of its own width and a lower lip that
        // had all but vanished, while a 60px head wore a lip larger than the mouth. Every offset is
        // now a fraction of the mouth's own width, calibrated on the 240px head that produced the
        // constants, so that size is unchanged and the rest are no longer wrong.
        var mw = MathF.Abs(right.X - left.X);
        if (mw <= 0.1f) mw = MouthWidthAt240;
        float F(float px) => mw * (px / MouthWidthAt240);

        // The upper lip curve bounds the cavity and is also the inked line, so it is built once.
        var lipLine = new CanvasPath();
        lipLine.MoveTo(left.X, left.Y);
        lipLine.QuadraticCurveTo(center.X, center.Y - F(2f), right.X, right.Y);

        var cavity = new CanvasPath(lipLine);
        cavity.QuadraticCurveTo(center.X, center.Y + F(12f), left.X, left.Y);
        cavity.ClosePath();

        var teeth = new CanvasPath();
        teeth.MoveTo(left.X + F(3f), left.Y);
        teeth.QuadraticCurveTo(center.X, center.Y - F(2f), right.X - F(3f), right.Y);
        teeth.LineTo(right.X - F(4f), right.Y + F(4f));
        teeth.QuadraticCurveTo(center.X, center.Y + F(3f), left.X + F(4f), left.Y + F(3f));
        teeth.ClosePath();

        var lowerLip = new CanvasPath();
        lowerLip.Arc(center.X, center.Y + F(16f), F(6f), 0.2f, MathF.PI - 0.2f);

        // **The lip slit as a tapered mark.** Full weight through the middle and lifting at both
        // corners, which is what stops a mouth reading as a drawn-on line. `lipLine` keeps its
        // documented meaning as the open centre-line; the filled mark arrives beside it.
        var lipMark = CreateTaperedStrokePath(
            left,
            new Point2D(left.X + ((center.X - left.X) * 0.6f), center.Y - F(2f)),
            new Point2D(right.X - ((right.X - center.X) * 0.6f), center.Y - F(2f)),
            right,
            Tier(LipLineTier, mw, MouthWidthAt240, weight * TaperGain));

        ctx.Save();

        ApplyMedium(ctx, medium);

        ctx.FillStyle = InMedium(medium, cavityColor);
        ctx.Fill(cavity);

        // **Clipped to the cavity, because teeth outside a mouth are a hole in the face.** The teeth
        // polygon's lower edge dips below the cavity's return curve at whichever corner is longer —
        // this construction's corners are not symmetric — so a wedge of white stood outside the mouth.
        // It was always there and was four pixels wide at 240px; scaling the geometry made it visible
        // rather than making it happen.
        ctx.Save();
        ctx.Clip(cavity);
        ctx.FillStyle = teethColor;
        ctx.Fill(teeth);
        ctx.Restore();

        // **A hairline along the centre-line under the tapered mark, to seal the corners.** The taper
        // lifts to nothing at each corner, and the teeth's top edge follows the same curve — so
        // without this the white of the teeth shows through as a notch at whichever corner is longer.
        // The round-capped constant stroke used to cover it by being constant.
        ctx.StrokeStyle = InMedium(medium, inkColor);
        ctx.LineWidth = MathF.Max(0.4f, Tier(LipLineTier, mw, MouthWidthAt240, weight * 0.4f));
        ctx.LineCap = "round";
        ctx.Stroke(lipLine);

        ctx.FillStyle = InMedium(medium, inkColor);
        ctx.Fill(lipMark);

        ctx.FillStyle = InMedium(medium, lipColor);
        ctx.Fill(lowerLip);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["cavity"] = cavity,
            ["teeth"] = teeth,
            ["lipLine"] = lipLine,
            ["lipMark"] = lipMark,
            ["lowerLip"] = lowerLip
        };
    }
    #endregion

    #region Feature Proportions & Ink Tiers
    /// <summary>The canon's own <c>height / width</c> for an eye, and the aperture's shape at it.</summary>
    /// <remarks>
    /// Kept as ratios of the upper deflection rather than as independent fractions of eye width, so
    /// that moving <c>height</c> opens and closes the whole aperture coherently instead of only
    /// lifting its top. At <see cref="CanonOpenness"/> these reproduce the constants that were
    /// written inline before the aperture was readable — 0.45, 0.40, 0.25 and 0.15 of the drawn
    /// width — which is what makes the change invisible to every script that does not use it.
    /// </remarks>
    private const float CanonOpenness = 0.45f;
    private const float LidCrest = 0.40f / 0.45f;
    private const float LidFloor = 0.25f / 0.45f;
    private const float LowerLidStart = 0.15f / 0.45f;

    /// <summary>
    /// The iris radius as a fraction of the eye's drawn width. <b>A comic iris, not a real one.</b>
    /// </summary>
    /// <remarks>
    /// A real iris measures <b>0.19</b> of the eye width in this construction — its diameter is
    /// 0.1886 of the interpupillary distance on MediaPipe's canonical face model (Apache 2.0, 468
    /// vertices; measured off the iris ring vertices, nothing inferred), and the pupils here sit one
    /// eye-width either side of the facial axis, so IPD is exactly twice the drawn width and the two
    /// ratios coincide. <b>0.32 is therefore 1.70× life size</b>, which is the comic idiom rather
    /// than a mistake — Manual 23 §1 measures the comic head as narrower relative to its eye than
    /// Loomis's, and a larger iris is the same observation from the other side. Kept as the default
    /// so that no face already drawn changes; pass <c>irisRatio: 0.19</c> for a naturalistic eye.
    /// </remarks>
    private const float CanonIrisRatio = 0.32f;

    /// <summary>
    /// How much lighter a far feature is drawn, taken from <see cref="DrawComicEye"/>'s own lid
    /// weights (2.6 against 3.8) so a far brow and the far eye under it agree.
    /// </summary>
    private const float FarFeatureWeight = 2.6f / 3.8f;

    /// <summary>
    /// Every ink weight on a feature, as a fraction of that feature's own measured size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These were absolute pixel counts until 2026-09-19, and that is a defect rather than a
    /// simplification.</b> The geometry has always scaled with head height and the ink did not, so a
    /// feature was correct at exactly one head size: at a 600px portrait the nose came back as a
    /// hairline with a 3.5px dot for a nostril, and at a 60px long shot the same constants read as a
    /// blob. Nothing errored, and the failure looks like a styling choice.
    /// </para>
    /// <para>
    /// <b>The constants they replace were not arbitrary — they were Manual 03's tier table frozen at
    /// one scale.</b> 2.4px is inside Tier 2 (2.0–3.0px, whose examples are "eyelid creases, nose
    /// bridge, lip slit"), and 1.2–1.6px is Tier 3. A tier is a page-scale convention, so the fix is
    /// to hold the tier <i>relative to the feature</i> rather than to the page. Each fraction below
    /// is the old pixel count divided by the feature's own measure on a <b>240px</b> head — the size
    /// Manual 23 takes its measurements at and the one the tests use — so a 240px head renders
    /// exactly as it did and every other size is now right instead of wrong.
    /// </para>
    /// <para>
    /// <b>Each feature scales off its own measurement rather than off the head</b>, because a drawer
    /// is handed the feature and never the head. That is also what lets a feature be drawn standalone
    /// — onto its own plate, for a mesh texture — and still come out at the right weight.
    /// </para>
    /// </remarks>
    /// <summary>The head height these tiers were set at, and the feature measures it produces.</summary>
    private const float TierHead = 240f;
    private const float EyeWidthAt240 = TierHead / 7f;          // unit × 0.5, unit = H / 3.5
    private const float NoseLengthAt240 = 68.4f;                // bridgeTop → underNose, frontal
    private const float MouthWidthAt240 = TierHead / 5f;        // leftCorner → rightCorner, frontal
    private const float EarHeightAt240 = TierHead / 3.5f;       // one unit, Plate 18

    /// <summary>The tier each mark is inked at, in pixels on a 240px head.</summary>
    private const float LidTier = 3.8f;
    private const float LowerLidTier = 1.6f;
    private const float IrisRimTier = 1.2f;
    private const float NoseBridgeTier = 2.4f;
    private const float NostrilTier = 2.0f;
    private const float NostrilRadiusTier = 3.5f;
    private const float LipLineTier = 2.4f;
    private const float HelixTier = 2.6f;
    private const float AntihelixTier = 1.6f;
    private const float ConchaTier = 1.2f;

    /// <summary>An ear divides into thirds — attachment, bowl, lobe — which is Gautier, p. 29.</summary>
    private const float Third = 1f / 3f;

    /// <summary>What is left of the bridge mark on a head with no turn in it.</summary>
    private const float FrontalBridge = 0.38f;

    /// <summary>
    /// A tier's width on a head of this size — <b>sub-linear, and that is the whole point</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first fix for the absolute constants scaled them <i>linearly</i> with the feature, which is
    /// defensible arithmetic and wrong as drawing. Rendered, it turned a 560px head's nose bridge into
    /// a 5.6px black dagger down the middle of the face and shrank a 110px head's eyelid to under half
    /// a pixel. **Line weight does not track subject size**: an inker drawing a long shot *simplifies*
    /// — fewer marks — rather than reaching for a finer nib, which is Janson's doctrine in Manual 03
    /// and Lee &amp; Buscema's reduction test in Manual 20.
    /// </para>
    /// <para>
    /// A square root is the honest middle. Against the old constants it is <b>exactly right at 240px</b>
    /// — so a head at the calibration size renders unchanged — and at 110px and 560px it gives 0.68×
    /// and 1.53× rather than 0.46× and 2.33×. The reference head is stated rather than implied, which
    /// is the difference between this and what it replaced.
    /// </para>
    /// <para>
    /// The implied head is recovered from the feature's own measure, because a drawer is handed the
    /// feature and never the head — which is also what lets a feature be drawn onto its own plate for
    /// a mesh texture and still come out at the right weight.
    /// </para>
    /// </remarks>
    private static float Tier(float tierPx, float measured, float measuredAt240, float multiplier = 1f) =>
        tierPx * MathF.Sqrt(MathF.Max(0.02f, measured / MathF.Max(0.01f, measuredAt240))) * multiplier;

    /// <summary>
    /// What a tapered mark's peak has to be to carry the same ink as the constant-width line it replaced.
    /// </summary>
    /// <remarks>
    /// <see cref="BuildTaperedStroke"/>'s envelope is <c>maxThickness × sin(tπ)</c>, whose mean over
    /// the run is <c>2/π</c> of its peak — so a taper set to the old tier width lays down about 64%
    /// of the ink and reads noticeably lighter, which would make a change meant to improve the
    /// drawing look like a regression. Peaking at <c>π/2</c> of the tier keeps the average weight
    /// where it was and puts the variation either side of it.
    /// </remarks>
    private const float TaperGain = MathF.PI / 2f;

    /// <summary>
    /// The quadratic control point that makes the curve pass <b>through</b> <paramref name="through"/>.
    /// </summary>
    /// <remarks>
    /// A quadratic at its midpoint is a quarter of each end plus half its control, so a control set
    /// at the landmark puts the curve only halfway to it. Solving for the control instead is the
    /// difference between a named station meaning what it says and meaning about half of it.
    /// </remarks>
    static Point2D ControlThrough(Point2D from, Point2D through, Point2D to) =>
        new((2f * through.X) - ((from.X + to.X) * 0.5f), (2f * through.Y) - ((from.Y + to.Y) * 0.5f));

    /// <summary>One measurement of a group divided by another, or <paramref name="fallback"/>.</summary>
    /// <remarks>
    /// A ratio rather than an absolute is what makes a landmark-derived proportion survive
    /// projection: the far eye is narrower at yaw, and dividing by its own width is what stops it
    /// also reading as half shut.
    /// </remarks>
    static float Ratio(IDictionary? d, string numerator, string denominator, float fallback)
    {
        var n = Num(d, numerator, float.NaN);
        var q = Num(d, denominator, float.NaN);
        if (!float.IsFinite(n) || !float.IsFinite(q) || q <= 0.01f || n < 0f) return fallback;
        var r = n / q;
        return float.IsFinite(r) ? r : fallback;
    }

    /// <summary>Which side of the face an Action Unit acts on.</summary>
    /// <remarks>
    /// <para>
    /// <b>Near and far, not left and right, because that is what the construction has.</b> A head
    /// carries <c>nearEye</c>/<c>farEye</c> and <c>nearBrow</c>/<c>farBrow</c>, and "near" is always
    /// the <c>+x</c> side of the facial axis whatever the yaw — it is a side of the *page*, not an
    /// anatomical left or right. Naming these <c>left</c> and <c>right</c> would be a claim about the
    /// character's own anatomy that the model cannot keep across a turn, so those two spellings are
    /// refused by name rather than quietly mapped.
    /// </para>
    /// </remarks>
    private enum FaceSide { Both, Near, Far }

    /// <summary>Whether <paramref name="side"/> includes the near (<c>+x</c>) or far half.</summary>
    static bool SideCovers(FaceSide side, bool isNear) =>
        side == FaceSide.Both || (isNear ? side == FaceSide.Near : side == FaceSide.Far);

    /// <summary>Reads the <c>side</c> option, refusing anything it cannot honestly honour.</summary>
    static FaceSide ReadSide(IDictionary? opt)
    {
        var raw = opt?["side"]?.ToString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(raw)) return FaceSide.Both;

        return raw switch
        {
            "both" => FaceSide.Both,
            "near" => FaceSide.Near,
            "far" => FaceSide.Far,
            "left" or "right" => throw new ArgumentException(
                $"side '{raw}' is not available: this head is built as near and far rather than left "
                + "and right, and 'near' is the +x side of the facial axis at every yaw. Left and "
                + "right would be a claim about the character's own anatomy that a turned head "
                + "cannot keep. Accepted: both, near, far."),
            _ => throw new ArgumentException($"side '{raw}' not recognised. Accepted: both, near, far.")
        };
    }

    /// <summary>A positive finite option read from a dictionary, or <paramref name="fallback"/>.</summary>
    /// <remarks>
    /// Distinct from <see cref="Ratio"/>, which divides one measurement of a group by another. This
    /// reads a ratio the caller states outright — a non-positive or non-finite one falls back rather
    /// than drawing a zero-radius iris or throwing, since an option is a preference and a silently
    /// absent feature is worse than an ignored number.
    /// </remarks>
    static float Fraction(IDictionary? d, string key, float fallback)
    {
        var v = Num(d, key, float.NaN);
        return float.IsFinite(v) && v > 0f ? v : fallback;
    }
    #endregion

    #region Action Units & Expressions
    /// <summary>
    /// Displaces a head by named muscle actions: <c>{ AU4: 0.9, AU7: 0.7 }</c>.
    /// </summary>
    /// <param name="headObj">A head from <see cref="CreateLoomisHead"/>.</param>
    /// <param name="weights">Action Unit names against weights in <c>0 … 1</c>. Unknown names are refused.</param>
    /// <remarks>
    /// <para>
    /// <b>Muscles, not emotions, and that is a decision with a source.</b> Loomis sets aside the
    /// psychological phase of expression explicitly, calling the emotions <i>too numerous to
    /// tabulate</i>, and works instead from two antagonist muscle groups plus a handful of wrinkle
    /// muscles. Ekman &amp; Friesen arrive at the same anatomy from measurement rather than from
    /// drawing. What is finite here is the muscles; a named emotion is a tuple of these, and a
    /// contested one.
    /// </para>
    /// <para>
    /// <b>Additive and order-independent.</b> Each unit adds its own displacement, so
    /// <c>{ AU4, AU7 }</c> means both and gives the same head whichever is folded first. That is what
    /// makes a small set cover a large range of faces without a preset per combination.
    /// </para>
    /// <para>
    /// <b>The units are chosen for what the construction can actually show.</b> A unit that moved a
    /// landmark this head does not carry would be an API that quietly does nothing, which is worse
    /// than an omission — so the set stops where the geometry does. What is missing and why is in the
    /// remarks on <see cref="ActionUnits"/>.
    /// </para>
    /// <para>
    /// <b><c>options.side</c> acts on one half of the face, and it is what makes a raised eyebrow and
    /// a smirk reachable at all.</b> Every unit moved both halves until 2026-09-19, so the single
    /// most recognisable comic brow — one up, one not — could not be drawn, and neither could a
    /// one-sided smile. <c>'near'</c> and <c>'far'</c> name the two halves this construction already
    /// has; see <see cref="FaceSide"/> for why they are not called left and right. <b><c>AU26</c>
    /// ignores the option</b>, because a jaw does not drop on one side, and an option silently doing
    /// nothing there is better than a half-open jaw that no anatomy explains.
    /// </para>
    /// <para>
    /// <b>Three lineages split their brow units per side and this construction did not.</b> ARKit and
    /// MediaPipe carry <c>browDownLeft</c>/<c>browDownRight</c> and <c>browOuterUpLeft</c>/<c>Right</c>;
    /// CANDIDE-3 carries an <i>Eyes vertical difference</i> shape unit. Asymmetry was the one axis
    /// every one of those sources had and this one had nowhere at all.
    /// </para>
    /// <para>
    /// <b>Magnitudes are the studio's, tuned by eye.</b> Neither source supplies displacement
    /// numbers: Loomis gives directions and a relaxed/contracted table, and Ekman &amp; Friesen's
    /// 1976 code scores <i>presence</i> — slight against strong — rather than a continuous
    /// intensity. Anything claiming a measured decimal for these is claiming more than either source
    /// says.
    /// </para>
    /// <para>
    /// Sources: Andrew Loomis, <i>Drawing the Head and Hands</i> (Viking, 1956), pp. 45–47 and
    /// Plate 21; Paul Ekman &amp; Wallace V. Friesen, <i>Measuring Facial Movement</i>, Environmental
    /// Psychology and Nonverbal Behavior 1(1):56–75, 1976, Table 1.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ApplyActionUnits(object headObj, object? weights = null, object? options = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a head from createLoomisHead", nameof(headObj));

        var optionDict = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(optionDict, ActionUnitOptions, "applyActionUnits option");
        var side = ReadSide(optionDict);

        var opt = JsInterop.AsDict(weights);
        var result = CloneHead(head);
        if (opt is null) return result;

        var h = Num(JsInterop.AsDict(head["unit"]), "H", 200f);

        foreach (DictionaryEntry entry in opt)
        {
            var name = entry.Key?.ToString()?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(name)) continue;

            if (!ActionUnits.TryGetValue(name, out var unit))
            {
                throw new ArgumentException(
                    $"'{entry.Key}' is not an Action Unit this construction can draw. Known: "
                    + string.Join(", ", ActionUnits.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    + ". The numbering is Ekman & Friesen's and is arbitrary by their own account, so "
                    + "a unit absent here is one the head carries no landmark for rather than one "
                    + "that does not exist.", nameof(weights));
            }

            var w = entry.Value is null ? 0f : Convert.ToSingle(entry.Value, CultureInfo.InvariantCulture);
            if (!float.IsFinite(w))
                throw new ArgumentException($"{name} weight must be a finite number, got {w}.", nameof(weights));

            // Refused rather than clamped to zero, because the opposite of an Action Unit is a
            // DIFFERENT unit - that separation is the whole design of the coding system, and a
            // negative weight means the caller has the wrong unit rather than the wrong sign.
            if (w < 0f)
            {
                throw new ArgumentException(
                    $"{name} weight is {w}; Action Units do not run negative. The opposing action is "
                    + "its own unit - AU1 raises the brow where AU4 lowers it, AU12 lifts the mouth "
                    + "corners where AU15 drops them. Name the unit you mean.", nameof(weights));
            }

            unit(result, MathF.Min(w, 1f), h, side);
        }

        return result;
    }

    /// <summary>Accepted option names for <see cref="ApplyActionUnits"/>.</summary>
    private static readonly string[] ActionUnitOptions = ["side"];

    /// <summary>
    /// What a named expression is, as muscle weights: <c>expressionUnits('sadness')</c>.
    /// </summary>
    /// <param name="expressionType">One of the six, or an alias. Case and surrounding space ignored.</param>
    /// <param name="intensity">Scales every weight. Defaults to full strength.</param>
    /// <remarks>
    /// <para>
    /// <b>This exists so that a named emotion is a claim you can read rather than a displacement you
    /// cannot.</b> The six names were previously a closed door: a caller who wanted one muscle had to
    /// buy a whole emotion and turn it down, and had no way to see what they were getting. Returning
    /// the tuple makes the claim inspectable, adjustable, and — most usefully — arguable.
    /// </para>
    /// <para>
    /// <b>The weights are the studio's, and no source supplies them.</b> Loomis gives directions and
    /// a relaxed/contracted table; Ekman &amp; Friesen's 1976 code scores <i>presence</i>, slight
    /// against strong, and contains the words <i>anger</i>, <i>happy</i> and <i>surprise</i> exactly
    /// zero times. Published emotion-to-unit tables are somebody's interpretation, not a measurement,
    /// and these are ours. Read them, disagree, pass your own dictionary to
    /// <see cref="ApplyActionUnits"/>.
    /// </para>
    /// <para>
    /// <b>Three of these tuples changed on 2026-09-18, when the head grew brow stations.</b> This
    /// paragraph used to record two consequences of there being one <c>brow</c> point: a canonical
    /// sad brow is AU1 with AU4 — the inner corners lift while the brows knit — and on one landmark
    /// those two <b>cancelled</b>, so <c>sadness</c> carried AU1 alone; and <c>fear</c> and
    /// <c>surprise</c> differed only in amount, because what separates them in life is AU4 knitting
    /// an already-raised brow. It ended by saying both would resolve if <see cref="CreateLoomisHead"/>
    /// ever carried inner and outer brow stations. It does, and they did: <c>sadness</c> names AU1
    /// and AU4 together for the oblique brow, and <c>fear</c> carries the corrugator where
    /// <c>surprise</c> arches without it.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ExpressionUnits(string expressionType, float intensity = 1.0f)
    {
        var name = (expressionType ?? string.Empty).Trim().ToLowerInvariant();
        var canonical = name switch
        {
            "happy" => "joy",
            "angry" => "anger",
            "scared" => "fear",
            "sad" => "sadness",
            _ => name,
        };

        if (!ExpressionTuples.TryGetValue(canonical, out var tuple))
        {
            throw new ArgumentException(
                $"'{expressionType}' is not a named expression. Known: "
                + string.Join(", ", ExpressionTuples.Keys.OrderBy(k => k, StringComparer.Ordinal))
                + ". These six are the toolkit's enumeration rather than a claim that the emotions "
                + "number six - Loomis calls them too numerous to tabulate. For anything else, name "
                + "the muscles: applyActionUnits(head, { AU4: 0.9 }).", nameof(expressionType));
        }

        if (!float.IsFinite(intensity) || intensity < 0f)
        {
            throw new ArgumentException(
                $"intensity must be a finite number of zero or more, got {intensity}. A negative "
                + "expression is not a thing: the opposite of a raised brow is a lowered one, which "
                + "is its own Action Unit.", nameof(intensity));
        }

        var scaled = new Dictionary<string, object?>(tuple.Count, StringComparer.Ordinal);
        foreach (var (unit, weight) in tuple) scaled[unit] = MathF.Min(weight * intensity, 1f);
        return scaled;
    }

    /// <summary>
    /// Applies a named expression. A thin wrapper over <see cref="ApplyActionUnits"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reimplemented 2026-09-18, and what it draws has changed.</b> Each of the six used to
    /// displace one or two landmarks directly, and several did not do what their own manual entry
    /// described: <c>sadness</c> was documented as lifting the inner brow and dropping the mouth
    /// corners, and moved only the mouth. A live run asked for it <i>"at 0.22 for the inner-brow lift
    /// only"</i>, recorded a careful threshold at which the lift would become a grimace, and received
    /// about two and a half pixels of a movement it had not wanted. That is the defect this replaces:
    /// not thinness, but a name that promised one muscle and moved another.
    /// </para>
    /// <para>
    /// <b>Prefer <see cref="ApplyActionUnits"/> where you know what you want.</b> This is the
    /// convenience call, and its tuples are visible through <see cref="ExpressionUnits"/> precisely
    /// so that reaching past it is easy.
    /// </para>
    /// <para>
    /// An unknown name is now <b>refused</b> rather than silently returning the head unchanged, and
    /// the result is a deep clone rather than a shallow one, so writing to its <c>jaw</c> no longer
    /// writes to the caller's.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ApplyFacialExpression(object headObj, string expressionType,
                                                             float intensity = 1.0f, object? options = null)
    {
        var head = ApplyActionUnits(headObj, ExpressionUnits(expressionType, intensity), options);

        // Kept from the previous implementation: a head that can say what it is costs nothing, and
        // something downstream may be reading it. Written after the units so a caller cannot smuggle
        // an `expression` key in through the weights.
        head["expression"] = (expressionType ?? string.Empty).Trim().ToLowerInvariant();
        return head;
    }

    /// <summary>A head copied deeply enough that displacing the copy cannot reach the original.</summary>
    /// <remarks>
    /// Two levels is the whole structure: the head's own keys, and the nested groups (<c>unit</c>,
    /// <c>nearEye</c>, <c>noseWedge</c>, <c>mouthGuides</c>, <c>jaw</c>, and the points inside them).
    /// A shallow copy would share those inner dictionaries, so parameterising a head would mutate
    /// the canon it came from — and the second call with the same head would start from the first
    /// call's face, which is exactly the consistency this exists to provide, destroyed.
    /// </remarks>
    /// <summary>
    /// The muscle actions this construction can actually show, by Ekman &amp; Friesen's numbering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The set is bounded by the construction rather than by the coding system</b>: a unit whose
    /// landmark this head does not have would bind, run, and change nothing, which reads as the
    /// parameter having no effect.
    /// </para>
    /// <para>
    /// <b>AU2 was the sharpest absence and is now here, because the head grew the landmarks for it.</b>
    /// <see cref="CreateLoomisHead"/> carries <c>nearBrow</c> and <c>farBrow</c> as inner, peak and
    /// outer stations, so an inner-only lift and an outer arch are two different displacements rather
    /// than one under two names — which is the difference between worry and surprise, and between
    /// fear and surprise in <see cref="ExpressionTuples"/>.
    /// </para>
    /// <para>
    /// <b>The same change fixed a defect the brow units had all along, and it was not a subtle one.</b>
    /// AU1 and AU4 used to displace <c>head["brow"]</c> — which is not a drawn eyebrow at all but the
    /// ball's equator, the landmark <see cref="CreateHeadGeometry"/> takes the cranium's centre and
    /// radius from. So raising the brows shrank the skull and lowering them grew it. Measured on a
    /// 240px head: <c>AU1</c> at full weight took the silhouette from <b>208.5px wide to 188.9</b>,
    /// and <c>AU4</c> took it to <b>225.4</b> — a <b>36.5px swing in head width across one
    /// expression range</b>, on a face meant to be the same character from panel to panel. It is
    /// invisible in any single render, since a head with raised brows merely looks like a slightly
    /// narrower head; only two panels side by side would show it, and by then it reads as the
    /// toolkit being inconsistent. The brow units now move the brow stations and never that
    /// landmark, which is the same rule Manual 23 states for the jaw: <b>a parameter that moves a
    /// landmark the construction uses as an attachment will break the silhouette.</b>
    /// </para>
    /// <para>
    /// <b>What is still deliberately absent, and why.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><b>AU6 (Cheek Raiser).</b> <c>createHeadGeometry</c> composes a cheek as of 2026-09-19,
    /// but it is <em>derived</em> from the ear and the jaw angle rather than being a landmark, so
    /// there is nothing here for a unit to displace; and <c>drawComicEye</c> draws no crow's feet, so
    /// the Duchenne marker still has nowhere to land.</item>
    /// <item><b>AU9/AU10 (Nose Wrinkler, Upper Lip Raiser).</b> The nose wedge has landmarks but the
    /// upper lip is a single <c>upperLipY</c>, so a sneer would read as the whole lip rising.
    /// Reachable later; not honest yet.</item>
    /// <item><b>AU17 (Chin Raiser).</b> The mentalis bulge is a surface change rather than a landmark
    /// move, and nothing downstream draws chin texture.</item>
    /// </list>
    /// <para>
    /// Each unit <b>adds</b> its displacement, so folding several is order-independent. Magnitudes
    /// are fractions of the head's own height, so a unit means the same thing at any scale — the
    /// same discipline <see cref="CreateParametricHead"/> follows, and for the same reason.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, Action<Dictionary<string, object?>, float, float, FaceSide>> ActionUnits =
        new(StringComparer.Ordinal)
        {
            // Frontalis, Pars Medialis. Loomis's wrinkle muscles at the inner brow: worry, pleading.
            // The inner end carries it, the peak follows at less than half, and the tail does not
            // move at all - which is the whole distinction from AU2 below.
            ["AU1"] = (head, w, h, s) => MoveBrows(head, 0f, -0.045f * h * w, 0f, -0.018f * h * w, 0f, 0f, s),

            // Frontalis, Pars Lateralis. The outer arch: the tail and the peak rise and the inner end
            // stays. **Unreachable before the brow carried stations**, because with one centre point
            // this and AU1 were the same displacement under two names - and the difference between
            // them is the difference between worry and surprise.
            ["AU2"] = (head, w, h, s) => MoveBrows(head, 0f, 0f, 0f, -0.030f * h * w, 0f, -0.040f * h * w, s),

            // Depressor Glabellae / Supercilii / Corrugator. Loomis has the unhappy group pulling the
            // inside corner of the brow down into a frown, which is this unit from the other side.
            //
            // **It knits as well as lowers, and the inward move is what stops it cancelling AU1.**
            // Corrugator draws the heads of the brows together; with one landmark that was a pure
            // vertical, so AU1 and AU4 subtracted to nothing and `sadness` had to omit AU4 to show
            // anything at all. Here AU1 lifts only the inner end while this lowers all three, so the
            // pair leaves the brow **oblique** - inner up, tail down - which is the sad brow itself
            // rather than an absence of one.
            ["AU4"] = (head, w, h, s) => MoveBrows(head, -0.020f * h * w, 0.030f * h * w,
                                                        0f, 0.030f * h * w,
                                                        0f, 0.030f * h * w, s),

            // Levator Palpebrae Superioris - the lid opens. Reachable only since drawComicEye began
            // reading `height`; before that this unit would have bound and drawn nothing.
            ["AU5"] = (head, w, h, s) => ScaleAperture(head, 0.020f * h * w, s),

            // Orbicularis Oculi, Pars Palpebralis - the lid narrows.
            ["AU7"] = (head, w, h, s) => ScaleAperture(head, -0.018f * h * w, s),

            // Orbicularis Oculi, Pars Orbitalis - the cheek rises and pushes the LOWER lid up, leaving the
            // upper where it is. Hamm: "whenever mouth is in laughing position, the lower lid pushes up,
            // covering part of iris" (Drawing the Head and Figure, p. 9). Reachable since the lower lid moves
            // on its own (`lowerLift`); before that it could only narrow both lids, which is AU7.
            ["AU6"] = (head, w, h, s) => LiftLowerLids(head, 0.030f * h * w, s),

            // Zygomatic Major. Loomis's "happy muscles", which pull the corners OUT and diagonally
            // UP - the diagonal is his, and a corner lifted straight up reads as a smirk.
            ["AU12"] = (head, w, h, s) => MoveMouthCorners(head, 0.018f * h * w, -0.032f * h * w, s),

            // Triangularis. The corners drop and do not spread; Loomis's leer is the round-cornered
            // failure of this one.
            ["AU15"] = (head, w, h, s) => MoveMouthCorners(head, 0f, 0.028f * h * w, s),

            // Masseter and the pterygoids relaxed. The one unit that reaches the silhouette, so a
            // dropped jaw changes the head's outline rather than only its marks.
            ["AU26"] = (head, w, h, _) => DropJaw(head, 0.055f * h * w),
        };

    /// <summary>
    /// Displaces both brows station by station, with <paramref name="innerDx"/> read as <b>inward</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Inward rather than leftward, because a face has two sides.</b> A knit draws the heads of
    /// the brows toward each other, so the same unit has to move the near brow one way and the far
    /// brow the other. Taking the displacement as signed screen-x would knit one brow and spread the
    /// other, which renders as a face with one raised and one dropped inner corner — a drawing fault
    /// rather than an expression, and one nothing downstream could report.
    /// </para>
    /// <para>
    /// <b>The axis is the old <c>brow</c> landmark</b>, which is exactly what it is for: the head's
    /// own facial centre at the brow line, left where the construction put it. This call never moves
    /// it — see <see cref="ActionUnits"/> for why that matters more than it sounds.
    /// </para>
    /// </remarks>
    static void MoveBrows(Dictionary<string, object?> head,
                          float innerDx, float innerDy, float peakDx, float peakDy, float outerDx, float outerDy,
                          FaceSide side = FaceSide.Both)
    {
        var axis = ExtractPoint(head.TryGetValue("brow", out var b) ? b : null).X;

        foreach (var group in new[] { "nearBrow", "farBrow" })
        {
            if (!SideCovers(side, group == "nearBrow")) continue;
            if (head[group] is not Dictionary<string, object?> brow) continue;

            // Which way "outward" runs on this side, taken from the brow's own geometry rather than
            // from the group's name: a head mirrored or rebuilt at another yaw stays correct.
            float outward = MathF.Sign(ExtractPoint(brow["outer"]).X - axis);
            if (outward == 0f) outward = 1f;

            foreach (var (key, dx, dy) in new[] { ("inner", innerDx, innerDy),
                                                  ("peak", peakDx, peakDy),
                                                  ("outer", outerDx, outerDy) })
            {
                if (!brow.ContainsKey(key)) continue;
                var p = ExtractPoint(brow[key]);
                brow[key] = ToDict(new Point2D(p.X + (outward * dx), p.Y + dy));
            }
        }
    }

    /// <summary>
    /// The lowest drawn brow station on the near side, or the construction landmark when a head
    /// carries no stations.
    /// </summary>
    /// <remarks>
    /// The lowest rather than an average, because the ladder asks which landmark crosses the one
    /// below it and an averaged brow would report a face as ordered while its inner corners sat in
    /// the eyes. The fallback keeps a hand-built or legacy head measurable rather than reporting it
    /// as broken at <c>y = 0</c>.
    /// </remarks>
    static float DrawnBrowY(IDictionary head)
    {
        var construction = ExtractPoint(head["brow"]).Y;
        if (JsInterop.AsDict(head["nearBrow"]) is not IDictionary brow) return construction;

        var lowest = float.MinValue;
        foreach (var key in new[] { "inner", "peak", "outer" })
        {
            if (!brow.Contains(key)) continue;
            lowest = MathF.Max(lowest, ExtractPoint(brow[key]).Y);
        }

        return lowest == float.MinValue ? construction : lowest;
    }

    /// <summary>Adds an offset to a top-level landmark.</summary>
    static void NudgePoint(Dictionary<string, object?> head, string key, float dx, float dy)
    {
        var p = ExtractPoint(head[key]);
        head[key] = ToDict(new Point2D(p.X + dx, p.Y + dy));
    }

    /// <summary>Opens or narrows both lids, which is a change to each eye's own aperture.</summary>
    static void ScaleAperture(Dictionary<string, object?> head, float delta, FaceSide side = FaceSide.Both)
    {
        foreach (var group in new[] { "nearEye", "farEye" })
        {
            if (!SideCovers(side, group == "nearEye")) continue;
            if (head[group] is not Dictionary<string, object?> eye) continue;
            if (!eye.TryGetValue("height", out var v) || v is null) continue;

            // Floored rather than allowed negative: a lid past shut is not a lid, and a negative
            // aperture would invert the eyelid curve into a shape nothing in the face explains.
            var next = Convert.ToSingle(v, CultureInfo.InvariantCulture) + delta;
            eye["height"] = MathF.Max(0f, next);
        }
    }

    /// <summary>Raises the lower lids by <paramref name="lift"/> pixels, which is a change to each eye's <c>lowerLift</c>.</summary>
    /// <remarks>Additive, like every unit: a lift is added to the lift already there, so units fold in any order.</remarks>
    static void LiftLowerLids(Dictionary<string, object?> head, float lift, FaceSide side = FaceSide.Both)
    {
        foreach (var group in new[] { "nearEye", "farEye" })
        {
            if (!SideCovers(side, group == "nearEye")) continue;
            if (head[group] is not Dictionary<string, object?> eye) continue;
            var had = eye.TryGetValue("lowerLift", out var v) && v is not null ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : 0f;
            eye["lowerLift"] = had + lift;
        }
    }

    /// <summary>Moves both mouth corners outward and vertically, each away from the mouth's centre.</summary>
    /// <remarks>
    /// The outward direction is read off the corners' own positions rather than assumed from their
    /// names, for the reason <c>MoveEyes</c> gives: at yaw the two are not symmetric about the axis,
    /// and "left" in the dictionary is a name rather than a guarantee about x.
    /// </remarks>
    static void MoveMouthCorners(Dictionary<string, object?> head, float spread, float lift,
                                 FaceSide side = FaceSide.Both)
    {
        if (head["mouthGuides"] is not Dictionary<string, object?> mouth) return;

        var centre = ExtractPoint(mouth["center"]);
        foreach (var key in new[] { "leftCorner", "rightCorner" })
        {
            // `rightCorner` is the +x corner, which is the side `nearEye` and `nearBrow` are on —
            // the construction names the mouth by hand and the eyes by depth, so the mapping has to
            // be stated somewhere rather than inferred from the names.
            if (!SideCovers(side, key == "rightCorner")) continue;
            if (!mouth.ContainsKey(key)) continue;
            var p = ExtractPoint(mouth[key]);
            var away = MathF.Sign(p.X - centre.X);
            if (away == 0) away = key == "rightCorner" ? 1 : -1;
            mouth[key] = ToDict(new Point2D(p.X + (away * spread), p.Y + lift));
        }
    }

    /// <summary>Drops the jaw: the lower lip opens and the chin stations follow it down.</summary>
    /// <remarks>
    /// The chin moves because a jaw drop is a change to the head's <i>outline</i>, not only to the
    /// marks inside it — <c>createHeadGeometry</c> builds its jaw polygon through these stations, so
    /// an open mouth that left them alone would draw a dropped lip inside a closed face.
    /// </remarks>
    static void DropJaw(Dictionary<string, object?> head, float drop)
    {
        if (head["mouthGuides"] is Dictionary<string, object?> mouth
            && mouth.TryGetValue("lowerLipY", out var lower) && lower is not null)
        {
            mouth["lowerLipY"] = Convert.ToSingle(lower, CultureInfo.InvariantCulture) + drop;
        }

        NudgePoint(head, "chin", 0f, drop * 0.55f);
        if (head["jaw"] is not Dictionary<string, object?> jaw) return;

        foreach (var key in new[] { "chin", "chinNear", "chinFar", "nearStation", "farStation" })
        {
            if (!jaw.ContainsKey(key)) continue;
            var p = ExtractPoint(jaw[key]);
            // The stations behind the chin move less: the jaw hinges rather than translating.
            var share = key.StartsWith("chin", StringComparison.Ordinal) ? 0.55f : 0.25f;
            jaw[key] = ToDict(new Point2D(p.X, p.Y + (drop * share)));
        }
    }

    /// <summary>
    /// The six named expressions as Action Unit weights. <b>Ours, tuned by eye.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No source supplies these numbers, and that is the first thing to know about them.</b>
    /// Loomis gives muscle directions and a relaxed/contracted table and declines to tabulate the
    /// emotions at all; Ekman &amp; Friesen's 1976 code scores whether a unit is present, slight or
    /// strong, and never names an emotion. So these are the studio's reading of Manual 08 §4's muscle
    /// descriptions, constrained to the seven units this construction can draw, and they are meant to
    /// be argued with rather than trusted.
    /// </para>
    /// <para>
    /// <b>How well each is served differs a lot, and pretending otherwise would be the failure this
    /// replaces.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><b>joy</b> and <b>sadness</b> are well served: their defining muscles — Zygomatic Major
    /// and Triangularis — are both implemented, and the mouth is where both live.</item>
    /// <item><b>anger</b> is good at the brow and approximate at the mouth: Loomis has it squaring,
    /// and there is no unit for that, so AU15 stands in.</item>
    /// <item><b>fear</b> and <b>surprise</b> are separated by AU4, which is what separates them in
    /// life: fear's frontalis lifts against a knitting corrugator, giving it a strained flat brow,
    /// where surprise arches cleanly with no corrugator at all. They are the most confusable pair in
    /// the literature even on real faces — but they are now two faces here rather than one at two
    /// strengths, which they were while the head had a single <c>brow</c> landmark.</item>
    /// <item><b>disgust</b> is the worst served and should be treated as a placeholder. Its defining
    /// action is Levator Labii Superioris curling the upper lip and wrinkling the nose — AU9 and
    /// AU10, neither implemented, because the upper lip is one <c>upperLipY</c> and would rise
    /// whole.</item>
    /// </list>
    /// <para>
    /// <b>sadness carries AU1 and AU4 together, which is the canonical oblique sad brow.</b> It
    /// omitted AU4 until the head had brow stations, because on one point the two cancelled exactly
    /// and the result was a face with no brow movement at all — close to what the implementation
    /// before that shipped. AU1 now lifts only the inner end while AU4 lowers all three, so the pair
    /// slants the brow instead of erasing itself.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, Dictionary<string, float>> ExpressionTuples =
        new(StringComparer.Ordinal)
        {
            // Zygomatic major pulls the corners out and up, and the cheek pushes the lower lid up under the
            // eye - Loomis's cheek-puff, and Hamm's laughing lower lid (p. 9). It was AU7 until the lower lid
            // could move on its own, which narrowed both lids and read as a squint rather than a smile.
            ["joy"] = new(StringComparer.Ordinal) { ["AU6"] = 0.60f, ["AU12"] = 0.85f },

            // Corrugator hard down and knitting, eyes narrowed. The mouth "squares" in Loomis and
            // cannot here.
            ["anger"] = new(StringComparer.Ordinal) { ["AU4"] = 0.90f, ["AU7"] = 0.60f, ["AU15"] = 0.25f },

            // **Fear and surprise are two expressions rather than one at two strengths, and the
            // difference is carried in two places.** Fear knits the brow while raising it - the
            // frontalis lifts and the corrugator fights it, which is what gives fear its strained
            // flat brow - where surprise arches cleanly with no corrugator at all. With a single
            // brow landmark that distinction could not be drawn at all, and until 2026-09-18 the two
            // were separated only by how much AU1 and AU5 each carried.
            //
            // **The second place is the sclera, and it is Gautier's rather than ours.** He puts the
            // WHOLE difference here - "fear and surprise both cause the eyes to widen while the jaw
            // drops helplessly. With fear, however, the eyes widen more, so more white is revealed
            // around the pupil" - and says in the same breath that "the difference is minimal"
            // (Drawing and Cartooning 1,001 Faces, Perigee 1993, book p. 80). AU5 therefore went
            // from 0.80 to 0.95 against surprise's 0.65, doubling a gap that was sub-pixel on a
            // 240px head. It is still a modest gap, which is the point: a decisive one would be
            // overstating a source that calls the difference minimal.
            //
            // **FACS would also give fear AU7, and this construction cannot take it.** Ekman codes
            // fear as AU1+2+4+5+7+20+26 - the lower lid tenses while the upper raises. Here AU5 and
            // AU7 are the same `eye.height` scaled in opposite directions, so adding AU7 would not
            // tense a lower lid, it would quietly undo the widening that is the whole point. The
            // omission is a limit of one aperture per eye rather than a reading of the source.
            ["fear"] = new(StringComparer.Ordinal)
            {
                ["AU1"] = 0.80f, ["AU2"] = 0.50f, ["AU4"] = 0.60f, ["AU5"] = 0.95f, ["AU26"] = 0.45f
            },

            // **Sadness carries AU4, which is the oblique brow it is named for.** The tuple used to
            // omit it with the comment "it would cancel AU1" - true when both were one vertical on
            // one point, and the reason the canonical sad brow was the one expression this set could
            // not draw. AU1 now lifts only the inner end and AU4 lowers all three, so the pair leaves
            // the brow slanting up toward the nose instead of leaving it flat.
            //
            // **And it carries AU7, because until 2026-09-20 this was a sorrow with no eyes in it.**
            // Gautier: "when sorrow falls upon us, our mouths purse and curl while the intricate
            // network of muscles around the eyes squeezes tightly together" (book p. 81). The tuple
            // moved a brow and two mouth corners and left the aperture exactly as it found it, so
            // every other expression in this table touched the eye and the one he describes as
            // eye-centred did not. 0.40 sits between joy's 0.25 and disgust's 0.45: enough to read
            // as a squeeze, short of the hard narrowing anger gets at 0.60.
            ["sadness"] = new(StringComparer.Ordinal)
            {
                ["AU1"] = 0.70f, ["AU4"] = 0.40f, ["AU7"] = 0.40f, ["AU15"] = 0.75f
            },

            // The clean arch: both frontalis parts, no corrugator. Weighted toward the brow and the
            // jaw rather than the lids, which is what separates it from fear at the eyes as well.
            ["surprise"] = new(StringComparer.Ordinal)
            {
                ["AU1"] = 0.90f, ["AU2"] = 0.90f, ["AU5"] = 0.65f, ["AU26"] = 0.75f
            },

            // A placeholder until AU9/AU10 exist. Reads as a sour narrowing rather than a sneer.
            ["disgust"] = new(StringComparer.Ordinal) { ["AU4"] = 0.40f, ["AU7"] = 0.45f, ["AU15"] = 0.50f },
        };
    #endregion
}
