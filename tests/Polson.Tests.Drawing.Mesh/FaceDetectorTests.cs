namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary>The face-landmark backend, and the parts of it that hold without one.</summary>
/// <remarks>
/// <b>Most of this runs on a machine with no venv</b>, which is the point: the parsing, the
/// refusals and the availability reporting are ours and must be testable without a 352 MB install.
/// Only the live detection is gated, and it says so rather than passing silently.
/// </remarks>
public class FaceDetectorTests : Polson.Tests.TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    #endregion

    #region Constructors
    public FaceDetectorTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (private)
    /// <summary>Whether to go on, saying plainly when it does not.</summary>
    bool Ready()
    {
        if (FaceDetector.Available) return true;
        output.WriteLine($"NOT RUN: face detection needs {FaceDetector.Missing}.");
        return false;
    }

    /// <summary>A minimal mesh with a face's extent, for the topology checks.</summary>
    static FaceMesh Mesh(int cols = 7, int rows = 9)
    {
        List<string> lines = [];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                lines.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"v {-6f + (12f * c / (cols - 1))} {8f - (17f * r / (rows - 1))} 3"));

        for (var r = 0; r < rows - 1; r++)
            for (var c = 0; c < cols - 1; c++)
            {
                int a = (r * cols) + c + 1, b = a + 1, d = a + cols;
                lines.Add($"f {a} {d} {b}");
            }

        return new MeshToolkit().FromObj(string.Join("\n", lines));
    }
    #endregion

    #region Methods
    /// <summary>A found face parses into pixels, pose and blendshapes.</summary>
    [Fact]
    public void TestAFoundFaceParses()
    {
        var d = FaceDetection.Parse(
            """
            {"found":true,"width":100,"height":200,"pad":16,
             "landmarks":[[10,20],[30,40],[50,60]],
             "yawDeg":12.5,"pitchDeg":-3.25,"rollDeg":0.5,
             "blendshapes":{"browDownLeft":0.25,"jawOpen":0.75}}
            """);

        Assert.True(d.Found);
        Assert.Equal(3, d.Count);
        Assert.Equal(16, d.Pad);
        Assert.Equal(12.5f, d.YawDeg, 3);
        Assert.Equal(-3.25f, d.PitchDeg, 3);
        Assert.Equal(0.75f, d.Blendshapes["jawOpen"], 3);

        var p = d.At(1);
        Assert.NotNull(p);
        Assert.Equal(30f, Convert.ToSingle(p!["x"]), 3);
        Assert.Equal(40f, Convert.ToSingle(p["y"]), 3);
        Assert.Equal(1, Convert.ToInt32(p["index"]));
    }

    /// <summary>
    /// **Not finding a face is a RESULT, not an exception**, and it carries the reason.
    /// </summary>
    /// <remarks>
    /// The distinction matters because the commonest cause is recoverable by the caller: a face
    /// below the detector's scale window needs cropping, which the backend cannot do without first
    /// locating it. A throw would turn a fixable situation into a dead script.
    /// </remarks>
    [Fact]
    public void TestNoFaceIsAResultRatherThanAnError()
    {
        var d = FaceDetection.Parse(
            """{"found":false,"width":64,"height":64,"reason":"no face found at any padding"}""");

        Assert.False(d.Found);
        Assert.Equal(0, d.Count);
        Assert.Contains("padding", d.Reason!, StringComparison.Ordinal);
        Assert.Null(d.At(0));
        Assert.Contains("no face", d.ToString(), StringComparison.Ordinal);
    }

    /// <summary>An index outside the landmark list answers null rather than throwing.</summary>
    /// <remarks>
    /// A question, not an error: a caller pairing a mesh against a detection asks this about indices
    /// the mesh chose, and the two can legitimately differ in count.
    /// </remarks>
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(999)]
    public void TestAnIndexOutsideTheListAnswersNull(int index) =>
        Assert.Null(FaceDetection.Parse(
            """{"found":true,"width":10,"height":10,"landmarks":[[1,2],[3,4],[5,6]]}""").At(index));

    /// <summary>The bounds are the landmarks' own extent, in the image's pixels.</summary>
    [Fact]
    public void TestBoundsAreTheLandmarkExtent()
    {
        var b = FaceDetection.Parse(
            """{"found":true,"width":100,"height":100,"landmarks":[[10,20],[50,20],[30,80]]}""")
            .Bounds;

        Assert.Equal(10f, Convert.ToSingle(b["x"]), 3);
        Assert.Equal(20f, Convert.ToSingle(b["y"]), 3);
        Assert.Equal(40f, Convert.ToSingle(b["width"]), 3);
        Assert.Equal(60f, Convert.ToSingle(b["height"]), 3);
        Assert.Equal(30f, Convert.ToSingle(b["cx"]), 3);
    }

    /// <summary>Fitting from a detection that found nothing is refused, with the reason.</summary>
    [Fact]
    public void TestFittingFromAMissedDetectionIsRefused()
    {
        var missed = FaceDetection.Parse(
            """{"found":false,"width":8,"height":8,"reason":"no face found at any padding"}""");

        var e = Assert.Throws<ArgumentException>(() =>
            Mesh().FitDetected(new SkiaBitmapWrapper(8, 8), missed));
        Assert.Contains("no face", e.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **A detection from a different topology is refused by name, not fitted to nonsense.**
    /// </summary>
    /// <remarks>
    /// The real case this guards: a canonical face model has 468 vertices and this bundle's
    /// detector returns 478, the last ten being the iris refinement. Anything pairing the two by
    /// index has to reckon with that, and silently indexing past the end would produce a fit built
    /// on whichever points happened to exist.
    /// </remarks>
    [Fact]
    public void TestATopologyMismatchIsRefusedByName()
    {
        var tiny = FaceDetection.Parse(
            """{"found":true,"width":8,"height":8,"landmarks":[[1,1],[2,2]]}""");

        var e = Assert.Throws<ArgumentException>(() =>
            Mesh().FitDetected(new SkiaBitmapWrapper(8, 8), tiny));
        Assert.Contains("topology", e.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("468", e.Message, StringComparison.Ordinal);
    }

    /// <summary>Availability and the missing-parts report never disagree.</summary>
    /// <remarks>
    /// Worth asserting because they are read in different places — a script asks <c>available</c>
    /// and a refusal message quotes <c>missing</c> — and a pair that could drift would produce the
    /// worst message of all: "unavailable because nothing is missing".
    /// </remarks>
    [Fact]
    public void TestAvailabilityAgreesWithWhatIsMissing() =>
        Assert.Equal(FaceDetector.Available, FaceDetector.Missing is null);

    /// <summary>The JS surface reports the same thing the backend does.</summary>
    [Fact]
    public void TestTheApiReportsTheBackendsOwnState()
    {
        var api = new FaceApi();
        Assert.Equal(FaceDetector.Available, api.Available);
        Assert.Equal(FaceDetector.Missing, api.Missing);
    }

    /// <summary>Detect refuses anything that is not a bitmap or a canvas, by type name.</summary>
    [Fact]
    public void TestDetectRefusesSomethingThatIsNotAnImage()
    {
        if (!Ready()) return;

        var e = Assert.Throws<ArgumentException>(() => new FaceApi().Detect("a filename"));
        Assert.Contains("String", e.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Live: a face the studio draws is found, and the landmarks land on it.**
    /// </summary>
    /// <remarks>
    /// Measured rather than asserted loosely: the detection must return the bundle's full count and
    /// its extent must sit inside the image and cover a real share of it. A detector that returned
    /// points clustered in a corner would satisfy "found" and nothing else.
    /// </remarks>
    [Fact]
    public void TestAFaceTheStudioDrawsIsFound()
    {
        if (!Ready()) return;

        const int S = 512;
        var canvas = new SkiaCanvas(S, S);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#6e6a63";
        ctx.FillRect(0, 0, S, S);

        var t = new ConstructiveDrawingToolkit();
        var head = t.CreateLoomisHead(S / 2f, 215f, 330f, 0f, 0f);
        var geo = t.CreateHeadGeometry(head, null);
        ctx.FillStyle = "#edd6bd";
        ctx.Fill((CanvasPath)geo["silhouette"]!, null);

        ctx.Save();
        ctx.Clip((CanvasPath)geo["mass"]!, null);
        var ink = new Dictionary<string, object?> { ["inkColor"] = "#241c14" };
        t.DrawComicBrow(ctx, head["farBrow"]!, true, ink);
        t.DrawComicBrow(ctx, head["nearBrow"]!, false, ink);
        t.DrawComicEye(ctx, head["farEye"]!, true, ink);
        t.DrawComicEye(ctx, head["nearEye"]!, false, ink);
        t.DrawComicNose(ctx, head["noseWedge"]!, ink);
        t.DrawComicMouth(ctx, head["mouthGuides"]!, ink);
        ctx.Restore();

        var found = new FaceApi().Detect(canvas);
        output.WriteLine(found.ToString());

        Assert.True(found.Found, "a frontal drawn face should be found");
        Assert.Equal(478, found.Count);

        var b = found.Bounds;
        float x = Convert.ToSingle(b["x"]), y = Convert.ToSingle(b["y"]);
        float w = Convert.ToSingle(b["width"]), h = Convert.ToSingle(b["height"]);

        Assert.True(x >= 0 && y >= 0 && x + w <= S && y + h <= S,
            $"landmarks left the image: {x},{y} {w}x{h}");
        Assert.True(w > S * 0.2f && h > S * 0.2f,
            $"landmarks covered too little of the frame to be a face: {w}x{h}");

        // The face is drawn on the vertical axis, so its landmarks should be centred on it.
        Assert.Equal(S / 2f, Convert.ToSingle(b["cx"]), 40f);
    }

    /// <summary>
    /// **Live: a mesh textures itself from a detection, with no coordinate read by hand.**
    /// </summary>
    [Fact]
    public void TestAMeshTexturesItselfFromADetection()
    {
        if (!Ready()) return;

        const int S = 512;
        var canvas = new SkiaCanvas(S, S);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#efd9c0";
        ctx.FillRect(0, 0, S, S);

        var t = new ConstructiveDrawingToolkit();
        var head = t.CreateLoomisHead(S / 2f, 215f, 330f, 0f, 0f);

        // **The silhouette is not decoration here.** A first version drew the features onto a flat
        // ground with no head under them and was not detected at all — correctly, since a detector
        // looks for a face-shaped mass and there was none. Features floating in space are not a face.
        var geo = t.CreateHeadGeometry(head, null);
        ctx.FillStyle = "#edd6bd";
        ctx.Fill((CanvasPath)geo["silhouette"]!, null);

        var ink = new Dictionary<string, object?> { ["inkColor"] = "#241c14" };
        t.DrawComicBrow(ctx, head["farBrow"]!, true, ink);
        t.DrawComicBrow(ctx, head["nearBrow"]!, false, ink);
        t.DrawComicEye(ctx, head["farEye"]!, true, ink);
        t.DrawComicEye(ctx, head["nearEye"]!, false, ink);
        t.DrawComicNose(ctx, head["noseWedge"]!, ink);
        t.DrawComicMouth(ctx, head["mouthGuides"]!, ink);

        var plate = canvas.ToBitmap();
        var found = new FaceApi().Detect(plate);
        if (!found.Found)
        {
            output.WriteLine("NOT RUN: the drawn face was not found, which its own test covers.");
            return;
        }

        var fitted = Mesh(13, 25).FitDetected(plate, found);
        Assert.True(fitted.Textured);
        Assert.Equal("pixels", fitted.UvSpace);

        // The three anchors must have landed on the drawing rather than off it, which is the
        // failure a bad fit actually produces.
        for (var i = 0; i < fitted.VertexCount; i++)
        {
            var uv = fitted.UvAt(i);
            Assert.InRange(Convert.ToSingle(uv["x"]), -S, 2 * S);
            Assert.InRange(Convert.ToSingle(uv["y"]), -S, 2 * S);
        }
    }
    #endregion
}
