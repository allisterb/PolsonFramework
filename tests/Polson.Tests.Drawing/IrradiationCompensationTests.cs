namespace Polson.Tests.Drawing;

using System;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A light shape on a dark ground looks larger than the same shape dark-on-light — the irradiation
/// illusion. The monochrome board used to draw its knockout panels at the positive's exact
/// dimensions, so it presented a pair that measures equal and visibly is not, and passed it.
/// <para>
/// These tests cover the correction: the pure computation, and the board that applies it. The
/// board's dark-on-light panels must stay untouched — over-correcting produces a knockout that
/// visibly under-reads, which is the more obvious of the two errors.
/// </para>
/// </summary>
public class IrradiationCompensationTests : TestsRuntime
{
    #region Constants
    private const int Width = 800;
    private const int Height = 600;
    #endregion

    #region Computation Tests
    /// <summary>Dark on light is the reference case and is never shrunk.</summary>
    [Theory]
    [InlineData("#111827", "#ffffff")]   // the positive panel
    [InlineData("#4b5563", "#e5e7eb")]   // the greyscale neutral panel
    [InlineData("#000000", "#ffffff")]
    public void TestDarkOnLightIsNotCompensated(string ink, string background)
    {
        var result = new LogoDesignToolkit().ComputeIrradiationCompensation(ink, background);

        Assert.False((bool)result["lighterOnDarker"]!);
        Assert.Equal(1f, Convert.ToSingle(result["scale"]));
    }

    /// <summary>Light on dark blooms, so it is drawn smaller.</summary>
    [Theory]
    [InlineData("#ffffff", "#111827")]   // the knockout panel
    [InlineData("#f8fafc", "#1e293b")]   // the app icon panel
    public void TestLightOnDarkIsCompensated(string ink, string background)
    {
        var result = new LogoDesignToolkit().ComputeIrradiationCompensation(ink, background);

        Assert.True((bool)result["lighterOnDarker"]!);
        Assert.InRange(Convert.ToSingle(result["scale"]), 0.98f, 0.999f);
    }

    /// <summary>
    /// The correction scales with contrast: a barely-lighter ink barely blooms, so shrinking it as
    /// hard as a full white-on-black knockout would introduce an error rather than remove one.
    /// </summary>
    [Fact]
    public void TestCompensationScalesWithContrast()
    {
        var toolkit = new LogoDesignToolkit();

        var extreme = Convert.ToSingle(toolkit.ComputeIrradiationCompensation("#ffffff", "#000000")["scale"]);
        var mild = Convert.ToSingle(toolkit.ComputeIrradiationCompensation("#9a9a9a", "#808080")["scale"]);

        Assert.True(extreme < mild, $"expected more shrink at higher contrast: {extreme} vs {mild}");
        Assert.True(mild < 1f);
    }

    /// <summary>Equal colours have no contrast to bloom across.</summary>
    [Fact]
    public void TestIdenticalColoursAreNotCompensated() =>
        Assert.Equal(1f, Convert.ToSingle(
            new LogoDesignToolkit().ComputeIrradiationCompensation("#808080", "#808080")["scale"]));

    /// <summary>
    /// The illusion has no single published magnitude, so the strength is a parameter and zero must
    /// genuinely defeat it — that is how an agent sees the uncorrected comparison.
    /// </summary>
    [Fact]
    public void TestZeroStrengthDisablesCompensation() =>
        Assert.Equal(1f, Convert.ToSingle(
            new LogoDesignToolkit().ComputeIrradiationCompensation("#ffffff", "#000000", 0f)["scale"]));

    /// <summary>The shrink can never exceed the requested strength, whatever the contrast.</summary>
    [Theory]
    [InlineData(0.015f)]
    [InlineData(0.05f)]
    public void TestCompensationIsBoundedByStrength(float strength)
    {
        var scale = Convert.ToSingle(
            new LogoDesignToolkit().ComputeIrradiationCompensation("#ffffff", "#000000", strength)["scale"]);

        Assert.InRange(scale, 1f - strength, 1f);
    }
    #endregion

