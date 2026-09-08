namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// The raster-to-vector crossing.
/// <para>
/// These run the real <c>potrace</c> and are inert without it, rather than faking one. A tracer's
/// only interesting property is whether the shape that comes out is the shape that went in, and a
/// stub proves nothing about that.
/// </para>
/// <para>
/// <b>Inert, not skipped, and the difference is worth knowing.</b> The binary is deliberately not
/// committed — <c>.gitignore</c> excludes <c>bin/</c>, and potrace is GPL, so redistributing it is a
/// thing to decide rather than to do by accident. xunit 2.9.2 has no skip mechanism and this project
/// does not add packages casually, so a machine without potrace runs these to a green tick having
/// asserted nothing. <c>WhetherATracerIsPresentIsRecorded</c> is the one that always runs and says
/// which of the two happened, so a silent pass is at least a legible one.
/// </para>
/// </summary>
public class BitmapTracerTests : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    #endregion

    #region Constructors
    public BitmapTracerTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (private)
    /// <summary>Whether to go on, saying plainly when it does not.</summary>
    bool Ready()
    {
        if (BitmapTracer.Available) return true;
        output.WriteLine("NOT RUN: potrace was not found. Install it, or set Tools:Potrace.");
        return false;
    }

    /// <summary>A white disc on black — a matte's own convention, and a shape with known geometry.</summary>
    static SkiaBitmapWrapper Disc(int size = 128, float radius = 40f)
    {
        var bitmap = new SkiaBitmapWrapper(size, size);
        using var canvas = new SKCanvas(bitmap.Bitmap);
        canvas.Clear(SKColors.Black);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = false };
        canvas.DrawCircle(size / 2f, size / 2f, radius, paint);
        return bitmap;
    }

    static SkiaBitmapWrapper TwoDiscs(int size = 160)
    {
        var bitmap = new SkiaBitmapWrapper(size, size);
        using var canvas = new SKCanvas(bitmap.Bitmap);
        canvas.Clear(SKColors.Black);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = false };
        canvas.DrawCircle(40, 40, 24, paint);
        canvas.DrawCircle(120, 120, 24, paint);
        return bitmap;
    }
    #endregion

    #region Availability
    [Fact]
    public void WhetherATracerIsPresentIsRecorded()
    {
        // Always runs, and always asserts the two answers agree. Its real job is the output line:
        // it is how a green suite says whether the tracing tests examined anything.
        var available = BitmapTracer.Available;
        Assert.Equal(available, BitmapTracer.Executable is not null);
        output.WriteLine(available
            ? $"potrace: {BitmapTracer.Executable} ({BitmapTracer.Version()})"
            : "potrace: absent — every tracing assertion below was inert.");
    }

    [Fact]
    public void AnAbsentTracerIsReportedRatherThanGuessed()
    {
        var kept = BitmapTracer.ExecutableOverride;
        try
        {
            BitmapTracer.ExecutableOverride = Path.Combine(Path.GetTempPath(), "no-such-potrace.exe");

            Assert.False(BitmapTracer.Available);
            Assert.Null(BitmapTracer.Executable);
            Assert.Null(BitmapTracer.Version());
        }
        finally
        {
            BitmapTracer.ExecutableOverride = kept;
        }
    }

    [Fact]
    public void TracingWithoutATracerSaysWhatToInstall()
    {
        var kept = BitmapTracer.ExecutableOverride;
        try
        {
            BitmapTracer.ExecutableOverride = Path.Combine(Path.GetTempPath(), "no-such-potrace.exe");
            using var disc = Disc();

            var thrown = Assert.Throws<InvalidOperationException>(() => disc.Trace());

            // The message is the whole value of this failure: a missing external program is an
            // environment to fix, not a bug to report, and the reader needs the fix rather than a
            // stack trace.
            Assert.Contains("potrace", thrown.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Tools:Potrace", thrown.Message);
        }
        finally
        {
            BitmapTracer.ExecutableOverride = kept;
        }
    }
    #endregion

    #region Tracing
    [Fact]
    public void ADiscBecomesOneClosedContourWhereTheDiscWas()
    {
        if (!Ready()) return;
        using var disc = Disc();

        var traced = disc.Trace();

        Assert.Equal(1, Convert.ToInt32(traced["count"]));
        var d = (string)traced["d"];
        Assert.False(string.IsNullOrWhiteSpace(d));

        // **The check that matters: the geometry landed where the pixels were.** A tracer that
        // returned potrace's own space would give a box ten times too large and upside down, and the
        // path data would still look perfectly plausible in isolation.
        //
        // `TightBounds`, not `Bounds`. The latter is the bounding box of the *control points*, which
        // for a circle built from cubics sits well outside the curve and is not even symmetric —
        // measured here as 20.4,22 85x93 against a true 24,24 80x80. Asserting on it would have meant
        // loosening the tolerance until the test could no longer tell a correct trace from a shifted
        // one, which is the failure this test exists to catch.
        var path = SKPath.ParseSvgPathData(d);
        Assert.NotNull(path);
        var bounds = path!.TightBounds;
        output.WriteLine($"disc 128px r40 -> tight {bounds}");

        // The disc spans 24..104 with its centre at 64. Held to a pixel and a half.
        Assert.InRange(bounds.MidX, 62.5, 65.5);
        Assert.InRange(bounds.MidY, 62.5, 65.5);
        Assert.InRange(bounds.Width, 78, 81.5);
        Assert.InRange(bounds.Height, 78, 81.5);
    }

    [Fact]
    public void TheResultIsInTheCoordinatesOfTheBitmapThatWentIn()
    {
        if (!Ready()) return;
        using var disc = Disc(size: 200, radius: 60);

        var traced = disc.Trace();
        var bounds = SKPath.ParseSvgPathData((string)traced["d"])!.TightBounds;

        Assert.Equal(200, Convert.ToInt32(traced["width"]));
        Assert.Equal(200, Convert.ToInt32(traced["height"]));
        Assert.InRange(bounds.Left, 39, 42);        // the disc spans 40..160
        Assert.InRange(bounds.Right, 158, 161);
    }

    [Fact]
    public void SeparateShapesComeBackSeparatelyAndAlsoCombined()
    {
        if (!Ready()) return;
        using var two = TwoDiscs();

        var traced = two.Trace();
        var paths = (string[])traced["paths"];

        // potrace groups disjoint contours into one <path> element, so `count` is elements rather
        // than shapes. What must hold is that the combined bounds span both discs — a trace that
        // dropped one would still return a perfectly valid path.
        Assert.NotEmpty(paths);
        var bounds = SKPath.ParseSvgPathData((string)traced["d"])!.TightBounds;
        Assert.InRange(bounds.Left, 15, 17);        // discs at (40,40) and (120,120), r 24
        Assert.InRange(bounds.Right, 143, 145);
    }

    [Fact]
    public void TheSubjectIsTheLightToneByDefaultAndTheDarkOneOnRequest()
    {
        if (!Ready()) return;
        using var disc = Disc();

        var light = disc.Trace();
        var dark = disc.Trace(new Dictionary<string, object> { ["subject"] = "dark" });

        // The disc is white on black. Tracing the light tone gives the disc; tracing the dark tone
        // gives the ground it sits in, which spans the whole frame.
        var lit = SKPath.ParseSvgPathData((string)light["d"])!.TightBounds;
        var gnd = SKPath.ParseSvgPathData((string)dark["d"])!.TightBounds;

        Assert.InRange(lit.Width, 78, 81.5);        // the disc
        Assert.InRange(gnd.Width, 127, 128.5);      // the whole frame it sits in
    }

    [Fact]
    public void BilevelReportsHowCleanlySeparatedThePlateWas()
    {
        if (!Ready()) return;
        using var disc = Disc();

        // Drawn without antialiasing, so all but nothing sits between the extremes.
        Assert.True(Convert.ToDouble(disc.Trace()["bilevel"]) > 0.99);
    }

    [Fact]
    public void ARampIsTracedAndSaysItWasARamp()
    {
        if (!Ready()) return;

        using var ramp = new SkiaBitmapWrapper(128, 128);
        using (var canvas = new SKCanvas(ramp.Bitmap))
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(128, 0),
                new[] { SKColors.Black, SKColors.White }, null, SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, 128, 128, paint);
        }

        // Not refused — a caller may know what they are doing. But `bilevel` is what tells them a
        // single cut through a gradient is a guess, and it is far from the ~0.99 a stencil gives.
        Assert.True(Convert.ToDouble(ramp.Trace()["bilevel"]) < 0.3);
    }

    [Fact]
    public void AnExplicitThresholdMovesTheEdgeAndIsReportedBack()
    {
        if (!Ready()) return;

        using var ramp = new SkiaBitmapWrapper(128, 32);
        using (var canvas = new SKCanvas(ramp.Bitmap))
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(128, 0),
                new[] { SKColors.Black, SKColors.White }, null, SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, 128, 32, paint);
        }

        var low = ramp.Trace(new Dictionary<string, object> { ["threshold"] = 64 });
        var high = ramp.Trace(new Dictionary<string, object> { ["threshold"] = 192 });

        Assert.Equal(64, Convert.ToInt32(low["threshold"]));
        Assert.Equal(192, Convert.ToInt32(high["threshold"]));

        // A lower cut calls more of the ramp "light", so the traced region starts further left.
        var lowLeft = SKPath.ParseSvgPathData((string)low["d"])!.TightBounds.Left;
        var highLeft = SKPath.ParseSvgPathData((string)high["d"])!.TightBounds.Left;
        Assert.True(lowLeft < highLeft, $"cut 64 began at {lowLeft}, cut 192 at {highLeft}");
    }

    [Fact]
    public void AnEmptyPlateTracesToNothingRatherThanFailing()
    {
        if (!Ready()) return;

        using var blank = new SkiaBitmapWrapper(64, 64);
        using (var canvas = new SKCanvas(blank.Bitmap)) canvas.Clear(SKColors.Black);

        var traced = blank.Trace();

        // A generation that came back empty is a real outcome the matte docs warn about, and the
        // honest answer is an empty path rather than an exception.
        Assert.Equal(0, Convert.ToInt32(traced["count"]));
        Assert.True(string.IsNullOrEmpty((string)traced["d"]));
    }

    [Fact]
    public void DespecklingDropsWhatIsTooSmallToBeShape()
    {
        if (!Ready()) return;

        using var speckled = new SkiaBitmapWrapper(128, 128);
        using (var canvas = new SKCanvas(speckled.Bitmap))
        using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = false })
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawCircle(64, 64, 30, paint);
            for (var i = 0; i < 12; i++) canvas.DrawRect(4 + i * 9, 6, 1, 1, paint);
        }

        var kept = speckled.Trace(new Dictionary<string, object> { ["despeckle"] = 0 });
        var dropped = speckled.Trace(new Dictionary<string, object> { ["despeckle"] = 8 });

        Assert.True(Convert.ToInt32(kept["count"]) >= Convert.ToInt32(dropped["count"]));
        var bounds = SKPath.ParseSvgPathData((string)dropped["d"])!.TightBounds;
        Assert.InRange(bounds.Top, 33, 36);      // the speckles along the top row are gone
    }
    #endregion
}
