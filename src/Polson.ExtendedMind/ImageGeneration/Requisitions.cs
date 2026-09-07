namespace Polson.ExtendedMind.ImageGeneration;

using System.Collections.Generic;

/// <summary>
/// Request and result types for the asset requisition API.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script reading <c>plate.keyLight</c> reaches <c>KeyLight</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public static class Requisitions
{
    /// <summary>Every generation returns 1024x1024 regardless of what is asked for. This is the floor.</summary>
    public const int NativeSize = 1024;

    /// <summary>Largest swatch an agent may receive. Derived locally from the 1024 master, never requested.</summary>
    public const int MaxDeliveredSize = 1024;
}

#region Requests

/// <summary>What kind of raw material is being requisitioned. Selects the enforced return shape.</summary>
public enum RequisitionKind
{
    /// <summary>A flat, geometry-independent swatch. Safest kind: reusable, cacheable, survives any redraw.</summary>
    Material = 0,

    /// <summary>A background plate composited beneath the scene. Never returned as a drawable-over layer.</summary>
    Backdrop = 1,

    /// <summary>A single-channel mask / height / displacement source, for use as a shader input.</summary>
    Matte = 2,
}

/// <summary>Region of a backdrop that must be left dark and free of detail for foreground work.</summary>
public enum QuietRegion
{
    None = 0,
    LowerThird = 1,
    UpperThird = 2,
    LeftHalf = 3,
    RightHalf = 4,
    Center = 5,
}

/// <summary>
/// Options for <c>ExtendedMind.material(...)</c>.
/// </summary>
/// <remarks>
/// <c>Size</c> costs nothing: a requisition always buys a 1024 master, which is cached, and every
/// requested size is a local resample of it. Asking for 256 then 512 bills once.
/// <c>Tileable</c> is a guarantee this layer keeps by repairing the swatch, not a hint forwarded
/// to the model — the model does not reliably honour it and fails silently when it does not.
/// </remarks>
public sealed record MaterialOptions
{
    /// <summary>Delivered edge length in pixels. Clamped to [32, 1024]. Default 512.</summary>
    public int Size { get; init; } = 512;

    /// <summary>Guarantee seamless wrapping. Verified on receipt and repaired if the swatch does not wrap.</summary>
    public bool Tileable { get; init; } = true;

    /// <summary>Delivered encoding. WebP q85 at 512 is ~27 KB against ~400 KB of PNG.</summary>
    public string Format { get; init; } = "webp";

    /// <summary>Encoder quality, 1-100.</summary>
    public int Quality { get; init; } = 85;

    /// <summary>Override the generator's default model for this requisition.</summary>
    public string? Model { get; init; }
}

/// <summary>
/// Options for <c>ExtendedMind.backdrop(...)</c>.
/// </summary>
/// <remarks>
/// Supplying <c>ConditionOn</c> changes what you get in a way the agent must understand: the model
/// inpaints only the empty region and bakes the blocking silhouette into the plate as solid black.
/// The result composes around your geometry — the bright mass moves to wherever the blocking left
/// sky open — but it is welded to that blocking. Any silhouette edit invalidates it. See
/// <see cref="BackdropPlate.BoundTo"/>.
/// </remarks>
public sealed record BackdropOptions
{
    /// <summary>Target width in pixels. The plate is cropped or letterboxed to fit, never stretched.</summary>
    public int Width { get; init; } = 1024;

    /// <summary>Target height in pixels.</summary>
    public int Height { get; init; } = 576;

    /// <summary>Region the plate must leave dark and detail-free. Verified against measured band luminance.</summary>
    public QuietRegion KeepQuiet { get; init; } = QuietRegion.None;

    /// <summary>Reject a horizon line in the plate. The scene supplies its own ground plane.</summary>
    public bool NoHorizon { get; init; } = true;

    /// <summary>Reject foreground objects. A plate carrying its own subject fights the drawn scene.</summary>
    public bool NoForeground { get; init; } = true;

    /// <summary>
    /// PNG bytes of the already-drawn blocking. Black is foreground silhouette, grey is sky to fill.
    /// Present means the returned plate is bound to this blocking and is not reusable across revisions.
    /// </summary>
    public byte[]? ConditionOn { get; init; }

    public string Format { get; init; } = "webp";

    public int Quality { get; init; } = 85;

    public string? Model { get; init; }
}

