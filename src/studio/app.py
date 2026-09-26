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

import hashlib
import json
import os
from pathlib import Path
from typing import Any, AsyncIterator

from fastapi import FastAPI, Form, HTTPException, Request, Response
from fastapi.responses import (FileResponse, HTMLResponse, JSONResponse, PlainTextResponse,
                               RedirectResponse)
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from starlette.concurrency import run_in_threadpool
from pygments import highlight
from pygments.formatters import HtmlFormatter
from pygments.lexers import JavascriptLexer, MarkdownLexer
from sse_starlette.sse import EventSourceResponse

from orchestrator import csm
from orchestrator.events import read_events

from . import archive
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


def asset_version() -> str:
    """A token that changes when the stylesheet does, for the `?v=` on its URL.

    **The bug this fixes wasted a reader's time before it was found.** `/static` is served by
    `StaticFiles`, which sets an ETag and a Last-Modified and *no* `Cache-Control` — so a browser
    applies heuristic caching and may hold a stylesheet for hours. After a deploy that adds a rule,
    the page then renders new markup against old CSS: the deliverables panel came back with the
    generic `button` styling on its format controls, complete with that rule's `margin-top`, which
    reads exactly like a layout bug in code that is in fact correct. A stale stylesheet does not look
    like a stale stylesheet; it looks like your CSS not working.

    Content rather than mtime, because a container image's file timestamps are a property of the
    build rather than of the file: two builds of the same stylesheet should not bust the cache, and
    the same mtime on changed content must not fail to.
    """
    try:
        data = (Path(__file__).parent / "static" / "studio.css").read_bytes()
    except OSError:
        return "0"
    return hashlib.sha256(data).hexdigest()[:12]


# Read once at import: the file does not change under a running server, and hashing it per request
# would be a file read on every page for a value that cannot have moved.
TEMPLATES.env.globals["asset_version"] = asset_version()

#: Line numbers because the record refers to scripts by path and a reader refers to them by line.
FORMATTER = HtmlFormatter(style="friendly", cssclass="code", linenos="table", lineanchors="L")

#: What a render can produce, and therefore the whole of what the artifact route will serve.
#:
#: `outFile` writes webp, png or jpeg; `outSvg` writes svg. Nothing else is an artifact, so nothing
#: else is reachable by a visitor-supplied name — see `serve_artifact`, which resolves against the
#: project root rather than a single directory and needs this to keep that boundary narrow.
ARTIFACT_SUFFIXES = frozenset({".webp", ".png", ".jpg", ".jpeg", ".svg"})

#: Prose, so no line numbers. A drawing script is cited by line and a report is not.
PROSE = HtmlFormatter(style="friendly", cssclass="code")

#: What a run *delivers*, as opposed to what it rendered: the documents beside the picture.
#:
#: **Exact filenames rather than a suffix rule, and that is the whole of the boundary here.** The
#: project root is not a deliverables folder — it also holds `GEMINI.md`, which is 75 KB of the
#: studio's own workflow instructions, plus `project.json`, `agent.config.json` and `.agents/`. A
#: `.md` rule would serve the first and read as a deliverable to anyone who found it; a broader one
#: reaches configuration. The image route next door needs containment *plus* a suffix allowlist
#: because it resolves a visitor-supplied path; this needs neither, because a deliverable has no path
#: to resolve.
#:
#: **`documents/` is the case this shape exists to make unreachable.** That folder is the director's
#: own supplied material — a client's unpublished figures, whatever they attached to the brief — and
#: it must never be served to a visitor. Declaring the route with `{name}` rather than `{name:path}`
#: means a name carrying a separator does not match the route at all, so `documents/anything.md` is
#: refused by the router before a handler sees it. That is a stronger guarantee than a check we could
#: forget to write.
#:
#: The names are taken from what the workflows actually ask for, counted across
#: `src/Polson.CLI/ProjectTemplate/*/instructions.md` rather than guessed: `brief.md` and
#: `findings.md` are asked for by every workflow, `artwork.js` by most, and the remaining four by one
#: or two each. A workflow that names a new one has to be added here — deliberately, since the point
#: is that this list is closed.
#:
#: Ordered for reading rather than alphabetically: what was asked, what was made, then what it is
#: worth. That is the order a judge wants them in, and the page shows them in the order given.
DELIVERABLES: tuple[tuple[str, str], ...] = (
    ("brief.md", "the commission, as the agent received it"),
    ("artwork.js", "the drawing itself, as code"),
    ("accuracy.md", "what was checked against a source, and what could not be"),
    ("findings.md", "the agent's own account of friction, gaps and surprise"),
    ("critique_log.md", "the studio's critique of its own passes"),
    ("materials.md", "the materials requisitioned, and what each was for"),
    ("turns.md", "the turn-by-turn record the drawing workflow keeps"),
    ("sketch.md", "the scene in verbs, and every check the sketch was held to"),
)