    #region Board Integration Tests
    /// <summary>
    /// The board must actually apply the correction, not merely be able to compute it. Measured by
    /// area, because that is what the illusion distorts: the compensated knockout covers fewer
    /// pixels than the uncompensated one.
    /// </summary>
    [Fact]
    public void TestBoardShrinksTheKnockoutPanel()
    {
        var uncompensated = KnockoutArea(0f);
        var compensated = KnockoutArea(0.015f);

        Assert.True(compensated < uncompensated,
            $"knockout was not shrunk: {compensated} vs {uncompensated} light pixels");
    }

    /// <summary>
    /// Area falls as the square of a uniform scale. This pins the correction as a scale rather than
    /// an erosion or a crop, either of which would move the area a different amount.
    /// </summary>
    [Fact]
    public void TestKnockoutAreaFallsAsScaleSquared()
    {
        var scale = Convert.ToSingle(
            new LogoDesignToolkit().ComputeIrradiationCompensation("#ffffff", "#111827")["scale"]);

        var ratio = (float)KnockoutArea(0.015f) / KnockoutArea(0f);

        Assert.InRange(ratio, scale * scale - 0.01f, scale * scale + 0.01f);
    }

    /// <summary>
    /// A stronger setting must shrink further, so tuning it the way Bokhua describes — adjust until
    /// the pair looks equal — actually does something monotonic.
    /// </summary>
    [Fact]
    public void TestStrongerCompensationShrinksFurther() =>
        Assert.True(KnockoutArea(0.05f) < KnockoutArea(0.015f));

    /// <summary>
    /// The positive panel is the reference and must be byte-identical whatever the knockout does,
    /// or the board would be comparing two moving targets.
    /// </summary>
    [Fact]
    public void TestPositivePanelIsUnaffectedByStrength() =>
        Assert.Equal(PositiveArea(0f), PositiveArea(0.05f));
    #endregion

    #region Methods
    /// <summary>Light pixels in the knockout quadrant of a rendered board.</summary>
    private static int KnockoutArea(float strength) =>
        CountInQuadrant(strength, Width / 2, 0, "d[i] > 200 && d[i + 1] > 200 && d[i + 2] > 200");

    /// <summary>Dark pixels in the positive quadrant of a rendered board.</summary>
    private static int PositiveArea(float strength) =>
        CountInQuadrant(strength, 0, 0, "d[i] < 80 && d[i + 1] < 80 && d[i + 2] < 80");

    private static int CountInQuadrant(float strength, int x, int y, string predicate)
    {
        var result = new JsDrawingEngine().Execute($$"""
            const canvas = createCanvas({{Width}}, {{Height}});
            const ctx = canvas.getContext('2d');
            const drawMark = (c, size) => {
                const r = size / 2;
                c.fillStyle = '#2f7fd4';
                c.beginPath();
                c.arc(r, r, r, 0, Math.PI * 2);
                c.fill();
            };
            Logo.generateMonochromeTest(ctx, drawMark, {{Width}}, {{Height}}, {{strength.ToString(System.Globalization.CultureInfo.InvariantCulture)}});
            const d = ctx.getImageData({{x}}, {{y}}, {{Width / 2}}, {{Height / 2}}).data;
            let n = 0;
            for (let i = 0; i < d.length; i += 4) { if ({{predicate}}) n++; }
            log('count=' + n);
            exit('counted');
            """, Width, Height, null, "png", 100);

        Assert.True(result.Success, result.Error);

        foreach (var line in result.Logs)
        {
            var marker = line.IndexOf("count=", StringComparison.Ordinal);
            if (marker >= 0) return int.Parse(line[(marker + 6)..].Trim());
        }

        throw new InvalidOperationException("script did not report a count");
    }
    #endregion
}
