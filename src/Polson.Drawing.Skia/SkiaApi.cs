namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

public class SkiaApi
{
    #region Properties
    public SkiaShaderApi Shader { get; } = new();
    public SkiaImageFilterApi ImageFilter { get; } = new();
    public SkiaColorFilterApi ColorFilter { get; } = new();
    public SkiaPathEffectApi PathEffect { get; } = new();
    public SkiaImageApi Image { get; } = new();
    public SkiaBitmapFactoryApi Bitmap { get; } = new();
    public ConstructiveDrawingToolkit Drawing { get; } = new();
    public LogoDesignToolkit Logo { get; } = new();
    public LogoTypeToolkit LogoType { get; } = new();
    #endregion
}

public class SkiaImageApi
{
    #region Methods
    public SkiaBitmapWrapper load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Image file not found: {filePath}", filePath);

        using var stream = File.OpenRead(filePath);
        var bmp = SKBitmap.Decode(stream);
        if (bmp == null)
            throw new InvalidOperationException($"Failed to decode image from {filePath}");

        return new SkiaBitmapWrapper(bmp);
    }

    public SkiaBitmapWrapper fromDataUrl(string dataUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataUrl);
        var commaIdx = dataUrl.IndexOf(',');
        var base64 = commaIdx >= 0 ? dataUrl[(commaIdx + 1)..] : dataUrl;
        var bytes = Convert.FromBase64String(base64);
        return fromBytes(bytes);
    }

    public SkiaBitmapWrapper fromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var bmp = SKBitmap.Decode(bytes);
        if (bmp == null)
            throw new InvalidOperationException("Failed to decode image from byte buffer");

        return new SkiaBitmapWrapper(bmp);
    }
    #endregion
}

public class SkiaBitmapFactoryApi
{
    #region Methods
    public SkiaBitmapWrapper create(int width, int height) =>
        new(width, height);
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

    public SKShader bitmap(object bitmapObj, string tileX = "clamp", string tileY = "clamp")
    {
        var bmp = bitmapObj switch
        {
            SkiaBitmapWrapper bw => bw.Bitmap,
            SkiaCanvas sc => sc.Bitmap,
            SKBitmap b => b,
            _ => throw new ArgumentException("Invalid bitmap object", nameof(bitmapObj))
        };

        var tx = ParseTileMode(tileX);
        var ty = ParseTileMode(tileY);
        return SKShader.CreateBitmap(bmp, tx, ty);
    }

    public SKShader sksl(string skslCode, object? uniformsObj = null, object? childrenObj = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skslCode);
        using var effect = SKRuntimeEffect.CreateShader(skslCode, out var errors);
        if (effect == null || !string.IsNullOrEmpty(errors))
        {
            throw new InvalidOperationException($"SkSL compilation error: {errors}");
        }

        var uniforms = PopulateUniforms(effect, uniformsObj);
        var children = PopulateChildren(effect, childrenObj);

        if (children != null)
        {
            uniforms ??= new SKRuntimeEffectUniforms(effect);
            return effect.ToShader(uniforms, children);
        }

        return uniforms != null ? effect.ToShader(uniforms) : effect.ToShader();
    }

    public SKShader custom(string skslCode, object? uniformsObj = null, object? childrenObj = null) =>
        sksl(skslCode, uniformsObj, childrenObj);

    internal static SKRuntimeEffectUniforms? PopulateUniforms(SKRuntimeEffect effect, object? uniformsObj)
    {
        if (uniformsObj == null) return null;

        var uniforms = new SKRuntimeEffectUniforms(effect);

        if (uniformsObj is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrEmpty(key)) continue;

                SetUniformValue(uniforms, key, entry.Value);
            }
        }
        else if (uniformsObj is IDictionary<string, object?> strDict)
        {
            foreach (var kvp in strDict)
            {
                SetUniformValue(uniforms, kvp.Key, kvp.Value);
            }
        }

        return uniforms;
    }

    private static void SetUniformValue(SKRuntimeEffectUniforms uniforms, string name, object? val)
    {
        if (val == null) return;

        if (val is float f)
        {
            uniforms[name] = f;
        }
        else if (val is double d)
        {
            uniforms[name] = (float)d;
        }
        else if (val is int i)
        {
            uniforms[name] = i;
        }
        else if (val is float[] fArr)
        {
            uniforms[name] = fArr;
        }
        else if (val is int[] iArr)
        {
            uniforms[name] = iArr;
        }
        else if (val is string s && (s.StartsWith('#') || s.StartsWith("rgb") || s.StartsWith("hsl")))
        {
            var col = SkiaColorParser.Parse(s);
            uniforms[name] = new[] { col.Red / 255f, col.Green / 255f, col.Blue / 255f, col.Alpha / 255f };
        }
        else if (val is IEnumerable enumerable and not string)
        {
            var list = new List<float>();
            foreach (var item in enumerable)
            {
                list.Add(Convert.ToSingle(item));
            }
            uniforms[name] = list.ToArray();
        }
        else
        {
            try
            {
                uniforms[name] = Convert.ToSingle(val);
            }
            catch
            {
                // Ignore unrecognized uniform
            }
        }
    }

    internal static SKRuntimeEffectChildren? PopulateChildren(SKRuntimeEffect effect, object? childrenObj)
    {
        if (childrenObj == null) return null;

        var children = new SKRuntimeEffectChildren(effect);

        if (childrenObj is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrEmpty(key)) continue;

                var shader = ResolveShader(entry.Value);
                if (shader != null) children[key] = shader;
            }
        }
        else if (childrenObj is IDictionary<string, object?> strDict)
        {
            foreach (var kvp in strDict)
            {
                var shader = ResolveShader(kvp.Value);
                if (shader != null) children[kvp.Key] = shader;
            }
        }

        return children;
    }

    private static SKShader? ResolveShader(object? obj) => obj switch
    {
        SKShader s => s,
        SkiaBitmapWrapper bw => SKShader.CreateBitmap(bw.Bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp),
        SkiaCanvas sc => SKShader.CreateBitmap(sc.Bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp),
        _ => null
    };

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

    public SKImageFilter runtimeShader(string skslCode, object? uniformsObj = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skslCode);
        using var effect = SKRuntimeEffect.CreateShader(skslCode, out var errors);
        if (effect == null || !string.IsNullOrEmpty(errors))
        {
            throw new InvalidOperationException($"SkSL compilation error: {errors}");
        }

        var uniforms = SkiaShaderApi.PopulateUniforms(effect, uniformsObj);
        var shader = uniforms != null ? effect.ToShader(uniforms) : effect.ToShader();
        return SKImageFilter.CreateShader(shader);
    }
    #endregion
}

public class SkiaColorFilterApi
{
    #region Methods
    public SKColorFilter runtimeEffect(string skslCode, object? uniformsObj = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skslCode);
        using var effect = SKRuntimeEffect.CreateColorFilter(skslCode, out var errors);
        if (effect == null || !string.IsNullOrEmpty(errors))
        {
            throw new InvalidOperationException($"SkSL color filter compilation error: {errors}");
        }

        var uniforms = SkiaShaderApi.PopulateUniforms(effect, uniformsObj);
        return uniforms != null ? effect.ToColorFilter(uniforms) : effect.ToColorFilter();
    }
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

