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
