namespace Polson.Tests.Drawing;

using Polson.Drawing.Svg;
using Xunit;

/// <summary>
/// Standard SVG transform syntax was silently discarded to identity: IsShorthand tested only the
/// first letter, and every SVG transform function (matrix/translate/scale/rotate/skew) starts with
/// a letter the Snap shorthand also uses as a command, so all of them were routed to the shorthand
/// parser, which recognised no command and returned identity. Found by the logo harness run, where
/// it silently flattened a construction plate to identity with no error.
/// </summary>
public class SnapTransformTests : TestsRuntime
{
    #region Detection Tests
    [Theory]
    [InlineData("matrix(2,0,0,2,30,40)")]
    [InlineData("translate(30,40)")]
    [InlineData("scale(2)")]
    [InlineData("rotate(45)")]
    [InlineData("skewX(10)")]
    [InlineData("skewY(10)")]
    [InlineData("  translate(1,2)  ")]
    public void TestSvgSyntaxIsNotMistakenForShorthand(string transform) =>
        Assert.False(SnapTransformParser.IsShorthand(transform), $"{transform} was routed to the shorthand parser");

    [Theory]
    [InlineData("t30,40")]
    [InlineData("s2")]
    [InlineData("r45")]
    [InlineData("t30,40s2")]
    [InlineData("m2,0,0,2,30,40")]
    [InlineData("s2,2,0,0t30,40")]
    public void TestShorthandIsStillDetected(string transform) =>
        Assert.True(SnapTransformParser.IsShorthand(transform), $"{transform} is Snap shorthand");
    #endregion

    #region Parsing Tests
    [Fact]
    public void TestSvgMatrixIsApplied()
    {
        var m = SnapTransformParser.ParseToMatrix("matrix(2,0,0,2,30,40)");

        Assert.Equal(2f, m.A, 3);
        Assert.Equal(2f, m.D, 3);
        Assert.Equal(30f, m.E, 3);
        Assert.Equal(40f, m.F, 3);
        Assert.False(m.IsIdentity);
    }

    [Fact]
    public void TestSvgTranslateAndScaleAreApplied()
    {
        var t = SnapTransformParser.ParseToMatrix("translate(30,40)");
        Assert.Equal(30f, t.E, 3);
        Assert.Equal(40f, t.F, 3);

        var s = SnapTransformParser.ParseToMatrix("scale(3)");
        Assert.Equal(3f, s.A, 3);
        Assert.Equal(3f, s.D, 3);
    }

    /// <summary>A SnapMatrix round-trips through its own toTransformString, which emits matrix(...).</summary>
    [Fact]
    public void TestSnapMatrixRoundTripsThroughItsOwnString()
    {
        var built = new SnapMatrix().Translate(30, 40).Scale(2, 2);
        var reparsed = SnapTransformParser.ParseToMatrix(built.ToTransformString());

        Assert.Equal(built.A, reparsed.A, 3);
        Assert.Equal(built.D, reparsed.D, 3);
        Assert.Equal(built.E, reparsed.E, 3);
        Assert.Equal(built.F, reparsed.F, 3);
    }

    [Fact]
    public void TestShorthandStillParses()
    {
        var m = SnapTransformParser.ParseToMatrix("t30,40s2");

        Assert.Equal(2f, m.A, 3);
        Assert.False(m.IsIdentity);
    }
    #endregion

    #region Element Tests
    [Theory]
    [InlineData("matrix(2,0,0,2,30,40)")]
    [InlineData("translate(30,40)")]
    public void TestElementTransformAttributeSurvivesSerialization(string transform)
    {
        var paper = Snap.Create(200, 200);
        var el = paper.Circle(10, 10, 5);
        el.Attr("transform", transform);

        Assert.DoesNotContain("matrix(1, 0, 0, 1, 0, 0)", paper.ToString());
    }

    [Fact]
    public void TestElementTransformMethodAcceptsSvgSyntax()
    {
        var paper = Snap.Create(200, 200);
        var el = paper.Circle(10, 10, 5);
        el.Transform("matrix(2,0,0,2,30,40)");

        Assert.DoesNotContain("matrix(1, 0, 0, 1, 0, 0)", paper.ToString());
    }

    /// <summary>getBBox reads the node's own transform string back, so it shared the same defect.</summary>
    [Fact]
    public void TestGetBBoxReflectsAnAppliedTransform()
    {
        var paper = Snap.Create(200, 200);
        var group = paper.Group();
        group.Circle(10, 10, 5);
        group.Transform("t30,40s2");

        var untransformed = group.GetBBox(true);
        var transformed = group.GetBBox();

        Assert.NotEqual(untransformed.X, transformed.X, 3);
        Assert.True(transformed.Width > untransformed.Width, "scaling must widen the measured box");
    }

    /// <summary>
    /// <c>getBBox()</c> does not see a <c>&lt;use&gt;</c>, though the renderer draws one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A referenced copy paints — that is separately pinned by the render — but it contributes
    /// <b>nothing</b> to the measured extent, while a <c>clone()</c> at the same distance contributes
    /// fully. So the two ways of repeating a shape measure differently despite drawing identically.
    /// </para>
    /// <para>
    /// This is a trap rather than a curiosity because of what it combines with. Cropping a document to
    /// its own content — <c>paper.attr({ viewBox: … })</c> built from <c>getBBox()</c>, which is how a
    /// mark becomes a file with no dead margin — will <b>silently crop away every reused instance</b>,
    /// leaving a deliverable showing one star of five. Both halves are recommended technique in
    /// <c>polson://manual/27</c>, which is why their interaction is pinned here rather than left to be
    /// rediscovered. Measure the extent yourself when a document contains <c>use</c> elements.
    /// </para>
    /// </remarks>
    [Fact]
    public void TestGetBBoxIgnoresUseButCountsAClone()
    {
        var paper = Snap.Create(400, 100);
        var source = paper.Rect(0, 0, 40, 40);
        source.Id = "s";
        var alone = paper.GetBBox().Width;

        paper.Use(source).Attr("transform", "t200,0");
        Assert.Equal(alone, paper.GetBBox().Width, 3);

        var copy = source.Clone();
        copy.Id = "c";
        copy.AppendTo(paper);
        copy.Attr("transform", "t300,0");
        Assert.True(paper.GetBBox().Width > alone,
            "a clone must widen the measured box — if this fails, use and clone now measure alike "
            + "and manual 27 §2/§6 needs revisiting");
    }
    #endregion
}
