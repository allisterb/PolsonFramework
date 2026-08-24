namespace Polson.Drawing.Svg;

using System;
using System.IO;
using System.Xml;
using SkiaSharp;

public static class SvgRenderPipeline
{
    #region Methods
    public static byte[] RenderToPng(SvgDocument document, int? width = null, int? height = null, SKColor? background = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var xml = SerializeDocument(document);
        return RenderToPng(xml, width, height, background);
    }

    public static byte[] RenderToPng(string svgXml, int? width = null, int? height = null, SKColor? background = null)
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

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static SKBitmap RenderToBitmap(SvgDocument document, int? width = null, int? height = null, SKColor? background = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var xml = SerializeDocument(document);
        using var skSvg = new SKSvg();
        skSvg.FromSvg(xml);

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

    public static void SavePng(SvgDocument document, string filePath, int? width = null, int? height = null, SKColor? background = null)
    {
        var pngBytes = RenderToPng(document, width, height, background);
        File.WriteAllBytes(filePath, pngBytes);
    }

    private static string SerializeDocument(SvgDocument document)
    {
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true });
        document.Write(xw);
        xw.Flush();
        return sw.ToString();
    }
    #endregion
}

