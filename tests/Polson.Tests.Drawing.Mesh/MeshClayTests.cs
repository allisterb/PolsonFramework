namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary><c>Mesh.draw(ctx, mesh, { clay })</c> — the surface in one colour, shaded flat by facet.</summary>
public class MeshClayTests : TestsRuntime
{
    /// <summary>A unit cube, centred on the origin.</summary>
    const string Cube = """
        v -1 -1 -1
        v  1 -1 -1
        v  1  1 -1
        v -1  1 -1
        v -1 -1  1
        v  1 -1  1
        v  1  1  1
        v -1  1  1
        f 1 3 2
        f 1 4 3
        f 5 6 7
        f 5 7 8
        f 1 2 6
        f 1 6 5
        f 4 8 7
        f 4 7 3
        f 1 5 8
        f 1 8 4
        f 2 3 7
        f 2 7 6
        """;

    static (SkiaCanvas Canvas, Dictionary<string, object?> Result) Draw(Dictionary<string, object?> options)
    {
        var canvas = new SkiaCanvas(200, 200);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#ff00ff";
        ctx.FillRect(0, 0, 200, 200);
        var mesh = new MeshToolkit().FromObj(Cube);
        var opt = new Dictionary<string, object?> { ["x"] = 100, ["y"] = 100, ["scale"] = 50, ["yawDeg"] = 35, ["pitchDeg"] = -25 };
        foreach (var (k, v) in options) opt[k] = v;
        return (canvas, new MeshToolkit().Draw(ctx, mesh, opt));
    }

    static SKColor At(SkiaCanvas c, int x, int y) => c.Bitmap.Bitmap.GetPixel(x, y);

    [Fact]
    public void TestClayFillsTheSurfaceInGrey()
    {
        var (canvas, result) = Draw(new() { ["clay"] = true });
        Assert.Equal(true, result["clay"]);
        Assert.Equal(false, result["textured"]);

        var centre = At(canvas, 100, 100);
        Assert.Equal(centre.Red, centre.Green);
        Assert.Equal(centre.Green, centre.Blue);
        Assert.InRange(centre.Red, 80, 235);
        Assert.Equal(new SKColor(255, 0, 255), At(canvas, 3, 3));   // the ground is left alone
    }

    /// <summary>Facets facing different ways shade differently, which is what makes the planes of a figure read.</summary>
    [Fact]
    public void TestFacetsAreShadedByWhichWayTheyFace()
    {
        var (canvas, _) = Draw(new() { ["clay"] = true });
        var tones = new HashSet<byte>();
        for (var y = 40; y < 160; y += 6)
            for (var x = 40; x < 160; x += 6)
            {
                var p = At(canvas, x, y);
                if (p.Red == p.Blue && p.Green == p.Red) tones.Add(p.Red);
            }
        Assert.True(tones.Count >= 3, $"only {tones.Count} tones on a cube showing three faces");
    }

    [Fact]
    public void TestAColourTintsTheClay()
    {
        var (canvas, _) = Draw(new() { ["clay"] = "#c8a078" });
        var centre = At(canvas, 100, 100);
        Assert.True(centre.Red > centre.Green && centre.Green > centre.Blue, $"got {centre}");
    }

    [Fact]
    public void TestClayWithWireframeOrABadValueIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Draw(new() { ["clay"] = true, ["wireframe"] = true }));
        Assert.Throws<ArgumentException>(() => Draw(new() { ["clay"] = 5 }));
    }

    [Fact]
    public void TestClayFalseDrawsAsBefore()
    {
        var (_, result) = Draw(new() { ["clay"] = false });
        Assert.Equal(false, result["clay"]);
    }
}
