namespace Polson.Tests.MCPServer;

using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Polson.ExtendedMind.CharacterGeneration;
using Polson.ExtendedMind.ObjectGeneration;
using Polson.MCPServer;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// <c>GenerateCharacter</c> end to end: views in, a posable character a script can load out.
/// </summary>
/// <remarks>
/// <b>Live, and needs three things</b>: <c>POLSON_CHARACTER_DIR</c> holding <c>turnaround.jpg</c> (a
/// FRONT, BACK, LEFT, RIGHT sheet), <c>POLSON_TRELLIS_URL</c> for a reconstruction service loaded for
/// images, and <c>POLSON_RIG_URL</c> for a rig service — which may be <c>unirig_server.py --echo
/// --echo-file</c> answering with a rigged file of the same character, so everything after rigging
/// runs for real on a machine with no rigging GPU. Without them it passes having said so.
/// </remarks>
public class CharacterGenerationLiveTests : TestsRuntime
{
    readonly ITestOutputHelper output;

    public CharacterGenerationLiveTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task ACharacterIsBuiltFromItsViewsAndLoadsInAScript()
    {
        var src = Environment.GetEnvironmentVariable("POLSON_CHARACTER_DIR");
        var trellis = Environment.GetEnvironmentVariable("POLSON_TRELLIS_URL");
        var rig = Environment.GetEnvironmentVariable("POLSON_RIG_URL");
        if (src is null || trellis is null || rig is null || !File.Exists(Path.Combine(src, "turnaround.jpg")))
        {
            output.WriteLine("NOT RUN: set POLSON_CHARACTER_DIR, POLSON_TRELLIS_URL and POLSON_RIG_URL");
            return;
        }

        // The sheet as the director supplied it, captions and all: splitting it is part of what is tested.
        var project = Path.Combine(Path.GetTempPath(), "polson-character-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(project, "refs"));
        File.Copy(Path.Combine(src, "turnaround.jpg"), Path.Combine(project, "refs", "julie-sheet.jpg"));

        DrawingMcpTools.CharacterReconstructor = new TrellisClient(trellis);
        DrawingMcpTools.CharacterRigger = new RigClient(rig);
        var tools = new DrawingMcpTools(null, null, null, project);

        var reply = await tools.GenerateCharacter(name: "julie", sheet: "refs/julie-sheet.jpg");
        while (reply["status"]?.GetValue<string>() == "running")
        {
            output.WriteLine($"  {reply["stage"]} after {reply["elapsedSeconds"]}s");
            reply = await tools.GenerateCharacter(jobId: reply["jobId"]!.GetValue<string>());
        }
        output.WriteLine(reply.ToJsonString(new() { WriteIndented = true }));
        Assert.Equal("completed", reply["status"]?.GetValue<string>());
        output.WriteLine($"project: {project}");

        var script = """
            const julie = Character.load('julie');
            log(Character.list().join(','));
            log(JSON.stringify(julie.jointMap));
            const c = createCanvas(1200, 560);
            const ctx = c.getContext('2d');
            ctx.fillStyle = '#f2efe8'; ctx.fillRect(0, 0, 1200, 560);
            const poses = [{}, { head: { yDeg: 35 } }, { head: { yDeg: 90 }, leftUpperArm: { zDeg: 40 }, rightUpperArm: { zDeg: -40 } }];
            poses.forEach((p, i) => Mesh.draw(ctx, julie.pose(p), { x: 200 + i * 400, y: 280, scale: 520,
                expression: i === 2 ? { browDown: 1, mouthFrown: 0.8 } : {} }));
            c;
            """;
        var result = await tools.ExecuteScript(script, outFile: "artifacts/character.png", format: "png");
        output.WriteLine(string.Join("\n", result.Logs ?? []));
        Assert.True(result.Success, result.Error);
    }
}
