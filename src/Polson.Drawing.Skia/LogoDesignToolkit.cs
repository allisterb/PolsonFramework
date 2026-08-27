namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SkiaSharp;

public class LogoDesignToolkit
{
    #region Constants
    public const float Phi = 1.61803398875f;
    #endregion

    #region Nested Helper Types
    public record struct Point2D(float X, float Y);
    public record struct Rect2D(float X, float Y, float Width, float Height);
    #endregion

    #region Point and Bounding Helpers
    public static object? GetProp(object? obj, string key)
    {
        if (obj == null) return null;

        // 1. Generic IDictionary<string, object?> (covers ExpandoObject)
        if (obj is IDictionary<string, object?> strDict)
        {
            if (strDict.TryGetValue(key, out var val)) return val;
            foreach (var kvp in strDict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        // 2. Generic IReadOnlyDictionary<string, object?>
        if (obj is IReadOnlyDictionary<string, object?> roDict)
        {
            if (roDict.TryGetValue(key, out var val)) return val;
            foreach (var kvp in roDict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        // 3. Generic IEnumerable<KeyValuePair<string, object?>>
        if (obj is IEnumerable<KeyValuePair<string, object?>> kvpEnum)
        {
            foreach (var kvp in kvpEnum)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
        }

        // 4. Non-generic IDictionary
        if (obj is IDictionary dict)
        {
            if (dict.Contains(key)) return dict[key];
            foreach (var k in dict.Keys)
            {
                if (string.Equals(k?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                    return dict[k!];
            }
            return null;
        }

        var type = obj.GetType();

        // 1. Try Jint GetOwnProperties() / GetProperties()
        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            var getPropsMethod = currentType.GetMethod("GetOwnProperties", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                 ?? currentType.GetMethod("GetProperties", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (getPropsMethod != null)
            {
                try
                {
                    var props = getPropsMethod.Invoke(obj, null) as System.Collections.IEnumerable;
                    if (props != null)
                    {
                        foreach (var item in props)
                        {
                            var itemType = item.GetType();
                            var keyProp = itemType.GetProperty("Key");
                            var valProp = itemType.GetProperty("Value");
                            var k = keyProp?.GetValue(item)?.ToString();
                            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                            {
                                var descriptor = valProp?.GetValue(item);
                                var descValProp = descriptor?.GetType().GetProperty("Value");
                                var jsVal = descValProp?.GetValue(descriptor);
                                if (jsVal != null)
                                {
                                    var typeName = jsVal.GetType().Name;
                                    if (typeName.Contains("Function") || typeName.Contains("Callback"))
                                        return jsVal;

                                    var toObj = jsVal.GetType().GetMethod("ToObject", Type.EmptyTypes);
                                    if (toObj != null)
                                    {
                                        var unwrapped = toObj.Invoke(jsVal, null);
                                        if (unwrapped != null) return unwrapped;
                                    }
                                    return jsVal;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            currentType = currentType.BaseType;
        }

        // 2. Standard .NET Property reflection
        var prop = type.GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (prop != null)
        {
            return prop.GetValue(obj);
        }

        return null;
    }

    private static Point2D ExtractPoint(object? pt, float defX = 0f, float defY = 0f)
    {
        if (pt == null) return new Point2D(defX, defY);
        if (pt is Point2D p2d) return p2d;

        var rawX = GetProp(pt, "x") ?? GetProp(pt, "X");
        var rawY = GetProp(pt, "y") ?? GetProp(pt, "Y");

        if (rawX != null || rawY != null)
        {
            var x = rawX != null ? Convert.ToSingle(rawX, CultureInfo.InvariantCulture) : defX;
            var y = rawY != null ? Convert.ToSingle(rawY, CultureInfo.InvariantCulture) : defY;
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

    private static Rect2D ExtractRect(object? obj, float defX = 0f, float defY = 0f, float defW = 100f, float defH = 100f)
    {
        if (obj == null) return new Rect2D(defX, defY, defW, defH);
        if (obj is Rect2D r2d) return r2d;

        var rawX = GetProp(obj, "x") ?? GetProp(obj, "X") ?? GetProp(obj, "left");
        var rawY = GetProp(obj, "y") ?? GetProp(obj, "Y") ?? GetProp(obj, "top");
        var rawW = GetProp(obj, "width") ?? GetProp(obj, "Width") ?? GetProp(obj, "w");
        var rawH = GetProp(obj, "height") ?? GetProp(obj, "Height") ?? GetProp(obj, "h");

        if (rawX != null || rawY != null || rawW != null || rawH != null)
        {
            var x = rawX != null ? Convert.ToSingle(rawX, CultureInfo.InvariantCulture) : defX;
            var y = rawY != null ? Convert.ToSingle(rawY, CultureInfo.InvariantCulture) : defY;
            var w = rawW != null ? Convert.ToSingle(rawW, CultureInfo.InvariantCulture) : defW;
            var h = rawH != null ? Convert.ToSingle(rawH, CultureInfo.InvariantCulture) : defH;
            return new Rect2D(x, y, w, h);
        }

        return new Rect2D(defX, defY, defW, defH);
    }

    private static Dictionary<string, object?> ToDict(Point2D p) => new() { ["x"] = p.X, ["y"] = p.Y };

    public static void InvokeCallback(object? callback, params object?[] args)
    {
        if (callback == null) return;
        Delegate? del = callback as Delegate;
        if (del != null)
        {
            if (del.Target != null && del.Target.GetType().Namespace?.StartsWith("Jint") == true)
            {
                var fnField = del.Target.GetType().GetField("_function", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                              ?? del.Target.GetType().GetField("_target", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                              ?? del.Target.GetType().GetField("function", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var innerFn = fnField?.GetValue(del.Target);
                callback = innerFn ?? del.Target;
            }
            else
            {
                try
                {
                    del.DynamicInvoke(args);
                    return;
                }
                catch
                {
                    if (del.Target != null)
                    {
                        callback = del.Target;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        var type = callback.GetType();
        object? engine = null;
        var currentType = type;
        while (engine == null && currentType != null && currentType != typeof(object))
        {
            engine = currentType.GetProperty("Engine", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(callback)
                     ?? currentType.GetField("_engine", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(callback)
                     ?? currentType.GetField("engine", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(callback);
            currentType = currentType.BaseType;
        }

        if (engine != null)
        {
            try
            {
                var jsValueType = type.Assembly.GetType("Jint.Native.JsValue");
                if (jsValueType != null)
                {
                    var fromObjectMethod = jsValueType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                                      .FirstOrDefault(m => m.Name == "FromObject" && m.GetParameters().Length == 2);
                    if (fromObjectMethod != null)
                    {
                        var jsArgsArray = Array.CreateInstance(jsValueType, args.Length);
                        for (var i = 0; i < args.Length; i++)
                        {
                            var jsVal = fromObjectMethod.Invoke(null, [engine, args[i]]);
                            jsArgsArray.SetValue(jsVal, i);
                        }

                        var undefinedVal = jsValueType.GetProperty("Undefined", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null)
                                           ?? jsValueType.GetField("Undefined", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null)
                                           ?? callback;

                        // 1. Delegate dynamic invoke with jsArgs
                        if (del != null)
                        {
                            try { del.DynamicInvoke([jsArgsArray]); return; } catch { }
                            try { del.DynamicInvoke([undefinedVal, jsArgsArray]); return; } catch { }
                        }

                        // 2. Engine.Call(JsValue, JsValue[])
                        var engineCall = engine.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                               .FirstOrDefault(m => m.Name == "Call" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.IsArray);
                        if (engineCall != null)
                        {
                            try { engineCall.Invoke(engine, [callback, jsArgsArray]); return; } catch { }
                        }

                        // 3. Engine.Call(JsValue, JsValue, JsValue[])
                        var engineCall3 = engine.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                                .FirstOrDefault(m => m.Name == "Call" && m.GetParameters().Length == 3 && m.GetParameters()[2].ParameterType.IsArray);
                        if (engineCall3 != null)
                        {
                            try { engineCall3.Invoke(engine, [callback, undefinedVal, jsArgsArray]); return; } catch { }
                        }

                        // 4. Function.Call(JsValue, JsCallArguments)
                        var jsCallArgsType = type.Assembly.GetType("Jint.Native.JsCallArguments");
                        if (jsCallArgsType != null)
                        {
                            var jsCallArgsFrom = jsCallArgsType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                                               .FirstOrDefault(m => m.Name == "From" && m.GetParameters().Length == 1);
                            var fnCallMethod = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                                   .FirstOrDefault(m => m.Name == "Call" && m.GetParameters().Length == 2);
                            if (jsCallArgsFrom != null && fnCallMethod != null)
                            {
                                var callArgs = jsCallArgsFrom.Invoke(null, [jsArgsArray]);
                                fnCallMethod.Invoke(callback, [undefinedVal, callArgs]);
                                return;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        try
        {
            var invokeMethod = type.GetMethod("Invoke");
            if (invokeMethod != null)
            {
                invokeMethod.Invoke(callback, args);
                return;
            }
        }
        catch { }
    }
    #endregion

    #region Golden Ratio and Geometry
    public string CreateSquircleSvgPath(float x, float y, float width, float height, float exponent = 4.5f)
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
            var px = cx + MathF.Sign(cosT) * halfW * MathF.Pow(MathF.Abs(cosT), 2f / n);
            var py = cy + MathF.Sign(sinT) * halfH * MathF.Pow(MathF.Abs(sinT), 2f / n);
            if (i == 0) sb.Append(string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", px, py));
            else sb.Append(string.Format(CultureInfo.InvariantCulture, " L {0:F2} {1:F2}", px, py));
        }
        sb.Append(" Z");
        return sb.ToString();
    }

    public string CreateGoldenSpiralSvgPath(float startX, float startY, float initialRadius, float turns = 3f, int segmentsPerTurn = 36)
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
            if (i == 0) sb.Append(string.Format(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", px, py));
            else sb.Append(string.Format(CultureInfo.InvariantCulture, " L {0:F2} {1:F2}", px, py));
        }
        return sb.ToString();
    }

    public string CreateEmblemBadgeSvgPath(float cx, float cy, float width, float height, string style = "shield")
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
    public Dictionary<string, object?> CreateGoldenCircles(float cx, float cy, float baseRadius, int count = 5, string direction = "growing")
    {
        const float phi = 1.61803398875f;
        var circles = new List<Dictionary<string, object?>>();
        var isGrowing = !string.Equals(direction, "shrinking", StringComparison.OrdinalIgnoreCase);

        var maxR = baseRadius;

        for (var i = 0; i < count; i++)
        {
            var factor = MathF.Pow(phi, isGrowing ? i : -i);
            var r = baseRadius * factor;
            if (r > maxR) maxR = r;

            circles.Add(new Dictionary<string, object?>
            {
                ["index"] = i,
                ["cx"] = cx,
                ["cy"] = cy,
                ["radius"] = r,
                ["phiFactor"] = factor
            });
        }

        return new Dictionary<string, object?>
        {
            ["circles"] = circles,
            ["phi"] = phi,
            ["bounds"] = new Dictionary<string, object?>
            {
                ["x"] = cx - maxR,
                ["y"] = cy - maxR,
                ["width"] = maxR * 2f,
                ["height"] = maxR * 2f
            }
        };
    }

    public void DrawGoldenSpiral(CanvasRenderingContext2D ctx, float cx, float cy, float startRadius = 20f, float turns = 2.5f, object? options = null)
    {
        var strokeColor = GetProp(options, "strokeColor")?.ToString() ?? GetProp(options, "stroke")?.ToString() ?? "#e2ae38";
        var rawLineWidth = GetProp(options, "lineWidth");
        var lineWidth = rawLineWidth != null ? Convert.ToSingle(rawLineWidth, CultureInfo.InvariantCulture) : 2.0f;
        var rawClockwise = GetProp(options, "clockwise");
        var clockwise = rawClockwise == null || Convert.ToBoolean(rawClockwise);
        var rawDrawRects = GetProp(options, "drawGoldenRectangles");
        var drawRects = rawDrawRects != null && Convert.ToBoolean(rawDrawRects);
        var rectColor = GetProp(options, "rectStrokeColor")?.ToString() ?? "rgba(226, 174, 56, 0.25)";

        const float phi = 1.61803398875f;
        var b = MathF.Log(phi) / (MathF.PI * 0.5f); // ~0.3063489

        ctx.save();

        if (drawRects)
        {
            ctx.strokeStyle = rectColor;
            ctx.lineWidth = 1.0f;
            var curA = startRadius;
            var rx = cx;
            var ry = cy;
            for (var t = 0; t < (int)(turns * 4); t++)
            {
                var curW = curA * MathF.Pow(phi, t * 0.5f);
                ctx.strokeRect(rx - curW * 0.5f, ry - curW * 0.5f, curW, curW);
            }
        }

        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = lineWidth;
        ctx.beginPath();

        var totalAngle = turns * MathF.PI * 2f;
        const int steps = 240;
        var isFirst = true;

        for (var i = 0; i <= steps; i++)
        {
            var theta = (i / (float)steps) * totalAngle;
            var r = startRadius * MathF.Exp(b * theta);
            var angle = clockwise ? theta : -theta;
            var px = cx + r * MathF.Cos(angle);
            var py = cy + r * MathF.Sin(angle);

            if (isFirst)
            {
                ctx.moveTo(px, py);
                isFirst = false;
            }
            else
            {
                ctx.lineTo(px, py);
            }
        }

        ctx.stroke();
        ctx.restore();
    }

    public Dictionary<string, object?> CreateTangentBlend(object p1, object corner, object p2, float radius)
    {
        var pt1 = ExtractPoint(p1);
        var ptC = ExtractPoint(corner);
        var pt2 = ExtractPoint(p2);

        var v1x = pt1.X - ptC.X;
        var v1y = pt1.Y - ptC.Y;
        var len1 = MathF.Sqrt(v1x * v1x + v1y * v1y);
        if (len1 == 0f) len1 = 1f;
        var u1x = v1x / len1;
        var u1y = v1y / len1;

        var v2x = pt2.X - ptC.X;
        var v2y = pt2.Y - ptC.Y;
        var len2 = MathF.Sqrt(v2x * v2x + v2y * v2y);
        if (len2 == 0f) len2 = 1f;
        var u2x = v2x / len2;
        var u2y = v2y / len2;

        var dot = Math.Clamp(u1x * u2x + u1y * u2y, -0.999f, 0.999f);
        var angleRad = MathF.Acos(dot);
        var halfAngle = angleRad * 0.5f;

        var tangentDist = radius / MathF.Tan(halfAngle);
        var centerDist = radius / MathF.Sin(halfAngle);

        var t1 = new Point2D(ptC.X + u1x * tangentDist, ptC.Y + u1y * tangentDist);
        var t2 = new Point2D(ptC.X + u2x * tangentDist, ptC.Y + u2y * tangentDist);

        var bisectorX = u1x + u2x;
        var bisectorY = u1y + u2y;
        var bisectorLen = MathF.Sqrt(bisectorX * bisectorX + bisectorY * bisectorY);
        if (bisectorLen == 0f) bisectorLen = 1f;
        bisectorX /= bisectorLen;
        bisectorY /= bisectorLen;

        var arcCenter = new Point2D(ptC.X + bisectorX * centerDist, ptC.Y + bisectorY * centerDist);
        var sweepDeg = (MathF.PI - angleRad) * 180f / MathF.PI;

        return new Dictionary<string, object?>
        {
            ["arcStart"] = ToDict(t1),
            ["arcEnd"] = ToDict(t2),
            ["arcCenter"] = ToDict(arcCenter),
            ["tangentDistance"] = tangentDist,
            ["cornerAngleDeg"] = angleRad * 180f / MathF.PI,
            ["sweepAngleDeg"] = sweepDeg
        };
    }
    #endregion

    #region Grids and Monograms
    public Dictionary<string, object?> CreateIsometricGrid(float width, float height, float cellSize = 40f)
    {
        var dx = cellSize * MathF.Cos(30f * MathF.PI / 180f); // ~0.866 * cellSize
        var dy = cellSize * MathF.Sin(30f * MathF.PI / 180f); // ~0.500 * cellSize

        var cols = (int)MathF.Ceiling(width / dx) + 4;
        var rows = (int)MathF.Ceiling(height / dy) + 4;

        var nodes = new List<List<Dictionary<string, object?>>>();

        for (var r = 0; r < rows; r++)
        {
            var rowList = new List<Dictionary<string, object?>>();
            var yOffset = r * dy;
            var xOffset = (r % 2 == 1) ? dx * 0.5f : 0f;

            for (var c = 0; c < cols; c++)
            {
                var x = c * dx + xOffset;
                var y = yOffset;
                rowList.Add(new Dictionary<string, object?> { ["x"] = x, ["y"] = y, ["col"] = c, ["row"] = r });
            }
            nodes.Add(rowList);
        }

        return new Dictionary<string, object?>
        {
            ["width"] = width,
            ["height"] = height,
            ["cellSize"] = cellSize,
            ["dx"] = dx,
            ["dy"] = dy,
            ["nodeRows"] = nodes
        };
    }

    public void DrawIsometricGrid(CanvasRenderingContext2D ctx, float width, float height, float cellSize = 40f, object? options = null)
    {
        var lineColor = GetProp(options, "lineColor")?.ToString() ?? GetProp(options, "color")?.ToString() ?? "rgba(74, 144, 226, 0.25)";
        var rawLineWidth = GetProp(options, "lineWidth");
        var lineWidth = rawLineWidth != null ? Convert.ToSingle(rawLineWidth, CultureInfo.InvariantCulture) : 1.0f;

        var dx = cellSize * MathF.Cos(30f * MathF.PI / 180f);
        var dy = cellSize * MathF.Sin(30f * MathF.PI / 180f);

        ctx.save();
        ctx.strokeStyle = lineColor;
        ctx.lineWidth = lineWidth;

        // 1. Vertical Lines
        for (var x = 0f; x <= width; x += dx)
        {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, height);
            ctx.stroke();
        }

        // 2. +30 Deg Diagonals
        var span = width + height * 2f;
        for (var y = -height; y <= span; y += dy * 2f)
        {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(width, y + width * MathF.Tan(30f * MathF.PI / 180f));
            ctx.stroke();
        }

        // 3. -30 Deg Diagonals
        for (var y = -height; y <= span; y += dy * 2f)
        {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(width, y - width * MathF.Tan(30f * MathF.PI / 180f));
            ctx.stroke();
        }

        ctx.restore();
    }

    public Dictionary<string, object?> CreatePolarGrid(float cx, float cy, object? rings = null, int radialSlices = 12)
    {
        var radii = new List<float>();
        if (rings is IList list)
        {
            foreach (var item in list)
                radii.Add(Convert.ToSingle(item, CultureInfo.InvariantCulture));
        }
        else
        {
            radii.AddRange([30f, 60f, 90f, 120f, 160f, 200f]);
        }

        var rayAngles = new List<float>();
        var angleStep = 360f / Math.Max(1, radialSlices);
        for (var i = 0; i < radialSlices; i++)
            rayAngles.Add(i * angleStep);

        return new Dictionary<string, object?>
        {
            ["cx"] = cx,
            ["cy"] = cy,
            ["radii"] = radii,
            ["rayAngles"] = rayAngles
        };
    }

    public void DrawPolarGrid(CanvasRenderingContext2D ctx, float cx, float cy, object? rings = null, int radialSlices = 12, object? options = null)
    {
        var lineColor = GetProp(options, "lineColor")?.ToString() ?? GetProp(options, "color")?.ToString() ?? "rgba(74, 144, 226, 0.25)";
        var rawLineWidth = GetProp(options, "lineWidth");
        var lineWidth = rawLineWidth != null ? Convert.ToSingle(rawLineWidth, CultureInfo.InvariantCulture) : 1.0f;

        var grid = CreatePolarGrid(cx, cy, rings, radialSlices);
        var radii = (List<float>)(grid["radii"] ?? new List<float>());
        var rayAngles = (List<float>)(grid["rayAngles"] ?? new List<float>());
        var maxR = radii.Count > 0 ? radii[^1] : 200f;

        ctx.save();
        ctx.strokeStyle = lineColor;
        ctx.lineWidth = lineWidth;

        // Concentric Circles
        foreach (var r in radii)
        {
            ctx.beginPath();
            ctx.arc(cx, cy, r, 0, MathF.PI * 2f);
            ctx.stroke();
        }

        // Radial Rays
        foreach (var deg in rayAngles)
        {
            var rad = deg * MathF.PI / 180f;
            ctx.beginPath();
            ctx.moveTo(cx, cy);
            ctx.lineTo(cx + maxR * MathF.Cos(rad), cy + maxR * MathF.Sin(rad));
            ctx.stroke();
        }

        ctx.restore();
    }

    public Dictionary<string, object?> CreateMonogramGrid(string type = "3x3", float size = 200f, float originX = 0f, float originY = 0f)
    {
        var nodes = new Dictionary<string, object?>();
        var normType = type.ToLowerInvariant();

        if (normType == "diamond")
        {
            var half = size * 0.5f;
            nodes["top"] = ToDict(new Point2D(originX + half, originY));
            nodes["bottom"] = ToDict(new Point2D(originX + half, originY + size));
            nodes["left"] = ToDict(new Point2D(originX, originY + half));
            nodes["right"] = ToDict(new Point2D(originX + size, originY + half));
            nodes["center"] = ToDict(new Point2D(originX + half, originY + half));
        }
        else if (normType == "hex")
        {
            var r = size * 0.5f;
            var cx = originX + r;
            var cy = originY + r;
            for (var i = 0; i < 6; i++)
            {
                var angle = (i * 60f - 30f) * MathF.PI / 180f;
                nodes[$"v{i}"] = ToDict(new Point2D(cx + r * MathF.Cos(angle), cy + r * MathF.Sin(angle)));
            }
            nodes["center"] = ToDict(new Point2D(cx, cy));
        }
        else // 2x2 or 3x3 default
        {
            var gridSteps = normType == "2x2" ? 2 : 3;
            var step = size / (gridSteps - 1);
            for (var r = 0; r < gridSteps; r++)
            {
                for (var c = 0; c < gridSteps; c++)
                {
                    nodes[$"p{r}{c}"] = ToDict(new Point2D(originX + c * step, originY + r * step));
                }
            }
            nodes["center"] = ToDict(new Point2D(originX + size * 0.5f, originY + size * 0.5f));
        }

        return new Dictionary<string, object?>
        {
            ["type"] = type,
            ["size"] = size,
            ["nodes"] = nodes
        };
    }
    #endregion

    #region Graphic Devices and Containers
    public SKPath CreateSquirclePath(float x, float y, float width, float height, float exponent = 4.5f)
    {
        var path = new SKPath();
        var a = width * 0.5f;
        var b = height * 0.5f;
        var cx = x + a;
        var cy = y + b;
        var n = Math.Max(1.0f, exponent);
        const int steps = 96;

        for (var i = 0; i <= steps; i++)
        {
            var theta = (i / (float)steps) * MathF.PI * 2f;
            var cosT = MathF.Cos(theta);
            var sinT = MathF.Sin(theta);

            var signCos = MathF.Sign(cosT);
            var signSin = MathF.Sign(sinT);

            var px = cx + signCos * a * MathF.Pow(MathF.Abs(cosT), 2f / n);
            var py = cy + signSin * b * MathF.Pow(MathF.Abs(sinT), 2f / n);

            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }

        path.Close();
        return path;
    }

    public void DrawSquircle(CanvasRenderingContext2D ctx, float x, float y, float width, float height, object? options = null)
    {
        var rawExp = GetProp(options, "exponent");
        var exponent = rawExp != null ? Convert.ToSingle(rawExp, CultureInfo.InvariantCulture) : 4.5f;
        var fill = GetProp(options, "fill")?.ToString() ?? GetProp(options, "fillStyle")?.ToString();
        var stroke = GetProp(options, "stroke")?.ToString() ?? GetProp(options, "strokeStyle")?.ToString();
        var rawStrokeWidth = GetProp(options, "strokeWidth");
        var strokeWidth = rawStrokeWidth != null ? Convert.ToSingle(rawStrokeWidth, CultureInfo.InvariantCulture) : 2.0f;

        var a = width * 0.5f;
        var b = height * 0.5f;
        var cx = x + a;
        var cy = y + b;
        var n = Math.Max(1.0f, exponent);
        const int steps = 96;

        ctx.save();
        ctx.beginPath();

        for (var i = 0; i <= steps; i++)
        {
            var theta = (i / (float)steps) * MathF.PI * 2f;
            var cosT = MathF.Cos(theta);
            var sinT = MathF.Sin(theta);

            var signCos = MathF.Sign(cosT);
            var signSin = MathF.Sign(sinT);

            var px = cx + signCos * a * MathF.Pow(MathF.Abs(cosT), 2f / n);
            var py = cy + signSin * b * MathF.Pow(MathF.Abs(sinT), 2f / n);

            if (i == 0) ctx.moveTo(px, py);
            else ctx.lineTo(px, py);
        }

        ctx.closePath();

        if (!string.IsNullOrEmpty(fill))
        {
            ctx.fillStyle = fill;
            ctx.fill();
        }

        if (!string.IsNullOrEmpty(stroke))
        {
            ctx.strokeStyle = stroke;
            ctx.lineWidth = strokeWidth;
            ctx.stroke();
        }

        ctx.restore();
    }

    public void DrawEmblemBadge(CanvasRenderingContext2D ctx, float cx, float cy, float radius, string type = "shield", object? options = null)
    {
        var fill = GetProp(options, "fill")?.ToString() ?? GetProp(options, "fillStyle")?.ToString();
        var stroke = GetProp(options, "stroke")?.ToString() ?? GetProp(options, "strokeStyle")?.ToString() ?? "#111827";
        var rawStrokeWidth = GetProp(options, "strokeWidth");
        var strokeWidth = rawStrokeWidth != null ? Convert.ToSingle(rawStrokeWidth, CultureInfo.InvariantCulture) : 3.0f;
        var rawPoints = GetProp(options, "points");
        var points = rawPoints != null ? Convert.ToInt32(rawPoints) : 12;

        ctx.save();
        ctx.beginPath();

        var normType = type.ToLowerInvariant();
        if (normType == "shield")
        {
            var top = cy - radius * 0.9f;
            var bottom = cy + radius * 1.05f;
            var left = cx - radius * 0.85f;
            var right = cx + radius * 0.85f;

            ctx.moveTo(cx, top);
            ctx.lineTo(right, top);
            ctx.bezierCurveTo(right, cy + radius * 0.2f, cx + radius * 0.5f, bottom - radius * 0.2f, cx, bottom);
            ctx.bezierCurveTo(cx - radius * 0.5f, bottom - radius * 0.2f, left, cy + radius * 0.2f, left, top);
            ctx.closePath();
        }
        else if (normType == "hexagon")
        {
            for (var i = 0; i < 6; i++)
            {
                var rad = (i * 60f - 30f) * MathF.PI / 180f;
                var px = cx + radius * MathF.Cos(rad);
                var py = cy + radius * MathF.Sin(rad);
                if (i == 0) ctx.moveTo(px, py);
                else ctx.lineTo(px, py);
            }
            ctx.closePath();
        }
        else if (normType == "diamond")
        {
            ctx.moveTo(cx, cy - radius);
            ctx.lineTo(cx + radius * 0.85f, cy);
            ctx.lineTo(cx, cy + radius);
            ctx.lineTo(cx - radius * 0.85f, cy);
            ctx.closePath();
        }
        else if (normType == "scallop")
        {
            var step = MathF.PI * 2f / points;
            var innerR = radius * 0.88f;
            for (var i = 0; i < points; i++)
            {
                var a1 = i * step;
                var a2 = a1 + step;
                var midA = a1 + step * 0.5f;

                var p1x = cx + innerR * MathF.Cos(a1);
                var p1y = cy + innerR * MathF.Sin(a1);
                var cpx = cx + radius * 1.12f * MathF.Cos(midA);
                var cpy = cy + radius * 1.12f * MathF.Sin(midA);
                var p2x = cx + innerR * MathF.Cos(a2);
                var p2y = cy + innerR * MathF.Sin(a2);

                if (i == 0) ctx.moveTo(p1x, p1y);
                ctx.quadraticCurveTo(cpx, cpy, p2x, p2y);
            }
            ctx.closePath();
        }
        else // default circle
        {
            ctx.arc(cx, cy, radius, 0, MathF.PI * 2f);
        }

        if (!string.IsNullOrEmpty(fill))
        {
            ctx.fillStyle = fill;
            ctx.fill();
        }

        if (!string.IsNullOrEmpty(stroke))
        {
            ctx.strokeStyle = stroke;
            ctx.lineWidth = strokeWidth;
            ctx.stroke();
        }

        ctx.restore();
    }

    public void DrawClearSpaceGuide(CanvasRenderingContext2D ctx, object markBounds, float xDimension = 40f, object? options = null)
    {
        var r = ExtractRect(markBounds);
        var color = GetProp(options, "color")?.ToString() ?? "rgba(74, 144, 226, 0.75)";
        var fill = GetProp(options, "fill")?.ToString() ?? "rgba(74, 144, 226, 0.08)";
        var rawShowLabels = GetProp(options, "showLabels");
        var showLabels = rawShowLabels == null || Convert.ToBoolean(rawShowLabels);

        var M = xDimension;
        var outerX = r.X - M;
        var outerY = r.Y - M;
        var outerW = r.Width + M * 2f;
        var outerH = r.Height + M * 2f;

        ctx.save();

        // 1. Margin Shade
        ctx.fillStyle = fill;
        ctx.fillRect(outerX, outerY, outerW, outerH);
        ctx.clearRect(r.X, r.Y, r.Width, r.Height);

        // 2. Boundary
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.2f;
        ctx.strokeRect(outerX, outerY, outerW, outerH);

        // Inner solid bounds
        ctx.lineWidth = 1.0f;
        ctx.strokeStyle = "rgba(75, 85, 99, 0.4)";
        ctx.strokeRect(r.X, r.Y, r.Width, r.Height);

        // 3. X-dimension corner squares and indicators
        if (showLabels)
        {
            ctx.fillStyle = color;
            ctx.font = "bold 12px sans-serif";
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";

            void DrawXBox(float bx, float by)
            {
                ctx.strokeStyle = color;
                ctx.strokeRect(bx, by, M, M);
                ctx.fillText("X", bx + M * 0.5f, by + M * 0.5f);
            }

            DrawXBox(outerX, outerY);
            DrawXBox(outerX + outerW - M, outerY);
            DrawXBox(outerX, outerY + outerH - M);
            DrawXBox(outerX + outerW - M, outerY + outerH - M);
        }

        ctx.restore();
    }
    #endregion

    #region Optical Tuning and Perception Fixes
    public List<Dictionary<string, object?>> CorrectBoneEffect(object p1, object p2, float strokeWidth, float pinchCorrectionFactor = 0.04f)
    {
        var pt1 = ExtractPoint(p1);
        var pt2 = ExtractPoint(p2);

        var dx = pt2.X - pt1.X;
        var dy = pt2.Y - pt1.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len == 0f) len = 1f;
        var nx = -dy / len;
        var ny = dx / len;

        var halfW = strokeWidth * 0.5f;
        var bulge = strokeWidth * pinchCorrectionFactor;

        var mx = (pt1.X + pt2.X) * 0.5f;
        var my = (pt1.Y + pt2.Y) * 0.5f;

        var poly = new List<Dictionary<string, object?>>
        {
            ToDict(new Point2D(pt1.X + nx * halfW, pt1.Y + ny * halfW)),
            ToDict(new Point2D(mx + nx * (halfW + bulge), my + ny * (halfW + bulge))),
            ToDict(new Point2D(pt2.X + nx * halfW, pt2.Y + ny * halfW)),
            ToDict(new Point2D(pt2.X - nx * halfW, pt2.Y - ny * halfW)),
            ToDict(new Point2D(mx - nx * (halfW + bulge), my - ny * (halfW + bulge))),
            ToDict(new Point2D(pt1.X - nx * halfW, pt1.Y - ny * halfW))
        };

        return poly;
    }

    public float ComputeOvershoot(float baseHeight, string shape = "circle")
    {
        var norm = shape.ToLowerInvariant();
        if (norm.Contains("tri") || norm.Contains("apex") || norm.Contains("point"))
            return baseHeight * 0.028f; // ~2.8% for sharp points

        if (norm.Contains("arch") || norm.Contains("dome"))
            return baseHeight * 0.015f; // ~1.5% for arches

        return baseHeight * 0.020f; // ~2.0% for circles
    }

    public Dictionary<string, object?> ComputeOpticalCenter(object pointsOrBounds, string shapeType = "triangle")
    {
        var rect = ExtractRect(pointsOrBounds);
        var norm = shapeType.ToLowerInvariant();

        if (norm.Contains("tri") || norm.Contains("apex"))
        {
            // Center of mass sits lower/higher depending on orientation
            return ToDict(new Point2D(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.58f));
        }

        if (norm.Contains("arrow") || norm.Contains("play"))
        {
            // Pointing right
            return ToDict(new Point2D(rect.X + rect.Width * 0.42f, rect.Y + rect.Height * 0.5f));
        }

        // General optical weight center slightly above geometric center
        return ToDict(new Point2D(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.48f));
    }
    #endregion

    #region Scale Stress Testing and Brand Sheets
    public void GenerateFaviconScaleTest(CanvasRenderingContext2D ctx, object drawMarkFn, object? options = null)
    {
        var opt = JsInterop.AsDict(options);
        var scales = new[] { 16, 24, 32, 48, 64, 128, 256 };

        ctx.save();
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(0, 0, ctx.Canvas.Width, ctx.Canvas.Height);

        // Title
        ctx.fillStyle = "#111827";
        ctx.font = "bold 18px sans-serif";
        ctx.fillText("MULTI-SCALE FAVICON & APP ICON LEGIBILITY TEST", 30, 40);

        ctx.fillStyle = "#6b7280";
        ctx.font = "12px sans-serif";
        ctx.fillText("Verifying stroke weight, contrast, and negative space legibility from 16px to 256px", 30, 62);

        var currentX = 30f;
        var currentY = 100f;

        foreach (var size in scales)
        {
            if (currentX + size + 20f > ctx.Canvas.Width)
            {
                currentX = 30f;
                currentY += 160f;
            }

            // Box Container
            ctx.fillStyle = "#f9fafb";
            ctx.fillRect(currentX, currentY, size, size);
            ctx.strokeStyle = "#e5e7eb";
            ctx.lineWidth = 1.0f;
            ctx.strokeRect(currentX, currentY, size, size);

            // Execute draw mark inside translated context
            ctx.save();
            ctx.translate(currentX, currentY);
            InvokeCallback(drawMarkFn, ctx, (float)size);
            ctx.restore();

            // Label
            ctx.fillStyle = "#4b5563";
            ctx.font = "11px sans-serif";
            ctx.fillText($"{size}px", currentX, currentY + size + 16f);

            currentX += size + 40f;
        }

        ctx.restore();
    }

    public void GenerateMonochromeTest(CanvasRenderingContext2D ctx, object drawMarkFn, float width = 800f, float height = 600f)
    {
        var halfW = width * 0.5f;
        var halfH = height * 0.5f;
        var markSize = MathF.Min(halfW, halfH) * 0.45f;

        ctx.save();

        // 1. Positive (Black on White)
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(0, 0, halfW, halfH);
        ctx.fillStyle = "#6b7280";
        ctx.font = "bold 12px sans-serif";
        ctx.fillText("POSITIVE (1-COLOR BLACK)", 20, 30);

        ctx.save();
        ctx.translate(halfW * 0.5f - markSize * 0.5f, halfH * 0.5f - markSize * 0.5f);
        InvokeCallback(drawMarkFn, ctx, markSize);
        ctx.restore();

        // 2. Negative (White on Black)
        ctx.fillStyle = "#111827";
        ctx.fillRect(halfW, 0, halfW, halfH);
        ctx.fillStyle = "#9ca3af";
        ctx.font = "bold 12px sans-serif";
        ctx.fillText("NEGATIVE (KNOCKOUT WHITE)", halfW + 20, 30);

        ctx.save();
        ctx.translate(halfW + halfW * 0.5f - markSize * 0.5f, halfH * 0.5f - markSize * 0.5f);
        InvokeCallback(drawMarkFn, ctx, markSize);
        ctx.restore();

        // 3. Dark Gray Neutral
        ctx.fillStyle = "#e5e7eb";
        ctx.fillRect(0, halfH, halfW, halfH);
        ctx.fillStyle = "#4b5563";
        ctx.font = "bold 12px sans-serif";
        ctx.fillText("GRAYSCALE NEUTRAL", 20, halfH + 30);

        ctx.save();
        ctx.translate(halfW * 0.5f - markSize * 0.5f, halfH + halfH * 0.5f - markSize * 0.5f);
        InvokeCallback(drawMarkFn, ctx, markSize);
        ctx.restore();

        // 4. App Icon Squircle Mockup
        ctx.fillStyle = "#0f172a";
        ctx.fillRect(halfW, halfH, halfW, halfH);
        ctx.fillStyle = "#94a3b8";
        ctx.font = "bold 12px sans-serif";
        ctx.fillText("APP ICON SQUIRCLE CONTAINER", halfW + 20, halfH + 30);

        var squircleSize = markSize * 1.35f;
        var sqX = halfW + halfW * 0.5f - squircleSize * 0.5f;
        var sqY = halfH + halfH * 0.5f - squircleSize * 0.5f;

        DrawSquircle(ctx, sqX, sqY, squircleSize, squircleSize, new Dictionary<string, object?>
        {
            ["fill"] = "#1e293b",
            ["stroke"] = "#334155",
            ["strokeWidth"] = 2.0f,
            ["exponent"] = 4.5f
        });

        ctx.save();
        ctx.translate(halfW + halfW * 0.5f - markSize * 0.5f, halfH + halfH * 0.5f - markSize * 0.5f);
        InvokeCallback(drawMarkFn, ctx, markSize);
        ctx.restore();

        // Separator grid lines
        ctx.strokeStyle = "#94a3b8";
        ctx.lineWidth = 1.0f;
        ctx.beginPath();
        ctx.moveTo(halfW, 0); ctx.lineTo(halfW, height);
        ctx.moveTo(0, halfH); ctx.lineTo(width, halfH);
        ctx.stroke();

        ctx.restore();
    }

    public void GenerateBrandPresentationSheet(CanvasRenderingContext2D ctx, object options)
    {
        var brandName = GetProp(options, "brandName")?.ToString() ?? "BRAND";
        var tagline = GetProp(options, "tagline")?.ToString() ?? "Visual Identity System";
        var primary = GetProp(options, "primaryColor")?.ToString() ?? "#2563eb";
        var secondary = GetProp(options, "secondaryColor")?.ToString() ?? "#38bdf8";
        var darkCol = GetProp(options, "darkColor")?.ToString() ?? "#0f172a";
        var lightCol = GetProp(options, "lightColor")?.ToString() ?? "#f8fafc";
        var drawMark = GetProp(options, "drawMark");

        var W = ctx.Canvas.Width;
        var H = ctx.Canvas.Height;

        ctx.save();

        // Background
        ctx.fillStyle = lightCol;
        ctx.fillRect(0, 0, W, H);

        // Header
        ctx.fillStyle = darkCol;
        ctx.font = "bold 28px sans-serif";
        ctx.fillText(brandName.ToUpperInvariant(), 40, 55);

        ctx.fillStyle = "#64748b";
        ctx.font = "14px sans-serif";
        ctx.fillText(tagline, 40, 80);

        ctx.strokeStyle = "#e2e8f0";
        ctx.lineWidth = 1.5f;
        ctx.beginPath();
        ctx.moveTo(40, 100); ctx.lineTo(W - 40, 100);
        ctx.stroke();

        // Hero Logo Squircle Box (Left)
        var heroBoxSize = MathF.Min(W * 0.35f, H * 0.45f);
        var heroX = 40f;
        var heroY = 130f;

        DrawSquircle(ctx, heroX, heroY, heroBoxSize, heroBoxSize, new Dictionary<string, object?>
        {
            ["fill"] = darkCol,
            ["exponent"] = 4.5f
        });

        var heroMarkSize = heroBoxSize * 0.55f;
        ctx.save();
        ctx.translate(heroX + heroBoxSize * 0.5f - heroMarkSize * 0.5f, heroY + heroBoxSize * 0.5f - heroMarkSize * 0.5f);
        InvokeCallback(drawMark, ctx, heroMarkSize);
        ctx.restore();

        // Horizontal Combination Lockup (Center-Right)
        var lockupX = heroX + heroBoxSize + 40f;
        var lockupY = heroY + 40f;
        var lockupMarkSize = 64f;

        ctx.save();
        ctx.translate(lockupX, lockupY);
        InvokeCallback(drawMark, ctx, lockupMarkSize);
        ctx.restore();

        ctx.fillStyle = darkCol;
        ctx.font = "bold 32px sans-serif";
        ctx.fillText(brandName, lockupX + lockupMarkSize + 20f, lockupY + lockupMarkSize * 0.6f);

        // Color Palette Swatches (Right)
        var swatchY = lockupY + 110f;
        var colors = new[]
        {
            ("Primary", primary),
            ("Secondary", secondary),
            ("Dark", darkCol),
            ("Light", lightCol)
        };

        var swX = lockupX;
        foreach (var (label, hex) in colors)
        {
            ctx.fillStyle = hex;
            ctx.fillRect(swX, swatchY, 60, 40);
            ctx.strokeStyle = "#cbd5e1";
            ctx.lineWidth = 1.0f;
            ctx.strokeRect(swX, swatchY, 60, 40);

            ctx.fillStyle = darkCol;
            ctx.font = "bold 11px sans-serif";
            ctx.fillText(label, swX, swatchY + 54);
            ctx.fillStyle = "#64748b";
            ctx.font = "10px monospace";
            ctx.fillText(hex, swX, swatchY + 68);

            swX += 80f;
        }

        // Bottom Clear Space Section
        var clearY = H - 160f;
        ctx.strokeStyle = "#e2e8f0";
        ctx.beginPath();
        ctx.moveTo(40, clearY - 20f); ctx.lineTo(W - 40, clearY - 20f);
        ctx.stroke();

        ctx.fillStyle = "#64748b";
        ctx.font = "bold 12px sans-serif";
        ctx.fillText("CLEAR SPACE & MINIMUM MARGINS (X = 24px)", 40, clearY);

        var clearMarkSize = 50f;
        var clearMarkX = 40f;
        var clearMarkY = clearY + 25f;

        DrawClearSpaceGuide(ctx, new Dictionary<string, object?>
        {
            ["x"] = clearMarkX,
            ["y"] = clearMarkY,
            ["width"] = clearMarkSize,
            ["height"] = clearMarkSize
        }, 24f, new Dictionary<string, object?> { ["showLabels"] = true });

        ctx.save();
        ctx.translate(clearMarkX, clearMarkY);
        InvokeCallback(drawMark, ctx, clearMarkSize);
        ctx.restore();

        ctx.restore();
    }
    #endregion
}