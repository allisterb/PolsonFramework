namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Polson.MCPServer;
using Xunit;

/// <summary>
/// The animation workflow's recipe for a mannequin that speaks, run as printed. The recipe is read out of
/// <c>ProjectTemplate/animation/instructions.md</c> rather than copied here, because the template is the agent's only
/// copy of it: a fix made in one place and not the other would pass here and fail the agent.
/// </summary>
/// <remarks>
/// <para>
/// Found by running earlier versions first: <c>line.weights()</c> holds only the mouths a line uses while
/// <c>line.poses()</c> holds all ten, so a mouth with no node must read as 0 rather than <c>NaN</c>; and every line names
/// its mouths the same, so the recipe names them per line and reads back the line speaking at <c>t</c>.
/// </para>
/// <para>
/// Since 2026-10-15 the face is <c>applyFaceChannels</c> and <c>drawFace</c>: blinks and an expression ride with the
/// mouths, where anim3 and anim4 drew default features around a hand-rolled mouth.
/// </para>
/// </remarks>
public class SpeakingFigureRecipeTests : TestsRuntime, IDisposable
{
    #region Fields
    private readonly string root = Directory.CreateTempSubdirectory("polson-recipe-").FullName;
    #endregion

    #region Methods
    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    /// <summary>The fenced recipe under "The face acts with the body" in the animation template.</summary>
    private static string Recipe()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Polson.CLI", "ProjectTemplate", "animation", "instructions.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var text = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Polson.CLI", "ProjectTemplate", "animation", "instructions.md"));
        var from = text.IndexOf("**The face acts with the body", StringComparison.Ordinal);
        Assert.True(from >= 0, "the template no longer has the speaking recipe");
        var open = text.IndexOf("```js", from, StringComparison.Ordinal) + "```js".Length;
        return text[open..text.IndexOf("```", open, StringComparison.Ordinal)];
    }
    #endregion

    #region Tests
    [Fact]
    public async Task TestTheMannequinSpeaksWithTheTemplatesRecipe()
    {
        // What the recipe assumes from the stages before it, then the recipe, then a reading of what it does.
        var script = """
            const LINES = [Motion.speech('Who knew?', { start: 0.2 }), Motion.speech('Bob moved the moon.', { start: 1.3 })];
            const KEYED = {};
            const CHARACTER = {};
            const INK = '#1a1a18';
            const JOY = Motion.nodes.constant('real', 0.3);
            const figureAt = (v) => Drawing.createMannequinFigure(120, 20, 290, {});
            const comp = Motion.composition({ width: 240, height: 320, fps: 12, duration: 3 });
            comp.fill({ color: '#f5f2e9' });
            """ + Recipe() + """

            // The head the layer draws at t, from the same values the layer is handed.
            const vAt = (t) => {
              const v = { blink: BLINK.at(t), joy: JOY.at(t) };
              for (const k of Object.keys(MOUTHS)) v[k] = MOUTHS[k].at(t);
              return v;
            };
            const headAt = (t) => faceAt(figureAt(vAt(t)), vAt(t), t);   // the recipe's own head, as the layer draws it
            const at = (p) => (p.start + p.end) / 2 - 0.04;   // mid-sound, less the lead the mouths are drawn ahead by
            let peak = 0, peakT = 0;
            for (let t = 0; t <= 3; t += 0.02) if (BLINK.at(t) > peak) { peak = BLINK.at(t); peakT = t; }
            log(JSON.stringify({
              ah: headAt(at(LINES[1].phonemes.find(p => p.mouth === 'AI'))).jaw.chinNear.y,
              b: headAt(at(LINES[1].phonemes.find(p => p.mouth === 'MBP'))).jaw.chinNear.y,
              between: headAt((LINES[0].end + LINES[1].start) / 2).jaw.chinNear.y,
              blinkPeak: peak, shutAtBlink: headAt(peakT).nearEye.closure || 0,
              scale: headAt(0.1).fit.scale
            }));
            return comp.render(at(LINES[1].phonemes.find(p => p.mouth === 'MBP')));
            """;

        var result = await new DrawingMcpTools(null, null, null, root).ExecuteScript(script, 240, 320, includeBytes: true);

        Assert.True(result.Success, result.Error + "\n--- script ---\n" + script);
        var read = JsonDocument.Parse(result.Logs.Single(l => l.StartsWith("[LOG] {")).Substring("[LOG] ".Length)).RootElement;
        double R(string k) => read.GetProperty(k).GetDouble();

        Assert.True(R("ah") > R("between") + 0.5, $"the vowel of 'Bob' drops the jaw: chin {R("ah"):0.00} against {R("between"):0.00} between lines");
        Assert.True(R("b") < R("ah") - 0.5, $"its B closes it: chin {R("b"):0.00} against {R("ah"):0.00}");
        Assert.True(R("blinkPeak") > 0.9 && R("shutAtBlink") > 0.9, $"the blink shuts the eye: {R("shutAtBlink"):0.00} at its peak {R("blinkPeak"):0.00}");
        Assert.Equal(1.5d, R("scale"), 3);
        Assert.NotNull(result.ImageBytes);
    }
    #endregion
}
