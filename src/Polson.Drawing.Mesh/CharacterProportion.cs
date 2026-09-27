namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

/// <summary>
/// Measures a character's proportions, and reshapes a rigged body to new ones.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> A script reaches this through <c>mesh.proportion(...)</c> and
/// <c>Character.proportions(...)</c>.
/// </para>
/// <para>
/// <b>Why it exists.</b> One stock rigged body can then stand in for characters of different builds, as a pose
/// guide or a figure to draw over. Measured on Kit, the image model took her body proportions from her sheet
/// whichever guide it was given, so for a build near the stock body's this matters little.
/// </para>
/// </remarks>
internal static class CharacterProportion
{
    #region Methods
    /// <summary>
    /// A character's proportions, each a share of its standing height: the lengths of its segments, its
    /// shoulder and hip width, and the head from its joint to the top of the mesh.
    /// </summary>
    internal static Dictionary<string, float> Measure(FaceMesh mesh)
    {
        var rig = Rigged(mesh, "Character.proportions");
        var p = Joints(rig, "Character.proportions");
        var verts = mesh.Attachment?.BodyBind ?? mesh.Reference;
        float floor = verts.Min(v => v.Y), top = verts.Max(v => v.Y), height = top - floor;
        if (height <= 0f) throw new ArgumentException("This character has no height to measure.");

        float Len(string a, string b) => Vector3.Distance(p[a], p[b]);
        float Both(string a, string b) => (Len("left" + a, "left" + b) + Len("right" + a, "right" + b)) / 2f;

        var raw = new Dictionary<string, float>
        {
            ["torso"] = Len("hips", "neck"),
            ["neck"] = Len("neck", "head"),
            ["head"] = top - p["head"].Y,
            ["upperArm"] = Both("UpperArm", "Forearm"),
            ["forearm"] = Both("Forearm", "Hand"),
            ["thigh"] = Both("Thigh", "Shin"),
            ["shin"] = Both("Shin", "Foot"),
            ["shoulders"] = MathF.Abs(p["leftUpperArm"].X - p["rightUpperArm"].X),
            ["hips"] = MathF.Abs(p["leftThigh"].X - p["rightThigh"].X),
        };
        return raw.ToDictionary(kv => kv.Key, kv => kv.Value / height);
    }

    /// <summary>The body reshaped by <paramref name="spec"/>, as a new mesh whose bind pose is the new shape.</summary>
    internal static FaceMesh Apply(FaceMesh mesh, IDictionary? spec)
    {
        var rig = Rigged(mesh, "proportion");
        if (mesh.Attachment is not null)
            throw new ArgumentException(
                "proportion reshapes a body before a face is transplanted onto it; this mesh already wears one. " +
                "Proportion the body first, then call withFaceMesh.");
        var f = Factors(mesh, spec);
        var p = Joints(rig, "proportion");

        // Which body part each bone belongs to: the nearest named bone at or above it.
        var partOf = rig.Aliases.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
        string? Owner(string h)
        {
            for (string? at = h; at is not null; at = rig.JointParent.GetValueOrDefault(at))
                if (partOf.TryGetValue(at, out var part)) return part;
            return null;
        }

        // And the named bone each unnamed one leads to, so a clavicle counts toward the shoulder.
        var children = rig.JointNames.Where(rig.JointParent.ContainsKey).ToLookup(h => rig.JointParent[h]);
        string? Toward(string h)
        {
            var queue = new Queue<string>([h]);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                if (partOf.TryGetValue(at, out var part)) return part;
                foreach (var c in children[at]) queue.Enqueue(c);
            }
            return null;
        }

        var bind = rig.JointNames.ToDictionary(h => h, h => rig.JointBind.TryGetValue(h, out var b) ? V(b) : Vector3.Zero, StringComparer.Ordinal);
        FaceMesh Build()
        {
            var moved = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            foreach (var h in rig.JointNames)
            {
                if (!rig.JointParent.TryGetValue(h, out var parent)) { moved[h] = bind[h]; continue; }
                var offset = bind[h] - bind[parent];
                moved[h] = moved[parent] + Offset(offset, Owner(parent), Toward(h), f);
            }

            // Each bone carries its vertices with a linear map about its joint: a limb stretches along itself
            // and thins across; the trunk scales its height, width and depth; a head, hand or foot scales whole.
            var maps = rig.JointNames.ToDictionary(h => h, h => Linear(Owner(h), p, f), StringComparer.Ordinal);
            Vector3 Carry(string h, Vector3 v) => Vector3.Transform(v - bind[h], maps[h]) + moved[h];
            return rig.Rebind(moved, Carry).Snapshot(bind: null);
        }

