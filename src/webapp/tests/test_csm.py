"""Coding a run as interaction over time.

`docs/creative-sense-making.md` is the specification these pin. The tests that matter most are the
ones about what must *not* be coded: a probe script counted as production, an execution counted twice
because the transcript also saw it, or an unknown tool guessed at would each produce a curve that
looks authoritative and says something false.

    python -m unittest discover -s src/webapp -t src/webapp
"""

from __future__ import annotations

import unittest

from orchestrator import csm


def server(kind: str, seq: int, ts: str = "", **fields) -> dict:
    return {"ts": ts or f"2026-08-30T20:00:{seq:02d}.000Z", "seq": seq, "src": "server",
            "type": kind, **fields}


def agent(kind: str, seq: int, ts: str = "", **fields) -> dict:
    return {"ts": ts or f"2026-08-30T20:00:{seq:02d}.000Z", "seq": seq, "src": "agent",
            "type": kind, **fields}


def executed(seq: int, execution: str, *, rendered: bool = True, failed: bool = False) -> list[dict]:
    """One script execution as the server records it."""
    events = [server("script.start", seq, execution=execution, script=f"scripts/{execution}.js")]
    if rendered:
        events.append(server("render", seq + 1, execution=execution,
                             artifact=f"artifacts/{execution}.webp"))
    events.append(server("script.error" if failed else "script.ok", seq + 2, execution=execution,
                         script=f"scripts/{execution}.js"))
    return events


class CodingTests(unittest.TestCase):
    """Each recorded action, coded by its functional role."""

    def modes(self, events, **kw) -> list[str]:
        return [p.mode for p in csm.code(events, **kw).points]

    def test_a_drawing_script_is_clamped_production(self):
        self.assertEqual(self.modes(executed(1, "e1")), ["execute"])

    def test_a_script_that_drew_nothing_is_not_production(self):
        """A probe script — check the fonts, exit — is unclamped work, not a mark on the canvas.

        Counting it as execution would flatten exactly the distinction the curve exists to show.
        """
        events = executed(1, "e1", rendered=False)
        events.append(server("inspect", 4, execution="e1", probes={"capability": 3}, total=3))

        self.assertEqual(self.modes(events), ["inspect"])

    def test_a_failed_script_is_still_production(self):
        """It is production that did not land, not an absence of it — and it is the surprise term."""
        coded = csm.code(executed(1, "e1", failed=True)).points

        self.assertEqual([p.mode for p in coded], ["execute"])
        self.assertIn("failed", coded[0].detail)

    def test_looking_and_reading_back_are_partial_unclamps(self):
        events = [
            server("inspect", 1, execution="e1", probes={"measure": 3}, total=3),
            server("artifact.read", 2, execution="e1", artifact="artifacts/01.webp"),
        ]
        self.assertEqual(self.modes(events), ["inspect", "inspect"])

    def test_notes_and_stages_are_communication(self):
        events = [
            server("stage.begin", 1, stage="Blocking"),
            server("note", 2, stage="Blocking", message="the wing, not the bird"),
            server("stage.end", 3, stage="Blocking"),
        ]
        self.assertEqual(self.modes(events), ["communicate"] * 3)

    def test_the_directors_own_turn_is_a_contribution(self):
        """Under the enactive account of the novice, an interjection opens affordances rather than
        merely instructing — so it belongs on the curve, attributed to the director."""
        coded = csm.code([{"ts": "2026-08-30T20:00:01.000Z", "seq": 1, "src": "director",
                           "type": "message", "text": "make the background red"}]).points

        self.assertEqual(coded[0].mode, "communicate")
        self.assertEqual(coded[0].agent, "director")

    def test_an_unrecognised_event_is_skipped_not_guessed(self):
        """A gap is visible; a wrong code is not."""
        self.assertEqual(self.modes([server("run.start", 1), server("something.new", 2)]), [])

    def test_a_probe_tally_is_one_point_carrying_its_magnitude(self):
        """A getPixel loop runs thousands of times. Weighting by count would swamp the session."""
        coded = csm.code([
            server("inspect", 1, execution="e1", probes={"sample": 4000}, total=4000)]).points

        self.assertEqual(len(coded), 1)
        self.assertEqual(coded[0].value, 0.5)
        self.assertEqual(coded[0].weight, 4000)
        self.assertIn("sample 4000", coded[0].detail)


