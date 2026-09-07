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

    /// <summary>
    /// Minutes since this run began, or null outside a session. Read as <c>Stage.elapsedMinutes</c>.
    /// </summary>
    /// <remarks>
    /// **The sandbox has `Date.now()` and `mina.time()`, and both answer "now" rather than "since
    /// when".** Without this the only way to pace a deadline was for the agent to stamp its own
    /// start — `Session.startedAt ??= Date.now()` — which measures from whenever it first ran a
    /// script, not from when the run began. That understates, silently, and returns a number that
    /// looks like an answer: an agent that spends twelve minutes reading the brief and the manuals
    /// before its first execution stamps minute twelve as zero. Two runs of one brief differed by
    /// four scripts before the first render, so the same recipe gave them very different baselines
    /// for the same true elapsed time.
    /// <para>
    /// Anchored on <see cref="SessionContext.StartedUtc"/>, which the server has known all along and
    /// never told anyone. Null rather than zero when there is no session, matching
    /// <see cref="Current"/>: outside a run there is no elapsed time, and zero would be a claim.
    /// </para>
    /// </remarks>
    public double? ElapsedMinutes => session is null
        ? null
        : (DateTimeOffset.UtcNow - session.StartedUtc).TotalMinutes;
    #endregion

    #region Methods
    /// <summary>
    /// Declares the stage of work. Persists across executions until changed or ended.
    /// </summary>
    /// <remarks>
    /// Beginning a <em>different</em> stage while one is open closes the previous one first, so the
    /// record never shows two stages running at once and a viewer can treat the events between begin
    /// and end as a contiguous group.
    /// <para>
    /// Re-declaring the stage you are already in is an announcement, not a transition: it records a
    /// <c>stage.continue</c> and leaves the stage running. The first agent run declared
    /// <c>Stress test</c> at the top of three consecutive scripts — reasonably, since that is where
    /// it said what it was about to do — and produced an end/begin pair each time, chopping one
    /// stage into three and burying the real transitions among them.
    /// </para>
    /// <para>
    /// The comparison ignores case so <c>Stress Test</c> continues <c>Stress test</c> rather than
    /// forking the record over a capital letter; the originally recorded spelling is kept and
    /// returned.
    /// </para>
    /// </remarks>
    public string Begin(string name)
    {
        var clean = Clean(name, MaxNameLength);
        if (clean.Length == 0) throw new ArgumentException("A stage needs a name.", nameof(name));

        var current = session?.Stage;

        if (current is { Length: > 0 } && string.Equals(current, clean, StringComparison.OrdinalIgnoreCase))
        {
            events?.Append("stage.continue", current, executionId);
            return current;
        }

        if (current is { Length: > 0 })
        {
            events?.Append("stage.end", current, executionId, new Dictionary<string, object?> { ["reason"] = "superseded" });
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

    /// <summary>
    /// Records what you expect the next render to show, <em>before</em> you make it.
    /// </summary>
    /// <remarks>
    /// The record carries actions and, since <see cref="ProbeScope"/>, what measurements found. What it
    /// cannot carry on its own is what anybody wanted — so a run that measured and was satisfied and a
    /// run that measured, found the value wrong and redrew four times leave almost the same trace.
    /// <para>
    /// A claim written down first is the difference. It is worth stating as something checkable — "the
    /// accent should be under 15% of the frame", "the horizon within 4px of y=380" — because the point
    /// is to be able to be wrong about it.
    /// </para>
    /// </remarks>
    public void Expect(string claim)
    {
        var clean = Clean(claim, MaxNoteLength);
        if (clean.Length == 0) return;

        events?.Append("expect", session?.Stage, executionId, new Dictionary<string, object?> { ["claim"] = clean });
    }

    /// <summary>
    /// Records the verdict on a claim, and returns <paramref name="passed"/> so it can be branched on.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="Expect"/>, and usable without it — most checks are stated and
    /// settled in the same script. Returning the verdict means the call reads as the test it is:
    /// <c>if (!Stage.check('accent under 15%', share &lt; 0.15)) { … }</c>.
    /// <para>
    /// A failing check is not a failing run. It is the most useful thing a record can contain: a stage
    /// that states four checks and passes three has said precisely where it stands, which is more than
    /// a stage full of renders and no claims can say at any length.
    /// </para>
    /// </remarks>
    public bool Check(string claim, bool passed, string? detail = null)
    {
        // No guard for swapped arguments here: Jint's overload resolution already refuses
        // `check(boolean, string)` before the call reaches this method, so a check that could not
        // fail never gets recorded. What was wrong was the *message* — see ArgumentHelp in
        // JsDrawingEngine, which now names the swap instead of advising a hunt for a typo. A guard
        // on this side would be dead code and would also refuse the legitimate claim "true".
        var clean = Clean(claim, MaxNoteLength);
        if (clean.Length == 0) return passed;

        var fields = new Dictionary<string, object?>
        {
            ["claim"] = clean,
            ["passed"] = passed
        };

        var cleanDetail = Clean(detail, MaxNoteLength);
        if (cleanDetail.Length > 0) fields["detail"] = cleanDetail;

        events?.Append("check", session?.Stage, executionId, fields);
        return passed;
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
