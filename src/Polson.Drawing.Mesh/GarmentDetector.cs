namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using SkiaSharp;

/// <summary>
/// The garment step of a character build: whether anything hangs between the legs, and which of the mesh's
/// vertices are garment. Runs <c>src/vision/garments.py</c> in the face venv's interpreter.
/// </summary>
/// <remarks>
/// <para>
/// <b>One process per character</b>, so the pose model and SAM 2 each load once. The views and the mesh go in over
/// stdin and JSON comes back over stdout; nothing is written to disk and no path crosses the boundary, as for
/// <see cref="BodyDetector"/>.
/// </para>
/// <para>
/// <b>It answers "does it need cloth?" first</b>, from the silhouette alone: trousers or bare legs leave background
/// between the thighs of a front or back view, and anything hanging fills it. When nothing hangs it stops there,
/// in about 5 s. When something does, SAM 2 takes the upper garment and whatever covers the thighs on every view,
/// and the masks are projected onto the mesh, about 30 to 50 s on CPU. See
/// <c>docs/internal/character-rigging-modes.md</c> §7.
/// </para>
/// <para>
/// <b>Licences.</b> SAM 2's checkpoint is Apache 2.0; the pose landmarker is Apache 2.0 per its model card.
/// </para>
/// </remarks>
public static class GarmentDetector
{
    #region Fields
    static string? _script, _model;
    static string? _rScript, _rModel;
    static bool _looked;
    #endregion

    #region Properties
    /// <summary>An explicit path to <c>garments.py</c>, or null to discover one.</summary>
    public static string? ScriptOverride
    {
        get => _script;
        set { _script = value; _looked = false; }
    }

    /// <summary>An explicit path to the SAM 2 checkpoint, or null to discover one.</summary>
    public static string? ModelOverride
    {
        get => _model;
        set { _model = value; _looked = false; }
    }

    /// <summary>The script that will be used, or null when none was found.</summary>
    public static string? Script { get { Look(); return _rScript; } }

    /// <summary>The SAM 2 checkpoint that will be used, or null when none was found.</summary>
    public static string? Model { get { Look(); return _rModel; } }

    /// <summary>Whether the garment step can run here: the face venv, the pose model, the script and SAM 2.</summary>
    public static bool Available => BodyDetector.Available && Script is not null && Model is not null;

    /// <summary>What is missing, or null when nothing is.</summary>
    public static string? Missing
    {
        get
        {
            Look();
            List<string> gaps = [];
            if (BodyDetector.Missing is { } body) gaps.Add(body);
            if (_rScript is null) gaps.Add("src/vision/garments.py");
            if (_rModel is null) gaps.Add("models/sam2.1_hiera_small.pt (tools/bootstrap.py --extras)");
            return gaps.Count == 0 ? null : string.Join(", ", gaps);
        }
    }
    #endregion

