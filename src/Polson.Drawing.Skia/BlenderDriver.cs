namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>Builds a prop by handing a JSON op list to Blender, and reads back a GLB.</summary>
/// <remarks>
/// <para>
/// <b>An optional backend, exactly as <see cref="BitmapTracer"/> and <see cref="FaceDetector"/>
/// are.</b> The studio builds and runs without it; a caller asks <see cref="Available"/> before
/// committing to a route that needs it, and a machine with no Blender reports the capability as
/// absent rather than failing halfway through a drawing.
/// </para>
/// <para>
/// <b>The agent never writes Python.</b> What crosses is a list of data ops, dispatched by a fixed
/// interpreter through explicit dicts — never <c>getattr</c> on a caller string. Blender is invoked
/// <c>--background --factory-startup --disable-autoexec</c>, so neither the user's preferences nor
/// any auto-running startup script participates, and the working directory is chosen here rather
/// than by a caller. See <c>docs/internal/blender-prop-pipeline.md</c> §2, §3 and §6.
/// </para>
/// <para>
/// <b>Results come back through a file, which is measured rather than cautious.</b> Blender writes
/// its own banner to stdout and the interleaving is not ordered — on a probe the interpreter's own
/// <c>print</c> emerged <i>before</i> Blender's version line. stdout is captured for diagnostics
/// only; the answer is <c>result.json</c> in the working directory.
/// </para>
/// <para>
/// <b>Licence.</b> Blender is GPL-3.0-or-later and is invoked as a separate process rather than
/// linked, which is aggregation — the same boundary potrace sits behind. Take no Python from
/// Blender's own tree into <c>src/</c>; that would be combination. The binary is not committed.
/// </para>
/// </remarks>
public static class BlenderDriver
{
    #region Fields
    //: Set by the host from `Tools:Blender` and `Tools:BlenderScript` when they are configured. The
    //: engine reads no environment variables by design, so an override arrives through settings
    //: exactly as `Assets:Budget` does.
    static string? _blender, _script;
    static string? _rBlender, _rScript;
    static bool _looked;

    //: Mirrors of the interpreter's own dispatch tables. They are duplicated rather than shared
    //: because nothing can be imported across the boundary, and `BlenderDriverTests` reads
    //: build_prop.py to assert the two agree — a drift guard rather than a hope.
    static readonly string[] ops = ["prim", "bevel", "boolean", "join", "uv"];
    static readonly string[] shapes = ["cube", "cylinder", "sphere", "cone"];
    static readonly string[] booleans = ["difference", "union", "intersect"];
    static readonly string[] projections = ["smart", "cube"];
    #endregion

    #region Properties
    /// <summary>An explicit path to blender.exe, or null to discover one.</summary>
    public static string? BlenderOverride
    {
        get => _blender;
        set { _blender = value; _looked = false; }
    }

    /// <summary>An explicit path to the interpreter script, or null to discover one.</summary>
    public static string? ScriptOverride
    {
        get => _script;
        set { _script = value; _looked = false; }
    }

    /// <summary>The Blender that will be used, or null when none was found.</summary>
    public static string? Blender { get { Look(); return _rBlender; } }

    /// <summary>The interpreter that will be used, or null when none was found.</summary>
    public static string? Script { get { Look(); return _rScript; } }

    /// <summary>Whether a prop can be built at all here.</summary>
    public static bool Available => Blender is not null && Script is not null;

    /// <summary>What is missing, for a caller that wants to say so rather than only fail.</summary>
    public static string? Missing
    {
        get
        {
            Look();
            List<string> gaps = [];
            if (_rBlender is null) gaps.Add("blender.exe under bin/");
            if (_rScript is null) gaps.Add("src/blender/build_prop.py");
            return gaps.Count == 0 ? null : string.Join(", ", gaps);
        }
    }

    /// <summary>The ops the interpreter accepts, for a caller that wants to say so.</summary>
    public static IReadOnlyList<string> KnownOps => ops;

