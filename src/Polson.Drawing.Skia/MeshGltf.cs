namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using SharpGLTF.Runtime;
using SharpGLTF.Schema2;
using SkiaSharp;

/// <summary>
/// Reads glTF 2.0 and GLB into a <see cref="FaceMesh"/>, flattening the scene to one buffer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> This is the back half of <see cref="MeshToolkit.Load"/>, which
/// picks a reader by extension; a script only ever sees <c>Mesh.load('character.glb')</c>.
/// </para>
/// <para>
/// <b>Why glTF rather than another OBJ path.</b> An OBJ carries geometry and a UV atlas and nothing
/// else — it cannot express a skeleton, so a character read from one can be turned but never posed.
/// glTF carries <c>JOINTS_0</c>, <c>WEIGHTS_0</c>, <c>inverseBindMatrices</c> and a node hierarchy,
/// and it is what every generator on the table actually emits: Stable Fast 3D writes GLB, VRoid
/// Studio and UniRig write glTF-derived formats. One reader, every route.
/// </para>
/// <para>
/// <b>The skinning is SharpGLTF's, not ours.</b> <c>SkinnedTransform</c> in <c>SharpGLTF.Core</c>
/// implements linear blend skinning with sparse weights, weight normalisation, inverse bind
/// matrices and morph targets. Applying it is one call per vertex — <c>GetPosition(i, xform)</c> —
/// so what is written here is the bridge and the pose arithmetic rather than the algorithm. MIT,
/// © Vicente Penades.
/// </para>
/// </remarks>
internal static class MeshGltf
{
    #region Methods
    /// <summary>Whether this reader claims the path, by extension.</summary>
    internal static bool Handles(string path) =>
        path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the file's default scene in its bind pose.</summary>
    /// <param name="fullPath">An absolute path, already contained by the caller.</param>
    /// <param name="display">The project-relative path, for messages and <c>Source</c>.</param>
    internal static FaceMesh Load(string fullPath, string display)
    {
        ModelRoot model;
        try
        {
            model = ModelRoot.Load(fullPath);
        }
        catch (Exception ex)
        {
            // A malformed asset is a bad file rather than a bug here, and the caller can act on the
            // reason. Wrapped so the message names the path the script actually passed.
            throw new ArgumentException($"Could not read glTF '{display}': {ex.Message}", nameof(fullPath), ex);
        }

        var scene = model.DefaultScene ?? model.LogicalScenes.FirstOrDefault()
            ?? throw new ArgumentException($"glTF '{display}' declares no scene.");

        // The rig is kept open rather than discarded once the bind pose has been read, because
        // posing is re-evaluating this same scene at different joint transforms. Re-reading the
        // file per pose would work and would re-decode every vertex in order to bend one elbow.
        return new MeshRig(model, scene, display).Snapshot(bind: null);
    }

    /// <summary>The first base-colour image in the file, decoded, or null when there is none.</summary>
    /// <remarks>
    /// <b>One texture, because <see cref="FaceMesh"/> holds one.</b> A glTF may give every primitive
    /// its own material; this takes the first base-colour image and the rest are not sampled. For a
    /// generated character that is almost always the whole asset — Stable Fast 3D writes a single
    /// UV-unwrapped atlas — but a multi-material import will lose the others, so it is stated rather
    /// than discovered.
    /// </remarks>
    internal static SKBitmap? FirstBaseColour(ModelRoot model)
    {
        foreach (var material in model.LogicalMaterials)
        {
            var channel = material.FindChannel("BaseColor");
            var image = channel?.Texture?.PrimaryImage;
            if (image is null) continue;

            var bytes = image.Content.Content;
            if (bytes.Length == 0) continue;

            // A decode failure is not worth throwing over: the mesh is still usable as a wireframe,
            // and `FaceMesh.Textured` already reports honestly when there is no picture to sample.
            var bitmap = SKBitmap.Decode(bytes.ToArray());
            if (bitmap is not null) return bitmap;
        }

        return null;
    }
    #endregion
}

