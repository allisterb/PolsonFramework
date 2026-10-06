namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;

/// <summary>
/// <c>createHeadGeometry</c>'s default outline: Loomis's skull (<i>Drawing the Head and Hands</i>, Plates 1, 5,
/// 6 and 19) rather than his ball with a jaw hung under it, which is kept as <c>face: 'ball'</c>.
/// </summary>
public class LoomisSkullTests : TestsRuntime
{
    const float H = 700f;
    const float OriginX = 400f;

    static readonly ConstructiveDrawingToolkit Toolkit = new();

    static Dictionary<string, object?> Head(float yaw = 0f) => Toolkit.CreateLoomisHead(OriginX, 400f, H, yaw);

    static Dictionary<string, object?> Geometry(Dictionary<string, object?> head, string face = "loomis") =>
        Toolkit.CreateHeadGeometry(head, new Dictionary<string, object?> { ["neckLength"] = 0f, ["face"] = face });

    static CanvasPath Part(Dictionary<string, object?> geo, string name) =>
        (CanvasPath)((Dictionary<string, object?>)geo["parts"]!)[name]!;

    /// <summary>The head without its ears: the ears are not part of the widths Plate 19 gives.</summary>
    static CanvasPath Skull(Dictionary<string, object?> geo) =>
        Part(geo, "cranium").Union(Part(geo, "jaw")).Union(Part(geo, "farCheek")).Union(Part(geo, "nearCheek"));

    static float Y(Dictionary<string, object?> head, string key) => Convert.ToSingle(((Dictionary<string, object?>)head[key]!)["y"]);

    static float Width(CanvasPath path, float y)
    {
        float? left = null, right = null;
        for (var x = 0f; x <= 800f; x += 0.5f)
        {
            if (!path.Path.Contains(x, y)) continue;
            left ??= x;
            right = x;
        }

        return left is null ? 0f : right!.Value - left.Value;
    }

    /// <summary>How far the outline stands off the crown's axis, toward <paramref name="dir"/>.</summary>
    static float Reach(CanvasPath path, float y, float dir)
    {
        float inside = OriginX, outside = OriginX + (dir * H);
        for (var i = 0; i < 40; i++)
        {
            var mid = (inside + outside) * 0.5f;
            if (path.Path.Contains(mid, y)) inside = mid; else outside = mid;
        }

        return MathF.Abs(inside - OriginX);
    }

    /// <summary>
    /// The frontal head is as wide as Plate 19 draws it, row by row, and the ball it replaces is not.
    /// </summary>
    /// <remarks>
    /// In Loomis's units (a head is 3.5 tall). The Plate 19 widths are the studio's readings off the plate,
    /// ears excluded, so the tolerance is a tenth and a half of a unit, and wider at the jaw where the reading
    /// is least certain. The ball is measured beside it so this stays a test of the outline: it was 3.0
    /// units across at the brow and 2.96 at the eyes.
    /// </remarks>
    [Theory]
    [InlineData(1.25f, 2.65f, 0.15f)]     // temple
    [InlineData(1.75f, 2.5f, 0.15f)]      // eye line
    [InlineData(2.5f, 2.3f, 0.15f)]       // nose base
    [InlineData(2.833f, 2.0f, 0.15f)]     // mouth
    [InlineData(3.233f, 1.65f, 0.2f)]     // jaw
    public void TestTheFrontalHeadIsAsWideAsPlate19(float down, float plate, float tolerance)
    {
        var head = Head();
        var unit = H / 3.5f;
        var y = Y(head, "crown") + (down * unit);

        var width = Width(Skull(Geometry(head)), y) / unit;
        Assert.InRange(width, plate - tolerance, plate + tolerance);
    }

    [Fact]
    public void TestTheBallWasWiderThanPlate19AtTheEyes()
    {
        var head = Head();
        var unit = H / 3.5f;
        Assert.True(Width(Skull(Geometry(head, "ball")), Y(head, "crown") + (1.75f * unit)) / unit > 2.9f);
    }

    /// <summary>The sides are sliced flat: the head is narrower than the ball and no shorter.</summary>
    [Fact]
    public void TestTheSidesAreSliced()
    {
        var head = Head();
        var skull = Part(Geometry(head), "cranium").Path.Bounds;
        var ball = Part(Geometry(head, "ball"), "cranium").Path.Bounds;

        Assert.True(skull.Width < ball.Width * 0.9f, $"sliced {skull.Width:F0} against the ball's {ball.Width:F0}");
        Assert.Equal(ball.Top, skull.Top, 1);

        // Flat: the same width at the brow as a quarter of a unit above it.
        var unit = H / 3.5f;
        var brow = Y(head, "brow");
        var cranium = Part(Geometry(head), "cranium");
        Assert.InRange(Width(cranium, brow) - Width(cranium, brow - (unit * 0.25f)), -2f, 2f);
    }

