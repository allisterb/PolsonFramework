namespace Polson.ExtendedMind.ImageGeneration;

using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Google.GenAI;
using Google.GenAI.Types;

/// <summary>
/// Transport to the cloud image models. Owns the client, the model name, and the raw call.
/// </summary>
/// <remarks>
/// <para>
/// This type is <b>not</b> exposed to the JavaScript sandbox and must not be. An unconstrained
/// <c>generate(prompt)</c> would let an agent buy a finished picture in one call, which defeats the
/// point twice over: it spends the budget on work the drawing toolkit does better, and it produces
/// an artifact no other agent can read. Stigmergic collaboration needs the trace to be code.
/// Scripts reach generation only through <see cref="AssetRequisitionToolkit"/>, whose return types
/// are raw material that requires code to become art.
/// </para>
/// <para>
/// Auth is Gemini Enterprise Agent Platform express mode: an API key with <c>enterprise: true</c>
/// and no project/location. The combinations are not interchangeable —
/// <c>new Client(apiKey, project, location)</c> without the flag throws
/// <i>"Project/location and API key are mutually exclusive"</i>, and the shipped XML docs claim the
/// API key is "Gemini API only", which is stale. Verified against Google.GenAI 1.20.0.
/// </para>
/// <para>
/// Nothing here throws for a remote fault. Every call returns an <see cref="ImageGenerationResult"/>
/// carrying a classified <see cref="ImageGenerationFailure"/>, because the caller two layers up is
/// an agent that has to decide between retrying, rewording, and giving up — and it cannot make that
/// decision from a stack trace.
/// </para>
/// </remarks>
public class ImageGenerator : Runtime, IImageGenerator, IDisposable
{
    #region Constructors
    /// <param name="apiKey">Express-mode API key. Read from <c>ApiKeys:GoogleAgentPlatform</c>.</param>
    /// <param name="model">Default model. Overridable per call.</param>
    /// <param name="projectId">Only for a non-express key authenticating with ADC. Leave null for express mode.</param>
    /// <param name="location">As <paramref name="projectId"/>.</param>
    public ImageGenerator(string apiKey, string model = DefaultModel, string? projectId = null, string? location = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        this.apiKey = apiKey;
        this.projectId = projectId;
        this.location = location;
        this.model = model;

        // enterprise:true selects the Agent Platform endpoint. Passing project/location alongside an
        // API key is only legal once that flag is set; without it the SDK rejects the combination.
        client = (projectId, location) switch
        {
            (null, null) => new Client(enterprise: true, apiKey: apiKey),
            _            => new Client(enterprise: true, apiKey: apiKey, project: projectId, location: location),
        };
    }
    #endregion

    #region Properties
    /// <summary>Nano Banana. The default: fast, and adequate for backdrops and coarse material.</summary>
    public const string DefaultModel = "gemini-2.5-flash-image";

    /// <summary>Nano Banana 2 / Pro. Finer feature density, better for material swatches. Costs more.</summary>
    public const string ProModel = "gemini-3-pro-image";

    /// <summary>Every generation returns this edge length whatever is requested. There is no smaller tier.</summary>
    public const int NativeSize = 1024;

    public string Model => model;
    #endregion

    #region Methods
    /// <summary>
    /// Generates one image. Never throws for a remote fault; inspect <see cref="ImageGenerationResult.Success"/>.
    /// </summary>
    /// <remarks>
    /// Output size is not controllable. <c>ImageConfig.ImageSize</c> offers only <c>1K</c>/<c>2K</c>/<c>4K</c>,
    /// and an explicit "exactly 256 by 256 pixels" instruction in the prompt is ignored — the result
    /// is 1024x1024 either way. Callers wanting a smaller asset resample locally; see
    /// <see cref="AssetRequisitionToolkit"/>, which caches the 1024 master so repeated sizes bill once.
    /// </remarks>
    public Task<ImageGenerationResult> Generate(string prompt, string? model = null) =>
        GenerateImage(prompt, model, null, null, CancellationToken.None);

