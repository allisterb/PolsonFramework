"""A `file://` artifact service that survives Windows.

**The bug.** `FileArtifactService.save_artifact` writes a version into `versions/.N.pending/` and
then commits it with `os.replace(staging_dir, version_dir)` — replacing one *directory* with
another. On POSIX that is atomic and reliable. On Windows `os.replace` becomes `MoveFileEx`, which
fails with `PermissionError: [WinError 5] Access is denied` whenever anything still holds a handle
on a file inside the source directory — which real-time antivirus routinely does for the few
milliseconds after a file is written.

**It is not rare and it is not file-type specific.** Measured on this machine, 30 staged saves of a
16 KB `.webp` and 30 of a small `.js`: **5 and 10 failures respectively**. Roughly one save in four,
arriving as a `PermissionError` that surfaces to the agent mid-run and loses the version. An earlier
three-save probe passed cleanly, which is exactly how a one-in-four race hides.

**The fix is a retry, because the failure is transient and the operation is safe to repeat.** On
failure the base implementation removes its staging directory (`shutil.rmtree(staging_dir,
ignore_errors=True)` in its own `except`) and commits nothing, so a retry recomputes the same next
version against unchanged state. Retrying the whole call, rather than reaching into the private
staging logic, keeps this a thin subclass that does not duplicate anything.

`save_artifact` is the only override: it is the sole path that renames a directory. Reads,
listings and deletes touch files rather than directories and do not show the fault.

> This is a Windows development problem only. A Linux container will not see it, and neither will
> `gs://`. It is fixed here anyway because an unreliable version history is worse than none — a
> missing version looks identical to an agent that chose not to save one.
"""

from __future__ import annotations

import asyncio
import logging
import os
from pathlib import Path
from typing import Any
from typing import Optional
from typing import Union
from urllib.parse import unquote
from urllib.parse import urlparse
from urllib.request import url2pathname

from google.adk.artifacts.base_artifact_service import BaseArtifactService
from google.adk.artifacts.file_artifact_service import FileArtifactService
from google.genai import types

logger = logging.getLogger("polson.artifact_store")

#: Attempts including the first. A benchmark of 40 back-to-back saves passed 40/40 at five, but a
#: live server has the .NET engine and the model's own I/O competing for the same disk, so the
#: budget is wider here than the measurement strictly required.
COMMIT_ATTEMPTS = 8

#: Seconds, linear rather than exponential: the handle is released in milliseconds, so a doubling
#: backoff would spend far longer waiting than the fault lasts. Eight attempts at 0.15s is about a
#: second of patience in the worst case, which is invisible next to a render.
COMMIT_DELAY = 0.15


class RetryingFileArtifactService(FileArtifactService):
    """`FileArtifactService` with the directory-commit race retried."""

    async def save_artifact(
        self,
        *,
        app_name: str,
        user_id: str,
        filename: str,
        artifact: Union[types.Part, dict[str, Any]],
        session_id: Optional[str] = None,
        custom_metadata: Optional[dict[str, Any]] = None,
    ) -> int:
        last: PermissionError | None = None
        for attempt in range(COMMIT_ATTEMPTS):
            try:
                return await super().save_artifact(
                    app_name=app_name,
                    user_id=user_id,
                    filename=filename,
                    artifact=artifact,
                    session_id=session_id,
                    custom_metadata=custom_metadata,
                )
            except PermissionError as error:
                # WinError 5 on the staging-directory commit. Anything else with this type is a
                # real permission problem — a read-only tree, a locked path — and retrying it would
                # turn a clear failure into a slow one.
                if os.name != "nt" or getattr(error, "winerror", None) != 5:
                    raise
                last = error
                # INFO, not DEBUG: when this fires it is the only evidence the fix is working,
                # and a silent success is indistinguishable from the fix not being installed —
                # which is exactly how a stale server process hid this once already.
                logger.info(
                    "artifact commit for %r lost the rename race, retrying (attempt %d/%d)",
                    filename, attempt + 1, COMMIT_ATTEMPTS,
                )
                if attempt + 1 < COMMIT_ATTEMPTS:
                    await asyncio.sleep(COMMIT_DELAY)

        logger.warning(
            "artifact %r could not be committed after %d attempts; the version is lost",
            filename, COMMIT_ATTEMPTS,
        )
        assert last is not None
        raise last


def _factory(uri: str, **_: Any) -> BaseArtifactService:
    """Mirrors ADK's own `file://` parsing, so the URI means exactly what it did before."""
    parsed = urlparse(uri)
    if parsed.netloc not in ("", "localhost"):
        raise ValueError("file:// artifact URIs must reference the local filesystem.")
    if not parsed.path:
        raise ValueError("file:// artifact URIs must include a path component.")

    path_str = unquote(parsed.path)
    if os.name == "nt":
        path_str = url2pathname(path_str)
    return RetryingFileArtifactService(root_dir=Path(path_str))


def install() -> None:
    """Replaces the built-in `file` artifact factory with the retrying one.

    Registering over the existing scheme rather than inventing a new one keeps
    `artifact_service_uri` unchanged, so nothing else in the runtime — or in a deployment — has to
    know this fix exists.
    """
    from google.adk.cli.service_registry import get_service_registry

    get_service_registry().register_artifact_service("file", _factory)
    logger.debug("file:// artifact service replaced with the retrying implementation")
