namespace Polson.Tests.Drawing;

using System;

using Polson;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

using Xunit;

/// <summary>
/// How a raster picture gets into a vector deliverable.
/// </summary>
/// <remarks>
/// <para>
/// Measured, because the intuition is wrong in a way nothing in a run would reveal. An
/// <c>&lt;image&gt;</c> resolves an external href only when the SVG is treated as a <b>document</b>
/// — opened directly, or embedded through <c>&lt;object&gt;</c> / <c>&lt;iframe&gt;</c>. Loaded
/// through <c>&lt;img&gt;</c> or a CSS background it is an <b>image</b>, and an image fetches no
/// external resources: the picture is absent even with the sidecar file beside it serving 200. Our
/// own renderer fetches nothing either and draws a broken-image cross while the execution still
/// reports success.
/// </para>
/// <para>
/// So the natural spelling — a project-relative path, which is what <c>outFile</c> and
/// <c>Skia.Image.load</c> both establish — is the one that fails, and fails quietly. Passing the
/// object inlines it, and these tests pin that.
/// </para>
/// </remarks>
public class SvgImageEmbeddingTests
{
    #region Inlining
    /// <summary>A bitmap passed as the source is inlined rather than stringified.</summary>
    /// <remarks>
    /// Before the overload existed this bound to the <c>string</c> parameter and produced
    /// <c>href="Polson.Drawing.Skia.SkiaBitmapWrapper"</c> — an href, technically, and the render
    /// showed a broken-image cross.
    /// </remarks>
    [Fact]
    public void TestABitmapIsInlinedAsADataUri()
    {
        using var bitmap = Bitmap(8, 8);
        var paper = new SnapPaper(60, 60);

        var image = paper.Image(bitmap, 0, 0, 60, 60);

        Assert.StartsWith("data:image/", image.Href);
        Assert.Contains(";base64,", image.Href);
        Assert.DoesNotContain("SkiaBitmapWrapper", image.Href);
    }

    /// <summary>A canvas too, so a raster pass can be dropped into a vector page without a round trip through disk.</summary>
    [Fact]
    public void TestACanvasIsInlinedAsADataUri()
    {
        using var canvas = new SkiaCanvas(8, 8);
        var paper = new SnapPaper(60, 60);

        Assert.StartsWith("data:image/", paper.Image(canvas, 0, 0, 60, 60).Href);
    }

    /// <summary>
    /// Anything implementing the seam works, which is the point of putting it in
    /// <c>Polson.Runtime</c> — a reference photograph and a requisitioned material reach the vector
    /// layer without it depending on either.
    /// </summary>
    [Fact]
    public void TestAnyDataUriSourceIsAccepted()
    {
        var paper = new SnapPaper(60, 60);

        Assert.Equal("data:image/png;base64,AAAA", paper.Image(new FakeSource(), 0, 0, 60, 60).Href);
    }

    /// <summary>A string is still an href, because a document-mode SVG may legitimately want one.</summary>
    [Fact]
    public void TestAStringIsStillUsedAsAnHref()
    {
        var paper = new SnapPaper(60, 60);

        Assert.Equal("portrait.png", paper.Image("portrait.png", 0, 0, 60, 60).Href);
    }

    /// <summary>
    /// An unusable source is refused by name rather than stringified into a plausible-looking href.
    /// </summary>
    [Fact]
    public void TestAnUnusableSourceIsRefusedByName()
    {
        var paper = new SnapPaper(60, 60);

        var ex = Assert.Throws<ArgumentException>(() => paper.Image(42, 0, 0, 60, 60));
        Assert.Contains("Int32", ex.Message);
        Assert.Contains("photograph", ex.Message);

        Assert.Throws<ArgumentNullException>(() => paper.Image(null!, 0, 0, 60, 60));
    }
    #endregion

    #region Round trip
    /// <summary>
    /// The inlined URI survives serialization and re-parsing, which is what a multi-stage run does to
    /// its own artifact. Losing the picture at stage two while stage one looked perfect is the
    /// failure this pins.
    /// </summary>
    [Fact]
    public void TestAnInlinedImageSurvivesSerializationAndReparsing()
    {
        using var bitmap = Bitmap(8, 8);
        var paper = new SnapPaper(60, 60);
        paper.Image(bitmap, 0, 0, 60, 60);

        var xml = paper.ToString();
        Assert.Contains("<image", xml);
        Assert.Contains("data:image/", xml);

        var reparsed = Snap.Parse(xml);
        var images = reparsed.SelectAll("image");

        Assert.Single(images);
        Assert.StartsWith("data:image/", ((SnapImage)images[0]).Href);
    }
    #endregion

    #region Fixtures
    private static SkiaBitmapWrapper Bitmap(int width, int height)
    {
        var canvas = new SkiaCanvas(width, height);
        canvas.Clear("#4080c0");
        return canvas.ToBitmap();
    }

    private sealed class FakeSource : IDataUriSource
    {
        public string ToDataUri() => "data:image/png;base64,AAAA";
    }
    #endregion
}
