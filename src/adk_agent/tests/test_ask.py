"""The agent's question to the director, and the wait for an answer.

The counterpart of `test_interject`, and the failure modes are different ones. An interjection that
goes nowhere is invisible; a question that goes nowhere **stops the run** until it times out, so the
tests that matter most here are the ones covering what happens when nobody answers.

    python-adk\\Scripts\\python.exe -m unittest discover -p "test_*.py"
"""

from __future__ import annotations

import asyncio
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

# `src/` first so `orchestrator.events` resolves, then `adk_agent/` on top of it — that order matters,
# because **two packages in this tree are called `studio`** and the ADK one has to win. See the same
# note in `main.py`.
sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import ask


class FakeContext:
    """What ADK hands a tool. Only the two fields `_credit` reads."""

    def __init__(self, invocation_id="inv1", agent_name="polson"):
        self.invocation_id = invocation_id
        self.agent_name = agent_name


class AskChannelTests(unittest.IsolatedAsyncioTestCase):
    """Opening a question, and the three ways one ends."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-ask-")) / "acme"
        (self.root / "events").mkdir(parents=True)
        ask.abandon("acme")
        # Question ids are monotonic per project for the life of the process, which is right in
        # production — `q3` means one thing to the page, the record and the agent — and leaks
        # between tests. Reset so an assertion on `q1` means what it says.
        ask._counter.pop("acme", None)
        self.tool = ask.make_tool(self.root)

    def tearDown(self) -> None:
        shutil.rmtree(self.root.parent, ignore_errors=True)

    def recorded(self, kind: str) -> list[dict]:
        path = self.root / "events" / "director.jsonl"
        if not path.exists():
            return []
        rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line]
        return [r for r in rows if r.get("type") == kind or r.get("kind") == kind]

    async def answer_when_asked(self, **reply):
        """Waits for the question to open, then settles it. What the click does."""
        for _ in range(200):
            if open_now := ask.pending("acme"):
                return ask.reply("acme", open_now[0], **reply)
            await asyncio.sleep(0.01)
        raise AssertionError("no question was ever opened")

    async def test_a_clicked_option_comes_back_as_the_answer(self):
        asking = asyncio.create_task(
            self.tool("Warm or cool?", ["Warm and editorial", "Cool and technical"], FakeContext()))
        await self.answer_when_asked(selected=["Warm and editorial"])
        result = await asking

        self.assertTrue(result["answered"])
        self.assertEqual(result["answer"], "Warm and editorial")

    async def test_typed_text_comes_back_too(self):
        asking = asyncio.create_task(self.tool("Which subject?", [], FakeContext()))
        await self.answer_when_asked(text="the wing, not the bird")
        result = await asking

        self.assertTrue(result["answered"])
        self.assertEqual(result["answer"], "the wing, not the bird")

    async def test_a_click_with_a_qualification_keeps_both(self):
        """The page submits its text field alongside the option button, so both can arrive.

        Dropping either would lose the half the director thought was the important one — and which
        half that is cannot be known from here.
        """
        asking = asyncio.create_task(self.tool("Warm or cool?", ["Warm"], FakeContext()))
        await self.answer_when_asked(selected=["Warm"], text="but keep the ground pale")
        result = await asking

        self.assertIn("Warm", result["answer"])
        self.assertIn("keep the ground pale", result["answer"])

    async def test_let_it_decide_is_not_an_answer_and_says_so(self):
        """The skip button. The agent must not read it as assent to whatever it listed first."""
        asking = asyncio.create_task(self.tool("Warm or cool?", ["Warm"], FakeContext()))
        await self.answer_when_asked(skipped=True)
        result = await asking

        self.assertFalse(result["answered"])
        self.assertIn("decide", result["answer"])
        self.assertIn("Stage.note", result["answer"])

    async def test_nobody_there_ends_the_wait_rather_than_the_run(self):
        """**The failure that matters.** A question nobody answers must not hang the run.

        A judge who opens the page and wanders off is the expected case, not the exceptional one.
        """
        ask.TIMEOUT, original = 0.05, ask.TIMEOUT
        try:
            result = await self.tool("Warm or cool?", ["Warm"], FakeContext())
        finally:
            ask.TIMEOUT = original

        self.assertFalse(result["answered"])
        self.assertIn("no answer arrived", result["answer"].lower())
        self.assertIn("carry on", result["answer"].lower())

    async def test_the_question_reaches_the_record_before_the_wait(self):
        """**The record is the transport.** The page tails `director.jsonl` and draws the card from
        this line, so a question written after the wait is one nobody could ever answer.
        """
        asking = asyncio.create_task(self.tool("Warm or cool?", ["Warm", "Cool"], FakeContext()))
        await self.answer_when_asked(selected=["Warm"])
        await asking

        opened = self.recorded("question.open")
        self.assertEqual(len(opened), 1)
        self.assertEqual(opened[0]["text"], "Warm or cool?")
        self.assertEqual(opened[0]["options"], ["Warm", "Cool"])
        self.assertEqual(self.recorded("question.closed")[0]["reason"], "answered")

    async def test_a_timeout_closes_the_card_too(self):
        """Or the page keeps offering buttons for a question the agent has stopped waiting on."""
        ask.TIMEOUT, original = 0.05, ask.TIMEOUT
        try:
            await self.tool("Warm or cool?", [], FakeContext())
        finally:
            ask.TIMEOUT = original

        self.assertEqual(self.recorded("question.closed")[0]["reason"], "timeout")

    async def test_an_empty_question_is_not_put_to_anybody(self):
        result = await self.tool("   ", ["Warm"], FakeContext())

        self.assertFalse(result["answered"])
        self.assertEqual(ask.pending("acme"), [])
        self.assertEqual(self.recorded("question.open"), [])

    async def test_options_are_clipped_rather_than_refused(self):
        """A refusal costs the agent a turn to discover why; a clip costs it nothing."""
        asking = asyncio.create_task(
            self.tool("x" * 900, [f"option {i}" for i in range(20)], FakeContext()))
        await self.answer_when_asked(text="fine")
        await asking

        opened = self.recorded("question.open")[0]
        self.assertEqual(len(opened["text"]), ask.MAX_QUESTION)
        self.assertEqual(len(opened["options"]), ask.MAX_OPTIONS)

    async def test_answering_twice_is_refused(self):
        """The second window's click. The route turns this into a 409, not a 404."""
        asking = asyncio.create_task(self.tool("Warm or cool?", ["Warm"], FakeContext()))
        await self.answer_when_asked(selected=["Warm"])
        await asking

        self.assertFalse(ask.reply("acme", "q1", selected=["Warm"]))

    async def test_an_unknown_question_is_refused(self):
        self.assertFalse(ask.reply("acme", "q99", text="hello"))

    async def test_runs_do_not_hear_each_other(self):
        """Two projects can be going at once; an answer settles one of them."""
        asking = asyncio.create_task(self.tool("Warm or cool?", ["Warm"], FakeContext()))
        for _ in range(200):
            if ask.pending("acme"):
                break
            await asyncio.sleep(0.01)

        self.assertFalse(ask.reply("other", "q1", text="not yours"))
        self.assertEqual(ask.pending("acme"), ["q1"])

        await self.answer_when_asked(text="mine")
        await asking

    async def test_too_many_open_questions_is_refused_rather_than_queued(self):
        """A loop that asks without waiting must not fill a director's page with cards."""
        asking = [asyncio.create_task(self.tool(f"q{i}?", [], FakeContext()))
                  for i in range(ask.MAX_OPEN)]
        for _ in range(300):
            if len(ask.pending("acme")) >= ask.MAX_OPEN:
                break
            await asyncio.sleep(0.01)

        refused = await self.tool("one too many?", [], FakeContext())
        self.assertFalse(refused["answered"])
        self.assertIn("limit", refused["answer"])

        self.assertEqual(ask.abandon("acme"), ask.MAX_OPEN)
        await asyncio.gather(*asking)

    async def test_a_new_run_does_not_inherit_a_dead_run_s_questions(self):
        """Building the tool claims the project id, and a stale future under it is dropped.

        The way to leave one behind is to be killed mid-wait, which is what happened twice in one
        day on Cloud Run and is the whole reason `mirror` exists.
        """
        asking = asyncio.create_task(self.tool("Warm or cool?", [], FakeContext()))
        for _ in range(200):
            if ask.pending("acme"):
                break
            await asyncio.sleep(0.01)

        ask.make_tool(self.root)                    # a second run on the same project

        # Awaited first, because settling is deferred onto the future's own loop — so the waiting
        # task resuming is the proof the sweep landed, and `pending` is only trustworthy after it.
        self.assertFalse((await asking)["answered"])
        self.assertEqual(ask.pending("acme"), [])


