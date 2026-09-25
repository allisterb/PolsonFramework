namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SkiaSharp;

/// <summary>
/// Finds a body in an image and reports where its joints are: MediaPipe's 33 pose landmarks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not exposed to scripts.</b> The character generator uses it twice: to name a generated rig's
/// joints, by detecting the body in a front render and matching each landmark to the nearest bone;
/// and to find the head in each view of a turnaround, so the face can be built from crops the
/// face detector will accept.
/// </para>
/// <para>
/// Invoked exactly as <see cref="FaceDetector"/> is — a separate process in the same
/// <c>python-mediapipe</c> venv, bytes in over a pipe and JSON out, nothing written to disk — and it
/// shares that detector's interpreter. The script is <c>src/vision/pose_landmarks.py</c> and the
/// model <c>models/pose_landmarker_full.task</c>, both Apache 2.0.
/// </para>
/// </remarks>
public static class PoseDetector
{
    #region Properties
    /// <summary>An explicit path to the script, or null to discover one.</summary>
    public static string? ScriptOverride { get => _script; set { _script = value; _looked = false; } }

    /// <summary>An explicit path to the model, or null to discover one.</summary>
    public static string? ModelOverride { get => _model; set { _model = value; _looked = false; } }

    public static string? Script { get { Look(); return _rScript; } }

    public static string? Model { get { Look(); return _rModel; } }

    /// <summary>Whether pose detection can be done here. Needs the face detector's interpreter too.</summary>
    public static bool Available => FaceDetector.Python is not null && Script is not null && Model is not null;

    /// <summary>What is missing, or null.</summary>
    public static string? Missing
    {
        get
        {
            Look();
            List<string> gaps = [];
            if (FaceDetector.Python is null) gaps.Add("the python-mediapipe interpreter");
            if (_rScript is null) gaps.Add("src/vision/pose_landmarks.py");
            if (_rModel is null) gaps.Add("models/pose_landmarker_full.task");
            return gaps.Count == 0 ? null : string.Join(", ", gaps);
        }
    }
    #endregion

    #region Methods
    /// <summary>Detects one body, or reports that there was none.</summary>
    public static PoseDetection Detect(SKBitmap bitmap, int timeoutMs = 60000)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!Available)
            throw new InvalidOperationException(
                $"Pose detection needs {Missing}. See src/vision/requirements.in, and fetch the model into models/.");

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("The bitmap could not be encoded for pose detection.");

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
            ?? throw new InvalidOperationException("The pose detection process would not start.");
        using (var stdin = process.StandardInput.BaseStream)
            data.AsStream().CopyTo(stdin);

        var json = process.StandardOutput.ReadToEnd();
        var err = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException($"Pose detection did not finish within {timeoutMs} ms.");
        }

        if (process.ExitCode != 0 || json.Length == 0)
            throw new InvalidOperationException($"Pose detection failed (exit {process.ExitCode}). {FaceDetector.Trim(err)}");

        return PoseDetection.Parse(json);
    }
    #endregion

    #region Private
    static void Look()
    {
        if (_looked) return;
        _looked = true;
        _rScript = FaceDetector.Pick(_script, FaceDetector.Candidates("src", "vision/pose_landmarks.py"));
        _rModel = FaceDetector.Pick(_model, FaceDetector.Candidates("models", "pose_landmarker_full.task"));
    }

    static string? _script, _model, _rScript, _rModel;
    static bool _looked;
    #endregion
}

/// <summary>What the pose detector found, in the image's own pixels.</summary>
public sealed class PoseDetection
{
    #region Properties
    public bool Found { get; private init; }

    public string? Reason { get; private init; }

    public int Width { get; private init; }

    public int Height { get; private init; }

    /// <summary>Each named landmark: position in pixels, and the model's visibility confidence.</summary>
    public IReadOnlyDictionary<string, PoseLandmark> Landmarks { get; private init; } = new Dictionary<string, PoseLandmark>();
    #endregion

    #region Methods
    /// <summary>A landmark if it was found and the model is at least this confident it is in view.</summary>
    public PoseLandmark? At(string name, float minVisibility = 0f) =>
        Landmarks.TryGetValue(name, out var p) && p.Visibility >= minVisibility ? p : null;

    internal static PoseDetection Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var found = root.TryGetProperty("found", out var f) && f.GetBoolean();
        var result = new PoseDetection
        {
            Found = found,
            Reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null,
            Width = root.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
            Height = root.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
        };
        if (!found || !root.TryGetProperty("landmarks", out var lms)) return result;

        var map = new Dictionary<string, PoseLandmark>(StringComparer.Ordinal);
        foreach (var p in lms.EnumerateObject())
            map[p.Name] = new PoseLandmark(p.Value.GetProperty("x").GetSingle(), p.Value.GetProperty("y").GetSingle(),
                                           p.Value.TryGetProperty("v", out var v) ? v.GetSingle() : 0f);
        return new PoseDetection { Found = true, Width = result.Width, Height = result.Height, Landmarks = map };
    }

    public override string ToString() =>
        Found ? string.Create(CultureInfo.InvariantCulture, $"pose: {Landmarks.Count} landmarks in {Width}x{Height}")
              : $"pose: none ({Reason})";
    #endregion
}

/// <summary>One landmark, in pixels, with the model's confidence that it is visible.</summary>
public readonly record struct PoseLandmark(float X, float Y, float Visibility)
{
    public SKPoint Point => new(X, Y);
}
