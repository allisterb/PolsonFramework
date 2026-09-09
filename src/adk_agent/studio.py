"""Builds a Polson studio agent from a generated project directory.

**A module, not a package, and that is load-bearing.** ADK lists an app for every *directory* in
`agents_dir` that looks like an agent, so a shared factory living there as a package would be listed
as a broken app. A single `.py` file is invisible to that scan while still being importable, because
ADK inserts `agents_dir` into `sys.path` before loading anything.

**One studio server per app, shared by every agent in it.** The toolset is constructed once and
handed to the root and to every sub-agent, so they all talk to one `Polson.CLI.dll server` process
and therefore one `Session` scratchpad. That is deliberate: it is what lets the Penciler leave
`Session.pencils = canvas.toBitmap()` for the Inker with no encode, no decode and no file — the
horizontal stigmergy of `CLAUDE.md` §2, done by the environment rather than described.

The costs are real and are the price of that: `ScriptHistory` is shared and unattributed, `Session`
keys are a flat namespace one agent can clobber, and `SessionContext.Stage` is a single slot. All
three are survivable **only because control transfer is sequential** — ADK's `transfer_to_agent`
moves control rather than forking it, so one agent runs at a time and nothing races.

> **Do not put these agents under `ParallelAgent`.** `SessionContext.Storage` is a plain
> `Dictionary<string, object?>`, not a concurrent one, and the engine builds a fresh Jint instance
> per execution. Two agents executing at once would mutate that dictionary from two threads, with
> SkiaSharp bitmaps stashed inside it. If genuine parallelism is ever needed, give each agent its
> own `McpToolset` instead — that yields one server and one isolated `Session` each, verified.
"""

from __future__ import annotations

import json
import logging
import os
import re
import time
from dataclasses import dataclass
from pathlib import Path

from google.adk.agents.llm_agent import Agent
from google.adk.apps import App
from google.adk.tools import FunctionTool
from google.adk.tools.agent_tool import AgentTool
from google.adk.tools.load_artifacts_tool import LoadArtifactsTool
from google.adk.tools.mcp_tool import McpToolset

import ask
import interject
import mirror
import transcript
from google.adk.tools.mcp_tool import StdioConnectionParams
from google.genai import types
from mcp import StdioServerParameters

from supervision import StudioWatchdog

#: Repository root, from `src/adk_agent/studio.py`.
REPO_ROOT = Path(__file__).resolve().parents[2]

#: The built MCP server. Same artifact the Antigravity wiring points at; not rebuilt here.
DEFAULT_CLI_DLL = REPO_ROOT / "bin" / "cli" / "Polson.CLI.dll"

#: Instructions filenames, in the order `ProjectGenerator.HostFiles` can have written them.
INSTRUCTIONS_FILENAMES = ("GEMINI.md", "CLAUDE.md")

#: **Measured against this project's key, not taken from `adk create`.** Re-probed 2026-09-06,
#: median of three calls on one reasoning prompt, with the total tokens each spent answering it:
#:
#:   gemini-2.5-flash  OK  1.0s    130 tok
#:   gemini-3.5-flash  OK  3.8s    485 tok
#:   gemini-3.7-flash  OK  4.3s    467 tok   <- the default
#:   gemini-2.5-pro    OK 11.8s   1260 tok
#:   gemini-3-flash / gemini-3-pro / gemini-3.0-pro / gemini-3.1-pro   404 NOT_FOUND
#:
#: **The 2026-09-04 probe recorded `gemini-2.5-flash OK 17.6s <- seven times slower`, and that was
#: wrong.** It is the fastest of the four here. A single unrepeated call was measured, so a cold
#: start or a moment of throttling became a documented property of the model, and the note then
#: argued against a model on a number that was never true. Probe more than once before writing a
#: figure into a comment somebody will later plan around.
#:
#: The token column is the more interesting one and cuts the other way: 3.x spends three to four
#: times as much answering the same question, because it thinks first. On a one-line prompt that is
#: pure overhead; across an eighty-turn studio run it is most of what the model is being paid for.
#: So this table ranks latency and cost, and says nothing about whether a cheaper model can hold a
#: staged brief together — which is the only question that matters and is not measurable from here.
#:
#: 3.7 rather than the 3.5 `adk create` offers, for one measured reason and one weaker one.
#:
#: **Measured:** it answers as fast as 3.5 through this key, and a `comic_studio` run on 3.5
#: exhausted its retries on a 429.
#:
#: **Weaker, and stated as such:** August billing for this project shows 12.4M uncached input
#: tokens against 31.4M cached on 3.7 Flash. That looked like evidence of implicit caching helping
#: exactly this workload's shape — the same large system prompt resent every turn — but the dates
#: predate the ADK runtime, and `reference/projects/antigravity-sdk-python-0.1.15` names
#: `gemini-3.7-flash` in its own source. So that traffic was **Antigravity SDK sessions, not ours**.
#:
#: It is the *same key and the same quota pool* though — Antigravity Desktop runs on a personal
#: account, but a standalone agy project uses this project's key, which is what those four days
#: were. So it is good evidence that this credential sustains volume on this model, and weak
#: evidence about caching, because the prompts were shaped by a different client. Do not repeat the
#: caching claim as though it were measured here.
#:
#: Note `Gemini 3.0 / 3.1 Pro` appears in billing as a **SKU name**, not a model id — none of those
#: spellings resolve. Do not infer a model id from an invoice line.
DEFAULT_MODEL = "gemini-3.7-flash"

#: The .NET server has a runtime to start before it speaks MCP. ADK's default is 5s, which is a cold
#: start away from being too short, and the failure is an empty toolset rather than a stack trace.
STDIO_TIMEOUT_SECONDS = 60.0

#: `roles/01_penciler.md` -> `penciler`. The number orders the pipeline and is not the name.
_ROLE_STEM = re.compile(r"^\d+_(.+)$")


class ConfigError(RuntimeError):
    """Missing or unusable configuration, with a message saying what to set."""


def _read(path: Path) -> str:
    """Text, tolerating the BOM the role templates carry."""
    return path.read_text(encoding="utf-8-sig")


def instructions_for(project: Path) -> str:
    """The project's own instructions file, read as the agent's instruction.

    Generated files are trusted here for the same reason `orchestrator/project.py` trusts them: we
    wrote them. `brief.md` is **not** trusted and is not inlined — the instructions already tell the
    agent to read it as data, and keeping that a file boundary is what keeps the trust boundary
    legible.
    """
    for name in INSTRUCTIONS_FILENAMES:
        candidate = project / name
        if candidate.is_file():
            text = _read(candidate).strip()
            if text:
                return text
            raise ConfigError(f"{candidate} is empty — the agent would start with no instructions.")

    tried = " or ".join(INSTRUCTIONS_FILENAMES)
    raise ConfigError(
        f"{project} has no {tried}, so it is not a generated Polson project "
        "(or was generated without instructions). Regenerate it with create-project."
    )


def deadline_in(project: Path) -> float | None:
    """The project's own deadline in minutes, from `project.json`, or None.

    Written by `create-project --deadline`, which defaults it per workflow — a logo is a quarter of
    an hour and a study painting is two. This is the right place for it to live: a deadline is a
    property of the commission, not of the deployment, and the instructions the agent reads were
    generated from the same number, so the two cannot disagree.

    Zero means the project deliberately has none, and is returned as None so it reads that way.
    """
    manifest = project / "project.json"
    if not manifest.is_file():
        return None
    try:
        value = json.loads(_read(manifest)).get("deadlineMinutes")
    except (ValueError, OSError, AttributeError):
        return None
    return float(value) if isinstance(value, (int, float)) and value > 0 else None


def roles_in(project: Path) -> list[tuple[str, str, str]]:
    """`(name, description, prompt)` per role file, ordered by the filename's leading number.

    Mirrors `ProjectGenerator.Role.From` exactly — same name derivation, same description from the
    first `# ` heading with a `Role:` prefix stripped — so the two cannot disagree about what a
    workflow's agents are called. A workflow gains an agent by gaining a file.
    """
    directory = project / "roles"
    if not directory.is_dir():
        return []

    found: list[tuple[str, str, str]] = []
    for path in sorted(directory.glob("*.md")):
        body = _read(path)
        stem = path.stem
        match = _ROLE_STEM.match(stem)
        name = match.group(1) if match else stem

        heading = next((l for l in body.splitlines() if l.startswith("# ")), None)
        description = heading[2:].strip() if heading else name
        if description.lower().startswith("role:"):
            description = description[5:].strip()

        found.append((name, description, body))
    return found


def toolset_for(project: Path, cli_dll: Path | None = None) -> McpToolset:
    """The whole .NET engine, as one ADK toolset.

    The command mirrors `ProjectGenerator.McpConfig` exactly — `dotnet <dll> server --project-dir
    <abs>` — because a second spelling of the same launch is a second thing to keep in step.
    """
    dll = (cli_dll or DEFAULT_CLI_DLL).resolve()
    if not dll.is_file():
        raise ConfigError(
            f"no MCP server at {dll}. Build it with dotnet build on src/Polson.CLI, or pass "
            "cli_dll."
        )

    return McpToolset(
        connection_params=StdioConnectionParams(
            server_params=StdioServerParameters(
                command="dotnet",
                args=[str(dll), "server", "--project-dir", str(project)],
            ),
            timeout=STDIO_TIMEOUT_SECONDS,
        ),
        # The studio manuals are MCP *resources* (`polson://manual/*`), not tools, and ADK leaves
        # resources off by default. Without this the agent has the drawing surface but not the
        # design knowledge — the Extended Mind half of `CLAUDE.md` §2 absent, and absent quietly.
        use_mcp_resources=True,
    )


# --------------------------------------------------------------------------------------------
# Peeking — the perception half of the loop.
# --------------------------------------------------------------------------------------------
#
# **Without this the agent is blind, and does not know it.** The first real ADK run wrote a render
# to disk, then reported "Here is what I see" followed by seven headings with empty bodies. It had
# no way to look: the Polson tools return JSON, and ADK's MCP bridge ends with
# `response.model_dump(mode="json")` — so even a proper MCP image content block would arrive as
# base64 inside text, costing the window without being viewable. The fix cannot live in the MCP
# server; it has to be an ADK tool that puts a real `inline_data` Part into the conversation.
#
# ADK artifacts are the mechanism, and they are more than a transport. An artifact is a versioned
# entity: `save_artifact` returns an integer version, and successive saves under one filename
# accumulate rather than overwrite. That matters here because `outFile` *does* overwrite — in the
# first run two scripts both wrote `artifacts/01_concept.webp`, and the earlier render (10,112
# bytes) is unrecoverable, while the run record still shows two `render` events pointing at the one
# surviving file. Peeking through the artifact service gives the progression somewhere to live.
#
# The two stores stay separate on purpose. `projects/<id>/artifacts/` is the durable, browsable,
# replayable record a human opens and the web app serves as URLs; the ADK artifact service holds
# versioned blobs the *model* can be shown. Neither substitutes for the other.

