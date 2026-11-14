namespace Polson.ExtendedMind.DocumentProcessing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    /// <param name="cache">Where repeated answers are held. Null disables caching entirely.</param>
    public DocumentProcessor(string? apiKey, DocumentBudget budget, string? projectRoot = null,
        string model = DefaultModel, DocumentCache? cache = null)
    {
        this.budget = budget;
        this.projectRoot = projectRoot;
        this.cache = cache;
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

    /// <summary>Most pages <see cref="Find"/> lists; past it, <c>more</c> says a longer phrase is wanted.</summary>
    public const int MaxHits = 40;

    /// <summary>Characters of context <see cref="Find"/> gives either side of a match.</summary>
    const int ContextChars = 80;

    public string Model { get; }

    /// <summary>Remaining allowance, readable by scripts so an agent can plan rather than hit a wall.</summary>
    public DocumentBudget Budget => budget;

    /// <summary>Whether a key is configured at all. False means every call will refuse.</summary>
    public virtual bool IsAvailable => client is not null;

    /// <summary>What the run record files a read under: <c>document</c> here, <c>video</c> for <see cref="VideoReader"/>.</summary>
    protected virtual string Kind => "document";

    /// <summary>The extensions this surface reads, and what each is sent as.</summary>
    protected virtual IReadOnlyDictionary<string, string> Types => Documents.MimeTypes;

    /// <summary>Largest file sent inline, in bytes.</summary>
    protected virtual int MaxBytes => Documents.MaxInlineBytes;

    /// <summary>
    /// How long <c>ask</c> waits for a read before answering <c>Pending</c> and letting it finish into the cache; 0 waits
    /// to the end. Set from the server's script limit, less room for the script's own work. Internal: a script that could
    /// set it could make a read hold the script to the end of its time.
    /// </summary>
    internal double WaitSeconds { get; set; } = 20;
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
            .Where(f => Types.ContainsKey(Path.GetExtension(f)))
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
                MimeType = Types[Path.GetExtension(f)],
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

        if (!IsAvailable) return Refused(DocumentFailure.NotConfigured, "No API key is configured.");
        if (string.IsNullOrWhiteSpace(query)) return Refused(DocumentFailure.NoQuery, "The query was empty.");

        string? schema;
        try
        {
            schema = SchemaJson(opts.Schema);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Refused(DocumentFailure.InvalidRequest, $"The schema is not a JSON Schema object: {ex.Message}");
        }

        int[]? pages = null;
        if (opts.Pages is not null && (pages = ParsePages(opts.Pages)) is null)
        {
            return Refused(DocumentFailure.InvalidRequest,
                $"pages '{opts.Pages}' is not a page list. Write it as a print dialog takes it, counted from 1: '40-60, 75'.");
        }

        byte[] bytes;
        string mime;
        string named;

        try
        {
            (bytes, mime, named) = Read(source, opts);
        }
        catch (DocumentRefusal refusal)
        {
            return Refused(refusal.Failure, refusal.Message, Describe(source));
        }

        if (bytes.Length > MaxBytes)
        {
            return Refused(DocumentFailure.TooLarge,
                $"{named} is {bytes.Length / (1024 * 1024.0):0.#} MB against a {MaxBytes / (1024 * 1024.0):0.#} MB inline limit.",
                named);
        }

        var useModel = opts.Model ?? Model;
        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16];

        // The cache key carries everything that changes the answer, and a schema reshapes it.
        var images = opts.Images ?? true;
        var keyQuery = (schema is null ? query : query + "\nschema:" + schema) + (images ? "\nimages:on" : "\nimages:off")
            + (pages is null ? "" : "\npages:" + PageList(pages));
        // Both ways named, so an answer cached before page images existed is never served as one made with them. Pages
        // are named only when chosen, so every answer cached before they existed is still the whole document's.

        // Before the budget check, not after — a hit costs nothing, so it must still be served when
        // the allowance is spent. Checking affordability first would refuse a read that was free.
        if (cache is not null)
        {
            var key = DocumentCache.KeyOf(hash, keyQuery, useModel, mime);
            if (await cache.Get(key) is { } hit)
            {
                budget.CacheHits += 1;
                RequisitionScope.Record(new RequisitionRecord(
                    Kind, named, Success: true, Failure: null,
                    Reason: hit.Warnings.Count == 0 ? null : string.Join("; ", hit.Warnings),
                    Model: hit.Model, FromCache: true, Refused: false));
                RecordBudgetState();

                return new DocumentAnswer
                {
                    Success = true,
                    Text = hit.Text,
                    Answer = Parsed(hit.Text, schema),
                    Warnings = [.. hit.Warnings],
                    Evidence = [.. hit.Evidence],
                    Provenance = new DocumentProvenance
                    {
                        Model = hit.Model,
                        Source = named,
                        MimeType = mime,
                        Bytes = bytes.Length,
                        Hash = hash,
                        Query = query,
                        // The original read's time and cost, not this call's. A hit spent nothing,
                        // and reporting "now" would erase how old the answer being used actually is.
                        ReadUtc = hit.ReadUtc,
                        TokensSpent = hit.TokensSpent,
                        FromCache = true,
                        Route = hit.Route,
                        Pages = hit.Pages,
                        Fallback = hit.Fallback,
                        Endpoint = hit.Endpoint,
                        ImagePages = [.. hit.ImagePages],
                    },
                };
            }
        }

        var readKey = DocumentCache.KeyOf(hash, keyQuery, useModel, mime);
        if (inFlight.TryGetValue(readKey, out var running)) return await WaitFor(running, named, useModel, ct);

        if (!budget.CanAfford())
        {
            return Refused(DocumentFailure.BudgetExhausted,
                $"{budget.Spent} of {budget.Total} {Kind} reads already spent.", named);
        }

        budget.Spent += 1;

        // The read is its own task, not the script's: a long document can take longer than a script has (one took 28 s
        // and another 31 s of a 30 s limit, 2026-10-20). Waited on for `WaitSeconds`; past that the script is told it is
        // pending, and the read finishes into the cache for the next run. Never cancelled by the script's end for that reason.
        var reading = Read(bytes, mime, query, useModel, schema, images, pages, hash, keyQuery, named);
        if (cache is null || WaitSeconds <= 0) return await reading;
        inFlight[readKey] = reading;
        _ = reading.ContinueWith(_ => inFlight.TryRemove(readKey, out var _), TaskScheduler.Default);
        return await WaitFor(reading, named, useModel, ct);
    }

    /// <summary>
    /// The pages a phrase is on: <c>Documents.find('documents/s1.pdf', 'Consolidated Statements of Operations')</c>.
    /// </summary>
    /// <param name="source">A project-relative path, or the bytes themselves.</param>
    /// <param name="phrase">Words to find, matched ignoring case and how the text is broken across lines.</param>
    /// <remarks>
    /// <b>Free, offline and exact</b>: the page text the reader keeps, searched here, with no model and no budget. It is how
    /// a long document's pages are found before <c>pages</c> asks them a question. A contents page cannot do it: the S-1
    /// was printed from HTML, so its printed page numbers drift from the PDF's — printed page 5 is the PDF's twelfth — and
    /// the gap grows through the document.
    /// </remarks>
    public async Task<DocumentSearch> Find(object? source, string? phrase, CancellationToken ct = default)
    {
        var wanted = Normal(phrase ?? "");
        if (wanted.Length == 0) return new DocumentSearch { Failure = DocumentFailure.NoQuery, Error = "Nothing to find: give some words." };

        byte[] bytes;
        string mime;
        string named;
        try
        {
            (bytes, mime, named) = Read(source, new DocumentOptions());
        }
        catch (DocumentRefusal refusal)
        {
            return new DocumentSearch { Failure = refusal.Failure, Error = refusal.Message };
        }

        PagedText? paged;
        try
        {
            paged = await PageText(bytes, mime, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TooManyScannedPages or TimeoutException)
        {
            return new DocumentSearch { Failure = DocumentFailure.ServiceError, Error = $"{named} could not be read as text: {ex.Message}" };
        }
        if (paged is null)
            return new DocumentSearch { Failure = DocumentFailure.UnsupportedType,
                Error = $"{named} cannot be searched: find reads PDFs and text files, and a PDF needs mutool." };

        var hits = new List<DocumentHit>();
        for (var p = 1; p <= paged.Pages.Count; p++)
        {
            var text = Normal(paged.Pages[p - 1]);
            var at = text.IndexOf(wanted, StringComparison.Ordinal);
            if (at < 0) continue;
            var count = 0;
            for (var i = at; i >= 0; i = text.IndexOf(wanted, i + wanted.Length, StringComparison.Ordinal)) count++;
            var from = Math.Max(0, at - ContextChars);
            var to = Math.Min(text.Length, at + wanted.Length + ContextChars);
            hits.Add(new DocumentHit { Page = p, Count = count,
                Context = (from > 0 ? "…" : "") + Collapse(paged.Pages[p - 1], from, to) + (to < text.Length ? "…" : "") });
        }

        int[] unsearched = [.. (paged.Scanned ?? []).Except(paged.OcrPages).Order()];
        return new DocumentSearch { Success = true, Hits = [.. hits.Take(MaxHits)], More = hits.Count > MaxHits,
            Pages = paged.Pages.Count, Unsearched = unsearched };
    }

    /// <summary>
    /// A document's pages as text without asking a model, for <see cref="Find"/>; null when this reader has no way to.
    /// </summary>
    /// <remarks>A text file is one page. A PDF needs a page reader, which <see cref="NemotronDocumentProcessor"/> has; it
    /// OCRs nothing here, so a scan not yet read is listed as unsearched rather than making a search slow.</remarks>
    protected virtual Task<PagedText?> PageText(byte[] bytes, string mime, CancellationToken ct) =>
        Task.FromResult(IsText(mime) ? new PagedText { Pages = [Encoding.UTF8.GetString(bytes)] } : null);

    /// <summary>Whether a type is read as plain text: text files, JSON, XML.</summary>
    protected static bool IsText(string mime) =>
        mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mime is "application/json" or "application/xml";

    /// <summary>Text as a search compares it: lower case, curly apostrophes straight, every run of whitespace one space.</summary>
    private static string Normal(string s) =>
        string.Join(' ', s.Replace('’', '\'').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>Characters <c>from</c>–<c>to</c> of the page's normalised text, in its own case.</summary>
    private static string Collapse(string page, int from, int to)
    {
        var plain = string.Join(' ', page.Replace('’', '\'').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return plain[from..Math.Min(to, plain.Length)];
    }

    /// <summary>The read waited on for <see cref="WaitSeconds"/>; past that, <c>Pending</c>, and it carries on into the cache.</summary>
    private async Task<DocumentAnswer> WaitFor(Task<DocumentAnswer> reading, string named, string model, CancellationToken ct)
    {
        var done = await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(WaitSeconds), ct));
        if (done == reading) return await reading;
        return Attempted(DocumentFailure.Pending,
            $"Still reading after {WaitSeconds:0} s. The read carries on and its answer is kept; ask the same question again.",
            named, model);
    }

    /// <summary>One read, from the service to the cache: the part of <c>ask</c> that may outlive the script that started it.</summary>
    private async Task<DocumentAnswer> Read(byte[] bytes, string mime, string query, string useModel, string? schema, bool images,
        int[]? pages, string hash, string keyQuery, string named)
    {
        var ct = CancellationToken.None;
        try
        {
            var generated = await Generate(bytes, mime, query, useModel, schema, images, pages, ct);
            var (text, tokens, finish) = (generated.Text, generated.Tokens, generated.Finish);
            var answeredBy = generated.Model ?? useModel;

            // A 200 carrying no text is not an empty answer. Reporting it as success with an empty
            // string would let "" propagate into a caption and read as a deliberate blank.
            if (string.IsNullOrWhiteSpace(text))
            {
                var blocked = finish?.Contains("SAFETY", StringComparison.OrdinalIgnoreCase) == true;
                return Attempted(blocked ? DocumentFailure.SafetyBlocked : DocumentFailure.NoAnswer,
                    finish is null ? "The response carried no text." : $"Finish reason: {finish}.",
                    named, answeredBy);
            }

            budget.TokensSpent += tokens;

            // The evidence is the document's own words, so it is scanned with the answer: a quote is where an
            // instruction hidden in a filing would ride back.
            var evidence = generated.Evidence.Select(e => e with { Quote = TextScan.Sanitize(e.Quote) }).ToList();
            var warnings = Scan(string.Join("\n", [text, .. generated.Evidence.Select(e => e.Quote)]));
            if (warnings.Count > 0)
            {
                Warn("{0} '{1}' produced an answer carrying {2} scan finding(s).", Kind, named, warnings.Count);
            }

            // The successful read, in the record. `Reason` carries the scan verdict rather than
            // being null: "this run read a document and the document talked back" is the one thing
            // a reader most needs to find afterwards, and it would otherwise live only in the
            // answer the script received and did not keep.
            RequisitionScope.Record(new RequisitionRecord(
                Kind, named, Success: true,
                Failure: null,
                Reason: warnings.Count == 0 ? null : string.Join("; ", warnings),
                Model: answeredBy, FromCache: false, Refused: false));
            RecordBudgetState();

            var sanitised = TextScan.Sanitize(text);

            // One timestamp for both, so a cache hit replays exactly the time the original read
            // reported. Two calls to UtcNow differ by about a millisecond, which is enough to make
            // a hit and its own original disagree about when the document was read.
            var readUtc = DateTime.UtcNow;

            if (cache is not null)
            {
                // Stored after scanning, so a hit and a miss deliver the same thing. Awaited rather
                // than fired and forgotten: a write that loses the race with the next identical call
                // would make the cache look broken in exactly the loop it exists for.
                await cache.Put(DocumentCache.KeyOf(hash, keyQuery, useModel, mime), new CachedAnswer
                {
                    Text = sanitised,
                    Warnings = warnings,
                    Model = answeredBy,
                    ReadUtc = readUtc,
                    TokensSpent = tokens,
                    Evidence = evidence,
                    Route = generated.Route,
                    Pages = generated.Pages,
                    Fallback = generated.Fallback,
                    Endpoint = generated.Endpoint,
                    ImagePages = [.. generated.ImagePages],
                });
            }

            return new DocumentAnswer
            {
                Success = true,
                Text = sanitised,
                Answer = Parsed(sanitised, schema),
                Warnings = [.. warnings],
                Evidence = [.. evidence],
                Provenance = new DocumentProvenance
                {
                    Model = answeredBy,
                    Source = named,
                    MimeType = mime,
                    Bytes = bytes.Length,
                    Hash = hash,
                    Query = query,
                    ReadUtc = readUtc,
                    TokensSpent = tokens,
                    FromCache = false,
                    Route = generated.Route,
                    Pages = generated.Pages,
                    Fallback = generated.Fallback,
                    Endpoint = generated.Endpoint,
                    ImagePages = [.. generated.ImagePages],
                },
            };
        }
        catch (OperationCanceledException)
        {
            return Attempted(DocumentFailure.Cancelled, "Cancelled.", named, useModel);
        }
        catch (DocumentRefusal refusal)
        {
            // Decided once the document was open (a page past its last), before anything was sent: it costs nothing.
            budget.Spent -= 1;
            return Refused(refusal.Failure, refusal.Message, named);
        }
        catch (Exception ex)
        {
            return Attempted(Classify(ex), ex.Message, named, useModel);
        }
    }

    /// <summary>
    /// The one call that reaches the service. Overridable so the rest can be tested without one.
    /// </summary>
    /// <remarks>
    /// <b>A seam rather than an interface, because there is exactly one thing to swap.</b> The asset
    /// side has <c>IImageGenerator</c> and earns it — a generator carries a model, a cache key and a
    /// failure taxonomy of its own. Here the whole boundary is "bytes and a question in, text and a
    /// token count out", and everything worth testing sits on this side of it: containment, typing,
    /// ordering, the budget, the scan and the cache. Without some seam the cache could not be tested
    /// at all, which is worse than a protected method.
    /// </remarks>
    /// <param name="schema">A JSON Schema the answer must follow, as JSON text; null for prose.</param>
    /// <param name="images">Whether pictures may go to the model as page images; a model sent the whole file sees them anyway.</param>
    /// <param name="pages">The pages chosen, counted from 1, ascending; null for all of them.</param>
    protected virtual async Task<Generation> Generate(
        byte[] bytes, string mime, string query, string model, string? schema, bool images, int[]? pages, CancellationToken ct)
    {
        // A model sent the file whole reads every page; said rather than silently widened.
        var whole = await GenerateInline(bytes, mime, query, model, schema, ct);
        return pages is null ? whole : whole with { Fallback = PagesIgnored };
    }

    /// <summary>The document sent whole to Gemini, which reads it itself. Overridable for tests.</summary>
    protected virtual Task<Generation> GenerateInline(
        byte[] bytes, string mime, string query, string model, string? schema, CancellationToken ct) =>
        GenerateInline(bytes, mime, query, model, schema, null, ct);

    /// <summary>The same, with a clip sampled at <paramref name="fps"/> frames a second rather than the model's default of one.</summary>
    protected virtual async Task<Generation> GenerateInline(
        byte[] bytes, string mime, string query, string model, string? schema, double? fps, CancellationToken ct)
    {
        var parts = new List<Part>
        {
            new() { InlineData = new Blob { MimeType = mime, Data = bytes }, VideoMetadata = fps is { } f ? new VideoMetadata { Fps = f } : null },
            new() { Text = query },
        };

        var config = schema is null ? null : new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseJsonSchema = JsonDocument.Parse(schema).RootElement.Clone(),
        };

        var response = await client!.Models.GenerateContentAsync(
            model, [new Content { Role = "user", Parts = parts }], config, ct);

        var text = string.Concat(response.Candidates?
            .SelectMany(c => c.Content?.Parts ?? [])
            .Select(p => p.Text)
            .Where(t => !string.IsNullOrEmpty(t)) ?? []);

        return new Generation(text, response.UsageMetadata?.TotalTokenCount ?? 0,
            response.Candidates?.FirstOrDefault()?.FinishReason?.ToString()) { Model = model };
    }

    /// <summary>Whether a Gemini key is configured, so a document can be sent whole.</summary>
    protected bool CanReadInline => client is not null;

    /// <summary>Releases the client. Host lifetime, not something a script calls.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) client?.Dispose();
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

    private string MimeFor(string extension) =>
        Types.TryGetValue(extension, out var mime)
            ? mime
            : throw new DocumentRefusal(DocumentFailure.UnsupportedType,
                $"No MIME type is known for '{extension}'. Known: {string.Join(", ", Types.Keys)}.");

    /// <summary>A script's schema as JSON text, or null for none.</summary>
    /// <remarks>
    /// A JS object arrives as a dictionary and serialises as one; a string is taken to be JSON already and is
    /// parsed here, so a malformed one is refused before anything is spent rather than by the service after.
    /// </remarks>
    private static string? SchemaJson(object? schema) => schema switch
    {
        null => null,
        string text => JsonDocument.Parse(text).RootElement.ValueKind == JsonValueKind.Object
            ? text
            : throw new JsonException("expected an object"),
        _ => JsonSerializer.Serialize(schema),
    };

    /// <summary>A schema'd answer as script values (<c>answer</c>); null without a schema or when it does not parse.</summary>
    /// <remarks>A ```json fence is taken off first: a model that does not take a response schema natively may add one.</remarks>
    internal static object? Parsed(string text, string? schema)
    {
        if (schema is null || string.IsNullOrWhiteSpace(text)) return null;
        var json = text.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var start = json.IndexOf('\n');
            var end = json.LastIndexOf("```", StringComparison.Ordinal);
            json = start < 0 || end <= start ? json : json[(start + 1)..end].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return Polson.ExtendedMind.ParallelSearch.JsonInterop.ToClr(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A print dialog's page list, <c>'40-60, 75'</c>, as page numbers from 1, ascending and once each; null when
    /// it is not one.</summary>
    internal static int[]? ParsePages(string spec)
    {
        var pages = new SortedSet<int>();
        foreach (var part in spec.Split(',', StringSplitOptions.TrimEntries))
        {
            var ends = part.Split(['-', '–'], StringSplitOptions.TrimEntries);
            if (ends.Length is < 1 or > 2 || !int.TryParse(ends[0], out var from)
                || !int.TryParse(ends[^1], out var to) || from < 1 || to < from || to - from >= MaxPagesChosen)
                return null;
            for (var p = from; p <= to; p++) pages.Add(p);
            if (pages.Count > MaxPagesChosen) return null;
        }
        return pages.Count == 0 ? null : [.. pages];
    }

    /// <summary>Pages as the shortest list naming them, <c>40-60,75</c>: one spelling per choice, for the cache key.</summary>
    internal static string PageList(int[] pages) => string.Join(",", pages
        .Select((p, i) => (Page: p, Run: p - i))
        .GroupBy(x => x.Run)
        .Select(g => g.First().Page == g.Last().Page ? $"{g.First().Page}" : $"{g.First().Page}-{g.Last().Page}"));

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

    /// <summary>
    /// A refusal decided here, recorded as one and charged for nothing.
    /// </summary>
    /// <remarks>
    /// <b>Recorded separately from an attempt, because they are different events for a reader.</b> A
    /// run that spent an hour rewording a query is a different run from one the service kept turning
    /// away, and a run that pointed at a file that was never there is a third. <c>Refused: true</c>
    /// is what keeps them apart on the run page, exactly as it does for a classifier refusal.
    /// </remarks>
    private DocumentAnswer Refused(DocumentFailure failure, string error, string? descriptor = null)
    {
        RequisitionScope.Record(new RequisitionRecord(
            Kind, descriptor ?? "(none)", Success: false,
            Failure: failure.ToString(), Reason: error, Model: null,
            FromCache: false, Refused: true));
        RecordBudgetState();

        return new DocumentAnswer { Success = false, Failure = failure, Error = error };
    }

    /// <summary>
    /// A read that reached the service and did not come back with an answer.
    /// </summary>
    /// <remarks>
    /// The budget has already been spent by the time this is called, so it is an <b>attempt</b>
    /// rather than a refusal: the distinction is the whole reason the run record carries both, and
    /// conflating them would make a run that was rate-limited read like one that mistyped a path.
    /// </remarks>
    private DocumentAnswer Attempted(DocumentFailure failure, string error, string descriptor, string model)
    {
        RequisitionScope.Record(new RequisitionRecord(
            Kind, descriptor, Success: false,
            Failure: failure.ToString(), Reason: error, Model: model,
            FromCache: false, Refused: false));
        RecordBudgetState();

        return new DocumentAnswer { Success = false, Failure = failure, Error = error };
    }

    /// <summary>
    /// What to call the source in the record when the read never got far enough to name it.
    /// </summary>
    /// <remarks>
    /// A path is the useful thing to record — it is what the agent typed and what a reader would
    /// check. Bytes have no name, and the record says so rather than inventing one.
    /// </remarks>
    private static string Describe(object? source) => source switch
    {
        string path => path,
        byte[] raw => $"{raw.Length} bytes",
        null => "(none)",
        _ => source.GetType().Name,
    };

    /// <summary>Pushes this processor's allowance into the ambient scope, if one is collecting.</summary>
    /// <remarks>
    /// Pushed rather than read back, for the reason the asset toolkit gives: the engine substitutes a
    /// disabled processor of its own when none is configured, so a caller reading the budget off the
    /// statically configured one would describe an object the script never touched.
    /// </remarks>
    private void RecordBudgetState() => RequisitionScope.RecordBudget(
        new BudgetSnapshot(budget.Total, budget.Spent, budget.Remaining, budget.CacheHits, budget.TokensSpent));
    #endregion

    #region Fields
    /// <summary>The <c>provenance.fallback</c> of a read that was asked for some pages and had to send the whole file.</summary>
    protected const string PagesIgnored = "the whole document was read: this reader cannot choose pages";

    /// <summary>Most pages one call may choose; a choice is for narrowing, and every page is what no choice reads.</summary>
    const int MaxPagesChosen = 2000;

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<DocumentAnswer>> inFlight = new();
    readonly Client? client;
    readonly DocumentBudget budget;
    readonly DocumentCache? cache;
    readonly string? projectRoot;
    #endregion

    #region Types
    /// <summary>What one call to a model produced.</summary>
    /// <param name="Text">The answer; empty or null is no answer.</param>
    /// <param name="Tokens">Billed, as the service reported.</param>
    /// <param name="Finish">The service's finish reason, when it gave one.</param>
    protected sealed record Generation(string? Text, long Tokens, string? Finish)
    {
        /// <summary>The model that actually answered, when it is not the one asked for.</summary>
        public string? Model { get; init; }

        public IReadOnlyList<DocumentEvidence> Evidence { get; init; } = [];

        public string Route { get; init; } = "inline";

        public int Pages { get; init; }

        public string? Fallback { get; init; }

        /// <summary>Which <see cref="NemotronEndpoint"/> served it; null when no Nemotron endpoint did.</summary>
        public string? Endpoint { get; init; }

        public int[] ImagePages { get; init; } = [];
    }

    /// <summary>A local refusal, carrying the failure it should be reported as.</summary>
    /// <remarks>
    /// Internal to the read step so that every locally-decidable problem can be raised where it is
    /// noticed and still returned as a value at the boundary, rather than each call site having to
    /// thread a tuple back out. Protected for the one decided after the document is open: a page past its last.
    /// </remarks>
    protected sealed class DocumentRefusal(DocumentFailure failure, string message) : Exception(message)
    {
        public DocumentFailure Failure { get; } = failure;
    }
    #endregion
}
