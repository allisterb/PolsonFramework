"""Following an event file that another process is writing.

`agent.jsonl` and `director.jsonl` are ours, written in-process, so they reach a watcher directly.
`server.jsonl` is not: the .NET MCP server owns it, and it is where `render` events live — which is
to say it is where the *pictures* are announced. Nothing on the Python side learns that an artifact
exists except by reading that file.

So this is the one place the web app crosses a process boundary, and it crosses it by reading rather
than by being told. The alternative — the server posting to an HTTP endpoint — buys latency we do
not need (renders are seconds apart) and costs the two properties that matter: a late watcher has
nothing to replay, and a dropped message leaves the page disagreeing with the file that
`polson report` reconciles against.

Reading is cheap here because of how the server writes. `RunEventLog.Append` calls
`File.AppendAllText` under a lock — open, append, close — so no handle is held, there is no sharing
mode to lose to, and a reader can open the file freely between writes.

Three things this has to get right, each of which is a way to silently report a run that never
happened:

- **Only whole lines.** A read can land mid-append, so the offset advances to the last `\\n` and the
  partial tail is left for the next pass.
- **Truncation.** `polson create-project --reset` deletes `events/`. A file shorter than the offset
  is a new file, not a corrupt one: start over rather than seeking past its end.
- **Identity is `(src, seq)`, not the byte offset.** The offset is an implementation detail of one
  reader; the sequence is what the record itself guarantees.
"""

from __future__ import annotations

import asyncio
import json
import sys
from pathlib import Path
from typing import Any, Callable

#: How often to look. Renders are seconds apart, so this is far below anything a person perceives,
#: and a stat() on an unchanged file is close to free.
INTERVAL = 0.15


class Tailer:
    """Follows one JSONL file, handing each complete line to a sink. One per file, not per watcher."""

    # region Constructors
    def __init__(self, path: Path | str, sink: Callable[[dict[str, Any]], None], *,
                 interval: float = INTERVAL, from_start: bool = True) -> None:
        self.path = Path(path)
        self.sink = sink
        self.interval = max(0.01, interval)

        # From the start by default, because a run being watched from the browser has usually already
        # written a few lines by the time anyone attaches, and the point of the file is that those
        # lines are not lost.
        self._offset = 0 if from_start else -1
        self._stopped = asyncio.Event()
        self._lines = 0
        self._resets = 0
    # endregion

    # region Properties
    @property
    def lines(self) -> int:
        """Complete lines handed to the sink."""
        return self._lines

    @property
    def resets(self) -> int:
        """How many times the file was truncated or replaced under us."""
        return self._resets
    # endregion

    # region Methods
    async def run(self) -> None:
        """Follows the file until `stop()`. Never raises; a tailer that dies takes the page with it."""
        while not self._stopped.is_set():
            try:
                self.poll()
            except Exception as exc:  # noqa: BLE001 — see the docstring
                print(f"warning: tailing {self.path} failed: {exc}", file=sys.stderr)

            try:
                await asyncio.wait_for(self._stopped.wait(), timeout=self.interval)
            except (asyncio.TimeoutError, TimeoutError):
                continue

        # One last pass, so the events written between the final poll and the run ending are not
        # lost. This is the common case rather than a rare one: the server writes `run.end` as it
        # shuts down, which is exactly when the orchestrator is stopping the tailer.
        try:
            self.poll()
        except Exception as exc:  # noqa: BLE001
            print(f"warning: final read of {self.path} failed: {exc}", file=sys.stderr)

    def poll(self) -> int:
        """Reads whatever whole lines have appeared since the last call. Returns how many."""
        if not self.path.exists():
            # Not an error: the server creates the file when it writes its first event, so a run
            # being watched from the moment it starts will find nothing here for a second or two.
            return 0

        size = self.path.stat().st_size

        if self._offset < 0:
            self._offset = size
            return 0

        if size < self._offset:
            # Shorter than we have already read: a reset, not corruption. Everything after this is a
            # new run, and re-reading from zero is the correct reading of it.
            self._offset = 0
            self._resets += 1

        if size == self._offset:
            return 0

        with open(self.path, "rb") as handle:
            handle.seek(self._offset)
            chunk = handle.read(size - self._offset)

        cut = chunk.rfind(b"\n")
        if cut < 0:
            # A partial line and nothing else. Leave the offset where it is and try again.
            return 0

        self._offset += cut + 1

        delivered = 0
        for raw in chunk[:cut].split(b"\n"):
            if not raw.strip():
                continue
            if (event := _parse(raw)) is not None:
                self._lines += 1
                delivered += 1
                self.sink(event)

        return delivered

    def stop(self) -> None:
        """Asks the loop in `run()` to finish, after one final read."""
        self._stopped.set()
    # endregion


def _parse(raw: bytes) -> dict[str, Any] | None:
    """One line to an event, or None if it cannot be read as one.

    Unparseable lines are skipped rather than raised on, matching `read_events`: a truncated or
    garbled line means the writer died mid-append, and it is not worth failing every later event
    over.
    """
    try:
        event = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None
    return event if isinstance(event, dict) else None
