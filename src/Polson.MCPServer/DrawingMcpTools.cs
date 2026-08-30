namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Polson.Drawing.Skia;
using Polson.Drawing.Svg;

public class DrawingMcpTools
{
    #region Constants & Static Properties
    public static TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(1);
    #endregion

    #region Constructors
    public DrawingMcpTools(JsDrawingEngine? engine = null, SessionRegistry? registry = null, IKnowledgeIndex? knowledge = null, string? projectRoot = null)
    {
        Engine = engine ?? new JsDrawingEngine();
        Registry = registry ?? new SessionRegistry();
        Knowledge = knowledge ?? new LocalKnowledgeIndex();
        ProjectRoot = string.IsNullOrWhiteSpace(projectRoot) ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        Events = new RunEventLog(ProjectRoot);
        Engine.Events = Events;

        // So a script's file paths mean the same thing the tool's outFile does.
        Engine.ProjectRoot = ProjectRoot;
    }
    #endregion

    #region Properties
    public JsDrawingEngine Engine { get; }

    public SessionRegistry Registry { get; }

    public IKnowledgeIndex Knowledge { get; }

    /// <summary>
    /// Directory that written output is confined to, or <c>null</c> for no confinement.
    /// </summary>
    /// <remarks>
    /// Set whenever the server is hosting an agent, which is every path through the CLI. Left null
    /// when the tools are constructed directly as a library, so tests and ad-hoc use keep writing
    /// wherever they ask to. The confinement is what makes a generated project's promise true —
    /// a visitor-driven agent has no filesystem reach outside its own run directory — and without
    /// it <c>outFile</c> accepts an absolute path or a <c>..</c> traversal and writes anywhere the
    /// process can.
    /// </remarks>
    public string? ProjectRoot { get; }

    /// <summary>The run's append-only record. Inert when there is no project directory.</summary>
    public RunEventLog Events { get; }

    /// <summary>
    /// Top score below which prose retrieval is reporting its nearest neighbour rather than an answer.
    /// </summary>
    /// <remarks>Calibrated against observed hits: on-target passages score 19-31, off-target ones 2-8.</remarks>
    public const double ScoreFloor = 10.0;
    #endregion

    #region Methods
    /// <summary>
    /// Resolves an agent-supplied output path against <see cref="ProjectRoot"/> and refuses anything
    /// that lands outside it. Creates the containing directory.
    /// </summary>
    /// <remarks>
    /// Refuses loudly rather than silently rewriting the path into bounds: a mark saved somewhere
    /// other than where the agent asked would go unnoticed and break the run log's account of where
    /// artifacts are. The message names both the offending path and the root, so the agent can
    /// correct itself without another round trip.
    /// <para>
    /// <c>Path.Combine</c> returns the second argument outright when it is absolute, so an absolute
    /// path arrives at the containment check rather than bypassing it. This compares resolved paths,
    /// so it stops <c>..</c> traversal and absolute paths; it does not follow symlinks, so a link
    /// planted inside the project could still point out of it.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Runs a tool body, recording a <c>tool.error</c> if it throws, then rethrowing.
    /// </summary>
    /// <remarks>
    /// Only <c>ExecuteScript</c> was recorded, so a failure in any other tool left the run's record
    /// silent about it. The first agent run hit an unexplained <c>MeasureSvgPath</c> failure and
    /// worked around it; nothing in <c>events/server.jsonl</c> showed it had happened, which is
    /// exactly the case a record exists for. The result is unchanged — this only observes.
    /// </remarks>
    private T Recorded<T>(string tool, Func<T> body)
    {
        try
        {
            return body();
        }
        catch (Exception ex)
        {
            Events.Append("tool.error", fields: new Dictionary<string, object?>
            {
                ["tool"] = tool,
                ["error"] = ex.Message
            });
            throw;
        }
    }

