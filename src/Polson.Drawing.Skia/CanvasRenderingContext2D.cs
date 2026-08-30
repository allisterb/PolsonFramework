namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Polson.Drawing.Svg;
using SkiaSharp;
using SKSvg = global::Svg.Skia.SKSvg;

/// <summary>HTML5 Canvas 2D rendering context, implemented over SkiaSharp.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class CanvasRenderingContext2D
{
    #region Constructors
    public CanvasRenderingContext2D(SkiaCanvas canvas)
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _states = new Stack<CanvasState>();
        _currentState = new CanvasState();
        _currentPath = new CanvasPath();
    }
    #endregion

    #region Properties
    public SkiaCanvas Canvas { get; }

    public object FillStyle
    {
        get => _currentState.FillGradient != null ? (object)_currentState.FillGradient
             : _currentState.FillPattern != null ? (object)_currentState.FillPattern
             : _currentState.CustomFillShader != null ? (object)_currentState.CustomFillShader
             : $"#{_currentState.FillColor.Red:X2}{_currentState.FillColor.Green:X2}{_currentState.FillColor.Blue:X2}";
        set
        {
            if (value is CanvasGradient grad)
            {
                _currentState.FillGradient = grad;
                _currentState.FillPattern = null;
                _currentState.CustomFillShader = null;
            }
            else if (value is CanvasPattern pattern)
            {
                _currentState.FillPattern = pattern;
                _currentState.FillGradient = null;
                _currentState.CustomFillShader = null;
            }
            else if (value is SKShader shader)
            {
                _currentState.CustomFillShader = shader;
                _currentState.FillGradient = null;
                _currentState.FillPattern = null;
            }
            else if (value != null)
            {
                _currentState.FillColor = SkiaColorParser.Parse(value.ToString());
                _currentState.FillGradient = null;
                _currentState.FillPattern = null;
                _currentState.CustomFillShader = null;
            }
        }
    }

    public object StrokeStyle
    {
        get => _currentState.StrokeGradient != null ? (object)_currentState.StrokeGradient
             : _currentState.StrokePattern != null ? (object)_currentState.StrokePattern
             : _currentState.CustomStrokeShader != null ? (object)_currentState.CustomStrokeShader
             : $"#{_currentState.StrokeColor.Red:X2}{_currentState.StrokeColor.Green:X2}{_currentState.StrokeColor.Blue:X2}";
        set
        {
            if (value is CanvasGradient grad)
            {
                _currentState.StrokeGradient = grad;
                _currentState.StrokePattern = null;
                _currentState.CustomStrokeShader = null;
            }
            else if (value is CanvasPattern pattern)
            {
                _currentState.StrokePattern = pattern;
                _currentState.StrokeGradient = null;
                _currentState.CustomStrokeShader = null;
            }
            else if (value is SKShader shader)
            {
                _currentState.CustomStrokeShader = shader;
                _currentState.StrokeGradient = null;
                _currentState.StrokePattern = null;
            }
            else if (value != null)
            {
                _currentState.StrokeColor = SkiaColorParser.Parse(value.ToString());
                _currentState.StrokeGradient = null;
                _currentState.StrokePattern = null;
                _currentState.CustomStrokeShader = null;
            }
        }
    }

    public float LineWidth
    {
        get => _currentState.LineWidth;
        set => _currentState.LineWidth = Math.Max(0.0001f, value);
    }

    public string LineCap
    {
        get => _currentState.LineCap.ToString().ToLowerInvariant();
        set => _currentState.LineCap = value.ToLowerInvariant() switch
        {
            "round" => SKStrokeCap.Round,
            "square" => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };
    }

    public string LineJoin
    {
        get => _currentState.LineJoin.ToString().ToLowerInvariant();
        set => _currentState.LineJoin = value.ToLowerInvariant() switch
        {
            "round" => SKStrokeJoin.Round,
            "bevel" => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter
        };
    }

    public float MiterLimit
    {
        get => _currentState.MiterLimit;
        set => _currentState.MiterLimit = Math.Max(0.0001f, value);
    }

    public float GlobalAlpha
    {
        get => _currentState.GlobalAlpha;
        set => _currentState.GlobalAlpha = Math.Clamp(value, 0f, 1f);
    }

    public string GlobalCompositeOperation
    {
        get => _currentState.BlendMode.ToString().ToLowerInvariant();
        set => _currentState.BlendMode = SkiaColorFilterApi.ParseBlendMode(value);
    }

    public string ShadowColor
    {
        get => $"#{_currentState.ShadowColor.Red:X2}{_currentState.ShadowColor.Green:X2}{_currentState.ShadowColor.Blue:X2}";
        set => _currentState.ShadowColor = SkiaColorParser.Parse(value);
    }

    public float ShadowBlur
    {
        get => _currentState.ShadowBlur;
        set => _currentState.ShadowBlur = Math.Max(0f, value);
    }

    public float ShadowOffsetX
    {
        get => _currentState.ShadowOffsetX;
        set => _currentState.ShadowOffsetX = value;
    }

    public float ShadowOffsetY
    {
        get => _currentState.ShadowOffsetY;
        set => _currentState.ShadowOffsetY = value;
    }

    public string Font
    {
        get => _currentState.Font;
        set
        {
            _currentState.Font = value;
            ParseFont(value, out var size, out var typeface);
            _currentState.FontSize = size;
            _currentState.Typeface = typeface;
        }
    }

    public string TextAlign
    {
        get => _currentState.TextAlign;
        set => _currentState.TextAlign = value.ToLowerInvariant();
    }

    public string TextBaseline
    {
        get => _currentState.TextBaseline;
        set => _currentState.TextBaseline = value.ToLowerInvariant();
    }

    /// <summary>
    /// Tracking added between glyphs: <c>"3px"</c>, <c>"0.15em"</c>, or a bare number read as px.
    /// </summary>
    /// <remarks>
    /// <c>em</c> is the unit <c>LogoType.computeWordmarkTracking</c> returns, so its result applies
    /// directly: <c>ctx.letterSpacing = LogoType.computeWordmarkTracking(48, true) + 'em'</c>.
    /// Spacing goes <b>between</b> glyphs and not after the last, so a tracked run stays centred
    /// under <c>textAlign</c>. Any non-zero value forces glyph-by-glyph placement, which loses
    /// kerning — the trade tracking always makes. Zero keeps the fast, kerned path.
    /// </remarks>
    public string LetterSpacing
    {
        get => _currentState.LetterSpacing;
        set => _currentState.LetterSpacing = string.IsNullOrWhiteSpace(value) ? "0px" : value.Trim();
    }

    public SKImageFilter? Filter
    {
        get => _currentState.ImageFilter;
        set => _currentState.ImageFilter = value;
    }

    public SKColorFilter? ColorFilter
    {
        get => _currentState.ColorFilter;
        set => _currentState.ColorFilter = value;
    }

    /// <summary>Phase offset into the dash pattern, in pixels. HTML5 <c>ctx.lineDashOffset</c>.</summary>
    public float LineDashOffset
    {
        get => _currentState.LineDashOffset;
        set => _currentState.LineDashOffset = value;
    }

    public SKPathEffect? PathEffect
    {
        get => _currentState.PathEffect;
        set => _currentState.PathEffect = value;
    }

    /// <summary>
    /// Mask filter applied to the shape's coverage before painting — see <c>Skia.MaskFilter.blur</c>.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Filter"/>: a mask filter softens the *shape*, an image filter blurs
    /// the *result*. Softening a mask leaves the fill flat, which is what an airbrushed edge is.
    /// Saved and restored with the rest of the drawing state.
    /// </remarks>
    public SKMaskFilter? MaskFilter
    {
        get => _currentState.MaskFilter;
        set => _currentState.MaskFilter = value;
    }

    /// <summary>
    /// Whether to dither, which removes banding from shallow gradients at the cost of fine noise.
    /// </summary>
    /// <remarks>
    /// Off by default, as in Skia. Worth turning on for a wide, shallow ramp — a sky, a soft tonal
    /// transition — where 8-bit steps show as visible bands; the noise that replaces them is finer
    /// than the banding. Saved and restored with the rest of the drawing state.
    /// </remarks>
    public bool Dither
    {
        get => _currentState.Dither;
        set => _currentState.Dither = value;
    }
    #endregion

    #region Brushes
    /// <summary>
    /// Applies a drawing medium: its colour or grain, its width and cap, its path texture and its edge.
    /// </summary>
    /// <remarks>
    /// Sets five properties that belong together and are tedious to set apart. Anything the preset
    /// leaves null is <em>cleared</em> rather than left standing, so switching media does not inherit
    /// the last one's texture — the commonest way a "why is my ink line speckled" hour begins.
    /// <para>
    /// Wrap it in <c>save()</c>/<c>restore()</c> to scope a medium to one passage, exactly as with any
    /// other drawing state.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Returns the outline of what <c>stroke()</c> would draw, as a fillable path.
    /// </summary>
    /// <remarks>
    /// The stroke stops being a line with a width and becomes a shape you can work on. That is the
    /// difference between a constant-width ribbon and a drawn mark: outline the stroke, then modify
    /// the outline — narrow it toward one end for pressure, boolean it against another shape, or fill
    /// it with a gradient that runs across the stroke rather than along the path.
    /// <para>
    /// It reflects the current <c>lineWidth</c>, <c>lineCap</c>, <c>lineJoin</c>, <c>miterLimit</c>
    /// and <c>pathEffect</c> — so outlining a stamped or hatched stroke gives you those marks as
    /// geometry, and they then survive into <c>outSvg</c> as vector rather than only as pixels.
    /// </para>
    /// </remarks>
    /// <param name="path">The path to outline. Defaults to the current path.</param>
    /// <returns>The stroke's outline, or an empty path if the stroke would be a hairline.</returns>
    public CanvasPath StrokeToPath(CanvasPath? path = null)
    {
        var source = path?.Path ?? _currentPath.Path;

        using var paint = _currentState.CreateStrokePaint();
        var outline = paint.GetFillPath(source);

        // Null means Skia would draw this as a hairline, which has no area to outline. An empty path
        // is the honest answer — it fills to nothing, exactly as a hairline outline would.
        return outline is null ? new CanvasPath() : new CanvasPath(outline.ToSvgPathData());
    }

    public void UseBrush(BrushPreset brush)
    {
        ArgumentNullException.ThrowIfNull(brush);

        if (brush.Grain is not null) StrokeStyle = brush.Grain;
        else StrokeStyle = brush.Color;

        LineWidth = brush.LineWidth;
        LineCap = brush.LineCap;
        PathEffect = brush.Texture;
        MaskFilter = brush.Edge;
    }
    #endregion

    #region Methods
    #region State Stack
    public void Save()
    {
        _states.Push(new CanvasState(_currentState));
        Canvas.SkCanvas.Save();
    }

    public void Restore()
    {
        if (_states.Count > 0)
        {
            _currentState = _states.Pop();
            Canvas.SkCanvas.Restore();
        }
    }
    #endregion

    #region Transformations
    public void Translate(float x, float y)
    {
        Canvas.SkCanvas.Translate(x, y);
    }

    public void Rotate(float angleRadians)
    {
        Canvas.SkCanvas.RotateRadians(angleRadians);
    }

    public void Scale(float sx, float sy)
    {
        Canvas.SkCanvas.Scale(sx, sy);
    }

    public void Transform(float a, float b, float c, float d, float e, float f)
    {
        var matrix = new SKMatrix(a, c, e, b, d, f, 0, 0, 1);
        Canvas.SkCanvas.Concat(in matrix);
    }

    public void SetTransform(float a = 1f, float b = 0f, float c = 0f, float d = 1f, float e = 0f, float f = 0f)
    {
        ResetTransform();
        Transform(a, b, c, d, e, f);
    }

    public void ResetTransform()
    {
        Canvas.SkCanvas.ResetMatrix();
    }
    #endregion

    #region Rectangles
    public void FillRect(float x, float y, float width, float height)
    {
        using var paint = _currentState.CreateFillPaint();
        Canvas.SkCanvas.DrawRect(SKRect.Create(x, y, width, height), paint);
    }

    public void StrokeRect(float x, float y, float width, float height)
    {
        using var paint = _currentState.CreateStrokePaint();
        Canvas.SkCanvas.DrawRect(SKRect.Create(x, y, width, height), paint);
    }

    public void ClearRect(float x, float y, float width, float height)
    {
        using var paint = new SKPaint { BlendMode = SKBlendMode.Clear };
        Canvas.SkCanvas.DrawRect(SKRect.Create(x, y, width, height), paint);
    }
    #endregion

    #region Paths
    /// <summary>
    /// Sets the dash pattern for subsequent strokes. HTML5 <c>ctx.setLineDash([4, 4])</c>.
    /// </summary>
    /// <remarks>
    /// The state and the paint already understood <c>LineDash</c>; only this entry point was
    /// missing, so a script writing the standard call got "Property 'setLineDash' of object is not
    /// a function" and had to reach for <c>Skia.PathEffect.dash</c> instead. An empty array clears
    /// the pattern, as in the DOM.
    /// <para>
    /// Takes <c>object</c> because a JS array arrives from Jint as a boxed enumerable rather than a
    /// <c>float[]</c>; the members are coerced individually, matching <c>Skia.PathEffect.dash</c>.
    /// An explicitly set <see cref="PathEffect"/> is composed with the dash rather than replaced.
    /// </para>
    /// </remarks>
    public void SetLineDash(object? segments)
    {
        if (segments is not System.Collections.IEnumerable enumerable || segments is string)
        {
            _currentState.LineDash = null;
            return;
        }

        var values = new List<float>();
        foreach (var item in enumerable)
        {
            if (item is null) continue;
            values.Add(Convert.ToSingle(item, CultureInfo.InvariantCulture));
        }

        // An odd count repeats to make it even, which is what the DOM specifies: [5] means 5 on, 5 off.
        if (values.Count % 2 == 1) values.AddRange(values);

        _currentState.LineDash = values.Count > 0 ? [.. values] : null;
    }

    /// <summary>The current dash pattern, empty when none is set. HTML5 <c>ctx.getLineDash()</c>.</summary>
    public float[] GetLineDash() => _currentState.LineDash is { } dash ? (float[])dash.Clone() : [];

    public void BeginPath()
    {
        _currentPath.BeginPath();
    }

    public void ClosePath()
    {
        _currentPath.ClosePath();
    }

    public void MoveTo(float x, float y)
    {
        _currentPath.MoveTo(x, y);
    }

    public void LineTo(float x, float y)
    {
        _currentPath.LineTo(x, y);
    }

    public void Rect(float x, float y, float width, float height)
    {
        _currentPath.Rect(x, y, width, height);
    }

    public void RoundRect(float x, float y, float width, float height, object? radii = null)
    {
        _currentPath.RoundRect(x, y, width, height, radii);
    }

    public void Arc(float x, float y, float radius, float startAngle, float endAngle, bool anticlockwise = false)
    {
        _currentPath.Arc(x, y, radius, startAngle, endAngle, anticlockwise);
    }

    public void ArcTo(float x1, float y1, float x2, float y2, float radius)
    {
        _currentPath.ArcTo(x1, y1, x2, y2, radius);
    }

    public void Ellipse(float x, float y, float radiusX, float radiusY, float rotation, float startAngle, float endAngle, bool anticlockwise = false)
    {
        _currentPath.Ellipse(x, y, radiusX, radiusY, rotation, startAngle, endAngle, anticlockwise);
    }

    public void BezierCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y)
    {
        _currentPath.BezierCurveTo(cp1x, cp1y, cp2x, cp2y, x, y);
    }

    public void SCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y) =>
        BezierCurveTo(cp1x, cp1y, cp2x, cp2y, x, y);

    public void QuadraticCurveTo(float cpx, float cpy, float x, float y)
    {
        _currentPath.QuadraticCurveTo(cpx, cpy, x, y);
    }

    public void CCurveTo(float cpx, float cpy, float x, float y) =>
        QuadraticCurveTo(cpx, cpy, x, y);

    public void Fill(object? pathOrFillRule = null, object? fillRule = null)
    {
        var path = pathOrFillRule is CanvasPath cp ? cp.Path : _currentPath.Path;
        var previous = ApplyFillRule(path, (pathOrFillRule as string) ?? fillRule as string);

        try
        {
            using var paint = _currentState.CreateFillPaint();
            Canvas.SkCanvas.DrawPath(path, paint);
        }
        finally
        {
            path.FillType = previous;
        }
    }

    public void Stroke(CanvasPath? path = null)
    {
        var p = path != null ? path.Path : _currentPath.Path;
        using var paint = _currentState.CreateStrokePaint();
        Canvas.SkCanvas.DrawPath(p, paint);
    }

    public void Clip(object? pathOrFillRule = null, object? fillRule = null)
    {
        var path = pathOrFillRule is CanvasPath cp ? cp.Path : _currentPath.Path;
        var previous = ApplyFillRule(path, (pathOrFillRule as string) ?? fillRule as string);

        try
        {
            Canvas.SkCanvas.ClipPath(path, SKClipOperation.Intersect, true);
        }
        finally
        {
            path.FillType = previous;
        }
    }

    /// <summary>
    /// Sets the fill rule for one operation and returns what it was, so the caller can put it back.
    /// </summary>
    /// <remarks>
    /// The rule is an argument to <c>fill</c> and <c>clip</c>, not a property of the path — so this
    /// must not outlive the call. It used to: one <c>clip(path, 'evenodd')</c> wrote even-odd onto the
    /// caller's <c>CanvasPath</c>, where it silently changed the meaning of every later call that
    /// omitted the rule. The SDK reference promises a <c>CanvasPath</c> is reusable, and it was not:
    /// the same call on the same path gave different results depending on its history, with no error
    /// and only on shapes with counters. Found by a comic-studio agent in stage 1.
    /// <para>
    /// Omitting the rule means non-zero, as in HTML5 — not "whatever was set last". Without the
    /// explicit <see cref="SKPathFillType.Winding"/> here, an early return would leave the previous
    /// value in place and reintroduce exactly that bug.
    /// </para>
    /// </remarks>
    private static SKPathFillType ApplyFillRule(SKPath path, string? rule)
    {
        var previous = path.FillType;

        path.FillType = rule is not null && rule.Equals("evenodd", StringComparison.OrdinalIgnoreCase)
            ? SKPathFillType.EvenOdd
            : SKPathFillType.Winding;

        return previous;
    }
    #endregion

    #region Typography
    public void FillText(string text, float x, float y, float? maxWidth = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateFillPaint();
        DrawTextLines(text, x, y, font, paint, maxWidth);
    }

    public void StrokeText(string text, float x, float y, float? maxWidth = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateStrokePaint();
        DrawTextLines(text, x, y, font, paint, maxWidth);
    }

    /// <summary>Wraps and fills a paragraph, returning the box it occupied.</summary>
    public Dictionary<string, object> FillWrappedText(string text, float x, float y, float maxWidth,
        float? lineHeight = null)
    {
        if (string.IsNullOrEmpty(text)) return EmptyBlock(x, y, lineHeight);

        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateFillPaint();
        return DrawWrapped(text, x, y, maxWidth, lineHeight, font, paint);
    }

    /// <summary>Wraps and strokes a paragraph, returning the box it occupied.</summary>
    public Dictionary<string, object> StrokeWrappedText(string text, float x, float y, float maxWidth,
        float? lineHeight = null)
    {
        if (string.IsNullOrEmpty(text)) return EmptyBlock(x, y, lineHeight);

        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateStrokePaint();
        return DrawWrapped(text, x, y, maxWidth, lineHeight, font, paint);
    }

    /// <summary>
    /// Wraps a paragraph and reports the box it <i>would</i> occupy, without drawing it.
    /// </summary>
    /// <remarks>
    /// The primitive that makes a stacked layout possible: until a block's height is knowable in
    /// advance, nothing can be placed beneath it except by guessing. It shares
    /// <see cref="WrapText"/> and <see cref="MeasureRun"/> with the drawing path on purpose — a
    /// layout measured by one code path and drawn by another disagrees with itself, which is the
    /// standing failure of every width-estimating layout engine.
    /// </remarks>
    public Dictionary<string, object> MeasureWrappedText(string text, float maxWidth, float? lineHeight = null)
    {
        if (string.IsNullOrEmpty(text)) return EmptyBlock(0f, 0f, lineHeight);

        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        font.GetFontMetrics(out var m);

        var lh = lineHeight ?? (m.Descent - m.Ascent) * 1.25f;
        var spacing = ResolveLetterSpacing();
        var lines = WrapText(text, maxWidth, font, spacing);

        return Block(lines, WidestLine(lines, font, spacing), lh, m, 0f, 0f, measuredOnly: true);
    }

    private Dictionary<string, object> DrawWrapped(string text, float x, float y, float maxWidth,
        float? lineHeight, SKFont font, SKPaint paint)
    {
        font.GetFontMetrics(out var m);
        var lh = lineHeight ?? (m.Descent - m.Ascent) * 1.25f;
        var spacing = ResolveLetterSpacing();

        var lines = WrapText(text, maxWidth, font, spacing);
        for (var i = 0; i < lines.Count; i++)
        {
            DrawTextRun(lines[i], x, y + i * lh, font, paint, spacing);
        }

        var widest = WidestLine(lines, font, spacing);

        // The block's left edge follows textAlign, and its top follows textBaseline — so the
        // returned box is where the ink actually landed, not where the anchor was.
        var left = _currentState.TextAlign switch
        {
            "center" => x - widest / 2f,
            "right" or "end" => x - widest,
            _ => x
        };
        var top = ResolveBaselineY(y, m) + m.Ascent;

        return Block(lines, widest, lh, m, left, top, measuredOnly: false);
    }

    private static float WidestLine(List<string> lines, SKFont font, float spacing)
    {
        var widest = 0f;
        foreach (var line in lines) widest = Math.Max(widest, MeasureRun(line, font, spacing));
        return widest;
    }

    private static Dictionary<string, object> Block(List<string> lines, float width, float lineHeight,
        SKFontMetrics metrics, float x, float y, bool measuredOnly)
    {
        var height = lines.Count * lineHeight;
        return new Dictionary<string, object>
        {
            ["x"] = x,
            ["y"] = y,
            ["width"] = width,
            ["height"] = height,
            ["x2"] = x + width,
            ["y2"] = y + height,
            ["cx"] = x + width / 2f,
            ["cy"] = y + height / 2f,
            ["lines"] = lines.ToArray(),
            ["lineCount"] = lines.Count,
            ["lineHeight"] = lineHeight,
            ["ascent"] = -metrics.Ascent,
            ["descent"] = metrics.Descent,
            // A measurement has no anchor, so its x/y are zero and only the size is meaningful.
            ["positioned"] = !measuredOnly
        };
    }

    private Dictionary<string, object> EmptyBlock(float x, float y, float? lineHeight)
    {
        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        font.GetFontMetrics(out var m);
        return Block([], 0f, lineHeight ?? (m.Descent - m.Ascent) * 1.25f, m, x, y, measuredOnly: false);
    }

    private static List<string> WrapText(string text, float maxWidth, SKFont font, float spacing)
    {
        var result = new List<string>();
        var paragraphs = text.Split('\n');

        foreach (var para in paragraphs)
        {
            var words = para.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            var currentLine = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = currentLine + " " + words[i];
                if (MeasureRun(candidate, font, spacing) <= maxWidth)
                {
                    currentLine = candidate;
                }
                else
                {
                    result.Add(currentLine);
                    currentLine = words[i];
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
            {
                result.Add(currentLine);
            }
        }

        return result;
    }

    public Dictionary<string, object> MeasureText(string text)
    {
        text ??= string.Empty;
        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        var width = MeasureRun(text, font, ResolveLetterSpacing());
        font.GetFontMetrics(out var metrics);

        return new Dictionary<string, object>
        {
            ["width"] = width,
            ["actualBoundingBoxAscent"] = -metrics.Ascent,
            ["actualBoundingBoxDescent"] = metrics.Descent,
            ["fontBoundingBoxAscent"] = -metrics.Top,
            ["fontBoundingBoxDescent"] = metrics.Bottom
        };
    }

    /// <summary>Tracking in pixels, resolving <c>em</c> against the size currently in force.</summary>
    private float ResolveLetterSpacing()
    {
        var raw = _currentState.LetterSpacing;
        if (string.IsNullOrWhiteSpace(raw)) return 0f;

        var text = raw.Trim();
        var split = text.Length;
        while (split > 0 && (char.IsLetter(text[split - 1]) || text[split - 1] == '%')) split--;

        var unit = text[split..].ToLowerInvariant();
        if (!float.TryParse(text[..split].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            return 0f;
        }

        // em is what LogoType.computeWordmarkTracking deals in; anything else is taken as pixels.
        return unit is "em" or "rem" ? n * _currentState.FontSize : n;
    }

    /// <summary>Where the baseline sits for a run drawn at <paramref name="y"/> under the current
    /// <c>textBaseline</c>.</summary>
    private float ResolveBaselineY(float y, SKFontMetrics metrics) => _currentState.TextBaseline switch
    {
        "top" or "hanging" => y - metrics.Ascent,
        "middle" => y - (metrics.Ascent + metrics.Descent) / 2f,
        "bottom" or "ideographic" => y - metrics.Descent,
        _ => y // alphabetic
    };

    /// <summary>Grapheme clusters, so tracking never splits an emoji or a combining accent.</summary>
    private static List<string> Graphemes(string text)
    {
        var result = new List<string>();
        var walker = StringInfo.GetTextElementEnumerator(text);
        while (walker.MoveNext()) result.Add((string)walker.Current);
        return result;
    }

    /// <summary>
    /// Advance width of a run, including tracking.
    /// </summary>
    /// <remarks>
    /// A tracked run is measured as the sum of its individual glyph advances rather than as one
    /// shaped string, because that is how it will be <i>drawn</i>. Measuring it the other way would
    /// leave the kerning in the measurement but not in the render, and centred text would sit off
    /// its point by exactly the kerning the shaper applied.
    /// </remarks>
    private static float MeasureRun(string text, SKFont font, float spacing)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        if (spacing == 0f) return font.MeasureText(text);

        var glyphs = Graphemes(text);
        var total = 0f;
        foreach (var g in glyphs) total += font.MeasureText(g);
        return total + spacing * Math.Max(0, glyphs.Count - 1);
    }

    /// <summary>Draws one run, resolving <c>textAlign</c> and <c>textBaseline</c> and any tracking.</summary>
    private void DrawTextRun(string text, float x, float y, SKFont font, SKPaint paint, float spacing)
    {
        if (string.IsNullOrEmpty(text)) return;

        var width = MeasureRun(text, font, spacing);
        font.GetFontMetrics(out var metrics);

        var startX = _currentState.TextAlign switch
        {
            "center" => x - width / 2f,
            "right" or "end" => x - width,
            _ => x
        };

        var baselineY = ResolveBaselineY(y, metrics);

        if (spacing == 0f)
        {
            Canvas.SkCanvas.DrawText(text, startX, baselineY, SKTextAlign.Left, font, paint);
            return;
        }

        var cursor = startX;
        foreach (var g in Graphemes(text))
        {
            Canvas.SkCanvas.DrawText(g, cursor, baselineY, SKTextAlign.Left, font, paint);
            cursor += font.MeasureText(g) + spacing;
        }
    }

    /// <summary>
    /// Draws text that may contain <c>\n</c>, one run per line, condensed to <paramref name="maxWidth"/>.
    /// </summary>
    /// <remarks>
    /// <c>maxWidth</c> condenses the type horizontally rather than shrinking it, which is what the
    /// HTML canvas specifies and what a caller fitting a label into a fixed box wants: the cap
    /// height stays put and the run narrows. Tracking is condensed with it, or a tracked run would
    /// overshoot the limit by exactly its spacing.
    /// <para>
    /// A multi-line string is condensed once, by its widest line, so the block stays internally
    /// consistent — condensing each line to its own scale would leave every line a different width
    /// of the same typeface. A <c>maxWidth</c> that is zero, negative or NaN draws nothing, as the
    /// specification requires; that surfaces a bad value instead of quietly ignoring the limit.
    /// </para>
    /// </remarks>
    private void DrawTextLines(string text, float x, float y, SKFont font, SKPaint paint, float? maxWidth)
    {
        if (maxWidth is { } given && (given <= 0f || float.IsNaN(given))) return;

        var spacing = ResolveLetterSpacing();
        var lines = text.Contains('\n') ? text.Split('\n') : [text];

        if (maxWidth is { } limit)
        {
            var widest = 0f;
            foreach (var line in lines) widest = Math.Max(widest, MeasureRun(line, font, spacing));

            if (widest > limit && widest > 0f)
            {
                var scale = limit / widest;
                font.ScaleX = scale;
                spacing *= scale;
            }
        }

        if (lines.Length == 1)
        {
            DrawTextRun(lines[0], x, y, font, paint, spacing);
            return;
        }

        font.GetFontMetrics(out var m);
        var lineHeight = (m.Descent - m.Ascent) * 1.2f;
        for (var i = 0; i < lines.Length; i++)
        {
            DrawTextRun(lines[i], x, y + i * lineHeight, font, paint, spacing);
        }
    }

    /// <summary>
    /// Parses a CSS font shorthand into a size and a typeface.
    /// </summary>
    /// <remarks>
    /// Follows the shorthand's grammar rather than scanning for keywords: everything before the size
    /// token is style/variant/weight/stretch, the size token carries its unit and an optional
    /// <c>/line-height</c>, and everything after it is the family list. That is what lets numeric
    /// weights (<c>600</c>), quoted multi-word families (<c>"Source Serif 4"</c>) and fallback lists
    /// (<c>Inter Tight, Helvetica, sans-serif</c>) parse — all three of which a keyword scan gets
    /// wrong, silently, by rendering in a face that was never asked for.
    /// <para>
    /// Deliberately more forgiving than a browser: named weights outside the CSS keyword set
    /// (<c>semibold</c>, <c>medium</c>, <c>black</c>) are accepted, because an agent writing a font
    /// string from a design spec reaches for them and a browser's silent rejection teaches nothing.
    /// </para>
    /// </remarks>
    private static void ParseFont(string fontStr, out float fontSize, out SKTypeface typeface)
    {
        fontSize = 10f;
        typeface = SKTypeface.Default;
        if (string.IsNullOrWhiteSpace(fontStr)) return;

        var text = fontStr.Trim().TrimEnd(';').Trim();
        var weight = SKFontStyleWeight.Normal;
        var slant = SKFontStyleSlant.Upright;

        // The size token anchors the grammar: descriptors precede it, families follow it.
        var size = Regex.Match(text, @"(?<![\w.-])(\d*\.?\d+)\s*(px|pt)\b(\s*/\s*[\d.]+\w*)?",
            RegexOptions.IgnoreCase);

        if (size.Success)
        {
            var value = float.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
            fontSize = size.Groups[2].Value.Equals("pt", StringComparison.OrdinalIgnoreCase)
                ? value * 4f / 3f
                : value;

            foreach (var token in text[..size.Index].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                var descriptor = token.Trim().ToLowerInvariant();
                if (descriptor is "italic" or "oblique")
                {
                    slant = SKFontStyleSlant.Italic;
                }
                else if (NamedWeights.TryGetValue(descriptor, out var named))
                {
                    weight = named;
                }
                else if (int.TryParse(descriptor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
                    && numeric is >= 1 and <= 1000)
                {
                    weight = (SKFontStyleWeight)numeric;
                }
            }
        }

        var familyList = size.Success ? text[(size.Index + size.Length)..] : text;
        typeface = ResolveFamily(familyList, weight, slant);
    }

    /// <summary>
    /// Picks a typeface from a CSS family list, honouring fallbacks the way a browser does.
    /// </summary>
    /// <remarks>
    /// A candidate is only accepted when Skia resolves it to <i>itself</i>. Skia substitutes a
    /// default face for a family it does not have and reports no error, so taking the first
    /// candidate would make every fallback list resolve to its first entry whether installed or
    /// not — the failure <c>Skia.Font.has</c> exists to expose. If nothing resolves exactly, the
    /// first substitute is used, since it still carries the requested weight and slant.
    /// </remarks>
    private static SKTypeface ResolveFamily(string familyList, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        SKTypeface? substitute = null;

        foreach (var raw in familyList.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = raw.Trim().Trim('\'', '"').Trim();
            if (name.Length == 0) continue;

            var candidates = GenericFamilies.TryGetValue(name.ToLowerInvariant(), out var ladder)
                ? ladder
                : [name];

            foreach (var candidate in candidates)
            {
                var face = SKTypeface.FromFamilyName(candidate, weight, SKFontStyleWidth.Normal, slant);
                if (face is null) continue;
                if (string.Equals(face.FamilyName, candidate, StringComparison.OrdinalIgnoreCase)) return face;
                substitute ??= face;
            }
        }

        return substitute ?? SKTypeface.Default;
    }
    #endregion

    #region Gradients & Patterns
    public CanvasGradient CreateLinearGradient(float x0, float y0, float x1, float y1) =>
        new(x0, y0, x1, y1);

    public CanvasGradient CreateRadialGradient(float x0, float y0, float r0, float x1, float y1, float r1) =>
        new(x0, y0, r0, x1, y1, r1);

    public CanvasGradient CreateConicGradient(float startAngle, float x, float y) =>
        new(startAngle, x, y);

    public CanvasPattern CreatePattern(object imageObj, string repetition = "repeat")
    {
        var bmp = ExtractBitmap(imageObj) ?? throw new ArgumentException("Invalid image object for pattern");
        return new CanvasPattern(bmp, repetition);
    }
    #endregion

    #region Image & Cross-Engine SVG Drawing
    public void DrawImage(object imageObj, float arg1, float arg2, float? arg3 = null, float? arg4 = null,
        float? arg5 = null, float? arg6 = null, float? arg7 = null, float? arg8 = null)
    {
        var bmp = ExtractBitmap(imageObj);
        if (bmp == null)
        {
            if (imageObj is SnapPaper paper)
            {
                DrawSvg(paper, arg1, arg2, arg3, arg4);
            }
            return;
        }

        var sampling = new SKSamplingOptions(SKFilterMode.Linear);
        using var paint = _currentState.CreateImagePaint();

        // 9-parameter overload: sx, sy, sw, sh, dx, dy, dw, dh
        if (arg5.HasValue && arg6.HasValue && arg7.HasValue && arg8.HasValue)
        {
            var srcRect = SKRect.Create(arg1, arg2, arg3!.Value, arg4!.Value);
            var destRect = SKRect.Create(arg5.Value, arg6.Value, arg7.Value, arg8.Value);
            Canvas.SkCanvas.DrawBitmap(bmp, srcRect, destRect, sampling, paint);
        }
        // 5-parameter overload: dx, dy, dw, dh
        else if (arg3.HasValue && arg4.HasValue)
        {
            var destRect = SKRect.Create(arg1, arg2, arg3.Value, arg4.Value);
            Canvas.SkCanvas.DrawBitmap(bmp, destRect, sampling, paint);
        }
        // 3-parameter overload: dx, dy
        else
        {
            var destRect = SKRect.Create(arg1, arg2, bmp.Width, bmp.Height);
            Canvas.SkCanvas.DrawBitmap(bmp, destRect, sampling, paint);
        }
    }

    public void DrawSvg(object svgObj, float x = 0, float y = 0, float? width = null, float? height = null)
    {
        if (svgObj is SnapPaper paper)
        {
            using var bmp = SvgRenderPipeline.RenderToBitmap(
                paper.Document, (int?)(width ?? paper.Width), (int?)(height ?? paper.Height));
            var destRect = SKRect.Create(x, y, width ?? paper.Width, height ?? paper.Height);
            using var paint = _currentState.CreateImagePaint();
            Canvas.SkCanvas.DrawBitmap(bmp, destRect, new SKSamplingOptions(SKFilterMode.Linear), paint);
        }
        else if (svgObj is string svgXml)
        {
            using var skSvg = new SKSvg();
            skSvg.FromSvg(svgXml);
            if (skSvg.Picture != null)
            {
                var pBounds = skSvg.Picture.CullRect;
                var targetW = width ?? (pBounds.Width > 0 ? pBounds.Width : 800f);
                var targetH = height ?? (pBounds.Height > 0 ? pBounds.Height : 600f);
                var scaleX = pBounds.Width > 0 ? targetW / pBounds.Width : 1f;
                var scaleY = pBounds.Height > 0 ? targetH / pBounds.Height : 1f;

                Canvas.SkCanvas.Save();
                Canvas.SkCanvas.Translate(x, y);
                Canvas.SkCanvas.Scale(scaleX, scaleY);
                using var paint = _currentState.CreateImagePaint();
                Canvas.SkCanvas.DrawPicture(skSvg.Picture, paint);
                Canvas.SkCanvas.Restore();
            }
        }
    }

    public ImageData GetImageData(int sx, int sy, int sw, int sh)
    {
        var w = Math.Max(1, sw);
        var h = Math.Max(1, sh);
        var imgData = new ImageData(w, h);

        var Rect = SKRectI.Create(sx, sy, w, h);
        using var subset = new SKBitmap();
        if (Canvas.SkBitmap.ExtractSubset(subset, Rect) && subset.ColorType == SKColorType.Rgba8888)
        {
            var span = subset.GetPixelSpan();
            for (var y = 0; y < h; y++)
            {
                var srcRow = span.Slice(y * subset.RowBytes, w * 4);
                var dstRow = imgData.Data.AsSpan(y * w * 4, w * 4);
                srcRow.CopyTo(dstRow);
            }
        }
        else
        {
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var srcX = sx + x;
                    var srcY = sy + y;
                    if (srcX >= 0 && srcX < Canvas.SkBitmap.Width && srcY >= 0 && srcY < Canvas.SkBitmap.Height)
                    {
                        var color = Canvas.SkBitmap.GetPixel(srcX, srcY);
                        var idx = (y * w + x) * 4;
                        imgData.Data[idx + 0] = color.Red;
                        imgData.Data[idx + 1] = color.Green;
                        imgData.Data[idx + 2] = color.Blue;
                        imgData.Data[idx + 3] = color.Alpha;
                    }
                }
            }
        }
        return imgData;
    }

    public void PutImageData(ImageData imageData, int dx, int dy)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        var w = imageData.Width;
        var h = imageData.Height;
        var data = imageData.Data;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var dstX = dx + x;
                var dstY = dy + y;
                if (dstX >= 0 && dstX < Canvas.SkBitmap.Width && dstY >= 0 && dstY < Canvas.SkBitmap.Height)
                {
                    var idx = (y * w + x) * 4;
                    var color = new SKColor(data[idx + 0], data[idx + 1], data[idx + 2], data[idx + 3]);
                    Canvas.SkBitmap.SetPixel(dstX, dstY, color);
                }
            }
        }
    }

    public ImageData CreateImageData(int width, int height) =>
        new(width, height);

    public ImageData CreateImageData(ImageData other) =>
        new(other.Width, other.Height);

    private static SKBitmap? ExtractBitmap(object obj) => obj switch
    {
        SkiaBitmapWrapper bw => bw.Bitmap,
        SkiaCanvas sc => sc.SkBitmap,
        SKBitmap b => b,
        _ => null
    };
    #endregion

    #region Constructive Drawing & Inking
    private static readonly ConstructiveDrawingToolkit _toolkit = new();

    public void DrawTaperedStroke(object start, object cp1, object cp2, object end, float maxThickness, object? fillOrStrokeStyle = null) =>
        _toolkit.DrawTaperedStroke(this, start, cp1, cp2, end, maxThickness, fillOrStrokeStyle ?? FillStyle);

    public void DrawTaperedStroke(float sx, float sy, float cp1x, float cp1y, float cp2x, float cp2y, float ex, float ey, float maxThickness, object? fillOrStrokeStyle = null) =>
        _toolkit.DrawTaperedStroke(this, sx, sy, cp1x, cp1y, cp2x, cp2y, ex, ey, maxThickness, fillOrStrokeStyle ?? FillStyle);

    public void DrawFeathering(object origin, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float LineWidth = 1.2f) =>
        _toolkit.DrawFeathering(this, origin, angleDeg, count, length, spacing, strokeColor ?? StrokeStyle, LineWidth);

    public void DrawFeathering(float ox, float oy, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float LineWidth = 1.2f) =>
        _toolkit.DrawFeathering(this, ox, oy, angleDeg, count, length, spacing, strokeColor ?? StrokeStyle, LineWidth);

    public void DrawCrossContourHatch(float cx, float cy, float rx, float ry, float startAngle, float endAngle, int count = 8, object? strokeColor = null, float LineWidth = 1.2f) =>
        _toolkit.DrawCrossContourHatch(this, cx, cy, rx, ry, startAngle, endAngle, count, strokeColor ?? StrokeStyle, LineWidth);

    public void DrawHairRibbon(object root, object tip, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f) =>
        _toolkit.DrawHairRibbon(this, root, tip, bendFactor, width, fillTop, fillUnderside, strokeColor ?? StrokeStyle, strokeWidth);

    public void DrawHairRibbon(float rx, float ry, float tx, float ty, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f) =>
        _toolkit.DrawHairRibbon(this, rx, ry, tx, ty, bendFactor, width, fillTop, fillUnderside, strokeColor ?? StrokeStyle, strokeWidth);

    public void DrawPerspectiveGrid(object grid, object? options = null) =>
        _toolkit.DrawPerspectiveGrid(this, grid, options);

    public void DrawPerspectiveBox(object box, object? options = null) =>
        _toolkit.DrawPerspectiveBox(this, box, options);

    public void DrawPerspectiveBox(object grid, float anchorX, float anchorY, float width, float height, float depth, object? options = null)
    {
        var box = _toolkit.CreatePerspectiveBox(grid, anchorX, anchorY, width, height, depth);
        _toolkit.DrawPerspectiveBox(this, box, options);
    }

    public void DrawPerspectiveCylinder(object grid, float anchorX, float anchorY, float radius, float height, object? options = null) =>
        _toolkit.DrawPerspectiveCylinder(this, grid, anchorX, anchorY, radius, height, options);

    public void DrawCastShadow(object lightSource, float groundY, object objectVerticesOrBounds, object? options = null)
    {
        var shadow = _toolkit.ProjectCastShadow(lightSource, groundY, objectVerticesOrBounds, options);
        _toolkit.DrawCastShadow(this, shadow, options);
    }

    public void RenderVolumetricSphere(float cx, float cy, float radius, object? lightDirection = null, object? options = null) =>
        _toolkit.RenderVolumetricSphere(this, cx, cy, radius, lightDirection, options);

    public void RenderVolumetricCylinder(float x, float y, float width, float height, object? lightDirection = null, object? options = null) =>
        _toolkit.RenderVolumetricCylinder(this, x, y, width, height, lightDirection, options);

    public void DrawRimLight(object boundsOrPts, float lightAngleDeg, object? rimColor = null, float thickness = 2.5f) =>
        _toolkit.DrawRimLight(this, boundsOrPts, lightAngleDeg, rimColor, thickness);

    public void DrawMannequin(object figure, bool solid = false, object? options = null)
    {
        if (solid)
            _toolkit.DrawMannequinSolid(this, figure, options);
        else
            _toolkit.DrawMannequinWireframe(this, figure, options);
    }

    public void DrawTorsoMusculature(object figure, object? options = null) =>
        _toolkit.DrawTorsoMusculature(this, figure, options);

    public void DrawCompositionGrid(object gridObjOrType, object? options = null) =>
        _toolkit.DrawCompositionGrid(this, gridObjOrType, options);

    public void DrawVignette(object? options = null) =>
        _toolkit.DrawVignette(this, Canvas.Width, Canvas.Height, options);

    public void DrawLeadingLines(object originPoints, object focalPoint, object? options = null) =>
        _toolkit.DrawLeadingLines(this, originPoints, focalPoint, options);
    #endregion

    #region Logo Design Toolkit
    private static readonly LogoDesignToolkit _logoToolkit = new();

    public void DrawSquircle(float x, float y, float width, float height, object? options = null) =>
        _logoToolkit.DrawSquircle(this, x, y, width, height, options);

    public void DrawGoldenSpiral(float cx, float cy, float startRadius = 20f, float turns = 2.5f, object? options = null) =>
        _logoToolkit.DrawGoldenSpiral(this, cx, cy, startRadius, turns, options);

    public void DrawIsometricGrid(float width, float height, float cellSize = 40f, object? options = null) =>
        _logoToolkit.DrawIsometricGrid(this, width, height, cellSize, options);

    public void DrawPolarGrid(float cx, float cy, object? rings = null, int radialSlices = 12, object? options = null) =>
        _logoToolkit.DrawPolarGrid(this, cx, cy, rings, radialSlices, options);

    public void DrawEmblemBadge(float cx, float cy, float radius, string type = "shield", object? options = null) =>
        _logoToolkit.DrawEmblemBadge(this, cx, cy, radius, type, options);

    public void DrawClearSpaceGuide(object markBounds, float xDimension = 40f, object? options = null) =>
        _logoToolkit.DrawClearSpaceGuide(this, markBounds, xDimension, options);

    public void GenerateFaviconScaleTest(object drawMarkFn, object? options = null) =>
        _logoToolkit.GenerateFaviconScaleTest(this, drawMarkFn, options);

    public void GenerateMonochromeTest(object drawMarkFn, float width = 800f, float height = 600f) =>
        _logoToolkit.GenerateMonochromeTest(this, drawMarkFn, width, height);

    public void GenerateBrandPresentationSheet(object options) =>
        _logoToolkit.GenerateBrandPresentationSheet(this, options);
    #endregion

    #region LogoType Toolkit
    private static readonly LogoTypeToolkit _logoTypeToolkit = new();

    public void DrawWordmarkLockup(object? drawMarkFn, string brandName, string tagline, object? options = null) =>
        _logoTypeToolkit.DrawWordmarkLockup(this, drawMarkFn, brandName, tagline, options);

    public void DrawOgeeCurve(float x1, float y1, float x2, float y2, float amplitude = 25f, float inflectionT = 0.5f)
    {
        using var skPath = _logoTypeToolkit.CreateOgeeCurveSKPath(x1, y1, x2, y2, inflectionT, amplitude);
        using var paint = _currentState.CreateStrokePaint();
        Canvas.SkCanvas.DrawPath(skPath, paint);
    }
    #endregion
    #endregion

    #region Fields
    private readonly Stack<CanvasState> _states;
    private CanvasState _currentState;
    private readonly CanvasPath _currentPath;

    /// <summary>
    /// Weight names accepted in a font shorthand. The CSS keywords, plus the names type foundries
    /// and design specs actually use for the numeric steps.
    /// </summary>
    private static readonly Dictionary<string, SKFontStyleWeight> NamedWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["thin"] = SKFontStyleWeight.Thin,
        ["hairline"] = SKFontStyleWeight.Thin,
        ["extralight"] = SKFontStyleWeight.ExtraLight,
        ["ultralight"] = SKFontStyleWeight.ExtraLight,
        ["light"] = SKFontStyleWeight.Light,
        ["normal"] = SKFontStyleWeight.Normal,
        ["regular"] = SKFontStyleWeight.Normal,
        ["book"] = SKFontStyleWeight.Normal,
        ["medium"] = SKFontStyleWeight.Medium,
        ["semibold"] = SKFontStyleWeight.SemiBold,
        ["demibold"] = SKFontStyleWeight.SemiBold,
        ["bold"] = SKFontStyleWeight.Bold,
        ["bolder"] = SKFontStyleWeight.Bold,
        ["lighter"] = SKFontStyleWeight.Light,
        ["extrabold"] = SKFontStyleWeight.ExtraBold,
        ["ultrabold"] = SKFontStyleWeight.ExtraBold,
        ["black"] = SKFontStyleWeight.Black,
        ["heavy"] = SKFontStyleWeight.Black
    };

    /// <summary>
    /// What each CSS generic family is tried as, in order. Skia has no notion of a generic, so a
    /// bare <c>sans-serif</c> would otherwise resolve to whatever the platform substitutes.
    /// </summary>
    private static readonly Dictionary<string, string[]> GenericFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["serif"] = ["Times New Roman", "Georgia", "Liberation Serif", "DejaVu Serif", "serif"],
        ["sans-serif"] = ["Segoe UI", "Helvetica", "Arial", "Liberation Sans", "DejaVu Sans", "sans-serif"],
        ["system-ui"] = ["Segoe UI", "Helvetica", "Arial", "Liberation Sans", "DejaVu Sans", "sans-serif"],
        ["ui-sans-serif"] = ["Segoe UI", "Helvetica", "Arial", "Liberation Sans", "DejaVu Sans", "sans-serif"],
        ["monospace"] = ["Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Courier New", "monospace"],
        ["ui-monospace"] = ["Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Courier New", "monospace"],
        ["cursive"] = ["Comic Sans MS", "Apple Chancery", "cursive"],
        ["fantasy"] = ["Impact", "Papyrus", "fantasy"]
    };
    #endregion
}

