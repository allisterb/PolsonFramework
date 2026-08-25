namespace Polson.CLI;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Configuration;
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

    #region Methods
    static async Task Main(string[] args)
    {
        var isHttp = args.Contains("--http", StringComparer.OrdinalIgnoreCase);
        var isEval = args.Length > 0 && string.Equals(args[0], "eval", StringComparison.OrdinalIgnoreCase);
        var isHelp = args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase);
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        if (isHttp || isEval || isHelp)
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

        var result = parser.ParseArguments<ServerOptions, EvalOptions>(args);
        try
        {
            await result.MapResult(
                async (ServerOptions opts) => await HandleServerArgs(opts),
                async (EvalOptions opts) => await HandleEvalArgs(opts),
                errs => Task.CompletedTask
            );
        }
        catch (Exception ex)
        {
            if (isHttp || isEval)
            {
                AnsiConsole.WriteException(ex);
            }
            else
            {
                Error("Unhandled exception: {0}", ex.Message);
            }
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

