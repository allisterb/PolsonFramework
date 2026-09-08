namespace Polson.ExtendedMind.Photos;

using System;
using System.Collections.Generic;

/// <summary>
/// Request and result types for the reference-photograph API.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script reading <c>photo.creditLine</c> reaches
/// <see cref="PhotoAsset.CreditLine"/>. The camelCase form is the one documented in
/// <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public static class Photographs
{
    /// <summary>Delivered width when the caller does not choose one.</summary>
    public const int DefaultWidth = 800;

    /// <summary>Narrowest delivery. Below this a face is not recognisable at any crop.</summary>
    public const int MinWidth = 64;

    /// <summary>
    /// Widest delivery. Sources hold originals far larger — one probe subject was 5,388 x 3,368 at
    /// 13 MB — and a full-resolution master has no use in a panel and every chance of blowing a
    /// context window if a script data-URIs it.
    /// </summary>
    public const int MaxWidth = 2000;
}

#region Requests

/// <summary>Options for <c>Photo.of(...)</c> and <c>Photo.resolve(...)</c>.</summary>
public sealed record PhotoOptions
{
    /// <summary>
    /// Requested width in pixels, clamped to [64, 2000]. <b>A request, not a guarantee.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source renders thumbnails at standard sizes and rounds up to the next one, so asking for
    /// 400 yields a 500-pixel rendition and asking for 800 yields 960. It compounds the confusion by
    /// reporting the width you asked for in its own metadata while linking the larger file — a
    /// mismatch a caller that trusted the metadata would never notice.
    /// </para>
    /// <para>
    /// So <see cref="PhotoAsset.Width"/> and <see cref="PhotoAsset.Height"/> are measured from the
    /// decoded bytes and are always true; treat this as a ceiling on cost rather than a layout
    /// dimension. Nothing is resampled on our side: the caller has to crop anyway, because aspect
    /// ratios are not consistent between subjects — see <see cref="PhotoAsset.AspectRatio"/> — and
    /// a re-encode to hit an exact width would lose quality for a number Skia can scale to at draw
    /// time for free.
    /// </para>
    /// </remarks>
    public int Width { get; init; } = Photographs.DefaultWidth;

    /// <summary>
    /// A word that must appear in the matched subject's own one-line description, else the match is
    /// refused as <see cref="PhotoFailure.WrongSubject"/>.
    /// </summary>
    /// <remarks>
    /// <b>The identity gate, and the reason this API is not just a fetch.</b> Coverage is easy;
    /// resolving to the wrong person is the failure that looks like success, because a wrong face
    /// renders perfectly and nothing downstream can tell. A probe over eight subjects found the
    /// descriptions to be reliably diagnostic — <c>"American actress (born 1997)"</c>,
    /// <c>"Volcano in Japan"</c>, <c>"Bridge in the San Francisco Bay Area"</c> — so a caller who
    /// knows it wants an actress can say so and have a musician of the same name refused.
    /// Case-insensitive substring. Null means no assertion, which is the permissive default.
    /// </remarks>
    public string? Expect { get; init; }

    /// <summary>
    /// Licence names that may be delivered, matched case-insensitively at a word boundary — e.g.
    /// <c>["CC0", "CC BY"]</c> accepts <c>CC0</c> and <c>CC BY 3.0</c> but refuses <c>CC BY-SA 4.0</c>
    /// and <c>CC BY-NC 2.0</c>. Name <c>CC BY-SA</c> explicitly to allow share-alike.
    /// </summary>
    /// <remarks>
    /// Null accepts any <i>stated</i> licence. Worth setting for anything commercial: cropping a
    /// photograph into a graphic is plausibly an adaptation rather than a collection, which would
    /// carry a share-alike obligation onto the finished artwork. Five of seven subjects in the probe
    /// came back CC BY-SA.
    /// </remarks>
    public string[]? RequireLicence { get; init; }

    /// <summary>
    /// Deliver a photograph whose source states no licence at all. Off, and it should stay off.
    /// </summary>
    /// <remarks>
    /// An unstated licence is not a permissive one. Turning this on produces bytes that cannot be
    /// credited and whose terms are unrecorded, which is the exact condition <c>CLAUDE.md</c> §0
    /// requires the reference ledger to prevent for written material.
    /// </remarks>
    public bool AllowUnstatedLicence { get; init; }

