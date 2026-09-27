namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static PoseRetarget;

/// <summary>
/// Poses a rigged character from the 3D body landmarks MediaPipe finds in a picture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> A script reaches this through <c>Character.retarget(character, detection)</c>.
/// </para>
/// <para>
/// <b>Points, not rotations.</b> A clip gives each bone a rotation; landmarks give only where joints are. So each
/// part is turned until a direction on it matches the picture's and then rolled until a second one does:
/// </para>
/// <list type="bullet">
/// <item>The <b>trunk</b> takes its up from hips to shoulders and its left from hip to hip and shoulder to
/// shoulder; the spine is halfway between the hips and the chest.</item>
/// <item>The <b>head</b> takes its forward from the ears to the nose and its left from ear to ear; the neck is
/// halfway between the chest and the head.</item>
/// <item>A <b>limb</b> is swung onto its joint-to-joint direction and rolled by its bend: an arm by where the
/// forearm folds, a leg by where the shin folds, or by the foot when the knee is nearly straight.</item>
/// <item>A <b>hand</b> points at its knuckles and rolls by the line from little finger to index; a <b>foot</b>
/// points from heel to toe.</item>
/// </list>
/// <para>
/// A limb whose landmarks the model was unsure of is left as its parent carries it. The hips then drop until the
/// lower foot is back on the floor, so a crouch or a kneel stays grounded.
/// </para>
/// </remarks>
internal static class LandmarkRetarget
{
    #region Methods
    /// <summary>The pose, keyed by body part, that puts <paramref name="mesh"/> in the detected body's pose.</summary>
    /// <param name="faceFront">Turn the result so the hips face the character's front, rather than keeping the picture's angle.</param>
    internal static Dictionary<string, object?> Retarget(FaceMesh mesh, BodyDetection body, bool faceFront, bool moveHips, bool flatFeet = true)
    {
        var rig = mesh.Rig ?? throw new ArgumentException("Character.retarget needs a rigged character.");
        if (!body.Found) throw new ArgumentException($"No body was found in the picture, so there is no pose to take: {body.Reason}");
        if (!body.HasWorld)
            throw new ArgumentException("This detection carries no 3D landmarks; the body backend predates them. Update src/vision/pose_landmarks.py.");

        Vector3 R(string part) => rig.Aliases.TryGetValue(part, out var h) && rig.FileNodes.TryGetValue(h, out var n)
            ? n.WorldMatrix.Translation
            : throw new ArgumentException($"Character.retarget needs '{part}' named on the rig; read mesh.jointMap.");
        bool Has(string part) => rig.Aliases.TryGetValue(part, out var h) && rig.FileNodes.ContainsKey(h);

        // The rest body's frame, from the rig itself, so it holds whichever way the model was built facing.
        // Up from the middle of the hip joints to the middle of the shoulders, which is what the picture's
        // landmarks measure; the rig's own pelvis and neck joints sit elsewhere, and would tilt the trunk.
        var restUp = Vector3.Normalize((R("leftUpperArm") + R("rightUpperArm")) / 2f - (R("leftThigh") + R("rightThigh")) / 2f);
        var restLeft = Vector3.Normalize(R("leftUpperArm") - R("rightUpperArm"));
        var restLeftHips = Vector3.Normalize(R("leftThigh") - R("rightThigh"));
        var restForward = Vector3.Normalize(Vector3.Cross(restLeft, restUp));

        // MediaPipe's frame is the camera's — x right, y down, z away — turned into ours: y up, z toward the viewer.
        // Then turned so the picture's forward is the character's front: a subject facing the camera faces the way
        // the character does at yawDeg 0, as a clip's performer does.
        var src = Ours(body.World3);
        Vector3 S(string n) => src[n];
        Vector3 Mid(string a, string b) => (S(a) + S(b)) / 2f;

        var sUp0 = Mid("leftShoulder", "rightShoulder") - Mid("leftHip", "rightHip");
        var sForward0 = Vector3.Cross(S("leftHip") - S("rightHip"), sUp0);
        var turn = faceFront ? Yaw(Flat(sForward0, Vector3.UnitY), Vector3.UnitZ) : Quaternion.Identity;
        turn = Yaw(Vector3.UnitZ, Flat(restForward, Vector3.UnitY)) * turn;
        foreach (var k in src.Keys.ToList()) src[k] = Vector3.Transform(src[k], turn);

        bool Sure(params string[] names) =>
            names.All(n => !body.Image.TryGetValue(n, out var p) || p.V >= Trust);

        // Trunk and head are measured against the rig's own rest frame. The picture's depth, along the camera's
        // line, is the weak part: measured on renders with a known pose, a lean toward the camera read at a
        // third of its size, and an upright figure read as leaning 1 to 25 degrees toward the camera depending on
        // the picture. That varies, so no fixed correction is applied.
        var sUp = Mid("leftShoulder", "rightShoulder") - Mid("leftHip", "rightHip");
        var hips = Frame(restUp, restLeftHips, sUp, S("leftHip") - S("rightHip"));
        var chest = Frame(restUp, restLeft, sUp, S("leftShoulder") - S("rightShoulder"));
        var head = Frame(restForward, restLeft, S("nose") - Mid("leftEar", "rightEar"), S("leftEar") - S("rightEar"));
        if (!Sure("nose", "leftEar", "rightEar")) head = chest;

        // Trunk and head: the rest orientation turned by the picture's frame, so a rig's own spine curve is kept.
        var whole = new Dictionary<string, Quaternion>(StringComparer.Ordinal)
        {
            ["hips"] = hips,
            ["spine"] = Quaternion.Slerp(hips, chest, 0.5f),
            ["chest"] = chest,
            ["neck"] = Quaternion.Slerp(chest, head, 0.5f),
            ["head"] = head,
        };

        // Limbs: aimed along the joint-to-joint direction, then rolled so a reference on the bone matches the
        // picture's. Each reference is where the bone's front points: body forward in the rest pose.
        // Which feet are on the floor: the lower one, and both when the ankles are level to within a tenth of a leg.
        float Leg(string side) => Vector3.Distance(S(side + "Hip"), S(side + "Knee")) + Vector3.Distance(S(side + "Knee"), S(side + "Ankle"));
        var level = MathF.Abs(S("leftAnkle").Y - S("rightAnkle").Y) < 0.1f * (Leg("left") + Leg("right")) / 2f;
        bool Planted(string side) => level || S(side + "Ankle").Y <= S((side == "left" ? "right" : "left") + "Ankle").Y;

        var aims = new Dictionary<string, (Vector3 Dir, Vector3? Ref)>(StringComparer.Ordinal);
        foreach (var side in new[] { "left", "right" })
        {
            string L(string n) => side + n;
            Vector3 sh = S(L("Shoulder")), el = S(L("Elbow")), wr = S(L("Wrist"));
            Vector3 hp = S(L("Hip")), kn = S(L("Knee")), an = S(L("Ankle"));
            Vector3 heel = S(L("Heel")), toe = S(L("FootIndex"));
            var knuckles = Mid(L("Index"), L("Pinky"));
            var across = S(L("Index")) - S(L("Pinky"));
            var bodyForward = Vector3.Transform(restForward, chest);

            if (Sure(L("Shoulder"), L("Elbow"), L("Wrist")))
            {
                // An arm folds forward at the elbow, so the forearm says where the upper arm's front is.
                aims[L("UpperArm")] = (el - sh, Bent(el - sh, wr - el) ? wr - el : bodyForward);
                aims[L("Forearm")] = (wr - el, Sure(L("Index"), L("Pinky")) ? across : null);
                if (Sure(L("Index"), L("Pinky"))) aims[L("Hand")] = (knuckles - wr, across);
            }
            if (Sure(L("Hip"), L("Knee"), L("Ankle")))
            {
                // A leg folds backward at the knee, so the kneecap points away from the shin; nearly straight, the
                // knee points where the toes do.
                var feet = Sure(L("Heel"), L("FootIndex")) ? toe - heel : bodyForward;
                aims[L("Thigh")] = (kn - hp, Bent(kn - hp, an - kn) ? kn - an : feet);
                aims[L("Shin")] = (an - kn, feet);
                // A planted foot is laid flat. The detector reads feet flat on the floor as pointing 6 to 50 degrees
                // toes-down, floor drawn or not; how far a foot really points down is not in the picture it reads.
                var sole = flatFeet && Planted(side) && Flat(toe - heel, Vector3.UnitY) is { } f && f != Vector3.Zero ? f : toe - heel;
                if (Sure(L("Heel"), L("FootIndex"))) aims[L("Foot")] = (sole, flatFeet && Planted(side) ? Vector3.UnitY : kn - an);
            }
        }

        var restPoint = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var part in rig.Aliases.Keys) if (Has(part)) restPoint[part] = R(part);

