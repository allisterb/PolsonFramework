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

    /// <summary>The documented surface, for <c>JSON.stringify</c>. See <see cref="RequisitionResult.ToJSON"/>.</summary>
    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["model"] = Model,
        ["hash"] = Hash,
        ["blockingHash"] = BlockingHash,
        ["requester"] = Requester,
        ["generatedUtc"] = GeneratedUtc,
        ["fromCache"] = FromCache,
        ["prompt"] = Prompt,
    };
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

    /// <summary>The documented surface, for <c>JSON.stringify</c>. See <see cref="RequisitionResult.ToJSON"/>.</summary>
    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["horizontalSeamStep"] = HorizontalSeamStep,
        ["verticalSeamStep"] = VerticalSeamStep,
        ["neighbourMedian"] = NeighbourMedian,
        ["neighbourMax"] = NeighbourMax,
        ["wraps"] = Wraps,
        ["repaired"] = Repaired,
    };
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

    /// <summary>
    /// What <c>JSON.stringify</c> serialises: the documented surface, in the documented spelling,
    /// with any image reported as a size rather than transcribed as numbers.
    /// </summary>
    /// <remarks>
    /// See <see cref="Polson.ExtendedMind.Photos.PhotoAsset.ToJSON"/> for the measurements behind
    /// this. In short: without the hook <c>JSON.stringify</c> walks the CLR object and emits raw
    /// bytes as a decimal array under PascalCase keys — and because that lands in the conversation
    /// and changes the prompt prefix, it also collapses prompt caching, so the cost repeats on
    /// every later turn instead of being paid once.
    /// <para>
    /// <paramref name="key"/> is the property name <c>JSON.stringify</c> passes. It is unused, but
    /// the parameter has to exist or no overload matches and the call fails with <i>"No public
    /// methods with the specified arguments were found"</i> — which is what a parameterless first
    /// attempt at this did.
    /// </para>
    /// </remarks>
    public virtual Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["success"] = Success,
        ["id"] = Id,
        ["provenance"] = Provenance?.ToJSON(),
        ["failureName"] = FailureName,
        ["error"] = Error,
        ["remedy"] = Remedy,
        ["retryable"] = Retryable,
    };
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

    /// <inheritdoc/>
    public override Dictionary<string, object?> ToJSON(string? key = null)
    {
        var json = base.ToJSON(key);
        json["byteLength"] = Bytes.Length;
        json["size"] = Size;
        json["mimeType"] = MimeType;
        json["tiling"] = Tiling?.ToJSON();
        return json;
    }
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

    /// <summary>The documented surface, for `JSON.stringify`. See <see cref="RequisitionResult.ToJSON"/>.</summary>
    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["keyLightX"] = KeyLightX,
        ["keyLightY"] = KeyLightY,
        ["bandLuminance"] = BandLuminance,
        ["quietX"] = QuietX,
        ["quietY"] = QuietY,
        ["quietWidth"] = QuietWidth,
        ["quietHeight"] = QuietHeight,
        ["quietRegionHonoured"] = QuietRegionHonoured,
        ["hasBakedMask"] = HasBakedMask,
    };
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

    /// <inheritdoc/>
    public override Dictionary<string, object?> ToJSON(string? key = null)
    {
        var json = base.ToJSON(key);
        json["byteLength"] = Bytes.Length;
        json["width"] = Width;
        json["height"] = Height;
        json["mimeType"] = MimeType;
        json["metrics"] = Metrics?.ToJSON();
        json["boundTo"] = BoundTo;
        json["isReusable"] = IsReusable;
        return json;
    }
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

    /// <inheritdoc/>
    public override Dictionary<string, object?> ToJSON(string? key = null)
    {
        var json = base.ToJSON(key);
        json["byteLength"] = Bytes.Length;
        json["size"] = Size;
        json["threshold"] = Threshold;
        json["coverage"] = Coverage;
        return json;
    }
}

/// <summary>One element of a cutout sheet: a trimmed RGBA image with its own alpha.</summary>
public sealed record CutoutCell : IDataUriSource
{
    /// <summary>The variant this cell was asked for, or <c>"subject"</c> when only one was.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The <see cref="RequisitionResult.Id"/> of the sheet this cell was cut from.</summary>
    /// <remarks>What lets a cell be passed as <see cref="CutoutOptions.Reference"/>: it resolves to its sheet.</remarks>
    public string SheetId { get; init; } = string.Empty;

