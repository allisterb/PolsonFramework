namespace Polson.ExtendedMind.ImageGeneration;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Polson;

using SkiaSharp;

/// <summary>
/// The requisition surface, exposed to scripts as <c>ExtendedMind</c>.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>ExtendedMind.material(...)</c> reaches
/// <see cref="Material"/>. The camelCase form is the one documented in <c>docs/Polson.core.md</c>
/// and the studio manuals.
/// </para>
/// <para>
/// <b>Why the methods are typed rather than a single prompt call.</b> A policy expressed in a manual
/// ("only use generation for textures") does not survive goal pressure, and the request text comes
/// from another agent, so a gatekeeper that reads it is a thing that can be argued with. These
/// signatures make the misuse structurally unavailable instead: every return value is raw material
/// that needs code to become art. That constraint is not only budgetary — an image the agent did not
/// write code for is illegible to the other agents reading the artifact.
/// </para>
/// <para>
/// <b>Ranked by risk</b>, because they are not interchangeable:
/// <see cref="Material"/> is geometry-independent, reusable across scenes, and survives unlimited
/// redrawing — prefer it. <see cref="Backdrop"/> without <c>conditionOn</c> is reusable but the
/// composition must accommodate it. <see cref="Backdrop"/> <i>with</i> <c>conditionOn</c> composes
/// around the drawn blocking, which no other route achieves, but comes back welded to that blocking
/// and is invalidated by any silhouette edit.
/// </para>
/// </remarks>
public partial class AssetRequisitionToolkit : Runtime
{
    #region Constructors
    public AssetRequisitionToolkit(IImageGenerator? generator, IRequisitionCache cache, AssetBudget budget, string requester = "unknown")
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(budget);

