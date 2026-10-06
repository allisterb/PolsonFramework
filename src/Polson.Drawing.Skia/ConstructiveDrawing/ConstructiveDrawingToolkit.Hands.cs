namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

public partial class ConstructiveDrawingToolkit
{
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
}
