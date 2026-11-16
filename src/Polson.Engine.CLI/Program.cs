namespace Polson.CLI;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Polson.MCPServer;
using Spectre.Console;

/// <summary>Polson Engine's command line: <c>eval</c> runs a script, <c>server</c> serves the engine over MCP.</summary>
/// <remarks>
/// <c>eval</c>, the tool discovery and the parse-failure exit code are the studio CLI's, from <c>Program.Engine.cs</c>.
/// This file is what differs: two verbs rather than eight, and a server that configures potrace, ffmpeg, speech, the
/// detectors and Blender, and no cloud service, because the engine has none.
/// </remarks>
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

    #region Methods
    static async Task Main(string[] args)
    {
        // A bare invocation is a request for help: `server` is the default verb, and a bare stdio server waits silently.
        if (args.Length == 0) args = ["--help"];

        var isHttp = args.Contains("--http", StringComparer.OrdinalIgnoreCase);
        var isEval = string.Equals(args[0], "eval", StringComparison.OrdinalIgnoreCase);
        var isHelp = args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase);
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

        // Stdio reserves standard output for JSON-RPC framing; every other use may print.
        var isConsole = isHttp || isEval;
        if (isConsole || isHelp)
        {
            PrintLogo();
            Runtime.WithFileAndConsoleLogging("Polson", "Engine", isDebug);
        }
        else
        {
            Runtime.WithFileLogging("Polson", "Engine", isDebug);
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
                errs => ReportParseFailure(errs));
        }
        catch (Exception ex)
        {
            if (isConsole) AnsiConsole.WriteException(ex);
            else Error("Unhandled exception: {0}", ex.Message);
            Environment.ExitCode = 1;
        }
    }

    static async Task HandleServerArgs(ServerOptions opts)
    {
        if (opts.Timeout is > 0)
        {
            JsDrawingEngine.ScriptTimeoutSeconds = opts.Timeout.Value;
        }
        else if (Setting("Server:DefaultTimeoutSeconds") is { } timeoutStr && int.TryParse(timeoutStr, out var cfgTimeout))
        {
            JsDrawingEngine.ScriptTimeoutSeconds = cfgTimeout;
        }

        ConfigureTracing();
        ConfigurePoses();
        ConfigureStock();
        ConfigureFaceDetection();
        ConfigureBodyDetection();
        ConfigureSpeech();
        ConfigureVideo();
        ConfigureBlender();

        var projectDir = !string.IsNullOrWhiteSpace(opts.ProjectDir)
            ? Path.GetFullPath(opts.ProjectDir)
            : Directory.GetCurrentDirectory();

        if (opts.Http)
        {
            Info("Starting Polson Engine MCP server in HTTP transport mode (timeout: {0}s, project: {1})...",
                JsDrawingEngine.ScriptTimeoutSeconds, projectDir);
            await PolsonMCPServer.RunHttpAsync(config, opts.Port, projectDir);
        }
        else
        {
            Info("Starting Polson Engine MCP server in stdio transport mode (timeout: {0}s, project: {1})...",
                JsDrawingEngine.ScriptTimeoutSeconds, projectDir);
            await PolsonMCPServer.RunStdioAsync(config, projectDir);
        }
    }

    static void PrintLogo()
    {
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.MarkupLine("[bold cyan]  POLSON ENGINE — Scriptable Drawing MCP Server     [/]");
        AnsiConsole.MarkupLine("[bold cyan]====================================================[/]");
        AnsiConsole.WriteLine();
    }
    #endregion
}
