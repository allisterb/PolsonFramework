namespace Polson.Tests.ExtendedMind;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Polson.Tests;
using Polson.ExtendedMind.ImageGeneration;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Whether a cutout shown an earlier one draws the same subject, against the real image model.
/// </summary>
/// <remarks>
/// <b>Live and billed: two generations.</b> Runs only with <c>POLSON_LIVE_IMAGE_TESTS=1</c> and a key in
/// <c>testappsettings.json</c>. The images are written to <c>POLSON_LIVE_OUT</c> (a temp folder otherwise)
/// because the question is one only looking can answer: the assertions say both calls succeeded and the
/// second was shown the first, not that the model kept the likeness. The cache lives in the same folder,
/// so running it again costs nothing and returns the same two sheets.
/// </remarks>
public class CutoutReferenceLiveTests : TestsRuntime
{
    readonly ITestOutputHelper output;
    readonly string? apiKey;

    public CutoutReferenceLiveTests(ITestOutputHelper output)
    {
        this.output = output;
        this.apiKey = config["ApiKeys:GoogleAgentPlatform"];
    }

    [Fact]
    public async Task Cutout_Live_AReferenceIsShownTheEarlierSheet()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;   // no key configured: nothing to exercise
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;   // opt-in: this one bills

        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT")
            ?? Path.Combine(Path.GetTempPath(), "polson-reference-live");
        Directory.CreateDirectory(dir);

