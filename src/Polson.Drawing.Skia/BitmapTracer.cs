namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

/// <summary>Turns a bilevel bitmap into real path geometry, by way of <c>potrace</c>.</summary>
/// <remarks>
/// <para>
/// <b>The conversion the stack was missing.</b> A requisitioned matte is the one <i>form</i> the
/// studio can ask a model for, and it arrives as a raster — so on a vector deliverable it inlines as
/// base64 inside the SVG. It renders correctly and it is not vector: it cannot be scaled, filled per
/// region, subtracted from, or edited by a designer. Measured on two real mattes, tracing also makes
/// the deliverable dramatically smaller, because a contour is cheaper than the pixels describing it:
/// a 341 KB hedge-maze plate became <b>17 KB</b> of geometry, and a 119 KB motif became 4.7 KB.
/// Against the base64 form those rasters would have taken inside an SVG (+33.5%), that is ~26×.
/// </para>
/// <para>
/// <b>potrace is a separate program, not a library we link.</b> It is GPL-2-or-later, which may be
/// taken as GPL-3 and is then compatible with this project's AGPL-3.0 — but only linking would raise
/// the question at all, and running it as its own process is aggregation. Whoever redistributes the
/// binary carries potrace's own source offer. See the ledger row in <c>reference/README.md</c>.
/// </para>
/// <para>
/// Nothing is written to disk: the bitmap goes in over stdin as a PBM and the SVG comes back over
/// stdout. That keeps the sandbox's containment rules irrelevant to this path rather than merely
/// satisfied by it.
/// </para>
/// </remarks>
public static class BitmapTracer
{
    #region Fields
    //: Set by the host from `Tools:Potrace` when it is configured. The engine reads no environment
    //: variables by design, so an override arrives through settings exactly as `Assets:Budget` does.
    static string? _override;
    static string? _resolved;
    static bool _looked;
    #endregion

    #region Properties
    /// <summary>An explicit path to the binary, or null to discover one.</summary>
    public static string? ExecutableOverride
    {
        get => _override;
        set { _override = value; _looked = false; _resolved = null; }
    }

    /// <summary>The binary that will be used, or null when none was found.</summary>
    public static string? Executable
    {
        get
        {
            if (_looked) return _resolved;
            _looked = true;
            _resolved = Discover();
            return _resolved;
        }
    }

    /// <summary>Whether tracing can be done at all here.</summary>
    public static bool Available => Executable is not null;
    #endregion