#: The same list as a set, for the membership test the route makes.
DELIVERABLE_NAMES = frozenset(name for name, _ in DELIVERABLES)


def base(request: Request) -> str:
    """The prefix this app is mounted under, or "" when it is the whole server.

    **Every absolute path the pages emit has to carry this**, because the studio is served two ways
    and only one of them is at the root. Standalone it is `""` and nothing changes; mounted on the
    ADK runtime at `/studio` it is that prefix, and without it the stylesheet, the live event stream
    and every artifact `src` resolve one level too high and 404 — a page that renders and is inert,
    which is the failure mode hardest to notice from a status code.

    Starlette sets `root_path` on a mounted sub-application, so this is read rather than configured;
    a setting would be one more thing that can disagree with where the app actually is.
    """
    return request.scope.get("root_path", "")


def console_app(apps: Path | None, project: str) -> str | None:
    """The console app for `project`, or `None` when there is not one to link to.

    A name rather than a boolean, so the template has nothing to reassemble. `Path(project).name`
    is the containment: the value reaches this from a run's own project id, which is a directory
    name — but this builds a filesystem path and then a URL from it, and a check written here costs
    a line where a check omitted here is the kind that is noticed later.
    """
    if not apps or not project or Path(project).name != project:
        return None
    try:
        return project if (apps / project).is_dir() else None
    except OSError:
        # A name the filesystem itself rejects is simply not an app. Refusing to render the whole
        # run page over a link that would have been omitted anyway is the wrong trade.
        return None


