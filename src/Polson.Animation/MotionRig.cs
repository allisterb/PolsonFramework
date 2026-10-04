namespace Polson.Animation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Polson.Drawing.Skia;

using SkiaSharp;

/// <summary>A drawing rigged to bend: what <c>composition.rigFromDrawing(...)</c> returns.</summary>
/// <remarks>
/// Its bones carry the body-part names <c>Character.jointMap</c> uses, so the same names pose a drawn figure and
/// a 3D one. Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase
/// spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>. The camelCase form is
/// the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public sealed class MotionRig
{
    #region Constructors
    internal MotionRig(Dictionary<string, MotionBone> bones, MotionGroup group, double reach, string[] unsure, int keyed)
    {
        this.bones = bones;
        Group = group;
        Reach = reach;
        Unsure = unsure;
        Keyed = keyed;
    }
    #endregion

    #region Fields
    private readonly Dictionary<string, MotionBone> bones;
    #endregion

    #region Properties
    /// <summary>The bones by body part: <c>rig.bones.leftForearm</c>.</summary>
    public Dictionary<string, object?> Bones => bones.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

    /// <summary>The body-part names, root first.</summary>
    public string[] Names => [.. bones.Keys];

    /// <summary>The group holding the drawing and the deformation that bends it.</summary>
    public MotionGroup Group { get; }

    /// <summary>The share of the drawing some bone reaches, 0 to 1. What no bone reaches is dropped, so expect 1.</summary>
    public double Reach { get; }

    /// <summary>The landmarks the detector was unsure of; the bones they place are guesses.</summary>
    public string[] Unsure { get; }

    /// <summary>How many background pixels were keyed out because the drawing came on an opaque ground. 0 for a cutout.</summary>
    public int Keyed { get; }
    #endregion

    #region Methods
    /// <summary>One bone by body part, or a refusal naming the ones there are.</summary>
    public MotionBone Bone(string name) => bones.TryGetValue(name, out var b)
        ? b
        : throw new ArgumentException($"The rig has no bone '{name}'. It has: {string.Join(", ", bones.Keys)}.");
    #endregion
}

/// <summary>
/// Builds a <see cref="MotionRig"/> from one drawing and its landmarks: the spike in
/// <c>docs/internal/rig-from-drawing-spike.md</c>, made a call.
/// </summary>
/// <remarks>
/// The landmarks become bones; every pixel of the drawing goes to the bone whose capsule surface is nearest
/// (distance less half-width, so a wide coat stays with the spine); each bone's reach is set to cover the pixels
/// it was given; end bones run on to the drawing's edge. The drawing and a skeleton deformation over it go in one
/// group, so the whole picture bends rather than tearing at the joints.
/// </remarks>
internal static class MotionRigBuilder
{
    #region Fields
    /// <summary>Bone, parent, from-landmark, to-landmark. <c>@mid</c> names are midpoints; <c>@crown</c> is above the nose.</summary>
    private static readonly (string Name, string? Parent, string From, string To)[] Layout =
    [
        ("spine", "hips", "@hipMid", "@shoulderMid"),
        ("head", "spine", "@shoulderMid", "@crown"),
        ("leftUpperArm", "spine", "leftShoulder", "leftElbow"),
        ("leftForearm", "leftUpperArm", "leftElbow", "leftWrist"),
        ("leftHand", "leftForearm", "leftWrist", "@leftHandTip"),
        ("rightUpperArm", "spine", "rightShoulder", "rightElbow"),
        ("rightForearm", "rightUpperArm", "rightElbow", "rightWrist"),
        ("rightHand", "rightForearm", "rightWrist", "@rightHandTip"),
        ("leftThigh", "hips", "leftHip", "leftKnee"),
        ("leftShin", "leftThigh", "leftKnee", "leftAnkle"),
        ("leftFoot", "leftShin", "leftAnkle", "leftFootIndex"),
        ("rightThigh", "hips", "rightHip", "rightKnee"),
        ("rightShin", "rightThigh", "rightKnee", "rightAnkle"),
        ("rightFoot", "rightShin", "rightAnkle", "rightFootIndex"),
    ];

    private static readonly string[] Required =
    [
        "nose", "leftShoulder", "rightShoulder", "leftElbow", "rightElbow", "leftWrist", "rightWrist",
        "leftHip", "rightHip", "leftKnee", "rightKnee", "leftAnkle", "rightAnkle", "leftFootIndex", "rightFootIndex"
    ];
    #endregion

