namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using SkiaSharp;

/// <summary>
/// How each of a character's rigs fails, measured over a fixed set of clips: tearing, and a garment moving as a rigid
/// slab while the legs under it bend. Recorded at build, not acted on: see <c>docs/internal/character-rigging-modes.md</c> §5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why two measures.</b> Stretch alone picks UniRig's rig in exactly the cases where it looks worse: a coat that
/// moves rigidly does not stretch, so it scores well while standing out from the thighs as a tube. The rigid-garment
/// score sees that failure and stretch sees the other one, the solver's torn seams.
/// </para>
/// <para>
/// <b>Not validated.</b> Neither score chooses anything until the director's calls on the render pairs in
/// <c>preview-rigs.png</c> agree with it (§5c). They are written so there is data to check them against.
/// </para>
/// </remarks>
internal static class RigScores
{
    #region Types
    /// <summary>One rig in one clip.</summary>
    /// <param name="Stretch">Mean over the surface's edges of |posed length / rest length - 1|.</param>
    /// <param name="P95">The 95th percentile of the same: a tear is local, and the mean dilutes it.</param>
    /// <param name="Torn">The share of edges over 1.5 times or under two thirds of their rest length.</param>
    /// <param name="GarmentResidual">How far the hanging garment is from moving as one rigid piece, as a share of the height.</param>
    /// <param name="LegResidual">The same for the legs under it.</param>
    /// <param name="Rigid">1 - garment / legs, clamped to 0..1: near 1 when the garment moved as a slab while the legs bent.
    /// Null without a hanging garment, or when the legs barely moved and there is nothing to compare.</param>
    internal sealed record ClipScore(string Clip, float At, double Stretch, double P95, double Torn,
                                     double? GarmentResidual, double? LegResidual, double? Rigid);
    #endregion

    #region Fields
    /// <summary>The five clips the design doc's measurements used (§2), each at a fixed moment.</summary>
    internal static readonly (string Clip, float At)[] Clips =
        [("Crouch_Idle", 0.5f), ("Walk", 0.25f), ("Idle_FoldArms", 0.5f), ("Sitting_Idle", 0.5f), ("Jumping Jacks", 0.3f)];

    /// <summary>Legs that moved less than this, as a share of the height, give no rigid-garment reading.</summary>
    const double StillLegs = 0.005;
    #endregion

    #region Methods
    /// <summary>
    /// Scores <paramref name="body"/>, a rig's body with its parts named, in each clip the library has. The hanging
    /// garment is <paramref name="garment"/>, a mask over its vertices, or null when nothing hangs.
    /// </summary>
    internal static List<ClipScore> Score(FaceMesh body, HangPlan? garment, string? projectRoot, List<string> notes)
    {
        var kit = new CharacterToolkit(projectRoot);
        var known = PoseRetarget.Clips(projectRoot).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var v = body.Vertices;
        var height = v.Max(p => p.Y) - v.Min(p => p.Y);
        var edges = Edges(body.Indices);
        var legs = garment is null ? null : Legs(body, garment);

        var scores = new List<ClipScore>();
        foreach (var (clip, at) in Clips)
        {
            if (!known.Contains(clip)) { notes.Add($"Rig scores: the pose library has no clip '{clip}', so it was left out."); continue; }
            var posed = body.Pose(kit.Retarget(body, clip, new Dictionary<string, object?> { ["at"] = (double)at })).Vertices;
            var (mean, p95, torn) = Stretch(v, posed, edges);

            double? g = null, l = null, rigid = null;
            if (garment is { Count: > 0 } && legs is { Length: > 0 })
            {
                g = Residual(v, posed, garment.Mask) / height;
                l = Residual(v, posed, legs) / height;
                if (l > StillLegs) rigid = Math.Clamp(1 - (g.Value / l.Value), 0, 1);
            }
            scores.Add(new ClipScore(clip, at, mean, p95, torn, g, l, rigid));
        }
        return scores;
    }

    /// <summary>A rig's scores as the manifest records them: a summary over the clips, then each clip.</summary>
    internal static JsonObject Record(List<ClipScore> scores)
    {
        static JsonNode? R(double? x) => x is { } d ? Math.Round(d, 4) : null;
        var rigid = scores.Where(s => s.Rigid is not null).Select(s => s.Rigid!.Value).ToList();
        return new JsonObject
        {
            ["stretch"] = scores.Count > 0 ? R(scores.Average(s => s.Stretch)) : null,
            ["p95"] = scores.Count > 0 ? R(scores.Max(s => s.P95)) : null,
            ["torn"] = scores.Count > 0 ? R(scores.Average(s => s.Torn)) : null,
            ["rigidGarment"] = rigid.Count > 0 ? R(rigid.Average()) : null,
            ["clips"] = new JsonObject(scores.Select(s => KeyValuePair.Create($"{s.Clip} {s.At.ToString("0.##", CultureInfo.InvariantCulture)}",
                (JsonNode?)new JsonObject
                {
                    ["stretch"] = R(s.Stretch), ["p95"] = R(s.P95), ["torn"] = R(s.Torn),
                    ["rigidGarment"] = R(s.Rigid), ["garmentResidual"] = R(s.GarmentResidual), ["legResidual"] = R(s.LegResidual)
                })))
        };
    }

