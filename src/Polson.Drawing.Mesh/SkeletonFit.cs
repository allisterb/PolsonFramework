namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SharpGLTF.Schema2;
using SkiaSharp;

/// <summary>
/// <b>Prototype.</b> Places Mesh2Motion's human rig template (<c>rig-human.glb</c>, the skeleton the pose clips were
/// recorded on) inside an unrigged body, from the body's detected landmarks and its own cross-sections.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is for.</b> The character pipeline rigs a reconstructed body with UniRig, which needs a GPU server and
/// invents a new skeleton per mesh with anonymous bones, so the builder then has to guess which bone is which limb
/// and every clip is retargeted across two different skeletons. Fitting one known skeleton instead makes the names
/// given and the clips native. Skinning is the other half and is not here yet.
/// </para>
/// <para>
/// <b>How.</b> The body is rendered from the front and <see cref="BodyDetector"/> gives the joints across the picture:
/// hips, knees, ankles, shoulders, elbows, wrists, ears. Their depth comes from the mesh, not from MediaPipe's
/// z, which was measured as unreliable along the camera's axis: each joint sits midway through the body's
/// cross-section at that point. The spine is spaced between hips and shoulders in the template's proportions, the
/// hands and feet are the template's scaled to the forearm and the foot, and each bone is turned by the least
/// rotation that points it at its child, so the template's axes follow the body into its A-pose.
/// </para>
/// </remarks>
internal static class SkeletonFit
{
    #region Types
    /// <summary>The fitted skeleton: every template bone's joint and bind rotation, in the body's own space.</summary>
    internal sealed class Result
    {
        public Dictionary<string, Vector3> Joints { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Quaternion> Rotations { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Parent { get; } = new(StringComparer.Ordinal);
        public List<string> Warnings { get; } = [];
        public int FrontSign { get; set; } = 1;
        public Dictionary<int, float> FacingScore { get; } = [];
        public float ToesForward { get; set; }
        public float Height { get; set; }
        public BodyDetection? Detection { get; set; }
        public SKBitmap? Render { get; set; }
    }

    /// <summary>The template as loaded: world bind position and rotation per bone, and the hierarchy.</summary>
    internal sealed record Template(Dictionary<string, Vector3> At, Dictionary<string, Quaternion> Turn, Dictionary<string, string> Parent,
                                    Dictionary<string, List<string>> Children);
    #endregion

    #region Fields
    /// <summary>The child each bone points at, for turning it. A bone not listed keeps its parent's turn.</summary>
    static readonly Dictionary<string, string> Aim = new(StringComparer.Ordinal)
    {
        ["root"] = "pelvis", ["pelvis"] = "spine_01", ["spine_01"] = "spine_02", ["spine_02"] = "spine_03",
        ["spine_03"] = "neck_01", ["neck_01"] = "head", ["head"] = "head_leaf",
        ["clavicle_l"] = "upperarm_l", ["upperarm_l"] = "lowerarm_l", ["lowerarm_l"] = "hand_l", ["hand_l"] = "middle_01_l",
        ["clavicle_r"] = "upperarm_r", ["upperarm_r"] = "lowerarm_r", ["lowerarm_r"] = "hand_r", ["hand_r"] = "middle_01_r",
        ["thigh_l"] = "calf_l", ["calf_l"] = "foot_l", ["foot_l"] = "ball_l", ["ball_l"] = "ball_leaf_l",
        ["thigh_r"] = "calf_r", ["calf_r"] = "foot_r", ["foot_r"] = "ball_r", ["ball_r"] = "ball_leaf_r",
    };
    #endregion

    #region Methods
    /// <summary>Reads a rig template: a skin with no mesh, every bone's rest pose in its node transforms.</summary>
    internal static Template LoadTemplate(string path)
    {
        var model = ModelRoot.Load(path);
        var skin = model.LogicalSkins.Single();
        var bones = Enumerable.Range(0, skin.JointsCount).Select(i => skin.GetJoint(i).Joint).ToList();
        var names = bones.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        var at = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        var turn = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var b in bones)
        {
            var w = b.WorldMatrix;
            at[b.Name] = w.Translation;
            turn[b.Name] = Quaternion.Normalize(PoseRetarget.Rot(w));
            if (b.VisualParent is { } p && names.Contains(p.Name)) parent[b.Name] = p.Name;
            children[b.Name] = [.. b.VisualChildren.Select(c => c.Name).Where(names.Contains)];
        }
        return new Template(at, turn, parent, children);
    }

    /// <summary>Fits the template into <paramref name="body"/>, an unrigged mesh standing on the floor.</summary>
    internal static Result Fit(FaceMesh body, Template t)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!BodyDetector.Available) throw new InvalidOperationException($"Fitting a skeleton needs body detection: {BodyDetector.Missing}.");

        var v = body.Reference;
        float x0 = v.Min(p => p.X), x1 = v.Max(p => p.X), y0 = v.Min(p => p.Y), y1 = v.Max(p => p.Y);
        float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, bodyH = y1 - y0;
        const int H = 1024;
        var s = 0.86f * H / bodyH;
        var W = Math.Max(256, (int)MathF.Ceiling(((x1 - x0) * s) + (0.14f * H)));

