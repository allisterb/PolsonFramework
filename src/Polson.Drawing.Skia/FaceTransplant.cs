namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SkiaSharp;

/// <summary>
/// A face mesh carried on a character's head: where it sits, and how it follows a pose.
/// </summary>
/// <remarks>
/// <b>Not exposed to scripts.</b> A script reaches this through <c>mesh.withFaceMesh(face)</c>, and
/// the mesh that returns poses and draws like any other.
/// </remarks>
internal sealed class FaceAttachment
{
    #region Fields
    /// <summary>How many of the combined mesh's vertices are the body's; the face's follow them.</summary>
    internal required int BodyCount;

    /// <summary>A vertex made by clipping one of the body's triangles: a fixed blend of its corners.</summary>
    internal readonly record struct Blend(int A, int B, int C, float Wa, float Wb, float Wc);

    /// <summary>The clipped vertices, which follow the body's corners they were cut from.</summary>
    internal required Blend[] Derived;

    /// <summary>Where the face's vertices start in the combined mesh.</summary>
    internal int FaceStart => BodyCount + Derived.Length;

    /// <summary>The face in its own frame, which is where its expressions are decided.</summary>
    internal required FaceMesh Face;

    /// <summary>The body's bind vertices, so a pose is always measured from bind.</summary>
    internal required SKPoint3[] BodyBind;

    /// <summary>Face (x, y) to the body's front frame (x · front, y).</summary>
    internal required SKMatrix Align;

    internal required float DepthScale, DepthOffset;

    internal required int FrontSign;

    /// <summary>Body vertices that carry the face: the ones it hides, which the head moves rigidly.</summary>
    internal required int[] Carriers;

    /// <summary>Per face vertex, the front-frame depth added so its rim meets the character's surface.</summary>
    internal required float[] Conform;

    /// <summary>How much nearer the viewer the face's triangles are sorted, in model units.</summary>
    internal required float SortBias;

    /// <summary>The rigid motion of the head since bind: posed = Rotation · bind + Translation.</summary>
    internal Quaternion Rotation = Quaternion.Identity;

    internal Vector3 Translation = Vector3.Zero;
    #endregion

    #region Methods
    /// <summary>A face-frame point, aligned to the head in bind and then moved with it.</summary>
    internal SKPoint3 Place(SKPoint3 p, int i)
    {
        var uv = Align.MapPoint(p.X, p.Y);
        var d = (p.Z * DepthScale) + DepthOffset + Conform[i];
        var bind = new Vector3(uv.X * FrontSign, uv.Y, d * FrontSign);
        var posed = Vector3.Transform(bind, Rotation) + Translation;
        return new SKPoint3(posed.X, posed.Y, posed.Z);
    }

    /// <summary>Writes the clipped vertices, from the body's current positions, after the body's own.</summary>
    internal void Derive(SKPoint3[] body, SKPoint3[] into)
    {
        for (var i = 0; i < Derived.Length; i++)
        {
            var d = Derived[i];
            SKPoint3 a = body[d.A], b = body[d.B], c = body[d.C];
            into[BodyCount + i] = new SKPoint3((d.Wa * a.X) + (d.Wb * b.X) + (d.Wc * c.X),
                                               (d.Wa * a.Y) + (d.Wb * b.Y) + (d.Wc * c.Y),
                                               (d.Wa * a.Z) + (d.Wb * b.Z) + (d.Wc * c.Z));
        }
    }

    /// <summary>The same attachment following a posed body.</summary>
    internal FaceAttachment Follow(SKPoint3[] posedBody)
    {
        var (r, t) = FaceTransplant.Rigid(
            [.. Carriers.Select(i => BodyBind[i])], [.. Carriers.Select(i => posedBody[i])]);
        return new FaceAttachment
        {
            BodyCount = BodyCount, Derived = Derived, Face = Face, BodyBind = BodyBind, Align = Align,
            DepthScale = DepthScale, DepthOffset = DepthOffset, FrontSign = FrontSign, Carriers = Carriers,
            Conform = Conform, SortBias = SortBias, Rotation = r, Translation = t
        };
    }
    #endregion
}

