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
import os
from pathlib import Path
from typing import Any, AsyncIterator

from fastapi import FastAPI, Form, HTTPException, Request, Response
from fastapi.responses import FileResponse, HTMLResponse, JSONResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from pygments import highlight
from pygments.formatters import HtmlFormatter
from pygments.lexers import JavascriptLexer
from sse_starlette.sse import EventSourceResponse

from orchestrator import csm

from . import projects
from . import observe as observe_mod
from .runs import DEFAULT_PROMPT, Registry, Run, StudioError

#: Where a page looks for projects to run. Overridden by the entry point.
DEFAULT_ROOT = Path.cwd()

TEMPLATES = Jinja2Templates(directory=str(Path(__file__).parent / "templates"))

#: Where a visitor can get the source of what they are using.
#:
#: Section 13 of the AGPL asks a modified version offered over a network to give the people using it
#: an opportunity to receive its corresponding source. That obligation belongs to whoever is running
#: the fork, not to us — so this is a setting rather than a hardcoded link, and honouring the licence
#: after a fork is one environment variable instead of an edit to a template nobody thinks to look at.
#:
#: Deliberately unlike `ApiKeys:GoogleAgentPlatform`, which has *no* environment override on purpose:
#: that one is read by both halves of the studio and an override honoured on one side would let them
#: disagree. This is read here and nowhere else.
SOURCE_URL = os.environ.get("POLSON_SOURCE_URL") or "https://github.com/allisterb/Polson"

# A global rather than a context entry, so a page added later cannot quietly ship without the offer.
TEMPLATES.env.globals["source_url"] = SOURCE_URL

#: Line numbers because the record refers to scripts by path and a reader refers to them by line.
FORMATTER = HtmlFormatter(style="friendly", cssclass="code", linenos="table", lineanchors="L")

