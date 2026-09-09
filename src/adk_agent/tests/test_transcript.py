"""Transcribing an ADK conversation into the project's record.

The event this file exists for is `run.begin`. `run.start` is the MCP *server* starting, and under
ADK the server is spawned once for the whole app process — so a project worked on twice through one
running app has two runs and one `run.start`, and `studio.observe` cutting there replays the earlier
run as though it were happening now. This is the runtime saying where its own runs begin.

Nothing here starts an agent or a model. The callbacks are driven directly with stand-ins for what
ADK passes them, which is the only way to assert ordering: the defect being guarded against is not
that the event is missing but that it arrives one line too late.
"""

from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import transcript as transcript_mod


def run(coro):
    """Drives one callback. The plugin does no awaiting of its own, so this needs no loop policy."""
    import asyncio
    return asyncio.run(coro)


def events(project: Path, name: str) -> list[dict]:
    path = project / "events" / name
    if not path.exists():
        return []
    return [json.loads(ln) for ln in path.read_text(encoding="utf-8").splitlines() if ln.strip()]


def context(invocation: str = "inv-1", agent: str = "facilitator"):
    """What ADK hands both callbacks."""
    return SimpleNamespace(invocation_id=invocation, agent=SimpleNamespace(name=agent))


def message(text: str):
    return SimpleNamespace(parts=[SimpleNamespace(text=text, thought=False, function_call=None)])


