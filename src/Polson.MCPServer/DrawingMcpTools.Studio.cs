namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Polson.Drawing.Skia;
using Polson.ExtendedMind.ParallelSearch;
using Polson.Drawing.Svg;

/// <summary>The studio's MCP tools and checks: Research, VerifyFigures, and the brief's words on a logo.</summary>
/// <remarks>
/// Kept apart so <c>Polson.Engine</c> can link <c>DrawingMcpTools.cs</c> without ExtendedMind. Taste and characters
/// have files of their own. A build without this file has neither tool, and <see cref="CheckClientWords"/> compiles
/// away.
/// </remarks>
public partial class DrawingMcpTools
{
    #region Constants & Static Properties
    // What the tool descriptions say of the corpus. Polson.Engine defines the same names for a server with the SDK
    // reference only, in DrawingMcpTools.Engine.cs.
    private const string SearchLead = "Searches the studio's design knowledge and API reference for passages relevant to a technique, " +
        "and returns them ranked with the SDK calls that implement them. The corpus is the studio manuals — classical " +
        "drawing, perspective, lighting, anatomy, composition, logo geometry, typography, distilled from the studio " +
        "reference library — plus the Polson JS SDK core and schema documents. CALL THIS FIRST when you know what you " +
        "want to draw but not how the studio does it";
    private const string SearchScopes = "'all' (default), 'manual' for design theory only, 'sdk' for the API reference only.";
    private const string ReadDocLead = "Reads a studio document IN FULL by its `polson://` URI and returns the text — the SDK method " +
        "reference (polson://sdk/core/{Area}), the schemas (polson://sdk/schema/{Area}), the symbol index " +
        "(polson://sdk/symbols), or a studio manual (polson://manual/13).";
    private const string ReadDocBareForms = "'13', 'manual/13', 'sdk/core/Chart'";
    private const string ReadDocExample = "'polson://sdk/core/Chart' or 'polson://manual/13'";

    /// <summary>Where a print or vector warning points for more: the poster manual, which only the studio has.</summary>
    private const string PrintManual = " See polson://manual/14.";

    /// <summary>
    /// Research transport, configured once at startup and picked up by every tools instance — the
    /// same arrangement as <see cref="JsDrawingEngine.Assets"/>. Null when no key is configured.
    /// </summary>
    public static ParallelClient? ResearchClient { get; set; }

    /// <summary>
    /// The processor every research run uses, from <c>Research:Processor</c>.
    /// </summary>
    /// <remarks>
    /// Configuration rather than a tool parameter, deliberately. The tiers span thirty-fold on price —
    /// $5 per thousand runs on <c>lite</c> against $300 on <c>ultra</c> — which makes this the sharpest
    /// cost lever in the studio, and it is not one an agent should be able to pull. The same reasoning
    /// keeps the image model out of <c>Assets</c>'s script-facing surface.
    /// </remarks>
    public static string ResearchProcessor { get; set; } = TaskProcessor.Default;

    /// <summary>
    /// Seconds <c>Research</c> holds a connection open before handing back a run id to resume with.
    /// </summary>
    /// <remarks>
    /// <b>Set by the transport, not by the research.</b> A task takes one to two minutes and the
    /// obvious default was 150 seconds to cover it — but an MCP host imposes its own request timeout,
    /// and ADK's is <b>60 seconds</b>. A wait longer than that does not produce a slow answer; it
    /// produces no answer at all, because the client gives up first and the reply is discarded with
    /// the run id inside it.
    /// <para>
    /// Measured on a live ADK run: the agent asked for 150, the call failed at 60 with a transport
    /// error carrying no run id, and — having no way to resume something it could not name — it
    /// started a <b>second</b> research run. Both completed server-side, so neither was refunded, and
    /// one question consumed the entire two-run allowance.
    /// </para>
    /// <para>
    /// Forty-five leaves headroom for the round trip and serialisation, so the reply arrives, carries
    /// the run id, and resuming costs nothing. Waiting is cheap; losing the handle is not.
    /// </para>
    /// </remarks>
    public const int DefaultWaitSeconds = 45;
    #endregion

