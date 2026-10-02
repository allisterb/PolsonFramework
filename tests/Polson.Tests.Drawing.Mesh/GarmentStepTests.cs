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

            // The second rig, from the same unrigged body, and the labels carried across to it.
            if (!SolverRig.Available) continue;
            started = DateTime.UtcNow;
            var rig = CharacterBuilder.AddSolverRig(dir);
            output.WriteLine($"{name}: solver rig in {(DateTime.UtcNow - started).TotalSeconds:0.0}s {rig.ToJsonString()}");
            manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, CharacterBuilder.Manifest)))!;
            Assert.Equal(CharacterBuilder.UniRig, manifest["rig"]!["default"]!.GetValue<string>());
            Assert.Equal([CharacterBuilder.UniRig, CharacterBuilder.Solver], CharacterBuilder.BuiltRigs(manifest.AsObject()));
            var solver = MeshGltf.Load(Path.Combine(dir, CharacterBuilder.RiggedSolverGlb), name);
            Assert.True(solver.Posable);
            if (record["segmented"]!.GetValue<bool>())
            {
                var carried = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "garments-solver.json")))!["labels"]!.AsArray();
                Assert.Equal(solver.VertexCount, carried.Count);
            }
        }
    }

    [Fact]
    public void ACharacterLoadsWithItsDefaultRigOrTheOneAskedFor()
    {
        // Two rigs of one body, as a build that made both leaves them: the stock female, which is on the template's skeleton.
        var root = Path.Combine(Path.GetTempPath(), "polson-rigs-" + Guid.NewGuid().ToString("N")[..8]);
        var dir = Path.Combine(root, CharacterToolkit.Folder, "ada");
        Directory.CreateDirectory(dir);
        try
        {
            var stock = CharacterStock.List(null).First(s => s.Name == "female").File;
            File.Copy(stock, Path.Combine(dir, CharacterBuilder.RiggedGlb));
            File.Copy(stock, Path.Combine(dir, CharacterBuilder.RiggedSolverGlb));
            var parts = new JsonObject(CharacterStock.Parts.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
            File.WriteAllText(Path.Combine(dir, CharacterBuilder.Manifest), new JsonObject
            {
                ["files"] = new JsonObject { ["rigged"] = CharacterBuilder.RiggedGlb, ["riggedSolver"] = CharacterBuilder.RiggedSolverGlb },
                ["joints"] = parts.DeepClone(),
                ["rig"] = new JsonObject { ["default"] = CharacterBuilder.Solver, ["solver"] = new JsonObject { ["joints"] = parts, ["facing"] = "+Z" } }
            }.ToJsonString());

            var kit = new CharacterToolkit(root);
            var byDefault = kit.Load("ada");
            var uni = kit.Load("ada", new Dictionary<string, object?> { ["rig"] = "unirig" });
            Assert.EndsWith(CharacterBuilder.RiggedSolverGlb, byDefault.Source);
            Assert.EndsWith(CharacterBuilder.RiggedGlb, uni.Source);
            Assert.NotSame(byDefault, uni);
            Assert.Same(byDefault, kit.Load("ada", new Dictionary<string, object?> { ["rig"] = "solver" }));
            Assert.Equal(CharacterStock.Parts.Count, byDefault.JointMap.Count);

            var bad = Assert.Throws<ArgumentException>(() => kit.Load("ada", new Dictionary<string, object?> { ["rig"] = "blender" }));
            Assert.Contains("unirig and solver", bad.Message);
            var unknown = Assert.Throws<ArgumentException>(() => kit.Load("ada", new Dictionary<string, object?> { ["rigs"] = "solver" }));
            Assert.Contains("takes rig", unknown.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TheRigidResidualIsNothingForARigidMoveAndSomethingForABend()
    {
        var rest = new List<SKPoint3>();
        for (var i = 0; i < 20; i++)
            for (var j = 0; j < 5; j++) rest.Add(new SKPoint3(j * 0.1f, i * 0.1f, (i * j) % 3 * 0.05f));
        var all = Enumerable.Repeat(true, rest.Count).ToArray();

        // Turned 40 degrees about a tilted axis and moved: one rigid piece.
        var q = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.Normalize(new(1, 2, 0.5f)), 0.7f);
        var moved = rest.Select(p =>
        {
            var v = System.Numerics.Vector3.Transform(new(p.X, p.Y, p.Z), q) + new System.Numerics.Vector3(0.3f, -0.2f, 1f);
            return new SKPoint3(v.X, v.Y, v.Z);
        }).ToArray();
        Assert.True(RigScores.Residual([.. rest], moved, all) < 1e-4);

        // Bent at its middle, as a leg bends at the knee: not one piece.
        var bent = rest.Select(p => p.Y < 1f ? p : new SKPoint3(p.X, 1f, p.Z + (p.Y - 1f))).ToArray();
        Assert.True(RigScores.Residual([.. rest], bent, all) > 0.05);
    }

    [Fact]
    public void ANewBuildDefaultsToTheSolverRigUnlessAskedOtherwise()
    {
        Assert.Equal(CharacterBuilder.Solver, CharacterBuilder.ChooseDefaultRig("auto", hasUniRig: true, hasSolver: true));
        Assert.Equal(CharacterBuilder.UniRig, CharacterBuilder.ChooseDefaultRig("auto", hasUniRig: true, hasSolver: false));
        Assert.Equal(CharacterBuilder.Solver, CharacterBuilder.ChooseDefaultRig("auto", hasUniRig: false, hasSolver: true));
        Assert.Equal(CharacterBuilder.UniRig, CharacterBuilder.ChooseDefaultRig(CharacterBuilder.UniRig, hasUniRig: true, hasSolver: true));
        Assert.Equal(CharacterBuilder.Solver, CharacterBuilder.ChooseDefaultRig(CharacterBuilder.Solver, hasUniRig: true, hasSolver: true));
        Assert.Throws<InvalidOperationException>(() => CharacterBuilder.ChooseDefaultRig("auto", hasUniRig: false, hasSolver: false));
    }

    [Fact]
    public void AManifestFromBeforeTheSolverRigIsUniRigsAlone()
    {
        var manifest = JsonNode.Parse("""{ "files": { "rigged": "rigged.glb" }, "rig": { "joints": 52, "weightedJoints": 50 } }""")!.AsObject();

        Assert.Equal(CharacterBuilder.UniRig, CharacterBuilder.DefaultRig(manifest));
        Assert.Equal([CharacterBuilder.UniRig], CharacterBuilder.BuiltRigs(manifest));
        Assert.Null(CharacterBuilder.RigFile(manifest, CharacterBuilder.Solver));
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
    /// Probe: scores the rigs of the characters in <c>POLSON_GARMENT_OUT</c> (built there by
    /// <see cref="AddTheGarmentStepToBuiltCharacters"/>), writing each one's <c>preview-rigs.png</c>.
    /// </summary>
    [Fact]
    public void ScoreTheRigsOfBuiltCharacters()
    {
        var outDir = Environment.GetEnvironmentVariable("POLSON_GARMENT_OUT");
        if (string.IsNullOrEmpty(outDir) || !Directory.Exists(Path.Combine(outDir, CharacterToolkit.Folder))) return;
        foreach (var dir in Directory.GetDirectories(Path.Combine(outDir, CharacterToolkit.Folder)))
        {
            var started = DateTime.UtcNow;
            var scores = CharacterBuilder.AddRigScores(dir);
            output.WriteLine($"{Path.GetFileName(dir)}: {(DateTime.UtcNow - started).TotalSeconds:0.0}s");
            foreach (var rig in CharacterBuilder.Rigs)
                if (scores[rig] is JsonObject s)
                {
                    output.WriteLine($"  {rig}: stretch {s["stretch"]}, p95 {s["p95"]}, torn {s["torn"]}, rigidGarment {s["rigidGarment"]?.ToJsonString() ?? "-"}");
                    foreach (var (clip, c) in s["clips"]!.AsObject())
                        output.WriteLine($"    {clip,-18} stretch {c!["stretch"]}  p95 {c["p95"]}  torn {c["torn"]}  rigid {c["rigidGarment"]?.ToJsonString() ?? "-"}" +
                                         $"  (garment {c["garmentResidual"]?.ToJsonString() ?? "-"}, legs {c["legResidual"]?.ToJsonString() ?? "-"})");
                }
            Assert.True(File.Exists(Path.Combine(dir, CharacterBuilder.PreviewRigsPng)) || CharacterBuilder.Rigs.Count(r => scores[r] is not null) < 2);
        }
    }

    /// <summary>
    /// Probe: drapes the characters <see cref="AddTheGarmentStepToBuiltCharacters"/> left in <c>POLSON_GARMENT_OUT</c>,
    /// with each rig it has, in three clips, and draws each rigged against draped at panel size, as clay.
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
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, CharacterToolkit.Folder, name, CharacterBuilder.Manifest)))!.AsObject();
            var rigs = CharacterBuilder.BuiltRigs(manifest);
            var drawn = new List<(FaceMesh Mesh, string Label)[]>();
            foreach (var (clip, at) in clips)
            {
                var row = new List<(FaceMesh, string)>();
                foreach (var rig in rigs)
                {
                    var character = kit.Load(name, new Dictionary<string, object?> { ["rig"] = rig });
                    var pose = kit.Retarget(character, clip, new Dictionary<string, object?> { ["at"] = at });
                    var result = kit.Drape(character, pose);
                    output.WriteLine($"{name} {rig} {clip}: draped {result["draped"]}, {result["vertices"]} vertices, {result["seconds"]} s, " +
                                     $"cached {result["cached"]}{(result["note"] is string note ? " - " + note : "")}");
                    if (result["draped"] is true)
                    {
                        var again = kit.Drape(character, pose);
                        Assert.True((bool)again["cached"]!, "the second drape of the same pose was not served from the cache");
                    }
                    row.Add((character.Pose(pose), $"{clip} {rig} rigged"));
                    row.Add(((FaceMesh)result["mesh"]!, $"{rig} draped"));
                }
                drawn.Add([.. row]);
            }

            // Panel size: a figure about 300 px tall, as it would stand in a four-panel strip; clay, so shape is all that shows.
            const int W = 220, H = 340;
            var cols = drawn[0].Length;
            using var canvas = new SkiaCanvas(W * cols, H * drawn.Count);
            var ctx = canvas.GetContext("2d");
            ctx.FillStyle = "#f2efe8";
            ctx.FillRect(0, 0, W * cols, H * drawn.Count);
            var mt = new MeshToolkit();
            ctx.Font = "12px sans-serif";
            for (var r = 0; r < drawn.Count; r++)
                for (var c = 0; c < cols; c++)
                {
                    mt.Draw(ctx, drawn[r][c].Mesh, new Dictionary<string, object?>
                    {
                        ["x"] = (W * c) + (W / 2f), ["y"] = (H * r) + (H / 2f) + 10, ["scale"] = H * 0.8, ["yawDeg"] = 30f, ["clay"] = true
                    });
                    ctx.FillStyle = "#333333";
                    ctx.FillText(drawn[r][c].Label, (W * c) + 6, (H * r) + 14);
                }
            using var png = canvas.Bitmap.Bitmap.Encode(SKEncodedImageFormat.Png, 90);
            File.WriteAllBytes(Path.Combine(outDir, $"drape-{name}.png"), png.ToArray());
        }
    }
}
