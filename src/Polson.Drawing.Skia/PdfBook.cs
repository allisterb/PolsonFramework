namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkiaSharp;

/// <summary>
/// A short presentation as a multi-page PDF: a header on every page, then a title, an image, its caption and text, in
/// that order — the process book or the pitch book a studio hands its client with the finished work (<c>Stage.book</c>).
/// </summary>
/// <remarks>
/// <para>
/// Lupton's concept presentations are sequential, simple and concise, with a header on each page naming the project
/// (<i>Graphic Design Thinking</i>, pp. 108–109). Pages are 16:9 at 960 × 540 points, the shape of a projected slide.
/// </para>
/// <para>
/// Shaw's conventions for the book itself (<i>Design for Motion</i>, ch. 4): one typeface, varied in weight and size; a
/// neutral ground with colour kept for accents; room on every edge; a simple cover naming the project, the date, the
/// studio and, for a pitch, the client; contents when it runs long; numbered pages; and a closing page. The book carries
/// the piece's own typeface and accent (<see cref="BookStyle"/>) — Hartstone's "the tone of the idea on every page" —
/// without competing with the work it holds.
/// </para>
/// <para>
/// Text is a light Markdown: <c>#</c> headings, <c>-</c> bullets, <c>&gt;</c> quotes indented, table rows as their cells
/// with the header bold, paragraphs split on blank lines, and <c>**</c>, <c>`</c> and link syntax dropped. Every line is
/// shaped with a face per character (<see cref="TextShaper"/>), so Japanese in a Latin face's book is not set as boxes; the
/// characters taken from another face are drawn as outlines, as Skia embeds a face whole. A page whose text runs past its bottom continues on the next under the same title. Images
/// arrive as bytes, decoded here, never from a path: a Skia-opened path is a handle a child process inherits.
/// </para>
/// </remarks>
public static class PdfBook
{
    #region Types
    /// <summary>One page: any of a title, an image (encoded bytes), a caption and text.</summary>
    public sealed record Page(string? Title, byte[]? Image, string? Caption, string? Text);

    /// <summary>The cover's lines: who the book is for, its date and the studio, any of them absent.</summary>
    public sealed record Cover(string? Client, string? Date, string? Studio, string? Subtitle);

    /// <summary>
    /// How a book looks and what frames it: its typeface (a family name), its accent colour, a cover, contents (true,
    /// false, or null for "when the book runs past eight pages"), and a closing page's words.
    /// </summary>
    public sealed record BookStyle(string? Font = null, string? Accent = null, Cover? Cover = null, bool? Contents = null, string? Close = null);
    #endregion

    #region Constants
    public const float Width = 960, Height = 540;

    /// <summary>Room on every edge: Shaw asks for a healthy amount, so the eye has somewhere to rest.</summary>
    private const float Margin = 56, HeaderY = 34, TitleY = 86;

    /// <summary>Past this many pages a book opens with its contents.</summary>
    private const int ContentsFrom = 8;

    /// <summary>The rule under a Markdown table's header: <c>| :--- | ---: |</c>.</summary>
    private const string TableRule = @"^\s*\|?\s*:?-{3,}";
    #endregion

