namespace Polson.Tests.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Probe: Mesh2Motion's human rig template fitted into lastlight3's unrigged TRELLIS bodies, against UniRig's rig of
/// the same bodies.
/// </summary>
/// <remarks>
/// <para>
/// Both rigs are measured the same way: for each of the 17 named joints, how far the two disagree, and how well each
/// joint is centred in the body's cross-section there (0 is the middle, 1 is on the surface, more is outside). The
/// fit takes its joints across the picture from the detected landmarks, so agreement with those is not a fair test
/// of it; UniRig's own distance from them is in each character's <c>jointError</c>.
/// </para>
/// <para>
/// Runs only with <c>POLSON_RIGFIT_OUT</c> set, where lastlight3, the rig template in <c>reference/</c> and the body
/// detector are on disk; writes a front and a side drawing of both skeletons per character there.
/// </para>
/// </remarks>
[Collection(TomasRig.Name)]
public class SkeletonFitProbeTests : TestsRuntime
{
    /// <summary>The project and characters probed: lastlight3's three, or <c>POLSON_RIGFIT_PROJECT</c> and <c>POLSON_RIGFIT_NAMES</c>.</summary>
    static readonly string Project = Environment.GetEnvironmentVariable("POLSON_RIGFIT_PROJECT") ?? @"C:\Projects\PolsonRuns\lastlight3";

    static readonly string[] Names = (Environment.GetEnvironmentVariable("POLSON_RIGFIT_NAMES") ?? "tomas,kit,warden").Split(',');

