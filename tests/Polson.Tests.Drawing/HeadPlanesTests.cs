namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>createHeadPlanes</c> and <c>drawHeadPlanes</c>: Loomis's Plate 9 planes as shapes with facings, lit by one light.
/// </summary>
public class HeadPlanesTests : TestsRuntime
{
    const float H = 560f;

    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static Dictionary<string, object?> Head(float yaw = 0f) => Toolkit.CreateLoomisHead(400f, 400f, H, yaw);

    static Dictionary<string, object?> Planes(Dictionary<string, object?> head, string detail = "basic") =>
        Toolkit.CreateHeadPlanes(head, new Dictionary<string, object?> { ["detail"] = detail });

    static Dictionary<string, object?> Plane(Dictionary<string, object?> planes, string name) =>
        (Dictionary<string, object?>)((Dictionary<string, object?>)planes["byName"]!)[name]!;

    static float Facing(Dictionary<string, object?> plane, string axis) =>
        Convert.ToSingle(((Dictionary<string, object?>)plane["facing"]!)[axis]);

    static CanvasPath Path(Dictionary<string, object?> plane) => (CanvasPath)plane["path"]!;

    [Fact]
    public void TestTheBasicPlanesAreLoomissAndAllDrawn()
    {
        var planes = Planes(Head());
        string[] expected =
        [
            "top", "forehead", "farForeheadSide", "nearForeheadSide", "farSocket", "nearSocket", "noseFront",
            "farNoseSide", "nearNoseSide", "noseBottom", "farCheekFront", "nearCheekFront", "farJawSide",
            "nearJawSide", "muzzle", "chin"
        ];
        var names = ((Dictionary<string, object?>)planes["byName"]!).Keys.ToHashSet();
        Assert.True(names.SetEquals(expected), string.Join(", ", names));

        foreach (var name in expected)
        {
            var plane = Plane(planes, name);
            Assert.True((bool)plane["visible"]!, $"{name} is hidden on a frontal head");
            Assert.True(Path(plane).Area > 20f, $"{name} is empty");
        }
    }

    [Fact]
    public void TestSecondaryPlanesSplitTheForeheadCheeksNoseAndMuzzle()
    {
        var names = ((Dictionary<string, object?>)Planes(Head(), "secondary")["byName"]!).Keys.ToHashSet();
        foreach (var name in new[] { "farForehead", "forehead", "nearForehead", "farCheekUpper", "farCheekInner",
                                     "farCheekLower", "farNoseBottom", "nearNoseBottom", "farUpperLip", "lowerLip" })
            Assert.Contains(name, names);
        Assert.DoesNotContain("muzzle", names);
        Assert.DoesNotContain("farCheekFront", names);
    }

    [Fact]
    public void TestEveryPlaneIsInsideTheHead()
    {
        var head = Head(25f);
        var mass = (CanvasPath)Toolkit.CreateHeadGeometry(head)["mass"]!;
        foreach (var item in (List<object?>)Planes(head, "secondary")["planes"]!)
        {
            var plane = (Dictionary<string, object?>)item!;
            Assert.True(Path(plane).Subtract(mass).Area < 1f, $"{plane["name"]} spills outside the head");
        }
    }

    [Fact]
    public void TestAFrontalHeadsPlanesMirror()
    {
        var planes = Planes(Head());
        foreach (var name in new[] { "Socket", "NoseSide", "CheekFront", "JawSide", "ForeheadSide" })
        {
            var far = Plane(planes, "far" + name);
            var near = Plane(planes, "near" + name);
            Assert.Equal(Path(far).Area, Path(near).Area, 0.03f * Path(near).Area);
            Assert.Equal(-Facing(far, "x"), Facing(near, "x"), 3);
        }
    }

    /// <summary>Positive yaw turns the face toward the far side, so the far planes turn away and the near ones come round.</summary>
    [Fact]
    public void TestThePlanesTurnWithTheHead()
    {
        var front = Planes(Head());
        var turned = Planes(Head(45f));
        Assert.True(Facing(Plane(turned, "farJawSide"), "z") < Facing(Plane(front, "farJawSide"), "z") - 0.2f);
        Assert.True(Facing(Plane(turned, "nearJawSide"), "z") > Facing(Plane(front, "nearJawSide"), "z") + 0.2f);
        Assert.Equal(45f, Convert.ToSingle(turned["yawDeg"]), 1);
    }

