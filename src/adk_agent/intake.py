"""Taking a commission from a visitor: a brief, and the document it refers to.

**Why this is not the ADK console.** The console can attach a file — it has `onFileSelect` and sends
it as an `inlineData` part — but an attached file becomes a *part in the conversation*, never a file
on disk. `Documents.ask(path, question)` reads the project directory, so it cannot see one; the agent
would hold the PDF and be unable to hand it to the toolkit, with no provenance, no scan for text
addressed to whoever is processing it, and no budget accounting. A document has to land in
`documents/` to be usable, and that is what this does.

**Why it lives here rather than in `src/webapp`.** `main.py` returns an ordinary `FastAPI` precisely
so a UI can share its app and its port, and this is the first thing mounted on that seam. It is
deliberately the *narrow* half: a form, an upload, and a redirect into the console. The studio's
observe page — the tailer, the broker, the sense-making curve — is a separate mount that reuses
`src/webapp` as it stands, and it needs a transcript writer on the ADK side before it has anything
to show.

**Everything here treats the visitor as a stranger**, because on a hosted URL that is what they are
(Milestone 6 §3). The brief is passed to the CLI, which sanitises it into `brief.md` as data; the
upload is checked for size, for a type the studio can actually declare, and for a filename that
cannot escape the directory it is written to.
"""

from __future__ import annotations

import asyncio
import os
import time
import logging
import re
import shutil
import tempfile
from collections import deque
from html import escape
from pathlib import Path
from xml.sax.saxutils import quoteattr

import httpx
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import HTMLResponse, RedirectResponse

from newproject import DOCUMENT_SUFFIXES, VALID_APP_NAME, GenerateError, create

_logger = logging.getLogger("polson.intake")

#: Largest upload accepted, in bytes.
#:
#: Below `Documents.MaxInlineBytes` (18 MB) on purpose: a document larger than the reader will accept
#: is one a visitor waits to upload and then cannot use, and refusing at the door is the kinder of the
#: two failures. Enforced by counting bytes as they arrive rather than by trusting `content-length`,
#: which is a header a client writes.
MAX_UPLOAD_BYTES = 16 * 1024 * 1024

#: Read size for the streaming copy. Small enough that an oversized upload is abandoned early.
CHUNK = 64 * 1024

#: A length bound, and nothing else. **What a name may *contain* is `VALID_APP_NAME`'s business** —
#: it is an ADK constraint, so it belongs with the code that writes the app.
#:
#: This module used to keep its own copy, described as mirroring that rule. It did not: the copy
#: allowed a dash, ADK does not, and ADK refuses at **run** time rather than at load time — so the
#: form accepted `boxoffice-2`, staged the visitor's uploaded PDF into it, served it in
#: `/list-apps`, opened it in the console, and answered the first message with a 404. Importing the
#: rule rather than restating it is what stops the two drifting again.
MAX_NAME = 48

#: Whose sessions these are. One director per host; ADK scopes sessions by user id.
#:
#: **`user` rather than `director`, because that is what the dev UI looks under.** ADK's own console
#: requests `/apps/<app>/users/user/sessions` with no way to change it from the page, so a session
#: created under any other id is invisible there — the app appears with an empty session list while
#: the run is going, which reads as a run that never started. Measured on kubrick8: the run was five
#: turns in and the console showed nothing.
#:
#: The descriptive name cost more than it bought. Nothing keys off the value — `launch` writes it and
#: reads it back, and that is all — so the studio page, the record and the transcript are unaffected.
USER = "user"

#: Workflows offered on the form, each with the types it offers. Not the full set: these are the
#: ones whose instructions tell an agent to look for documents, and offering a workflow that ignores
#: an upload would be worse than not offering it.
#:
#: **A type is a `type.<name>.md` file in the workflow's template**, so this table mirrors a set that
#: lives somewhere else — and hardcoding it is deliberate, because no visitor string should ever
#: select a template by name. A mirror drifts silently, though: a type added to the template simply
#: never appears here and nobody finds out. `WorkflowCatalogueTests` compares the two and names the
#: difference. It cannot be read from disk at runtime — the templates are *embedded resources* in
#: `Polson.CLI.dll`, so a container has the DLL and no `ProjectTemplate/` tree at all.
WORKFLOWS: dict[str, tuple[str, ...]] = {
    "vector_infographic": ("blueprint", "brutalist", "editorial", "specimen", "swiss"),
    "infographic": ("blueprint", "brutalist", "editorial", "specimen", "swiss"),
}

