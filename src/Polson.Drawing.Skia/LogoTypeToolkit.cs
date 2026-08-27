namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SkiaSharp;

/// <summary>Typography, optical kerning and brand lockup toolkit (Studio Manual 11).</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class LogoTypeToolkit
{
    #region Nested Types
    public record struct KerningPairResult(char Left, char Right, float OffsetPx, string GeometryType);
    public record struct TypographicScaleStep(string Name, float SizePx, float LineHeightPx, float TrackingEm);
    #endregion

    #region Optical Kerning & Letter-Spacing Mechanics
    public static string GetGlyphShapeType(char c)
    {
        var upper = char.ToUpperInvariant(c);
        if ("HIMN BDFKLP RUhilm nu".Contains(c)) return "straight";
        if ("OCGQS ocdeg pqs".Contains(c)) return "round";
        if ("AVWXY TJZa vwxy tz".Contains(c)) return "diagonal";
        return "neutral";
    }

    public float ComputeOpticalKerning(char left, char right, float fontSize = 32f, string fontCategory = "sans")
    {
        var typeL = GetGlyphShapeType(left);
        var typeR = GetGlyphShapeType(right);
        var upperL = char.ToUpperInvariant(left);
        var upperR = char.ToUpperInvariant(right);

        // Extreme diagonal/open tucking pairs (Doyald Young rules)
        if ((upperL == 'T' && (upperR == 'A' || upperR == 'O' || upperR == 'Y')) ||
            (upperL == 'A' && (upperR == 'V' || upperR == 'W' || upperR == 'T' || upperR == 'Y')) ||
            (upperL == 'V' && (upperR == 'A' || upperR == 'O')) ||
            (upperL == 'W' && (upperR == 'A' || upperR == 'O')) ||
            (upperL == 'L' && (upperR == 'T' || upperR == 'V' || upperR == 'W' || upperR == 'Y')) ||
            (upperL == 'P' && upperR == 'A') ||
            (upperL == 'F' && (upperR == 'A' || upperR == 'O')))
        {
            return -0.09f * fontSize;
        }

        // Shape-based optical kerning factors
        if (typeL == "straight" && typeR == "straight") return 0.04f * fontSize;  // Max spacing
        if (typeL == "straight" && typeR == "round") return 0.025f * fontSize;    // Medium spacing
        if (typeL == "round" && typeR == "straight") return 0.025f * fontSize;
        if (typeL == "round" && typeR == "round") return 0.012f * fontSize;       // Tight spacing
        if (typeL == "diagonal" || typeR == "diagonal") return -0.035f * fontSize;// Tuck spacing

        return 0.02f * fontSize;
    }

    public float ComputeWordmarkTracking(float fontSize = 36f, bool isAllCaps = false, string role = "wordmark")
    {
        var normRole = (role ?? "wordmark").Trim().ToLowerInvariant();
        if (normRole == "tagline" || normRole == "subhead" || normRole == "caption")
        {
            // Generous tracking for small/medium subheads
            return isAllCaps ? 0.22f : 0.06f;
        }

        // Large display wordmark: tighter tracking for cohesive silhouette
        if (fontSize >= 48f) return isAllCaps ? -0.015f : -0.035f;
        if (fontSize >= 28f) return isAllCaps ? 0.0f : -0.02f;
        return isAllCaps ? 0.08f : 0.0f;
    }
    #endregion

    #region Typographic Scale & Harmonic Ratios
    public static float GetRatioFactor(string ratioName) =>
        (ratioName ?? "goldenRatio").Trim().ToLowerInvariant() switch
        {
            "goldenratio" or "phi" => 1.61803398875f,
            "perfectfifth" => 1.500f,
            "augmentedfourth" => 1.41421356f,
            "perfectfourth" => 1.33333333f,
            "majorthird" => 1.250f,
            "minorthird" => 1.200f,
            "majorsecond" => 1.125f,
            _ => 1.33333333f
        };

    public Dictionary<string, object?> CalculateTypographicScale(float baseSize = 16f, string ratio = "goldenRatio", int stepsDown = 2, int stepsUp = 5)
    {
        var r = GetRatioFactor(ratio);
        var namesDown = new[] { "caption", "micro" };
        var namesUp = new[] { "body", "h4", "h3", "h2", "h1", "display", "hero" };

        var scaleDict = new Dictionary<string, object?>();
        var stepsList = new List<Dictionary<string, object?>>();

        // Steps down
        for (var i = stepsDown; i >= 1; i--)
        {
            var size = baseSize * MathF.Pow(r, -i);
            var name = i <= namesDown.Length ? namesDown[i - 1] : $"sub_{i}";
            var entry = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["size"] = MathF.Round(size, 1),
                ["lineHeight"] = MathF.Round(size * 1.35f, 1),
                ["tracking"] = 0.15f
            };
            scaleDict[name] = entry;
            stepsList.Add(entry);
        }

        // Base & steps up
        for (var i = 0; i <= stepsUp; i++)
        {
            var size = baseSize * MathF.Pow(r, i);
            var name = i < namesUp.Length ? namesUp[i] : $"step_{i}";
            var tracking = i == 0 ? 0.0f : (i >= 4 ? -0.03f : 0.0f);
            var entry = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["size"] = MathF.Round(size, 1),
                ["lineHeight"] = MathF.Round(size * 1.25f, 1),
                ["tracking"] = tracking
            };
            scaleDict[name] = entry;
            stepsList.Add(entry);
        }

        scaleDict["baseSize"] = baseSize;
        scaleDict["ratioName"] = ratio;
        scaleDict["ratioFactor"] = r;
        scaleDict["steps"] = stepsList;

        return scaleDict;
    }
    #endregion

    #region Font Pairing & Contrast Evaluation (Robin Williams Matrix)
    public Dictionary<string, object?> EvaluateFontPairing(string primaryCategory, string secondaryCategory)
    {
        var cat1 = (primaryCategory ?? "sansSerif").Trim().ToLowerInvariant();
        var cat2 = (secondaryCategory ?? "sansSerif").Trim().ToLowerInvariant();

        string relationship;
        var score = 85;
        var recommendations = new List<string>();
        string description;

        if (cat1 == cat2)
        {
            relationship = "concordant";
            score = 75;
            description = "Concordant pairing within the same type family. Very clean, elegant, and safe.";
            recommendations.Add("Ensure strong contrast of WEIGHT (e.g. Black Bold title vs Light/Regular subhead).");
            recommendations.Add("Ensure strong contrast of SIZE (e.g. 36pt title vs 12pt subhead).");
            recommendations.Add("Use wide all-caps tracking for the secondary subhead to establish clear hierarchy.");
        }
        else if ((cat1.Contains("sans") && cat2.Contains("serif") && !cat2.Contains("slab")) ||
                 (cat1.Contains("serif") && !cat1.Contains("slab") && cat2.Contains("sans")) ||
                 (cat1.Contains("modern") && cat2.Contains("sans")) ||
                 (cat1.Contains("slab") && cat2.Contains("sans")))
        {
            relationship = "contrasting";
            score = 95;
            description = "High-contrast dynamic pairing across distinct anatomical structures (Sans + Serif). Ideal for modern brand systems.";
            recommendations.Add("Assign the dominant brand voice to the Primary Wordmark.");
            recommendations.Add("Use the clean geometric counterpart for taglines, descriptors, and UI labels.");
        }
        else if ((cat1.Contains("oldstyle") && cat2.Contains("modern")) ||
                 (cat1.Contains("slab") && cat2.Contains("oldstyle")))
        {
            relationship = "conflicting";
            score = 40;
            description = "Conflicting pairing between two similar serif categories. The subtle difference causes visual tension and looks unintentional.";
            recommendations.Add("Replace one of the serifs with a clean geometric Sans-Serif.");
            recommendations.Add("Or unify both into a single font family (Concordant pairing).");
        }
        else
        {
            relationship = "contrasting";
            score = 90;
            description = "Contrasting display pairing.";
            recommendations.Add("Ensure the secondary font remains neutral and legible at small sizes.");
        }

        return new Dictionary<string, object?>
        {
            ["primaryCategory"] = primaryCategory,
            ["secondaryCategory"] = secondaryCategory,
            ["relationship"] = relationship,
            ["score"] = score,
            ["description"] = description,
            ["recommendations"] = recommendations
        };
    }
    #endregion

    #region Doyald Young Ogee Curves (S-Curves)
    public string CreateOgeeCurvePath(float x1, float y1, float x2, float y2, float inflectionT = 0.5f, float amplitude = 25f)
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

        // First arc control points
        var c1x = x1 + dx * (t * 0.4f) + nx * amplitude;
        var c1y = y1 + dy * (t * 0.4f) + ny * amplitude;
        var c2x = x1 + dx * (t * 0.8f) + nx * amplitude;
        var c2y = y1 + dy * (t * 0.8f) + ny * amplitude;

        // Second arc control points (inverted normal)
        var remT = 1f - t;
        var c3x = midX + dx * (remT * 0.3f) - nx * amplitude;
        var c3y = midY + dy * (remT * 0.3f) - ny * amplitude;
        var c4x = midX + dx * (remT * 0.7f) - nx * amplitude;
        var c4y = midY + dy * (remT * 0.7f) - ny * amplitude;

        return string.Format(CultureInfo.InvariantCulture,
            "M {0:F2} {1:F2} C {2:F2} {3:F2} {4:F2} {5:F2} {6:F2} {7:F2} C {8:F2} {9:F2} {10:F2} {11:F2} {12:F2} {13:F2}",
            x1, y1, c1x, c1y, c2x, c2y, midX, midY, c3x, c3y, c4x, c4y, x2, y2);
    }

    public SKPath CreateOgeeCurveSKPath(float x1, float y1, float x2, float y2, float inflectionT = 0.5f, float amplitude = 25f)
    {
        var path = new SKPath();
        var dx = x2 - x1;
        var dy = y2 - y1;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f)
        {
            path.MoveTo(x1, y1);
            path.LineTo(x2, y2);
            return path;
        }

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

        path.MoveTo(x1, y1);
        path.CubicTo(c1x, c1y, c2x, c2y, midX, midY);
        path.CubicTo(c3x, c3y, c4x, c4y, x2, y2);
        return path;
    }
    #endregion

    #region Automated Brand Wordmark Lockup Engine
    public void DrawWordmarkLockup(CanvasRenderingContext2D ctx, object? drawMarkFn, string brandName, string tagline, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var layout = LogoDesignToolkit.GetProp(options, "layout")?.ToString()?.ToLowerInvariant() ?? "horizontal";
        var primaryColor = LogoDesignToolkit.GetProp(options, "primaryColor")?.ToString() ?? "#ffffff";
        var taglineColor = LogoDesignToolkit.GetProp(options, "taglineColor")?.ToString() ?? "#94a3b8";
        var markSize = Convert.ToSingle(LogoDesignToolkit.GetProp(options, "markSize") ?? 80f, CultureInfo.InvariantCulture);
        var originX = Convert.ToSingle(LogoDesignToolkit.GetProp(options, "x") ?? 100f, CultureInfo.InvariantCulture);
        var originY = Convert.ToSingle(LogoDesignToolkit.GetProp(options, "y") ?? 100f, CultureInfo.InvariantCulture);
        var brandFontSize = Convert.ToSingle(LogoDesignToolkit.GetProp(options, "fontSize") ?? 38f, CultureInfo.InvariantCulture);
        var taglineFontSize = Convert.ToSingle(LogoDesignToolkit.GetProp(options, "taglineSize") ?? 12f, CultureInfo.InvariantCulture);

        ctx.Save();

        if (layout == "vertical" || layout == "stacked")
        {
            // Vertical Stacked Lockup
            var markX = originX;
            var markY = originY;

            if (drawMarkFn != null)
            {
                ctx.Save();
                ctx.Translate(markX, markY);
                LogoDesignToolkit.InvokeCallback(drawMarkFn, [ctx, markSize]);
                ctx.Restore();
            }

            // Wordmark below mark
            ctx.FillStyle = primaryColor;
            ctx.Font = $"700 {brandFontSize}px sans-serif";
            ctx.TextAlign = "center";
            ctx.TextBaseline = "top";
            var textY = markY + markSize + 18f;
            ctx.FillText(brandName ?? string.Empty, markX + markSize * 0.5f, textY);

            // Tagline below wordmark
            if (!string.IsNullOrWhiteSpace(tagline))
            {
                ctx.FillStyle = taglineColor;
                ctx.Font = $"500 {taglineFontSize}px sans-serif";
                ctx.FillText(tagline.ToUpperInvariant(), markX + markSize * 0.5f, textY + brandFontSize + 8f);
            }
        }
        else
        {
            // Horizontal Lockup
            var markX = originX;
            var markY = originY;

            if (drawMarkFn != null)
            {
                ctx.Save();
                ctx.Translate(markX, markY);
                LogoDesignToolkit.InvokeCallback(drawMarkFn, [ctx, markSize]);
                ctx.Restore();
            }

            var textStartX = markX + markSize + 24f;
            var midY = markY + markSize * 0.5f;

            // Brand Wordmark
            ctx.FillStyle = primaryColor;
            ctx.Font = $"700 {brandFontSize}px sans-serif";
            ctx.TextAlign = "left";
            ctx.TextBaseline = string.IsNullOrWhiteSpace(tagline) ? "middle" : "alphabetic";

            var wordmarkY = string.IsNullOrWhiteSpace(tagline) ? midY : midY + 4f;
            ctx.FillText(brandName ?? string.Empty, textStartX, wordmarkY);

            // Tagline
            if (!string.IsNullOrWhiteSpace(tagline))
            {
                ctx.FillStyle = taglineColor;
                ctx.Font = $"500 {taglineFontSize}px sans-serif";
                ctx.TextBaseline = "top";
                ctx.FillText(tagline.ToUpperInvariant(), textStartX + 1f, wordmarkY + 6f);
            }
        }

        ctx.Restore();
    }
    #endregion
}
