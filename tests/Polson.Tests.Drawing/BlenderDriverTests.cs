namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary>The prop pipeline: its pre-flight, its drift guards, and a real build when there is one.</summary>
/// <remarks>
/// <b>Most of this runs on a machine with no Blender</b>, which is the point. The op-list analysis is
/// ours and is the whole reason a bad list costs nothing — so it must be testable without a 900 MB
/// install. Only the live builds are gated, and they say so rather than passing silently.
/// </remarks>
public class BlenderDriverTests : Polson.Tests.TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;

    //: A chair, which is the shape section 2 built by hand and section 9's bug came from.
    const string Chair = """
        {"ops":[
          {"op":"prim","shape":"cube","name":"seat","loc":[0,0,0.44],"scale":[0.5,0.5,0.04]},
          {"op":"prim","shape":"cylinder","name":"leg0","sides":12,"loc":[-0.2,-0.2,0.21],"scale":[0.04,0.04,0.42]},
          {"op":"prim","shape":"cylinder","name":"leg1","sides":12,"loc":[0.2,-0.2,0.21],"scale":[0.04,0.04,0.42]},
          {"op":"prim","shape":"cylinder","name":"leg2","sides":12,"loc":[-0.2,0.2,0.21],"scale":[0.04,0.04,0.42]},
          {"op":"prim","shape":"cylinder","name":"leg3","sides":12,"loc":[0.2,0.2,0.21],"scale":[0.04,0.04,0.42]},
          {"op":"join","names":["seat","leg0","leg1","leg2","leg3"],"name":"chair"}
        ]}
        """;

    /// <summary>Op lists that must be refused, and the rule that must refuse each.</summary>
    /// <remarks>
    /// <b>This corpus is run against BOTH validators</b> — the C# pre-flight here, and the
    /// interpreter itself in <see cref="InterpreterAgreesWithPreflight"/>. The analysis is duplicated
    /// by necessity (nothing can be imported across the boundary), so it is the shared corpus rather
    /// than good intentions that keeps the two from drifting apart.
    /// </remarks>
    public static readonly (string Name, string Code, int? Index, string Json)[] Refusals =
    [
        ("join rebinding its own input — section 9's bug", "name-rebound", 5, """
            {"ops":[
              {"op":"prim","shape":"cube","name":"seat"},
              {"op":"prim","shape":"cube","name":"leg0"},
              {"op":"prim","shape":"cube","name":"leg1"},
              {"op":"prim","shape":"cube","name":"leg2"},
              {"op":"prim","shape":"cube","name":"leg3"},
              {"op":"join","names":["seat","leg0","leg1","leg2","leg3"],"name":"seat"}
            ]}
            """),

        ("a name reused by a later prim", "name-rebound", 1, """
            {"ops":[{"op":"prim","shape":"cube","name":"seat"},
                    {"op":"prim","shape":"cube","name":"seat"}]}
            """),

        ("use after join consumed it", "name-consumed", 3, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"prim","shape":"cube","name":"b"},
                    {"op":"join","names":["a","b"],"name":"c"},
                    {"op":"bevel","on":"a","width":0.01}]}
            """),

        ("use after boolean consumed it", "name-consumed", 3, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"prim","shape":"cube","name":"b"},
                    {"op":"boolean","how":"difference","on":"a","with":"b"},
                    {"op":"uv","on":"b"}]}
            """),

        ("a reference to something never created", "name-unbound", 1, """
            {"ops":[{"op":"prim","shape":"cube","name":"seat"},
                    {"op":"bevel","on":"backrest","width":0.01}]}
            """),

        ("an op nobody implements", "unknown-op", 0, """
            {"ops":[{"op":"extrude","on":"seat"}]}
            """),

        ("a shape nobody implements", "unknown-shape", 0, """
            {"ops":[{"op":"prim","shape":"torus","name":"ring"}]}
            """),

        ("a boolean nobody implements", "unknown-boolean", 2, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"prim","shape":"cube","name":"b"},
                    {"op":"boolean","how":"subtract","on":"a","with":"b"}]}
            """),

        ("a cylinder with two sides", "bad-sides", 0, """
            {"ops":[{"op":"prim","shape":"cylinder","name":"a","sides":2}]}
            """),

        ("a boolean against itself", "same-operands", 1, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"boolean","how":"difference","on":"a","with":"a"}]}
            """),

        ("a join of one", "join-arity", 1, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"join","names":["a"],"name":"b"}]}
            """),

        ("a join naming the same thing twice", "join-duplicate", 2, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"prim","shape":"cube","name":"b"},
                    {"op":"join","names":["a","a"],"name":"c"}]}
            """),

        ("a uv projection nobody implements", "unknown-uv", 1, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"uv","on":"a","how":"spherical"}]}
            """),

        ("no ops at all", "no-ops", null, """{"ops":[]}"""),

        ("two live objects and nothing saying which to export", "export-ambiguous", null, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"prim","shape":"cube","name":"b"}]}
            """),

        ("an export naming something consumed", "export-unknown", null, """
            {"ops":[{"op":"prim","shape":"cube","name":"a"},
                    {"op":"prim","shape":"cube","name":"b"},
                    {"op":"join","names":["a","b"],"name":"c"}],"export":"a"}
            """),
    ];
    #endregion

    #region Constructors
    public BlenderDriverTests(ITestOutputHelper output) => this.output = output;
    #endregion

    #region Methods (tests)
    /// <summary>Every refusal in the corpus is caught by the pre-flight, with the right code.</summary>
    [Fact]
    public void PreflightRefusesTheCorpus()
    {
        foreach (var (name, code, index, json) in Refusals)
        {
            var result = BlenderDriver.Validate(json);
            Assert.False(result.Ok, $"{name}: expected a refusal, got none");
            Assert.Equal(code, result.Code);
            Assert.Equal(index, result.Index);
            Assert.NotEqual("", result.Message);
            output.WriteLine($"{code,-18} op={index?.ToString() ?? "-",-4} {name}");
        }
    }

    /// <summary>A well-formed list passes, and says what it would export.</summary>
    [Fact]
    public void PreflightPassesAChair()
    {
        var result = BlenderDriver.Validate(Chair);

        Assert.True(result.Ok, result.Message);
        Assert.Equal("chair", result.Export);
        Assert.Equal(["chair"], result.Live);
    }

    /// <summary>Bevelling before the join is fine; it is the rebind that was never the ordering.</summary>
    /// <remarks>
    /// The pair that makes section 9's finding a naming problem rather than a sequencing one: the
    /// same two ops in the same order are accepted when <c>join</c> yields a fresh name, and refused
    /// when it does not.
    /// </remarks>
    [Fact]
    public void BevelBeforeJoinIsAccepted()
    {
        var json = Chair.Replace(
            """{"op":"join",""",
            """{"op":"bevel","on":"seat","width":0.012,"segments":3},{"op":"join",""");

        Assert.True(BlenderDriver.Validate(json).Ok);
    }

    /// <summary>Malformed JSON is refused here and cannot reach the interpreter.</summary>
    [Fact]
    public void MalformedJsonIsRefusedLocally()
    {
        var result = BlenderDriver.Validate("""{"ops":[{"op":"prim",}]}""");

        Assert.False(result.Ok);
        Assert.Equal("bad-json", result.Code);
    }

    /// <summary>The driver's dispatch tables still match the interpreter's.</summary>
    /// <remarks>
    /// <b>A drift guard, not a style check.</b> The driver mirrors the interpreter's vocabulary so a
    /// wrong op is refused before a Blender startup is paid for — and a mirror that silently falls
    /// behind is worse than none, because it would refuse an op the interpreter has grown, or wave
    /// through one it has dropped. Reads the Python source rather than trusting the comment on it.
    /// </remarks>
    [Fact]
    public void DispatchTablesMatchTheInterpreter()
    {
        var script = BlenderDriver.Script;
        Assert.NotNull(script);
        var source = File.ReadAllText(script!);

        Assert.Equal(Keys(source, "OPS"), BlenderDriver.KnownOps.Order());
        Assert.Equal(Keys(source, "PRIMS"), BlenderDriver.KnownShapes.Order());
        Assert.Equal(Keys(source, "BOOLEANS"), new[] { "difference", "intersect", "union" });

        var projections = Regex.Match(source, @"UV_PROJECTIONS = \(([^)]*)\)").Groups[1].Value;
        Assert.Equal(["cube", "smart"], Quoted(projections).Order());
    }

    /// <summary>A real chair, and the numbers it comes back with.</summary>
    [Fact]
    public void BuildsAChair()
    {
        if (!Ready()) return;

        using var dir = new TempDirectory();
        var build = BlenderDriver.Build(Chair, dir.Path);

        Assert.True(build.Ok, build.Error + " " + build.Diagnostics);
        Assert.Equal("chair", build.Exported);
        Assert.NotNull(build.ArtifactPath);
        Assert.True(File.Exists(build.ArtifactPath), "the GLB was reported but is not there");
        Assert.Equal(new FileInfo(build.ArtifactPath!).Length, build.Bytes);

        //: glTF splits a vertex at every normal and UV discontinuity, so the artifact always carries
        //: more than the mesh — measured at three to four times, never fewer. It is this count that
        //: meets MeshGltf's 65,535 ceiling, which is why both are reported.
        Assert.True(build.Verts > build.MeshVerts,
            $"expected the GLB to carry more verts than the mesh, got {build.Verts} vs {build.MeshVerts}");

        //: Splitting duplicates vertices and never faces, so these must agree exactly. A mismatch
        //: means the GLB was parsed wrongly rather than that the prop is unusual.
        Assert.Equal(build.MeshTris, build.Tris);

        output.WriteLine($"chair: GLB {build.Verts} verts / {build.Tris} tris / {build.Bytes} B, "
            + $"mesh {build.MeshVerts} / {build.MeshTris}, {build.Ms} ms");
    }

    /// <summary>A modifier reaches the artifact, which is section 9's first finding.</summary>
    /// <remarks>
    /// <c>export_apply</c> defaults to <b>False</b> and drops every modifier silently: both chairs
    /// first exported at exactly 6,076 bytes, identical, with the bevel present in the JSON and no
    /// error anywhere. This is the test that keeps that from coming back.
    /// </remarks>
    [Fact]
    public void ABevelReachesTheArtifact()
    {
        if (!Ready()) return;

        var bevelled = Chair.Replace(
            """{"op":"join",""",
            """{"op":"bevel","on":"seat","width":0.012,"segments":3},{"op":"join",""");

        using var plain = new TempDirectory();
        using var round = new TempDirectory();
        var a = BlenderDriver.Build(Chair, plain.Path);
        var b = BlenderDriver.Build(bevelled, round.Path);

        Assert.True(a.Ok, a.Error);
        Assert.True(b.Ok, b.Error);
        Assert.True(b.Bytes > a.Bytes,
            $"the bevel did not reach the GLB: {a.Bytes} B plain against {b.Bytes} B bevelled — "
            + "check that export_apply is still passed");
        Assert.True(b.MeshVerts > a.MeshVerts);

        output.WriteLine($"plain {a.Bytes} B / {a.Verts} verts, bevelled {b.Bytes} B / {b.Verts} verts");
    }

    /// <summary>The interpreter refuses the same lists, for the same stated reasons.</summary>
    /// <remarks>
    /// <b>The test the duplication exists for.</b> <see cref="BlenderDriver.Build"/> never lets a bad
    /// list reach Blender, so the interpreter's own analysis can only be observed by invoking it
    /// directly — which is what this does. Without it, the two copies could disagree indefinitely and
    /// nothing would notice, because the pre-flight would always answer first.
    /// </remarks>
    [Fact]
    public void InterpreterAgreesWithPreflight()
    {
        if (!Ready()) return;

        foreach (var (name, code, index, json) in Refusals)
        {
            if (code == "bad-json") continue;

            using var dir = new TempDirectory();
            var result = RunInterpreterDirectly(json, dir.Path);

            Assert.False(result.GetProperty("ok").GetBoolean(), name);
            Assert.Equal(code, result.GetProperty("code").GetString());

            var at = result.GetProperty("op");
            Assert.Equal(index, at.ValueKind == JsonValueKind.Number ? at.GetInt32() : null);
            output.WriteLine($"agreed: {code,-18} op={index?.ToString() ?? "-",-4} {name}");
        }
    }
    #endregion

    #region Methods (private)
    /// <summary>Whether to go on, saying plainly when it does not.</summary>
    bool Ready()
    {
        if (BlenderDriver.Available) return true;
        output.WriteLine($"NOT RUN: building props needs {BlenderDriver.Missing}.");
        return false;
    }

    /// <summary>Invokes the interpreter with no pre-flight, so its own verdict can be read.</summary>
    static JsonElement RunInterpreterDirectly(string requestJson, string directory)
    {
        var info = new ProcessStartInfo(BlenderDriver.Blender!)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "--background", "--factory-startup", "--disable-autoexec",
            "--python", BlenderDriver.Script!, "--", directory
        })
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using (var stdin = process.StandardInput) stdin.Write(requestJson);
        process.WaitForExit();
        _ = stdout.Result;
        _ = stderr.Result;

        var path = Path.Combine(directory, "result.json");
        Assert.True(File.Exists(path), "the interpreter wrote no result.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    /// <summary>The quoted keys of a Python dict literal, in order.</summary>
    /// <remarks>
    /// Matches braces rather than looking for a <c>}</c> in the first column, because
    /// <c>BOOLEANS</c> is a one-liner and the column rule would run past it into the next dict.
    /// </remarks>
    static IEnumerable<string> Keys(string source, string name)
    {
        var start = source.IndexOf(name + " = {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{name} is no longer a dict literal in build_prop.py");

        var open = source.IndexOf('{', start);
        var depth = 0;
        var end = open;
        for (; end < source.Length; end++)
        {
            if (source[end] == '{') depth++;
            else if (source[end] == '}' && --depth == 0) break;
        }

        return Quoted(source[open..end]).Distinct().Order();
    }

    /// <summary>Every double-quoted token that looks like an identifier.</summary>
    static IEnumerable<string> Quoted(string text) =>
        Regex.Matches(text, "\"([a-z_]+)\"").Select(m => m.Groups[1].Value);
    #endregion

    #region Types
    /// <summary>A scratch directory that removes itself.</summary>
    sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "polson-prop-" + Guid.NewGuid().ToString("N")[..12]);

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }
    #endregion
}
