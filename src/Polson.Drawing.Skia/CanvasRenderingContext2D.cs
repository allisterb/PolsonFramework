namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Polson.Drawing.Svg;
using SkiaSharp;
using SKSvg = global::Svg.Skia.SKSvg;

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

    public object fillStyle
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

    public object strokeStyle
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

    public float lineWidth
    {
        get => _currentState.LineWidth;
        set => _currentState.LineWidth = Math.Max(0.0001f, value);
    }

    public string lineCap
    {
        get => _currentState.LineCap.ToString().ToLowerInvariant();
        set => _currentState.LineCap = value.ToLowerInvariant() switch
        {
            "round" => SKStrokeCap.Round,
            "square" => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };
    }

    public string lineJoin
    {
        get => _currentState.LineJoin.ToString().ToLowerInvariant();
        set => _currentState.LineJoin = value.ToLowerInvariant() switch
        {
            "round" => SKStrokeJoin.Round,
            "bevel" => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter
        };
    }

    public float miterLimit
    {
        get => _currentState.MiterLimit;
        set => _currentState.MiterLimit = Math.Max(0.0001f, value);
    }

    public float globalAlpha
    {
        get => _currentState.GlobalAlpha;
        set => _currentState.GlobalAlpha = Math.Clamp(value, 0f, 1f);
    }

    public string globalCompositeOperation
    {
        get => _currentState.BlendMode.ToString().ToLowerInvariant();
        set => _currentState.BlendMode = SkiaColorFilterApi.ParseBlendMode(value);
    }

    public string shadowColor
    {
        get => $"#{_currentState.ShadowColor.Red:X2}{_currentState.ShadowColor.Green:X2}{_currentState.ShadowColor.Blue:X2}";
        set => _currentState.ShadowColor = SkiaColorParser.Parse(value);
    }

    public float shadowBlur
    {
        get => _currentState.ShadowBlur;
        set => _currentState.ShadowBlur = Math.Max(0f, value);
    }

    public float shadowOffsetX
    {
        get => _currentState.ShadowOffsetX;
        set => _currentState.ShadowOffsetX = value;
    }

    public float shadowOffsetY
    {
        get => _currentState.ShadowOffsetY;
        set => _currentState.ShadowOffsetY = value;
    }

    public string font
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

    public string textAlign
    {
        get => _currentState.TextAlign;
        set => _currentState.TextAlign = value.ToLowerInvariant();
    }

    public string textBaseline
    {
        get => _currentState.TextBaseline;
        set => _currentState.TextBaseline = value.ToLowerInvariant();
    }

    public SKImageFilter? filter
    {
        get => _currentState.ImageFilter;
        set => _currentState.ImageFilter = value;
    }

    public SKColorFilter? colorFilter
    {
        get => _currentState.ColorFilter;
        set => _currentState.ColorFilter = value;
    }

    public SKPathEffect? pathEffect
    {
        get => _currentState.PathEffect;
        set => _currentState.PathEffect = value;
    }
    #endregion

    #region Methods
    #region State Stack
    public void save()
    {
        _states.Push(new CanvasState(_currentState));
        Canvas.SkCanvas.Save();
    }

    public void restore()
    {
        if (_states.Count > 0)
        {
            _currentState = _states.Pop();
            Canvas.SkCanvas.Restore();
        }
    }
    #endregion

    #region Transformations
    public void translate(float x, float y)
    {
        Canvas.SkCanvas.Translate(x, y);
    }

    public void rotate(float angleRadians)
    {
        Canvas.SkCanvas.RotateRadians(angleRadians);
    }

    public void scale(float sx, float sy)
    {
        Canvas.SkCanvas.Scale(sx, sy);
    }

    public void transform(float a, float b, float c, float d, float e, float f)
    {
        var matrix = new SKMatrix(a, c, e, b, d, f, 0, 0, 1);
        Canvas.SkCanvas.Concat(in matrix);
    }

    public void setTransform(float a = 1f, float b = 0f, float c = 0f, float d = 1f, float e = 0f, float f = 0f)
    {
        resetTransform();
        transform(a, b, c, d, e, f);
    }

    public void resetTransform()
    {
        Canvas.SkCanvas.ResetMatrix();
    }
    #endregion

    #region Rectangles
    public void fillRect(float x, float y, float width, float height)
    {
        using var paint = _currentState.CreateFillPaint();
        Canvas.SkCanvas.DrawRect(SKRect.Create(x, y, width, height), paint);
    }

    public void strokeRect(float x, float y, float width, float height)
    {
        using var paint = _currentState.CreateStrokePaint();
        Canvas.SkCanvas.DrawRect(SKRect.Create(x, y, width, height), paint);
    }

    public void clearRect(float x, float y, float width, float height)
    {
        using var paint = new SKPaint { BlendMode = SKBlendMode.Clear };
        Canvas.SkCanvas.DrawRect(SKRect.Create(x, y, width, height), paint);
    }
    #endregion

    #region Paths
    public void beginPath()
    {
        _currentPath.beginPath();
    }

    public void closePath()
    {
        _currentPath.closePath();
    }

    public void moveTo(float x, float y)
    {
        _currentPath.moveTo(x, y);
    }

    public void lineTo(float x, float y)
    {
        _currentPath.lineTo(x, y);
    }

    public void rect(float x, float y, float width, float height)
    {
        _currentPath.rect(x, y, width, height);
    }

    public void roundRect(float x, float y, float width, float height, object? radii = null)
    {
        _currentPath.roundRect(x, y, width, height, radii);
    }

    public void arc(float x, float y, float radius, float startAngle, float endAngle, bool anticlockwise = false)
    {
        _currentPath.arc(x, y, radius, startAngle, endAngle, anticlockwise);
    }

    public void arcTo(float x1, float y1, float x2, float y2, float radius)
    {
        _currentPath.arcTo(x1, y1, x2, y2, radius);
    }

    public void ellipse(float x, float y, float radiusX, float radiusY, float rotation, float startAngle, float endAngle, bool anticlockwise = false)
    {
        _currentPath.ellipse(x, y, radiusX, radiusY, rotation, startAngle, endAngle, anticlockwise);
    }

    public void bezierCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y)
    {
        _currentPath.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, x, y);
    }

    public void quadraticCurveTo(float cpx, float cpy, float x, float y)
    {
        _currentPath.quadraticCurveTo(cpx, cpy, x, y);
    }

    public void fill(object? pathOrFillRule = null)
    {
        var path = pathOrFillRule is CanvasPath cp ? cp.Path : _currentPath.Path;
        using var paint = _currentState.CreateFillPaint();
        Canvas.SkCanvas.DrawPath(path, paint);
    }

    public void stroke(CanvasPath? path = null)
    {
        var p = path != null ? path.Path : _currentPath.Path;
        using var paint = _currentState.CreateStrokePaint();
        Canvas.SkCanvas.DrawPath(p, paint);
    }

    public void clip(object? pathOrFillRule = null)
    {
        var path = pathOrFillRule is CanvasPath cp ? cp.Path : _currentPath.Path;
        Canvas.SkCanvas.ClipPath(path, SKClipOperation.Intersect, true);
    }
    #endregion

    #region Typography
    public void fillText(string text, float x, float y, float? maxWidth = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateFillPaint();

        var (adjX, adjY) = AdjustTextPosition(text, x, y, font);
        Canvas.SkCanvas.DrawText(text, adjX, adjY, SKTextAlign.Left, font, paint);
    }

    public void strokeText(string text, float x, float y, float? maxWidth = null)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        using var paint = _currentState.CreateStrokePaint();

        var (adjX, adjY) = AdjustTextPosition(text, x, y, font);
        Canvas.SkCanvas.DrawText(text, adjX, adjY, SKTextAlign.Left, font, paint);
    }

    public Dictionary<string, object> measureText(string text)
    {
        text ??= string.Empty;
        using var font = new SKFont(_currentState.Typeface, _currentState.FontSize);
        var width = font.MeasureText(text);
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

    private (float x, float y) AdjustTextPosition(string text, float x, float y, SKFont font)
    {
        var width = font.MeasureText(text);
        font.GetFontMetrics(out var metrics);

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
    public CanvasGradient createLinearGradient(float x0, float y0, float x1, float y1) =>
        new(x0, y0, x1, y1);

    public CanvasGradient createRadialGradient(float x0, float y0, float r0, float x1, float y1, float r1) =>
        new(x0, y0, r0, x1, y1, r1);

    public CanvasGradient createConicGradient(float startAngle, float x, float y) =>
        new(startAngle, x, y);

    public CanvasPattern createPattern(SKBitmap bitmap, string repetition = "repeat") =>
        new(bitmap, repetition);
    #endregion

    #region Image & Cross-Engine SVG Drawing
    public void drawImage(object imageObj, float dx, float dy, float? dWidth = null, float? dHeight = null)
    {
        var sampling = new SKSamplingOptions(SKFilterMode.Linear);
        if (imageObj is SkiaCanvas otherCanvas)
        {
            var destRect = SKRect.Create(dx, dy, dWidth ?? otherCanvas.Width, dHeight ?? otherCanvas.Height);
            Canvas.SkCanvas.DrawBitmap(otherCanvas.Bitmap, destRect, sampling, null);
        }
        else if (imageObj is SKBitmap bmp)
        {
            var destRect = SKRect.Create(dx, dy, dWidth ?? bmp.Width, dHeight ?? bmp.Height);
            Canvas.SkCanvas.DrawBitmap(bmp, destRect, sampling, null);
        }
    }

    public void drawSvg(object svgObj, float x = 0, float y = 0, float? width = null, float? height = null)
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
    #endregion
    #endregion

    #region Fields
    private readonly Stack<CanvasState> _states;
    private CanvasState _currentState;
    private readonly CanvasPath _currentPath;
    #endregion
}
