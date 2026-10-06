namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// Hamm's eye (<i>Drawing the Head and Figure</i>, pp. 7–9) in <c>drawComicEye</c>, and drawing the face features in
/// a medium: <c>medium</c>, <c>drawTone</c> and <c>createEyeSocket</c>.
/// </summary>
public class HammEyeTests : TestsRuntime
{
    static readonly ConstructiveDrawingToolkit Toolkit = new();
    static readonly SkiaBrushApi Brushes = new();

    static Dictionary<string, object?> Head() => Toolkit.CreateLoomisHead(400f, 400f, 700f, 0f);

    static Dictionary<string, object?> Eye(Dictionary<string, object?> head) => (Dictionary<string, object?>)head["nearEye"]!;

    static Dictionary<string, object?> Draw(Dictionary<string, object?>? options) =>
        Toolkit.DrawComicEye(new SkiaCanvas(800, 800).GetContext("2d"), Eye(Head()), false, options);

    static float P(Dictionary<string, object?> eye, string key, string axis) => Convert.ToSingle(((Dictionary<string, object?>)eye[key]!)[axis]);

    [Fact]
    public void TestDetailZeroIsThePlainEye()
    {
        var plain = Draw(new() { ["detail"] = 0f });
        foreach (var key in new[] { "fold", "lowerRim", "innerCorner", "lashes" })
            Assert.True(((CanvasPath)plain[key]!).Path.IsEmpty, $"{key} should be empty at detail 0");

        var hamm = Draw(null);
        foreach (var key in new[] { "fold", "lowerRim", "innerCorner" })
            Assert.False(((CanvasPath)hamm[key]!).Path.IsEmpty, $"{key} should be drawn by default");
        Assert.True(((CanvasPath)hamm["lashes"]!).Path.IsEmpty, "lashes are opt-in");
    }

    [Fact]
    public void TestTheFoldIsAStripJustAboveTheLid()
    {
        var parts = Draw(null);
        var lid = ((CanvasPath)parts["upperLid"]!).Path.TightBounds;
        var fold = ((CanvasPath)parts["fold"]!).Path.TightBounds;
        var w = MathF.Abs(P(Eye(Head()), "outer", "x") - P(Eye(Head()), "inner", "x"));
        Assert.True(fold.Top < lid.Top, "the fold sits above the lid");
        Assert.True(lid.Top - fold.Top < w * 0.25f, $"the fold is a strip, {lid.Top - fold.Top:0.0}px above a {w:0}px eye");
    }

    [Fact]
    public void TestNoLashesGrowAtTheInnerCorner()
    {
        var eye = Eye(Head());
        float inner = P(eye, "inner", "x"), outer = P(eye, "outer", "x");
        var lashes = ((CanvasPath)Draw(new() { ["lashes"] = 1f })["lashes"]!).Path.TightBounds;
        Assert.False(lashes.IsEmpty);
        // The near eye runs left to right, so its inner half is the left half.
        Assert.True(lashes.Left > inner + (outer - inner) * 0.4f, $"lashes start at {lashes.Left}, inner corner {inner}");
    }

