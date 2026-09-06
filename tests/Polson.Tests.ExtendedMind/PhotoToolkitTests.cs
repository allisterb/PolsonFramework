namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using global::Polson;
using global::Polson.ExtendedMind.Photos;
using global::Polson.Tests;

using SkiaSharp;

using Xunit;

/// <summary>
/// Tests for the reference-photograph surface.
/// </summary>
/// <remarks>
/// <para>
/// The gates are the feature, so most of this is about what does <b>not</b> get delivered: an
/// ambiguous name, a subject whose description contradicts what the caller expected, a file with no
/// stated licence, a licence outside what was allowed, bytes that do not decode, and a media URL off
/// the allowlist. Each of those is a way to end up with a plausible-looking wrong picture, and each
/// costs nothing when it is caught before the fetch.
/// </para>
/// <para>
/// Everything is offline except the Live region, gated behind <c>POLSON_LIVE_PHOTO_TESTS=1</c>.
/// The wiki API is free and unmetered, but a test suite should not depend on a third party being up.
/// </para>
/// </remarks>
public class PhotoToolkitTests : TestsRuntime
{
    #region Resolution and identity
    /// <summary>
    /// The regression the feasibility probe found: MediaWiki normalises underscores to spaces in the
    /// titles it returns, so matching the <c>imageinfo</c> page against the title we <i>sent</i>
    /// misses every multi-word filename. Six of seven probe subjects failed this way, and the
    /// symptom — "the source returned no file information" — points nowhere near the cause.
    /// </summary>
    [Fact]
    public async Task ResolveMatchesFileTitlesAcrossUnderscoreNormalisation()
    {
        var source = Source(Wiki(
            search: Search("Margot Robbie", "Australian actress and producer (born 1990)",
                pageImage: "Margot_Robbie_at_the_premiere.jpg"),
            // Returned with spaces, as the real API does.
            imageInfo: ImageInfo("File:Margot Robbie at the premiere.jpg", "CC0", "Ondine Goat")));

        var match = await source.Resolve("Margot Robbie", new PhotoOptions());

        Assert.True(match.Success, match.Error);
        Assert.Equal("File:Margot_Robbie_at_the_premiere.jpg", match.File);
        Assert.Equal("CC0", match.Licence?.Name);
    }

    /// <summary>A disambiguation page is never a subject, and the runners-up are the remedy.</summary>
    [Fact]
    public async Task AmbiguousNameIsRefusedAndCarriesTheAlternatives()
    {
        var source = Source(Wiki(search: Disambiguation(
            "Georgia", ["Georgia (country)", "Georgia (U.S. state)"])));

        var match = await source.Resolve("Georgia", new PhotoOptions());

        Assert.False(match.Success);
        Assert.Equal(PhotoFailure.Ambiguous, match.Failure);
        Assert.True(match.IsDisambiguation);
        Assert.Equal(["Georgia (country)", "Georgia (U.S. state)"], match.Alternatives);
        Assert.Contains("alternatives", match.Remedy);
    }

    /// <summary>
    /// The identity gate. A musician of the same name renders as convincingly as the actress, so the
    /// caller's expectation is checked against the subject's own description.
    /// </summary>
    [Fact]
    public async Task ExpectationRefusesAWrongSubjectOfTheSameName()
    {
        var source = Source(Wiki(search: Search(
            "Jordan Baker", "British racing driver (born 1991)", pageImage: "Baker.jpg")));

        var match = await source.Resolve("Jordan Baker", new PhotoOptions { Expect = "actress" });

        Assert.False(match.Success);
        Assert.Equal(PhotoFailure.WrongSubject, match.Failure);
        Assert.Contains("racing driver", match.Error);
        Assert.False(match.Retryable);   // asking again returns the same driver
    }

