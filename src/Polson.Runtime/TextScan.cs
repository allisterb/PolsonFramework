namespace Polson;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>A class of codepoint used to hide text from a reader while leaving it in the bytes.</summary>
public sealed record HiddenTextFinding(string ClassName, int Codepoint, int Count, int FirstIndex)
{
    /// <summary>The codepoint as <c>U+202E</c>.</summary>
    public string Notation => $"U+{Codepoint:X4}";
}

/// <summary>Text that reads as an instruction to whoever is processing it.</summary>
public sealed record PhraseFinding(string Kind, string Match, int Index);

/// <summary>What a scan found.</summary>
public sealed record TextScanReport
{
    #region Properties
    public string Label { get; init; } = string.Empty;

    public int Chars { get; init; }

    public int Bytes { get; init; }

    /// <summary>False when the bytes were not well-formed UTF-8, which is itself worth knowing.</summary>
    public bool WellFormedUtf8 { get; init; } = true;

    /// <summary>True when the content held a NUL and was treated as binary rather than read.</summary>
    public bool Binary { get; init; }

    /// <summary>Every non-ASCII codepoint and how often it occurred, most frequent first.</summary>
    public IReadOnlyList<(int Codepoint, int Count)> Census { get; init; } = [];

    /// <summary>Codepoints from the concealment classes. Empty is the expected result.</summary>
    public IReadOnlyList<HiddenTextFinding> Hidden { get; init; } = [];

    /// <summary>Text addressed to the reader rather than stating a fact.</summary>
    public IReadOnlyList<PhraseFinding> Phrases { get; init; } = [];

    /// <summary>Nothing suspicious. Ordinary typography and foreign script are not suspicious.</summary>
    public bool Clean => Hidden.Count == 0 && Phrases.Count == 0;
    #endregion

    #region Methods
    /// <summary>The scan as the Perl script prints it, for a person or a run record to read.</summary>
    public string Summary(int censusLimit = 40)
    {
        var sb = new StringBuilder();
        sb.Append(Binary
            ? $"{Label}: binary (contains NUL) — not read as text.\n"
            : $"{Label}: {Chars} chars, {Bytes} bytes.\n");

        if (Binary) return sb.ToString();
        if (!WellFormedUtf8) sb.Append("!! not well-formed UTF-8\n");

        sb.Append("\n-- non-ASCII census (codepoint, count, char) --\n");
        if (Census.Count == 0) sb.Append("  none — pure ASCII\n");
        foreach (var (codepoint, count) in Census.Take(censusLimit))
        {
            var glyph = codepoint is >= 0x20 and not 0x7F ? char.ConvertFromUtf32(codepoint) : ".";
            sb.Append($"  U+{codepoint:X4}  {count,7}  {glyph}\n");
        }

        if (Census.Count > censusLimit) sb.Append($"  … {Census.Count - censusLimit} more\n");

        sb.Append("\n-- suspicious codepoint classes --\n");
        if (Hidden.Count == 0) sb.Append("  none\n");
        foreach (var group in Hidden.GroupBy(h => h.ClassName))
        {
            sb.Append($"  {group.Key,-24} {group.Sum(h => h.Count)} occurrence(s)\n");
            foreach (var finding in group.Take(5))
            {
                sb.Append($"      {finding.Notation} (x{finding.Count}) at {finding.FirstIndex}\n");
            }
        }

        sb.Append("\n-- injection phrasing --\n");
        if (Phrases.Count == 0) sb.Append("  none\n");
        foreach (var phrase in Phrases.Take(20))
        {
            sb.Append($"  [{phrase.Kind}] at {phrase.Index}: {Clip(phrase.Match)}\n");
        }

        return sb.ToString();
    }

    private static string Clip(string s) => s.Length <= 120 ? s : string.Concat(s.AsSpan(0, 120), "…");
    #endregion
}

