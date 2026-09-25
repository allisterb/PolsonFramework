namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SkiaSharp;

/// <summary>
/// A drawing surface lined up to one character's face, ready to bake with <c>mesh.withFace(...)</c>.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>The alignment lives in <see cref="Context"/>, not in the head.</b> <see cref="Head"/> is an
/// ordinary Loomis head at yaw 0, and the context already carries the transform that lands its eyes
/// and mouth on the character's. Draw the head's features with the usual calls on that context and
/// they arrive in the right place; reset its transform and they will not.
/// </para>
/// </remarks>
public sealed class FaceSheet
{
    #region Constructors
    internal FaceSheet(FaceBake.Setup setup)
    {
        Setup = setup;
        Canvas = new SkiaCanvas(setup.Width, setup.Height);
        Canvas.SkCanvas.DrawBitmap(setup.Base, 0, 0);
        Context = Canvas.GetContext("2d");
        var t = setup.HeadTransform;
        Context.SetTransform(t.ScaleX, t.SkewY, t.SkewX, t.ScaleY, t.TransX, t.TransY);
        Head = FaceBake.Drawing.CreateLoomisHead(0f, 0f, setup.HeadHeight, 0f);
    }
    #endregion

    #region Properties
    /// <summary>The sheet's size in pixels.</summary>
    public int Width => Setup.Width;

    /// <summary>The sheet's size in pixels.</summary>
    public int Height => Setup.Height;

    /// <summary>The sheet itself. It already holds the character's face with its old features erased.</summary>
    public SkiaCanvas Canvas { get; }

    /// <summary>A context on <see cref="Canvas"/> that already carries the alignment to this face.</summary>
    public CanvasRenderingContext2D Context { get; }

    /// <summary>A Loomis head at yaw 0 whose eyes and mouth land on the character's through <see cref="Context"/>.</summary>
    public Dictionary<string, object?> Head { get; }

    /// <summary>The skin tone sampled from the character, as <c>#RRGGBB</c>.</summary>
    public string Skin => Setup.Skin;

    /// <summary>Whether the character's texture is greyscale, so drawn features should be too.</summary>
    public bool Greyscale => Setup.Greyscale;

    /// <summary>How the face was found: <c>detected</c> or <c>anchors</c>.</summary>
    public string Found => Setup.Found;

    /// <summary>Where the eyes and mouth are in the model's own space, as <c>{ x, y }</c>.</summary>
    public Dictionary<string, object?> Anchors => new()
    {
        ["eyeLeft"] = Pt(Setup.ModelEyeLeft),
        ["eyeRight"] = Pt(Setup.ModelEyeRight),
        ["mouth"] = Pt(Setup.ModelMouth)
    };
    #endregion

    #region Internal
    internal FaceBake.Setup Setup { get; }

    static Dictionary<string, object?> Pt(SKPoint p) => new() { ["x"] = p.X, ["y"] = p.Y };
    #endregion
}

/// <summary>
/// Bakes a drawn face into a mesh's texture atlas, so the face rides the rig at any head angle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> A script reaches this through <c>mesh.faceSheet()</c>,
/// <c>mesh.withFace(...)</c> and <c>mesh.withExpression(...)</c>.
/// </para>
/// <para>
/// <b>Why bake rather than overlay.</b> A generated character's face is paint on a few hundred
/// triangles — measured on a TRELLIS/UniRig figure: ~280 triangles and ~124 px of texture for the
/// whole face — so there is no geometry to deform into an expression. Drawing the features and
/// baking them into the atlas puts them on the head itself: they turn, nod, tilt and occlude with
/// the rig, and nothing per panel can fall out of alignment the way a 2D overlay can.
/// </para>
/// <para>
/// <b>What it cannot do.</b> Measured on a TRELLIS/UniRig figure, a baked face is sound to 60° of
/// head turn, smears at 75° and is gone at 90° — and the model's own painted face fails at the same
/// angles, because a generated face is close to a flat plane: a profile has no silhouette to read.
/// And a baked face changes the picture, never the shape — a dropped jaw moves no silhouette.
/// </para>
/// </remarks>
internal static class FaceBake
{
    #region Types
    /// <summary>Everything about one character's face that does not depend on what is drawn on it.</summary>
    internal sealed class Setup
    {
        internal required SKPoint3[] Key;
        internal required int FrontSign;
        internal required float K, MinU, MaxV;
        internal required int Width, Height;
        internal required SKPoint[] Hull;
        internal required SKBitmap Base;
        internal required string Skin;
        internal required bool Greyscale;
        internal required string Found;
        internal required SKMatrix HeadTransform;
        internal required float HeadHeight;
        internal required SKPoint ModelEyeLeft, ModelEyeRight, ModelMouth;