#: What a script can render, and so what `peek` can show. An `.svg` is not here and is not readable
#: either: see `read_file`, which turns one away with the route that actually produces a picture.
PEEKABLE = {
    ".webp": "image/webp",
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
}

#: A guard, not a policy. Gemini accepts far larger, but a runaway render should fail loudly here
#: rather than as an opaque model error several seconds later.
MAX_PEEK_BYTES = 12 * 1024 * 1024


def _make_peek(project: Path):
    """Builds the `peek` tool bound to one project directory.

    Bound rather than parameterised because the project is a property of the app, and a path
    argument that could name any directory is a containment hole rather than a feature.
    """

    async def peek(artifact_path: str, tool_context) -> dict:
        """Look at an image this project rendered. Call this after every render, before judging it.

        Loads the image into your context so you can actually see it. You cannot assess a render you
        have not peeked at — describing one you have not looked at is guessing, and it will be wrong.

        Args:
            artifact_path: Path of the image, relative to the project directory, exactly as passed
                to `outFile` — for example `artifacts/01_concept.webp`.

        Returns:
            A dict with `ok`, the `version` the artifact was saved under, and `filename`. On failure,
            `ok` is False and `error` says what to do.
        """
        candidate = (project / artifact_path).resolve()
        try:
            candidate.relative_to(project)
        except ValueError:
            return {"ok": False, "error": (
                f"{artifact_path!r} resolves outside the project directory. Peek only at files this "
                "project rendered.")}

        mime = PEEKABLE.get(candidate.suffix.lower())
        if mime is None:
            # The .svg case is called out rather than left to the generic list, because the generic
            # advice used to be "read it another way" and read_file now turns an .svg away too.
            # Two tools pointing at each other is the exact failure `_make_read_file` records:
            # turns spent discovering that a documented instruction is unfollowable.
            svg_hint = (
                f" An .svg is markup, not an image — render it first with "
                f"RenderSvg(file='{artifact_path}', outFile='artifacts/preview.png'), then peek that."
                if candidate.suffix.lower() == ".svg" else "")
            return {"ok": False, "error": (
                f"{candidate.suffix!r} is not a peekable image. Peekable: "
                f"{', '.join(sorted(PEEKABLE))}.{svg_hint}")}

        if not candidate.is_file():
            existing = sorted(p.name for p in (project / "artifacts").glob("*")
                              if p.suffix.lower() in PEEKABLE) if (project / "artifacts").is_dir() else []
            return {"ok": False, "error": (
                f"no file at {artifact_path}. Render it first with outFile. "
                + (f"Present in artifacts/: {', '.join(existing)}" if existing
                   else "Nothing has been rendered yet."))}

        data = candidate.read_bytes()
        if len(data) > MAX_PEEK_BYTES:
            return {"ok": False, "error": (
                f"{artifact_path} is {len(data)} bytes, over the {MAX_PEEK_BYTES} limit. "
                "Render smaller — draft at 800x600 rather than full size.")}

        # The artifact name keeps the path so successive renders of *different* stages stay
        # distinct, while re-renders of the same stage become versions of one artifact.
        filename = artifact_path.replace("\\", "/").lstrip("./")
        version = await tool_context.save_artifact(
            filename, types.Part.from_bytes(data=data, mime_type=mime))

        return {"ok": True, "filename": filename, "version": version, "bytes": len(data),
                "note": "Loaded. Call load_artifacts to bring it into view, then say what you see."}

    return peek


#: What `write_script` will author. Still not a general file-write tool — a fixed, short list rather
#: than "any text file".
#:
#: `.md` was added because a live `logo --test` run found the runtime could not produce its own
#: deliverable. The instructions of every workflow ask for `findings.md`, and `comic_studio` also asks
#: for `critique_log.md`; the agent wrote one, called `write_script`, and was told *"'findings.md'
#: does not end in .js"*. Its report survived only as chat text, which is not a file anyone can read
#: afterwards. A named deliverable no tool can write is a gap in the runtime, not a misuse by the
#: agent.
AUTHORABLE_SUFFIXES = (".js", ".md")

#: Kept for callers that mean the drawing file specifically.
SCRIPT_SUFFIX = ".js"

#: A guard against a runaway generation, not a style rule. The largest script in a measured
#: four-agent run was well under this.
MAX_SCRIPT_BYTES = 512 * 1024


#: What `read_file` will open. Text only — an image is `peek`'s job, and a binary handed to a model
#: as mojibake is worse than a refusal that says which tool to use.
#:
#: **`.svg` is deliberately absent**, though it is text. Reading one is never the right move: it
#: cannot be *seen* by reading, it should not be *edited* as text when `Snap.load` will open it
#: inside a script, and its structure is answered better by querying it there than by pulling the
#: whole document into context. For an SVG the agent itself produced, the script that made it is
#: both shorter and more meaningful than its output. See the `.svg` branch in `read_file`.
READABLE_SUFFIXES = {
    ".md", ".txt", ".json", ".js", ".csv", ".yaml", ".yml", ".xml", ".html", ".css",
}

#: A cap, not a policy. `brief.md` is a couple of KB and a drawing script is tens; anything past
#: this is a mistake about which file was wanted.
MAX_READ_BYTES = 256 * 1024


def _make_read_file(project: Path):
    """Builds the `read_file` tool bound to one project directory.

    **The failure this removes appeared in every run.** `GEMINI.md` opens by telling the agent that
    the brief is in `brief.md`, and the agent had no verb for opening it. The event log shows what
    it did instead — `ExecuteScript(scriptFile='brief.md')`, giving *"brief.md is not parseable
    JavaScript"*, then `peek('brief.md')`, giving *"Failed to decode image from brief.md"*. Two
    wasted turns per run, discovering by flailing that a documented instruction was unfollowable.

    It also makes `edit_script` reliable. That tool needs `old_text` to match the file exactly;
    without a way to read the file back, the agent has to remember precisely what it wrote several
    turns ago, and a near-miss costs a turn.

    `InspectScript` remains the better tool for a *large* JavaScript file — it answers structural
    questions without pulling the source into context. This is for when the text itself is wanted.
    """

    async def read_file(path: str) -> dict:
        """Read a text file from this project — the brief, a script you wrote, a note.

        Use this for `brief.md` before anything else: it is the client's actual request. For a long
        JavaScript file prefer InspectScript, which answers questions about it without spending
        context on the whole source.

        Args:
            path: Path relative to the project directory, e.g. `brief.md` or `artwork.js`.

        Returns:
            A dict with `ok`, the `content`, and its `lines` and `bytes`. On failure, `ok` is False
            and `error` says what to do instead.
        """
        candidate = (project / path).resolve()
        try:
            candidate.relative_to(project)
        except ValueError:
            return {"ok": False, "error": (
                f"{path!r} resolves outside the project directory. Read only inside the project.")}

        suffix = candidate.suffix.lower()

        # Turned away before the generic check, so the message names the three things that do work
        # rather than listing readable extensions. This is the ADK-side half of the rule the MCP
        # server enforces by no longer returning svgXml in a tool result — without it, the field
        # simply came back through the file reader instead.
        #
        # It is a flat refusal rather than a size or data-URI test, because no threshold makes
        # reading an SVG the right move. A vector page carrying one 400px portrait is ~110,000
        # characters of base64 describing a picture the model still cannot see; a page with none is
        # merely verbose rather than useful, since the script that produced it is shorter and says
        # what it meant. Every question an agent has about an SVG is answered better elsewhere.
        if suffix == ".svg":
            return {"ok": False, "error": (
                f"{path} is markup, and reading it is never what you want — you cannot see a "
                "picture by reading its source, and this would spend the context of a large "
                "document to tell you nothing you can look at.\n"
                f"  • To SEE it: RenderSvg(file='{path}', outFile='artifacts/preview.png'), then "
                "peek('artifacts/preview.png').\n"
                f"  • To EDIT or INSPECT it: open it inside a script with Snap.load('{path}') and "
                "query it there — selectAll('path').length, attr('fill'), getBBox() — so only your "
                "answer comes back, not the whole document.\n"
                "  • To understand one you drew: read the .js script that produced it. It is "
                "shorter than its output and says what it meant.")}

        if suffix not in READABLE_SUFFIXES:
            hint = (" Use peek(...) to look at a rendered image."
                    if suffix in PEEKABLE else "")
            return {"ok": False, "error": (
                f"{suffix or path!r} is not a readable text file. Readable: "
                f"{', '.join(sorted(READABLE_SUFFIXES))}.{hint}")}

        if not candidate.is_file():
            siblings = sorted(p.name for p in project.iterdir()
                              if p.is_file() and p.suffix.lower() in READABLE_SUFFIXES)
            return {"ok": False, "error": (
                f"no file at {path}."
                + (f" In the project root: {', '.join(siblings[:12])}" if siblings else ""))}


        size = candidate.stat().st_size
        if size > MAX_READ_BYTES:
            return {"ok": False, "error": (
                f"{path} is {size} bytes, over the {MAX_READ_BYTES} limit."
                + (" Use InspectScript for a large script." if suffix == ".js" else ""))}

        try:
            # utf-8-sig because the generated templates carry a BOM; errors='replace' so a stray
            # byte degrades one character rather than failing the whole read.
            content = candidate.read_text(encoding="utf-8-sig", errors="replace")
        except OSError as error:
            return {"ok": False, "error": f"could not read {path}: {error}"}

        return {"ok": True, "path": path, "bytes": size,
                "lines": content.count(chr(10)) + 1, "content": content}

    return read_file


