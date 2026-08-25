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
