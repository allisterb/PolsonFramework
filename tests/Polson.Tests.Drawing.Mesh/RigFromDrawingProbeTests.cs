namespace Polson.Tests.Drawing.Mesh;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Polson.Animation;
using Polson.Drawing.Mesh;
using Polson.Drawing.Skia;

using SkiaSharp;

using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Probe (spike): rig a single drawn cell for cut-out animation, from nothing but its pixels.
/// </summary>
/// <remarks>
/// <para>
/// The body detector's 2D landmarks become a bone hierarchy (torso and head; per side upper arm, forearm,
/// hand, thigh, shin, foot). Every opaque pixel goes to the bone it is nearest, measured against that bone's
/// own half-width so a wide coat stays with the torso rather than the arms. Each child piece also takes a
/// round cap of its parent's pixels at the joint it turns about, so a bend does not open a notch. The pieces
/// are bound to their bones in a <c>Motion.composition</c> and posed.
/// </para>
/// <para>
/// Measured per cell: the detector's confidence, how exactly the rest pose reproduces the cell, and how much
/// enclosed transparency (holes) the pose opens. Writes the landmarks and bones, the partition and a contact
/// sheet. Runs only with <c>POLSON_RIGSPIKE_OUT</c> set and the body detector installed;
/// <c>POLSON_RIGSPIKE_CELLS</c> takes a <c>;</c>-separated list of cells, defaulting to lastlight3's front views.
/// </para>
/// </remarks>
public class RigFromDrawingProbeTests(ITestOutputHelper output) : TestsRuntime
{
    const int Margin = 160;

    static readonly string[] Cells = (Environment.GetEnvironmentVariable("POLSON_RIGSPIKE_CELLS")
        ?? string.Join(';', new[] { "kit", "tomas", "warden" }.Select(n => $@"C:\Projects\PolsonRuns\lastlight3\characters\{n}\view-front.png")))
        .Split(';', StringSplitOptions.RemoveEmptyEntries);

    sealed record Bone(string Name, string? Parent, SKPoint From, SKPoint To)
    {
        public float Radius { get; set; }

        public float JointRadius { get; set; }
    }