    /// <summary>
    /// Each clip as a row, each rig as a column pair (three-quarter and side), as clay: the render pairs the director
    /// labels, so they carry the rigs' names and not their scores.
    /// </summary>
    internal static SKBitmap Preview(IReadOnlyDictionary<string, FaceMesh> bodies, string? projectRoot)
    {
        const int W = 240, H = 330, Label = 22;
        var kit = new CharacterToolkit(projectRoot);
        var known = PoseRetarget.Clips(projectRoot).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var clips = Clips.Where(c => known.Contains(c.Clip)).ToList();
        var rigs = bodies.Keys.ToList();
        var yaws = new[] { 30f, 90f };
        var cols = rigs.Count * yaws.Length;

        using var canvas = new SkiaCanvas(W * cols, Label + (H * clips.Count));
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#f2efe8";
        ctx.FillRect(0, 0, W * cols, Label + (H * clips.Count));
        ctx.Font = "13px sans-serif";
        ctx.FillStyle = "#333333";
        for (var c = 0; c < cols; c++)
            ctx.FillText($"{rigs[c % rigs.Count]}, {yaws[c / rigs.Count]:0}°", (W * c) + 8, 16);

        var mt = new MeshToolkit();
        for (var r = 0; r < clips.Count; r++)
        {
            var (clip, at) = clips[r];
            for (var i = 0; i < rigs.Count; i++)
            {
                var body = bodies[rigs[i]];
                var posed = body.Pose(kit.Retarget(body, clip, new Dictionary<string, object?> { ["at"] = (double)at }));
                var b = body.Bounds;
                var scale = 0.82f * H / Convert.ToSingle(b["height"], CultureInfo.InvariantCulture);
                for (var y = 0; y < yaws.Length; y++)
                {
                    var c = (y * rigs.Count) + i;
                    mt.Draw(ctx, posed, new Dictionary<string, object?>
                    {
                        ["x"] = (W * c) + (W / 2f), ["y"] = Label + (H * r) + (H / 2f) + 12, ["scale"] = scale,
                        ["yawDeg"] = yaws[y], ["clay"] = true
                    });
                }
            }
            ctx.FillStyle = "#333333";
            ctx.FillText($"{clip} {at.ToString("0.##", CultureInfo.InvariantCulture)}", 8, Label + (H * r) + 16);
        }
        return canvas.Bitmap.Bitmap.Copy();
    }
    #endregion

    #region Private
    /// <summary>Each edge once, by its two vertex indices.</summary>
    static (int A, int B)[] Edges(ushort[] idx)
    {
        var set = new HashSet<long>();
        for (var i = 0; i < idx.Length; i += 3)
            foreach (var (u, w) in new[] { (idx[i], idx[i + 1]), (idx[i + 1], idx[i + 2]), (idx[i + 2], idx[i]) })
                set.Add(u < w ? ((long)u << 32) | w : ((long)w << 32) | u);
        return [.. set.Select(e => ((int)(e >> 32), (int)(e & 0xffffffff)))];
    }

    static (double Mean, double P95, double Torn) Stretch(SKPoint3[] rest, SKPoint3[] posed, (int A, int B)[] edges)
    {
        var d = new List<double>(edges.Length);
        var torn = 0;
        foreach (var (a, b) in edges)
        {
            var r0 = Dist(rest[a], rest[b]);
            if (r0 < 1e-6) continue;
            var ratio = Dist(posed[a], posed[b]) / r0;
            d.Add(Math.Abs(ratio - 1));
            if (ratio > 1.5 || ratio < 2.0 / 3) torn++;
        }
        if (d.Count == 0) return (0, 0, 0);
        d.Sort();
        return (d.Average(), d[(int)(0.95 * (d.Count - 1))], torn / (double)d.Count);
    }

    /// <summary>The legs under a hanging garment: below its top, not garment, and within the legs' span across.</summary>
    static bool[] Legs(FaceMesh body, HangPlan garment)
    {
        Vector3 J(string part)
        {
            var at = CharacterIk.Where(body, null, part, null);
            return new Vector3(Convert.ToSingle(at["x"], CultureInfo.InvariantCulture), Convert.ToSingle(at["y"], CultureInfo.InvariantCulture),
                               Convert.ToSingle(at["z"], CultureInfo.InvariantCulture));
        }
        var v = body.Vertices;
        var height = v.Max(p => p.Y) - v.Min(p => p.Y);
        float l = J("leftShin").X, r = J("rightShin").X;
        float x0 = MathF.Min(l, r) - (0.06f * height), x1 = MathF.Max(l, r) + (0.06f * height);
        return [.. v.Select((p, i) => p.Y < garment.Top && !garment.Mask[i] && p.X >= x0 && p.X <= x1)];
    }

    /// <summary>
    /// The root-mean-square distance left after the best rigid fit (rotation and translation) of the masked vertices from
    /// rest to posed: how far they are from having moved as one piece. Horn's quaternion method.
    /// </summary>
    internal static double Residual(SKPoint3[] rest, SKPoint3[] posed, bool[] mask)
    {
        var ids = Enumerable.Range(0, Math.Min(mask.Length, rest.Length)).Where(i => mask[i]).ToArray();
        if (ids.Length < 3) return 0;
        Vector3 P(int i) => new(rest[i].X, rest[i].Y, rest[i].Z);
        Vector3 Q(int i) => new(posed[i].X, posed[i].Y, posed[i].Z);
        var cp = ids.Aggregate(Vector3.Zero, (s, i) => s + P(i)) / ids.Length;
        var cq = ids.Aggregate(Vector3.Zero, (s, i) => s + Q(i)) / ids.Length;

        double sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
        foreach (var i in ids)
        {
            var a = P(i) - cp;
            var b = Q(i) - cq;
            sxx += a.X * b.X; sxy += a.X * b.Y; sxz += a.X * b.Z;
            syx += a.Y * b.X; syy += a.Y * b.Y; syz += a.Y * b.Z;
            szx += a.Z * b.X; szy += a.Z * b.Y; szz += a.Z * b.Z;
        }
        double[,] n =
        {
            { sxx + syy + szz, syz - szy, szx - sxz, sxy - syx },
            { syz - szy, sxx - syy - szz, sxy + syx, szx + sxz },
            { szx - sxz, sxy + syx, -sxx + syy - szz, syz + szy },
            { sxy - syx, szx + sxz, syz + szy, -sxx - syy + szz }
        };

        // The eigenvector of the largest eigenvalue is the rotation. N is symmetric, so Jacobi rotations diagonalise it
        // exactly; power iteration was tried first and converged too slowly when the top two eigenvalues were close.
        var q = TopEigenvector(n);
        var rot = Quaternion.Normalize(new Quaternion((float)q[1], (float)q[2], (float)q[3], (float)q[0]));

        var sum = 0.0;
        foreach (var i in ids)
        {
            var fit = Vector3.Transform(P(i) - cp, rot) + cq;
            sum += Vector3.DistanceSquared(fit, Q(i));
        }
        return Math.Sqrt(sum / ids.Length);
    }

    /// <summary>The unit eigenvector of a symmetric 4×4 matrix's largest eigenvalue, by cyclic Jacobi rotations.</summary>
    static double[] TopEigenvector(double[,] m)
    {
        var a = (double[,])m.Clone();
        var v = new double[4, 4];
        for (var i = 0; i < 4; i++) v[i, i] = 1;
        for (var sweep = 0; sweep < 50; sweep++)
        {
            var off = 0.0;
            for (var p = 0; p < 4; p++) for (var r = p + 1; r < 4; r++) off += a[p, r] * a[p, r];
            if (off < 1e-24) break;
            for (var p = 0; p < 4; p++)
                for (var r = p + 1; r < 4; r++)
                {
                    if (Math.Abs(a[p, r]) < 1e-300) continue;
                    var theta = (a[r, r] - a[p, p]) / (2 * a[p, r]);
                    var t = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt((theta * theta) + 1));
                    double c = 1 / Math.Sqrt((t * t) + 1), s = t * c;
                    for (var k = 0; k < 4; k++)
                    {
                        double akp = a[k, p], akr = a[k, r];
                        a[k, p] = (c * akp) - (s * akr);
                        a[k, r] = (s * akp) + (c * akr);
                    }
                    for (var k = 0; k < 4; k++)
                    {
                        double apk = a[p, k], ark = a[r, k];
                        a[p, k] = (c * apk) - (s * ark);
                        a[r, k] = (s * apk) + (c * ark);
                    }
                    for (var k = 0; k < 4; k++)
                    {
                        double vkp = v[k, p], vkr = v[k, r];
                        v[k, p] = (c * vkp) - (s * vkr);
                        v[k, r] = (s * vkp) + (c * vkr);
                    }
                }
        }
        var top = Enumerable.Range(0, 4).MaxBy(i => a[i, i]);
        return [v[0, top], v[1, top], v[2, top], v[3, top]];
    }

    static double Dist(SKPoint3 a, SKPoint3 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)) + ((a.Z - b.Z) * (a.Z - b.Z)));
    #endregion
}