class EnrichmentTests(unittest.TestCase):
    """The standalone transcript supplies the column the spine cannot."""

    def test_the_spine_alone_reports_that_waiting_is_missing(self):
        curve = csm.code(executed(1, "e1"))

        self.assertFalse(curve.enriched)
        self.assertEqual(curve.missing, ("wait",))
        self.assertIn("wait", curve.summary()["missing"])

    def test_thinking_supplies_the_waiting_column(self):
        events = executed(1, "e1") + [agent("thinking", 4, text="considering the palette")]
        curve = csm.code(events, enriched=True)

        self.assertIn("wait", curve.counts)
        self.assertEqual(curve.missing, ())

    def test_an_execution_the_spine_already_owns_is_not_coded_twice(self):
        """The transcript sees ExecuteScript too. Coding both would double every execution — the one
        way an enriched curve can be worse than an unenriched one."""
        events = executed(1, "e1") + [agent("tool.call", 4, tool="ExecuteScript", args={})]
        curve = csm.code(events, enriched=True)

        self.assertEqual(curve.counts.get("execute"), 1)

    def test_host_file_reads_count_as_inspection(self):
        events = [agent("tool.call", 1, tool="view_file", args={"file_path": "brief.md"})]

        self.assertEqual(csm.code(events, enriched=True).points[0].mode, "inspect")

    def test_a_doc_search_is_gathering(self):
        events = [agent("tool.call", 1, tool="Search", args={"query": "golden ratio"})]

        self.assertEqual(csm.code(events, enriched=True).points[0].mode, "gather")

    def test_an_unknown_tool_is_left_uncoded(self):
        events = [agent("tool.call", 1, tool="some_new_tool", args={})]

        self.assertEqual(csm.code(events, enriched=True).points, [])


class CurveTests(unittest.TestCase):
    """What can be read off the coded run."""

    def curve(self) -> csm.Curve:
        events = [
            server("stage.begin", 1, stage="Blocking"),                                  # +1
            server("inspect", 2, execution="e1", probes={"measure": 2}, total=2),         # +0.5
            *executed(3, "e1"),                                                           # -1
        ]
        return csm.code(events)

    def test_the_trace_accumulates_and_is_relative_to_the_first_action(self):
        trace = self.curve().trace

        self.assertEqual([round(p["cumulative"], 2) for p in trace], [1.0, 1.5, 0.5])
        self.assertEqual(trace[0]["t"], 0)
        self.assertGreater(trace[-1]["t"], 0)

    def test_the_trace_carries_what_each_point_was(self):
        """Every point opens the script that caused it — the thing a stroke-based curve cannot do."""
        point = [p for p in self.curve().trace if p["mode"] == "execute"][0]

        self.assertEqual(point["execution"], "e1")
        self.assertIn("scripts/e1.js", point["detail"])

    def test_a_run_that_only_produces_trends_downward(self):
        curve = csm.code(executed(1, "a") + executed(4, "b") + executed(7, "c"))

        self.assertEqual(curve.summary()["net"], -3.0)
        self.assertEqual(curve.summary()["regulating"], 0)

    def test_a_run_that_only_regulates_trends_upward(self):
        curve = csm.code([server("note", i, message="thinking aloud") for i in range(1, 4)])

        self.assertEqual(curve.summary()["net"], 3.0)
        self.assertEqual(curve.summary()["executing"], 0)

    def test_points_are_ordered_by_time_across_sources(self):
        """The record is merged on read — that is the whole reason one writer owns each file."""
        events = [
            agent("thinking", 9, ts="2026-08-30T20:00:01.000Z", text="first"),
            server("note", 1, ts="2026-08-30T20:00:02.000Z", message="second"),
        ]
        coded = csm.code(events, enriched=True).points

        self.assertEqual([c.detail for c in coded], ["first", "second"])

    def test_an_unreadable_timestamp_does_not_break_the_curve(self):
        events = [server("note", 1, ts="not a timestamp", message="x"), *executed(2, "e1")]

        self.assertEqual(len(csm.code(events).points), 2)

    def test_an_empty_run_summarises_without_dividing_by_zero(self):
        summary = csm.code([]).summary()

        self.assertEqual(summary["actions"], 0)
        self.assertEqual(summary["slope"], 0.0)
        self.assertEqual(summary["durationMs"], 0)


