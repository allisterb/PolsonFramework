namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkiaSharp;

/// <summary>Finds face landmarks in a bitmap, by way of a MediaPipe process.</summary>
/// <remarks>
/// <para>
/// <b>An optional backend, exactly as <see cref="BitmapTracer"/> is.</b> The studio builds and runs
/// without it; a script asks <c>Face.available</c> before committing to a route that needs it, and a
/// machine without the venv reports the capability as absent rather than failing halfway through a
/// drawing.
/// </para>
/// <para>
/// <b>Nothing is written to disk and no path crosses the boundary.</b> The picture goes in over
/// stdin as encoded bytes and JSON comes back over stdout — the same property the potrace path has,
/// and for the same reason: a script cannot name a file for this to open, so the containment rules
/// that govern <c>outFile</c> are not merely satisfied here, they are not reachable. The single
/// argument is the model bundle, and it comes from the host rather than from a script.
/// </para>
/// <para>
/// <b>Licence.</b> mediapipe is Apache 2.0 and so are all three models in the bundle — BlazeFace,
/// Face Mesh V2 and Blendshape V2, each stating it on its own model card. The card's terms are
/// <i>not</i> served beside the weights, so read the ledger row before assuming: the storage bucket
/// 404s on LICENSE and NOTICE, and the documentation page's boilerplate covers the page rather than
/// the model.
/// </para>
/// <para>
/// <b>What it does NOT do.</b> The Python task API returns normalised image coordinates and a pose
/// matrix; it does not expose the metric face mesh, which lives in the C++ geometry pipeline. Face
/// is the only landmarker with no world-space output — pose, hand and holistic all have one. The
/// landmarks do carry a per-point depth, which <see cref="FaceDetection.Mesh"/> uses, but it is
/// relative and it is the network's human-face prior: good enough to turn a face to profile, not
/// a measurement of the person, and the 2D residual was measured and does not carry identity.
/// </para>
/// </remarks>
public static class FaceDetector
{
    #region Fields
    //: Set by the host from `Tools:FacePython`, `Tools:FaceScript` and `Tools:FaceModel` when they
    //: are configured. The engine reads no environment variables by design, so an override arrives
    //: through settings exactly as `Assets:Budget` does.
    static string? _python, _script, _model;
    static string? _rPython, _rScript, _rModel;
    static bool _looked;
    static ushort[]? _triangles;
    static SKPoint[]? _uvs;
    #endregion

    #region Properties
    /// <summary>An explicit path to the interpreter, or null to discover one.</summary>
    public static string? PythonOverride
    {
        get => _python;
        set { _python = value; _looked = false; }
    }

    /// <summary>An explicit path to the script, or null to discover one.</summary>
    public static string? ScriptOverride
    {
        get => _script;
        set { _script = value; _looked = false; }
    }

    /// <summary>An explicit path to the model bundle, or null to discover one.</summary>
    public static string? ModelOverride
    {
        get => _model;
        set { _model = value; _looked = false; }
    }

    /// <summary>The interpreter that will be used, or null when none was found.</summary>
    public static string? Python { get { Look(); return _rPython; } }

    /// <summary>The script that will be used, or null when none was found.</summary>
    public static string? Script { get { Look(); return _rScript; } }

    /// <summary>The model bundle that will be used, or null when none was found.</summary>
    public static string? Model { get { Look(); return _rModel; } }

    /// <summary>Whether detection can be done at all here.</summary>
    public static bool Available => Python is not null && Script is not null && Model is not null;

    /// <summary>What is missing, for a caller that wants to say so rather than only fail.</summary>
    public static string? Missing
    {
        get
        {
            Look();
            List<string> gaps = [];
            if (_rPython is null) gaps.Add("the python-mediapipe interpreter");
            if (_rScript is null) gaps.Add("src/vision/face_landmarks.py");
            if (_rModel is null) gaps.Add("models/face_landmarker.task");
            return gaps.Count == 0 ? null : string.Join(", ", gaps);
        }
    }
    #endregion