/// <summary>
/// Puts a face mesh on a character's head: aligned by the eyes and mouth, the character's own face
/// hidden under it, and both in one atlas so a single depth sort handles what is in front of what.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> A script reaches this through <c>mesh.withFaceMesh(face)</c>.
/// </para>
/// <para>
/// <b>Why one mesh rather than drawing the face as a second pass.</b> <c>Mesh.draw</c> sorts
/// triangles back to front and has no depth buffer, so occlusion is only right within one call. A
/// face drawn afterwards would paint over a hand raised in front of it, and one drawn before would
/// vanish under the head it belongs on.
/// </para>
/// <para>
/// <b>Why the face follows a rigid fit rather than a bone.</b> SharpGLTF does not expose a posed
/// joint's world matrix, but it does pose every vertex — so the head's motion is recovered from the
/// body vertices the face covers (Horn's closed-form absolute orientation), which the head carries
/// almost rigidly. It also means the attachment needs no knowledge of which bone is the head.
/// </para>
/// </remarks>
internal static class FaceTransplant
{
    #region Methods
    internal static FaceMesh Attach(FaceMesh body, FaceMesh face, IDictionary? opt)
    {
        if (body.Attachment is not null)
            throw new ArgumentException(
                "This mesh already carries a transplanted face. Start from the character as loaded: "
                + "julie.withFaceMesh(face), not julie.withFaceMesh(a).withFaceMesh(b).");
        if (!body.HasUvs || body.Fitted || body.Texture is null)
            throw new ArgumentException(
                $"Mesh '{body.Source}' has no texture atlas, so there is nothing to put the face's picture beside. "
                + "Transplant onto a .glb or .obj carrying its own texture.");
        if (face.VertexCount < 468 || !face.HasUvs || face.Fitted || face.Texture is null)
            throw new ArgumentException(
                $"withFaceMesh takes a face on MediaPipe's canonical topology with its own atlas — from "
                + $"Face.fromViews(...) or Face.detect(image).mesh(image). '{face.Source}' has "
                + $"{face.VertexCount} vertices{(face.Texture is null ? " and no texture" : "")}.");

        var anchors = AnchorOptions(opt);
        var setup = FaceBake.For(body, anchors);
        var front = setup.FrontSign;
        var bodyBind = body.Reference;
        var B = bodyBind.Length;
        if (B + face.VertexCount > ushort.MaxValue)
            throw new ArgumentException(
                $"The character has {B:N0} vertices and the face {face.VertexCount:N0}; together they pass "
                + $"the mesh buffer's {ushort.MaxValue:N0}. Decimate the character first.");

        // ── Align: the face's eyes and mouth onto the character's, exactly, by an affine map ──────────
        var fv = face.Vertices;
        var (fl, fr) = Order(Mid(fv[33], fv[133]), Mid(fv[362], fv[263]));
        var fm = Mid(fv[13], fv[14]);
        var (bl, br) = Order(new SKPoint(setup.ModelEyeLeft.X * front, setup.ModelEyeLeft.Y),
                             new SKPoint(setup.ModelEyeRight.X * front, setup.ModelEyeRight.Y));
        var bm = new SKPoint(setup.ModelMouth.X * front, setup.ModelMouth.Y);
        if (!FaceBake.TryAffine([Flat(fl), Flat(fr), Flat(fm)], [bl, br, bm], out var align))
            throw new ArgumentException("The face's eyes and mouth are collinear, so it cannot be lined up.");
        var k = MathF.Sqrt(MathF.Abs((align.ScaleX * align.ScaleY) - (align.SkewX * align.SkewY)));

        var placed = fv.Select(p => align.MapPoint(p.X, p.Y)).ToArray();

        // ── Depth: seat the eyes and mouth on the character's own surface ─────────────────────────────
        var outline = Outline(face, placed);
        var surface = new Surface(bodyBind, body.Indices, front);
        var offsets = new List<float>();
        foreach (var (at, z) in new[] { (bl, fl.Z), (br, fr.Z), (bm, fm.Z) })
            if (surface.At(at) is var d && !float.IsNaN(d)) offsets.Add(d - (z * k));
        if (offsets.Count == 0)
            throw new ArgumentException("No surface of this character lies under the face's eyes or mouth, so it cannot be seated.");
        var offset = offsets.Average() + (MeshToolkit.Num(opt, "depth", 0f) * k * FaceHeight(fv));

        // ── Conform the rim onto the character's surface, fading to nothing a few rings in ──────────
        // **A face from a turnaround wraps back round the cheeks, and a generated character's face
        // usually does not** — measured on Julie, the drawn face's cheek edge sat 0.05 model units
        // behind her flat cheek, so her own surface showed through as jagged slivers the moment the
        // head turned. Pulling the rim onto her surface makes the seam continuous; the interior keeps
        // the drawn face's shape, which is the whole reason for transplanting it.
        var rings = MeshToolkit.Num(opt, "blend", 5f);
        var hops = Hops(face);
        var under = placed.Select(surface.At).ToArray();
        var conform = new float[face.VertexCount];
        for (var i = 0; i < face.VertexCount; i++)
        {
            var w = rings <= 0f ? 0f : Math.Clamp(1f - (hops[i] / rings), 0f, 1f);
            if (w <= 0f || float.IsNaN(under[i])) continue;
            conform[i] = w * w * (3f - (2f * w)) * (under[i] - ((fv[i].Z * k) + offset));
        }

        // ── Flush: nothing of the face sits behind the character's surface ────────────────────────────
        // **Measured on Julie: 63 of the 147 vertices across the brow and forehead sat behind her
        // surface, by up to 9% of the face's height** — a drawn face has eye sockets and temples, a
        // generated one is nearly flat. Wherever her hair stood proud of that, it occluded the drawn
        // forehead as a dark crescent once the head turned. Lifting those vertices onto her surface
        // costs some of the drawn relief there and none of the nose, lips or brow, which stand in
        // front of it anyway; `flush: false` keeps the relief and the crescents.
        if (opt is null || !opt.Contains("flush") || Convert.ToBoolean(opt["flush"]))
        {
            var push = new float[face.VertexCount];
            for (var i = 0; i < face.VertexCount; i++)
                if (!float.IsNaN(under[i])) push[i] = MathF.Max(0f, under[i] - ((fv[i].Z * k) + offset + conform[i]));
            var nbr = Neighbours(face);
            for (var pass = 0; pass < 2; pass++)
                push = [.. push.Select((p, i) => hops[i] == 0 ? 0f : MathF.Max(p, nbr[i].Count == 0 ? 0f : nbr[i].Average(j => push[j])))];
            for (var i = 0; i < face.VertexCount; i++) conform[i] += push[i];
        }

        // ── Cut the character's own face out along the face's outline ────────────────────────────────
        // **Hidden whole where inside, clipped where straddling.** Hiding by centroid left the
        // character's large hairline triangles half over the face's forehead, in front of it by more
        // than any sort bias could settle — dark wedges on the brow. Clipping them removes that.
        //
        // **Clipped a little inside the outline, not on it**, so a band of the character's surface
        // runs under the face's rim. The rim meets that surface exactly at its own vertices and only
        // approximately between them, and where the surface is steep — the side of the face, seen
        // nearly edge-on from the front — the difference opened as dark cracks once the head turned.
        // The band fills them, and the flush seat above keeps it behind the face everywhere else.
        // Only the surface nearest the viewer is cut: the back of the head lies inside the outline too.
        var depth = FaceBake.FrontDepth(bodyBind, body.Indices, Expand(outline, 1.3f), front);
        var (bodyUvs, atlas, faceUv) = Compose(body, face, setup.Skin, opt);
        var cut = Cut(bodyBind, body.Indices, bodyUvs, Expand(outline, 1f - MeshToolkit.Num(opt, "overlap", 0.05f)), depth, front);
        if (cut.Carriers.Length < 3)
            throw new ArgumentException(
                "The face covers none of this character's own face, so there is nothing for it to ride on. "
                + "Check the eyes and mouth: mesh.faceSheet().anchors shows where they were found.");

        if (B + cut.Derived.Count + face.VertexCount > ushort.MaxValue)
            throw new ArgumentException(
                $"The character, the cut and the face come to {B + cut.Derived.Count + face.VertexCount:N0} "
                + $"vertices, past the mesh buffer's {ushort.MaxValue:N0}. Decimate the character first.");

        var attachment = new FaceAttachment
        {
            BodyCount = B, Derived = [.. cut.Derived], Face = face, BodyBind = bodyBind, Align = align,
            DepthScale = k, DepthOffset = offset, FrontSign = front, Carriers = cut.Carriers, Conform = conform,
            SortBias = 0.05f * k * FaceHeight(fv)
        };

        // ── One mesh: the body, the vertices its clipping made, then the face ─────────────────────────
        var start = attachment.FaceStart;
        var verts = new SKPoint3[start + face.VertexCount];
        Array.Copy(bodyBind, verts, B);
        attachment.Derive(bodyBind, verts);
        for (var i = 0; i < face.VertexCount; i++) verts[start + i] = attachment.Place(fv[i], i);

        var uvs = new SKPoint[verts.Length];
        Array.Copy(bodyUvs, uvs, B);
        for (var i = 0; i < cut.Derived.Count; i++) uvs[B + i] = cut.DerivedUvs[i];
        for (var i = 0; i < face.VertexCount; i++) uvs[start + i] = faceUv(face.Uvs[i]);

        List<ushort> tris = [.. cut.Triangles.Select(i => (ushort)i)];
        tris.AddRange(face.Indices.Select(i => (ushort)(i + start)));

        return new FaceMesh(verts, uvs, [.. tris], true,
                            $"{body.Source} wearing {face.Source}; {cut.Hidden} of its own face triangles hidden, {cut.Clipped} clipped")
        { Texture = atlas, Rig = body.Rig, Attachment = attachment };
    }