def _make_budget_status(token_cap: int | None, budget_minutes: float | None):
    """Builds `budget_status`, the tool that lets an agent ask what it has spent.

    **Time and tokens were asymmetric, and this closes it.** `deadline.md` hands the agent a clock —
    `Session.startedAt`, then `Date.now()` — so it can pace itself. Nothing equivalent existed for
    tokens: the counter lives here, in the Python process, and the sandbox cannot see it. The agent
    learned its spend only when the 75% notice arrived, which is advice at a moment it cannot
    choose. Being able to *ask* is what makes a budget something to plan against rather than
    something that happens to you — the same argument `deadline.md` makes for stating the deadline
    up front.

    Reports both allowances, since a run usually has both and the binding one is whichever is
    nearer. Cheap and read-only: no model call, no I/O, just the counters this module already keeps.
    """

    async def budget_status(tool_context) -> dict:
        """How much of this run's budget is spent: input tokens, and wall-clock minutes.

        Call it when deciding whether another pass will fit. Input tokens are the whole conversation
        resent every turn, so they climb whether or not you are making progress.
        """
        invocation = getattr(tool_context, "invocation_id", None)
        spent = _invocation_input.get(invocation, 0)
        started = _invocation_started.get(invocation)
        elapsed = (time.monotonic() - started) / 60.0 if started else 0.0

        cached = _invocation_cached.get(invocation, 0)
        status: dict = {
            "inputTokensSpent": spent,
            # Part of inputTokensSpent, not extra to it. Worth watching: a low share means the
            # conversation is being re-sent at full price, and it is not something you control.
            "cachedInputTokens": cached,
            "cachedSharePercent": round(cached / spent * 100, 1) if spent else 0.0,
            "minutesElapsed": round(elapsed, 1),
            # The count is only known after a turn completes, so this trails the turn in progress.
            "note": "inputTokensSpent excludes the turn now running; it is known only once the "
                    "model has answered.",
        }
        if token_cap:
            # **The cap is measured against the cache-adjusted figure, not `inputTokensSpent`.**
            # Reporting the raw number against the cap would tell an agent it is far closer to the
            # ceiling than it is — on a well-cached run the two differ several-fold — and an agent
            # that believes it is nearly out of budget starts cutting the work short.
            billable = _billable(invocation)
            status["inputTokenCap"] = token_cap
            status["billableTokensSpent"] = billable
            status["inputTokensRemaining"] = max(0, token_cap - billable)
            status["inputTokensUsedPercent"] = round(billable / token_cap * 100, 1)
            status["note"] += (" The cap counts billable tokens: a cached token is charged at "
                               f"{CACHED_TOKEN_SHARE:.0%} of a fresh one, so billableTokensSpent is "
                               "what the ceiling is measured against.")
        if budget_minutes:
            status["deadlineMinutes"] = budget_minutes
            status["minutesRemaining"] = round(max(0.0, budget_minutes - elapsed), 1)
        if not token_cap and not budget_minutes:
            status["note"] = "This run has no budget set; the figures above are what you have spent."
        return status

    return budget_status


def _make_write_script(project: Path):
    """Builds the `write_script` tool bound to one project directory.

    **Why this exists.** `ExecuteScript` takes `scriptFile`, `InspectScript`'s own parameter
    documentation gives `artwork.js` as its example, and the SDK reference recommends keeping any
    piece longer than a screenful in a file rather than re-sending it. None of that was reachable:
    the sandbox writes only images (`outFile`) and SVG (`outSvg`), and ADK supplies no file-writing
    tool, so the agent could *run* a file but never *create* one. Under Antigravity and Claude Code
    the host's own write tool fills that gap; here nothing did.

    The cost was measured on this runtime rather than assumed. In one 148-second run the three
    `ExecuteScript` calls took 20.3s, 22.9s and 37.1s to emit against 1.3-10.6s for every other
    call - 54% of the run spent typing JavaScript - while consecutive scripts shared 33% and 50% of
    their lines. Most of that was retyping a program in order to change part of it.
    """

    async def write_script(path: str, content: str, tool_context) -> dict:
        """Save a JavaScript file in the project, then run it with ExecuteScript(scriptFile=path).

        Use this instead of re-sending a whole program. Once a piece is more than a screenful, write
        it once, then change only the part you are working on and re-run the file. Each save is kept
        as a numbered version, so an earlier draft is never lost.

        Args:
            path: Where to save it, relative to the project directory, ending in `.js` — for
                example `artwork.js`. Saving to the same path again makes a new version.
            content: The complete file contents. This replaces the file; it is not a patch.

        Returns:
            A dict with `ok`, the `path` to pass as `scriptFile`, and the `version` saved. On
            failure, `ok` is False and `error` says what to do.
        """
        candidate = (project / path).resolve()
        try:
            candidate.relative_to(project)
        except ValueError:
            return {"ok": False, "error": (
                f"{path!r} resolves outside the project directory. Write only inside the project.")}

        if candidate.suffix.lower() not in AUTHORABLE_SUFFIXES:
            return {"ok": False, "error": (
                f"{path!r} does not end in {' or '.join(AUTHORABLE_SUFFIXES)}. This tool writes the "
                "files you author — a drawing script, or a note such as findings.md. Renders go to "
                "disk through ExecuteScript's outFile and outSvg.")}

        # `scripts/` is where the server archives what actually ran, one file per execution. A
        # writable working file in there would make the record disagree with itself.
        if candidate.parent.name == "scripts" and candidate.parent.parent == project:
            return {"ok": False, "error": (
                "scripts/ is the server's own archive of every execution and is not writable. "
                "Keep your working file at the project root, e.g. `artwork.js`.")}

        data = content.encode("utf-8")
        if len(data) > MAX_SCRIPT_BYTES:
            return {"ok": False, "error": (
                f"{len(data)} bytes is over the {MAX_SCRIPT_BYTES} limit. Split the drawing across "
                "files and compose them, or cut what is not doing work.")}

        candidate.parent.mkdir(parents=True, exist_ok=True)
        candidate.write_text(content, encoding="utf-8")

        # Versioned alongside the renders, in the same artifact store. `text/javascript` matters:
        # ADK inlines only image, audio, video and PDF, and falls through to a UTF-8 text
        # conversion for anything `text/*` — so a loaded script reaches the model as source rather
        # than as base64. The ADK console lists artifacts and their versions, so this is also what
        # makes the progression of a working file visible without reading the project directory.
        version = await tool_context.save_artifact(
            path.replace("\\", "/").lstrip("./"),
            types.Part.from_bytes(data=data, mime_type="text/javascript"))

        return {"ok": True, "path": path, "version": version, "bytes": len(data),
                "lines": content.count(chr(10)) + 1,
                "note": f"Saved as version {version}. Run it with "
                        f"ExecuteScript(scriptFile='{path}', outFile=...), then peek at the render."}

    return write_script


def _make_edit_script(project: Path):
    """Builds the `edit_script` tool bound to one project directory.

    **`write_script` alone does not capture the cost that justified it.** It takes the whole file,
    so changing one layer still means re-emitting the program — the same 20-37 seconds per call the
    measurement found. The SDK reference's advice is to *"change the layer you are working on"*,
    which presumes an editor. This is that: an exact-match replacement, so the model emits the lines
    it is changing rather than the ones it is keeping.

    Uniqueness is required rather than replacing the first hit. A repeated fragment silently edited
    in the wrong place produces a drawing that is wrong somewhere the agent is not looking, which is
    the failure mode this whole runtime keeps having to design against.
    """

    async def edit_script(path: str, old_text: str, new_text: str, tool_context) -> dict:
        """Change part of a script you already saved, without re-sending the whole file.

        Prefer this over write_script once a file exists. Send only the lines you are changing.

        Args:
            path: The script to edit, relative to the project directory, e.g. `artwork.js`.
            old_text: The exact text to replace, including its indentation. It must appear
                **exactly once** in the file — include a surrounding line or two if it does not.
            new_text: What to put there instead. Pass an empty string to delete.

        Returns:
            A dict with `ok`, the new `version`, and how many lines the file now has. On failure,
            `ok` is False and `error` says what to do.
        """
        candidate = (project / path).resolve()
        try:
            candidate.relative_to(project)
        except ValueError:
            return {"ok": False, "error": f"{path!r} resolves outside the project directory."}

        if not candidate.is_file():
            existing = sorted(p.name for p in project.glob("*.js"))
            return {"ok": False, "error": (
                f"no file at {path}. Create it with write_script first."
                + (f" Present: {', '.join(existing)}" if existing else ""))}

        current = candidate.read_text(encoding="utf-8")
        occurrences = current.count(old_text)
        if occurrences == 0:
            return {"ok": False, "error": (
                "old_text does not appear in the file. It must match exactly, including "
                "indentation and line breaks. Read the file back with InspectScript if unsure.")}
        if occurrences > 1:
            return {"ok": False, "error": (
                f"old_text appears {occurrences} times, so the edit is ambiguous. Include a "
                "surrounding line or two to make it unique.")}

        updated = current.replace(old_text, new_text)
        data = updated.encode("utf-8")
        if len(data) > MAX_SCRIPT_BYTES:
            return {"ok": False, "error": (
                f"the edit would make the file {len(data)} bytes, over the "
                f"{MAX_SCRIPT_BYTES} limit.")}

        candidate.write_text(updated, encoding="utf-8")
        version = await tool_context.save_artifact(
            path.replace("\\", "/").lstrip("./"),
            types.Part.from_bytes(data=data, mime_type="text/javascript"))

        return {"ok": True, "path": path, "version": version, "bytes": len(data),
                "lines": updated.count(chr(10)) + 1,
                "note": f"Edited, saved as version {version}. Re-run with "
                        f"ExecuteScript(scriptFile='{path}', outFile=...), then peek."}

    return edit_script


# --------------------------------------------------------------------------------------------
# Rate limiting.
# --------------------------------------------------------------------------------------------
#
# A design run is a burst: every tool call is an LLM turn, and a peeked render adds an image to the
# request. One measured run made 70 tool calls in 368 seconds with 13 image loads, which is enough
# to hit a per-minute quota even when the daily allowance is nowhere near spent — the key kept
# answering single probes throughout, so the 429s were rate, not exhaustion.
#
# ADK's own guidance is to enable client-side retries, and its example is
# `HttpRetryOptions(initial_delay=1, attempts=2)`. That is too weak here: a single retry one second
# later lands inside the same per-minute window and fails again. These values back off across
# roughly half a minute instead, which is the timescale a per-minute quota actually clears on.
#
# The other half of the remedy is not code: request higher quota for the model, or draft at a
# smaller canvas so each peeked image costs fewer tokens. `Polson.core.md` already says to draft
# small and render the final large, and under a rate limit that advice earns its keep twice.

#: Attempts *including* the original request, so 5 means four retries.
RETRY_ATTEMPTS = int(os.environ.get("POLSON_RETRY_ATTEMPTS", "8"))

