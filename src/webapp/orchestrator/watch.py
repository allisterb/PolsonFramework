"""One run, watchable.

The three event files have two different relationships to this process, and this is where that stops
mattering to a watcher:

- `agent.jsonl` and `director.jsonl` are ours. `run_turn` takes a `sink`, so every line it writes is
  offered to the broker as it is written.
- `server.jsonl` belongs to the .NET MCP server, which is a child process. Nothing here learns that
  a script ran or a render landed except by reading that file, so a `Tailer` follows it.

Both arrive on one broker, and a watcher attaches to that. Which side an event came from is still
visible — every line carries `src` — but nobody has to care in order to watch.

**Arrival order is not the record's order.** Events reach a watcher roughly as they happen, which is
good enough to read as it streams; the canonical ordering is `(ts, src, seq)`, and `events.merge`
applies it to the files. A client that needs the exact interleaving sorts, or re-reads. This is the
same division as everywhere else here: the stream is immediate, the files are true.

Closing order matters and is deliberate. The server writes `run.end` while shutting down — which is
exactly when the run is finishing and the tailer is being stopped — so the tailer gets a final read
before the broker closes, and watchers see the end of the run rather than losing it to the teardown.
"""

from __future__ import annotations

import asyncio
from typing import Any, Callable

from .broker import BACKLOG, HISTORY, Broker, Subscription
from .director import WebDirector
from .interject import Interjections
from .events import EventLog
from .project import Project
from .tail import INTERVAL, Tailer

#: How long a question waits for a browser before it becomes a skip the agent can act on.
ANSWER_TIMEOUT = 600.0


class RunStream:
    """The broker for one run, plus the tailer that reaches the process we do not own."""

    # region Constructors
    def __init__(self, project: Project, *, history: int = HISTORY, backlog: int = BACKLOG,
                 interval: float = INTERVAL) -> None:
        self.project = project
        self.broker = Broker(history=history, backlog=backlog)
        self.tailer = Tailer(project.server_events, self.broker.publish, interval=interval)
        self._task: asyncio.Task | None = None

        # Built when `run_turn` calls the factory, and kept so a later request can reach them. An
        # answer arrives on a different HTTP request from the one that asked, and an interjection on
        # no request at all, so neither can be handled by whatever built them.
        self.director: WebDirector | None = None
        self.interjections = Interjections(publish=self.broker.publish)
    # endregion

    # region Properties
    @property
    def sink(self) -> Callable[[dict[str, Any]], None]:
        """Hand this to `run_turn`, which gives it to the logs it writes."""
        return self.broker.publish

    @property
    def running(self) -> bool:
        return self._task is not None and not self._task.done()
    # endregion

    # region Methods
    async def __aenter__(self) -> "RunStream":
        self.start()
        return self

    async def __aexit__(self, *_exc: Any) -> None:
        await self.stop()

    def start(self, *, replay_existing: bool = False) -> None:
        """Begins following `server.jsonl`. Idempotent.

        A project's record is appended to across runs, so by default everything already in the file
        belongs to an earlier one and is skipped — otherwise a second run in the same project shows
        the first run's work as its own, which it did. The skip happens here and synchronously, so
        there is no window in which the server can write before the offset is fixed.

        `replay_existing` is for reading a finished run back rather than following a live one.
        """
        if not replay_existing:
            self.tailer.skip_existing()

        if self._task is None or self._task.done():
            self._task = asyncio.create_task(self.tailer.run())

    async def stop(self) -> None:
        """Stops following, lets the tailer take its final read, then ends every subscription."""
        self.tailer.stop()
        if self._task is not None:
            try:
                await asyncio.wait_for(self._task, timeout=5.0)
            except (asyncio.TimeoutError, TimeoutError):
                self._task.cancel()
            except asyncio.CancelledError:
                pass
            self._task = None

        # Only now: the tailer's last read is where the server's own run.end comes from.
        self.broker.close()

    def attach(self, *, replay: bool = True) -> Subscription:
        """Registers a watcher, which then iterates the backlog and the live tail."""
        return self.broker.attach(replay=replay)

    def build_director(self, log: EventLog, *, timeout: float = ANSWER_TIMEOUT) -> WebDirector:
        """The browser's half of the loop, publishing onto this run's broker.

        Hand this to `run_turn` as its `director_factory`: it owns `director.jsonl`, and passing an
        already-built director would put a second writer on a file whose whole concurrency model is
        that it has one. The log arrives here, and the interjection channel adopts it too so that an
        interruption lands in the same record as everything else the director said.
        """
        self.director = WebDirector(log, self.broker.publish, timeout=timeout)
        self.interjections.log = log
        return self.director

    def answer(self, question_id: str, **reply: Any) -> bool:
        """Answers an open question. False when there is no such question, or it is settled."""
        return self.director is not None and self.director.reply(question_id, **reply)

    def say(self, text: str) -> bool:
        """The director interrupting, which is a contribution rather than a correction."""
        return self.interjections.say(text)

    @property
    def triggers(self) -> list[Any]:
        """What `run_turn` registers on the agent so an interjection can reach it mid-turn."""
        return [self.interjections.trigger()]
    # endregion