        // Which way it faces, from its feet: toes reach forward of the knees. The detector cannot say — measured on
        // lastlight3, its front and back renders scored within 1% of each other, and it chose the back for Tomas —
        // so it is asked only when the feet are hidden (a floor-length skirt), and then both facings are rendered.
        var toes = ToesForward(v, y0, bodyH);
        var facings = MathF.Abs(toes) >= MinToes ? new[] { toes > 0 ? 1 : -1 } : new[] { 1, -1 };
        (BodyDetection Pose, int Front, SKBitmap Image)? best = null;
        var scores = new Dictionary<int, float>();
        var bestScore = -1f;
        foreach (var front in facings)
        {
            var image = FaceBake.Render(body, W, H,
                p => new SKPoint((W / 2f) + (((p.X * front) - (cx * front)) * s), (H / 2f) - ((p.Y - cy) * s)), front, SKColors.White);
            var pose = BodyDetector.Detect(image);
            var score = pose.Found ? CharacterBuilder.FaceScore(pose) + CharacterBuilder.LimbScore(pose) : -1f;
            scores[front] = score;
            if (score > bestScore) { best?.Image.Dispose(); best = (pose, front, image); bestScore = score; }
            else image.Dispose();
        }
        if (best is not { Pose.Found: true } chosen) throw new InvalidOperationException("No body was found in a front render.");

        var f = chosen.Front;
        var r = new Result { FrontSign = f, Height = bodyH, Detection = chosen.Pose, Render = chosen.Image, ToesForward = toes };
        if (facings.Length > 1) r.Warnings.Add($"the feet do not show which way the body faces; the detector's choice, {(f > 0 ? "+Z" : "-Z")}, was used.");
        foreach (var (k, p) in t.Parent) r.Parent[k] = p;
        foreach (var (k, sc) in scores) r.FacingScore[k] = sc;

        // Everything below is in a canonical frame facing +Z like the template; mapped back at the end.
        var cv = v.Select(p => new Vector3(p.X * f, p.Y, p.Z * f)).ToArray();
        var floor = y0;
        Vector2? Lm(string name, float min = 0.3f) =>
            chosen.Pose.Point(name, min) is { } p ? new Vector2((f * cx) + ((p.X - (W / 2f)) / s), cy - ((p.Y - (H / 2f)) / s)) : null;

        // The middle of the body's cross-section through a point seen from the front: the joint's depth.
        float Depth(Vector2 at, float radius = 0.025f)
        {
            for (var k = 1; k <= 4; k++)
            {
                var rr = radius * bodyH * k;
                float zMin = float.MaxValue, zMax = float.MinValue;
                foreach (var p in cv)
                    if (((p.X - at.X) * (p.X - at.X)) + ((p.Y - at.Y) * (p.Y - at.Y)) < rr * rr)
                    { zMin = Math.Min(zMin, p.Z); zMax = Math.Max(zMax, p.Z); }
                if (zMax >= zMin) return (zMin + zMax) / 2f;
            }
            r.Warnings.Add($"no surface near ({at.X:0.000}, {at.Y:0.000}); depth taken as the body's middle.");
            return 0f;
        }
        Vector3 In(Vector2 at) => new(at.X, at.Y, Depth(at));

        var need = new[] { "leftHip", "rightHip", "leftShoulder", "rightShoulder", "leftKnee", "rightKnee", "leftAnkle", "rightAnkle",
                           "leftElbow", "rightElbow", "leftWrist", "rightWrist" };
        var missing = need.Where(n => Lm(n) is null).ToList();
        if (missing.Count > 0) throw new InvalidOperationException($"The front render did not show {string.Join(", ", missing)} clearly enough.");

