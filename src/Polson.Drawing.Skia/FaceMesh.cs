namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

/// <summary>
/// One loaded mesh: its vertices, its texture coordinates, and the triangles that join them.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>Every call that changes a mesh returns a new one.</b> A mesh is the identity of a character,
/// and a route whose whole selling point is that panel 1 and panel 40 are the same face cannot have
/// a fit or a deformation quietly mutate the thing it was derived from.
/// </para>
/// </remarks>
public class FaceMesh
{
    #region Constructors
    internal FaceMesh(SKPoint3[] vertices, SKPoint[] uvs, ushort[] indices, bool hasUvs, string source)
    {
        Vertices = vertices;
        Uvs = uvs;
        Indices = indices;
        HasUvs = hasUvs;
        Source = source;
    }
    #endregion

    #region Properties
    /// <summary>How many vertices the mesh carries.</summary>
    public int VertexCount => Vertices.Length;

    /// <summary>How many triangles.</summary>
    public int TriangleCount => Indices.Length / 3;

    /// <summary>Whether an image is attached, so a draw will texture rather than wireframe.</summary>
    /// <remarks>
    /// <b>This means what the draw path checks, which is not the same as "the file had UVs".</b> An
    /// OBJ can carry a full texture coordinate for every vertex and still have no picture to sample;
    /// reporting that as textured would promise a render the toolkit cannot produce, and the caller
    /// would find out by looking at a wireframe they did not ask for.
    /// </remarks>
    public bool Textured => Texture is not null;

    /// <summary>Where the mesh came from, for a <c>Stage.note</c>.</summary>
    public string Source { get; }