    /// <summary>The primitive shapes the interpreter accepts.</summary>
    public static IReadOnlyList<string> KnownShapes => shapes;
    #endregion

    #region Methods
    /// <summary>Checks an op list without building it. Costs nothing and starts no process.</summary>
    /// <remarks>
    /// <b>This is what single assignment buys.</b> A name is bound once by the op that creates it
    /// and never rebound, so the list is a dependency graph rather than a stream of mutations
    /// against implicit state — which makes an unbound reference, a use-after-consume and a rebind
    /// all decidable here, <i>before</i> a Blender startup is paid for. That startup is 4.5 s cold
    /// and about 1 s warm, against a build of roughly 200 ms, so the pre-flight is most of the cost
    /// of being wrong.
    /// <para>
    /// The same analysis runs again inside the interpreter, which is the authoritative copy because
    /// it is what executes. Both emit the same <see cref="PropValidation.Code"/> for the same rule.
    /// One asymmetry: malformed JSON is refused here as <c>bad-json</c> and cannot reach the
    /// interpreter at all.
    /// </para>
    /// </remarks>
    public static PropValidation Validate(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(requestJson); }
        catch (JsonException e) { return PropValidation.Fail("bad-json", null, e.Message); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return PropValidation.Fail("no-ops", null, "the request must be an object");

            if (!root.TryGetProperty("ops", out var list)
                || list.ValueKind != JsonValueKind.Array
                || list.GetArrayLength() == 0)
                return PropValidation.Fail("no-ops", null, "the request carries no ops");

            //: name -> null while live, or the op that consumed it. The two states are kept apart
            //: so a use-after-consume says so rather than reading as a name that never existed.
            Dictionary<string, (string By, int At)?> names = [];
            var index = 0;

            foreach (var op in list.EnumerateArray())
            {
                var fail = Step(op, index, names);
                if (fail is not null) return fail;
                index++;
            }

            var live = names.Where(n => n.Value is null).Select(n => n.Key).OrderBy(n => n).ToList();

            if (!root.TryGetProperty("export", out var export)
                || export.ValueKind == JsonValueKind.Null)
                return live.Count == 1
                    ? PropValidation.Pass(live[0], live)
                    : PropValidation.Fail("export-ambiguous", null,
                        $"the request does not say what to export and {live.Count} objects are "
                        + $"live: {string.Join(", ", live)}");

            var target = export.ValueKind == JsonValueKind.String ? export.GetString() : null;
            return target is not null && live.Contains(target)
                ? PropValidation.Pass(target, live)
                : PropValidation.Fail("export-unknown", null,
                    $"cannot export '{target}'; live objects are "
                    + $"{(live.Count == 0 ? "(none)" : string.Join(", ", live))}");
        }
    }

    /// <summary>Builds a prop, writing <c>prop.glb</c> and <c>result.json</c> into a directory.</summary>
    /// <remarks>
    /// <paramref name="outputDirectory"/> is the host's to choose and is created if absent; the file
    /// names inside it are this driver's. A caller cannot name a file, which is what keeps the
    /// containment rules that govern <c>outFile</c> unreachable here rather than merely satisfied.
    /// <para>
    /// A failed build is a <b>result</b> rather than an exception — <see cref="PropBuild.Ok"/> is
    /// the first thing to read. Only an absent backend throws, because that is an environment to fix
    /// rather than an outcome to handle: the same line <see cref="BitmapTracer"/> draws.
    /// </para>
    /// </remarks>
    public static PropBuild Build(string requestJson, string outputDirectory, int timeoutMs = 120000)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!Available)
            throw new InvalidOperationException(
                $"Building props needs {Missing}. Install Blender under bin/, or set 'Tools:Blender' "
                + "and 'Tools:BlenderScript' in appsettings.json. Check BlenderDriver.Available "
                + "before calling build().");

        var check = Validate(requestJson);
        if (!check.Ok)
            return PropBuild.Refused(check.Code ?? "invalid", check.Index, check.Message);

        Directory.CreateDirectory(outputDirectory);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        if (File.Exists(resultPath)) File.Delete(resultPath);

        var info = new ProcessStartInfo(Blender!)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("--background");
        info.ArgumentList.Add("--factory-startup");
        info.ArgumentList.Add("--disable-autoexec");
        info.ArgumentList.Add("--python");
        info.ArgumentList.Add(Script!);
        info.ArgumentList.Add("--");
        info.ArgumentList.Add(outputDirectory);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("The Blender process would not start.");

        //: Drained concurrently rather than one after the other. Blender is chatty on both streams,
        //: and reading one to the end while the other's pipe buffer fills is a deadlock.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using (var stdin = process.StandardInput)
            stdin.Write(requestJson);

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException($"The prop build did not finish within {timeoutMs} ms.");
        }

        process.WaitForExit();
        var diagnostics = Trim(stderr.Result.Length > 0 ? stderr.Result : stdout.Result);

        if (!File.Exists(resultPath))
            return PropBuild.Refused("no-result", null,
                $"Blender exited {process.ExitCode} without writing result.json. {diagnostics}");

        var build = PropBuild.Parse(File.ReadAllText(resultPath), outputDirectory);
        build.Diagnostics = diagnostics;
        return build;
    }

    static PropValidation? Step(
        JsonElement op, int index, Dictionary<string, (string By, int At)?> names)
    {
        if (op.ValueKind != JsonValueKind.Object)
            return PropValidation.Fail("not-an-op", index, "an op must be an object");

        var kind = Text(op, "op");
        if (kind is null || !ops.Contains(kind))
            return PropValidation.Fail("unknown-op", index,
                $"unknown op '{kind}'; known ops are {string.Join(", ", ops.Order())}");

        switch (kind)
        {
            case "prim":
                if (Text(op, "shape") is not { } shape || !shapes.Contains(shape))
                    return PropValidation.Fail("unknown-shape", index,
                        $"unknown shape '{Text(op, "shape")}'; known shapes are "
                        + $"{string.Join(", ", shapes.Order())}");
                if (op.TryGetProperty("sides", out var sides)
                    && sides.ValueKind == JsonValueKind.Number && sides.GetInt32() < 3)
                    return PropValidation.Fail("bad-sides", index,
                        $"sides must be at least 3, got {sides.GetInt32()}");
                return Bind(op, index, names);

            case "bevel":
                return Reference(op, "on", index, names);

            case "uv":
                if (op.TryGetProperty("how", out var how) && how.ValueKind == JsonValueKind.String
                    && !projections.Contains(how.GetString()))
                    return PropValidation.Fail("unknown-uv", index,
                        $"unknown uv projection '{how.GetString()}'; known are "
                        + $"{string.Join(", ", projections)}");
                return Reference(op, "on", index, names);

            case "boolean":
                if (Text(op, "how") is not { } boolean || !booleans.Contains(boolean))
                    return PropValidation.Fail("unknown-boolean", index,
                        $"unknown boolean '{Text(op, "how")}'; known are "
                        + $"{string.Join(", ", booleans.Order())}");
                if (Text(op, "on") is { } on && on == Text(op, "with"))
                    return PropValidation.Fail("same-operands", index,
                        "a boolean cannot take the same object as both operands");
                if (Reference(op, "on", index, names) is { } badTarget) return badTarget;
                if (Reference(op, "with", index, names) is { } badCutter) return badCutter;
                names[Text(op, "with")!] = ("boolean", index);
                return null;

            case "join":
                if (!op.TryGetProperty("names", out var list)
                    || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() < 2)
                    return PropValidation.Fail("join-arity", index,
                        "join needs a list of at least two names");

                var joined = list.EnumerateArray()
                    .Select(n => n.ValueKind == JsonValueKind.String ? n.GetString() : null)
                    .ToList();
                if (joined.Any(n => n is null))
                    return PropValidation.Fail("join-arity", index, "join names must be strings");
                if (joined.Distinct().Count() != joined.Count)
                    return PropValidation.Fail("join-duplicate", index, "join names must be distinct");

                foreach (var n in joined)
                    if (Resolve(n!, index, names) is { } bad) return bad;
                foreach (var n in joined)
                    names[n!] = ("join", index);

                return Bind(op, index, names);
        }

        return null;
    }

    static PropValidation? Bind(
        JsonElement op, int index, Dictionary<string, (string By, int At)?> names)
    {
        if (Text(op, "name") is not { Length: > 0 } name)
            return PropValidation.Fail("missing-name", index,
                $"a {Text(op, "op")} needs a 'name'");

        if (names.TryGetValue(name, out var state))
            return PropValidation.Fail("name-rebound", index, state is null
                ? $"name '{name}' is already bound; names are single-assignment, so this op needs a "
                  + "fresh one"
                : $"name '{name}' was consumed by the {state.Value.By} at op {state.Value.At} and "
                  + "cannot be rebound");

        names[name] = null;
        return null;
    }

    static PropValidation? Reference(
        JsonElement op, string field, int index, Dictionary<string, (string By, int At)?> names)
    {
        if (Text(op, field) is not { Length: > 0 } name)
            return PropValidation.Fail("name-unbound", index,
                $"a {Text(op, "op")} needs a '{field}'");

        return Resolve(name, index, names);
    }

    static PropValidation? Resolve(
        string name, int index, Dictionary<string, (string By, int At)?> names)
    {
        if (!names.TryGetValue(name, out var state))
        {
            var live = names.Where(n => n.Value is null).Select(n => n.Key).Order().ToList();
            return PropValidation.Fail("name-unbound", index,
                $"name '{name}' is not bound; known names are "
                + $"{(live.Count == 0 ? "(none)" : string.Join(", ", live))}");
        }

        return state is null ? null : PropValidation.Fail("name-consumed", index,
            $"name '{name}' was consumed by the {state.Value.By} at op {state.Value.At}, so it no "
            + "longer refers to anything");
    }

    static string? Text(JsonElement op, string field) =>
        op.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    static string Trim(string s) =>
        s.Length <= 400 ? s.Trim() : string.Concat(s.AsSpan(s.Length - 400).Trim(), "");

    static void Look()
    {
        if (_looked) return;
        _looked = true;

        _rBlender = Pick(_blender, Candidates("bin",
            OperatingSystem.IsWindows()
                ? "blender-5.2.2-windows-x64/blender.exe" : "blender/blender"));
        _rScript = Pick(_script, Candidates("src", "blender/build_prop.py"));
    }

    static string? Pick(string? overridden, IEnumerable<string> candidates)
    {
        if (!string.IsNullOrWhiteSpace(overridden))
            return File.Exists(overridden) ? overridden : null;

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return null;
    }

    //: Walks up from the assembly, as the tracer and the face detector do. A test runs from its own
    //: output directory and the CLI from `bin/cli`, so neither can assume the repository root is the
    //: working directory.
    static IEnumerable<string> Candidates(string folder, string tail)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            yield return Path.Combine(dir.FullName, folder, tail.Replace('/', Path.DirectorySeparatorChar));
    }
    #endregion
}

