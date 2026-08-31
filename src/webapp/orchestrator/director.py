"""The human side of the loop, and its record.

The agent asks questions — Manual 12 tells it to stop after the concept and the mood board and let
the director rule a direction out. Somebody has to answer, and `events/director.jsonl` has to say
what was asked and what came back, because a run where the human changed the direction is
unreadable without it.

Two directors, one interface:

- `ConsoleDirector` — a person at the terminal. This is what makes the standalone profile real
  before the web app exists.
- `AbsentDirector` — nobody is attached, so every question is recorded and skipped. The agent is
  told plainly that it was not answered rather than being left to wait for a reply that cannot come.

Milestone 6 adds a third backed by the browser. It replaces `ConsoleDirector` and nothing else:
the hook, the event vocabulary and the file are already the contract.
"""

from __future__ import annotations

import asyncio
import sys
from dataclasses import dataclass, field
from typing import Any, Callable

from google.antigravity import types
from google.antigravity.hooks import hooks

from .events import EventLog


@dataclass(frozen=True)
class Reply:
    """One answer travelling from a watcher back to the director."""

    text: str = ""
    selected: list[str] = field(default_factory=list)
    skipped: bool = False


class Director(hooks.OnInteractionHook):
    """Answers the agent's questions and records the exchange."""

    #: Whether a human can actually be reached. It selects the agent's behaviour — INTERACTIVE means
    #: the agent may stop and ask — so it is a property of the director rather than of its class,
    #: which is what lets a third one be added without `run_turn` learning its name.
    attended = False

    def __init__(self, log: EventLog) -> None:
        self.log = log
        self.asked = 0

    async def run(self, context: hooks.HookContext, data: Any) -> types.QuestionHookResult:
        questions = list(getattr(data, "questions", None) or [])
        if not questions:
            return types.QuestionHookResult(responses=[])

        responses = []
        for entry in questions:
            self.asked += 1
            options = [getattr(o, "text", "") for o in getattr(entry, "options", None) or []]
            self.log.append("question", text=getattr(entry, "question", ""), options=options or None)

            response = await self.answer(entry, options)
            responses.append(response)

            self.log.append(
                "answer",
                text=response.freeform_response or None,
                selected=response.selected_option_ids or None,
                skipped=response.skipped or None,
            )

        return types.QuestionHookResult(responses=responses)
    def __deepcopy__(self, memo: dict[int, Any]) -> "Director":
        """A handle to something live, not a value: a copy of one must be the same object.

        The SDK deep-copies the whole `LocalAgentConfig` in `Agent.__init__`, and hooks and
        triggers are fields on it — so everything reachable from them is copied at startup.
        A `threading.Lock` cannot be, and the run died with `TypeError: cannot pickle
        '_thread.lock' object` before it had done anything. `EventLog` already carried this
        guard for the same reason; a copied director would hold its own pending questions, so an
        answer would settle a future nothing is waiting on.
        """
        memo[id(self)] = self
        return self

    def __copy__(self) -> "Director":
        return self

    async def answer(self, entry: Any, options: list[str]) -> types.QuestionResponse:
        raise NotImplementedError


class AbsentDirector(Director):
    """Nobody is attached. Every question is recorded, and skipped rather than waited on."""

    async def answer(self, entry: Any, options: list[str]) -> types.QuestionResponse:
        print(f"\n  [no director attached] agent asked: {getattr(entry, 'question', '')}")
        return types.QuestionResponse(
            skipped=True,
            freeform_response=(
                "No director is attached to this run, so this question cannot be answered. "
                "Choose the direction you judge best, say in a Stage.note which one you chose and "
                "why, and carry on."
            ),
        )


class ConsoleDirector(Director):
    """A person at the terminal. Numbered options, or free text, or blank to skip."""

    attended = True

    async def answer(self, entry: Any, options: list[str]) -> types.QuestionResponse:
        question = getattr(entry, "question", "")
        multi = bool(getattr(entry, "is_multi_select", False))

        print(f"\n  ── the agent is asking ─────────────────────────────")
        print(f"  {question}")
        for i, option in enumerate(options, start=1):
            print(f"    {i}. {option}")
        if options:
            print(f"  Reply with a number{'s, comma-separated' if multi else ''}, or type your own answer. "
                  f"Blank skips.")
        else:
            print("  Type your answer. Blank skips.")

        # stdin is read off the event loop so the agent's own work is not blocked by a person
        # thinking, and a non-interactive stdin fails as a skip rather than as an EOF crash.
        try:
            reply = (await asyncio.to_thread(input, "  > ")).strip()
        except (EOFError, KeyboardInterrupt):
            print("  (no input available — skipping)")
            return types.QuestionResponse(skipped=True)

        if not reply:
            return types.QuestionResponse(skipped=True)

        if options and (chosen := self._as_choices(reply, entry, options, multi)):
            return types.QuestionResponse(selected_option_ids=chosen)

        return types.QuestionResponse(freeform_response=reply)

    @staticmethod
    def _as_choices(reply: str, entry: Any, options: list[str], multi: bool) -> list[str] | None:
        """Reads a reply as option numbers, or returns None so it is taken as free text.

        Anything that is not entirely numbers is free text — a director who types "2 is closer, but
        warmer" means the sentence, not option 2.
        """
        parts = [p.strip() for p in reply.split(",") if p.strip()]
        if not parts or not all(p.isdigit() for p in parts):
            return None
        if not multi and len(parts) > 1:
            return None

        entries = list(getattr(entry, "options", None) or [])
        picked = []
        for part in parts:
            index = int(part) - 1
            if not 0 <= index < len(entries):
                return None
            picked.append(getattr(entries[index], "id", "") or options[index])

        return picked


