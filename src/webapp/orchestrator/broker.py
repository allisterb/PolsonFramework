"""Fanning one run out to whoever is watching, without giving up the file as the record.

`EventLog` writes the durable record; this carries the same events to watchers *now*. The two are
not alternatives, and the broker is deliberately the lesser of them: it holds a bounded window in
memory and forgets, while the files hold everything and outlive the process. Anything that must be
true later belongs in a file — see `docs/project-layout.md`.

**Replay is the point rather than a feature.** A watcher attaching halfway through a run gets the
backlog and then the live tail from one iterator, so "watch a run" and "read a run back" are the
same code path. That is what makes a browser refresh survivable without a second mechanism, and it
is why the web app tails `server.jsonl` rather than having the .NET side push into it: a push
channel has no backlog to give a late arrival, so it grows one, and then there are two records that
can disagree about what happened.

**A watcher that cannot keep up is told so.** Both bounded buffers here can lose events — the
history window when a run outlives it, a subscriber queue when a client stalls — and in both cases
the loss is announced in the stream (`stream.truncated`, `stream.lag`) rather than papered over. A
gap a viewer knows about is recoverable by re-reading the files; a gap it does not know about is a
page quietly showing a different run from the one on disk.
"""

from __future__ import annotations

import asyncio
import sys
import threading
from typing import Any, AsyncIterator

#: Events kept for replay. A long run is a few hundred, so this is generous and still bounded.
HISTORY = 2000

#: Per-subscriber queue depth. Past this a subscriber is not slow, it is gone.
BACKLOG = 512

#: Ends a subscriber's iteration. Identity-compared, so it can never collide with an event.
_CLOSED: Any = object()


def _running_loop() -> asyncio.AbstractEventLoop | None:
    try:
        return asyncio.get_running_loop()
    except RuntimeError:
        return None


class Subscriber:
    """One watcher's queue. Drops the oldest event when full, and counts what it dropped."""

    def __init__(self, backlog: int) -> None:
        self.queue: asyncio.Queue[Any] = asyncio.Queue(maxsize=backlog)
        self.dropped = 0

    def offer(self, event: Any) -> None:
        """Never blocks and never raises: a stalled watcher must not stall the run."""
        if self.queue.full():
            try:
                self.queue.get_nowait()
                self.dropped += 1
            except asyncio.QueueEmpty:
                pass
        try:
            self.queue.put_nowait(event)
        except asyncio.QueueFull:
            self.dropped += 1