    [Fact]
    public void RigACellFromItsLandmarks()
    {
        var dir = Environment.GetEnvironmentVariable("POLSON_RIGSPIKE_OUT");
        if (string.IsNullOrEmpty(dir) || !BodyDetector.Available)
        { output.WriteLine("NOT RUN: set POLSON_RIGSPIKE_OUT, with the body detector installed"); return; }
        Directory.CreateDirectory(dir);

        foreach (var path in Cells.Where(File.Exists))
        {
            var name = Path.GetFileNameWithoutExtension(path) + "-" + Path.GetFileName(Path.GetDirectoryName(path));
            using var cell = Keyed(SKBitmap.Decode(path).Copy(SKColorType.Rgba8888), out var keyed);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var body = BodyDetector.Detect(cell);
            var detectMs = sw.ElapsedMilliseconds;
            if (!body.Found) { output.WriteLine($"{name}: no body ({body.Reason})"); continue; }

            var bones = Bones(body);

            // An end bone (a hand, a foot, the head) runs on to where the drawing ends along it: a landmark sits
            // at the knuckles or the nose, and a deformation drops what no bone reaches.
            for (var k = 0; k < bones.Count; k++)
            {
                var b = bones[k];
                if (bones.Any(c => c.Parent == b.Name)) continue;
                var along = b.To - b.From;
                var len = along.Length;
                if (len < 1) continue;
                var step = Scale(along, 1 / len);
                var to = b.To;
                for (var d = 1; d < 400; d++)
                {
                    var p = b.To + Scale(step, d);
                    if (p.X < 0 || p.Y < 0 || p.X >= cell.Width || p.Y >= cell.Height || cell.GetPixel((int)p.X, (int)p.Y).Alpha < 128) break;
                    to = p;
                }

                bones[k] = b with { To = to };
            }
            output.WriteLine($"{name}: {cell.Width}x{cell.Height}, {keyed} background pixels keyed, detected in {detectMs} ms, {bones.Count} bones, unsure of: "
                + (body.Unsure.Length == 0 ? "nothing" : string.Join(", ", body.Unsure)));
            // The half-width is the shorter side from the bone, at three points along it: the longer side of a
            // sloping arm runs on into the coat. A proximal limb is held to 1.4× its distal one, so a thigh under a
            // coat cannot claim the coat. Caps are the limb's own half-width, not the circle that fits at the joint,
            // which inside a coat is the coat.
            foreach (var b in bones)
            {
                var half = new[] { 0.3f, 0.5f, 0.7f }.Select(f => HalfWidth(cell, b.From + Scale(b.To - b.From, f), b.To - b.From)).Order().ElementAt(1);
                b.JointRadius = Math.Max(3, half * 1.1f);
                b.Radius = Math.Max(4, half * 1.3f);
            }

            foreach (var b in bones.Where(b => b.Parent is not null))
            {
                var parent = bones.First(x => x.Name == b.Parent);
                if (parent.Name != "torso") parent.Radius = Math.Min(parent.Radius, 1.4f * b.Radius);
            }

            output.WriteLine("  half-widths: " + string.Join(", ", bones.Select(b => $"{b.Name} {b.Radius:0}/{b.JointRadius:0}")));
            sw.Restart();
            var owner = Partition(cell, bones);
            var pieces = Pieces(cell, bones, owner);
            output.WriteLine($"  partitioned in {sw.ElapsedMilliseconds} ms; pixels per bone: "
                + string.Join(", ", bones.Select((b, i) => $"{b.Name} {owner.Count(o => o == i)}")));

            var bare = Rig(cell, bones, pieces, out var motion, skeleton: false);   // captures into this motion
            var comp = Rig(cell, bones, pieces, out _, skeleton: true);
            using var rest = bare.Render(0);
            var (fidelity, maxDelta) = Fidelity(rest.SkBitmap, cell);
            output.WriteLine($"  rest pose reproduces the cell: {fidelity:P2} of pixels within 8 levels, worst {maxDelta}");

            var restHoles = Holes(rest.SkBitmap);
            foreach (var t in new[] { 0.5, 1.0, 1.5 })
            {
                using var posed = bare.Render(t);
                output.WriteLine($"  t={t}: holes {Holes(posed.SkBitmap)} px (rest {restHoles})");
            }

            File.WriteAllBytes(Path.Combine(dir, $"{name}-bones.png"), Encode(Overlay(cell, body, bones)));
            File.WriteAllBytes(Path.Combine(dir, $"{name}-pieces.png"), Encode(PartitionMap(cell, owner, bones.Count)));
            bare.Capture(new Hashtable { ["fps"] = 2d });
            motion.Sheet(Path.Combine(dir, $"{name}-sheet.png"), new Hashtable { ["count"] = 5, ["cols"] = 5, ["scale"] = 0.45, ["fps"] = 2d });
            motion.Clear();
            using (var rigged = comp.Render(1)) File.WriteAllBytes(Path.Combine(dir, $"{name}-rig.png"), Encode(rigged.SkBitmap));
            bare.SaveSvg(Path.Combine(dir, $"{name}.svg"));

            // The same bones bending the whole drawing as one mesh, instead of moving cut pieces.
            var deform = Deform(cell, bones, owner, out var deformMotion, out var reach);
            File.WriteAllBytes(Path.Combine(dir, $"{name}-unreached.png"), Encode(Unreached!));
            using (var deformRest = deform.Render(0))
            {
                var (share, worst) = Fidelity(deformRest.SkBitmap, cell);
                output.WriteLine($"  deform: the bones reach {reach:P1} of the drawing; rest pose within 8 levels on {share:P2}, worst {worst}");
            }

            foreach (var t in new[] { 0.5, 1.0, 1.5 })
            {
                using var posed = deform.Render(t);
                output.WriteLine($"  deform t={t}: holes {Holes(posed.SkBitmap)} px");
            }

            deform.Capture(new Hashtable { ["fps"] = 2d });
            deformMotion.Sheet(Path.Combine(dir, $"{name}-deform-sheet.png"), new Hashtable { ["count"] = 5, ["cols"] = 5, ["scale"] = 0.45, ["fps"] = 2d });
            deformMotion.Clear();
        }
    }