#: What a render can produce, and therefore the whole of what the artifact route will serve.
#:
#: `outFile` writes webp, png or jpeg; `outSvg` writes svg. Nothing else is an artifact, so nothing
#: else is reachable by a visitor-supplied name — see `serve_artifact`, which resolves against the
#: project root rather than a single directory and needs this to keep that boundary narrow.
ARTIFACT_SUFFIXES = frozenset({".webp", ".png", ".jpg", ".jpeg", ".svg"})


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
            "workflows": projects.WORKFLOWS,
            "all_types": projects.ALL_TYPES,
            "form": {},
        })

    @app.post("/runs")
    async def start(request: Request, project: str = Form(...), prompt: str = Form(""),
                    fresh: str = Form("")) -> Any:
        """Starts a run against a project already on disk.

        `fresh` starts a new session rather than continuing the recorded one. Continuing is the
        default because it is what carrying on with a piece means — but it is also the way to waste a
        turn, so `Registry.start` refuses to continue a session with nothing new to say.
        """
        try:
            chosen = contain(app.state.root, project)
        except StudioError as exc:
            return refuse(request, str(exc))

        try:
            run = await app.state.registry.start(chosen, prompt, resume=not fresh)
        except StudioError as exc:
            return refuse(request, str(exc))

        return RedirectResponse(f"/runs/{run.id}", status_code=303)

    @app.post("/projects")
    async def make(request: Request,
                   name: str = Form(...),
                   workflow: str = Form("logo"),
                   kind: str = Form(""),
                   brief: str = Form(""),
                   start_now: str = Form("")) -> Any:
        """Turns a brief into a project, and optionally starts it.

        The brief is never held here and never reaches an agent from here: `create` hands it to the
        CLI as a file, which sanitises it into the project's own `brief.md`. `project.load` does not
        read that file, and the agent reaches it only by reference from its instructions — so the
        boundary between "instructions we wrote" and "text a stranger typed" stays a file boundary.
        """
        try:
            made = await projects.create(app.state.root, name.strip(), workflow, kind.strip(), brief)
        except StudioError as exc:
            return refuse(request, str(exc), form={"name": name, "workflow": workflow,
                                                   "kind": kind, "brief": brief})

        if not start_now:
            return RedirectResponse("/", status_code=303)

        try:
            # A project made moments ago has no session to continue, so this is the opening
            # instruction and Registry will supply it.
            run = await app.state.registry.start(made, "")
        except StudioError as exc:
            # The project exists either way; only the run was refused, and saying so is the
            # difference between "try again later" and "your brief is gone".
            return refuse(request, f"Project '{made.name}' was created, but the run was not started: {exc}")

        return RedirectResponse(f"/runs/{run.id}", status_code=303)

    @app.post("/observe")
    async def watch(request: Request, project: str = Form(...)) -> Any:
        """Watches a project someone else is driving, in Claude Code or Claude Desktop.

        Not a run: nothing is started and nothing is spent. The record is already being written —
        the MCP server writes `server.jsonl` whoever drives it, and the `preserve-chatlog` hook
        preserves the conversation — so this only puts a reader on it. See `studio.observe`.
        """
        try:
            chosen = contain(app.state.root, project)
            run = await observe_mod.observe(app.state.registry, chosen)
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
        """The run coded as creative sense-making. See `docs/creative-sense-making.md`.

        Scoped to this run by `run.since`, for the same reason the tailer skips what is already in
        `server.jsonl`: the event files belong to the project and outlive any one run. This is a run
        page, so the trace and the curve have to be readings of the same events.
        """
        run = found(app.state.registry, run_id)
        coded = csm.read(run.project, since=run.since)
        return JSONResponse({"summary": coded.summary(), "trace": coded.trace})
    # endregion

    # region The director
    @app.post("/runs/{run_id}/answer")
    async def answer(run_id: str,
                     question: str = Form(...),
                     text: str = Form(""),
                     option: str = Form(""),
                     skip: str = Form("")) -> JSONResponse:
        """Answers a question the agent asked.

        A refusal here is `409`, not `404`: the question existed, and it has been settled — usually
        because it timed out while nobody was looking, or another watcher answered first. Telling
        those apart from a wrong id is what stops a page retrying forever.
        """
        run = found(app.state.registry, run_id)

        reply: dict[str, Any] = {"skipped": bool(skip)}
        if option:
            reply["selected"] = [option]
        if text.strip():
            reply["text"] = text

        if not run.answer(question, **reply):
            raise HTTPException(
                status_code=409,
                detail="That question is already settled — it may have timed out, or been answered "
                       "in another window.")

        return JSONResponse({"answered": question})

    @app.post("/runs/{run_id}/say")
    async def say(run_id: str, text: str = Form(...)) -> JSONResponse:
        """The director interrupting a turn already under way.

        Not an answer to anything: the agent did not ask. Under the enactive account this is a
        contribution rather than a correction — a disruption that opens directions the agent would
        not have reached — so it goes into the record as one.
        """
        run = found(app.state.registry, run_id)

        if not run.live:
            raise HTTPException(status_code=409, detail="That run has finished; nothing is listening.")

        if len(text) > MAX_INTERJECTION:
            raise HTTPException(
                status_code=413,
                detail=f"That is {len(text)} characters; an interjection is capped at "
                       f"{MAX_INTERJECTION}. Longer direction belongs in a brief.")

        if not run.say(text):
            raise HTTPException(status_code=400, detail="Nothing to say.")

        return JSONResponse({"said": text.strip()[:200]})
    # endregion

    # region Files
    @app.get("/runs/{run_id}/scripts/{name:path}", response_class=HTMLResponse)
    async def serve_script(run_id: str, name: str) -> HTMLResponse:
        """One executed script, highlighted, as a fragment the page drops into a panel.

        This is the half of a Polson run that a rendered image cannot carry. A bitmap records what
        the canvas ended up looking like; the script records *why* — the golden section the mast was
        placed on, the scale the bars were measured against, the direction that was tried and
        abandoned in a comment. Serving it beside the render is what makes the trace legible as
        reasoning rather than as progress.
        """
        run = found(app.state.registry, run_id)

        try:
            path = contain(run.scripts, name)
        except StudioError as exc:
            raise HTTPException(status_code=404, detail=str(exc)) from exc

        if not path.is_file():
            raise HTTPException(status_code=404, detail=f"no such script: {name}")

        source = path.read_text(encoding="utf-8", errors="replace")
        return HTMLResponse(highlight(source, JavascriptLexer(), FORMATTER))

    @app.get("/code.css")
    async def code_css() -> Response:
        """Pygments' own styles, generated rather than checked in so they cannot drift from it."""
        return Response(FORMATTER.get_style_defs(".code"), media_type="text/css",
                        headers={"Cache-Control": "max-age=3600"})

    @app.get("/runs/{run_id}/artifact/{name:path}")
    async def serve_artifact(run_id: str, name: str) -> FileResponse:
        """A render, by the project-relative path the record names it by.

        **Project-relative, not `artifacts/`-relative**, and the difference is a real bug rather than
        a tidying. A render event carries whatever `outFile` was given, and a workflow's final
        delivery is conventionally written to the project root — `output.webp` beside `output.svg`.
        Resolving every name under `artifacts/` therefore served the staged renders (whose names
        happen to begin `artifacts/`, matching the old route segment by coincidence) and 404'd the
        finished picture. The page showed a caption with no image under it, which reads as a broken
        render rather than as a missing route.

        Widening the root widens what a visitor-supplied name can reach, so the suffix allowlist is
        what keeps the boundary meaningful: containment says *inside the project*, and the allowlist
        says *and it is an image*. Without the second, this route would serve `brief.md`,
        `project.json` and `.agents/settings.json` — a project's whole configuration — to anyone who
        guessed the name.
        """
        run = found(app.state.registry, run_id)

        if Path(name).suffix.lower() not in ARTIFACT_SUFFIXES:
            # Refused on the name alone, before touching the disk: whether the file exists is not
            # something a visitor should be able to learn about a path we would never serve.
            raise HTTPException(status_code=404, detail=f"not an artifact: {name}")

        try:
            path = contain(run.project.root, name)
        except StudioError as exc:
            raise HTTPException(status_code=404, detail=str(exc)) from exc

        if not path.is_file():
            raise HTTPException(status_code=404, detail=f"no such artifact: {name}")

        return FileResponse(path)
    # endregion

    return app


