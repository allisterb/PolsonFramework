"""The agent's live turn stream, transcribed into `events/agent.jsonl`.

`server.jsonl` records what the agent *did* — scripts, renders, the stages it declared. This records
what it was *thinking* while doing it, which is the half that explains the other. Without it a
reader sees a mark appear and no account of why that mark and not another.

The vocabulary deliberately mirrors the SDK's own `StepType` rather than inventing a parallel
taxonomy: `turn.start`, `thinking`, `tool.call`, `text`, `compaction`, `turn.end`, `usage`.

**`receive_steps()` re-yields a step as it accumulates**, so a streaming answer arrives as a dozen
progressively longer versions of one step, and — this is the part that catches you — *steps
interleave*: a THINKING step and the TOOL_CALL step after it alternate, each re-yielded several
times, before either finishes. Treating "a different step arrived" as "the previous one is finished"
therefore writes the same tool call three or four times; a first run recorded 17 `tool.call` events
for 6 scripts the server actually ran.

`status` is what settles it. A step is `ACTIVE` while it accumulates and `DONE` when it is final, so
each step is written exactly once, when it finishes. Anything still unfinished when the turn ends —
a timeout, a cancellation — is written then and marked `partial`, because a step that never
completed is still evidence of what the agent was doing.
"""

from __future__ import annotations

import time
from typing import Any

from .events import EventLog

#: Long enough for real reasoning, short enough that one turn cannot make the file unreadable.
MAX_TEXT = 4000

#: An argument longer than this is described rather than quoted. A script argument is the whole
#: program, and the server already saves it to `scripts/` — copying it here would double the record
#: and bury everything else.
MAX_ARG = 200


def clip(text: str, limit: int = MAX_TEXT) -> tuple[str, bool]:
    """Returns the text bounded to `limit`, and whether it had to be cut."""
    text = text or ""
    return (text, False) if len(text) <= limit else (text[:limit], True)


#: Arguments that are whole documents rather than descriptions of an action. For these the length is
#: the only useful summary: the content is already saved elsewhere — a script into `scripts/`, a file
#: edit into the file — and an excerpt of it in the record is noise beside the real thing.
WHOLE_DOCUMENT = frozenset({"script", "content", "new_string", "old_string", "prompt"})


def summarize_args(args: dict[str, Any]) -> dict[str, Any]:
    """Keeps short arguments verbatim; long ones become a leading excerpt and their size.

    The excerpt is the point. A shell command replaced by `<453 chars>` says a tool ran and nothing
    about what it was doing, which is exactly no help when one has been running for twenty minutes —
    while its first line, `cat >> critique_log.md << 'EOF'`, says everything. Only arguments that are
    *documents* are reduced to a bare length, because for those the content lives somewhere better.
    """
    summary: dict[str, Any] = {}
    for key, value in (args or {}).items():
        if not isinstance(value, str) or len(value) <= MAX_ARG:
            summary[key] = value
        elif key in WHOLE_DOCUMENT:
            summary[key] = f"<{len(value)} chars>"
        else:
            summary[key] = f"{value[:MAX_ARG].rstrip()}… <{len(value)} chars>"
    return summary


