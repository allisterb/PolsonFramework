namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The <c>Stage</c> global: lets a script declare which stage of work it is in, and add notes to the
/// run's record.
/// </summary>
/// <remarks>
/// Members are PascalCase in C#; Jint resolves the JS camelCase spelling onto them, so this is
/// <c>Stage.begin(...)</c>, <c>Stage.end()</c>, <c>Stage.current</c> and <c>Stage.note(...)</c> from
/// a script. Do not add camelCase aliases.
/// <para>
/// The stage is kept on the <see cref="SessionContext"/>, not here, so it outlives the execution
/// that set it — one stage spans many scripts, which is the entire point of tagging with it.
/// </para>
/// <para>
/// What a stage records is the agent's <em>claim</em> about its own intent, not a fact the server
/// verified. That is what makes the run legible rather than merely logged, but it also means this
/// is not evidence: the machine-recorded fields beside it — script path, duration, artifact, bytes —
/// remain the checkable half of every event.
/// </para>
/// </remarks>
public sealed class StageApi
{
    #region Constants
    /// <summary>Stage names are labels in a viewer, not prose. Long ones are truncated rather than refused.</summary>
    private const int MaxNameLength = 80;

    private const int MaxNoteLength = 2000;
    #endregion

    #region Fields
    private readonly SessionContext? session;
    private readonly RunEventLog? events;
    private readonly string? executionId;
    #endregion

    #region Constructors
    public StageApi(SessionContext? session, RunEventLog? events, string? executionId)
    {
        this.session = session;
        this.events = events;
        this.executionId = executionId;
    }
    #endregion

    #region Properties
    /// <summary>The stage currently in effect, or null if none has been declared.</summary>
    public string? Current => session?.Stage;
    #endregion

    #region Methods
    /// <summary>
    /// Declares the stage of work. Persists across executions until changed or ended.
    /// </summary>
    /// <remarks>
    /// Beginning a stage while one is open closes the previous one first, so the record never shows
    /// two stages running at once and a viewer can treat the events between begin and end as a
    /// contiguous group.
    /// </remarks>
    public string Begin(string name)
    {
        var clean = Clean(name, MaxNameLength);
        if (clean.Length == 0) throw new ArgumentException("A stage needs a name.", nameof(name));

        if (session?.Stage is { Length: > 0 } previous)
        {
            events?.Append("stage.end", previous, executionId, new Dictionary<string, object?> { ["reason"] = "superseded" });
        }

        if (session is not null) session.Stage = clean;
        events?.Append("stage.begin", clean, executionId);

        return clean;
    }

    /// <summary>Ends the current stage. Harmless when none is open.</summary>
    public void End()
    {
        if (session?.Stage is not { Length: > 0 } current) return;

        events?.Append("stage.end", current, executionId);
        session.Stage = null;
    }

    /// <summary>
    /// Records a note in the run's record, tagged with the current stage.
    /// </summary>
    /// <remarks>
    /// For the reasoning that would otherwise be lost — why a direction was abandoned, what a render
    /// was meant to test. <c>log(...)</c> reaches only the caller of this one tool call; a note
    /// persists into the run directory and is what a reader sees afterwards.
    /// </remarks>
    public void Note(string message)
    {
        var clean = Clean(message, MaxNoteLength);
        if (clean.Length == 0) return;

        events?.Append("note", session?.Stage, executionId, new Dictionary<string, object?> { ["message"] = clean });
    }

    /// <summary>Strips the characters that would corrupt a one-line JSON record, and caps the length.</summary>
    /// <remarks>
    /// A stage name can be influenced by a client brief the agent read, so it gets the same treatment
    /// as any other text arriving from outside: newlines and controls out, invisible characters out,
    /// length bounded.
    /// </remarks>
    private static string Clean(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            var v = rune.Value;
            if (v < 0x20 || v is >= 0x7F and <= 0x9F) continue;                     // controls, newlines included
            if (v is >= 0x202A and <= 0x202E or >= 0x2066 and <= 0x2069) continue;   // bidi overrides
            if (v is 0x200B or 0x200C or 0x200D or 0x2060 or 0xFEFF) continue;       // zero-width
            if (v is >= 0xE0000 and <= 0xE007F) continue;                            // tag block
            sb.Append(rune);
        }

        var cleaned = sb.ToString().Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].TrimEnd() + "…";
    }
    #endregion
}