#: An interjection is a sentence, not a brief. Longer direction belongs in the brief, which is
#: sanitised on the way in; this reaches a running agent directly, so it stays short.
MAX_INTERJECTION = 2000


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

            # A project that has run before will *continue* rather than start over, which changes
            # what a useful opening prompt is. The form has to say so before the turn is spent.
            entry["session"] = bool(data.get("conversationId"))

            # Runnability is decided by the same things `project.load` decides it by, and not by the
            # `profile` label, which since every Antigravity project carries a tool policy records
            # only what the project was generated for. Two sources of truth for one question is how a
            # page comes to disagree with the runner.
            #
            # The host check was missing while `load` had it, so a project generated for Claude Code
            # offered an enabled Start button that failed on submit. That is now the ordinary kind of
            # project here rather than an oddity — it is the one Watch exists for — so the row says
            # what can be done with it instead of finding out after the click.
            if (data.get("sdk") or "agy").lower() != "agy":
                entry["why"] = (f"generated for '{data.get('sdk')}' — the orchestrator runs "
                                f"Antigravity projects only. Work it in that host and Watch it here")
            elif not (manifest.parent / "agent.config.json").is_file():
                entry["why"] = ("no agent.config.json — the orchestrator would have no tool policy "
                                "for it. Regenerate it, or open it with a desktop host")
        except Exception as exc:  # noqa: BLE001
            entry["workflow"] = entry["profile"] = "?"
            entry["session"] = False
            entry["why"] = f"unreadable project.json: {exc}"
        found_projects.append(entry)
    return found_projects


def found(registry: Registry, run_id: str) -> Run:
    if (run := registry.get(run_id)) is None:
        raise HTTPException(status_code=404, detail=f"no such run: {run_id}")
    return run


def refuse(request: Request, why: str, form: dict[str, str] | None = None) -> Any:
    """A refusal a visitor can act on, rendered as the page they were on rather than as a stack.

    `form` gives back what they typed. A refused brief that clears the textarea is a refusal that
    costs the visitor their work, which is a worse outcome than whatever was wrong with it.
    """
    return TEMPLATES.TemplateResponse(request, "index.html", {
        "root": request.app.state.root,
        "projects": discover(request.app.state.root),
        "runs": [r.summary() for r in request.app.state.registry.runs],
        "active": (a := request.app.state.registry.active) and a.id,
        "workflows": projects.WORKFLOWS,
        "all_types": projects.ALL_TYPES,
        "refused": why,
        "form": form or {},
    }, status_code=409)
