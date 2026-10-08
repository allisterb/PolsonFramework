namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// Hamm's nose (<i>Drawing the Head and Figure</i>, pp. 13–15) in <c>drawComicNose</c>: the depressions beside the
/// bridge, the far wing going round the ball as the head turns, and the shadow side under a light.
/// </summary>
public class HammNoseTests : TestsRuntime
{
    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static Dictionary<string, object?> Head(float yaw = 0f) => Toolkit.CreateLoomisHead(400f, 400f, 560f, yaw);

    static Dictionary<string, object?> Nose(Dictionary<string, object?> head) => (Dictionary<string, object?>)head["noseWedge"]!;

    static Dictionary<string, object?> Draw(Dictionary<string, object?> head, Dictionary<string, object?>? options = null) =>
        Toolkit.DrawComicNose(new SkiaCanvas(800, 800).GetContext("2d"), Nose(head), options);

    static float P(Dictionary<string, object?> d, string key, string axis) => Convert.ToSingle(((Dictionary<string, object?>)d[key]!)[axis]);

    static SkiaSharp.SKRect Bounds(Dictionary<string, object?> parts, string key) => ((CanvasPath)parts[key]!).Path.TightBounds;

    [Fact]
    public void TestTheDepressionsSitBesideTheBridgeAtTheEyes()
    {
        var head = Head();
        var marks = Bounds(Draw(head), "depressions");
        Assert.False(marks.IsEmpty, "drawn by default, as Hamm advises");
        Assert.True(((CanvasPath)Draw(head, new() { ["depressions"] = 0f })["depressions"]!).Path.IsEmpty);

        // Between the inner eye corners, and from about the eye line, well above the ball.
        float innerFar = P((Dictionary<string, object?>)head["farEye"]!, "inner", "x");
        float innerNear = P((Dictionary<string, object?>)head["nearEye"]!, "inner", "x");
        Assert.InRange(marks.Left, MathF.Min(innerFar, innerNear) - 1f, 400f);
        Assert.InRange(marks.Right, 400f, MathF.Max(innerFar, innerNear) + 1f);
        Assert.True(marks.Bottom < P(Nose(head), "apex", "y"), "the hollows are between the eyes, not on the nose");
    }

    [Fact]
    public void TestTheFarWingGoesRoundTheBall()
    {
        static float Size(Dictionary<string, object?> parts, string key) { var b = Bounds(parts, key); return b.IsEmpty ? 0f : b.Width * b.Height; }

        var front = Draw(Head(0f));
        Assert.Equal(Size(front, "nostrilHole"), Size(front, "farNostrilHole"), 1f);

        // Turned, the far opening is a trace against the septum, and the wing only a rim.
        var head = Head(50f);
        var turned = Draw(head);
        Assert.True(Size(turned, "farNostrilHole") < Size(turned, "nostrilHole") * 0.35f, "the far opening shrinks to a trace");
        float septum = P(Nose(head), "underNose", "x"), farEdge = P(Nose(head), "farNostril", "x");
        Assert.True(MathF.Abs(Bounds(turned, "farNostrilHole").MidX - septum) < MathF.Abs(farEdge - septum) * 0.5f, "and hugs the septum");

        // At the turn the construction allows, the far wing is gone.
        var away = Draw(Head(63f));
        Assert.True(((CanvasPath)away["farNostril"]!).Path.IsEmpty);
        Assert.True(((CanvasPath)away["farNostrilHole"]!).Path.IsEmpty);
    }

    static bool Drawn(Dictionary<string, object?> parts, string key) => !((CanvasPath)parts[key]!).Path.IsEmpty;

    [Fact]
    public void TestEachDetailLevelDrawsHammsStage()
    {
        var head = Head();
        Dictionary<string, object?> At(int level) => Draw(head, new() { ["detail"] = level });

        // 1: the base line only.
        var one = At(1);
        Assert.Equal(1, one["detail"]);
        Assert.True(Drawn(one, "base"));
        foreach (var key in new[] { "nostril", "nostrilHole", "depressions", "sideLine", "underPlane", "bridgeMark" })
            Assert.False(Drawn(one, key), $"{key} at detail 1");

        // 2: the bottom of the ball, and one stroke down the side of the bridge.
        var two = At(2);
        Assert.True(Drawn(two, "base") && Drawn(two, "sideLine"));
        Assert.False(Drawn(two, "nostril") || Drawn(two, "underPlane"));
        Assert.True(Bounds(two, "base").Height > Bounds(one, "base").Height, "the ends turn up");

        // 3: the wings and nostrils, and the hollows between the eyes; no shading yet.
        var three = At(3);
        Assert.True(Drawn(three, "nostril") && Drawn(three, "nostrilHole") && Drawn(three, "depressions"));
        Assert.False(Drawn(three, "underPlane") || Drawn(three, "bridgeMark") || Drawn(three, "sideLine"));

        // 4: the whole form.
        var four = At(4);
        Assert.True(Drawn(four, "underPlane") && Drawn(four, "bridgeMark"));
    }

