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
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a valid dictionary from createLoomisHead", nameof(headObj));

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
        if (JsInterop.AsDict(eyeObj) is not IDictionary eye) return;

        var optDict = JsInterop.AsDict(options);
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
        if (JsInterop.AsDict(noseObj) is not IDictionary nose) return;

        var optDict = JsInterop.AsDict(options);
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
        if (JsInterop.AsDict(mouthObj) is not IDictionary mouth) return;

        var optDict = JsInterop.AsDict(options);
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

    public void drawPerspectiveGrid(CanvasRenderingContext2D ctx, object gridObj, object? options = null)
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
        if (JsInterop.AsDict(gridObj) is not IDictionary grid)
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
        if (JsInterop.AsDict(boxObj) is not IDictionary box) return;

        var opt = JsInterop.AsDict(options);
        var topFill = opt?["topFill"]?.ToString() ?? "#e6f0fa";
        var leftFill = opt?["leftFill"]?.ToString() ?? "#b8d5f2";
        var rightFill = opt?["rightFill"]?.ToString() ?? "#7baad4";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;
        var drawHidden = opt != null && opt.Contains("drawHiddenLines") && Convert.ToBoolean(opt["drawHiddenLines"]);

        var faces = JsInterop.AsDict(box["faces"]);
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
        var opt = JsInterop.AsDict(options);
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
        // Left contour up, across the FRONT of the top ellipse, right contour down, then back
        // along the front of the bottom ellipse. Both arcs must start at the tangent extreme they
        // meet (angle PI on the left, 0 on the right) — sweeping either from the far side folds the
        // body into a bowtie. The contours sit at center +/- rx so they stay tangent to the arcs.
        ctx.moveTo(botCenter.X - rx, botCenter.Y);
        ctx.lineTo(topCenter.X - rx, topCenter.Y);
        ctx.ellipse(topCenter.X, topCenter.Y, rx, ry, 0f, MathF.PI, 0f, true);
        ctx.lineTo(botCenter.X + rx, botCenter.Y);
        ctx.ellipse(botCenter.X, botCenter.Y, rx, ry, 0f, 0f, MathF.PI);
        ctx.closePath();
        ctx.fill();

        // Contour edges
        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = strokeWidth;
        ctx.beginPath();
        ctx.moveTo(botCenter.X - rx, botCenter.Y);
        ctx.lineTo(topCenter.X - rx, topCenter.Y);
        ctx.moveTo(botCenter.X + rx, botCenter.Y);
        ctx.lineTo(topCenter.X + rx, topCenter.Y);
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

    #region Volumetric Lighting & Cast Shadows
    public Dictionary<string, object?> projectCastShadow(object lightSource, float groundY, object objectVerticesOrBounds, object? options = null)
    {
        var light = ExtractPoint(lightSource);
        var opt = JsInterop.AsDict(options);
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#1c2733";
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.65f;
        var penumbraBlur = opt != null && opt.Contains("penumbraBlur") ? Convert.ToSingle(opt["penumbraBlur"], CultureInfo.InvariantCulture) : 6f;

        // Every light ray meets the ground line at groundY, so the projected vertices are collinear:
        // a ground plane seen edge-on in a 2D elevation genuinely has no thickness. The footprint only
        // becomes a fillable shape once the ground is given an apparent depth, which cannot be derived
        // from 2D inputs — groundDepth supplies it. The light ray still sets the shadow's length and
        // direction; groundDepth only adds the dimension the projection cannot know.
        var basePts = new List<Point2D>();
        var farPts = new List<Point2D>();

        Point2D ProjectToGround(Point2D top)
        {
            var dy = top.Y - light.Y;
            if (MathF.Abs(dy) < 0.001f) dy = 0.001f;
            var t = (groundY - light.Y) / dy;
            return new Point2D(light.X + t * (top.X - light.X), groundY);
        }

        if (JsInterop.AsDict(objectVerticesOrBounds) is IDictionary b && b.Contains("width") && b.Contains("height"))
        {
            var bx = Convert.ToSingle(b["x"], CultureInfo.InvariantCulture);
            var by = Convert.ToSingle(b["y"], CultureInfo.InvariantCulture);
            var bw = Convert.ToSingle(b["width"], CultureInfo.InvariantCulture);

            basePts.Add(new Point2D(bx, groundY));
            basePts.Add(new Point2D(bx + bw, groundY));
            farPts.Add(ProjectToGround(new Point2D(bx, by)));
            farPts.Add(ProjectToGround(new Point2D(bx + bw, by)));
        }
        else if (objectVerticesOrBounds is IList list)
        {
            foreach (var item in list)
            {
                var pt = ExtractPoint(item);
                basePts.Add(new Point2D(pt.X, groundY));
                farPts.Add(ProjectToGround(pt));
            }
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

            // Contact edge along the ground line, then the projected edge back the other way, lowered
            // by groundDepth so the band recedes toward the viewer.
            foreach (var pt in basePts) shadowPts.Add(ToDict(pt));
            for (var i = farPts.Count - 1; i >= 0; i--)
            {
                shadowPts.Add(ToDict(new Point2D(farPts[i].X, groundY + groundDepth)));
            }
        }

        return new Dictionary<string, object?>
        {
            ["shadowPolygon"] = shadowPts,
            ["groundY"] = groundY,
            ["groundDepth"] = groundDepth,
            ["shadowColor"] = shadowColor,
            ["opacity"] = opacity,
            ["penumbraBlur"] = penumbraBlur
        };
    }

    public void drawCastShadow(CanvasRenderingContext2D ctx, object shadowPolygonOrResult, object? options = null)
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
                $"drawCastShadow needs a polygon of at least 3 points, got {pts?.Count ?? 0}. " +
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
                $"drawCastShadow was given a degenerate polygon ({spanX:F2} x {spanY:F2}px) that would fill nothing. " +
                "A ground plane seen edge-on projects to a line: pass a 'groundDepth' option to " +
                "Drawing.projectCastShadow(...) to give the footprint depth.",
                nameof(shadowPolygonOrResult));
        }

        ctx.save();
        ctx.globalAlpha = opacity;
        ctx.fillStyle = shadowColor;
        if (blur > 0.5f)
        {
            ctx.shadowColor = shadowColor;
            ctx.shadowBlur = blur;
        }

        ctx.beginPath();
        var first = ExtractPoint(pts[0]);
        ctx.moveTo(first.X, first.Y);
        for (var i = 1; i < pts.Count; i++)
        {
            var p = ExtractPoint(pts[i]);
            ctx.lineTo(p.X, p.Y);
        }
        ctx.closePath();
        ctx.fill();

        ctx.restore();
    }

    public void renderVolumetricSphere(CanvasRenderingContext2D ctx, float cx, float cy, float radius, object? lightDirection = null, object? options = null)
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

        ctx.save();

        // 1. Ground Cast Shadow (if enabled)
        if (drawGroundShadow)
        {
            var groundY = cy + radius + 8f;
            var shadowCx = cx - lx * (radius * 0.7f);
            var shadowRx = radius * 1.15f;
            var shadowRy = radius * 0.28f;

            ctx.save();
            ctx.fillStyle = "#1c140e";
            ctx.globalAlpha = 0.55f;
            ctx.shadowColor = "#1c140e";
            ctx.shadowBlur = 8f;
            ctx.beginPath();
            ctx.ellipse(shadowCx, groundY, shadowRx, shadowRy, 0f, 0f, MathF.PI * 2f);
            ctx.fill();
            ctx.restore();
        }

        // 2. Base Sphere Fill & 3D Lighting Radial Gradient
        var lightSpotX = cx + lx * (radius * 0.45f);
        var lightSpotY = cy + ly * (radius * 0.45f);

        var grad = ctx.createRadialGradient(lightSpotX, lightSpotY, radius * 0.1f, cx, cy, radius * 1.05f);
        grad.addColorStop(0.0f, highlightColor);
        grad.addColorStop(0.35f, baseColor);
        grad.addColorStop(0.75f, shadowColor);
        grad.addColorStop(1.0f, shadowColor);

        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.fill();

        // 3. Ambient Bounce / Reflected Light on shadow side
        var bounceSpotX = cx - lx * (radius * 0.65f);
        var bounceSpotY = cy - ly * (radius * 0.65f);

        ctx.save();
        ctx.beginPath();
        ctx.arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.clip();

        var bounceGrad = ctx.createRadialGradient(bounceSpotX, bounceSpotY, radius * 0.05f, bounceSpotX, bounceSpotY, radius * 0.75f);
        bounceGrad.addColorStop(0.0f, bounceColor);
        bounceGrad.addColorStop(1.0f, "rgba(0,0,0,0)");

        ctx.globalAlpha = 0.45f;
        ctx.globalCompositeOperation = "screen";
        ctx.fillStyle = bounceGrad;
        ctx.beginPath();
        ctx.arc(cx, cy, radius, 0f, MathF.PI * 2f);
        ctx.fill();
        ctx.restore();

        // 4. Specular Highlight Glint
        ctx.save();
        ctx.fillStyle = "#ffffff";
        ctx.globalAlpha = 0.85f;
        ctx.beginPath();
        ctx.ellipse(lightSpotX - 2f, lightSpotY - 2f, radius * 0.18f, radius * 0.12f, -0.3f, 0f, MathF.PI * 2f);
        ctx.fill();
        ctx.restore();

        ctx.restore();
    }

    public void renderVolumetricCylinder(CanvasRenderingContext2D ctx, float x, float y, float width, float height, object? lightDirection = null, object? options = null)
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

        ctx.save();

        // 1. Cylinder Body Longitudinal Gradient
        var grad = ctx.createLinearGradient(x, y, x + width, y);
        grad.addColorStop(0.0f, shadowColor);
        grad.addColorStop(0.25f, highlightColor);
        grad.addColorStop(0.55f, baseColor);
        grad.addColorStop(0.85f, shadowColor);
        grad.addColorStop(1.0f, bounceColor);

        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.moveTo(x, y + ry);
        ctx.lineTo(x, y + height - ry);
        ctx.ellipse(cx, y + height - ry, rx, ry, 0f, 0f, MathF.PI);
        ctx.lineTo(x + width, y + ry);
        ctx.ellipse(cx, y + ry, rx, ry, 0f, 0f, MathF.PI);
        ctx.closePath();
        ctx.fill();

        // 2. Top Elliptical Cap
        var topGrad = ctx.createRadialGradient(cx, y + ry * 0.6f, rx * 0.1f, cx, y + ry, rx);
        topGrad.addColorStop(0.0f, highlightColor);
        topGrad.addColorStop(1.0f, baseColor);

        ctx.fillStyle = topGrad;
        ctx.beginPath();
        ctx.ellipse(cx, y + ry, rx, ry, 0f, 0f, MathF.PI * 2f);
        ctx.fill();

        // Top cap stroke
        ctx.strokeStyle = shadowColor;
        ctx.lineWidth = 1.5f;
        ctx.stroke();

        ctx.restore();
    }

    public Dictionary<string, object?> createThreePointLighting(object? options = null)
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

    public void drawRimLight(CanvasRenderingContext2D ctx, object boundsOrPts, float lightAngleDeg, object? rimColor = null, float thickness = 2.5f)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var color = rimColor?.ToString() ?? "#ffffff";
        var rad = (lightAngleDeg * MathF.PI) / 180f;
        var nx = MathF.Cos(rad);
        var ny = MathF.Sin(rad);

        ctx.save();
        ctx.strokeStyle = color;
        ctx.lineWidth = thickness;
        ctx.lineCap = "round";

        if (JsInterop.AsDict(boundsOrPts) is IDictionary b && b.Contains("x") && b.Contains("width"))
        {
            var bx = Convert.ToSingle(b["x"], CultureInfo.InvariantCulture);
            var by = Convert.ToSingle(b["y"], CultureInfo.InvariantCulture);
            var bw = Convert.ToSingle(b["width"], CultureInfo.InvariantCulture);
            var bh = Convert.ToSingle(b["height"], CultureInfo.InvariantCulture);

            ctx.beginPath();
            if (nx > 0)
            {
                ctx.moveTo(bx + bw, by + 4f);
                ctx.lineTo(bx + bw, by + bh - 4f);
            }
            else
            {
                ctx.moveTo(bx, by + 4f);
                ctx.lineTo(bx, by + bh - 4f);
            }
            ctx.stroke();
        }
        else if (boundsOrPts is IList pts && pts.Count >= 2)
        {
            ctx.beginPath();
            var first = ExtractPoint(pts[0]);
            ctx.moveTo(first.X + nx * 2f, first.Y + ny * 2f);
            for (var i = 1; i < pts.Count; i++)
            {
                var p = ExtractPoint(pts[i]);
                ctx.lineTo(p.X + nx * 2f, p.Y + ny * 2f);
            }
            ctx.stroke();
        }

        ctx.restore();
    }

    public SKShader createVolumetricSphereShader(object? options = null)
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
        return skiaShaderApi.sksl(sksl, uniforms);
    }
    #endregion

    #region Full-Body Anatomy, Mannequins & Expressions
    public Dictionary<string, object?> createMannequinFigure(float originX, float originY, float totalHeight = 560f, object? options = null)
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
        var shoulderSpan = H * 1.8f;
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

        return new Dictionary<string, object?>
        {
            ["headUnit"] = H,
            ["totalHeight"] = totalHeight,
            ["head"] = new Dictionary<string, object?> { ["center"] = ToDict(headCenter), ["rx"] = H * 0.36f, ["ry"] = H * 0.50f },
            ["neck"] = ToDict(neckCenter),
            ["sternum"] = ToDict(sternalNotch),
            ["clavicles"] = new Dictionary<string, object?> { ["left"] = ToDict(leftShoulder), ["right"] = ToDict(rightShoulder), ["center"] = ToDict(sternalNotch) },
            ["ribcage"] = new Dictionary<string, object?> { ["center"] = ToDict(ribcageCenter), ["rx"] = ribcageRx, ["ry"] = ribcageRy, ["tiltDeg"] = shoulderTiltDeg },
            ["navel"] = ToDict(navel),
            ["pelvis"] = new Dictionary<string, object?> { ["center"] = ToDict(pelvisCenter), ["leftHip"] = ToDict(leftHip), ["rightHip"] = ToDict(rightHip), ["rx"] = H * 0.70f, ["ry"] = H * 0.45f, ["tiltDeg"] = pelvicTiltDeg },
            ["crotch"] = ToDict(crotch),
            ["leftArm"] = new Dictionary<string, object?> { ["shoulder"] = ToDict(leftShoulder), ["elbow"] = ToDict(leftElbow), ["wrist"] = ToDict(leftWrist), ["hand"] = ToDict(leftHand) },
            ["rightArm"] = new Dictionary<string, object?> { ["shoulder"] = ToDict(rightShoulder), ["elbow"] = ToDict(rightElbow), ["wrist"] = ToDict(rightWrist), ["hand"] = ToDict(rightHand) },
            ["leftLeg"] = new Dictionary<string, object?> { ["hip"] = ToDict(leftHip), ["knee"] = ToDict(leftKnee), ["ankle"] = ToDict(leftAnkle), ["foot"] = ToDict(leftFoot) },
            ["rightLeg"] = new Dictionary<string, object?> { ["hip"] = ToDict(rightHip), ["knee"] = ToDict(rightKnee), ["ankle"] = ToDict(rightAnkle), ["foot"] = ToDict(rightFoot) }
        };
    }

    public void drawMannequinWireframe(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
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
        var headRy = head != null && head.Contains("ry") ? Convert.ToSingle(head["ry"], CultureInfo.InvariantCulture) : 35f;

        var neck = ExtractPoint(fig["neck"]);
        var sternum = ExtractPoint(fig["sternum"]);
        var navel = ExtractPoint(fig["navel"]);
        var crotch = ExtractPoint(fig["crotch"]);

        var ribcage = JsInterop.AsDict(fig["ribcage"]);
        var ribCenter = ExtractPoint(ribcage?["center"]);
        var ribRx = ribcage != null && ribcage.Contains("rx") ? Convert.ToSingle(ribcage["rx"], CultureInfo.InvariantCulture) : 60f;
        var ribRy = ribcage != null && ribcage.Contains("ry") ? Convert.ToSingle(ribcage["ry"], CultureInfo.InvariantCulture) : 50f;

        var pelvis = JsInterop.AsDict(fig["pelvis"]);
        var pelCenter = ExtractPoint(pelvis?["center"]);
        var pelRx = pelvis != null && pelvis.Contains("rx") ? Convert.ToSingle(pelvis["rx"], CultureInfo.InvariantCulture) : 50f;
        var pelRy = pelvis != null && pelvis.Contains("ry") ? Convert.ToSingle(pelvis["ry"], CultureInfo.InvariantCulture) : 32f;

        var lArm = JsInterop.AsDict(fig["leftArm"]);
        var rArm = JsInterop.AsDict(fig["rightArm"]);
        var lLeg = JsInterop.AsDict(fig["leftLeg"]);
        var rLeg = JsInterop.AsDict(fig["rightLeg"]);

        ctx.save();

        // 1. Blue-line Skeleton Gesture & Masses
        ctx.strokeStyle = blueLine;
        ctx.lineWidth = lineWidth;

        // Head Ellipse
        ctx.beginPath();
        ctx.ellipse(headCenter.X, headCenter.Y, headRx, headRy, 0f, 0f, MathF.PI * 2f);
        ctx.stroke();

        // Spine Line of Action (Cervical -> Thoracic -> Lumbar -> Sacral)
        ctx.beginPath();
        ctx.moveTo(headCenter.X, headCenter.Y + headRy);
        ctx.lineTo(neck.X, neck.Y);
        ctx.quadraticCurveTo(sternum.X, sternum.Y, ribCenter.X, ribCenter.Y);
        ctx.quadraticCurveTo(navel.X, navel.Y, pelCenter.X, pelCenter.Y);
        ctx.lineTo(crotch.X, crotch.Y);
        ctx.stroke();

        // Ribcage Egg
        ctx.beginPath();
        ctx.ellipse(ribCenter.X, ribCenter.Y, ribRx, ribRy, 0f, 0f, MathF.PI * 2f);
        ctx.stroke();

        // Pelvic Basin
        ctx.beginPath();
        ctx.ellipse(pelCenter.X, pelCenter.Y, pelRx, pelRy, 0f, 0f, MathF.PI * 2f);
        ctx.stroke();

        // 2. Graphite Limb Bones & Joint Hinges
        ctx.strokeStyle = graphite;
        ctx.lineWidth = lineWidth * 1.25f;

        void DrawLimb(Point2D a, Point2D b, Point2D c, Point2D d)
        {
            ctx.beginPath();
            ctx.moveTo(a.X, a.Y);
            ctx.lineTo(b.X, b.Y);
            ctx.lineTo(c.X, c.Y);
            ctx.lineTo(d.X, d.Y);
            ctx.stroke();

            // Joint hinges
            ctx.beginPath();
            ctx.arc(b.X, b.Y, 5f, 0f, MathF.PI * 2f);
            ctx.arc(c.X, c.Y, 4f, 0f, MathF.PI * 2f);
            ctx.stroke();
        }

        if (lArm != null) DrawLimb(ExtractPoint(lArm["shoulder"]), ExtractPoint(lArm["elbow"]), ExtractPoint(lArm["wrist"]), ExtractPoint(lArm["hand"]));
        if (rArm != null) DrawLimb(ExtractPoint(rArm["shoulder"]), ExtractPoint(rArm["elbow"]), ExtractPoint(rArm["wrist"]), ExtractPoint(rArm["hand"]));
        if (lLeg != null) DrawLimb(ExtractPoint(lLeg["hip"]), ExtractPoint(lLeg["knee"]), ExtractPoint(lLeg["ankle"]), ExtractPoint(lLeg["foot"]));
        if (rLeg != null) DrawLimb(ExtractPoint(rLeg["hip"]), ExtractPoint(rLeg["knee"]), ExtractPoint(rLeg["ankle"]), ExtractPoint(rLeg["foot"]));

        ctx.restore();
    }

    public void drawMannequinSolid(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var fillColor = opt?["fillColor"]?.ToString() ?? "#dbe7f2";
        var shadowColor = opt?["shadowColor"]?.ToString() ?? "#9bbcd9";
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#2d547d";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 1.8f;

        var H = fig.Contains("headUnit") ? Convert.ToSingle(fig["headUnit"], CultureInfo.InvariantCulture) : 70f;

        ctx.save();
        ctx.fillStyle = fillColor;
        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = strokeWidth;

        // 1. Shaded Limbs (Tapered Cylinders)
        void DrawCylinderLimb(Point2D a, Point2D b, float r1, float r2)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 0.001f) dist = 1f;
            var nx = -dy / dist;
            var ny = dx / dist;

            ctx.beginPath();
            ctx.moveTo(a.X - nx * r1, a.Y - ny * r1);
            ctx.lineTo(b.X - nx * r2, b.Y - ny * r2);
            ctx.lineTo(b.X + nx * r2, b.Y + ny * r2);
            ctx.lineTo(a.X + nx * r1, a.Y + ny * r1);
            ctx.closePath();
            ctx.fill();
            ctx.stroke();
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
        ctx.beginPath();
        ctx.ellipse(pelCenter.X, pelCenter.Y, pelRx, pelRy, 0f, 0f, MathF.PI * 2f);
        ctx.fill();
        ctx.stroke();

        // Ribcage
        var ribcage = JsInterop.AsDict(fig["ribcage"]);
        var ribCenter = ExtractPoint(ribcage?["center"]);
        var ribRx = ribcage != null && ribcage.Contains("rx") ? Convert.ToSingle(ribcage["rx"], CultureInfo.InvariantCulture) : H * 0.85f;
        var ribRy = ribcage != null && ribcage.Contains("ry") ? Convert.ToSingle(ribcage["ry"], CultureInfo.InvariantCulture) : H * 0.70f;
        ctx.beginPath();
        ctx.ellipse(ribCenter.X, ribCenter.Y, ribRx, ribRy, 0f, 0f, MathF.PI * 2f);
        ctx.fill();
        ctx.stroke();

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
        var headRy = head != null && head.Contains("ry") ? Convert.ToSingle(head["ry"], CultureInfo.InvariantCulture) : H * 0.50f;
        ctx.beginPath();
        ctx.ellipse(headCenter.X, headCenter.Y, headRx, headRy, 0f, 0f, MathF.PI * 2f);
        ctx.fill();
        ctx.stroke();

        ctx.restore();
    }

    public void drawTorsoMusculature(CanvasRenderingContext2D ctx, object figureObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (JsInterop.AsDict(figureObj) is not IDictionary fig) return;

        var opt = JsInterop.AsDict(options);
        var strokeColor = opt?["strokeColor"]?.ToString() ?? "#1a2938";
        var strokeWidth = opt != null && opt.Contains("strokeWidth") ? Convert.ToSingle(opt["strokeWidth"], CultureInfo.InvariantCulture) : 2.0f;
        var H = fig.Contains("headUnit") ? Convert.ToSingle(fig["headUnit"], CultureInfo.InvariantCulture) : 70f;

        var sternum = ExtractPoint(fig["sternum"]);
        var navel = ExtractPoint(fig["navel"]);
        var clavicles = JsInterop.AsDict(fig["clavicles"]);
        var leftClav = ExtractPoint(clavicles?["left"]);
        var rightClav = ExtractPoint(clavicles?["right"]);

        ctx.save();
        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = strokeWidth;
        ctx.lineCap = "round";

        // 1. Clavicle Handlebars
        ctx.beginPath();
        ctx.moveTo(leftClav.X, leftClav.Y);
        ctx.quadraticCurveTo((leftClav.X + sternum.X) * 0.5f, sternum.Y + 4f, sternum.X, sternum.Y);
        ctx.quadraticCurveTo((rightClav.X + sternum.X) * 0.5f, sternum.Y + 4f, rightClav.X, rightClav.Y);
        ctx.stroke();

        // 2. Pectoralis Major Chest Plates
        var pecY = sternum.Y + H * 0.55f;
        var pecW = H * 0.65f;

        // Left Pectoral
        ctx.beginPath();
        ctx.moveTo(sternum.X, sternum.Y + 8f);
        ctx.lineTo(sternum.X, pecY);
        ctx.quadraticCurveTo(sternum.X - pecW * 0.5f, pecY + 6f, leftClav.X + 8f, pecY - 8f);
        ctx.lineTo(leftClav.X + 4f, leftClav.Y + 8f);
        ctx.stroke();

        // Right Pectoral
        ctx.beginPath();
        ctx.moveTo(sternum.X, sternum.Y + 8f);
        ctx.lineTo(sternum.X, pecY);
        ctx.quadraticCurveTo(sternum.X + pecW * 0.5f, pecY + 6f, rightClav.X - 8f, pecY - 8f);
        ctx.lineTo(rightClav.X - 4f, rightClav.Y + 8f);
        ctx.stroke();

        // 3. Linea Alba & Rectus Abdominis Six-Pack
        ctx.beginPath();
        ctx.moveTo(sternum.X, pecY);
        ctx.lineTo(navel.X, navel.Y + H * 0.4f);
        ctx.stroke();

        for (var i = 1; i <= 2; i++)
        {
            var tierY = pecY + (navel.Y - pecY) * (i * 0.33f);
            ctx.beginPath();
            ctx.moveTo(sternum.X - H * 0.35f, tierY);
            ctx.lineTo(sternum.X + H * 0.35f, tierY);
            ctx.stroke();
        }

        ctx.restore();
    }

    public Dictionary<string, object?> applyFacialExpression(object headObj, string expressionType, float intensity = 1.0f)
    {
        if (JsInterop.AsDict(headObj) is not IDictionary head)
            throw new ArgumentException("headObj must be a valid Loomis head dictionary", nameof(headObj));

        // Create shallow clone of dictionary to avoid mutating caller unpredictably
        var res = new Dictionary<string, object?>();
        foreach (DictionaryEntry de in head)
            res[de.Key.ToString()!] = de.Value;

        var unit = JsInterop.AsDict(head["unit"]);
        var H = unit != null && unit.Contains("H") ? Convert.ToSingle(unit["H"], CultureInfo.InvariantCulture) : 200f;
        var scale = MathF.Max(0.1f, MathF.Min(2.0f, intensity));

        var nearEye = JsInterop.AsDict(head["nearEye"]);
        var farEye = JsInterop.AsDict(head["farEye"]);
        var mouth = JsInterop.AsDict(head["mouthGuides"]);
        var brow = ExtractPoint(head["brow"]);

        var exp = expressionType.Trim().ToLowerInvariant();

        switch (exp)
        {
            case "joy":
            case "happy":
                // Lift mouth corners up
                if (mouth != null)
                {
                    var left = ExtractPoint(mouth["leftCorner"]);
                    var right = ExtractPoint(mouth["rightCorner"]);
                    var mDict = new Dictionary<string, object?>(mouth.Count);
                    foreach (DictionaryEntry de in mouth) mDict[de.Key.ToString()!] = de.Value;
                    mDict["leftCorner"] = ToDict(new Point2D(left.X, left.Y - H * 0.05f * scale));
                    mDict["rightCorner"] = ToDict(new Point2D(right.X, right.Y - H * 0.05f * scale));
                    res["mouthGuides"] = mDict;
                }
                break;

            case "anger":
            case "angry":
                // Pull brow down and in
                res["brow"] = ToDict(new Point2D(brow.X, brow.Y + H * 0.06f * scale));
                break;

            case "fear":
            case "scared":
                // Raise brow high, drop mouth open
                res["brow"] = ToDict(new Point2D(brow.X, brow.Y - H * 0.07f * scale));
                if (mouth != null)
                {
                    var center = ExtractPoint(mouth["center"]);
                    var mDict = new Dictionary<string, object?>(mouth.Count);
                    foreach (DictionaryEntry de in mouth) mDict[de.Key.ToString()!] = de.Value;
                    mDict["center"] = ToDict(new Point2D(center.X, center.Y + H * 0.06f * scale));
                    res["mouthGuides"] = mDict;
                }
                break;

            case "sadness":
            case "sad":
                // Pull mouth corners down
                if (mouth != null)
                {
                    var left = ExtractPoint(mouth["leftCorner"]);
                    var right = ExtractPoint(mouth["rightCorner"]);
                    var mDict = new Dictionary<string, object?>(mouth.Count);
                    foreach (DictionaryEntry de in mouth) mDict[de.Key.ToString()!] = de.Value;
                    mDict["leftCorner"] = ToDict(new Point2D(left.X, left.Y + H * 0.05f * scale));
                    mDict["rightCorner"] = ToDict(new Point2D(right.X, right.Y + H * 0.05f * scale));
                    res["mouthGuides"] = mDict;
                }
                break;

            case "surprise":
                // Raise brow high & drop mouth center
                res["brow"] = ToDict(new Point2D(brow.X, brow.Y - H * 0.08f * scale));
                if (mouth != null)
                {
                    var center = ExtractPoint(mouth["center"]);
                    var mDict = new Dictionary<string, object?>(mouth.Count);
                    foreach (DictionaryEntry de in mouth) mDict[de.Key.ToString()!] = de.Value;
                    mDict["center"] = ToDict(new Point2D(center.X, center.Y + H * 0.08f * scale));
                    res["mouthGuides"] = mDict;
                }
                break;

            case "disgust":
                // Raise upper lip
                if (mouth != null)
                {
                    var center = ExtractPoint(mouth["center"]);
                    var mDict = new Dictionary<string, object?>(mouth.Count);
                    foreach (DictionaryEntry de in mouth) mDict[de.Key.ToString()!] = de.Value;
                    mDict["upperLipY"] = center.Y - H * 0.06f * scale;
                    res["mouthGuides"] = mDict;
                }
                break;
        }

        res["expression"] = exp;
        return res;
    }
    #endregion

    #region Composition Armatures, Notan & Visual Emphasis
    public Dictionary<string, object?> createCompositionGrid(float width, float height, string type = "ruleOfThirds", object? options = null)
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

    public void drawCompositionGrid(CanvasRenderingContext2D ctx, object gridObjOrType, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        IDictionary? grid = null;

        if (gridObjOrType is string typeStr)
        {
            grid = createCompositionGrid(ctx.Canvas.Width, ctx.Canvas.Height, typeStr, options);
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

        ctx.save();
        ctx.globalAlpha = opacity;
        ctx.strokeStyle = lineColor;
        ctx.fillStyle = pointColor;
        ctx.lineWidth = lineWidth;

        // 1. Draw Grid Lines
        if (grid["lines"] is IList lines)
        {
            foreach (var l in lines)
            {
                if (l is not IList pts || pts.Count < 2) continue;
                var a = ExtractPoint(pts[0]);
                var b = ExtractPoint(pts[1]);
                ctx.beginPath();
                ctx.moveTo(a.X, a.Y);
                ctx.lineTo(b.X, b.Y);
                ctx.stroke();
            }
        }

        // 2. Draw Focal Power Points
        if (JsInterop.AsDict(grid["powerPoints"]) is IDictionary pps)
        {
            foreach (DictionaryEntry de in pps)
            {
                var pt = ExtractPoint(de.Value);
                ctx.beginPath();
                ctx.arc(pt.X, pt.Y, 5f, 0f, MathF.PI * 2f);
                ctx.fill();
            }
        }

        ctx.restore();
    }

    public void drawVignette(CanvasRenderingContext2D ctx, float width, float height, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var opt = JsInterop.AsDict(options);
        var vignetteColor = opt?["vignetteColor"]?.ToString() ?? "#080b10";
        var intensity = opt != null && opt.Contains("intensity") ? Convert.ToSingle(opt["intensity"], CultureInfo.InvariantCulture) : 0.65f;
        var radius = opt != null && opt.Contains("radius") ? Convert.ToSingle(opt["radius"], CultureInfo.InvariantCulture) : MathF.Max(width, height) * 0.72f;

        var cx = width * 0.5f;
        var cy = height * 0.5f;

        ctx.save();
        ctx.globalAlpha = intensity;
        var grad = ctx.createRadialGradient(cx, cy, radius * 0.35f, cx, cy, radius);
        grad.addColorStop(0.0f, "rgba(0,0,0,0)");
        grad.addColorStop(0.65f, "rgba(0,0,0,0.15)");
        grad.addColorStop(1.0f, vignetteColor);

        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, width, height);
        ctx.restore();
    }

    public void drawLeadingLines(CanvasRenderingContext2D ctx, object originPoints, object focalPoint, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var fp = ExtractPoint(focalPoint);
        if (originPoints is not IList list || list.Count == 0) return;

        var opt = JsInterop.AsDict(options);
        var lineColor = opt?["lineColor"]?.ToString() ?? "#e74c3c";
        var lineWidth = opt != null && opt.Contains("lineWidth") ? Convert.ToSingle(opt["lineWidth"], CultureInfo.InvariantCulture) : 1.2f;
        var opacity = opt != null && opt.Contains("opacity") ? Convert.ToSingle(opt["opacity"], CultureInfo.InvariantCulture) : 0.35f;

        ctx.save();
        ctx.globalAlpha = opacity;
        ctx.strokeStyle = lineColor;
        ctx.lineWidth = lineWidth;

        foreach (var item in list)
        {
            var p = ExtractPoint(item);
            ctx.beginPath();
            ctx.moveTo(p.X, p.Y);
            ctx.lineTo(fp.X, fp.Y);
            ctx.stroke();
        }

        ctx.restore();
    }

    public Dictionary<string, object?> createNotanPalette(string type = "classic3")
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

    public Dictionary<string, object?> subdivideProportions(object bounds, string direction = "horizontal", object? ratios = null)
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
    #endregion
}