    #region Methods
    public static MotionRig Build(MotionLayerList into, MotionComposition root, object? image, object? landmarks, object? options)
    {
        var o = new MotionOptions(options, "rigFromDrawing", "tl", "br", "poses", "turns", "move", "prefix", "subdivisions", "desc");
        var picture = MotionPicture.From(image, "rigFromDrawing's image");
        using var cell = Unpremultiplied(picture.Image);
        var keyed = KeyGround(cell);
        int w = cell.Width, h = cell.Height;

        var points = Landmarks(landmarks, w, h, out var unsure);
        var joints = Joints(points);
        var bones = Layout.Select(l => new Proto(l.Name, l.Parent, joints[l.From], joints[l.To])).ToList();
        var alpha = Alpha(cell);
        foreach (var b in bones.Where(b => b.Name.EndsWith("Hand") || b.Name.EndsWith("Foot") || b.Name == "head"))
            b.To = RunOn(alpha, w, h, b.From, b.To);
        foreach (var b in bones)
        {
            var half = new[] { 0.3, 0.5, 0.7 }.Select(f => HalfWidth(alpha, w, h, Lerp(b.From, b.To, f), b.To - b.From)).Order().ElementAt(1);
            b.Radius = Math.Max(4, half * 1.3);
        }

        foreach (var b in bones.Where(b => b.Parent is not null and not "hips" and not "spine"))
        {
            var parent = bones.First(x => x.Name == b.Parent);
            parent.Radius = Math.Min(parent.Radius, 1.4 * b.Radius);
        }

        var reach = Reaches(alpha, w, h, bones);

        // Image pixels into the composition, where the drawing is placed.
        var tl = o.Has("tl") ? MotionTypes.Read(o.Raw("tl"), MotionType.Vector, "rigFromDrawing's tl") : [0, 0];
        var br = o.Has("br") ? MotionTypes.Read(o.Raw("br"), MotionType.Vector, "rigFromDrawing's br") : [tl[0] + w, tl[1] + h];
        var (sx, sy) = ((br[0] - tl[0]) / w, (br[1] - tl[1]) / h);
        var scale = (Math.Abs(sx) + Math.Abs(sy)) / 2;
        double[] Place(Vec p) => [tl[0] + p.X * sx, tl[1] + p.Y * sy];

        var prefix = o.Has("prefix") ? o.Raw("prefix")?.ToString() ?? "" : "";
        var names = Layout.Select(l => l.Name).Prepend("hips").ToArray();
        foreach (var n in names.Where(n => root.Bones.Any(b => b.Name == prefix + n)))
            throw new ArgumentException($"rigFromDrawing: the composition already has a bone '{prefix + n}'. Give this rig a prefix: {{ prefix: 'kit.' }}.");

        var turns = Turns(o, names);
        var made = new Dictionary<string, MotionBone>();

        // The root: at the hips, pointing down, not deformed by, only carrying everything. `move` travels it.
        var hipMid = joints["@hipMid"];
        var hipSpan = Math.Max(4, (joints["leftHip"] - joints["rightHip"]).Length);
        var origin = new MotionConstant(MotionType.Vector, Place(hipMid));
        made["hips"] = root.Bone(new Hashtable
        {
            ["name"] = prefix + "hips",
            ["origin"] = o.Has("move") ? new MotionAdd(MotionType.Vector, origin,
                MotionNodeFactory.Node(o.Raw("move"), MotionType.Vector, "rigFromDrawing's move"), new MotionConstant(MotionType.Real, [1])) : origin,
            ["angle"] = turns.TryGetValue("hips", out var hipsTurn)
                ? new MotionAdd(MotionType.Angle, new MotionConstant(MotionType.Angle, [90]), hipsTurn, new MotionConstant(MotionType.Real, [1]))
                : (object)90d,
            ["length"] = hipSpan * scale,
            ["width"] = 0d, ["tipwidth"] = 0d,
        });

        foreach (var b in bones)
        {
            var options2 = new Hashtable
            {
                ["name"] = prefix + b.Name,
                ["parent"] = made[b.Parent!],
                ["from"] = Place(b.From),
                ["to"] = Place(b.To),
                ["width"] = reach[b.Name] * scale,
                ["tipwidth"] = reach[b.Name] * scale,
            };
            if (turns.TryGetValue(b.Name, out var turn)) options2["turn"] = turn;
            made[b.Name] = root.Bone(options2);
        }

        var group = into.Group(new Hashtable { ["desc"] = o.Has("desc") ? o.Raw("desc") : "rigged drawing" });
        group.Image(new Hashtable { ["image"] = new SkiaBitmapWrapper(Premultiplied(cell)), ["tl"] = tl, ["br"] = br });
        var grid = o.Has("subdivisions") ? o.Number("subdivisions", 48) : 48;
        group.SkeletonDeformation(new Hashtable
        {
            ["bones"] = bones.Select(b => (object)made[b.Name]).ToList(),
            ["xSubdivisions"] = grid, ["ySubdivisions"] = grid,
        });

        return new MotionRig(made, group, reach["@covered"], unsure, keyed);
    }

