namespace Polson.Drawing.Svg;

using System;
using System.IO;
using System.Xml;
using SkiaSharp;

public static class SvgRenderPipeline
{
    #region Methods
    /// <summary>
    /// Seeks a loaded document to <paramref name="atTime"/>, so an SVG carrying SMIL animation
    /// renders the frame at that time rather than its opening frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this every animated document renders frozen at <c>t = 0</c>, silently — the picture
    /// is produced, nothing errors, and the animation simply never starts. <c>SetAnimationTime</c>
    /// is an absolute seek and is deterministic: seeking to a time directly and seeking to it after
    /// visiting a later time give identical output, so frames may be rendered in any order.
    /// </para>
    /// <para>
    /// The minimum render interval is cleared first. It exists to throttle an interactive host that
    /// would otherwise rebuild faster than it can display, and a headless renderer wants every frame
    /// it asks for — left at its default, a frame requested too soon after the previous one is
    /// deferred and the caller silently gets the older picture back.
    /// </para>
    /// </remarks>
    private static void SeekTo(SKSvg skSvg, TimeSpan? atTime)
    {
        if (atTime is not { } time || !skSvg.HasAnimations) return;

        skSvg.AnimationMinimumRenderInterval = TimeSpan.Zero;
        skSvg.SetAnimationTime(time);
        if (skSvg.HasPendingAnimationFrame) skSvg.FlushPendingAnimationFrame();
    }

    public static byte[] RenderToImage(SvgDocument document, int? width = null, int? height = null, string format = "webp", int quality = 85, SKColor? background = null, TimeSpan? atTime = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var skSvg = new SKSvg();
        skSvg.FromSvgDocument(document);
        SeekTo(skSvg, atTime);
        return Encode(skSvg, width, height, format, quality, background);
    }

    public static byte[] RenderToImage(string svgXml, int? width = null, int? height = null, string format = "webp", int quality = 85, SKColor? background = null, TimeSpan? atTime = null)
    {
        ArgumentNullException.ThrowIfNull(svgXml);

        if (svgXml.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            var idx = svgXml.IndexOf("?>", StringComparison.Ordinal);
            if (idx >= 0)
            {
                svgXml = svgXml[(idx + 2)..].TrimStart();
            }
        }

        using var skSvg = new SKSvg();
        skSvg.FromSvg(svgXml);
        SeekTo(skSvg, atTime);
        return Encode(skSvg, width, height, format, quality, background);
    }

    private static byte[] Encode(SKSvg skSvg, int? width, int? height, string format, int quality, SKColor? background)
    {
        if (skSvg.Picture == null)
            return Array.Empty<byte>();

        var pictureBounds = skSvg.Picture.CullRect;
        var targetWidth = width ?? (int)MathF.Ceiling(pictureBounds.Width > 0 ? pictureBounds.Width : 800f);
        var targetHeight = height ?? (int)MathF.Ceiling(pictureBounds.Height > 0 ? pictureBounds.Height : 600f);

        if (targetWidth <= 0) targetWidth = 800;
        if (targetHeight <= 0) targetHeight = 600;

        using var bitmap = new SKBitmap(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            if (background.HasValue)
            {
                canvas.Clear(background.Value);
            }
            else
            {
                canvas.Clear(SKColors.Transparent);
            }

            var scaleX = pictureBounds.Width > 0 ? targetWidth / pictureBounds.Width : 1f;
            var scaleY = pictureBounds.Height > 0 ? targetHeight / pictureBounds.Height : 1f;
            var matrix = SKMatrix.CreateScale(scaleX, scaleY);

            canvas.DrawPicture(skSvg.Picture, in matrix);
        }

        var encFormat = (format?.Trim().ToLowerInvariant()) switch
        {
            "png" => SKEncodedImageFormat.Png,
            "jpeg" or "jpg" => SKEncodedImageFormat.Jpeg,
            _ => SKEncodedImageFormat.Webp,
        };

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(encFormat, Math.Clamp(quality, 1, 100));
        return data?.ToArray() ?? Array.Empty<byte>();
    }

    public static SKBitmap RenderToBitmap(SvgDocument document, int? width = null, int? height = null, SKColor? background = null, TimeSpan? atTime = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var skSvg = new SKSvg();
        skSvg.FromSvgDocument(document);
        SeekTo(skSvg, atTime);

        var pictureBounds = skSvg.Picture?.CullRect ?? SKRect.Create(0, 0, 800, 600);
        var targetWidth = width ?? (int)MathF.Ceiling(pictureBounds.Width > 0 ? pictureBounds.Width : 800f);
        var targetHeight = height ?? (int)MathF.Ceiling(pictureBounds.Height > 0 ? pictureBounds.Height : 600f);

        var bitmap = new SKBitmap(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            if (background.HasValue)
            {
                canvas.Clear(background.Value);
            }
            else
            {
                canvas.Clear(SKColors.Transparent);
            }

            if (skSvg.Picture != null)
            {
                var scaleX = pictureBounds.Width > 0 ? targetWidth / pictureBounds.Width : 1f;
                var scaleY = pictureBounds.Height > 0 ? targetHeight / pictureBounds.Height : 1f;
                var matrix = SKMatrix.CreateScale(scaleX, scaleY);
                canvas.DrawPicture(skSvg.Picture, in matrix);
            }
        }

        return bitmap;
    }

    public static void SaveImage(SvgDocument document, string filePath, int? width = null, int? height = null, string format = "webp", int quality = 85, SKColor? background = null, TimeSpan? atTime = null)
    {
        var imgBytes = RenderToImage(document, width, height, format, quality, background, atTime);
        File.WriteAllBytes(filePath, imgBytes);
    }

    /// <summary>
    /// Whether this markup carries SMIL animation — so a caller can tell an animated document from a
    /// still one before deciding to ask for a time.
    /// </summary>
    /// <remarks>
    /// Worth having because the failure it prevents is silent: rendering an animated document with
    /// no <c>atTime</c> succeeds and returns the opening frame, which looks like a still drawing
    /// rather than like a missing argument.
    /// </remarks>
    public static bool HasAnimations(string svgXml)
    {
        ArgumentNullException.ThrowIfNull(svgXml);

        using var skSvg = new SKSvg();
        skSvg.FromSvg(svgXml);
        return skSvg.HasAnimations;
    }

    /// <summary>
    /// Serializes a document to markup. No longer used to render one — see the remarks.
    /// </summary>
    /// <remarks>
    /// The document overloads used to serialize and re-parse. They now hand the document to
    /// <c>SKSvg.FromSvgDocument</c> directly, for two reasons.
    /// <para>
    /// <b>The round trip is lossy for animation.</b> <c>document.Write</c> emits SMIL's timing
    /// attribute <c>fill="freeze"</c> as <c>style="fill:freeze;"</c> — conflating it with the paint
    /// property of the same name. The freeze is therefore lost on re-parse, and an animation snaps
    /// back to its start once it ends instead of holding. Measured on a 4s translate: the serialize
    /// route renders x = 20 at t = 4s where the document route renders 420.
    /// </para>
    /// <para>
    /// <b>And it was never free.</b> A serialize plus a full re-parse ran on every render of a
    /// document that was already in memory.
    /// </para>
    /// Static output is unaffected: the two routes were checked byte-for-byte on a document with
    /// shapes, opacity, stroke and shaped text, and produce identical PNGs.
    /// </remarks>
    public static string SerializeDocument(SvgDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true });
        document.Write(xw);
        xw.Flush();
        return sw.ToString();
    }
    #endregion
}
