namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// A mannequin's own head, moved by <c>Drawing.applyFaceChannels</c> and drawn by <c>Drawing.drawFace</c> inside a
/// <c>comp.drawn</c> layer, as an agent would write it: a line of speech's weights and poses as channels, and blinks from
/// <c>Motion.nodes.blinks</c>. anim3's keeper could have none of this; its head was drawn feature by feature.
/// </summary>
public class FaceOnFigureScriptTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Directory.CreateTempSubdirectory("polson-faceon-").FullName;
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Tests
    [Fact]
    public async Task TestAFiguresHeadSpeaksAndBlinksThroughTheFaceCalls()
    {
        var result = await new DrawingMcpTools(null, null, null, root).ExecuteScript("""
            const line = Motion.speech('Bob moved the moon.', { start: 0.2 });
            const blink = Motion.nodes.blinks({ at: [1.5] });
            const POSES = line.poses();
            const MOUTH = line.weights();
            const figureAt = () => Drawing.createMannequinFigure(200, 30, 560, { pose: { spineDeg: 12 } });

            // The channels at t: the blink, a smile, and the mouths of the line as pose weights.
            const channelsAt = (v) => Object.assign({ AU45: v.blink, joy: 0.3 }, ...Object.keys(MOUTH).map(m => ({ [m]: v[m] })));
            const faceAt = (v) => Drawing.applyFaceChannels(Drawing.createHeadForFigure(figureAt(), { yawDeg: 15 }), channelsAt(v), { poses: POSES });

            const comp = Motion.composition({ width: 400, height: 640, fps: 12, duration: 2 });
            comp.fill({ color: '#f5f2e9' });
            comp.drawn((ctx, v, t) => {
              const head = faceAt(v);
              ctx.save();
              ctx.translate(head.fit.pivot.x, head.fit.pivot.y);
              ctx.rotate(head.fit.rollDeg * Math.PI / 180);
              ctx.translate(-head.fit.pivot.x, -head.fit.pivot.y);
              Drawing.drawFace(ctx, head, { nose: { treatment: 'male2' }, mouth: { full: 0.4 } });
              ctx.restore();
            }, { values: Object.assign({ blink }, MOUTH) });

            // Read the head the layer draws at chosen times, from the same values the layer is handed.
            const vAt = (t) => { const v = { blink: blink.at(t) }; for (const m of Object.keys(MOUTH)) v[m] = MOUTH[m].at(t); return v; };
            const mid = (p) => (p.start + p.end) / 2 - 0.04;
            const chin = (t) => faceAt(vAt(t)).jaw.chinNear.y;
            const shut = (t) => faceAt(vAt(t)).nearEye.closure || 0;
            let peak = 0, peakT = 0;
            for (let t = 1.3; t <= 1.8; t += 0.02) if (blink.at(t) > peak) { peak = blink.at(t); peakT = t; }
            log(JSON.stringify({
              b: chin(mid(line.phonemes.find(p => p.mouth === 'MBP'))),
              ah: chin(mid(line.phonemes.find(p => p.mouth === 'AI'))),
              rest: chin(0.05),
              open: shut(0.05), blinkPeak: peak, closedAtBlink: shut(peakT)
            }));
            return comp.render(1.0);
            """, 400, 640, includeBytes: true);

        Assert.True(result.Success, result.Error);
        var read = JsonDocument.Parse(result.Logs.Single(l => l.StartsWith("[LOG] {"))["[LOG] ".Length..]).RootElement;
        double R(string k) => read.GetProperty(k).GetDouble();

        Assert.True(R("ah") > R("rest") + 1, $"the vowel of 'Bob' drops the jaw: chin {R("ah"):0.0} against {R("rest"):0.0} at rest");
        Assert.True(R("b") < R("ah") - 1, $"its B closes it: chin {R("b"):0.0} against {R("ah"):0.0}");
        Assert.Equal(0d, R("open"), 3);
        Assert.True(R("blinkPeak") > 0.9 && R("closedAtBlink") > 0.9, $"the blink shuts the eye: {R("closedAtBlink"):0.00} at its peak {R("blinkPeak"):0.00}");
        Assert.NotNull(result.ImageBytes);
    }
    #endregion
}