    #region Methods
    /// <summary>The version string the binary reports, or null when there is no binary.</summary>
    public static string? Version()
    {
        if (Executable is not { } exe) return null;
        try
        {
            var info = Info(exe, "--version");
            using var process = Process.Start(info);
            if (process is null) return null;
            var line = process.StandardOutput.ReadLine();
            process.WaitForExit(5000);
            return line?.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Traces <paramref name="bitmap"/>, returning path data in <b>bitmap pixel</b> coordinates.</summary>
    /// <remarks>
    /// potrace emits its own coordinate space — tenths of a pixel, with the Y axis flipped, carried on
    /// a group transform rather than baked into the path data. Handing that back would give a caller a
    /// <c>d</c> string that draws upside down at ten times the size unless they knew to re-apply a
    /// transform nobody told them about, so the transform is applied here and the result is in the
    /// coordinates of the bitmap that went in.
    /// </remarks>
    public static Dictionary<string, object> Trace(SKBitmap bitmap, TraceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var opts = options ?? new TraceOptions();

        if (Executable is not { } exe)
            throw new InvalidOperationException(
                "Tracing needs the 'potrace' program and none was found. Install it (Debian: "
                + "`apt-get install potrace`), or set 'Tools:Potrace' in appsettings.json to its "
                + "full path. Check Skia.tracer.available before calling trace().");

        var (grey, bilevel) = Luminance(bitmap);
        var cut = opts.Threshold ?? 128;
        var pbm = Pbm(grey, bitmap.Width, bitmap.Height, cut, opts.SubjectIsLight);
        var svg = Run(exe, pbm, opts);
        var paths = Extract(svg, bitmap.Height);

        var combined = new SKPath { FillType = SKPathFillType.Winding };
        foreach (var d in paths)
            if (SKPath.ParseSvgPathData(d) is { } piece)
                combined.AddPath(piece);

        return new Dictionary<string, object>
        {
            ["d"] = combined.ToSvgPathData() ?? string.Empty,
            ["paths"] = paths,
            ["count"] = paths.Length,
            ["width"] = bitmap.Width,
            ["height"] = bitmap.Height,
            ["threshold"] = cut,
            // What share of the image sat at one extreme or the other. A stencil measures ~0.98; a
            // ramp measures far less, and on a ramp a fixed cut is a guess rather than a reading.
            // Reported rather than acted on, because which is wanted is the caller's business.
            ["bilevel"] = Math.Round(bilevel, 4)
        };
    }
    #endregion

    #region Methods (private)
    /// <summary>Settings first, then the copy checked into <c>bin/</c>, then the PATH.</summary>
    static string? Discover()
    {
        if (!string.IsNullOrWhiteSpace(_override))
            return File.Exists(_override) ? _override : null;

        // The Windows build is carried in `bin/` because there is no package manager to get it from;
        // on Debian it is one `apt-get` away and lands on the PATH, which is the container's case.
        foreach (var root in Roots())
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root, "potrace-*"))
            {
                var candidate = Path.Combine(dir, OperatingSystem.IsWindows() ? "potrace.exe" : "potrace");
                if (File.Exists(candidate)) return candidate;
            }
        }

