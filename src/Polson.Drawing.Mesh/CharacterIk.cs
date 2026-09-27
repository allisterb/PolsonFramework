namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

/// <summary>
/// Puts a character's hands and feet where a panel needs them, and points its head: two-bone IK and
/// look-at, applied on top of a pose.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-bone IK is solved exactly, not iterated.</b> An arm is a shoulder, an elbow and a wrist; given
/// where the wrist must go, the two bone lengths fix the elbow to a circle, and the bend direction picks
/// one point on it. The upper arm is swung onto the new elbow and the forearm onto the new wrist, each by
/// the smallest rotation that does it, so the roll the pose already had is kept.
/// </para>
/// <para>
/// <b>Everything else in the pose is left alone</b>, which is the point: <c>Character.retarget</c> gives
/// the body a performer's weight and opposition, and this fixes only the contacts it cannot know about —
/// a hand on this rail rather than the clip's.
/// </para>
/// <para>
/// Positions are in the character's own model space, the space of <c>mesh.bounds</c>. A page point is
/// taken back into it through the same projection <c>Mesh.draw</c> uses, at the depth the limb already
/// has, so "put the hand here on the page" moves it across the picture plane and not toward the camera.
/// </para>
/// </remarks>
internal static class CharacterIk
{
    #region Fields
    /// <summary>Goal name, the chain's root, middle and end body parts.</summary>
    static readonly Dictionary<string, (string Root, string Mid, string End)> Limbs = new(StringComparer.Ordinal)
    {
        ["leftHand"] = ("leftUpperArm", "leftForearm", "leftHand"),
        ["rightHand"] = ("rightUpperArm", "rightForearm", "rightHand"),
        ["leftFoot"] = ("leftThigh", "leftShin", "leftFoot"),
        ["rightFoot"] = ("rightThigh", "rightShin", "rightFoot"),
    };

    static readonly string[] Bends = ["back", "forward", "out", "in", "up", "down"];

    /// <summary>How far a look-at will turn the head, in degrees; past it the head turns this far and stops.</summary>
    const float MaxLookDeg = 80f;
    #endregion

    #region Methods
    /// <summary>The pose with its goals met as far as the limbs reach; see <c>Character.reach</c>.</summary>
    internal static Dictionary<string, object?> Reach(FaceMesh mesh, object? poseObj, object goalsObj, object? drawObj)
    {
        var rig = Rigged(mesh, "reach");
        var deltas = ReadPose(rig, poseObj, "reach", out var moves);
        var draw = drawObj is null ? null : MeshToolkit.Pose.From(JsInterop.AsDict(drawObj), mesh);
        var goals = JsInterop.AsDict(goalsObj)
            ?? throw new ArgumentException("Character.reach needs goals, such as { rightHand: { at: { x, y, z } } }.");

        var changed = new HashSet<string>(StringComparer.Ordinal);
        var reached = new Dictionary<string, object?>(StringComparer.Ordinal);
        var miss = new Dictionary<string, object?>(StringComparer.Ordinal);
        var up = Vector3.UnitY;
        var forward = Vector3.UnitZ * rig.FrontSign;

        foreach (var key in goals.Keys)
        {
            var name = Convert.ToString(key, CultureInfo.InvariantCulture) ?? "";
            var goal = JsInterop.AsDict(goals[key!])
                ?? throw new ArgumentException($"The goal for '{name}' is not an object.");
            var world = Forward(rig, deltas, moves);

            if (name == "head")
            {
                var head = Handle(rig, "head");
                CheckKeys(goal, name, "lookAt");
                var from = world[head].Translation;
                var target = Point(goal, "lookAt", name, draw, from);
                var (turned, full) = LookAt(rig, world, head, target, forward);
                deltas[head] = turned;
                changed.Add(head);
                reached[name] = full;
                miss[name] = 0.0;
                continue;
            }

            if (!Limbs.TryGetValue(name, out var chain))
                throw new ArgumentException($"Character.reach has no goal '{name}'. It takes leftHand, rightHand, leftFoot, rightFoot and head.");
            CheckKeys(goal, name, "at", "page", "bend");
            if (goal.Contains("at") == goal.Contains("page"))
                throw new ArgumentException($"The goal for '{name}' needs at (model space) or page (a page point, with draw options), and only one.");

            string root = Handle(rig, chain.Root), mid = Handle(rig, chain.Mid), end = Handle(rig, chain.End);
            Vector3 a = world[root].Translation, b = world[mid].Translation, c = world[end].Translation;
            var t = Point(goal, goal.Contains("at") ? "at" : "page", name, draw, c);
            var side = Vector3.UnitX * rig.FrontSign * (name.StartsWith("left", StringComparison.Ordinal) ? 1f : -1f);
            var isLeg = name.EndsWith("Foot", StringComparison.Ordinal);
            var pole = goal.Contains("bend") ? Bend(goal["bend"], name, forward, up, side) : CurrentBend(a, b, c, isLeg ? forward : -forward);

            var (b2, c2, ok) = Solve(a, b, c, t, pole);
            var (qRoot, qMid) = Swing(rig, world, deltas, root, mid, a, b, c, b2, c2);
            deltas[root] = qRoot;
            deltas[mid] = qMid;
            changed.Add(root);
            changed.Add(mid);
            reached[name] = ok;
            miss[name] = Math.Round(Vector3.Distance(c2, t), 5);
        }

        return new Dictionary<string, object?>
        {
            ["pose"] = WritePose(rig, poseObj, deltas, changed),
            ["reached"] = reached,
            ["miss"] = miss
        };
    }

