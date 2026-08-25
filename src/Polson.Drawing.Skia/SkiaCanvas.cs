namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

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
    public int width => Width;
    public int height => Height;

    public int Width { get; }
    public int Height { get; }
    public SKBitmap Bitmap { get; }
    public SKCanvas SkCanvas { get; }
    #endregion

    #region Methods
    public CanvasRenderingContext2D getContext(string contextType = "2d")
    {
        if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Context type '{contextType}' is not supported. Only '2d' is supported.");

        _context ??= new CanvasRenderingContext2D(this);
        return _context;
    }

    public void clear(string? color = null)
    {
        var skColor = string.IsNullOrEmpty(color) ? SKColors.Transparent : SkiaColorParser.Parse(color);
        SkCanvas.Clear(skColor);
    }

    public SkiaBitmapWrapper toBitmap() =>
        new(Bitmap.Copy());

    public ImageData toImageData() =>
        getContext("2d").getImageData(0, 0, Width, Height);

    public byte[] ToImageBytes(string format = "webp", int quality = 85) =>
        SkiaImageEncoder.Encode(Bitmap, format, quality);

    public byte[] toImageBytes(string format = "webp", int quality = 85) =>
        ToImageBytes(format, quality);

    public string ToDataUri(string format = "webp", int quality = 85)
    {
        var bytes = ToImageBytes(format, quality);
        return SkiaImageEncoder.ToDataUri(bytes, format);
    }

    public string toDataUri(string format = "webp", int quality = 85) =>
        ToDataUri(format, quality);

    public string toDataURL(string format = "webp", int quality = 85) =>
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
