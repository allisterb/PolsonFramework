namespace Polson.HtmlParser;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;

/// <summary>
/// Reads a stylesheet as a <b>design language</b> — its tokens and its text styling, not its layout.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// This answers "what does <c>.h1</c> look like" — family, size, weight, colour, tracking — in a
/// form the drawing context can use directly. It deliberately does not answer "where does it go":
/// there is no box model here. Geometry stays with <c>Layout</c> and
/// <c>ctx.measureWrappedText(...)</c>, which measure real glyphs.
/// </para>
/// <para>
/// <b>It reads declared rules, not a computed cascade.</b> That is a deliberate narrowing, and the
/// reason is measurable: AngleSharp.Css 1.0.2's computed style <i>throws</i> on
/// <c>font: … 46px/1.1 …</c> and on <c>%</c>, <c>vw</c> and <c>calc()</c>, silently drops any
/// shorthand containing <c>var()</c>, and resolves <c>em</c> tracking against 16px rather than the
/// element's own size. At the declared-rule level none of that happens — the shorthand expands
/// correctly, slash form included, and <c>em</c> survives as written for
/// <c>ctx.letterSpacing</c> to resolve properly. So custom properties are substituted in the source
/// text first and the flattened CSS is parsed once; every <c>var()</c> is gone before AngleSharp
/// sees it.
/// </para>
/// <para>
/// The cost of that trade is inheritance and specificity: rules are read as written, and only rules
/// sharing an identical selector are merged (later wins, as the cascade would). For the flat,
/// class-per-element stylesheets a design language is actually written in, that is the whole of it.
/// For a page that leans on inheritance, it is not — and this is the wrong tool for that page.
/// </para>
/// <para>
/// <b>No resource loader is configured, deliberately.</b> Without one AngleSharp never issues a
/// request, so a <c>&lt;link rel="stylesheet"&gt;</c>, an <c>@import</c> or an <c>&lt;img src&gt;</c>
/// in visitor-supplied markup is inert rather than an outbound fetch from inside the sandbox. Only
/// the CSS present in the string itself is read.
/// </para>
/// </remarks>
public class CssToolkit
{
    #region Methods
    /// <summary>Reads an HTML document, using the CSS in its <c>&lt;style&gt;</c> blocks.</summary>
    public StyleSheetView Parse(string html) => StyleSheetView.FromHtml(html ?? string.Empty);

    /// <summary>Reads a bare stylesheet, with no document around it.</summary>
    public StyleSheetView FromCss(string css) => StyleSheetView.FromCss(css ?? string.Empty);
    #endregion
}

