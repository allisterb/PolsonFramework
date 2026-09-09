"""The agent's question to the director, and the wait for an answer.

**The other half of `interject`.** That one carries words the director volunteered; this carries a
question the agent stopped to ask. Both cross the same boundary and neither existed on this runtime
until it was built, but they are not symmetrical: an interjection is fire-and-forget, and a question
suspends the work until somebody answers or the wait runs out.

**Why it matters more than it sounds.** A brief is usually a sentence. The two workflows this studio
offers both tell the agent *"Where the brief is silent, decide — and write down that you did"*, and
that instruction was written around a missing capability rather than chosen: on the Antigravity path
the agent asks, the studio renders the options as buttons, and a director settles a direction in one
click without typing a brief at all. Deciding silently is what an agent does when it has nobody to
ask.

**Nothing in the browser had to be built for this.** The studio's run page has rendered question
cards since Milestone 6 — one submit button per option, a free-text box, and a *"Let it decide"*
skip — and it dispatches on a `question.open` event from the record without knowing or caring which
runtime wrote it. `POST /runs/{id}/answer` is likewise already there. So the whole gap was that
nothing on the ADK side ever published the event, and `ObservedRun.answer` returned False.

**Deliberately not `LongRunningFunctionTool`.** ADK ships one, and it is the wrong tool: it ends the
invocation and resumes only when a later message carries a matching `function_call_id`, which needs
a real session store and reopens the resume-and-wall-clock problem that was considered and declined.
A plain tool that awaits a future works instead, for exactly the reason `interject` works — the
studio is mounted on the ADK app, so the page and the agent are in one process and the future is a
local variable rather than a message.

**The wait is credited back to the deadline.** A director thinking for ninety seconds is not the
agent spending ninety seconds of its allowance, and billing it that way would make asking a question
something an agent should avoid — which is the opposite of the point. See `studio.credit_wait`.
"""

from __future__ import annotations

import asyncio
import logging
import os
import threading
from pathlib import Path
from typing import Any

_logger = logging.getLogger("polson.ask")

#: Open questions, per project id, by question id. A dict rather than a deque: unlike interjections
#: these are addressed individually, because the answer names the question it settles.
_pending: dict[str, dict[str, asyncio.Future]] = {}

#: Question ids are per project and monotonic, so `q3` means the same thing to the page, the record
#: and the agent for the life of a run.
_counter: dict[str, int] = {}

_lock = threading.Lock()

#: How long the agent waits. **Far shorter than the Antigravity path's 600s, and deliberately so.**
#: There the director drives the run and a long wait costs nothing; here the wait is credited back to
#: the token deadline but not to the .NET side's `Stage.elapsedMinutes`, and a judge who has opened
#: the page and wandered off should not strand a run for ten minutes. The answer is a button click.
try:
    TIMEOUT = max(15.0, float(os.environ.get("POLSON_ASK_SECONDS", "120")))
except ValueError:
    TIMEOUT = 120.0

#: More than this and it is a form, not a question. The page lays the buttons out in one row.
MAX_OPTIONS = 6

#: A question is a sentence and an option is a phrase. Both reach a browser, and both are clipped
#: rather than refused — a question that arrives truncated is still answerable, and one refused for
#: length costs the agent a turn to discover why.
MAX_QUESTION = 500
MAX_OPTION = 120

#: How many questions may be open at once for one project. The agent asks one and waits, so more
#: than one means a second agent in a multi-agent run — or a loop, which this bounds.
MAX_OPEN = 4


# region Public
def pending(project: str) -> list[str]:
    """Ids currently waiting on an answer, oldest first."""
    with _lock:
        return [qid for qid, future in _pending.get(project, {}).items() if not future.done()]


