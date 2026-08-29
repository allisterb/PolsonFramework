namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

/// <summary>Root of the <c>Skia</c> namespace: shaders, filters, images and bitmaps.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaApi
{
    #region Constructors
    /// <summary>
    /// Builds the namespace, optionally rooted at a project so file paths resolve against it.
    /// </summary>
    /// <remarks>
    /// The root is passed in per execution rather than held statically: the server hosts one project,
    /// but the tests host several in one process, and a static would let one leak into another.
    /// </remarks>
    public SkiaApi(string? projectRoot = null) => Image = new SkiaImageApi(projectRoot);
    #endregion

    #region Properties
    public SkiaShaderApi Shader { get; } = new();
    public SkiaImageFilterApi ImageFilter { get; } = new();
    public SkiaColorFilterApi ColorFilter { get; } = new();
    public SkiaPathEffectApi PathEffect { get; } = new();
    public SkiaMaskFilterApi MaskFilter { get; } = new();
    public SkiaBrushApi Brush { get; } = new();
    public SkiaImageApi Image { get; }
    public SkiaBitmapFactoryApi Bitmap { get; } = new();
    public SkiaFontApi Font { get; } = new();
    public ConstructiveDrawingToolkit Drawing { get; } = new();
    public LogoDesignToolkit Logo { get; } = new();
    public LogoTypeToolkit LogoType { get; } = new();
    #endregion
}

/// <summary>The <c>Skia.Image</c> sub-namespace.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaImageApi
{
    #region Fields
    /// <summary>
    /// The project directory a relative path resolves against, if there is one.
    /// </summary>
    /// <remarks>
    /// Deliberately not a public property: every public member of this class is part of the JS
    /// surface and has to be documented as such, and an absolute host path is not something a script
    /// has any use for.
    /// </remarks>
    private readonly string? projectRoot;
    #endregion

    #region Constructors
    public SkiaImageApi(string? projectRoot = null) => this.projectRoot = projectRoot;
    #endregion

    #region Methods
    /// <summary>
    /// Loads an image from a path relative to the project directory.
    /// </summary>
    /// <remarks>
    /// The same resolution as <c>outFile</c>, deliberately: <c>Skia.Image.load('artifacts/x.webp')</c>
    /// used to resolve against the server's working directory instead, so a path an agent had just
    /// written to came back as "not found". A path means the same thing whichever call takes it.
    /// </remarks>
    public SkiaBitmapWrapper Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Read");

        if (!File.Exists(full))
        {
            throw new FileNotFoundException(
                string.IsNullOrEmpty(projectRoot) || full.Equals(filePath, StringComparison.Ordinal)
                    ? $"Image file not found: {filePath}"
                    : $"Image file not found: '{filePath}' resolves to '{full}'. Paths are relative to the project directory.",
                full);
        }

        using var stream = File.OpenRead(full);
        var bmp = SKBitmap.Decode(stream);
        if (bmp == null)
            throw new InvalidOperationException($"Failed to decode image from {filePath}");

        return new SkiaBitmapWrapper(bmp);
    }

    public SkiaBitmapWrapper FromDataUrl(string dataUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataUrl);
        var commaIdx = dataUrl.IndexOf(',');
        var base64 = commaIdx >= 0 ? dataUrl[(commaIdx + 1)..] : dataUrl;
        var bytes = Convert.FromBase64String(base64);
        return FromBytes(bytes);
    }

    public SkiaBitmapWrapper FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var bmp = SKBitmap.Decode(bytes);
        if (bmp == null)
            throw new InvalidOperationException("Failed to decode image from byte buffer");

        return new SkiaBitmapWrapper(bmp);
    }
    #endregion
}

/// <summary>The <c>Skia.Bitmap</c> sub-namespace.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaBitmapFactoryApi
{
    #region Methods
    public SkiaBitmapWrapper Create(int width, int height) =>
        new(width, height);
    #endregion
}