        using var generator = new ImageGenerator(this.apiKey!);
        var budget = new AssetBudget(2);
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(Path.Combine(dir, "cache")), budget, "live");
        const string Style = "clean storyboard illustration, even light, no cast shadow";

        var turnaround = await toolkit.Cutout(
            "a heavyset lighthouse keeper in his sixties, full grey beard, dark knitted cap, long dark oilskin coat, "
            + "heavy trousers, work boots, full figure standing in an A-pose, arms held away from the body",
            new CutoutOptions { Variants = ["front view", "side view, in profile", "back view"], Size = 768, Style = Style, Tolerance = 0.10 });
        Save(dir, "1-turnaround", turnaround);

        var heads = await toolkit.Cutout(
            "the same lighthouse keeper, head and shoulders, neutral expression",
            new CutoutOptions
            {
                Variants = ["front, looking at the viewer", "side view, in profile"],
                Size = 768, Style = Style, Tolerance = 0.10, Reference = turnaround,
            });
        Save(dir, "2-heads-with-reference", heads);

        Assert.True(turnaround.Success, turnaround.Error);
        Assert.True(heads.Success, heads.Error);
        Assert.Equal([turnaround.Id], heads.References);
        output.WriteLine($"spent {budget.Spent}, cache hits {budget.CacheHits}, tokens {budget.TokensSpent}; images in {dir}");
    }

    /// <summary>A head sheet through the toolkit: framed as a head, keyed on green, three turns of the face.</summary>
    /// <remarks>Live and billed: one generation. Saved for the face detector to be tried on each turn.</remarks>
    [Fact]
    public async Task Cutout_Live_AHeadSheetKeysOnGreen()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;   // no key configured: nothing to exercise
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;   // opt-in: this one bills

        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT")
            ?? Path.Combine(Path.GetTempPath(), "polson-reference-live");
        Directory.CreateDirectory(dir);

        using var generator = new ImageGenerator(this.apiKey!);
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(Path.Combine(dir, "cache")), new AssetBudget(1), "live");

        var heads = await toolkit.Cutout(
            "a heavyset lighthouse keeper in his sixties, weathered ruddy face, heavy brows, full grey beard, "
            + "black knitted watch cap, black oilskin coat, neutral expression",
            new CutoutOptions
            {
                Variants = ["front, looking at the viewer", "three-quarter view, turned 45 degrees", "side view, in profile"],
                Framing = "head", Size = 768, Style = "clean storyboard illustration, even light", Tolerance = 0.10,
            });
        Save(dir, "3-heads-green", heads);

        Assert.True(heads.Success, heads.Error);
        Assert.Equal("green", heads.KeyColor);
    }

    /// <summary>
    /// A built character shown to the model and asked for a new pose: image editing through <c>reference</c>.
    /// </summary>
    /// <remarks>
    /// Live and billed: one generation. Point <c>POLSON_LIVE_OUT</c> at a folder whose <c>cache/</c> holds a
    /// copy of a project's <c>.polson/assets</c>, and <c>POLSON_LIVE_REFERENCE</c> at the id of a body sheet in
    /// it (lastlight3's Tomas: <c>8D67D3D7BDFF34847CA1E1307DDE00AD</c>). The question is whether the model
    /// keeps the character while changing the pose, which only looking can answer.
    /// </remarks>
    [Fact]
    public async Task Cutout_Live_ABuiltCharacterIsAskedForANewPose()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;
        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT");
        var reference = Environment.GetEnvironmentVariable("POLSON_LIVE_REFERENCE");
        if (dir is null || reference is null) { output.WriteLine("NOT RUN: set POLSON_LIVE_OUT and POLSON_LIVE_REFERENCE"); return; }

        using var generator = new ImageGenerator(this.apiKey!);
        var budget = new AssetBudget(1);
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(Path.Combine(dir, "cache")), budget, "live");
        var started = DateTime.UtcNow;
        var crouch = await toolkit.Cutout(
            "the heavyset lighthouse keeper in his sixties from the reference, weathered ruddy face, heavy brows, full grey beard, "
            + "black knitted watch cap, black oilskin coat, crouching down low on his heels, knees bent, one hand resting on a knee",
            new CutoutOptions
            {
                Variants = ["three-quarter front view", "side view, in profile"], Framing = "full",
                Size = 768, Style = "clean storyboard illustration, even light", Tolerance = 0.10, Reference = reference,
            });
        output.WriteLine($"{(DateTime.UtcNow - started).TotalSeconds:0.0} s; spent {budget.Spent}, tokens {budget.TokensSpent}");
        Save(dir, "crouch", crouch);
        Assert.True(crouch.Success, crouch.Error);
    }

    /// <summary>
    /// The hybrid: a 3D render gives the pose, camera and contact; the character sheet gives the look.
    /// </summary>
    /// <remarks>
    /// Live and billed: one generation per <c>guide-*.png</c> in <c>POLSON_LIVE_OUT</c>. Calls the generator
    /// directly with two images, because the route an agent would use for this does not exist yet — this
    /// is the probe that decides whether it should. <c>POLSON_LIVE_SHEET</c> is the character sheet.
    /// </remarks>
    [Fact]
    public async Task Generate_Live_APoseGuideIsRedrawnAsTheCharacter()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;
        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT");
        var sheetPath = Environment.GetEnvironmentVariable("POLSON_LIVE_SHEET");
        if (dir is null || sheetPath is null) { output.WriteLine("NOT RUN: set POLSON_LIVE_OUT and POLSON_LIVE_SHEET"); return; }

        using var generator = new ImageGenerator(this.apiKey!);
        var sheet = File.ReadAllBytes(sheetPath);
        const string Prompt =
            "Image 1 is a pose guide: a rough 3D model render of a character, with a brown bar that is a ship's rail. "
            + "Image 2 is the character reference sheet for that same character, shown front, side and back. "
            + "Redraw image 1 as a finished clean storyboard illustration of the character in image 2, in the drawing style of image 2. "
            + "From image 1 take exactly: the pose of every limb, the direction the head faces, the camera angle, the framing, "
            + "the figure's size and position in the frame, and any contact such as a hand resting on the rail. "
            + "From image 2 take: the face, beard, hat, clothing, colours and costume details, which must match the sheet exactly, "
            + "including the full length of the coat. Keep the rail if there is one. Plain white background, no other objects.";

        foreach (var guidePath in Directory.GetFiles(dir, "guide-*.png").Order())
        {
            var name = Path.GetFileNameWithoutExtension(guidePath)["guide-".Length..];
            var started = DateTime.UtcNow;
            var result = await generator.GenerateImage(Prompt, aspectRatio: "3:4", conditionOn: [File.ReadAllBytes(guidePath), sheet]);
            output.WriteLine($"{name}: {(DateTime.UtcNow - started).TotalSeconds:0.0} s, success {result.Success} {result.Failure} {result.Error}");
            if (result.Success) File.WriteAllBytes(Path.Combine(dir, $"hybrid-{name}.png"), result.ImageBytes!);
        }
    }

    /// <summary>
    /// The hybrid with a clay guide on a keyed ground: pose from the guide, every part of the look from the sheet.
    /// </summary>
    /// <remarks>
    /// Live and billed: one generation per <c>clay-*.png</c> guide in <c>POLSON_LIVE_OUT</c>, drawn by
    /// <c>ClayGuideProbeTests</c>. Each result is keyed with the cutout route's own hue keyer and saved
    /// beside it, with how much of the frame the ground took and how much the key left, so a result that
    /// drew a floor or drifted off magenta says so.
    /// </remarks>
    [Fact]
    public async Task Generate_Live_AClayGuideIsDrawnAsTheCharacter()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;
        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT");
        var sheetPath = Environment.GetEnvironmentVariable("POLSON_LIVE_SHEET");
        if (dir is null || sheetPath is null) { output.WriteLine("NOT RUN: set POLSON_LIVE_OUT and POLSON_LIVE_SHEET"); return; }

        using var generator = new ImageGenerator(this.apiKey!);
        var sheet = File.ReadAllBytes(sheetPath);
        // POLSON_LIVE_GUIDE_KIND describes the guide, so one prompt serves a clay render and a 2D mannequin.
        var kind = Environment.GetEnvironmentVariable("POLSON_LIVE_GUIDE_KIND") ?? "a plain grey clay 3D model";
        var Prompt =
            $"Image 1 is a pose guide: {kind} standing on a flat magenta background. The grey is not "
            + "a colour of anything; it says nothing about the character's appearance. A brown bar, if present, is a wooden rail. "
            + "Image 2 is the character reference sheet, shown front, side and back. "
            + "Draw the character from image 2 in exactly the pose of image 1, as a clean storyboard illustration in the drawing "
            + "style of image 2. From image 1 take only: the pose of every limb, the direction the head faces, the camera angle, "
            + "the framing, the figure's size and position in the frame, and any contact such as a hand resting on the rail. "
            + "Take everything about how the character looks from image 2: the face, hair, age, build and height, the clothing "
            + "and its length, footwear, colours and costume details. The guide's body is generic: copy its pose, never its "
            + "proportions. Keep the background flat pure magenta #FF00FF exactly as in image 1: "
            + "no floor, no deck, no shadow, no other objects. Keep the rail if there is one.";

        foreach (var guidePath in Directory.GetFiles(dir, "guide-*.png").Order())
        {
            var name = Path.GetFileNameWithoutExtension(guidePath)["guide-".Length..];
            var started = DateTime.UtcNow;
            var result = await generator.GenerateImage(Prompt, aspectRatio: "3:4", conditionOn: [File.ReadAllBytes(guidePath), sheet]);
            var seconds = (DateTime.UtcNow - started).TotalSeconds;
            if (!result.Success) { output.WriteLine($"{name}: {seconds:0.0} s, FAILED {result.Failure} {result.Error}"); continue; }
            File.WriteAllBytes(Path.Combine(dir, $"clay-{name}.png"), result.ImageBytes!);

            using var drawn = SkiaSharp.SKBitmap.Decode(result.ImageBytes!);
            var ground = PlateAnalysis.SampleBackground(drawn);
            using var keyed = PlateAnalysis.DifferenceKey(drawn, ground, "magenta");
            var kept = 0L;
            if (keyed is not null)
            {
                for (var y = 0; y < keyed.Height; y++)
                    for (var x = 0; x < keyed.Width; x++)
                        if (keyed.GetPixel(x, y).Alpha > 128) kept++;
                using var png = keyed.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(dir, $"clay-{name}-keyed.png"), png.ToArray());
            }
            output.WriteLine($"{name}: {seconds:0.0} s, ground {ground}, keyed {(keyed is null ? "no" : "yes")}, "
                + $"subject {100.0 * kept / (drawn.Width * drawn.Height):0.0}% of the frame");
        }
    }

    /// <summary>
    /// <c>Assets.redraw</c> end to end: each <c>guide-*.png</c> in <c>POLSON_LIVE_OUT</c> redrawn as the character whose
    /// sheet is <c>POLSON_LIVE_REFERENCE</c>, from a <c>cache/</c> copy of a project's assets.
    /// </summary>
    /// <remarks>Live and billed: one generation per guide. Writes <c>redraw-*.png</c>, keyed on the guide's own frame.</remarks>
    [Fact]
    public async Task Redraw_Live_ClayGuidesBecomeTheCharacter()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;
        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT");
        var reference = Environment.GetEnvironmentVariable("POLSON_LIVE_REFERENCE");
        if (dir is null || reference is null) { output.WriteLine("NOT RUN: set POLSON_LIVE_OUT and POLSON_LIVE_REFERENCE"); return; }

        using var generator = new ImageGenerator(this.apiKey!);
        var budget = new AssetBudget(6);
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(Path.Combine(dir, "cache")), budget, "live");
        foreach (var guidePath in Directory.GetFiles(dir, "guide-*.png").Order())
        {
            var name = Path.GetFileNameWithoutExtension(guidePath)["guide-".Length..];
            var started = DateTime.UtcNow;
            var result = await toolkit.Redraw(File.ReadAllBytes(guidePath), new RedrawOptions { Reference = reference });
            var seconds = (DateTime.UtcNow - started).TotalSeconds;
            if (!result.Success) { output.WriteLine($"{name}: {seconds:0.0} s, {result.FailureName}: {result.Error}"); continue; }
            File.WriteAllBytes(Path.Combine(dir, $"redraw-{name}.png"), result.Bytes);
            output.WriteLine($"{name}: {seconds:0.0} s, {result.Width}x{result.Height}, agreement {result.GuideAgreement:0.00}, "
                + $"key {result.KeyColor}, warnings: {string.Join(" | ", result.Warnings)}");
        }
        output.WriteLine($"spent {budget.Spent}, tokens {budget.TokensSpent}");
    }

    /// <summary>
    /// A body sheet for a character dressed to rig cleanly: a jacket ending at the waist and slim trousers, so nothing
    /// hangs between the legs, laid out as <c>GenerateCharacter</c> reads it.
    /// </summary>
    /// <remarks>
    /// Live and billed: one generation. The lastlight3 characters all wore coats, and reconstructed coats rig badly:
    /// each is one thick surface the legs are modelled inside, and no weighting made one hang. This is the control.
    /// Writes <c>short-jacket.png</c> to <c>POLSON_LIVE_OUT</c>: the cells on white, feet on one line.
    /// </remarks>
    [Fact]
    public async Task Cutout_Live_AShortJacketTurnaround()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;   // opt-in: this one bills
        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT");
        if (dir is null) { output.WriteLine("NOT RUN: set POLSON_LIVE_OUT"); return; }
        Directory.CreateDirectory(dir);

        using var generator = new ImageGenerator(this.apiKey!);
        var budget = new AssetBudget(1);
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(Path.Combine(dir, "cache")), budget, "live");
        var sheet = await toolkit.Cutout(
            "a young woman bicycle courier in her twenties, short dark hair cut above the collar, a fitted zip-up jacket that "
            + "ends at the waist, slim cargo trousers tucked into ankle boots, a small bag on a strap across the chest, "
            + "full figure standing in an A-pose, arms held away from the body, legs slightly apart",
            new CutoutOptions
            {
                Variants = ["front view", "side view, in profile", "back view"], Framing = "full", Size = 768,
                Style = "clean storyboard illustration, even light, no cast shadow", Tolerance = 0.10,
            });
        Save(dir, "short-jacket", sheet);
        Assert.True(sheet.Success, sheet.Error);

        // The cells on white, feet on one line, as the SDK docs lay a sheet out for GenerateCharacter.
        var cells = sheet.Cells.Select(c => SkiaSharp.SKBitmap.Decode(c.Bytes)).ToList();
        const int Gap = 60;
        var h = cells.Max(c => c.Height);
        using var page = new SkiaSharp.SKBitmap(cells.Sum(c => c.Width + Gap) + Gap, h + (2 * Gap));
        using (var canvas = new SkiaSharp.SKCanvas(page))
        {
            canvas.Clear(SkiaSharp.SKColors.White);
            var x = Gap;
            foreach (var c in cells) { canvas.DrawBitmap(c, x, Gap + (h - c.Height)); x += c.Width + Gap; }
        }
        foreach (var c in cells) c.Dispose();
        using var data = page.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, "short-jacket.png"), data.ToArray());
        output.WriteLine($"sheet {page.Width}x{page.Height}, spent {budget.Spent}; {dir}");
    }

    /// <summary>
    /// Body sheets for a spread of garments, to test finding garments from pose-prompted segmentation beyond coats.
    /// </summary>
    /// <remarks>
    /// Live and billed: one generation per character, eight in all, skipping any whose cells are already in
    /// <c>POLSON_LIVE_OUT</c>. Each was chosen to probe one case: a hem the shin stop would cut, two garments, a
    /// sleeveless drape, a sleeveless vest, a control with nothing hanging, wide sleeves, a bib garment, and a plain
    /// hoodie. Writes <c>&lt;name&gt;-cell0..2.png</c> (front, side, back) per character.
    /// </remarks>
    [Fact]
    public async Task Cutout_Live_AGarmentSet()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;   // opt-in: this one bills
        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT");
        if (dir is null) { output.WriteLine("NOT RUN: set POLSON_LIVE_OUT"); return; }
        Directory.CreateDirectory(dir);

        const string Pose = ", full figure standing in an A-pose, arms held away from the body, legs slightly apart";
        (string Name, string Description)[] set =
        [
            ("gown", "a woman in her thirties in a long-sleeved floor-length evening dress that covers her feet, hair in a low bun"),
            ("skirt", "a woman in her twenties in a chunky knit sweater tucked into a pleated knee-length skirt, tights and loafers"),
            ("cloak", "a traveller in a long hooded cloak with no sleeves, open at the front, over a belted tunic, trousers and boots, hood down"),
            ("vest", "a man in his thirties in a sleeveless quilted puffer vest over a long-sleeved t-shirt, jeans and trainers"),
            ("shorts", "a teenage boy in a short-sleeved t-shirt and knee-length shorts, bare arms and legs, trainers"),
            ("robe", "an old monk in a floor-length hooded robe with wide sleeves and a rope belt, sandals, hood down"),
            ("overalls", "a mechanic in denim bib overalls over a short-sleeved shirt, work boots"),
            ("hoodie", "a young woman in a loose pullover hoodie and straight jeans, hood down, sneakers"),
        ];
        using var generator = new ImageGenerator(this.apiKey!);
        var budget = new AssetBudget(set.Length);
        var toolkit = new AssetRequisitionToolkit(generator, new RequisitionCache(Path.Combine(dir, "cache")), budget, "live");
        foreach (var (name, description) in set)
        {
            if (File.Exists(Path.Combine(dir, $"{name}-cell2.png"))) { output.WriteLine($"{name}: already here"); continue; }
            var sheet = await toolkit.Cutout(description + Pose, new CutoutOptions
            {
                Variants = ["front view", "side view, in profile", "back view"], Framing = "full", Size = 768,
                Style = "clean storyboard illustration, even light, no cast shadow", Tolerance = 0.10,
            });
            Save(dir, name, sheet);
        }
        output.WriteLine($"spent {budget.Spent}, tokens {budget.TokensSpent}; {dir}");
    }

    void Save(string dir, string name, CutoutAsset cutout)
    {
        if (!cutout.Success)
        {
            output.WriteLine($"{name}: FAILED {cutout.FailureName}: {cutout.Error}");
            return;
        }

        File.WriteAllBytes(Path.Combine(dir, name + "-sheet.png"), cutout.Bytes);
        foreach (var (cell, i) in cutout.Cells.Select((c, i) => (c, i)))
            File.WriteAllBytes(Path.Combine(dir, $"{name}-cell{i}.png"), cell.Bytes);

        output.WriteLine($"{name}: id {cutout.Id}, split {cutout.Split}, fromCache {cutout.Provenance?.FromCache}, "
            + string.Join("; ", cutout.Cells.Select(c => $"{c.Name} {c.Width}x{c.Height} coverage {c.Coverage:F2} holes {c.Holes:F3}")));
    }
}
