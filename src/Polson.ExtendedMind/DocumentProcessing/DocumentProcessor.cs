namespace Polson.ExtendedMind.DocumentProcessing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Google.GenAI;
using Google.GenAI.Types;

/// <summary>
/// Reads a document the director supplied and answers a question about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The other half of the extended mind.</b> <c>Research</c> commissions figures from the open web
/// and <c>Assets</c> generates material; neither can read the PDF sitting in the project directory.
/// A studio asked to chart a box-office return should start from the records rather than from what a
/// model recalls about them, and this is the call that lets it.
/// </para>
/// <para>
/// <b>Bytes are sent inline, never a URL, and that is a decision rather than a simplification.</b>
/// The service does not dereference arbitrary <c>https://</c> links — <c>fileData.fileUri</c> takes a
/// Cloud Storage URI on Vertex and a File API handle on the Gemini API — so "just pass the URL" fails
/// locally and when deployed, differently in each place. Reading the bytes here is one code path that
/// behaves identically in both, and it keeps the set of things we will dereference ours rather than
/// the model's.
/// </para>
/// <para>
/// <b>A document is untrusted data.</b> Its text reaches an agent verbatim through the answer, so a
/// paragraph inside a PDF addressed to whoever is processing it would be relayed faithfully. Every
/// answer is scanned with <see cref="TextScan"/> before it is returned: the concealment classes are
/// stripped, and what was found travels with the answer as
/// <see cref="DocumentAnswer.Warnings"/> rather than being quietly removed.
/// </para>
/// </remarks>
public class DocumentProcessor : Runtime, IDisposable
{
    #region Constructors
    /// <param name="apiKey">Express-mode API key. Read from <c>ApiKeys:GoogleAgentPlatform</c>.</param>
    /// <param name="budget">Reads allowed this session.</param>
    /// <param name="projectRoot">Documents are read from inside this directory and nowhere else.</param>
    /// <param name="model">Default model. Overridable per call.</param>
    public DocumentProcessor(string? apiKey, DocumentBudget budget, string? projectRoot = null, string model = DefaultModel)
    {
        this.budget = budget;
        this.projectRoot = projectRoot;
        Model = model;

        // Null when unconfigured rather than throwing, so a server without a key still starts and
        // every call reports NotConfigured with a remedy. A studio that will not boot because one
        // optional surface has no key is worse than one that says so per call.
        client = string.IsNullOrWhiteSpace(apiKey) ? null : new Client(enterprise: true, apiKey: apiKey);
    }
    #endregion

    #region Properties
    /// <summary>Default model. Flash reads documents well and is what a budget is sized against.</summary>
    public const string DefaultModel = "gemini-2.5-flash";

    public string Model { get; }

    /// <summary>Remaining allowance, readable by scripts so an agent can plan rather than hit a wall.</summary>
    public DocumentBudget Budget => budget;

    /// <summary>Whether a key is configured at all. False means every call will refuse.</summary>
    public bool IsAvailable => client is not null;
    #endregion

