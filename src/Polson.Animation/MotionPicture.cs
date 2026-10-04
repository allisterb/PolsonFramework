namespace Polson.Animation;

using System;

using Polson.Drawing.Skia;

using SkiaSharp;

/// <summary>
/// A picture an image layer holds: a copy taken when the layer is made, so later drawing on the source
/// does not reach the animation.
/// </summary>
internal sealed class MotionPicture
{
    #region Constructors
    private MotionPicture(SKImage image) => Image = image;
    #endregion

    #region Fields
    private byte[]? png;
    #endregion

    #region Properties
    public SKImage Image { get; }

    public int Width => Image.Width;

    public int Height => Image.Height;

    /// <summary>The picture as PNG, lossless, for the <c>.sif</c> writer's sidecar file.</summary>
    public byte[] Png => png ??= Image.Encode(SKEncodedImageFormat.Png, 100).ToArray();

    /// <summary>The picture inlined, because an external href does not resolve in an SVG loaded as an image.</summary>
    public string DataUri => "data:image/png;base64," + Convert.ToBase64String(Png);
    #endregion

    #region Methods
    /// <summary>
    /// Copies the pixels out of a bitmap, a canvas, or anything carrying its own (a cutout cell, a
    /// photograph, a material), and refuses anything else by name — including a filename, because a
    /// script names no files here.
    /// </summary>
    public static MotionPicture From(object? value, string who)
    {
        var image = value switch
        {
            SkiaBitmapWrapper b => Copy(b.Bitmap),
            SkiaCanvas c => Copy(c.SkBitmap),
            SKBitmap b => Copy(b),
            ILosslessImageSource l => Decode(l.ToLosslessBytes()),
            IDataUriSource d => Decode(FromDataUri(d.ToDataUri(), who)),
            string => throw new ArgumentException(
                $"{who} takes a picture, not a filename: pass a bitmap, a canvas, or a cell (Skia.Image.load reads a file into a bitmap)."),
            null => throw new ArgumentException($"{who} needs an image: a bitmap, a canvas, or a cutout cell."),
            _ => throw new ArgumentException($"{who} cannot use a {value.GetType().Name} as its image. It takes a bitmap, a canvas, or a cutout cell.")
        };

        if (image is null) throw new ArgumentException($"{who}'s image could not be decoded.");
        if (image.Width == 0 || image.Height == 0) throw new ArgumentException($"{who}'s image is empty.");
        return new MotionPicture(image);
    }

    private static SKImage Copy(SKBitmap bitmap)
    {
        using var pixmap = bitmap.PeekPixels();
        return SKImage.FromPixelCopy(pixmap);
    }

    private static SKImage? Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var encoded = SKImage.FromEncodedData(data);
        return encoded?.ToRasterImage(true);
    }

    private static byte[] FromDataUri(string uri, string who)
    {
        var comma = uri.IndexOf(',');
        if (comma < 0 || !uri.StartsWith("data:", StringComparison.Ordinal))
            throw new ArgumentException($"{who}'s image carries no pixels.");
        return Convert.FromBase64String(uri[(comma + 1)..]);
    }
    #endregion
}