/// <summary>The rules and tokens of one stylesheet.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class StyleSheetView
{
    #region Constructors
    private StyleSheetView(Dictionary<string, string> tokens, List<(string Selector, ICssStyleRule Rule)> rules)
    {
        this.tokens = tokens;
        this.rules = rules;
    }
    #endregion

    #region Methods
    internal static StyleSheetView FromCss(string css) =>
        FromHtml($"<!doctype html><html><head><style>{css}</style></head><body></body></html>");

    internal static StyleSheetView FromHtml(string html)
    {
        var tokens = ReadTokens(html);
        var flattened = Substitute(html, tokens);

        // No loader: nothing in the markup can cause a network request. See CssToolkit's remarks.
        var context = BrowsingContext.New(Configuration.Default.WithCss());
        var document = context.OpenAsync(req => req.Content(flattened)).GetAwaiter().GetResult();

        var rules = document.StyleSheets.OfType<ICssStyleSheet>()
            .SelectMany(sheet => sheet.Rules.OfType<ICssStyleRule>())
            .Select(rule => (Selector: (rule.SelectorText ?? string.Empty).Trim(), Rule: rule))
            .Where(entry => entry.Selector.Length > 0)
            .ToList();

        return new StyleSheetView(tokens, rules);
    }

    /// <summary>
    /// The CSS custom properties the sheet declares — a design language's tokens.
    /// </summary>
    /// <remarks>
    /// Where a stylesheet keeps the things worth taking: the palette, the type scale, the canvas
    /// size. Values are returned already resolved, so a token defined in terms of another token
    /// (<c>--lead: var(--ink)</c>) comes back as the colour rather than as the reference.
    /// </remarks>
    public Dictionary<string, object> Tokens()
    {
        var resolved = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, value) in tokens) resolved[name] = value;
        return resolved;
    }

    /// <summary>Every selector the sheet defines a rule for, in source order, without duplicates.</summary>
    public string[] Selectors() => [.. rules.Select(r => r.Selector).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The declared style for <paramref name="selector"/>, or <c>null</c> if the sheet has no such rule.
    /// </summary>
    /// <remarks>
    /// The selector must match as written — this is a lookup of a rule, not a query of the cascade.
    /// Rules repeating the same selector are merged in source order, later declarations winning.
    /// </remarks>
    public Dictionary<string, object>? Rule(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;

        var wanted = selector.Trim();
        var matching = rules.Where(r => string.Equals(r.Selector, wanted, StringComparison.Ordinal)).ToArray();
        if (matching.Length == 0) return null;

        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, rule) in matching)
        {
            foreach (var property in rule.Style)
            {
                if (string.IsNullOrWhiteSpace(property.Value)) continue;
                if (property.Name.StartsWith("--", StringComparison.Ordinal)) continue;
                declared[property.Name] = property.Value.Trim();
            }
        }

        return Describe(wanted, declared);
    }

    /// <summary>Every rule in the sheet, as declared styles in source order.</summary>
    public Dictionary<string, object>[] Rules() =>
        [.. Selectors().Select(Rule).Where(r => r is not null).Select(r => r!)];
    #endregion

    #region Methods (private)
    /// <summary>
    /// Reads custom properties out of the source text, resolving references between them.
    /// </summary>
    /// <remarks>
    /// Read from the text rather than from the parsed sheet because a custom property has no value
    /// of its own until something references it, and because this has to run <i>before</i> parsing —
    /// the substitution it feeds is what lets the shorthand expand at all.
    /// </remarks>
    private static Dictionary<string, string> ReadTokens(string source)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in CustomProperty.Matches(source))
        {
            tokens[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }

        // A token may be defined in terms of another; settle them against each other first.
        foreach (var name in tokens.Keys.ToArray())
        {
            tokens[name] = Substitute(tokens[name], tokens);
        }
        return tokens;
    }

    /// <summary>
    /// Replaces every <c>var(--name)</c> with its value, honouring the fallback argument.
    /// </summary>
    /// <remarks>
    /// Bounded rather than recursive: a stylesheet can define tokens that reference each other in a
    /// cycle, and a fixed pass limit turns that into a leftover <c>var()</c> the caller can see
    /// instead of a hang. An unknown token with no fallback resolves to nothing, which is what CSS
    /// does with it.
    /// </remarks>
    private static string Substitute(string source, IDictionary<string, string> tokens)
    {
        for (var pass = 0; pass < SubstitutionPasses; pass++)
        {
            var next = VarReference.Replace(source, match =>
                tokens.TryGetValue(match.Groups[1].Value, out var value) ? value
                : match.Groups[2].Success ? match.Groups[2].Value.Trim()
                : string.Empty);

            if (string.Equals(next, source, StringComparison.Ordinal)) break;
            source = next;
        }
        return source;
    }

    private static Dictionary<string, object> Describe(string selector, Dictionary<string, string> declared)
    {
        var family = Value(declared, "font-family");
        var size = Pixels(Value(declared, "font-size"));
        var weight = Value(declared, "font-weight");
        var slant = Value(declared, "font-style");

        var described = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            // Ready to assign straight onto the drawing context.
            ["font"] = CanvasFont(slant, weight, size, family),
            ["fontFamily"] = family,
            ["fontSize"] = size,
            ["fontWeight"] = weight,
            ["fontStyle"] = slant,
            ["color"] = Value(declared, "color"),
            ["backgroundColor"] = Value(declared, "background-color"),
            // Left in the unit it was written in: ctx.letterSpacing resolves em against the font
            // size actually in force, which is the resolution a stylesheet means.
            ["letterSpacing"] = Tracking(Value(declared, "letter-spacing")),
            ["lineHeight"] = LineHeight(Value(declared, "line-height"), size),
            ["textAlign"] = Value(declared, "text-align"),
            ["textTransform"] = Value(declared, "text-transform"),
            ["opacity"] = Opacity(Value(declared, "opacity")),
            ["selector"] = selector
        };

        var all = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, value) in declared) all[name] = value;
        described["properties"] = all;

        return described;
    }

    /// <summary>Assembles the CSS font shorthand that <c>ctx.font</c> parses.</summary>
    private static string CanvasFont(string slant, string weight, float size, string family)
    {
        var parts = new List<string>(4);
        if (slant is "italic" or "oblique") parts.Add(slant);
        if (weight.Length > 0 && weight != "normal" && weight != "400") parts.Add(weight);
        parts.Add((size > 0f ? size : DefaultFontSize).ToString("0.##", CultureInfo.InvariantCulture) + "px");
        parts.Add(family.Length > 0 ? family : "sans-serif");
        return string.Join(' ', parts);
    }

    private static string Value(Dictionary<string, string> declared, string name) =>
        declared.TryGetValue(name, out var value) ? value : string.Empty;

    private static float Pixels(string value)
    {
        if (value.Length == 0 || value.Equals("normal", StringComparison.OrdinalIgnoreCase)) return 0f;
        var text = value.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? value[..^2] : value;

        return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0f;
    }

    /// <summary>
    /// Line height in pixels, resolving the unitless form against the rule's own font size.
    /// </summary>
    /// <remarks>
    /// <c>line-height: 1.5</c> is a <i>ratio</i>, not a length — the commonest way a stylesheet
    /// states it, and a number that means 1.5px if taken literally. Returns zero when there is
    /// nothing to resolve against, which the drawing calls read as "use the font's own leading".
    /// </remarks>
    private static float LineHeight(string value, float fontSize)
    {
        if (value.Length == 0 || value.Equals("normal", StringComparison.OrdinalIgnoreCase)) return 0f;
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) return Pixels(value);

        if (float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio))
        {
            return fontSize > 0f ? ratio * fontSize : 0f;
        }
        return 0f;
    }

    /// <summary>Tracking in the form <c>ctx.letterSpacing</c> takes; <c>normal</c> becomes <c>0px</c>.</summary>
    private static string Tracking(string value) =>
        value.Length == 0 || value.Equals("normal", StringComparison.OrdinalIgnoreCase) ? "0px" : value;

    private static float Opacity(string value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 1f;
    #endregion

    #region Fields
    private readonly Dictionary<string, string> tokens;
    private readonly List<(string Selector, ICssStyleRule Rule)> rules;

    private const int SubstitutionPasses = 8;
    private const float DefaultFontSize = 16f;

    private static readonly Regex CustomProperty =
        new(@"(--[\w-]+)\s*:\s*([^;}]+)", RegexOptions.Compiled);

    private static readonly Regex VarReference =
        new(@"var\(\s*(--[\w-]+)\s*(?:,\s*([^()]*))?\)", RegexOptions.Compiled);
    #endregion
}