    /// <summary>The outline steps nowhere from the brow to the chin, frontally or turned.</summary>
    /// <remarks>
    /// The defect the ball and jaw had: a cliff below the nose line, where the jaw hung under a ball much
    /// wider than it. Measured on the skull without ears, since an ear's lobe hanging free is anatomy. On
    /// the back of a turned head the ball's underside meets the far jaw angle under the ear, and the neck
    /// is what carries the outline on from there, so that side is measured with it.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(25f)]
    [InlineData(45f)]
    public void TestTheOutlineHasNoStep(float yaw)
    {
        var head = Head(yaw);
        var skull = Skull(Geometry(head));
        var withNeck = skull.Union(Part(Toolkit.CreateHeadGeometry(head), "neck"));
        float brow = Y(head, "brow"), chin = Y(head, "chin");
        var faceSide = MathF.Sign(Convert.ToSingle(((Dictionary<string, object?>)head["brow"]!)["x"]) - OriginX);
        foreach (var dir in new[] { -1f, 1f })
        {
            var outline = faceSide != 0f && dir != faceSide ? withNeck : skull;
            var previous = Reach(outline, brow, dir);
            for (var y = brow + 2f; y <= chin - 4f; y += 2f)
            {
                var here = Reach(outline, y, dir);
                Assert.True(previous - here < H * 0.012f, $"a {previous - here:F1}px step at y {y:F0}, side {dir}, yaw {yaw}");
                previous = here;
            }
        }
    }

    /// <summary>
    /// On a turned head the cheekbone, not the ball, makes the outline on the side the face turns toward.
    /// </summary>
    /// <remarks>
    /// The ball's front quarter below the brow is where the eye sockets and cheekbones are. Left whole, a
    /// three-quarter face sat inside a balloon.
    /// </remarks>
    [Fact]
    public void TestATurnedHeadsCheekboneMakesTheOutline()
    {
        var head = Head(45f);
        var geo = Geometry(head);
        var unit = H / 3.5f;
        var cheek = Y(head, "crown") + (2.0f * unit);
        var faceSide = MathF.Sign(Convert.ToSingle(((Dictionary<string, object?>)head["brow"]!)["x"]) - OriginX);

        Assert.True(Reach(Part(geo, "jaw"), cheek, faceSide) > Reach(Part(geo, "cranium"), cheek, faceSide) + (unit * 0.2f));
        // And the back of the head is still the ball, round to well below the brow.
        Assert.True(Reach(Part(geo, "cranium"), cheek, -faceSide) > Reach(Part(geo, "jaw"), cheek, -faceSide));
    }

    /// <summary>A frontal head is symmetric about its own axis.</summary>
    [Fact]
    public void TestAFrontalHeadIsSymmetric()
    {
        var head = Head();
        var skull = Skull(Geometry(head));
        foreach (var down in new[] { 1.25f, 2f, 2.5f, 3f, 3.4f })
        {
            var y = Y(head, "crown") + (down * H / 3.5f);
            Assert.Equal(Reach(skull, y, -1f), Reach(skull, y, 1f), 0);
        }
    }

    /// <summary>The character parameters reach the outline, since every station hangs off a landmark they move.</summary>
    [Fact]
    public void TestAWiderJawWidensTheOutline()
    {
        var head = Head();
        var wide = Toolkit.CreateParametricHead(head, new Dictionary<string, object?> { ["jawShape"] = 1f });
        var y = Y(head, "crown") + (3.1f * H / 3.5f);
        Assert.True(Width(Skull(Geometry(wide)), y) > Width(Skull(Geometry(head)), y) + 5f);
    }

    /// <summary>
    /// A turned head foreshortens the eye on the side it turns toward, and shows the ear on the other side.
    /// </summary>
    /// <remarks>
    /// Until 2026-10-06 the facial axis and nose swung toward +x while the far, narrowed eye sat at -x,
    /// beside the visible ear - so the eye that should stay full width was the one that narrowed.
    /// </remarks>
    [Theory]
    [InlineData(20f)]
    [InlineData(45f)]
    public void TestTheFaceTurnsTowardItsNarrowEye(float yaw)
    {
        var head = Head(yaw);
        float X(string group, string key) => Convert.ToSingle(((Dictionary<string, object?>)((Dictionary<string, object?>)head[group]!)[key]!)["x"]);
        float Width(string eye) => Convert.ToSingle(((Dictionary<string, object?>)head[eye]!)["width"]);

        var crownX = Convert.ToSingle(((Dictionary<string, object?>)head["crown"]!)["x"]);
        var faceSide = MathF.Sign(Convert.ToSingle(((Dictionary<string, object?>)head["brow"]!)["x"]) - crownX);

        Assert.True(Width("farEye") < Width("nearEye"));
        Assert.Equal(faceSide, MathF.Sign(X("farEye", "center") - crownX));
        Assert.Equal(faceSide, MathF.Sign(X("noseWedge", "apex") - X("noseWedge", "bridgeTop")));
        Assert.Equal(-faceSide, MathF.Sign(X("jaw", "ear") - crownX));
    }

    /// <summary>The ears sit on the side planes, and the near one goes behind the face as the head turns.</summary>
    [Fact]
    public void TestTheEarsSitOnTheSidePlanes()
    {
        var front = Geometry(Head());
        var ears = (Dictionary<string, object?>)front["ears"]!;
        var far = Convert.ToSingle(((Dictionary<string, object?>)((Dictionary<string, object?>)ears["far"]!)["center"]!)["x"]);
        var near = Convert.ToSingle(((Dictionary<string, object?>)((Dictionary<string, object?>)ears["near"]!)["center"]!)["x"]);
        Assert.Equal(OriginX - far, near - OriginX, 1);
        Assert.InRange(MathF.Abs(near - OriginX) / (H / 3.5f), 1.15f, 1.35f);

        var turned = (Dictionary<string, object?>)Geometry(Head(25f))["ears"]!;
        Assert.False((bool)((Dictionary<string, object?>)turned["near"]!)["visible"]!);
    }
}