    #region Methods
    /// <summary>Detects one face, or reports that there was none.</summary>
    /// <remarks>
    /// <b>The scale window is handled here rather than left to the caller</b>, because it is the
    /// single thing most likely to waste somebody's afternoon: a face filling the whole frame is not
    /// detected at all, and a trimmed <c>Assets.cutout</c> cell is always exactly that. The script
    /// pads and retries, then subtracts the offset so the landmarks come back in the bitmap's own
    /// pixels. Nothing announces the original failure, which is why it is not optional.
    /// </remarks>
    public static FaceDetection Detect(SkiaBitmapWrapper bitmap, int timeoutMs = 60000)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!Available)
            throw new InvalidOperationException(
                $"Face detection needs {Missing}. Create the venv and install "
                + "src/vision/requirements.lock.txt, fetch the model into models/, or set "
                + "'Tools:FacePython', 'Tools:FaceScript' and 'Tools:FaceModel' in appsettings.json.");

        using var data = bitmap.Bitmap.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("The bitmap could not be encoded for detection.");

        var info = new ProcessStartInfo(Python!)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(Script!);
        info.ArgumentList.Add(Model!);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("The face detection process would not start.");

        using (var stdin = process.StandardInput.BaseStream)
            data.AsStream().CopyTo(stdin);

        var json = process.StandardOutput.ReadToEnd();
        var err = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException($"Face detection did not finish within {timeoutMs} ms.");
        }

        if (process.ExitCode != 0 || json.Length == 0)
            throw new InvalidOperationException(
                $"Face detection failed (exit {process.ExitCode}). {Trim(err)}");

        return FaceDetection.Parse(json);
    }

    /// <summary>The canonical face mesh's 898 triangles, read once from the model bundle itself.</summary>
    /// <remarks>
    /// <b>The topology travels with the weights, so nothing else has to be installed.</b> The
    /// <c>.task</c> bundle is a zip, and its <c>geometry_pipeline_metadata_landmarks.binarypb</c>
    /// carries the canonical mesh — 468 vertices of x, y, z, u, v and 2,694 indices — which is the
    /// same topology, winding included, as MediaPipe's <c>canonical_face_model.obj</c>. Read here
    /// with a minimal protobuf walk, because the 1.0 Python API has no protobuf dependency and this
    /// needs two fields of one message.
    /// </remarks>
    internal static ushort[] Triangles() => Canonical().Triangles;

    /// <summary>The canonical mesh's own texture coordinates, one per vertex, stored v-up as an OBJ writes them.</summary>
    /// <remarks>
    /// The bundle measures <c>v</c> from the top of the atlas and <see cref="FaceMesh"/> stores it
    /// from the bottom, so it is flipped here — checked against <c>canonical_face_model.obj</c>, whose
    /// <c>vt</c> is exactly one minus the bundle's. The atlas has no seams: 468 coordinates for 468
    /// vertices.
    /// </remarks>
    internal static SKPoint[] CanonicalUvs() => Canonical().Uvs;

    static (ushort[] Triangles, SKPoint[] Uvs) Canonical()
    {
        if (_triangles is not null && _uvs is not null) return (_triangles, _uvs);
        if (Model is null)
            throw new InvalidOperationException($"The face mesh's triangles come from the model bundle, which is missing: {Missing}.");

        using var zip = System.IO.Compression.ZipFile.OpenRead(Model);
        var entry = zip.GetEntry("geometry_pipeline_metadata_landmarks.binarypb")
            ?? throw new InvalidOperationException($"'{Model}' carries no canonical mesh, so the face has no triangles.");
        using var ms = new MemoryStream();
        using (var stream = entry.Open()) stream.CopyTo(ms);
        var bytes = ms.ToArray();

        // GeometryPipelineMetadata.canonical_mesh (1) -> Mesh3d.index_buffer (4), unpacked varints.
        var mesh = Fields(bytes, 0, bytes.Length).FirstOrDefault(f => f.Number == 1 && f.Wire == 2);
        if (mesh.Length == 0) throw new InvalidOperationException($"'{Model}' has no canonical mesh message.");
        // Mesh3d.vertex_buffer (3) is x, y, z, u, v per vertex as unpacked floats; index_buffer (4) is varints.
        List<ushort> idx = [];
        List<float> buffer = [];
        foreach (var f in Fields(bytes, mesh.Offset, mesh.Offset + mesh.Length))
        {
            if (f.Number == 3 && f.Wire == 5) buffer.Add(BitConverter.ToSingle(bytes, f.Offset));
            else if (f.Number == 3 && f.Wire == 2)
                for (var i = f.Offset; i + 4 <= f.Offset + f.Length; i += 4) buffer.Add(BitConverter.ToSingle(bytes, i));
            else if (f.Number == 4 && f.Wire == 0) idx.Add((ushort)f.Value);
            else if (f.Number == 4 && f.Wire == 2)
                for (var i = f.Offset; i < f.Offset + f.Length;) idx.Add((ushort)Varint(bytes, ref i));
        }

        if (idx.Count == 0 || idx.Count % 3 != 0)
            throw new InvalidOperationException($"The canonical mesh in '{Model}' has {idx.Count} indices, which is not a triangle list.");
        if (buffer.Count % 5 != 0)
            throw new InvalidOperationException($"The canonical mesh in '{Model}' has {buffer.Count} vertex floats, which is not x, y, z, u, v per vertex.");

        var uvs = new SKPoint[buffer.Count / 5];
        for (var v = 0; v < uvs.Length; v++) uvs[v] = new SKPoint(buffer[(v * 5) + 3], 1f - buffer[(v * 5) + 4]);
        _uvs = uvs;
        _triangles = [.. idx];
        return (_triangles, _uvs);
    }

    static IEnumerable<(int Number, int Wire, ulong Value, int Offset, int Length)> Fields(byte[] b, int start, int end)
    {
        var i = start;
        while (i < end)
        {
            var key = Varint(b, ref i);
            int number = (int)(key >> 3), wire = (int)(key & 7);
            switch (wire)
            {
                case 0:
                    yield return (number, wire, Varint(b, ref i), 0, 0);
                    break;
                case 1: i += 8; break;
                case 5:
                    yield return (number, wire, 0UL, i, 4);
                    i += 4;
                    break;
                case 2:
                    var n = (int)Varint(b, ref i);
                    yield return (number, wire, 0UL, i, n);
                    i += n;
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected protobuf wire type {wire} in the model bundle.");
            }
        }
    }

    static ulong Varint(byte[] b, ref int i)
    {
        ulong r = 0;
        for (var shift = 0; ; shift += 7)
        {
            var c = b[i++];
            r |= (ulong)(c & 0x7F) << shift;
            if (c < 0x80) return r;
        }
    }

    static string Trim(string s) =>
        s.Length <= 400 ? s.Trim() : string.Concat(s.AsSpan(0, 400).Trim(), "…");

    static void Look()
    {
        if (_looked) return;
        _looked = true;

        _rPython = Pick(_python, Candidates("python-mediapipe",
            OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python"));
        _rScript = Pick(_script, Candidates("src", "vision/face_landmarks.py"));
        _rModel = Pick(_model, Candidates("models", "face_landmarker.task"));
    }

    static string? Pick(string? overridden, IEnumerable<string> candidates)
    {
        if (!string.IsNullOrWhiteSpace(overridden))
            return File.Exists(overridden) ? overridden : null;

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return null;
    }

    //: Walks up from the assembly, as the tracer does for `bin/`. A test runs from its own output
    //: directory and the CLI from `bin/cli`, so neither can assume the repository root is the
    //: working directory.
    static IEnumerable<string> Candidates(string folder, string tail)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            yield return Path.Combine(dir.FullName, folder, tail.Replace('/', Path.DirectorySeparatorChar));
    }
    #endregion
}