    /// <summary>Generation with aspect control and optional conditioning images.</summary>
    /// <param name="conditionOn">
    /// Reference images sent ahead of the prompt. Note the model inpaints rather than regenerating:
    /// it preserves the reference geometry and fills only the empty region, so a plate conditioned
    /// this way comes back with the reference silhouette baked in as solid black.
    /// </param>
    public async Task<ImageGenerationResult> GenerateImage(
        string prompt,
        string? model = null,
        string? aspectRatio = null,
        IReadOnlyList<byte[]>? conditionOn = null,
        CancellationToken ct = default)
    {
        var useModel = model ?? this.model;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ImageGenerationResult.Failed(
                ImageGenerationFailure.InvalidRequest, "Prompt was empty.", prompt ?? string.Empty, useModel);
        }

        var parts = new List<Part>();
        foreach (var image in conditionOn ?? [])
        {
            parts.Add(new Part { InlineData = new Blob { MimeType = "image/png", Data = image } });
        }

        parts.Add(new Part { Text = prompt });

        var config = new GenerateContentConfig
        {
            ResponseModalities = ["IMAGE"],
            ImageConfig = aspectRatio is null ? null : new ImageConfig { AspectRatio = aspectRatio },
        };

        var started = DateTime.UtcNow;
        long Elapsed() => (long)(DateTime.UtcNow - started).TotalMilliseconds;

        try
        {
            var response = await client.Models.GenerateContentAsync(
                useModel, [new Content { Role = "user", Parts = parts }], config, ct);

            var blob = response.Candidates?
                .SelectMany(c => c.Content?.Parts ?? [])
                .Select(p => p.InlineData)
                .FirstOrDefault(b => b?.Data is not null);

            // A 200 carrying no image is not an empty image. Reporting it as success with zero bytes
            // would let a blank asset propagate into a scene and read as a deliberate blank.
            if (blob?.Data is null)
            {
                var finish = response.Candidates?.FirstOrDefault()?.FinishReason?.ToString();
                // The service declines in more than one way and the right response differs: a safety
                // block and a recitation block both need new words, while a bare empty response is
                // worth one retry. Lumping them together sends the agent down the wrong branch.
                var reason = finish switch
                {
                    null => ImageGenerationFailure.NoImageReturned,
                    _ when finish.Contains("RECITATION", StringComparison.OrdinalIgnoreCase)
                        => ImageGenerationFailure.Recitation,
                    _ when finish.Contains("SAFETY", StringComparison.OrdinalIgnoreCase)
                        || finish.Contains("BLOCK", StringComparison.OrdinalIgnoreCase)
                        || finish.Contains("PROHIBITED", StringComparison.OrdinalIgnoreCase)
                        => ImageGenerationFailure.SafetyBlocked,
                    _ => ImageGenerationFailure.NoImageReturned,
                };

                Warn("{Model} returned no image part (finish={Finish})", useModel, finish ?? "none");

                var noImage = ImageGenerationResult.Failed(
                    reason,
                    $"No image part in response (finishReason={finish ?? "none"}).", prompt, useModel);
                noImage.ElapsedMs = Elapsed();
                return noImage;
            }

            // Untrusted binary from the network. A truncated payload must fail loudly rather than
            // yield nonsense dimensions that only surface much later as a broken draw.
            if (!TryReadPngSize(blob.Data, out var width, out var height))
            {
                var bad = ImageGenerationResult.Failed(
                    ImageGenerationFailure.NoImageReturned,
                    $"Undecodable image payload ({blob.Data.Length} bytes, mime={blob.MimeType ?? "unknown"}).",
                    prompt, useModel);
                bad.ElapsedMs = Elapsed();
                return bad;
            }

            return new ImageGenerationResult
            {
                Success = true,
                ImageBytes = blob.Data,
                Width = width,
                Height = height,
                MimeType = blob.MimeType ?? "image/png",
                Model = response.ModelVersion ?? useModel,
                Prompt = prompt,
                Hash = HashOf(useModel, prompt, aspectRatio, conditionOn),
                GeneratedUtc = started,
                ElapsedMs = Elapsed(),
                Charged = true,
                PromptTokens = response.UsageMetadata?.PromptTokenCount,
                OutputTokens = response.UsageMetadata?.CandidatesTokenCount,
                TotalTokens = response.UsageMetadata?.TotalTokenCount,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Stamp(ImageGenerationResult.Failed(
                ImageGenerationFailure.Cancelled, "Cancelled by the caller.", prompt, useModel), Elapsed());
        }
        catch (TaskCanceledException ex)
        {
            return Stamp(ImageGenerationResult.Failed(
                ImageGenerationFailure.Timeout, ex.Message, prompt, useModel), Elapsed());
        }
        catch (ClientError ex)
        {
            Error(ex, "{Model} rejected the request ({Status})", useModel, ex.StatusCode);
            return Stamp(ImageGenerationResult.Failed(
                Classify(ex.StatusCode, ex.Message), ex.Message, prompt, useModel, ex.StatusCode), Elapsed());
        }
        catch (ServerError ex)
        {
            Error(ex, "{Model} failed server-side ({Status})", useModel, ex.StatusCode);
            return Stamp(ImageGenerationResult.Failed(
                ImageGenerationFailure.ServiceError, ex.Message, prompt, useModel, ex.StatusCode), Elapsed());
        }
        catch (HttpRequestException ex)
        {
            return Stamp(ImageGenerationResult.Failed(
                ImageGenerationFailure.Network, ex.Message, prompt, useModel), Elapsed());
        }
        catch (Exception ex)
        {
            Error(ex, "Unexpected failure generating with {Model}", useModel);
            return Stamp(ImageGenerationResult.Failed(
                ImageGenerationFailure.Unknown, ex.Message, prompt, useModel), Elapsed());
        }
    }

