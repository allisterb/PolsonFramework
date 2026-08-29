namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

/// <summary>
/// Generates a self-contained design project directory: brief, agent instructions, MCP wiring, and
/// the event/artifact skeleton described in <c>docs/project-layout.md</c>.
/// </summary>
/// <remarks>
/// Nothing derived from the client's brief may reach a path, a config file, or a policy entry —
/// those come from the template alone. The brief text is normalised on the way in and quoted on
/// the way out; see <see cref="SanitizeBrief"/>.
/// </remarks>
internal static class ProjectGenerator
{
    #region Constants
    /// <summary>Manifest prefix under which the templates are embedded.</summary>
    const string ResourcePrefix = "ProjectTemplate.";

    /// <summary>Cap on client-supplied brief text. Long enough for a real brief, short enough to bound the prompt.</summary>
    const int MaxBriefLength = 8000;

    /// <summary>Tools denied on every profile, and why. Order is stable so the emitted config diffs cleanly.</summary>
    static readonly string[] AlwaysDenied = ["generate_image"];

    /// <summary>Additionally denied when the agent is reachable from a public URL.</summary>
    static readonly string[] StandaloneDenied = ["run_command"];

    static readonly string[] KnownWorkflows = ["logo"];

    static readonly Regex ValidId = new(@"^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);
    #endregion

    #region Methods
    /// <summary>Creates the project directory. Returns false and reports the reason if the request is not valid.</summary>
    public static bool Create(CreateProjectOptions opts)
    {
        var workflow = opts.Workflow.ToLowerInvariant();
        if (!KnownWorkflows.Contains(workflow))
        {
            return Fail($"Unknown workflow '{opts.Workflow}'. Known: {string.Join(", ", KnownWorkflows)}.");
        }

        var profile = opts.Profile.ToLowerInvariant();
        if (profile is not ("standalone" or "managed"))
        {
            return Fail($"Unknown profile '{opts.Profile}'. Use 'standalone' or 'managed'.");
        }

        var dir = Path.GetFullPath(opts.Directory);
        var id = string.IsNullOrWhiteSpace(opts.Id) ? Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)) : opts.Id;

