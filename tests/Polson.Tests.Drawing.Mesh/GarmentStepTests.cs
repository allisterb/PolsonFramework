namespace Polson.Tests.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Polson.Drawing.Mesh;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>The garment step of a character build: reading its result, and a probe that runs it on built characters.</summary>
public class GarmentStepTests(ITestOutputHelper output) : TestsRuntime
{
    [Fact]
    public void ASegmentedResultCarriesALabelPerVertexAndLeavesThemOutOfTheRecord()
    {
        var json = """
            {"hangs":true,"hangsBy":{"front":true,"back":true},"segmented":true,"ms":{"pose":1},
             "views":{"front":{"pose":true,"garment":{"hemBelow":"knee","sleeves":"full","score":0.9,"share":0.1,"pick":"garment#1"}}},
             "garment":{"sleeves":"full","hemBelow":"knee"},
             "projection":{"names":["none","garment","lower"],"labels":[0,1,1,2],"views":[{"name":"front","axis":"x","iou":0.8,"seen":3}],
                           "unseen":0,"counts":{"none":1,"garment":2,"lower":1}},
             "sheet":"iVBORw0KGgo="}
            """;
        var result = GarmentResult.Parse(json);

        Assert.True(result.Hangs);
        Assert.True(result.Segmented);
        Assert.Equal(["none", "garment", "lower"], result.Names);
        Assert.Equal([0, 1, 1, 2], result.Labels);
        Assert.Equal(8, result.Sheet.Length);
        // The manifest gets the readings, not four bytes a vertex of labels or an image.
        Assert.Null(result.Record["sheet"]);
        Assert.Null(result.Record["projection"]!["labels"]);
        Assert.Equal("knee", result.Record["garment"]!["hemBelow"]!.GetValue<string>());
        Assert.Equal("hangs true, 2 garment, 1 lower vertices", result.ToString());
    }

    [Fact]
    public void AnUnreadableGapIsNullAndSaysWhy()
    {
        var result = GarmentResult.Parse("""{"hangs":null,"hangsBy":{"front":null},"segmented":false,"reason":"no pose","ms":{}}""");

        Assert.Null(result.Hangs);
        Assert.False(result.Segmented);
        Assert.Empty(result.Labels);
        Assert.Equal("hangs unknown: no pose", result.ToString());
    }

    /// <summary>
    /// Probe: adds the garment step to copies of lastlight3's characters, in <c>POLSON_GARMENT_OUT/characters/</c>. The
    /// originals are a finished run's record and are not touched.
    /// </summary>
    [Fact]
    public void AddTheGarmentStepToBuiltCharacters()
    {
        var outDir = Environment.GetEnvironmentVariable("POLSON_GARMENT_OUT");
        if (string.IsNullOrEmpty(outDir) || !GarmentDetector.Available) return;
        var project = Environment.GetEnvironmentVariable("POLSON_GARMENT_PROJECT") ?? @"C:\Projects\PolsonRuns\lastlight3";
        var names = (Environment.GetEnvironmentVariable("POLSON_GARMENT_NAMES") ?? "tomas,kit,warden").Split(',');

        foreach (var name in names)
        {
            var source = Path.Combine(project, "characters", name);
            var dir = Path.Combine(outDir, CharacterToolkit.Folder, name);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));