#: Every type any offered workflow has, for building the form's one type control. Derived rather
#: than listed, so the control and the table above cannot disagree about what exists.
ALL_TYPES: tuple[str, ...] = tuple(sorted({t for types in WORKFLOWS.values() for t in types}))


#: Ready-made commissions, offered on the form as one-click starters.
#:
#: **Why they exist.** A visitor handed a URL and an empty textarea has to invent a brief before they
#: can see anything, and inventing one is the slowest part — the old standalone webapp got this right
#: by letting a director click through a workflow rather than type it. These restore that, and steer
#: the demo toward the industries it is aimed at, without narrowing what anyone may ask for: the
#: field stays free and nothing is refused.
#:
#: **Every one is a subject these two workflows can actually do.** That is the constraint that
#: matters and it is easy to get wrong — `vector_infographic` and `infographic` make *graphics that
#: encode quantities*, so a title card or a key-art comp would be a starter that reliably produces a
#: poor run. The industry is in the **subject**, not in the form.
#:
#: **None of them states a figure.** A brief carrying invented numbers is the one thing the whole
#: research and document apparatus exists to prevent — `polson://manual/13` and the `Research`
#: CAUTION both say so — and a starter that supplied them would be teaching the agent to draw a
#: sourced-looking graphic from nothing. Each asks for something the agent must research, or read out
#: of a document the director attaches.
STARTERS: tuple[dict[str, str], ...] = (
    {
        "label": "Box office, 2025",
        "name": "boxoffice2025",
        "workflow": "vector_infographic",
        "kind": "editorial",
        "brief": "The 2025 theatrical box office: how the year's ten highest-grossing films earned, "
                 "by studio and by release window. Show opening weekend against total domestic "
                 "gross, and make the difference between a wide opening and a platform release "
                 "legible at a glance.",
    },
    {
        "label": "Where a TV spot's budget goes",
        "name": "spotbudget",
        "workflow": "vector_infographic",
        "kind": "swiss",
        "brief": "Where the money goes in a national 30-second television commercial: production, "
                 "director and talent, post and grade, music licensing, and the media buy, as shares "
                 "of the total. Research typical splits and cite them. Call out the two line items "
                 "clients are most often surprised by.",
    },
    {
        "label": "How a film earns out",
        "name": "earnout",
        "workflow": "infographic",
        "kind": "blueprint",
        "brief": "How a mid-budget feature earns out over eighteen months across theatrical, "
                 "premium video on demand, streaming licence and television. One timeline, with the "
                 "break-even point marked and each window's contribution shown against it.",
    },
    {
        "label": "A launch campaign, week by week",
        "name": "campaign",
        "workflow": "vector_infographic",
        "kind": "brutalist",
        "brief": "An eight-week campaign for a streaming series launch: teaser, full trailer, "
                 "out-of-home, paid social, press junket and premiere. Show what runs when and how "
                 "the spend is weighted across channels.",
    },
    {
        "label": "Genre share, ten years apart",
        "name": "genreshare",
        "workflow": "infographic",
        "kind": "specimen",
        "brief": "Genre share of the hundred highest-grossing films, 2015 against 2025. One plate, "
                 "one cell per film, arranged so the shift between the two years reads without a "
                 "caption explaining it.",
    },
    {
        "label": "The shape of a shoot day",
        "name": "shootday",
        "workflow": "vector_infographic",
        "kind": "blueprint",
        "brief": "The shape of a commercial shoot day, call time to wrap: which departments are on "
                 "set when, where the waiting happens, and how the hours actually distribute across "
                 "camera, grip and electric, art, wardrobe and talent.",
    },
)


