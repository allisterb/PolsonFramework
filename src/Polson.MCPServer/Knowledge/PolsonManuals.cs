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
            distils a source text — Loomis for construction and light, Janson for inking and for staging a
            page, Glebas for depth and screen continuity, Lee & Buscema for the comic idiom, Stanchfield
            for gesture, Hampton, Faragasso and Norling, Bokhua and Tubik for logo work — into formulas, construction
            procedures, and the **named SDK calls that implement them**. Every manual names its own
            sources in its header; that list is the authority, and this sentence is only a summary of it.

            Not every manual rests on a book. Where the studio's own practice is the source — the run
            record, measuring a render, scoring motion — the manual says so in its header instead of
            borrowing a citation, because an unearned one is the same defect as an unearned
            measurement.

            *How to Draw Comics the Marvel Way* is worth stating precisely, because this paragraph has
            twice said something false about it. It was listed here as a source while **no** manual
            cited it; that was corrected on 2026-09-04 by removing it. **Manuals 23 and 24 were then
            written from its ch. 8 and ch. 6**, so it is a source again, and the removal is now the
            stale claim — corrected 2026-09-05. Three chapters have been read in all; the third,
            ch. 12 on inking, was supplanted by `janson-inking` and is cited by nothing.

            Use them to decide *what* to draw and *why*; use `polson://sdk/*` for the exact call signatures.

            **They are not prescriptive, and where they carry two schools that is deliberate.** Drawing has more
            than one tradition for most things — Loomis and Reilly construct a figure differently, a fixed
            armature and a generative subdivision compose a frame differently — and a manual that carried only
            one would be hiding a choice from you rather than making one for you. Where the sources disagree,
            each is named with what it is for and which calls implement it. **Choose per drawing, and mixing
            schools is normal**: proportions from one and construction from another is what working artists do.
            A canon is a starting position, not a specification.

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

    /// <summary>
    /// Prepended to every manual as it is served, so the scope travels with the thing it scopes.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately here rather than only in <see cref="BuildIndex"/>.</b> The index already
    /// carried a "not prescriptive" paragraph, and an agent that reads <c>polson://manual/06</c>
    /// directly never sees it — the same defect that let the index claim for two days that no manual
    /// cited *How to Draw Comics the Marvel Way* while two did. A statement kept in a second place
    /// from the thing it describes is read by nobody and drifts unnoticed; attached to the body, it
    /// cannot be missed and cannot go stale separately.
    /// <para>
    /// Injected at <b>serve</b> time only. <c>ManualExampleTests</c> and <c>ManualCoverageTests</c>
    /// read <see cref="StudioManual.Body"/>, and <see cref="KnowledgeCorpus"/> chunks it for search —
    /// none of those should see it, and twenty-five identical preamble chunks would be search noise.
    /// </para>
    /// </remarks>
    internal const string ScopeNote = """
        > [!NOTE]
        > **What a studio manual is.** It teaches a craft *once you have chosen it*. It does not decide
        > that the craft is the right one for this job — that judgment is yours and sits upstream of
        > every manual here. A manual that lists forms is listing the forms of its own craft, not the
        > options available to the piece.
        >
        > **These are recommendations, and where a craft has two traditions the manual carries both**
        > rather than hiding the choice — Loomis and Reilly construct a figure differently and neither
        > is wrong. Where a source scoped its own claim, the manual keeps that scope and says so.
        >
        > **One exception, and it is narrow.** Manual 13 §2 — the integrity of a quantitative encoding —
        > is about *truth* rather than taste: a bar whose length misstates its value is an error, not a
        > style, and that section is firm on purpose. Everything else you may argue with.
        >
        > **The SDK calls named in a manual are what implement its technique, not a requirement to use
        > them.** Any of it can be drawn by hand from the primitives.

        ---

        """;

    /// <summary>A manual as an agent receives it: the scope note, then the manual.</summary>
    /// <remarks>
    /// A named method rather than an expression inside the resource lambda, so a test can assert that
    /// the note is actually served. Composed here and nowhere else.
    /// </remarks>
    public static string ServedBody(StudioManual manual) =>
        ScopeNote + (manual ?? throw new ArgumentNullException(nameof(manual))).Body;

    /// <summary>One addressable MCP resource per manual, plus the catalogue.</summary>
    public static IEnumerable<McpServerResource> ManualResources()
    {
        foreach (var manual in All)
        {
            var captured = manual;
            yield return McpServerResource.Create(
                () => PolsonResources.Served(captured.Uri, ServedBody(captured)),
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
    public static string ManualIndex() =>
        PolsonResources.Served("polson://manual/index", index ??= BuildIndex());

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