class CreditTests(unittest.TestCase):
    """That waiting is not billed as working."""

    def test_the_wait_is_given_back_to_both_clocks(self):
        """**Otherwise `ask_director` is a tool whose cost the agent cannot control**, which teaches
        it to guess instead of asking — the opposite of why the tool exists.
        """
        import studio

        studio._invocation_started["inv1"] = 1000.0
        studio._role_clock[("inv1", "polson")] = studio._RoleClock(1000.0, set())
        try:
            self.assertTrue(studio.credit_wait("inv1", "polson", 90.0))
            self.assertEqual(studio._invocation_started["inv1"], 1090.0)
            self.assertEqual(studio._role_clock[("inv1", "polson")].started, 1090.0)
        finally:
            studio._invocation_started.pop("inv1", None)
            studio._role_clock.pop(("inv1", "polson"), None)

    def test_nothing_to_credit_is_not_an_error(self):
        import studio

        self.assertFalse(studio.credit_wait(None, "polson", 90.0))
        self.assertFalse(studio.credit_wait("nosuch", "polson", 90.0))
        self.assertFalse(studio.credit_wait("inv1", "polson", 0.0))


class AskWiringTests(unittest.TestCase):
    """That the tool reaches the agent, and only the one that should have it."""

    def test_only_the_root_agent_may_ask(self):
        """A question suspends whoever calls it, so four roles with this tool is four cards.

        The Facilitator holds the brief and is the one that knows what is genuinely unsettled; a
        role that wants a ruling transfers to it.
        """
        import json as _json

        root = Path(tempfile.mkdtemp(prefix="polson-askwire-")) / "acme"
        try:
            (root / "events").mkdir(parents=True)
            (root / "project.json").write_text(
                _json.dumps({"id": "acme", "workflow": "logo"}), encoding="utf-8")
            (root / "GEMINI.md").write_text("# instructions", encoding="utf-8")

            import studio
            agent = studio.build(root)
            names = [getattr(t, "name", "") for t in agent.tools]
        finally:
            shutil.rmtree(root.parent, ignore_errors=True)

        self.assertIn("ask_director", names)
        self.assertIn("ask_director", agent.instruction,
                      "a tool the instructions never mention is one the agent will not reach for")
        for sub in getattr(agent, "sub_agents", None) or []:
            self.assertNotIn("ask_director", [getattr(t, "name", "") for t in sub.tools],
                             f"{sub.name} should transfer to the facilitator rather than ask")