def safe_filename(raw: str | None) -> str:
    """The name to write, with every route out of the directory removed.

    `UploadFile.filename` is attacker-controlled: it arrives in a multipart header and nothing
    validates it. `..\\..\\etc\\passwd`, an absolute path, an NTFS alternate data stream and a name
    that is nothing but dots are all things a client can send, and `folder / name` would honour the
    first two. Taking the basename of *both* separator conventions — a POSIX server must still reject
    a Windows path, since the attacker chooses the separator, not the host — and then allowing only a
    conservative character set leaves nothing that can traverse.
    """
    name = (raw or "").replace("\\", "/").split("/")[-1].strip()

    # Windows drops a trailing dot or space, so "a.pdf." and "a.pdf " resolve to "a.pdf" *after* any
    # check that read them literally. Stripping them here means the name that is checked is the name
    # that lands.
    name = name.rstrip(". ")

    if not name or set(name) <= {"."}:
        raise HTTPException(400, "The uploaded file has no usable name.")

    # A conservative allowlist rather than a denylist of bad characters: a colon is an NTFS stream,
    # a null truncates a path in some layers, and the set of things worth excluding is not knowable.
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._ -]{0,127}", name):
        raise HTTPException(400,
            f"{name!r} is not a usable file name. Use letters, digits, spaces, dots, dashes and "
            "underscores.")

    if Path(name).suffix.lower() not in DOCUMENT_SUFFIXES:
        raise HTTPException(400,
            f"{Path(name).suffix or 'that'} is not a type the studio can read. Supported: "
            + ", ".join(sorted(DOCUMENT_SUFFIXES)))

    return name


async def stage_upload(upload: UploadFile, into: Path) -> Path:
    """Streams an upload into `into`, under its own sanitised name, refusing it past the cap.

    **Staged under the real name, in a directory of its own.** An earlier version wrote to a
    `NamedTemporaryFile` and handed back that path, so the document arrived in the project as
    `tmpi7tswuc1.csv` — `stage_documents` copies using the source's name, and the sanitised one had
    been computed, checked, and then not used. The name a visitor sees in `Documents.list()` is the
    name they uploaded.

    Streamed rather than `await upload.read()`: reading first and checking the length afterwards
    means a visitor can spend the server's memory before being refused, which on a public URL is the
    whole attack.
    """
    name = safe_filename(upload.filename)
    staged = into / name

    written = 0
    with staged.open("wb") as out:
        while chunk := await upload.read(CHUNK):
            written += len(chunk)
            if written > MAX_UPLOAD_BYTES:
                out.close()
                staged.unlink(missing_ok=True)
                raise HTTPException(413,
                    f"{name} is larger than the {MAX_UPLOAD_BYTES // (1024 * 1024)} MB limit.")
            out.write(chunk)

    if written == 0:
        staged.unlink(missing_ok=True)
        raise HTTPException(400, f"{name} is empty.")

    _logger.info("intake: staged %s (%d bytes)", name, written)
    return staged


#: What the agent is told first. The brief is already in `brief.md`; this only points at it, so the
#: director's words reach the agent by reference rather than by being restated here.
OPENING = "Begin. Read brief.md and work the project through to a finished deliverable."

#: Runs in flight, held so the event loop does not collect a task nobody is awaiting. A run outlives
#: the request that started it by many minutes, which is the whole reason it is a task at all.
_running: set[asyncio.Task] = set()

#: How many runs this host will start in a rolling 24 hours, and how many at once.
#:
#: **The per-run budgets do not add up to a limit.** `POLSON_BUDGET_TOKENS`, `POLSON_BUDGET_MINUTES`
#: and the circuit breaker each bound *one* run; nothing bounded how many runs a stranger could
#: start. On a public URL that is an open door onto a metered image service and a metered model —
#: the cost surface `CLAUDE.md` Milestone 6 §4 names and nothing implemented.
#:
#: A spend brake, **not a security control**: it is global rather than per visitor, because behind
#: Cloud Run the only per-visitor key is an IP, and rate-limiting by IP is both easy to evade and a
#: kind of fingerprinting this project has no reason to do. One number, applied to everyone.
MAX_RUNS_PER_DAY = int(os.environ.get("POLSON_MAX_RUNS_PER_DAY", "25"))
MAX_CONCURRENT_RUNS = int(os.environ.get("POLSON_MAX_CONCURRENT_RUNS", "2"))