    /// <summary>Per-bone turn nodes from <c>poses</c> (whole-pose keys) or <c>turns</c> (a node per bone), never both for one bone.</summary>
    private static Dictionary<string, MotionNode> Turns(MotionOptions o, string[] names)
    {
        var turns = new Dictionary<string, MotionNode>();
        if (o.Has("turns"))
        {
            var d = JsInterop.AsDict(o.Raw("turns")) ?? throw new ArgumentException("rigFromDrawing's turns is { boneName: angle or node }.");
            foreach (var key in d.Keys.Cast<object>().Select(k => k.ToString()!))
            {
                if (!names.Contains(key)) throw new ArgumentException($"rigFromDrawing's turns names no bone '{key}'. Bones: {string.Join(", ", names)}.");
                turns[key] = MotionNodeFactory.Node(d[key], MotionType.Angle, $"rigFromDrawing's turn for {key}");
            }
        }

        if (!o.Has("poses")) return turns;
        if (o.Raw("poses") is not IList poses || poses.Count == 0)
            throw new ArgumentException("rigFromDrawing's poses is a list of { time, ease?, boneName: degrees, ... }.");

        var keys = poses.Cast<object?>().Select((p, i) =>
        {
            var d = JsInterop.AsDict(p) ?? throw new ArgumentException($"rigFromDrawing's pose {i} is an object: {{ time, boneName: degrees }}.");
            if (!d.Contains("time")) throw new ArgumentException($"rigFromDrawing's pose {i} has no time.");
            foreach (var key in d.Keys.Cast<object>().Select(k => k.ToString()!).Where(k => k is not ("time" or "ease")))
                if (!names.Contains(key)) throw new ArgumentException($"rigFromDrawing's pose {i} names no bone '{key}'. Bones: {string.Join(", ", names)}.");
            return d;
        }).ToArray();

        var posed = keys.SelectMany(d => d.Keys.Cast<object>().Select(k => k.ToString()!)).Where(k => k is not ("time" or "ease")).Distinct();
        foreach (var bone in posed)
        {
            if (turns.ContainsKey(bone)) throw new ArgumentException($"rigFromDrawing: bone '{bone}' is given both in turns and in poses; give it once.");
            var waypoints = keys.Select(d => (object)new Hashtable
            {
                ["time"] = d["time"],
                ["value"] = d.Contains(bone) ? d[bone] : 0d,   // a bone a pose leaves out is at rest in it
                ["ease"] = d["ease"] ?? "clamped",
            }).ToArray();
            turns[bone] = new MotionNodeFactory().Animated("angle", waypoints);
        }

        return turns;
    }