    /// <summary>
    /// Discovers which models this key can actually reach, by probing names.
    /// </summary>
    /// <remarks>
    /// <c>Models.ListAsync</c> is unavailable on an express key — it fails with
    /// <i>"API keys are not supported by this API. Expected OAuth2 access token"</i> — so enumeration
    /// needs ADC. Probing is the only discovery route, and a probe that reaches a live model bills
    /// for an image, so this should run once at startup and be cached, never per call.
    /// </remarks>
    public Task<IReadOnlyList<string>> ProbeModels(IReadOnlyList<string> candidates, CancellationToken ct = default) =>
        throw new NotImplementedException("Probe each candidate with a minimal prompt; a miss returns ModelNotFound for free, a hit bills one image.");

    /// <summary>Content address of a generation request. Doubles as the cache key.</summary>
    public static string HashOf(string model, string prompt, string? aspectRatio, IReadOnlyList<byte[]>? conditionOn)
    {
        var sb = new StringBuilder().Append(model).Append('|').Append(prompt).Append('|').Append(aspectRatio);
        foreach (var image in conditionOn ?? [])
        {
            sb.Append('|').Append(Convert.ToHexString(SHA256.HashData(image))[..16]);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..32];
    }

    /// <summary>Reads width and height from a PNG IHDR, avoiding an image-decoding dependency here.</summary>
    public static bool TryReadPngSize(byte[] png, out int width, out int height)
    {
        width = height = 0;
        if (png.Length < 24 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
        {
            return false;
        }

        width = png[16] << 24 | png[17] << 16 | png[18] << 8 | png[19];
        height = png[20] << 24 | png[21] << 16 | png[22] << 8 | png[23];
        return width > 0 && height > 0;
    }

    static ImageGenerationFailure Classify(int status, string message) => status switch
    {
        400 when message.Contains("safety", StringComparison.OrdinalIgnoreCase) => ImageGenerationFailure.SafetyBlocked,
        400 => ImageGenerationFailure.InvalidRequest,
        401 or 403 => ImageGenerationFailure.Auth,
        404 => ImageGenerationFailure.ModelNotFound,
        429 when message.Contains("quota", StringComparison.OrdinalIgnoreCase) => ImageGenerationFailure.Quota,
        429 => ImageGenerationFailure.RateLimited,
        _ => ImageGenerationFailure.InvalidRequest,
    };

    static ImageGenerationResult Stamp(ImageGenerationResult result, long elapsedMs)
    {
        result.ElapsedMs = elapsedMs;
        return result;
    }

    /// <summary>Releases the underlying SDK client and its HTTP resources.</summary>
    public void Dispose()
    {
        client.Dispose();
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Fields
    readonly string apiKey;
    readonly string? projectId;
    readonly string? location;
    readonly string model;
    readonly Client client;
    #endregion
}
