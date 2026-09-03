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

import os
import re
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
    instruction = instructions_for(project)
    # One toolset, shared. See the module docstring for why, and for what it costs.
    toolset = toolset_for(project, Path(chosen_dll) if chosen_dll else None)
    # Perception. Every agent gets these: a role that cannot see its own work is the failure
    # this pair exists to prevent, and it fails silently.
    perception = [FunctionTool(_make_peek(project)), LoadArtifactsTool()]

    sub_agents = [
        Agent(
            model=chosen_model,
            name=name,
            description=description,
            instruction=prompt,
            tools=[toolset, *perception],
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
        sub_agents=sub_agents,
    )