        /// <summary>Model space to sheet pixels, through the front view.</summary>
        internal SKPoint ToSheet(SKPoint3 v) => new(((v.X * FrontSign) - MinU) * K, (MaxV - v.Y) * K);
    }
    #endregion

    #region Methods
    /// <summary>The face setup for a mesh, found once per character and cached on its bind geometry.</summary>
    internal static Setup For(FaceMesh mesh, IDictionary? anchors)
    {
        Require(mesh);
        if (anchors is not null && anchors.Count > 0) return Build(mesh, anchors);
        return Cache.GetValue(mesh.Reference, _ => Build(mesh, null));
    }

    /// <summary>Bakes <paramref name="sheet"/> into a copy of the mesh's atlas and returns the atlas.</summary>
    internal static SKBitmap Bake(FaceMesh mesh, FaceSheet sheet, int resolution)
    {
        Require(mesh);
        var s = sheet.Setup;
        if (!ReferenceEquals(s.Key, mesh.Reference))
            throw new ArgumentException(
                "That face sheet was made for a different mesh. Ask this mesh for its own: mesh.faceSheet().");

        var tex = mesh.Texture!;
        var res = resolution > 0 ? Math.Clamp(resolution, 256, 8192) : Math.Min(4096, Math.Max(2048, tex.Width * 4));
        var atlas = new SKBitmap(new SKImageInfo(res, res, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(atlas);
        using (var img = SKImage.FromBitmap(tex))
            canvas.DrawImage(img, new SKRect(0, 0, res, res), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using var src = SKImage.FromBitmap(sheet.Canvas.SkBitmap);
        using var paint = new SKPaint { IsAntialias = true };
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        var baked = 0;
        var v = mesh.Reference;
        var idx = mesh.Indices;
        var candidates = new List<int>();
        for (var i = 0; i < idx.Length; i += 3)
        {
            SKPoint3 a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];

            // Facing: the normal's component toward the viewer. Side and back triangles keep their
            // own texture, which is what stops the front drawing smearing round the head.
            float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            float nx = (uy * vz) - (uz * vy), ny = (uz * vx) - (ux * vz), nz = (ux * vy) - (uy * vx);
            var len = MathF.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
            if (len <= 0f || nz * s.FrontSign / len < FacingThreshold) continue;

            var centre = new SKPoint(((a.X + b.X + c.X) / 3f) * s.FrontSign, (a.Y + b.Y + c.Y) / 3f);
            if (Inside(s.Hull, centre)) candidates.Add(i);
        }

        // **Only the surface nearest the viewer takes the face.** Facing and the outline are not
        // enough: a crossbow slung behind the head faces forward and sits inside the face's outline
        // from the front, so it was baked with skin — hidden at 0° and revealed the moment the head
        // turned, as a pale wedge behind it. A small depth buffer over the face keeps the frontmost.
        var front = FrontDepth(mesh.Reference, mesh.Indices, s.Hull, s.FrontSign);
        foreach (var i in candidates)
        {
            SKPoint3 a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];
            if (!front.IsFront(a, b, c)) continue;

            SKPoint[] from = [s.ToSheet(a), s.ToSheet(b), s.ToSheet(c)];
            SKPoint[] to = [Atlas(mesh, idx[i], res), Atlas(mesh, idx[i + 1], res), Atlas(mesh, idx[i + 2], res)];
            if (!TryAffine(from, to, out var map)) continue;

            canvas.Save();
            canvas.ClipPath(Grown(to, 1.5f), SKClipOperation.Intersect, antialias: true);
            canvas.SetMatrix(map);
            canvas.DrawImage(src, 0, 0, sampling, paint);
            canvas.Restore();
            baked++;
        }

        if (baked == 0)
        {
            atlas.Dispose();
            throw new ArgumentException(
                "No forward-facing triangle of this mesh lies inside the face, so there was nothing to bake. "
                + "Check that the character faces +Z or -Z, and that the face sheet was found on this mesh.");
        }

        return atlas;
    }

    /// <summary>The nearest surface over the face region, seen from the front.</summary>
    internal sealed class DepthBuffer
    {
        internal required float U0, V0, Cell, Epsilon;
        internal required int N;
        internal required int FrontSign;
        internal required float[] Near;

        /// <summary>Whether a triangle's centre is on the nearest surface at that point.</summary>
        internal bool IsFront(SKPoint3 a, SKPoint3 b, SKPoint3 c)
        {
            var u = ((a.X + b.X + c.X) / 3f) * FrontSign;
            var v = (a.Y + b.Y + c.Y) / 3f;
            var d = ((a.Z + b.Z + c.Z) / 3f) * FrontSign;
            int x = (int)((u - U0) / Cell), y = (int)((v - V0) / Cell);
            if (x < 0 || y < 0 || x >= N || y >= N) return true;
            var near = Near[(y * N) + x];
            return float.IsNegativeInfinity(near) || d >= near - Epsilon;
        }

        /// <summary>
        /// The nearest surface's depth at a front-frame point (<c>x · front</c>, <c>y</c>), in front-frame
        /// depth (<c>z · front</c>); NaN outside the buffer or where no surface covers it.
        /// </summary>
        internal float At(float u, float v)
        {
            int x = (int)((u - U0) / Cell), y = (int)((v - V0) / Cell);
            if (x < 0 || y < 0 || x >= N || y >= N) return float.NaN;
            var near = Near[(y * N) + x];
            return float.IsNegativeInfinity(near) ? float.NaN : near;
        }
    }

    /// <summary>
    /// Rasterises every triangle over a region into a coarse depth buffer, seen from the front — all
    /// of them, so a surface that is edge-on to the viewer still hides what is behind it.
    /// </summary>
    /// <param name="hull">The region, in the front frame: <c>(x · front, y)</c>.</param>
    internal static DepthBuffer FrontDepth(SKPoint3[] v, ushort[] idx, SKPoint[] hull, int frontSign)
    {
        const int N = 160;
        float u0 = hull.Min(p => p.X), u1 = hull.Max(p => p.X);
        float v0 = hull.Min(p => p.Y), v1 = hull.Max(p => p.Y);
        var cell = MathF.Max(u1 - u0, v1 - v0) / N;
        var near = Enumerable.Repeat(float.NegativeInfinity, N * N).ToArray();

        for (var i = 0; i < idx.Length; i += 3)
        {
            SKPoint3 a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];
            float au = a.X * frontSign, bu = b.X * frontSign, cu = c.X * frontSign;
            float ad = a.Z * frontSign, bd = b.Z * frontSign, cd = c.Z * frontSign;
            int x0 = Math.Max(0, (int)((MathF.Min(au, MathF.Min(bu, cu)) - u0) / cell));
            int x1 = Math.Min(N - 1, (int)((MathF.Max(au, MathF.Max(bu, cu)) - u0) / cell));
            int y0 = Math.Max(0, (int)((MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) - v0) / cell));
            int y1 = Math.Min(N - 1, (int)((MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) - v0) / cell));
            if (x0 > x1 || y0 > y1) continue;

