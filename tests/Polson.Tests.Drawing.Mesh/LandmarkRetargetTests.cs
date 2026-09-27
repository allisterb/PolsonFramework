namespace Polson.Tests.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>Character.retarget(character, Character.detect(image))</c> — a pose read off a picture's 3D body landmarks.
/// </summary>
/// <remarks>
/// A round trip with a known answer: the stock body is posed from a clip, rendered as grey clay, the render is
/// detected, and the detection is retargeted back onto the same body. Every limb should come back pointing where
/// the clip put it. Runs where the stock body, the pose library and the MediaPipe backend are all on disk.
/// </remarks>
[Collection(TomasRig.Name)]
public class LandmarkRetargetTests : TestsRuntime
{
    readonly ITestOutputHelper output;

    public LandmarkRetargetTests(ITestOutputHelper output)
    {
        this.output = output;
        // POLSON_POSE_MODEL picks the pose model, to compare Full against Heavy.
        BodyDetector.ModelOverride = Environment.GetEnvironmentVariable("POLSON_POSE_MODEL");
    }

    static readonly (string Part, string Child)[] Bones =
    [
        ("hips", "neck"), ("neck", "head"),
        ("leftUpperArm", "leftForearm"), ("leftForearm", "leftHand"),
        ("rightUpperArm", "rightForearm"), ("rightForearm", "rightHand"),
        ("leftThigh", "leftShin"), ("leftShin", "leftFoot"),
        ("rightThigh", "rightShin"), ("rightShin", "rightFoot"),
    ];

    static Vector3 At(FaceMesh m, object? pose, string part)
    {
        var p = CharacterIk.Where(m, pose, part, null);
        return new Vector3(Convert.ToSingle(p["x"]), Convert.ToSingle(p["y"]), Convert.ToSingle(p["z"]));
    }

    static Dictionary<string, Vector3> Directions(FaceMesh m, object? pose) =>
        Bones.ToDictionary(b => b.Part, b => Vector3.Normalize(At(m, pose, b.Child) - At(m, pose, b.Part)));

