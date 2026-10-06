namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
    #region Head Geometry
    /// <summary>Accepted options for <see cref="CreateHeadGeometry"/>. An unrecognised one is refused.</summary>
    static readonly string[] HeadGeometryOptions = ["padding", "neckLength", "neckWidth", "skull", "face"];

    /// <summary>
    /// The face shapes <c>createHeadGeometry({ face })</c> can build in place of Loomis's jaw: points
    /// across (<c>-1</c> to <c>1</c> of the face's half-width) and down (<c>0</c> at the brow line, <c>1</c>
    /// at the chin), smoothed into a curve. The family is Stanchfield's (*Drawn to Life* vol. 1 ch. 37,
    /// the basic head shapes a cartoon deviates into) and Gautier's (*Cartoons*, the same features in
    /// different head shapes); the coordinates are the studio's.
    /// </summary>
    static readonly Dictionary<string, (float U, float V)[]> FaceShapes = new()
    {
        ["box"] = [(-1f, 0f), (1f, 0f), (1f, 0.8f), (0.85f, 1f), (-0.85f, 1f), (-1f, 0.8f)],
        ["narrow"] = [(-0.75f, 0f), (0.75f, 0f), (0.75f, 0.85f), (0.55f, 1f), (-0.55f, 1f), (-0.75f, 0.85f)],
        ["wedge"] = [(-1.05f, 0f), (1.05f, 0f), (0.9f, 0.35f), (0.12f, 1f), (-0.12f, 1f), (-0.9f, 0.35f)],
        ["pear"] = [(-0.7f, 0f), (0.7f, 0f), (1.15f, 0.65f), (0.8f, 0.95f), (0f, 1f), (-0.8f, 0.95f), (-1.15f, 0.65f)],
        ["peanut"] = [(-1f, 0f), (1f, 0f), (0.7f, 0.4f), (1f, 0.75f), (0.55f, 1f), (-0.55f, 1f), (-1f, 0.75f), (-0.7f, 0.4f)],
    };

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

        // **A face shape in place of the jaw, under the same cranium** (Stanchfield ch. 35, 37: a circle
        // for the skull over an oval for the face, and a cartoon head is that pair pushed into another
        // shape). The face spans the jaw stations at the brow line and ends on the chin, so the
        // character parameters, an expression or `squashHead` still move it.
        var face = opt?["face"]?.ToString()?.Trim() ?? (IsDoubleCircle(head) ? "doubleCircle" : "loomis");
        if (!string.Equals(face, "doubleCircle", StringComparison.OrdinalIgnoreCase)) face = face.ToLowerInvariant();
        else face = "doubleCircle";
        var shaped = face != "loomis";

        // **Hamm's own outline** (Drawing the Head and Figure, pp. 2–3): the cheek arcs swung from J and K down
        // to the chin circle, and the big circle trimmed by verticals from J and K. Built from the head's ball and
        // chin, so it reaches any head, and squashHead or a longer chin move it with them.
        Func<float, float>? sideAt = null;
        if (face == "doubleCircle")
        {
            var frame = DoubleCircleOf(crown, brow, chin)
                ?? throw new ArgumentException("createHeadGeometry face 'doubleCircle': this head's chin is inside its cranium circle, so there are no tangents to build the face from.");
            var axis = frame.A.X;
            float Sx(float x) => axis + (x - axis) * wScale;
            CanvasPath Oval(Point2D c, float r) => OrientedEllipse(new Point2D(Sx(c.X), c.Y), r * wScale + padding, r + padding, 0f);
            CanvasPath Box(float xa, float ya, float xb, float yb)
            {
                var p = new CanvasPath();
                p.Rect(MathF.Min(xa, xb), MathF.Min(ya, yb), MathF.Abs(xb - xa), MathF.Abs(yb - ya));
                return p;
            }

            var far = frame.Arc * 4f + frame.R1 * 4f;
            var topY = MathF.Min(frame.J.Y, frame.K.Y) - padding;
            var lens = Oval(frame.J, frame.Arc).Intersect(Oval(frame.K, frame.Arc));
            var keep = Box(axis - far, topY, axis, frame.L.Y).Union(Box(axis, topY, axis + far, frame.M.Y));
            jawPath = lens.Intersect(keep).Union(Oval(frame.F, frame.R2));
            cranium = cranium.Intersect(Box(Sx(frame.J.X) - padding, crown.Y - padding * 2f - frame.R1, Sx(frame.K.X) + padding, frame.A.Y + frame.R1 + padding));
            var b = jawPath.Path.Bounds;
            Extent(b.MidX, b.MidY, b.Width * 0.5f, b.Height * 0.5f);
            // The sides trim the ball, so it no longer reaches the extent recorded for it above.
            x0 = MathF.Max(x0, MathF.Min(Sx(frame.J.X) - padding, b.Left));
            x1 = MathF.Min(x1, MathF.Max(Sx(frame.K.X) + padding, b.Right));

            // How far the outline reaches from the axis at a height: the side above J–K, a cheek arc below.
            sideAt = y =>
            {
                if (y <= frame.K.Y) return (frame.K.X - axis) * wScale;
                var dy = y - frame.J.Y;
                return (frame.J.X + MathF.Sqrt(MathF.Max(0f, frame.Arc * frame.Arc - dy * dy)) - axis) * wScale;
            };
        }
        else if (shaped)
        {
            var far = ExtractPoint(jaw?["farStation"]);
            var near = ExtractPoint(jaw?["nearStation"]);
            var top = brow.Y;
            var w = MathF.Abs(near.X - far.X) * 0.5f;
            var cxTop = (near.X + far.X) * 0.5f;

            (float U, float V)[] shape;
            if (face is "oval" or "round")
            {
                // An ellipse inside the brow-to-chin box. A round face is a fuller superellipse, wider
                // through the cheeks: a true circle in a box taller than it is wide comes out narrower
                // than the oval, which is the opposite of round.
                var n = face == "round" ? 2.8f : 2f;
                if (face == "round") w *= 1.12f;
                shape = [.. Enumerable.Range(0, 32).Select(i =>
                {
                    var t = i * MathF.PI * 2f / 32f;
                    float s = MathF.Sin(t), c = MathF.Cos(t);
                    float Pow(float v) => MathF.Sign(v) * MathF.Pow(MathF.Abs(v), 2f / n);
                    return (Pow(s), 0.5f - (0.5f * Pow(c)));
                })];
            }
            else if (!FaceShapes.TryGetValue(face, out shape!))
            {
                throw new ArgumentException(
                    $"createHeadGeometry face not recognised: {face}. Accepted: loomis, doubleCircle, oval, round, {string.Join(", ", FaceShapes.Keys)}.");
            }

            var pts = shape.Select(s => new Point2D(cxTop + ((chin.X - cxTop) * s.V) + (s.U * w), top + (s.V * (chin.Y - top)))).ToList();
            jawPath = Polygon(face is "oval" or "round" ? pts : Chaikin(pts, 3), padding);
            foreach (var p in pts) Extent(p.X, p.Y, padding, padding);
        }

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
        // **1:2 tall to wide at profile, which is Gautier's measurement rather than our estimate.**
        // *"Divide its length in thirds, then check to see that the widest part of the ear ... is half
        // the length"* (*Drawing and Cartooning 1,001 Faces*, p. 29). This read `0.18 + 0.10 * sin`,
        // peaking at 0.56 of the height — the comment beside it said "roughly 1:0.55", which was an
        // estimate by eye landing within a twentieth of the published figure. The frontal value is
        // unchanged, because that is the ear foreshortened rather than its own proportion; only the
        // profile limit moves, so a frontal head renders exactly as before and a turned one narrows
        // slightly. Thinner than this and the mass reads as a chip out of the skull rather than as an
        // ear: half of it is inside the cranium, so the reader sees one radius against a full unit.
        var earRx = thirdH * (0.18f + 0.07f * sin) + padding;
        var earOut = (sideAt?.Invoke(ear.Y) ?? Reach(ear.Y)) * cos - earRx * 0.3f;
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
        var nearEarCx = crown.X - (earDir * ((sideAt?.Invoke(ear.Y) ?? Reach(ear.Y)) * cos - (nearEarRx * 0.3f)));
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

        // A cheek is the seam between Loomis's ball and his jaw, so a shaped face has none.
        var farCheek = shaped ? new CanvasPath() : Cheek(earCx, earRx, ExtractPoint(jaw?["angle"]), earDir);
        var nearCheek = shaped ? new CanvasPath() : Cheek(nearEarCx, nearEarRx, ExtractPoint(jaw?["nearAngle"]), -earDir);

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
            ["face"] = face,
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
    /// <summary>
    /// A child's face, as character parameters fading out by twelve. Gautier (Drawing and Cartooning 1,001
    /// Faces, ch. "Children"): the cranium is larger than the face, the eyes keep their adult size so look
    /// larger and are set wider, the nose is small and the mouth smaller, and around ten the lower face
    /// catches up. The amounts are the studio's.
    /// </summary>
    static Dictionary<string, object?> ChildFace(float age)
    {
        var t = Math.Clamp((12f - age) / 11f, 0f, 1f);
        if (t <= 0f) return [];
        return new()
        {
            ["eyeLine"] = 0.9f * t, ["eyesSize"] = 0.4f * t, ["eyesDistance"] = 0.3f * t,
            ["noseLength"] = -0.6f * t, ["mouthWidth"] = -0.4f * t
        };
    }

    static readonly string[] HeadForFigureOptions = ["yawDeg", "pitchDeg", "skull", "neckLength", "character"];

    static readonly string[] FaceAirOptions = ["skull", "face"];

    /// <summary>
    /// How much of the face the features cover, and how much is left as "air": the eyes, brows, nose
    /// and mouth drawn as <c>drawComicEye</c> and its siblings draw them, measured against the face
    /// from hairline to chin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gautier's warning about cartoon heads (*Drawing and Cartooning 1,001 Faces*, ch. "Cartoons"): a
    /// face crammed with large eyes and a wide mouth reads as badly drawn, because the features need
    /// open space around them; they have to share a small area and still make sense. He gives no
    /// number, so the threshold a workflow checks against is the studio's.
    /// </para>
    /// <para>
    /// Measured by rasterising, not by adding path areas: the strokes, the fills and their overlaps
    /// are what a reader sees crowding the face. The head is drawn at least 400px tall for the count,
    /// so a small head is not measured at a few dozen pixels. <c>options</c> takes the
    /// <c>skull</c> and <c>face</c> that <c>createHeadGeometry</c> takes, which set the face outline.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> MeasureFaceAir(object headObj, object? options = null)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("measureFaceAir needs a head from Drawing.createLoomisHead(...).", nameof(headObj));

        var opt = JsInterop.AsDict(options);
        RefuseUnknownHeadParameters(opt, FaceAirOptions, "measureFaceAir option");

        var geoOptions = new Dictionary<string, object?> { ["neckLength"] = 0f };
        if (opt?["skull"] is { } skull) geoOptions["skull"] = skull;
        if (opt?["face"] is { } faceShape) geoOptions["face"] = faceShape;
        var geometry = CreateHeadGeometry(head, geoOptions);

        var mass = (CanvasPath)geometry["mass"]!;
        var bounds = JsInterop.AsDict(geometry["bounds"]);
        float x0 = Num(bounds, "x", 0f), y0 = Num(bounds, "y", 0f);
        float bw = Num(bounds, "width", 1f), bh = Num(bounds, "height", 1f);

        var hairline = ExtractPoint(head["hairline"]);
        var chin = ExtractPoint(head["chin"]);
        var band = new CanvasPath();
        band.Rect(x0 - 1f, hairline.Y, bw + 2f, chin.Y - hairline.Y + 1f);
        var faceRegion = mass.Intersect(band);

        var scale = MathF.Max(1f, 400f / MathF.Max(1f, bh));
        var w = (int)MathF.Ceiling(bw * scale) + 2;
        var h = (int)MathF.Ceiling(bh * scale) + 2;

        SkiaCanvas Surface(Action<CanvasRenderingContext2D> draw)
        {
            var canvas = new SkiaCanvas(w, h);
            var ctx = canvas.GetContext("2d");
            ctx.Scale(scale, scale);
            ctx.Translate(-x0 + (1f / scale), -y0 + (1f / scale));
            draw(ctx);
            return canvas;
        }

        using var mask = Surface(ctx => { ctx.FillStyle = "#000000"; ctx.Fill(faceRegion); });
        var maskPixels = mask.SkBitmap.Pixels;
        var facePixels = maskPixels.Count(p => p.Alpha > 0);

        int Covered(SkiaCanvas canvas)
        {
            var px = canvas.SkBitmap.Pixels;
            var n = 0;
            for (var i = 0; i < px.Length; i++)
                if (px[i].Alpha > 0 && maskPixels[i].Alpha > 0) n++;
            return n;
        }

        var ink = new Dictionary<string, object?> { ["inkColor"] = "#000000" };
        using var eyes = Surface(ctx => { DrawComicEye(ctx, head["farEye"]!, true, ink); DrawComicEye(ctx, head["nearEye"]!, false, ink); });
        using var brows = Surface(ctx =>
        {
            if (head.Contains("farBrow")) DrawComicBrow(ctx, head["farBrow"]!, true, ink);
            if (head.Contains("nearBrow")) DrawComicBrow(ctx, head["nearBrow"]!, false, ink);
        });
        using var nose = Surface(ctx => DrawComicNose(ctx, head["noseWedge"]!, ink));
        using var mouth = Surface(ctx => DrawComicMouth(ctx, head["mouthGuides"]!, ink));
        using var all = Surface(ctx =>
        {
            DrawComicEye(ctx, head["farEye"]!, true, ink); DrawComicEye(ctx, head["nearEye"]!, false, ink);
            if (head.Contains("farBrow")) DrawComicBrow(ctx, head["farBrow"]!, true, ink);
            if (head.Contains("nearBrow")) DrawComicBrow(ctx, head["nearBrow"]!, false, ink);
            DrawComicNose(ctx, head["noseWedge"]!, ink);
            DrawComicMouth(ctx, head["mouthGuides"]!, ink);
        });

        float Share(SkiaCanvas canvas) => facePixels == 0 ? 0f : Covered(canvas) / (float)facePixels;
        var share = Share(all);

        return new Dictionary<string, object?>
        {
            ["share"] = share,
            ["air"] = 1f - share,
            ["byFeature"] = new Dictionary<string, object?>
            {
                ["eyes"] = Share(eyes),
                ["brows"] = Share(brows),
                ["nose"] = Share(nose),
                ["mouth"] = Share(mouth)
            },
            ["face"] = geometry["face"],
            ["message"] = $"features cover {share:P1} of the face, hairline to chin"
        };
    }

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

        // A child's head is rounder than an adult's (Loomis, Figure Drawing p. 29): at three it is about
        // 0.8 of its height across, which is Loomis's six-eye skull rather than the comic five.
        var figureHead = Num(node, "rx", 0f) / MathF.Max(Num(node, "ry", 1f), 0.001f);
        var skull = opt?["skull"]?.ToString()?.Trim().ToLowerInvariant() ?? (figureHead > 0.786f ? "loomis" : "comic");
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

        var character = ChildFace(Num(JsInterop.AsDict(fig["build"]), "age", 18f));
        if (JsInterop.AsDict(opt?["character"]) is IDictionary given)
            foreach (DictionaryEntry e in given) character[e.Key.ToString()!] = e.Value;
        if (character.Count > 0)
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
}
