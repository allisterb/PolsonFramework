namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using SkiaSharp;

/// <summary>
/// The local stages of building a character: naming a generated rig's joints, finding and building
/// the face from a turnaround, and assembling the finished character from its folder.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> The server's <c>GenerateCharacter</c> tool runs the remote stages —
/// reconstructing a mesh from the views, rigging it — and calls these for the rest; a script reaches
/// the result through <c>Character.load(name)</c>.
/// </para>
/// <para>
/// <b>Why joints need naming at all.</b> An automatic rigger names its bones <c>bone_0</c> to
/// <c>bone_51</c> in whatever order its network emitted them, so "which bone is the head" differs per
/// character and a pose written for one does not transfer. The pose detector does know which way up
/// a body is: render the rig from the front, detect the 33 landmarks, and give each bone the name of
/// the body part whose landmark it sits on.
/// </para>
/// </remarks>
public static class CharacterBuilder
{
    #region Types
    /// <summary>Semantic joint names mapped to a rig's own handles, and how sure the mapping is.</summary>
    public sealed class JointLabels
    {
        /// <summary><c>head</c> → <c>bone_5</c>, and so on.</summary>
        public Dictionary<string, string> Map { get; } = new(StringComparer.Ordinal);

        /// <summary>How far each named bone sits from its landmark, as a share of body height.</summary>
        public Dictionary<string, float> Error { get; } = new(StringComparer.Ordinal);

        public List<string> Warnings { get; } = [];

        /// <summary>+1 when the body faces +Z, as glTF intends; -1 when a generator turned it round.</summary>
        public int FrontSign { get; set; }

        /// <summary>The render the landmarks were found in, for a caller that wants to look.</summary>
        public SKBitmap? Render { get; set; }
    }
    #endregion

    #region Methods
    /// <summary>Names a rig's joints by detecting the body in a front render of its bind pose.</summary>
    public static JointLabels LabelJoints(FaceMesh rigged)
    {
        ArgumentNullException.ThrowIfNull(rigged);
        var rig = rigged.Rig ?? throw new ArgumentException("This mesh carries no skeleton to label.");
        if (rig.JointBind.Count == 0) throw new ArgumentException("The skeleton states no bind positions, so its bones cannot be located.");
        if (!PoseDetector.Available) throw new InvalidOperationException($"Labelling joints needs pose detection: {PoseDetector.Missing}.");

        var v = rigged.Reference;
        float x0 = v.Min(p => p.X), x1 = v.Max(p => p.X), y0 = v.Min(p => p.Y), y1 = v.Max(p => p.Y);
        float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, bodyH = y1 - y0;
        const int H = 1024;
        var s = 0.86f * H / bodyH;
        var W = Math.Max(256, (int)MathF.Ceiling(((x1 - x0) * s) + (0.14f * H)));

        // Both facings: glTF says +Z is front, a generator is not obliged to listen, and a back view
        // detects as a body too — so the one whose face landmarks the model is surer of wins.
        (PoseDetection Pose, int Front, SKBitmap Image)? best = null;
        float bestScore = -1f;
        foreach (var front in new[] { 1, -1 })
        {
            var image = FaceBake.Render(rigged, W, H,
                p => new SKPoint((W / 2f) + (((p.X * front) - (cx * front)) * s), (H / 2f) - ((p.Y - cy) * s)),
                front, SKColors.White);
            var pose = PoseDetector.Detect(image);
            var score = pose.Found ? FaceScore(pose) + LimbScore(pose) : -1f;
            if (score > bestScore) { best?.Image.Dispose(); best = (pose, front, image); bestScore = score; }
            else image.Dispose();
        }

        var labels = new JointLabels();
        if (best is not { } chosen || !chosen.Pose.Found)
        {
            labels.Warnings.Add("No body was found in a front render of the rig, so no joint could be named. "
                              + "Pose it by the rig's own bone names (mesh.joints).");
            return labels;
        }

        labels.FrontSign = chosen.Front;
        labels.Render = chosen.Image;
        var pose0 = chosen.Pose;
        var f = chosen.Front;
        SKPoint Model(PoseLandmark p) => new((cx * f) + ((p.X - (W / 2f)) / s), cy - ((p.Y - (H / 2f)) / s));
        SKPoint? Lm(string name) => pose0.At(name, 0.3f) is { } p ? Model(p) : null;

        var joints = rig.JointBind.ToDictionary(kv => kv.Key, kv => new SKPoint(kv.Value.X * f, kv.Value.Y));
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (child, parent) in rig.JointParent)
            (children.TryGetValue(parent, out var list) ? list : children[parent] = []).Add(child);

