namespace Polson.Drawing.Skia;

using System;
using SkiaSharp;

/// <summary>
/// Utility for encoding Skia bitmaps to WebP, PNG, or JPEG formats.
/// </summary>
public static class SkiaImageEncoder
{
    #region Methods
    public static SKEncodedImageFormat ParseFormat(string? format) =>
        (format?.Trim().ToLowerInvariant()) switch
        {
            "png" => SKEncodedImageFormat.Png,
            "jpeg" or "jpg" => SKEncodedImageFormat.Jpeg,
            _ => SKEncodedImageFormat.Webp,
        };

    public static string NormalizeFormatName(string? format) =>
        (format?.Trim().ToLowerInvariant()) switch
        {
            "png" => "png",
            "jpeg" or "jpg" => "jpeg",
            _ => "webp",
        };

    public static string GetMimeType(string? format) =>
        (format?.Trim().ToLowerInvariant()) switch
        {
            "png" => "image/png",
            "jpeg" or "jpg" => "image/jpeg",
            _ => "image/webp",
        };

    public static byte[] Encode(SKBitmap bitmap, string format = "webp", int quality = 85)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        using var image = SKImage.FromBitmap(bitmap);
        var encFormat = ParseFormat(format);
        var clampedQuality = Math.Clamp(quality, 1, 100);

        using var data = image.Encode(encFormat, clampedQuality);
        return data?.ToArray() ?? Array.Empty<byte>();
    }

    public static string ToDataUri(byte[] bytes, string format = "webp")
    {
        var mime = GetMimeType(format);
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }
    #endregion
}
