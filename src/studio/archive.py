"""Bringing a vanished run back from object storage.

The read half of `adk_agent/mirror.py`. That module copies a project out of the container while the
run is happening, because a Cloud Run instance can be replaced mid-run and take the whole project
directory with it — twice on 2026-09-08, the second time twenty minutes into a live run. This is what
makes the copy worth having: without a way back, the work is preserved and unreachable, which is only
marginally better than losing it.

**It restores to local disk rather than serving out of the bucket, and that is the whole design.**
The alternative — teaching every read path to fall back to GCS — means changing `contain`,
`FileResponse`, the three event tailers, `discover` and the curve endpoint, each of which would then
have two ways to be wrong. Downloading the project once puts it exactly where every existing route
already looks, so the run page, the artifact route, the script view and the deliverables listing all
work unchanged and none of them needs to know this module exists.

That the restored copy lands on the same ephemeral disk is not a flaw. It is a **cache**: if the
instance is replaced again, the archive is still the archive and the next visitor restores it again.
What must never be true is that the *only* copy is on the container, and after `mirror.py` that is no
longer true.

**A restored run is a record, not a runnable project.** It arrives without `GEMINI.md`, `.agents/` or
`agent.config.json`, because the mirror deliberately does not copy them — so it can be read, replayed
and observed, and it cannot be driven. That is the correct shape for a finished run and it is not
worth pretending otherwise.

Configured by `POLSON_MIRROR_URI` — deliberately the *same* variable the mirror writes with. Two
variables that must agree is a defect waiting for the day they do not.
"""

from __future__ import annotations

import logging
import os
import re
from pathlib import Path
from urllib.parse import urlparse

_logger = logging.getLogger("polson.archive")

#: Where the mirror put things. Empty disables every function here, which is what a local checkout
#: with no bucket wants — `available()` is then False and the pages simply do not offer restoring.
ARCHIVE_URI = os.environ.get("POLSON_MIRROR_URI", "").strip()

#: A project name, as it may appear in a URL and as a directory. Deliberately far narrower than what
#: a filesystem accepts: this string arrives from a blob listing and becomes a path, so it is checked
#: against a pattern rather than merely scanned for `..`.
SAFE_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")

#: Ceilings on a restore, so one archived project cannot fill the container's disk. A measured run's
#: whole directory is a few MB across a few dozen files; these are three orders of magnitude clear.
MAX_FILES = 2000
MAX_TOTAL_BYTES = 512 * 1024 * 1024


class ArchiveError(Exception):
    """A restore that could not be done, with the reason a reader needs."""


# region Public
def available() -> bool:
    """Whether an archive is configured *and* reachable.

    Both halves matter and they fail differently: no URI is an ordinary local checkout, while a URI
    whose client will not construct is a misconfiguration worth a log line. Neither raises — a studio
    that will not start because an *archive* is unreachable is the wrong trade, the same call
    `mirror.make_plugin` makes.
    """
    if not ARCHIVE_URI:
        return False
    try:
        _bucket()
        return True
    except Exception as exc:
        _logger.warning("polson archive: %s unusable (%s)", ARCHIVE_URI, exc)
        return False


def names() -> list[str]:
    """Every project in the archive, sorted. Empty when there is no archive or it cannot be read.

    A listing, not a restore: this is what the index page needs to *offer* a vanished run, and it
    costs one request rather than a download.
    """
    try:
        bucket, prefix = _bucket()
    except Exception as exc:
        _logger.debug("polson archive: cannot list (%s)", exc)
        return []

    base = f"{prefix}/" if prefix else ""
    try:
        found = bucket.client.list_blobs(bucket, prefix=base, delimiter="/")
        # `prefixes` is only populated once the iterator has been consumed, which is the part of this
        # API most easily got wrong: reading it first returns an empty set and looks like an empty
        # archive.
        for _ in found:
            pass
        folders = found.prefixes
    except Exception as exc:
        _logger.debug("polson archive: listing failed (%s)", exc)
        return []

    out = []
    for folder in folders:
        name = folder[len(base):].strip("/")
        if SAFE_NAME.match(name):
            out.append(name)
        else:
            # Not an error to investigate so much as a thing never to open. Logged, not raised.
            _logger.warning("polson archive: ignoring %r, not a safe project name", name)
    return sorted(out)


def restore(name: str, into: Path) -> Path:
    """Downloads the archived project `name` into `into/name`, and returns that directory.

    Raises `ArchiveError` rather than returning a sentinel, because every caller has something
    specific to say to a visitor and none of them can do anything useful with a None.
    """
    if not SAFE_NAME.match(name or ""):
        raise ArchiveError(f"that is not a project name: {name!r}")

    bucket, prefix = _bucket()
    root = Path(into).resolve()
    target = (root / name).resolve()
    if target.parent != root:
        raise ArchiveError(f"that path is outside {root.name}: {name}")

    base = f"{prefix}/{name}/" if prefix else f"{name}/"
    blobs = list(bucket.client.list_blobs(bucket, prefix=base))
    if not blobs:
        raise ArchiveError(f"nothing archived under {name}")
    if len(blobs) > MAX_FILES:
        raise ArchiveError(f"{name} holds {len(blobs)} files, more than this will restore")

    total = sum(b.size or 0 for b in blobs)
    if total > MAX_TOTAL_BYTES:
        raise ArchiveError(f"{name} is {total // (1024 * 1024)} MB, more than this will restore")

    written = 0
    for blob in blobs:
        rel = blob.name[len(base):]
        if not rel or rel.endswith("/"):
            continue

        # **Resolve, then verify it is still inside** — the same discipline `app.contain` uses, and
        # for a sharper reason here: `rel` comes from a blob listing rather than from a person, and a
        # key containing `../` would otherwise write wherever it liked. Checking the string for `..`
        # is the version that looks right and misses an absolute path.
        destination = (target / rel).resolve()
        if destination != target and target not in destination.parents:
            _logger.warning("polson archive: skipping %r, which resolves outside %s", rel, name)
            continue

        destination.parent.mkdir(parents=True, exist_ok=True)
        blob.download_to_filename(str(destination))
        written += 1

    if not (target / "project.json").is_file():
        # Without it `project.read` cannot identify the run, and a directory that looks like a
        # project and is not is worse than an honest refusal.
        raise ArchiveError(f"{name} has no project.json in the archive; it cannot be opened")

    _logger.info("polson archive: restored %s, %d file(s), %d bytes", name, written, total)
    return target


def restorable(root: Path) -> list[str]:
    """Archived projects that are **not** on local disk — the ones worth offering.

    A project still present locally needs no restoring and listing it twice on the index would invite
    someone to overwrite a live run with an older copy of itself.
    """
    if not ARCHIVE_URI:
        return []
    here = {p.parent.name for p in Path(root).glob("*/project.json")}
    return [n for n in names() if n not in here]
# endregion


# region Implementation
def _bucket():
    """The bucket and key prefix from `POLSON_MIRROR_URI`, as `(bucket, prefix)`.

    The client is constructed per call rather than cached. This runs a few times a session, at human
    speed, and a cached client is a thing to invalidate when credentials rotate.
    """
    from google.cloud import storage                            # deferred: optional dependency

    parsed = urlparse(ARCHIVE_URI)
    if parsed.scheme != "gs" or not parsed.netloc:
        raise ArchiveError(f"archive URI must be gs://bucket[/prefix], not {ARCHIVE_URI!r}")

    return storage.Client().bucket(parsed.netloc), parsed.path.strip("/")
# endregion
