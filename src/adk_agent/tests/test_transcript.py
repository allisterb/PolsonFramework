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


if __name__ == "__main__":
    unittest.main()