class Broker:
    """In-process publish/subscribe over one run's events."""

    # region Constructors
    def __init__(self, *, history: int = HISTORY, backlog: int = BACKLOG) -> None:
        self._history: list[dict[str, Any]] = []
        self._trimmed = 0
        self._limit = max(1, history)
        self._backlog = max(1, backlog)
        self._subscribers: list[Subscriber] = []
        self._closed = False

        # Bound on the first subscribe, which always happens inside the loop. Until then delivery is
        # synchronous, which is what makes a Broker usable from a plain unit test.
        self._loop: asyncio.AbstractEventLoop | None = None
        self._lock = threading.Lock()
    # endregion

    # region Properties
    @property
    def closed(self) -> bool:
        return self._closed

    @property
    def watchers(self) -> int:
        return len(self._subscribers)
    # endregion

    # region Methods
    def publish(self, event: dict[str, Any]) -> None:
        """Offers one event to every watcher. Never raises, never blocks, never waits on a watcher.

        Safe to call from another thread: delivery hops onto the loop, so the queues are only ever
        touched from one. `EventLog` holds a `threading.Lock` for the same reason — nothing on the
        record's path may depend on which thread reached it.
        """
        if self._closed:
            return

        loop = self._loop
        if loop is None or _running_loop() is loop:
            self._deliver(event)
            return

        try:
            loop.call_soon_threadsafe(self._deliver, event)
        except RuntimeError:
            # The loop closed under us: the run is over, and the record is already on disk.
            pass

    def attach(self, *, replay: bool = True) -> "Subscription":
        """Registers a watcher **now** and returns it, without waiting to be iterated.

        An async generator does not run until its first `__anext__`, so `subscribe()` alone leaves a
        window in which a watcher believes it is attached and is not. Replay closes that window for
        anything already written, but a caller that wants no window at all — an HTTP handler
        attaching before it starts the run — needs registration to be an ordinary call. This is it.
        """
        subscriber = Subscriber(self._backlog)
        with self._lock:
            self._subscribers.append(subscriber)
            backlog = list(self._history) if replay else []
            trimmed = self._trimmed if replay else 0
            closed = self._closed

        if self._loop is None:
            self._loop = _running_loop()

        return Subscription(self, subscriber, backlog, trimmed, closed)

    async def subscribe(self, *, replay: bool = True) -> AsyncIterator[dict[str, Any]]:
        """Attaches and iterates: the backlog, then the live tail, until the broker closes."""
        subscription = self.attach(replay=replay)
        async for event in subscription:
            yield event

    def detach(self, subscriber: Subscriber) -> None:
        with self._lock:
            if subscriber in self._subscribers:
                self._subscribers.remove(subscriber)

    def close(self) -> None:
        """Ends every subscription. Idempotent."""
        if self._closed:
            return
        self._closed = True

        loop = self._loop
        if loop is None or _running_loop() is loop:
            self._release()
            return

        try:
            loop.call_soon_threadsafe(self._release)
        except RuntimeError:
            pass

    def history(self) -> list[dict[str, Any]]:
        """A copy of the replay window, for a caller that wants it without subscribing."""
        with self._lock:
            return list(self._history)
    # endregion

    # region Methods (private)
    def _deliver(self, event: dict[str, Any]) -> None:
        try:
            with self._lock:
                self._history.append(event)
                if len(self._history) > self._limit:
                    excess = len(self._history) - self._limit
                    del self._history[:excess]
                    self._trimmed += excess
                subscribers = list(self._subscribers)

            for subscriber in subscribers:
                subscriber.offer(event)
        except Exception as exc:  # noqa: BLE001 — a broken watcher must not take the run with it
            print(f"warning: event broker dropped an event: {exc}", file=sys.stderr)

    def _release(self) -> None:
        with self._lock:
            subscribers = list(self._subscribers)
        for subscriber in subscribers:
            subscriber.offer(_CLOSED)
    # endregion


class Subscription:
    """One attached watcher: the backlog it was given, and the live tail after it."""

    # region Constructors
    def __init__(self, broker: Broker, subscriber: Subscriber, backlog: list[dict[str, Any]],
                 trimmed: int, closed: bool) -> None:
        self._broker = broker
        self._subscriber = subscriber
        self._backlog = backlog
        self._trimmed = trimmed
        self._closed_at_attach = closed
        self._iterator: AsyncIterator[dict[str, Any]] | None = None
    # endregion

    # region Methods
    def __aiter__(self) -> AsyncIterator[dict[str, Any]]:
        """The *same* iterator every time.

        Returning a fresh async generator per `async for` would make a subscription re-iterable, and
        each pass would replay the backlog — so a handler that stops reading and resumes would show
        the run twice. One attach is one position in the stream.
        """
        if self._iterator is None:
            self._iterator = self._stream()
        return self._iterator

    async def _stream(self) -> AsyncIterator[dict[str, Any]]:
        self._broker._loop = asyncio.get_running_loop()
        try:
            if self._trimmed:
                yield {"src": "stream", "type": "stream.truncated", "dropped": self._trimmed}
            for event in self._backlog:
                yield event
            if self._closed_at_attach:
                return

            while True:
                event = await self._subscriber.queue.get()
                if event is _CLOSED:
                    return

                # Reported before the event that follows the gap, so a reader sees the break where it
                # happened rather than at the end of the run.
                if self._subscriber.dropped:
                    dropped, self._subscriber.dropped = self._subscriber.dropped, 0
                    yield {"src": "stream", "type": "stream.lag", "dropped": dropped}

                yield event
        finally:
            self.close()

    def close(self) -> None:
        """Unregisters. Idempotent, and safe to call while iterating."""
        self._broker.detach(self._subscriber)
    # endregion