class AddendumTests(unittest.TestCase):
    """What the agent is told about asking, and the one way that text can kill a run."""

    def test_no_brace_survives_into_the_instruction(self):
        """**ADK resolves `{name}` in an instruction against session state.**

        So a stray brace is not a literal that renders oddly — it is a missing-key error at startup,
        before the agent has done anything. This project has had that failure once already, from a
        workflow template carrying `{ bareIdentifier }`. The timeout is substituted rather than
        interpolated for exactly this reason.
        """
        import re

        import studio

        self.assertEqual(re.findall(r"\{[^}]*\}", studio.RUNTIME_ADDENDUM), [])
        self.assertIn(f"{ask.TIMEOUT:.0f} seconds", studio.RUNTIME_ADDENDUM)

    def test_the_agent_is_told_the_decide_instruction_no_longer_stands_alone(self):
        """The workflows say "where the brief is silent, decide", written with nobody to ask.

        Leaving that uncorrected is doc/code drift of the kind that makes a capability real in the
        code and absent in practice: the agent has the tool and a standing instruction not to need
        it.
        """
        import studio

        self.assertIn("decide", studio.RUNTIME_ADDENDUM)
        self.assertIn("not charged against your deadline", studio.RUNTIME_ADDENDUM)


if __name__ == "__main__":
    unittest.main()