        // The character class permits dots, so it alone would accept "." and ".." — which are not
        // names but traversal, and would resolve to the parent directory rather than a project.
        if (!ValidId.IsMatch(id) || id.Trim('.').Length == 0)
        {
            return Fail($"Invalid project id '{id}'. Use letters, digits, dot, underscore or dash (max 64), and not only dots.");
        }

        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any() && !opts.Force)
        {
            return Fail($"Directory is not empty: {dir}\n       Use --force to generate into it anyway.");
        }

        var brief = SanitizeBrief(ReadBrief(opts.Brief));
        var createdUtc = DateTime.UtcNow.ToString("O");

        foreach (var sub in new[] { "artifacts", "scripts", "events", "session/save", "session/appdata" })
        {
            Directory.CreateDirectory(Path.Combine(dir, sub.Replace('/', Path.DirectorySeparatorChar)));
        }

        var tokens = new Dictionary<string, string>
        {
            ["PROJECT_ID"] = id,
            ["PROFILE"] = profile,
            ["CREATED_UTC"] = createdUtc,
            ["BRIEF"] = brief,
        };

        WriteText(dir, "GEMINI.md", Render(workflow, "GEMINI.md", tokens));
        WriteText(dir, "brief.md", Render(workflow, "brief.md", tokens));
        WriteText(dir, ".gitignore", GitIgnore());
        WriteJson(dir, "project.json", ProjectManifest(id, workflow, profile, createdUtc));
        WriteJson(dir, ".mcp.json", McpConfig());
        WriteJson(dir, "agent.config.json", AgentConfig(profile));

        Report(dir, id, workflow, profile);
        return true;
    }

    /// <summary>Strips every class of character used to hide text, then quotes what is left.</summary>
    /// <remarks>
    /// The same codepoint hygiene the project applies to third-party reference material, applied to
    /// the one input a stranger controls. Visible characters survive; the machinery of concealment
    /// does not. A line that reads exactly like a delimiter is removed outright, because the
    /// delimiters are the only thing separating this text from instruction.
    /// </remarks>
    internal static string SanitizeBrief(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "(no brief supplied — ask the director for one)";

        var normalised = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        var sb = new StringBuilder(normalised.Length);

        foreach (var rune in normalised.EnumerateRunes())
        {
            var v = rune.Value;
            if (v is '\n' or '\t') { sb.Append((char)v); continue; }
            if (v < 0x20 || v is >= 0x7F and <= 0x9F) continue;                 // C0 / C1 controls
            if (v is >= 0x202A and <= 0x202E or >= 0x2066 and <= 0x2069) continue; // bidi overrides & isolates
            if (v is 0x200B or 0x200C or 0x200D or 0x2060 or 0xFEFF) continue;  // zero-width, joiners, BOM
            if (v is >= 0xE0000 and <= 0xE007F) continue;                       // Unicode tag block
            sb.Append(rune);
        }

        var text = sb.ToString().Trim();
        if (text.Length > MaxBriefLength) text = string.Concat(text.AsSpan(0, MaxBriefLength), "\n… (truncated)");

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() is "BRIEF-BEGIN" or "BRIEF-END")
            {
                lines[i] = "[line removed: it read exactly as a brief delimiter]";
            }
        }
        text = string.Join('\n', lines);

        return text.Length == 0 ? "(no brief supplied — ask the director for one)" : text;
    }

    /// <summary>Treats the argument as a file path if one exists, otherwise as the brief text itself.</summary>
    static string ReadBrief(string briefArg) =>
        string.IsNullOrWhiteSpace(briefArg) ? string.Empty
        : File.Exists(briefArg) ? File.ReadAllText(briefArg)
        : briefArg;

    /// <summary>Loads an embedded template and substitutes <c>{{TOKEN}}</c> placeholders.</summary>
    static string Render(string workflow, string name, Dictionary<string, string> tokens)
    {
        var suffix = $"{ResourcePrefix}{workflow}.{name}";
        var assembly = typeof(ProjectGenerator).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new FileNotFoundException($"Template not embedded: {suffix}");

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var body = reader.ReadToEnd();

        return tokens.Aggregate(body, (acc, t) => acc.Replace($"{{{{{t.Key}}}}}", t.Value, StringComparison.Ordinal));
    }

    static object ProjectManifest(string id, string workflow, string profile, string createdUtc) => new
    {
        schema = 1,
        id,
        workflow,
        profile,
        createdUtc,
        conversationId = (string?)null,
    };

    /// <summary>
    /// Wires the agent to this CLI's own MCP server, rooted at the project directory.
    /// </summary>
    /// <remarks>
    /// The server needs no awareness of whether a human is attached: <c>ask_question</c> is a
    /// built-in of the agent runtime, handled by the host, not by us.
    /// <para>
    /// <see cref="Environment.ProcessPath"/> is the apphost when this was launched as
    /// <c>Polson.CLI.exe</c>, but the shared <c>dotnet</c> host when it was launched as
    /// <c>dotnet Polson.CLI.dll</c> — in which case the assembly is an <em>argument</em> rather than
    /// the command, and taking the process path alone wires the project to <c>dotnet server</c>,
    /// which cannot start. The generated file has to work whichever way the generator was invoked.
    /// </para>
    /// </remarks>
    static object McpConfig()
    {
        var process = Environment.ProcessPath;
        var sharedHost = process is null ||
            Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        var command = sharedHost ? process ?? "dotnet" : process;
        string[] args = sharedHost
            ? [Path.Combine(AppContext.BaseDirectory, "Polson.CLI.dll"), "server", "--project-dir", "."]
            : ["server", "--project-dir", "."];

        return new
        {
            mcpServers = new
            {
                polson = new
                {
                    command,
                    args,
                },
            },
        };
    }

    /// <summary>
    /// Tool policy and SDK directories for the orchestrator.
    /// </summary>
    /// <remarks>
    /// <c>generate_image</c> is denied on <em>both</em> profiles, and the reason is integrity rather
    /// than security: it bypasses asset requisition entirely — no budget, no cache, and no
    /// form-versus-substance check — so an agent holding it can produce a finished picture and the
    /// studio's whole premise, that the model supplies material and code supplies form, stops being
    /// true. In the managed profile this file is a record of intent only: the desktop host owns tool
    /// policy, so the prohibition is carried in GEMINI.md as a rule the agent must follow.
    /// </remarks>
    static object AgentConfig(string profile) => new
    {
        schema = 1,
        agentBehavior = "interactive",
        enforced = profile == "standalone",
        deniedTools = profile == "standalone" ? AlwaysDenied.Concat(StandaloneDenied).ToArray() : AlwaysDenied,
        saveDir = "session/save",
        appDataDir = "session/appdata",
    };

    static string GitIgnore() =>
        """
        # SDK-owned session state: regenerable, sometimes large, never the deliverable.
        session/

        # Renders are reproducible from scripts/ and are noisy in review. Commit deliberately
        # if a particular stage is worth keeping in history.
        artifacts/
        """;

    static void WriteText(string dir, string name, string body) =>
        File.WriteAllText(Path.Combine(dir, name), body.ReplaceLineEndings("\n"), new UTF8Encoding(false));

    static void WriteJson(string dir, string name, object value) =>
        WriteText(dir, name, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

    static void Report(string dir, string id, string workflow, string profile)
    {
        AnsiConsole.MarkupLine($"[bold green]Project created:[/] {Markup.Escape(dir)}");
        AnsiConsole.MarkupLine($"  id [bold]{Markup.Escape(id)}[/] · workflow [bold]{workflow}[/] · profile [bold]{profile}[/]");
        AnsiConsole.WriteLine();

        if (profile == "managed")
        {
            AnsiConsole.MarkupLine("[yellow]  Note:[/] in the managed profile the desktop host owns tool policy, so");
            AnsiConsole.MarkupLine("        [yellow]agent.config.json is advisory[/]. The image-generation prohibition is");
            AnsiConsole.MarkupLine("        carried in GEMINI.md as a rule rather than enforced here.");
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("  Next: fill in [bold]brief.md[/], then open the directory with your agent host.");
    }

    static bool Fail(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape(message)}");
        return false;
    }
    #endregion
}