    #region Properties
    /// <summary>
    /// Sourced-data research, or null when no Parallel key is configured.
    /// </summary>
    /// <remarks>
    /// Null is a first-class state rather than a fault: a studio with no key must still run, and the
    /// <c>Research</c> tool answers plainly that data cannot be sourced instead of failing obscurely.
    /// What it must never do is let an agent conclude that inventing the figure is the alternative.
    /// </remarks>
    public ParallelClient? Parallel { get; set; } = ResearchClient;
    #endregion

    #region Methods
    partial void CheckClientWords(DrawingExecutionResult result, string? outFile, string? outSvg, SessionContext session, string executionId, string? scriptPath)
    {
        // A logo's words, read off what was drawn rather than off the tagline argument (ClientWords): on the mark a word
        // the brief does not contain fails a check the server records; the brand sheet, with labels of its own, warns.
        if (result.Success && ClientWords.Applies(ProjectRoot)
            && new[] { outSvg, outFile }.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)
                && Path.GetFileNameWithoutExtension(p) is "final" or "brand_sheet") is { } piece)
        {
            var lines = ClientWords.LinesOf(result.RenderedFromPaper ? result.SvgXml : result.ResultCanvas);
            var foreign = ClientWords.Foreign(lines, ClientWords.Brief(ProjectRoot!));
            var artifact = piece.Replace('\\', '/');
            if (Path.GetFileNameWithoutExtension(piece) == "final")
            {
                var claim = ClientWords.Claim(artifact);
                var detail = foreign.Length == 0 ? $"{lines.Count} line(s), every word in the brief" : ClientWords.MarkDetail(foreign);
                Events.Append("check", session.Stage, executionId, new Dictionary<string, object?>
                {
                    ["claim"] = claim, ["passed"] = foreign.Length == 0, ["detail"] = detail,
                });
                result.Logs.Add($"[CHECK] {(foreign.Length == 0 ? "PASS" : "FAIL")} {claim} - {detail}");
                result.RecordCheck(claim, foreign.Length == 0, detail);
            }
            else if (foreign.Length > 0)
            {
                result.Logs.Add(ClientWords.SheetWarning(artifact, foreign));
                Events.Append("render.words", session.Stage, executionId, new Dictionary<string, object?>
                {
                    ["script"] = scriptPath, ["artifact"] = artifact, ["words"] = foreign,
                });
            }
        }
    }

    [McpServerTool(Name = "Research")]
    [Description("Commissions sourced factual data from the web and returns it as JSON, with a citation and a " +
        "confidence for EVERY field. Use it whenever a graphic states a number, a date, a rank or a quantity that " +
        "you do not already have from the brief.\n\n" +
        "NEVER INVENT A FIGURE, AND NEVER DRAW A PLACEHOLDER NUMBER. A plausible-looking invented value is the " +
        "single worst thing this studio can produce: the layout puts a source line under it and the graphic then " +
        "asserts something nobody checked. If research fails, say so in the artifact and to the director — a chart " +
        "that admits a missing figure is worth more than one that fabricates it.\n\n" +
        "YOU GET TWO RUNS, AND THEY ARE NOT EQUAL. The FIRST must carry your ENTIRE data requirement — every " +
        "figure the graphic needs, in one schema, planned before you call. The SECOND exists only to CORRECT the " +
        "first: a field that came back empty, wrong, or at a confidence too low to draw. It is not the second " +
        "half of the research. If you are planning to use both, you have already split the requirement, which is " +
        "the mistake this ceiling exists to prevent. `researchRemaining` in the reply tells you what is left.\n\n" +
        "THIS IS AN LLM DOING RESEARCH, NOT A KEYWORD LOOKUP — so write to it as you would brief a researcher. " +
        "`objective` is prose read by a model and has no published length limit, so being complete costs you " +
        "nothing and terseness costs you accuracy. Say the whole question, the context it sits in, the units " +
        "and period you want, and any source preference. A long specific objective with a rich schema is both " +
        "FASTER and MORE ACCURATE than several small ones: one run pays the latency once instead of per query, " +
        "and the model reconciles every field against the others in a single pass rather than answering each in " +
        "isolation. (Observed once: a six-row table returned Apollo 11's duration exactly at high confidence, " +
        "while a narrower two-field query on the same figure came back 36 seconds out at medium.)\n\n" +
        "The limit is FIELD COUNT, not length. An array counts as ONE field however many rows it holds — six " +
        "Apollo missions with three properties each, eighteen values, went through as a single `missions` " +
        "field. Around five TOP-LEVEL fields is the comfortable size; past that, group related ones into a " +
        "nested object or an array. Never split the requirement into a second run, and note you cannot choose " +
        "a bigger engine: which processor runs your research is a cost decision held in configuration, not a " +
        "parameter.\n\n" +
        "IF THIS CALL ITSELF TIMES OUT — a transport error rather than a reply from this tool — YOUR RESEARCH IS " +
        "STILL RUNNING AND HAS STILL COST YOU A RUN. Do NOT call Research again with the same question: that " +
        "starts a SECOND run and spends the whole allowance on one. Find the run you already have, from a " +
        "script — `for (const t of Research.tasks) log(t.id + ' ' + t.status)` — then call Research with that " +
        "runId to collect it. A run is lost only if you abandon it.\n\n" +
        "A run that FAILS is refunded, so the ceiling is two successful runs rather than two attempts — you may " +
        "retry a genuine failure. It is not a second question.\n\n" +
        "Your schema is CHECKED BEFORE ANYTHING IS SPENT. Malformed JSON, a schema that is not an object, or one " +
        "declaring no properties is REFUSED — that costs nothing and leaves the allowance untouched, so read it " +
        "as free advice rather than as one of your two runs. Too many top-level fields only WARNS, in " +
        "`schemaWarnings`: it is a forecast about quality rather than a hard limit, so heed it if your fields " +
        "are research-heavy and ignore it if they are dates and booleans.\n\n" +
        "Give `schema` when you want a table or a record set: a JSON Schema whose field DESCRIPTIONS are " +
        "instructions, because they determine what comes back. Omit it for a prose answer. This BLOCKS while the " +
        "research runs — typically 15-50 seconds — so start it before the work that needs it. If it has not " +
        "finished by `waitSeconds`, you get a runId back: call Research again with that runId to keep waiting, " +
        "which costs nothing extra. Pass wait=false to start one and collect it later while you do other work.\n\n" +
        "Completed results stay readable from any later script as the `Research` global: Research.latest.result is " +
        "ordinary JS, and Research.latest.citeField('missions.0') gives the caption line for one row.")]
    public Task<JsonObject> Research(
        [Description("Optional label for this research, in your own words — recorded in the run and used to find the task later with Research.find(...). Left out, it is taken from the objective.")] string? description = null,
        [Description("The whole research brief, in prose, read by a model — not a search string. No published length limit, so be complete: the question, its context, the units and period you want, and any source preference. Required when starting; omit when resuming with runId.")] string? objective = null,
        [Description("JSON Schema for the answer, as a string. Field descriptions steer the result, so write them as instructions. Omit for prose.")] string? schema = null,
        [Description("Seconds to hold the connection before handing back a runId to resume with. Default 45, deliberately under the 60s request timeout most MCP hosts impose — a longer wait does not reach you, it just makes the whole call fail. Raise it only on a host you know waits longer.")] int? waitSeconds = null,
        [Description("false to start the research and return immediately, collecting it later with runId. Default true.")] bool? wait = null,
        [Description("Resume or collect an already-started task by its run id.")] string? runId = null,
        RequestContext<CallToolRequestParams>? context = null,
        CancellationToken cancellationToken = default)
    => RecordedAsync(nameof(Research), async () =>
    {
        var session = Registry.GetOrCreate(GetSessionId(context?.Server));
        var response = new JsonObject();

        // A label is not worth failing a research call over. It was required, and a model that sent
        // objective and schema without it got "An error occurred invoking 'Research'" — no clue which
        // argument was missing — three times, then gave up and reported the tool broken. It was right
        // to refuse to invent figures; it should never have been in that position. Derive one.
        description = Trimmed(description) ?? Label(objective) ?? "research";

        if (Parallel is null)
        {
            response["ok"] = false;
            response["error"] = "No Parallel API key is configured, so research is unavailable for this whole run.";
            response["remedy"] = "Do not invent figures. Tell the director the data could not be sourced, and "
                               + "either drop the quantitative element or label it as unsourced.";
            return response;
        }

        var wait_ = wait ?? true;
        var budget = TimeSpan.FromSeconds(Math.Clamp(waitSeconds ?? DefaultWaitSeconds, 5, 900));
        ResearchTask task;

        if (!string.IsNullOrWhiteSpace(runId))
        {
            var known = session.Research.Get(runId);
            if (known is null)
            {
                response["ok"] = false;
                response["error"] = $"No research task '{runId}' was started in this session.";
                return response;
            }

            task = known;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(objective))
            {
                response["ok"] = false;
                response["error"] = "An objective is required to start research.";
                return response;
            }

            // Checked before the call, so an exhausted allowance costs nothing and says so plainly.
            if (!session.Research.Budget.CanAfford())
            {
                response["ok"] = false;
                response["error"] = $"This run's research allowance is spent "
                                  + $"({session.Research.Budget.Spent} of {session.Research.Budget.Total} used).";
                response["remedy"] = "Work with the figures already sourced — read them from a script with "
                                   + "Research.tasks. Do NOT invent the missing ones: if the graphic needs a "
                                   + "figure you could not source, say so in the artifact and tell the director "
                                   + "the allowance was reached.";
                Events.Append("research.refused", session.Stage, null,
                    new Dictionary<string, object?>
                    {
                        ["description"] = description,
                        ["spent"] = session.Research.Budget.Spent,
                        ["total"] = session.Research.Budget.Total,
                    });
                return response;
            }

            var chosen = ResearchProcessor;

            // A dry run over the schema, before anything is spent. The allowance is two and the first
            // carries the whole requirement, so a schema that would come back thin is worth catching
            // here rather than discovering a minute later with the run already gone.
            var inspection = TaskSchema.Inspect(schema, chosen);
            if (!inspection.Usable)
            {
                response["ok"] = false;
                response["error"] = inspection.Problem;
                response["remedy"] = inspection.Remedy;
                response["fieldCount"] = inspection.FieldCount;
                response["processorCapacity"] = inspection.Capacity;
                response["researchRemaining"] = session.Research.Budget.Remaining;
                response["note"] = "Nothing was spent — fix the schema and call again.";
                if (inspection.SuggestedProcessor is not null)
                {
                    response["suggestedProcessor"] = inspection.SuggestedProcessor;   // for the operator, not the agent
                }

                Events.Append("research.schemaRejected", session.Stage, null,
                    new Dictionary<string, object?>
                    {
                        ["description"] = description,
                        ["problem"] = inspection.Problem,
                        ["fields"] = inspection.FieldCount,
                        ["capacity"] = inspection.Capacity,
                    });
                return response;
            }

            TaskSpec? spec;
            try
            {
                spec = string.IsNullOrWhiteSpace(schema) ? null : TaskSpec.Json(schema);
            }
            catch (ArgumentException ex)
            {
                // Belt and braces: Inspect has already parsed it, so this should be unreachable.
                response["ok"] = false;
                response["error"] = ex.Message;
                return response;
            }
            var started = await Parallel.StartTask(
                objective, spec, new TaskOptions { Processor = chosen }, cancellationToken);

            if (!started.Success)
            {
                response["ok"] = false;
                response["error"] = started.Error;
                response["remedy"] = started.Remedy;
                response["retryable"] = started.Retryable;
                return response;
            }

            task = session.Research.Start(
                started.RunId, description, objective, started.Processor, started.Status);

            // Not grounds for refusing, but worth saying while the answer can still be judged against
            // them — an undescribed field is the one most likely to come back wrong.
            if (inspection.Warnings.Count > 0)
            {
                response["schemaWarnings"] = new JsonArray(
                    inspection.Warnings.Select(w => (JsonNode)System.Text.Json.Nodes.JsonValue.Create(w)!).ToArray());
            }

            Events.Append("research.started", session.Stage, null,
                new Dictionary<string, object?>
                {
                    ["runId"] = task.Id,
                    ["description"] = description,
                    ["processor"] = task.Processor,

                    // **How long this will look like nothing happening.** Between this event and
                    // research.completed the server writes nothing at all, so a reader watching the
                    // record sees a run that has stopped. A director called a healthy run broken on
                    // exactly that: it was 132 seconds into a task that took 292.
                    ["expectSeconds"] = ResearchTypicalSeconds,
                    ["expectNote"] = ResearchWaitNote,
                });
        }

        if (wait_ && task.IsActive)
        {
            var result = await Parallel.AwaitTask(task.Id, budget, cancellationToken);
            ApplyResult(session.Research, task, result);
        }
        else if (!wait_ && task.IsActive)
        {
            var status = await Parallel.CheckTask(task.Id, cancellationToken);
            if (status.Success) session.Research.SetStatus(task, status.Status);
        }

        return DescribeTask(task, response, session);
    });

    /// <summary>
    /// How long a research run typically takes, in seconds. Measured, not predicted.
    /// </summary>
    /// <remarks>
    /// <b>The reference used to say "about a minute" and that was wrong by two to five times.</b> Two
    /// runs measured on the <c>base</c> tier took <b>143.5s</b> for a single field and <b>292.3s</b>
    /// for sixteen — so a richer schema costs more, which is the only shape two points support. The
    /// number here is the upper measurement rather than the average, because its job is to say when
    /// waiting stops being normal, and an average would call half of all healthy runs late.
    /// </remarks>
    internal const int ResearchTypicalSeconds = 300;

    /// <summary>What to tell a reader who is watching a research wait and seeing nothing.</summary>
    /// <remarks>
    /// Written for two audiences at once: the agent, which otherwise polls a task that cannot
    /// possibly have finished, and whoever is watching the run page, where the gap between
    /// <c>research.started</c> and <c>research.completed</c> is the longest silence in a run.
    /// </remarks>
    internal const string ResearchWaitNote =
        "Research is slow on purpose: a model is reading sources, not a lookup. Measured on this "
        + "tier: 143s for one field, 292s for sixteen. Nothing is written to the record in between, "
        + "so a run that looks stopped during this window is usually working. Do the parts of the "
        + "piece that need no figures while you wait.";

    /// <summary>Null for anything blank, so an empty argument reads as absent rather than as "".</summary>
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// A short label from the objective's first sentence, for when a caller gave no description.
    /// </summary>
    /// <remarks>
    /// Only ever a fallback: a caller's own words say what the research is <i>for</i>, which the
    /// objective does not — that says what was asked. But a derived label keeps the run findable
    /// through <c>Research.find(...)</c>, and findable beats absent.
    /// </remarks>
    private static string? Label(string? objective)
    {
        var text = Trimmed(objective);
        if (text is null) return null;

        // A research objective is as likely to end its first sentence with '?' as with '.', and the
        // mark is worth keeping: a label reading as a question is clearer than one that trails off.
        var stop = text.AsSpan().IndexOfAny(".?!\n".AsSpan());
        var end = stop > 0 && text[stop] != '\n' ? stop + 1 : stop;
        var first = (end > 0 ? text[..end] : text).Trim();

        return first.Length <= 80 ? first : string.Concat(first.AsSpan(0, 77), "…");
    }

    /// <summary>
    /// Scans everything a research run brought back from the open web, and says what it found.
    /// </summary>
    /// <remarks>
    /// The reasoning and the excerpts are the exposed surface: they are long-form prose from a page
    /// nobody here chose, and they reach an agent verbatim. The values are scanned too — a field
    /// returning a string is a field that can carry one.
    /// </remarks>
    private static IReadOnlyList<string> ScanResearch(ParallelTaskResult result)
    {
        List<string>? findings = null;

        void Check(string? text, string where)
        {
            if (string.IsNullOrEmpty(text)) return;
            var report = TextScan.Scan(text, where);
            if (report.Clean) return;

            foreach (var hidden in report.Hidden)
            {
                (findings ??= []).Add($"{where}: {hidden.Count}× {hidden.ClassName} ({hidden.Notation})");
            }

            foreach (var phrase in report.Phrases)
            {
                (findings ??= []).Add($"{where}: text addressed to the reader [{phrase.Kind}] — \"{phrase.Match}\"");
            }
        }

        Check(result.Text, "output");
        if (result.Json is { } json) Check(json.ToString(), "output");

        foreach (var basis in result.Basis)
        {
            Check(basis.Reasoning, $"basis[{basis.Field}].reasoning");
            foreach (var citation in basis.Citations ?? [])
            {
                Check(citation.Title, $"basis[{basis.Field}].title");
                foreach (var excerpt in citation.Excerpts ?? []) Check(excerpt, $"basis[{basis.Field}].excerpt");
            }
        }

        return findings is null ? [] : findings;
    }

    /// <summary>
    /// Strips the concealment classes out of the basis, so what an agent reads is what a person would
    /// see. The values themselves are left exactly as returned — a figure is evidence and must not be
    /// quietly rewritten; anything wrong with one is reported instead.
    /// </summary>
    private static IReadOnlyList<FieldBasis> SanitizeBasis(IReadOnlyList<FieldBasis> basis) =>
        [.. basis.Select(b => b with
        {
            Reasoning = TextScan.Sanitize(b.Reasoning),
            Citations = b.Citations is null ? null :
                [.. b.Citations.Select(c => c with
                {
                    Title = c.Title is null ? null : TextScan.Sanitize(c.Title),
                    Excerpts = c.Excerpts is null ? null : [.. c.Excerpts.Select(TextScan.Sanitize)],
                })],
        })];

    /// <summary>Folds a collected result into the task, or records why it is not there yet.</summary>
    private void ApplyResult(ResearchRegistry registry, ResearchTask task, ParallelTaskResult result)
    {
        if (result.Success)
        {
            // Scanned and sanitised HERE, on the way in — not left to a tool the agent might call,
            // because by then the text is already in its context and reading it is the injection.
            var scan = ScanResearch(result);
            registry.Complete(
                task, JsonInterop.ToClr(result.Json), SanitizeBasis(result.Basis), result.Run?.Status);
            registry.Flag(task, scan);

            if (scan.Count > 0)
            {
                Events.Append("research.contentFlagged", null, null,
                    new Dictionary<string, object?> { ["runId"] = task.Id, ["findings"] = string.Join("; ", scan) });
            }

            Events.Append("research.completed", null, null,
                new Dictionary<string, object?>
                {
                    ["runId"] = task.Id,
                    ["seconds"] = task.ElapsedSeconds,
                    ["fields"] = task.Basis.Count,
                });
            return;
        }

        // A timeout is the run still running, not a failure — the id stays good.
        if (result.Failure != ParallelFailure.Timeout)
        {
            registry.Fail(task, result.Error);
            Events.Append("research.failed", null, null,
                new Dictionary<string, object?> { ["runId"] = task.Id, ["error"] = result.Error });
        }
    }

    /// <summary>
    /// Builds the tool's answer. The data is inlined because that is the point; the basis is
    /// summarised, because a full reasoning string per field is large and a script can read it in
    /// full from the <c>Research</c> global.
    /// </summary>
    private static JsonObject DescribeTask(ResearchTask task, JsonObject response, SessionContext session)
    {
        response["ok"] = task.IsComplete;
        response["runId"] = task.Id;
        response["status"] = task.Status;

        // Said on every describe rather than only at the start: an agent that polls a task reads
        // this response, and "how long should this take" is exactly the question it is asking.
        if (task.IsActive)
        {
            response["expectSeconds"] = ResearchTypicalSeconds;
            response["waiting"] = ResearchWaitNote;
        }
        response["processor"] = task.Processor;
        response["elapsedSeconds"] = task.ElapsedSeconds;
        response["description"] = task.Description;
        response["researchRemaining"] = session.Research.Budget.Remaining;

        if (task.IsComplete)
        {
            response["result"] = task.Result is null ? null : JsonSerializer.SerializeToNode(task.Result);

            var basis = new JsonArray();
            foreach (var entry in task.Basis)
            {
                basis.Add(new JsonObject
                {
                    ["field"] = entry.Field,
                    ["confidence"] = entry.Confidence,
                    ["sources"] = new JsonArray(
                        (entry.Citations ?? []).Select(c => (JsonNode)System.Text.Json.Nodes.JsonValue.Create(c.Cite())!).ToArray()),
                });
            }

            response["basis"] = basis;
            response["note"] = $"Read the full reasoning from a script: Research.get('{task.Id}').basis. "
                             + $"Per-row caption: Research.get('{task.Id}').citeField('field.0').";

            // Said at the moment it matters — when the agent is looking at an answer and deciding
            // whether it is good enough — rather than only in the tool description it read once.
            var remaining = session.Research.Budget.Remaining;
            response["allowanceNote"] = remaining switch
            {
                0 => "No research runs remain. Draw with these figures; if one is unusable, say so in the "
                   + "artifact rather than substituting a guess.",
                1 => "One run remains, and it is for CORRECTING this answer — a field that came back empty, "
                   + "wrong, or too low-confidence to draw. Check the basis now and decide. It is not for a "
                   + "further question.",
                _ => $"{remaining} research runs remain.",
            };
            return response;
        }

        if (task.IsFailed)
        {
            response["error"] = task.Error ?? "The research run failed.";
            response["remedy"] = "Do NOT substitute an invented figure. Either commission different research, "
                               + "or state in the graphic and to the director that the number could not be sourced.";
            return response;
        }

        if (task.NeedsAction)
        {
            response["remedy"] = "The run is waiting on something outside itself and will not progress on its "
                               + "own. Do not keep polling it.";
            return response;
        }

        response["remedy"] = $"Still running after {task.ElapsedSeconds:F0}s. Call Research again with "
                           + $"runId '{task.Id}' to keep waiting — it costs nothing extra. Meanwhile you may do "
                           + "work that does not depend on these figures (layout, palette, type), but do not draw "
                           + "placeholder numbers.";
        return response;
    }

    [McpServerTool(Name = "VerifyFigures")]
    [Description("Checks the figures drawn on a saved SVG against the research or Qloo answer they are tagged to. Reads the markup here and returns only a verdict, so nothing large reaches your context. Tag a figure when you draw it: .attr({ 'data-basis': 'trun_abc:films.3.runtime' }) for research, .attr({ 'data-basis': 't3:entities.0.affinity' }) for a Qloo answer, the path as Taste.get('t3') shows it.")]
    public JsonObject VerifyFigures(
        [Description("Path of the saved SVG, relative to the project directory.")] string path,
        RequestContext<CallToolRequestParams>? context = null)
    => Recorded(nameof(VerifyFigures), () =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = ProjectPath.Resolve(ProjectRoot, path, nameof(path), "Read");
        if (!File.Exists(full))
        {
            throw new FileNotFoundException(
                $"No SVG at '{path}' (resolves to '{full}'). Paths are relative to the project directory.",
                full);
        }

        var archive = new ResearchArchive(
            SessionContext.ResearchArchiveDir ?? Path.Combine(ProjectRoot, ".polson", "research"));

        var session = Registry.GetOrCreate(GetSessionId(context?.Server));
        var verdict = FigureVerifier.Verify(File.ReadAllText(full), archive, session.Taste.Get);

        var response = new JsonObject
        {
            ["checked"] = verdict.Checked,
            ["verified"] = verdict.Verified,
            ["findings"] = verdict.Findings.Count,
        };

        // **Nothing tagged is not the same answer as everything verified**, and reporting `0 of 0`
        // as a pass is how a piece with no tags at all would come back clean. The distinction is the
        // one the run report's own `documents read` row got wrong for a day: an absent thing and a
        // zero look identical to a reader who does not know the thing exists.
        if (verdict.Checked == 0)
        {
            response["verdict"] = "nothing to check";
            response["advice"] =
                "No figure on this page carries a data-basis tag, so none of them was checked. Tag "
                + "each number as you draw it with the run and field it came from — "
                + "paper.text(x, y, '1,636').attr({ 'data-basis': 'trun_abc:totalRuntimeMinutes' }), or a Qloo answer's "
                + "id and path, 't3:entities.0.affinity' — and run this again.";
            return response;
        }

        response["verdict"] = verdict.Findings.Count == 0 ? "all tagged figures reconcile" : "figures do not reconcile";

        if (verdict.Findings.Count > 0)
        {
            var findings = new JsonArray();
            foreach (var finding in verdict.Findings)
            {
                var entry = new JsonObject
                {
                    ["basis"] = finding.Basis,
                    ["drawn"] = finding.Drawn,
                    ["reason"] = finding.Reason,
                };

                if (finding.Expected is not null) entry["expected"] = finding.Expected;
                findings.Add(entry);
            }

            response["notReconciled"] = findings;
        }

        return response;
    });
    #endregion
}
