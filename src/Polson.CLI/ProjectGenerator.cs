namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// The type each workflow falls back to when none is given. Absent means the type is optional and
    /// the workflow renders without one.
    /// </summary>
    /// <remarks>
    /// The only workflow-specific knowledge kept in code. Everything else about a workflow — that it
    /// exists at all, and which types it offers — is discovered from its embedded templates, so
    /// adding either is adding a file rather than editing this class.
    /// </remarks>
    static readonly Dictionary<string, string> DefaultTypes = new() { ["harness"] = "image", ["drawing"] = "review", ["comic"] = "review" };

    /// <summary>Used when the optional third positional is omitted.</summary>
    const string DefaultSdk = "agy";

    static readonly Regex ValidId = new(@"^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);
    #endregion

    #region Types
    /// <summary>What a given agent host calls the three files it reads.</summary>
    /// <remarks>
    /// The contents are the same either way — the same MCP wiring, the same instructions. Only the
    /// names differ, and that is the entire difference between an Antigravity project and a Claude
    /// Code one, which is why the SDK is the first thing <see cref="Create"/> branches on.
    /// <para>
    /// Antigravity's wiring goes in <c>.agents/</c>, which is its canonical workspace configuration
    /// directory — alongside <c>.agents/settings.json</c>, <c>rules/</c> and <c>skills/</c>. It also
    /// reads a copy at the project root, but only as a fallback for generic MCP tooling, and one file
    /// is the honest state now that the question is settled. This was written to both locations while
    /// which one the host read was unknown.
    /// </para>
    /// </remarks>
    readonly record struct HostFiles(string Instructions, string McpConfig, string Permissions)
    {
        public static HostFiles For(string sdk) => sdk == "agy"
            ? new("GEMINI.md", ".agents/mcp_config.json", ".agents/settings.json")
            : new("CLAUDE.md", ".mcp.json", ".claude/settings.local.json");
    }

    /// <summary>
    /// One specialised agent in a multi-agent workflow, read from the role file itself.
    /// </summary>
    /// <remarks>
    /// Both fields come from the file rather than a list kept beside it: the name from
    /// <c>roles/NN_name.md</c>, the description from its first heading. A workflow gains an agent by
    /// gaining a file, and the registry cannot disagree with the prompt it points at.
    /// </remarks>
    /// <param name="Body">
    /// The rendered spec. Carried because the two hosts register a role differently: Antigravity
    /// points at the file with <c>promptFile</c>, while Claude Code has no such indirection and
    /// needs the prompt in the agent definition itself.
    /// </param>
    readonly record struct Role(string Name, string Description, string PromptFile, string Body)
    {
        public static Role? From(string path, string body)
        {
            if (!path.StartsWith("roles/", StringComparison.Ordinal)) return null;

            // "roles/01_penciler.md" -> "penciler"; the number orders the pipeline and is not the name.
            var stem = Path.GetFileNameWithoutExtension(path);
            var name = stem.Split('_', 2) is [var lead, var rest] && lead.All(char.IsDigit) ? rest : stem;

            var heading = body.Split('\n').FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal)) ?? name;
            var description = heading[2..].Trim();
            if (description.StartsWith("Role:", StringComparison.OrdinalIgnoreCase))
            {
                description = description["Role:".Length..].Trim();
            }

            return new Role(name, description, path, body);
        }
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

        // The third positional is optional and defaults to Antigravity, which is the host the studio
        // is built around; naming 'claude' is the deliberate act. Because it may now be omitted, a
        // stray token in that slot is most often a workflow or type the caller meant to flag, so the
        // error says so rather than only naming the two SDKs.
        var sdk = opts.Sdk.Trim().ToLowerInvariant();
        if (sdk.Length == 0)
        {
            sdk = DefaultSdk;
        }
        else if (sdk is not ("agy" or "claude"))
        {
            var hint = KnownWorkflows.Contains(sdk)
                ? $" '{opts.Sdk}' is a workflow — did you mean --workflow {sdk}?"
                : string.Empty;
            return Fail($"Unknown SDK '{opts.Sdk}'. Use 'agy' (Google Antigravity) or 'claude' (Claude Code), "
                + $"or omit it for '{DefaultSdk}'.{hint}");
        }

        // What a type means is the workflow's business; whether one was offered is not. Refused
        // rather than ignored, because a flag that silently does nothing reads as a choice honoured.
        var offered = TypesFor(workflow);
        var type = opts.Type.Trim().ToLowerInvariant();

        if (type.Length > 0 && offered.Length == 0)
        {
            return Fail($"The '{workflow}' workflow has no types, so --type does not apply to it.");
        }
        if (type.Length > 0 && !offered.Contains(type))
        {
            return Fail($"Unknown type '{opts.Type}' for the '{workflow}' workflow. Known: {string.Join(", ", offered)}.");
        }
        if (type.Length == 0 && DefaultTypes.TryGetValue(workflow, out var fallback))
        {
            type = fallback;
        }

        // One channel for what to make, so there is one place the trust boundary sits: --brief reads
        // a file, --prompt takes the text directly, and both are quoted into brief.md as data.
        // Taking both would mean silently dropping one.
        if (opts.Brief.Length > 0 && opts.Prompt.Length > 0)
        {
            return Fail("Give --brief or --prompt, not both. --brief reads a file; --prompt takes the text itself.");
        }

        // A path, never the text. When this accepted either, a mistyped path quietly *became* the
        // brief, and the agent read its client brief as `C:\typo\brief.txt` — a failure with no
        // symptom until someone looked at the generated file.
        if (opts.Brief.Length > 0 && !File.Exists(opts.Brief))
        {
            return Fail($"No such brief file: {opts.Brief}\n"
                      + "       --brief takes a path. To pass the text itself, use --prompt.");
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

        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any() && !opts.Force && !opts.Reset)
        {
            return Fail($"Directory is not empty: {dir}\n"
                + "       Use --reset to clear the previous run and keep your brief,\n"
                + "       or --force to overwrite every generated file including brief.md.");
        }

        // --reset clears the run before anything is written, so the regenerated project is what a
        // fresh one would be. Reported rather than silent: deleting a previous run's work is the
        // one thing here that cannot be undone.
        var preserved = opts.Reset ? ClearRun(dir, HostFiles.For(sdk)) : [];

        var host = HostFiles.For(sdk);
        // A reset clears the run; it does not decide what the project is. Without this, resetting a
        // standalone project without repeating --standalone silently downgraded it to managed —
        // rewriting project.json, dropping the orchestrator's own files, and taking `session/` out
        // of .gitignore, which exposed SDK state already on disk. The flag can still promote a
        // managed project, because that is someone asking for it; only the silent demotion is wrong.
        var standalone = opts.Standalone || (opts.Reset && WasStandalone(dir));
        var profile = standalone ? "standalone" : "managed";

        // Whether the Polson orchestrator can host this project's agent — which is a fact about the
        // SDK, not about the profile. Every Antigravity project gets the orchestrator's config, so a
        // project generated for a desktop host can also be run from the terminal or the browser
        // without being regenerated. `profile` survives as a label for what it was made for.
        //
        // The two policies stay separate on purpose, because they protect different situations. The
        // host's permission file keeps the managed set; `agent.config.json` carries the standalone
        // set, including the `run_command` denial that exists because a public URL must not reach a
        // shell. Each is read by exactly one runtime, so the denial that matters arrives with the
        // runtime that needs it instead of being chosen at generation time by guessing.
        var orchestratable = sdk == "agy";
        string briefText;
        try
        {
            briefText = ReadBrief(opts);
        }
        catch (Exception ex)   // present but unreadable: a directory, a lock, a permission
        {
            return Fail($"Could not read the brief file {opts.Brief}: {ex.Message}");
        }

        var brief = SanitizeBrief(briefText);
        var createdUtc = DateTime.UtcNow.ToString("O");
        var deadline = DeadlineFor(workflow, opts.Deadline);

        // The orchestrator's session directories. Written for every Antigravity project rather than
        // only the standalone ones, because any of them may now be run from the orchestrator — see
        // the note on agent.config.json below. Empty and inert under a desktop host, which owns its
        // own session.
        var dirs = new List<string> { "artifacts", "scripts", "events" };
        if (orchestratable)
        {
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
            ["TYPE"] = type.Length > 0 ? Render(workflow, $"type.{type}.md", []) : "",

            // Shared across every workflow, and rendered from one file so the four instruction
            // templates cannot drift apart on the rule that matters most. `_shared` is not a
            // workflow: it carries no instructions.md, which is the only thing discovery looks for.
            ["ENGINE_ONLY"] = Render("_shared", "engine_only.md", []),

            // Stay in the project directory. Shared because it is true of every workflow, and stated
            // as a prompt rule because that is what has actually worked: the engine-only guardrail
            // above dropped permission prompts sharply where wrangling permission syntax had not.
            // It also replaces `{{ISOLATION}}` in the design workflows — see `Isolation`.
            ["PROJECT_DIR"] = Render("_shared", "project_dir.md", []),

            // How to open when the visitor had nothing to say yet. Shared for the same reason:
            // every workflow can be started from the web form with an empty brief, so every one of
            // them needs the same answer to it.
            ["BLANK_BRIEF"] = Render("_shared", "blank_brief.md", []),

            // How to write the files the workflow asks for. Shared because the trap is the host's,
            // not the workflow's: every one of them asks for plain files in the project directory.
            ["DELIVERABLES"] = Render("_shared", "deliverables.md", []),

            // What earlier runs of this project left behind, and how to write for the next one.
            // Shared because a tool nobody is told to call is never called: `artifact.read` was
            // instrumented and stayed at zero across whole runs until the instructions said to look.
            ["RECALL"] = Render("_shared", "recall.md", []),

            // Run the artwork from a file rather than re-sending it. Shared because the cost is the
            // medium's rather than any one workflow's: every workflow that asks for a consolidated
            // `artwork.js` makes re-sending it the only way to run it, and a measured run spent most
            // of its time doing exactly that.
            ["SCRIPT_FILE"] = Render("_shared", "script_file.md", []),

            // How long the commission has, and how to work to it. Shared because the discipline is
            // the same whatever is being drawn, and stated up front because a deadline learned about
            // at three-quarters spent is only bad news — by then it cannot be planned against.
            // Empty for a workflow with no deadline, so nothing claims a limit that is not enforced.
            ["DEADLINE"] = deadline > 0
                ? Render("_shared", "deadline.md",
                    new Dictionary<string, string> { ["DEADLINE_MINUTES"] = deadline.ToString() })
                : string.Empty,
        };

        // The instructions are always rewritten: they are the project's system prompt, generated
        // from the template, and a reset that kept them would freeze a project on whatever the
        // template said the day it was made.
        WriteText(dir, host.Instructions, Render(workflow, "instructions.md", tokens));

        // The brief is the one file a person authors, so a reset leaves it exactly as it is —
        // re-rendering it would discard the client's brief and, for a data-driven workflow, the
        // whole hand-typed data table with it.
        if (!preserved.Contains("brief.md"))
        {
            WriteText(dir, "brief.md", Render(workflow, "brief.md", tokens));
        }

        WriteText(dir, ".gitignore", GitIgnore(orchestratable));
        WriteJson(dir, "project.json", ProjectManifest(id, workflow, type, sdk, profile, createdUtc, orchestratable, deadline));

        // Roles, checklists, anything else the workflow ships. Rendered like the rest, so they can
        // carry the same tokens.
        var roles = new List<Role>();
        foreach (var extra in ExtraTemplates(workflow))
        {
            var path = ResourcePath(extra);
            var body = Render(workflow, extra, tokens);
            WriteText(dir, path, body);

            if (Role.From(path, body) is { } role) roles.Add(role);
        }

        WriteJson(dir, host.McpConfig, McpConfig(dir));
        WriteJson(dir, host.Permissions, Permissions(sdk, standalone, workflow, dir, roles.Count > 0));

        // Antigravity keeps hooks in their own file; Claude Code carries them inside the settings
        // file written just above, so only one of these two lines does anything per host.
        if (sdk == "agy")
        {
            WriteJson(dir, ".agents/hooks.json", AgyHooks(sdk, dir));
            WriteScript(dir, HookScriptPath, HookScript(sdk, dir));
        }

        // Both hosts have a registry; they disagree only about its shape. Antigravity takes one JSON
        // file naming every subagent and pointing at its prompt; Claude Code takes one Markdown file
        // per agent with the prompt inside it. Registering the roles matters either way, because the
        // instructions tell the director to run them as subagents — an unregistered role leaves that
        // promise unkeepable, which is what a Claude project used to get.
        if (roles.Count > 0)
        {
            if (sdk == "agy")
            {
                WriteJson(dir, ".agents/agents.json", MultiAgentConfig(id, host.Instructions, roles));
            }
            else
            {
                foreach (var role in roles)
                {
                    WriteText(dir, $".claude/agents/{role.Name}.md", ClaudeSubagent(role));
                }
            }
        }

        // Written for every Antigravity project, not only the standalone ones. It is the file
        // `project.load` keys on, so emitting it always is what lets one file set be run either way
        // — and it removes the trap of choosing at generation time, discovering later that the
        // orchestrator refuses the project, and having to regenerate it to get a flag flipped.
        //
        // Not written for `claude`: the orchestrator refuses a non-Antigravity project regardless,
        // so the file would be one nothing ever reads.
        if (orchestratable)
        {
            WriteJson(dir, "agent.config.json", AgentConfig(host));
        }

        Report(dir, id, workflow, type, sdk, standalone, host, preserved);
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

    /// <summary>Reads the brief: the contents of <c>--brief</c>'s file, or <c>--prompt</c> verbatim.</summary>
    /// <remarks>
    /// Existence is checked in <see cref="Create"/> so a missing file is reported as a refusal rather
    /// than an exception; anything still failing here is unreadable rather than absent, and says so.
    /// </remarks>
    static string ReadBrief(CreateProjectOptions opts) =>
        opts.Brief.Length == 0 ? opts.Prompt : File.ReadAllText(opts.Brief);

    /// <summary>Every workflow that has an instructions template embedded.</summary>
    /// <remarks>
    /// Discovered rather than listed, so a workflow is registered by adding
    /// <c>ProjectTemplate/&lt;name&gt;/instructions.md</c> and nothing else. A hardcoded list would
    /// have to be edited in lockstep with the templates, and the failure when it was not is a
    /// workflow that exists on disk and is refused by name.
    /// </remarks>
    internal static string[] KnownWorkflows => TemplateNames("instructions.md");

    /// <summary>
    /// Minutes a workflow is expected to take, used when <c>--deadline</c> is not given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per workflow rather than one number, because the work genuinely differs: a mark is a handful
    /// of decisions and a study painting is a sequence of passes over the same surface. A single
    /// default would be wrong for both, and wrong in the more damaging direction for the painting —
    /// a deadline that cannot be met teaches an agent to ignore deadlines.
    /// </para>
    /// <para>
    /// These are starting points, not measurements. The only figure behind them is that a
    /// single-agent logo brief has run end to end in about three minutes, and one Inker alone has
    /// spent 58.8 on a pass it never finished. Tune them as real runs accumulate, and pass
    /// <c>--deadline</c> for anything unusual.
    /// </para>
    /// </remarks>
    static readonly Dictionary<string, int> WorkflowDeadlines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["logo"] = 15,
        ["infographic"] = 30,
        ["drawing"] = 45,
        ["comic"] = 60,
        ["painting"] = 120,
        ["comic_studio"] = 90,
        // A test fixture rather than a commission: it is measured on what it exercises, not on
        // whether it finished in time, and a deadline would only add a failure mode to the harness.
        ["harness"] = 0,
    };

    /// <summary>The deadline for a project, in minutes. Zero means none.</summary>
    internal static int DeadlineFor(string workflow, int? requested) =>
        requested is { } given
            ? Math.Max(0, given)
            : WorkflowDeadlines.TryGetValue(workflow, out var value) ? value : 0;

    /// <summary>
    /// The types a workflow offers. What a type <em>means</em> is the workflow's business.
    /// </summary>
    /// <remarks>
    /// For <c>harness</c> it selects the task — a picture or a brand identity. For <c>logo</c> it
    /// selects the stylistic frame, which Manual 12 §2.5b calls temperature, weight and shape
    /// language. It deliberately does <em>not</em> select the archetype: that is Stage 4's structural
    /// choice, and the manual is explicit that it reads better once there are candidate forms to look
    /// at, so fixing it from a command line would settle it before anything has been drawn.
    /// <para>
    /// An empty result means the workflow has no type axis, and <c>--type</c> is refused for it.
    /// </para>
    /// </remarks>
    static string[] TypesFor(string workflow) => TemplateNames($"{workflow}.type", trailing: true);

    /// <summary>
    /// Reads workflow or type names out of the embedded resource manifest.
    /// </summary>
    /// <remarks>
    /// Resources are named <c>…ProjectTemplate.&lt;workflow&gt;.&lt;file&gt;</c>. With
    /// <paramref name="trailing"/> the segment <em>after</em> the marker is wanted (a type name);
    /// without it, the segment before (a workflow name).
    /// </remarks>
    static string[] TemplateNames(string marker, bool trailing = false)
    {
        var names = new List<string>();

        foreach (var resource in typeof(ProjectGenerator).Assembly.GetManifestResourceNames())
        {
            var start = resource.IndexOf(ResourcePrefix, StringComparison.Ordinal);
            if (start < 0) continue;

            var relative = resource[(start + ResourcePrefix.Length)..];   // "<workflow>.<file>"

            if (trailing)
            {
                var prefix = marker + ".";
                if (relative.StartsWith(prefix, StringComparison.Ordinal) && relative.EndsWith(".md", StringComparison.Ordinal))
                {
                    names.Add(relative[prefix.Length..^3]);
                }
            }
            else if (relative.EndsWith("." + marker, StringComparison.Ordinal))
            {
                names.Add(relative[..^(marker.Length + 1)]);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return [.. names];
    }

    /// <summary>
    /// Everything else a workflow ships — role specs, checklists — beyond its instructions, brief and
    /// type sections. Returned as flattened resource suffixes, e.g. <c>roles.01_penciler.md</c>.
    /// </summary>
    /// <remarks>
    /// Discovered, like workflows and types, so a multi-agent workflow gains a role by gaining a file.
    /// </remarks>
    static string[] ExtraTemplates(string workflow)
    {
        var prefix = $"{ResourcePrefix}{workflow}.";
        var names = new List<string>();

        foreach (var resource in typeof(ProjectGenerator).Assembly.GetManifestResourceNames())
        {
            var start = resource.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) continue;

            var relative = resource[(start + prefix.Length)..];
            if (relative is "instructions.md" or "brief.md") continue;
            if (relative.StartsWith("type.", StringComparison.Ordinal)) continue;
            if (!relative.EndsWith(".md", StringComparison.Ordinal)) continue;

            names.Add(relative);
        }

        names.Sort(StringComparer.Ordinal);
        return [.. names];
    }

    /// <summary>
    /// Turns a flattened resource suffix back into the path it should be written to.
    /// </summary>
    /// <remarks>
    /// Embedding flattens directories into dots, so <c>roles.01_penciler.md</c> is
    /// <c>roles/01_penciler.md</c> on disk. Every segment but the extension becomes a directory.
    /// </remarks>
    static string ResourcePath(string relative) =>
        string.Join('/', relative[..^".md".Length].Split('.')) + ".md";

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
    static object ProjectManifest(string id, string workflow, string type, string sdk, string profile, string createdUtc, bool orchestratable, int deadlineMinutes)
    {
        // An ordered dictionary rather than an anonymous type: the optional fields would otherwise
        // need one shape per combination of them, and the order here is the order on disk.
        var manifest = new Dictionary<string, object?> { ["schema"] = 1, ["id"] = id, ["workflow"] = workflow };

        if (type.Length > 0) manifest["type"] = type;

        manifest["sdk"] = sdk;
        manifest["profile"] = profile;
        manifest["createdUtc"] = createdUtc;

        // Read by the ADK runtime to size each role's share of the run. Written even when zero,
        // so "this project has no deadline" is a stated fact rather than a missing key that a
        // reader has to interpret.
        manifest["deadlineMinutes"] = deadlineMinutes;

        // The slot the orchestrator records a resumable session into, so it exists wherever the
        // orchestrator can run — which is now any Antigravity project, not only a standalone one.
        if (orchestratable) manifest["conversationId"] = null;

        return manifest;
    }

    /// <summary>
    /// Wires the agent to this CLI's own MCP server, rooted at the project directory.
    /// </summary>
    /// <remarks>
    /// The server needs no awareness of whether a human is attached: <c>ask_question</c> is a
    /// built-in of the agent runtime, handled by the host, not by us.
    /// <para>
    /// Always <c>dotnet</c> with the assembly as an argument, never the apphost — <c>Polson.CLI.exe</c>
    /// does not exist on Linux, so naming it makes the generated file platform-specific for no gain.
    /// It also matches the one wiring observed to work end to end in Antigravity Desktop, which is
    /// worth more than a shape we reasoned our way to: a host that cannot launch the command shows an
    /// agent with no tools and no error to explain it.
    /// </para>
    /// <para>
    /// Absolute paths, forward-slashed. A relative <c>--project-dir</c> resolves against whatever
    /// working directory the host happens to use, and when it resolves wrongly the server still
    /// starts and still draws — it just records nothing, which is the failure mode with no symptom.
    /// Movability is the lesser property; regenerate with <c>--force</c> after moving a project.
    /// </para>
    /// </remarks>
    static object McpConfig(string projectDir) => new
    {
        mcpServers = new
        {
            polson = new
            {
                command = "dotnet",
                args = new[]
                {
                    Forward(Path.Combine(AppContext.BaseDirectory, "Polson.CLI.dll")),
                    "server",
                    "--project-dir",
                    Forward(projectDir),
                },
            },
        },
    };

    /// <summary>Forward slashes, which every platform accepts and JSON does not have to escape.</summary>
    static string Forward(string path) => path.Replace('\\', '/');

    /// <summary>
    /// Registers a workflow's roles as Antigravity subagents.
    /// </summary>
    /// <remarks>
    /// Derived entirely from the role files, so the registry cannot name an agent whose prompt is
    /// missing, or miss one that exists. Paths are relative to <c>.agents/</c>, which is where this
    /// file lives.
    /// <para>
    /// Unverified, like most of this host's schema: the shape follows the hand-written registry in
    /// <c>tests/multi_agent/comic_studio</c>, and no sample the desktop wrote itself was available to
    /// check it against. The workflow does not depend on it — its instructions describe running the
    /// roles as sequential personas, which needs no host support at all, and the registry is an
    /// optimisation the host may or may not take.
    /// </para>
    /// </remarks>
    static object MultiAgentConfig(string id, string instructions, IReadOnlyList<Role> roles) => new
    {
        version = "1.0",
        name = id,
        orchestrator = new { role = "Studio Director", promptFile = $"../{instructions}" },
        subagents = roles.Select(r => new
        {
            name = r.Name,
            role = r.Description,
            promptFile = $"../{r.PromptFile}",
            tools = ToolNames().Select(t => $"polson.{t}").Append("view_file").ToArray(),
        }).ToArray(),
    };

    /// <summary>
    /// Registers one workflow role as a Claude Code subagent.
    /// </summary>
    /// <remarks>
    /// Claude Code reads subagent definitions from <c>.claude/agents/*.md</c>: YAML frontmatter
    /// naming the agent and the tools it may hold, then the system prompt as the body.
    /// <para>
    /// <b>The prompt is inlined rather than referenced</b>, which is the one real difference from the
    /// Antigravity registry. There is no <c>promptFile</c> here, so the spec is copied in — and
    /// because both files are written in the same pass from the same rendered string they cannot
    /// disagree when generated, only if one is edited afterwards. The header says which file is the
    /// source, so the fix is to edit <c>roles/</c> and regenerate rather than to patch the copy.
    /// </para>
    /// <para>
    /// The tool list is the main agent's own, minus dispatch. A subagent that could reach the shell
    /// would be a way around the denials the main agent is held to, which is the shape of hole a
    /// permissions file exists to close — but the file's <c>deny</c> rules apply inside a subagent
    /// too, measured on a live run where three of four subagents hit a refusal on Polson's source. So
    /// this list is an additional restriction rather than the only one, and narrowing it further buys
    /// no isolation while costing the role its job.
    /// <para>
    /// <b>Reading alone was too narrow, and a live run showed both ways it failed.</b> The roles are
    /// asked to write <c>critique_log.md</c> and <c>findings.md</c>; with no writer, all sixteen of
    /// those edits fell to the coordinator, so the collaboration trace was written second-hand by an
    /// intermediary rather than by the agents whose work it describes. And <c>scriptFile</c> exists so
    /// an agent can edit a file instead of re-sending the program — but the subagents made every one
    /// of the fifty-eight <c>ExecuteScript</c> calls in that run, so without an editor the one
    /// mechanism that would have saved them the most time was reachable only by the agent that barely
    /// draws.
    /// </para>
    /// <para>
    /// Dispatch is deliberately absent: one level of delegation is the design, and a subagent that
    /// could spawn subagents is the peer-to-peer case this workflow is not.
    /// </para>
    /// </remarks>
    static string ClaudeSubagent(Role role)
    {
        // `Bash` unqualified, because the frontmatter names tools while the settings file names
        // commands: which invocations are permitted is decided there, by ShellAllows/ShellDenies, and
        // a subagent is held to that file exactly as the main agent is — measured on a live run where
        // three of four subagents hit its deny rules.
        var tools = string.Join(", ", ToolNames().Select(t => $"mcp__polson__{t}")
            .Concat(["Read", "Write", "Edit", "Glob", "Grep", "Bash"]));

        // Quoted, because a description is prose from a role file's heading and YAML would otherwise
        // read a colon in it as a key. Embedded quotes are doubled rather than backslash-escaped,
        // which is what YAML's double-quoted style would need — simpler to remove them.
        var description = role.Description.Replace("\"", "'");

        return $"""
            ---
            name: {role.Name}
            description: "{description}"
            tools: {tools}
            ---

            <!-- Generated by `polson create-project` from {role.PromptFile}. Edit that file and
                 regenerate with `--force`; editing this copy leaves the two disagreeing. -->

            {role.Body.TrimEnd()}

            """;
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
    /// <para>
    /// <c>run_command</c> is the standalone half of that story, and it is the one place a rule in
    /// the instructions becomes a rule the runtime keeps: the engine-only section tells every agent
    /// never to reach the drawing engine or the docs through a command line, and here that stops
    /// being a request. A public URL must not reach a shell either, which is the same denial
    /// arriving for a second reason.
    /// </para>
    /// <para>
    /// <c>instructionsFile</c> exists so the orchestrator does not have to know the name. It composed
    /// its bootstrap prompt around a hardcoded <c>GEMINI.md</c> and a restatement of how to work —
    /// a second copy of policy, in Python, able to drift from the template that owns it.
    /// </para>
    /// <para>
    /// <c>readBy</c> is written because this file now ships in every Antigravity project, including
    /// ones a desktop host will run. A tool policy that is read by nothing in the current context is
    /// the project's oldest trap — a file that looks like enforcement and enforces nothing — so the
    /// file says which runtime reads it rather than leaving that to be inferred from its presence.
    /// It is a constant, and nothing parses it.
    /// </para>
    /// </remarks>
    static object AgentConfig(HostFiles host) => new
    {
        schema = 1,
        readBy = "polson-orchestrator",
        agentBehavior = "interactive",
        instructionsFile = host.Instructions,
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
    /// <summary>
    /// The allow entries a subagent's MCP calls go through, in both spellings given as valid.
    /// </summary>
    /// <remarks>
    /// A subagent does not call <c>polson.ExecuteScript</c> directly; it dispatches through the
    /// host's lazy tool interface, so approving the server's own tool names covers the main agent
    /// and nothing it delegates to. That is why a run with a correct <c>mcp.autoApprove</c> still
    /// prompted for every call the designer subagent made.
    /// </remarks>
    /// <summary>
    /// Clears a previous run, and reports which hand-written files were kept.
    /// </summary>
    /// <remarks>
    /// What goes: <c>events/</c>, <c>scripts/</c> and <c>artifacts/</c> — everything the engine
    /// produced. <c>scripts/</c> is included even though it looks like source, because it is engine
    /// output too: the server numbers and writes those files. Clearing the log while leaving them
    /// would also make <c>polson report</c> flag every one as unaccounted for, which is a warning
    /// the reset itself would have manufactured.
    /// <para>
    /// What stays: <c>brief.md</c>, because that is the one file a person authors — the client's
    /// brief and, for a data-driven workflow, the whole hand-typed data table. So does
    /// <c>.polson/</c>: the requisition cache is content-addressed and re-filling it costs real
    /// money, so a reset of the <i>work</i> should not be a reset of the <i>spend</i>.
    /// </para>
    /// <para>
    /// The instructions file is <b>regenerated</b>, not kept. It is this project's system prompt
    /// rather than the director's document, and keeping it meant a reset silently shipped whatever
    /// the template said on the day the project was created — so a rule added to the template later
    /// reached new projects and never reached the one being reset, which is the opposite of what a
    /// reset is for. An edit worth keeping belongs in
    /// <c>ProjectTemplate/&lt;workflow&gt;/instructions.md</c>, where every project gets it.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether the project already in this directory was generated as standalone.
    /// </summary>
    /// <remarks>
    /// Read from its own manifest rather than assumed, and false for anything unreadable: a project
    /// that cannot say what it is gets the safer of the two, since a later regeneration with the
    /// flag can still add whatever standalone needs.
    /// </remarks>
    static bool WasStandalone(string dir)
    {
        try
        {
            var manifest = Path.Combine(dir, "project.json");
            if (!File.Exists(manifest)) return false;

            return string.Equals(
                JsonNode.Parse(File.ReadAllText(manifest))?["profile"]?.GetValue<string>(),
                "standalone", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Moves the previous run aside into <c>previous/&lt;timestamp&gt;/</c>. Nothing is deleted.
    /// </summary>
    /// <remarks>
    /// This used to delete <c>events/</c>, <c>scripts/</c> and <c>artifacts/</c>, and it cost a real
    /// baseline: a four-agent run whose measurements were the only evidence for what the next change
    /// was worth. It came back only because the *host* keeps its own transcripts outside the project,
    /// which is luck rather than design.
    /// <para>
    /// It also left the run behind. The agent-authored files — <c>findings.md</c>,
    /// <c>critique_log.md</c>, <c>artwork.js</c>, <c>output.*</c> — were not cleared, so a reset
    /// produced a project holding a report describing a run whose record had just been deleted. The
    /// next agent read it, correctly judged it stale, and reached for <c>rm</c>. Both halves of that
    /// were our doing.
    /// </para>
    /// <para>
    /// So: archive, and say where. Renaming aside keeps every property a delete had — the new run
    /// starts clean, and nothing stale is left to mislead it — while giving up none of the evidence.
    /// <c>brief.md</c> stays put because it is the one file a person authors, and <c>.polson/</c>
    /// because a reset of the <i>work</i> should not be a reset of the <i>spend</i>.
    /// </para>
    /// </remarks>
    static string[] ClearRun(string dir, HostFiles host)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var archive = Path.Combine(dir, "previous", stamp);
        var moved = 0;

        void Aside(string relative)
        {
            var from = Path.Combine(dir, relative);
            if (!File.Exists(from) && !Directory.Exists(from)) return;

            var to = Path.Combine(archive, relative);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (Directory.Exists(from)) Directory.Move(from, to);
                else File.Move(from, to);
                moved += 1;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file the host still holds open. Say so rather than failing the reset: a stale
                // artifact left behind is a nuisance, an aborted reset is a blocker.
                AnsiConsole.MarkupLine($"[yellow]  kept (in use):[/] {Markup.Escape(relative)}");
            }
        }

        foreach (var name in new[] { "events", "scripts", "artifacts" })
        {
            Aside(name);
        }

        // The run's own account of itself, which the engine did not write and a reset previously
        // left in place. Stale, and read as current by whatever runs next.
        foreach (var name in new[] { "findings.md", "critique_log.md", "artwork.js", "output.webp", "output.svg", "output.png" })
        {
            Aside(name);
        }

        if (moved > 0)
        {
            // A note in the archive rather than only on the console, because the console scrolls away
            // and the question "what is this directory?" is asked months later.
            File.WriteAllText(Path.Combine(archive, "README.md"),
                $"# Previous run, archived {DateTime.UtcNow:u}\n\n"
              + "Moved aside by `polson create-project --reset`, which archives rather than deletes.\n"
              + "Everything the previous run produced is here: its record, its scripts, its renders,\n"
              + "and whatever it wrote about itself. Delete this directory yourself if you do not\n"
              + "want it — nothing else will.\n");

            AnsiConsole.MarkupLine($"[green]  archived:[/] previous/{stamp} [dim]({moved} items — nothing deleted)[/]");
        }

        return [.. new[] { "brief.md" }.Where(f => File.Exists(Path.Combine(dir, f)))];
    }

    /// <summary>The command a hook runs: this CLI, in the runtime the project already needs.</summary>
    /// <remarks>
    /// <c>dotnet "&lt;dll&gt;"</c> rather than an apphost, for the same reason the MCP wiring uses it —
    /// the <c>.exe</c> is Windows-only and these files are read on Linux too.
    /// </remarks>
    /// <remarks>
    /// The SDK is baked in because the payload alone is not always enough: Antigravity's
    /// <c>Stop</c> names a conversation and no transcript, so the verb has to know whose store to
    /// look in. It is a hint for the fallback only — a payload that names its transcript still wins.
    /// </remarks>
    /// <summary>The wrapper a hook invokes, beside <c>hooks.json</c> and named without spaces.</summary>
    const string HookScriptPath = ".agents/preserve-chatlog.cmd";

    /// <summary>
    /// What a host's hook actually runs: a bare filename, deliberately.
    /// </summary>
    /// <remarks>
    /// The Antigravity CLI hands the command line to <c>cmd /c</c> as a single argument, and the Go
    /// side escapes the quotes inside it. A command beginning with a quoted path therefore reaches
    /// cmd as <c>\"C:/...\"</c> — quotes and all, as part of the command name — and dies with
    /// "is not recognized as an internal or external command". Every quoting scheme tried against
    /// that had the same shape of problem, so this stops quoting altogether: the command is one
    /// bare token with no path, no spaces and no quotes.
    /// <para>
    /// That works because a hook does <b>not</b> run in the project directory. It runs in the folder
    /// holding <c>hooks.json</c> — <c>.agents/</c> — which is exactly where the wrapper is written.
    /// The one awkward fact about hooks turns out to be the thing that makes them reliable.
    /// </para>
    /// <para>
    /// The leading <c>.\</c> is not decoration. <c>cmd.exe</c> searches the working directory for a
    /// bare command name only while <c>NoDefaultCurrentDirectoryInExePath</c> is unset, and it is set
    /// on at least one machine this has to work on — where the bare name failed with the same
    /// "is not recognized" message as the quoted path did. Naming the directory explicitly is immune
    /// to the setting either way.
    /// </para>
    /// </remarks>
    static string HookCommand(string sdk, string projectDir) =>
        sdk == "agy" ? ".\\" + Path.GetFileName(HookScriptPath) : DirectCommand(sdk, projectDir);

    /// <summary>
    /// The batch wrapper itself, where quoting is safe because cmd parses the file normally.
    /// </summary>
    /// <remarks>
    /// Backslashes and CRLF, because this one file is read by <c>cmd.exe</c> rather than by us.
    /// Its own paths are absolute for the reason the command line could not be: the working
    /// directory is <c>.agents/</c>, one level below the project.
    /// </remarks>
    static string HookScript(string sdk, string projectDir) =>
        $"@echo off{Environment.NewLine}"
        + $"REM Generated by `polson create-project`. Invoked by .agents/hooks.json.{Environment.NewLine}"
        + $"REM A hook runs with its working directory set to this folder, not the project root,{Environment.NewLine}"
        + $"REM which is why every path below is absolute.{Environment.NewLine}"
        + $"{DirectCommand(sdk, projectDir, windows: true)}{Environment.NewLine}";

    /// <summary>The command itself, for a host that can take one without mangling its quotes.</summary>
    static string DirectCommand(string sdk, string projectDir, bool windows = false)
    {
        string Path_(string p) => windows ? p.Replace('/', '\\') : Forward(p);

        var exe = Path.Combine(AppContext.BaseDirectory, "Polson.CLI.exe");
        var launcher = File.Exists(exe)
            ? $"\"{Path_(Forward(exe))}\""
            : $"dotnet \"{Path_(Forward(Path.Combine(AppContext.BaseDirectory, "Polson.CLI.dll")))}\"";

        return $"{launcher} preserve-chatlog --host {sdk} --project-dir \"{Path_(Forward(projectDir))}\"";
    }

    /// <summary>
    /// Antigravity's hook registry: the chat transcript, preserved into the project.
    /// </summary>
    /// <remarks>
    /// <c>enabled</c> is set explicitly. A group written without it did nothing — no copy, no error,
    /// nothing in the project to say the hook had ever been consulted — which is the same failure
    /// mode as every other config we have guessed at here: an entry the host does not act on looks
    /// exactly like one it does.
    /// <para>
    /// <c>PostInvocation</c> fires after each model response and <c>Stop</c> once the turn's loop
    /// ends, so the log stays current through a long session and is complete when it finishes.
    /// <c>PreInvocation</c> was wired here first and is the wrong event for this: it runs
    /// <i>before</i> the model, so it can only ever preserve the exchange before the current one.
    /// </para>
    /// </remarks>
    static object AgyHooks(string sdk, string dir) => new Dictionary<string, object>
    {
        ["polson-chatlog"] = new Dictionary<string, object>
        {
            ["enabled"] = true,
            ["PostInvocation"] = new[] { new { type = "command", command = HookCommand(sdk, dir), timeout = 30 } },
            ["Stop"] = new[] { new { type = "command", command = HookCommand(sdk, dir), timeout = 30 } },
        },
    };

    /// <summary>
    /// Claude Code's hook block, which lives inside the settings file rather than its own.
    /// </summary>
    /// <remarks>
    /// <c>Stop</c> fires after every agent turn and <c>SessionEnd</c> once on clean exit. Both run
    /// the same verb: the per-turn copy keeps the log current if a session is killed rather than
    /// closed, and the final one catches the last exchange. The verb overwrites one file per
    /// session, so firing repeatedly costs a copy rather than an accumulation.
    /// </remarks>
    static object ClaudeHooks(string sdk, string dir) => new Dictionary<string, object>
    {
        ["Stop"] = new[] { new { hooks = new[] { new { type = "command", command = HookCommand(sdk, dir) } } } },
        ["SessionEnd"] = new[] { new { hooks = new[] { new { type = "command", command = HookCommand(sdk, dir) } } } },
    };

    static IEnumerable<KeyValuePair<string, string>> SubagentAllows() =>
        new[] { "call_mcp_tool", "default_api:call_mcp_tool", "polson:*" }
            .Concat(ToolNames().Select(t => $"polson:{t}"))
            .Select(key => new KeyValuePair<string, string>(key, "allow"));

    static object Permissions(string sdk, bool standalone, string workflow, string dir, bool hasSubagents)
    {
        var denied = standalone ? [.. AlwaysDenied, .. StandaloneDenied] : AlwaysDenied;

        return sdk == "agy"
            ? new
            {
                // The shape Antigravity Desktop actually reads: `mcp.autoApprove`, with names dotted
                // as `<server>.<Tool>`. Taken from a settings file the desktop wrote itself
                // (`tests/multi_agent/comic_studio/.agents/settings.json`). Without it every call
                // stops for approval — as two rounds of the `permissions` form below demonstrated.
                //
                // The `polson:*` wildcard beside it is colon-separated rather than dotted, which is
                // Antigravity's own recommendation for covering a whole server. Only one of the two
                // spellings is likely to be the real one; carrying both is deliberate, since an
                // unrecognised entry is inert rather than harmful. It does mean a run that stops
                // prompting does not tell us *which* form fixed it — see the note below.
                mcp = new
                {
                    autoApprove = new[] { "polson:*" }
                        .Concat(ToolNames().Select(t => $"polson.{t}"))
                        .ToArray(),
                },

                // Removes the tool rather than refusing it, so it never reaches the model's context
                // and no tokens are spent being told no. The stronger of the two forms.
                tools = new { disabled = denied },

                // `permissions` maps a tool to a verdict — it is **not** the `{allow: [], deny: []}`
                // arrays this file used to carry, which no host-written sample contains and which
                // prompted for every call when we tried it.
                //
                // The allows exist for *subagents*: a run where the main agent was auto-approved
                // still prompted for every call made by a subagent it spawned, because a subagent
                // dispatches through `call_mcp_tool` rather than through the server's own tool
                // names. Both the bare and `default_api:`-prefixed spellings are named, as with the
                // denials, because both have been given as valid and neither is verifiable here.
                permissions = SubagentAllows()
                    .Concat(denied.SelectMany(t => new[] { t, $"default_api:{t}" })
                        .Select(t => new KeyValuePair<string, string>(t, "deny")))
                    .ToDictionary(e => e.Key, e => e.Value),
            }
            : (object)new
            {
                permissions = new
                {
                    defaultMode = "default",
                    // Subagent dispatch, only where there is something to dispatch. Without it every
                    // dispatch stops to ask — the same shape of gap as approving a server's own tool
                    // names and still being prompted for everything a subagent called. Allowing it
                    // where no subagent exists would widen the policy for a capability that project
                    // never uses.
                    //
                    // **Both spellings**, because the tool has been named both and an entry the host
                    // does not recognise is silently inert rather than an error — so naming only one
                    // is a rule that looks enforced and is not. A live `comic_studio` run settled
                    // which is current: it dispatched `Agent` with `subagent_type: "penciler"`, while
                    // the file allowed `Task` alone and the run prompted for approval anyway.
                    allow = ToolNames().Select(t => $"mcp__polson__{t}")
                        .Concat(["Read", "Write", "Edit", "Glob", "Grep"])
                        .Concat(ShellAllows())
                        .Concat(hasSubagents ? ["Agent", "Task"] : Array.Empty<string>()).ToArray(),
                    deny = ShellDenies()
                        .Concat(["BashOutput", "KillShell", "WebFetch", "WebSearch"])
                        .Concat(IsIsolated(workflow) ? SourceDenies() : []).ToArray(),
                },

                // Without this, Claude Code asks a human to approve the project's own MCP server
                // before any tool is callable. The server is the point of the project; approving it
                // is not a decision worth interrupting a run for.
                enabledMcpjsonServers = new[] { "polson" },

                hooks = ClaudeHooks(sdk, dir),
            };
    }

    /// <summary>
    /// The text-handling commands an agent working on a JavaScript file actually needs.
    /// </summary>
    /// <remarks>
    /// The shell was denied wholesale, and that was too blunt. It was denied for one good reason —
    /// an agent must not produce artwork by running <c>node</c>, <c>dotnet</c> or ImageMagick,
    /// because a picture made outside the engine has no trace of how it came to exist — but the
    /// denial also removed <c>grep</c>, <c>sed</c> and <c>diff</c>, which have nothing to do with
    /// drawing and everything to do with maintaining a large source file. With <c>scriptFile</c>
    /// making that file the working surface, they are the ordinary tools of the job.
    /// <para>
    /// Allowed by command prefix rather than as a blanket <c>Bash</c>, so what is permitted is
    /// legible in the file rather than resting on a rule elsewhere. <see cref="ShellDenies"/> keeps
    /// the route-around closed.
    /// </para>
    /// </remarks>
    static string[] ShellAllows() =>
        [.. new[]
        {
            // Finding and reading.
            "grep", "rg", "find", "ls", "cat", "head", "tail", "wc", "diff", "file", "stat",
            // Transforming a file in place, which is the point of allowing any of this.
            "sed", "awk", "sort", "uniq", "cut", "tr",
            // Moving work about inside the project.
            "cp", "mv", "mkdir", "touch", "tee",

            // The small change that costs an hour when it is missing. An agent writes
            // `cat >> log.md << 'EOF' … EOF; echo done`, and one unlisted verb in a compound puts a
            // permission prompt in front of a person who is watching a dashboard rather than the
            // session. The subagent stalled, the coordinator blocked behind it, and the run sat for
            // an hour — over `echo`. None of these can do anything the others cannot.
            "echo", "printf", "pwd", "cd", "test", "true", "false",
            "basename", "dirname", "date", "which", "jq", "nl", "column",
        }.Select(c => $"Bash({c}:*)")];

    /// <summary>
    /// The commands that would let an agent produce artwork, or reach the network, outside the engine.
    /// </summary>
    /// <remarks>
    /// This is the half of the shell denial that was always load-bearing. Every mark must be made
    /// through <c>ExecuteScript</c>: a script the server ran is saved, numbered and recorded against
    /// a stage, while the identical script handed to a CLI leaves an image and no account of how it
    /// came to exist. A run can look complete and reconcile to nothing.
    /// <para>
    /// <b>What this gives up, stated plainly:</b> a shell that can run <c>grep</c> can read Polson's
    /// own source, and no deny rule on the <c>Read</c> tool prevents that. On a workflow carrying the
    /// isolation rule, source isolation therefore becomes a convention the agent keeps rather than
    /// something the host enforces — the same conclusion Antigravity forced, arrived at here by
    /// choice. The instructions still ask, and a run that peeks can still say so in its findings.
    /// </para>
    /// </remarks>
    static string[] ShellDenies() =>
        [.. new[]
        {
            // Anything that could draw, encode or post-process outside the engine.
            "node", "npm", "npx", "deno", "bun", "python", "python3", "dotnet",
            "magick", "convert", "mogrify", "ffmpeg", "inkscape", "rsvg-convert",
            // Anything that could reach the network, which WebFetch/WebSearch already deny by tool.
            "curl", "wget", "ssh", "scp",
            // Nested shells, which would make every rule above a suggestion.
            "bash", "sh", "zsh", "cmd", "powershell", "pwsh",

            // Destroying the run's own record. An allow list only decides what is *auto-approved*;
            // anything unlisted falls through to `defaultMode: "default"`, which asks — and a prompt
            // arriving in the middle of a long run is waved through. A live run proved it: the agent
            // ran `rm -v findings.md critique_log.md`, both of which the workflow had told it to
            // write, and both were gone. Deleting is never part of drawing, and the one thing a
            // studio must not lose is its account of what it did.
            "rm", "rmdir", "del", "erase", "shred", "truncate",

            // Rewriting history, which would make the record disagree with the repository it sits in.
            "git", "gh",
        }.Select(c => $"Bash({c}:*)")];

    /// <summary>
    /// Whether a workflow claims to hide the implementation from the agent.
    /// </summary>
    /// <remarks>
    /// Read off the template rather than from a list of workflow names: a workflow carries the
    /// isolation paragraph exactly when it is an evaluation harness, so the token is the signal. A
    /// client design project has no reason to deny reading anything and does not get these rules.
    /// </remarks>
    static bool IsIsolated(string workflow) =>
        Render(workflow, "instructions.md", []).Contains("{{ISOLATION}}", StringComparison.Ordinal);

    /// <summary>
    /// Deny rules hiding Polson's own implementation from an agent evaluating its published API.
    /// </summary>
    /// <remarks>
    /// The harness measures whether the published API and manuals are sufficient. A run where the
    /// agent read the source cannot answer that, so the rule matters — and it was previously left to
    /// the agent to write these by hand, which one duly did, correctly, and reported as friction that
    /// depended on knowing the working spelling.
    /// <para>
    /// Absolute paths, listed per directory. <c>Read(../**)</c> is the intuitive form and denies
    /// nothing, because patterns are rooted at the project; <c>Read(//**)</c> denies everything
    /// including the project itself. And <c>tests/</c> cannot be denied wholesale, because a harness
    /// is often generated inside it and a deny cannot carve an exception out of itself — so the
    /// test projects are named individually.
    /// </para>
    /// <para>
    /// Only directories that exist are emitted. A rule naming a path that is not there protects
    /// nothing while reading exactly like one that does.
    /// </para>
    /// </remarks>
    static string[] SourceDenies()
    {
        // Walk up to the checkout rather than counting directories from the assembly: the CLI runs
        // from bin/cli when shipped and from a test host's output directory otherwise, and a fixed
        // number of "..' segments is right in exactly one of those. Getting it wrong emits nothing
        // and looks like it worked.
        var root = FindCheckout(AppContext.BaseDirectory);

        // No checkout means no implementation on this machine to hide, so there is nothing to deny.
        // Emitting rules for paths that are not there would read like protection and be none.
        if (root is null) return [];

        var targets = new List<string>();

        foreach (var relative in new[] { "src", "ext", "docs" })
        {
            var full = Path.Combine(root, relative);
            if (Directory.Exists(full)) targets.Add(full);
        }

        var tests = Path.Combine(root, "tests");
        if (Directory.Exists(tests))
        {
            targets.AddRange(Directory.EnumerateDirectories(tests, "Polson.Tests.*"));
        }

        return [.. targets
            .Select(Forward)
            .OrderBy(t => t, StringComparer.Ordinal)
            .SelectMany(t => new[] { $"Read({t}/**)", $"Grep({t}/**)", $"Glob({t}/**)" })];
    }

    /// <summary>The Polson checkout containing <paramref name="start"/>, or null if there is none.</summary>
    static string? FindCheckout(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Polson.sln"))) return dir.FullName;
        }

        return null;
    }

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
          ### Partly enforced, partly a convention — and the difference matters.

          `.claude/settings.local.json` does three things, and only the first two are enforcement:

          1. **The network is denied by tool**: `WebFetch` and `WebSearch`.
          2. **`Read`, `Grep` and `Glob` are denied over Polson's own source, tests and docs**, by
             absolute path. Against those tools the implementation is genuinely out of reach.
          3. **The shell is not denied.** It is *shaped*: an allowlist of ordinary reading and
             file-transforming verbs is auto-approved, and the interpreters, image tools, network
             clients, nested shells, `rm` and `git` are denied — so every mark is still made through
             `ExecuteScript`, which is the property under test. But `grep`, `cat` and `find` are
             allowed, and a rule that names commands cannot also police the paths they are given.

          So **source isolation is a convention you keep**, not a wall you will bounce off. Do not
          test it and do not route around it: reading the implementation would not make this run
          fail, it would make it worthless, because the point is what an agent can do from the
          published API alone.

          What is worth proving is claim 2, because it is real and it can go stale — those paths were
          written when this project was generated, and if the project or the Polson checkout has moved
          since, they name somewhere that no longer exists and protect nothing. **Attempt one `Read`
          of a Polson source file and confirm it is refused.** Report the result in `findings.md`
          either way. A rule you assumed was holding is worth less than one you watched refuse.

          Report what you observe, not what this file told you. `echo` succeeding proves the shell
          runs; it proves nothing about which paths are reachable through it. A previous run reported
          a sandbox breach on exactly that inference, and was wrong — a security finding is held to
          the same standard as a measurement, and an unearned one is worse than none, because someone
          will act on it.
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

    /// <summary>
    /// Ignores <c>session/</c> wherever the orchestrator can create it, which is every Antigravity
    /// project — so the exposure does not depend on a flag having been passed at generation time.
    /// </summary>
    static string GitIgnore(bool orchestratable) =>
        (orchestratable
            ? """
              # SDK-owned session state: regenerable, sometimes large, never the deliverable.
              session/


              """
            : "")
        + """
          # Renders are reproducible from scripts/ and are noisy in review. Commit deliberately
          # if a particular stage is worth keeping in history.
          artifacts/

          # Earlier runs, moved aside by --reset rather than deleted. Kept so a measurement is never
          # lost to a re-run; ignored because it is a local archive, not the project's history.
          previous/
          """;

    /// <summary>
    /// Writes a batch file, which is the one thing here that must not have Unix line endings.
    /// </summary>
    /// <remarks>
    /// <c>cmd.exe</c> mishandles LF-only <c>.cmd</c> files — the repository's own
    /// <c>.gitattributes</c> pins <c>*.cmd</c> to CRLF for exactly this reason — so this bypasses
    /// <see cref="WriteText"/>, which normalises everything to LF.
    /// </remarks>
    static void WriteScript(string dir, string name, string body)
    {
        var path = Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body.ReplaceLineEndings("\r\n"), new UTF8Encoding(false));
    }

    /// <summary>Writes a file, creating the directory a nested name implies.</summary>
    static void WriteText(string dir, string name, string body)
    {
        var path = Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body.ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }

    /// <summary>
    /// Relaxed escaping, because these files are read by people as well as parsed.
    /// </summary>
    /// <remarks>
    /// The strict default turns every <c>&amp;</c>, <c>&lt;</c> and <c>+</c> into a <c>\uXXXX</c>
    /// escape, so a role described as "Composition &amp; Pose" lands as
    /// <c>"Composition & Pose"</c>. Both parse to the same string; only one is readable in a
    /// diff. The same choice, for the same reason, as <c>RunEventLog</c>.
    /// </remarks>
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static void WriteJson(string dir, string name, object value) =>
        WriteText(dir, name, JsonSerializer.Serialize(value, JsonOptions));

    static void Report(string dir, string id, string workflow, string type, string sdk, bool standalone, HostFiles host, string[] preserved)
    {
        AnsiConsole.MarkupLine($"[bold green]Project {(preserved.Length > 0 ? "reset" : "created")}:[/] {Markup.Escape(dir)}");
        AnsiConsole.MarkupLine($"  id [bold]{Markup.Escape(id)}[/] · workflow [bold]{workflow}[/]"
                             + $"{(type.Length > 0 ? $" [bold]{type}[/]" : "")} · sdk [bold]{sdk}[/] · "
                             + $"[bold]{(standalone ? "standalone" : "managed")}[/]");

        // Naming what survived matters more than naming what was regenerated: the kept files are the
        // ones holding work a person did by hand, and a reset that silently overwrote them would be
        // discovered only when the brief turned out to be the template again.
        if (preserved.Length > 0)
        {
            AnsiConsole.MarkupLine($"  cleared [bold]events/[/], [bold]scripts/[/], [bold]artifacts/[/] · kept "
                                 + string.Join(", ", preserved.Select(f => $"[bold]{Markup.Escape(f)}[/]"))
                                 + $" · rewrote [bold]{Markup.Escape(host.Instructions)}[/]");
        }
        AnsiConsole.WriteLine();

        // Named as the mode it applies to, first thing. The note is only printed for a managed
        // project, but a reader who has generated both cannot tell that from the text alone — and a
        // caveat about unenforceable policy is exactly the kind a person carries over to the profile
        // it was never about.
        if (!standalone)
        {
            AnsiConsole.MarkupLine("[yellow]  Note:[/] run in [bold]managed[/] mode — opened with a desktop host — that host owns");
            AnsiConsole.MarkupLine("        tool policy, not us. Image generation is refused two ways");
            AnsiConsole.MarkupLine($"        in [bold]{Markup.Escape(host.Permissions)}[/] and carried in [bold]{Markup.Escape(host.Instructions)}[/] as a rule");
            AnsiConsole.MarkupLine("        as well — which form this host honours is not something we can check");
            AnsiConsole.MarkupLine("        from here. Run the same project from Polson instead and that caveat");
            AnsiConsole.MarkupLine("        goes: we host the agent and enforce the policy ourselves.");
            AnsiConsole.WriteLine();
        }

        // Both ways are offered for any Antigravity project, because both now work on the same file
        // set. Which is listed first follows the profile, since that is what the generator was asked
        // for — but neither is a door that has been closed.
        AnsiConsole.MarkupLine($"  Next: fill in [bold]brief.md[/], then run it either way:");

        var orchestrator = $"        [bold]./polson_run {Markup.Escape(dir)}[/]  (or ./polson_webapp on its parent)";
        const string desktop = "        open the directory with your agent host.";

        if (sdk != "agy")
        {
            AnsiConsole.MarkupLine(desktop);
        }
        else if (standalone)
        {
            AnsiConsole.MarkupLine(orchestrator);
            AnsiConsole.MarkupLine(desktop);
        }
        else
        {
            AnsiConsole.MarkupLine(desktop);
            AnsiConsole.MarkupLine(orchestrator);
        }
    }

    static bool Fail(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape(message)}");
        return false;
    }
    #endregion
}
