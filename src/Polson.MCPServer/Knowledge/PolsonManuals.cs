namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

/// <summary>A studio manual: design theory from the reference corpus, bound to the JS SDK methods that implement it.</summary>
public sealed record StudioManual(
    string Id,
    string Slug,
    string Title,
    string Purpose,
    string Source,
    string Body)
{
    #region Properties
    public string Uri => $"polson://manual/{Id}";

    /// <summary>SDK calls cited by this manual, in citation order (e.g. <c>Drawing.createPerspectiveGrid</c>).</summary>
    public IReadOnlyList<string> Apis => apis ??= PolsonManuals.CitedApis(Body);
    #endregion

    #region Fields
    private IReadOnlyList<string>? apis;
    #endregion
}

/// <summary>
/// Serves the studio manuals — the bridge between the reference books in <c>reference/</c> and the callable
/// JS SDK. Manuals carry the theory, the formulas, and the named API that implements each technique.
/// </summary>
public class PolsonManuals
{
    #region Constants
    private const string ResourcePrefix = "docs.manuals.";
    #endregion

    #region Properties
    /// <summary>All manuals, ordered by number.</summary>
    public static IReadOnlyList<StudioManual> All { get; } = Load();
    #endregion

    #region Methods
    /// <summary>Extracts SDK calls cited in markdown, e.g. <c>Drawing.createPerspectiveGrid(...)</c>.</summary>
    public static IReadOnlyList<string> CitedApis(string markdown)
    {
        var names = new List<string>();
        foreach (Match m in ApiCitation.Matches(markdown))
        {
            var name = $"{m.Groups[1].Value}.{m.Groups[2].Value}";
            if (!names.Contains(name, StringComparer.Ordinal)) names.Add(name);
        }
        return names;
    }

    public static StudioManual? Find(string idOrSlug) =>
        All.FirstOrDefault(m =>
            string.Equals(m.Id, idOrSlug, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Slug, idOrSlug, StringComparison.OrdinalIgnoreCase));

    /// <summary>The manual catalogue — what each manual covers and which SDK calls it binds to.</summary>
    public static string BuildIndex()
    {
        var known = KnownSdkCalls();
        var primitives = Canvas2DPrimitives();
        var index = new StringBuilder();

        index.AppendLine("# Polson Studio Manuals — Design Knowledge Index");
        index.AppendLine();
        index.AppendLine("""
            These manuals are the **bridge between the studio's reference library and the drawing SDK**. Each one
            distils a source text (Loomis, *How to Draw Comics the Marvel Way*, *Imaginative Drawing*, Bokhua's
            *Principles of Logo Design*, Tubik) into formulas, construction procedures, and the **named SDK calls
            that implement them**.

            Use them to decide *what* to draw and *why*; use `polson://sdk/*` for the exact call signatures.

            - `polson://manual/{NN}` — a manual in full.
            - `Search(query)` — ranked passages across all manuals and the SDK reference. **Prefer this** when you
              know the technique but not which manual covers it.

            Working method: `Search` the technique, read the manual passage for the construction rules and the
            numeric constants, then read `polson://sdk/core/{Area}` for exact parameters before writing the script.
            """);
        index.AppendLine();
        index.AppendLine("## Manuals");
        index.AppendLine();

        foreach (var manual in All)
        {
            index.AppendLine($"### {manual.Id} — {manual.Title}");
            index.AppendLine();
            if (manual.Purpose.Length > 0) index.AppendLine(manual.Purpose);
            if (manual.Source.Length > 0) index.AppendLine($"Source: {manual.Source}");
            index.AppendLine($"Read: `{manual.Uri}`");
            index.AppendLine();

            var topics = Topics(manual.Body);
            if (topics.Count > 0)
            {
                index.AppendLine($"- Topics: {string.Join("; ", topics)}");
            }

            var bound = manual.Apis.Where(a => known.Contains(a) && !primitives.Contains(a)).ToArray();
            var unbound = manual.Apis.Where(a => !known.Contains(a)).ToArray();

            if (bound.Length > 0)
            {
                index.AppendLine($"- Implemented by ({bound.Length}): {string.Join(", ", bound.Select(b => $"`{b}`"))}");
            }
            else if (manual.Apis.Any(primitives.Contains))
            {
                index.AppendLine("- Implemented by: — this manual hand-rolls the technique from raw Canvas2D primitives " +
                    "instead of calling a toolkit method. Check `polson://sdk/index` for an existing call before " +
                    "reimplementing what it teaches.");
            }
            else
            {
                index.AppendLine("- Implemented by: — (theory only; no SDK binding yet)");
            }

            if (unbound.Length > 0)
            {
                index.AppendLine($"- Cited but NOT in the SDK reference — do not call these: {string.Join(", ", unbound.Select(u => $"`{u}`"))}");
            }
            index.AppendLine();
        }

        return index.ToString();
    }

