namespace Polson.Tests.ExtendedMind;

using System;

using global::Polson.ExtendedMind;
using global::Polson.Tests;
using Polson.ExtendedMind.ImageGeneration;
using SkiaSharp;

using Xunit;

/// <summary>
/// Tests for the measurement and repair layer.
/// </summary>
/// <remarks>
/// These matter more than they look. Every guarantee the requisition API makes — that a swatch
/// tiles, that a plate left its quiet region alone, that a plate was inpainted rather than filled —
/// is a measurement taken here, because none of them can be obtained by asking the model nicely.
/// </remarks>
public class PlateAnalysisTests : TestsRuntime
{
    #region Tiling
    [Fact]
    public void MeasureTiling_DetectsAnImageThatDoesNotWrap()
    {
        // A left-to-right ramp is maximally discontinuous at the wrap: 255 meets 0.
        using var ramp = Generate(256, 256, (x, y) => new SKColor((byte)x, (byte)x, (byte)x));

        var metrics = PlateAnalysis.MeasureTiling(ramp);

        Assert.False(metrics.Wraps);
        Assert.True(metrics.VerticalSeamStep > metrics.NeighbourMax,
            $"seam {metrics.VerticalSeamStep:F2} should exceed the largest ordinary step {metrics.NeighbourMax:F2}");
    }

    [Fact]
    public void MeasureTiling_AcceptsAnImageThatDoesWrap()
    {
        // One full sine period across each axis is continuous across the wrap by construction.
        using var wave = Generate(256, 256, (x, y) =>
        {
            var v = (byte)(127 + (64 * Math.Sin(x * 2 * Math.PI / 256)) + (64 * Math.Sin(y * 2 * Math.PI / 256)));
            return new SKColor(v, v, v);
        });

        Assert.True(PlateAnalysis.MeasureTiling(wave).Wraps);
    }

    [Fact]
    public void MakeTileable_RepairsAnImageThatDoesNotWrap()
    {
        using var ramp = Generate(256, 256, (x, y) => new SKColor((byte)x, (byte)x, (byte)x));
        Assert.False(PlateAnalysis.MeasureTiling(ramp).Wraps);

        using var repaired = PlateAnalysis.MakeTileable(ramp);

        Assert.True(PlateAnalysis.MeasureTiling(repaired).Wraps);
        Assert.True(repaired.Width < ramp.Width, "repair crops by the blend overlap");
    }

    [Fact]
    public void MeasureTiling_IsNotFooledByAUniformTexture()
    {
        // The naive test — seam versus "unrelated" pixels — passes everything here, because on noise
        // unrelated columns differ as much as adjacent ones. The outlier test must still say wraps.
        var rng = new Random(4);
        using var noise = Generate(256, 256, (x, y) =>
        {
            var v = (byte)rng.Next(110, 140);
            return new SKColor(v, v, v);
        });

        var metrics = PlateAnalysis.MeasureTiling(noise);

        Assert.True(metrics.NeighbourMedian > 0, "uniform noise still has neighbour variation");
        Assert.True(metrics.Wraps);
    }
    #endregion

    #region Plate measurement
    [Fact]
    public void MeasurePlate_ReadsTheKeyLightFromTheBrightMass()
    {
        using var plate = Generate(300, 300, (x, y) =>
            x < 100 && y < 100 ? new SKColor(250, 250, 250) : new SKColor(4, 4, 8));

        var metrics = PlateAnalysis.MeasurePlate(plate, QuietRegion.None);

        Assert.InRange(metrics.KeyLightX, 0.0, 0.4);
        Assert.InRange(metrics.KeyLightY, 0.0, 0.4);
    }

    [Fact]
    public void MeasurePlate_VerifiesTheQuietRegionRatherThanAssumingIt()
    {
        using var honoured = Generate(300, 300, (x, y) =>
            y > 200 ? new SKColor(1, 1, 2) : new SKColor(120, 130, 180));
        using var ignored = Generate(300, 300, (x, y) =>
            y > 200 ? new SKColor(180, 170, 140) : new SKColor(120, 130, 180));

        Assert.True(PlateAnalysis.MeasurePlate(honoured, QuietRegion.LowerThird).QuietRegionHonoured);
        Assert.False(PlateAnalysis.MeasurePlate(ignored, QuietRegion.LowerThird).QuietRegionHonoured);
    }

    [Fact]
    public void MeasurePlate_ReportsBandLuminanceTopToBottom()
    {
        using var plate = Generate(300, 300, (x, y) => new SKColor((byte)(y * 255 / 300), (byte)(y * 255 / 300), (byte)(y * 255 / 300)));

        var bands = PlateAnalysis.MeasurePlate(plate, QuietRegion.None).BandLuminance;

        Assert.Equal(3, bands.Length);
        Assert.True(bands[0] < bands[1] && bands[1] < bands[2]);
    }

    [Fact]
    public void MeasurePlate_WithoutBlocking_NeverClaimsABakedMask()
    {
        // A plate can be legitimately dark over most of its area — an unconditioned "keep the lower
        // third black" plate measured 18% pure black — so darkness alone must not imply a mask.
        using var dark = Generate(300, 300, (x, y) => y > 100 ? SKColors.Black : new SKColor(90, 100, 140));

        Assert.False(PlateAnalysis.MeasurePlate(dark, QuietRegion.None).HasBakedMask);
    }
    #endregion

    #region Mask agreement
    [Fact]
    public void MaskAgreement_IsTotalForIdenticalSilhouettes()
    {
        using var a = Silhouette(300, 200);
        using var b = Silhouette(300, 200);

        Assert.True(PlateAnalysis.MaskAgreement(a, b) > PlateAnalysis.BakedMaskIoU);
    }