    /// <summary>The same subject passes when the expectation actually holds.</summary>
    [Fact]
    public async Task ExpectationPassesWhenTheDescriptionAgrees()
    {
        var source = Source(Wiki(
            search: Search("Sydney Sweeney", "American actress (born 1997)", pageImage: "Sweeney.jpg"),
            imageInfo: ImageInfo("File:Sweeney.jpg", "CC BY-SA 4.0", "Jay Dixit", restrictions: "personality")));

        var match = await source.Resolve("Sydney Sweeney", new PhotoOptions { Expect = "Actress" });

        Assert.True(match.Success, match.Error);
        Assert.Equal("personality", match.Licence?.Restrictions);
    }

    /// <summary>A subject with no lead photograph is a dead end, not a retry.</summary>
    [Fact]
    public async Task SubjectWithoutALeadImageReportsNoImage()
    {
        var source = Source(Wiki(search: Search("Some Concept", "An abstract idea", pageImage: null)));

        var match = await source.Resolve("Some Concept", new PhotoOptions());

        Assert.Equal(PhotoFailure.NoImage, match.Failure);
        Assert.False(match.Retryable);
    }

    /// <summary>An empty result set is <see cref="PhotoFailure.NotFound"/>, not an exception.</summary>
    [Fact]
    public async Task NoSearchHitReportsNotFound()
    {
        var source = Source(Wiki(search: """{"batchcomplete":true}"""));

        var match = await source.Resolve("qwertyuiop asdfgh", new PhotoOptions());

        Assert.Equal(PhotoFailure.NotFound, match.Failure);
    }
    #endregion