            var started = DateTime.UtcNow;
            var record = CharacterBuilder.AddGarments(dir);
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, CharacterBuilder.Manifest)))!;
            output.WriteLine($"{name}: {(DateTime.UtcNow - started).TotalSeconds:0.0}s {record.ToJsonString()}");
            Assert.NotNull(manifest["garments"]);
            Assert.True(File.Exists(Path.Combine(dir, CharacterBuilder.GarmentsPng)));

            if (record["segmented"]!.GetValue<bool>())
            {
                // One label per vertex of the rigged body, and the body keeps its indices on the finished character.
                var labels = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, CharacterBuilder.GarmentsJson)))!["labels"]!.AsArray();
                var body = MeshGltf.Load(Path.Combine(dir, CharacterBuilder.RiggedGlb), name);
                Assert.Equal(body.VertexCount, labels.Count);
                var character = CharacterBuilder.Assemble(dir, name);
                Assert.True(character.VertexCount >= body.VertexCount);
                Assert.True(Enumerable.Range(0, body.VertexCount).All(i => character.Vertices[i] == body.Vertices[i]),
                    "the assembled character reorders the body's vertices, so the labels would land on the wrong ones");
            }
        }
    }

    [Fact]
    public void ABodyWithNoGarmentLabelledHasNothingToHang()
    {
        var body = new CharacterToolkit(null).Stock("female");
        var plan = ClothDrape.Hanging(body, new int[body.VertexCount], ["none", "garment", "lower"]);

        Assert.Equal(0, plan.Count);
        Assert.Contains("0 labelled vertices", plan.Why);
    }

    [Fact]
    public void AScaledPoseScalesEveryAngleAndTheHipMove()
    {
        var pose = new Dictionary<string, object?>
        {
            ["leftThigh"] = new Dictionary<string, object?> { ["xDeg"] = 40.0, ["zDeg"] = -10f },
            ["hips"] = new Dictionary<string, object?> { ["move"] = new Dictionary<string, object?> { ["x"] = 0.0, ["y"] = -0.2, ["z"] = 0.1 } }
        };
        var half = ClothDrape.Scaled(pose, 0.5);

        var thigh = (Dictionary<string, object?>)half["leftThigh"]!;
        Assert.Equal(20.0, thigh["xDeg"]);
        Assert.Equal(-5f, thigh["zDeg"]);
        var move = (Dictionary<string, object?>)((Dictionary<string, object?>)half["hips"]!)["move"]!;
        Assert.Equal(-0.1, (double)move["y"]!, 6);
    }

    /// <summary>
    /// Probe: drapes the characters <see cref="AddTheGarmentStepToBuiltCharacters"/> left in <c>POLSON_GARMENT_OUT</c>,
    /// in three clips, and draws each rigged against draped at panel size, textured and as clay.
    /// </summary>
    [Fact]
    public void DrapeBuiltCharacters()
    {
        var outDir = Environment.GetEnvironmentVariable("POLSON_GARMENT_OUT");
        if (string.IsNullOrEmpty(outDir) || !BlenderDriver.CanDrape || !Directory.Exists(Path.Combine(outDir, CharacterToolkit.Folder))) return;
        var names = (Environment.GetEnvironmentVariable("POLSON_GARMENT_NAMES") ?? "tomas,kit,warden").Split(',');
        var clips = new[] { ("Sitting_Idle", 0.5), ("Walk", 0.25), ("Crouch_Idle", 0.5) };
        var kit = new CharacterToolkit(outDir);

        foreach (var name in names)
        {
            var character = kit.Load(name);
            var drawn = new List<(FaceMesh Rigged, FaceMesh Draped, string Label)>();
            foreach (var (clip, at) in clips)
            {
                var pose = kit.Retarget(character, clip, new Dictionary<string, object?> { ["at"] = at });
                var result = kit.Drape(character, pose);
                output.WriteLine($"{name} {clip}: draped {result["draped"]}, {result["vertices"]} vertices, {result["seconds"]} s, " +
                                 $"cached {result["cached"]}{(result["note"] is string note ? " - " + note : "")}");
                if (result["draped"] is true)
                {
                    var again = kit.Drape(name, pose);
                    Assert.True((bool)again["cached"]!, "the second drape of the same pose was not served from the cache");
                }
                drawn.Add((character.Pose(pose), (FaceMesh)result["mesh"]!, $"{clip} {at}"));
            }

            // Panel size: a figure about 300 px tall, as it would stand in a four-panel strip.
            const int W = 220, H = 340;
            using var canvas = new SkiaCanvas(W * 4, H * drawn.Count);
            var ctx = canvas.GetContext("2d");
            ctx.FillStyle = "#f2efe8";
            ctx.FillRect(0, 0, W * 4, H * drawn.Count);
            var mt = new MeshToolkit();
            ctx.Font = "12px sans-serif";
            for (var r = 0; r < drawn.Count; r++)
                for (var c = 0; c < 4; c++)
                {
                    var (rigged, draped, label) = drawn[r];
                    mt.Draw(ctx, c % 2 == 0 ? rigged : draped, new Dictionary<string, object?>
                    {
                        ["x"] = (W * c) + (W / 2f), ["y"] = (H * r) + (H / 2f) + 10, ["scale"] = H * 0.8, ["yawDeg"] = 30f,
                        ["clay"] = c >= 2
                    });
                    ctx.FillStyle = "#333333";
                    ctx.FillText($"{label} {(c % 2 == 0 ? "rigged" : "draped")}", (W * c) + 6, (H * r) + 14);
                }
            using var png = canvas.Bitmap.Bitmap.Encode(SKEncodedImageFormat.Png, 90);
            File.WriteAllBytes(Path.Combine(outDir, $"drape-{name}.png"), png.ToArray());
        }
    }
}