    /// <summary>The rigid motion taking one point set onto another: Horn's quaternion method.</summary>
    /// <remarks>
    /// Closed form, so no iteration can land in a local minimum. The rotation is the eigenvector of the
    /// largest eigenvalue of a symmetric 4×4 built from the cross-covariance (Horn, <i>Closed-form
    /// solution of absolute orientation using unit quaternions</i>, JOSA A 4(4), 1987).
    /// </remarks>
    internal static (Quaternion Rotation, Vector3 Translation) Rigid(SKPoint3[] from, SKPoint3[] to)
    {
        double cfx = 0, cfy = 0, cfz = 0, ctx = 0, cty = 0, ctz = 0;
        for (var i = 0; i < from.Length; i++)
        {
            cfx += from[i].X; cfy += from[i].Y; cfz += from[i].Z;
            ctx += to[i].X; cty += to[i].Y; ctz += to[i].Z;
        }
        double n = from.Length;
        cfx /= n; cfy /= n; cfz /= n; ctx /= n; cty /= n; ctz /= n;

        double sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
        for (var i = 0; i < from.Length; i++)
        {
            double ax = from[i].X - cfx, ay = from[i].Y - cfy, az = from[i].Z - cfz;
            double bx = to[i].X - ctx, by = to[i].Y - cty, bz = to[i].Z - ctz;
            sxx += ax * bx; sxy += ax * by; sxz += ax * bz;
            syx += ay * bx; syy += ay * by; syz += ay * bz;
            szx += az * bx; szy += az * by; szz += az * bz;
        }

        double[,] m =
        {
            { sxx + syy + szz, syz - szy, szx - sxz, sxy - syx },
            { syz - szy, sxx - syy - szz, sxy + syx, szx + sxz },
            { szx - sxz, sxy + syx, -sxx + syy - szz, syz + szy },
            { sxy - syx, szx + sxz, syz + szy, -sxx - syy + szz }
        };
        var q = LargestEigenvector(m);
        var rotation = Quaternion.Normalize(new Quaternion((float)q[1], (float)q[2], (float)q[3], (float)q[0]));
        var translation = new Vector3((float)ctx, (float)cty, (float)ctz)
                        - Vector3.Transform(new Vector3((float)cfx, (float)cfy, (float)cfz), rotation);
        return (rotation, translation);
    }
    #endregion

