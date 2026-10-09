namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
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

        // 4: the whole form, in tone. Front-on that still has no line down the middle.
        var four = At(4);
        Assert.True(Drawn(four, "underPlane"));
        Assert.NotEmpty((System.Collections.IList)four["planes"]!);
        Assert.False(Drawn(four, "bridgeMark"));
    }

    /// <summary>
    /// None of Hamm's front-view noses has a line down the middle (p. 14). Turned, the edge of the front plane on the
    /// side the tip swings toward is the nose's profile (p. 15), and that is where the line goes, heavier as it turns.
    /// </summary>
    [Fact]
    public void TestTheBridgeLineIsTheProfileOfATurnedNose()
    {
        Assert.False(Drawn(Draw(Head(0f)), "bridgeMark"), "a front-view nose has no centre line");

        var head = Head(30f);
        var mark = Bounds(Draw(head, new() { ["treatment"] = "male1" }), "bridgeMark");
        var tip = P(Nose(head), "apex", "x");
        var axis = P(Nose(head), "underNose", "x");
        Assert.False(mark.IsEmpty);
        Assert.Equal(MathF.Sign(tip - axis), MathF.Sign(mark.MidX - axis));
        Assert.True(mark.Bottom > P(Nose(head), "apex", "y"), "it turns round under the tip");

        float Ink(float yaw) => ((CanvasPath)Draw(Head(yaw), new() { ["treatment"] = "male1" })["bridgeMark"]!).Area;
        Assert.True(Ink(30f) > Ink(10f) * 1.5f, "heavier as the head turns");
    }

    /// <summary>
    /// The tone is the solid lit: with no light only the underside darkens, the "little shadow" of Hamm's p. 14; with a
    /// light from one side the planes on the other side darken, and those are the side shadow.
    /// </summary>
    [Fact]
    public void TestTheToneIsTheSolidLit()
    {
        static IEnumerable<Dictionary<string, object?>> Planes(Dictionary<string, object?> parts) =>
            ((System.Collections.IList)parts["planes"]!).Cast<Dictionary<string, object?>>();
        static float Tone(Dictionary<string, object?> p) => Convert.ToSingle(p["tone"]);

        var head = Head();
        var unlit = Planes(Draw(head)).ToList();
        Assert.All(unlit.Where(p => Tone(p) > 0.01f), p => Assert.Equal("under", p["group"]));
        Assert.Contains(unlit, p => (string)p["group"]! == "under" && Tone(p) > 0.1f);

        var lit = Planes(Draw(head, new() { ["light"] = 0f })).ToList();         // from the right
        float SideTone(string side) => lit.Where(p => (string)p["side"]! == side && (string)p["group"]! is "side" or "wing").Max(Tone);
        Assert.True(SideTone("far") > 0.1f && SideTone("near") < 0.01f, "the side turned from the light darkens, the other does not");

        Assert.Equal(0f, Planes(Draw(head, new() { ["tone"] = 0f, ["light"] = 0f })).Max(Tone));
    }

    /// <summary>
    /// Hamm, p. 14: the male is usually more coarse, the female more delicate, and a line along the side of the
    /// female nose is best left out.
    /// </summary>
    [Fact]
    public void TestTheMaleNoseIsCoarserThanTheFemale()
    {
        var head = Head(30f);
        Dictionary<string, object?> As(string build) => Draw(head, new() { ["build"] = build, ["light"] = 0f, ["treatment"] = "male1" });
        static float Ink(Dictionary<string, object?> parts) => ((CanvasPath)parts["bridgeMark"]!).Area;
        Assert.True(Ink(As("male")) > Ink(As("female")) * 1.4f);
        Assert.True(Drawn(Draw(Head(), new() { ["light"] = 0f, ["build"] = "male" }), "sideLine"));
        Assert.False(Drawn(Draw(Head(), new() { ["light"] = 0f, ["build"] = "female" }), "sideLine"));
        Assert.Throws<ArgumentException>(() => Draw(head, new() { ["build"] = "boy" }));
    }

    /// <summary>
    /// Hamm's front-view noses are different choices of marks (p. 14), so each treatment draws its own and no others:
    /// female 1 is slashes, wings and nostril dashes; female 2 one long base line; female 3 one line down the side
    /// into the wing; male 3 a slash and a heavy bar.
    /// </summary>
    [Fact]
    public void TestEachTreatmentDrawsItsOwnMarks()
    {
        var head = Head();
        Dictionary<string, object?> As(string treatment) => Draw(head, new() { ["treatment"] = treatment });
        static string[] Marks(Dictionary<string, object?> parts) =>
            ((System.Collections.IList)parts["marks"]!).Cast<object>().Select(m => m.ToString()!).ToArray();

        var standard = Draw(head);
        Assert.Equal("female1", standard["treatment"]);
        Assert.Equal(["slashes:both", "wings:both", "nostrils:both"], Marks(standard));
        Assert.True(Drawn(standard, "nostril") && Drawn(standard, "nostrilHole") && Drawn(standard, "depressions"));
        Assert.False(Drawn(standard, "base") || Drawn(standard, "noseLine") || Drawn(standard, "shadowMark"));

        var lost = As("female2");
        Assert.True(Drawn(lost, "base"));
        Assert.False(Drawn(lost, "nostril") || Drawn(lost, "nostrilHole"), "the cavities are lost in the line");
        Assert.True(Bounds(lost, "base").Width > MathF.Abs(P(Nose(head), "nearNostril", "x") - P(Nose(head), "farNostril", "x")) * 0.8f,
            "one line from wing to wing");

        // Female 3: one line down the shadow side, his light from the top right, so on the left, curling into the wing.
        var line = Bounds(As("female3"), "noseLine");
        Assert.True(line.MidX < P(Nose(head), "underNose", "x"));
        Assert.True(line.Top < P(Nose(head), "apex", "y") - (P(Nose(head), "underNose", "y") - P(Nose(head), "bridgeTop", "y")) * 0.5f);
        Assert.True(line.Bottom > P(Nose(head), "apex", "y"));

        var bar = As("male3");
        Assert.True(((CanvasPath)bar["base"]!).Area > P(Nose(head), "underNose", "y") - P(Nose(head), "bridgeTop", "y"), "a heavy bar, filled");
        Assert.True(Drawn(As("female8"), "shadowMark") && Drawn(As("female7"), "hatch"));

        foreach (var name in new[] { "female1", "female2", "female3", "female4", "female5", "female6", "female7", "female8",
                                     "male1", "male2", "male3", "male4", "male5", "male6", "male7", "male8" })
            Assert.NotEmpty(Marks(As(name)));
        Assert.Throws<ArgumentException>(() => As("female9"));
    }

    /// <summary>
    /// "Never put in two round holes by themselves" (p. 14): the inner nostril is a dash or a concave arc.
    /// </summary>
    [Fact]
    public void TestTheNostrilsAreDashesNotHoles()
    {
        var hole = Bounds(Draw(Head()), "nostrilHole");
        Assert.True(hole.Width > hole.Height * 2f, $"a dash, {hole.Width:F1} by {hole.Height:F1}");
    }

    /// <summary>`marks` composes a nose from the same marks; a side can be named, and an unknown mark is refused.</summary>
    [Fact]
    public void TestMarksComposeANose()
    {
        var head = Head();
        var parts = Draw(head, new() { ["marks"] = new object[] { "slashes", "noseLine:near", "baseBar" } });
        Assert.Equal("custom", parts["treatment"]);
        Assert.True(Drawn(parts, "noseLine") && Drawn(parts, "base") && Drawn(parts, "depressions"));
        Assert.False(Drawn(parts, "nostril"));
        Assert.True(Bounds(parts, "noseLine").MidX > P(Nose(head), "underNose", "x"), "the near side");

        Assert.Throws<ArgumentException>(() => Draw(head, new() { ["marks"] = new object[] { "freckles" } }));
        Assert.Throws<ArgumentException>(() => Draw(head, new() { ["marks"] = new object[] { "wings:up" } }));
        Assert.Throws<ArgumentException>(() => Draw(head, new() { ["marks"] = new object[] { "wings" }, ["treatment"] = "male1" }));
    }

    /// <summary>
    /// The solid's planes face where a nose's do, and the head's own planes are the solid's, so they agree.
    /// </summary>
    [Fact]
    public void TestTheNoseSolidFacesWhereANoseDoes()
    {
        var head = Head(25f);
        var solid = Toolkit.CreateNoseSolid(Nose(head));
        var planes = ((System.Collections.IList)solid["planes"]!).Cast<Dictionary<string, object?>>().ToDictionary(p => (string)p["name"]!);
        float N(string name, string axis) => Convert.ToSingle(((Dictionary<string, object?>)planes[name]["normal"]!)[axis]);

        Assert.True(N("under", "y") > 0.8f, "the underside faces down");
        Assert.True(N("bridge", "z") > 0.8f, "the bridge faces out");
        Assert.True(N("nearBridge", "x") > 0.6f && N("farBridge", "x") < -0.6f, "the sides face sideways, nearly perpendicular to the face");
        Assert.True(N("nearWing", "x") > 0.3f && N("nearWing", "z") > 0f);

        // Turned 25 degrees toward the far side, the far side of the bridge goes edge-on before the near side does.
        float F(string name) => Convert.ToSingle(((Dictionary<string, object?>)planes[name]["facing"]!)["z"]);
        Assert.True(F("farBridge") < F("nearBridge"));

        // The tip of the solid lands on the construction's own apex.
        Assert.Equal(P(Nose(head), "apex", "x"), Convert.ToSingle(((Dictionary<string, object?>)solid["tip"]!)["x"]), 1f);

        var headPlanes = (Dictionary<string, object?>)Toolkit.CreateHeadPlanes(head)["byName"]!;
        var bottom = (Dictionary<string, object?>)headPlanes["noseBottom"]!;
        Assert.True(Convert.ToSingle(((Dictionary<string, object?>)bottom["normal"]!)["y"]) > 0.8f);
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
        Dictionary<string, object?> With(string key, float value, string treatment = "female1") => Draw(head, new() { [key] = value, ["treatment"] = treatment });
        static float Area(Dictionary<string, object?> parts, string key) => ((CanvasPath)parts[key]!).Area;
        var plain = Draw(head);
        var plainBase = Draw(head, new() { ["treatment"] = "female5" });    // a treatment with Hamm's base line

        var large = With("ball", 1f);
        Assert.True(Bounds(large, "nostril").Height < Bounds(plain, "nostril").Height * 0.85f, "a large ball does not crowd the wings");
        Assert.True(Bounds(large, "underPlane").Height > Bounds(plain, "underPlane").Height * 1.3f, "a large ball's underside is no taller");

        Assert.True(Area(With("nostrils", 1f), "nostrilHole") > Area(plain, "nostrilHole") * 1.8f, "exposed nostrils are not larger");
        Assert.True(Area(With("nostrils", -1f), "nostrilHole") < Area(plain, "nostrilHole") * 0.3f, "hidden nostrils are not smaller");
        Assert.Equal(Area(plain, "nostrilHole"), Area(With("ball", -1f), "nostrilHole"), 0.5f);

        var septum = P(Nose(head), "underNose", "y");
        var len = septum - P(Nose(head), "bridgeTop", "y");
        Assert.True(Bounds(With("septum", 1f, "female5"), "base").Bottom > Bounds(plainBase, "base").Bottom + (len * 0.03f), "a low septum does not hang");
        var tucked = Bounds(With("septum", -1f, "female5"), "base");
        Assert.True(tucked.Bottom >= septum - 1f, "a tucked septum arches the base");
        Assert.True(tucked.Height < Bounds(plainBase, "base").Height, "a tucked septum does not flatten the base");
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
