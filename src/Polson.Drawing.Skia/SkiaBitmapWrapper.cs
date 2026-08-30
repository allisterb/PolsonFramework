namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;

/// <summary>Editable bitmap with post-processing and encoding helpers.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaBitmapWrapper : IDisposable
{
    #region Constructors
    public SkiaBitmapWrapper(int width, int height)
    {
        var info = new SKImageInfo(Math.Max(1, width), Math.Max(1, height), SKColorType.Rgba8888, SKAlphaType.Premul);
        Bitmap = new SKBitmap(info);
        Bitmap.Erase(SKColors.Transparent);
    }

    public SkiaBitmapWrapper(SKBitmap bitmap)
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }
    #endregion

    #region Properties
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public SKBitmap Bitmap { get; }
    #endregion

    #region Methods
    public SkiaBitmapWrapper ExtractSubset(int x, int y, int width, int height)
    {
        var clampedX = Math.Clamp(x, 0, Bitmap.Width);
        var clampedY = Math.Clamp(y, 0, Bitmap.Height);
        var clampedW = Math.Clamp(width, 1, Bitmap.Width - clampedX);
        var clampedH = Math.Clamp(height, 1, Bitmap.Height - clampedY);

        var rect = SKRectI.Create(clampedX, clampedY, clampedW, clampedH);
        var subset = new SKBitmap();
        if (Bitmap.ExtractSubset(subset, rect))
        {
            return new SkiaBitmapWrapper(subset);
        }

        var copy = new SKBitmap(new SKImageInfo(clampedW, clampedH, Bitmap.ColorType, Bitmap.AlphaType));
        using (var canvas = new SKCanvas(copy))
        {
            var src = SKRect.Create(clampedX, clampedY, clampedW, clampedH);
            var dest = SKRect.Create(0, 0, clampedW, clampedH);
            canvas.DrawBitmap(Bitmap, src, dest, new SKSamplingOptions(SKFilterMode.Linear), null);
        }
        return new SkiaBitmapWrapper(copy);
    }

    public SkiaBitmapWrapper Resize(int targetWidth, int targetHeight, string quality = "linear")
    {
        var tw = Math.Max(1, targetWidth);
        var th = Math.Max(1, targetHeight);

        var info = new SKImageInfo(tw, th, Bitmap.ColorType, Bitmap.AlphaType);
        var resized = new SKBitmap(info);

        var filterMode = quality.ToLowerInvariant() switch
        {
            "nearest" => SKFilterMode.Nearest,
            _ => SKFilterMode.Linear
        };

        using (var canvas = new SKCanvas(resized))
        {
            var sampling = new SKSamplingOptions(filterMode);
            var src = SKRect.Create(0, 0, Bitmap.Width, Bitmap.Height);
            var dest = SKRect.Create(0, 0, tw, th);
            canvas.DrawBitmap(Bitmap, src, dest, sampling, null);
        }

        return new SkiaBitmapWrapper(resized);
    }

    public SkiaBitmapWrapper Rotate(float angleDeg)
    {
        var rad = angleDeg * MathF.PI / 180f;
        var sin = MathF.Abs(MathF.Sin(rad));
        var cos = MathF.Abs(MathF.Cos(rad));
        var newW = Math.Max(1, (int)MathF.Round(Bitmap.Width * cos + Bitmap.Height * sin));
        var newH = Math.Max(1, (int)MathF.Round(Bitmap.Width * sin + Bitmap.Height * cos));

        var info = new SKImageInfo(Math.Max(1, newW), Math.Max(1, newH), Bitmap.ColorType, Bitmap.AlphaType);
        var rotated = new SKBitmap(info);
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(newW / 2f, newH / 2f);
            canvas.RotateDegrees(angleDeg);
            canvas.Translate(-Bitmap.Width / 2f, -Bitmap.Height / 2f);
            canvas.DrawBitmap(Bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), null);
        }
        return new SkiaBitmapWrapper(rotated);
    }

    public SkiaBitmapWrapper Flip(string direction = "horizontal")
    {
        var info = new SKImageInfo(Bitmap.Width, Bitmap.Height, Bitmap.ColorType, Bitmap.AlphaType);
        var flipped = new SKBitmap(info);
        using (var canvas = new SKCanvas(flipped))
        {
            canvas.Clear(SKColors.Transparent);
            var sx = direction.Contains("horiz", StringComparison.OrdinalIgnoreCase) || direction.Equals("both", StringComparison.OrdinalIgnoreCase) ? -1f : 1f;
            var sy = direction.Contains("vert", StringComparison.OrdinalIgnoreCase) || direction.Equals("both", StringComparison.OrdinalIgnoreCase) ? -1f : 1f;

            canvas.Translate(sx < 0 ? Bitmap.Width : 0, sy < 0 ? Bitmap.Height : 0);
            canvas.Scale(sx, sy);
            canvas.DrawBitmap(Bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        }
        return new SkiaBitmapWrapper(flipped);
    }

    public string GetPixel(int x, int y)
    {
        if (x < 0 || x >= Bitmap.Width || y < 0 || y >= Bitmap.Height)
            return "#00000000";
        var c = Bitmap.GetPixel(x, y);
        return $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}{c.Alpha:X2}";
    }

    public void SetPixel(int x, int y, string color)
    {
        if (x < 0 || x >= Bitmap.Width || y < 0 || y >= Bitmap.Height)
            return;
        var c = SkiaColorParser.Parse(color);
        Bitmap.SetPixel(x, y, c);
    }

    public SkiaBitmapWrapper ApplyFilter(SKImageFilter filter)
    {
        var info = new SKImageInfo(Bitmap.Width, Bitmap.Height, Bitmap.ColorType, Bitmap.AlphaType);
        var filtered = new SKBitmap(info);
        using (var canvas = new SKCanvas(filtered))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { ImageFilter = filter };
            canvas.DrawBitmap(Bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);
        }
        return new SkiaBitmapWrapper(filtered);
    }

    public SkiaBitmapWrapper ApplyColorFilter(SKColorFilter colorFilter)
    {
        var info = new SKImageInfo(Bitmap.Width, Bitmap.Height, Bitmap.ColorType, Bitmap.AlphaType);
        var filtered = new SKBitmap(info);
        using (var canvas = new SKCanvas(filtered))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { ColorFilter = colorFilter };
            canvas.DrawBitmap(Bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);
        }
        return new SkiaBitmapWrapper(filtered);
    }

    /// <summary>PNG shorthand for <see cref="ToImageBytes"/>, the spelling the SDK reference documents.</summary>
    public byte[] ToPngBytes(int quality = 100) => ToImageBytes("png", quality);

    public byte[] ToImageBytes(string format = "webp", int quality = 85) =>
        SkiaImageEncoder.Encode(Bitmap, format, quality);

    public string ToDataUri(string format = "webp", int quality = 85)
    {
        var bytes = ToImageBytes(format, quality);
        return SkiaImageEncoder.ToDataUri(bytes, format);
    }

    public string ToDataURL(string format = "webp", int quality = 85) =>
        ToDataUri(format, quality);

    public SkiaBitmapWrapper Clone() =>
        new(Bitmap.Copy());

    /// <summary>
    /// Measures this bitmap against another, pixel by pixel.
    /// </summary>
    /// <remarks>
    /// The verification primitive. "Look again at what you rendered" means comparing it to something,
    /// and doing that in script means a loop over every pixel — which is both slow and the surest way
    /// to hit the sandbox's statement cap. This runs natively, so a full-frame comparison costs one
    /// call regardless of resolution.
    /// <para>
    /// <paramref name="options"/> takes <c>{ tolerance?: 8, ignoreAlpha?: false }</c>. Tolerance is
    /// per channel, because two renders of the same scene differ by a point or two along every
    /// antialiased edge and an exact comparison reports that as thousands of differing pixels.
    /// </para>
    /// <para>
    /// Differently sized bitmaps <b>throw</b> rather than comparing what overlaps. A size mismatch is
    /// a mistake about which images are being compared, and a similarity score computed over a
    /// partial overlap would look like an answer.
    /// </para>
    /// </remarks>
    public Dictionary<string, object> Diff(SkiaBitmapWrapper other, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireSameSize(other, "diff");

        var tolerance = OptionInt(options, "tolerance", 8);
        var ignoreAlpha = OptionBool(options, "ignoreAlpha", false);

        long differing = 0, deltaSum = 0;
        var maxDelta = 0;
        int minX = Bitmap.Width, minY = Bitmap.Height, maxX = -1, maxY = -1;

        for (var y = 0; y < Bitmap.Height; y++)
        {
            for (var x = 0; x < Bitmap.Width; x++)
            {
                var delta = ChannelDelta(Bitmap.GetPixel(x, y), other.Bitmap.GetPixel(x, y), ignoreAlpha);
                deltaSum += delta;
                if (delta > maxDelta) maxDelta = delta;
                if (delta <= tolerance) continue;

                differing++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        var total = (long)Bitmap.Width * Bitmap.Height;
        return new Dictionary<string, object>
        {
            ["width"] = Bitmap.Width,
            ["height"] = Bitmap.Height,
            ["totalPixels"] = total,
            ["differingPixels"] = differing,
            ["similarity"] = total == 0 ? 1d : 1d - (double)differing / total,
            ["meanDelta"] = total == 0 ? 0d : (double)deltaSum / total,
            ["maxDelta"] = maxDelta,
            ["identical"] = differing == 0,
            // Where the difference actually is, which is the part a score cannot tell you.
            ["bounds"] = maxX < 0 ? null! : Rect(minX, minY, maxX - minX + 1, maxY - minY + 1)
        };
    }

    /// <summary>A bitmap marking where this one differs from another.</summary>
    /// <remarks>
    /// A score says how much changed; this says <i>where</i>, which is what a person needs in order
    /// to look. Matching pixels are dimmed towards the background so the differences read at a
    /// glance rather than having to be hunted for.
    /// </remarks>
    public SkiaBitmapWrapper DiffMap(SkiaBitmapWrapper other, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireSameSize(other, "diffMap");

        var tolerance = OptionInt(options, "tolerance", 8);
        var ignoreAlpha = OptionBool(options, "ignoreAlpha", false);
        var mark = SkiaColorParser.Parse(OptionString(options, "color", "#ff0055"));

        var info = new SKImageInfo(Bitmap.Width, Bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var map = new SKBitmap(info);

        for (var y = 0; y < Bitmap.Height; y++)
        {
            for (var x = 0; x < Bitmap.Width; x++)
            {
                var mine = Bitmap.GetPixel(x, y);
                var delta = ChannelDelta(mine, other.Bitmap.GetPixel(x, y), ignoreAlpha);

                map.SetPixel(x, y, delta > tolerance
                    ? mark
                    : new SKColor(mine.Red, mine.Green, mine.Blue, (byte)(mine.Alpha / 6)));
            }
        }
        return new SkiaBitmapWrapper(map);
    }

    /// <summary>
    /// Where a colour class begins and ends on each row — the shape of a region, as numbers.
    /// </summary>
    /// <remarks>
    /// Reduces an image to a few hundred rows of measurement, so comparing two images becomes a loop
    /// over rows rather than over pixels. That is the difference between a comparison that fits the
    /// statement budget comfortably and one that does not: a per-row delta report over two 700-row
    /// images is ~1,400 iterations, where the pixel-level equivalent is nearly a million.
    /// <para>
    /// <paramref name="options"/> takes <c>{ tolerance?: 24, axis?: 'row'|'column', minCount?: 1 }</c>.
    /// Rows matching nothing are omitted, so an empty result means the colour is absent rather than
    /// that the image is.
    /// </para>
    /// </remarks>
    public Dictionary<string, object>[] RowProfile(string color, object? options = null)
    {
        var target = SkiaColorParser.Parse(color);
        var tolerance = OptionInt(options, "tolerance", 24);
        var minCount = OptionInt(options, "minCount", 1);
        var byColumn = OptionString(options, "axis", "row")
            .StartsWith("col", StringComparison.OrdinalIgnoreCase);

        var outer = byColumn ? Bitmap.Width : Bitmap.Height;
        var inner = byColumn ? Bitmap.Height : Bitmap.Width;
        var rows = new List<Dictionary<string, object>>();

        for (var i = 0; i < outer; i++)
        {
            int first = -1, last = -1, count = 0;
            for (var j = 0; j < inner; j++)
            {
                var pixel = byColumn ? Bitmap.GetPixel(i, j) : Bitmap.GetPixel(j, i);
                if (ChannelDelta(pixel, target, ignoreAlpha: false) > tolerance) continue;

                if (first < 0) first = j;
                last = j;
                count++;
            }

            if (count < minCount || first < 0) continue;
            rows.Add(new Dictionary<string, object>
            {
                ["index"] = i,
                ["start"] = first,
                ["end"] = last,
                ["extent"] = last - first + 1,
                ["count"] = count
            });
        }
        return [.. rows];
    }

    /// <summary>
    /// The dominant colours, with the share of the image each covers.
    /// </summary>
    /// <remarks>
    /// What a reference image is actually made of. Colours are bucketed before counting, so
    /// antialiasing and compression noise collapse into the flat colour they surround instead of
    /// producing thousands of near-duplicates. Fully transparent pixels are ignored.
    /// </remarks>
    public Dictionary<string, object>[] Palette(int count = 8, object? options = null)
    {
        var buckets = Math.Clamp(OptionInt(options, "buckets", 16), 2, 64);
        var size = 256 / buckets;
        var tally = new Dictionary<int, (long Count, long R, long G, long B)>();
        long counted = 0;

        for (var y = 0; y < Bitmap.Height; y++)
        {
            for (var x = 0; x < Bitmap.Width; x++)
            {
                var p = Bitmap.GetPixel(x, y);
                if (p.Alpha == 0) continue;

                var key = (p.Red / size * buckets + p.Green / size) * buckets + p.Blue / size;
                var current = tally.TryGetValue(key, out var t) ? t : default;
                tally[key] = (current.Count + 1, current.R + p.Red, current.G + p.Green, current.B + p.Blue);
                counted++;
            }
        }

        return [.. tally
            .OrderByDescending(e => e.Value.Count)
            .Take(Math.Max(1, count))
            // The reported colour is the bucket's mean, not its corner, so it is a colour that is
            // actually in the image rather than the quantisation grid showing through.
            .Select(e => new Dictionary<string, object>
            {
                ["color"] = $"#{(byte)(e.Value.R / e.Value.Count):X2}{(byte)(e.Value.G / e.Value.Count):X2}{(byte)(e.Value.B / e.Value.Count):X2}",
                ["share"] = counted == 0 ? 0d : (double)e.Value.Count / counted,
                ["pixels"] = e.Value.Count
            })];
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Methods (private)
    /// <summary>Largest per-channel difference between two pixels, 0–255.</summary>
    /// <remarks>
    /// Max-channel rather than a Euclidean distance, so the number means the same thing as the
    /// tolerance the caller passed: "no channel differs by more than this".
    /// </remarks>
    private static int ChannelDelta(SKColor a, SKColor b, bool ignoreAlpha)
    {
        var delta = Math.Abs(a.Red - b.Red);
        delta = Math.Max(delta, Math.Abs(a.Green - b.Green));
        delta = Math.Max(delta, Math.Abs(a.Blue - b.Blue));
        if (!ignoreAlpha) delta = Math.Max(delta, Math.Abs(a.Alpha - b.Alpha));
        return delta;
    }

    private void RequireSameSize(SkiaBitmapWrapper other, string call)
    {
        if (other.Width == Width && other.Height == Height) return;

        throw new ArgumentException(
            $"bitmap.{call}(...) needs two bitmaps of the same size: this one is {Width}x{Height}, " +
            $"the other is {other.Width}x{other.Height}. Resize one first with bitmap.resize(w, h).");
    }

    private static Dictionary<string, object> Rect(int x, int y, int width, int height) =>
        new()
        {
            ["x"] = x,
            ["y"] = y,
            ["width"] = width,
            ["height"] = height,
            ["x2"] = x + width,
            ["y2"] = y + height,
            ["cx"] = x + width / 2.0,
            ["cy"] = y + height / 2.0
        };

    private static IDictionary? AsOptions(object? options) => JsInterop.AsDict(options);

    private static int OptionInt(object? options, string name, int fallback)
    {
        var dict = AsOptions(options);
        return dict is not null && dict.Contains(name) && dict[name] is not null
            ? Convert.ToInt32(dict[name], CultureInfo.InvariantCulture)
            : fallback;
    }

    private static bool OptionBool(object? options, string name, bool fallback)
    {
        var dict = AsOptions(options);
        return dict is not null && dict.Contains(name) && dict[name] is not null
            ? Convert.ToBoolean(dict[name], CultureInfo.InvariantCulture)
            : fallback;
    }

    private static string OptionString(object? options, string name, string fallback)
    {
        var dict = AsOptions(options);
        return dict is not null && dict.Contains(name) && dict[name] is not null
            ? dict[name]!.ToString() ?? fallback
            : fallback;
    }
    #endregion
}

