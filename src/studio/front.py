"""The studio as a person commissioning work sees it.

The developer pages (`/`, `/runs/<id>`) show the machinery: projects, sessions, the record, the
sense-making curve. These show the same things in a client's terms — choose a workflow, say what you
want in which sizes and formats, attach the files to use, and watch it get made — styled by the Polson
Studio design system (`static/polson.css`).

**Nothing here is a second path to the agent.** A request becomes a project through `projects.create`,
which hands the brief to the CLI as a file; a run starts through `Registry.start`; a live request page
reads `/runs/<id>/events` and the artifact and deliverable routes the developer page already uses, with
their containment and allowlists unchanged. The one new file route, `/requests/<name>/thumb`, takes no
visitor path at all: it serves the newest image the project's own record says the engine rendered.

**One file is written beside a project, `request.json`**: the title and choices the person made, so the
request reads as "Harbour festival trailer" rather than as its directory name. It is display data only —
nothing the agent reads — and a project without one falls back to its id.
"""

from __future__ import annotations

import json
import logging
import re
import shutil
import tempfile
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

from fastapi import FastAPI, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import FileResponse, HTMLResponse, RedirectResponse
from fastapi.templating import Jinja2Templates
from starlette.concurrency import run_in_threadpool

from orchestrator.events import read_events

from . import archive, catalogue, projects, techniques, uploads
from . import direction as direction_mod
from . import observe as observe_mod
from .research import is_research, research_executions
from .runs import Registry, Run, StudioError

_logger = logging.getLogger("polson.front")

REQUEST_FILE = "request.json"

#: What the thumbnail route may serve: still images only, never video, never a print PDF.
THUMB_SUFFIXES = frozenset({".webp", ".png", ".jpg", ".jpeg", ".svg"})

#: What the workflows name a finished piece: `output`, `final`, or one per audience `final_a`, `final_b`…
FINAL = re.compile(r"output|final(_[a-z])?")

#: Status words, in the order a list sorts them: what needs you first.
ORDER = {"needs-you": 0, "running": 1, "queued": 2, "draft": 3, "delivered": 4, "stopped": 5}


# region Reading a project as a request
def read_request(project: Path) -> dict[str, Any]:
    """The `request.json` written when the request was composed, or {} for any other project."""
    try:
        data = json.loads((project / REQUEST_FILE).read_text(encoding="utf-8"))
        return data if isinstance(data, dict) else {}
    except (OSError, ValueError):
        return {}


def can_say(run: Run | None) -> bool:
    """Whether words typed on the request page will reach the agent while it works.

    A driven run hears them through its own director. An observed one only where it is an ADK run in this process, whose
    plugin hands them over at its next tool call (`adk_agent.interject`). A Claude Code or Antigravity run watched here has
    no channel: its words would be queued for nobody and recorded as said, which is worse than offering no box at all.
    """
    if run is None or not run.live:
        return False
    return not run.observed or (observe_mod._channel() is not None and run.project.sdk == "adk")


def run_for(registry: Registry, project: Path) -> Run | None:
    """The run to show for a project: a live driven run first, then the newest of any kind."""
    root = project.resolve()
    mine = [r for r in registry.runs if Path(r.project.root).resolve() == root]
    return next((r for r in mine if r.live and not r.observed), None) or (mine[0] if mine else None)