/// <summary>Options for <c>ExtendedMind.matte(...)</c>. Returns one channel, never RGB.</summary>
/// <remarks>
/// <b>This is also the stencil channel, and <see cref="HardEdge"/> is what makes it one.</b> The
/// classifier does not run here — a matte of a form is what a matte is for — so this is the one
/// requisition that will answer "a rearing horse" or "a bare oak in winter". What comes back is a
/// silhouette the code then owns: colour, scale and placement stay with the drawing toolkit, which
/// is the whole difference between this and buying a finished picture.
/// </remarks>
public sealed record MatteOptions
{
    public int Size { get; init; } = 512;

    /// <summary>Invert so that white marks the region of interest.</summary>
    public bool Invert { get; init; }

    /// <summary>
    /// Cut the luminance ramp to pure black and white at a level measured from the image itself.
    /// </summary>
    /// <remarks>
    /// Off by default, because a height field and a displacement source both want the ramp. Turn it
    /// on for a stencil: a soft-edged silhouette clips and masks with a halo, and traces to nothing.
    /// </remarks>
    public bool HardEdge { get; init; }

    /// <summary>Explicit cut level in 0-255. Implies <see cref="HardEdge"/>; leave unset to measure it.</summary>
    public int? Threshold { get; init; }

    public string? Model { get; init; }
}

#endregion

#region Results

/// <summary>Where an asset came from and what it cost. Attached to every delivered asset.</summary>
/// <remarks>
/// This is what lets the Facilitator distinguish bought pixels from computed ones when evaluating a
/// pull request. Without it, "how much of this did the agent actually make" is unanswerable.
/// </remarks>
public sealed record Provenance
{
    /// <summary>Model that produced the master, e.g. <c>gemini-2.5-flash-image</c>.</summary>
    public required string Model { get; init; }

    /// <summary>Content address of (model, prompt, params). Also the cache key.</summary>
    public required string Hash { get; init; }

    /// <summary>Hash of the blocking a conditioned plate was generated against, else null.</summary>
    public string? BlockingHash { get; init; }

    /// <summary>Agent role that requisitioned it, for library attribution and spend accounting.</summary>
    public string Requester { get; init; } = "unknown";

    /// <summary>UTC timestamp of the generation that produced the master.</summary>
    public required DateTime GeneratedUtc { get; init; }

    /// <summary>True when this delivery was served from cache and cost nothing.</summary>
    public bool FromCache { get; init; }

    /// <summary>Verbatim prompt sent upstream. Logged for audit; prompts leave the machine.</summary>
    public required string Prompt { get; init; }
}

/// <summary>Measured tiling behaviour of a material swatch.</summary>
/// <remarks>
/// The naive test — comparing the wrap seam against "unrelated" pixels — does not discriminate on a
/// uniform texture, where unrelated columns differ as much as adjacent ones. The test that works is
/// an outlier test: roll the image by half so the seam falls mid-frame, then compare the step across
/// it against the distribution of ordinary neighbour steps. A seam above <c>NeighbourMax</c> is a
/// real discontinuity even when it looks intentional, which it does whenever the motif carries
/// linear features that camouflage it.
/// </remarks>
public sealed record TilingMetrics
{
    public required double HorizontalSeamStep { get; init; }
    public required double VerticalSeamStep { get; init; }
    public required double NeighbourMedian { get; init; }
    public required double NeighbourMax { get; init; }

    /// <summary>True when both seam steps sit inside the ordinary neighbour-step distribution.</summary>
    /// <remarks>
    /// Judged with a little slack: a seam landing exactly on the steepest legitimate gradient is not
    /// an outlier. Real failures clear this comfortably — the wood swatch measured 2.05x its largest
    /// ordinary step.
    /// </remarks>
    public bool Wraps => HorizontalSeamStep <= NeighbourMax * SeamTolerance
                      && VerticalSeamStep <= NeighbourMax * SeamTolerance;

    /// <summary>Multiple of the largest ordinary neighbour step a seam may reach and still pass.</summary>
    public const double SeamTolerance = 1.15;

    /// <summary>Set when the swatch did not wrap and this layer repaired it.</summary>
    public bool Repaired { get; init; }
}

/// <summary>
/// Common shape of every requisition outcome, successful or not.
/// </summary>
/// <remarks>
/// A requisition is a metered network call and can fail in ways the agent must tell apart: a
/// rate-limit wants the same request again, a safety block wants different words, an exhausted
/// budget wants the material drawn procedurally instead. Throwing collapses those into one event
/// and forces every call site into a guard, so failure is returned as a value. Check
/// <see cref="Success"/>, then act on <see cref="Remedy"/>.
/// </remarks>
public abstract record RequisitionResult
{
    public bool Success { get; init; }

