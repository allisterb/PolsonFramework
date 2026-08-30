"""Watching a run as it happens: the broker, the tailer, and the browser's half of the loop.

`unittest` rather than pytest, for the same reason as `test_orchestrator.py`: nothing here may
install a package.

    python -m unittest discover -s src/webapp -t src/webapp

Nothing reaches the network or starts an agent. What is checked here is the machinery that carries a
run to a watcher — and, more to the point, the four ways it could lie about a run: losing an event
silently, showing a stale file after a reset, reading half a line as a whole one, or leaving a run
wedged on an answer that is never coming.
"""

from __future__ import annotations

import asyncio
import contextlib
import io
import json
import shutil
import tempfile
import threading
import unittest
from pathlib import Path
from types import SimpleNamespace

from orchestrator import events
from orchestrator.broker import Broker
from orchestrator.director import Reply, WebDirector
from orchestrator.tail import Tailer
from orchestrator.watch import RunStream


def question(text: str = "Which direction?", options: list[str] | None = None, multi: bool = False):
    """A stand-in for the SDK's question object, which the director reads by duck typing."""
    return SimpleNamespace(
        question=text,
        is_multi_select=multi,
        options=[SimpleNamespace(id=f"opt{i}", text=o) for i, o in enumerate(options or [], start=1)],
    )


async def take(source, count: int, *, timeout: float = 2.0) -> list[dict]:
    """Reads `count` events off a subscription, failing rather than hanging if they never arrive."""
    collected: list[dict] = []
    if count == 0:
        return collected

    async def drain() -> None:
        async for event in source:
            collected.append(event)
            if len(collected) >= count:
                return

    await asyncio.wait_for(drain(), timeout=timeout)
    return collected


