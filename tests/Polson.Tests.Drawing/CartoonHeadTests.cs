namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// The cartoon head: Stanchfield's two masses and his rigid skull over a squashable face (*Drawn to
/// Life* vol. 1 chs. 35, 37), and Gautier's movable eye line and "air" (*Drawing and Cartooning 1,001
/// Faces*, ch. "Cartoons").
/// </summary>
public class CartoonHeadTests : TestsRuntime
{
    const float H = 240f;

    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static Dictionary<string, object?> Head(float yaw = 0f) => Toolkit.CreateLoomisHead(400f, 200f, H, yaw, 0f);

    static Dictionary<string, object?> Group(Dictionary<string, object?> head, string name) =>
        (Dictionary<string, object?>)head[name]!;

    static (float X, float Y) P(Dictionary<string, object?> head, string key) =>
        (Convert.ToSingle(Group(head, key)["x"]), Convert.ToSingle(Group(head, key)["y"]));

    static (float X, float Y) P(Dictionary<string, object?> head, string group, string key)
    {
        var p = (Dictionary<string, object?>)Group(head, group)[key]!;
        return (Convert.ToSingle(p["x"]), Convert.ToSingle(p["y"]));
    }

    static float F(Dictionary<string, object?> d, string key) => Convert.ToSingle(d[key]);

