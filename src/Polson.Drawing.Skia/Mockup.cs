// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

/// <summary>
/// A design shown where it will live — on a wall, in a frame, in a bus shelter, on a billboard, a phone, a screen, a
/// business card or a box — or pinned into four corners of a picture: Lupton's mock-up (<i>Graphic Design Thinking</i>,
/// pp. 136–138), drawn rather than photographed. <c>Stage.mockup</c>.
/// </summary>
/// <remarks>
/// <para>
/// A mock-up tests scale, shape, surface and hierarchy, and shows a client the work in use. Each drawn setting is a small
/// scene in millimetres seen by a pinhole camera, so the design keeps its proportions in perspective, and where a size
/// matters a figure 1.7 m tall stands beside it: a poster's real size is what the setting is for.
/// </para>
/// <para>
/// Every image carries a label saying it is a mock-up. A simulated placement shown as a photograph of a real one would
/// be a claim about the world.
/// </para>
/// </remarks>
public static class Mockup
{
    #region Types
    /// <summary>
    /// What to draw: a setting, the design's real size in millimetres (either side may be null), the output size, the
    /// label, and for the settings that take them a picture to pin into (<c>Onto</c>, with <c>Corners</c>) and the other
    /// faces of a box, the back of a card.
    /// </summary>
    public sealed record Spec(string Setting, double? WidthMm = null, double? HeightMm = null, int Width = 1600, int Height = 1000,
        string Label = DefaultLabel, SKBitmap? Onto = null, SKPoint[]? Corners = null, SKBitmap? Side = null, SKBitmap? Top = null,
        SKBitmap? Back = null, SKColor? Accent = null, bool Find = false);

    /// <summary>The picture, where the design landed (top-left, top-right, bottom-right, bottom-left), its size, and the
    /// scale at the design for a setting seen face on.</summary>
    public sealed record Result(SKBitmap Image, SKPoint[] Corners, double WidthMm, double HeightMm, double? PixelsPerMm, string[] Warnings);
    #endregion

    #region Constants
    /// <summary>The drawn settings.</summary>
    public static readonly string[] Settings = ["wall", "frame", "shelter", "billboard", "phone", "screen", "card", "box"];

    public const string DefaultLabel = "Mock-up — simulated placement";
    #endregion

