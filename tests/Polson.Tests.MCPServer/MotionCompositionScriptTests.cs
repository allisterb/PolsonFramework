namespace Polson.Tests.MCPServer;

using System.Linq;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// SPIKE: <c>Motion.composition</c> and <c>Motion.nodes</c> driven from a script, so the marshalling of
/// arrays, object literals and nodes across the boundary is checked where an agent meets it.
/// </summary>
public class MotionCompositionScriptTests : TestsRuntime
{
    [Fact]
    public void AScriptBuildsRendersAndExportsAComposition()
    {
        var result = Execute("""
            const n = Motion.nodes;
            const comp = Motion.composition({ width: 320, height: 180, fps: 8, duration: 1 });
            comp.fill({ color: '#f2efe8' });
            const x = n.animated('real', [{ time: 0, value: 40, ease: 'halt' }, { time: 1, value: 280, ease: 'halt' }]);
            comp.circle({ origin: n.composite(x, 90), radius: 14, color: '#1f6f8b' });
            comp.outline({ points: [{ point: [20, 160], t1: [100, -60], width: 0.3 }, { point: { x: 300, y: 160 }, t1: [100, 60], width: 1.5 }],
                width: 8, color: '#15151a' });
            const g = comp.group({ origin: [160, 90], offset: [160, 90], angle: n.linear('angle', 90) });
            g.region({ points: [[150, 60], [170, 60], [160, 80]], color: '#c9553d' });
            const p = n.composite(x, 90).at(0.5);
            log('frames ' + comp.frameCount + ' x ' + x.at(0.5) + ' p ' + p.x + ',' + p.y + ' layers ' + comp.layerCount + '/' + g.layerCount);
            log('sif ' + (comp.toSif().indexOf('<layer type="group"') > 0));
            log('captured ' + comp.capture({ fps: 4 }) + ' held ' + Motion.count);
            comp.render(0.5);
            """);

        Assert.True(result.Success, result.Error);
        var logs = string.Join("\n", result.Logs);
        Assert.Contains("frames 9 x 160 p 160,90 layers 4/1", logs);
        Assert.Contains("sif true", logs);
        Assert.Contains("captured 5 held 5", logs);
        Assert.NotNull(result.ImageBytes);
    }

    [Theory]
    [InlineData("comp.circle({ radious: 4 });", "has no 'radious'")]
    [InlineData("comp.circle({ radius: n.composite(1, 2) });", "takes a real")]
    [InlineData("n.animated('real', [{ time: 0, value: 1, eas: 'halt' }]);", "has no 'eas'")]
    [InlineData("comp.region({ points: [[0, 0], [1, 1]], loops: true });", "has no 'loops'")]
    [InlineData("comp.outline({ points: [{ pont: [0, 0] }] });", "has no 'pont'")]
    public void AMistakeIsRefusedByName(string line, string expected)
    {
        var result = Execute($"const n = Motion.nodes; const comp = Motion.composition(); {line}");

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void ADrawnLayerPosesAFigureFromKeys()
    {
        var result = Execute("""
            const n = Motion.nodes;
            const comp = Motion.composition({ width: 320, height: 180, fps: 4, duration: 1 });
            comp.fill({ color: '#f2efe8' });
            const lean = n.animated('real', [{ time: 0, value: -20, ease: 'halt' }, { time: 1, value: 25, ease: 'halt' }]);
            const seen = [];
            comp.drawn((ctx, v, t) => {
                seen.push(t);
                const fig = Drawing.createMannequinFigure(160, 10, 160,
                    { pose: { spineDeg: v.lean, rightArm: { shoulderDeg: v.arm, elbowDeg: -30 } } });
                Drawing.drawMannequinSolid(ctx, fig);
            }, { values: { lean, arm: n.linear('real', 60, 20) }, desc: 'figure' });

            const d = comp.render(0).bitmap.diff(comp.render(1).bitmap);
            log('moved ' + (d.similarity < 0.99) + ' calls ' + seen.join(','));
            log('captured ' + comp.capture({ fps: 4 }));
            """);

        Assert.True(result.Success, result.Error);
        var logs = string.Join("\n", result.Logs);
        Assert.Contains("moved true calls 0,1", logs);
        Assert.Contains("captured 5", logs);

        // A host refusal ends the script rather than throwing into it, as every SDK refusal does.
        var sif = Execute("const comp = Motion.composition(); comp.drawn((ctx, v, t) => {}, { desc: 'figure' }); comp.toSif();");
        Assert.False(sif.Success);
        Assert.Contains("'figure' cannot be written as .sif", sif.Error);
    }

    [Fact]
    public void ADrawnLayerWithoutAFunctionFails()
    {
        var result = Execute("const comp = Motion.composition(); comp.drawn({ values: { a: 1 } });");

        Assert.False(result.Success);
        Assert.Contains("takes a function first", result.Error);
    }

    private static DrawingExecutionResult Execute(string body) =>
        new JsDrawingEngine().Execute($"const c = createCanvas(320, 180); {body}", 320, 180, null, "png", 90);
}
