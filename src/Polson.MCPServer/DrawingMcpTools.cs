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

    /// <summary>
    /// The script to run, from whichever of the two sources was given.
    /// </summary>
    /// <remarks>
    /// <c>scriptFile</c> exists because re-sending the program is what a run actually spends its time
    /// on. Measured on a four-agent <c>comic_studio</c> run: 850 KB of JavaScript over 55 calls, and
    /// the twenty largest took a mean of <b>three minutes each to emit</b> against a median engine
    /// time of 45 ms — while consecutive large scripts shared <b>71%</b> of their lines. The agent was
    /// retyping the same program to change part of it, and the instructions tell it to keep one
    /// consolidated <c>artwork.js</c>, so that was the only route available.
    /// <para>
    /// Both given is <b>refused rather than resolved</b>. Either could plausibly be meant as the
    /// override, so picking one would silently run code the caller did not intend — and the failure
    /// would look like the edit not having taken effect, which is the most expensive kind to diagnose.
    /// </para>
    /// <para>
    /// The file is read here and the text follows the ordinary path from there, so
    /// <see cref="RunEventLog.SaveScript"/> still writes what actually ran into <c>scripts/</c>. That
    /// matters more with a file source than without one: the file keeps changing, and a record that
    /// pointed at it rather than copying it would describe whatever the file says later instead of
    /// what this execution ran.
    /// </para>
    /// </remarks>
    internal string ReadScriptSource(string? script, string? scriptFile)
    {
        var hasScript = !string.IsNullOrWhiteSpace(script);
        var hasFile = !string.IsNullOrWhiteSpace(scriptFile);

        if (hasScript && hasFile)
        {
            throw new ArgumentException(
                "Give either 'script' or 'scriptFile', not both — which one to run would be a guess. "
              + "To run the file, drop 'script'; to run the inline code, drop 'scriptFile'.");
        }

        if (!hasScript && !hasFile)
        {
            throw new ArgumentException(
                "Nothing to execute: pass 'script' with the JavaScript, or 'scriptFile' with a path "
              + "to a .js file in the project (e.g. 'artwork.js').");
        }

        if (!hasFile) return script!;

        var full = ProjectPath.Resolve(ProjectRoot, scriptFile!, nameof(scriptFile), "Read");

        if (!File.Exists(full))
        {
            // Named as the caller wrote it rather than as it resolved: an agent that passed
            // 'artwork.js' is looking for that, and an absolute path it never typed reads as a
            // different failure.
            throw new FileNotFoundException(
                $"No such script file: '{scriptFile}'. The path is relative to the project directory. "
              + "Write the file first, then run it.", full);
        }

        var text = File.ReadAllText(full);

        // An empty file executes cleanly, returns nothing, and renders nothing — a success that looks
        // like a drawing failure. Far likelier to be a write that has not landed than a deliberate
        // no-op, so it is worth an error rather than a puzzling blank.
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                $"'{scriptFile}' is empty, so there is nothing to execute. If a write to it is still "
              + "in flight, run it again once the file is saved.");
        }

        return text;
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
        [Description("The JavaScript code to execute. Omit this when passing scriptFile.")] string? script = null,
        [Description("Default canvas / SVG viewport width in pixels (default 800).")] int? width = null,
        [Description("Default canvas / SVG viewport height in pixels (default 600).")] int? height = null,
        [Description("Output image encoding format ('webp', 'png', 'jpeg'; default 'webp').")] string? format = null,
        [Description("Image encoding quality (1-100; default 85).")] int? quality = null,
        [Description("Optional file path where the rendered image should be saved, relative to the project directory (e.g. 'artifacts/stage1.webp'). Paths outside the project are refused.")] string? outFile = null,
        [Description("Optional file path where the rendered SVG XML should be saved, relative to the project directory (e.g. 'artifacts/stage1.svg'). Paths outside the project are refused.")] string? outSvg = null,
        [Description("Whether to include base64 imageBytes in the JSON response (default: true if outFile is omitted, false if outFile is specified).")] bool? includeBytes = null,
        [Description("Optional path to a JavaScript file to execute INSTEAD of `script`, relative to the project directory (e.g. 'artwork.js'). Use this when iterating on a file you maintain: edit the file with your ordinary editor, then run it — rather than re-sending the whole program on every call. Paths outside the project are refused. Give either `script` or `scriptFile`, never both.")] string? scriptFile = null,
        RequestContext<CallToolRequestParams>? context = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        script = ReadScriptSource(script, scriptFile);

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
        Events.Append("script.start", session.Stage, executionId, new Dictionary<string, object?>
        {
            ["script"] = scriptPath,
            ["session"] = sessionId,

            // Where the source came from, when it was not the call itself. Absent for an inline
            // script, so the record reads the same as it always has for those. Worth keeping because
            // a file source is the one case where the same path can run repeatedly with different
            // contents, and `scripts/` alone cannot say which file a given execution came from.
            ["source"] = string.IsNullOrWhiteSpace(scriptFile) ? null : scriptFile,
        });

        // What the script looks at, not just what it draws. The scope is opened here rather than
        // inside the engine because this is where the event log is, and it flows into the Task.Run
        // below with the execution context — the toolkits mutate the same instance, so the tallies
        // are readable again on this side once the run returns.
        using var probes = ProbeScope.Begin();
        using var requisitions = RequisitionScope.Begin();

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
            else if (!string.IsNullOrWhiteSpace(outSvg) && result.Success)
            {
                // Asking for vector output and getting silence is how a run answers a brief that said
                // "an SVG" with a .webp and never notices. There is no vector document to write
                // because the script never made one — say so, on the response the agent reads.
                result.Logs.Add(
                    $"[WARN] outSvg '{outSvg}' wrote nothing: this script produced no vector document, " +
                    "so there is no SVG markup to save. A raster canvas (createCanvas / getContext('2d')) " +
                    "renders to pixels only. Build the scene on a Snap paper — Snap(width, height) — and " +
                    "return it, or drop outSvg. See polson://manual/14.");

                Events.Append("render.novector", session.Stage, executionId, new Dictionary<string, object?>
                {
                    ["script"] = scriptPath,
                    ["requested"] = outSvg
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
            RecordRequisitions(session.Stage, executionId, scriptPath, requisitions);
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
    /// <summary>
    /// Writes what this execution requisitioned, and where the budget stands afterwards.
    /// </summary>
    /// <remarks>
    /// Requisition was the only thing an agent could do that left no trace at all. Asking what a
    /// painting generated and what it drew meant reading the cache directory and the scripts — which
    /// is the first question anyone asks of a piece that used generation, and precisely the one a
    /// provenance file written by the agent is not sufficient to answer.
    /// <para>
    /// A refusal is its own event rather than a failed requisition. Nothing was reached and nothing
    /// was spent; the remedy is to reword. A run that spent its time rewording descriptors is a very
    /// different run from one the service kept turning away, and they should not read alike.
    /// </para>
    /// <para>
    /// The budget snapshot is written once per execution that touched the surface, not once per
    /// requisition: it is a running total, and repeating it after every call would say the same thing
    /// several times while making the interesting transition harder to find.
    /// </para>
    /// </remarks>
    private void RecordRequisitions(string? stage, string executionId, string? scriptPath, RequisitionScope requisitions)
    {
        if (!requisitions.Any) return;

        foreach (var r in requisitions.Records)
        {
            var fields = new Dictionary<string, object?>
            {
                ["script"] = scriptPath,
                ["kind"] = r.Kind,
                ["descriptor"] = r.Descriptor,
                ["model"] = r.Model,
                ["reason"] = r.Reason
            };

            if (r.Refused)
            {
                Events.Append("asset.refused", stage, executionId, fields);
                continue;
            }

            fields["success"] = r.Success;
            fields["fromCache"] = r.FromCache;
            if (!r.Success) fields["failure"] = r.Failure;

            Events.Append("asset.requisition", stage, executionId, fields);
        }

        if (requisitions.Budget is { } budget)
        {
            Events.Append("budget", stage, executionId, new Dictionary<string, object?>
            {
                ["total"] = budget.Total,
                ["spent"] = budget.Spent,
                ["remaining"] = budget.Remaining,
                ["cacheHits"] = budget.CacheHits,
                ["tokensSpent"] = budget.TokensSpent,
                ["recordsDropped"] = requisitions.Dropped == 0 ? null : requisitions.Dropped
            });
        }
    }

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

        // What the looking found, not just that it happened. One event per finding, because each
        // answers a question somebody asked and a tally of answers is not an answer.
        foreach (var outcome in probes.Outcomes)
        {
            var fields = new Dictionary<string, object?>
            {
                ["script"] = scriptPath,
                ["kind"] = outcome.Kind,
                ["found"] = outcome.Summary
            };
            if (outcome.Fields is not null)
            {
                foreach (var (key, value) in outcome.Fields) fields[key] = value;
            }

            Events.Append("observe", stage, executionId, fields);
        }

        Events.Append("inspect", stage, executionId, new Dictionary<string, object?>
        {
            ["script"] = scriptPath,
            ["probes"] = probes.Counts,
            ["total"] = probes.Total,
            // Say so rather than truncating quietly: a capped record that does not admit the cap
            // reads as a complete one.
            ["outcomesDropped"] = probes.OutcomesDropped == 0 ? null : probes.OutcomesDropped
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
        bool? includeBytes = null,
        string? scriptFile = null)
        // Named rather than positional: this forwards a parameter list that grows, and passing them
        // by position meant adding one in the middle silently rebound every argument after it.
        => ExecuteScript(script: script, scriptFile: scriptFile, width: width, height: height,
                         format: format, quality: quality, outFile: outFile, outSvg: outSvg,
                         includeBytes: includeBytes);

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

    [McpServerTool(Name = "Recall")]
    [Description("Recalls what THIS PROJECT did in its EARLIER runs — the stages it declared, the reasoning it wrote " +
        "into Stage.note, what those stages produced, and what failed. This is memory across sessions, not within " +
        "one: use `History` for scripts you ran a moment ago, and `Search` for design theory and the API. CALL THIS " +
        "AT THE START of a project that has run before, and again whenever you are about to attempt something the " +
        "record may already have an answer for ('how did the last run light this', 'did the arcade proportions " +
        "work'). Read `runs`: **0 means this project has never run before**, which is a definitive answer that there " +
        "is nothing to remember — not a failed search. A non-zero `runs` with no `results` means those runs happened " +
        "and none of them matched, which is a different fact and worth acting on differently.")]
    public JsonObject Recall(
        [Description("What you are trying to remember, in natural language (e.g. 'how the aqueduct arches were built').")] string query,
        [Description("Number of episodes to return (1-25; default 5).")] int? k = null)
    => Recorded(nameof(Recall), () =>
    {
        ArgumentNullException.ThrowIfNull(query);

        var episodes = EpisodicMemory.Read(ProjectRoot);
        var runs = episodes.Count == 0 ? 0 : episodes.Max(e => e.Run);
        var recalled = EpisodicMemory.Recall(episodes, query, Math.Clamp(k ?? 5, 1, 25));

        var results = new JsonArray();
        foreach (var r in recalled)
        {
            results.Add(new JsonObject
            {
                ["uri"] = r.Episode.Uri,
                ["run"] = r.Episode.Run,
                ["stage"] = r.Episode.Stage,
                ["when"] = r.Episode.RunStarted?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["score"] = r.Score,
                ["notes"] = new JsonArray([.. r.Episode.Notes.Select(n => (JsonNode)JsonValue.Create(n)!)]),
                ["artifacts"] = new JsonArray([.. r.Episode.Artifacts.Select(a => (JsonNode)JsonValue.Create(a)!)]),
                ["scripts"] = new JsonArray([.. r.Episode.Scripts.Select(s => (JsonNode)JsonValue.Create(s)!)]),
                ["executions"] = r.Episode.Executions,
                ["failures"] = r.Episode.Failures
            });
        }

        // Three states, and they are not the same answer. An agent told "nothing" cannot tell a
        // project with no past from one whose past does not mention what it asked about, and the
        // second is the one where the record is worth reading rather than ignored.
        var hint = runs == 0
            ? "This project has no earlier run, so there is nothing to remember yet. Proceed from the brief."
            : results.Count == 0
                ? $"{runs} earlier run(s) exist and none of their episodes matched this query. The past is real but "
                  + "silent on this; try the words the earlier run would have used, or proceed and leave a "
                  + "Stage.note that makes the next run's recall better."
                : "Each result is one stage of one earlier run. `notes` is what that pass said it was doing, and "
                  + "`artifacts` can be opened with Skia.Image.load(...) to see what it actually produced.";

        return new JsonObject
        {
            ["query"] = query,
            ["project"] = ProjectRoot is null ? null : Path.GetFileName(ProjectRoot),
            ["runs"] = runs,
            ["episodes"] = episodes.Count,
            ["count"] = results.Count,
            ["results"] = results,
            ["hint"] = hint
        };
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
