namespace Polson.Tests.Drawing;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>Character.reach</c> and <c>Character.where</c> — two-bone IK and look-at on top of a pose.
/// </summary>
/// <remarks>
/// Every check is made on the <b>posed mesh's own bones</b>, not on the solver's arithmetic, so a
/// convention slip anywhere between the solve and <c>mesh.pose</c> shows up. Live: they run where
/// lastlight3's Tomas and the pose library are on disk.
/// </remarks>
[Collection(TomasRig.Name)]
public class ReachTests : TestsRuntime
{
    const string Project = @"C:\Projects\PolsonRuns\lastlight3";

    readonly ITestOutputHelper output;

    public ReachTests(ITestOutputHelper output) => this.output = output;

    static CharacterToolkit? Kit() =>
        PoseRetarget.Clips(null).Any(c => c.Name == "Idle_Rail_Call")
        && File.Exists(Path.Combine(Project, "characters", "tomas", "character.json"))
            ? new CharacterToolkit(Project) : null;

    static Dictionary<string, object?> P(float x, float y, float z) => new() { ["x"] = x, ["y"] = y, ["z"] = z };

    static Dictionary<string, object?> Goal(string limb, object spec) => new() { [limb] = spec };

    static Vector3 V(IDictionary d) => new(Convert.ToSingle(d["x"]), Convert.ToSingle(d["y"]), Convert.ToSingle(d["z"]));

    (CharacterToolkit Kit, FaceMesh Tomas, MeshRig Rig, Dictionary<string, object?> Base)? Setup()
    {
        if (Kit() is not { } kit) { output.WriteLine("NOT RUN: pose library or character absent"); return null; }
        var tomas = kit.Load("tomas");
        return (kit, tomas, tomas.Rig!, kit.Retarget(tomas, "Idle_Rail_Call", new Dictionary<string, object?> { ["at"] = 0.5 }));
    }

    static Vector3 Joint(FaceMesh mesh, MeshRig rig, object pose, string part)
    {
        mesh.Pose(pose);
        return rig.EvaluatedAt(rig.Aliases[part]);
    }

    /// <summary>The wrist lands on a reachable target, and neither bone changes length.</summary>
    [Fact]
    public void TestAHandReachesAPointAndTheArmKeepsItsLengths()
    {
        if (Setup() is not var (kit, tomas, rig, pose)) return;
        // Inside reach by construction: 80% of the arm's length, in a new direction from the shoulder.
        var shoulder = V(kit.Where(tomas, pose, "rightUpperArm"));
        var elbow = V(kit.Where(tomas, pose, "rightForearm"));
        var wrist = V(kit.Where(tomas, pose, "rightHand"));
        var arm = Vector3.Distance(shoulder, elbow) + Vector3.Distance(elbow, wrist);
        var target = shoulder + (Vector3.Normalize(wrist - shoulder + new Vector3(0.05f, 0.12f, 0f)) * 0.8f * arm);

        var r = kit.Reach(tomas, pose, Goal("rightHand", new Dictionary<string, object?> { ["at"] = P(target.X, target.Y, target.Z) }));
        Assert.True((bool)((IDictionary)r["reached"]!)["rightHand"]!);

        var after = (Dictionary<string, object?>)r["pose"]!;
        var got = Joint(tomas, rig, after, "rightHand");
        output.WriteLine($"miss {Vector3.Distance(got, target):E2}");
        Assert.True(Vector3.Distance(got, target) < 1e-3f);

        float Len(object p, string x, string y) => Vector3.Distance(Joint(tomas, rig, p, x), Joint(tomas, rig, p, y));
        Assert.Equal(Len(pose, "rightUpperArm", "rightForearm"), Len(after, "rightUpperArm", "rightForearm"), 3);
        Assert.Equal(Len(pose, "rightForearm", "rightHand"), Len(after, "rightForearm", "rightHand"), 3);
    }

    /// <summary>Everything the goal does not need is left exactly as the pose had it.</summary>
    [Fact]
    public void TestOnlyTheChainChanges()
    {
        if (Setup() is not var (kit, tomas, _, pose)) return;
        var wrist = V(kit.Where(tomas, pose, "leftHand"));
        var after = (Dictionary<string, object?>)kit.Reach(tomas, pose,
            Goal("leftHand", new Dictionary<string, object?> { ["at"] = P(wrist.X, wrist.Y + 0.05f, wrist.Z) }))["pose"]!;
        foreach (var part in pose.Keys.Where(k => k is not ("leftUpperArm" or "leftForearm")))
            Assert.Same(pose[part], after[part]);
    }