/// <summary>The <c>Skia.Shader</c> sub-namespace, including SkSL compilation.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaShaderApi
{
    #region Methods
    public SKShader PerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, float tileSizeX = 0, float tileSizeY = 0)
    {
        if (tileSizeX > 0 && tileSizeY > 0)
        {
            return SKShader.CreatePerlinNoiseTurbulence(
                baseFrequencyX, baseFrequencyY, numOctaves, seed, new SKPointI((int)tileSizeX, (int)tileSizeY));
        }
        return SKShader.CreatePerlinNoiseTurbulence(baseFrequencyX, baseFrequencyY, numOctaves, seed);
    }

    public SKShader PerlinNoiseFractal(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, float tileSizeX = 0, float tileSizeY = 0)
    {
        if (tileSizeX > 0 && tileSizeY > 0)
        {
            return SKShader.CreatePerlinNoiseFractalNoise(
                baseFrequencyX, baseFrequencyY, numOctaves, seed, new SKPointI((int)tileSizeX, (int)tileSizeY));
        }
        return SKShader.CreatePerlinNoiseFractalNoise(baseFrequencyX, baseFrequencyY, numOctaves, seed);
    }

    public SKShader TwoPointConical(float x0, float y0, float r0, float x1, float y1, float r1, object colorsObj, object? posObj = null, string tileMode = "clamp")
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        var tm = ParseTileMode(tileMode);
        return SKShader.CreateTwoPointConicalGradient(new SKPoint(x0, y0), r0, new SKPoint(x1, y1), r1, colors, positions, tm);
    }

    public SKShader Sweep(float cx, float cy, object colorsObj, object? posObj = null, float startAngleDeg = 0f, float endAngleDeg = 360f)
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        return SKShader.CreateSweepGradient(new SKPoint(cx, cy), colors, positions, SKShaderTileMode.Clamp, startAngleDeg, endAngleDeg);
    }

    public SKShader Linear(float x0, float y0, float x1, float y1, object colorsObj, object? posObj = null, string tileMode = "clamp")
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        var tm = ParseTileMode(tileMode);
        return SKShader.CreateLinearGradient(new SKPoint(x0, y0), new SKPoint(x1, y1), colors, positions, tm);
    }

    public SKShader Radial(float cx, float cy, float radius, object colorsObj, object? posObj = null, string tileMode = "clamp")
    {
        var colors = ParseColors(colorsObj);
        var positions = ParsePositions(posObj);
        var tm = ParseTileMode(tileMode);
        return SKShader.CreateRadialGradient(new SKPoint(cx, cy), radius, colors, positions, tm);
    }

    public SKShader Bitmap(object bitmapObj, string tileX = "clamp", string tileY = "clamp")
    {
        var bmp = bitmapObj switch
        {
            SkiaBitmapWrapper bw => bw.Bitmap,
            SkiaCanvas sc => sc.SkBitmap,
            SKBitmap b => b,
            _ => throw new ArgumentException("Invalid bitmap object", nameof(bitmapObj))
        };

        var tx = ParseTileMode(tileX);
        var ty = ParseTileMode(tileY);
        return SKShader.CreateBitmap(bmp, tx, ty);
    }

    public SKShader Sksl(string skslCode, object? uniformsObj = null, object? childrenObj = null)
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

    public SKShader Custom(string skslCode, object? uniformsObj = null, object? childrenObj = null) =>
        Sksl(skslCode, uniformsObj, childrenObj);

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
        SkiaCanvas sc => SKShader.CreateBitmap(sc.SkBitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp),
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

