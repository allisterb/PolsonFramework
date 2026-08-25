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
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        if (isHttp || args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            PrintLogo();
            Runtime.WithFileAndConsoleLogging("Polson", "CLI", isDebug);
        }
        else
        {
            // Stdio transport: standard output is reserved for JSON-RPC messages; logs go to files/stderr
            Runtime.WithFileLogging("Polson", "CLI", isDebug);
        }

        var parser = new Parser(with =>
        {
            with.CaseInsensitiveEnumValues = true;
            with.HelpWriter = Console.Error;
        });

        var result = parser.ParseArguments<ServerOptions>(args);
        try
        {
            await result.MapResult(
                async opts => await HandleServerArgs(opts),
                errs => Task.CompletedTask
            );
        }
        catch (Exception ex)
        {
            if (isHttp)
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

    static void PrintLogo()
    {
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.MarkupLine("[bold cyan]  POLSON — Enactive Co-Creative Graphics MCP Server [/]");
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.WriteLine();
    }
    #endregion
}