/// <summary>What the detector found, in the bitmap's own pixels.</summary>
/// <remarks>
/// <b>Not finding a face is a result rather than an error</b>, so <see cref="Found"/> is the first
/// thing to read. A portrait can be perfectly good and still fall outside the detector's scale
/// window in the other direction — too small in frame — which padding cannot fix and this reports.
/// </remarks>
public sealed class FaceDetection
{
    #region Properties
    /// <summary>Whether a face was found at all.</summary>
    public bool Found { get; private init; }

    /// <summary>Why not, when it was not.</summary>
    public string? Reason { get; private init; }

    /// <summary>The image's own dimensions.</summary>
    public int Width { get; private init; }

    /// <summary>The image's own dimensions.</summary>
    public int Height { get; private init; }

    /// <summary>How many pixels of padding the detection needed. Zero for an ordinary photograph.</summary>
    public int Pad { get; private init; }

    /// <summary>How many landmarks were returned. 478 with this bundle's iris refinement.</summary>
    /// <remarks>
    /// The base mesh is the first <b>468</b>; the last ten are five iris points per eye, which sit
    /// inside the eye and carry nothing about pose. A canonical OBJ has 468, so anything pairing
    /// the two must truncate rather than assume they match.
    /// </remarks>
    public int Count => Points.Count;

