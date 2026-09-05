namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Polson.Drawing.Svg;

using SkiaSharp;

/// <summary>
/// Frame capture and animated encoding — the piece that turns a sequence of drawn states into one
/// moving artifact.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a spike.</b> It exists to prove one path end to end: a script drives its own timeline,
/// captures a frame per step, and saves an animated WebP. The shape of the authoring API above it is
/// still open, so nothing here should be treated as settled surface.
/// </para>
/// <para>
/// The timeline itself is deliberately <i>not</i> here. Snap's tweening decomposes into an easing
/// (a pure function of <c>0..1</c>, and <c>mina</c> already provides all nine) and a setter called
/// with the eased value — so a timeline is a few lines of JavaScript over calls that already exist,
/// and does not need engine support to be tried out.
/// </para>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class MotionToolkit : IDisposable
{
    #region Constructors
    public MotionToolkit(string? projectRoot = null) => this.projectRoot = projectRoot;
    #endregion

    #region Fields
    /// <summary>
    /// A ceiling on retained frames, because every one is an uncompressed bitmap.
    /// </summary>
    /// <remarks>
    /// 1200 frames of 1920×1080 is roughly 10 GB, which a sandbox should refuse rather than discover.
    /// The limit is on pixels rather than on frame count so a small board can hold many more frames
    /// than a large one — the resource that runs out is memory, not frames.
    /// </remarks>
    private const long MaxRetainedPixels = 600_000_000L;

    private readonly string? projectRoot;
    private readonly List<SKBitmap> frames = [];
    private long retainedPixels;
    #endregion

    #region Properties
    /// <summary>How many frames are held.</summary>
    public int Count => frames.Count;
    #endregion

    #region Methods
    /// <summary>
    /// A new seekable score.
    /// </summary>
    /// <remarks>
    /// Independent of the frame buffer above: a timeline decides what the scene looks like at a
    /// time, <see cref="Frame"/> keeps what it looked like. The usual loop seeks and captures.
    /// <para>
    /// <c>options</c> is <c>{ defaults?: { dur?: number, easing?: fn } }</c>. Read as a typed
    /// options object rather than a dictionary, for the reason given on
    /// <see cref="MotionEntryOptions"/>.
    /// </para>
    /// </remarks>
    public MotionTimeline Timeline(MotionTimelineOptions? options = null) =>
        new(options?.Defaults?.Dur ?? 500d, options?.Defaults?.Easing);

    /// <summary>
    /// Rasterises the current state of a paper, canvas or bitmap and keeps it as the next frame.
    /// </summary>
    /// <remarks>
    /// A <b>copy</b> is taken. A canvas is a live surface and a paper is a live document, so a caller
    /// that captured a reference and then drew the next state would otherwise find every frame
    /// showing the last one — the failure would be silent and the whole sequence would be wrong.
    /// </remarks>
    public int Frame(object? source, int? width = null, int? height = null)
    {
        var bitmap = Rasterise(source, width, height)
            ?? throw new ArgumentException(
                "Motion.frame(...) takes a Snap paper, a canvas, or a bitmap.", nameof(source));

        var pixels = (long)bitmap.Width * bitmap.Height;
        if (retainedPixels + pixels > MaxRetainedPixels)
        {
            bitmap.Dispose();
            throw new InvalidOperationException(
                $"Motion is holding {Count} frames ({retainedPixels / 1_000_000}M pixels) and this one would exceed the "
                + $"{MaxRetainedPixels / 1_000_000}M limit. Save what you have, call Motion.clear(), or draw a smaller board.");
        }

        if (frames.Count > 0 && (frames[0].Width != bitmap.Width || frames[0].Height != bitmap.Height))
        {
            // Both sizes are read *before* the bitmap is released. Composing the message afterwards
            // reads width and height off freed native memory, which does not throw — it takes the
            // whole process down, and an agent loses the run rather than the frame.
            var message =
                $"Every frame must be the same size. Frame 1 is {frames[0].Width}x{frames[0].Height}, this one is "
                + $"{bitmap.Width}x{bitmap.Height}. Pass the same width and height each time, or clear and start again.";
            bitmap.Dispose();
            throw new InvalidOperationException(message);
        }

        frames.Add(bitmap);
        retainedPixels += pixels;
        return frames.Count;
    }

    /// <summary>Discards every held frame.</summary>
    public void Clear()
    {
        foreach (var frame in frames) frame.Dispose();
        frames.Clear();
        retainedPixels = 0;
    }

    /// <summary>
    /// Encodes the held frames as one animated WebP and writes it, returning what it wrote.
    /// </summary>
    /// <remarks>
    /// <c>fps</c> sets each frame's duration; pass <c>frameMs</c> instead to state the duration
    /// directly. The frames are <b>kept</b> afterwards, so a caller can save the same sequence twice
    /// at different qualities without redrawing it — call <see cref="Clear"/> when done.
    /// </remarks>
    public Dictionary<string, object?> Save(string filePath, object? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (frames.Count == 0)
            throw new InvalidOperationException("Motion.save(...) has no frames. Call Motion.frame(...) first.");

        var opt = JsInterop.AsDict(options);
        var fps = Num(opt, "fps", 25f);
        var frameMs = Num(opt, "frameMs", fps > 0 ? 1000f / fps : 40f);
        var quality = Num(opt, "quality", 80f);
        var lossless = opt != null && opt.Contains("lossless") && Convert.ToBoolean(opt["lossless"]);

        if (frameMs <= 0) throw new ArgumentException("fps must be positive.", nameof(options));

        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Write to");
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var duration = TimeSpan.FromMilliseconds(frameMs);
        var encoderFrames = new SKWebpEncoderFrame[frames.Count];
        for (var i = 0; i < frames.Count; i++) encoderFrames[i] = new SKWebpEncoderFrame(frames[i], duration);

        var encoderOptions = new SKWebpEncoderOptions(
            lossless ? SKWebpEncoderCompression.Lossless : SKWebpEncoderCompression.Lossy,
            Math.Clamp(quality, 1f, 100f));

        using var data = SKWebpEncoder.EncodeAnimated(encoderFrames, encoderOptions)
            ?? throw new InvalidOperationException("Encoding the animated WebP produced no data.");

        var bytes = data.ToArray();
        File.WriteAllBytes(full, bytes);

        // What the file holds, read back from the file rather than assumed. The WebP encoder merges
        // consecutive pixel-identical frames and sums their durations — correct, and a real size
        // win, but it means the count handed in is not always the count stored. Reporting the
        // submitted number alone would be quietly wrong for any sequence that holds still.
        var stored = frames.Count;
        using (var codec = SKCodec.Create(new MemoryStream(bytes)))
        {
            if (codec != null) stored = codec.FrameCount;
        }

        return new Dictionary<string, object?>
        {
            ["path"] = filePath,
            ["frames"] = frames.Count,
            ["storedFrames"] = stored,
            ["merged"] = frames.Count - stored,
            ["width"] = frames[0].Width,
            ["height"] = frames[0].Height,
            ["frameMs"] = frameMs,
            ["durationMs"] = frameMs * frames.Count,
            ["bytes"] = (long)bytes.Length
        };
    }

    /// <summary>
    /// Tiles a selection of the held frames into one labelled image — the artifact to *look* at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An agent cannot watch a video.</b> It reads images, so a moving artifact is close to the
    /// worst possible thing to hand it for inspection: it can produce the file and still not perceive
    /// the motion. A contact sheet puts several instants in one image, which is a single read and,
    /// unlike a video, supports comparison — the eye and <c>bitmap.diff</c> both work across cells.
    /// </para>
    /// <para>
    /// Frames are chosen by <c>indices</c>, or evenly spaced when it is omitted. The first and last
    /// held frames are always included in the even spacing, because the ends of a movement are what
    /// a reader checks first — Studio Manual 24's extremes, arriving from a different direction.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> Sheet(string filePath, object? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (frames.Count == 0)
            throw new InvalidOperationException("Motion.sheet(...) has no frames. Call Motion.frame(...) first.");

        var opt = JsInterop.AsDict(options);
        var picked = SelectIndices(opt);
        var cols = Math.Max(1, (int)Num(opt, "cols", picked.Count <= 6 ? picked.Count : (int)MathF.Ceiling(MathF.Sqrt(picked.Count))));
        var rows = (int)MathF.Ceiling(picked.Count / (float)cols);
        var scale = Math.Clamp(Num(opt, "scale", 1f), 0.05f, 4f);
        var gap = Num(opt, "gap", 10f);
        var pad = Num(opt, "padding", 12f);
        var labels = opt == null || !opt.Contains("labels") || Convert.ToBoolean(opt["labels"]);
        var fps = Num(opt, "fps", 25f);
        var labelH = labels ? Num(opt, "labelHeight", 22f) : 0f;

        var cellW = MathF.Max(1f, frames[0].Width * scale);
        var cellH = MathF.Max(1f, frames[0].Height * scale);
        var sheetW = (int)MathF.Ceiling(cols * cellW + (cols + 1) * gap + pad * 2);
        var sheetH = (int)MathF.Ceiling(rows * (cellH + labelH) + (rows + 1) * gap + pad * 2);

        using var sheet = new SKBitmap(sheetW, sheetH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(SkiaColorParser.Parse(opt?["background"]?.ToString() ?? "#e4e0d4"));

        using var font = new SKFont(SKTypeface.FromFamilyName(opt?["fontFamily"]?.ToString() ?? "Georgia"),
            Math.Max(7f, labelH * 0.62f));
        using var ink = new SKPaint { Color = SkiaColorParser.Parse(opt?["labelColor"]?.ToString() ?? "#15151a"), IsAntialias = true };
        using var border = new SKPaint { Color = ink.Color, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };

        for (var i = 0; i < picked.Count; i++)
        {
            var frame = frames[picked[i]];
            var x = pad + (i % cols) * (cellW + gap) + gap;
            var y = pad + (i / cols) * (cellH + labelH + gap) + gap;

            canvas.DrawBitmap(frame,
                SKRect.Create(0, 0, frame.Width, frame.Height),
                SKRect.Create(x, y, cellW, cellH));
            canvas.DrawRect(SKRect.Create(x, y, cellW, cellH), border);

            if (!labels) continue;
            var ms = fps > 0 ? picked[i] * 1000f / fps : picked[i];
            canvas.DrawText(
                $"{picked[i]}  ·  {ms / 1000f:0.00}s",
                x, y + cellH + labelH * 0.72f, SKTextAlign.Left, font, ink);
        }

        var format = opt?["format"]?.ToString() ?? "png";
        var quality = (int)Num(opt, "quality", 92f);
        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Write to");
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var bytes = SkiaImageEncoder.Encode(sheet, format, quality);
        File.WriteAllBytes(full, bytes);

        return new Dictionary<string, object?>
        {
            ["path"] = filePath,
            ["cells"] = picked.Count,
            ["indices"] = picked.ConvertAll(i => (object?)i),
            ["cols"] = cols,
            ["rows"] = rows,
            ["width"] = sheetW,
            ["height"] = sheetH,
            ["bytes"] = (long)bytes.Length
        };
    }

    /// <summary>Which frames the sheet shows: named outright, or spread across what is held.</summary>
    private List<int> SelectIndices(IDictionary? opt)
    {
        var picked = new List<int>();

        if (opt != null && opt.Contains("indices") && opt["indices"] is IEnumerable given and not string)
        {
            foreach (var value in given)
            {
                var i = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                if (i < 0 || i >= frames.Count)
                    throw new ArgumentOutOfRangeException(nameof(opt),
                        $"Frame {i} was asked for but only 0..{frames.Count - 1} are held.");
                picked.Add(i);
            }
            if (picked.Count > 0) return picked;
        }

        var count = Math.Clamp((int)Num(opt, "count", 6f), 1, frames.Count);
        if (count == 1) { picked.Add(0); return picked; }

        // Inclusive of both ends: the extremes of a movement are what a reader checks first.
        for (var i = 0; i < count; i++)
            picked.Add((int)MathF.Round(i * (frames.Count - 1) / (float)(count - 1)));

        return picked;
    }

    public void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }

    private SKBitmap? Rasterise(object? source, int? width, int? height) => source switch
    {
        SnapPaper paper => SvgRenderPipeline.RenderToBitmap(
            paper.Document,
            width ?? (int)MathF.Ceiling(paper.Width),
            height ?? (int)MathF.Ceiling(paper.Height),
            SKColors.Transparent),
        SkiaCanvas canvas => Copy(canvas.SkBitmap, width, height),
        SkiaBitmapWrapper wrapper => Copy(wrapper.Bitmap, width, height),
        _ => null
    };

    /// <summary>A detached copy, resampled only when a size was actually asked for.</summary>
    private static SKBitmap Copy(SKBitmap source, int? width, int? height)
    {
        var w = width ?? source.Width;
        var h = height ?? source.Height;

        var copy = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(copy);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, SKRect.Create(0, 0, source.Width, source.Height), SKRect.Create(0, 0, w, h));
        return copy;
    }

    private static float Num(IDictionary? options, string key, float fallback) =>
        options != null && options.Contains(key) && options[key] != null
            ? Convert.ToSingle(options[key], CultureInfo.InvariantCulture)
            : fallback;
    #endregion
}
