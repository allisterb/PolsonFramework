namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.IO;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary>
/// Frame capture, animated encoding, and the contact sheet.
/// </summary>
/// <remarks>
/// <c>Motion</c> is a spike, so these pin behaviour rather than surface: what a caller could not
/// discover was wrong by looking at the output. Capture taking a copy is the first of them — a
/// sequence whose every frame shows the last state renders, encodes, and is silently useless.
/// </remarks>
public class MotionToolkitTests : TestsRuntime
{
    #region Methods
    private static SkiaCanvas Board(int w = 40, int h = 30, string colour = "#ff0000")
    {
        var canvas = new SkiaCanvas(w, h);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = colour;
        ctx.FillRect(0, 0, w, h);
        return canvas;
    }

    private static string TempPath(string name) =>
        Path.Combine(Path.GetTempPath(), $"polson-motion-{Guid.NewGuid():N}-{name}");

    /// <summary>Capture takes a copy, so drawing on after it does not rewrite the frame.</summary>
    /// <remarks>
    /// A canvas is a live surface. Holding the reference instead would make every frame show the
    /// final state — a whole sequence quietly wrong, with a file that encodes and plays.
    /// </remarks>
    [Fact]
    public void TestCaptureTakesACopyOfTheLiveSurface()
    {
        using var motion = new MotionToolkit();
        using var canvas = Board(colour: "#ff0000");
        var ctx = canvas.GetContext("2d");

        motion.Frame(canvas);
        ctx.FillStyle = "#0000ff";
        ctx.FillRect(0, 0, 40, 30);
        motion.Frame(canvas);

        Assert.Equal(2, motion.Count);

        var path = TempPath("copy.png");
        try
        {
            var sheet = motion.Sheet(path, new Dictionary<string, object?>
            {
                ["indices"] = new List<object?> { 0, 1 }, ["cols"] = 2, ["labels"] = false,
                ["gap"] = 0f, ["padding"] = 0f, ["background"] = "#00ff00"
            });

            Assert.Equal(2, Convert.ToInt32(sheet["cells"]));
            using var bitmap = SKBitmap.Decode(path);

            // Left cell must still be the first colour; right cell the second.
            var left = bitmap.GetPixel(bitmap.Width / 4, bitmap.Height / 2);
            var right = bitmap.GetPixel(bitmap.Width * 3 / 4, bitmap.Height / 2);
            Assert.True(left.Red > 200 && left.Blue < 60, $"first frame should still be red, got {left}");
            Assert.True(right.Blue > 200 && right.Red < 60, $"second frame should be blue, got {right}");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    /// <summary>A size mismatch is refused, with both sizes named.</summary>
    [Fact]
    public void TestFramesOfDifferentSizesAreRefused()
    {
        using var motion = new MotionToolkit();
        using var small = Board(40, 30);
        using var large = Board(60, 30);

        motion.Frame(small);
        var ex = Assert.Throws<InvalidOperationException>(() => motion.Frame(large));

        Assert.Contains("40x30", ex.Message, StringComparison.Ordinal);
        Assert.Contains("60x30", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, motion.Count);
    }

    /// <summary>Saving reports what the file holds, not only what was handed in.</summary>
    /// <remarks>
    /// The WebP encoder merges consecutive pixel-identical frames and sums their durations. That is
    /// correct and a real size win, but a caller told "57 frames" about a file containing 47 has been
    /// misinformed — so both numbers are reported and the duration stays exact.
    /// </remarks>
    [Fact]
    public void TestSavingReportsMergedFramesHonestly()
    {
        using var motion = new MotionToolkit();
        using var canvas = Board();

        for (var i = 0; i < 8; i++) motion.Frame(canvas);   // eight identical frames

        var path = TempPath("merged.webp");
        try
        {
            var saved = motion.Save(path, new Dictionary<string, object?> { ["fps"] = 25f });

            Assert.Equal(8, Convert.ToInt32(saved["frames"]));
            Assert.True(Convert.ToInt32(saved["storedFrames"]) < 8, "identical frames should merge");
            Assert.Equal(8 - Convert.ToInt32(saved["storedFrames"]), Convert.ToInt32(saved["merged"]));
            Assert.Equal(320f, Convert.ToSingle(saved["durationMs"]), 1);   // 8 x 40ms, regardless

            using var codec = SKCodec.Create(path);
            Assert.Equal(Convert.ToInt32(saved["storedFrames"]), codec.FrameCount);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    /// <summary>Even spacing always includes both ends of the sequence.</summary>
    /// <remarks>
    /// The extremes of a movement are what a reader checks first, so a sheet that sampled the middle
    /// and stopped short would omit exactly the two frames worth having.
    /// </remarks>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void TestASheetAlwaysShowsTheFirstAndLastFrame(int count)
    {
        using var motion = new MotionToolkit();
        using var canvas = Board();
        for (var i = 0; i < 20; i++) motion.Frame(canvas);

        var path = TempPath($"ends{count}.png");
        try
        {
            var sheet = motion.Sheet(path, new Dictionary<string, object?> { ["count"] = (float)count });
            var indices = (List<object?>)sheet["indices"]!;

            Assert.Equal(count, indices.Count);
            Assert.Equal(0, Convert.ToInt32(indices[0]));
            Assert.Equal(19, Convert.ToInt32(indices[^1]));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    /// <summary>A frame that was never captured is refused rather than clamped.</summary>
    [Fact]
    public void TestAskingForAFrameThatIsNotHeldIsRefused()
    {
        using var motion = new MotionToolkit();
        using var canvas = Board();
        motion.Frame(canvas);

        Assert.Throws<ArgumentOutOfRangeException>(() => motion.Sheet(
            TempPath("oob.png"),
            new Dictionary<string, object?> { ["indices"] = new List<object?> { 0, 5 } }));
    }

    /// <summary>Both writers refuse an empty sequence, rather than producing an empty file.</summary>
    [Fact]
    public void TestSavingNothingIsRefused()
    {
        using var motion = new MotionToolkit();

        Assert.Throws<InvalidOperationException>(() => motion.Save(TempPath("empty.webp")));
        Assert.Throws<InvalidOperationException>(() => motion.Sheet(TempPath("empty.png")));
    }

    /// <summary>Clearing releases the frames and resets the count.</summary>
    [Fact]
    public void TestClearingReleasesEverything()
    {
        using var motion = new MotionToolkit();
        using var canvas = Board();
        for (var i = 0; i < 5; i++) motion.Frame(canvas);
        Assert.Equal(5, motion.Count);

        motion.Clear();
        Assert.Equal(0, motion.Count);

        motion.Frame(canvas);
        Assert.Equal(1, motion.Count);
    }
    #endregion
}