    [Fact]
    public void TestASmallerHeadGetsLessOfTheNose()
    {
        int Level(float size) => (int)Toolkit.DrawComicNose(new SkiaCanvas(800, 800).GetContext("2d"),
            Nose(Toolkit.CreateLoomisHead(400f, 400f, size, 20f)), null)["detail"]!;
        Assert.Equal(4, Level(300f));
        Assert.Equal(3, Level(120f));
        Assert.Equal(2, Level(60f));
        Assert.Equal(1, Level(30f));
        Assert.Equal(4, (int)Toolkit.DrawComicNose(new SkiaCanvas(200, 200).GetContext("2d"),
            Nose(Toolkit.CreateLoomisHead(100f, 100f, 30f, 0f)), new Dictionary<string, object?> { ["detail"] = 4 })["detail"]!);
    }

    [Fact]
    public void TestTheDetailTwoBridgeStrokeTakesTheShadowSide()
    {
        var head = Head();
        var axis = P(Nose(head), "underNose", "x");
        var lit = Bounds(Draw(head, new() { ["detail"] = 2, ["light"] = 180f }), "sideLine");
        Assert.True(lit.MidX > axis, "light from the left puts the stroke on the right");
    }

    /// <summary>
    /// The nose's shape options from Hamm's p. 13 catalogue move what they name: a large ball shrinks the wings and
    /// raises its underside; exposed nostrils are larger openings, hidden ones smaller, whatever the ball; a
    /// low-hanging septum drops the base, a tucked one flattens it without arching it.
    /// </summary>
    [Fact]
    public void TestTheNosesShapeOptionsMoveWhatTheyName()
    {
        var head = Head();
        Dictionary<string, object?> With(string key, float value) => Draw(head, new() { [key] = value });
        static float Area(Dictionary<string, object?> parts, string key) => ((CanvasPath)parts[key]!).Area;
        var plain = Draw(head);

        var large = With("ball", 1f);
        Assert.True(Bounds(large, "nostril").Height < Bounds(plain, "nostril").Height * 0.85f, "a large ball does not crowd the wings");
        Assert.True(Bounds(large, "underPlane").Height > Bounds(plain, "underPlane").Height * 1.3f, "a large ball's underside is no taller");

        Assert.True(Area(With("nostrils", 1f), "nostrilHole") > Area(plain, "nostrilHole") * 1.8f, "exposed nostrils are not larger");
        Assert.True(Area(With("nostrils", -1f), "nostrilHole") < Area(plain, "nostrilHole") * 0.3f, "hidden nostrils are not smaller");
        Assert.Equal(Area(plain, "nostrilHole"), Area(With("ball", -1f), "nostrilHole"), 0.5f);

        var septum = P(Nose(head), "underNose", "y");
        var len = septum - P(Nose(head), "bridgeTop", "y");
        Assert.True(Bounds(With("septum", 1f), "base").Bottom > Bounds(plain, "base").Bottom + (len * 0.03f), "a low septum does not hang");
        var tucked = Bounds(With("septum", -1f), "base");
        Assert.True(tucked.Bottom >= septum - 1f, "a tucked septum arches the base");
        Assert.True(tucked.Height < Bounds(plain, "base").Height, "a tucked septum does not flatten the base");
    }

    [Theory]
    [InlineData(0f, -1f)]       // light from the right: shadow on the left
    [InlineData(-40f, -1f)]     // from the upper right
    [InlineData(180f, 1f)]      // from the left: shadow on the right
    [InlineData(-90f, 0f)]      // from straight above: no side shadow
    public void TestTheShadowSideFacesAwayFromTheLight(float light, float side)
    {
        var head = Head();
        Assert.True(((CanvasPath)Draw(head)["sideShadow"]!).Path.IsEmpty, "no light, no side shadow");

        var parts = Draw(head, new() { ["light"] = light });
        var shadow = Bounds(parts, "sideShadow");
        if (side == 0f)
        {
            Assert.True(shadow.IsEmpty);
            return;
        }
        var axis = P(Nose(head), "underNose", "x");
        Assert.Equal(side, MathF.Sign(shadow.MidX - axis));
        Assert.Equal(side, MathF.Sign(Bounds(parts, "sideLine").MidX - axis));
    }
}
