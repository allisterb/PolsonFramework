namespace Polson.Tests.MCPServer;

using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// Guards the boundary between the JavaScript sandbox and the .NET toolkits.
/// <para>
/// An object literal written in JS arrives as an <c>ExpandoObject</c>, which implements
/// <c>IDictionary&lt;string, object&gt;</c> but not the non-generic <c>IDictionary</c>. Before
/// <see cref="Polson.Drawing.Skia.JsInterop.AsDict"/> existed, every options argument and every
/// <c>{ x, y }</c> point passed from a script was silently discarded — scripts succeeded and rendered
/// default styling, or nothing at all, with no error. These tests fail loudly if that returns.
/// </para>
/// </summary>
public class JsInteropTests : TestsRuntime
{
    #region Options Tests
    [Fact]
    public void TestOptionsObjectFromScriptIsHonoured()
    {
        var result = Execute("""
            const a = Drawing.createPerspectiveGrid({ horizonY: 100, focalLength: 500, cameraAngleDeg: 20 });
            const b = Drawing.createPerspectiveGrid({ horizonY: 900, focalLength: 1900, cameraAngleDeg: 70 });
            log(a.horizonY + '|' + a.focalLength + '|' + b.horizonY + '|' + b.focalLength);
            """);

        Assert.True(result.Success, result.Error);
        Assert.Contains("100|500|900|1900", string.Join(" ", result.Logs));
    }

    [Fact]
    public void TestOptionsChangeRenderedOutput()
    {
        var sparse = Render("Drawing.drawCompositionGrid(ctx, 'ruleOfThirds', { opacity: 0.15, lineWidth: 0.5 });");
        var dense = Render("Drawing.drawCompositionGrid(ctx, 'dynamicSymmetry', { opacity: 1.0, lineWidth: 4 });");

        Assert.NotEqual(sparse, dense);
    }

    [Fact]
    public void TestNestedOptionsObjectIsHonoured()
    {
        var result = Execute("""
            const figure = Drawing.createMannequinFigure(300, 60, 640, { shoulderTiltDeg: -20, pelvicTiltDeg: 18 });
            log('' + figure.ribcage.tiltDeg + '|' + figure.pelvis.tiltDeg);
            """);

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain("0|0", string.Join(" ", result.Logs));
    }
    #endregion

    #region Point Tests
    [Theory]
    [InlineData(
        "Drawing.drawTaperedStroke(ctx,{x:50,y:200},{x:150,y:100},{x:250,y:300},{x:350,y:200},8,'#000');",
        "Drawing.drawTaperedStroke(ctx,50,200,150,100,250,300,350,200,8,'#000');")]
    [InlineData(
        "Drawing.drawFeathering(ctx,{x:200,y:200},285,14,58,9,'#000',1.2);",
        "Drawing.drawFeathering(ctx,200,200,285,14,58,9,'#000',1.2);")]
    [InlineData(
        "Drawing.drawHairRibbon(ctx,{x:80,y:80},{x:300,y:300},0.4,30,'#c96a2e','#7a3410','#000',2);",
        "Drawing.drawHairRibbon(ctx,80,80,300,300,0.4,30,'#c96a2e','#7a3410','#000',2);")]
    public void TestPointObjectOverloadMatchesNumericOverload(string objectForm, string numericForm)
    {
        var fromObjects = Render(objectForm);
        var fromNumbers = Render(numericForm);

        Assert.NotEqual(BlankCanvasSize, fromObjects);
        Assert.Equal(fromNumbers, fromObjects);
    }

    [Fact]
    public void TestPointObjectActuallyDrawsSomething()
    {
        Assert.NotEqual(BlankCanvasSize, Render("Drawing.drawTaperedStroke(ctx,{x:40,y:40},{x:120,y:360},{x:280,y:40},{x:360,y:360},10,'#000');"));
    }
    #endregion

    #region Model Argument Tests
    /// <summary>
    /// Methods that branch on the *shape* of an argument — a bounds rect versus a vertex list — used a
    /// pattern-match cast rather than an assignment, so they fell through to an empty result for a
    /// script-authored literal instead of erroring.
    /// </summary>
    [Fact]
    public void TestBoundsLiteralProducesAShadowPolygon()
    {
        var result = Execute("""
            const fromBounds = Drawing.projectCastShadow({ x: 250, y: 90 }, 470, { x: 360, y: 300, width: 180, height: 180 }, { opacity: 0.55 });
            const fromVertices = Drawing.projectCastShadow({ x: 250, y: 90 }, 470, [{ x: 360, y: 300 }, { x: 540, y: 300 }], {});
            log(fromBounds.shadowPolygon.length + '|' + fromVertices.shadowPolygon.length);
            """);

        // Two contact points plus two projected points in each case; see CastShadowTests for the geometry.
        Assert.True(result.Success, result.Error);
        Assert.Contains("4|4", string.Join(" ", result.Logs));
    }

    [Fact]
    public void TestScriptAuthoredModelStillRenders()
    {
        var reconstructed = Render("""
            const head = Drawing.createLoomisHead(200, 60, 260, 30, 0);
            const eye = { inner: head.nearEye.inner, outer: head.nearEye.outer, center: head.nearEye.center,
                          width: head.nearEye.width, height: head.nearEye.height };
            Drawing.drawComicEye(ctx, eye, false, { irisColor: '#3b6a8a' });
            """);

        Assert.NotEqual(BlankCanvasSize, reconstructed);
    }
    #endregion

    #region Methods
    /// <summary>Byte length of the 400x400 white canvas the helpers below draw onto, with nothing on it.</summary>
    private static int BlankCanvasSize { get; } = Render("");

    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const c = createCanvas(400, 400); const ctx = c.getContext('2d'); {body} c;", 400, 400, null, "png", 90);

    private static int Render(string call)
    {
        var result = new JsDrawingEngine().Execute(
            $"const c = createCanvas(400,400); const ctx = c.getContext('2d'); ctx.fillStyle='#fff'; ctx.fillRect(0,0,400,400); {call} c;",
            400, 400, null, "png", 90);

        Assert.True(result.Success, result.Error);
        return result.ImageBytes?.Length ?? 0;
    }
    #endregion
}
