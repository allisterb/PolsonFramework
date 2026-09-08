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
from typing import Any, TYPE_CHECKING

from orchestrator import project as project_mod
from orchestrator.broker import Subscription

if TYPE_CHECKING:                                  # pragma: no cover - types only
    from orchestrator import run as run_mod
from orchestrator.events import EventLog, timestamp
from orchestrator.project import Project
from orchestrator.watch import RunStream

#: How long a turn may take before it is abandoned. Generous: a full infographic is several minutes.
TURN_TIMEOUT = 1800.0

#: The per-day ceiling on runs, which is the crudest possible spend cap and the one that cannot be
#: argued with. The asset budget inside the engine is the fine-grained one.
DAILY_RUNS = 24

#: What starts an agent that has already been told everything else in its own instructions. Used only
#: for a project that has never run — see `Registry.start`, which is the one place that decides
#: whether this is the right thing to say.
DEFAULT_PROMPT = (
    "Read GEMINI.md in this project directory, then brief.md, and begin the project. "
    "Work through the stages it names, declaring each one with Stage.begin, and save every render "
    "to artifacts/ with outFile."
)


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

    #: Where this run begins in the project's event files, in the record's own timestamp format.
    #: The files are appended to across runs, so this is what separates one run's reading of them
    #: from the project's whole history — the same cut the tailer makes with a byte offset, in the
    #: one form that works across all three files. Empty means read everything.
    since: str = ""
    status: str = "starting"
    resumed: bool = False
    error: str | None = None
    result: run_mod.RunResult | None = None
    task: asyncio.Task | None = field(default=None, repr=False)

    # region Properties
    @property
    def observed(self) -> bool:
        """Whether this run is being watched rather than driven. See `studio.observe`.

        On the base class because two callers need the answer without knowing the subclass: `active`,
        which must not let a free observation block a paid run, and the page, which must not offer a
        direction channel that reaches nothing.
        """
        return False

    @property
    def live(self) -> bool:
        return self.task is not None and not self.task.done()

    @property
    def artifacts(self) -> Path:
        return self.project.root / "artifacts"

    @property
    def scripts(self) -> Path:
        """Where the engine saved every script it executed, named as the record names them."""
        return self.project.root / "scripts"
    # endregion

    # region Methods
    def attach(self, *, replay: bool = True) -> Subscription:
        """A watcher's position in this run's stream. Replay is what makes a refresh survivable."""
        return self.stream.attach(replay=replay)

    def answer(self, question_id: str, **reply: Any) -> bool:
        """Answers a question the agent asked. False if it is unknown or already settled."""
        return self.stream.answer(question_id, **reply)

    def say(self, text: str) -> bool:
        """Interrupts. False if there was nothing to say."""
        return self.stream.say(text)

    def summary(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "project": self.project.id,
            "root": str(self.project.root),
            "workflow": self.project.workflow,
            "started": self.started,
            "status": self.status,
            "resumed": self.resumed,
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
        """The one run being driven, if any.

        Observations are excluded deliberately. This is what `start` refuses on, and refusing exists
        because a second *agent session* would be spending twice — watching a project costs nothing,
        so an observation left open must not make the studio look busy and lock out every real run.
        """
        return next((r for r in self._runs.values() if r.live and not r.observed), None)

    @property
    def runs(self) -> list[Run]:
        """Newest first, which is the order a page wants them."""
        return sorted(self._runs.values(), key=lambda r: r.started, reverse=True)
    # endregion

    # region Methods
    def get(self, run_id: str) -> Run | None:
        return self._runs.get(run_id)

    def register(self, run: Run) -> Run:
        """Adds a run this registry did not start.

        The one caller is `studio.observe`, which builds a run around a project someone else is
        driving. Deliberately not counted against the daily cap: that cap bounds what agents cost,
        and an observation runs none.
        """
        self._runs[run.id] = run
        return run

    async def start(self, root: Path, prompt: str, *, resume: bool = True) -> Run:
        """Loads a project, registers a run, and starts it. Raises `StudioError` with a reason.

        `resume` continues the project's recorded conversation, which is the default because carrying
        on with a piece is the ordinary case — the agent keeps what it decided and why. It is also
        the thing that quietly wastes a turn: resuming a *finished* project with the opening prompt
        gets "the project has completed all 7 stages", one tool call, and no work, because the agent
        is right. A resumed run needs something new to do, and the caller is made to supply it.
        """
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

        # One place decides what an empty prompt means, because it means opposite things. A project
        # that has never run wants the opening instruction; one that is being continued wants
        # something new, and giving it the opening instruction spends a whole session being told the
        # work is finished.
        resuming = bool(resume and project.conversation_id)
        prompt = prompt.strip()

        if resuming and not prompt:
            raise StudioError(
                "That project already has a session, so this run would continue it — and continuing "
                "with 'begin the project' gets 'it is already done'. Say what you want changed or "
                "added, or start a fresh session instead.")

        if not prompt:
            prompt = DEFAULT_PROMPT

        # Stamped before the stream starts, so the cut is never later than the tailer's byte offset.
        # Erring that way includes an event or two the trace might miss; erring the other way would
        # drop this run's own opening events out of its curve.
        run = Run(
            id=_run_id(project.id, len(self._runs)),
            project=project,
            prompt=prompt,
            stream=RunStream(project),
            started=datetime.now(timezone.utc).isoformat(timespec="seconds"),
            since=timestamp(),
            resumed=resuming,
        )

        self._runs[run.id] = run
        self._today.append(run.id)

        run.stream.start()
        run.status = "running"
        run.task = asyncio.create_task(self._drive(run, resume=resume))
        return run
    # endregion

    # region Methods (private)
    async def _drive(self, run: Run, *, resume: bool = True) -> None:
        """Runs the turn and closes the stream, whatever happens.

        Every failure lands on the run rather than in a traceback nobody sees: a visitor watching a
        page needs to be told the run stopped, and why, far more than the server needs to raise.
        """
        # Imported here rather than at module scope, so that *observing* a run does not require the
        # driver. `orchestrator.run` pulls in the Antigravity SDK, which the ADK runtime deliberately
        # does not ship — `src/adk_agent/requirements.in` says why — and without this the studio's
        # read-only pages could not be served from there at all. Starting a run still needs the SDK,
        # and still fails here if it is absent, which is correct.
        from orchestrator import run as run_mod

        try:
            run.result = await run_mod.run_turn(
                run.project,
                run.prompt,
                timeout=self.timeout,
                echo=False,
                resume=resume,
                sink=run.stream.sink,

                # A factory, not a director: run_turn owns director.jsonl, and a second writer on it
                # would break the one-writer rule the whole record depends on.
                director_factory=run.stream.build_director,

                # The channel for an interruption. Registered up front because a trigger is started
                # with the agent; there is no way to add one to a turn already under way.
                triggers=run.stream.triggers,
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
