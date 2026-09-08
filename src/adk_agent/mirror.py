"""Copies a project's work to object storage while the run is still happening.

**Why this exists, precisely.** On 2026-09-08 a Cloud Run instance serving a live run was replaced
mid-run — reason logged only as `AUTOSCALING`, with memory at 19% of 2 GiB, CPU under 4% and four SSE
connections against a concurrency limit of 30, so nothing the container did caused it. The agent kept
working on the draining instance for another three minutes and then was killed. Everything it had
made — `artifacts/`, `scripts/`, `events/`, `artwork.js` — was on that instance's filesystem, and the
filesystem does not outlive the instance. The run was unrecoverable. It was the second such loss that
day.

So the rule this module encodes is: **a container's disk is scratch space, and the work is not saved
until it is somewhere else.**

**It sweeps rather than hooking the writes, and that is the design decision worth defending.** The
obvious implementation intercepts each writer — `write_script` for authored files, the render events
for images. It does not work, because the biggest writer is not Python: `outFile` and `outSvg` are
handled inside the .NET engine, which writes to the project directory over the MCP boundary and tells
this process nothing except an event that names a path. A hook per writer would therefore mirror the
files we happened to think of and silently miss the rest, which is the failure mode of a backup
nobody tests. A sweep sees whatever is on disk, whoever put it there.

**Publishing is not what this is for.** This is durability: a private bucket, everything worth
keeping, continuously. Handing a judge a permalink to a finished piece is a different job with a
different bucket, a curated subset and a single moment — see `gs://polson-published`. Conflating them
is how `documents/` ends up on the public internet.

Configure with `POLSON_MIRROR_URI`; absent, this is inert and every call is a no-op, which is what a
local checkout wants.
"""

from __future__ import annotations

import asyncio
import logging
import os
from pathlib import Path
from urllib.parse import urlparse

_logger = logging.getLogger("polson.mirror")

#: Where the copy goes: `gs://bucket` or `gs://bucket/prefix`. Empty disables the whole module.
#:
#: Deliberately its own variable rather than reusing `POLSON_ARTIFACT_SERVICE_URI`. That one is
#: ADK's versioned store for the blobs the *model* is shown, with ADK's own key layout; this is the
#: project directory a *person* opens. They can share a bucket and must not share a prefix.
MIRROR_URI = os.environ.get("POLSON_MIRROR_URI", "").strip()

#: How often the sweep runs. The cost of a longer interval is measured in lost work, and the cost of
#: a shorter one is a directory walk over a few dozen files — so this is biased low.
try:
    SWEEP_SECONDS = max(5.0, float(os.environ.get("POLSON_MIRROR_SECONDS", "20")))
except ValueError:
    SWEEP_SECONDS = 20.0

#: Directories copied whole, relative to the project root.
#:
#: `events/` is the run record the studio replays, `scripts/` is what actually ran, `artifacts/` is
#: the work. `.polson/research/` is here because a research run costs one of a run's two allowances
#: and is what makes a drawn figure checkable afterwards — losing it turns a sourced number back into
#: an assertion.
MIRRORED_DIRS = ("artifacts", "scripts", "events", ".polson/research")

#: Files at the project root, by exact name.
#:
#: **An allowlist, so a file appearing later is not copied by default.** `.agents/` carries MCP
#: command lines built for the machine that generated them, and `agent.config.json` is the
#: Antigravity tool policy; neither is read by this runtime, and a restored copy of the first would
#: point at paths that do not exist here.
#:
#: **`GEMINI.md` is copied, and excluding it was a mistake worth recording.** The comment here used
#: to justify leaving it out on the grounds that it is ~75 KB "identical in every project", so
#: copying it would be most of the traffic for none of the value. Both halves were wrong: across
#: forty archived projects it is **forty distinct files**, because it is generated per workflow and
#: per brief — and the sweep only sends what changed, so a file written once at project creation is
#: uploaded once and never again. The cost was a single upload; the price was that every recovered
#: project was missing the one file ADK reads as the agent's instructions, which is the difference
#: between a run that can be continued and a run that can only be read.
#:
#: Related to `studio.app.DELIVERABLES` but deliberately not shared with it. That list answers "what
#: may be served over HTTP" — and `GEMINI.md` must never be, it is the studio's own instructions.
#: This answers "what is needed to have this run again", which is a different question.
MIRRORED_FILES = (
    "brief.md", "artwork.js", "accuracy.md", "findings.md",
    "critique_log.md", "materials.md", "turns.md", "project.json",
    "GEMINI.md", "CLAUDE.md",
)

#: Never copied, at any depth. `documents/` is the director's own supplied material — a client's
#: unpublished figures, whatever was attached to the brief — and it is not ours to put anywhere.
#: The allowlists above already exclude it; this is the second lock, so that widening one of them
#: later cannot quietly reach it.
NEVER = ("documents",)

#: A guard against copying something absurd, not a policy. The largest artifact measured in a real
#: run is a little under 1 MB.
MAX_BYTES = 32 * 1024 * 1024


# region Public
def configured() -> bool:
    """Whether a destination is set. Everything here is a no-op when it is not."""
    return bool(MIRROR_URI)


