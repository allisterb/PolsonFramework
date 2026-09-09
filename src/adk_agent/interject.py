"""The director's channel into an ADK run that is already going.

Answering a question and interrupting are different acts, and Polson needs both. The first is a hook
the agent chose to reach; this is the other direction — the director says something the agent did not
ask for, mid-stride. *"Make the background red"* is not an answer to anything.

Under the enactive account this is not a convenience. Davis's studies of collaborative drawing have
the novice's **disruptions** acting as creative catalysts — contributions that open affordances the
expert would not have reached alone. So an interruption belongs in the record as a contribution
rather than as a correction, which is how `transcript` files it.

**Contribution cuts both ways, and the second way is the one worth stating.** A suggestion taken as a
correction is an override — and the studio holds craft the director did not bring: the manuals, the
measured integrity rules, the form a deliverable has to take. *"Start the bars at 50 so the gap looks
bigger"* is a lie factor, whoever asked for it. So the agent is told to take the **intent** rather
than the letter where the two conflict, and to say in the same note what it took and what it did
instead. That is not a licence to ignore the director: an unrecorded departure is exactly the failure
this channel exists to prevent, and the note is what makes it a conversation rather than a refusal.

**The ADK half of `orchestrator/interject.py`, and the mechanism is completely different.** That one
rides the Antigravity SDK's trigger surface: a coroutine lives for the agent's lifetime and may
`ctx.send(...)` at any moment. ADK offers no equivalent — an invocation is a single `run_async`, and
there is no supported way to push a new message into one that is already running.

What ADK does offer is `after_tool_callback`, which may return a **replacement tool result**. The
studio watchdog already uses it to put a directive in front of a role that has stopped making
progress, so the path is proven rather than invented here. A drawing agent calls tools every few
seconds, so an interjection waits for the next call and no longer.

**Appended to the result, never substituted for it.** The agent asked for a render path or a file's
contents and still needs them; taking those away to deliver a message would strand it mid-pass.

Nothing here is configured. The queue lives in the process the studio and the runtime already share —
`main.py` mounts the studio on the ADK app — so the director's words cross no boundary at all.
"""

from __future__ import annotations

import logging
import threading
from collections import defaultdict, deque
from pathlib import Path
from typing import Any

_logger = logging.getLogger("polson.interject")

#: Waiting words, per project id. Bounded: a director holding the send key while an agent thinks is
#: not a reason to grow without limit, and the oldest lines are the least worth delivering late.
_pending: dict[str, deque[str]] = defaultdict(lambda: deque(maxlen=MAX_WAITING))

#: Both sides run on one event loop today, so this guards nothing that is currently contended. It is
#: here because "currently" is doing work in that sentence: a route moved to a threadpool, or a
#: runner moved to its own thread, would otherwise corrupt a deque silently.
_lock = threading.Lock()

#: How many interjections may wait at once for one project.
MAX_WAITING = 8

#: One interjection is a sentence. Longer direction belongs in a brief, which is sanitised on the way
#: in; this reaches a running agent, so it stays short. Matches the studio's own cap.
MAX_CHARS = 2000


# region Public
def offer(project: str, text: str) -> bool:
    """Queues `text` for the agent working on `project`. False when there is nothing to say.

    Returns as soon as it is queued rather than when it is delivered. The two are seconds apart and
    conflating them would make the director's box hang on the agent's next tool call.
    """
    said = (text or "").strip()
    if not said or not project:
        return False

    with _lock:
        _pending[project].append(said[:MAX_CHARS])
        depth = len(_pending[project])

    _logger.warning("polson interject: queued for %s (%d waiting)", project, depth)
    return True


def waiting(project: str) -> int:
    """How many lines are queued for that project and not yet delivered."""
    with _lock:
        return len(_pending.get(project, ()))


def take(project: str) -> list[str]:
    """Everything queued for `project`, removed. Empty when there is nothing."""
    with _lock:
        queue = _pending.get(project)
        if not queue:
            return []
        lines = list(queue)
        queue.clear()
    return lines


def make_plugin(project_dir: str | Path):
    """A plugin that delivers this project's interjections, or None if it cannot be built.

    Returns None rather than raising, as the other two plugins do: an app that will not start because
    a *message channel* could not be constructed is a worse outcome than one that starts without it.
    """
    try:
        from google.adk.plugins.base_plugin import BasePlugin
    except Exception as exc:                                    # pragma: no cover - import guard
        _logger.warning("polson interject: not available (%s)", exc)
        return None

    project = Path(project_dir).name

    class InterjectPlugin(BasePlugin):
        """Hands the director's words to the agent at its next tool call."""

        def __init__(self) -> None:
            super().__init__(name="polson_interject")

        async def after_tool_callback(self, *, tool, tool_args, tool_context, result):
            """Appends anything the director said, or leaves the result exactly as it was.

            Returning None is ADK's "unchanged", and is the answer on nearly every call — the queue
            is empty almost always, so this is one dict lookup on the hot path.
            """
            lines = take(project)
            if not lines:
                return None

            amended = attach(result, lines)
            if amended is None:
                # Put them back rather than dropping them: an undeliverable shape now is very likely
                # a deliverable one at the next call, and a lost interjection is invisible.
                with _lock:
                    _pending[project].extendleft(reversed(lines))
                return None

            _logger.warning("polson interject: delivered %d line(s) to %s via %s",
                         len(lines), project, getattr(tool, "name", "?"))
            return amended

    return InterjectPlugin()


def attach(result: Any, lines: list[str]) -> Any:
    """The tool result the agent asked for, with the director's words appended. None if it cannot be.

    **Two result shapes, and handling only the first is what made the watchdog inert once already.**
    An MCP tool returns `{"content": [{"type": "text", ...}], "isError": bool}`; a `FunctionTool` —
    `write_script`, `peek`, `read_file` — returns a plain dict of its own fields. Both have to be
    understood or the message is silently dropped on whichever the agent happened to call.
    """
    if not isinstance(result, dict):
        _logger.warning("polson interject: cannot attach to a %s result", type(result).__name__)
        return None

    body = " ".join(f"[director] {line}" for line in lines)
    text = (f"{body}\n\nThis is the director speaking to you mid-run, not a tool result and not an "
            f"answer to a question you asked.\n\n"
            f"**If they asked you something, answer it in a `Stage.note` — that is the only place "
            f"they can read your reply.** Finding the answer is not the same as giving it, and the "
            f"director can see neither your tool results nor your reasoning.\n\n"
            f"If they asked for a change, take it as a contribution rather than a correction: say "
            f"in a `Stage.note` how you are taking it, then act on it in your next pass. Where it "
            f"cuts against something the studio knows — a manual, an integrity rule, the form the "
            f"deliverable has to take — **take the intent rather than the letter**, and say in that "
            f"note what you took and what you did instead. Either way, carry on with what you were "
            f"doing afterwards — this is an aside, not a new brief.")

    amended = dict(result)
    content = amended.get("content")
    if isinstance(content, list):
        amended["content"] = [*content, {"type": "text", "text": text}]
    else:
        # A distinctive key, easier to find in a transcript than prose merged into another field.
        amended["director_says"] = text
    return amended
# endregion