/// <summary>The <c>Skia.ImageFilter</c> sub-namespace.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaImageFilterApi
{
    #region Methods
    public SKImageFilter Blur(float sigmaX, float sigmaY) =>
        SKImageFilter.CreateBlur(sigmaX, sigmaY);

    public SKImageFilter DropShadow(float dx, float dy, float sigmaX, float sigmaY, string color)
    {
        var skColor = SkiaColorParser.Parse(color);
        return SKImageFilter.CreateDropShadow(dx, dy, sigmaX, sigmaY, skColor);
    }

    public SKImageFilter Dilate(int radiusX, int radiusY) =>
        SKImageFilter.CreateDilate(radiusX, radiusY);

    public SKImageFilter Erode(int radiusX, int radiusY) =>
        SKImageFilter.CreateErode(radiusX, radiusY);

    public SKImageFilter ColorFilter(SKColorFilter filter) =>
        SKImageFilter.CreateColorFilter(filter);

    public SKImageFilter RuntimeShader(string skslCode, object? uniformsObj = null)
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

/// <summary>The <c>Skia.ColorFilter</c> sub-namespace.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaColorFilterApi
{
    #region Methods
    public SKColorFilter RuntimeEffect(string skslCode, object? uniformsObj = null)
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
    public SKColorFilter ColorMatrix(object matrixObj)
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

    public SKColorFilter Blend(string color, string blendMode = "srcOver")
    {
        var skColor = SkiaColorParser.Parse(color);
        var bm = ParseBlendMode(blendMode);
        return SKColorFilter.CreateBlendMode(skColor, bm);
    }

    public SKColorFilter HighContrast(bool grayscale = true, string invertStyle = "none", float contrast = 0.5f)
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

/// <summary>The <c>Skia.PathEffect</c> sub-namespace.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
/// <summary>The <c>Skia.Font</c> sub-namespace — which typefaces this machine can actually render.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// Skia substitutes a default face for a family it does not have, silently and with no signal, so
/// <c>ctx.font = '40px Didot'</c> renders in something else and measures as something else. Without
/// a way to enumerate or test, the only way to discover this is to measure text against a control
/// string — which is what an agent had to do, after choosing a typeface by elimination.
/// </para>
/// </remarks>
public class SkiaFontApi
{
    #region Methods
    /// <summary>Every font family installed on this machine, sorted.</summary>
    public string[] Families()
    {
        using var manager = SKFontManager.CreateDefault();
        return [.. manager.FontFamilies.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether <paramref name="family"/> resolves to itself rather than a substitute.</summary>
    public bool Has(string family) =>
        !string.IsNullOrWhiteSpace(family)
        && string.Equals(Resolve(family), family.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The family name that would actually be used for <paramref name="family"/>. When it differs
    /// from what was asked for, the request fell back.
    /// </summary>
    public string Resolve(string family)
    {
        if (string.IsNullOrWhiteSpace(family)) return SKTypeface.Default.FamilyName;

        using var typeface = SKTypeface.FromFamilyName(family.Trim());
        return typeface?.FamilyName ?? SKTypeface.Default.FamilyName;
    }
    #endregion
}

public class SkiaPathEffectApi
{
    #region Methods
    public SKPathEffect Dash(object intervalsObj, float phase = 0f)
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

    public SKPathEffect Corner(float radius) =>
        SKPathEffect.CreateCorner(radius);

    public SKPathEffect Discrete(float segLength, float deviation, uint seed = 0) =>
        SKPathEffect.CreateDiscrete(segLength, deviation, seed);

    /// <summary>
    /// Stamps a shape repeatedly along the path being stroked — the brush primitive.
    /// </summary>
    /// <remarks>
    /// This is what a brush *is*: a mark repeated along a stroke rather than a constant-width ribbon.
    /// With <c>style: 'rotate'</c> each stamp turns to follow the path's tangent, which is what makes
    /// bristles, foliage, grass, stitching and chain read as drawn rather than as a row of pasted
    /// copies. Compose it with <see cref="Discrete"/> through <see cref="Sum"/> for natural media,
    /// where the irregularity is the point.
    /// <para>
    /// <paramref name="advance"/> is the distance between stamps in pixels. Below the stamp's own
    /// width they overlap into a continuous textured band; above it they read as discrete marks.
    /// </para>
    /// </remarks>
    /// <param name="shape">The mark to stamp: a <c>CanvasPath</c>, an <c>SKPath</c>, or an SVG path string.</param>
    /// <param name="advance">Distance between successive stamps, in pixels.</param>
    /// <param name="phase">Offset into the first interval.</param>
    /// <param name="style">'rotate' (follow the tangent), 'translate' (keep upright), or 'morph' (bend to the path).</param>
    public SKPathEffect Stamp(object shape, float advance, float phase = 0f, string style = "rotate")
    {
        var path = ToPath(shape, nameof(shape));

        if (advance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(advance), advance, "advance must be greater than zero, or the stamp has nowhere to go.");

        var mode = style?.ToLowerInvariant() switch
        {
            "translate" => SKPath1DPathEffectStyle.Translate,
            "morph" => SKPath1DPathEffectStyle.Morph,
            "rotate" or null or "" => SKPath1DPathEffectStyle.Rotate,
            _ => throw new ArgumentException($"Unknown stamp style '{style}'. Use 'rotate', 'translate' or 'morph'.", nameof(style)),
        };

        return SKPathEffect.Create1DPath(path, advance, phase, mode);
    }

    /// <summary>
    /// Fills the stroked or filled region with parallel hatch lines, as geometry.
    /// </summary>
    /// <remarks>
    /// Real lines rather than a shader, so they survive scaling and export as vector. Cross-hatching
    /// is two of these summed at different angles — see <see cref="Sum"/>.
    /// </remarks>
    /// <param name="width">Thickness of each hatch line.</param>
    /// <param name="spacing">Distance between lines.</param>
    /// <param name="angleDeg">Direction of the hatching, in degrees.</param>
    public SKPathEffect Hatch(float width, float spacing, float angleDeg = 45f)
    {
        if (spacing <= 0f)
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "spacing must be greater than zero.");

        var matrix = SKMatrix.CreateScale(spacing, spacing);
        matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(angleDeg));

        return SKPathEffect.Create2DLine(width, matrix);
    }

    /// <summary>
    /// Tiles a shape across the region on a square lattice — stipple, screen tone, repeated motif.
    /// </summary>
    /// <remarks>
    /// The geometric counterpart to <c>Drawing.createHalftoneDotShader</c>: a shader colours pixels,
    /// this places actual shapes, so the result scales and exports as vector.
    /// </remarks>
    /// <param name="shape">The motif: a <c>CanvasPath</c>, an <c>SKPath</c>, or an SVG path string.</param>
    /// <param name="spacing">Lattice pitch in pixels.</param>
    /// <param name="angleDeg">Rotation of the lattice, in degrees.</param>
    public SKPathEffect Tile(object shape, float spacing, float angleDeg = 0f)
    {
        var path = ToPath(shape, nameof(shape));

        if (spacing <= 0f)
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "spacing must be greater than zero.");

        var matrix = SKMatrix.CreateScale(spacing, spacing);
        matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees(angleDeg));

        return SKPathEffect.Create2DPath(matrix, path);
    }

    /// <summary>Applies both effects to the original path and draws both results.</summary>
    /// <remarks>
    /// Additive, unlike <see cref="Compose"/>: each effect sees the same input. Two <see cref="Hatch"/>
    /// effects summed at different angles give cross-hatching; a stamp summed with a plain stroke
    /// gives a textured edge that still has a continuous line under it.
    /// </remarks>
    public SKPathEffect Sum(SKPathEffect first, SKPathEffect second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return SKPathEffect.CreateSum(first, second);
    }

    /// <summary>Applies <paramref name="inner"/> first, then <paramref name="outer"/> to its result.</summary>
    /// <remarks>
    /// Sequential, unlike <see cref="Sum"/>. This is how natural media are built: jitter the path with
    /// <see cref="Discrete"/>, then stamp along the jittered result, and the marks inherit the
    /// irregularity instead of marching evenly down a clean curve.
    /// </remarks>
    public SKPathEffect Compose(SKPathEffect outer, SKPathEffect inner)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);

        return SKPathEffect.CreateCompose(outer, inner);
    }

    /// <summary>Accepts the several ways a script can name a shape.</summary>
    private static SKPath ToPath(object shape, string parameterName) => shape switch
    {
        SKPath path => path,
        CanvasPath canvasPath => canvasPath.Path,
        string svg => SKPath.ParseSvgPathData(svg)
            ?? throw new ArgumentException($"Could not parse '{svg}' as SVG path data.", parameterName),
        null => throw new ArgumentNullException(parameterName),
        _ => throw new ArgumentException(
            $"Expected a CanvasPath, an SKPath or an SVG path string, but got {shape.GetType().Name}.", parameterName),
    };
    #endregion
}