/// <summary>
/// A loaded glTF held open so its skeleton can be posed, and the evaluation that reads it back.
/// </summary>
/// <remarks>
/// <b>Not exposed to scripts.</b> A script reaches this through <c>mesh.joints</c>,
/// <c>mesh.posable</c> and <c>mesh.pose(...)</c> on the <see cref="FaceMesh"/> the load produced.
/// </remarks>
internal sealed class MeshRig
{
    #region Constructors
    internal MeshRig(ModelRoot model, Scene scene, string source)
    {
        this.model = model;
        this.source = source;

        decoded = model.LogicalMeshes.Decode();
        instance = SceneTemplate.Create(scene).CreateInstance();

        // **A scene node is not a bone.** An exporter's scene carries a root, the armature object and
        // the mesh's own node beside the joints — Blender's glTF writer gives `world`, `Armature` and
        // `geometry_0` — and listing those as joints hands a caller names that rotate the whole model
        // or nothing, not a limb. The skins say which nodes are bones; that is the list worth giving.
        var bones = model.LogicalSkins.SelectMany(s => s.Joints).Select(n => n.LogicalIndex).ToHashSet();
        var logical = instance.Armature.LogicalNodes;
        var fileNodes = MatchFileNodes(scene, logical);

        // **glTF does not require a node to be named, and a rigged file may have none.** Khronos's
        // own `SimpleSkin` is exactly that: two joints, both anonymous. An API keyed only on names
        // reports such a file as having no joints at all, which reads as "this mesh cannot be
        // posed" when the truth is "this mesh does not label its bones" — so every node also gets a
        // positional handle, and a caller passes back whatever `mesh.joints` handed it either way.
        var handles = new List<string>();
        nodes = [];
        for (var i = 0; i < logical.Count; i++)
        {
            var handle = string.IsNullOrEmpty(logical[i].Name) ? $"node:{i}" : logical[i].Name;

            // A file free to leave names off is also free to repeat them. Last-wins would make one
            // of a duplicated pair silently unreachable, so the later one takes its index instead.
            if (nodes.ContainsKey(handle) || sceneNodes.Contains(handle)) handle = $"node:{i}";

            if (fileNodes[i] is not { } file || !bones.Contains(file.LogicalIndex))
            {
                sceneNodes.Add(handle);
                continue;
            }

            nodes[handle] = logical[i];
            handles.Add(handle);
        }

        JointNames = [.. handles];
        Skinned = model.LogicalSkins.Count > 0;
    }
    #endregion

    #region Properties
    /// <summary>
    /// A handle for every bone the file's skins declare, parents first: the exporter's name where
    /// there is one, and <c>node:{i}</c> where there is not. Empty when the file carries no skin.
    /// </summary>
    internal string[] JointNames { get; }

    /// <summary>Whether the file declares a skin, so rotating a joint deforms geometry.</summary>
    /// <remarks>
    /// Read from the skins rather than inferred from the node count, because every glTF has nodes
    /// and only a rigged one has a skin — counting nodes would call an unrigged mesh posable.
    /// </remarks>
    internal bool Skinned { get; }
    #endregion

