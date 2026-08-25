namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

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

        if (pt is IDictionary dict)
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
    public Dictionary<string, object?> createLoomisHead(float originX, float originY, float headHeight, float yawDeg = 35f, float pitchDeg = 0f)
    {
        var H = headHeight;
        var W = H * 0.72f;
        var rad = (yawDeg * MathF.PI) / 180f;
        var pitchRad = (pitchDeg * MathF.PI) / 180f;

        // 3/4 Yaw & Pitch Offsets
        var turnX = MathF.Sin(rad) * (W * 0.22f);
        var pitchY = MathF.Sin(pitchRad) * (H * 0.15f);
        var centerAxisX = originX + turnX;

        // Vertical Levels (Rule of Thirds)
        var yCrown = originY - H * 0.50f + pitchY;
        var yHairline = originY - H * 0.28f + pitchY * 0.8f;
        var yBrow = originY - H * 0.05f + pitchY * 0.5f;
        var yEye = originY + H * 0.02f + pitchY * 0.4f;
        var yNose = originY + H * 0.20f + pitchY * 0.2f;
        var yMouth = originY + H * 0.33f + pitchY * 0.1f;
        var yChin = originY + H * 0.50f;

        var eyeW = W * 0.20f;
        var farScale = MathF.Max(0.45f, MathF.Cos(rad));
        var eyeWFar = eyeW * farScale;

        var crownPt = new Point2D(originX, yCrown);
        var hairlinePt = new Point2D(centerAxisX, yHairline);
        var browCenterPt = new Point2D(centerAxisX, yBrow);
        var noseBasePt = new Point2D(centerAxisX, yNose);
        var mouthCenterPt = new Point2D(centerAxisX, yMouth);
        var chinPt = new Point2D(centerAxisX + turnX * 0.1f, yChin);

        // Near eye
        var nearInner = new Point2D(centerAxisX + eyeW * 0.40f, yEye);
        var nearOuter = new Point2D(centerAxisX + eyeW * 1.40f, yEye - 3f);
        var nearCenter = new Point2D(centerAxisX + eyeW * 0.90f, yEye);

        // Far eye
        var farInner = new Point2D(centerAxisX - eyeW * 0.35f, yEye);
        var farOuter = new Point2D(centerAxisX - eyeW * 0.35f - eyeWFar, yEye - 2f);
        var farCenter = new Point2D(centerAxisX - eyeW * 0.35f - eyeWFar * 0.5f, yEye);

        // Ear & Jaw
        var earPt = new Point2D(originX - W * 0.45f, (yBrow + yNose) * 0.5f);
        var jawAnglePt = new Point2D(originX - W * 0.28f, yNose + H * 0.08f);
        var cheekApexPt = new Point2D(centerAxisX - eyeWFar - W * 0.08f, yEye + H * 0.04f);

        // Nose Wedge
        var bridgeTopPt = new Point2D(centerAxisX, yBrow + (yEye - yBrow) * 0.5f);
        var noseApexPt = new Point2D(centerAxisX + turnX * 0.35f, yNose);
        var underNosePt = new Point2D(centerAxisX, yNose + H * 0.035f);
        var nearNostrilPt = new Point2D(centerAxisX + eyeW * 0.45f, yNose + H * 0.02f);

        // Mouth Guides
        var mouthLeft = new Point2D(centerAxisX - eyeW * 0.55f * farScale, yMouth);
        var mouthRight = new Point2D(centerAxisX + eyeW * 0.85f, yMouth + 1f);

        return new Dictionary<string, object?>
        {
            ["unit"] = new Dictionary<string, object?>
            {
                ["H"] = H,
                ["W"] = W,
                ["eyeW"] = eyeW,
                ["thirdH"] = H / 3f
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

    public void drawLoomisWireframe(CanvasRenderingContext2D ctx, object headObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (headObj is not IDictionary head)
            throw new ArgumentException("headObj must be a valid dictionary from createLoomisHead", nameof(headObj));

        var optDict = options as IDictionary;
        var blueLine = optDict?["blueLineColor"]?.ToString() ?? "#4a90e2";
        var graphite = optDict?["graphiteColor"]?.ToString() ?? "#444444";

        var unit = head["unit"] as IDictionary;
        var H = unit != null && unit.Contains("H") ? Convert.ToSingle(unit["H"], CultureInfo.InvariantCulture) : 200f;
        var W = unit != null && unit.Contains("W") ? Convert.ToSingle(unit["W"], CultureInfo.InvariantCulture) : 150f;

        var origin = ExtractPoint(head["origin"]);
        var crown = ExtractPoint(head["crown"]);
        var hairline = ExtractPoint(head["hairline"]);
        var brow = ExtractPoint(head["brow"]);
        var noseBase = ExtractPoint(head["noseBase"]);
        var mouthCenter = ExtractPoint(head["mouthCenter"]);
        var chin = ExtractPoint(head["chin"]);

        var jaw = head["jaw"] as IDictionary;
        var ear = ExtractPoint(jaw?["ear"]);
        var jawAngle = ExtractPoint(jaw?["angle"]);
        var cheekApex = ExtractPoint(jaw?["cheekApex"]);

        var nearEye = head["nearEye"] as IDictionary;
        var nearInner = ExtractPoint(nearEye?["inner"]);
        var nearOuter = ExtractPoint(nearEye?["outer"]);

        var farEye = head["farEye"] as IDictionary;
        var farInner = ExtractPoint(farEye?["inner"]);
        var farOuter = ExtractPoint(farEye?["outer"]);

        var noseWedge = head["noseWedge"] as IDictionary;
        var bridgeTop = ExtractPoint(noseWedge?["bridgeTop"]);
        var noseApex = ExtractPoint(noseWedge?["apex"]);

        var temporal = head["temporalOval"] as IDictionary;
        var tempCx = temporal != null && temporal.Contains("cx") ? Convert.ToSingle(temporal["cx"], CultureInfo.InvariantCulture) : origin.X;
        var tempCy = temporal != null && temporal.Contains("cy") ? Convert.ToSingle(temporal["cy"], CultureInfo.InvariantCulture) : origin.Y;
        var tempRx = temporal != null && temporal.Contains("rx") ? Convert.ToSingle(temporal["rx"], CultureInfo.InvariantCulture) : W * 0.3f;
        var tempRy = temporal != null && temporal.Contains("ry") ? Convert.ToSingle(temporal["ry"], CultureInfo.InvariantCulture) : H * 0.25f;

        ctx.save();

        // 1. Blue-line Construction Sphere & Proportions
        ctx.strokeStyle = blueLine;
        ctx.lineWidth = 1.5f;

        // Cranial Sphere
        ctx.beginPath();
        ctx.arc(origin.X, origin.Y - H * 0.08f, H * 0.42f, 0f, MathF.PI * 2f);
        ctx.stroke();

        // Temporal Slice Oval
        ctx.beginPath();
        ctx.ellipse(tempCx, tempCy, tempRx, tempRy, 0f, 0f, MathF.PI * 2f);
        ctx.stroke();

        // Central 3/4 Facial Meridian Arc
        ctx.beginPath();
        ctx.moveTo(crown.X, crown.Y);
        ctx.bezierCurveTo(hairline.X + 8f, hairline.Y, brow.X + 8f, brow.Y, noseBase.X, noseBase.Y);
        ctx.bezierCurveTo(mouthCenter.X, mouthCenter.Y, chin.X, chin.Y - 10f, chin.X, chin.Y);
        ctx.stroke();

        // Horizontal Guide Lines (Hairline, Brow, Eye, Nose, Mouth, Chin)
        void DrawGuide(Point2D pt, float widthSpan)
        {
            ctx.beginPath();
            ctx.moveTo(pt.X - widthSpan * 0.5f, pt.Y);
            ctx.lineTo(pt.X + widthSpan * 0.5f, pt.Y);
            ctx.stroke();
        }

        DrawGuide(hairline, W * 0.7f);
        DrawGuide(brow, W * 0.85f);
        DrawGuide(noseBase, W * 0.75f);
        DrawGuide(chin, W * 0.45f);

        // 2. Graphite Pencil Contours (Jawline, Eyes, Nose, Mouth, Ear)
        ctx.strokeStyle = graphite;
        ctx.lineWidth = 2.0f;

        // Jawline path: ear -> jaw angle -> chin -> cheek apex
        ctx.beginPath();
        ctx.moveTo(ear.X, ear.Y);
        ctx.lineTo(jawAngle.X, jawAngle.Y);
        ctx.quadraticCurveTo(chin.X - 10f, chin.Y + 4f, chin.X, chin.Y);
        ctx.quadraticCurveTo(chin.X + 15f, chin.Y - 5f, cheekApex.X, cheekApex.Y);
        ctx.stroke();

        // Eye Socket Guidelines
        ctx.beginPath();
        ctx.moveTo(nearInner.X, nearInner.Y);
        ctx.quadraticCurveTo(nearInner.X + (nearOuter.X - nearInner.X) * 0.5f, nearInner.Y - 12f, nearOuter.X, nearOuter.Y);
        ctx.moveTo(farInner.X, farInner.Y);
        ctx.quadraticCurveTo(farInner.X + (farOuter.X - farInner.X) * 0.5f, farInner.Y - 10f, farOuter.X, farOuter.Y);
        ctx.stroke();

        // Nose Wedge Guideline
        ctx.beginPath();
        ctx.moveTo(bridgeTop.X, bridgeTop.Y);
        ctx.lineTo(noseApex.X, noseApex.Y);
        ctx.lineTo(noseBase.X, noseBase.Y);
        ctx.stroke();

        // Ear Outline
        ctx.beginPath();
        ctx.arc(ear.X, ear.Y, H * 0.08f, 0f, MathF.PI * 2f);
        ctx.stroke();

        ctx.restore();
    }
    #endregion

    #region Comic Feature Drawing Helpers
    public void drawComicEye(CanvasRenderingContext2D ctx, object eyeObj, bool isFar = false, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (eyeObj is not IDictionary eye) return;

        var optDict = options as IDictionary;
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var irisColor = optDict?["irisColor"]?.ToString() ?? "#3b6884";
        var scleraColor = optDict?["scleraColor"]?.ToString() ?? "#f1f4f7";

        var inner = ExtractPoint(eye["inner"]);
        var outer = ExtractPoint(eye["outer"]);
        var center = ExtractPoint(eye["center"]);

        var w = MathF.Abs(outer.X - inner.X);
        if (w <= 0.1f) w = 24f;
        var dir = outer.X > inner.X ? 1f : -1f;

        ctx.save();

        // 1. Sclera fill inside eyelid bounds
        ctx.save();
        ctx.beginPath();
        ctx.moveTo(inner.X, inner.Y);
        ctx.bezierCurveTo(inner.X + dir * w * 0.3f, inner.Y - w * 0.45f, inner.X + dir * w * 0.7f, inner.Y - w * 0.40f, outer.X, outer.Y);
        ctx.quadraticCurveTo(center.X, inner.Y + w * 0.25f, inner.X, inner.Y);
        ctx.closePath();
        ctx.fillStyle = scleraColor;
        ctx.fill();
        ctx.clip();

        // 2. Iris & Pupil
        var irisR = w * 0.32f;
        var irisX = center.X + dir * w * 0.08f;
        var irisY = center.Y - 1f;

        // Iris
        ctx.beginPath();
        ctx.arc(irisX, irisY, irisR, 0f, MathF.PI * 2f);
        ctx.fillStyle = irisColor;
        ctx.fill();

        // Dark iris rim
        ctx.strokeStyle = inkColor;
        ctx.lineWidth = 1.2f;
        ctx.stroke();

        // Pupil
        ctx.beginPath();
        ctx.arc(irisX, irisY, irisR * 0.45f, 0f, MathF.PI * 2f);
        ctx.fillStyle = inkColor;
        ctx.fill();

        // White catchlight
        ctx.beginPath();
        ctx.arc(irisX - irisR * 0.25f, irisY - irisR * 0.25f, irisR * 0.22f, 0f, MathF.PI * 2f);
        ctx.fillStyle = "#ffffff";
        ctx.fill();

        ctx.restore();

        // 3. Thick Inked S-Curve Upper Eyelid
        ctx.strokeStyle = inkColor;
        ctx.lineWidth = isFar ? 2.6f : 3.8f;
        ctx.lineCap = "round";
        ctx.beginPath();
        ctx.moveTo(inner.X, inner.Y);
        ctx.bezierCurveTo(inner.X + dir * w * 0.3f, inner.Y - w * 0.45f, inner.X + dir * w * 0.7f, inner.Y - w * 0.40f, outer.X, outer.Y);
        ctx.stroke();

        // 4. Delicate Lower Eyelid
        ctx.lineWidth = 1.6f;
        ctx.beginPath();
        ctx.moveTo(inner.X + dir * w * 0.2f, inner.Y + w * 0.15f);
        ctx.quadraticCurveTo(center.X, inner.Y + w * 0.25f, outer.X - dir * w * 0.1f, outer.Y);
        ctx.stroke();

        ctx.restore();
    }

    public void drawComicNose(CanvasRenderingContext2D ctx, object noseObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (noseObj is not IDictionary nose) return;

        var optDict = options as IDictionary;
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var shadowColor = optDict?["shadowColor"]?.ToString() ?? "#b06f4c";

        var bridgeTop = ExtractPoint(nose["bridgeTop"]);
        var apex = ExtractPoint(nose["apex"]);
        var underNose = ExtractPoint(nose["underNose"]);
        var nearNostril = ExtractPoint(nose["nearNostril"]);

        ctx.save();

        // Shaded Under-Nose Plane
        ctx.fillStyle = shadowColor;
        ctx.beginPath();
        ctx.moveTo(apex.X, apex.Y);
        ctx.lineTo(nearNostril.X, nearNostril.Y);
        ctx.lineTo(underNose.X, underNose.Y);
        ctx.closePath();
        ctx.fill();

        // Inked Nose Bridge
        ctx.strokeStyle = inkColor;
        ctx.lineWidth = 2.4f;
        ctx.lineCap = "round";
        ctx.beginPath();
        ctx.moveTo(bridgeTop.X, bridgeTop.Y);
        ctx.lineTo(apex.X, apex.Y);
        ctx.lineTo(underNose.X, underNose.Y);
        ctx.stroke();

        // Nostril Teardrop
        ctx.lineWidth = 2.0f;
        ctx.beginPath();
        ctx.arc(nearNostril.X, nearNostril.Y, 3.5f, 0.2f, MathF.PI * 1.5f);
        ctx.stroke();

        ctx.restore();
    }

    public void drawComicMouth(CanvasRenderingContext2D ctx, object mouthObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (mouthObj is not IDictionary mouth) return;

        var optDict = options as IDictionary;
        var inkColor = optDict?["inkColor"]?.ToString() ?? "#0a0a0c";
        var lipColor = optDict?["lipColor"]?.ToString() ?? "#b84848";
        var teethColor = optDict?["teethColor"]?.ToString() ?? "#fbf8ee";
        var cavityColor = optDict?["cavityColor"]?.ToString() ?? "#3a1215";

        var center = ExtractPoint(mouth["center"]);
        var left = ExtractPoint(mouth["leftCorner"]);
        var right = ExtractPoint(mouth["rightCorner"]);

        ctx.save();

        // 1. Mouth Opening / Cavity
        ctx.fillStyle = cavityColor;
        ctx.beginPath();
        ctx.moveTo(left.X, left.Y);
        ctx.quadraticCurveTo(center.X, center.Y - 2f, right.X, right.Y);
        ctx.quadraticCurveTo(center.X, center.Y + 12f, left.X, left.Y);
        ctx.closePath();
        ctx.fill();

        // 2. Teeth Shelf (Upper teeth band)
        ctx.fillStyle = teethColor;
        ctx.beginPath();
        ctx.moveTo(left.X + 3f, left.Y);
        ctx.quadraticCurveTo(center.X, center.Y - 2f, right.X - 3f, right.Y);
        ctx.lineTo(right.X - 4f, right.Y + 4f);
        ctx.quadraticCurveTo(center.X, center.Y + 3f, left.X + 4f, left.Y + 3f);
        ctx.closePath();
        ctx.fill();

        // 3. Inked Mouth Outline & Upper Lip
        ctx.strokeStyle = inkColor;
        ctx.lineWidth = 2.4f;
        ctx.lineCap = "round";
        ctx.beginPath();
        ctx.moveTo(left.X, left.Y);
        ctx.quadraticCurveTo(center.X, center.Y - 2f, right.X, right.Y);
        ctx.stroke();

        // 4. Lower Lip Shadow Crescent
        ctx.fillStyle = lipColor;
        ctx.beginPath();
        ctx.arc(center.X, center.Y + 16f, 6f, 0.2f, MathF.PI - 0.2f);
        ctx.fill();

        ctx.restore();
    }
    #endregion

    #region Tapered Strokes, Feathering & Hair Ribbons
    public void drawTaperedStroke(CanvasRenderingContext2D ctx, object start, object cp1, object cp2, object end, float maxThickness, object? fillOrStrokeStyle = null)
    {
        var s = ExtractPoint(start);
        var c1 = ExtractPoint(cp1);
        var c2 = ExtractPoint(cp2);
        var e = ExtractPoint(end);
        drawTaperedStroke(ctx, s.X, s.Y, c1.X, c1.Y, c2.X, c2.Y, e.X, e.Y, maxThickness, fillOrStrokeStyle);
    }

    public void drawTaperedStroke(CanvasRenderingContext2D ctx, float sx, float sy, float cp1x, float cp1y, float cp2x, float cp2y, float ex, float ey, float maxThickness, object? fillOrStrokeStyle = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
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

        ctx.save();
        ctx.beginPath();
        ctx.moveTo(points[0].x, points[0].y);

        // Forward pass along positive normal
        for (var i = 0; i <= steps; i++)
        {
            var p = points[i];
            ctx.lineTo(p.x + p.nx * (p.thickness * 0.5f), p.y + p.ny * (p.thickness * 0.5f));
        }

        // Backward pass along negative normal
        for (var i = steps; i >= 0; i--)
        {
            var p = points[i];
            ctx.lineTo(p.x - p.nx * (p.thickness * 0.5f), p.y - p.ny * (p.thickness * 0.5f));
        }

        ctx.closePath();

        if (fillOrStrokeStyle != null)
            ctx.fillStyle = fillOrStrokeStyle;

        ctx.fill();
        ctx.restore();
    }

    public void drawFeathering(CanvasRenderingContext2D ctx, object origin, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float lineWidth = 1.2f)
    {
        var pt = ExtractPoint(origin);
        drawFeathering(ctx, pt.X, pt.Y, angleDeg, count, length, spacing, strokeColor, lineWidth);
    }

    public void drawFeathering(CanvasRenderingContext2D ctx, float ox, float oy, float angleDeg, int count, float length, float spacing, object? strokeColor = null, float lineWidth = 1.2f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var rad = (angleDeg * MathF.PI) / 180f;
        var dx = MathF.Cos(rad);
        var dy = MathF.Sin(rad);
        var px = -dy;
        var py = dx;

        ctx.save();
        if (strokeColor != null) ctx.strokeStyle = strokeColor;
        ctx.lineWidth = lineWidth;
        ctx.lineCap = "round";

        for (var i = 0; i < count; i++)
        {
            var sx = ox + px * (i * spacing);
            var sy = oy + py * (i * spacing);
            var lenVar = length * (0.8f + 0.4f * MathF.Sin(i * 1.5f));
            var ex = sx + dx * lenVar;
            var ey = sy + dy * lenVar;

            ctx.beginPath();
            ctx.moveTo(sx, sy);
            ctx.lineTo(ex, ey);
            ctx.stroke();
        }

        ctx.restore();
    }

    public void drawCrossContourHatch(CanvasRenderingContext2D ctx, float cx, float cy, float rx, float ry, float startAngle, float endAngle, int count = 8, object? strokeColor = null, float lineWidth = 1.2f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ctx.save();
        if (strokeColor != null) ctx.strokeStyle = strokeColor;
        ctx.lineWidth = lineWidth;

        for (var i = 0; i < count; i++)
        {
            var t = (float)i / Math.Max(1, count - 1);
            var y = cy - ry * 0.5f + t * ry;
            ctx.beginPath();
            ctx.ellipse(cx, y, rx, ry * 0.25f, 0f, startAngle, endAngle);
            ctx.stroke();
        }

        ctx.restore();
    }

    public void drawHairRibbon(CanvasRenderingContext2D ctx, object root, object tip, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f)
    {
        var r = ExtractPoint(root);
        var t = ExtractPoint(tip);
        drawHairRibbon(ctx, r.X, r.Y, t.X, t.Y, bendFactor, width, fillTop, fillUnderside, strokeColor, strokeWidth);
    }

    public void drawHairRibbon(CanvasRenderingContext2D ctx, float rx, float ry, float tx, float ty, float bendFactor, float width, object fillTop, object fillUnderside, object? strokeColor = null, float strokeWidth = 2.0f)
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

        ctx.save();

        // 1. Top Ribbon Surface
        ctx.beginPath();
        ctx.moveTo(rx, ry);
        ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, tx, ty);
        ctx.bezierCurveTo(cp3x, cp3y, cp4x, cp4y, rootLowerX, rootLowerY);
        ctx.closePath();

        ctx.fillStyle = fillTop;
        ctx.fill();

        if (strokeColor != null)
        {
            ctx.strokeStyle = strokeColor;
            ctx.lineWidth = strokeWidth;
            ctx.lineJoin = "round";
            ctx.stroke();
        }

        // 2. Inner Highlight / Underside Streak
        ctx.beginPath();
        ctx.moveTo(rx + dx * 0.15f + nx * (width * 0.3f), ry + dy * 0.15f + ny * (width * 0.3f));
        ctx.bezierCurveTo(
            cp1x + nx * (width * 0.2f), cp1y + ny * (width * 0.2f),
            cp2x + nx * (width * 0.2f), cp2y + ny * (width * 0.2f),
            tx - dx * 0.1f, ty - dy * 0.1f
        );
        ctx.strokeStyle = fillUnderside;
        ctx.lineWidth = MathF.Max(1.2f, strokeWidth * 0.75f);
        ctx.stroke();

        ctx.restore();
    }
    #endregion

    #region Procedural Shaders & Material Presets
    public SKShader createHalftoneDotShader(object? options = null)
    {
        var optDict = options as IDictionary;
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
        return skiaShaderApi.sksl(sksl, uniforms);
    }

    public SKShader createRopeFiberShader(float frequencyX = 0.08f, float frequencyY = 0.40f, int octaves = 3, int seed = 42)
    {
        var skiaShaderApi = new SkiaShaderApi();
        return skiaShaderApi.perlinNoiseTurbulence(frequencyX, frequencyY, octaves, seed);
    }

    public SKShader createAtmosphericCloudShader(float frequencyX = 0.015f, float frequencyY = 0.015f, int octaves = 4, int seed = 101)
    {
        var skiaShaderApi = new SkiaShaderApi();
        return skiaShaderApi.perlinNoiseFractal(frequencyX, frequencyY, octaves, seed);
    }
    #endregion

    #region Visual Measurement Helpers
    public Dictionary<string, object?> verifyPlumbAlignment(object topPoint, object bottomPoint, float maxTolerance = 12f)
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

    public float computeRelativeDistance(float headHeight, object pointA, object pointB)
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
    private static Point2D LineIntersection(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
    {
        var denom = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
        if (MathF.Abs(denom) < 0.00001f) return new Point2D((p1.X + p3.X) * 0.5f, (p1.Y + p3.Y) * 0.5f);

        var t = ((p1.X - p3.X) * (p3.Y - p4.Y) - (p1.Y - p3.Y) * (p3.X - p4.X)) / denom;
        return new Point2D(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
    }

    public Dictionary<string, object?> createPerspectiveGrid(object? options = null)
    {
        var opt = options as IDictionary;
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

    public void drawPerspectiveGrid(CanvasRenderingContext2D ctx, object gridObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (gridObj is not IDictionary grid) return;

        var opt = options as IDictionary;
        var lineColor = opt?["lineColor"]?.ToString() ?? "#dbe7f2";
        var horizonColor = opt?["horizonColor"]?.ToString() ?? "#4a90e2";
        var lineCount = opt != null && opt.Contains("lineCount") ? Convert.ToInt32(opt["lineCount"]) : 14;
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1f;

        var horizonY = Convert.ToSingle(grid["horizonY"], CultureInfo.InvariantCulture);
        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);

        ctx.save();

        // 1. Vanishing Point Rays
        ctx.strokeStyle = lineColor;
        ctx.lineWidth = lineWidth;

        var w = ctx.Canvas.Width;
        var h = ctx.Canvas.Height;

        for (var i = 0; i <= lineCount; i++)
        {
            var targetY = horizonY + (i + 1) * ((h - horizonY) / lineCount);
            // Left VP rays
            ctx.beginPath();
            ctx.moveTo(vpL.X, vpL.Y);
            ctx.lineTo(w + 200f, targetY);
            ctx.stroke();

            // Right VP rays
            ctx.beginPath();
            ctx.moveTo(vpR.X, vpR.Y);
            ctx.lineTo(-200f, targetY);
            ctx.stroke();
        }

        // 2. Horizon Line
        ctx.strokeStyle = horizonColor;
        ctx.lineWidth = lineWidth * 1.5f;
        ctx.beginPath();
        ctx.moveTo(0, horizonY);
        ctx.lineTo(w, horizonY);
        ctx.stroke();

        ctx.restore();
    }

    public Dictionary<string, object?> createPerspectiveBox(object gridObj, float anchorX, float anchorY, float width, float height, float depth)
    {
        if (gridObj is not IDictionary grid)
            throw new ArgumentException("gridObj must be a valid perspective grid dictionary", nameof(gridObj));

        var vpL = ExtractPoint(grid["vpL"]);
        var vpR = ExtractPoint(grid["vpR"]);

        var v0 = new Point2D(anchorX, anchorY);
        var v4 = new Point2D(anchorX, anchorY - height);

        // Direction vectors to VPs
        var dxL = vpL.X - v0.X;
        var dyL = vpL.Y - v0.Y;
        var lenL = MathF.Sqrt(dxL * dxL + dyL * dyL);
        if (lenL < 0.0001f) lenL = 1f;
        var tL = MathF.Min(0.85f, width / lenL);
        var v1 = new Point2D(v0.X + dxL * tL, v0.Y + dyL * tL);

        var dxR = vpR.X - v0.X;
        var dyR = vpR.Y - v0.Y;
        var lenR = MathF.Sqrt(dxR * dxR + dyR * dyR);
        if (lenR < 0.0001f) lenR = 1f;
        var tR = MathF.Min(0.85f, depth / lenR);
        var v2 = new Point2D(v0.X + dxR * tR, v0.Y + dyR * tR);

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

    public void drawPerspectiveBox(CanvasRenderingContext2D ctx, object boxObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (boxObj is not IDictionary box) return;

        var opt = options as IDictionary;
        var topFill = opt?["topFill"]?.ToString() ?? "#e6f0fa";
        var leftFill = opt?["leftFill"]?.ToString() ?? "#b8d5f2";
        var rightFill = opt?["rightFill"]?.ToString() ?? "#7baad4";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;
        var drawHidden = opt != null && opt.Contains("drawHiddenLines") && Convert.ToBoolean(opt["drawHiddenLines"]);

        var faces = box["faces"] as IDictionary;
        if (faces == null) return;

        void RenderFace(object? faceObj, object? fill)
        {
            if (faceObj is not IList pts || pts.Count < 3) return;
            ctx.beginPath();
            var first = ExtractPoint(pts[0]);
            ctx.moveTo(first.X, first.Y);
            for (var i = 1; i < pts.Count; i++)
            {
                var p = ExtractPoint(pts[i]);
                ctx.lineTo(p.X, p.Y);
            }
            ctx.closePath();
            if (fill != null)
            {
                ctx.fillStyle = fill;
                ctx.fill();
            }
            if (strokeColor != null)
            {
                ctx.strokeStyle = strokeColor;
                ctx.lineWidth = strokeWidth;
                ctx.lineJoin = "round";
                ctx.stroke();
            }
        }

        ctx.save();

        if (drawHidden)
        {
            ctx.save();
            ctx.strokeStyle = "#9bbcd9";
            ctx.lineWidth = strokeWidth * 0.7f;
            RenderFace(faces["bottom"], null);
            RenderFace(faces["backLeft"], null);
            RenderFace(faces["backRight"], null);
            ctx.restore();
        }

        RenderFace(faces["left"], leftFill);
        RenderFace(faces["right"], rightFill);
        RenderFace(faces["top"], topFill);

        ctx.restore();
    }

    public void drawPerspectiveCylinder(CanvasRenderingContext2D ctx, object gridObj, float anchorX, float anchorY, float radius, float height, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = options as IDictionary;
        var sideFill = opt?["sideFill"]?.ToString() ?? "#b8d5f2";
        var topFill = opt?["topFill"]?.ToString() ?? "#e6f0fa";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;

        var box = createPerspectiveBox(gridObj, anchorX, anchorY, radius * 2f, height, radius * 2f);
        var verts = (IList)box["vertices"]!;

        var v0 = ExtractPoint(verts[0]);
        var v1 = ExtractPoint(verts[1]);
        var v2 = ExtractPoint(verts[2]);
        var v3 = ExtractPoint(verts[3]);
        var v4 = ExtractPoint(verts[4]);
        var v5 = ExtractPoint(verts[5]);
        var v6 = ExtractPoint(verts[6]);
        var v7 = ExtractPoint(verts[7]);

        // Bottom center & top center
        var botCenter = new Point2D((v0.X + v3.X) * 0.5f, (v0.Y + v3.Y) * 0.5f);
        var topCenter = new Point2D((v4.X + v7.X) * 0.5f, (v4.Y + v7.Y) * 0.5f);

        var rx = MathF.Abs(v2.X - v1.X) * 0.5f;
        var ry = MathF.Abs(v0.Y - v3.Y) * 0.45f;

        ctx.save();

        // 1. Cylinder Body
        ctx.fillStyle = sideFill;
        ctx.beginPath();
        ctx.moveTo(v1.X, botCenter.Y);
        ctx.lineTo(v5.X, topCenter.Y);
        ctx.ellipse(topCenter.X, topCenter.Y, rx, ry, 0f, 0f, MathF.PI);
        ctx.lineTo(v2.X, botCenter.Y);
        ctx.ellipse(botCenter.X, botCenter.Y, rx, ry, 0f, 0f, MathF.PI);
        ctx.closePath();
        ctx.fill();

        // Contour edges
        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = strokeWidth;
        ctx.beginPath();
        ctx.moveTo(v1.X, botCenter.Y);
        ctx.lineTo(v5.X, topCenter.Y);
        ctx.moveTo(v2.X, botCenter.Y);
        ctx.lineTo(v6.X, topCenter.Y);
        ctx.stroke();

        // Bottom ellipse arc
        ctx.beginPath();
        ctx.ellipse(botCenter.X, botCenter.Y, rx, ry, 0f, 0f, MathF.PI);
        ctx.stroke();

        // 2. Top Elliptical Cap
        ctx.fillStyle = topFill;
        ctx.beginPath();
        ctx.ellipse(topCenter.X, topCenter.Y, rx, ry, 0f, 0f, MathF.PI * 2f);
        ctx.fill();
        ctx.stroke();

        ctx.restore();
    }

    public List<List<Dictionary<string, object?>>> subdividePerspectiveQuad(object quadObj, int uCount, int vCount)
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

    public Dictionary<string, object?> verifyPerspectiveConvergence(object linesList, object expectedVp, float maxToleranceDeg = 5f)
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

            var drawnAngle = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
            var targetAngle = MathF.Atan2(vp.Y - a.Y, vp.X - a.X) * 180f / MathF.PI;
            var diff = MathF.Abs(drawnAngle - targetAngle);
            if (diff > 180f) diff = 360f - diff;

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
}

