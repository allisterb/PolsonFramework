namespace Polson.Tests.Drawing;

using System;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// Regression tests for paint alpha leaking between fillStyle/strokeStyle assignments.
/// Found by the E2E agent harness: a starfield drawn with rgba() colours silently tinted the
/// sea gradient several layers later, because the stale alpha rode through on the paint colour.
/// </summary>
public class CanvasPaintAlphaTests : TestsRuntime
{
    #region Fields
    private const string Opaque = "#151F2E";
    #endregion

    #region Methods
    [Fact]
    public void TestGradientFillIsOpaqueOnCleanContext()
    {
        Assert.Equal("FF", FillAlphaAfter(null, useGradient: true));
    }

    [Theory]
    [InlineData("rgba(236,243,255,0.08)")]
    [InlineData("rgba(255,0,0,0.5)")]
    public void TestTranslucentColorDoesNotLeakIntoGradient(string translucent)
    {
        Assert.Equal("FF", FillAlphaAfter(translucent, useGradient: true));
    }

    [Theory]
    [InlineData("rgba(236,243,255,0.08)")]
    [InlineData("rgba(255,0,0,0.5)")]
    public void TestTranslucentColorDoesNotLeakIntoShader(string translucent)
    {
        Assert.Equal("FF", FillAlphaAfter(translucent, useGradient: false));
    }

    [Fact]
    public void TestTranslucentColorDoesNotLeakIntoStrokeGradient()
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        ctx.StrokeStyle = "rgba(236,243,255,0.08)";

        var grad = ctx.CreateLinearGradient(0, 0, 64, 0);
        grad.AddColorStop(0, Opaque);
        grad.AddColorStop(1, Opaque);
        ctx.StrokeStyle = grad;
        ctx.LineWidth = 40f;
        ctx.BeginPath();
        ctx.MoveTo(0, 32);
        ctx.LineTo(64, 32);
        ctx.Stroke();

        Assert.Equal("FF", AlphaAt(canvas, 32, 32));
    }

    /// <summary>An opaque colour after a translucent one already worked; keep it working.</summary>
    [Fact]
    public void TestOpaqueColorResetsAlpha()
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        ctx.FillStyle = "rgba(236,243,255,0.08)";
        ctx.FillStyle = Opaque;
        ctx.FillRect(0, 0, 64, 64);

        Assert.Equal("FF", AlphaAt(canvas, 32, 32));
    }

    /// <summary>globalAlpha must apply once to a gradient, not twice (0.5 → 0x80, not 0x40).</summary>
    [Fact]
    public void TestGlobalAlphaAppliesOnceToGradient()
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        ctx.GlobalAlpha = 0.5f;
        var grad = ctx.CreateLinearGradient(0, 0, 64, 0);
        grad.AddColorStop(0, Opaque);
        grad.AddColorStop(1, Opaque);
        ctx.FillStyle = grad;
        ctx.FillRect(0, 0, 64, 64);

        var alpha = Convert.ToInt32(AlphaAt(canvas, 32, 32), 16);
        Assert.InRange(alpha, 125, 131);
    }

    /// <summary>globalAlpha must still apply to a plain colour fill.</summary>
    [Fact]
    public void TestGlobalAlphaStillAppliesToColorFill()
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        ctx.GlobalAlpha = 0.5f;
        ctx.FillStyle = Opaque;
        ctx.FillRect(0, 0, 64, 64);

        var alpha = Convert.ToInt32(AlphaAt(canvas, 32, 32), 16);
        Assert.InRange(alpha, 125, 131);
    }

    private static string FillAlphaAfter(string? priorFillStyle, bool useGradient)
    {
        var canvas = new SkiaCanvas(64, 64);
        var ctx = canvas.GetContext("2d");

        if (priorFillStyle != null)
        {
            ctx.FillStyle = priorFillStyle;
        }

        if (useGradient)
        {
            var grad = ctx.CreateLinearGradient(0, 0, 64, 0);
            grad.AddColorStop(0, Opaque);
            grad.AddColorStop(1, Opaque);
            ctx.FillStyle = grad;
        }
        else
        {
            ctx.FillStyle = new SkiaApi().Shader.Linear(0, 0, 64, 0, new[] { Opaque, Opaque });
        }

        ctx.FillRect(0, 0, 64, 64);
        return AlphaAt(canvas, 32, 32);
    }

    /// <summary>The trailing AA byte of the bitmap's "#RRGGBBAA" pixel readback.</summary>
    private static string AlphaAt(SkiaCanvas canvas, int x, int y) =>
        canvas.ToBitmap().GetPixel(x, y)[^2..];
    #endregion
}
