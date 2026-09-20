namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

/// <summary>Constructive drawing, perspective, lighting and anatomy toolkit (Studio Manuals 01-09).</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class ConstructiveDrawingToolkit
{
    #region Nested Helper Types
    public record struct Point2D(float X, float Y);
    #endregion

    #region Point Helpers
    private static Point2D ExtractPoint(object? pt, float defX = 0f, float defY = 0f)
    {
        if (pt == null) return new Point2D(defX, defY);

        if (pt is Point2D p2d) return p2d;

        if (JsInterop.AsDict(pt) is IDictionary dict)
        {
            var x = dict.Contains("x") ? Convert.ToSingle(dict["x"], CultureInfo.InvariantCulture)
                  : dict.Contains("X") ? Convert.ToSingle(dict["X"], CultureInfo.InvariantCulture)
                  : defX;
            var y = dict.Contains("y") ? Convert.ToSingle(dict["y"], CultureInfo.InvariantCulture)
                  : dict.Contains("Y") ? Convert.ToSingle(dict["Y"], CultureInfo.InvariantCulture)
                  : defY;
            return new Point2D(x, y);
        }

        if (pt is IList list && list.Count >= 2)
        {
            var x = Convert.ToSingle(list[0], CultureInfo.InvariantCulture);
            var y = Convert.ToSingle(list[1], CultureInfo.InvariantCulture);
            return new Point2D(x, y);
        }

        return new Point2D(defX, defY);
    }

    private static Dictionary<string, object?> ToDict(Point2D p) => new() { ["x"] = p.X, ["y"] = p.Y };
    #endregion

    #region Loomis Head Construction
    /// <summary>
    /// Computes Loomis head landmarks. <b>Human proportions — not valid for an animal skull.</b>
    /// </summary>
    /// <remarks>
    /// The signature takes bare numbers and will return a confident, complete landmark set for any
    /// subject, which is the trap: a comic-studio agent drawing rabbits got usable vertical thirds
    /// (crown 99 against a measured 105, eye line 242.5 against 235) and an inter-eye distance
    /// <em>2.16× too narrow</em>, because a rabbit carries its eyes on the sides of the skull. The
    /// vertical divisions transfer between species; the lateral placement does not, and neither do
    /// <c>chin</c>, <c>noseBase</c> or <c>jaw.angle</c>, which have no animal equivalent.
    /// <para>
    /// There is no animal-head constructor in the toolkit. For a non-human subject, build the cranial
    /// mass directly — a sphere plus a muzzle lobe — and measure the reference rather than deriving
    /// from this.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateLoomisHead(float originX, float originY, float headHeight, float yawDeg = 35f, float pitchDeg = 0f)
    {
        // Loomis's scale for the standard head (Drawing the Head and Hands, Plate 18): the head is
        // 3.5 units tall and 3 units wide including the ears. Half a unit of dome sits above the
        // hairline, then three equal units - forehead, nose, jaw. Plate 1 constructs those three by
        // stepping the forehead interval off twice down the middle line, which is why they are equal
        // by construction rather than measured independently.
        var H = headHeight;
        var unit = H / 3.5f;
        var W = unit * 3f;
        var rad = (yawDeg * MathF.PI) / 180f;
        var pitchRad = (pitchDeg * MathF.PI) / 180f;

        // 3/4 Yaw & Pitch Offsets
        var turnX = MathF.Sin(rad) * (W * 0.22f);
        var pitchY = MathF.Sin(pitchRad) * (H * 0.15f);
        var centerAxisX = originX + turnX;

        // Vertical levels, in units down from the crown. The eye line lands at 1.75 units, which is
        // exactly half the head - Loomis states that outright and defends it as what averages out
        // across a large percentage of real faces.
        var top = originY - H * 0.5f;
        var yCrown = top + pitchY;
        var yHairline = top + unit * 0.5f + pitchY * 0.8f;
        var yBrow = top + unit * 1.5f + pitchY * 0.5f;
        var yEye = top + unit * 1.75f + pitchY * 0.4f;
        var yNose = top + unit * 2.5f + pitchY * 0.2f;
        var yMouth = top + unit * (2.5f + 1f / 3f) + pitchY * 0.1f;   // lip line a third of a unit below the nose
        var yChin = top + unit * 3.5f;

        // Each eye is half a unit wide, so the 2-unit face is 4 eye-widths across and the 3-unit head
        // is 6 (Plate 19, where the eyes fall at the quarter points of the two units).
        var eyeW = unit * 0.5f;
        var farScale = MathF.Max(0.45f, MathF.Cos(rad));
        var eyeWFar = eyeW * farScale;

        var crownPt = new Point2D(originX, yCrown);
        var hairlinePt = new Point2D(centerAxisX, yHairline);
        var browCenterPt = new Point2D(centerAxisX, yBrow);
        var noseBasePt = new Point2D(centerAxisX, yNose);
        var mouthCenterPt = new Point2D(centerAxisX, yMouth);
        var chinPt = new Point2D(centerAxisX + turnX * 0.1f, yChin);

        // Near eye. Inner corner half an eye-width off the facial axis, so the two inner corners sit
        // one eye-width apart - the half-unit spacing of Plate 19.
        var nearInner = new Point2D(centerAxisX + eyeW * 0.50f, yEye);
        var nearOuter = new Point2D(centerAxisX + eyeW * 1.50f, yEye - 3f);
        var nearCenter = new Point2D(centerAxisX + eyeW * 1.00f, yEye);

        // Far eye. The far side of the face compresses with the turn, so its inner corner comes in with
        // it; at yaw 0 the pair is symmetric and the gap is exactly one eye-width.
        var farInnerX = centerAxisX - eyeW * 0.50f * farScale;
        var farInner = new Point2D(farInnerX, yEye);
        var farOuter = new Point2D(farInnerX - eyeWFar, yEye - 2f);
        var farCenter = new Point2D(farInnerX - eyeWFar * 0.5f, yEye);

        // Brows. Loomis puts the brow line at 1.5 units and the eyes at 1.75, so a brow belongs to its
        // own eye rather than to the head's centre - which is all the single `brow` landmark could
        // ever express. **That landmark stays exactly where it is and keeps its meaning**: it is the
        // ball's equator, the radius `createHeadGeometry` builds the cranium from, and a construction
        // line rather than a drawn eyebrow. These are the drawn ones.
        //
        // Three stations each, because two cannot carry an arch and the arch is precisely where AU1
        // and AU2 differ - an inner lift against an outer one is worry against surprise. The tail
        // runs a little past the eye's outer corner, as a brow does, and the peak sits two thirds
        // out, which is roughly over the outer limbus.
        //
        // `span` is signed, so it carries which way "outward" runs on this side of the face and the
        // same arithmetic builds both brows: the far eye's outer corner is at negative x from its
        // inner one, so the tail extends away from the axis there too without a sign to get wrong.
        var browArch = unit * 0.07f;
        Dictionary<string, object?> Brow(Point2D eyeInner, Point2D eyeOuter)
        {
            var span = eyeOuter.X - eyeInner.X;
            var inner = new Point2D(eyeInner.X, yBrow);
            var outer = new Point2D(eyeOuter.X + (span * 0.12f), yBrow);
            return new Dictionary<string, object?>
            {
                ["inner"] = ToDict(inner),
                ["peak"] = ToDict(new Point2D(inner.X + ((outer.X - inner.X) * 0.66f), yBrow - browArch)),
                ["outer"] = ToDict(outer),

                // How heavy the brow is drawn, carried as its own measurement exactly as the eye
                // carries `width` and `height`. **It is not derived from the stations, and that is
                // the whole reason it is here.** Thickness taken from the arch - which is what
                // `drawComicBrow` did when it was written - collapses under any unit that flattens
                // the brow: `sadness` raises the inner end toward the peak, and on a 760px head the
                // arch went from 15.2px to **0.9px**, so the drawn brow came out a hairline. A
                // vertical measure held apart from the stations is immune to that and to the turn
                // alike, since neither an expression nor a yaw changes how thick an eyebrow is.
                ["thickness"] = unit * 0.12f
            };
        }

        // Ear & Jaw
        // The 3-unit width INCLUDES the ears (Plate 18), and an ear is one unit tall - so as a circle it
        // is half a unit across and its centre sits one unit off the axis, putting its outer edge exactly
        // on the head's half-width. Vertically it spans brow to nose, which is that same unit.
        // ...and it swings with the turn: the ear rides the ball, so its offset from the cranium axis
        // foreshortens by cos(yaw), the same projection the far eye already uses. Without this it stayed
        // pinned to the front view and drifted off the side of a turned head.
        var earPt = new Point2D(originX - unit * MathF.Cos(rad), (yBrow + yNose) * 0.5f);
        // Plate 1: the jaw line connects about halfway around the ball on each side, and the ears attach
        // along that same halfway line - so the jaw angle hangs directly below the ear rather than at its
        // own fraction of W, and follows the ear round as the head turns. Its depth below the nose line
        // is the studio's; Loomis gives no measurement for it.
        var jawAnglePt = new Point2D(earPt.X + unit * 0.25f, yNose + unit * 0.3f);

        // Plate 1: the jaw line connects about halfway around the ball ON EACH SIDE, so the jaw has two
        // stations and the near one is the far one mirrored about the cranium axis. That halfway line is
        // the ball's own silhouette, and at the height the jaw leaves it - the nose line - it has come in
        // from the full radius to sqrt(R^2 - unit^2); the turn foreshortens both. Shortening them together
        // is what closes the near side of the jaw as the head turns away.
        //
        // Loomis gives the two stations and nothing else. The angle's depth below the nose line and the
        // chin's share of the span between the angles are the studio's, and are in units so they scale.
        var ballR = yBrow - yCrown;
        var stationHalf = MathF.Sqrt(MathF.Max(1f, ballR * ballR - unit * unit)) * MathF.Cos(rad);
        var farStationPt = new Point2D(originX - stationHalf, yNose);

        // **The near angle is the far one mirrored about the cranium axis, stated as a mirror so it
        // cannot drift again.** It did drift: this read `originX + stationHalf - unit * 0.25f`, which
        // measures from the *station* while the far angle measures from the *ear* — and those are two
        // different quantities, 1.118 units and 1.000 units off the axis at yaw 0. So a head that was
        // square on had `jaw.angle` at -0.750 units and `jaw.nearAngle` at +0.868, with `chinFar` and
        // `chinNear` inheriting it at -0.600 against +0.694. **Measured on the rasterised silhouette,
        // a 240px frontal head came out 7px wider on one side through the jaw** — 3% of head height,
        // on a head with no turn in it at all.
        //
        // The comment above the stations has always said the near one is the far one mirrored, and
        // for the *stations* that was true. Nothing said it for the angles, and nothing checked:
        // `TestAFrontalHeadIsSymmetricAboutItsOwnAxis` measured the reported bounds, and the bounds
        // are set by the ears. A crooked jaw inside a symmetric box passes that test forever.
        //
        // **The guard stays, and it is what actually governs a turned head.** Past the point where
        // the facial axis has swung further out than the near angle has come in, the near jaw would
        // cross the chin and the path would turn inside out; holding it clear of the chin instead
        // lets the near jaw go on shortening while the chin stays between its own two angles. The
        // note here used to say "past about 60 degrees". **Measured by sweeping the yaw in half-degree
        // steps and watching for the floor to take over: it engages at 35.5, and it engaged at 40
        // before this change.** So the mirror moved that boundary by four and a half degrees, and the
        // figure that was written down was wrong by twenty. It had never been measured — which is
        // also how 60 came to sit next to a range this call documents as 30-45.
        var nearAngleX = MathF.Max((2f * originX) - jawAnglePt.X, chinPt.X + unit * 0.15f);
        var nearAnglePt = new Point2D(nearAngleX, jawAnglePt.Y);
        var nearStationPt = new Point2D(MathF.Max(originX + stationHalf, nearAngleX + unit * 0.1f), yNose);
        var chinFarPt = new Point2D(chinPt.X + (jawAnglePt.X - chinPt.X) * 0.8f, yChin);
        var chinNearPt = new Point2D(chinPt.X + (nearAngleX - chinPt.X) * 0.8f, yChin);
        var cheekApexPt = new Point2D(centerAxisX - eyeWFar - W * 0.08f, yEye + H * 0.04f);

        // Nose Wedge
        var bridgeTopPt = new Point2D(centerAxisX, yBrow + (yEye - yBrow) * 0.5f);
        var noseApexPt = new Point2D(centerAxisX + turnX * 0.35f, yNose);
        var underNosePt = new Point2D(centerAxisX, yNose + H * 0.035f);
        // **The nostril sits on Loomis's own line, not at a fixed drop below the nose.** Plate 26:
        // *"The nostrils should be set evenly on the line running from the base of the nose to the
        // base of the ear."* The base of the ear is the nose line itself — the ear is one unit tall
        // centred between brow and nose, so its foot lands exactly on `yNose` — which makes the line
        // a real construction rather than a rule of thumb, and it tilts correctly under pitch and
        // yaw where the old `yNose + H*0.02` could not.
        //
        // Its x is unchanged, so the nostril keeps its lateral station; only the height is now
        // derived. On a 240px frontal head that moves it **1.5px lower**, which is the measure of how
        // close the old constant was rather than a licence to have kept it.
        var earBaseX = originX + unit * MathF.Cos(rad);          // the NEAR ear, mirroring `earPt`
        var nostrilX = centerAxisX + eyeW * 0.50f;
        var nostrilRun = earBaseX - centerAxisX;

        // Clamped to the segment: past roughly 60 degrees the near ear has swung behind the facial
        // axis and the line reverses, which would throw the nostril up the bridge rather than fail.
        var nostrilT = MathF.Abs(nostrilRun) < 0.01f
            ? 0f : Math.Clamp((nostrilX - centerAxisX) / nostrilRun, 0f, 1f);
        var nearNostrilPt = new Point2D(nostrilX, (yNose + H * 0.035f) + (nostrilT * -H * 0.035f));

        // Mouth Guides
        var mouthLeft = new Point2D(centerAxisX - eyeW * 0.55f * farScale, yMouth);
        // The 1px drop on the near corner was the last absolute left in the mouth's construction; as a
        // fraction of head height it is the same nudge at 240px and a real one at 900.
        var mouthRight = new Point2D(centerAxisX + eyeW * 0.85f, yMouth + (H / 240f));

        return new Dictionary<string, object?>
        {
            ["unit"] = new Dictionary<string, object?>
            {
                ["H"] = H,
                ["W"] = W,
                ["eyeW"] = eyeW,
                ["thirdH"] = unit
            },
            ["origin"] = new Dictionary<string, object?> { ["x"] = originX, ["y"] = originY },
            ["crown"] = ToDict(crownPt),
            ["hairline"] = ToDict(hairlinePt),
            ["brow"] = ToDict(browCenterPt),
            ["eyeLineY"] = yEye,
            ["noseBase"] = ToDict(noseBasePt),
            ["mouthCenter"] = ToDict(mouthCenterPt),
            ["chin"] = ToDict(chinPt),
            ["nearEye"] = new Dictionary<string, object?>
            {
                ["inner"] = ToDict(nearInner),
                ["outer"] = ToDict(nearOuter),
                ["center"] = ToDict(nearCenter),
                ["width"] = eyeW,
                ["height"] = eyeW * 0.45f
            },
            ["farEye"] = new Dictionary<string, object?>
            {
                ["inner"] = ToDict(farInner),
                ["outer"] = ToDict(farOuter),
                ["center"] = ToDict(farCenter),
                ["width"] = eyeWFar,
                ["height"] = eyeWFar * 0.45f
            },
            ["nearBrow"] = Brow(nearInner, nearOuter),
            ["farBrow"] = Brow(farInner, farOuter),
            ["noseWedge"] = new Dictionary<string, object?>
            {
                ["bridgeTop"] = ToDict(bridgeTopPt),
                ["apex"] = ToDict(noseApexPt),
                ["underNose"] = ToDict(underNosePt),
                ["nearNostril"] = ToDict(nearNostrilPt)
            },
            ["mouthGuides"] = new Dictionary<string, object?>
            {
                ["center"] = ToDict(mouthCenterPt),
                ["leftCorner"] = ToDict(mouthLeft),
                ["rightCorner"] = ToDict(mouthRight),
                ["upperLipY"] = yMouth - H * 0.02f,
                ["lowerLipY"] = yMouth + H * 0.03f
            },
            ["jaw"] = new Dictionary<string, object?>
            {
                ["ear"] = ToDict(earPt),
                ["angle"] = ToDict(jawAnglePt),
                ["nearAngle"] = ToDict(nearAnglePt),
                ["farStation"] = ToDict(farStationPt),
                ["nearStation"] = ToDict(nearStationPt),
                ["chinFar"] = ToDict(chinFarPt),
                ["chinNear"] = ToDict(chinNearPt),
                ["chin"] = ToDict(chinPt),
                ["cheekApex"] = ToDict(cheekApexPt)
            },
            ["temporalOval"] = new Dictionary<string, object?>
            {
                ["cx"] = originX - W * 0.15f,
                ["cy"] = (yBrow + yNose) * 0.5f,
                ["rx"] = W * 0.32f,
                ["ry"] = H * 0.26f
            }
        };
    }

    public void DrawLoomisWireframe(CanvasRenderingContext2D ctx, object headObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a valid dictionary from CreateLoomisHead", nameof(headObj));

        var optDict = JsInterop.AsDict(options);
        var blueLine = optDict?["blueLineColor"]?.ToString() ?? "#4a90e2";
        var graphite = optDict?["graphiteColor"]?.ToString() ?? "#444444";

        var unit = JsInterop.AsDict(head["unit"]);
        var H = unit != null && unit.Contains("H") ? Convert.ToSingle(unit["H"], CultureInfo.InvariantCulture) : 200f;
        var W = unit != null && unit.Contains("W") ? Convert.ToSingle(unit["W"], CultureInfo.InvariantCulture) : 150f;

        var origin = ExtractPoint(head["origin"]);
        var crown = ExtractPoint(head["crown"]);
        var hairline = ExtractPoint(head["hairline"]);
        var brow = ExtractPoint(head["brow"]);
        var noseBase = ExtractPoint(head["noseBase"]);
        var mouthCenter = ExtractPoint(head["mouthCenter"]);
        var chin = ExtractPoint(head["chin"]);

        var jaw = JsInterop.AsDict(head["jaw"]);
        var ear = ExtractPoint(jaw?["ear"]);
        var jawAngle = ExtractPoint(jaw?["angle"]);
        var nearAngle = ExtractPoint(jaw?["nearAngle"]);
        var farStation = ExtractPoint(jaw?["farStation"]);
        var nearStation = ExtractPoint(jaw?["nearStation"]);
        var chinFar = ExtractPoint(jaw?["chinFar"]);
        var chinNear = ExtractPoint(jaw?["chinNear"]);
        var cheekApex = ExtractPoint(jaw?["cheekApex"]);

        var nearEye = JsInterop.AsDict(head["nearEye"]);
        var nearInner = ExtractPoint(nearEye?["inner"]);
        var nearOuter = ExtractPoint(nearEye?["outer"]);

        var farEye = JsInterop.AsDict(head["farEye"]);
        var farInner = ExtractPoint(farEye?["inner"]);
        var farOuter = ExtractPoint(farEye?["outer"]);

        var noseWedge = JsInterop.AsDict(head["noseWedge"]);
        var bridgeTop = ExtractPoint(noseWedge?["bridgeTop"]);
        var noseApex = ExtractPoint(noseWedge?["apex"]);

        var temporal = JsInterop.AsDict(head["temporalOval"]);
        var tempCx = temporal != null && temporal.Contains("cx") ? Convert.ToSingle(temporal["cx"], CultureInfo.InvariantCulture) : origin.X;
        var tempCy = temporal != null && temporal.Contains("cy") ? Convert.ToSingle(temporal["cy"], CultureInfo.InvariantCulture) : origin.Y;
        var tempRx = temporal != null && temporal.Contains("rx") ? Convert.ToSingle(temporal["rx"], CultureInfo.InvariantCulture) : W * 0.3f;
        var tempRy = temporal != null && temporal.Contains("ry") ? Convert.ToSingle(temporal["ry"], CultureInfo.InvariantCulture) : H * 0.25f;

        ctx.Save();

        // 1. Blue-line Construction Sphere & Proportions
        ctx.StrokeStyle = blueLine;
        ctx.LineWidth = 1.5f;

        // Cranial Sphere
        ctx.BeginPath();
        // Plate 1: the ball's equator IS the brow line and its top IS the crown, so both come off the
        // landmarks rather than from fractions of H that only happen to land near them.
        ctx.Arc(origin.X, brow.Y, brow.Y - crown.Y, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // Temporal Slice Oval
        ctx.BeginPath();
        ctx.Ellipse(tempCx, tempCy, tempRx, tempRy, 0f, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // Central 3/4 Facial Meridian Arc
        ctx.BeginPath();
        ctx.MoveTo(crown.X, crown.Y);
        ctx.BezierCurveTo(hairline.X + 8f, hairline.Y, brow.X + 8f, brow.Y, noseBase.X, noseBase.Y);
        ctx.BezierCurveTo(mouthCenter.X, mouthCenter.Y, chin.X, chin.Y - 10f, chin.X, chin.Y);
        ctx.Stroke();

        // Horizontal Guide Lines (Hairline, Brow, Eye, Nose, Mouth, Chin)
        void DrawGuide(Point2D pt, float widthSpan)
        {
            ctx.BeginPath();
            ctx.MoveTo(pt.X - widthSpan * 0.5f, pt.Y);
            ctx.LineTo(pt.X + widthSpan * 0.5f, pt.Y);
            ctx.Stroke();
        }

        var unitLen = noseBase.Y - brow.Y;                     // one Loomis unit, read off the landmarks
        var chinRise = (chinNear.X - chinFar.X) * 0.15f;      // corners sit above the lowest point

        DrawGuide(hairline, W * 0.7f);
        DrawGuide(brow, W * 0.85f);
        DrawGuide(noseBase, W * 0.75f);
        DrawGuide(chin, chinNear.X - chinFar.X);

        // 2. Graphite Pencil Contours (Jawline, Eyes, Nose, Mouth, Ear)
        ctx.StrokeStyle = graphite;
        ctx.LineWidth = 2.0f;

        // Jawline: down from each halfway station to its angle, and round a chin with real width. The
        // frame comes off the model - see head.jaw - so a script can draw its own jaw on the same points.
        ctx.BeginPath();
        ctx.MoveTo(farStation.X, farStation.Y);
        ctx.LineTo(jawAngle.X, jawAngle.Y);
        ctx.QuadraticCurveTo(jawAngle.X, chinFar.Y - chinRise, chinFar.X, chinFar.Y - chinRise);
        ctx.QuadraticCurveTo(chin.X, chin.Y + chinRise, chinNear.X, chinNear.Y - chinRise);
        ctx.QuadraticCurveTo(nearAngle.X, chinNear.Y - chinRise, nearAngle.X, nearAngle.Y);
        ctx.LineTo(nearStation.X, nearStation.Y);
        ctx.Stroke();

        // Far cheek plane, from the cheekbone down to the jaw angle. Drawn separately: it used to be the
        // tail of the jaw path, and since cheekApex sits on the FAR side the path doubled back across the
        // face and closed into a narrow V with a pointed chin.
        ctx.BeginPath();
        ctx.MoveTo(cheekApex.X, cheekApex.Y);
        ctx.QuadraticCurveTo(cheekApex.X, jawAngle.Y - unitLen * 0.35f, jawAngle.X, jawAngle.Y);
        ctx.Stroke();

        // Eye Socket Guidelines
        ctx.BeginPath();
        ctx.MoveTo(nearInner.X, nearInner.Y);
        ctx.QuadraticCurveTo(nearInner.X + (nearOuter.X - nearInner.X) * 0.5f, nearInner.Y - 12f, nearOuter.X, nearOuter.Y);
        ctx.MoveTo(farInner.X, farInner.Y);
        ctx.QuadraticCurveTo(farInner.X + (farOuter.X - farInner.X) * 0.5f, farInner.Y - 10f, farOuter.X, farOuter.Y);
        ctx.Stroke();

        // Brow stations. Drawn as the spine each brow is built on rather than as the filled mass, so
        // the sheet shows where AU1, AU2 and AU4 act — which is the point of a construction sheet and
        // was unshowable while the brow was one point on the meridian.
        foreach (var group in new[] { "nearBrow", "farBrow" })
        {
            if (JsInterop.AsDict(head[group]) is not IDictionary b) continue;
            var bi = ExtractPoint(b["inner"]);
            var bp = ExtractPoint(b["peak"]);
            var bo = ExtractPoint(b["outer"]);
            var bc = ControlThrough(bi, bp, bo);
            ctx.BeginPath();
            ctx.MoveTo(bi.X, bi.Y);
            ctx.QuadraticCurveTo(bc.X, bc.Y, bo.X, bo.Y);
            ctx.Stroke();
        }

        // Nose Wedge Guideline
        ctx.BeginPath();
        ctx.MoveTo(bridgeTop.X, bridgeTop.Y);
        ctx.LineTo(noseApex.X, noseApex.Y);
        ctx.LineTo(noseBase.X, noseBase.Y);
        ctx.Stroke();

        // Ear Outline
        ctx.BeginPath();
        // Plate 1: the ear's height is the brow-to-nose span, so its radius is half that rather than a
        // fraction of H. The old H * 0.08f drew it at a little over half the size it should be.
        ctx.Arc(ear.X, ear.Y, (noseBase.Y - brow.Y) * 0.5f, 0f, MathF.PI * 2f);
        ctx.Stroke();

        ctx.Restore();
    }
    #endregion

    #region Comic Feature Drawing Helpers
    /// <summary>
    /// Draws the eye and <b>returns the parts it built</b>, each as a <see cref="CanvasPath"/>:
    /// <c>aperture</c>, <c>iris</c>, <c>pupil</c>, <c>catchlight</c>, <c>upperLid</c>, <c>lowerLid</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>aperture</c> is the one worth having: it is the shape the sclera fills and the shape the
    /// interior is clipped to, so it is what a caller clips a highlight, a cast shadow from the brow,
    /// or a reflected window into. Reconstructing it by hand means re-deriving the eyelid curve from
    /// <c>inner</c>, <c>outer</c> and the eye's own width, which is exactly the duplication these
    /// return values exist to prevent.
    /// </para>
    /// <para>
    /// The two lids are open centre-lines rather than filled shapes, so they can be re-stroked at a
    /// different weight — Studio Manual 03's tier hierarchy is a decision about weight, and an eye
    /// drawn at panel size wants a different one from an eye in close-up. Pass either through
    /// <c>ctx.strokeToPath(...)</c> to turn it into a fillable mark.
    /// </para>
    /// <para>A script that ignores the return value behaves exactly as before.</para>
    /// </remarks>
    public Dictionary<string, object?> DrawComicEye(CanvasRenderingContext2D ctx, object eyeObj, bool isFar = false, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(eyeObj) is not IDictionary eye) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var irisColor = optDict?["irisColor"]?.ToString() ?? "#3b6884";
        var scleraColor = optDict?["scleraColor"]?.ToString() ?? "#f1f4f7";

        // A multiplier on the feature's own weights rather than a pixel count, as `drawComicBrow`'s
        // `thickness` already is — so a tier chosen here survives a change of head size.
        var weight = Fraction(optDict, "weight", 1f);

        var inner = ExtractPoint(eye["inner"]);
        var outer = ExtractPoint(eye["outer"]);
        var center = ExtractPoint(eye["center"]);

        var w = MathF.Abs(outer.X - inner.X);
        if (w <= 0.1f) w = 24f;
        var dir = outer.X > inner.X ? 1f : -1f;

        // **The lid aperture, read from the eye rather than fixed here.** `height` is written by
        // CreateLoomisHead and was read by nothing until 2026-09-18, so the whole opening was a
        // hard-coded fraction of the eye's drawn width and no expression could close an eye.
        //
        // Taken as a RATIO against `width` rather than as an absolute, for two reasons. It is
        // scale- and yaw-invariant, so a projected far eye narrows without also closing; and at the
        // canon's own 0.45 it reproduces the previous constants exactly, which is what lets this
        // change render every existing script byte-identically.
        var openness = Ratio(eye, "height", "width", CanonOpenness);
        var up = w * openness;

        // **The iris, as a settable fraction of the eye's own width, defaulting to the comic canon.**
        // 0.32 is a deliberately large iris and is what every existing script draws; the measured
        // figure for a real face is **0.19**, taken off the iris ring in MediaPipe's canonical face
        // model — iris diameter is 0.1886 of the interpupillary distance there, and this
        // construction places the pupils one eye-width either side of the axis, so IPD is exactly
        // twice `w` and the two ratios are the same number. **Ours is therefore 1.70x life size**,
        // which is the comic idiom rather than an error: Manual 23 §1 measures the comic head as
        // narrower relative to its eye than Loomis's, and a bigger iris is the other half of that.
        // Left as the default for the reason `CanonOpenness` was: moving it would restyle every
        // face already drawn.
        var irisR = w * Fraction(optDict, "irisRatio", CanonIrisRatio);
        var irisX = center.X + dir * w * 0.08f;
        var irisY = center.Y - 1f;

        // The eyelid curve is used three times — filled as the sclera, as the clip for the interior,
        // and stroked as the upper lid — so it is built once.
        var upperLid = new CanvasPath();
        upperLid.MoveTo(inner.X, inner.Y);
        upperLid.BezierCurveTo(inner.X + dir * w * 0.3f, inner.Y - up, inner.X + dir * w * 0.7f, inner.Y - up * LidCrest, outer.X, outer.Y);

        var aperture = new CanvasPath(upperLid);
        aperture.QuadraticCurveTo(center.X, inner.Y + up * LidFloor, inner.X, inner.Y);
        aperture.ClosePath();

        var iris = Disc(new Point2D(irisX, irisY), irisR);
        var pupil = Disc(new Point2D(irisX, irisY), irisR * 0.45f);
        var catchlight = Disc(new Point2D(irisX - irisR * 0.25f, irisY - irisR * 0.25f), irisR * 0.22f);

        var lowerLid = new CanvasPath();
        lowerLid.MoveTo(inner.X + dir * w * 0.2f, inner.Y + up * LowerLidStart);
        lowerLid.QuadraticCurveTo(center.X, inner.Y + up * LidFloor, outer.X - dir * w * 0.1f, outer.Y);

        ctx.Save();

        // 1. Sclera fill inside eyelid bounds
        ctx.Save();
        ctx.FillStyle = scleraColor;
        ctx.Fill(aperture);
        ctx.Clip(aperture);

        // 2. Iris & Pupil
        ctx.FillStyle = irisColor;
        ctx.Fill(iris);
        ctx.StrokeStyle = inkColor;
        ctx.LineWidth = Tier(IrisRimTier, w, EyeWidthAt240, weight);
        ctx.Stroke(iris);                 // dark iris rim

        ctx.FillStyle = inkColor;
        ctx.Fill(pupil);
        ctx.FillStyle = "#ffffff";
        ctx.Fill(catchlight);

        ctx.Restore();

        // 3. Thick Inked S-Curve Upper Eyelid
        ctx.StrokeStyle = inkColor;
        ctx.LineWidth = Tier(LidTier, w, EyeWidthAt240, weight * (isFar ? FarFeatureWeight : 1f));
        ctx.LineCap = "round";
        ctx.Stroke(upperLid);

        // 4. Delicate Lower Eyelid
        ctx.LineWidth = Tier(LowerLidTier, w, EyeWidthAt240, weight);
        ctx.Stroke(lowerLid);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["aperture"] = aperture,
            ["iris"] = iris,
            ["pupil"] = pupil,
            ["catchlight"] = catchlight,
            ["upperLid"] = upperLid,
            ["lowerLid"] = lowerLid
        };
    }

    /// <summary>
    /// Draws one eyebrow and <b>returns the parts it built</b>: <c>mass</c> (the filled brow) and
    /// <c>spine</c> (its centre-line, through the peak).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This exists so the brow stations are not another <c>eye.height</c>.</b> That field was
    /// written by <see cref="CreateLoomisHead"/> and read by nothing for as long as it existed, so
    /// every expression that meant to narrow a lid silently did nothing. Inner and outer brow
    /// stations without a call that draws them would be the same defect a second time: AU1 and AU2
    /// would differ in the landmark data and be indistinguishable in every render, which is the one
    /// outcome worse than not separating them at all.
    /// </para>
    /// <para>
    /// <b>The weight is read from the brow's own <c>thickness</c>, not derived from its stations.</b>
    /// A brow foreshortens horizontally and not vertically, so weight taken from the projected span
    /// would return a far brow 45% too thin at the yaw clamp — the same trap <c>drawComicEye</c>
    /// avoids by reading its aperture as a ratio. <b>But the arch is not the fix either</b>, and the
    /// first version of this call used it: <c>|peak.y − inner.y|</c> is vertical, yet it is also a
    /// quantity the Action Units <i>move</i>. AU1 raises the inner end toward the peak, so
    /// <c>sadness</c> flattens the arch — measured on a 760px head it fell from <b>15.2px to
    /// 0.9px</b> and the brow drew as a hairline. A measurement held apart from the stations, exactly
    /// as the eye holds <c>width</c> and <c>height</c>, is immune to the expression and to the turn
    /// alike.
    /// </para>
    /// <para>
    /// <c>isFar</c> thins the mass to match the lighter stroke <see cref="DrawComicEye"/> already
    /// gives a far eye, so a far brow and the eye under it read at one weight.
    /// </para>
    /// <para>
    /// <c>options</c>: <c>inkColor</c>, and <c>thickness</c> as a multiplier on the default rather
    /// than a pixel count, so it survives a change of head size.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> DrawComicBrow(CanvasRenderingContext2D ctx, object browObj, bool isFar = false, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(browObj) is not IDictionary brow) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var weight = optDict is not null && optDict.Contains("thickness")
            ? Convert.ToSingle(optDict["thickness"], CultureInfo.InvariantCulture) : 1f;

        var inner = ExtractPoint(brow["inner"]);
        var peak = ExtractPoint(brow["peak"]);
        var outer = ExtractPoint(brow["outer"]);

        // **Read, never derived from the stations.** This was `|peak.y - inner.y| * 0.85` when it was
        // written, on the reasoning that a vertical measure does not foreshorten - true, and beside
        // the point: the arch is a quantity the ACTION UNITS MOVE. AU1 lifts the inner end toward the
        // peak, so `sadness` flattens the arch, and on a 760px head it fell from 15.2px to 0.9px and
        // drew a hairline. Same shape of mistake as the brow units moving the cranium's own landmark:
        // a drawn quantity taken from something an expression displaces.
        //
        // The fallbacks are for a hand-built or legacy brow that carries no measurement, and only
        // there is the arch used - a thin brow beats no brow.
        var thickness = Convert.ToSingle(brow.Contains("thickness") ? brow["thickness"] : 0f,
                                         CultureInfo.InvariantCulture);
        if (thickness <= 0.1f) thickness = MathF.Abs(peak.Y - inner.Y) * 1.7f;
        if (thickness <= 0.1f) thickness = MathF.Abs(outer.X - inner.X) * 0.14f;
        if (thickness <= 0.1f) return [];

        var half = thickness * 0.5f * weight * (isFar ? FarFeatureWeight : 1f);

        // The stations say where the brow goes; these put the curve THROUGH the peak rather than
        // merely toward it. A quadratic's midpoint is a quarter of each end plus half the control,
        // so the control has to overshoot - aimed at the peak instead, the arch reads about half as
        // high as the landmark states and `peak` stops meaning what its name says.
        var crest = ControlThrough(inner, peak, outer);
        var spine = new CanvasPath();
        spine.MoveTo(inner.X, inner.Y);
        spine.QuadraticCurveTo(crest.X, crest.Y, outer.X, outer.Y);

        // The mass tapers to nothing at the tail and stays blunt at the head, which is the shape of a
        // brow rather than of a leaf: both ends pointed reads as a moustache set above the eye.
        var top = ControlThrough(new Point2D(inner.X, inner.Y - half), new Point2D(peak.X, peak.Y - half), outer);
        var under = ControlThrough(outer, new Point2D(peak.X, peak.Y + (half * 0.55f)), new Point2D(inner.X, inner.Y + half));

        var mass = new CanvasPath();
        mass.MoveTo(inner.X, inner.Y - half);
        mass.QuadraticCurveTo(top.X, top.Y, outer.X, outer.Y);
        mass.QuadraticCurveTo(under.X, under.Y, inner.X, inner.Y + half);
        mass.ClosePath();

        ctx.Save();
        ctx.FillStyle = inkColor;
        ctx.Fill(mass);
        ctx.Restore();

        return new Dictionary<string, object?> { ["mass"] = mass, ["spine"] = spine };
    }

    /// <summary>
    /// Draws the nose and <b>returns the parts it built</b>: <c>underPlane</c> (the shaded plane
    /// beneath), <c>bridge</c> (the inked centre-line) and <c>nostril</c>.
    /// </summary>
    /// <remarks>
    /// <c>underPlane</c> is the one that composes: it is the plane turned away from the key, so it is
    /// what a caller re-fills when the light moves, or unions with the other shadow shapes on a face
    /// to make one cast-shadow mass. Studio Manual 03's rule is that a heavy line is the beginning of
    /// a shadow — that only becomes actionable when the shadow is a shape you hold.
    /// </remarks>
    public Dictionary<string, object?> DrawComicNose(CanvasRenderingContext2D ctx, object noseObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(noseObj) is not IDictionary nose) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var shadowColor = optDict?["shadowColor"]?.ToString() ?? "#b06f4c";
        var weight = Fraction(optDict, "weight", 1f);

        var bridgeTop = ExtractPoint(nose["bridgeTop"]);
        var apex = ExtractPoint(nose["apex"]);
        var underNose = ExtractPoint(nose["underNose"]);
        var nearNostril = ExtractPoint(nose["nearNostril"]);

        // The nose's own length, which is what every weight here is measured against.
        var len = MathF.Abs(underNose.Y - bridgeTop.Y);
        if (len <= 0.1f) len = NoseLengthAt240;

        var underPlane = new CanvasPath();
        underPlane.MoveTo(apex.X, apex.Y);
        underPlane.LineTo(nearNostril.X, nearNostril.Y);
        underPlane.LineTo(underNose.X, underNose.Y);
        underPlane.ClosePath();

        // **The bridge is a tapered mark, not a constant-width polyline.** Manual 03 §3 says outright
        // that a constant-width stroke reads as a technical drawing rather than as inking, and this
        // call was drawing one. The envelope is heaviest in the middle and vanishes at both ends,
        // which is where a nose actually carries its weight: the ball is the form, the bridge fades
        // into the brow, and the base is picked up by the nostril rather than by a line.
        //
        // The two control points run through the apex, so the mark passes over the ball rather than
        // cutting the corner between the three landmarks.
        // **The mark starts below the brow, not at it.** The landmarks run from `bridgeTop`, which is
        // halfway between brow and eye, and a mark carried all the way up from there reads as a line
        // ruled down the middle of the face — tolerable while it was a 2.4px hairline, and a wedge
        // once the weight scaled with the head. Comic practice inks the lower bridge and the ball and
        // lets the upper bridge be carried by the brow, so the run begins 45% of the way down.
        var markTop = new Point2D(
            bridgeTop.X + ((apex.X - bridgeTop.X) * 0.45f),
            bridgeTop.Y + ((apex.Y - bridgeTop.Y) * 0.45f));

        // **A frontal nose has no bridge shadow, and drawing one gives it a dagger down the middle.**
        // At yaw 0 the three landmarks are collinear and vertical, so the mark can only be a vertical
        // wedge — tolerable while it was a 2.4px hairline, and the first thing a reader saw once the
        // weight scaled. The bridge line is the break between the front plane and the side plane, so
        // it belongs to a turned head; comic practice inks a frontal nose with its nostrils and its
        // under-plane and lets the bridge go.
        //
        // How far the head has turned is recoverable from the nose itself — `apex` carries the lateral
        // offset the construction gave it — so this needs no yaw argument and works on a head that has
        // been through `applyActionUnits` or a blend.
        var turn = Math.Clamp(MathF.Abs(apex.X - underNose.X) / (len * 0.15f), 0f, 1f);
        var thickness = Tier(NoseBridgeTier, len, NoseLengthAt240,
                             weight * TaperGain * (FrontalBridge + ((1f - FrontalBridge) * turn)));
        var bridgeMark = CreateTaperedStrokePath(
            markTop,
            new Point2D(apex.X, markTop.Y + ((apex.Y - markTop.Y) * 0.6f)),
            apex,
            underNose,
            thickness);

        // **`bridge` keeps its meaning — the open centre-line — and the filled mark arrives beside it
        // as `bridgeMark`.** Changing what an existing key holds would break every caller that strokes
        // it at its own tier, which is exactly the thing these returns exist to allow.
        var bridge = new CanvasPath();
        bridge.MoveTo(bridgeTop.X, bridgeTop.Y);
        bridge.LineTo(apex.X, apex.Y);
        bridge.LineTo(underNose.X, underNose.Y);

        var nostril = new CanvasPath();
        nostril.Arc(nearNostril.X, nearNostril.Y, len * (NostrilRadiusTier / NoseLengthAt240), 0.2f, MathF.PI * 1.5f);

        ctx.Save();

        ctx.FillStyle = shadowColor;
        ctx.Fill(underPlane);

        ctx.FillStyle = inkColor;
        ctx.Fill(bridgeMark);

        ctx.StrokeStyle = inkColor;
        ctx.LineWidth = Tier(NostrilTier, len, NoseLengthAt240, weight);
        ctx.LineCap = "round";
        ctx.Stroke(nostril);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["underPlane"] = underPlane,
            ["bridge"] = bridge,
            ["bridgeMark"] = bridgeMark,
            ["nostril"] = nostril
        };
    }

    /// <summary>
    /// Draws one ear and <b>returns the parts it built</b>: <c>helix</c>, <c>antihelix</c>,
    /// <c>concha</c> and <c>lobe</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Until 2026-09-19 nothing drew an ear at all.</b> <c>createHeadGeometry</c> has carried
    /// <c>parts.ear</c> and <c>parts.nearEar</c> since the same week, but those are *masses* — padded
    /// ellipses unioned into the silhouette — with no internal structure whatever, so every head this
    /// studio drew wore two blank flaps. Manual 23 §9 named the gap in its own words: what the
    /// composition gives you is shape, and *"it does not shade them"*.
    /// </para>
    /// <para>
    /// <b>Loomis declines to give a canon for the shape, and that decides what this call is.</b> Plate
    /// 26: *"The real problem is much more one of setting them into the construction of the head in
    /// their correct positions than one of drawing the actual details themselves. Noses and ears vary
    /// widely in shape but not a great deal in basic construction."* So there is no measured ear to
    /// implement — the placement half is what <c>createHeadGeometry</c> already does, and this is the
    /// basic construction: an outer rim, the ridge inside it, the bowl between them, and the lobe.
    /// The proportions below are the studio's, by eye, and are not his.
    /// </para>
    /// <para>
    /// Pass <c>geo.ears.far</c> or <c>geo.ears.near</c>. An ear foreshortens the opposite way to an
    /// eye — edge-on frontally, full-face in profile — and the width in that block already carries
    /// the turn, so this call needs no yaw: a narrow ear simply draws narrow.
    /// </para>
    /// <para>
    /// <c>options</c>: <c>inkColor</c>, <c>shadowColor</c>, <c>weight</c> as a multiplier, and
    /// <c>hatch</c> (default <c>true</c>) for the cross-contour arcs inside the bowl.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> DrawComicEar(CanvasRenderingContext2D ctx, object earObj, bool isFar = false, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(earObj) is not IDictionary ear) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";

        // **A neutral, near-transparent bowl rather than the nose's orange under-plane.** The first
        // version borrowed `#b06f4c` from `drawComicNose` and at panel size the ear read as a bruise:
        // the concha is a hollow catching less light, not a lit plane of its own.
        var shadowColor = optDict?["shadowColor"]?.ToString() ?? "rgba(0,0,0,0.13)";
        var weight = Fraction(optDict, "weight", 1f);
        var hatch = optDict is null || !optDict.Contains("hatch") || Convert.ToBoolean(optDict["hatch"]);

        var center = ExtractPoint(ear["center"]);
        var w = Num(ear, "width", 0f);
        var h = Num(ear, "height", 0f);
        if (w <= 0.5f || h <= 0.5f) return [];

        // **An ear that no longer stands outside the skull is not inked**, because it is behind the
        // head. The masses can be unioned blind — a hidden ear adds nothing to a silhouette — but a
        // drawn one is painted on top, and past about 35 degrees of yaw the near ear's rim and bowl
        // landed on the cheek beside the near eye. Defaults to drawing, so a hand-built ear that
        // carries no such flag still draws.
        if (ear.Contains("visible") && ear["visible"] is not null && !Convert.ToBoolean(ear["visible"]))
            return [];

        // Which way the face lies. Without it the helix would run round the wrong side and the lobe
        // would sit behind the jaw rather than in front of it.
        var faceDir = Num(ear, "faceDir", -1f) >= 0f ? 1f : -1f;
        float rx = w * 0.5f, ry = h * 0.5f;
        var back = -faceDir;                       // away from the face, where the rim is widest

        var tier = isFar ? FarFeatureWeight : 1f;

        // **The helix — the outer rim, from the top of the ear round the back to the lobe.** Tapered,
        // because a rim is a rolled edge: it is fullest where it turns away from the light at the back
        // and thins where it meets the skull at either end.
        var helixTop = new Point2D(center.X + (faceDir * rx * 0.35f), center.Y - ry);
        var helixLobe = new Point2D(center.X + (faceDir * rx * 0.15f), center.Y + ry);
        var helix = CreateTaperedStrokePath(
            helixTop,
            new Point2D(center.X + (back * rx * 1.02f), center.Y - (ry * 0.70f)),
            new Point2D(center.X + (back * rx * 0.90f), center.Y + (ry * 0.52f)),
            helixLobe,
            Tier(HelixTier, h, EarHeightAt240, weight * tier * TaperGain));

        // **The antihelix — the Y-shaped ridge inside the rim**, drawn as its single strong arm. It
        // runs roughly parallel to the helix at about half the radius, which is what gives an ear its
        // depth rather than reading as a flat disc.
        var antihelix = CreateTaperedStrokePath(
            new Point2D(center.X + (faceDir * rx * 0.10f), center.Y - (ry * 0.62f)),
            new Point2D(center.X + (back * rx * 0.58f), center.Y - (ry * 0.42f)),
            new Point2D(center.X + (back * rx * 0.50f), center.Y + (ry * 0.18f)),
            new Point2D(center.X + (faceDir * rx * 0.05f), center.Y + (ry * 0.46f)),
            Tier(AntihelixTier, h, EarHeightAt240, weight * tier * TaperGain));

        // **The concha — the bowl the ridge encloses**, toward the face and slightly below centre.
        float cx = center.X + (faceDir * rx * 0.22f), cy = center.Y + (ry * 0.02f);
        float crx = rx * 0.34f, cry = ry * 0.24f;
        var concha = new CanvasPath();
        concha.Ellipse(cx, cy, crx, cry, 0f, 0f, MathF.PI * 2f, false);

        // The lobe, a short soft arc at the foot, in front of the helix's own end.
        var lobe = new CanvasPath();
        lobe.Arc(center.X + (faceDir * rx * 0.05f), center.Y + (ry * 0.78f), rx * 0.34f,
                 MathF.PI * 0.05f, MathF.PI * 0.95f);

        ctx.Save();

        ctx.FillStyle = shadowColor;
        ctx.Fill(concha);

        // **Cross-contour arcs across the bowl, which is what `drawCrossContourHatch` is for.** They
        // run across the form rather than along it, so they state the hollow instead of shading it
        // flat — the same reason Manual 03 §4 hatches a three-quarter head at two different angles.
        // Three arcs rather than four, and only where they are big enough to read: below about eight
        // pixels of bowl they merge into the blob they were drawn to avoid.
        if (hatch && cry > 4f)
            DrawCrossContourHatch(ctx, cx, cy, crx * 0.82f, cry * 0.82f,
                                  MathF.PI * 0.15f, MathF.PI * 0.85f, 3, inkColor,
                                  MathF.Max(0.3f, Tier(ConchaTier, h, EarHeightAt240, weight * tier * 0.8f)));

        ctx.FillStyle = inkColor;
        ctx.Fill(helix);
        ctx.Fill(antihelix);

        ctx.StrokeStyle = inkColor;
        ctx.LineWidth = MathF.Max(0.3f, Tier(ConchaTier, h, EarHeightAt240, weight * tier));
        ctx.LineCap = "round";
        ctx.Stroke(lobe);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["helix"] = helix,
            ["antihelix"] = antihelix,
            ["concha"] = concha,
            ["lobe"] = lobe
        };
    }

    /// <summary>
    /// Draws the mouth and <b>returns the parts it built</b>: <c>cavity</c>, <c>teeth</c>,
    /// <c>lipLine</c> (the inked upper lip, as an open centre-line) and <c>lowerLip</c>.
    /// </summary>
    /// <remarks>
    /// The mouth is the feature Studio Manual 23 §4 says carries expression along with the brows and
    /// the eyes, so it is the one most often redrawn — and <c>lipLine</c> being a centre-line rather
    /// than a filled mark is what lets it be re-stroked heavier, run through
    /// <c>ctx.strokeToPath(...)</c> to be tapered, or clipped against the head's own silhouette.
    /// </remarks>
    public Dictionary<string, object?> DrawComicMouth(CanvasRenderingContext2D ctx, object mouthObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(mouthObj) is not IDictionary mouth) return [];

        var optDict = JsInterop.AsDict(options);
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var lipColor = optDict?["lipColor"]?.ToString() ?? "#b84848";
        var teethColor = optDict?["teethColor"]?.ToString() ?? "#fbf8ee";
        var cavityColor = optDict?["cavityColor"]?.ToString() ?? "#3a1215";

        var weight = Fraction(optDict, "weight", 1f);

        var center = ExtractPoint(mouth["center"]);
        var left = ExtractPoint(mouth["leftCorner"]);
        var right = ExtractPoint(mouth["rightCorner"]);

        // **The mouth's GEOMETRY was in absolute pixels too, not just its ink** — the cavity 12px
        // deep, the lower lip a 6px disc 16px below centre, the teeth inset 3px from each corner.
        // So a mouth on a 600px head had a cavity a fortieth of its own width and a lower lip that
        // had all but vanished, while a 60px head wore a lip larger than the mouth. Every offset is
        // now a fraction of the mouth's own width, calibrated on the 240px head that produced the
        // constants, so that size is unchanged and the rest are no longer wrong.
        var mw = MathF.Abs(right.X - left.X);
        if (mw <= 0.1f) mw = MouthWidthAt240;
        float F(float px) => mw * (px / MouthWidthAt240);

        // The upper lip curve bounds the cavity and is also the inked line, so it is built once.
        var lipLine = new CanvasPath();
        lipLine.MoveTo(left.X, left.Y);
        lipLine.QuadraticCurveTo(center.X, center.Y - F(2f), right.X, right.Y);

        var cavity = new CanvasPath(lipLine);
        cavity.QuadraticCurveTo(center.X, center.Y + F(12f), left.X, left.Y);
        cavity.ClosePath();

        var teeth = new CanvasPath();
        teeth.MoveTo(left.X + F(3f), left.Y);
        teeth.QuadraticCurveTo(center.X, center.Y - F(2f), right.X - F(3f), right.Y);
        teeth.LineTo(right.X - F(4f), right.Y + F(4f));
        teeth.QuadraticCurveTo(center.X, center.Y + F(3f), left.X + F(4f), left.Y + F(3f));
        teeth.ClosePath();

        var lowerLip = new CanvasPath();
        lowerLip.Arc(center.X, center.Y + F(16f), F(6f), 0.2f, MathF.PI - 0.2f);

        // **The lip slit as a tapered mark.** Full weight through the middle and lifting at both
        // corners, which is what stops a mouth reading as a drawn-on line. `lipLine` keeps its
        // documented meaning as the open centre-line; the filled mark arrives beside it.
        var lipMark = CreateTaperedStrokePath(
            left,
            new Point2D(left.X + ((center.X - left.X) * 0.6f), center.Y - F(2f)),
            new Point2D(right.X - ((right.X - center.X) * 0.6f), center.Y - F(2f)),
            right,
            Tier(LipLineTier, mw, MouthWidthAt240, weight * TaperGain));

        ctx.Save();

        ctx.FillStyle = cavityColor;
        ctx.Fill(cavity);

        // **Clipped to the cavity, because teeth outside a mouth are a hole in the face.** The teeth
        // polygon's lower edge dips below the cavity's return curve at whichever corner is longer —
        // this construction's corners are not symmetric — so a wedge of white stood outside the mouth.
        // It was always there and was four pixels wide at 240px; scaling the geometry made it visible
        // rather than making it happen.
        ctx.Save();
        ctx.Clip(cavity);
        ctx.FillStyle = teethColor;
        ctx.Fill(teeth);
        ctx.Restore();

        // **A hairline along the centre-line under the tapered mark, to seal the corners.** The taper
        // lifts to nothing at each corner, and the teeth's top edge follows the same curve — so
        // without this the white of the teeth shows through as a notch at whichever corner is longer.
        // The round-capped constant stroke used to cover it by being constant.
        ctx.StrokeStyle = inkColor;
        ctx.LineWidth = MathF.Max(0.4f, Tier(LipLineTier, mw, MouthWidthAt240, weight * 0.4f));
        ctx.LineCap = "round";
        ctx.Stroke(lipLine);

        ctx.FillStyle = inkColor;
        ctx.Fill(lipMark);

        ctx.FillStyle = lipColor;
        ctx.Fill(lowerLip);

        ctx.Restore();

        return new Dictionary<string, object?>
        {
            ["cavity"] = cavity,
            ["teeth"] = teeth,
            ["lipLine"] = lipLine,
            ["lipMark"] = lipMark,
            ["lowerLip"] = lowerLip
        };
    }
    #endregion

    #region Tapered Strokes, Feathering & Hair Ribbons
    public CanvasPath DrawTaperedStroke(CanvasRenderingContext2D ctx, object start, object cp1, object cp2, object end, float maxThickness, object? fillOrStrokeStyle = null)
    {
        var s = ExtractPoint(start);
        var c1 = ExtractPoint(cp1);
        var c2 = ExtractPoint(cp2);
        var e = ExtractPoint(end);
        return DrawTaperedStroke(ctx, s.X, s.Y, c1.X, c1.Y, c2.X, c2.Y, e.X, e.Y, maxThickness, fillOrStrokeStyle);
    }

    /// <summary>
    /// The tapered ink mark, as the shape it is: a sine-tapered envelope around a cubic Bézier,
    /// filled on <paramref name="ctx"/> and <b>returned</b> so it can be built on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returning the path is the point. A mark that is only painted is finished; a mark you hold is
    /// geometry — <c>subtract</c> a bite out of it, fill it with a gradient running <i>across</i> the
    /// stroke rather than along it, clip inside it, union a run of them into one silhouette, or let
    /// it reach <c>outSvg</c> as vector instead of only as pixels. None of that was reachable while
    /// the call painted and returned nothing, and rebuilding the envelope by hand means duplicating
    /// the twenty-five-sample sweep below and getting the normals right.
    /// </para>
    /// <para>
    /// A script that ignores the return value behaves exactly as before, which is what makes this
    /// safe to add to a call that was <c>void</c>.
    /// </para>
    /// </remarks>
    public CanvasPath DrawTaperedStroke(CanvasRenderingContext2D ctx, float sx, float sy, float cp1x, float cp1y, float cp2x, float cp2y, float ex, float ey, float maxThickness, object? fillOrStrokeStyle = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var mark = BuildTaperedStroke(sx, sy, cp1x, cp1y, cp2x, cp2y, ex, ey, maxThickness);

        ctx.Save();
        if (fillOrStrokeStyle != null)
            ctx.FillStyle = fillOrStrokeStyle;
        ctx.Fill(mark);
        ctx.Restore();
        return mark;
    }

    /// <summary>
    /// The same envelope without a context to paint it on — for laying out marks, measuring them, or
    /// combining a run of them before anything is drawn.
    /// </summary>
    public CanvasPath CreateTaperedStrokePath(object start, object cp1, object cp2, object end, float maxThickness)
    {
        var s = ExtractPoint(start);
        var c1 = ExtractPoint(cp1);
        var c2 = ExtractPoint(cp2);
        var e = ExtractPoint(end);
        return BuildTaperedStroke(s.X, s.Y, c1.X, c1.Y, c2.X, c2.Y, e.X, e.Y, maxThickness);
    }

    static CanvasPath BuildTaperedStroke(float sx, float sy, float cp1x, float cp1y, float cp2x, float cp2y, float ex, float ey, float maxThickness)
    {
        const int steps = 24;
        var points = new (float x, float y, float nx, float ny, float thickness)[steps + 1];

        for (var i = 0; i <= steps; i++)
        {
            var t = (float)i / steps;
            var it = 1f - t;

            // Cubic Bezier position
            var x = it * it * it * sx + 3f * it * it * t * cp1x + 3f * it * t * t * cp2x + t * t * t * ex;
            var y = it * it * it * sy + 3f * it * it * t * cp1y + 3f * it * t * t * cp2y + t * t * t * ey;

            // Derivative for tangent & normal
            var dx = 3f * it * it * (cp1x - sx) + 6f * it * t * (cp2x - cp1x) + 3f * t * t * (ex - cp2x);
            var dy = 3f * it * it * (cp1y - sy) + 6f * it * t * (cp2y - cp1y) + 3f * t * t * (ey - cp2y);
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f) len = 1f;

            var nx = -dy / len;
            var ny = dx / len;
            var thickness = maxThickness * MathF.Sin(t * MathF.PI);

            points[i] = (x, y, nx, ny, thickness);
        }

        var path = new CanvasPath();
        path.MoveTo(points[0].x, points[0].y);

        // Forward pass along positive normal
        for (var i = 0; i <= steps; i++)
        {
            var p = points[i];
            path.LineTo(p.x + p.nx * (p.thickness * 0.5f), p.y + p.ny * (p.thickness * 0.5f));
        }

        // Backward pass along negative normal
        for (var i = steps; i >= 0; i--)
        {
            var p = points[i];
            path.LineTo(p.x - p.nx * (p.thickness * 0.5f), p.y - p.ny * (p.thickness * 0.5f));
        }

        path.ClosePath();
        return path;
    }

    public void DrawFeathering(CanvasRenderingContext2D ctx, object origin, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float lineWidth = 1.2f)
    {
        var pt = ExtractPoint(origin);
        DrawFeathering(ctx, pt.X, pt.Y, angleDeg, count, length, spacing, strokeColor, lineWidth);
    }

    public void DrawFeathering(CanvasRenderingContext2D ctx, float ox, float oy, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float lineWidth = 1.2f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var rad = (angleDeg * MathF.PI) / 180f;
        var dx = MathF.Cos(rad);
        var dy = MathF.Sin(rad);
        var px = -dy;
        var py = dx;

        ctx.Save();
        if (strokeColor != null) ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = lineWidth;
        ctx.LineCap = "round";

        for (var i = 0; i < count; i++)
        {
            var sx = ox + px * (i * spacing);
            var sy = oy + py * (i * spacing);
            var lenVar = length * (0.8f + 0.4f * MathF.Sin(i * 1.5f));
            var ex = sx + dx * lenVar;
            var ey = sy + dy * lenVar;

            ctx.BeginPath();
            ctx.MoveTo(sx, sy);
            ctx.LineTo(ex, ey);
            ctx.Stroke();
        }

        ctx.Restore();
    }

    public void DrawCrossContourHatch(CanvasRenderingContext2D ctx, float cx, float cy, float rx, float ry, float startAngle, float endAngle, int count = 8, object? strokeColor = null, float lineWidth = 1.2f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ctx.Save();
        if (strokeColor != null) ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = lineWidth;

        for (var i = 0; i < count; i++)
        {
            var t = (float)i / Math.Max(1, count - 1);
            var y = cy - ry * 0.5f + t * ry;
            ctx.BeginPath();
            ctx.Ellipse(cx, y, rx, ry * 0.25f, 0f, startAngle, endAngle);
            ctx.Stroke();
        }

        ctx.Restore();
    }

    public void DrawHairRibbon(CanvasRenderingContext2D ctx, object root, object tip, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f)
    {
        var r = ExtractPoint(root);
        var t = ExtractPoint(tip);
        DrawHairRibbon(ctx, r.X, r.Y, t.X, t.Y, bendFactor, width, fillTop, fillUnderside, strokeColor, strokeWidth);
    }

    public void DrawHairRibbon(CanvasRenderingContext2D ctx, float rx, float ry, float tx, float ty, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var dx = tx - rx;
        var dy = ty - ry;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.0001f) dist = 1f;

        var nx = -dy / dist;
        var ny = dx / dist;

        var cp1x = rx + dx * 0.35f + nx * bendFactor;
        var cp1y = ry + dy * 0.35f + ny * bendFactor;
        var cp2x = rx + dx * 0.75f + nx * (bendFactor * 0.8f);
        var cp2y = ry + dy * 0.75f + ny * (bendFactor * 0.8f);

        var rootLowerX = rx + nx * width;
        var rootLowerY = ry + ny * width;
        var cp3x = rootLowerX + dx * 0.70f + nx * (bendFactor * 0.5f);
        var cp3y = rootLowerY + dy * 0.70f + ny * (bendFactor * 0.5f);
        var cp4x = rootLowerX + dx * 0.30f + nx * (bendFactor * 0.7f);
        var cp4y = rootLowerY + dy * 0.30f + ny * (bendFactor * 0.7f);

        ctx.Save();

        // 1. Top Ribbon Surface
        ctx.BeginPath();
        ctx.MoveTo(rx, ry);
        ctx.BezierCurveTo(cp1x, cp1y, cp2x, cp2y, tx, ty);
        ctx.BezierCurveTo(cp3x, cp3y, cp4x, cp4y, rootLowerX, rootLowerY);
        ctx.ClosePath();

        ctx.FillStyle = fillTop;
        ctx.Fill();

        if (strokeColor != null)
        {
            ctx.StrokeStyle = strokeColor;
            ctx.LineWidth = strokeWidth;
            ctx.LineJoin = "round";
            ctx.Stroke();
        }

        // 2. Inner Highlight / Underside Streak
        ctx.BeginPath();
        ctx.MoveTo(rx + dx * 0.15f + nx * (width * 0.3f), ry + dy * 0.15f + ny * (width * 0.3f));
        ctx.BezierCurveTo(
            cp1x + nx * (width * 0.2f), cp1y + ny * (width * 0.2f),
            cp2x + nx * (width * 0.2f), cp2y + ny * (width * 0.2f),
            tx - dx * 0.1f, ty - dy * 0.1f
        );
        ctx.StrokeStyle = fillUnderside;
        ctx.LineWidth = MathF.Max(1.2f, strokeWidth * 0.75f);
        ctx.Stroke();

        ctx.Restore();
    }
    #endregion

    #region Procedural Shaders & Material Presets
    public SKShader CreateHalftoneDotShader(object? options = null)
    {
        var optDict = JsInterop.AsDict(options);
        var dotSpacing = optDict != null && optDict.Contains("dotSpacing")
            ? Convert.ToSingle(optDict["dotSpacing"], CultureInfo.InvariantCulture)
            : 6.5f;

        var colorStr = optDict?["shadowColor"]?.ToString() ?? "#7f4124";
        var shadowColor = SkiaColorParser.Parse(colorStr);

        var resW = 900f;
        var resH = 750f;
        if (optDict != null && optDict.Contains("resolution") && optDict["resolution"] is IList resList && resList.Count >= 2)
        {
            resW = Convert.ToSingle(resList[0], CultureInfo.InvariantCulture);
            resH = Convert.ToSingle(resList[1], CultureInfo.InvariantCulture);
        }

        const string sksl = @"
            uniform float2 u_resolution;
            uniform float4 u_shadowColor;
            uniform float u_dotSpacing;

            half4 main(float2 coord) {
                float2 pos = mod(coord, u_dotSpacing) - (u_dotSpacing * 0.5);
                float dist = length(pos);
                float radius = u_dotSpacing * 0.38;
                float dot = smoothstep(radius + 0.4, radius - 0.4, dist);
                return u_shadowColor * dot;
            }
        ";

        var uniforms = new Dictionary<string, object>
        {
            ["u_resolution"] = new[] { resW, resH },
            ["u_shadowColor"] = new[] { shadowColor.Red / 255f, shadowColor.Green / 255f, shadowColor.Blue / 255f, shadowColor.Alpha / 255f },
            ["u_dotSpacing"] = dotSpacing
        };

        var skiaShaderApi = new SkiaShaderApi();
        return skiaShaderApi.Sksl(sksl, uniforms);
    }

    /// <summary>Twisted-cordage fibre: stretched-Y turbulence, as a value field by default.</summary>
    /// <remarks>
    /// <paramref name="luminanceOnly"/> defaults true because the documented use is a second pass
    /// over a coloured base under <c>overlay</c>, and raw Perlin carries a separate noise field per
    /// channel — which tints the rope in random hues rather than giving it fibre. Pass false for the
    /// unfiltered field; <c>Skia.Shader.perlinNoiseTurbulence(...)</c> is always unfiltered.
    /// </remarks>
    public SKShader CreateRopeFiberShader(float frequencyX = 0.08f, float frequencyY = 0.40f, int octaves = 3, int seed = 42, bool luminanceOnly = true)
    {
        var skiaShaderApi = new SkiaShaderApi();
        var noise = skiaShaderApi.PerlinNoiseTurbulence(frequencyX, frequencyY, octaves, seed);
        return luminanceOnly ? skiaShaderApi.Luminance(noise) : noise;
    }

    /// <summary>Sea-air vapour: isotropic fractal noise, as a value field by default.</summary>
    /// <remarks>
    /// Same reasoning as <c>createRopeFiberShader</c>. The alpha field is left varying, which is what
    /// makes the vapour wispy — greying the noise and flattening alpha together produces an opaque
    /// sheet, so the two corrections are not interchangeable.
    /// </remarks>
    public SKShader CreateAtmosphericCloudShader(float frequencyX = 0.015f, float frequencyY = 0.015f, int octaves = 4, int seed = 101, bool luminanceOnly = true)
    {
        var skiaShaderApi = new SkiaShaderApi();
        var noise = skiaShaderApi.PerlinNoiseFractal(frequencyX, frequencyY, octaves, seed);
        return luminanceOnly ? skiaShaderApi.Luminance(noise) : noise;
    }
    #endregion

    #region Visual Measurement Helpers
    public Dictionary<string, object?> VerifyPlumbAlignment(object topPoint, object bottomPoint, float maxTolerance = 12f)
    {
        var p1 = ExtractPoint(topPoint);
        var p2 = ExtractPoint(bottomPoint);
        var deltaX = MathF.Abs(p1.X - p2.X);
        var aligned = deltaX <= maxTolerance;

        return new Dictionary<string, object?>
        {
            ["aligned"] = aligned,
            ["deltaX"] = deltaX,
            ["message"] = aligned ? "PASS: Vertically aligned" : $"DRIFT: Offset by {deltaX:F1}px (tolerance: {maxTolerance:F1}px)"
        };
    }

    public float ComputeRelativeDistance(float headHeight, object pointA, object pointB)
    {
        if (headHeight <= 0.001f) return 0f;
        var p1 = ExtractPoint(pointA);
        var p2 = ExtractPoint(pointB);
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        return dist / headHeight;
    }
    #endregion

    #region Linear Perspective & 3D Forms
    /// <summary>
    /// The furthest a box side may recede toward its vanishing point, as a fraction of the anchor-to-VP
    /// distance. Beyond this the projection degenerates.
    /// </summary>
    private const float MaxRecession = 0.85f;

    private static Point2D LineIntersection(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
    {
        var denom = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
        if (MathF.Abs(denom) < 0.00001f) return new Point2D((p1.X + p3.X) * 0.5f, (p1.Y + p3.Y) * 0.5f);

        var t = ((p1.X - p3.X) * (p3.Y - p4.Y) - (p1.Y - p3.Y) * (p3.X - p4.X)) / denom;
        return new Point2D(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
    }

    public Dictionary<string, object?> CreatePerspectiveGrid(object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var type = opt?["type"]?.ToString() ?? "2point";
        var horizonY = opt != null && opt.Contains("horizonY") ? Convert.ToSingle(opt["horizonY"], CultureInfo.InvariantCulture) : 300f;
        var cvX = opt != null && opt.Contains("centerOfVisionX") ? Convert.ToSingle(opt["centerOfVisionX"], CultureInfo.InvariantCulture) : 400f;
        var focalLength = opt != null && opt.Contains("focalLength") ? Convert.ToSingle(opt["focalLength"], CultureInfo.InvariantCulture) : 800f;
        var cameraAngleDeg = opt != null && opt.Contains("cameraAngleDeg") ? Convert.ToSingle(opt["cameraAngleDeg"], CultureInfo.InvariantCulture) : 45f;

        var rad = (cameraAngleDeg * MathF.PI) / 180f;
        var tanTheta = MathF.Tan(rad);
        var cotTheta = 1f / MathF.Max(0.001f, tanTheta);

        Point2D vpL;
        Point2D vpR;
        Point2D? vpV = null;

        if (string.Equals(type, "1point", StringComparison.OrdinalIgnoreCase))
        {
            vpL = new Point2D(cvX, horizonY);
            vpR = new Point2D(cvX, horizonY);
        }
        else
        {
            vpL = new Point2D(cvX - focalLength * cotTheta, horizonY);
            vpR = new Point2D(cvX + focalLength * tanTheta, horizonY);
        }

        if (string.Equals(type, "3point", StringComparison.OrdinalIgnoreCase))
        {
            var tiltDeg = opt != null && opt.Contains("tiltAngleDeg") ? Convert.ToSingle(opt["tiltAngleDeg"], CultureInfo.InvariantCulture) : 30f;
            var tiltRad = (tiltDeg * MathF.PI) / 180f;
            var verticalY = horizonY + (tiltDeg >= 0 ? 1 : -1) * (focalLength / MathF.Max(0.001f, MathF.Tan(tiltRad)));
            vpV = new Point2D(cvX, verticalY);
        }

        return new Dictionary<string, object?>
        {
            ["type"] = type,
            ["horizonY"] = horizonY,
            ["cv"] = ToDict(new Point2D(cvX, horizonY)),
            ["vpL"] = ToDict(vpL),
            ["vpR"] = ToDict(vpR),
            ["vpV"] = vpV.HasValue ? ToDict(vpV.Value) : null,
            ["focalLength"] = focalLength,
            ["cameraAngleDeg"] = cameraAngleDeg
        };
    }

    public void DrawPerspectiveGrid(CanvasRenderingContext2D ctx, object gridObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(gridObj) is not IDictionary grid) return;

        var opt = JsInterop.AsDict(options);
        var lineColor = opt?["lineColor"]?.ToString() ?? "#dbe7f2";
        var horizonColor = opt?["horizonColor"]?.ToString() ?? "#4a90e2";
        var lineCount = opt != null && opt.Contains("lineCount") ? Convert.ToInt32(opt["lineCount"]) : 14;
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1f;

        var horizonY = Convert.ToSingle(grid["horizonY"], CultureInfo.InvariantCulture);
        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);

        ctx.Save();

        // 1. Vanishing Point Rays
        ctx.StrokeStyle = lineColor;
        ctx.LineWidth = lineWidth;

        var w = ctx.Canvas.Width;
        var h = ctx.Canvas.Height;

        for (var i = 0; i <= lineCount; i++)
        {
            var targetY = horizonY + (i + 1) * ((h - horizonY) / lineCount);
            // Left VP rays
            ctx.BeginPath();
            ctx.MoveTo(vpL.X, vpL.Y);
            ctx.LineTo(w + 200f, targetY);
            ctx.Stroke();

            // Right VP rays
            ctx.BeginPath();
            ctx.MoveTo(vpR.X, vpR.Y);
            ctx.LineTo(-200f, targetY);
            ctx.Stroke();
        }

        // 2. Horizon Line
        ctx.StrokeStyle = horizonColor;
        ctx.LineWidth = lineWidth * 1.5f;
        ctx.BeginPath();
        ctx.MoveTo(0, horizonY);
        ctx.LineTo(w, horizonY);
        ctx.Stroke();

        ctx.Restore();
    }

    /// <summary>
    /// Steps <paramref name="extent"/> screen pixels from <paramref name="from"/> along the ray to
    /// <paramref name="vp"/>, refusing any extent that would reach the vanishing point.
    /// </summary>
    private static Point2D Recede(Point2D from, Point2D vp, float extent, string name, string vpName)
    {
        var dx = vp.X - from.X;
        var dy = vp.Y - from.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.0001f) len = 1f;

        var t = extent / len;
        if (t > MaxRecession)
        {
            throw new ArgumentOutOfRangeException(name, extent,
                $"Drawing.createPerspectiveBox(...) was given a {name} of {extent:F1}px, which is {t:P0} of the " +
                $"{len:F1}px from the anchor to {vpName}. Past {MaxRecession:P0} the far corner reaches the " +
                $"vanishing point and the box turns inside out. Reduce the {name} to at most " +
                $"{len * MaxRecession:F1}px, or move the anchor further from {vpName}.");
        }

        return new Point2D(from.X + dx * t, from.Y + dy * t);
    }

    public Dictionary<string, object?> CreatePerspectiveBox(object gridObj, float anchorX, float anchorY, float width, float height, float depth)
    {
        if (JsInterop.AsDict(gridObj) is not IDictionary grid)
            throw new ArgumentException("gridObj must be a valid perspective grid dictionary", nameof(gridObj));

        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);

        var v0 = new Point2D(anchorX, anchorY);
        var v4 = new Point2D(anchorX, anchorY - height);

        // Width and depth are screen distances stepped along the ray to each vanishing point. Past
        // MaxRecession the far corner arrives at the vanishing point and the box turns inside out, so the
        // request is refused rather than clamped: a silently shortened box is a wrong drawing that looks
        // like the one that was asked for.
        var v1 = Recede(v0, vpL, width, "width", "the left vanishing point");
        var v2 = Recede(v0, vpR, depth, "depth", "the right vanishing point");

        // Top left & top right points
        var v5 = LineIntersection(v4, vpL, v1, new Point2D(v1.X, v1.Y - height * 2f));
        var v6 = LineIntersection(v4, vpR, v2, new Point2D(v2.X, v2.Y - height * 2f));

        // Back bottom & back top
        var v3 = LineIntersection(v1, vpR, v2, vpL);
        var v7 = LineIntersection(v5, vpR, v6, vpL);

        List<Dictionary<string, object?>> ToFace(params Point2D[] pts)
        {
            var list = new List<Dictionary<string, object?>>();
            foreach (var p in pts) list.Add(ToDict(p));
            return list;
        }

        return new Dictionary<string, object?>
        {
            ["vertices"] = new List<Dictionary<string, object?>>
            {
                ToDict(v0), ToDict(v1), ToDict(v2), ToDict(v3),
                ToDict(v4), ToDict(v5), ToDict(v6), ToDict(v7)
            },
            ["faces"] = new Dictionary<string, object?>
            {
                ["top"] = ToFace(v4, v5, v7, v6),
                ["bottom"] = ToFace(v0, v1, v3, v2),
                ["left"] = ToFace(v0, v1, v5, v4),
                ["right"] = ToFace(v0, v2, v6, v4),
                ["backLeft"] = ToFace(v2, v3, v7, v6),
                ["backRight"] = ToFace(v1, v3, v7, v5)
            }
        };
    }

    /// <summary>
    /// Draws the box and <b>returns its faces as geometry</b>: <c>faces</c> (a
    /// <see cref="CanvasPath"/> per face, including the hidden ones whether or not they were drawn)
    /// and <c>silhouette</c>, the three visible faces unioned into the box's outline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A face is the natural unit to <i>clip a texture to</i>, which is what a box in a scene is
    /// usually for — a crate's planking, a wall's brick, a floor's tiling all want the quad the
    /// projection produced, and re-deriving one from <c>box.faces</c> means re-walking the point
    /// list on every use. The <c>silhouette</c> is what a cast shadow, a rim light or an occluding
    /// clip wants instead.
    /// </para>
    /// <para>
    /// The hidden faces are returned even when <c>drawHiddenLines</c> is false: building a path is
    /// not drawing it, and a caller staging occlusion needs the back of the box precisely when it is
    /// not being drawn.
    /// </para>
    /// <para>A script that ignores the return value behaves exactly as before.</para>
    /// </remarks>
    public Dictionary<string, object?> DrawPerspectiveBox(CanvasRenderingContext2D ctx, object boxObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(boxObj) is not IDictionary box) return [];

        var opt = JsInterop.AsDict(options);
        var topFill = opt?["topFill"]?.ToString() ?? "#e6f0fa";
        var leftFill = opt?["leftFill"]?.ToString() ?? "#b8d5f2";
        var rightFill = opt?["rightFill"]?.ToString() ?? "#7baad4";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;
        var drawHidden = opt != null && opt.Contains("drawHiddenLines") && Convert.ToBoolean(opt["drawHiddenLines"]);

        var faces = JsInterop.AsDict(box["faces"]);
        if (faces == null) return [];

        // Every face the projection produced, drawn or not.
        var built = new Dictionary<string, CanvasPath>();
        foreach (var name in new[] { "bottom", "backLeft", "backRight", "left", "right", "top" })
        {
            if (faces[name] is not IList pts || pts.Count < 3) continue;
            var quad = new CanvasPath();
            var first = ExtractPoint(pts[0]);
            quad.MoveTo(first.X, first.Y);
            for (var i = 1; i < pts.Count; i++)
            {
                var p = ExtractPoint(pts[i]);
                quad.LineTo(p.X, p.Y);
            }
            quad.ClosePath();
            built[name] = quad;
        }

        void RenderFace(string name, object? fill)
        {
            if (!built.TryGetValue(name, out var quad)) return;
            if (fill != null)
            {
                ctx.FillStyle = fill;
                ctx.Fill(quad);
            }
            if (strokeColor != null)
            {
                ctx.StrokeStyle = strokeColor;
                ctx.LineWidth = strokeWidth;
                ctx.LineJoin = "round";
                ctx.Stroke(quad);
            }
        }

        ctx.Save();

        if (drawHidden)
        {
            ctx.Save();
            ctx.StrokeStyle = "#9bbcd9";
            ctx.LineWidth = strokeWidth * 0.7f;
            RenderFace("bottom", null);
            RenderFace("backLeft", null);
            RenderFace("backRight", null);
            ctx.Restore();
        }

        RenderFace("left", leftFill);
        RenderFace("right", rightFill);
        RenderFace("top", topFill);

        ctx.Restore();

        CanvasPath? outline = null;
        foreach (var name in new[] { "left", "right", "top" })
            if (built.TryGetValue(name, out var quad))
                outline = outline == null ? quad : outline.Union(quad);

        var faceDict = new Dictionary<string, object?>();
        foreach (var kv in built) faceDict[kv.Key] = kv.Value;

        return new Dictionary<string, object?>
        {
            ["faces"] = faceDict,
            ["silhouette"] = outline == null ? new CanvasPath() : outline.Simplify()
        };
    }

    /// <summary>
    /// Draws an upright cylinder, both caps foreshortened by the grid at their own screen height.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="anchorX"/>/<paramref name="anchorY"/> is the centre of the <em>base circle</em>
    /// and <paramref name="radius"/> is half the cylinder's drawn width, so the silhouette lands
    /// where a caller put it and can be checked with a ruler. Both were previously otherwise: the
    /// call built a <c>2r x 2r</c> perspective box, anchored at that box's near <em>corner</em>, and
    /// took its width from the footprint's diagonal — which drew at 1.9x the requested width, off
    /// centre, without erroring.
    /// </para>
    /// <para>
    /// There is deliberately no elevation parameter. A cap's flatness is read from the directions to
    /// the vanishing points at its own centre, and a point nearer the horizon has shallower rays, so
    /// a bowl on a counter is flatter than the same bowl on the floor for free. That is also why the
    /// two caps get separate ellipses rather than one shared squash.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Draws an upright cylinder, both caps foreshortened by the grid at their own screen height.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="anchorX"/>/<paramref name="anchorY"/> is the centre of the <em>base circle</em>
    /// and <paramref name="radius"/> is half the cylinder's drawn width, so the silhouette lands
    /// where a caller put it and can be checked with a ruler. Both were previously otherwise: the
    /// call built a <c>2r x 2r</c> perspective box, anchored at that box's near <em>corner</em>, and
    /// took its width from the footprint's diagonal — which drew at 1.9x the requested width, off
    /// centre, without erroring.
    /// </para>
    /// <para>
    /// There is deliberately no elevation parameter. A cap's flatness is read from the directions to
    /// the vanishing points at its own centre, and a point nearer the horizon has shallower rays, so
    /// a bowl on a counter is flatter than the same bowl on the floor for free. That is also why the
    /// two caps get separate ellipses rather than one shared squash.
    /// </para>
    /// <para>
    /// <b>Both caps are axis-aligned</b>, after Norling: the long axis of the ellipse forms a T with
    /// the cylinder's upright line, so on an upright cylinder it is horizontal. An earlier version
    /// built each cap from conjugate semi-diameters toward the two vanishing points, which tilted the
    /// cap away from the centre of vision — measured at 13-15 px on a 140 px cap placed 250-300 px
    /// off axis, which reads as a leaning bottle. A true wide-angle projection does tilt a circle off
    /// axis, but this grid is a screen-space construction rather than a metric camera, so the drawing
    /// convention is the one to keep.
    /// </para>
    /// </remarks>
    public void DrawPerspectiveCylinder(CanvasRenderingContext2D ctx, object gridObj, float anchorX, float anchorY, float radius, float height, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(gridObj) is not IDictionary grid)
            throw new ArgumentException("gridObj must be a valid perspective grid dictionary", nameof(gridObj));

        var opt = JsInterop.AsDict(options);
        var sideFill = opt?["sideFill"]?.ToString() ?? "#b8d5f2";
        var topFill = opt?["topFill"]?.ToString() ?? "#e6f0fa";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;

        var (vpL, vpR) = CircleAxisVanishingPoints(grid);

        var baseY = anchorY;
        var topY = anchorY - height;
        var baseRy = GroundCircleSquash(vpL, vpR, new Point2D(anchorX, baseY), radius);
        var topRy = GroundCircleSquash(vpL, vpR, new Point2D(anchorX, topY), radius);

        ctx.Save();

        // 1. Body — the near half of each cap, joined by the two vertical contours. Each arc starts
        // at the tangent extreme it meets (PI on the left, 0 on the right); sweeping either from the
        // far side folds the body into a bowtie.
        ctx.FillStyle = sideFill;
        ctx.BeginPath();
        ctx.MoveTo(anchorX - radius, baseY);
        ctx.LineTo(anchorX - radius, topY);
        ctx.Ellipse(anchorX, topY, radius, topRy, 0f, MathF.PI, 0f, true);
        ctx.LineTo(anchorX + radius, baseY);
        ctx.Ellipse(anchorX, baseY, radius, baseRy, 0f, 0f, MathF.PI);
        ctx.ClosePath();
        ctx.Fill();

        ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = strokeWidth;
        ctx.LineJoin = "round";

        // Contours. Vertical by construction now that the caps share a horizontal major axis.
        ctx.BeginPath();
        ctx.MoveTo(anchorX - radius, baseY);
        ctx.LineTo(anchorX - radius, topY);
        ctx.MoveTo(anchorX + radius, baseY);
        ctx.LineTo(anchorX + radius, topY);
        ctx.Stroke();

        // Base contour: only the near half is visible past the body.
        ctx.BeginPath();
        ctx.Ellipse(anchorX, baseY, radius, baseRy, 0f, 0f, MathF.PI);
        ctx.Stroke();

        // 2. Top cap, whole — its own squash, not the base's.
        ctx.FillStyle = topFill;
        ctx.BeginPath();
        ctx.Ellipse(anchorX, topY, radius, topRy, 0f, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        ctx.Restore();
    }

    /// <summary>The two vanishing points to take a horizontal circle's foreshortening from.</summary>
    /// <remarks>
    /// A circle has equal radii along <em>any</em> perpendicular pair of ground directions, so either
    /// axis family will do. A one-point grid puts both vanishing points on the centre of vision,
    /// which leaves the two directions coincident and collapses the ellipse to a line; the 45-degree
    /// distance points at <c>cv +/- focalLength</c> are a perpendicular pair in the same plane and
    /// are not degenerate.
    /// </remarks>
    private static (Point2D VpL, Point2D VpR) CircleAxisVanishingPoints(IDictionary grid)
    {
        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);
        if (MathF.Abs(vpR.X - vpL.X) > 1f) return (vpL, vpR);

        var horizonY = Convert.ToSingle(grid["horizonY"], CultureInfo.InvariantCulture);
        var cv = ExtractPoint(grid["cv"]);
        var focalLength = grid.Contains("focalLength")
            ? Convert.ToSingle(grid["focalLength"], CultureInfo.InvariantCulture)
            : 800f;
        return (new Point2D(cv.X - focalLength, horizonY), new Point2D(cv.X + focalLength, horizonY));
    }

    /// <summary>
    /// Half the drawn <em>height</em> of a horizontal circle whose drawn half-width is
    /// <paramref name="radius"/>, centred at <paramref name="centre"/>.
    /// </summary>
    /// <remarks>
    /// Taken from the ellipse the two ground directions would describe — semi-diameters along each,
    /// scaled so the horizontal extent comes out at exactly <paramref name="radius"/> — and then only
    /// its vertical extent is kept, because the cap is drawn axis-aligned. So the foreshortening still
    /// comes from the grid while the major axis stays square to the cylinder.
    /// </remarks>
    private static float GroundCircleSquash(Point2D vpL, Point2D vpR, Point2D centre, float radius)
    {
        static Point2D Unit(Point2D from, Point2D to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            return len < 0.0001f ? new Point2D(1f, 0f) : new Point2D(dx / len, dy / len);
        }

        var uL = Unit(centre, vpL);
        var uR = Unit(centre, vpR);

        // Half the drawn width of an ellipse with conjugate semi-diameters a and b is
        // sqrt(a.x^2 + b.x^2), so this is the scale that makes it come out at exactly radius.
        var spread = MathF.Sqrt(uL.X * uL.X + uR.X * uR.X);
        var k = spread < 0.0001f ? radius : radius / spread;

        return k * MathF.Sqrt(uL.Y * uL.Y + uR.Y * uR.Y);
    }

    /// <summary>The two vanishing points to take a horizontal circle's conjugate diameters from.</summary>
    /// <remarks>
    /// A circle has equal radii along <em>any</em> perpendicular pair of ground directions, so either
    /// axis family will do. A one-point grid puts both vanishing points on the centre of vision,
    /// which leaves the two directions coincident and collapses the ellipse to a line; the 45-degree
    /// distance points at <c>cv +/- focalLength</c> are a perpendicular pair in the same plane and
    /// are not degenerate.
    /// </remarks>
    /// <summary>
    /// Conjugate semi-diameters of a horizontal circle centred at <paramref name="centre"/>, scaled so
    /// the ellipse is exactly <c>2 * radius</c> wide on screen.
    /// </summary>
    /// <remarks>
    /// Normalising each cap to the same drawn width is what keeps the contours vertical, as a
    /// vertical cylinder's silhouette must be; only the flatness is left to vary with height.
    /// </remarks>
    /// <summary>The two parameters at which the ellipse reaches its extreme x — its silhouette edges.</summary>
    /// <remarks>
    /// dx/dt is zero where <c>tan t = b.x / a.x</c>; the two roots are half a turn apart. Returned
    /// left-then-right so callers do not have to compare them again.
    /// </remarks>
    /// <summary>
    /// Which way round to sweep from <paramref name="from"/> to <paramref name="to"/> to take the
    /// near half of the ellipse — the half lower on screen.
    /// </summary>
    /// <summary>Appends an elliptical arc as a polyline; the ellipse is rotated, so ctx.Ellipse cannot carry it.</summary>
    public List<List<Dictionary<string, object?>>> SubdividePerspectiveQuad(object quadObj, int uCount, int vCount)
    {
        if (quadObj is not IList pts || pts.Count < 4)
            throw new ArgumentException("quadObj must contain 4 points [P0, P1, P2, P3]", nameof(quadObj));

        var p0 = ExtractPoint(pts[0]);
        var p1 = ExtractPoint(pts[1]);
        var p2 = ExtractPoint(pts[2]);
        var p3 = ExtractPoint(pts[3]);

        var grid = new List<List<Dictionary<string, object?>>>();

        for (var v = 0; v < vCount; v++)
        {
            var v0 = (float)v / vCount;
            var v1 = (float)(v + 1) / vCount;

            for (var u = 0; u < uCount; u++)
            {
                var u0 = (float)u / uCount;
                var u1 = (float)(u + 1) / uCount;

                Point2D Interp(float ui, float vi)
                {
                    var x = (1f - ui) * (1f - vi) * p0.X + ui * (1f - vi) * p1.X + ui * vi * p2.X + (1f - ui) * vi * p3.X;
                    var y = (1f - ui) * (1f - vi) * p0.Y + ui * (1f - vi) * p1.Y + ui * vi * p2.Y + (1f - ui) * vi * p3.Y;
                    return new Point2D(x, y);
                }

                var cell = new List<Dictionary<string, object?>>
                {
                    ToDict(Interp(u0, v0)),
                    ToDict(Interp(u1, v0)),
                    ToDict(Interp(u1, v1)),
                    ToDict(Interp(u0, v1))
                };
                grid.Add(cell);
            }
        }

        return grid;
    }

    public Dictionary<string, object?> VerifyPerspectiveConvergence(object linesList, object expectedVp, float maxToleranceDeg = 5f)
    {
        var vp = ExtractPoint(expectedVp);
        if (linesList is not IList lines || lines.Count == 0)
            return new Dictionary<string, object?> { ["passed"] = true, ["testedLines"] = 0 };

        var passed = true;
        var maxError = 0f;

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i] is not IList pts || pts.Count < 2) continue;
            var a = ExtractPoint(pts[0]);
            var b = ExtractPoint(pts[1]);

            // Anchored at the midpoint, and folded to 90°, so the answer does not depend on which
            // end of the line was listed first. A line is undirected: it converges on a vanishing
            // point whether you name it top-to-bottom or bottom-to-top.
            //
            // Without the fold, listing a line away from the vanishing point reported ~179.9° — a
            // confident total-drift verdict on a line that converges perfectly. Worse, the failing
            // order is the natural one to write: a vertical listed from the top of the frame
            // downward, with the vanishing point above it. Found by a comic-studio agent, which
            // caught it only because 179.9 is too round a number to be a real measurement.
            var mid = new SKPoint((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

            var drawnAngle = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
            var targetAngle = MathF.Atan2(vp.Y - mid.Y, vp.X - mid.X) * 180f / MathF.PI;

            var diff = MathF.Abs(drawnAngle - targetAngle);
            if (diff > 180f) diff = 360f - diff;   // an angle difference is at most 180°
            if (diff > 90f) diff = 180f - diff;    // and a line's direction is modulo 180°

            if (diff > maxError) maxError = diff;
            if (diff > maxToleranceDeg) passed = false;
        }

        return new Dictionary<string, object?>
        {
            ["passed"] = passed,
            ["maxAngularErrorDeg"] = maxError,
            ["testedLines"] = lines.Count,
            ["message"] = passed ? "PASS: Lines converge correctly" : $"DRIFT: Max angular error of {maxError:F1}° exceeds {maxToleranceDeg:F1}° tolerance"
        };
    }
    #endregion

    #region Volumetric Lighting & Cast Shadows
    /// <summary>
    /// Projects a cast shadow onto the ground. <paramref name="groundOrGrid"/> selects the model:
    /// a number is a ground <em>line</em> at that Y (a 2D elevation, where depth comes from
    /// <c>options.groundDepth</c>); a <c>PerspectiveGrid</c> is a ground <em>plane</em>, and the
    /// footprint is then a true projective shadow needing no depth hint.
    /// </summary>
    public Dictionary<string, object?> ProjectCastShadow(object lightSource, object groundOrGrid, object objectVerticesOrBounds, object? options = null)
    {
        var light = ExtractPoint(lightSource);
        var opt = JsInterop.AsDict(options);
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#1c2733";
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.65f;
        var penumbraBlur = opt != null && opt.Contains("penumbraBlur") ? Convert.ToSingle(opt["penumbraBlur"], CultureInfo.InvariantCulture) : 6f;

        var grid = JsInterop.AsDict(groundOrGrid);
        var hasHorizon = grid != null && grid.Contains("horizonY");

        var pairs = ExtractShadowCasters(objectVerticesOrBounds, hasHorizon ? null : Convert.ToSingle(groundOrGrid, CultureInfo.InvariantCulture));

        return hasHorizon
            ? ProjectOntoGroundPlane(light, Convert.ToSingle(grid!["horizonY"], CultureInfo.InvariantCulture), pairs, shadowColor, opacity, penumbraBlur)
            : ProjectOntoGroundLine(light, Convert.ToSingle(groundOrGrid, CultureInfo.InvariantCulture), pairs, opt, shadowColor, opacity, penumbraBlur);
    }

    /// <summary>
    /// The 2D elevation model. Every light ray meets the ground line at the same Y, so the projected
    /// vertices are collinear — a ground plane seen edge-on has no thickness. <c>groundDepth</c>
    /// supplies the recession the projection cannot know; the light still sets length and direction.
    /// </summary>
    private static Dictionary<string, object?> ProjectOntoGroundLine(
        Point2D light, float groundY, List<(Point2D Base, Point2D Top)> pairs,
        IDictionary? opt, string shadowColor, float opacity, float penumbraBlur)
    {
        var basePts = new List<Point2D>();
        var farPts = new List<Point2D>();

        foreach (var (basePt, top) in pairs)
        {
            var dy = top.Y - light.Y;
            if (MathF.Abs(dy) < 0.001f) dy = 0.001f;
            var t = (groundY - light.Y) / dy;

            basePts.Add(new Point2D(basePt.X, groundY));
            farPts.Add(new Point2D(light.X + t * (top.X - light.X), groundY));
        }

        var shadowPts = new List<Dictionary<string, object?>>();
        var groundDepth = 0f;

        if (basePts.Count >= 2)
        {
            // Default the depth from the contact footprint, so a caller that supplies no option still
            // gets a plausible shadow rather than an invisible sliver.
            var extent = basePts.Max(p => p.X) - basePts.Min(p => p.X);
            groundDepth = opt != null && opt.Contains("groundDepth")
                ? Convert.ToSingle(opt["groundDepth"], CultureInfo.InvariantCulture)
                : MathF.Max(4f, extent * 0.20f);

            foreach (var pt in basePts) shadowPts.Add(ToDict(pt));
            for (var i = farPts.Count - 1; i >= 0; i--)
            {
                shadowPts.Add(ToDict(new Point2D(farPts[i].X, groundY + groundDepth)));
            }
        }

        return new Dictionary<string, object?>
        {
            ["shadowPolygon"] = shadowPts,
            ["model"] = "groundLine",
            ["groundY"] = groundY,
            ["groundDepth"] = groundDepth,
            ["shadowColor"] = shadowColor,
            ["opacity"] = opacity,
            ["penumbraBlur"] = penumbraBlur
        };
    }

    /// <summary>
    /// The perspective model — the classical architectural construction, and the reason a
    /// <c>PerspectiveGrid</c> is worth passing.
    /// <para>
    /// The light is read as the vanishing point of the light rays, so its ground-projected rays
    /// converge on the horizon directly beneath it. Each shadow vertex is the meeting of the ray
    /// through the top vertex with the ground ray through the matching contact point. Because contact
    /// points at different depths sit at different image heights, the footprint comes out as a real
    /// receding quad — no <c>groundDepth</c> hint required.
    /// </para>
    /// </summary>
    private static Dictionary<string, object?> ProjectOntoGroundPlane(
        Point2D light, float horizonY, List<(Point2D Base, Point2D Top)> pairs,
        string shadowColor, float opacity, float penumbraBlur)
    {
        var vpShadow = new Point2D(light.X, horizonY);
        var basePts = new List<Point2D>();
        var shadowVerts = new List<Point2D>();

        foreach (var (basePt, top) in pairs)
        {
            var hit = IntersectLines(light, top, vpShadow, basePt)
                ?? throw new ArgumentException(
                    $"The light ray through ({top.X:F1}, {top.Y:F1}) runs parallel to the ground ray through its " +
                    $"contact point ({basePt.X:F1}, {basePt.Y:F1}), so the shadow never lands. Move the light off " +
                    "the horizon line, or away from directly above the object.",
                    nameof(light));

            if (hit.Y <= horizonY)
            {
                throw new ArgumentException(
                    $"The shadow of ({top.X:F1}, {top.Y:F1}) falls at or beyond the horizon (y {hit.Y:F1} <= " +
                    $"horizonY {horizonY:F1}), which is infinitely far away. Raise the light above the object, " +
                    "or move it further from the horizon.",
                    nameof(light));
            }

            basePts.Add(basePt);
            shadowVerts.Add(hit);
        }

        var shadowPts = new List<Dictionary<string, object?>>();
        if (basePts.Count >= 2)
        {
            foreach (var pt in basePts) shadowPts.Add(ToDict(pt));
            for (var i = shadowVerts.Count - 1; i >= 0; i--) shadowPts.Add(ToDict(shadowVerts[i]));
        }

        return new Dictionary<string, object?>
        {
            ["shadowPolygon"] = shadowPts,
            ["model"] = "perspective",
            ["horizonY"] = horizonY,
            ["groundY"] = basePts.Count > 0 ? basePts.Max(p => p.Y) : horizonY,
            ["vpShadow"] = ToDict(vpShadow),
            ["shadowColor"] = shadowColor,
            ["opacity"] = opacity,
            ["penumbraBlur"] = penumbraBlur
        };
    }

    /// <summary>
    /// Resolves the caster into (contact point, top vertex) pairs. A <c>PerspectiveBox</c> already
    /// carries both, which is what makes the perspective model exact; a bounds rect supplies the top
    /// and bottom edges; a bare point list is tops only, so it needs a ground line to stand on.
    /// </summary>
    private static List<(Point2D Base, Point2D Top)> ExtractShadowCasters(object shape, float? groundY)
    {
        var pairs = new List<(Point2D, Point2D)>();
        var dict = JsInterop.AsDict(shape);

        if (dict != null && dict.Contains("vertices") && dict["vertices"] is IList verts && verts.Count >= 8)
        {
            // PerspectiveBox: V0..V3 are the base, V4..V7 the matching top. The bottom face walks
            // V0, V1, V3, V2 (Manual 06 section 2), so follow that order or the quad self-crosses.
            foreach (var i in new[] { 0, 1, 3, 2 })
            {
                pairs.Add((ExtractPoint(verts[i]), ExtractPoint(verts[i + 4])));
            }
            return pairs;
        }

        if (dict != null && dict.Contains("width") && dict.Contains("height"))
        {
            var bx = Convert.ToSingle(dict["x"], CultureInfo.InvariantCulture);
            var by = Convert.ToSingle(dict["y"], CultureInfo.InvariantCulture);
            var bw = Convert.ToSingle(dict["width"], CultureInfo.InvariantCulture);
            var bh = Convert.ToSingle(dict["height"], CultureInfo.InvariantCulture);

            pairs.Add((new Point2D(bx, by + bh), new Point2D(bx, by)));
            pairs.Add((new Point2D(bx + bw, by + bh), new Point2D(bx + bw, by)));
            return pairs;
        }

        if (shape is IList list)
        {
            foreach (var item in list)
            {
                var entry = JsInterop.AsDict(item);
                if (entry != null && entry.Contains("top") && entry.Contains("base"))
                {
                    pairs.Add((ExtractPoint(entry["base"]), ExtractPoint(entry["top"])));
                    continue;
                }

                var top = ExtractPoint(item);
                if (groundY is not float line)
                {
                    throw new ArgumentException(
                        "A bare point list gives top vertices with no contact points, so it cannot be projected " +
                        "onto a perspective ground plane. Pass a PerspectiveBox from Drawing.createPerspectiveBox(...), " +
                        "a bounds rect, or a list of { top, base } pairs.",
                        nameof(shape));
                }
                pairs.Add((new Point2D(top.X, line), top));
            }
        }

        return pairs;
    }

    /// <summary>Meeting point of the lines through (a1, a2) and (b1, b2); null when they are parallel.</summary>
    private static Point2D? IntersectLines(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        var dxA = a2.X - a1.X;
        var dyA = a2.Y - a1.Y;
        var dxB = b2.X - b1.X;
        var dyB = b2.Y - b1.Y;

        var denominator = (dxA * dyB) - (dyA * dxB);
        if (MathF.Abs(denominator) < 1e-5f) return null;

        var t = (((b1.X - a1.X) * dyB) - ((b1.Y - a1.Y) * dxB)) / denominator;
        return new Point2D(a1.X + (t * dxA), a1.Y + (t * dyA));
    }

    public void DrawCastShadow(CanvasRenderingContext2D ctx, object shadowPolygonOrResult, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        IList? pts = null;
        var shadowColor = "#1c2733";
        var opacity = 0.65f;
        var blur = 6f;

        if (JsInterop.AsDict(shadowPolygonOrResult) is IDictionary res && res.Contains("shadowPolygon"))
        {
            pts = res["shadowPolygon"] as IList;
            if (res.Contains("shadowColor")) shadowColor = res["shadowColor"]?.ToString() ?? shadowColor;
            if (res.Contains("opacity")) opacity = Convert.ToSingle(res["opacity"], CultureInfo.InvariantCulture);
            if (res.Contains("penumbraBlur")) blur = Convert.ToSingle(res["penumbraBlur"], CultureInfo.InvariantCulture);
        }
        else if (shadowPolygonOrResult is IList list)
        {
            pts = list;
        }

        var opt = JsInterop.AsDict(options);
        if (opt != null)
        {
            if (opt.Contains("shadowColor")) shadowColor = opt["shadowColor"]?.ToString() ?? shadowColor;
            if (opt.Contains("opacity")) opacity = Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture);
            if (opt.Contains("blur")) blur = Convert.ToSingle(opt["blur"], CultureInfo.InvariantCulture);
        }

        if (pts == null || pts.Count < 3)
        {
            throw new ArgumentException(
                $"DrawCastShadow needs a polygon of at least 3 points, got {pts?.Count ?? 0}. " +
                "Pass the result of Drawing.projectCastShadow(...) or a point list.",
                nameof(shadowPolygonOrResult));
        }

        // A polygon with no area fills to nothing. Report it rather than returning quietly, so a shadow
        // that silently fails to appear is a visible error instead of a mystery.
        var corners = new List<Point2D>();
        for (var i = 0; i < pts.Count; i++) corners.Add(ExtractPoint(pts[i]));
        var spanX = corners.Max(p => p.X) - corners.Min(p => p.X);
        var spanY = corners.Max(p => p.Y) - corners.Min(p => p.Y);

        if (spanX < 0.5f || spanY < 0.5f)
        {
            throw new ArgumentException(
                $"DrawCastShadow was given a degenerate polygon ({spanX:F2} x {spanY:F2}px) that would fill nothing. " +
                "A ground plane seen edge-on projects to a line: pass a 'groundDepth' option to " +
                "Drawing.projectCastShadow(...) to give the footprint depth.",
                nameof(shadowPolygonOrResult));
        }

        ctx.Save();
        ctx.GlobalAlpha = opacity;
        ctx.FillStyle = shadowColor;
        if (blur > 0.5f)
        {
            ctx.ShadowColor = shadowColor;
            ctx.ShadowBlur = blur;
        }

        ctx.BeginPath();
        var first = ExtractPoint(pts[0]);
        ctx.MoveTo(first.X, first.Y);
        for (var i = 1; i < pts.Count; i++)
        {
            var p = ExtractPoint(pts[i]);
            ctx.LineTo(p.X, p.Y);
        }
        ctx.ClosePath();
        ctx.Fill();

        ctx.Restore();
    }

    public void RenderVolumetricSphere(CanvasRenderingContext2D ctx, float cx, float cy, float radius, object? lightDirection = null, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        var baseColor = opt?["baseColor"]?.ToString() ?? "#c4a382";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#3a281e";
        var highlightColor = opt?["highlightColor"]?.ToString() ?? "#fff6e8";
        var bounceColor = opt?["bounceColor"]?.ToString() ?? "#556e8c";
        var drawGroundShadow = opt == null || !opt.Contains("drawGroundShadow") || Convert.ToBoolean(opt["drawGroundShadow"]);

        var lDir = ExtractPoint(lightDirection, -0.6f, -0.6f);
        var len = MathF.Sqrt(lDir.X * lDir.X + lDir.Y * lDir.Y);
        if (len < 0.001f) len = 1f;
        var lx = lDir.X / len;
        var ly = lDir.Y / len;

        ctx.Save();

        // 1. Ground Cast Shadow (if enabled)
        if (drawGroundShadow)
        {
            var groundY = cy + radius + 8f;
            var shadowCx = cx - lx * (radius * 0.7f);
            var shadowRx = radius * 1.15f;
            var shadowRy = radius * 0.28f;

            ctx.Save();
            ctx.FillStyle = "#1c140e";
            ctx.GlobalAlpha = 0.55f;
            ctx.ShadowColor = "#1c140e";
            ctx.ShadowBlur = 8f;
            ctx.BeginPath();
            ctx.Ellipse(shadowCx, groundY, shadowRx, shadowRy, 0f, 0f, MathF.PI * 2f);
            ctx.Fill();
            ctx.Restore();
        }

        // 2. Base Sphere Fill & 3D Lighting Radial Gradient
        var lightSpotX = cx + lx * (radius * 0.45f);
        var lightSpotY = cy + ly * (radius * 0.45f);

        var grad = ctx.CreateRadialGradient(lightSpotX, lightSpotY, radius * 0.1f, cx, cy, radius * 1.05f);
        grad.AddColorStop(0.0f, highlightColor);
        grad.AddColorStop(0.35f, baseColor);
        grad.AddColorStop(0.75f, shadowColor);
        grad.AddColorStop(1.0f, shadowColor);

        ctx.FillStyle = grad;
        ctx.BeginPath();
        ctx.Arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.Fill();

        // 3. Ambient Bounce / Reflected Light on shadow side
        var bounceSpotX = cx - lx * (radius * 0.65f);
        var bounceSpotY = cy - ly * (radius * 0.65f);

        ctx.Save();
        ctx.BeginPath();
        ctx.Arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.Clip();

        var bounceGrad = ctx.CreateRadialGradient(bounceSpotX, bounceSpotY, radius * 0.05f, bounceSpotX, bounceSpotY, radius * 0.75f);
        bounceGrad.AddColorStop(0.0f, bounceColor);
        bounceGrad.AddColorStop(1.0f, "rgba(0,0,0,0)");

        ctx.GlobalAlpha = 0.45f;
        ctx.GlobalCompositeOperation = "screen";
        ctx.FillStyle = bounceGrad;
        ctx.BeginPath();
        ctx.Arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Restore();

        // 4. Specular Highlight Glint
        ctx.Save();
        ctx.FillStyle = "#ffffff";
        ctx.GlobalAlpha = 0.85f;
        ctx.BeginPath();
        ctx.Ellipse(lightSpotX - 2f, lightSpotY - 2f, radius * 0.18f, radius * 0.12f, -0.3f, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Restore();

        ctx.Restore();
    }

    public void RenderVolumetricCylinder(CanvasRenderingContext2D ctx, float x, float y, float width, float height, object? lightDirection = null, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        var baseColor = opt?["baseColor"]?.ToString() ?? "#c4a382";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#3a281e";
        var highlightColor = opt?["highlightColor"]?.ToString() ?? "#fff6e8";
        var bounceColor = opt?["bounceColor"]?.ToString() ?? "#556e8c";

        var ry = width * 0.22f;
        var rx = width * 0.5f;
        var cx = x + rx;

        ctx.Save();

        // 1. Cylinder Body Longitudinal Gradient
        var grad = ctx.CreateLinearGradient(x, y, x + width, y);
        grad.AddColorStop(0.0f, shadowColor);
        grad.AddColorStop(0.25f, highlightColor);
        grad.AddColorStop(0.55f, baseColor);
        grad.AddColorStop(0.85f, shadowColor);
        grad.AddColorStop(1.0f, bounceColor);

        ctx.FillStyle = grad;
        ctx.BeginPath();
        ctx.MoveTo(x, y + ry);
        ctx.LineTo(x, y + height - ry);
        ctx.Ellipse(cx, y + height - ry, rx, ry, 0f, 0f, MathF.PI);
        ctx.LineTo(x + width, y + ry);
        ctx.Ellipse(cx, y + ry, rx, ry, 0f, 0f, MathF.PI);
        ctx.ClosePath();
        ctx.Fill();

        // 2. Top Elliptical Cap
        var topGrad = ctx.CreateRadialGradient(cx, y + ry * 0.6f, rx * 0.1f, cx, y + ry, rx);
        topGrad.AddColorStop(0.0f, highlightColor);
        topGrad.AddColorStop(1.0f, baseColor);

        ctx.FillStyle = topGrad;
        ctx.BeginPath();
        ctx.Ellipse(cx, y + ry, rx, ry, 0f, 0f, MathF.PI * 2f);
        ctx.Fill();

        // Top cap stroke
        ctx.StrokeStyle = shadowColor;
        ctx.LineWidth = 1.5f;
        ctx.Stroke();

        ctx.Restore();
    }

    public Dictionary<string, object?> CreateThreePointLighting(object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var keyColor = opt?["keyColor"]?.ToString() ?? "#fff3d6";
        var fillColor = opt?["fillColor"]?.ToString() ?? "#8cb5db";
        var rimColor = opt?["rimColor"]?.ToString() ?? "#ffffff";

        var keyAngleDeg = opt != null && opt.Contains("keyAngleDeg") ? Convert.ToSingle(opt["keyAngleDeg"], CultureInfo.InvariantCulture) : -45f;
        var fillAngleDeg = opt != null && opt.Contains("fillAngleDeg") ? Convert.ToSingle(opt["fillAngleDeg"], CultureInfo.InvariantCulture) : 60f;
        var rimAngleDeg = opt != null && opt.Contains("rimAngleDeg") ? Convert.ToSingle(opt["rimAngleDeg"], CultureInfo.InvariantCulture) : 135f;

        return new Dictionary<string, object?>
        {
            ["keyLight"] = new Dictionary<string, object?> { ["angleDeg"] = keyAngleDeg, ["color"] = keyColor, ["intensity"] = 0.75f },
            ["fillLight"] = new Dictionary<string, object?> { ["angleDeg"] = fillAngleDeg, ["color"] = fillColor, ["intensity"] = 0.30f },
            ["rimLight"] = new Dictionary<string, object?> { ["angleDeg"] = rimAngleDeg, ["color"] = rimColor, ["intensity"] = 0.90f }
        };
    }

    /// <summary>
    /// Renders grazing edge light along a silhouette, culled and faded by the light's angle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="lightAngleDeg"/> points <em>from</em> the form <em>toward</em> the light, the
    /// same convention as <c>lightDirection</c> elsewhere in this toolkit. Each stretch of contour is
    /// weighted by <c>max(0, n · L)^spread</c> with <c>n</c> its outward normal, so only the arc
    /// facing the light is drawn and it fades where the form turns away. That is what makes it read
    /// as light rather than as ink: the call used to stroke the entire point list at full opacity
    /// whatever the angle was, which is an outline, and a live run had to hand-trim its point lists
    /// down to the arc that should have caught light.
    /// </para>
    /// <para>
    /// The band sits <em>inside</em> the contour, offset inward by half the thickness, because a rim
    /// is light on the form rather than a wire beside it. The previous version pushed it two pixels
    /// <em>outward</em> along the light vector — the "pale wire floating clear of the figure" the same
    /// run reported. A point list that merely approximates the silhouette will still float: the list
    /// has to <em>be</em> the silhouette, and nothing here can know the form well enough to correct it.
    /// </para>
    /// <para>
    /// <paramref name="options"/> takes <c>{ spread }</c>, the exponent on the cosine — higher is a
    /// tighter rim, 1 is a broad Lambertian falloff over the whole lit half.
    /// </para>
    /// </remarks>
    public void DrawRimLight(CanvasRenderingContext2D ctx, object boundsOrPts, float lightAngleDeg, object? rimColor = null, float thickness = 2.5f, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var opt = JsInterop.AsDict(options);
        var spread = opt != null && opt.Contains("spread")
            ? MathF.Max(0.1f, Convert.ToSingle(opt["spread"], CultureInfo.InvariantCulture))
            : 2f;
        var color = rimColor?.ToString() ?? "#ffffff";

        var contour = RimContour(boundsOrPts);
        if (contour.Count < 2) return;

        var rad = (lightAngleDeg * MathF.PI) / 180f;
        var lightX = MathF.Cos(rad);
        var lightY = MathF.Sin(rad);

        // "Outward" is away from the middle of the contour, which is what a silhouette gives us.
        var cx = 0f;
        var cy = 0f;
        foreach (var p in contour)
        {
            cx += p.X;
            cy += p.Y;
        }
        cx /= contour.Count;
        cy /= contour.Count;

        var weights = new float[contour.Count];
        var offsets = new Point2D[contour.Count];
        var inset = thickness * 0.5f;

        for (var i = 0; i < contour.Count; i++)
        {
            var before = contour[Math.Max(0, i - 1)];
            var after = contour[Math.Min(contour.Count - 1, i + 1)];
            var dx = after.X - before.X;
            var dy = after.Y - before.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f)
            {
                weights[i] = 0f;
                offsets[i] = contour[i];
                continue;
            }

            var nx = dy / len;
            var ny = -dx / len;
            if (nx * (contour[i].X - cx) + ny * (contour[i].Y - cy) < 0f)
            {
                nx = -nx;
                ny = -ny;
            }

            var facing = nx * lightX + ny * lightY;
            weights[i] = facing <= 0f ? 0f : MathF.Pow(facing, spread);
            offsets[i] = new Point2D(contour[i].X - nx * inset, contour[i].Y - ny * inset);
        }

        var baseAlpha = ctx.GlobalAlpha;

        ctx.Save();
        ctx.StrokeStyle = color;
        ctx.LineWidth = thickness;
        // Butt caps, because runs abut: a round cap would overlap its neighbour and bead the seam.
        ctx.LineCap = "butt";
        ctx.LineJoin = "round";

        // Segments are grouped into runs of equal quantised weight and stroked as polylines. Stroking
        // each segment separately would be simpler and wrong — at any alpha below 1 the caps overlap
        // and the rim comes out beaded rather than continuous.
        const int levels = 14;
        var start = -1;
        var level = 0;

        for (var i = 0; i <= contour.Count - 1; i++)
        {
            var segmentLevel = i < contour.Count - 1
                ? (int)MathF.Round((weights[i] + weights[i + 1]) * 0.5f * levels)
                : 0;

            if (start >= 0 && (segmentLevel != level || i == contour.Count - 1))
            {
                StrokeRun(ctx, offsets, start, i, baseAlpha * level / levels);
                start = -1;
            }
            if (i < contour.Count - 1 && segmentLevel > 0 && start < 0)
            {
                start = i;
                level = segmentLevel;
            }
        }

        ctx.Restore();
    }

    /// <summary>Strokes <c>offsets[from..to]</c> as one polyline at <paramref name="alpha"/>.</summary>
    private static void StrokeRun(CanvasRenderingContext2D ctx, Point2D[] offsets, int from, int to, float alpha)
    {
        if (to <= from || alpha <= 0f) return;

        ctx.GlobalAlpha = MathF.Min(1f, alpha);
        ctx.BeginPath();
        ctx.MoveTo(offsets[from].X, offsets[from].Y);
        for (var i = from + 1; i <= to; i++) ctx.LineTo(offsets[i].X, offsets[i].Y);
        ctx.Stroke();
    }

    /// <summary>The contour to light: a rect's perimeter, or the point list as given.</summary>
    /// <remarks>
    /// A rect is closed, so it gets its fourth edge back and all four can catch light — the previous
    /// version drew a single straight line down either the left or the right edge and could not rim
    /// the top at all. A point list is left open: a silhouette arc closed with a chord would light
    /// the chord.
    /// </remarks>
    private static List<Point2D> RimContour(object boundsOrPts)
    {
        var contour = new List<Point2D>();

        if (JsInterop.AsDict(boundsOrPts) is IDictionary b && b.Contains("x") && b.Contains("width"))
        {
            var bx = Convert.ToSingle(b["x"], CultureInfo.InvariantCulture);
            var by = Convert.ToSingle(b["y"], CultureInfo.InvariantCulture);
            var bw = Convert.ToSingle(b["width"], CultureInfo.InvariantCulture);
            var bh = Convert.ToSingle(b["height"], CultureInfo.InvariantCulture);

            // Sampled along each edge so the falloff can vary across it, not just between edges.
            const int perEdge = 8;
            Point2D[] corners =
            [
                new(bx, by), new(bx + bw, by), new(bx + bw, by + bh), new(bx, by + bh), new(bx, by)
            ];
            for (var e = 0; e < 4; e++)
            {
                for (var s = 0; s < perEdge; s++)
                {
                    var t = s / (float)perEdge;
                    contour.Add(new Point2D(
                        corners[e].X + (corners[e + 1].X - corners[e].X) * t,
                        corners[e].Y + (corners[e + 1].Y - corners[e].Y) * t));
                }
            }
            contour.Add(corners[0]);
        }
        else if (boundsOrPts is IList pts)
        {
            foreach (var p in pts) contour.Add(ExtractPoint(p));
        }

        return contour;
    }

    public SKShader CreateVolumetricSphereShader(object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var lightColor = opt?["lightColor"]?.ToString() ?? "#fff6e8";
        var baseColor = opt?["baseColor"]?.ToString() ?? "#c4a382";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#3a281e";

        var lc = SkiaColorParser.Parse(lightColor);
        var bc = SkiaColorParser.Parse(baseColor);
        var sc = SkiaColorParser.Parse(shadowColor);

        const string sksl = @"
            uniform float2 u_center;
            uniform float u_radius;
            uniform float3 u_lightDir;
            uniform float4 u_lightColor;
            uniform float4 u_baseColor;
            uniform float4 u_shadowColor;

            half4 main(float2 coord) {
                float2 d = (coord - u_center) / u_radius;
                float distSq = dot(d, d);
                if (distSq > 1.0) {
                    return half4(0.0);
                }
                float nz = sqrt(1.0 - distSq);
                float3 normal = float3(d.x, d.y, nz);

                float diff = max(0.0, dot(normal, normalize(u_lightDir)));
                float4 litColor = mix(u_shadowColor, u_baseColor, diff);

                // Specular glint
                float3 halfVec = normalize(u_lightDir + float3(0.0, 0.0, 1.0));
                float spec = pow(max(0.0, dot(normal, halfVec)), 24.0);
                litColor += u_lightColor * spec * 0.8;

                return half4(litColor);
            }
        ";

        var uniforms = new Dictionary<string, object>
        {
            ["u_center"] = new[] { 400f, 300f },
            ["u_radius"] = 150f,
            ["u_lightDir"] = new[] { 0.577f, -0.577f, 0.577f },
            ["u_lightColor"] = new[] { lc.Red / 255f, lc.Green / 255f, lc.Blue / 255f, 1f },
            ["u_baseColor"] = new[] { bc.Red / 255f, bc.Green / 255f, bc.Blue / 255f, 1f },
            ["u_shadowColor"] = new[] { sc.Red / 255f, sc.Green / 255f, sc.Blue / 255f, 1f }
        };

        var skiaShaderApi = new SkiaShaderApi();
        return skiaShaderApi.Sksl(sksl, uniforms);
    }
    #endregion

    #region Full-Body Anatomy, Mannequins & Expressions
    #region Posing
    /// <summary>Length of a limb segment, so a pose keeps the proportions the canon gave it.</summary>
    static float SegmentLength(Point2D from, Point2D to) =>
        MathF.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));

    /// <summary>Screen angle of a segment in degrees, measured the way <see cref="AlongSegment"/> reads them.</summary>
    static float SegmentAngle(Point2D from, Point2D to) =>
        MathF.Atan2(to.Y - from.Y, to.X - from.X) * 180f / MathF.PI;

    /// <summary>The point <paramref name="length"/> away from <paramref name="from"/> at a screen angle.</summary>
    /// <remarks>
    /// Screen degrees, so <b>90 is straight down</b> — the direction gravity and a hanging arm both
    /// go. Chosen over the mathematical convention because every default in this toolkit is a
    /// standing figure, and an author reasoning about a pose is reasoning about the drawing.
    /// </remarks>
    static Point2D AlongSegment(Point2D from, float length, float degrees)
    {
        var r = degrees * MathF.PI / 180f;
        return new Point2D(from.X + MathF.Cos(r) * length, from.Y + MathF.Sin(r) * length);
    }

    /// <summary>Rotates a point about a pivot, for leaning the upper body over the pelvis.</summary>
    static Point2D RotateAbout(Point2D point, Point2D pivot, float degrees)
    {
        if (degrees == 0f) return point;
        var r = degrees * MathF.PI / 180f;
        float cos = MathF.Cos(r), sin = MathF.Sin(r), dx = point.X - pivot.X, dy = point.Y - pivot.Y;
        return new Point2D(pivot.X + dx * cos - dy * sin, pivot.Y + dx * sin + dy * cos);
    }

    static float? PoseAngle(IDictionary? pose, string key) =>
        pose != null && pose.Contains(key) && pose[key] != null
            ? Convert.ToSingle(pose[key], CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// Re-places one three-segment limb from joint angles, keeping the segment lengths the
    /// proportional construction produced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Forward kinematics, and deliberately the simplest kind: the <paramref name="rootKey"/> angle is
    /// <b>absolute</b> on screen, the <paramref name="bendKey"/> angle is <b>relative</b> to the segment
    /// above it — which is what a joint actually is. An elbow does not have an opinion about the
    /// horizon; it has an opinion about the upper arm.
    /// </para>
    /// <para>
    /// <b>Every omitted angle falls back to the default figure's own value</b>, recovered from the
    /// points the canon already computed. So a limb with no pose is bit-identical to the unposed
    /// figure, and a limb given only <c>elbowDeg</c> keeps the standing shoulder. That is what makes
    /// this additive rather than a second construction to keep in step with the first.
    /// </para>
    /// <para>
    /// The hand and foot keep their own offset from the segment before them, so they swing with the
    /// forearm and shin rather than staying pinned downward.
    /// </para>
    /// </remarks>
    static (Point2D Mid, Point2D End, Point2D Tip) PoseLimb(
        IDictionary? pose, string rootKey, string bendKey,
        Point2D anchor, Point2D defaultMid, Point2D defaultEnd, Point2D defaultTip)
    {
        if (pose == null) return (defaultMid, defaultEnd, defaultTip);

        var upper = SegmentAngle(anchor, defaultMid);
        var lower = SegmentAngle(defaultMid, defaultEnd);
        var tipOffset = SegmentAngle(defaultEnd, defaultTip) - lower;

        var rootDeg = PoseAngle(pose, rootKey) ?? upper;
        var bendDeg = PoseAngle(pose, bendKey) ?? lower - upper;

        var mid = AlongSegment(anchor, SegmentLength(anchor, defaultMid), rootDeg);
        var end = AlongSegment(mid, SegmentLength(defaultMid, defaultEnd), rootDeg + bendDeg);
        var tip = AlongSegment(end, SegmentLength(defaultEnd, defaultTip), rootDeg + bendDeg + tipOffset);
        return (mid, end, tip);
    }
    #endregion

    public Dictionary<string, object?> CreateMannequinFigure(float originX, float originY, float totalHeight = 560f, object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var H = totalHeight / 8f;
        var shoulderTiltDeg = opt != null && opt.Contains("shoulderTiltDeg") ? Convert.ToSingle(opt["shoulderTiltDeg"], CultureInfo.InvariantCulture) : -6f;
        var pelvicTiltDeg = opt != null && opt.Contains("pelvicTiltDeg") ? Convert.ToSingle(opt["pelvicTiltDeg"], CultureInfo.InvariantCulture) : 6f;
        var spineOffset = opt != null && opt.Contains("spineOffset") ? Convert.ToSingle(opt["spineOffset"], CultureInfo.InvariantCulture) : 6f;

        var radShoulder = (shoulderTiltDeg * MathF.PI) / 180f;
        var radPelvis = (pelvicTiltDeg * MathF.PI) / 180f;

        // Head (0.0H to 1.0H)
        var headY = originY + H * 0.5f;
        var headCenter = new Point2D(originX, headY);

        // Neck (1.0H to 1.3H)
        var neckCenter = new Point2D(originX + spineOffset * 0.2f, originY + H * 1.15f);

        // Shoulders / Clavicles (1.4H)
        var shoulderY = originY + H * 1.4f;
        // In head units, so it survives any figure height. The canons disagree and all of them are
        // usable: Loomis gives 2 1/3 H for the figure at its widest and about 2 H for the shoulder
        // "cape"; Faragasso, after Reilly, gives 1 1/3 H from the pit of the neck to each shoulder,
        // so 2 2/3 H across. The default is none of them — it is a shoulder-joint span, which is a
        // narrower measurement again — so an agent following a particular canon has to be able to
        // say so rather than being stuck with ours.
        var shoulderSpanHeads = opt != null && opt.Contains("shoulderSpanHeads")
            ? Convert.ToSingle(opt["shoulderSpanHeads"], CultureInfo.InvariantCulture)
            : 1.8f;
        var shoulderSpan = H * shoulderSpanHeads;
        var leftShoulder = new Point2D(originX - MathF.Cos(radShoulder) * (shoulderSpan * 0.5f), shoulderY - MathF.Sin(radShoulder) * (shoulderSpan * 0.5f));
        var rightShoulder = new Point2D(originX + MathF.Cos(radShoulder) * (shoulderSpan * 0.5f), shoulderY + MathF.Sin(radShoulder) * (shoulderSpan * 0.5f));
        var sternalNotch = new Point2D(originX + spineOffset * 0.3f, shoulderY);

        // Ribcage (1.4H to 2.8H)
        var ribcageCenter = new Point2D(originX + spineOffset * 0.6f, originY + H * 2.1f);
        var ribcageRx = H * 0.85f;
        var ribcageRy = H * 0.70f;

        // Navel (3.0H)
        var navel = new Point2D(originX + spineOffset * 0.8f, originY + H * 3.0f);

        // Pelvis (3.2H to 4.0H)
        var pelvisY = originY + H * 3.6f;
        var pelvisSpan = H * 1.4f;
        var leftHip = new Point2D(originX - MathF.Cos(radPelvis) * (pelvisSpan * 0.5f), pelvisY - MathF.Sin(radPelvis) * (pelvisSpan * 0.5f));
        var rightHip = new Point2D(originX + MathF.Cos(radPelvis) * (pelvisSpan * 0.5f), pelvisY + MathF.Sin(radPelvis) * (pelvisSpan * 0.5f));
        var pelvisCenter = new Point2D(originX + spineOffset * 0.4f, pelvisY);
        var crotch = new Point2D(originX, originY + H * 4.0f);

        // Left Arm (Shoulder -> Elbow 2.8H -> Wrist 4.0H -> Hand 4.8H)
        var leftElbow = new Point2D(leftShoulder.X - H * 0.3f, originY + H * 2.8f);
        var leftWrist = new Point2D(leftShoulder.X - H * 0.2f, originY + H * 4.0f);
        var leftHand = new Point2D(leftWrist.X - 2f, leftWrist.Y + H * 0.75f);

        // Right Arm
        var rightElbow = new Point2D(rightShoulder.X + H * 0.35f, originY + H * 2.85f);
        var rightWrist = new Point2D(rightShoulder.X + H * 0.25f, originY + H * 4.0f);
        var rightHand = new Point2D(rightWrist.X + 2f, rightWrist.Y + H * 0.75f);

        // Left Leg (Hip -> Knee 6.0H -> Ankle 7.8H -> Foot 8.0H)
        var leftKnee = new Point2D(leftHip.X + H * 0.05f, originY + H * 6.0f);
        var leftAnkle = new Point2D(leftKnee.X - H * 0.05f, originY + H * 7.8f);
        var leftFoot = new Point2D(leftAnkle.X - H * 0.15f, originY + H * 8.0f);

        // Right Leg
        var rightKnee = new Point2D(rightHip.X - H * 0.05f, originY + H * 6.0f);
        var rightAnkle = new Point2D(rightKnee.X + H * 0.05f, originY + H * 7.8f);
        var rightFoot = new Point2D(rightAnkle.X + H * 0.15f, originY + H * 8.0f);

        // A pose re-places the limbs from joint angles. Applied *after* the canon has laid the figure
        // out, so the segment lengths are the canon's and only the directions change — and a limb the
        // pose does not mention is left exactly where the standing figure put it.
        var pose = JsInterop.AsDict(opt != null && opt.Contains("pose") ? opt["pose"] : null);
        var leftArmPose = JsInterop.AsDict(pose != null && pose.Contains("leftArm") ? pose["leftArm"] : null);
        var rightArmPose = JsInterop.AsDict(pose != null && pose.Contains("rightArm") ? pose["rightArm"] : null);
        var leftLegPose = JsInterop.AsDict(pose != null && pose.Contains("leftLeg") ? pose["leftLeg"] : null);
        var rightLegPose = JsInterop.AsDict(pose != null && pose.Contains("rightLeg") ? pose["rightLeg"] : null);

        // The spine leans the whole upper body over the pelvis — ribcage, shoulders, neck and head,
        // and the arms with them, because an arm hangs from a shoulder that has moved. Applied before
        // the limbs so an arm the pose does not mention still swings with the lean instead of being
        // left behind in the standing position.
        //
        // Positive leans the figure's own left (screen right, toward increasing x at the shoulders).
        // A separate `neckDeg` counter-rotates the head, which is what keeps a leaning figure looking
        // where it was looking rather than tipping its gaze with its chest.
        var spineDeg = PoseAngle(pose, "spineDeg") ?? 0f;
        var neckDeg = PoseAngle(pose, "neckDeg") ?? 0f;
        if (spineDeg != 0f || neckDeg != 0f)
        {
            var pivot = pelvisCenter;
            headCenter = RotateAbout(headCenter, pivot, spineDeg);
            neckCenter = RotateAbout(neckCenter, pivot, spineDeg);
            sternalNotch = RotateAbout(sternalNotch, pivot, spineDeg);
            ribcageCenter = RotateAbout(ribcageCenter, pivot, spineDeg);
            navel = RotateAbout(navel, pivot, spineDeg);
            leftShoulder = RotateAbout(leftShoulder, pivot, spineDeg);
            rightShoulder = RotateAbout(rightShoulder, pivot, spineDeg);
            leftElbow = RotateAbout(leftElbow, pivot, spineDeg);
            leftWrist = RotateAbout(leftWrist, pivot, spineDeg);
            leftHand = RotateAbout(leftHand, pivot, spineDeg);
            rightElbow = RotateAbout(rightElbow, pivot, spineDeg);
            rightWrist = RotateAbout(rightWrist, pivot, spineDeg);
            rightHand = RotateAbout(rightHand, pivot, spineDeg);

            // The head turns about the neck, on top of whatever the spine did.
            headCenter = RotateAbout(headCenter, neckCenter, neckDeg);
        }

        (leftElbow, leftWrist, leftHand) = PoseLimb(leftArmPose, "shoulderDeg", "elbowDeg",
            leftShoulder, leftElbow, leftWrist, leftHand);
        (rightElbow, rightWrist, rightHand) = PoseLimb(rightArmPose, "shoulderDeg", "elbowDeg",
            rightShoulder, rightElbow, rightWrist, rightHand);
        (leftKnee, leftAnkle, leftFoot) = PoseLimb(leftLegPose, "hipDeg", "kneeDeg",
            leftHip, leftKnee, leftAnkle, leftFoot);
        (rightKnee, rightAnkle, rightFoot) = PoseLimb(rightLegPose, "hipDeg", "kneeDeg",
            rightHip, rightKnee, rightAnkle, rightFoot);

        // The lean is an orientation, not just a displacement. The masses used to carry only their
        // static tilt while `spineDeg` moved their centres, so a figure leaning 18 degrees kept a
        // perfectly upright head and an untilted ribcage. The pelvis is the pivot and so is unmoved.
        var ribcageTiltDeg = shoulderTiltDeg + spineDeg;
        var headAngleDeg = spineDeg + neckDeg;

        var figure = new Dictionary<string, object?>
        {
            ["headUnit"] = H,
            ["totalHeight"] = totalHeight,
            // Echoed so a caller can read back what the figure is doing — and so a later pass can
            // reproduce or nudge a pose without having kept the arguments that made it.
            ["posed"] = pose != null,
            ["head"] = new Dictionary<string, object?> { ["center"] = ToDict(headCenter), ["rx"] = H * 0.36f, ["ry"] = H * 0.50f, ["angleDeg"] = headAngleDeg },
            ["neck"] = ToDict(neckCenter),
            ["sternum"] = ToDict(sternalNotch),
            ["clavicles"] = new Dictionary<string, object?> { ["left"] = ToDict(leftShoulder), ["right"] = ToDict(rightShoulder), ["center"] = ToDict(sternalNotch) },
            ["ribcage"] = new Dictionary<string, object?> { ["center"] = ToDict(ribcageCenter), ["rx"] = ribcageRx, ["ry"] = ribcageRy, ["tiltDeg"] = ribcageTiltDeg },
            ["navel"] = ToDict(navel),
            ["pelvis"] = new Dictionary<string, object?> { ["center"] = ToDict(pelvisCenter), ["leftHip"] = ToDict(leftHip), ["rightHip"] = ToDict(rightHip), ["rx"] = H * 0.70f, ["ry"] = H * 0.45f, ["tiltDeg"] = pelvicTiltDeg },
            ["crotch"] = ToDict(crotch),
            ["leftArm"] = new Dictionary<string, object?> { ["shoulder"] = ToDict(leftShoulder), ["elbow"] = ToDict(leftElbow), ["wrist"] = ToDict(leftWrist), ["hand"] = ToDict(leftHand) },
            ["rightArm"] = new Dictionary<string, object?> { ["shoulder"] = ToDict(rightShoulder), ["elbow"] = ToDict(rightElbow), ["wrist"] = ToDict(rightWrist), ["hand"] = ToDict(rightHand) },
            ["leftLeg"] = new Dictionary<string, object?> { ["hip"] = ToDict(leftHip), ["knee"] = ToDict(leftKnee), ["ankle"] = ToDict(leftAnkle), ["foot"] = ToDict(leftFoot) },
            ["rightLeg"] = new Dictionary<string, object?> { ["hip"] = ToDict(rightHip), ["knee"] = ToDict(rightKnee), ["ankle"] = ToDict(rightAnkle), ["foot"] = ToDict(rightFoot) }
        };

        // Cheap enough to be unconditional — closed-form over the same masses the geometry uses, no
        // paths and no boolean operations. A posed figure's extent is *not* its height: a thrown arm
        // reaches wider than the canon ever does, so fitting one to a panel by height alone runs a
        // limb straight off the edge.
        figure["bounds"] = FigureBounds(figure);
        return figure;
    }

    #region Figure Geometry
    /// <summary>A bone as a drawable mass: a tapering band with a disc at each joint.</summary>
    /// <remarks>
    /// Discs rather than hand-swept arc caps. An arc cap has to choose which half of the circle it
    /// sweeps, and choosing wrong subtracts a bite from the join instead of adding one — silently,
    /// as a white disc at every joint. A union of a band and two circles cannot get that wrong.
    /// </remarks>
    static CanvasPath Capsule(Point2D a, Point2D b, float ra, float rb)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return Disc(a, MathF.Max(ra, rb));

        float nx = -dy / len, ny = dx / len;
        var band = new CanvasPath();
        band.MoveTo(a.X + nx * ra, a.Y + ny * ra);
        band.LineTo(b.X + nx * rb, b.Y + ny * rb);
        band.LineTo(b.X - nx * rb, b.Y - ny * rb);
        band.LineTo(a.X - nx * ra, a.Y - ny * ra);
        band.ClosePath();
        return band.Union(Disc(a, ra)).Union(Disc(b, rb));
    }

    static CanvasPath Disc(Point2D c, float r)
    {
        var p = new CanvasPath();
        p.Arc(c.X, c.Y, MathF.Max(0.01f, r), 0f, MathF.PI * 2f);
        p.ClosePath();
        return p;
    }

    static CanvasPath OrientedEllipse(Point2D c, float rx, float ry, float degrees)
    {
        var p = new CanvasPath();
        p.Ellipse(c.X, c.Y, MathF.Max(0.01f, rx), MathF.Max(0.01f, ry), degrees * MathF.PI / 180f, 0f, MathF.PI * 2f);
        return p;
    }

    static float Num(IDictionary? d, string key, float fallback) =>
        d != null && d.Contains(key) && d[key] != null
            ? Convert.ToSingle(d[key], CultureInfo.InvariantCulture)
            : fallback;

    /// <summary>
    /// Every tapering mass in the figure, named, with the radii <see cref="DrawMannequinSolid"/>
    /// already paints. One table so bounds and geometry can never disagree about the same figure.
    /// </summary>
    static List<(string Name, string Group, Point2D A, Point2D B, float RA, float RB)> FigureBones(IDictionary fig, float H)
    {
        var lArm = JsInterop.AsDict(fig["leftArm"]);
        var rArm = JsInterop.AsDict(fig["rightArm"]);
        var lLeg = JsInterop.AsDict(fig["leftLeg"]);
        var rLeg = JsInterop.AsDict(fig["rightLeg"]);
        var clav = JsInterop.AsDict(fig["clavicles"]);
        var pelvis = JsInterop.AsDict(fig["pelvis"]);

        var neck = ExtractPoint(fig["neck"]);
        var sternum = ExtractPoint(fig["sternum"]);
        var pelCenter = ExtractPoint(pelvis?["center"]);

        var bones = new List<(string, string, Point2D, Point2D, float, float)>
        {
            ("neck", "torso", neck, sternum, H * 0.18f, H * 0.34f),
            ("spine", "torso", sternum, pelCenter, H * 0.55f, H * 0.58f),
            ("shoulders", "torso", ExtractPoint(clav?["left"]), ExtractPoint(clav?["right"]), H * 0.29f, H * 0.29f)
        };

        void Limb(IDictionary? d, string side, string group, string j0, string j1, string j2, string j3,
                  string n0, string n1, string n2, float r0, float r1, float r2, float r3)
        {
            if (d == null) return;
            Point2D p0 = ExtractPoint(d[j0]), p1 = ExtractPoint(d[j1]), p2 = ExtractPoint(d[j2]), p3 = ExtractPoint(d[j3]);
            bones.Add((side + n0, group, p0, p1, r0, r1));
            bones.Add((side + n1, group, p1, p2, r1, r2));
            bones.Add((side + n2, group, p2, p3, r2, r3));
        }

        Limb(lArm, "left", "leftArm", "shoulder", "elbow", "wrist", "hand",
            "UpperArm", "Forearm", "Hand", H * 0.22f, H * 0.16f, H * 0.12f, H * 0.10f);
        Limb(rArm, "right", "rightArm", "shoulder", "elbow", "wrist", "hand",
            "UpperArm", "Forearm", "Hand", H * 0.22f, H * 0.16f, H * 0.12f, H * 0.10f);
        Limb(lLeg, "left", "leftLeg", "hip", "knee", "ankle", "foot",
            "Thigh", "Shin", "Foot", H * 0.28f, H * 0.20f, H * 0.14f, H * 0.10f);
        Limb(rLeg, "right", "rightLeg", "hip", "knee", "ankle", "foot",
            "Thigh", "Shin", "Foot", H * 0.28f, H * 0.20f, H * 0.14f, H * 0.10f);

        return bones;
    }

    /// <summary>The three elliptical masses, each with the orientation the pose gave it.</summary>
    static List<(string Name, string Group, Point2D C, float Rx, float Ry, float Deg)> FigureMasses(IDictionary fig, float H)
    {
        var head = JsInterop.AsDict(fig["head"]);
        var rib = JsInterop.AsDict(fig["ribcage"]);
        var pel = JsInterop.AsDict(fig["pelvis"]);

        return
        [
            ("head", "head", ExtractPoint(head?["center"]), Num(head, "rx", H * 0.36f), Num(head, "ry", H * 0.50f), Num(head, "angleDeg", 0f)),
            ("ribcage", "torso", ExtractPoint(rib?["center"]), Num(rib, "rx", H * 0.85f), Num(rib, "ry", H * 0.70f), Num(rib, "tiltDeg", 0f)),
            ("pelvis", "torso", ExtractPoint(pel?["center"]), Num(pel, "rx", H * 0.70f), Num(pel, "ry", H * 0.45f), Num(pel, "tiltDeg", 0f))
        ];
    }

    /// <summary>Closed-form extent of every mass, without building a single path.</summary>
    static Dictionary<string, object?> FigureBounds(IDictionary fig, float padding = 0f)
    {
        var H = Num(fig as IDictionary, "headUnit", 70f);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;

        void Add(float cx, float cy, float hw, float hh)
        {
            x0 = MathF.Min(x0, cx - hw); y0 = MathF.Min(y0, cy - hh);
            x1 = MathF.Max(x1, cx + hw); y1 = MathF.Max(y1, cy + hh);
        }

        foreach (var b in FigureBones(fig, H))
        {
            Add(b.A.X, b.A.Y, b.RA + padding, b.RA + padding);
            Add(b.B.X, b.B.Y, b.RB + padding, b.RB + padding);
        }
        foreach (var m in FigureMasses(fig, H))
        {
            // Exact half-extents of an ellipse rotated by theta.
            var t = m.Deg * MathF.PI / 180f;
            float c = MathF.Cos(t), s = MathF.Sin(t), rx = m.Rx + padding, ry = m.Ry + padding;
            Add(m.C.X, m.C.Y, MathF.Sqrt(rx * rx * c * c + ry * ry * s * s), MathF.Sqrt(rx * rx * s * s + ry * ry * c * c));
        }

        if (x0 > x1) return new Dictionary<string, object?>
        {
            ["x"] = 0f, ["y"] = 0f, ["width"] = 0f, ["height"] = 0f,
            ["x2"] = 0f, ["y2"] = 0f, ["cx"] = 0f, ["cy"] = 0f
        };

        return new Dictionary<string, object?>
        {
            ["x"] = x0, ["y"] = y0, ["width"] = x1 - x0, ["height"] = y1 - y0,
            ["x2"] = x1, ["y2"] = y1, ["cx"] = (x0 + x1) * 0.5f, ["cy"] = (y0 + y1) * 0.5f
        };
    }

    /// <summary>
    /// The figure as geometry rather than as a drawing: one silhouette, a path per mass, the coarse
    /// groups those masses belong to, and the bounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>drawMannequinSolid</c> builds these same masses and hands nothing back, so every caller
    /// that wanted to clip to a limb, occlude a figure, cut a garment, or fit a figure to a panel
    /// had to rebuild them by hand. A path composes — it fills, clips, strokes, takes boolean
    /// operations, converts through <c>strokeToPath</c>, and survives into <c>outSvg</c>; a draw
    /// call composes with nothing.
    /// </para>
    /// <para>
    /// Separate from <see cref="CreateMannequinFigure"/> because paths are not free: this builds
    /// about twenty native paths and some fifty boolean operations, which a loop measuring poses
    /// should not pay for. <c>figure.bounds</c> lives on the figure itself and costs nothing.
    /// </para>
    /// <para>
    /// <c>padding</c> inflates every mass, which is how a garment is derived: cloth covers the
    /// figure without fitting it, and that gap is where every fold comes from (Studio Manual 22).
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateFigureGeometry(object figureObj, object? options = null)
    {
        if (JsInterop.AsDict(figureObj) is not IDictionary fig)
            throw new ArgumentException("createFigureGeometry needs a figure from Drawing.createMannequinFigure(...).", nameof(figureObj));

        var opt = JsInterop.AsDict(options);
        var padding = Num(opt, "padding", 0f);
        var H = Num(fig, "headUnit", 70f);

        var parts = new Dictionary<string, object?>();
        var groups = new Dictionary<string, CanvasPath>();

        void Record(string name, string group, CanvasPath path)
        {
            parts[name] = path;
            groups[group] = groups.TryGetValue(group, out var acc) ? acc.Union(path) : path;
        }

        foreach (var b in FigureBones(fig, H))
            Record(b.Name, b.Group, Capsule(b.A, b.B, b.RA + padding, b.RB + padding));
        foreach (var m in FigureMasses(fig, H))
            Record(m.Name, m.Group, OrientedEllipse(m.C, m.Rx + padding, m.Ry + padding, m.Deg));

        CanvasPath? whole = null;
        var groupDict = new Dictionary<string, object?>();
        foreach (var kv in groups)
        {
            groupDict[kv.Key] = kv.Value;
            whole = whole == null ? kv.Value : whole.Union(kv.Value);
        }

        return new Dictionary<string, object?>
        {
            ["silhouette"] = whole == null ? new CanvasPath() : whole.Simplify(),
            ["parts"] = parts,
            ["groups"] = groupDict,
            ["bounds"] = FigureBounds(fig, padding),
            ["padding"] = padding,
            // Construction order, NOT depth — the toolkit has no z, so this is the order
            // `drawMannequinSolid` paints in and nothing more. A pose where an arm passes behind the
            // torso still needs the caller to say so.
            ["order"] = new List<object?> { "leftLeg", "rightLeg", "torso", "leftArm", "rightArm", "head" }
        };
    }
    #endregion

    public void DrawMannequinWireframe(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var blueLine = opt?["blueLineColor"]?.ToString() ?? "#4a90e2";
        var graphite = opt?["graphiteColor"]?.ToString() ?? "#444444";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.5f;

        var head = JsInterop.AsDict(fig["head"]);
        var headCenter = ExtractPoint(head?["center"]);
        var headRx = head != null && head.Contains("rx") ? Convert.ToSingle(head["rx"], CultureInfo.InvariantCulture) : 25f;
        // The pose orients the masses as well as placing them; without this a leaning figure keeps
        // a perfectly upright head, which is what it did before these three angles were read.
        var headAngle = Num(head, "angleDeg", 0f) * MathF.PI / 180f;
        var headRy = head != null && head.Contains("ry") ? Convert.ToSingle(head["ry"], CultureInfo.InvariantCulture) : 35f;

        var neck = ExtractPoint(fig["neck"]);
        var sternum = ExtractPoint(fig["sternum"]);
        var navel = ExtractPoint(fig["navel"]);
        var crotch = ExtractPoint(fig["crotch"]);

        var ribcage = JsInterop.AsDict(fig["ribcage"]);
        var ribCenter = ExtractPoint(ribcage?["center"]);
        var ribRx = ribcage != null && ribcage.Contains("rx") ? Convert.ToSingle(ribcage["rx"], CultureInfo.InvariantCulture) : 60f;
        var ribRy = ribcage != null && ribcage.Contains("ry") ? Convert.ToSingle(ribcage["ry"], CultureInfo.InvariantCulture) : 50f;
        var ribTilt = Num(ribcage, "tiltDeg", 0f) * MathF.PI / 180f;

        var pelvis = JsInterop.AsDict(fig["pelvis"]);
        var pelCenter = ExtractPoint(pelvis?["center"]);
        var pelRx = pelvis != null && pelvis.Contains("rx") ? Convert.ToSingle(pelvis["rx"], CultureInfo.InvariantCulture) : 50f;
        var pelRy = pelvis != null && pelvis.Contains("ry") ? Convert.ToSingle(pelvis["ry"], CultureInfo.InvariantCulture) : 32f;
        var pelTilt = Num(pelvis, "tiltDeg", 0f) * MathF.PI / 180f;

        var lArm = JsInterop.AsDict(fig["leftArm"]);
        var rArm = JsInterop.AsDict(fig["rightArm"]);
        var lLeg = JsInterop.AsDict(fig["leftLeg"]);
        var rLeg = JsInterop.AsDict(fig["rightLeg"]);

        ctx.Save();

        // 1. Blue-line Skeleton Gesture & Masses
        ctx.StrokeStyle = blueLine;
        ctx.LineWidth = lineWidth;

        // Head Ellipse
        ctx.BeginPath();
        ctx.Ellipse(headCenter.X, headCenter.Y, headRx, headRy, headAngle, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // Spine Line of Action (Cervical -> Thoracic -> Lumbar -> Sacral)
        ctx.BeginPath();
        ctx.MoveTo(headCenter.X, headCenter.Y + headRy);
        ctx.LineTo(neck.X, neck.Y);
        ctx.QuadraticCurveTo(sternum.X, sternum.Y, ribCenter.X, ribCenter.Y);
        ctx.QuadraticCurveTo(navel.X, navel.Y, pelCenter.X, pelCenter.Y);
        ctx.LineTo(crotch.X, crotch.Y);
        ctx.Stroke();

        // Ribcage Egg
        ctx.BeginPath();
        ctx.Ellipse(ribCenter.X, ribCenter.Y, ribRx, ribRy, ribTilt, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // Pelvic Basin
        ctx.BeginPath();
        ctx.Ellipse(pelCenter.X, pelCenter.Y, pelRx, pelRy, pelTilt, 0f, MathF.PI * 2f);
        ctx.Stroke();

        // 2. Graphite Limb Bones & Joint Hinges
        ctx.StrokeStyle = graphite;
        ctx.LineWidth = lineWidth * 1.25f;

        void DrawLimb(Point2D a, Point2D b, Point2D c, Point2D d)
        {
            ctx.BeginPath();
            ctx.MoveTo(a.X, a.Y);
            ctx.LineTo(b.X, b.Y);
            ctx.LineTo(c.X, c.Y);
            ctx.LineTo(d.X, d.Y);
            ctx.Stroke();

            // Joint hinges
            ctx.BeginPath();
            ctx.Arc(b.X, b.Y, 5f, 0f, MathF.PI * 2f);
            ctx.Arc(c.X, c.Y, 4f, 0f, MathF.PI * 2f);
            ctx.Stroke();
        }

        if (lArm != null) DrawLimb(ExtractPoint(lArm["shoulder"]), ExtractPoint(lArm["elbow"]), ExtractPoint(lArm["wrist"]), ExtractPoint(lArm["hand"]));
        if (rArm != null) DrawLimb(ExtractPoint(rArm["shoulder"]), ExtractPoint(rArm["elbow"]), ExtractPoint(rArm["wrist"]), ExtractPoint(rArm["hand"]));
        if (lLeg != null) DrawLimb(ExtractPoint(lLeg["hip"]), ExtractPoint(lLeg["knee"]), ExtractPoint(lLeg["ankle"]), ExtractPoint(lLeg["foot"]));
        if (rLeg != null) DrawLimb(ExtractPoint(rLeg["hip"]), ExtractPoint(rLeg["knee"]), ExtractPoint(rLeg["ankle"]), ExtractPoint(rLeg["foot"]));

        ctx.Restore();
    }

    public void DrawMannequinSolid(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var fillColor = opt?["fillColor"]?.ToString() ?? "#dbe7f2";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#9bbcd9";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;

        var H = fig.Contains("headUnit") ? Convert.ToSingle(fig["headUnit"], CultureInfo.InvariantCulture) : 70f;

        ctx.Save();
        ctx.FillStyle = fillColor;
        ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = strokeWidth;

        // 1. Shaded Limbs (Tapered Cylinders)
        void DrawCylinderLimb(Point2D a, Point2D b, float r1, float r2)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 0.001f) dist = 1f;
            var nx = -dy / dist;
            var ny = dx / dist;

            ctx.BeginPath();
            ctx.MoveTo(a.X - nx * r1, a.Y - ny * r1);
            ctx.LineTo(b.X - nx * r2, b.Y - ny * r2);
            ctx.LineTo(b.X + nx * r2, b.Y + ny * r2);
            ctx.LineTo(a.X + nx * r1, a.Y + ny * r1);
            ctx.ClosePath();
            ctx.Fill();
            ctx.Stroke();
        }

        var lLeg = JsInterop.AsDict(fig["leftLeg"]);
        var rLeg = JsInterop.AsDict(fig["rightLeg"]);
        var lArm = JsInterop.AsDict(fig["leftArm"]);
        var rArm = JsInterop.AsDict(fig["rightArm"]);

        // Legs
        if (lLeg != null)
        {
            DrawCylinderLimb(ExtractPoint(lLeg["hip"]), ExtractPoint(lLeg["knee"]), H * 0.28f, H * 0.20f);
            DrawCylinderLimb(ExtractPoint(lLeg["knee"]), ExtractPoint(lLeg["ankle"]), H * 0.20f, H * 0.14f);
        }
        if (rLeg != null)
        {
            DrawCylinderLimb(ExtractPoint(rLeg["hip"]), ExtractPoint(rLeg["knee"]), H * 0.28f, H * 0.20f);
            DrawCylinderLimb(ExtractPoint(rLeg["knee"]), ExtractPoint(rLeg["ankle"]), H * 0.20f, H * 0.14f);
        }

        // Pelvis
        var pelvis = JsInterop.AsDict(fig["pelvis"]);
        var pelCenter = ExtractPoint(pelvis?["center"]);
        var pelRx = pelvis != null && pelvis.Contains("rx") ? Convert.ToSingle(pelvis["rx"], CultureInfo.InvariantCulture) : H * 0.70f;
        var pelRy = pelvis != null && pelvis.Contains("ry") ? Convert.ToSingle(pelvis["ry"], CultureInfo.InvariantCulture) : H * 0.45f;
        var pelTilt = Num(pelvis, "tiltDeg", 0f) * MathF.PI / 180f;
        ctx.BeginPath();
        ctx.Ellipse(pelCenter.X, pelCenter.Y, pelRx, pelRy, pelTilt, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        // Ribcage
        var ribcage = JsInterop.AsDict(fig["ribcage"]);
        var ribCenter = ExtractPoint(ribcage?["center"]);
        var ribRx = ribcage != null && ribcage.Contains("rx") ? Convert.ToSingle(ribcage["rx"], CultureInfo.InvariantCulture) : H * 0.85f;
        var ribRy = ribcage != null && ribcage.Contains("ry") ? Convert.ToSingle(ribcage["ry"], CultureInfo.InvariantCulture) : H * 0.70f;
        var ribTilt = Num(ribcage, "tiltDeg", 0f) * MathF.PI / 180f;
        ctx.BeginPath();
        ctx.Ellipse(ribCenter.X, ribCenter.Y, ribRx, ribRy, ribTilt, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        // Arms
        if (lArm != null)
        {
            DrawCylinderLimb(ExtractPoint(lArm["shoulder"]), ExtractPoint(lArm["elbow"]), H * 0.22f, H * 0.16f);
            DrawCylinderLimb(ExtractPoint(lArm["elbow"]), ExtractPoint(lArm["wrist"]), H * 0.16f, H * 0.12f);
        }
        if (rArm != null)
        {
            DrawCylinderLimb(ExtractPoint(rArm["shoulder"]), ExtractPoint(rArm["elbow"]), H * 0.22f, H * 0.16f);
            DrawCylinderLimb(ExtractPoint(rArm["elbow"]), ExtractPoint(rArm["wrist"]), H * 0.16f, H * 0.12f);
        }

        // Head
        var head = JsInterop.AsDict(fig["head"]);
        var headCenter = ExtractPoint(head?["center"]);
        var headRx = head != null && head.Contains("rx") ? Convert.ToSingle(head["rx"], CultureInfo.InvariantCulture) : H * 0.36f;
        var headAngle = Num(head, "angleDeg", 0f) * MathF.PI / 180f;
        var headRy = head != null && head.Contains("ry") ? Convert.ToSingle(head["ry"], CultureInfo.InvariantCulture) : H * 0.50f;
        ctx.BeginPath();
        ctx.Ellipse(headCenter.X, headCenter.Y, headRx, headRy, headAngle, 0f, MathF.PI * 2f);
        ctx.Fill();
        ctx.Stroke();

        ctx.Restore();
    }

    public void DrawTorsoMusculature(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#1a2938";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 2.0f;
        var H = fig.Contains("headUnit") ? Convert.ToSingle(fig["headUnit"], CultureInfo.InvariantCulture) : 70f;

        var sternum = ExtractPoint(fig["sternum"]);
        var navel = ExtractPoint(fig["navel"]);
        var neck = ExtractPoint(fig["neck"]);
        var clavicles = JsInterop.AsDict(fig["clavicles"]);
        var leftClav = ExtractPoint(clavicles?["left"]);
        var rightClav = ExtractPoint(clavicles?["right"]);
        var leftArm = JsInterop.AsDict(fig["leftArm"]);
        var rightArm = JsInterop.AsDict(fig["rightArm"]);

        // Hampton's active/passive rule (Figure Drawing: Design and Invention, "Anatomy and Motion"):
        // an active shape squashes, a passive one stretches, and drawing them symmetrically is what
        // kills the gesture. The pose already says which side is which - the shoulder line tilts down
        // toward the closed side - so the compression is read off the figure rather than asked for.
        var shoulderDrop = rightClav.Y - leftClav.Y;
        var tiltSpan = MathF.Max(H * 0.5f, MathF.Abs(rightClav.X - leftClav.X));
        var squash = MathF.Max(-0.35f, MathF.Min(0.35f, shoulderDrop / tiltSpan));
        var leftScale = 1f - squash;       // shoulder down on the right => right side compresses
        var rightScale = 1f + squash;

        ctx.Save();
        ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = strokeWidth;
        ctx.LineCap = "round";

        // 1. Clavicle Handlebars
        ctx.BeginPath();
        ctx.MoveTo(leftClav.X, leftClav.Y);
        ctx.QuadraticCurveTo((leftClav.X + sternum.X) * 0.5f, sternum.Y + 4f, sternum.X, sternum.Y);
        ctx.QuadraticCurveTo((rightClav.X + sternum.X) * 0.5f, sternum.Y + 4f, rightClav.X, rightClav.Y);
        ctx.Stroke();

        // 2. Pectoralis Major Chest Plates
        var pecY = sternum.Y + H * 0.55f;
        var pecW = H * 0.65f;

        // Hampton's pectoral is a fan whose widest part is low, near the nipple, and whose tail wraps to
        // the front of the humerus. The active side keeps less of that width; the passive side spreads.
        void Pectoral(Point2D clav, float dir, float scale)
        {
            ctx.BeginPath();
            ctx.MoveTo(sternum.X, sternum.Y + 8f);
            ctx.LineTo(sternum.X, pecY);
            ctx.QuadraticCurveTo(sternum.X + dir * pecW * 0.5f * scale, pecY + 6f, clav.X - dir * 8f, pecY - 8f);
            ctx.LineTo(clav.X - dir * 4f, clav.Y + 8f);
            ctx.Stroke();
        }

        Pectoral(leftClav, -1f, leftScale);
        Pectoral(rightClav, 1f, rightScale);

        // 3. Linea Alba & Rectus Abdominis Six-Pack
        ctx.BeginPath();
        ctx.MoveTo(sternum.X, pecY);
        ctx.LineTo(navel.X, navel.Y + H * 0.4f);
        ctx.Stroke();

        // Hampton: the abdominal group holds EIGHT sections, and the row at the navel is the straight
        // one - the rows above it progressively rise to a peak. Two flat tiers used to be drawn here,
        // which is a six-pack with no gesture in it at all.
        var abHalf = H * 0.35f;
        for (var i = 0; i < 4; i++)
        {
            var tierY = navel.Y - (navel.Y - pecY) * (i * 0.20f);
            var rise = abHalf * 0.22f * i;                  // the navel row is flat; each row above bows more
            ctx.BeginPath();
            ctx.MoveTo(sternum.X - abHalf * leftScale, tierY);
            ctx.QuadraticCurveTo(sternum.X, tierY - rise * 2f, sternum.X + abHalf * rightScale, tierY);
            ctx.Stroke();
        }

        // 4. Sternocleidomastoid. Hampton's shape is a baseball bat running diagonally from the
        // manubrium to the base of the skull, behind the ear - and it is never drawn symmetrically,
        // because one side is always higher.
        void Sternomastoid(Point2D clav, float dir, float scale)
        {
            var top = new Point2D(neck.X + dir * H * 0.16f, neck.Y - H * 0.10f * scale);
            var foot = new Point2D(sternum.X + dir * H * 0.06f, sternum.Y);
            ctx.BeginPath();
            ctx.MoveTo(top.X, top.Y);
            ctx.QuadraticCurveTo(neck.X + dir * H * 0.10f, (top.Y + foot.Y) * 0.5f, foot.X, foot.Y);
            ctx.Stroke();
        }

        Sternomastoid(leftClav, -1f, leftScale);
        Sternomastoid(rightClav, 1f, rightScale);

        // 5. Deltoid. Hampton's shape is an inverted triangle from the shoulder girdle down to an
        // insertion about halfway along the upper arm; from the front it is the thin version of it.
        void Deltoid(Point2D clav, IDictionary? arm, float dir)
        {
            var shoulder = ExtractPoint(arm?["shoulder"]);
            var elbow = ExtractPoint(arm?["elbow"]);
            var insert = new Point2D(shoulder.X + (elbow.X - shoulder.X) * 0.42f, shoulder.Y + (elbow.Y - shoulder.Y) * 0.42f);

            ctx.BeginPath();
            ctx.MoveTo(clav.X, clav.Y);
            ctx.QuadraticCurveTo(shoulder.X + dir * H * 0.40f, shoulder.Y + H * 0.14f, insert.X, insert.Y);
            ctx.QuadraticCurveTo(shoulder.X + dir * H * 0.03f, (shoulder.Y + insert.Y) * 0.5f, clav.X, clav.Y);
            ctx.Stroke();
        }

        Deltoid(leftClav, leftArm, -1f);
        Deltoid(rightClav, rightArm, 1f);

        ctx.Restore();
    }

    /// <summary>
    /// Accepted parameter names for <see cref="CreateParametricHead"/>. An unrecognised one is refused.
    /// </summary>
    /// <remarks>
    /// One per facial domain to begin with, chosen to answer whether a named character is reachable
    /// at all before the rest of <i>FaceMaker</i>'s thirty-two are worth writing; the set has grown
    /// since, and the count is deliberately not stated in prose anywhere — it has changed three times
    /// and the prose went stale each time. Refused rather than ignored for the reason
    /// <c>ChartToolkit</c> gives: a misspelled key binds to nothing and silently draws the canon,
    /// which looks like the parameter having no effect.
    /// </remarks>
    private static readonly string[] HeadParameters =
        ["eyesDistance", "eyesSize", "eyesOpening", "noseLength",
         "jawShape", "chinShape", "chinLength", "mouthWidth"];

    /// <summary>
    /// Displaces a Loomis head's landmarks by named character parameters, and returns a whole head.
    /// </summary>
    /// <param name="headObj">A head from <see cref="CreateLoomisHead"/>.</param>
    /// <param name="parameters">
    /// Signed scales, each <c>-1 … 0 … +1</c>, defaulting to <c>0</c>. Out-of-range values are clamped.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>A character is a value, not a procedure.</b> This is a pure function of the head and the
    /// parameters — no clock, no randomness, no state — so the same five numbers give byte-identical
    /// landmarks in panel 1 and panel 40. That is the whole point: consistency across panels comes
    /// from the agent keeping the numbers, which a `const` at the top of `artwork.js` already does,
    /// rather than from anything remembering a face.
    /// </para>
    /// <para>
    /// <b>Zero is the canon, exactly.</b> Every parameter defaults to <c>0</c> and a head built with
    /// none of them, or all of them at zero, is identical to the one that went in. An unparameterised
    /// head must not change, or every comic script already written silently redraws.
    /// </para>
    /// <para>
    /// <b>Displacements are fractions of the head's own <c>unit.H</c></b>, never pixels, so a
    /// parameter means the same thing on a 200px head and a 900px one. Same discipline as
    /// <see cref="ApplyFacialExpression"/>, which scales its offsets by <c>H</c> for the same reason.
    /// </para>
    /// <para>
    /// <b>Returns a complete head, and a deeply cloned one.</b> This paragraph used to say that
    /// <see cref="ApplyFacialExpression"/> "returns only the keys it changed — so passing its result
    /// to <c>drawLoomisWireframe</c> fails on a missing <c>unit</c>". <b>That was false</b>: measured,
    /// it copies every key and adds an <c>expression</c> marker, and its result draws. The real
    /// difference is that its clone is <i>shallow</i>, so the returned head shares its nested groups
    /// with the one passed in and writing to the result's <c>jaw</c> writes to the caller's. This one
    /// copies all the way down.
    /// </para>
    /// <para>
    /// <b>Known limit: the head is already projected.</b> <see cref="CreateLoomisHead"/> applies yaw
    /// and pitch before this sees it, so displacements are applied in screen space rather than head
    /// space. At modest yaw the difference is invisible; past roughly 40° a widened jaw widens the
    /// wrong way. Re-deriving from the canon construction with modified ratios is the correct fix and
    /// is deliberately not done here — this layer is worth proving before it is worth rebuilding.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateParametricHead(object headObj, object? parameters = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a valid dictionary from createLoomisHead", nameof(headObj));

        var opt = JsInterop.AsDict(parameters);
        RefuseUnknownHeadParameters(opt, HeadParameters);

        var result = CloneHead(head);

        var unit = JsInterop.AsDict(head["unit"]);
        var H = unit is not null && unit.Contains("H")
            ? Convert.ToSingle(unit["H"], CultureInfo.InvariantCulture) : 200f;
        var eyeW = unit is not null && unit.Contains("eyeW")
            ? Convert.ToSingle(unit["eyeW"], CultureInfo.InvariantCulture) : H * 0.14f;

        float P(string name) => Math.Clamp(Param(opt, name), -1f, 1f);

        // Each parameter's full-scale displacement, as a fraction of head height. Kept modest: a
        // face at +1 should read as a different person, not as a deformity, and the canon sits in
        // the middle of a range a reader would accept as human.
        MoveEyes(result, P("eyesDistance") * eyeW * 0.45f, P("eyesSize"));
        OpenEyes(result, P("eyesOpening"));
        StretchNose(result, P("noseLength") * H * 0.07f);
        SquareJaw(result, P("jawShape"));
        ShapeChin(result, P("chinShape"), H);
        LengthenChin(result, P("chinLength"), H);
        WidenMouth(result, P("mouthWidth") * H * 0.06f);

        return result;
    }

    /// <summary>
    /// Blends one head toward another: <c>base + amount * (to - from)</c>, in head-relative terms.
    /// </summary>
    /// <param name="baseObj">The head the result is built from. Its <c>unit</c> and <c>origin</c> are kept.</param>
    /// <param name="fromObj">The reference the difference is measured from.</param>
    /// <param name="toObj">The reference the difference is measured to.</param>
    /// <param name="amount">How much of that difference to apply. Unbounded; see the remarks.</param>
    /// <remarks>
    /// <para>
    /// <b>One operation, because identity, caricature and expression are the same arithmetic.</b>
    /// Brennan's Caricature Generator differenced two faces of identical topology point by point,
    /// scaled the difference vectors, and added them back — and generated expression the same way,
    /// by comparing a face to a stored template. Her description of it is the clearest: <i>the
    /// converse of in-betweening — rather than averaging points together, the distance between them
    /// is increased.</i> Three arguments rather than two is what covers both uses:
    /// </para>
    /// <list type="bullet">
    /// <item><c>blendHead(a, a, b, t)</c> — interpolate two faces.</item>
    /// <item><c>blendHead(subject, norm, subject, k)</c> — caricature, which is her formula exactly.</item>
    /// <item><c>blendHead(head, neutral, template, w)</c> — an expression applied at a weight, which
    /// is FACS's formula exactly, and additive so several compose by folding.</item>
    /// </list>
    /// <para>
    /// <b>Normalisation is not optional, and is the step a summary of this method drops.</b> Every
    /// value is taken to head-relative terms before differencing — a point as
    /// <c>(p - origin) / H</c>, a length as <c>s / H</c> — and returned to the base head's terms
    /// after. Without it the blend amplifies differences of <i>size and position</i> rather than of
    /// <i>shape</i>, so morphing a 200px head toward a 180px template shrinks the face and calls it
    /// an expression. It runs, and it is wrong.
    /// </para>
    /// <para>
    /// <b>Correspondence is by name, and a missing key is refused rather than skipped.</b> Brennan
    /// needed points consistent in number and order, and carried invisible <i>virtual lines</i> on
    /// faces without wrinkles so that the correspondence never broke. Every head from
    /// <see cref="CreateLoomisHead"/> has an identical key tree, so we get that free — but a head
    /// assembled by hand might not, and a silently skipped landmark is a face that blends everywhere
    /// except one feature, which reads as a drawing bug rather than a data one.
    /// </para>
    /// <para>
    /// <b>Yaw must agree.</b> Blending a frontal head with a three-quarter one interpolates through a
    /// projection that corresponds to no viewing angle, and reads as a face melting rather than
    /// turning. The turn is recovered from <c>farEye.width / unit.eyeW</c>, as
    /// <see cref="CreateHeadGeometry"/> already does. Pitch is not separately recoverable from the
    /// dictionary and remains the caller's responsibility.
    /// </para>
    /// <para>
    /// Source: Susan E. Brennan, <i>Caricature Generator: The Dynamic Exaggeration of Faces by
    /// Computer</i>, Leonardo 18(3):170-178, 1985.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> BlendHead(object baseObj, object fromObj, object toObj, float amount)
    {
        if (JsInterop.AsDict(baseObj) is not IDictionary b)
            throw new ArgumentException("base must be a head from createLoomisHead", nameof(baseObj));
        if (JsInterop.AsDict(fromObj) is not IDictionary f)
            throw new ArgumentException("from must be a head from createLoomisHead", nameof(fromObj));
        if (JsInterop.AsDict(toObj) is not IDictionary t)
            throw new ArgumentException("to must be a head from createLoomisHead", nameof(toObj));

        if (!float.IsFinite(amount))
        {
            throw new ArgumentException(
                $"blendHead amount must be a finite number, got {amount}. An amount of 0 returns the "
                + "base head unchanged; 1 applies the whole difference.", nameof(amount));
        }

        // A blend across a turn interpolates through a projection nothing was ever seen at, so it is
        // refused rather than averaged - the failure is a face that melts, which reads as a defect in
        // the construction rather than in the arguments.
        float yawA = HeadTurn(f), yawB = HeadTurn(t);
        if (MathF.Abs(yawA - yawB) > 0.02f)
        {
            throw new ArgumentException(
                $"blendHead cannot blend heads at different turns: from is at cos(yaw) {yawA:0.###} and "
                + $"to is at {yawB:0.###}. Build both with the same yawDeg, or blend in head space "
                + "before projecting.", nameof(toObj));
        }

        var result = CloneHead(b);
        var (bo, bh) = HeadFrame(b, nameof(baseObj));
        var (fo, fh) = HeadFrame(f, nameof(fromObj));
        var (to_, th) = HeadFrame(t, nameof(toObj));

        foreach (var (path, kind) in HeadSchema)
        {
            switch (kind)
            {
                case HeadValue.Point:
                    var pb = SchemaPoint(b, path, nameof(baseObj));
                    var pf = SchemaPoint(f, path, nameof(fromObj));
                    var pt = SchemaPoint(t, path, nameof(toObj));
                    var nx = ((pb.X - bo.X) / bh) + (amount * (((pt.X - to_.X) / th) - ((pf.X - fo.X) / fh)));
                    var ny = ((pb.Y - bo.Y) / bh) + (amount * (((pt.Y - to_.Y) / th) - ((pf.Y - fo.Y) / fh)));
                    WriteSchemaPoint(result, path, new Point2D(bo.X + (nx * bh), bo.Y + (ny * bh)));
                    break;

                default:
                    float Norm(IDictionary h, Point2D o, float hh, string which)
                    {
                        var v = SchemaScalar(h, path, which);
                        return kind switch
                        {
                            HeadValue.CoordX => (v - o.X) / hh,
                            HeadValue.CoordY => (v - o.Y) / hh,
                            _ => v / hh,
                        };
                    }

                    var n = Norm(b, bo, bh, nameof(baseObj))
                          + (amount * (Norm(t, to_, th, nameof(toObj)) - Norm(f, fo, fh, nameof(fromObj))));
                    WriteSchemaScalar(result, path, kind switch
                    {
                        HeadValue.CoordX => bo.X + (n * bh),
                        HeadValue.CoordY => bo.Y + (n * bh),
                        _ => n * bh,
                    });
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Pushes a head further from a reference: Brennan's caricature dial.
    /// </summary>
    /// <param name="headObj">The subject.</param>
    /// <param name="amount">
    /// <c>0</c> leaves it alone; <c>1</c> doubles every difference from the reference, which is
    /// Brennan's own choice of the best caricature in her published sequence. Negative values move
    /// the face toward the reference and, past <c>-1</c>, out the other side of it.
    /// </param>
    /// <param name="referenceObj">
    /// What to measure against. Omitted, the canon is used: the same head with no character
    /// parameters, built at this head's own origin, size and turn.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>No feature is selected, and that is what makes it implementable.</b> Every spatial
    /// relationship is exaggerated in parallel; Brennan's system makes no qualitative judgment about
    /// which feature is distinctive, because <i>a relationship becomes a "feature" only when it
    /// differs significantly from the corresponding relationship on a comparison face</i>. The part
    /// that would otherwise need a trained model is the part being declined.
    /// </para>
    /// <para>
    /// <b>The bound is worth knowing and is not enforced.</b> Brennan quotes Francis Grose: a modest
    /// deviation causes laughter, a great one incites horror. Her published ladder runs 0, 50, 100,
    /// 140 and 160 per cent, and she names 100 - <c>amount = 1</c> here - as the best of them.
    /// </para>
    /// <para>
    /// <b>At large amounts the silhouette can break, and that is inherited rather than accidental.</b>
    /// She deliberately left lines unconstrained, so that at high exaggeration <i>an eye is free to
    /// float above an eyebrow</i>, and kept it because users enjoyed finding the limit.
    /// <see cref="CreateHeadGeometry"/> unions its masses into one contour, so the same freedom shows
    /// up there as a broken outline rather than as a style. Nothing clamps it; look at the render.
    /// </para>
    /// <para>
    /// <b>One reference is our simplification, not her finding.</b> She reports the opposite - that
    /// her results <i>throw into question the idea that there need be only one strong norm for all
    /// human faces</i>, and that a successful caricature often came from comparing against any face
    /// that simply seemed very different. The default is a convenience; <paramref name="referenceObj"/>
    /// is how you disagree with it.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ExaggerateHead(object headObj, float amount, object? referenceObj = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a head from createLoomisHead", nameof(headObj));

        var reference = referenceObj is not null && JsInterop.AsDict(referenceObj) is IDictionary r
            ? r
            : CanonFor(head);

        return BlendHead(head, reference, head, amount);
    }

    /// <summary>
    /// Displaces a head by named muscle actions: <c>{ AU4: 0.9, AU7: 0.7 }</c>.
    /// </summary>
    /// <param name="headObj">A head from <see cref="CreateLoomisHead"/>.</param>
    /// <param name="weights">Action Unit names against weights in <c>0 … 1</c>. Unknown names are refused.</param>
    /// <remarks>
    /// <para>
    /// <b>Muscles, not emotions, and that is a decision with a source.</b> Loomis sets aside the
    /// psychological phase of expression explicitly, calling the emotions <i>too numerous to
    /// tabulate</i>, and works instead from two antagonist muscle groups plus a handful of wrinkle
    /// muscles. Ekman &amp; Friesen arrive at the same anatomy from measurement rather than from
    /// drawing. What is finite here is the muscles; a named emotion is a tuple of these, and a
    /// contested one.
    /// </para>
    /// <para>
    /// <b>Additive and order-independent.</b> Each unit adds its own displacement, so
    /// <c>{ AU4, AU7 }</c> means both and gives the same head whichever is folded first. That is what
    /// makes a small set cover a large range of faces without a preset per combination.
    /// </para>
    /// <para>
    /// <b>The units are chosen for what the construction can actually show.</b> A unit that moved a
    /// landmark this head does not carry would be an API that quietly does nothing, which is worse
    /// than an omission — so the set stops where the geometry does. What is missing and why is in the
    /// remarks on <see cref="ActionUnits"/>.
    /// </para>
    /// <para>
    /// <b><c>options.side</c> acts on one half of the face, and it is what makes a raised eyebrow and
    /// a smirk reachable at all.</b> Every unit moved both halves until 2026-09-19, so the single
    /// most recognisable comic brow — one up, one not — could not be drawn, and neither could a
    /// one-sided smile. <c>'near'</c> and <c>'far'</c> name the two halves this construction already
    /// has; see <see cref="FaceSide"/> for why they are not called left and right. <b><c>AU26</c>
    /// ignores the option</b>, because a jaw does not drop on one side, and an option silently doing
    /// nothing there is better than a half-open jaw that no anatomy explains.
    /// </para>
    /// <para>
    /// <b>Three lineages split their brow units per side and this construction did not.</b> ARKit and
    /// MediaPipe carry <c>browDownLeft</c>/<c>browDownRight</c> and <c>browOuterUpLeft</c>/<c>Right</c>;
    /// CANDIDE-3 carries an <i>Eyes vertical difference</i> shape unit. Asymmetry was the one axis
    /// every one of those sources had and this one had nowhere at all.
    /// </para>
    /// <para>
    /// <b>Magnitudes are the studio's, tuned by eye.</b> Neither source supplies displacement
    /// numbers: Loomis gives directions and a relaxed/contracted table, and Ekman &amp; Friesen's
    /// 1976 code scores <i>presence</i> — slight against strong — rather than a continuous
    /// intensity. Anything claiming a measured decimal for these is claiming more than either source
    /// says.
    /// </para>
    /// <para>
    /// Sources: Andrew Loomis, <i>Drawing the Head and Hands</i> (Viking, 1956), pp. 45–47 and
    /// Plate 21; Paul Ekman &amp; Wallace V. Friesen, <i>Measuring Facial Movement</i>, Environmental
    /// Psychology and Nonverbal Behavior 1(1):56–75, 1976, Table 1.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ApplyActionUnits(object headObj, object? weights = null, object? options = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a head from createLoomisHead", nameof(headObj));

        var optionDict = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(optionDict, ActionUnitOptions, "applyActionUnits option");
        var side = ReadSide(optionDict);

        var opt = JsInterop.AsDict(weights);
        var result = CloneHead(head);
        if (opt is null) return result;

        var h = Num(JsInterop.AsDict(head["unit"]), "H", 200f);

        foreach (DictionaryEntry entry in opt)
        {
            var name = entry.Key?.ToString()?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(name)) continue;

            if (!ActionUnits.TryGetValue(name, out var unit))
            {
                throw new ArgumentException(
                    $"'{entry.Key}' is not an Action Unit this construction can draw. Known: "
                    + string.Join(", ", ActionUnits.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    + ". The numbering is Ekman & Friesen's and is arbitrary by their own account, so "
                    + "a unit absent here is one the head carries no landmark for rather than one "
                    + "that does not exist.", nameof(weights));
            }

            var w = entry.Value is null ? 0f : Convert.ToSingle(entry.Value, CultureInfo.InvariantCulture);
            if (!float.IsFinite(w))
                throw new ArgumentException($"{name} weight must be a finite number, got {w}.", nameof(weights));

            // Refused rather than clamped to zero, because the opposite of an Action Unit is a
            // DIFFERENT unit - that separation is the whole design of the coding system, and a
            // negative weight means the caller has the wrong unit rather than the wrong sign.
            if (w < 0f)
            {
                throw new ArgumentException(
                    $"{name} weight is {w}; Action Units do not run negative. The opposing action is "
                    + "its own unit - AU1 raises the brow where AU4 lowers it, AU12 lifts the mouth "
                    + "corners where AU15 drops them. Name the unit you mean.", nameof(weights));
            }

            unit(result, MathF.Min(w, 1f), h, side);
        }

        return result;
    }

    /// <summary>Accepted option names for <see cref="ApplyActionUnits"/>.</summary>
    private static readonly string[] ActionUnitOptions = ["side"];

    /// <summary>
    /// Whether a head's landmarks still run in the order a face's do, and by how much.
    /// </summary>
    /// <param name="headObj">Any head — constructed, parameterised, exaggerated or expressed.</param>
    /// <remarks>
    /// <para>
    /// <b>The check that turns "an eye is free to float above an eyebrow" into a number.</b> Brennan
    /// deliberately left her lines unconstrained at high exaggeration and kept it because her users
    /// enjoyed finding the limit — but her users were watching a screen. An agent reads images poorly
    /// and will ship what comes out, so the limit needs to be *measurable* here rather than merely
    /// visible.
    /// </para>
    /// <para>
    /// <b>It reports rather than refuses, and the reason is measured.</b> The safe exaggeration
    /// varies more than thirty-fold between characters — a mild one holds its order past λ 12, the
    /// canon never breaks at all, and a head carrying <c>noseLength: 1</c> breaks at <b>0.40</b>,
    /// below the setting Brennan recommends. No constant could clamp that, so the honest move is to
    /// hand back the number and let the caller decide.
    /// </para>
    /// <para>
    /// <b><c>margin</c> is the useful field, not <c>ordered</c>.</b> It is the smallest gap between
    /// consecutive stations as a fraction of head height, so it is comparable across sizes and it
    /// shrinks toward zero *before* it crosses — which is what lets a run see the edge coming rather
    /// than discover it. Negative means broken, and how badly.
    /// </para>
    /// <para>
    /// <b>This is a judgment encoded, not a law.</b> A face whose stations run in order can still be
    /// a bad drawing, and a deliberately grotesque one may cross a station on purpose. What it
    /// catches is the specific failure that renders perfectly and reads as a defect in the toolkit.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> VerifyHeadOrdering(object headObj)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a head from createLoomisHead", nameof(headObj));

        var h = Num(JsInterop.AsDict(head["unit"]), "H", 200f);
        if (h <= 0f) h = 200f;

        // Top to bottom. The eye is read from the near eye's own centre rather than from `eyeLineY`,
        // because the parameter layer moves the eye points and leaves that scalar where it was - so
        // checking the scalar would pass on a head whose drawn eyes had crossed the brow.
        //
        // **The brow is read the same way, and for the same reason one step later.** `head["brow"]`
        // is the ball's equator and no expression moves it any more - AU1, AU2 and AU4 move the brow
        // stations. Read from that landmark this ladder would report a fixed brow line however far a
        // frown had driven the drawn brows into the eyes, which is precisely the failure the eye
        // entry above already exists to prevent. The LOWEST station is the one that crosses first.
        var ladder = new (string Name, float Y)[]
        {
            ("crown", ExtractPoint(head["crown"]).Y),
            ("hairline", ExtractPoint(head["hairline"]).Y),
            ("brow", DrawnBrowY(head)),
            ("eyes", ExtractPoint(JsInterop.AsDict(head["nearEye"])?["center"]).Y),
            ("nose", ExtractPoint(head["noseBase"]).Y),
            ("mouth", ExtractPoint(JsInterop.AsDict(head["mouthGuides"])?["center"]).Y),
            ("chin", ExtractPoint(head["chin"]).Y),
        };

        var broken = new List<object?>();
        var margin = float.MaxValue;

        for (var i = 1; i < ladder.Length; i++)
        {
            var gap = (ladder[i].Y - ladder[i - 1].Y) / h;
            margin = MathF.Min(margin, gap);
            if (gap < 0f) broken.Add($"{ladder[i].Name} above {ladder[i - 1].Name}");
        }

        // A separate class of break: the mouth turning itself inside out. Reachable by exaggerating a
        // narrowed mouth, and it draws as a bow-tie rather than as a mouth.
        var mouth = JsInterop.AsDict(head["mouthGuides"]);
        if (mouth is not null)
        {
            var left = ExtractPoint(mouth["leftCorner"]);
            var right = ExtractPoint(mouth["rightCorner"]);
            var width = (right.X - left.X) / h;
            margin = MathF.Min(margin, MathF.Abs(width));
            if (width <= 0f) broken.Add("mouth corners crossed");
        }

        if (margin == float.MaxValue) margin = 0f;
        var ordered = broken.Count == 0;

        return new Dictionary<string, object?>
        {
            ["ordered"] = ordered,
            ["margin"] = margin,
            ["broken"] = broken,
            ["message"] = ordered
                ? $"head stations in order, tightest gap {margin:0.###} H"
                : $"head no longer reads as a face: {string.Join("; ", broken)} "
                  + $"(margin {margin:0.###} H). Lower the exaggeration, or the character parameter "
                  + "that closed the gap - noseLength is the usual one, since its full range already "
                  + "spends most of the nose-to-mouth distance.",
        };
    }

    /// <summary>
    /// What a named expression is, as muscle weights: <c>expressionUnits('sadness')</c>.
    /// </summary>
    /// <param name="expressionType">One of the six, or an alias. Case and surrounding space ignored.</param>
    /// <param name="intensity">Scales every weight. Defaults to full strength.</param>
    /// <remarks>
    /// <para>
    /// <b>This exists so that a named emotion is a claim you can read rather than a displacement you
    /// cannot.</b> The six names were previously a closed door: a caller who wanted one muscle had to
    /// buy a whole emotion and turn it down, and had no way to see what they were getting. Returning
    /// the tuple makes the claim inspectable, adjustable, and — most usefully — arguable.
    /// </para>
    /// <para>
    /// <b>The weights are the studio's, and no source supplies them.</b> Loomis gives directions and
    /// a relaxed/contracted table; Ekman &amp; Friesen's 1976 code scores <i>presence</i>, slight
    /// against strong, and contains the words <i>anger</i>, <i>happy</i> and <i>surprise</i> exactly
    /// zero times. Published emotion-to-unit tables are somebody's interpretation, not a measurement,
    /// and these are ours. Read them, disagree, pass your own dictionary to
    /// <see cref="ApplyActionUnits"/>.
    /// </para>
    /// <para>
    /// <b>Three of these tuples changed on 2026-09-18, when the head grew brow stations.</b> This
    /// paragraph used to record two consequences of there being one <c>brow</c> point: a canonical
    /// sad brow is AU1 with AU4 — the inner corners lift while the brows knit — and on one landmark
    /// those two <b>cancelled</b>, so <c>sadness</c> carried AU1 alone; and <c>fear</c> and
    /// <c>surprise</c> differed only in amount, because what separates them in life is AU4 knitting
    /// an already-raised brow. It ended by saying both would resolve if <see cref="CreateLoomisHead"/>
    /// ever carried inner and outer brow stations. It does, and they did: <c>sadness</c> names AU1
    /// and AU4 together for the oblique brow, and <c>fear</c> carries the corrugator where
    /// <c>surprise</c> arches without it.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ExpressionUnits(string expressionType, float intensity = 1.0f)
    {
        var name = (expressionType ?? string.Empty).Trim().ToLowerInvariant();
        var canonical = name switch
        {
            "happy" => "joy",
            "angry" => "anger",
            "scared" => "fear",
            "sad" => "sadness",
            _ => name,
        };

        if (!ExpressionTuples.TryGetValue(canonical, out var tuple))
        {
            throw new ArgumentException(
                $"'{expressionType}' is not a named expression. Known: "
                + string.Join(", ", ExpressionTuples.Keys.OrderBy(k => k, StringComparer.Ordinal))
                + ". These six are the toolkit's enumeration rather than a claim that the emotions "
                + "number six - Loomis calls them too numerous to tabulate. For anything else, name "
                + "the muscles: applyActionUnits(head, { AU4: 0.9 }).", nameof(expressionType));
        }

        if (!float.IsFinite(intensity) || intensity < 0f)
        {
            throw new ArgumentException(
                $"intensity must be a finite number of zero or more, got {intensity}. A negative "
                + "expression is not a thing: the opposite of a raised brow is a lowered one, which "
                + "is its own Action Unit.", nameof(intensity));
        }

        var scaled = new Dictionary<string, object?>(tuple.Count, StringComparer.Ordinal);
        foreach (var (unit, weight) in tuple) scaled[unit] = MathF.Min(weight * intensity, 1f);
        return scaled;
    }

    /// <summary>
    /// Applies a named expression. A thin wrapper over <see cref="ApplyActionUnits"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reimplemented 2026-09-18, and what it draws has changed.</b> Each of the six used to
    /// displace one or two landmarks directly, and several did not do what their own manual entry
    /// described: <c>sadness</c> was documented as lifting the inner brow and dropping the mouth
    /// corners, and moved only the mouth. A live run asked for it <i>"at 0.22 for the inner-brow lift
    /// only"</i>, recorded a careful threshold at which the lift would become a grimace, and received
    /// about two and a half pixels of a movement it had not wanted. That is the defect this replaces:
    /// not thinness, but a name that promised one muscle and moved another.
    /// </para>
    /// <para>
    /// <b>Prefer <see cref="ApplyActionUnits"/> where you know what you want.</b> This is the
    /// convenience call, and its tuples are visible through <see cref="ExpressionUnits"/> precisely
    /// so that reaching past it is easy.
    /// </para>
    /// <para>
    /// An unknown name is now <b>refused</b> rather than silently returning the head unchanged, and
    /// the result is a deep clone rather than a shallow one, so writing to its <c>jaw</c> no longer
    /// writes to the caller's.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ApplyFacialExpression(object headObj, string expressionType,
                                                             float intensity = 1.0f, object? options = null)
    {
        var head = ApplyActionUnits(headObj, ExpressionUnits(expressionType, intensity), options);

        // Kept from the previous implementation: a head that can say what it is costs nothing, and
        // something downstream may be reading it. Written after the units so a caller cannot smuggle
        // an `expression` key in through the weights.
        head["expression"] = (expressionType ?? string.Empty).Trim().ToLowerInvariant();
        return head;
    }

    #endregion

    #region Composition Armatures, Notan & Visual Emphasis
    public Dictionary<string, object?> CreateCompositionGrid(float width, float height, string type = "ruleOfThirds", object? options = null)
    {
        var gridType = type.Trim().ToLowerInvariant();
        var lines = new List<List<Dictionary<string, object?>>>();
        var powerPoints = new Dictionary<string, object?>();

        switch (gridType)
        {
            case "ruleofthirds":
            case "thirds":
            default:
                gridType = "ruleOfThirds";
                var x1 = width / 3f;
                var x2 = width * 2f / 3f;
                var y1 = height / 3f;
                var y2 = height * 2f / 3f;

                // 2 Vertical Lines
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(x1, 0)), ToDict(new Point2D(x1, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(x2, 0)), ToDict(new Point2D(x2, height)) });

                // 2 Horizontal Lines
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, y1)), ToDict(new Point2D(width, y1)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, y2)), ToDict(new Point2D(width, y2)) });

                powerPoints["topLeft"] = ToDict(new Point2D(x1, y1));
                powerPoints["topRight"] = ToDict(new Point2D(x2, y1));
                powerPoints["bottomLeft"] = ToDict(new Point2D(x1, y2));
                powerPoints["bottomRight"] = ToDict(new Point2D(x2, y2));
                break;

            case "goldenratio":
            case "goldenspiral":
            case "fibonacci":
                gridType = "goldenRatio";
                const float phi = 1.6180339887f;
                const float r = 1f / phi; // ~0.618

                var gx1 = width * (1f - r);
                var gx2 = width * r;
                var gy1 = height * (1f - r);
                var gy2 = height * r;

                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(gx1, 0)), ToDict(new Point2D(gx1, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(gx2, 0)), ToDict(new Point2D(gx2, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, gy1)), ToDict(new Point2D(width, gy1)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, gy2)), ToDict(new Point2D(width, gy2)) });

                // Diagonal bar
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, 0)), ToDict(new Point2D(width, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width, 0)), ToDict(new Point2D(gx1, height)) });

                powerPoints["goldenEye"] = ToDict(new Point2D(gx2, gy2));
                powerPoints["secondaryEye"] = ToDict(new Point2D(gx1, gy1));
                break;

            case "dynamicsymmetry":
            case "harmonicarmature":
                gridType = "dynamicSymmetry";
                // 1. Corner Diagonals
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, 0)), ToDict(new Point2D(width, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width, 0)), ToDict(new Point2D(0, height)) });

                // 2. Midpoint Cross
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width * 0.5f, 0)), ToDict(new Point2D(width * 0.5f, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, height * 0.5f)), ToDict(new Point2D(width, height * 0.5f)) });

                // 3. Midpoint Diamond
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width * 0.5f, 0)), ToDict(new Point2D(width, height * 0.5f)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width, height * 0.5f)), ToDict(new Point2D(width * 0.5f, height)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(width * 0.5f, height)), ToDict(new Point2D(0, height * 0.5f)) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(new Point2D(0, height * 0.5f)), ToDict(new Point2D(width * 0.5f, 0)) });

                powerPoints["center"] = ToDict(new Point2D(width * 0.5f, height * 0.5f));
                powerPoints["harmonicTopLeft"] = ToDict(new Point2D(width * 0.25f, height * 0.25f));
                powerPoints["harmonicTopRight"] = ToDict(new Point2D(width * 0.75f, height * 0.25f));
                break;

            case "triangle":
            case "pyramid":
                gridType = "triangle";
                var apex = new Point2D(width * 0.5f, height * 0.15f);
                var botL = new Point2D(width * 0.10f, height * 0.88f);
                var botR = new Point2D(width * 0.90f, height * 0.88f);

                lines.Add(new List<Dictionary<string, object?>> { ToDict(apex), ToDict(botL) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(apex), ToDict(botR) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(botL), ToDict(botR) });
                lines.Add(new List<Dictionary<string, object?>> { ToDict(apex), ToDict(new Point2D(width * 0.5f, height * 0.88f)) });

                powerPoints["apex"] = ToDict(apex);
                powerPoints["center"] = ToDict(new Point2D(width * 0.5f, height * 0.55f));
                break;
        }

        return new Dictionary<string, object?>
        {
            ["type"] = gridType,
            ["width"] = width,
            ["height"] = height,
            ["lines"] = lines,
            ["powerPoints"] = powerPoints
        };
    }

    public void DrawCompositionGrid(CanvasRenderingContext2D ctx, object gridObjOrType, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        IDictionary? grid = null;

        if (gridObjOrType is string typeStr)
        {
            grid = CreateCompositionGrid(ctx.Canvas.Width, ctx.Canvas.Height, typeStr, options);
        }
        else if (JsInterop.AsDict(gridObjOrType) is IDictionary dict)
        {
            grid = dict;
        }

        if (grid == null) return;

        var opt = JsInterop.AsDict(options);
        var lineColor = opt?["lineColor"]?.ToString() ?? "#3ba3d0";
        var pointColor = opt?["pointColor"]?.ToString() ?? "#e74c3c";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.2f;
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.45f;

        ctx.Save();
        ctx.GlobalAlpha = opacity;
        ctx.StrokeStyle = lineColor;
        ctx.FillStyle = pointColor;
        ctx.LineWidth = lineWidth;

        // 1. Draw Grid Lines
        if (grid["lines"] is IList lines)
        {
            foreach (var l in lines)
            {
                if (l is not IList pts || pts.Count < 2) continue;
                var a = ExtractPoint(pts[0]);
                var b = ExtractPoint(pts[1]);
                ctx.BeginPath();
                ctx.MoveTo(a.X, a.Y);
                ctx.LineTo(b.X, b.Y);
                ctx.Stroke();
            }
        }

        // 2. Draw Focal Power Points
        if (JsInterop.AsDict(grid["powerPoints"]) is IDictionary pps)
        {
            foreach (DictionaryEntry de in pps)
            {
                var pt = ExtractPoint(de.Value);
                ctx.BeginPath();
                ctx.Arc(pt.X, pt.Y, 5f, 0f, MathF.PI * 2f);
                ctx.Fill();
            }
        }

        ctx.Restore();
    }

    public void DrawVignette(CanvasRenderingContext2D ctx, float width, float height, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        var vignetteColor = opt?["vignetteColor"]?.ToString() ?? "#080b10";
        var intensity = opt != null && opt.Contains("intensity") ? Convert.ToSingle(opt["intensity"], CultureInfo.InvariantCulture) : 0.65f;
        var radius = opt != null && opt.Contains("radius") ? Convert.ToSingle(opt["radius"], CultureInfo.InvariantCulture) : MathF.Max(width, height) * 0.72f;

        var cx = width * 0.5f;
        var cy = height * 0.5f;

        ctx.Save();
        ctx.GlobalAlpha = intensity;
        var grad = ctx.CreateRadialGradient(cx, cy, radius * 0.35f, cx, cy, radius);
        grad.AddColorStop(0.0f, "rgba(0,0,0,0)");
        grad.AddColorStop(0.65f, "rgba(0,0,0,0.15)");
        grad.AddColorStop(1.0f, vignetteColor);

        ctx.FillStyle = grad;
        ctx.FillRect(0, 0, width, height);
        ctx.Restore();
    }

    public void DrawLeadingLines(CanvasRenderingContext2D ctx, object originPoints, object focalPoint, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var fp = ExtractPoint(focalPoint);
        if (originPoints is not IList list || list.Count == 0) return;

        var opt = JsInterop.AsDict(options);
        var lineColor = opt?["lineColor"]?.ToString() ?? "#e74c3c";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.2f;
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.35f;

        ctx.Save();
        ctx.GlobalAlpha = opacity;
        ctx.StrokeStyle = lineColor;
        ctx.LineWidth = lineWidth;

        foreach (var item in list)
        {
            var p = ExtractPoint(item);
            ctx.BeginPath();
            ctx.MoveTo(p.X, p.Y);
            ctx.LineTo(fp.X, fp.Y);
            ctx.Stroke();
        }

        ctx.Restore();
    }

    public Dictionary<string, object?> CreateNotanPalette(string type = "classic3")
    {
        var pal = type.Trim().ToLowerInvariant();
        return pal switch
        {
            "binary" or "2value" => new Dictionary<string, object?>
            {
                ["type"] = "binary",
                ["dominant"] = "#ffffff",
                ["secondary"] = "#0a0a0c"
            },
            "highkey" => new Dictionary<string, object?>
            {
                ["type"] = "highKey",
                ["background"] = "#ffffff",
                ["formLight"] = "#e2e8f0",
                ["formMid"] = "#a0aec0",
                ["darkAccent"] = "#4a5568"
            },
            "lowkey" => new Dictionary<string, object?>
            {
                ["type"] = "lowKey",
                ["background"] = "#0c0f14",
                ["formDark"] = "#1a202c",
                ["formMid"] = "#3e4c5e",
                ["rimAccent"] = "#e2e8f0"
            },
            _ => new Dictionary<string, object?>
            {
                ["type"] = "classic3",
                ["dominantLight"] = "#f4f6f8",
                ["secondaryMid"] = "#828c9b",
                ["accentDark"] = "#181e28"
            }
        };
    }

    public Dictionary<string, object?> SubdivideProportions(object bounds, string direction = "horizontal", object? ratios = null)
    {
        var b = JsInterop.AsDict(bounds);
        var bx = b != null && b.Contains("x") ? Convert.ToSingle(b["x"], CultureInfo.InvariantCulture) : 0f;
        var by = b != null && b.Contains("y") ? Convert.ToSingle(b["y"], CultureInfo.InvariantCulture) : 0f;
        var bw = b != null && b.Contains("width") ? Convert.ToSingle(b["width"], CultureInfo.InvariantCulture) : 800f;
        var bh = b != null && b.Contains("height") ? Convert.ToSingle(b["height"], CultureInfo.InvariantCulture) : 600f;

        var isHoriz = direction.Trim().ToLowerInvariant() == "horizontal";

        // Big: 70%, Medium: 20%, Small: 10%
        if (isHoriz)
        {
            var wBig = bw * 0.70f;
            var wMed = bw * 0.20f;
            var wSmall = bw * 0.10f;

            return new Dictionary<string, object?>
            {
                ["big"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by, ["width"] = wBig, ["height"] = bh },
                ["medium"] = new Dictionary<string, object?> { ["x"] = bx + wBig, ["y"] = by, ["width"] = wMed, ["height"] = bh },
                ["small"] = new Dictionary<string, object?> { ["x"] = bx + wBig + wMed, ["y"] = by, ["width"] = wSmall, ["height"] = bh }
            };
        }
        else
        {
            var hBig = bh * 0.70f;
            var hMed = bh * 0.20f;
            var hSmall = bh * 0.10f;

            return new Dictionary<string, object?>
            {
                ["big"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by, ["width"] = bw, ["height"] = hBig },
                ["medium"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by + hBig, ["width"] = bw, ["height"] = hMed },
                ["small"] = new Dictionary<string, object?> { ["x"] = bx, ["y"] = by + hBig + hMed, ["width"] = bw, ["height"] = hSmall }
            };
        }
    }
    #region Hands
    /// <summary>
    /// Computes hand landmarks on Loomis's scale (<i>Drawing the Head and Hands</i>, Plates 78-79,
    /// pp. 136-137). <paramref name="originX"/>/<paramref name="originY"/> is the <b>centre of the
    /// wrist</b>, and the hand runs toward the fingertips along <c>rotationDeg</c> (0 = up the page).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loomis's proportions, all as fractions of <paramref name="handLength"/> (wrist to middle
    /// fingertip): the middle finger measured from its knuckle at the back is <b>slightly over half</b>
    /// the hand, so the palm is the rest; the palm is <b>slightly more than half the hand</b> wide; the
    /// index reaches the middle finger's fingernail; the ring is about equal to the index; the little
    /// finger reaches the ring's top knuckle. Two fingers fall on each side of a line through the middle
    /// of the palm, and the thumb is turned at right angles to the others.
    /// </para>
    /// <para>
    /// What is <i>not</i> his and is the studio's: the split of a finger into its three phalanges, the
    /// thumb's length, and the exact fractions standing in for "reaches the fingernail" and "reaches the
    /// top knuckle" - both need a nail or a phalanx length he never gives.
    /// </para>
    /// <para>Exposed to scripts, so members are PascalCase here and camelCase in JS.</para>
    /// </remarks>
    public Dictionary<string, object?> CreateHandFigure(float originX, float originY, float handLength = 200f, object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        float Opt(string key, float fallback) =>
            opt != null && opt.Contains(key) ? Convert.ToSingle(opt[key], CultureInfo.InvariantCulture) : fallback;

        var side = opt?["side"]?.ToString()?.Trim().ToLowerInvariant() ?? "right";
        var mirror = side == "left" ? -1f : 1f;
        var spreadDeg = Opt("spreadDeg", 7f);
        var curlDeg = Opt("curlDeg", 0f);
        var thumbDeg = Opt("thumbDeg", 46f);
        var rotationDeg = Opt("rotationDeg", 0f);

        var L = handLength;
        var palmLength = L * 0.48f;          // the rest of the hand once the middle finger has its share
        var palmWidth = L * 0.55f;           // "slightly more than half the hand", measured inside
        var middleLen = L * 0.52f;           // "slightly over half", knuckle at the back to the tip

        // Loomis states the finger lengths as reaches rather than numbers. The little finger "just reaches
        // the top knuckle of the third finger", which is one distal phalanx down - so once Hampton's 3:2
        // ratio below fixes the distal share, that reach becomes computable rather than guessed. The
        // index's "reaches the fingernail" still needs a nail length neither of them gives; a nail as
        // about half the distal phalanx puts it at 0.90.
        var ratios = new[] { 0.90f, 1.00f, 0.90f, 0.90f * (1f - 4f / 19f) };   // index, middle, ring, little
        var names = new[] { "index", "middle", "ring", "little" };
        // Michael Hampton, Figure Drawing: Design and Invention (2009), "Hand Structure and Proportion":
        // the finger bones run on a 3:2 ratio - divide the proximal phalanx in three and two of those
        // parts are the middle phalanx; divide the middle in three and two of those are the distal. That
        // is 1 : 2/3 : 4/9, normalised below. It replaces a studio guess of 0.45 / 0.30 / 0.25, which was
        // close on the first two bones and had the fingertip a fifth too long.
        var phalanx = new[] { 9f / 19f, 6f / 19f, 4f / 19f };     // 0.474, 0.316, 0.211

        var rot = rotationDeg * MathF.PI / 180f;
        var cosR = MathF.Cos(rot);
        var sinR = MathF.Sin(rot);

        // Hand space runs +y toward the fingertips and +x across the palm toward the thumb; this puts it
        // on the canvas, where y grows downward.
        Point2D Place(float across, float along)
        {
            var x = across * mirror;
            return new Point2D(originX + x * cosR + along * sinR, originY + x * sinR - along * cosR);
        }

        var wristPt = Place(0f, 0f);

        // Knuckle stations across the hand. The knuckle arc is the flat one; the joint arcs beyond it
        // deepen row by row, which happens on its own because the fingers differ in length.
        var acrossAt = new[] { -0.34f, -0.10f, 0.14f, 0.36f };    // index..little, in palm widths from centre
        var knuckleAlong = new[] { 0.99f, 1.00f, 0.96f, 0.88f };  // little sits lowest on the arc

        var fingers = new List<Dictionary<string, object?>>();
        for (var i = 0; i < 4; i++)
        {
            var across = acrossAt[i] * palmWidth;
            var basePt = Place(across, knuckleAlong[i] * palmLength);
            var len = middleLen * ratios[i];

            // Fingers fan from the knuckle line, and curl at each joint by the same amount.
            var fanDeg = (i - 1.5f) * spreadDeg;
            var dir = (rotationDeg + fanDeg * mirror) * MathF.PI / 180f;

            var joints = new List<Dictionary<string, object?>>();
            var cursor = basePt;
            var heading = dir;
            for (var s = 0; s < 3; s++)
            {
                heading += curlDeg * MathF.PI / 180f;
                var seg = len * phalanx[s];
                cursor = new Point2D(cursor.X + seg * MathF.Sin(heading), cursor.Y - seg * MathF.Cos(heading));
                joints.Add(ToDict(cursor));
            }

            fingers.Add(new Dictionary<string, object?>
            {
                ["name"] = names[i],
                ["knuckle"] = ToDict(basePt),
                ["joints"] = joints,               // [first, second, tip]
                ["tip"] = joints[2],
                ["length"] = len,
                ["width"] = palmWidth * 0.21f
            });
        }

        // Loomis: the thumb is turned at right angles to the other fingers, and operates mostly in and
        // out from the palm where the fingers open and close toward it. That is a statement about the
        // PLANE it moves in, not about the angle it makes on the page - drawn at a literal 90 degrees it
        // sticks straight out of the side of the wrist. thumbDeg is the drawn angle off the hand's long
        // axis, and its default and length are the studio's; Loomis gives neither.
        var thumbBase = Place(palmWidth * 0.42f, palmLength * 0.44f);
        var thumbLen = L * 0.34f;
        var thumbDir = (rotationDeg + thumbDeg * mirror) * MathF.PI / 180f;
        var thumbJoints = new List<Dictionary<string, object?>>();
        var tCursor = thumbBase;
        var tHeading = thumbDir;
        foreach (var share in new[] { 0.58f, 0.42f })
        {
            tHeading -= curlDeg * 0.5f * MathF.PI / 180f * mirror;
            var seg = thumbLen * share;
            tCursor = new Point2D(tCursor.X + seg * MathF.Sin(tHeading), tCursor.Y - seg * MathF.Cos(tHeading));
            thumbJoints.Add(ToDict(tCursor));
        }

        return new Dictionary<string, object?>
        {
            ["unit"] = new Dictionary<string, object?>
            {
                ["length"] = L,
                ["palmLength"] = palmLength,
                ["palmWidth"] = palmWidth,
                ["middleLength"] = middleLen,
                ["side"] = side
            },
            ["wrist"] = ToDict(wristPt),
            ["palm"] = new Dictionary<string, object?>
            {
                ["wristInner"] = ToDict(Place(-palmWidth * 0.40f, 0f)),
                ["wristOuter"] = ToDict(Place(palmWidth * 0.40f, 0f)),
                ["knuckleInner"] = ToDict(Place(-palmWidth * 0.46f, palmLength * 0.99f)),
                ["knuckleOuter"] = ToDict(Place(palmWidth * 0.46f, palmLength * 0.92f)),
                ["centre"] = ToDict(Place(0f, palmLength * 0.5f))
            },
            // "Two fingers lie on each side of a line drawn through the middle of the palm."
            ["midLine"] = new Dictionary<string, object?>
            {
                ["from"] = ToDict(wristPt),
                ["to"] = ToDict(Place(0.02f * palmWidth, palmLength * 1.02f))
            },
            // "The big muscle of the thumb is by far the most important one in the hand" - the thenar
            // mass, which is what joins the thumb to the palm instead of leaving it floating.
            ["thenar"] = new Dictionary<string, object?>
            {
                ["wrist"] = ToDict(Place(palmWidth * 0.34f, palmLength * 0.02f)),
                ["crest"] = ToDict(Place(palmWidth * 0.60f, palmLength * 0.22f)),
                ["base"] = ToDict(thumbBase),
                ["web"] = ToDict(Place(palmWidth * 0.34f, palmLength * 0.80f))
            },
            ["fingers"] = fingers,
            ["thumb"] = new Dictionary<string, object?>
            {
                ["base"] = ToDict(thumbBase),
                ["joints"] = thumbJoints,          // [knuckle, tip]
                ["tip"] = thumbJoints[1],
                ["length"] = thumbLen,
                ["width"] = palmWidth * 0.26f
            }
        };
    }

    /// <summary>
    /// Renders the hand's construction: the palm plate, the midline, the knuckle and joint arcs, and each
    /// digit as a jointed line. Non-repro blue for the armature, graphite for the contours.
    /// </summary>
    /// <remarks>Exposed to scripts, so members are PascalCase here and camelCase in JS.</remarks>
    public void DrawHandWireframe(CanvasRenderingContext2D ctx, object handObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(handObj) is not IDictionary hand) return;

        var opt = JsInterop.AsDict(options);
        var blue = opt?["blueLineColor"]?.ToString() ?? "#4a90e2";
        var graphite = opt?["graphiteColor"]?.ToString() ?? "#444444";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.4f;

        var palm = JsInterop.AsDict(hand["palm"]);
        var mid = JsInterop.AsDict(hand["midLine"]);
        var fingers = hand["fingers"] as IList;
        var thumb = JsInterop.AsDict(hand["thumb"]);
        if (palm == null || fingers == null || thumb == null) return;

        var jointR = Convert.ToSingle(JsInterop.AsDict(hand["unit"])?["palmWidth"] ?? 60f, CultureInfo.InvariantCulture) * 0.055f;

        ctx.Save();
        ctx.StrokeStyle = blue;
        ctx.LineWidth = lineWidth;

        // Palm plate.
        var wi = ExtractPoint(palm["wristInner"]);
        var wo = ExtractPoint(palm["wristOuter"]);
        var ki = ExtractPoint(palm["knuckleInner"]);
        var ko = ExtractPoint(palm["knuckleOuter"]);
        ctx.BeginPath();
        ctx.MoveTo(wi.X, wi.Y);
        ctx.LineTo(ki.X, ki.Y);
        ctx.LineTo(ko.X, ko.Y);
        ctx.LineTo(wo.X, wo.Y);
        ctx.ClosePath();
        ctx.Stroke();

        // The line through the middle of the palm, two fingers each side of it.
        if (mid != null)
        {
            var a = ExtractPoint(mid["from"]);
            var b = ExtractPoint(mid["to"]);
            ctx.BeginPath();
            ctx.MoveTo(a.X, a.Y);
            ctx.LineTo(b.X, b.Y);
            ctx.Stroke();
        }

        // Row arcs: knuckles, then each joint row, then the tips. Loomis has the knuckle curve flat and
        // the curves deepening as they cross toward the fingertips, which is what these show.
        void RowArc(Func<IDictionary, Point2D> pick)
        {
            var pts = new List<Point2D>();
            foreach (var f in fingers)
            {
                var fd = JsInterop.AsDict(f);
                if (fd != null) pts.Add(pick(fd));
            }
            if (pts.Count < 2) return;

            ctx.BeginPath();
            ctx.MoveTo(pts[0].X, pts[0].Y);
            for (var i = 1; i < pts.Count; i++)
            {
                var prev = pts[i - 1];
                ctx.QuadraticCurveTo((prev.X + pts[i].X) * 0.5f, (prev.Y + pts[i].Y) * 0.5f, pts[i].X, pts[i].Y);
            }
            ctx.Stroke();
        }

        RowArc(f => ExtractPoint(f["knuckle"]));
        for (var s = 0; s < 3; s++)
        {
            var step = s;
            RowArc(f => ExtractPoint((f["joints"] as IList)?[step]));
        }

        // The thumb muscle, which is what attaches the thumb to the palm.
        var thenar = JsInterop.AsDict(hand["thenar"]);
        if (thenar != null)
        {
            var tw = ExtractPoint(thenar["wrist"]);
            var tc = ExtractPoint(thenar["crest"]);
            var tb = ExtractPoint(thenar["base"]);
            var tweb = ExtractPoint(thenar["web"]);
            ctx.BeginPath();
            ctx.MoveTo(tw.X, tw.Y);
            ctx.QuadraticCurveTo(tc.X, tc.Y, tb.X, tb.Y);
            ctx.LineTo(tweb.X, tweb.Y);
            ctx.Stroke();
        }

        // Graphite: the digits themselves.
        ctx.StrokeStyle = graphite;
        ctx.LineWidth = lineWidth * 1.4f;

        void DrawDigit(Point2D start, IList joints)
        {
            ctx.BeginPath();
            ctx.MoveTo(start.X, start.Y);
            foreach (var j in joints)
            {
                var p = ExtractPoint(j);
                ctx.LineTo(p.X, p.Y);
            }
            ctx.Stroke();

            ctx.BeginPath();
            ctx.Arc(start.X, start.Y, jointR, 0f, MathF.PI * 2f);
            ctx.Stroke();
            foreach (var j in joints)
            {
                var p = ExtractPoint(j);
                ctx.BeginPath();
                ctx.Arc(p.X, p.Y, jointR * 0.8f, 0f, MathF.PI * 2f);
                ctx.Stroke();
            }
        }

        foreach (var f in fingers)
        {
            var fd = JsInterop.AsDict(f);
            var joints = fd?["joints"] as IList;
            if (fd != null && joints != null) DrawDigit(ExtractPoint(fd["knuckle"]), joints);
        }

        var thumbJoints = thumb["joints"] as IList;
        if (thumbJoints != null) DrawDigit(ExtractPoint(thumb["base"]), thumbJoints);

        ctx.Restore();
    }

    /// <summary>
    /// Renders the hand as Loomis's block forms (Plate 78): the palm as a slab, the thumb muscle as a
    /// wedge off it, and every phalanx as its own tapering box.
    /// </summary>
    /// <remarks>Exposed to scripts, so members are PascalCase here and camelCase in JS.</remarks>
    /// <summary>
    /// Draws Plate 78's block forms and <b>returns them</b>: <c>silhouette</c> (the whole hand as one
    /// contour), <c>parts</c> (a <see cref="CanvasPath"/> per mass, named anatomically) and
    /// <c>bounds</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same gap the mannequin had, at a tenth the size: this paints a <i>construction sheet</i> —
    /// a palm slab, a thenar wedge and eleven separate boxes, each with its own outline — where a
    /// drawn hand is one shape. At the scale a hand actually appears in a panel, perhaps fifteen
    /// pixels, the interior outlines are noise and the silhouette is the whole of the drawing.
    /// </para>
    /// <para>
    /// Part names follow the bones: <c>palm</c>, <c>thenar</c>, then
    /// <c>indexProximal</c> / <c>indexMiddle</c> / <c>indexDistal</c> and the same for
    /// <c>middle</c>, <c>ring</c> and <c>little</c>, plus <c>thumbProximal</c> and
    /// <c>thumbDistal</c>. A hand posed with a different joint count keeps the same scheme,
    /// falling back to <c>Segment{n}</c> past the named three.
    /// </para>
    /// <para>A script that ignores the return value behaves exactly as before.</para>
    /// </remarks>
    public Dictionary<string, object?> DrawHandSolid(CanvasRenderingContext2D ctx, object handObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(handObj) is not IDictionary hand) return [];

        var opt = JsInterop.AsDict(options);
        var fillColor = opt?["fillColor"]?.ToString() ?? "#dbe7f2";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#9bbcd9";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.6f;

        var palm = JsInterop.AsDict(hand["palm"]);
        var fingers = hand["fingers"] as IList;
        var thumb = JsInterop.AsDict(hand["thumb"]);
        if (palm == null || fingers == null || thumb == null) return [];

        var parts = new Dictionary<string, object?>();
        CanvasPath? whole = null;

        void Keep(string name, CanvasPath path)
        {
            parts[name] = path;
            whole = whole == null ? path : whole.Union(path);
        }

        ctx.Save();
        ctx.StrokeStyle = strokeColor;
        ctx.LineWidth = strokeWidth;

        // A phalanx is a box: a quad about the segment, narrowing toward the tip.
        CanvasPath? Box(Point2D a, Point2D b, float wA, float wB, string fill)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.01f) return null;
            var nx = -dy / len;
            var ny = dx / len;

            var box = new CanvasPath();
            box.MoveTo(a.X + nx * wA * 0.5f, a.Y + ny * wA * 0.5f);
            box.LineTo(b.X + nx * wB * 0.5f, b.Y + ny * wB * 0.5f);
            box.LineTo(b.X - nx * wB * 0.5f, b.Y - ny * wB * 0.5f);
            box.LineTo(a.X - nx * wA * 0.5f, a.Y - ny * wA * 0.5f);
            box.ClosePath();

            ctx.FillStyle = fill;
            ctx.Fill(box);
            ctx.Stroke(box);
            return box;
        }

        // 1. The palm slab.
        var wi = ExtractPoint(palm["wristInner"]);
        var wo = ExtractPoint(palm["wristOuter"]);
        var ki = ExtractPoint(palm["knuckleInner"]);
        var ko = ExtractPoint(palm["knuckleOuter"]);
        var palmSlab = new CanvasPath();
        palmSlab.MoveTo(wi.X, wi.Y);
        palmSlab.LineTo(ki.X, ki.Y);
        palmSlab.LineTo(ko.X, ko.Y);
        palmSlab.LineTo(wo.X, wo.Y);
        palmSlab.ClosePath();
        ctx.FillStyle = fillColor;
        ctx.Fill(palmSlab);
        ctx.Stroke(palmSlab);
        Keep("palm", palmSlab);

        // 2. The thumb muscle - the big mass Loomis calls by far the most important in the hand - as a
        // wedge from the wrist out to the thumb's base.
        var thumbBase = ExtractPoint(thumb["base"]);
        var thenar = JsInterop.AsDict(hand["thenar"]);
        var thenarWedge = new CanvasPath();
        if (thenar != null)
        {
            var thenarWrist = ExtractPoint(thenar["wrist"]);
            var thenarCrest = ExtractPoint(thenar["crest"]);
            var thenarWeb = ExtractPoint(thenar["web"]);
            thenarWedge.MoveTo(thenarWrist.X, thenarWrist.Y);
            thenarWedge.QuadraticCurveTo(thenarCrest.X, thenarCrest.Y, thumbBase.X, thumbBase.Y);
            thenarWedge.LineTo(thenarWeb.X, thenarWeb.Y);
        }
        else
        {
            thenarWedge.MoveTo(wo.X, wo.Y);
            thenarWedge.LineTo(thumbBase.X, thumbBase.Y);
            thenarWedge.LineTo(ko.X, ko.Y);
        }
        thenarWedge.ClosePath();
        ctx.FillStyle = shadowColor;
        ctx.Fill(thenarWedge);
        ctx.Stroke(thenarWedge);
        Keep("thenar", thenarWedge);

        // 3. Three boxes per finger, narrowing toward the tip.
        foreach (var f in fingers)
        {
            var fd = JsInterop.AsDict(f);
            var joints = fd?["joints"] as IList;
            if (fd == null || joints == null) continue;

            var name = fd["name"]?.ToString() ?? "finger";
            var w = Convert.ToSingle(fd["width"], CultureInfo.InvariantCulture);
            var from = ExtractPoint(fd["knuckle"]);
            for (var s = 0; s < joints.Count; s++)
            {
                var to = ExtractPoint(joints[s]);
                var box = Box(from, to, w * (1f - s * 0.13f), w * (1f - (s + 1) * 0.13f), s % 2 == 0 ? fillColor : shadowColor);
                if (box != null) Keep(name + PhalanxName(s, joints.Count), box);
                from = to;
            }
        }

        // 4. Two boxes for the thumb.
        var tw = Convert.ToSingle(thumb["width"], CultureInfo.InvariantCulture);
        var tJoints = thumb["joints"] as IList;
        if (tJoints != null)
        {
            var from = thumbBase;
            for (var s = 0; s < tJoints.Count; s++)
            {
                var to = ExtractPoint(tJoints[s]);
                var box = Box(from, to, tw * (1f - s * 0.15f), tw * (1f - (s + 1) * 0.15f), s % 2 == 0 ? fillColor : shadowColor);
                if (box != null) Keep("thumb" + PhalanxName(s, tJoints.Count), box);
                from = to;
            }
        }

        ctx.Restore();

        var silhouette = whole == null ? new CanvasPath() : whole.Simplify();
        var box2 = silhouette.Path.Bounds;
        return new Dictionary<string, object?>
        {
            ["silhouette"] = silhouette,
            ["parts"] = parts,
            ["bounds"] = new Dictionary<string, object?>
            {
                ["x"] = box2.Left, ["y"] = box2.Top, ["width"] = box2.Width, ["height"] = box2.Height,
                ["x2"] = box2.Right, ["y2"] = box2.Bottom,
                ["cx"] = box2.MidX, ["cy"] = box2.MidY
            }
        };
    }

    /// <summary>Bone name for the n-th segment out from a knuckle, so parts read anatomically.</summary>
    static string PhalanxName(int index, int count) => count == 2
        ? index switch { 0 => "Proximal", 1 => "Distal", _ => "Segment" + index }
        : index switch { 0 => "Proximal", 1 => "Middle", 2 => "Distal", _ => "Segment" + index };
    #endregion

    #region Parametric head
    /// <summary>One parameter's value, or 0 when it was not given. Absent means canon, never a default.</summary>
    static float Param(IDictionary? opt, string name)
    {
        if (opt is null) return 0f;
        foreach (DictionaryEntry entry in opt)
        {
            if (string.Equals(entry.Key?.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                try { return Convert.ToSingle(entry.Value, CultureInfo.InvariantCulture); }
                catch (Exception) { return 0f; }   // a non-numeric value is canon, not a crash mid-draw
            }
        }

        return 0f;
    }

    /// <summary>Refuses a misspelled parameter by name, rather than silently drawing the canon.</summary>
    /// <param name="noun">
    /// What the caller passed, singular — pluralised for the message. Shared with
    /// <see cref="CreateHeadGeometry"/>, whose dictionary is options rather than face parameters, so
    /// the error has to be able to say which.
    /// </param>
    static void RefuseUnknownHeadParameters(IDictionary? opt, string[] accepted, string noun = "Head parameter")
    {
        if (opt is null) return;

        var unknown = new List<string>();
        foreach (DictionaryEntry entry in opt)
        {
            var name = entry.Key?.ToString();
            if (name is not null && !accepted.Contains(name, StringComparer.OrdinalIgnoreCase)) unknown.Add(name);
        }

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"{noun}{(unknown.Count > 1 ? "s" : string.Empty)} not recognised: "
                + $"{string.Join(", ", unknown)}. Accepted: {string.Join(", ", accepted)}.");
        }
    }

    /// <summary>A head copied deeply enough that displacing the copy cannot reach the original.</summary>
    /// <remarks>
    /// Two levels is the whole structure: the head's own keys, and the nested groups (<c>unit</c>,
    /// <c>nearEye</c>, <c>noseWedge</c>, <c>mouthGuides</c>, <c>jaw</c>, and the points inside them).
    /// A shallow copy would share those inner dictionaries, so parameterising a head would mutate
    /// the canon it came from — and the second call with the same head would start from the first
    /// call's face, which is exactly the consistency this exists to provide, destroyed.
    /// </remarks>
    /// <summary>
    /// The muscle actions this construction can actually show, by Ekman &amp; Friesen's numbering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The set is bounded by the construction rather than by the coding system</b>: a unit whose
    /// landmark this head does not have would bind, run, and change nothing, which reads as the
    /// parameter having no effect.
    /// </para>
    /// <para>
    /// <b>AU2 was the sharpest absence and is now here, because the head grew the landmarks for it.</b>
    /// <see cref="CreateLoomisHead"/> carries <c>nearBrow</c> and <c>farBrow</c> as inner, peak and
    /// outer stations, so an inner-only lift and an outer arch are two different displacements rather
    /// than one under two names — which is the difference between worry and surprise, and between
    /// fear and surprise in <see cref="ExpressionTuples"/>.
    /// </para>
    /// <para>
    /// <b>The same change fixed a defect the brow units had all along, and it was not a subtle one.</b>
    /// AU1 and AU4 used to displace <c>head["brow"]</c> — which is not a drawn eyebrow at all but the
    /// ball's equator, the landmark <see cref="CreateHeadGeometry"/> takes the cranium's centre and
    /// radius from. So raising the brows shrank the skull and lowering them grew it. Measured on a
    /// 240px head: <c>AU1</c> at full weight took the silhouette from <b>208.5px wide to 188.9</b>,
    /// and <c>AU4</c> took it to <b>225.4</b> — a <b>36.5px swing in head width across one
    /// expression range</b>, on a face meant to be the same character from panel to panel. It is
    /// invisible in any single render, since a head with raised brows merely looks like a slightly
    /// narrower head; only two panels side by side would show it, and by then it reads as the
    /// toolkit being inconsistent. The brow units now move the brow stations and never that
    /// landmark, which is the same rule Manual 23 states for the jaw: <b>a parameter that moves a
    /// landmark the construction uses as an attachment will break the silhouette.</b>
    /// </para>
    /// <para>
    /// <b>What is still deliberately absent, and why.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><b>AU6 (Cheek Raiser).</b> <c>createHeadGeometry</c> composes a cheek as of 2026-09-19,
    /// but it is <em>derived</em> from the ear and the jaw angle rather than being a landmark, so
    /// there is nothing here for a unit to displace; and <c>drawComicEye</c> draws no crow's feet, so
    /// the Duchenne marker still has nowhere to land.</item>
    /// <item><b>AU9/AU10 (Nose Wrinkler, Upper Lip Raiser).</b> The nose wedge has landmarks but the
    /// upper lip is a single <c>upperLipY</c>, so a sneer would read as the whole lip rising.
    /// Reachable later; not honest yet.</item>
    /// <item><b>AU17 (Chin Raiser).</b> The mentalis bulge is a surface change rather than a landmark
    /// move, and nothing downstream draws chin texture.</item>
    /// </list>
    /// <para>
    /// Each unit <b>adds</b> its displacement, so folding several is order-independent. Magnitudes
    /// are fractions of the head's own height, so a unit means the same thing at any scale — the
    /// same discipline <see cref="CreateParametricHead"/> follows, and for the same reason.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, Action<Dictionary<string, object?>, float, float, FaceSide>> ActionUnits =
        new(StringComparer.Ordinal)
        {
            // Frontalis, Pars Medialis. Loomis's wrinkle muscles at the inner brow: worry, pleading.
            // The inner end carries it, the peak follows at less than half, and the tail does not
            // move at all - which is the whole distinction from AU2 below.
            ["AU1"] = (head, w, h, s) => MoveBrows(head, 0f, -0.045f * h * w, 0f, -0.018f * h * w, 0f, 0f, s),

            // Frontalis, Pars Lateralis. The outer arch: the tail and the peak rise and the inner end
            // stays. **Unreachable before the brow carried stations**, because with one centre point
            // this and AU1 were the same displacement under two names - and the difference between
            // them is the difference between worry and surprise.
            ["AU2"] = (head, w, h, s) => MoveBrows(head, 0f, 0f, 0f, -0.030f * h * w, 0f, -0.040f * h * w, s),

            // Depressor Glabellae / Supercilii / Corrugator. Loomis has the unhappy group pulling the
            // inside corner of the brow down into a frown, which is this unit from the other side.
            //
            // **It knits as well as lowers, and the inward move is what stops it cancelling AU1.**
            // Corrugator draws the heads of the brows together; with one landmark that was a pure
            // vertical, so AU1 and AU4 subtracted to nothing and `sadness` had to omit AU4 to show
            // anything at all. Here AU1 lifts only the inner end while this lowers all three, so the
            // pair leaves the brow **oblique** - inner up, tail down - which is the sad brow itself
            // rather than an absence of one.
            ["AU4"] = (head, w, h, s) => MoveBrows(head, -0.020f * h * w, 0.030f * h * w,
                                                        0f, 0.030f * h * w,
                                                        0f, 0.030f * h * w, s),

            // Levator Palpebrae Superioris - the lid opens. Reachable only since drawComicEye began
            // reading `height`; before that this unit would have bound and drawn nothing.
            ["AU5"] = (head, w, h, s) => ScaleAperture(head, 0.020f * h * w, s),

            // Orbicularis Oculi, Pars Palpebralis - the lid narrows.
            ["AU7"] = (head, w, h, s) => ScaleAperture(head, -0.018f * h * w, s),

            // Zygomatic Major. Loomis's "happy muscles", which pull the corners OUT and diagonally
            // UP - the diagonal is his, and a corner lifted straight up reads as a smirk.
            ["AU12"] = (head, w, h, s) => MoveMouthCorners(head, 0.018f * h * w, -0.032f * h * w, s),

            // Triangularis. The corners drop and do not spread; Loomis's leer is the round-cornered
            // failure of this one.
            ["AU15"] = (head, w, h, s) => MoveMouthCorners(head, 0f, 0.028f * h * w, s),

            // Masseter and the pterygoids relaxed. The one unit that reaches the silhouette, so a
            // dropped jaw changes the head's outline rather than only its marks.
            ["AU26"] = (head, w, h, _) => DropJaw(head, 0.055f * h * w),
        };

    /// <summary>
    /// Displaces both brows station by station, with <paramref name="innerDx"/> read as <b>inward</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Inward rather than leftward, because a face has two sides.</b> A knit draws the heads of
    /// the brows toward each other, so the same unit has to move the near brow one way and the far
    /// brow the other. Taking the displacement as signed screen-x would knit one brow and spread the
    /// other, which renders as a face with one raised and one dropped inner corner — a drawing fault
    /// rather than an expression, and one nothing downstream could report.
    /// </para>
    /// <para>
    /// <b>The axis is the old <c>brow</c> landmark</b>, which is exactly what it is for: the head's
    /// own facial centre at the brow line, left where the construction put it. This call never moves
    /// it — see <see cref="ActionUnits"/> for why that matters more than it sounds.
    /// </para>
    /// </remarks>
    static void MoveBrows(Dictionary<string, object?> head,
                          float innerDx, float innerDy, float peakDx, float peakDy, float outerDx, float outerDy,
                          FaceSide side = FaceSide.Both)
    {
        var axis = ExtractPoint(head.TryGetValue("brow", out var b) ? b : null).X;

        foreach (var group in new[] { "nearBrow", "farBrow" })
        {
            if (!SideCovers(side, group == "nearBrow")) continue;
            if (head[group] is not Dictionary<string, object?> brow) continue;

            // Which way "outward" runs on this side, taken from the brow's own geometry rather than
            // from the group's name: a head mirrored or rebuilt at another yaw stays correct.
            float outward = MathF.Sign(ExtractPoint(brow["outer"]).X - axis);
            if (outward == 0f) outward = 1f;

            foreach (var (key, dx, dy) in new[] { ("inner", innerDx, innerDy),
                                                  ("peak", peakDx, peakDy),
                                                  ("outer", outerDx, outerDy) })
            {
                if (!brow.ContainsKey(key)) continue;
                var p = ExtractPoint(brow[key]);
                brow[key] = ToDict(new Point2D(p.X + (outward * dx), p.Y + dy));
            }
        }
    }

    /// <summary>
    /// The lowest drawn brow station on the near side, or the construction landmark when a head
    /// carries no stations.
    /// </summary>
    /// <remarks>
    /// The lowest rather than an average, because the ladder asks which landmark crosses the one
    /// below it and an averaged brow would report a face as ordered while its inner corners sat in
    /// the eyes. The fallback keeps a hand-built or legacy head measurable rather than reporting it
    /// as broken at <c>y = 0</c>.
    /// </remarks>
    static float DrawnBrowY(IDictionary head)
    {
        var construction = ExtractPoint(head["brow"]).Y;
        if (JsInterop.AsDict(head["nearBrow"]) is not IDictionary brow) return construction;

        var lowest = float.MinValue;
        foreach (var key in new[] { "inner", "peak", "outer" })
        {
            if (!brow.Contains(key)) continue;
            lowest = MathF.Max(lowest, ExtractPoint(brow[key]).Y);
        }

        return lowest == float.MinValue ? construction : lowest;
    }

    /// <summary>Adds an offset to a top-level landmark.</summary>
    static void NudgePoint(Dictionary<string, object?> head, string key, float dx, float dy)
    {
        var p = ExtractPoint(head[key]);
        head[key] = ToDict(new Point2D(p.X + dx, p.Y + dy));
    }

    /// <summary>Opens or narrows both lids, which is a change to each eye's own aperture.</summary>
    static void ScaleAperture(Dictionary<string, object?> head, float delta, FaceSide side = FaceSide.Both)
    {
        foreach (var group in new[] { "nearEye", "farEye" })
        {
            if (!SideCovers(side, group == "nearEye")) continue;
            if (head[group] is not Dictionary<string, object?> eye) continue;
            if (!eye.TryGetValue("height", out var v) || v is null) continue;

            // Floored rather than allowed negative: a lid past shut is not a lid, and a negative
            // aperture would invert the eyelid curve into a shape nothing in the face explains.
            var next = Convert.ToSingle(v, CultureInfo.InvariantCulture) + delta;
            eye["height"] = MathF.Max(0f, next);
        }
    }

    /// <summary>Moves both mouth corners outward and vertically, each away from the mouth's centre.</summary>
    /// <remarks>
    /// The outward direction is read off the corners' own positions rather than assumed from their
    /// names, for the reason <c>MoveEyes</c> gives: at yaw the two are not symmetric about the axis,
    /// and "left" in the dictionary is a name rather than a guarantee about x.
    /// </remarks>
    static void MoveMouthCorners(Dictionary<string, object?> head, float spread, float lift,
                                 FaceSide side = FaceSide.Both)
    {
        if (head["mouthGuides"] is not Dictionary<string, object?> mouth) return;

        var centre = ExtractPoint(mouth["center"]);
        foreach (var key in new[] { "leftCorner", "rightCorner" })
        {
            // `rightCorner` is the +x corner, which is the side `nearEye` and `nearBrow` are on —
            // the construction names the mouth by hand and the eyes by depth, so the mapping has to
            // be stated somewhere rather than inferred from the names.
            if (!SideCovers(side, key == "rightCorner")) continue;
            if (!mouth.ContainsKey(key)) continue;
            var p = ExtractPoint(mouth[key]);
            var away = MathF.Sign(p.X - centre.X);
            if (away == 0) away = key == "rightCorner" ? 1 : -1;
            mouth[key] = ToDict(new Point2D(p.X + (away * spread), p.Y + lift));
        }
    }

    /// <summary>Drops the jaw: the lower lip opens and the chin stations follow it down.</summary>
    /// <remarks>
    /// The chin moves because a jaw drop is a change to the head's <i>outline</i>, not only to the
    /// marks inside it — <c>createHeadGeometry</c> builds its jaw polygon through these stations, so
    /// an open mouth that left them alone would draw a dropped lip inside a closed face.
    /// </remarks>
    static void DropJaw(Dictionary<string, object?> head, float drop)
    {
        if (head["mouthGuides"] is Dictionary<string, object?> mouth
            && mouth.TryGetValue("lowerLipY", out var lower) && lower is not null)
        {
            mouth["lowerLipY"] = Convert.ToSingle(lower, CultureInfo.InvariantCulture) + drop;
        }

        NudgePoint(head, "chin", 0f, drop * 0.55f);
        if (head["jaw"] is not Dictionary<string, object?> jaw) return;

        foreach (var key in new[] { "chin", "chinNear", "chinFar", "nearStation", "farStation" })
        {
            if (!jaw.ContainsKey(key)) continue;
            var p = ExtractPoint(jaw[key]);
            // The stations behind the chin move less: the jaw hinges rather than translating.
            var share = key.StartsWith("chin", StringComparison.Ordinal) ? 0.55f : 0.25f;
            jaw[key] = ToDict(new Point2D(p.X, p.Y + (drop * share)));
        }
    }

    /// <summary>
    /// The six named expressions as Action Unit weights. <b>Ours, tuned by eye.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No source supplies these numbers, and that is the first thing to know about them.</b>
    /// Loomis gives muscle directions and a relaxed/contracted table and declines to tabulate the
    /// emotions at all; Ekman &amp; Friesen's 1976 code scores whether a unit is present, slight or
    /// strong, and never names an emotion. So these are the studio's reading of Manual 08 §4's muscle
    /// descriptions, constrained to the seven units this construction can draw, and they are meant to
    /// be argued with rather than trusted.
    /// </para>
    /// <para>
    /// <b>How well each is served differs a lot, and pretending otherwise would be the failure this
    /// replaces.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><b>joy</b> and <b>sadness</b> are well served: their defining muscles — Zygomatic Major
    /// and Triangularis — are both implemented, and the mouth is where both live.</item>
    /// <item><b>anger</b> is good at the brow and approximate at the mouth: Loomis has it squaring,
    /// and there is no unit for that, so AU15 stands in.</item>
    /// <item><b>fear</b> and <b>surprise</b> are separated by AU4, which is what separates them in
    /// life: fear's frontalis lifts against a knitting corrugator, giving it a strained flat brow,
    /// where surprise arches cleanly with no corrugator at all. They are the most confusable pair in
    /// the literature even on real faces — but they are now two faces here rather than one at two
    /// strengths, which they were while the head had a single <c>brow</c> landmark.</item>
    /// <item><b>disgust</b> is the worst served and should be treated as a placeholder. Its defining
    /// action is Levator Labii Superioris curling the upper lip and wrinkling the nose — AU9 and
    /// AU10, neither implemented, because the upper lip is one <c>upperLipY</c> and would rise
    /// whole.</item>
    /// </list>
    /// <para>
    /// <b>sadness carries AU1 and AU4 together, which is the canonical oblique sad brow.</b> It
    /// omitted AU4 until the head had brow stations, because on one point the two cancelled exactly
    /// and the result was a face with no brow movement at all — close to what the implementation
    /// before that shipped. AU1 now lifts only the inner end while AU4 lowers all three, so the pair
    /// slants the brow instead of erasing itself.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, Dictionary<string, float>> ExpressionTuples =
        new(StringComparer.Ordinal)
        {
            // Zygomatic major pulls the corners out and up; the eye narrowing is the part of Loomis's
            // cheek-puff this construction can actually show.
            ["joy"] = new(StringComparer.Ordinal) { ["AU12"] = 0.85f, ["AU7"] = 0.25f },

            // Corrugator hard down and knitting, eyes narrowed. The mouth "squares" in Loomis and
            // cannot here.
            ["anger"] = new(StringComparer.Ordinal) { ["AU4"] = 0.90f, ["AU7"] = 0.60f, ["AU15"] = 0.25f },

            // **Fear and surprise are now two expressions rather than one at two strengths, and AU4
            // is the whole of the difference.** Fear knits the brow while raising it - the frontalis
            // lifts and the corrugator fights it, which is what gives fear its strained flat brow -
            // where surprise arches cleanly with no corrugator at all. With a single brow landmark
            // that distinction could not be drawn, so the two were separated only by how much AU1
            // and AU5 each carried, and the comment here read "as fear, weighted toward the brow".
            ["fear"] = new(StringComparer.Ordinal)
            {
                ["AU1"] = 0.80f, ["AU2"] = 0.50f, ["AU4"] = 0.60f, ["AU5"] = 0.80f, ["AU26"] = 0.45f
            },

            // **Sadness carries AU4 again, which is the oblique brow it is named for.** The tuple used
            // to omit it with the comment "it would cancel AU1" - true when both were one vertical on
            // one point, and the reason the canonical sad brow was the one expression this set could
            // not draw. AU1 now lifts only the inner end and AU4 lowers all three, so the pair leaves
            // the brow slanting up toward the nose instead of leaving it flat.
            ["sadness"] = new(StringComparer.Ordinal) { ["AU1"] = 0.70f, ["AU4"] = 0.40f, ["AU15"] = 0.75f },

            // The clean arch: both frontalis parts, no corrugator. Weighted toward the brow and the
            // jaw rather than the lids, which is what separates it from fear at the eyes as well.
            ["surprise"] = new(StringComparer.Ordinal)
            {
                ["AU1"] = 0.90f, ["AU2"] = 0.90f, ["AU5"] = 0.65f, ["AU26"] = 0.75f
            },

            // A placeholder until AU9/AU10 exist. Reads as a sour narrowing rather than a sneer.
            ["disgust"] = new(StringComparer.Ordinal) { ["AU4"] = 0.40f, ["AU7"] = 0.45f, ["AU15"] = 0.50f },
        };

    /// <summary>What a head's leaves mean, which is what says how each is normalised.</summary>
    private enum HeadValue { Point, Length, CoordX, CoordY }

    /// <summary>
    /// Every blendable value in a head, by path, with what kind of measurement it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Enumerated rather than inferred, because the three kinds normalise differently</b> and
    /// nothing in a key's name reliably says which it is: <c>width</c> is a length, <c>eyeLineY</c>
    /// is a coordinate, and guessing from a trailing letter would work until the first landmark
    /// named otherwise. The set is closed - one function builds every head - so enumerating it costs
    /// nothing and buys a refusal for anything unrecognised.
    /// </para>
    /// <para>
    /// <c>unit</c> and <c>origin</c> are deliberately absent. They are the frame the blend is
    /// measured in, so blending them would move the ruler along with the thing being measured; they
    /// are copied from the base head instead.
    /// </para>
    /// </remarks>
    private static readonly (string Path, HeadValue Kind)[] HeadSchema =
    [
        ("crown", HeadValue.Point),
        ("hairline", HeadValue.Point),
        ("brow", HeadValue.Point),
        ("noseBase", HeadValue.Point),
        ("mouthCenter", HeadValue.Point),
        ("chin", HeadValue.Point),
        ("eyeLineY", HeadValue.CoordY),

        ("nearEye.inner", HeadValue.Point),
        ("nearEye.outer", HeadValue.Point),
        ("nearEye.center", HeadValue.Point),
        ("nearEye.width", HeadValue.Length),
        ("nearEye.height", HeadValue.Length),

        ("farEye.inner", HeadValue.Point),
        ("farEye.outer", HeadValue.Point),
        ("farEye.center", HeadValue.Point),
        ("farEye.width", HeadValue.Length),
        ("farEye.height", HeadValue.Length),

        ("nearBrow.inner", HeadValue.Point),
        ("nearBrow.peak", HeadValue.Point),
        ("nearBrow.outer", HeadValue.Point),
        ("nearBrow.thickness", HeadValue.Length),

        ("farBrow.inner", HeadValue.Point),
        ("farBrow.peak", HeadValue.Point),
        ("farBrow.outer", HeadValue.Point),
        ("farBrow.thickness", HeadValue.Length),

        ("noseWedge.bridgeTop", HeadValue.Point),
        ("noseWedge.apex", HeadValue.Point),
        ("noseWedge.underNose", HeadValue.Point),
        ("noseWedge.nearNostril", HeadValue.Point),

        ("mouthGuides.center", HeadValue.Point),
        ("mouthGuides.leftCorner", HeadValue.Point),
        ("mouthGuides.rightCorner", HeadValue.Point),
        ("mouthGuides.upperLipY", HeadValue.CoordY),
        ("mouthGuides.lowerLipY", HeadValue.CoordY),

        ("jaw.ear", HeadValue.Point),
        ("jaw.angle", HeadValue.Point),
        ("jaw.nearAngle", HeadValue.Point),
        ("jaw.farStation", HeadValue.Point),
        ("jaw.nearStation", HeadValue.Point),
        ("jaw.chinFar", HeadValue.Point),
        ("jaw.chinNear", HeadValue.Point),
        ("jaw.chin", HeadValue.Point),
        ("jaw.cheekApex", HeadValue.Point),

        ("temporalOval.cx", HeadValue.CoordX),
        ("temporalOval.cy", HeadValue.CoordY),
        ("temporalOval.rx", HeadValue.Length),
        ("temporalOval.ry", HeadValue.Length),
    ];

    /// <summary>The frame a blend is measured in: the head's origin and its height.</summary>
    static (Point2D Origin, float H) HeadFrame(IDictionary head, string which)
    {
        var unit = JsInterop.AsDict(head["unit"]);
        var origin = JsInterop.AsDict(head["origin"]);
        var h = Num(unit, "H", float.NaN);
        if (!float.IsFinite(h) || h <= 0f)
        {
            throw new ArgumentException(
                $"{which} has no usable unit.H, so there is no scale to measure a blend against. "
                + "Pass a head from createLoomisHead.", which);
        }

        return (new Point2D(Num(origin, "x", 0f), Num(origin, "y", 0f)), h);
    }

    /// <summary>How far the head is turned, as <c>cos(yaw)</c>, read off its own far eye.</summary>
    static float HeadTurn(IDictionary head)
    {
        var eyeW = Num(JsInterop.AsDict(head["unit"]), "eyeW", 0f);
        if (eyeW <= 0f) return 1f;
        return Math.Clamp(Num(JsInterop.AsDict(head["farEye"]), "width", eyeW) / eyeW, 0f, 1f);
    }

    /// <summary>Walks a dotted path to the group holding its last segment.</summary>
    static IDictionary? PathParent(IDictionary head, string path, out string leaf)
    {
        var cut = path.IndexOf('.', StringComparison.Ordinal);
        if (cut < 0)
        {
            leaf = path;
            return head;
        }

        leaf = path[(cut + 1)..];
        return JsInterop.AsDict(head[path[..cut]]);
    }

    static Point2D SchemaPoint(IDictionary head, string path, string which)
    {
        var parent = PathParent(head, path, out var leaf);
        var value = parent?.Contains(leaf) == true ? parent[leaf] : null;
        if (JsInterop.AsDict(value) is not IDictionary p || !p.Contains("x") || !p.Contains("y"))
        {
            throw new ArgumentException(
                $"{which} has no point at '{path}', so there is nothing to blend it against. Every "
                + "head in a blend must carry the same landmarks; build them all with "
                + "createLoomisHead.", which);
        }

        return ExtractPoint(p);
    }

    static float SchemaScalar(IDictionary head, string path, string which)
    {
        var parent = PathParent(head, path, out var leaf);
        var value = parent?.Contains(leaf) == true ? parent[leaf] : null;
        if (value is null || !float.IsFinite(Convert.ToSingle(value, CultureInfo.InvariantCulture)))
        {
            throw new ArgumentException(
                $"{which} has no number at '{path}', so there is nothing to blend it against. Every "
                + "head in a blend must carry the same landmarks; build them all with "
                + "createLoomisHead.", which);
        }

        return Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }

    static void WriteSchemaPoint(Dictionary<string, object?> head, string path, Point2D value)
    {
        var cut = path.IndexOf('.', StringComparison.Ordinal);
        if (cut < 0) head[path] = ToDict(value);
        else SetPoint(head, path[..cut], path[(cut + 1)..], value);
    }

    static void WriteSchemaScalar(Dictionary<string, object?> head, string path, float value)
    {
        var cut = path.IndexOf('.', StringComparison.Ordinal);
        if (cut < 0) { head[path] = value; return; }
        if (head[path[..cut]] is Dictionary<string, object?> group) group[path[(cut + 1)..]] = value;
    }

    /// <summary>The unparameterised head this one is a variation of, at its own size and turn.</summary>
    /// <remarks>
    /// Rebuilt from the construction rather than carried on the head, so that a caller who never
    /// asked for a caricature pays nothing for one. The turn is recovered rather than remembered,
    /// which is the same route <see cref="CreateHeadGeometry"/> takes and for the same reason: yaw
    /// is not stored on the head, and re-deriving it is exact enough for a reference face.
    /// </remarks>
    static Dictionary<string, object?> CanonFor(IDictionary head)
    {
        var (origin, h) = HeadFrame(head, "headObj");
        var yaw = MathF.Acos(Math.Clamp(HeadTurn(head), 0f, 1f)) * 180f / MathF.PI;
        return new ConstructiveDrawingToolkit().CreateLoomisHead(origin.X, origin.Y, h, yaw);
    }

    /// <summary>The canon's own <c>height / width</c> for an eye, and the aperture's shape at it.</summary>
    /// <remarks>
    /// Kept as ratios of the upper deflection rather than as independent fractions of eye width, so
    /// that moving <c>height</c> opens and closes the whole aperture coherently instead of only
    /// lifting its top. At <see cref="CanonOpenness"/> these reproduce the constants that were
    /// written inline before the aperture was readable — 0.45, 0.40, 0.25 and 0.15 of the drawn
    /// width — which is what makes the change invisible to every script that does not use it.
    /// </remarks>
    private const float CanonOpenness = 0.45f;
    private const float LidCrest = 0.40f / 0.45f;
    private const float LidFloor = 0.25f / 0.45f;
    private const float LowerLidStart = 0.15f / 0.45f;

    /// <summary>
    /// The iris radius as a fraction of the eye's drawn width. <b>A comic iris, not a real one.</b>
    /// </summary>
    /// <remarks>
    /// A real iris measures <b>0.19</b> of the eye width in this construction — its diameter is
    /// 0.1886 of the interpupillary distance on MediaPipe's canonical face model (Apache 2.0, 468
    /// vertices; measured off the iris ring vertices, nothing inferred), and the pupils here sit one
    /// eye-width either side of the facial axis, so IPD is exactly twice the drawn width and the two
    /// ratios coincide. <b>0.32 is therefore 1.70× life size</b>, which is the comic idiom rather
    /// than a mistake — Manual 23 §1 measures the comic head as narrower relative to its eye than
    /// Loomis's, and a larger iris is the same observation from the other side. Kept as the default
    /// so that no face already drawn changes; pass <c>irisRatio: 0.19</c> for a naturalistic eye.
    /// </remarks>
    private const float CanonIrisRatio = 0.32f;

    /// <summary>
    /// How much lighter a far feature is drawn, taken from <see cref="DrawComicEye"/>'s own lid
    /// weights (2.6 against 3.8) so a far brow and the far eye under it agree.
    /// </summary>
    private const float FarFeatureWeight = 2.6f / 3.8f;

    /// <summary>
    /// Every ink weight on a feature, as a fraction of that feature's own measured size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These were absolute pixel counts until 2026-09-19, and that is a defect rather than a
    /// simplification.</b> The geometry has always scaled with head height and the ink did not, so a
    /// feature was correct at exactly one head size: at a 600px portrait the nose came back as a
    /// hairline with a 3.5px dot for a nostril, and at a 60px long shot the same constants read as a
    /// blob. Nothing errored, and the failure looks like a styling choice.
    /// </para>
    /// <para>
    /// <b>The constants they replace were not arbitrary — they were Manual 03's tier table frozen at
    /// one scale.</b> 2.4px is inside Tier 2 (2.0–3.0px, whose examples are "eyelid creases, nose
    /// bridge, lip slit"), and 1.2–1.6px is Tier 3. A tier is a page-scale convention, so the fix is
    /// to hold the tier <i>relative to the feature</i> rather than to the page. Each fraction below
    /// is the old pixel count divided by the feature's own measure on a <b>240px</b> head — the size
    /// Manual 23 takes its measurements at and the one the tests use — so a 240px head renders
    /// exactly as it did and every other size is now right instead of wrong.
    /// </para>
    /// <para>
    /// <b>Each feature scales off its own measurement rather than off the head</b>, because a drawer
    /// is handed the feature and never the head. That is also what lets a feature be drawn standalone
    /// — onto its own plate, for a mesh texture — and still come out at the right weight.
    /// </para>
    /// </remarks>
    /// <summary>The head height these tiers were set at, and the feature measures it produces.</summary>
    private const float TierHead = 240f;
    private const float EyeWidthAt240 = TierHead / 7f;          // unit × 0.5, unit = H / 3.5
    private const float NoseLengthAt240 = 68.4f;                // bridgeTop → underNose, frontal
    private const float MouthWidthAt240 = TierHead / 5f;        // leftCorner → rightCorner, frontal
    private const float EarHeightAt240 = TierHead / 3.5f;       // one unit, Plate 18

    /// <summary>The tier each mark is inked at, in pixels on a 240px head.</summary>
    private const float LidTier = 3.8f;
    private const float LowerLidTier = 1.6f;
    private const float IrisRimTier = 1.2f;
    private const float NoseBridgeTier = 2.4f;
    private const float NostrilTier = 2.0f;
    private const float NostrilRadiusTier = 3.5f;
    private const float LipLineTier = 2.4f;
    private const float HelixTier = 2.6f;
    private const float AntihelixTier = 1.6f;
    private const float ConchaTier = 1.2f;

    /// <summary>What is left of the bridge mark on a head with no turn in it.</summary>
    private const float FrontalBridge = 0.38f;

    /// <summary>
    /// A tier's width on a head of this size — <b>sub-linear, and that is the whole point</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first fix for the absolute constants scaled them <i>linearly</i> with the feature, which is
    /// defensible arithmetic and wrong as drawing. Rendered, it turned a 560px head's nose bridge into
    /// a 5.6px black dagger down the middle of the face and shrank a 110px head's eyelid to under half
    /// a pixel. **Line weight does not track subject size**: an inker drawing a long shot *simplifies*
    /// — fewer marks — rather than reaching for a finer nib, which is Janson's doctrine in Manual 03
    /// and Lee &amp; Buscema's reduction test in Manual 20.
    /// </para>
    /// <para>
    /// A square root is the honest middle. Against the old constants it is <b>exactly right at 240px</b>
    /// — so a head at the calibration size renders unchanged — and at 110px and 560px it gives 0.68×
    /// and 1.53× rather than 0.46× and 2.33×. The reference head is stated rather than implied, which
    /// is the difference between this and what it replaced.
    /// </para>
    /// <para>
    /// The implied head is recovered from the feature's own measure, because a drawer is handed the
    /// feature and never the head — which is also what lets a feature be drawn onto its own plate for
    /// a mesh texture and still come out at the right weight.
    /// </para>
    /// </remarks>
    private static float Tier(float tierPx, float measured, float measuredAt240, float multiplier = 1f) =>
        tierPx * MathF.Sqrt(MathF.Max(0.02f, measured / MathF.Max(0.01f, measuredAt240))) * multiplier;

    /// <summary>
    /// What a tapered mark's peak has to be to carry the same ink as the constant-width line it replaced.
    /// </summary>
    /// <remarks>
    /// <see cref="BuildTaperedStroke"/>'s envelope is <c>maxThickness × sin(tπ)</c>, whose mean over
    /// the run is <c>2/π</c> of its peak — so a taper set to the old tier width lays down about 64%
    /// of the ink and reads noticeably lighter, which would make a change meant to improve the
    /// drawing look like a regression. Peaking at <c>π/2</c> of the tier keeps the average weight
    /// where it was and puts the variation either side of it.
    /// </remarks>
    private const float TaperGain = MathF.PI / 2f;

    /// <summary>
    /// The quadratic control point that makes the curve pass <b>through</b> <paramref name="through"/>.
    /// </summary>
    /// <remarks>
    /// A quadratic at its midpoint is a quarter of each end plus half its control, so a control set
    /// at the landmark puts the curve only halfway to it. Solving for the control instead is the
    /// difference between a named station meaning what it says and meaning about half of it.
    /// </remarks>
    static Point2D ControlThrough(Point2D from, Point2D through, Point2D to) =>
        new((2f * through.X) - ((from.X + to.X) * 0.5f), (2f * through.Y) - ((from.Y + to.Y) * 0.5f));

    /// <summary>One measurement of a group divided by another, or <paramref name="fallback"/>.</summary>
    /// <remarks>
    /// A ratio rather than an absolute is what makes a landmark-derived proportion survive
    /// projection: the far eye is narrower at yaw, and dividing by its own width is what stops it
    /// also reading as half shut.
    /// </remarks>
    static float Ratio(IDictionary? d, string numerator, string denominator, float fallback)
    {
        var n = Num(d, numerator, float.NaN);
        var q = Num(d, denominator, float.NaN);
        if (!float.IsFinite(n) || !float.IsFinite(q) || q <= 0.01f || n < 0f) return fallback;
        var r = n / q;
        return float.IsFinite(r) ? r : fallback;
    }

    /// <summary>Which side of the face an Action Unit acts on.</summary>
    /// <remarks>
    /// <para>
    /// <b>Near and far, not left and right, because that is what the construction has.</b> A head
    /// carries <c>nearEye</c>/<c>farEye</c> and <c>nearBrow</c>/<c>farBrow</c>, and "near" is always
    /// the <c>+x</c> side of the facial axis whatever the yaw — it is a side of the *page*, not an
    /// anatomical left or right. Naming these <c>left</c> and <c>right</c> would be a claim about the
    /// character's own anatomy that the model cannot keep across a turn, so those two spellings are
    /// refused by name rather than quietly mapped.
    /// </para>
    /// </remarks>
    private enum FaceSide { Both, Near, Far }

    /// <summary>Whether <paramref name="side"/> includes the near (<c>+x</c>) or far half.</summary>
    static bool SideCovers(FaceSide side, bool isNear) =>
        side == FaceSide.Both || (isNear ? side == FaceSide.Near : side == FaceSide.Far);

    /// <summary>Reads the <c>side</c> option, refusing anything it cannot honestly honour.</summary>
    static FaceSide ReadSide(IDictionary? opt)
    {
        var raw = opt?["side"]?.ToString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(raw)) return FaceSide.Both;

        return raw switch
        {
            "both" => FaceSide.Both,
            "near" => FaceSide.Near,
            "far" => FaceSide.Far,
            "left" or "right" => throw new ArgumentException(
                $"side '{raw}' is not available: this head is built as near and far rather than left "
                + "and right, and 'near' is the +x side of the facial axis at every yaw. Left and "
                + "right would be a claim about the character's own anatomy that a turned head "
                + "cannot keep. Accepted: both, near, far."),
            _ => throw new ArgumentException($"side '{raw}' not recognised. Accepted: both, near, far.")
        };
    }

    /// <summary>A positive finite option read from a dictionary, or <paramref name="fallback"/>.</summary>
    /// <remarks>
    /// Distinct from <see cref="Ratio"/>, which divides one measurement of a group by another. This
    /// reads a ratio the caller states outright — a non-positive or non-finite one falls back rather
    /// than drawing a zero-radius iris or throwing, since an option is a preference and a silently
    /// absent feature is worse than an ignored number.
    /// </remarks>
    static float Fraction(IDictionary? d, string key, float fallback)
    {
        var v = Num(d, key, float.NaN);
        return float.IsFinite(v) && v > 0f ? v : fallback;
    }

    static Dictionary<string, object?> CloneHead(IDictionary head)
    {
        var copy = new Dictionary<string, object?>(head.Count);
        foreach (DictionaryEntry entry in head)
        {
            var key = entry.Key?.ToString();
            if (key is null) continue;
            copy[key] = JsInterop.AsDict(entry.Value) is IDictionary nested ? CloneHead(nested) : entry.Value;
        }

        return copy;
    }

    /// <summary>Replaces a point inside a nested group, leaving the rest of the group alone.</summary>
    static void SetPoint(Dictionary<string, object?> head, string group, string key, Point2D value)
    {
        if (head.TryGetValue(group, out var g) && g is Dictionary<string, object?> dict)
            dict[key] = ToDict(value);
    }

    static Point2D GetPoint(Dictionary<string, object?> head, string group, string key) =>
        head.TryGetValue(group, out var g) && g is Dictionary<string, object?> dict && dict.ContainsKey(key)
            ? ExtractPoint(dict[key])
            : new Point2D(0f, 0f);

    /// <summary>
    /// Eye spacing and eye size, the two that most change who a face is.
    /// </summary>
    /// <remarks>
    /// Spacing moves the inner corners toward or away from the facial axis and carries the outer
    /// corner and centre with them, so the eye keeps its width while the pair moves. Size scales each
    /// eye about its own centre, so the spacing set above survives it — doing them the other way
    /// round makes the two parameters fight, and a reader cannot tell which one they are adjusting.
    /// </remarks>
    /// <summary>
    /// Opens or hoods the lids, by scaling each eye's <c>height</c> and leaving its width alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sixth parameter, and it was absent for a mechanical reason rather than a design one:</b>
    /// <see cref="DrawComicEye"/> derived the whole aperture from eye width, so <c>height</c> was
    /// written by the construction and read by nothing. Now that the renderer takes it as a ratio
    /// against width, changing it here is the difference between a hooded eye and a staring one.
    /// </para>
    /// <para>
    /// <b>This is identity, not expression.</b> The range tops out well short of a shut eye, because
    /// a character who is permanently blinking is not a character. Closing an eye is an Action Unit
    /// and belongs to the expression layer, which drives the same field through a blend.
    /// </para>
    /// </remarks>
    static void OpenEyes(Dictionary<string, object?> head, float opening)
    {
        if (opening == 0f) return;

        var scale = 1f + (opening * 0.6f);
        foreach (var group in new[] { "nearEye", "farEye" })
        {
            if (head[group] is not Dictionary<string, object?> eye) continue;
            if (eye.TryGetValue("height", out var h) && h is not null)
                eye["height"] = Convert.ToSingle(h, CultureInfo.InvariantCulture) * scale;
        }
    }

    static void MoveEyes(Dictionary<string, object?> head, float spacing, float size)
    {
        foreach (var (group, sign) in new[] { ("nearEye", 1f), ("farEye", -1f) })
        {
            if (head[group] is not Dictionary<string, object?> eye) continue;

            var inner = GetPoint(head, group, "inner");
            var outer = GetPoint(head, group, "outer");
            var centre = GetPoint(head, group, "center");

            // Which way "outward" points for this eye, read off its own geometry rather than assumed:
            // the far eye mirrors, and at yaw its corners are not symmetric about the axis.
            var away = MathF.Sign(outer.X - inner.X);
            if (away == 0) away = (int)sign;

            // Positive is WIDER: the whole eye translates outward, so the gap between the two inner
            // corners — which is what "eyes distance" names — opens. Written negated first, which
            // made +1 narrow the face; the sign is not guessable from the parameter name alone,
            // which is why both directions are asserted rather than one.
            var dx = away * spacing;
            inner = new Point2D(inner.X + dx, inner.Y);
            outer = new Point2D(outer.X + dx, outer.Y);
            centre = new Point2D(centre.X + dx, centre.Y);

            var scale = 1f + size * 0.35f;
            inner = new Point2D(centre.X + (inner.X - centre.X) * scale, centre.Y + (inner.Y - centre.Y) * scale);
            outer = new Point2D(centre.X + (outer.X - centre.X) * scale, centre.Y + (outer.Y - centre.Y) * scale);

            SetPoint(head, group, "inner", inner);
            SetPoint(head, group, "outer", outer);
            SetPoint(head, group, "center", centre);

            if (eye.TryGetValue("width", out var w) && w is not null)
                eye["width"] = Convert.ToSingle(w, CultureInfo.InvariantCulture) * scale;
            if (eye.TryGetValue("height", out var h) && h is not null)
                eye["height"] = Convert.ToSingle(h, CultureInfo.InvariantCulture) * scale;
        }
    }

    /// <summary>
    /// Nose length, from the bridge down.
    /// </summary>
    /// <remarks>
    /// The bridge is the hinge, not the apex: a nose lengthens from where it leaves the brow, which
    /// is what keeps the eye line fixed while the nose changes. `underNose` and the nostril travel
    /// with the apex, and `noseBase` at the top level is moved to match — it and `noseWedge.apex`
    /// describe the same feature, and a head where they disagree draws a wireframe that does not meet
    /// the inked nose.
    /// </remarks>
    static void StretchNose(Dictionary<string, object?> head, float dy)
    {
        if (dy == 0f) return;

        foreach (var key in new[] { "apex", "underNose", "nearNostril" })
        {
            var p = GetPoint(head, "noseWedge", key);
            SetPoint(head, "noseWedge", key, new Point2D(p.X, p.Y + dy));
        }

        if (head.TryGetValue("noseBase", out var nb) && nb is not null)
        {
            var basePt = ExtractPoint(nb);
            head["noseBase"] = ToDict(new Point2D(basePt.X, basePt.Y + dy));
        }
    }

    /// <summary>
    /// Points or squares the chin alone, leaving the jaw's own width where <c>jawShape</c> put it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A second axis rather than more of the first.</b> <c>jawShape</c> spreads all six stations
    /// together, so it answers <i>how wide is this jaw</i> and nothing else — a broad jaw ending in a
    /// point, or a narrow one ending square, were both unreachable. This moves the two chin corners
    /// against the chin itself, so the two parameters multiply instead of duplicating.
    /// </para>
    /// <para>
    /// <b>The vertical component is deliberate and small.</b> Negative pulls the corners in and drops
    /// the point, which is what a pointed chin does; positive spreads them and lifts it a little,
    /// which flattens. The coupling is anatomical — a chin that comes to a point is longer than one
    /// that ends square — and it is kept to a quarter of the horizontal move so this stays a
    /// <i>shape</i> control rather than a length one. <b>Length is <see cref="LengthenChin"/>'s</b>,
    /// which takes the jaw angle down with the chin as lengthening the lower face requires; this
    /// small dy is coupling, not a second way to reach it.
    /// </para>
    /// <para>
    /// <b>Both chins move.</b> The construction records the chin twice — <c>head.chin</c> and
    /// <c>jaw.chin</c> — and only the first is read by <see cref="CreateHeadGeometry"/>. Moving one
    /// and not the other would leave a duplicate disagreeing with the drawing, which is the kind of
    /// divergence that surfaces later as a blend doing something inexplicable.
    /// </para>
    /// </remarks>
    static void ShapeChin(Dictionary<string, object?> head, float amount, float H)
    {
        if (amount == 0f) return;

        // Read before anything moves: the axis is the chin's own x, and the point is about to shift.
        var chin = ExtractPoint(head.TryGetValue("chin", out var c) ? c : null);

        foreach (var key in new[] { "chinNear", "chinFar" })
        {
            var p = GetPoint(head, "jaw", key);
            var outward = p.X - chin.X;
            if (outward == 0f) continue;
            SetPoint(head, "jaw", key, new Point2D(p.X + (MathF.Sign(outward) * amount * H * 0.045f), p.Y));
        }

        var dy = -amount * H * 0.012f;
        head["chin"] = ToDict(new Point2D(chin.X, chin.Y + dy));

        var jawChin = GetPoint(head, "jaw", "chin");
        SetPoint(head, "jaw", "chin", new Point2D(jawChin.X, jawChin.Y + dy));
    }

    /// <summary>
    /// Lower-face length: the whole mandible grows or shortens, chin and angle together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The angle comes down with the chin, and that is the entire difference between this and
    /// moving the chin point.</b> A mandible that lengthens lengthens in two places — the ramus, from
    /// the station down to the angle, and the body, from the angle forward to the chin — so dropping
    /// the chin alone would stretch the last inch of the outline into a spike and leave a jaw that
    /// still ended where it did. The angle takes half a step to the chin's one, which is the share
    /// that keeps the two segments in proportion to each other.
    /// </para>
    /// <para>
    /// <b>The stations do not move at all.</b> They are the joint against the skull, and a skull does
    /// not get longer because a jaw does — the same reasoning that took the stations out of
    /// <see cref="SquareJaw"/> after they drew a lateral spur, and the same reasoning that gives them
    /// the smallest share in <see cref="DropJaw"/>. This is the axis <c>jawShape</c> works along
    /// turned ninety degrees: that one asks how wide, this one asks how long, and they compose.
    /// </para>
    /// <para>
    /// <b>Full scale is 0.05 H, about a quarter of the canon's mouth-to-chin gap and a sixth of the
    /// lower face.</b> Deliberately more modest than <c>noseLength</c>'s <c>0.07 H</c>, which already
    /// spends most of its own gap and is the binding constraint on every caricature: this one moves
    /// the <i>silhouette</i>, so it reads at panel size at a fraction of the displacement a feature
    /// inside the face needs. At <c>-1</c> the chin still sits <c>0.14 H</c> below the mouth, so the
    /// full range stays a face and <see cref="VerifyHeadOrdering"/> keeps room to report on.
    /// </para>
    /// <para>
    /// <b>Order against <see cref="ShapeChin"/> does not matter</b>, since that one reads the chin's
    /// <c>x</c> and writes <c>x</c> and <c>y</c> on the corners while this writes <c>y</c> only. They
    /// are run in the documented order anyway, so a reader never has to establish that.
    /// </para>
    /// </remarks>
    static void LengthenChin(Dictionary<string, object?> head, float amount, float H)
    {
        if (amount == 0f) return;

        var dy = amount * H * 0.05f;

        if (head.TryGetValue("chin", out var c) && c is not null)
        {
            var chin = ExtractPoint(c);
            head["chin"] = ToDict(new Point2D(chin.X, chin.Y + dy));
        }

        if (head["jaw"] is not Dictionary<string, object?> jaw) return;

        foreach (var (key, share) in new[] { ("chin", 1f), ("chinNear", 1f), ("chinFar", 1f),
                                             ("angle", 0.5f), ("nearAngle", 0.5f) })
        {
            if (!jaw.ContainsKey(key)) continue;
            var p = ExtractPoint(jaw[key]);
            jaw[key] = ToDict(new Point2D(p.X, p.Y + (dy * share)));
        }
    }

    /// <summary>
    /// Jaw shape, from tapering toward the chin to squared at the angle.
    /// </summary>
    /// <remarks>
    /// Loomis hangs the jaw off the halfway line round the ball (Plate 1), so the stations are what
    /// the shape lives in: widening them squares the jaw, narrowing them tapers it to the chin. The
    /// chin points move a fraction of that so the outline stays continuous — moving the stations
    /// alone leaves a jaw that steps in at the chin instead of running to it.
    /// </remarks>
    static void SquareJaw(Dictionary<string, object?> head, float amount)
    {
        if (amount == 0f) return;

        var axis = ExtractPoint(head.TryGetValue("chin", out var c) ? c : null).X;
        var unit = head["unit"] as Dictionary<string, object?>;
        var W = unit is not null && unit.TryGetValue("W", out var w) && w is not null
            ? Convert.ToSingle(w, CultureInfo.InvariantCulture) : 160f;

        // **The station barely moves, and that is the whole shape of a squared jaw.** Loomis hangs the
        // jaw off the ball's halfway line station to station, so the station is a *joint* - the point
        // where the mandible meets the skull - and the skull does not widen when a character's jaw
        // does. Displacing it was this call's first behaviour and it produced a sharp lateral spur at
        // every value above zero: the jaw's top edge is a straight line between the two stations, the
        // ball curves inward above them, and past the ball's own reach the two meet at a corner with
        // nothing over it. Measured on a 240px head, `nearStation` sits at dx 76.7 against a ball
        // reaching 76.6 - exactly on it - and the old full share took it to 93.1, seventeen pixels
        // into open air.
        //
        // What a square jaw actually widens is the **angle** (the gonion) and the **chin**, which is
        // where the shares now sit. A small residual at the station keeps the ramus from reading as a
        // parallel-sided slab.
        foreach (var (key, share) in new[] { ("nearStation", 0.15f), ("farStation", 0.15f),
                                             ("nearAngle", 1f), ("angle", 1f),
                                             ("chinNear", 0.55f), ("chinFar", 0.55f) })
        {
            var p = GetPoint(head, "jaw", key);
            var outward = p.X - axis;
            if (outward == 0f) continue;
            SetPoint(head, "jaw", key, new Point2D(p.X + MathF.Sign(outward) * amount * W * 0.08f * share, p.Y));
        }
    }

    /// <summary>Mouth width, about its own centre, leaving the lip heights alone.</summary>
    static void WidenMouth(Dictionary<string, object?> head, float dx)
    {
        if (dx == 0f) return;

        var centre = GetPoint(head, "mouthGuides", "center");
        foreach (var (key, sign) in new[] { ("leftCorner", -1f), ("rightCorner", 1f) })
        {
            var p = GetPoint(head, "mouthGuides", key);
            var away = MathF.Sign(p.X - centre.X);
            if (away == 0) away = (int)sign;
            SetPoint(head, "mouthGuides", key, new Point2D(p.X + away * dx, p.Y));
        }
    }
    #endregion

    #region Head Geometry
    /// <summary>Accepted options for <see cref="CreateHeadGeometry"/>. An unrecognised one is refused.</summary>
    static readonly string[] HeadGeometryOptions = ["padding", "neckLength", "neckWidth", "skull"];

    /// <summary>Chaikin corner-cutting on a closed polygon: each pass replaces every vertex with two.</summary>
    /// <remarks>
    /// Closed-form and deterministic, which a head has to be — the same landmarks must give the same
    /// outline in panel 1 and panel 40. Two passes turn seven jaw stations into twenty-eight points
    /// and round every corner; the contour pulls in by about a quarter of each corner, which is what
    /// a jaw does and a scaffold does not.
    /// </remarks>
    static List<Point2D> Chaikin(IReadOnlyList<Point2D> pts, int passes)
    {
        var current = new List<Point2D>(pts);
        for (var pass = 0; pass < passes && current.Count > 2; pass++)
        {
            var next = new List<Point2D>(current.Count * 2);
            for (var i = 0; i < current.Count; i++)
            {
                Point2D a = current[i], b = current[(i + 1) % current.Count];
                next.Add(new Point2D(a.X * 0.75f + b.X * 0.25f, a.Y * 0.75f + b.Y * 0.25f));
                next.Add(new Point2D(a.X * 0.25f + b.X * 0.75f, a.Y * 0.25f + b.Y * 0.75f));
            }

            current = next;
        }

        return current;
    }

    /// <summary>A closed polygon through the given points, optionally grown outward by <paramref name="padding"/>.</summary>
    /// <remarks>
    /// The growth is a union of capsules along the edges rather than a true polygon offset. It is
    /// exact along each edge and slightly round at the corners, which is what cloth over a jaw does
    /// anyway — and unlike an offset it cannot invert on a concave vertex.
    /// </remarks>
    static CanvasPath Polygon(IReadOnlyList<Point2D> pts, float padding)
    {
        var p = new CanvasPath();
        if (pts.Count == 0) return p;

        p.MoveTo(pts[0].X, pts[0].Y);
        for (var i = 1; i < pts.Count; i++) p.LineTo(pts[i].X, pts[i].Y);
        p.ClosePath();
        if (padding <= 0f) return p;

        var grown = p;
        for (var i = 0; i < pts.Count; i++)
            grown = grown.Union(Capsule(pts[i], pts[(i + 1) % pts.Count], padding, padding));
        return grown;
    }

    /// <summary>
    /// The head as geometry rather than as landmarks: one silhouette, and the four masses it is made
    /// of — cranium, jaw, ear and neck.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> <see cref="CreateLoomisHead"/> returns landmarks and the comic feature
    /// drawers place marks at them, and between the two there was nothing that unioned anything into
    /// a head. Studio Manual 23 §7 named the absence — <em>"nothing that unions them into a drawn
    /// head with a silhouette, an ear and a neck"</em> — and a live run then drew it: two faces whose
    /// features were correctly placed and which read as masks on undifferentiated shoulder-masses,
    /// because the features had nothing to sit on. The agent had not failed to compose a head; no
    /// call composed one.
    /// </para>
    /// <para>
    /// Same split as <see cref="CreateMannequinFigure"/> against <see cref="CreateFigureGeometry"/>,
    /// and for the same reason: paths are not free, so a loop that only places heads should not pay
    /// for a dozen boolean operations it will not use.
    /// </para>
    /// <para>
    /// <b>Two of the four masses are Loomis's own construction and cost nothing to derive.</b> The
    /// cranium is the ball the head is already built on — <c>brow.y − crown.y</c> is its radius, which
    /// is exactly the <c>ballR</c> the jaw stations are computed from, so the ball drawn here and the
    /// jaw hung off it cannot disagree. The ear is a unit tall and half a unit across with its outer
    /// edge on the head's half-width (Plate 18), which is already why <c>jaw.ear</c> sits where it does.
    /// </para>
    /// <para>
    /// <b>The neck's attachment is cited; its length and width are the studio's.</b> Loomis puts the
    /// turning muscles on the skull <em>just behind the ears</em> at the top and on the breastbone
    /// between the collarbones at the bottom, and places the pivot <em>well inside the roundness of
    /// the neck and deep under the skull</em>, a little back of its centre line (<em>Drawing the Head
    /// and Hands</em>, the head-on-neck passage in Part One — <b>cite by passage, not by page</b>: the
    /// scan's page numbers do not survive extraction reliably). That is the one fact that makes a head
    /// sit rather than float, and it is why the column is anchored under the ear rather than under the
    /// chin. He gives no measurement for how long or how thick, so <c>neckLength</c> defaults to
    /// 0.42 H below the chin and the half-width is derived from the jaw stations — which also means
    /// the neck foreshortens with the turn, because the stations do.
    /// </para>
    /// <para>
    /// <b>The ear widens with the turn rather than narrowing.</b> Frontally an ear is seen edge-on; in
    /// profile it is seen full-face — the opposite of the far eye, which is the only foreshortening
    /// this construction already carried. The turn is recovered from <c>farEye.width / unit.eyeW</c>,
    /// which is <c>cos(yaw)</c> <b>clamped at 0.45</b> by the construction, so past roughly 63° the ear
    /// stops widening. Beyond that angle build the ear yourself.
    /// </para>
    /// <para>
    /// <c>padding</c> inflates every mass, exactly as it does on a figure: a hood, a hat band or a
    /// collar is the padded head minus the bare one.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateHeadGeometry(object headObj, object? options = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException(
                "createHeadGeometry needs a head from Drawing.createLoomisHead(...) or Drawing.createParametricHead(...).",
                nameof(headObj));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, HeadGeometryOptions, "createHeadGeometry option");

        var unit = JsInterop.AsDict(head["unit"]);
        var H = Num(unit, "H", 100f);
        var thirdH = Num(unit, "thirdH", H / 3.5f);
        var eyeW = Num(unit, "eyeW", thirdH * 0.5f);

        // A head from `createHeadForFigure` carries the fit it was built to. An explicit option still
        // wins; without one these follow the figure rather than the canon, so a caller never has to
        // remember to forward two values that were computed for this exact head on that exact body.
        var fit = JsInterop.AsDict(head["fit"]);

        var padding = MathF.Max(0f, Num(opt, "padding", 0f));
        var neckLength = Num(opt, "neckLength", Num(fit, "neckLength", 0.30f)) * H;

        // **The one cited departure from the ball, and it is this manual's own measurement.** Lee &
        // Buscema's head is five eye-widths across where Loomis's construction is six (Studio Manual
        // 23 §1, measured on both) — so a comic skull is 5/6 of the ball, and the same face inside a
        // narrower cranium is most of what makes a head read at panel size. `loomis` stays the
        // default: the landmarks were laid out for a six-eye head, and silently narrowing every head
        // already drawn is not a default's job.
        var skull = opt?["skull"]?.ToString()?.Trim().ToLowerInvariant()
                    ?? fit?["skull"]?.ToString()?.Trim().ToLowerInvariant()
                    ?? "loomis";
        var wScale = skull switch
        {
            "loomis" => 1f,
            "comic" => 5f / 6f,
            _ => throw new ArgumentException(
                $"createHeadGeometry skull not recognised: {skull}. Accepted: loomis, comic.")
        };

        var jaw = JsInterop.AsDict(head["jaw"]);
        var crown = ExtractPoint(head["crown"]);
        var brow = ExtractPoint(head["brow"]);
        var chin = ExtractPoint(head["chin"]);
        var noseBase = ExtractPoint(head["noseBase"]);
        var ear = ExtractPoint(jaw?["ear"]);

        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        void Extent(float cx, float cy, float hw, float hh)
        {
            x0 = MathF.Min(x0, cx - hw); y0 = MathF.Min(y0, cy - hh);
            x1 = MathF.Max(x1, cx + hw); y1 = MathF.Max(y1, cy + hh);
        }

        // The ball. Its radius IS the one the jaw stations were computed from, so the two agree by
        // construction rather than by matching numbers written in two places.
        var ballR = MathF.Max(thirdH * 0.5f, brow.Y - crown.Y);
        var craniumC = new Point2D(crown.X, brow.Y);
        var cranium = OrientedEllipse(craniumC, ballR * wScale + padding, ballR + padding, 0f);
        Extent(craniumC.X, craniumC.Y, ballR * wScale + padding, ballR + padding);

        // Half-width of the cranium at a given height, which is what the jaw and the ear both meet.
        float Reach(float y) => MathF.Sqrt(MathF.Max(0f, ballR * ballR - (y - brow.Y) * (y - brow.Y))) * wScale;

        // The jaw hangs off the halfway line round the ball, station to station (Plate 1). These are
        // the six points `squareJaw` displaces, so a parametric head changes shape here for free.
        // **A station is held out to the ball's own silhouette at its height.** The turn foreshortens
        // the stations (`× cos(yaw)`) while a sphere's outline does not foreshorten at all, so on any
        // turned head the jaw starts inboard of the cranium and the union shows a notch at the cheek
        // — a bite out of the face, not a jaw. Holding it out only changes where the two meet: the
        // ball already governs the outline at that height, so no extent is added and a squared jaw
        // still widens past it.
        Point2D OnBall(Point2D p)
        {
            var reach = Reach(p.Y);
            var outward = p.X - crown.X;
            return MathF.Abs(outward) >= reach ? p : new Point2D(crown.X + MathF.Sign(outward) * reach, p.Y);
        }

        Point2D[] jawPts =
        [
            OnBall(ExtractPoint(jaw?["farStation"])), ExtractPoint(jaw?["angle"]), ExtractPoint(jaw?["chinFar"]),
            chin, ExtractPoint(jaw?["chinNear"]), ExtractPoint(jaw?["nearAngle"]), OnBall(ExtractPoint(jaw?["nearStation"]))
        ];
        // Corner-cut before filling. The seven stations are a scaffold, not an outline: joined by
        // straight segments they give a jaw with a hard shelf at the angle, which at head size reads
        // as a machined part rather than a face. Two rounds of Chaikin is enough to round it and is
        // closed-form, so it stays deterministic.
        var jawPath = Polygon(Chaikin(jawPts, 2), padding);
        foreach (var p in jawPts) Extent(p.X, p.Y, padding, padding);

        // cos(yaw), recovered from the one place the construction recorded it. The clamp is the
        // construction's, not ours, and it is why the ear stops widening past about 63 degrees.
        var cos = eyeW > 0f ? Math.Clamp(Num(JsInterop.AsDict(head["farEye"]), "width", eyeW) / eyeW, 0f, 1f) : 1f;
        var sin = MathF.Sqrt(MathF.Max(0f, 1f - cos * cos));

        // **The ear straddles the ball's own silhouette, and that is Loomis rather than a nudge to
        // make it show.** Plate 1 attaches the ears along the same halfway line round the ball that
        // the jaw hangs from, and that halfway line IS the silhouette. Centred on the `jaw.ear`
        // landmark instead, the mass sits at one unit from the axis against a ball 1.41 units wide at
        // that height — wholly inside the cranium, invisible on every head that is not a profile. The
        // landmark is the attachment, not the centre; it is left untouched and supplies the side.
        var earDir = ear.X < brow.X ? -1f : 1f;
        var earDy = ear.Y - brow.Y;
        var earRy = thirdH * 0.5f + padding;                       // one unit tall — Plate 18
        // Roughly 1:0.55 tall to wide, opening toward profile. Thinner than this and the mass reads
        // as a chip taken out of the skull rather than as an ear: half of it is inside the cranium,
        // so what the reader sees is one radius wide against a full unit tall.
        var earRx = thirdH * (0.18f + 0.10f * sin) + padding;
        var earOut = Reach(ear.Y) * cos - earRx * 0.3f;
        var earCx = crown.X + earDir * earOut;
        var earPath = OrientedEllipse(new Point2D(earCx, ear.Y), earRx, earRy, 0f);
        Extent(earCx, ear.Y, earRx, earRy);

        // **The other ear, which this call drew for nobody until 2026-09-19.** A head has two, and
        // `jaw.ear` names only the far one — so every frontal head came back lopsided, with a bump on
        // one side and a clean curve on the other. The limits note used to say so and tell the caller
        // to mirror `parts.ear` about `crown.x` themselves, which is the wrong default twice over: it
        // is anatomy rather than style, so nobody chose it; and the instruction was missed by the
        // author of this very toolkit the first time he drew a frontal sheet with it.
        //
        // **It narrows where the far ear widens, which is the same fact read from the other side.**
        // An ear is edge-on frontally and full-face in profile, so as the head turns the far ear
        // opens toward the viewer (`+0.10 * sin` above) and the near one closes away from them. At
        // yaw 0 the two widths are identical and the head is symmetric, which is the whole point.
        //
        // **Nothing here models the occlusion, and nothing needs to.** Its centre rides the ball at
        // `Reach * cos` exactly as the far one does, so as it narrows it is swallowed by the cranium
        // ellipse it is unioned into. **Measured on a 240px head it stops altering the outline at
        // 10 degrees** — 2.71px of protrusion frontally, 0.57px at 8, nothing from 10 on — and that
        // is the physics rather than an aggressive fudge: a frontal ear sits exactly ON the ball's
        // silhouette, so any turn toward it puts it behind the head's own edge. The union does the
        // hiding for free, which is as well, since a construction with no cheek could not do it any
        // other way.
        var nearEarRx = MathF.Max(0f, thirdH * 0.18f * (1f - sin)) + padding;
        var nearEarCx = crown.X - (earDir * (Reach(ear.Y) * cos - (nearEarRx * 0.3f)));
        var nearEarPath = OrientedEllipse(new Point2D(nearEarCx, ear.Y), nearEarRx, earRy, 0f);
        Extent(nearEarCx, ear.Y, nearEarRx, earRy);

        // **The cheek, and it is the last hole this call's own limits note admitted to.** The note
        // called it "a shallow concave step" where the ball's inward curve crosses the jaw's outward
        // one, and told the caller to ink over it or union their own wedge in. Both halves of that
        // sentence were wrong: it is neither shallow nor where it said, and a caller cannot fix
        // anatomy with ink.
        //
        // **Printing the outline row by row is what found it.** On a 480px frontal head the far edge
        // holds 205-211px off the axis from the brow all the way down to y=564, and then reads 153 at
        // y=572 — a **45px cliff in eight rows**, with everything above and below it already smooth
        // and monotonic. It is not the jaw meeting the ball at all: it is **the foot of the ear**. The
        // ear is a tall narrow ellipse riding the ball's silhouette, so its lower half hangs a long
        // way outboard of a ball that is collapsing under it, and at the nose line it simply stops.
        // Every head had a V bitten out under the ear.
        //
        // **Three constructions were measured before this one, and the first two are worth recording
        // because they looked right.** A capsule from the cheekbone to the jaw angle moved the notch
        // instead of removing it, from 62% of brow-to-chin to 70% — its own toe, landing on a jawline
        // it was not tangent to. A wedge carried on to the chin corner removed more of it but added
        // **23-34px of width** to the lower face, five times the defect it was correcting. A straight
        // line from the ear's widest point to the jaw angle did nothing whatsoever: it lies inside the
        // ball for its whole length, and the A/B render is what said so after a metric had implied it
        // worked.
        //
        // **What it is now: the outer tangent from the jaw angle to the ear.** The cheek is the
        // triangle between the point where that tangent touches the ear, the angle itself, and the
        // ball's centre. Touching the ear tangentially is what makes the join seamless — there is no
        // step at the ear's foot because the outline never leaves the ear, it rolls off it — and the
        // whole thing is solved from the ear and the jaw angle, so **there is not one constant in it
        // to tune or to defend.** Measured on the same 480px head, the worst single-row drop goes from
        // **38px to 4px**, and 4px is the floor: a turned head with no cheek acting measures the same.
        //
        // Anatomically this is the masseter, which runs from the zygomatic arch — the ear — to the
        // angle of the jaw, and it is drawn twice elsewhere already: `drawLoomisWireframe` inks its
        // far half from `jaw.cheekApex`, and Studio Manual 04 names the band as the *cheek hollow /
        // mandible plane*. What was missing was never the anatomy, only a mass.
        //
        // **The near cheek empties on its own as the head turns, with no factor applied.** It is
        // strung from the near ear, and that ear is already narrowing and riding inboard at
        // `Reach × cos`, so past a small turn the whole triangle falls inside the cranium. A cheek is
        // at the silhouette when you are looking at the front of a head and in the middle of the face
        // once that side comes toward you, which this does without being told.
        Point2D? EarTangent(float cx, float rx, float ry, Point2D angle, float side)
        {
            if (rx <= 0.01f || ry <= 0.01f) return null;

            // Solved on the unit circle the ellipse maps to, which is why an ear that is nearly
            // edge-on at yaw is still exact rather than nearly-degenerate.
            float px = (angle.X - cx) / rx, py = (angle.Y - ear.Y) / ry;
            var d2 = (px * px) + (py * py);
            if (d2 <= 1.0001f) return null;          // the jaw angle is inside the ear: no tangent

            var s = MathF.Sqrt(d2 - 1f) / d2;
            Point2D On(float ux, float uy) => new(cx + (ux * rx), ear.Y + (uy * ry));
            Point2D a = On((px / d2) + (s * py), (py / d2) - (s * px));
            Point2D b = On((px / d2) - (s * py), (py / d2) + (s * px));
            // Of the two tangents, the one further from the facial axis is the outer one.
            return (a.X - crown.X) * side > (b.X - crown.X) * side ? a : b;
        }

        // The ear radii carry `padding` already, so the tangent is taken against the bare ear and
        // `Polygon` grows the triangle — which lands it exactly where the padded ear and the padded
        // jaw put their own edges, instead of padding it twice.
        CanvasPath Cheek(float cx, float rx, Point2D angle, float side)
        {
            var touch = EarTangent(cx, rx - padding, earRy - padding, angle, side);
            return touch is null ? new CanvasPath() : Polygon([touch.Value, angle, craniumC], padding);
        }

        var farCheek = Cheek(earCx, earRx, ExtractPoint(jaw?["angle"]), earDir);
        var nearCheek = Cheek(nearEarCx, nearEarRx, ExtractPoint(jaw?["nearAngle"]), -earDir);

        // Behind the ear at the top, deep under the skull — the cited part. How far down and how
        // thick are the studio's, and both are in head units so they scale.
        var neckHalf = MathF.Max(thirdH * 0.2f, Num(opt, "neckWidth", thirdH * 1.30f) * 0.5f);
        // **Scaled by the turn, so a frontal head gets a centred neck.** Loomis's "a little back of
        // the centre line" is a statement about depth, and depth only becomes screen offset once the
        // head turns — applied flat it hangs the column off one side of a head looking straight out.
        var neckX = crown.X + (ear.X - crown.X) * 0.45f * sin;
        var neckTop = new Point2D(neckX, (noseBase.Y + chin.Y) * 0.5f);

        var neck = new CanvasPath();
        if (neckLength > 0f)
        {
            // `neckLength` is the whole extent below the chin, base cap included. Measuring to the
            // capsule's centre instead put the drawn end a further half-width down, which is how the
            // first version of this came out as a light-bulb stem a third longer than asked for.
            var baseR = neckHalf + padding;
            var neckBase = new Point2D(neckX, MathF.Max(neckTop.Y + 1f, chin.Y + neckLength - baseR));
            neck = Capsule(neckTop, neckBase, neckHalf + padding, baseR);
            Extent(neckTop.X, neckTop.Y, neckHalf + padding, neckHalf + padding);
            Extent(neckBase.X, neckBase.Y, baseR, baseR);
        }

        var mass = cranium.Union(farCheek).Union(nearCheek).Union(jawPath).Union(earPath).Union(nearEarPath);
        // The cheek reaches no further out than the ear and the jaw angle it is strung between, so it
        // can add nothing to the extent those two already recorded — nothing to measure here.

        return new Dictionary<string, object?>
        {
            ["silhouette"] = (neckLength > 0f ? mass.Union(neck) : mass).Simplify(),
            // The head without the neck: what a feature clips to, and what a hat sits on.
            ["mass"] = mass.Simplify(),
            ["parts"] = new Dictionary<string, object?>
            {
                ["cranium"] = cranium,
                ["jaw"] = jawPath,
                // `ear` keeps its name and its side — it is the FAR ear, the one `jaw.ear` locates,
                // and renaming it to `farEar` would break every caller to make a pair read tidily.
                ["ear"] = earPath,
                ["nearEar"] = nearEarPath,
                // Named by side from the start, unlike `ear` — there is no caller to keep faith with
                // here, so the pair reads as a pair.
                ["farCheek"] = farCheek,
                ["nearCheek"] = nearCheek,
                ["neck"] = neck
            },
            // **Where each ear is, so something can draw one.** `parts.ear` is a mass and has no
            // internal structure; these are what `drawComicEar` needs, and they live here rather than
            // on the head because an ear's placement depends on the cranium — its centre rides the
            // ball's own silhouette, which `skull: 'comic'` narrows. The radii are the unpadded ones.
            // `faceDir` points from the ear toward the facial axis, which is what tells the drawer
            // which way round the helix and the lobe go.
            ["ears"] = new Dictionary<string, object?>
            {
                ["far"] = new Dictionary<string, object?>
                {
                    ["center"] = ToDict(new Point2D(earCx, ear.Y)),
                    ["width"] = (earRx - padding) * 2f,
                    ["height"] = (earRy - padding) * 2f,
                    ["faceDir"] = -earDir,

                    // **The far ear rotates in FRONT of the ball, so it is never occluded by it.**
                    // It is also the one that widens with the turn (`+0.10 * sin` on its radius),
                    // which is the same fact: an ear is edge-on frontally and full-face in profile.
                    // At three-quarters it sits inside the silhouette rather than on it, and that is
                    // what an ear does — being inside the outline is not being hidden.
                    ["visible"] = true
                },
                ["near"] = new Dictionary<string, object?>
                {
                    ["center"] = ToDict(new Point2D(nearEarCx, ear.Y)),
                    ["width"] = (nearEarRx - padding) * 2f,
                    ["height"] = (earRy - padding) * 2f,
                    ["faceDir"] = earDir,

                    // **The near ear rotates BEHIND the ball, so the skull occludes it — and the
                    // test for that is exactly the protrusion the note above already measured.** The
                    // masses can be unioned blind, because a hidden ear adds nothing to a
                    // silhouette; a *drawn* one is painted on top, so without this its rim and bowl
                    // appeared on the cheek beside the near eye at yaw 38.
                    //
                    // Asked of the real paths rather than of an approximation of them, so it agrees
                    // with that measurement — 2.71px of protrusion frontally, 0.57px at 8 degrees,
                    // nothing from 10 on — instead of drifting from it by however much the padding
                    // and the skull's own narrowing happen to be worth.
                    ["visible"] = nearEarPath.Subtract(cranium).Path.Bounds.Width > 0.75f
                }
            },
            ["bounds"] = x0 > x1
                ? new Dictionary<string, object?>
                {
                    ["x"] = 0f, ["y"] = 0f, ["width"] = 0f, ["height"] = 0f,
                    ["x2"] = 0f, ["y2"] = 0f, ["cx"] = 0f, ["cy"] = 0f
                }
                : new Dictionary<string, object?>
                {
                    ["x"] = x0, ["y"] = y0, ["width"] = x1 - x0, ["height"] = y1 - y0,
                    ["x2"] = x1, ["y2"] = y1, ["cx"] = (x0 + x1) * 0.5f, ["cy"] = (y0 + y1) * 0.5f
                },
            ["padding"] = padding,
            // Construction order, NOT depth — as on a figure. The neck goes down first because the
            // jaw overlaps it; on a head turned far enough that the far jaw passes behind the neck,
            // the caller still has to say so.
            // The near ear goes down before the cranium: it sits behind the face on a turned head,
            // and on a frontal one it is symmetric with the far ear so the order cannot show.
            ["order"] = new List<object?>
                { "neck", "nearEar", "cranium", "farCheek", "nearCheek", "jaw", "ear" }
        };
    }

    /// <summary>Accepted options for <see cref="CreateHeadForFigure"/>. An unrecognised one is refused.</summary>
    static readonly string[] HeadForFigureOptions = ["yawDeg", "pitchDeg", "skull", "neckLength", "character"];

    /// <summary>
    /// A head built to sit on a mannequin figure: placed and scaled to the figure's own head mass,
    /// with a neck that reaches its shoulder line. Returns an ordinary head, plus a <c>fit</c> block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four things have to line up and three of them are arithmetic the caller kept getting to do
    /// itself.</b> Measured against <see cref="CreateMannequinFigure"/>, whose stations in head units
    /// down from the crown are chin <c>1.00 H</c>, neck <c>1.15 H</c>, shoulder line <c>1.40 H</c>:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Placement and scale.</b> <c>createLoomisHead</c>'s <c>originY</c> is the head's
    /// vertical <i>centre</i>, not its crown, so the figure's <c>head.center</c> goes straight in and
    /// the height is <c>2 * head.ry</c> — one head unit, which is what the canon says it is.</item>
    /// <item><b>The skull defaults to <c>comic</c> here, and to <c>loomis</c> everywhere else.</b> The
    /// figure's head egg is <c>2 * rx</c> = <c>0.72 H</c> wide. A Loomis head is <c>3 * (H / 3.5)</c> =
    /// <c>0.857 H</c>, so it overhangs its own shoulders by 19%; the comic skull is <c>5/6</c> of that,
    /// <c>0.714 H</c>, which is the egg to within 1%. The mannequin has been carrying a comic skull
    /// all along.</item>
    /// <item><b>The neck is measured, not assumed.</b> <c>createHeadGeometry</c>'s default
    /// <c>neckLength</c> of <c>0.30</c> ends at <c>1.30 H</c> and the shoulder line is at
    /// <c>1.40 H</c> — a tenth of a head unit of daylight under the chin. This measures chin to
    /// sternal notch on the figure in hand, so a posed figure gets the length its own pose needs
    /// rather than the canon's.</item>
    /// <item><b>The roll is reported and not applied.</b> <c>figure.head.angleDeg</c> is
    /// <c>spineDeg + neckDeg</c>, a lean on the page — where <c>createLoomisHead</c> takes only yaw
    /// and pitch. Rotating every landmark here would produce a head whose features no longer agree
    /// with the axis the drawing calls build from, so <c>fit.rollDeg</c> and <c>fit.pivot</c> are
    /// handed back for the caller to apply with a transform. Without it a leaning figure keeps an
    /// upright face.</item>
    /// </list>
    /// <para>
    /// <c>yawDeg</c> defaults to <b>0</b> rather than <c>createLoomisHead</c>'s 35: a head on a figure
    /// faces where the figure faces until told otherwise. <c>character</c> is passed to
    /// <see cref="CreateParametricHead"/> before the neck is measured, and <b>that order is now
    /// load-bearing rather than merely tidy.</b> It was kept when nothing depended on it — the
    /// parameters of the day reached <c>jaw.chinNear</c> and <c>jaw.chinFar</c> and never the
    /// top-level <c>chin</c> the neck hangs from — on the argument that a parameter which ever moved
    /// that one would silently hang the neck off a chin that no longer existed. <c>chinShape</c> and
    /// <c>chinLength</c> are both that parameter, so a long-jawed character now gets the shorter neck
    /// its own chin leaves room for, and did so the day it was added without anything being changed
    /// here. <b>Measuring last cost nothing and bought exactly this.</b>
    /// </para>
    /// <para>
    /// <b><c>fit</c> travels with the head</b>, so <c>createHeadGeometry</c> reads <c>neckLength</c>
    /// and <c>skull</c> from it unless you pass your own. One source of truth, and nothing to
    /// remember to forward.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateHeadForFigure(object figureObj, object? options = null)
    {
        if (JsInterop.AsDict(figureObj) is not IDictionary fig || JsInterop.AsDict(fig["head"]) is not IDictionary node)
            throw new ArgumentException(
                "createHeadForFigure needs a figure from Drawing.createMannequinFigure(...).", nameof(figureObj));

        if (fig["sternum"] is null)
            throw new ArgumentException(
                "createHeadForFigure: the figure carries no sternum, so there is no shoulder line to reach.",
                nameof(figureObj));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, HeadForFigureOptions, "createHeadForFigure option");

        var skull = opt?["skull"]?.ToString()?.Trim().ToLowerInvariant() ?? "comic";
        if (skull is not ("loomis" or "comic"))
            throw new ArgumentException(
                $"createHeadForFigure skull not recognised: {skull}. Accepted: loomis, comic.");

        var center = ExtractPoint(node["center"]);
        var headHeight = Num(node, "ry", 0f) * 2f;
        if (headHeight <= 0f)
            throw new ArgumentException(
                "createHeadForFigure: the figure's head carries no ry, so there is no size to build to.",
                nameof(figureObj));

        var head = CreateLoomisHead(center.X, center.Y, headHeight,
            Num(opt, "yawDeg", 0f), Num(opt, "pitchDeg", 0f));

        if (JsInterop.AsDict(opt?["character"]) is IDictionary character)
            head = CreateParametricHead(head, character);

        // Measured on the figure in hand rather than on the canon, which is what lets a posed figure
        // get the neck its own pose needs. Taken after any character parameters, which is now what
        // makes a long- or short-chinned character get the neck its own chin leaves room for.
        var chin = ExtractPoint(head["chin"]);
        var sternum = ExtractPoint(fig["sternum"]);
        var reach = MathF.Sqrt((sternum.X - chin.X) * (sternum.X - chin.X)
                             + (sternum.Y - chin.Y) * (sternum.Y - chin.Y));

        head["fit"] = new Dictionary<string, object?>
        {
            ["rollDeg"] = Num(node, "angleDeg", 0f),
            ["pivot"] = ToDict(center),
            ["headHeight"] = headHeight,
            ["neckLength"] = opt != null && opt.Contains("neckLength")
                ? MathF.Max(0f, Num(opt, "neckLength", 0f))
                : reach / headHeight,
            ["reach"] = reach,
            ["skull"] = skull
        };

        return head;
    }

    #endregion

    #endregion
}




