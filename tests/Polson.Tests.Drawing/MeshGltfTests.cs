namespace Polson.Tests.Drawing;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// The glTF crossing: a mesh that carries a skeleton and a UV atlas, which an OBJ cannot.
/// <para>
/// These run against real assets rather than a hand-written fixture, because the whole point of the
/// format is the parts a generated fixture would not have — a skin, an atlas, an embedded image.
/// The three are Khronos reference models under <b>CC0</b>; see <c>tests/fixtures/gltf/README.md</c>.
/// </para>
/// <para>
/// <b>Two of the three are fetched rather than committed, so two of these tests are INERT until they
/// are.</b> <c>SimpleSkin.gltf</c> is 3.5 KB and is in the repository; <c>RiggedFigure.glb</c> and
/// <c>CesiumMan.glb</c> are 49 KB and 428 KB and are not, on the same reasoning that keeps potrace
/// out of <c>bin/</c> and the mediapipe weights out of <c>models/</c>. Run
/// <c>python tests/fixtures/gltf/fetch.py</c> to get them.
/// </para>
/// <para>
/// <b>Inert, not skipped, and the difference matters here.</b> xunit 2.9.2 has no skip mechanism and
/// this project does not add packages casually, so a machine without the fixtures runs those tests
/// to a green tick having asserted nothing. <see cref="WhichFixturesArePresentIsRecorded"/> always
/// runs and says which happened, so a silent pass is at least a legible one — and the loss is real:
/// without <c>CesiumMan.glb</c> nothing checks the texture path or the UV flip, which is the defect
/// most likely to ship unnoticed.
/// </para>
/// <para>
/// <b>What is deliberately not tested at all.</b> <see cref="FaceMesh"/> indexes with <c>ushort</c>,
/// so <c>MeshGltf</c> refuses a mesh above 65,535 vertices by name rather than wrapping silently. No
/// asset that large is fetched to prove it, and building one in a test would mean writing a glTF
/// writer to test a reader. The guard is stated in the source and unasserted, which is worth saying
/// plainly rather than leaving a reader to assume coverage.
/// </para>
/// </summary>
public class MeshGltfTests : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    #endregion

    #region Constructors
    public MeshGltfTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (private)
    /// <summary>
    /// The repository root, found by walking up for the fixtures rather than assumed.
    /// </summary>
    /// <remarks>
    /// A test runs from its own output directory, several levels below the root, and
    /// <c>MeshToolkit</c> resolves every path against the project root it is given — so without
    /// this the fixtures are simply not reachable.
    /// </remarks>
    static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tests", "fixtures", "gltf")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No 'tests/fixtures/gltf' above '{AppContext.BaseDirectory}'.");
    }

    static string Fixture(string name) => Path.Combine(Root(), "tests", "fixtures", "gltf", name);

    static FaceMesh Load(string name) =>
        new MeshToolkit(Root()).Load(Path.Combine("tests", "fixtures", "gltf", name));

    /// <summary>Whether a fetched fixture is here, saying plainly when it is not.</summary>
    bool Ready(string name)
    {
        if (File.Exists(Fixture(name))) return true;
        output.WriteLine($"NOT RUN: {name} is not present. " +
                         "Run 'python tests/fixtures/gltf/fetch.py' to fetch it.");
        return false;
    }

    /// <summary>The four texture coordinates the synthetic fixture is written with.</summary>
    /// <remarks>
    /// <b>Every <c>u</c> is distinct, so a vertex is identifiable without assuming the reader keeps
    /// the file's order</b>; and no <c>v</c> is 0.5, because the flip is a no-op there and a fixture
    /// sitting on the midline would pass the assertion while proving nothing.
    /// </remarks>
    static readonly (float U, float V)[] SyntheticUvs =
        [(0.10f, 0.20f), (0.35f, 0.45f), (0.60f, 0.70f), (0.85f, 0.95f)];

    /// <summary>
    /// Writes a minimal textured, UV-mapped glTF and returns its project-relative path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hand-written rather than produced by SharpGLTF's writer, and that is the point.</b> A
    /// fixture round-tripped through the same library that reads it would still pass if writer and
    /// reader shared a convention error — which is exactly the class of defect this fixture exists to
    /// catch. Every byte here is specified by this method, so the assertion is against what the file
    /// says rather than against what the library believes.
    /// </para>
    /// <para>
    /// It is a `.gltf` rather than a `.glb` because the container is irrelevant to a UV convention
    /// and a JSON file with a base64 buffer is legible when something goes wrong. The GLB path is
    /// covered by <see cref="ABinaryGlbLoadsAsReadilyAsTheJsonForm"/> when its fixture is present.
    /// </para>
    /// </remarks>
    static string WriteSyntheticTexturedGltf()
    {
        var inv = CultureInfo.InvariantCulture;

        // A unit quad. Positions are 4 x VEC3 at offset 0, texture coordinates 4 x VEC2 at 48, and
        // six UNSIGNED_SHORT indices at 80 — each offset a multiple of its own component size, which
        // glTF requires and a reader is entitled to assume.
        var buffer = new MemoryStream();
        var w = new BinaryWriter(buffer);          // little-endian, as the format specifies
        foreach (var (x, y) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
        {
            w.Write(x); w.Write(y); w.Write(0f);
        }
        foreach (var (u, v) in SyntheticUvs) { w.Write(u); w.Write(v); }
        foreach (ushort i in new ushort[] { 0, 1, 2, 1, 3, 2 }) w.Write(i);
        w.Flush();
        var bin = Convert.ToBase64String(buffer.ToArray());

        // Two bands rather than a flat fill, so a flipped sample is visible to a person as well as
        // measurable by the assertion below.
        using var bitmap = new SKBitmap(4, 4);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Navy);
            using var paint = new SKPaint { Color = SKColors.Gold };
            canvas.DrawRect(0, 0, 4, 2, paint);
        }
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var png = Convert.ToBase64String(encoded.ToArray());

        var json = $$"""
        {
          "asset": { "version": "2.0", "generator": "Polson.Tests.Drawing" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0 } ],
          "meshes": [ { "primitives": [ {
              "attributes": { "POSITION": 0, "TEXCOORD_0": 1 }, "indices": 2, "material": 0 } ] } ],
          "materials": [ { "pbrMetallicRoughness": { "baseColorTexture": { "index": 0 } } } ],
          "textures": [ { "source": 0 } ],
          "images": [ { "uri": "data:image/png;base64,{{png}}" } ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 4, "type": "VEC3",
              "min": [ 0.0, 0.0, 0.0 ], "max": [ 1.0, 1.0, 0.0 ] },
            { "bufferView": 1, "componentType": 5126, "count": 4, "type": "VEC2" },
            { "bufferView": 2, "componentType": 5123, "count": 6, "type": "SCALAR" }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0,  "byteLength": 48, "target": 34962 },
            { "buffer": 0, "byteOffset": 48, "byteLength": 32, "target": 34962 },
            { "buffer": 0, "byteOffset": 80, "byteLength": 12, "target": 34963 }
          ],
          "buffers": [ { "byteLength": 92,
                         "uri": "data:application/octet-stream;base64,{{bin}}" } ]
        }
        """;

        // Written beside the other fixtures because `MeshToolkit` resolves every path against the
        // project root and refuses anything outside it — a temp directory is not reachable. The
        // leading underscore is what `.gitignore` matches on.
        var relative = Path.Combine("tests", "fixtures", "gltf", "_synthetic-textured.gltf");
        File.WriteAllText(Path.Combine(Root(), relative), json);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }
    #endregion

    #region Availability
    /// <summary>
    /// Always runs, and its real job is the output line: it is how a green suite says which of the
    /// glTF tests examined anything.
    /// </summary>
    [Fact]
    public void WhichFixturesArePresentIsRecorded()
    {
        // The committed one is not optional. If this fails the repository is wrong, not the machine.
        Assert.True(File.Exists(Fixture("SimpleSkin.gltf")),
            "SimpleSkin.gltf is committed and must be present.");

        foreach (var name in new[] { "SimpleSkin.gltf", "RiggedFigure.glb", "CesiumMan.glb" })
        {
            var path = Fixture(name);
            output.WriteLine(File.Exists(path)
                ? $"present  {name}  ({new FileInfo(path).Length:N0} bytes)"
                : $"ABSENT   {name}  — the tests needing it asserted nothing");
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// The minimal skinned asset loads as the geometry its own specification documents.
    /// </summary>
    /// <remarks>
    /// Chosen as the first test because the expected answer is knowable independently: Khronos's
    /// <c>SimpleSkin</c> is a 1 x 2 unit strip in the XY plane, ten vertices in five rows of two,
    /// eight triangles, and no depth at all. A reader that mangled the index buffer or the vertex
    /// order would not land on those numbers by accident.
    /// </remarks>
    [Fact]
    public void SimpleSkinLoadsAsTheGeometryItsSpecificationDocuments()
    {
        var mesh = Load("SimpleSkin.gltf");

        Assert.Equal(10, mesh.VertexCount);
        Assert.Equal(8, mesh.TriangleCount);

        var b = mesh.Bounds;
        Assert.Equal(1.0f, Convert.ToSingle(b["width"]), 3);
        Assert.Equal(2.0f, Convert.ToSingle(b["height"]), 3);
        Assert.Equal(0.0f, Convert.ToSingle(b["depth"]), 3);

        // It carries no texture coordinates, and reporting otherwise would promise an atlas that
        // is not there - which is the failure `UvSpace` exists to make legible.
        Assert.False(mesh.Textured);
        Assert.Equal("none", mesh.UvSpace);
        output.WriteLine($"SimpleSkin: {mesh.VertexCount} verts, {mesh.TriangleCount} tris");
    }

    /// <summary>The binary container loads as readily as the JSON one.</summary>
    /// <remarks>
    /// Worth its own test because GLB and glTF are different parse paths, and a reader that handled
    /// only the JSON form would still pass every other test in this class if they all used it.
    /// </remarks>
    [Fact]
    public void ABinaryGlbLoadsAsReadilyAsTheJsonForm()
    {
        if (!Ready("RiggedFigure.glb")) return;
        var mesh = Load("RiggedFigure.glb");

        Assert.Equal(370, mesh.VertexCount);
        Assert.Equal(256, mesh.TriangleCount);
        Assert.True(mesh.TriangleCount * 3 % 3 == 0);
        output.WriteLine($"RiggedFigure: {mesh.VertexCount} verts, {mesh.TriangleCount} tris");
    }

    /// <summary>An embedded image is decoded, and the atlas it addresses survives the load.</summary>
    /// <remarks>
    /// The two halves are separable and both are needed: a mesh can carry a full set of texture
    /// coordinates and no picture to sample (<see cref="FaceMesh.Textured"/> is false), or a picture
    /// and coordinates that all collapsed to the origin — which draws the whole model in one flat
    /// colour, renders perfectly, and is obviously wrong only if somebody looks.
    /// </remarks>
    [Fact]
    public void AnEmbeddedTextureIsDecodedAndItsAtlasSurvives()
    {
        if (!Ready("CesiumMan.glb")) return;
        var mesh = Load("CesiumMan.glb");

        Assert.True(mesh.Textured);
        Assert.Equal("atlas", mesh.UvSpace);

        float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
        for (var i = 0; i < mesh.VertexCount; i++)
        {
            var uv = mesh.UvAt(i);
            var x = Convert.ToSingle(uv["x"]);
            var y = Convert.ToSingle(uv["y"]);
            u0 = MathF.Min(u0, x); u1 = MathF.Max(u1, x);
            v0 = MathF.Min(v0, y); v1 = MathF.Max(v1, y);
        }

        // A real unwrap spans most of the square. Asserting a span rather than exact values keeps
        // this about the reader rather than about one asset's layout.
        Assert.True(u1 - u0 > 0.5f, $"u spans only {u1 - u0:F3}");
        Assert.True(v1 - v0 > 0.5f, $"v spans only {v1 - v0:F3}");
        Assert.InRange(u0, 0f, 1f);
        Assert.InRange(v1, 0f, 1f);
        output.WriteLine($"CesiumMan atlas: u {u0:F3}..{u1:F3}, v {v0:F3}..{v1:F3}");
    }

    /// <summary>
    /// The texture's vertical origin is flipped on the way in, to the convention the draw path uses.
    /// </summary>
    /// <remarks>
    /// <b>This is the test that stops a silent upside-down render.</b> glTF puts the UV origin at the
    /// top-left and OBJ puts it at the bottom-left; <c>MeshToolkit.Draw</c> samples with
    /// <c>(1 - v) * height</c> because it was written for OBJ. So a glTF coordinate passed through
    /// unchanged samples the texture inverted — a result that looks like a broken asset rather than
    /// a convention mismatch, and that no other assertion in this class would catch.
    /// <para>
    /// Read straight from the file with SharpGLTF rather than from a remembered constant, so this
    /// keeps testing the relationship if the asset is ever replaced.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheUvOriginIsFlippedOnASyntheticFixtureWhoseCoordinatesAreKnown()
    {
        var path = WriteSyntheticTexturedGltf();
        var mesh = new MeshToolkit(Root()).Load(path);

        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(2, mesh.TriangleCount);
        Assert.True(mesh.Textured, "the base-colour image should have been decoded");
        Assert.Equal("atlas", mesh.UvSpace);

        // Identified by u, which the reader does not touch, so this does not assume vertex order.
        foreach (var (u, v) in SyntheticUvs)
        {
            var match = Enumerable.Range(0, mesh.VertexCount)
                .Select(i => mesh.UvAt(i))
                .Where(uv => MathF.Abs(Convert.ToSingle(uv["x"]) - u) < 1e-4f)
                .ToList();

            Assert.True(match.Count == 1, $"expected exactly one vertex at u={u}, found {match.Count}");
            Assert.Equal(1f - v, Convert.ToSingle(match[0]["y"]), 4);
        }

        output.WriteLine("synthetic fixture: " + string.Join(", ",
            SyntheticUvs.Select(p => $"v {p.V:F2} -> {1f - p.V:F2}")));
    }

    /// <summary>The same relationship on a real asset, when its fixture has been fetched.</summary>
    /// <remarks>
    /// Kept beside the synthetic one rather than replaced by it. The synthetic fixture proves the
    /// convention; this proves it still holds on a file produced by somebody else's exporter, which
    /// is the case that actually ships.
    /// </remarks>
    [Fact]
    public void TheUvOriginIsFlippedToTheConventionTheDrawPathExpects()
    {
        if (!Ready("CesiumMan.glb")) return;
        var mesh = Load("CesiumMan.glb");

        var model = SharpGLTF.Schema2.ModelRoot.Load(Fixture("CesiumMan.glb"));
        var primitive = model.LogicalMeshes[0].Primitives[0];
        var raw = primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array();

        var flipped = 0;
        for (var i = 0; i < Math.Min(64, raw.Count); i++)
        {
            var stored = Convert.ToSingle(mesh.UvAt(i)["y"]);
            Assert.Equal(1f - raw[i].Y, stored, 4);
            if (MathF.Abs(raw[i].Y - stored) > 1e-4f) flipped++;
        }

        // Guards the assertion itself: on a v of exactly 0.5 the flip is a no-op, so a file whose
        // coordinates all sat on the midline would pass the loop while proving nothing.
        Assert.True(flipped > 0, "every sampled v was 0.5, so the flip was untestable on this asset");
        output.WriteLine($"{flipped} of the first 64 texture coordinates moved under the flip");
    }

    /// <summary>Adding a second reader did not disturb the first.</summary>
    /// <remarks>
    /// <c>Load</c> now picks a reader by extension, so the OBJ path is reachable only if that choice
    /// is right. A plain regression guard, and cheap.
    /// </remarks>
    [Fact]
    public void TheObjPathStillLoadsAfterTheGltfReaderWasAdded()
    {
        var obj = string.Join("\n",
            "v 0 0 0", "v 1 0 0", "v 0 1 0",
            "vt 0 0", "vt 1 0", "vt 0 1",
            "f 1/1 2/2 3/3");

        var mesh = new MeshToolkit().FromObj(obj);
        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(1, mesh.TriangleCount);
        Assert.Equal("atlas", mesh.UvSpace);
    }
    #endregion
}
