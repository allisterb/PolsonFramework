namespace Polson.Drawing.Skia;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SharpGLTF.Schema2;

/// <summary>
/// Poses a rigged character from one frame of a recorded humanoid clip, by chain retargeting.
/// </summary>
/// <remarks>
/// <para>
/// <b>The method is Mesh2Motion's swing-and-twist retargeting</b> (Scott Petrovic, MIT, after
/// sketchpunklabs), reimplemented here against our rig rather than ported: each target bone is turned
/// until one direction on it matches the source bone's, then rolled about that direction until a
/// second one does. Nothing is solved; the pose is whatever the performer did.
/// </para>
/// <para>
/// <b>One change, and it is the reason this works on our characters.</b> Mesh2Motion takes each limb's
/// swing direction as a fixed world axis — +X along a left arm — which is right only when both rigs
/// rest in the same pose. Its clips rest in a T-pose and a UniRig character does not, so a fixed axis
/// carries the difference into every frame. Here a limb's swing is the bone's own direction, joint to
/// child, which is the same thing in any rest pose.
/// </para>
/// <para>
/// Trunk, head, hands and feet take the source's change of world rotation from its rest. The hips also
/// move, in place and scaled to the character's legs, so a crouch lowers the pelvis and the feet stay
/// down; see <see cref="HipMove"/>.
/// </para>
/// </remarks>
public static class PoseRetarget
{
    #region Types
    /// <summary>One clip: its name, length, and the file it came from.</summary>
    internal sealed record Clip(string Name, float Seconds, string File, ModelRoot Model, Animation Animation);
    #endregion

    #region Fields
    /// <summary>
    /// Target body part, the source bone it follows, and — for a limb bone — the source and target
    /// children that give its direction. Source names are Mesh2Motion's skeleton.
    /// </summary>
    static readonly (string Part, string Src, string? SrcChild, string? PartChild)[] Chains =
    [
        ("hips", "pelvis", null, null),
        ("spine", "spine_02", null, null),
        ("chest", "spine_03", null, null),
        ("neck", "neck_01", null, null),
        ("head", "head", null, null),
        ("leftUpperArm", "upperarm_l", "lowerarm_l", "leftForearm"),
        ("leftForearm", "lowerarm_l", "hand_l", "leftHand"),
        ("leftHand", "hand_l", null, null),
        ("rightUpperArm", "upperarm_r", "lowerarm_r", "rightForearm"),
        ("rightForearm", "lowerarm_r", "hand_r", "rightHand"),
        ("rightHand", "hand_r", null, null),
        ("leftThigh", "thigh_l", "calf_l", "leftShin"),
        ("leftShin", "calf_l", "foot_l", "leftFoot"),
        ("leftFoot", "foot_l", null, null),
        ("rightThigh", "thigh_r", "calf_r", "rightShin"),
        ("rightShin", "calf_r", "foot_r", "rightFoot"),
        ("rightFoot", "foot_r", null, null),
    ];

    static readonly ConcurrentDictionary<string, (DateTime Stamp, ModelRoot Model)> Loaded = new(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Properties
    /// <summary>A pose library folder from <c>Poses:Library</c>, overriding discovery.</summary>
    public static string? LibraryOverride { get; set; }

    /// <summary>The folder under the project whose clips come first.</summary>
    public const string ProjectFolder = "poses";
    #endregion

    #region Methods
    /// <summary>Every clip available here, project folder first, then the library; first name wins.</summary>
    internal static List<Clip> Clips(string? projectRoot)
    {
        var clips = new List<Clip>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var folder in Folders(projectRoot))
            foreach (var file in Directory.GetFiles(folder, "*.glb").Order(StringComparer.Ordinal))
            {
                var model = Load(file);
                foreach (var anim in model.LogicalAnimations)
                    if (!string.IsNullOrEmpty(anim.Name) && seen.Add(anim.Name))
                        clips.Add(new Clip(anim.Name, anim.Duration, file, model, anim));
            }
        return clips;
    }

    /// <summary>Where clips are looked for, in order: the project's <c>poses/</c>, then the library.</summary>
    public static List<string> Folders(string? projectRoot)
    {
        var folders = new List<string>();
        if (!string.IsNullOrEmpty(projectRoot) && Directory.Exists(Path.Combine(projectRoot, ProjectFolder)))
            folders.Add(Path.Combine(projectRoot, ProjectFolder));
        if (!string.IsNullOrWhiteSpace(LibraryOverride))
        {
            if (Directory.Exists(LibraryOverride)) folders.Add(LibraryOverride);
        }
        else if (FaceDetector.Candidates("models", "poses").FirstOrDefault(Directory.Exists) is { } found)
            folders.Add(found);
        return folders;
    }

