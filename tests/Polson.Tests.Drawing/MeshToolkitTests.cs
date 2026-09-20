namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;

/// <summary>
/// <c>Mesh</c> — a face as a textured, deformable surface.
///
/// **What is tested here is not "does it draw a head".** It is the four properties that make the
/// route trustworthy: that a mesh loads with its texture coordinates paired to the right vertices,
/// that the fit absorbs framing and says so when it cannot, that deformation happens in model space
/// so a turn and a reshape compose in either order, and that nothing mutates the mesh it came from.
/// </summary>
public class MeshToolkitTests : TestsRuntime
{
    /// <summary>
    /// A small face-shaped grid, written as OBJ text so the tests exercise the real parser.
    /// </summary>
    /// <remarks>
    /// <b>The texture coordinates are deliberately written in a DIFFERENT ORDER from the vertices</b>,
    /// because that is what a real export does and what the first attempt at this got wrong.
    /// MediaPipe's canonical model writes <c>f 174/43 …</c>; a loader that indexes the texture array
    /// by the vertex index produces confetti that renders perfectly.
    /// </remarks>
    static string Obj(int cols = 7, int rows = 9)
    {
        var inv = CultureInfo.InvariantCulture;
        List<string> v = [], vt = [], f = [];
        var count = cols * rows;

        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                float x = -6f + (12f * c / (cols - 1)), y = 8f - (17f * r / (rows - 1));
                v.Add($"v {x.ToString(inv)} {y.ToString(inv)} {(7f - (((x * x) + (y * y * 0.35f)) * 0.12f)).ToString(inv)}");
            }

        // The same UVs, written back to front — so a loader that assumes 1:1 ordering fails loudly.
        for (var i = count - 1; i >= 0; i--)
        {
            float u = (i % cols) / (float)(cols - 1), w = 1f - ((i / cols) / (float)(rows - 1));
            vt.Add($"vt {u.ToString(inv)} {w.ToString(inv)}");
        }

        for (var r = 0; r < rows - 1; r++)
            for (var c = 0; c < cols - 1; c++)
            {
                int a = (r * cols) + c + 1, b = a + 1, d = a + cols, e = d + 1;
                int ta = count - a + 1, tb = count - b + 1, td = count - d + 1, te = count - e + 1;
                f.Add($"f {a}/{ta} {d}/{td} {b}/{tb}");
                f.Add($"f {b}/{tb} {d}/{td} {e}/{te}");
            }