    #region Methods
    /// <summary>Evaluates the scene as it stands and captures it as a <see cref="FaceMesh"/>.</summary>
    /// <param name="bind">
    /// The bind-pose vertices to record as the mesh's reference, or null when this IS the bind pose.
    /// </param>
    internal FaceMesh Snapshot(SKPoint3[]? bind)
    {
        List<SKPoint3> verts = [];
        List<SKPoint> uvs = [];
        List<int> tris = [];
        var anyUvs = false;

        // `SceneInstance` is itself `IEnumerable<DrawableInstance>`, and the enumerator is where the
        // work happens: it skips invisible nodes and hands back each drawable already carrying the
        // armature's evaluated transform.
        foreach (var drawable in instance)
        {
            var mesh = decoded[drawable.Template.LogicalMeshIndex];
            var xform = drawable.Transform;

            foreach (var prim in mesh.Primitives)
            {
                var start = verts.Count;
                var hasUv = prim.TexCoordsCount > 0;
                anyUvs |= hasUv;

                for (var i = 0; i < prim.VertexCount; i++)
                {
                    // One call, and it is the whole of the skinning: SharpGLTF resolves the morph
                    // targets and the weighted joint blend behind this.
                    var p = prim.GetPosition(i, xform);
                    verts.Add(new SKPoint3(p.X, p.Y, p.Z));

                    // **glTF's UV origin is top-left; OBJ's is bottom-left.** `MeshToolkit.Draw`
                    // samples with `(1 - v) * height` because it was written for OBJ, so a glTF
                    // coordinate passed through unchanged renders the texture upside down — which
                    // looks like a broken asset rather than a convention mismatch. Flipping here
                    // means one convention reaches `FaceMesh` whatever the file was.
                    var t = hasUv ? prim.GetTextureCoord(i, 0) : default;
                    uvs.Add(hasUv ? new SKPoint(t.X, 1f - t.Y) : default);
                }

                foreach (var (a, b, c) in prim.TriangleIndices)
                {
                    tris.Add(start + a);
                    tris.Add(start + b);
                    tris.Add(start + c);
                }
            }
        }

        if (verts.Count == 0) throw new ArgumentException($"No vertices in glTF '{source}'.");
        if (tris.Count == 0) throw new ArgumentException($"No triangles in glTF '{source}'.");

        // **`FaceMesh` indexes with `ushort`, so 65,535 vertices is a hard ceiling.** Silently
        // wrapping would draw a shredded mesh that renders perfectly and is obviously wrong only if
        // somebody looks, which is the failure mode this file exists to avoid elsewhere. A generated
        // character is usually well under — Stable Fast 3D targets a low polygon count — so this is
        // a limit to report rather than a reason to widen the buffer.
        if (verts.Count > ushort.MaxValue)
            throw new ArgumentException(
                $"glTF '{source}' has {verts.Count:N0} vertices; the mesh buffer indexes with " +
                $"16 bits and tops out at {ushort.MaxValue:N0}. Decimate it before loading.");

        var idx = new ushort[tris.Count];
        for (var i = 0; i < tris.Count; i++) idx[i] = (ushort)tris[i];

        var placed = verts.ToArray();
        return new FaceMesh(placed, uvs.ToArray(), idx, anyUvs, source)
        {
            Texture = texture ??= MeshGltf.FirstBaseColour(model),
            Rig = this,

            // A posed mesh keeps the BIND geometry as its reference, so `shape` and `expression`
            // bands still key on where a feature anatomically is rather than on where a pose has
            // swung it — the same reasoning `fitOutline` is already held to.
            Reference = bind ?? placed
        };
    }

    /// <summary>Resets to bind, applies the rotations, and captures the result.</summary>
    internal FaceMesh Pose(IDictionary? pose, SKPoint3[] bind)
    {
        if (!Skinned)
            throw new ArgumentException(
                $"glTF '{source}' carries no skin, so there are no joints to pose. It can still be " +
                "turned with yawDeg/pitchDeg/rollDeg on Mesh.draw(...). A generator that outputs an " +
                "unrigged mesh — Stable Fast 3D, CharacterGen — needs a rigging step before this.");

        // Reset first, every time, so poses never accumulate and the mesh a pose was taken from is
        // never disturbed. That is what lets `mesh.pose(...)` read as a pure function of its
        // argument, which every other call that changes a mesh here already is.
        instance.Armature.SetPoseTransforms();

        if (pose is not null)
            foreach (var key in pose.Keys)
            {
                var name = Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!nodes.TryGetValue(name, out var node)) throw Unknown(name);

                // Row-vector convention, so `delta * local` rotates the joint about its OWN axes
                // and the bind transform then carries the result into the parent's space. The other
                // order rotates about the PARENT's axes, which looks plausible on a root node and
                // is wrong on every elbow below it.
                node.LocalMatrix = Rotation(JsInterop.AsDict(pose[key]), name) * node.LocalMatrix;
            }

