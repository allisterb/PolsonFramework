namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

/// <summary>Raw RGBA pixel buffer, mirroring the Canvas <c>ImageData</c> interface.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class ImageData : IDataUriSource
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

    public int Width { get; }
    public int Height { get; }
    public byte[] Data { get; }
    #endregion

    #region Methods
    /// <summary>PNG shorthand for <see cref="ToImageBytes"/>, the spelling the SDK reference documents.</summary>
    public byte[] ToPngBytes(int quality = 100) => ToImageBytes("png", quality);

    public byte[] ToImageBytes(string format = "webp", int quality = 85)
    {
        using var tempBmp = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var pixels = tempBmp.GetPixelSpan();
        Data.CopyTo(pixels);
        return SkiaImageEncoder.Encode(tempBmp, format, quality);
    }

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
    #endregion
}