        return string.Join("\n", v.Concat(vt).Concat(f));
    }

    static FaceMesh Mesh() => new MeshToolkit().FromObj(Obj());

    static SkiaCanvas Texture(int size = 256)
    {
        var canvas = new SkiaCanvas(size, size);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#efd9c0";
        ctx.FillRect(0, 0, size, size);
        ctx.FillStyle = "#2a2018";
        ctx.FillRect(size * 0.25f, size * 0.35f, size * 0.12f, size * 0.06f);
        ctx.FillRect(size * 0.63f, size * 0.35f, size * 0.12f, size * 0.06f);
        ctx.FillStyle = "#b05048";
        ctx.FillRect(size * 0.38f, size * 0.68f, size * 0.24f, size * 0.07f);
        return canvas;
    }

    static Dictionary<string, object?> P(float x, float y) =>
        new() { ["x"] = x, ["y"] = y };

    /// <summary>A mesh loads with one texture coordinate per vertex, correctly paired.</summary>
    /// <remarks>
    /// The OBJ above writes its UVs in reverse order, so this passes only if the loader honours the
    /// <c>f v/vt</c> pairing rather than assuming the two arrays line up. A vertex at the top-left of
    /// the grid must carry the UV that belongs to the top-left of the texture.
    /// </remarks>
    [Fact]
    public void TestTextureCoordinatesArePairedToTheirOwnVertices()
    {
        var mesh = Mesh();
        Assert.Equal(63, mesh.VertexCount);
        Assert.Equal(96, mesh.TriangleCount);

        // The mesh's own extremes, found by geometry rather than by index.
        var topLeft = mesh.Landmark(-6f, 8f, 0f);
        var bottomRight = mesh.Landmark(6f, -9f, 0f);

        var fitted = mesh.FitTexture(Texture(), new Dictionary<string, object?>
        {
            ["eyeLeft"] = P(80f, 96f), ["eyeRight"] = P(176f, 96f), ["mouth"] = P(128f, 184f)
        });

        // Front projection is monotonic, so the mesh's leftmost vertex must sample left of its
        // rightmost one, and its topmost above its lowest. A scrambled pairing breaks both.
        static (float X, float Y) Uv(FaceMesh m, int i)
        {
            var d = m.UvAt(i);
            return (Convert.ToSingle(d["x"]), Convert.ToSingle(d["y"]));
        }

        var uvTl = Uv(fitted, topLeft);
        var uvBr = Uv(fitted, bottomRight);
        Assert.True(uvTl.X < uvBr.X, $"left vertex sampled at x {uvTl.X:F1}, right one at {uvBr.X:F1}");
        Assert.True(uvTl.Y < uvBr.Y, $"top vertex sampled at y {uvTl.Y:F1}, bottom one at {uvBr.Y:F1}");
    }

    /// <summary>
    /// The fit absorbs framing: the same face at three scales and offsets lands identically.
    /// </summary>
    /// <remarks>
    /// This is the property that makes the route usable with any portrait rather than a prepared
    /// plate. It is asserted on the drawn bounds rather than on the transform, because what matters
    /// is where the head ends up on the page.
    /// </remarks>
    [Theory]
    [InlineData(1.0f, 0f, 0f)]
    [InlineData(0.42f, 60f, 15f)]
    [InlineData(1.80f, -40f, -60f)]
    public void TestTheFitAbsorbsFraming(float scale, float dx, float dy)
    {
        var toolkit = new MeshToolkit();
        var fitted = Mesh().FitTexture(Texture(), new Dictionary<string, object?>
        {
            ["eyeLeft"] = P((80f * scale) + dx, (96f * scale) + dy),
            ["eyeRight"] = P((176f * scale) + dx, (96f * scale) + dy),
            ["mouth"] = P((128f * scale) + dx, (184f * scale) + dy)
        });

        var canvas = new SkiaCanvas(400, 400);
        var result = toolkit.Draw(canvas.GetContext("2d"), fitted, new Dictionary<string, object?>
        {
            ["x"] = 200f, ["y"] = 200f, ["scale"] = 12f
        });

        // The fit changes which pixels are sampled, never where the geometry lands.
        var b = (Dictionary<string, object?>)result["bounds"]!;
        Assert.Equal(144f, Convert.ToSingle(b["width"]), 1);
        Assert.Equal(204f, Convert.ToSingle(b["height"]), 1);
    }

    /// <summary>A fit with a landmark missing is refused, and the message says where to get them.</summary>
    /// <remarks>
    /// There is no face detector in this stack, so the three points are the caller's to supply. A
    /// silent default would put every face on the same guess and render perfectly.
    /// </remarks>
    [Fact]
    public void TestAFitWithoutLandmarksIsRefused()
    {
        var error = Assert.Throws<ArgumentException>(() => Mesh().FitTexture(Texture(),
            new Dictionary<string, object?> { ["eyeLeft"] = P(80f, 96f), ["eyeRight"] = P(176f, 96f) }));

        Assert.Contains("mouth", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no face detector", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An unrecognised option or deformation unit is refused by name.</summary>
    [Fact]
    public void TestUnknownOptionsAreRefusedByName()
    {
        var toolkit = new MeshToolkit();
        var canvas = new SkiaCanvas(200, 200);
        var ctx = canvas.GetContext("2d");
        var mesh = Mesh();

        var opt = Assert.Throws<ArgumentException>(() => toolkit.Draw(ctx, mesh,
            new Dictionary<string, object?> { ["yawDegrees"] = 30f }));
        Assert.Contains("yawDegrees", opt.Message, StringComparison.Ordinal);
        Assert.Contains("yawDeg", opt.Message, StringComparison.Ordinal);

        var unit = Assert.Throws<ArgumentException>(() => toolkit.Draw(ctx, mesh,
            new Dictionary<string, object?>
            {
                ["expression"] = new Dictionary<string, object?> { ["smile"] = 1f }
            }));
        Assert.Contains("smile", unit.Message, StringComparison.Ordinal);
        Assert.Contains("mouthWide", unit.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Deformation happens in model space, so a turn and a reshape compose in either order.
    /// </summary>
    /// <remarks>
    /// <b>This is the property the landmark head does not have</b>, and the reason this route exists
    /// at all. <c>createLoomisHead</c> projects first and its parameter layer displaces in screen
    /// space afterwards, which is why its own documentation warns that past roughly 40 degrees a
    /// widened jaw widens the wrong way. Here the deformation is applied to the model and the pose
    /// after it, so widening at yaw 0 and then turning gives the same silhouette width as widening
    /// at yaw 50 — the shape is a fact about the head rather than about the page.
    /// </remarks>
    [Fact]
    public void TestDeformationHappensInModelSpaceRatherThanOnThePage()
    {
        var toolkit = new MeshToolkit();
        var mesh = Mesh();

        static float Width(MeshToolkit tk, FaceMesh m, float yaw, float widen)
        {
            var canvas = new SkiaCanvas(600, 600);
            var r = tk.Draw(canvas.GetContext("2d"), m, new Dictionary<string, object?>
            {
                ["x"] = 300f, ["y"] = 300f, ["scale"] = 12f, ["yawDeg"] = yaw,
                ["shape"] = new Dictionary<string, object?> { ["width"] = widen }
            });
            return Convert.ToSingle(((Dictionary<string, object?>)r["bounds"]!)["width"]);
        }

        // Frontally, widening the model widens the drawing in proportion: +0.30 per unit of `width`.
        float plain = Width(toolkit, mesh, 0f, 0f), wide = Width(toolkit, mesh, 0f, 1f);
        Assert.Equal(1.30f, wide / plain, 2);

        // Turned, the widening still acts on the model, so it survives the projection rather than
        // being applied along a screen axis that no longer means "across the face".
        float turned = Width(toolkit, mesh, 50f, 0f), turnedWide = Width(toolkit, mesh, 50f, 1f);
        Assert.True(turnedWide > turned * 1.15f,
            $"a widened model at yaw 50 drew {turnedWide:F1} against {turned:F1} — the deformation did not survive the turn");
    }

    /// <summary>Nothing mutates the mesh it came from.</summary>
    /// <remarks>
    /// A mesh <i>is</i> the identity of a character. A route whose whole selling point is that panel
    /// 1 and panel 40 are the same face cannot have a fit quietly change what it was derived from.
    /// </remarks>
    [Fact]
    public void TestEveryChangeReturnsANewMesh()
    {
        var mesh = Mesh();
        Assert.False(mesh.Textured);

        var fitted = mesh.FitTexture(Texture(), new Dictionary<string, object?>
        {
            ["eyeLeft"] = P(80f, 96f), ["eyeRight"] = P(176f, 96f), ["mouth"] = P(128f, 184f)
        });

        Assert.True(fitted.Textured);
        Assert.False(mesh.Textured);                 // the original is untouched
        Assert.NotSame(mesh, fitted);
        Assert.NotSame(mesh, mesh.Clone());
    }

    /// <summary>The boundary is computed from the triangles, not listed.</summary>
    /// <remarks>
    /// An edge belonging to exactly one triangle is a boundary edge. On a 7x9 grid that is the
    /// perimeter — 2*(7+9) - 4 = 28 vertices — which is checkable arithmetic rather than a number
    /// somebody recorded.
    /// </remarks>
    [Fact]
    public void TestTheBoundaryIsTheRingOfTheSurface()
    {
        var ring = Mesh().Boundary();
        Assert.Equal(28, ring.Length);
        Assert.Equal(ring.Length, ring.Distinct().Count());
    }

    /// <summary>A mesh with no texture draws as a wireframe rather than as nothing.</summary>
    [Fact]
    public void TestAnUntexturedMeshDrawsItsWireframe()
    {
        var canvas = new SkiaCanvas(400, 400);
        var result = new MeshToolkit().Draw(canvas.GetContext("2d"), Mesh(),
            new Dictionary<string, object?> { ["x"] = 200f, ["y"] = 200f, ["scale"] = 12f });

        Assert.False(Convert.ToBoolean(result["textured"]));
        Assert.Equal(96, Convert.ToInt32(result["triangles"]));

        // Something reached the pixels: a wireframe that drew nothing would leave the canvas clear.
        var bitmap = canvas.ToBitmap();
        var lit = 0;
        for (var y = 0; y < 400; y += 4)
            for (var x = 0; x < 400; x += 4)
                if (bitmap.GetPixel(x, y) != "#00000000") lit++;

        Assert.True(lit > 50, $"the wireframe put {lit} sampled pixels on a 400px canvas");
    }

    #region Deformation
    /// <summary>The three landmarks a fit needs, placed on a square texture.</summary>
    static Dictionary<string, object?> Fit(int size) => new()
    {
        ["eyeLeft"] = new Dictionary<string, object?> { ["x"] = size * 0.31f, ["y"] = size * 0.38f },
        ["eyeRight"] = new Dictionary<string, object?> { ["x"] = size * 0.69f, ["y"] = size * 0.38f },
        ["mouth"] = new Dictionary<string, object?> { ["x"] = size * 0.50f, ["y"] = size * 0.71f }
    };

    /// <summary>
    /// Where every vertex ends up in model space, so an assertion is about the deformation rather
    /// than about the pixels it happened to produce.
    /// </summary>
    /// <summary>
    /// The vertex nearest a point on the facial plane, ignoring depth.
    /// </summary>
    /// <remarks>
    /// <b>Not <c>Landmark</c>, and the difference bit.</b> That measures in three dimensions, so a
    /// query with a made-up <c>z</c> picks whichever vertex happens to sit nearest in depth — on this
    /// grid a brow query at <c>x = -1.5</c> returned the vertex at <c>x = -4</c>, two columns away,
    /// because the surface curves back. A test about which column moves should ask about columns.
    /// </remarks>
    static int At(FaceMesh m, float x, float y)
    {
        int best = 0;
        var bestD = float.MaxValue;
        for (var i = 0; i < m.VertexCount; i++)
        {
            var v = m.Vertex(i);
            float dx = Convert.ToSingle(v["x"]) - x, dy = Convert.ToSingle(v["y"]) - y;
            var d = (dx * dx) + (dy * dy);
            if (d < bestD) { bestD = d; best = i; }
        }

        return best;
    }

    static SKPoint3[] Placed(FaceMesh m, Dictionary<string, object?> options)
    {
        var pose = MeshToolkit.Pose.From(JsInterop.AsDict(options), m);
        var outp = new SKPoint3[m.VertexCount];
        for (var i = 0; i < outp.Length; i++) outp[i] = m.Displace(i, pose.Shape, pose.Expression, pose.Side);
        return outp;
    }

    static FaceMesh Scaled(float k)
    {
        var inv = CultureInfo.InvariantCulture;
        var lines = Obj().Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("v ", StringComparison.Ordinal)) continue;
            var p = lines[i].Split(' ');
            lines[i] = $"v {(float.Parse(p[1], inv) * k).ToString(inv)} "
                     + $"{(float.Parse(p[2], inv) * k).ToString(inv)} {(float.Parse(p[3], inv) * k).ToString(inv)}";
        }

        return new MeshToolkit().FromObj(string.Join("\n", lines));
    }

    /// <summary>
    /// **The defect this normalisation exists for.** A mesh authored at another scale used to get no
    /// deformation at all, because every band sat at a literal MediaPipe coordinate outside its
    /// geometry — a neutral face returned from a call that asked for anger, with nothing said.
    /// </summary>
    [Theory]
    [InlineData(0.1f)]
    [InlineData(1f)]
    [InlineData(40f)]
    public void TestTheDeformationBandsFollowTheMeshsOwnScale(float k)
    {
        var units = new Dictionary<string, object?>
        {
            ["expression"] = new Dictionary<string, object?> { ["browLower"] = 1f, ["squint"] = 0.6f },
            ["shape"] = new Dictionary<string, object?> { ["jawWidth"] = 0.5f }
        };

        var one = Placed(Mesh(), units);
        var other = Placed(Scaled(k), units);

        // Same mesh, k times bigger: every deformed vertex must land k times further out.
        var worst = 0f;
        for (var i = 0; i < one.Length; i++)
        {
            worst = MathF.Max(worst, MathF.Abs((one[i].X * k) - other[i].X));
            worst = MathF.Max(worst, MathF.Abs((one[i].Y * k) - other[i].Y));
        }

        Assert.True(worst < 0.002f * MathF.Max(1f, k),
                    $"the deformation is not proportional at {k}x: worst vertex off by {worst}");
    }

    /// <summary>
    /// **The fractions reproduce the absolutes they replaced, to the digit.**
    /// </summary>
    /// <remarks>
    /// The normalisation's whole promise is that nothing already drawn with the canonical model
    /// changed — so it is worth pinning rather than asserting in a comment. Built here from that
    /// model's own measured bounds (<c>x ±7.7431</c>, <c>y -9.4034..8.2618</c>,
    /// <c>z -2.4359..7.5866</c>) rather than from the file, because a test must not depend on
    /// material under <c>reference/</c>.
    /// </remarks>
    [Fact]
    public void TestTheFrameReproducesTheAbsolutesItReplaced()
    {
        var inv = CultureInfo.InvariantCulture;
        (float X, float Y, float Z)[] pts =
        [
            (-7.7431f, 8.2618f, -2.4359f), (7.7431f, 8.2618f, 7.5866f),   // the corners that set the frame
            (-7.7431f, -9.4034f, -2.4359f), (7.7431f, -9.4034f, 7.5866f),
            (0f, 3.6f, 3f),                                                // the brow station, as it was
            (0f, 2.4f, 3f),                                                // exactly one band radius above it
            (0f, -3.4f, 3f)                                                // the mouth station, as it was
        ];

        List<string> lines = [];
        foreach (var p in pts)
            lines.Add($"v {p.X.ToString(inv)} {p.Y.ToString(inv)} {p.Z.ToString(inv)}");
        lines.Add("f 1 2 3");
        lines.Add("f 2 3 4");

        var mesh = new MeshToolkit().FromObj(string.Join("\n", lines));
        var rest = Placed(mesh, []);
        var raised = Placed(mesh, new Dictionary<string, object?>
        { ["expression"] = new Dictionary<string, object?> { ["browRaise"] = 1f } });

        // The brow station moved by exactly the 0.7 units the absolute version moved it.
        Assert.Equal(0.7f, raised[4].Y - rest[4].Y, 0.001f);

        // A vertex one band radius away — 1.2 units, as it was — sits exactly on the edge and does not.
        Assert.Equal(0f, raised[5].Y - rest[5].Y, 0.001f);

        // And the mouth band still finds the mouth where it used to be.
        var dropped = Placed(mesh, new Dictionary<string, object?>
        { ["expression"] = new Dictionary<string, object?> { ["mouthOpen"] = 1f } });
        Assert.Equal(1.6f * (-2.6f - -3.4f) / 6f, rest[6].Y - dropped[6].Y, 0.001f);
    }

    /// <summary>The mouth corners come down without the mouth's own centre following them.</summary>
    /// <remarks>
    /// AU15, the lip corner depressor, and the one unit an angry mouth cannot do without. The corner
    /// weight has to fall away past the corner, or the mouth band alone drags the cheeks down with it.
    /// </remarks>
    [Fact]
    public void TestTheMouthCornersCanBeDepressed()
    {
        var mesh = Mesh();
        var before = Placed(mesh, []);
        var after = Placed(mesh, new Dictionary<string, object?>
        { ["expression"] = new Dictionary<string, object?> { ["mouthCornerDown"] = 1f } });

        // The mouth row on this 7x9 grid sits nearest y = -3.4; take its midline and its outermost.
        int mid = At(mesh, 0f, -3.2f), corner = At(mesh, -2f, -3.2f);
        float dropMid = before[mid].Y - after[mid].Y, dropCorner = before[corner].Y - after[corner].Y;

        Assert.True(dropCorner > 0.05f, $"the corner barely moved: {dropCorner}");
        Assert.True(dropCorner > dropMid * 2f,
                    $"the corner must outrun the centre: corner {dropCorner}, centre {dropMid}");
    }

    /// <summary>The brow heads draw together, and the midline does not move.</summary>
    [Fact]
    public void TestTheBrowCanBeKnitted()
    {
        var mesh = Mesh();
        var before = Placed(mesh, []);
        var after = Placed(mesh, new Dictionary<string, object?>
        { ["expression"] = new Dictionary<string, object?> { ["browKnit"] = 1f } });

        int head = At(mesh, -2f, 3.5f), axis = At(mesh, 0f, 3.5f);

        // The head of the brow moves toward the midline — inward, not leftward.
        Assert.True(after[head].X > before[head].X + 0.05f,
                    $"the brow head did not knit: {before[head].X} to {after[head].X}");
        Assert.Equal(before[axis].X, after[axis].X, 0.001f);

        // Its mirror moves the other way by the same amount, so knitting stays symmetric.
        int mirror = At(mesh, 2f, 3.5f);
        Assert.Equal(before[head].X - after[head].X, after[mirror].X - before[mirror].X, 0.001f);
    }

    /// <summary>
    /// **One raised eyebrow, which was unreachable at any weight while every unit moved both halves.**
    /// </summary>
    [Fact]
    public void TestAnExpressionCanActOnOneHalfOfTheFace()
    {
        var mesh = Mesh();
        var before = Placed(mesh, []);
        var near = Placed(mesh, new Dictionary<string, object?>
        {
            ["expression"] = new Dictionary<string, object?> { ["browRaise"] = 1f },
            ["side"] = "near"
        });

        int plus = At(mesh, 2f, 3.5f), minus = At(mesh, -2f, 3.5f);

        Assert.True(near[plus].Y > before[plus].Y + 0.05f, "the near brow did not raise");
        Assert.Equal(before[minus].Y, near[minus].Y, 0.001f);

        // 'far' is the same unit on the other half, and 'both' is what it was before.
        var far = Placed(mesh, new Dictionary<string, object?>
        {
            ["expression"] = new Dictionary<string, object?> { ["browRaise"] = 1f },
            ["side"] = "far"
        });
        Assert.Equal(before[plus].Y, far[plus].Y, 0.001f);
        Assert.True(far[minus].Y > before[minus].Y + 0.05f, "the far brow did not raise");

        var both = Placed(mesh, new Dictionary<string, object?>
        { ["expression"] = new Dictionary<string, object?> { ["browRaise"] = 1f } });
        Assert.Equal(near[plus].Y, both[plus].Y, 0.001f);
        Assert.Equal(far[minus].Y, both[minus].Y, 0.001f);
    }

    /// <summary>A jaw does not drop on one side, so the option is ignored rather than the call refused.</summary>
    [Fact]
    public void TestAJawDropIgnoresTheSide()
    {
        var mesh = Mesh();
        var opts = new Dictionary<string, object?>
        { ["expression"] = new Dictionary<string, object?> { ["mouthOpen"] = 1f } };

        var both = Placed(mesh, opts);
        var oneSided = Placed(mesh, new Dictionary<string, object?>
        { ["expression"] = opts["expression"], ["side"] = "near" });

        for (var i = 0; i < both.Length; i++) Assert.Equal(both[i].Y, oneSided[i].Y, 0.0001f);
    }

    /// <summary>
    /// **`left` and `right` are refused by name**, because near and far are sides of the page.
    /// </summary>
    [Fact]
    public void TestLeftAndRightAreRefusedByName()
    {
        foreach (var side in new[] { "left", "right" })
        {
            var e = Assert.Throws<ArgumentException>(() => Placed(Mesh(), new Dictionary<string, object?>
            {
                ["expression"] = new Dictionary<string, object?> { ["browRaise"] = 1f },
                ["side"] = side
            }));
            Assert.Contains("near", e.Message, StringComparison.Ordinal);
            Assert.Contains("page", e.Message, StringComparison.Ordinal);
        }

        var bad = Assert.Throws<ArgumentException>(() => Placed(Mesh(), new Dictionary<string, object?>
        { ["side"] = "sideways" }));
        Assert.Contains("sideways", bad.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// **An expression still lands on the feature it names after the silhouette has been redrawn.**
    /// </summary>
    /// <remarks>
    /// <c>fitOutline</c> moves vertices, so a band keyed on where a vertex now sits slides off the
    /// feature. Which vertex is a brow vertex is a fact about the anatomy, and the reference geometry
    /// is what remembers it.
    /// </remarks>
    [Fact]
    public void TestDeformationSurvivesAnOutlineFit()
    {
        var fitted = Mesh().FitTexture(Texture(), Fit(256));

        // A silhouette noticeably wider and shorter than the mesh's own, in the texture's pixels.
        var outline = new CanvasPath();
        outline.MoveTo(10f, 40f);
        outline.LineTo(246f, 40f);
        outline.LineTo(246f, 210f);
        outline.LineTo(10f, 210f);
        outline.ClosePath();

        var shaped = fitted.FitOutline(outline, new Dictionary<string, object?> { ["strength"] = 1f });
        Assert.NotEqual(fitted.Vertex(0)["y"], shaped.Vertex(0)["y"]);

        // The brow band still finds the brow: the same vertices move, and they move the same way.
        var raise = new Dictionary<string, object?>
        { ["expression"] = new Dictionary<string, object?> { ["browRaise"] = 1f } };

        var flat = Placed(fitted, raise);
        var flatRest = Placed(fitted, []);
        var bent = Placed(shaped, raise);
        var bentRest = Placed(shaped, []);

        for (var i = 0; i < flat.Length; i++)
        {
            var a = flat[i].Y - flatRest[i].Y;
            var b = bent[i].Y - bentRest[i].Y;
            Assert.Equal(a, b, 0.0001f);
        }
    }
    #endregion

    #region Texture coordinate space
    /// <summary>The distinct colours a textured draw actually put on the canvas.</summary>
    static HashSet<string> Drawn(FaceMesh mesh, object texture, int size = 400)
    {
        var canvas = new SkiaCanvas(size, size);
        new MeshToolkit().Draw(canvas.GetContext("2d"), mesh, new Dictionary<string, object?>
        {
            ["x"] = size / 2f, ["y"] = size / 2f, ["scale"] = size / 26f, ["texture"] = texture
        });

        var bitmap = canvas.ToBitmap();
        HashSet<string> seen = [];
        for (var y = 0; y < size; y += 3)
            for (var x = 0; x < size; x += 3)
            {
                var px = bitmap.GetPixel(x, y);
                if (px != "#00000000") seen.Add(px);
            }

        return seen;
    }

    /// <summary>A plate divided into four quadrants, so a mapping that collapses is visible.</summary>
    static SkiaCanvas Quadrants(int size = 512)
    {
        var canvas = new SkiaCanvas(size, size);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#c9553d"; ctx.FillRect(0, 0, size / 2f, size / 2f);
        ctx.FillStyle = "#1f6f8b"; ctx.FillRect(size / 2f, 0, size / 2f, size / 2f);
        ctx.FillStyle = "#5fb49c"; ctx.FillRect(0, size / 2f, size / 2f, size / 2f);
        ctx.FillStyle = "#e5a93c"; ctx.FillRect(size / 2f, size / 2f, size / 2f, size / 2f);
        return canvas;
    }

    /// <summary>
    /// **A mesh drawn straight from its own file uses its own UV atlas.**
    /// </summary>
    /// <remarks>
    /// Until 2026-09-19 it did not, and the failure was silent: an OBJ's coordinates are an atlas —
    /// <c>0..1</c>, <c>v</c> measured up from the bottom — and the draw path sampled them as pixels,
    /// so the whole head came back in the colour of the texture's top-left pixel. **The atlas was
    /// loaded correctly the entire time**; nothing consumed it. Four coloured quadrants in, one
    /// colour out, no error.
    /// </remarks>
    [Fact]
    public void TestAnUnfittedMeshSamplesItsOwnAtlas()
    {
        var mesh = Mesh();
        Assert.Equal("atlas", mesh.UvSpace);

        var seen = Drawn(mesh, Quadrants());
        Assert.True(seen.Count > 2,
            $"an atlas mapping should reach several quadrants; saw {seen.Count}: {string.Join(", ", seen)}");
    }

    /// <summary>The <c>v</c> axis is flipped, because an OBJ measures it up and an image measures it down.</summary>
    /// <remarks>
    /// Getting this backwards renders a face upside down on its own head and nothing errors, so it is
    /// asserted against a plate whose halves differ only top from bottom.
    /// </remarks>
    [Fact]
    public void TestTheAtlasVAxisIsFlippedAgainstTheImage()
    {
        var plate = new SkiaCanvas(256, 256);
        var pctx = plate.GetContext("2d");
        pctx.FillStyle = "#c9553d"; pctx.FillRect(0, 0, 256, 128);      // top of the image
        pctx.FillStyle = "#1f6f8b"; pctx.FillRect(0, 128, 256, 128);    // bottom of the image

        var mesh = Mesh();

        // The grid's own top row carries v near 1, which is the image's TOP once flipped.
        var top = mesh.UvAt(mesh.Landmark(0f, 8f, 0f));
        Assert.True(Convert.ToSingle(top["y"]) > 0.5f, "the mesh's top row should hold a high atlas v");

        // So the crown of the drawn head must be the colour at the top of the plate.
        var canvas = new SkiaCanvas(400, 400);
        new MeshToolkit().Draw(canvas.GetContext("2d"), mesh, new Dictionary<string, object?>
        {
            ["x"] = 200f, ["y"] = 200f, ["scale"] = 14f, ["texture"] = plate
        });

        var bitmap = canvas.ToBitmap();
        string? crown = null, chin = null;
        for (var y = 0; y < 400 && crown is null; y++)
            if (bitmap.GetPixel(200, y) != "#00000000") crown = bitmap.GetPixel(200, y);
        for (var y = 399; y >= 0 && chin is null; y--)
            if (bitmap.GetPixel(200, y) != "#00000000") chin = bitmap.GetPixel(200, y);

        Assert.Equal("#C9553DFF", crown);
        Assert.Equal("#1F6F8BFF", chin);
    }

    /// <summary>
    /// **A texture with nothing to map it by falls back to a wireframe rather than a flat fill.**
    /// </summary>
    [Fact]
    public void TestATextureWithNoCoordinatesDrawsAWireframe()
    {
        // The same grid with every `vt` and every `f v/vt` pairing stripped out.
        var bare = string.Join("\n", Obj().Split('\n')
            .Where(l => !l.StartsWith("vt ", StringComparison.Ordinal))
            .Select(l => l.StartsWith("f ", StringComparison.Ordinal)
                ? string.Join(" ", l.Split(' ').Select(p => p.Split('/')[0]))
                : l));

        var mesh = new MeshToolkit().FromObj(bare);
        Assert.Equal("none", mesh.UvSpace);

        var canvas = new SkiaCanvas(400, 400);
        var result = new MeshToolkit().Draw(canvas.GetContext("2d"), mesh, new Dictionary<string, object?>
        {
            ["x"] = 200f, ["y"] = 200f, ["scale"] = 14f, ["texture"] = Quadrants(), ["inkColor"] = "#123456"
        });

        Assert.False(Convert.ToBoolean(result["textured"]));

        // What reached the canvas is the ink, not one of the plate's four colours.
        var seen = Drawn(mesh, Quadrants());
        Assert.DoesNotContain("#C9553DFF", seen);
    }

    /// <summary>
    /// **A misread landmark costs about what it was misread by — it does not cascade.**
    /// </summary>
    /// <remarks>
    /// The three points are the caller's to supply and there is no detector in this stack, so for a
    /// photograph they are read by eye. What decides whether that is workable is not how exact the
    /// reading is but whether an error in it <i>amplifies</i>, and the fit is a similarity transform
    /// from three points, so it should not. Measured rather than argued: the ratio of probe drift to
    /// landmark error is **constant** across a 5× range of error, at 1.02× for a vertical eye slip,
    /// 1.65× for a horizontal one and 2.05× for the mouth. There is no cliff to fall off.
    /// </remarks>
    [Fact]
    public void TestAMisreadLandmarkDoesNotAmplify()
    {
        var mesh = Mesh();
        var plate = Texture(256);

        static Dictionary<string, object?> Pts(float rx, float ry, float my) => new()
        {
            ["eyeLeft"] = new Dictionary<string, object?> { ["x"] = 80f, ["y"] = 96f },
            ["eyeRight"] = new Dictionary<string, object?> { ["x"] = rx, ["y"] = ry },
            ["mouth"] = new Dictionary<string, object?> { ["x"] = 128f, ["y"] = my }
        };

        var truth = mesh.FitTexture(plate, Pts(176f, 96f, 184f));

        // The corners of the mesh, which are the furthest a landmark error has to reach.
        int[] probes =
        [
            mesh.Landmark(0f, -9f, 0f), mesh.Landmark(0f, 8f, 0f),
            mesh.Landmark(-6f, 0f, 0f), mesh.Landmark(6f, 0f, 0f)
        ];

        float Drift(FaceMesh other)
        {
            var worst = 0f;
            foreach (var i in probes)
            {
                var a = truth.UvAt(i);
                var b = other.UvAt(i);
                float dx = Convert.ToSingle(a["x"]) - Convert.ToSingle(b["x"]),
                      dy = Convert.ToSingle(a["y"]) - Convert.ToSingle(b["y"]);
                worst = MathF.Max(worst, MathF.Sqrt((dx * dx) + (dy * dy)));
            }

            return worst;
        }

        float? firstRatio = null;
        foreach (var k in new[] { 2f, 5f, 10f })
        {
            var ratios = new[]
            {
                Drift(mesh.FitTexture(plate, Pts(176f + k, 96f, 184f))) / k,
                Drift(mesh.FitTexture(plate, Pts(176f, 96f + k, 184f))) / k,
                Drift(mesh.FitTexture(plate, Pts(176f, 96f, 184f + k))) / k
            };

            foreach (var r in ratios)
                Assert.True(r < 2.6f, $"a landmark error of {k}px moved a probe {r:F2}x as far");

            // The same error, five times larger, must cost five times as much and no more — which is
            // what "no cliff" means and is the property a caller is relying on when reading by eye.
            firstRatio ??= ratios[0];
            Assert.Equal(firstRatio.Value, ratios[0], 0.01f);
        }
    }

    /// <summary>A fitted mesh keeps reporting pixels, so nothing already written changes.</summary>
    [Fact]
    public void TestAFittedMeshStillReportsPixels()
    {
        var fitted = Mesh().FitTexture(Texture(), Fit(256));
        Assert.Equal("pixels", fitted.UvSpace);

        // Pixel-space coordinates run to the texture's own dimensions rather than to 1.
        var uv = fitted.UvAt(fitted.Landmark(6f, -9f, 0f));
        Assert.True(Convert.ToSingle(uv["x"]) > 1.5f, "a fitted UV should be a pixel position");

        Assert.Equal("atlas", Mesh().UvSpace);
    }
    #endregion
}
