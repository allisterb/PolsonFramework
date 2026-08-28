namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// In-process BM25 retrieval over the studio manuals and the SDK reference. The corpus is small enough
/// (hundreds of sections) that lexical ranking is both adequate and sub-millisecond; it exists so
/// <c>Search</c> works with no cloud dependency, and so the tool contract is settled before a
/// vector/RAG backend is introduced for user-supplied reference works.
/// </summary>
public sealed class LocalKnowledgeIndex : IKnowledgeIndex
{
    #region Constants
    private const double K1 = 1.2;
    private const double B = 0.75;
    private const double TitleBoost = 1.75;
    private const double ApiBoost = 2.5;
    private const int MaxExcerptChars = 1800;
    #endregion

    #region Constructors
    public LocalKnowledgeIndex(IReadOnlyList<KnowledgeChunk>? chunks = null)
    {
        Chunks = chunks ?? KnowledgeCorpus.Chunks;
        documents = [.. Chunks.Select(Document)];
        averageLength = documents.Count > 0 ? documents.Average(d => (double)d.Length) : 0.0;

        documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var term in document.Terms.Keys)
            {
                documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
            }
        }
    }
    #endregion

    #region Properties
    public string Name => "local-bm25";

    public IReadOnlyList<KnowledgeChunk> Chunks { get; }
    #endregion

    #region Methods
    public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(string query, int k, KnowledgeScope scope, CancellationToken ct = default) =>
        Task.FromResult(Search(query, k, scope));

    public IReadOnlyList<KnowledgeHit> Search(string query, int k, KnowledgeScope scope)
    {
        if (string.IsNullOrWhiteSpace(query) || documents.Count == 0) return [];

        var terms = Tokenize(query).Distinct(StringComparer.Ordinal).ToArray();
        if (terms.Length == 0) return [];

        var hits = new List<(int Index, double Score)>();
        for (var i = 0; i < documents.Count; i++)
        {
            if (scope != KnowledgeScope.All && Chunks[i].Scope != scope) continue;
            var score = Score(documents[i], terms);
            if (score > 0) hits.Add((i, score));
        }

        return [.. hits
            .OrderByDescending(h => h.Score)
            .Take(Math.Clamp(k, 1, 25))
            .Select(h => Hit(Chunks[h.Index], h.Score, terms))];
    }

    /// <summary>Splits text into search terms, keeping whole identifiers and their camelCase parts.</summary>
    public static IEnumerable<string> Tokenize(string text)
    {
        foreach (Match match in Word.Matches(text))
        {
            var raw = match.Value;
            var lower = raw.ToLowerInvariant();
            if (!Stop.Contains(lower) && lower.Length > 1) yield return Stem(lower);

            if (!HasInnerCaps(raw)) continue;
            foreach (Match part in CamelPart.Matches(raw))
            {
                var piece = part.Value.ToLowerInvariant();
                if (piece.Length > 1 && !Stop.Contains(piece)) yield return Stem(piece);
            }
        }
    }

    private double Score(Doc document, string[] terms)
    {
        var total = 0.0;
        foreach (var term in terms)
        {
            if (!document.Terms.TryGetValue(term, out var frequency)) continue;

            var n = documentFrequency.GetValueOrDefault(term);
            var idf = Math.Log(1.0 + ((documents.Count - n + 0.5) / (n + 0.5)));
            var norm = frequency * (K1 + 1.0) / (frequency + (K1 * (1.0 - B + (B * document.Length / Math.Max(averageLength, 1.0)))));

            var weight = 1.0;
            if (document.Title.Contains(term)) weight *= TitleBoost;
            if (document.Apis.Contains(term)) weight *= ApiBoost;

            total += idf * norm * weight;
        }
        return total;
    }

    private static Doc Document(KnowledgeChunk chunk)
    {
        var terms = new Dictionary<string, int>(StringComparer.Ordinal);
        var length = 0;
        foreach (var term in Tokenize($"{chunk.Title}\n{chunk.Section}\n{chunk.Text}"))
        {
            terms[term] = terms.GetValueOrDefault(term) + 1;
            length++;
        }

        return new Doc(
            terms,
            length,
            [.. Tokenize($"{chunk.Title} {chunk.Section}")],
            [.. chunk.Apis.SelectMany(Tokenize)]);
    }

    private static KnowledgeHit Hit(KnowledgeChunk chunk, double score, string[] terms) =>
        new(chunk.Uri,
            chunk.Title,
            chunk.Section,
            chunk.Scope == KnowledgeScope.Manual ? "manual" : "sdk",
            Math.Round(score, 4),
            chunk.Apis,
            Excerpt(chunk.Text, terms));

    /// <summary>Returns the section whole when it is small, otherwise the densest window of paragraphs.</summary>
    private static string Excerpt(string text, string[] terms)
    {
        if (text.Length <= MaxExcerptChars) return text;

        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var scores = paragraphs
            .Select(p => Tokenize(p).Count(t => terms.Contains(t, StringComparer.Ordinal)))
            .ToArray();

        var best = Array.IndexOf(scores, scores.Max());
        var excerpt = new StringBuilder(paragraphs[0].StartsWith('#') ? paragraphs[0] + "\n\n" : "");

        for (var i = best; i < paragraphs.Length && excerpt.Length < MaxExcerptChars; i++)
        {
            excerpt.Append(paragraphs[i]).Append("\n\n");
        }

        return excerpt.ToString().TrimEnd() + (best > 0 || excerpt.Length < text.Length ? "\n\n… (section truncated — read the full resource for the rest)" : "");
    }

    private static string Stem(string term)
    {
        if (term.EndsWith("ies", StringComparison.Ordinal) && term.Length > 4) return term[..^3] + "y";
        if (term.EndsWith("ing", StringComparison.Ordinal) && term.Length > 5) return term[..^3];
        if (term.EndsWith("es", StringComparison.Ordinal) && term.Length > 4) return term[..^2];
        if (term.EndsWith("ed", StringComparison.Ordinal) && term.Length > 4) return term[..^2];
        if (term.EndsWith('s') && !term.EndsWith("ss", StringComparison.Ordinal) && term.Length > 3) return term[..^1];
        return term;
    }

    private static bool HasInnerCaps(string raw) =>
        raw.Length > 1 && raw.Skip(1).Any(char.IsUpper);

    private static readonly Regex Word = new(@"[A-Za-z][A-Za-z0-9]*", RegexOptions.Compiled);

    private static readonly Regex CamelPart = new(@"[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|^[a-z0-9]+", RegexOptions.Compiled);

    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "for", "from", "how", "i", "in", "is", "it",
        "its", "me", "of", "on", "or", "so", "than", "that", "the", "then", "there", "these", "this", "to", "use",
        "using", "was", "we", "what", "when", "which", "will", "with", "you", "your"
    };

    private sealed record Doc(
        Dictionary<string, int> Terms,
        int Length,
        HashSet<string> Title,
        HashSet<string> Apis);

    private readonly List<Doc> documents;
    private readonly Dictionary<string, int> documentFrequency;
    private readonly double averageLength;
    #endregion
}