            var den = ((b.Y - c.Y) * (au - cu)) + ((cu - bu) * (a.Y - c.Y));
            if (MathF.Abs(den) < 1e-12f) continue;

            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    float pu = u0 + ((x + 0.5f) * cell), pv = v0 + ((y + 0.5f) * cell);
                    var w1 = (((b.Y - c.Y) * (pu - cu)) + ((cu - bu) * (pv - c.Y))) / den;
                    var w2 = (((c.Y - a.Y) * (pu - cu)) + ((au - cu) * (pv - c.Y))) / den;
                    var w3 = 1f - w1 - w2;
                    if (w1 < 0f || w2 < 0f || w3 < 0f) continue;
                    var d = (w1 * ad) + (w2 * bd) + (w3 * cd);
                    ref var slot = ref near[(y * N) + x];
                    if (d > slot) slot = d;
                }
        }

        return new DepthBuffer
        {
            U0 = u0, V0 = v0, Cell = cell, N = N, FrontSign = frontSign, Near = near,
            Epsilon = (u1 - u0) * 0.03f
        };
    }

    /// <summary>Draws a named expression, or a set of Action Units, onto a sheet in the character's palette.</summary>
    internal static void DrawExpression(FaceSheet sheet, object expression, float amount, IDictionary? opt)
    {
        var head = sheet.Head;
        if (expression is string name)
            head = Drawing.ApplyFacialExpression(head, name, amount);
        else if (JsInterop.AsDict(expression) is IDictionary units)
        {
            var scaled = new Dictionary<string, object?>();
            foreach (DictionaryEntry e in units)
                scaled[e.Key.ToString()!] = Convert.ToSingle(e.Value, System.Globalization.CultureInfo.InvariantCulture) * amount;
            head = Drawing.ApplyActionUnits(head, scaled);
        }
        else
            throw new ArgumentException(
                "withExpression takes an expression name, such as 'anger', or Action Units, such as { AU12: 0.8 }.");

        var ink = opt?["inkColor"]?.ToString() ?? "#1a1a1e";
        var weight = MeshToolkit.Num(opt, "weight", 1.5f);
        var ctx = sheet.Context;
        var grey = sheet.Greyscale;

        Drawing.DrawComicBrow(ctx, head["farBrow"]!, true, new Dictionary<string, object?> { ["inkColor"] = ink, ["thickness"] = 1.3f });
        Drawing.DrawComicBrow(ctx, head["nearBrow"]!, false, new Dictionary<string, object?> { ["inkColor"] = ink, ["thickness"] = 1.3f });

        var eye = new Dictionary<string, object?> { ["inkColor"] = ink, ["weight"] = weight };
        if (grey) { eye["irisColor"] = "#55555c"; eye["scleraColor"] = "#eeeeec"; }
        Drawing.DrawComicEye(ctx, head["farEye"]!, true, eye);
        Drawing.DrawComicEye(ctx, head["nearEye"]!, false, eye);

        var nose = new Dictionary<string, object?> { ["inkColor"] = ink, ["weight"] = weight };
        if (grey) nose["shadowColor"] = "rgba(40,40,46,0.35)";
        Drawing.DrawComicNose(ctx, head["noseWedge"]!, nose);

        var mouth = new Dictionary<string, object?> { ["inkColor"] = ink, ["weight"] = weight };
        if (grey) { mouth["lipColor"] = "#8e8e94"; mouth["teethColor"] = "#eeeeec"; mouth["cavityColor"] = "#2a2a2e"; }
        Drawing.DrawComicMouth(ctx, head["mouthGuides"]!, mouth);
    }
    #endregion

    #region Methods (private)
    static void Require(FaceMesh mesh)
    {
        if (!mesh.HasUvs || mesh.Fitted || mesh.Texture is null)
            throw new ArgumentException(
                $"Mesh '{mesh.Source}' has no texture atlas to bake into. A face is baked into the atlas a "
                + ".glb or .obj carries with its own texture; a mesh textured by fitTexture(...) has none.");

        // Baking returns a mesh without the transplant, so the face mesh would silently vanish.
        if (mesh.Attachment is not null)
            throw new ArgumentException(
                "This mesh wears a transplanted face, which a baked face would replace rather than draw on. "
                + "Bake onto the character as loaded, or draw the face mesh's expressions with Mesh.draw's 'expression'.");
    }

    static Setup Build(FaceMesh mesh, IDictionary? anchors)
    {
        SKPoint eyeL, eyeR, mouth;
        SKPoint[] hull;
        List<SKPoint[]> features;
        int front;
        string found;

        if (anchors is not null && anchors.Count > 0)
        {
            MeshToolkit.RefuseUnknown(anchors, AnchorOptions, "faceSheet option");
            var l = Model(anchors, "eyeLeft");
            var r = Model(anchors, "eyeRight");
            var m = Model(anchors, "mouth");
            front = 1;
            (eyeL, eyeR) = l.X <= r.X ? (l, r) : (r, l);
            mouth = m;
            (hull, features) = Ellipses(eyeL, eyeR, mouth);
            found = "anchors";
        }
        else
        {
            if (!FaceDetector.Available)
                throw new ArgumentException(
                    $"Finding a face needs the optional face backend, which is not available here ({FaceDetector.Missing}). "
                    + "Pass the three points yourself, in the model's own space: "
                    + "mesh.faceSheet({ eyeLeft: { x, y }, eyeRight: { x, y }, mouth: { x, y } }).");

            (front, var view) = Detect(mesh);
            SKPoint At(int i) => view[i];
            SKPoint iA = view.Count >= 478 ? At(468) : Mid(At(33), At(133));
            SKPoint iB = view.Count >= 478 ? At(473) : Mid(At(362), At(263));
            (eyeL, eyeR) = iA.X <= iB.X ? (iA, iB) : (iB, iA);
            mouth = Mid(At(13), At(14));
            hull = ConvexHull(view);
            features = [.. FeatureGroups.Select(g => Expand(ConvexHull([.. g.Select(At)]), 1.45f))];
            found = "detected";
        }

        // The sheet: about 512 px across the face, padded a tenth all round.
        var minU = hull.Min(p => p.X); var maxU = hull.Max(p => p.X);
        var minV = hull.Min(p => p.Y); var maxV = hull.Max(p => p.Y);
        var faceW = maxU - minU;
        if (faceW <= 0f) throw new ArgumentException("The face found on this mesh has no width.");
        var k = 512f / faceW;
        var pad = faceW * 0.1f;
        minU -= pad; maxU += pad; minV -= pad; maxV += pad;
        int w = (int)MathF.Ceiling((maxU - minU) * k), h = (int)MathF.Ceiling((maxV - minV) * k);

        SKPoint Sheet(SKPoint p) => new((p.X - minU) * k, (maxV - p.Y) * k);

        // Base: the character's own face at sheet scale.
        var render = Render(mesh, w, h, p => Sheet(new SKPoint(p.X * front, p.Y)), front);
        var span = Distance(eyeL, eyeR);
        var probe = Sheet(new SKPoint((eyeL.X + eyeR.X) / 2f, ((eyeL.Y + eyeR.Y) / 2f) + (span * 0.35f)));
        var skin = render.GetPixel(Math.Clamp((int)probe.X, 0, w - 1), Math.Clamp((int)probe.Y, 0, h - 1));
        if (skin.Alpha < 128) skin = new SKColor(0xC8, 0xC4, 0xBE);
        skin = skin.WithAlpha(255);

        var baseBmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var c = new SKCanvas(baseBmp))
        {
            c.Clear(skin);
            c.DrawBitmap(render, 0, 0);

            // Erase only the painted features: soft patches over eyes and brows, nose and mouth, filled
            // from a heavily blurred copy of the face. Everywhere else the base IS the original, so the
            // bake has no edge of its own to show.
            using var smooth = Blurred(baseBmp, w * 0.09f);
            using var mask = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var mc = new SKCanvas(mask))
            using (var mp = new SKPaint { Color = SKColors.White, IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, w * 0.04f) })
                foreach (var f in features) mc.DrawPath(Path(f.Select(Sheet)), mp);

            using var patch = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var pc = new SKCanvas(patch))
            {
                pc.DrawBitmap(smooth, 0, 0);
                using var din = new SKPaint { BlendMode = SKBlendMode.DstIn };
                pc.DrawBitmap(mask, 0, 0, din);
            }

            c.DrawBitmap(patch, 0, 0);
        }

        render.Dispose();

        // The head: sized so its eye-to-mouth drop matches the face's, then mapped exactly by three points.
        SKPoint sl = Sheet(eyeL), sr = Sheet(eyeR), sm = Sheet(mouth);
        var probeHead = Drawing.CreateLoomisHead(0f, 0f, 1000f, 0f);
        var drop = MathF.Abs(MouthCentre(probeHead).Y - ((EyeCentre(probeHead, true).Y + EyeCentre(probeHead, false).Y) / 2f));
        var target = MathF.Abs(sm.Y - ((sl.Y + sr.Y) / 2f));
        var headH = drop > 0f ? 1000f * target / drop : 1000f;
        var head = Drawing.CreateLoomisHead(0f, 0f, headH, 0f);
        var (hA, hB) = (EyeCentre(head, true), EyeCentre(head, false));
        var (hl, hr) = hA.X <= hB.X ? (hA, hB) : (hB, hA);
        if (!TryAffine([hl, hr, MouthCentre(head)], [sl, sr, sm], out var headMap))
            throw new ArgumentException("The eyes and mouth found on this mesh are collinear, so the face cannot be lined up.");

        return new Setup
        {
            Key = mesh.Reference,
            FrontSign = front,
            K = k, MinU = minU, MaxV = maxV,
            Width = w, Height = h,
            Hull = hull,
            Base = baseBmp,
            Skin = $"#{skin.Red:X2}{skin.Green:X2}{skin.Blue:X2}",
            Greyscale = IsGreyscale(baseBmp, [.. hull.Select(Sheet)]),
            Found = found,
            HeadTransform = headMap,
            HeadHeight = headH,
            ModelEyeLeft = new SKPoint(eyeL.X * front, eyeL.Y),
            ModelEyeRight = new SKPoint(eyeR.X * front, eyeR.Y),
            ModelMouth = new SKPoint(mouth.X * front, mouth.Y)
        };
    }

    /// <summary>Finds the face by rendering the top of the model from the front and asking the detector.</summary>
    /// <remarks>
    /// The detector wants a face filling roughly 35–70% of the frame, and the head's share of the
    /// figure is not known in advance — eight heads tall or four — so progressively larger crops of
    /// the top are tried until one is found. Detection pads a face that fills the frame on its own,
    /// so erring small is the safe direction. Both facing directions are tried, because glTF puts the
    /// front on +Z but a generated mesh is not obliged to honour it.
    /// </remarks>
    static (int Front, List<SKPoint> View) Detect(FaceMesh mesh)
    {
        const int N = 640;
        var v = mesh.Reference;
        var top = v.Max(p => p.Y);
        var height = top - v.Min(p => p.Y);
        var tried = new List<string>();

        foreach (var front in new[] { 1, -1 })
            foreach (var frac in CropFractions)
            {
                var spanV = height * frac;
                var cv = top - (spanV * 0.5f);
                var band = v.Where(p => p.Y >= top - spanV).Select(p => p.X * front).OrderBy(x => x).ToArray();
                if (band.Length == 0) continue;
                var cu = band[band.Length / 2];
                var s = N / spanV;

                using var img = Render(mesh, N, N, p => new SKPoint((N / 2f) + (((p.X * front) - cu) * s), (N / 2f) - ((p.Y - cv) * s)), front, SKColors.White);
                var det = FaceDetector.Detect(new SkiaBitmapWrapper(img));
                if (!det.Found) { tried.Add($"{(front > 0 ? "+Z" : "-Z")} top {frac:P0}: {det.Reason}"); continue; }

                var view = det.Points.Select(q => new SKPoint(cu + ((q.X - (N / 2f)) / s), cv - ((q.Y - (N / 2f)) / s))).ToList();
                return (front, view);
            }

        throw new ArgumentException(
            "No face was found on this mesh from the front. Tried: " + string.Join("; ", tried)
            + ". Pass the eyes and mouth yourself: mesh.faceSheet({ eyeLeft: { x, y }, eyeRight: { x, y }, mouth: { x, y } }).");
    }

    /// <summary>An orthographic front view of the mesh's bind geometry, textured, far triangles first.</summary>
    internal static SKBitmap Render(FaceMesh mesh, int w, int h, Func<SKPoint3, SKPoint> toPx, int front, SKColor? background = null)
    {
        var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(background ?? SKColors.Transparent);

        var v = mesh.Reference;
        var tex = mesh.Texture!;
        var placed = v.Select(toPx).ToArray();
        var uvs = mesh.Uvs.Select(u => new SKPoint(u.X * tex.Width, (1f - u.Y) * tex.Height)).ToArray();

        var tris = mesh.Indices.Length / 3;
        var order = Enumerable.Range(0, tris)
            .OrderBy(t => (v[mesh.Indices[t * 3]].Z + v[mesh.Indices[(t * 3) + 1]].Z + v[mesh.Indices[(t * 3) + 2]].Z) * front)
            .ToArray();
        var sorted = new ushort[mesh.Indices.Length];
        for (var t = 0; t < order.Length; t++)
            for (var k = 0; k < 3; k++) sorted[(t * 3) + k] = mesh.Indices[(order[t] * 3) + k];

        using var shader = tex.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader, IsAntialias = true };
        using var verts = SKVertices.CreateCopy(SKVertexMode.Triangles, placed, uvs, null, sorted);
        canvas.DrawVertices(verts, SKBlendMode.Dst, paint);
        return bmp;
    }

    /// <summary>A face region and feature patches from three points, when no detector found them.</summary>
    /// <remarks>
    /// Human-average proportions, deliberately generous: the eyes sit a little above the middle of
    /// the face, the face is about 2.2 eye-spans wide, and the patches cover the brows as well as the
    /// eyes. An erase patch that is too large costs a little texture; one that is too small leaves a
    /// painted eye showing beside the drawn one.
    /// </remarks>
    static (SKPoint[] Hull, List<SKPoint[]> Features) Ellipses(SKPoint eyeL, SKPoint eyeR, SKPoint mouth)
    {
        var mid = Mid(eyeL, eyeR);
        var span = Distance(eyeL, eyeR);
        var drop = MathF.Max(1e-6f, mid.Y - mouth.Y);
        float top = mid.Y + (1.3f * drop), bottom = mouth.Y - (0.9f * drop);
        var hull = Ellipse(new SKPoint(mid.X, (top + bottom) / 2f), 1.1f * span, (top - bottom) / 2f);
        List<SKPoint[]> features =
        [
            Ellipse(new SKPoint(eyeL.X, eyeL.Y + (0.12f * span)), 0.45f * span, 0.42f * span),
            Ellipse(new SKPoint(eyeR.X, eyeR.Y + (0.12f * span)), 0.45f * span, 0.42f * span),
            Ellipse(new SKPoint(mid.X, mid.Y - (0.55f * drop)), 0.3f * span, 0.35f * drop),
            Ellipse(mouth, 0.55f * span, 0.25f * drop),
        ];
        return (hull, features);
    }

    static SKPoint[] Ellipse(SKPoint c, float rx, float ry) =>
        [.. Enumerable.Range(0, 32).Select(i => new SKPoint(c.X + (rx * MathF.Cos(i * MathF.Tau / 32f)), c.Y + (ry * MathF.Sin(i * MathF.Tau / 32f))))];

    static SKPoint Model(IDictionary d, string key)
    {
        if (!d.Contains(key) || JsInterop.AsDict(d[key]) is not IDictionary p)
            throw new ArgumentException(
                $"faceSheet needs {string.Join(", ", AnchorOptions)} together, each as {{ x, y }} in the model's own space. '{key}' is missing.");
        return new SKPoint(MeshToolkit.Num(p, "x", 0f), MeshToolkit.Num(p, "y", 0f));
    }

    static SKPoint Atlas(FaceMesh mesh, int i, int res) => new(mesh.Uvs[i].X * res, (1f - mesh.Uvs[i].Y) * res);

    static SKPoint EyeCentre(Dictionary<string, object?> head, bool far) =>
        PointOf(((IDictionary)head[far ? "farEye" : "nearEye"]!)["center"]);

    static SKPoint MouthCentre(Dictionary<string, object?> head) =>
        PointOf(((IDictionary)head["mouthGuides"]!)["center"]);

    static SKPoint PointOf(object? o) =>
        JsInterop.AsDict(o) is IDictionary p ? new SKPoint(MeshToolkit.Num(p, "x", 0f), MeshToolkit.Num(p, "y", 0f)) : SKPoint.Empty;

    static SKPoint Mid(SKPoint a, SKPoint b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

    static float Distance(SKPoint a, SKPoint b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    /// <summary>The affine map taking three points onto three others; false when the source is degenerate.</summary>
    internal static bool TryAffine(SKPoint[] s, SKPoint[] d, out SKMatrix m)
    {
        var det = ((s[1].X - s[0].X) * (s[2].Y - s[0].Y)) - ((s[2].X - s[0].X) * (s[1].Y - s[0].Y));
        if (MathF.Abs(det) < 1e-9f) { m = SKMatrix.Identity; return false; }

        var a = (((d[1].X - d[0].X) * (s[2].Y - s[0].Y)) - ((d[2].X - d[0].X) * (s[1].Y - s[0].Y))) / det;
        var c = (((d[2].X - d[0].X) * (s[1].X - s[0].X)) - ((d[1].X - d[0].X) * (s[2].X - s[0].X))) / det;
        var b = (((d[1].Y - d[0].Y) * (s[2].Y - s[0].Y)) - ((d[2].Y - d[0].Y) * (s[1].Y - s[0].Y))) / det;
        var dd = (((d[2].Y - d[0].Y) * (s[1].X - s[0].X)) - ((d[1].Y - d[0].Y) * (s[2].X - s[0].X))) / det;
        m = new SKMatrix(a, c, d[0].X - (a * s[0].X) - (c * s[0].Y),
                         b, dd, d[0].Y - (b * s[0].X) - (dd * s[0].Y),
                         0, 0, 1);
        return true;
    }

    /// <summary>A triangle grown outward about its centroid, so neighbouring bakes leave no seam.</summary>
    internal static SKPath Grown(SKPoint[] t, float by)
    {
        var c = new SKPoint((t[0].X + t[1].X + t[2].X) / 3f, (t[0].Y + t[1].Y + t[2].Y) / 3f);
        return Path(t.Select(p =>
        {
            float dx = p.X - c.X, dy = p.Y - c.Y, l = MathF.Max(1e-6f, MathF.Sqrt((dx * dx) + (dy * dy)));
            return new SKPoint(p.X + (dx / l * by), p.Y + (dy / l * by));
        }));
    }

    static SKPath Path(IEnumerable<SKPoint> pts)
    {
        var path = new SKPath();
        var first = true;
        foreach (var p in pts) { if (first) path.MoveTo(p); else path.LineTo(p); first = false; }
        path.Close();
        return path;
    }

    static SKPoint[] Expand(SKPoint[] poly, float by)
    {
        var c = new SKPoint(poly.Average(p => p.X), poly.Average(p => p.Y));
        return [.. poly.Select(p => new SKPoint(c.X + ((p.X - c.X) * by), c.Y + ((p.Y - c.Y) * by)))];
    }

    internal static SKPoint[] ConvexHull(IEnumerable<SKPoint> points)
    {
        var p = points.OrderBy(q => q.X).ThenBy(q => q.Y).ToArray();
        if (p.Length < 3) return p;
        static float Cross(SKPoint o, SKPoint a, SKPoint b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));
        var hull = new List<SKPoint>();
        foreach (var pass in new[] { p, p.Reverse().ToArray() })
        {
            var start = hull.Count;
            foreach (var q in pass)
            {
                while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], q) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(q);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return [.. hull];
    }

    internal static bool Inside(SKPoint[] poly, SKPoint q)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y)
                && q.X < ((poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y)) + poly[i].X)
                inside = !inside;
        return inside;
    }

    static SKBitmap Blurred(SKBitmap src, float sigma)
    {
        var dst = new SKBitmap(src.Info);
        using var c = new SKCanvas(dst);
        using var p = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sigma, sigma) };
        c.DrawBitmap(src, 0, 0, p);
        return dst;
    }

    /// <summary>Whether the face region is essentially without colour, by mean HSV saturation.</summary>
    static bool IsGreyscale(SKBitmap bmp, SKPoint[] region)
    {
        double sum = 0; var n = 0;
        var step = Math.Max(1, Math.Min(bmp.Width, bmp.Height) / 64);
        for (var y = 0; y < bmp.Height; y += step)
            for (var x = 0; x < bmp.Width; x += step)
            {
                if (!Inside(region, new SKPoint(x, y))) continue;
                var c = bmp.GetPixel(x, y);
                int max = Math.Max(c.Red, Math.Max(c.Green, c.Blue)), min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
                sum += max == 0 ? 0 : (max - min) / (double)max;
                n++;
            }
        return n == 0 || sum / n < 0.06;
    }
    #endregion

    #region Fields
    internal static readonly ConstructiveDrawingToolkit Drawing = new();

    internal static readonly string[] AnchorOptions = ["eyeLeft", "eyeRight", "mouth"];

    /// <summary>A triangle facing the viewer less than this (cosine) keeps its own texture.</summary>
    const float FacingThreshold = 0.25f;

    static readonly float[] CropFractions = [0.13f, 0.2f, 0.28f, 0.4f];

    static readonly ConditionalWeakTable<SKPoint3[], Setup> Cache = new();

    /// <summary>MediaPipe's landmark groups for the painted features to erase: each eye with its brow, the nose, the mouth.</summary>
    static readonly int[][] FeatureGroups =
    [
        [70, 63, 105, 66, 107, 55, 65, 52, 53, 46, 33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246],
        [300, 293, 334, 296, 336, 285, 295, 282, 283, 276, 263, 249, 390, 373, 374, 380, 381, 382, 362, 398, 384, 385, 386, 387, 388, 466],
        [1, 2, 98, 327, 168, 6, 197, 195, 5, 4, 45, 275, 220, 440, 64, 294],
        [61, 146, 91, 181, 84, 17, 314, 405, 321, 375, 291, 409, 270, 269, 267, 0, 37, 39, 40, 185],
    ];
    #endregion
}
