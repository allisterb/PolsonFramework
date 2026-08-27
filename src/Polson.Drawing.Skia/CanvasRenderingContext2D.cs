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

    public SKPathEffect? PathEffect
    {
        get => _currentState.PathEffect;
        set => _currentState.PathEffect = value;
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
        ApplyFillRule(path, (pathOrFillRule as string) ?? fillRule as string);
        using var paint = _currentState.CreateFillPaint();
        Canvas.SkCanvas.DrawPath(path, paint);
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
        ApplyFillRule(path, (pathOrFillRule as string) ?? fillRule as string);
        Canvas.SkCanvas.ClipPath(path, SKClipOperation.Intersect, true);
    }

    /// <summary>
    /// Applies the Canvas Fill rule to a path. Without this, <c>Fill('evenodd')</c> silently falls back to
    /// non-zero winding, which is what makes a counter cut into a mark — the negative space every logo
    /// relies on — come out solid.
    /// </summary>
    private static void ApplyFillRule(SKPath path, string? rule)
    {
        if (string.IsNullOrEmpty(rule)) return;

        path.FillType = rule.Equals("evenodd", StringComparison.OrdinalIgnoreCase)
            ? SKPathFillType.EvenOdd
            : SKPathFillType.Winding;
    }
    #endregion

    #region Typography
    public void FillText(string text, float x, float y, float? maxWidth = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var Font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateFillPaint();

        if (text.Contains('\n'))
        {
            Font.GetFontMetrics(out var m);
            var lineHeight = (m.Descent - m.Ascent) * 1.2f;
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var (adjX, adjY) = AdjustTextPosition(lines[i], x, y + i * lineHeight, Font);
                Canvas.SkCanvas.DrawText(lines[i], adjX, adjY, SKTextAlign.Left, Font, paint);
            }
        }
        else
        {
            var (adjX, adjY) = AdjustTextPosition(text, x, y, Font);
            Canvas.SkCanvas.DrawText(text, adjX, adjY, SKTextAlign.Left, Font, paint);
        }
    }

    public void StrokeText(string text, float x, float y, float? maxWidth = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var Font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateStrokePaint();

        if (text.Contains('\n'))
        {
            Font.GetFontMetrics(out var m);
            var lineHeight = (m.Descent - m.Ascent) * 1.2f;
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var (adjX, adjY) = AdjustTextPosition(lines[i], x, y + i * lineHeight, Font);
                Canvas.SkCanvas.DrawText(lines[i], adjX, adjY, SKTextAlign.Left, Font, paint);
            }
        }
        else
        {
            var (adjX, adjY) = AdjustTextPosition(text, x, y, Font);
            Canvas.SkCanvas.DrawText(text, adjX, adjY, SKTextAlign.Left, Font, paint);
        }
    }

    public void FillWrappedText(string text, float x, float y, float maxWidth, float? lineHeight = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var Font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateFillPaint();

        Font.GetFontMetrics(out var m);
        var lh = lineHeight ?? (m.Descent - m.Ascent) * 1.25f;

        var wrappedLines = WrapText(text, maxWidth, Font);
        for (var i = 0; i < wrappedLines.Count; i++)
        {
            var (adjX, adjY) = AdjustTextPosition(wrappedLines[i], x, y + i * lh, Font);
            Canvas.SkCanvas.DrawText(wrappedLines[i], adjX, adjY, SKTextAlign.Left, Font, paint);
        }
    }

    public void StrokeWrappedText(string text, float x, float y, float maxWidth, float? lineHeight = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var Font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateStrokePaint();

        Font.GetFontMetrics(out var m);
        var lh = lineHeight ?? (m.Descent - m.Ascent) * 1.25f;

        var wrappedLines = WrapText(text, maxWidth, Font);
        for (var i = 0; i < wrappedLines.Count; i++)
        {
            var (adjX, adjY) = AdjustTextPosition(wrappedLines[i], x, y + i * lh, Font);
            Canvas.SkCanvas.DrawText(wrappedLines[i], adjX, adjY, SKTextAlign.Left, Font, paint);
        }
    }

    private static List<string> WrapText(string text, float maxWidth, SKFont Font)
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
                if (Font.MeasureText(candidate) <= maxWidth)
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
        using var Font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        var width = Font.MeasureText(text);
        Font.GetFontMetrics(out var metrics);

        return new Dictionary<string, object>
        {
            ["width"] = width,
            ["actualBoundingBoxAscent"] = -metrics.Ascent,
            ["actualBoundingBoxDescent"] = metrics.Descent,
            ["fontBoundingBoxAscent"] = -metrics.Top,
            ["fontBoundingBoxDescent"] = metrics.Bottom
        };
    }

    private (float x, float y) AdjustTextPosition(string text, float x, float y, SKFont Font)
    {
        var width = Font.MeasureText(text);
        Font.GetFontMetrics(out var metrics);

        var adjX = _currentState.TextAlign switch
        {
            "center" => x - width / 2f,
            "right" or "end" => x - width,
            _ => x
        };

        var adjY = _currentState.TextBaseline switch
        {
            "top" or "hanging" => y - metrics.Ascent,
            "middle" => y - (metrics.Ascent + metrics.Descent) / 2f,
            "bottom" or "ideographic" => y - metrics.Descent,
            _ => y // alphabetic
        };

        return (adjX, adjY);
    }

    private static void ParseFont(string fontStr, out float fontSize, out SKTypeface typeface)
    {
        fontSize = 10f;
        var family = "sans-serif";
        var weight = SKFontStyleWeight.Normal;
        var slant = SKFontStyleSlant.Upright;

        if (string.IsNullOrWhiteSpace(fontStr))
        {
            typeface = SKTypeface.Default;
            return;
        }

        var lower = fontStr.ToLowerInvariant();
        if (lower.Contains("bold")) weight = SKFontStyleWeight.Bold;
        if (lower.Contains("italic") || lower.Contains("oblique")) slant = SKFontStyleSlant.Italic;

        var sizeMatch = Regex.Match(fontStr, @"([0-9.]+)\s*px", RegexOptions.IgnoreCase);
        if (sizeMatch.Success)
        {
            fontSize = float.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var parts = fontStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            family = parts[^1].Trim('\'', '"', ';');
        }

        typeface = SKTypeface.FromFamilyName(family, weight, SKFontStyleWidth.Normal, slant) ?? SKTypeface.Default;
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

        // 9-parameter overload: sx, sy, sw, sh, dx, dy, dw, dh
        if (arg5.HasValue && arg6.HasValue && arg7.HasValue && arg8.HasValue)
        {
            var srcRect = SKRect.Create(arg1, arg2, arg3!.Value, arg4!.Value);
            var destRect = SKRect.Create(arg5.Value, arg6.Value, arg7.Value, arg8.Value);
            Canvas.SkCanvas.DrawBitmap(bmp, srcRect, destRect, sampling, null);
        }
        // 5-parameter overload: dx, dy, dw, dh
        else if (arg3.HasValue && arg4.HasValue)
        {
            var destRect = SKRect.Create(arg1, arg2, arg3.Value, arg4.Value);
            Canvas.SkCanvas.DrawBitmap(bmp, destRect, sampling, null);
        }
        // 3-parameter overload: dx, dy
        else
        {
            var destRect = SKRect.Create(arg1, arg2, bmp.Width, bmp.Height);
            Canvas.SkCanvas.DrawBitmap(bmp, destRect, sampling, null);
        }
    }

    public void DrawSvg(object svgObj, float x = 0, float y = 0, float? width = null, float? height = null)
    {
        if (svgObj is SnapPaper paper)
        {
            using var bmp = SvgRenderPipeline.RenderToBitmap(
                paper.Document, (int?)(width ?? paper.Width), (int?)(height ?? paper.Height));
            var destRect = SKRect.Create(x, y, width ?? paper.Width, height ?? paper.Height);
            Canvas.SkCanvas.DrawBitmap(bmp, destRect, new SKSamplingOptions(SKFilterMode.Linear), null);
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
                var matrix = SKMatrix.CreateScale(scaleX, scaleY);
                Canvas.SkCanvas.DrawPicture(skSvg.Picture, in matrix);
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
        if (Canvas.Bitmap.ExtractSubset(subset, Rect) && subset.ColorType == SKColorType.Rgba8888)
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
                    if (srcX >= 0 && srcX < Canvas.Bitmap.Width && srcY >= 0 && srcY < Canvas.Bitmap.Height)
                    {
                        var color = Canvas.Bitmap.GetPixel(srcX, srcY);
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
                if (dstX >= 0 && dstX < Canvas.Bitmap.Width && dstY >= 0 && dstY < Canvas.Bitmap.Height)
                {
                    var idx = (y * w + x) * 4;
                    var color = new SKColor(data[idx + 0], data[idx + 1], data[idx + 2], data[idx + 3]);
                    Canvas.Bitmap.SetPixel(dstX, dstY, color);
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
        SkiaCanvas sc => sc.Bitmap,
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
    #endregion
}

