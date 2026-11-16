namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using Polson.Drawing.Svg;
using SkiaSharp;

/// <summary>Raster canvas surface — the root object a raster script draws onto.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SkiaCanvas : IDisposable, IDataUriSource, ILosslessImageSource
{
    #region Constructors
    public SkiaCanvas(int width = 800, int height = 600) : this(width, height, SKColorType.Rgba8888)
    {
    }

    /// <summary>A canvas of another pixel format: float, for arithmetic that must not clamp until the end.</summary>
    internal SkiaCanvas(int width, int height, SKColorType colorType)
    {
        ScriptLimits.CheckRaster(width, height, "A canvas");
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);

        var info = new SKImageInfo(Width, Height, colorType, SKAlphaType.Premul);
        SkBitmap = new SKBitmap(info);
        SkCanvas = new SKCanvas(SkBitmap);
        SkCanvas.Clear(SKColors.Transparent);
    }
    #endregion

    #region Properties

    public int Width { get; }
    public int Height { get; }
    public SKBitmap SkBitmap { get; }
    public SKCanvas SkCanvas { get; }

    /// <summary>
    /// The canvas's live backing bitmap, unlike <see cref="ToBitmap"/> which copies.
    /// </summary>
    /// <remarks>
    /// Wraps <see cref="SkBitmap"/> rather than returning it. Handing a script the raw
    /// <see cref="SKBitmap"/> put a SkiaSharp type on the JS surface, so <c>canvas.bitmap.getPixel()</c>
    /// reached SkiaSharp's own method and returned an <c>SKColor</c> struct — an object rather than a
    /// string, printing as <c>#AARRGGBB</c> instead of the documented <c>#RRGGBBAA</c> and never equal
    /// to anything under <c>===</c>. The wrapper is cached so the identity stays stable across reads.
    /// </remarks>
    public SkiaBitmapWrapper Bitmap => _liveBitmap ??= new SkiaBitmapWrapper(SkBitmap);
    #endregion

    #region Methods
    public CanvasRenderingContext2D GetContext(string contextType = "2d")
    {
        if (!string.Equals(contextType, "2d", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Context type '{contextType}' is not supported. Only '2d' is supported.");

        _context ??= new CanvasRenderingContext2D(this);
        return _context;
    }

    public void Clear(string? color = null)
    {
        var skColor = string.IsNullOrEmpty(color) ? SKColors.Transparent : SkiaColorParser.Parse(color);
        SkCanvas.Clear(skColor);
    }

    public SkiaBitmapWrapper ToBitmap() =>
        new(SkBitmap.Copy());

    public ImageData ToImageData() =>
        GetContext("2d").GetImageData(0, 0, Width, Height);

    public byte[] ToImageBytes(string format = "webp", int quality = 85) =>
        SkiaImageEncoder.Encode(SkBitmap, format, quality);

    public string ToDataUri(string format = "webp", int quality = 85)
    {
        var bytes = ToImageBytes(format, quality);
        return SkiaImageEncoder.ToDataUri(bytes, format);
    }

    /// <summary>Explicit: the optional-parameter overload above does not satisfy the interface, and
    /// explicit keeps it off the reflected public surface. See <see cref="IDataUriSource"/>.</summary>
    string IDataUriSource.ToDataUri() => ToDataUri();

    /// <summary>The pixels as PNG, for a caller that measures them. See <see cref="ILosslessImageSource"/>.</summary>
    byte[] ILosslessImageSource.ToLosslessBytes() => ToImageBytes("png", 100);

    public string ToDataURL(string format = "webp", int quality = 85) =>
        ToDataUri(format, quality);

    /// <summary>
    /// What <c>JSON.stringify</c> writes for a canvas: its size. Without it the stringify walked the CLR object into
    /// SkiaSharp's own and stopped at "Cyclic reference detected", so no result carrying a canvas could be logged whole
    /// (<c>Stage.mockup</c>'s <c>image</c>, nightshift4).
    /// </summary>
    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["width"] = Width,
        ["height"] = Height,
    };

    /// <summary>The layout audit on what this canvas drew: type on type, off the canvas, or across a shape's edge.</summary>
    /// <remarks>
    /// <b>Internal, not on the script surface.</b> The engine reports it with every render that returns this canvas, as it
    /// does for a paper (<see cref="LayoutAudit"/>), because the point is that the agent need not think to ask. Five of six
    /// logo mood boards were drawn with <c>ctx.fillText</c>; a check that read only SVG saw none of their collisions.
    /// </remarks>
    internal IReadOnlyList<string> AuditLayout() =>
        _layoutOverflowed ? [] : LayoutAudit.CheckMarks(_layoutLines, _layoutShapes, SKRect.Create(0, 0, Width, Height));

    /// <summary>
    /// Every line of text this canvas drew, in canvas pixels and in order: what <c>Verify.image</c> holds the read-back
    /// against. Kept apart from the layout record, which a canvas drawn a pixel at a time empties.
    /// </summary>
    internal IReadOnlyList<TypeLine> DrawnText => _drawnText;

    /// <summary>A line of text as drawn, in canvas pixels. Called by the context.</summary>
    internal void RecordText(string words, SKRect bounds, float size, bool upright)
    {
        var line = new TypeLine(words, bounds, size, upright, order: _marks++);
        if (_drawnText.Count < MaxDrawnText) _drawnText.Add(line);
        if (Room()) _layoutLines.Add(line);
    }

    /// <summary>A shape as drawn, in canvas pixels, numbered in drawing order so the audit knows what covers what. Called by the context.</summary>
    internal void RecordShape(LayoutShape shape)
    {
        var numbered = shape with { Order = _marks++ };
        if (Room()) _layoutShapes.Add(numbered);
    }

    /// <summary>
    /// Whether there is room for another mark. A canvas drawn a pixel or a cell at a time — noise, a texture, a pixel
    /// sheet — has no layout to audit, so past the cap the record stops and the audit says nothing rather than fifty
    /// thousand non-findings.
    /// </summary>
    private bool Room()
    {
        if (_layoutOverflowed) return false;
        if (_layoutLines.Count + _layoutShapes.Count < MaxLayoutMarks) return true;
        _layoutOverflowed = true;
        _layoutLines.Clear();
        _layoutShapes.Clear();
        return false;
    }

    public void Dispose()
    {
        SkCanvas.Dispose();
        SkBitmap.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Fields
    private const int MaxLayoutMarks = 4000;
    private const int MaxDrawnText = 4000;

    private CanvasRenderingContext2D? _context;
    private SkiaBitmapWrapper? _liveBitmap;
    private readonly List<TypeLine> _layoutLines = [];
    private readonly List<LayoutShape> _layoutShapes = [];
    private readonly List<TypeLine> _drawnText = [];
    private bool _layoutOverflowed;
    private int _marks;
    #endregion
}
