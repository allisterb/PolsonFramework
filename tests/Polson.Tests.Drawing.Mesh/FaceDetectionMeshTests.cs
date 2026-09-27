namespace Polson.Tests.Drawing;

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>detection.mesh()</c>: landmarks with depth become a turnable face fitted to the portrait.
/// </summary>
/// <remarks>
/// <b>Built from a written detection, so none of these needs the face backend</b> — only the last
/// reads the real model bundle, and it says so when the bundle is not here.
/// </remarks>
public class FaceDetectionMeshTests : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    #endregion

    #region Constructors
    public FaceDetectionMeshTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (private)
    /// <summary>A detection of 478 landmarks in a 400 x 300 image: landmark i at (10 + i % 20 * 15, 20 + i / 20 * 10), depth i / 10.</summary>
    static string Json(bool depth)
    {
        var sb = new StringBuilder("{\"found\":true,\"width\":400,\"height\":300,\"pad\":0,\"landmarks\":[");
        for (var i = 0; i < 478; i++)
        {
            if (i > 0) sb.Append(',');
            var (x, y, z) = Landmark(i);
            sb.Append(CultureInfo.InvariantCulture, $"[{x},{y}");
            if (depth) sb.Append(CultureInfo.InvariantCulture, $",{z}");
            sb.Append(']');
        }
        return sb.Append("]}").ToString();
    }

    static (float X, float Y, float Z) Landmark(int i) => (10f + (i % 20 * 15f), 20f + (i / 20 * 10f), i / 10f);

    static ushort[] OneTriangle() => [0, 1, 20];
    #endregion

    #region Methods
    /// <summary>The third value per landmark is read as depth, and reported by <c>at(i)</c>.</summary>
    [Fact]
    public void DepthIsReadAndReported()
    {
        var det = FaceDetection.Parse(Json(depth: true));
        Assert.True(det.HasDepth);
        Assert.Equal(1.5f, Convert.ToSingle(det.At(15)!["z"]), 3);

        var flat = FaceDetection.Parse(Json(depth: false));
        Assert.False(flat.HasDepth);
        Assert.False(flat.At(15)!.ContainsKey("z"));
    }

    /// <summary>Vertices are the landmarks centred on the face, y up, depth toward the viewer; each samples its own pixel.</summary>
    /// <remarks>
    /// <b>Only the first 468 landmarks are used</b> — the canonical topology's — so the ten iris
    /// points never become vertices the triangles do not know about.
    /// </remarks>
    [Fact]
    public void TheMeshIsTheLandmarksInTheImagesOwnPixels()
    {
        var det = FaceDetection.Parse(Json(depth: true));
        var image = new SKBitmap(400, 300);
        var mesh = det.MeshWith(OneTriangle, image);

        Assert.Equal(468, mesh.VertexCount);
        Assert.Equal("atlas", mesh.UvSpace);
        Assert.True(mesh.Textured);

        // The first 468 landmarks span x 10..295 and y 20..250, so the face is centred on (152.5, 135).
        var (x, y, z) = Landmark(45);
        var v = mesh.Vertex(45);
        Assert.Equal(x - 152.5f, Convert.ToSingle(v["x"]), 3);
        Assert.Equal(135f - y, Convert.ToSingle(v["y"]), 3);
        Assert.Equal(z, Convert.ToSingle(v["z"]), 3);

        var uv = mesh.UvAt(45);
        Assert.Equal(x / 400f, Convert.ToSingle(uv["x"]), 4);
        Assert.Equal(1f - (y / 300f), Convert.ToSingle(uv["y"]), 4);
    }

    /// <summary>The refusals, each because the alternative is a face that is wrong without saying so.</summary>
    [Fact]
    public void MeshRefusesWhatItCannotBuild()
    {
        // No depth: a flat mesh would turn edge-on and look like a failure of the renderer.
        var flat = Assert.Throws<InvalidOperationException>(() => FaceDetection.Parse(Json(depth: false)).MeshWith(OneTriangle, null));
        Assert.Contains("no depth", flat.Message);

        // A texture of another shape would put every feature in the wrong place.
        var det = FaceDetection.Parse(Json(depth: true));
        var wrong = Assert.Throws<ArgumentException>(() => det.MeshWith(OneTriangle, new SKBitmap(300, 300)));
        Assert.Contains("400x300", wrong.Message);

        // A scaled copy of the right image is fine.
        Assert.True(det.MeshWith(OneTriangle, new SKBitmap(800, 600)).Textured);

        // Nothing found: say so rather than build from nothing.
        var none = FaceDetection.Parse("{\"found\":false,\"width\":10,\"height\":10,\"reason\":\"too small\"}");
        Assert.Contains("too small", Assert.Throws<InvalidOperationException>(() => none.Mesh()).Message);
    }

    /// <summary>The canonical triangles come out of the installed model bundle, 898 of them over 468 vertices.</summary>
    [Fact]
    public void TheTopologyIsReadFromTheModelBundle()
    {
        if (FaceDetector.Model is null)
        {
            output.WriteLine($"NOT RUN: no face model here ({FaceDetector.Missing}).");
            return;
        }

        var tris = FaceDetector.Triangles();
        Assert.Equal(898 * 3, tris.Length);
        Assert.Equal(467, tris.Max());
        output.WriteLine($"{tris.Length / 3} triangles from {FaceDetector.Model}");
    }
    #endregion
}
