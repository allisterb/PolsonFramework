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
    public byte[] ToImageBytes(string format = "webp", int quality = 85)
    {
        using var tempBmp = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var pixels = tempBmp.GetPixelSpan();
        Data.CopyTo(pixels);
        return SkiaImageEncoder.Encode(tempBmp, format, quality);
    }

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
    #endregion
}