    #region Methods
    /// <summary>Draws the mock-up. <paramref name="design"/> is the work, flat; it is not disposed.</summary>
    public static Result Render(SKBitmap design, Spec spec)
    {
        ArgumentNullException.ThrowIfNull(design);
        if (spec.Width is < 320 or > 4096 || spec.Height is < 240 or > 4096)
            throw new ArgumentException("Stage.mockup's width and height are 320–4096 and 240–4096 pixels.");
        if (string.IsNullOrWhiteSpace(spec.Label))
            throw new ArgumentException("Stage.mockup always says it is a mock-up: give a label, or leave it out for the default.");

        var warnings = new List<string>();
        var aspect = design.Width / (double)design.Height;
        var (wmm, hmm) = Size(spec, aspect, warnings);
        using var image = SKImage.FromBitmap(design);
        var output = new SKBitmap(new SKImageInfo(spec.Width, spec.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(output);
        var accent = spec.Accent ?? Lead(design);
        var (corners, ppmm) = spec.Setting switch
        {
            "onto" => Onto(canvas, image, spec, aspect, warnings),
            "wall" or "frame" => Wall(canvas, image, spec, wmm, hmm, spec.Setting == "frame"),
            "shelter" => Shelter(canvas, image, spec, wmm, hmm),
            "billboard" => Billboard(canvas, image, spec, wmm, hmm),
            "phone" => Device(canvas, image, spec, phone: true),
            "screen" => Device(canvas, image, spec, phone: false),
            "card" => Card(canvas, image, spec, wmm, hmm, accent),
            "box" => Box(canvas, image, spec, wmm, hmm, accent),
            _ => throw new ArgumentException($"Stage.mockup's setting is one of {string.Join(", ", Settings.Select(s => $"'{s}'"))}, or 'onto' a picture with corners; not '{spec.Setting}'."),
        };
        Label(canvas, spec);
        return new Result(output, corners, wmm, hmm, ppmm, [.. warnings]);
    }

    /// <summary>
    /// The perspective that maps the unit square onto <paramref name="quad"/> (top-left, top-right, bottom-right,
    /// bottom-left), after Heckbert's projective mapping. Null when the corners are in a line or fold through.
    /// </summary>
    public static SKMatrix? SquareToQuad(SKPoint[] quad)
    {
        if (quad.Length != 4) return null;
        double x0 = quad[0].X, y0 = quad[0].Y, x1 = quad[1].X, y1 = quad[1].Y, x2 = quad[2].X, y2 = quad[2].Y, x3 = quad[3].X, y3 = quad[3].Y;
        if (!Convex(quad)) return null;
        double dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3, dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
        double g = 0, h = 0;
        if (Math.Abs(dx3) > 1e-9 || Math.Abs(dy3) > 1e-9)
        {
            var den = dx1 * dy2 - dx2 * dy1;
            if (Math.Abs(den) < 1e-12) return null;
            g = (dx3 * dy2 - dx2 * dy3) / den;
            h = (dx1 * dy3 - dx3 * dy1) / den;
        }
        return new SKMatrix(
            (float)(x1 - x0 + g * x1), (float)(x3 - x0 + h * x3), (float)x0,
            (float)(y1 - y0 + g * y1), (float)(y3 - y0 + h * y3), (float)y0,
            (float)g, (float)h, 1f);
    }
    #endregion

    #region Settings
    /// <summary>
    /// Pinned into four corners of a picture: a plate built around a panel, a photograph of a wall. With
    /// <see cref="Spec.Find"/>, the corners are a hint and the blank panel near them is found and used instead.
    /// </summary>
    /// <remarks>
    /// Measured on 2026-10-25: a backdrop conditioned on a black panel quad put the shelter's end where the quad was, but
    /// its blank panel inset and narrower, about two thirds of the quad's width. Pinned to the quad, the poster floated
    /// over the scene; pinned to the panel the plate actually drew, it sat in it.
    /// </remarks>
    private static (SKPoint[], double?) Onto(SKCanvas canvas, SKImage design, Spec spec, double aspect, List<string> warnings)
    {
        if (spec.Onto is not { } plate || spec.Corners is not { Length: 4 } corners)
            throw new ArgumentException("Stage.mockup onto a picture needs onto (the picture) and corners: [topLeft, topRight, bottomRight, bottomLeft] in the output's pixels.");
        if (SquareToQuad(corners) is null) throw new ArgumentException("Stage.mockup's corners must make a quadrilateral that does not fold: top-left, top-right, bottom-right, bottom-left.");
        using var scaled = plate.Width == spec.Width && plate.Height == spec.Height ? null
            : plate.Resize(new SKImageInfo(spec.Width, spec.Height, SKColorType.Rgba8888, SKAlphaType.Premul), Sampling);
        var ground = scaled ?? plate;
        using var picture = SKImage.FromBitmap(ground);
        canvas.DrawImage(picture, 0, 0, Sampling);
        if (spec.Find)
        {
            if (FindPanel(ground, corners) is { } found)
            {
                corners = found;
                var across = (SKPoint.Distance(found[0], found[1]) + SKPoint.Distance(found[3], found[2])) / 2;
                var down = (SKPoint.Distance(found[0], found[3]) + SKPoint.Distance(found[1], found[2])) / 2;
                if (Math.Abs(across / down - aspect) / aspect > 0.15)
                    warnings.Add($"the panel found is about {across / down:0.##} wide to 1 tall and the design {aspect:0.##}: pinned, it is stretched. Ask for a panel of the design's shape, or check it by eye.");
            }
            else warnings.Add("no blank panel was found near the corners, so the design is pinned to them as given. Look at the picture: it may have drawn the panel elsewhere, or not blank.");
        }
        Pin(canvas, design, corners);
        return (corners, null);
    }

    /// <summary>
    /// The blank panel a plate drew near <paramref name="hint"/>: the largest connected region of its brightest pixels
    /// within the hint grown by a quarter, its corners the region's extremes along the diagonals. Null when there is
    /// nothing bright there or the region is too small or too ragged to be a panel.
    /// </summary>
    internal static SKPoint[]? FindPanel(SKBitmap plate, SKPoint[] hint)
    {
        var scale = Math.Min(1f, 800f / Math.Max(plate.Width, plate.Height));
        var (w, h) = ((int)(plate.Width * scale), (int)(plate.Height * scale));
        using var small = plate.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul), Sampling);
        if (small is null) return null;

        var (cx, cy) = (hint.Average(p => p.X) * scale, hint.Average(p => p.Y) * scale);
        var area = hint.Select(p => new SKPoint(cx + (p.X * scale - cx) * 1.25f, cy + (p.Y * scale - cy) * 1.25f)).ToArray();
        using var region = Path(area);
        var luma = new float[w * h];
        var inside = new bool[w * h];
        var brightest = 0f;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (!region.Contains(x + 0.5f, y + 0.5f)) continue;
                var c = small.GetPixel(x, y);
                var l = (0.2126f * c.Red + 0.7152f * c.Green + 0.0722f * c.Blue) / 255f;
                (luma[y * w + x], inside[y * w + x]) = (l, true);
                brightest = Math.Max(brightest, l);
            }
        if (brightest < 0.6f) return null;

