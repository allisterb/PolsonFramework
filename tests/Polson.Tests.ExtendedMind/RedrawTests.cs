namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using global::Polson.Tests;
using Polson.ExtendedMind.ImageGeneration;
using SkiaSharp;
using Xunit;

/// <summary><c>Assets.redraw(guide, { reference })</c> — a character drawn in the pose of a clay guide.</summary>
/// <remarks>
/// Offline: the generator is a fake that returns a fixed drawing and records what it was shown, so what is under
/// test is everything around the model — the guide check, the reference lookup, the prompt, the keying, the frame
/// and the agreement score — and that a refusal costs nothing.
/// </remarks>
public class RedrawTests : TestsRuntime
{
    #region Tests
    [Fact]
    public async Task TestARedrawIsShownTheGuideAndTheSheetAndLandsOnTheGuidesFrame()
    {
        var budget = new AssetBudget(5);
        var generator = new Fake(Sheet());
        var toolkit = Toolkit(generator, budget);
        var sheet = await toolkit.Cutout("a keeper, full figure", new CutoutOptions { Variants = ["front view", "back view"] });
        Assert.True(sheet.Success, sheet.Error);

        generator.Next = Drawing(scale: 2);
        var result = await toolkit.Redraw(Guide(), new RedrawOptions { Reference = sheet, Describe = "calling out across the water" });

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, generator.Calls);
        Assert.Equal(2, generator.Shown[1].Count);                     // the guide, then the sheet
        Assert.Contains("pose guide", generator.Prompts[1]);
        Assert.Contains("calling out across the water", generator.Prompts[1]);
        Assert.Contains("never its proportions", generator.Prompts[1]);