        var result = Build();
        return mesh.Texture is { } tex && !ReferenceEquals(tex, result.Texture)
            ? new FaceMesh(result.Vertices, result.Uvs, result.Indices, result.HasUvs, result.Source)
            { Texture = tex, Rig = result.Rig, Reference = result.Reference }
            : result;
    }
    #endregion

    #region Private
    static MeshRig Rigged(FaceMesh mesh, string call)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Rig is not { Skinned: true } rig)
            throw new ArgumentException($"{call} needs a rigged body; mesh '{mesh.Source}' carries no skeleton.");
        return rig;
    }

    /// <summary>Bind positions of the body parts proportions are measured between, refusing a rig without them.</summary>
    static Dictionary<string, Vector3> Joints(MeshRig rig, string call)
    {
        var missing = Required.Where(part => !rig.Aliases.TryGetValue(part, out var h) || !rig.JointBind.ContainsKey(h)).ToArray();
        if (missing.Length > 0)
            throw new ArgumentException(
                $"{call} needs the body parts named on the rig (mesh.jointMap); missing: {string.Join(", ", missing)}. " +
                "A character from Character.load has them.");
        return Required.ToDictionary(part => part, part => V(rig.JointBind[rig.Aliases[part]]), StringComparer.Ordinal);
    }

    /// <summary>The factors to apply, from <c>like</c> and then any named ones over it.</summary>
    /// <remarks>
    /// <b>With <c>like</c>, lengths are compared as shares of the head-to-ankle chain, not of standing height.</b>
    /// Riggers differ on how far the pelvis joint sits above the hips and the ankle above the floor, so shares of
    /// height cannot all be matched at once: the gaps the chain skips were 3% of the stock body's height and 9% of
    /// a UniRig character's. The chain is what the factors act on, so matching it is exact in one pass.
    /// </remarks>
    static Dictionary<string, float> Factors(FaceMesh mesh, IDictionary? spec)
    {
        var f = Segments.ToDictionary(s => s, _ => 1f, StringComparer.Ordinal);
        if (spec is null) return f;

        if (spec.Contains("like"))
        {
            if (spec["like"] is not FaceMesh like)
                throw new ArgumentException("proportion's 'like' is a character: Character.load(name), or a mesh with a jointMap.");
            var want = Measure(like);
            var have = Measure(mesh);
            float want0 = Chain(want), have0 = Chain(have);
            foreach (var (k, v) in want) f[k] = (v / want0) / (have[k] / have0);
        }

        foreach (var key in spec.Keys)
        {
            var name = Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
            if (name == "like") continue;
            if (!f.ContainsKey(name))
                throw new ArgumentException(
                    $"proportion has no '{name}'. It takes like, and factors for: {string.Join(", ", Segments)}.");
            var v = Convert.ToSingle(spec[key], CultureInfo.InvariantCulture);
            if (!float.IsFinite(v) || v < 0.25f || v > 4f)
                throw new ArgumentException($"proportion '{name}' is a factor on the body's own size, 0.25 to 4; got {v}.");
            f[name] = v;
        }
        return f;
    }

    /// <summary>A bone's offset from its parent under the new proportions.</summary>
    static Vector3 Offset(Vector3 o, string? owner, string? toward, Dictionary<string, float> f)
    {
        if (owner is "head") return o * f["head"];
        if (Hand(owner)) return o * f["hands"];
        if (Foot(owner)) return o * f["feet"];

        if (toward is not null && toward.EndsWith("UpperArm", StringComparison.Ordinal))
            return new Vector3(o.X * f["shoulders"], o.Y * f["torso"], o.Z * f["torso"]);
        if (toward is not null && toward.EndsWith("Thigh", StringComparison.Ordinal))
            return new Vector3(o.X * f["hips"], o.Y * f["torso"], o.Z * f["torso"]);
        return o * Length(owner, f);
    }

    /// <summary>The length factor of the segment leaving a body part.</summary>
    static float Length(string? part, Dictionary<string, float> f) => part switch
    {
        "hips" or "spine" or "chest" => f["torso"],
        "neck" => f["neck"],
        _ when part?.EndsWith("UpperArm", StringComparison.Ordinal) == true => f["upperArm"],
        _ when part?.EndsWith("Forearm", StringComparison.Ordinal) == true => f["forearm"],
        _ when part?.EndsWith("Thigh", StringComparison.Ordinal) == true => f["thigh"],
        _ when part?.EndsWith("Shin", StringComparison.Ordinal) == true => f["shin"],
        _ => 1f
    };

    /// <summary>How a bone of this body part carries its vertices, about its own joint.</summary>
    static Matrix4x4 Linear(string? part, Dictionary<string, Vector3> p, Dictionary<string, float> f)
    {
        if (part is "head") return Matrix4x4.CreateScale(f["head"]);
        if (Hand(part)) return Matrix4x4.CreateScale(f["hands"]);
        if (Foot(part)) return Matrix4x4.CreateScale(f["feet"]);
        if (part is "hips") return Matrix4x4.CreateScale(f["hips"], f["torso"], f["girth"]);
        if (part is "spine" or "chest") return Matrix4x4.CreateScale(f["shoulders"], f["torso"], f["girth"]);

        var next = part switch
        {
            "neck" => "head",
            _ when part?.EndsWith("UpperArm", StringComparison.Ordinal) == true => part[..^"UpperArm".Length] + "Forearm",
            _ when part?.EndsWith("Forearm", StringComparison.Ordinal) == true => part[..^"Forearm".Length] + "Hand",
            _ when part?.EndsWith("Thigh", StringComparison.Ordinal) == true => part[..^"Thigh".Length] + "Shin",
            _ when part?.EndsWith("Shin", StringComparison.Ordinal) == true => part[..^"Shin".Length] + "Foot",
            _ => null
        };
        if (part is null || next is null) return Matrix4x4.Identity;

        // Stretch along the bone, thin across it: L·uuᵀ + g·(I − uuᵀ).
        var u = Vector3.Normalize(p[next] - p[part]);
        float l = Length(part, f), g = f["girth"];
        Matrix4x4 m = Matrix4x4.Identity;
        m.M11 = g + ((l - g) * u.X * u.X); m.M12 = (l - g) * u.X * u.Y; m.M13 = (l - g) * u.X * u.Z;
        m.M21 = (l - g) * u.Y * u.X; m.M22 = g + ((l - g) * u.Y * u.Y); m.M23 = (l - g) * u.Y * u.Z;
        m.M31 = (l - g) * u.Z * u.X; m.M32 = (l - g) * u.Z * u.Y; m.M33 = g + ((l - g) * u.Z * u.Z);
        return m;
    }

    /// <summary>The vertical chain lengths are compared along: torso, neck, head, thigh and shin.</summary>
    internal static float Chain(Dictionary<string, float> m) => m["torso"] + m["neck"] + m["head"] + m["thigh"] + m["shin"];

    static bool Hand(string? part) => part is "leftHand" or "rightHand";

    static bool Foot(string? part) => part is "leftFoot" or "rightFoot";

    static Vector3 V(SkiaSharp.SKPoint3 p) => new(p.X, p.Y, p.Z);

    /// <summary>The factors <c>proportion</c> takes, besides <c>like</c>.</summary>
    internal static readonly string[] Segments =
        ["torso", "neck", "head", "upperArm", "forearm", "thigh", "shin", "shoulders", "hips", "hands", "feet", "girth"];

    static readonly string[] Required =
    [
        "hips", "neck", "head",
        "leftUpperArm", "leftForearm", "leftHand", "leftThigh", "leftShin", "leftFoot",
        "rightUpperArm", "rightForearm", "rightHand", "rightThigh", "rightShin", "rightFoot"
    ];
    #endregion
}