class WebDirector(Director):
    """A person in a browser. The question goes out on the broker; the answer comes back by id.

    This is the third director the module docstring anticipated, and it replaces `ConsoleDirector`
    without touching anything else — the hook, the event vocabulary and `director.jsonl` are already
    the contract.

    The question needs an id, because unlike a terminal there may be several watchers and the reply
    arrives on a different request from the one that asked. That id is a **transport** detail and is
    deliberately kept out of the durable record: `director.jsonl` keeps the question and the answer
    in the words a person would read, and the id lives only in the broker's stream. A watcher that
    refreshes still finds it, because the broker replays.

    Every path ends the question. A visitor who closes the tab, or thinks for longer than the run can
    wait, becomes a skip with an explanation the agent can act on — the same shape `AbsentDirector`
    uses — rather than a run wedged on a reply that is never coming.
    """

    attended = True

    # region Constructors
    def __init__(self, log: EventLog, publish: Callable[[dict[str, Any]], None], *,
                 timeout: float = 600.0) -> None:
        super().__init__(log)
        self.publish = publish
        self.timeout = timeout
        self._pending: dict[str, asyncio.Future] = {}
        self._next = 0
    # endregion

    # region Properties
    @property
    def pending(self) -> list[str]:
        """Ids currently waiting on an answer. Normally one; never assumed to be."""
        return [qid for qid, future in self._pending.items() if not future.done()]
    # endregion

    # region Methods
    async def answer(self, entry: Any, options: list[str]) -> types.QuestionResponse:
        self._next += 1
        question_id = f"q{self._next}"

        future: asyncio.Future = asyncio.get_running_loop().create_future()
        self._pending[question_id] = future

        self.publish({
            "src": "director",
            "type": "question.open",
            "id": question_id,
            "text": getattr(entry, "question", ""),
            "options": options,
            "multi": bool(getattr(entry, "is_multi_select", False)),
            "timeout": self.timeout,
        })

        try:
            reply = await asyncio.wait_for(future, timeout=self.timeout)
        except (asyncio.TimeoutError, TimeoutError):
            self._close(question_id, "timeout")
            return types.QuestionResponse(
                skipped=True,
                freeform_response=(
                    f"No answer arrived within {self.timeout:.0f}s, so this question went "
                    f"unanswered. Choose the direction you judge best, say in a Stage.note which one "
                    f"you chose and why, and carry on."
                ),
            )
        except asyncio.CancelledError:
            self._close(question_id, "cancelled")
            raise
        finally:
            self._pending.pop(question_id, None)

        self._close(question_id, "answered")

        if reply.skipped:
            return types.QuestionResponse(skipped=True)

        # Ids the agent offered, matched by identity rather than by position, so a client that sends
        # back an option it invented is treated as free text instead of silently choosing the wrong
        # one.
        if reply.selected:
            entries = list(getattr(entry, "options", None) or [])
            valid = {getattr(o, "id", "") or "" for o in entries} | set(options)
            chosen = [c for c in reply.selected if c in valid]
            if chosen:
                return types.QuestionResponse(selected_option_ids=chosen)

        if reply.text:
            return types.QuestionResponse(freeform_response=reply.text)

        return types.QuestionResponse(skipped=True)

    def reply(self, question_id: str, *, text: str = "", selected: list[str] | None = None,
              skipped: bool = False) -> bool:
        """Answers an open question. False if there is no such question, or it is already settled.

        The caller turns that into its own refusal — the web layer has the vocabulary for saying
        "gone" that this does not.
        """
        future = self._pending.get(question_id)
        if future is None or future.done():
            return False

        future.set_result(Reply(text=text.strip(), selected=list(selected or []), skipped=skipped))
        return True

    def abandon(self) -> int:
        """Skips every open question. What a run being torn down does before it stops."""
        settled = 0
        for question_id in list(self._pending):
            if self.reply(question_id, skipped=True):
                settled += 1
        return settled
    # endregion

    # region Methods (private)
    def _close(self, question_id: str, reason: str) -> None:
        self.publish({"src": "director", "type": "question.closed", "id": question_id, "reason": reason})
    # endregion


def for_run(log: EventLog, *, interactive: bool) -> Director:
    """Picks the director for this run: the terminal if a person can reach it, nobody otherwise."""
    if interactive and sys.stdin is not None and sys.stdin.isatty():
        return ConsoleDirector(log)
    return AbsentDirector(log)
