namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

/// <summary>
/// <b>Prototype.</b> Skin weights for a mesh and a skeleton, by distance: Mesh2Motion's solver, ported.
/// </summary>
/// <remarks>
/// <para>
/// Ported from Mesh2Motion's <c>src/lib/solvers/</c> (<c>WeightCalculator</c>, <c>ExtremityWeightCorrector</c>,
/// <c>ArmWeightCorrector</c>, <c>WeightSmoother</c>, <c>BoneClassifier</c>, <c>WeightNormalizer</c>), under its MIT
/// licence:
/// </para>
/// <code>
/// MIT License
///
/// Copyright (c) 2025 Scott Petrovic
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
///
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.
/// </code>
/// <para>
/// <b>The pipeline, as theirs:</b> every vertex goes wholly to the bone whose midpoint (joint to first child) is
/// nearest; vertices behind a finger's, toe's or hand's start joint go back to its parent; optionally, arm bones
/// give up vertices inboard of the shoulders; then the seams between bones are blended, three rings wide on the
/// torso, toward the child only on limbs, and not at all between two extremity bones.
/// </para>
/// <para>
/// <b>Three deliberate departures</b>, each where the original assumes something our meshes break.
/// <i>The pelvis rule</i>: theirs compares a distance with an absolute height, which works only with the floor at
/// y = 0; ours sit with the floor at -0.5, so this implements what its comment says it means — the pelvis takes no
/// vertex below the crotch, found by a ray down from the pelvis. <i>The arm plane</i> is measured from the body's
/// midline rather than from x = 0. <i>Normalising</i> divides by the sum where theirs adds a third of the shortfall
/// to each slot; the two agree wherever the smoother's blends already sum to one, which is everywhere it writes.
/// Their optional head correction, for chibi figures, is not ported.
/// </para>
/// </remarks>
internal static class SkinWeights
{
    #region Types
    /// <summary>One skeleton bone as the solver sees it. The first child is the one its midpoint is measured to.</summary>
    internal sealed record Bone(string Name, string? Parent, Vector3 At, string? FirstChild, bool HasChildren);

    enum Category { Torso, Limb, Extremity, Root, Other }

    enum Smoothing { Torso, Limb, Extremity, Standard }

    readonly record struct Pair(int A, int B, int BoneA, int BoneB, Smoothing Kind);
    #endregion

    #region Methods
    /// <summary>
    /// Four joint indices and four weights per vertex, indices into <paramref name="bones"/>. Slot index 0 is the
    /// empty marker with weight 0, as in the original, so <paramref name="bones"/>[0] must be the root.
    /// </summary>
    internal static (int[] Joints, float[] Weights) Solve(IReadOnlyList<Bone> bones, IReadOnlyList<Vector3> positions,
        IReadOnlyList<(int A, int B, int C)> triangles, bool armPlane = true, float armPlaneOffset = 0f, int diffuse = 0, bool coat = false, List<string>? report = null,
        Action<int[], float[]>? garment = null)
    {
        var n = positions.Count;
        var joints = new int[n * 4];
        var weights = new float[n * 4];
        var index = bones.Select((b, i) => (b.Name, i)).ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);
        var category = bones.Select(Classify).ToArray();
        var at = bones.Select(b => b.At).ToArray();
        var mid = bones.Select(b => b.FirstChild is { } c && index.TryGetValue(c, out var ci) ? Vector3.Lerp(b.At, at[ci], 0.5f) : b.At).ToArray();
        var skipped = bones.Select(b => b.Name == "root" || IsLeaf(b) || b.Name.StartsWith("skirt_", StringComparison.Ordinal)).ToArray();