/// <summary>The <c>Skia.MaskFilter</c> sub-namespace: effects applied to a shape's coverage mask.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// A mask filter acts on the shape's alpha before it is painted, which is why it can soften an edge
/// while leaving the fill flat — unlike <c>Skia.ImageFilter.blur</c>, which blurs the drawn result.
/// </para>
/// </remarks>
public class SkiaMaskFilterApi
{
    #region Methods
    /// <summary>
    /// Blurs the shape's coverage mask: soft edges, glow, and airbrush.
    /// </summary>
    /// <remarks>
    /// The style decides what survives:
    /// <list type="bullet">
    /// <item><c>'normal'</c> — the whole shape softens. An airbrushed mark.</item>
    /// <item><c>'solid'</c> — the shape stays solid and the blur is added outside it. A glow around a crisp form.</item>
    /// <item><c>'outer'</c> — only the blur outside remains, the shape itself is knocked out. A halo, or a cast glow.</item>
    /// <item><c>'inner'</c> — only the blur inside remains. An inward vignette on a form.</item>
    /// </list>
    /// </remarks>
    /// <param name="sigma">Blur radius. Roughly half the visible softness in pixels.</param>
    /// <param name="style">'normal', 'solid', 'outer' or 'inner'.</param>
    public SKMaskFilter Blur(float sigma, string style = "normal")
    {
        if (sigma <= 0f)
            throw new ArgumentOutOfRangeException(nameof(sigma), sigma, "sigma must be greater than zero.");

        var blurStyle = style?.ToLowerInvariant() switch
        {
            "solid" => SKBlurStyle.Solid,
            "outer" => SKBlurStyle.Outer,
            "inner" => SKBlurStyle.Inner,
            "normal" or null or "" => SKBlurStyle.Normal,
            _ => throw new ArgumentException($"Unknown blur style '{style}'. Use 'normal', 'solid', 'outer' or 'inner'.", nameof(style)),
        };

        return SKMaskFilter.CreateBlur(blurStyle, sigma);
    }
    #endregion
}

