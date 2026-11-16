// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;

/// <summary>
/// The <c>font-family</c> lists in a vector document that name no installed family, so the type draws in a substitute.
/// </summary>
/// <remarks>
/// <para>
/// Skia substitutes silently. poster3's review found that on Windows <c>'Franklin Gothic Heavy'</c> — the legacy name of
/// one weight — draws in a default serif, while <c>'Franklin Gothic'</c> at weight 900 draws the Heavy face; and the
/// renderer's substitute is not even the one <c>Skia.Font.resolve</c> reports (a serif drawn, Segoe UI reported), so the
/// warning names no substitute. A <c>direction.md</c> can then credit a typeface the poster does not use.
/// </para>
/// <para>
/// A list is reported only when <b>none</b> of its named families is installed: <c>'Helvetica Neue', Arial, sans-serif</c>
/// is a list doing its job. Generic families (<c>sans-serif</c>, <c>serif</c>…) are not names to check, and a list of
/// nothing else is never reported.
/// </para>
/// </remarks>
public static class FontAudit
{
    #region Methods
    /// <summary>One warning per distinct list naming no installed family, with where it is used and, for a weight
    /// written into the name, the family and <c>font-weight</c> that draw it.</summary>
    /// <param name="installed">Whether a family resolves to itself; the machine's fonts when omitted.</param>
    /// <param name="families">Installed family names, for the weight suggestion; the machine's when omitted.</param>
    /// <param name="faceWeight">The weight of the face a family actually matches for a requested weight and slant, or
    /// null for none; the machine's when omitted. A suggestion is made only when it lands within 100 of the request.</param>
    public static IReadOnlyList<string> Check(string svgXml, Func<string, bool>? installed = null, IEnumerable<string>? families = null,
        Func<string, int, bool, int?>? faceWeight = null)
    {
        if (string.IsNullOrWhiteSpace(svgXml)) return [];

        XDocument doc;
        try { doc = XDocument.Parse(svgXml); }
        catch (System.Xml.XmlException) { return []; }

        var uses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var element in doc.Descendants())
        {
            if (FontFamily(element) is not { Length: > 0 } list) continue;
            if (!uses.TryGetValue(list, out var where)) uses[list] = where = [];
            where.Add(element.Attribute("id")?.Value is { Length: > 0 } id ? "#" + id : "<" + element.Name.LocalName + ">");
        }