        IEnumerable<string> Ancestors(string h, bool self)
        {
            if (self) yield return h;
            while (rig.JointParent.TryGetValue(h, out var p)) { yield return p; h = p; }
        }

        IEnumerable<string> Descendants(string h)
        {
            var stack = new Stack<string>(children.TryGetValue(h, out var c) ? c : []);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                yield return n;
                if (children.TryGetValue(n, out var cc)) foreach (var x in cc) stack.Push(x);
            }
        }

        string? Nearest(SKPoint? at, IEnumerable<string> among) =>
            at is { } p ? among.Where(joints.ContainsKey).OrderBy(h => SKPoint.Distance(joints[h], p)).FirstOrDefault() : null;

        void Name(string name, string? handle, SKPoint? at)
        {
            if (handle is null || at is not { } p) { labels.Warnings.Add($"'{name}' could not be found."); return; }
            if (labels.Map.ContainsValue(handle))
                labels.Warnings.Add($"'{name}' matched {handle}, which is already '{labels.Map.First(kv => kv.Value == handle).Key}'.");
            labels.Map[name] = handle;
            var err = SKPoint.Distance(joints[handle], p) / bodyH;
            labels.Error[name] = err;
            if (err > 0.06f)
                labels.Warnings.Add($"'{name}' is {handle}, {err:P0} of the body's height from where the body's {name} was detected; check it before relying on it.");
        }

        // **Limbs from the far end inward.** The bone nearest the elbow is found first, and the upper
        // arm is then its own ancestor nearest the shoulder. A 2D nearest-bone search alone would take
        // whatever bone sits near the shoulder — a strap, a quiver, a slung weapon — where walking up
        // from the elbow can only land on the arm that elbow belongs to.
        var all = joints.Keys.ToList();
        foreach (var side in new[] { "left", "right" })
        {
            var Side = side == "left" ? "Left" : "Right";
            foreach (var (root, mid, tip, rootName, midName, tipName) in new[]
            {
                ("Shoulder", "Elbow", "Wrist", "UpperArm", "Forearm", "Hand"),
                ("Hip", "Knee", "Ankle", "Thigh", "Shin", "Foot"),
            })
            {
                var pRoot = Lm(side + root);
                var pMid = Lm(side + mid);
                var pTip = Lm(side + tip);
                var jMid = Nearest(pMid, all);
                var jTip = jMid is null ? null : Nearest(pTip, Descendants(jMid).DefaultIfEmpty(jMid));
                var jRoot = jMid is null ? null : Nearest(pRoot, Ancestors(jMid, self: false).DefaultIfEmpty(jMid));
                Name(side + rootName, jRoot, pRoot);
                Name(side + midName, jMid, pMid);
                Name(side + tipName, jTip, pTip);
            }
        }

        // The head turns about the base of the skull, which sits a little below the ears; the neck
        // about its own base, just above the shoulders.
        var ears = Mid(Lm("leftEar"), Lm("rightEar")) ?? Mid(Lm("leftEye"), Lm("rightEye")) ?? Lm("nose");
        var shoulders = Mid(Lm("leftShoulder"), Lm("rightShoulder"));
        if (ears is { } e && shoulders is { } sh)
        {
            var skullBase = Lerp(e, sh, 0.3f);
            var neckBase = Lerp(sh, e, 0.15f);
            var head = Nearest(skullBase, all);
            Name("head", head, skullBase);
            if (head is not null)
                Name("neck", Nearest(neckBase, Ancestors(head, self: false).DefaultIfEmpty(head)), neckBase);
        }
        else labels.Warnings.Add("The head could not be located, so 'head' and 'neck' are unnamed.");

        // The trunk: the thighs' common ancestor nearest the hips, and the arms' nearest the chest.
        if (Mid(Lm("leftHip"), Lm("rightHip")) is { } hips && labels.Map.TryGetValue("leftThigh", out var lt)
            && labels.Map.TryGetValue("rightThigh", out var rt))
        {
            var common = Ancestors(lt, true).Intersect(Ancestors(rt, true)).ToList();
            Name("hips", Nearest(hips, common.Count > 0 ? common : all), hips);
            if (shoulders is { } sh2 && labels.Map.TryGetValue("leftUpperArm", out var la)
                && labels.Map.TryGetValue("rightUpperArm", out var ra))
            {
                var upper = Ancestors(la, false).Intersect(Ancestors(ra, false)).ToList();
                Name("chest", Nearest(sh2, upper.Count > 0 ? upper : all), sh2);
                if (labels.Map.TryGetValue("chest", out var chest) && labels.Map.TryGetValue("hips", out var hip))
                {
                    var between = Ancestors(chest, false).TakeWhile(h => h != hip).ToList();
                    if (between.Count > 0) Name("spine", Nearest(Lerp(hips, sh2, 0.5f), between), Lerp(hips, sh2, 0.5f));
                }
            }
        }

        return labels;
    }

    /// <summary>Where a head is in one view: its centre, and the side of a square that holds it.</summary>
    public readonly record struct HeadBox(float Cx, float Cy, float Side, string How);

    /// <summary>Finds the head in one full-figure view of a turnaround.</summary>
    /// <remarks>
    /// <para>
    /// <b>By the body first.</b> The pose detector gives the eyes, ears and nose, and the square is
    /// sized from the drop from the eyes to the shoulders, which reads the same in a front view and
    /// a profile. The face detector wants a face filling a third to two thirds of the frame and pads
    /// one that fills too much, so erring large is safe and erring small is not.
    /// </para>
    /// <para>
    /// <b>By the front view when the body is not found</b> — measured on a drawn turnaround, the pose
    /// detector found the right profile and not the left. A turnaround draws every view at one scale
    /// and one height, so the front's size and height carry over, and the head's horizontal place is
    /// the ink at the top of the figure.
    /// </para>
    /// </remarks>
    public static HeadBox? FindHead(SKBitmap view, HeadBox? like, out string? why)
    {
        why = null;
        var pose = PoseDetector.Detect(view);
        if (pose.Found)
        {
            var face = new[] { "nose", "leftEye", "rightEye", "leftEar", "rightEar" }
                .Select(n => pose.At(n, 0.2f)).Where(p => p is not null).Select(p => p!.Value.Point).ToList();
            var shoulders = new[] { "leftShoulder", "rightShoulder" }
                .Select(n => pose.At(n, 0.2f)).Where(p => p is not null).Select(p => p!.Value.Point).ToList();
            if (face.Count > 0 && shoulders.Count > 0)
            {
                var centre = new SKPoint(face.Average(p => p.X), face.Average(p => p.Y));
                var drop = shoulders.Average(p => p.Y) - centre.Y;
                if (drop > 2f) return new HeadBox(centre.X, centre.Y + (drop * 0.05f), drop * 2.1f, "pose");
            }
            why = "the body was found but its head or shoulders were not";
        }
        else why = $"no body was found ({pose.Reason})";

        if (like is not { } front) return null;

        // The top of the figure: the first row carrying ink, then the ink's centre over the band the
        // crown and face occupy. Only the band, because a slung weapon beside the head sits lower.
        int top = -1;
        for (var y = 0; y < view.Height && top < 0; y++)
            for (var x = 0; x < view.Width; x++)
                if (Ink(view.GetPixel(x, y))) { top = y; break; }
        if (top < 0) { why += "; and the view is blank"; return null; }

        double sum = 0; var n = 0;
        var band = Math.Min(view.Height, top + (int)(front.Side * 0.3f));
        for (var y = top; y < band; y++)
            for (var x = 0; x < view.Width; x++)
                if (Ink(view.GetPixel(x, y))) { sum += x; n++; }
        if (n == 0) { why += "; and no head could be found at the top of the figure"; return null; }

        why = null;
        return new HeadBox((float)(sum / n), front.Cy, front.Side, "front view's scale");
    }

    /// <summary>The square a <see cref="HeadBox"/> names, padded with white where it runs off the view.</summary>
    public static SKBitmap CropHead(SKBitmap view, HeadBox box)
    {
        var side = Math.Max(32, (int)MathF.Round(box.Side));
        var crop = new SKBitmap(side, side);
        using var c = new SKCanvas(crop);
        c.Clear(SKColors.White);
        c.DrawBitmap(view, (side / 2f) - box.Cx, (side / 2f) - box.Cy);
        return crop;
    }

    static bool Ink(SKColor c) => c.Alpha > 128 && ((c.Red + c.Green + c.Blue) / 3) < 200;

    /// <summary>Builds the face from full-figure views, finding each head first.</summary>
    public static FaceMesh BuildFace(SKBitmap front, SKBitmap? left, SKBitmap? right, List<string> notes) =>
        BuildFace(front, [.. new[] { ("left", left), ("right", right) }.Where(v => v.Item2 is not null).Select(v => (v.Item1, v.Item2!))], notes);

    /// <summary>Builds the face from a front view and up to two named profiles, finding each head first.</summary>
    /// <remarks>
    /// A profile's name is only what the notes call it. Which side of the face a profile corrects is read
    /// from its detected turn, so a <c>side</c> view whose side nobody knew builds the same face as one
    /// labelled correctly.
    /// </remarks>
    public static FaceMesh BuildFace(SKBitmap front, IReadOnlyList<(string Name, SKBitmap Image)> sides, List<string> notes)
    {
        if (sides.Count > 2)
            throw new ArgumentException($"A face takes at most two profiles, and {sides.Count} were given.", nameof(sides));

        var frontBox = FindHead(front, null, out var frontWhy)
            ?? throw new ArgumentException($"The head could not be found in the front view: {frontWhy}.");
        var views = new Dictionary<string, object?> { ["front"] = new SkiaBitmapWrapper(CropHead(front, frontBox)) };

        // Face.fromViews names its two profile slots left and right; which a view lands in does not matter.
        var named = new Dictionary<string, string>();
        foreach (var (name, image) in sides)
        {
            if (FindHead(image, frontBox, out var why) is not { } box)
            {
                notes.Add($"The {name} view was not used for the face: {why}.");
                continue;
            }
            if (box.How != "pose") notes.Add($"The {name} view's head was placed by the {box.How}: the pose detector did not find its body.");
            var slot = name is "left" or "right" && !views.ContainsKey(name) ? name : views.ContainsKey("left") ? "right" : "left";
            views[slot] = new SkiaBitmapWrapper(CropHead(image, box));
            named[slot] = name;
        }

        // A side view the face detector cannot read is dropped rather than failing the character: the
        // front alone still gives a face, and a profile is an improvement rather than a requirement.
        while (true)
        {
            try
            {
                return new FaceApi().FromViews(views);
            }
            catch (ArgumentException ex) when (views.Count > 1
                                               && views.Keys.FirstOrDefault(k => k != "front" && ex.Message.Contains($"'{k}'")) is { } bad)
            {
                notes.Add($"The {named.GetValueOrDefault(bad, bad)} view was dropped from the face: {ex.Message}");
                views.Remove(bad);
            }
        }
    }

    /// <summary>
    /// Splits a turnaround sheet into its figures, left to right: the <paramref name="count"/>
    /// widest ink-free column gaps divide it, and each figure is trimmed to its tallest run of ink.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the widest gaps rather than every gap.</b> Neighbouring figures on a sheet can nearly
    /// touch — on the sheet this was written against, one figure's hand ends a few pixels short of the
    /// next one's — while a single figure's own columns are never empty, since the torso spans them.
    /// So the <c>count − 1</c> widest gaps are the separations, however narrow the narrowest is.
    /// </para>
    /// <para>
    /// <b>Why the tallest run.</b> A sheet labels its views — FRONT, BACK — above the figures, and a
    /// label is ink. It is separated from the figure by empty rows, so keeping the tallest run of
    /// inked rows keeps the figure and drops the caption.
    /// </para>
    /// </remarks>
    /// <param name="count">How many figures to cut; zero or less for every figure the sheet separates, which <see cref="OrderOf"/> then names.</param>
    public static List<SKBitmap> SplitSheet(SKBitmap sheet, int count, out string? why)
    {
        why = null;

        // Ink is whatever differs from the sheet's own ground, read from its corner: paper for a
        // drawn sheet, transparency for a keyed one. "Dark" would call a white shirt empty paper.
        var ground = sheet.GetPixel(0, 0);
        bool Ink(SKColor c) => ground.Alpha < 128
            ? c.Alpha >= 128
            : c.Alpha >= 128 && Math.Abs(c.Red - ground.Red) + Math.Abs(c.Green - ground.Green) + Math.Abs(c.Blue - ground.Blue) > 60;

        // **The figures' band first.** Captions sit in their own columns, so a column scan over the
        // whole sheet reads the gap between a caption and the figure under it as a separation —
        // measured: a label came back as a 97x31 "figure" and two real figures merged. The figures
        // are the tallest run of inked rows; the columns are then read within that band only.
        var (bandTop, bandLen) = TallestRun(sheet.Height, y =>
        {
            for (var x = 0; x < sheet.Width; x += 2) if (Ink(sheet.GetPixel(x, y))) return true;
            return false;
        });
        if (bandLen == 0) { why = "the sheet is blank"; return []; }

        var ink = new int[sheet.Width];
        for (var x = 0; x < sheet.Width; x++)
            for (var y = bandTop; y < bandTop + bandLen; y += 2)
                if (Ink(sheet.GetPixel(x, y))) ink[x]++;

        // A column is empty below a trace of ink: JPEG ringing beside a line is not a figure.
        var floor = Math.Max(1, bandLen / 500);
        List<(int Start, int End)> gaps = [];
        int first = Array.FindIndex(ink, c => c > floor), last = Array.FindLastIndex(ink, c => c > floor);
        if (first < 0) { why = "the sheet is blank"; return []; }
        for (var x = first; x <= last; x++)
        {
            if (ink[x] > floor) continue;
            var s = x;
            while (x <= last && ink[x] <= floor) x++;
            gaps.Add((s, x));
        }

        if (count <= 0)
        {
            count = gaps.Count + 1;
        }

        if (gaps.Count < count - 1)
        {
            why = $"the sheet shows {gaps.Count + 1} separate figure(s), and {count} views were expected";
            return [];
        }

        var cuts = gaps.OrderByDescending(g => g.End - g.Start).Take(count - 1).OrderBy(g => g.Start).ToList();

        // Each figure's columns, and how far its margin may reach: to the middle of the gap beside it,
        // never across — a narrow gap padded blindly picks up a sliver of the neighbour's hand.
        List<(int X0, int X1, int Left, int Right)> columns = [];
        int from = first, reach = 0;
        foreach (var g in cuts)
        {
            var middle = (g.Start + g.End) / 2;
            columns.Add((from, g.Start, reach, middle));
            from = g.End;
            reach = middle;
        }
        columns.Add((from, last + 1, reach, sheet.Width));

        List<SKBitmap> figures = [];
        foreach (var (x0, x1, left, right) in columns)
        {
            // This figure's own rows: its tallest run of ink, so its caption is left behind.
            var (bestStart, bestLen) = TallestRun(sheet.Height, y =>
            {
                for (var x = x0; x < x1; x += 2) if (Ink(sheet.GetPixel(x, y))) return true;
                return false;
            });

            // A margin back in, so the figure is not cut flush at its own edge.
            var m = Math.Max(4, (x1 - x0) / 20);
            var r = SKRectI.Intersect(new SKRectI(Math.Max(left, x0 - m), bestStart - m, Math.Min(right, x1 + m), bestStart + bestLen + m),
                                      new SKRectI(0, 0, sheet.Width, sheet.Height));
            var fig = new SKBitmap(r.Width, r.Height);
            using (var c = new SKCanvas(fig))
            {
                c.Clear(SKColors.White);
                c.DrawBitmap(sheet, -r.Left, -r.Top);
            }
            figures.Add(fig);
        }

        return figures;
    }

    /// <summary>What a sheet's figures are, left to right, when nobody said: read from how many there are.</summary>
    /// <remarks>
    /// <para>
    /// Three is front, side, back: the layout the storyboard workflows generate, with one profile,
    /// because a generated sheet asked for a left and a right side view tends to return the front twice
    /// or two profiles facing the same way, and <see cref="BuildFace(SKBitmap, IReadOnlyList{ValueTuple{string, SKBitmap}}, List{string})"/>
    /// reads which way a profile faces from the picture anyway. Four is front, back, left, right, as
    /// sheets were made before that. One is a front alone.
    /// </para>
    /// <para>
    /// <b>Null when two figures touch.</b> They are read as one, and the count then names every view
    /// after them wrongly, so a four-figure sheet with two hands touching would build as a three-view
    /// one. A merged pair is about twice as wide as the other figures, which no single figure is.
    /// </para>
    /// </remarks>
    public static string[]? OrderOf(IReadOnlyList<SKBitmap> figures, out string? why)
    {
        why = null;
        var widths = figures.Select(f => f.Width).Order().ToArray();
        if (widths.Length > 1 && widths[^1] > 1.7 * widths[widths.Length / 2])
        {
            why = $"one figure is {widths[^1]}px wide against a typical {widths[widths.Length / 2]}px, which is two figures touching";
            return null;
        }

        string[]? order = figures.Count switch
        {
            1 => ["front"],
            3 => ["front", "side", "back"],
            4 => ["front", "back", "left", "right"],
            _ => null,
        };
        if (order is null) why = $"the sheet holds {figures.Count} separate figure(s), and only 1, 3 or 4 can be named without sheetOrder";
        return order;
    }

    /// <summary>The longest run of rows the test holds for, tolerating breaks of under 12 rows.</summary>
    /// <remarks>The tolerance is so a figure's own light patches — a white shirt, a gap between the legs — do not split it.</remarks>
    static (int Start, int Length) TallestRun(int height, Func<int, bool> inked)
    {
        int bestStart = 0, bestLen = 0;
        for (var y = 0; y < height; y++)
        {
            if (!inked(y)) continue;
            int s = y, gap = 0;
            while (y < height && (inked(y) ? (gap = 0) == 0 : ++gap < 12)) y++;
            var len = y - s - gap;
            if (len > bestLen) { bestStart = s; bestLen = len; }
        }
        return (bestStart, bestLen);
    }

    /// <summary>Writes a face mesh as <c>face.obj</c> and its atlas as <c>face.png</c>.</summary>
    public static void SaveFace(FaceMesh face, string dir)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (face.Texture is null || !face.HasUvs || face.Fitted)
            throw new ArgumentException("Only a face with its own atlas can be saved.");

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("# face mesh written by Polson's character generator\n");
        foreach (var p in face.Vertices) sb.Append(inv, $"v {p.X:R} {p.Y:R} {p.Z:R}\n");
        foreach (var t in face.Uvs) sb.Append(inv, $"vt {t.X:R} {t.Y:R}\n");
        for (var i = 0; i < face.Indices.Length; i += 3)
        {
            int a = face.Indices[i] + 1, b = face.Indices[i + 1] + 1, c = face.Indices[i + 2] + 1;
            sb.Append(inv, $"f {a}/{a} {b}/{b} {c}/{c}\n");
        }

        File.WriteAllText(Path.Combine(dir, FaceObj), sb.ToString());
        using var data = face.Texture.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(Path.Combine(dir, FacePng));
        data.SaveTo(fs);
    }

    /// <summary>Reads back what <see cref="SaveFace"/> wrote.</summary>
    public static FaceMesh LoadFace(string dir, string source)
    {
        var mesh = new MeshToolkit().FromObj(File.ReadAllText(Path.Combine(dir, FaceObj)));
        var texture = SKBitmap.Decode(Path.Combine(dir, FacePng))
            ?? throw new ArgumentException($"The face atlas in {dir} could not be decoded.");
        return new FaceMesh(mesh.Vertices, mesh.Uvs, mesh.Indices, true, source) { Texture = texture };
    }

    /// <summary>
    /// The finished character from its folder: the rigged body, its joint names, and its face.
    /// </summary>
    /// <param name="dir">The character's folder, holding <c>character.json</c>.</param>
    /// <param name="display">The project-relative path, for messages.</param>
    public static FaceMesh Assemble(string dir, string display)
    {
        var manifestPath = Path.Combine(dir, Manifest);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"No {Manifest} in '{display}', so it is not a finished character.", manifestPath);
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();

        var rigged = manifest["files"]?["rigged"]?.GetValue<string>() ?? RiggedGlb;
        var body = MeshGltf.Load(Path.Combine(dir, rigged), $"{display}/{rigged}");
        if (body.Rig is { } rig && manifest["joints"] is JsonObject map)
            foreach (var (name, handle) in map)
                if (handle?.GetValue<string>() is { } h) rig.Aliases[name] = h;

        if (!File.Exists(Path.Combine(dir, FaceObj)) || manifest["face"]?["anchors"] is not JsonObject anchors)
            return body;

        var face = LoadFace(dir, $"{display}/{FaceObj}");
        var options = new Dictionary<string, object?>();
        foreach (var (key, node) in anchors)
            options[key] = new Dictionary<string, object?>
            {
                ["x"] = node!["x"]!.GetValue<float>(),
                ["y"] = node["y"]!.GetValue<float>()
            };
        return body.WithFaceMesh(face, options);
    }
    #endregion

    #region Private
    static float FaceScore(PoseDetection p) =>
        new[] { "nose", "leftEye", "rightEye" }.Average(n => p.At(n)?.Visibility ?? 0f);

    static float LimbScore(PoseDetection p) =>
        new[] { "leftShoulder", "rightShoulder", "leftElbow", "rightElbow", "leftHip", "rightHip", "leftKnee", "rightKnee" }
            .Average(n => p.At(n)?.Visibility ?? 0f);

    static SKPoint? Mid(SKPoint? a, SKPoint? b) =>
        a is { } p && b is { } q ? new SKPoint((p.X + q.X) / 2f, (p.Y + q.Y) / 2f) : a ?? b;

    static SKPoint Lerp(SKPoint a, SKPoint b, float t) => new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
    #endregion

    #region Fields
    public const string Manifest = "character.json";
    public const string MeshGlb = "mesh.glb";
    public const string RiggedGlb = "rigged.glb";
    public const string FaceObj = "face.obj";
    public const string FacePng = "face.png";
    #endregion
}