def make_plugin(project_dir: str | Path):
    """A plugin that mirrors this project for as long as a run is in flight, or None.

    Returns None rather than raising, for the reason `transcript.make_plugin` does: an app that will
    not start because a *backup* could not be constructed is a worse outcome than one whose work is
    merely unprotected.
    """
    if not configured():
        return None

    try:
        from google.adk.plugins.base_plugin import BasePlugin
    except Exception as exc:                                    # pragma: no cover - import guard
        _logger.warning("polson mirror: not available (%s)", exc)
        return None

    try:
        mirror = ProjectMirror(project_dir)
    except Exception as exc:
        _logger.warning("polson mirror: not started (%s)", exc)
        return None

    class MirrorPlugin(BasePlugin):
        """Starts the sweep when a run begins and takes a final copy when it ends."""

        def __init__(self) -> None:
            super().__init__(name="polson_mirror")

        async def before_run_callback(self, *, invocation_context):
            mirror.start()
            return None

        async def after_run_callback(self, *, invocation_context):
            await mirror.finish()
            return None

        async def on_run_error_callback(self, *, invocation_context, error):
            # **The path that matters most.** A run that failed is exactly the one whose partial work
            # someone will want to look at, and ADK skips `after_run_callback` entirely here.
            await mirror.finish()
            return None

    _logger.info("polson mirror: %s every %.0fs -> %s", Path(project_dir).name,
                 SWEEP_SECONDS, MIRROR_URI)
    return MirrorPlugin()
# endregion


class ProjectMirror:
    """Copies changed files under `project_dir` to object storage, on a timer."""

    # region Constructors
    def __init__(self, project_dir: str | Path, uri: str | None = None,
                 interval: float | None = None) -> None:
        from google.cloud import storage                        # deferred: optional dependency

        self.root = Path(project_dir).resolve()
        self.interval = SWEEP_SECONDS if interval is None else interval

        parsed = urlparse(uri or MIRROR_URI)
        if parsed.scheme != "gs" or not parsed.netloc:
            raise ValueError(f"mirror URI must be gs://bucket[/prefix], not {uri or MIRROR_URI!r}")

        self.bucket = storage.Client().bucket(parsed.netloc)
        base = parsed.path.strip("/")
        self.prefix = f"{base}/{self.root.name}" if base else self.root.name

        #: What has already been copied, as (size, mtime_ns) per relative path. Compared rather than
        #: hashed: a hash means reading every file every sweep, and the pair catches every write the
        #: engine or the agent actually makes.
        self._sent: dict[str, tuple[int, int]] = {}
        self._task: asyncio.Task | None = None
    # endregion

    # region Methods
    def start(self) -> None:
        """Begins sweeping, if it is not already."""
        if self._task is not None and not self._task.done():
            return
        try:
            self._task = asyncio.get_running_loop().create_task(self._loop())
        except RuntimeError:                                    # pragma: no cover - no loop
            _logger.debug("polson mirror: no running loop; sweep not started")

    async def finish(self) -> None:
        """Stops sweeping and takes one last copy.

        The final sweep happens **after** the timer is cancelled and is awaited rather than
        scheduled, so a run that ends between ticks still lands whole.
        """
        task, self._task = self._task, None
        if task is not None:
            task.cancel()
            try:
                await task
            except (asyncio.CancelledError, Exception):         # noqa: B014 - never propagate
                pass
        await self.sweep()

    async def sweep(self) -> int:
        """Copies everything that has changed. Returns how many files were sent.

        Never raises. A mirror that can take a run down with it is worse than no mirror, and the
        thing it is protecting against is precisely the moment when infrastructure is misbehaving.
        """
        try:
            pending = [(rel, path) for rel, path in self._walk()
                       if self._changed(rel, path)]
        except Exception as exc:
            _logger.debug("polson mirror: walk failed (%s)", exc)
            return 0

        sent = 0
        for rel, path in pending:
            try:
                stat = path.stat()
                # Off the event loop: the storage client is blocking, and a blocking call here stalls
                # every request this container is serving — including the health probe.
                await asyncio.to_thread(self._upload, rel, path)
                self._sent[rel] = (stat.st_size, stat.st_mtime_ns)
                sent += 1
            except Exception as exc:
                # Left out of `_sent`, so the next sweep tries it again.
                _logger.debug("polson mirror: %s not copied (%s)", rel, exc)

        if sent:
            _logger.info("polson mirror: %d file(s) -> gs://%s/%s", sent, self.bucket.name,
                         self.prefix)
        return sent
    # endregion

    # region Implementation
    async def _loop(self) -> None:
        while True:
            await asyncio.sleep(self.interval)
            await self.sweep()

    def _walk(self):
        """Every file worth copying, as (relative posix path, absolute path)."""
        for name in MIRRORED_FILES:
            path = self.root / name
            if path.is_file():
                yield name, path

        for folder in MIRRORED_DIRS:
            base = self.root / folder
            if not base.is_dir():
                continue
            for path in sorted(base.rglob("*")):
                if not path.is_file():
                    continue
                rel = path.relative_to(self.root).as_posix()
                # The second lock. `documents/` is not under any mirrored directory, so this only
                # fires if one of the lists above is widened later.
                if any(part in NEVER for part in rel.split("/")):
                    continue
                yield rel, path

    def _changed(self, rel: str, path: Path) -> bool:
        try:
            stat = path.stat()
        except OSError:
            return False
        if stat.st_size > MAX_BYTES:
            _logger.debug("polson mirror: %s skipped, %d bytes", rel, stat.st_size)
            return False
        return self._sent.get(rel) != (stat.st_size, stat.st_mtime_ns)

    def _upload(self, rel: str, path: Path) -> None:
        self.bucket.blob(f"{self.prefix}/{rel}").upload_from_filename(str(path))
    # endregion
