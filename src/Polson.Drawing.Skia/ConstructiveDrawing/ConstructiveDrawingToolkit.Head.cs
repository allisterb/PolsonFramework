namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
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
        //
        // **And there are two of them, which is where the nose's width comes from.** Gautier gives
        // the one measurement every other reference here declines to: *"The width of the base of the
        // nose measures exactly one eye width, the same as the distance between the eyes measured
        // from the inside corner"* (*Drawing and Cartooning 1,001 Faces*, p. 27). Both quantities are
        // already in this construction — `eyeW`, and an inner-corner gap set to exactly one
        // eye-width above — so the nostrils sit half an eye-width either side of the meridian and the
        // base spans `eyeW` by construction. Faragasso reaches the same span from the brows instead
        // of from the eyes, which is two independent routes to one number.
        //
        // Until 2026-09-19 only the near one existed, so the nose had **no width anywhere**: a
        // bridge line on the axis and a single nostril beside it.
        Point2D Nostril(float side, float scale)
        {
            // The ear on that side, mirrored about the crown axis exactly as the jaw stations are.
            var earX = originX + (side * unit * MathF.Cos(rad));
            var x = centerAxisX + (side * eyeW * 0.50f * scale);
            var run = earX - centerAxisX;

            // Clamped to the segment: past roughly 60 degrees the near ear has swung behind the
            // facial axis and the line reverses, which would throw the nostril up the bridge.
            var t = MathF.Abs(run) < 0.01f ? 0f : Math.Clamp((x - centerAxisX) / run, 0f, 1f);
            return new Point2D(x, (yNose + (H * 0.035f)) + (t * -H * 0.035f));
        }

        var nearNostrilPt = Nostril(1f, 1f);

        // The far wing foreshortens with the turn, as the far eye and the far mouth corner do.
        var farNostrilPt = Nostril(-1f, farScale);

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
                ["nearNostril"] = ToDict(nearNostrilPt),
                ["farNostril"] = ToDict(farNostrilPt)
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

    #region Double-Circle Head
    /// <summary>
    /// Hamm's double-circle construction (<i>Drawing the Head and Figure</i>, pp. 2–3), read off a head's own
    /// landmarks: the ball, the chin circle, the tangents joining them, and the cheek arcs swung from J and K.
    /// </summary>
    /// <param name="A">Centre of the big circle, on the starting line — the head's <c>brow</c> landmark.</param>
    /// <param name="C">Bottom of the big circle: the nose line.</param>
    /// <param name="F">Centre of the chin circle, and the top of the mouth.</param>
    /// <param name="D">As far below F as C is above it: the bottom of the mouth.</param>
    /// <param name="E">The chin.</param>
    /// <param name="J">Where the left tangent touches the big circle; the eyes' tops sit on J–K.</param>
    /// <param name="G">Where the left tangent touches the chin circle.</param>
    /// <param name="L">Where the left cheek arc, swung from K, meets the chin circle.</param>
    /// <param name="Arc">The cheek arcs' radius, |JK|.</param>
    readonly record struct DoubleCircleFrame(Point2D A, float R1, Point2D C, Point2D F, Point2D D, Point2D E, float R2,
        Point2D J, Point2D K, Point2D G, Point2D H, float Arc, Point2D L, Point2D M);

    /// <summary>Whether a head was built by <see cref="CreateDoubleCircleHead"/>.</summary>
    static bool IsDoubleCircle(IDictionary head) =>
        string.Equals(head.Contains("construction") ? head["construction"]?.ToString() : null, "doubleCircle", StringComparison.Ordinal);

    /// <summary>
    /// The construction for any head: the ball from <c>crown</c> and <c>brow</c>, and the chin circle two thirds
    /// of the way from the ball's centre to the chin, reaching the chin — where Hamm puts it. Null when the chin
    /// sits so high there is no tangent between the circles.
    /// </summary>
    static DoubleCircleFrame? DoubleCircleOf(Point2D crown, Point2D brow, Point2D chin)
    {
        var a = new Point2D(crown.X, brow.Y);
        var r1 = brow.Y - crown.Y;
        float ex = chin.X - a.X, ey = chin.Y - a.Y, ae = MathF.Sqrt(ex * ex + ey * ey);
        if (r1 <= 0f || ae <= 0f) return null;

        float ux = ex / ae, uy = ey / ae, px = -uy, py = ux;
        var f = new Point2D(a.X + ex * 2f / 3f, a.Y + ey * 2f / 3f);
        float r2 = ae / 3f, d = ae * 2f / 3f;
        if (d <= MathF.Abs(r1 - r2) + 1e-3f) return null;

        // The outer tangents: a normal whose component along the centre line is (r1 − r2) / d.
        float c = (r1 - r2) / d, s = MathF.Sqrt(MathF.Max(0f, 1f - c * c));
        Point2D Touch(Point2D o, float r, float side) => new(o.X + r * (c * ux + side * s * px), o.Y + r * (c * uy + side * s * py));
        Point2D j = Touch(a, r1, 1f), k = Touch(a, r1, -1f);
        var arc = MathF.Sqrt((k.X - j.X) * (k.X - j.X) + (k.Y - j.Y) * (k.Y - j.Y));

        // Where a cheek arc meets the chin circle: the lower crossing, or — Hamm's own case, where the arc passes
        // a hundredth of an eye outside the circle — the point of closest approach.
        Point2D Meet(Point2D centre)
        {
            float dx = f.X - centre.X, dy = f.Y - centre.Y, q = MathF.Sqrt(dx * dx + dy * dy);
            if (q <= 1e-3f) return new Point2D(f.X, f.Y + r2);
            if (q + r2 <= arc || q >= arc + r2) return new Point2D(centre.X + dx / q * arc, centre.Y + dy / q * arc);
            var along = (q * q + arc * arc - r2 * r2) / (2f * q);
            var h = MathF.Sqrt(MathF.Max(0f, arc * arc - along * along));
            float bx = centre.X + dx / q * along, by = centre.Y + dy / q * along;
            Point2D p1 = new(bx - dy / q * h, by + dx / q * h), p2 = new(bx + dy / q * h, by - dx / q * h);
            return p1.Y >= p2.Y ? p1 : p2;
        }

        var cPt = new Point2D(a.X + ux * r1, a.Y + uy * r1);
        return new DoubleCircleFrame(a, r1, cPt, f, new Point2D(2f * f.X - cPt.X, 2f * f.Y - cPt.Y), chin, r2,
            j, k, Touch(f, r2, 1f), Touch(f, r2, -1f), arc, Meet(k), Meet(j));
    }

    /// <summary>
    /// A head built by Jack Hamm's double-circle construction (<i>Drawing the Head and Figure</i>, pp. 2–4): a
    /// front view, five eyes wide, with every feature placed off two circles and the lines between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In eye-widths <c>e</c> = <c>headHeight</c> / 7, from the big circle's centre A: the big circle has radius
    /// 2.5 e, so it is five eyes across; the nose sits on C, its bottom, at 2.5 e; the mouth runs from F at 3 e to
    /// D at 3.5 e; the chin E is at 4.5 e, and the chin circle is centred on F and reaches it. The tangents from
    /// circle to circle touch the big circle at J and K, 0.83 e below A, and the eyes' tops sit on J–K, under the
    /// second and fourth fifths of the starting line. The brows sit halfway between that line and the eyes; the
    /// ears run from the eyes' tops to the nose; the nose is the middle fifth wide and the mouth 1.5 e, measured
    /// on Hamm's step-9 plate. An eye is half as tall as it is wide (p. 4).
    /// </para>
    /// <para>
    /// The head is in the same schema as <see cref="CreateLoomisHead"/>, so every feature drawer, expression,
    /// parametric call and <c>createHeadGeometry</c> takes it; the last builds Hamm's own outline for it — the
    /// big circle trimmed by verticals from J and K, the cheek arcs, and the chin circle. The hairline is
    /// Loomis's, a seventh of the head below the crown; Hamm gives none here.
    /// </para>
    /// <para>
    /// <b>A front view only.</b> Hamm draws turned heads by other means, and a turn is not a property of this
    /// construction; for one, use <see cref="CreateLoomisHead"/>.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> CreateDoubleCircleHead(float originX, float originY, float headHeight)
    {
        var H = headHeight;
        var e = H / 7f;
        var x0 = originX;
        var top = originY - H * 0.5f;
        var a = top + 2.5f * e;

        // The tangent points, from the frame itself, so the eye line and the outline cannot disagree.
        var frame = DoubleCircleOf(new Point2D(x0, top), new Point2D(x0, a), new Point2D(x0, top + 7f * e))!.Value;
        var eyeTop = frame.J.Y;
        var eyeH = 0.5f * e;
        var yEye = eyeTop + eyeH * 0.5f;
        var yBrow = (a + eyeTop) * 0.5f;
        var yNose = frame.C.Y;
        var upperLip = frame.F.Y;
        var lowerLip = frame.D.Y;
        var yMouth = upperLip + (lowerLip - upperLip) / 3f;    // the top lip is a third of the mouth's depth (p. 4)

        Dictionary<string, object?> Eye(float side) => new()
        {
            ["inner"] = ToDict(new Point2D(x0 + side * 0.5f * e, yEye)),
            ["outer"] = ToDict(new Point2D(x0 + side * 1.5f * e, yEye)),
            ["center"] = ToDict(new Point2D(x0 + side * e, yEye)),
            ["width"] = e,
            ["height"] = eyeH
        };

        // The same brow stations Loomis's head carries, on Hamm's brow line.
        Dictionary<string, object?> Brow(float side)
        {
            var inner = new Point2D(x0 + side * 0.5f * e, yBrow);
            var outer = new Point2D(x0 + side * 1.62f * e, yBrow);
            return new Dictionary<string, object?>
            {
                ["inner"] = ToDict(inner),
                ["peak"] = ToDict(new Point2D(inner.X + (outer.X - inner.X) * 0.66f, yBrow - 0.14f * e)),
                ["outer"] = ToDict(outer),
                ["thickness"] = 0.24f * e
            };
        }

        // Points on a cheek arc and on the chin circle, for the jaw landmarks a Loomis-style jaw would use.
        Point2D OnArc(Point2D centre, float y, float side) =>
            new(centre.X + side * MathF.Sqrt(MathF.Max(0f, frame.Arc * frame.Arc - (y - centre.Y) * (y - centre.Y))), y);
        var chinY = frame.E.Y - 0.25f * frame.R2;
        var chinDx = frame.R2 * MathF.Sqrt(1f - 0.5625f);
        var yEar = (eyeTop + yNose) * 0.5f;
        var W = 5f * e;

        return new Dictionary<string, object?>
        {
            ["construction"] = "doubleCircle",
            ["unit"] = new Dictionary<string, object?>
            {
                ["H"] = H,
                ["W"] = W,
                ["eyeW"] = e,
                // What createHeadGeometry reads it for: the ear's height, eyes' tops to nose (p. 4).
                ["thirdH"] = yNose - eyeTop
            },
            ["origin"] = new Dictionary<string, object?> { ["x"] = originX, ["y"] = originY },
            ["crown"] = ToDict(new Point2D(x0, top)),
            ["hairline"] = ToDict(new Point2D(x0, top + e)),
            ["brow"] = ToDict(frame.A),
            ["eyeLineY"] = yEye,
            ["noseBase"] = ToDict(new Point2D(x0, yNose)),
            ["mouthCenter"] = ToDict(new Point2D(x0, yMouth)),
            ["chin"] = ToDict(frame.E),
            ["farEye"] = Eye(-1f),
            ["nearEye"] = Eye(1f),
            ["farBrow"] = Brow(-1f),
            ["nearBrow"] = Brow(1f),
            ["noseWedge"] = new Dictionary<string, object?>
            {
                ["bridgeTop"] = ToDict(new Point2D(x0, (yBrow + yEye) * 0.5f)),
                ["apex"] = ToDict(new Point2D(x0, yNose - 0.245f * e)),
                ["underNose"] = ToDict(new Point2D(x0, yNose)),
                ["nearNostril"] = ToDict(new Point2D(x0 + 0.5f * e, yNose - 0.03f * e)),
                ["farNostril"] = ToDict(new Point2D(x0 - 0.5f * e, yNose - 0.03f * e))
            },
            ["mouthGuides"] = new Dictionary<string, object?>
            {
                ["center"] = ToDict(new Point2D(x0, yMouth)),
                ["leftCorner"] = ToDict(new Point2D(x0 - 0.75f * e, yMouth)),
                ["rightCorner"] = ToDict(new Point2D(x0 + 0.75f * e, yMouth)),
                ["upperLipY"] = upperLip,
                ["lowerLipY"] = lowerLip
            },
            ["jaw"] = new Dictionary<string, object?>
            {
                ["ear"] = ToDict(new Point2D(x0 - 2f * e, yEar)),
                ["angle"] = ToDict(OnArc(frame.K, upperLip, -1f)),
                ["nearAngle"] = ToDict(OnArc(frame.J, upperLip, 1f)),
                ["farStation"] = ToDict(frame.J),
                ["nearStation"] = ToDict(frame.K),
                ["chinFar"] = ToDict(new Point2D(x0 - chinDx, chinY)),
                ["chinNear"] = ToDict(new Point2D(x0 + chinDx, chinY)),
                ["chin"] = ToDict(frame.E),
                ["cheekApex"] = ToDict(new Point2D(frame.J.X + 0.35f * e, yEye + 0.3f * e))
            },
            ["temporalOval"] = new Dictionary<string, object?>
            {
                ["cx"] = x0 - W * 0.15f,
                ["cy"] = (a + yNose) * 0.5f,
                ["rx"] = W * 0.32f,
                ["ry"] = H * 0.26f
            }
        };
    }

    static readonly string[] DoubleCircleWireframeOptions = ["blueLineColor", "labels", "lineWidth"];

    /// <summary>
    /// Draws Hamm's construction for a head, steps 1–8: the starting line in fifths, the big circle, the centre
    /// line with C, F, D and E, the chin circle, the tangents, J–K, the cheek arcs and the sides.
    /// </summary>
    /// <remarks>
    /// Works on any head, since the frame is read off its landmarks; on a Loomis head the fifths, which are its
    /// eye-widths, show that the ball is six eyes across rather than five. <c>labels</c> letters the points as
    /// Hamm does.
    /// </remarks>
    public void DrawDoubleCircleWireframe(CanvasRenderingContext2D ctx, object headObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("drawDoubleCircleWireframe needs a head from Drawing.createDoubleCircleHead(...) or createLoomisHead(...).", nameof(headObj));
        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, DoubleCircleWireframeOptions, "drawDoubleCircleWireframe option");

        var f = DoubleCircleOf(ExtractPoint(head["crown"]), ExtractPoint(head["brow"]), ExtractPoint(head["chin"]))
            ?? throw new ArgumentException("drawDoubleCircleWireframe: this head's chin is inside its cranium circle, so there is no construction to draw.");
        var e = Num(JsInterop.AsDict(head["unit"]), "eyeW", f.R1 / 2.5f);
        var labels = opt != null && opt.Contains("labels") && opt["labels"] is true;

        ctx.Save();
        ctx.StrokeStyle = opt?["blueLineColor"]?.ToString() ?? "#4a90e2";
        ctx.LineWidth = Num(opt, "lineWidth", 1.5f);

        void Line(Point2D p, Point2D q) { ctx.BeginPath(); ctx.MoveTo(p.X, p.Y); ctx.LineTo(q.X, q.Y); ctx.Stroke(); }
        void Circle(Point2D c, float r) { ctx.BeginPath(); ctx.Arc(c.X, c.Y, r, 0f, MathF.PI * 2f); ctx.Stroke(); }
        void Tick(Point2D p, float half, bool across) =>
            Line(across ? new Point2D(p.X, p.Y - half) : new Point2D(p.X - half, p.Y), across ? new Point2D(p.X, p.Y + half) : new Point2D(p.X + half, p.Y));
        void ArcBetween(Point2D centre, Point2D from, Point2D to)
        {
            float a0 = MathF.Atan2(from.Y - centre.Y, from.X - centre.X), a1 = MathF.Atan2(to.Y - centre.Y, to.X - centre.X);
            var sweep = a1 - a0;
            while (sweep > MathF.PI) sweep -= MathF.PI * 2f;
            while (sweep < -MathF.PI) sweep += MathF.PI * 2f;
            ctx.BeginPath();
            ctx.Arc(centre.X, centre.Y, f.Arc, a0, a0 + sweep, sweep < 0f);
            ctx.Stroke();
        }

        // Steps 1–2: the starting line in fifths, and the big circle on it.
        Line(new Point2D(f.A.X - f.R1 * 1.1f, f.A.Y), new Point2D(f.A.X + f.R1 * 1.1f, f.A.Y));
        for (var k = -5; k <= 5; k += 2) Tick(new Point2D(f.A.X + k * 0.5f * e, f.A.Y), e * 0.15f, true);
        Circle(f.A, f.R1);
        // Step 3: the centre line, with C, F, D and E.
        Line(new Point2D(f.A.X, f.A.Y - f.R1 * 1.1f), new Point2D(f.E.X, f.E.Y + e * 0.4f));
        foreach (var p in new[] { f.C, f.F, f.D, f.E }) Tick(p, e * 0.3f, false);
        // Steps 4–6: the chin circle, the tangents, and J–K.
        Circle(f.F, f.R2);
        Line(f.J, f.G);
        Line(f.K, f.H);
        Line(f.J, f.K);
        // Step 7: the cheek arcs, from K swung about J and from J swung about K.
        ArcBetween(f.J, f.K, f.M);
        ArcBetween(f.K, f.J, f.L);
        // Step 8: the sides, up from J and K.
        Line(f.J, new Point2D(f.J.X, f.A.Y - f.R1 * 0.85f));
        Line(f.K, new Point2D(f.K.X, f.A.Y - f.R1 * 0.85f));

        if (labels)
        {
            ctx.FillStyle = ctx.StrokeStyle;
            ctx.Font = $"{MathF.Max(9f, e * 0.32f):0}px sans-serif";
            void Label(string text, Point2D p, float dx, float dy) => ctx.FillText(text, p.X + dx, p.Y + dy);
            var g = e * 0.18f;
            Label("A", f.A, g, -g);
            Label("C", f.C, -e * 0.55f, g);
            Label("F", f.F, -e * 0.55f, g);
            Label("D", f.D, -e * 0.55f, g);
            Label("E", f.E, -e * 0.55f, g);
            Label("J", f.J, -e * 0.4f, g);
            Label("K", f.K, g, g);
            Label("L", f.L, -e * 0.4f, g);
            Label("M", f.M, g, g);
        }

        ctx.Restore();
    }
    #endregion

    #region Parametric Head
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
        ["eyesDistance", "eyesSize", "eyesOpening", "eyeLine", "noseLength",
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
        // Gautier's first cartoon decision: start from the portrait oval and set the eye line above or
        // below its standard mark (*Drawing and Cartooning 1,001 Faces*, ch. "Cartoons"). Positive
        // lowers it, which enlarges the cranium over the face. A tenth of the head either way keeps
        // the eye clear of the hairline above and the nose below.
        MoveEyeLine(result, P("eyeLine") * H * 0.11f);
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
    /// Squashes or stretches the face while the skull keeps its shape: <c>amount</c> from <c>-1</c>
    /// (squashed flat) through <c>0</c> to <c>+1</c> (stretched long). Returns a new head.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stanchfield, *Drawn to Life* vol. 1 ch. 37: the skull is usually pretty solid, and the rest of
    /// the head does the squashing and stretching; chs. 6-8: a squash keeps its volume. So the face
    /// below the eye line is scaled about it, vertically by <c>s</c> and across by <c>1/s</c>, which
    /// keeps its area. The crown, hairline, cranium, ears, eyes and brows do not move. The eyes stay
    /// with the skull because a squash that pushed them into the brows crushed them to slits on the
    /// first version of this call. At <c>+1</c> the lower face is 1.35 times as long, at <c>-1</c>
    /// 0.65 times. The range is the studio's: past it the nose meets the mouth.
    /// </para>
    /// <para>
    /// It is a pure function of the head and the amount, so it can be keyed: a jaw drop or a laugh in a
    /// <c>comp.drawn(...)</c> animation stretches the face frame by frame and the character stays the
    /// same person (ch. 35). Apply it after the character parameters and any expression.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> SquashHead(object headObj, float amount)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("squashHead needs a head from Drawing.createLoomisHead(...).", nameof(headObj));
        if (!float.IsFinite(amount))
            throw new ArgumentException("squashHead: amount must be a number from -1 (squash) to 1 (stretch).", nameof(amount));

        var result = CloneHead(head);
        var a = Math.Clamp(amount, -1f, 1f);
        if (a == 0f) return result;

        var sy = 1f + (a * 0.35f);
        var sx = 1f / sy;
        var axis = ExtractPoint(head["brow"]);
        var eyeLine = head.Contains("eyeLineY") && head["eyeLineY"] is not null
            ? Convert.ToSingle(head["eyeLineY"], CultureInfo.InvariantCulture)
            : ExtractPoint(JsInterop.AsDict(head["nearEye"])?["center"]).Y;
        var pivot = new Point2D(axis.X, eyeLine);
        float MapY(float y) => pivot.Y + ((y - pivot.Y) * sy);

        // The skull and what is fixed to it. `fit` is a figure's placement, not a landmark.
        var skull = new HashSet<string> { "unit", "origin", "crown", "hairline", "brow", "fit",
                                          "nearEye", "farEye", "nearBrow", "farBrow", "eyeLineY" };

        void Walk(Dictionary<string, object?> dict, string? parent)
        {
            foreach (var key in dict.Keys.ToList())
            {
                if (parent is null && skull.Contains(key)) continue;
                if (parent == "jaw" && key == "ear") continue;   // the ear rides the skull
                if (parent == "noseWedge" && key == "bridgeTop") continue;   // between the eyes

                switch (dict[key])
                {
                    case Dictionary<string, object?> point when point.Count == 2 && point.ContainsKey("x") && point.ContainsKey("y"):
                        var p = ExtractPoint(point);
                        dict[key] = ToDict(new Point2D(pivot.X + ((p.X - pivot.X) * sx), MapY(p.Y)));
                        break;
                    case Dictionary<string, object?> nested:
                        Walk(nested, key);
                        break;
                    case not null when key is "upperLipY" or "lowerLipY":
                        dict[key] = MapY(Convert.ToSingle(dict[key], CultureInfo.InvariantCulture));
                        break;
                }
            }
        }

        Walk(result, null);
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
    #endregion

    #region Blending
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
        ("noseWedge.farNostril", HeadValue.Point),

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
    /// A head built by another construction is measured against that construction's canon.
    /// </remarks>
    static Dictionary<string, object?> CanonFor(IDictionary head)
    {
        var (origin, h) = HeadFrame(head, "headObj");
        if (IsDoubleCircle(head)) return new ConstructiveDrawingToolkit().CreateDoubleCircleHead(origin.X, origin.Y, h);
        var yaw = MathF.Acos(Math.Clamp(HeadTurn(head), 0f, 1f)) * 180f / MathF.PI;
        return new ConstructiveDrawingToolkit().CreateLoomisHead(origin.X, origin.Y, h, yaw);
    }
    #endregion

    #region Landmark Displacement
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
    /// <summary>
    /// Moves the eye line: both eyes, both drawn brows and the top of the nose bridge, which sits
    /// between them. The cranium's equator (<c>brow</c>) stays, since the skull does not move.
    /// </summary>
    static void MoveEyeLine(Dictionary<string, object?> head, float dy)
    {
        if (dy == 0f) return;

        foreach (var (group, keys) in new[]
                 {
                     ("nearEye", new[] { "inner", "outer", "center" }), ("farEye", new[] { "inner", "outer", "center" }),
                     ("nearBrow", new[] { "inner", "peak", "outer" }), ("farBrow", new[] { "inner", "peak", "outer" }),
                     ("noseWedge", new[] { "bridgeTop" })
                 })
        {
            if (!head.TryGetValue(group, out var g) || g is not Dictionary<string, object?> dict) continue;
            foreach (var key in keys)
            {
                if (!dict.ContainsKey(key)) continue;
                var p = ExtractPoint(dict[key]);
                dict[key] = ToDict(new Point2D(p.X, p.Y + dy));
            }
        }

        if (head.TryGetValue("eyeLineY", out var y) && y is not null)
            head["eyeLineY"] = Convert.ToSingle(y, CultureInfo.InvariantCulture) + dy;
    }

    static void StretchNose(Dictionary<string, object?> head, float dy)
    {
        if (dy == 0f) return;

        foreach (var key in new[] { "apex", "underNose", "nearNostril", "farNostril" })
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
}