    #region Rig
    /// <summary>The bones, from the landmarks: root torso and free legs, so a lean at the waist leaves the feet planted.</summary>
    static List<Bone> Bones(BodyDetection body)
    {
        SKPoint P(string n) => body.Point(n) ?? throw new InvalidOperationException($"no {n}");
        var shoulderMid = Mid(P("leftShoulder"), P("rightShoulder"));
        var hipMid = Mid(P("leftHip"), P("rightHip"));
        var nose = P("nose");
        var crown = nose + Scale(nose - shoulderMid, 0.8f);

        var list = new List<Bone> { new("torso", null, hipMid, shoulderMid), new("head", "torso", shoulderMid, crown) };
        foreach (var s in new[] { "left", "right" })
        {
            var wrist = P(s + "Wrist");
            var elbow = P(s + "Elbow");
            var index = body.Visibility(s + "Index") > 0.3f ? P(s + "Index") : wrist + Scale(wrist - elbow, 0.3f);
            list.Add(new(s + "UpperArm", "torso", P(s + "Shoulder"), elbow));
            list.Add(new(s + "Forearm", s + "UpperArm", elbow, wrist));
            list.Add(new(s + "Hand", s + "Forearm", wrist, index));
            list.Add(new(s + "Thigh", null, P(s + "Hip"), P(s + "Knee")));
            list.Add(new(s + "Shin", s + "Thigh", P(s + "Knee"), P(s + "Ankle")));
            list.Add(new(s + "Foot", s + "Shin", P(s + "Ankle"), P(s + "FootIndex")));
        }

        return list;
    }

    /// <summary>Each opaque pixel to the bone it is nearest, distance measured in that bone's half-widths.</summary>
    static int[] Partition(SKBitmap cell, List<Bone> bones)
    {
        var owner = new int[cell.Width * cell.Height];
        for (var y = 0; y < cell.Height; y++)
        {
            for (var x = 0; x < cell.Width; x++)
            {
                var i = y * cell.Width + x;
                if (cell.GetPixel(x, y).Alpha < 8) { owner[i] = -1; continue; }
                var best = double.MaxValue;
                for (var b = 0; b < bones.Count; b++)
                {
                    // Distance to the bone's capsule surface: negative inside. A ratio would let a fat torso win far-off fingertips.
                    var d = SegmentDistance(new SKPoint(x + 0.5f, y + 0.5f), bones[b].From, bones[b].To) - bones[b].Radius;
                    if (d < best) (best, owner[i]) = (d, b);
                }
            }
        }

        return owner;
    }

    /// <summary>
    /// A bitmap per bone: its own pixels, plus caps both ways at each joint — a disc of the parent at the joint
    /// it turns about, and a disc of each child at theirs — so the two overlap like cut paper and a bend shows
    /// drawing rather than a notch.
    /// </summary>
    static SKBitmap[] Pieces(SKBitmap cell, List<Bone> bones, int[] owner) => [.. bones.Select((bone, b) =>
    {
        var piece = new SKBitmap(cell.Info);
        piece.Erase(SKColors.Transparent);
        var parent = bone.Parent is null ? -1 : bones.FindIndex(x => x.Name == bone.Parent);
        var children = bones.Select((c, i) => (c, i)).Where(x => x.c.Parent == bone.Name).ToArray();
        for (var y = 0; y < cell.Height; y++)
        {
            for (var x = 0; x < cell.Width; x++)
            {
                var o = owner[y * cell.Width + x];
                var p = new SKPoint(x + 0.5f, y + 0.5f);
                var cap = (parent >= 0 && o == parent && SKPoint.Distance(p, bone.From) <= bone.JointRadius)
                    || children.Any(c => o == c.i && SKPoint.Distance(p, c.c.From) <= c.c.JointRadius);
                if (o == b || cap) piece.SetPixel(x, y, cell.GetPixel(x, y));
            }
        }

        return piece;
    })];