/// <summary>
/// Codepoint-level inspection of text this studio did not write: reference material, a client's
/// brief, and anything a remote service returns.
/// </summary>
/// <remarks>
/// <para>
/// A faithful port of <c>reference/scan-codepoints.pl</c>, and now the single implementation — the
/// brief sanitiser and the reference-ledger scan were two separate copies of the same codepoint list,
/// which is one copy too many for a rule the guardrails depend on.
/// </para>
/// <para>
/// <b>It enumerates runes, not chars, and that is load-bearing.</b> The Unicode Tag block lives at
/// U+E0000 and reaches C# as a surrogate <i>pair</i>; a <c>foreach (char c in text)</c> sees two code
/// units in the D800–DFFF range and matches none of the classes below, so the one technique designed
/// specifically to smuggle instructions past a reader would pass a scan silently.
/// </para>
/// <para>
/// <b>What it can and cannot do.</b> It finds concealment — characters that are in the bytes and not
/// on the screen — and phrasing that reads as an instruction. It cannot tell you whether ordinary
/// visible prose is hostile, because that is a judgment about meaning. A clean scan means "nothing is
/// hidden here", never "this text is safe to obey": text from outside is data whatever this returns.
/// </para>
/// </remarks>
public static partial class TextScan
{
    #region Methods
    /// <summary>Scans decoded text. <paramref name="label"/> names it in the report.</summary>
    public static TextScanReport Scan(string? text, string? label = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TextScanReport { Label = label ?? "text", Chars = 0, Bytes = 0 };
        }

        var census = new Dictionary<int, int>();
        var firstIndex = new Dictionary<int, int>();
        var hiddenCounts = new Dictionary<int, (string ClassName, int Count)>();

        var index = 0;
        var isFirstRune = true;

        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            var at = index;
            index += rune.Utf16SequenceLength;

            // Ordinary PRINTABLE ASCII and the three whitespace characters are not evidence of
            // anything. Note the bound: printable, not "below 0x80". The Perl script this ports
            // skipped everything under 0x80 except DEL, which made its own C0-control class
            // unreachable — a file carrying U+0001 scanned clean. Verified against the script and
            // fixed in both.
            if (value is >= 0x20 and < 0x7F) { isFirstRune = false; continue; }
            if (value is 0x0A or 0x0D or 0x09) { isFirstRune = false; continue; }

            census[value] = census.GetValueOrDefault(value) + 1;
            firstIndex.TryAdd(value, at);

            // A leading BOM is how half the world's editors save a file. Anywhere else it is hiding.
            var leadingBom = isFirstRune && value == 0xFEFF;
            isFirstRune = false;
            if (leadingBom) continue;