        return OnPath(OperatingSystem.IsWindows() ? "potrace.exe" : "potrace");
    }

    static IEnumerable<string> Roots()
    {
        var here = AppContext.BaseDirectory;
        for (var dir = new DirectoryInfo(here); dir is not null; dir = dir.Parent)
            yield return Path.Combine(dir.FullName, "bin");
    }

    static string? OnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry is not this method's problem to report.
            }
        }
        return null;
    }

    static ProcessStartInfo Info(string exe, params string[] args)
    {
        var info = new ProcessStartInfo(exe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    /// <summary>Luminance per pixel, and what share of it sits at one extreme or the other.</summary>
    static (byte[] Grey, double Bilevel) Luminance(SKBitmap bitmap)
    {
        var grey = new byte[bitmap.Width * bitmap.Height];
        var extremes = 0;

        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var p = bitmap.GetPixel(x, y);
            // Rec. 709, as `Skia.Shader.luminance` uses, so a value read here and a value read there
            // mean the same thing.
            var v = (byte)((p.Red * 2126 + p.Green * 7152 + p.Blue * 722) / 10000);
            grey[y * bitmap.Width + x] = v;
            if (v <= 16 || v >= 239) extremes++;
        }

        return (grey, grey.Length == 0 ? 0 : (double)extremes / grey.Length);
    }

    /// <summary>A P4 bitmap. One bit per pixel, packed high bit first, each row byte-aligned.</summary>
    /// <remarks>
    /// Hand-written rather than encoded, because Skia ships no PBM or BMP encoder — <c>SkiaImageEncoder</c>
    /// maps png and jpeg and falls through to webp, which is the whole of what is available. In PBM a
    /// set bit is <b>black</b>, and potrace traces black, so the bit is set for whichever tone the
    /// caller called the subject.
    /// </remarks>
    static byte[] Pbm(byte[] grey, int width, int height, int cut, bool subjectIsLight)
    {
        var stride = (width + 7) / 8;
        var header = Encoding.ASCII.GetBytes($"P4\n{width} {height}\n");
        var bytes = new byte[header.Length + stride * height];
        Buffer.BlockCopy(header, 0, bytes, 0, header.Length);

        for (var y = 0; y < height; y++)
        {
            var row = header.Length + y * stride;
            for (var x = 0; x < width; x++)
            {
                var light = grey[y * width + x] >= cut;
                if (light != subjectIsLight) continue;           // background: leave the bit clear
                bytes[row + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }

        return bytes;
    }

    static string Run(string exe, byte[] pbm, TraceOptions opts)
    {
        var info = Info(exe,
            "-s",                                                // SVG backend
            "-o", "-",                                           // to stdout
            "--turdsize", opts.Despeckle.ToString(CultureInfo.InvariantCulture),
            "--alphamax", opts.Smoothness.ToString("0.###", CultureInfo.InvariantCulture),
            "--opttolerance", opts.Tolerance.ToString("0.###", CultureInfo.InvariantCulture));

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start {exe}.");

        // Written on a thread of its own: potrace streams its output while it reads, so filling the
        // input pipe and only then draining stdout deadlocks on a large plate.
        var writing = System.Threading.Tasks.Task.Run(() =>
        {
            using var input = process.StandardInput.BaseStream;
            input.Write(pbm, 0, pbm.Length);
        });

        var svg = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        writing.Wait(TimeSpan.FromSeconds(30));

        if (!process.WaitForExit(30_000))
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { /* already gone */ }
            throw new InvalidOperationException("Tracing timed out after 30s.");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Tracing failed ({process.ExitCode}): {error.Trim()}");

        return svg;
    }

    /// <summary>Every <c>d</c> in the output, moved out of potrace's space and into the bitmap's.</summary>
    static string[] Extract(string svg, int height)
    {
        var transform = TransformRegex.Match(svg);
        var matrix = SKMatrix.Identity;
        if (transform.Success)
        {
            var tx = Num(transform.Groups[1].Value);
            var ty = Num(transform.Groups[2].Value);
            var sx = Num(transform.Groups[3].Value);
            var sy = Num(transform.Groups[4].Value);
            // SVG composes outer-to-inner, so a point is scaled and then translated.
            matrix = new SKMatrix(sx, 0, tx, 0, sy, ty, 0, 0, 1);
        }
        else
        {
            // No group transform is not a shape we have seen from potrace 1.16, and guessing one
            // would silently move every contour. Identity leaves the data as it came.
            matrix = SKMatrix.Identity;
        }

        var found = new List<string>();
        foreach (Match match in PathRegex.Matches(svg))
        {
            var d = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(d)) continue;
            if (SKPath.ParseSvgPathData(d) is not { } path) continue;

            path.Transform(matrix);
            if (path.ToSvgPathData() is { Length: > 0 } moved) found.Add(moved);
        }

        return found.ToArray();
    }

    static float Num(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
    #endregion

    #region Fields (private)
    static readonly Regex TransformRegex = new(
        @"transform=""translate\(([-\d.eE]+)[, ]+([-\d.eE]+)\)\s*scale\(([-\d.eE]+)[, ]+([-\d.eE]+)\)""",
        RegexOptions.Compiled);

    static readonly Regex PathRegex = new(@"<path[^>]*\sd=""([^""]*)""", RegexOptions.Compiled);
    #endregion
}

/// <summary>What to trace, and how smoothly.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox through an options dictionary rather than as a constructed type,
/// which is the convention the other measurement calls follow.
/// </remarks>
public sealed class TraceOptions
{
    #region Properties
    /// <summary>Which tone is the subject. A matte is a white subject on black, so light by default.</summary>
    public bool SubjectIsLight { get; set; } = true;

    /// <summary>The black/white cut, 0-255. Null measures nothing and uses the midpoint.</summary>
    public int? Threshold { get; set; }

    /// <summary>Speckles up to this many pixels are dropped. potrace's <c>turdsize</c>.</summary>
    public int Despeckle { get; set; } = 2;

    /// <summary>Corner threshold, 0 to 1.334. Lower keeps more corners; potrace's <c>alphamax</c>.</summary>
    public double Smoothness { get; set; } = 1.0;

    /// <summary>Curve-fitting tolerance in pixels. potrace's <c>opttolerance</c>.</summary>
    public double Tolerance { get; set; } = 0.2;
    #endregion
}