/// <summary>Whether an op list is well formed, decided without starting anything.</summary>
public sealed class PropValidation
{
    #region Properties
    /// <summary>Whether the list would be accepted.</summary>
    public bool Ok { get; private init; }

    /// <summary>The rule that refused, matching the interpreter's own code. Null when ok.</summary>
    public string? Code { get; private init; }

    /// <summary>The op that refused, or null when the refusal is about the request as a whole.</summary>
    public int? Index { get; private init; }

    /// <summary>What was wrong, in words.</summary>
    public string Message { get; private init; } = "";

    /// <summary>What would be exported. Null when the list was refused.</summary>
    public string? Export { get; private init; }

    /// <summary>The names still live at the end, in order.</summary>
    public IReadOnlyList<string> Live { get; private init; } = [];
    #endregion

    #region Methods (internal)
    internal static PropValidation Pass(string export, IReadOnlyList<string> live) =>
        new() { Ok = true, Export = export, Live = live };

    internal static PropValidation Fail(string code, int? index, string message) =>
        new() { Ok = false, Code = code, Index = index, Message = message };
    #endregion
}

/// <summary>What a build produced, or why it did not.</summary>
/// <remarks>
/// <b><see cref="Ok"/> is the first thing to read.</b> A refused op list and a Blender that fell over
/// are both results here rather than exceptions, because a caller composing props wants to report
/// them rather than unwind.
/// </remarks>
public sealed class PropBuild
{
    #region Properties
    /// <summary>Whether a prop was produced.</summary>
    public bool Ok { get; private init; }