    /// <summary>One addressable MCP resource per manual, plus the catalogue.</summary>
    public static IEnumerable<McpServerResource> ManualResources()
    {
        foreach (var manual in All)
        {
            var captured = manual;
            yield return McpServerResource.Create(
                () => captured.Body,
                new McpServerResourceCreateOptions
                {
                    UriTemplate = captured.Uri,
                    Name = $"polson-manual-{captured.Id}-{captured.Slug.Replace('_', '-')}",
                    Title = $"Studio Manual {captured.Id} — {captured.Title}",
                    Description = Truncate(captured.Purpose.Length > 0 ? captured.Purpose : captured.Title, 320),
                    MimeType = "text/markdown"
                });
        }
    }

    [McpServerResource(UriTemplate = "polson://manual/index", Name = "polson-manual-index",
        Title = "Polson Studio Manuals — Design Knowledge Index", MimeType = "text/markdown")]
    [Description("START HERE for design theory. The catalogue of studio manuals — classical drawing, perspective, " +
        "lighting, anatomy, composition, logo geometry, and typography — distilled from the studio reference library " +
        "and cross-referenced to the SDK calls that implement each technique. Read a manual at polson://manual/{NN}, " +
        "or use the Search tool for ranked passages.")]
    public static string ManualIndex() => index ??= BuildIndex();

    /// <summary>
    /// Generic Canvas2D drawing primitives (<c>ctx.moveTo</c>, <c>ctx.fill</c>, …). Citing these is not evidence
    /// that a manual is bound to the toolkit — a hand-rolled reimplementation cites them heavily — so they are
    /// excluded from the catalogue's binding list. Toolkit methods surfaced on the context, such as
    /// <c>ctx.drawWordmarkLockup</c>, are documented under their own area and so are not treated as primitives.
    /// </summary>
    public static HashSet<string> Canvas2DPrimitives()
    {
        var slice = SdkDocs.Slice(PolsonResources.Docs.Core(), "Canvas2D");
        var calls = new HashSet<string>(StringComparer.Ordinal);
        if (slice is null) return calls;

        foreach (Match m in ApiCitation.Matches(slice))
        {
            calls.Add($"{m.Groups[1].Value}.{m.Groups[2].Value}");
        }
        return calls;
    }