        installed ??= IsInstalled;
        faceWeight ??= FaceWeight;
        var known = (families ?? InstalledFamilies()).ToList();
        var warnings = new List<string>();
        foreach (var (list, where) in uses)
        {
            var names = Names(list).Where(n => !Generic.Contains(n)).ToList();
            if (names.Count == 0 || names.Any(installed)) continue;

            var at = string.Join(", ", where.Distinct().Take(3)) + (where.Distinct().Count() > 3 ? ", …" : "");
            var advice = names.Select(n => Suggest(n, known, faceWeight)).FirstOrDefault(s => s is not null)
                ?? "Skia.Font.families() lists what is installed; Skia.Font.has(name) checks one.";
            warnings.Add($"font-family \"{list}\" ({at}) names no installed family, so it draws in a substitute face "
                + $"and the type is not the one chosen. {advice}");
        }
        return warnings;
    }
    /// <summary>
    /// For a family name that is not installed, the installed family and weight or style it names, checked to exist —
    /// <c>'Franklin Gothic Heavy' is a face of 'Franklin Gothic': …</c> — or null when there is none to suggest.
    /// </summary>
    public static string? Suggestion(string name) => Suggest(name, [.. InstalledFamilies()], FaceWeight);

    /// <summary>Whether <paramref name="family"/> is installed under that name, rather than substituted.</summary>
    public static bool Installed(string family) => IsInstalled(family);

    /// <summary>
    /// <paramref name="list"/>, when it names an installed family; refused when it names only generics or only families
    /// that are not installed, as Motion text refuses a substitute face.
    /// </summary>
    /// <remarks>
    /// <see cref="Check"/> warns and passes over a generic-only list, and lanternkeepercloud1 (2026-10-24) set a poster's
    /// English lines in <c>sans-serif</c> at weight 300: the render drew a stock face at regular weight, a browser would
    /// draw its own, and neither was chosen. A paper is a deliverable, so the list is refused where it is set. A generic
    /// after an installed family is a fallback doing its job and passes; <c>inherit</c> and an empty value pass.
    /// </remarks>
    public static string Require(string list, Func<string, bool>? installed = null)
    {
        if (string.IsNullOrWhiteSpace(list) || list.Trim() is "inherit" or "initial" or "unset") return list;
        installed ??= IsInstalled;
        var concrete = Names(list).Where(n => !Generic.Contains(n)).ToList();
        if (concrete.Any(installed)) return list;

        var advice = concrete.Select(Suggestion).FirstOrDefault(s => s is not null)
            ?? "Skia.Font.families() lists what is installed; Skia.Font.has(name) checks one.";
        throw new ArgumentException(concrete.Count == 0
            ? $"font-family \"{list}\" names only a generic family, which each viewer resolves to a face of its own, so the render "
              + $"here and the SVG in a browser would disagree and neither face was chosen. Name an installed family first and "
              + $"keep the generic as the last fallback. {advice}"
            : $"font-family \"{list}\" names no installed family, so it would draw in a substitute face and the type would not be "
              + $"the one chosen. {advice}");
    }
    #endregion

    #region Methods (private)
    /// <summary>The element's own list, from its attribute or its <c>style</c>; inherited lists are checked where declared.</summary>
    private static string? FontFamily(XElement element)
    {
        if (element.Attribute("font-family")?.Value is { } attr) return attr.Trim();
        var style = element.Attribute("style")?.Value;
        if (style is null) return null;
        return style.Split(';')
            .Select(d => d.Split(':', 2))
            .Where(d => d.Length == 2 && d[0].Trim().Equals("font-family", StringComparison.OrdinalIgnoreCase))
            .Select(d => d[1].Trim())
            .LastOrDefault();
    }

    private static IEnumerable<string> Names(string list) =>
        list.Split(',').Select(f => f.Trim().Trim('\'', '"').Trim()).Where(f => f.Length > 0);

    /// <summary>For a weight or style written into the name — <c>Franklin Gothic Heavy</c> — the installed family it is
    /// a face of, and the attributes that select it. Null unless every word after the family is a weight or a slant,
    /// and unless the family really has a face near that weight: an agent obeys a suggestion, so a wrong one is worse
    /// than none.</summary>
    private static string? Suggest(string name, IReadOnlyList<string> families, Func<string, int, bool, int?> faceWeight)
    {
        var family = families
            .Where(f => name.Length > f.Length + 1 && name.StartsWith(f + " ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Length)
            .FirstOrDefault();
        if (family is null) return null;

        var words = name[(family.Length + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int? weight = null;
        var italic = false;
        foreach (var word in words)
        {
            if (Weights.TryGetValue(word, out var w)) weight = w;
            else if (word.Equals("Italic", StringComparison.OrdinalIgnoreCase) || word.Equals("Oblique", StringComparison.OrdinalIgnoreCase)) italic = true;
            else return null;
        }

        var requested = weight ?? 400;
        if (faceWeight(family, requested, italic) is not int matched || Math.Abs(matched - requested) > 100) return null;

        var select = string.Join(" and ", new[]
        {
            weight is int v ? $"font-weight {v}" : null,
            italic ? "font-style italic" : null,
        }.OfType<string>());
        return $"'{name}' is a face of '{family}': use font-family \"'{family}'\" with {select}.";
    }

    private static bool IsInstalled(string family)
    {
        using var face = SKTypeface.FromFamilyName(family);
        return face is not null && string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase);
    }

    private static int? FaceWeight(string family, int weight, bool italic)
    {
        var style = new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        using var face = SKFontManager.Default.MatchFamily(family, style);
        return face?.FontStyle.Weight;
    }

    private static IEnumerable<string> InstalledFamilies()
    {
        using var manager = SKFontManager.CreateDefault();
        return [.. manager.FontFamilies];
    }
    #endregion

    #region Fields
    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
    {
        "serif", "sans-serif", "monospace", "cursive", "fantasy", "system-ui", "ui-serif", "ui-sans-serif",
        "ui-monospace", "ui-rounded", "math", "emoji", "fangsong", "inherit", "initial", "unset",
    };

    private static readonly Dictionary<string, int> Weights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Thin"] = 100, ["Hairline"] = 100, ["ExtraLight"] = 200, ["UltraLight"] = 200, ["Light"] = 300,
        ["Book"] = 400, ["Regular"] = 400, ["Normal"] = 400, ["Medium"] = 500, ["Demi"] = 600, ["DemiBold"] = 600,
        ["SemiBold"] = 600, ["Bold"] = 700, ["ExtraBold"] = 800, ["UltraBold"] = 800, ["Heavy"] = 900, ["Black"] = 900,
    };
    #endregion
}