    #region Methods
    /// <summary>Runs the garment step on a character's views and its rigged mesh at rest.</summary>
    /// <param name="views">By name: <c>front</c> (required), <c>back</c>, <c>side</c>, <c>left</c>, <c>right</c>.</param>
    /// <param name="body">The mesh whose vertices are labelled, or null to only answer whether anything hangs.</param>
    /// <param name="always">Segment even when nothing hangs, for the sleeve and hem readings.</param>
    public static GarmentResult Detect(IReadOnlyDictionary<string, SKBitmap> views, FaceMesh? body, bool always = false,
                                       int timeoutMs = 600000)
    {
        ArgumentNullException.ThrowIfNull(views);
        if (!Available)
            throw new InvalidOperationException(
                $"The garment step needs {Missing}. Install src/vision/requirements.lock.txt into python-mediapipe/ and run tools/bootstrap.py --extras.");
        if (!views.ContainsKey("front"))
            throw new ArgumentException("The garment step needs a front view.", nameof(views));

        var request = new JsonObject
        {
            ["views"] = new JsonObject(views.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)Base64Png(kv.Value)))),
            ["segment"] = always ? "always" : "auto"
        };
        if (body is not null)
        {
            request["positions"] = new JsonArray([.. body.Vertices.Select(v => (JsonNode)new JsonArray(v.X, v.Y, v.Z))]);
            var tris = body.Indices;
            request["triangles"] = new JsonArray([.. Enumerable.Range(0, tris.Length / 3)
                .Select(t => (JsonNode)new JsonArray((int)tris[3 * t], (int)tris[(3 * t) + 1], (int)tris[(3 * t) + 2]))]);
        }

        var info = new ProcessStartInfo(FaceDetector.Python!)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(Script!);
        info.ArgumentList.Add(BodyDetector.Model!);
        info.ArgumentList.Add(Model!);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("The garment process would not start.");
        // Read stderr alongside stdout, or a chatty model load fills its pipe and the process stalls.
        var errTask = process.StandardError.ReadToEndAsync();
        using (var stdin = process.StandardInput)
            stdin.Write(request.ToJsonString());

        var json = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException($"The garment step did not finish within {timeoutMs} ms.");
        }
        if (process.ExitCode != 0 || json.Length == 0)
            throw new InvalidOperationException($"The garment step failed (exit {process.ExitCode}). {FaceDetector.Trim(errTask.Result)}");

        return GarmentResult.Parse(json);
    }

    static string Base64Png(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("A view could not be encoded for the garment step.");
        return Convert.ToBase64String(data.AsSpan());
    }

    static void Look()
    {
        if (_looked) return;
        _looked = true;
        _rScript = FaceDetector.Pick(_script, FaceDetector.Candidates("src", "vision/garments.py"));
        _rModel = FaceDetector.Pick(_model, FaceDetector.Candidates("models", "sam2.1_hiera_small.pt"));
    }
    #endregion
}

/// <summary>What the garment step found: whether anything hangs, and a label per vertex when it segmented.</summary>
public sealed class GarmentResult
{
    #region Properties
    /// <summary>Whether something hangs between the legs; null when it could not be read.</summary>
    public bool? Hangs { get; private init; }

    /// <summary>Whether the views were segmented and projected onto the mesh.</summary>
    public bool Segmented { get; private init; }

    /// <summary>Why it stopped short of segmenting, when it did.</summary>
    public string? Reason { get; private init; }

    /// <summary>The label names, index 0 being <c>none</c>: <c>none</c>, <c>garment</c>, <c>lower</c>.</summary>
    public string[] Names { get; private init; } = [];

    /// <summary>A label index per vertex of the mesh passed in, or empty when nothing was projected.</summary>
    public int[] Labels { get; private init; } = [];

    /// <summary>Each view with its masks over it, and below it the mesh's vertices coloured by label, as PNG.</summary>
    public byte[] Sheet { get; private init; } = [];

    /// <summary>Everything else the script reported, for the character's manifest: per-view readings, sleeves, hem, timings.</summary>
    public JsonObject Record { get; private init; } = [];
    #endregion

    #region Methods
    /// <summary>A one-line summary, for a build note.</summary>
    public override string ToString() =>
        $"hangs {(Hangs is { } h ? h.ToString().ToLowerInvariant() : "unknown")}" +
        (Segmented ? $", {string.Join(", ", Names.Skip(1).Select((n, i) => $"{Labels.Count(l => l == i + 1)} {n}"))} vertices" : $": {Reason}");

    internal static GarmentResult Parse(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var sheet = root["sheet"]?.GetValue<string>();
        root.Remove("sheet");
        var projection = root["projection"]?.AsObject();
        int[] labels = [];
        string[] names = [];
        if (projection is not null)
        {
            labels = [.. projection["labels"]!.AsArray().Select(n => n!.GetValue<int>())];
            names = [.. projection["names"]!.AsArray().Select(n => n!.GetValue<string>())];
            projection.Remove("labels");
        }
        return new GarmentResult
        {
            Hangs = root["hangs"]?.GetValue<bool>(),
            Segmented = root["segmented"]?.GetValue<bool>() ?? false,
            Reason = root["reason"]?.GetValue<string>(),
            Names = names,
            Labels = labels,
            Sheet = sheet is null ? [] : Convert.FromBase64String(sheet),
            Record = root
        };
    }
    #endregion
}