    /// <summary>The landmarks in image pixels: a detection, or a plain object of name → point.</summary>
    private static Dictionary<string, Vec> Landmarks(object? value, int w, int h, out string[] unsure)
    {
        var points = new Dictionary<string, Vec>();
        var doubtful = new List<string>();
        switch (value)
        {
            case ILandmarkSource source:
                if (source.SourceWidth != w || source.SourceHeight != h)
                    throw new ArgumentException($"rigFromDrawing: the landmarks were found in a {source.SourceWidth}×{source.SourceHeight} image and the drawing is {w}×{h}; detect on the drawing itself.");
                foreach (var name in Required.Concat(["leftIndex", "rightIndex"]))
                {
                    if (!source.TryGetLandmark(name, out var x, out var y, out var v)) continue;
                    points[name] = new(x, y);
                    if (v < 0.5 && Required.Contains(name)) doubtful.Add(name);
                }

                break;
            case null:
                throw new ArgumentException("rigFromDrawing(image, landmarks) needs the landmarks: Character.detect(image), or { leftShoulder: [x, y], ... }.");
            default:
                var d = JsInterop.AsDict(value) ?? throw new ArgumentException("rigFromDrawing's landmarks are Character.detect(image) or { name: [x, y] }.");
                foreach (var key in d.Keys.Cast<object>().Select(k => k.ToString()!))
                {
                    var p = MotionTypes.Read(d[key], MotionType.Vector, $"rigFromDrawing's landmark '{key}'");
                    points[key] = new(p[0], p[1]);
                }

                break;
        }

        var missing = Required.Where(n => !points.ContainsKey(n)).ToArray();
        if (missing.Length > 0) throw new ArgumentException($"rigFromDrawing: no {string.Join(", ", missing)}. It needs: {string.Join(", ", Required)}.");
        unsure = [.. doubtful];
        return points;
    }

    private static Dictionary<string, Vec> Joints(Dictionary<string, Vec> p)
    {
        var shoulderMid = Vec.Mid(p["leftShoulder"], p["rightShoulder"]);
        var hipMid = Vec.Mid(p["leftHip"], p["rightHip"]);
        var joints = new Dictionary<string, Vec>(p)
        {
            ["@shoulderMid"] = shoulderMid,
            ["@hipMid"] = hipMid,
            ["@crown"] = p["nose"] + (p["nose"] - shoulderMid) * 0.8,
        };
        foreach (var s in new[] { "left", "right" })
            joints[$"@{s}HandTip"] = p.TryGetValue(s + "Index", out var index) ? index : p[s + "Wrist"] + (p[s + "Wrist"] - p[s + "Elbow"]) * 0.3;
        return joints;
    }