        // Back on the guide's own frame, with the figure where the guide put it.
        Assert.Equal(300, result.Width);
        Assert.Equal(400, result.Height);
        Assert.Equal("magenta", result.KeyColor);
        Assert.InRange(result.GuideAgreement!.Value, 0.8, 1.0);
        Assert.Empty(result.Warnings);
        Assert.Single(result.Cells);
        Assert.Equal([sheet.Id], result.References);
        Assert.Equal(2, budget.Spent);
    }

    [Fact]
    public async Task TestTheSameRedrawTwiceIsServedFromCache()
    {
        var generator = new Fake(Sheet());
        var toolkit = Toolkit(generator, new AssetBudget(5));
        var sheet = await toolkit.Cutout("a keeper, full figure", new CutoutOptions { Variants = ["front view", "back view"] });
        generator.Next = Drawing(scale: 1);

        var first = await toolkit.Redraw(Guide(), new RedrawOptions { Reference = sheet });
        var again = await toolkit.Redraw(Guide(), new RedrawOptions { Reference = sheet });

        Assert.True(again.Success, again.Error);
        Assert.Equal(2, generator.Calls);
        Assert.Equal(first.Id, again.Id);
    }

    /// <summary>Everything decidable before the network is refused there, and costs nothing.</summary>
    [Fact]
    public async Task TestWhatIsNotAClayGuideOrHasNoGoodReferenceIsRefusedForFree()
    {
        var budget = new AssetBudget(5);
        var generator = new Fake(Sheet());
        var toolkit = Toolkit(generator, budget);
        var sheet = await toolkit.Cutout("a keeper, full figure", new CutoutOptions { Variants = ["front view", "back view"] });
        var calls = generator.Calls;

        var refusals = new (string What, object Guide, RedrawOptions Options)[]
        {
            ("a photograph", Photo(), new() { Reference = sheet }),
            ("an inked drawing", Guide(ink: true), new() { Reference = sheet }),
            ("a white ground", Guide(ground: SKColors.White), new() { Reference = sheet }),
            ("no reference", Guide(), new()),
            ("a reference not generated here", Guide(), new() { Reference = "NOTAGENERATEDSHEET" }),
            ("not an image", 42, new() { Reference = sheet }),
            ("a lossy guide", Lossy(Guide()), new() { Reference = sheet }),
        };
        foreach (var (what, guide, options) in refusals)
        {
            var refused = await toolkit.Redraw(guide, options);
            Assert.False(refused.Success, what);
            Assert.Equal(ImageGenerationFailure.InvalidRequest, refused.Failure);
        }

        var likeness = await toolkit.Redraw(Guide(), new RedrawOptions { Reference = sheet, Describe = "Stanley Kubrick shouting at a crew" });
        Assert.Equal(ImageGenerationFailure.RefusedLikeness, likeness.Failure);

        Assert.Equal(calls, generator.Calls);
        Assert.Equal(1, budget.Spent);
    }

    /// <summary>A drawing that strays from its guide is flagged, because it renders as well as one that does not.</summary>
    [Fact]
    public async Task TestADrawingThatStraysFromTheGuideIsWarnedAbout()
    {
        var generator = new Fake(Sheet());
        var toolkit = Toolkit(generator, new AssetBudget(5));
        var sheet = await toolkit.Cutout("a keeper, full figure", new CutoutOptions { Variants = ["front view", "back view"] });
        generator.Next = Drawing(scale: 1, offset: 120);

        var result = await toolkit.Redraw(Guide(), new RedrawOptions { Reference = sheet });

        Assert.True(result.Success, result.Error);
        Assert.True(result.GuideAgreement < 0.4, $"agreement {result.GuideAgreement}");
        Assert.Contains(result.Warnings, w => w.Contains("strayed"));
    }

    /// <summary>The guide check alone: clay passes, and what it refuses it names.</summary>
    [Fact]
    public void TestTheGuideCheckKnowsClay()
    {
        using var clay = SKBitmap.Decode(Guide());
        Assert.Null(PlateAnalysis.GuideProblem(clay, out var ground));
        Assert.Equal("magenta", ground);

        using var photo = SKBitmap.Decode(Photo());
        Assert.Contains("flat", PlateAnalysis.GuideProblem(photo, out _));
        using var inked = SKBitmap.Decode(Guide(ink: true));
        Assert.Contains("near black", PlateAnalysis.GuideProblem(inked, out _));
    }
    #endregion

    #region Helpers
    static AssetRequisitionToolkit Toolkit(Fake generator, AssetBudget budget) =>
        new(generator, new RequisitionCache(ImageGeneratorTests.TempCacheDir()), budget, "Framer");

    /// <summary>A figure of flat facets in one grey, lit from the left, on magenta: what Mesh.draw's clay mode draws.</summary>
    static byte[] Guide(bool ink = false, SKColor? ground = null)
    {
        using var bitmap = new SKBitmap(300, 400);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(ground ?? new SKColor(0xFF, 0x00, 0xFF));
            Figure(canvas, 0, 1f, ink);
        }
        return Encode(bitmap);
    }

    /// <summary>The model's answer: a coloured figure in the guide's place, at <paramref name="scale"/> times its size.</summary>
    static byte[] Drawing(int scale, int offset = 0)
    {
        using var bitmap = new SKBitmap(300 * scale, 400 * scale);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0xF4, 0x10, 0xE8));   // the model's magenta, never quite pure
            canvas.Scale(scale);
            canvas.Translate(offset, 0);
            using var coat = new SKPaint { Color = new SKColor(240, 200, 40) };
            using var face = new SKPaint { Color = new SKColor(150, 100, 70) };
            canvas.DrawRect(SKRect.Create(110, 110, 80, 150), coat);
            canvas.DrawOval(150, 80, 30, 34, face);
            canvas.DrawRect(SKRect.Create(115, 260, 30, 110), coat);
            canvas.DrawRect(SKRect.Create(155, 260, 30, 110), coat);
        }
        return Encode(bitmap);
    }

    static void Figure(SKCanvas canvas, int dx, float s, bool ink)
    {
        void Facet(SKRect r, float k)
        {
            using var p = new SKPaint { Color = new SKColor((byte)(235 * k), (byte)(235 * k), (byte)(235 * k)) };
            canvas.DrawRect(r, p);
        }
        Facet(SKRect.Create(110 + dx, 110, 40, 150), 0.95f);
        Facet(SKRect.Create(150 + dx, 110, 40, 150), 0.6f);
        Facet(SKRect.Create(120 + dx, 46, 30, 68), 0.9f);
        Facet(SKRect.Create(150 + dx, 46, 30, 68), 0.5f);
        Facet(SKRect.Create(115 + dx, 260, 30, 110), 0.8f);
        Facet(SKRect.Create(155 + dx, 260, 30, 110), 0.45f);
        if (ink)
        {
            using var line = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 6 };
            canvas.DrawRect(SKRect.Create(110 + dx, 46, 80, 324), line);
            canvas.DrawLine(150 + dx, 46, 150 + dx, 370, line);
        }
    }

    /// <summary>A photograph's signature: a subject of continuously varying tone, on a ground that is not flat either.</summary>
    static byte[] Photo()
    {
        var random = new Random(7);
        using var bitmap = new SKBitmap(300, 400);
        for (var y = 0; y < 400; y++)
            for (var x = 0; x < 300; x++)
            {
                var inside = x is > 100 and < 200 && y is > 50 and < 370;
                var v = inside ? 90 + random.Next(0, 60) : 200 + random.Next(0, 30);
                bitmap.SetPixel(x, y, new SKColor((byte)v, (byte)v, (byte)v));
            }
        return Encode(bitmap);
    }

    /// <summary>The same picture as WebP, which is what a canvas's data URI is by default.</summary>
    static byte[] Lossy(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        using var data = bitmap.Encode(SKEncodedImageFormat.Webp, 85);
        return data.ToArray();
    }

    static byte[] Sheet()
    {
        using var bitmap = new SKBitmap(400, 200);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0xFF, 0x00, 0xFF));
            using var paint = new SKPaint { Color = new SKColor(40, 90, 160) };
            canvas.DrawRect(SKRect.Create(40, 30, 80, 140), paint);
            canvas.DrawRect(SKRect.Create(260, 30, 80, 140), paint);
        }
        return Encode(bitmap);
    }

    static byte[] Encode(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Returns <see cref="Next"/> (the sheet until set), and records every prompt and every image shown.</summary>
    sealed class Fake(byte[] first) : IImageGenerator
    {
        public string Model => "fake-image-model";
        public byte[] Next { get; set; } = first;
        public int Calls { get; private set; }
        public List<string> Prompts { get; } = [];
        public List<IReadOnlyList<byte[]>> Shown { get; } = [];

        public Task<ImageGenerationResult> GenerateImage(string prompt, string? model = null, string? aspectRatio = null,
            IReadOnlyList<byte[]>? conditionOn = null, CancellationToken ct = default)
        {
            Calls++;
            Prompts.Add(prompt);
            Shown.Add(conditionOn ?? []);
            ImageGenerator.TryReadPngSize(Next, out var width, out var height);
            return Task.FromResult(new ImageGenerationResult
            {
                Success = true, ImageBytes = Next, Width = width, Height = height, MimeType = "image/png",
                Model = model ?? Model, Prompt = prompt,
                Hash = ImageGenerator.HashOf(model ?? Model, prompt, aspectRatio, conditionOn),
                Charged = true,
            });
        }
    }
    #endregion
}