class Transcript:
    """Turns SDK steps into agent events. One per run; owns nothing but its log."""

    #: A step in one of these states will not change again, so it can be written.
    SETTLED = frozenset({"DONE", "ERROR", "CANCELED"})

    def __init__(self, log: EventLog) -> None:
        self.log = log
        self.counts: dict[str, int] = {}
        self._unfinished: dict[str, Any] = {}
        self._written: set[str] = set()
        self._first_seen: dict[str, float] = {}

    # region Turn lifecycle
    def turn_start(self, prompt: str) -> None:
        """Opens a turn. The prompt is recorded by size, not text — the director's own file holds the words."""
        self._unfinished.clear()
        self._written.clear()
        self._first_seen.clear()
        self._emit("turn.start", chars=len(prompt or ""))

    def turn_end(self, status: str, error: str | None = None) -> None:
        """Closes a turn, writing down whatever had not finished."""
        for key, step in sorted(self._unfinished.items(), key=lambda kv: getattr(kv[1], "step_index", 0)):
            self._write(step, partial=True, elapsed=self._elapsed(key))
        self._unfinished.clear()

        self._emit("turn.end", status=status, error=error or None)
    # endregion

    # region Steps
    def observe(self, step: Any) -> None:
        """Takes one step from `receive_steps()`, writing it only once it has settled."""
        key = getattr(step, "id", "") or f"#{getattr(step, 'step_index', 0)}"
        if key in self._written:
            return

        # The SDK's Step carries no timestamp of its own — the desktop host's transcript does, the
        # library's model does not — so first sight is the only start time available. Without it every
        # event is an instant and a run has no durations: no way to tell deliberation from a slow
        # tool, which is exactly the distinction the interaction record needs to be worth reading.
        self._first_seen.setdefault(key, time.monotonic())

        if self._status(step) in self.SETTLED:
            self._unfinished.pop(key, None)
            self._written.add(key)
            self._write(step, elapsed=self._elapsed(key))
        else:
            self._unfinished[key] = step

    def _elapsed(self, key: str) -> int | None:
        """Milliseconds from first sight of a step to now, or None if it was never seen before."""
        started = self._first_seen.pop(key, None)
        return None if started is None else max(0, round((time.monotonic() - started) * 1000))

    @staticmethod
    def _status(step: Any) -> str:
        return getattr(getattr(step, "status", None), "name", "") or ""

    def _write(self, step: Any, *, partial: bool = False, elapsed: int | None = None) -> None:
        kind = getattr(getattr(step, "type", None), "name", "UNKNOWN")

        # A step that ended badly says so on its own event. It is not the end of the turn — the SDK
        # retries a 429 and carries on — so it must not be recorded as one.
        #
        # `depth` and `trajectory` are what distinguish a subagent's work from the main agent's. The
        # SDK carries both on every step and we were dropping them, which flattened a run with
        # delegated work into one undifferentiated sequence — the one thing a record of collaboration
        # cannot afford to lose.
        depth = getattr(step, "depth", 0) or 0
        state = {
            "status": self._status(step) if self._status(step) != "DONE" else None,
            "error": getattr(step, "error", "") or None,
            "partial": partial or None,
            "ms": elapsed,
            "depth": depth or None,
            "trajectory": getattr(step, "trajectory_id", "") or None if depth else None,
        }

        if kind == "TOOL_CALL":
            # A step can carry several calls; each is its own event, so a reader counts calls rather
            # than steps.
            for call in getattr(step, "tool_calls", None) or []:
                self._emit(
                    "tool.call",
                    call=getattr(call, "id", None),
                    tool=str(getattr(call, "name", "?")),
                    server=getattr(call, "server_name", None),
                    args=summarize_args(getattr(call, "args", None) or {}),
                    **state,
                )

        elif kind == "THINKING":
            text, truncated = clip(getattr(step, "thinking", "") or getattr(step, "content", ""))
            if text:
                self._emit("thinking", text=text, truncated=truncated or None, **state)

        elif kind == "COMPACTION":
            # Invisible in any UI, and the only thing that explains an agent apparently forgetting
            # its own earlier decisions.
            self._emit("compaction", **state)

        else:
            # TEXT_RESPONSE, SYSTEM_MESSAGE, FINISH and UNKNOWN all reduce to text the reader wants;
            # `source` keeps them distinguishable without a second vocabulary.
            text, truncated = clip(getattr(step, "content", ""))
            if text:
                self._emit(
                    "text",
                    text=text,
                    truncated=truncated or None,
                    source=getattr(getattr(step, "source", None), "name", None),
                    complete=getattr(step, "is_complete_response", None) or None,
                    **state,
                )

        self._usage(step)

    def _usage(self, step: Any) -> None:
        """Records what the step cost, when the model reported it."""
        self._record_usage(getattr(step, "usage_metadata", None), scope="step")

    def record_turn_usage(self, conversation: Any) -> None:
        """
        Records what the whole turn cost, from the conversation's own accumulator.

        **This is where the numbers actually are.** `AgentStep.usage_metadata` is declared by the SDK
        and looks like the obvious place to read, so that is what this transcriber read for months —
        but the local harness never populates it. Measured across two complete Apollo runs: 45 tool
        calls, three compactions, and **zero** usage events. The emitter looked correct and fired
        never, which is why nobody noticed and why a director asking what a run cost got no answer.

        `Conversation.last_turn_usage` is the accumulator behind `ExecutionTurn.usage_metadata`, and
        it carries the turn's totals. The per-step read is kept because a harness that does fill it
        gives finer grain for free, and a duplicate is distinguishable: every record says its `scope`.
        """
        self._record_usage(getattr(conversation, "last_turn_usage", None), scope="turn")

    def _record_usage(self, usage: Any, *, scope: str) -> None:
        if usage is None:
            return

        # Cached input bills at roughly a tenth of uncached, so a total without it is an upper bound
        # rather than a cost. The ADK runtime learned this on a 23.7M-token run that could not be
        # priced afterwards; the same fields are recorded here so the two runtimes stay comparable.
        self._emit(
            "usage",
            scope=scope,
            prompt=getattr(usage, "prompt_token_count", None),
            cached=getattr(usage, "cached_content_token_count", None),
            output=getattr(usage, "candidates_token_count", None),
            thinking=getattr(usage, "thoughts_token_count", None),
            total=getattr(usage, "total_token_count", None),
        )
    # endregion

    def _emit(self, kind: str, **fields: Any) -> None:
        self.counts[kind] = self.counts.get(kind, 0) + 1
        self.log.append(kind, **fields)