    #region Methods
    /// <summary>
    /// What documents this project holds. <c>Documents.list()</c>.
    /// </summary>
    /// <remarks>
    /// <b>The discovery half, and the studio had none.</b> No tool on either side of the boundary
    /// enumerates the project — <c>read_file</c> opens a path it is given and the MCP tools list
    /// nothing — so an agent told "the figures are in the attached report" could only guess file
    /// names. It guesses badly and expensively; a live run spent three scripts guessing at a
    /// property name for want of the same kind of answer.
    /// <para>
    /// Free, offline, and unmetered on purpose: an agent should be able to see what it has before
    /// deciding what to spend on. Files of a type this surface cannot declare are omitted rather
    /// than listed-then-refused.
    /// </para>
    /// </remarks>
    public DocumentEntry[] List()
    {
        if (string.IsNullOrEmpty(projectRoot)) return [];

        var folder = Path.Combine(projectRoot, Documents.Folder);
        if (!Directory.Exists(folder)) return [];

        return [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => Documents.MimeTypes.ContainsKey(Path.GetExtension(f)))
            // The folder's own README is ours, not the director's. Listing it would offer the agent
            // a document whose only content is instructions about documents — and an agent that
            // spent a metered read on it would be right to be confused.
            .Where(f => !string.Equals(Path.GetFileName(f), "README.md", StringComparison.OrdinalIgnoreCase))
            .Select(f => new DocumentEntry
            {
                // Forward slashes and project-relative, so the value can be handed straight back to
                // `ask` on any platform rather than needing repair.
                Path = Path.GetRelativePath(projectRoot, f).Replace('\\', '/'),
                Name = Path.GetFileName(f),
                Bytes = (int)Math.Min(int.MaxValue, new FileInfo(f).Length),
                MimeType = Documents.MimeTypes[Path.GetExtension(f)],
            })
            .OrderBy(e => e.Path, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Asks a question of a document. <c>Documents.ask('documents/boxoffice.pdf', 'every film and its gross')</c>.
    /// </summary>
    /// <param name="source">A project-relative path, or the bytes themselves.</param>
    /// <param name="query">What to get out of it. Specific beats short.</param>
    /// <param name="options">Model and MIME overrides.</param>
    /// <remarks>
    /// Order matters and is the same as a requisition's: everything decidable locally is decided
    /// before the budget is touched, so a bad path, an unknown type, an oversized file and an empty
    /// question all cost nothing.
    /// </remarks>
    public async Task<DocumentAnswer> Ask(
        object? source, string? query, DocumentOptions? options = null, CancellationToken ct = default)
    {
        var opts = options ?? new DocumentOptions();

        if (client is null) return Refused(DocumentFailure.NotConfigured, "No API key is configured.");
        if (string.IsNullOrWhiteSpace(query)) return Refused(DocumentFailure.NoQuery, "The query was empty.");

        byte[] bytes;
        string mime;
        string named;

        try
        {
            (bytes, mime, named) = Read(source, opts);
        }
        catch (DocumentRefusal refusal)
        {
            return Refused(refusal.Failure, refusal.Message);
        }

        if (bytes.Length > Documents.MaxInlineBytes)
        {
            return Refused(DocumentFailure.TooLarge,
                $"{named} is {bytes.Length / (1024 * 1024)} MB against a {Documents.MaxInlineBytes / (1024 * 1024)} MB inline limit.");
        }

        if (!budget.CanAfford())
        {
            return Refused(DocumentFailure.BudgetExhausted,
                $"{budget.Spent} of {budget.Total} document reads already spent.");
        }

        var useModel = opts.Model ?? Model;
        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16];

        budget.Spent += 1;

        try
        {
            var parts = new List<Part>
            {
                new() { InlineData = new Blob { MimeType = mime, Data = bytes } },
                new() { Text = query },
            };

            var response = await client.Models.GenerateContentAsync(
                useModel, [new Content { Role = "user", Parts = parts }], config: null, ct);

            var text = string.Concat(response.Candidates?
                .SelectMany(c => c.Content?.Parts ?? [])
                .Select(p => p.Text)
                .Where(t => !string.IsNullOrEmpty(t)) ?? []);

            // A 200 carrying no text is not an empty answer. Reporting it as success with an empty
            // string would let "" propagate into a caption and read as a deliberate blank.
            if (string.IsNullOrWhiteSpace(text))
            {
                var finish = response.Candidates?.FirstOrDefault()?.FinishReason?.ToString();
                var blocked = finish?.Contains("SAFETY", StringComparison.OrdinalIgnoreCase) == true;
                return Refused(blocked ? DocumentFailure.SafetyBlocked : DocumentFailure.NoAnswer,
                    finish is null ? "The response carried no text." : $"Finish reason: {finish}.");
            }

            var tokens = response.UsageMetadata?.TotalTokenCount ?? 0;
            budget.TokensSpent += tokens;

            var warnings = Scan(text);
            if (warnings.Count > 0)
            {
                Warn("Document '{0}' produced an answer carrying {1} scan finding(s).", named, warnings.Count);
            }

            return new DocumentAnswer
            {
                Success = true,
                Text = TextScan.Sanitize(text),
                Warnings = warnings,
                Provenance = new DocumentProvenance
                {
                    Model = useModel,
                    Source = named,
                    MimeType = mime,
                    Bytes = bytes.Length,
                    Hash = hash,
                    Query = query,
                    ReadUtc = DateTime.UtcNow,
                    TokensSpent = tokens,
                },
            };
        }
        catch (OperationCanceledException)
        {
            return Refused(DocumentFailure.Cancelled, "Cancelled.");
        }
        catch (Exception ex)
        {
            return Refused(Classify(ex), ex.Message);
        }
    }

    /// <summary>Releases the client. Host lifetime, not something a script calls.</summary>
    public void Dispose()
    {
        client?.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Methods — reading
    /// <summary>Bytes, MIME type and a name for the record, from whatever the caller passed.</summary>
    private (byte[] Bytes, string Mime, string Named) Read(object? source, DocumentOptions opts)
    {
        switch (source)
        {
            case null:
                throw new DocumentRefusal(DocumentFailure.NotFound, "No document was given.");

            case byte[] raw:
            {
                // Bytes carry no name, so the type cannot be inferred and guessing it would produce a
                // confident answer about nothing.
                var mime = opts.MimeType
                    ?? throw new DocumentRefusal(DocumentFailure.UnsupportedType,
                        "Bytes carry no file name, so pass an explicit mimeType, e.g. 'application/pdf'.");
                return (raw, mime, $"{raw.Length} bytes");
            }

            case string path:
            {
                // Containment throws ArgumentException, which is not a DocumentRefusal — so without
                // this it escaped Ask entirely and a path traversal was the one failure this surface
                // reported by throwing. Every other one is a value, and an agent branching on
                // `answer.success` would never have seen it.
                string full;
                try
                {
                    full = ProjectPath.Resolve(projectRoot, path, nameof(source), "Read");
                }
                catch (ArgumentException ex)
                {
                    throw new DocumentRefusal(DocumentFailure.NotFound, ex.Message);
                }

                // Naming what IS there, rather than only what is not. An agent that guessed
                // 'boxoffice.pdf' for 'documents/box-office-2025.pdf' is one line from being right,
                // and without this it spends a turn per guess. Same move as peek's artifact listing.
                if (!System.IO.File.Exists(full))
                {
                    var available = List();
                    throw new DocumentRefusal(DocumentFailure.NotFound, available.Length == 0
                        ? $"'{path}' is not there, and this project holds no documents at all."
                        : $"'{path}' is not there. This project holds: {string.Join(", ", available.Select(e => e.Path))}.");
                }

                var mime = opts.MimeType ?? MimeFor(Path.GetExtension(full));
                return (System.IO.File.ReadAllBytes(full), mime, path);
            }

            default:
                throw new DocumentRefusal(DocumentFailure.InvalidRequest,
                    $"A document is a project-relative path or a byte array; got {source.GetType().Name}.");
        }
    }

    private static string MimeFor(string extension) =>
        Documents.MimeTypes.TryGetValue(extension, out var mime)
            ? mime
            : throw new DocumentRefusal(DocumentFailure.UnsupportedType,
                $"No MIME type is known for '{extension}'. Known: {string.Join(", ", Documents.MimeTypes.Keys)}.");

    /// <summary>
    /// What the scan found in an answer, in the same wording research findings use.
    /// </summary>
    private static IReadOnlyList<string> Scan(string text)
    {
        var report = TextScan.Scan(text, "answer");
        if (report.Clean) return [];

        var findings = new List<string>();
        foreach (var hidden in report.Hidden)
        {
            findings.Add($"answer: {hidden.Count}× {hidden.ClassName} ({hidden.Notation})");
        }

        foreach (var phrase in report.Phrases)
        {
            findings.Add($"answer: text addressed to the reader [{phrase.Kind}] — \"{phrase.Match}\"");
        }

        return findings;
    }

    private static DocumentFailure Classify(Exception ex) => ex switch
    {
        ArgumentException => DocumentFailure.NotFound,
        TimeoutException => DocumentFailure.Timeout,
        System.Net.Http.HttpRequestException => DocumentFailure.Network,
        _ when Says(ex, "429") || Says(ex, "RESOURCE_EXHAUSTED") => DocumentFailure.RateLimited,
        _ when Says(ex, "401") || Says(ex, "403") || Says(ex, "API key") => DocumentFailure.Auth,
        _ when Says(ex, "400") || Says(ex, "INVALID_ARGUMENT") => DocumentFailure.InvalidRequest,
        _ => DocumentFailure.ServiceError,
    };

    private static bool Says(Exception ex, string token) =>
        ex.Message.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static DocumentAnswer Refused(DocumentFailure failure, string error) =>
        new() { Success = false, Failure = failure, Error = error };
    #endregion

    #region Fields
    readonly Client? client;
    readonly DocumentBudget budget;
    readonly string? projectRoot;
    #endregion

    #region Types
    /// <summary>A local refusal, carrying the failure it should be reported as.</summary>
    /// <remarks>
    /// Internal to the read step so that every locally-decidable problem can be raised where it is
    /// noticed and still returned as a value at the boundary, rather than each call site having to
    /// thread a tuple back out.
    /// </remarks>
    private sealed class DocumentRefusal(DocumentFailure failure, string message) : Exception(message)
    {
        public DocumentFailure Failure { get; } = failure;
    }
    #endregion
}