        var c = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var (side, sfx) in new[] { ("left", "_l"), ("right", "_r") })
        {
            c["thigh" + sfx] = In(Lm(side + "Hip")!.Value);
            c["calf" + sfx] = In(Lm(side + "Knee")!.Value);
            c["foot" + sfx] = In(Lm(side + "Ankle")!.Value);
            c["upperarm" + sfx] = In(Lm(side + "Shoulder")!.Value);
            c["lowerarm" + sfx] = In(Lm(side + "Elbow")!.Value);
            c["hand" + sfx] = In(Lm(side + "Wrist")!.Value);
        }

        // The trunk: hips to shoulders, spaced as the template spaces them.
        var hips = (c["thigh_l"] + c["thigh_r"]) / 2f;
        var shoulders = (c["upperarm_l"] + c["upperarm_r"]) / 2f;
        var tHips = (t.At["thigh_l"] + t.At["thigh_r"]) / 2f;
        var tShoulders = (t.At["upperarm_l"] + t.At["upperarm_r"]) / 2f;
        var torso = (shoulders - hips).Length() / (tShoulders - tHips).Length();
        foreach (var b in new[] { "pelvis", "spine_01", "spine_02", "spine_03" })
        {
            var u = (t.At[b].Y - tHips.Y) / (tShoulders.Y - tHips.Y);
            var p = Vector2.Lerp(new(hips.X, hips.Y), new(shoulders.X, shoulders.Y), u);
            c[b] = In(p);
        }

        // Neck and head as the joint namer places them: the neck just above the shoulders, the head at the base of
        // the skull, a little below the ears; the top of the head is the mesh's own top above it.
        var ears = Mid(Lm("leftEar"), Lm("rightEar")) ?? Mid(Lm("leftEye"), Lm("rightEye")) ?? Lm("nose");
        if (ears is not { } e) throw new InvalidOperationException("The front render did not show the head clearly enough.");
        var sh2 = new Vector2(shoulders.X, shoulders.Y);
        c["neck_01"] = In(Vector2.Lerp(sh2, e, 0.15f));
        c["head"] = In(Vector2.Lerp(e, sh2, 0.3f));
        var top = cv.Where(p => MathF.Abs(p.X - e.X) < 0.06f * bodyH && p.Y > e.Y).Select(p => p.Y).DefaultIfEmpty(e.Y).Max();
        c["head_leaf"] = new Vector3(c["head"].X, top, c["head"].Z);
        c["root"] = new Vector3(c["pelvis"].X, floor, c["pelvis"].Z);

        // Clavicles: from the neck's base toward each shoulder, as far along and as far forward as in the template.
        foreach (var sfx in new[] { "_l", "_r" })
        {
            var tc = t.At["clavicle" + sfx] - t.At["neck_01"];
            var tu = t.At["upperarm" + sfx] - t.At["neck_01"];
            var along = Vector3.Dot(tc, tu) / tu.LengthSquared();
            var p = Vector3.Lerp(c["neck_01"], c["upperarm" + sfx], along);
            c["clavicle" + sfx] = p with { Z = p.Z + ((tc.Z - (tu.Z * along)) * torso) };
        }

        // Feet: the ankle is detected, but no lower than the template's ankle scaled to the torso: over boots the
        // detector puts it at the sole (0.4% of Tomas's height, against UniRig's 5.7%). The toe is the front of the
        // foot's own geometry at the floor, and the ball sits where the template puts it between them.
        foreach (var sfx in new[] { "_l", "_r" })
        {
            var lowest = floor + ((t.At["foot" + sfx].Y - t.At["root"].Y) * torso);
            if (c["foot" + sfx].Y < lowest) c["foot" + sfx] = In(new Vector2(c["foot" + sfx].X, lowest));
            var ankle = c["foot" + sfx];
            var foot = cv.Where(p => p.Y < ankle.Y && MathF.Abs(p.X - ankle.X) < 0.05f * bodyH).ToList();
            var toeZ = foot.Count > 0 ? foot.Max(p => p.Z) : ankle.Z + (0.25f * torso);
            var leaf = new Vector3(ankle.X, floor + ((t.At["ball_leaf" + sfx].Y - t.At["root"].Y) * torso), toeZ);
            var tAnkle = t.At["foot" + sfx];
            var tLeaf = t.At["ball_leaf" + sfx];
            var k = (leaf.Z - ankle.Z) / (tLeaf.Z - tAnkle.Z);
            var tb = t.At["ball" + sfx] - tAnkle;
            c["ball_leaf" + sfx] = leaf;
            c["ball" + sfx] = new Vector3(ankle.X, floor + ((t.At["ball" + sfx].Y - t.At["root"].Y) * torso), ankle.Z + (tb.Z * k));
        }

        // Hands: the template's hand, scaled to the forearm and swung onto it.
        foreach (var sfx in new[] { "_l", "_r" })
        {
            var tFore = t.At["hand" + sfx] - t.At["lowerarm" + sfx];
            var fore = c["hand" + sfx] - c["lowerarm" + sfx];
            var swing = PoseRetarget.FromTo(Vector3.Normalize(tFore), Vector3.Normalize(fore));
            var k = fore.Length() / tFore.Length();
            foreach (var b in Descendants(t, "hand" + sfx))
                c[b] = c["hand" + sfx] + Vector3.Transform((t.At[b] - t.At["hand" + sfx]) * k, swing);
        }

        var unplaced = t.At.Keys.Where(b => !c.ContainsKey(b)).ToList();
        if (unplaced.Count > 0) r.Warnings.Add($"not placed: {string.Join(", ", unplaced)}.");

        // Turns: each bone swung by the least rotation taking its template direction onto its fitted one.
        foreach (var b in TopDown(t))
        {
            Quaternion q;
            if (Aim.TryGetValue(b, out var child) && c.ContainsKey(child) && c.ContainsKey(b))
            {
                var from = t.At[child] - t.At[b];
                var to = c[child] - c[b];
                q = from.LengthSquared() > 1e-10f && to.LengthSquared() > 1e-10f
                    ? PoseRetarget.FromTo(Vector3.Normalize(from), Vector3.Normalize(to)) : Quaternion.Identity;
            }
            else if (b.Contains('_') && FingerAim(t, b) is { } next && c.ContainsKey(next))
                q = PoseRetarget.FromTo(Vector3.Normalize(t.At[next] - t.At[b]), Vector3.Normalize(c[next] - c[b]));
            else
                q = t.Parent.TryGetValue(b, out var p) && r.Rotations.TryGetValue(p, out var pq)
                    ? pq * Quaternion.Inverse(t.Turn[p]) : Quaternion.Identity;
            r.Rotations[b] = Quaternion.Normalize(q * t.Turn[b]);
        }

        // Back to the body's own facing.
        foreach (var (b, p) in c) r.Joints[b] = new Vector3(p.X * f, p.Y, p.Z * f);
        if (f < 0)
        {
            var flip = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
            foreach (var b in r.Rotations.Keys.ToList()) r.Rotations[b] = Quaternion.Normalize(flip * r.Rotations[b]);
        }
        return r;
    }

    /// <summary>
    /// The unrigged body at <paramref name="meshPath"/>, rigged with the fitted skeleton: bone nodes at the fitted
    /// joints and turns, a skin binding them, and weights from <see cref="SkinWeights"/>. Returns GLB bytes.
    /// </summary>
    /// <remarks>
    /// Weights are solved against the mesh as the file places it (its node's world transform applied), and each
    /// inverse bind carries that transform, since a skinned mesh's own node transform is ignored in glTF.
    /// </remarks>
    internal static byte[] Rig(string meshPath, Result fit, Template t, bool armPlane = true, List<string>? report = null, int diffuse = 0, bool coat = false) =>
        RigWithSkirt(meshPath, fit, t, skirt: false, armPlane, report, diffuse, coat).Glb;

    /// <summary>
    /// As <see cref="Rig"/>, and with <paramref name="skirt"/>, a coat or skirt found round the legs gets bones of its
    /// own: <see cref="SkirtChains"/> chains hanging from the pelvis, its vertices weighted to them, and a
    /// <see cref="Skirt"/> to drive them from the legs at every pose. Null when no garment was found.
    /// </summary>
    internal static (byte[] Glb, Skirt? Skirt) RigWithSkirt(string meshPath, Result fit, Template t, bool skirt, bool armPlane = true,
        List<string>? report = null, int diffuse = 0, bool coat = false)
    {
        var model = ModelRoot.Load(meshPath);
        var scene = model.DefaultScene;
        var order = TopDown(t).ToList();
        var at = new Dictionary<string, Vector3>(fit.Joints, StringComparer.Ordinal);
        var turn = new Dictionary<string, Quaternion>(fit.Rotations, StringComparer.Ordinal);
        var parent = new Dictionary<string, string>(t.Parent, StringComparer.Ordinal);
        var children = t.Children.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal);
        SkinWeights.Bone BoneOf(string b) => new(b, parent.GetValueOrDefault(b), at[b], children[b].FirstOrDefault(), children[b].Count > 0);

        var meshNodes = model.LogicalNodes.Where(nd => nd.Mesh is not null).ToList();
        (Vector3[] Positions, List<(int A, int B, int C)> Tris) Read(Node node, MeshPrimitive prim)
        {
            var place = node.WorldMatrix;
            return ([.. prim.GetVertexAccessor("POSITION").AsVector3Array().Select(v => Vector3.Transform(v, place))], [.. prim.GetTriangleIndices()]);
        }

        // The skirt, found on the first primitive with a first pass of the plain weights, then built as bones.
        Skirt? drive = null;
        SkinWeights.Garment? garment = null;
        if (skirt && meshNodes.Count > 0)
        {
            var (positions, tris) = Read(meshNodes[0], meshNodes[0].Mesh.Primitives[0]);
            var plain = order.Select(BoneOf).ToList();
            var (first, _) = SkinWeights.Solve(plain, positions, tris, armPlane);
            garment = SkinWeights.FindGarment(plain, positions, tris, first);
            if (garment is { Count: > 0 } g && BuildSkirt(g, positions, fit, at, turn, parent, children, report) is { } built)
            {
                drive = built;
                order.AddRange(built.Chains.SelectMany(c => c));
            }
            else report?.Add("skirt: no garment round the legs, so no skirt bones");
        }

        Matrix4x4 World(string b) => Matrix4x4.CreateFromQuaternion(turn[b]) with { Translation = at[b] };
        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var b in order)
        {
            var node = parent.TryGetValue(b, out var p) ? nodes[p].CreateNode(b) : scene.CreateNode(b);
            var parentWorld = parent.TryGetValue(b, out var pp) ? World(pp) : Matrix4x4.Identity;
            Matrix4x4.Invert(parentWorld, out var toParent);
            node.LocalMatrix = World(b) * toParent;
            nodes[b] = node;
        }
        var bones = order.Select(BoneOf).ToList();

        foreach (var meshNode in meshNodes)
        {
            var place = meshNode.WorldMatrix;
            var skin = model.CreateSkin();
            skin.BindJoints([.. order.Select(b =>
            {
                Matrix4x4.Invert(World(b), out var inv);
                return (nodes[b], place * inv);
            })]);

            foreach (var prim in meshNode.Mesh.Primitives)
            {
                var (positions, tris) = Read(meshNode, prim);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Action<int[], float[]>? toChains = drive is null ? null : (jj, ww) => drive.Weigh(bones, positions, tris, jj, ww, report);
                var (joints, weights) = SkinWeights.Solve(bones, positions, tris, armPlane, diffuse: diffuse, coat: coat, report: report, garment: toChains);
                report?.Add($"{positions.Length} vertices, {tris.Count} triangles weighted in {sw.ElapsedMilliseconds} ms");

                var jb = new byte[joints.Length * 2];
                var js = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(jb.AsSpan());
                for (var i = 0; i < joints.Length; i++) js[i] = (ushort)joints[i];
                var ja = model.CreateAccessor();
                ja.SetData(model.UseBufferView(jb, 0, null, 0, BufferMode.ARRAY_BUFFER), 0, positions.Length,
                           DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, false);
                prim.SetVertexAccessor("JOINTS_0", ja);

                var wb = new byte[weights.Length * 4];
                weights.AsSpan().CopyTo(System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(wb.AsSpan()));
                var wa = model.CreateAccessor();
                wa.SetData(model.UseBufferView(wb, 0, null, 0, BufferMode.ARRAY_BUFFER), 0, positions.Length,
                           DimensionType.VEC4, EncodingType.FLOAT, false);
                prim.SetVertexAccessor("WEIGHTS_0", wa);

                if (report is not null)
                {
                    var byBone = new int[order.Count];
                    for (var i = 0; i < positions.Length; i++) byBone[joints[i * 4]]++;
                    var blended = Enumerable.Range(0, positions.Length).Count(i => weights[(i * 4) + 1] > 0f);
                    report.Add($"blended {blended} ({blended / (float)positions.Length:P0}); bones owning vertices: "
                             + string.Join(", ", Enumerable.Range(0, order.Count).Where(b => byBone[b] > 0)
                                 .OrderByDescending(b => byBone[b]).Take(12).Select(b => $"{order[b]} {byBone[b]}")));
                }
            }
            meshNode.Skin = skin;
        }

        using var stream = new System.IO.MemoryStream();
        model.WriteGLB(stream);
        return (stream.ToArray(), drive);
    }

    /// <summary>
    /// The garment round the legs of a body's first primitive, found from a first pass of the plain weights, with
    /// the positions and triangles it indexes (the node's world transform applied, as <see cref="Rig"/> reads them).
    /// </summary>
    internal static (Vector3[] Positions, List<(int A, int B, int C)> Triangles, SkinWeights.Garment? Garment) FindGarment(
        string meshPath, Result fit, Template t, bool armPlane = true)
    {
        var model = ModelRoot.Load(meshPath);
        var node = model.LogicalNodes.First(nd => nd.Mesh is not null);
        var prim = node.Mesh.Primitives[0];
        var place = node.WorldMatrix;
        Vector3[] positions = [.. prim.GetVertexAccessor("POSITION").AsVector3Array().Select(v => Vector3.Transform(v, place))];
        List<(int A, int B, int C)> tris = [.. prim.GetTriangleIndices()];
        var parent = new Dictionary<string, string>(t.Parent, StringComparer.Ordinal);
        var bones = TopDown(t).Select(b => new SkinWeights.Bone(b, parent.GetValueOrDefault(b), fit.Joints[b],
            t.Children[b].FirstOrDefault(), t.Children[b].Count > 0)).ToList();
        var (first, _) = SkinWeights.Solve(bones, positions, tris, armPlane);
        return (positions, tris, SkinWeights.FindGarment(bones, positions, tris, first));
    }

    /// <summary>How many chains hang round the hips.</summary>
    internal const int SkirtChains = 8;

    /// <summary>
    /// Builds the skirt: <see cref="SkirtChains"/> chains of two bones each, spaced round the hips from the pelvis's
    /// centre, running down the garment's own surface from the crotch to the hem.
    /// </summary>
    static Skirt? BuildSkirt(SkinWeights.Garment g, Vector3[] positions, Result fit, Dictionary<string, Vector3> at,
        Dictionary<string, Quaternion> turn, Dictionary<string, string> parent, Dictionary<string, List<string>> children, List<string>? report)
    {
        var centre = fit.Joints["pelvis"];
        var y0 = positions.Min(p => p.Y);
        var height = positions.Max(p => p.Y) - y0;
        var cloth = Enumerable.Range(0, positions.Length).Where(i => g.Mask[i]).Select(i => positions[i]).ToList();
        float Angle(Vector3 p) => (MathF.Atan2(p.X - centre.X, p.Z - centre.Z) + (2f * MathF.PI)) % (2f * MathF.PI);
        float Gap(float a, float b) => MathF.Min(MathF.Abs(a - b), (2f * MathF.PI) - MathF.Abs(a - b));

        var skirt = new Skirt { Gravity = 0.5f };
        // From the top of the cloth actually found, which on an open coat can sit below the crotch line.
        var clothTop = MathF.Min(g.Top, cloth.Max(p => p.Y));
        float[] heights = [clothTop, (clothTop + g.Hem) / 2f, g.Hem];
        var radius = new float?[SkirtChains, heights.Length];
        for (var k = 0; k < SkirtChains; k++)
            for (var h = 0; h < heights.Length; h++)
            {
                var theta = 2f * MathF.PI * k / SkirtChains;
                foreach (var (band, spread) in new[] { (0.03f, 1f), (0.06f, 1.5f) })
                {
                    var near = cloth.Where(p => MathF.Abs(p.Y - heights[h]) < band * height && Gap(Angle(p), theta) < spread * MathF.PI / SkirtChains).ToList();
                    if (near.Count == 0) continue;
                    radius[k, h] = near.Average(p => MathF.Sqrt(((p.X - centre.X) * (p.X - centre.X)) + ((p.Z - centre.Z) * (p.Z - centre.Z))));
                    break;
                }
            }

        // A chain with no cloth at a height (Tomas's coat is open at the front) takes the nearest chains' radius there.
        var filled = 0;
        for (var h = 0; h < heights.Length; h++)
        {
            var known = Enumerable.Range(0, SkirtChains).Where(k => radius[k, h] is not null).ToList();
            if (known.Count == 0) { report?.Add($"skirt: no cloth at {(heights[h] - y0) / height:P0} of the height"); return null; }
            for (var k = 0; k < SkirtChains; k++)
                if (radius[k, h] is null)
                {
                    int Steps(int other) => Math.Min((k - other + SkirtChains) % SkirtChains, (other - k + SkirtChains) % SkirtChains);
                    var nearest = known.Min(Steps);
                    radius[k, h] = known.Where(o => Steps(o) == nearest).Average(o => radius[o, h]!.Value);
                    filled++;
                }
        }
        if (filled > 0) report?.Add($"skirt: {filled} chain joints had no cloth nearby and took their neighbours' radius");

        for (var k = 0; k < SkirtChains; k++)
        {
            var theta = 2f * MathF.PI * k / SkirtChains;
            var dir = new Vector3(MathF.Sin(theta), 0f, MathF.Cos(theta));
            var joints = new List<Vector3>();
            for (var h = 0; h < heights.Length; h++)
                joints.Add(new Vector3(centre.X + (dir.X * radius[k, h]!.Value * 0.9f), heights[h], centre.Z + (dir.Z * radius[k, h]!.Value * 0.9f)));

            string[] names = [$"skirt_{k}_0", $"skirt_{k}_1", $"skirt_{k}_2"];
            for (var j = 0; j < 3; j++)
            {
                at[names[j]] = joints[j];
                var along = j < 2 ? joints[j + 1] - joints[j] : joints[j] - joints[j - 1];
                turn[names[j]] = PoseRetarget.FromTo(Vector3.UnitY, Vector3.Normalize(along));
                parent[names[j]] = j == 0 ? "pelvis" : names[j - 1];
                children[names[j]] = j < 2 ? [names[j + 1]] : [];
            }
            skirt.Chains.Add(names);
        }

        // Legs as capsules, and how clear of each one each chain bone is at rest: a pose only has to keep a bone as
        // clear as it was, up to the leg's radius, since a chain starts right beside the hip joint.
        var r0 = MathF.Max(g.LegRadius, 0.02f * height);
        foreach (var sfx in new[] { "_l", "_r" })
        {
            skirt.Legs.Add(("thigh" + sfx, "calf" + sfx, 1.1f * r0));
            skirt.Legs.Add(("calf" + sfx, "foot" + sfx, 0.9f * r0));
        }
        skirt.Margin = 0.15f * r0;
        var pelvisTurn = turn["pelvis"];
        foreach (var chain in skirt.Chains)
            for (var j = 0; j < 2; j++)
            {
                var (a, b) = (at[chain[j]], at[chain[j + 1]]);
                skirt.Rest[chain[j]] = (Vector3.Transform(Vector3.Normalize(b - a), Quaternion.Inverse(pelvisTurn)), Vector3.Normalize(b - a), (b - a).Length());
                for (var l = 0; l < skirt.Legs.Count; l++)
                {
                    var leg = skirt.Legs[l];
                    var free = Skirt.Distance(a, b, at[leg.From], at[leg.To]);
                    skirt.Clearance[(chain[j], l)] = MathF.Min(leg.Radius + skirt.Margin, 0.95f * free);
                }
            }
        skirt.Centre = centre;
        skirt.Top = clothTop;
        skirt.Hem = g.Hem;
        report?.Add($"skirt: {SkirtChains} chains from {(clothTop - y0) / height:P0} to {(g.Hem - y0) / height:P0} of the height, legs {r0 / height:P1} thick");
        return skirt;
    }

    /// <summary>
    /// <b>Prototype.</b> A coat or skirt's own bones, and how to drive them: each chain hangs between the pelvis's lean
    /// and straight down, and swings outward just far enough to keep clear of a leg that would pass through it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why bones and not weights.</b> Weighted to the legs and pelvis, a coat can only be dragged by a leg or
    /// stretched between two; neither is hanging. Games animate long coats this way, without cloth simulation:
    /// bones of the garment's own, set every frame from the legs. Deterministic, so a pose gives the same coat every
    /// time; and a coat made of two unjoined halves has one chain down its seam, so the halves move together.
    /// </para>
    /// <para>
    /// It is stiff panels, not cloth: nothing sways, folds or bunches, and a pose extreme enough can still push a leg
    /// through.
    /// </para>
    /// </remarks>
    internal sealed class Skirt
    {
        public List<string[]> Chains { get; } = [];
        public List<(string From, string To, float Radius)> Legs { get; } = [];
        public Dictionary<string, (Vector3 InPelvis, Vector3 InWorld, float Length)> Rest { get; } = new(StringComparer.Ordinal);
        public Dictionary<(string Bone, int Leg), float> Clearance { get; } = [];

        /// <summary>0 turns each chain with the pelvis, 1 keeps it hanging as it hangs at rest, whatever the hips do.</summary>
        public float Gravity { get; set; } = 0.5f;
        public float Margin { get; set; }
        public Vector3 Centre { get; set; }
        public float Top { get; set; }
        public float Hem { get; set; }
        public string Pelvis { get; init; } = "pelvis";

        /// <summary>Weights the garment's vertices to the chains: by angle round the hips, by height down the chain.</summary>
        internal void Weigh(IReadOnlyList<SkinWeights.Bone> bones, IReadOnlyList<Vector3> positions,
            IReadOnlyList<(int A, int B, int C)> triangles, int[] joints, float[] weights, List<string>? report)
        {
            if (SkinWeights.FindGarment(bones, positions, triangles, joints) is not { Count: > 0 } g) return;
            var index = bones.Select((b, i) => (b.Name, i)).ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);
            var pelvis = index[Pelvis];
            var n = Chains.Count;
            for (var i = 0; i < positions.Count; i++)
            {
                if (!g.Mask[i]) continue;
                var p = positions[i];
                var phi = (MathF.Atan2(p.X - Centre.X, p.Z - Centre.Z) + (2f * MathF.PI)) % (2f * MathF.PI);
                var f = phi / (2f * MathF.PI / n);
                var k0 = (int)MathF.Floor(f) % n;
                var a = f - MathF.Floor(f);
                var k1 = (k0 + 1) % n;
                var t = Math.Clamp((Top - p.Y) / MathF.Max(1e-6f, Top - Hem), 0f, 1f);
                var b1 = Math.Clamp(((2f * t) - 0.75f) / 0.5f, 0f, 1f);
                var ps = t < 0.15f ? 1f - (t / 0.15f) : 0f;
                var mix = new List<(int Bone, float W)>
                {
                    (index[Chains[k0][0]], (1f - a) * (1f - ps) * (1f - b1)), (index[Chains[k0][1]], (1f - a) * (1f - ps) * b1),
                    (index[Chains[k1][0]], a * (1f - ps) * (1f - b1)), (index[Chains[k1][1]], a * (1f - ps) * b1), (pelvis, ps),
                };
                var top = mix.Where(m => m.W > 0f).OrderByDescending(m => m.W).Take(4).ToList();
                var sum = top.Sum(m => m.W);
                var o = i * 4;
                for (var q = 0; q < 4; q++)
                {
                    joints[o + q] = q < top.Count ? top[q].Bone : 0;
                    weights[o + q] = q < top.Count ? top[q].W / sum : 0f;
                }
            }
            report?.Add($"skirt: {g.Count} garment vertices weighted to the chains");
        }

        /// <summary>Sets every chain bone for the pose the rig is in. Installed as <c>MeshRig.Driver</c>.</summary>
        internal void Drive(MeshRig rig)
        {
            var pelvis = rig.EvaluatedMatrix(Pelvis);
            var lean = PoseRetarget.Rot(pelvis);
            var hub = pelvis.Translation;
            var legs = Legs.Select(l => (A: rig.EvaluatedAt(l.From), B: rig.EvaluatedAt(l.To))).ToList();
            foreach (var chain in Chains)
            {
                // The upper bone's swing, carried into the lower one so a panel stays straight unless a leg bends it.
                var carried = Quaternion.Identity;
                for (var j = 0; j < 2; j++)
                {
                    var bone = chain[j];
                    var start = rig.EvaluatedAt(bone);
                    var (inPelvis, inWorld, length) = Rest[bone];
                    var follow = j == 0 ? Vector3.Transform(inPelvis, lean) : Vector3.Transform(inWorld, carried);
                    var d = Vector3.Normalize(Vector3.Lerp(follow, inWorld, Gravity));
                    var outward = start - hub;
                    outward.Y = 0f;
                    outward = outward.LengthSquared() > 1e-10f ? Vector3.Normalize(outward) : Vector3.Transform(Vector3.UnitZ, lean);
                    d = Clear(bone, start, d, length, outward, legs);
                    Aim(rig, bone, chain[j + 1], d);
                    carried = PoseRetarget.FromTo(inWorld, d);
                }
            }
        }

        /// <summary>The direction nearest <paramref name="d"/>, swung toward <paramref name="outward"/>, that keeps the bone clear of every leg.</summary>
        Vector3 Clear(string bone, Vector3 start, Vector3 d, float length, Vector3 outward, List<(Vector3 A, Vector3 B)> legs)
        {
            var axis = Vector3.Cross(d, outward);
            if (axis.LengthSquared() < 1e-10f) return d;
            axis = Vector3.Normalize(axis);
            var tried = d;
            for (var deg = 0; deg <= 100; deg += 2)
            {
                tried = Vector3.Transform(d, Quaternion.CreateFromAxisAngle(axis, deg * MathF.PI / 180f));
                var end = start + (tried * length);
                var clear = true;
                for (var l = 0; l < legs.Count && clear; l++)
                    clear = Distance(start, end, legs[l].A, legs[l].B) >= Clearance[(bone, l)];
                if (clear) return tried;
            }
            return tried;
        }

        /// <summary>Turns a bone about its own joint so it points along <paramref name="d"/>.</summary>
        static void Aim(MeshRig rig, string bone, string child, Vector3 d)
        {
            var world = rig.EvaluatedMatrix(bone);
            var at = world.Translation;
            var now = Vector3.Normalize(rig.EvaluatedAt(child) - at);
            var q = PoseRetarget.FromTo(now, d);
            var turned = world * Matrix4x4.CreateTranslation(-at) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(at);
            Matrix4x4.Invert(rig.EvaluatedMatrix(rig.JointParent[bone]), out var toParent);
            rig.SetLocal(bone, turned * toParent);
        }

        /// <summary>The least distance between two segments.</summary>
        internal static float Distance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-12f && e <= 1e-12f) return r.Length();
            if (a <= 1e-12f) { s = 0f; t = Math.Clamp(f / e, 0f, 1f); }
            else
            {
                var c = Vector3.Dot(d1, r);
                if (e <= 1e-12f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                else
                {
                    var b = Vector3.Dot(d1, d2);
                    var denom = (a * e) - (b * b);
                    s = denom > 1e-12f ? Math.Clamp(((b * f) - (c * e)) / denom, 0f, 1f) : 0f;
                    t = ((b * s) + f) / e;
                    if (t < 0f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                    else if (t > 1f) { t = 1f; s = Math.Clamp((b - c) / a, 0f, 1f); }
                }
            }
            return ((p1 + (d1 * s)) - (p2 + (d2 * t))).Length();
        }
    }
    #endregion

    #region Private
    /// <summary>How far the toes must reach past the knees, as a share of height, to decide the facing: half the least measured.</summary>
    const float MinToes = 0.008f;

    /// <summary>Mean z of the lowest 4% of the body less mean z at knee height, as a share of height; positive when the feet point +Z.</summary>
    static float ToesForward(SKPoint3[] v, float y0, float height)
    {
        var feet = v.Where(p => p.Y < y0 + (0.04f * height)).Select(p => p.Z).DefaultIfEmpty().Average();
        var knees = v.Where(p => p.Y > y0 + (0.25f * height) && p.Y < y0 + (0.30f * height)).Select(p => p.Z).DefaultIfEmpty().Average();
        return (feet - knees) / height;
    }

    static Vector2? Mid(Vector2? a, Vector2? b) => a is { } p && b is { } q ? (p + q) / 2f : a ?? b;

    static IEnumerable<string> Descendants(Template t, string bone)
    {
        var stack = new Stack<string>(t.Parent.Where(kv => kv.Value == bone).Select(kv => kv.Key));
        while (stack.Count > 0)
        {
            var b = stack.Pop();
            yield return b;
            foreach (var ch in t.Parent.Where(kv => kv.Value == b).Select(kv => kv.Key)) stack.Push(ch);
        }
    }

    /// <summary>Parents before children, so a leaf can take its parent's turn.</summary>
    static IEnumerable<string> TopDown(Template t)
    {
        var roots = t.At.Keys.Where(b => !t.Parent.ContainsKey(b));
        foreach (var root in roots)
        {
            yield return root;
            foreach (var d in Descendants(t, root).OrderBy(b => Depth(t, b))) yield return d;
        }
    }

    static int Depth(Template t, string b)
    {
        var d = 0;
        while (t.Parent.TryGetValue(b, out var p)) { b = p; d++; }
        return d;
    }

    /// <summary>A finger bone's only child, which is the next knuckle along.</summary>
    static string? FingerAim(Template t, string bone) =>
        t.Parent.Where(kv => kv.Value == bone).Select(kv => kv.Key).SingleOrDefault(k => !k.StartsWith("hand", StringComparison.Ordinal));
    #endregion
}