    /// <summary>Wiki language edition to resolve against. Affects which article — and so which lead image — is found.</summary>
    public string Language { get; init; } = "en";
}

#endregion

#region Results

/// <summary>What a source says about the terms attached to a photograph.</summary>
/// <remarks>
/// The whole argument for this source over an image search. Google's Custom Search JSON API can
/// <i>filter</i> by licence (<c>rights=cc_publicdomain</c> and friends) but its result schema carries
/// no licence field at all, so a filtered result still cannot be credited or recorded. Everything
/// here comes back as metadata alongside the bytes.
/// </remarks>
public sealed record PhotoLicence
{
    /// <summary>Short name, e.g. <c>CC BY-SA 4.0</c>, <c>CC0</c>. Null when the source states none.</summary>
    public string? Name { get; init; }

    /// <summary>The longer statement of terms, when there is one.</summary>
    public string? UsageTerms { get; init; }

    /// <summary>Who made the photograph. Null when unstated.</summary>
    public string? Artist { get; init; }

    /// <summary>Where it came from — often <c>Own work</c>.</summary>
    public string? Credit { get; init; }

    /// <summary>Whether the licence obliges attribution. Null when the source does not say.</summary>
    public bool? AttributionRequired { get; init; }

    /// <summary>
    /// Non-licence constraints the source flags, e.g. <c>personality</c> or <c>trademarked</c>.
    /// </summary>
    /// <remarks>
    /// Separate from the licence and not implied by it: <c>personality</c> means the subject's own
    /// publicity rights bear on the use, which a permissive copyright licence does not settle. Seen
    /// live on one of eight probe subjects.
    /// </remarks>
    public string? Restrictions { get; init; }

    /// <summary>Page describing the file and its terms — where a human verifies any of this.</summary>
    public string? DescriptionUrl { get; init; }

    /// <summary>True when the source stated a licence at all.</summary>
    public bool IsStated => !string.IsNullOrWhiteSpace(Name);

    /// <summary>The documented surface, for `JSON.stringify`. See <see cref="PhotoAsset.ToJSON"/>.</summary>
    /// <remarks>
    /// Nested types need this too, or a half-done job emits camelCase at the top level and
    /// PascalCase one level down — which is harder to work with than being uniformly wrong.
    /// </remarks>
    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["name"] = Name,
        ["usageTerms"] = UsageTerms,
        ["artist"] = Artist,
        ["credit"] = Credit,
        ["attributionRequired"] = AttributionRequired,
        ["restrictions"] = Restrictions,
        ["descriptionUrl"] = DescriptionUrl,
        ["isStated"] = IsStated,
    };
}

/// <summary>A subject resolved to a page and a lead photograph, before any bytes are fetched.</summary>
public sealed record SubjectMatch
{
    public bool Success { get; init; }

    /// <summary>What the caller asked for, verbatim.</summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>The article the query resolved to.</summary>
    public string? Title { get; init; }

    /// <summary>The subject's own one-line description, which is what <see cref="PhotoOptions.Expect"/> tests.</summary>
    public string? Description { get; init; }

    /// <summary>True when the query landed on a disambiguation page, which is never a subject.</summary>
    public bool IsDisambiguation { get; init; }

    /// <summary>
    /// Other pages the search matched, best first.
    /// </summary>
    /// <remarks>
    /// Carried so a wrong resolution is <i>visible</i> rather than silent — when "Georgia" refuses,
    /// this is what says <c>Georgia (country)</c> and <c>Georgia (U.S. state)</c> were the runners-up,
    /// which is the whole of the remedy. Declared as an array rather than
    /// <c>IReadOnlyList</c> for the reason recorded on <c>RequisitionVerdict.Triggers</c>: Jint
    /// caches a property's member accessor against its runtime type, and a property that alternates
    /// between <c>string[]</c> and <c>List&lt;string&gt;</c> throws on a later call.
    /// </remarks>
    public string[] Alternatives { get; init; } = [];

    /// <summary>The lead image's file title on the source, e.g. <c>File:Zendaya-byPhilipRomano.jpg</c>.</summary>
    public string? File { get; init; }

    /// <summary>Terms attached to that file. Null when resolution failed before reading them.</summary>
    public PhotoLicence? Licence { get; init; }

