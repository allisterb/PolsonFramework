namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>mesh.proportion(...)</c> — a rigged body reshaped, with the new shape as its bind pose.
/// </summary>
/// <remarks>
/// Runs on Mesh2Motion's CC0 stock male where the reference tree is on disk, and against lastlight3's Kit
/// where that is; says NOT RUN otherwise. The checks are geometric: a bone asked to be 0.8 as long is 0.8
/// as long, the feet stay on the floor, and the reshaped body still poses.
/// </remarks>
[Collection(TomasRig.Name)]
public class ProportionTests : TestsRuntime
{
    const string Project = @"C:\Projects\PolsonRuns\lastlight3";

    static readonly string Stock = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        @"..\..\..\..\..\reference\projects\mesh2motion-app-main\static\models-variation\human\male.glb"));

    readonly ITestOutputHelper output;

    public ProportionTests(ITestOutputHelper output) => this.output = output;

    /// <summary>The stock body with its bones named as a character's are, or null where it is not on disk.</summary>
    internal static FaceMesh? StockBody()
    {
        if (!File.Exists(Stock)) return null;
        var body = new MeshToolkit(Path.GetDirectoryName(Stock)!).Load(Path.GetFileName(Stock));
        foreach (var (part, bone) in StockNames) body.Rig!.Aliases[part] = bone;
        return body;
    }

    internal static readonly Dictionary<string, string> StockNames = new()
    {
        ["hips"] = "pelvis", ["spine"] = "spine_02", ["chest"] = "spine_03", ["neck"] = "neck_01", ["head"] = "head",
        ["leftUpperArm"] = "upperarm_l", ["leftForearm"] = "lowerarm_l", ["leftHand"] = "hand_l",
        ["rightUpperArm"] = "upperarm_r", ["rightForearm"] = "lowerarm_r", ["rightHand"] = "hand_r",
        ["leftThigh"] = "thigh_l", ["leftShin"] = "calf_l", ["leftFoot"] = "foot_l",
        ["rightThigh"] = "thigh_r", ["rightShin"] = "calf_r", ["rightFoot"] = "foot_r",
    };

    static Vector3 Joint(FaceMesh m, string part)
    {
        var p = m.Rig!.JointBind[m.Rig.Aliases[part]];
        return new Vector3(p.X, p.Y, p.Z);
    }

    static float Floor(FaceMesh m) => m.Vertices.Min(v => v.Y);

    /// <summary>No factors leaves the body as it was, joint for joint and vertex for vertex.</summary>
    [Fact]
    public void TestNoFactorsChangesNothing()
    {
        if (StockBody() is not { } body) { output.WriteLine("NOT RUN: stock body not on disk"); return; }
        var same = body.Proportion(null);

        Assert.Equal(body.VertexCount, same.VertexCount);
        var worst = body.Vertices.Zip(same.Vertices, (a, b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)) + ((a.Z - b.Z) * (a.Z - b.Z)))).Max();
        output.WriteLine($"worst vertex drift {worst:E2}");
        Assert.True(worst < 1e-4f);
        foreach (var part in StockNames.Keys) Assert.True(Vector3.Distance(Joint(body, part), Joint(same, part)) < 1e-4f, part);
    }

    /// <summary>Each factor lands on its segment, and the feet stay where they stood.</summary>
    [Fact]
    public void TestFactorsLandOnTheirSegments()
    {
        if (StockBody() is not { } body) { output.WriteLine("NOT RUN: stock body not on disk"); return; }
        var p = body.Proportion(new Dictionary<string, object?> { ["thigh"] = 0.8, ["upperArm"] = 1.2, ["shoulders"] = 0.85 });

        float Len(FaceMesh m, string a, string b) => Vector3.Distance(Joint(m, a), Joint(m, b));
        Assert.Equal(0.8f * Len(body, "leftThigh", "leftShin"), Len(p, "leftThigh", "leftShin"), 3);
        Assert.Equal(Len(body, "leftShin", "leftFoot"), Len(p, "leftShin", "leftFoot"), 3);
        Assert.Equal(1.2f * Len(body, "rightUpperArm", "rightForearm"), Len(p, "rightUpperArm", "rightForearm"), 3);
        var span = MathF.Abs(Joint(body, "leftUpperArm").X - Joint(body, "rightUpperArm").X);
        var spanNew = MathF.Abs(Joint(p, "leftUpperArm").X - Joint(p, "rightUpperArm").X);
        output.WriteLine($"shoulder span {span:0.000} -> {spanNew:0.000}");
        Assert.True(spanNew < span * 0.95f);
        Assert.Equal(Floor(body), Floor(p), 3);
    }

    /// <summary>A bigger head is bigger about its own joint, and carries the neck no further.</summary>
    [Fact]
    public void TestTheHeadScalesAboutItsJoint()
    {
        if (StockBody() is not { } body) { output.WriteLine("NOT RUN: stock body not on disk"); return; }
        var p = body.Proportion(new Dictionary<string, object?> { ["head"] = 1.3 });

        var before = CharacterProportion.Measure(body)["head"];
        var after = CharacterProportion.Measure(p)["head"];
        output.WriteLine($"head share {before:0.000} -> {after:0.000}");
        Assert.InRange(after / before, 1.2f, 1.35f);
    }

    /// <summary>The reshaped body is an ordinary rig: a clip retargets onto it and the crouch stays grounded.</summary>
    [Fact]
    public void TestTheReshapedBodyStillPoses()
    {
        if (StockBody() is not { } body || !PoseRetarget.Clips(null).Any(c => c.Name == "Crouch_Idle"))
        { output.WriteLine("NOT RUN: stock body or pose library not on disk"); return; }
        var p = body.Proportion(new Dictionary<string, object?> { ["thigh"] = 0.75, ["shin"] = 0.75, ["head"] = 1.25 });

        // Ankles, not the lowest vertex: a crouch presses the toes down whatever the hips do.
        var kit = new CharacterToolkit(null);
        float Ankles(FaceMesh m, object? pose) =>
            (Convert.ToSingle(kit.Where(m, pose, "leftFoot")["y"]) + Convert.ToSingle(kit.Where(m, pose, "rightFoot")["y"])) / 2f;
        float Lift(FaceMesh m)
        {
            var crouch = kit.Retarget(m, "Crouch_Idle", new Dictionary<string, object?> { ["at"] = 0.5 });
            return Ankles(m, crouch) - Ankles(m, null);
        }

        var height = p.Vertices.Max(v => v.Y) - Floor(p);
        float plain = Lift(body), shaped = Lift(p);
        output.WriteLine($"crouch moves the ankles {plain:0.000} on the stock body, {shaped:0.000} reshaped (height {height:0.00})");
        // The stock body is not the clips' own skeleton, so even unshaped its ankles move a little.
        Assert.True(MathF.Abs(plain) < 0.03f * height);
        Assert.True(MathF.Abs(shaped) < 0.03f * height);
    }

    /// <summary>
    /// The stock body reshaped like Kit comes out with Kit's proportions. Prints both, and the result.
    /// </summary>
    [Fact]
    public void TestTheStockBodyTakesKitsProportions()
    {
        if (StockBody() is not { } body || !File.Exists(Path.Combine(Project, "characters", "kit", "character.json")))
        { output.WriteLine("NOT RUN: stock body or lastlight3's Kit not on disk"); return; }
        var kit = new CharacterToolkit(Project).Load("kit");

        var want = CharacterProportion.Measure(kit);
        var had = CharacterProportion.Measure(body);
        var got = CharacterProportion.Measure(body.Proportion(new Dictionary<string, object?> { ["like"] = kit }));

        // Compared as shares of the head-to-ankle chain, which is what proportion matches.
        float w = CharacterProportion.Chain(want), h = CharacterProportion.Chain(had), g = CharacterProportion.Chain(got);
        output.WriteLine($"{"per chain",-10} {"kit",7} {"stock",7} {"result",7}");
        foreach (var k in want.Keys) output.WriteLine($"{k,-10} {want[k] / w,7:0.000} {had[k] / h,7:0.000} {got[k] / g,7:0.000}");

        foreach (var k in want.Keys) Assert.InRange(got[k] / g / (want[k] / w), 0.97f, 1.03f);
    }
}