    /// <summary>Per-bone angle between two poses, after the one turn about the vertical that best lines them up.</summary>
    static (float Yaw, Dictionary<string, float> Errors) Compare(Dictionary<string, Vector3> truth, Dictionary<string, Vector3> got)
    {
        (float, Dictionary<string, float>) best = (0f, []);
        var bestMean = float.MaxValue;
        for (var yaw = -180; yaw < 180; yaw++)
        {
            var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * MathF.PI / 180f);
            var e = truth.ToDictionary(kv => kv.Key,
                kv => MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Transform(kv.Value, q), got[kv.Key]), -1f, 1f)) * 180f / MathF.PI);
            var mean = e.Values.Average();
            if (mean < bestMean) { bestMean = mean; best = (yaw, e); }
        }
        return best;
    }

    [Fact]
    public void TestAPoseRoundTripsThroughAPicture()
    {
        if (ProportionTests.StockBody() is not { } body || !BodyDetector.Available
            || !PoseRetarget.Clips(null).Any(c => c.Name == "Greeting"))
        { output.WriteLine("NOT RUN: stock body, pose library or body backend not on disk"); return; }

        var kit = new CharacterToolkit(null);
        var height = body.Vertices.Max(v => v.Y) - body.Vertices.Min(v => v.Y);
        var out_ = Environment.GetEnvironmentVariable("POLSON_GUIDE_OUT");
        var means = new List<float>();

        foreach (var (clip, at) in new[] { ("Greeting", 0.4), ("Crouch_Idle", 0.5), ("Idle_Rail_Call", 0.5) })
            foreach (var yaw in new[] { 0f, 40f })
            {
                var truthPose = kit.Retarget(body, clip, new Dictionary<string, object?> { ["at"] = at });
                var draw = new Dictionary<string, object?>
                {
                    ["x"] = ClayGuideProbeTests.W / 2f, ["y"] = ClayGuideProbeTests.H * 0.9f,
                    ["scale"] = ClayGuideProbeTests.H * 0.75f / height, ["yawDeg"] = yaw
                };
                using var picture = ClayGuideProbeTests.Clay(body.Pose(truthPose), draw, null);
                if (out_ is not null)
                {
                    Directory.CreateDirectory(out_);
                    using var png = picture.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine(out_, $"truth-{clip}-{yaw}.png"), png.ToArray());
                }

                var found = BodyDetector.Detect(new SkiaBitmapWrapper(picture.Copy()));
                Assert.True(found.Found, $"{clip} at {yaw}: {found.Reason}");
                var pose = kit.Retarget(body, found);
                if (out_ is not null)
                {
                    // Drawn with no turn: the picture's angle is carried in the pose.
                    var plain = new Dictionary<string, object?>(draw) { ["yawDeg"] = 0f };
                    using var back = ClayGuideProbeTests.Clay(body.Pose(pose), plain, null);
                    using var png = back.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine(out_, $"got-{clip}-{yaw}.png"), png.ToArray());
                }

                var (turn, errors) = Compare(Directions(body, truthPose), Directions(body, pose));
                var (_, rest) = Compare(Directions(body, truthPose), Directions(body, null));
                var mean = errors.Values.Average();
                means.Add(mean);
                var ankles = MathF.Min(At(body, pose, "leftFoot").Y, At(body, pose, "rightFoot").Y)
                             - MathF.Min(At(body, null, "leftFoot").Y, At(body, null, "rightFoot").Y);
                output.WriteLine($"{clip,-15} yaw {yaw,3}: mean {mean,5:0.0} deg (rest pose {rest.Values.Average(),5:0.0}), " +
                                 $"turned {turn,4} deg, lower ankle {ankles / height * 100:+0.0;-0.0}% of height; unsure: {string.Join(" ", found.Unsure)}");
                output.WriteLine("   " + string.Join("  ", errors.Select(kv => $"{kv.Key} {kv.Value:0}")));

                Assert.True(MathF.Abs(ankles) < 0.01f * height);
                Assert.True(mean < rest.Values.Average(), $"{clip} at {yaw}: {mean:0.0} deg is no better than standing at rest");
            }

        // Measured 22.7 deg over these six. Depth along the camera's line is MediaPipe's weak axis, and most of it.
        Assert.True(means.Average() < 27f, $"mean limb error {means.Average():0.0} deg");
    }

    /// <summary>
    /// Probe: the stock body posed from each picture named in <c>POLSON_POSE_IMAGES</c> (separated by <c>;</c>), drawn
    /// as the picture saw it and from the side, into <c>POLSON_GUIDE_OUT</c>.
    /// </summary>
    [Fact]
    public void RenderPosesFromPictures()
    {
        var list = Environment.GetEnvironmentVariable("POLSON_POSE_IMAGES");
        var dir = Environment.GetEnvironmentVariable("POLSON_GUIDE_OUT");
        if (list is null || dir is null || ProportionTests.StockBody() is not { } body || !BodyDetector.Available)
        { output.WriteLine("NOT RUN: set POLSON_POSE_IMAGES and POLSON_GUIDE_OUT"); return; }
        Directory.CreateDirectory(dir);

        var kit = new CharacterToolkit(null);
        var height = body.Vertices.Max(v => v.Y) - body.Vertices.Min(v => v.Y);
        foreach (var path in list.Split(';'))
        {
            using var picture = SkiaSharp.SKBitmap.Decode(path);
            var found = BodyDetector.Detect(new SkiaBitmapWrapper(picture.Copy()));
            output.WriteLine($"{Path.GetFileName(path)}: {found}");
            if (!found.Found) continue;
            var pose = kit.Retarget(body, found);
            float Ankle(object? p, string f) => At(body, p, f).Y;
            output.WriteLine($"   ankles above rest: left {(Ankle(pose, "leftFoot") - Ankle(null, "leftFoot")) / height * 100:0.0}%, " +
                             $"right {(Ankle(pose, "rightFoot") - Ankle(null, "rightFoot")) / height * 100:0.0}% of height");
            foreach (var yaw in new[] { 0f, 90f })
            {
                var draw = new Dictionary<string, object?> { ["x"] = ClayGuideProbeTests.W / 2f, ["y"] = ClayGuideProbeTests.H * 0.9f,
                    ["scale"] = ClayGuideProbeTests.H * 0.75f / height, ["yawDeg"] = yaw };
                using var clay = ClayGuideProbeTests.Clay(body.Pose(pose), draw, null);
                using var png = clay.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(path)}-{yaw}.png"), png.ToArray());
            }
        }
    }
}