        return Snapshot(bind);
    }
    #endregion

    #region Methods (private)
    /// <summary>One joint's rotation, from <c>{ xDeg, yDeg, zDeg }</c>.</summary>
    /// <remarks>
    /// Composed through <c>CreateFromYawPitchRoll</c> rather than by multiplying three matrices by
    /// hand, so the order is the framework's documented one — yaw about Y, then pitch about X, then
    /// roll about Z — rather than a convention invented here and liable to be written down wrongly.
    /// </remarks>
    static Matrix4x4 Rotation(IDictionary? spec, string joint)
    {
        if (spec is null)
            throw new ArgumentException(
                $"The pose for joint '{joint}' is not an object. Give it rotations in degrees, as " +
                "{ xDeg, yDeg, zDeg } — any of the three may be left out.");

        float x = 0f, y = 0f, z = 0f;
        foreach (var key in spec.Keys)
        {
            var name = Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
            var value = Convert.ToSingle(spec[name], CultureInfo.InvariantCulture);
            switch (name)
            {
                case "xDeg": x = value; break;
                case "yDeg": y = value; break;
                case "zDeg": z = value; break;
                default:
                    throw new ArgumentException(
                        $"Joint '{joint}' was given '{name}', which is not a rotation. " +
                        "Accepted: xDeg, yDeg, zDeg.");
            }
        }

        const float rad = MathF.PI / 180f;
        return Matrix4x4.CreateFromYawPitchRoll(y * rad, x * rad, z * rad);
    }

    /// <summary>The file node each runtime node was built from, or null where none matches.</summary>
    /// <remarks>
    /// <b>SharpGLTF exposes no link back.</b> The runtime flattens the scene parents-first rather than
    /// in the file's order, and the template that remembers each node's source is internal — so the
    /// correspondence is rebuilt by structure: the same parent, the same name, the same bind transform.
    /// Parents-first order guarantees a node's parent is matched before it is. Siblings identical in
    /// all three are interchangeable for this purpose, and are claimed in file order.
    /// </remarks>
    static Node?[] MatchFileNodes(Scene scene, IReadOnlyList<NodeInstance> runtime)
    {
        var matched = new Node?[runtime.Count];
        var byInstance = new Dictionary<NodeInstance, Node>(ReferenceEqualityComparer.Instance);
        var claimed = new HashSet<int>();

        for (var i = 0; i < runtime.Count; i++)
        {
            var node = runtime[i];
            IEnumerable<Node> candidates = node.VisualParent is null
                ? scene.VisualChildren
                : byInstance.TryGetValue(node.VisualParent, out var parent) ? parent.VisualChildren : [];

            var open = candidates.Where(n => !claimed.Contains(n.LogicalIndex)
                                          && string.Equals(n.Name ?? "", node.Name ?? "", StringComparison.Ordinal))
                                 .ToArray();
            var file = open.FirstOrDefault(n => Near(n.LocalMatrix, node.LocalMatrix)) ?? open.FirstOrDefault();
            if (file is null) continue;

            claimed.Add(file.LogicalIndex);
            byInstance[node] = file;
            matched[i] = file;
        }

        return matched;
    }

    static bool Near(Matrix4x4 a, Matrix4x4 b) =>
        MathF.Abs(a.M11 - b.M11) + MathF.Abs(a.M12 - b.M12) + MathF.Abs(a.M13 - b.M13) +
        MathF.Abs(a.M21 - b.M21) + MathF.Abs(a.M22 - b.M22) + MathF.Abs(a.M23 - b.M23) +
        MathF.Abs(a.M31 - b.M31) + MathF.Abs(a.M32 - b.M32) + MathF.Abs(a.M33 - b.M33) +
        MathF.Abs(a.M41 - b.M41) + MathF.Abs(a.M42 - b.M42) + MathF.Abs(a.M43 - b.M43) < 1e-4f;

    /// <summary>A joint name the file does not have, refused with the nearest ones it does.</summary>
    /// <remarks>
    /// Joint names come from whoever exported the file — <c>mixamorig:LeftForeArm</c>,
    /// <c>J_Bip_L_UpperArm</c>, <c>bone_012</c> — so they cannot be guessed, and a bare "unknown
    /// joint" leaves a caller with nowhere to go. A humanoid skeleton carries sixty or more, so the
    /// whole list would be noise; these are the ones sharing text with what was asked for.
    /// </remarks>
    ArgumentException Unknown(string name)
    {
        if (sceneNodes.Contains(name))
            return new ArgumentException(
                $"'{name}' in glTF '{source}' is a scene node, not a bone of the skin, so rotating it " +
                "would move the whole model or nothing rather than a limb. Turn the whole mesh with " +
                "yawDeg/pitchDeg/rollDeg on Mesh.draw(...); read mesh.joints for the bones.");

        var near = nodes.Keys
            .Where(k => k.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                        (name.Length > 2 && name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Take(8).ToArray();

        var hint = near.Length > 0
            ? $" Did you mean {string.Join(", ", near.Select(n => $"'{n}'"))}?"
            : $" Read mesh.joints for the {JointNames.Length} this file declares.";

        return new ArgumentException($"glTF '{source}' has no node named '{name}'.{hint}");
    }
    #endregion

    #region Fields
    readonly ModelRoot model;
    readonly SceneInstance instance;
    readonly IMeshDecoder<Material>[] decoded;
    readonly Dictionary<string, NodeInstance> nodes;
    readonly HashSet<string> sceneNodes = new(StringComparer.Ordinal);
    readonly string source;
    SKBitmap? texture;
    #endregion
}
