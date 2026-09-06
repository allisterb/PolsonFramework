namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

/// <summary>Raster canvas surface — the root object a raster script draws onto.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaCanvas : IDisposable, IDataUriSource
{
    #region Constructors
    public SkiaCanvas(int width = 800, int height = 600)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);

        var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        SkBitmap = new SKBitmap(info);
        SkCanvas = new SKCanvas(SkBitmap);
        SkCanvas.Clear(SKColors.Transparent);
    }
    #endregion

    #region Properties

    public int Width { get; }
    public int Height { get; }
    public SKBitmap SkBitmap { get; }
    public SKCanvas SkCanvas { get; }

    /// <summary>
    /// The canvas's live backing bitmap, unlike <see cref="ToBitmap"/> which copies.
    /// </summary>
    /// <remarks>
    /// Wraps <see cref="SkBitmap"/> rather than returning it. Handing a script the raw
    /// <see cref="SKBitmap"/> put a SkiaSharp type on the JS surface, so <c>canvas.bitmap.getPixel()</c>
    /// reached SkiaSharp's own method and returned an <c>SKColor</c> struct — an object rather than a
    /// string, printing as <c>#AARRGGBB</c> instead of the documented <c>#RRGGBBAA</c> and never equal
    /// to anything under <c>===</c>. The wrapper is cached so the identity stays stable across reads.
    /// </remarks>
    public SkiaBitmapWrapper Bitmap => _liveBitmap ??= new SkiaBitmapWrapper(SkBitmap);
    #endregion

    #region Methods
    public CanvasRenderingContext2D GetContext(string contextType = "2d")
    {
        if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Context type '{contextType}' is not supported. Only '2d' is supported.");

        _context ??= new CanvasRenderingContext2D(this);
        return _context;
    }

    public void Clear(string? color = null)
    {
        var skColor = string.IsNullOrEmpty(color) ? SKColors.Transparent : SkiaColorParser.Parse(color);
        SkCanvas.Clear(skColor);
    }

    public SkiaBitmapWrapper ToBitmap() =>
        new(SkBitmap.Copy());

    public ImageData ToImageData() =>
        GetContext("2d").GetImageData(0, 0, Width, Height);

    public byte[] ToImageBytes(string format = "webp", int quality = 85) =>
        SkiaImageEncoder.Encode(SkBitmap, format, quality);

    public string ToDataUri(string format = "webp", int quality = 85)
    {
        var bytes = ToImageBytes(format, quality);
        return SkiaImageEncoder.ToDataUri(bytes, format);
    }

    /// <summary>Explicit: the optional-parameter overload above does not satisfy the interface, and
    /// explicit keeps it off the reflected public surface. See <see cref="IDataUriSource"/>.</summary>
    string IDataUriSource.ToDataUri() => ToDataUri();

    public string ToDataURL(string format = "webp", int quality = 85) =>
        ToDataUri(format, quality);

    public void Dispose()
    {
        SkCanvas.Dispose();
        SkBitmap.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Fields
    private CanvasRenderingContext2D? _context;
    private SkiaBitmapWrapper? _liveBitmap;
    #endregion
}
