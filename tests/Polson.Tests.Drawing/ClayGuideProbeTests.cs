namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Probe: pose guides for the image model, drawn as untextured clay on a keyable ground.
/// </summary>
/// <remarks>
/// <para>
/// A textured render hands the model the character's colours and shading, so it touches the render up
/// instead of drawing the character from its sheet. A grey clay figure keeps only the pose, camera and
/// framing, so everything about how the character looks has to come from the sheet. Flat magenta is the
/// ground the cutout route already keys.
/// </para>
/// <para>
/// Uses <c>Mesh.draw</c>'s own projection and depth order, and shades each triangle by the way it faces.
/// Runs only with <c>POLSON_GUIDE_OUT</c> set, where lastlight3's Tomas and the pose library are on disk.
/// </para>
/// </remarks>
[Collection(TomasRig.Name)]
public class ClayGuideProbeTests : TestsRuntime
{
    const string Project = @"C:\Projects\PolsonRuns\lastlight3";
    const int W = 768, H = 1024;

    readonly ITestOutputHelper output;

    public ClayGuideProbeTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void RenderClayGuides()
    {
        var dir = Environment.GetEnvironmentVariable("POLSON_GUIDE_OUT");
        if (dir is null || !File.Exists(Path.Combine(Project, "characters", "tomas", "character.json"))
            || !PoseRetarget.Clips(null).Any(c => c.Name == "Idle_Rail_Call"))
        { output.WriteLine("NOT RUN: set POLSON_GUIDE_OUT"); return; }
        Directory.CreateDirectory(dir);

        var kit = new CharacterToolkit(Project);
        RenderGuides(kit, kit.Load("tomas"), dir);
    }

    /// <summary>
    /// The same guides on a STOCK body: Mesh2Motion's CC0 male (Quaternius), already on the skeleton the
    /// clips were recorded on, so no per-character build. <c>POLSON_STOCK_BODY</c> is its <c>.glb</c>.
    /// </summary>
    [Fact]
    public void RenderStockBodyGuides()
    {
        var dir = Environment.GetEnvironmentVariable("POLSON_GUIDE_OUT");
        var glb = Environment.GetEnvironmentVariable("POLSON_STOCK_BODY");
        if (dir is null || glb is null || !File.Exists(glb) || !PoseRetarget.Clips(null).Any(c => c.Name == "Idle_Rail_Call"))
        { output.WriteLine("NOT RUN: set POLSON_GUIDE_OUT and POLSON_STOCK_BODY"); return; }
        Directory.CreateDirectory(dir);

        var body = new MeshToolkit(Path.GetDirectoryName(glb)!).Load(Path.GetFileName(glb));
        foreach (var (part, bone) in ProportionTests.StockNames) body.Rig!.Aliases[part] = bone;

        // POLSON_PROPORTION reshapes it first: a JSON object of proportion factors.
        if (Environment.GetEnvironmentVariable("POLSON_PROPORTION") is { Length: > 0 } json)
        {
            var factors = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(json)!;
            body = body.Proportion(factors.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
            output.WriteLine("proportioned " + json);
        }

        // The standing front view, to measure the body the way a drawn character's is measured.
        Save(dir, "front", Clay(body, new Dictionary<string, object?> { ["x"] = W / 2f, ["y"] = H * 0.95f,
            ["scale"] = H * 0.85f / (body.Vertices.Max(v => v.Y) - body.Vertices.Min(v => v.Y)) }, null));
        RenderGuides(new CharacterToolkit(null), body, dir);
    }

    void RenderGuides(CharacterToolkit kit, FaceMesh tomas, string dir)
    {
        var frame = new Dictionary<string, object?> { ["x"] = 0, ["y"] = 0, ["width"] = W, ["height"] = H };
        Dictionary<string, object?> Place(float yaw) => kit.Place(tomas, frame, new Dictionary<string, object?>
            { ["at"] = new Dictionary<string, object?> { ["x"] = 0.5, ["y"] = 0.93 }, ["height"] = 0.8, ["yawDeg"] = yaw });
        Dictionary<string, object?> Clip(string name, double at) => kit.Retarget(tomas, name, new Dictionary<string, object?> { ["at"] = at });

        // Rail call: hand onto a rail just above where it hangs, head turned to look right.
        var railDraw = Place(35f);
        var rail = Clip("Idle_Rail_Call", 0.5);
        var hand = kit.Where(tomas, rail, "leftHand", railDraw);
        var railY = Convert.ToSingle(hand["y"]) - 20f;
        var reached = kit.Reach(tomas, rail, new Dictionary<string, object?>
        {
            ["leftHand"] = new Dictionary<string, object?> { ["page"] = new Dictionary<string, object?> { ["x"] = Convert.ToSingle(hand["x"]) + 25f, ["y"] = railY + 2f } },
            ["head"] = new Dictionary<string, object?> { ["lookAt"] = new Dictionary<string, object?> { ["x"] = W - 20f, ["y"] = 330f } }
        }, railDraw);
        output.WriteLine("rail call reached " + string.Join(", ", ((IDictionary<string, object?>)reached["reached"]!).Select(kv => $"{kv.Key} {kv.Value}")));

        Save(dir, "railcall", Clay(tomas.Pose(reached["pose"]!), railDraw, railY));
        Save(dir, "crouch", Clay(tomas.Pose(Clip("Crouch_Idle", 0.5)), Place(35f), null));
        Save(dir, "greeting", Clay(tomas.Pose(Clip("Greeting", 0.4)), Place(-60f), null));
    }

    static SKBitmap Clay(FaceMesh mesh, Dictionary<string, object?> draw, float? railY)
    {
        var bitmap = new SKBitmap(W, H);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0xFF, 0x00, 0xFF));
        if (railY is { } y)
            using (var wood = new SKPaint { Color = new SKColor(0x7A, 0x5A, 0x3A), IsAntialias = true })
                canvas.DrawRect(0, y, W, 18, wood);

