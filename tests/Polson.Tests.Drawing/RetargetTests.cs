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
/// <c>Character.retarget</c> — a character posed from one frame of a recorded clip.
/// </summary>
/// <remarks>
/// The live tests run where the pose library (<c>models/poses/</c>) and lastlight3's Tomas are on
/// disk, and say NOT RUN otherwise. The check that matters is geometric: after posing, every arm and
/// leg bone points where the source bone points. A retarget that is wrong by a rest-pose offset
/// renders a plausible figure, so looking is not enough.
/// </remarks>
[Collection(TomasRig.Name)]
public class RetargetTests : TestsRuntime
{
    const string Project = @"C:\Projects\PolsonRuns\lastlight3";

    readonly ITestOutputHelper output;

    public RetargetTests(ITestOutputHelper output) => this.output = output;

    static CharacterToolkit? Kit() =>
        PoseRetarget.Clips(null).Any(c => c.Name == "Idle_Rail_Call")
        && File.Exists(Path.Combine(Project, "characters", "tomas", "character.json"))
            ? new CharacterToolkit(Project) : null;

    /// <summary>The conversion into <c>pose</c>'s degrees undoes <c>CreateFromYawPitchRoll</c>.</summary>
    [Fact]
    public void TestYawPitchRollRoundTrips()
    {
        var rng = new Random(7);
        for (var i = 0; i < 500; i++)
        {
            var q = Quaternion.Normalize(new Quaternion(
                (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f));
            var (y, p, r) = PoseRetarget.YawPitchRoll(q);
            Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.CreateFromYawPitchRoll(y, p, r), q)) > 0.99999f);
        }
    }

    static readonly (string Part, string Child, string Src, string SrcChild)[] Limbs =
    [
        ("leftUpperArm", "leftForearm", "upperarm_l", "lowerarm_l"), ("leftForearm", "leftHand", "lowerarm_l", "hand_l"),
        ("rightUpperArm", "rightForearm", "upperarm_r", "lowerarm_r"), ("rightForearm", "rightHand", "lowerarm_r", "hand_r"),
        ("leftThigh", "leftShin", "thigh_l", "calf_l"), ("leftShin", "leftFoot", "calf_l", "foot_l"),
        ("rightThigh", "rightShin", "thigh_r", "calf_r"), ("rightShin", "rightFoot", "calf_r", "foot_r"),
    ];

    /// <summary>Every arm and leg bone ends up pointing the way the performer's did.</summary>
    [Fact]
    public void TestEveryLimbPointsWhereTheSourceDoes()
    {
        if (Kit() is not { } kit) { output.WriteLine("NOT RUN: pose library or character absent"); return; }
        var tomas = kit.Load("tomas");
        var rig = tomas.Rig!;
        var clip = PoseRetarget.Clips(null).First(c => c.Name == "Idle_Rail_Call");
        Vector3 Src(string n, float t) => clip.Model.LogicalNodes.First(x => x.Name == n).GetWorldMatrix(clip.Animation, t).Translation;

        var worst = 0f;
        foreach (var at in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            tomas.Pose(kit.Retarget(tomas, "Idle_Rail_Call", new Dictionary<string, object?> { ["at"] = at }));
            var t = at * clip.Seconds;
            foreach (var (part, child, src, srcChild) in Limbs)
            {
                var want = Vector3.Normalize(Src(srcChild, t) - Src(src, t));
                var got = Vector3.Normalize(rig.EvaluatedAt(rig.Aliases[child]) - rig.EvaluatedAt(rig.Aliases[part]));
                var deg = MathF.Acos(Math.Clamp(Vector3.Dot(want, got), -1f, 1f)) * 180f / MathF.PI;
                worst = MathF.Max(worst, deg);
            }
        }
        output.WriteLine($"worst limb direction error: {worst:0.00} deg");
        Assert.True(worst < 1f, $"a limb is {worst:0.00} degrees off the source");
    }

    /// <summary>The pose is keyed by body part, so one entry can be replaced before posing.</summary>
    [Fact]
    public void TestThePoseIsKeyedByBodyPart()
    {
        if (Kit() is not { } kit) { output.WriteLine("NOT RUN: pose library or character absent"); return; }
        var pose = kit.Retarget("tomas", "Idle_Rail_Call");
        Assert.Equal(17, pose.Count);
        Assert.Contains("head", pose.Keys);
        Assert.Contains("leftForearm", pose.Keys);
        Assert.DoesNotContain(pose.Keys, k => k.StartsWith("bone_", StringComparison.Ordinal));
        Assert.Contains(kit.Clips().Cast<IDictionary<string, object?>>(), c => (string)c["name"]! == "Idle_Rail_Call");
    }

    [Fact]
    public void TestBadRequestsAreRefusedByName()
    {
        if (Kit() is not { } kit) { output.WriteLine("NOT RUN: pose library or character absent"); return; }
        var both = Assert.Throws<ArgumentException>(() => kit.Retarget("tomas", "Idle_Rail_Call",
            new Dictionary<string, object?> { ["at"] = 0.5, ["time"] = 1.0 }));
        Assert.Contains("not both", both.Message);
        Assert.Throws<ArgumentException>(() => kit.Retarget("tomas", "Idle_Rail_Call", new Dictionary<string, object?> { ["at"] = 1.5 }));
        var opt = Assert.Throws<ArgumentException>(() => kit.Retarget("tomas", "Idle_Rail_Call", new Dictionary<string, object?> { ["frame"] = 3 }));
        Assert.Contains("frame", opt.Message);
        var clip = Assert.Throws<ArgumentException>(() => kit.Retarget("tomas", "Idle_Rail_Cal"));
        Assert.Contains("Idle_Rail_Call", clip.Message);

        // A rig with no body-part names cannot be mapped onto a skeleton.
        var bare = new MeshToolkit(Path.Combine(Project, "characters", "tomas")).Load("rigged.glb");
        Assert.Contains("Character.load", Assert.Throws<ArgumentException>(() => kit.Retarget(bare, "Idle_Rail_Call")).Message);
    }
}

/// <summary>
/// The tests that pose lastlight3's Tomas, run one class at a time: <c>Character.load</c> caches one
/// mesh per character, and reading a bone back after <c>pose</c> reads that shared rig.
/// </summary>
[CollectionDefinition(Name)]
public class TomasRig
{
    public const string Name = "Tomas rig";
}