#: When each run was started, newest last. In memory on purpose — the deployment runs one instance
#: and its projects are ephemeral, so a counter that outlived them would be describing runs whose
#: record is already gone. A restart clears it, which is the honest failure mode for a brake whose
#: subject is the container's own work: it can be defeated by crashing the service, and anyone who
#: can do that has a better lever than this one anyway.
_started: deque[float] = deque(maxlen=1000)


def _too_many() -> str | None:
    """Why this run may not start, or None. Checked before anything is created or staged."""
    if MAX_CONCURRENT_RUNS > 0 and len(_running) >= MAX_CONCURRENT_RUNS:
        return (f"{len(_running)} run(s) already going, which is the limit for this host. "
                "Watch one of those, or come back when it has finished — a run takes a few minutes.")

    if MAX_RUNS_PER_DAY > 0:
        cutoff = time.time() - 86_400
        while _started and _started[0] < cutoff:
            _started.popleft()
        if len(_started) >= MAX_RUNS_PER_DAY:
            return (f"This host has started its {MAX_RUNS_PER_DAY} runs for today. The studio spends "
                    "real money on every run, so the daily allowance is deliberate rather than a "
                    "fault. Finished runs are still readable in the studio.")
    return None


#: `POST /runs/<id>/say` — the director interrupting a run that is already going.
SAY = re.compile(r"^/runs/[^/]+/say$")

#: `POST /runs/<id>/answer` — the director settling a question the agent stopped to ask.
ANSWER = re.compile(r"^/runs/[^/]+/answer$")


def observing_only(method: str, path: str, root_path: str = "") -> bool:
    """Whether a request to the mounted studio is one an observer may make.

    Reading is every GET. Three mutations belong to it: registering a watch, **speaking to a run that
    is already going**, and **answering a question that run asked**.

    **Why these are allowed when they look like driving.** The blanket refusal was right when it was
    written — the studio's whole write side went through the Antigravity SDK, which this runtime does
    not ship — but it stopped being true one verb at a time. `adk_agent.interject` gives an ADK run a
    channel for words the director volunteered; `adk_agent.ask` gives it the other direction, holding
    the future the agent is awaiting. Both live in this process, because the studio is mounted on the
    ADK app, so neither needs a driver.

    `answer` was the last to change and this comment used to say why it could not: it settles a
    question the *host* asked, and with no host that could ask one, `ObservedRun.answer` returned
    False and refusing here merely saved the request a second refusal. Now the host is this process.

    Creating a project and starting a run still need a driver, and are still refused.

    **The subtlety is the path.** Middleware on a mounted app sees the *whole* path with `root_path`
    beside it — `/studio/observe`, not `/observe`; only route matching strips the prefix afterwards.
    Comparing the raw path refuses the one mutation this exists to allow, which is what the first
    version did.
    """
    if method in ("GET", "HEAD"):
        return True

    if root_path and path.startswith(root_path):
        path = path[len(root_path):]

    path = path.rstrip("/")
    return path == "/observe" or bool(SAY.match(path)) or bool(ANSWER.match(path))


async def launch(app: FastAPI, name: str, message: str = OPENING) -> None:
    """Starts the agent on `name`, by driving this very app.

    **In-process rather than over the network.** `ASGITransport` calls the app directly, so there is
    no port to discover, no second connection to the machine, and nothing to configure differently
    when this is deployed behind a proxy. It is how the tests drive it too.
    """
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://intake", timeout=None) as web:
        made = await web.post(f"/apps/{name}/users/{USER}/sessions", json={})
        made.raise_for_status()
        session = made.json()["id"]

        answer = await web.post("/run_sse", json={
            "appName": name, "userId": USER, "sessionId": session,
            "newMessage": {"role": "user", "parts": [{"text": message}]},
            "streaming": False,
        })
        answer.raise_for_status()
    _logger.info("intake: %s ran to completion", name)


