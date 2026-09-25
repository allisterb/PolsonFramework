namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// The character builder's local stages against a real rigged character and its turnaround.
/// </summary>
/// <remarks>
/// <b>Live: runs only when <c>POLSON_CHARACTER_DIR</c> names a folder holding <c>char_rigged.glb</c>
/// and <c>turnaround.jpg</c></b> — a four-view sheet laid out FRONT, BACK, LEFT, RIGHT — and the face
/// backend is installed. The assets are a director's, not fixtures this repository can carry; without
/// them each test passes having said so, as the other live tests here do.
/// </remarks>
public class CharacterBuilderLiveTests : TestsRuntime
{
    readonly ITestOutputHelper output;

    public CharacterBuilderLiveTests(ITestOutputHelper output) => this.output = output;

    static string? Dir()
    {
        var d = Environment.GetEnvironmentVariable("POLSON_CHARACTER_DIR");
        return d is not null && File.Exists(Path.Combine(d, "char_rigged.glb")) && PoseDetector.Available && FaceDetector.Available
            ? d : null;
    }

    static SKBitmap View(SKBitmap sheet, int x0, int x1)
    {
        var crop = new SKBitmap(x1 - x0, sheet.Height - 90);
        sheet.ExtractSubset(crop, new SKRectI(x0, 90, x1, sheet.Height));
        return crop;
    }

    [Fact]
    public void JointsAreNamedFromTheDetectedBody()
    {
        if (Dir() is not { } dir) { output.WriteLine("NOT RUN: set POLSON_CHARACTER_DIR"); return; }
        var mesh = new MeshToolkit(dir).Load("char_rigged.glb");
        var labels = CharacterBuilder.LabelJoints(mesh);

        output.WriteLine($"front {labels.FrontSign}");
        foreach (var (name, handle) in labels.Map.OrderBy(kv => kv.Key))
            output.WriteLine($"  {name,-14} {handle,-8} {labels.Error[name]:P1}");
        foreach (var w in labels.Warnings) output.WriteLine("  WARN " + w);
        if (labels.Render is { } r)
        {
            using var png = r.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(dir, "label-render.png"), png.ToArray());
        }

        // Measured by hand on this character before the labeller existed.
        Assert.Equal("bone_5", labels.Map["head"]);
        Assert.Equal("bone_4", labels.Map["neck"]);
        Assert.Equal(1, labels.FrontSign);
    }

    [Fact]
    public void ASheetSplitsIntoItsFigures()
    {
        if (Dir() is not { } dir) { output.WriteLine("NOT RUN: set POLSON_CHARACTER_DIR"); return; }
        using var sheet = SKBitmap.Decode(Path.Combine(dir, "turnaround.jpg"));
        var figures = CharacterBuilder.SplitSheet(sheet, 4, out var why);
        output.WriteLine(why ?? string.Join(", ", figures.Select(f => $"{f.Width}x{f.Height}")));
        for (var i = 0; i < figures.Count; i++)
        {
            using var png = figures[i].Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(dir, $"split-{i}.png"), png.ToArray());
        }
        Assert.Equal(4, figures.Count);
        // The labels are gone: every figure starts below the caption row, which ends near y = 70.
        Assert.All(figures, f => Assert.True(f.Height < sheet.Height - 40, $"a figure kept its caption ({f.Height} of {sheet.Height})"));
    }

    [Fact]
    public void TheFaceIsBuiltFromTheFullFigureViews()
    {
        if (Dir() is not { } dir) { output.WriteLine("NOT RUN: set POLSON_CHARACTER_DIR"); return; }
        using var sheet = SKBitmap.Decode(Path.Combine(dir, "turnaround.jpg"));
        var views = new Dictionary<string, SKBitmap>
        {
            ["front"] = View(sheet, 0, 590), ["back"] = View(sheet, 585, 1110),
            ["left"] = View(sheet, 1140, 1560), ["right"] = View(sheet, 1560, 2000),
        };
        var front = CharacterBuilder.FindHead(views["front"], null, out _);
        foreach (var (name, v) in views)
        {
            var box = CharacterBuilder.FindHead(v, front, out var why);
            output.WriteLine($"{name}: {box?.ToString() ?? why}");
            if (box is { } bx)
            {
                using var png = CharacterBuilder.CropHead(v, bx).Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(dir, $"head-{name}.png"), png.ToArray());
            }
        }

        List<string> notes = [];
        var face = CharacterBuilder.BuildFace(views["front"], views["left"], views["right"], notes);
        output.WriteLine(face.Source);
        foreach (var n in notes) output.WriteLine("  NOTE " + n);
        Assert.True(face.VertexCount >= 468);
        Assert.Contains("left", face.Source);
        Assert.Contains("right", face.Source);
    }
}
