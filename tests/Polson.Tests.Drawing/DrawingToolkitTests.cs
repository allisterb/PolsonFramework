namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

public class DrawingToolkitTests : TestsRuntime
{
    #region Constructive Toolkit Tests
    /// <summary>
    /// The head follows Loomis's 3.5-unit scale (Drawing the Head and Hands, Plate 18): half a unit of
    /// dome, three equal units, and the eye line at exactly half the head. Manual 01 §2 publishes these
    /// as the canon, so they are pinned here — a change to the scale fails this and forces the manual
    /// to move with it.
    /// </summary>
    /// <remarks>
    /// Replaced an earlier canon in which the eye line sat at <c>H/2 + 0.02H</c>, the three divisions
    /// measured 0.230 / 0.250 / 0.300 H rather than being equal, and <c>unit.thirdH</c> was <c>H/3</c> —
    /// the height of none of them. See the Manual 01 audit for what each of those cost.
    /// </remarks>
    [Fact]
    public void TestLoomisHeadFollowsTheThreeAndAHalfUnitScale()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateLoomisHead(500f, 400f, 210f, 0f, 0f);

        float Y(string key) => Convert.ToSingle(((Dictionary<string, object?>)head[key]!)["y"]);

        var crown = Y("crown");
        float Units(float y) => (y - crown) / (210f / 3.5f);

        Assert.Equal(0.00f, Units(crown), 3);
        Assert.Equal(0.50f, Units(Y("hairline")), 3);
        Assert.Equal(1.50f, Units(Y("brow")), 3);
        Assert.Equal(1.75f, Units(Convert.ToSingle(head["eyeLineY"])), 3);
        Assert.Equal(2.50f, Units(Y("noseBase")), 3);
        Assert.Equal(2.50f + 1f / 3f, Units(Y("mouthCenter")), 3);
        Assert.Equal(3.50f, Units(Y("chin")), 3);

        // The eye line is exactly half the head, which is the one proportion Loomis states outright.
        Assert.Equal(400f, Convert.ToSingle(head["eyeLineY"]), 3);
        Assert.Equal(0.5f, (Convert.ToSingle(head["eyeLineY"]) - crown) / 210f, 4);

        // The three divisions are equal, and unit.thirdH is that height rather than H/3.
        var unit = (Dictionary<string, object?>)head["unit"]!;
        var thirdH = Convert.ToSingle(unit["thirdH"]);
        Assert.Equal(210f / 3.5f, thirdH, 3);
        Assert.Equal(thirdH, Y("brow") - Y("hairline"), 3);
        Assert.Equal(thirdH, Y("noseBase") - Y("brow"), 3);
        Assert.Equal(thirdH, Y("chin") - Y("noseBase"), 3);