class BrokerTests(unittest.IsolatedAsyncioTestCase):
    """One run, many watchers, and the honest reporting of anything lost on the way."""

    async def test_a_late_watcher_is_given_the_backlog_before_the_live_tail(self):
        """The property the whole design rests on: attaching late is the same as reading back.

        Without it a browser refresh loses the run, and the fix would be a replay buffer — which is
        the file, rebuilt in memory and worse.
        """
        broker = Broker()
        broker.publish({"src": "server", "seq": 1, "type": "run.start"})
        broker.publish({"src": "server", "seq": 2, "type": "script.ok"})

        stream = broker.attach()
        backlog = await take(stream, 2)
        self.assertEqual([e["type"] for e in backlog], ["run.start", "script.ok"])

        # Reading again continues where the last read stopped. One attach is one position in the
        # stream, so resuming must not replay what has already been handed over.
        broker.publish({"src": "server", "seq": 3, "type": "render"})
        live = await take(stream, 1)
        self.assertEqual(live[0]["type"], "render")

    async def test_every_watcher_sees_every_event(self):
        broker = Broker()
        # attach() registers now rather than on the first read, so both watchers are known to be
        # listening before anything is published — no sleeps, no race.
        first, second = broker.attach(), broker.attach()
        self.assertEqual(broker.watchers, 2)

        broker.publish({"src": "agent", "seq": 1, "type": "text"})
        self.assertEqual((await take(first, 1))[0]["type"], "text")
        self.assertEqual((await take(second, 1))[0]["type"], "text")

    async def test_replay_can_be_declined(self):
        broker = Broker()
        broker.publish({"src": "server", "seq": 1, "type": "run.start"})

        stream = broker.attach(replay=False)
        broker.publish({"src": "server", "seq": 2, "type": "render"})
        self.assertEqual((await take(stream, 1))[0]["type"], "render")

    async def test_a_run_outliving_the_history_window_says_so(self):
        """A trimmed backlog is announced, because a page that quietly starts mid-run looks complete."""
        broker = Broker(history=2)
        for i in range(5):
            broker.publish({"src": "server", "seq": i, "type": "note"})

        received = await take(broker.attach(), 3)
        self.assertEqual(received[0]["type"], "stream.truncated")
        self.assertEqual(received[0]["dropped"], 3)
        self.assertEqual([e["seq"] for e in received[1:]], [3, 4])

    async def test_a_stalled_watcher_is_told_what_it_missed(self):
        """The queue is bounded, so a client that stops reading loses events — but is never lied to."""
        broker = Broker(backlog=2)
        stream = broker.attach(replay=False)

        for i in range(6):
            broker.publish({"src": "server", "seq": i, "type": "note"})

        # The gap is announced before the event that follows it, so a reader sees the break where it
        # happened rather than discovering it at the end.
        received = await take(stream, 3)
        self.assertEqual(received[0]["type"], "stream.lag")
        self.assertEqual(received[0]["dropped"], 4)
        self.assertEqual([e["seq"] for e in received[1:]], [4, 5])   # the queue kept the newest

    async def test_a_stalled_watcher_never_blocks_the_run(self):
        broker = Broker(backlog=1)
        broker.attach(replay=False)

        # Far more than the queue holds. If publishing waited on a watcher this would not return.
        for i in range(1000):
            broker.publish({"src": "server", "seq": i, "type": "note"})

        self.assertEqual(broker.watchers, 1)

    async def test_closing_ends_every_subscription(self):
        broker = Broker()
        stream = broker.attach()

        collected = []

        async def drain():
            async for event in stream:
                collected.append(event)

        task = asyncio.create_task(drain())
        await asyncio.sleep(0.05)
        broker.close()
        await asyncio.wait_for(task, timeout=2.0)

        self.assertTrue(broker.closed)
        broker.close()   # idempotent

    async def test_subscribing_after_close_returns_the_record_and_stops(self):
        broker = Broker()
        broker.publish({"src": "server", "seq": 1, "type": "run.start"})
        broker.close()

        collected = [e async for e in broker.attach()]
        self.assertEqual([e["type"] for e in collected], ["run.start"])

    async def test_publishing_from_another_thread_arrives(self):
        """`EventLog` holds a lock because nothing on the record's path may assume a thread."""
        broker = Broker()
        stream = broker.attach(replay=False)

        threading.Thread(
            target=lambda: broker.publish({"src": "agent", "seq": 1, "type": "text"}),
            daemon=True,
        ).start()

        self.assertEqual((await take(stream, 1, timeout=3.0))[0]["type"], "text")

    def test_a_broker_works_with_no_loop_at_all(self):
        """Usable from a plain synchronous test, which is how `EventLog` is checked below."""
        broker = Broker()
        broker.publish({"src": "server", "seq": 1, "type": "note"})
        self.assertEqual(len(broker.history()), 1)