#: 4s, then doubling, capped at 60 — 4+8+16+32+60+60+60, about **four minutes** across seven
#: retries.
#:
#: The first version budgeted 30s, which was wrong on its own reasoning: the note beside it said the
#: backoff should cover "the timescale a per-minute quota actually clears on", and 30s is less than
#: that window. A `comic_studio` run proved it — five agents, each `transfer_to_agent` opening a
#: fresh call with a new system prompt on top of growing history and images, exhausted the retries
#: and surfaced as an HTTP 500. A probe immediately afterwards answered in 2s, so the key was never
#: exhausted; the burst simply outlasted the patience.
#:
#: Four minutes is a long time to wait for one call, and it is the right trade here: a design run is
#: measured in minutes, and losing the whole turn costs far more than waiting.
RETRY_INITIAL_DELAY = 4.0
RETRY_EXP_BASE = 2.0
RETRY_MAX_DELAY = 60.0

#: Named rather than left to the default so it is visible that 429 is covered, which is the whole
#: point of the setting. 5xx are transient too and cost nothing to include.
RETRY_STATUS_CODES = [429, 500, 502, 503, 504]


def _retry_config() -> types.GenerateContentConfig:
    """Client-side backoff and thought summaries, applied to every agent this module builds."""
    return types.GenerateContentConfig(
        # **Thinking is billed whether or not it is returned, and it was being billed silently.**
        # Gemini emits a thought *part* only when summaries are asked for; without this the model
        # reasons, `thoughts_token_count` climbs, and `part.thought` is never true — so the
        # transcript's `thinking` branch could not fire and the record held none. Measured on the
        # kubrick8 run: 771 thinking tokens in a single turn, **zero** thinking events across all 40.
        #
        # That is the half of the record that says *why* the agent did something, missing from the
        # one place built to show it — and it is the deliberation half of the sense-making curve,
        # which reads a run with no thinking as pure action.
        thinking_config=types.ThinkingConfig(include_thoughts=True),
        http_options=types.HttpOptions(
            retry_options=types.HttpRetryOptions(
                attempts=RETRY_ATTEMPTS,
                initial_delay=RETRY_INITIAL_DELAY,
                exp_base=RETRY_EXP_BASE,
                max_delay=RETRY_MAX_DELAY,
                http_status_codes=RETRY_STATUS_CODES,
            )
        )
    )


# --------------------------------------------------------------------------------------------
# Per-turn timing.
# --------------------------------------------------------------------------------------------
#
# **Why this and not the run event log.** `projects/<id>/events/server.jsonl` already records every
# execution with a timestamp, and it survives restarts — it is what let us compare three server runs
# after the fact. But it measures the *engine*, so its cadence moves with the kind of work being
# done: a stress-test stage runs many tiny scripts and looks fast, an orientation stage runs four
# large ones and looks slow. Comparing those numbers across runs compares the workload, not the
# runtime.
#
# The model call is the part that does not depend on what stage the agent is in. Timing it gives a
# figure that means the same thing in every run: how long the model took to produce one turn, and
# how large that turn's request was. That is the number to compare when asking whether a change made
# things faster.
#
# Logged at WARNING so it survives the default log level without anyone configuring logging — these
# lines are diagnostics rather than warnings, and the level is chosen for visibility, not severity.

_turn_started: dict[str, float] = {}

_TURN_LOG = logging.getLogger("polson.turn")


# --------------------------------------------------------------------------------------------
# Time budgets.
# --------------------------------------------------------------------------------------------
#
# **Why the role files were not enough.** They now say "One corrective pass, then hand off." A
# prompt is advice, and the run that motivated this declined it: the Inker took 91 turns, 20 script
# versions, 16 renders and 58.8 minutes, and never reached the Colorist. Nothing in the loop counted
# anything, so the only thing that would eventually have stopped it was Cloud Run's 3600s request
# timeout — which is a wall, not a budget.
#
# **What this does and does not do.** It tells a role, in the turn it is about to take, how much of
# its allowance is gone. It does not stop the role. That is deliberate: the warning keeps the model
# in charge, and a model told it has four minutes left can choose to finish *well* — save the render,
# say what it shows, hand off — where a hard stop would cut it off mid-pass and lose the work. The
# short-circuiting hard stop is a separate lever (return an `LlmResponse` from this callback and set
# `callback_context.actions.transfer_to_agent`); it is not wired, on purpose.
#
# **Where the notice goes, and why not the obvious place.** `llm_request.append_instructions(...)`
# exists and is wrong here: it changes the system instruction, which is the head of the prompt cache
# prefix, and cached input bills at roughly a tenth of uncached. On a run of this size, warning that
# way could cost more than the overrun it prevents. Appending to `contents` puts the notice where
# new turns already go, so the cached prefix is untouched.
#
# **The notice evaporates, and that is correct.** `contents` is rebuilt from session events on every
# step, and this is injected into the request rather than recorded as an event — so the model sees
# each warning for exactly one turn. That is what a nudge should be. It also means the record of the
# warning lives only in the log line below, which is why that line exists.
#
# Time rather than tokens or turns, for one reason: the hard external constraint is the Cloud Run
# request timeout and it is denominated in seconds. A budget in the same currency as the wall is the
# one that stops you hitting it. A token budget would not have prevented a 58.8-minute Inker.
#
# > **This works on `run_async` and is silently inert on `run_live`.** Verified in ADK 2.8.0: the
# > async path hands the callback the real `LlmRequest` and calls the model with that same object
# > (`base_llm_flow.py:1735`), so appending to `contents` reaches the model. The bidi path first does
# > `llm_request.model_copy(update={'contents': [content]})` (`base_llm_flow.py:985`) and passes the
# > **copy**, so the notice is appended to a throwaway and discarded with no error. We serve over
# > `/run_sse`, which is the async path; if a live/bidi transport is ever added, this lever has to be
# > re-done against `actions` or a plugin rather than against `contents`.

#: Fractions of a role's allowance at which it is told what is left. Each fires at most once per
#: role per invocation — a warning repeated every turn stops being read and costs tokens to send.
#: Two rather than one because a single notice at 75% is forgotten by 95%; the second is sharper.
BUDGET_WARN_AT = (0.75, 0.90)

#: Share of the total allowance held back for the root agent's own review once the last role hands
#: back. Without it the facilitator inherits a spent clock and is warned on its first turn.
FACILITATOR_RESERVE = 0.15

#: How long past the *whole* allowance the circuit breaker waits before halting the invocation
#: outright. Grace, not budget: the warnings above have already asked twice by this point, and this
#: is what is left when they were ignored.
BREAKER_GRACE_MINUTES = 15.0

#: Entries are dropped after this long. The callback has no invocation-end hook to clean up on, so
#: an abandoned run would otherwise leave its clock behind forever.
_ROLE_CLOCK_TTL = 6 * 3600.0


@dataclass
class _RoleClock:
    """When a role first took a turn in one invocation, and which warnings it has already had."""

    started: float
    warned: set[float]


@dataclass(frozen=True)
class _RoleBudget:
    """One role's allowance, in seconds, and the agent it is expected to hand to when spent."""

    seconds: float
    hand_off_to: str | None


_role_clock: dict[tuple[str, str], _RoleClock] = {}

#: First model call of each invocation, and the invocations the breaker has already halted. Keyed on
#: `invocation_id`, which is what makes the breaker work at all — see the section below.
_invocation_started: dict[str, float] = {}
_tripped: set[str] = set()

#: Seconds of director-waiting already credited back, per invocation. See `credit_wait`.
_credited: dict[str, float] = {}

#: How much waiting an invocation may have back before the clock starts running again. Five minutes
#: is generous for a run where somebody is answering — a click is seconds — and bounds the drift
#: between the breaker's anchor and the wall for a run where nobody is. `POLSON_MAX_CREDIT_SECONDS`
#: overrides it; zero disables crediting entirely, which is what a fully unattended deployment wants.
try:
    MAX_CREDIT_SECONDS = max(0.0, float(os.environ.get("POLSON_MAX_CREDIT_SECONDS", "300")))
except ValueError:
    MAX_CREDIT_SECONDS = 300.0

# --------------------------------------------------------------------------------------------
# The token budget.
# --------------------------------------------------------------------------------------------
#
# **Input, not output, and not dollars.** Input is what spirals: every turn resends the whole
# conversation, so a run that will not converge grows its input as O(n²) while output stays bounded
# per turn. Measured on one small brief — 22 turns, a conversation growing 12K → 83K — the input
# summed to 1.2M against 11K of output. A cap on output would not have noticed.
#
# **Raw prompt tokens, deliberately, even though cached input bills at about a tenth.** Cost is not
# what this defends against; runaway is. Weighting by cache rate would make the number a better
# estimate of the bill and a worse alarm, because a loop that resends an identical prefix caches
# *well* and would be discounted precisely when it is most out of control. `cached=` and `out=` are
# recorded per turn alongside it, so the bill stays computable afterwards from the same log.
#
# **It is one turn late, and that is inherent.** Tokens are only known once the model has answered,
# so this halts the turn *after* the one that crossed the line. The overshoot is bounded by the
# largest single turn — 83K on the run above. Checking `llm_request.contents` beforehand would give
# an estimate rather than a count, and an estimate that disagreed with the log would be worse than
# a known one-turn lag.
_invocation_input: dict[str, int] = {}

#: Cached input, accumulated the same way. **A subset of `_invocation_input`, not an addition to
#: it** — `prompt_token_count` is the whole prompt and `cached_content_token_count` is the part of
#: that which was served from cache, so the two must never be summed.
#:
#: Reported rather than deducted, because it is the difference between a run being cheap and dear
#: and nothing else surfaces it. Measured across three runs of one brief, with identical shape and
#: comparable work: 54%, 46% and 29% cached, and **the run with the fewest turns and the least
#: input cost the most**. Caching here is implicit — we neither create nor control it — so this is
#: a fact to observe, not a lever to pull.
_invocation_cached: dict[str, int] = {}
_token_warned: dict[str, set[float]] = {}

#: What a cached input token costs relative to a fresh one, as a fraction.
#:
#: Approximate and provider-specific — a tenth is the figure this project has used since token
#: accounting went in. It is named rather than inlined so it is visibly an assumption rather than
#: arithmetic, and so a change of provider is one edit.
CACHED_TOKEN_SHARE = 0.1

#: How much raw input the runaway guard allows, as a multiple of the spend cap.
#:
#: Derived rather than configured so an existing deployment gains the guard without setting anything
#: new, and set by what separates the two cases rather than by taste: a cached token is charged at
#: `CACHED_TOKEN_SHARE`, so a *perfectly* cached run could reach ten times its spend cap in raw
#: tokens. Four leaves a well-cached run of real work comfortable — kubrick1's 2,005,223 raw against
#: a 2,000,000 spend cap sits at 0.25 of an 8,000,000 guard — while still bounding a loop.
#: `POLSON_BUDGET_RAW_TOKENS` overrides it outright.
RAW_CAP_MULTIPLE = 4