    /// <summary>Where a body part's joint is under a pose: model space, or the page when draw options are given.</summary>
    internal static Dictionary<string, object?> Where(FaceMesh mesh, object? poseObj, string part, object? drawObj)
    {
        var rig = Rigged(mesh, "where");
        var world = Forward(rig, ReadPose(rig, poseObj, "where", out var moves), moves);
        var at = world[Handle(rig, part)].Translation;
        if (drawObj is null)
            return new() { ["x"] = at.X, ["y"] = at.Y, ["z"] = at.Z };

        var draw = MeshToolkit.Pose.From(JsInterop.AsDict(drawObj), mesh);
        var r = draw.Rotate(new SkiaSharp.SKPoint3(at.X * draw.StretchX, at.Y * draw.StretchY, at.Z * draw.StretchZ));
        return new() { ["x"] = draw.X + (r.X * draw.Scale), ["y"] = draw.Y - (r.Y * draw.Scale), ["depth"] = r.Z };
    }
    /// <summary>
    /// The <c>Mesh.draw</c> options that stand a character in a frame: feet at <c>at</c> — or the
    /// <c>anchor</c> body part there instead — standing <c>height</c> of the frame tall; see
    /// <c>Character.place</c>.
    /// </summary>
    /// <remarks>
    /// <b>Sized from the character standing, not from the pose.</b> A crouch is shorter than a stance,
    /// and sizing it to fill the height would make one character two sizes in two panels. The feet are
    /// where the character's floor is — the bottom of its bind pose, under its hips — which is where a
    /// retargeted pose keeps them, since the hips move with the clip.
    /// </remarks>
    internal static Dictionary<string, object?> Place(FaceMesh mesh, object frameObj, object? optObj)
    {
        var f = LayoutToolkit.AsRect(frameObj);
        var opt = JsInterop.AsDict(optObj);
        float atX = 0.5f, atY = 0.95f, height = 0.8f, yaw = 0f, pitch = 0f;
        string? anchor = null;
        var atGiven = false;
        if (opt != null)
            foreach (var key in opt.Keys)
            {
                var k = Convert.ToString(key, CultureInfo.InvariantCulture);
                switch (k)
                {
                    case "at":
                        var at = JsInterop.AsDict(opt[key!]) ?? throw new ArgumentException("Character.place: at is a point in the frame, { x, y }, as fractions of it.");
                        if (at.Contains("x")) atX = Convert.ToSingle(at["x"], CultureInfo.InvariantCulture);
                        if (at.Contains("y")) atY = Convert.ToSingle(at["y"], CultureInfo.InvariantCulture);
                        atGiven = true;
                        break;
                    case "anchor":
                        anchor = Convert.ToString(opt[key!], CultureInfo.InvariantCulture);
                        if (anchor == "feet") anchor = null;
                        break;
                    case "height": height = Convert.ToSingle(opt[key!], CultureInfo.InvariantCulture); break;
                    case "yawDeg": yaw = Convert.ToSingle(opt[key!], CultureInfo.InvariantCulture); break;
                    case "pitchDeg": pitch = Convert.ToSingle(opt[key!], CultureInfo.InvariantCulture); break;
                    default: throw new ArgumentException($"Character.place has no option '{k}'. It takes at, anchor, height, yawDeg and pitchDeg.");
                }
            }
        if (!(height > 0f) || !float.IsFinite(height))
            throw new ArgumentException($"Character.place: height is a share of the frame's height and must be above 0; got {height}.");
        if (f.Width <= 0f || f.Height <= 0f)
            throw new ArgumentException("Character.place needs a frame with a width and a height.");

        var bind = mesh.Reference;
        float minY = float.MaxValue, maxY = float.MinValue, cx = 0f, cz = 0f;
        foreach (var v in bind)
        {
            minY = MathF.Min(minY, v.Y);
            maxY = MathF.Max(maxY, v.Y);
            cx += v.X;
            cz += v.Z;
        }
        cx /= bind.Length;
        cz /= bind.Length;
        if (mesh.Rig is { } rig && rig.Aliases.TryGetValue("hips", out var hips) && rig.FileNodes.TryGetValue(hips, out var node))
            (cx, cz) = (node.WorldMatrix.Translation.X, node.WorldMatrix.Translation.Z);

        var point = new SkiaSharp.SKPoint3(cx, minY, cz);
        if (anchor is not null)
        {
            if (mesh.Rig is not { } r2 || r2.Aliases.Count == 0)
                throw new ArgumentException($"Character.place: anchor '{anchor}' needs a character's body-part names; load it with Character.load(name).");
            var at = r2.FileNodes[Handle(r2, anchor)].WorldMatrix.Translation;
            point = new SkiaSharp.SKPoint3(at.X, at.Y, at.Z);
            if (!atGiven) (atX, atY) = (0.5f, 0.3f);
        }

        var scale = height * f.Height / (maxY - minY);
        var options = new Dictionary<string, object?> { ["yawDeg"] = yaw, ["pitchDeg"] = pitch, ["scale"] = scale };
        var r = MeshToolkit.Pose.From(options, mesh).Rotate(point);
        options["x"] = f.X + (atX * f.Width) - (r.X * scale);
        options["y"] = f.Y + (atY * f.Height) + (r.Y * scale);
        return options;
    }
    #endregion