    /// <inheritdoc cref="Recorded{T}(string, Func{T})"/>
    private async Task<T> RecordedAsync<T>(string tool, Func<Task<T>> body)
    {
        try
        {
            return await body();
        }
        catch (Exception ex)
        {
            Events.Append("tool.error", fields: new Dictionary<string, object?>
            {
                ["tool"] = tool,
                ["error"] = ex.Message
            });
            throw;
        }
    }

    internal string ResolveOutputPath(string path, string parameterName)
    {
        var full = ProjectPath.Resolve(ProjectRoot, path, parameterName, "Write to");

        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return full;
    }

    [McpServerTool(Name = "Search")]
    [Description("Searches the studio's design knowledge and API reference for passages relevant to a technique, " +
        "and returns them ranked with the SDK calls that implement them. The corpus is the studio manuals — classical " +
        "drawing, perspective, lighting, anatomy, composition, logo geometry, typography, distilled from the studio " +
        "reference library — plus the Polson JS SDK core and schema documents. CALL THIS FIRST when you know what you " +
        "want to draw but not how the studio does it (e.g. 'two point perspective box', 'cast shadow falloff', " +
        "'golden ratio logo grid', 'optical kerning'). Each result carries a resource URI to read in full.\n\n" +
        "Pass a CALL NAME instead ('paper.squircle', 'ctx.drawRimLight', or a bare 'clearSpaceGuide') and this " +
        "resolves it exactly against the generated symbol index rather than searching prose. Read `confidence`: " +
        "'direct' means the call exists and the returned signature is authoritative; 'no-match' on a call name is a " +
        "DEFINITIVE answer that no such call exists — do not write it, use `nearest` instead; 'related' means these " +
        "are the closest passages and confirm nothing about any call. `notSearched` lists corpora not consulted, so " +
        "an empty result never means the knowledge is absent.")]
    public Task<JsonObject> Search(
        [Description("What you are trying to do or find, in natural language or as an API name (e.g. 'construct a perspective cylinder').")] string query,
        [Description("Number of passages to return (1-25; default 5).")] int? k = null,
        [Description("Corpus to search: 'all' (default), 'manual' for design theory only, 'sdk' for the API reference only.")] string? scope = null,
        CancellationToken cancellationToken = default)
    => RecordedAsync(nameof(Search), async () =>
    {
        ArgumentNullException.ThrowIfNull(query);

        var searchScope = scope?.ToLowerInvariant() switch
        {
            "manual" or "manuals" or "theory" or "design" => KnowledgeScope.Manual,
            "sdk" or "api" or "reference" => KnowledgeScope.Sdk,
            _ => KnowledgeScope.All
        };

        // A dotted, space-free query is a claim that a call exists, and that claim gets an exact
        // answer. Routing it through prose retrieval instead is how an agent gets told about the
        // raster Logo toolkit when it asked for a vector squircle, and believes it.
        var named = JsSymbolManifest.ResolveQuery(query);
        if (JsSymbolManifest.LooksLikeSymbol(query) && named.Count == 0)
        {
            var suggestions = new JsonArray();
            foreach (var s in JsSymbolManifest.Nearest(query, 5))
            {
                suggestions.Add(new JsonObject { ["name"] = s.Name, ["signature"] = s.Signature, ["uri"] = s.Uri });
            }

            return new JsonObject
            {
                ["query"] = query,
                ["confidence"] = "no-match",
                ["scope"] = "symbols",
                ["backend"] = "symbol-index",
                ["count"] = 0,
                ["results"] = new JsonArray(),
                ["nearest"] = suggestions,
                ["hint"] = $"`{JsSymbolManifest.Normalise(query)}` is not a call in this SDK. This is a definitive "
                    + "answer from the generated symbol index, not a failed text search — do not write it. Use one of "
                    + "`nearest`, list a receiver's full surface at `polson://sdk/symbols/{Receiver}`, or search again "
                    + "in prose for the technique rather than the name."
            };
        }

        var hits = await Knowledge.SearchAsync(query, k ?? 5, searchScope, cancellationToken);

        var results = new JsonArray();
        foreach (var hit in hits)
        {
            results.Add(new JsonObject
            {
                ["uri"] = hit.Uri,
                ["title"] = hit.Title,
                ["section"] = hit.Section,
                ["source"] = hit.Source,
                ["score"] = hit.Score,
                ["apis"] = new JsonArray([.. hit.Apis.Select(a => (JsonNode)JsonValue.Create(a)!)]),
                ["text"] = hit.Text
            });
        }

        var symbols = new JsonArray();
        foreach (var s in named)
        {
            symbols.Add(new JsonObject
            {
                ["name"] = s.Name,
                ["signature"] = s.Signature,
                ["kind"] = s.Kind,
                ["area"] = s.Area,
                ["uri"] = s.Uri,
                ["inherited"] = s.Inherited
            });
        }

        // Prose retrieval always returns its nearest neighbour, so a low top score means "nothing
        // here answers this" rather than "here is a weak answer". Say which it is.
        var confidence = named.Count > 0
            ? "direct"
            : hits.Count > 0 && hits[0].Score >= ScoreFloor ? "related" : "no-match";

        var notSearched = new JsonArray();
        foreach (var s in Enum.GetValues<KnowledgeScope>())
        {
            if (s != KnowledgeScope.All && searchScope != KnowledgeScope.All && s != searchScope)
            {
                notSearched.Add(JsonValue.Create(s.ToString().ToLowerInvariant()));
            }
        }

        return new JsonObject
        {
            ["query"] = query,
            ["confidence"] = confidence,
            ["scope"] = searchScope.ToString().ToLowerInvariant(),
            ["notSearched"] = notSearched,
            ["backend"] = Knowledge.Name,
            ["symbols"] = symbols,
            ["count"] = results.Count,
            ["results"] = results,
            ["hint"] = confidence switch
            {
                "direct" => "`symbols` resolved exactly against the generated index — those signatures are authoritative. "
                    + "The passages below are context for how the call is used.",
                "related" => "Read the `uri` of a result for the full section. These are the nearest passages, not a "
                    + "confirmation that any particular call exists — check `polson://sdk/symbols` before writing one.",
                _ => "Nothing matched with confidence. Nothing here confirms a capability exists or is absent: to settle "
                    + "that, read `polson://sdk/symbols` or `polson://sdk/symbols/{Receiver}`. "
                    + (notSearched.Count > 0 ? $"Scopes not consulted: {string.Join(", ", notSearched.Select(n => n!.ToString()))}." : "")
            }
        };
    });

