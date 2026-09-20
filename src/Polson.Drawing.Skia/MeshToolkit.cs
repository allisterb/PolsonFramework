namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SkiaSharp;

/// <summary>
/// A face as a textured, deformable surface: the studio's third creation route.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>What it is for, against the two routes that already exist.</b> The <i>constructed</i> route
/// (<see cref="ConstructiveDrawingToolkit.CreateLoomisHead"/>) draws a face from landmarks and is
/// fully articulate, but its turn is a screen-space approximation and it can only ever draw what we
/// wrote a rule for. The <i>arranged</i> route (<see cref="SceneToolkit"/>, with a requisitioned
/// cutout) buys a whole picture and cannot change it — a seventh expression is a new person, because
/// generation is not deterministic across calls. This route takes <b>one</b> frontal image and gives
/// it a surface: after that the same face turns, reshapes and takes an expression, and it is the
/// same face every time because there is only ever one of them.
/// </para>
/// <para>
/// <b>It ships no mesh.</b> <see cref="Load"/> reads an OBJ the caller supplies, contained exactly as
/// <c>outFile</c> is. That is deliberate: the two face meshes this studio has read carry different
/// terms — MediaPipe's canonical model is Apache 2.0 and genuinely usable with attribution, while
/// CANDIDE-3's data states no licence at all — and committing either one into this assembly is a
/// redistribution decision for the director rather than a default for a toolkit. See the ledger rows
/// in <c>reference/README.md</c>.
/// </para>
/// <para>
/// <b>The technique is not ours and the citation is owed.</b> It is the face-swap pipeline of Jared
/// Sanson and Richard Green, <i>Face Replacement Demo using the Kinect Depth Sensor</i> (COSC428,
/// University of Canterbury) — a parameterised mesh, UV-mapped to a face image, deformed and drawn.
/// Their §III.D names both texturing routes and rejects the automatic one because <i>"this technique
/// may fail if the head is rotated, due to obscured regions in the face"</i>. That objection is about
/// a live video frame in which the head may already be turned. A studio chooses its own portrait, so
/// frontality is ours to require and the automatic route is the one implemented here.
/// </para>
/// </remarks>
public class MeshToolkit
{
    #region Constructors
    public MeshToolkit(string? projectRoot = null) => this.projectRoot = projectRoot;
    #endregion

    #region Methods
    /// <summary>Reads a Wavefront OBJ carrying <c>v</c>, optional <c>vt</c>, and triangular <c>f</c>.</summary>
    /// <remarks>
    /// <b>`v` and `vt` are the same count and not the same order</b>, and assuming otherwise is the
    /// first thing that goes wrong: MediaPipe's canonical model writes <c>f 174/43 …</c>, pairing
    /// vertex 174 with texture coordinate 43. The pairing is resolved here, once, so a mesh always
    /// carries one texture coordinate per vertex whatever the file's internal order was.
    /// </remarks>
    public FaceMesh Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var full = ProjectPath.Resolve(projectRoot, filePath, nameof(filePath), "Read");
        if (!File.Exists(full))
            throw new FileNotFoundException(
                $"No such mesh file: '{filePath}'. The path is relative to the project directory.", full);