    #region Private
    static MeshRig Rigged(FaceMesh mesh, string call)
    {
        if (mesh.Rig is not { } rig)
            throw new ArgumentException($"Character.{call} needs a rigged character; this mesh carries no skeleton.");
        if (rig.Aliases.Count == 0)
            throw new ArgumentException($"Character.{call} needs a character's body-part names. Load it with Character.load(name).");
        return rig;
    }

    static string Handle(MeshRig rig, string part) =>
        rig.Aliases.TryGetValue(part, out var h) && rig.FileNodes.ContainsKey(h) ? h
        : rig.FileNodes.ContainsKey(part) ? part
        : throw new ArgumentException($"This character has no '{part}'. It has: {string.Join(", ", rig.Aliases.Keys.Order(StringComparer.Ordinal))}.");

    static void CheckKeys(IDictionary goal, string name, params string[] allowed)
    {
        foreach (var k in goal.Keys)
            if (Array.IndexOf(allowed, Convert.ToString(k, CultureInfo.InvariantCulture)) < 0)
                throw new ArgumentException($"The goal for '{name}' has no option '{k}'. It takes {string.Join(", ", allowed)}.");
    }

    /// <summary>The pose's rotations and hip moves, by bone handle, whichever spelling each key used.</summary>
    static Dictionary<string, Quaternion> ReadPose(MeshRig rig, object? poseObj, string call, out Dictionary<string, Vector3> moves)
    {
        var deltas = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
        moves = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        if (JsInterop.AsDict(poseObj) is not { } pose) return deltas;
        foreach (var key in pose.Keys)
        {
            var name = Convert.ToString(key, CultureInfo.InvariantCulture) ?? "";
            var handle = Handle(rig, name);
            var (rotation, move) = MeshRig.ReadJoint(JsInterop.AsDict(pose[key!]), name);
            deltas[handle] = rotation;
            if (move is { } m) moves[handle] = m;
        }
        return deltas;
    }

    /// <summary>
    /// The input pose with only the solved bones rewritten, keyed by body part; every other entry is the
    /// input's own. A solved bone the input named by its handle loses that key, or <c>pose</c> would
    /// apply both.
    /// </summary>
    static Dictionary<string, object?> WritePose(MeshRig rig, object? poseObj, Dictionary<string, Quaternion> deltas, HashSet<string> changed)
    {
        var partOf = rig.Aliases.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key, StringComparer.Ordinal);
        var outp = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (JsInterop.AsDict(poseObj) is { } pose)
            foreach (var key in pose.Keys)
                outp[Convert.ToString(key, CultureInfo.InvariantCulture)!] = pose[key!];

