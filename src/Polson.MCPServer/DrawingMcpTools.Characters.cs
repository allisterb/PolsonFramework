namespace Polson.MCPServer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SkiaSharp;

using Polson.Drawing.Skia;
using Polson.ExtendedMind.CharacterGeneration;
using Polson.ExtendedMind.ObjectGeneration;

/// <summary>One character being built: where it has got to, and what it has said on the way.</summary>
internal sealed class CharacterJob
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DateTime Started { get; } = DateTime.UtcNow;
    public volatile string Stage = "queued";
    public volatile string Status = "running";   // running | completed | failed
    public string? Error, Remedy;
    public JsonObject? Manifest;
    public Task? Work;
    public List<string> Notes { get; } = [];
    public ConcurrentDictionary<string, int> Ms { get; } = new();
    public double ElapsedSeconds => (DateTime.UtcNow - Started).TotalSeconds;
}

public partial class DrawingMcpTools
{
    #region Character generation
    /// <summary>Reconstructs a mesh from a character's views. Null when <c>Trellis:BaseUrl</c> is not set.</summary>
    public static TrellisClient? CharacterReconstructor { get; set; }

    /// <summary>Rigs a mesh. Null when <c>Characters:RigUrl</c> is not set.</summary>
    public static RigClient? CharacterRigger { get; set; }

    /// <summary>
    /// How long building a character takes, measured end to end. Said in every reply while it runs, so
    /// an agent polling it knows when waiting stops being normal.
    /// </summary>
    internal const int CharacterTypicalSeconds = 240;

    static readonly ConcurrentDictionary<string, CharacterJob> CharacterJobs = new(StringComparer.Ordinal);

    /// <summary>One GPU on the far end: characters queue rather than contend.</summary>
    static readonly SemaphoreSlim CharacterGate = new(1, 1);

