namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>Vector logo construction on Snap.svg papers (Studio Manuals 10 and 12).</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class VectorLogoToolkit
{
    #region Constants
    public const float Phi = 1.61803398875f;
    #endregion

    #region Mathematical SVG Path Generators
    public static string CreateSquirclePath(float x, float y, float width, float height, float exponent = 4.5f)
    {
        var n = MathF.Max(0.5f, exponent);
        var halfW = width * 0.5f;
        var halfH = height * 0.5f;
        var cx = x + halfW;
        var cy = y + halfH;

        const int steps = 64;
        var sb = new StringBuilder(steps * 20);

        for (var i = 0; i <= steps; i++)
        {
            var theta = i * 2f * MathF.PI / steps;
            var cosT = MathF.Cos(theta);
            var sinT = MathF.Sin(theta);

            var sgnCos = MathF.Sign(cosT);
            var sgnSin = MathF.Sign(sinT);

            var px = cx + sgnCos * halfW * MathF.Pow(MathF.Abs(cosT), 2f / n);
            var py = cy + sgnSin * halfH * MathF.Pow(MathF.Abs(sinT), 2f / n);

            if (i == 0)
                sb.Append(string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", px, py));
            else
                sb.Append(string.Format(CultureInfo.InvariantCulture, " L {0:F2} {1:F2}", px, py));
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    public static string CreateGoldenSpiralPath(float startX, float startY, float initialRadius, float turns = 3f, int segmentsPerTurn = 36)
    {
        var a = MathF.Max(1f, initialRadius);
        var b = MathF.Log(Phi) / (MathF.PI * 0.5f);
        var totalSteps = (int)(turns * segmentsPerTurn);
        var totalTheta = turns * 2f * MathF.PI;

        var sb = new StringBuilder(totalSteps * 20);

        for (var i = 0; i <= totalSteps; i++)
        {
            var theta = i * totalTheta / totalSteps;
            var r = a * MathF.Exp(b * theta);
            var px = startX + r * MathF.Cos(theta);
            var py = startY + r * MathF.Sin(theta);

            if (i == 0)
                sb.Append(string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", px, py));
            else
                sb.Append(string.Format(CultureInfo.InvariantCulture, " L {0:F2} {1:F2}", px, py));
        }

        return sb.ToString();
    }

    public static string CreateEmblemBadgePath(float cx, float cy, float width, float height, string style = "shield")
    {
        var halfW = width * 0.5f;
        var halfH = height * 0.5f;
        var normStyle = (style ?? "shield").Trim().ToLowerInvariant();

        switch (normStyle)
        {
            case "hexagon":
                var sbHex = new StringBuilder();
                for (var i = 0; i < 6; i++)
                {
                    var angle = (i * 60f - 30f) * MathF.PI / 180f;
                    var hx = cx + halfW * MathF.Cos(angle);
                    var hy = cy + halfH * MathF.Sin(angle);
                    if (i == 0) sbHex.Append(string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", hx, hy));
                    else sbHex.Append(string.Format(CultureInfo.InvariantCulture, " L {0:F2} {1:F2}", hx, hy));
                }
                sbHex.Append(" Z");
                return sbHex.ToString();

            case "diamond":
                return string.Format(CultureInfo.InvariantCulture,
                    "M {0:F2} {1:F2} L {2:F2} {3:F2} L {4:F2} {5:F2} L {6:F2} {7:F2} Z",
                    cx, cy - halfH, cx + halfW, cy, cx, cy + halfH, cx - halfW, cy);

            case "scallop":
                const int lobes = 16;
                var sbScallop = new StringBuilder();
                for (var i = 0; i < lobes; i++)
                {
                    var a1 = (i * 2f * MathF.PI) / lobes;
                    var a2 = ((i + 1) * 2f * MathF.PI) / lobes;
                    var amid = (a1 + a2) * 0.5f;

                    var p1x = cx + halfW * MathF.Cos(a1);
                    var p1y = cy + halfH * MathF.Sin(a1);
                    var pmidX = cx + (halfW * 1.12f) * MathF.Cos(amid);
                    var pmidY = cy + (halfH * 1.12f) * MathF.Sin(amid);
                    var p2x = cx + halfW * MathF.Cos(a2);
                    var p2y = cy + halfH * MathF.Sin(a2);

                    if (i == 0)
                        sbScallop.Append(string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", p1x, p1y));

                    sbScallop.Append(string.Format(CultureInfo.InvariantCulture, " Q {0:F2} {1:F2} {2:F2} {3:F2}", pmidX, pmidY, p2x, p2y));
                }
                sbScallop.Append(" Z");
                return sbScallop.ToString();

            case "circle":
                return string.Format(CultureInfo.InvariantCulture,
                    "M {0:F2} {1:F2} A {2:F2} {3:F2} 0 1 0 {4:F2} {5:F2} A {6:F2} {7:F2} 0 1 0 {8:F2} {9:F2} Z",
                    cx - halfW, cy, halfW, halfH, cx + halfW, cy, halfW, halfH, cx - halfW, cy);

            case "shield":
            default:
                var topY = cy - halfH;
                var bottomY = cy + halfH;
                var leftX = cx - halfW;
                var rightX = cx + halfW;
                var waistY = cy + halfH * 0.15f;

                return string.Format(CultureInfo.InvariantCulture,
                    "M {0:F2} {1:F2} Q {2:F2} {3:F2} {4:F2} {5:F2} L {6:F2} {7:F2} C {8:F2} {9:F2} {10:F2} {11:F2} {12:F2} {13:F2} C {14:F2} {15:F2} {16:F2} {17:F2} {18:F2} {19:F2} Z",
                    leftX, topY, cx, topY + halfH * 0.12f, rightX, topY, rightX, waistY,
                    rightX, bottomY - halfH * 0.25f, cx + halfW * 0.35f, bottomY - halfH * 0.05f, cx, bottomY,
                    cx - halfW * 0.35f, bottomY - halfH * 0.05f, leftX, bottomY - halfH * 0.25f, leftX, waistY);
        }
    }

    public static string CreateTangentFilletPath(float x1, float y1, float cornerX, float cornerY, float x2, float y2, float radius)
    {
        var v1x = x1 - cornerX;
        var v1y = y1 - cornerY;
        var len1 = MathF.Sqrt(v1x * v1x + v1y * v1y);

        var v2x = x2 - cornerX;
        var v2y = y2 - cornerY;
        var len2 = MathF.Sqrt(v2x * v2x + v2y * v2y);

        if (len1 < 0.001f || len2 < 0.001f)
            return string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2} L {2:F2} {3:F2} L {4:F2} {5:F2}", x1, y1, cornerX, cornerY, x2, y2);

        var u1x = v1x / len1;
        var u1y = v1y / len1;
        var u2x = v2x / len2;
        var u2y = v2y / len2;

        var dot = MathF.Max(-0.9999f, MathF.Min(0.9999f, u1x * u2x + u1y * u2y));
        var halfAngle = MathF.Acos(dot) * 0.5f;
        var tanHalf = MathF.Tan(halfAngle);
        var d = tanHalf > 0.001f ? radius / tanHalf : 0f;

        d = MathF.Min(d, MathF.Min(len1 * 0.95f, len2 * 0.95f));

        var t1x = cornerX + u1x * d;
        var t1y = cornerY + u1y * d;
        var t2x = cornerX + u2x * d;
        var t2y = cornerY + u2y * d;

        var cross = u1x * u2y - u1y * u2x;
        var sweepFlag = cross > 0 ? 0 : 1;

        return string.Format(CultureInfo.InvariantCulture,
            "M {0:F2} {1:F2} L {2:F2} {3:F2} A {4:F2} {5:F2} 0 0 {6} {7:F2} {8:F2} L {9:F2} {10:F2}",
            x1, y1, t1x, t1y, radius, radius, sweepFlag, t2x, t2y, x2, y2);
    }

    public static string CreateBoneEffectPath(float startX, float startY, float endX, float endY, float maxBulge = 4f, float controlT = 0.5f)
    {
        var dx = endX - startX;
        var dy = endY - startY;
        var len = MathF.Sqrt(dx * dx + dy * dy);

        if (len < 0.001f)
            return string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2} L {2:F2} {3:F2}", startX, startY, endX, endY);

        var nx = -dy / len;
        var ny = dx / len;

        var t = MathF.Max(0.1f, MathF.Min(0.9f, controlT));
        var midX = startX + dx * t;
        var midY = startY + dy * t;

        var ctrlX = midX + nx * maxBulge;
        var ctrlY = midY + ny * maxBulge;

        return string.Format(CultureInfo.InvariantCulture,
            "M {0:F2} {1:F2} Q {2:F2} {3:F2} {4:F2} {5:F2}",
            startX, startY, ctrlX, ctrlY, endX, endY);
    }

    public static string CreateOgeeCurvePath(float x1, float y1, float x2, float y2, float inflectionT = 0.5f, float amplitude = 25f)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2} L {2:F2} {3:F2}", x1, y1, x2, y2);

        var nx = -dy / len;
        var ny = dx / len;

        var t = MathF.Max(0.2f, MathF.Min(0.8f, inflectionT));
        var midX = x1 + dx * t;
        var midY = y1 + dy * t;

        var c1x = x1 + dx * (t * 0.4f) + nx * amplitude;
        var c1y = y1 + dy * (t * 0.4f) + ny * amplitude;
        var c2x = x1 + dx * (t * 0.8f) + nx * amplitude;
        var c2y = y1 + dy * (t * 0.8f) + ny * amplitude;

        var remT = 1f - t;
        var c3x = midX + dx * (remT * 0.3f) - nx * amplitude;
        var c3y = midY + dy * (remT * 0.3f) - ny * amplitude;
        var c4x = midX + dx * (remT * 0.7f) - nx * amplitude;
        var c4y = midY + dy * (remT * 0.7f) - ny * amplitude;

        return string.Format(CultureInfo.InvariantCulture,
            "M {0:F2} {1:F2} C {2:F2} {3:F2} {4:F2} {5:F2} {6:F2} {7:F2} C {8:F2} {9:F2} {10:F2} {11:F2} {12:F2} {13:F2}",
            x1, y1, c1x, c1y, c2x, c2y, midX, midY, c3x, c3y, c4x, c4y, x2, y2);
    }
    #endregion

    #region SnapPaper Element Builders
    public SnapPath Squircle(SnapPaper paper, float x, float y, float width, float height, float exponent = 4.5f)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var d = CreateSquirclePath(x, y, width, height, exponent);
        return paper.Path(d);
    }

    public SnapPath OgeeCurve(SnapPaper paper, float x1, float y1, float x2, float y2, float amplitude = 25f, float inflectionT = 0.5f)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var d = CreateOgeeCurvePath(x1, y1, x2, y2, inflectionT, amplitude);
        return paper.Path(d);
    }

    public SnapPath GoldenSpiral(SnapPaper paper, float startX, float startY, float initialRadius, float turns = 3f, int segmentsPerTurn = 36)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var d = CreateGoldenSpiralPath(startX, startY, initialRadius, turns, segmentsPerTurn);
        return paper.Path(d);
    }

    public SnapPath EmblemBadge(SnapPaper paper, float cx, float cy, float width, float height, string style = "shield")
    {
        ArgumentNullException.ThrowIfNull(paper);
        var d = CreateEmblemBadgePath(cx, cy, width, height, style);
        return paper.Path(d);
    }

    /// <summary>
    /// Guide colouring for the vector armature helpers, matching their raster twins' options.
    /// </summary>
    /// <remarks>
    /// These helpers used to bake their own colours into each child's inline <c>style</c>, which an
    /// inherited presentation attribute on the returned group cannot override — so calling
    /// <c>attr({stroke: …})</c> on the group was silently ineffective and the guides stayed the
    /// toolkit's indigo and amber whatever palette the mark used.
    /// </remarks>
    static (string Line, float Width, float Opacity) GuideStyle(object? options, string defaultColor, float defaultWidth, float defaultOpacity) =>
        (OptionString(options, "lineColor", defaultColor),
         OptionFloat(options, "lineWidth", defaultWidth),
         OptionFloat(options, "opacity", defaultOpacity));

    static object? Option(object? options, string name)
    {
        if (options is null) return null;

        if (options is IDictionary<string, object?> dict)
        {
            foreach (var (k, v) in dict)
            {
                if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) return v;
            }
            return null;
        }

        if (options is System.Collections.IDictionary raw)
        {
            foreach (System.Collections.DictionaryEntry e in raw)
            {
                if (string.Equals(e.Key?.ToString(), name, StringComparison.OrdinalIgnoreCase)) return e.Value;
            }
            return null;
        }

        return options.GetType()
            .GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)
            ?.GetValue(options);
    }

    static string OptionString(object? options, string name, string fallback) =>
        Option(options, name)?.ToString() is string s && !string.IsNullOrWhiteSpace(s) ? s : fallback;

    static float OptionFloat(object? options, string name, float fallback) =>
        Option(options, name) is object v
        && float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
            ? f
            : fallback;

    public SnapGroup GoldenCircles(SnapPaper paper, float cx, float cy, float baseRadius, int count = 5, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var group = paper.Group();
        var style = GuideStyle(options, "#f59e0b", 1.0f, 0.6f);
        var r = baseRadius;
        for (var i = 0; i < count; i++)
        {
            var circle = paper.Circle(cx, cy, r);
            circle.Attr(new Dictionary<string, object?>
            {
                ["fill"] = "none",
                ["stroke"] = style.Line,
                ["stroke-width"] = style.Width,
                ["stroke-dasharray"] = "4,4",
                ["opacity"] = style.Opacity
            });
            group.Append(circle);
            r /= Phi;
        }
        return group;
    }

    public SnapGroup IsometricGrid(SnapPaper paper, float width, float height, float spacing = 40f, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var group = paper.Group();
        var style = GuideStyle(options, "#38bdf8", 0.5f, 0.35f);
        var tan30 = MathF.Tan(30f * MathF.PI / 180f);

        for (var y = -height; y < height * 2f; y += spacing)
        {
            var line1 = paper.Line(0, y, width, y + width * tan30);
            line1.Attr(new Dictionary<string, object?> { ["stroke"] = style.Line, ["stroke-width"] = style.Width, ["opacity"] = style.Opacity });
            group.Append(line1);

            var line2 = paper.Line(0, y, width, y - width * tan30);
            line2.Attr(new Dictionary<string, object?> { ["stroke"] = style.Line, ["stroke-width"] = style.Width, ["opacity"] = style.Opacity });
            group.Append(line2);
        }

        for (var x = 0f; x <= width; x += spacing)
        {
            var lineV = paper.Line(x, 0, x, height);
            lineV.Attr(new Dictionary<string, object?> { ["stroke"] = style.Line, ["stroke-width"] = style.Width, ["opacity"] = style.Opacity * 0.57f });
            group.Append(lineV);
        }

        return group;
    }

    public SnapGroup PolarGrid(SnapPaper paper, float cx, float cy, float maxRadius, int ringCount = 5, int rayCount = 12, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var group = paper.Group();
        var style = GuideStyle(options, "#6366f1", 0.5f, 0.4f);

        var ringStep = maxRadius / ringCount;
        for (var i = 1; i <= ringCount; i++)
        {
            var r = i * ringStep;
            var circle = paper.Circle(cx, cy, r);
            circle.Attr(new Dictionary<string, object?> { ["fill"] = "none", ["stroke"] = style.Line, ["stroke-width"] = style.Width, ["opacity"] = style.Opacity });
            group.Append(circle);
        }

        for (var i = 0; i < rayCount; i++)
        {
            var angle = i * 2f * MathF.PI / rayCount;
            var rx = cx + maxRadius * MathF.Cos(angle);
            var ry = cy + maxRadius * MathF.Sin(angle);
            var line = paper.Line(cx, cy, rx, ry);
            line.Attr(new Dictionary<string, object?> { ["stroke"] = style.Line, ["stroke-width"] = style.Width, ["opacity"] = style.Opacity * 0.75f });
            group.Append(line);
        }

        return group;
    }

    public SnapGroup MonogramMatrix(SnapPaper paper, float x, float y, float width, float height, string type = "3x3", object? options = null)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var group = paper.Group();
        var mStyle = GuideStyle(options, "#94a3b8", 0.75f, 0.5f);
        var nodeColor = OptionString(options, "nodeColor", "#64748b");
        var rows = 3;
        var cols = 3;
        var normType = (type ?? "3x3").Trim().ToLowerInvariant();

        if (normType.StartsWith("2x2")) { rows = 2; cols = 2; }
        else if (normType.StartsWith("4x4")) { rows = 4; cols = 4; }

        var dx = width / cols;
        var dy = height / rows;

        for (var r = 0; r <= rows; r++)
        {
            var ly = y + r * dy;
            var line = paper.Line(x, ly, x + width, ly);
            line.Attr(new Dictionary<string, object?> { ["stroke"] = mStyle.Line, ["stroke-width"] = mStyle.Width, ["stroke-dasharray"] = "2,2", ["opacity"] = mStyle.Opacity });
            group.Append(line);
        }

        for (var c = 0; c <= cols; c++)
        {
            var lx = x + c * dx;
            var line = paper.Line(lx, y, lx, y + height);
            line.Attr(new Dictionary<string, object?> { ["stroke"] = mStyle.Line, ["stroke-width"] = mStyle.Width, ["stroke-dasharray"] = "2,2", ["opacity"] = mStyle.Opacity });
            group.Append(line);
        }

        for (var r = 0; r <= rows; r++)
        {
            for (var c = 0; c <= cols; c++)
            {
                var dot = paper.Circle(x + c * dx, y + r * dy, 2.5f);
                dot.Attr(new Dictionary<string, object?> { ["fill"] = nodeColor });
                group.Append(dot);
            }
        }

        return group;
    }

    public SnapGroup ClearSpaceGuide(SnapPaper paper, float x, float y, float width, float height, float margin = 24f, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(paper);
        var group = paper.Group();
        var csStyle = GuideStyle(options, "#3b82f6", 1.0f, 1.0f);
        var innerColor = OptionString(options, "innerColor", "#93c5fd");
        // Derived from the line colour rather than a fixed rgba, so overriding lineColor recolours
        // the dimension blocks too. An rgba() fill also serialises to its own hex plus fill-opacity,
        // which would leave the default blue in the markup whatever the caller asked for.
        var blockFill = OptionString(options, "fill", csStyle.Line);
        var blockOpacity = OptionFloat(options, "fillOpacity", 0.08f);

        // Inner bounds
        var innerRect = paper.Rect(x, y, width, height);
        innerRect.Attr(new Dictionary<string, object?> { ["fill"] = "none", ["stroke"] = innerColor, ["stroke-width"] = 1.0f });
        group.Append(innerRect);

        // Outer margin bounds
        var outerRect = paper.Rect(x - margin, y - margin, width + margin * 2f, height + margin * 2f);
        outerRect.Attr(new Dictionary<string, object?> { ["fill"] = "none", ["stroke"] = csStyle.Line, ["stroke-width"] = 1.0f, ["stroke-dasharray"] = "3,3" });
        group.Append(outerRect);

        // Corner squares
        var corners = new[]
        {
            (x - margin, y - margin),
            (x + width, y - margin),
            (x - margin, y + height),
            (x + width, y + height)
        };

        foreach (var (cx, cy) in corners)
        {
            var sq = paper.Rect(cx, cy, margin, margin);
            sq.Attr(new Dictionary<string, object?> { ["fill"] = blockFill, ["fill-opacity"] = blockOpacity, ["stroke"] = csStyle.Line, ["stroke-width"] = 0.5f });
            group.Append(sq);

            var txt = paper.Text(cx + margin * 0.35f, cy + margin * 0.65f, "X");
            txt.Attr(new Dictionary<string, object?> { ["fill"] = csStyle.Line, ["font-size"] = "11px", ["font-family"] = "sans-serif" });
            group.Append(txt);
        }

        return group;
    }
    #endregion
}