def credit_wait(invocation: str | None, agent_name: str | None, seconds: float) -> bool:
    """Gives back time the agent spent waiting on a person. False when there was nothing to credit.

    **Both clocks are wall-clock, and waiting is not working.** `_invocation_started` anchors the
    circuit breaker and `budget_status`'s `minutesRemaining`; `_role_clock` anchors the per-role
    allowance. A director thinking for ninety seconds moves both, and charging that to the agent
    would make `ask_director` a tool with a cost the agent cannot control — which is the surest way
    to teach it to guess instead of asking. Pushing the start forward by the wait is the whole fix:
    every later reading of `now - started` then excludes it.

    **It does not reach `Stage.elapsedMinutes`**, which the .NET side anchors on the session's own
    `StartedUtc` and this process cannot move. So a run that asks several questions will see the
    sandbox clock and `budget_status` disagree, the sandbox one reading higher. That is a real
    limitation rather than a rounding difference, and it is the reason `ask.TIMEOUT` is 120s and not
    the Antigravity path's 600.

    **Capped in total, and the cap is the part that matters on this host.** Crediting moves the
    breaker's anchor but not the wall, so an invocation that waits repeatedly drifts away from real
    time — and Cloud Run kills a request at **3600s** whatever the breaker thinks. The `drawing`
    workflow is where that bites: it asks at every turn boundary by design, its default deadline is
    45 minutes, and 45 plus the 15-minute breaker grace already *is* the 3600s ceiling with nothing
    to spare. Uncapped, a run nobody answers takes 120s of credit per turn and is killed mid-flight
    by the platform instead of being halted cleanly by the breaker — the exact failure the breaker
    exists to replace.

    So the first `MAX_CREDIT_SECONDS` of waiting are free and the rest is charged. Past the ceiling
    an agent that keeps asking into an empty room pays for it, which is the right incentive: the
    tool is for the few decisions that shape everything after them, not for every turn.
    """
    if not invocation or seconds <= 0:
        return False

    # Clamped rather than refused: a wait that straddles the ceiling is credited up to it. Refusing
    # the whole wait would make one long question cost more than two short ones summing to the same.
    already = _credited.get(invocation, 0.0)
    seconds = min(seconds, max(0.0, MAX_CREDIT_SECONDS - already))
    if seconds <= 0:
        _TURN_LOG.warning("polson budget: %s has spent its %.0fs of free waiting; the clock runs",
                          invocation, MAX_CREDIT_SECONDS)
        return False

    credited = False
    if invocation in _invocation_started:
        _invocation_started[invocation] += seconds
        credited = True

    if agent_name and (clock := _role_clock.get((invocation, agent_name))) is not None:
        clock.started += seconds
        credited = True

    if credited:
        # Counted only when it was actually applied, so an invocation with no clock yet — a tool
        # called before the first model call — does not silently burn its allowance on nothing.
        _credited[invocation] = already + seconds
        _TURN_LOG.warning("polson budget: %.0fs of waiting credited back to %s (%.0fs of %.0fs used)",
                          seconds, invocation, _credited[invocation], MAX_CREDIT_SECONDS)
    return credited


def _billable(invocation: str) -> int:
    """Input tokens weighted by what they actually cost, which is what the cap should measure.

    **The breaker used to count raw input, and that is the wrong quantity.** Input is cumulative —
    the whole conversation is resent every turn — so the counter climbs whether or not the run is
    achieving anything, and cached tokens climb it at full rate while billing at a tenth.

    Measured on the kubrick1 deployed run: halted at **2,005,223** raw input tokens after ~25 turns,
    with the last turns reporting **83,737 cached of 87,975** — around 95%. Cache-adjusted, that run
    had spent closer to 300,000 tokens' worth. It was stopped as though it had spent seven times
    what it did, for doing the thing that makes a run cheap.

    Derived from the two counters rather than accumulated separately, so it cannot drift from them
    and `budget_status` keeps reporting the raw figures a reader recognises from the turn log.
    """
    raw = _invocation_input.get(invocation, 0)
    cached = _invocation_cached.get(invocation, 0)

    # `cached` is a *subset* of `raw`, never an addition, so the fresh part is the difference.
    # Clamped because the two are accumulated from separate fields of separate responses, and a
    # provider that reported them inconsistently should not produce a negative allowance.
    fresh = max(0, raw - cached)
    return int(fresh + cached * CACHED_TOKEN_SHARE)


# --------------------------------------------------------------------------------------------
# The circuit breaker.
# --------------------------------------------------------------------------------------------
#
# The warnings above are advice with a clock attached; a model may still decline them. This is the
# thing that does not ask. At `budget + BREAKER_GRACE_MINUTES` from the invocation's first model
# call, **no agent in that invocation makes another model call**, and the run unwinds.
#
# **Why it is a module-level set and not `end_invocation`.** The obvious implementation is to set
# `invocation_context.end_invocation = True`, which every agent loop checks
# (`base_llm_flow.py:1300`, `:1422`, `llm_agent.py:610`). It does not work on its own, for two
# reasons found by reading ADK 2.8.0 rather than by guessing:
#
#   1. `BaseAgent._create_invocation_context` does `parent_context.model_copy(...)`, a **shallow**
#      copy, so `end_invocation` set inside a sub-agent never reaches the facilitator that
#      transferred to it. The facilitator would simply transfer somewhere else.
#   2. The public route to the context, `callback_context.get_invocation_context()`, returns a
#      **copy** — its own docstring says so — so setting the flag on it changes nothing at all.
#
# What *is* shared is `invocation_id`: the copy keeps it. So the breaker is a set of tripped
# invocation ids, consulted at the top of every `before_model_callback`. Every model call in the
# process passes through there, whichever agent makes it, so once an id is in the set no further
# call can be issued under it. That is the enforcement; `end_invocation` is set too, best-effort,
# only so the current agent unwinds this turn instead of next.
#
# **What it does not stop.** A tool call already in flight. The breaker sits between model calls, so
# a running `ExecuteScript` finishes first — bounded by the engine's own script timeout, which is
# why that is survivable rather than a hole. It also does not stop the *process*; it halts one
# invocation, which is the runaway unit that costs money.


def _make_halt_response(elapsed: float, cap: float, limit: str = "time"):
    """The turn a halted agent gets instead of a model call.

    Phrased as the runtime speaking and naming the numbers, because this text lands in the
    transcript: a halt that reads like the agent deciding it had finished would be worse than no
    message, and someone reading the run later has to be able to tell the two apart.

    `limit` names which cap was hit, so the transcript distinguishes a run that ran long from one
    that ran expensive — they call for different fixes and would otherwise read identically.
    """
    from google.adk.models.llm_response import LlmResponse

    if limit == "tokens":
        breached = (
            f"spent {elapsed:,.0f} billable input tokens against a limit of {cap:,.0f}"
        )
    elif limit == "raw":
        # Worded so the agent can tell this apart from running out of money: reaching the runaway
        # guard means the conversation grew without converging, which is a different thing to fix.
        breached = (
            f"resent {elapsed:,.0f} raw input tokens against a runaway limit of {cap:,.0f} — the "
            "conversation kept growing without the work converging"
        )
    else:
        breached = f"passed its hard limit of {cap / 60:.0f} minutes ({elapsed / 60:.0f} used)"

    return LlmResponse(
        content=types.Content(
            role="model",
            parts=[
                types.Part(
                    text=(
                        f"[studio runtime] HALTED. This run {breached} and was stopped by the "
                        f"circuit breaker. No further model calls will be made under this "
                        f"invocation. Work already written to the project directory is intact; "
                        f"anything in progress at the moment of the halt is not."
                    )
                )
            ],
        )
    )


def _env_minutes(name: str) -> float | None:
    """A minutes-valued environment variable, or None. A value that will not parse is ignored.

    Ignored rather than raised: an unreadable budget should not stop a studio from starting, and a
    deployment that mistypes it would otherwise fail at import with nothing drawn. Logged, though —
    a silently ignored cap is how a runaway run gets blamed on the breaker not working.
    """
    raw = os.environ.get(name, "").strip()
    if not raw:
        return None
    try:
        return float(raw)
    except ValueError:
        _TURN_LOG.warning("%s=%r is not a number, ignored.", name, raw)
        return None


def _env_tokens(name: str) -> int | None:
    """An integer-valued environment variable, or None. Ignored, and logged, if it will not parse.

    Ignored rather than raised for the same reason as `_env_minutes`: a mistyped cap should not stop
    a studio from starting. Accepts `2_000_000` and `2,000,000`, because a seven-digit number typed
    into a deploy command is easy to get wrong by an order of magnitude and both spellings are what
    people reach for to make it readable.
    """
    raw = os.environ.get(name, "").strip().replace("_", "").replace(",", "")
    if not raw:
        return None
    try:
        return int(float(raw))
    except ValueError:
        _TURN_LOG.warning("%s=%r is not a number, ignored.", name, os.environ.get(name))
        return None


def breaker_seconds(
    budget_minutes: float | None,
    breaker_minutes: float | None = None,
    grace_minutes: float | None = None,
) -> float | None:
    """The hard cap for one invocation, in seconds, or None for no breaker.

    An explicit `breaker_minutes` wins; then `POLSON_MAX_MINUTES`, which arms the breaker on its own
    so a deployment can be protected without adopting per-role budgets; then the budget plus its
    grace. A budget with no cap of its own is always covered, because the case this exists for —
    a role that will not stop — is exactly the case where the budget was ignored.
    """
    explicit = breaker_minutes if breaker_minutes is not None else _env_minutes("POLSON_MAX_MINUTES")
    if explicit is not None:
        return explicit * 60.0 if explicit > 0 else None
    if not budget_minutes or budget_minutes <= 0:
        return None
    grace = grace_minutes if grace_minutes is not None else _env_minutes(
        "POLSON_BREAKER_GRACE_MINUTES"
    )
    return (budget_minutes + (BREAKER_GRACE_MINUTES if grace is None else grace)) * 60.0


