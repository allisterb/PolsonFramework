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

    static readonly string TemplatePath = SolverRig.Template ?? "";

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
            var diffuse = int.TryParse(Environment.GetEnvironmentVariable("POLSON_RIGFIT_DIFFUSE"), out var dd) ? dd : 12;
            var glb = SkeletonFit.Rig(Path.Combine(folder, "mesh.glb"), fit, template, armPlane: true, report, diffuse);
            File.WriteAllBytes(Path.Combine(dir, $"{name}-fit.glb"), glb);
            output.WriteLine($"\n{name}: rigged in {sw.ElapsedMilliseconds} ms ({glb.Length / 1e6:0.0} MB); {string.Join("; ", report)}");
            fit.Render?.Dispose();

            var ours = new MeshToolkit(dir).Load($"{name}-fit.glb");
            Assert.True(ours.Posable);
            foreach (var (part, bone) in CharacterStock.Parts) ours.Rig!.Aliases[part] = bone;

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
            Save(Path.Combine(dir, $"{name}-poses-d{diffuse}.png"), tiles);
            foreach (var t in tiles) t.Dispose();
        }
        output.WriteLine($"\nposes and rigs in {dir}");
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
