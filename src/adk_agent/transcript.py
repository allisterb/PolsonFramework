"""The ADK conversation, transcribed into the project's record.

**The third producer of `agent.jsonl`, and the easiest of the three.** The studio's run page reads
two halves: `server.jsonl`, written by the MCP server whoever is driving, and the conversation that
produced it. Two writers already exist for the second — `orchestrator.transcript` pushes typed steps
as an Antigravity run happens, and `orchestrator.hostlog` reconstructs one by polling the transcript
a Claude Code hook preserves. ADK belonged in the first group and had neither, so an ADK run left a
spine with no conversation and `/studio/observe` showed a record of an agent talking to itself.

This is a push writer, because ADK gives the same vantage point Antigravity does: a `BasePlugin`'s
callbacks fire for *every* agent in the app, outside any of them, so one instance sees the whole run
without each agent having to opt in.

**The vocabulary is not ours to choose.** `hostlog.transcribe` already fixed it — `director/message`,
`agent/thinking`, `agent/text`, `agent/tool.call`, `agent/usage`, each carrying a `uuid` — and the
broker, the CSM coder and the page all read that shape. Emitting anything else here would mean a
second dialect for one reader to reconcile, so this writes the same events with the same fields.

**What it deliberately does not do:**

- *No `EventLog` of its own.* It imports the one `orchestrator` already has, so the format has a
  single implementation. That is also why `src/webapp/orchestrator/` is copied into the image.
- *No failure that reaches the agent.* Every callback returns `None` and swallows what it catches.
  A transcript is a record of the work, and a recorder that can stop the work is worse than no
  recorder — this is the same reasoning `EventLog.append` applies to its own writes.
- *No second timestamp source.* Events are stamped with `orchestrator.events.timestamp()`, the
  format the .NET writer uses, because the page interleaves these with `server.jsonl` and a
  different format sorts wrongly against it.
"""

from __future__ import annotations

import logging
import sys
import uuid as _uuid
from collections import OrderedDict, deque
from pathlib import Path
from typing import Any

_logger = logging.getLogger("polson.transcript")

#: `orchestrator` lives in the webapp tree, which is not on the path by default in this runtime.
#: Added here rather than relying on the studio mount having run: the transcript is worth writing
#: even when the pages are not being served, and a writer that only works when a UI is mounted is a
#: writer that silently stops the day someone disables the UI.
_WEBAPP = Path(__file__).resolve().parent.parent / "webapp"
if _WEBAPP.is_dir() and str(_WEBAPP) not in sys.path:
    sys.path.insert(0, str(_WEBAPP))

#: How much of one text block is kept. Matches `hostlog.clip`'s intent: a record is for reading, and
#: a model's whole reply is not what a reader wants in a timeline entry.
MAX_TEXT = 4000

#: How many invocation ids to remember for the "already begun" check. Two callbacks race to write
#: one `run.begin`, so only the invocation in flight really matters; the rest is slack for a server
#: holding several sessions at once.
BEGUN_MEMORY = 64

#: How many halt reasons to hold waiting for their run to unwind. Same slack as `BEGUN_MEMORY`, and
#: bounded for the same reason: an entry is normally consumed by the `run.end` moments later, but a
#: run that trips and then dies before `after_run_callback` would leave one behind for the life of
#: the process.
HALT_MEMORY = 64

#: Why an invocation was halted, keyed by invocation id, waiting to be spent by its `run.end`.
#: Written by the circuit breaker in `studio`, which already imports this module — the note travels
#: the way the dependency already runs, so neither file needs a lazy import to reach the other.
_halted: OrderedDict[str, dict[str, Any]] = OrderedDict()


def note_halt(invocation: str, *, limit: str, used: float, cap: float) -> None:
    """Records that the circuit breaker halted `invocation`, for its `run.end` to report.

    A halt and an ending are different moments: the breaker fires on a turn that is refused, and the
    run then unwinds through ADK's ordinary success path. So this only leaves the *reason* where the
    terminal event will find it, rather than writing an event of its own.

    Never raises. A recorder that can take down the run it is recording is worse than a gap.
    """
    try:
        if not invocation:
            return
        _halted[invocation] = {"limit": limit, "used": used, "cap": cap}
        while len(_halted) > HALT_MEMORY:
            _halted.popitem(last=False)
    except Exception as exc:                                    # pragma: no cover - defensive
        _logger.debug("polson transcript: halt not noted (%s)", exc)


def clip(text: str) -> tuple[str, bool]:
    """The text as it is recorded, and whether anything was dropped."""
    text = (text or "").strip()
    return (text, False) if len(text) <= MAX_TEXT else (text[:MAX_TEXT], True)


