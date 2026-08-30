namespace Polson.CLI;

using System;
using CommandLine;

public class Options
{
    #region Properties
    [Option("debug", Required = false, HelpText = "Enable debug logging.")]
    public bool Debug { get; set; }

    [Option("options", Required = false, HelpText = "Additional options string.")]
    public string AdditionalOptions { get; set; } = string.Empty;
    #endregion
}

[Verb("server", isDefault: true, HelpText = "Start the Polson MCP server in stdio or HTTP mode.")]
public class ServerOptions : Options
{
    #region Properties
    [Option("http", Required = false, HelpText = "Enable the MCP server HTTP transport instead of default stdio.")]
    public bool Http { get; set; }

    [Option("port", Required = false, HelpText = "HTTP listening port (default: 5000 or from appsettings.json).")]
    public int? Port { get; set; }

    [Option("project-dir", Required = false, HelpText = "Working project directory for scene artifacts and exports.")]
    public string ProjectDir { get; set; } = string.Empty;

    [Option("timeout", Required = false, HelpText = "Script execution timeout in seconds (default: 30).")]
    public int? Timeout { get; set; }
    #endregion
}

[Verb("report", HelpText = "Summarise what actually happened in a project run, from its event log.")]
public class ReportOptions : Options
{
    #region Properties
    [Value(0, MetaName = "project-dir", Required = true, HelpText = "The project directory to report on.")]
    public string ProjectDir { get; set; } = string.Empty;

    [Option("json", Required = false, HelpText = "Emit the report as JSON rather than as a table.")]
    public bool Json { get; set; }
    #endregion
}

[Verb("create-project", HelpText = "Generate a self-contained design project directory for an agent to work in.")]
public class CreateProjectOptions : Options
{
    #region Properties
    [Value(0, MetaName = "directory", Required = true, HelpText = "Parent directory the project directory is created in.")]
    public string Directory { get; set; } = string.Empty;

    [Value(1, MetaName = "id", Required = true, HelpText = "Project id, and the name of the directory created (letters, digits, dot, underscore, dash).")]
    public string Id { get; set; } = string.Empty;

    [Value(2, MetaName = "sdk", Required = true, HelpText = "Agent SDK to target: 'agy' (Google Antigravity) or 'claude' (Claude Code). It decides what the config files are called.")]
    public string Sdk { get; set; } = string.Empty;

    [Option("workflow", Required = false, Default = "logo", HelpText = "Workflow template to generate: 'logo' or 'harness' (default: 'logo').")]
    public string Workflow { get; set; } = "logo";

    [Option("type", Required = false, HelpText = "For --workflow harness, the kind of task: 'image' (default) or 'logo'.")]
    public string Type { get; set; } = string.Empty;

    [Option("prompt", Required = false, HelpText = "The subject, in a line — a short form of --brief. Treated as untrusted data and quoted into brief.md exactly as --brief is.")]
    public string Prompt { get; set; } = string.Empty;

    [Option("standalone", Required = false, HelpText = "Also generate what the Polson orchestrator needs to host the agent itself. Without it the project is managed by a desktop or IDE host.")]
    public bool Standalone { get; set; }

    [Option("brief", Required = false, HelpText = "Path to a file holding the client brief. Use --prompt to pass the text itself. Treated as untrusted data and normalised before it is written.")]
    public string Brief { get; set; } = string.Empty;

    [Option("force", Required = false, HelpText = "Generate into a directory that already has contents.")]
    public bool Force { get; set; }
    #endregion
}

[Verb("eval", HelpText = "Execute a JavaScript drawing file and save rendered image and SVG output.")]
public class EvalOptions : Options
{
    #region Properties
    [Value(0, MetaName = "script-file", Required = true, HelpText = "Path to the JavaScript script file or raw code to execute.")]
    public string ScriptFile { get; set; } = string.Empty;

    [Option("width", Required = false, Default = 800, HelpText = "Viewport width in pixels (default: 800).")]
    public int Width { get; set; } = 800;

    [Option("height", Required = false, Default = 600, HelpText = "Viewport height in pixels (default: 600).")]
    public int Height { get; set; } = 600;

    [Option("format", Required = false, Default = "webp", HelpText = "Output image format ('webp', 'png', 'jpeg'; default: 'webp').")]
    public string Format { get; set; } = "webp";

    [Option("quality", Required = false, Default = 85, HelpText = "Image encoding quality (1-100; default: 85).")]
    public int Quality { get; set; } = 85;

    [Option("img", Required = false, HelpText = "Custom output path for the rendered image file (defaults to output.<format>).")]
    public string? OutImg { get; set; }

    [Option("svg", Required = false, Default = "output.svg", HelpText = "Output path for the rendered SVG XML file.")]
    public string? OutSvg { get; set; } = "output.svg";
    #endregion
}
