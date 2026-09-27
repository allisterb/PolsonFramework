namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>mesh.withFaceMesh(face)</c>: a face mesh put on a character's head in place of its own face.
/// </summary>
/// <remarks>
/// <b>Built from a flat character and a synthetic face whose landmarks are placed by hand</b>, so none
/// of these needs the face backend and every expected position is computed here, not by the code
/// under test. The character is <c>FaceBakeTests</c>' plane at z = 0 facing +Z; the face is a 468-vertex
/// grid bulging toward the viewer, with the six vertices the alignment reads — 33/133 and 362/263 for
/// the eyes, 13/14 for the mouth — moved into cells where a face would have them.
/// </remarks>
public class FaceTransplantTests : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    #endregion

    #region Constructors
    public FaceTransplantTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (private)
    const int Cols = 18, Rows = 26;

    /// <summary>The character: a 6 × 6 plane at z = 0, facing +Z, on a 24 × 24 grid.</summary>
    static FaceMesh Body()
    {
        const int n = 24;
        List<SKPoint3> v = [];
        List<SKPoint> uv = [];
        for (var r = 0; r <= n; r++)
            for (var c = 0; c <= n; c++)
            {
                v.Add(new SKPoint3(-3f + (6f * c / n), -3f + (6f * r / n), 0f));
                uv.Add(new SKPoint((float)c / n, (float)r / n));
            }

        List<ushort> idx = [];
        for (var r = 0; r < n; r++)
            for (var c = 0; c < n; c++)
            {
                var a = (ushort)((r * (n + 1)) + c);
                idx.AddRange([a, (ushort)(a + 1), (ushort)(a + n + 1), (ushort)(a + 1), (ushort)(a + n + 2), (ushort)(a + n + 1)]);
            }

        var tex = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(tex)) canvas.Clear(new SKColor(0x80, 0x80, 0x80));
        return new FaceMesh([.. v], [.. uv], [.. idx], true, "flat-body") { Texture = tex };
    }

    /// <summary>A grid face: cell (row, col) at x = col − 8.5, y = row − 12.5, bulging toward +Z.</summary>
    static FaceMesh Face()
    {
        // Which vertex index sits in which cell: the identity, with the six landmarks swapped into place.
        var cellOf = Enumerable.Range(0, Cols * Rows).ToArray();
        void Put(int index, int row, int col)
        {
            var cell = (row * Cols) + col;
            var other = Array.IndexOf(cellOf, cell);
            (cellOf[index], cellOf[other]) = (cellOf[other], cellOf[index]);
        }
        Put(33, 17, 13); Put(133, 17, 11);        // one eye, centred at x = 3.5
        Put(362, 17, 4); Put(263, 17, 6);         // the other, at x = -3.5
        Put(13, 6, 8); Put(14, 6, 9);             // the mouth, at x = 0, y = -6.5

        var indexAt = new int[Cols * Rows];
        for (var i = 0; i < cellOf.Length; i++) indexAt[cellOf[i]] = i;

        var v = new SKPoint3[Cols * Rows];
        var uv = new SKPoint[Cols * Rows];
        for (var i = 0; i < v.Length; i++)
        {
            int row = cellOf[i] / Cols, col = cellOf[i] % Cols;
            float x = col - 8.5f, y = row - 12.5f;
            v[i] = new SKPoint3(x, y, 4f - (0.02f * ((x * x) + (y * y))));
            uv[i] = new SKPoint((col + 0.5f) / Cols, (row + 0.5f) / Rows);
        }

        List<ushort> idx = [];
        for (var r = 0; r < Rows - 1; r++)
            for (var c = 0; c < Cols - 1; c++)
            {
                int a = indexAt[(r * Cols) + c], b = indexAt[(r * Cols) + c + 1];
                int d = indexAt[((r + 1) * Cols) + c], e = indexAt[((r + 1) * Cols) + c + 1];
                idx.AddRange([(ushort)a, (ushort)b, (ushort)d, (ushort)b, (ushort)e, (ushort)d]);
            }

        var tex = new SKBitmap(32, 32);
        using (var canvas = new SKCanvas(tex)) canvas.Clear(SKColors.White);
        return new FaceMesh(v, uv, [.. idx], true, "grid-face") { Texture = tex };
    }

    static Dictionary<string, object?> Anchors() => new()
    {
        ["eyeLeft"] = new Dictionary<string, object?> { ["x"] = -1f, ["y"] = 0.8f },
        ["eyeRight"] = new Dictionary<string, object?> { ["x"] = 1f, ["y"] = 0.8f },
        ["mouth"] = new Dictionary<string, object?> { ["x"] = 0f, ["y"] = -1.2f }
    };

    static float Dist(SKPoint3 a, SKPoint3 b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    static SKPoint Mid(SKPoint3 a, SKPoint3 b) => new((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

    static SKPoint3 Centroid(FaceMesh m, int t) => new(
        (m.Vertices[m.Indices[t * 3]].X + m.Vertices[m.Indices[(t * 3) + 1]].X + m.Vertices[m.Indices[(t * 3) + 2]].X) / 3f,
        (m.Vertices[m.Indices[t * 3]].Y + m.Vertices[m.Indices[(t * 3) + 1]].Y + m.Vertices[m.Indices[(t * 3) + 2]].Y) / 3f,
        (m.Vertices[m.Indices[t * 3]].Z + m.Vertices[m.Indices[(t * 3) + 1]].Z + m.Vertices[m.Indices[(t * 3) + 2]].Z) / 3f);
    #endregion

    #region Methods
    /// <summary>Horn's method recovers a rotation and translation it was not told.</summary>
    [Fact]
    public void RigidRecoversAKnownMotion()
    {
        var rng = new Random(7);
        var from = Enumerable.Range(0, 40)
            .Select(_ => new SKPoint3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble())).ToArray();
        var q = Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(0.3f, 1f, 0.2f)), 0.65f);
        var t = new Vector3(0.4f, -1.2f, 2.5f);
        var to = from.Select(p => Vector3.Transform(new Vector3(p.X, p.Y, p.Z), q) + t)
                     .Select(p => new SKPoint3(p.X, p.Y, p.Z)).ToArray();

        var (r, tr) = FaceTransplant.Rigid(from, to);

        for (var i = 0; i < from.Length; i++)
        {
            var got = Vector3.Transform(new Vector3(from[i].X, from[i].Y, from[i].Z), r) + tr;
            Assert.True(Vector3.Distance(got, new Vector3(to[i].X, to[i].Y, to[i].Z)) < 1e-4f, $"point {i} off by {Vector3.Distance(got, new Vector3(to[i].X, to[i].Y, to[i].Z))}");
        }
    }

    /// <summary>The face's eyes and mouth land exactly on the character's.</summary>
    [Fact]
    public void TheFaceLandsOnTheCharactersEyesAndMouth()
    {
        var face = Face();
        var worn = Body().WithFaceMesh(face, Anchors());
        var at = worn.Attachment!.FaceStart;
        SKPoint3 V(int i) => worn.Vertices[at + i];
        output.WriteLine(worn.Source);

        var eyeA = Mid(V(33), V(133));
        var eyeB = Mid(V(362), V(263));
        var mouth = Mid(V(13), V(14));
        var (left, right) = eyeA.X < eyeB.X ? (eyeA, eyeB) : (eyeB, eyeA);

        Assert.Equal(-1f, left.X, 3); Assert.Equal(0.8f, left.Y, 3);
        Assert.Equal(1f, right.X, 3); Assert.Equal(0.8f, right.Y, 3);
        Assert.Equal(0f, mouth.X, 3); Assert.Equal(-1.2f, mouth.Y, 3);
    }

    /// <summary>Nothing of the face sits behind the character's surface, and its rim lies on it.</summary>
    [Fact]
    public void TheFaceIsSeatedOnTheSurfaceAndItsRimMeetsIt()
    {
        var face = Face();
        var worn = Body().WithFaceMesh(face, Anchors());
        var at = worn.Attachment!.FaceStart;

        var behind = Enumerable.Range(0, face.VertexCount).Min(i => worn.Vertices[at + i].Z);
        Assert.True(behind > -1e-4f, $"a face vertex sits {-behind} behind the flat character");

        // The grid's bulge falls to 4 − 0.02·(8.5² + 12.5²) ≈ -0.57 face units at its corners, well
        // behind a seat at the eyes; conformed, the rim is on the plane.
        foreach (var b in face.Boundary())
            Assert.True(MathF.Abs(worn.Vertices[at + b].Z) < 1e-3f, $"rim vertex {b} sits at z {worn.Vertices[at + b].Z}");

        var nose = worn.Vertices[at + face.Landmark(-0.5f, 0.5f, 4f)];
        // Seated by its eyes and mouth, the centre stands (3.99 − 3.285) × k ≈ 0.16 proud of the plane.
        Assert.True(nose.Z > 0.12f, $"the face's relief was flattened: its centre is at z {nose.Z}");
    }

    /// <summary>The character's own face is cut out under the face and kept everywhere else.</summary>
    [Fact]
    public void TheCharactersOwnFaceIsCutOutUnderIt()
    {
        var body = Body();
        var face = Face();
        var worn = body.WithFaceMesh(face, Anchors());
        var start = worn.Attachment!.FaceStart;

        var bodyTris = Enumerable.Range(0, worn.TriangleCount)
            .Where(t => worn.Indices[t * 3] < start && worn.Indices[(t * 3) + 1] < start && worn.Indices[(t * 3) + 2] < start)
            .Select(t => Centroid(worn, t)).ToArray();

        // The face covers roughly x ±2.4, y -2.3..2.3 once aligned; a band well inside it must be empty.
        var inside = bodyTris.Count(c => MathF.Abs(c.X) < 1.8f && c.Y > -1.8f && c.Y < 1.8f);
        Assert.Equal(0, inside);

        // Well outside it, the character is untouched.
        var outsideBefore = Enumerable.Range(0, body.TriangleCount).Select(t => Centroid(body, t))
            .Count(c => MathF.Abs(c.X) > 2.8f || MathF.Abs(c.Y) > 2.6f);
        var outsideAfter = bodyTris.Count(c => MathF.Abs(c.X) > 2.8f || MathF.Abs(c.Y) > 2.6f);
        Assert.Equal(outsideBefore, outsideAfter);

        // And every face triangle is there, after the character's.
        var faceTris = Enumerable.Range(0, worn.TriangleCount).Count(t => worn.Indices[t * 3] >= start);
        Assert.Equal(face.TriangleCount, faceTris);
        Assert.True(worn.Indices.Skip((worn.TriangleCount - face.TriangleCount) * 3).All(i => i >= start));
    }

    /// <summary>An expression moves the face and nothing of the character.</summary>
    [Fact]
    public void AnExpressionMovesTheFaceAndNotTheCharacter()
    {
        var worn = Body().WithFaceMesh(Face(), Anchors());
        var start = worn.Attachment!.FaceStart;
        Dictionary<string, float> none = [], open = new() { ["jawOpen"] = 1f };

        for (var i = 0; i < start; i++)
            Assert.Equal(worn.Vertices[i], worn.Displace(i, none, open));

        var moved = Enumerable.Range(start, worn.VertexCount - start)
            .Count(i => Dist(worn.Displace(i, none, open), worn.Vertices[i]) > 1e-4f);
        Assert.True(moved > 20, $"only {moved} face vertices moved for jawOpen");
    }

    /// <summary>The face follows the head: moved rigidly, it stays exactly where it was on it.</summary>
    [Fact]
    public void TheFaceFollowsTheHeadRigidly()
    {
        var worn = Body().WithFaceMesh(Face(), Anchors());
        var a = worn.Attachment!;
        var q = Quaternion.CreateFromYawPitchRoll(0.6f, -0.2f, 0.1f);
        var t = new Vector3(0.3f, 1.1f, -0.4f);
        SKPoint3 Move(SKPoint3 p) { var m = Vector3.Transform(new Vector3(p.X, p.Y, p.Z), q) + t; return new SKPoint3(m.X, m.Y, m.Z); }

        var follow = a.Follow([.. a.BodyBind.Select(Move)]);
        for (var i = 0; i < a.Face.VertexCount; i += 17)
        {
            var expected = Move(worn.Vertices[a.FaceStart + i]);
            var got = follow.Place(a.Face.Vertices[i], i);
            Assert.True(Dist(expected, got) < 1e-4f, $"face vertex {i} off by {Dist(expected, got)}");
        }
    }

    /// <summary>The face's triangles are drawn after the character's where the two lie together.</summary>
    [Fact]
    public void TheFaceIsSortedInFrontOfTheSurfaceItLiesOn()
    {
        var worn = Body().WithFaceMesh(Face(), Anchors());
        var faceFrom = worn.TriangleCount - worn.Attachment!.Face.TriangleCount;
        var pose = MeshToolkit.Pose.From(null, worn);
        var order = pose.DepthOrder(worn);
        var rank = new int[order.Length];
        for (var i = 0; i < order.Length; i++) rank[order[i]] = i;

        // The rim lies on the plane; every rim triangle of the face must come after every body triangle.
        var lastBody = Enumerable.Range(0, faceFrom).Max(t => rank[t]);
        var rim = Enumerable.Range(faceFrom, worn.TriangleCount - faceFrom)
            .Where(t => MathF.Abs(Centroid(worn, t).Z) < 0.01f).ToArray();
        Assert.NotEmpty(rim);
        Assert.All(rim, t => Assert.True(rank[t] > lastBody, $"face triangle {t} sorts before the character"));
    }

    /// <summary>The refusals, each because the alternative is a picture that is wrong without saying so.</summary>
    [Fact]
    public void TransplantingRefusesWhatItCannotDo()
    {
        var body = Body();
        var worn = body.WithFaceMesh(Face(), Anchors());

        var twice = Assert.Throws<ArgumentException>(() => worn.WithFaceMesh(Face(), Anchors()));
        Assert.Contains("already carries", twice.Message);

        var small = new FaceMesh([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [default, default, default], [0, 1, 2], true, "tiny")
        { Texture = new SKBitmap(4, 4) };
        Assert.Contains("canonical topology", Assert.Throws<ArgumentException>(() => body.WithFaceMesh(small, Anchors())).Message);

        Assert.Contains("not recognised", Assert.Throws<ArgumentException>(
            () => body.WithFaceMesh(Face(), new Dictionary<string, object?> { ["cover"] = 0.9 })).Message);

        Assert.Contains("transplanted face", Assert.Throws<ArgumentException>(() => worn.FaceSheet(Anchors())).Message);
    }
    #endregion
}
