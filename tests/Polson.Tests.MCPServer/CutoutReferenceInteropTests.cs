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
/// <c>Assets.cutout</c>'s <c>reference</c> option, as a script writes it.
/// </summary>
/// <remarks>
/// The option is typed as an object on the C# side so that it can take a cutout, a cell, an id or an
/// array, which means it is the engine's conversion that decides what arrives. An option that binds
/// to nothing is silently ignored, and the call then draws somebody else while reporting success, so
/// each spelling a script might use is asserted to reach the toolkit.
/// </remarks>
[Collection(AssetsCollection.Name)]
public class CutoutReferenceInteropTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-reference-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public CutoutReferenceInteropTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        JsDrawingEngine.Assets = null;
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>A cutout, a cell, an id and an array each bind, and each names the first sheet.</summary>
    [Theory]
    [InlineData("first")]
    [InlineData("first.cells[1]")]
    [InlineData("first.id")]
    [InlineData("[first]")]
    public async Task TestEachSpellingOfAReferenceBinds(string reference)
    {
        UseFakeGenerator();

        var result = await Run($$"""
            const first = await Assets.cutout('a heavyset keeper, full figure', { variants: ['front view', 'back view'] });
            const again = await Assets.cutout('a heavyset keeper, full figure', { variants: ['side view'], reference: {{reference}} });
            log(again.success + '|' + (again.references[0] === first.id) + '|' + again.references.length);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("true|true|1", Logged(result));
    }

    /// <summary>A canvas is refused by name, before anything is generated.</summary>
    [Fact]
    public async Task TestACanvasIsRefusedAsAReference()
    {
        UseFakeGenerator();

        var result = await Run("""
            const c = createCanvas(40, 40);
            const refused = await Assets.cutout('a heavyset keeper', { reference: c });
            log(refused.success + '|' + refused.failureName + '|' + Assets.budget.spent);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Equal("false|InvalidRequest|0", Logged(result));
    }
    #endregion

    #region Methods (private)
    private void UseFakeGenerator() =>
        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            new FakeGenerator(Sheet()), new RequisitionCache(root), new AssetBudget(5), "test");

    private Task<DrawingExecutionResult> Run(string script) =>
        new DrawingMcpTools(null, null, null, root).ExecuteScript(
            script + " const _c = createCanvas(20,20); _c.getContext('2d').fillRect(0,0,20,20); _c;", 20, 20);

    private static string Logged(DrawingExecutionResult result) =>
        string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();

    /// <summary>Two grey figures on magenta, with a clear gap between them.</summary>
    private static byte[] Sheet()
    {
        using var bitmap = new SKBitmap(200, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0xFF, 0x00, 0xFF));
            using var paint = new SKPaint { Color = new SKColor(128, 128, 128) };
            canvas.DrawRect(SKRect.Create(20, 10, 50, 80), paint);
            canvas.DrawRect(SKRect.Create(120, 10, 50, 80), paint);
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
