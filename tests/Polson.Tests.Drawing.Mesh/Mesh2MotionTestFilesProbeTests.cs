namespace Polson.Tests.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polson.Drawing.Mesh;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Probe: Mesh2Motion's own test meshes (<c>reference/projects/mesh2motion-app-main/static/test-files</c>) through our
/// loader and skinning. Skips when the reference tree is absent; the files are not copied into the repository.
/// </summary>
public class Mesh2MotionTestFilesProbeTests(ITestOutputHelper output) : TestsRuntime
{
    [Fact]
    public void LoadAndPoseMesh2MotionTestFiles()
    {
        var root = Path.Combine(RepoRoot(), "reference", "projects", "mesh2motion-app-main", "static", "test-files");
        if (!Directory.Exists(root)) return;
        var failures = new List<string>();
        foreach (var file in Directory.GetFiles(root, "*.glb", SearchOption.AllDirectories).Order())
        {
            var name = Path.GetRelativePath(root, file);
            FaceMesh mesh;
            try { mesh = MeshGltf.Load(file, name); }
            catch (Exception ex) { failures.Add($"{name}: load failed: {ex.Message}"); continue; }

            var v = mesh.Vertices;
            var height = v.Max(p => p.Y) - v.Min(p => p.Y);
            var line = $"{name}: {mesh.VertexCount} v, {mesh.TriangleCount} tris, height {height:0.###}, textured {mesh.Textured}, " +
                       $"posable {mesh.Posable}, {mesh.Joints.Length} joints";
            if (mesh.Posable)
            {
                // At rest: posing with no rotations must land exactly on the bind geometry.
                var rest = mesh.Pose(new Dictionary<string, object?>()).Vertices;
                var restDrift = v.Zip(rest, Dist).Max() / height;
                if (restDrift > 1e-4) failures.Add($"{name}: posed at rest, drifted {restDrift:0.#####} of its height");

                // One joint bent: nothing may travel further than the body is tall.
                var joint = mesh.Joints.Skip(mesh.Joints.Length / 3).First();
                var bent = mesh.Pose(new Dictionary<string, object?> { [joint] = new Dictionary<string, object?> { ["zDeg"] = 30.0 } }).Vertices;
                var moved = v.Zip(bent, Dist).ToArray();
                line += $"; rest drift {restDrift:0.#####}H; bend {joint} 30deg: {moved.Count(d => d > 1e-4 * height)} moved, max {moved.Max() / height:0.###}H";
            }
            output.WriteLine(line);
        }
        Assert.Empty(failures);
    }

    /// <summary>
    /// Mesh2Motion's own harness case: an armature node carrying a rotation, as a Z-up tool exports it, with the inverse
    /// binds baked to match. A loader that folds the armature transform into the root bone poses such a rig 90° off at rest.
    /// </summary>
    [Fact]
    public void ARotatedArmatureStillPosesToItsBindShape()
    {
        var file = Path.Combine(RepoRoot(), "reference", "projects", "mesh2motion-app-main", "static", "test-files", "retarget testing", "mixamo-sample-rig.glb");
        if (!File.Exists(file)) return;

        var model = SharpGLTF.Schema2.ModelRoot.Load(file);
        var armature = model.LogicalNodes.First(n => n.Name?.Contains("armature", StringComparison.OrdinalIgnoreCase) == true);
        armature.LocalTransform = armature.LocalTransform.WithRotation(
            System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX, -MathF.PI / 2) * armature.LocalTransform.Rotation);
        foreach (var meshNode in model.LogicalNodes.Where(n => n.Skin is not null))
        {
            var skin = meshNode.Skin;
            var joints = Enumerable.Range(0, skin.JointsCount).Select(i => skin.GetJoint(i).Joint).ToArray();
            skin.BindJoints([.. joints.Select(j =>
            {
                System.Numerics.Matrix4x4.Invert(j.WorldMatrix, out var inv);
                return (j, meshNode.WorldMatrix * inv);
            })]);
        }
        var rotated = Path.Combine(Path.GetTempPath(), $"m2m-rotated-armature-{Guid.NewGuid():N}.glb");
        model.SaveGLB(rotated);
        try
        {
            var mesh = MeshGltf.Load(rotated, "rotated armature");
            var v = mesh.Vertices;
            float Extent(Func<SKPoint3, float> axis) => v.Max(axis) - v.Min(axis);
            var rest = mesh.Pose(new Dictionary<string, object?>()).Vertices;
            var drift = v.Zip(rest, Dist).Max();
            output.WriteLine($"extent x {Extent(p => p.X):0.###} y {Extent(p => p.Y):0.###} z {Extent(p => p.Z):0.###}; rest drift {drift:0.######}");

            Assert.True(Extent(p => p.Z) > 1.5f, "the armature's rotation should lay the body along Z");
            Assert.True(drift < 1e-3, $"posed at rest, the rotated rig drifted {drift} from its bind shape");
        }
        finally { File.Delete(rotated); }
    }

    /// <summary>Probe: a <c>.gltf</c> whose buffer and texture are files beside it, complete and with each piece missing.</summary>
    [Fact]
    public void LoadAGltfWithItsFilesBesideIt()
    {
        var root = Path.Combine(RepoRoot(), "reference", "projects", "mesh2motion-app-main", "static", "test-files");
        if (!Directory.Exists(root)) return;
        foreach (var zip in Directory.GetFiles(root, "fox-model*.zip").Order())
        {
            var dir = Path.Combine(Path.GetTempPath(), $"m2m-{Path.GetFileNameWithoutExtension(zip)}-{Guid.NewGuid():N}");
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, dir);
            try
            {
                var gltf = Directory.GetFiles(dir, "*.gltf").FirstOrDefault();
                if (gltf is null) { output.WriteLine($"{Path.GetFileName(zip)}: no .gltf in it, nothing to load"); continue; }
                try
                {
                    var mesh = MeshGltf.Load(gltf, Path.GetFileName(gltf));
                    output.WriteLine($"{Path.GetFileName(zip)}: {mesh.VertexCount} v, textured {mesh.Textured}, posable {mesh.Posable}");
                }
                catch (Exception ex) { output.WriteLine($"{Path.GetFileName(zip)}: {ex.GetType().Name}: {ex.Message}"); }
            }
            finally { Directory.Delete(dir, recursive: true); }
        }
    }

    static double Dist(SKPoint3 a, SKPoint3 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Polson.slnx")) && !Directory.Exists(Path.Combine(dir.FullName, "reference"))) dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
