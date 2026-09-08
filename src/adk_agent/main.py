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
ordinary `FastAPI`**, so `src/studio`'s UI can be mounted on the same app and the same port.
ADK's own Cloud Run guide names this as the reason to take this route — *"particularly if you want to
embed your agent within a custom FastAPI application."* That collapses the hackathon's hosted-URL
requirement and the agent runtime into one container instead of two deployments.

Nothing is mounted yet; that is the next piece of work. Until then this is `adk web` with a seam.
"""

from __future__ import annotations

import logging
import os
from pathlib import Path

from fastapi import FastAPI
from google.adk.cli.fast_api import get_fast_api_app

import artifact_store

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
# `file://` on Windows loses roughly one save in four to a directory-rename race with antivirus.
# This swaps in a retrying implementation behind the same URI scheme, so nothing downstream changes.
# See artifact_store.py for the measurement.
artifact_store.install()

ARTIFACT_SERVICE_URI = (
    os.environ.get("POLSON_ARTIFACT_SERVICE_URI", "").strip()
    or (Path(__file__).resolve().parent / "artifact_versions").as_uri()
)

#: Serve ADK's bundled console. On by default because it is the fastest way to *watch* a run — it
#: renders the event stream, the tool calls and their arguments, and the agent graph, which is
#: exactly the trace `CLAUDE.md` §6 asks a run to leave. Turn it off for a public deployment where
#: the studio UI is the front door and the dev console should not be reachable.
SERVE_CONSOLE = _flag("POLSON_SERVE_CONSOLE", True)

#: Branding for the console's logo area. `logo_text` and `logo_image_url` are the parts of
#: google/adk-python#2859 that actually shipped in 2.8.0 — the `display_name` labelling for tools in
#: the graph view, and `ADK_WEB_DIR` for serving a custom adk-web build, did **not**, so tool nodes
#: still read as their raw function names. Checked in the 2.8.0 source rather than inferred from
#: the issue being closed.
#: **Both or neither.** ADK raises `ValueError: Both --logo-text and --logo-image-url must be
#: defined when using logo config.` if only one is set, so branding is opt-in as a pair and the
#: default is ADK's own. Set both env vars to rebrand; the image URL is fetched by the browser, so
#: it must be reachable from wherever the console is opened.
LOGO_TEXT = os.environ.get("POLSON_LOGO_TEXT", "").strip() or None
LOGO_IMAGE_URL = os.environ.get("POLSON_LOGO_IMAGE_URL", "").strip() or None
if not (LOGO_TEXT and LOGO_IMAGE_URL):
    LOGO_TEXT = LOGO_IMAGE_URL = None

#: Create a session on demand rather than requiring one to exist. For a visitor handed a URL — a
#: hackathon judge, say — the alternative is a console that errors until they find the "new session"
#: button, which is a poor first ten seconds.
AUTO_CREATE_SESSION = _flag("POLSON_AUTO_CREATE_SESSION", True)

# A startup banner, because twice now a stale process kept serving after a "successful" restart
# and there was no way to tell from outside which code was live. The server now says so itself.
_logger = logging.getLogger("polson.runtime")
try:
    from google.adk.cli.service_registry import get_service_registry
    _probe = get_service_registry().create_artifact_service(ARTIFACT_SERVICE_URI, agents_dir=AGENTS_DIR)
    # The engine path is on the banner because getting it wrong is silent until something tries to
    # draw. The first Cloud Run deploy skipped project seeding with `no CLI at
    # /bin/cli/Polson.CLI.dll` — a container-root path derived from a checkout layout — and nothing
    # said so until the entrypoint happened to attempt it. Now the server states it at startup.
    from studio import DEFAULT_CLI_DLL
    _cli = Path(os.environ.get("POLSON_CLI_DLL", "").strip() or DEFAULT_CLI_DLL)
    _logger.warning(
        "polson runtime: agents=%s | artifacts=%s (%s) | console=%s | logo=%r | engine=%s (%s)",
        AGENTS_DIR, type(_probe).__name__, ARTIFACT_SERVICE_URI, SERVE_CONSOLE,
        LOGO_TEXT or "adk default", _cli, "present" if _cli.is_file() else "MISSING")
except Exception as _e:  # a banner must never be the reason the server does not start
    _logger.warning("polson runtime: could not identify the artifact service (%s)", _e)

app: FastAPI = get_fast_api_app(
    agents_dir=AGENTS_DIR,
    session_service_uri=SESSION_SERVICE_URI,
    artifact_service_uri=ARTIFACT_SERVICE_URI,
    allow_origins=_origins(),
    web=SERVE_CONSOLE,
    logo_text=LOGO_TEXT,
    logo_image_url=LOGO_IMAGE_URL,
    auto_create_session=AUTO_CREATE_SESSION,
)

# The first thing mounted on the seam this file exists to provide: `/new` takes a brief and a
# document from a visitor and `/projects` turns them into a project the console can open. Failing
# to mount it must not stop the agent runtime from serving — a broken intake form is a degraded
# demo, while a server that will not start is no demo at all.
try:
    import intake

    intake.mount(app)
    _logger.warning("polson runtime: intake mounted at /new")
except Exception as _e:
    _logger.warning("polson runtime: intake not mounted (%s)", _e)

# The studio's own pages, mounted as a sub-application at /studio.
#
# **Read-only here, and that is a property of the runtime rather than a setting.** The studio can
# both drive a run and watch one; driving needs `orchestrator.run`, which needs the Antigravity SDK,
# which this environment deliberately does not ship (`requirements.in` says why). Every module on the
# *observing* path was decoupled from the driver so this import succeeds — see the lazy imports in
# `studio.runs`, `orchestrator.watch` and `orchestrator/__init__.py`. Decoupling let it *mount*; it
# did not stop it *offering* to drive, so the mount below refuses every mutation but the one that
# registers a watch. A page here is a window onto the record, and direction happens at `/new`.
#
# Mounted rather than merged so its routes cannot collide with ADK's own, and wrapped because a
# missing UI must not stop the agent runtime from serving.
try:
    import sys as _sys
    from pathlib import Path as _Path

    from fastapi.responses import HTMLResponse as _HTML
    from fastapi.responses import JSONResponse as _JSON

    # `src/`, which is where `studio/` and `orchestrator/` now live. They were under
    # `src/webapp/`, which meant this runtime could not be shipped without the Antigravity tree
    # beside it — see `tools/python/README.md` for the same argument applied to the installers.
    _shared = _Path(__file__).resolve().parent.parent
    if (_shared / "studio").is_dir():
        _sys.path.insert(0, str(_shared))

        # **Two packages in this tree are called `studio`.** `src/adk_agent/studio.py` is the agent
        # factory every generated `agent.py` imports by that name, and `src/studio/` is the
        # web layer. This process has the ADK one first on `sys.path` (uvicorn's `--app-dir`), so a
        # plain `import studio.app` finds the module and reports that `studio` is not a package.
        #
        # Loaded under an alias rather than renaming either: the ADK name is baked into every
        # generated app on disk, and the webapp name is what its own tests and imports use. The
        # alias is local to this import — inside the package, `from . import projects` still
        # resolves against it, and `from orchestrator import …` finds the path added above.
        import importlib.util as _ilu

        _pkg = _shared / "studio"
        _spec = _ilu.spec_from_file_location(
            "polson_studio", _pkg / "__init__.py", submodule_search_locations=[str(_pkg)])
        _mod = _ilu.module_from_spec(_spec)
        _sys.modules["polson_studio"] = _mod
        _spec.loader.exec_module(_mod)

        from polson_studio.app import create_app as _create_studio

        # From `newproject`, which owns the POLSON_PROJECTS_DIR resolution — recomputing it here
        # would let the page list a different directory from the one projects are created in.
        from newproject import PROJECTS_DIR as _projects

        # `observe_only` is what stops the page *offering* what the middleware below refuses.
        # A form a visitor fills in and is then told cannot work is worse than no form: the
        # refusal arrives after the effort, and a director hit exactly that.
        _studio = _create_studio(root=_Path(_projects), observe_only=True, create_at="/new")

        # **"Observe only" was a sentence in this comment and nothing enforced it.** The mounted app
        # still served `POST /projects`, `POST /runs`, `/answer` and `/say` — and its create path
        # hardcodes the `agy` SDK, so a director who used the studio's own form got an Antigravity
        # project with no ADK app, which this runtime cannot serve and cannot drive: the driver needs
        # a package `python-adk` deliberately does not ship. It returned 303, the run page returned
        # 200, and the event stream stayed open forever with nothing to send. No error anywhere.
        #
        # So the boundary is enforced here rather than described. Reading is every GET; the one
        # mutation that belongs to reading is registering a watch.
        @_studio.middleware("http")
        async def _observe_only(request, call_next):                # noqa: ANN001, ANN202
            if intake.observing_only(request.method, request.scope["path"],
                                     request.scope.get("root_path") or ""):
                return await call_next(request)
            why = ("This studio is mounted for observation. It cannot create or drive a project "
                   "here, because its driver is the Antigravity SDK and this runtime does not ship "
                   "it. Make an ADK project at /new — that form attaches a document, starts the "
                   "agent, and brings you back here to watch it.")

            # A backstop rather than the common path: the page no longer offers these. Still worth
            # answering in the visitor's own medium — a browser that posts a form and receives raw
            # JSON on a black page has been given a stack trace by another name.
            if "text/html" in request.headers.get("accept", ""):
                return _HTML(
                    "<!doctype html><title>Not here</title>"
                    "<style>body{font:15px/1.6 system-ui,sans-serif;max-width:34rem;margin:5rem auto;"
                    "padding:0 1rem;color:#1c2733;background:#faf8f4}"
                    "a{color:#1f6f8b}</style>"
                    f"<h1>Not from this page</h1><p>{why}</p>"
                    '<p><a href="/new">Make one at /new</a> &middot; '
                    '<a href="/studio/">back to the studio</a></p>', status_code=409)
            return _JSON({"detail": why}, status_code=409)

        app.mount("/studio", _studio)
        _logger.warning("polson runtime: studio mounted at /studio (observing only, enforced)")
    else:
        _logger.warning("polson runtime: studio not mounted — no studio package at %s", _shared)
except Exception as _e:
    _logger.warning("polson runtime: studio not mounted (%s)", _e)