    /// <summary>Direct URL of the delivered rendition, on the source's media host.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Pixel size of the source original, before the delivered rendition is resampled.</summary>
    public int SourceWidth { get; init; }

    public int SourceHeight { get; init; }

    public PhotoFailure Failure { get; init; } = PhotoFailure.None;

    public string? Error { get; init; }

    /// <inheritdoc cref="PhotoAsset.FailureName"/>
    public string FailureName => Failure.ToString();

    /// <summary>What to do next, phrased for the agent reading it.</summary>
    public string Remedy => PhotoFailures.RemedyFor(Failure);

    public bool Retryable => PhotoFailures.IsRetryable(Failure);

    /// <summary>The documented surface, for `JSON.stringify`. See <see cref="PhotoAsset.ToJSON"/>.</summary>
    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["success"] = Success,
        ["query"] = Query,
        ["title"] = Title,
        ["description"] = Description,
        ["isDisambiguation"] = IsDisambiguation,
        ["alternatives"] = Alternatives,
        ["file"] = File,
        ["licence"] = Licence?.ToJSON(),
        ["imageUrl"] = ImageUrl,
        ["sourceWidth"] = SourceWidth,
        ["sourceHeight"] = SourceHeight,
        ["failureName"] = FailureName,
        ["error"] = Error,
        ["remedy"] = Remedy,
        ["retryable"] = Retryable,
    };
}

/// <summary>A reference photograph and the terms it arrived with.</summary>
public sealed record PhotoAsset : IDataUriSource
{
    #region Properties
    public bool Success { get; init; }

    /// <summary>Content address of (source, file, width). Stable across runs; also the cache key.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Encoded bytes. Empty when <see cref="Success"/> is false.</summary>
    public byte[] Bytes { get; init; } = [];

    public int Width { get; init; }

    public int Height { get; init; }

    public string MimeType { get; init; } = "image/jpeg";

    /// <summary>How the subject resolved, including the runners-up. Never null.</summary>
    public required SubjectMatch Subject { get; init; }

    /// <summary>Terms. Null only when the fetch failed before they were read.</summary>
    public PhotoLicence? Licence { get; init; }

    /// <summary>Which source answered, e.g. <c>wikimedia</c>.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Direct URL these bytes came from.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Agent role that requisitioned it, for attribution and spend accounting in the run record.</summary>
    public string Requester { get; init; } = "unknown";

    public DateTime FetchedUtc { get; init; }

    /// <summary>True when this delivery came from the session cache and cost no request.</summary>
    public bool FromCache { get; init; }

    public PhotoFailure Failure { get; init; } = PhotoFailure.None;

    public string? Error { get; init; }

    /// <summary>
    /// The failure as a name rather than a number.
    /// </summary>
    /// <remarks>
    /// Jint surfaces a CLR enum to a script as its underlying integer, so <c>failure</c> alone reads
    /// as <c>6</c> in JavaScript. This is the spelling the reference documents and an agent can act on.
    /// </remarks>
    public string FailureName => Failure.ToString();

    /// <summary>What to do next, phrased for the agent reading it.</summary>
    public string Remedy => PhotoFailures.RemedyFor(Failure);

    /// <summary>Whether repeating the identical request could plausibly succeed.</summary>
    public bool Retryable => PhotoFailures.IsRetryable(Failure);

    /// <summary>
    /// Delivered width over height. Not consistent between subjects, so a row of portraits needs
    /// cropping: the probe measured 0.67 to 0.82 across five people and 1.60 to 2.05 across two places.
    /// </summary>
    public double AspectRatio => Height > 0 ? (double)Width / Height : 0;
    #endregion