        this.generator = generator;
        this.cache = cache;
        this.requester = requester;
        Budget = budget;
    }
    #endregion

    #region Properties
    /// <summary>Remaining allowance, readable by scripts so an agent can plan rather than hit a wall.</summary>
    public AssetBudget Budget { get; }

    /// <summary>
    /// Every asset requisitioned this session, by any agent.
    /// </summary>
    /// <remarks>
    /// Shared visibility is the point. Four agents each buying their own materials produce four
    /// disconnected vocabularies and the piece reads as a collage; a library the Builder can see
    /// lets it reuse the oak the Framer already established. This is also what an Asset Manager
    /// curates, and it is the one coordination job that type constraints cannot do.
    /// </remarks>
    public IReadOnlyList<MaterialAsset> Library => library;
    #endregion

    #region Methods
    /// <summary>
    /// Requisitions a flat, tiling material swatch. <c>ExtendedMind.material('weathered oak planking')</c>.
    /// </summary>
    /// <remarks>
    /// Pipeline, in order:
    /// <list type="number">
    /// <item>Classify the descriptor (<see cref="Classify"/>); refuse a form request outright.</item>
    /// <item>Cache lookup on the content address. A hit costs nothing and is not charged.</item>
    /// <item>On a miss, charge the budget, then generate with the flatness constraints appended —
    /// no object, no perspective, no lighting, no shadow. Form and light are the code's job.</item>
    /// <item>Cache the 1024 master. Every delivered size is a local resample of it, so a later
    /// request at another size bills nothing.</item>
    /// <item>Measure tiling (<see cref="TilingMetrics"/>) and repair the wrap if it fails. Do not
    /// ask the model for seamlessness — it does not honour it and the failure is silent and
    /// plausible, which is the worst combination.</item>
    /// <item>Resample to <c>options.Size</c>, re-encode to WebP, and only then let the bytes exist
    /// where a script can reach them. A raw 1024 PNG master is ~1.6 MB and must never enter context.</item>
    /// </list>
    /// </remarks>
    public async Task<MaterialAsset> Material(string descriptor, MaterialOptions? options = null)
    {
        var opts = options ?? new MaterialOptions();

        var verdict = Classify(descriptor);
        if (!verdict.Allowed)
        {
            // Recorded separately from a failure, and before anything is attempted: nothing was
            // spent, nothing was reached, and the remedy is to reword rather than to retry. A run
            // that spent an hour rewording is a different run from one the service kept refusing.
            RequisitionScope.Record(new RequisitionRecord(
                "material", descriptor, Success: false, Failure: nameof(ImageGenerationFailure.RefusedFormRequest),
                Reason: verdict.Reason, Model: null, FromCache: false, Refused: true));
            RecordBudgetState();

            return new MaterialAsset
            {
                Success = false,
                Failure = ImageGenerationFailure.RefusedFormRequest,
                Error = verdict.Reason,
            };
        }

        var prompt = MaterialPrompt(descriptor);
        var model = opts.Model ?? generator?.Model ?? ImageGenerator.DefaultModel;

        var generated = await Acquire(prompt, model, "1:1", null, "material", descriptor);
        if (!generated.Success)
        {
            return new MaterialAsset
            {
                Success = false,
                Failure = generated.Failure,
                Error = generated.Error,
            };
        }

        using var master = SKBitmap.Decode(generated.ImageBytes);
        if (master is null)
        {
            return new MaterialAsset
            {
                Success = false,
                Failure = ImageGenerationFailure.NoImageReturned,
                Error = "Master decoded to nothing.",
            };
        }

        var metrics = PlateAnalysis.MeasureTiling(master);
        var source = master;
        var repaired = false;

        if (opts.Tileable && !metrics.Wraps)
        {
            // Not a prompt problem to re-ask about: the model does not honour seamlessness and the
            // failure is silent, so the guarantee is kept here or not at all.
            Info("Swatch '{Descriptor}' did not wrap (h={H:F2} v={V:F2} vs max {Max:F2}); repairing.",
                descriptor, metrics.HorizontalSeamStep, metrics.VerticalSeamStep, metrics.NeighbourMax);
            source = PlateAnalysis.MakeTileable(master);
            metrics = PlateAnalysis.MeasureTiling(source) with { Repaired = true };
            repaired = true;
        }

        using var sized = PlateAnalysis.Resize(source, Math.Clamp(opts.Size, 32, Requisitions.MaxDeliveredSize));
        if (repaired)
        {
            source.Dispose();
        }

        var asset = new MaterialAsset
        {
            Success = true,
            Id = generated.Hash,
            Bytes = PlateAnalysis.Encode(sized, opts.Format, opts.Quality),
            Size = sized.Width,
            MimeType = PlateAnalysis.MimeFor(opts.Format),
            Tiling = metrics,
            Provenance = ProvenanceOf(generated, null),
        };

        library.Add(asset);
        return asset;
    }

    /// <summary>
    /// Requisitions a background plate. <c>ExtendedMind.backdrop('starry night sky', { keepQuiet: 'lowerThird' })</c>.
    /// </summary>
    /// <remarks>
    /// The prompt this builds is mostly a negative-space contract rather than a description — no
    /// horizon, no ground, no foreground, keep region X dark — because that is what makes the result
    /// a plate rather than a picture with its own focal point.
    /// <para>
    /// Measurements are taken on receipt and returned in <see cref="PlateMetrics"/>: the requested
    /// quiet region is verified rather than assumed, and the key light is read off the plate so the
    /// foreground can match it. If the quiet region was not honoured, fail rather than hand back a
    /// plate with a mountain range where the buildings go.
    /// </para>
    /// <para>
    /// With <c>options.ConditionOn</c> set, also hash the blocking into
    /// <see cref="Provenance.BlockingHash"/>, and extend the surrounding plate inward over the baked
    /// black mask by a margin, so small silhouette edits do not expose black fringes. The mask is
    /// exactly recoverable by threshold, since the model leaves it pure black.
    /// </para>
    /// </remarks>
    public async Task<BackdropPlate> Backdrop(string descriptor, BackdropOptions? options = null)
    {
        var opts = options ?? new BackdropOptions();
        var prompt = BackdropPrompt(descriptor, opts);
        var model = opts.Model ?? generator?.Model ?? ImageGenerator.DefaultModel;
        var conditioning = opts.ConditionOn is null ? null : new[] { opts.ConditionOn };

        // The one image sent to a model that the studio did not generate, so it is held to being
        // what it claims: a silhouette, which carries no likeness. See PlateAnalysis.BlockingProblem.
        if (opts.ConditionOn is not null)
        {
            using var sent = SKBitmap.Decode(opts.ConditionOn);
            var problem = sent is null ? "it could not be decoded as an image" : PlateAnalysis.BlockingProblem(sent);
            if (problem is not null)
            {
                var reason = "conditionOn is refused: " + problem + ". It must be a blocking - the scene's "
                    + "foreground as flat black silhouettes on one flat grey - because a picture sent here reaches "
                    + "the model as something to build around, which is how a photograph or a likeness would get "
                    + "past the checks Photo applies. Fill the silhouettes #000000 on a #808080 ground, with no "
                    + "shading, and send that.";

                RequisitionScope.Record(new RequisitionRecord(
                    "backdrop", descriptor, Success: false, Failure: nameof(ImageGenerationFailure.InvalidRequest),
                    Reason: reason, Model: null, FromCache: false, Refused: true));
                RecordBudgetState();

                return new BackdropPlate { Success = false, Failure = ImageGenerationFailure.InvalidRequest, Error = reason };
            }
        }

        var generated = await Acquire(prompt, model, AspectFor(opts.Width, opts.Height), conditioning, "backdrop", descriptor);
        if (!generated.Success)
        {
            return new BackdropPlate
            {
                Success = false,
                Failure = generated.Failure,
                Error = generated.Error,
            };
        }

        using var master = SKBitmap.Decode(generated.ImageBytes);
        using var blocking = opts.ConditionOn is null ? null : SKBitmap.Decode(opts.ConditionOn);
        if (master is null)
        {
            return new BackdropPlate
            {
                Success = false,
                Failure = ImageGenerationFailure.NoImageReturned,
                Error = "Plate decoded to nothing.",
            };
        }

        var metrics = PlateAnalysis.MeasurePlate(master, opts.KeepQuiet, blocking);

        // Verified rather than assumed. A plate that ignored the quiet region will put detail exactly
        // where the foreground is about to go, and that is cheaper to catch here than to draw over.
        if (!metrics.QuietRegionHonoured)
        {
            return new BackdropPlate
            {
                Success = false,
                Failure = ImageGenerationFailure.ConstraintNotMet,
                Error = $"Requested quiet region {opts.KeepQuiet} was not delivered "
                      + $"(band luminance {string.Join(", ", metrics.BandLuminance.Select(b => b.ToString("F1")))}).",
                Metrics = metrics,
            };
        }

        var source = master;
        var extended = false;

        if (metrics.HasBakedMask)
        {
            // The model inpainted rather than filling the frame, so the plate carries a
            // foreground-shaped hole. Growing the sky inward buys tolerance for small silhouette
            // edits; it cannot make the plate reusable, which is what BoundTo records.
            Info("Plate for '{Descriptor}' came back inpainted; extending sky over the baked mask.", descriptor);
            source = PlateAnalysis.ExtendIntoMask(master);
            extended = true;
        }

        using var fitted = PlateAnalysis.FitTo(source, opts.Width, opts.Height);
        if (extended)
        {
            source.Dispose();
        }

        return new BackdropPlate
        {
            Success = true,
            Id = generated.Hash,
            Bytes = PlateAnalysis.Encode(fitted, opts.Format, opts.Quality),
            Width = fitted.Width,
            Height = fitted.Height,
            MimeType = PlateAnalysis.MimeFor(opts.Format),
            Metrics = metrics,
            Provenance = ProvenanceOf(generated, BlockingHashOf(opts.ConditionOn)),
        };
    }

    /// <summary>Requisitions a single-channel mask, height field, or displacement source.</summary>
    public async Task<MatteAsset> Matte(string descriptor, MatteOptions? options = null)
    {
        var opts = options ?? new MatteOptions();

        // Refused before any network call, and recorded the way a form refusal is: nothing spent,
        // nothing reached, and the remedy is a different call rather than a retry.
        if (NamedLikeness(descriptor) is { } who)
        {
            var reason =
                who + " reads as a real person, and a matte cannot depict one. A generated face " +
                "carries none of the identity, licence or publicity-rights checks Photo applies, and " +
                "it renders perfectly whether or not it resembles anybody — so nothing downstream can " +
                "catch it. Use Photo.of('" + who + "') for the likeness. If you meant a shape rather " +
                "than a person, name the shape and drop the name.";

            RequisitionScope.Record(new RequisitionRecord(
                "matte", descriptor, Success: false, Failure: nameof(ImageGenerationFailure.RefusedLikeness),
                Reason: reason, Model: null, FromCache: false, Refused: true));
            RecordBudgetState();

            return new MatteAsset
            {
                Success = false,
                Failure = ImageGenerationFailure.RefusedLikeness,
                Error = reason,
            };
        }

        var prompt = MattePrompt(descriptor, opts);

        var generated = await Acquire(prompt, opts.Model ?? generator?.Model ?? ImageGenerator.DefaultModel, "1:1", null, "matte", descriptor);
        if (!generated.Success)
        {
            return new MatteAsset { Success = false, Failure = generated.Failure, Error = generated.Error };
        }

        using var master = SKBitmap.Decode(generated.ImageBytes);
        if (master is null)
        {
            return new MatteAsset
            {
                Success = false,
                Failure = ImageGenerationFailure.NoImageReturned,
                Error = "Matte decoded to nothing.",
            };
        }

        // The level is measured on the master, before resampling. Resampling reintroduces intermediate
        // values along every edge, so thresholding afterwards would cut a ramp this pass just removed.
        int? cut = opts.Threshold ?? (opts.HardEdge ? PlateAnalysis.OtsuThreshold(master) : null);

        using var grey = PlateAnalysis.ToMatte(master, opts.Invert, cut);
        using var sized = PlateAnalysis.Resize(grey, Math.Clamp(opts.Size, 32, Requisitions.MaxDeliveredSize));

        return new MatteAsset
        {
            Success = true,
            Id = generated.Hash,
            Bytes = PlateAnalysis.Encode(sized, "png", 100),
            Size = sized.Width,
            Threshold = cut,
            Coverage = PlateAnalysis.Coverage(grey),
            Provenance = ProvenanceOf(generated, null),
        };
    }

    /// <summary>
    /// A mask asks for a field; a stencil asks for a shape. The words differ accordingly.
    /// </summary>
    /// <remarks>
    /// <c>fill the whole frame</c> is right for a height field or a displacement source and wrong for
    /// a silhouette, where it crops the subject at the edges — so the two cases cannot share a prompt.
    /// </remarks>
    static string MattePrompt(string descriptor, MatteOptions opts) =>
        opts.HardEdge || opts.Threshold is not null
            ? $"A bold black-and-white STENCIL of {descriptor}. Pure white silhouette on pure black. "
            + "One solid shape: no interior detail, no outline, no gradient, no shading, no grey of any kind. "
            + "The whole subject inside the frame with a small margin, centred, seen straight on."
            : $"A flat greyscale mask of {descriptor}. Pure white where the feature is, pure black "
            + "elsewhere, no colour, no lighting, no shadow, no perspective, fill the whole frame.";

    #region Cutout

    /// <summary>Requisitions one or more cut-out elements with a real alpha channel.</summary>
    /// <remarks>
    /// <b>The one requisition that returns a depiction, and the reasoning is in
    /// <see cref="CutoutOptions"/>.</b> In short: the classifier's line was never "buy no forms" —
    /// <see cref="Matte"/> already answers a form on purpose — but "buy no pictures", and a cutout is
    /// one element of a composition the code arranges.
    /// <para>
    /// <b>Why a matte could not do this.</b> Its prompt asks for no interior detail and no shading,
    /// and <see cref="PlateAnalysis.ToMatte"/> thresholds on luminance, so everything inside the
    /// subject collapses to one value. That is correct for a silhouette and leaves a face as a blank
    /// head-shaped blob. Nothing an agent could write in a descriptor reaches past it, because the
    /// wording belongs to the server.
    /// </para>
    /// </remarks>
    public async Task<CutoutAsset> Cutout(string descriptor, CutoutOptions? options = null)
    {
        var opts = options ?? new CutoutOptions();

        // Stricter here than on a matte, and for a reason the matte case only approaches: a cutout
        // carries interior detail, so a generated likeness of a named person is not a silhouette
        // that happens to resemble somebody - it is a fabricated portrait that renders perfectly.
        if (NamedLikeness(descriptor) is { } who)
        {
            var reason =
                who + " reads as a real person, and a cutout depicts rather than outlines - so this " +
                "would be a fabricated likeness carrying none of the identity, licence or " +
                "publicity-rights checks Photo applies, and nothing downstream could catch it. Use " +
                "Photo.of('" + who + "'). If you meant an anonymous figure, describe the figure and " +
                "drop the name.";

            RequisitionScope.Record(new RequisitionRecord(
                "cutout", descriptor, Success: false, Failure: nameof(ImageGenerationFailure.RefusedLikeness),
                Reason: reason, Model: null, FromCache: false, Refused: true));
            RecordBudgetState();

            return new CutoutAsset
            {
                Success = false,
                Failure = ImageGenerationFailure.RefusedLikeness,
                Error = reason,
            };
        }

        // **The ground is not the caller's to set, and a style that sets one defeats the key.**
        // Found on the first live run: `style` carried "white background", the model obliged both
        // instructions and drew each figure on its own white card inside the magenta gutters. The
        // gutters keyed out and the cards did not, so four cells came back 99.7% opaque - white
        // rectangles with drawings on them. Refused here rather than warned about, because it is
        // decidable before the network is touched and the result is otherwise indistinguishable
        // from a subject that happens to be pale.
        if (NamedGround(opts.Style) is { } word)
        {
            var reason =
                "cutout style names a " + word + ", and the ground belongs to the call: the subject "
                + "is generated on a flat keyed background so that it can be cut out, and a style "
                + "that asks for one too produces a subject sitting on an opaque card. Describe how "
                + "the subject is drawn and leave the ground alone; check cell coverage afterwards.";

            RequisitionScope.Record(new RequisitionRecord(
                "cutout", descriptor, Success: false, Failure: nameof(ImageGenerationFailure.InvalidRequest),
                Reason: reason, Model: null, FromCache: false, Refused: true));
            RecordBudgetState();

            return new CutoutAsset
            {
                Success = false,
                Failure = ImageGenerationFailure.InvalidRequest,
                Error = reason,
            };
        }

        // A reference is resolved through the cache, never read off the object passed: what makes it
        // safe is that the cache only ever holds what this studio generated.
        var (referenceIds, referenceError) = ReferenceIdsOf(opts.Reference);
        List<byte[]> references = [];
        foreach (var id in referenceIds)
        {
            if (await cache.Get(id) is { ImageBytes: { Length: > 0 } bytes })
            {
                references.Add(bytes);
                continue;
            }

            referenceError ??= $"reference '{id}' is not an image this project generated. A reference must be a "
                + "cutout requisitioned in this project, one of its cells, or its id. A photograph, a drawing or a "
                + "canvas cannot be passed, because a reference is how a likeness would enter without the checks "
                + "Photo applies.";
        }

        if (referenceError is not null)
        {
            RequisitionScope.Record(new RequisitionRecord(
                "cutout", descriptor, Success: false, Failure: nameof(ImageGenerationFailure.InvalidRequest),
                Reason: referenceError, Model: null, FromCache: false, Refused: true));
            RecordBudgetState();

            return new CutoutAsset { Success = false, Failure = ImageGenerationFailure.InvalidRequest, Error = referenceError };
        }

        if (KeyGround(opts) is null)
        {
            var reason = $"keyColor '{opts.KeyColor}' is not one of {string.Join(", ", KeyGrounds.Keys)}.";
            RequisitionScope.Record(new RequisitionRecord(
                "cutout", descriptor, Success: false, Failure: nameof(ImageGenerationFailure.InvalidRequest),
                Reason: reason, Model: null, FromCache: false, Refused: true));
            RecordBudgetState();

            return new CutoutAsset { Success = false, Failure = ImageGenerationFailure.InvalidRequest, Error = reason };
        }

        var variants = (opts.Variants ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Take(6).ToList();
        var prompt = CutoutPrompt(descriptor, opts, variants, references.Count > 0);
        var aspect = variants.Count > 2 ? "16:9" : variants.Count == 2 ? "4:3" : "1:1";

        var generated = await Acquire(prompt, opts.Model ?? generator?.Model ?? ImageGenerator.DefaultModel,
            aspect, references.Count > 0 ? references : null, "cutout", descriptor);
        if (!generated.Success)
        {
            return new CutoutAsset { Success = false, Failure = generated.Failure, Error = generated.Error };
        }

        using var master = SKBitmap.Decode(generated.ImageBytes);
        if (master is null)
        {
            return new CutoutAsset
            {
                Success = false,
                Failure = ImageGenerationFailure.NoImageReturned,
                Error = "Cutout decoded to nothing.",
            };
        }

        // A named ground is keyed by hue, which the model's drift toward grey or pink cannot fool; an
        // explicit background colour could be anything, so it keeps the distance key it was written for.
        var background = ParseHex(opts.Background) ?? PlateAnalysis.SampleBackground(master);
        using var keyed = (opts.Background is null
                              ? PlateAnalysis.DifferenceKey(master, background, KeyGround(opts)!.Value.Name, opts.Tolerance)
                              : null)
                          ?? PlateAnalysis.ChromaKey(master, background, opts.Tolerance);

        var (cells, split) = SplitSheet(keyed, variants, opts.Size, generated.Hash);

        return new CutoutAsset
        {
            Success = true,
            Id = generated.Hash,
            References = referenceIds,
            KeyColor = KeyGround(opts)!.Value.Name,
            Bytes = PlateAnalysis.Encode(keyed, "png", 100),
            Width = keyed.Width,
            Height = keyed.Height,
            Cells = cells,
            Split = split,
            BackgroundColor = $"#{background.Red:X2}{background.Green:X2}{background.Blue:X2}",
            Provenance = ProvenanceOf(generated, null),
        };
    }

    /// <summary>Divides a keyed sheet into one cell per variant, reporting how it managed it.</summary>
    /// <remarks>
    /// Gaps first, because a model does not lay a row out on a grid and equal columns cut through
    /// shoulders. Equal columns only when the gaps do not yield the count asked for, and the caller
    /// is told which happened - a mis-split renders perfectly, so it cannot be left to be noticed.
    /// </remarks>
    static (List<CutoutCell> Cells, string Split) SplitSheet(
        SKBitmap keyed, List<string> variants, int size, string sheetId)
    {
        if (variants.Count <= 1)
        {
            return ([CellOf(keyed, 0, keyed.Width - 1, variants.FirstOrDefault() ?? "subject", size, sheetId)], "single");
        }

        var runs = PlateAnalysis.SegmentsByGaps(PlateAnalysis.AlphaColumnProfile(keyed));
        var split = runs.Length == variants.Count ? "gaps" : "even";

        if (split == "even")
        {
            var step = keyed.Width / (double)variants.Count;
            runs = [.. Enumerable.Range(0, variants.Count)
                .Select(i => ((int)Math.Round(i * step), (int)Math.Round(((i + 1) * step) - 1)))];
        }

        return ([.. runs.Select((r, i) => CellOf(keyed, r.Start, r.End, variants[i], size, sheetId))], split);
    }

    /// <summary>One column range of the sheet, trimmed to its own extent and scaled to fit.</summary>
    static CutoutCell CellOf(SKBitmap keyed, int from, int to, string name, int size, string sheetId)
    {
        var width = Math.Max(1, to - from + 1);
        using var slice = new SKBitmap(width, keyed.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (var canvas = new SKCanvas(slice))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(keyed, new SKRect(from, 0, to + 1, keyed.Height),
                new SKRect(0, 0, width, keyed.Height), PlateAnalysis.Sampling, null);
        }

        using var trimmed = PlateAnalysis.TrimToAlpha(slice);
        var longest = Math.Max(trimmed.Width, trimmed.Height);
        var scale = longest <= size ? 1.0 : size / (double)longest;

        using var sized = scale >= 1.0
            ? trimmed.Copy()
            : PlateAnalysis.Resize(trimmed, Math.Max(1, (int)Math.Round(trimmed.Width * scale)),
                Math.Max(1, (int)Math.Round(trimmed.Height * scale)));

        return new CutoutCell
        {
            Name = name,
            SheetId = sheetId,
            Bytes = PlateAnalysis.Encode(sized, "png", 100),
            Width = sized.Width,
            Height = sized.Height,
            Coverage = PlateAnalysis.AlphaCoverage(sized),
            Holes = PlateAnalysis.EnclosedTransparency(sized),
        };
    }

    /// <summary>
    /// A sheet asks for the same subject several times; a single asks for it once.
    /// </summary>
    /// <remarks>
    /// Every clause here is load-bearing. The flat keyable ground is what makes an alpha channel
    /// possible at all; forbidding a cast shadow and a ground plane stops the key taking a bite out
    /// of the subject's feet; forbidding labels stops a "character sheet" arriving annotated, which
    /// is what the phrase means to a model trained on real ones. And <i>identical in every respect
    /// except</i> is the sentence the whole consistency argument rests on.
    /// </remarks>
    static string CutoutPrompt(string descriptor, CutoutOptions opts, List<string> variants, bool referenced = false)
    {
        var (name, hex) = KeyGround(opts) ?? ("magenta", "#FF00FF");
        var ground =
            $"Flat, perfectly uniform pure {name} ({hex}) background filling everything around the "
            + "subject. No cast shadow, no ground plane, no horizon, no vignette, no text, no labels, "
            + "no numbers, no frame, no border, no panel divisions.";

        // Said first, because it governs everything after it. It names what to KEEP rather than asking
        // for nothing to change: "nothing about its design changed" was read as the whole drawing, and a
        // live run came back a near-copy of its reference, framing included. "Maintain that face and that
        // costume" is the wording AI Cinematic Filmmaking ch. 8 gives for this model, and with it a
        // referenced pose came back genuinely new and still the same person.
        var reference = referenced
            ? "The attached image shows this character. Maintain that face and that costume exactly: the same "
                + "features, hair, build, clothing and colours. The pose, expression, angle and framing come from "
                + "this description, not from the attached image. "
            : string.Empty;

        var framing = FramingSentence(opts.Framing);

        if (variants.Count <= 1)
        {
            return $"{reference}A single {opts.Style} of {descriptor}, the whole subject within the frame with a "
                + $"clear margin on every side, centred, seen straight on. {framing}{ground}";
        }

        var listed = string.Join("; ", variants.Select((v, i) => $"{i + 1}. {v}"));
        return $"{reference}A reference sheet of {variants.Count} {opts.Style}s of {descriptor}, arranged in ONE "
            + "horizontal row, evenly spaced, with a clear band of background between each and the "
            + "next so that none of them touch or overlap. It is the SAME subject in every one, "
            + "identical in every respect except as listed, left to right: " + listed + ". " + framing + ground;
    }

    /// <summary>The framing as its own sentence: a preset expanded, a phrase of the caller's kept as written.</summary>
    static string FramingSentence(string? framing) => framing?.Trim().ToLowerInvariant() switch
    {
        null or "" => string.Empty,
        "head" => "Framing, for every one: close-up portrait, head and shoulders only, cropped at the upper chest, "
            + "the face large in the frame. ",
        "full" => "Framing, for every one: full body, the whole figure in frame from head to feet. ",
        _ => $"Framing, for every one: {framing!.Trim().TrimEnd('.')}. ",
    };

    /// <summary>The keyable grounds a cutout may be drawn on.</summary>
    static readonly Dictionary<string, string> KeyGrounds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["green"] = "#00FF00",
        ["magenta"] = "#FF00FF",
        ["blue"] = "#0000FF",
    };

    /// <summary>The ground a cutout asks for, or null when <see cref="CutoutOptions.KeyColor"/> names none.</summary>
    /// <remarks>Green by default for a head sheet, because skin is most of a head and skin is never green.</remarks>
    static (string Name, string Hex)? KeyGround(CutoutOptions opts)
    {
        var name = string.IsNullOrWhiteSpace(opts.KeyColor)
            ? string.Equals(opts.Framing?.Trim(), "head", StringComparison.OrdinalIgnoreCase) ? "green" : "magenta"
            : opts.KeyColor.Trim().ToLowerInvariant();
        return KeyGrounds.TryGetValue(name, out var hex) ? (name, hex) : null;
    }

    /// <summary>The sheet ids a <see cref="CutoutOptions.Reference"/> names, or why it names none usable.</summary>
    /// <remarks>
    /// Ids only: the bytes are fetched from the cache by the caller, so an object whose bytes were
    /// swapped for a photograph still resolves to what the studio generated under that id.
    /// </remarks>
    internal static (List<string> Ids, string? Error) ReferenceIdsOf(object? reference)
    {
        const int MaxReferences = 3;
        if (reference is null)
        {
            return ([], null);
        }

        IEnumerable<object?> items = reference is string or CutoutAsset or CutoutCell || reference is not IEnumerable many
            ? [reference]
            : many.Cast<object?>();

        List<string> ids = [];
        foreach (var item in items)
        {
            var id = item switch
            {
                CutoutAsset { Success: true } sheet => sheet.Id,
                CutoutAsset => null,
                CutoutCell cell => cell.SheetId,
                string text => text.Trim(),
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(id))
            {
                return ([], item switch
                {
                    CutoutAsset => "reference is a cutout that failed, so it has no image to show the model.",
                    null => "reference contains nothing. Pass a cutout, one of its cells, or its id.",
                    _ => $"reference is a {item.GetType().Name}, and only a cutout, one of its cells, or its id can "
                        + "be a reference: a reference must be an image this project generated, so that a "
                        + "photograph or a drawing cannot carry a likeness past the checks Photo applies.",
                });
            }

            if (!ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids.Count > MaxReferences
            ? ([], $"reference names {ids.Count} sheets and at most {MaxReferences} can be shown to the model. Pass the one that shows the subject best.")
            : (ids, null);
    }

    /// <summary>The word in a style that claims the ground, or null.</summary>
    /// <remarks>
    /// Deliberately two words and no more. <c>background</c> and <c>backdrop</c> in a <i>style</i>
    /// can only be about the ground, which this call owns; anything looser starts refusing legitimate
    /// descriptions - "white shirt", "against the light" - and a check that cries wolf is one a
    /// caller works around rather than reads.
    /// </remarks>
    static string? NamedGround(string? style) =>
        string.IsNullOrWhiteSpace(style) ? null
        : style.Contains("background", StringComparison.OrdinalIgnoreCase) ? "background"
        : style.Contains("backdrop", StringComparison.OrdinalIgnoreCase) ? "backdrop"
        : null;

    /// <summary>A <c>#RRGGBB</c> string as a colour, or null when unparseable or absent.</summary>
    static SKColor? ParseHex(string? hex) =>
        !string.IsNullOrWhiteSpace(hex) && SKColor.TryParse(hex, out var c) ? c : null;

    #endregion

    /// <summary>Cache lookup, budget check, then generation. The only path that can spend money.</summary>
    /// <remarks>
    /// Also the only place worth recording from. Every requisition passes through here whatever it
    /// asked for, and here is where the two facts a reader wants are both known: whether an image came
    /// back, and whether it cost anything. <paramref name="kind"/> and <paramref name="descriptor"/>
    /// are carried in from the caller purely so the record can name what was asked for in the words
    /// the script used, rather than in the elaborated prompt this layer built from them.
    /// </remarks>
    async Task<ImageGenerationResult> Acquire(string prompt, string model, string? aspect,
        IReadOnlyList<byte[]>? conditionOn, string kind, string descriptor)
    {
        // Read off the budget rather than off the result: a cached result is a *replay* of a charged
        // one and carries its Charged flag with it, so asking the result whether it cost anything
        // gets the answer for the generation it came from rather than for this call.
        var hitsBefore = Budget.CacheHits;
        var result = await AcquireCore(prompt, model, aspect, conditionOn, kind, descriptor);

        RequisitionScope.Record(new RequisitionRecord(
            kind, descriptor, result.Success,
            Failure: result.Success ? null : result.Failure.ToString(),
            Reason: result.Success ? null : result.Error,
            Model: model,
            FromCache: Budget.CacheHits > hitsBefore,
            Refused: false));
        RecordBudgetState();

        return result;
    }

    /// <summary>Pushes this toolkit's allowance into the ambient scope, if one is collecting.</summary>
    /// <remarks>
    /// Pushed rather than read back, because the engine substitutes a disabled toolkit of its own when
    /// no requisition surface is configured. A caller reading the budget off the statically configured
    /// toolkit would be describing an object the script never touched.
    /// </remarks>
    void RecordBudgetState() => RequisitionScope.RecordBudget(
        new BudgetSnapshot(Budget.Total, Budget.Spent, Budget.Remaining, Budget.CacheHits, Budget.TokensSpent));

    /// <remarks>
    /// <b>Only a material may be served by a near match</b>, and only by one of the same model. The
    /// fuzzy fallback compared the whole elaborated prompt against every stored prompt and so hit
    /// requests of every kind, until 2026-09-24. A cutout prompt is mostly fixed wording and the
    /// tokeniser dropped short words such as <c>3</c> and <c>4</c>, so a retry with a new pose and one
    /// fewer variant was served the first sheet split into the wrong number of cells, and reported as
    /// a cache hit. A matte of another shape, or a backdrop drawn to another blocking, fails the same
    /// way. A material is the one kind where two wordings are the same request.
    /// </remarks>
    async Task<ImageGenerationResult> AcquireCore(string prompt, string model, string? aspect,
        IReadOnlyList<byte[]>? conditionOn, string kind, string descriptor)
    {
        if (this.generator is null)
        {
            return ImageGenerationResult.Failed(
                ImageGenerationFailure.NotConfigured,
                "No image-generation credentials are configured for this studio.", prompt, model);
        }

        var hash = ImageGenerator.HashOf(model, prompt, aspect, conditionOn);

        var cached = await cache.Get(hash)
            ?? (kind == "material" ? await cache.FindSimilar(kind, model, descriptor) : null);
        if (cached is not null)
        {
            Budget.CacheHits++;
            return cached;
        }

        if (!Budget.CanAfford())
        {
            return ImageGenerationResult.Failed(
                ImageGenerationFailure.BudgetExhausted,
                $"Budget exhausted ({Budget.Spent}/{Budget.Total} spent).", prompt, model);
        }

        var result = await generator.GenerateImage(prompt, model, aspect, conditionOn);

        // Only a completed generation is charged; a network fault must not consume the allowance.
        if (result.Charged)
        {
            Budget.Spent++;
            Budget.TokensSpent += result.TotalTokens ?? 0;
            await cache.Put(result, kind, descriptor);
        }

        return result;
    }

    static string MaterialPrompt(string descriptor) =>
        $"A flat material swatch of {descriptor}.\n"
        + "This is a FLAT TEXTURE SAMPLE ONLY, photographed straight on: no object, no scene, "
        + "no horizon, no sky, no shadows, no highlights, no vignette, no perspective. "
        + "Completely flat even lighting. Fill the whole frame with the material.";

    static string BackdropPrompt(string descriptor, BackdropOptions opts)
    {
        var sb = new StringBuilder();

        if (opts.ConditionOn is not null)
        {
            sb.Append("The attached image is the BLOCKING of a scene already drawn in code. Black shapes are "
                    + "foreground silhouettes that will be drawn on top; grey is empty sky you must fill. "
                    + "Compose for THIS blocking: put the brightest, most detailed region where the blocking "
                    + "leaves the most open space, and keep it simple and dark where the silhouettes are tall.\n");
        }

        sb.Append($"Generate a BACKGROUND PLATE: {descriptor}.\n");

        // The constraints, not the description, are what make this a plate rather than a picture.
        if (opts.NoForeground)
        {
            sb.Append("No foreground objects of any kind: no buildings, trees, people, or creatures. ");
        }

        if (opts.NoHorizon)
        {
            sb.Append("No horizon line and no ground plane. ");
        }

        sb.Append(opts.KeepQuiet switch
        {
            QuietRegion.LowerThird => "The LOWER THIRD must be near-black and empty of detail so foreground silhouettes can be drawn over it. ",
            QuietRegion.UpperThird => "The UPPER THIRD must be near-black and empty of detail. ",
            QuietRegion.LeftHalf => "The LEFT HALF must stay dark and simple. ",
            QuietRegion.RightHalf => "The RIGHT HALF must stay dark and simple. ",
            QuietRegion.Center => "The CENTRE must stay dark and simple. ",
            _ => string.Empty,
        });

        sb.Append("Flat and even, no vignette.");
        return sb.ToString();
    }

    /// <summary>Nearest aspect the service accepts. Anything else is silently reinterpreted.</summary>
    static string AspectFor(int width, int height)
    {
        var target = (double)width / Math.Max(1, height);
        return Aspects.MinBy(a => Math.Abs(a.Ratio - target)).Name;
    }

    static string? BlockingHashOf(byte[]? blocking) => blocking is null
        ? null
        : Convert.ToHexString(SHA256.HashData(blocking))[..16];

    Provenance ProvenanceOf(ImageGenerationResult generated, string? blockingHash) => new()
    {
        Model = generated.Model,
        Hash = generated.Hash,
        BlockingHash = blockingHash,
        Requester = requester,
        GeneratedUtc = generated.GeneratedUtc,
        FromCache = generated.FromCache,
        Prompt = generated.Prompt,
    };

    /// <summary>
    /// The form-versus-substance check. Runs before any generation and costs nothing.
    /// </summary>
    /// <remarks>
    /// The failure this catches is requesting <i>form</i> where the agent should be requesting
    /// <i>substance</i>. "Weathered oak planking" is a mass noun with no silhouette, so code supplies
    /// the shape. "A wooden ship" is a count noun with an outline, and asking for it hands
    /// structural work to a model that the drawing toolkit does better and controllably. A descriptor
    /// naming something with an outline is a form request wearing a material's clothes.
    /// </remarks>
    public static RequisitionVerdict Classify(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            return new RequisitionVerdict { Class = RequisitionClass.Form, Reason = "Empty descriptor." };
        }

        var text = descriptor.ToLowerInvariant();

        // Compare by word position rather than character index, and let a noun match the tail of a
        // closed compound: "sailboat" is a boat and "limestone" is stone, but a whole-word test sees
        // neither. Without this the guard missed every compound object it exists to catch.
        var words = WordRegex().Matches(text).Select(m => m.Value).ToArray();
        var hits = new List<string>();
        var lastForm = -1;
        var lastSubstance = -1;

        for (var i = 0; i < words.Length; i++)
        {
            // Substance wins the word. The compound rule is a suffix test, so "surface" ends with
            // "face" and "sandstone" ends with "stone"; a word already known as a material is not
            // also an object, and checking that first is what stops "brushed copper surface" from
            // reading as a request for a face.
            if (SubstanceCues.Any(c => IsNounMatch(words[i], c)))
            {
                lastSubstance = i;
                continue;
            }

            // An exact match is preferred over a suffix one, because the trigger is reported to the
            // agent and must be a word it can find in its own descriptor. Taking the first match in
            // list order made "woman" report as "man" — "man" is a suffix of it and sits earlier in
            // FormNouns — so the remedy named a word the descriptor did not contain.
            var form = FormNouns.FirstOrDefault(n => words[i] == n || words[i] == n + "s")
                    ?? FormNouns.FirstOrDefault(n => IsNounMatch(words[i], n));
            if (form is not null)
            {
                hits.Add(form);
                lastForm = i;
            }
        }

        hits = hits.Distinct().ToList();

        // "a ship made of oak" puts the material last but is still a request for the ship. When a
        // form noun precedes one of these connectives, the head-noun rule does not apply.
        // Measured in words, to stay comparable with lastForm / lastSubstance above.
        var connective = Connectives
            .Select(k => text.IndexOf(k, StringComparison.Ordinal))
            .Where(i => i >= 0)
            .Select(i => WordRegex().Matches(text).Count(m => m.Index < i))
            .DefaultIfEmpty(-1)
            .Max();

        if (hits.Count > 0 && lastSubstance > lastForm && !(connective > lastForm && connective >= 0))
        {
            return new RequisitionVerdict
            {
                Class = RequisitionClass.Substance,
                Reason = "Head noun is a material; the object named is an attributive modifier.",
            };
        }

        if (hits.Count > 0)
        {
            return new RequisitionVerdict
            {
                Class = RequisitionClass.Form,
                Triggers = [.. hits],
                Reason = $"'{string.Join("', '", hits)}' names a thing with an outline. Draw the form with the "
                       + "drawing toolkit and requisition its surface instead — e.g. the planking, not the ship.",
            };
        }

        // An article in front of a bare noun usually means one countable object rather than a substance.
        var article = ArticlePrefixes.FirstOrDefault(a => text.StartsWith(a, StringComparison.Ordinal));
        if (article is not null && !SubstanceCues.Any(c => ContainsWord(text, c)))
        {
            return new RequisitionVerdict
            {
                Class = RequisitionClass.Ambiguous,
                Triggers = [article.Trim()],
                Reason = "Reads as a single object rather than a material. Allowed, but recorded for review.",
            };
        }

        return new RequisitionVerdict { Class = RequisitionClass.Substance, Reason = "Reads as a material." };
    }

    /// <summary>
    /// Whether <paramref name="word"/> is <paramref name="noun"/>, its plural, or a closed compound
    /// ending in it — "sailboat" is a boat, "sandstone" is stone.
    /// </summary>
    /// <remarks>
    /// The compound rule is a suffix test, so it also catches words that merely end in the same
    /// letters without being one: "craftsmanship" is not a ship. Those are listed in
    /// <see cref="CompoundExceptions"/> rather than given a cleverer rule, because the set is small,
    /// well understood, and an explicit list is inspectable when a descriptor is misjudged.
    /// </remarks>
    static bool IsNounMatch(string word, string noun)
    {
        if (word == noun || word == noun + "s")
        {
            return true;
        }

        if (word.Length <= noun.Length || CompoundExceptions.Contains(word))
        {
            return false;
        }

        return word.EndsWith(noun, StringComparison.Ordinal)
            || word.EndsWith(noun + "s", StringComparison.Ordinal);
    }

    /// <summary>Index of the last whole-word occurrence of <paramref name="word"/>, or -1.</summary>
    static int LastWordIndex(string haystack, string word)
    {
        var best = -1;
        var i = haystack.IndexOf(word, StringComparison.Ordinal);
        while (i >= 0)
        {
            var end = i + word.Length;
            if ((i == 0 || !char.IsLetter(haystack[i - 1])) &&
                (end >= haystack.Length || !char.IsLetter(haystack[end])))
            {
                best = i;
            }

            i = haystack.IndexOf(word, i + 1, StringComparison.Ordinal);
        }

        return best;
    }

    static bool ContainsWord(string haystack, string word)
    {
        var i = haystack.IndexOf(word, StringComparison.Ordinal);
        while (i >= 0)
        {
            var beforeOk = i == 0 || !char.IsLetter(haystack[i - 1]);
            var end = i + word.Length;
            var afterOk = end >= haystack.Length || !char.IsLetter(haystack[end]);
            if (beforeOk && afterOk)
            {
                return true;
            }

            i = haystack.IndexOf(word, i + 1, StringComparison.Ordinal);
        }

        return false;
    }
    #endregion

    #region Fields
    static readonly string[] FormNouns =
    [
        "ship", "boat", "vessel", "building", "house", "tower", "castle", "car", "vehicle",
        "person", "man", "woman", "figure", "character", "face", "portrait", "creature",
        "animal", "tree", "mountain", "landscape", "scene", "logo", "icon", "illustration",
        "painting", "drawing", "artwork", "poster", "sword", "weapon", "robot", "dragon",
    ];

    static readonly (string Name, double Ratio)[] Aspects =
    [
        ("1:1", 1.0), ("2:3", 2.0 / 3), ("3:2", 1.5), ("3:4", 0.75), ("4:3", 4.0 / 3),
        ("9:16", 9.0 / 16), ("16:9", 16.0 / 9), ("21:9", 21.0 / 9),
    ];

    static readonly string[] Connectives =
    [
        "made of", "made from", "out of", "carved from", "carved of", "built of", "built from",
    ];

    static readonly string[] ArticlePrefixes = ["a ", "an ", "the ", "one "];

    /// <summary>
    /// Words ending in the letters of a form noun without being one. Mostly the abstract
    /// <c>-ship</c> nouns, which are common in design prose ("fine craftsmanship") and would
    /// otherwise refuse a perfectly good material request.
    /// </summary>
    static readonly HashSet<string> CompoundExceptions = new(StringComparer.Ordinal)
    {
        "craftsmanship", "workmanship", "penmanship", "seamanship", "sportsmanship", "showmanship",
        "friendship", "relationship", "partnership", "membership", "ownership", "leadership",
        "championship", "apprenticeship", "hardship", "worship", "township",
        "german", "germanic", "roman", "romanesque", "talisman", "ottoman",
        // -face words that are not faces. "typeface" matters for a logotype brief.
        "typeface", "interface", "preface", "boldface", "surface",
        "silicon", "obscene", "imposter",
    };

    static readonly string[] SubstanceCues =
    [
        "texture", "material", "swatch", "surface", "grain", "finish", "pattern", "weave",
        "fabric", "cloth", "canvas", "leather", "velvet", "silk", "wool", "denim",
        "stone", "marble", "granite", "slate", "brick", "concrete", "plaster", "tile",
        "metal", "steel", "iron", "copper", "brass", "rust", "patina",
        "wood", "oak", "pine", "planking", "planks", "boards", "bark",
        "paper", "parchment", "sand", "snow", "ice", "water", "cloud", "smoke", "sky",
        "paint", "ink", "enamel", "lacquer", "moss", "lichen", "dirt", "gravel",
        // Timber and marine surfaces. The head-noun rule can only rescue "teak boat decking" if the
        // head is recognised as a material, so the vocabulary has to reach as wide as the form list.
        "teak", "mahogany", "walnut", "birch", "cedar", "ash", "elm", "timber", "veneer", "plywood",
        "decking", "deck", "grain", "knot", "sawdust", "varnish", "shellac", "tar", "pitch", "oakum",
        "rope", "cordage", "twine", "hemp", "jute", "sisal", "rattan", "wicker", "cork",
        "linen", "sailcloth", "burlap", "hessian", "muslin", "twill", "tweed", "corduroy", "felt",
        "suede", "hide", "shagreen", "gauze", "mesh", "knit", "thread", "fibre", "fiber",
        // Hard surfaces and finishes a brand or product board reaches for.
        "clay", "terracotta", "porcelain", "ceramic", "glaze", "glass", "crystal",
        "bronze", "pewter", "chrome", "nickel", "zinc", "aluminium", "aluminum", "gold", "silver",
        "foam", "rubber", "vinyl", "plastic", "resin", "acrylic", "wax", "gesso",
        "chalk", "charcoal", "graphite", "pastel", "pigment", "dye", "varnishing",
        "shale", "basalt", "limestone", "sandstone", "quartz", "obsidian", "flint", "pebble",
        "grass", "foliage", "leaf", "petal", "husk", "straw", "thatch", "reed",
    ];

    [GeneratedRegex(@"[a-z]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    readonly IImageGenerator? generator;
    readonly IRequisitionCache cache;
    readonly string requester;
    readonly List<MaterialAsset> library = [];
    #endregion
    /// <summary>Words that make a descriptor about somebody's face rather than about a shape.</summary>
    /// <remarks>
    /// A name alone is not enough to refuse on: "Golden Gate", "Art Deco" and "Ben Day" are all
    /// name-shaped and none is a person. A face word alone is not enough either — "bearded film
    /// director portrait" is a generic figure and a legitimate graphic. It is the pair that means
    /// somebody in particular, and the pair is what a caption then presents as real.
    /// </remarks>
    static readonly string[] FaceWords =
        ["portrait", "likeness", "headshot", "face", "bust", "head", "profile", "selfie"];

    /// <summary>
    /// The name in a descriptor that asks a matte for a real person's likeness, or null.
    /// </summary>
    /// <remarks>
    /// <b>Why this is a refusal rather than a paragraph.</b> That a matte is not the route to a real
    /// person is already written in <c>polson://sdk/core/Assets</c> and in the vector infographic
    /// instructions. A live run read "a stencil of the author" in its brief and went round the
    /// guidance three times — <c>Assets.matte('Stanley Kubrick bearded director portrait with
    /// camera')</c> and two rewordings — until one came back. The finished piece carried an invented
    /// face under the heading "STANLEY KUBRICK (1928 — 1999)", with a dated caption and a source line
    /// beneath it, and every other integrity check on the page passed.
    /// <para>
    /// The form-versus-substance classifier deliberately does not run here, because a matte <i>is</i>
    /// a silhouette — so nothing refused it. This is the missing rung.
    /// </para>
    /// <para>
    /// <b>Both signals are required.</b> Refusing on a name alone would turn away "Art Deco fan
    /// motif"; refusing on a face word alone would turn away the anonymous figure a section marker
    /// legitimately wants. Requiring the pair keeps the check narrow enough to be worth obeying, and
    /// the message says how to proceed either way.
    /// </para>
    /// </remarks>
    internal static string? NamedLikeness(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor)) return null;

        var match = PersonalName().Match(descriptor);
        if (!match.Success) return null;

        return FaceWords.Any(w => descriptor.Contains(w, StringComparison.OrdinalIgnoreCase))
            ? match.Value
            : null;
    }

    /// <summary>Two or more adjacent capitalised words: the shape a personal name takes.</summary>
    [GeneratedRegex(@"\p{Lu}\p{Ll}+(?:\s+\p{Lu}\p{Ll}+)+")]
    private static partial Regex PersonalName();
}

