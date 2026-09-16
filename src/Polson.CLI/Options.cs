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

[Verb("preserve-chatlog", Hidden = true, HelpText = "Hook: read the host's hook payload from stdin and copy its chat transcript into the project's events/ directory. Wired in by create-project.")]
public class PreserveChatlogOptions : Options
{
    #region Properties
    [Option("host", Required = false, HelpText = "Which host's transcript store to search when the payload does not name a transcript: 'agy' or 'claude'. Baked in by create-project.")]
    public string Host { get; set; } = string.Empty;

    [Option("project-dir", Required = false, HelpText = "The project to preserve into. Baked in by create-project, because a hook runs in whatever directory its host chose and not necessarily the project's.")]
    public string ProjectDir { get; set; } = string.Empty;
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

[Verb("reset", HelpText = "Clear a project's previous run so it can be run again, keeping the brief, the instructions and everything else that was generated.")]
public class ResetOptions : Options
{
    #region Properties
    [Value(0, MetaName = "project-dir", Required = true, HelpText = "The project directory to clear.")]
    public string ProjectDir { get; set; } = string.Empty;

    // Not `--archive`, which is what archiving would be called if it were the option rather than the
    // behaviour. It is the default here for the reason `create-project --reset` made it the default
    // there: deleting a run once cost the only measurements that said what a change was worth.
    [Option("delete", Required = false, HelpText = "Delete the previous run instead of archiving it under previous/. Irreversible — the default moves everything aside and keeps it.")]
    public bool Delete { get; set; }
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

    [Value(2, MetaName = "sdk", Required = false, HelpText = "Agent SDK to target: 'agy' (Google Antigravity, the default) or 'claude' (Claude Code). It decides what the config files are called. Omit it for an Antigravity project.")]
    public string Sdk { get; set; } = string.Empty;

    // No `Default`, deliberately: the parser cannot distinguish an omitted flag from one typed with
    // the default value, and regenerating an existing project needs that distinction — see
    // ProjectGenerator's workflow resolution. Omitted means "logo" for a new project and "whatever
    // this project already is" for one being reset.
    [Option("workflow", Required = false, HelpText = "Workflow template to generate: 'logo' (the default), 'infographic', 'drawing', 'comic', 'painting', 'comic_studio' (multi-agent) or 'harness'. Workflows are discovered from the embedded templates, so an unknown name lists what is actually available. Omitted on --reset or --force, the workflow recorded in project.json is kept.")]
    public string Workflow { get; set; } = string.Empty;

    [Option("type", Required = false, HelpText = "Narrows the chosen workflow's direction. Which types exist depends on the workflow and 'comic_studio' has none, so an unknown name lists the ones on offer. Omitted on --reset or --force, the type recorded in project.json is kept when the workflow is unchanged.")]
    public string Type { get; set; } = string.Empty;

    [Option("prompt", Required = false, HelpText = "The subject, in a line — a short form of --brief. Treated as untrusted data and quoted into brief.md exactly as --brief is.")]
    public string Prompt { get; set; } = string.Empty;

    [Option("deadline", Required = false, HelpText = "Minutes the commission is allowed to take, written into project.json and stated in the instructions so the agent can plan against it. Omit it for the workflow's own default — a logo is quarter of an hour, a study painting is two. Pass 0 for no deadline.")]
    public int? Deadline { get; set; }

    [Option("test", Required = false, HelpText = "Run this workflow as an evaluation of the framework as well as a commission: the agent reports friction, errors and gaps in findings.md, and reading Polson's own source is denied so the run can say whether the published API was sufficient. Applies to any workflow, each of which exercises a different part of the stack.")]
    public bool Test { get; set; }

    [Option("inline-manuals", Required = false, HelpText = "Write the workflow's judgment manuals into the instructions file instead of leaving them to be searched for. Costs tokens on every turn — roughly 15k for `drawing` — but they are a stable prefix, so a host that caches pays a fraction of that after the first turn. Worth it in managed mode, where the host's own subscription covers the spend; think harder about it on a metered deployment. Measured: pointing at a manual has never reliably produced a read, and `polson://manual/index` was never opened in any run.")]
    public bool InlineManuals { get; set; }

    [Option("standalone", Required = false, HelpText = "Also generate what the Polson orchestrator needs to host the agent itself. Without it the project is managed by a desktop or IDE host.")]
    public bool Standalone { get; set; }

    [Option("brief", Required = false, HelpText = "Path to a file holding the client brief. Use --prompt to pass the text itself. Treated as untrusted data and normalised before it is written.")]
    public string Brief { get; set; } = string.Empty;

    [Option("force", Required = false, HelpText = "Generate into a directory that already has contents. Overwrites every generated file, including brief.md.")]
    public bool Force { get; set; }

    [Option("reset", Required = false, HelpText = "Clear a previous run — events/, scripts/ and artifacts/ — and regenerate every generated file including the instructions, keeping only brief.md as you wrote it.")]
    public bool Reset { get; set; }
    #endregion
}

[Verb("eval", HelpText = "Execute a JavaScript drawing file and save rendered image and SVG output.")]
public class EvalOptions : Options
{
    #region Properties
    [Value(0, MetaName = "script-file", Required = false, HelpText = "Path to the JavaScript script file or raw code to execute. Omit it when using --svg-in.")]
    public string ScriptFile { get; set; } = string.Empty;

    [Option("svg-in", Required = false, HelpText = "Render an existing .svg file instead of running a script. Useful for inspecting an artifact a run left behind.")]
    public string SvgIn { get; set; } = string.Empty;

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
