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

    public byte[] ToPngBytes(int quality = 100)
    {
        using var image = SKImage.FromBitmap(Bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality);
        return data.ToArray();
    }

    public string ToDataUrl()
    {
        var bytes = ToPngBytes();
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

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