def budget_plan(
    role_names: list[str],
    budget_minutes: float | None,
    weights: dict[str, float] | None = None,
    root_name: str = "facilitator",
    reserve: float = FACILITATOR_RESERVE,
) -> dict[str, _RoleBudget]:
    """Splits an allowance across a pipeline, keyed by agent name.

    Roles arrive in `roles_in` order — the leading number on the filename — so the successor of each
    is simply the next one, and the last hands back to the root. An empty or non-positive budget
    returns an empty plan, which is how "no budgeting" is spelled.

    The default split is **even**, not weighted toward the early stages. A decaying weight is a
    plausible prior for a pipeline — construction usually costs more than critique — but it is a
    guess about workflows this function has never seen, and a guess that silently starves the Critic
    is worse than an even split anyone can override with `weights`.
    """
    if not budget_minutes or budget_minutes <= 0:
        return {}

    total = budget_minutes * 60.0
    if not role_names:
        # Single-agent: the whole allowance, and nobody to hand to.
        return {root_name: _RoleBudget(total, None)}

    share = total * (1.0 - reserve)
    weighted = {n: max(0.0, float((weights or {}).get(n, 1.0))) for n in role_names}
    divisor = sum(weighted.values()) or float(len(role_names))

    plan = {
        name: _RoleBudget(
            share * (weighted[name] or 1.0) / divisor,
            role_names[i + 1] if i + 1 < len(role_names) else root_name,
        )
        for i, name in enumerate(role_names)
    }
    # Not `plan[root_name] = ...`: a workflow whose role file is named `facilitator` would otherwise
    # have its own allowance silently replaced by the reserve.
    plan.setdefault(root_name, _RoleBudget(total * reserve, None))
    return plan


