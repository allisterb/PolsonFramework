namespace Polson.ExtendedMind.Photos;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using SkiaSharp;

/// <summary>
/// Reference photographs from a MediaWiki wiki and its shared media repository.
/// </summary>
/// <remarks>
/// <para>
/// Two unauthenticated calls resolve a bare name to a photograph and its terms:
/// <c>generator=search</c> with <c>prop=pageimages</c> finds the subject's article and its lead
/// image in one request, and a batched <c>prop=imageinfo</c> returns the rendition URL alongside the
/// licence, the photographer and any non-copyright restriction.
/// </para>
/// <para>
/// <b>Why this source rather than an image search.</b> It is the only one measured that returns the
/// terms with the bytes. Google's Custom Search JSON API can filter by licence but its result schema
/// carries no licence field, so a filtered result still cannot be credited or entered in a ledger;
/// Parallel's extract drops <c>src</c> entirely and returns only the file's description-page link.
/// The terms are the deliverable here as much as the pixels are.
/// </para>
/// <para>
/// No API key. Wikimedia's policy requires a descriptive <c>User-Agent</c> carrying contact
/// information and serves a 403 to a generic one, so <see cref="DefaultUserAgent"/> is a real
/// identifier rather than a placeholder — change the contact when this is deployed anywhere.
/// </para>
/// </remarks>
public sealed partial class WikimediaPhotoSource : Runtime, IPhotoSource, IDisposable
{
    #region Constructors
    /// <param name="httpClient">Optional. When supplied it is neither mutated nor disposed.</param>
    /// <param name="apiUrlTemplate">
    /// Overridable for a test double. <c>{lang}</c> is replaced with <see cref="PhotoOptions.Language"/>.
    /// </param>
    /// <param name="allowedMediaHosts">
    /// Hosts bytes may be fetched from. Defaults to Wikimedia's upload host only.
    /// </param>
    /// <param name="userAgent">Sent on every request. Wikimedia refuses a generic one.</param>
    public WikimediaPhotoSource(
        HttpClient? httpClient = null,
        string? apiUrlTemplate = null,
        IEnumerable<string>? allowedMediaHosts = null,
        string? userAgent = null)
    {
        this.template = apiUrlTemplate ?? DefaultApiTemplate;
        this.userAgent = string.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent : userAgent;
        this.allowedHosts = new HashSet<string>(
            allowedMediaHosts ?? DefaultMediaHosts, StringComparer.OrdinalIgnoreCase);
        this.ownsHttp = httpClient is null;

        // A client we own carries no ambient timeout; every call sets its own deadline.
        this.http = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }
    #endregion

    #region Properties
    public const string DefaultApiTemplate = "https://{lang}.wikipedia.org/w/api.php";

    /// <summary>Wikimedia serves a 403 to a generic agent, so this must stay descriptive.</summary>
    public const string DefaultUserAgent = "PolsonStudio/0.1 (co-creative art studio; https://github.com/allisterb/Polson)";

    /// <summary>
    /// Where bytes may be fetched from.
    /// </summary>
    /// <remarks>
    /// An allowlist rather than a scheme check because this fetcher is reachable, indirectly, from
    /// visitor-supplied text on the demo site (Milestone 6 §3). A URL is data returned by a remote
    /// service; the set of hosts we will dereference is ours.
    /// </remarks>
    /// <remarks>
    /// <b>Both hosts are needed, and the second was found the hard way.</b> Wikimedia serves
    /// originals from <c>upload.wikimedia.org</c> and now serves rendered thumbnails — which is what
    /// a <c>width</c> request returns, so it is the common case rather than the rare one — from
    /// <c>thumb.wikimedia.org</c>. With only the first, <c>Photo.of</c> resolved the subject, found
    /// the right image, and then refused to fetch its own URL: a live run lost its portrait and spent
    /// two scripts retrying under different descriptors, because the block is on the host and no
    /// change to the name can move it.
    /// <para>
    /// Named hosts rather than a <c>*.wikimedia.org</c> suffix match on purpose. The list is a
    /// security boundary — this fetcher is reachable, indirectly, from visitor-supplied text
    /// (Milestone 6 §3) — and a suffix rule would admit every present and future subdomain of a wiki
    /// farm that lets the public name things. Add hosts as they are observed, one at a time.
    /// </para>
    /// </remarks>
    public static readonly string[] DefaultMediaHosts = ["upload.wikimedia.org", "thumb.wikimedia.org"];