/// <summary>
/// Content-addressed store of 1024 masters.
/// </summary>
/// <remarks>
/// The highest-leverage piece of the whole feature. Generation is metered and non-deterministic, and
/// the loop this exists to speed up is retry-heavy; without a cache every re-run of a script re-bills
/// for pixels already bought and no script is reproducible. Keying on the 1024 master rather than on
/// the delivered size matters because a requisition always buys 1024 whatever size was asked for, so
/// every other size is free once the master is held.
/// </remarks>
public interface IRequisitionCache
{
    Task<ImageGenerationResult?> Get(string hash);

    /// <summary>Stores a master. <paramref name="kind"/> and <paramref name="descriptor"/> make it findable by <see cref="FindSimilar"/>.</summary>
    Task Put(ImageGenerationResult image, string? kind = null, string? descriptor = null);

    /// <summary>Near-duplicate lookup: distinct wordings of one material need should not bill twice.</summary>
    /// <remarks>
    /// A hash catches identical prompts and nothing else. "weathered oak planks" and "old wooden
    /// boards" are different strings and the same material. Closing that gap needs judgement or an
    /// embedding, which is the concrete reason an Asset Manager role earns its keep.
    /// <para>
    /// Matches only a record of the same <paramref name="kind"/> and <paramref name="model"/>, and
    /// compares the caller's descriptors rather than the elaborated prompts, whose fixed wording would
    /// otherwise dominate the overlap.
    /// </para>
    /// </remarks>
    Task<ImageGenerationResult?> FindSimilar(string kind, string model, string descriptor, double threshold = 0.9);
}
