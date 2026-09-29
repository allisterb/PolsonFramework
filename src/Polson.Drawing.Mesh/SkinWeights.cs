namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

/// <summary>
/// Skin weights for a mesh and a skeleton, by distance: Mesh2Motion's solver, ported. What <see cref="SolverRig"/> weights with.
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
/// <para>
/// A coat rule, which weighted a garment round the legs across the gap between them, was tried and removed on
/// 2026-09-29: it found every coat and made almost no difference, since bones that belong to the legs cannot make
/// cloth hang. Cloth is <c>Character.drape</c>'s job. See <c>docs/internal/character-rigging-modes.md</c> §2c.
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
        IReadOnlyList<(int A, int B, int C)> triangles, bool armPlane = true, float armPlaneOffset = 0f, int diffuse = 0)
    {
        var n = positions.Count;
        var joints = new int[n * 4];
        var weights = new float[n * 4];
        var index = bones.Select((b, i) => (b.Name, i)).ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);
        var category = bones.Select(Classify).ToArray();
        var at = bones.Select(b => b.At).ToArray();
        var mid = bones.Select(b => b.FirstChild is { } c && index.TryGetValue(c, out var ci) ? Vector3.Lerp(b.At, at[ci], 0.5f) : b.At).ToArray();
        var skipped = bones.Select(b => b.Name == "root" || IsLeaf(b)).ToArray();

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

        // 2a. Not in the original: widen every seam by diffusing the weights over the surface. Measured on
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
    internal static float? CastDown(Vector3 from, IReadOnlyList<Vector3> p, IReadOnlyList<(int A, int B, int C)> tris)
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
