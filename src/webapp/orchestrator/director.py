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
from typing import Any

from google.antigravity import types
from google.antigravity.hooks import hooks

from .events import EventLog


class Director(hooks.OnInteractionHook):
    """Answers the agent's questions and records the exchange."""

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


def for_run(log: EventLog, *, interactive: bool) -> Director:
    """Picks the director for this run: the terminal if a person can reach it, nobody otherwise."""
    if interactive and sys.stdin is not None and sys.stdin.isatty():
        return ConsoleDirector(log)
    return AbsentDirector(log)