    #region Methods
    /// <summary>
    /// What <c>JSON.stringify(photo)</c> serialises: the documented surface, in the documented
    /// spelling, with the image reported as a size rather than transcribed as numbers.
    /// </summary>
    /// <remarks>
    /// <b>Two faults, one hook.</b> Without this, <c>JSON.stringify</c> walks the CLR object, so it
    /// emits the raw <c>Bytes</c> as a decimal array and names every field in PascalCase. Measured
    /// on one 960px photograph: <b>523,295 characters</b> — about six times the file's own size,
    /// because a byte becomes up to four digits and a comma — and
    /// <c>JSON.parse(JSON.stringify(p)).aspectRatio</c> reads <c>undefined</c> while
    /// <c>p.aspectRatio</c> works, since the camelCase spelling is Jint's *access* mapping and does
    /// not survive serialisation.
    /// <para>
    /// <b>The cost is recurring, not one-off, which is what makes it worth fixing.</b> On the
    /// 2026-09-08 deployed run a stringified asset took one turn's input from ~32k tokens to
    /// ~555k — and because it changed the prompt prefix, <c>cached</c> fell from 27,590 to
    /// <b>zero and stayed there</b>. Every later turn re-paid the full 555k, and four of them
    /// tripped the 2,000,000-token breaker.
    /// </para>
    /// <para>
    /// <c>byteLength</c> rather than silently dropping the image: a reader asking what is in this
    /// object should still learn that there is one and how big it is. <c>photo.bytes</c> is
    /// untouched — only serialisation changes.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["success"] = Success,
        ["id"] = Id,
        ["byteLength"] = Bytes.Length,
        ["width"] = Width,
        ["height"] = Height,
        ["mimeType"] = MimeType,
        ["aspectRatio"] = AspectRatio,
        ["subject"] = Subject.ToJSON(),
        ["licence"] = Licence?.ToJSON(),
        ["source"] = Source,
        ["sourceUrl"] = SourceUrl,
        ["requester"] = Requester,
        ["fetchedUtc"] = FetchedUtc,
        ["fromCache"] = FromCache,
        ["failureName"] = FailureName,
        ["error"] = Error,
        ["remedy"] = Remedy,
        ["retryable"] = Retryable,
        ["creditLine"] = CreditLine(),
    };

    /// <summary>Base64 data URI, for handing straight to <c>Skia.Image.fromDataUrl</c>.</summary>
    public string ToDataUri() => Bytes.Length > 0
        ? $"data:{MimeType};base64,{Convert.ToBase64String(Bytes)}"
        : string.Empty;

    /// <summary>
    /// A caption-ready credit, e.g. <c>Photo: Harald Krichel, CC BY-SA 4.0</c>.
    /// </summary>
    /// <remarks>
    /// Omits what the source did not supply rather than substituting a placeholder, the same
    /// discipline as <c>ParallelCitation.Format</c>. Returns an empty string when nothing is known,
    /// so a layout can test it rather than printing <c>Photo: unknown</c> under a picture.
    /// </remarks>
    public string CreditLine()
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(Licence?.Artist)) parts.Add(Licence.Artist!);
        if (!string.IsNullOrWhiteSpace(Licence?.Name)) parts.Add(Licence.Name!);
        return parts.Count == 0 ? string.Empty : "Photo: " + string.Join(", ", parts);
    }
    #endregion
}

#endregion

#region Budget

/// <summary>
/// Remaining photograph allowance, visible to scripts.
/// </summary>
/// <remarks>
/// Readable rather than a hidden cap that throws, for the reason recorded on <c>AssetBudget</c>: an
/// agent that can see a dwindling resource forms a strategy, and one that hits a silent wall behaves
/// incoherently. The unit is a <i>delivered photograph</i>, not an HTTP call — resolution costs
/// nothing against the allowance, so an agent may check identity as often as it likes and is charged
/// only for bytes.
/// </remarks>
public sealed class PhotoBudget(int total)
{
    public int Total { get; } = total;

    public int Spent { get; internal set; }

    public int Remaining => Math.Max(0, Total - Spent);

    /// <summary>Deliveries served from the session cache, which cost nothing and are not charged.</summary>
    public int CacheHits { get; internal set; }

    /// <summary>Bytes actually pulled over the wire, which is the cost the host cares about.</summary>
    public long BytesFetched { get; internal set; }

    public bool CanAfford(int count = 1) => Remaining >= count;
}

#endregion

#region Failures

/// <summary>Why a photograph was not delivered.</summary>
public enum PhotoFailure
{
    None = 0,

    /// <summary>No source is configured. A script cannot recover; the host must wire one up.</summary>
    NotConfigured = 1,

    /// <summary>The allowance is gone. Draw the subject or drop it; retrying will not help.</summary>
    BudgetExhausted = 2,

    /// <summary>Nothing matched the query at all.</summary>
    NotFound = 3,

    /// <summary>The query is ambiguous — it landed on a disambiguation page. Qualify it.</summary>
    Ambiguous = 4,

