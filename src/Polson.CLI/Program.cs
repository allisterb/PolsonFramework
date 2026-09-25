namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Polson.ExtendedMind.CharacterGeneration;
using Polson.ExtendedMind.DocumentProcessing;
using Polson.ExtendedMind.ImageGeneration;
using Polson.ExtendedMind.ObjectGeneration;
using Polson.ExtendedMind.ParallelSearch;
using Polson.ExtendedMind.Photos;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using Spectre.Console;

internal class Program : Runtime
{
    #region Constructors
    static Program()
    {
        var configPath = Path.Combine(AssemblyLocation, "appsettings.json");
        if (File.Exists(configPath))
        {
            config = LoadConfigFile(configPath);
        }
    }
    #endregion

    #region Constants
    /// <summary>
    /// Generations allowed per server run when <c>Assets:Budget</c> is not set.
    /// </summary>
    /// <remarks>
    /// An image costs roughly 1,300 tokens whatever is asked for, so this is still a ceiling rather
    /// than an allowance — but it is sized for the work rather than for a runaway. A painting is
    /// built from many surfaces (planking, rope, cloth, sky, a backdrop plate, mattes), and a budget
    /// that runs out mid-piece is worse than one that is never reached: the agent has already
    /// committed to a composition it can no longer finish.
    /// <para>
    /// Two things keep this from being an open cheque. The form-versus-substance classifier refuses
    /// a descriptor naming an object rather than a material, so the budget cannot be spent on
    /// finished pictures; and requisitions are cached by content, so a repeated one costs nothing.
    /// </para>
    /// <para>
    /// It is still a per-<i>server-run</i> ceiling, not a per-day one. A public deployment needs its
    /// own limit on top — see <c>DAILY_RUNS</c> in the web app, which is the crude cap this fine
    /// one sits inside.
    /// </para>
    /// </remarks>
    const int DefaultAssetBudget = 120;

    /// <summary>Document reads allowed per session when nothing is configured.</summary>
    const int DefaultDocumentBudget = 40;

    /// <summary>
    /// Reference photographs allowed per run. Far smaller than the asset budget because the unit is
    /// bigger: one photograph is a whole subject in the finished graphic, and a piece needing more
    /// than a couple of dozen likenesses is a piece that wanted drawing.
    /// </summary>
    const int DefaultPhotoBudget = 24;
    #endregion

    #region Methods
    static async Task Main(string[] args)
    {
        // A bare invocation is a request for help, not a request to start the server. `server` is the
        // default verb because that is how an MCP host launches us, but a person typing the bare
        // command got a process waiting silently on stdin — indistinguishable from a hang, and
        // printing nothing, because stdio mode keeps standard output clear for JSON-RPC framing.
        if (args.Length == 0) args = ["--help"];

        var isHttp = args.Contains("--http", StringComparer.OrdinalIgnoreCase);
        var isEval = args.Length > 0 && string.Equals(args[0], "eval", StringComparison.OrdinalIgnoreCase);
        var isCreate = args.Length > 0 && string.Equals(args[0], "create-project", StringComparison.OrdinalIgnoreCase);
        // A hook's stdout is a JSON reply channel, so it gets no logo and file-only logging.
        var isHook = args.Length > 0 && string.Equals(args[0], "preserve-chatlog", StringComparison.OrdinalIgnoreCase);
        var isHelp = args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase);
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        // Every verb but the default stdio server is free to write to standard output; stdio reserves it for JSON-RPC framing.
        var isConsoleVerb = (isHttp || isEval || isCreate) && !isHook;

        // The hook is checked first: its standard output belongs to the host's JSON reply, so it
        // gets no logo and no console sink. A stray byte on stdout makes the hook malformed on every
        // turn, which presents as the whole integration being broken rather than as a stray byte.
        // `logdir` is left to default to the assembly location; a hardcoded path here stopped
        // working the moment this ran on a second machine.
        if (isHook)
        {
            Runtime.WithFileLogging("Polson", "CLI-hook", isDebug);
        }
        else if (isConsoleVerb || isHelp)
        {
            PrintLogo();
            Runtime.WithFileAndConsoleLogging("Polson", "CLI", isDebug);
        }
        else
        {
            // Stdio transport: standard output is strictly reserved for JSON-RPC framing; logs go to file/stderr
            Runtime.WithFileLogging("Polson", "CLI", isDebug);
        }

        var parser = new Parser(with =>
        {
            with.CaseInsensitiveEnumValues = true;
            with.HelpWriter = Console.Error;
        });

