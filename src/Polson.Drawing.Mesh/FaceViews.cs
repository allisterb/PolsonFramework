namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

/// <summary>
/// A face built from a turnaround: depth from the side views, texture from every view.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> A script reaches this through <c>Face.fromViews(...)</c>.
/// </para>
/// <para>
/// <b>Why the side views, and why their own landmarks.</b> A front view alone gives a face whose
/// depth is the landmark network's human prior and whose sides are the front drawing stretched. The
/// detector finds a drawn profile too — measured on a turnaround sheet, the landmarks on the visible
/// side land on the drawn eye, nose tip, mouth corner, chin and cheek edge, while the hidden side's
/// collapse toward the front — so every vertex is seen properly by at least one view. Each vertex
/// takes its depth from where it sits in a side view that sees it, and each triangle is painted from
/// the single view that sees it most squarely, <i>through that view's own landmarks</i>.
/// </para>
/// <para>
/// <b>Two approaches that were tried first and failed, recorded so they are not tried again.</b>
/// Fitting one depth scale to the drawn outline squashed the whole face into a blade: the outline
/// says how far the nose stands out from the chin, not how far the cheeks sit behind the nose.
/// Matching only the outline left the interior depths wrong, so projecting the side views through
/// them painted each eye twice. Blending views per triangle ghosted features the same way.
/// </para>
/// </remarks>
internal static class FaceViews
{
    #region Types
    /// <summary>One view: what was detected, and the picture it was detected in.</summary>
    internal sealed record View(string Name, FaceDetection Detection, SKBitmap Image);
    #endregion