    #region Methods (private)
    /// <summary>The character's texture at the left of one atlas and the face's at the right, tone-matched.</summary>
    static (SKPoint[] BodyUvs, SKBitmap Atlas, Func<SKPoint, SKPoint> FaceUv) Compose(
        FaceMesh mesh, FaceMesh face, string skin, IDictionary? opt)
    {
        var body = mesh.Texture!;
        var ft = face.Texture!;
        int bw = body.Width, bh = body.Height, fw = ft.Width, fh = ft.Height;
        int W = bw + fw, H = Math.Max(bh, fh);
        var atlas = new SKBitmap(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var c = new SKCanvas(atlas))
        {
            c.Clear(SKColors.Transparent);
            c.DrawBitmap(body, 0, 0);

            // **The face is tone-matched to the character, not the other way round.** A turnaround is
            // usually line art on white paper, so its "skin" is the paper; drawn as it is, the face is
            // a white mask on a grey head. Scaling each channel so the face's skin lands on the
            // character's keeps the linework and changes only the ground.
            using var paint = new SKPaint();
            var match = opt is null || !opt.Contains("matchSkin") || Convert.ToBoolean(opt["matchSkin"]);
            if (match && SKColor.TryParse(skin, out var target))
            {
                var from = FaceSkin(face);
                float Gain(byte to, byte fromc) => Math.Clamp(to / MathF.Max(8f, fromc), 0.2f, 2.5f);
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(
                [
                    Gain(target.Red, from.Red), 0, 0, 0, 0,
                    0, Gain(target.Green, from.Green), 0, 0, 0,
                    0, 0, Gain(target.Blue, from.Blue), 0, 0,
                    0, 0, 0, 1, 0
                ]);
            }

            c.DrawBitmap(ft, bw, 0, paint);
        }

        SKPoint BodyUv(SKPoint uv) => new(uv.X * bw / W, 1f - ((1f - uv.Y) * bh / H));
        SKPoint FaceUv(SKPoint uv) => new((bw + (uv.X * fw)) / W, 1f - ((1f - uv.Y) * fh / H));
        return ([.. mesh.Reference.Select((_, i) => BodyUv(mesh.Uvs[i]))], atlas, FaceUv);
    }