class TimeWeightingTests(unittest.TestCase):
    """Counting actions and counting time are different measurements, and they can disagree in sign.

    On a real run they did: 37 burst-written notes occupied 8 seconds between them while 13
    inspections occupied 182, so counting actions read +34.5 and counting time read -25.9. Reporting
    only the first would have described a run as mostly deliberation when it mostly drew and looked.
    """

    def at(self, second: int, kind: str, seq: int, **fields) -> dict:
        """An event `second` seconds into the run, carrying minutes properly — "20:00:305" is not a
        timestamp, and the coder floors an unreadable one to the epoch rather than guessing."""
        stamp = "2026-08-30T20:%02d:%02d.000Z" % divmod(second, 60)
        return server(kind, seq, ts=stamp, **fields)

    def test_a_state_holds_until_the_next_action(self):
        curve = csm.code([
            self.at(0, "note", 1, message="a"),
            self.at(10, "note", 2, message="b"),
        ])

        self.assertEqual([p.hold_ms for p in curve.points], [10_000, 0])

    def test_the_two_readings_can_disagree_in_sign(self):
        """Many cheap communications against one long execution."""
        events = [self.at(i, "note", i, message="burst") for i in range(0, 5)]
        events += [
            self.at(5, "script.start", 90, execution="e1"),
            self.at(5, "render", 91, execution="e1", artifact="artifacts/a.webp"),
            self.at(5, "script.ok", 92, execution="e1", script="scripts/e1.js"),
            self.at(305, "note", 93, message="after five minutes of drawing"),
        ]
        summary = csm.code(events).summary()

        self.assertGreater(summary["net"], 0)          # five notes and one script
        self.assertLess(summary["integral"], 0)        # but the script held for five minutes

    def test_held_time_is_reported_per_mode(self):
        events = [
            self.at(0, "note", 1, message="declaring"),
            self.at(4, "inspect", 2, execution="e1", probes={"measure": 1}, total=1),
            self.at(10, "note", 3, message="done"),
        ]
        held = csm.code(events).summary()["heldMs"]

        self.assertEqual(held["communicate"], 4_000)
        self.assertEqual(held["inspect"], 6_000)

    def test_the_trace_carries_both_readings(self):
        trace = csm.code([
            self.at(0, "note", 1, message="a"),
            self.at(2, "note", 2, message="b"),
        ]).trace

        self.assertEqual([p["cumulative"] for p in trace], [1.0, 2.0])
        self.assertEqual([p["integral"] for p in trace], [2.0, 2.0])   # the last point holds for 0

    def test_the_last_point_holds_for_nothing(self):
        """There is no next action to bound it, and inventing one would invent time."""
        curve = csm.code([self.at(0, "note", 1, message="only")])

        self.assertEqual(curve.points[0].hold_ms, 0)
        self.assertEqual(curve.summary()["integral"], 0.0)


class MultiAgentTests(unittest.TestCase):
    """One curve per contributor is where participatory sense-making becomes visible."""

    def test_a_subagents_work_is_attributed_to_it(self):
        events = [
            agent("thinking", 1, text="main agent"),
            agent("tool.call", 2, tool="view_file", depth=1, trajectory="traj-designer-9f", args={}),
        ]
        curve = csm.code(events, enriched=True)

        self.assertEqual(curve.agents, ["agent", "agent:traj-des"])

    def test_each_contributor_gets_its_own_curve(self):
        events = [
            server("note", 1, message="from the engine"),
            {"ts": "2026-08-30T20:00:02.000Z", "seq": 2, "src": "director", "type": "message",
             "text": "make it red"},
        ]
        split = csm.code(events).per_agent()

        self.assertEqual(sorted(split), ["agent", "director"])
        self.assertEqual(len(split["director"].points), 1)

    def test_a_split_curve_keeps_the_parent_provenance(self):
        curve = csm.code(executed(1, "e1"), enriched=True)

        self.assertTrue(curve.per_agent()["agent"].enriched)


if __name__ == "__main__":
    unittest.main()
