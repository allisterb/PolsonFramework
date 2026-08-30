namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Polson.ExtendedMind.ImageGeneration;
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
    /// Generations allowed per server run when <c>Assets:Budget</c> is not configured.
    /// </summary>
    /// <remarks>
    /// Deliberately small. An image costs roughly 1,300 tokens whatever is asked for, so a default
    /// that quietly allows hundreds would turn a stuck retry loop into real money.
    /// </remarks>
    const int DefaultAssetBudget = 12;
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
        var isHelp = args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase);
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        // Every verb but the default stdio server is free to write to standard output; stdio reserves it for JSON-RPC framing.
        var isConsoleVerb = isHttp || isEval || isCreate;

        if (isConsoleVerb || isHelp)
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

        var result = parser.ParseArguments<ServerOptions, EvalOptions, CreateProjectOptions, ReportOptions>(args);
        try
        {
            await result.MapResult(
                async (ServerOptions opts) => await HandleServerArgs(opts),
                async (EvalOptions opts) => await HandleEvalArgs(opts),
                (CreateProjectOptions opts) => HandleCreateProjectArgs(opts),
                (ReportOptions opts) => Task.FromResult(RunReport.Run(opts)),
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
    static void ConfigureAssetRequisition(string projectDir)
    {
        var apiKey = config?["ApiKeys:GoogleAgentPlatform"];
        var model = config?["Assets:Model"] ?? ImageGenerator.DefaultModel;
        var budget = int.TryParse(config?["Assets:Budget"], out var b) ? b : DefaultAssetBudget;
        var cacheDir = config?["Assets:CacheDir"] ?? Path.Combine(projectDir, ".polson", "assets");

        var generator = string.IsNullOrWhiteSpace(apiKey) ? null : new ImageGenerator(apiKey, model);

        JsDrawingEngine.Assets = new AssetRequisitionToolkit(
            generator, new RequisitionCache(cacheDir), new AssetBudget(budget), "agent");

        if (generator is null)
        {
            Warn("Asset requisition disabled: no ApiKeys:GoogleAgentPlatform in appsettings.json.");
        }
        else
        {
            Info("Asset requisition enabled (model: {0}, budget: {1} generations, cache: {2}).",
                model, budget, cacheDir);
        }
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

        var projectDir = !string.IsNullOrWhiteSpace(opts.ProjectDir)
            ? Path.GetFullPath(opts.ProjectDir)
            : Directory.GetCurrentDirectory();

        ConfigureAssetRequisition(projectDir);

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
        var script = File.Exists(opts.ScriptFile)
            ? File.ReadAllText(opts.ScriptFile)
            : opts.ScriptFile;

        var engine = new JsDrawingEngine();
        var result = engine.Execute(script, opts.Width, opts.Height, null, opts.Format, opts.Quality);

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

        if (!string.IsNullOrWhiteSpace(result.SvgXml) && !string.IsNullOrWhiteSpace(opts.OutSvg))
        {
            var svgPath = Path.GetFullPath(opts.OutSvg);
            File.WriteAllText(svgPath, result.SvgXml);
            AnsiConsole.MarkupLine($"[bold green]Rendered SVG saved:[/] {svgPath} ({result.SvgXml.Length:N0} chars)");
        }

        return Task.CompletedTask;
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