async def observe(app: FastAPI, name: str) -> str | None:
    """Registers a watch on `name` and returns the studio path, or None if there is no studio.

    Registered *after* the run is started rather than before: the ADK cut is the last `run.begin`,
    and a project with earlier work would otherwise replay it as though it were this run.
    """
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://intake") as web:
        try:
            seen = await web.post("/studio/observe", data={"project": name})
        except Exception as exc:                                # noqa: BLE001 - studio is optional
            _logger.info("intake: no studio to observe %s with (%s)", name, exc)
            return None

    location = seen.headers.get("location")
    return location if seen.status_code == 303 and location else None


FORM = """<!doctype html>
<meta charset="utf-8">
<title>Polson — new commission</title>
<style>
 body { font: 15px/1.5 system-ui, sans-serif; max-width: 40rem; margin: 4rem auto; padding: 0 1rem;
        color: #1c2733; background: #faf8f4; }
 h1 { font-weight: 600; letter-spacing: -.02em; }
 label { display: block; margin: 1.2rem 0 .3rem; font-weight: 600; }
 input, select, textarea { width: 100%; padding: .5rem; font: inherit;
        border: 1px solid #cdc7bb; border-radius: 3px; background: #fff; }
 textarea { min-height: 6rem; }
 small { color: #6b7280; font-weight: 400; }
 .note { margin-top: 1.2rem; padding: .7rem .9rem; font-size: .88rem; color: #4b5563;
        background: #f3f0ea; border-left: 3px solid #cdc7bb; border-radius: 3px; }
 button { margin-top: 1.5rem; padding: .6rem 1.4rem; font: inherit; font-weight: 600;
        background: #1f6f8b; color: #fff; border: 0; border-radius: 3px; cursor: pointer; }
 .starters { margin: 1.4rem 0 .4rem; }
 .starters p { margin: 0 0 .5rem; font-size: .88rem; color: #6b7280; }
 .chips { display: flex; flex-wrap: wrap; gap: .4rem; }
 /* Not the submit button: these fill the form, they do not send it. Stated in the styling as well
    as in `type="button"`, so the one that commissions a run never looks like the five that do not. */
 .chips button { margin: 0; padding: .35rem .7rem; font-size: .86rem; font-weight: 500;
        background: #fff; color: #1f6f8b; border: 1px solid #cdc7bb; border-radius: 999px; }
 .chips button:hover { background: #f3f0ea; }
 .chips button[aria-pressed="true"] { background: #1f6f8b; color: #fff; border-color: #1f6f8b; }
</style>
<h1>New commission</h1>
<p>Describe what you want made. Attach a document and the studio will read its figures
   rather than researching them.</p>

<div class="starters">
  <p>Or start from one of these &mdash; every field stays editable.</p>
  <div class="chips">__STARTERS__</div>
</div>

<form method="post" action="/projects" enctype="multipart/form-data">
  <label>Project name <small>letters, digits and underscores &mdash; no dashes</small></label>
  <input name="name" required pattern="[a-zA-Z][a-zA-Z0-9_]{0,47}" placeholder="boxoffice2025">

  <label>Workflow</label>
  <select id="workflow" name="workflow">__WORKFLOWS__</select>

  <label>Type <small>optional &mdash; the direction the workflow takes</small></label>
  <select id="kind" name="kind">__TYPES__</select>

  <label>Brief</label>
  <textarea name="brief" required
      placeholder="Chart the 2025 box office returns as a timeline."></textarea>

  <label>Document <small>optional — PDF, CSV, TXT, MD, JSON, or an image</small></label>
  <input type="file" name="document" accept="__ACCEPT__">

  <label style="font-weight:400; margin-top:1.4rem">
    <input type="checkbox" name="start" value="1" checked style="width:auto; margin-right:.4rem">
    Start the agent straight away, and open the studio on it
  </label>

  <p class="note">If this host restarts while your project is running, the run stops and you will
     need to start it again. <b>The work it had already done is kept</b> &mdash; the project comes
     back under <b>Archived projects</b> in the studio, with its renders, its scripts and its
     documents.</p>

  <button type="submit">Create</button>
</form>
<script>
// The option list holds every type any offered workflow has; this hides the ones the selected
// workflow does not offer, and disables the control entirely when it offers none. Convenience only
// — the server refuses a bad pairing whether or not this ran. Same guard idiom as the studio form:
// a missing element returns rather than throwing, so one absent control cannot take the page down.
(function () {
  const workflow = document.getElementById('workflow');
  const kind = document.getElementById('kind');
  if (!workflow || !kind) return;

  function sync() {
    const types = (workflow.selectedOptions[0].dataset.types || '').split(',').filter(Boolean);
    kind.disabled = types.length === 0;
    if (kind.disabled) kind.value = '';
    for (const option of kind.options) {
      option.hidden = option.value !== '' && !types.includes(option.value);
    }
  }
  workflow.addEventListener('change', sync);
  sync();

  // The starters. Each chip carries its whole commission as data attributes, so filling the form is
  // four assignments and no round trip — and because they write into the ordinary controls, every
  // field stays editable afterwards and the server validates exactly what it would have anyway.
  //
  // **`sync()` runs between the workflow and the type**, not after both. The type control hides the
  // options the chosen workflow does not offer, so setting a type before its workflow has been
  // synced assigns a value to a hidden option — which the browser keeps, and the server then refuses
  // with a message about a type that is not offered. Order is load-bearing here.
  for (const chip of document.querySelectorAll('.chips button')) {
    chip.addEventListener('click', () => {
      const form = document.querySelector('form');
      if (!form) return;

      workflow.value = chip.dataset.workflow;
      sync();
      kind.value = chip.dataset.kind;
      form.name.value = chip.dataset.name;
      form.brief.value = chip.dataset.brief;

      for (const other of document.querySelectorAll('.chips button')) {
        other.setAttribute('aria-pressed', String(other === chip));
      }
      form.brief.focus();
      // The end, not the start: this is a draft to edit rather than a value to accept, and a cursor
      // sitting after the last word says so without a line of instruction.
      form.brief.setSelectionRange(form.brief.value.length, form.brief.value.length);
    });
  }
})();
</script>
"""


