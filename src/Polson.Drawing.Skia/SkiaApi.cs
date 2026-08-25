namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

public class SkiaApi
{
    #region Properties
    public SkiaShaderApi Shader { get; } = new();
    public SkiaImageFilterApi ImageFilter { get; } = new();
    public SkiaColorFilterApi ColorFilter { get; } = new();
    public SkiaPathEffectApi PathEffect { get; } = new();
    #endregion
}

public class SkiaShaderApi
{
    #region Methods
    public SKShader perlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, float tileSizeX = 0, float tileSizeY = 0)
    {
        if (tileSizeX > 0 && tileSizeY > 0)
        {
            return SKShader.CreatePerlinNoiseTurbulence(
                baseFrequencyX, baseFrequencyY, numOctaves, seed, new SKPointI((int)tileSizeX, (int)tileSizeY));
        }
        return SKShader.CreatePerlinNoiseTurbulence(baseFrequencyX, baseFrequencyY, numOctaves, seed);
    }

    public SKShader perlinNoiseFractal(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, float tileSizeX = 0, float tileSizeY = 0)
    {
        if (tileSizeX > 0 && tileSizeY > 0)
        {
            return SKShader.CreatePerlinNoiseFractalNoise(
                baseFrequencyX, baseFrequencyY, numOctaves, seed, new SKPointI((int)tileSizeX, (int)tileSizeY));
        }
        return SKShader.CreatePerlinNoiseFractalNoise(baseFrequencyX, baseFrequencyY, numOctaves, seed);
    }

    public SKShader twoPointConical(float x0, float y0, float r0, float x1, float y1, float r1, object colorsObj, object? posObj = null, string tileMode = "clamp")
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        var tm = ParseTileMode(tileMode);
        return SKShader.CreateTwoPointConicalGradient(new SKPoint(x0, y0), r0, new SKPoint(x1, y1), r1, colors, positions, tm);
    }

    public SKShader sweep(float cx, float cy, object colorsObj, object? posObj = null, float startAngleDeg = 0f, float endAngleDeg = 360f)
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        return SKShader.CreateSweepGradient(new SKPoint(cx, cy), colors, positions, SKShaderTileMode.Clamp, startAngleDeg, endAngleDeg);
    }

    public SKShader linear(float x0, float y0, float x1, float y1, object colorsObj, object? posObj = null, string tileMode = "clamp")
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        var tm = ParseTileMode(tileMode);
        return SKShader.CreateLinearGradient(new SKPoint(x0, y0), new SKPoint(x1, y1), colors, positions, tm);
    }

    public SKShader radial(float cx, float cy, float radius, object colorsObj, object? posObj = null, string tileMode = "clamp")
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        var tm = ParseTileMode(tileMode);
        return SKShader.CreateRadialGradient(new SKPoint(cx, cy), radius, colors, positions, tm);
    }

    private static SKColor[] ParseColors(object obj)
    {
        if (obj is IEnumerable enumerable and not string)
        {
            var list = new List<SKColor>();
            foreach (var item in enumerable)
            {
                list.Add(SkiaColorParser.Parse(item?.ToString()));
            }
            if (list.Count > 0) return list.ToArray();
        }
        return [SKColors.Black, SKColors.White];
    }

    private static float[]? ParsePositions(object? obj)
    {
        if (obj is IEnumerable enumerable and not string)
        {
            var list = new List<float>();
            foreach (var item in enumerable)
            {
                list.Add(Convert.ToSingle(item));
            }
            if (list.Count > 0) return list.ToArray();
        }
        return null;
    }

    private static SKShaderTileMode ParseTileMode(string mode) => mode.ToLowerInvariant() switch
    {
        "repeat" => SKShaderTileMode.Repeat,
        "mirror" => SKShaderTileMode.Mirror,
        "decal" => SKShaderTileMode.Decal,
        _ => SKShaderTileMode.Clamp
    };
    #endregion
}

public class SkiaImageFilterApi
{
    #region Methods
    public SKImageFilter blur(float sigmaX, float sigmaY) =>
        SKImageFilter.CreateBlur(sigmaX, sigmaY);

    public SKImageFilter dropShadow(float dx, float dy, float sigmaX, float sigmaY, string color)
    {
        var skColor = SkiaColorParser.Parse(color);
        return SKImageFilter.CreateDropShadow(dx, dy, sigmaX, sigmaY, skColor);
    }

