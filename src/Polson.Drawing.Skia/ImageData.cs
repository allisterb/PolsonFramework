namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

public class ImageData
{
    #region Constructors
    public ImageData(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Data = new byte[Width * Height * 4];
    }

    public ImageData(byte[] data, int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        ArgumentNullException.ThrowIfNull(data);

        var expectedLen = Width * Height * 4;
        if (data.Length != expectedLen)
        {
            Data = new byte[expectedLen];
            Array.Copy(data, Data, Math.Min(data.Length, expectedLen));
        }
        else
        {
            Data = data;
        }
    }
    #endregion

    #region Properties
    public int width => Width;
    public int height => Height;
    public byte[] data => Data;

    public int Width { get; }
    public int Height { get; }
    public byte[] Data { get; }
    #endregion

    #region Methods
    public byte[] ToPngBytes(int quality = 100)
    {
        using var tempBmp = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var pixels = tempBmp.GetPixelSpan();
        Data.CopyTo(pixels);
        using var skImg = SKImage.FromBitmap(tempBmp);
        using var pngData = skImg.Encode(SKEncodedImageFormat.Png, quality);
        return pngData.ToArray();
    }

    public string ToDataUrl() =>
        "data:image/png;base64," + Convert.ToBase64String(ToPngBytes());
    #endregion
}

