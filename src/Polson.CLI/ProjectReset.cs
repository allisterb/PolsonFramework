namespace Polson.CLI;

using System;
using System.IO;
using System.Linq;
using Spectre.Console;

/// <summary>
/// <c>polson reset</c> — clear a project's previous run so it can be run again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The difference from <c>create-project --reset</c> is what it does NOT touch.</b> That flag
/// clears the run and then <em>regenerates every generated file</em> — the instructions, the MCP
/// wiring, the tool policy, the manifest — which is right when the generator has changed and wrong
/// when you have edited the instructions yourself. This clears the run and stops. Hand edits to
/// <c>CLAUDE.md</c> or <c>GEMINI.md</c> survive it, and nothing has to be restated on the command
/// line: no parent directory, no id, no sdk, no workflow.
/// </para>
/// <para>
/// So the two are a pair rather than a duplicate. <c>reset</c> is "run this project again";
/// <c>create-project --reset</c> is "rebuild this project from the current templates and run it
/// again". Both archive the run the same way, into <c>previous/&lt;timestamp&gt;/</c>, so a project
/// reset by either route has the same shape afterwards.
/// </para>
/// <para>
/// <b>Archiving is the default and <c>--delete</c> is the opt-in</b>, which is the inverse of the
/// obvious design and is deliberate. The generator's own history is the argument: clearing a run by
/// deleting it destroyed a four-agent run's measurements, which were the only evidence for what the
/// next change was worth, and they came back only because the host happened to keep transcripts
/// outside the project. A directory of old runs costs disk; a deleted one costs the answer to
/// "was that better than what we had?".
/// </para>
/// </remarks>
public static class ProjectReset
{
    #region Methods
    public static bool Run(ResetOptions opts)
    {
        var dir = Path.GetFullPath(opts.ProjectDir.Trim());

        if (!Directory.Exists(dir))
        {
            return Fail($"No such directory: {dir}");
        }

        // A project rather than any directory, because this moves files and a mistyped path should
        // not quietly rearrange something else. `project.json` is what `create-project` writes and
        // what `--reset` reads back, so it is the same marker the rest of the CLI already trusts.
        if (!File.Exists(Path.Combine(dir, "project.json")))
        {
            return Fail($"Not a Polson project: {dir}\n"
                + "       No project.json. `polson create-project` writes one; this clears a run\n"
                + "       from a project that already exists rather than making one.");
        }

        var present = ProjectGenerator.RunOutput
            .Where(name => File.Exists(Path.Combine(dir, name)) || Directory.Exists(Path.Combine(dir, name)))
            .ToArray();

        if (present.Length == 0)
        {
            // Not a failure: the requested state is the state it is in. Reported rather than silent,
            // because "nothing happened" and "nothing needed to happen" look identical otherwise.
            AnsiConsole.MarkupLine($"[green]Already clear:[/] {Markup.Escape(dir)} [dim](no run to clear)[/]");
            return true;
        }

        AnsiConsole.MarkupLine($"[bold]Clearing:[/] {Markup.Escape(dir)}");
        foreach (var name in present)
        {
            AnsiConsole.MarkupLine($"[dim]  {Markup.Escape(name)}[/]");
        }

        var cleared = ProjectGenerator.ClearRun(dir, opts.Delete);

        // What survived, named rather than implied. The question this verb raises is "have I just
        // lost my brief?", and the answer is worth printing every time rather than trusting to the
        // reader having read the help.
        // Both instruction names are checked rather than resolved from the sdk, because this verb
        // does not need to know which host a project was generated for — and not needing to know is
        // most of why it takes one argument where `create-project --reset` takes three.
        var kept = new[] { "brief.md", "CLAUDE.md", "GEMINI.md", "project.json", ".polson" }
            .Where(name => File.Exists(Path.Combine(dir, name)) || Directory.Exists(Path.Combine(dir, name)))
            .ToArray();

        if (kept.Length > 0)
        {
            AnsiConsole.MarkupLine("[green]  kept:[/] "
                + string.Join(", ", kept.Select(f => $"[bold]{Markup.Escape(f)}[/]")));
        }

        AnsiConsole.MarkupLine("[dim]  the instructions and every other generated file are untouched — "
            + "use `create-project --reset` to regenerate them too.[/]");

        // **A partial clear is a failure, and it is the one outcome that must not read as success.**
        // Half-cleared is worse than either end: the next run starts against renders belonging to a
        // record that is no longer there, and nothing downstream can tell. Reported last, where the
        // eye lands, and with a non-zero exit so `polson reset && <run>` does not carry on.
        if (cleared.Kept.Length > 0)
        {
            AnsiConsole.MarkupLine(string.Empty);
            AnsiConsole.MarkupLine("[bold red]Not fully cleared.[/] Still in the project: "
                + string.Join(", ", cleared.Kept.Select(Markup.Escape)));
            AnsiConsole.MarkupLine("[yellow]  This project is now part old run, part new.[/] Close whatever holds those");
            AnsiConsole.MarkupLine("  files — a running `polson studio`, an open image viewer, an editor — and run this");
            AnsiConsole.MarkupLine("  again. It is safe to repeat: what already moved is in previous/, and a second");
            AnsiConsole.MarkupLine("  pass archives only what is left.");
            return false;
        }

        return true;
    }

    static bool Fail(string message)
    {
        AnsiConsole.MarkupLine($"[red]error:[/] {Markup.Escape(message)}");
        return false;
    }
    #endregion
}