    /// <summary>SDK calls documented in the core reference, as <c>Namespace.method</c>.</summary>
    public static HashSet<string> KnownSdkCalls()
    {
        var calls = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in ApiCitation.Matches(PolsonResources.Docs.Core()))
        {
            calls.Add($"{m.Groups[1].Value}.{m.Groups[2].Value}");
        }
        return calls;
    }

    private static IReadOnlyList<StudioManual> Load()
    {
        var assembly = typeof(PolsonManuals).Assembly;
        var manuals = new List<StudioManual>();

        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.Contains(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            var body = reader.ReadToEnd();

            var file = resource[(resource.IndexOf(ResourcePrefix, StringComparison.Ordinal) + ResourcePrefix.Length)..];
            if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) file = file[..^3];

            var id = Regex.Match(file, @"^(\d+)").Value;
            if (id.Length == 0) continue;

            manuals.Add(new StudioManual(
                Id: id,
                Slug: file[(id.Length + 1)..],
                Title: Heading(body, id),
                Purpose: Field(body, @"\*\*Purpose\*\*:\s*(.+)"),
                Source: Field(body, @"\*\*(?:Source Reference|Credits & Theoretical Foundation)\*\*:\s*(.+)"),
                Body: body));
        }

        return [.. manuals.OrderBy(m => m.Id, StringComparer.Ordinal)];
    }

    private static string Heading(string body, string id)
    {
        var line = body.Split('\n').FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?.Trim() ?? id;
        line = line[2..].Trim();
        var colon = line.IndexOf(':');
        return colon > 0 && line.StartsWith("Studio Manual", StringComparison.OrdinalIgnoreCase) ? line[(colon + 1)..].Trim() : line;
    }

    private static string Field(string body, string pattern)
    {
        var m = Regex.Match(body, pattern);
        return m.Success ? Clean(m.Groups[1].Value) : "";
    }

    /// <summary>Strips markdown emphasis without touching underscores, which occur in source filenames.</summary>
    private static string Clean(string value) =>
        Regex.Replace(value.Trim().TrimEnd('\r'), @"[*`]", "").Trim();

    private static List<string> Topics(string body) =>
        SdkDocs.Headings(body)
            .Where(h => h.Level == 2)
            .Select(h => Regex.Replace(h.Text, @"^\d+\.\s*", "").Trim())
            .Where(t => t.Length > 0)
            .Take(10)
            .ToList();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    /// <summary>
    /// Matches an SDK call written as <c>receiver.method(</c>, for both the manual corpus and the core
    /// reference — so the two are read by one rule and a citation can be checked against a definition.
    /// </summary>
    /// <remarks>
    /// The receiver list holds the <b>instance</b> receivers as well as the namespace ones. Without
    /// them a manual built on <c>bitmap.diff</c>, <c>element.attr</c> or <c>material.toDataUri</c>
    /// cited nothing the catalogue could see, and the index announced it as theory that "hand-rolls
    /// the technique from raw Canvas2D primitives" — which is the opposite of true, and appears at
    /// exactly the point where an agent is deciding whether the manual is worth opening.
    /// <para>
    /// Nested namespaces (<c>Skia.ColorFilter.colorMatrix</c>, <c>Snap.path.getPointAtLength</c>) are
    /// deliberately <i>not</i> matched. Allowing a second segment would also capture chains that are
    /// not symbols — <c>Assets.budget.canAfford</c> is real to call and is documented as
    /// <c>budget.canAfford</c>, so it would read as a citation of something the reference does not
    /// define. The receiver alone is enough to bind a manual to its area.
    /// </para>
    /// <para>
    /// <c>plate</c> is absent for the opposite reason: the reference spells a backdrop that way, but
    /// "plate" is also studio vocabulary for a construction board, and Manual 12 uses it as an
    /// ordinary variable — so including it turned <c>plate.getContext(...)</c> into a citation of a
    /// call that does not exist. A receiver name shared with common drawing vocabulary costs more
    /// than it earns; Manual 16 binds through <c>Assets.*</c> instead.
    /// </para>
    /// </remarks>
    private static readonly Regex ApiCitation = new(
        @"\b(Drawing|Logo|LogoType|VectorLogo|Snap|Skia|Layout|Scale|Css|Assets|paper|ctx|sheet" +
        @"|element|bitmap|matrix|gradient|material|matte|budget|imageData)\.([a-zA-Z][A-Za-z0-9]*)\s*\(",
        RegexOptions.Compiled);

    private static string? index;
    #endregion
}
