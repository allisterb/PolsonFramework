namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// so what is written here is the bridge rather than the algorithm. MIT, © Vicente Penades.
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

        // The decoded meshes hold the vertex data; the scene instance holds WHERE each one goes and,
        // for a skinned mesh, the joint matrices. Evaluated with no animation applied, so this is
        // the bind pose — correctly placed by the node hierarchy rather than sitting at the origin.
        var decoded = model.LogicalMeshes.Decode();
        var instance = SceneTemplate.Create(scene).CreateInstance();

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
                    // One call, and it is the whole of the skinning: SharpGLTF resolves morph
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

        if (verts.Count == 0) throw new ArgumentException($"No vertices in glTF '{display}'.");
        if (tris.Count == 0) throw new ArgumentException($"No triangles in glTF '{display}'.");

        // **`FaceMesh` indexes with `ushort`, so 65,535 vertices is a hard ceiling.** Silently
        // wrapping would draw a shredded mesh that renders perfectly and is obviously wrong only if
        // somebody looks, which is the failure mode this file exists to avoid elsewhere. A generated
        // character is usually well under — Stable Fast 3D targets a low polygon count — so this is
        // a limit to report rather than a reason to widen the buffer.
        if (verts.Count > ushort.MaxValue)
            throw new ArgumentException(
                $"glTF '{display}' has {verts.Count:N0} vertices; the mesh buffer indexes with " +
                $"16 bits and tops out at {ushort.MaxValue:N0}. Decimate it before loading.");

        var idx = new ushort[tris.Count];
        for (var i = 0; i < tris.Count; i++) idx[i] = (ushort)tris[i];

        return new FaceMesh(verts.ToArray(), uvs.ToArray(), idx, anyUvs, display)
        {
            Texture = FirstBaseColour(model)
        };
    }

    /// <summary>The first base-colour image in the file, decoded, or null when there is none.</summary>
    /// <remarks>
    /// <b>One texture, because <see cref="FaceMesh"/> holds one.</b> A glTF may give every primitive
    /// its own material; this takes the first base-colour image and the rest are not sampled. For a
    /// generated character that is almost always the whole asset — Stable Fast 3D writes a single
    /// UV-unwrapped atlas — but a multi-material import will lose the others, so it is stated rather
    /// than discovered.
    /// </remarks>
    private static SKBitmap? FirstBaseColour(ModelRoot model)
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