def status_of(project: Path, run: Run | None, events: list[dict[str, Any]],
              in_flight: Callable[[str], bool] | None = None) -> str:
    """One of the six status words, from the run this server holds and the project's own record.

    The record decides for a project this server did not run: no events is a draft, a `run.end` after
    renders is delivered, and anything else that has stopped moving is stopped. A live driven run is
    in the studio, or needs you while a question is open, and so is a project the host says it is working
    (`in_flight`), however long its record has been still.
    """
    if run is not None and run.live and not run.observed:
        director = getattr(run.stream, "director", None)
        return "needs-you" if director is not None and director.pending else "running"
    if in_flight is not None and in_flight(project.name):
        return "needs-you" if asking(project) else "running"
    # A watched run is "live" for a while after its record last moved, so the agent's own ending has to win
    # over that: otherwise a finished ADK run reads as in the studio for minutes after it delivered.
    if run is not None and run.observed and run.live and not agent_ended(project):
        return "needs-you" if asking(project) else "running"
    if run is not None and run.status in ("failed", "incomplete", "cancelled"):
        return "stopped"
    if not events:
        # Watched with nothing written yet is a run commissioned and not yet begun — the ADK runtime starts
        # a request the moment it is composed, and the agent's first event is seconds away. A draft is
        # never watched: the page only registers a watch on a project that has a record.
        return "queued" if run is not None and run.observed else "draft"
    started = [i for i, e in enumerate(events) if e.get("type") == "run.start"]
    after = events[started[-1]:] if started else events
    # The agent's own ending, where its runtime wrote one, outranks the files: a run that hit a quota error
    # after writing output.png stopped, and the live page says so from the same event.
    since = after[0].get("ts", "") if started else ""
    completed = False
    for e in reversed(read_events(project / "events" / "agent.jsonl")):
        if e.get("type") == "run.end" and (e.get("ts") or "") >= since:
            how = e.get("reason") or e.get("status") or ""
            if e.get("error") or how not in ("", "completed", "done"):
                return "stopped"
            completed = True
            break
    # A run this server did not start, whose record is still moving, is running: nightshift1, launched with `claude -p`,
    # read as Stopped on the home page while its server wrote an event every few seconds, because only a watched run
    # was ever called live. Judged by the record's own last timestamp, and only before its end.
    if not agent_ended(project) and not any(e.get("type") == "run.end" for e in after) and recently_moved(after):
        return "running"
    # A finished piece is the evidence that matters: Claude Code ends a run by stopping the MCP server, which
    # writes no run.end, so a run that delivered `final_a.mp4` would otherwise read as stopped.
    if any(e.get("type") == "render" and FINAL.fullmatch(Path(e.get("artifact") or "").stem) for e in after):
        return "delivered"
    # The agent's own completed `run.end` counts as the server's does: on ADK the server writes none while its process
    # lives, and polson_graphics_studio_logo (2026-10-30), completed with a brand sheet, read as Stopped.
    if (completed or any(e.get("type") == "run.end" for e in after)) and any(e.get("type") == "render" for e in events):
        return "delivered"
    return "stopped"


#: How recently a record must have moved for a run this server did not start to read as running. Longer than a model turn
#: or an encode usually leaves it still (minutes, not seconds), shorter than a run that has died and left its record.
LIVE_WINDOW_SECONDS = 300.0


def recently_moved(events: list[dict[str, Any]], now: float | None = None) -> bool:
    """Whether the newest timestamped event is within `LIVE_WINDOW_SECONDS` of `now`. An event with no `ts` says nothing."""
    stamps = [e.get("ts") for e in events if e.get("ts")]
    if not stamps:
        return False
    try:
        newest = datetime.fromisoformat(max(stamps).replace("Z", "+00:00")).timestamp()
    except ValueError:
        return False
    return (now if now is not None else time.time()) - newest <= LIVE_WINDOW_SECONDS


def agent_ended(project: Path) -> bool:
    """Whether the agent's own record ends with its run's end: a `run.end` after its last start.

    Read from `agent.jsonl` alone, and its own start — `run.begin` on ADK, `run.start` on Antigravity —
    rather than the server's: ADK keeps one MCP server per app across runs, so the server's `run.start`
    can belong to an earlier run and would make a new one read as finished.
    """
    last = ""
    for e in read_events(project / "events" / "agent.jsonl"):
        kind = e.get("type")
        if kind in ("run.begin", "run.start"):
            last = "start"
        elif kind == "run.end":
            last = "end"
    return last == "end"


def asking(project: Path) -> bool:
    """Whether an agent in this process is waiting on an answer for `project`.

    Only the ADK runtime's runs are watched *and* askable from here: its question channel holds the future
    the agent awaits, keyed by the project's directory name. A driven run says so through its own director.
    """
    channel = observe_mod._ask_channel()
    try:
        return bool(channel is not None and channel.pending(project.name))
    except Exception:                                            # noqa: BLE001 - a status, never fatal
        return False