    public string Id { get; init; } = string.Empty;

    /// <summary>Null when the requisition failed before anything was generated.</summary>
    public Provenance? Provenance { get; init; }

    public ImageGenerationFailure Failure { get; init; } = ImageGenerationFailure.None;

    public string? Error { get; init; }

    /// <summary>
    /// The failure as a name rather than a number.
    /// </summary>
    /// <remarks>
    /// Jint surfaces a CLR enum to a script as its underlying integer, so <c>failure</c> alone reads
    /// as <c>16</c> in JavaScript. This is the spelling the reference documents and an agent can act on.
    /// </remarks>
    public string FailureName => Failure.ToString();

    /// <summary>What to do next, phrased for the agent reading it.</summary>
    public string Remedy => ImageGenerationResult.RemedyFor(Failure);

    /// <summary>Whether repeating the identical requisition could plausibly succeed.</summary>
    public bool Retryable => ImageGenerationResult.IsRetryable(Failure);
}

/// <summary>A flat material swatch. Geometry-independent, reusable, and cheap to re-derive at any size.</summary>
public sealed record MaterialAsset : RequisitionResult, IDataUriSource
{
    /// <summary>Encoded bytes at the requested size and format. Empty when <see cref="RequisitionResult.Success"/> is false.</summary>
    public byte[] Bytes { get; init; } = [];

    public int Size { get; init; }

    public string MimeType { get; init; } = "image/webp";

    /// <summary>Null when the requisition failed.</summary>
    public TilingMetrics? Tiling { get; init; }

    /// <summary>Base64 data URI, for handing straight to <c>Skia.Image.fromDataUrl</c>.</summary>
    public string ToDataUri() => Bytes.Length > 0
        ? $"data:{MimeType};base64,{Convert.ToBase64String(Bytes)}"
        : string.Empty;
}

/// <summary>Measurements taken from a returned backdrop, so the foreground reads the plate rather than guessing.</summary>
/// <remarks>
/// <see cref="KeyLight"/> feeds <c>Drawing.projectCastShadow</c>, <c>Drawing.drawRimLight</c> and
/// <c>Drawing.createThreePointLighting</c> directly. This is the join between the generated half of
/// the system and the procedural half: the agent perceives the light rather than assuming it.
/// </remarks>
public sealed record PlateMetrics
{
    /// <summary>Luminance-weighted centroid of the brightest mass, in normalised [0,1] plate coordinates.</summary>
    public required double KeyLightX { get; init; }
    public required double KeyLightY { get; init; }

    /// <summary>Mean luminance of the top, middle and lower thirds.</summary>
    /// <remarks>An array for the same reason as <see cref="RequisitionVerdict.Triggers"/>: a JS-reachable
    /// collection property must not alternate its runtime type between calls.</remarks>
    public required double[] BandLuminance { get; init; }

    /// <summary>Darkest large region, in normalised coordinates: where foreground can safely sit.</summary>
    public required double QuietX { get; init; }
    public required double QuietY { get; init; }
    public required double QuietWidth { get; init; }
    public required double QuietHeight { get; init; }

    /// <summary>True when the requested <see cref="BackdropOptions.KeepQuiet"/> region was actually delivered.</summary>
    public required bool QuietRegionHonoured { get; init; }

    /// <summary>
    /// True when the model inpainted rather than filling the frame, leaving a foreground-shaped
    /// region of pure black. Detected by thresholding; the mask is exactly recoverable.
    /// </summary>
    public required bool HasBakedMask { get; init; }
}

/// <summary>A background plate. Composited beneath the scene; never handed back as a drawable-over layer.</summary>
public sealed record BackdropPlate : RequisitionResult, IDataUriSource
{
    public byte[] Bytes { get; init; } = [];

    public int Width { get; init; }

    public int Height { get; init; }

    public string MimeType { get; init; } = "image/webp";

    /// <summary>Null when the requisition failed.</summary>
    public PlateMetrics? Metrics { get; init; }

    /// <summary>
    /// Hash of the blocking this plate was composed around, or null for an unconditioned plate.
    /// A non-null value means the plate is single-use: change the silhouette and it must be re-bought.
    /// </summary>
    public string? BoundTo => Provenance?.BlockingHash;

    /// <summary>True when this plate survives foreground iteration.</summary>
    public bool IsReusable => BoundTo is null;

    public string ToDataUri() => Bytes.Length > 0
        ? $"data:{MimeType};base64,{Convert.ToBase64String(Bytes)}"
        : string.Empty;
}