    /// <summary>The face's own skin tone, as the per-channel median over the cheeks, forehead and brow.</summary>
    static SKColor FaceSkin(FaceMesh face)
    {
        var t = face.Texture!;
        var samples = SkinLandmarks.Where(i => i < face.VertexCount).Select(i =>
        {
            var uv = face.Uvs[i];
            return t.GetPixel(Math.Clamp((int)(uv.X * t.Width), 0, t.Width - 1),
                              Math.Clamp((int)((1f - uv.Y) * t.Height), 0, t.Height - 1));
        }).ToArray();
        byte Median(Func<SKColor, byte> ch) => samples.Select(ch).OrderBy(b => b).ElementAt(samples.Length / 2);
        return new SKColor(Median(c => c.Red), Median(c => c.Green), Median(c => c.Blue));
    }

    static IDictionary? AnchorOptions(IDictionary? opt)
    {
        if (opt is null) return null;
        Dictionary<string, object?> a = [];
        foreach (var key in FaceBake.AnchorOptions)
            if (opt.Contains(key)) a[key] = opt[key];
        return a.Count == 0 ? null : a;
    }

    static double[] LargestEigenvector(double[,] a)
    {
        const int N = 4;
        var v = new double[N, N];
        for (var i = 0; i < N; i++) v[i, i] = 1;

        for (var sweep = 0; sweep < 64; sweep++)
        {
            double off = 0;
            for (var p = 0; p < N; p++)
                for (var q = p + 1; q < N; q++) off += a[p, q] * a[p, q];
            if (off < 1e-24) break;

            for (var p = 0; p < N; p++)
                for (var q = p + 1; q < N; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-30) continue;
                    var theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    var t = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt((theta * theta) + 1));
                    var c = 1 / Math.Sqrt((t * t) + 1);
                    var s = t * c;
                    for (var k = 0; k < N; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = (c * akp) - (s * akq);
                        a[k, q] = (s * akp) + (c * akq);
                    }
                    for (var k = 0; k < N; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = (c * apk) - (s * aqk);
                        a[q, k] = (s * apk) + (c * aqk);
                    }
                    for (var k = 0; k < N; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = (c * vkp) - (s * vkq);
                        v[k, q] = (s * vkp) + (c * vkq);
                    }
                }
        }

        var best = 0;
        for (var i = 1; i < N; i++) if (a[i, i] > a[best, best]) best = i;
        return [v[0, best], v[1, best], v[2, best], v[3, best]];
    }

    /// <summary>The frontmost surface of a mesh, seen from the front, at any front-frame point.</summary>
    /// <remarks>Exact rather than rasterised, because the face's rim is laid onto it and a cell's error shows as a seam.</remarks>
    sealed class Surface(SKPoint3[] v, ushort[] idx, int front)
    {
        internal float At(SKPoint p)
        {
            var best = float.NaN;
            for (var t = 0; t < idx.Length; t += 3)
            {
                SKPoint3 a = v[idx[t]], b = v[idx[t + 1]], c = v[idx[t + 2]];
                if (!Barycentric(p, Uv(a, front), Uv(b, front), Uv(c, front), out var w)) continue;
                var d = ((w.X * a.Z) + (w.Y * b.Z) + (w.Z * c.Z)) * front;
                if (float.IsNaN(best) || d > best) best = d;
            }
            return best;
        }
    }

    /// <summary>What cutting the character's face out left: its triangles, and the vertices the cut made.</summary>
    sealed class CutResult
    {
        internal List<int> Triangles = [];
        internal List<FaceAttachment.Blend> Derived = [];
        internal List<SKPoint> DerivedUvs = [];
        internal int[] Carriers = [];
        internal int Hidden, Clipped;
    }

    static CutResult Cut(SKPoint3[] v, ushort[] idx, SKPoint[] uvs, SKPoint[] outline, FaceBake.DepthBuffer depth, int front)
    {
        var result = new CutResult();
        var carriers = new HashSet<int>();
        var B = v.Length;

        // Path ops are float and the model is small, so the cut is done at a working scale.
        float x0 = outline.Min(p => p.X), x1 = outline.Max(p => p.X), y0 = outline.Min(p => p.Y), y1 = outline.Max(p => p.Y);
        var S = 4096f / MathF.Max(x1 - x0, y1 - y0);
        SKPoint Up(SKPoint p) => new(p.X * S, p.Y * S);
        using var mask = Polygon(outline.Select(Up));
        var grown = new SKRect(x0, y0, x1, y1);
        grown.Inflate((x1 - x0) * 0.02f, (y1 - y0) * 0.02f);

        for (var t = 0; t < idx.Length; t += 3)
        {
            int ia = idx[t], ib = idx[t + 1], ic = idx[t + 2];
            SKPoint3 a = v[ia], b = v[ib], c = v[ic];
            SKPoint pa = Uv(a, front), pb = Uv(b, front), pc = Uv(c, front);
            var cross = ((pb.X - pa.X) * (pc.Y - pa.Y)) - ((pb.Y - pa.Y) * (pc.X - pa.X));
            var tbox = new SKRect(MathF.Min(pa.X, MathF.Min(pb.X, pc.X)), MathF.Min(pa.Y, MathF.Min(pb.Y, pc.Y)),
                                  MathF.Max(pa.X, MathF.Max(pb.X, pc.X)), MathF.Max(pa.Y, MathF.Max(pb.Y, pc.Y)));

            if (cross <= 0f || !tbox.IntersectsWith(grown) || !depth.IsFront(a, b, c))
            {
                result.Triangles.AddRange([ia, ib, ic]);
                continue;
            }

            using var tri = Polygon([Up(pa), Up(pb), Up(pc)]);
            using var rest = new SKPath();
            if (!tri.Op(mask, SKPathOp.Difference, rest))
            {
                result.Triangles.AddRange([ia, ib, ic]);
                continue;
            }

            var pieces = Contours(rest).Where(q => MathF.Abs(Area(q)) > 1e-3f).ToList();
            var left = pieces.Sum(q => MathF.Abs(Area(q)));
            var whole = MathF.Abs(cross) * S * S / 2f;
            if (left >= whole * 0.999f)
            {
                result.Triangles.AddRange([ia, ib, ic]);
                continue;
            }

            carriers.Add(ia); carriers.Add(ib); carriers.Add(ic);
            if (pieces.Count == 0) { result.Hidden++; continue; }

            result.Clipped++;
            foreach (var piece in pieces)
                foreach (var (p, q, r) in EarClip(piece))
                    foreach (var point in new[] { p, q, r })
                    {
                        Barycentric(new SKPoint(point.X / S, point.Y / S), pa, pb, pc, out var w, clamp: true);
                        result.Triangles.Add(B + result.Derived.Count);
                        result.Derived.Add(new FaceAttachment.Blend(ia, ib, ic, w.X, w.Y, w.Z));
                        result.DerivedUvs.Add(new SKPoint((w.X * uvs[ia].X) + (w.Y * uvs[ib].X) + (w.Z * uvs[ic].X),
                                                          (w.X * uvs[ia].Y) + (w.Y * uvs[ib].Y) + (w.Z * uvs[ic].Y)));
                    }
        }

        result.Carriers = [.. carriers];
        return result;
    }

    /// <summary>The face's outer silhouette as an ordered loop, in the character's front frame.</summary>
    static SKPoint[] Outline(FaceMesh face, SKPoint[] placed)
    {
        Dictionary<(int, int), int> edges = [];
        var idx = face.Indices;
        for (var t = 0; t < idx.Length; t += 3)
            for (var k = 0; k < 3; k++)
            {
                int a = idx[t + k], b = idx[t + ((k + 1) % 3)];
                var key = a < b ? (a, b) : (b, a);
                edges[key] = edges.TryGetValue(key, out var n) ? n + 1 : 1;
            }

        var next = new Dictionary<int, List<int>>();
        void Link(int a, int b)
        {
            if (!next.TryGetValue(a, out var list)) next[a] = list = [];
            list.Add(b);
        }
        foreach (var ((a, b), n) in edges)
            if (n == 1) { Link(a, b); Link(b, a); }

        // Every loop, and the longest is the outer one: a face mesh may have eye or mouth holes.
        var seen = new HashSet<int>();
        List<int> best = [];
        foreach (var s in next.Keys)
        {
            if (!seen.Add(s)) continue;
            List<int> loop = [s];
            int prev = -1, cur = s;
            while (true)
            {
                var n = next[cur].FirstOrDefault(j => j != prev && !seen.Contains(j), -1);
                if (n < 0) break;
                loop.Add(n); seen.Add(n); prev = cur; cur = n;
            }
            if (loop.Count > best.Count) best = loop;
        }

        if (best.Count < 3) throw new ArgumentException($"The face '{face.Source}' has no outer boundary, so it cannot be cut into a head.");
        return [.. best.Select(i => placed[i])];
    }

    static SKPoint Uv(SKPoint3 p, int front) => new(p.X * front, p.Y);

    static bool Barycentric(SKPoint p, SKPoint a, SKPoint b, SKPoint c, out Vector3 w, bool clamp = false)
    {
        var den = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
        if (MathF.Abs(den) < 1e-14f) { w = new Vector3(1f / 3f); return false; }
        var w1 = (((b.Y - c.Y) * (p.X - c.X)) + ((c.X - b.X) * (p.Y - c.Y))) / den;
        var w2 = (((c.Y - a.Y) * (p.X - c.X)) + ((a.X - c.X) * (p.Y - c.Y))) / den;
        var w3 = 1f - w1 - w2;
        if (clamp)
        {
            // A clipped point sits on the triangle's own edge; float error must not move it off.
            w1 = MathF.Max(0f, w1); w2 = MathF.Max(0f, w2); w3 = MathF.Max(0f, w3);
            var sum = w1 + w2 + w3;
            w = new Vector3(w1 / sum, w2 / sum, w3 / sum);
            return true;
        }
        w = new Vector3(w1, w2, w3);
        const float e = -1e-5f;
        return w1 >= e && w2 >= e && w3 >= e;
    }

    static SKPath Polygon(IEnumerable<SKPoint> points)
    {
        var path = new SKPath();
        var first = true;
        foreach (var p in points) { if (first) path.MoveTo(p); else path.LineTo(p); first = false; }
        path.Close();
        return path;
    }

    static List<List<SKPoint>> Contours(SKPath path)
    {
        List<List<SKPoint>> all = [];
        List<SKPoint>? cur = null;
        using var it = path.CreateRawIterator();
        var pts = new SKPoint[4];
        SKPathVerb verb;
        while ((verb = it.Next(pts)) != SKPathVerb.Done)
            switch (verb)
            {
                case SKPathVerb.Move:
                    if (cur is { Count: >= 3 }) all.Add(cur);
                    cur = [pts[0]];
                    break;
                case SKPathVerb.Line:
                    cur?.Add(pts[1]);
                    break;
                case SKPathVerb.Close:
                    if (cur is { Count: >= 3 }) all.Add(cur);
                    cur = null;
                    break;
            }
        if (cur is { Count: >= 3 }) all.Add(cur);

        // Drop repeated points, including a closing point that returns to the start.
        return [.. all.Select(c => c.Where((p, i) => SKPoint.Distance(p, c[(i + 1) % c.Count]) > 1e-4f).ToList())
                       .Where(c => c.Count >= 3)];
    }

    static float Area(List<SKPoint> poly)
    {
        var a = 0f;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            a += (poly[j].X * poly[i].Y) - (poly[i].X * poly[j].Y);
        return a / 2f;
    }

    /// <summary>A simple polygon as triangles, counter-clockwise.</summary>
    static IEnumerable<(SKPoint, SKPoint, SKPoint)> EarClip(List<SKPoint> poly)
    {
        var p = Area(poly) < 0f ? Enumerable.Reverse(poly).ToList() : [.. poly];
        static float Cross(SKPoint o, SKPoint a, SKPoint b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));

        var guard = p.Count * p.Count;
        while (p.Count > 3 && guard-- > 0)
        {
            var clipped = false;
            for (var i = 0; i < p.Count; i++)
            {
                SKPoint a = p[(i + p.Count - 1) % p.Count], b = p[i], c = p[(i + 1) % p.Count];
                if (Cross(a, b, c) <= 1e-6f) continue;
                var ear = true;
                for (var j = 0; j < p.Count && ear; j++)
                {
                    var q = p[j];
                    if (q == a || q == b || q == c) continue;
                    ear = !(Cross(a, b, q) >= 0 && Cross(b, c, q) >= 0 && Cross(c, a, q) >= 0);
                }
                if (!ear) continue;
                yield return (a, b, c);
                p.RemoveAt(i);
                clipped = true;
                break;
            }

            // A polygon with no ear is degenerate at this precision; fan the rest rather than lose it.
            if (!clipped) break;
        }

        for (var i = 1; i + 1 < p.Count; i++) yield return (p[0], p[i], p[i + 1]);
    }

    static List<int>[] Neighbours(FaceMesh face)
    {
        var nbr = Enumerable.Range(0, face.VertexCount).Select(_ => new HashSet<int>()).ToArray();
        var idx = face.Indices;
        for (var t = 0; t < idx.Length; t += 3)
            for (var k = 0; k < 3; k++)
            {
                int a = idx[t + k], b = idx[t + ((k + 1) % 3)];
                nbr[a].Add(b);
                nbr[b].Add(a);
            }
        return [.. nbr.Select(s => s.ToList())];
    }

    /// <summary>How many edges each vertex is from the face's boundary.</summary>
    static int[] Hops(FaceMesh face)
    {
        var n = face.VertexCount;
        var nbr = Neighbours(face);

        var hops = Enumerable.Repeat(int.MaxValue, n).ToArray();
        var queue = new Queue<int>();
        foreach (var b in face.Boundary()) { hops[b] = 0; queue.Enqueue(b); }
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            foreach (var j in nbr[i])
                if (hops[j] > hops[i] + 1) { hops[j] = hops[i] + 1; queue.Enqueue(j); }
        }
        return hops;
    }

    static SKPoint3 Mid(SKPoint3 a, SKPoint3 b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f, (a.Z + b.Z) / 2f);

    static SKPoint Flat(SKPoint3 p) => new(p.X, p.Y);

    static (SKPoint3, SKPoint3) Order(SKPoint3 a, SKPoint3 b) => a.X <= b.X ? (a, b) : (b, a);

    static (SKPoint, SKPoint) Order(SKPoint a, SKPoint b) => a.X <= b.X ? (a, b) : (b, a);

    static float FaceHeight(SKPoint3[] v) => v.Max(p => p.Y) - v.Min(p => p.Y);

    static SKPoint[] Expand(SKPoint[] poly, float by)
    {
        var c = new SKPoint(poly.Average(p => p.X), poly.Average(p => p.Y));
        return [.. poly.Select(p => new SKPoint(c.X + ((p.X - c.X) * by), c.Y + ((p.Y - c.Y) * by)))];
    }
    #endregion

    #region Fields
    internal static readonly string[] Options = ["eyeLeft", "eyeRight", "mouth", "matchSkin", "depth", "blend", "flush", "overlap"];

    /// <summary>Cheeks, forehead and glabella on the canonical topology: skin, away from any feature.</summary>
    static readonly int[] SkinLandmarks = [50, 280, 101, 330, 151, 9, 108, 337, 205, 425];
    #endregion
}