class RunBeginTests(unittest.TestCase):
    """Where an ADK run begins, and that the record says so before it says anything else."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-transcript-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.plugin = transcript_mod.make_plugin(self.root)
        if self.plugin is None:
            self.skipTest("google.adk or orchestrator.events is not importable here")

    def test_before_run_writes_one_begin_marker(self):
        run(self.plugin.before_run_callback(invocation_context=context()))

        begins = [e for e in events(self.root, "agent.jsonl") if e["type"] == "run.begin"]
        self.assertEqual(1, len(begins))
        self.assertEqual("inv-1", begins[0]["invocation"])
        self.assertEqual("facilitator", begins[0]["agent"])

    def test_the_marker_is_not_written_twice_for_one_invocation(self):
        """Both callbacks reach for it, and only one of them may win.

        A second marker would not merely be noise: `session_since` takes the *last* one, so a
        duplicate written after the director's message would cut the message back out of the run.
        """
        ctx = context()
        run(self.plugin.on_user_message_callback(invocation_context=ctx,
                                                 user_message=message("draw a poster")))
        run(self.plugin.before_run_callback(invocation_context=ctx))

        self.assertEqual(1, len([e for e in events(self.root, "agent.jsonl")
                                 if e["type"] == "run.begin"]))

    def test_the_user_message_callback_writes_the_marker_itself(self):
        """The ordering the whole change turns on, asserted where it cannot pass by luck.

        `runners.py` calls `on_user_message_callback` before `before_run_callback`, whatever the
        latter's docstring claims about being first in the lifecycle. A marker written only in
        `before_run_callback` lands *after* the message, and a cut at it drops the one event saying
        what the run was asked to do.

        Comparing the two timestamps is the obvious test and a weak one: both writes usually fall
        in the same millisecond, and the cut is inclusive of equals, so it would pass with the
        defect present and fail only on an unlucky machine. This asserts the design instead —
        whichever callback arrives first is the one that writes it.
        """
        run(self.plugin.on_user_message_callback(invocation_context=context(),
                                                 user_message=message("draw a poster")))

        begins = [e for e in events(self.root, "agent.jsonl") if e["type"] == "run.begin"]
        said = [e for e in events(self.root, "director.jsonl") if e["type"] == "message"]

        self.assertEqual(1, len(begins), "the marker must not wait for before_run_callback")
        self.assertLessEqual(begins[0]["ts"], said[0]["ts"])
        self.assertEqual("draw a poster", said[0]["text"])

    def test_a_second_invocation_begins_again(self):
        run(self.plugin.before_run_callback(invocation_context=context("inv-1")))
        run(self.plugin.before_run_callback(invocation_context=context("inv-2")))

        begins = [e for e in events(self.root, "agent.jsonl") if e["type"] == "run.begin"]
        self.assertEqual(["inv-1", "inv-2"], [b["invocation"] for b in begins])

    def test_a_callback_never_fails_the_run(self):
        """A recorder that can stop the work is worse than no recorder."""
        self.assertIsNone(run(self.plugin.before_run_callback(invocation_context=object())))
        self.assertIsNone(run(self.plugin.on_user_message_callback(
            invocation_context=object(), user_message=object())))




class BudgetWarningTests(unittest.TestCase):
    """That a director is told the run is running out, before it stops.

    **The gap this closes.** The breaker's 75% and 90% warnings went to `_TURN_LOG` — the container
    log, reachable with `gcloud logging read` and nowhere else — and into the agent's own `contents`.
    Neither is the page. On `nightstreet2` the two warnings landed twelve and four minutes before the
    halt, and the one person who could have said "wrap up now" saw a trace that mentioned neither,
    then a run that stopped.

    The run page has carried a `budget` label since Milestone 6 and nothing on this runtime ever
    produced one.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-transcript-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        transcript_mod._budget.clear()
        self.addCleanup(transcript_mod._budget.clear)
        self.plugin = transcript_mod.make_plugin(self.root)
        if self.plugin is None:
            self.skipTest("google.adk or orchestrator.events is not importable here")

    def turn(self):
        """One model turn that said something, which is when usage and budget are written."""
        run(self.plugin.after_model_callback(
            callback_context=context(), llm_response=SimpleNamespace(
                content=message("drawing"), usage_metadata=None, model_version="gemini-3.7-flash",
                partial=False, turn_complete=True)))

    def written(self) -> list[dict]:
        return [e for e in events(self.root, "agent.jsonl") if e["type"] == "budget"]

    def test_a_crossed_threshold_reaches_the_record(self):
        transcript_mod.note_budget("inv-1", limit="tokens", share=0.9008,
                                   spent=1_801_505, cap=2_000_000)
        self.turn()

        self.assertEqual(1, len(self.written()))
        note = self.written()[0]
        self.assertEqual("tokens", note["limit"])
        self.assertEqual(0.901, note["share"])
        self.assertEqual(1_801_505, note["spent"])
        self.assertEqual(2_000_000, note["cap"])

    def test_the_time_budget_warns_the_same_way(self):
        transcript_mod.note_budget("inv-1", limit="time", share=0.75, spent=26.3, cap=35)
        self.turn()

        self.assertEqual("time", self.written()[0]["limit"])

    def test_a_warning_is_written_once_rather_than_on_every_later_turn(self):
        """It is a threshold crossing, not a running state — repeating it is noise in a trace whose
        whole value is that a reader can scan it."""
        transcript_mod.note_budget("inv-1", limit="tokens", share=0.75, spent=1_500_000, cap=2_000_000)
        self.turn()
        self.turn()

        self.assertEqual(1, len(self.written()))

    def test_both_thresholds_survive_when_one_turn_crosses_both(self):
        transcript_mod.note_budget("inv-1", limit="tokens", share=0.75, spent=1_500_000, cap=2_000_000)
        transcript_mod.note_budget("inv-1", limit="tokens", share=0.90, spent=1_800_000, cap=2_000_000)
        self.turn()

        self.assertEqual([0.75, 0.9], [n["share"] for n in self.written()])

    def test_a_note_for_another_invocation_is_not_taken(self):
        transcript_mod.note_budget("inv-2", limit="tokens", share=0.9, spent=1, cap=2)
        self.turn()

        self.assertEqual([], self.written())
        self.assertIn("inv-2", transcript_mod._budget)