/// <summary>A single-channel mask, height field, or displacement source.</summary>
/// <remarks>
/// <b>Implements <see cref="IDataUriSource"/> so that <c>paper.image(matte, …)</c> works.</b> It did
/// not, and the omission was found by a live run: a stencil was requisitioned successfully and then
/// took two further scripts to get onto the page, because <c>image(src, …)</c> dispatches on this
/// interface and fell through to its generic refusal. A matte is encoded bytes exactly as a material
/// is, so there was never a reason for it to be the one requisition you could not draw.
/// </remarks>
public sealed record MatteAsset : RequisitionResult, IDataUriSource
{
    public byte[] Bytes { get; init; } = [];

    /// <summary>Delivered edge length. A matte is square, so this is both width and height.</summary>
    public int Size { get; init; }

    /// <summary>The cut level actually used, or null when the ramp was kept.</summary>
    /// <remarks>Reported rather than assumed: a measured level is a fact about the plate that came back.</remarks>
    public int? Threshold { get; init; }

    /// <summary>Share of the frame that is "on", 0 to 1.</summary>
    /// <remarks>
    /// <b>Check this.</b> A stencil near 0 or near 1 decoded, encoded and will draw — as an empty
    /// frame or a solid block. Nothing downstream can tell that apart from a subject that happens to
    /// be small or large, so this is the only signal that the generation failed.
    /// </remarks>
    public double Coverage { get; init; }

    /// <summary>Base64 data URI. A matte is delivered as PNG, which is lossless on a hard edge.</summary>
    public string ToDataUri() => Bytes.Length > 0
        ? $"data:image/png;base64,{Convert.ToBase64String(Bytes)}"
        : string.Empty;
}

#endregion

#region Budget and gatekeeping

/// <summary>
/// Remaining generation allowance, visible to scripts.
/// </summary>
/// <remarks>
/// Deliberately readable rather than a hidden cap that throws. An agent that can see a dwindling
/// resource forms a strategy; an agent that hits a silent wall behaves incoherently.
/// </remarks>
public sealed class AssetBudget
{
    public AssetBudget(int total) => Total = total;

    public int Total { get; }

    public int Spent { get; internal set; }

    public int Remaining => Math.Max(0, Total - Spent);

    /// <summary>Generations served from cache, which cost nothing and are not charged.</summary>
    public int CacheHits { get; internal set; }

    /// <summary>
    /// Tokens billed this session, summed from what the service reported per call.
    /// </summary>
    /// <remarks>
    /// The generation count is what the allowance is enforced against, because it is knowable before
    /// a call. This is what the spend actually was — the two diverge as soon as a session mixes Pro
    /// with Flash, or sends conditioning images, which are charged as prompt tokens.
    /// </remarks>
    public long TokensSpent { get; internal set; }

    public bool CanAfford(int count = 1) => Remaining >= count;
}

/// <summary>Outcome of the form-versus-substance check.</summary>
public enum RequisitionClass
{
    /// <summary>A substance with no silhouette. Code supplies the shape. Allowed.</summary>
    Substance = 0,

    /// <summary>A thing with an outline. The toolkit draws this better and controllably. Refused.</summary>
    Form = 1,

    /// <summary>Could be read either way. Allowed, but recorded for the Asset Manager to review.</summary>
    Ambiguous = 2,
}

/// <summary>Verdict returned by the cheap pre-flight check that runs before any generation.</summary>
public sealed record RequisitionVerdict
{
    public required RequisitionClass Class { get; init; }

    /// <summary>
    /// The classification as a readable name — <c>'Substance'</c>, <c>'Form'</c> or
    /// <c>'Ambiguous'</c>. Scripts see <see cref="Class"/> as a bare integer, so prefer this.
    /// </summary>
    public string ClassName => Class.ToString();

    public required string Reason { get; init; }

    /// <summary>Terms that triggered the classification, for the message shown to the agent.</summary>
    /// <remarks>
    /// Declared as an array, not <c>IReadOnlyList</c>, and that is load-bearing. Jint caches the
    /// member accessor for a property and the cache assumes the runtime type is stable, so a
    /// property that returned <c>string[]</c> on one call and <c>List&lt;string&gt;</c> on the next
    /// threw <c>Unable to cast … List`1[System.String] … to type 'System.Array'</c> — on the *third*
    /// call, after the type alternated. Classifying a material and then an object in one session was
    /// enough to hit it. An array-typed property cannot alternate.
    /// </remarks>
    public string[] Triggers { get; init; } = [];

    public bool Allowed => Class != RequisitionClass.Form;
}

#endregion