def offered(state: Any, card: catalogue.Card) -> dict[int, tuple[str, ...] | None]:
    """The routes of `card` this host can run, by index, each with the types it offers (None: all of them).

    A driving studio runs every route. One that only watches runs none, unless it was given a commissioner,
    which then says per workflow — the ADK runtime offers what its own intake offers, so the composer cannot
    take a request the host will refuse. A route missing here is shown, and cannot be chosen.

    A card that needs audiences runs no route while Qloo is off (`catalogue.taste_available`), whatever the host.
    """
    if card.needs_taste and not catalogue.taste_available():
        return {}
    if not state.observe_only:
        return {i: None for i in range(len(card.routes))}
    if state.commissioner is None:
        return {}
    return {i: types for i, r in enumerate(card.routes)
            if (types := state.commissioner.types(r.workflow)) is not None}


def runnable(state: Any, cards: Any = None) -> list[catalogue.Card]:
    """The cards this host can start, in order: a card none of whose routes it runs is hidden until it does.

    Showing them disabled told a visitor what the studio could not do before what it could (2026-10-30: four of fifteen on
    Cloud Run). A host that runs nothing, one only watching, shows them all, so its pages are the catalogue rather than empty.
    """
    cards = list(catalogue.CARDS if cards is None else cards)
    return [c for c in cards if offered(state, c)] or cards


def still_renders(project: Path, events: list[dict[str, Any]]):
    """The still images the record says were rendered, newest first, inside the project and never from `documents/`,
    whether or not they are on disk."""
    root = project.resolve()
    executions = research_executions(events)
    for event in reversed(events):
        if event.get("type") != "render" or (event.get("motion") or "").lower() in ("sheet", "board"):
            continue
        if is_research(event, executions):
            continue                                # a sheet of Qloo's pictures is never a request's face (research.py)
        artifact = event.get("artifact") or ""
        if Path(artifact).suffix.lower() not in THUMB_SUFFIXES:
            continue
        candidate = (root / artifact).resolve()
        if root not in candidate.parents:
            continue
        if candidate.relative_to(root).as_posix().split("/", 1)[0].lower() in ("documents",):
            continue
        yield candidate


def latest_render(project: Path, events: list[dict[str, Any]]) -> Path | None:
    """The newest still image the record says was rendered, if it is still on disk and inside the project."""
    return next((c for c in still_renders(project, events) if c.is_file()), None)


