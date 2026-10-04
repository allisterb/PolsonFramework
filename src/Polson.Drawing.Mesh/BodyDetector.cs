namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SkiaSharp;

/// <summary>Finds a body's 33 landmarks in a bitmap, by way of a MediaPipe process.</summary>
/// <remarks>
/// <para>
/// <b>The same optional backend as <see cref="FaceDetector"/></b>, and the same interpreter: the picture goes
/// in over stdin, JSON comes back over stdout, nothing is written to disk and no path crosses the boundary.
/// It runs <c>src/vision/pose_landmarks.py</c> against <c>models/pose_landmarker_heavy.task</c>, or the smaller
/// <c>pose_landmarker_full.task</c> where Heavy is absent. Measured on a round trip, Heavy's mean limb error was
/// 20 degrees against Full's 23, and it read an upright figure as closer to upright; both take about 3 seconds.
/// </para>
/// <para>
/// <b>Why pose and not only face.</b> Pose returns its landmarks twice: in the image's pixels, and as metric
/// 3D points centred on the hips. The 3D set is what lets a pose be read off a picture with its depth — an arm
/// pointing at the camera, a body turned three-quarters — rather than flattened onto the page.
/// </para>
/// <para>
/// <b>The one pose detector.</b> <c>Character.detect</c> reads a pose off a picture with it, and the character
/// generator names a rig's joints and finds each view's head with it, so both use the same model.
/// </para>
/// <para>
/// <b>Licence.</b> mediapipe is Apache 2.0, and so is the pose landmarker, per its model card; as with the face
/// bundle, the terms are on the card rather than beside the weights.
/// </para>
/// </remarks>
public static class BodyDetector
{
    #region Fields
    static string? _script, _model;
    static string? _rScript, _rModel;
    static bool _looked;
    #endregion

    #region Properties
    /// <summary>An explicit path to the script, or null to discover one.</summary>
    public static string? ScriptOverride
    {
        get => _script;
        set { _script = value; _looked = false; }
    }

    /// <summary>An explicit path to the pose model, or null to discover one.</summary>
    public static string? ModelOverride
    {
        get => _model;
        set { _model = value; _looked = false; }
    }

    /// <summary>The script that will be used, or null when none was found.</summary>
    public static string? Script { get { Look(); return _rScript; } }

    /// <summary>The model that will be used, or null when none was found.</summary>
    public static string? Model { get { Look(); return _rModel; } }

    /// <summary>Whether detection can be done at all here.</summary>
    public static bool Available => FaceDetector.Python is not null && Script is not null && Model is not null;

    /// <summary>What is missing, or null when nothing is.</summary>
    public static string? Missing
    {
        get
        {
            Look();
            List<string> gaps = [];
            if (FaceDetector.Python is null) gaps.Add("the python-mediapipe interpreter");
            if (_rScript is null) gaps.Add("src/vision/pose_landmarks.py");
            if (_rModel is null) gaps.Add("models/pose_landmarker_heavy.task (or _full)");
            return gaps.Count == 0 ? null : string.Join(", ", gaps);
        }
    }
    #endregion

    #region Methods
    /// <summary>Detects one body, or reports that there was none.</summary>
    public static BodyDetection Detect(SkiaBitmapWrapper bitmap, int timeoutMs = 60000)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return Detect(bitmap.Bitmap, timeoutMs);
    }

    /// <summary>Detects one body in a raw bitmap, or reports that there was none.</summary>
    public static BodyDetection Detect(SKBitmap bitmap, int timeoutMs = 60000)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!Available)
            throw new InvalidOperationException(
                $"Body detection needs {Missing}. Create the venv and install src/vision/requirements.lock.txt, " +
                "fetch the pose model into models/, or set 'Tools:PoseScript' and 'Tools:PoseModel' in appsettings.json.");

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("The bitmap could not be encoded for detection.");

        var info = new ProcessStartInfo(FaceDetector.Python!)
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
            ?? throw new InvalidOperationException("The body detection process would not start.");
        using (var stdin = process.StandardInput.BaseStream)
            data.AsStream().CopyTo(stdin);

        var json = process.StandardOutput.ReadToEnd();
        var err = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException($"Body detection did not finish within {timeoutMs} ms.");
        }
        if (process.ExitCode != 0 || json.Length == 0)
            throw new InvalidOperationException($"Body detection failed (exit {process.ExitCode}). {FaceDetector.Trim(err)}");

        return BodyDetection.Parse(json);
    }

    static void Look()
    {
        if (_looked) return;
        _looked = true;
        _rScript = FaceDetector.Pick(_script, FaceDetector.Candidates("src", "vision/pose_landmarks.py"));
        _rModel = FaceDetector.Pick(_model, FaceDetector.Candidates("models", "pose_landmarker_heavy.task"))
                  ?? FaceDetector.Pick(_model, FaceDetector.Candidates("models", "pose_landmarker_full.task"));
    }
    #endregion
}

/// <summary>A body the detector found: its 33 landmarks in the image's pixels, and again in metres in 3D.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling
/// onto them. <b>Not finding a body is a result rather than an error</b>, so read <see cref="Found"/> first.
/// </remarks>
public sealed class BodyDetection : ILandmarkSource
{
    #region Properties
    /// <summary>Whether a body was found.</summary>
    public bool Found { get; private init; }

    /// <summary>Why not, when it was not.</summary>
    public string? Reason { get; private init; }

    /// <summary>The image's own width.</summary>
    public int Width { get; private init; }

