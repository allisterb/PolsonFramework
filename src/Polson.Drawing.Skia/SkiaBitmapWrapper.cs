namespace Polson.Drawing.Skia;

using System;
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

    public void Dispose()
    {
        Bitmap.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion
}

