namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using global::Polson.Tests;
using Polson.ExtendedMind.ImageGeneration;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// A character built face first, in the order <i>AI Cinematic Filmmaking: Pre-Production</i> ch. 8 uses,
/// with raw prompts sent straight to the image model.
/// </summary>
/// <remarks>
/// <para>
/// <b>An experiment, not a regression test.</b> The first live reference check showed a full-figure
/// reference pulling full-figure output and copying its source nearly line for line. The chapter
/// anchors the other way round - face first, then the body with the face attached - tells the model to
/// <i>maintain that face</i> rather than to change nothing, and states framing as its own instruction.
/// These three prompts test that structure before any of it goes into <c>CutoutPrompt</c>.
/// </para>
/// <para>
/// <b>Live and billed: three generations.</b> Runs only with <c>POLSON_LIVE_IMAGE_TESTS=1</c>. Each result
/// is written to <c>POLSON_LIVE_OUT</c>, and a result already there is reused rather than bought again.
/// </para>
/// </remarks>
public class CharacterSheetPromptLiveTests : TestsRuntime
{
    const string Ground =
        "Flat, perfectly uniform pure magenta (#FF00FF) background filling everything around the "
        + "subject. No cast shadow, no ground plane, no horizon, no vignette, no text, no labels, "
        + "no numbers, no frame, no border, no panel divisions.";

    const string Style = "Clean storyboard illustration, even light, no cast shadow.";

    readonly ITestOutputHelper output;
    readonly string? apiKey;

    public CharacterSheetPromptLiveTests(ITestOutputHelper output)
    {
        this.output = output;
        this.apiKey = config["ApiKeys:GoogleAgentPlatform"];
    }

    [Fact]
    public async Task Sheet_Live_FaceFirstThenBodyThenANewPose()
    {
        if (string.IsNullOrWhiteSpace(this.apiKey)) return;   // no key configured: nothing to exercise
        if (Environment.GetEnvironmentVariable("POLSON_LIVE_IMAGE_TESTS") != "1") return;   // opt-in: this one bills

        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT")
            ?? Path.Combine(Path.GetTempPath(), "polson-sheet-prompt-live");
        Directory.CreateDirectory(dir);
        using var generator = new ImageGenerator(this.apiKey!);

        // 1. The face, first and large, with nothing attached.
        var face = await Generate(generator, dir, "1-face", "4:3", [],
            "Character design reference sheet: the face. A heavyset lighthouse keeper in his sixties, weathered "
            + "ruddy face, deep-set eyes, heavy brows, full grey beard, dark knitted watch cap, the collar of a dark "
            + "oilskin coat. Neutral expression. Two views side by side in one row with a clear gap between them: "
            + "1. front, looking at the viewer; 2. side view, in profile. Close-up portrait framing: head and "
            + "shoulders only, cropped at the upper chest, the face large in the frame. " + Style + " " + Ground);

        // 2. The body, told to maintain that face.
        var body = await Generate(generator, dir, "2-body", "16:9", [face],
            "The attached image is the face reference for this character. Maintain that face exactly: the same "
            + "features, beard and cap. Character design reference sheet: a full body turnaround of the same "
            + "lighthouse keeper, heavyset, long dark oilskin coat with two flap pockets, dark brown heavy trousers, "
            + "black rubber boots, standing in an A-pose with the arms held away from the body. Three views in one "
            + "row, evenly spaced, not touching: 1. front view; 2. side view, in profile; 3. back view. Full body "
            + "shot: the whole figure in frame from cap to boots. " + Style + " " + Ground);

        // 3. A change: keep the face and the costume, move everything else.
        var pose = await Generate(generator, dir, "3-pose", "1:1", [face, body],
            "The attached images are the face reference and the costume reference for this character. Maintain "
            + "that face and that costume exactly. New pose and expression: the same lighthouse keeper shouting in "
            + "alarm, mouth wide open, both arms raised above his head, leaning forward, seen from the front at a "
            + "slight three-quarter angle. Full body shot: the whole figure in frame. " + Style + " " + Ground);

        Assert.NotEmpty(face);
        Assert.NotEmpty(body);
        Assert.NotEmpty(pose);
    }

    async Task<byte[]> Generate(ImageGenerator generator, string dir, string name, string aspect,
        IReadOnlyList<byte[]> shown, string prompt)
    {
        var path = Path.Combine(dir, name + ".png");
        if (File.Exists(path))
        {
            output.WriteLine($"{name}: reused {path}");
            return File.ReadAllBytes(path);
        }

        File.WriteAllText(Path.Combine(dir, name + ".prompt.txt"), prompt);
        var result = await generator.GenerateImage(prompt, null, aspect, shown.Count > 0 ? shown : null);
        Assert.True(result.Success, $"{name}: {result.Failure} {result.Error}");
        File.WriteAllBytes(path, result.ImageBytes!);

        // Keyed as the studio would key it, so a ground the keyer cannot take shows up here too.
        using var master = SKBitmap.Decode(result.ImageBytes);
        var background = PlateAnalysis.SampleBackground(master);
        using var keyed = PlateAnalysis.ChromaKey(master, background, 0.10);
        File.WriteAllBytes(Path.Combine(dir, name + "-keyed.png"), PlateAnalysis.Encode(keyed, "png", 100));

        output.WriteLine($"{name}: {result.Width}x{result.Height}, {result.ElapsedMs} ms, {result.TotalTokens} tokens, "
            + $"shown {shown.Count}, ground #{background.Red:X2}{background.Green:X2}{background.Blue:X2}, "
            + $"coverage {PlateAnalysis.AlphaCoverage(keyed):F2}");
        return result.ImageBytes!;
    }
}