        // Dome above the hairline is half a unit; the head is 3 units wide and 6 eye-widths.
        Assert.Equal(thirdH * 0.5f, Y("hairline") - crown, 3);
        Assert.Equal(thirdH * 3f, Convert.ToSingle(unit["W"]), 3);
        Assert.Equal(6f, Convert.ToSingle(unit["W"]) / Convert.ToSingle(unit["eyeW"]), 3);
    }

    /// <summary>
    /// Plate 19's half-unit spacing, read off the model: each eye one eye-width across, the inner
    /// corners one eye-width apart, and the wings of the nose in line with them.
    /// </summary>
    [Fact]
    public void TestLoomisHeadEyeAndNoseSpacingIsOnTheHalfUnitGrid()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateLoomisHead(500f, 400f, 210f, 0f, 0f);

        var unit = (Dictionary<string, object?>)head["unit"]!;
        var eyeW = Convert.ToSingle(unit["eyeW"]);

        float X(string group, string point) =>
            Convert.ToSingle(((Dictionary<string, object?>)
                ((Dictionary<string, object?>)head[group]!)[point]!)["x"]);

        // At yaw 0 the pair is symmetric about the facial axis.
        Assert.Equal(eyeW, X("nearEye", "outer") - X("nearEye", "inner"), 3);
        Assert.Equal(eyeW, X("farEye", "inner") - X("farEye", "outer"), 3);
        Assert.Equal(eyeW, X("nearEye", "inner") - X("farEye", "inner"), 3);

        // The near nostril sits on the same half-unit line as the near inner corner.
        Assert.Equal(X("nearEye", "inner"), X("noseWedge", "nearNostril"), 3);

        // The ear is a unit off the cranium axis at full front, so its outer edge lands on the head's
        // half-width, and it swings inward with the turn rather than staying pinned to the front view.
        var thirdH = Convert.ToSingle(unit["thirdH"]);
        Assert.Equal(500f - thirdH, X("jaw", "ear"), 3);

        var turned = toolkit.CreateLoomisHead(500f, 400f, 210f, 55f, 0f);
        var turnedEar = Convert.ToSingle(((Dictionary<string, object?>)
            ((Dictionary<string, object?>)turned["jaw"]!)["ear"]!)["x"]);
        Assert.Equal(500f - thirdH * MathF.Cos(55f * MathF.PI / 180f), turnedEar, 3);
        Assert.True(turnedEar > X("jaw", "ear"), "the ear must come in toward the axis as the head turns.");
    }

    /// <summary>
    /// Plate 1 has the jaw line connect about halfway around the ball ON EACH SIDE, so the jaw has two
    /// stations and the chin sits between the two angles they lead to. The ordering is the property that
    /// matters: it used to break, and the drawing turned inside out without erroring.
    /// </summary>
    /// <remarks>
    /// The old path ran ear -> angle -> chin -> cheekApex, and cheekApex is on the FAR side, so it doubled
    /// back across the face and closed into a narrow V with a pointed chin. Nothing caught it because
    /// nothing asserted on the shape; it was found by rendering three yaw angles and looking.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(35f)]
    [InlineData(55f)]
    [InlineData(70f)]
    [InlineData(85f)]
    public void TestLoomisJawStaysOrderedAcrossTheTurn(float yaw)
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateLoomisHead(500f, 400f, 210f, yaw, 0f);
        var jaw = (Dictionary<string, object?>)head["jaw"]!;

        float X(string k) => Convert.ToSingle(((Dictionary<string, object?>)jaw[k]!)["x"]);

        // Left to right: far station, far angle, far chin corner, near chin corner, near angle, near
        // station. Any inversion here is the path doubling back on itself.
        Assert.True(X("farStation") <= X("angle"), "far station must sit outboard of the far jaw angle.");
        Assert.True(X("angle") < X("chinFar"), "the far jaw angle must sit outboard of the chin.");
        Assert.True(X("chinFar") < X("chinNear"), $"the chin inverted at yaw {yaw}.");
        Assert.True(X("chinNear") < X("nearAngle"), "the near jaw angle must sit outboard of the chin.");
        Assert.True(X("nearAngle") <= X("nearStation"), "near station must sit outboard of the near angle.");

        // The chin is centred on the facial axis, and stays inside the jaw it hangs from.
        var chinX = Convert.ToSingle(((Dictionary<string, object?>)jaw["chin"]!)["x"]);
        Assert.InRange(chinX, X("chinFar"), X("chinNear"));
    }

    /// <summary>At full front the jaw is symmetric about the cranium axis, and it shortens as it turns.</summary>
    [Fact]
    public void TestLoomisJawIsSymmetricAtFullFrontAndShortensWithTheTurn()
    {
        var toolkit = new ConstructiveDrawingToolkit();

        float Span(float yaw)
        {
            var jaw = (Dictionary<string, object?>)toolkit.CreateLoomisHead(500f, 400f, 210f, yaw, 0f)["jaw"]!;
            float X(string k) => Convert.ToSingle(((Dictionary<string, object?>)jaw[k]!)["x"]);
            return X("nearStation") - X("farStation");
        }

        var front = (Dictionary<string, object?>)toolkit.CreateLoomisHead(500f, 400f, 210f, 0f, 0f)["jaw"]!;
        float F(string k) => Convert.ToSingle(((Dictionary<string, object?>)front[k]!)["x"]);
        Assert.Equal(500f - F("farStation"), F("nearStation") - 500f, 3);

        Assert.True(Span(35f) < Span(0f), "the jaw must foreshorten as the head turns.");
        Assert.True(Span(55f) < Span(35f), "the jaw must go on foreshortening.");
    }

    #region Hand Tests
    /// <summary>
    /// Loomis's scale, from Plate 79: the middle finger measured from its back knuckle is slightly over
    /// half the hand, the palm is the rest, and the palm is slightly more than half the hand wide.
    /// </summary>
    [Fact]
    public void TestHandFollowsTheTwoUnitScale()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var hand = toolkit.CreateHandFigure(400f, 500f, 200f, null);
        var unit = (Dictionary<string, object?>)hand["unit"]!;

        Assert.Equal(200f, Convert.ToSingle(unit["length"]), 3);
        Assert.Equal(0.52f, Convert.ToSingle(unit["middleLength"]) / 200f, 3);
        Assert.Equal(0.48f, Convert.ToSingle(unit["palmLength"]) / 200f, 3);
        Assert.Equal(0.55f, Convert.ToSingle(unit["palmWidth"]) / 200f, 3);

        // The middle finger and the palm are the whole hand between them.
        Assert.Equal(200f, Convert.ToSingle(unit["middleLength"]) + Convert.ToSingle(unit["palmLength"]), 2);

        // "Slightly over half", and the palm slightly wider than it is long - both of them Loomis's.
        Assert.True(Convert.ToSingle(unit["middleLength"]) > 100f);
        Assert.True(Convert.ToSingle(unit["palmWidth"]) > Convert.ToSingle(unit["palmLength"]));
    }

    /// <summary>
    /// The three other fingers are given as reaches: the index to the middle's fingernail, the ring
    /// about equal to the index, the little finger to the ring's top knuckle.
    /// </summary>
    [Fact]
    public void TestHandFingerLengthsSatisfyLoomisReaches()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var hand = toolkit.CreateHandFigure(400f, 500f, 200f, null);
        var fingers = (IList)hand["fingers"]!;

        float Len(int i) => Convert.ToSingle(((Dictionary<string, object?>)fingers[i]!)["length"]);
        string Name(int i) => (string)((Dictionary<string, object?>)fingers[i]!)["name"]!;

        Assert.Equal(new[] { "index", "middle", "ring", "little" }, new[] { Name(0), Name(1), Name(2), Name(3) });

        Assert.True(Len(1) > Len(0), "the middle finger is the longest.");
        Assert.Equal(Len(0), Len(2), 2);                       // ring about equal to the index
        Assert.True(Len(3) < Len(2), "the little finger is the shortest.");

        // The index stops just short of the middle's tip - a fingernail, not a phalanx.
        var shortfall = (Len(1) - Len(0)) / Len(1);
        Assert.InRange(shortfall, 0.05f, 0.15f);

        // The little finger stops exactly one distal phalanx short of the ring's tip - Loomis's reach,
        // made computable by Hampton's 3:2 ratio for the bones within a finger (4/19 of the finger).
        Assert.Equal(4f / 19f, (Len(2) - Len(3)) / Len(2), 4);
    }

    /// <summary>
    /// Plate 79: the knuckles make a flat curve across the back, and the curves deepen row by row toward
    /// the fingertips. It is a consequence of the fingers differing in length, so it also tests that they
    /// still do - flatten the ratios to equal and this is the assertion that fails.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(7f)]
    [InlineData(14f)]
    public void TestHandArcsDeepenTowardTheFingertips(float spread)
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var hand = toolkit.CreateHandFigure(400f, 500f, 200f,
            new Dictionary<string, object?> { ["spreadDeg"] = spread });
        var fingers = (IList)hand["fingers"]!;

        // Sagitta of a row: how far its middle departs from the chord across its ends.
        float Sagitta(Func<Dictionary<string, object?>, Dictionary<string, object?>> pick)
        {
            var ys = new List<float>();
            foreach (var f in fingers) ys.Add(Convert.ToSingle(pick((Dictionary<string, object?>)f!)["y"]));
            var chord = (ys[0] + ys[^1]) * 0.5f;
            return MathF.Abs(((ys[1] + ys[2]) * 0.5f) - chord);
        }

        var knuckles = Sagitta(f => (Dictionary<string, object?>)f["knuckle"]!);
        var row1 = Sagitta(f => (Dictionary<string, object?>)((IList)f["joints"]!)[0]!);
        var tips = Sagitta(f => (Dictionary<string, object?>)((IList)f["joints"]!)[2]!);

        Assert.True(row1 > knuckles, $"the first joint row must bow more than the knuckles ({row1:F2} vs {knuckles:F2}).");
        Assert.True(tips > row1, $"the fingertip row must be the deepest ({tips:F2} vs {row1:F2}).");
    }

    /// <summary>
    /// The thumb is drawn off the hand's long axis rather than at a literal right angle - "at right
    /// angles" is Loomis describing the plane it moves in. A literal 90 puts it out of the wrist.
    /// </summary>
    [Fact]
    public void TestHandThumbIsAngledNotPerpendicular()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var hand = toolkit.CreateHandFigure(400f, 500f, 200f, null);
        var thumb = (Dictionary<string, object?>)hand["thumb"]!;
        var wrist = (Dictionary<string, object?>)hand["wrist"]!;

        var basePt = (Dictionary<string, object?>)thumb["base"]!;
        var tip = (Dictionary<string, object?>)thumb["tip"]!;

        // The thumb leaves the palm outward and upward, so its tip is above its base and outboard of it.
        Assert.True(Convert.ToSingle(tip["y"]) < Convert.ToSingle(basePt["y"]), "the thumb must rise toward its tip.");
        Assert.True(Convert.ToSingle(tip["x"]) > Convert.ToSingle(basePt["x"]), "the thumb must travel outward.");

        // ...and it starts partway up the palm, not at the wrist.
        Assert.True(Convert.ToSingle(basePt["y"]) < Convert.ToSingle(wrist["y"]),
            "the thumb's base sits up the palm, not on the wrist line.");
    }

    /// <summary>A left hand is the right one mirrored: the thumb goes the other way, the fingers do not reorder.</summary>
    [Fact]
    public void TestHandMirrorsForTheLeftSide()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var right = toolkit.CreateHandFigure(400f, 500f, 200f, new Dictionary<string, object?> { ["side"] = "right" });
        var left = toolkit.CreateHandFigure(400f, 500f, 200f, new Dictionary<string, object?> { ["side"] = "left" });

        float ThumbX(Dictionary<string, object?> h) =>
            Convert.ToSingle(((Dictionary<string, object?>)((Dictionary<string, object?>)h["thumb"]!)["tip"]!)["x"]);

        Assert.True(ThumbX(right) > 400f, "a right hand's thumb goes right of the wrist.");
        Assert.True(ThumbX(left) < 400f, "a left hand's thumb goes left of it.");
        Assert.Equal(ThumbX(right) - 400f, 400f - ThumbX(left), 3);

        var rf = (IList)right["fingers"]!;
        var lf = (IList)left["fingers"]!;
        Assert.Equal(
            ((Dictionary<string, object?>)rf[0]!)["name"],
            ((Dictionary<string, object?>)lf[0]!)["name"]);
    }

    /// <summary>Both renderers put ink on the canvas for every pose the options allow.</summary>
    [Theory]
    [InlineData(0f, 0f, "right", false)]
    [InlineData(0f, 18f, "right", false)]
    [InlineData(-35f, 0f, "left", true)]
    [InlineData(120f, 25f, "left", true)]
    public void TestHandRenderersDraw(float rotation, float curl, string side, bool solid)
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canvas = new SkiaCanvas(500, 500);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#ffffff";
        ctx.FillRect(0, 0, 500, 500);

        var hand = toolkit.CreateHandFigure(250f, 380f, 220f, new Dictionary<string, object?>
        {
            ["rotationDeg"] = rotation,
            ["curlDeg"] = curl,
            ["side"] = side
        });

        if (solid) toolkit.DrawHandSolid(ctx, hand, null);
        else toolkit.DrawHandWireframe(ctx, hand, null);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 100);

        // Something other than the white ground actually landed.
        var bitmap = canvas.ToBitmap();
        var palette = bitmap.Palette(4, null);
        Assert.True(palette.Count() > 1, "the hand rendered nothing but background.");
    }
    #endregion

    #region Torso Musculature Tests
    /// <summary>
    /// Renders a mannequin, then the same one with musculature over it, and reports where the two
    /// differ. Used to check that a muscle is drawn where it should be rather than not at all.
    /// </summary>
    private static Dictionary<string, object> MusculatureFootprint(float shoulderTiltDeg)
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var figure = toolkit.CreateMannequinFigure(250f, 60f, 340f, new Dictionary<string, object?>
        {
            ["shoulderTiltDeg"] = shoulderTiltDeg
        });

        SkiaBitmapWrapper Render(bool withMuscles)
        {
            var canvas = new SkiaCanvas(500, 460);
            var ctx = canvas.GetContext("2d");
            ctx.FillStyle = "#ffffff";
            ctx.FillRect(0, 0, 500, 460);
            toolkit.DrawMannequinWireframe(ctx, figure, null);
            if (withMuscles) toolkit.DrawTorsoMusculature(ctx, figure, null);
            return canvas.ToBitmap();
        }

        return Render(false).Diff(Render(true), null);
    }

    /// <summary>
    /// The SDK reference has always said this call renders deltoids and sternomastoid cords. Until
    /// 2026-09-02 it drew neither — only clavicles, pectorals and two flat abdominal tiers — so the
    /// promise is now pinned rather than left to the prose.
    /// </summary>
    [Fact]
    public void TestTorsoMusculatureReachesTheShouldersAndTheNeck()
    {
        var diff = MusculatureFootprint(0f);
        Assert.False((bool)diff["identical"], "the musculature pass drew nothing at all.");

        var bounds = (Dictionary<string, object>)diff["bounds"]!;
        var left = Convert.ToSingle(bounds["x"]);
        var width = Convert.ToSingle(bounds["width"]);
        var top = Convert.ToSingle(bounds["y"]);

        // The deltoids put ink well outside the ribcage on both sides of the centre line at x=250.
        Assert.True(left < 210f, $"nothing was drawn out at the left shoulder (leftmost ink at {left:F0}).");
        Assert.True(left + width > 290f, $"nothing was drawn out at the right shoulder (rightmost ink at {left + width:F0}).");

        // The sternomastoid runs up into the neck, above where the clavicles sit.
        var figure = new ConstructiveDrawingToolkit().CreateMannequinFigure(250f, 60f, 340f, null);
        var neckY = Convert.ToSingle(((Dictionary<string, object?>)figure["neck"]!)["y"]);
        Assert.True(top <= neckY + 4f, $"nothing was drawn up at the neck (topmost ink at {top:F0}, neck at {neckY:F0}).");
    }

    /// <summary>
    /// Hampton's active/passive rule: an active shape squashes and a passive one stretches, so a tilted
    /// figure must not get the same musculature as an upright one. Drawing them symmetrically is what
    /// kills the gesture, and it is invisible in a still image unless something asserts on it.
    /// </summary>
    [Fact]
    public void TestTorsoMusculatureRespondsToTheShoulderTilt()
    {
        var toolkit = new ConstructiveDrawingToolkit();

        SkiaBitmapWrapper Render(float tilt)
        {
            var figure = toolkit.CreateMannequinFigure(250f, 60f, 340f, new Dictionary<string, object?>
            {
                ["shoulderTiltDeg"] = tilt
            });
            var canvas = new SkiaCanvas(500, 460);
            var ctx = canvas.GetContext("2d");
            ctx.FillStyle = "#ffffff";
            ctx.FillRect(0, 0, 500, 460);
            toolkit.DrawTorsoMusculature(ctx, figure, null);
            return canvas.ToBitmap();
        }

        // Opposite tilts are mirror poses, so the musculature must differ between them.
        var diff = Render(20f).Diff(Render(-20f), null);
        Assert.False((bool)diff["identical"], "the musculature ignored the pose's tilt entirely.");
        Assert.True(Convert.ToSingle(diff["similarity"]) < 0.999f,
            "the musculature barely moved with the tilt; the squash/stretch is not reaching it.");
    }
    #endregion

    [Fact]
    public void TestLoomisHeadParametricCalculation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateLoomisHead(500, 400, 200, 35f, 0f);

        Assert.NotNull(head);
        Assert.True(head.ContainsKey("unit"));
        Assert.True(head.ContainsKey("nearEye"));
        Assert.True(head.ContainsKey("farEye"));
        Assert.True(head.ContainsKey("noseWedge"));
        Assert.True(head.ContainsKey("mouthGuides"));
        Assert.True(head.ContainsKey("jaw"));

        var unit = (Dictionary<string, object?>)head["unit"]!;
        Assert.Equal(200f, Convert.ToSingle(unit["H"]));
        Assert.Equal(200f * 3f / 3.5f, Convert.ToSingle(unit["W"]), 3); // 3 units wide, 3.5 units tall

        var chin = (Dictionary<string, object?>)head["chin"]!;
        Assert.True(Convert.ToSingle(chin["y"]) > 400f);
    }

    [Fact]
    public void TestLoomisWireframeRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateLoomisHead(400, 300, 220, 30f, 0f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        toolkit.DrawLoomisWireframe(ctx, head);

        // Verify non-empty raster rendering
        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestComicEyeNoseMouthRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateLoomisHead(400, 300, 200, 35f, 0f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        toolkit.DrawComicEye(ctx, head["nearEye"]!, false);
        toolkit.DrawComicEye(ctx, head["farEye"]!, true);
        toolkit.DrawComicNose(ctx, head["noseWedge"]!);
        toolkit.DrawComicMouth(ctx, head["mouthGuides"]!);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestTaperedStrokeRendering()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.GetContext("2d");

        ctx.DrawTaperedStroke(50, 50, 150, 20, 250, 180, 350, 350, 8f, "#0a0a0c");

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestFeatheringAndCrossContour()
    {
        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.GetContext("2d");

        ctx.DrawFeathering(100, 100, 45f, 10, 30f, 6f, "#0a0a0c", 1.5f);
        ctx.DrawCrossContourHatch(200, 200, 80f, 120f, 0f, MathF.PI, 6, "#555555", 1.2f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void Test3DHairRibbonRendering()
    {
        var canvas = new SkiaCanvas(500, 500);
        var ctx = canvas.GetContext("2d");

        ctx.DrawHairRibbon(
            new Dictionary<string, object?> { ["x"] = 250f, ["y"] = 100f },
            new Dictionary<string, object?> { ["x"] = 50f, ["y"] = 350f },
            40f,
            25f,
            "#c65727",
            "#7e2c12",
            "#0a0a0c",
            2.5f
        );

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestHalftoneDotShaderPreset()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var shader = toolkit.CreateHalftoneDotShader(new Dictionary<string, object?>
        {
            ["dotSpacing"] = 6.0f,
            ["shadowColor"] = "#b06f4c",
            ["resolution"] = new[] { 800f, 600f }
        });

        Assert.NotNull(shader);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = shader;
        ctx.FillRect(0, 0, 800, 600);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestRopeAndCloudShaderPresets()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var ropeShader = toolkit.CreateRopeFiberShader();
        var cloudShader = toolkit.CreateAtmosphericCloudShader();

        Assert.NotNull(ropeShader);
        Assert.NotNull(cloudShader);

        var canvas = new SkiaCanvas(400, 400);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = ropeShader;
        ctx.FillRect(0, 0, 200, 400);
        ctx.FillStyle = cloudShader;
        ctx.FillRect(200, 0, 200, 400);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPlumbAndRelativeDistanceMeasurements()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var pt1 = new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 50f };
        var pt2 = new Dictionary<string, object?> { ["x"] = 105f, ["y"] = 250f };
        var pt3 = new Dictionary<string, object?> { ["x"] = 140f, ["y"] = 250f };

        var check1 = toolkit.VerifyPlumbAlignment(pt1, pt2, 10f);
        Assert.True((bool)check1["aligned"]!);

        var check2 = toolkit.VerifyPlumbAlignment(pt1, pt3, 10f);
        Assert.False((bool)check2["aligned"]!);

        var relDist = toolkit.ComputeRelativeDistance(200f, pt1, pt2);
        Assert.True(relDist > 0.9f && relDist < 1.1f);
    }

    [Fact]
    public void TestJavaScriptEngineDrawingGlobals()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Loomis Parametric Head
            const head = Drawing.createLoomisHead(400, 300, 220, 35, 0);
            Drawing.drawLoomisWireframe(ctx, head);

            // 2. Comic Features
            Drawing.drawComicEye(ctx, head.nearEye, false);
            Drawing.drawComicEye(ctx, head.farEye, true);
            Drawing.drawComicNose(ctx, head.noseWedge);
            Drawing.drawComicMouth(ctx, head.mouthGuides);

            // 3. Hair Ribbon
            Drawing.drawHairRibbon(ctx, {x: 350, y: 200}, {x: 120, y: 380}, 45, 28, '#c65727', '#7e2c12', '#0a0a0c', 2.5);

            // 4. Halftone Dot Cel-Shading
            const shadowShader = Drawing.createHalftoneDotShader({ dotSpacing: 6, shadowColor: '#b06f4c' });
            ctx.fillStyle = shadowShader;
            ctx.fillRect(300, 350, 100, 100);

            // 5. CSI Curve Aliases & Tapered Stroke
            ctx.beginPath();
            ctx.moveTo(100, 100);
            ctx.cCurveTo(200, 50, 300, 100);
            ctx.sCurveTo(350, 150, 400, 80, 500, 120);
            ctx.stroke();

            ctx.drawTaperedStroke(50, 500, 150, 450, 250, 550, 350, 500, 6, '#0a0a0c');
            ctx.drawFeathering(400, 450, -45, 8, 25, 5, '#0a0a0c', 1.2);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }

    [Fact]
    public void TestPerspectiveGridCalculation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var grid2p = toolkit.CreatePerspectiveGrid(new Dictionary<string, object?>
        {
            ["type"] = "2point",
            ["horizonY"] = 300f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f,
            ["cameraAngleDeg"] = 45f
        });

        Assert.NotNull(grid2p);
        Assert.Equal("2point", grid2p["type"]);
        var vpL = (Dictionary<string, object?>)grid2p["vpL"]!;
        var vpR = (Dictionary<string, object?>)grid2p["vpR"]!;

        // In 45 degree angle with d=800, vpL = 400 - 800 = -400, vpR = 400 + 800 = 1200
        Assert.Equal(-400f, Convert.ToSingle(vpL["x"]));
        Assert.Equal(1200f, Convert.ToSingle(vpR["x"]));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");
        toolkit.DrawPerspectiveGrid(ctx, grid2p);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    /// <summary>
    /// A side longer than 85% of the anchor-to-VP distance used to be clamped, so an over-large box came
    /// back silently shortened and looked deliberate. It now refuses, and the message carries the measured
    /// fraction and the largest extent that would have been accepted.
    /// </summary>
    [Fact]
    public void TestPerspectiveBoxRefusesASideThatReachesItsVanishingPoint()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var grid = toolkit.CreatePerspectiveGrid(new Dictionary<string, object?>
        {
            ["horizonY"] = 250f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f,
            ["cameraAngleDeg"] = 40f
        });

        var vpL = (Dictionary<string, object?>)grid["vpL"]!;
        var reach = MathF.Sqrt(
            MathF.Pow(Convert.ToSingle(vpL["x"]) - 400f, 2) + MathF.Pow(Convert.ToSingle(vpL["y"]) - 450f, 2));

        // Just inside the limit still projects; just outside it is refused rather than shortened.
        Assert.NotNull(toolkit.CreatePerspectiveBox(grid, 400f, 450f, reach * 0.84f, 120f, 150f));

        var tooWide = Assert.Throws<ArgumentOutOfRangeException>(
            () => toolkit.CreatePerspectiveBox(grid, 400f, 450f, reach * 0.95f, 120f, 150f));
        Assert.Equal("width", tooWide.ParamName);
        Assert.Contains("createPerspectiveBox", tooWide.Message);
        Assert.Contains("left vanishing point", tooWide.Message);

        var tooDeep = Assert.Throws<ArgumentOutOfRangeException>(
            () => toolkit.CreatePerspectiveBox(grid, 400f, 450f, 180f, 120f, 100000f));
        Assert.Equal("depth", tooDeep.ParamName);
        Assert.Contains("right vanishing point", tooDeep.Message);
    }

    [Fact]
    public void TestPerspectiveBoxProjection()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var grid = toolkit.CreatePerspectiveGrid(new Dictionary<string, object?>
        {
            ["horizonY"] = 250f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f,
            ["cameraAngleDeg"] = 40f
        });

        var box = toolkit.CreatePerspectiveBox(grid, 400f, 450f, 180f, 120f, 150f);
        Assert.NotNull(box);

        var verts = (IList<Dictionary<string, object?>>)box["vertices"]!;
        Assert.Equal(8, verts.Count);

        var faces = (Dictionary<string, object?>)box["faces"]!;
        Assert.True(faces.ContainsKey("top"));
        Assert.True(faces.ContainsKey("left"));
        Assert.True(faces.ContainsKey("right"));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");
        toolkit.DrawPerspectiveBox(ctx, box, new Dictionary<string, object?> { ["drawHiddenLines"] = true });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPerspectiveCylinderRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var grid = toolkit.CreatePerspectiveGrid(new Dictionary<string, object?>
        {
            ["horizonY"] = 200f,
            ["centerOfVisionX"] = 400f,
            ["focalLength"] = 800f
        });

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");
        toolkit.DrawPerspectiveCylinder(ctx, grid, 400f, 420f, 60f, 140f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestPerspectiveQuadSubdivision()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var quad = new List<Dictionary<string, object?>>
        {
            new() { ["x"] = 100f, ["y"] = 300f },
            new() { ["x"] = 300f, ["y"] = 250f },
            new() { ["x"] = 300f, ["y"] = 450f },
            new() { ["x"] = 100f, ["y"] = 500f }
        };

        var cells = toolkit.SubdividePerspectiveQuad(quad, 3, 2);
        Assert.NotNull(cells);
        Assert.Equal(6, cells.Count); // 3 x 2 cells
        Assert.Equal(4, cells[0].Count); // each cell has 4 vertices
    }

    [Fact]
    public void TestPerspectiveConvergenceVerification()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var vp = new Dictionary<string, object?> { ["x"] = 1000f, ["y"] = 300f };

        var goodLines = new List<object>
        {
            new List<object> { new Dictionary<string, object?> { ["x"] = 100f, ["y"] = 500f }, new Dictionary<string, object?> { ["x"] = 550f, ["y"] = 400f } },
            new List<object> { new Dictionary<string, object?> { ["x"] = 200f, ["y"] = 600f }, new Dictionary<string, object?> { ["x"] = 600f, ["y"] = 450f } }
        };

        var check = toolkit.VerifyPerspectiveConvergence(goodLines, vp, 5f);
        Assert.True((bool)check["passed"]!);
    }

    [Fact]
    public void TestJavaScriptEnginePerspectiveIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Create Grid
            const grid = Drawing.createPerspectiveGrid({
                type: '2point',
                horizonY: 280,
                centerOfVisionX: 400,
                focalLength: 750,
                cameraAngleDeg: 40
            });
            ctx.drawPerspectiveGrid(grid);

            // 2. 3D Boxes
            ctx.drawPerspectiveBox(grid, 380, 480, 140, 110, 160, {
                topFill: '#f4ebd0',
                leftFill: '#cbb69d',
                rightFill: '#8d7862',
                strokeColor: '#3d2e1e'
            });

            // 3. Cylinder
            ctx.drawPerspectiveCylinder(grid, 580, 460, 45, 90);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }

    [Fact]
    public void TestCastShadowProjectionAndDrawing()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var light = new Dictionary<string, object?> { ["x"] = 150f, ["y"] = 100f };
        var boxBounds = new Dictionary<string, object?> { ["x"] = 350f, ["y"] = 300f, ["width"] = 100f, ["height"] = 150f };

        var shadow = toolkit.ProjectCastShadow(light, 450f, boxBounds);
        Assert.NotNull(shadow);
        var poly = (IList<Dictionary<string, object?>>)shadow["shadowPolygon"]!;
        Assert.Equal(4, poly.Count);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");
        toolkit.DrawCastShadow(ctx, shadow);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestVolumetricSphereRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        toolkit.RenderVolumetricSphere(ctx, 400f, 300f, 120f, new Dictionary<string, object?> { ["x"] = -0.6f, ["y"] = -0.6f });

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestVolumetricCylinderRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        toolkit.RenderVolumetricCylinder(ctx, 300f, 200f, 150f, 250f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestThreePointLightingSetupAndRimLight()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var lighting = toolkit.CreateThreePointLighting();
        Assert.NotNull(lighting);
        Assert.True(lighting.ContainsKey("keyLight"));
        Assert.True(lighting.ContainsKey("fillLight"));
        Assert.True(lighting.ContainsKey("rimLight"));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");
        toolkit.DrawRimLight(ctx, new Dictionary<string, object?> { ["x"] = 300f, ["y"] = 200f, ["width"] = 150f, ["height"] = 200f }, 135f, "#ffffff", 3f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestVolumetricSphereShaderCompilation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var shader = toolkit.CreateVolumetricSphereShader();
        Assert.NotNull(shader);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = shader;
        ctx.FillRect(0, 0, 800, 600);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestJavaScriptEngineLightingIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Cast shadow
            ctx.drawCastShadow({ x: 120, y: 80 }, 500, { x: 300, y: 350, width: 120, height: 150 }, { opacity: 0.6 });

            // 2. Volumetric sphere with ground bounce & highlight
            ctx.renderVolumetricSphere(360, 420, 80, { x: -0.6, y: -0.6 }, {
                baseColor: '#c89a74',
                shadowColor: '#3c2415',
                highlightColor: '#fff5e6',
                bounceColor: '#6384a6'
            });

            // 3. Volumetric cylinder
            ctx.renderVolumetricCylinder(520, 320, 90, 180);

            // 4. Rim light kicker
            ctx.drawRimLight({ x: 520, y: 320, width: 90, height: 180 }, 135, '#ffffff', 3.0);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }

    [Fact]
    public void TestMannequinFigureCreation()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var fig = toolkit.CreateMannequinFigure(400f, 50f, 560f);

        Assert.NotNull(fig);
        Assert.Equal(70f, Convert.ToSingle(fig["headUnit"]));
        Assert.True(fig.ContainsKey("head"));
        Assert.True(fig.ContainsKey("ribcage"));
        Assert.True(fig.ContainsKey("pelvis"));
        Assert.True(fig.ContainsKey("leftArm"));
        Assert.True(fig.ContainsKey("rightLeg"));

        var leftArm = (Dictionary<string, object?>)fig["leftArm"]!;
        Assert.True(leftArm.ContainsKey("shoulder"));
        Assert.True(leftArm.ContainsKey("elbow"));
        Assert.True(leftArm.ContainsKey("wrist"));
        Assert.True(leftArm.ContainsKey("hand"));
    }

    [Fact]
    public void TestMannequinWireframeAndSolidRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var fig = toolkit.CreateMannequinFigure(400f, 50f, 520f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        // 1. Draw solid
        toolkit.DrawMannequinSolid(ctx, fig);

        // 2. Draw wireframe on top
        toolkit.DrawMannequinWireframe(ctx, fig);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestTorsoMusculatureRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var fig = toolkit.CreateMannequinFigure(400f, 50f, 520f);

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        toolkit.DrawTorsoMusculature(ctx, fig);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestFacialExpressionsModifiers()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var head = toolkit.CreateLoomisHead(400f, 300f, 200f);

        var joyHead = toolkit.ApplyFacialExpression(head, "joy", 1.2f);
        Assert.NotNull(joyHead);
        Assert.Equal("joy", joyHead["expression"]);

        var angerHead = toolkit.ApplyFacialExpression(head, "anger", 1.5f);
        Assert.NotNull(angerHead);
        Assert.Equal("anger", angerHead["expression"]);

        var fearHead = toolkit.ApplyFacialExpression(head, "fear", 1.0f);
        Assert.NotNull(fearHead);
        Assert.Equal("fear", fearHead["expression"]);

        var sadHead = toolkit.ApplyFacialExpression(head, "sadness", 1.0f);
        Assert.NotNull(sadHead);
        Assert.Equal("sadness", sadHead["expression"]);
    }

    [Fact]
    public void TestJavaScriptEngineAnatomyIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Create full-body 8-head mannequin
            const fig = Drawing.createMannequinFigure(400, 40, 520, {
                shoulderTiltDeg: -8,
                pelvicTiltDeg: 8,
                spineOffset: 10
            });

            // 2. Draw solid volumetric mannequin
            ctx.drawMannequin(fig, true, {
                fillColor: '#d6e4f0',
                strokeColor: '#2b4d6f'
            });

            // 3. Draw torso musculature contours
            ctx.drawTorsoMusculature(fig, { strokeColor: '#1a334d', strokeWidth: 2.2 });

            // 4. Test Loomis head with Expression
            const head = Drawing.createLoomisHead(150, 150, 140);
            const happyHead = Drawing.applyFacialExpression(head, 'joy', 1.2);
            Drawing.drawComicMouth(ctx, happyHead.mouthGuides);

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }

    [Fact]
    public void TestCompositionGridCreation()
    {
        var toolkit = new ConstructiveDrawingToolkit();

        // 1. Rule of Thirds
        var thirds = toolkit.CreateCompositionGrid(900f, 600f, "ruleOfThirds");
        Assert.NotNull(thirds);
        Assert.Equal("ruleOfThirds", thirds["type"]);
        var pps = (Dictionary<string, object?>)thirds["powerPoints"]!;
        Assert.True(pps.ContainsKey("topLeft"));
        Assert.True(pps.ContainsKey("bottomRight"));

        // 2. Golden Ratio
        var golden = toolkit.CreateCompositionGrid(900f, 600f, "goldenRatio");
        Assert.NotNull(golden);
        Assert.Equal("goldenRatio", golden["type"]);

        // 3. Dynamic Symmetry
        var dynamicSym = toolkit.CreateCompositionGrid(900f, 600f, "dynamicSymmetry");
        Assert.NotNull(dynamicSym);
        Assert.Equal("dynamicSymmetry", dynamicSym["type"]);
    }

    [Fact]
    public void TestCompositionGridAndVignetteRendering()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        // 1. Draw composition grid
        toolkit.DrawCompositionGrid(ctx, "ruleOfThirds");

        // 2. Draw vignette
        toolkit.DrawVignette(ctx, 800f, 600f);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestNotanPaletteAndLeadingLines()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var pal = toolkit.CreateNotanPalette("classic3");
        Assert.NotNull(pal);
        Assert.True(pal.ContainsKey("dominantLight"));
        Assert.True(pal.ContainsKey("accentDark"));

        var canvas = new SkiaCanvas(800, 600);
        var ctx = canvas.GetContext("2d");

        var origins = new List<object>
        {
            new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 0f },
            new Dictionary<string, object?> { ["x"] = 800f, ["y"] = 0f },
            new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 600f }
        };
        var focal = new Dictionary<string, object?> { ["x"] = 400f, ["y"] = 300f };

        toolkit.DrawLeadingLines(ctx, origins, focal);

        var bytes = canvas.ToImageBytes("png");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void TestProportionalSubdivision()
    {
        var toolkit = new ConstructiveDrawingToolkit();
        var bounds = new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 0f, ["width"] = 1000f, ["height"] = 500f };

        var sub = toolkit.SubdivideProportions(bounds, "horizontal");
        Assert.NotNull(sub);
        Assert.True(sub.ContainsKey("big"));
        Assert.True(sub.ContainsKey("medium"));
        Assert.True(sub.ContainsKey("small"));

        var big = (Dictionary<string, object?>)sub["big"]!;
        Assert.Equal(700f, Convert.ToSingle(big["width"]));
    }

    [Fact]
    public void TestJavaScriptEngineCompositionIntegration()
    {
        var engine = new JsDrawingEngine();
        const string script = @"
            const canvas = createCanvas(800, 600);
            const ctx = canvas.getContext('2d');

            // 1. Draw Rule of Thirds armature
            ctx.drawCompositionGrid('ruleOfThirds');

            // 2. Draw leading lines to focal point
            ctx.drawLeadingLines([
                { x: 50, y: 550 },
                { x: 750, y: 550 }
            ], { x: 533, y: 200 }); // Top-Right Power Point

            // 3. Proportional layout
            const layout = Drawing.subdivideProportions({ x: 0, y: 0, width: 800, height: 600 }, 'horizontal');

            // 4. Cinematic vignette
            ctx.drawVignette({ intensity: 0.65 });

            canvas;
        ";

        var result = engine.Execute(script);
        Assert.NotNull(result);
        Assert.NotNull(result.ImageBytes);
        Assert.True(result.ImageBytes.Length > 1000);
    }
    #endregion
}
