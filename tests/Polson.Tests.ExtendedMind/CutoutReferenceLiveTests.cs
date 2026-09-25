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