    /// <summary>The model-space extent, as an ordinary rectangle plus its depth.</summary>
    public Dictionary<string, object?> Bounds
    {
        get
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue;
            float x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (var v in Vertices)
            {
                x0 = MathF.Min(x0, v.X); y0 = MathF.Min(y0, v.Y); z0 = MathF.Min(z0, v.Z);
                x1 = MathF.Max(x1, v.X); y1 = MathF.Max(y1, v.Y); z1 = MathF.Max(z1, v.Z);
            }

            var r = MeshToolkit.Rect(x0, y0, x1, y1);
            r["z"] = z0;
            r["z2"] = z1;
            r["depth"] = z1 - z0;
            return r;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// The index of the vertex nearest a point in the model's own space.
    /// </summary>
    /// <remarks>
    /// <b>Landmarks are found by geometry here, never by a remembered index.</b> A vertex number
    /// quoted from memory is a claim about one export of one mesh, and the three copies of
    /// "CANDIDE-3" this studio holds differ from each other while all claiming the same version. Ask
    /// the mesh in front of you where its eye is.
    /// </remarks>
    public int Landmark(float x, float y, float z)
    {
        int best = 0;
        var bestD = float.MaxValue;
        for (var i = 0; i < Vertices.Length; i++)
        {
            float dx = Vertices[i].X - x, dy = Vertices[i].Y - y, dz = Vertices[i].Z - z;
            var d = (dx * dx) + (dy * dy) + (dz * dz);
            if (d < bestD) { bestD = d; best = i; }
        }

        return best;
    }

    /// <summary>The model-space position of one vertex.</summary>
    public Dictionary<string, object?> Vertex(int index)
    {
        if (index < 0 || index >= Vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"vertex {index} is outside this mesh, which has {Vertices.Length}.");

        var v = Vertices[index];
        return new Dictionary<string, object?> { ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z, ["index"] = index };
    }

    /// <summary>
    /// Textures the mesh from a frontal image by projecting the neutral mesh onto it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what removes the UV atlas from the problem.</b> The neutral mesh is projected flat,
    /// fitted to the face in the picture by a similarity transform solved from three landmarks, and
    /// those fitted positions <i>are</i> the texture coordinates. `face-replace` needed an atlas and
    /// said so — its README records the texture being mapped by hand in the WinCandide utility — and
    /// an atlas is the one thing neither a photograph nor a generated portrait will ever arrive with.
    /// </para>
    /// <para>
    /// <b>What it absorbs and what it does not, measured rather than assumed.</b> Scale and position
    /// fall out of the fit, so the same face framed at 100% and at 42% in a corner produces the same
    /// head. <b>The face must be wholly inside the image</b> — a crop that runs off the edge has no
    /// pixels to sample and smears, which is a limit of the picture rather than of the fit. There is
    /// no rotation term, so a tilted head is not straightened. And <b>only two internal ratios are
    /// pinned</b>, eye separation and eye-to-mouth: every other proportion is the mesh's, so a face
    /// built to other proportions is redistributed onto this one.
    /// </para>
    /// </remarks>
    public FaceMesh FitTexture(object image, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bitmap = MeshToolkit.AsBitmap(image);
        var opt = JsInterop.AsDict(options);
        MeshToolkit.RefuseUnknown(opt, FitOptions, "fitTexture option");

        var eyeL = Point(opt, "eyeLeft");
        var eyeR = Point(opt, "eyeRight");
        var mouth = Point(opt, "mouth");
        if (eyeL is null || eyeR is null || mouth is null)
            throw new ArgumentException(
                "fitTexture needs eyeLeft, eyeRight and mouth, each as { x, y } in the image's own pixels. "
                + "There is no face detector in this stack, so the three points are yours to supply — "
                + "from Drawing.createLoomisHead(...) for a drawn face, or read off a photograph.");

        // **Where this mesh keeps its own eyes and mouth, as fractions of its own frame** — the same
        // normalisation the deformation bands use, and for the same reason: these were literal
        // MediaPipe coordinates, so a mesh at another scale fitted against three points that were
        // nowhere near its face.
        var f = Box;
        var mEyeL = Vertices[Landmark(f.X(-EyeX), f.Y(EyeY), f.Z(EyeZ))];
        var mEyeR = Vertices[Landmark(f.X(EyeX), f.Y(EyeY), f.Z(EyeZ))];
        var mMouth = Vertices[Landmark(f.Cx, f.Y(MouthY), f.Z(MouthZ))];

        var span = mEyeR.X - mEyeL.X;
        if (MathF.Abs(span) < 1e-4f)
            throw new ArgumentException("this mesh has no measurable eye separation, so it cannot be fitted.");

        var sx = (eyeR.Value.X - eyeL.Value.X) / span;
        float mEyeMidY = (mEyeL.Y + mEyeR.Y) * 0.5f, pEyeMidY = (eyeL.Value.Y + eyeR.Value.Y) * 0.5f;
        var drop = mEyeMidY - mMouth.Y;
        if (MathF.Abs(drop) < 1e-4f)
            throw new ArgumentException("this mesh has no measurable eye-to-mouth distance, so it cannot be fitted.");

        var sy = (mouth.Value.Y - pEyeMidY) / drop;
        float ox = (eyeL.Value.X + eyeR.Value.X) * 0.5f, mOx = (mEyeL.X + mEyeR.X) * 0.5f;

        var uvs = new SKPoint[Vertices.Length];
        for (var i = 0; i < Vertices.Length; i++)
            uvs[i] = new SKPoint(ox + ((Vertices[i].X - mOx) * sx),
                                 pEyeMidY - ((Vertices[i].Y - mEyeMidY) * sy));

        return new FaceMesh(Vertices, uvs, Indices, true, Source)
        { Texture = bitmap, Fitted = true, Reference = Reference };
    }

    /// <summary>
    /// The ring of vertices on the edge of the surface — the mesh's own silhouette.
    /// </summary>
    /// <remarks>
    /// An edge belonging to exactly one triangle is a boundary edge, by definition of a surface with
    /// a hole in it. Computed rather than listed, so it is right for whatever mesh was loaded.
    /// </remarks>
    public int[] Boundary()
    {
        if (boundary is not null) return boundary;

        Dictionary<(int, int), int> edges = [];
        for (var t = 0; t < Indices.Length; t += 3)
            for (var k = 0; k < 3; k++)
            {
                int a = Indices[t + k], b = Indices[t + ((k + 1) % 3)];
                var key = a < b ? (a, b) : (b, a);
                edges[key] = edges.TryGetValue(key, out var n) ? n + 1 : 1;
            }

        SortedSet<int> ring = [];
        foreach (var ((a, b), n) in edges)
            if (n == 1) { ring.Add(a); ring.Add(b); }

        boundary = [.. ring];
        return boundary;
    }

    /// <summary>Textures this mesh from an image a detector has already found a face in.</summary>
    /// <remarks>
    /// <para>
    /// <b>This is the step that used to be done by eye.</b> <see cref="FitTexture"/> needs three
    /// points in the image's own pixels, and before a detector existed a caller either read them off
    /// a photograph by looking or took them free from a face the studio had itself constructed. Four
    /// separate places in this SDK's documentation recorded the gap in the same words — <i>there is
    /// no face detector anywhere in this stack</i>.
    /// </para>
    /// <para>
    /// <b>The indices are derived, never remembered.</b> Each anchor is a POSITION in the canonical
    /// model's own space; <see cref="Landmark"/> finds the vertex nearest it on the mesh actually in
    /// hand, and that index is then read out of the detection. A vertex number quoted from memory is
    /// a claim about one export of one mesh, which is the trap this whole class was built to avoid.
    /// </para>
    /// </remarks>
    public FaceMesh FitDetected(object image, FaceDetection detection)
    {
        ArgumentNullException.ThrowIfNull(detection);
        if (!detection.Found)
            throw new ArgumentException(
                $"No face was found in that image, so there is nothing to fit: {detection.Reason}",
                nameof(detection));

        Dictionary<string, object?> Anchor((float X, float Y, float Z) at, string name)
        {
            var i = Landmark(at.X, at.Y, at.Z);
            return detection.At(i) ?? throw new ArgumentException(
                $"This mesh wanted vertex {i} for '{name}' and the detection carries "
                + $"{detection.Count} landmarks, so the two do not share a topology. A canonical "
                + "face model has 468 vertices against the detector's 478, whose last ten are the "
                + "iris refinement.", nameof(detection));
        }

        return FitTexture(image, new Dictionary<string, object?>
        {
            ["eyeLeft"] = Anchor(EyeLeftAt, "eyeLeft"),
            ["eyeRight"] = Anchor(EyeRightAt, "eyeRight"),
            ["mouth"] = Anchor(MouthAt, "mouth")
        });
    }

    /// <summary>
    /// Pushes the mesh's boundary out to a drawn outline, so a face that is not a human average keeps
    /// its own silhouette.
    /// </summary>
    /// <remarks>
    /// <b>This is what a cartoon face needs, and without it the mesh imposes a human head on
    /// everything.</b> Texturing alone puts the drawing's features in the right places and then cuts
    /// them out with an average human mask — measured on our own comic head, the features read and
    /// the jaw and cranium do not survive at all, because the outline belongs to the mesh.
    ///
    /// Each boundary vertex is moved along the ray from the face's own centre until it meets
    /// <paramref name="outline"/>, and the interior is carried with it by inverse-distance weighting
    /// so the features do not tear away from the edge. It is the poor relation of a shape unit — a
    /// real one would displace named groups — but it is derived from the drawing rather than from a
    /// table, which is the half we could not license anyway.
    /// </remarks>
    public FaceMesh FitOutline(CanvasPath outline, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(outline);
        var opt = JsInterop.AsDict(options);
        MeshToolkit.RefuseUnknown(opt, OutlineOptions, "fitOutline option");

        var strength = Math.Clamp(MeshToolkit.Num(opt, "strength", 1f), 0f, 1f);
        var falloff = MathF.Max(0.01f, MeshToolkit.Num(opt, "falloff", 1.4f));

        // **The outline is in the texture's pixels and the mesh is in its own units, so the two need
        // a map — and when the mesh has been fitted that map already exists.** `fitTexture` projected
        // every vertex into texture pixels and stored the result as its UVs, so the transform is
        // recoverable from the mesh itself rather than guessed from bounding boxes. Recovering it is
        // exact where a box fit is an approximation, and it is the difference between an outline that
        // lands on the drawing and one that does not.
        //
        // **Getting this backwards is not a subtle failure.** The first version divided the mesh's
        // width by the outline's instead of the other way round, mapping the whole head into less
        // than a pixel; the bisection then ran to the far end of its range and the mesh came out
        // **437 times too wide**, covering the canvas in whatever colour the texture clamps to. It
        // rendered without an error and looked like a blank page.
        var (sx, sy, ox, oy) = PixelMap(outline);
        if (MathF.Abs(sx) < 1e-6f || MathF.Abs(sy) < 1e-6f) return this;

        var box = Bounds;
        var centre = Point(opt, "center")
            ?? new SKPoint(Convert.ToSingle(box["cx"]), Convert.ToSingle(box["cy"]));

        // The longest ray worth testing, in the mesh's own units: the outline's diagonal.
        var reach = MathF.Sqrt((outline.Path.Bounds.Width * outline.Path.Bounds.Width)
                             + (outline.Path.Bounds.Height * outline.Path.Bounds.Height))
                  / MathF.Max(MathF.Abs(sx), MathF.Abs(sy));

        var ring = Boundary();
        Dictionary<int, SKPoint> moved = [];
        foreach (var vi in ring)
        {
            var v = Vertices[vi];
            float dx = v.X - centre.X, dy = v.Y - centre.Y;
            var len = MathF.Sqrt((dx * dx) + (dy * dy));
            if (len < 1e-4f) continue;

            // March out along the ray until the outline no longer contains the point. Bisection
            // rather than stepping, so the cost is fixed and the answer is sub-pixel.
            float lo = 0f, hi = reach;
            for (var i = 0; i < 30; i++)
            {
                var mid = (lo + hi) * 0.5f;
                var px = ox + ((centre.X + (dx / len * mid)) * sx);
                var py = oy - ((centre.Y + (dy / len * mid)) * sy);
                if (outline.Path.Contains(px, py)) lo = mid; else hi = mid;
            }

            // A ray that never left the outline found nothing, so leave that vertex alone rather
            // than flinging it to the end of the search range.
            if (lo >= reach * 0.999f) continue;

            var target = new SKPoint(centre.X + (dx / len * lo), centre.Y + (dy / len * lo));
            moved[vi] = new SKPoint(v.X + ((target.X - v.X) * strength), v.Y + ((target.Y - v.Y) * strength));
        }

        if (moved.Count == 0) return this;

        var outv = (SKPoint3[])Vertices.Clone();
        for (var i = 0; i < Vertices.Length; i++)
        {
            if (moved.TryGetValue(i, out var exact))
            {
                outv[i] = new SKPoint3(exact.X, exact.Y, Vertices[i].Z);
                continue;
            }

            // Interior vertices follow their nearest boundary neighbours, weighted by distance, so the
            // surface stretches rather than tearing at the ring.
            float wx = 0f, wy = 0f, w = 0f;
            foreach (var (vi, to) in moved)
            {
                var from = Vertices[vi];
                float dx = Vertices[i].X - from.X, dy = Vertices[i].Y - from.Y;
                var weight = 1f / MathF.Pow(MathF.Max(0.05f, (dx * dx) + (dy * dy)), falloff);
                wx += (to.X - from.X) * weight;
                wy += (to.Y - from.Y) * weight;
                w += weight;
            }

            if (w > 0f) outv[i] = new SKPoint3(Vertices[i].X + (wx / w), Vertices[i].Y + (wy / w), Vertices[i].Z);
        }

        // **The texture coordinates travel with their vertices, and leaving them behind is the whole
        // difference between this working and not.** A fitted UV *is* the projection of its own
        // vertex, so moving the vertex and keeping the old UV makes the mesh grow to the drawing's
        // outline while still sampling the same small patch in the middle of it — measurably worse
        // than doing nothing. Re-projecting keeps every vertex pinned to the pixel it stands on, so
        // the surface takes the drawing's shape *and* its whole picture.
        var outUvs = Uvs;
        if (Fitted)
        {
            outUvs = new SKPoint[outv.Length];
            for (var i = 0; i < outv.Length; i++)
                outUvs[i] = new SKPoint(ox + (outv[i].X * sx), oy - (outv[i].Y * sy));
        }

        // **The reference geometry does not travel with the vertices, and that is the point.** The
        // silhouette has changed; which vertex is a brow vertex has not. Carrying the original through
        // is what keeps an expression landing on the feature it names after the surface has been
        // pushed out to a drawing's own outline.
        return new FaceMesh(outv, outUvs, Indices, HasUvs, Source)
        { Texture = Texture, Fitted = Fitted, Reference = Reference };
    }

    /// <summary>
    /// Which space this mesh's texture coordinates are in: <c>pixels</c>, <c>atlas</c> or <c>none</c>.
    /// </summary>
    /// <remarks>
    /// <b>There are two, and a number that could be either is the defect this reports its way out
    /// of.</b> <see cref="FitTexture"/> writes coordinates as pixels in the image it fitted to;
    /// an OBJ writes them as an atlas, <c>0..1</c> with <c>v</c> measured up from the bottom. A mesh
    /// carrying neither reports <c>none</c>, and drawing it with a texture falls back to a wireframe
    /// rather than painting the head in one pixel's colour.
    /// </remarks>
    public string UvSpace => Fitted ? "pixels" : HasUvs ? "atlas" : "none";

    /// <summary>The texture coordinate one vertex samples, in the space <see cref="UvSpace"/> names.</summary>
    /// <remarks>
    /// The pair to <see cref="Vertex"/>, and how a caller checks a fit without rendering it: front
    /// projection is monotonic, so a mesh's leftmost vertex must sample left of its rightmost one.
    /// <b>Read <see cref="UvSpace"/> before comparing these against pixels</b> — an unfitted mesh
    /// reports its file's atlas coordinates, which are fractions rather than pixel positions.
    /// </remarks>
    public Dictionary<string, object?> UvAt(int index)
    {
        if (index < 0 || index >= Uvs.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"vertex {index} is outside this mesh, which has {Uvs.Length}.");

        return new Dictionary<string, object?> { ["x"] = Uvs[index].X, ["y"] = Uvs[index].Y, ["index"] = index };
    }

    /// <summary>A copy, so a character can be kept and a variant drawn from it.</summary>
    public FaceMesh Clone() =>
        new((SKPoint3[])Vertices.Clone(), (SKPoint[])Uvs.Clone(), (ushort[])Indices.Clone(), HasUvs, Source)
        { Texture = Texture, Fitted = Fitted, Reference = Reference, Rig = Rig };

    /// <summary>Whether this mesh carries a skeleton, so <see cref="Pose"/> will do anything.</summary>
    /// <remarks>
    /// <b>Ask before planning a route around posing.</b> An OBJ never has one, and neither does an
    /// unrigged glTF — which is what the single-image generators produce. Same discipline as
    /// <c>Skia.tracer.available</c>: a script that commits to posing and only then discovers it
    /// cannot has spent its passes for nothing.
    /// </remarks>
    public bool Posable => Rig?.Skinned == true;

    /// <summary>The joint names this file declares, or empty when it carries no skin.</summary>
    /// <remarks>
    /// <b>Read these rather than guessing.</b> Names come from whoever exported the file —
    /// <c>mixamorig:LeftForeArm</c>, <c>J_Bip_L_UpperArm</c>, <c>bone_012</c> — and no convention
    /// spans the exporters. <see cref="Pose"/> refuses an unknown name and suggests the nearest.
    /// </remarks>
    public string[] Joints => Rig?.JointNames ?? [];

    /// <summary>
    /// Rotates joints and returns the deformed mesh: <c>{ 'LeftArm': { zDeg: -40 } }</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always measured from the BIND pose, never from wherever this mesh already is.</b> So a
    /// pose is a pure function of its argument rather than of the order calls happened in, and two
    /// poses taken from one mesh cannot interfere. Posing a posed mesh re-poses the original.
    /// </para>
    /// <para>
    /// Rotations are degrees about the joint's <b>own</b> axes, applied yaw (Y), then pitch (X),
    /// then roll (Z). Any of the three may be left out. An unknown joint name, or a key that is not
    /// one of <c>xDeg</c>/<c>yDeg</c>/<c>zDeg</c>, is refused by name — accepting it silently is how
    /// a caller ends up looking at an unchanged figure and blaming the renderer.
    /// </para>
    /// <para>
    /// <b>This is linear blend skinning, so it does what LBS does.</b> A joint bent hard pinches on
    /// the inside of the bend, and clothing modelled onto a body deforms with the body rather than
    /// draping. Neither is a defect here; both are what the technique is.
    /// </para>
    /// </remarks>
    public FaceMesh Pose(object? pose)
    {
        if (Rig is null)
            throw new ArgumentException(
                $"Mesh '{Source}' was not read from a glTF, so it has no skeleton. Only .glb and " +
                ".gltf carry joints; an OBJ cannot express one.");

        return Rig.Pose(JsInterop.AsDict(pose), Reference);
    }
    #endregion

    #region Internal
    internal SKPoint3[] Vertices { get; }

    /// <summary>
    /// The geometry as it was loaded, which is what every deformation weight is decided against.
    /// </summary>
    /// <remarks>
    /// <see cref="FitOutline"/> moves vertices, so without this a mesh fitted to a drawing's own
    /// silhouette would have its brow band sitting wherever the brow had been pushed to rather than
    /// on the brow. Defaults to <see cref="Vertices"/>, so a freshly loaded mesh is its own reference.
    /// </remarks>
    internal SKPoint3[] Reference { get => reference ?? Vertices; init => reference = value; }

    /// <summary>The mesh's own frame, computed once from <see cref="Reference"/>.</summary>
    internal Frame Box => frame ??= Frame.Of(Reference);

    internal SKPoint[] Uvs { get; }

    internal SKPoint UvOf(int i) => Uvs[i];

    internal ushort[] Indices { get; }

    internal bool HasUvs { get; }

    internal SKBitmap? Texture { get; init; }

    /// <summary>The open glTF this came from, when it came from one, so it can be posed.</summary>
    /// <remarks>
    /// Shared between a mesh and every pose taken from it, which is safe because a pose snapshots
    /// its vertices: the rig is re-evaluated per call and holds no state a caller depends on.
    /// </remarks>
    internal MeshRig? Rig { get; init; }

    internal bool Fitted { get; init; }

    /// <summary>
    /// The named deformations, and the honest note about where they come from.
    /// </summary>
    /// <remarks>
    /// <b>These are the studio's, written by hand, and they are not CANDIDE's Shape Units.</b> The
    /// mesh this route is designed against ships a neutral surface and no displacement data at all —
    /// that is the whole licensing asymmetry: the model with the units states no terms, and the model
    /// with terms states no units. So each of these is a rule about which vertices move and by how
    /// much, tuned by eye, exactly as <c>applyActionUnits</c>'s magnitudes are. Anything claiming a
    /// measured decimal for them is claiming more than any source here supports.
    /// </remarks>
    internal static readonly string[] ShapeUnits =
        ["width", "height", "jawWidth", "browHeight", "eyeSize", "noseLength"];

    /// <summary>
    /// The expression units, named for ARKit's blendshape vocabulary wherever ARKit has a name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The names are a published interface; every displacement behind them is still ours.</b>
    /// Apple's 52-blendshape set is a naming specification that ARKit, MediaPipe's Face Landmarker
    /// and most rigging tools already speak, so borrowing it costs nothing and lets a preset written
    /// for one of those be read here — exactly as this studio borrowed FACS's AU numbering for
    /// <c>applyActionUnits</c>.
    /// </para>
    /// <para>
    /// <b>What it does not do is supply a single number, and that is worth stating because it is
    /// routinely claimed otherwise.</b> MediaPipe's blendshape graph is a regressor from landmarks
    /// <i>to</i> coefficients — its own header reads <i>"Predicts face blendshapes from
    /// landmarks"</i> — and what the file carries is <c>std::array&lt;string_view, 52&gt;</c>, fifty-two
    /// strings. The model producing the weights is a TFLite binary fetched at build time and absent
    /// from the tree, and it runs the wrong way for us in any case. ARKit likewise hands an
    /// application coefficients and expects the application's own rigged model to carry the deltas.
    /// So the licensing asymmetry is unchanged: the model with usable terms has no units, the model
    /// with units states no terms, and every magnitude here is hand-written and tuned by eye.
    /// </para>
    /// <para>
    /// <b>Two of these are ours and have no ARKit counterpart.</b> <c>browRaise</c> lifts the whole
    /// brow, which ARKit expresses only as inner and outer separately; <c>browKnit</c> draws the brow
    /// heads together, which ARKit folds into <c>browDown</c>. Both are kept because the geometry can
    /// show them, and because a name that already worked should go on working.
    /// </para>
    /// <para>
    /// <b>No unit here carries a side in its name.</b> ARKit's are per-side — <c>browDownLeft</c>,
    /// <c>mouthSmileRight</c> — and this construction deliberately has no left and right: its halves
    /// are <c>near</c> and <c>far</c>, sides of the page rather than of the character, because a
    /// turned head cannot keep the other claim. A suffixed name is refused by name and pointed at the
    /// <c>side</c> option, the same judgment <c>Pose.ReadSide</c> makes.
    /// </para>
    /// </remarks>
    internal static readonly string[] ExpressionUnits =
    [
        "browDown", "browInnerUp", "browOuterUp", "browKnit", "browRaise",
        "eyeBlink", "eyeSquint", "eyeWide",
        "jawOpen", "mouthFrown", "mouthSmile", "mouthStretch"
    ];

    /// <summary>The pre-ARKit spellings, so nothing written before 2026-09-19 stops working.</summary>
    private static readonly Dictionary<string, string> ExpressionAliases = new(StringComparer.Ordinal)
    {
        ["browLower"] = "browDown",
        ["squint"] = "eyeSquint",
        ["mouthOpen"] = "jawOpen",
        ["mouthCornerDown"] = "mouthFrown",
        ["mouthWide"] = "mouthStretch"
    };

    /// <summary>ARKit blendshapes this construction cannot show, and what each would need.</summary>
    /// <remarks>
    /// <b>Named and declined rather than left to the generic refusal</b>, for the reason
    /// <c>applyActionUnits</c> names AU6, AU9 and AU17 in its own documentation: a real name that is
    /// merely absent from the accepted list reads as a typo, when the truth is that the geometry
    /// cannot carry it. Accepting all fifty-two and letting two thirds of them quietly do nothing
    /// would be worse than either — an API that silently does nothing is the failure this whole file
    /// was corrected for once already, when every band sat at an absolute coordinate and a mesh at
    /// another scale returned a neutral face from a call asked for anger.
    /// </remarks>
    private static readonly Dictionary<string, string> ExpressionDeclined = new(StringComparer.Ordinal)
    {
        ["cheekPuff"] = "this mesh has no cheek band to inflate",
        ["cheekSquint"] = "this mesh has no cheek band",
        ["eyeLookDown"] = "the eyeball is not separate from the socket here",
        ["eyeLookIn"] = "the eyeball is not separate from the socket here",
        ["eyeLookOut"] = "the eyeball is not separate from the socket here",
        ["eyeLookUp"] = "the eyeball is not separate from the socket here",
        ["jawForward"] = "the jaw moves only open here",
        ["jawLeft"] = "the jaw moves only open here",
        ["jawRight"] = "the jaw moves only open here",
        ["mouthClose"] = "the lips are one band rather than two, so they cannot close against each other",
        ["mouthDimple"] = "a dimple is a surface crease rather than a landmark move",
        ["mouthFunnel"] = "the lips are a band rather than a ring",
        ["mouthLeft"] = "lateral mouth shift is not implemented",
        ["mouthRight"] = "lateral mouth shift is not implemented",
        ["mouthLowerDown"] = "upper and lower lips are not separable here",
        ["mouthPress"] = "the lips are a band rather than a ring",
        ["mouthPucker"] = "the lips are a band rather than a ring",
        ["mouthRollLower"] = "the lips are a band rather than a ring",
        ["mouthRollUpper"] = "the lips are a band rather than a ring",
        ["mouthShrugLower"] = "the chin is not a separate mass here",
        ["mouthShrugUpper"] = "the chin is not a separate mass here",
        ["mouthUpperUp"] = "upper and lower lips are not separable here (AU10)",
        ["noseSneer"] = "the nose has no band that can curl (AU9)"
    };

    internal static readonly string[] FitOptions = ["eyeLeft", "eyeRight", "mouth"];

    /// <summary>Where <see cref="FitDetected"/>'s three anchors sit in the canonical model's space.</summary>
    /// <remarks>
    /// <b>Positions rather than indices, and measured rather than recalled.</b> The eye line and the
    /// pupils were read off MediaPipe's <c>face_model_with_iris.obj</c> by deriving them from the
    /// geometry — the two iris centres at <c>y = 2.636</c>, <c>x = ±3.182</c> — and the mouth centre
    /// is where the midline meets the lip line. <see cref="Landmark"/> resolves each to whatever
    /// vertex is nearest on the mesh in hand, so a differently-exported model still answers
    /// correctly instead of silently addressing the wrong point.
    /// </remarks>
    internal static readonly (float X, float Y, float Z) EyeLeftAt = (-3.18f, 2.64f, 3.4f);

    internal static readonly (float X, float Y, float Z) EyeRightAt = (3.18f, 2.64f, 3.4f);

    internal static readonly (float X, float Y, float Z) MouthAt = (0f, -3.4f, 6.0f);

    internal static readonly string[] OutlineOptions = ["center", "strength", "falloff"];

    /// <param name="arkit">
    /// Whether to resolve the pre-ARKit aliases and refuse a known ARKit name by name. Expression
    /// units do; shape units have no such vocabulary and are matched exactly.
    /// </param>
    internal static Dictionary<string, float> ReadUnits(object? o, string[] accepted, string what,
                                                        bool arkit = false)
    {
        Dictionary<string, float> units = [];
        if (JsInterop.AsDict(o) is not IDictionary d) return units;

        foreach (var key in d.Keys)
        {
            var given = key?.ToString();
            if (given is null) continue;

            // The value is looked up by the spelling the caller used, never by the canonical one —
            // an alias resolved before the read would ask the dictionary for a key it has not got
            // and quietly apply zero, which is the silent-no-op failure this file already has a
            // scar from.
            var name = arkit ? Canonical(given, accepted, what) : given;
            if (!accepted.Contains(name, StringComparer.Ordinal))
                throw new ArgumentException(
                    $"{what} unit not recognised: {given}. Accepted: {string.Join(", ", accepted)}.");

            units[name] = Math.Clamp(MeshToolkit.Num(d, given, 0f), -1f, 1f);
        }

        return units;
    }

    /// <summary>Resolves an alias or an ARKit spelling, refusing by name what cannot be shown.</summary>
    /// <remarks>
    /// Order matters here. A declined stem is reported as declined even when the caller also wrote a
    /// side, because telling them to drop the side first would cost a turn and then refuse anyway.
    /// </remarks>
    private static string Canonical(string given, string[] accepted, string what)
    {
        if (accepted.Contains(given, StringComparer.Ordinal)) return given;
        if (ExpressionAliases.TryGetValue(given, out var canonical)) return canonical;

        var stem = given;
        var sided = false;
        if (given.EndsWith("Left", StringComparison.Ordinal)) { stem = given[..^4]; sided = true; }
        else if (given.EndsWith("Right", StringComparison.Ordinal)) { stem = given[..^5]; sided = true; }

        if (ExpressionDeclined.TryGetValue(stem, out var why))
            throw new ArgumentException(
                $"{what} unit '{given}' is an ARKit blendshape this construction cannot show: {why}. "
                + $"It is declined by name rather than accepted and ignored. "
                + $"Accepted: {string.Join(", ", accepted)}.");

        if (sided && accepted.Contains(stem, StringComparer.Ordinal))
            throw new ArgumentException(
                $"{what} unit '{given}' names a side. This construction has no left and right — its "
                + $"halves are 'near' and 'far', sides of the page rather than of the character, "
                + $"because a turned head cannot keep the other claim. Write '{stem}' and pass "
                + $"{{ side: 'near' }} or {{ side: 'far' }}.");

        return given;
    }

    /// <summary>One vertex, deformed in the model's own space.</summary>
    /// <remarks>
    /// <para>
    /// <b>Every station and every magnitude is a fraction of the mesh's own frame, never a model
    /// coordinate.</b> They were absolute until 2026-09-19 — the brow band sat at literal
    /// <c>y = 3.6</c>, the eyes at <c>2.64</c>, the mouth at <c>-3.4</c> — which are MediaPipe's
    /// numbers and nobody else's. A mesh authored at a tenth of that scale, which is an ordinary
    /// thing for an OBJ to be, put every band outside its own geometry and <b>every expression
    /// silently did nothing</b>: no error, no warning, a neutral face returned from a call that was
    /// asked for anger. The fractions below reproduce the old absolutes exactly on the canonical
    /// model, so nothing already drawn with it changes.
    /// </para>
    /// <para>
    /// <b>The weights are decided by the reference geometry and applied to the current geometry</b>,
    /// which is what makes this survive <see cref="FitOutline"/>. That call moves vertices — pushing
    /// the boundary out to a drawing's own silhouette and carrying the interior with it — so a band
    /// keyed on where a vertex <i>now</i> sits would slide off the feature it was named for. Which
    /// vertex is a brow vertex is a fact about the anatomy, not about where the brow has been pushed.
    /// </para>
    /// </remarks>
    internal SKPoint3 Displace(int i, Dictionary<string, float> shape, Dictionary<string, float> expression,
                               float side = 0f)
    {
        var v = Vertices[i];
        if (shape.Count == 0 && expression.Count == 0) return v;

        var r = Reference[i];
        var f = Box;
        float x = v.X, y = v.Y, z = v.Z;

        foreach (var (name, a) in shape)
            switch (name)
            {
                case "width": x = f.Cx + ((x - f.Cx) * (1f + (0.30f * a))); break;
                case "height": y = f.Y(0.5f) + ((y - f.Y(0.5f)) * (1f + (0.30f * a))); break;
                case "jawWidth":
                    x = f.Cx + ((x - f.Cx) * (1f + (0.35f * a * f.Below(r.Y, JawY))));
                    break;
                case "browHeight": y += BrowHeightMag * f.H * a * f.Band(r.Y, BrowY, BrowBandWide); break;
                case "eyeSize":
                    var eye = f.Band(r.Y, EyeY, EyeBand);
                    x += (r.X - (MathF.Sign(r.X - f.Cx) * f.X(EyeX))) * 0.35f * a * eye;
                    y += (r.Y - f.Y(EyeY)) * 0.35f * a * eye;
                    break;
                case "noseLength":
                    y -= NoseMag * f.H * a * f.Band(r.Y, NoseY, NoseBand) * (r.Z > f.Z(NoseZ) ? 1f : 0.3f);
                    break;
            }

        foreach (var (name, a) in expression)
        {
            // **A jaw does not drop on one side**, so `mouthOpen` ignores the option rather than
            // refusing the whole call over it — the same judgment `applyActionUnits` makes for AU26,
            // and for the same reason: asking for a one-sided brow beside an open mouth is ordinary.
            var w = name == "jawOpen" ? 1f : f.SideWeight(r.X, side);
            if (w <= 0f) continue;

            switch (name)
            {
                case "mouthStretch":
                    x = f.Cx + ((x - f.Cx) * (1f + (0.20f * a * w * f.Band(r.Y, MouthY, MouthBand))));
                    break;
                case "jawOpen": y -= MouthOpenMag * f.H * a * f.Below(r.Y, JawDropY); break;
                case "mouthFrown":
                    y -= CornerDownMag * f.H * a * w * f.Band(r.Y, MouthY, MouthBand)
                       * f.Peak(r.X, MouthCornerX, CornerFade);
                    break;

                // **Out as well as up, which is Loomis's observation rather than a detail.** His
                // "happy muscles" run from the cheekbones diagonally down to the mouth, so they pull
                // the corner outward too; a corner lifted straight up reads as a smirk. The same
                // reasoning `applyActionUnits` gives for AU12, and the mesh route had no smile unit
                // at all before this — as CANDIDE-3 has none, which is what a model built for
                // videophone bitrate would choose and not what a comic face needs.
                case "mouthSmile":
                    var smile = a * w * f.Band(r.Y, MouthY, MouthBand)
                              * f.Peak(r.X, MouthCornerX, CornerFade);
                    x += MathF.Sign(r.X - f.Cx) * SmileOutMag * f.HalfW * smile;
                    y += SmileUpMag * f.H * smile;
                    break;

                case "browRaise": y += BrowMoveMag * f.H * a * w * f.Band(r.Y, BrowY, BrowBand); break;
                case "browDown": y -= BrowMoveMag * f.H * a * w * f.Band(r.Y, BrowY, BrowBand); break;
                case "browInnerUp":
                    y += BrowMoveMag * f.H * a * w * f.Band(r.Y, BrowY, BrowBand)
                       * f.Peak(r.X, KnitPeakX, KnitFade);
                    break;
                case "browOuterUp":
                    y += BrowMoveMag * f.H * a * w * f.Band(r.Y, BrowY, BrowBand)
                       * f.Peak(r.X, OuterPeakX, OuterFade);
                    break;
                case "browKnit":
                    x -= MathF.Sign(r.X - f.Cx) * KnitMag * f.HalfW * a * w
                       * f.Band(r.Y, BrowY, BrowBand) * f.Peak(r.X, KnitPeakX, KnitFade);
                    break;

                // Orbicularis oculi tightens both lids, so this draws the whole aperture toward the
                // eye line. The two below act on the upper lid alone, which is what separates them:
                // a blink is not a hard squint and a widened eye is not an un-squint.
                case "eyeSquint":
                    y += (f.Y(EyeY) - r.Y) * 0.45f * a * w * f.Band(r.Y, EyeY, SquintBand);
                    break;
                case "eyeBlink":
                    if (r.Y > f.Y(EyeY))
                        y += (f.Y(EyeY) - r.Y) * BlinkMag * a * w * f.Band(r.Y, EyeY, SquintBand);
                    break;

                // **The sclera dial, and it is expressive in both directions.** Gautier locates the
                // whole difference between surprise and fear here — the eyes widen more in fear, so
                // more white shows round the pupil — and warns on the same page that too much white
                // *beneath* the pupil reads as sinister. Raising the upper lid is the half of that
                // which this band can reach.
                case "eyeWide":
                    if (r.Y > f.Y(EyeY))
                        y += (r.Y - f.Y(EyeY)) * EyeWideMag * a * w * f.Band(r.Y, EyeY, SquintBand);
                    break;
            }
        }

        return new SKPoint3(x, y, z);
    }

    #region Frame
    /// <summary>
    /// Where the features sit and how far they move, as fractions of the mesh's own extent.
    /// </summary>
    /// <remarks>
    /// <b>Every number here was derived by dividing the absolute constant it replaced by the
    /// canonical model's own frame</b>, so the deformation on that mesh is unchanged to within float
    /// precision: <c>x -7.7431..7.7431</c>, <c>y -9.4034..8.2618</c>, <c>z -2.4359..7.5866</c>. The
    /// vertical ones are measured <i>up from the chin</i> as a share of the head's height, the
    /// horizontal ones outward from the midline as a share of its half-width, and the depth one back
    /// from the rearmost vertex.
    /// <para>
    /// <b>The frame assumes the mesh's extent is a face</b> — chin at the bottom, brow or forehead at
    /// the top, midline in the middle. That is what every face mesh this studio has read actually is,
    /// and it is the only assumption available without semantic landmarks the format does not carry.
    /// A mesh carrying a whole head with hair would put these stations too low, and the wireframe
    /// would show it.
    /// </para>
    /// </remarks>
    private const float BrowY = 0.73610f;        // was y = 3.6
    private const float EyeY = 0.68176f;         // was y = 2.64
    private const float NoseY = 0.56062f;        // was y = 0.5
    private const float JawY = 0.44741f;         // was y = -1.5
    private const float JawDropY = 0.38514f;     // was y = -2.6
    private const float MouthY = 0.33984f;       // was y = -3.4
    private const float BelowRamp = 0.33964f;    // was the /6 inside Below

    private const float BrowBandWide = 0.101895f;   // was radius 1.8
    private const float BrowBand = 0.067930f;       // was radius 1.2
    private const float EyeBand = 0.079252f;        // was radius 1.4
    private const float SquintBand = 0.056608f;     // was radius 1.0
    private const float MouthBand = 0.101895f;      // was radius 1.8
    private const float NoseBand = 0.113217f;       // was radius 2.0

    private const float BrowHeightMag = 0.050948f;  // was 0.9 units
    private const float BrowMoveMag = 0.039626f;    // was 0.7 units
    private const float MouthOpenMag = 0.090574f;   // was 1.6 units
    private const float NoseMag = 0.045287f;        // was 0.8 units

    private const float EyeX = 0.410689f;        // was x = ±3.18
    private const float NoseZ = 0.692039f;       // was z = 4.5
    private const float EyeZ = 0.582290f;        // was z = 3.4, the eye anchor the fit searches from
    private const float MouthZ = 0.841705f;      // was z = 6.0

    /// <summary>How much of a corner drop, and how far out the mouth's corner is taken to be.</summary>
    /// <remarks>
    /// <b>New on 2026-09-19, so there is no old absolute to reproduce — these are tuned by eye like
    /// every other magnitude here.</b> The corner weight rises to the corner and falls away again
    /// past it, because the mouth band alone reaches the cheeks: weighting purely by distance from
    /// the midline would drag half the face down with the lip.
    /// </remarks>
    private const float CornerDownMag = 0.031134f;
    private const float MouthCornerX = 0.30f;
    private const float CornerFade = 2.0f;

    /// <summary>How far the brow heads draw together, and where the head of the brow is.</summary>
    /// <remarks>
    /// Corrugator draws the <i>inner ends</i> together, so the weight peaks at the brow head and
    /// fades outward — and it is zero at the midline itself, which has nothing to move toward.
    /// </remarks>
    private const float KnitMag = 0.058116f;
    private const float KnitPeakX = 0.20f;
    private const float KnitFade = 2.75f;

    /// <summary>Where the brow's tail is, for the outer raise.</summary>
    /// <remarks>
    /// Further out than <see cref="KnitPeakX"/> and fading sooner, so <c>browOuterUp</c> and
    /// <c>browInnerUp</c> overlap in the middle of the brow rather than meeting at a seam — which is
    /// what lets the pair compose into an arch, and what makes each alone read as one end lifting.
    /// </remarks>
    private const float OuterPeakX = 0.60f;
    private const float OuterFade = 1.8f;

    /// <summary>How far a lid travels, as a share of its own distance from the eye line.</summary>
    /// <remarks>
    /// Proportional rather than absolute, so the lid arrives <i>at</i> the eye line as the weight
    /// reaches 1 whatever the eye's size — a fixed drop would overshoot a small eye and leave a large
    /// one open. A blink nearly closes at full weight; the widening is deliberately gentler, because
    /// the whole of its expressive work is how much white it reveals.
    /// </remarks>
    private const float BlinkMag = 0.90f;
    private const float EyeWideMag = 0.35f;

    /// <summary>How far a smile takes the corners out, and up.</summary>
    /// <remarks>
    /// Out slightly less than up, and both close to <see cref="CornerDownMag"/>, so a smile and a
    /// frown are the same size of gesture in opposite directions. Tuned by eye like every other
    /// magnitude here; neither ARKit nor MediaPipe supplies one.
    /// </remarks>
    private const float SmileOutMag = 0.030f;
    private const float SmileUpMag = 0.034f;

    /// <summary>How wide the blend across the midline is when only one half is acting.</summary>
    /// <remarks>A hard cut at the axis leaves a seam down the nose; this is about a fifth of a half-face.</remarks>
    private const float SideBlend = 0.18f;

    /// <summary>The mesh's own extent, so a station is a fraction rather than a coordinate.</summary>
    internal sealed class Frame
    {
        internal static Frame Of(SKPoint3[] v)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue;
            float x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (var p in v)
            {
                x0 = MathF.Min(x0, p.X); y0 = MathF.Min(y0, p.Y); z0 = MathF.Min(z0, p.Z);
                x1 = MathF.Max(x1, p.X); y1 = MathF.Max(y1, p.Y); z1 = MathF.Max(z1, p.Z);
            }

            if (v.Length == 0) { x0 = y0 = z0 = 0f; x1 = y1 = z1 = 1f; }

            return new Frame
            {
                Cx = (x0 + x1) * 0.5f,
                HalfW = MathF.Max(1e-4f, (x1 - x0) * 0.5f),
                Y0 = y0,
                H = MathF.Max(1e-4f, y1 - y0),
                Z0 = z0,
                D = MathF.Max(1e-4f, z1 - z0)
            };
        }

        internal float Cx, HalfW, Y0, H, Z0, D;

        /// <summary>The model y at a fraction of the head's height, measured up from the chin.</summary>
        internal float Y(float fraction) => Y0 + (fraction * H);

        /// <summary>The model x at a fraction of the half-width, measured out from the midline.</summary>
        internal float X(float fraction) => Cx + (fraction * HalfW);

        /// <summary>The model z at a fraction of the depth, measured forward from the back.</summary>
        internal float Z(float fraction) => Z0 + (fraction * D);

        /// <summary>A smooth 0..1 weight centred on a station, reaching zero at the radius.</summary>
        internal float Band(float y, float station, float radius)
        {
            var t = MathF.Abs(y - Y(station)) / MathF.Max(1e-4f, radius * H);
            return t >= 1f ? 0f : 1f - (t * t);
        }

        /// <summary>A smooth 0..1 weight that grows below a station, for the jaw and the chin.</summary>
        internal float Below(float y, float station) =>
            Math.Clamp((Y(station) - y) / (BelowRamp * H), 0f, 1f);

        /// <summary>
        /// 1 at a station out from the midline, 0 at the midline and 0 again past the fade.
        /// </summary>
        /// <remarks>
        /// <b>One shape serving the mouth corner, the brow head and the brow tail.</b> It replaced a
        /// <c>Corner</c> and an <c>Inner</c> that differed only in whether the falloff was normalised
        /// — and did not actually differ, because the corner's fade of 2.0 makes its divisor exactly
        /// 1. The band alone cannot do this work: weighting purely by distance from the midline drags
        /// half the face along with whatever is being moved.
        /// </remarks>
        internal float Peak(float x, float station, float fade)
        {
            var t = MathF.Abs(x - Cx) / MathF.Max(1e-4f, station * HalfW);
            return t <= 1f ? t : MathF.Max(0f, (fade - t) / MathF.Max(1e-4f, fade - 1f));
        }

        /// <summary>How much of a one-sided unit this vertex takes: 0 for both halves, ±1 to pick one.</summary>
        internal float SideWeight(float x, float side) =>
            side == 0f ? 1f : Math.Clamp((x - Cx) * side / (SideBlend * HalfW), 0f, 1f);
    }
    #endregion