class TailerTests(unittest.TestCase):
    """Reading a file another process is appending to, without ever reporting a run that did not happen."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="polson-tail-"))
        self.path = self.dir / "server.jsonl"
        self.seen: list[dict] = []
        self.tailer = Tailer(self.path, self.seen.append)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def write(self, *lines: str, partial: str = "") -> None:
        with open(self.path, "a", encoding="utf-8", newline="") as handle:
            for line in lines:
                handle.write(line + "\n")
            if partial:
                handle.write(partial)

    def event(self, seq: int, kind: str = "render") -> str:
        return json.dumps({"ts": "2026-08-30T00:00:00.000Z", "seq": seq, "src": "server", "type": kind},
                          separators=(",", ":"))

    def test_a_missing_file_is_not_an_error(self):
        """The server creates it on its first event, so a watcher attaching at once finds nothing."""
        self.assertEqual(self.tailer.poll(), 0)
        self.assertEqual(self.seen, [])

    def test_appended_lines_are_picked_up_and_never_repeated(self):
        self.write(self.event(1), self.event(2))
        self.assertEqual(self.tailer.poll(), 2)
        self.assertEqual(self.tailer.poll(), 0)

        self.write(self.event(3))
        self.assertEqual(self.tailer.poll(), 1)
        self.assertEqual([e["seq"] for e in self.seen], [1, 2, 3])

    def test_a_half_written_line_is_held_back_until_it_is_whole(self):
        """A read can land mid-append. Half a JSON object is not an event, and must not be dropped either."""
        self.write(self.event(1), partial='{"seq":2,"src":"ser')
        self.assertEqual(self.tailer.poll(), 1)

        self.assertEqual(self.tailer.poll(), 0)   # still partial, still held back

        with open(self.path, "a", encoding="utf-8", newline="") as handle:
            handle.write('ver","type":"render"}\n')

        self.assertEqual(self.tailer.poll(), 1)
        self.assertEqual([e["seq"] for e in self.seen], [1, 2])

    def test_a_reset_starts_the_file_over_rather_than_seeking_past_its_end(self):
        """`create-project --reset` deletes events/. A shorter file is a new run, not a corrupt one."""
        self.write(self.event(1), self.event(2), self.event(3))
        self.tailer.poll()
        self.seen.clear()

        self.path.unlink()
        self.write(self.event(1, "run.start"))

        self.assertEqual(self.tailer.poll(), 1)
        self.assertEqual(self.tailer.resets, 1)
        self.assertEqual(self.seen[0]["type"], "run.start")

    def test_an_unreadable_line_is_skipped_and_the_rest_still_arrive(self):
        self.write(self.event(1), "{not json at all", self.event(3))
        self.assertEqual(self.tailer.poll(), 2)
        self.assertEqual([e["seq"] for e in self.seen], [1, 3])

    def test_a_line_that_is_not_an_object_is_not_an_event(self):
        self.write("[1, 2, 3]", '"just a string"', self.event(4))
        self.assertEqual(self.tailer.poll(), 1)
        self.assertEqual(self.seen[0]["seq"], 4)

    def test_from_start_false_skips_what_is_already_there(self):
        self.write(self.event(1), self.event(2))
        tailer = Tailer(self.path, self.seen.append, from_start=False)

        self.assertEqual(tailer.poll(), 0)
        self.write(self.event(3))
        self.assertEqual(tailer.poll(), 1)
        self.assertEqual([e["seq"] for e in self.seen], [3])


class TailerLifecycleTests(unittest.IsolatedAsyncioTestCase):
    """`run()` and `stop()`, including the last read that catches the server's own `run.end`."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="polson-tail-"))
        self.path = self.dir / "server.jsonl"
        self.seen: list[dict] = []

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def append(self, kind: str) -> None:
        with open(self.path, "a", encoding="utf-8", newline="") as handle:
            handle.write(json.dumps({"seq": 1, "src": "server", "type": kind}) + "\n")

    async def test_running_follows_the_file_until_stopped(self):
        tailer = Tailer(self.path, self.seen.append, interval=0.02)
        task = asyncio.create_task(tailer.run())

        self.append("render")
        for _ in range(100):
            if self.seen:
                break
            await asyncio.sleep(0.02)

        tailer.stop()
        await asyncio.wait_for(task, timeout=2.0)
        self.assertEqual(self.seen[0]["type"], "render")

    async def test_the_final_read_catches_what_was_written_as_the_run_ended(self):
        """The server writes `run.end` while shutting down — which is when the tailer is being stopped."""
        tailer = Tailer(self.path, self.seen.append, interval=5.0)
        task = asyncio.create_task(tailer.run())
        await asyncio.sleep(0.05)

        self.append("run.end")
        tailer.stop()
        await asyncio.wait_for(task, timeout=2.0)

        self.assertEqual([e["type"] for e in self.seen], ["run.end"])