def reply(project: str, question_id: str, *, text: str = "",
          selected: list[str] | None = None, skipped: bool = False) -> bool:
    """Settles an open question. False if there is no such question, or it is already settled.

    The caller turns that into its own refusal — the studio answers `409` and says the question may
    have timed out or been answered in another window, which this has no vocabulary for.
    """
    with _lock:
        future = _pending.get(project, {}).get(question_id)
        if future is None or future.done():
            return False

        answer = {"text": (text or "").strip(), "selected": list(selected or []),
                  "skipped": bool(skipped)}
        loop = future.get_loop()

    # Set on the future's own loop rather than here. The route runs on the same loop today, so this
    # is the same guard `interject._lock` is: cheap now, and the difference between working and
    # corrupting state the day a route moves to a threadpool.
    loop.call_soon_threadsafe(_settle, future, answer)
    return True


def abandon(project: str) -> int:
    """Skips every open question and returns how many. What a run being torn down does first.

    An agent left awaiting a future nobody will ever resolve does not end, and a run that will not
    end is worse than one that guessed.
    """
    settled = 0
    for question_id in pending(project):
        if reply(project, question_id, skipped=True):
            settled += 1
    return settled


def make_tool(project_dir: str | Path):
    """Builds `ask_director`, bound to one project. None if the record cannot be written.

    Bound rather than parameterised for the reason `_make_peek` is: the project is a property of the
    app, and a project argument the model could set is a way to put a question in front of the wrong
    director.
    """
    try:
        from orchestrator.events import EventLog, timestamp
    except Exception as exc:                                    # pragma: no cover - import guard
        _logger.warning("polson ask: not available (%s)", exc)
        return None

    root = Path(project_dir)
    project = root.name
    log = EventLog(root / "events" / "director.jsonl", "director")

    # A new run owns this project id outright, so nothing from a previous one may still be waiting
    # under it. Normally settles nothing — a run cannot end while an agent is inside a tool call, so
    # the only way to leave one behind is to be killed mid-wait, which is exactly the case Cloud Run
    # supplied twice in one day and the reason `mirror` exists.
    if stale := abandon(project):
        _logger.warning("polson ask: %d stale question(s) dropped for %s", stale, project)

    async def ask_director(question: str, options: list[str], tool_context) -> dict:
        """Ask the director a question and wait for their answer.

        Use this where the brief is silent on something you cannot settle by drawing — a direction,
        a palette, which of two readings of the subject to take. Offer options wherever you can:
        the director answers by clicking one, so a question with options is settled in a second and
        an open one has to be typed. Put the option you would take first.

        Prefer it to guessing on a decision that is expensive to reverse, and prefer deciding to
        asking on one that is not — a pass is cheap and the record makes it reversible. Do not ask
        permission for each step.

        Args:
            question: What you need to know, in one sentence, in the director's language rather than
                in measurements. They can see the renders and the notes, not your reasoning.
            options: The answers you would accept, best first — for example
                `["Warm and editorial", "Cool and technical"]`. Pass an empty list `[]` for an open
                question. At most 6; longer lists are clipped.

        Returns:
            A dict with `answered`, and `answer` carrying what the director said or clicked. When
            `answered` is False, `answer` says why — nobody was there, or they asked you to decide —
            and you should choose, say in a `Stage.note` which you chose and why, and carry on.
        """
        asked = (question or "").strip()[:MAX_QUESTION]
        if not asked:
            return {"answered": False, "answer": "No question was asked, so nothing was put to the "
                                                 "director. Call this with a question, or decide."}

        choices = [str(o).strip()[:MAX_OPTION] for o in (options or []) if str(o).strip()]
        choices = choices[:MAX_OPTIONS]

        with _lock:
            open_now = [q for q, f in _pending.get(project, {}).items() if not f.done()]
            if len(open_now) >= MAX_OPEN:
                return {"answered": False, "answer": (
                    f"{len(open_now)} questions are already waiting for this run, which is the "
                    "limit. Decide this one yourself, say in a Stage.note what you chose and why, "
                    "and carry on.")}

            _counter[project] = _counter.get(project, 0) + 1
            question_id = f"q{_counter[project]}"
            future: asyncio.Future = asyncio.get_running_loop().create_future()
            _pending.setdefault(project, {})[question_id] = future

        # Into the record before the wait, because the record *is* the transport: the page tails
        # director.jsonl and renders the card from this line. Publishing after the wait would ask a
        # question nobody could see.
        _write(log, timestamp, "question.open", id=question_id, text=asked,
               options=choices or None, multi=False, timeout=TIMEOUT)
        _logger.warning("polson ask: %s asked %s (%d option(s))", project, question_id, len(choices))

        waited = 0.0
        started = asyncio.get_running_loop().time()
        try:
            answer = await asyncio.wait_for(asyncio.shield(future), timeout=TIMEOUT)
            outcome, settled = "answered", True
        except (asyncio.TimeoutError, TimeoutError):
            answer, outcome, settled = None, "timeout", False
        except asyncio.CancelledError:
            _close(project, question_id)
            _write(log, timestamp, "question.closed", id=question_id, reason="cancelled")
            raise
        finally:
            waited = asyncio.get_running_loop().time() - started
            _close(project, question_id)

        # **Credited whichever way it went.** A question that timed out cost the agent the whole
        # wait and bought it nothing; billing that against the deadline as well would be the worst
        # of both.
        _credit(tool_context, waited)
        _write(log, timestamp, "question.closed", id=question_id, reason=outcome)

        if not settled:
            return {"answered": False, "waitedSeconds": round(waited, 1), "answer": (
                f"No answer arrived within {TIMEOUT:.0f}s, so this question went unanswered. Choose "
                f"the direction you judge best, say in a Stage.note which one you chose and why, "
                f"and carry on. The wait was not charged against your deadline.")}

        if answer["skipped"] and not answer["text"] and not answer["selected"]:
            return {"answered": False, "waitedSeconds": round(waited, 1), "answer": (
                "The director asked you to decide this one. Choose, say in a Stage.note which you "
                "chose and why, and carry on.")}

        # A click and a typed reply can both arrive: the page submits the text field alongside the
        # option button. Both are given, in that order, rather than one being dropped — the typed
        # line is usually a qualification of the click rather than an alternative to it.
        said = " ".join(part for part in (", ".join(answer["selected"]), answer["text"]) if part)
        _write(log, timestamp, "answer", id=question_id, text=said)
        return {"answered": True, "waitedSeconds": round(waited, 1), "answer": said}

    return ask_director