    #region Methods
    /// <summary>
    /// The book as PDF bytes, how many pages it came to, and for each of <paramref name="pages"/> how many continuation pages
    /// its text overflowed onto. A page a top-level heading opens is not a continuation: a concept to a page is the format.
    /// With <paramref name="previewScale"/> above 0, every page is also drawn as a PNG at that multiple of its size, so the
    /// book can be proofed where a PDF cannot be opened.
    /// </summary>
    public static (byte[] Pdf, int Pages, int[] Overflows, byte[][] Previews) Write(string title, IReadOnlyList<Page> pages,
        BookStyle? style = null, float previewScale = 0)
    {
        style ??= new BookStyle();
        using var look = new Look(style);
        var parts = pages.Select(p => Paginate(p, look).ToList()).ToArray();
        var layouts = parts.SelectMany(p => p).ToList();
        var cover = style.Cover is not null;
        var contents = style.Contents ?? layouts.Count > ContentsFrom;
        var close = !string.IsNullOrWhiteSpace(style.Close);
        var first = (cover ? 1 : 0) + (contents ? 1 : 0) + 1;
        var total = layouts.Count + first - 1 + (close ? 1 : 0);

        using var stream = new MemoryStream();
        // From Default, never `new`: a struct initialiser leaves EncodingQuality at 0, and Skia then writes every picture as
        // a quality-0 JPEG — nightshift2's board and research sheet came out blocky and unreadable. Default's 101 is lossless.
        var metadata = SKDocumentPdfMetadata.Default;
        metadata.Title = title;
        metadata.Creator = style.Cover?.Studio ?? "Polson Studio";
        // Each page as a drawing, so the PDF and the previews are the same pages.
        var drawn = new List<Action<SKCanvas>>();
        if (cover) drawn.Add(c => RenderCover(c, title, style.Cover!, look));
        if (contents)
        {
            // Each page that opens a section, by its title, at the number it will print with.
            var entries = layouts.Select((l, i) => (l.Page.Title, Number: first + i, l.Continued)).Where(e => !e.Continued && e.Title is not null);
            drawn.Add(c => RenderContents(c, title, [.. entries.Select(e => (e.Title!, e.Number))], cover ? 2 : 1, total, look));
        }
        var number = first - 1;
        for (var i = 0; i < layouts.Count; i++)
        {
            var (page, lines, continued, overflow) = layouts[i];
            var n = ++number;
            // Only a page's own continuation is an overflow, so the next part being one means this part's text goes on.
            var continues = i + 1 < layouts.Count && layouts[i + 1].Overflow;
            drawn.Add(c => Render(c, title, page, lines, continued, overflow, continues, n, total, look));
        }
        if (close) drawn.Add(c => RenderClose(c, title, style.Close!, style.Cover?.Studio, total, look));

        using (var document = SKDocument.CreatePdf(stream, metadata))
        {
            foreach (var draw in drawn)
            {
                using var canvas = document.BeginPage(Width, Height);
                draw(canvas);
                document.EndPage();
            }
            document.Close();
        }
        byte[][] previews = previewScale <= 0 ? [] : [.. drawn.Select(draw =>
        {
            using var surface = SKSurface.Create(new SKImageInfo((int)Math.Round(Width * previewScale), (int)Math.Round(Height * previewScale)));
            surface.Canvas.Clear(SKColors.White);
            surface.Canvas.Scale(previewScale);
            draw(surface.Canvas);
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        })];
        return (stream.ToArray(), total, [.. parts.Select(p => p.Count(part => part.Overflow))], previews);
    }

