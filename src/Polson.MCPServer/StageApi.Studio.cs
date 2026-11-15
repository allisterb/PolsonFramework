namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SkiaSharp;

/// <summary>The studio's half of <see cref="StageApi"/>: the process book, mock-ups and the planning sheets.</summary>
/// <remarks>
/// <c>Stage.book</c>, <c>mockup</c>, <c>mindMap</c>, <c>matrix</c>, <c>shape</c> and <c>borders</c>, and the refusal of
/// claims that <c>Verify</c> and the client's-words check record themselves. Kept apart so <c>Polson.Engine</c> keeps the
/// run record (<c>begin</c>, <c>end</c>, <c>note</c>, <c>expect</c>, <c>check</c>) without Taste, Verify or ClientWords.
/// </remarks>
public sealed partial class StageApi
{
    #region Constants
    /// <summary>Continuation pages one book page's text may run onto before <c>Stage.book</c> says it is a file pasted.</summary>
    private const int MaxOverflow = 2;
    #endregion

    #region Methods
    partial void RefuseRecordedClaim(string clean)
    {
        // **A read-back's verdict is the read-back's to record.** A live run dropped its Verify.image call and wrote the
        // claim itself as passed, over a check that had failed; the studio would have shown the pass.
        if (clean.StartsWith(VerifyToolkit.ReadBackPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.check: a claim beginning '{VerifyToolkit.ReadBackPrefix}' is recorded by Verify.image and "
                + "Verify.film themselves, from what the reader read. Run the check; do not write its verdict.");
        if (clean.StartsWith(VerifyToolkit.ClaimPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.check: a claim beginning '{VerifyToolkit.ClaimPrefix}' is recorded by Verify.claim itself, "
                + "from the code that checked it. Run the check; do not write its verdict.");
        if (clean.StartsWith(ClientWords.ClaimPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.check: a claim beginning '{ClientWords.ClaimPrefix}' is recorded by the server when a logo's "
                + "mark is written, from the words it drew. Fix the words; do not write the verdict.");
        if (clean.StartsWith(VerifyToolkit.DerivationPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.check: a claim beginning '{VerifyToolkit.DerivationPrefix}' is recorded by Verify.derivation "
                + "itself, from the mathematics. Run the check; do not write its verdict.");
        if (clean.StartsWith(VerifyToolkit.SizePrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.check: a claim beginning '{VerifyToolkit.SizePrefix}' is recorded by Verify.film itself, "
                + "from the encoded film. Encode at the brief's size and check it again; do not write its verdict.");
    }

    /// <summary>
    /// The run's process book: a multi-page PDF of the work in order — the brief, the concepts, the boards, the frames, the
    /// finished piece — written to <paramref name="filePath"/>. <c>options</c>: <c>{ title, pages }</c>, each page
    /// <c>{ title?, image?, caption?, text?, file? }</c>, <c>image</c> and <c>file</c> project paths (a still, and a text or
    /// Markdown file such as <c>concepts.md</c>). <c>proof: 'artifacts/pitch_proof'</c> also writes every page as a PNG,
    /// <c>_01.png</c>, <c>_02</c>…, to read back. Returns <c>{ path, pages, bytes, kind, proofs, warnings }</c>.
    /// </summary>
    /// <remarks>
    /// What a studio hands its client beside the work, and what the run hands its reader: Lupton's concept presentation,
    /// sequential, simple and headed on every page (<i>Graphic Design Thinking</i>, pp. 108–109). It assembles what the run
    /// already made; it draws nothing new.
    /// </remarks>
    public Dictionary<string, object?> Book(string filePath, object? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.book writes a PDF: name it '….pdf', not '{filePath}'.");
        var o = JsInterop.AsDict(options) ?? throw new ArgumentException("Stage.book needs { title, pages: [{ title, image, caption, text, file }] }.");
        var title = Clean(o["title"]?.ToString() ?? "", MaxNameLength);
        if (title.Length == 0) throw new ArgumentException("Stage.book needs a title: the project, as every page's header names it.");
        if (o["pages"] is not System.Collections.IEnumerable given || o["pages"] is string)
            throw new ArgumentException("Stage.book's pages is a list: [{ title, image, caption, text, file }].");
        // Shaw's two books (Manual 25 §13d): a process book shows how the work was arrived at; a pitch book sells the chosen
        // concept, and carries its treatment, its design board and its frames rather than the workshop's sheets.
        var kind = o["kind"]?.ToString()?.Trim().ToLowerInvariant() ?? "process";
        if (kind is not ("process" or "pitch"))
            throw new ArgumentException($"Stage.book's kind is 'process' or 'pitch', not '{kind}'.");

        var pages = new List<Polson.Drawing.Skia.PdfBook.Page>();
        var warnings = new List<string>();
        var shown = new List<string>();
        foreach (var item in given)
        {
            var p = JsInterop.AsDict(item) ?? throw new ArgumentException("Each of Stage.book's pages is an object: { title, image, caption, text, file }.");
            foreach (var key in p.Keys.Cast<object>().Select(k => k.ToString()!))
                if (key is not ("title" or "image" or "caption" or "text" or "file"))
                    throw new ArgumentException($"Stage.book: a page takes title, image, caption, text and file, not '{key}'. "
                        + "A still is image: 'artifacts/03_concepts_a.png'; file is a text or Markdown file such as 'concepts.md'.");
            byte[]? image = null;
            if (p["image"]?.ToString() is { Length: > 0 } imagePath)
            {
                var full = ProjectPath.Resolve(projectRoot, imagePath, "image", "Read");
                if (!File.Exists(full)) throw new ArgumentException($"Stage.book: '{imagePath}' is not in the project. Name a still you rendered, e.g. 'artifacts/03_concepts_a.png'.");
                // The book is the client's, and a research sheet shows other people's posters and logos.
                if (projectRoot is not null && events?.IsResearch(Path.GetRelativePath(projectRoot, full)) == true)
                    throw new ArgumentException($"Stage.book: '{imagePath}' is a research sheet of Qloo's pictures — other people's posters and logos — " +
                        "and the process book is the client's. Show the research board instead, e.g. 'artifacts/01_field_a.png' (Manual 29 §2b).");
                if (!System.Text.RegularExpressions.Regex.IsMatch(full, @"\.(png|jpe?g|webp)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    throw new ArgumentException($"Stage.book's pages show stills (.png, .jpg, .webp), not '{imagePath}'. For a film, render a frame of it.");
                image = File.ReadAllBytes(full);
                shown.Add(Path.GetFileNameWithoutExtension(full));
            }
            var text = PageText(p["text"], "text", paragraphs: true);
            if (p["file"]?.ToString() is { Length: > 0 } filePart)
            {
                var full = ProjectPath.Resolve(projectRoot, filePart, "file", "Read");
                if (!File.Exists(full)) throw new ArgumentException($"Stage.book: '{filePart}' is not in the project.");
                text = (text is null ? "" : text + "\n\n") + BookText(full, filePart);
                // A test run's findings are its report to the studio about the studio: friction, gaps, defects. A client's
                // book that carries them hands over the workshop's notes with the work (nightshift1: 17 of 46 pages).
                if (Path.GetFileName(full).Equals("findings.md", StringComparison.OrdinalIgnoreCase))
                    warnings.Add("findings.md is the run's report to the studio, not part of the work: leave it out of the book.");
            }
            pages.Add(new(PageText(p["title"], "title"), image, PageText(p["caption"], "caption"), text));
        }

        // A page's words as a string. An array reached the PDF as "System.Object[]" (becool2), so a list of paragraphs is
        // joined for text and anything else not a string is refused, as an unknown key already is.
        static string? PageText(object? value, string field, bool paragraphs = false) => value switch
        {
            null => null,
            string s => s,
            IConvertible c => c.ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Collections.IEnumerable list when paragraphs && list.Cast<object?>().All(x => x is string) => string.Join("\n\n", list.Cast<string>()),
            _ => throw new ArgumentException($"Stage.book: a page's {field} is a string{(paragraphs ? ", or a list of paragraphs" : "")}, "
                + $"not {(value is System.Collections.IEnumerable ? "a list with something other than strings in it" : value.GetType().Name)}."),
        };
        if (pages.Count is 0 or > 60) throw new ArgumentException($"Stage.book takes 1 to 60 pages, not {pages.Count}.");
        if (kind == "pitch")
        {
            // The workshop's sheets belong in the process book; a pitch shows the idea already chosen, finished.
            var workshop = shown.Where(s => PitchExcluded.IsMatch(s)).ToArray();
            if (workshop.Length > 0)
                warnings.Add($"a pitch book sells the chosen concept, so the workshop's sheets go in the process book: {string.Join(", ", workshop)}.");
            if (!shown.Any(s => s.Contains("board", StringComparison.OrdinalIgnoreCase) || s.Contains("style", StringComparison.OrdinalIgnoreCase)))
                warnings.Add("a pitch book shows the look: its design board or its style frames, and it has neither.");
            if (!given.Cast<object?>().Select(JsInterop.AsDict).Any(p => p?["file"]?.ToString()?.Contains("treatment", StringComparison.OrdinalIgnoreCase) == true))
                warnings.Add("a pitch book opens on the story: pass treatment.md as a page's file.");
        }

        // Shaw's frame for the book (Manual 25 §13d): a cover unless asked not to, contents when it runs long, a closing page
        // for a pitch, and the piece's own typeface and accent on the neutral ground if given.
        var coverGiven = JsInterop.AsDict(o["cover"]);
        var cover = o["cover"] is false ? null : new Polson.Drawing.Skia.PdfBook.Cover(
            coverGiven?["client"]?.ToString(), coverGiven?["date"]?.ToString() ?? DateTime.Now.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
            coverGiven?["studio"]?.ToString() ?? "Polson Studio", coverGiven?["subtitle"]?.ToString());
        var font = o["font"]?.ToString();
        if (font is { Length: > 0 } && !Polson.Drawing.Skia.PdfBook.Installed(font))
            warnings.Add($"the typeface '{font}' is not installed, so the book is set in a substitute. Name an installed family, or leave font out for Arial.");
        var accent = o["accent"]?.ToString();
        if (accent is { Length: > 0 } && !SkiaSharp.SKColor.TryParse(accent, out _))
            throw new ArgumentException($"Stage.book's accent is a colour such as '#c8553d', not '{accent}'.");
        var close = o["close"] is false ? null : o["close"]?.ToString() ?? (kind == "pitch" ? "Thank you." : null);
        bool? contents = o["contents"] is bool b ? b : null;
        // nightshift2 could not open either of its books (its host had no PDF renderer), so "read every word back" reached only
        // the Markdown. `proof` writes every page as a PNG beside the PDF, the same drawing, for the image tool to read.
        var proof = o["proof"]?.ToString() is { Length: > 0 } prefix ? prefix : null;
        var (pdf, count, overflows, previews) = Polson.Drawing.Skia.PdfBook.Write(title, pages, new(font, accent, cover, contents, close),
            proof is null ? 0 : 1.5f);
        // A page whose text runs on for pages is a file pasted, not a page written: nightshift1 put the whole of
        // direction.md in, ten pages of it. Summarise it in a page of your own — the decisions, each with its evidence.
        for (var i = 0; i < pages.Count; i++)
            if (overflows[i] > MaxOverflow)
                warnings.Add($"'{pages[i].Title ?? $"page {i + 1}"}' runs on for {overflows[i] + 1} pages: a book page is read at a glance. "
                    + "Write its substance as a page of text — the decisions and their evidence — rather than passing the whole file.");
            // Beside an image the text has half the page, so a page of prose runs over, and its proof looks cut off:
            // lanternkeepermotion1 read its story page alone and took the treatment for truncated.
            else if (overflows[i] > 0 && pages[i].Image is not null)
                warnings.Add($"'{pages[i].Title ?? $"page {i + 1}"}' has more text than fits beside its image, so it continues on the "
                    + "next page: proof both, or give the image a page of its own.");
        foreach (var w in warnings) result?.Logs.Add("[WARN] Stage.book: " + w);
        var output = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Write to");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllBytes(output, pdf);
        events?.Append("render", session?.Stage, executionId, new Dictionary<string, object?>
        {
            ["artifact"] = events.Relativize(output),
            ["format"] = "pdf",
            ["bytes"] = pdf.Length,
            ["pages"] = count,
            ["book"] = kind,
            ["warnings"] = warnings.Count == 0 ? null : warnings.ToArray(),
        });
        var proofs = new List<object?>();
        for (var i = 0; i < previews.Length; i++)
        {
            var page = $"{proof}_{i + 1:00}.png";
            var full = ProjectPath.Resolve(projectRoot, page, "proof", "Write to");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, previews[i]);
            // A sheet, not a render of the work: a proof page must never be taken for the piece.
            events?.Append("render", session?.Stage, executionId, new Dictionary<string, object?>
            {
                ["artifact"] = events.Relativize(full), ["format"] = "png", ["bytes"] = previews[i].Length, ["motion"] = "sheet", ["kind"] = "proof",
            });
            proofs.Add(page);
        }
        return new()
        {
            ["path"] = filePath, ["pages"] = count, ["bytes"] = (long)pdf.Length, ["kind"] = kind,
            ["proofs"] = proofs.ToArray(), ["warnings"] = warnings.Cast<object?>().ToList(),
        };
    }

    /// <summary>
    /// The work shown where it will live (Manual 30 §4; Lupton, pp. 136–138): <paramref name="options"/> is
    /// <c>{ design, setting?, size?, width?, height?, label?, onto?, corners?, side?, top?, back?, accent? }</c>. Returns
    /// <c>{ path, setting, image, corners, size, pixelsPerMm, warnings }</c>; <c>image</c> is a canvas, so a film's
    /// frames can be shown on a phone one by one. <paramref name="filePath"/> may be null to draw without writing, and
    /// a file's name says <c>mockup</c>: a simulated placement is never filed as the piece.
    /// </summary>
    public Dictionary<string, object?> Mockup(string? filePath, object? options)
    {
        var o = JsInterop.AsDict(options) ?? throw new ArgumentException("Stage.mockup needs { design, setting }: the work, and where it is shown.");
        if (filePath is not null && !Path.GetFileName(filePath).Contains("mockup", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.mockup: name the file as a mock-up, e.g. 'artifacts/mockup_wall_a.png', not '{filePath}'. A simulated placement is never filed as the piece.");
        if (filePath is not null && !filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Stage.mockup writes a PNG: name it '….png', not '{filePath}'.");
        using var design = MockupImage(o["design"], "design") ?? throw new ArgumentException("Stage.mockup needs a design: the canvas, bitmap or paper of the finished work.");
        using var onto = MockupImage(o["onto"], "onto");
        using var side = MockupImage(o["side"], "side");
        using var top = MockupImage(o["top"], "top");
        using var back = MockupImage(o["back"], "back");
        var setting = o["setting"]?.ToString() ?? (onto is null ? "wall" : "onto");
        if (onto is not null && setting != "onto") throw new ArgumentException($"Stage.mockup: onto is for setting 'onto', a picture with corners; '{setting}' draws its own.");
        var (widthMm, heightMm) = MockupSize(o["size"], design.Width >= design.Height);
        SKColor? accent = o["accent"]?.ToString() is { Length: > 0 } a
            ? SKColor.TryParse(a, out var c) ? c : throw new ArgumentException($"Stage.mockup's accent is a colour such as '#c8553d', not '{a}'.") : null;
        var spec = new Polson.Drawing.Skia.Mockup.Spec(setting, widthMm, heightMm,
            o["width"] is { } w ? Convert.ToInt32(w, System.Globalization.CultureInfo.InvariantCulture) : 1600,
            o["height"] is { } h ? Convert.ToInt32(h, System.Globalization.CultureInfo.InvariantCulture) : 1000,
            o["label"]?.ToString() ?? Polson.Drawing.Skia.Mockup.DefaultLabel,
            onto, MockupCorners(o["corners"]), side, top, back, accent, o["find"] is true);

        // A logo's mock-up carries words the agent wrote as readily as a sheet does (ClientWords): said while it can be fixed.
        if (ClientWords.Applies(projectRoot)
            && ClientWords.Foreign(ClientWords.LinesOf(o["design"]), ClientWords.Brief(projectRoot!)) is { Length: > 0 } foreign)
        {
            var what = filePath ?? "this mock-up";
            result?.Logs.Add(ClientWords.SheetWarning(what, foreign));
            events?.Append("render.words", session?.Stage, executionId, new Dictionary<string, object?>
            {
                ["artifact"] = filePath, ["words"] = foreign,
            });
        }

        var drawn = Polson.Drawing.Skia.Mockup.Render(design, spec);
        using var picture = drawn.Image;
        var canvas = new Polson.Drawing.Skia.SkiaCanvas(picture.Width, picture.Height);
        using (var onCanvas = new SKCanvas(canvas.SkBitmap)) onCanvas.DrawBitmap(picture, 0, 0);
        foreach (var warning in drawn.Warnings) result?.Logs.Add("[WARN] Stage.mockup: " + warning);

        if (filePath is not null)
        {
            var output = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Write to");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using var png = picture.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(output, png.ToArray());
            // A sheet, not a render of the work, as a book's proof is: a mock-up must never be taken for the piece.
            events?.Append("render", session?.Stage, executionId, new Dictionary<string, object?>
            {
                ["artifact"] = events.Relativize(output), ["format"] = "png", ["bytes"] = png.Size, ["motion"] = "sheet", ["kind"] = "mockup",
                ["setting"] = setting,
            });
        }
        return new()
        {
            ["path"] = filePath, ["setting"] = setting, ["image"] = canvas,
            ["corners"] = drawn.Corners.Select(p => (object?)new Dictionary<string, object?> { ["x"] = p.X, ["y"] = p.Y }).ToArray(),
            ["size"] = new Dictionary<string, object?> { ["width"] = Math.Round(drawn.WidthMm), ["height"] = Math.Round(drawn.HeightMm) },
            ["pixelsPerMm"] = drawn.PixelsPerMm is { } ppmm ? Math.Round(ppmm, 4) : null,
            ["warnings"] = drawn.Warnings.Cast<object?>().ToList(),
        };
    }

    /// <summary>A picture given to <c>Stage.mockup</c>, as a bitmap it owns; null when none was given.</summary>
    private SKBitmap? MockupImage(object? value, string field) => value is null ? null : DecodeMockupImage(value, field)
        ?? throw new ArgumentException($"Stage.mockup's {field} could not be decoded.");

    private SKBitmap? DecodeMockupImage(object value, string field) => value switch
    {
        Polson.Drawing.Skia.SkiaCanvas c => c.SkBitmap.Copy(),
        Polson.Drawing.Skia.SkiaBitmapWrapper b => b.Bitmap.Copy(),
        Polson.Drawing.Svg.SnapPaper p => SKBitmap.Decode(p.ToImageBytes((int)Math.Ceiling(p.Width > 0 ? p.Width : 800), (int)Math.Ceiling(p.Height > 0 ? p.Height : 600), "png", 100)),
        ILosslessImageSource l => SKBitmap.Decode(l.ToLosslessBytes()),
        IDataUriSource d when d.ToDataUri() is { Length: > 0 } uri && uri.IndexOf(";base64,", StringComparison.Ordinal) is var at and > 0
            => SKBitmap.Decode(Convert.FromBase64String(uri[(at + 8)..])),
        string path when System.Text.RegularExpressions.Regex.IsMatch(path, @"\.(png|jpe?g|webp)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            => File.Exists(ProjectPath.Resolve(projectRoot, path, field, "Read"))
                ? SKBitmap.Decode(File.ReadAllBytes(ProjectPath.Resolve(projectRoot, path, field, "Read")))
                : throw new ArgumentException($"Stage.mockup: '{path}' is not in the project."),
        _ => throw new ArgumentException($"Stage.mockup's {field} is a canvas, a bitmap, a paper, a requisitioned picture or a project image path, not a {value.GetType().Name}."),
    };

    /// <summary>
    /// A size in millimetres: an ISO name (<c>'A0'</c>–<c>'A6'</c>, turned to the design's orientation), <c>'one-sheet'</c>
    /// (27 × 40 in), or <c>{ width?, height? }</c>. Either side may be missing; the design's shape gives the other.
    /// </summary>
    private static (double? Width, double? Height) MockupSize(object? value, bool landscape)
    {
        if (value is null) return (null, null);
        if (value is string name)
        {
            (double Short, double Long)? iso = name.Trim().ToUpperInvariant() switch
            {
                "A0" => (841, 1189), "A1" => (594, 841), "A2" => (420, 594), "A3" => (297, 420), "A4" => (210, 297), "A5" => (148, 210), "A6" => (105, 148),
                "ONE-SHEET" => (686, 1016),
                _ => null,
            };
            if (iso is not { } s) throw new ArgumentException($"Stage.mockup's size is 'A0'–'A6', 'one-sheet', or {{ width, height }} in millimetres; not '{name}'.");
            return landscape ? (s.Long, s.Short) : (s.Short, s.Long);
        }
        var d = JsInterop.AsDict(value) ?? throw new ArgumentException("Stage.mockup's size is a name such as 'A2', or { width, height } in millimetres.");
        double? Mm(string key) => d.Contains(key) && d[key] is not null ? Convert.ToDouble(d[key], System.Globalization.CultureInfo.InvariantCulture) : null;
        var (w, h) = (Mm("width"), Mm("height"));
        if (w is <= 0 || h is <= 0 || (w is null && h is null)) throw new ArgumentException("Stage.mockup's size needs a width or a height above 0, in millimetres.");
        return (w, h);
    }

    private static SKPoint[]? MockupCorners(object? value)
    {
        if (value is null) return null;
        if (value is not System.Collections.IEnumerable list || value is string) throw new ArgumentException("Stage.mockup's corners are four points: [topLeft, topRight, bottomRight, bottomLeft].");
        var points = list.Cast<object?>().Select(p => JsInterop.AsDict(p) is { } d && d.Contains("x")
            ? new SKPoint(Convert.ToSingle(d["x"], System.Globalization.CultureInfo.InvariantCulture), Convert.ToSingle(d["y"], System.Globalization.CultureInfo.InvariantCulture))
            : p is System.Collections.IEnumerable xy && p is not string && xy.Cast<object?>().ToArray() is [var x, var y]
                ? new SKPoint(Convert.ToSingle(x, System.Globalization.CultureInfo.InvariantCulture), Convert.ToSingle(y, System.Globalization.CultureInfo.InvariantCulture))
                : throw new ArgumentException("Each of Stage.mockup's corners is { x, y } or [x, y].")).ToArray();
        return points.Length == 4 ? points : throw new ArgumentException($"Stage.mockup's corners are four points, not {points.Length}.");
    }

    /// <summary>
    /// The run's mind map (Manual 29 §2a; Lupton, pp. 22–25): reads the outline at <paramref name="filePath"/>, checks
    /// every measured node against the Taste answer it cites, and — given a <c>ctx</c> — draws it radially, a colour
    /// per branch. Returns <c>{ centre, depth, branches, leaves, measured, assumed, unverified, typeSize, summary }</c>.
    /// </summary>
    /// <param name="ctx">A canvas context to draw into, or null to read the map without drawing it.</param>
    /// <param name="options"><c>{ x, y, width, height, font, legend }</c>: the box to draw in (the canvas by default).</param>
    public Dictionary<string, object?> MindMap(string filePath, object? ctx = null, object? options = null)
    {
        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Read");
        if (!File.Exists(full))
            throw new ArgumentException($"Stage.mindMap: '{filePath}' is not in the project. Write the outline first: a '# ' centre and nested '- ' branches.");
        var root = Polson.Drawing.Skia.MindMap.Parse(File.ReadAllText(full));
        var nodes = root.Descendants.ToArray();

        // A measured node is one a Qloo answer returned, checked as Taste.checkReferences checks direction.md: a node
        // that only remembers being measured is drawn as a fault and listed, never as evidence.
        var unverified = new List<string>();
        foreach (var n in nodes.Where(n => n.Kind == Polson.Drawing.Skia.MindMapKind.Measured))
        {
            var answer = session?.Taste.Get(n.Handle!);
            n.Verified = answer is not null && Polson.ExtendedMind.Taste.TasteReferences.Carries(answer, n.Text, n.IsTag);
            if (n.Verified == false)
                unverified.Add(answer is null ? $"{n.Trail} ({n.Handle} is not an answer of this session)" : $"{n.Trail} (not returned by {n.Handle})");
        }

        float typeSize = 0;
        if (ctx is Polson.Drawing.Skia.CanvasRenderingContext2D context)
        {
            var o = JsInterop.AsDict(options);
            float Option(string key, float fallback) =>
                o is not null && o.Contains(key) && o[key] is not null ? Convert.ToSingle(o[key], System.Globalization.CultureInfo.InvariantCulture) : fallback;
            typeSize = Polson.Drawing.Skia.MindMap.Draw(context, root,
                Option("x", 0), Option("y", 0), Option("width", context.Canvas.Width), Option("height", context.Canvas.Height),
                o?["font"]?.ToString() ?? "Georgia", 14, o is null || o["legend"] is not false);
        }
        else if (ctx is not null)
            throw new ArgumentException("Stage.mindMap's second argument is a canvas context to draw into — canvas.getContext('2d') — or null.");

        var leaves = root.Leaves.ToArray();
        int measured = nodes.Count(n => n.Kind == Polson.Drawing.Skia.MindMapKind.Measured),
            assumed = nodes.Count(n => n.Kind == Polson.Drawing.Skia.MindMapKind.Assumed);
        var depth = leaves.Max(l => l.Depth);
        var summary = $"{root.Children.Count} branches, {leaves.Length} leaves, {depth} levels deep; {measured} measured, {assumed} assumed"
            + (unverified.Count == 0 ? "." : $"; {unverified.Count} cited to an answer that did not return it: {string.Join("; ", unverified)}.");

        events?.Append("mindmap", session?.Stage, executionId, new Dictionary<string, object?>
        {
            ["path"] = filePath, ["branches"] = root.Children.Count, ["leaves"] = leaves.Length, ["depth"] = depth,
            ["measured"] = measured, ["assumed"] = assumed, ["unverified"] = unverified.Count == 0 ? null : unverified.ToArray(),
        });
        return new()
        {
            ["centre"] = root.Text,
            ["depth"] = depth,
            ["branches"] = root.Children.Select((b, i) => (object?)new Dictionary<string, object?>
            {
                ["name"] = b.Text, ["colour"] = Polson.Drawing.Skia.MindMap.Palette[i % Polson.Drawing.Skia.MindMap.Palette.Length],
                ["leaves"] = b.Leaves.Count(),
                ["measured"] = b.Descendants.Count(n => n.Kind == Polson.Drawing.Skia.MindMapKind.Measured),
                ["assumed"] = b.Descendants.Count(n => n.Kind == Polson.Drawing.Skia.MindMapKind.Assumed),
            }).ToArray(),
            ["leaves"] = leaves.Select(l => (object?)new Dictionary<string, object?>
            {
                ["text"] = l.Text, ["branch"] = l.Branch?.Text, ["trail"] = l.Trail, ["kind"] = l.Kind.ToString().ToLowerInvariant(),
                ["handle"] = l.Handle, ["verified"] = l.Verified,
            }).ToArray(),
            ["measured"] = measured,
            ["assumed"] = assumed,
            ["unverified"] = unverified.ToArray(),
            ["typeSize"] = typeSize,
            ["summary"] = summary,
        };
    }

    /// <summary>
    /// A brand matrix (Manual 29 §2d; Lupton, pp. 42–45): the audience's field and the concepts on two crossed
    /// polarities, drawn into <paramref name="ctx"/>, with each quadrant counted and the empty ones named. Returns
    /// <c>{ x, y, field, concepts, quadrants, empty, unplaced, summary }</c>.
    /// </summary>
    /// <param name="spec">
    /// <c>{ x, y, field, concepts, chosen, box, font }</c>. An axis is <c>{ measure, low?, high? }</c> — <c>lightness</c>,
    /// <c>chroma</c>, <c>colourful</c>, <c>warmth</c>, <c>popularity</c> or <c>year</c> — or <c>{ judged: { name: 0–1 }, low,
    /// high }</c>. <c>field</c> is <c>Taste.images</c>' pictures; <c>concepts</c> is <c>[{ label, image }]</c>.
    /// </param>
    public Dictionary<string, object?> Matrix(object? ctx, object? spec)
    {
        var o = JsInterop.AsDict(spec) ?? throw new ArgumentException("Stage.matrix needs { x, y, field, concepts }: two axes, Taste.images' pictures, and [{ label, image }].");
        var field = (o["field"] as System.Collections.IEnumerable)?.Cast<object?>().OfType<Polson.ExtendedMind.Taste.TasteImage>().Where(t => t.Success).ToArray() ?? [];
        var concepts = (o["concepts"] as System.Collections.IEnumerable)?.Cast<object?>().Select(JsInterop.AsDict).OfType<System.Collections.IDictionary>().ToArray() ?? [];
        var chosen = o["chosen"]?.ToString();

        // A concept is measured as a Qloo picture is measured on arrival, so the two sets of points are comparable.
        var readings = concepts.Select(c =>
        {
            var label = c["label"]?.ToString() ?? throw new ArgumentException("Stage.matrix: each concept is { label, image }, e.g. { label: 'A', image: conceptA.bitmap }.");
            var reading = c["image"] is ILosslessImageSource image && Polson.ExtendedMind.Taste.QlooImageSource.Decode(image.ToLosslessBytes(), "the script") is { Success: true, Colour: { } r } ? r : null;
            return (Label: label, Reading: reading);
        }).ToArray();

        var x = Axis(o["x"], "x");
        var y = Axis(o["y"], "y");
        var unplaced = new List<string>();
        double? Value(MatrixAxis a, string name, Polson.ExtendedMind.Taste.TasteImage? t, Polson.ExtendedMind.Taste.TasteColourReading? r)
        {
            if (a.Judged is { } judged) return judged.TryGetValue(name, out var v) ? v : null;
            double? lightness = t?.Lightness ?? r?.Lightness, chroma = t?.Chroma ?? r?.Chroma, neutral = t?.Neutral ?? r?.Neutral;
            var lead = t?.Lead ?? r?.Lead;
            return a.Measure switch
            {
                "lightness" => lightness,
                "chroma" => chroma,
                "colourful" => 1 - neutral,
                // Warm toward orange (OKLCH 60°), cool toward blue (240°); a picture with no lead hue sits between.
                "warmth" => lead?.GetValueOrDefault("hue") is double h ? 0.5 + 0.5 * Math.Cos((h - 60) * Math.PI / 180) : lightness is null ? null : 0.5,
                "popularity" => t?.Popularity,
                "year" => t?.Year,
                _ => null,
            };
        }

        var points = new List<(string Label, string Name, double X, double Y, string Colour, bool Concept)>();
        for (var i = 0; i < field.Length; i++)
        {
            var t = field[i];
            if (Value(x, t.Name, t, null) is { } vx && Value(y, t.Name, t, null) is { } vy)
                points.Add(($"{i + 1}", t.Name, vx, vy, t.Lead?.GetValueOrDefault("color") as string ?? "#bdbab2", false));
            else unplaced.Add($"{i + 1} {t.Name}");
        }
        foreach (var (label, reading) in readings)
        {
            if (reading is null && (x.Judged is null || y.Judged is null))
                throw new ArgumentException($"Stage.matrix: concept {label} has no image to measure. Give it one — {{ label: '{label}', image: canvas.bitmap }} — or place it on judged axes.");
            if (Value(x, label, null, reading) is { } vx && Value(y, label, null, reading) is { } vy)
                points.Add((label, label, vx, vy, "#15151a", true));
            else if (x.Measure is "popularity" or "year" || y.Measure is "popularity" or "year")
                throw new ArgumentException($"Stage.matrix: a concept has no {(x.Measure is "popularity" or "year" ? x.Measure : y.Measure)}. Place concepts on lightness, chroma, colourful or warmth, or on a judged axis.");
            else unplaced.Add(label);
        }
        if (points.Count == 0) throw new ArgumentException("Stage.matrix: nothing could be placed. Pass field: Taste.images' pictures, and concepts: [{ label, image }].");

        // The range is the points' own, padded, so the crossing is the middle of this field rather than of an ideal
        // scale: the question a matrix answers is where this field is crowded, not where it sits on the world's.
        // A judged axis keeps 0–1, since its numbers mean nothing but their order.
        (double Min, double Max) Range(MatrixAxis a, Func<(string, string, double X, double Y, string, bool), double> pick)
        {
            if (a.Judged is not null) return (0, 1);
            double lo = points.Min(p => pick(p)), hi = points.Max(p => pick(p)), pad = Math.Max((hi - lo) * 0.1, a.Measure == "year" ? 1 : 0.02);
            return (lo - pad, hi + pad);
        }
        var (x0, x1) = Range(x, p => p.X);
        var (y0, y1) = Range(y, p => p.Y);
        double xm = (x0 + x1) / 2, ym = (y0 + y1) / 2;

        string Quadrant(double vx, double vy) => $"{(vy >= ym ? y.High : y.Low)} & {(vx >= xm ? x.High : x.Low)}";
        var quadrants = new[] { (true, false), (true, true), (false, false), (false, true) }
            .Select(q => Quadrant(q.Item2 ? x1 : x0, q.Item1 ? y1 : y0)).ToArray();
        var inQuadrant = quadrants.ToDictionary(q => q, q => points.Where(p => !p.Concept && Quadrant(p.X, p.Y) == q).Select(p => p.Name).ToArray());
        var empty = quadrants.Where(q => inQuadrant[q].Length == 0).ToArray();
        var conceptResults = points.Where(p => p.Concept).Select(p =>
        {
            var q = Quadrant(p.X, p.Y);
            return (object?)new Dictionary<string, object?>
            {
                ["label"] = p.Label, ["x"] = Math.Round(p.X, 4), ["y"] = Math.Round(p.Y, 4), ["quadrant"] = q,
                ["fieldInQuadrant"] = inQuadrant[q].Length, ["chosen"] = p.Label == chosen,
            };
        }).ToArray();

        if (ctx is Polson.Drawing.Skia.CanvasRenderingContext2D context)
        {
            var box = JsInterop.AsDict(o["box"]);
            float B(string key, float fallback) => box is not null && box.Contains(key) && box[key] is not null ? Convert.ToSingle(box[key], System.Globalization.CultureInfo.InvariantCulture) : fallback;
            Polson.Drawing.Skia.BrandMatrix.Draw(context,
                new(x.Low, x.High, x.Kind, x0, x1), new(y.Low, y.High, y.Kind, y0, y1),
                [.. points.Select(p => new Polson.Drawing.Skia.BrandMatrixPoint(p.Label, p.X, p.Y, p.Colour, p.Concept, p.Label == chosen))],
                B("x", 0), B("y", 0), B("width", context.Canvas.Width), B("height", context.Canvas.Height), o["font"]?.ToString() ?? "Georgia");
        }
        else if (ctx is not null)
            throw new ArgumentException("Stage.matrix's first argument is a canvas context to draw into — canvas.getContext('2d') — or null.");

        var fieldCount = points.Count(p => !p.Concept);
        var summary = $"{fieldCount} of the field: " + string.Join(", ", quadrants.Select(q => $"{inQuadrant[q].Length} {q}"))
            + (empty.Length == 0 ? "; no quadrant empty" : $"; empty: {string.Join(", ", empty)}")
            + string.Concat(conceptResults.Cast<Dictionary<string, object?>>().Select(c =>
                $". Concept {c["label"]}{((bool)c["chosen"]! ? " (chosen)" : "")} sits {c["quadrant"]}, with {c["fieldInQuadrant"]} of the field"))
            + (unplaced.Count == 0 ? "." : $". Not placed: {string.Join(", ", unplaced)}.");

        events?.Append("matrix", session?.Stage, executionId, new Dictionary<string, object?>
        {
            ["x"] = x.Kind, ["y"] = y.Kind, ["field"] = fieldCount, ["empty"] = empty, ["summary"] = summary,
        });
        return new()
        {
            ["x"] = new Dictionary<string, object?> { ["low"] = x.Low, ["high"] = x.High, ["kind"] = x.Kind, ["min"] = x0, ["max"] = x1 },
            ["y"] = new Dictionary<string, object?> { ["low"] = y.Low, ["high"] = y.High, ["kind"] = y.Kind, ["min"] = y0, ["max"] = y1 },
            ["field"] = points.Where(p => !p.Concept).Select(p => (object?)new Dictionary<string, object?>
            {
                ["n"] = p.Label, ["name"] = p.Name, ["x"] = Math.Round(p.X, 4), ["y"] = Math.Round(p.Y, 4), ["quadrant"] = Quadrant(p.X, p.Y),
            }).ToArray(),
            ["concepts"] = conceptResults,
            ["quadrants"] = quadrants.Select(q => (object?)new Dictionary<string, object?> { ["name"] = q, ["count"] = inQuadrant[q].Length, ["names"] = inQuadrant[q] }).ToArray(),
            ["empty"] = empty,
            ["unplaced"] = unplaced.ToArray(),
            ["summary"] = summary,
        };
    }

    /// <summary>
    /// The shape of the story (Manual 25 §13b; Shaw, ch. 8): each beat's intensity, judged 0–1, drawn as a curve over the
    /// film's time, for the written treatment. Returns <c>{ peak, range, flat, warnings }</c>; <c>ctx</c> may be null.
    /// </summary>
    /// <param name="spec"><c>{ beats: [{ name, time, intensity }], duration?, box?, font? }</c>.</param>
    public Dictionary<string, object?> Shape(object? ctx, object? spec)
    {
        var o = JsInterop.AsDict(spec) ?? throw new ArgumentException("Stage.shape needs { beats: [{ name, time, intensity }] }.");
        var beats = (o["beats"] as System.Collections.IEnumerable)?.Cast<object?>().Select((b, i) =>
        {
            var d = JsInterop.AsDict(b) ?? throw new ArgumentException($"Stage.shape: beat {i + 1} is {{ name, time, intensity }}.");
            double Num(string k) => d[k] is { } v ? Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture)
                : throw new ArgumentException($"Stage.shape: beat {i + 1} has no {k}.");
            var intensity = Num("intensity");
            if (intensity is < 0 or > 1) throw new ArgumentException($"Stage.shape: beat {i + 1}'s intensity is 0 to 1, not {intensity}.");
            return (Name: d["name"]?.ToString() ?? $"beat {i + 1}", Time: Num("time"), Intensity: intensity);
        }).OrderBy(b => b.Time).ToArray() ?? [];
        if (beats.Length < 2) throw new ArgumentException("Stage.shape needs at least two beats: a shape is a change.");
        var duration = o["duration"] is { } dur ? Convert.ToDouble(dur, System.Globalization.CultureInfo.InvariantCulture) : beats[^1].Time;

        var range = beats.Max(b => b.Intensity) - beats.Min(b => b.Intensity);
        var peak = beats.MaxBy(b => b.Intensity);
        var warnings = new List<string>();
        // Shaw's graph-editor lesson: a velocity that does not change is a straight line and dull, and a story is the same.
        if (range < 0.3)
            warnings.Add($"The story barely changes: its intensity spans {range:0.00} of 1. A flat line is static; give it a rise, a turn and a resolve (Shaw, ch. 8).");

        if (ctx is Polson.Drawing.Skia.CanvasRenderingContext2D context)
        {
            var box = JsInterop.AsDict(o["box"]);
            float B(string key, float fallback) => box is not null && box.Contains(key) && box[key] is not null ? Convert.ToSingle(box[key], System.Globalization.CultureInfo.InvariantCulture) : fallback;
            Polson.Drawing.Skia.StoryShape.Draw(context, [.. beats.Select(b => (b.Name, b.Time, b.Intensity))], duration,
                B("x", 0), B("y", 0), B("width", context.Canvas.Width), B("height", context.Canvas.Height), o["font"]?.ToString() ?? "Georgia");
        }
        else if (ctx is not null)
            throw new ArgumentException("Stage.shape's first argument is a canvas context to draw into — canvas.getContext('2d') — or null.");

        foreach (var w in warnings) result?.Logs.Add("[WARN] Stage.shape: " + w);
        return new()
        {
            ["peak"] = new Dictionary<string, object?> { ["name"] = peak.Name, ["time"] = peak.Time, ["intensity"] = peak.Intensity },
            ["range"] = Math.Round(range, 3), ["flat"] = range < 0.3, ["warnings"] = warnings.ToArray(),
        };
    }

    /// <summary>
    /// The DOs &amp; DON'Ts list (Manual 25 §13c; Shaw, ch. 7) checked: every <c>- DO</c> or <c>- DON'T</c> line of the file
    /// that ends with a clause in backticks is measured on <paramref name="images"/>, and the rest are listed as judged.
    /// Returns <c>{ passed, lines, judged, summary }</c>.
    /// </summary>
    /// <param name="images">A project path, a bitmap or canvas, or an array of them: the clause must hold on every one.</param>
    /// <param name="options"><c>{ fonts?: string[] }</c>: the typefaces used, for a <c>fonts</c> clause.</param>
    public Dictionary<string, object?> Borders(string filePath, object? images, object? options = null)
    {
        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Read");
        if (!File.Exists(full)) throw new ArgumentException($"Stage.borders: '{filePath}' is not in the project. Write the list first: '- DO …' and '- DON'T …' lines.");
        var targets = (images is string or ILosslessImageSource || images is null ? [images] : (images as System.Collections.IEnumerable)?.Cast<object?>().ToArray() ?? [images])
            .Where(t => t is not null).Select(t => (Name: t is string s ? s : "an image", Look: BorderLook.Of(t switch
            {
                string path => File.ReadAllBytes(ProjectPath.Resolve(projectRoot, path, "images", "Read")),
                ILosslessImageSource image => image.ToLosslessBytes(),
                _ => throw new ArgumentException("Stage.borders measures project paths, bitmaps or canvases."),
            }))).ToArray();
        var fonts = (JsInterop.AsDict(options)?["fonts"] as System.Collections.IEnumerable)?.Cast<object?>().Select(f => f?.ToString()).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var lines = new List<object?>();
        var judged = new List<string>();
        foreach (var raw in File.ReadAllLines(full))
        {
            if (BorderLine.Match(raw) is not { Success: true } m) continue;
            var kind = m.Groups[1].Value.StartsWith("DO", StringComparison.OrdinalIgnoreCase) && m.Groups[1].Value.Length == 2 ? "do" : "don't";
            var text = m.Groups[2].Value.Trim();
            if (BorderClause.Match(text) is not { Success: true } c) { judged.Add($"{kind.ToUpperInvariant()}: {text}"); continue; }

            var (metric, scope, op, limit) = (c.Groups["metric"].Value.Trim().ToLowerInvariant(), c.Groups["scope"].Value.ToLowerInvariant(), c.Groups["op"].Value,
                double.Parse(c.Groups["limit"].Value, System.Globalization.CultureInfo.InvariantCulture) / (c.Groups["percent"].Success ? 100 : 1));
            bool Holds(double v) => op switch { "<" => v < limit, "<=" => v <= limit, ">" => v > limit, _ => v >= limit };
            double[] values;
            var measured = (scope switch { "first" => targets.Take(1), "last" => targets.TakeLast(1), _ => targets }).ToArray();
            if (metric == "fonts")
                values = [fonts?.Length ?? throw new ArgumentException($"Stage.borders: '{text}' counts typefaces; pass {{ fonts: [...] }}, the families the piece sets.")];
            else
            {
                if (targets.Length == 0) throw new ArgumentException($"Stage.borders: '{text}' is measured on images; pass the frames or the final to measure.");
                values = [.. measured.Select(t => t.Look.Measure(metric) ?? throw new ArgumentException(
                    $"Stage.borders: '{metric}' is not a measure. Use lightness, chroma, colourful, warmth, balance, symmetry, 'offPalette #rrggbb #rrggbb …', 'hue <sector>' ({string.Join(", ", Polson.ExtendedMind.Taste.TasteVisual.SectorNames)}), 'colour #rrggbb' or fonts, each optionally 'on first' or 'on last'."))];
            }
            var worst = values.Select((v, i) => (v, i)).OrderBy(p => Holds(p.v) ? 1 : 0).First();
            lines.Add(new Dictionary<string, object?>
            {
                ["kind"] = kind, ["text"] = text[..c.Index].TrimEnd(' ', '—', '-', ':'), ["clause"] = c.Value.Trim('`'),
                ["passed"] = values.All(Holds), ["value"] = Math.Round(worst.v, 4),
                ["where"] = metric == "fonts" ? null : measured[worst.i].Name,
            });
        }

        var failed = lines.Cast<Dictionary<string, object?>>().Where(l => !(bool)l["passed"]!).ToArray();
        var summary = $"{lines.Count} measured, {judged.Count} judged"
            + (failed.Length == 0 ? "; every measured line holds." : $"; {failed.Length} broken: " + string.Join("; ", failed.Select(l => $"{l["text"]} ({l["clause"]}: {l["value"]} on {l["where"] ?? "the piece"})")) + ".");
        events?.Append("borders", session?.Stage, executionId, new Dictionary<string, object?>
        {
            ["path"] = filePath, ["measured"] = lines.Count, ["judged"] = judged.Count, ["broken"] = failed.Length, ["summary"] = summary,
        });
        return new() { ["passed"] = failed.Length == 0, ["lines"] = lines.ToArray(), ["judged"] = judged.ToArray(), ["summary"] = summary };
    }

    /// <summary>What the workflows name a workshop sheet: the map, the research and dump sheets, the concepts and the matrix.</summary>
    private static readonly System.Text.RegularExpressions.Regex PitchExcluded =
        new(@"^0[0-3]_(map|field|research|dump|concepts|matrix)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary><c>- DO …</c> or <c>- DON'T …</c>, the apostrophe straight, curly or absent.</summary>
    private static readonly System.Text.RegularExpressions.Regex BorderLine =
        new(@"^\s*[-*]\s*\**(DON[’']?T|DO)\b\**[:\s]*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// A measurable clause in backticks at the end: <c>`lightness &gt; 0.6`</c>, <c>`hue green &lt; 2%`</c>, <c>`colour #000000 &lt; 1%`</c>,
    /// <c>`offPalette #14202b #f2b134 &lt; 3%`</c>, <c>`symmetry on last &gt; 0.85`</c>.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex BorderClause =
        new(@"`\s*(?<metric>[a-z][a-z\- ]*?(?:\s*#[0-9a-f]{6})*)(?:\s+on\s+(?<scope>first|last))?\s*(?<op><=|>=|<|>)\s*(?<limit>[0-9]*\.?[0-9]+)\s*(?<percent>%)?\s*`\s*\.?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>An image measured once for every clause a DOs &amp; DON'Ts list may ask of it, in OKLCH.</summary>
    private sealed class BorderLook
    {
        private readonly List<(double L, double A, double B, double C, double H)> pixels = [];
        private Polson.ExtendedMind.Taste.TasteColourReading reading = null!;
        private Lazy<double> symmetry = null!;
        private Lazy<double> balance = null!;

        public static BorderLook Of(byte[] bytes)
        {
            var fetched = Polson.ExtendedMind.Taste.QlooImageSource.Decode(bytes, "the script");
            if (!fetched.Success) throw new ArgumentException($"Stage.borders: {fetched.Error}");
            var look = new BorderLook { reading = fetched.Colour!, symmetry = new(() => Symmetry(bytes)), balance = new(() => Balance(bytes)) };
            using var bitmap = SkiaSharp.SKBitmap.Decode(bytes);
            var step = Math.Max(1, Math.Max(bitmap.Width, bitmap.Height) / 200);
            for (var y = 0; y < bitmap.Height; y += step)
                for (var x = 0; x < bitmap.Width; x += step)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.Alpha == 0) continue;
                    var (L, C, H) = Polson.ExtendedMind.Taste.TasteColour.Oklch(p.Red, p.Green, p.Blue);
                    look.pixels.Add((L, C * Math.Cos(H * Math.PI / 180), C * Math.Sin(H * Math.PI / 180), C, H));
                }
            return look;
        }

        /// <summary>The measure a clause names, or null when it names none.</summary>
        public double? Measure(string metric)
        {
            if (metric.StartsWith("hue ", StringComparison.Ordinal))
            {
                var sector = Array.IndexOf(Polson.ExtendedMind.Taste.TasteVisual.SectorNames, metric[4..].Trim());
                return sector < 0 ? null : pixels.Count(p => p.C >= Polson.ExtendedMind.Taste.TasteColour.NeutralChroma && (int)(p.H / 30) % 12 == sector) / (double)Math.Max(1, pixels.Count);
            }
            if (metric.StartsWith("colour ", StringComparison.Ordinal) || metric.StartsWith("color ", StringComparison.Ordinal))
            {
                var hex = metric[(metric.IndexOf(' ') + 1)..].Trim().TrimStart('#');
                if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return null;
                var (L, C, H) = Polson.ExtendedMind.Taste.TasteColour.Oklch((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                double a = C * Math.Cos(H * Math.PI / 180), b = C * Math.Sin(H * Math.PI / 180);
                // Within 0.04 in Oklab: about as close as two colours can be and still be told apart side by side.
                return pixels.Count(p => Math.Sqrt((p.L - L) * (p.L - L) + (p.A - a) * (p.A - a) + (p.B - b) * (p.B - b)) < 0.04) / (double)Math.Max(1, pixels.Count);
            }
            if (OffPaletteName.Match(metric) is { Success: true } off)
            {
                var palette = System.Text.RegularExpressions.Regex.Matches(metric[off.Length..], "#([0-9a-f]{6})").Select(m => Oklab(Convert.ToInt32(m.Groups[1].Value, 16))).ToArray();
                if (palette.Length == 0)
                    throw new ArgumentException("Stage.borders: offPalette needs the palette in the clause: `offPalette #14202b #f2b134 #e8e2d6 < 3%`, the ground included.");
                return pixels.Count(p => OffPalette((p.L, p.A, p.B), palette)) / (double)Math.Max(1, pixels.Count);
            }
            return metric switch
            {
                "symmetry" => symmetry.Value,
                "balance" => balance.Value,
                "lightness" => reading.Lightness,
                "chroma" => reading.Chroma,
                "colourful" or "colorful" => 1 - reading.Neutral,
                "warmth" => reading.Lead?.GetValueOrDefault("hue") is double h ? 0.5 + 0.5 * Math.Cos((h - 60) * Math.PI / 180) : 0.5,
                _ => null,
            };
        }

        private static readonly System.Text.RegularExpressions.Regex OffPaletteName = new(@"^off[\- ]?palette\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static (double L, double A, double B) Oklab(int rgb)
        {
            var (L, C, H) = Polson.ExtendedMind.Taste.TasteColour.Oklch((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return (L, C * Math.Cos(H * Math.PI / 180), C * Math.Sin(H * Math.PI / 180));
        }

        /// <summary>
        /// Off the palette: farther than 0.06 in Oklab from every colour and from every blend of two, so a gradient
        /// between palette colours, or an antialiased edge, stays on it.
        /// </summary>
        private static bool OffPalette((double L, double A, double B) p, (double L, double A, double B)[] palette)
        {
            double Distance((double L, double A, double B) a, (double L, double A, double B) b)
            {
                var (dl, da, db) = (b.L - a.L, b.A - a.A, b.B - a.B);
                var length = dl * dl + da * da + db * db;
                var t = length == 0 ? 0 : Math.Clamp(((p.L - a.L) * dl + (p.A - a.A) * da + (p.B - a.B) * db) / length, 0, 1);
                var (x, y, z) = (p.L - a.L - t * dl, p.A - a.A - t * da, p.B - a.B - t * db);
                return Math.Sqrt(x * x + y * y + z * z);
            }
            for (var i = 0; i < palette.Length; i++)
                for (var j = i; j < palette.Length; j++)
                    if (Distance(palette[i], palette[j]) < 0.06) return false;
            return true;
        }

        /// <summary>
        /// Manual 25 §14a's balance: blurred until lettering is only weight, then compared with its own mirror, the
        /// share of pixels within 8 levels of their reflection. 1 is mirror-even; lower leans.
        /// </summary>
        private static double Symmetry(byte[] bytes)
        {
            using var source = SkiaSharp.SKBitmap.Decode(bytes);
            var (w, h) = (320, Math.Max(1, (int)Math.Round(320.0 * source.Height / source.Width)));
            using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
            using (var image = SkiaSharp.SKImage.FromBitmap(source))
            using (var blur = SkiaSharp.SKImageFilter.CreateBlur(w * 0.0375f, w * 0.0375f, SkiaSharp.SKShaderTileMode.Clamp))
            using (var paint = new SkiaSharp.SKPaint { ImageFilter = blur })
            {
                surface.Canvas.Clear(SkiaSharp.SKColors.White);
                surface.Canvas.DrawImage(image, new SkiaSharp.SKRect(0, 0, w, h), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear), paint);
            }
            using var soft = new SkiaSharp.SKBitmap(w, h);
            surface.ReadPixels(soft.Info, soft.GetPixels(), soft.RowBytes, 0, 0);
            var even = 0;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var (a, b) = (soft.GetPixel(x, y), soft.GetPixel(w - 1 - x, y));
                    if (Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)), Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha))) <= 8) even++;
                }
            return even / (double)(w * h);
        }

        /// <summary>
        /// Manual 25 §14a's balance as a lever: where the frame's visual weight — each pixel's Oklab distance from the
        /// ground, the commonest colour — balances across its width, as <c>1 − 2·|centre − ½|</c>. 1 balances on the middle;
        /// 0.9 balances 5% of the width off it. Unlike <see cref="Symmetry"/>, a card need not mirror itself to pass:
        /// cruelty2's centred billing over scraps strewn unevenly along its foot measured 0.8765 as symmetry, and is balanced.
        /// </summary>
        private static double Balance(byte[] bytes)
        {
            using var source = SkiaSharp.SKBitmap.Decode(bytes);
            var (w, h) = (320, Math.Max(1, (int)Math.Round(320.0 * source.Height / source.Width)));
            using var small = source.Resize(new SkiaSharp.SKImageInfo(w, h), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear));
            var colours = new SkiaSharp.SKColor[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++) colours[y * w + x] = small.GetPixel(x, y);

            // The ground: the commonest colour, to a sixteenth of each channel, averaged within its bucket.
            static int Bucket(SkiaSharp.SKColor c) => (c.Red >> 4) << 8 | (c.Green >> 4) << 4 | c.Blue >> 4;
            var common = colours.GroupBy(Bucket).MaxBy(g => g.Count())!;
            var ground = Oklab((int)common.Average(c => c.Red) << 16 | (int)common.Average(c => c.Green) << 8 | (int)common.Average(c => c.Blue));

            double weight = 0, moment = 0;
            for (var i = 0; i < colours.Length; i++)
            {
                var c = colours[i];
                var p = Oklab(c.Red << 16 | c.Green << 8 | c.Blue);
                var d = Math.Sqrt((p.L - ground.L) * (p.L - ground.L) + (p.A - ground.A) * (p.A - ground.A) + (p.B - ground.B) * (p.B - ground.B)) * c.Alpha / 255.0;
                weight += d;
                moment += d * ((i % w + 0.5) / w);
            }
            return weight == 0 ? 1 : 1 - 2 * Math.Abs(moment / weight - 0.5);
        }
    }

    /// <summary>An axis as a script gives it: a measure with default poles, or judged values the studio placed.</summary>
    private static MatrixAxis Axis(object? given, string which)
    {
        var a = JsInterop.AsDict(given) ?? throw new ArgumentException($"Stage.matrix: {which} is an axis — {{ measure: 'lightness' }} or {{ judged: {{ name: 0.2, … }}, low: 'formal', high: 'informal' }}.");
        if (JsInterop.AsDict(a["judged"]) is { } judged)
        {
            var values = judged.Keys.Cast<object>().ToDictionary(k => k.ToString()!, k => Convert.ToDouble(judged[k], System.Globalization.CultureInfo.InvariantCulture));
            return new(null, values, a["low"]?.ToString() ?? throw new ArgumentException($"Stage.matrix: a judged {which} axis names its poles, low and high."),
                a["high"]?.ToString() ?? throw new ArgumentException($"Stage.matrix: a judged {which} axis names its poles, low and high."), "judged by the studio");
        }
        var measure = a["measure"]?.ToString()?.Trim().ToLowerInvariant();
        (string Low, string High, string Kind) poles = measure switch
        {
            "lightness" => ("dark", "light", "measured · mean lightness"),
            "chroma" => ("muted", "vivid", "measured · mean chroma"),
            "colourful" or "colorful" => ("monochrome", "colourful", "measured · share in colour"),
            "warmth" => ("cool", "warm", "measured · lead hue"),
            "popularity" => ("niche", "mainstream", "Qloo · popularity"),
            "year" => ("older", "newer", "Qloo · release year"),
            _ => throw new ArgumentException($"Stage.matrix: '{measure}' is not a measure. Use lightness, chroma, colourful, warmth, popularity or year, or a judged axis."),
        };
        return new(measure == "colorful" ? "colourful" : measure, null, a["low"]?.ToString() ?? poles.Low, a["high"]?.ToString() ?? poles.High, poles.Kind);
    }

    private sealed record MatrixAxis(string? Measure, Dictionary<string, double>? Judged, string Low, string High, string Kind);

    /// <summary>A book page's <c>file</c>, which must be text: a still passed here is refused, naming <c>image</c>.</summary>
    /// <remarks>
    /// smoke2 passed ten PNGs as <c>file</c> after <c>path</c> was refused, and each was read as text and typeset: one
    /// 34 KB still made 8 pages and 11.6 MB, and the ten ran past the host's 60 s tool limit three times, so the run
    /// delivered no book.
    /// </remarks>
    private static string BookText(string full, string given)
    {
        if (StillFile.IsMatch(full))
            throw new ArgumentException($"Stage.book: '{given}' is a picture. Show it with image: '{given}'; file is a text or Markdown file such as 'concepts.md'.");
        var bytes = File.ReadAllBytes(full);
        if (!MediaFile.IsMatch(full) && Array.IndexOf(bytes, (byte)0) < 0)
        {
            try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'); }
            catch (DecoderFallbackException) { }
        }
        throw new ArgumentException($"Stage.book: '{given}' is not a text file. file takes text or Markdown such as 'concepts.md', "
            + "and a still goes in image as a .png, .jpg or .webp.");
    }

    private static readonly System.Text.RegularExpressions.Regex StillFile =
        new(@"\.(png|jpe?g|webp)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex MediaFile =
        new(@"\.(gif|bmp|svg|pdf|mp4|webm|mov|wav|mp3)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    #endregion
}
