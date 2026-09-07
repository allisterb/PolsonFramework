namespace Polson.ExtendedMind.DocumentProcessing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// File-backed store of answers, keyed by the document, the question and the model together.
/// </summary>
/// <remarks>
/// <para>
/// <b>The loop this shortens is re-running a script, which is how an agent works.</b> A drawing is
/// built by editing <c>artwork.js</c> and running it again — measured on one session, consecutive
/// large scripts shared 71% of their lines — and a <c>Documents.ask</c> near the top of that file is
/// re-billed on every pass. Nothing about the answer changes between them: the same bytes and the
/// same question put to the same model is the definition of a repeat.
/// </para>
/// <para>
/// <b>All three parts are in the key, and leaving any out would be wrong in a way that is hard to
/// see.</b> Drop the document and two files answer each other's questions. Drop the query and the
/// first question asked of a file answers every later one — the failure that would have made the
/// Northwind run's verbatim transcription return a field extraction instead. Drop the model and an
/// answer from a cheaper model is served for a call that asked for a better one.
/// </para>
/// <para>
/// The answer is stored <b>after</b> scanning and sanitising, with its findings beside it, so a hit
/// and a miss deliver the same thing. Storing the raw text and re-scanning on read would be a second
/// implementation of the check that matters most here.
/// </para>
/// </remarks>
public class DocumentCache : Runtime
{
    #region Constructors
    public DocumentCache(string? directory = null)
    {
        this.directory = directory ?? Path.Combine(CamelDir, "documents");
        Directory.CreateDirectory(this.directory);
    }
    #endregion

    #region Methods
    /// <summary>The content address of one question put to one document by one model.</summary>
    public static string KeyOf(string documentHash, string query, string model, string mimeType)
    {
        var material = new StringBuilder()
            .Append(documentHash).Append('|')
            .Append(model).Append('|')
            .Append(mimeType).Append('|')
            .Append(query);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString())))[..32];
    }

    /// <summary>The stored answer, or null when nothing is held or the entry is unusable.</summary>
    public async Task<CachedAnswer?> Get(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return null;

        try
        {
            var stored = JsonSerializer.Deserialize<CachedAnswer>(await File.ReadAllTextAsync(path));

            // An entry with no text is not an answer. Missing rather than delivering an empty string
            // keeps a corrupted file from presenting as a document that said nothing.
            return string.IsNullOrWhiteSpace(stored?.Text) ? null : stored;
        }
        catch (Exception ex)
        {
            // A cache is an optimisation and must never be the reason a read fails. A bad entry is
            // reported and treated as a miss, so the next call rewrites it.
            Warn("Discarding unreadable document cache entry {0}: {1}", key, ex.Message);
            return null;
        }
    }

    /// <summary>Stores an answer. Failure to write is logged and otherwise ignored.</summary>
    public async Task Put(string key, CachedAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (string.IsNullOrWhiteSpace(answer.Text)) return;

        try
        {
            await File.WriteAllTextAsync(PathFor(key),
                JsonSerializer.Serialize(answer, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Warn("Could not write document cache entry {0}: {1}", key, ex.Message);
        }
    }

    private string PathFor(string key) => Path.Combine(directory, key + ".json");
    #endregion

    #region Fields
    readonly string directory;
    #endregion
}

/// <summary>An answer as it is held between runs.</summary>
/// <remarks>
/// Carries what a hit must reproduce, and nothing that describes the call rather than the answer: the
/// path and the byte count belong to <i>this</i> read, and a hit for a file that moved should still
/// report where it was read from now.
/// </remarks>
public sealed record CachedAnswer
{
    /// <summary>The answer, already scanned and stripped.</summary>
    public required string Text { get; init; }

    /// <summary>What the scan found when it was first read. Empty is the expected value.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public required string Model { get; init; }

    /// <summary>When the answer was originally obtained, not when it was served.</summary>
    public required DateTime ReadUtc { get; init; }

    /// <summary>What the original read cost. A hit costs nothing and does not add to this.</summary>
    public long TokensSpent { get; init; }
}
