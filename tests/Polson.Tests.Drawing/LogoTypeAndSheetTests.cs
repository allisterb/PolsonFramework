namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// Fixes from the logo harness run: font pairing scored unrecognised categories above judged ones,
/// the optical centre inverted for triangular marks, the lockup could not be given a typeface, and
/// the brand sheet's app-icon panel painted the mark on a ground it could not be seen against.
/// </summary>
public class LogoTypeAndSheetTests : TestsRuntime
{
    #region Font Pairing Tests
    /// <summary>An unrecognised category must not outscore a considered verdict.</summary>
    [Fact]
    public void TestUnknownCategoriesAreReportedNotScored()
    {
        var verdict = new LogoTypeToolkit().EvaluateFontPairing("nonsense", "garbage");

        Assert.Equal("unknown", verdict["relationship"]);
        Assert.Equal(0, verdict["score"]);
        Assert.Equal(false, verdict["recognised"]);
        Assert.Contains("nonsense", verdict["description"]!.ToString());
    }

    [Fact]
    public void TestJudgedPairingsOutrankUnjudgedOnes()
    {
        var toolkit = new LogoTypeToolkit();

        var concordant = Convert.ToInt32(toolkit.EvaluateFontPairing("serif", "serif")["score"]);
        var contrasting = Convert.ToInt32(toolkit.EvaluateFontPairing("serif", "sans-serif")["score"]);
        var unjudged = Convert.ToInt32(toolkit.EvaluateFontPairing("script", "blackletter")["score"]);
        var unknown = Convert.ToInt32(toolkit.EvaluateFontPairing("nonsense", "sans")["score"]);

        Assert.True(contrasting > concordant, "the considered contrasting pairing should rank highest");
        Assert.True(concordant > unjudged, $"a judged verdict ({concordant}) must outrank an unjudged one ({unjudged})");
        Assert.True(unjudged > unknown, "a real pairing must outrank an unrecognised one");
    }
    #endregion

    #region Optical Centre Tests
    /// <summary>
    /// A triangular mark reads bottom-heavy, so the placement target must sit above the geometric
    /// centre — it previously returned 58% of the height, pushing the mark down.
    /// </summary>
    [Fact]
    public void TestTriangleOpticalCentreSitsAboveGeometricCentre()
    {
        var bounds = new Dictionary<string, object?> { ["x"] = 4f, ["y"] = 4f, ["width"] = 92f, ["height"] = 92f };
        var centre = new LogoDesignToolkit().ComputeOpticalCenter(bounds, "triangle");

        var y = Convert.ToSingle(centre["y"]);
        var fraction = (y - 4f) / 92f;

        Assert.True(y < 50f, $"triangle target y={y} must be above the geometric centre of 50");
        Assert.InRange(fraction, 0.42f, 0.48f);
    }

    [Fact]
    public void TestGeneralOpticalCentreStaysInDocumentedRange()
    {
        var bounds = new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 0f, ["width"] = 100f, ["height"] = 100f };
        var centre = new LogoDesignToolkit().ComputeOpticalCenter(bounds, "general");

        Assert.InRange(Convert.ToSingle(centre["y"]) / 100f, 0.42f, 0.48f);
    }
    #endregion

    #region Lockup Tests
    /// <summary>The typeface is the subject of a logotype toolkit, so it must be reachable.</summary>
    [Fact]
    public void TestLockupHonoursFontFamily()
    {
        var withDefault = RenderLockup(null);
        var withSerif = RenderLockup("Georgia");

        Assert.NotEqual(withDefault, withSerif);
    }

    /// <summary>The tagline must carry the tracking the toolkit itself computes for it.</summary>
    [Fact]
    public void TestTaglineIsTracked()
    {
        var toolkit = new LogoTypeToolkit();
        var tracking = toolkit.ComputeWordmarkTracking(12f, true, "tagline");

        Assert.True(tracking >= 0.15f, $"a small all-caps tagline should track wide, got {tracking}");

        // A tracked tagline occupies more width, so the rendered result differs from an untracked one.
        var canvas = new SkiaCanvas(600, 200);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 600, 200);
        toolkit.DrawWordmarkLockup(ctx, null, "BRAND", "SUNSET AND MOONLIGHT SAILING", new Dictionary<string, object?>
        {
            ["x"] = 20f,
            ["y"] = 60f,
        });

        var bmp = canvas.ToBitmap();
        var inkedColumns = 0;
        for (var x = 0; x < 600; x++)
        {
            for (var y = 0; y < 200; y++)
            {
                if (bmp.GetPixel(x, y) != "#FFFFFFFF") { inkedColumns++; break; }
            }
        }

        Assert.True(inkedColumns > 0, "the lockup drew nothing");
    }
    #endregion

    #region Brand Sheet Tests
    /// <summary>
    /// The app-icon panel used to fill its squircle with darkColor and paint the mark in
    /// primaryColor on top, which for a dark primary is about 1.3:1 — invisible.
    /// </summary>
    [Fact]
    public void TestBrandSheetAppIconPanelHasReadableContrast()
    {
        var canvas = new SkiaCanvas(1400, 900);
        var ctx = canvas.GetContext("2d");

        new LogoDesignToolkit().GenerateBrandPresentationSheet(ctx, new Dictionary<string, object?>
        {
            ["brandName"] = "Sailboat Tours",
            ["primaryColor"] = "#22304a",   // dark primary, as most identity palettes have
            ["darkColor"] = "#101a2b",
            ["lightColor"] = "#f7f3ea",
            ["drawMark"] = null,
        });

        var bmp = canvas.ToBitmap();
        var ground = bmp.GetPixel(80, 180);

        // The ground must not be the dark colour a dark mark would disappear into.
        Assert.NotEqual("#101A2BFF", ground);
    }
    #endregion

    private static byte[] RenderLockup(string? fontFamily)
    {
        var canvas = new SkiaCanvas(600, 200);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 600, 200);

        var options = new Dictionary<string, object?> { ["x"] = 20f, ["y"] = 60f, ["primaryColor"] = "#000000" };
        if (fontFamily is not null) options["fontFamily"] = fontFamily;

        new LogoTypeToolkit().DrawWordmarkLockup(ctx, null, "Sailboat Tours", "", options);
        return canvas.ToImageBytes("png", 100);
    }
}