def mount(app: FastAPI) -> None:
    """Adds the intake form and its endpoint to an existing app."""
    request_app = app

    @app.get("/new", response_class=HTMLResponse)
    async def form() -> str:
        # `data-types` is what lets the type control narrow itself to the chosen workflow without a
        # round trip, and it is the same list the POST validates against, so the two cannot disagree.
        options = "".join(
            f'<option value="{w}" data-types="{",".join(types)}">{w}</option>'
            for w, types in WORKFLOWS.items())
        types_options = '<option value="">&mdash;</option>' + "".join(
            f'<option value="{t}">{t}</option>' for t in ALL_TYPES)
        accept = ",".join(sorted(DOCUMENT_SUFFIXES))

        # Escaped with `quoteattr` rather than by hand. These are our own strings, so nothing here is
        # untrusted — but a brief is prose, prose acquires apostrophes and quotation marks the moment
        # anyone edits one, and an unescaped quote in an attribute ends the attribute. The failure is
        # a chip that silently fills half a brief, which is worse than one that does not work at all.
        chips = "".join(
            f'<button type="button" aria-pressed="false"'
            f' data-name={quoteattr(s["name"])}'
            f' data-workflow={quoteattr(s["workflow"])}'
            f' data-kind={quoteattr(s["kind"])}'
            f' data-brief={quoteattr(s["brief"])}>{escape(s["label"])}</button>'
            for s in STARTERS)

        return (FORM.replace("__WORKFLOWS__", options)
                    .replace("__TYPES__", types_options)
                    .replace("__STARTERS__", chips)
                    .replace("__ACCEPT__", accept))

    @app.post("/projects")
    async def make(
        name: str = Form(...),
        brief: str = Form(...),
        workflow: str = Form("vector_infographic"),
        kind: str = Form(""),
        document: UploadFile | None = File(None),
        start: str = Form(""),
    ):
        """Creates a project, stages the upload into its `documents/`, opens the console on it."""
        name = name.strip()
        if not VALID_APP_NAME.match(name) or len(name) > MAX_NAME:
            raise HTTPException(400,
                f"{name!r} cannot be a project name — start with a letter, then letters, digits or "
                f"underscores, up to {MAX_NAME} characters. No dashes: the name becomes an ADK app "
                "name, which must be a Python identifier.")

        if workflow not in WORKFLOWS:
            raise HTTPException(400, f"{workflow!r} is not offered here. Choose: {', '.join(WORKFLOWS)}.")

        # Checked against *this* workflow's list rather than the union: every type here happens to be
        # offered by both, but a type the chosen workflow lacks would reach the generator as a
        # `--type` naming a `type.<name>.md` that is not in its template, and the agent would be sent
        # a direction nothing describes.
        kind = kind.strip()
        if kind and kind not in WORKFLOWS[workflow]:
            offered = ", ".join(WORKFLOWS[workflow]) or "none"
            raise HTTPException(400,
                f"The {workflow} workflow does not offer a type {kind!r}. It offers: {offered}.")

        if not brief.strip():
            raise HTTPException(400, "A brief is required — say what you want made.")

        # Before the upload is staged and before the project is written, so a refused commission
        # leaves nothing behind and costs nothing. 429 rather than 400: the request is fine, the
        # host is not willing right now, and a client should read it as "later" rather than "wrong".
        if start and (full := _too_many()):
            raise HTTPException(429, full)

        # Staged into a scratch directory *before* the project exists, so a refused upload leaves
        # nothing behind and no half-made project. Its own directory rather than the shared temp
        # folder, so the file can keep its real name without colliding with a concurrent upload of
        # the same name. `create` copies it in and is the only thing that writes into the project.
        scratch: Path | None = None
        staged: Path | None = None
        try:
            if document is not None and document.filename:
                scratch = Path(tempfile.mkdtemp(prefix="polson-intake-"))
                staged = await stage_upload(document, scratch)

            create(name, workflow=workflow, prompt=brief, type_=kind or None,
                   documents=[str(staged)] if staged else None)
        except GenerateError as exc:
            raise HTTPException(400, str(exc)) from exc
        finally:
            if scratch is not None:
                shutil.rmtree(scratch, ignore_errors=True)

        # The app appears without a restart — `list_agents` re-reads the directory on every call,
        # which `newproject` relies on too.
        if not start:
            return RedirectResponse(f"/dev-ui/?app={name}", status_code=303)

        # **Creating a project is not starting one, and that gap is what this closes.** The form used
        # to end at a redirect into ADK's console, where the visitor had to know to type a message
        # before anything happened. Two directors in a row read the empty studio as a failure — which
        # it was, of the flow rather than of the run.
        #
        # Fired as a task rather than awaited: a run takes minutes and the response has to come back
        # now. Nothing reads its result, so a failure is logged rather than raised.
        # Recorded at the moment of starting rather than of asking, so a commission refused above or
        # one that never reached here does not consume the day's allowance.
        _started.append(time.time())

        run = asyncio.create_task(launch(request_app, name))
        _running.add(run)
        run.add_done_callback(_running.discard)
        run.add_done_callback(lambda t: t.cancelled() or t.exception() is None
                              or _logger.error("intake: %s failed to run: %r", name, t.exception()))

        # Straight to the record, which is the thing worth looking at while it works. Falls back to
        # the console when there is no studio mounted, so this never strands a visitor.
        watching = await observe(request_app, name)
        return RedirectResponse(watching or f"/dev-ui/?app={name}", status_code=303)