    public byte[] Bytes { get; init; } = [];

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>Share of this cell's own box carrying opacity. Near zero means the key took the subject.</summary>
    /// <remarks>
    /// A whole-cell statistic, and therefore blind to what <see cref="Holes"/> measures. Check both.
    /// </remarks>
    public double Coverage { get; init; }

    /// <summary>
    /// Share of the cell that is transparent but enclosed by the subject: a hole punched through it.
    /// </summary>
    /// <remarks>
    /// <b>The check a face needs, because <see cref="Coverage"/> cannot see one.</b> A key set wide
    /// enough to catch the ground can also catch a skin tone, and what it removes then is the
    /// interior — which is a couple of percent of a standing figure and vanishes into a healthy
    /// coverage number. Measured on a live run: 68% coverage, and a face averaging 33/255 alpha.
    /// Near zero is clean; anything above a percent or so is worth looking at before building
    /// anything around the cell.
    /// </remarks>
    public double Holes { get; init; }

    /// <summary>Width over height. Cells are trimmed, so this differs between them.</summary>
    public double AspectRatio => Height == 0 ? 0 : Width / (double)Height;

    /// <summary>Base64 data URI. PNG, because alpha is the whole point and JPEG has none.</summary>
    public string ToDataUri() => Bytes.Length > 0
        ? $"data:image/png;base64,{Convert.ToBase64String(Bytes)}"
        : string.Empty;

    public Dictionary<string, object?> ToJSON(string? key = null) => new()
    {
        ["name"] = Name,
        ["sheetId"] = SheetId,
        ["byteLength"] = Bytes.Length,
        ["width"] = Width,
        ["height"] = Height,
        ["coverage"] = Coverage,
        ["holes"] = Holes,
        ["aspectRatio"] = AspectRatio,
    };
}

/// <summary>A sheet of cut-out elements from one generation, keyed to transparency and split.</summary>
/// <remarks>
/// <b>Check <see cref="Split"/> before trusting the cells.</b> The sheet is divided on the gaps the
/// background actually leaves, which is robust to a model that does not lay out on a grid — but when
/// the subjects touch, or one is missing, the gaps do not yield the count that was asked for and the
/// division falls back to equal columns. That fallback cuts through shoulders. It is reported rather
/// than hidden because the picture it produces is wrong in a way that renders perfectly.
/// </remarks>
public sealed record CutoutAsset : RequisitionResult, IDataUriSource
{
    /// <summary>One per variant, in the order they were asked for.</summary>
    public IReadOnlyList<CutoutCell> Cells { get; init; } = [];

    /// <summary>The whole keyed sheet, before splitting. Useful for seeing what came back.</summary>
    public byte[] Bytes { get; init; } = [];

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>How the sheet was divided: <c>single</c>, <c>gaps</c>, or <c>even</c> when gaps failed.</summary>
    public string Split { get; init; } = "single";

    /// <summary>The colour actually keyed out, as <c>#RRGGBB</c>.</summary>
    public string BackgroundColor { get; init; } = string.Empty;

    /// <summary>The ids of the sheets passed as <see cref="CutoutOptions.Reference"/>. Empty when none was.</summary>
    public IReadOnlyList<string> References { get; init; } = [];

    /// <summary>The cell for a variant, or null. Case-insensitive.</summary>
    /// <remarks>
    /// Null rather than a throw, because a panel loop asks this about every character it might show
    /// — the same reason <c>Shot.element(name)</c> answers a question rather than raising an error.
    /// </remarks>
    public CutoutCell? Cell(string name) =>
        Cells.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The whole sheet as a data URI. Usually you want a cell instead.</summary>
    public string ToDataUri() => Bytes.Length > 0
        ? $"data:image/png;base64,{Convert.ToBase64String(Bytes)}"
        : string.Empty;

    /// <inheritdoc/>
    public override Dictionary<string, object?> ToJSON(string? key = null)
    {
        var json = base.ToJSON(key);
        json["byteLength"] = Bytes.Length;
        json["width"] = Width;
        json["height"] = Height;
        json["split"] = Split;
        json["backgroundColor"] = BackgroundColor;
        json["references"] = References.ToList();
        json["cells"] = Cells.Select(c => c.ToJSON()).ToList();
        return json;
    }
}

