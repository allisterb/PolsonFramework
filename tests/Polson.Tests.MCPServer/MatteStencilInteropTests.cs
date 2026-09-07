namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Polson.ExtendedMind;
using Polson.ExtendedMind.ImageGeneration;
using Polson.MCPServer;
using SkiaSharp;
using Xunit;

/// <summary>
/// That a JS options literal actually reaches <see cref="MatteOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because the binding is an assumption rather than code anyone wrote.</b> Nothing maps a
/// JS object onto the options records — <c>engine.SetValue("Assets", assets)</c> hands Jint the CLR
/// object and Jint converts the literal itself. That works today for <c>MaterialOptions</c>, which is
/// the only reason to expect it to work for a new property, and "it worked for the last one" is not a
/// test.
/// </para>
/// <para>
/// The failure it guards against is silent in the worst way: an unbound <c>hardEdge</c> leaves
/// <c>HardEdge</c> false, so the call succeeds, returns a perfectly good <i>ramp</i>, and the caller
/// gets grey mush where it asked for a stencil. Only <c>threshold</c> coming back null would say so,
/// and nothing was checking it.
/// </para>
/// </remarks>
[Collection(AssetsCollection.Name)]
public class MatteStencilInteropTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-stencil-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public MatteStencilInteropTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        JsDrawingEngine.Assets = null;
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>A camelCase flag in a JS literal binds to the PascalCase property.</summary>
    [Fact]
    public async Task TestHardEdgeBindsFromAJsOptionsLiteral()
    {
        UseFakeGenerator();

        var result = await Run("""
            const s = await Assets.matte('a rearing horse', { hardEdge: true, size: 128 });
            log(s.success + '|' + s.threshold + '|' + s.coverage.toFixed(2));
            """);

        Assert.True(result.Success, result.Error);

        // A bound flag gives a measured level and a quarter-frame subject. An unbound one gives
        // "true|null|0.04" — a successful call that quietly did the other thing.
        var logged = Logged(result);
        Assert.StartsWith("true|", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("null", logged, StringComparison.Ordinal);
        Assert.EndsWith("|0.25", logged, StringComparison.Ordinal);
    }

    /// <summary>An explicit numeric level binds too, and overrides the measured one.</summary>
    [Fact]
    public async Task TestAnExplicitThresholdBindsFromAJsOptionsLiteral()
    {
        UseFakeGenerator();

        var result = await Run("""
            const s = await Assets.matte('a rearing horse', { threshold: 200, size: 128 });
            log(s.threshold + '|' + s.coverage.toFixed(2));
            """);

        Assert.True(result.Success, result.Error);

        // Above both populations, so the whole frame cuts to black. The point is the 200, not the 0.
        Assert.Equal("200|0.00", Logged(result));
    }

    /// <summary>
    /// A stencil goes straight onto a paper, exactly as the workflow instructions say it does.
    /// </summary>
    /// <remarks>
    /// <b>This is the call a live run could not make.</b> The instructions say
    /// "<c>Assets.matte(subject, …)</c>, then <c>paper.image(...)</c>", and that failed twice — once
    /// on <c>.dataUri</c> read as a property (undefined, so "got null") and once on the asset itself
    /// ("cannot use a MatteAsset"), before the agent found <c>Skia.Image.fromBytes</c>. Documenting a
    /// path is a promise about the plumbing, so the promise is now a test.
    /// </remarks>
    [Fact]
    public async Task TestAStencilCanBeDrawnStraightOntoAPaper()
    {
        UseFakeGenerator();

        var result = await Run("""
            const s = await Assets.matte('a rearing horse', { hardEdge: true, size: 128 });
            const paper = Snap(400, 300);
            paper.image(s, 20, 20, 128, 128);
            const xml = paper.toString();
            log(String(xml.indexOf('<image') > 0) + '|' + String(xml.indexOf('data:image/png;base64,') > 0));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("true|true", Logged(result));
    }

    /// <summary>Omitting the flag keeps the ramp, so the existing height-field call is untouched.</summary>
    [Fact]
    public async Task TestOmittingTheFlagKeepsTheRamp()
    {
        UseFakeGenerator();

        var result = await Run("""
            const s = await Assets.matte('rolling hills height field', { size: 128 });
            log(String(s.threshold));
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("null", Logged(result));
    }
    #endregion

    #region Methods
    private void UseFakeGenerator() =>
        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            new FakeGenerator(Plate()), new RequisitionCache(root), new AssetBudget(5), "test");

    private Task<DrawingExecutionResult> Run(string script) =>
        new DrawingMcpTools(null, null, null, root).ExecuteScript(
            script + " const _c = createCanvas(20,20); _c.getContext('2d').fillRect(0,0,20,20); _c;", 20, 20);

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();

    /// <summary>A quarter-frame subject at 110 on a 20 ground: both below a fixed midpoint cut.</summary>
    private static byte[] Plate()
    {
        using var bitmap = new SKBitmap(128, 128);
        for (var y = 0; y < 128; y++)
        {
            for (var x = 0; x < 128; x++)
            {
                var v = x < 32 ? (byte)110 : (byte)20;
                bitmap.SetPixel(x, y, new SKColor(v, v, v));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    #endregion

    #region Types
    private sealed class FakeGenerator(byte[] png) : IImageGenerator
    {
        public string Model => "fake-image-model";

        public Task<ImageGenerationResult> GenerateImage(
            string prompt,
            string? model = null,
            string? aspectRatio = null,
            IReadOnlyList<byte[]>? conditionOn = null,
            CancellationToken ct = default)
        {
            var useModel = model ?? Model;
            ImageGenerator.TryReadPngSize(png, out var width, out var height);

            return Task.FromResult(new ImageGenerationResult
            {
                Success = true,
                ImageBytes = png,
                Width = width,
                Height = height,
                MimeType = "image/png",
                Model = useModel,
                Prompt = prompt,
                Hash = ImageGenerator.HashOf(useModel, prompt, aspectRatio, conditionOn),
                GeneratedUtc = DateTime.UtcNow,
                ElapsedMs = 1,
                Charged = true,
            });
        }
    }
    #endregion
}