    public SKImageFilter dilate(int radiusX, int radiusY) =>
        SKImageFilter.CreateDilate(radiusX, radiusY);

    public SKImageFilter erode(int radiusX, int radiusY) =>
        SKImageFilter.CreateErode(radiusX, radiusY);

    public SKImageFilter colorFilter(SKColorFilter filter) =>
        SKImageFilter.CreateColorFilter(filter);
    #endregion
}

public class SkiaColorFilterApi
{
    #region Methods
    public SKColorFilter colorMatrix(object matrixObj)
    {
        var list = new List<float>();
        if (matrixObj is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                list.Add(Convert.ToSingle(item));
            }
        }

        if (list.Count >= 20)
        {
            return SKColorFilter.CreateColorMatrix(list.Take(20).ToArray());
        }

        // Identity 4x5 matrix
        var id = new float[20]
        {
            1, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, 1, 0
        };
        return SKColorFilter.CreateColorMatrix(id);
    }

    public SKColorFilter blend(string color, string blendMode = "srcOver")
    {
        var skColor = SkiaColorParser.Parse(color);
        var bm = ParseBlendMode(blendMode);
        return SKColorFilter.CreateBlendMode(skColor, bm);
    }

    public SKColorFilter highContrast(bool grayscale = true, string invertStyle = "none", float contrast = 0.5f)
    {
        var style = invertStyle.ToLowerInvariant() switch
        {
            "brightness" => SKHighContrastConfigInvertStyle.InvertBrightness,
            "lightness" => SKHighContrastConfigInvertStyle.InvertLightness,
            _ => SKHighContrastConfigInvertStyle.NoInvert
        };

        var config = new SKHighContrastConfig(grayscale, style, Math.Clamp(contrast, -1f, 1f));
        return SKColorFilter.CreateHighContrast(config);
    }

    public static SKBlendMode ParseBlendMode(string mode) => mode.ToLowerInvariant() switch
    {
        "clear" => SKBlendMode.Clear,
        "src" or "copy" => SKBlendMode.Src,
        "dst" => SKBlendMode.Dst,
        "srcover" or "source-over" => SKBlendMode.SrcOver,
        "dstover" or "destination-over" => SKBlendMode.DstOver,
        "srcin" or "source-in" => SKBlendMode.SrcIn,
        "dstin" or "destination-in" => SKBlendMode.DstIn,
        "srcout" or "source-out" => SKBlendMode.SrcOut,
        "dstout" or "destination-out" => SKBlendMode.DstOut,
        "srcatop" or "source-atop" => SKBlendMode.SrcATop,
        "dstatop" or "destination-atop" => SKBlendMode.DstATop,
        "xor" => SKBlendMode.Xor,
        "plus" or "lighter" => SKBlendMode.Plus,
        "multiply" => SKBlendMode.Multiply,
        "screen" => SKBlendMode.Screen,
        "overlay" => SKBlendMode.Overlay,
        "darken" => SKBlendMode.Darken,
        "lighten" => SKBlendMode.Lighten,
        "colordodge" or "color-dodge" => SKBlendMode.ColorDodge,
        "colorburn" or "color-burn" => SKBlendMode.ColorBurn,
        "hardlight" or "hard-light" => SKBlendMode.HardLight,
        "softlight" or "soft-light" => SKBlendMode.SoftLight,
        "difference" => SKBlendMode.Difference,
        "exclusion" => SKBlendMode.Exclusion,
        "hue" => SKBlendMode.Hue,
        "saturation" => SKBlendMode.Saturation,
        "color" => SKBlendMode.Color,
        "luminosity" => SKBlendMode.Luminosity,
        _ => SKBlendMode.SrcOver
    };
    #endregion
}

public class SkiaPathEffectApi
{
    #region Methods
    public SKPathEffect dash(object intervalsObj, float phase = 0f)
    {
        var list = new List<float>();
        if (intervalsObj is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                list.Add(Convert.ToSingle(item));
            }
        }
        var arr = list.Count >= 2 ? list.ToArray() : [10f, 5f];
        return SKPathEffect.CreateDash(arr, phase);
    }

    public SKPathEffect corner(float radius) =>
        SKPathEffect.CreateCorner(radius);

    public SKPathEffect discrete(float segLength, float deviation, uint seed = 0) =>
        SKPathEffect.CreateDiscrete(segLength, deviation, seed);
    #endregion
}