    /// <summary>The composition: bones from the joints, each piece in a group bound to its bone, a pose keyed on the turns.</summary>
    static MotionComposition Rig(SKBitmap cell, List<Bone> bones, SKBitmap[] pieces, out MotionToolkit motion, bool skeleton)
    {
        motion = new MotionToolkit();
        var n = motion.Nodes;
        var comp = motion.Composition(new Hashtable
        {
            ["width"] = (double)(cell.Width + 2 * Margin), ["height"] = (double)(cell.Height + Margin), ["fps"] = 12d, ["duration"] = 2d
        });
        comp.Fill(new Hashtable { ["color"] = "#e8e4dc" });

        // A wave on the figure's right, the left arm dropped, a head tilt, a lean, a step.
        var poses = new Dictionary<string, double>
        {
            ["rightUpperArm"] = 55, ["rightForearm"] = 70, ["leftUpperArm"] = 55, ["leftForearm"] = 20,
            ["head"] = 12, ["torso"] = -8, ["leftThigh"] = -14, ["leftShin"] = 18, ["rightThigh"] = 6
        };

        var made = new Dictionary<string, MotionBone>();
        foreach (var b in bones)
        {
            var peak = poses.GetValueOrDefault(b.Name);
            var options = new Hashtable
            {
                ["name"] = b.Name,
                ["from"] = new[] { b.From.X + (double)Margin, b.From.Y + (double)Margin * 0.5 },
                ["to"] = new[] { b.To.X + (double)Margin, b.To.Y + (double)Margin * 0.5 },
            };
            if (b.Parent is not null) options["parent"] = made[b.Parent];
            if (peak != 0)
            {
                options["turn"] = n.Animated("angle", new object[]
                {
                    Key(0, 0), Key(1, peak), Key(2, 0)
                });
            }

            made[b.Name] = comp.Bone(options);
        }

        // Front view: legs under the torso, head and arms over it, each limb parent before child.
        string[] order = ["leftThigh", "leftShin", "leftFoot", "rightThigh", "rightShin", "rightFoot", "torso", "head",
            "leftUpperArm", "leftForearm", "leftHand", "rightUpperArm", "rightForearm", "rightHand"];
        foreach (var name in order)
        {
            var i = bones.FindIndex(x => x.Name == name);
            var g = comp.Group(new Hashtable { ["bone"] = made[name], ["desc"] = name });
            g.Image(new Hashtable
            {
                ["image"] = new SkiaBitmapWrapper(pieces[i]),
                ["tl"] = new[] { (double)Margin, Margin * 0.5 },
                ["br"] = new[] { (double)(Margin + cell.Width), Margin * 0.5 + cell.Height }
            });
        }

        if (skeleton) comp.Skeleton(new Hashtable { ["color"] = "#c2553d55" });
        return comp;

        static Hashtable Key(double t, double v) => new() { ["time"] = t, ["value"] = v, ["ease"] = "halt" };
    }
    #endregion

