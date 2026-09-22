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

    //: A barrel, which is the prop section 11 named as the one gap five probes found: it BULGES, so
    //: no combination of a cylinder and a cone reaches it — a primitive's profile is fixed, and
    //: radius can be constant in height or linear in it and nothing else. Both ends sit on the axis,
    //: which is how a cap is expressed here rather than as a flag.
    const int BarrelSides = 24;

    const string Barrel = """
        {"ops":[
          {"op":"lathe","name":"barrel","sides":24,
           "profile":[[0,0],[0.30,0],[0.34,0.15],[0.36,0.40],[0.34,0.65],[0.30,0.80],[0,0.80]]}
        ],"export":"barrel"}
        """;

    //: The same wall, written top-down. A revolve's winding follows the profile's direction, so this
    //: is the pair that separates "the same shape" from "the same solid".
    const string ReversedBarrel = """
        {"ops":[
          {"op":"lathe","name":"barrel","sides":24,
           "profile":[[0,0.80],[0.30,0.80],[0.34,0.65],[0.36,0.40],[0.34,0.15],[0.30,0],[0,0]]}
        ],"export":"barrel"}
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

        ("a lathe with no profile", "bad-profile", 0, """
            {"ops":[{"op":"lathe","name":"b"}]}
            """),

        ("a profile of one point", "bad-profile", 0, """
            {"ops":[{"op":"lathe","name":"b","profile":[[0.2,0]]}]}
            """),

        //: A negative radius sweeps the wall through the axis and out the far side, so the solid
        //: comes back inside out — which builds and exports perfectly and is visible only once lit.
        ("a negative profile radius", "bad-profile", 0, """
            {"ops":[{"op":"lathe","name":"b","profile":[[0.2,0],[-0.3,1]]}]}
            """),

        ("a profile lying entirely on the axis", "bad-profile", 0, """
            {"ops":[{"op":"lathe","name":"b","profile":[[0,0],[0,1]]}]}
            """),

        ("a profile point that is not a pair", "bad-profile", 0, """
            {"ops":[{"op":"lathe","name":"b","profile":[[0.2,0,5],[0.3,1,5]]}]}
            """),

        ("a profile point that is not a number", "bad-profile", 0, """
            {"ops":[{"op":"lathe","name":"b","profile":[[0.2,0],["x",1]]}]}
            """),

        ("a lathe sweeping nothing", "bad-angle", 0, """
            {"ops":[{"op":"lathe","name":"b","angleDeg":0,"profile":[[0.2,0],[0.3,1]]}]}
            """),

        ("a lathe sweeping past a full turn", "bad-angle", 0, """
            {"ops":[{"op":"lathe","name":"b","angleDeg":540,"profile":[[0.2,0],[0.3,1]]}]}
            """),

        ("a lathe with two sides", "bad-sides", 0, """
            {"ops":[{"op":"lathe","name":"b","sides":2,"profile":[[0.2,0],[0.3,1]]}]}
            """),

        //: The pair below pins the ORDER of the checks, not just their presence. The interpreter
        //: tests its `BINDS` field before dispatching an op at all, so a nameless op reports the
        //: missing name even when its own fields are wrong too — and both of these have two faults.
        //: The pre-flight got this backwards until the lathe was added, reporting `bad-shape` for
        //: the prim; nothing caught it, because every earlier corpus entry carried a name.
        ("a nameless lathe with a bad profile too", "missing-name", 0, """
            {"ops":[{"op":"lathe"}]}
            """),

        ("a nameless prim with a bad shape too", "missing-name", 0, """
            {"ops":[{"op":"prim","shape":"torus"}]}
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

        //: Matched as key/value PAIRS rather than through `Keys`, because BINDS is the one mirrored
        //: dict whose values are lowercase quoted strings — `Keys` would return "name" alongside the
        //: op names and the comparison would say less than it appears to.
        var pairs = Regex.Matches(
                Regex.Match(source, @"BINDS = \{([^}]*)\}").Groups[1].Value,
                @"""(\w+)"":\s*""(\w+)""")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

        Assert.NotEmpty(pairs);
        Assert.Equal(pairs.OrderBy(p => p.Key), BlenderDriver.KnownBinds.OrderBy(p => p.Key));
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

    /// <summary>A modifier reaches its own object and no other — section 9's second half.</summary>
    /// <remarks>
    /// <b>This exists because <see cref="ABevelReachesTheArtifact"/> passed while the bug was live.</b>
    /// That test asserts the bevel reaches the GLB, which it did — onto the legs as well as the seat,
    /// because Blender's <c>join</c> merges the selection into the <i>active</i> object and the active
    /// object keeps its modifier stack. Bytes went up, the test went green, and the chair was wrong.
    /// <para>
    /// Single assignment does not help: it stops a name meaning two different objects and says
    /// nothing about whose modifiers reach the merged geometry. So the arithmetic is asserted
    /// exactly rather than as an inequality — measured before the fix, joining a bevelled cube to a
    /// plain one gave <b>192</b> verts, precisely twice the bevelled cube, where scoped is 96 + 8.
    /// </para>
    /// </remarks>
    [Fact]
    public void AModifierDoesNotLeakAcrossAJoin()
    {
        if (!Ready()) return;

        const string Cube = """{"op":"prim","shape":"cube","name":"%","loc":[%x,0,0],"scale":[1,1,1]}""";
        string One(string name, int x) => Cube.Replace("%x", x.ToString()).Replace("%", name);

        using var d1 = new TempDirectory();
        using var d2 = new TempDirectory();
        using var d3 = new TempDirectory();

        var bevelled = BlenderDriver.Build(
            $$"""{"ops":[{{One("a", 0)}},{"op":"bevel","on":"a","width":0.1,"segments":3}],"export":"a"}""",
            d1.Path);
        var plain = BlenderDriver.Build($$"""{"ops":[{{One("a", 0)}}],"export":"a"}""", d2.Path);
        var joined = BlenderDriver.Build(
            $$"""
            {"ops":[{{One("a", 0)}},{{One("b", 3)}},
                    {"op":"bevel","on":"a","width":0.1,"segments":3},
                    {"op":"join","names":["a","b"],"name":"j"}],"export":"j"}
            """, d3.Path);

        Assert.True(bevelled.Ok, bevelled.Error);
        Assert.True(plain.Ok, plain.Error);
        Assert.True(joined.Ok, joined.Error);

        Assert.Equal(bevelled.MeshVerts + plain.MeshVerts, joined.MeshVerts);
        output.WriteLine($"bevelled {bevelled.MeshVerts} + plain {plain.MeshVerts} "
            + $"= joined {joined.MeshVerts} (a leak would read {bevelled.MeshVerts * 2})");
    }

    /// <summary>A lathe passes the pre-flight and reports what it would export.</summary>
    [Fact]
    public void PreflightPassesABarrel()
    {
        var result = BlenderDriver.Validate(Barrel);

        Assert.True(result.Ok, result.Message);
        Assert.Equal("barrel", result.Export);
        Assert.Equal(["barrel"], result.Live);
    }

    /// <summary>A barrel comes back as the solid of revolution its profile describes, exactly.</summary>
    /// <remarks>
    /// <b>Both assertions are closed form rather than golden numbers</b>, which is what makes this a
    /// check on the revolve rather than a record of one run. The counts follow from the profile: five
    /// of its seven points sit off the axis and sweep a ring apiece, the two on it weld to one pole
    /// each, and the two bands touching a pole are triangles where the other four are quads. The
    /// volume follows from the frustum formula times the inscription factor of a 24-sided polygon —
    /// so it would catch a revolve that produced the right topology at the wrong radius, which the
    /// counts cannot see.
    /// <para>
    /// The <i>sign</i> is the winding. A mesh built inside out has the same counts and the same
    /// volume magnitude and differs only here — it renders perfectly in a wireframe and is lit
    /// inside out, which is the failure the interpreter's <c>recalc_face_normals</c> exists to stop.
    /// </para>
    /// </remarks>
    [Fact]
    public void ABarrelIsTheSolidItsProfileDescribes()
    {
        if (!Ready()) return;

        using var dir = new TempDirectory();
        var build = BlenderDriver.Build(Barrel, dir.Path);

        Assert.True(build.Ok, build.Error + " " + build.Diagnostics);
        Assert.Equal("barrel", build.Exported);

        Assert.Equal((5 * BarrelSides) + 2, build.MeshVerts);
        Assert.Equal((2 * BarrelSides) + (4 * BarrelSides * 2), build.MeshTris);

        var volume = SignedVolume(new MeshToolkit(dir.Path).Load("prop.glb"));
        Assert.Equal(BarrelVolume(BarrelSides), volume, 4);

        output.WriteLine($"barrel: {build.MeshVerts} verts / {build.MeshTris} tris, "
            + $"volume {volume:+0.000000;-0.000000} against "
            + $"{BarrelVolume(BarrelSides):0.000000} predicted, {build.Ms} ms");
    }

    /// <summary>A profile written top-down gives the same solid as one written bottom-up.</summary>
    /// <remarks>
    /// A revolve's face winding follows the direction the profile was written in, so without the
    /// interpreter's normal pass these two would be the same shape with opposite normals — identical
    /// in a wireframe, one of them lit inside out. The volume's <b>sign</b> is what separates them
    /// and the counts are not, which is why both are asserted.
    /// </remarks>
    [Fact]
    public void AProfileWrittenEitherWayGivesTheSameSolid()
    {
        if (!Ready()) return;

        using var down = new TempDirectory();
        using var up = new TempDirectory();

        var a = BlenderDriver.Build(Barrel, down.Path);
        var b = BlenderDriver.Build(ReversedBarrel, up.Path);

        Assert.True(a.Ok, a.Error);
        Assert.True(b.Ok, b.Error);
        Assert.Equal(a.MeshVerts, b.MeshVerts);
        Assert.Equal(a.MeshTris, b.MeshTris);

        var forward = SignedVolume(new MeshToolkit(down.Path).Load("prop.glb"));
        var reversed = SignedVolume(new MeshToolkit(up.Path).Load("prop.glb"));

        Assert.Equal(forward, reversed, 4);
        Assert.True(forward > 0, $"the barrel is wound inside out: volume {forward}");

        output.WriteLine($"bottom-up {forward:0.000000} == top-down {reversed:0.000000}");
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

    /// <summary>A mesh's volume by the divergence theorem, signed by its winding.</summary>
    /// <remarks>
    /// Outward winding on a closed mesh gives a positive volume and inside out gives the same
    /// magnitude negative, so this is the one measurement that reads both the shape and the
    /// orientation. Blender's Z-up to glTF's Y-up is a rotation rather than a reflection, so the
    /// sign survives the export.
    /// </remarks>
    static double SignedVolume(FaceMesh mesh)
    {
        var volume = 0.0;
        for (var t = 0; t < mesh.Indices.Length / 3; t++)
        {
            var a = mesh.Vertices[mesh.Indices[(t * 3) + 0]];
            var b = mesh.Vertices[mesh.Indices[(t * 3) + 1]];
            var c = mesh.Vertices[mesh.Indices[(t * 3) + 2]];
            volume += ((a.X * ((b.Y * c.Z) - (b.Z * c.Y)))
                     - (a.Y * ((b.X * c.Z) - (b.Z * c.X)))
                     + (a.Z * ((b.X * c.Y) - (b.Y * c.X)))) / 6.0;
        }

        return volume;
    }

    /// <summary>What <see cref="Barrel"/> should displace, from the profile and nothing else.</summary>
    /// <remarks>
    /// The wall is a stack of frustums, each of volume <c>π h (r1² + r1 r2 + r2²) / 3</c>. A revolve
    /// of <paramref name="sides"/> steps inscribes that circle rather than matching it, so the
    /// result is short by a factor the polygon's own area ratio gives — 1.14% at 24 sides. Including
    /// it is what makes this an equality rather than an inequality with a fudge in it.
    /// </remarks>
    static double BarrelVolume(int sides)
    {
        (double R, double Z)[] wall =
            [(0.30, 0.0), (0.34, 0.15), (0.36, 0.40), (0.34, 0.65), (0.30, 0.80)];

        var exact = 0.0;
        for (var i = 0; i < wall.Length - 1; i++)
        {
            var (r1, z1) = wall[i];
            var (r2, z2) = wall[i + 1];
            exact += Math.PI * (z2 - z1) * ((r1 * r1) + (r1 * r2) + (r2 * r2)) / 3.0;
        }

        return exact * (sides / (2 * Math.PI)) * Math.Sin(2 * Math.PI / sides);
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
