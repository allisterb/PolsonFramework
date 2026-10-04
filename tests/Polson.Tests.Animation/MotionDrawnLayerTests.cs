namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Linq;

using Polson.Animation;
using Polson.Drawing.Skia;

using SkiaSharp;

using Xunit;

/// <summary>
/// The drawn layer: a function the SDK draws with, called per frame with the values its nodes give at
/// that time. Tested with plain delegates, so the marshalling across the JS boundary is left to the
/// MCPServer tests.
/// </summary>
public class MotionDrawnLayerTests : TestsRuntime
{
    [Fact]
    public void TheFunctionDrawsWhereItsValuesSay()
    {
        var motion = new MotionToolkit();
        var comp = Composition(motion);
        comp.Drawn(Square(), Opts(("values", Opts(("x", motion.Nodes.Linear("real", 100, 20))))));

        Assert.Equal(25, CentreOfRed(comp, 0).X, 0);
        Assert.Equal(125, CentreOfRed(comp, 1).X, 0);
    }

    [Fact]
    public void AGroupsTransformAndClockReachIt()
    {
        var motion = new MotionToolkit();
        var comp = Composition(motion);
        var later = comp.Group(Opts(("offset", new[] { 100d, 50d }), ("timeOffset", -1d)));
        later.Drawn(Square(), Opts(("values", Opts(("x", motion.Nodes.Linear("real", 100, 0))))));

        // At t = 1 the group's clock reads 0, so x = 0, moved by the group to (100, 50).
        var c = CentreOfRed(comp, 1);
        Assert.Equal(105, c.X, 0);
        Assert.Equal(55, c.Y, 0);
    }

    [Fact]
    public void ResetTransformReturnsToTheLayersFrameNotTheCanvas()
    {
        var comp = Composition(new MotionToolkit());
        var g = comp.Group(Opts(("offset", new[] { 100d, 50d })));
        g.Drawn((ctx, v, t) =>
        {
            ctx.Translate(300, 300);
            ctx.ResetTransform();
            ctx.FillStyle = "#ff0000";
            ctx.FillRect(0, 0, 10, 10);
        });

        var c = CentreOfRed(comp, 0);
        Assert.Equal(105, c.X, 0);
        Assert.Equal(55, c.Y, 0);
    }

    [Fact]
    public void NothingTheFunctionDoesReachesTheNextLayer()
    {
        var comp = Composition(new MotionToolkit());
        comp.Drawn((ctx, v, t) =>
        {
            ctx.Save();
            ctx.Translate(200, 0);
            var clip = new CanvasPath();
            clip.Rect(0, 0, 1, 1);
            ctx.Clip(clip);                 // left saved and clipped on purpose
        });
        comp.Circle(Opts(("origin", new[] { 40d, 40d }), ("radius", 10d), ("color", "#ff0000")));

        var c = CentreOfRed(comp, 0);
        Assert.Equal(40, c.X, 0);
        Assert.Equal(40, c.Y, 0);
    }

    [Fact]
    public void ValuesArriveAsTheSdkReadsThem()
    {
        var motion = new MotionToolkit();
        var comp = Composition(motion);
        IDictionary<string, object?>? seen = null;
        comp.Drawn((ctx, v, t) => seen = (IDictionary<string, object?>)v!, Opts(("values", Opts(
            ("lean", 12d), ("at", new[] { 3d, 4d }), ("ink", "#15151a"),
            ("turn", motion.Nodes.Linear("angle", 90))))));

        comp.Render(0.5).Dispose();
        Assert.Equal(12d, seen!["lean"]);
        Assert.Equal(3d, ((IDictionary<string, object?>)seen["at"]!)["x"]);
        Assert.Equal("#15151AFF", seen["ink"]);
        Assert.Equal(45d, seen["turn"]);
    }

    [Fact]
    public void ItCannotBeWrittenAsSif()
    {
        var comp = Composition(new MotionToolkit());
        comp.Drawn((ctx, v, t) => { }, Opts(("desc", "figure")));

        var e = Assert.Throws<InvalidOperationException>(comp.ToSif);
        Assert.Contains("'figure'", e.Message);
    }

    [Theory]
    [InlineData("vals", "has no 'vals'")]
    [InlineData("values", "is a number, a point, a colour or a node")]
    public void AMistakeIsRefusedByName(string key, string expected)
    {
        var comp = Composition(new MotionToolkit());
        object? value = key == "values" ? Opts(("flag", true)) : 1d;
        var e = Assert.Throws<ArgumentException>(() => comp.Drawn((ctx, v, t) => { }, Opts((key, value))));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public void AMissingFunctionIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => Composition(new MotionToolkit()).Drawn(null));
        Assert.Contains("takes a function first", e.Message);
    }

    #region Private
    private static MotionComposition Composition(MotionToolkit motion) =>
        motion.Composition(Opts(("width", 400d), ("height", 200d), ("fps", 4d), ("duration", 1d)));

    private static Action<CanvasRenderingContext2D, object?, double> Square() => (ctx, v, t) =>
    {
        var x = (double)((IDictionary<string, object?>)v!)["x"]!;
        ctx.FillStyle = "#ff0000";
        ctx.FillRect((float)x, 0, 10, 10);
    };

    private static (double X, double Y) CentreOfRed(MotionComposition comp, double time)
    {
        using var frame = comp.Render(time);
        double sx = 0, sy = 0, n = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var c = frame.SkBitmap.GetPixel(x, y);
                if (c.Red < 200 || c.Alpha < 200 || c.Green > 60) continue;
                sx += x + 0.5;
                sy += y + 0.5;
                n++;
            }
        }

        Assert.True(n > 0, $"nothing red was drawn at {time}s");
        return (sx / n, sy / n);
    }

    private static Dictionary<string, object?> Opts(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
    #endregion
}