    [McpServerTool(Name = "GenerateCharacter")]
    [Description("Builds a character you can POSE and DRAW from any angle, from pictures of it: a textured 3D body is " +
        "reconstructed from the views, rigged with a skeleton, its joints named by body part, and a face built from " +
        "the views put on its head. A script then loads it with Character.load(name) and poses it with body-part " +
        "names — pose({ head: { yDeg: 30 }, leftForearm: { zDeg: -40 } }) — the same names for every character.\n\n" +
        "GIVE IT A TURNAROUND SHEET: `sheet` is a project path to ONE image with the character's views side by side, " +
        "which it splits itself. Three figures are read as front, side, back; four as front, back, left, right; pass " +
        "`sheetOrder` for any other layout. Make one by generating the views in ONE call, Assets.cutout(desc, { variants: " +
        "['front view', 'side view, in profile', 'back view'] }), drawing the cells onto one canvas with a clear gap between " +
        "them, and saving it with outFile; separate generations of 'the same' character are different characters. ONE " +
        "profile is enough: which way it faces is read from the picture, so 'side' needs no left or right. Or pass the views " +
        "one by one as `front` (required), `back`, and `side` or `left`/`right`. One character, one style, one scale. Stand the figure in an " +
        "A-pose or T-pose, arms clear of the body, whole figure in frame: a rigger cannot separate an arm drawn " +
        "against the torso, and a detector cannot find a head that was cropped off.\n\n" +
        "IT TAKES MINUTES (typically about four), so start it early and do other work while it runs. It BLOCKS up to " +
        "`waitSeconds` (default 45, under the 60-second timeout most hosts impose) and then hands back a jobId: call " +
        "again with that jobId to keep waiting, which costs nothing. Pass wait=false to start it and collect later. If " +
        "the call itself times out the job is STILL RUNNING — do not start a second one for the same character; read " +
        "Character.list() from a script, or call with the name alone to be told the job that owns it.\n\n" +
        "When it finishes, look at the preview it writes (characters/<name>/preview.png: front, three-quarter, " +
        "profile) before drawing with it, and read `warnings`: a joint that could not be named, or a face built without " +
        "a profile, is said there and nowhere else.\n\n" +
        "GIVE IT THE FACE LARGE, TOO, if you can: `faceSheet` is a project path to a head sheet, a front then one or two " +
        "profiles, head and shoulders, which it splits the same way. The face is then built from the large front head " +
        "rather than from one cropped out of the full figure. Make it FIRST, " +
        "with Assets.cutout(desc, { variants: ['front, looking at the viewer', 'side view, in profile'], framing: 'head' }), " +
        "and pass that cutout as `reference` to the body turnaround so both show the same person.")]
    public Task<JsonObject> GenerateCharacter(
        [Description("A name for the character: letters, digits, '-' and '_'. It is the folder characters/<name>/ and what Character.load takes. Required when starting.")] string? name = null,
        [Description("Project path to a turnaround sheet: the character's views side by side on one image. Use this or the separate views.")] string? sheet = null,
        [Description("The views on the sheet, left to right, comma-separated, from front, back, side, left, right. Must include 'front'. Omitted, it is read from how many figures the sheet holds: three are 'front,side,back', four are 'front,back,left,right'.")] string? sheetOrder = null,
        [Description("Project path to a head sheet: the character's face, head and shoulders, front first and then one or two profiles, side by side. Optional; with it the face is built from these instead of from the full figures.")] string? faceSheet = null,
        [Description("Project path to the front view — the whole figure, facing the viewer. Required when starting without a sheet.")] string? front = null,
        [Description("Project path to the back view.")] string? back = null,
        [Description("Project path to the character's left side view (its left, whichever way it faces on the page).")] string? left = null,
        [Description("Project path to the character's right side view.")] string? right = null,
        [Description("Project path to a profile view when you have one and no reason to say which side it shows. Not with `left` or `right`.")] string? side = null,
        [Description("Seed for the body reconstruction. The same views and seed give the same shape.")] int? seed = null,
        [Description("true to rebuild a character that already exists, replacing it. Default false, because a rebuild takes minutes.")] bool? replace = null,
        [Description("Seconds to hold the connection before handing back a jobId. Default 45.")] int? waitSeconds = null,
        [Description("false to start the build and return at once; collect it later with jobId. Default true.")] bool? wait = null,
        [Description("Resume or collect a build already started, by the jobId it returned.")] string? jobId = null,
        RequestContext<CallToolRequestParams>? context = null,
        CancellationToken cancellationToken = default)
    => RecordedAsync(nameof(GenerateCharacter), async () =>
    {
        var response = new JsonObject();
        CharacterJob job;

        if (!string.IsNullOrWhiteSpace(jobId))
        {
            if (!CharacterJobs.TryGetValue(jobId, out var known))
                return Refuse(response, $"No character build '{jobId}' is known to this server.",
                    "Call GenerateCharacter with a name to start one, or Character.list() from a script to see what is built.");
            job = known;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(name) || !CharacterToolkit.ValidName.IsMatch(name))
                return Refuse(response, $"'{name}' is not a usable character name.",
                    "Pass `name`: letters, digits, '-' and '_', starting with a letter or digit.");

            // A build already under way for this name is the one to wait on, not a reason for a second.
            if (CharacterJobs.Values.FirstOrDefault(j => j.Name == name && j.Status == "running") is { } running)
            {
                job = running;
                response["note"] = $"A build of '{name}' is already running as {running.Id}; waiting on that one.";
            }
            else
            {
                if (ProjectRoot is null)
                    return Refuse(response, "This server has no project directory, so there is nowhere to put a character.",
                        "Start the server with --project-dir.");
                if (string.IsNullOrWhiteSpace(front) && string.IsNullOrWhiteSpace(sheet))
                    return Refuse(response, "Pictures of the character are required.",
                        "Pass `sheet`: a project path to a turnaround sheet. Or pass `front` (and `back`, `left`, `right`) as separate images.");
                if (!string.IsNullOrWhiteSpace(front) && !string.IsNullOrWhiteSpace(sheet))
                    return Refuse(response, "Both a sheet and separate views were given.",
                        "Pass one or the other: a sheet is split into views, so the two would disagree.");
                if (CharacterReconstructor is not { } trellis || CharacterRigger is not { } rigger)
                    return Refuse(response,
                        "Character generation is not configured on this server: it needs "
                        + (CharacterReconstructor is null ? "a reconstruction service (Trellis:BaseUrl)" : "")
                        + (CharacterReconstructor is null && CharacterRigger is null ? " and " : "")
                        + (CharacterRigger is null ? "a rig service (Characters:RigUrl)" : "") + ".",
                        "Tell the director. Meanwhile draw the character by construction: Drawing.createMannequinFigure and createHeadForFigure.");

                var dir = ProjectPath.Resolve(ProjectRoot, $"{CharacterToolkit.Folder}/{name}", nameof(name), "Write");
                if (File.Exists(Path.Combine(dir, CharacterBuilder.Manifest)) && replace != true)
                    return Refuse(response, $"The character '{name}' already exists.",
                        $"Use it: Character.load('{name}'). To rebuild it from new views, pass replace=true — it takes minutes.");

                var order = sheetOrder?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (sheet is not null && order is not null && (!order.Contains("front") || order.Except(ViewKeys).Any() || order.Distinct().Count() != order.Length))
                    return Refuse(response, $"sheetOrder '{sheetOrder}' is not a list of distinct views including 'front'.",
                        "Name the views on the sheet left to right from front, back, side, left, right: e.g. 'front,side,back'.");

                // One profile of unknown side, or named ones; both would be three profiles of one head.
                if (order is not null && order.Contains("side") && (order.Contains("left") || order.Contains("right"))
                    || !string.IsNullOrWhiteSpace(side) && (!string.IsNullOrWhiteSpace(left) || !string.IsNullOrWhiteSpace(right)))
                    return Refuse(response, "A 'side' view was given with a 'left' or 'right' one.",
                        "Use 'side' for a single profile whose side does not matter, or 'left' and 'right' for two. Not both.");

                var views = new Dictionary<string, string>();
                foreach (var (key, path) in new[] { ("sheet", sheet), ("faceSheet", faceSheet), ("front", front), ("back", back), ("left", left), ("right", right), ("side", side) })
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    var full = ProjectPath.Resolve(ProjectRoot, path, key, "Read");
                    if (!File.Exists(full))
                        return Refuse(response, $"The {key} view '{path}' does not exist.", "Pass a path inside the project, as outFile writes them.");
                    views[key] = full;
                }

                // Fail before minutes are spent, and say which half is down.
                if (!await trellis.IsReadyAsync(cancellationToken))
                    return Refuse(response, $"The reconstruction service at {trellis.InferUrl} is not ready.",
                        "Tell the director it needs starting. Do not retry in a loop.");
                if (!await rigger.IsReadyAsync(cancellationToken))
                    return Refuse(response, $"The rig service at {rigger.BaseUrl} is not ready (it may still be loading its models).",
                        "Tell the director it needs starting, or wait a minute if it was just started.");

                job = new CharacterJob { Id = "chr_" + Guid.NewGuid().ToString("N")[..12], Name = name };
                CharacterJobs[job.Id] = job;
                var events = Events;
                events.Append("character.started", null, null, new Dictionary<string, object?>
                {
                    ["job"] = job.Id, ["name"] = name, ["views"] = string.Join(",", views.Keys),
                    ["expectSeconds"] = CharacterTypicalSeconds
                });
                job.Work = Task.Run(() => BuildCharacter(job, dir, views, order, seed ?? (int)TrellisClient.DefaultSeed, trellis, rigger, events));
            }
        }

