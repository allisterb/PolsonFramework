namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

public class CanvasState
{
    #region Constructors
    public CanvasState()
    {
        FillColor = SKColors.Black;
        StrokeColor = SKColors.Black;
        LineWidth = 1f;
        LineCap = SKStrokeCap.Butt;
        LineJoin = SKStrokeJoin.Miter;
        MiterLimit = 10f;
        GlobalAlpha = 1f;
        BlendMode = SKBlendMode.SrcOver;
        ShadowColor = SKColors.Transparent;
        Font = "10px sans-serif";
        FontSize = 10f;
        Typeface = SKTypeface.Default;
        TextAlign = "start";
        TextBaseline = "alphabetic";
        LetterSpacing = "0px";
    }

    public CanvasState(CanvasState other)
    {
        ArgumentNullException.ThrowIfNull(other);

        FillColor = other.FillColor;
        FillGradient = other.FillGradient;
        FillPattern = other.FillPattern;
        CustomFillShader = other.CustomFillShader;

        StrokeColor = other.StrokeColor;
        StrokeGradient = other.StrokeGradient;
        StrokePattern = other.StrokePattern;
        CustomStrokeShader = other.CustomStrokeShader;

        LineWidth = other.LineWidth;
        LineCap = other.LineCap;
        LineJoin = other.LineJoin;
        MiterLimit = other.MiterLimit;
        LineDash = other.LineDash != null ? (float[])other.LineDash.Clone() : null;
        LineDashOffset = other.LineDashOffset;

        GlobalAlpha = other.GlobalAlpha;
        BlendMode = other.BlendMode;

        ShadowColor = other.ShadowColor;
        ShadowBlur = other.ShadowBlur;
        ShadowOffsetX = other.ShadowOffsetX;
        ShadowOffsetY = other.ShadowOffsetY;

        Font = other.Font;
        FontSize = other.FontSize;
        Typeface = other.Typeface;
        TextAlign = other.TextAlign;
        TextBaseline = other.TextBaseline;
        LetterSpacing = other.LetterSpacing;

        ImageFilter = other.ImageFilter;
        ColorFilter = other.ColorFilter;
        PathEffect = other.PathEffect;
        MaskFilter = other.MaskFilter;
        Dither = other.Dither;
    }
    #endregion

    #region Properties
    public SKColor FillColor { get; set; }
    public CanvasGradient? FillGradient { get; set; }
    public CanvasPattern? FillPattern { get; set; }
    public SKShader? CustomFillShader { get; set; }

    public SKColor StrokeColor { get; set; }
    public CanvasGradient? StrokeGradient { get; set; }
    public CanvasPattern? StrokePattern { get; set; }
    public SKShader? CustomStrokeShader { get; set; }

    public float LineWidth { get; set; }
    public SKStrokeCap LineCap { get; set; }
    public SKStrokeJoin LineJoin { get; set; }
    public float MiterLimit { get; set; }
    public float[]? LineDash { get; set; }
    public float LineDashOffset { get; set; }

    public float GlobalAlpha { get; set; }
    public SKBlendMode BlendMode { get; set; }

    public SKColor ShadowColor { get; set; }
    public float ShadowBlur { get; set; }
    public float ShadowOffsetX { get; set; }
    public float ShadowOffsetY { get; set; }

    public string Font { get; set; }
    public float FontSize { get; set; }
    public SKTypeface Typeface { get; set; }
    public string TextAlign { get; set; }
    public string TextBaseline { get; set; }

    /// <summary>
    /// Tracking as an unresolved CSS length (<c>"3px"</c>, <c>"0.15em"</c>). Kept as written because
    /// <c>em</c> resolves against whatever <see cref="FontSize"/> is in force when the text is drawn,
    /// not when the spacing was set.
    /// </summary>
    public string LetterSpacing { get; set; }

    public SKImageFilter? ImageFilter { get; set; }
    public SKColorFilter? ColorFilter { get; set; }
    public SKPathEffect? PathEffect { get; set; }

    /// <summary>Applied to the shape's coverage mask before painting. See <c>Skia.MaskFilter</c>.</summary>
    public SKMaskFilter? MaskFilter { get; set; }