    /// <summary>The pose, keyed by body part, that puts <paramref name="rig"/> where the clip is at <paramref name="seconds"/>.</summary>
    internal static Dictionary<string, object?> Retarget(MeshRig rig, Clip clip, float seconds, bool moveHips = true)
    {
        if (rig.Aliases.Count == 0)
            throw new ArgumentException(
                "Character.retarget needs a character's body-part names, and this mesh has none. " +
                "Load it with Character.load(name) rather than Mesh.load(...).");

        var source = clip.Model.LogicalNodes.Where(n => !string.IsNullOrEmpty(n.Name))
            .GroupBy(n => n.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var missing = Chains.SelectMany(c => new[] { c.Src, c.SrcChild }).OfType<string>()
            .Where(n => !source.ContainsKey(n)).Distinct().ToList();
        if (missing.Count > 0)
            throw new ArgumentException(
                $"Clip '{clip.Name}' is not on a skeleton Character.retarget can read: it has no " +
                $"{string.Join(", ", missing)}. It reads Mesh2Motion's human skeleton.");

        // A character built facing -Z sees the source turned half round, so its left stays its left.
        var align = rig.FrontSign < 0 ? Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI) : Quaternion.Identity;
        var forward = Vector3.Transform(Vector3.UnitZ, align);
        Quaternion SrcRest(string n) => Quaternion.Normalize(align * Rot(source[n].WorldMatrix));
        Quaternion SrcAnim(string n) => Quaternion.Normalize(align * Rot(source[n].GetWorldMatrix(clip.Animation, seconds)));
        Vector3 SrcAt(string n) => Vector3.Transform(source[n].GetWorldMatrix(clip.Animation, seconds).Translation, align);

        var byHandle = new Dictionary<string, (string Part, string Src, string? SrcChild, string? PartChild)>(StringComparer.Ordinal);
        foreach (var c in Chains)
            if (rig.Aliases.TryGetValue(c.Part, out var h) && rig.FileNodes.ContainsKey(h)) byHandle[h] = c;

        var animWorld = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
        var pose = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var handle in rig.JointNames)
        {
            if (!rig.FileNodes.TryGetValue(handle, out var node)) continue;
            var restLocal = Rot(node.LocalMatrix);
            var parentAnim = rig.JointParent.TryGetValue(handle, out var p) && animWorld.TryGetValue(p, out var pa)
                ? pa : Rot(node.VisualParent?.WorldMatrix ?? Matrix4x4.Identity);
            var current = Quaternion.Normalize(parentAnim * restLocal);

            if (!byHandle.TryGetValue(handle, out var chain)) { animWorld[handle] = current; continue; }

            var tRest = Rot(node.WorldMatrix);
            Quaternion world;
            if (chain.SrcChild is null || chain.PartChild is null
                || !rig.Aliases.TryGetValue(chain.PartChild, out var childHandle)
                || !rig.FileNodes.TryGetValue(childHandle, out var child))
            {
                world = Quaternion.Normalize(SrcAnim(chain.Src) * Quaternion.Inverse(SrcRest(chain.Src)) * tRest);
            }
            else
            {
                var dSrc = Vector3.Normalize(SrcAt(chain.SrcChild) - SrcAt(chain.Src));
                var dLocal = Vector3.Transform(child.WorldMatrix.Translation - node.WorldMatrix.Translation, Quaternion.Inverse(tRest));
                var swung = Quaternion.Normalize(FromTo(Vector3.Transform(dLocal, current), dSrc) * current);

                // Roll about the bone: one forward reference, carried through both rigs, matched.
                var sRef = Flat(Vector3.Transform(Vector3.Transform(forward, Quaternion.Inverse(SrcRest(chain.Src))), SrcAnim(chain.Src)), dSrc);
                var tRef = Flat(Vector3.Transform(Vector3.Transform(forward, Quaternion.Inverse(tRest)), swung), dSrc);
                world = sRef == Vector3.Zero || tRef == Vector3.Zero ? swung : Quaternion.Normalize(FromTo(tRef, sRef) * swung);
            }

            animWorld[handle] = world;
            var local = Quaternion.Normalize(Quaternion.Inverse(parentAnim) * world);
            var (yaw, pitch, roll) = YawPitchRoll(Quaternion.Normalize(Quaternion.Inverse(restLocal) * local));
            const float deg = 180f / MathF.PI;
            pose[chain.Part] = new Dictionary<string, object?>
            {
                ["xDeg"] = MathF.Round(pitch * deg, 2),
                ["yDeg"] = MathF.Round(yaw * deg, 2),
                ["zDeg"] = MathF.Round(roll * deg, 2)
            };
        }

        if (moveHips && HipMove(rig, source, clip, seconds, align) is { } move
            && pose.TryGetValue("hips", out var hipsEntry) && hipsEntry is Dictionary<string, object?> hipsPose)
            hipsPose["move"] = new Dictionary<string, object?>
            {
                ["x"] = Math.Round(move.X, 5),
                ["y"] = Math.Round(move.Y, 5),
                ["z"] = Math.Round(move.Z, 5)
            };

