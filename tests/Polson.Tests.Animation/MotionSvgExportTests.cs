namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Polson.Animation;
using Polson.Drawing.Svg;

using SkiaSharp;

using Xunit;
using Xunit.Abstractions;

using static MotionCompositionTests;

/// <summary>
/// Animated SVG export, checked the way the Synfig route is: the exported file rendered by Svg.Skia at a
/// time, against the composition rendered by us at the same time. Nothing external is needed.
/// </summary>
public class MotionSvgExportTests(ITestOutputHelper output) : TestsRuntime
{
    /// <summary>The whole oracle scene, at every frame and halfway between frames.</summary>
    [Fact]
    public void TheExportedSvgPlaysTheSameFrames()
    {
        var comp = OracleScene(new MotionToolkit());
        var svg = comp.ToSvg();
        output.WriteLine(Regex.Match(svg, "<!--(.*?)-->").Groups[1].Value.Trim());

        double worstOn = 0, worstBetween = 0;
        for (var i = 0; i < comp.FrameCount; i++)
        {
            var t = i / comp.Fps;
            worstOn = Math.Max(worstOn, Compare(comp, svg, t));
            if (i + 1 < comp.FrameCount) worstBetween = Math.Max(worstBetween, Compare(comp, svg, t + 0.5 / comp.Fps));
        }

        output.WriteLine($"worst frame {worstOn:P2}, worst between frames {worstBetween:P2} of pixels differ by more than 48");
        Assert.True(worstOn < 0.01, $"worst frame differs in {worstOn:P2} of its pixels");
        Assert.True(worstBetween < 0.02, $"worst in-between differs in {worstBetween:P2} of its pixels");
    }

    /// <summary>
    /// Keyed values written as keySplines are exact at any time, not just at frames: a disc per ease,
    /// measured between frames, where a sampled track would only be approximately right.
    /// </summary>
    [Fact]
    public void KeyedValuesAreExactBetweenFrames()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 400d), ("height", 300d), ("fps", 4d), ("duration", 3d)));
        comp.Fill(Opts(("color", "#ffffff")));
        string[] eases = ["clamped", "halt", "linear", "constant"];
        var colors = new[] { "#ff0000", "#00ff00", "#0000ff", "#ff00ff" };
        var nodes = new List<MotionNode>();
        for (var k = 0; k < eases.Length; k++)
        {
            var x = Animated("real", (0.2, 40, eases[k]), (0.75, 300, eases[k]), (1.25, 320, eases[k]), (2.6, 60, eases[k]));
            nodes.Add(x);
            comp.Circle(Opts(("origin", motion.Nodes.Composite(x, 40d + k * 60)), ("radius", 9d), ("color", colors[k])));
        }

        var svg = comp.ToSvg();
        Assert.Contains("4 exact track(s), 0 sampled", svg);

        var worst = 0d;
        foreach (var t in new[] { 0.1, 0.37, 0.61, 0.99, 1.13, 1.7, 2.33, 2.9 })
        {
            using var bitmap = Svg(svg, comp, t);
            for (var k = 0; k < eases.Length; k++)
            {
                var (cx, _) = Centroid(bitmap, c => Near(c, SKColor.Parse(colors[k])));
                var expected = (double)nodes[k].At(t);
                worst = Math.Max(worst, Math.Abs(cx - expected));
                if (Math.Abs(cx - expected) > 0.5) output.WriteLine($"{eases[k]} at {t}s: svg {cx:0.00}, ours {expected:0.00}");
            }
        }

        output.WriteLine($"worst centre error {worst:0.000}px");
        Assert.True(worst < 0.5, $"worst centre error {worst:0.000}px");
    }

    /// <summary>A bar whose top is keyed stays exact: its y and height are affine in the key.</summary>
    [Fact]
    public void AKeyedBarIsWrittenExactly()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("width", 200d), ("height", 200d), ("fps", 4d), ("duration", 2d)));
        var top = Animated("vector", (0.25, new[] { 80d, 180d }, "halt"), (1.5, new[] { 80d, 40d }, "halt"));
        comp.Rectangle(Opts(("point1", new[] { 40d, 180d }), ("point2", top), ("color", "#ff0000")));

        var svg = comp.ToSvg();
        Assert.Contains("1 exact track(s), 0 sampled", svg);   // y moves; x and the width never change

        foreach (var t in new[] { 0.4, 0.77, 1.1 })
        {
            using var bitmap = SKBitmap.Decode(SvgRenderPipeline.RenderToImage(svg, 200, 200, "png", 100, null, TimeSpan.FromSeconds(t)));
            var rows = Enumerable.Range(0, 200).Where(y => Near(bitmap.GetPixel(60, y), SKColor.Parse("#ff0000"))).ToArray();
            var expected = ((IDictionary<string, object?>)top.At(t))["y"]!;
            Assert.InRange(rows.Min(), (double)expected - 1, (double)expected + 1);   // the first fully covered row
        }
    }

    [Fact]
    public void AnOvershootingCurveFallsBackToSamples()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition(Opts(("duration", 2d)));
        var x = Animated("real", (0, 0, "auto"), (1, 100, "auto"), (2, 100, "auto"));
        comp.Circle(Opts(("origin", motion.Nodes.Composite(x, 50d))));

        Assert.Contains("0 exact track(s), 1 sampled", comp.ToSvg());
    }

    [Fact]
    public void ADrawnLayerIsRefusedByName()
    {
        var comp = new MotionToolkit().Composition();
        comp.Drawn((ctx, v, t) => { }, Opts(("desc", "figure")));

        var e = Assert.Throws<InvalidOperationException>(() => comp.ToSvg());
        Assert.Contains("'figure'", e.Message);
    }

    [Fact]
    public void LoopRepeatsAndTheDefaultHolds()
    {
        var motion = new MotionToolkit();
        var comp = motion.Composition();
        comp.Circle(Opts(("radius", motion.Nodes.Linear("real", 10, 5))));

        Assert.Contains("fill=\"freeze\"", comp.ToSvg());
        Assert.Contains("repeatCount=\"indefinite\"", comp.ToSvg(Opts(("loop", true))));
    }

    [Fact]
    public void AStaticCompositionHasNoAnimation()
    {
        var comp = new MotionToolkit().Composition();
        comp.Circle(Opts(("origin", new[] { 50d, 50d }), ("radius", 20d)));

        Assert.DoesNotContain("<animate", comp.ToSvg());
    }

    #region Private
    private double Compare(MotionComposition comp, string svg, double t)
    {
        using var ours = comp.Render(t);
        using var theirs = Svg(svg, comp, t);
        return Differing(ours.SkBitmap, theirs, 48);
    }

    private static SKBitmap Svg(string svg, MotionComposition comp, double t) =>
        SKBitmap.Decode(SvgRenderPipeline.RenderToImage(svg, comp.Width, comp.Height, "png", 100, null, TimeSpan.FromSeconds(t)));
    #endregion
}