        // The rest direction each aimed part is swung from, and its rest reference.
        (Vector3 Dir, Vector3 Ref) Rest(string part) => part switch
        {
            _ when part.EndsWith("UpperArm", StringComparison.Ordinal) => (Toward(part, "Forearm"), restForward),
            _ when part.EndsWith("Forearm", StringComparison.Ordinal) => (Toward(part, "Hand"), restForward),
            _ when part.EndsWith("Hand", StringComparison.Ordinal) => (restPoint[part] - restPoint[part.Replace("Hand", "Forearm")], restForward),
            _ when part.EndsWith("Thigh", StringComparison.Ordinal) => (Toward(part, "Shin"), restForward),
            _ when part.EndsWith("Shin", StringComparison.Ordinal) => (Toward(part, "Foot"), restForward),
            _ => (restForward, restUp)   // a foot points forward and its top faces up
        };
        Vector3 Toward(string part, string next)
        {
            var side = part.StartsWith("left", StringComparison.Ordinal) ? "left" : "right";
            return restPoint[side + next] - restPoint[part];
        }

        var byHandle = rig.Aliases.Where(kv => rig.FileNodes.ContainsKey(kv.Value)).ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
        var animWorld = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
        var pose = new Dictionary<string, object?>(StringComparer.Ordinal);
        const float deg = 180f / MathF.PI;

