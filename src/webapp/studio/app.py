"""The studio, over HTTP.

Milestone 6, step 2a: enough of a server to watch a real run happen. A project is chosen, a run
starts, and its record streams to the browser as it is written. The brief form, the director's
answers and the sense-making curve come next; the shape they attach to is here.

Three rules from `CLAUDE.md` §4 govern this file, and two of them are already satisfied by things
that exist rather than by code here:

- **Artifacts are streamed as URLs, never as embedded bytes.** That is what makes a run replayable
  and a refresh survivable, and it is why `serve_artifact` exists at all.
- **Visitor text is untrusted input to an agent that holds tools.** It reaches an agent only through
  `create-project`, which sanitises it into `brief.md` — a file `project.load` never reads. The web
  layer does not get its own path to the agent, and step 2b will not give it one.
- **Concurrency and spend are capped**, in `Registry`.

The path containment in `serve_artifact` is the one genuinely dangerous thing here: it serves files
by a name a visitor supplies. It is written the way `ProjectPath.Resolve` is written on the .NET
side — resolve, then verify the result is still inside — and it has tests before it has a template.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, AsyncIterator

from fastapi import FastAPI, Form, HTTPException, Request
from fastapi.responses import FileResponse, HTMLResponse, JSONResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from sse_starlette.sse import EventSourceResponse

from orchestrator import csm

from .runs import Registry, Run, StudioError

#: Where a page looks for projects to run. Overridden by the entry point.
DEFAULT_ROOT = Path.cwd()

TEMPLATES = Jinja2Templates(directory=str(Path(__file__).parent / "templates"))


def create_app(root: Path | None = None, registry: Registry | None = None) -> FastAPI:
    """Builds the app. Takes its collaborators so a test can supply its own."""
    app = FastAPI(title="Polson Studio", docs_url=None, redoc_url=None)
    app.state.root = Path(root or DEFAULT_ROOT).resolve()
    app.state.registry = registry or Registry()

    # The stylesheet, and nothing else. Serving a directory of our own files needs no containment
    # check because no visitor-supplied name reaches it — unlike the artifact route below.
    app.mount("/static", StaticFiles(directory=str(Path(__file__).parent / "static")), name="static")

    # region Pages
    @app.get("/", response_class=HTMLResponse)
    async def index(request: Request) -> Any:
        return TEMPLATES.TemplateResponse(request, "index.html", {
            "root": app.state.root,
            "projects": discover(app.state.root),
            "runs": [r.summary() for r in app.state.registry.runs],
            "active": (a := app.state.registry.active) and a.id,
        })

    @app.post("/runs")
    async def start(request: Request, project: str = Form(...), prompt: str = Form("")) -> Any:
        """Starts a run against a project already on disk. The brief form arrives in 2b."""
        try:
            chosen = contain(app.state.root, project)
        except StudioError as exc:
            return refuse(request, str(exc))

        try:
            run = await app.state.registry.start(chosen, prompt.strip() or DEFAULT_PROMPT)
        except StudioError as exc:
            return refuse(request, str(exc))

        return RedirectResponse(f"/runs/{run.id}", status_code=303)

    @app.get("/runs/{run_id}", response_class=HTMLResponse)
    async def show(request: Request, run_id: str) -> Any:
        run = found(app.state.registry, run_id)
        return TEMPLATES.TemplateResponse(request, "run.html", {"run": run.summary()})
    # endregion

    # region Stream
    @app.get("/runs/{run_id}/events")
    async def events(run_id: str) -> EventSourceResponse:
        """The run's record, as it is written.

        Replay first, then the live tail, from one subscription — so a browser that refreshes gets
        the whole run rather than whatever happens next.
        """
        run = found(app.state.registry, run_id)

        async def stream() -> AsyncIterator[dict[str, str]]:
            subscription = run.attach()
            try:
                async for event in subscription:
                    yield {"event": event.get("type", "event"), "data": json.dumps(event)}
            finally:
                subscription.close()

            # The stream ends when the broker closes, which is after the run's last event has been
            # delivered. Saying so lets the page stop reconnecting.
            yield {"event": "run.closed", "data": json.dumps(run.summary())}

        return EventSourceResponse(stream())

    @app.get("/runs/{run_id}/curve")
    async def curve(run_id: str) -> JSONResponse:
        """The run coded as creative sense-making. See `docs/creative-sense-making.md`."""
        run = found(app.state.registry, run_id)
        coded = csm.read(run.project)
        return JSONResponse({"summary": coded.summary(), "trace": coded.trace})
    # endregion

    # region Files
    @app.get("/runs/{run_id}/artifacts/{name:path}")
    async def serve_artifact(run_id: str, name: str) -> FileResponse:
        """A render, by the project-relative path the record names it by."""
        run = found(app.state.registry, run_id)

        try:
            path = contain(run.artifacts, name)
        except StudioError as exc:
            raise HTTPException(status_code=404, detail=str(exc)) from exc

        if not path.is_file():
            raise HTTPException(status_code=404, detail=f"no such artifact: {name}")

        return FileResponse(path)
    # endregion

    return app


#: What starts an agent that has already been told everything else in its own instructions.
DEFAULT_PROMPT = (
    "Read GEMINI.md in this project directory, then brief.md, and begin the project. "
    "Work through the stages it names, declaring each one with Stage.begin, and save every render "
    "to artifacts/ with outFile."
)


def contain(root: Path, name: str) -> Path:
    """Resolves `name` under `root`, refusing anything that escapes it.

    Written the way `ProjectPath.Resolve` is written on the .NET side: resolve first, then check the
    result is still inside. Testing the *string* for `..` is the version that looks right and is not
    — it misses an absolute path, a symlink, and every encoding of a separator the platform accepts.
    """
    root = Path(root).resolve()
    candidate = (root / name).resolve()

    if candidate != root and root not in candidate.parents:
        raise StudioError(f"that path is outside {root.name}: {name}")

    return candidate


def discover(root: Path) -> list[dict[str, str]]:
    """Every runnable project under `root`, shallowly.

    A project is a directory with a `project.json`. Unreadable ones are listed with the reason rather
    than hidden, because a project that cannot be run is exactly what someone needs to see.
    """
    found_projects = []
    for manifest in sorted(Path(root).glob("*/project.json")):
        entry = {"name": manifest.parent.name, "path": manifest.parent.name, "why": ""}
        try:
            data = json.loads(manifest.read_text(encoding="utf-8-sig"))
            entry["workflow"] = data.get("workflow", "?")
            entry["profile"] = data.get("profile", "?")
            if entry["profile"] != "standalone":
                entry["why"] = "managed — its host owns tool policy, so the orchestrator will not run it"
        except Exception as exc:  # noqa: BLE001
            entry["workflow"] = entry["profile"] = "?"
            entry["why"] = f"unreadable project.json: {exc}"
        found_projects.append(entry)
    return found_projects


def found(registry: Registry, run_id: str) -> Run:
    if (run := registry.get(run_id)) is None:
        raise HTTPException(status_code=404, detail=f"no such run: {run_id}")
    return run


def refuse(request: Request, why: str) -> Any:
    """A refusal a visitor can act on, rendered as the page they were on rather than as a stack."""
    return TEMPLATES.TemplateResponse(request, "index.html", {
        "root": request.app.state.root,
        "projects": discover(request.app.state.root),
        "runs": [r.summary() for r in request.app.state.registry.runs],
        "active": (a := request.app.state.registry.active) and a.id,
        "refused": why,
    }, status_code=409)
