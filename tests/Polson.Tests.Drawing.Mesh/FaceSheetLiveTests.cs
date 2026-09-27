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
/// A face built from a head sheet, the way <c>GenerateCharacter</c>'s <c>faceSheet</c> builds it.
/// </summary>
/// <remarks>
/// <b>Live: runs only when <c>POLSON_FACE_SHEET</c> names a head sheet</b> - a front then one or two
/// profiles, head and shoulders, on white or on transparency - and the face backend is installed. The
/// face is rendered front, three-quarter and profile to <c>POLSON_LIVE_OUT</c>, because whether the
/// profile gave the face its shape is a question for the eye.
/// </remarks>
public class FaceSheetLiveTests : TestsRuntime
{
    readonly ITestOutputHelper output;

    public FaceSheetLiveTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void BuildFaceFromHeads_Live_UsesEveryHeadOnTheSheet()
    {
        var path = Environment.GetEnvironmentVariable("POLSON_FACE_SHEET");
        if (path is null || !File.Exists(path) || !FaceDetector.Available) return;   // nothing to exercise here

        using var sheet = SKBitmap.Decode(path);
        var heads = CharacterBuilder.SplitSheet(sheet, 0, out var why);
        output.WriteLine($"{heads.Count} head(s){(why is null ? "" : ": " + why)}: {string.Join(", ", heads.Select(h => $"{h.Width}x{h.Height}"))}");
        Assert.InRange(heads.Count, 1, 3);
        Assert.Null(CharacterBuilder.Touching(heads));

        List<string> notes = [];
        var face = CharacterBuilder.BuildFaceFromHeads(heads[0], [.. heads.Skip(1).Select((h, i) => ($"profile {i + 1}", h))], notes);
        output.WriteLine(face.Source);
        foreach (var n in notes) output.WriteLine("  NOTE " + n);
        Assert.True(face.VertexCount >= 468);

        var dir = Environment.GetEnvironmentVariable("POLSON_LIVE_OUT") ?? Path.GetTempPath();
        var canvas = new SkiaCanvas(1500, 560);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#f2efe8";
        ctx.FillRect(0, 0, 1500, 560);
        var scale = 440f / Math.Max(1f, Convert.ToSingle(face.Bounds["height"]));
        foreach (var (yaw, x) in new[] { (0f, 250f), (45f, 750f), (90f, 1250f) })
            new MeshToolkit().Draw(ctx, face, new Dictionary<string, object?> { ["x"] = x, ["y"] = 280f, ["scale"] = scale, ["yawDeg"] = yaw });
        File.WriteAllBytes(Path.Combine(dir, "face-from-heads.png"), canvas.ToBitmap().ToPngBytes());
    }
}