    /// <summary>The image's own height.</summary>
    public int Height { get; private init; }

    /// <summary>How many pixels of padding the detection needed.</summary>
    public int Pad { get; private init; }

    /// <summary>The landmark names, MediaPipe's 33 in its order: <c>nose</c>, <c>leftShoulder</c>, <c>rightHeel</c>…</summary>
    public string[] Names => [.. Image.Keys];

    /// <summary>Whether the 3D landmarks came back, which <c>Character.retarget</c> needs.</summary>
    public bool HasWorld => World3.Count > 0;

    /// <summary>
    /// The limb and head landmarks the model was not sure of (visibility under 0.5): hidden, out of frame, or
    /// guessed. A retarget leaves the limb they belong to as its parent carries it.
    /// </summary>
    public string[] Unsure => [.. Image.Where(kv => Used.Contains(kv.Key) && kv.Value.V < Sure).Select(kv => kv.Key)];

    internal IReadOnlyDictionary<string, (float X, float Y, float V)> Image { get; private init; }
        = new Dictionary<string, (float, float, float)>();

    /// <summary>The 3D landmarks as MediaPipe gives them: metres, centred on the hips, x right and y down in the image, z away from the camera.</summary>
    internal IReadOnlyDictionary<string, Vector3> World3 { get; private init; } = new Dictionary<string, Vector3>();
    #endregion

    #region Methods
    /// <summary>One landmark in the image's pixels, <c>{ x, y, visibility }</c>, or null for a name there is not.</summary>
    public Dictionary<string, object?>? At(string name) =>
        Image.TryGetValue(name, out var p)
            ? new Dictionary<string, object?> { ["x"] = p.X, ["y"] = p.Y, ["visibility"] = p.V }
            : null;

    /// <summary>
    /// One landmark in 3D, <c>{ x, y, z }</c> in metres from the middle of the hips, as MediaPipe gives it: x to the
    /// image's right, y down, z away from the camera. Null for a name there is not, or when no 3D set came back.
    /// </summary>
    public Dictionary<string, object?>? World(string name) =>
        World3.TryGetValue(name, out var p)
            ? new Dictionary<string, object?> { ["x"] = p.X, ["y"] = p.Y, ["z"] = p.Z }
            : null;

    /// <summary>A landmark in the image's pixels, if the model is at least this sure it is in view.</summary>
    internal SKPoint? Point(string name, float minVisibility = 0f) =>
        Image.TryGetValue(name, out var p) && p.V >= minVisibility ? new SKPoint(p.X, p.Y) : null;

    /// <summary>The model's visibility for a landmark, or 0 for a name there is not.</summary>
    internal float Visibility(string name) => Image.TryGetValue(name, out var p) ? p.V : 0f;

    int ILandmarkSource.SourceWidth => Width;

    int ILandmarkSource.SourceHeight => Height;

    bool ILandmarkSource.TryGetLandmark(string name, out double x, out double y, out double visibility)
    {
        var found = Image.TryGetValue(name, out var p);
        (x, y, visibility) = found ? (p.X, p.Y, p.V) : (0d, 0d, 0d);
        return found;
    }

    /// <summary>A one-line summary, for a log or a stage note.</summary>
    public override string ToString() => Found
        ? string.Create(CultureInfo.InvariantCulture,
            $"{Image.Count} landmarks{(HasWorld ? " with 3D" : "")}, pad {Pad}px{(Unsure.Length > 0 ? ", unsure of " + string.Join(", ", Unsure) : "")}")
        : $"no body: {Reason}";

    internal static BodyDetection Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        int w = root.GetProperty("width").GetInt32(), h = root.GetProperty("height").GetInt32();
        if (!root.GetProperty("found").GetBoolean())
            return new BodyDetection
            {
                Found = false, Width = w, Height = h,
                Reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null
            };

        var image = new Dictionary<string, (float, float, float)>(StringComparer.Ordinal);
        foreach (var p in root.GetProperty("landmarks").EnumerateObject())
            image[p.Name] = (p.Value.GetProperty("x").GetSingle(), p.Value.GetProperty("y").GetSingle(),
                             p.Value.TryGetProperty("v", out var v) ? v.GetSingle() : 0f);

        var world = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        if (root.TryGetProperty("worldLandmarks", out var wl))
            foreach (var p in wl.EnumerateObject())
                world[p.Name] = new Vector3(p.Value.GetProperty("x").GetSingle(), p.Value.GetProperty("y").GetSingle(),
                                            p.Value.GetProperty("z").GetSingle());

        return new BodyDetection
        {
            Found = true, Width = w, Height = h,
            Pad = root.TryGetProperty("pad", out var pd) ? pd.GetInt32() : 0,
            Image = image, World3 = world
        };
    }

    /// <summary>The visibility under which a landmark is not trusted.</summary>
    internal const float Sure = 0.5f;

    /// <summary>The landmarks a retarget reads.</summary>
    internal static readonly HashSet<string> Used = new(StringComparer.Ordinal)
    {
        "nose", "leftEar", "rightEar",
        "leftShoulder", "rightShoulder", "leftElbow", "rightElbow", "leftWrist", "rightWrist",
        "leftPinky", "rightPinky", "leftIndex", "rightIndex",
        "leftHip", "rightHip", "leftKnee", "rightKnee", "leftAnkle", "rightAnkle",
        "leftHeel", "rightHeel", "leftFootIndex", "rightFootIndex"
    };
    #endregion
}
