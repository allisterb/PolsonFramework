"""The input-token budget: the counter, the warning ladder, and the circuit breaker.

No model calls and no clock — usage is fed in the shape ADK delivers it, so these run in
milliseconds and can be run on every change.

**Why input rather than output or dollars.** Input is what spirals: every turn resends the whole
conversation, so a run that will not converge grows its input as O(n^2) while output stays bounded
per turn. Measured on one small brief - 22 turns, conversation growing 12K -> 83K - input summed to
1.2M against 11K of output. A cap on output would not have noticed.

Raw prompt tokens deliberately, even though cached input bills at about a tenth: this defends
against runaway, not cost, and a loop resending an identical prefix caches *well* - so a
cache-weighted count would discount it exactly when it is most out of control.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import studio
import transcript


class _Ctx:
    """The parts of a callback context these callbacks touch."""

    def __init__(self, invocation: str = "inv-1", agent: str = "polson"):
        self.invocation_id = invocation
        self.agent_name = agent


class _Request:
    """An LlmRequest stand-in: only `contents` is read or appended to."""

    def __init__(self):
        self.contents = []


def _usage(prompt: int, cached: int = 0, out: int = 10, thoughts: int = 0, tool_use: int = 0):
    return SimpleNamespace(
        prompt_token_count=prompt,
        cached_content_token_count=cached,
        candidates_token_count=out,
        thoughts_token_count=thoughts,
        tool_use_prompt_token_count=tool_use,
    )


def _spend(ctx: _Ctx, prompt: int) -> None:
    """One completed turn of `prompt` input tokens, counted where ADK reports it."""
    studio._turn_started[ctx.invocation_id] = 0.0
    studio._after_model(ctx, SimpleNamespace(usage_metadata=_usage(prompt)))


class TokenBudgetTests(unittest.TestCase):
    def setUp(self):
        for state in (studio._invocation_input, studio._invocation_cached, studio._token_warned,
                      studio._invocation_started, studio._turn_started, studio._role_clock):
            state.clear()
        studio._tripped.clear()

    # ---------------------------------------------------------------- the counter
    def test_input_accumulates_across_turns(self):
        ctx = _Ctx()
        _spend(ctx, 1000)
        _spend(ctx, 2500)
        self.assertEqual(studio._invocation_input["inv-1"], 3500)

    def test_invocations_are_counted_separately(self):
        _spend(_Ctx("a"), 1000)
        _spend(_Ctx("b"), 4000)
        self.assertEqual(studio._invocation_input["a"], 1000)
        self.assertEqual(studio._invocation_input["b"], 4000)

    def test_a_turn_without_usage_is_survived(self):
        ctx = _Ctx()
        studio._turn_started[ctx.invocation_id] = 0.0
        studio._after_model(ctx, SimpleNamespace(usage_metadata=None))
        self.assertNotIn("inv-1", studio._invocation_input)

    # ---------------------------------------------------------------- the breaker
    def test_under_the_cap_the_turn_proceeds(self):
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=10_000)
        _spend(ctx, 9_000)
        self.assertIsNone(before(ctx, _Request()))
        self.assertNotIn("inv-1", studio._tripped)

    def test_at_the_cap_the_invocation_halts(self):
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=10_000)
        _spend(ctx, 10_500)
        halted = before(ctx, _Request())

        self.assertIsNotNone(halted)
        self.assertIn("inv-1", studio._tripped)
        text = halted.content.parts[0].text
        self.assertIn("HALTED", text)
        # The transcript has to distinguish a run that ran long from one that ran expensive.
        self.assertIn("input tokens", text)
        self.assertIn("10,500", text)

    def test_a_halted_invocation_stays_halted_for_every_agent(self):
        """The trip is per invocation, so a transfer after the halt gets no free call."""
        before = studio._make_before_model(None, None, token_cap=10_000)
        _spend(_Ctx(agent="penciler"), 10_500)
        before(_Ctx(agent="penciler"), _Request())

        other = _Ctx(agent="inker")
        self.assertIsNotNone(before(other, _Request()))

    def test_no_cap_means_no_breaker(self):
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=None)
        _spend(ctx, 50_000_000)
        self.assertIsNone(before(ctx, _Request()))

    def test_the_time_breaker_still_reports_minutes(self):
        """Both caps share one trip set and one halt path; the message must not blur them."""
        ctx = _Ctx()
        studio._invocation_started[ctx.invocation_id] = 0.0
        halted = studio._trip_breaker(ctx, elapsed=7200.0, cap=6300.0)
        text = halted.content.parts[0].text
        self.assertIn("minutes", text)
        self.assertNotIn("input tokens", text)

    # ---------------------------------------------------------------- the ladder
    def test_warns_once_per_threshold(self):
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=10_000)

        _spend(ctx, 7_600)                       # 76% — crosses 0.75
        first = _Request()
        before(ctx, first)
        self.assertEqual(len(first.contents), 1)
        self.assertIn("76%", first.contents[0].parts[0].text)

        _spend(ctx, 100)                         # still 77%, nothing new to say
        second = _Request()
        before(ctx, second)
        self.assertEqual(len(second.contents), 0)

        _spend(ctx, 1_500)                       # 92% — crosses 0.90
        third = _Request()
        before(ctx, third)
        self.assertEqual(len(third.contents), 1)

    def test_passing_both_thresholds_at_once_consumes_both(self):
        """The gentler notice must not arrive after the sharper one."""
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=10_000)
        _spend(ctx, 9_500)                       # 95% — crosses 0.75 and 0.90 together
        first = _Request()
        before(ctx, first)
        self.assertEqual(len(first.contents), 1)

        _spend(ctx, 100)
        second = _Request()
        before(ctx, second)
        self.assertEqual(len(second.contents), 0)

    def test_the_notice_goes_on_contents_not_the_instruction(self):
        """`append_instructions` edits the head of the cache prefix, where a warning costs more
        than the overrun it prevents."""
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=10_000)
        _spend(ctx, 8_000)
        request = _Request()
        before(ctx, request)
        self.assertEqual(request.contents[0].role, "user")

    # ---------------------------------------------------------------- configuration
    def test_env_tokens_accepts_readable_spellings(self):
        import os
        for raw, expected in (("2000000", 2_000_000), ("2_000_000", 2_000_000),
                              ("2,000,000", 2_000_000), ("", None), ("lots", None)):
            os.environ["POLSON_TEST_TOKENS"] = raw
            self.assertEqual(studio._env_tokens("POLSON_TEST_TOKENS"), expected, raw)
        del os.environ["POLSON_TEST_TOKENS"]

    def test_pruning_drops_counters_once_there_are_enough_to_matter(self):
        """The guard is deliberate — it keeps the common turn from scanning — so this has to fill
        past it. The bug it now covers: the guard used to read `_role_clock` alone, and a
        single-agent run with a token cap and no deadline creates none, so nothing was ever pruned.
        """
        for i in range(70):
            ctx = _Ctx(f"old-{i}")
            _spend(ctx, 1_000)
            studio._token_warned[ctx.invocation_id] = {0.75}
            studio._invocation_started[ctx.invocation_id] = 0.0
            studio._tripped.add(ctx.invocation_id)
        self.assertEqual(len(studio._role_clock), 0, "no role clocks: this is the single-agent case")

        studio._prune_role_clocks(studio._ROLE_CLOCK_TTL * 2)

        self.assertEqual(studio._invocation_input, {})
        self.assertEqual(studio._token_warned, {})
        self.assertEqual(studio._tripped, set())

    def test_a_live_run_is_not_pruned(self):
        """Only entries past the TTL go; a long but current run keeps its counter."""
        for i in range(70):
            _spend(_Ctx(f"live-{i}"), 1_000)
            studio._invocation_started[f"live-{i}"] = 0.0

        studio._prune_role_clocks(studio._ROLE_CLOCK_TTL / 2)

        self.assertEqual(len(studio._invocation_input), 70)


class TurnLogTests(unittest.TestCase):
    """The turn line is a grep target in a container log, so its shape is a contract."""

    def setUp(self):
        studio._turn_started.clear()
        studio._invocation_input.clear()
        studio._invocation_cached.clear()

    def _log(self, usage):
        ctx = _Ctx()
        studio._turn_started[ctx.invocation_id] = 0.0
        with self.assertLogs(studio._TURN_LOG, level="WARNING") as captured:
            studio._after_model(ctx, SimpleNamespace(usage_metadata=usage))
        return captured.output[0]

    def test_reports_thinking_and_tool_use_separately(self):
        line = self._log(_usage(1000, cached=400, out=50, thoughts=9_000, tool_use=1_200))
        self.assertIn("in=1000", line)
        self.assertIn("cached=400", line)
        self.assertIn("out=50", line)
        self.assertIn("think=9000", line)
        self.assertIn("tooluse=1200", line)

    def test_a_thinking_loop_is_visible_as_think_without_out(self):
        """The failure the watchdog structurally cannot see: it counts tool calls, and an agent
        thinking in circles makes none, so `after_tool_callback` never fires."""
        line = self._log(_usage(50_000, out=0, thoughts=30_000))
        self.assertIn("think=30000", line)
        self.assertIn("out=0", line)

    def test_absent_counts_keep_the_field_order(self):
        line = self._log(SimpleNamespace(prompt_token_count=None, cached_content_token_count=None,
                                         candidates_token_count=None, thoughts_token_count=None,
                                         tool_use_prompt_token_count=None))
        # "?" where the model said nothing and 0 would be a claim; 0 for the rest, so a parser
        # splits on the same fields every turn.
        self.assertIn("in=? cached=0 out=? think=0 tooluse=0", line)

    def test_a_model_without_the_newer_counts_is_survived(self):
        """`thoughts_token_count` is not on every model's usage; its absence must not throw."""
        line = self._log(SimpleNamespace(prompt_token_count=100, cached_content_token_count=0,
                                         candidates_token_count=10))
        self.assertIn("think=0", line)
        self.assertIn("tooluse=0", line)