        if ((wait ?? true) && job.Status == "running" && job.Work is { } work)
        {
            var budget = TimeSpan.FromSeconds(Math.Clamp(waitSeconds ?? DefaultWaitSeconds, 5, 900));
            await Task.WhenAny(work, Task.Delay(budget, cancellationToken));
        }

        return DescribeCharacter(job, response);
    });

    static JsonObject Refuse(JsonObject response, string error, string remedy)
    {
        response["ok"] = false;
        response["error"] = error;
        response["remedy"] = remedy;
        return response;
    }

    static JsonObject DescribeCharacter(CharacterJob job, JsonObject response)
    {
        response["ok"] = job.Status == "completed";
        response["jobId"] = job.Id;
        response["name"] = job.Name;
        response["status"] = job.Status;
        response["stage"] = job.Stage;
        response["elapsedSeconds"] = Math.Round(job.ElapsedSeconds);
        lock (job.Notes) if (job.Notes.Count > 0) response["notes"] = new JsonArray([.. job.Notes.Select(n => (JsonNode)n)]);
        response["stageMs"] = new JsonObject(job.Ms.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));

        switch (job.Status)
        {
            case "completed":
                response["character"] = job.Manifest?.DeepClone();
                response["next"] = $"Look at characters/{job.Name}/preview.png, then in a script: "
                                 + $"const c = Character.load('{job.Name}'); Mesh.draw(ctx, c.pose({{ head: {{ yDeg: 30 }} }}), {{ x, y, scale }});";
                break;
            case "failed":
                response["error"] = job.Error;
                response["remedy"] = job.Remedy;
                break;
            default:
                response["expectSeconds"] = CharacterTypicalSeconds;
                response["remedy"] = $"Still building ({job.Stage}) after {job.ElapsedSeconds:F0}s. Call GenerateCharacter with "
                                   + $"jobId '{job.Id}' to keep waiting — it costs nothing. Meanwhile do work that does not need the character.";
                break;
        }
        return response;
    }

    /// <summary>The whole build, off the request thread. Every stage records its time and anything worth saying.</summary>
    static readonly string[] ViewKeys = ["front", "back", "left", "right", "side"];

    static async Task BuildCharacter(CharacterJob job, string dir, Dictionary<string, string> views, string[]? order, int seed,
                                     TrellisClient trellis, RigClient rigger, RunEventLog events)
    {
        void Note(string note) { lock (job.Notes) job.Notes.Add(note); }
        async Task<T> Step<T>(string stage, Func<Task<T>> body)
        {
            job.Stage = stage;
            var t = DateTime.UtcNow;
            var result = await body();
            var ms = (int)(DateTime.UtcNow - t).TotalMilliseconds;
            job.Ms[stage] = ms;
            events.Append("character.stage", null, null, new Dictionary<string, object?> { ["job"] = job.Id, ["stage"] = stage, ["ms"] = ms });
            return result;
        }

        await CharacterGate.WaitAsync();
        try
        {
            var staging = dir + ".building";
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            // ── Views ──────────────────────────────────────────────────────────────────────────────
            var images = await Step("views", () =>
            {
                SKBitmap Decode(string key, string path) => SKBitmap.Decode(path)
                    ?? throw new StageFailure($"The {key} could not be decoded as an image.", "Pass a PNG, JPEG or WebP.");
                if (!views.TryGetValue("sheet", out var sheetPath))
                    return Task.FromResult(views.Where(kv => kv.Key != "faceSheet").ToDictionary(kv => kv.Key, kv => Decode(kv.Key + " view", kv.Value)));

                using var sheetImage = Decode("sheet", sheetPath);
                var figures = CharacterBuilder.SplitSheet(sheetImage, order?.Length ?? 0, out var why);
                if (order is null)
                {
                    order = CharacterBuilder.OrderOf(figures, out var unread)
                        ?? throw new StageFailure($"The sheet's views could not be told apart: {unread}.",
                            "Leave a clear gap between every figure on the sheet, or pass sheetOrder naming the views left to right, e.g. 'front,side,back'.");
                    Note($"The sheet held {figures.Count} figure(s), read as {string.Join(", ", order)}.");
                }
                if (figures.Count != order.Length)
                    throw new StageFailure($"The sheet could not be split into {order.Length} views: {why}.",
                        "Leave a clear gap between the figures on the sheet, or pass sheetOrder naming only the views it holds.");
                return Task.FromResult(order.Zip(figures).ToDictionary(p => p.First, p => p.Second));
            });
            foreach (var (key, image) in images)
            {
                using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(staging, $"view-{key}.png"), png.ToArray());
            }

            // The head sheet, when there is one: a front first, then its profiles.
            List<SKBitmap>? heads = null;
            if (views.TryGetValue("faceSheet", out var headPath))
            {
                using var headImage = SKBitmap.Decode(headPath)
                    ?? throw new StageFailure("The face sheet could not be decoded as an image.", "Pass a PNG, JPEG or WebP.");
                heads = CharacterBuilder.SplitSheet(headImage, 0, out var headWhy);
                var problem = heads.Count is 0 or > 3
                    ? $"it holds {heads.Count} separate figure(s){(headWhy is null ? "" : $": {headWhy}")}"
                    : CharacterBuilder.Touching(heads);
                if (problem is not null)
                    throw new StageFailure($"The face sheet could not be read: {problem}.",
                        "A face sheet is a front view first, then one or two profiles, head and shoulders, with a clear gap between them.");
                Note($"The face sheet held {heads.Count} head(s): a front{(heads.Count > 1 ? $" and {heads.Count - 1} profile(s)" : "")}.");
                for (var i = 0; i < heads.Count; i++)
                {
                    using var png = heads[i].Encode(SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine(staging, $"view-head-{i}.png"), png.ToArray());
                }
            }

            // ── Body: reconstruct ──────────────────────────────────────────────────────────────────
            var mesh = await Step("reconstruct", async () =>
            {
                var caps = await trellis.GetCapabilitiesAsync();
                if (caps.Available && !caps.Modes.Contains("image"))
                    throw new StageFailure($"The reconstruction service is loaded for {string.Join("/", caps.Modes)} only, and a character needs pictures.",
                        "The director needs to load an image variant of the service.");

                // The canonical order, so the front always conditions first.
                var uris = new[] { "front", "back", "left", "right", "side" }.Where(images.ContainsKey)
                    .Select(k => "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(staging, $"view-{k}.png"))))
                    .ToArray();
                var result = await trellis.GenerateAsync(new TrellisRequest
                {
                    Mode = "image",
                    Image = uris.Length == 1 ? uris[0] : uris,
                    MultiImageAlgorithm = uris.Length > 1 ? "stochastic" : null,
                    Seed = seed,
                    OutputFormat = "glb"
                });
                if (!result.Success) throw new StageFailure($"Reconstruction failed: {result.Error}", result.Remedy);
                File.WriteAllBytes(Path.Combine(staging, CharacterBuilder.MeshGlb), result.Bytes);
                return result.Bytes;
            });

            // ── Body: rig ──────────────────────────────────────────────────────────────────────────
            var rig = await Step("rig", async () =>
            {
                var result = await rigger.RigAsync(mesh);
                if (!result.Success) throw new StageFailure($"Rigging failed: {result.Error}", result.Remedy);
                File.WriteAllBytes(Path.Combine(staging, CharacterBuilder.RiggedGlb), result.Bytes);
                if (result.WeightedJoints < result.Joints / 2)
                    Note($"Only {result.WeightedJoints} of {result.Joints} joints carry weight; some bones will move nothing.");
                return result;
            });

            // ── Joints: name them by body part ─────────────────────────────────────────────────────
            var body = new MeshToolkit(staging).Load(CharacterBuilder.RiggedGlb);
            var labels = await Step("joints", () => Task.FromResult(CharacterBuilder.LabelJoints(body)));
            foreach (var w in labels.Warnings) Note(w);
            if (labels.Render is { } render)
            {
                using var png = render.Encode(SKEncodedImageFormat.Png, 90);
                File.WriteAllBytes(Path.Combine(staging, "joints-render.png"), png.ToArray());
            }

            // ── Face: build from the views, find the body's own, record where it goes ─────────────
            JsonObject? faceInfo = null;
            await Step("face", () =>
            {
                if (!FaceDetector.Available) { Note("No face backend here, so the character keeps its reconstructed face."); return Task.FromResult(0); }
                try
                {
                    var notes = new List<string>();
                    var face = heads is not null
                        ? CharacterBuilder.BuildFaceFromHeads(heads[0], [.. heads.Skip(1).Select((h, i) => ($"face sheet profile {i + 1}", h))], notes)
                        : CharacterBuilder.BuildFace(images["front"],
                            [.. new[] { "left", "right", "side" }.Where(images.ContainsKey).Select(k => (k, images[k]))], notes);
                    notes.ForEach(Note);
                    CharacterBuilder.SaveFace(face, staging);
                    var anchors = body.FaceSheet().Anchors;
                    body.WithFaceMesh(face, anchors);   // proves the transplant before the character is declared finished
                    faceInfo = new JsonObject { ["source"] = face.Source, ["anchors"] = JsonNodeOf(anchors) };
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    Note($"The face could not be built, so the character keeps its reconstructed face: {ex.Message}");
                }
                return Task.FromResult(0);
            });

            // ── Record, and move into place ────────────────────────────────────────────────────────
            var manifest = new JsonObject
            {
                ["name"] = job.Name,
                ["built"] = DateTime.UtcNow.ToString("O"),
                ["views"] = new JsonArray([.. images.Keys.Select(k => (JsonNode)$"view-{k}.png")]),
                ["files"] = new JsonObject { ["mesh"] = CharacterBuilder.MeshGlb, ["rigged"] = CharacterBuilder.RiggedGlb, ["preview"] = "preview.png" },
                ["joints"] = new JsonObject(labels.Map.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                ["jointError"] = new JsonObject(labels.Error.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)Math.Round(kv.Value, 3)))),
                ["facing"] = labels.FrontSign >= 0 ? "+Z" : "-Z",
                ["face"] = faceInfo,
                ["rig"] = new JsonObject
                {
                    ["joints"] = rig.Joints, ["weightedJoints"] = rig.WeightedJoints, ["vertices"] = rig.Vertices,
                    ["serviceMs"] = new JsonObject(rig.Timing.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)))
                },
                ["seed"] = seed,
            };
            lock (job.Notes) manifest["warnings"] = new JsonArray([.. job.Notes.Select(n => (JsonNode)n)]);
            File.WriteAllText(Path.Combine(staging, CharacterBuilder.Manifest), manifest.ToJsonString(new() { WriteIndented = true }));

            await Step("preview", () =>
            {
                WritePreview(staging, job.Name);
                return Task.FromResult(0);
            });

            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.Move(staging, dir);
            job.Manifest = manifest;
            job.Stage = "done";
            job.Status = "completed";
            events.Append("character.completed", null, null, new Dictionary<string, object?>
            {
                ["job"] = job.Id, ["name"] = job.Name, ["seconds"] = Math.Round(job.ElapsedSeconds),
                ["joints"] = labels.Map.Count, ["face"] = faceInfo is not null, ["warnings"] = job.Notes.Count
            });
        }
        catch (Exception ex)
        {
            job.Error = ex is StageFailure ? ex.Message : $"{job.Stage}: {ex.GetType().Name}: {ex.Message}";
            job.Remedy = ex is StageFailure sf ? sf.Remedy : "This is a fault in the character pipeline rather than in your views; tell the director.";
            job.Status = "failed";
            events.Append("character.failed", null, null, new Dictionary<string, object?>
            {
                ["job"] = job.Id, ["name"] = job.Name, ["stage"] = job.Stage, ["error"] = job.Error
            });
        }
        finally
        {
            CharacterGate.Release();
        }
    }

    /// <summary>Front, three-quarter and profile, so the build can be judged by eye before it is used.</summary>
    static void WritePreview(string dir, string name)
    {
        var character = CharacterBuilder.Assemble(dir, $"{CharacterToolkit.Folder}/{name}");
        var box = character.Bounds;
        float h = Convert.ToSingle(box["height"]), cy = Convert.ToSingle(box["cy"]);
        const int W = 1200, H = 700;
        var scale = 0.86f * H / h;
        using var canvas = new SkiaCanvas(W, H);
        var ctx = canvas.GetContext("2d");
        ctx.FillStyle = "#f2efe8";
        ctx.FillRect(0, 0, W, H);
        var mesh = new MeshToolkit();
        var i = 0;
        foreach (var yaw in new[] { 0f, 35f, 90f })
        {
            mesh.Draw(ctx, character, new Dictionary<string, object?>
            {
                ["x"] = (W / 6f) + (i++ * W / 3f), ["y"] = (H / 2f) + (cy * scale), ["scale"] = scale, ["yawDeg"] = yaw
            });
        }
        using var png = canvas.Bitmap.Bitmap.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(Path.Combine(dir, "preview.png"), png.ToArray());
    }

    static JsonNode? JsonNodeOf(Dictionary<string, object?> d) =>
        System.Text.Json.JsonSerializer.SerializeToNode(d);

    /// <summary>A stage that failed for a reason the caller can act on.</summary>
    sealed class StageFailure(string message, string remedy) : Exception(message)
    {
        public string Remedy { get; } = remedy;
    }
    #endregion
}
