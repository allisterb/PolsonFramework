"""The supervising watchdog, and the single-agent case it used to get wrong.

`CLAUDE.md` §1 says one agent is the default and several is a mode. The watchdog was written for the
mode: every message and threshold assumed a peer to hand off to. On the default path it fired one
unactionable sentence at the eighth working call and then disarmed itself for good, because the only
routes back — a handoff or an advisor call — do not exist there.

Two live runs showed both halves. `inf5` was told at call 8 to hand off, had nobody, and went on to
hand-roll a chart across thirty-two working calls with the plugin silent. `inf6` was told the same
thing on a run that finished correctly in fourteen.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import supervision
from supervision import SOLO_THRASH_CALLS, THRASH_CALLS, StudioWatchdog, _RoleActivity


class _Ctx:
    def __init__(self, agent="polson", invocation="inv-1"):
        self.agent_name = agent
        self.invocation_id = invocation


def _activity(calls=0, stalled=0, armed=True, interventions=0):
    a = _RoleActivity(started=0.0)
    a.calls, a.stalled_renders, a.armed, a.interventions = calls, stalled, armed, interventions
    return a


class SoloThresholdTests(unittest.TestCase):
    """Eight is the floor of normal for one agent, not a warning sign."""

    def setUp(self):
        self.solo = StudioWatchdog(advisor_tool=None)
        self.team = StudioWatchdog(advisor_tool="ask_facilitator")

    def test_a_lone_agent_is_not_nagged_at_the_pipeline_threshold(self):
        # Measured working calls on runs that finished correctly: 8, 9, 9, 14.
        for calls in (8, 9, 14):
            self.assertIsNone(
                self.solo._trigger(_activity(calls=calls), now=0.0, agent="polson"),
                f"{calls} working calls is normal for a single agent")

    def test_a_role_in_a_pipeline_still_is(self):
        reason = self.team._trigger(_activity(calls=THRASH_CALLS), now=0.0, agent="inker")
        self.assertIsNotNone(reason)
        self.assertIn("handing off", reason)

    def test_a_lone_agent_trips_at_the_solo_threshold(self):
        # inf5, the run this plugin exists for, reached thirty-two.
        reason = self.solo._trigger(_activity(calls=SOLO_THRASH_CALLS), now=0.0, agent="polson")
        self.assertIsNotNone(reason)

    def test_the_solo_message_names_something_it_can_do(self):
        """`inf6` was told to hand off and had nobody to hand off to."""
        reason = self.solo._trigger(_activity(calls=SOLO_THRASH_CALLS), now=0.0, agent="polson")
        self.assertNotIn("handing off", reason)
        self.assertNotIn("ask_facilitator", reason)
        self.assertIn("look", reason)

    def test_the_solo_threshold_clears_every_healthy_run_observed(self):
        for run, calls in (("doctest8", 8), ("doctest6", 9), ("doctest7", 9), ("inf6", 14)):
            self.assertLess(calls, SOLO_THRASH_CALLS, f"{run} would be nagged")
        self.assertGreaterEqual(32, SOLO_THRASH_CALLS, "inf5 must still trip it")


class ReArmTests(unittest.IsolatedAsyncioTestCase):
    """One intervention used to silence the plugin for the rest of a single-agent run."""

    def setUp(self):
        self.solo = StudioWatchdog(advisor_tool=None)

    def test_a_disarmed_watchdog_reports_nothing(self):
        disarmed = _activity(calls=SOLO_THRASH_CALLS, stalled=99, armed=False)
        self.assertIsNone(self.solo._trigger(disarmed, now=0.0, agent="polson"))

    async def test_a_render_that_moved_re_arms_and_clears_the_count(self):
        activity = _activity(calls=SOLO_THRASH_CALLS, stalled=2, armed=False, interventions=1)
        activity.last_render, activity.last_digest = "a.webp", "aaa"

        self.solo._moved = lambda *a, **k: _completed(True)
        await self.solo._record_render(activity, {"imageFilePath": "b.webp"}, tool_context=None)

        self.assertTrue(activity.armed)
        self.assertEqual(activity.calls, 0)
        self.assertEqual(activity.stalled_renders, 0)

    async def test_a_render_that_did_not_move_leaves_it_disarmed(self):
        """Re-arming on any render would let a stuck role earn its way back by re-running."""
        activity = _activity(calls=SOLO_THRASH_CALLS, stalled=1, armed=False, interventions=1)
        activity.last_render, activity.last_digest = "a.webp", "aaa"

        self.solo._moved = lambda *a, **k: _completed(False)
        await self.solo._record_render(activity, {"imageFilePath": "b.webp"}, tool_context=None)

        self.assertFalse(activity.armed)
        self.assertEqual(activity.stalled_renders, 2)

    async def test_identical_bytes_are_never_treated_as_movement(self):
        activity = _activity(armed=False)
        activity.last_render, activity.last_digest = "a.webp", "same"
        supervision._digest = lambda path: "same"

        await self.solo._record_render(activity, {"imageFilePath": "b.webp"}, tool_context=None)

        self.assertFalse(activity.armed)


async def _completed(value):
    return value


if __name__ == "__main__":
    unittest.main(verbosity=2)