    /// <summary>The rectangle the landmarks occupy, in the bitmap's own pixels.</summary>
    public Dictionary<string, object?> Bounds
    {
        get
        {
            if (Points.Count == 0) return Rect(0f, 0f, 0f, 0f);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in Points)
            {
                x0 = MathF.Min(x0, p.X); y0 = MathF.Min(y0, p.Y);
                x1 = MathF.Max(x1, p.X); y1 = MathF.Max(y1, p.Y);
            }

            return Rect(x0, y0, x1 - x0, y1 - y0);
        }
    }

    internal IReadOnlyList<SKPoint> Points { get; private init; } = [];

    /// <summary>Per-landmark depth in the image's own pixels, positive toward the viewer; empty from an older backend.</summary>
    internal IReadOnlyList<float> Depths { get; private init; } = [];

    /// <summary>Whether the landmarks carry depth, so <see cref="Mesh"/> can build a face that turns.</summary>
    public bool HasDepth => Depths.Count == Points.Count && Points.Count > 0;

    /// <summary>The source's head pose, read off the transformation matrix. Pose only, no shape.</summary>
    public float YawDeg { get; private init; }

    /// <summary>The source's head pose, read off the transformation matrix. Pose only, no shape.</summary>
    public float PitchDeg { get; private init; }

    /// <summary>The source's head pose, read off the transformation matrix. Pose only, no shape.</summary>
    public float RollDeg { get; private init; }

    /// <summary>The 51 ARKit-named blendshape coefficients, as the model read them.</summary>
    public IReadOnlyDictionary<string, float> Blendshapes { get; private init; }
        = new Dictionary<string, float>();
    #endregion

    #region Methods
    /// <summary>One landmark by index, or null when it is out of range.</summary>
    /// <remarks>
    /// <b>Pair this with <c>mesh.landmark(x, y, z)</c> rather than with a remembered number.</b>
    /// Detected landmark <c>i</c> corresponds to canonical vertex <c>i</c>, so the index is found by
    /// asking the mesh in hand where its own eye is, and that index is then read out of here.
    /// </remarks>
    public Dictionary<string, object?>? At(int index)
    {
        if (index < 0 || index >= Points.Count) return null;
        var p = new Dictionary<string, object?>
        {
            ["x"] = Points[index].X, ["y"] = Points[index].Y, ["index"] = index
        };
        if (HasDepth) p["z"] = Depths[index];
        return p;
    }

    /// <summary>
    /// The face as a 3D mesh fitted to the image it was found in, textured by that image when given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A turnable face from any portrait, drawn or photographed.</b> The 468 base landmarks become
    /// vertices in the image's own pixels — centred on the face, y up, depth toward the viewer — on
    /// MediaPipe's canonical topology, and each samples the pixel it was found at. So the front view
    /// reproduces the portrait, and turning it shows a nose, brow and chin that the depth supplies.
    /// </para>
    /// <para>
    /// <b>The depth is the network's human prior, not a measurement.</b> It gives a plausible human
    /// profile; a heavily stylised face regresses toward human proportions, as every MediaPipe output
    /// does. The texture comes from one front view, so the far side stretches as the face turns past
    /// about 60°. And it is a face, not a head: no ears, no hair, no back.
    /// </para>
    /// <para>
    /// The topology is the one <c>Mesh.draw</c>'s expression units were written for, so
    /// <c>expression</c> and <c>side</c> work on it directly.
    /// </para>
    /// </remarks>
    public FaceMesh Mesh(object? image = null) => MeshWith(FaceDetector.Triangles, image);

    /// <summary><see cref="Mesh"/> with the topology supplied, so it is testable without the model bundle.</summary>
    internal FaceMesh MeshWith(Func<ushort[]> triangles, object? image)
    {
        if (!Found)
            throw new InvalidOperationException($"No face was found, so there is no mesh to build: {Reason}");
        if (!HasDepth)
            throw new InvalidOperationException(
                "These landmarks carry no depth: the face backend predates it. Update src/vision/face_landmarks.py, "
                + "which now returns a third value per landmark.");

        const int N = 468;
        if (Points.Count < N)
            throw new InvalidOperationException($"A face mesh needs {N} landmarks and this detection has {Points.Count}.");

        SKBitmap? texture = null;
        if (image is not null)
        {
            texture = MeshToolkit.AsBitmap(image);
            if (MathF.Abs(((float)texture.Width / texture.Height) - ((float)Width / Height)) > 0.01f)
                throw new ArgumentException(
                    $"The texture is {texture.Width}x{texture.Height} and the face was found in a {Width}x{Height} image. "
                    + "Pass the image the face was detected in, or one scaled from it.", nameof(image));
        }

        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (var i = 0; i < N; i++)
        {
            x0 = MathF.Min(x0, Points[i].X); y0 = MathF.Min(y0, Points[i].Y);
            x1 = MathF.Max(x1, Points[i].X); y1 = MathF.Max(y1, Points[i].Y);
        }

        float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
        var verts = new SKPoint3[N];
        var uvs = new SKPoint[N];
        for (var i = 0; i < N; i++)
        {
            verts[i] = new SKPoint3(Points[i].X - cx, cy - Points[i].Y, Depths[i]);
            uvs[i] = new SKPoint(Points[i].X / Width, 1f - (Points[i].Y / Height));   // atlas, v up
        }

        return new FaceMesh(verts, uvs, triangles(), true, "detected face") { Texture = texture };
    }

    static Dictionary<string, object?> Rect(float x, float y, float w, float h) => new()
    {
        ["x"] = x, ["y"] = y, ["width"] = w, ["height"] = h,
        ["x2"] = x + w, ["y2"] = y + h, ["cx"] = x + (w / 2f), ["cy"] = y + (h / 2f)
    };

    internal static FaceDetection Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.GetProperty("found").GetBoolean())
            return new FaceDetection
            {
                Found = false,
                Reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null,
                Width = root.GetProperty("width").GetInt32(),
                Height = root.GetProperty("height").GetInt32()
            };

        List<SKPoint> pts = [];
        List<float> depths = [];
        foreach (var p in root.GetProperty("landmarks").EnumerateArray())
        {
            pts.Add(new SKPoint(p[0].GetSingle(), p[1].GetSingle()));
            if (p.GetArrayLength() > 2) depths.Add(p[2].GetSingle());
        }

        Dictionary<string, float> shapes = new(StringComparer.Ordinal);
        if (root.TryGetProperty("blendshapes", out var bs))
            foreach (var kv in bs.EnumerateObject()) shapes[kv.Name] = kv.Value.GetSingle();

        float Angle(string name) =>
            root.TryGetProperty(name, out var v) ? v.GetSingle() : 0f;

        return new FaceDetection
        {
            Found = true,
            Width = root.GetProperty("width").GetInt32(),
            Height = root.GetProperty("height").GetInt32(),
            Pad = root.TryGetProperty("pad", out var pd) ? pd.GetInt32() : 0,
            Points = pts,
            Depths = depths.Count == pts.Count ? depths : [],
            YawDeg = Angle("yawDeg"),
            PitchDeg = Angle("pitchDeg"),
            RollDeg = Angle("rollDeg"),
            Blendshapes = shapes
        };
    }

    /// <summary>A one-line summary, for a log or a stage note.</summary>
    public override string ToString() => Found
        ? string.Create(CultureInfo.InvariantCulture,
            $"{Points.Count} landmarks, yaw {YawDeg:F1} pitch {PitchDeg:F1} roll {RollDeg:F1}, pad {Pad}px")
        : $"no face: {Reason}";
    #endregion
}
