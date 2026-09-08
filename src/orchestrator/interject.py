"""The director's channel into a turn that is already running.

Answering a question and interrupting are different acts, and Polson needs both.

`Director` answers when the **agent** asks — it is a hook, so it only ever runs at a moment the agent
chose. Interjecting is the other direction: the director says something the agent did not ask for,
while it is mid-stride. "Make the background red" is not an answer to anything.

Under the enactive account this is not a convenience. Davis's studies of collaborative drawing have
the novice's *disruptions* acting as creative catalysts — contributions that open affordances the
expert would not have reached alone. An interruption is a contribution, so it belongs in the record
as one.

The mechanism is the SDK's own: a trigger is an async function that runs for the agent's lifetime and
may call `ctx.send(...)` at any point, which reaches the model as an `automated_trigger` input event.
This is a trigger that waits on a queue.
"""

from __future__ import annotations

import asyncio
from typing import Any, Callable

from .events import EventLog


class Interjections:
    """A queue the director writes to and a trigger the agent reads from."""

    # region Constructors
    def __init__(self, log: EventLog | None = None,
                 publish: Callable[[dict[str, Any]], None] | None = None) -> None:
        self.log = log
        self.publish = publish
        self.sent = 0
        self._queue: asyncio.Queue[str] = asyncio.Queue()
    # endregion

    # region Properties
    @property
    def waiting(self) -> int:
        """How many are queued but not yet delivered."""
        return self._queue.qsize()
    # endregion

    # region Methods
    def say(self, text: str) -> bool:
        """Queues one interjection. False if there was nothing to say.

        Recorded here rather than on delivery, because the record is of what the *director* did and
        when they did it. Whether the agent had picked it up yet is the agent's half of the story.
        """
        text = (text or "").strip()
        if not text:
            return False

        self._queue.put_nowait(text)
        self.sent += 1

        if self.log is not None:
            self.log.append("message", text=text[:2000], interjected=True)
        elif self.publish is not None:
            # No log yet — the run has not built its director. The watcher still hears it, and the
            # agent still receives it; only the durable record misses, which is worth saying.
            self.publish({"src": "director", "type": "message", "text": text[:2000],
                          "interjected": True, "unrecorded": True})

        return True
    def __deepcopy__(self, memo: dict[int, Any]) -> "Interjections":
        """A handle to something live, not a value: a copy of one must be the same object.

        The SDK deep-copies the whole `LocalAgentConfig` in `Agent.__init__`, and hooks and
        triggers are fields on it — so everything reachable from them is copied at startup.
        A `threading.Lock` cannot be, and the run died with `TypeError: cannot pickle
        '_thread.lock' object` before it had done anything. `EventLog` already carried this
        guard for the same reason; a copied channel would have its own queue, and what the director
        said would go into the one the agent is not reading.
        """
        memo[id(self)] = self
        return self

    def __copy__(self) -> "Interjections":
        return self

    def trigger(self) -> Callable:
        """The SDK trigger to register on the agent. One per run.

        Marked the way `@triggers.trigger` marks one, without importing the decorator: the SDK checks
        for an async function of one parameter carrying `__is_trigger__`, and matching that contract
        directly keeps this module importable without the SDK — which is what lets it be tested.
        """
        async def pump(context: Any) -> None:
            while True:
                text = await self._queue.get()
                try:
                    await context.send(text)
                except Exception:  # noqa: BLE001 — a failed delivery must not end the run
                    if self.publish is not None:
                        self.publish({"src": "director", "type": "message",
                                      "text": "an interjection could not be delivered",
                                      "failed": True})

        pump.__is_trigger__ = True
        return pump
    # endregion
