namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

/// <summary>Raster canvas surface — the root object a raster script draws onto.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaCanvas : IDisposable
{
    #region Constructors
    public SkiaCanvas(int width = 800, int height = 600)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);

        var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        Bitmap = new SKBitmap(info);
        SkCanvas = new SKCanvas(Bitmap);
        SkCanvas.Clear(SKColors.Transparent);
    }
    #endregion

    #region Properties

    public int Width { get; }
    public int Height { get; }
    public SKBitmap Bitmap { get; }
    public SKCanvas SkCanvas { get; }
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
        new(Bitmap.Copy());

    public ImageData ToImageData() =>
        GetContext("2d").GetImageData(0, 0, Width, Height);

    public byte[] ToImageBytes(string format = "webp", int quality = 85) =>
        SkiaImageEncoder.Encode(Bitmap, format, quality);

    public string ToDataUri(string format = "webp", int quality = 85)
    {
        var bytes = ToImageBytes(format, quality);
        return SkiaImageEncoder.ToDataUri(bytes, format);
    }

    public string ToDataURL(string format = "webp", int quality = 85) =>
        ToDataUri(format, quality);

    public void Dispose()
    {
        SkCanvas.Dispose();
        Bitmap.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Fields
    private CanvasRenderingContext2D? _context;
    #endregion
}