    /// <summary>Whether a typeface family is installed: a family that is not would be replaced without a word.</summary>
    public static bool Installed(string family)
    {
        using var face = SKTypeface.FromFamilyName(family);
        return face is not null && string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Methods (private)
    /// <summary>A line of laid-out text: what it says, its size, whether bold, its indent, and the space before it.</summary>
    internal sealed record Line(string Text, float Size, bool Strong, float Indent, float Before, bool Opens = false);

    /// <summary>One book's typefaces and colours.</summary>
    internal sealed class Look : IDisposable
    {
        public Look(BookStyle style)
        {
            var family = string.IsNullOrWhiteSpace(style.Font) ? "Arial" : style.Font;
            Regular = SKTypeface.FromFamilyName(family) ?? SKTypeface.Default;
            Bold = SKTypeface.FromFamilyName(family, SKFontStyle.Bold) ?? SKTypeface.Default;
            Accent = style.Accent is { Length: > 0 } a && SKColor.TryParse(a, out var c) ? c : Ink;
        }

        public SKTypeface Regular { get; }
        public SKTypeface Bold { get; }
        public SKColor Accent { get; }

        public static readonly SKColor Ground = new(0xfa, 0xf8, 0xf4), Ink = new(0x1d, 0x1f, 0x24), Muted = new(0x7a, 0x7f, 0x88), Rule = new(0xd8, 0xd3, 0xc9);

        public void Dispose()
        {
            Regular.Dispose();
            Bold.Dispose();
        }
    }

    /// <summary>
    /// The page, split where its text overflows and before each top-level heading after the first, so a concepts file
    /// gives each concept its page, as a concept presentation does; the first part keeps the image.
    /// </summary>
    private static IEnumerable<(Page Page, List<Line> Lines, bool Continued, bool Overflow)> Paginate(Page page, Look look)
    {
        var textLeft = page.Image is null ? Margin : Width * 0.58f;
        var width = Width - Margin - textLeft;
        var lines = Lay(page.Text ?? "", width, look);
        var top = page.Title is null ? TitleY - 24 : TitleY + 18;
        var room = Height - Margin - top;
        var part = new List<Line>();
        float used = 0;
        var continued = false;
        var overflow = false;
        foreach (var line in lines)
        {
            var need = line.Before + line.Size * 1.35f;
            var opens = line.Opens && part.Any(l => l.Text.Length > 0);
            if ((used + need > room || opens) && part.Count > 0)
            {
                yield return (continued ? page with { Image = null, Caption = null } : page, part, continued, overflow);
                (part, used, continued, overflow) = ([], 0, true, !opens);
                need = line.Size * 1.35f;
                part.Add(line with { Before = 0 });
                used += need;
                continue;
            }
            part.Add(line);
            used += need;
        }
        yield return (continued ? page with { Image = null, Caption = null } : page, part, continued, overflow);
    }

    /// <summary>Light Markdown, wrapped to <paramref name="width"/> points.</summary>
    internal static List<Line> Lay(string text, float width, Look look)
    {
        var lines = new List<Line>();
        foreach (var block in Regex.Split(text.Replace("\r\n", "\n").Trim(), @"\n\s*\n"))
        {
            // A line that starts nothing of its own continues the one before, as Markdown reads it: a file wrapped at 120
            // columns was set with a break at every source line, so a treatment broke mid-sentence ("arrives / as / if").
            // A blockquote's `>` marks every line it spans and is not text: joined with the rest, becool2's markers were set
            // mid-sentence (2026-10-25). A quote is indented instead, and a quoted line joins only another quoted line.
            var joined = new List<(string Text, bool Quote)>();
            foreach (var raw in block.Split('\n'))
            {
                var quote = Regex.Match(raw, @"^\s*>\s?(.*)$");
                var (content, quoted) = quote.Success ? (quote.Groups[1].Value, true) : (raw, false);
                var starts = joined.Count == 0 || joined[^1].Quote != quoted || Regex.IsMatch(content, @"^\s*(#{1,6}\s|[-*]\s|\|)")
                    || Regex.IsMatch(joined[^1].Text, @"^\s*(#{1,6}\s|\|)");
                if (starts) joined.Add((content, quoted));
                else joined[^1] = (joined[^1].Text.TrimEnd() + " " + content.Trim(), quoted);
            }
            for (var j = 0; j < joined.Count; j++)
            {
                var (raw, quoted) = joined[j];
                var line = Plain(raw.TrimEnd());
                if (line.Trim().Length == 0 || Regex.IsMatch(line, TableRule)) continue;
                var heading = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                var bullet = Regex.Match(line, @"^(\s*)[-*]\s+(.*)$");
                var indent = quoted ? 18f : 0f;
                if (heading.Success)
                {
                    var size = heading.Groups[1].Length <= 2 ? 17f : 14f;
                    var opens = heading.Groups[1].Length == 1;
                    lines.AddRange(Wrap(heading.Groups[2].Value, size, true, 0, width, look).Select((l, i) => i == 0 ? l with { Before = 10, Opens = opens } : l));
                }
                else if (bullet.Success)
                    lines.AddRange(Wrap("•  " + bullet.Groups[2].Value, 12.5f, false, indent + 14, width, look).Select((l, i) => i == 0 ? l with { Before = 3 } : l with { Indent = indent + 26 }));
                else if (line.TrimStart().StartsWith('|'))
                {
                    // A table row as its cells in order, an empty cell left out; the row above the rule is the header, in bold.
                    var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).Where(c => c.Length > 0);
                    var header = j + 1 < joined.Count && Regex.IsMatch(joined[j + 1].Text, TableRule);
                    lines.AddRange(Wrap(string.Join(" · ", cells), 12.5f, header, indent, width, look).Select((l, i) => i == 0 ? l with { Before = 3 } : l));
                }
                else
                    lines.AddRange(Wrap(line.Trim(), 12.5f, false, indent, width, look));
            }
            lines.Add(new Line("", 6, false, 0, 0));
        }
        return lines;
    }

    private static string Plain(string s) =>
        Regex.Replace(Regex.Replace(s.Replace("**", "").Replace("`", ""), @"\[([^\]]*)\]\([^)]*\)", "$1"), @"(?<!\w)\*(\S[^*]*)\*(?!\w)", "$1");