    /// <summary>
    /// Whether to dither, trading a little noise for the absence of banding.
    /// </summary>
    /// <remarks>
    /// Off by default, as Skia has it. It earns its place on a wide, shallow gradient — a sky, a soft
    /// tonal ramp — where 8-bit steps otherwise show as visible bands, and the noise that removes them
    /// is finer than the banding it replaces.
    /// </remarks>
    public bool Dither { get; set; }
    #endregion

    #region Methods
    public SKPaint CreateFillPaint()
    {
        var paint = new SKPaint
        {
            IsAntialias = true,
            IsDither = Dither,
            Style = SKPaintStyle.Fill,
            Color = BasePaintColor(FillColor, FillGradient != null || FillPattern != null || CustomFillShader != null),
            BlendMode = BlendMode,
            ImageFilter = ImageFilter,
            ColorFilter = ColorFilter,
            PathEffect = PathEffect,
            MaskFilter = MaskFilter
        };

        if (CustomFillShader != null)
        {
            paint.Shader = CustomFillShader;
        }
        else if (FillGradient != null)
        {
            paint.Shader = FillGradient.CreateShader();
        }
        else if (FillPattern != null)
        {
            paint.Shader = FillPattern.CreateShader();
        }

        ApplyShadow(paint);
        return paint;
    }

    public SKPaint CreateStrokePaint()
    {
        var paint = new SKPaint
        {
            IsAntialias = true,
            IsDither = Dither,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = LineWidth,
            StrokeCap = LineCap,
            StrokeJoin = LineJoin,
            StrokeMiter = MiterLimit,
            Color = BasePaintColor(StrokeColor, StrokeGradient != null || StrokePattern != null || CustomStrokeShader != null),
            BlendMode = BlendMode,
            ImageFilter = ImageFilter,
            ColorFilter = ColorFilter,
            PathEffect = PathEffect,
            MaskFilter = MaskFilter
        };

        if (LineDash != null && LineDash.Length > 0)
        {
            var dashEffect = SKPathEffect.CreateDash(LineDash, LineDashOffset);
            paint.PathEffect = PathEffect != null ? SKPathEffect.CreateCompose(PathEffect, dashEffect) : dashEffect;
        }

        if (CustomStrokeShader != null)
        {
            paint.Shader = CustomStrokeShader;
        }
        else if (StrokeGradient != null)
        {
            paint.Shader = StrokeGradient.CreateShader();
        }
        else if (StrokePattern != null)
        {
            paint.Shader = StrokePattern.CreateShader();
        }

        ApplyShadow(paint);
        return paint;
    }

    /// <summary>
    /// Paint for image and SVG compositing. Per the canvas model these honour globalAlpha,
    /// globalCompositeOperation, the filters and the shadow, but not fillStyle — the source
    /// pixels supply the colour, so no shader or fill colour is attached.
    /// </summary>
    public SKPaint CreateImagePaint()
    {
        var paint = new SKPaint
        {
            IsAntialias = true,
            IsDither = Dither,
            Color = SkiaColorParser.ApplyAlpha(SKColors.Black, GlobalAlpha),
            BlendMode = BlendMode,
            ImageFilter = ImageFilter,
            ColorFilter = ColorFilter
        };

        ApplyShadow(paint);
        return paint;
    }

    /// <summary>
    /// Base colour for a paint. Skia ignores a paint colour's RGB once a shader is attached and
    /// modulates the shader by its alpha alone, so with a gradient, pattern or shader in play the
    /// fill/stroke colour must not contribute — otherwise the alpha of the last <c>rgba(...)</c>
    /// assigned to <c>fillStyle</c> silently tints every later gradient. Only globalAlpha applies.
    /// </summary>
    private SKColor BasePaintColor(SKColor color, bool hasShader) =>
        SkiaColorParser.ApplyAlpha(hasShader ? SKColors.Black : color, GlobalAlpha);

    private void ApplyShadow(SKPaint paint)
    {
        if (ShadowColor.Alpha > 0 && (ShadowBlur > 0 || ShadowOffsetX != 0 || ShadowOffsetY != 0))
        {
            var sigma = ShadowBlur * 0.5f;
            var shadowFilter = SKImageFilter.CreateDropShadowOnly(
                ShadowOffsetX, ShadowOffsetY, sigma, sigma, SkiaColorParser.ApplyAlpha(ShadowColor, GlobalAlpha));

            paint.ImageFilter = paint.ImageFilter != null
                ? SKImageFilter.CreateCompose(paint.ImageFilter, shadowFilter)
                : shadowFilter;
        }
    }
    #endregion
}