def create_app(root: Path | None = None, registry: Registry | None = None,
               observe_only: bool = False, create_at: str | None = None,
               console_at: str | None = None, console_apps: Path | None = None) -> FastAPI:
    """Builds the app. Takes its collaborators so a test can supply its own.

    `observe_only` is for a host that can read a record but not drive one — the ADK runtime, whose
    environment deliberately does not ship the Antigravity SDK this studio drives with. **It changes
    what the page offers, not only what the routes accept**: a form that cannot work is worse than a
    missing one, because a visitor fills it in before finding out. `create_at` is where projects are
    made instead, and is shown in its place.

    `console_at` is the agent console this studio is mounted beside, or `None` where there is none —
    which is every local checkout, and is why it is passed in rather than assumed. **Root-relative,
    not `base`-relative**: the studio lives at `/studio` and the console is its sibling on the parent
    app, so prefixing it would point at a page inside the studio that does not exist.

    `console_apps` is the directory whose child names are that console's app names, and it is what
    makes the per-run deep link honest. **A project is not always an app.** `archive.restore` fetches
    a project directory back from storage and writes no app package, so on the ADK runtime every
    recovered project — which is most of what a visitor opens — has a run page and no app behind it.
    Linking to `?app=<name>` regardless would send exactly those visitors to a console opening an app
    that is not there. Only the directory knows, so the directory is asked.
    """
    app = FastAPI(title="Polson Studio", docs_url=None, redoc_url=None)
    app.state.root = Path(root or DEFAULT_ROOT).resolve()
    app.state.registry = registry or Registry()
    app.state.observe_only = observe_only
    app.state.create_at = create_at
    app.state.console_at = console_at
    app.state.console_apps = Path(console_apps).resolve() if console_apps else None

    # The stylesheet, and nothing else. Serving a directory of our own files needs no containment
    # check because no visitor-supplied name reaches it — unlike the artifact route below.
    app.mount("/static", StaticFiles(directory=str(Path(__file__).parent / "static")), name="static")

    # region Pages
    @app.get("/", response_class=HTMLResponse)
    async def index(request: Request) -> Any:
        return TEMPLATES.TemplateResponse(request, "index.html", {
            "base": base(request),
            "root": app.state.root,
            "projects": discover(app.state.root),
            # Archived runs whose directory is no longer here. Usually empty, and never empty for
            # the reason that matters: a Cloud Run instance is replaced and takes the projects with
            # it, so this is the only route back to work that was already done and paid for.
            "archived": archive.restorable(app.state.root),
            "runs": [r.summary() for r in app.state.registry.runs],
            "active": (a := app.state.registry.active) and a.id,
            "workflows": projects.WORKFLOWS,
            "all_types": projects.ALL_TYPES,
            "form": {},
            "observe_only": app.state.observe_only,
            "create_at": app.state.create_at,
            "console_at": app.state.console_at,
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

        return RedirectResponse(f"{base(request)}/runs/{run.id}", status_code=303)

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
            return RedirectResponse(f"{base(request)}/", status_code=303)

        try:
            # A project made moments ago has no session to continue, so this is the opening
            # instruction and Registry will supply it.
            run = await app.state.registry.start(made, "")
        except StudioError as exc:
            # The project exists either way; only the run was refused, and saying so is the
            # difference between "try again later" and "your brief is gone".
            return refuse(request, f"Project '{made.name}' was created, but the run was not started: {exc}")

        return RedirectResponse(f"{base(request)}/runs/{run.id}", status_code=303)

    @app.post("/observe")
    async def watch(request: Request, project: str = Form(...)) -> Any:
        """Watches a project someone else is driving, in Claude Code or Claude Desktop.

        Not a run: nothing is started and nothing is spent. The record is already being written —
        the MCP server writes `server.jsonl` whoever drives it, and the `preserve-chatlog` hook
        preserves the conversation — so this only puts a reader on it. See `studio.observe`.
        """
        try:
            chosen = contain(app.state.root, project)

            # **Restore before observing, not instead of it.** A project that vanished with its
            # instance is exactly the one someone is trying to open, and the alternative is a
            # refusal naming a directory that used to exist. Only when it is genuinely absent: a
            # project on disk is never overwritten by an older copy of itself.
            if not (chosen / "project.json").is_file() and archive.available():
                chosen = await run_in_threadpool(archive.restore, chosen.name, app.state.root)

            run = await observe_mod.observe(app.state.registry, chosen)
        except archive.ArchiveError as exc:
            return refuse(request, str(exc))
        except StudioError as exc:
            return refuse(request, str(exc))

        return RedirectResponse(f"{base(request)}/runs/{run.id}", status_code=303)

    @app.get("/runs/{run_id}", response_class=HTMLResponse)
    async def show(request: Request, run_id: str) -> Any:
        run = found(app.state.registry, run_id)
        summary = run.summary()
        return TEMPLATES.TemplateResponse(request, "run.html", {
            "run": summary,
            "base": base(request),
            "console_at": app.state.console_at,
            "console_app": console_app(app.state.console_apps, summary["project"]),
        })
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

    @app.get("/runs/{run_id}/deliverables")
    async def deliverables(run_id: str) -> JSONResponse:
        """Which documents this run has actually produced, in reading order.

        **A listing rather than a set of names the page hardcodes**, because the names differ per
        workflow: `accuracy.md` is the vector-infographic audit, `critique_log.md` is the comic
        studio's, `turns.md` is the drawing workflow's, and no run writes all seven. A page that
        guessed would either show dead links for six of them or show none of the workflow-specific
        ones, and the workflow-specific one is usually the interesting document.

        It is also a listing rather than a bundle: the sizes let the page say what it is offering
        before a visitor spends a request on it, and a document the agent has not written yet is
        simply absent rather than a link that 404s. That distinction matters mid-run — these appear
        as the work proceeds, and `findings.md` is conventionally written last.

        Free, and it reads no contents. `Documents.list()` on the SDK side makes the same split for
        the same reason: finding out what exists must not cost what reading it costs.
        """
        run = found(app.state.registry, run_id)

        written = []

        # The picture first, because it is the work. Everything under it is what the studio *says*
        # about the work, which is worth less to a reader who has not yet seen it.
        picture = final_render(run)
        if picture:
            written.append(picture)

        for name, what in DELIVERABLES:
            try:
                path = contain(run.project.root, name)
            except StudioError:
                # Unreachable for a bare filename, and kept anyway: `contain` resolves before it
                # verifies, so this is also what catches a symlink pointing out of the project.
                continue
            if not path.is_file():
                continue
            written.append({"kind": "document", "name": name, "what": what,
                            "bytes": path.stat().st_size})

        return JSONResponse({"deliverables": written})

    @app.get("/runs/{run_id}/deliverables/{name}")
    async def serve_deliverable(run_id: str, name: str, raw: int = 0) -> Response:
        """One delivered document, highlighted, as a fragment the page drops into the overlay.

        This is the half of a finished run that neither the render nor the trace carries. The render
        is what the work looks like and the trace is how it got there; these are what the studio
        *says about it* — which figure came from which source, what the brief asked for that could
        not be done, where the agent found the environment wanting. A run whose documents are on disk
        and unreachable from the page has done the work and not delivered it.

        **The exact-name check below is what refuses everything; `{name}` rather than `{name:path}`
        is a second guard behind it, and neither is redundant.** The check alone already refuses
        `documents/brief.md`, because that string is not one of the seven names — verified by
        mutating this route to `{name:path}` and finding the tests still passed. What the bare
        parameter adds is that the refusal does not depend on the *shape* of the check: widen the
        list to a suffix rule some day, as the artifact route next door already is, and a path
        parameter would immediately reach `documents/` while this one still cannot route to it.

        Worth saying plainly because the tempting version of this comment is the one that was written
        first, claiming the route shape is what keeps the director's material private. It is not, and
        a guard credited with work it does not do is the kind that gets removed as decoration.
        """
        run = found(app.state.registry, run_id)

        if name not in DELIVERABLE_NAMES:
            # On the name alone, before the disk is touched — as `serve_artifact` does, and for the
            # same reason: whether a file we would never serve exists is not a visitor's to learn.
            raise HTTPException(status_code=404, detail=f"not a deliverable: {name}")

        try:
            path = contain(run.project.root, name)
        except StudioError as exc:
            raise HTTPException(status_code=404, detail=str(exc)) from exc

        if not path.is_file():
            # Distinct from "not a deliverable" in wording only — both are 404, since the difference
            # is not something worth publishing. The listing is how the page avoids asking at all.
            raise HTTPException(status_code=404, detail=f"not written yet: {name}")

        source = path.read_text(encoding="utf-8", errors="replace")
        code = name.endswith(".js")

        # `?raw=1` is what a *shared* link needs to be. The page fetches the highlighted fragment for
        # its overlay, which is right there and useless anywhere else: pasted into a chat or a
        # submission it opens as unstyled markup with no page around it. The raw form is the document
        # itself — readable in a browser, saveable, and diffable — so that is what the row links to
        # and what "copy link" copies.
        if raw:
            return PlainTextResponse(source, media_type="text/plain; charset=utf-8",
                                     headers={"Content-Disposition": f'inline; filename="{name}"'})

        return HTMLResponse(highlight(source, JavascriptLexer() if code else MarkdownLexer(),
                                      FORMATTER if code else PROSE))
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


#: Rasters before vector in the finished-piece row. The first entry is what the page shows when a
#: reader clicks, and a raster is the one that always displays; the SVG is the file they want *after*
#: seeing it. Not a claim about which is the real deliverable — for a vector piece that is the SVG.
FORMAT_ORDER = ("webp", "png", "jpeg", "jpg", "svg")


def final_render(run: Run) -> dict[str, Any] | None:
    """The finished piece: the last render of each format this run produced, as **one** entry.

    **Read from the run record rather than from a filename, because the filename is not knowable.**
    Three of the eight workflows prescribe no name for the final image at all — `vector_infographic`
    among them — so the agent invents one, and a measured run of it delivered `artifacts/final.svg`
    where the convention elsewhere is `output.svg`. A closed list of names would therefore have
    missed the deliverable on exactly the workflow whose deliverable is the point, which is the same
    failure that would have dropped `findings.md` from `DELIVERABLES`.

    Every `render` event names its artifact and its format, so the last of each is the delivered
    picture wherever it was put and whatever it was called.

    **One entry, not one per format.** A raster and a vector of the same artwork are one deliverable
    in two forms; listing them as two rows invites a reader to think the run produced two pieces.
    """
    latest: dict[str, str] = {}
    for event in read_events(run.project.server_events):
        if event.get("type") != "render":
            continue
        # Scoped to this run exactly as the curve is: the event files belong to the project and
        # outlive any one run, so without this an observation shows the previous run's picture.
        if run.since and (event.get("ts") or "") < run.since:
            continue
        fmt, artifact = (event.get("format") or "").lower(), event.get("artifact")
        if fmt and artifact:
            latest[fmt] = artifact                  # later events win, so this ends up the last

    formats = []
    for fmt in sorted(latest, key=lambda f: (FORMAT_ORDER.index(f) if f in FORMAT_ORDER else 99, f)):
        artifact = latest[fmt]
        if Path(artifact).suffix.lower() not in ARTIFACT_SUFFIXES:
            # The artifact route would refuse it, so offering it here would be a link to a 404.
            continue
        try:
            path = contain(run.project.root, artifact)
        except StudioError:
            continue
        if path.is_file():
            formats.append({"format": fmt, "path": artifact, "bytes": path.stat().st_size})

    if not formats:
        return None

    # Named by the stem when the formats share one — `final`, not `final.webp` beside an `svg`
    # button, which reads as though the row were the raster and the vector something else. They
    # usually do share it, because one script writes both with `outFile` and `outSvg`.
    stems = {Path(f["path"]).stem for f in formats}
    name = stems.pop() if len(stems) == 1 else Path(formats[0]["path"]).name

    return {"kind": "render", "name": name, "what": "the finished piece",
            "state": run_state(run), "formats": formats}


def run_state(run: Run) -> str:
    """How this run ended, from `run.end`: `completed`, `halted`, `failed`, or `unknown`.

    **`unknown` is mostly about live runs, not old ones.** A run still in progress has no `run.end`
    either, and this panel fills as the work proceeds — so reading a missing ending as "did not
    finish" would put *the run was halted* under the picture of a run that is at that moment still
    drawing. Absence is the normal mid-run state.

    It also covers runs recorded before `run.end` existed, which is every archived run older than
    2026-09-08. That case matters less; the live one is why the value is here.

    Only a recorded ending is ever asserted, and the page says nothing at all for `unknown`.
    """
    for event in reversed(read_events(run.project.agent_events)):
        if event.get("type") != "run.end":
            continue
        if run.since and (event.get("ts") or "") < run.since:
            continue
        return str(event.get("reason") or "unknown")
    return "unknown"


def has_record(project: Path) -> bool:
    """Whether anything has ever run in this project, judged by its own event record.

    **The one signal that works on both runtimes.** `conversationId` in `project.json` is written by
    the Antigravity orchestrator and by nothing else, so asking it on an ADK project always answers
    no — see the note in `discover`. A generated project has an events directory and no files in it;
    the first thing any run writes, on either path, is a line into one of them.

    Deliberately checks for a non-empty *file* rather than for the directory: the generator creates
    `events/` up front, so its presence says only that the project was made properly. A zero-byte
    file is treated as no record for the same reason — it is what an interrupted create leaves.
    """
    try:
        return any(path.stat().st_size > 0 for path in (project / "events").glob("*.jsonl"))
    except OSError:
        # A project directory that cannot be read is a worse problem than this answer, and
        # `discover` reports it separately from the row's own `why`.
        return False


def discover(root: Path) -> list[dict[str, str]]:
    """Every runnable project under `root`, shallowly.

    See `has_record` for why a project's state is read off the record rather than off `project.json`.

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
            #
            # **`conversationId` alone answered this for the wrong runtime.** It is written by
            # `orchestrator/project.py` and by nothing else — `ProjectGenerator` sets it to null for
            # an orchestratable project and omits it entirely otherwise — so on the ADK runtime it is
            # never populated and every project read "not started" for ever, including ones that had
            # run, drawn, and been killed by a container restart mid-flight. Three of them were
            # sitting on the deployed studio saying they had never begun while their artifacts and
            # all three event logs were in the bucket.
            #
            # The record answers it for both runtimes: a generated project has an **empty** events
            # directory, and the first thing any run writes is a line into one of those files.
            entry["session"] = bool(data.get("conversationId")) or has_record(manifest.parent)

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

    **It has to build the same context the index does, and it did not.** `observe_only` and
    `create_at` were missing, and Jinja renders an undefined name as falsy rather than complaining —
    so on the ADK runtime, where `observe_only` is True, any refusal re-rendered this page with the
    Start button and the prompt box that host cannot honour. A visitor's next click then failed for a
    second, unrelated reason. Add a key to the index and add it here.
    """
    return TEMPLATES.TemplateResponse(request, "index.html", {
        "base": base(request),
        "root": request.app.state.root,
        "projects": discover(request.app.state.root),
        "archived": archive.restorable(request.app.state.root),
        "runs": [r.summary() for r in request.app.state.registry.runs],
        "active": (a := request.app.state.registry.active) and a.id,
        "workflows": projects.WORKFLOWS,
        "all_types": projects.ALL_TYPES,
        "observe_only": request.app.state.observe_only,
        "create_at": request.app.state.create_at,
        "console_at": request.app.state.console_at,
        "refused": why,
        "form": form or {},
    }, status_code=409)