def _budget_notice(elapsed: float, allowance: float, hand_off_to: str | None, urgent: bool) -> str:
    """What the role is told. Marked as the runtime speaking, so it is not read as the client."""
    # Floored, not rounded: a role told it has "4 minutes" when it has 3.5 will spend four. Erring
    # short is the safe direction, and it is why this reads `int()` rather than `:.0f`.
    left_min = int(max(0.0, allowance - elapsed) // 60)
    left = "under a minute" if left_min < 1 else f"about {left_min} minute{'s' if left_min > 1 else ''}"
    total_min = max(1, int(allowance // 60))
    head = (
        f"[studio runtime] Your time for this role is almost gone — {left} left of {total_min}."
        if urgent
        else f"[studio runtime] Time check: {left} left of your {total_min} minute allowance "
        f"for this role."
    )
    close = (
        f"Finish the pass you are on — save the render with `outFile`, `peek` at it, say what it "
        f"shows — then `transfer_to_agent` to '{hand_off_to}'. Do not begin another corrective pass "
        f"or a new exploration."
        if hand_off_to
        else "Finish the pass you are on, save the render with `outFile`, and bring the work to a "
        "close. Do not begin another corrective pass or a new exploration."
    )
    return f"{head} {close}"


def _prune_role_clocks(now: float) -> None:
    """Drops clocks, counters and trip marks from runs that ended, or died, long ago.

    **The size guard reads every map it prunes, not just the clocks.** It used to check
    `_role_clock` alone, and role clocks only exist when a *time* budget does — so a single-agent
    run with a token cap and no deadline created none, the guard returned immediately, and
    `_invocation_started`, `_tripped` and the token counters grew for the life of the process.
    There is no invocation-end hook to clean up on, so this is the only thing that bounds them.
    """
    if max(len(_role_clock), len(_invocation_started), len(_invocation_input),
           len(_invocation_cached), len(_credited)) < 64:
        return
    stale = {
        key for key, clock in _role_clock.items() if now - clock.started > _ROLE_CLOCK_TTL
    }
    for key in stale:
        del _role_clock[key]
    for invocation, started in list(_invocation_started.items()):
        if now - started > _ROLE_CLOCK_TTL:
            del _invocation_started[invocation]
            _tripped.discard(invocation)
            _invocation_input.pop(invocation, None)
            _invocation_cached.pop(invocation, None)
            _token_warned.pop(invocation, None)
            _credited.pop(invocation, None)


def _trip_breaker(callback_context, elapsed: float, cap: float, limit: str = "time"):
    """Halts this invocation and returns the response the agent gets instead of a model call."""
    invocation = callback_context.invocation_id
    first = invocation not in _tripped
    _tripped.add(invocation)
    # A tripped invocation has passed the cap by definition. Clamping keeps the message honest in
    # the one case where it would not be: a start time already dropped by the pruner reads as zero
    # elapsed, and "passed its limit of 105 minutes (0 used)" is worse than saying nothing.
    elapsed = max(elapsed, cap)
    # `_after_model` never runs for a short-circuited turn, so its entry would otherwise be left.
    _turn_started.pop(invocation, None)

    # Best-effort only, and allowed to fail: this reaches a private attribute, and its sole benefit
    # is that the current agent unwinds on this turn rather than on its next one. The trip set above
    # is what actually enforces the halt, so a future ADK that renames this changes nothing.
    try:
        callback_context._invocation_context.end_invocation = True
    except AttributeError:  # pragma: no cover - depends on ADK internals
        pass

    # Left for the transcript's `run.end` to spend, on the first trip only — the breaker fires again
    # on every agent that turns up afterwards, and a halted run ended once. Without this the record
    # shows a run that simply stops: `run.begin` written, the halt visible only in the server's own
    # log, and nothing on the page a director is watching to say the run is over rather than stuck.
    if first:
        transcript.note_halt(invocation, limit=limit, used=elapsed, cap=cap)

    if limit in ("tokens", "raw"):
        # Both figures on either path, because they differ several-fold on a well-cached run and the
        # raw one is what the turn log has been showing all along. A halt reported only as the
        # billable number would look wrong against those lines; only as the raw number, it would
        # misstate a spend cap. Which limit was hit is named, since the two mean different things:
        # "spend" is this run costing too much, "runaway" is it not converging.
        _TURN_LOG.log(
            logging.ERROR if first else logging.WARNING,
            "BREAKER %s/%s halted on %s at %.0f (cap %.0f; %.0f raw, %.0f cached, %.0f billable)%s",
            callback_context.agent_name, invocation,
            "spend" if limit == "tokens" else "runaway", elapsed, cap,
            _invocation_input.get(invocation, 0), _invocation_cached.get(invocation, 0),
            _billable(invocation),
            "" if first else " [already tripped]",
        )
    else:
        _TURN_LOG.log(
            logging.ERROR if first else logging.WARNING,
            "BREAKER %s/%s halted at %.1f min (cap %.1f min)%s",
            callback_context.agent_name, invocation, elapsed / 60, cap / 60,
            "" if first else " [already tripped]",
        )
    return _make_halt_response(elapsed, cap, limit)


def _make_before_model(budget: _RoleBudget | None, cap: float | None = None,
                       token_cap: int | None = None, raw_cap: int | None = None):
    """The per-turn clock, the budget warning, and the circuit breaker.

    A closure per agent rather than one shared function: the allowance and the successor differ by
    role, and `callback_context` carries the agent's name but not its budget. `cap` is the same for
    every agent — the breaker is a property of the invocation, not of a role.
    """

    def before_model(callback_context, llm_request):
        now = time.monotonic()
        invocation = callback_context.invocation_id

        # The breaker comes first, and returns before anything else can happen. Once an invocation
        # is tripped it stays tripped, so an agent transferred to after the halt is stopped on its
        # first turn rather than getting one free model call.
        if cap:
            if invocation in _tripped:
                return _trip_breaker(callback_context, now - _invocation_started.get(invocation, now), cap)
            started = _invocation_started.setdefault(invocation, now)
            if now - started >= cap:
                return _trip_breaker(callback_context, now - started, cap)

        # Both token limits share the trip set with the clock, so whichever fires first halts the
        # invocation and none of the others can un-halt it. Checked even when there is no time cap:
        # a run can be given one budget without the other.
        #
        # **Spend first, then runaway.** They measure different things — billable tokens against the
        # cost ceiling, raw against the loop guard — and a halt has to name which one it was, or a
        # reader comparing the number against the turn log finds it does not match either count.
        if token_cap or raw_cap:
            if invocation in _tripped:
                return _trip_breaker(callback_context, _billable(invocation),
                                     token_cap or raw_cap or 0, "tokens")

            if token_cap and (spent := _billable(invocation)) >= token_cap:
                return _trip_breaker(callback_context, spent, token_cap, "tokens")

            if raw_cap and (raw := _invocation_input.get(invocation, 0)) >= raw_cap:
                return _trip_breaker(callback_context, raw, raw_cap, "raw")

        _turn_started[invocation] = now
        _prune_role_clocks(now)

        # Told once per threshold per invocation, on `contents` rather than the system instruction —
        # the latter is the head of the cache prefix, and cached input bills at roughly a tenth, so
        # warning that way can cost more than the overrun it prevents.
        if token_cap:
            spent = _billable(invocation)
            share = spent / token_cap
            seen = _token_warned.setdefault(invocation, set())
            crossed = [t for t in BUDGET_WARN_AT if share >= t and t not in seen]
            if crossed:
                seen.update(crossed)
                _TURN_LOG.warning(
                    "budget %s/%s %.0f%% of token cap (%.0f of %.0f input tokens)",
                    callback_context.agent_name, invocation, share * 100, spent, token_cap)
                # And to the *page*. The container log and the agent's own context were the only
                # two places this went, so a director watching a run spend its allowance saw
                # nothing until it stopped.
                transcript.note_budget(invocation, limit="tokens", share=share,
                                       spent=spent, cap=token_cap)
                llm_request.contents.append(types.Content(role="user", parts=[types.Part(
                    text=(f"[studio runtime] You have used {share * 100:.0f}% of this run's input-token "
                          f"budget ({spent:,.0f} of {token_cap:,.0f}). Input is the whole conversation "
                          f"resent every turn, so it grows whether or not you are making progress. "
                          f"Finish what is drawn and write it out rather than starting a new pass; at "
                          f"100% the run is halted where it stands."))]))

        if budget is None or budget.seconds <= 0:
            return None

        key = (invocation, callback_context.agent_name)
        clock = _role_clock.get(key)
        if clock is None:
            # The clock starts at a role's *first* turn, not at the invocation's start — a role
            # three handoffs down the pipeline has not spent any of its own allowance waiting.
            _prune_role_clocks(now)
            _role_clock[key] = _RoleClock(now, set())
            return None

        elapsed = now - clock.started
        share = elapsed / budget.seconds
        crossed = [t for t in BUDGET_WARN_AT if share >= t and t not in clock.warned]
        if not crossed:
            return None

        # Going straight past both thresholds in one turn consumes both, so the gentler notice is
        # not delivered after the sharper one.
        clock.warned.update(crossed)
        highest = max(crossed)
        _TURN_LOG.warning(
            # ASCII only: this line is a grep target in a container log, not prose.
            "budget %s/%s %.0f%% used (%.1f of %.1f min) warned, next=%s",
            callback_context.agent_name,
            callback_context.invocation_id,
            share * 100,
            elapsed / 60,
            budget.seconds / 60,
            budget.hand_off_to or "nobody",
        )
        # The clock's warning reaches the page for the same reason the token one does: a director is
        # the only party who can act on "this role is running out of time", and they were not told.
        transcript.note_budget(callback_context.invocation_id, limit="time", share=share,
                               spent=elapsed / 60, cap=budget.seconds / 60)
        llm_request.contents.append(
            types.Content(
                role="user",
                parts=[
                    types.Part(
                        text=_budget_notice(
                            elapsed, budget.seconds, budget.hand_off_to, highest >= 0.90
                        )
                    )
                ],
            )
        )
        return None

    return before_model


def _after_model(callback_context, llm_response):
    """Reports how long the turn took, and what it cost to send.

    Keyed on `invocation_id` rather than a single module-level timestamp, because sub-agents in a
    multi-agent app interleave turns and a shared slot would attribute one agent's latency to
    another.
    """
    started = _turn_started.pop(callback_context.invocation_id, None)
    if started is None:
        return None

    elapsed = time.monotonic() - started
    usage = getattr(llm_response, "usage_metadata", None)
    prompt_tokens = getattr(usage, "prompt_token_count", None) if usage else None
    output_tokens = getattr(usage, "candidates_token_count", None) if usage else None
    # Cached input bills at roughly a tenth of uncached, so without this any cost figure derived
    # from the log is an upper bound rather than a number. A 23.7M-token run could not be priced
    # accurately for exactly this reason.
    cached_tokens = getattr(usage, "cached_content_token_count", None) if usage else None

    # Reasoning tokens, billed as output and reported apart from it. **This is the only number that
    # sees an agent thinking rather than working**, and it is the one signal the watchdog cannot
    # have: every trigger it owns is counted from tool calls, so an agent that thinks in circles and
    # calls nothing never reaches `after_tool_callback` at all. A thinking loop reads here as
    # `think=` climbing while `out=` stays flat.
    thought_tokens = getattr(usage, "thoughts_token_count", None) if usage else None

    # What the tool definitions and their results cost, separable from the conversation. Worth
    # having because tool output is the part we control: it answers "what did that Search response
    # actually cost" directly, instead of inferring it from the growth between two turns.
    tool_tokens = getattr(usage, "tool_use_prompt_token_count", None) if usage else None

    # The token budget's counter. Here rather than in `before_model` because this is the only place
    # the number exists — which is also why the breaker is one turn late.
    if prompt_tokens:
        invocation = callback_context.invocation_id
        _invocation_input[invocation] = _invocation_input.get(invocation, 0) + int(prompt_tokens)
        if cached_tokens:
            _invocation_cached[invocation] = _invocation_cached.get(invocation, 0) + int(cached_tokens)

    # ASCII, one line, fixed key order: this is a grep target in a container log, not prose. Absent
    # counts print as 0 rather than being dropped, so a parser can split on the same fields every
    # turn — except in= and out=, where "?" says the model reported nothing and 0 would be a claim.
    _TURN_LOG.warning(
        "turn %s/%s %.1fs in=%s cached=%s out=%s think=%s tooluse=%s",
        callback_context.agent_name,
        callback_context.invocation_id,
        elapsed,
        prompt_tokens if prompt_tokens is not None else "?",
        cached_tokens if cached_tokens is not None else 0,
        output_tokens if output_tokens is not None else "?",
        thought_tokens if thought_tokens is not None else 0,
        tool_tokens if tool_tokens is not None else 0,
    )
    return None


#: Appended to every agent's instruction on this runtime.
#:
#: The project's `GEMINI.md` is written for a host that supplies its own file and image tools —
#: Antigravity Desktop, Claude Code — and ADK supplies neither. `peek` and `write_script` are how
#: those verbs exist here, and an agent that is not told about them will do what the first real run
#: did: render to disk, never look, and describe an image it could not see.
#:
#: Kept short and marked as runtime-specific so it reads as an amendment to the brief rather than a
#: competing set of instructions.
RUNTIME_ADDENDUM = """

---

## This runtime (ADK)

These tools exist here that the instructions above do not mention, because they replace what a
desktop host would otherwise provide.

- **`read_file(path)` — read a text file in the project.** Start with `brief.md`: it is the
  client's actual request, and nothing else can open it for you.
- **`peek(artifact_path)` — look at what you rendered.** You cannot see a render otherwise. After
  every `outFile`, peek at it, then `load_artifacts` to bring it into view, and only then say what
  you see. Describing a render you have not peeked at is guessing, and it will be wrong.
- **`write_script(path, content)` — keep your drawing in a file.** Once a piece is more than a
  screenful, save it as `artwork.js` and run it with `ExecuteScript(scriptFile='artwork.js', ...)`.
- **`edit_script(path, old_text, new_text)` — change part of it.** Use this for every revision
  after the first save. Send only the lines you are changing, then re-run the file. Re-sending a
  whole program in order to change part of it is the single largest cost in a run.
- **`ask_director(question, options)` — put a question to the director and wait.** They answer by
  clicking, so offer options wherever you can and put the one you would take first.

`write_script` and `edit_script` keep numbered versions, so nothing you overwrite is lost.

### Asking, and when not to

**The brief you were given may say "where the brief is silent, decide". That was written for a
runtime with nobody to ask, and this is not one.** A director is watching this run and can settle a
direction with one click, which is often what they came to do — most briefs are a sentence, and the
rest of the commission is in their head rather than in the file.

So: **ask about the few decisions that shape everything after them** — the subject's treatment, the
palette's temperament, which of two readings of the brief to take — and decide the rest yourself, as
you were told to. A pass is cheap and the record makes it reversible, so anything you can settle by
drawing it and looking is not worth a question. Do not ask permission to proceed.

Nobody may be there. An unanswered question comes back after __ASK_TIMEOUT__ seconds saying so, and
the answer is then to choose, say in a `Stage.note` which you chose and why, and carry on — exactly
as if you had never asked. The waiting time is not charged against your deadline.
"""

# **Substituted rather than interpolated, and this is not a style preference.** ADK resolves `{name}`
# in an instruction against session state, so a stray brace here does not render as a literal — it
# kills the agent at startup with a missing-key error, which is a failure this project has already
# had once from a workflow template. A placeholder with no braces in it cannot.
RUNTIME_ADDENDUM = RUNTIME_ADDENDUM.replace("__ASK_TIMEOUT__", f"{ask.TIMEOUT:.0f}")


# --------------------------------------------------------------------------------------------
# The advisor — a role's way of asking for a second opinion.
# --------------------------------------------------------------------------------------------
#
# Wrapped as an `AgentTool`, so a stuck role can *call* it and get an answer back without control
# ever leaving that role. This is the one place ADK offers something genuinely message-shaped:
# `transfer_to_agent` moves control, whereas this is request and response.
#
# **It is a separate agent, not the Facilitator herself.** Two reasons, and the second is the real
# one. An `AgentTool` runs its agent in a **fresh in-memory session** seeded with state but not with
# conversation history (`agent_tool.py:264-289`), so passing the root in would not give it the
# Facilitator's memory anyway — and passing an agent that owns sub-agents invites a transfer inside
# what is supposed to be a single question. More importantly, fresh eyes are the point: the advice
# should come from looking at the *artifact*, not from having sat through the ninety turns that
# produced it. That is what an art director does when asked to come and look.
#
# It has `peek` and `read_file` and nothing else. It cannot draw, so it cannot answer a question by
# quietly doing the work itself — which is the failure mode that would make the whole mechanism
# worthless, because the role would learn to delegate rather than to think.

#: The tool's name as the roles call it. `AgentTool` takes its name from the agent, so this is both.
ADVISOR_NAME = "ask_facilitator"

#: This becomes the tool's description, which is what the roles actually read when deciding whether
#: to call it. Written for that audience rather than as a summary of the code.
ADVISOR_DESCRIPTION = (
    "Ask the art director for a second opinion when you are stuck or going in circles. Send what "
    "you are trying to achieve, what you have already tried, and the path of your latest render. "
    "They will look at it with fresh eyes and answer with a direction to take."
)

ADVISOR_INSTRUCTION = """# Role: Art director, consulted

A member of the studio has stopped making progress and has been told to ask you. You are being
called for one answer, not for a conversation: what you say goes straight back to them and then you
are gone.

1. **Read `brief.md`** with `read_file`. Their problem is usually that they have drifted from it.
2. **Look at the render they name**, with `peek`, then `load_artifacts`. Do not answer without
   looking. If they gave you no path, say so and ask for one — that is a complete answer.
3. **Say what to do next**, in three sentences or fewer.

What makes this useful is that you have not been in their head for the last hour. Say the obvious
thing. If the work is further along than they think, tell them to stop and hand off — that is the
most valuable answer you can give and the one they are least able to reach on their own.

You cannot draw and must not try. No script, no code, no markup: a direction, in words.
"""


def role_deadline_note(budget: _RoleBudget | None, total_minutes: float | None) -> str:
    """A role's own share of the deadline, appended to its brief. Empty when there is no budget.

    Appended to the **instruction**, which is the system prompt and constant for the whole run — so
    unlike the running `[studio runtime]` notices it costs the prompt cache nothing, and unlike them
    it arrives before the first decision rather than after three-quarters of the time is gone. The
    project's instructions state the whole deadline; only this can state the split, because only the
    runtime knows it.
    """
    if budget is None or budget.seconds <= 0:
        return ""

    whole = f" of the project's {total_minutes:.0f}" if total_minutes else ""
    onward = (
        f"hand off to `{budget.hand_off_to}`"
        if budget.hand_off_to
        else "bring the work to a close"
    )
    return f"""

---

## Your share of the deadline

**You have about {budget.seconds / 60:.0f} minutes{whole}, and the stage after yours depends on you
leaving it.** Decide before you start how many passes that buys, and plan for the last one to be a
check rather than a rescue.

When the time is nearly gone, save what you have with `outFile`, say what it shows, and {onward} —
a finished stage handed on late is worth less than a plainer one handed on in time, because everyone
after you inherits the overrun.

If you are going round in circles, call `ask_facilitator` rather than trying again harder. Describe
what you are after, what you have tried, and the path of your latest render. Going again on something
that is not working is the usual way a deadline is missed.
"""


def advisor_for(project: Path, model: str, perception: list) -> Agent:
    """The agent behind `ask_facilitator`. Looking tools only — deliberately no drawing."""
    return Agent(
        model=model,
        name=ADVISOR_NAME,
        description=ADVISOR_DESCRIPTION,
        instruction=ADVISOR_INSTRUCTION,
        tools=perception,
        generate_content_config=_retry_config(),
    )


def build(
    project_dir: str | Path,
    *,
    model: str | None = None,
    cli_dll: str | Path | None = None,
    transfer_between_roles: bool = True,
    budget_minutes: float | None = None,
    role_weights: dict[str, float] | None = None,
    breaker_minutes: float | None = None,
    breaker_grace_minutes: float | None = None,
    budget_tokens: int | None = None,
) -> Agent:
    """The studio for one generated project, as a single agent or a role tree.

    A workflow with `roles/` becomes a root plus one sub-agent per role file — the multi-agent case
    from `CLAUDE.md` §5, where the root holds the Facilitator's job. A workflow without becomes one
    agent carrying the whole brief, which §1 says is the default and not the fallback.

    `transfer_between_roles` leaves ADK's default topology, where a role may hand off directly to a
    peer — a penciler to an inker, which is how a pipeline actually runs. Set it False to force
    every handoff back through the root, closer to the Facilitator-mediated model of §3C.

    `budget_minutes` splits a wall-clock allowance across the roles, warning each as its share runs
    down; `role_weights` skews that split. Falls back to `POLSON_BUDGET_MINUTES`, and to no
    budgeting at all when neither is set.

    `breaker_minutes` is the hard cap on the whole invocation, after which no agent makes another
    model call. It defaults to the budget plus `breaker_grace_minutes` (15), and can be set on its
    own — via the argument or `POLSON_MAX_MINUTES` — to arm the breaker without adopting per-role
    budgets. The warnings are craft; the breaker is a cost control, and they are separable on
    purpose.
    """
    project = Path(project_dir).expanduser().resolve()
    if not project.is_dir():
        raise ConfigError(f"{project} is not a directory.")

    # Environment is the fallback, not the source: an app names its own project explicitly, but the
    # model and the engine path are deployment facts. POLSON_CLI_DLL especially — a container's
    # layout is not the repository's.
    chosen_model = model or os.environ.get("POLSON_MODEL", "").strip() or DEFAULT_MODEL
    chosen_dll = cli_dll or os.environ.get("POLSON_CLI_DLL", "").strip() or None
    instruction = instructions_for(project) + RUNTIME_ADDENDUM
    # One toolset, shared. See the module docstring for why, and for what it costs.
    toolset = toolset_for(project, Path(chosen_dll) if chosen_dll else None)
    # Perception. Every agent gets these: a role that cannot see its own work is the failure
    # this pair exists to prevent, and it fails silently.
    # Split so the advisor can be given the looking half without the writing half.
    looking = [
        FunctionTool(_make_peek(project)),
        FunctionTool(_make_read_file(project)),
        LoadArtifactsTool(),
    ]
    perception = [
        *looking,
        FunctionTool(_make_write_script(project)),
        FunctionTool(_make_edit_script(project)),
    ]

    roles = roles_in(project)
    root_name = "facilitator" if roles else "polson"

    # No per-project default: unlike a deadline, which the workflow sets because a logo and a study
    # painting are different commissions, a token cap is a property of the deployment paying for it.
    # A public URL wants one; a developer iterating locally usually does not.
    token_cap = budget_tokens if budget_tokens is not None else _env_tokens("POLSON_BUDGET_TOKENS")
    if token_cap is not None and token_cap <= 0:
        token_cap = None

    # **Two limits, because there are two failure modes and one number cannot see both.**
    #
    # `token_cap` is the *spend* ceiling and is measured in billable tokens, so a well-cached run is
    # not punished for the thing that makes it cheap. `raw_cap` is the *runaway* guard and counts
    # raw input, because a loop resending an identical prefix caches beautifully — it is the
    # best-cached traffic there is — so a cost-weighted count discounts a stuck agent by tenfold at
    # exactly the moment it is most out of control.
    #
    # Weighting alone would have been a straight trade of one failure for the other: kubrick1 was
    # halted at 2,005,223 raw tokens while ~95% cached, having spent perhaps 300,000 tokens' worth,
    # and cost-weighting would have let a genuine loop run ten times longer. Whichever trips first
    # wins, and each is named in the halt so a reader knows which one it was.
    raw_cap = _env_tokens("POLSON_BUDGET_RAW_TOKENS")
    if raw_cap is None and token_cap:
        raw_cap = token_cap * RAW_CAP_MULTIPLE
    if raw_cap is not None and raw_cap <= 0:
        raw_cap = None

    if token_cap:
        _TURN_LOG.warning("token budget: %s billable input tokens per invocation (runaway guard: "
                          "%s raw)", f"{token_cap:,}",
                          f"{raw_cap:,}" if raw_cap else "none")

    # Appended here rather than with the other project tools because it needs the caps, and those
    # are resolved below the perception list — the resolution reads in the order the values are
    # decided, which is worth more than having every tool in one literal.
    perception.append(FunctionTool(_make_budget_status(
        token_cap, deadline_in(project) or _env_minutes("POLSON_BUDGET_MINUTES"))))

    # **Only the root agent may ask, which is why this is its own list rather than another entry in
    # `perception`.** A question suspends whoever calls it until a person answers, so giving it to
    # every role in a multi-agent run is how four agents come to queue four questions at one
    # director — and the Facilitator is the role holding the brief, so it is the one that knows what
    # is genuinely unsettled rather than merely locally ambiguous. A role that wants a ruling asks
    # the Facilitator, which is what `transfer` is already for.
    asking = []
    if (ask_tool := ask.make_tool(project)) is not None:
        asking.append(FunctionTool(ask_tool))
    # Environment is the fallback here for the same reason it is for the model: an allowance is a
    # deployment fact, and the container is where a run's deadline is actually known.
    # Explicit argument, then the project's own deadline, then the deployment-wide default.
    # The project beats the environment because a deadline belongs to the commission: a
    # 15-minute logo and a two-hour study painting cannot share one number, and the project is
    # the only place that distinction is recorded.
    allowance = budget_minutes
    if allowance is None:
        allowance = deadline_in(project) or _env_minutes("POLSON_BUDGET_MINUTES")
    plan = budget_plan([name for name, _, _ in roles], allowance, role_weights, root_name)
    cap = breaker_seconds(allowance, breaker_minutes, breaker_grace_minutes)
    if cap:
        _TURN_LOG.warning("breaker armed at %.1f min per invocation", cap / 60)

    # Only in the multi-agent case: a single agent carrying the whole brief has no art director to
    # ask, and giving it one would be asking itself.
    #
    # `include_plugins=False` keeps the watchdog out of the advisor's own sub-run, so a supervisor
    # cannot end up supervising the consultation it caused.
    advice = (
        [AgentTool(agent=advisor_for(project, chosen_model, looking), include_plugins=False)]
        if roles
        else []
    )

    sub_agents = [
        Agent(
            model=chosen_model,
            name=name,
            description=description,
            instruction=prompt + role_deadline_note(plan.get(name), allowance),
            tools=[toolset, *perception, *advice],
            generate_content_config=_retry_config(),
            before_model_callback=_make_before_model(plan.get(name), cap, token_cap, raw_cap),
            after_model_callback=_after_model,
            disallow_transfer_to_peers=not transfer_between_roles,
        )
        for name, description, prompt in roles
    ]

    return Agent(
        model=chosen_model,
        name=root_name,
        description=(
            "Coordinates a multi-agent design studio." if sub_agents else
            "An enactive co-creative design studio. Writes JavaScript that draws, renders it, "
            "looks at the result, and revises."
        ),
        instruction=instruction,
        # The root holds the toolset too: in the single-agent case it is the only worker, and in the
        # multi-agent case the Facilitator still needs to look at what the roles produced. `asking`
        # is here and nowhere else — see where it is built.
        tools=[toolset, *perception, *asking],
        generate_content_config=_retry_config(),
        before_model_callback=_make_before_model(plan.get(root_name), cap, token_cap, raw_cap),
        after_model_callback=_after_model,
        sub_agents=sub_agents,
    )


def build_app(project_dir: str | Path, *, name: str | None = None, **kwargs) -> App:
    """The same studio, wrapped as an `App` so a supervising plugin can be attached.

    ADK's loader checks a module for `app` before `root_agent` (`agent_loader.py:128`), so a
    generated `agent.py` exporting this is served exactly as one exporting a bare agent — no change
    to how the studio is deployed, and an older generated file keeps working.

    The watchdog is given the **same** toolset instance the agents hold, found by type rather than by
    position. A second `McpToolset` would spawn a second `Polson.CLI server` process, and the module
    docstring's whole argument for one server per app would quietly stop being true.
    """
    project = Path(project_dir).expanduser().resolve()
    root = build(project, **kwargs)

    # The same precedence `build` uses. Duplicated deliberately rather than guessed at: if these two
    # ever disagreed, the watchdog would be timing the roles against a different clock from the one
    # they were told about, which is the sort of fault that looks like a flaky model.
    allowance = kwargs.get("budget_minutes")
    if allowance is None:
        allowance = deadline_in(project) or _env_minutes("POLSON_BUDGET_MINUTES")
    plan = budget_plan(
        [role for role, _, _ in roles_in(project)],
        allowance,
        kwargs.get("role_weights"),
        root.name,
    )

    # The conversation half of the record, and the copy that survives the machine. Appended rather
    # than replacing the watchdog: they observe the same run for different reasons, and a plugin that
    # fails to build must not take the others with it — both `make_plugin`s return None instead of
    # raising, and this drops them.
    #
    # `mirror` returns None whenever `POLSON_MIRROR_URI` is unset, so a local checkout registers
    # nothing and behaves exactly as it did before.
    plugins = [p for p in (transcript.make_plugin(project), mirror.make_plugin(project),
                           interject.make_plugin(project))
               if p is not None]

    return App(
        name=name or project.name,
        root_agent=root,
        plugins=[
            *plugins,
            StudioWatchdog(
                role_seconds={agent: budget.seconds for agent, budget in plan.items()},
                toolset=next((t for t in root.tools if isinstance(t, McpToolset)), None),
                # None in a single-agent app: `build` only attaches the advisor where there are
                # roles, so naming it there would direct an agent at a tool it does not have. A live
                # logo run was told to call `ask_facilitator` and had no such tool.
                advisor_tool=ADVISOR_NAME if roles_in(project) else None,
            )
        ],
    )