    [Fact]
    public void MaskAgreement_IsLowForUnrelatedSilhouettes()
    {
        using var blocking = Silhouette(300, 200);
        using var unrelated = Generate(300, 200, (x, y) => x < 40 ? SKColors.Black : new SKColor(160, 160, 160));

        Assert.True(PlateAnalysis.MaskAgreement(unrelated, blocking) < PlateAnalysis.BakedMaskIoU);
    }

    [Fact]
    public void MeasurePlate_WithBlocking_DetectsAnInpaintedPlate()
    {
        using var blocking = Silhouette(300, 200);
        using var inpainted = Silhouette(300, 200);   // the plate came back with the silhouette baked in

        Assert.True(PlateAnalysis.MeasurePlate(inpainted, QuietRegion.None, blocking).HasBakedMask);
    }
    #endregion

    #region Fitting and encoding
    [Fact]
    public void FitTo_CropsRatherThanStretches()
    {
        // A 2:1 source fitted to a square must keep the marker square. Stretching would widen it.
        using var source = Generate(200, 100, (x, y) =>
            x is >= 90 and < 110 && y is >= 40 and < 60 ? SKColors.White : SKColors.Black);

        using var fitted = PlateAnalysis.FitTo(source, 100, 100);

        Assert.Equal(100, fitted.Width);
        Assert.Equal(100, fitted.Height);

        var (w, h) = WhiteExtent(fitted);
        Assert.True(Math.Abs(w - h) <= 2, $"marker measured {w}x{h}; a stretch would distort it");
    }

    [Fact]
    public void FitTo_AlwaysReturnsTheRequestedSize()
    {
        using var source = Generate(1344, 768, (x, y) => new SKColor(30, 40, 60));   // a real plate's shape

        using var fitted = PlateAnalysis.FitTo(source, 1120, 640);

        Assert.Equal(1120, fitted.Width);
        Assert.Equal(640, fitted.Height);
    }

    [Fact]
    public void Encode_WebpUndercutsPngOnPhotographicContent()
    {
        // Content matters: on a synthetic pattern like (x ^ y), PNG's predictors win outright and
        // WebP is nearly twice the size. The claim only holds for the noisy, gradient-rich imagery
        // the service actually returns, which is why this generates that rather than a clean pattern.
        var rng = new Random(9);
        using var bitmap = Generate(512, 512, (x, y) =>
        {
            var v = Math.Clamp((int)(128 + (60 * Math.Sin(x / 40.0)) + (40 * Math.Cos(y / 33.0)) + rng.Next(-18, 18)), 0, 255);
            return new SKColor((byte)v, (byte)Math.Clamp(v + 12, 0, 255), (byte)Math.Clamp(v - 20, 0, 255));
        });

        var png = PlateAnalysis.Encode(bitmap, "png", 100);
        var webp = PlateAnalysis.Encode(bitmap, "webp", 85);

        Assert.True(webp.Length < png.Length, $"webp {webp.Length} should undercut png {png.Length}");
    }

    [Fact]
    public void Encode_ProducesDecodableOutputInEveryFormat()
    {
        using var bitmap = Generate(64, 64, (x, y) => new SKColor((byte)x, (byte)y, 90));

        foreach (var format in new[] { "png", "webp", "jpeg" })
        {
            using var round = SKBitmap.Decode(PlateAnalysis.Encode(bitmap, format, 90));
            Assert.NotNull(round);
            Assert.Equal(64, round!.Width);
        }
    }

    [Fact]
    public void MimeFor_MatchesTheEncoding()
    {
        Assert.Equal("image/webp", PlateAnalysis.MimeFor("webp"));
        Assert.Equal("image/png", PlateAnalysis.MimeFor("png"));
        Assert.Equal("image/jpeg", PlateAnalysis.MimeFor("jpeg"));
        Assert.Equal("image/webp", PlateAnalysis.MimeFor("something-unknown"));
    }

    [Fact]
    public void ToMatte_ProducesGreyAndHonoursInversion()
    {
        using var source = Generate(64, 64, (x, y) => new SKColor(200, 60, 30));

        using var matte = PlateAnalysis.ToMatte(source);
        using var inverted = PlateAnalysis.ToMatte(source, invert: true);

        var m = matte.GetPixel(10, 10);
        Assert.Equal(m.Red, m.Green);
        Assert.Equal(m.Green, m.Blue);
        Assert.Equal(255 - m.Red, inverted.GetPixel(10, 10).Red);
    }

    [Fact]
    public void Resize_ReturnsTheRequestedEdge()
    {
        using var source = Generate(1024, 1024, (x, y) => new SKColor(10, 20, 30));
        using var resized = PlateAnalysis.Resize(source, 256);

        Assert.Equal(256, resized.Width);
        Assert.Equal(256, resized.Height);
    }
    #endregion

    #region Helpers
    static SKBitmap Generate(int width, int height, Func<int, int, SKColor> f)
    {
        var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, f(x, y));
            }
        }

        return bitmap;
    }

    /// <summary>A blocky skyline on grey, matching the shape of a real blocking render.</summary>
    static SKBitmap Silhouette(int width, int height) => Generate(width, height, (x, y) =>
    {
        var top = height - 40 - (((x / 30) % 4) * 25);
        return y >= top ? SKColors.Black : new SKColor(128, 128, 128);
    });

    static (int Width, int Height) WhiteExtent(SKBitmap bitmap)
    {
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (PlateAnalysis.Luminance(bitmap.GetPixel(x, y)) <= 200)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < 0 ? (0, 0) : (maxX - minX + 1, maxY - minY + 1);
    }
    #endregion
}