class BudgetStatusTests(unittest.IsolatedAsyncioTestCase):
    """`Date.now()` gives an agent a clock; nothing gave it a token count. This is that."""

    def setUp(self):
        studio._invocation_input.clear()
        studio._invocation_cached.clear()
        studio._invocation_started.clear()

    async def test_reports_spend_against_both_allowances(self):
        ctx = _Ctx()
        _spend(ctx, 400_000)
        studio._invocation_started[ctx.invocation_id] = studio.time.monotonic()

        status = await studio._make_budget_status(1_000_000, 30.0)(ctx)

        self.assertEqual(status["inputTokensSpent"], 400_000)
        self.assertEqual(status["inputTokenCap"], 1_000_000)
        self.assertEqual(status["inputTokensRemaining"], 600_000)
        self.assertEqual(status["inputTokensUsedPercent"], 40.0)
        self.assertEqual(status["deadlineMinutes"], 30.0)

    async def test_says_so_when_the_run_has_no_budget(self):
        status = await studio._make_budget_status(None, None)(_Ctx())
        self.assertEqual(status["inputTokensSpent"], 0)
        self.assertNotIn("inputTokenCap", status)
        self.assertIn("no budget set", status["note"])


    async def test_reports_the_cache_share(self):
        """54%, 46% and 29% across three runs of one brief - and the run with the least input cost
        the most. Nothing else surfaces it, and it is not a lever: caching here is implicit."""
        ctx = _Ctx()
        studio._turn_started[ctx.invocation_id] = 0.0
        studio._after_model(ctx, SimpleNamespace(usage_metadata=_usage(100_000, cached=60_000)))
        studio._turn_started[ctx.invocation_id] = 0.0
        studio._after_model(ctx, SimpleNamespace(usage_metadata=_usage(100_000, cached=0)))

        status = await studio._make_budget_status(1_000_000, None)(ctx)

        self.assertEqual(status["inputTokensSpent"], 200_000)
        self.assertEqual(status["cachedInputTokens"], 60_000)
        self.assertEqual(status["cachedSharePercent"], 30.0)

    async def test_cached_is_a_subset_not_an_addition(self):
        """`prompt_token_count` is the whole prompt; `cached_content_token_count` is the part of it
        served from cache. Summing them would double-count."""
        ctx = _Ctx()
        _spend(ctx, 0)  # no-op: prompt of 0 is falsy and must not be counted
        studio._turn_started[ctx.invocation_id] = 0.0
        studio._after_model(ctx, SimpleNamespace(usage_metadata=_usage(80_000, cached=75_000)))

        status = await studio._make_budget_status(100_000, None)(ctx)
        self.assertEqual(status["inputTokensSpent"], 80_000)
        self.assertLessEqual(status["cachedInputTokens"], status["inputTokensSpent"])
        self.assertEqual(status["inputTokensRemaining"], 20_000)

    async def test_cache_share_is_zero_before_any_turn(self):
        status = await studio._make_budget_status(1_000_000, None)(_Ctx())
        self.assertEqual(status["cachedSharePercent"], 0.0)

    async def test_remaining_never_goes_negative(self):
        ctx = _Ctx()
        _spend(ctx, 1_500_000)
        status = await studio._make_budget_status(1_000_000, None)(ctx)
        self.assertEqual(status["inputTokensRemaining"], 0)