    /// <summary>The rule or stage that refused. Null on success.</summary>
    public string? Code { get; private init; }

    /// <summary>The op that refused, when the refusal was about one.</summary>
    public int? Index { get; private init; }

    /// <summary>What went wrong, in words. Empty on success.</summary>
    public string Error { get; private init; } = "";

    /// <summary>The GLB's full path, or null when nothing was written.</summary>
    public string? ArtifactPath { get; private init; }

    /// <summary>The GLB's size in bytes.</summary>
    public long Bytes { get; private init; }

    /// <summary>Vertices as the ARTIFACT carries them — the count that meets a ceiling.</summary>
    /// <remarks>
    /// glTF stores one attribute set per vertex, so a vertex shared by faces with different normals
    /// or UV islands is split on export: measured, three to four times the mesh count every time
    /// (a cube 8 → 24, a chair 104 → 312, a bevelled chair 992 → 3,936). <see cref="MeshGltf"/>
    /// refuses above 65,535 because the index buffer is 16-bit, and it is <i>this</i> number that
    /// reaches it — <see cref="MeshVerts"/> would understate the limit roughly fourfold.
    /// </remarks>
    public int Verts { get; private init; }

    /// <summary>Triangles in the artifact. Equal to <see cref="MeshTris"/>: splitting duplicates
    /// vertices, never faces.</summary>
    public int Tris { get; private init; }