    [McpServerTool(Name = "ExecuteScript")]
    [Description("Executes a JavaScript drawing script inside the sandboxed graphics engine, supporting Snap.svg vector graphics, HTML5 2D Canvas, and Skia procedural shaders, filters, and image processing. Automatically renders returned paper/canvas/bitmap/image-data to WebP/PNG/JPEG bytes and SVG markup.")]
    public async Task<DrawingExecutionResult> ExecuteScript(
        [Description("The JavaScript code to execute.")] string script,
        [Description("Default canvas / SVG viewport width in pixels (default 800).")] int? width = null,
        [Description("Default canvas / SVG viewport height in pixels (default 600).")] int? height = null,
        [Description("Output image encoding format ('webp', 'png', 'jpeg'; default 'webp').")] string? format = null,
        [Description("Image encoding quality (1-100; default 85).")] int? quality = null,
        [Description("Optional file path where the rendered image should be saved, relative to the project directory (e.g. 'artifacts/stage1.webp'). Paths outside the project are refused.")] string? outFile = null,
        [Description("Optional file path where the rendered SVG XML should be saved, relative to the project directory (e.g. 'artifacts/stage1.svg'). Paths outside the project are refused.")] string? outSvg = null,
        [Description("Whether to include base64 imageBytes in the JSON response (default: true if outFile is omitted, false if outFile is specified).")] bool? includeBytes = null,
        RequestContext<CallToolRequestParams>? context = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        var sessionId = GetSessionId(context?.Server);
        var session = Registry.GetOrCreate(sessionId);

        lock (session.ScriptHistory)
        {
            session.ScriptHistory.Add(script);
        }

        session.EnterCall();

        var executionId = Guid.NewGuid().ToString("N")[..8];

        // Three nested scopes, outermost first, so ordinary log lines carry the same attribution the
        // event log does. The stage is re-read from the session on every call: a property pushed
        // inside an async handler never reaches the next request, so this is what makes a stage span
        // more than one execution. Camel keeps CaseId the same way.
        using var _project = Runtime.PushAuditProperty("Project", ProjectRoot);
        using var _stage = Runtime.PushAuditProperty("Stage", session.Stage);
        using var _exec = Runtime.PushAuditProperty("ExecutionId", executionId);

        // Saved before execution, so a script that hangs or crashes the engine is still on disk to
        // read afterwards — which is exactly the run you most want the source of.
        var scriptPath = Events.SaveScript(script);
        Events.Append("script.start", session.Stage, executionId, new Dictionary<string, object?> { ["script"] = scriptPath, ["session"] = sessionId });

        // What the script looks at, not just what it draws. The scope is opened here rather than
        // inside the engine because this is where the event log is, and it flows into the Task.Run
        // below with the execution context — the toolkits mutate the same instance, so the tallies
        // are readable again on this side once the run returns.
        using var probes = ProbeScope.Begin();

        try
        {
            var fmt = format ?? "webp";
            var q = quality ?? 85;
            var runTask = Task.Run(() => Engine.Execute(script, width ?? 800, height ?? 600, session, fmt, q, executionId), cancellationToken);
            var result = await RunWithHeartbeatAsync(runTask, progress, HeartbeatInterval, cancellationToken);

            if (!string.IsNullOrWhiteSpace(outFile) && result.ImageBytes != null && result.ImageBytes.Length > 0)
            {
                var fullOutPath = ResolveOutputPath(outFile, nameof(outFile));
                File.WriteAllBytes(fullOutPath, result.ImageBytes);
                result.ImageFilePath = fullOutPath;

                // session.Stage is read fresh rather than captured at entry: a script may declare a
                // new stage partway through, and the render belongs to the stage it was made in.
                Events.Append("render", session.Stage, executionId, new Dictionary<string, object?>
                {
                    ["script"] = scriptPath,
                    ["artifact"] = Events.Relativize(fullOutPath),
                    ["format"] = result.ImageFormat,
                    ["bytes"] = result.ImageBytes.Length
                });
            }

            if (!string.IsNullOrWhiteSpace(outSvg) && !string.IsNullOrWhiteSpace(result.SvgXml))
            {
                var fullSvgPath = ResolveOutputPath(outSvg, nameof(outSvg));
                File.WriteAllText(fullSvgPath, result.SvgXml);
                result.SvgFilePath = fullSvgPath;

                Events.Append("render", session.Stage, executionId, new Dictionary<string, object?>
                {
                    ["script"] = scriptPath,
                    ["artifact"] = Events.Relativize(fullSvgPath),
                    ["format"] = "svg"
                });
            }

            Events.Append(result.Success ? "script.ok" : "script.error", session.Stage, executionId, new Dictionary<string, object?>
            {
                ["script"] = scriptPath,
                ["ms"] = result.ExecutionTimeMs,
                ["error"] = result.Success ? null : result.Error
            });

            result.ExecutionId = executionId;

            result.ImageSize = result.ImageBytes?.Length ?? 0;
            var shouldIncludeBytes = includeBytes ?? string.IsNullOrWhiteSpace(outFile);
            if (!shouldIncludeBytes)
            {
                result.ImageBytes = null;
            }

            return result;
        }
        catch (Exception ex)
        {
            // A refused output path throws rather than returning a failed result, so without this
            // the log would carry a script.start that never ends and the run would read as hung.
            Events.Append("script.error", session.Stage, executionId, new Dictionary<string, object?>
            {
                ["script"] = scriptPath,
                ["error"] = ex.Message
            });
            throw;
        }
        finally
        {
            // In the finally so a script that crashed still reports what it managed to look at first
            // — an agent that measured, disliked what it found and then failed has told the record
            // something, and losing that would make the failure look like it came from nowhere.
            RecordProbes(session.Stage, executionId, scriptPath, probes);
            session.LeaveCall();
        }
    }