class EventLogSinkTests(unittest.TestCase):
    """The file stays the record; the sink is what makes the same events visible now."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="polson-sink-"))

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def test_the_sink_sees_exactly_what_was_written(self):
        seen: list[dict] = []
        log = events.EventLog(self.dir / "agent.jsonl", "agent", seen.append)
        log.append("text", chars=12)

        written = events.read_events(self.dir / "agent.jsonl")
        self.assertEqual(len(seen), 1)
        self.assertEqual(seen, written)

    def test_a_sink_that_raises_does_not_break_the_record(self):
        """A watcher is the lesser half. Losing one must never cost a line on disk."""
        def explode(_event):
            raise RuntimeError("watcher is broken")

        log = events.EventLog(self.dir / "agent.jsonl", "agent", explode)
        with contextlib.redirect_stderr(io.StringIO()):
            log.append("text", chars=1)
            log.append("text", chars=2)

        self.assertTrue(log.enabled)
        self.assertEqual(len(events.read_events(self.dir / "agent.jsonl")), 2)

    def test_a_broker_can_be_the_sink(self):
        broker = Broker()
        log = events.EventLog(self.dir / "director.jsonl", "director", broker.publish)
        log.append("message", text="begin")

        self.assertEqual([e["type"] for e in broker.history()], ["message"])


class WebDirectorTests(unittest.IsolatedAsyncioTestCase):
    """The browser's half of the loop: a question with an id, and every path that closes it."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="polson-director-"))
        self.published: list[dict] = []
        self.log = events.EventLog(self.dir / "director.jsonl", "director")

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def director(self, **kwargs) -> WebDirector:
        return WebDirector(self.log, self.published.append, **kwargs)

    async def answered(self, director: WebDirector, reply: dict, entry=None):
        """Runs the hook and answers the question it opens, as the web layer would."""
        entry = entry or question()
        task = asyncio.create_task(director.run(None, SimpleNamespace(questions=[entry])))

        for _ in range(100):
            if director.pending:
                break
            await asyncio.sleep(0.01)

        self.assertTrue(director.reply(director.pending[0], **reply))
        return await asyncio.wait_for(task, timeout=2.0)

    async def test_a_question_is_published_with_an_id_and_answered_by_it(self):
        director = self.director()
        result = await self.answered(director, {"text": "warmer, and lose the globe"})

        opened = [e for e in self.published if e["type"] == "question.open"]
        self.assertEqual(len(opened), 1)
        self.assertEqual(opened[0]["text"], "Which direction?")
        self.assertEqual(result.responses[0].freeform_response, "warmer, and lose the globe")

        closed = [e for e in self.published if e["type"] == "question.closed"]
        self.assertEqual(closed[0]["reason"], "answered")

    async def test_the_durable_record_keeps_the_words_and_not_the_transport_id(self):
        """`director.jsonl` is read by people. The id is how one request finds another, nothing more."""
        director = self.director()
        await self.answered(director, {"text": "warmer"})

        written = events.read_events(self.dir / "director.jsonl")
        self.assertEqual([e["type"] for e in written], ["question", "answer"])
        self.assertEqual(written[0]["text"], "Which direction?")
        self.assertEqual(written[1]["text"], "warmer")
        self.assertNotIn("id", written[0])

    async def test_a_chosen_option_comes_back_as_an_option_id(self):
        director = self.director()
        entry = question(options=["Serif", "Grotesque"])
        result = await self.answered(director, {"selected": ["opt2"]}, entry=entry)

        self.assertEqual(result.responses[0].selected_option_ids, ["opt2"])

    async def test_an_option_the_agent_never_offered_is_not_chosen_for_it(self):
        """A client sending back an id of its own invention must not silently pick something else."""
        director = self.director()
        entry = question(options=["Serif", "Grotesque"])
        result = await self.answered(director, {"selected": ["opt99"], "text": "neither, go rounder"},
                                     entry=entry)

        self.assertIsNone(result.responses[0].selected_option_ids or None)
        self.assertEqual(result.responses[0].freeform_response, "neither, go rounder")

    async def test_an_empty_answer_is_a_skip(self):
        director = self.director()
        result = await self.answered(director, {"text": "   "})
        self.assertTrue(result.responses[0].skipped)

    async def test_replying_to_an_unknown_or_settled_question_is_refused(self):
        director = self.director()
        self.assertFalse(director.reply("q404", text="hello"))

        await self.answered(director, {"text": "once"})
        self.assertFalse(director.reply("q1", text="twice"))

    async def test_a_question_nobody_answers_becomes_a_skip_the_agent_can_act_on(self):
        """A visitor closing the tab must not wedge the run — and the agent is told plainly why."""
        director = self.director(timeout=0.1)
        result = await asyncio.wait_for(
            director.run(None, SimpleNamespace(questions=[question()])), timeout=2.0)

        response = result.responses[0]
        self.assertTrue(response.skipped)
        self.assertIn("unanswered", response.freeform_response)

        closed = [e for e in self.published if e["type"] == "question.closed"]
        self.assertEqual(closed[0]["reason"], "timeout")
        self.assertEqual(director.pending, [])

    async def test_abandoning_settles_every_open_question(self):
        director = self.director(timeout=5.0)
        task = asyncio.create_task(director.run(None, SimpleNamespace(questions=[question()])))

        for _ in range(100):
            if director.pending:
                break
            await asyncio.sleep(0.01)

        self.assertEqual(director.abandon(), 1)
        result = await asyncio.wait_for(task, timeout=2.0)
        self.assertTrue(result.responses[0].skipped)

    async def test_a_run_with_no_questions_asks_nothing(self):
        director = self.director()
        result = await director.run(None, SimpleNamespace(questions=[]))
        self.assertEqual(result.responses, [])
        self.assertEqual(self.published, [])

    def test_a_web_director_counts_as_attended(self):
        """It selects INTERACTIVE behaviour, which is what lets the agent stop and ask at all."""
        self.assertTrue(self.director().attended)

    def test_a_reply_defaults_to_an_empty_answer(self):
        reply = Reply()
        self.assertEqual((reply.text, reply.selected, reply.skipped), ("", [], False))


