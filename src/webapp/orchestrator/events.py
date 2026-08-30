"""Append-only JSONL event records.

The filesystem is the record, not the transport (`docs/project-layout.md`). This is the Python half
of that contract; `RunEventLog` in `Polson.MCPServer` is the .NET half, and the two must produce
lines a single reader can merge without special-casing either. Same timestamp format, same key
order, same `\\n` line ending on every platform.

**One writer per file is the whole concurrency model.** Nothing merges on disk; readers merge on
read, ordering by `ts` and tie-breaking on `(src, seq)`. So an `EventLog` owns its file: the
orchestrator writes `agent.jsonl` and `director.jsonl`, the MCP server writes `server.jsonl`, and
none of them ever touches another's.

Every failure is swallowed, for the same reason the .NET side swallows them: a run that dies because
its diary could not be written is a worse outcome than a run with no diary.
"""

from __future__ import annotations

import json
import sys
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Iterator


def timestamp() -> str:
    """Millisecond precision, sortable, unambiguous — matching the .NET writer exactly."""
    now = datetime.now(timezone.utc)
    return f"{now.strftime('%Y-%m-%dT%H:%M:%S')}.{now.microsecond // 1000:03d}Z"


def read_events(path: Path) -> list[dict[str, Any]]:
    """Reads one event file, skipping anything unparseable.

    A truncated last line is normal — it means the writer died mid-append — and is not worth
    failing a reader over.
    """
    if not path.exists():
        return []

    events = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            events.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return events


def merge(*paths: Path) -> list[dict[str, Any]]:
    """Merges several event files into one ordered reading, which is how the record is meant to be read."""
    events = [e for path in paths for e in read_events(path)]
    return sorted(events, key=lambda e: (e.get("ts", ""), e.get("src", ""), e.get("seq", 0)))


class EventLog:
    """Writes one append-only JSONL file. One instance per file, one writer per file."""

    def __init__(self, path: Path, src: str, sink: Callable[[dict[str, Any]], None] | None = None) -> None:
        self.path = Path(path)
        self.src = src

        # Where the same event goes for anyone watching now, if anyone is. The file is the record and
        # the sink is not: it is bounded, in memory, and forgets. Nothing here waits on it, and a sink
        # that raises loses its own event rather than the written one — the line is already on disk by
        # the time it is called.
        self.sink = sink

        self._seq: int | None = None
        self._disabled = False
        self._lock = threading.Lock()

    @property
    def enabled(self) -> bool:
        return not self._disabled

    def append(self, kind: str, *, stage: str | None = None, execution: str | None = None, **fields: Any) -> None:
        """Appends one event. Never raises."""
        if self._disabled:
            return

        try:
            with self._lock:
                self.path.parent.mkdir(parents=True, exist_ok=True)

                # Seeded from what is already on disk so a restart continues the sequence rather than
                # repeating it, which would make (src, seq) useless as a tie-break.
                if self._seq is None:
                    self._seq = len(read_events(self.path))
                self._seq += 1

                event: dict[str, Any] = {
                    "ts": timestamp(),
                    "seq": self._seq,
                    "src": self.src,
                    "type": kind,
                }

                # Ahead of the payload so a reader can filter on them without parsing the whole line.
                if stage:
                    event["stage"] = stage
                if execution:
                    event["execution"] = execution

                # Absent rather than null: a reader checks for the key's presence either way, and a
                # line full of nulls is unreadable.
                event.update({k: v for k, v in fields.items() if v is not None})

                # Byte-for-byte the shape the .NET writer produces: compact separators, no ASCII
                # escaping, and newline="" so the line ends "\n" on Windows too. The record is meant
                # to be read by people as well as parsed, and one grep should span all three files —
                # `"type":"render"` must match whoever wrote the line.
                with open(self.path, "a", encoding="utf-8", newline="") as handle:
                    handle.write(json.dumps(event, ensure_ascii=False, separators=(",", ":")) + "\n")

        except Exception as exc:  # noqa: BLE001 — see the module docstring
            self._disabled = True
            print(f"warning: event log {self.path} disabled after a write failure: {exc}", file=sys.stderr)
            return

        # Outside the lock, and after the write: a watcher never delays a writer, and never sees an
        # event that did not reach the disk.
        if self.sink is not None:
            try:
                self.sink(event)
            except Exception as exc:  # noqa: BLE001 — a watcher must not break the record
                print(f"warning: event sink for {self.path} failed: {exc}", file=sys.stderr)

    def __iter__(self) -> Iterator[dict[str, Any]]:
        return iter(read_events(self.path))

    def __deepcopy__(self, memo: dict[int, Any]) -> "EventLog":
        """A log is a handle to a file, not a value: a copy of one must be the same writer.

        Copying it would produce a second writer on one file with its own sequence counter, which is
        precisely what the one-writer rule exists to prevent. It also keeps an `EventLog` safe to
        reach through a hook handed to the SDK: the agent deep-copies its configuration on startup,
        and a `threading.Lock` cannot be copied — a run failed with `cannot pickle '_thread.lock'`
        before this existed, at agent startup rather than anywhere near here.
        """
        memo[id(self)] = self
        return self

    def __copy__(self) -> "EventLog":
        return self
