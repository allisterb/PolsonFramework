namespace Polson.Tests.Drawing;

using System.Collections.Generic;
using Polson.Drawing.Svg;
using Xunit;

/// <summary>
/// The vector armature helpers baked their guide colours into each child's inline style and took no
/// options, unlike their raster twins. An inline style beats an inherited presentation attribute,
/// so attr() on the returned group was silently ineffective and a construction plate came out in
/// the toolkit's indigo and amber whatever the brand palette was. Found by the logo harness run.
/// </summary>
public class VectorArmatureOptionsTests : TestsRuntime
{
    #region Methods
    [Fact]
    public void TestPolarGridHonoursLineColor()
    {
        var paper = Snap.Create(200, 200);
        paper.PolarGrid(100, 100, 80, 3, 6, Options("#9fb6c9"));

        var svg = paper.ToString();
        Assert.Contains("#9FB6C9", svg, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#6366F1", svg, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestGoldenCirclesHonoursLineColor()
    {
        var paper = Snap.Create(200, 200);
        paper.GoldenCircles(100, 100, 80, 3, Options("#9fb6c9"));

        var svg = paper.ToString();
        Assert.Contains("#9FB6C9", svg, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#F59E0B", svg, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestIsometricGridHonoursLineColor()
    {
        var paper = Snap.Create(200, 200);
        paper.IsometricGrid(200, 200, 40, Options("#9fb6c9"));

        var svg = paper.ToString();
        Assert.Contains("#9FB6C9", svg, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#38BDF8", svg, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestMonogramMatrixHonoursLineColor()
    {
        var paper = Snap.Create(200, 200);
        paper.MonogramMatrix(10, 10, 180, 180, "3x3", Options("#9fb6c9"));

        var svg = paper.ToString();
        Assert.Contains("#9FB6C9", svg, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#94A3B8", svg, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestClearSpaceGuideHonoursLineColor()
    {
        var paper = Snap.Create(200, 200);
        paper.ClearSpaceGuide(40, 40, 120, 120, 20, Options("#9fb6c9"));

        var svg = paper.ToString();
        Assert.Contains("#9FB6C9", svg, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#3B82F6", svg, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Omitting options must leave the existing defaults exactly as they were.</summary>
    [Fact]
    public void TestDefaultsAreUnchangedWhenNoOptionsGiven()
    {
        var paper = Snap.Create(200, 200);
        paper.PolarGrid(100, 100, 80, 3, 6);

        Assert.Contains("#6366F1", paper.ToString(), System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestLineWidthIsHonoured()
    {
        var paper = Snap.Create(200, 200);
        paper.PolarGrid(100, 100, 80, 2, 4, new Dictionary<string, object?>
        {
            ["lineColor"] = "#111111",
            ["lineWidth"] = 3.5f
        });

        Assert.Contains("3.5", paper.ToString());
    }
    #endregion

    private static Dictionary<string, object?> Options(string lineColor) =>
        new() { ["lineColor"] = lineColor };
}