class RunEndTests(unittest.TestCase):
    """That the record says a run is over, and which of the three ways it ended.

    `run.begin` was written and nothing ever closed it, so a finished run, one the circuit breaker
    halted, and one wedged mid-turn were the same picture to a reader: a log that stops. That is the
    state a director is looking at when they ask whether a run is still going.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-transcript-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        transcript_mod._halted.clear()
        self.addCleanup(transcript_mod._halted.clear)
        self.plugin = transcript_mod.make_plugin(self.root)
        if self.plugin is None:
            self.skipTest("google.adk or orchestrator.events is not importable here")

    def ends(self) -> list[dict]:
        return [e for e in events(self.root, "agent.jsonl") if e["type"] == "run.end"]

    def test_a_run_that_finishes_normally_says_completed(self):
        run(self.plugin.after_run_callback(invocation_context=context()))

        self.assertEqual(1, len(self.ends()))
        self.assertEqual("completed", self.ends()[0]["reason"])
        self.assertEqual("inv-1", self.ends()[0]["invocation"])
        self.assertEqual("facilitator", self.ends()[0]["agent"])

    def test_a_halted_run_says_halted_and_carries_the_breakers_numbers(self):
        """The whole point: a halt reaches ADK's success path, so without the note it reads as a
        normal finish — the one confusion this event exists to prevent."""
        transcript_mod.note_halt("inv-1", limit="tokens", used=10_500, cap=10_000)
        run(self.plugin.after_run_callback(invocation_context=context()))

        end = self.ends()[0]
        self.assertEqual("halted", end["reason"])
        self.assertEqual("tokens", end["limit"])
        self.assertEqual(10_500, end["used"])
        self.assertEqual(10_000, end["cap"])

    def test_the_time_breaker_is_distinguishable_from_the_token_one(self):
        transcript_mod.note_halt("inv-1", limit="time", used=7200.0, cap=6300.0)
        run(self.plugin.after_run_callback(invocation_context=context()))

        self.assertEqual("time", self.ends()[0]["limit"])

    def test_a_halt_belongs_to_one_run_and_is_not_inherited(self):
        """Spent on the ending that consumes it. A later invocation reusing the id — which happens
        across a long-lived app process — must not report a halt it never had."""
        transcript_mod.note_halt("inv-1", limit="tokens", used=10_500, cap=10_000)
        run(self.plugin.after_run_callback(invocation_context=context("inv-1")))
        run(self.plugin.after_run_callback(invocation_context=context("inv-1")))

        self.assertEqual(["halted", "completed"], [e["reason"] for e in self.ends()])

    def test_one_runs_halt_does_not_end_another(self):
        transcript_mod.note_halt("inv-1", limit="tokens", used=10_500, cap=10_000)
        run(self.plugin.after_run_callback(invocation_context=context("inv-2")))

        self.assertEqual("completed", self.ends()[0]["reason"])

    def test_a_run_that_raises_says_failed_and_names_the_error(self):
        """ADK skips after_run_callback entirely on this path, so it needs its own hook."""
        run(self.plugin.on_run_error_callback(
            invocation_context=context(), error=ValueError("model refused")))

        end = self.ends()[0]
        self.assertEqual("failed", end["reason"])
        self.assertIn("ValueError", end["error"])
        self.assertIn("model refused", end["error"])

    def test_a_failure_outranks_a_pending_halt(self):
        """Both can be true — the breaker fires, then something throws on the way out. What killed
        the run is the more useful of the two, and `reason` carries only one."""
        transcript_mod.note_halt("inv-1", limit="tokens", used=10_500, cap=10_000)
        run(self.plugin.on_run_error_callback(
            invocation_context=context(), error=RuntimeError("boom")))

        self.assertEqual("failed", self.ends()[0]["reason"])

    def test_the_record_opens_and_closes(self):
        run(self.plugin.before_run_callback(invocation_context=context()))
        run(self.plugin.after_run_callback(invocation_context=context()))

        kinds = [e["type"] for e in events(self.root, "agent.jsonl")]
        self.assertEqual(["run.begin", "run.end"], kinds)

    def test_held_reasons_are_bounded(self):
        """An entry is normally spent moments later, but a run that trips and then dies never
        collects its own, and the app process outlives every run it serves."""
        for i in range(transcript_mod.HALT_MEMORY + 20):
            transcript_mod.note_halt(f"inv-{i}", limit="tokens", used=1, cap=1)

        self.assertEqual(transcript_mod.HALT_MEMORY, len(transcript_mod._halted))
        self.assertNotIn("inv-0", transcript_mod._halted)
        self.assertIn(f"inv-{transcript_mod.HALT_MEMORY + 19}", transcript_mod._halted)

    def test_an_ending_callback_never_fails_the_run(self):
        self.assertIsNone(run(self.plugin.after_run_callback(invocation_context=object())))
        self.assertIsNone(run(self.plugin.on_run_error_callback(
            invocation_context=object(), error=ValueError("x"))))

    def test_noting_a_halt_never_raises(self):
        transcript_mod.note_halt("", limit="tokens", used=1, cap=1)
        self.assertEqual(0, len(transcript_mod._halted))

if __name__ == "__main__":
    unittest.main()
