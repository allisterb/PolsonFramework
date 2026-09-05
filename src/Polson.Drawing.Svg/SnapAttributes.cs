namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

public static partial class SnapAttributes
{
    #region Methods
    public static void ApplyAttribute(SvgElement element, string name, object? value, SnapBBox? bbox = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrWhiteSpace(name)) return;

        var key = NormalizeKey(name);
        var valStr = value?.ToString() ?? string.Empty;

        switch (key)
        {
            case "id":
                element.ID = valStr;
                break;

            case "class":
                element.CustomAttributes["class"] = valStr;
                break;

            case "fill":
                element.Fill = ParsePaintServer(value, element);
                break;

            case "stroke":
                element.Stroke = ParsePaintServer(value, element);
                break;

            case "strokewidth":
            case "stroke-width":
                element.StrokeWidth = ParseUnit(ComputeDelta(element.StrokeWidth.Value, valStr));
                break;

            case "strokelinecap":
            case "stroke-linecap":
                element.StrokeLineCap = ParseStrokeLineCap(valStr);
                break;

            case "strokelinejoin":
            case "stroke-linejoin":
                element.StrokeLineJoin = ParseStrokeLineJoin(valStr);
                break;

            case "strokedasharray":
            case "stroke-dasharray":
                element.StrokeDashArray = ParseUnitCollection(valStr);
                break;

            case "strokedashoffset":
            case "stroke-dashoffset":
                element.StrokeDashOffset = ParseUnit(ComputeDelta(element.StrokeDashOffset.Value, valStr));
                break;

            case "opacity":
                element.Opacity = ParseFloat(valStr);
                break;

            case "fillopacity":
            case "fill-opacity":
                element.FillOpacity = ParseFloat(valStr);
                break;

            case "strokeopacity":
            case "stroke-opacity":
                element.StrokeOpacity = ParseFloat(valStr);
                break;

            case "transform":
                element.Transforms = SnapTransformParser.ParseToSvgTransforms(valStr, bbox);
                break;

            case "x":
                SetX(element, valStr);
                break;

            case "y":
                SetY(element, valStr);
                break;

            case "cx":
                SetCx(element, valStr);
                break;

            case "cy":
                SetCy(element, valStr);
                break;

            case "r":
                SetR(element, valStr);
                break;

            case "rx":
                SetRx(element, valStr);
                break;

            case "ry":
                SetRy(element, valStr);
                break;

            case "width":
                SetWidth(element, valStr);
                break;

            case "height":
                SetHeight(element, valStr);
                break;

            case "x1":
                SetX1(element, valStr);
                break;

            case "y1":
                SetY1(element, valStr);
                break;

            case "x2":
                SetX2(element, valStr);
                break;

            case "y2":
                SetY2(element, valStr);
                break;

            case "d":
                if (element is SvgPath path)
                {
                    path.PathData = SvgPathBuilder.Parse(valStr);
                }
                break;

            case "points":
                SetPoints(element, value);
                break;

            case "href":
            case "xlink:href":
            case "src":
                if (element is SvgImage img)
                {
                    img.Href = valStr;
                }
                else if (element is SvgUse use)
                {
                    use.ReferencedElement = new Uri(valStr.StartsWith('#') ? valStr : "#" + valStr, UriKind.RelativeOrAbsolute);
                }
                break;

            case "text":
                if (element is SvgText svgText)
                {
                    svgText.Text = valStr;
                }
                break;

            case "fontfamily":
            case "font-family":
                element.FontFamily = valStr;
                break;

            case "fontsize":
            case "font-size":
                element.FontSize = ParseUnit(ComputeDelta(element.FontSize.Value, valStr));
                break;

            case "fontweight":
            case "font-weight":
                element.FontWeight = ParseFontWeight(valStr);
                break;

            case "textanchor":
            case "text-anchor":
                element.TextAnchor = ParseTextAnchor(valStr);
                break;

            // NormalizeKey lower-cases the key, so a mixed-case viewBox label here would never be reached.
            case "viewbox":
                SetViewBox(element, valStr);
                break;

            case "visibility":
                element.Visibility = valStr;
                break;

            case "display":
                element.Display = valStr;
                break;

            default:
                element.CustomAttributes[name] = valStr;
                break;
        }
    }

    public static void ApplyAttributes(SvgElement element, IDictionary<string, object?> attributes, SnapBBox? bbox = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(attributes);

        foreach (var (k, v) in attributes)
        {
            ApplyAttribute(element, k, v, bbox);
        }
    }

    public static void ApplyAttributes(SvgElement element, object? attributes, SnapBBox? bbox = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (attributes is null) return;

        if (attributes is IDictionary<string, object?> dict)
        {
            ApplyAttributes(element, dict, bbox);
            return;
        }

        if (attributes is System.Collections.IDictionary sysDict)
        {
            foreach (System.Collections.DictionaryEntry entry in sysDict)
            {
                var k = entry.Key?.ToString();
                if (!string.IsNullOrEmpty(k))
                {
                    ApplyAttribute(element, k, entry.Value, bbox);
                }
            }
            return;
        }

        if (attributes is IEnumerable<KeyValuePair<string, object?>> kvps)
        {
            foreach (var (k, v) in kvps)
            {
                ApplyAttribute(element, k, v, bbox);
            }
            return;
        }

        var props = attributes.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var prop in props)
        {
            var val = prop.GetValue(attributes);
            ApplyAttribute(element, prop.Name, val, bbox);
        }
    }

    public static object? GetAttribute(SvgElement element, string name)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var key = NormalizeKey(name);
        return key switch
        {
            "id" => element.ID,
            "class" => element.CustomAttributes.TryGetValue("class", out var cls) ? cls : null,
            "fill" => PaintServerToString(element.Fill),
            "stroke" => PaintServerToString(element.Stroke),
            "strokewidth" or "stroke-width" => element.StrokeWidth.Value,
            "opacity" => element.Opacity,
            "fillopacity" or "fill-opacity" => element.FillOpacity,
            "strokeopacity" or "stroke-opacity" => element.StrokeOpacity,
            "x" => GetX(element),
            "y" => GetY(element),
            "cx" => GetCx(element),
            "cy" => GetCy(element),
            "r" => GetR(element),
            "rx" => GetRx(element),
            "ry" => GetRy(element),
            "width" => GetWidth(element),
            "height" => GetHeight(element),
            "d" => (element as SvgPath)?.PathData?.ToString(),
            "text" => (element as SvgText)?.Text,
            // Settable through attr(), so it must be readable through it. Without this the setter
            // succeeded and the getter answered null, which reads as "the transform did not take".
            "transform" => element.Transforms is { Count: > 0 } t ? t.ToString() : null,

            // The same rule, applied to three more that were settable and unreadable. `display` is
            // what a timeline's visibility window writes, and a window that cannot read back the
            // value it is restoring has nothing to restore.
            "display" => string.IsNullOrEmpty(element.Display) ? null : element.Display,
            "visibility" => string.IsNullOrEmpty(element.Visibility) ? null : element.Visibility,
            "fontfamily" or "font-family" => string.IsNullOrEmpty(element.FontFamily) ? null : element.FontFamily,

            // And to the rest of them. Each needed an enum or a collection put back into its SVG
            // spelling rather than its CLR one — `middle`, not `Middle` — so that what comes out is
            // a value the setter above would accept again. SnapAttributeRoundTripTests holds the whole
            // switch to that: every case label in the setter must have an arm here.
            "strokelinecap" or "stroke-linecap" => StrokeLineCapToString(element.StrokeLineCap),
            "strokelinejoin" or "stroke-linejoin" => StrokeLineJoinToString(element.StrokeLineJoin),
            "strokedasharray" or "stroke-dasharray" => element.StrokeDashArray is { Count: > 0 } dashes ? dashes.ToString() : null,
            "strokedashoffset" or "stroke-dashoffset" => element.StrokeDashOffset.Value,
            "fontsize" or "font-size" => element.FontSize.Value,
            "fontweight" or "font-weight" => FontWeightToString(element.FontWeight),
            "textanchor" or "text-anchor" => TextAnchorToString(element.TextAnchor),
            "x1" => GetX1(element),
            "y1" => GetY1(element),
            "x2" => GetX2(element),
            "y2" => GetY2(element),
            "points" => GetPoints(element),
            "viewbox" => GetViewBox(element),
            "href" or "xlink:href" or "src" => GetHref(element),

            _ => element.CustomAttributes.TryGetValue(name, out var customVal) ? customVal : null
        };
    }

    public static SvgPaintServer ParsePaintServer(object? value, SvgElement? context = null)
    {
        if (value is null) return SvgPaintServer.None;
        if (value is SvgPaintServer server) return server;
        if (value is SnapElement snapEl && snapEl.Node is SvgPaintServer elServer) return elServer;

        var str = value.ToString()?.Trim() ?? string.Empty;
        if (string.Equals(str, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return SvgPaintServer.None;
        }

        if (str.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && str.EndsWith(')'))
        {
            // Keep the whole `url(#id)` form as the deferred id. Handing over the bare `#id` makes the
            // server serialise as `fill:#id`, which then reads back as a colour literal and renders
            // black — a paint-server reference silently degraded into a nonsense colour.
            var uriStr = str[4..^1].Trim('\'', '"', ' ');
#pragma warning disable CS0618
            return new SvgDeferredPaintServer(context?.OwnerDocument, $"url({uriStr})");
#pragma warning restore CS0618
        }

        var color = ParseColor(str);
        return new SvgColourServer(color);
    }

    public static Color ParseColor(string? colorStr)
    {
        if (string.IsNullOrWhiteSpace(colorStr))
            return Color.Black;

        var str = colorStr.Trim();
        if (string.Equals(str, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(str, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Transparent;
        }

        // Hex formats: #rgb, #rrggbb, #rgba, #rrggbbaa
        if (str.StartsWith('#'))
        {
            var hex = str[1..];
            if (hex.Length == 3)
            {
                var r = Convert.ToInt32(new string(hex[0], 2), 16);
                var g = Convert.ToInt32(new string(hex[1], 2), 16);
                var b = Convert.ToInt32(new string(hex[2], 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
            if (hex.Length == 4)
            {
                var r = Convert.ToInt32(new string(hex[0], 2), 16);
                var g = Convert.ToInt32(new string(hex[1], 2), 16);
                var b = Convert.ToInt32(new string(hex[2], 2), 16);
                var a = Convert.ToInt32(new string(hex[3], 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            if (hex.Length == 6)
            {
                var r = Convert.ToInt32(hex[..2], 16);
                var g = Convert.ToInt32(hex[2..4], 16);
                var b = Convert.ToInt32(hex[4..6], 16);
                return Color.FromArgb(255, r, g, b);
            }
            if (hex.Length == 8)
            {
                var r = Convert.ToInt32(hex[..2], 16);
                var g = Convert.ToInt32(hex[2..4], 16);
                var b = Convert.ToInt32(hex[4..6], 16);
                var a = Convert.ToInt32(hex[6..8], 16);
                return Color.FromArgb(a, r, g, b);
            }
        }

        // rgb(...) / rgba(...)
        var rgbMatch = RgbRegex().Match(str);
        if (rgbMatch.Success)
        {
            var r = ParseColorChannel(rgbMatch.Groups[1].Value);
            var g = ParseColorChannel(rgbMatch.Groups[2].Value);
            var b = ParseColorChannel(rgbMatch.Groups[3].Value);
            var a = rgbMatch.Groups[4].Success ? (int)(ParseAlphaChannel(rgbMatch.Groups[4].Value) * 255f) : 255;
            return Color.FromArgb(Math.Clamp(a, 0, 255), Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
        }

        // hsl(...) / hsla(...)
        var hslMatch = HslRegex().Match(str);
        if (hslMatch.Success)
        {
            var h = float.Parse(hslMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var s = float.Parse(hslMatch.Groups[2].Value.TrimEnd('%'), CultureInfo.InvariantCulture) / 100f;
            var l = float.Parse(hslMatch.Groups[3].Value.TrimEnd('%'), CultureInfo.InvariantCulture) / 100f;
            var a = hslMatch.Groups[4].Success ? ParseAlphaChannel(hslMatch.Groups[4].Value) : 1f;
            return HslToRgb(h, s, l, a);
        }

        // Named colors
        var namedColor = Color.FromName(str);
        if (namedColor.IsKnownColor)
            return namedColor;

        return Color.Black;
    }

    public static SvgUnit ParseUnit(object? val)
    {
        if (val is null) return new SvgUnit(0f);
        if (val is SvgUnit unit) return unit;
        if (val is float f) return new SvgUnit(f);
        if (val is double d) return new SvgUnit((float)d);
        if (val is int i) return new SvgUnit(i);

        var str = val.ToString()?.Trim() ?? "0";
        if (str.EndsWith('%'))
        {
            if (float.TryParse(str[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                return new SvgUnit(SvgUnitType.Percentage, pct);
        }
        if (str.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(str[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                return new SvgUnit(SvgUnitType.Pixel, px);
        }
        if (str.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(str[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var em))
                return new SvgUnit(SvgUnitType.Em, em);
        }
        if (float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
        {
            return new SvgUnit(num);
        }
        return new SvgUnit(0f);
    }

    private static string NormalizeKey(string key) =>
        key.Trim().Replace("_", "").ToLowerInvariant();

    private static float ComputeDelta(float currentVal, string input)
    {
        if (input.StartsWith("+=") && float.TryParse(input[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var add))
            return currentVal + add;
        if (input.StartsWith("-=") && float.TryParse(input[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var sub))
            return currentVal - sub;
        if (input.StartsWith("*=") && float.TryParse(input[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var mul))
            return currentVal * mul;
        if (input.StartsWith("/=") && float.TryParse(input[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var div) && Math.Abs(div) > 1e-6f)
            return currentVal / div;
        if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var direct))
            return direct;
        return currentVal;
    }

    private static float ParseFloat(string val) =>
        float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 1f;

    private static int ParseColorChannel(string val)
    {
        var str = val.Trim();
        if (str.EndsWith('%'))
        {
            var pct = float.Parse(str[..^1], CultureInfo.InvariantCulture);
            return (int)(pct * 255f / 100f);
        }
        return (int)float.Parse(str, CultureInfo.InvariantCulture);
    }

    private static float ParseAlphaChannel(string val)
    {
        var str = val.Trim();
        if (str.EndsWith('%'))
        {
            var pct = float.Parse(str[..^1], CultureInfo.InvariantCulture);
            return pct / 100f;
        }
        return float.Parse(str, CultureInfo.InvariantCulture);
    }

    private static Color HslToRgb(float h, float s, float l, float a)
    {
        h = (h % 360f + 360f) % 360f / 360f;
        float r, g, b;
        if (s == 0f)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            var p = 2f * l - q;
            r = HueToRgb(p, q, h + 1f / 3f);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1f / 3f);
        }
        return Color.FromArgb((int)(a * 255f), (int)(r * 255f), (int)(g * 255f), (int)(b * 255f));
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    private static SvgStrokeLineCap ParseStrokeLineCap(string val) =>
        val.ToLowerInvariant() switch
        {
            "round" => SvgStrokeLineCap.Round,
            "square" => SvgStrokeLineCap.Square,
            _ => SvgStrokeLineCap.Butt
        };

    private static SvgStrokeLineJoin ParseStrokeLineJoin(string val) =>
        val.ToLowerInvariant() switch
        {
            "round" => SvgStrokeLineJoin.Round,
            "bevel" => SvgStrokeLineJoin.Bevel,
            _ => SvgStrokeLineJoin.Miter
        };

    private static SvgFontWeight ParseFontWeight(string val) =>
        val.ToLowerInvariant() switch
        {
            "bold" => SvgFontWeight.Bold,
            "bolder" => SvgFontWeight.Bolder,
            "lighter" => SvgFontWeight.Lighter,
            "100" => SvgFontWeight.W100,
            "200" => SvgFontWeight.W200,
            "300" => SvgFontWeight.W300,
            "400" => SvgFontWeight.W400,
            "500" => SvgFontWeight.W500,
            "600" => SvgFontWeight.W600,
            "700" => SvgFontWeight.W700,
            "800" => SvgFontWeight.W800,
            "900" => SvgFontWeight.W900,
            _ => SvgFontWeight.Normal
        };

    private static SvgTextAnchor ParseTextAnchor(string val) =>
        val.ToLowerInvariant() switch
        {
            "middle" or "center" => SvgTextAnchor.Middle,
            "end" or "right" => SvgTextAnchor.End,
            _ => SvgTextAnchor.Start
        };

    private static SvgUnitCollection ParseUnitCollection(string val)
    {
        var collection = new SvgUnitCollection();
        var parts = val.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            collection.Add(ParseUnit(p));
        }
        return collection;
    }

    // The inverses of the four parsers above. Each returns the spelling SVG uses — which is also the
    // spelling its own parser accepts — so attr(name, v) followed by attr(name) yields something the
    // setter would take again. The CLR enum name would not: it says `Middle` where SVG says `middle`.
    // Each default arm mirrors its parser's, so the pair agrees on what an unrecognised value means.
    private static string StrokeLineCapToString(SvgStrokeLineCap val) =>
        val switch
        {
            SvgStrokeLineCap.Round => "round",
            SvgStrokeLineCap.Square => "square",
            SvgStrokeLineCap.Inherit => "inherit",
            _ => "butt"
        };

    private static string StrokeLineJoinToString(SvgStrokeLineJoin val) =>
        val switch
        {
            SvgStrokeLineJoin.Round => "round",
            SvgStrokeLineJoin.Bevel => "bevel",
            SvgStrokeLineJoin.MiterClip => "miter-clip",
            SvgStrokeLineJoin.Arcs => "arcs",
            SvgStrokeLineJoin.Inherit => "inherit",
            _ => "miter"
        };

    private static string FontWeightToString(SvgFontWeight val) =>
        val switch
        {
            SvgFontWeight.Bold => "bold",
            SvgFontWeight.Bolder => "bolder",
            SvgFontWeight.Lighter => "lighter",
            SvgFontWeight.W100 => "100",
            SvgFontWeight.W200 => "200",
            SvgFontWeight.W300 => "300",
            SvgFontWeight.W400 => "400",
            SvgFontWeight.W500 => "500",
            SvgFontWeight.W600 => "600",
            SvgFontWeight.W700 => "700",
            SvgFontWeight.W800 => "800",
            SvgFontWeight.W900 => "900",
            SvgFontWeight.Inherit => "inherit",
            _ => "normal"
        };

    private static string TextAnchorToString(SvgTextAnchor val) =>
        val switch
        {
            SvgTextAnchor.Middle => "middle",
            SvgTextAnchor.End => "end",
            SvgTextAnchor.Inherit => "inherit",
            _ => "start"
        };

    private static string PaintServerToString(SvgPaintServer? server)
    {
        if (server is null || server == SvgPaintServer.None) return "none";
        if (server is SvgColourServer col)
        {
            return col.Colour.A < 255
                ? $"rgba({col.Colour.R},{col.Colour.G},{col.Colour.B},{(col.Colour.A / 255f):0.##})"
                : $"#{col.Colour.R:X2}{col.Colour.G:X2}{col.Colour.B:X2}";
        }
        return server.ToString() ?? "none";
    }

    #region Element Coordinate Setters/Getters
    private static void SetX(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgRectangle rect) rect.X = unit;
        else if (el is SvgImage img) img.X = unit;
        else if (el is SvgText text) text.X = new SvgUnitCollection { unit };
        else if (el is SvgFragment frag) frag.X = unit;
    }

    private static void SetY(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgRectangle rect) rect.Y = unit;
        else if (el is SvgImage img) img.Y = unit;
        else if (el is SvgText text) text.Y = new SvgUnitCollection { unit };
        else if (el is SvgFragment frag) frag.Y = unit;
    }

    private static void SetCx(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgCircle circle) circle.CenterX = unit;
        else if (el is SvgEllipse ellipse) ellipse.CenterX = unit;
        else if (el is SvgRadialGradientServer radial) radial.CenterX = unit;
    }

    private static void SetCy(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgCircle circle) circle.CenterY = unit;
        else if (el is SvgEllipse ellipse) ellipse.CenterY = unit;
        else if (el is SvgRadialGradientServer radial) radial.CenterY = unit;
    }

    private static void SetR(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgCircle circle) circle.Radius = unit;
        else if (el is SvgRadialGradientServer radial) radial.Radius = unit;
    }

    private static void SetRx(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgRectangle rect) rect.CornerRadiusX = unit;
        else if (el is SvgEllipse ellipse) ellipse.RadiusX = unit;
    }

    private static void SetRy(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgRectangle rect) rect.CornerRadiusY = unit;
        else if (el is SvgEllipse ellipse) ellipse.RadiusY = unit;
    }

    private static void SetWidth(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgRectangle rect) rect.Width = unit;
        else if (el is SvgImage img) img.Width = unit;
        else if (el is SvgFragment frag) frag.Width = unit;
    }

    private static void SetHeight(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgRectangle rect) rect.Height = unit;
        else if (el is SvgImage img) img.Height = unit;
        else if (el is SvgFragment frag) frag.Height = unit;
    }

    private static void SetX1(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgLine line) line.StartX = unit;
        else if (el is SvgLinearGradientServer linear) linear.X1 = unit;
    }

    private static void SetY1(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgLine line) line.StartY = unit;
        else if (el is SvgLinearGradientServer linear) linear.Y1 = unit;
    }

    private static void SetX2(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgLine line) line.EndX = unit;
        else if (el is SvgLinearGradientServer linear) linear.X2 = unit;
    }

    private static void SetY2(SvgElement el, string val)
    {
        var unit = ParseUnit(val);
        if (el is SvgLine line) line.EndY = unit;
        else if (el is SvgLinearGradientServer linear) linear.Y2 = unit;
    }

    private static void SetPoints(SvgElement el, object? value)
    {
        var collection = new SvgPointCollection();
        if (value is IEnumerable<float> floats)
        {
            var list = new List<float>(floats);
            for (var i = 0; i < list.Count; i += 2)
            {
                var x = list[i];
                var y = i + 1 < list.Count ? list[i + 1] : 0f;
                collection.Add(new SvgUnit(x));
                collection.Add(new SvgUnit(y));
            }
        }
        else if (value is IEnumerable<object> objs)
        {
            var list = new List<float>();
            foreach (var o in objs)
            {
                if (float.TryParse(o?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    list.Add(f);
            }
            for (var i = 0; i < list.Count; i += 2)
            {
                var x = list[i];
                var y = i + 1 < list.Count ? list[i + 1] : 0f;
                collection.Add(new SvgUnit(x));
                collection.Add(new SvgUnit(y));
            }
        }
        else if (value is string str)
        {
            var parts = str.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                collection.Add(ParseUnit(p));
            }
        }

        if (el is SvgPolyline polyline) polyline.Points = collection;
        else if (el is SvgPolygon polygon) polygon.Points = collection;
    }

    private static void SetViewBox(SvgElement el, string val)
    {
        var parts = val.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) &&
            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
        {
            if (el is SvgFragment frag)
            {
                frag.ViewBox = new SvgViewBox(x, y, w, h);
            }
        }
    }

    private static object? GetX(SvgElement el) =>
        el switch
        {
            SvgRectangle r => r.X.Value,
            SvgImage img => img.X.Value,
            SvgText t => t.X?.Count > 0 ? t.X[0].Value : 0f,
            _ => null
        };

    private static object? GetY(SvgElement el) =>
        el switch
        {
            SvgRectangle r => r.Y.Value,
            SvgImage img => img.Y.Value,
            SvgText t => t.Y?.Count > 0 ? t.Y[0].Value : 0f,
            _ => null
        };

    private static object? GetCx(SvgElement el) =>
        el switch
        {
            SvgCircle c => c.CenterX.Value,
            SvgEllipse e => e.CenterX.Value,
            SvgRadialGradientServer radial => radial.CenterX.Value,
            _ => null
        };

    private static object? GetCy(SvgElement el) =>
        el switch
        {
            SvgCircle c => c.CenterY.Value,
            SvgEllipse e => e.CenterY.Value,
            SvgRadialGradientServer radial => radial.CenterY.Value,
            _ => null
        };

    private static object? GetR(SvgElement el) =>
        el switch
        {
            SvgCircle c => c.Radius.Value,
            SvgRadialGradientServer radial => radial.Radius.Value,
            _ => null
        };

    private static object? GetRx(SvgElement el) =>
        el switch
        {
            SvgRectangle r => r.CornerRadiusX.Value,
            SvgEllipse e => e.RadiusX.Value,
            _ => null
        };

    private static object? GetRy(SvgElement el) =>
        el switch
        {
            SvgRectangle r => r.CornerRadiusY.Value,
            SvgEllipse e => e.RadiusY.Value,
            _ => null
        };

    private static object? GetWidth(SvgElement el) =>
        el switch
        {
            SvgRectangle r => r.Width.Value,
            SvgImage img => img.Width.Value,
            SvgFragment f => f.Width.Value,
            _ => null
        };

    private static object? GetHeight(SvgElement el) =>
        el switch
        {
            SvgRectangle r => r.Height.Value,
            SvgImage img => img.Height.Value,
            SvgFragment f => f.Height.Value,
            _ => null
        };

    // Each reads back from whatever the matching setter above writes to, gradient servers included,
    // so `null` from here means "this element has no such attribute" rather than "unimplemented".
    private static object? GetX1(SvgElement el) =>
        el switch
        {
            SvgLine line => line.StartX.Value,
            SvgLinearGradientServer linear => linear.X1.Value,
            _ => null
        };

    private static object? GetY1(SvgElement el) =>
        el switch
        {
            SvgLine line => line.StartY.Value,
            SvgLinearGradientServer linear => linear.Y1.Value,
            _ => null
        };

    private static object? GetX2(SvgElement el) =>
        el switch
        {
            SvgLine line => line.EndX.Value,
            SvgLinearGradientServer linear => linear.X2.Value,
            _ => null
        };

    private static object? GetY2(SvgElement el) =>
        el switch
        {
            SvgLine line => line.EndY.Value,
            SvgLinearGradientServer linear => linear.Y2.Value,
            _ => null
        };

    // `SvgPointCollection` serialises as `1,2 3,4`, which SetPoints parses back to the same points —
    // it splits on comma and whitespace alike. The list form the setter also takes is not recoverable
    // from the element, so one spelling has to serve, and the SVG one is what the markup holds.
    private static object? GetPoints(SvgElement el)
    {
        // SvgPolyline derives from SvgPolygon here, so the base type covers both elements.
        var points = (el as SvgPolygon)?.Points;
        return points is { Count: > 0 } ? points.ToString() : null;
    }

    // SvgViewBox.ToString() is the inherited object one — it answers "Svg.SvgViewBox" — so the four
    // numbers are formatted here, invariantly, in the order SetViewBox reads them.
    private static object? GetViewBox(SvgElement el) =>
        el is SvgFragment frag && frag.ViewBox != SvgViewBox.Empty
            ? string.Create(CultureInfo.InvariantCulture, $"{frag.ViewBox.MinX} {frag.ViewBox.MinY} {frag.ViewBox.Width} {frag.ViewBox.Height}")
            : null;

    private static object? GetHref(SvgElement el) =>
        el switch
        {
            SvgImage img => string.IsNullOrEmpty(img.Href) ? null : img.Href,
            SvgUse use => use.ReferencedElement?.ToString() is { Length: > 0 } href ? href : null,
            _ => null
        };
    #endregion

    [GeneratedRegex(@"rgba?\(\s*(\d+%?)\s*,\s*(\d+%?)\s*,\s*(\d+%?)(?:\s*,\s*([0-9.]+%?))?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RgbRegex();

    [GeneratedRegex(@"hsla?\(\s*([0-9.]+)\s*,\s*([0-9.]+%)\s*,\s*([0-9.]+%)(?:\s*,\s*([0-9.]+%?))?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex HslRegex();
    #endregion
}

