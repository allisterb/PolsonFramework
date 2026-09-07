namespace Polson.Drawing.Svg;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Polson.HtmlParser;

/// <summary>
/// Applies a CSS stylesheet to a paper, and keeps it in the document.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both halves are necessary and the second one is the reason this exists.</b> A
/// <c>&lt;style&gt;</c> block alone serialises correctly and renders as nothing: the library turns
/// rules into element styles only inside its parser, so a class-styled rect came back <b>black</b>
/// in the peek while looking right in the saved file. A call whose effect appears in the artifact
/// and not in the render is the worst shape available — the author verifies against the peek and
/// ships something else.
/// </para>
/// <para>
/// So the rules are resolved here and written onto the matching elements as ordinary attributes,
/// which render; and the <c>&lt;style&gt;</c> block is appended as well, so the deliverable still
/// carries a stylesheet a designer can edit in one place. The two agree because the same
/// declarations produce both.
/// </para>
/// <para>
/// The parsing is <c>Polson.HtmlParser</c>'s, not a second implementation. It reads <b>declared
/// rules rather than a computed cascade</b> — no inheritance, no specificity — which is exactly
/// right here: an SVG built by a script is flat, class-per-element, and that is the shape a design
/// language is written in anyway.
/// </para>
/// </remarks>
public static class SnapStylesheet
{
    #region Methods
    /// <summary>
    /// Resolves <paramref name="css"/> against the tree under <paramref name="root"/> and applies it.
    /// </summary>
    /// <returns>
    /// How many rule-to-element applications happened, so a caller can tell a typo from an empty
    /// sheet. <b>Not a count of distinct elements</b> — one element matched by two rules counts twice,
    /// which is what the loop below actually measures.
    /// </returns>
    public static int Apply(SnapElement root, string css)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrWhiteSpace(css)) return 0;

        var sheet = new CssToolkit().FromCss(css);
        var styled = 0;

        // Source order, so a later rule wins — the one part of the cascade that survives here.
        foreach (var rule in sheet.Rules())
        {
            if (rule.TryGetValue("selector", out var s) && s?.ToString() is not { Length: > 0 } selector) continue;
            var name = rule["selector"]!.ToString()!;
            if (rule.TryGetValue("properties", out var raw) && Declarations(raw) is { Count: > 0 } declared)
            {
                foreach (var element in Match(root, name))
                {
                    foreach (var (property, value) in declared)
                    {
                        SnapAttributes.ApplyAttribute(element.Node, property, value);
                    }
                    styled++;
                }
            }
        }

        return styled;
    }
    #endregion

    #region Private methods
    /// <summary>
    /// Elements matching one selector: <c>.class</c>, <c>#id</c>, a bare tag name, or a
    /// comma-separated list of those.
    /// </summary>
    /// <remarks>
    /// Deliberately the simple three rather than a selector engine. A descendant or attribute
    /// selector silently matching nothing would be worse than not offering it, so anything else is
    /// left to <c>.attr(...)</c> — and the three cover a flat, class-per-element sheet completely.
    /// </remarks>
    private static IEnumerable<SnapElement> Match(SnapElement root, string selector)
    {
        foreach (var part in selector.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // `:root` and `*` address the document itself, which is where a page's ground is set.
            if (part is "*" or ":root")
            {
                yield return root;
                continue;
            }

            if (part.Length < 2 && part is not ("*" or ":root")) continue;

            foreach (var found in root.SelectAll(part))
            {
                yield return found;
            }
        }
    }

    /// <summary>The declared properties of one rule, whatever collection shape they arrived in.</summary>
    private static Dictionary<string, string> Declarations(object? raw)
    {
        var declared = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (JsInterop.AsDict(raw) is not { } dict) return declared;

        foreach (DictionaryEntry entry in dict)
        {
            var key = entry.Key?.ToString();
            var value = entry.Value?.ToString();

            // A custom property is a token, not something to write onto an element — CssToolkit has
            // already substituted every var() before this point.
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;
            if (key!.StartsWith("--", StringComparison.Ordinal)) continue;

            declared[key] = value!;
        }

        return declared;
    }
    #endregion
}
