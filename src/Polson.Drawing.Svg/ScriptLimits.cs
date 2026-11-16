// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Svg;

using System;
using Svg;

/// <summary>
/// What a script may not make the host do, whoever wrote it: read a file or a URL through an SVG reference, or allocate a
/// raster large enough to take the machine's memory.
/// </summary>
/// <remarks>
/// <para>
/// <b>External references.</b> The Svg library fetches an <c>&lt;image&gt;</c> or <c>&lt;use&gt;</c> href itself — a
/// <c>file://</c> path anywhere on disk, or an <c>http(s)://</c> URL — and never asks the project's path containment.
/// Measured on 2026-11-15: an image href to a PNG outside the project rendered its pixels, which a script can then read
/// back. Its switches are process-wide statics, so <see cref="Ensure"/> sets them once, and every entry point that
/// parses or renders markup calls it. A picture goes into a document as a data URI (<c>paper.image(bitmap, ...)</c>),
/// which this leaves alone.
/// </para>
/// <para>
/// <b>Raster size.</b> A canvas was bounded below and not above, so <c>createCanvas(100000, 100000)</c> asked Skia for
/// 40 GB. <see cref="CheckRaster"/> refuses anything over <see cref="MaxSide"/> a side or <see cref="MaxPixels"/> in all.
/// It is a per-raster cap: a script that makes many large canvases is still bounded only by the time limit.
/// </para>
/// </remarks>
public static class ScriptLimits
{
    #region Constructors
    static ScriptLimits()
    {
        SvgDocument.ResolveExternalImages = ExternalType.None;
        SvgDocument.ResolveExternalElements = ExternalType.None;
        SvgDocument.ResolveExternalXmlEntites = ExternalType.None;
    }
    #endregion

    #region Constants
    /// <summary>The longest side of a raster, in pixels: Skia's own ceiling.</summary>
    public const int MaxSide = 32_767;

    /// <summary>The most pixels in one raster: 2^28, a gigabyte at four bytes a pixel. A0 at 300 dpi fits.</summary>
    public const long MaxPixels = 1L << 28;
    #endregion

    #region Methods
    /// <summary>Turns off the Svg library's fetching of external images, elements and entities. Idempotent and cheap.</summary>
    public static void Ensure() { }

    /// <summary>Refuses a raster too large to allocate safely, naming what asked for it.</summary>
    public static void CheckRaster(long width, long height, string what)
    {
        if (width <= MaxSide && height <= MaxSide && width * height <= MaxPixels) return;

        throw new ArgumentOutOfRangeException(nameof(width),
            $"{what} of {width:N0} × {height:N0} pixels is larger than this engine will allocate: at most {MaxSide:N0} " +
            $"a side and {MaxPixels:N0} pixels in all. Draw at a smaller size and scale the output, or split the work into tiles.");
    }
    #endregion
}
