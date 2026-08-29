namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using ModelContextProtocol.Server;
using Polson.MCPServer;
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

    static readonly string[] KnownWorkflows = ["logo", "harness"];

    static readonly Regex ValidId = new(@"^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);
    #endregion

    #region Types
    /// <summary>What a given agent host calls the three files it reads.</summary>
    /// <remarks>
    /// The contents are the same either way — the same MCP wiring, the same instructions. Only the
    /// names differ, and that is the entire difference between an Antigravity project and a Claude
    /// Code one, which is why the SDK is the first thing <see cref="Create"/> branches on.
    /// <para>
    /// Antigravity gets the wiring twice, at the project root and inside <c>.agents/</c>, because the
    /// working harness carries both and which one the desktop host actually reads is unverified. Two
    /// serialisations of one object cannot drift; a wrong guess here would leave the agent with no
    /// MCP server and, per the run record, no error either — the worst failure mode we have found.
    /// Drop one once the desktop host confirms which it reads.
    /// </para>
    /// </remarks>
    readonly record struct HostFiles(string Instructions, string[] McpConfig, string Permissions)
    {
        public static HostFiles For(string sdk) => sdk == "agy"
            ? new("GEMINI.md", [".agents/mcp_config.json", "mcp_config.json"], ".agents/settings.json")
            : new("CLAUDE.md", [".mcp.json"], ".claude/settings.local.json");
    }
    #endregion

    #region Methods
    /// <summary>Creates the project directory. Returns false and reports the reason if the request is not valid.</summary>
    /// <remarks>
    /// Two branch points, in this order. The <b>SDK</b> decides what the host's files are
    /// <em>called</em> — that is the only thing that actually differs between an Antigravity project
    /// and a Claude Code one, since both read the same MCP wiring under different names.
    /// <b>Standalone</b> then <em>adds</em> what our own orchestrator needs on top. It is strictly a
    /// superset, so the file set itself says which kind of project this is, which is checkable —
    /// a flag inside a JSON file is not, and an earlier version got that wrong: both profiles
    /// emitted identical files and differed only in a field nothing enforced.
    /// </remarks>
    public static bool Create(CreateProjectOptions opts)
    {
        var workflow = opts.Workflow.ToLowerInvariant();
        if (!KnownWorkflows.Contains(workflow))
        {
            return Fail($"Unknown workflow '{opts.Workflow}'. Known: {string.Join(", ", KnownWorkflows)}.");
        }

        var sdk = opts.Sdk.ToLowerInvariant();
        if (sdk is not ("agy" or "claude"))
        {
            return Fail($"Unknown SDK '{opts.Sdk}'. Use 'agy' (Google Antigravity) or 'claude' (Claude Code).");
        }

        // Reported rather than thrown: this is a plausible thing to type, not a bug, and every other
        // rejection here prints a sentence instead of a stack trace.
        if (sdk == "claude" && opts.Standalone)
        {
            return Fail("'claude --standalone' is not supported yet — the orchestrator builds Antigravity SDK\n"
                      + "       configurations only. Generate it managed, or use 'agy' for a standalone project.");
        }

        var id = opts.Id.Trim();

        // The character class permits dots, so it alone would accept "." and ".." — which are not
        // names but traversal, and would resolve to the parent directory rather than a project.
        if (!ValidId.IsMatch(id) || id.Trim('.').Length == 0)
        {
            return Fail($"Invalid project id '{id}'. Use letters, digits, dot, underscore or dash (max 64), and not only dots.");
        }

        // The id names the directory, so one parent holds many projects. Validated first, above:
        // it becomes a path segment here.
        var dir = Path.Combine(Path.GetFullPath(opts.Directory), id);

        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any() && !opts.Force)
        {
            return Fail($"Directory is not empty: {dir}\n       Use --force to generate into it anyway.");
        }

        var host = HostFiles.For(sdk);
        var profile = opts.Standalone ? "standalone" : "managed";
        var brief = SanitizeBrief(ReadBrief(opts.Brief));
        var createdUtc = DateTime.UtcNow.ToString("O");

        var dirs = new List<string> { "artifacts", "scripts", "events" };
        if (opts.Standalone)
        {
            // LocalAgentConfig.save_dir and app_data_dir — a Python SDK concept, meaningless when a
            // desktop host owns the session.
            dirs.AddRange(["session/save", "session/appdata"]);
        }

        foreach (var sub in dirs)
        {
            Directory.CreateDirectory(Path.Combine(dir, sub.Replace('/', Path.DirectorySeparatorChar)));
        }

        var tokens = new Dictionary<string, string>
        {
            ["PROJECT_ID"] = id,
            ["PROFILE"] = profile,
            ["CREATED_UTC"] = createdUtc,
            ["BRIEF"] = brief,
            ["INSTRUCTIONS_FILE"] = host.Instructions,
            ["ISOLATION"] = Isolation(sdk),
        };

        WriteText(dir, host.Instructions, Render(workflow, "instructions.md", tokens));
        WriteText(dir, "brief.md", Render(workflow, "brief.md", tokens));
        WriteText(dir, ".gitignore", GitIgnore(opts.Standalone));
        WriteJson(dir, "project.json", ProjectManifest(id, workflow, sdk, profile, createdUtc, opts.Standalone));

        var wiring = McpConfig();
        foreach (var name in host.McpConfig) WriteJson(dir, name, wiring);
        WriteJson(dir, host.Permissions, Permissions(sdk));

        if (opts.Standalone)
        {
            WriteJson(dir, "agent.config.json", AgentConfig());
        }

        Report(dir, id, workflow, sdk, opts.Standalone, host);
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

    /// <summary>
    /// The manifest, which is how a reader — and the orchestrator — knows what kind of project this is.
    /// </summary>
    /// <remarks>
    /// <c>sdk</c> is what tells the orchestrator which wiring file to open, so it never has to guess
    /// by probing filenames. <c>conversationId</c> is standalone-only state: resume belongs to us,
    /// and a desktop host neither writes nor reads it.
    /// </remarks>
    static object ProjectManifest(string id, string workflow, string sdk, string profile, string createdUtc, bool standalone) =>
        standalone
            ? new { schema = 1, id, workflow, sdk, profile, createdUtc, conversationId = (string?)null }
            : (object)new { schema = 1, id, workflow, sdk, profile, createdUtc };

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
    static object AgentConfig() => new
    {
        schema = 1,
        agentBehavior = "interactive",
        deniedTools = AlwaysDenied.Concat(StandaloneDenied).ToArray(),
        saveDir = "session/save",
        appDataDir = "session/appdata",
    };

    /// <summary>
    /// The permission file the host reads. Its schema belongs to the host, not to us.
    /// </summary>
    /// <remarks>
    /// Antigravity permissions name <em>tools</em>, never paths — <c>mcp:polson:*</c> and
    /// <c>bash:node*</c> work, <c>read:C:/...</c> does nothing at all and fails open. This emits only
    /// the two namespaces a working example proves, because an entry whose key the host does not
    /// recognise is silently inert, which is indistinguishable from a rule that is being enforced.
    /// <para>
    /// Notably absent for that reason: a deny for <c>generate_image</c>. Whether the desktop host can
    /// deny a builtin, and under what key, is unverified — so in a managed project the prohibition
    /// stays a rule in the instructions file rather than a guess in a config file. Claude Code has no
    /// image-generation tool, so there is nothing to deny there either way.
    /// </para>
    /// </remarks>
    static object Permissions(string sdk) => sdk == "agy"
        ? new
        {
            permissions = new
            {
                allow = new[] { "mcp:polson:*" },
                deny = new[] { "bash:node*", "bash:python*", "bash:npm*", "bash:dotnet*" },
            },
        }
        : (object)new
        {
            permissions = new
            {
                defaultMode = "default",
                allow = ToolNames().Select(t => $"mcp__polson__{t}").Concat(["Read", "Write", "Edit", "Glob", "Grep"]).ToArray(),
                deny = new[] { "Bash", "BashOutput", "KillShell", "WebFetch", "WebSearch" },
            },
        };

    /// <summary>
    /// How the harness's isolation rule is actually backed on this host — which is not the same
    /// sentence for both, and must not be written as though it were.
    /// </summary>
    /// <remarks>
    /// Claude Code's permission rules take paths, so a deny there really does refuse a read.
    /// Antigravity's name tools and commands only, so no rule can stop the agent reading Polson's
    /// source; what stops it is a person declining a prompt. Telling an Antigravity agent its reads
    /// are enforced would be false, and a harness that lies about its own boundaries measures
    /// nothing — the whole value of this run is being able to say afterwards whether the published
    /// API was sufficient, which only holds if the source really went unread.
    /// <para>
    /// Note what is <em>not</em> claimed for Claude Code either: the generated permission file denies
    /// the shell and the network, but not reads outside the project, because what would need denying
    /// depends on where the project was generated and the generator cannot know it. Real path
    /// isolation is a deployment step, and saying so beats implying a rule that is not there.
    /// </para>
    /// </remarks>
    static string Isolation(string sdk) => sdk == "agy"
        ? """
          ### This limit is not enforced by a permissions file. It is on you.

          Antigravity's permissions name **tools and commands** — `mcp:polson:*`, `bash:node*` — never
          paths. No rule in this harness denies reading Polson's source. If you ask to read it, your
          host will put a prompt in front of a person, and that person will decline. The isolation is
          a convention you keep, backed by someone saying no.

          So do not test it. **Do not attempt to read anything outside this folder.** Attempting it is
          not a harness check; it interrupts a person, and repeated prompts are how a careless
          approval eventually happens and quietly ruins the run.
          """
        : """
          ### Some of this is enforced, and some of it is on you.

          `.claude/settings.local.json` denies the shell (`Bash`) and the network (`WebFetch`,
          `WebSearch`), so all code execution goes through `ExecuteScript` — the thing under test.

          It does **not** deny reads outside this folder: what would need denying depends on where
          this project was generated, so the generator does not guess at it. Reading Polson's source
          is therefore a convention you keep, not a wall you will hit. **Do not attempt it.**

          If this harness is being run somewhere that matters, add the `Read(...)` denies for the
          source tree by hand before starting, and note in `findings.md` that you did.
          """;

    /// <summary>The MCP server's tool names, read from the server itself so the allowlist cannot go stale.</summary>
    /// <remarks>
    /// A hand-written list would silently stop covering a tool the day one is added, and the symptom
    /// would be an agent told it lacks a capability it actually has.
    /// </remarks>
    static string[] ToolNames() =>
        typeof(DrawingMcpTools).GetMethods()
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    static string GitIgnore(bool standalone) =>
        (standalone
            ? """
              # SDK-owned session state: regenerable, sometimes large, never the deliverable.
              session/


              """
            : "")
        + """
          # Renders are reproducible from scripts/ and are noisy in review. Commit deliberately
          # if a particular stage is worth keeping in history.
          artifacts/
          """;

    /// <summary>Writes a file, creating the directory a nested name implies.</summary>
    static void WriteText(string dir, string name, string body)
    {
        var path = Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body.ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }

    static void WriteJson(string dir, string name, object value) =>
        WriteText(dir, name, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

    static void Report(string dir, string id, string workflow, string sdk, bool standalone, HostFiles host)
    {
        AnsiConsole.MarkupLine($"[bold green]Project created:[/] {Markup.Escape(dir)}");
        AnsiConsole.MarkupLine($"  id [bold]{Markup.Escape(id)}[/] · workflow [bold]{workflow}[/] · sdk [bold]{sdk}[/] · "
                             + $"[bold]{(standalone ? "standalone" : "managed")}[/]");
        AnsiConsole.WriteLine();

        if (!standalone)
        {
            AnsiConsole.MarkupLine("[yellow]  Note:[/] the host owns tool policy in a managed project, so the");
            AnsiConsole.MarkupLine($"        image-generation prohibition is carried in [bold]{Markup.Escape(host.Instructions)}[/] as a rule");
            AnsiConsole.MarkupLine("        the agent must follow, not as a config entry we can enforce.");
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine($"  Next: fill in [bold]brief.md[/], then");
        AnsiConsole.MarkupLine(standalone
            ? $"        [bold]python src/webapp/run_studio.py {Markup.Escape(dir)}[/]"
            : "        open the directory with your agent host.");
    }

    static bool Fail(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape(message)}");
        return false;
    }
    #endregion
}