    /// <summary>
    /// Each pixel to the bone whose capsule surface is nearest; each bone's reach the 99th-percentile distance of
    /// its pixels, at least its half-width. Also stores the share of the drawing covered under <c>@covered</c>.
    /// </summary>
    private static Dictionary<string, double> Reaches(byte[] alpha, int w, int h, List<Proto> bones)
    {
        var distances = bones.Select(_ => new List<double>()).ToArray();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (alpha[y * w + x] < 8) continue;
                var p = new Vec(x + 0.5, y + 0.5);
                int best = -1;
                double bestScore = double.MaxValue, bestDistance = 0;
                for (var b = 0; b < bones.Count; b++)
                {
                    var d = Segment(p, bones[b].From, bones[b].To);
                    if (d - bones[b].Radius < bestScore) (bestScore, best, bestDistance) = (d - bones[b].Radius, b, d);
                }

                distances[best].Add(bestDistance);
            }
        }

        var reach = bones.Select((b, i) =>
        {
            var d = distances[i].Order().ToArray();
            var p99 = d.Length == 0 ? 0 : d[(int)(0.99 * (d.Length - 1))];
            return (b.Name, Reach: Math.Max(b.Radius * 1.15, p99 + 2));
        }).ToDictionary(x => x.Name, x => x.Reach);

        int inside = 0, total = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (alpha[y * w + x] < 128) continue;
                total++;
                var p = new Vec(x + 0.5, y + 0.5);
                if (bones.Any(b => Segment(p, b.From, b.To) <= reach[b.Name])) inside++;
            }
        }

        reach["@covered"] = total == 0 ? 1 : inside / (double)total;
        return reach;
    }

    /// <summary>An end bone runs on to where the drawing ends along it: a landmark sits at the knuckles or the nose.</summary>
    private static Vec RunOn(byte[] alpha, int w, int h, Vec from, Vec to)
    {
        var along = to - from;
        var len = along.Length;
        if (len < 1) return to;
        var step = along * (1 / len);
        var end = to;
        for (var d = 1; d < 600; d++)
        {
            var p = to + step * d;
            if (!Inside(alpha, w, h, p)) break;
            end = p;
        }

        return end;
    }

    /// <summary>The nearer silhouette edge across a bone at a point.</summary>
    private static double HalfWidth(byte[] alpha, int w, int h, Vec at, Vec along)
    {
        var len = Math.Max(1e-3, along.Length);
        var normal = new Vec(-along.Y / len, along.X / len);
        double March(double sign)
        {
            for (var d = 1; d < 400; d++)
                if (!Inside(alpha, w, h, at + normal * (d * sign))) return d;
            return 400;
        }

        return Math.Min(March(1), March(-1));
    }

    private static bool Inside(byte[] alpha, int w, int h, Vec p) =>
        p.X >= 0 && p.Y >= 0 && p.X < w && p.Y < h && alpha[(int)p.Y * w + (int)p.X] >= 128;

    private static double Segment(Vec p, Vec a, Vec b)
    {
        var ab = b - a;
        var len2 = ab.X * ab.X + ab.Y * ab.Y;
        var t = len2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2, 0, 1);
        return (p - (a + ab * t)).Length;
    }

    private static Vec Lerp(Vec a, Vec b, double t) => a + (b - a) * t;

    private static SKBitmap Unpremultiplied(SKImage image)
    {
        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
        return bitmap;
    }

    private static SKBitmap Premultiplied(SKBitmap unpremul)
    {
        var bitmap = new SKBitmap(new SKImageInfo(unpremul.Width, unpremul.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var pixmap = unpremul.PeekPixels();
        pixmap.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
        return bitmap;
    }

    private static byte[] Alpha(SKBitmap cell)
    {
        var bytes = cell.GetPixelSpan();
        var alpha = new byte[cell.Width * cell.Height];
        for (var i = 0; i < alpha.Length; i++) alpha[i] = bytes[i * 4 + 3];
        return alpha;
    }

    /// <summary>
    /// A drawing saved on an opaque flat ground has it keyed out, flooding in from the borders over the corner
    /// colour, with a soft edge so the antialiasing halo does not travel with the bend. A cutout is left alone.
    /// </summary>
    private static int KeyGround(SKBitmap cell)
    {
        int w = cell.Width, h = cell.Height;
        var px = cell.GetPixelSpan();
        int At(int x, int y) => (y * w + x) * 4;
        if (px[At(0, 0) + 3] < 8) return 0;

        var (br, bg, bb) = (px[0], px[1], px[2]);
        var seen = new bool[w * h];
        var stack = new Stack<(int X, int Y)>();
        for (var x = 0; x < w; x++) { stack.Push((x, 0)); stack.Push((x, h - 1)); }
        for (var y = 0; y < h; y++) { stack.Push((0, y)); stack.Push((w - 1, y)); }
        var keyed = 0;
        var pixels = new byte[px.Length];
        px.CopyTo(pixels);
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || y < 0 || x >= w || y >= h || seen[y * w + x]) continue;
            var i = At(x, y);
            if (Math.Abs(pixels[i] - br) > 24 || Math.Abs(pixels[i + 1] - bg) > 24 || Math.Abs(pixels[i + 2] - bb) > 24) continue;
            seen[y * w + x] = true;
            pixels[i + 3] = 0;
            keyed++;
            stack.Push((x + 1, y)); stack.Push((x - 1, y)); stack.Push((x, y + 1)); stack.Push((x, y - 1));
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (seen[y * w + x]) continue;
                var edge = (x > 0 && seen[y * w + x - 1]) || (x < w - 1 && seen[y * w + x + 1])
                    || (y > 0 && seen[(y - 1) * w + x]) || (y < h - 1 && seen[(y + 1) * w + x]);
                if (!edge) continue;
                var i = At(x, y);
                var diff = Math.Max(Math.Abs(pixels[i] - br), Math.Max(Math.Abs(pixels[i + 1] - bg), Math.Abs(pixels[i + 2] - bb)));
                pixels[i + 3] = (byte)Math.Clamp(diff * 255 / 120, 0, 255);
            }
        }

        pixels.CopyTo(cell.GetPixelSpan());
        return keyed;
    }
    #endregion

    #region Types
    private sealed class Proto(string name, string? parent, Vec from, Vec to)
    {
        public string Name { get; } = name;

        public string? Parent { get; } = parent;

        public Vec From { get; } = from;

        public Vec To { get; set; } = to;

        public double Radius { get; set; }
    }

    private readonly record struct Vec(double X, double Y)
    {
        public double Length => Math.Sqrt(X * X + Y * Y);

        public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);

        public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);

        public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);

        public static Vec Mid(Vec a, Vec b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }
    #endregion
}