        // 1. Nearest midpoint. The pelvis takes nothing below the crotch, so a leg is never nearer the hips.
        var pelvis = bones.ToList().FindIndex(b => b.Name.Contains("pelvis", StringComparison.OrdinalIgnoreCase)
                                                   || b.Name.Contains("hips", StringComparison.OrdinalIgnoreCase));
        var crotch = float.NegativeInfinity;
        if (pelvis >= 0)
        {
            var hit = CastDown(mid[pelvis], positions, triangles);
            crotch = mid[pelvis].Y - (1.1f * (hit is { } h ? mid[pelvis].Y - h : 0f));
        }
        for (var i = 0; i < n; i++)
        {
            var v = positions[i];
            var best = 0;
            var bestD = float.MaxValue;
            for (var b = 0; b < bones.Count; b++)
            {
                if (skipped[b] || (b == pelvis && v.Y < crotch)) continue;
                var d = Vector3.DistanceSquared(mid[b], v);
                if (d < bestD) { bestD = d; best = b; }
            }
            joints[i * 4] = best;
            weights[i * 4] = 1f;
        }

        // 1b. Knuckles back to the hand: an extremity bone gives up whatever sits behind its start joint.
        for (var b = 0; b < bones.Count; b++)
        {
            if (category[b] != Category.Extremity || bones[b].Parent is not { } p || !index.TryGetValue(p, out var pi)) continue;
            var dir = bones[b].FirstChild is { } c && index.TryGetValue(c, out var ci)
                ? Vector3.Normalize(at[ci] - at[b]) : Vector3.Normalize(at[b] - at[pi]);
            for (var i = 0; i < n; i++)
                if (joints[i * 4] == b && Vector3.Dot(positions[i] - at[b], dir) < 0f) joints[i * 4] = pi;
        }

        // 1c. Torso back from the arms: arm bones give up vertices inboard of the shoulder joint.
        if (armPlane) ArmPlane(bones, index, positions, joints, weights, mid, skipped, armPlaneOffset);

        // 2. Blend the seams.
        Smooth(bones, index, category, positions, triangles, joints, weights);

        // 2a. Not in the original: a coat or skirt hanging between the legs, weighted across the gap rather than
        // snapped to the nearer thigh, before the diffusion so its edges blend into the body. See Coat.
        if (garment is not null) garment(joints, weights);
        else if (coat && pelvis >= 0) Coat(bones, index, positions, triangles, joints, weights, pelvis, report);


        // 2b. Not in the original: widen every seam by diffusing the weights over the surface. Measured on
        // lastlight3, one blended ring leaves coats tearing at the hem and armpits where UniRig's weights do not.
        if (diffuse > 0) Diffuse(positions, triangles, joints, weights, diffuse);