def summarize_args(args: Any) -> dict[str, Any]:
    """Tool arguments, with anything long reduced to a description of itself.

    A drawing script is tens of kilobytes and is already saved to `scripts/` by the server; putting
    it in the conversation record too would make the transcript larger than the work it describes,
    and the page would render a wall of JavaScript where a tool call should be.
    """
    if not isinstance(args, dict):
        return {}

    out: dict[str, Any] = {}
    for key, value in args.items():
        if isinstance(value, str) and len(value) > 200:
            out[key] = f"<{len(value)} chars>"
        elif isinstance(value, (str, int, float, bool)) or value is None:
            out[key] = value
        elif isinstance(value, (list, tuple)):
            out[key] = f"<{len(value)} items>"
        else:
            out[key] = f"<{type(value).__name__}>"
    return out


def make_plugin(project_dir: str | Path):
    """A plugin that transcribes this project's conversation, or None if it cannot be built.

    Returns None rather than raising: an app that will not start because a *recorder* could not be
    constructed is a worse outcome than an app whose run is not written down.
    """
    try:
        from google.adk.plugins.base_plugin import BasePlugin
        from orchestrator.events import EventLog, timestamp
    except Exception as exc:                                    # pragma: no cover - import guard
        _logger.warning("polson transcript: not available (%s)", exc)
        return None

    events = Path(project_dir) / "events"
    events.mkdir(parents=True, exist_ok=True)

    class TranscriptPlugin(BasePlugin):
        """Writes `agent.jsonl` and `director.jsonl` as the run happens."""

        def __init__(self) -> None:
            super().__init__(name="polson_transcript")
            self.agent = EventLog(events / "agent.jsonl", "agent")
            self.director = EventLog(events / "director.jsonl", "director")

            # Which invocations have had their `run.begin` written. Bounded, because this plugin
            # outlives every run in the app process and an unbounded set would grow for as long as
            # the server is up.
            self._begun: deque[str] = deque(maxlen=BEGUN_MEMORY)

        # region Callbacks
        async def before_run_callback(self, *, invocation_context):
            """The start of one ADK invocation, which is what an ADK run *is*.

            This is the cut `session_since` takes for an ADK project. It cannot be `run.start`:
            under ADK the MCP server is spawned once for the whole app process, so its start marks
            the server booting rather than this run beginning, and an observation cut there replays
            every run the app has ever served.
            """
            try:
                self._begin(invocation_context, timestamp())
            except Exception as exc:
                _logger.debug("polson transcript: run start not recorded (%s)", exc)
            return None

        async def on_user_message_callback(self, *, invocation_context, user_message):
            """The director speaking. The only event on the director's side."""
            try:
                # `runners.py` calls this *before* `before_run_callback`, whatever the latter's
                # docstring claims about being first in the lifecycle. So the marker is written from
                # whichever callback sees the invocation first, or the cut would fall after this
                # message and drop the one event saying what the run was asked to do.
                at = timestamp()
                self._begin(invocation_context, at)

                text, truncated = clip(_text_of(user_message))
                if text:
                    self.director.append("message", at=at, uuid=_id(),
                                         text=text, truncated=truncated or None)
            except Exception as exc:
                _logger.debug("polson transcript: user message not recorded (%s)", exc)
            return None

        async def after_model_callback(self, *, callback_context, llm_response):
            """One model turn, flattened into the events it actually contains.

            A single response routinely carries several — a thinking block, some prose and two tool
            calls are four events — and separating them is what lets the sense-making curve see
            deliberation apart from action. Same flattening `hostlog.transcribe` does.
            """
            try:
                self._record(callback_context, llm_response, timestamp())
            except Exception as exc:
                _logger.debug("polson transcript: turn not recorded (%s)", exc)
            return None

        async def after_run_callback(self, *, invocation_context):
            """The run ending, on the path that is not an exception.

            **This is the event the record had no way to express.** `run.begin` was written and
            nothing closed it, so a finished run, a halted one and one wedged mid-turn were the same
            picture to anyone reading the log — which is exactly the question a director asks of a
            run page that has stopped moving.

            A breaker halt arrives here too, because tripping unwinds the invocation normally rather
            than raising; the reason left by `note_halt` is what tells the two apart.
            """
            try:
                self._end(invocation_context, timestamp())
            except Exception as exc:
                _logger.debug("polson transcript: run end not recorded (%s)", exc)
            return None

        async def on_run_error_callback(self, *, invocation_context, error):
            """The run ending badly. ADK skips `after_run_callback` entirely on this path.

            Notification-only in ADK: the exception is re-raised once every plugin has been told, so
            recording it here changes nothing about how the run fails — it only means the record
            says so rather than simply stopping.
            """
            try:
                self._end(invocation_context, timestamp(), failure=error)
            except Exception as exc:
                _logger.debug("polson transcript: run failure not recorded (%s)", exc)
            return None
        # endregion

        # region Methods
        def _begin(self, invocation_context, at: str) -> None:
            """Writes `run.begin` once for this invocation, from whichever callback arrives first."""
            invocation = getattr(invocation_context, "invocation_id", None) or ""
            if invocation and invocation in self._begun:
                return
            self._begun.append(invocation)

            agent = getattr(getattr(invocation_context, "agent", None), "name", None)
            self.agent.append("run.begin", at=at, uuid=_id(),
                              invocation=invocation or None, agent=agent)

        def _end(self, invocation_context, at: str, failure: Exception | None = None) -> None:
            """Writes `run.end`, saying which of the three ways this run finished.

            `reason` is `completed`, `halted` or `failed` — one field, because a reader scanning for
            *did this finish* should not have to infer it from the presence or absence of others.
            The breaker's numbers ride alongside when there are any, so the event answers "why did it
            stop" without a second lookup.
            """
            invocation = getattr(invocation_context, "invocation_id", None) or ""
            agent = getattr(getattr(invocation_context, "agent", None), "name", None)

            # Popped rather than read: the reason belongs to this run, and leaving it behind would
            # let a later invocation that happened to reuse the id inherit a halt it never had.
            halt = _halted.pop(invocation, None) if invocation else None

            if failure is not None:
                reason, extra = "failed", {"error": f"{type(failure).__name__}: {failure}"[:MAX_TEXT]}
            elif halt:
                reason, extra = "halted", halt
            else:
                reason, extra = "completed", {}

            self.agent.append("run.end", at=at, uuid=_id(),
                              invocation=invocation or None, agent=agent, reason=reason, **extra)

        def _record(self, callback_context, llm_response, at: str) -> None:
            turn = _id()
            agent = getattr(callback_context, "agent_name", None)

            # `agent` names which one spoke. Carried on every event because a multi-agent app
            # interleaves turns, and a curve that cannot tell them apart reads two collaborators as
            # one erratic participant.
            common = {"uuid": turn, "agent": agent} if agent else {"uuid": turn}
            wrote = False

            for part in _parts_of(llm_response):
                if getattr(part, "thought", False):
                    text, truncated = clip(getattr(part, "text", "") or "")
                    self.agent.append("thinking", at=at, **common,
                                      text=text or None, truncated=truncated or None,
                                      redacted=None if text else True)
                    wrote = True
                    continue

                if (raw := getattr(part, "text", None)):
                    text, truncated = clip(raw)
                    if text:
                        self.agent.append("text", at=at, **common,
                                          text=text, truncated=truncated or None)
                        wrote = True

                if (call := getattr(part, "function_call", None)) is not None:
                    self.agent.append("tool.call", at=at, **common,
                                      tool=getattr(call, "name", None) or "?",
                                      args=summarize_args(getattr(call, "args", None)))
                    wrote = True

            # Attached once per turn and only when the turn said something, so an empty response
            # does not appear as a cost with nothing to show for it.
            usage = getattr(llm_response, "usage_metadata", None)
            if usage is not None and wrote:
                self.agent.append(
                    "usage", at=at, **common,
                    inputTokens=getattr(usage, "prompt_token_count", None),
                    outputTokens=getattr(usage, "candidates_token_count", None),
                    cacheReadTokens=getattr(usage, "cached_content_token_count", None),
                    thinkingTokens=getattr(usage, "thoughts_token_count", None),
                    model=getattr(llm_response, "model_version", None))
        # endregion

    return TranscriptPlugin()


def _id() -> str:
    """A per-event identity, since ADK supplies none.

    `hostlog` deduplicates on the host's own transcript uuid because it re-reads the same file every
    pass. This writes each event once as it happens, so the uuid is only ever an identity — but the
    field has to be there, because `HostTranscript.written()` reads it back and an event without one
    is invisible to a reader that later syncs the same project from a host transcript.
    """
    return _uuid.uuid4().hex


def _text_of(message: Any) -> str:
    """The prose in a user message, whatever shape it arrived in."""
    if isinstance(message, str):
        return message
    if (text := getattr(message, "text", None)):
        return text
    return "".join(getattr(p, "text", "") or "" for p in _parts_of(message))


def _parts_of(carrier: Any) -> list[Any]:
    """The content parts of a response or a message, or nothing."""
    content = getattr(carrier, "content", None) or carrier
    return list(getattr(content, "parts", None) or [])