    #region Eye line
    [Fact]
    public void TestTheEyeLineMovesEyesBrowsAndBridgeButNotTheSkull()
    {
        var canon = Head();
        var low = Toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["eyeLine"] = 1f });
        var dy = H * 0.11f;

        Assert.Equal(P(canon, "nearEye", "center").Y + dy, P(low, "nearEye", "center").Y, 3);
        Assert.Equal(P(canon, "farBrow", "peak").Y + dy, P(low, "farBrow", "peak").Y, 3);
        Assert.Equal(P(canon, "noseWedge", "bridgeTop").Y + dy, P(low, "noseWedge", "bridgeTop").Y, 3);
        Assert.Equal(F(canon, "eyeLineY") + dy, F(low, "eyeLineY"), 3);

        Assert.Equal(P(canon, "brow"), P(low, "brow"));          // the cranium's equator
        Assert.Equal(P(canon, "noseBase"), P(low, "noseBase"));
        Assert.Equal(P(canon, "chin"), P(low, "chin"));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void TestTheEyeLineAtEitherEndStillReadsAsAFace(float amount)
    {
        var head = Toolkit.CreateParametricHead(Head(), new Dictionary<string, object?> { ["eyeLine"] = amount });
        Assert.True((bool)Toolkit.VerifyHeadOrdering(head)["ordered"]!);
    }
    #endregion

    #region Squash and stretch
    [Fact]
    public void TestSquashAtZeroIsTheHeadUnchanged()
    {
        var canon = Head();
        var same = Toolkit.SquashHead(Head(), 0f);
        foreach (var key in new[] { "chin", "noseBase", "mouthCenter" })
            Assert.Equal(P(canon, key), P(same, key));
    }

    /// <summary>The skull is rigid; the lower face scales about the eye line, keeping its area.</summary>
    [Fact]
    public void TestStretchScalesTheLowerFaceAndLeavesTheSkull()
    {
        var canon = Head();
        var stretched = Toolkit.SquashHead(Head(), 1f);
        var eyeLine = F(canon, "eyeLineY");

        foreach (var key in new[] { "crown", "hairline", "brow" })
            Assert.Equal(P(canon, key), P(stretched, key));
        Assert.Equal(P(canon, "nearEye", "center"), P(stretched, "nearEye", "center"));
        Assert.Equal(P(canon, "jaw", "ear"), P(stretched, "jaw", "ear"));

        var down = (P(stretched, "chin").Y - eyeLine) / (P(canon, "chin").Y - eyeLine);
        Assert.Equal(1.35f, down, 3);

        float MouthWidth(Dictionary<string, object?> h) =>
            P(h, "mouthGuides", "rightCorner").X - P(h, "mouthGuides", "leftCorner").X;
        var across = MouthWidth(stretched) / MouthWidth(canon);
        Assert.Equal(1f, across * down, 3);   // the area of the face is kept
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void TestSquashAtEitherEndStillReadsAsAFace(float amount)
    {
        var head = Toolkit.SquashHead(Head(), amount);
        Assert.True((bool)Toolkit.VerifyHeadOrdering(head)["ordered"]!);
    }

    [Fact]
    public void TestSquashDoesNotTouchTheHeadItWasGiven()
    {
        var head = Head();
        var chin = P(head, "chin");
        _ = Toolkit.SquashHead(head, -1f);
        Assert.Equal(chin, P(head, "chin"));
    }

    [Fact]
    public void TestASquashThatIsNotANumberIsRefused() =>
        Assert.Throws<ArgumentException>(() => Toolkit.SquashHead(Head(), float.NaN));
    #endregion

    #region Face shapes
    [Theory]
    [InlineData("oval")]
    [InlineData("round")]
    [InlineData("box")]
    [InlineData("narrow")]
    [InlineData("wedge")]
    [InlineData("pear")]
    [InlineData("peanut")]
    public void TestEveryFaceShapeEndsOnTheChinAndHasNoCheeks(string face)
    {
        var head = Head();
        var geo = Toolkit.CreateHeadGeometry(head, new Dictionary<string, object?> { ["face"] = face, ["neckLength"] = 0f });
        var jaw = (CanvasPath)Group(geo, "parts")["jaw"]!;
        var bottom = jaw.Path.Bounds.Bottom;

        Assert.Equal(face, geo["face"]);
        Assert.InRange(bottom, P(head, "chin").Y - 3f, P(head, "chin").Y + 0.5f);
        Assert.True(((CanvasPath)Group(geo, "parts")["farCheek"]!).IsEmpty);
    }

    [Fact]
    public void TestLoomisIsTheDefaultFace()
    {
        var plain = Toolkit.CreateHeadGeometry(Head());
        var named = Toolkit.CreateHeadGeometry(Head(), new Dictionary<string, object?> { ["face"] = "loomis" });
        Assert.Equal(((CanvasPath)plain["silhouette"]!).Area, ((CanvasPath)named["silhouette"]!).Area, 1);
    }

    [Fact]
    public void TestAnUnknownFaceIsRefusedWithTheShapesThatExist()
    {
        var e = Assert.Throws<ArgumentException>(() =>
            Toolkit.CreateHeadGeometry(Head(), new Dictionary<string, object?> { ["face"] = "tall" }));
        Assert.Contains("narrow", e.Message);
        Assert.Contains("peanut", e.Message);
    }

    /// <summary>A squashed face drives the shaped jaw, because the shape is built from the landmarks.</summary>
    [Fact]
    public void TestAShapedFaceFollowsTheSquash()
    {
        var options = new Dictionary<string, object?> { ["face"] = "box", ["neckLength"] = 0f };
        var plain = (CanvasPath)Group(Toolkit.CreateHeadGeometry(Head(), options), "parts")["jaw"]!;
        var stretched = (CanvasPath)Group(Toolkit.CreateHeadGeometry(Toolkit.SquashHead(Head(), 1f), options), "parts")["jaw"]!;

        Assert.True(stretched.Path.Bounds.Bottom > plain.Path.Bounds.Bottom + 20f);
        Assert.True(stretched.Path.Bounds.Width < plain.Path.Bounds.Width);
    }
    #endregion

    #region Air
    /// <summary>
    /// Measured: the canon face is about 6% features, and one crammed with big open eyes and a wide
    /// mouth about 11.5%, which is Gautier's inept cartoon. The workflow threshold of 10% sits between.
    /// </summary>
    [Fact]
    public void TestAirSeparatesTheCanonFromACrammedFace()
    {
        var canon = F(Toolkit.MeasureFaceAir(Head()), "share");
        var crammed = F(Toolkit.MeasureFaceAir(Toolkit.CreateParametricHead(Head(), new Dictionary<string, object?>
        {
            ["eyesSize"] = 1f, ["eyesOpening"] = 1f, ["mouthWidth"] = 1f, ["eyesDistance"] = 0.3f
        })), "share");

        Assert.InRange(canon, 0.04f, 0.09f);
        Assert.True(crammed > 0.10f, $"crammed face measured {crammed:P1}");
        Assert.True(crammed > canon * 1.5f);
    }

    [Fact]
    public void TestAirDoesNotDependOnTheHeadsSize()
    {
        var small = F(Toolkit.MeasureFaceAir(Toolkit.CreateLoomisHead(100f, 100f, 90f, 0f, 0f)), "share");
        var large = F(Toolkit.MeasureFaceAir(Toolkit.CreateLoomisHead(400f, 400f, 600f, 0f, 0f)), "share");
        Assert.InRange(small / large, 0.8f, 1.25f);
    }

    [Fact]
    public void TestAirReportsEachFeature()
    {
        var air = Toolkit.MeasureFaceAir(Head());
        var by = (Dictionary<string, object?>)air["byFeature"]!;
        foreach (var feature in new[] { "eyes", "brows", "nose", "mouth" })
            Assert.True(F(by, feature) > 0f, feature);
        Assert.Equal(1f, F(air, "share") + F(air, "air"), 4);
    }

    [Fact]
    public void TestAnUnknownAirOptionIsRefused() =>
        Assert.Throws<ArgumentException>(() =>
            Toolkit.MeasureFaceAir(Head(), new Dictionary<string, object?> { ["padding"] = 4f }));
    #endregion
}
