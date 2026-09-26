namespace Polson.Tests.MCPServer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using global::Polson.MCPServer;
using global::Polson.Tests;

using Polson.ExtendedMind;
using Polson.ExtendedMind.ImageGeneration;
using SkiaSharp;
using Xunit;

/// <summary>
/// A list the SDK hands a script reads like any other SDK object, and serialises.
/// </summary>
/// <remarks>
/// <b>Until 2026-09-25 reading anything a list lacked killed the script.</b> The member index says yes to
/// every name on a list, since it cannot tell a declared member from one <c>Array.prototype</c> attaches,
/// so the read went to reflection, found nothing and threw an exception that is not a JS error: no
/// <c>try/catch</c> could stop it. <c>JSON.stringify</c> asks every value for <c>toJSON</c>, so stringifying
/// anything holding a list died the same way, and a gradient failed anyway on the cycle its elements'
/// <c>parent</c> and <c>paper</c> make.
/// </remarks>
[Collection(AssetsCollection.Name)]
public class SdkListTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Path.Combine(Path.GetTempPath(), "polson-lists-" + Guid.NewGuid().ToString("N"));
    #endregion

    #region Methods
    public SdkListTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        JsDrawingEngine.Assets = null;
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    /// <summary>A gradient serialises as its markup: tag, attributes and children, and no cycle.</summary>
    [Fact]
    public async Task TestStringifyingAGradientGivesItsMarkupTree()
    {
        var logged = await Log("""
            const g = Snap(10, 10).gradient('l(0,0,1,0)#f00-#00f');
            const tree = JSON.parse(JSON.stringify(g));
            log([tree.type, tree.children.length, tree.children[0].type, tree.children[0].attributes['stop-color'],
                 tree.id === g.attr('id'), 'id' in tree.attributes, 'paper' in tree, 'parent' in tree].join(' '));
            """);

        Assert.Equal("linearGradient 2 stop #FF0000 true false false false", logged);
    }

    /// <summary>A list serialises as a JSON array.</summary>
    [Fact]
    public async Task TestStringifyingAListGivesAnArray()
    {
        var logged = await Log("""
            const stops = Snap(10, 10).gradient('l(0,0,1,0)#f00-#00f').stops();
            const parsed = JSON.parse(JSON.stringify(stops));
            log(Array.isArray(parsed) + ' ' + parsed.length + ' ' + parsed[1].attributes.offset);
            """);

        Assert.Equal("true 2 100%", logged);
    }

    /// <summary>A missing member on a list reads as undefined, and the array methods still work.</summary>
    [Fact]
    public async Task TestAMissingMemberOnAListReadsUndefined()
    {
        var logged = await Log("""
            const stops = Snap(10, 10).gradient('l(0,0,1,0)#f00-#00f').stops();
            const seen = [];
            stops.forEach((s, i) => seen.push(i));
            log([typeof stops.nonexistent, typeof stops.toJSON, stops.length, stops.filter(s => true).length,
                 stops.map(s => s.type).join('+'), seen.join(',')].join(' '));
            """);

        Assert.Equal("undefined undefined 2 2 stop+stop 0,1", logged);
    }

    /// <summary>An error from a list read is a JS error, so try/catch catches it.</summary>
    [Fact]
    public async Task TestAnErrorFromAListIsCatchable()
    {
        var logged = await Log("""
            const stops = Snap(10, 10).gradient('l(0,0,1,0)#f00-#00f').stops();
            let caught = 'not caught';
            try { stops.nonexistent(); } catch (e) { caught = 'caught ' + (e instanceof TypeError); }
            log(caught);
            """);

        Assert.Equal("caught true", logged);
    }

    /// <summary>has() answers for a list instead of saying yes to everything.</summary>
    [Fact]
    public async Task TestHasAnswersForAList()
    {
        var logged = await Log("""
            const stops = Snap(10, 10).gradient('l(0,0,1,0)#f00-#00f').stops();
            log([has(stops, 'filter'), has(stops, 'length'), has(stops, '0'), has(stops, 'nonexistent')].join(' '));
            """);

        Assert.Equal("true true true false", logged);
    }

    /// <summary>Assets.library, documented until now as having no array methods and no forEach index.</summary>
    [Fact]
    public async Task TestTheAssetLibraryTakesArrayMethods()
    {
        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            new FakeGenerator(Sheet()), new RequisitionCache(root), new AssetBudget(3), "test");

        var logged = await Log("""
            const lib = Assets.library;
            log([lib.length, typeof lib.filter, typeof lib.map, lib.map(m => m.id).length, JSON.stringify(lib),
                 has(lib, 'forEach'), has(lib, 'nonexistent')].join(' '));
            """);

        Assert.Equal("0 function function 0 [] true false", logged);
    }

    /// <summary>A read-only list behaves the same: a cutout's cells.</summary>
    [Fact]
    public async Task TestAReadOnlyListBehavesTheSame()
    {
        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            new FakeGenerator(Sheet()), new RequisitionCache(root), new AssetBudget(3), "test");

        var logged = await Log("""
            const cut = await Assets.cutout('a keeper', { variants: ['front', 'back'] });
            const cells = cut.cells;
            const seen = [];
            cells.forEach((c, i) => seen.push(i + ':' + c.name));
            const parsed = JSON.parse(JSON.stringify(cells));
            log([typeof cells.nonexistent, seen.join(','), Array.isArray(parsed), parsed.length, parsed[0].name].join(' '));
            """);

        Assert.Equal("undefined 0:front,1:back true 2 front", logged);
    }
    #endregion

    #region Methods (private)
    private async Task<string> Log(string script)
    {
        var result = await new DrawingMcpTools(null, null, null, root).ExecuteScript(
            script + " const _c = createCanvas(4, 4); _c.getContext('2d').fillRect(0, 0, 4, 4); _c;", 4, 4);
        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.Logs);
        return string.Join(" ", result.Logs).Replace("[LOG]", string.Empty, StringComparison.Ordinal).Trim();
    }

    /// <summary>Two grey figures on magenta, with a clear gap between them.</summary>
    private static byte[] Sheet()
    {
        using var bitmap = new SKBitmap(200, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0xFF, 0x00, 0xFF));
            using var paint = new SKPaint { Color = new SKColor(128, 128, 128), IsAntialias = true };
            canvas.DrawOval(new SKRect(20, 10, 70, 90), paint);
            canvas.DrawOval(new SKRect(120, 10, 170, 90), paint);
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
