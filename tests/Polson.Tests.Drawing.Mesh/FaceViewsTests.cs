namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Polson.Drawing.Skia;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>Face.fromViews</c>: depth from a turnaround's side views, registered to the front.
/// </summary>
/// <remarks>
/// <b>Built from written detections of a surface whose true depth is known</b>, so none of these
/// needs the face backend and every assertion is against a number the test chose. The front view's
/// depth is deliberately wrong — half the truth, as a network prior might be — and the side views
/// carry the truth; the build must recover its SHAPE.
/// <para>
/// <b>Shape, not absolute depth, because a side view cannot say where zero is.</b> Its horizontal
/// position depends on how it was cropped, so the offset is pinned by matching the side view's
/// median to the front's — and when the front's depth is wrong, the whole face shifts forward or back
/// by a constant. That moves only the point it turns about. The first draft of these tests asserted
/// absolute depth and failed by exactly that constant.
/// </para>
/// </remarks>
public class FaceViewsTests : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    #endregion

    #region Constructors
    public FaceViewsTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (private)
    const int Cols = 18, Rows = 26;

    // 468 landmarks on an 18 x 26 grid, x 100..270, y 100..300.
    static float X(int i) => 100f + (i % Cols * 10f);
    static float Y(int i) => 100f + (i / Cols * 8f);

    /// <summary>The true surface: bulging toward the viewer at the midline.</summary>
    static float Depth(int i) => 60f - (0.004f * (X(i) - 185f) * (X(i) - 185f));

    static FaceDetection Detection(float yaw, Func<int, (float X, float Y, float? Z)> at)
    {
        var sb = new StringBuilder("{\"found\":true,\"width\":700,\"height\":500,\"pad\":0,\"yawDeg\":");
        sb.Append(yaw.ToString(CultureInfo.InvariantCulture)).Append(",\"landmarks\":[");
        for (var i = 0; i < 468; i++)
        {
            if (i > 0) sb.Append(',');
            var (x, y, z) = at(i);
            sb.Append(CultureInfo.InvariantCulture, $"[{x},{y}");
            if (z is { } d) sb.Append(CultureInfo.InvariantCulture, $",{d}");
            sb.Append(']');
        }
        return FaceDetection.Parse(sb.Append("]}").ToString());
    }

    static FaceViews.View Front() => new("front", Detection(0f, i => (X(i), Y(i), Depth(i) * 0.5f)), new SKBitmap(400, 400));

    // A side view that sees depth as x: +1 is nose-right (yaw > 0), −1 nose-left. Scale and offset optional.
    static FaceViews.View Side(string name, int sign, float scale = 1f, float dy = 0f, Func<int, float>? noise = null) =>
        new(name, Detection(sign * 60f, i => (scale * (350f + (sign * Depth(i))), (scale * Y(i)) + dy + (noise?.Invoke(i) ?? 0f), null)),
            new SKBitmap(700, 500));

    static ushort[] Grid()
    {
        List<ushort> t = [];
        for (var r = 0; r < Rows - 1; r++)
            for (var c = 0; c < Cols - 1; c++)
            {
                var a = (ushort)((r * Cols) + c);
                // Wound as MediaPipe's canonical mesh is once y points up: counter-clockwise seen from the front.
                t.AddRange([a, (ushort)(a + Cols), (ushort)(a + 1), (ushort)(a + 1), (ushort)(a + Cols), (ushort)(a + Cols + 1)]);
            }
        return [.. t];
    }

    static SKPoint[] Uvs() => [.. Enumerable.Range(0, 468).Select(i => new SKPoint((i % Cols) / (Cols - 1f), 1f - ((i / Cols) / (Rows - 1f))))];

    static FaceMesh Build(params FaceViews.View[] sides) => FaceViews.Build(Front(), sides, Grid(), Uvs(), 256);

    /// <summary>The worst error in depth SHAPE over interior vertices: each depth measured from the mean.</summary>
    static float WorstInterior(FaceMesh mesh, int c0 = 2, int c1 = Cols - 2)
    {
        List<int> interior = [];
        for (var r = 2; r < Rows - 2; r++)
            for (var c = c0; c < c1; c++) interior.Add((r * Cols) + c);

        float Z(int i) => Convert.ToSingle(mesh.Vertex(i)["z"]);
        var mz = interior.Average(Z);
        var mt = interior.Average(Depth);
        return interior.Max(i => MathF.Abs((Z(i) - mz) - (Depth(i) - mt)));
    }
    #endregion

    #region Methods
    /// <summary>With both side views the true depth is recovered, though the front's was half of it.</summary>
    [Fact]
    public void BothSideViewsRecoverTheTrueDepth()
    {
        var mesh = Build(Side("left", +1), Side("right", -1));
        var err = WorstInterior(mesh);
        output.WriteLine($"worst interior depth error {err:F2} px; source: {mesh.Source}");
        Assert.True(err < 1.5f, $"depth off by {err:F2} px");
        Assert.True(mesh.Textured);
        Assert.DoesNotContain("front 0,", mesh.Source);          // front-facing triangles are painted from the front
        Assert.Equal("atlas", mesh.UvSpace);
    }

    /// <summary>A side view at another scale and offset is registered by its rows, not refused.</summary>
    /// <remarks>
    /// A turnaround is drawn to one height scale, but an agent's crops need not be cut to one size:
    /// the scale is fitted from where the side view's features sit, row by row, against the front's.
    /// </remarks>
    [Fact]
    public void ASideViewAtAnotherScaleIsRegistered()
    {
        var mesh = Build(Side("left", +1), Side("right", -1, scale: 0.6f, dy: 15f));
        var err = WorstInterior(mesh);
        output.WriteLine($"worst interior depth error {err:F2} px; source: {mesh.Source}");
        Assert.True(err < 1.5f, $"depth off by {err:F2} px");
        Assert.Contains("right 0.60", mesh.Source);
    }

    /// <summary>The names are labels: which half each side sees is read from its turn.</summary>
    [Fact]
    public void SideViewsAreClassifiedByTheirTurnNotTheirName()
    {
        var swapped = Build(Side("right", +1), Side("left", -1));
        Assert.True(WorstInterior(swapped) < 1.5f);
    }

    /// <summary>One side view corrects the half it sees; the other half keeps the front's depth.</summary>
    [Fact]
    public void OneSideViewCorrectsTheHalfItSees()
    {
        var mesh = Build(Side("left", +1));

        // The left view sees the image-left half, columns 0..8: its shape is recovered well inside it.
        var seen = WorstInterior(mesh, 2, 7);
        output.WriteLine($"seen half shape error {seen:F2} px; source: {mesh.Source}");
        Assert.True(seen < 1.5f, $"the seen half's shape is off by {seen:F2} px");

        // The unseen half is not corrected: its depth span is still the front's half-size one.
        float Z(int i) => Convert.ToSingle(mesh.Vertex(i)["z"]);
        int a = (12 * Cols) + 11, b = (12 * Cols) + 15;
        Assert.Equal(0.5f * (Depth(a) - Depth(b)), Z(a) - Z(b), 0);
    }

    /// <summary>The refusals, each because the alternative is a face that is wrong without saying so.</summary>
    [Fact]
    public void ViewsThatCannotBeRegisteredAreRefused()
    {
        var notSide = Assert.Throws<ArgumentException>(() => FaceViews.Build(Front(),
            [new("left", Detection(5f, i => (X(i), Y(i), null)), new SKBitmap(700, 500))], Grid(), Uvs(), 256));
        Assert.Contains("not a side view", notSide.Message);

        var sameWay = Assert.Throws<ArgumentException>(() => Build(Side("left", +1), Side("right", +1)));
        Assert.Contains("same way", sameWay.Message);

        var misfit = Assert.Throws<ArgumentException>(() => Build(Side("left", +1, noise: i => ((i * 37) % 120) - 60f)));
        Assert.Contains("do not line up", misfit.Message);

        var noDepth = Assert.Throws<ArgumentException>(() => FaceViews.Build(
            new("front", Detection(0f, i => (X(i), Y(i), null)), new SKBitmap(400, 400)), [], Grid(), Uvs(), 256));
        Assert.Contains("no depth", noDepth.Message);

        output.WriteLine(misfit.Message);
    }
    #endregion
}