        foreach (var handle in rig.JointNames)
        {
            if (!rig.FileNodes.TryGetValue(handle, out var node)) continue;
            var restLocal = Rot(node.LocalMatrix);
            var parentAnim = rig.JointParent.TryGetValue(handle, out var p) && animWorld.TryGetValue(p, out var pa)
                ? pa : Rot(node.VisualParent?.WorldMatrix ?? Matrix4x4.Identity);
            var current = Quaternion.Normalize(parentAnim * restLocal);

            Quaternion world;
            var tRest = Rot(node.WorldMatrix);
            if (!byHandle.TryGetValue(handle, out var part)) { animWorld[handle] = current; continue; }
            if (whole.TryGetValue(part, out var turnBy)) world = Quaternion.Normalize(turnBy * tRest);
            else if (aims.TryGetValue(part, out var aim))
            {
                // Rest vectors carried into this bone's frame, then through the parent's new pose.
                var (restDir, restRef) = Rest(part);
                var dir = Vector3.Transform(Vector3.Transform(restDir, Quaternion.Inverse(tRest)), current);
                var swung = Quaternion.Normalize(FromTo(dir, aim.Dir) * current);
                world = swung;
                if (aim.Ref is { } sRef)
                {
                    var goal = Vector3.Normalize(aim.Dir);
                    var tRef = Flat(Vector3.Transform(Vector3.Transform(restRef, Quaternion.Inverse(tRest)), swung), goal);
                    var s = Flat(sRef, goal);
                    if (tRef != Vector3.Zero && s != Vector3.Zero) world = Quaternion.Normalize(FromTo(tRef, s) * swung);
                }
            }
            else { animWorld[handle] = current; continue; }

            animWorld[handle] = world;
            var local = Quaternion.Normalize(Quaternion.Inverse(parentAnim) * world);
            var (yaw, pitch, roll) = YawPitchRoll(Quaternion.Normalize(Quaternion.Inverse(restLocal) * local));
            pose[part] = new Dictionary<string, object?>
            {
                ["xDeg"] = MathF.Round(pitch * deg, 2),
                ["yDeg"] = MathF.Round(yaw * deg, 2),
                ["zDeg"] = MathF.Round(roll * deg, 2)
            };
        }