# endregion


# region Implementation
def _settle(future: asyncio.Future, answer: dict[str, Any]) -> None:
    if not future.done():
        future.set_result(answer)


def _close(project: str, question_id: str) -> None:
    with _lock:
        _pending.get(project, {}).pop(question_id, None)


def _credit(tool_context: Any, seconds: float) -> None:
    """Gives the waiting time back to this invocation's clocks.

    Imported here rather than at module scope: `studio` builds the tool, so importing it at the top
    is a cycle. It is also optional — a test exercising the channel alone has no runtime to credit.
    """
    if seconds <= 0:
        return
    try:
        import studio
        studio.credit_wait(getattr(tool_context, "invocation_id", None),
                           getattr(tool_context, "agent_name", None), seconds)
    except Exception as exc:                                    # pragma: no cover - optional
        _logger.debug("polson ask: wait not credited (%s)", exc)


def _write(log: Any, timestamp: Any, kind: str, **fields: Any) -> None:
    """Appends to the record, never raising. A question that cannot be filed is still asked.

    `EventLog.append` already swallows its own failures for this reason; this guards the call around
    it, since a missing `events/` directory would otherwise take the run down from inside a tool.
    """
    try:
        log.append(kind, at=timestamp(), **{k: v for k, v in fields.items() if v is not None})
    except Exception as exc:
        _logger.debug("polson ask: %s not recorded (%s)", kind, exc)
# endregion
