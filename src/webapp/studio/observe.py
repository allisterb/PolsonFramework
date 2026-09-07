"""Watching a run this process is not driving.

`Registry.start` holds a billed agent session open and streams what it produces. This does the other
half of the same job with none of the agent: a project is being worked in Claude Code or Claude
Desktop, and the studio page becomes a window onto the record rather than a way into the agent.

Nothing new has to be captured for that to work. The MCP server writes `server.jsonl` whoever is
driving it, so the spine — scripts, renders, stages, expectations, measurements — is already on disk;
`hostlog` transcribes the host's own conversation into `agent.jsonl` and `director.jsonl` from the
transcript the `preserve-chatlog` hook preserves. This puts a tailer on the first and a sync loop on
the second, and hands both to the broker the page already knows how to read.

**It is read-only, and that is the design rather than a limitation.** The host's interface is where
direction happens — that is what it is for, and it is already good at it. There is no answer channel
and no interjection channel here; a page offering them would be offering a second way in that reaches
nothing. What the page adds is the reading the host cannot give: the sense-making curve, the renders
beside the scripts that made them, and a record that survives the session.

Two things this has to get right, and both are ways of showing a run that is not the one on screen:

- **Which run.** A project's event files are appended to across every session it has ever had, so an
  observation with no cut shows the project's whole history as though it were happening now. Where
  that cut falls **depends on the SDK the project was generated for**, because the event that marks
  a run beginning is not the same one under every host — see `CUTS`.
- **Whether it is still going.** A driven run is live while its task is; an observed one has no task.
  Liveness here is the record still moving, which is a guess — a long thinking pause looks like an
  ended run — so it is deliberately generous, and being wrong shows a finished run as live rather
  than a live one as finished.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from orchestrator import project as project_mod
from orchestrator.broker import BACKLOG, HISTORY, Broker, Subscription
from orchestrator.events import merge
from orchestrator.hostlog import HostTranscript
from orchestrator.project import Project
from orchestrator.tail import Tailer

from .runs import Run, StudioError

#: How often to re-read the host transcript. The hook rewrites it once per agent turn, so anything
#: below a few seconds is polling a file that has not changed; anything above makes the conversation
#: visibly lag the renders it explains.
TRANSCRIPT_INTERVAL = 3.0

#: How long the record can be still before an observation stops calling itself live.
#:
#: Generous on purpose. A driven run knows it has ended because its own task finished; this can only
#: infer it from a file not moving, and an agent can easily spend two minutes thinking, reading its
#: own earlier passes, or waiting on the director. Erring long shows a finished run as live, which
#: costs a stale badge. Erring short shows a working agent as stopped, which is a lie the page tells
#: while the thing it describes is happening.
IDLE_AFTER = 180.0


#: Where each SDK's runs begin: the event that marks one, and the log it is written to.
#:
#: **`run.start` does not mean the same thing under every host, which is why this is a table rather
#: than a constant.** It is the MCP *server* starting, and how often that happens is the host's
#: decision, not the studio's. Claude Code and Antigravity spawn the server per session, so its
#: start is the run's start and the two are indistinguishable. ADK spawns it once for the whole app
#: process and then serves every invocation through it, so a project worked on twice through one
#: running app has two runs and **one** `run.start` — cutting there replays the earlier run as part
#: of the current one, and the page shows work the director did an hour ago as though it were
#: happening now. Measured on `tainted`: two `run.start` events an hour and a half apart, both
#: server boots, neither a run beginning.
#:
#: So ADK cuts on `run.begin`, which `adk_agent.transcript` writes per invocation — the ADK runtime
#: is the one that knows where its own runs begin, and it is the one that says so.
CUTS = {
    "adk": ("run.begin", "agent_events"),
}

#: For every other host, and for a project whose manifest names one we have no table entry for.
DEFAULT_CUT = ("run.start", "server_events")


def session_since(project: Project) -> str:
    """The timestamp the current run began at, in whatever the project's SDK means by that.

    Falls back to the default cut when the SDK's own marker is absent, and then to empty — meaning
    read everything, since a log carrying no start is one session rather than none, exactly as
    `RunReport.Sessions` treats it.

    **The fallback is a widening, never a narrowing**, which is what makes it safe to take
    silently: an ADK project recorded before the transcript plugin existed, or one where
    `make_plugin` returned None, has no `run.begin` at all, and `run.start` then scopes the page to
    the last app boot rather than to the project's whole history. Both are wider than the run in
    hand; neither can hide work that belongs to it.
    """
    kind, log = CUTS.get(project.sdk, DEFAULT_CUT)
    return _latest(project, kind, log) or (
        _latest(project, *DEFAULT_CUT) if (kind, log) != DEFAULT_CUT else "")


def _latest(project: Project, kind: str, log: str) -> str:
    """The timestamp of the last `kind` event in the named log, or empty."""
    latest = ""
    for event in merge(getattr(project, log)):
        if event.get("type") == kind and (ts := event.get("ts")):
            latest = ts
    return latest


class Observation:
    """The broker, the tailer and the transcript syncer for one watched project."""

    # region Constructors
    def __init__(self, project: Project, *, since: str = "",
                 interval: float = TRANSCRIPT_INTERVAL) -> None:
        self.project = project
        self.since = since
        self.interval = max(0.5, interval)
        self.broker = Broker(history=HISTORY, backlog=BACKLOG)
        self.transcript = HostTranscript(project, sink=self.broker.publish)
        self.tailer = Tailer(project.server_events, self.broker.publish)
        self._tail: asyncio.Task | None = None
        self._sync: asyncio.Task | None = None
        self._stopped = asyncio.Event()
    # endregion

    # region Methods
    def start(self) -> None:
        """Replays what is already recorded, then follows. Idempotent."""
        if self._tail is not None and not self._tail.done():
            return

        # Transcribed before the replay rather than after, so the conversation is already in the
        # files when they are read: publishing it afterwards would put every agent turn after every
        # render, and the page draws them in arrival order.
        self.transcript.sync()
        self._replay()

        # The tailer starts from the end because the replay above has already delivered everything on
        # disk. Skipping is what stops each render arriving twice — once from the replay and once
        # from the follow.
        self.tailer.skip_existing()
        self._tail = asyncio.create_task(self.tailer.run())
        self._sync = asyncio.create_task(self._resync())

    async def stop(self) -> None:
        """Stops following and ends every subscription."""
        self._stopped.set()
        self.tailer.stop()

        for task in (self._tail, self._sync):
            if task is None:
                continue
            try:
                await asyncio.wait_for(task, timeout=5.0)
            except (asyncio.TimeoutError, TimeoutError):
                task.cancel()
            except asyncio.CancelledError:
                pass

        self._tail = self._sync = None
        self.broker.close()

    def attach(self, *, replay: bool = True) -> Subscription:
        return self.broker.attach(replay=replay)

    def moved(self) -> float:
        """Seconds since anything in the record last changed, or a large number if nothing has."""
        newest = 0.0
        for path in (self.project.server_events, self.project.agent_events):
            try:
                newest = max(newest, path.stat().st_mtime)
            except OSError:
                continue

        if not newest:
            return float("inf")
        return max(0.0, datetime.now(timezone.utc).timestamp() - newest)
    # endregion

    # region Methods (private)
    def _replay(self) -> None:
        """Publishes the record so far, in the record's own order rather than in arrival order.

        A live run's broker fills up as events happen, so its history is already ordered. An
        observation starts against a file that is finished or half-finished, and the three logs
        interleave — `merge` is what puts a render back between the script that produced it and the
        measurement that checked it.
        """
        for event in merge(self.project.server_events,
                           self.project.agent_events,
                           self.project.director_events):
            if not self.since or (event.get("ts") or "") >= self.since:
                self.broker.publish(event)

    async def _resync(self) -> None:
        """Re-reads the host transcript until stopped. Never raises."""
        while not self._stopped.is_set():
            try:
                await asyncio.wait_for(self._stopped.wait(), timeout=self.interval)
                return
            except (asyncio.TimeoutError, TimeoutError):
                pass

            try:
                self.transcript.sync()
            except Exception as exc:  # noqa: BLE001 — a syncer that dies takes the page with it
                print(f"warning: syncing the host transcript failed: {exc}")
    # endregion


@dataclass
class ObservedRun(Run):
    """A run on the page that nothing in this process is running."""

    observation: Observation | None = field(default=None, repr=False)

    # region Properties
    @property
    def observed(self) -> bool:
        return True

    @property
    def live(self) -> bool:
        """Whether the record is still moving. An inference, not a fact — see `IDLE_AFTER`."""
        return self.observation is not None and self.observation.moved() < IDLE_AFTER
    # endregion

    # region Methods
    def attach(self, *, replay: bool = True) -> Any:
        if self.observation is None:
            raise StudioError("that observation has stopped")
        return self.observation.attach(replay=replay)

    def answer(self, question_id: str, **reply: Any) -> bool:
        """Always false. The host owns the conversation; this page only reads the record."""
        return False

    def say(self, text: str) -> bool:
        """Always false, for the same reason `answer` is."""
        return False

    def summary(self) -> dict[str, Any]:
        return {**super().summary(), "observed": True}
    # endregion


async def observe(registry: Any, root: Path) -> ObservedRun:
    """Registers an observation of the project at `root`, or returns the one already watching it.

    Reused rather than duplicated, because two observations of one project would put two tailers on
    one file and show the same run twice on the index. Nothing is spent either way, so this is
    neither refused when a run is going nor counted against the daily cap — those exist to bound what
    an agent costs, and an observation runs no agent.
    """
    try:
        project = project_mod.read(root)
    except project_mod.ProjectError as exc:
        raise StudioError(str(exc)) from exc

    for existing in registry.runs:
        if isinstance(existing, ObservedRun) and existing.project.root == project.root:
            return existing

    since = session_since(project)
    run = ObservedRun(
        id=_observation_id(project.id, registry),
        project=project,
        prompt="",
        stream=None,
        started=datetime.now(timezone.utc).isoformat(timespec="seconds"),
        since=since,
        status="watching",
    )
    run.observation = Observation(project, since=since)

    registry.register(run)
    run.observation.start()
    return run


def _observation_id(project_id: str, registry: Any) -> str:
    """Readable, and distinct from a driven run's id so the two are never confused in a URL."""
    taken = {r.id for r in registry.runs}
    for index in range(1, 1000):
        candidate = f"{project_id}-watch-{index}"
        if candidate not in taken:
            return candidate
    raise StudioError("too many observations of that project")
