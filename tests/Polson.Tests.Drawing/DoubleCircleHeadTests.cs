namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// Hamm's double-circle head (<i>Drawing the Head and Figure</i>, pp. 2–4). A 700px head is 100px an eye: the big
/// circle is centred at A, 250px below the crown, with radius 250; C, F, D and E are 250, 300, 350 and 450 below A.
/// </summary>
public class DoubleCircleHeadTests : TestsRuntime
{
    const float X = 400f, OY = 450f, H = 700f, E = 100f, A = OY - H / 2f + 2.5f * E;

    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static Dictionary<string, object?> Head() => Toolkit.CreateDoubleCircleHead(X, OY, H);

    static Dictionary<string, object?> D(Dictionary<string, object?> d, string key) => (Dictionary<string, object?>)d[key]!;

    static float F(Dictionary<string, object?> d, string key) => Convert.ToSingle(d[key]);

    static (float X, float Y) P(Dictionary<string, object?> d, string key) => (F(D(d, key), "x"), F(D(d, key), "y"));

    [Fact]
    public void TestTheVerticalStationsAreHamms()
    {
        var h = Head();
        Assert.Equal(OY - H / 2f, P(h, "crown").Y, 2);
        Assert.Equal(A, P(h, "brow").Y, 2);                                  // the starting line through the big circle
        Assert.Equal(A + 2.5f * E, P(h, "noseBase").Y, 2);                   // C
        Assert.Equal(A + 3f * E, F(D(h, "mouthGuides"), "upperLipY"), 2);   // F
        Assert.Equal(A + 3.5f * E, F(D(h, "mouthGuides"), "lowerLipY"), 2); // D
        Assert.Equal(A + 4.5f * E, P(h, "chin").Y, 2);                       // E
        Assert.Equal("doubleCircle", h["construction"]);
    }

    [Fact]
    public void TestTheEyesHangFromJKUnderTheSecondAndFourthFifths()
    {
        var h = Head();
        var jk = A + 2.5f * E / 3f;                                          // the tangents touch at (r1 − r2) / d of the radius
        foreach (var (key, side) in new[] { ("farEye", -1f), ("nearEye", 1f) })
        {
            var eye = D(h, key);
            Assert.Equal(E, F(eye, "width"), 2);
            Assert.Equal(E / 2f, F(eye, "height"), 2);                       // half its own length (p. 4)
            Assert.Equal(jk, P(eye, "center").Y - F(eye, "height") / 2f, 2);
            Assert.Equal(X + side * 0.5f * E, P(eye, "inner").X, 2);
            Assert.Equal(X + side * 1.5f * E, P(eye, "outer").X, 2);
        }
        var wedge = D(h, "noseWedge");
        Assert.Equal(E, P(wedge, "nearNostril").X - P(wedge, "farNostril").X, 2);   // the middle fifth
        var mouth = D(h, "mouthGuides");
        Assert.Equal(1.5f * E, P(mouth, "rightCorner").X - P(mouth, "leftCorner").X, 2);
        Assert.Equal(X - 2.5f * E * MathF.Sqrt(8f) / 3f, P(D(h, "jaw"), "farStation").X, 2);   // J
    }

    [Fact]
    public void TestTheGeometryIsHammsOutlineAndFiveEyesWide()
    {
        var geo = Toolkit.CreateHeadGeometry(Head());
        Assert.Equal("doubleCircle", geo["face"]);
        var mass = (CanvasPath)geo["mass"]!;
        var side = 2.5f * E * MathF.Sqrt(8f) / 3f;
        // The verticals from J and K trim the big circle: inside at the side, outside where the ball alone would reach.
        Assert.True(mass.Contains(X + side - 2f, A - 50f));
        Assert.False(mass.Contains(X + side + 6f, A - 50f));
        Assert.True(mass.Contains(X, A + 4.5f * E - 2f));                   // reaches the chin
        Assert.False(mass.Contains(X, A + 4.5f * E + 3f));
        var width = F(D(geo, "bounds"), "width") / E;
        Assert.InRange(width, 4.8f, 5.2f);                                   // ears included
    }

    [Fact]
    public void TestALoomisHeadCanTakeHammsOutline()
    {
        var loomis = Toolkit.CreateLoomisHead(X, OY, H, 0f);
        Assert.Equal("loomis", Toolkit.CreateHeadGeometry(loomis)["face"]);
        Assert.Equal("doubleCircle", Toolkit.CreateHeadGeometry(loomis, new Dictionary<string, object?> { ["face"] = "DoubleCircle" })["face"]);
    }

    [Fact]
    public void TestExaggeratingAPlainHeadChangesNothingBecauseTheCanonIsHamms()
    {
        var h = Head();
        var same = Toolkit.ExaggerateHead(h, 1f);
        foreach (var key in new[] { "crown", "brow", "chin", "noseBase" })
        {
            Assert.Equal(P(h, key).X, P(same, key).X, 2);
            Assert.Equal(P(h, key).Y, P(same, key).Y, 2);
        }
        Assert.Equal(P(D(h, "nearEye"), "center").Y, P(D(same, "nearEye"), "center").Y, 2);
    }

    [Fact]
    public void TestParametersAndExpressionsKeepTheConstruction()
    {
        var h = Toolkit.ApplyFacialExpression(
            Toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["chinLength"] = 0.6f }), "anger", 0.8f);
        Assert.Equal("doubleCircle", h["construction"]);
        Assert.True(P(h, "chin").Y > A + 4.5f * E);
        Assert.Equal("doubleCircle", Toolkit.CreateHeadGeometry(h)["face"]);
    }

    [Fact]
    public void TestTheWireframeRefusesAnUnknownOption()
    {
        var canvas = new SkiaCanvas(200, 200);
        var ctx = (CanvasRenderingContext2D)canvas.GetContext("2d");
        Toolkit.DrawDoubleCircleWireframe(ctx, Head(), new Dictionary<string, object?> { ["labels"] = true });
        Assert.Throws<ArgumentException>(() => Toolkit.DrawDoubleCircleWireframe(ctx, Head(), new Dictionary<string, object?> { ["label"] = true }));
    }
}
