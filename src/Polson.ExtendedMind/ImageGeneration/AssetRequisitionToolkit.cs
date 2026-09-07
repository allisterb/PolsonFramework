namespace Polson.ExtendedMind.ImageGeneration;

using System;
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
        var result = await AcquireCore(prompt, model, aspect, conditionOn);

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

    async Task<ImageGenerationResult> AcquireCore(string prompt, string model, string? aspect, IReadOnlyList<byte[]>? conditionOn)
    {
        if (this.generator is null)
        {
            return ImageGenerationResult.Failed(
                ImageGenerationFailure.NotConfigured,
                "No image-generation credentials are configured for this studio.", prompt, model);
        }

        var hash = ImageGenerator.HashOf(model, prompt, aspect, conditionOn);

        var cached = await cache.Get(hash) ?? await cache.FindSimilar(prompt);
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
            await cache.Put(result);
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

    Task Put(ImageGenerationResult image);

    /// <summary>Near-duplicate lookup: distinct wordings of one material need should not bill twice.</summary>
    /// <remarks>
    /// A hash catches identical prompts and nothing else. "weathered oak planks" and "old wooden
    /// boards" are different strings and the same material. Closing that gap needs judgement or an
    /// embedding, which is the concrete reason an Asset Manager role earns its keep.
    /// </remarks>
    Task<ImageGenerationResult?> FindSimilar(string descriptor, double threshold = 0.9);
}