        // Put the lower foot back on the floor: the picture has no floor, and a bent leg lifts the feet.
        if (moveHips && rig.Aliases.TryGetValue("hips", out var hipsHandle) && rig.CanMove(hipsHandle)
            && pose.TryGetValue("hips", out var hipsEntry) && hipsEntry is Dictionary<string, object?> hipsPose)
        {
            float Y(object? posed, string foot) => Convert.ToSingle(CharacterIk.Where(mesh, posed, foot, null)["y"]);
            var rest = MathF.Min(Y(null, "leftFoot"), Y(null, "rightFoot"));
            var now = MathF.Min(Y(pose, "leftFoot"), Y(pose, "rightFoot"));
            hipsPose["move"] = new Dictionary<string, object?> { ["x"] = 0.0, ["y"] = Math.Round(rest - now, 5), ["z"] = 0.0 };

            // Both feet down in the picture, both feet down here. The legs' depth is the least reliable part of the
            // reading, and a second foot left hanging makes the whole figure look as if it is falling; so when the
            // picture's ankles are level to within a tenth of a leg, the higher foot is put on the floor by reach,
            // where it already is across the floor, keeping the knee bent the way it was.
            if (level)
            {
                var higher = Y(pose, "leftFoot") > Y(pose, "rightFoot") ? "leftFoot" : "rightFoot";
                var at = CharacterIk.Where(mesh, pose, higher, null);
                var goal = new Dictionary<string, object?> { ["x"] = at["x"], ["y"] = rest, ["z"] = at["z"] };
                var planted = CharacterIk.Reach(mesh, pose, new Dictionary<string, object?>
                    { [higher] = new Dictionary<string, object?> { ["at"] = goal } }, null);
                pose = (Dictionary<string, object?>)planted["pose"]!;
            }
        }

        return pose;
    }
    #endregion

    #region Private
    /// <summary>MediaPipe's 3D landmarks in our frame: x right, y down, z away from the camera becomes y up, z toward it.</summary>
    static Dictionary<string, Vector3> Ours(IReadOnlyDictionary<string, Vector3> world) =>
        world.ToDictionary(kv => kv.Key, kv => new Vector3(kv.Value.X, -kv.Value.Y, -kv.Value.Z), StringComparer.Ordinal);

    /// <summary>
    /// The visibility under which a limb's landmarks are not used: next to none. Measured on a round trip, the model's 3D guess
    /// for an arm it marks unsure is still nearer the truth than leaving the arm at rest (24 against 32 degrees).
    /// </summary>
    const float Trust = 0.05f;

    /// <summary>The rotation turning one frame, given by a direction and a second one, onto another.</summary>
    static Quaternion Frame(Vector3 fromDir, Vector3 fromRef, Vector3 toDir, Vector3 toRef)
    {
        var swing = FromTo(fromDir, toDir);
        var goal = Vector3.Normalize(toDir);
        var a = Flat(Vector3.Transform(fromRef, swing), goal);
        var b = Flat(toRef, goal);
        if (a == Vector3.Zero || b == Vector3.Zero) return swing;
        var twist = Vector3.Dot(a, b) < -0.99999f ? Quaternion.CreateFromAxisAngle(goal, MathF.PI) : FromTo(a, b);
        return Quaternion.Normalize(twist * swing);
    }

    /// <summary>A turn about the vertical taking one horizontal direction onto another.</summary>
    static Quaternion Yaw(Vector3 from, Vector3 to)
    {
        if (from == Vector3.Zero || to == Vector3.Zero) return Quaternion.Identity;
        var angle = MathF.Atan2(to.X, to.Z) - MathF.Atan2(from.X, from.Z);
        return Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
    }

    /// <summary>Whether a joint is bent enough for the fold to say which way the bone faces.</summary>
    static bool Bent(Vector3 upper, Vector3 lower) =>
        Vector3.Dot(Vector3.Normalize(upper), Vector3.Normalize(lower)) < MathF.Cos(20f * MathF.PI / 180f);
    #endregion
}