        return pose;
    }

    /// <summary>
    /// How far the clip's pelvis has moved from its rest, in place, scaled to this character's legs;
    /// null where the hips cannot move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In place, not travelling.</b> A clip that crosses the floor carries that travel on the node
    /// above the pelvis — Mesh2Motion's <c>root</c> slides 2.96 back in a knockback and climbs 1.57 in
    /// a ladder climb — and the pelvis carries the rest: a crouch drops it from 0.92 to 0.47. So the
    /// pelvis is read relative to its parent and put back on the parent's rest, which keeps the crouch
    /// and the weight shift and leaves the character standing where it was placed.
    /// </para>
    /// <para>
    /// <b>Scaled by leg length</b>, hips above ankles, target over source, after Mesh2Motion's scaled
    /// translation: a crouch drops a short-legged character less far, so its feet stay down.
    /// </para>
    /// </remarks>
    static Vector3? HipMove(MeshRig rig, Dictionary<string, Node> source, Clip clip, float seconds, Quaternion align)
    {
        if (!rig.Aliases.TryGetValue("hips", out var hips) || !rig.CanMove(hips)
            || !rig.FileNodes.TryGetValue(hips, out var tHips)) return null;
        if (!rig.Aliases.TryGetValue("leftFoot", out var lf) || !rig.Aliases.TryGetValue("rightFoot", out var rf)
            || !rig.FileNodes.TryGetValue(lf, out var tLeft) || !rig.FileNodes.TryGetValue(rf, out var tRight)) return null;

        var pelvis = source["pelvis"];
        var anchor = pelvis.VisualParent;
        var anim = pelvis.GetWorldMatrix(clip.Animation, seconds);
        if (anchor is not null && Matrix4x4.Invert(anchor.GetWorldMatrix(clip.Animation, seconds), out var inv))
            anim = anim * inv * anchor.WorldMatrix;

        var srcLegs = pelvis.WorldMatrix.Translation.Y - ((source["foot_l"].WorldMatrix.Translation.Y + source["foot_r"].WorldMatrix.Translation.Y) / 2f);
        var tarLegs = tHips.WorldMatrix.Translation.Y - ((tLeft.WorldMatrix.Translation.Y + tRight.WorldMatrix.Translation.Y) / 2f);
        if (srcLegs <= 1e-6f || tarLegs <= 1e-6f) return null;

        return Vector3.Transform(anim.Translation - pelvis.WorldMatrix.Translation, align) * (tarLegs / srcLegs);
    }

    /// <summary>
    /// The inverse of <c>Quaternion.CreateFromYawPitchRoll</c>, which is how <c>mesh.pose</c> builds a
    /// rotation from degrees: R = Ry(yaw)·Rx(pitch)·Rz(roll).
    /// </summary>
    internal static (float Yaw, float Pitch, float Roll) YawPitchRoll(Quaternion q)
    {
        float x = q.X, y = q.Y, z = q.Z, w = q.W;
        var pitch = MathF.Asin(Math.Clamp(-2f * ((y * z) - (x * w)), -1f, 1f));
        return (MathF.Atan2(2f * ((x * z) + (y * w)), 1f - (2f * ((x * x) + (y * y)))),
                pitch,
                MathF.Atan2(2f * ((x * y) + (z * w)), 1f - (2f * ((x * x) + (z * z)))));
    }
    #endregion

    #region Private
    static ModelRoot Load(string file)
    {
        var stamp = File.GetLastWriteTimeUtc(file);
        if (Loaded.TryGetValue(file, out var hit) && hit.Stamp == stamp) return hit.Model;
        var model = ModelRoot.Load(file);
        Loaded[file] = (stamp, model);
        return model;
    }

    static Quaternion Rot(Matrix4x4 m)
    {
        Matrix4x4.Decompose(m, out _, out var q, out _);
        return Quaternion.Normalize(q);
    }

    static Vector3 Flat(Vector3 v, Vector3 axis)
    {
        var f = v - (Vector3.Dot(v, axis) * axis);
        return f.LengthSquared() < 1e-8f ? Vector3.Zero : Vector3.Normalize(f);
    }

    static Quaternion FromTo(Vector3 a, Vector3 b)
    {
        a = Vector3.Normalize(a);
        b = Vector3.Normalize(b);
        var d = Vector3.Dot(a, b);
        if (d > 0.99999f) return Quaternion.Identity;
        if (d < -0.99999f)
        {
            var ortho = Vector3.Cross(a, MathF.Abs(a.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(ortho), MathF.PI);
        }
        return Quaternion.Normalize(new Quaternion(Vector3.Cross(a, b), 1f + d));
    }
    #endregion
}
