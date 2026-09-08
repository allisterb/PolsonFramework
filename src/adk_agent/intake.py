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
import logging
import re
import shutil
import tempfile
from pathlib import Path

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
USER = "director"

#: Workflows offered on the form. Not the full set: these are the ones whose instructions tell an
#: agent to look for documents, and offering a workflow that ignores an upload would be worse than
#: not offering it.
WORKFLOWS = ("vector_infographic", "infographic")


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


def observing_only(method: str, path: str, root_path: str = "") -> bool:
    """Whether a request to the mounted studio is one an observer may make.

    Reading is every GET; the single mutation that belongs to reading is registering a watch.
    Everything else — creating a project, starting or answering a run — needs a driver this runtime
    does not ship, and used to be offered anyway.

    **The subtlety is the path.** Middleware on a mounted app sees the *whole* path with `root_path`
    beside it — `/studio/observe`, not `/observe`; only route matching strips the prefix afterwards.
    Comparing the raw path refuses the one mutation this exists to allow, which is what the first
    version did.
    """
    if method in ("GET", "HEAD"):
        return True

    if root_path and path.startswith(root_path):
        path = path[len(root_path):]
    return path.rstrip("/") == "/observe"


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
 button { margin-top: 1.5rem; padding: .6rem 1.4rem; font: inherit; font-weight: 600;
        background: #1f6f8b; color: #fff; border: 0; border-radius: 3px; cursor: pointer; }
</style>
<h1>New commission</h1>
<p>Describe what you want made. Attach a document and the studio will read its figures
   rather than researching them.</p>
<form method="post" action="/projects" enctype="multipart/form-data">
  <label>Project name <small>letters, digits and underscores &mdash; no dashes</small></label>
  <input name="name" required pattern="[a-zA-Z][a-zA-Z0-9_]{0,47}" placeholder="boxoffice2025">

  <label>Workflow</label>
  <select name="workflow">__WORKFLOWS__</select>

  <label>Brief</label>
  <textarea name="brief" required
      placeholder="Chart the 2025 box office returns as a timeline."></textarea>

  <label>Document <small>optional — PDF, CSV, TXT, MD, JSON, or an image</small></label>
  <input type="file" name="document" accept="__ACCEPT__">

  <label style="font-weight:400; margin-top:1.4rem">
    <input type="checkbox" name="start" value="1" checked style="width:auto; margin-right:.4rem">
    Start the agent straight away, and open the studio on it
  </label>

  <button type="submit">Create</button>
</form>
"""


def mount(app: FastAPI) -> None:
    """Adds the intake form and its endpoint to an existing app."""
    request_app = app

    @app.get("/new", response_class=HTMLResponse)
    async def form() -> str:
        options = "".join(f'<option value="{w}">{w}</option>' for w in WORKFLOWS)
        accept = ",".join(sorted(DOCUMENT_SUFFIXES))
        return FORM.replace("__WORKFLOWS__", options).replace("__ACCEPT__", accept)

    @app.post("/projects")
    async def make(
        name: str = Form(...),
        brief: str = Form(...),
        workflow: str = Form("vector_infographic"),
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

        if not brief.strip():
            raise HTTPException(400, "A brief is required — say what you want made.")

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

            create(name, workflow=workflow, prompt=brief,
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
        run = asyncio.create_task(launch(request_app, name))
        _running.add(run)
        run.add_done_callback(_running.discard)
        run.add_done_callback(lambda t: t.cancelled() or t.exception() is None
                              or _logger.error("intake: %s failed to run: %r", name, t.exception()))

        # Straight to the record, which is the thing worth looking at while it works. Falls back to
        # the console when there is no studio mounted, so this never strands a visitor.
        watching = await observe(request_app, name)
        return RedirectResponse(watching or f"/dev-ui/?app={name}", status_code=303)
