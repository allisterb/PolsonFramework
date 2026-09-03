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

import logging
import os
import re
import time
from pathlib import Path

from google.adk.agents.llm_agent import Agent
from google.adk.tools import FunctionTool
from google.adk.tools.load_artifacts_tool import LoadArtifactsTool
from google.adk.tools.mcp_tool import McpToolset
from google.adk.tools.mcp_tool import StdioConnectionParams
from google.genai import types
from mcp import StdioServerParameters

#: Repository root, from `src/adk_agent/studio.py`.
REPO_ROOT = Path(__file__).resolve().parents[2]

#: The built MCP server. Same artifact the Antigravity wiring points at; not rebuilt here.
DEFAULT_CLI_DLL = REPO_ROOT / "bin" / "cli" / "Polson.CLI.dll"

#: Instructions filenames, in the order `ProjectGenerator.HostFiles` can have written them.
INSTRUCTIONS_FILENAMES = ("GEMINI.md", "CLAUDE.md")

#: `adk create` in ADK 2.8.0 offers this first, and it is confirmed reachable on this project's
#: Agent Platform key. A demanding brief may want `gemini-2.5-pro`, also confirmed. **Gemini 3
#: pro is NOT available on it** — `gemini-3-pro` and `gemini-3-pro-preview` both return 404
#: NOT_FOUND from us-east4, so do not assume a newer name resolves just because it exists.
DEFAULT_MODEL = "gemini-3.5-flash"

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

#: What a script can render. `outSvg` writes text, which the model reads better as source anyway.
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
            return {"ok": False, "error": (
                f"{candidate.suffix!r} is not a peekable image. Peekable: "
                f"{', '.join(sorted(PEEKABLE))}. An .svg is text — read it another way.")}

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


#: What `write_script` will author. Deliberately one extension: this is not a general file-write
#: tool, and widening it would make it one.
SCRIPT_SUFFIX = ".js"

#: A guard against a runaway generation, not a style rule. The largest script in a measured
#: four-agent run was well under this.
MAX_SCRIPT_BYTES = 512 * 1024


#: What `read_file` will open. Text only — an image is `peek`'s job, and a binary handed to a model
#: as mojibake is worse than a refusal that says which tool to use.
READABLE_SUFFIXES = {
    ".md", ".txt", ".json", ".js", ".svg", ".csv", ".yaml", ".yml", ".xml", ".html", ".css",
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

        if candidate.suffix.lower() != SCRIPT_SUFFIX:
            return {"ok": False, "error": (
                f"{path!r} does not end in {SCRIPT_SUFFIX}. This tool writes drawing scripts only — "
                "renders go to disk through ExecuteScript's outFile and outSvg.")}

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
RETRY_ATTEMPTS = int(os.environ.get("POLSON_RETRY_ATTEMPTS", "5"))

#: 2s, then doubling — about 30s of total backoff across four retries.
RETRY_INITIAL_DELAY = 2.0
RETRY_EXP_BASE = 2.0
RETRY_MAX_DELAY = 30.0

#: Named rather than left to the default so it is visible that 429 is covered, which is the whole
#: point of the setting. 5xx are transient too and cost nothing to include.
RETRY_STATUS_CODES = [429, 500, 502, 503, 504]


def _retry_config() -> types.GenerateContentConfig:
    """Client-side backoff, applied to every agent this module builds."""
    return types.GenerateContentConfig(
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


def _before_model(callback_context, llm_request):
    """Starts the clock for one model call. Returns None so the call proceeds normally."""
    _turn_started[callback_context.invocation_id] = time.monotonic()
    return None


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

    _TURN_LOG.warning(
        "turn %s/%s %.1fs in=%s out=%s",
        callback_context.agent_name,
        callback_context.invocation_id,
        elapsed,
        prompt_tokens if prompt_tokens is not None else "?",
        output_tokens if output_tokens is not None else "?",
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

Two tools exist here that the instructions above do not mention, because they replace what a
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

Both keep numbered versions, so nothing you overwrite is lost.
"""


def build(
    project_dir: str | Path,
    *,
    model: str | None = None,
    cli_dll: str | Path | None = None,
    transfer_between_roles: bool = True,
) -> Agent:
    """The studio for one generated project, as a single agent or a role tree.

    A workflow with `roles/` becomes a root plus one sub-agent per role file — the multi-agent case
    from `CLAUDE.md` §5, where the root holds the Facilitator's job. A workflow without becomes one
    agent carrying the whole brief, which §1 says is the default and not the fallback.

    `transfer_between_roles` leaves ADK's default topology, where a role may hand off directly to a
    peer — a penciler to an inker, which is how a pipeline actually runs. Set it False to force
    every handoff back through the root, closer to the Facilitator-mediated model of §3C.
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
    perception = [
        FunctionTool(_make_peek(project)),
        FunctionTool(_make_read_file(project)),
        FunctionTool(_make_write_script(project)),
        FunctionTool(_make_edit_script(project)),
        LoadArtifactsTool(),
    ]

    sub_agents = [
        Agent(
            model=chosen_model,
            name=name,
            description=description,
            instruction=prompt,
            tools=[toolset, *perception],
            generate_content_config=_retry_config(),
            before_model_callback=_before_model,
            after_model_callback=_after_model,
            disallow_transfer_to_peers=not transfer_between_roles,
        )
        for name, description, prompt in roles_in(project)
    ]

    return Agent(
        model=chosen_model,
        name="facilitator" if sub_agents else "polson",
        description=(
            "Coordinates a multi-agent design studio." if sub_agents else
            "An enactive co-creative design studio. Writes JavaScript that draws, renders it, "
            "looks at the result, and revises."
        ),
        instruction=instruction,
        # The root holds the toolset too: in the single-agent case it is the only worker, and in the
        # multi-agent case the Facilitator still needs to look at what the roles produced.
        tools=[toolset, *perception],
        generate_content_config=_retry_config(),
        before_model_callback=_before_model,
        after_model_callback=_after_model,
        sub_agents=sub_agents,
    )