    #region Methods
    internal static FaceMesh Build(View front, IReadOnlyList<View> sides, ushort[] triangles, SKPoint[] canonicalUvs, int resolution)
    {
        var fd = front.Detection;
        if (!fd.Found)
            throw new ArgumentException($"No face was found in the front view: {fd.Reason}");
        if (!fd.HasDepth)
            throw new ArgumentException("The front view's landmarks carry no depth: the face backend predates it. Update src/vision/face_landmarks.py.");
        if (fd.Count < V || canonicalUvs.Length < V)
            throw new ArgumentException($"A face needs {V} landmarks; the front view has {fd.Count}.");

        var F = fd.Points.Take(V).ToArray();
        var z0 = fd.Depths.Take(V).ToArray();
        float x0 = F.Min(p => p.X), x1 = F.Max(p => p.X), y0 = F.Min(p => p.Y), y1 = F.Max(p => p.Y);
        float faceW = x1 - x0, faceH = y1 - y0, bx = (x0 + x1) / 2f, by = (y0 + y1) / 2f;
        var mid = Midline.Average(i => F[i].X);
        var margin = 0.04f * faceW;

        // ── Register each side view to the front: which half it sees, its scale, its offset ────────
        var registered = new List<Side>();
        foreach (var view in sides)
            registered.Add(Register(view, F, z0, mid, margin, faceH));
        if (registered.Count == 2 && registered[0].Sign == registered[1].Sign)
            throw new ArgumentException(
                $"The side views '{registered[0].View.Name}' and '{registered[1].View.Name}' both face the same way. "
                + "A turnaround's two side views face opposite ways; pass one of them, or the other side.");

        // ── Depth: each vertex from the side view(s) that see it, the change smoothed over the mesh ─
        var target = new float[V];
        var seen = new int[V];
        foreach (var s in registered)
            for (var i = 0; i < V; i++)
                if (s.Sees[i]) { target[i] += s.Depth(i); seen[i]++; }

        var d = new float[V];
        for (var i = 0; i < V; i++) d[i] = seen[i] > 0 ? (target[i] / seen[i]) - z0[i] : 0f;
        d = Smooth(d, Neighbours(triangles), 3);
        var z = z0.Select((v, i) => v + d[i]).ToArray();

        var verts = new SKPoint3[V];
        for (var i = 0; i < V; i++) verts[i] = new SKPoint3(F[i].X - bx, by - F[i].Y, z[i]);

        // ── Texture: one view per triangle, through that view's own landmarks ──────────────────────
        var res = resolution > 0 ? Math.Clamp(resolution, 256, 8192) : 2048;
        var atlas = new SKBitmap(new SKImageInfo(res, res, SKColorType.Rgba8888, SKAlphaType.Premul));
        var counts = new Dictionary<string, int> { [front.Name] = 0 };
        foreach (var s in registered) counts[s.View.Name] = 0;

        using (var canvas = new SKCanvas(atlas))
        {
            canvas.Clear(Sample(front.Image, F[Glabella]));
            var images = new Dictionary<string, SKImage> { [front.Name] = SKImage.FromBitmap(front.Image) };
            foreach (var s in registered) images[s.View.Name] = SKImage.FromBitmap(s.View.Image);
            using var paint = new SKPaint { IsAntialias = true };
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

            for (var t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                var n = Normal(verts[a], verts[b], verts[c]);

                // The front, unless a side view sees this triangle more squarely AND sees all of it.
                var (name, pts, best) = (front.Name, F, n.Z);
                foreach (var s in registered)
                {
                    var facing = -s.Sign * n.X;       // sign +1 sees the model's -x side
                    if (facing > best && s.Sees[a] && s.Sees[b] && s.Sees[c])
                        (name, pts, best) = (s.View.Name, s.Points, facing);
                }

                SKPoint[] to = [Uv(canonicalUvs[a], res), Uv(canonicalUvs[b], res), Uv(canonicalUvs[c], res)];
                if (!FaceBake.TryAffine([pts[a], pts[b], pts[c]], to, out var map)) continue;

                canvas.Save();
                canvas.ClipPath(FaceBake.Grown(to, 1.2f), SKClipOperation.Intersect, antialias: true);
                canvas.SetMatrix(map);
                canvas.DrawImage(images[name], 0, 0, sampling, paint);
                canvas.Restore();
                counts[name]++;
            }

            foreach (var img in images.Values) img.Dispose();
        }

        var summary = string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}"));
        var scales = registered.Count == 0 ? "" : "; side scale " + string.Join(", ", registered.Select(s => $"{s.View.Name} {s.Scale:F2}"));
        return new FaceMesh(verts, canonicalUvs.Take(V).ToArray(), triangles, true,
                            $"face from views — triangles from {summary}{scales}")
        { Texture = atlas };
    }
    #endregion

    #region Methods (private)
    /// <summary>A side view registered to the front: which half it sees, and how its pixels map to depth.</summary>
    sealed class Side
    {
        internal required View View;
        internal required int Sign;             // +1: nose points right, sees the front view's image-left half
        internal required float Scale, Offset;
        internal required bool[] Sees;
        internal required SKPoint[] Points;
        internal float Depth(int i) => Sign * ((Points[i].X / Scale) - Offset);
    }

    static Side Register(View view, SKPoint[] F, float[] z0, float mid, float margin, float faceH)
    {
        var det = view.Detection;
        if (!det.Found)
            throw new ArgumentException($"No face was found in the '{view.Name}' view: {det.Reason}");
        if (MathF.Abs(det.YawDeg) < 20f)
            throw new ArgumentException(
                $"The '{view.Name}' view reads as turned only {det.YawDeg:F0}°, which is not a side view. "
                + "Pass a profile — the detector reads a true profile at around 60°.");

        var sign = det.YawDeg > 0 ? 1 : -1;
        var S = det.Points.Take(V).ToArray();
        var sees = new bool[V];
        for (var i = 0; i < V; i++) sees[i] = sign > 0 ? F[i].X < mid + margin : F[i].X > mid - margin;
        var vis = Enumerable.Range(0, V).Where(i => sees[i]).ToArray();

        // Scale and vertical offset from the rows: a turnaround draws every view to one height scale,
        // so side y = a · front y + b. Fitting it means crops need not be cut to the same size.
        double sy = 0, sf = 0, sff = 0, sfy = 0;
        foreach (var i in vis) { sf += F[i].Y; sy += S[i].Y; sff += F[i].Y * F[i].Y; sfy += F[i].Y * S[i].Y; }
        double n = vis.Length, varf = (sff / n) - ((sf / n) * (sf / n));
        var a = (float)(((sfy / n) - ((sf / n) * (sy / n))) / Math.Max(varf, 1e-9));
        var b = (float)((sy / n) - (a * sf / n));
        if (a < 0.5f || a > 2f)
            throw new ArgumentException(
                $"The '{view.Name}' view is at {a:F2}× the front view's scale, measured from its rows. "
                + "Crop the views from one sheet at roughly one scale.");

        var resid = vis.Select(i => MathF.Abs(S[i].Y - ((a * F[i].Y) + b))).OrderBy(r => r).ToArray();
        var misfit = resid[resid.Length / 2] / (a * faceH);
        if (misfit > 0.05f)
            throw new ArgumentException(
                $"The '{view.Name}' view's features do not line up with the front's: they are {misfit:P0} of the face height out "
                + "even after fitting scale and offset. A turnaround's views must be drawn to one height scale.");

        // Horizontal offset, by median, so a disagreement at the nose or lips cannot drag it.
        var offset = vis.Select(i => (S[i].X / a) - (sign * z0[i])).OrderBy(o => o).ElementAt(vis.Length / 2);
        return new Side { View = view, Sign = sign, Scale = a, Offset = offset, Sees = sees, Points = S };
    }

    static float[] Smooth(float[] d, List<int>[] nbr, int passes)
    {
        for (var p = 0; p < passes; p++)
        {
            var next = new float[d.Length];
            for (var i = 0; i < d.Length; i++)
            {
                float sum = d[i] * 2f, k = 2f;
                foreach (var j in nbr[i]) { sum += d[j]; k++; }
                next[i] = sum / k;
            }
            d = next;
        }
        return d;
    }

    static List<int>[] Neighbours(ushort[] tris)
    {
        var nbr = Enumerable.Range(0, V).Select(_ => new List<int>()).ToArray();
        for (var t = 0; t < tris.Length; t += 3)
            for (var k = 0; k < 3; k++)
            {
                int a = tris[t + k], b = tris[t + ((k + 1) % 3)];
                if (a >= V || b >= V) continue;
                if (!nbr[a].Contains(b)) nbr[a].Add(b);
                if (!nbr[b].Contains(a)) nbr[b].Add(a);
            }
        return nbr;
    }

    static SKPoint3 Normal(SKPoint3 a, SKPoint3 b, SKPoint3 c)
    {
        float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
        float nx = (uy * vz) - (uz * vy), ny = (uz * vx) - (ux * vz), nz = (ux * vy) - (uy * vx);
        var l = MathF.Max(1e-9f, MathF.Sqrt((nx * nx) + (ny * ny) + (nz * nz)));
        return new SKPoint3(nx / l, ny / l, nz / l);
    }

    static SKPoint Uv(SKPoint uv, int res) => new(uv.X * res, (1f - uv.Y) * res);

    static SKColor Sample(SKBitmap img, SKPoint p) =>
        img.GetPixel(Math.Clamp((int)p.X, 0, img.Width - 1), Math.Clamp((int)p.Y, 0, img.Height - 1)).WithAlpha(255);
    #endregion

    #region Fields
    const int V = 468;

    const int Glabella = 9;

    /// <summary>Landmarks on the facial midline, whose mean x is where one side view hands over to the other.</summary>
    static readonly int[] Midline = [1, 4, 5, 6, 168, 197, 195, 2, 0, 13, 14, 17, 152, 10, 151, 9];
    #endregion
}