        return Parse(File.ReadLines(full), filePath);
    }

    /// <summary>The same, from OBJ text a script already holds.</summary>
    public FaceMesh FromObj(string objText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objText);
        return Parse(objText.Split('\n'), "(text)");
    }

    /// <summary>
    /// Draws a mesh, textured and depth-sorted.
    /// </summary>
    /// <remarks>
    /// <b>The sort is the caller's job and is done here.</b> <c>DrawVertices</c> has no depth buffer,
    /// so without it a turned head paints its far cheek over its near eye — a sliver across the face
    /// that reads as a rendering fault. Sorting the triangles back to front is the whole fix, and on
    /// a 898-triangle head it costs nothing worth measuring.
    /// </remarks>
    public Dictionary<string, object?> Draw(CanvasRenderingContext2D ctx, object meshObj, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var mesh = AsMesh(meshObj, nameof(meshObj));
        var opt = JsInterop.AsDict(options);
        RefuseUnknown(opt, DrawOptions, "drawMesh option");

        var pose = Pose.From(opt, mesh);
        var placed = pose.Apply(mesh);

        var wireframe = opt is not null && opt.Contains("wireframe") && Convert.ToBoolean(opt["wireframe"]);
        var texture = opt?["texture"] is { } t ? AsBitmap(t) : mesh.Texture;

        // **A texture with nothing to map it by is a wireframe, not a flat fill.** A mesh that was
        // never fitted and whose file carried no `vt` has every texture coordinate at the origin, so
        // sampling it paints the whole head in one pixel's colour — which renders perfectly and is
        // indistinguishable from a drawing decision. The same fallback an untextured mesh gets.
        if (texture is not null && !mesh.Fitted && !mesh.HasUvs) texture = null;

        if (wireframe || texture is null)
        {
            var ink = opt?["inkColor"]?.ToString() ?? "#1f6f8b";
            ctx.StrokeStyle = ink;
            ctx.LineWidth = Num(opt, "lineWidth", 0.6f);
            for (var i = 0; i < mesh.Indices.Length; i += 3)
            {
                ctx.BeginPath();
                ctx.MoveTo(placed[mesh.Indices[i]].X, placed[mesh.Indices[i]].Y);
                ctx.LineTo(placed[mesh.Indices[i + 1]].X, placed[mesh.Indices[i + 1]].Y);
                ctx.LineTo(placed[mesh.Indices[i + 2]].X, placed[mesh.Indices[i + 2]].Y);
                ctx.ClosePath();
                ctx.Stroke();
            }
        }
        else
        {
            var order = pose.DepthOrder(mesh);
            var sorted = new ushort[mesh.Indices.Length];
            for (var tri = 0; tri < order.Length; tri++)
                for (var k = 0; k < 3; k++) sorted[(tri * 3) + k] = mesh.Indices[(order[tri] * 3) + k];

            // **A mesh carries its texture coordinates in one of two spaces, and the draw path has to
            // know which.** `fitTexture` writes them as pixels in the image it fitted to; an OBJ
            // writes them as an atlas, `0..1` with `v` measured up from the bottom. Until 2026-09-19
            // only the first was handled, so a mesh drawn straight from its own file sampled the
            // rectangle from `(0,0)` to `(1,1)` — **the whole 468-vertex head in one flat colour**,
            // with no error. MediaPipe's canonical model loads at `u 0.008..0.992, v 0.046..0.893`,
            // so the atlas was there and correct the entire time; nothing consumed it.
            var uvs = mesh.Uvs;
            if (!mesh.Fitted)
            {
                uvs = new SKPoint[mesh.Uvs.Length];
                for (var i = 0; i < uvs.Length; i++)
                    uvs[i] = new SKPoint(mesh.Uvs[i].X * texture.Width,
                                         (1f - mesh.Uvs[i].Y) * texture.Height);
            }

            using var shader = texture.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader, IsAntialias = true };
            using var verts = SKVertices.CreateCopy(SKVertexMode.Triangles, placed, uvs, null, sorted);
            ctx.Canvas.SkCanvas.DrawVertices(verts, SKBlendMode.Dst, paint);
        }

        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in placed)
        {
            x0 = MathF.Min(x0, p.X); y0 = MathF.Min(y0, p.Y);
            x1 = MathF.Max(x1, p.X); y1 = MathF.Max(y1, p.Y);
        }

        return new Dictionary<string, object?>
        {
            ["bounds"] = Rect(x0, y0, x1, y1),
            ["triangles"] = mesh.TriangleCount,
            ["textured"] = !wireframe && texture is not null
        };
    }
    #endregion

    #region Fields
    internal static readonly string[] DrawOptions =
        ["x", "y", "scale", "yawDeg", "pitchDeg", "rollDeg", "texture", "wireframe", "inkColor",
         "lineWidth", "shape", "expression", "side"];

    private readonly string? projectRoot;
    #endregion

    #region Child types
    /// <summary>The pose and deformation a draw was asked for, resolved once.</summary>
    internal sealed class Pose
    {
        internal static Pose From(IDictionary? opt, FaceMesh mesh)
        {
            var p = new Pose
            {
                X = Num(opt, "x", 0f),
                Y = Num(opt, "y", 0f),
                Scale = Num(opt, "scale", 1f),
                Cy = MathF.Cos(Num(opt, "yawDeg", 0f) * MathF.PI / 180f),
                Sy = MathF.Sin(Num(opt, "yawDeg", 0f) * MathF.PI / 180f),
                Cp = MathF.Cos(Num(opt, "pitchDeg", 0f) * MathF.PI / 180f),
                Sp = MathF.Sin(Num(opt, "pitchDeg", 0f) * MathF.PI / 180f),
                Cr = MathF.Cos(Num(opt, "rollDeg", 0f) * MathF.PI / 180f),
                Sr = MathF.Sin(Num(opt, "rollDeg", 0f) * MathF.PI / 180f),
                Shape = FaceMesh.ReadUnits(opt?["shape"], FaceMesh.ShapeUnits, "shape"),
                Expression = FaceMesh.ReadUnits(opt?["expression"], FaceMesh.ExpressionUnits,
                                                "expression", arkit: true),
                Side = ReadSide(opt)
            };
            return p;
        }

        /// <summary>
        /// Which half of the face an expression acts on: 0 for both, +1 for the near half, -1 for the far.
        /// </summary>
        /// <remarks>
        /// <b><c>left</c> and <c>right</c> are refused by name, exactly as <c>applyActionUnits</c>
        /// refuses them.</b> This mesh has a near half and a far half — the <c>+x</c> and <c>-x</c>
        /// sides of its own facial axis — and those are sides of the <i>page</i>. Naming one left or
        /// right would be a claim about the character's own anatomy that a turned head cannot keep,
        /// and mapping it quietly onto the wrong half is worse than saying so.
        /// <para>
        /// It exists because one raised eyebrow and a one-sided smirk are the two most recognisable
        /// comic expressions there are, and neither was reachable at any weight while every unit moved
        /// both halves. Three lineages split their brow units per side and this one did not: ARKit and
        /// MediaPipe carry <c>browDownLeft</c>/<c>browDownRight</c>, and <c>candide3.wfm</c> v3.1.6
        /// carries an <i>Eyes vertical difference</i> shape unit.
        /// </para>
        /// </remarks>
        internal static float ReadSide(IDictionary? opt)
        {
            var s = opt?["side"]?.ToString();
            if (string.IsNullOrWhiteSpace(s)) return 0f;

            return s.Trim().ToLowerInvariant() switch
            {
                "both" => 0f,
                "near" => 1f,
                "far" => -1f,
                "left" or "right" => throw new ArgumentException(
                    $"side '{s}' is refused by name. This mesh has a near half and a far half — the +x "
                    + "and -x sides of its own facial axis — and those are sides of the page rather than "
                    + "of the character, so a turned head cannot keep them. Use 'near', 'far' or 'both'."),
                _ => throw new ArgumentException($"side not recognised: {s}. Accepted: both, near, far.")
            };
        }

        internal float X, Y, Scale, Cy, Sy, Cp, Sp, Cr, Sr, Side;
        internal Dictionary<string, float> Shape = [];
        internal Dictionary<string, float> Expression = [];

        /// <summary>Deform in model space, then rotate, then place — never the other way round.</summary>
        /// <remarks>
        /// <b>This ordering is the one thing the mesh route gets right that the landmark head does
        /// not.</b> <c>createLoomisHead</c> projects first and the parameter layer displaces in screen
        /// space afterwards, which is why its own documentation warns that past roughly 40 degrees a
        /// widened jaw widens the wrong way. Deforming in the model's own space and posing afterwards
        /// is the formulation CANDIDE states as <c>g' = R·s·(g + S·σ + A·α) + t</c>, and it has no
        /// such limit.
        /// </remarks>
        internal SKPoint[] Apply(FaceMesh mesh)
        {
            var outp = new SKPoint[mesh.Vertices.Length];
            for (var i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Displace(i, Shape, Expression, Side);
                var r = Rotate(v);
                outp[i] = new SKPoint(X + (r.X * Scale), Y - (r.Y * Scale));
            }

            return outp;
        }

        internal SKPoint3 Rotate(SKPoint3 v)
        {
            float x = (v.X * Cy) + (v.Z * Sy), z = (-v.X * Sy) + (v.Z * Cy);   // yaw, about Y
            float y = (v.Y * Cp) - (z * Sp);
            z = (v.Y * Sp) + (z * Cp);                                          // pitch, about X
            return new SKPoint3((x * Cr) - (y * Sr), (x * Sr) + (y * Cr), z);   // roll, about Z
        }

        /// <summary>Triangle indices ordered furthest first.</summary>
        internal int[] DepthOrder(FaceMesh mesh)
        {
            var count = mesh.Indices.Length / 3;
            var depth = new float[count];
            var order = new int[count];
            for (var t = 0; t < count; t++)
            {
                order[t] = t;
                var z = 0f;
                for (var k = 0; k < 3; k++)
                    z += Rotate(mesh.Displace(mesh.Indices[(t * 3) + k], Shape, Expression, Side)).Z;
                depth[t] = z;
            }

            Array.Sort(depth, order);
            return order;
        }
    }
    #endregion

    #region Private methods
    private static FaceMesh Parse(IEnumerable<string> lines, string source)
    {
        List<SKPoint3> v = [];
        List<SKPoint> vt = [];
        List<ushort> idx = [];
        Dictionary<int, int> pair = [];
        var inv = CultureInfo.InvariantCulture;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length < 2) continue;

            if (line[0] == 'v' && line[1] == ' ')
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                v.Add(new SKPoint3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv)));
            }
            else if (line[0] == 'v' && line[1] == 't')
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                vt.Add(new SKPoint(float.Parse(p[1], inv), float.Parse(p[2], inv)));
            }
            else if (line[0] == 'f' && line[1] == ' ')
            {
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 4) continue;

                // Fan-triangulate, so a quad or an n-gon loads rather than being silently dropped.
                for (var i = 2; i < p.Length - 1; i++)
                    foreach (var corner in new[] { p[1], p[i], p[i + 1] })
                    {
                        var bits = corner.Split('/');
                        var vi = int.Parse(bits[0], inv) - 1;                  // OBJ is 1-based
                        idx.Add((ushort)vi);
                        if (bits.Length > 1 && bits[1].Length > 0)
                            pair[vi] = int.Parse(bits[1], inv) - 1;
                    }
            }
        }

        if (v.Count == 0) throw new ArgumentException($"No vertices in mesh '{source}'.");
        if (idx.Count == 0) throw new ArgumentException($"No triangles in mesh '{source}'.");

        var uvs = new SKPoint[v.Count];
        if (vt.Count > 0)
            foreach (var (vi, ti) in pair)
                if (ti >= 0 && ti < vt.Count) uvs[vi] = vt[ti];

        return new FaceMesh(v.ToArray(), uvs, idx.ToArray(), vt.Count > 0, source);
    }

    internal static FaceMesh AsMesh(object? o, string name) => o as FaceMesh
        ?? throw new ArgumentException($"{name} must be a mesh from Mesh.load(...) or Mesh.fromObj(...).", name);

    internal static SKBitmap AsBitmap(object o) => o switch
    {
        SkiaBitmapWrapper w => w.Bitmap,
        SkiaCanvas c => c.Bitmap.Bitmap,
        SKBitmap b => b,
        _ => throw new ArgumentException(
            "texture must be a bitmap or a canvas — pass canvas.toBitmap(), Skia.Image.load(...), or a photo's decoded bytes.")
    };

    internal static float Num(IDictionary? d, string key, float fallback) =>
        d != null && d.Contains(key) && d[key] != null
            ? Convert.ToSingle(d[key], CultureInfo.InvariantCulture)
            : fallback;

    internal static void RefuseUnknown(IDictionary? d, string[] accepted, string what)
    {
        if (d is null) return;
        foreach (var key in d.Keys)
        {
            var name = key?.ToString();
            if (name is null || accepted.Contains(name, StringComparer.Ordinal)) continue;
            throw new ArgumentException(
                $"{what} not recognised: {name}. Accepted: {string.Join(", ", accepted)}.");
        }
    }

    internal static Dictionary<string, object?> Rect(float x0, float y0, float x1, float y1) => new()
    {
        ["x"] = x0, ["y"] = y0, ["width"] = x1 - x0, ["height"] = y1 - y0,
        ["x2"] = x1, ["y2"] = y1, ["cx"] = (x0 + x1) * 0.5f, ["cy"] = (y0 + y1) * 0.5f
    };
    #endregion
}
