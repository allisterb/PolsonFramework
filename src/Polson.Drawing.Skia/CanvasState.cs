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

        ImageFilter = other.ImageFilter;
        ColorFilter = other.ColorFilter;
        PathEffect = other.PathEffect;
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

    public SKImageFilter? ImageFilter { get; set; }
    public SKColorFilter? ColorFilter { get; set; }
    public SKPathEffect? PathEffect { get; set; }
    #endregion

    #region Methods
    public SKPaint CreateFillPaint()
    {
        var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SkiaColorParser.ApplyAlpha(FillColor, GlobalAlpha),
            BlendMode = BlendMode,
            ImageFilter = ImageFilter,
            ColorFilter = ColorFilter,
            PathEffect = PathEffect
        };

        if (CustomFillShader != null)
        {
            paint.Shader = CustomFillShader;
        }
        else if (FillGradient != null)
        {
            paint.Shader = FillGradient.CreateShader(GlobalAlpha);
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
            Style = SKPaintStyle.Stroke,
            StrokeWidth = LineWidth,
            StrokeCap = LineCap,
            StrokeJoin = LineJoin,
            StrokeMiter = MiterLimit,
            Color = SkiaColorParser.ApplyAlpha(StrokeColor, GlobalAlpha),
            BlendMode = BlendMode,
            ImageFilter = ImageFilter,
            ColorFilter = ColorFilter,
            PathEffect = PathEffect
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
            paint.Shader = StrokeGradient.CreateShader(GlobalAlpha);
        }
        else if (StrokePattern != null)
        {
            paint.Shader = StrokePattern.CreateShader();
        }

        ApplyShadow(paint);
        return paint;
    }

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