    /// <summary>Something matched, but its description failed <see cref="PhotoOptions.Expect"/>.</summary>
    WrongSubject = 5,

    /// <summary>The subject resolved but its page carries no lead photograph.</summary>
    NoImage = 6,

    /// <summary>The source states no licence, and <see cref="PhotoOptions.AllowUnstatedLicence"/> is off.</summary>
    LicenceUnstated = 7,

    /// <summary>A licence was stated but is not in <see cref="PhotoOptions.RequireLicence"/>.</summary>
    LicenceNotAllowed = 8,

    /// <summary>
    /// The bytes arrived but are not a picture this studio can decode.
    /// </summary>
    /// <remarks>
    /// Its own failure rather than an empty result, per <c>CLAUDE.md</c> §0 on untrusted binary: a
    /// silently blank asset reads downstream as a deliberate blank.
    /// </remarks>
    Undecodable = 9,

    /// <summary>The media URL pointed somewhere the allowlist does not cover.</summary>
    BlockedHost = 10,

    /// <summary>Throttled. The same request later is correct.</summary>
    RateLimited = 11,

    /// <summary>The request never reached the source.</summary>
    Network = 12,

    Timeout = 13,

    /// <summary>The source answered with an error.</summary>
    ServiceError = 14,

    Cancelled = 15,
}

/// <summary>Remedies and retry classification for <see cref="PhotoFailure"/>.</summary>
public static class PhotoFailures
{
    /// <summary>What to do next, addressed to the agent that will read it.</summary>
    public static string RemedyFor(PhotoFailure failure) => failure switch
    {
        PhotoFailure.None => "No failure.",
        PhotoFailure.NotConfigured =>
            "No photograph source is configured, so reference photography is unavailable for this run. "
            + "Draw the subject, or represent it without a likeness. Do not invent an image URL.",
        PhotoFailure.BudgetExhausted =>
            "The photograph allowance for this run is spent. Reuse something from Photo.library, "
            + "or draw the subject instead.",
        PhotoFailure.NotFound =>
            "Nothing matched. Check the spelling, or try the subject's full formal name.",
        PhotoFailure.Ambiguous =>
            "The name is ambiguous and resolved to a disambiguation page, so no single subject was "
            + "identified. Qualify it — see subject.alternatives for what the source offered.",
        PhotoFailure.WrongSubject =>
            "A subject was found but its description did not contain the word you required, so this "
            + "is probably a different person or place of the same name. Check subject.description "
            + "and subject.alternatives, then qualify the query or relax 'expect'.",
        PhotoFailure.NoImage =>
            "The subject exists but its page carries no lead photograph. There is nothing to fetch; "
            + "draw the subject or choose another.",
        PhotoFailure.LicenceUnstated =>
            "The source states no licence for this file, so it cannot be credited or recorded. "
            + "Choose another subject rather than using it.",
        PhotoFailure.LicenceNotAllowed =>
            "The file's licence is not among the ones you allowed. Relax 'requireLicence' if the "
            + "obligation is acceptable for this piece, or choose another subject.",
        PhotoFailure.Undecodable =>
            "The bytes that arrived are not a decodable image. Treat this as no photograph at all — "
            + "do not draw a blank in its place.",
        PhotoFailure.BlockedHost =>
            "The media URL is not on the allowed host list, so nothing was fetched. This is a "
            + "configuration boundary, not something to work around.",
        PhotoFailure.RateLimited => "Throttled by the source. The same request will work shortly.",
        PhotoFailure.Network => "The source was not reachable. Retrying is reasonable.",
        PhotoFailure.Timeout => "The source did not answer in time. Retrying is reasonable.",
        PhotoFailure.ServiceError => "The source returned an error. Retrying is reasonable.",
        PhotoFailure.Cancelled => "The request was cancelled.",
        _ => "Unclassified failure.",
    };

    /// <summary>Whether repeating the identical request could plausibly succeed.</summary>
    /// <remarks>
    /// False for everything decided locally or by the subject's own data: a refused licence, a
    /// missing lead image and an ambiguous name all return the same answer however many times they
    /// are asked. Only transport faults are worth repeating.
    /// </remarks>
    public static bool IsRetryable(PhotoFailure failure) => failure
        is PhotoFailure.RateLimited
        or PhotoFailure.Network
        or PhotoFailure.Timeout
        or PhotoFailure.ServiceError;
}

#endregion