    /// <summary>
    /// The drawing whole, in a group with a skeleton deformation: each bone's width its measured half-width
    /// (its reach), the same pose keyed on the same turns. Reports how much of the drawing the bones reach,
    /// since anything outside every capsule is dropped.
    /// </summary>
    static MotionComposition Deform(SKBitmap cell, List<Bone> bones, int[] owner, out MotionToolkit motion, out double reach)
    {
        motion = new MotionToolkit();
        var n = motion.Nodes;
        var comp = motion.Composition(new Hashtable
        {
            ["width"] = (double)(cell.Width + 2 * Margin), ["height"] = (double)(cell.Height + Margin), ["fps"] = 12d, ["duration"] = 2d
        });
        comp.Fill(new Hashtable { ["color"] = "#e8e4dc" });

        var poses = new Dictionary<string, double>
        {
            ["rightUpperArm"] = 55, ["rightForearm"] = 70, ["leftUpperArm"] = 55, ["leftForearm"] = 20,
            ["head"] = 12, ["torso"] = -8, ["leftThigh"] = -14, ["leftShin"] = 18, ["rightThigh"] = 6
        };

        // Each bone's reach covers the pixels the partition gave it: their 99th-percentile distance from the bone,
        // and at least its half-width. A landmark line runs along the top of a sleeve, so the half-width alone
        // misses the underside.
        var distances = bones.Select(_ => new List<double>()).ToArray();
        for (var y = 0; y < cell.Height; y++)
        {
            for (var x = 0; x < cell.Width; x++)
            {
                var o = owner[y * cell.Width + x];
                if (o >= 0) distances[o].Add(SegmentDistance(new SKPoint(x + 0.5f, y + 0.5f), bones[o].From, bones[o].To));
            }
        }

        var reaches = bones.Select((b, i) =>
        {
            var d = distances[i].Order().ToArray();
            var p99 = d.Length == 0 ? 0 : d[(int)(0.99 * (d.Length - 1))];
            return (b.Name, Reach: Math.Max(b.Radius * 1.15, p99 + 2));
        }).ToDictionary(x => x.Name, x => x.Reach);

        var made = new Dictionary<string, MotionBone>();
        foreach (var b in bones)
        {
            // The reach must cover the drawing across the bone: the measured half-width, a little more at the ends.
            var options = new Hashtable
            {
                ["name"] = b.Name,
                ["from"] = new[] { b.From.X + (double)Margin, b.From.Y + (double)Margin * 0.5 },
                ["to"] = new[] { b.To.X + (double)Margin, b.To.Y + (double)Margin * 0.5 },
                ["width"] = reaches[b.Name], ["tipwidth"] = reaches[b.Name],
            };
            if (b.Parent is not null) options["parent"] = made[b.Parent];
            if (poses.GetValueOrDefault(b.Name) is var peak && peak != 0)
                options["turn"] = n.Animated("angle", new object[] { Key(0, 0), Key(1, peak), Key(2, 0) });
            made[b.Name] = comp.Bone(options);
        }

        var figure = comp.Group(new Hashtable { ["desc"] = "figure" });
        figure.Image(new Hashtable
        {
            ["image"] = new SkiaBitmapWrapper(cell),
            ["tl"] = new[] { (double)Margin, Margin * 0.5 },
            ["br"] = new[] { (double)(Margin + cell.Width), Margin * 0.5 + cell.Height }
        });
        figure.SkeletonDeformation(new Hashtable { ["xSubdivisions"] = 48d, ["ySubdivisions"] = 48d });

        // How much of the drawing lies inside some bone's capsule, and so survives; the rest marked in Unreached.
        int inside = 0, total = 0;
        Unreached = cell.Copy();
        for (var y = 0; y < cell.Height; y++)
        {
            for (var x = 0; x < cell.Width; x++)
            {
                if (cell.GetPixel(x, y).Alpha < 128) continue;
                total++;
                var p = new SKPoint(x + 0.5f, y + 0.5f);
                if (bones.Any(b => SegmentDistance(p, b.From, b.To) <= reaches[b.Name])) inside++;
                else Unreached.SetPixel(x, y, new SKColor(230, 0, 160));
            }
        }

        reach = inside / (double)total;
        return comp;

        static Hashtable Key(double t, double v) => new() { ["time"] = t, ["value"] = v, ["ease"] = "halt" };
    }

    static SKBitmap? Unreached;