    /// <summary>Deadline for one API call or one byte fetch.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Largest body accepted from the media host. A rendition of a few hundred KB is normal.</summary>
    public const int MaxImageBytes = 24 * 1024 * 1024;

    public string Name => "wikimedia";
    #endregion

    #region Methods
    /// <inheritdoc/>
    public async Task<SubjectMatch> Resolve(
        string subject, PhotoOptions options, CancellationToken cancellationToken = default)
    {
        var query = (subject ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return Failed(query, PhotoFailure.NotFound, "No subject was given.");
        }

        options ??= new PhotoOptions();
        var width = ClampWidth(options.Width);

        // One call for the article and its lead image. gsrlimit=3 buys the runners-up, which are
        // what make a wrong resolution visible rather than silent.
        var search = await Get(options.Language, new Dictionary<string, string>
        {
            ["action"] = "query",
            ["generator"] = "search",
            ["gsrsearch"] = query,
            ["gsrlimit"] = "3",
            ["prop"] = "pageimages|pageprops|description",
            ["piprop"] = "thumbnail|original|name",
            ["pithumbsize"] = width.ToString(),
        }, cancellationToken);

        if (search.Failure != PhotoFailure.None)
        {
            return Failed(query, search.Failure, search.Error);
        }

        using var document = search.Document!;
        var pages = Pages(document.RootElement);
        if (pages.Count == 0)
        {
            return Failed(query, PhotoFailure.NotFound, $"Nothing matched '{query}'.");
        }

        // The search generator returns pages in arbitrary object order; "index" carries the ranking.
        var ranked = pages.OrderBy(p => Int(p, "index", int.MaxValue)).ToList();
        var top = ranked[0];
        var alternatives = ranked.Skip(1).Select(p => Str(p, "title") ?? string.Empty)
            .Where(t => t.Length > 0).ToArray();

        var title = Str(top, "title");
        var description = Str(top, "description");
        var disambiguation = top.TryGetProperty("pageprops", out var props)
            && props.ValueKind == JsonValueKind.Object
            && props.TryGetProperty("disambiguation", out _);

        var partial = new SubjectMatch
        {
            Query = query,
            Title = title,
            Description = description,
            IsDisambiguation = disambiguation,
            Alternatives = alternatives,
        };

        if (disambiguation)
        {
            return partial with
            {
                Failure = PhotoFailure.Ambiguous,
                Error = $"'{query}' resolved to the disambiguation page '{title}'.",
            };
        }

        // The identity gate. Cheap, and it catches the failure that renders perfectly.
        if (!string.IsNullOrWhiteSpace(options.Expect)
            && (description is null
                || description.IndexOf(options.Expect, StringComparison.OrdinalIgnoreCase) < 0))
        {
            return partial with
            {
                Failure = PhotoFailure.WrongSubject,
                Error = $"'{title}' is described as '{description ?? "(no description)"}', "
                      + $"which does not mention '{options.Expect}'.",
            };
        }

        var pageImage = Str(top, "pageimage");
        if (string.IsNullOrWhiteSpace(pageImage))
        {
            return partial with
            {
                Failure = PhotoFailure.NoImage,
                Error = $"'{title}' carries no lead photograph.",
            };
        }

        var file = "File:" + pageImage;
        var info = await ImageInfo(file, width, options.Language, cancellationToken);
        if (info.Failure != PhotoFailure.None)
        {
            return partial with { File = file, Failure = info.Failure, Error = info.Error };
        }

        return partial with
        {
            Success = true,
            File = file,
            Licence = info.Licence,
            ImageUrl = info.Url,
            SourceWidth = info.Width,
            SourceHeight = info.Height,
        };
    }

