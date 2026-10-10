namespace Polson.Tests.Animation;

using System;
using System.Collections.Generic;
using System.Linq;

using Polson.Animation;
using Polson.Drawing.Skia;

using Xunit;

/// <summary>
/// The face layer: a constructed head rebuilt every frame from its channels, Action Units, expressions and Hamm's
/// eye-wheel settings, each a value or a node.
/// </summary>
public class MotionFaceLayerTests : TestsRuntime
{
    static readonly ConstructiveDrawingToolkit Toolkit = new();

    [Fact]
    public void TheFaceIsTheHeadBuiltFromItsChannels()
    {
        var comp = Composition(new MotionToolkit());
        var face = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("yaw", 20d),
            ("character", Opts(("jawShape", 0.4d))),
            ("channels", Opts(("AU12", 0.8d), ("upperLid", 4d)))));

        var byHand = Toolkit.CreateLoomisHead(200f, 200f, 300f, 20f, 0f);
        byHand = Toolkit.CreateParametricHead(byHand, Opts(("jawShape", 0.4f)));
        byHand = Toolkit.ApplyActionUnits(byHand, Opts(("AU12", 0.8f)));
        byHand = Toolkit.EyeWheel(byHand, Opts(("upperLid", 4f)));

        var drawn = face.HeadAt(0);
        Assert.Equal(Point(byHand, "mouthGuides", "rightCorner"), Point(drawn, "mouthGuides", "rightCorner"));
        Assert.Equal(Value(byHand, "nearEye", "height"), Value(drawn, "nearEye", "height"), 3);
        Assert.Equal(Point(byHand, "jaw", "chinNear"), Point(drawn, "jaw", "chinNear"));
    }

    [Fact]
    public void AKeyedChannelReachesTheFaceAtItsTime()
    {
        var motion = new MotionToolkit();
        var comp = Composition(motion);
        var face = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d),
            ("channels", Opts(("AU12", motion.Nodes.Linear("real", 1, 0)), ("upperLid", motion.Nodes.Linear("real", 2, 3))))));

        float Wide(double t) => Point(face.HeadAt(t), "mouthGuides", "rightCorner").X - Point(face.HeadAt(t), "mouthGuides", "leftCorner").X;
        Assert.True(Wide(1) > Wide(0) + 3f, "the smile widens the mouth as AU12 rises");
        Assert.True(Value(face.HeadAt(1), "nearEye", "height") < Value(face.HeadAt(0), "nearEye", "height") * 0.6f, "the lid closes from 3 to 5");
        Assert.Equal(new[] { "AU12", "upperLid" }, face.Channels.ToArray());
        Assert.Equal(0.5d, (double)face.ChannelsAt(0.5)["AU12"]!, 6);
    }

    [Fact]
    public void ASidePrefixMovesOneSide()
    {
        var comp = Composition(new MotionToolkit());
        var rest = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d))).HeadAt(0);
        var smirk = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("channels", Opts(("near.AU12", 1d), ("far.browTop", 1d))))).HeadAt(0);

        Assert.NotEqual(Point(rest, "mouthGuides", "rightCorner"), Point(smirk, "mouthGuides", "rightCorner"));
        Assert.Equal(Point(rest, "mouthGuides", "leftCorner"), Point(smirk, "mouthGuides", "leftCorner"));
        Assert.True(Point(smirk, "farBrow", "peak").Y < Point(rest, "farBrow", "peak").Y - 2f);
        Assert.Equal(Point(rest, "nearBrow", "peak").Y, Point(smirk, "nearBrow", "peak").Y, 2);
    }

    [Fact]
    public void AnExpressionChannelIsItsUnits()
    {
        var comp = Composition(new MotionToolkit());
        var face = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("channels", Opts(("sadness", 0.7d)))));
        var byHand = Toolkit.ApplyActionUnits(Toolkit.CreateLoomisHead(200f, 200f, 300f, 0f, 0f), Toolkit.ExpressionUnits("sadness", 0.7f));
        Assert.Equal(Point(byHand, "nearBrow", "inner"), Point(face.HeadAt(0), "nearBrow", "inner"));
        Assert.Equal(Point(byHand, "mouthGuides", "rightCorner"), Point(face.HeadAt(0), "mouthGuides", "rightCorner"));
    }

    /// <summary>A keyed curve that overshoots a wheel setting is held inside the wheel, so an overshooting ease can be used.</summary>
    [Fact]
    public void AWheelChannelPastItsRangeIsHeldInside()
    {
        var comp = Composition(new MotionToolkit());
        var over = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("channels", Opts(("upperLid", 6.5d))))).HeadAt(0);
        var shut = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("channels", Opts(("upperLid", 5d))))).HeadAt(0);
        Assert.Equal(Value(shut, "nearEye", "height"), Value(over, "nearEye", "height"), 4);
    }

    [Fact]
    public void ItDrawsInItsLayersFrame()
    {
        var comp = Composition(new MotionToolkit());
        var group = comp.Group(Opts(("offset", new[] { 100d, 0d })));
        group.Face(Opts(("origin", new[] { 100d, 100d }), ("height", 150d), ("look", Opts(("skin", "#ff0000")))));
        using var frame = comp.Render(0);
        var red = frame.SkBitmap.GetPixel(200, 100);
        Assert.True(red.Red > 200 && red.Green < 80, $"the skin at the moved centre, got {red}");
        Assert.True(frame.SkBitmap.GetPixel(100, 100).Red < 200 || frame.SkBitmap.GetPixel(100, 100).Green > 80, "and not where it started");
    }

    [Fact]
    public void WhatItCannotDoIsRefusedByName()
    {
        var comp = Composition(new MotionToolkit());
        Assert.Contains("'upperlid'", Assert.Throws<ArgumentException>(() => comp.Face(Opts(("channels", Opts(("upperlid", 4d)))))).Message);
        Assert.Throws<ArgumentException>(() => comp.Face(Opts(("channels", Opts(("AU99", 1d))))));
        var badLook = Opts(("look", Opts(("eyes", Opts()))));
        Assert.Contains("'eyes'", Assert.Throws<ArgumentException>(() => comp.Face(badLook)).Message);
        Assert.Throws<ArgumentException>(() => comp.Face(Opts(("construction", "doubleCircle"), ("yaw", 20d))));
        var badLines = Opts(("look", Opts(("lines", Opts(("D", 1d))))));
        Assert.Throws<ArgumentException>(() => comp.Face(badLines));   // the drawer's own refusal, at the call

        var ok = Composition(new MotionToolkit());
        ok.Face(Opts(("desc", "Mort")));
        Assert.Contains("face layer", Assert.Throws<InvalidOperationException>(() => ok.ToSif()).Message);
    }

    [Fact]
    public void TheDoubleCircleHeadIsAConstruction()
    {
        var comp = Composition(new MotionToolkit());
        var head = comp.Face(Opts(("origin", new[] { 200d, 200d }), ("height", 300d), ("construction", "doubleCircle"))).HeadAt(0);
        Assert.Equal("doubleCircle", head["construction"]);
    }

    #region Helpers
    private static MotionComposition Composition(MotionToolkit motion) =>
        motion.Composition(Opts(("width", 400d), ("height", 400d), ("fps", 4d), ("duration", 1d)));

    private static (float X, float Y) Point(Dictionary<string, object?> head, string group, string key)
    {
        var p = (IDictionary<string, object?>)((IDictionary<string, object?>)head[group]!)[key]!;
        return (Convert.ToSingle(p["x"]), Convert.ToSingle(p["y"]));
    }

    private static float Value(Dictionary<string, object?> head, string group, string key) =>
        Convert.ToSingle(((IDictionary<string, object?>)head[group]!)[key]);

    private static Dictionary<string, object?> Opts(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
    #endregion
}