    /// <summary>
    /// Writes what one execution perceived: which artifacts it read back, and a tally of its probes.
    /// </summary>
    /// <remarks>
    /// Two events rather than one, because they answer different questions. <c>artifact.read</c> names
    /// a file an earlier pass produced, which is the only direct evidence the record carries that one
    /// pass coordinated with another through the environment rather than through its own memory; its
    /// <c>artifact</c> path is spelled exactly as the <c>render</c> event that wrote it, so the two
    /// join. <c>inspect</c> is a count, because a single <c>getPixel</c> loop runs thousands of times
    /// and the useful reading is how much looking happened, not each look.
    /// <para>
    /// Nothing is written when a script only drew. Silence means it never looked, which is itself
    /// worth being able to see.
    /// </para>
    /// </remarks>
    private void RecordProbes(string? stage, string executionId, string? scriptPath, ProbeScope probes)
    {
        if (!probes.Any) return;

        foreach (var artifact in probes.Reads)
        {
            Events.Append("artifact.read", stage, executionId, new Dictionary<string, object?>
            {
                ["script"] = scriptPath,
                ["artifact"] = artifact
            });
        }

        Events.Append("inspect", stage, executionId, new Dictionary<string, object?>
        {
            ["script"] = scriptPath,
            ["probes"] = probes.Counts,
            ["total"] = probes.Total
        });
    }