        var result = parser.ParseArguments<ServerOptions, EvalOptions, CreateProjectOptions, ResetOptions, ReportOptions, PreserveChatlogOptions>(args);
        try
        {
            await result.MapResult(
                async (ServerOptions opts) => await HandleServerArgs(opts),
                async (EvalOptions opts) => await HandleEvalArgs(opts),
                (CreateProjectOptions opts) => HandleCreateProjectArgs(opts),
                (ResetOptions opts) => HandleResetArgs(opts),
                (ReportOptions opts) => HandleReportArgs(opts),
                (PreserveChatlogOptions opts) => Task.FromResult(ChatlogPreserver.Run(opts)),
                errs => ReportParseFailure(errs)
            );
        }
        catch (Exception ex)
        {
            if (isConsoleVerb)
            {
                AnsiConsole.WriteException(ex);
            }
            else
            {
                Error("Unhandled exception: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Builds the asset requisition surface from configuration, or leaves it disabled.
    /// </summary>
    /// <remarks>
    /// Absence of a key is a normal configuration, not an error: the toolkit is still registered so a
    /// script gets a readable "not configured" refusal from <c>Assets.material(...)</c> rather than a
    /// ReferenceError it cannot interpret. The budget is a hard ceiling on generations per server
    /// run, so a runaway agent cannot spend without bound.
    /// </remarks>
    /// <summary>
    /// A configuration value, treating blank as absent.
    /// </summary>
    /// <remarks>
    /// <c>IConfiguration</c>'s indexer returns null for a missing key but an empty string for a
    /// present-and-empty one, so <c>??</c> does not catch the second. That matters because the
    /// shipped example carries <c>""</c> placeholders for a person to fill in: copied as-is it would
    /// have set the image model to the empty string and resolved the asset cache to nowhere, both
    /// silently.
    /// </remarks>
    static string? Setting(string key) =>
        config?[key] is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// The generation ceiling from <c>Assets:Budget</c>, or the default.
    /// </summary>
    /// <remarks>
    /// A value that is present but unusable is <b>reported and ignored</b> rather than accepted. The
    /// failure it avoids is specific: <c>int.TryParse</c> leaves <c>0</c> on failure, and a budget of
    /// zero disables requisition entirely — so a typo would present as "asset generation is broken"
    /// with nothing anywhere saying why. Zero and negatives are refused for the same reason; to turn
    /// requisition off, leave the API key unset, which says so explicitly at startup.
    /// </remarks>
    static int AssetBudgetSetting() => ResolveAssetBudget(Setting("Assets:Budget"));

    /// <summary>Reads the configured value; separated from the config lookup so it can be tested.</summary>
    internal static int ResolveAssetBudget(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return DefaultAssetBudget;

        if (int.TryParse(configured.Trim(), out var parsed) && parsed > 0) return parsed;

        Warn("Ignoring Assets:Budget='{0}': it must be a positive whole number. Using {1}.",
            configured, DefaultAssetBudget);
        return DefaultAssetBudget;
    }

    static void ConfigureAssetRequisition(string projectDir)
    {
        var apiKey = Setting("ApiKeys:GoogleAgentPlatform");
        var model = Setting("Assets:Model") ?? ImageGenerator.DefaultModel;
        var budget = AssetBudgetSetting();
        var cacheDir = Setting("Assets:CacheDir") ?? Path.Combine(projectDir, ".polson", "assets");

        var generator = string.IsNullOrWhiteSpace(apiKey) ? null : new ImageGenerator(apiKey, model);

        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            generator, new RequisitionCache(cacheDir), new AssetBudget(budget), "agent");

        if (generator is null)
        {
            Warn("Asset requisition disabled: no ApiKeys:GoogleAgentPlatform in {0}. "
               + "Copy appsettings.json.example from the repository root there and fill in the key.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
        }
        else
        {
            Info("Asset requisition enabled (model: {0}, budget: {1} generations, cache: {2}).",
                model, budget, cacheDir);
        }
    }

    /// <summary>
    /// Wires document reading, so a script can ask a question of a file in the project directory.
    /// </summary>
    /// <remarks>
    /// The project directory is passed through and is the whole containment: a document is read from
    /// inside it or not at all, exactly as <c>outFile</c> writes inside it or not at all.
    /// </remarks>
    static void ConfigureDocuments(string projectDir)
    {
        var apiKey = Setting("ApiKeys:GoogleAgentPlatform");
        var model = Setting("Documents:Model") ?? DocumentProcessor.DefaultModel;
        var budget = ResolveDocumentBudget(Setting("Documents:Budget"));

        // Inside the project rather than beside the asset cache, because an answer is about *this*
        // project's document: the same file staged into two commissions is two readings, and a
        // director clearing a project should not leave its answers behind.
        var cacheDir = Setting("Documents:CacheDir") ?? Path.Combine(projectDir, ".polson", "documents");

        JsDrawingEngine.Documents = new DocumentProcessor(
            apiKey, new DocumentBudget(budget), projectDir, model, new DocumentCache(cacheDir));

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Warn("Document reading disabled: no ApiKeys:GoogleAgentPlatform in {0}.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
        }
        else
        {
            Info("Document reading enabled (model: {0}, budget: {1} reads).", model, budget);
        }
    }

    internal static int ResolveDocumentBudget(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return DefaultDocumentBudget;
        if (int.TryParse(configured, out var parsed) && parsed >= 0) return parsed;

        Warn("Documents:Budget '{0}' is not a non-negative integer; using {1}.",
            configured, DefaultDocumentBudget);
        return DefaultDocumentBudget;
    }

    /// <summary>
    /// Wires the reference-photograph surface.
    /// </summary>
    /// <remarks>
    /// Always enabled, unlike asset requisition and research: the source needs no key and bills
    /// nothing. The budget is therefore about restraint rather than money — a graphic wanting twenty
    /// portraits is usually a graphic that should have been drawn — and about the fact that a public
    /// URL driving an unbounded fetcher is an open surface, per Milestone 6 §4.
    /// </remarks>
    static void ConfigurePhotos()
    {
        var budget = ResolvePhotoBudget(Setting("Photos:Budget"));
        var hosts = Setting("Photos:AllowedHosts")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        JsDrawingEngine.Photos = new PhotoToolkit(
            new WikimediaPhotoSource(allowedMediaHosts: hosts, userAgent: Setting("Photos:UserAgent")),
            new PhotoBudget(budget), "agent");

        Info("Reference photography enabled (budget: {0} photographs, hosts: {1}).",
            budget, string.Join(", ", hosts ?? WikimediaPhotoSource.DefaultMediaHosts));
    }

    /// <summary>Reads <c>Photos:Budget</c>; separated from the config lookup so it can be tested.</summary>
    /// <remarks>
    /// Zero is <b>accepted</b> here, unlike <c>Assets:Budget</c>, and the difference is deliberate:
    /// there is no API key to leave unset, so setting the budget to zero is the only way to turn the
    /// surface off. A negative value is still a typo and is refused.
    /// </remarks>
    internal static int ResolvePhotoBudget(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return DefaultPhotoBudget;

        if (int.TryParse(configured.Trim(), out var parsed) && parsed >= 0) return parsed;

        Warn("Ignoring Photos:Budget='{0}': it must be a whole number, zero or more. Using {1}.",
            configured, DefaultPhotoBudget);
        return DefaultPhotoBudget;
    }

    /// <summary>
    /// Wires the research transport, or leaves it null so the <c>Research</c> tool can say plainly
    /// that data cannot be sourced. Absent credentials must never read to an agent as licence to
    /// invent a figure, which is why the warning says what to do instead.
    /// </summary>
    /// <summary>
    /// The research ceiling from <c>Research:Budget</c>, or the default. Reported and ignored when
    /// unusable, for the same reason as the asset budget: a typo parsing to zero would disable
    /// research entirely and present as "research is broken" with nothing saying why.
    /// </summary>
    internal static int ResolveResearchBudget(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return ResearchRegistry.DefaultBudget;

        if (int.TryParse(configured.Trim(), out var parsed) && parsed > 0) return parsed;

        Warn("Ignoring Research:Budget='{0}': it must be a positive whole number. Using {1}.",
            configured, ResearchRegistry.DefaultBudget);
        return ResearchRegistry.DefaultBudget;
    }

    /// <summary>
    /// The processor every research run uses, from <c>Research:Processor</c>.
    /// </summary>
    /// <remarks>
    /// An unrecognised name is <b>accepted with a warning</b> rather than replaced. New tiers keep
    /// arriving — <c>core2x</c> and <c>ultra8x</c> both did — and silently substituting the default
    /// for a name we simply have not heard of would run the whole studio on the wrong engine while
    /// the configuration said otherwise. The service rejects a name it does not know, which is a
    /// clearer failure than ours would be. Only a blank falls back.
    /// </remarks>
    internal static string ResolveResearchProcessor(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return TaskProcessor.Default;

        var processor = configured.Trim();
        if (TaskSchema.CapacityOf(processor) == 0)
        {
            Warn("Research:Processor='{0}' is not a tier this build knows, so its field capacity cannot "
               + "be checked and schemas will not be warned about. Using it anyway.", processor);
        }

        return processor;
    }

    static void ConfigureResearch(string projectDir)
    {
        var apiKey = Setting("ApiKeys:Parallel");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            DrawingMcpTools.ResearchClient = null;
            Warn("Research disabled: no ApiKeys:Parallel in {0}. Agents will be told that figures "
               + "cannot be sourced, and must say so rather than inventing them.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
            return;
        }

        var budget = ResolveResearchBudget(Setting("Research:Budget"));
        var processor = ResolveResearchProcessor(Setting("Research:Processor"));

        DrawingMcpTools.ResearchClient = new ParallelClient(apiKey);
        DrawingMcpTools.ResearchProcessor = processor;
        SessionContext.ResearchBudget = budget;

        // Beside the asset and document caches, and inside the project for the same reason: the run
        // that commissioned this research is the run whose figures rest on it. Without it the data
        // dies with the session and every number drawn from it becomes unverifiable — the record
        // keeps the run id and nobody keeps what it returned.
        SessionContext.ResearchArchiveDir =
            Setting("Research:ArchiveDir") ?? Path.Combine(projectDir, ".polson", "research");
        Info("Research enabled (processor: {0}, ~{1} fields, budget: {2} runs per session).",
            processor, TaskSchema.CapacityOf(processor), budget);
    }

    /// <summary>Points <c>bitmap.trace(...)</c> at a tracer, and says which one it found.</summary>
    /// <remarks>
    /// <para>
    /// <c>Tools:Potrace</c> is an override, not a requirement: the tracer discovers a copy under
    /// <c>bin/</c> and then falls back to the PATH, which is where a Debian package lands. The setting
    /// exists for the case neither is true.
    /// </para>
    /// <para>
    /// Announced either way, because tracing is the one capability here that depends on something
    /// outside the build. A run that quietly could not trace looks identical to one that never tried,
    /// and the difference only surfaces as a deliverable full of base64.
    /// </para>
    /// </remarks>
    static void ConfigureTracing()
    {
        if (Setting("Tools:Potrace") is { Length: > 0 } configured)
            BitmapTracer.ExecutableOverride = configured;

        if (BitmapTracer.Executable is { } exe)
            Info("Tracing enabled ({0}).", exe);
        else
            Warn("Tracing unavailable: no 'potrace' found. bitmap.trace() will refuse, and a "
                 + "requisitioned matte can only reach an SVG as base64. Install it "
                 + "(Debian: apt-get install potrace) or set Tools:Potrace.");
    }

    /// <summary>Points <c>Face.detect(...)</c> at a backend, and says whether it found one.</summary>
    /// <remarks>
    /// <para>
    /// Three settings rather than one, because unlike potrace this is not a single binary: an
    /// interpreter, a script and a model bundle, each of which can be missing on its own. All three
    /// are overrides rather than requirements — the detector walks up from the assembly looking for
    /// <c>python-mediapipe/</c>, <c>src/vision/</c> and <c>models/</c>, which is the developer case.
    /// </para>
    /// <para>
    /// Announced either way, and the absence names what is missing. This capability is the difference
    /// between reading three landmarks off a photograph by eye and measuring them, so a run that
    /// quietly could not detect is worth distinguishing from one that never tried.
    /// </para>
    /// </remarks>
    static void ConfigureFaceDetection()
    {
        if (Setting("Tools:FacePython") is { Length: > 0 } python)
            FaceDetector.PythonOverride = python;
        if (Setting("Tools:FaceScript") is { Length: > 0 } script)
            FaceDetector.ScriptOverride = script;
        if (Setting("Tools:FaceModel") is { Length: > 0 } model)
            FaceDetector.ModelOverride = model;

        if (FaceDetector.Available)
            Info("Face detection enabled ({0}).", FaceDetector.Model);
        else
            Warn("Face detection unavailable: missing {0}. Face.detect() will refuse, and a mesh "
                 + "textured from a photograph needs its three landmarks supplied by hand. "
                 + "See src/vision/requirements.in.", FaceDetector.Missing);
    }

    /// <summary>Points the <c>GenerateCharacter</c> tool at its two services, and says which are there.</summary>
    /// <remarks>
    /// Two settings because the halves run on different machines as often as not: reconstruction is
    /// the TRELLIS container at <c>Trellis:BaseUrl</c>, rigging the resident rig server at
    /// <c>Characters:RigUrl</c> (<c>src/rig_server</c>). Either missing leaves the tool present and
    /// answering that it is not configured, which is what an agent should be told, rather than absent.
    /// </remarks>
    static void ConfigureCharacterGeneration()
    {
        if (Setting("Trellis:BaseUrl") is { Length: > 0 } trellis)
            DrawingMcpTools.CharacterReconstructor = new TrellisClient(trellis, Setting("ApiKeys:NvidiaNIM"));
        if (Setting("Characters:RigUrl") is { Length: > 0 } rig)
            DrawingMcpTools.CharacterRigger = new RigClient(rig);

        if (DrawingMcpTools.CharacterReconstructor is not null && DrawingMcpTools.CharacterRigger is not null)
            Info("Character generation enabled (reconstruct {0}, rig {1}).", Setting("Trellis:BaseUrl"), Setting("Characters:RigUrl"));
        else
            Warn("Character generation unavailable: set {0}. GenerateCharacter will say so when asked.",
                string.Join(" and ", new[] { DrawingMcpTools.CharacterReconstructor is null ? "Trellis:BaseUrl" : null,
                                             DrawingMcpTools.CharacterRigger is null ? "Characters:RigUrl" : null }.OfType<string>()));
    }

    static async Task HandleServerArgs(ServerOptions opts)
    {
        if (opts.Timeout.HasValue && opts.Timeout.Value > 0)
        {
            JsDrawingEngine.ScriptTimeoutSeconds = opts.Timeout.Value;
        }
        else if (config?["Server:DefaultTimeoutSeconds"] is string timeoutStr && int.TryParse(timeoutStr, out var cfgTimeout))
        {
            JsDrawingEngine.ScriptTimeoutSeconds = cfgTimeout;
        }

        ConfigureTracing();
        ConfigureFaceDetection();
        ConfigureCharacterGeneration();

        var projectDir = !string.IsNullOrWhiteSpace(opts.ProjectDir)
            ? Path.GetFullPath(opts.ProjectDir)
            : Directory.GetCurrentDirectory();

        ConfigureAssetRequisition(projectDir);
        ConfigureDocuments(projectDir);
        ConfigurePhotos();
        ConfigureResearch(projectDir);

        if (opts.Http)
        {
            Info("Starting Polson MCP Server in HTTP transport mode (timeout: {0}s, project: {1})...",
                JsDrawingEngine.ScriptTimeoutSeconds, projectDir);
            await PolsonMCPServer.RunHttpAsync(config, opts.Port, projectDir);
        }
        else
        {
            Info("Starting Polson MCP Server in stdio transport mode (timeout: {0}s, project: {1})...",
                JsDrawingEngine.ScriptTimeoutSeconds, projectDir);
            await PolsonMCPServer.RunStdioAsync(config, projectDir);
        }
    }

    /// <summary>
    /// Fails the process when the command line could not be parsed.
    /// </summary>
    /// <remarks>
    /// The parser has already written what went wrong; what was missing is the exit code. Without
    /// one, a mistyped flag and a successful run are indistinguishable to a script, and
    /// <c>create-project … || exit 1</c> never fires. Asking for help or the version is not a
    /// failure, so those keep exit 0.
    /// </remarks>
    static Task ReportParseFailure(IEnumerable<Error> errors)
    {
        var asked = errors.All(e => e is HelpRequestedError or HelpVerbRequestedError or VersionRequestedError);
        if (!asked) Environment.ExitCode = 1;

        return Task.CompletedTask;
    }

    /// <summary>
    /// A report that could not be produced exits non-zero.
    /// </summary>
    /// <remarks>
    /// <c>RunReport.Run</c> was already returning one — 1 for a directory that is not there, 0
    /// otherwise — and the dispatcher wrapped it in <c>Task.FromResult</c> and dropped it on the
    /// floor. So `polson report missing-dir` printed its error and exited 0, indistinguishable from
    /// a report and invisible to `polson report … || exit 1`. The code was right; nothing read it.
    /// </remarks>
    static Task HandleReportArgs(ReportOptions opts)
    {
        Environment.ExitCode = RunReport.Run(opts);
        return Task.CompletedTask;
    }

    /// <summary>A refused reset exits non-zero, so a script can tell it did not happen.</summary>
    static Task HandleResetArgs(ResetOptions opts)
    {
        if (!ProjectReset.Run(opts))
        {
            Environment.ExitCode = 1;
        }

        return Task.CompletedTask;
    }

    static Task HandleCreateProjectArgs(CreateProjectOptions opts)
    {
        if (!ProjectGenerator.Create(opts))
        {
            Environment.ExitCode = 1;
        }

        return Task.CompletedTask;
    }

    static Task HandleEvalArgs(EvalOptions opts)
    {
        // Reference photography, but not requisition or research. The difference is credentials:
        // this one needs none, so a script pasted into `eval` behaves as it would under the server
        // instead of refusing with NotConfigured for a reason the author cannot act on. The other
        // two stay unconfigured here because a metered surface should be turned on deliberately.
        ConfigurePhotos();

        var engine = new JsDrawingEngine();
        DrawingExecutionResult result;

        if (!string.IsNullOrWhiteSpace(opts.SvgIn))
        {
            // Rendering a file the run already produced, rather than executing anything. Without
            // this the only way to look at an artifact was to embed its markup into a script, which
            // is both awkward and a good way to corrupt the very thing being inspected.
            if (!File.Exists(opts.SvgIn))
            {
                AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape($"No such SVG file: {opts.SvgIn}")}");
                return Task.CompletedTask;
            }

            var svgXml = File.ReadAllText(opts.SvgIn);
            result = new DrawingExecutionResult
            {
                Success = true,
                SvgXml = svgXml,
                ImageFormat = SkiaImageEncoder.NormalizeFormatName(opts.Format),
                ImageBytes = SvgRenderPipeline.RenderToImage(svgXml, opts.Width, opts.Height,
                    opts.Format, opts.Quality),
            };
        }
        else if (string.IsNullOrWhiteSpace(opts.ScriptFile))
        {
            AnsiConsole.MarkupLine("[bold red]error:[/] give a script file, or --svg-in to render an SVG.");
            return Task.CompletedTask;
        }
        else
        {
            var script = File.Exists(opts.ScriptFile)
                ? File.ReadAllText(opts.ScriptFile)
                : opts.ScriptFile;

            result = engine.Execute(script, opts.Width, opts.Height, null, opts.Format, opts.Quality);
        }

        AnsiConsole.MarkupLine($"[bold]Script Execution:[/] {(result.Success ? "[green]Success[/]" : "[red]Failed[/]")} ({result.ExecutionTimeMs}ms)");

        foreach (var log in result.Logs)
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(log)}[/]");
        }

        if (!result.Success && !string.IsNullOrEmpty(result.Error))
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(result.Error)}");
        }

        if (result.ImageBytes != null && result.ImageBytes.Length > 0)
        {
            var ext = result.ImageFormat == "png" ? ".png" : (result.ImageFormat == "jpeg" ? ".jpg" : ".webp");
            var outPath = !string.IsNullOrWhiteSpace(opts.OutImg)
                ? Path.GetFullPath(opts.OutImg)
                : Path.GetFullPath($"output{ext}");

            File.WriteAllBytes(outPath, result.ImageBytes);
            AnsiConsole.MarkupLine($"[bold green]Rendered {result.ImageFormat.ToUpperInvariant()} saved:[/] {outPath} ({result.ImageBytes.Length:N0} bytes)");
        }

        // Rendering an SVG file already has the markup on disk, so echoing it back out under the
        // default name would drop an `output.svg` wherever the command was run for no gain. An
        // explicit --svg still writes, since that is a copy the caller asked for.
        var echoingInput = !string.IsNullOrWhiteSpace(opts.SvgIn) && IsDefaultOutSvg(opts.OutSvg);

        if (!echoingInput && !string.IsNullOrWhiteSpace(result.SvgXml) && !string.IsNullOrWhiteSpace(opts.OutSvg))
        {
            var svgPath = Path.GetFullPath(opts.OutSvg);
            File.WriteAllText(svgPath, result.SvgXml);
            AnsiConsole.MarkupLine($"[bold green]Rendered SVG saved:[/] {svgPath} ({result.SvgXml.Length:N0} chars)");
        }

        return Task.CompletedTask;
    }

    /// <summary>Whether <c>--svg</c> is still at its default rather than having been asked for.</summary>
    static bool IsDefaultOutSvg(string? outSvg) =>
        string.Equals(outSvg, "output.svg", StringComparison.OrdinalIgnoreCase);

    static void PrintLogo()
    {
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.MarkupLine("[bold cyan]  POLSON — Enactive Co-Creative Graphics MCP Server [/]");
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.WriteLine();
    }
    #endregion
}