    #region Measures
    /// <summary>The rest render against the cell placed where the rig put it; the skeleton overlay is excluded by comparing only where the cell is opaque.</summary>
    static (double Share, int Worst) Fidelity(SKBitmap rest, SKBitmap cell)
    {
        int ok = 0, total = 0, worst = 0;
        for (var y = 0; y < cell.Height; y++)
        {
            for (var x = 0; x < cell.Width; x++)
            {
                var c = cell.GetPixel(x, y);
                if (c.Alpha < 250) continue;
                var r = rest.GetPixel(x + Margin, y + Margin / 2);
                var d = Math.Max(Math.Abs(c.Red - r.Red), Math.Max(Math.Abs(c.Green - r.Green), Math.Abs(c.Blue - r.Blue)));
                total++;
                if (d <= 8) ok++;
                worst = Math.Max(worst, d);
            }
        }

        return (ok / (double)total, worst);
    }

    /// <summary>Background pixels the figure encloses: what a gap at a joint looks like once it closes round.</summary>
    static int Holes(SKBitmap frame)
    {
        var bg = frame.GetPixel(2, 2);
        bool IsBg(int x, int y)
        {
            var c = frame.GetPixel(x, y);
            return Math.Abs(c.Red - bg.Red) < 6 && Math.Abs(c.Green - bg.Green) < 6 && Math.Abs(c.Blue - bg.Blue) < 6;
        }

        var w = frame.Width;
        var h = frame.Height;
        var seen = new bool[w * h];
        var stack = new Stack<(int, int)>();
        for (var x = 0; x < w; x++) { stack.Push((x, 0)); stack.Push((x, h - 1)); }
        for (var y = 0; y < h; y++) { stack.Push((0, y)); stack.Push((w - 1, y)); }
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || y < 0 || x >= w || y >= h || seen[y * w + x] || !IsBg(x, y)) continue;
            seen[y * w + x] = true;
            stack.Push((x + 1, y)); stack.Push((x - 1, y)); stack.Push((x, y + 1)); stack.Push((x, y - 1));
        }

        var holes = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (!seen[y * w + x] && IsBg(x, y)) holes++;
        return holes;
    }
    #endregion

    #region Geometry
    /// <summary>
    /// Makes an opaque flat ground transparent, flooding in from the borders over pixels near the corner colour,
    /// so a cell saved on white is treated like a keyed cutout. A cell already transparent is left alone.
    /// </summary>
    static SKBitmap Keyed(SKBitmap cell, out int keyed)
    {
        keyed = 0;
        var bg = cell.GetPixel(0, 0);
        if (bg.Alpha < 8) return cell;

        int w = cell.Width, h = cell.Height;
        var seen = new bool[w * h];
        var stack = new Stack<(int, int)>();
        for (var x = 0; x < w; x++) { stack.Push((x, 0)); stack.Push((x, h - 1)); }
        for (var y = 0; y < h; y++) { stack.Push((0, y)); stack.Push((w - 1, y)); }
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || y < 0 || x >= w || y >= h || seen[y * w + x]) continue;
            var c = cell.GetPixel(x, y);
            if (Math.Abs(c.Red - bg.Red) > 24 || Math.Abs(c.Green - bg.Green) > 24 || Math.Abs(c.Blue - bg.Blue) > 24) continue;
            seen[y * w + x] = true;
            cell.SetPixel(x, y, SKColors.Transparent);
            keyed++;
            stack.Push((x + 1, y)); stack.Push((x - 1, y)); stack.Push((x, y + 1)); stack.Push((x, y - 1));
        }

        // A soft edge: a pixel touching the keyed ground takes alpha by how far it is from the ground colour, so the
        // antialiasing halo does not travel with a moving piece as an opaque pale line.
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (seen[y * w + x]) continue;
                var edge = (x > 0 && seen[y * w + x - 1]) || (x < w - 1 && seen[y * w + x + 1])
                    || (y > 0 && seen[(y - 1) * w + x]) || (y < h - 1 && seen[(y + 1) * w + x]);
                if (!edge) continue;
                var c = cell.GetPixel(x, y);
                var diff = Math.Max(Math.Abs(c.Red - bg.Red), Math.Max(Math.Abs(c.Green - bg.Green), Math.Abs(c.Blue - bg.Blue)));
                cell.SetPixel(x, y, c.WithAlpha((byte)Math.Clamp(diff * 255 / 120, 0, 255)));
            }
        }

        return cell;
    }

    static SKPoint Mid(SKPoint a, SKPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    static SKPoint Scale(SKPoint p, float k) => new(p.X * k, p.Y * k);

    static double SegmentDistance(SKPoint p, SKPoint a, SKPoint b)
    {
        var ab = b - a;
        var len2 = ab.X * ab.X + ab.Y * ab.Y;
        var t = len2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2, 0, 1);
        return SKPoint.Distance(p, a + Scale(ab, (float)t));
    }

    /// <summary>The half-width across a bone at a point: the nearer of the silhouette's edges either side of it.</summary>
    static float HalfWidth(SKBitmap cell, SKPoint at, SKPoint along)
    {
        var len = Math.Max(1e-3f, along.Length);
        var normal = new SKPoint(-along.Y / len, along.X / len);
        float March(float sign)
        {
            for (var d = 1; d < 400; d++)
            {
                var p = at + Scale(normal, d * sign);
                if (p.X < 0 || p.Y < 0 || p.X >= cell.Width || p.Y >= cell.Height || cell.GetPixel((int)p.X, (int)p.Y).Alpha < 128) return d;
            }

            return 400;
        }

        return Math.Min(March(1), March(-1));
    }

    /// <summary>The largest circle about a point that stays inside the silhouette.</summary>
    static float Inscribed(SKBitmap cell, SKPoint at)
    {
        for (var r = 1; r < 200; r++)
        {
            for (var k = 0; k < 32; k++)
            {
                var a = k * Math.PI / 16;
                var x = (int)(at.X + r * Math.Cos(a));
                var y = (int)(at.Y + r * Math.Sin(a));
                if (x < 0 || y < 0 || x >= cell.Width || y >= cell.Height || cell.GetPixel(x, y).Alpha < 128) return r;
            }
        }

        return 200;
    }
    #endregion

    #region Pictures
    static SKBitmap Overlay(SKBitmap cell, BodyDetection body, List<Bone> bones)
    {
        var bmp = cell.Copy();
        using var c = new SKCanvas(bmp);
        using var bonePaint = new SKPaint { Color = new SKColor(194, 85, 61), StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke };
        foreach (var b in bones)
        {
            c.DrawLine(b.From, b.To, bonePaint);
            c.DrawCircle(b.From, b.JointRadius, bonePaint);
        }

        using var dot = new SKPaint { IsAntialias = true };
        foreach (var n in body.Names)
        {
            dot.Color = body.Visibility(n) >= 0.5f ? new SKColor(31, 111, 139) : new SKColor(230, 0, 160);
            if (body.Point(n) is { } p) c.DrawCircle(p, 4, dot);
        }

        return bmp;
    }

    static SKBitmap PartitionMap(SKBitmap cell, int[] owner, int count)
    {
        var bmp = new SKBitmap(cell.Info);
        for (var y = 0; y < cell.Height; y++)
        {
            for (var x = 0; x < cell.Width; x++)
            {
                var o = owner[y * cell.Width + x];
                var src = cell.GetPixel(x, y);
                if (o < 0) { bmp.SetPixel(x, y, SKColors.White); continue; }
                var hue = SKColor.FromHsl(o * 360f / count, 70, 55);
                byte Mix(byte a, byte b) => (byte)((a + b) / 2);
                bmp.SetPixel(x, y, new SKColor(Mix(hue.Red, src.Red), Mix(hue.Green, src.Green), Mix(hue.Blue, src.Blue)));
            }
        }

        return bmp;
    }

    static byte[] Encode(SKBitmap b) => b.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    #endregion
}
