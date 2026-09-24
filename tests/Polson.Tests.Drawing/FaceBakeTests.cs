namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Baking a drawn face into a mesh's atlas: <c>mesh.faceSheet()</c>, <c>withFace</c>, <c>withExpression</c>.
/// </summary>
/// <remarks>
/// <b>None of these needs the face backend.</b> Every test passes the three anchor points itself, so
/// the placement arithmetic is checked on any machine; finding the face by detection is covered by
/// running it against a real character, which is not a fixture this repository can carry.
/// </remarks>
public class FaceBakeTests : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    #endregion

    #region Constructors
    public FaceBakeTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (private)
    /// <summary>
    /// A flat "face": a 12 x 12 grid over x, y in [-3, 3] at z = 0, facing +Z, mapped into the middle
    /// half of a grey atlas. Where any model point lands in the atlas is known in closed form.
    /// </summary>
    static FaceMesh FlatFace()
    {
        const int n = 12;
        var verts = new List<SKPoint3>();
        var uvs = new List<SKPoint>();
        for (var r = 0; r <= n; r++)
            for (var c = 0; c <= n; c++)
            {
                float x = -3f + (6f * c / n), y = -3f + (6f * r / n);
                verts.Add(new SKPoint3(x, y, 0f));
                uvs.Add(new SKPoint(U(x), V(y)));
            }

        var idx = new List<ushort>();
        for (var r = 0; r < n; r++)
            for (var c = 0; c < n; c++)
            {
                var a = (ushort)((r * (n + 1)) + c);
                var b = (ushort)(a + 1);
                var d = (ushort)(a + n + 1);
                var e = (ushort)(d + 1);
                idx.AddRange([a, b, d, b, e, d]);   // counter-clockwise seen from +Z, so the normal faces +Z
            }

        var tex = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(tex)) canvas.Clear(Grey);
        return new FaceMesh([.. verts], [.. uvs], [.. idx], true, "flat-face") { Texture = tex };
    }

    // Model space to atlas coordinates: the grid fills the middle half, v measured up from the bottom
    // as an OBJ writes it — the convention FaceMesh stores.
    static float U(float x) => 0.25f + ((x + 3f) / 6f * 0.5f);
    static float V(float y) => 0.25f + ((y + 3f) / 6f * 0.5f);

    static SKPoint AtlasPixel(float x, float y, int res) => new(U(x) * res, (1f - V(y)) * res);

    static Dictionary<string, object?> Anchors() => new()
    {
        ["eyeLeft"] = new Dictionary<string, object?> { ["x"] = -1f, ["y"] = 0.8f },
        ["eyeRight"] = new Dictionary<string, object?> { ["x"] = 1f, ["y"] = 0.8f },
        ["mouth"] = new Dictionary<string, object?> { ["x"] = 0f, ["y"] = -1.2f }
    };

    static SKPoint Centre(object? o)
    {
        var d = (System.Collections.IDictionary)o!;
        return new SKPoint(Convert.ToSingle(d["x"]), Convert.ToSingle(d["y"]));
    }

    static readonly SKColor Grey = new(0x80, 0x80, 0x80);
    #endregion

    #region Methods
    /// <summary>What is drawn at the head's right eye lands on the model's right eye, and nowhere else.</summary>
    /// <remarks>
    /// <b>The test that the whole route rests on.</b> The sheet's context carries a transform from the
    /// Loomis head into sheet pixels, and the bake then carries sheet pixels into atlas pixels one
    /// triangle at a time. A mark put on the head's eye must come out at the atlas position of the
    /// model's eye anchor — computed here from the grid's own UV formula rather than by the code under
    /// test — and the atlas outside the face must be untouched.
    /// </remarks>
    [Fact]
    public void AMarkOnTheHeadsEyeLandsOnTheModelsEyeInTheAtlas()
    {
        var mesh = FlatFace();
        var sheet = mesh.FaceSheet(Anchors());
        Assert.Equal("anchors", sheet.Found);

        SKPoint far = Centre(((System.Collections.IDictionary)sheet.Head["farEye"]!)["center"]);
        SKPoint near = Centre(((System.Collections.IDictionary)sheet.Head["nearEye"]!)["center"]);
        var right = far.X > near.X ? far : near;
        var radius = 0.15f * MathF.Abs(far.X - near.X);

        var ctx = sheet.Context;
        ctx.FillStyle = "#ff0000";
        ctx.BeginPath();
        ctx.Arc(right.X, right.Y, radius, 0f, MathF.Tau);
        ctx.Fill();

        const int res = 512;
        var baked = mesh.WithFace(sheet, new Dictionary<string, object?> { ["resolution"] = res });
        Assert.NotSame(mesh.Texture, baked.Texture);
        Assert.Equal(res, baked.Texture!.Width);

        var at = AtlasPixel(1f, 0.8f, res);
        var hit = baked.Texture.GetPixel((int)at.X, (int)at.Y);
        output.WriteLine($"right eye -> atlas ({at.X:F0}, {at.Y:F0}) = {hit}");
        Assert.True(hit.Red > 200 && hit.Green < 60 && hit.Blue < 60, $"expected red at the right eye, got {hit}");

        var other = AtlasPixel(-1f, 0.8f, res);
        var miss = baked.Texture.GetPixel((int)other.X, (int)other.Y);
        Assert.True(miss.Red < 200 || miss.Green > 60, $"the left eye should not be red, got {miss}");

        var corner = baked.Texture.GetPixel(8, 8);
        Assert.Equal(Grey, corner.WithAlpha(255));
    }

    /// <summary>A surface hidden behind the face is not baked, even though it faces forward inside the outline.</summary>
    /// <remarks>
    /// <b>Found by turning a real character's head past 45°.</b> A crossbow slung behind Julie's head
    /// faced forward and lay inside the face's outline from the front, so it took the face's skin —
    /// invisible at 0°, a pale wedge behind the head the moment it turned. Here a second plane sits
    /// behind the face, mapped to its own corner of the atlas; it must come out untouched.
    /// </remarks>
    [Fact]
    public void ASurfaceBehindTheFaceIsNotBaked()
    {
        var face = FlatFace();
        var v = face.Vertices.ToList();
        var uv = face.Uvs.ToList();
        var idx = face.Indices.ToList();

        // A square behind the face (z = -1), facing +Z, sampling the atlas's top-left eighth.
        var b = (ushort)v.Count;
        v.AddRange([new(-2f, -2f, -1f), new(2f, -2f, -1f), new(-2f, 2f, -1f), new(2f, 2f, -1f)]);
        uv.AddRange([new(0.02f, 0.87f), new(0.12f, 0.87f), new(0.02f, 0.97f), new(0.12f, 0.97f)]);
        idx.AddRange([b, (ushort)(b + 1), (ushort)(b + 2), (ushort)(b + 1), (ushort)(b + 3), (ushort)(b + 2)]);
        var mesh = new FaceMesh([.. v], [.. uv], [.. idx], true, "face-and-backplate") { Texture = face.Texture };

        var sheet = mesh.FaceSheet(Anchors());
        var ctx = sheet.Context;
        ctx.SetTransform();
        ctx.FillStyle = "#ff0000";
        ctx.FillRect(0, 0, sheet.Width, sheet.Height);     // everything the bake takes comes out red

        const int res = 512;
        var baked = mesh.WithFace(sheet, new Dictionary<string, object?> { ["resolution"] = res });

        var faceHit = baked.Texture!.GetPixel((int)AtlasPixel(0f, 0f, res).X, (int)AtlasPixel(0f, 0f, res).Y);
        var back = baked.Texture.GetPixel((int)(0.07f * res), (int)((1f - 0.92f) * res));
        output.WriteLine($"face {faceHit}, back plate {back}");
        Assert.True(faceHit.Red > 200 && faceHit.Green < 60, $"the face itself should be baked, got {faceHit}");
        Assert.Equal(Grey, back.WithAlpha(255));
    }

    /// <summary>A face baked into the atlas survives posing.</summary>
    /// <remarks>
    /// <b>A pose rebuilds the mesh from the rig, and the rig only knows the file's own texture</b>, so
    /// before this was fixed every baked face vanished the moment the character moved — silently,
    /// since the original face still rendered. Checked on the committed rig with a texture attached.
    /// </remarks>
    [Fact]
    public void PosingKeepsATextureThatDidNotComeFromTheFile()
    {
        var loaded = new MeshToolkit(Root()).Load(System.IO.Path.Combine("tests", "fixtures", "gltf", "SimpleSkin.gltf"));
        var mine = new SKBitmap(8, 8);
        var dressed = new FaceMesh(loaded.Vertices, loaded.Uvs, loaded.Indices, loaded.HasUvs, loaded.Source)
        { Texture = mine, Rig = loaded.Rig, Reference = loaded.Reference };

        var posed = dressed.Pose(new Dictionary<string, object?>
        {
            [dressed.Joints[0]] = new Dictionary<string, object?> { ["zDeg"] = 30.0 }
        });

        Assert.Same(mine, posed.Texture);
    }

    /// <summary>The fast path draws, bakes and changes the face.</summary>
    [Fact]
    public void WithExpressionBakesAFaceIntoANewAtlas()
    {
        var mesh = FlatFace();
        var options = Anchors();
        options["resolution"] = 512;

        var angry = mesh.WithExpression("anger", 1f, options);
        var units = mesh.WithExpression(new Dictionary<string, object?> { ["AU12"] = 0.8 }, 1f, options);

        Assert.Equal(512, angry.Texture!.Width);
        Assert.NotSame(angry.Texture, units.Texture);
        Assert.Equal(mesh.Texture!.GetPixel(0, 0), mesh.Texture.GetPixel(63, 63));   // the original is untouched

        // Something dark was drawn on the face: ink, somewhere between the eyes and the mouth.
        var dark = 0;
        for (var y = (int)AtlasPixel(0, 1.5f, 512).Y; y < (int)AtlasPixel(0, -1.8f, 512).Y; y++)
            for (var x = (int)AtlasPixel(-1.8f, 0, 512).X; x < (int)AtlasPixel(1.8f, 0, 512).X; x++)
                if (angry.Texture.GetPixel(x, y).Red < 60) dark++;
        output.WriteLine($"ink pixels on the face: {dark}");
        Assert.True(dark > 50, $"expected drawn features on the face, found {dark} dark pixels");
    }

    /// <summary>The refusals, each because the alternative is a picture that is wrong without saying so.</summary>
    [Fact]
    public void BakingRefusesWhatItCannotDo()
    {
        var mesh = FlatFace();

        // A sheet from one mesh baked onto another would land the face wherever that mesh's atlas says.
        var otherSheet = FlatFace().FaceSheet(Anchors());
        var wrongMesh = Assert.Throws<ArgumentException>(() => mesh.WithFace(otherSheet));
        Assert.Contains("different mesh", wrongMesh.Message);

        // A mesh textured by fitTexture has pixel coordinates, not an atlas, so there is nothing to bake into.
        var fitted = mesh.FitTexture(new SKBitmap(16, 16), new Dictionary<string, object?>
        {
            ["eyeLeft"] = new Dictionary<string, object?> { ["x"] = 4, ["y"] = 6 },
            ["eyeRight"] = new Dictionary<string, object?> { ["x"] = 12, ["y"] = 6 },
            ["mouth"] = new Dictionary<string, object?> { ["x"] = 8, ["y"] = 12 }
        });
        var noAtlas = Assert.Throws<ArgumentException>(() => fitted.FaceSheet(Anchors()));
        Assert.Contains("no texture atlas", noAtlas.Message);

        // Half the anchors is refused by name rather than guessed at.
        var partial = Assert.Throws<ArgumentException>(() => mesh.FaceSheet(new Dictionary<string, object?>
        {
            ["eyeLeft"] = new Dictionary<string, object?> { ["x"] = -1f, ["y"] = 0.8f }
        }));
        Assert.Contains("eyeRight", partial.Message);

        // No anchors and no backend: say what to pass. Only asserted where the backend is absent.
        if (!FaceDetector.Available)
        {
            var noBackend = Assert.Throws<ArgumentException>(() => mesh.FaceSheet());
            Assert.Contains("eyeLeft", noBackend.Message);
            output.WriteLine(noBackend.Message);
        }
    }

    static string Root()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "tests", "fixtures", "gltf")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new System.IO.DirectoryNotFoundException("tests/fixtures/gltf");
    }
    #endregion
}