    /// <inheritdoc/>
    public async Task<PhotoFetch> Fetch(
        SubjectMatch match, PhotoOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (string.IsNullOrWhiteSpace(match.ImageUrl))
        {
            return new PhotoFetch
            {
                Failure = PhotoFailure.NoImage,
                Error = "The match carries no image URL, so there is nothing to fetch.",
            };
        }

        if (!Uri.TryCreate(match.ImageUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !allowedHosts.Contains(uri.Host))
        {
            return new PhotoFetch
            {
                Failure = PhotoFailure.BlockedHost,
                Error = $"'{match.ImageUrl}' is not on the allowed media host list "
                      + $"({string.Join(", ", allowedHosts)}).",
            };
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

            using var response = await http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new PhotoFetch
                {
                    Failure = Classify(response.StatusCode),
                    Error = $"HTTP {(int)response.StatusCode} fetching {uri}.",
                };
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
            if (bytes.Length > MaxImageBytes)
            {
                return new PhotoFetch
                {
                    Failure = PhotoFailure.Undecodable,
                    Error = $"{bytes.Length} bytes exceeds the {MaxImageBytes}-byte ceiling.",
                };
            }

            // Decode rather than trust the extension or the Content-Type. Both are claims made by
            // the source about bytes we did not produce; a decode is the only thing that establishes
            // this is a picture, and it yields the true dimensions in the same pass.
            //
            // Guarded, because SKBitmap.Decode does NOT return null for undecodable input as its
            // signature suggests: on SkiaSharp 4.148 an HTML error page fed to it throws
            // ArgumentNullException("codec") from inside the decoder. Letting that escape would turn
            // the one case this check exists for — a bad file from the open web — into a thrown
            // exception that ends the script, which is the opposite of the intent.
            SKBitmap? bitmap = null;
            try
            {
                bitmap = SKBitmap.Decode(bytes);
                if (bitmap is null || bitmap.Width == 0 || bitmap.Height == 0)
                {
                    return Undecodable(bytes.Length, uri.Host, null);
                }

                return Delivered(bytes, bitmap, response, uri);
            }
            catch (Exception ex)
            {
                return Undecodable(bytes.Length, uri.Host, ex.Message);
            }
            finally
            {
                bitmap?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new PhotoFetch { Failure = PhotoFailure.Cancelled, Error = "Cancelled by the caller." };
        }
        catch (OperationCanceledException)
        {
            return new PhotoFetch { Failure = PhotoFailure.Timeout, Error = "The media host did not answer in time." };
        }
        catch (HttpRequestException ex)
        {
            return new PhotoFetch { Failure = PhotoFailure.Network, Error = ex.Message };
        }

        static PhotoFetch Undecodable(int length, string host, string? detail) => new()
        {
            Failure = PhotoFailure.Undecodable,
            Error = $"{length} bytes from {host} did not decode as an image"
                  + (detail is null ? "." : $": {detail}"),
        };

        static PhotoFetch Delivered(byte[] bytes, SKBitmap bitmap, HttpResponseMessage response, Uri uri) => new()
        {
            Success = true,
            Bytes = bytes,
            Width = bitmap.Width,
            Height = bitmap.Height,
            MimeType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg",
            SourceUrl = uri.ToString(),
        };
    }

    public void Dispose()
    {
        if (ownsHttp) http.Dispose();
    }

    /// <summary>Delivered width, held inside what a rendition is useful at.</summary>
    public static int ClampWidth(int width) =>
        Math.Clamp(width <= 0 ? Photographs.DefaultWidth : width, Photographs.MinWidth, Photographs.MaxWidth);
    #endregion

    #region Fields
    private readonly HttpClient http;
    private readonly string template;
    private readonly string userAgent;
    private readonly bool ownsHttp;
    private readonly HashSet<string> allowedHosts;
    #endregion

    #region Child types
    private sealed record ApiResult(JsonDocument? Document, PhotoFailure Failure, string? Error);

    private sealed record ImageInfoResult(
        PhotoLicence? Licence, string? Url, int Width, int Height, PhotoFailure Failure, string? Error);
    #endregion

    #region Private methods
    /// <summary>The licence half. Batched by title, though the toolkit only ever asks for one.</summary>
    private async Task<ImageInfoResult> ImageInfo(
        string file, int width, string language, CancellationToken cancellationToken)
    {
        var result = await Get(language, new Dictionary<string, string>
        {
            ["action"] = "query",
            ["titles"] = file,
            ["prop"] = "imageinfo",
            ["iiprop"] = "url|size|mime|extmetadata",
            ["iiurlwidth"] = width.ToString(),
        }, cancellationToken);

        if (result.Failure != PhotoFailure.None)
        {
            return new ImageInfoResult(null, null, 0, 0, result.Failure, result.Error);
        }

        using var document = result.Document!;
        var pages = Pages(document.RootElement);

        // MediaWiki normalises underscores to spaces on the way back, so a page keyed on the title
        // we SENT misses every multi-word filename — which is most of them. Measured during the
        // feasibility probe: six of seven subjects reported "no imageinfo" until this was fixed.
        var wanted = Normalise(file);
        var page = pages.FirstOrDefault(p => string.Equals(Normalise(Str(p, "title") ?? string.Empty),
            wanted, StringComparison.OrdinalIgnoreCase));

        if (page.ValueKind != JsonValueKind.Object
            || !page.TryGetProperty("imageinfo", out var infos)
            || infos.ValueKind != JsonValueKind.Array
            || infos.GetArrayLength() == 0)
        {
            return new ImageInfoResult(null, null, 0, 0, PhotoFailure.NoImage,
                $"The source returned no file information for {file}.");
        }

        var info = infos[0];
        var meta = info.TryGetProperty("extmetadata", out var m) && m.ValueKind == JsonValueKind.Object
            ? m
            : default;

        var licence = new PhotoLicence
        {
            Name = Meta(meta, "LicenseShortName"),
            UsageTerms = Meta(meta, "UsageTerms"),
            Artist = Meta(meta, "Artist"),
            Credit = Meta(meta, "Credit"),
            AttributionRequired = Bool(Meta(meta, "AttributionRequired")),
            Restrictions = Meta(meta, "Restrictions"),
            DescriptionUrl = Str(info, "descriptionurl"),
        };

        // Prefer the rendition: an original can be 13 MB, and for an SVG source the rendition is a
        // rasterised PNG, which is the only form Skia will decode.
        var url = Str(info, "thumburl") ?? Str(info, "url");

        return new ImageInfoResult(licence, url, Int(info, "width", 0), Int(info, "height", 0),
            PhotoFailure.None, null);
    }

    private async Task<ApiResult> Get(
        string language, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        parameters["format"] = "json";
        parameters["formatversion"] = "2";

        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        var query = string.Join("&", parameters.Select(
            p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var url = template.Replace("{lang}", Uri.EscapeDataString(lang)) + "?" + query;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

            using var response = await http.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult(null, Classify(response.StatusCode),
                    $"HTTP {(int)response.StatusCode} from the wiki API.");
            }

            return new ApiResult(JsonDocument.Parse(body), PhotoFailure.None, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ApiResult(null, PhotoFailure.Cancelled, "Cancelled by the caller.");
        }
        catch (OperationCanceledException)
        {
            return new ApiResult(null, PhotoFailure.Timeout, "The wiki API did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            return new ApiResult(null, PhotoFailure.Network, ex.Message);
        }
        catch (JsonException ex)
        {
            return new ApiResult(null, PhotoFailure.ServiceError, $"Unparseable response: {ex.Message}");
        }
    }

    private static PhotoFailure Classify(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests => PhotoFailure.RateLimited,
        >= HttpStatusCode.InternalServerError => PhotoFailure.ServiceError,
        HttpStatusCode.NotFound => PhotoFailure.NotFound,
        _ => PhotoFailure.ServiceError,
    };

    private static SubjectMatch Failed(string query, PhotoFailure failure, string? error) =>
        new() { Query = query, Failure = failure, Error = error };

    private static List<JsonElement> Pages(JsonElement root) =>
        root.TryGetProperty("query", out var q)
        && q.ValueKind == JsonValueKind.Object
        && q.TryGetProperty("pages", out var pages)
        && pages.ValueKind == JsonValueKind.Array
            ? [.. pages.EnumerateArray()]
            : [];

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Int(JsonElement element, string name, int fallback) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : fallback;

    /// <summary>One <c>extmetadata</c> field, with the markup the wiki wraps it in stripped.</summary>
    private static string? Meta(JsonElement meta, string name)
    {
        if (meta.ValueKind != JsonValueKind.Object
            || !meta.TryGetProperty(name, out var field)
            || field.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var raw = Str(field, "value");
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = WebUtility.HtmlDecode(TagRegex().Replace(raw, " ")).Trim();
        text = WhitespaceRegex().Replace(text, " ");
        return text.Length == 0 ? null : text;
    }

    /// <summary>The wiki spells these as <c>true</c>/<c>false</c> strings, not JSON booleans.</summary>
    private static bool? Bool(string? value) => value is null
        ? null
        : bool.TryParse(value, out var parsed) ? parsed : null;

    private static string Normalise(string title) => title.Replace('_', ' ');

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
    #endregion
}