        foreach (var handle in changed)
        {
            var q = deltas[handle];
            var key = partOf.TryGetValue(handle, out var part) ? part : handle;
            if (key != handle) outp.Remove(handle);
            var (yaw, pitch, roll) = PoseRetarget.YawPitchRoll(Quaternion.Normalize(q));
            const float deg = 180f / MathF.PI;
            outp[key] = new Dictionary<string, object?>
            {
                ["xDeg"] = Math.Round(pitch * deg, 3),
                ["yDeg"] = Math.Round(yaw * deg, 3),
                ["zDeg"] = Math.Round(roll * deg, 3)
            };
        }
        return outp;
    }

    /// <summary>Every bone's model-space transform under the given rotations: forward kinematics.</summary>
    /// <remarks>The same composition <c>mesh.pose</c> applies, so a position read here is where the drawn joint is.</remarks>
    static Dictionary<string, Matrix4x4> Forward(MeshRig rig, Dictionary<string, Quaternion> deltas, Dictionary<string, Vector3> moves)
    {
        var world = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        foreach (var handle in rig.JointNames)
        {
            if (!rig.FileNodes.TryGetValue(handle, out var node)) continue;
            var local = deltas.TryGetValue(handle, out var q) ? Matrix4x4.CreateFromQuaternion(q) * node.LocalMatrix : node.LocalMatrix;
            if (moves.TryGetValue(handle, out var by)) local = rig.Moved(local, handle, handle, by);
            var parent = rig.JointParent.TryGetValue(handle, out var p) && world.TryGetValue(p, out var pw)
                ? pw : node.VisualParent?.WorldMatrix ?? Matrix4x4.Identity;
            world[handle] = local * parent;
        }
        return world;
    }

    /// <summary>A goal's point in model space: given there directly, or a page point at <paramref name="depthOf"/>'s depth.</summary>
    static Vector3 Point(IDictionary goal, string key, string name, MeshToolkit.Pose? draw, Vector3 depthOf)
    {
        var p = JsInterop.AsDict(goal[key])
            ?? throw new ArgumentException($"The goal for '{name}': {key} is not a point.");
        float F(string k, bool need) => p.Contains(k) ? Convert.ToSingle(p[k], CultureInfo.InvariantCulture)
            : need ? throw new ArgumentException($"The goal for '{name}': {key} needs {k}.") : float.NaN;

        var page = key == "page" || (key == "lookAt" && !p.Contains("z"));
        if (!page) return new Vector3(F("x", true), F("y", true), F("z", true));

        if (draw is null)
            throw new ArgumentException($"The goal for '{name}' is a page point, so Character.reach needs the draw options you pass to Mesh.draw.");
        var r0 = draw.Rotate(new SkiaSharp.SKPoint3(depthOf.X * draw.StretchX, depthOf.Y * draw.StretchY, depthOf.Z * draw.StretchZ));
        var m = draw.Unrotate(new SkiaSharp.SKPoint3((F("x", true) - draw.X) / draw.Scale, (draw.Y - F("y", true)) / draw.Scale, r0.Z));
        return new Vector3(m.X / draw.StretchX, m.Y / draw.StretchY, m.Z / draw.StretchZ);
    }

    static Vector3 Bend(object? spec, string name, Vector3 forward, Vector3 up, Vector3 side)
    {
        if (spec is string word)
            return word switch
            {
                "back" => -forward,
                "forward" => forward,
                "out" => side,
                "in" => -side,
                "up" => up,
                "down" => -up,
                _ => throw new ArgumentException($"The goal for '{name}': bend '{word}' is not a direction. Use {string.Join(", ", Bends)}, or { "{ x, y, z }" }.")
            };
        if (JsInterop.AsDict(spec) is { } p)
            return new Vector3(Convert.ToSingle(p["x"], CultureInfo.InvariantCulture), Convert.ToSingle(p["y"], CultureInfo.InvariantCulture),
                               Convert.ToSingle(p["z"], CultureInfo.InvariantCulture));
        throw new ArgumentException($"The goal for '{name}': bend is a word ({string.Join(", ", Bends)}) or a direction {{ x, y, z }}.");
    }

    /// <summary>The way the middle joint already bends, or the limb's natural way when it is straight.</summary>
    static Vector3 CurrentBend(Vector3 a, Vector3 b, Vector3 c, Vector3 natural)
    {
        var axis = c - a;
        if (axis.LengthSquared() < 1e-10f) return natural;
        axis = Vector3.Normalize(axis);
        var off = (b - a) - (Vector3.Dot(b - a, axis) * axis);
        return off.Length() > 1e-3f * Vector3.Distance(a, c) ? off : natural;
    }

    /// <summary>Where the middle joint and the end go: the exact two-bone solution, clamped to reach.</summary>
    static (Vector3 Mid, Vector3 End, bool Reached) Solve(Vector3 a, Vector3 b, Vector3 c, Vector3 t, Vector3 pole)
    {
        float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c);
        var toT = t - a;
        var d = toT.Length();
        var dir = d > 1e-8f ? toT / d : Vector3.Normalize(c - a);
        var min = MathF.Abs(l1 - l2) * 1.0001f;
        var max = (l1 + l2) * 0.9999f;
        var reached = d >= min && d <= max;
        d = Math.Clamp(d, min, max);

        var u = pole - (Vector3.Dot(pole, dir) * dir);
        if (u.LengthSquared() < 1e-10f)
            u = Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
        u = Vector3.Normalize(u);

        var cosA = Math.Clamp(((l1 * l1) + (d * d) - (l2 * l2)) / (2f * l1 * d), -1f, 1f);
        var mid = a + (l1 * ((cosA * dir) + (MathF.Sqrt(1f - (cosA * cosA)) * u)));
        return (mid, a + (dir * d), reached);
    }

    /// <summary>The new local rotations for the chain's first two bones, by the smallest swings.</summary>
    static (Quaternion Root, Quaternion Mid) Swing(MeshRig rig, Dictionary<string, Matrix4x4> world,
        Dictionary<string, Quaternion> deltas, string root, string mid,
        Vector3 a, Vector3 b, Vector3 c, Vector3 b2, Vector3 c2)
    {
        var qRoot = Rot(world[root]);
        var qMid = Rot(world[mid]);
        var r = FromTo(b - a, b2 - a);
        var newRoot = Quaternion.Normalize(r * qRoot);
        var carried = Quaternion.Normalize(r * qMid);
        var newMid = Quaternion.Normalize(FromTo(Vector3.Transform(c - b, r), c2 - b2) * carried);

        var parentOfRoot = rig.JointParent.TryGetValue(root, out var p) && world.TryGetValue(p, out var pw)
            ? Rot(pw) : Rot(rig.FileNodes[root].VisualParent?.WorldMatrix ?? Matrix4x4.Identity);
        return (Delta(rig, root, parentOfRoot, newRoot), Delta(rig, mid, newRoot, newMid));
    }

    /// <summary>Turns the head so its face points at the target, at most <see cref="MaxLookDeg"/>.</summary>
    static (Quaternion Delta, bool Full) LookAt(MeshRig rig, Dictionary<string, Matrix4x4> world, string head, Vector3 target, Vector3 forward)
    {
        var node = rig.FileNodes[head];
        var faceLocal = Vector3.Transform(forward, Quaternion.Inverse(Rot(node.WorldMatrix)));
        var q = Rot(world[head]);
        var want = target - world[head].Translation;
        var turn = FromTo(Vector3.Transform(faceLocal, q), want);
        var angle = 2f * MathF.Acos(Math.Clamp(MathF.Abs(turn.W), 0f, 1f)) * 180f / MathF.PI;
        var full = angle <= MaxLookDeg;
        if (!full) turn = Quaternion.Slerp(Quaternion.Identity, turn, MaxLookDeg / angle);

        var parent = rig.JointParent.TryGetValue(head, out var p) && world.TryGetValue(p, out var pw)
            ? Rot(pw) : Rot(node.VisualParent?.WorldMatrix ?? Matrix4x4.Identity);
        return (Delta(rig, head, parent, Quaternion.Normalize(turn * q)), full);
    }

    /// <summary>The <c>pose</c> rotation that gives a bone this world rotation under this parent.</summary>
    static Quaternion Delta(MeshRig rig, string handle, Quaternion parentWorld, Quaternion world)
    {
        var local = Quaternion.Normalize(Quaternion.Inverse(parentWorld) * world);
        return Quaternion.Normalize(Quaternion.Inverse(Rot(rig.FileNodes[handle].LocalMatrix)) * local);
    }

    static Quaternion Rot(Matrix4x4 m)
    {
        Matrix4x4.Decompose(m, out _, out var q, out _);
        return Quaternion.Normalize(q);
    }

    static Quaternion FromTo(Vector3 a, Vector3 b)
    {
        a = Vector3.Normalize(a);
        b = Vector3.Normalize(b);
        var d = Vector3.Dot(a, b);
        if (d > 0.999999f) return Quaternion.Identity;
        if (d < -0.999999f)
        {
            var ortho = Vector3.Cross(a, MathF.Abs(a.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(ortho), MathF.PI);
        }
        return Quaternion.Normalize(new Quaternion(Vector3.Cross(a, b), 1f + d));
    }
    #endregion
}