    /// <summary>
    /// The mesh-to-pixel transform, recovered from the fitted UVs where there are any.
    /// </summary>
    /// <remarks>
    /// <c>fitTexture</c> writes <c>uv = (ox + x·sx, oy − y·sy)</c>, so two vertices far apart in each
    /// axis recover all four numbers exactly. An unfitted mesh has no such map and falls back to
    /// matching the two bounding boxes, which is an approximation and says so.
    /// </remarks>
    private (float Sx, float Sy, float Ox, float Oy) PixelMap(CanvasPath outline)
    {
        if (Fitted && Vertices.Length > 1)
        {
            int loX = 0, hiX = 0, loY = 0, hiY = 0;
            for (var i = 1; i < Vertices.Length; i++)
            {
                if (Vertices[i].X < Vertices[loX].X) loX = i;
                if (Vertices[i].X > Vertices[hiX].X) hiX = i;
                if (Vertices[i].Y < Vertices[loY].Y) loY = i;
                if (Vertices[i].Y > Vertices[hiY].Y) hiY = i;
            }

            float dx = Vertices[hiX].X - Vertices[loX].X, dy = Vertices[hiY].Y - Vertices[loY].Y;
            if (MathF.Abs(dx) > 1e-4f && MathF.Abs(dy) > 1e-4f)
            {
                var sx = (Uvs[hiX].X - Uvs[loX].X) / dx;
                var sy = -(Uvs[hiY].Y - Uvs[loY].Y) / dy;
                return (sx, sy, Uvs[loX].X - (Vertices[loX].X * sx), Uvs[loY].Y + (Vertices[loY].Y * sy));
            }
        }

        var box = outline.Path.Bounds;
        var mine = Bounds;
        float mw = Convert.ToSingle(mine["width"]), mh = Convert.ToSingle(mine["height"]);
        float bx = box.Width / MathF.Max(1e-4f, mw), by = box.Height / MathF.Max(1e-4f, mh);
        return (bx, by,
                box.MidX - (Convert.ToSingle(mine["cx"]) * bx),
                box.MidY + (Convert.ToSingle(mine["cy"]) * by));
    }

    private static SKPoint? Point(IDictionary? d, string key)
    {
        if (d is null || !d.Contains(key) || d[key] is null) return null;
        if (JsInterop.AsDict(d[key]) is not IDictionary p) return null;
        return new SKPoint(MeshToolkit.Num(p, "x", 0f), MeshToolkit.Num(p, "y", 0f));
    }

    private readonly SKPoint3[]? reference;

    private Frame? frame;

    private int[]? boundary;
    #endregion
}