        // Connected regions of the brightest pixels; the largest is the panel.
        var threshold = brightest * 0.85f;
        var label = new int[w * h];
        var (best, bestSize, next) = (0, 0, 1);
        var stack = new Stack<int>();
        for (var i = 0; i < w * h; i++)
        {
            if (!inside[i] || label[i] != 0 || luma[i] < threshold) continue;
            var size = 0;
            stack.Push(i);
            label[i] = next;
            while (stack.Count > 0)
            {
                var j = stack.Pop();
                size++;
                var (x, y) = (j % w, j / w);
                foreach (var k in new[] { x > 0 ? j - 1 : -1, x < w - 1 ? j + 1 : -1, y > 0 ? j - w : -1, y < h - 1 ? j + w : -1 })
                    if (k >= 0 && inside[k] && label[k] == 0 && luma[k] >= threshold) { label[k] = next; stack.Push(k); }
            }
            if (size > bestSize) (best, bestSize) = (next, size);
            next++;
        }
        var hintArea = Math.Abs(Area([.. hint.Select(p => new SKPoint(p.X * scale, p.Y * scale))]));
        if (bestSize < hintArea * 0.15f) return null;

        // Its corners: the extremes of x + y and x − y, which a quad in perspective keeps at its corners.
        var points = Enumerable.Range(0, w * h).Where(i => label[i] == best).Select(i => new SKPoint(i % w + 0.5f, i / w + 0.5f)).ToArray();
        SKPoint[] quad = [points.MinBy(p => p.X + p.Y), points.MaxBy(p => p.X - p.Y), points.MaxBy(p => p.X + p.Y), points.MinBy(p => p.X - p.Y)];
        // A panel fills its quad; a glow or a sky does not.
        if (!Convex(quad) || bestSize < Math.Abs(Area(quad)) * 0.8f) return null;
        return [.. quad.Select(p => new SKPoint(p.X / scale, p.Y / scale))];
    }

    /// <summary>A wall seen face on, a figure beside the design for scale; framed and matted for <c>frame</c>.</summary>
    private static (SKPoint[], double?) Wall(SKCanvas canvas, SKImage design, Spec spec, double wmm, double hmm, bool framed)
    {
        const double Eye = 1600, Depth = 4000;
        var cam = new Camera(0, Eye, 0, 0);
        var centreY = Math.Max(1500, hmm / 2 + 450);
        var x = -wmm * 0.2;
        var personX = x + wmm / 2 + 650;
        // Fit what a visitor standing back would see: the design, the figure, floor to a little above both. Any wider and
        // an A2 poster is a speck (1.9% of the picture, measured), which hides the scale it is drawn to show.
        var view = new[] { cam.Project(x - wmm * 0.5 - 450, -120, Depth), cam.Project(personX + 450, Math.Max(2000, centreY + hmm / 2 + 300), Depth) };
        var fit = Fit(view, spec.Width, spec.Height, 1.0f);

        var floorY = fit.MapPoint(cam.Project(0, 0, Depth)).Y;
        Vertical(canvas, 0, floorY, spec.Width, new SKColor(0xec, 0xe8, 0xe1), new SKColor(0xdb, 0xd5, 0xcb));
        Vertical(canvas, floorY, spec.Height, spec.Width, new SKColor(0xb8, 0xae, 0xa0), new SKColor(0x9f, 0x94, 0x85));
        using (var skirting = new SKPaint { Color = new SKColor(0xf6, 0xf3, 0xee), IsAntialias = true })
            canvas.DrawRect(0, floorY - (float)(fit.ScaleX * 90 / Depth), spec.Width, (float)(fit.ScaleX * 90 / Depth), skirting);

        var quad = Panel(cam, fit, x, centreY, Depth, wmm, hmm, 0);
        if (framed)
        {
            var mat = wmm * 0.08;
            var frame = Panel(cam, fit, x, centreY, Depth - 1, wmm + mat * 2 + 50, hmm + mat * 2 + 50, 0);
            Shadow(canvas, frame, 14, 0.30f);
            Fill(canvas, frame, new SKColor(0x1c, 0x1b, 0x1a));
            Fill(canvas, Panel(cam, fit, x, centreY, Depth - 2, wmm + mat * 2, hmm + mat * 2, 0), new SKColor(0xfb, 0xfa, 0xf7));
        }
        else Shadow(canvas, quad, 10, 0.25f);
        Pin(canvas, design, quad);
        // A light tone: the figure is there for scale, and a solid one made itself the subject of the picture.
        Person(canvas, cam, fit, personX, Depth - 200, new SKColor(0x8a, 0x84, 0x7a, 0x78));
        return (quad, fit.ScaleX / Depth);
    }

    /// <summary>A bus shelter's backlit panel at night, three-quarters on, a figure waiting beside it.</summary>
    private static (SKPoint[], double?) Shelter(SKCanvas canvas, SKImage design, Spec spec, double wmm, double hmm)
    {
        const double Eye = 1600, Depth = 5200, Yaw = 32;
        var cam = new Camera(0, Eye, 0, 0);
        var bottom = 450.0;
        var centreY = bottom + hmm / 2;
        var cx = -wmm * 0.35;
        var frameW = wmm + 120;
        var frameH = hmm + 120;
        var (r, n) = Axes(Yaw);
        var personX = cx + r.X * (frameW / 2 + 500);
        var personZ = Depth + r.Z * (frameW / 2 + 500) - 250;
        var view = new[]
        {
            cam.Project(cx - wmm * 1.4, -50, Depth), cam.Project(cx + wmm * 1.9, centreY + frameH / 2 + 900, Depth),
            cam.Project(personX + 600, 0, personZ),
        };
        var fit = Fit(view, spec.Width, spec.Height, 1.0f);

        var horizon = fit.MapPoint(cam.Project(0, Eye, 1e7)).Y;
        Vertical(canvas, 0, horizon, spec.Width, new SKColor(0x14, 0x19, 0x26), new SKColor(0x34, 0x3c, 0x52));
        Vertical(canvas, horizon, spec.Height, spec.Width, new SKColor(0x2b, 0x2c, 0x31), new SKColor(0x1a, 0x1b, 0x1f));
        // The kerb, a line across the pavement in front of the shelter.
        using (var kerb = new SKPaint { Color = new SKColor(0x4a, 0x4b, 0x50), StrokeWidth = 3, IsAntialias = true })
        {
            var a = fit.MapPoint(cam.Project(-30000, 0, 2600));
            var b = fit.MapPoint(cam.Project(30000, 0, 2600));
            canvas.DrawLine(a, b, kerb);
        }

        var steel = new SKColor(0x2d, 0x30, 0x36);
        // Posts at the frame's sides, ground to roof, and the roof slab running back from the top.
        var roofY = centreY + frameH / 2 + 260;
        foreach (var side in new[] { -1, 1 })
        {
            var px = cx + r.X * side * (frameW / 2 + 30);
            var pz = Depth + r.Z * side * (frameW / 2 + 30);
            Fill(canvas, Panel(cam, fit, px, roofY / 2, pz, 60, roofY, Yaw), steel);
        }
        var roof = new[]
        {
            Point(cam, fit, cx - r.X * (frameW / 2 + 120), roofY, Depth - r.Z * (frameW / 2 + 120)),
            Point(cam, fit, cx + r.X * (frameW / 2 + 120), roofY, Depth + r.Z * (frameW / 2 + 120)),
            Point(cam, fit, cx + r.X * (frameW / 2 + 120) - n.X * 1400, roofY, Depth + r.Z * (frameW / 2 + 120) - n.Z * 1400),
            Point(cam, fit, cx - r.X * (frameW / 2 + 120) - n.X * 1400, roofY, Depth - r.Z * (frameW / 2 + 120) - n.Z * 1400),
        };
        Fill(canvas, roof, new SKColor(0x3a, 0x3e, 0x46));

        var frame = Panel(cam, fit, cx, centreY, Depth, frameW, frameH, Yaw);
        Glow(canvas, frame, new SKColor(0xff, 0xf2, 0xd2, 0x70), 36);
        Fill(canvas, frame, steel);
        var quad = Panel(cam, fit, cx, centreY, Depth - 1, wmm, hmm, Yaw);
        Pin(canvas, design, quad);
        // Backlit: the panel a little brighter than the night around it.
        Fill(canvas, quad, new SKColor(0xff, 0xff, 0xff, 0x12));
        Person(canvas, cam, fit, personX, personZ, new SKColor(0x0c, 0x0e, 0x13, 0xf0));
        return (quad, null);
    }

    /// <summary>A billboard high on its posts by day, a little turned.</summary>
    private static (SKPoint[], double?) Billboard(SKCanvas canvas, SKImage design, Spec spec, double wmm, double hmm)
    {
        const double Eye = 1600, Depth = 26000, Yaw = 18;
        var cam = new Camera(0, Eye, 0, -7);
        var bottom = 4500.0;
        var centreY = bottom + hmm / 2;
        var view = new[] { cam.Project(-wmm * 1.1, 0, Depth), cam.Project(wmm * 1.1, centreY + hmm * 0.9, Depth) };
        var fit = Fit(view, spec.Width, spec.Height, 1.0f);

        var horizon = fit.MapPoint(cam.Project(0, Eye, 1e7)).Y;
        Vertical(canvas, 0, horizon, spec.Width, new SKColor(0x8f, 0xb8, 0xd6), new SKColor(0xe2, 0xec, 0xf2));
        Vertical(canvas, horizon, spec.Height, spec.Width, new SKColor(0xa7, 0xa3, 0x96), new SKColor(0x86, 0x82, 0x77));
        var (r, _) = Axes(Yaw);
        var steel = new SKColor(0x4b, 0x4e, 0x55);
        foreach (var side in new[] { -0.3, 0.3 })
            Fill(canvas, Panel(cam, fit, r.X * side * wmm, bottom / 2, Depth + r.Z * side * wmm + 300, 450, bottom, Yaw), steel);
        Fill(canvas, Panel(cam, fit, 0, centreY, Depth + 40, wmm + 300, hmm + 300, Yaw), new SKColor(0x3b, 0x3d, 0x42));
        var quad = Panel(cam, fit, 0, centreY, Depth, wmm, hmm, Yaw);
        Pin(canvas, design, quad);
        return (quad, null);
    }

    /// <summary>A phone or a screen, face on and slightly turned, its screen the design's shape.</summary>
    private static (SKPoint[], double?) Device(SKCanvas canvas, SKImage design, Spec spec, bool phone)
    {
        Vertical(canvas, 0, spec.Height, spec.Width, new SKColor(0xee, 0xea, 0xe3), new SKColor(0xd9, 0xd3, 0xc9));
        var aspect = design.Width / (float)design.Height;
        // The screen as large as the frame allows with room for the body.
        var maxW = spec.Width * (phone ? 0.62f : 0.76f);
        var maxH = spec.Height * (phone ? 0.78f : 0.70f);
        var (sw, sh) = aspect > maxW / maxH ? (maxW, maxW / aspect) : (maxH * aspect, maxH);
        var bezel = phone ? Math.Min(sw, sh) * 0.05f : Math.Min(sw, sh) * 0.022f;
        var cx = spec.Width / 2f;
        var cy = spec.Height / 2f - (phone ? 0 : spec.Height * 0.04f);
        var body = SKRect.Create(cx - sw / 2 - bezel, cy - sh / 2 - bezel, sw + bezel * 2, sh + bezel * 2);
        var radius = phone ? Math.Min(body.Width, body.Height) * 0.11f : bezel * 0.6f;

        canvas.Save();
        canvas.RotateDegrees(phone ? -6 : 0, cx, cy);
        using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 0x55), IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18) })
            canvas.DrawRoundRect(SKRect.Create(body.Left + 10, body.Top + 22, body.Width, body.Height), radius, radius, shadow);
        using (var shell = new SKPaint { Color = new SKColor(0x15, 0x15, 0x17), IsAntialias = true })
            canvas.DrawRoundRect(body, radius, radius, shell);
        if (!phone)
            using (var stand = new SKPaint { Color = new SKColor(0x22, 0x22, 0x25), IsAntialias = true })
            {
                canvas.DrawRect(SKRect.Create(cx - body.Width * 0.04f, body.Bottom, body.Width * 0.08f, spec.Height * 0.07f), stand);
                canvas.DrawRoundRect(SKRect.Create(cx - body.Width * 0.18f, body.Bottom + spec.Height * 0.07f, body.Width * 0.36f, spec.Height * 0.018f), 6, 6, stand);
            }
        var screen = SKRect.Create(cx - sw / 2, cy - sh / 2, sw, sh);
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(screen, phone ? radius * 0.75f : 0), SKClipOperation.Intersect, antialias: true);
        canvas.DrawImage(design, screen, Sampling);
        canvas.Restore();
        if (phone)
            using (var notch = new SKPaint { Color = new SKColor(0x15, 0x15, 0x17), IsAntialias = true })
            {
                var portrait = sh > sw;
                var pill = portrait ? SKRect.Create(cx - sw * 0.13f, screen.Top + bezel * 0.35f, sw * 0.26f, bezel * 0.75f)
                    : SKRect.Create(screen.Left + bezel * 0.35f, cy - sh * 0.13f, bezel * 0.75f, sh * 0.26f);
                canvas.DrawRoundRect(pill, bezel, bezel, notch);
            }
        var total = canvas.TotalMatrix;
        canvas.Restore();
        return ([total.MapPoint(screen.Left, screen.Top), total.MapPoint(screen.Right, screen.Top), total.MapPoint(screen.Right, screen.Bottom),
            total.MapPoint(screen.Left, screen.Bottom)], null);
    }

    /// <summary>Cards on a table, seen from above at an angle: the design on the front card, the back or the accent behind.</summary>
    private static (SKPoint[], double?) Card(SKCanvas canvas, SKImage design, Spec spec, double wmm, double hmm, SKColor accent)
    {
        var cam = new Camera(0, 520, 0, 52);
        var view = new[] { cam.Project(-wmm * 1.3, 0, 380), cam.Project(wmm * 1.5, 0, 380 + hmm * 2.4) };
        var fit = Fit(view, spec.Width, spec.Height, 0.86f);
        Vertical(canvas, 0, spec.Height, spec.Width, new SKColor(0xcf, 0xc2, 0xae), new SKColor(0xb4, 0xa5, 0x8d));

        var back = Flat(cam, fit, wmm * 0.42, 380 + hmm * 1.25, wmm, hmm, 14);
        Shadow(canvas, back, 10, 0.22f);
        if (spec.Back is { } b) { using var bi = SKImage.FromBitmap(b); Pin(canvas, bi, back); }
        else Fill(canvas, back, accent);
        var front = Flat(cam, fit, -wmm * 0.15, 380 + hmm * 0.75, wmm, hmm, -10);
        Shadow(canvas, front, 10, 0.25f);
        Pin(canvas, design, front);
        return (front, null);
    }

    /// <summary>A box on a table, three-quarters on and from a little above: the design on its front, the other faces given or in the accent.</summary>
    private static (SKPoint[], double?) Box(SKCanvas canvas, SKImage design, Spec spec, double wmm, double hmm, SKColor accent)
    {
        const double Yaw = -28;
        var depth = wmm * 0.42;
        var cam = new Camera(0, hmm * 1.9, 0, 20);
        var z = hmm * 3.2;
        var (r, n) = Axes(Yaw);
        // Corners of the front face, then the box's back, away from the camera along -n.
        Vec3 C(double u, double v, double d) => new(r.X * u - n.X * d, v, z + r.Z * u - n.Z * d);
        var corner = new Dictionary<string, Vec3>
        {
            ["ftl"] = C(-wmm / 2, hmm, 0), ["ftr"] = C(wmm / 2, hmm, 0), ["fbr"] = C(wmm / 2, 0, 0), ["fbl"] = C(-wmm / 2, 0, 0),
            ["btl"] = C(-wmm / 2, hmm, depth), ["btr"] = C(wmm / 2, hmm, depth), ["bbr"] = C(wmm / 2, 0, depth), ["bbl"] = C(-wmm / 2, 0, depth),
        };
        var projected = corner.ToDictionary(k => k.Key, k => cam.Project(k.Value.X, k.Value.Y, k.Value.Z));
        var fit = Fit([.. projected.Values], spec.Width, spec.Height, 0.62f);
        SKPoint P(string k) => fit.MapPoint(projected[k]);
        Vertical(canvas, 0, spec.Height, spec.Width, new SKColor(0xe9, 0xe4, 0xdb), new SKColor(0xcc, 0xc4, 0xb6));

        // The contact shadow: the base, blurred.
        Shadow(canvas, [P("fbl"), P("fbr"), P("bbr"), P("bbl")], 18, 0.35f);
        var front = new[] { P("ftl"), P("ftr"), P("fbr"), P("fbl") };
        var right = new[] { P("ftr"), P("btr"), P("bbr"), P("fbr") };
        var left = new[] { P("btl"), P("ftl"), P("fbl"), P("bbl") };
        var top = new[] { P("btl"), P("btr"), P("ftr"), P("ftl") };
        var facing = Area(front);
        foreach (var (face, image, shade) in new[] { (right, spec.Side, 0.22f), (left, spec.Side, 0.22f), (top, spec.Top, -0.10f) })
        {
            if (Math.Sign(Area(face)) != Math.Sign(facing)) continue;
            if (image is { } i) { using var fi = SKImage.FromBitmap(i); Pin(canvas, fi, face); }
            else Fill(canvas, face, accent);
            Fill(canvas, face, shade > 0 ? new SKColor(0, 0, 0, (byte)(shade * 255)) : new SKColor(255, 255, 255, (byte)(-shade * 255)));
        }
        Pin(canvas, design, front);
        return (front, null);
    }
    #endregion

    #region Drawing
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>The design drawn into a quad through its perspective.</summary>
    private static void Pin(SKCanvas canvas, SKImage image, SKPoint[] quad)
    {
        var map = SquareToQuad(quad) ?? throw new InvalidOperationException("Stage.mockup: the design's corners fold through each other.");
        canvas.Save();
        canvas.Concat(map.PreConcat(SKMatrix.CreateScale(1f / image.Width, 1f / image.Height)));
        canvas.DrawImage(image, 0, 0, Sampling);
        canvas.Restore();
    }

    private static void Fill(SKCanvas canvas, SKPoint[] quad, SKColor colour)
    {
        using var path = Path(quad);
        using var paint = new SKPaint { Color = colour, IsAntialias = true };
        canvas.DrawPath(path, paint);
    }

    private static void Shadow(SKCanvas canvas, SKPoint[] quad, float blur, float strength)
    {
        using var path = Path(quad.Select(p => new SKPoint(p.X + blur * 0.4f, p.Y + blur * 0.7f)).ToArray());
        using var paint = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(strength * 255)), IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blur) };
        canvas.DrawPath(path, paint);
    }

    private static void Glow(SKCanvas canvas, SKPoint[] quad, SKColor colour, float blur)
    {
        using var path = Path(quad);
        using var paint = new SKPaint { Color = colour, IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, blur) };
        canvas.DrawPath(path, paint);
    }

    private static void Vertical(SKCanvas canvas, float top, float bottom, float width, SKColor from, SKColor to)
    {
        if (bottom <= top) return;
        using var shader = SKShader.CreateLinearGradient(new SKPoint(0, top), new SKPoint(0, bottom), [from, to], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, top, width, bottom - top, paint);
    }

    /// <summary>A figure 1.7 m tall standing at <paramref name="x"/>, <paramref name="z"/>: head, shoulders, body, legs.</summary>
    private static void Person(SKCanvas canvas, Camera cam, SKMatrix fit, double x, double z, SKColor colour)
    {
        var foot = fit.MapPoint(cam.Project(x, 0, z));
        var head = fit.MapPoint(cam.Project(x, 1700, z));
        var h = foot.Y - head.Y;
        if (h <= 4) return;
        using var paint = new SKPaint { Color = colour, IsAntialias = true };
        var cx = foot.X;
        // One path, filled once: overlapping parts filled separately in a translucent colour darken where they meet.
        using var body = new SKPath();
        body.AddCircle(cx, head.Y + h * 0.065f, h * 0.065f);
        body.AddRoundRect(SKRect.Create(cx - h * 0.115f, head.Y + h * 0.15f, h * 0.23f, h * 0.06f), h * 0.03f, h * 0.03f);
        body.MoveTo(cx - h * 0.115f, head.Y + h * 0.17f);
        body.LineTo(cx + h * 0.115f, head.Y + h * 0.17f);
        body.LineTo(cx + h * 0.095f, head.Y + h * 0.56f);
        body.LineTo(cx + h * 0.07f, foot.Y);
        body.LineTo(cx + h * 0.01f, foot.Y);
        body.LineTo(cx, head.Y + h * 0.6f);
        body.LineTo(cx - h * 0.01f, foot.Y);
        body.LineTo(cx - h * 0.07f, foot.Y);
        body.LineTo(cx - h * 0.095f, head.Y + h * 0.56f);
        body.Close();
        canvas.DrawPath(body, paint);
    }

    /// <summary>The label every mock-up carries, bottom right, light on a dark setting and dark on a light one.</summary>
    private static void Label(SKCanvas canvas, Spec spec)
    {
        var dark = spec.Setting is "shelter";
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default, Math.Max(12, spec.Height / 70f));
        using var paint = new SKPaint { Color = dark ? new SKColor(0xd8, 0xd4, 0xcc, 0xd0) : new SKColor(0x5d, 0x58, 0x50, 0xd0), IsAntialias = true };
        TextShaper.DrawText(canvas, spec.Label.Trim(), spec.Width - spec.Height / 40f, spec.Height - spec.Height / 40f, SKTextAlign.Right, font, paint);
    }

    private static SKPath Path(SKPoint[] points)
    {
        var path = new SKPath();
        path.AddPoly(points, close: true);
        return path;
    }
    #endregion

    #region Geometry
    private readonly record struct Vec3(double X, double Y, double Z);

    /// <summary>A pinhole camera at (x, y, z), looking along +z and down by <c>pitch</c> degrees; y is up.</summary>
    private sealed class Camera(double x, double y, double z, double pitch)
    {
        private readonly double cos = Math.Cos(pitch * Math.PI / 180), sin = Math.Sin(pitch * Math.PI / 180);

        /// <summary>Onto the image plane at unit distance, y down.</summary>
        public SKPoint Project(double px, double py, double pz)
        {
            var (dx, dy, dz) = (px - x, py - y, pz - z);
            var up = dy * cos + dz * sin;
            var forward = -dy * sin + dz * cos;
            if (forward < 1e-6) forward = 1e-6;
            return new SKPoint((float)(dx / forward), (float)(-up / forward));
        }
    }

    /// <summary>The axes of a panel turned by <paramref name="yaw"/> degrees: across it, and its face toward the camera.</summary>
    private static (Vec3 Right, Vec3 Normal) Axes(double yaw)
    {
        var a = yaw * Math.PI / 180;
        return (new Vec3(Math.Cos(a), 0, Math.Sin(a)), new Vec3(Math.Sin(a), 0, -Math.Cos(a)));
    }

    /// <summary>An upright panel's corners, centred at (x, y, z), turned by <paramref name="yaw"/>, in output pixels.</summary>
    private static SKPoint[] Panel(Camera cam, SKMatrix fit, double x, double y, double z, double w, double h, double yaw)
    {
        var (r, _) = Axes(yaw);
        SKPoint At(double u, double v) => fit.MapPoint(cam.Project(x + r.X * u, y + v, z + r.Z * u));
        return [At(-w / 2, h / 2), At(w / 2, h / 2), At(w / 2, -h / 2), At(-w / 2, -h / 2)];
    }

    /// <summary>A panel lying flat on the ground (y = 0), its top edge away from the camera.</summary>
    private static SKPoint[] Flat(Camera cam, SKMatrix fit, double x, double z, double w, double h, double yaw)
    {
        var (r, n) = Axes(yaw);
        SKPoint At(double u, double v) => fit.MapPoint(cam.Project(x + r.X * u - n.X * v, 0, z + r.Z * u - n.Z * v));
        return [At(-w / 2, h / 2), At(w / 2, h / 2), At(w / 2, -h / 2), At(-w / 2, -h / 2)];
    }

    private static SKPoint Point(Camera cam, SKMatrix fit, double x, double y, double z) => fit.MapPoint(cam.Project(x, y, z));

    /// <summary>Scales and centres projected points into the output, filling <paramref name="share"/> of it.</summary>
    private static SKMatrix Fit(SKPoint[] points, int width, int height, float share)
    {
        var (minX, maxX, minY, maxY) = (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y), points.Max(p => p.Y));
        var s = Math.Min(width * share / Math.Max(1e-6f, maxX - minX), height * share / Math.Max(1e-6f, maxY - minY));
        var tx = width / 2f - s * (minX + maxX) / 2f;
        var ty = height / 2f - s * (minY + maxY) / 2f;
        return new SKMatrix(s, 0, tx, 0, s, ty, 0, 0, 1);
    }

    private static float Area(SKPoint[] q)
    {
        var a = 0f;
        for (var i = 0; i < q.Length; i++) a += q[i].X * q[(i + 1) % q.Length].Y - q[(i + 1) % q.Length].X * q[i].Y;
        return a / 2;
    }

    private static bool Convex(SKPoint[] q)
    {
        var sign = 0;
        for (var i = 0; i < 4; i++)
        {
            var (a, b, c) = (q[i], q[(i + 1) % 4], q[(i + 2) % 4]);
            var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (Math.Abs(cross) < 1e-6f) return false;
            var s = Math.Sign(cross);
            if (sign == 0) sign = s;
            else if (s != sign) return false;
        }
        return true;
    }
    #endregion

    #region Sizes
    /// <summary>
    /// The design's real size: as given (a missing side from the design's own shape), else the setting's usual one. A
    /// size whose shape is not the design's is warned about, and the design's shape is kept.
    /// </summary>
    private static (double W, double H) Size(Spec spec, double aspect, List<string> warnings)
    {
        if (spec.WidthMm is { } w && spec.HeightMm is { } h)
        {
            if (Math.Abs(w / h - aspect) / aspect > 0.03)
                warnings.Add($"the size given, {w:0} × {h:0} mm, is not the design's shape ({aspect:0.###} wide to 1 tall): the design is shown {h * aspect:0} × {h:0} mm.");
            return (h * aspect, h);
        }
        if (spec.WidthMm is { } ow) return (ow, ow / aspect);
        if (spec.HeightMm is { } oh) return (oh * aspect, oh);
        // The usual size of each, on the design's long side: A2, a six-sheet, a 48-sheet's height, a 55" screen, a card.
        var (longSide, byHeight) = spec.Setting switch
        {
            "wall" or "frame" => (594.0, aspect <= 1),
            "shelter" => (1750.0, aspect <= 1),
            "billboard" => (3048.0, true),
            "card" => (85.0, aspect < 1),
            "box" => (140.0, aspect <= 1),
            _ => (1000.0, aspect <= 1),
        };
        return byHeight ? (longSide * aspect, longSide) : (longSide, longSide / aspect);
    }

    /// <summary>The design's mean colour, for the faces a box or a card's back shows when nothing is given.</summary>
    private static SKColor Lead(SKBitmap design)
    {
        long r = 0, g = 0, b = 0, n = 0;
        var step = Math.Max(1, Math.Max(design.Width, design.Height) / 64);
        for (var y = 0; y < design.Height; y += step)
            for (var x = 0; x < design.Width; x += step)
            {
                var c = design.GetPixel(x, y);
                (r, g, b, n) = (r + c.Red, g + c.Green, b + c.Blue, n + 1);
            }
        return n == 0 ? new SKColor(0x88, 0x88, 0x88) : new SKColor((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }
    #endregion
}