    private static IEnumerable<Line> Wrap(string text, float size, bool strong, float indent, float width, Look look)
    {
        using var font = new SKFont(strong ? look.Bold : look.Regular, size);
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var trial = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && TextShaper.Measure(trial, font) > width - indent)
            {
                yield return new Line(line, size, strong, indent, 0);
                line = word;
            }
            else line = trial;
        }
        if (line.Length > 0) yield return new Line(line, size, strong, indent, 0);
    }

    /// <summary>
    /// A line of the book's words: shaped, and any run in a face other than the book's drawn as outlines, so the PDF
    /// embeds the book's own face and not a whole CJK collection besides (<see cref="TextShaper.Draw"/>).
    /// </summary>
    private static void Text(SKCanvas canvas, string text, float x, float y, SKTextAlign align, SKFont font, SKPaint paint) =>
        TextShaper.DrawText(canvas, text, x, y, align, font, paint, outlineFallback: true);

    /// <summary>The header Lupton asks for on every page: the project, and where this page sits; the rule in the accent.</summary>
    private static void Header(SKCanvas canvas, string book, int number, int total, Look look)
    {
        using var muted = new SKPaint { Color = Look.Muted, IsAntialias = true };
        using var rule = new SKPaint { Color = look.Accent, StrokeWidth = 1.5f, IsStroke = true };
        using var small = new SKFont(look.Regular, 10);
        Text(canvas, book, Margin, HeaderY, SKTextAlign.Left, small, muted);
        Text(canvas, $"{number} of {total}", Width - Margin, HeaderY, SKTextAlign.Right, small, muted);
        canvas.DrawLine(Margin, HeaderY + 9, Width - Margin, HeaderY + 9, rule);
    }

    /// <remarks>
    /// A page whose text goes on says so at its foot. lanternkeepermotion1 (2026-11-14) proofed a story page that ended
    /// "…arrives in amber and / cools to", took the treatment for cut off, and never opened the page it continued on.
    /// </remarks>
    private static void Render(SKCanvas canvas, string book, Page page, List<Line> lines, bool continued, bool overflow, bool continues,
        int number, int total, Look look)
    {
        canvas.Clear(Look.Ground);
        using var ink = new SKPaint { Color = Look.Ink, IsAntialias = true };
        using var muted = new SKPaint { Color = Look.Muted, IsAntialias = true };
        using var rule = new SKPaint { Color = Look.Rule, StrokeWidth = 1, IsStroke = true };
        using var titleFont = new SKFont(look.Bold, 24);
        using var captionFont = new SKFont(look.Regular, 11);
        Header(canvas, book, number, total, look);

        if (page.Title is { } title)
            Text(canvas, overflow ? $"{title} (continued)" : title, Margin, TitleY, SKTextAlign.Left, titleFont, ink);

        var top = page.Title is null ? TitleY - 24 : TitleY + 18;
        if (!continued && page.Image is { } bytes)
        {
            using var image = SKImage.FromEncodedData(bytes) ?? throw new ArgumentException("Stage.book: an image could not be decoded.");
            var boxW = (lines.Any(l => l.Text.Length > 0) ? Width * 0.58f - Margin * 1.5f : Width - Margin * 2);
            var boxH = Height - top - Margin - (page.Caption is null ? 0 : 22);
            var scale = Math.Min(boxW / image.Width, boxH / image.Height);
            var rect = SKRect.Create(Margin, top, image.Width * scale, image.Height * scale);
            canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            canvas.DrawRect(rect, rule);
            if (page.Caption is { } caption)
                foreach (var (l, i) in Wrap(Plain(caption), 11, false, 0, Math.Max(rect.Width, 260), look).Take(2).Select((l, i) => (l, i)))
                    Text(canvas, l.Text, Margin, rect.Bottom + 15 + i * 13, SKTextAlign.Left, captionFont, muted);
        }

        var x = page.Image is null || continued ? Margin : Width * 0.58f;
        var y = top;
        foreach (var line in lines)
        {
            y += line.Before + line.Size * 1.35f;
            if (line.Text.Length == 0) continue;
            using var font = new SKFont(line.Strong ? look.Bold : look.Regular, line.Size);
            Text(canvas, line.Text, x + line.Indent, y - line.Size * 0.3f, SKTextAlign.Left, font, ink);
        }

        if (continues)
        {
            using var small = new SKFont(look.Regular, 10);
            Text(canvas, $"continued on page {number + 1} →", Width - Margin, Height - Margin / 2, SKTextAlign.Right, small, muted);
        }
    }

    /// <summary>Shaw's cover: simple and clear — the project, who it is for, the date and the studio.</summary>
    private static void RenderCover(SKCanvas canvas, string book, Cover cover, Look look)
    {
        canvas.Clear(Look.Ground);
        using var ink = new SKPaint { Color = Look.Ink, IsAntialias = true };
        using var muted = new SKPaint { Color = Look.Muted, IsAntialias = true };
        using var accent = new SKPaint { Color = look.Accent, IsAntialias = true };
        using var titleFont = new SKFont(look.Bold, 40);
        using var lineFont = new SKFont(look.Regular, 16);
        using var smallFont = new SKFont(look.Regular, 12);

        canvas.DrawRect(SKRect.Create(Margin, Height * 0.36f, 64, 5), accent);
        var y = Height * 0.36f + 58;
        foreach (var line in Wrap(book, 40, true, 0, Width - Margin * 2, look).Take(2)) { Text(canvas, line.Text, Margin, y, SKTextAlign.Left, titleFont, ink); y += 48; }
        if (cover.Subtitle is { Length: > 0 } subtitle) { Text(canvas, subtitle, Margin, y + 4, SKTextAlign.Left, lineFont, ink); y += 26; }
        if (cover.Client is { Length: > 0 } client) Text(canvas, $"Prepared for {client}", Margin, y + 8, SKTextAlign.Left, lineFont, muted);
        var foot = string.Join("   ·   ", new[] { cover.Studio, cover.Date }.Where(s => !string.IsNullOrWhiteSpace(s)));
        Text(canvas, foot, Margin, Height - Margin, SKTextAlign.Left, smallFont, muted);
    }

    /// <summary>The contents: each section's title and the page it opens on.</summary>
    private static void RenderContents(SKCanvas canvas, string book, IReadOnlyList<(string Title, int Page)> entries, int number, int total, Look look)
    {
        canvas.Clear(Look.Ground);
        using var ink = new SKPaint { Color = Look.Ink, IsAntialias = true };
        using var accent = new SKPaint { Color = look.Accent, IsAntialias = true };
        using var titleFont = new SKFont(look.Bold, 24);
        using var entryFont = new SKFont(look.Regular, 13);
        Header(canvas, book, number, total, look);
        Text(canvas, "Contents", Margin, TitleY, SKTextAlign.Left, titleFont, ink);

        // Two columns once one would run off the page, so a long book's contents stay on one page.
        var perColumn = (int)((Height - Margin - TitleY - 30) / 22);
        var columnW = (Width - Margin * 2 - 40) / 2;
        for (var i = 0; i < entries.Count && i < perColumn * 2; i++)
        {
            var x = Margin + (i / perColumn) * (columnW + 40);
            var y = TitleY + 40 + (i % perColumn) * 22;
            var label = entries[i].Title;
            while (label.Length > 4 && TextShaper.Measure(label, entryFont) > columnW - 50) label = label[..^2] + "…";
            Text(canvas, label, x, y, SKTextAlign.Left, entryFont, ink);
            Text(canvas, entries[i].Page.ToString(CultureInfo.InvariantCulture), x + columnW, y, SKTextAlign.Right, entryFont, accent);
        }
    }

    /// <summary>Shaw's closing page: a simple word of thanks, and the studio.</summary>
    private static void RenderClose(SKCanvas canvas, string book, string words, string? studio, int total, Look look)
    {
        canvas.Clear(Look.Ground);
        using var ink = new SKPaint { Color = Look.Ink, IsAntialias = true };
        using var muted = new SKPaint { Color = Look.Muted, IsAntialias = true };
        using var accent = new SKPaint { Color = look.Accent, IsAntialias = true };
        using var bigFont = new SKFont(look.Bold, 34);
        using var smallFont = new SKFont(look.Regular, 13);
        Header(canvas, book, total, total, look);
        canvas.DrawRect(SKRect.Create(Width / 2 - 32, Height / 2 - 48, 64, 4), accent);
        Text(canvas, words, Width / 2, Height / 2 + 4, SKTextAlign.Center, bigFont, ink);
        if (studio is { Length: > 0 }) Text(canvas, studio, Width / 2, Height / 2 + 36, SKTextAlign.Center, smallFont, muted);
    }
    #endregion
}