/// <summary>Options for <c>Assets.cutout(...)</c>. Returns RGBA with a real alpha channel.</summary>
/// <remarks>
/// <b>The line this call sits on, and why it is not the one the classifier draws.</b> A material is
/// substance and a matte is a silhouette; a cutout is a <i>depiction</i>, which is further than either.
/// It is allowed because the studio's rule was never "buy no forms" — <see cref="MatteOptions"/>
/// already answers a form deliberately — but "buy no pictures". A cutout supplies one element; where
/// it sits, how deep, at what scale and in what colour all stay with the code, which is what makes a
/// composition the studio's rather than the model's.
/// <para>
/// <b><see cref="Variants"/> is the consistency mechanism and is the reason this is not n separate
/// calls.</b> Generation is not deterministic across calls, so two requisitions of "the same man"
/// return two different men and a board loses its character between panels. One generation carrying
/// every pose needed is one context, so the figure is the same figure by construction — the arranged
/// route's answer to what <c>createParametricHead</c> does for the constructed one.
/// </para>
/// </remarks>
public sealed record CutoutOptions
{
    /// <summary>
    /// Poses, expressions or angles to generate together. One cell each, left to right.
    /// </summary>
    /// <remarks>
    /// Capped at six: the sheet is one generation of fixed width, so every variant added makes every
    /// cell narrower. Four across a 1024 master is about 250px a cell, which is a storyboard face and
    /// is not a portrait.
    /// </remarks>
    public IReadOnlyList<string>? Variants { get; init; }

    /// <summary>Longest edge of each delivered cell. Cells are trimmed to their own extent, so they differ.</summary>
    public int Size { get; init; } = 512;

    /// <summary>How it should be drawn. The default is what a storyboard wants.</summary>
    public string Style { get; init; } = "loose graphite storyboard sketch, clean line, no rendering";

    /// <summary>Background to key out as <c>#RRGGBB</c>. Left unset it is measured from the corners.</summary>
    /// <remarks>
    /// <b>This steers the keyer, not the model.</b> The prompt always asks for the same flat ground;
    /// setting this changes only which colour is <i>removed</i> afterwards. So naming a colour the
    /// sheet was never painted in removes nothing — measured on a live run, <c>'#00FF00'</c> against
    /// a sheet the model had drawn in dusty pink gave <c>split: 'even'</c>, 100% coverage and two
    /// opaque rectangles, for one wasted generation.
    /// <para>
    /// Measuring is the better default for the same reason a stencil measures its cut level: a model
    /// asked for pure magenta delivers approximately magenta, and keying the literal value leaves a
    /// fringe standing all round the subject. Override it only to key a colour you have read off the
    /// sheet that came back.
    /// </para>
    /// </remarks>
    public string? Background { get; init; }

    /// <summary>How close to the background a pixel must be to be removed, 0 to 1.</summary>
    /// <remarks>
    /// <b>Lower it for a subject carrying skin.</b> The default is tuned to clear a ground the model
    /// drew approximately, and it is wide enough to reach a pale or warm skin tone when the ground
    /// drifts toward one — which takes the face and leaves the body, so the cell still measures a
    /// healthy <see cref="CutoutCell.Coverage"/>. On the run that found this, 0.18 removed a face and
    /// <b>0.10 was the answer</b>: ground gone, split back to <c>gaps</c>, face alpha 223/255. Raise
    /// it again only if a fringe of ground stands around the subject, which is at least visible.
    /// </remarks>
    public double Tolerance { get; init; } = 0.18;

    /// <summary>
    /// An earlier cutout of the same subject, shown to the model so this one draws the same subject.
    /// </summary>
    /// <remarks>
    /// <b>The way to add to a character after its first call.</b> <see cref="Variants"/> makes one
    /// generation consistent, but a second call cannot see the first, so without this a later
    /// expression, a closer head sheet or a missed view is a different person. With it, the model is
    /// handed the earlier sheet and asked to change nothing but what the variants list.
    /// <para>
    /// Takes a <see cref="CutoutAsset"/>, one of its <see cref="CutoutCell"/>s, its
    /// <see cref="RequisitionResult.Id"/>, or an array of up to three of those. A cell stands for the
    /// whole sheet it was cut from, which is what is sent.
    /// </para>
    /// <para>
    /// <b>Only an image this studio generated can be a reference</b>, resolved by id through the
    /// project's asset cache rather than read off the object passed. Anything else is refused before
    /// the network is touched: a photograph or a drawing handed in here would carry a likeness past
    /// the checks <c>Photo</c> applies, and a canvas could hold either.
    /// </para>
    /// </remarks>
    public object? Reference { get; init; }

    public string? Model { get; init; }
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