    [Fact]
    public void TestAMediumMustBeABrush()
    {
        var ex = Assert.Throws<ArgumentException>(() => Draw(new() { ["medium"] = "pencil" }));
        Assert.Contains("Skia.Brush", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestAPencilBrowIsGrainedNotSolid()
    {
        var head = Head();
        string Darkest(Dictionary<string, object?>? options)
        {
            var canvas = new SkiaCanvas(800, 800);
            var ctx = canvas.GetContext("2d");
            ctx.FillStyle = "#ffffff";
            ctx.FillRect(0, 0, 800, 800);
            Toolkit.DrawComicBrow(ctx, head["nearBrow"]!, false, options);
            var brow = (Dictionary<string, object?>)head["nearBrow"]!;
            float x = (Convert.ToSingle(((Dictionary<string, object?>)brow["inner"]!)["x"]) + Convert.ToSingle(((Dictionary<string, object?>)brow["peak"]!)["x"])) / 2f;
            float y = Convert.ToSingle(((Dictionary<string, object?>)brow["peak"]!)["y"]);
            var bmp = canvas.Bitmap;
            var light = 0;
            for (var dx = -6; dx <= 6; dx++)
                for (var dy = -2; dy <= 2; dy++)
                    if (bmp.GetPixel((int)x + dx, (int)y + 4 + dy) is { } c && c.StartsWith("#F", StringComparison.OrdinalIgnoreCase)) light++;
            return light.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.Equal("0", Darkest(null));                                                            // solid ink
        Assert.NotEqual("0", Darkest(new() { ["medium"] = Brushes.Pencil("#2e2e2e", 2f, 1f, 7) }));  // gaps in the grain
    }

    static Dictionary<string, object?> Brow(Dictionary<string, object?> head, Dictionary<string, object?>? options) =>
        Toolkit.DrawComicBrow(new SkiaCanvas(800, 800).GetContext("2d"), head["nearBrow"]!, false, options);

    static int Contours(CanvasPath path)
    {
        using var measure = new SkiaSharp.SKPathMeasure(path.Path, false);
        var n = 0;
        do if (measure.Length > 0f) n++; while (measure.NextContour());
        return n;
    }

    [Fact]
    public void TestHairsAreOptInAndScaleWithTheSetting()
    {
        var head = Head();
        Assert.True(((CanvasPath)Brow(head, null)["hairs"]!).Path.IsEmpty);
        var few = Contours((CanvasPath)Brow(head, new() { ["hairs"] = 0.4f })["hairs"]!);
        var many = Contours((CanvasPath)Brow(head, new() { ["hairs"] = 1f })["hairs"]!);
        Assert.True(many > few && few > 0, $"{few} hairs at 0.4, {many} at 1");
    }

    [Fact]
    public void TestTheSameHairsRideAMovedBrow()
    {
        // Placed in the brow's own frame from a fixed seed: the same render twice, and the same number of hairs
        // when an expression moves the brow, so a sequence does not reshuffle them frame to frame.
        var head = Head();
        var a = ((CanvasPath)Brow(head, new() { ["hairs"] = 1f })["hairs"]!).Path.ToSvgPathData();
        var b = ((CanvasPath)Brow(head, new() { ["hairs"] = 1f })["hairs"]!).Path.ToSvgPathData();
        Assert.Equal(a, b);

        var sad = Toolkit.ApplyFacialExpression(head, "sadness", 1f);
        Assert.Equal(Contours((CanvasPath)Brow(head, new() { ["hairs"] = 1f })["hairs"]!),
                     Contours((CanvasPath)Brow(sad, new() { ["hairs"] = 1f })["hairs"]!));
        Assert.NotEqual(a, ((CanvasPath)Brow(head, new() { ["hairs"] = 1f, ["seed"] = 7 })["hairs"]!).Path.ToSvgPathData());
    }

    [Fact]
    public void TestHairsStayAboutTheBrow()
    {
        var head = Head();
        var parts = Brow(head, new() { ["hairs"] = 1f });
        var mass = ((CanvasPath)parts["mass"]!).Path.TightBounds;
        var hairs = ((CanvasPath)parts["hairs"]!).Path.TightBounds;
        var thickness = Convert.ToSingle(((Dictionary<string, object?>)head["nearBrow"]!)["thickness"]);
        Assert.True(hairs.Top > mass.Top - thickness * 0.6f, $"hairs reach {mass.Top - hairs.Top:0.0}px above a {thickness:0}px brow");
        Assert.True(hairs.Bottom < mass.Bottom + thickness * 0.6f);
    }

    [Fact]
    public void TestToneGradesFromFromToTo()
    {
        var canvas = new SkiaCanvas(400, 200);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 400, 200);
        var box = new CanvasPath();
        box.Rect(20, 20, 360, 160);
        Toolkit.DrawTone(ctx, box, new Dictionary<string, object?>
        {
            ["amount"] = 0.8f, ["color"] = "#000000",
            ["from"] = new Dictionary<string, object?> { ["x"] = 20f, ["y"] = 100f },
            ["to"] = new Dictionary<string, object?> { ["x"] = 380f, ["y"] = 100f }
        });
        static int Grey(string hex) => Convert.ToInt32(hex.Substring(1, 2), 16);
        var bmp = canvas.Bitmap;
        Assert.True(Grey(bmp.GetPixel(30, 100)) < Grey(bmp.GetPixel(200, 100)));
        Assert.True(Grey(bmp.GetPixel(200, 100)) < Grey(bmp.GetPixel(370, 100)));
        Assert.Equal(255, Grey(bmp.GetPixel(10, 100)));                                            // nothing outside the shape
    }

    [Fact]
    public void TestTheSocketRunsFromTheBrowToTheLid()
    {
        var head = Head();
        var socket = Toolkit.CreateEyeSocket(head["nearEye"]!, head["nearBrow"]!).Path.TightBounds;
        var brow = (Dictionary<string, object?>)head["nearBrow"]!;
        var eye = Eye(head);
        Assert.True(socket.Top <= Convert.ToSingle(((Dictionary<string, object?>)brow["peak"]!)["y"]) + 1f);
        Assert.True(socket.Bottom <= P(eye, "center", "y") + 1f, "the socket stops at the upper lid");
    }
}