        var pose = MeshToolkit.Pose.From(draw, mesh);
        var placed = Enumerable.Range(0, mesh.Vertices.Length).Select(i => pose.Place(mesh, i)).ToArray();
        var light = Normalize(new SKPoint3(-0.45f, 0.6f, 0.65f));
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 0.6f };
        using var path = new SKPath();

        foreach (var t in pose.DepthOrder(mesh))
        {
            SKPoint3 a = placed[mesh.Indices[t * 3]], b = placed[mesh.Indices[(t * 3) + 1]], c = placed[mesh.Indices[(t * 3) + 2]];
            var n = Normalize(Cross(Sub(b, a), Sub(c, a)));
            if (n.Z < 0) n = new SKPoint3(-n.X, -n.Y, -n.Z);      // painter's order hides the back; shade the face we see
            var lum = 0.35f + (0.6f * MathF.Max(0f, (n.X * light.X) + (n.Y * light.Y) + (n.Z * light.Z)));
            var g = (byte)Math.Clamp((int)(lum * 235f), 0, 255);
            paint.Color = new SKColor(g, g, g);

            path.Reset();
            path.MoveTo(pose.X + (a.X * pose.Scale), pose.Y - (a.Y * pose.Scale));
            path.LineTo(pose.X + (b.X * pose.Scale), pose.Y - (b.Y * pose.Scale));
            path.LineTo(pose.X + (c.X * pose.Scale), pose.Y - (c.Y * pose.Scale));
            path.Close();
            canvas.DrawPath(path, paint);
        }
        return bitmap;
    }

    void Save(string dir, string name, SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, $"guide-{name}.png"), data.ToArray());
        output.WriteLine($"wrote guide-{name}.png");
    }

    static SKPoint3 Sub(SKPoint3 a, SKPoint3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    static SKPoint3 Cross(SKPoint3 a, SKPoint3 b) => new((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));

    static SKPoint3 Normalize(SKPoint3 v)
    {
        var l = MathF.Sqrt((v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z));
        return l < 1e-12f ? v : new SKPoint3(v.X / l, v.Y / l, v.Z / l);
    }
}
