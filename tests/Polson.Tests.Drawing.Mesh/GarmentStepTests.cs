namespace Polson.Tests.Drawing.Mesh;

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Polson.Drawing.Mesh;
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
    /// Probe: adds the garment step to copies of lastlight3's characters, in <c>POLSON_GARMENT_OUT</c>. The originals
    /// are a finished run's record and are not touched.
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
            var dir = Path.Combine(outDir, name);
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
}