class HaltNoteTests(unittest.TestCase):
    """That a halted run leaves the record able to say so.

    The breaker already wrote a line to the server's own log, which is the wrong reader: a director
    watching a run page sees `run.begin` and then nothing, and cannot tell a halt from a hang. The
    note left here is what the transcript's `run.end` spends to say *why* the run stopped.
    """

    def setUp(self):
        for state in (studio._invocation_input, studio._invocation_cached, studio._token_warned,
                      studio._invocation_started, studio._turn_started, studio._role_clock):
            state.clear()
        studio._tripped.clear()
        transcript.__dict__["_halted"].clear()
        self.addCleanup(transcript.__dict__["_halted"].clear)

    def test_the_token_breaker_leaves_its_numbers(self):
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=10_000)
        _spend(ctx, 10_500)
        before(ctx, _Request())

        self.assertEqual({"limit": "tokens", "used": 10_500, "cap": 10_000},
                         transcript._halted["inv-1"])

    def test_the_time_breaker_leaves_seconds_and_says_it_was_time(self):
        studio._trip_breaker(_Ctx(), elapsed=7200.0, cap=6300.0)

        note = transcript._halted["inv-1"]
        self.assertEqual("time", note["limit"])
        self.assertEqual(7200.0, note["used"])
        self.assertEqual(6300.0, note["cap"])

    def test_the_note_records_the_halt_not_the_last_refusal(self):
        """The breaker fires again for every agent that turns up after the halt, and on the time
        path each re-trip carries a larger `elapsed` — so without the first-trip guard the note
        drifts, and `run.end` reports when the run gave up rather than when it was stopped.

        Asserted on the time path deliberately. The token path re-trips with an identical `spent`,
        so a test written there passes whether or not the guard is present — which is what an
        earlier version of this test did.
        """
        studio._trip_breaker(_Ctx(agent="penciler"), elapsed=6400.0, cap=6300.0)
        studio._trip_breaker(_Ctx(agent="inker"), elapsed=9999.0, cap=6300.0)

        self.assertEqual(1, len(transcript._halted))
        self.assertEqual(6400.0, transcript._halted["inv-1"]["used"],
                         "the re-trip overwrote the moment the run was actually halted")

    def test_a_run_under_its_cap_leaves_nothing(self):
        ctx, before = _Ctx(), studio._make_before_model(None, None, token_cap=10_000)
        _spend(ctx, 500)
        before(ctx, _Request())

        self.assertEqual(0, len(transcript._halted))


if __name__ == "__main__":
    unittest.main(verbosity=2)