    static readonly string TemplatePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "reference", "projects", "mesh2motion-app-main", "static", "rigs", "rig-human.glb"));

    readonly ITestOutputHelper output;

    public SkeletonFitProbeTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void FitTheTemplateAndCompareWithUniRig()
    {
        var dir = Environment.GetEnvironmentVariable("POLSON_RIGFIT_OUT");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(Project) || !File.Exists(TemplatePath) || !BodyDetector.Available)
        { output.WriteLine("NOT RUN: set POLSON_RIGFIT_OUT, with lastlight3, the rig template and the body detector on disk"); return; }
        Directory.CreateDirectory(dir);

        var template = SkeletonFit.LoadTemplate(TemplatePath);
        output.WriteLine($"template: {template.At.Count} bones");
        foreach (var name in Names)
        {
            var folder = Path.Combine(Project, "characters", name);
            var tk = new MeshToolkit(folder);
            var body = tk.Load("mesh.glb");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var fit = SkeletonFit.Fit(body, template);
            var ms = sw.ElapsedMilliseconds;

            var rigged = tk.Load("rigged.glb");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "character.json")));
            var uni = doc.RootElement.GetProperty("joints").EnumerateObject()
                .ToDictionary(p => p.Name, p => rigged.Rig!.JointBind[p.Value.GetString()!]);

            var verts = body.Reference.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
            output.WriteLine($"\n{name}: {body.VertexCount} vertices, facing {(fit.FrontSign > 0 ? "+Z" : "-Z")}, "
                           + $"fitted {fit.Joints.Count} bones in {ms} ms{(fit.Warnings.Count > 0 ? "; " + string.Join(" ", fit.Warnings) : "")}");
            output.WriteLine($"  {"part",-14} {"apart",6} {"fit ctr",8} {"uni ctr",8}");
            float sumApart = 0, sumFit = 0, sumUni = 0;
            foreach (var (part, bone) in CharacterStock.Parts)
            {
                var a = fit.Joints[bone];
                var u = new Vector3(uni[part].X, uni[part].Y, uni[part].Z);
                var apart = (a - u).Length() / fit.Height;
                float cf = Centring(verts, a, fit.Height), cu = Centring(verts, u, fit.Height);
                sumApart += apart; sumFit += cf; sumUni += cu;
                output.WriteLine($"  {part,-14} {apart,6:P1} {cf,8:0.00} {cu,8:0.00}");
            }
            var floorY = verts.Min(p => p.Y);
            output.WriteLine($"  facing scores +Z {fit.FacingScore.GetValueOrDefault(1):0.000}, -Z {fit.FacingScore.GetValueOrDefault(-1):0.000}; "
                           + $"ankles above floor: fit {(fit.Joints["foot_l"].Y - floorY) / fit.Height:P1}, uni {(uni["leftFoot"].Y - floorY) / fit.Height:P1}; "
                           + $"toes forward of shins (mesh, +Z): {fit.ToesForward:0.000}");
            var n = CharacterStock.Parts.Count;
            output.WriteLine($"  {"mean",-14} {sumApart / n,6:P1} {sumFit / n,8:0.00} {sumUni / n,8:0.00}");

            Draw(Path.Combine(dir, $"{name}-front.png"), verts, fit, rigged, uni, side: false);
            Draw(Path.Combine(dir, $"{name}-side.png"), verts, fit, rigged, uni, side: true);
            fit.Render?.Dispose();
        }
        output.WriteLine($"\ndrawings in {dir}");
    }

    static readonly (string Clip, double At)[] Clips =
        [("Crouch_Idle", 0.5), ("Walk", 0.25), ("Idle_FoldArms", 0.5), ("Sitting_Idle", 0.5), ("Jumping Jacks", 0.3)];

    /// <summary>
    /// Rigs each body with the fitted template and Mesh2Motion's weights, poses it and UniRig's rig of the same body
    /// with the same clips, and measures how much each stretches the surface: every edge's posed length against its
    /// rest length. A good rig bends at the joints and leaves the rest of the surface near its own length.
    /// </summary>
    [Fact]
    public void RigWithTheTemplateAndPoseAgainstUniRig()
    {
        var dir = Environment.GetEnvironmentVariable("POLSON_RIGFIT_OUT");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(Project) || !File.Exists(TemplatePath) || !BodyDetector.Available
            || !PoseRetarget.Clips(null).Any(c => c.Name == "Crouch_Idle"))
        { output.WriteLine("NOT RUN: set POLSON_RIGFIT_OUT, with lastlight3, the rig template, the detector and the pose library on disk"); return; }
        Directory.CreateDirectory(dir);

        var template = SkeletonFit.LoadTemplate(TemplatePath);
        var kit = new CharacterToolkit(null);
        foreach (var name in Names)
        {
            var folder = Path.Combine(Project, "characters", name);
            var tk = new MeshToolkit(folder);
            var fit = SkeletonFit.Fit(tk.Load("mesh.glb"), template);
            var report = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var diffuse = int.TryParse(Environment.GetEnvironmentVariable("POLSON_RIGFIT_DIFFUSE"), out var dd) ? dd : 0;
            var coat = Environment.GetEnvironmentVariable("POLSON_RIGFIT_COAT") == "1";
            var withSkirt = Environment.GetEnvironmentVariable("POLSON_RIGFIT_SKIRT") == "1";
            var (glb, skirt) = SkeletonFit.RigWithSkirt(Path.Combine(folder, "mesh.glb"), fit, template, withSkirt, armPlane: true, report, diffuse, coat);
            File.WriteAllBytes(Path.Combine(dir, $"{name}-fit.glb"), glb);
            output.WriteLine($"\n{name}: rigged in {sw.ElapsedMilliseconds} ms ({glb.Length / 1e6:0.0} MB); {string.Join("; ", report)}");
            fit.Render?.Dispose();

            var ours = new MeshToolkit(dir).Load($"{name}-fit.glb");
            Assert.True(ours.Posable);
            foreach (var (part, bone) in CharacterStock.Parts) ours.Rig!.Aliases[part] = bone;
            if (skirt is not null) ours.Rig!.Driver = skirt.Drive;
            if (skirt is not null)
            {
                // The rest pose must be left as it was: a driver that moves anything here moves it in every pose.
                var still = ours.Pose(new Dictionary<string, object?>());
                var drift = Enumerable.Range(0, ours.VertexCount).Max(i => Dist(still.Vertices[i], ours.Vertices[i]));
                output.WriteLine($"  skirt at rest: largest vertex drift {drift / fit.Height:P2} of the height");
            }

            var theirs = tk.Load("rigged.glb");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "character.json")));
            foreach (var p in doc.RootElement.GetProperty("joints").EnumerateObject()) theirs.Rig!.Aliases[p.Name] = p.Value.GetString()!;

            output.WriteLine($"  {"clip",-16} {"template: mean",14} {"p95",6} {"torn",6}   {"UniRig: mean",12} {"p95",6} {"torn",6}");
            var tiles = new List<SKBitmap>();
            foreach (var (clip, at) in Clips)
            {
                var opts = new Dictionary<string, object?> { ["at"] = at };
                var a = ours.Pose(kit.Retarget(ours, clip, opts));
                var b = theirs.Pose(kit.Retarget(theirs, clip, opts));
                var (am, ap, at2) = Stretch(ours, a);
                var (bm, bp, bt) = Stretch(theirs, b);
                output.WriteLine($"  {clip,-16} {am,14:P1} {ap,6:P0} {at2,6:P1}   {bm,12:P1} {bp,6:P0} {bt,6:P1}");
                tiles.Add(Pair(a, b, clip));
            }
            Save(Path.Combine(dir, $"{name}-poses-d{diffuse}{(coat ? "-coat" : "")}{(withSkirt ? "-skirt" : "")}.png"), tiles);
            foreach (var t in tiles) t.Dispose();
        }
        output.WriteLine($"\nposes and rigs in {dir}");
    }

    /// <summary>
    /// Spike: the garment round the legs draped by Blender's cloth while the body moves from rest into a clip's pose.
    /// The body rigged with the template (diffusion 12) is a collider; the garment is cloth pinned along its top.
    /// </summary>
    /// <remarks>
    /// Runs only with <c>POLSON_CLOTH_OUT</c> and <c>POLSON_CLOTH_SCRIPT</c> (the Blender script, which is not in the
    /// repository) set. <c>POLSON_CLOTH_NAME</c>, <c>POLSON_CLOTH_CLIP</c>, <c>POLSON_CLOTH_AT</c> and
    /// <c>POLSON_CLOTH_SETTINGS</c> (a JSON object passed to the script) vary it.
    /// </remarks>
    [Fact]
    public void DrapeTheCoatWithBlenderCloth()
    {
        var dir = Environment.GetEnvironmentVariable("POLSON_CLOTH_OUT");
        var script = Environment.GetEnvironmentVariable("POLSON_CLOTH_SCRIPT");
        var blender = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "bin", "blender-5.2.2-windows-x64", "blender.exe"));
        if (string.IsNullOrEmpty(dir) || !File.Exists(script) || !File.Exists(blender) || !Directory.Exists(Project) || !File.Exists(TemplatePath))
        { output.WriteLine("NOT RUN: set POLSON_CLOTH_OUT and POLSON_CLOTH_SCRIPT, with Blender, lastlight3 and the rig template on disk"); return; }
        Directory.CreateDirectory(dir);
        var name = Environment.GetEnvironmentVariable("POLSON_CLOTH_NAME") ?? "warden";
        var clip = Environment.GetEnvironmentVariable("POLSON_CLOTH_CLIP") ?? "Sitting_Idle";
        var at = double.TryParse(Environment.GetEnvironmentVariable("POLSON_CLOTH_AT"), System.Globalization.CultureInfo.InvariantCulture, out var a0) ? a0 : 0.5;
        var settings = Environment.GetEnvironmentVariable("POLSON_CLOTH_SETTINGS") ?? "{}";
        var tag = Environment.GetEnvironmentVariable("POLSON_CLOTH_TAG") ?? "";

        var template = SkeletonFit.LoadTemplate(TemplatePath);
        var folder = Path.Combine(Project, "characters", name);
        var meshPath = Path.Combine(folder, "mesh.glb");
        var fit = SkeletonFit.Fit(new MeshToolkit(folder).Load("mesh.glb"), template);
        File.WriteAllBytes(Path.Combine(dir, $"{name}-cloth-rig.glb"), SkeletonFit.Rig(meshPath, fit, template, armPlane: true, null, diffuse: 12));
        var (positions, _, garment) = SkeletonFit.FindGarment(meshPath, fit, template);
        fit.Render?.Dispose();
        Assert.True(garment is { Count: > 0 }, "no garment round the legs");

        // A garment mask projected from the views' segmentation replaces the one found round the legs; the crotch
        // found round the legs still sets where the pinned band ends. Only the part below it hangs free.
        var mask = garment!.Mask;
        if (Environment.GetEnvironmentVariable("POLSON_CLOTH_MASK") is { Length: > 0 } maskPath)
        {
            using var mj = JsonDocument.Parse(File.ReadAllText(maskPath));
            mask = [.. mj.RootElement.GetProperty("mask").EnumerateArray().Select(e => e.GetBoolean())];
            Assert.Equal(positions.Length, mask.Length);
            output.WriteLine($"{name}: projected garment mask, {mask.Count(m => m)} vertices; round the legs: {garment.Count}");
        }
        var hanging = mask.Select((m, i) => m && positions[i].Y < garment.Top).ToArray();

        var ours = new MeshToolkit(dir).Load($"{name}-cloth-rig.glb");
        foreach (var (part, bone) in CharacterStock.Parts) ours.Rig!.Aliases[part] = bone;
        Assert.Equal(positions.Length, ours.VertexCount);
        var off = Enumerable.Range(0, positions.Length).Max(i => Dist(ours.Vertices[i], new SKPoint3(positions[i].X, positions[i].Y, positions[i].Z)));
        Assert.True(off < 1e-3 * fit.Height, $"the loaded mesh and the garment's positions differ by {off}");

        // Keys from rest to the pose: the retargeted angles and hip move scaled, so limbs swing rather than cut corners.
        var kit = new CharacterToolkit(null);
        var pose = kit.Retarget(ours, clip, new Dictionary<string, object?> { ["at"] = at });
        var keys = new List<SKPoint3[]> { ours.Vertices };
        foreach (var s in new[] { 0.2, 0.4, 0.6, 0.8 }) keys.Add(ours.Pose(Scaled(pose, s)).Vertices);
        var posed = ours.Pose(pose);
        keys.Add(posed.Vertices);

        var input = Path.Combine(dir, $"{name}-cloth-in.json");
        var result = Path.Combine(dir, $"{name}-cloth-out.json");
        var idx = ours.Indices;
        File.WriteAllText(input, JsonSerializer.Serialize(new
        {
            triangles = Enumerable.Range(0, idx.Length / 3).Select(t => new[] { (int)idx[t * 3], idx[(t * 3) + 1], idx[(t * 3) + 2] }),
            mask,
            top = garment.Top,
            keys = keys.Select(k => k.Select(p => new[] { p.X, p.Y, p.Z })),
            settings = JsonDocument.Parse(settings).RootElement
        }));
        output.WriteLine($"{name}: garment {garment.Count} vertices, top {garment.Top:0.000}, hem {garment.Hem:0.000}; {clip} at {at}");

        var info = new System.Diagnostics.ProcessStartInfo(blender) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "--background", "--factory-startup", "--disable-autoexec", "--python-exit-code", "1", "--python", script!, "--", input, result }) info.ArgumentList.Add(arg);
        File.Delete(result);   // a stale answer from an earlier run must not pass for this one
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (var p = System.Diagnostics.Process.Start(info)!)
        {
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            Assert.True(p.WaitForExit(600_000), "Blender did not finish in 10 minutes");
            foreach (var line in (stdout.Result + stderr.Result).Split('\n').Where(l => l.StartsWith("cloth:") || l.Contains("Error") || l.Contains("Traceback")))
                output.WriteLine("  " + line.TrimEnd());
            Assert.Equal(0, p.ExitCode);
        }
        output.WriteLine($"  Blender ran in {sw.ElapsedMilliseconds} ms");

        using var doc = JsonDocument.Parse(File.ReadAllText(result));
        var draped = (SKPoint3[])posed.Vertices.Clone();
        var ids = doc.RootElement.GetProperty("indices").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var pts = doc.RootElement.GetProperty("positions").EnumerateArray().ToArray();
        var moved = new List<double>();
        for (var i = 0; i < ids.Length; i++)
        {
            var q = pts[i].EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
            draped[ids[i]] = new SKPoint3(q[0], q[1], q[2]);
            moved.Add(Dist(draped[ids[i]], posed.Vertices[ids[i]]) / fit.Height);
        }
        moved.Sort();
        output.WriteLine($"  cloth against the rigged coat: mean {moved.Average():P1}, p95 {moved[(int)(0.95 * (moved.Count - 1))]:P1}, max {moved[^1]:P1} of the height");
        var cloth = new FaceMesh(draped, posed.Uvs, posed.Indices, posed.HasUvs, posed.Source);
        var (rm, rp) = GarmentStretch(ours, posed, hanging);
        var (cm, cp) = GarmentStretch(ours, cloth, hanging);
        output.WriteLine($"  hanging below the crotch: {hanging.Count(h => h)} vertices");
        output.WriteLine($"  coat edge stretch: rigged mean {rm:P1} p95 {rp:P0}; cloth mean {cm:P1} p95 {cp:P0}");

        var theirs = new MeshToolkit(folder).Load("rigged.glb");
        using (var cj = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "character.json"))))
            foreach (var p in cj.RootElement.GetProperty("joints").EnumerateObject()) theirs.Rig!.Aliases[p.Name] = p.Value.GetString()!;
        var uni = theirs.Pose(kit.Retarget(theirs, clip, new Dictionary<string, object?> { ["at"] = at }));

        var tiles = new[] { 30f, 90f, -30f }.Select(yaw => Row([(posed, "rigged"), (cloth, "rigged + Blender cloth"), (uni, "UniRig")], yaw, $"{clip} at {at}, yaw {yaw}")).ToList();
        Save(Path.Combine(dir, $"{name}-cloth-{clip}{tag}.png"), tiles);
        foreach (var t in tiles) t.Dispose();
        output.WriteLine($"  drawings in {dir}");
    }

    /// <summary>Edge stretch over the garment's own edges: mean and 95th percentile of |posed/rest - 1|.</summary>
    static (double Mean, double P95) GarmentStretch(FaceMesh rest, FaceMesh posed, bool[] mask)
    {
        var idx = rest.Indices;
        var d = new List<double>();
        for (var i = 0; i < idx.Length; i += 3)
            foreach (var (u, v) in new[] { (idx[i], idx[i + 1]), (idx[i + 1], idx[i + 2]), (idx[i + 2], idx[i]) })
            {
                if (!mask[u] || !mask[v]) continue;
                var r0 = Dist(rest.Vertices[u], rest.Vertices[v]);
                if (r0 > 1e-6) d.Add(Math.Abs((Dist(posed.Vertices[u], posed.Vertices[v]) / r0) - 1));
            }
        d.Sort();
        return (d.Average(), d[(int)(0.95 * (d.Count - 1))]);
    }

    /// <summary>A retargeted pose with every angle and the hip move scaled by <paramref name="s"/>.</summary>
    static Dictionary<string, object?> Scaled(Dictionary<string, object?> pose, double s) =>
        pose.ToDictionary(kv => kv.Key, kv => kv.Value switch
        {
            Dictionary<string, object?> inner => (object?)Scaled(inner, s),
            float f => (object?)(float)(f * s),
            double d => (object?)(d * s),
            _ => kv.Value
        }, StringComparer.Ordinal);

    /// <summary>Meshes side by side as clay at one yaw, labelled.</summary>
    static SKBitmap Row((FaceMesh Mesh, string Label)[] meshes, float yaw, string title)
    {
        const int W = 440, H = 620;
        var canvas = new SkiaCanvas(W * meshes.Length, H);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#f4f1ea";
        ctx.FillRect(0, 0, W * meshes.Length, H);
        var mt = new MeshToolkit();
        ctx.Font = "16px sans-serif";
        for (var i = 0; i < meshes.Length; i++)
        {
            mt.Draw(ctx, meshes[i].Mesh, new Dictionary<string, object?> { ["x"] = (W * i) + (W / 2), ["y"] = H / 2 + 10, ["scale"] = H * 0.78, ["yawDeg"] = yaw, ["clay"] = true });
            ctx.FillStyle = "#333333";
            ctx.FillText(i == 0 ? $"{title} — {meshes[i].Label}" : meshes[i].Label, (W * i) + 12, 22);
        }
        return canvas.Bitmap.Bitmap.Copy();
    }

    /// <summary>Edge stretch between rest and posed: mean and 95th percentile of |posed/rest - 1|, and the share torn (over 1.5x or under 2/3).</summary>
    static (double Mean, double P95, double Torn) Stretch(FaceMesh rest, FaceMesh posed)
    {
        var idx = rest.Indices;
        var edges = new HashSet<long>();
        for (var i = 0; i < idx.Length; i += 3)
            foreach (var (u, v) in new[] { (idx[i], idx[i + 1]), (idx[i + 1], idx[i + 2]), (idx[i + 2], idx[i]) })
                edges.Add(u < v ? ((long)u << 32) | v : ((long)v << 32) | u);
        var d = new List<double>(edges.Count);
        var torn = 0;
        foreach (var e in edges)
        {
            int u = (int)(e >> 32), v = (int)(e & 0xffffffff);
            var r0 = Dist(rest.Vertices[u], rest.Vertices[v]);
            if (r0 < 1e-6) continue;
            var r = Dist(posed.Vertices[u], posed.Vertices[v]) / r0;
            d.Add(Math.Abs(r - 1));
            if (r > 1.5 || r < 2.0 / 3) torn++;
        }
        d.Sort();
        return (d.Average(), d[(int)(0.95 * (d.Count - 1))], torn / (double)d.Count);
    }

    static double Dist(SKPoint3 a, SKPoint3 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    /// <summary>One clip: the template rig on the left, UniRig's on the right, as clay at a three-quarter turn.</summary>
    static SKBitmap Pair(FaceMesh ours, FaceMesh theirs, string clip)
    {
        const int W = 440, H = 620;
        var canvas = new SkiaCanvas(W * 2, H);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#f4f1ea";
        ctx.FillRect(0, 0, W * 2, H);
        var mt = new MeshToolkit();
        foreach (var (mesh, x) in new[] { (ours, W / 2), (theirs, W + (W / 2)) })
            mt.Draw(ctx, mesh, new Dictionary<string, object?> { ["x"] = x, ["y"] = H / 2 + 10, ["scale"] = H * 0.78, ["yawDeg"] = 30, ["clay"] = true });
        ctx.FillStyle = "#333333";
        ctx.Font = "16px sans-serif";
        ctx.FillText($"{clip} — template + ported weights", 12, 22);
        ctx.FillText("UniRig", W + 12, 22);
        return canvas.Bitmap.Bitmap.Copy();
    }

    static void Save(string path, List<SKBitmap> tiles)
    {
        using var sheet = new SKBitmap(tiles[0].Width, tiles.Sum(t => t.Height));
        using (var c = new SKCanvas(sheet))
        {
            var y = 0;
            foreach (var t in tiles) { c.DrawBitmap(t, 0, y); y += t.Height; }
        }
        using var data = sheet.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    /// <summary>How far a joint sits from the middle of the body's cross-section through it, in half-widths.</summary>
    /// <remarks>Measured along the depth axis (z), in a slice around the joint's height and across; -1 where no surface is near.</remarks>
    static float Centring(Vector3[] verts, Vector3 j, float height)
    {
        var r = 0.025f * height;
        var near = verts.Where(p => ((p.X - j.X) * (p.X - j.X)) + ((p.Y - j.Y) * (p.Y - j.Y)) < r * r).ToList();
        if (near.Count == 0) return -1f;
        float zMin = near.Min(p => p.Z), zMax = near.Max(p => p.Z);
        var half = Math.Max(1e-4f, (zMax - zMin) / 2f);
        return MathF.Abs(j.Z - ((zMin + zMax) / 2f)) / half;
    }

    /// <summary>The body as a grey point cloud, the fitted template in red, UniRig's skeleton in blue.</summary>
    static void Draw(string path, Vector3[] verts, SkeletonFit.Result fit, FaceMesh rigged, Dictionary<string, SKPoint3> uni, bool side)
    {
        const int H = 1200;
        var s = 0.9f * H / fit.Height;
        float Across(Vector3 p) => side ? p.Z * fit.FrontSign : p.X * fit.FrontSign;
        var xs = verts.Select(Across).ToList();
        var cx = (xs.Min() + xs.Max()) / 2f;
        var W = (int)((xs.Max() - xs.Min()) * s) + 200;
        var cy = (verts.Min(p => p.Y) + verts.Max(p => p.Y)) / 2f;
        SKPoint Px(Vector3 p) => new((W / 2f) + ((Across(p) - cx) * s), (H / 2f) - ((p.Y - cy) * s));

        using var bmp = new SKBitmap(W, H);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        using var dot = new SKPaint { Color = new SKColor(190, 190, 190), IsAntialias = true };
        foreach (var p in verts) canvas.DrawCircle(Px(p), 1.2f, dot);

        using var blue = new SKPaint { Color = new SKColor(40, 90, 220), StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var blueDot = new SKPaint { Color = new SKColor(40, 90, 220), IsAntialias = true };
        var bind = rigged.Rig!.JointBind;
        foreach (var (child, parent) in rigged.Rig.JointParent)
            if (bind.TryGetValue(child, out var a) && bind.TryGetValue(parent, out var b))
                canvas.DrawLine(Px(new(a.X, a.Y, a.Z)), Px(new(b.X, b.Y, b.Z)), blue);
        foreach (var p in uni.Values) canvas.DrawCircle(Px(new(p.X, p.Y, p.Z)), 6, blueDot);

        using var red = new SKPaint { Color = new SKColor(220, 50, 40), StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var redDot = new SKPaint { Color = new SKColor(220, 50, 40), IsAntialias = true };
        foreach (var (child, parent) in fit.Parent)
            if (fit.Joints.TryGetValue(child, out var a) && fit.Joints.TryGetValue(parent, out var b))
                canvas.DrawLine(Px(a), Px(b), red);
        foreach (var bone in CharacterStock.Parts.Values) canvas.DrawCircle(Px(fit.Joints[bone]), 5, redDot);

        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