    [Fact]
    public void TestThePlanesFollowAnExpression()
    {
        var head = Head();
        var open = Toolkit.ApplyActionUnits(head, new Dictionary<string, object?> { ["AU26"] = 1f });
        Assert.True(Path(Plane(Planes(open), "chin")).Path.Bounds.Bottom > Path(Plane(Planes(head), "chin")).Path.Bounds.Bottom + 5f);
    }

    /// <summary>A light from the left leaves the far side lit and puts the near side in shadow.</summary>
    [Fact]
    public void TestALightPicksWhichPlanesAreDark()
    {
        var canvas = new SkiaCanvas(800, 800);
        var lit = Toolkit.DrawHeadPlanes(canvas.GetContext("2d"), Planes(Head()), new Dictionary<string, object?> { ["light"] = 180f });
        var tones = ((List<object?>)lit["planes"]!).Cast<Dictionary<string, object?>>()
            .ToDictionary(p => (string)p["name"]!, p => Convert.ToSingle(p["tone"]));

        Assert.Equal(0f, tones["farJawSide"]);
        Assert.Equal(0.4f, tones["nearJawSide"], 3);
        Assert.True(tones["nearCheekFront"] >= tones["farCheekFront"]);
        Assert.True(tones["noseBottom"] > 0f, "the bottom of the nose faces down, away from a side light");
    }

    /// <summary>
    /// At 45° the side of the head and the jaw side below it face the same way, so one light shades them alike;
    /// a lit skull over a dark jaw read as a square cut out of the head.
    /// </summary>
    [Fact]
    public void TestATurnedHeadsSideIsOneShadow()
    {
        var head = Head(45f);
        var lit = Toolkit.DrawHeadPlanes(new SkiaCanvas(800, 800).GetContext("2d"), Planes(head),
            new Dictionary<string, object?> { ["light"] = -135f, ["values"] = 2 });
        var tones = ((List<object?>)lit["planes"]!).Cast<Dictionary<string, object?>>()
            .ToDictionary(p => (string)p["name"]!, p => Convert.ToSingle(p["tone"]));
        Assert.True(tones["nearJawSide"] > 0f);
        Assert.Equal(tones["nearJawSide"], tones["nearForeheadSide"]);

        // The jaw side ends at the ear rather than running on round the back of the skull.
        var ear = (Dictionary<string, object?>)((Dictionary<string, object?>)Toolkit.CreateHeadGeometry(head)["ears"]!)["far"]!;
        var earCenter = (Dictionary<string, object?>)ear["center"]!;
        var earFront = Convert.ToSingle(earCenter["x"]) - (Convert.ToSingle(ear["width"]) * 0.5f);
        Assert.True(Path(Plane(Planes(head), "nearJawSide")).Path.Bounds.Right <= earFront + 1f);
    }

    [Fact]
    public void TestAHeadIsLitAsItsBasicPlanes()
    {
        var lit = Toolkit.DrawHeadPlanes(new SkiaCanvas(800, 800).GetContext("2d"), Head(), null);
        Assert.Equal(16, ((List<object?>)lit["planes"]!).Count);
    }

    [Fact]
    public void TestBadOptionsAreRefusedByName()
    {
        Assert.Contains("detial", Assert.Throws<ArgumentException>(() =>
            Toolkit.CreateHeadPlanes(Head(), new Dictionary<string, object?> { ["detial"] = "basic" })).Message);
        Assert.Contains("tertiary", Assert.Throws<ArgumentException>(() =>
            Toolkit.CreateHeadPlanes(Head(), new Dictionary<string, object?> { ["detail"] = "tertiary" })).Message);
        Assert.Throws<ArgumentException>(() => Toolkit.DrawHeadPlanes(new SkiaCanvas(100, 100).GetContext("2d"), Head(),
            new Dictionary<string, object?> { ["values"] = 5 }));
    }
}