class RunStreamTests(unittest.IsolatedAsyncioTestCase):
    """Both halves of the record on one broker: ours in-process, the server's by reading its file."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="polson-watch-"))
        (self.dir / "events").mkdir()
        self.project = SimpleNamespace(server_events=self.dir / "events" / "server.jsonl")

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def server_writes(self, kind: str, **fields) -> None:
        """Stands in for the .NET MCP server, which appends and closes exactly like this."""
        line = json.dumps({"src": "server", "seq": 1, "type": kind, **fields}, separators=(",", ":"))
        with open(self.project.server_events, "a", encoding="utf-8", newline="") as handle:
            handle.write(line + "\n")

    async def test_both_sides_of_the_record_reach_one_watcher(self):
        stream = RunStream(self.project, interval=0.02)
        async with stream:
            watcher = stream.attach()

            # Ours, written in-process and offered to the sink as it is written.
            log = events.EventLog(self.dir / "events" / "agent.jsonl", "agent", stream.sink)
            log.append("text", chars=4)

            # The server's, which can only be learned by reading its file.
            self.server_writes("render", artifact="artifacts/01.webp")

            received = await take(watcher, 2, timeout=3.0)

        by_source = {e["src"]: e["type"] for e in received}
        self.assertEqual(by_source, {"agent": "text", "server": "render"})

    async def test_the_end_of_the_run_is_not_lost_to_the_teardown(self):
        """The server writes run.end while shutting down, which is when the tailer is being stopped."""
        stream = RunStream(self.project, interval=5.0)   # long enough that only the final read can catch it
        stream.start()
        watcher = stream.attach()

        self.server_writes("run.end")
        await stream.stop()

        received = [e async for e in watcher]
        self.assertEqual([e["type"] for e in received], ["run.end"])

    async def test_a_watcher_attaching_after_the_run_still_gets_it(self):
        """The replay window is what makes a browser refresh survivable."""
        stream = RunStream(self.project, interval=0.02)
        async with stream:
            self.server_writes("run.start")
            await take(stream.attach(), 1, timeout=3.0)

        self.assertEqual([e["type"] for e in await take(stream.attach(), 1)], ["run.start"])

    async def test_stopping_twice_is_harmless(self):
        stream = RunStream(self.project, interval=0.02)
        stream.start()
        self.assertTrue(stream.running)
        await stream.stop()
        await stream.stop()
        self.assertFalse(stream.running)

    async def test_the_director_it_builds_publishes_onto_this_run(self):
        stream = RunStream(self.project, interval=0.02)
        watcher = stream.attach()

        log = events.EventLog(self.dir / "events" / "director.jsonl", "director")
        director = stream.director(log, timeout=0.1)
        self.assertTrue(director.attended)

        await asyncio.wait_for(director.run(None, SimpleNamespace(questions=[question()])), timeout=3.0)

        kinds = [e["type"] for e in await take(watcher, 2, timeout=3.0)]
        self.assertEqual(kinds, ["question.open", "question.closed"])


if __name__ == "__main__":
    unittest.main()
