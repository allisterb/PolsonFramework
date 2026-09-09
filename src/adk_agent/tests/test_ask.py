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
from unittest import mock

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
    """That waiting is not billed as working — and that the giving back is bounded."""

    def setUp(self) -> None:
        import studio

        studio._credited.pop("inv1", None)

    def tearDown(self) -> None:
        import studio

        studio._invocation_started.pop("inv1", None)
        studio._role_clock.pop(("inv1", "polson"), None)
        studio._credited.pop("inv1", None)

    def test_the_wait_is_given_back_to_both_clocks(self):
        """**Otherwise `ask_director` is a tool whose cost the agent cannot control**, which teaches
        it to guess instead of asking — the opposite of why the tool exists.
        """
        import studio

        studio._invocation_started["inv1"] = 1000.0
        studio._role_clock[("inv1", "polson")] = studio._RoleClock(1000.0, set())

        self.assertTrue(studio.credit_wait("inv1", "polson", 90.0))
        self.assertEqual(studio._invocation_started["inv1"], 1090.0)
        self.assertEqual(studio._role_clock[("inv1", "polson")].started, 1090.0)

    def test_nothing_to_credit_is_not_an_error(self):
        import studio

        self.assertFalse(studio.credit_wait(None, "polson", 90.0))
        self.assertFalse(studio.credit_wait("nosuch", "polson", 90.0))
        self.assertFalse(studio.credit_wait("inv1", "polson", 0.0))

    def test_the_credit_runs_out(self):
        """**Cloud Run kills a request at 3600s whatever the breaker thinks.**

        Crediting moves the breaker's anchor and not the wall, so an invocation that waits over and
        over drifts away from real time. `drawing` is where that bites — it asks at every turn by
        design, and its 45-minute deadline plus the 15-minute grace already *is* the 3600s ceiling.
        Uncapped, a run nobody answers is killed mid-flight by the platform rather than halted
        cleanly by the breaker.
        """
        import studio

        studio._invocation_started["inv1"] = 1000.0
        for _ in range(int(studio.MAX_CREDIT_SECONDS // 100)):
            self.assertTrue(studio.credit_wait("inv1", "polson", 100.0))

        self.assertFalse(studio.credit_wait("inv1", "polson", 100.0), "past the ceiling, it is not")
        self.assertEqual(studio._invocation_started["inv1"], 1000.0 + studio.MAX_CREDIT_SECONDS)

    def test_a_wait_straddling_the_ceiling_is_clamped_not_refused(self):
        """Otherwise one long question costs more than two short ones summing to the same."""
        import studio

        studio._invocation_started["inv1"] = 1000.0
        studio._credited["inv1"] = studio.MAX_CREDIT_SECONDS - 20.0

        self.assertTrue(studio.credit_wait("inv1", "polson", 90.0))
        self.assertEqual(studio._invocation_started["inv1"], 1020.0, "20 credited, 70 charged")
        self.assertEqual(studio._credited["inv1"], studio.MAX_CREDIT_SECONDS)

    def test_an_uncredited_wait_does_not_burn_the_allowance(self):
        """A tool called before the first model call has no clock to move.

        Counting it anyway would spend the ceiling on nothing, and the run would then be charged for
        waits it was never given back.
        """
        import studio

        self.assertFalse(studio.credit_wait("inv1", "polson", 120.0))
        self.assertEqual(studio._credited.get("inv1", 0.0), 0.0)

    def test_the_counter_is_pruned_with_the_rest(self):
        """There is no invocation-end hook, so the size guard is the only thing bounding these."""
        import studio

        studio._invocation_started["inv1"] = 1000.0
        studio.credit_wait("inv1", "polson", 60.0)
        self.assertIn("inv1", studio._credited)

        # Far enough past the TTL that the entry is stale, and over the 64-entry size guard. The
        # margin is generous on purpose: crediting has just moved this invocation's own anchor
        # forward, so measuring from where it started is off by exactly the credit.
        filler = {f"f{i}": 1000.0 for i in range(70)}
        studio._invocation_started.update(filler)
        try:
            studio._prune_role_clocks(1000.0 + studio._ROLE_CLOCK_TTL + 3600)
            self.assertNotIn("inv1", studio._credited)
        finally:
            for key in filler:
                studio._invocation_started.pop(key, None)


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


class PeekArtifactNameTests(unittest.IsolatedAsyncioTestCase):
    """One artifact name per file type, behind a flag, because names are what cost the cache.

    **The measurement this exists for.** ADK's `LoadArtifactsTool` writes the artifact *name list*
    into the instructions, which is the head of the prompt-cache prefix — so a new name re-bills the
    whole conversation and a new version does not. On `overshoulder2`, 25 of 95 turns missed cache
    and cost 1,501,528 tokens: 62% of the entire run.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-peek-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        (self.root / "artifacts").mkdir()
        (self.root / "artifacts" / "001_ground.webp").write_bytes(b"RIFF....WEBPVP8 pretend")
        (self.root / "artifacts" / "002_darks.webp").write_bytes(b"RIFF....WEBPVP8 pretend two")

        import studio
        self.studio = studio
        self.saved = []

        class Ctx:
            invocation_id = "inv-peek"

            async def save_artifact(inner, filename, part):          # noqa: N805
                self.saved.append(filename)
                return self.saved.count(filename) - 1

        self.ctx = Ctx()
        studio.transcript._artifacts.clear()
        self.addCleanup(studio.transcript._artifacts.clear)

    async def peek(self, path):
        return await self.studio._make_peek(self.root)(path, self.ctx)

    async def test_off_by_default_each_render_keeps_its_own_name(self):
        """The behaviour every run so far has had. Default-off is the cautious choice: `peek` is the
        whole perception path, and if it breaks every run breaks."""
        with mock.patch.object(self.studio, "PEEK_STABLE_NAME", False):
            a = await self.peek("artifacts/001_ground.webp")
            b = await self.peek("artifacts/002_darks.webp")

        self.assertEqual(["artifacts/001_ground.webp", "artifacts/002_darks.webp"], self.saved)
        self.assertTrue(a["ok"] and b["ok"])

    async def test_on_every_render_becomes_a_version_of_one_name(self):
        with mock.patch.object(self.studio, "PEEK_STABLE_NAME", True):
            a = await self.peek("artifacts/001_ground.webp")
            b = await self.peek("artifacts/002_darks.webp")

        self.assertEqual(["peek.webp", "peek.webp"], self.saved, "one name, two versions")
        self.assertEqual(0, a["version"])
        self.assertEqual(1, b["version"])

    async def test_the_mapping_is_recorded_so_a_version_still_says_what_it_holds(self):
        """Anonymous versions would be a regression from the self-describing names."""
        with mock.patch.object(self.studio, "PEEK_STABLE_NAME", True):
            await self.peek("artifacts/002_darks.webp")

        queued = self.studio.transcript._artifacts["inv-peek"]
        self.assertEqual([{"artifact": "peek.webp", "version": 0,
                           "source": "artifacts/002_darks.webp"}], queued)

    async def test_nothing_is_recorded_when_the_name_is_the_path(self):
        """With the flag off the name already says what it holds, so the event would be noise."""
        with mock.patch.object(self.studio, "PEEK_STABLE_NAME", False):
            await self.peek("artifacts/001_ground.webp")

        self.assertEqual([], self.studio.transcript._artifacts.get("inv-peek", []))

    async def test_the_real_path_still_comes_back_to_the_agent(self):
        """It has to be able to say which render it is looking at, whatever the store called it."""
        with mock.patch.object(self.studio, "PEEK_STABLE_NAME", True):
            answer = await self.peek("artifacts/001_ground.webp")

        self.assertEqual("artifacts/001_ground.webp", answer["source"])

    async def test_peeking_an_older_render_makes_it_the_newest_version(self):
        """**The capability collapsing names would otherwise cost.**

        `load_artifacts` fetches the latest version of a name, so with one name an earlier pass could
        not be brought back into view — except that `peek` re-reads from disk, so asking for the old
        file saves it again as the newest version. Nothing is lost.
        """
        with mock.patch.object(self.studio, "PEEK_STABLE_NAME", True):
            await self.peek("artifacts/001_ground.webp")
            await self.peek("artifacts/002_darks.webp")
            back = await self.peek("artifacts/001_ground.webp")

        self.assertEqual(2, back["version"], "the old render is now the newest version")
        self.assertEqual("artifacts/001_ground.webp", back["source"])


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