            if (ClassOf(value) is string className)
            {
                var existing = hiddenCounts.GetValueOrDefault(value);
                hiddenCounts[value] = (className, existing.Count + 1);
            }
        }

        return new TextScanReport
        {
            Label = label ?? "text",
            Chars = text.Length,
            Bytes = Encoding.UTF8.GetByteCount(text),
            Census = [.. census.OrderByDescending(p => p.Value).ThenBy(p => p.Key)
                               .Select(p => (p.Key, p.Value))],
            Hidden = [.. hiddenCounts.OrderBy(p => p.Key)
                                     .Select(p => new HiddenTextFinding(
                                         p.Value.ClassName, p.Key, p.Value.Count, firstIndex[p.Key]))],
            Phrases = FindPhrases(text),
        };
    }

    /// <summary>
    /// Scans raw bytes, deciding for itself whether they are text. Content holding a NUL is reported
    /// as binary and not read — the same rule the Perl script applies, and for the same reason: a
    /// codepoint census of a JPEG says nothing about anything.
    /// </summary>
    public static TextScanReport ScanBytes(ReadOnlySpan<byte> bytes, string? label = null)
    {
        if (bytes.IndexOf((byte)0) >= 0)
        {
            return new TextScanReport { Label = label ?? "bytes", Bytes = bytes.Length, Binary = true };
        }

        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        bool wellFormed;
        string text;
        try
        {
            text = strict.GetString(bytes);
            wellFormed = true;
        }
        catch (DecoderFallbackException)
        {
            // Decode anyway, replacing what will not decode: a file that is not valid UTF-8 is a fact
            // worth reporting, not a reason to refuse to look at it.
            text = Encoding.UTF8.GetString(bytes);
            wellFormed = false;
        }

        return Scan(text, label) with { Bytes = bytes.Length, WellFormedUtf8 = wellFormed };
    }

    /// <summary>Scans one file. Never writes, and never re-encodes what it reads.</summary>
    public static TextScanReport ScanFile(string path) =>
        ScanBytes(System.IO.File.ReadAllBytes(path), path);

    /// <summary>
    /// Removes every concealment class, keeping the visible text. Newlines are normalised to
    /// <c>\n</c>; tab and newline survive, and nothing else below U+0020 does.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="Scan"/>: scanning tells you what is there, this makes it safe to
    /// pass on. Applied to a client's brief and to anything a remote service returns, so the text an
    /// agent reads is the text a person would see.
    /// </remarks>
    public static string Sanitize(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var normalised = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        var sb = new StringBuilder(normalised.Length);

        foreach (var rune in normalised.EnumerateRunes())
        {
            var value = rune.Value;
            if (value is '\n' or '\t') { sb.Append((char)value); continue; }
            if (value < 0x20 || value is >= 0x7F and <= 0x9F) continue;
            if (ClassOf(value) is not null) continue;
            sb.Append(rune);
        }

        return sb.ToString();
    }

    /// <summary>
    /// The concealment class a codepoint belongs to, or null when it is ordinary.
    /// </summary>
    /// <remarks>
    /// Private-use area is included because it renders as whatever a font decides, so it can carry
    /// content a reader will not see as text. It is also the class most likely to fire on something
    /// innocent — icon fonts live there — which is why a finding is reported rather than fatal.
    /// </remarks>
    public static string? ClassOf(int codepoint) => codepoint switch
    {
        >= 0x202A and <= 0x202E or >= 0x2066 and <= 0x2069 => "bidi override / isolate",
        >= 0x200B and <= 0x200F or 0x2060 or 0xFEFF => "zero-width / joiner",
        0x00AD => "soft hyphen",
        >= 0xE0000 and <= 0xE007F => "Unicode Tag block",
        >= 0xE000 and <= 0xF8FF or >= 0xF0000 and <= 0x10FFFD => "private use area",
        < 0x09 or (>= 0x0E and < 0x20) or (>= 0x7F and < 0xA0) => "C0/C1 control",
        _ => null,
    };
    #endregion

    #region Methods (private)
    private static IReadOnlyList<PhraseFinding> FindPhrases(string text)
    {
        List<PhraseFinding>? found = null;

        foreach (var (kind, pattern) in Patterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                (found ??= []).Add(new PhraseFinding(kind, match.Value, match.Index));
            }
        }

        return found is null ? [] : found.OrderBy(p => p.Index).ToArray();
    }

    private static (string Kind, Regex Pattern)[] Patterns =>
    [
        ("override", IgnorePrevious()),
        ("override", DisregardAbove()),
        ("persona", YouAreNow()),
        ("system", SystemPrompt()),
        ("chat markup", ChatTemplateToken()),
        ("chat markup", InstToken()),
        ("system", SectionHeading()),
        ("directive", AgentMust()),
    ];

    [GeneratedRegex(@"ignore\s+(all\s+)?previous\s+instructions", RegexOptions.IgnoreCase)]
    private static partial Regex IgnorePrevious();

    [GeneratedRegex(@"disregard\s+(the\s+)?above", RegexOptions.IgnoreCase)]
    private static partial Regex DisregardAbove();

    [GeneratedRegex(@"you\s+are\s+now\s+", RegexOptions.IgnoreCase)]
    private static partial Regex YouAreNow();

    [GeneratedRegex(@"system\s*prompt", RegexOptions.IgnoreCase)]
    private static partial Regex SystemPrompt();

    [GeneratedRegex(@"<\|[a-z_]+\|>", RegexOptions.IgnoreCase)]
    private static partial Regex ChatTemplateToken();

    [GeneratedRegex(@"\[INST\]|\[/INST\]", RegexOptions.IgnoreCase)]
    private static partial Regex InstToken();

    [GeneratedRegex(@"###\s*(instruction|system)", RegexOptions.IgnoreCase)]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"\bAI\s+(assistant|agent)\b.{0,40}\b(must|should|shall)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AgentMust();
    #endregion
}