    /// <summary>Vertices as modelled, after modifiers are evaluated.</summary>
    public int MeshVerts { get; private init; }

    /// <summary>Triangles as modelled.</summary>
    public int MeshTris { get; private init; }

    /// <summary>The name that was exported.</summary>
    public string? Exported { get; private init; }

    /// <summary>The names still live when the list ended.</summary>
    public IReadOnlyList<string> Live { get; private init; } = [];

    /// <summary>How long the interpreter took, excluding Blender's own startup.</summary>
    public int Ms { get; private init; }

    /// <summary>Blender's own output, kept for a failure that needs explaining.</summary>
    public string Diagnostics { get; internal set; } = "";
    #endregion

    #region Methods (internal)
    internal static PropBuild Refused(string code, int? index, string error) =>
        new() { Ok = false, Code = code, Index = index, Error = error };

    internal static PropBuild Parse(string json, string directory)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            return new PropBuild
            {
                Ok = false,
                Code = Str(root, "code") ?? "blender",
                Index = root.TryGetProperty("op", out var at)
                    && at.ValueKind == JsonValueKind.Number ? at.GetInt32() : null,
                Error = Str(root, "error") ?? "the build failed without saying why",
                Ms = Int(root, "ms")
            };

        var artifact = Str(root, "artifact");
        return new PropBuild
        {
            Ok = true,
            ArtifactPath = artifact is null ? null : Path.Combine(directory, artifact),
            Bytes = Int(root, "bytes"),
            Verts = Int(root, "verts"),
            Tris = Int(root, "tris"),
            MeshVerts = Int(root, "meshVerts"),
            MeshTris = Int(root, "meshTris"),
            Exported = Str(root, "exported"),
            Live = root.TryGetProperty("live", out var live) && live.ValueKind == JsonValueKind.Array
                ? [.. live.EnumerateArray().Select(n => n.GetString() ?? "")] : [],
            Ms = Int(root, "ms")
        };
    }
    #endregion

    #region Methods (private)
    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    #endregion
}
