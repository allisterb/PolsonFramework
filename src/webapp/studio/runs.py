"""The runs a server is holding, and the one it is running.

Milestone 6's web layer over Milestone 5. Nothing here reimplements a run: `run_turn` still owns the
agent, the tool policy and the record, and this owns only the part a web server needs — knowing which
runs exist, which one is live, and how to reach its stream.

**One run at a time, deliberately.** Each run is a billed agent session held open in this process, so
concurrency is a cost surface before it is an engineering problem (`CLAUDE.md` §4, Milestone 6, task
4). A second start is refused with a message rather than queued, because a queue would let a page
that looks idle be spending money.

**A run outlives the request that started it.** It is a background task; the POST returns as soon as
the run is registered. A run that died with its request would end every time a visitor refreshed.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from orchestrator import project as project_mod
from orchestrator import run as run_mod
from orchestrator.broker import Subscription
from orchestrator.events import EventLog
from orchestrator.project import Project
from orchestrator.watch import RunStream

#: How long a turn may take before it is abandoned. Generous: a full infographic is several minutes.
TURN_TIMEOUT = 1800.0

#: The per-day ceiling on runs, which is the crudest possible spend cap and the one that cannot be
#: argued with. The asset budget inside the engine is the fine-grained one.
DAILY_RUNS = 24


class StudioError(Exception):
    """Something a visitor is allowed to be told, in words they can act on."""


@dataclass
class Run:
    """One turn, from the moment it is registered to whatever it leaves behind."""

    id: str
    project: Project
    prompt: str
    stream: RunStream
    started: str
    status: str = "starting"
    error: str | None = None
    result: run_mod.RunResult | None = None
    task: asyncio.Task | None = field(default=None, repr=False)

    # region Properties
    @property
    def live(self) -> bool:
        return self.task is not None and not self.task.done()

    @property
    def artifacts(self) -> Path:
        return self.project.root / "artifacts"
    # endregion

    # region Methods
    def attach(self, *, replay: bool = True) -> Subscription:
        """A watcher's position in this run's stream. Replay is what makes a refresh survivable."""
        return self.stream.attach(replay=replay)

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "project": self.project.id,
            "root": str(self.project.root),
            "workflow": self.project.workflow,
            "started": self.started,
            "status": self.status,
            "live": self.live,
            "error": self.error,
            "reply": (self.result.reply if self.result else "") or "",
        }
    # endregion


class Registry:
    """Every run this server has held, and the at most one it is running."""

    # region Constructors
    def __init__(self, *, timeout: float = TURN_TIMEOUT, daily: int = DAILY_RUNS) -> None:
        self.timeout = timeout
        self.daily = daily
        self._runs: dict[str, Run] = {}
        self._today: list[str] = []
    # endregion

    # region Properties
    @property
    def active(self) -> Run | None:
        return next((r for r in self._runs.values() if r.live), None)

    @property
    def runs(self) -> list[Run]:
        """Newest first, which is the order a page wants them."""
        return sorted(self._runs.values(), key=lambda r: r.started, reverse=True)
    # endregion

    # region Methods
    def get(self, run_id: str) -> Run | None:
        return self._runs.get(run_id)

    async def start(self, root: Path, prompt: str) -> Run:
        """Loads a project, registers a run, and starts it. Raises `StudioError` with a reason."""
        if (busy := self.active) is not None:
            raise StudioError(
                f"A run is already going ({busy.project.id}). One at a time — each is a live agent "
                f"session, and two would be spending twice.")

        if len(self._today) >= self.daily:
            raise StudioError(
                f"That is {self.daily} runs today, which is the daily cap. It exists because a public "
                f"URL driving a metered service is an open cost surface.")

        try:
            project = project_mod.load(root)
        except project_mod.ProjectError as exc:
            raise StudioError(str(exc)) from exc

        run = Run(
            id=_run_id(project.id, len(self._runs)),
            project=project,
            prompt=prompt,
            stream=RunStream(project),
            started=datetime.now(timezone.utc).isoformat(timespec="seconds"),
        )

        self._runs[run.id] = run
        self._today.append(run.id)

        run.stream.start()
        run.status = "running"
        run.task = asyncio.create_task(self._drive(run))
        return run
    # endregion

    # region Methods (private)
    async def _drive(self, run: Run) -> None:
        """Runs the turn and closes the stream, whatever happens.

        Every failure lands on the run rather than in a traceback nobody sees: a visitor watching a
        page needs to be told the run stopped, and why, far more than the server needs to raise.
        """
        try:
            run.result = await run_mod.run_turn(
                run.project,
                run.prompt,
                timeout=self.timeout,
                echo=False,
                sink=run.stream.sink,

                # A factory, not a director: run_turn owns director.jsonl, and a second writer on it
                # would break the one-writer rule the whole record depends on.
                director_factory=run.stream.director,
            )
            run.status = "done" if run.result.completed else "incomplete"
            run.error = run.result.error
        except asyncio.CancelledError:
            run.status = "cancelled"
            raise
        except Exception as exc:  # noqa: BLE001 — reported to the page, not swallowed
            run.status = "failed"
            run.error = f"{type(exc).__name__}: {exc}"
        finally:
            # After the turn, so the tailer's last read catches the server's own run.end. Closing
            # first would lose the end of every run to its own teardown.
            await run.stream.stop()
    # endregion


def _run_id(project_id: str, index: int) -> str:
    """Readable and unique within a server's life. Not a secret and not persisted."""
    return f"{project_id}-{index + 1}"
