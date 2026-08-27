namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Which body of knowledge a search should draw from.</summary>
public enum KnowledgeScope
{
    /// <summary>Studio manuals and the SDK reference.</summary>
    All,
    /// <summary>Design theory only — the studio manuals.</summary>
    Manual,
    /// <summary>API reference only — the SDK core and schema documents.</summary>
    Sdk
}

/// <summary>A retrievable passage: one markdown section of a manual or of the SDK reference.</summary>
public sealed record KnowledgeChunk(
    string Uri,
    KnowledgeScope Scope,
    string Title,
    string Section,
    string Text,
    IReadOnlyList<string> Apis);

/// <summary>A ranked passage returned by <see cref="IKnowledgeIndex"/>.</summary>
public sealed record KnowledgeHit(
    string Uri,
    string Title,
    string Section,
    string Source,
    double Score,
    IReadOnlyList<string> Apis,
    string Text);

/// <summary>
/// Retrieval over the studio's design knowledge. Implemented locally today (<see cref="LocalKnowledgeIndex"/>);
/// the same contract is what a Vertex AI RAG backend fills once user-supplied reference works are ingested,
/// so agent prompts and the <c>Search</c> tool signature do not change when the backend is swapped.
/// </summary>
public interface IKnowledgeIndex
{
    #region Properties
    /// <summary>Backend identifier, surfaced in results so an agent knows what answered it.</summary>
    string Name { get; }
    #endregion

    #region Methods
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(string query, int k, KnowledgeScope scope, CancellationToken ct = default);
    #endregion
}

/// <summary>Chunks the manuals and the SDK reference into retrievable markdown sections.</summary>
public static class KnowledgeCorpus
{
    #region Constants
    private const int MaxChunkChars = 2400;
    private const int MinChunkChars = 120;
    #endregion

    #region Properties
    public static IReadOnlyList<KnowledgeChunk> Chunks { get; } = Build();
    #endregion

    #region Methods
    public static IReadOnlyList<KnowledgeChunk> Build()
    {
        var chunks = new List<KnowledgeChunk>();

        foreach (var manual in PolsonManuals.All)
        {
            chunks.AddRange(Sections(manual.Body, manual.Uri, KnowledgeScope.Manual, $"Manual {manual.Id} — {manual.Title}"));
        }

        foreach (var area in PolsonResources.Docs.Addressable)
        {
            AddSdk(chunks, PolsonResources.Docs.Core(), area, "core");
            AddSdk(chunks, PolsonResources.Docs.Schema(), area, "schema");
        }

        return chunks;
    }

    private static void AddSdk(List<KnowledgeChunk> chunks, string doc, SdkArea area, string kind)
    {
        var slice = SdkDocs.Slice(doc, area.HeadingText);
        if (slice is null) return;
        chunks.AddRange(Sections(slice, $"polson://sdk/{kind}/{area.Name}", KnowledgeScope.Sdk, $"SDK {area.Name} ({kind})"));
    }

    /// <summary>
    /// Splits markdown into sections at heading level 2, descending to level 3 only when a level-2 section
    /// is too large to return whole.
    /// </summary>
    private static IEnumerable<KnowledgeChunk> Sections(string doc, string uri, KnowledgeScope scope, string title)
    {
        var lines = doc.Split('\n');
        var headings = SdkDocs.Headings(doc);
        var tops = headings.Where(h => h.Level <= 2).ToList();

        if (tops.Count == 0)
        {
            var whole = doc.Trim();
            if (whole.Length >= MinChunkChars)
                yield return Chunk(uri, scope, title, "", whole);
            yield break;
        }

        for (var i = 0; i < tops.Count; i++)
        {
            var start = tops[i].Line;
            var end = i + 1 < tops.Count ? tops[i + 1].Line : lines.Length;
            var text = string.Join('\n', lines[start..end]).Trim();
            var section = SectionName(tops[i].Text);

            if (text.Length <= MaxChunkChars)
            {
                if (text.Length >= MinChunkChars) yield return Chunk(uri, scope, title, section, text);
                continue;
            }

            var subs = headings.Where(h => h.Level == 3 && h.Line > start && h.Line < end).ToList();
            if (subs.Count == 0)
            {
                yield return Chunk(uri, scope, title, section, text);
                continue;
            }

            var lead = string.Join('\n', lines[start..subs[0].Line]).Trim();
            if (lead.Length >= MinChunkChars) yield return Chunk(uri, scope, title, section, lead);

            for (var j = 0; j < subs.Count; j++)
            {
                var subEnd = j + 1 < subs.Count ? subs[j + 1].Line : end;
                var subText = string.Join('\n', lines[subs[j].Line..subEnd]).Trim();
                if (subText.Length < MinChunkChars) continue;
                yield return Chunk(uri, scope, title, $"{section} / {SectionName(subs[j].Text)}", subText);
            }
        }
    }

    private static KnowledgeChunk Chunk(string uri, KnowledgeScope scope, string title, string section, string text) =>
        new(uri, scope, title, section, text, PolsonManuals.CitedApis(text));

    private static string SectionName(string heading) =>
        Regex.Replace(heading, @"^\d+\.\s*", "").Trim();
    #endregion
}