    public Task<DrawingExecutionResult> ExecuteSvgScript(
        string script,
        int? width = null,
        int? height = null,
        string? format = null,
        int? quality = null,
        string? outFile = null,
        string? outSvg = null,
        bool? includeBytes = null)
        => ExecuteScript(script, width, height, format, quality, outFile, outSvg, includeBytes);

    [McpServerTool(Name = "History")]
    [Description("Returns the last n scripts executed by the agent in this session. If n is null or omitted, returns the last script.")]
    public List<string> History(
        [Description("The number of recent scripts to return. If null or omitted, returns the last script.")] int? n = null,
        RequestContext<CallToolRequestParams>? context = null)
    => Recorded(nameof(History), () =>
    {
        var sessionId = GetSessionId(context?.Server);
        var session = Registry.GetOrCreate(sessionId);

        lock (session.ScriptHistory)
        {
            var count = n ?? 1;
            if (count <= 0) return [];
            return session.ScriptHistory.TakeLast(count).ToList();
        }
    });

    [McpServerTool(Name = "RenderSvg")]
    [Description("Headlessly renders raw SVG XML markup to a WebP/PNG/JPEG byte array.")]
    public DrawingExecutionResult RenderSvg(
        [Description("The SVG XML string to render.")] string svgXml,
        [Description("Target image width in pixels (optional, defaults to SVG width or 800).")] int? width = null,
        [Description("Target image height in pixels (optional, defaults to SVG height or 600).")] int? height = null,
        [Description("Output image encoding format ('webp', 'png', 'jpeg'; default 'webp').")] string? format = null,
        [Description("Image encoding quality (1-100; default 85).")] int? quality = null,
        [Description("Optional file path where the rendered image should be saved, relative to the project directory. Paths outside the project are refused.")] string? outFile = null,
        [Description("Whether to include base64 imageBytes in the JSON response (default: true if outFile is omitted, false if outFile is specified).")] bool? includeBytes = null)
    => Recorded(nameof(RenderSvg), () =>
    {
        ArgumentNullException.ThrowIfNull(svgXml);

        var fmt = format ?? "webp";
        var q = quality ?? 85;
        var result = new DrawingExecutionResult
        {
            SvgXml = svgXml,
            ImageFormat = SkiaImageEncoder.NormalizeFormatName(fmt)
        };

        try
        {
            var imgBytes = SvgRenderPipeline.RenderToImage(svgXml, width, height, fmt, q);
            result.Success = true;
            result.ImageBytes = imgBytes;

            if (!string.IsNullOrWhiteSpace(outFile) && imgBytes != null && imgBytes.Length > 0)
            {
                var fullOutPath = ResolveOutputPath(outFile, nameof(outFile));
                File.WriteAllBytes(fullOutPath, imgBytes);
                result.ImageFilePath = fullOutPath;

                // RenderSvg produces artifacts exactly as ExecuteScript does, so it belongs in the
                // record on the same terms — otherwise a file appears in artifacts/ that the run
                // log cannot account for.
                Events.Append("render", fields: new Dictionary<string, object?>
                {
                    ["tool"] = nameof(RenderSvg),
                    ["artifact"] = Events.Relativize(fullOutPath),
                    ["format"] = result.ImageFormat,
                    ["bytes"] = imgBytes.Length
                });
            }

            result.ImageSize = result.ImageBytes?.Length ?? 0;
            var shouldIncludeBytes = includeBytes ?? string.IsNullOrWhiteSpace(outFile);
            if (!shouldIncludeBytes)
            {
                result.ImageBytes = null;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;

            // Unlike the other tools this one turns a failure into a failed result rather than
            // throwing, so Recorded never sees it. Record it here or a refused output path — which
            // an agent will hit — would leave the run's record claiming nothing went wrong.
            Events.Append("tool.error", fields: new Dictionary<string, object?>
            {
                ["tool"] = nameof(RenderSvg),
                ["error"] = ex.Message
            });
        }

        return result;
    });

    [McpServerTool(Name = "MeasureSvgPath")]
    [Description("Measures an SVG path definition to calculate its total length, bounding box, and optional point coordinates at length.")]
    public JsonObject MeasureSvgPath(
        [Description("The SVG path data string (e.g. 'M10 10 L50 50 Z').")] string pathData,
        [Description("Optional distance along the path to sample coordinates and tangent angle.")] float? length = null)
    => Recorded(nameof(MeasureSvgPath), () =>
    {
        ArgumentNullException.ThrowIfNull(pathData);

        var totalLength = SnapPathMeasurement.GetTotalLength(pathData);
        var bbox = SnapPathMeasurement.GetBBox(pathData);

        var response = new JsonObject
        {
            ["totalLength"] = totalLength,
            ["bbox"] = new JsonObject
            {
                ["x"] = bbox.X,
                ["y"] = bbox.Y,
                ["width"] = bbox.Width,
                ["height"] = bbox.Height,
                ["cx"] = bbox.Cx,
                ["cy"] = bbox.Cy
            }
        };

        if (length.HasValue)
        {
            var pt = SnapPathMeasurement.GetPointAtLength(pathData, length.Value);
            response["pointAtLength"] = new JsonObject
            {
                ["x"] = pt.X,
                ["y"] = pt.Y,
                ["alpha"] = pt.Alpha
            };
        }

        return response;
    });

    internal static async Task<T> RunWithHeartbeatAsync<T>(
        Task<T> task,
        IProgress<ProgressNotificationValue>? progress,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        if (progress == null)
        {
            return await task;
        }

        using var timer = new PeriodicTimer(interval);
        float tick = 0f;
        while (!task.IsCompleted)
        {
            var waitTick = timer.WaitForNextTickAsync(cancellationToken).AsTask();
            var completed = await Task.WhenAny(task, waitTick);
            if (completed == task)
            {
                return await task;
            }
            tick++;
            progress.Report(new ProgressNotificationValue { Progress = tick, Total = null });
        }
        return await task;
    }

    private static string GetSessionId(McpServer? server)
    {
        if (server == null) return "default";
        return server.SessionId ?? "default";
    }
    #endregion
}