    /// <summary>Out of reach, the arm straightens toward the target and says it fell short.</summary>
    [Fact]
    public void TestAnUnreachableGoalIsReportedAndPointedAt()
    {
        if (Setup() is not var (kit, tomas, rig, pose)) return;
        var shoulder = V(kit.Where(tomas, pose, "rightUpperArm"));
        var target = shoulder + new Vector3(-2f, 0.3f, 0.5f);
        var r = kit.Reach(tomas, pose, Goal("rightHand", new Dictionary<string, object?> { ["at"] = P(target.X, target.Y, target.Z) }));
        Assert.False((bool)((IDictionary)r["reached"]!)["rightHand"]!);
        Assert.True(Convert.ToDouble(((IDictionary)r["miss"]!)["rightHand"]) > 1.0);

        var wrist = Joint(tomas, rig, r["pose"]!, "rightHand");
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(wrist - shoulder), Vector3.Normalize(target - shoulder)), -1f, 1f)) * 180f / MathF.PI;
        output.WriteLine($"unreachable: arm points {angle:0.000} deg off the target");
        Assert.True(angle < 0.5f, $"the arm points {angle:0.00} deg off the target");
    }

    /// <summary>The bend word decides which side of the shoulder-wrist line the elbow goes.</summary>
    [Fact]
    public void TestTheElbowBendsTheWayAsked()
    {
        if (Setup() is not var (kit, tomas, rig, pose)) return;
        var wrist = V(kit.Where(tomas, pose, "leftHand"));
        var shoulder = V(kit.Where(tomas, pose, "leftUpperArm"));
        var target = shoulder + (Vector3.Normalize(wrist - shoulder) * 0.7f * Vector3.Distance(wrist, shoulder));

        float Side(string bend)
        {
            var p = kit.Reach(tomas, pose, Goal("leftHand", new Dictionary<string, object?>
                { ["at"] = P(target.X, target.Y, target.Z), ["bend"] = bend }))["pose"]!;
            var elbow = Joint(tomas, rig, p, "leftForearm");
            var axis = Vector3.Normalize(target - shoulder);
            var off = elbow - shoulder - (Vector3.Dot(elbow - shoulder, axis) * axis);
            return Vector3.Dot(off, Vector3.UnitY);
        }
        Assert.True(Side("down") < 0f);
        Assert.True(Side("up") > 0f);
    }

    /// <summary>A page goal lands on that page point when drawn with the same options.</summary>
    [Fact]
    public void TestAPageGoalLandsOnThePage()
    {
        if (Setup() is not var (kit, tomas, rig, pose)) return;
        var draw = new Dictionary<string, object?> { ["x"] = 400f, ["y"] = 620f, ["scale"] = 300f, ["yawDeg"] = 35f, ["pitchDeg"] = -10f };
        // Halfway from the wrist to the shoulder on the page, at the wrist's depth: inside reach.
        var now = kit.Where(tomas, pose, "rightHand", draw);
        var sh = kit.Where(tomas, pose, "rightUpperArm", draw);
        var tx = (Convert.ToSingle(now["x"]) + Convert.ToSingle(sh["x"])) / 2f;
        var ty = (Convert.ToSingle(now["y"]) + Convert.ToSingle(sh["y"])) / 2f;

        var r = kit.Reach(tomas, pose, Goal("rightHand", new Dictionary<string, object?>
            { ["page"] = new Dictionary<string, object?> { ["x"] = tx, ["y"] = ty } }), draw);
        Assert.True((bool)((IDictionary)r["reached"]!)["rightHand"]!);
        var after = r["pose"]!;
        var landed = kit.Where(tomas, after, "rightHand", draw);
        var err = MathF.Sqrt(MathF.Pow(Convert.ToSingle(landed["x"]) - tx, 2) + MathF.Pow(Convert.ToSingle(landed["y"]) - ty, 2));
        output.WriteLine($"page error {err:0.000} px");
        Assert.True(err < 0.5f);

        // And where() agrees with the posed mesh.
        tomas.Pose(after);
        Assert.True(Vector3.Distance(rig.EvaluatedAt(rig.Aliases["rightHand"]), V(kit.Where(tomas, after, "rightHand"))) < 1e-4f);
    }

    /// <summary>The head turns its face toward a target within its range.</summary>
    [Fact]
    public void TestTheHeadLooksAtATarget()
    {
        if (Setup() is not var (kit, tomas, rig, pose)) return;
        var head = V(kit.Where(tomas, pose, "head"));
        var target = head + new Vector3(0.4f, 0.1f, 0.6f);
        var r = kit.Reach(tomas, pose, Goal("head", new Dictionary<string, object?> { ["lookAt"] = P(target.X, target.Y, target.Z) }));
        Assert.True((bool)((IDictionary)r["reached"]!)["head"]!);

        tomas.Pose(r["pose"]!);
        var handle = rig.Aliases["head"];
        Matrix4x4.Decompose(rig.FileNodes[handle].WorldMatrix, out _, out var rest, out _);
        Matrix4x4.Decompose(rig.EvaluatedMatrix(handle), out _, out var now, out _);
        var face = Vector3.Transform(Vector3.Transform(Vector3.UnitZ, Quaternion.Inverse(rest)), now);
        var want = Vector3.Normalize(target - rig.EvaluatedAt(handle));
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(face), want), -1f, 1f)) * 180f / MathF.PI;
        output.WriteLine($"look error {angle:0.00} deg");
        Assert.True(angle < 1f);
    }

    [Fact]
    public void TestBadGoalsAreRefusedByName()
    {
        if (Setup() is not var (kit, tomas, _, pose)) return;
        Assert.Contains("rightHand", Assert.Throws<ArgumentException>(() =>
            kit.Reach(tomas, pose, Goal("rightFist", new Dictionary<string, object?> { ["at"] = P(0, 0, 0) }))).Message);
        Assert.Contains("only one", Assert.Throws<ArgumentException>(() => kit.Reach(tomas, pose, Goal("rightHand", new Dictionary<string, object?>
            { ["at"] = P(0, 0, 0), ["page"] = new Dictionary<string, object?> { ["x"] = 1, ["y"] = 1 } }))).Message);
        Assert.Contains("draw options", Assert.Throws<ArgumentException>(() => kit.Reach(tomas, pose, Goal("rightHand", new Dictionary<string, object?>
            { ["page"] = new Dictionary<string, object?> { ["x"] = 1, ["y"] = 1 } }))).Message);
        Assert.Contains("sideways", Assert.Throws<ArgumentException>(() => kit.Reach(tomas, pose, Goal("rightHand", new Dictionary<string, object?>
            { ["at"] = P(0, 0, 0), ["bend"] = "sideways" }))).Message);
    }
}