        // 3. Normalise.
        for (var i = 0; i < n; i++)
        {
            var o = i * 4;
            var sum = weights[o] + weights[o + 1] + weights[o + 2] + weights[o + 3];
            if (sum > 0f && MathF.Abs(sum - 1f) > 1e-4f)
                for (var k = 0; k < 4; k++) weights[o + k] /= sum;
        }
        return (joints, weights);
    }
    #endregion

    #region Private
    static bool IsLeaf(Bone b) =>
        !b.HasChildren && (b.Name.Contains("leaf", StringComparison.OrdinalIgnoreCase) || b.Name.Contains("tip", StringComparison.OrdinalIgnoreCase));

    static readonly string[] ExtremityWords = ["hand", "foot", "feet", "toe", "ball", "thumb", "index", "middle", "ring", "pinky",
                                                "finger", "teeth", "eye", "tongue", "wing", "feather", "leaf", "ear", "horn"];

    static readonly string[] LimbWords = ["arm", "upperarm", "lowerarm", "forearm", "elbow", "wrist", "nose", "shoulder", "clavicle",
                                           "ankle", "fin", "head", "chin", "jaw", "mouth", "thigh", "calf", "shin", "knee", "leg",
                                           "upleg", "lowleg", "neck", "humerus"];

    static readonly string[] TorsoWords = ["spine", "chest", "hips", "pelvis", "torso", "abdomen", "body", "tail", "stomach",
                                            "collar", "scapula", "ribcage"];

    static Category Classify(Bone b)
    {
        var name = b.Name.ToLowerInvariant();
        if (ExtremityWords.Any(name.Contains)) return Category.Extremity;
        if (LimbWords.Any(name.Contains)) return Category.Limb;
        if (TorsoWords.Any(name.Contains)) return Category.Torso;
        return b.Parent is null ? Category.Root : Category.Other;
    }

    /// <summary>The height of the first surface straight below a point, or null.</summary>
    static float? CastDown(Vector3 from, IReadOnlyList<Vector3> p, IReadOnlyList<(int A, int B, int C)> tris)
    {
        float? best = null;
        foreach (var (a, b, c) in tris)
        {
            // Möller-Trumbore with direction (0, -1, 0).
            Vector3 v0 = p[a], e1 = p[b] - v0, e2 = p[c] - v0, dir = -Vector3.UnitY;
            var h = Vector3.Cross(dir, e2);
            var det = Vector3.Dot(e1, h);
            if (MathF.Abs(det) < 1e-12f) continue;
            var f = 1f / det;
            var s = from - v0;
            var u = f * Vector3.Dot(s, h);
            if (u is < 0f or > 1f) continue;
            var q = Vector3.Cross(s, e1);
            var w = f * Vector3.Dot(dir, q);
            if (w < 0f || u + w > 1f) continue;
            var t = f * Vector3.Dot(e2, q);
            if (t > 1e-6f && (best is null || from.Y - t > best)) best = from.Y - t;
        }
        return best;
    }

    /// <summary>A garment found round the legs: which vertices, where it starts and ends, and how thick the legs are.</summary>
    internal sealed record Garment(bool[] Mask, int Count, float Top, float Hem, float LegRadius, int Slices, int WithGarment);

    /// <summary>
    /// Finds the garment hanging round the legs below the crotch: a coat or skirt, as the vertices of a surface round
    /// a leg that is not the leg. Null when the skeleton has no legs to measure against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Found as a layer outside the leg.</b> A reconstructed body is one surface with no labels, and a coat on it
    /// takes more than one shape: measured on lastlight3, Kit's and Tomas's coats are a single band with thickness and
    /// an open front, whose cross-section is one C-shaped loop round both legs that a crossing test reads as not
    /// enclosing them; the warden's is two tubes split down the middle, one round each leg. What all three share is
    /// that the legs are modelled inside, so a garment is a surface round a leg that is not the leg.
    /// </para>
    /// <para>
    /// Every half percent of height the surface is sliced into connected loops. The loops whose extent contains a
    /// leg's axis are round that leg: the smallest that encloses the axis is the leg itself, or its boot, and any
    /// larger one is a layer outside it. A layer counts as garment only if it reaches the middle between the legs,
    /// as a coat does: Kit's rubber boots are a layer round each leg too, and pulling a boot toward the other leg
    /// stretched it. Vertices the arm bones own are left alone, since a hand hanging at the hips is not coat.
    /// </para>
    /// </remarks>
    internal static Garment? FindGarment(IReadOnlyList<Bone> bones, IReadOnlyList<Vector3> positions,
        IReadOnlyList<(int A, int B, int C)> triangles, int[] joints)
    {
        var index = bones.Select((b, i) => (b.Name, i)).ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);
        var pelvis = bones.ToList().FindIndex(b => b.Name.Contains("pelvis", StringComparison.OrdinalIgnoreCase)
                                                   || b.Name.Contains("hips", StringComparison.OrdinalIgnoreCase));
        if (pelvis < 0) return null;
        var pelvisMid = bones[pelvis].FirstChild is { } pc && index.TryGetValue(pc, out var pci)
            ? Vector3.Lerp(bones[pelvis].At, bones[pci].At, 0.5f) : bones[pelvis].At;
        var crotchY = CastDown(pelvisMid, positions, triangles) ?? pelvisMid.Y;

        string[] need = ["thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r"];
        if (need.Any(b => !index.ContainsKey(b))) return null;
        Vector3 J(string b) => bones[index[b]].At;

        // A leg's axis at a height: along thigh to knee, then knee to ankle.
        Vector2? Axis(string sfx, float y)
        {
            Vector3 hip = J("thigh" + sfx), knee = J("calf" + sfx), ankle = J("foot" + sfx);
            if (y > hip.Y || y < ankle.Y) return null;
            var (a, b) = y >= knee.Y ? (hip, knee) : (knee, ankle);
            var t = MathF.Abs(a.Y - b.Y) < 1e-6f ? 0f : (a.Y - y) / (a.Y - b.Y);
            var p = Vector3.Lerp(a, b, t);
            return new Vector2(p.X, p.Z);
        }

        var n = positions.Count;
        var weld = new Dictionary<Vector3, int>();
        var rep = new int[n];
        for (var i = 0; i < n; i++) rep[i] = weld.TryGetValue(positions[i], out var r) ? r : weld[positions[i]] = i;

        var y0 = positions.Min(p => p.Y);
        var height = positions.Max(p => p.Y) - y0;
        var top = Math.Min(crotchY, Math.Min(J("thigh_l").Y, J("thigh_r").Y));
        var bottom = Math.Max(J("foot_l").Y, J("foot_r").Y);
        var step = 0.005f * height;

        // Each slice: loops whose extent contains a leg's axis are round that leg. The smallest of them that encloses
        // the axis is the leg itself (or the boot); any larger one is a layer outside it.
        var garment = new bool[n];
        var leg = new bool[n];
        var radii = new List<float>();
        var sliceCount = 0;
        var withGarment = 0;
        for (var y = top - (step / 2f); y > bottom; y -= step)
        {
            if (Axis("_l", y) is not { } left || Axis("_r", y) is not { } right) continue;
            sliceCount++;
            var segments = new List<(Vector2 A, Vector2 B)>();
            var owner = new List<int>();
            var byEdge = new Dictionary<(int, int), int>();
            var parent = new List<int>();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            for (var t = 0; t < triangles.Count; t++)
            {
                var (a, b, c) = triangles[t];
                int[] v = [rep[a], rep[b], rep[c]];
                var points = new List<Vector2>(2);
                var edges = new List<(int, int)>(2);
                for (var q = 0; q < 3; q++)
                {
                    int u = v[q], w = v[(q + 1) % 3];
                    Vector3 pu = positions[u], pw = positions[w];
                    if ((pu.Y > y) == (pw.Y > y)) continue;
                    var f = (y - pu.Y) / (pw.Y - pu.Y);
                    points.Add(new Vector2(pu.X + ((pw.X - pu.X) * f), pu.Z + ((pw.Z - pu.Z) * f)));
                    edges.Add(u < w ? (u, w) : (w, u));
                }
                if (points.Count != 2) continue;
                var id = segments.Count;
                segments.Add((points[0], points[1]));
                owner.Add(t);
                parent.Add(id);
                foreach (var e in edges)
                    if (byEdge.TryGetValue(e, out var other)) parent[Find(id)] = Find(other);
                    else byEdge[e] = id;
            }

            var loops = Enumerable.Range(0, segments.Count).GroupBy(Find).Select(g =>
            {
                var ids = g.ToList();
                var pts = ids.Select(q => segments[q].A).ToList();
                float x0 = pts.Min(q => q.X), x1 = pts.Max(q => q.X), z0 = pts.Min(q => q.Y), z1 = pts.Max(q => q.Y);
                return (Ids: ids, X0: x0, X1: x1, Z0: z0, Z1: z1, Area: (x1 - x0) * (z1 - z0));
            }).ToList();
            bool Around((List<int> Ids, float X0, float X1, float Z0, float Z1, float Area) lp, Vector2 a) =>
                a.X >= lp.X0 && a.X <= lp.X1 && a.Y >= lp.Z0 && a.Y <= lp.Z1;
            bool Encloses(List<int> ids, Vector2 a) => ids.Count(q => Crosses(segments[q].A, segments[q].B, a)) % 2 == 1;

            var legLoops = new HashSet<int>();
            var outer = new HashSet<int>();
            foreach (var axis in new[] { left, right })
            {
                var around = loops.Select((lp, q) => (lp, q)).Where(x => Around(x.lp, axis)).OrderBy(x => x.lp.Area).ToList();
                if (around.Count == 0) continue;
                var own = around.FirstOrDefault(x => Encloses(x.lp.Ids, axis), around[0]);
                legLoops.Add(own.q);
                radii.Add(own.lp.Ids.Average(q => Vector2.Distance(segments[q].A, axis)));
                foreach (var x in around.Where(x => x.lp.Area > own.lp.Area)) outer.Add(x.q);
            }
            outer.ExceptWith(legLoops);

            // A coat reaches the middle between the legs (the warden's two halves meet there); a boot or a turned-up
            // trouser stays round its own leg, and is left to it.
            var middle = (left.X + right.X) / 2f;
            var slack = 0.15f * MathF.Abs(right.X - left.X);
            outer.RemoveWhere(q => loops[q].X0 > middle + slack || loops[q].X1 < middle - slack);
            if (outer.Count > 0) withGarment++;
            for (var q = 0; q < loops.Count; q++)
            {
                if (!legLoops.Contains(q) && !outer.Contains(q)) continue;
                var into = outer.Contains(q) ? garment : leg;
                foreach (var sid in loops[q].Ids)
                {
                    var (a, b, c) = triangles[owner[sid]];
                    into[rep[a]] = into[rep[b]] = into[rep[c]] = true;
                }
            }
        }
        if (sliceCount == 0) return null;

        string[] armWords = ["arm", "hand", "thumb", "index", "middle", "ring", "pinky"];
        var arm = new HashSet<int>(bones.Select((b, q) => (b, q))
            .Where(x => armWords.Any(w => x.b.Name.Contains(w, StringComparison.OrdinalIgnoreCase))).Select(x => x.q));

        var mask = new bool[n];
        var count = 0;
        var lowest = float.MaxValue;
        for (var i = 0; i < n; i++)
        {
            var p = positions[i];
            if (!garment[rep[i]] || leg[rep[i]] || arm.Contains(joints[i * 4])) continue;
            if (Axis("_l", p.Y) is null || Axis("_r", p.Y) is null) continue;
            mask[i] = true;
            count++;
            lowest = Math.Min(lowest, p.Y);
        }
        return new Garment(mask, count, top, count > 0 ? lowest : top, radii.Count > 0 ? radii.Average() : 0f, sliceCount, withGarment);
    }

    /// <summary>
    /// Weights a garment found by <see cref="FindGarment"/> across the gap between the legs: its position between the
    /// two leg axes sets the split between the legs' bones, and the pelvis takes up to half, most at the middle.
    /// </summary>
    /// <remarks>
    /// Snapped to the nearer thigh, a coat's middle splits when the legs part; shared between both legs and the hips,
    /// a leg lifting pulls its side of the coat and the middle hangs. Measured on lastlight3, this found each coat
    /// and made almost no difference: bones that belong to the legs cannot make cloth hang. See <c>SkeletonFit.Skirt</c>.
    /// </remarks>
    static void Coat(IReadOnlyList<Bone> bones, Dictionary<string, int> index, IReadOnlyList<Vector3> positions,
        IReadOnlyList<(int A, int B, int C)> triangles, int[] joints, float[] weights, int pelvis, List<string>? report)
    {
        if (FindGarment(bones, positions, triangles, joints) is not { } g) return;
        Vector3 J(string b) => bones[index[b]].At;
        Vector2 Axis(string sfx, float y)
        {
            Vector3 hip = J("thigh" + sfx), knee = J("calf" + sfx), ankle = J("foot" + sfx);
            var (a, b) = y >= knee.Y ? (hip, knee) : (knee, ankle);
            var t = MathF.Abs(a.Y - b.Y) < 1e-6f ? 0f : Math.Clamp((a.Y - y) / (a.Y - b.Y), 0f, 1f);
            var p = Vector3.Lerp(a, b, t);
            return new Vector2(p.X, p.Z);
        }
        for (var i = 0; i < positions.Count; i++)
        {
            if (!g.Mask[i]) continue;
            var p = positions[i];
            Vector2 l = Axis("_l", p.Y), r = Axis("_r", p.Y);
            var at = new Vector2(p.X, p.Z);
            var across = r - l;
            var s = Math.Clamp(Vector2.Dot(at - l, across) / across.LengthSquared(), 0f, 1f);
            var k = 0.5f * (1f - MathF.Abs((2f * s) - 1f));
            int legL = index[p.Y >= J("calf_l").Y ? "thigh_l" : "calf_l"], legR = index[p.Y >= J("calf_r").Y ? "thigh_r" : "calf_r"];
            var o = i * 4;
            joints[o] = legL; weights[o] = (1f - s) * (1f - k);
            joints[o + 1] = legR; weights[o + 1] = s * (1f - k);
            joints[o + 2] = pelvis; weights[o + 2] = k;
            joints[o + 3] = 0; weights[o + 3] = 0f;
        }
        var y0 = positions.Min(q => q.Y);
        var height = positions.Max(q => q.Y) - y0;
        report?.Add(g.Count == 0 ? $"no garment round the legs in {g.Slices} slices"
            : $"garment: {g.Count} vertices round the legs, from the crotch at {(g.Top - y0) / height:P0} down to {(g.Hem - y0) / height:P0} "
              + $"of the height, in {g.WithGarment} of {g.Slices} slices");
    }

    static bool Crosses(Vector2 a, Vector2 b, Vector2 p)
    {
        if ((a.Y > p.Y) == (b.Y > p.Y)) return false;
        var x = a.X + ((p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
        return x > p.X;
    }

    /// <summary>
    /// Each step, every vertex's weights become half its own and half its neighbours' average (vertices sharing a
    /// position count as one); the four largest are kept.
    /// </summary>
    static void Diffuse(IReadOnlyList<Vector3> positions, IReadOnlyList<(int A, int B, int C)> triangles, int[] joints, float[] weights, int steps)
    {
        var n = positions.Count;
        var weld = new Dictionary<Vector3, int>();
        var rep = new int[n];
        for (var i = 0; i < n; i++) rep[i] = weld.TryGetValue(positions[i], out var r) ? r : weld[positions[i]] = i;
        var adjacency = Enumerable.Range(0, n).Select(_ => new HashSet<int>()).ToArray();
        foreach (var (a, b, c) in triangles)
        {
            int ra = rep[a], rb = rep[b], rc = rep[c];
            adjacency[ra].Add(rb); adjacency[ra].Add(rc);
            adjacency[rb].Add(ra); adjacency[rb].Add(rc);
            adjacency[rc].Add(ra); adjacency[rc].Add(rb);
        }
        var w = new Dictionary<int, float>[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = [];
            for (var k = 0; k < 4; k++)
                if (weights[(i * 4) + k] > 0f) w[i][joints[(i * 4) + k]] = w[i].GetValueOrDefault(joints[(i * 4) + k]) + weights[(i * 4) + k];
        }
        for (var s = 0; s < steps; s++)
        {
            var next = new Dictionary<int, float>[n];
            for (var i = 0; i < n; i++)
            {
                if (rep[i] != i) continue;
                var mix = w[i].ToDictionary(kv => kv.Key, kv => kv.Value * 0.5f);
                var nb = adjacency[i];
                if (nb.Count == 0) { next[i] = w[i]; continue; }
                foreach (var j in nb)
                    foreach (var (bone, v) in w[j]) mix[bone] = mix.GetValueOrDefault(bone) + (0.5f * v / nb.Count);
                next[i] = mix;
            }
            for (var i = 0; i < n; i++) w[i] = next[rep[i]];
        }
        for (var i = 0; i < n; i++)
        {
            var top = w[i].OrderByDescending(kv => kv.Value).Take(4).ToList();
            var sum = top.Sum(kv => kv.Value);
            for (var k = 0; k < 4; k++)
            {
                joints[(i * 4) + k] = k < top.Count ? top[k].Key : 0;
                weights[(i * 4) + k] = k < top.Count && sum > 0f ? top[k].Value / sum : 0f;
            }
        }
    }

    static void ArmPlane(IReadOnlyList<Bone> bones, Dictionary<string, int> index, IReadOnlyList<Vector3> positions,
        int[] joints, float[] weights, Vector3[] mid, bool[] skipped, float offset)
    {
        var shoulder = bones.FirstOrDefault(b => b.Name.Contains("upperarm", StringComparison.OrdinalIgnoreCase));
        if (shoulder is null) return;
        var midline = bones[0].At.X;
        var plane = MathF.Abs(shoulder.At.X - midline) + offset;
        if (plane <= 0f) return;

        var arm = new HashSet<int>();
        foreach (var b in bones.Where(b => b.Name.Contains("upperarm", StringComparison.OrdinalIgnoreCase)))
        {
            var stack = new Stack<string>([b.Name]);
            while (stack.Count > 0)
            {
                var name = stack.Pop();
                arm.Add(index[name]);
                foreach (var c in bones.Where(x => x.Parent == name)) stack.Push(c.Name);
            }
        }
        var fallback = Enumerable.Range(0, bones.Count).Where(b => !arm.Contains(b) && !skipped[b]).ToArray();
        if (fallback.Length == 0) return;

        for (var i = 0; i < positions.Count; i++)
        {
            var v = positions[i];
            if (MathF.Abs(v.X - midline) >= plane) continue;
            var o = i * 4;
            float stolen = 0f;
            var freed = -1;
            for (var k = 0; k < 4; k++)
            {
                if (!arm.Contains(joints[o + k]) || weights[o + k] <= 0f) continue;
                stolen += weights[o + k];
                weights[o + k] = 0f;
                joints[o + k] = 0;
                if (freed < 0) freed = k;
            }
            if (stolen <= 0f) continue;
            var to = fallback.MinBy(b => Vector3.DistanceSquared(mid[b], v));
            var slot = Enumerable.Range(0, 4).FirstOrDefault(k => joints[o + k] == to && weights[o + k] > 0f, -1);
            if (slot < 0) { slot = freed; joints[o + slot] = to; }
            weights[o + slot] += stolen;
            var sum = weights[o] + weights[o + 1] + weights[o + 2] + weights[o + 3];
            if (sum > 0f) for (var k = 0; k < 4; k++) weights[o + k] /= sum;
        }
    }

    static void Smooth(IReadOnlyList<Bone> bones, Dictionary<string, int> index, Category[] category, IReadOnlyList<Vector3> positions,
        IReadOnlyList<(int A, int B, int C)> triangles, int[] joints, float[] weights)
    {
        var n = positions.Count;
        var adjacency = Enumerable.Range(0, n).Select(_ => new HashSet<int>()).ToArray();
        foreach (var (a, b, c) in triangles)
        {
            adjacency[a].Add(b); adjacency[a].Add(c);
            adjacency[b].Add(a); adjacency[b].Add(c);
            adjacency[c].Add(a); adjacency[c].Add(b);
        }

        // Vertices split along a UV seam share a position, and are blended together.
        string Key(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:F6},{v.Y:F6},{v.Z:F6}");
        var shared = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < n; i++)
        {
            var k = Key(positions[i]);
            if (!shared.TryGetValue(k, out var list)) shared[k] = list = [];
            list.Add(i);
        }
        List<int> Shared(int v) => shared.TryGetValue(Key(positions[v]), out var l) ? l : [v];

        bool IsParent(int parent, int child) => bones[child].Parent == bones[parent].Name;
        bool Torso(int a, int b) => (category[a] == Category.Torso || category[b] == Category.Torso)
                                   && category[a] != Category.Extremity && category[b] != Category.Extremity;
        bool Limb(int a, int b) => category[a] == Category.Limb || category[b] == Category.Limb;
        bool Extremity(int a, int b) => category[a] == Category.Extremity && category[b] == Category.Extremity;

        // Every edge between two rigid vertices of different bones.
        var pairs = new List<Pair>();
        var visited = new HashSet<long>();
        for (var i = 0; i < n; i++)
        {
            int oa = i * 4, ba = joints[oa];
            if (weights[oa] != 1f) continue;
            foreach (var j in adjacency[i])
            {
                int ob = j * 4, bb = joints[ob];
                if (ba == bb || weights[ob] != 1f) continue;
                if (!visited.Add(i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i)) continue;
                var kind = Torso(ba, bb) ? Smoothing.Torso : Limb(ba, bb) ? Smoothing.Limb : Extremity(ba, bb) ? Smoothing.Extremity : Smoothing.Standard;
                pairs.Add(new Pair(i, j, ba, bb, kind));
            }
        }

        void Set(int v, int primary, int secondary, float w2)
        {
            foreach (var idx in Shared(v))
            {
                var o = idx * 4;
                joints[o] = primary; joints[o + 1] = secondary; joints[o + 2] = 0; joints[o + 3] = 0;
                weights[o] = 1f - w2; weights[o + 1] = w2; weights[o + 2] = 0f; weights[o + 3] = 0f;
            }
        }

        // Torso: 50/50 at the seam, then 75/25 and 90/10 one and two rings out.
        var torso = pairs.Where(p => p.Kind == Smoothing.Torso).ToList();
        if (torso.Count > 0)
        {
            float[] ring = [0.5f, 0.25f, 0.10f];
            var processed = new HashSet<int>();
            var current = new HashSet<int>();
            foreach (var p in torso)
            {
                Set(p.A, p.BoneA, p.BoneB, ring[0]);
                Set(p.B, p.BoneB, p.BoneA, ring[0]);
                processed.Add(p.A); processed.Add(p.B);
                current.Add(p.A); current.Add(p.B);
            }
            for (var r = 1; r < ring.Length; r++)
            {
                var next = new HashSet<int>();
                foreach (var v in current)
                {
                    var primary = joints[v * 4];
                    var other = joints[(v * 4) + 1];
                    if (other == primary || other == 0) continue;
                    foreach (var nb in adjacency[v])
                    {
                        if (processed.Contains(nb) || joints[nb * 4] != primary || weights[nb * 4] != 1f) continue;
                        Set(nb, primary, other, ring[r]);
                        processed.Add(nb);
                        next.Add(nb);
                    }
                }
                current = next;
            }
        }

        // Limbs: only the child's side of the seam blends, so bending the elbow leaves the bicep alone.
        foreach (var p in pairs.Where(p => p.Kind == Smoothing.Limb))
        {
            if (IsParent(p.BoneA, p.BoneB)) Set(p.B, p.BoneB, p.BoneA, 0.5f);
            else if (IsParent(p.BoneB, p.BoneA)) Set(p.A, p.BoneA, p.BoneB, 0.5f);
            else { Set(p.A, p.BoneA, p.BoneB, 0.5f); Set(p.B, p.BoneB, p.BoneA, 0.5f); }
        }

        // Everything else 50/50; extremity seams stay rigid by design.
        foreach (var p in pairs.Where(p => p.Kind == Smoothing.Standard))
        {
            Set(p.A, p.BoneA, p.BoneB, 0.5f);
            Set(p.B, p.BoneB, p.BoneA, 0.5f);
        }
    }
    #endregion
}
