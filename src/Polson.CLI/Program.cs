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
using Polson.ExtendedMind.SpeechGeneration;
using Polson.Animation;
using Polson.Drawing.Mesh;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using Spectre.Console;

internal partial class Program : Runtime
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

    /// <summary>Clips watched per session when nothing is configured. About 2,400 input tokens per 15 s of video.</summary>
    const int DefaultVideoBudget = 40;

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

        var result = parser.ParseArguments<ServerOptions, EvalOptions, CreateProjectOptions, ResetOptions, ReportOptions, PreserveChatlogOptions, TechniquesOptions, CompareOptions>(args);
        try
        {
            await result.MapResult(
                async (ServerOptions opts) => await HandleServerArgs(opts),
                async (EvalOptions opts) => await HandleEvalArgs(opts),
                (CreateProjectOptions opts) => HandleCreateProjectArgs(opts),
                (ResetOptions opts) => HandleResetArgs(opts),
                (ReportOptions opts) => HandleReportArgs(opts),
                (PreserveChatlogOptions opts) => Task.FromResult(ChatlogPreserver.Run(opts)),
                (TechniquesOptions opts) => HandleTechniquesArgs(opts),
                (CompareOptions opts) => HandleCompareArgs(opts),
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

        // Assets.speak: the same key and the same budget, so a run's spend is one number whatever it bought.
        var speechModel = Setting("Assets:SpeechModel") ?? SpeechGenerator.DefaultModel;
        var speech = string.IsNullOrWhiteSpace(apiKey) ? null : new SpeechGenerator(apiKey, speechModel);
        var speechCacheDir = Setting("Assets:SpeechCacheDir") ?? Path.Combine(projectDir, ".polson", "speech");

        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            generator, new RequisitionCache(cacheDir), new AssetBudget(budget), "agent",
            speech, new SpeechCache(speechCacheDir));

        if (generator is null)
        {
            Warn("Asset requisition disabled: no ApiKeys:GoogleAgentPlatform in {0}. "
               + "Copy appsettings.json.example from the repository root there and fill in the key.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
        }
        else
        {
            Info("Asset requisition enabled (model: {0}, speech: {1}, budget: {2} generations, cache: {3}).",
                model, speechModel, budget, cacheDir);
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

        // PDFs and text go to Nemotron, read as page text with their evidence checked; images, clips and anything
        // Nemotron cannot answer go to Gemini whole. Either a Nemotron endpoint or the Gemini key is enough to read.
        var pages = new DocumentPages(Path.Combine(cacheDir, "pages"), Setting("Tools:Mutool"), Setting("Tools:Tesseract"));

        var processor = new NemotronDocumentProcessor(apiKey, NemotronEndpoints(), new DocumentBudget(budget),
            projectDir, pages, model, cache: new DocumentCache(cacheDir));
        JsDrawingEngine.Documents = processor;

        if (!processor.IsAvailable)
        {
            Warn("Document reading disabled: neither ApiKeys:GoogleAgentPlatform nor a Nemotron key (ApiKeys:NvidiaNIM, "
                + "ApiKeys:NebiusTokenFactory) is in {0}.", Path.Combine(AssemblyLocation, "appsettings.json"));
        }
        else
        {
            Info("Document reading enabled (first choice: {0}{1}; fallback: {2}; PDF text: {3}; OCR: {4}; budget: {5} reads).",
                processor.Model, Served(processor.Endpoints), string.IsNullOrWhiteSpace(apiKey) ? "none" : model,
                pages.Mutool ?? "no mutool", pages.Tesseract ?? "no tesseract", budget);
        }
    }

    /// <summary>
    /// Wires watching a clip, served by Nemotron whatever model the agent runs on.
    /// </summary>
    /// <remarks>
    /// The same containment and the same reasoning about the cache as <see cref="ConfigureDocuments"/>; the
    /// answers live beside the documents' rather than among them, so neither surface can serve the other's.
    /// </remarks>
    static void ConfigureVideo(string projectDir)
    {
        var budget = ResolveDocumentBudget(Setting("Video:Budget"), DefaultVideoBudget, "Video:Budget");
        var cacheDir = Setting("Video:CacheDir") ?? Path.Combine(projectDir, ".polson", "video");
        var cache = new DocumentCache(cacheDir);

        // Gemini unless Nemotron is asked for; Nemotron too when Gemini has no key, rather than no reader at all.
        var key = Setting("ApiKeys:GoogleAgentPlatform");
        var gemini = ResolveVerifyReader(Setting("Verify:Reader") ?? Environment.GetEnvironmentVariable(VerifyReaderVariable)) == VideoReader.GeminiReader;
        if (gemini && key is null)
            Warn("Verify:Reader is gemini but ApiKeys:GoogleAgentPlatform is not set; films are read by Nemotron instead.");
        var reader = gemini && key is not null
            ? new VideoReader(key, new DocumentBudget(budget), projectDir, Setting("Verify:Model") ?? VideoReader.DefaultGeminiModel, cache)
            : new VideoReader(NemotronEndpoints(), new DocumentBudget(budget), projectDir, cache: cache);
        JsDrawingEngine.Video = reader;

        if (!reader.IsAvailable)
        {
            Warn("Video watching disabled: no Nemotron endpoint is configured — ApiKeys:NvidiaNIM, or "
                + "ApiKeys:NebiusTokenFactory with NebiusTokenFactory:BaseUrl, in {0}.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
        }
        else
        {
            Info("Video watching enabled (reader: {0}; model: {1}{2}; budget: {3} clips).", reader.Reader, reader.Model,
                Served(reader.Endpoints), budget);
        }
    }

    /// <summary>The environment variable that chooses the reader when <c>Verify:Reader</c> is not set; the studio reads it too.</summary>
    internal const string VerifyReaderVariable = "POLSON_VERIFY_READER";

    /// <summary><c>gemini</c> (the default) or <c>nemotron</c>; any other value is reported and the default used.</summary>
    internal static string ResolveVerifyReader(string? configured)
    {
        var value = configured?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value == VideoReader.GeminiReader) return VideoReader.GeminiReader;
        if (value == VideoReader.NemotronReader) return VideoReader.NemotronReader;
        Warn("Ignoring Verify:Reader='{0}': it must be '{1}' or '{2}'. Using '{1}'.", configured!, VideoReader.GeminiReader, VideoReader.NemotronReader);
        return VideoReader.GeminiReader;
    }

    /// <summary>
    /// The services that serve Nemotron, in the order <c>Nemotron:Prefer</c> gives: <c>nim</c> (the default) or
    /// <c>nebius</c> first, the other as its backup. Each is left out by the reader while it has no key.
    /// </summary>
    static IReadOnlyList<NemotronEndpoint> NemotronEndpoints() => ResolveNemotronEndpoints(
        Setting("Nemotron:Prefer"),
        new NemotronEndpoint(NemotronEndpoint.Nim, Setting("NvidiaNIM:BaseUrl") ?? NemotronEndpoint.NimBaseUrl,
            Setting("ApiKeys:NvidiaNIM"), Setting("NvidiaNIM:NemotronModel") ?? NemotronEndpoint.NimModel),
        // No default base URL: an account's Token Factory host is regional.
        new NemotronEndpoint(NemotronEndpoint.Nebius, Setting("NebiusTokenFactory:BaseUrl"),
            Setting("ApiKeys:NebiusTokenFactory"), Setting("NebiusTokenFactory:NemotronModel") ?? NemotronEndpoint.NebiusModel));

    /// <summary>Orders the endpoints by <paramref name="prefer"/>; a value that names neither is reported and the default used.</summary>
    internal static IReadOnlyList<NemotronEndpoint> ResolveNemotronEndpoints(string? prefer, NemotronEndpoint nim, NemotronEndpoint nebius)
    {
        if (NemotronEndpoint.InOrder(prefer, nim, nebius) is { } ordered) return ordered;

        Warn("Ignoring Nemotron:Prefer='{0}': it must be '{1}' or '{2}'. Using '{3}'.",
            prefer!, NemotronEndpoint.Nim, NemotronEndpoint.Nebius, NemotronEndpoint.DefaultPrefer);
        return NemotronEndpoint.InOrder(null, nim, nebius)!;
    }

    /// <summary>" via nim, then nebius" — for the startup line; empty when no endpoint is configured.</summary>
    static string Served(IReadOnlyList<NemotronEndpoint> endpoints) =>
        endpoints.Count == 0 ? "" : " via " + string.Join(", then ", endpoints.Select(e => e.Name));

    internal static int ResolveDocumentBudget(string? configured) =>
        ResolveDocumentBudget(configured, DefaultDocumentBudget, "Documents:Budget");

    internal static int ResolveDocumentBudget(string? configured, int fallback, string setting)
    {
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        if (int.TryParse(configured, out var parsed) && parsed >= 0) return parsed;

        Warn("{0} '{1}' is not a non-negative integer; using {2}.", setting, configured, fallback);
        return fallback;
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
            JsDrawingEngine.Facts = null;
            Warn("Research disabled: no ApiKeys:Parallel in {0}. Agents will be told that figures "
               + "cannot be sourced, and must say so rather than inventing them.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
            return;
        }

        var budget = ResolveResearchBudget(Setting("Research:Budget"));
        var processor = ResolveResearchProcessor(Setting("Research:Processor"));

        DrawingMcpTools.ResearchClient = new ParallelClient(apiKey);
        DrawingMcpTools.ResearchProcessor = processor;
        // The same key checks the facts a read-back states (Verify's `sources`): about a second and a tenth of a cent each.
        JsDrawingEngine.Facts = new FactChecker(DrawingMcpTools.ResearchClient);
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

    /// <summary>
    /// Wires Qloo's taste graph for the <c>Taste</c> tool, or leaves it null so the tool says plainly that
    /// audience taste cannot be fetched — never that guessing is the alternative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One client for the process: the key's pacing and monthly quota are shared by every session, and each
    /// session spends from its own <c>Qloo:Budget</c> through a scoped view of it.
    /// </para>
    /// <para>
    /// The cache is shared across projects by default (<see cref="QlooCache.DefaultDirectory"/>), unlike the
    /// document cache: an answer is about an audience, not a project, and on a 10,000-a-month key a preset run
    /// in a fresh project should not ask Qloo again. It is also outside every repository, as Qloo's terms require.
    /// </para>
    /// </remarks>
    static void ConfigureTaste()
    {
        // The switch first: a key can stay in the file while Qloo is off, and the studio's form reads the same two
        // settings (orchestrator/credentials.py `qloo_enabled`), so it never offers audiences the engine will refuse.
        if (!ResolveQlooEnabled(Setting("Qloo:Enabled")))
        {
            DrawingMcpTools.TasteClient = null;
            Warn("Taste disabled: Qloo:Enabled is not true in {0}. Agents will be told audience taste cannot be fetched.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
            return;
        }

        var apiKey = Setting("ApiKeys:Qloo");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            DrawingMcpTools.TasteClient = null;
            Warn("Taste disabled: no ApiKeys:Qloo in {0}. Agents will be told audience taste cannot be fetched.",
                Path.Combine(AssemblyLocation, "appsettings.json"));
            return;
        }

        var budget = ResolveQlooSetting("Qloo:Budget", Setting("Qloo:Budget"), Polson.ExtendedMind.Taste.QlooBudget.DefaultTotal);
        var reserve = ResolveQlooSetting("Qloo:MonthlyReserve", Setting("Qloo:MonthlyReserve"), Polson.ExtendedMind.Taste.QlooBudget.DefaultMonthlyReserve);
        var cache = new Polson.ExtendedMind.Taste.QlooCache(Setting("Qloo:CacheDir"));

        try
        {
            DrawingMcpTools.TasteClient = new Polson.ExtendedMind.Taste.QlooClient(apiKey, baseUrl: Setting("Qloo:BaseUrl"), cache: cache);
        }
        catch (ArgumentException ex)
        {
            // A malformed base URL or a key with a pasted newline: configuration, said at startup rather than on the
            // first call, where it would read as Qloo being down.
            DrawingMcpTools.TasteClient = null;
            Warn("Taste disabled: {0}", ex.Message);
            return;
        }

        SessionContext.TasteBudget = budget;
        SessionContext.TasteMonthlyReserve = reserve;
        // Qloo's pictures need no key, but come only with an answer, so they are on exactly when answers are.
        JsDrawingEngine.TasteImages = new Polson.ExtendedMind.Taste.QlooImageSource(Path.Combine(cache.Location, "images"));
        Info("Taste enabled ({0}; budget: {1} requests per session; monthly reserve: {2}; cache: {3}).",
            Setting("Qloo:BaseUrl") ?? Polson.ExtendedMind.Taste.QlooClient.HackathonBaseUrl, budget, reserve, cache.Location);
    }

    /// <summary>
    /// Whether <c>Qloo:Enabled</c> leaves Qloo on: absent or blank is on, <c>true</c> or <c>false</c> as written.
    /// Anything else is reported and read as <b>off</b> — the opposite of <see cref="ResolveQlooSetting"/>'s
    /// fallback, because this is an off switch and a typo should not quietly leave a key in use.
    /// </summary>
    internal static bool ResolveQlooEnabled(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return true;
        if (bool.TryParse(configured.Trim(), out var on)) return on;

        Warn("Qloo:Enabled='{0}' is not true or false; Qloo stays off.", configured);
        return false;
    }

    /// <summary>
    /// A Qloo count from configuration, or the default. Zero is accepted — it turns the surface off or spends the
    /// month to the last request — but a value that is not a whole number is reported and ignored, because
    /// <c>int.TryParse</c> would leave zero and a typo would silently turn Qloo off.
    /// </summary>
    internal static int ResolveQlooSetting(string key, string? configured, int fallback)
    {
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        if (int.TryParse(configured.Trim(), out var parsed) && parsed >= 0) return parsed;

        Warn("Ignoring {0}='{1}': it must be a whole number, zero or more. Using {2}.", key, configured, fallback);
        return fallback;
    }

    /// <summary>The garment step of a character build: the body detector's interpreter and model, with SAM 2.</summary>
    static void ConfigureGarmentDetection()
    {
        if (Setting("Tools:GarmentScript") is { Length: > 0 } script)
            GarmentDetector.ScriptOverride = script;
        if (Setting("Tools:SamModel") is { Length: > 0 } model)
            GarmentDetector.ModelOverride = model;

        if (GarmentDetector.Available)
            Info("Garment step enabled ({0}).", GarmentDetector.Model);
        else
            Warn("Garment step unavailable: missing {0}. Characters will be built without knowing whether they need cloth.",
                 GarmentDetector.Missing);
    }

    /// <summary>Points the <c>GenerateCharacter</c> tool at its two services, and says which are there.</summary>
    /// <remarks>
    /// Two settings because the halves run on different machines as often as not: reconstruction is
    /// the TRELLIS container at <c>Trellis:BaseUrl</c>, rigging the resident rig server at
    /// <c>Characters:RigUrl</c> (<c>src/rig_server</c>). Without the rig service a build still rigs, with the
    /// solver rig alone, when its template (<c>Characters:RigTemplate</c>, or the library's) and the body
    /// detector are here. With neither, the tool stays present and answers that it is not configured, which is
    /// what an agent should be told, rather than absent.
    /// </remarks>
    static void ConfigureCharacterGeneration()
    {
        if (Setting("Trellis:BaseUrl") is { Length: > 0 } trellis)
            DrawingMcpTools.CharacterReconstructor = new TrellisClient(trellis, Setting("ApiKeys:NvidiaNIM"));
        if (Setting("Characters:RigUrl") is { Length: > 0 } rig)
            DrawingMcpTools.CharacterRigger = new RigClient(rig);

        if (Setting("Characters:RigTemplate") is { Length: > 0 } template)
            SolverRig.TemplateOverride = template;

        var rigs = string.Join(" and ", new[]
        {
            DrawingMcpTools.CharacterRigger is not null ? $"UniRig at {Setting("Characters:RigUrl")}" : null,
            SolverRig.Available ? "the solver" : null
        }.OfType<string>());
        if (DrawingMcpTools.CharacterReconstructor is not null && rigs.Length > 0)
            Info("Character generation enabled (reconstruct {0}; rig with {1}).", Setting("Trellis:BaseUrl"), rigs);
        else
            Warn("Character generation unavailable: needs {0}. GenerateCharacter will say so when asked.",
                string.Join(" and ", new[] { DrawingMcpTools.CharacterReconstructor is null ? "Trellis:BaseUrl" : null,
                                             rigs.Length == 0 ? $"Characters:RigUrl or the solver rig's {SolverRig.Missing}" : null }.OfType<string>()));
        if (DrawingMcpTools.CharacterRigger is null && SolverRig.Available)
            Info("No rig service: characters will be built with the solver rig alone.");
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
        ConfigurePoses();
        ConfigureStock();
        ConfigureFaceDetection();
        ConfigureBodyDetection();
        ConfigureGarmentDetection();
        ConfigureSpeech();
        ConfigureVideo();
        ConfigureBlender();
        ConfigureCharacterGeneration();

        var projectDir = !string.IsNullOrWhiteSpace(opts.ProjectDir)
            ? Path.GetFullPath(opts.ProjectDir)
            : Directory.GetCurrentDirectory();

        ConfigureAssetRequisition(projectDir);
        ConfigureDocuments(projectDir);
        ConfigureVideo(projectDir);

        // A read waits for most of a script's time and then answers Pending, finishing into the cache: ten seconds are
        // left for the script's own work, and the read never decides whether the script lives.
        var wait = Math.Max(5, JsDrawingEngine.ScriptTimeoutSeconds - 10);
        if (JsDrawingEngine.Documents is { } documents) documents.WaitSeconds = wait;
        if (JsDrawingEngine.Video is { } video) video.WaitSeconds = wait;
        ConfigurePhotos();
        ConfigureResearch(projectDir);
        ConfigureTaste();

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

    /// <summary>The technique catalogue, whole or one workflow's, as a list or as JSON for the studio's form.</summary>
    static Task HandleTechniquesArgs(TechniquesOptions opts)
    {
        var workflow = opts.Workflow.Trim().ToLowerInvariant();
        var list = workflow.Length == 0 ? Techniques.All.ToArray() : Techniques.For(workflow);
        if (workflow.Length > 0 && list.Length == 0)
        {
            Console.Error.WriteLine($"No techniques apply to '{workflow}'. They apply to: {string.Join(", ", Techniques.All.SelectMany(t => t.Workflows).Distinct())}.");
            Environment.ExitCode = 1;
            return Task.CompletedTask;
        }
        if (opts.Json)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(list.Select(t => new
            {
                id = t.Id, name = t.Name, workflows = t.Workflows, source = t.Source, summary = t.Summary,
                measured = t.Evidence.Judged is null,
            // Escaped to ASCII on purpose: redirected, the CLI writes in the console's code page, and a raw "§" reached the
            // studio as cp437's 0x15, a control character its JSON parser refused. People read the listing without --json.
            }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return Task.CompletedTask;
        }
        foreach (var t in list)
        {
            Console.WriteLine($"  {t.Id,-20} {t.Name} ({t.Source}) — {string.Join(", ", t.Workflows)}{(t.Evidence.Judged is null ? "" : "; judged, not measured")}");
            Console.WriteLine($"  {"",-20} {t.Summary}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// The audiences' films side by side in one MP4, for the studio's Deliverables tab. Prints what it wrote as JSON; a
    /// refusal is one line on stderr and exit 1, which the studio shows as it is.
    /// </summary>
    /// <remarks>Caught here rather than by <c>Main</c>, whose catch-all logs to file and exits 0: the studio would then
    /// offer a file that was never written.</remarks>
    static Task HandleCompareArgs(CompareOptions opts)
    {
        try
        {
            ConfigureVideo();
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(opts.Films));
            string Text(System.Text.Json.JsonElement e, string name) =>
                e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString()! : "";
            var films = json.RootElement.EnumerateArray().Select(e => new Comparison.Film(Text(e, "path"), Text(e, "title"), Text(e, "detail"))).ToArray();
            var bytes = Comparison.Write(films, opts.Output, opts.Width, opts.Sound > 0 ? opts.Sound - 1 : null);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { path = Path.GetFullPath(opts.Output), bytes }));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or TimeoutException
                                   or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
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

    /// <summary>Reference photography, but not requisition or research, for <c>eval</c>.</summary>
    static partial void ConfigureEvalExtras()
    {
        // Reference photography, but not requisition or research. The difference is credentials:
        // this one needs none, so a script pasted into `eval` behaves as it would under the server
        // instead of refusing with NotConfigured for a reason the author cannot act on. The other
        // two stay unconfigured here because a metered surface should be turned on deliberately.
        ConfigurePhotos();
    }

    static void PrintLogo()
    {
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.MarkupLine("[bold cyan]  POLSON — Enactive Co-Creative Graphics MCP Server [/]");
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.WriteLine();
    }
    #endregion
}