def describe(project: Path, registry: Registry) -> dict[str, Any]:
    """A project as one row of the requests list."""
    record = read_request(project)
    try:
        manifest = json.loads((project / "project.json").read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        manifest = {}
    workflow = record.get("workflow") or manifest.get("workflow") or ""
    # A request made here names its card. A run made elsewhere names only its workflow and type, and the type is what
    # tells a product ad from a film trailer: both are `motion`.
    card = (catalogue.BY_SLUG.get(record.get("card", ""))
            or catalogue.card_for(workflow, record.get("type") or manifest.get("type") or ""))
    events = read_events(project / "events" / "server.jsonl")
    run = run_for(registry, project)

    stamp = events[-1].get("ts", "") if events else ""
    if not stamp:
        try:
            stamp = datetime.fromtimestamp((project / "project.json").stat().st_mtime, timezone.utc).isoformat()
        except OSError:
            stamp = ""

    return {
        "id": project.name,
        "title": record.get("title") or project.name,
        "workflow": card.title if card else (workflow or "Unknown"),
        "kind": card.kind if card else "image",
        "status": status_of(project, run, events, getattr(registry, "in_flight", None)),
        "updated": stamp,
        "updated_label": when(stamp),
        "thumb": latest_render(project, events) is not None,
        # The same test `discover` makes: the orchestrator runs Antigravity projects with a tool policy only.
        # Anything else is watched here and worked in its own host, so the page offers no Start or change.
        "runnable": (manifest.get("sdk") or "agy").lower() == "agy" and (project / "agent.config.json").is_file(),
        "record": record,
        "card": card,
        "run": run,
        "events": events,
    }


def requests(root: Path, registry: Registry) -> list[dict[str, Any]]:
    """Every project under `root` as a request, needs-you first, then newest."""
    rows = [describe(m.parent, registry) for m in Path(root).glob("*/project.json")]
    rows.sort(key=lambda r: r["updated"], reverse=True)
    rows.sort(key=lambda r: ORDER.get(r["status"], 9))
    return rows


def when(stamp: str) -> str:
    """'Oct 18, 11:02' from an ISO timestamp, or '' when there is none."""
    try:
        moment = datetime.fromisoformat(stamp.replace("Z", "+00:00"))
    except ValueError:
        return ""
    return moment.strftime("%b %d, %H:%M").replace(" 0", " ")


def learned_from(project: Path, run: Run | None, events: list[dict[str, Any]]) -> dict[str, Any]:
    """What the run learned from its audiences, or {} before it has fetched a profile or written `direction.md`."""
    try:
        text = (project / "direction.md").read_text(encoding="utf-8", errors="replace")
    except OSError:
        text = ""
    found = direction_mod.learned(text, events, run.since if run else None)
    return found if (found["compare"] or found["audiences"]) else {}


def unique_id(root: Path, title: str) -> str:
    """A free project id derived from the title: `harbour-festival-trailer`, then `-2`, `-3`…"""
    base_id = catalogue.slugify(title)
    for n in range(1, 100):
        candidate = base_id if n == 1 else f"{base_id[:44]}-{n}"
        if not (root / candidate).exists():
            return candidate
    raise StudioError("There are too many requests with that name; choose another.")
# endregion


# region Requests that outlived their instance
# A Cloud Run instance is replaced when it idles or a revision rolls out, and its disk goes with it: every request
# made there vanished from these pages, though `adk_agent/mirror.py` had copied each one to the bucket. They are
# rebuilt from there, and nothing on the page says so: a request is a request, wherever its files were last.
#
# **In two steps, because the disk is memory.** Listing restores only what a row is read from (`archive.light`, plus
# the thumbnail); opening a request restores the rest. Restoring every archived request whole to draw one list would
# spend memory on videos nobody opened.

#: Requests restored as far as their row and no further, by name.
_partial: set[str] = set()
_rebuilding = threading.Lock()
_checked_at = float("-inf")

#: How long a look at the archive stands, so a page load is not a bucket listing.
RECHECK_SECONDS = 60.0


def rebuild_listed(root: Path) -> None:
    """Brings back, as far as the request list needs, every archived request this instance does not have."""
    global _checked_at
    if not archive.ARCHIVE_URI or time.monotonic() - _checked_at < RECHECK_SECONDS:
        return
    with _rebuilding:
        if time.monotonic() - _checked_at < RECHECK_SECONDS:
            return
        _checked_at = time.monotonic()
        for name in archive.restorable(root):
            try:
                project = archive.restore(name, root, only=archive.light)
                _partial.add(name)
                thumb = next(still_renders(project, read_events(project / "events" / "server.jsonl")), None)
                if thumb is not None:
                    wanted = thumb.relative_to(project.resolve()).as_posix()
                    archive.restore(name, root, only=lambda rel, wanted=wanted: rel in (wanted, "project.json"))
            except Exception as exc:                             # noqa: BLE001 - one bad request never hides the rest
                _logger.warning("polson front: could not bring back %s for the list (%s)", name, exc)


def rebuild_request(root: Path, name: str) -> None:
    """Makes `name` whole on this disk before it is shown: the rest of a listed one, or all of one not seen yet."""
    if not archive.ARCHIVE_URI:
        return
    with _rebuilding:
        here = (Path(root) / name / "project.json").is_file()
        if here and name not in _partial:
            return
        if not here and name not in archive.names():
            return
        try:
            archive.restore(name, root)
            _partial.discard(name)
        except Exception as exc:                                 # noqa: BLE001 - the page then says it is not found
            _logger.warning("polson front: could not bring back %s (%s)", name, exc)
# endregion


def install(app: FastAPI, templates: Jinja2Templates, base, contain) -> None:
    """Adds the user-facing pages to the studio app. `base` and `contain` are `app.base` and `app.contain`."""

    notices: dict[str, str] = {}

    # The workflow cards' pictures that are actually here. They are not in git, so a fresh clone has none,
    # and a card naming a missing one shows its kind icon rather than a broken image. Read once: the
    # directory does not change under a running server.
    gallery = Path(__file__).parent / "static" / "gallery"
    templates.env.globals["gallery"] = frozenset(p.name for p in gallery.glob("*") if p.is_file())
    # A request's design techniques are recorded by id; the page names them.
    templates.env.globals["technique_names"] = techniques.names

    def page(request: Request, name: str, nav: str, status_code: int = 200, **context: Any) -> HTMLResponse:
        state = request.app.state
        # Questions open right now, from the runs in memory rather than a scan of every project.
        waiting = sum(1 for r in state.registry.runs if r.live and not r.observed
                      and getattr(getattr(r.stream, "director", None), "pending", None))
        waiting += sum(1 for r in state.registry.runs if r.live and r.observed and asking(Path(r.project.root)))
        return templates.TemplateResponse(request, f"front/{name}", {
            "base": base(request), "nav": nav, "waiting_count": waiting,
            "observe_only": state.observe_only, "create_at": state.create_at,
            "active": state.registry.active, **context,
        }, status_code=status_code)

    def project_dir(request: Request, name: str) -> Path:
        try:
            path = contain(request.app.state.root, name)
        except StudioError as exc:
            raise HTTPException(status_code=404, detail=str(exc)) from exc
        if path.parent != Path(request.app.state.root).resolve() or not (path / "project.json").is_file():
            raise HTTPException(status_code=404, detail=f"no such request: {name}")
        return path

    # region Pages
    @app.get("/home", response_class=HTMLResponse)
    async def home(request: Request) -> Any:
        await run_in_threadpool(rebuild_listed, Path(request.app.state.root))
        rows = requests(request.app.state.root, request.app.state.registry)
        return page(request, "home.html", "home", rows=rows[:6],
                    waiting=[r for r in rows if r["status"] == "needs-you"],
                    working=[r for r in rows if r["status"] == "running"],
                    drafts=[r for r in rows if r["status"] == "draft"][:3],
                    # Every card in HOME, imaged or not: the macro draws a kind icon in a missing image's place, so the rows hold.
                    cards=runnable(request.app.state, [catalogue.BY_SLUG[s] for s in catalogue.HOME]))

    @app.get("/workflows", response_class=HTMLResponse)
    async def workflows(request: Request) -> Any:
        return page(request, "workflows.html", "new", cards=runnable(request.app.state))

    @app.get("/compose/{slug}", response_class=HTMLResponse)
    async def compose(request: Request, slug: str) -> Any:
        if slug in catalogue.RENAMED:
            return RedirectResponse(f"{base(request)}/compose/{catalogue.RENAMED[slug]}", status_code=301)
        card = catalogue.BY_SLUG.get(slug)
        if card is None:
            raise HTTPException(status_code=404, detail=f"no such workflow: {slug}")
        return composer(request, card, {})

    def composer(request: Request, card: catalogue.Card, form: dict[str, Any], refused: str = "",
                 status_code: int = 200) -> HTMLResponse:
        state = request.app.state
        route = int(form.get("route") or 0) if str(form.get("route") or "0").isdigit() else 0
        route = min(route, len(card.routes) - 1)
        runs = offered(state, card)
        if runs and route not in runs:
            route = min(runs)
        styles = {i: [(v, label) for v, label in catalogue.types_for(card, r.workflow)
                      if i in runs and (runs[i] is None or v in runs[i]) and not card.fixed_type]
                  for i, r in enumerate(card.routes)}
        # The design techniques each route's workflow offers (techniques.py): the "Graphic design" field, per route as
        # the styles are, since a technique belongs to a workflow.
        designs = {i: techniques.for_workflow(r.workflow) for i, r in enumerate(card.routes)}
        return page(request, "compose.html", "new", status_code=status_code, card=card,
                    sizes=catalogue.SIZES[card.kind], formats=catalogue.FORMATS[card.kind],
                    silent_preview=catalogue.SILENT_PREVIEW,
                    styles=styles, offered=runs, designs=designs,
                    can_make=not state.observe_only or state.commissioner is not None,
                    drafts=not state.observe_only,
                    taste=catalogue.taste_available(),
                    form=form, route=route, refused=refused, ages=catalogue.AGE_BANDS, languages=catalogue.LANGUAGES,
                    checked_by_eye=catalogue.checked_by_eye(), eye_note=catalogue.EYE_NOTE,
                    max_audiences=catalogue.MAX_AUDIENCES, example_places=catalogue.EXAMPLE_PLACES,
                    accept=",".join(sorted(uploads.DOCUMENT_SUFFIXES)), max_files=uploads.MAX_FILES)

    @app.get("/requests", response_class=HTMLResponse)
    async def listing(request: Request, view: str = "active") -> Any:
        await run_in_threadpool(rebuild_listed, Path(request.app.state.root))
        rows = requests(request.app.state.root, request.app.state.registry)
        groups = {"active": [r for r in rows if r["status"] in ("needs-you", "running", "queued")],
                  "drafts": [r for r in rows if r["status"] == "draft"],
                  "done": [r for r in rows if r["status"] in ("delivered", "stopped")]}
        view = view if view in groups else "active"
        return page(request, "requests.html", "requests", rows=groups[view], view=view,
                    counts={k: len(v) for k, v in groups.items()})

    @app.get("/requests/{name}", response_class=HTMLResponse)
    async def show(request: Request, name: str) -> Any:
        await run_in_threadpool(rebuild_request, Path(request.app.state.root), name)
        project = project_dir(request, name)
        row = describe(project, request.app.state.registry)
        run = row["run"]

        # A finished project this server never ran has no run id, and the artifact and deliverable
        # routes are keyed by one. Watching it is free and read-only, so it is how those routes open.
        if run is None and row["events"]:
            try:
                run = await observe_mod.observe(request.app.state.registry, project)
            except StudioError:
                run = None

        brief = ""
        try:
            brief = (project / "brief.md").read_text(encoding="utf-8", errors="replace")
        except OSError:
            pass
        # The brief as a document, with the renderer the Deliverables reader uses: raw HTML and images off, so what
        # a visitor typed into it arrives as text. Imported here because `app` imports this module.
        from . import app as app_mod
        brief_html = app_mod.MARKDOWN.render(brief) if brief and app_mod.MARKDOWN is not None else ""
        # The commission's deadline, for the Progress card's clock: what the project was given, as `project.json` has it.
        try:
            deadline = int(json.loads((project / "project.json").read_text(encoding="utf-8")).get("deadlineMinutes") or 0)
        except (OSError, ValueError, TypeError):
            deadline = 0

        # A driven run answers through its own director. An observed one only where the agent is in this
        # process (the ADK runtime); elsewhere the host owns the question and the page shows it read-only.
        answerable = run is not None and (not run.observed or observe_mod._ask_channel() is not None)
        return page(request, "request.html", "requests", row=row, run=run, brief=brief, brief_html=brief_html,
                    answerable=answerable, deadline=deadline,
                    sayable=can_say(run),
                    card=row["card"],
                    learned=learned_from(project, run, row["events"]),
                    notice=notices.pop(name, ""))

    @app.get("/requests/{name}/taste")
    async def taste(request: Request, name: str) -> Any:
        """The Audiences, Contrast and Choices views' data: profiles, contrasts and the evidence map, as JSON."""
        await run_in_threadpool(rebuild_request, Path(request.app.state.root), name)
        project = project_dir(request, name)
        row = describe(project, request.app.state.registry)
        try:
            text = (project / "direction.md").read_text(encoding="utf-8", errors="replace")
        except OSError:
            text = ""
        run = row["run"]
        return direction_mod.taste_views(text, row["events"], run.since if run else None)

    @app.get("/requests/{name}/learned", response_class=HTMLResponse)
    async def learned(request: Request, name: str) -> Any:
        """The learned panel alone, for the page to swap in once a run has written its direction."""
        await run_in_threadpool(rebuild_request, Path(request.app.state.root), name)
        project = project_dir(request, name)
        row = describe(project, request.app.state.registry)
        return templates.TemplateResponse(request, "front/learned.html",
                                          {"learned": learned_from(project, row["run"], row["events"])})

    @app.get("/requests/{name}/thumb")
    async def thumb(request: Request, name: str) -> FileResponse:
        project = project_dir(request, name)
        path = latest_render(project, read_events(project / "events" / "server.jsonl"))
        if path is None:
            raise HTTPException(status_code=404, detail="nothing rendered yet")
        return FileResponse(path, headers={"Cache-Control": "no-cache", "X-Content-Type-Options": "nosniff"})
    # endregion

    # region Actions
    @app.post("/requests")
    async def create(request: Request,
                     card: str = Form(...), title: str = Form(""), route: str = Form("0"),
                     kind: str = Form(""), sizes: list[str] = Form([]), custom_size: str = Form(""),
                     formats: list[str] = Form([]), brief: str = Form(""), action: str = Form("save"),
                     aud_place: list[str] = Form([]), aud_age: list[str] = Form([]), aud_lang: list[str] = Form([]),
                     auto: str = Form(""), files: list[UploadFile] = File([])) -> Any:
        state = request.app.state
        form_data = await request.form()
        extras = {k[2:]: str(v) for k, v in form_data.items() if k.startswith("x_")}
        # "Decide everything": the project is made with `--auto`, so the agent asks nothing and waits on no one.
        autonomous = bool(auto.strip())
        form = {"title": title, "route": route, "kind": kind, "sizes": sizes, "custom_size": custom_size,
                "formats": formats, "brief": brief, "extras": extras, "auto": "1" if autonomous else "",
                "audiences": [{"place": p, "age": aud_age[i] if i < len(aud_age) else "",
                               "lang": aud_lang[i] if i < len(aud_lang) else ""}
                              for i, p in enumerate(aud_place)],
                "techniques": techniques.states(form_data)}

        chosen = catalogue.BY_SLUG.get(card)
        if chosen is None:
            raise HTTPException(status_code=404, detail=f"no such workflow: {card}")
        if state.observe_only:
            if state.commissioner is None:
                return composer(request, chosen, form, "This studio can watch requests but not make them. "
                                "Use the create page instead.", 409)
            return await commissioned(request, chosen, form, title, route, kind, sizes, custom_size, formats,
                                      extras, brief, aud_place, aud_age, files, autonomous, aud_lang)

        scratch = Path(tempfile.mkdtemp(prefix="polson-request-"))
        try:
            commission = catalogue.parse(card, title, route, kind, sizes, custom_size, formats, extras, brief,
                                         aud_place, aud_age, aud_lang)
            commission.request, commission.deny = techniques.parse(commission.workflow, form["techniques"])
            staged = await uploads.stage(files, scratch)
            commission.card.check_files([p.name for p in staged])
            project_id = unique_id(state.root, commission.title)
            made = await projects.create(state.root, project_id, commission.workflow, commission.kind,
                                         commission.brief_text([p.name for p in staged]), auto=autonomous,
                                         request=commission.request, deny=commission.deny)
        except StudioError as exc:
            shutil.rmtree(scratch, ignore_errors=True)
            note = " Files have to be chosen again." if any((f.filename or "").strip() for f in files) else ""
            return composer(request, chosen, form, f"{exc}{note}", 409)

        try:
            if staged:
                documents = made / "documents"
                documents.mkdir(exist_ok=True)
                for path in staged:
                    shutil.copy2(path, documents / path.name)
            record = commission.record()
            record["files"] = [p.name for p in staged]
            record["created"] = datetime.now(timezone.utc).isoformat(timespec="seconds")
            (made / REQUEST_FILE).write_text(json.dumps(record, indent=2), encoding="utf-8")
        finally:
            shutil.rmtree(scratch, ignore_errors=True)

        if action == "start":
            try:
                await state.registry.start(made, "")
            except StudioError as exc:
                notices[made.name] = f"Saved as a draft, but not started: {exc}"

        return RedirectResponse(f"{base(request)}/requests/{made.name}", status_code=303)

    async def commissioned(request: Request, chosen: catalogue.Card, form: dict[str, Any], title: str,
                           route: str, kind: str, sizes: list[str], custom_size: str, formats: list[str],
                           extras: dict[str, str], brief: str, aud_place: list[str], aud_age: list[str],
                           files: list[UploadFile], autonomous: bool = False, aud_lang: list[str] | None = None) -> Any:
        """The composer on a host that commissions rather than drives: the ADK runtime.

        The commission is checked, staged and named here and made by the host's commissioner, which applies
        every check its own intake form does — name, offered workflow and type, deadline ceiling, spend brake —
        and starts the run. Always started: this host has no driver to start a draft later, nor a session
        to continue, so neither is offered.
        """
        state = request.app.state
        commissioner = state.commissioner
        scratch = Path(tempfile.mkdtemp(prefix="polson-request-"))
        try:
            commission = catalogue.parse(chosen.slug, title, route, kind, sizes, custom_size, formats, extras,
                                         brief, aud_place, aud_age, aud_lang)
            commission.request, commission.deny = techniques.parse(commission.workflow, form.get("techniques", {}))
            types = commissioner.types(commission.workflow)
            if types is None:
                raise StudioError(f"This studio does not make {chosen.title.lower()} requests yet.")
            if commission.kind and commission.kind not in types:
                raise StudioError(f"That {chosen.style_label.lower()} is not offered here. "
                                  f"Choose one of: {', '.join(types)}.")
            # One type is not a choice, as on the intake form: what it would have chosen anyway is chosen. **The
            # card's types, not the workflow's**: a film trailer's card offers none, though its workflow has one.
            offered = [t for t, _ in catalogue.types_for(chosen, commission.workflow) if t in types]
            if not commission.kind and len(offered) == 1:
                commission.kind = offered[0]
            elif not commission.kind and chosen.default_type in offered:
                commission.kind = chosen.default_type
            staged = await uploads.stage(files, scratch)
            commission.card.check_files([p.name for p in staged])
            name = commissioner.free_name(commission.title)
            await commissioner.commission(name, commission.workflow, commission.kind,
                                          commission.brief_text([p.name for p in staged]), staged, auto=autonomous,
                                          request=commission.request, deny=commission.deny)
        except Exception as exc:                                 # noqa: BLE001 - refusals carry a status
            status = 409 if isinstance(exc, StudioError) else getattr(exc, "status", None)
            if status is None:
                raise
            note = " Files have to be chosen again." if any((f.filename or "").strip() for f in files) else ""
            return composer(request, chosen, form, f"{exc}{note}", status)
        finally:
            shutil.rmtree(scratch, ignore_errors=True)

        # Display data beside the project, as on a driving studio. The project is already running, so this
        # is best effort: a request page without it falls back to the project's name.
        made = Path(state.root) / name
        try:
            record = commission.record()
            record["files"] = [p.name for p in staged]
            record["created"] = datetime.now(timezone.utc).isoformat(timespec="seconds")
            (made / REQUEST_FILE).write_text(json.dumps(record, indent=2), encoding="utf-8")
        except OSError:
            pass
        return RedirectResponse(f"{base(request)}/requests/{name}", status_code=303)

    @app.post("/requests/{name}/start")
    async def start(request: Request, name: str, change: str = Form("")) -> Any:
        """Starts a draft, or asks for a change to finished work by continuing its session."""
        project = project_dir(request, name)
        if request.app.state.observe_only:
            notices[name] = "This studio can watch requests but not run them."
        else:
            change = change.strip()
            try:
                if change:
                    await request.app.state.registry.start(project, change, resume=True)
                else:
                    await request.app.state.registry.start(project, "", resume=False)
            except StudioError as exc:
                notices[name] = str(exc)
        return RedirectResponse(f"{base(request)}/requests/{name}", status_code=303)
    # endregion
