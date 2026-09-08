namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Polson.ExtendedMind.ParallelSearch;

/// <summary>
/// Checks the numbers drawn on a deliverable against the research they claim to rest on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The audit a run performs is written by the agent that drew the piece, and that is the whole
/// problem.</b> It knows what it meant, so it checks its intention. A live run recorded seventeen
/// passing checks — career span, average gap, runtime sum, both baselines, 518 labels — under a
/// headline reading <c>27 / 10</c> whose own cards summed to 27 and <b>9</b>. The footer said
/// <c>HONESTY AUDIT · ALL VALUES SOURCED</c> directly above it.
/// </para>
/// <para>
/// This is not that. It reads the saved markup and the archived research, neither of which the agent
/// authored at the moment of checking, and compares one against the other. Nothing it reports is a
/// matter of the agent's opinion.
/// </para>
/// <para>
/// <b>It runs here rather than in a script, and the reason is the reference's own.</b> An agent must
/// not read SVG markup into its context: measured on one page, the markup was 109,045 characters of a
/// 117,786-character result, and <c>result.svgXml</c> was removed over exactly that. So the file is
/// parsed on this side and what crosses the boundary is a verdict — counts, and the figures that did
/// not reconcile.
/// </para>
/// </remarks>
public static partial class FigureVerifier
{
    #region Methods
    /// <summary>
    /// Verifies every tagged figure in <paramref name="svg"/> against the archived research.
    /// </summary>
    /// <param name="svg">The saved markup.</param>
    /// <param name="archive">Where finished research runs are filed.</param>
    public static Verdict Verify(string svg, ResearchArchive archive)
    {
        var verdict = new Verdict();
        if (string.IsNullOrWhiteSpace(svg)) return verdict;

        XDocument document;
        try
        {
            document = XDocument.Parse(svg);
        }
        catch (Exception ex)
        {
            verdict.Findings.Add(new Finding("", "", null, "the markup could not be parsed: " + ex.Message));
            return verdict;
        }

        // Cached per run id: a page of thirty figures usually rests on one or two research runs, and
        // re-reading the same file thirty times would make the check cost more than the drawing.
        var loaded = new Dictionary<string, ArchivedResearch?>(StringComparer.Ordinal);

        foreach (var element in document.Descendants())
        {
            var tag = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "data-basis")?.Value;
            if (string.IsNullOrWhiteSpace(tag)) continue;

            verdict.Checked++;

            var drawn = Flatten(element);
            var split = tag.IndexOf(':');
            if (split <= 0 || split == tag.Length - 1)
            {
                verdict.Findings.Add(new Finding(tag, drawn, null,
                    "the tag is not 'runId:field.path', so nothing can be resolved from it"));
                continue;
            }

            var runId = tag[..split];
            var field = tag[(split + 1)..];

            if (!loaded.TryGetValue(runId, out var run))
            {
                loaded[runId] = run = archive.Load(runId);
            }

            if (run is null)
            {
                verdict.Findings.Add(new Finding(tag, drawn, null,
                    "research run " + runId + " is not archived, so this figure cannot be checked"));
                continue;
            }

            if (Resolve(run.Result, field) is not { } expected)
            {
                verdict.Findings.Add(new Finding(tag, drawn, null,
                    "the archived result has no field '" + field + "'"));
                continue;
            }

            if (Agrees(drawn, expected))
            {
                verdict.Verified++;
                continue;
            }

            verdict.Findings.Add(new Finding(tag, drawn, expected, "the drawn value is not what the research returned"));
        }

        return verdict;
    }

    /// <summary>
    /// The text a reader sees for one element, including any spans inside it.
    /// </summary>
    /// <remarks>
    /// Tracked type arrives as one positioned element per glyph inside a group, and a wrapped
    /// paragraph as one span per line, so the value is often not on the tagged node itself. Taking
    /// the whole subtree is what lets a figure be tagged where it is composed rather than only where
    /// a single string happens to sit.
    /// </remarks>
    internal static string Flatten(XElement element) =>
        Whitespace().Replace(string.Concat(element.DescendantNodes().OfType<XText>().Select(t => t.Value)), " ").Trim();

    /// <summary>
    /// Whether the drawn text says the same thing as the archived value.
    /// </summary>
    /// <remarks>
    /// <b>Presentation is the caller's business and must not be mistaken for disagreement.</b> A
    /// figure drawn as <c>$171.9M</c> against an archived <c>171.9</c> is correct, and a check that
    /// called it a mismatch would be switched off within one run. So a number is compared as a
    /// number once currency, grouping and a trailing unit are removed, and anything that is not a
    /// number is compared as trimmed, case-insensitive text.
    /// <para>
    /// The tolerance is relative, because a piece legitimately rounds: 1,636 minutes may be drawn as
    /// <c>27h 16m</c> elsewhere, but where it is drawn as a number it may be shown to fewer decimals
    /// than the source carries. Half a percent absorbs rounding without absorbing a wrong figure —
    /// the 27/10 case is out by a whole unit in ten.
    /// </para>
    /// </remarks>
    internal static bool Agrees(string drawn, string expected)
    {
        if (string.Equals(drawn.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase)) return true;

        if (!TryNumber(drawn, out var a) || !TryNumber(expected, out var b)) return false;
        if (a == b) return true;

        var scale = Math.Max(Math.Abs(a), Math.Abs(b));
        return scale > 0 && Math.Abs(a - b) / scale <= 0.005d;
    }

    /// <summary>The number in a drawn string, ignoring currency, grouping and a trailing unit.</summary>
    internal static bool TryNumber(string text, out double value)
    {
        value = 0d;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = Numeric().Match(text.Replace(",", "", StringComparison.Ordinal));
        return match.Success
            && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// One field of an archived result, addressed as <c>films.3.runtime</c>, or null.
    /// </summary>
    /// <remarks>
    /// Dot-indexed for arrays, which is how the service itself addresses a list element in
    /// <c>basis.field</c> — so a tag can name the same path the citation does, and the two cannot
    /// drift into separate conventions.
    /// </remarks>
    internal static string? Resolve(object? result, string field)
    {
        if (result is null || string.IsNullOrWhiteSpace(field)) return null;

        JsonElement current;
        try
        {
            current = result is JsonElement already
                ? already
                : JsonSerializer.SerializeToElement(result);
        }
        catch (Exception)
        {
            return null;
        }

        foreach (var step in field.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(step, out var index))
            {
                if (index < 0 || index >= current.GetArrayLength()) return null;
                current = current[index];
                continue;
            }

            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(step, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => current.GetRawText(),
        };
    }
    #endregion

    #region Fields
    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex Numeric();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    #endregion

    #region Child types
    /// <summary>What the check found.</summary>
    public sealed class Verdict
    {
        /// <summary>How many tagged figures were examined.</summary>
        public int Checked { get; set; }

        /// <summary>How many matched the research they name.</summary>
        public int Verified { get; set; }

        /// <summary>Every figure that did not reconcile, and why.</summary>
        public List<Finding> Findings { get; } = [];
    }

    /// <summary>One figure that did not reconcile.</summary>
    public sealed record Finding(string Basis, string Drawn, string? Expected, string Reason);
    #endregion
}
