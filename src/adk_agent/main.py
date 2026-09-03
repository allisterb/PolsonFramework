"""ASGI entry point for the Polson ADK runtime.

Run it locally exactly as the container does, which is the point of having it::

    python-adk\\Scripts\\python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000
    # from src/adk_agent, with POLSON_PROJECT_DIR set

**Why this file exists at all, when `adk web` would also serve the agent.**

`adk web <agents_dir>` and `adk api_server <agents_dir>` are the CLI's two servers, and both are thin
wrappers around `get_fast_api_app(...)` — the same function called below. They differ only in `web`:
`adk web` serves ADK's bundled console (an 8.3 MB pre-built Angular SPA shipped inside the wheel at
`google/adk/cli/browser/`) alongside the REST API; `adk api_server` serves the API alone.

Calling the function ourselves buys the one thing the CLI cannot give: **the returned object is an
ordinary `FastAPI`**, so `src/webapp`'s studio UI can be mounted on the same app and the same port.
ADK's own Cloud Run guide names this as the reason to take this route — *"particularly if you want to
embed your agent within a custom FastAPI application."* That collapses the hackathon's hosted-URL
requirement and the agent runtime into one container instead of two deployments.

Nothing is mounted yet; that is the next piece of work. Until then this is `adk web` with a seam.
"""

from __future__ import annotations

import os
from pathlib import Path

from fastapi import FastAPI
from google.adk.cli.fast_api import get_fast_api_app

#: The directory ADK scans for apps — one app per generated project, written by `newproject.py`.
#: It holds nothing committed, which is why `studio.py` sits one level up: ADK lists an app for
#: every *directory* here that looks like an agent, so a shared factory living alongside them would
#: be listed as a broken app. A `.py` file is invisible to that scan and still importable.
#:
#: Created if absent so a fresh checkout serves an empty app list rather than failing to start.
AGENTS_DIR = str(Path(__file__).resolve().parent / "apps")
Path(AGENTS_DIR).mkdir(parents=True, exist_ok=True)


def _flag(name: str, default: bool) -> bool:
    """An env var read as a boolean, accepting the spellings a Dockerfile or shell actually uses."""
    raw = os.environ.get(name)
    if raw is None or not raw.strip():
        return default
    return raw.strip().lower() in {"1", "true", "yes", "on"}


def _origins() -> list[str] | None:
    """CORS origins, comma-separated. `None` rather than `[]` — ADK treats absent as "no CORS"."""
    raw = os.environ.get("POLSON_ALLOW_ORIGINS", "").strip()
    return [o.strip() for o in raw.split(",") if o.strip()] or None


#: Sessions persist here. ADK defaults to in-memory, which loses a run on restart — fine locally,
#: wrong for anything a judge clicks through. A `sqlite://` URI keeps it to one file and no service;
#: swap for `postgresql://` or an Agent Engine URI when there is more than one instance.
SESSION_SERVICE_URI = os.environ.get("POLSON_SESSION_SERVICE_URI", "").strip() or None

#: Where peeked renders are versioned. ADK recognises three schemes — `memory://`, `file://` and
#: `gs://` — and the default here is `file://` rather than memory because the versions are the
#: point: `outFile` overwrites, so a re-render under one filename destroys what came before, and
#: this store is the only place the progression survives. Use `gs://` on Cloud Run, where a
#: container's filesystem does not outlive it.
#:
#: This is a *second* artifact store, not a replacement for `projects/<id>/artifacts/`. That one
#: stays the browsable, replayable record a person opens; this one holds versioned blobs the model
#: is shown. Neither substitutes for the other.
ARTIFACT_SERVICE_URI = (
    os.environ.get("POLSON_ARTIFACT_SERVICE_URI", "").strip()
    or (Path(__file__).resolve().parent / "artifact_versions").as_uri()
)

#: Serve ADK's bundled console. On by default because it is the fastest way to *watch* a run — it
#: renders the event stream, the tool calls and their arguments, and the agent graph, which is
#: exactly the trace `CLAUDE.md` §6 asks a run to leave. Turn it off for a public deployment where
#: the studio UI is the front door and the dev console should not be reachable.
SERVE_CONSOLE = _flag("POLSON_SERVE_CONSOLE", True)

app: FastAPI = get_fast_api_app(
    agents_dir=AGENTS_DIR,
    session_service_uri=SESSION_SERVICE_URI,
    artifact_service_uri=ARTIFACT_SERVICE_URI,
    allow_origins=_origins(),
    web=SERVE_CONSOLE,
)