    #region Licence gates
    /// <summary>An unstated licence cannot be credited or recorded, so it is refused by default.</summary>
    [Fact]
    public async Task UnstatedLicenceIsRefusedByDefault()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Nobody", "A person", pageImage: "Nobody.jpg"),
            imageInfo: ImageInfo("File:Nobody.jpg", licence: null, artist: null)));

        var photo = await toolkit.Of("Nobody");

        Assert.False(photo.Success);
        Assert.Equal(PhotoFailure.LicenceUnstated, photo.Failure);
        Assert.Equal(0, toolkit.Budget.Spent);   // refused before anything was charged
    }

    /// <summary>...and delivered when the caller takes that decision explicitly.</summary>
    [Fact]
    public async Task UnstatedLicenceIsDeliveredWhenExplicitlyAllowed()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Nobody", "A person", pageImage: "Nobody.jpg"),
            imageInfo: ImageInfo("File:Nobody.jpg", licence: null, artist: null),
            image: Png(40, 50)));

        var photo = await toolkit.Of("Nobody", new PhotoOptions { AllowUnstatedLicence = true });

        Assert.True(photo.Success, photo.Error);
        Assert.Equal(string.Empty, photo.CreditLine());   // nothing known, so nothing claimed
    }

    /// <summary>
    /// The share-alike distinction, which a bare <c>StartsWith</c> gets wrong: <c>"CC BY-SA 4.0"</c>
    /// does start with <c>"CC BY"</c>, so a caller who allowed <c>CC BY</c> precisely to avoid the
    /// obligation would have been handed exactly what they excluded. The match is at a word boundary.
    /// </summary>
    [Theory]
    [InlineData("CC BY 3.0", true)]
    [InlineData("CC0", true)]
    [InlineData("CC BY-SA 4.0", false)]
    [InlineData("CC BY-NC 2.0", false)]
    public async Task RequiredLicencesAreMatchedByPrefix(string licence, bool allowed)
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", licence, "A Photographer"),
            image: Png(40, 50)));

        var photo = await toolkit.Of("Subject", new PhotoOptions { RequireLicence = ["CC0", "CC BY"] });

        Assert.Equal(allowed, photo.Success);
        if (!allowed)
        {
            Assert.Equal(PhotoFailure.LicenceNotAllowed, photo.Failure);
            Assert.Contains(licence, photo.Error);
            Assert.Equal(0, toolkit.Budget.Spent);
        }
    }

    /// <summary>The credit is assembled from what the source stated, omitting what it did not.</summary>
    [Fact]
    public async Task CreditLineNamesThePhotographerAndTheLicence()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Zendaya", "American actress, singer, and dancer (born 1996)", pageImage: "Z.jpg"),
            imageInfo: ImageInfo("File:Z.jpg", "CC BY-SA 4.0", "PhilipRomano"),
            image: Png(60, 90)));

        var photo = await toolkit.Of("Zendaya");

        Assert.True(photo.Success, photo.Error);
        Assert.Equal("Photo: PhilipRomano, CC BY-SA 4.0", photo.CreditLine());
        Assert.Equal(["Photo: PhilipRomano, CC BY-SA 4.0"], toolkit.Credits());
    }

    /// <summary>Wiki metadata arrives wrapped in markup; the fields are plain text by the time a script sees them.</summary>
    [Fact]
    public async Task LicenceFieldsAreStrippedOfMarkup()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC BY 3.0",
                artist: "<a href='//example.org' title='x'>dvna creative agency</a>"),
            image: Png(40, 50)));

        var photo = await toolkit.Of("Subject");

        Assert.Equal("dvna creative agency", photo.Licence?.Artist);
    }
    #endregion

    #region Budget and cache
    /// <summary>An exhausted allowance refuses rather than fetching, and says so in words.</summary>
    [Fact]
    public async Task ExhaustedBudgetRefusesBeforeFetching()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC0", "Someone"),
            image: Png(40, 50)), total: 0);

        var photo = await toolkit.Of("Subject");

        Assert.Equal(PhotoFailure.BudgetExhausted, photo.Failure);
        Assert.False(photo.Retryable);
        Assert.Contains("allowance", photo.Remedy);
    }

    /// <summary>
    /// A second identical request is served from the session cache: no request, no charge, and
    /// <c>fromCache</c> set so a script can tell.
    /// </summary>
    [Fact]
    public async Task RepeatedRequestIsServedFromCacheAndNotCharged()
    {
        var handler = Wiki(
            search: Search("Zendaya", "American actress (born 1996)", pageImage: "Z.jpg"),
            imageInfo: ImageInfo("File:Z.jpg", "CC BY-SA 4.0", "PhilipRomano"),
            image: Png(60, 90));
        var toolkit = Toolkit(handler);

        var first = await toolkit.Of("Zendaya");
        var imageRequests = handler.ImageRequests;
        var second = await toolkit.Of("Zendaya");

        Assert.True(first.Success && second.Success);
        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(1, toolkit.Budget.Spent);
        Assert.Equal(1, toolkit.Budget.CacheHits);
        Assert.Equal(imageRequests, handler.ImageRequests);   // nothing was fetched the second time
        Assert.Single(toolkit.Library);
    }

    /// <summary>Resolution is free, so an agent may check identity as often as it likes.</summary>
    [Fact]
    public async Task ResolutionIsNeverCharged()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC0", "Someone")), total: 1);

        for (var i = 0; i < 5; i++) await toolkit.Resolve("Subject");

        Assert.Equal(0, toolkit.Budget.Spent);
        Assert.True(toolkit.Budget.CanAfford());
    }

    /// <summary>
    /// A failed fetch still spent the host's request, so it is still charged. An allowance that
    /// counted only successes could be overrun without limit by an unlucky run.
    /// </summary>
    [Fact]
    public async Task FailedFetchIsStillCharged()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC0", "Someone"),
            image: [1, 2, 3, 4]));   // not a picture

        var photo = await toolkit.Of("Subject");

        Assert.Equal(PhotoFailure.Undecodable, photo.Failure);
        Assert.Equal(1, toolkit.Budget.Spent);
    }
    #endregion

    #region Untrusted bytes and hosts
    /// <summary>
    /// Bytes that do not decode are a named failure, never an empty picture. Per <c>CLAUDE.md</c> §0
    /// a silently blank asset reads downstream as a deliberate blank.
    /// </summary>
    [Fact]
    public async Task UndecodableBytesAreAFailureNotABlankImage()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC0", "Someone"),
            image: Encoding.UTF8.GetBytes("<html>404 not found</html>")));

        var photo = await toolkit.Of("Subject");

        Assert.False(photo.Success);
        Assert.Equal(PhotoFailure.Undecodable, photo.Failure);
        Assert.Empty(photo.Bytes);
        Assert.Equal(0, photo.Width);
        Assert.Contains("do not draw a blank", photo.Remedy);
    }

    /// <summary>
    /// A URL is data returned by a remote service. The set of hosts this studio will dereference is
    /// ours, and it is checked even though the URL came from the source we asked.
    /// </summary>
    [Fact]
    public async Task MediaHostOutsideTheAllowlistIsNotFetched()
    {
        var handler = Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC0", "Someone",
                thumbUrl: "https://images.example.net/S.jpg"),
            image: Png(40, 50));
        var toolkit = Toolkit(handler);

        var photo = await toolkit.Of("Subject");

        Assert.Equal(PhotoFailure.BlockedHost, photo.Failure);
        Assert.Equal(0, handler.ImageRequests);
        Assert.Contains("images.example.net", photo.Error);
    }

    /// <summary>The delivered size is read from the decoded picture, not from what the source claimed.</summary>
    [Fact]
    public async Task DeliveredSizeIsMeasuredFromTheDecodedImage()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC0", "Someone", width: 9999, height: 1),
            image: Png(64, 96)));

        var photo = await toolkit.Of("Subject");

        Assert.Equal(64, photo.Width);
        Assert.Equal(96, photo.Height);
        Assert.Equal(64d / 96d, photo.AspectRatio, 5);
    }
    #endregion

    #region Availability and the run record
    /// <summary>With no source configured every call refuses, and says not to invent a URL.</summary>
    [Fact]
    public async Task UnconfiguredToolkitRefusesEverything()
    {
        var toolkit = new PhotoToolkit(null, new PhotoBudget(5));

        var photo = await toolkit.Of("Zendaya");
        var match = await toolkit.Resolve("Zendaya");

        Assert.False(toolkit.IsAvailable);
        Assert.Equal(PhotoFailure.NotConfigured, photo.Failure);
        Assert.Equal(PhotoFailure.NotConfigured, match.Failure);
        Assert.Contains("Do not invent an image URL", photo.Remedy);
    }

    /// <summary>
    /// A refusal and a fetch failure are different events in the run record. A run that spent its
    /// time rewording queries is a different run from one the network kept dropping.
    /// </summary>
    [Fact]
    public async Task RefusalsAndDeliveriesAreRecordedDistinctly()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Zendaya", "American actress (born 1996)", pageImage: "Z.jpg"),
            imageInfo: ImageInfo("File:Z.jpg", "CC BY-SA 4.0", "PhilipRomano"),
            image: Png(60, 90)));

        using var scope = RequisitionScope.Begin();
        await toolkit.Of("Zendaya", new PhotoOptions { Expect = "novelist" });   // refused
        await toolkit.Of("Zendaya");                                             // delivered

        var records = scope.Records;
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal("photo", r.Kind));

        Assert.True(records[0].Refused);
        Assert.False(records[0].Success);
        Assert.Equal(nameof(PhotoFailure.WrongSubject), records[0].Failure);

        Assert.False(records[1].Refused);
        Assert.True(records[1].Success);
        Assert.Equal("wikimedia", records[1].Model);
    }

    /// <summary>
    /// The photo budget must not be pushed into the scope's budget slot: it is single and
    /// last-one-wins, and it belongs to the generation allowance. Overwriting it would make a run
    /// report that it spent nothing on assets.
    /// </summary>
    [Fact]
    public async Task DeliveryDoesNotOverwriteTheGenerationBudgetSnapshot()
    {
        var toolkit = Toolkit(Wiki(
            search: Search("Subject", "A person", pageImage: "S.jpg"),
            imageInfo: ImageInfo("File:S.jpg", "CC0", "Someone"),
            image: Png(40, 50)));

        using var scope = RequisitionScope.Begin();
        RequisitionScope.RecordBudget(new BudgetSnapshot(20, 7, 13, 2, 4096));
        await toolkit.Of("Subject");

        Assert.Equal(new BudgetSnapshot(20, 7, 13, 2, 4096), scope.Budget);
    }
    #endregion

    #region Live
    /// <summary>
    /// The whole path against the real wiki: a name in, decodable bytes and a stated licence out.
    /// Gated behind <c>POLSON_LIVE_PHOTO_TESTS=1</c> — free and unmetered, but a suite should not
    /// depend on a third party being up.
    /// </summary>
    [Fact]
    public async Task Live_NameResolvesToACreditedPhotograph()
    {
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PHOTO_TESTS") != "1") return;

        using var source = new WikimediaPhotoSource();
        var toolkit = new PhotoToolkit(source, new PhotoBudget(3), "test");

        var photo = await toolkit.Of("Zendaya", new PhotoOptions { Expect = "actress", Width = 400 });

        Assert.True(photo.Success, $"{photo.FailureName}: {photo.Error} — {photo.Remedy}");
        Assert.NotEmpty(photo.Bytes);

        // Width is a request, not a guarantee: the source rounds up to a standard rendition size,
        // so 400 comes back as 500. What must hold is that the reported size is the size of the
        // bytes we actually got, and that "round up" never becomes "send the 13 MB original".
        Assert.True(photo.Width >= 400, $"delivered {photo.Width}px for a 400px request");
        Assert.True(photo.Width <= 1200, $"delivered {photo.Width}px, which is not a rounding up of 400");
        Assert.True(photo.Height > 0);
        Assert.True(photo.Licence?.IsStated, "a delivered photograph always carries a stated licence");
        Assert.NotEqual(string.Empty, photo.CreditLine());
        Info("Live photograph: {Title}, {W}x{H}, {Bytes} bytes, {Credit}",
            photo.Subject.Title!, photo.Width, photo.Height, photo.Bytes.Length, photo.CreditLine());
    }

    /// <summary>The ambiguity case, live: it must refuse rather than return a confident wrong picture.</summary>
    [Fact]
    public async Task Live_AmbiguousNameIsRefused()
    {
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_PHOTO_TESTS") != "1") return;

        using var source = new WikimediaPhotoSource();
        var toolkit = new PhotoToolkit(source, new PhotoBudget(3), "test");

        var photo = await toolkit.Of("Georgia");

        Assert.False(photo.Success);
        Assert.Equal(0, toolkit.Budget.Spent);
        Info("Live ambiguity: {Failure} — alternatives {Alts}",
            photo.FailureName, string.Join(" / ", photo.Subject.Alternatives));
    }
    #endregion

    #region Fixtures
    private const string MediaHost = "https://upload.wikimedia.org/wikipedia/commons/";

    private static WikimediaPhotoSource Source(WikiHandler handler) =>
        new(new HttpClient(handler), "https://{lang}.example.test/w/api.php");

    private static PhotoToolkit Toolkit(WikiHandler handler, int total = 5) =>
        new(Source(handler), new PhotoBudget(total), "test");

    /// <summary>A search response carrying one subject, plus two runners-up.</summary>
    /// <remarks>
    /// Built with a small quoting helper rather than a raw interpolated literal: JSON is mostly
    /// braces, and the brace-doubling rules of <c>$$"""</c> make the intent unreadable well before
    /// the compiler starts complaining.
    /// </remarks>
    private static string Search(string title, string description, string? pageImage)
    {
        var lead = pageImage is null ? string.Empty : ", " + Q("pageimage") + ": " + Q(pageImage);

        return "{ " + Q("batchcomplete") + ": true, " + Q("query") + ": { " + Q("pages") + ": ["
            + "{ " + Q("pageid") + ": 1, " + Q("index") + ": 1, "
            + Q("title") + ": " + Q(title) + ", "
            + Q("description") + ": " + Q(description) + lead + " },"
            + "{ " + Q("pageid") + ": 2, " + Q("index") + ": 2, "
            + Q("title") + ": " + Q(title + " filmography") + " },"
            + "{ " + Q("pageid") + ": 3, " + Q("index") + ": 3, "
            + Q("title") + ": " + Q(title + " discography") + " }"
            + "] } }";
    }

    private static string Disambiguation(string title, string[] alternatives)
    {
        var others = string.Join(",", alternatives.Select((a, i) =>
            "{ " + Q("pageid") + ": " + (i + 2) + ", " + Q("index") + ": " + (i + 2) + ", "
            + Q("title") + ": " + Q(a) + " }"));

        return "{ " + Q("batchcomplete") + ": true, " + Q("query") + ": { " + Q("pages") + ": ["
            + "{ " + Q("pageid") + ": 1, " + Q("index") + ": 1, "
            + Q("title") + ": " + Q(title) + ", "
            + Q("description") + ": " + Q("Topics referred to by the same term") + ", "
            + Q("pageprops") + ": { " + Q("disambiguation") + ": " + Q(string.Empty) + " } },"
            + others + "] } }";
    }

    /// <summary>An imageinfo response. <paramref name="title"/> is spelled as the API would return it.</summary>
    private static string ImageInfo(
        string title, string? licence, string? artist,
        string? restrictions = null, string? thumbUrl = null, int width = 2000, int height = 3000)
    {
        var meta = new List<string>();
        void Add(string key, string? value)
        {
            if (value is not null)
            {
                meta.Add(Q(key) + ": { " + Q("value") + ": " + Q(value) + ", "
                    + Q("source") + ": " + Q("commons-desc-page") + " }");
            }
        }

        Add("LicenseShortName", licence);
        Add("Artist", artist);
        Add("Restrictions", restrictions);
        Add("AttributionRequired", licence is null or "CC0" ? null : "true");

        return "{ " + Q("batchcomplete") + ": true, " + Q("query") + ": { " + Q("pages") + ": [ { "
            + Q("pageid") + ": 9, " + Q("ns") + ": 6, " + Q("title") + ": " + Q(title) + ", "
            + Q("imageinfo") + ": [ { "
            + Q("size") + ": 123456, "
            + Q("width") + ": " + width + ", " + Q("height") + ": " + height + ", "
            + Q("mime") + ": " + Q("image/jpeg") + ", "
            + Q("thumburl") + ": " + Q(thumbUrl ?? MediaHost + "a/ad/thumb.jpg") + ", "
            + Q("url") + ": " + Q(MediaHost + "a/ad/original.jpg") + ", "
            + Q("descriptionurl") + ": " + Q("https://commons.wikimedia.org/wiki/" + title) + ", "
            + Q("extmetadata") + ": { " + string.Join(",", meta) + " }"
            + " } ] } ] } }";
    }

    /// <summary>
    /// One JSON string value, quoted and escaped.
    /// </summary>
    /// <remarks>
    /// Escaping is not decorative here: one fixture feeds the source an artist field wrapped in an
    /// anchor tag, which is how the wiki really returns it, and an unescaped quote inside that would
    /// produce malformed JSON and a failure that looks like a parser bug.
    /// </remarks>
    private static string Q(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>A real, decodable PNG — the fetch path decodes rather than trusting a Content-Type.</summary>
    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0x40, 0x80, 0xC0));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static WikiHandler Wiki(string search, string? imageInfo = null, byte[]? image = null) =>
        new(search, imageInfo, image);

    /// <summary>
    /// Answers the two API shapes and the media host, and counts image fetches so a test can assert
    /// that nothing was pulled — which is what "refused before spending" actually means.
    /// </summary>
    private sealed class WikiHandler(string search, string? imageInfo, byte[]? image) : HttpMessageHandler
    {
        public int ImageRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();

            if (request.RequestUri.Host.Contains("upload.wikimedia", StringComparison.Ordinal)
                || request.RequestUri.Host.Contains("example.net", StringComparison.Ordinal))
            {
                ImageRequests += 1;
                if (image is null) return Task.FromResult(Status(HttpStatusCode.NotFound));

                var content = new ByteArrayContent(image);
                content.Headers.TryAddWithoutValidation("Content-Type", "image/png");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            var body = url.Contains("imageinfo", StringComparison.Ordinal)
                ? imageInfo ?? """{"batchcomplete":true,"query":{"pages":[]}}"""
                : search;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }

        private static HttpResponseMessage Status(HttpStatusCode code) =>
            new(code) { Content = new StringContent(string.Empty) };
    }
    #endregion
}
