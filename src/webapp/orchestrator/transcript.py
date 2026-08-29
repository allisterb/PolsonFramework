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

from typing import Any

from .events import EventLog

#: Long enough for real reasoning, short enough that one turn cannot make the file unreadable.
MAX_TEXT = 4000

#: An argument longer than this is described rather than quoted. A script argument is the whole
#: program, and the server already saves it to `scripts/` — copying it here would double the record
#: and bury everything else.
MAX_ARG = 200


def _clip(text: str, limit: int = MAX_TEXT) -> tuple[str, bool]:
    """Returns the text bounded to `limit`, and whether it had to be cut."""
    text = text or ""
    return (text, False) if len(text) <= limit else (text[:limit], True)


def _summarize_args(args: dict[str, Any]) -> dict[str, Any]:
    """Keeps short arguments verbatim and replaces long ones with their size."""
    summary: dict[str, Any] = {}
    for key, value in (args or {}).items():
        if isinstance(value, str) and len(value) > MAX_ARG:
            summary[key] = f"<{len(value)} chars>"
        else:
            summary[key] = value
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

    # region Turn lifecycle
    def turn_start(self, prompt: str) -> None:
        """Opens a turn. The prompt is recorded by size, not text — the director's own file holds the words."""
        self._unfinished.clear()
        self._written.clear()
        self._emit("turn.start", chars=len(prompt or ""))

    def turn_end(self, status: str, error: str | None = None) -> None:
        """Closes a turn, writing down whatever had not finished."""
        for step in sorted(self._unfinished.values(), key=lambda s: getattr(s, "step_index", 0)):
            self._write(step, partial=True)
        self._unfinished.clear()

        self._emit("turn.end", status=status, error=error or None)
    # endregion

    # region Steps
    def observe(self, step: Any) -> None:
        """Takes one step from `receive_steps()`, writing it only once it has settled."""
        key = getattr(step, "id", "") or f"#{getattr(step, 'step_index', 0)}"
        if key in self._written:
            return

        if self._status(step) in self.SETTLED:
            self._unfinished.pop(key, None)
            self._written.add(key)
            self._write(step)
        else:
            self._unfinished[key] = step

    @staticmethod
    def _status(step: Any) -> str:
        return getattr(getattr(step, "status", None), "name", "") or ""

    def _write(self, step: Any, *, partial: bool = False) -> None:
        kind = getattr(getattr(step, "type", None), "name", "UNKNOWN")

        # A step that ended badly says so on its own event. It is not the end of the turn — the SDK
        # retries a 429 and carries on — so it must not be recorded as one.
        state = {
            "status": self._status(step) if self._status(step) != "DONE" else None,
            "error": getattr(step, "error", "") or None,
            "partial": partial or None,
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
                    args=_summarize_args(getattr(call, "args", None) or {}),
                    **state,
                )

        elif kind == "THINKING":
            text, truncated = _clip(getattr(step, "thinking", "") or getattr(step, "content", ""))
            if text:
                self._emit("thinking", text=text, truncated=truncated or None, **state)

        elif kind == "COMPACTION":
            # Invisible in any UI, and the only thing that explains an agent apparently forgetting
            # its own earlier decisions.
            self._emit("compaction", **state)

        else:
            # TEXT_RESPONSE, SYSTEM_MESSAGE, FINISH and UNKNOWN all reduce to text the reader wants;
            # `source` keeps them distinguishable without a second vocabulary.
            text, truncated = _clip(getattr(step, "content", ""))
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
        usage = getattr(step, "usage_metadata", None)
        if usage is None:
            return

        self._emit(
            "usage",
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
