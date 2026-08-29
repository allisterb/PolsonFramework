"""Running one design project: the agent's lifecycle, and the record it leaves.

This is Milestone 5 — the thing every remaining milestone sits on. It owns the agent session, the
tool policy, the director, and the two event files the Python side writes. It does not own a web
server; Milestone 6 wraps this rather than replacing it, which is why the director is a hook and
the record is a file.

Three constants are carried in deliberately rather than rediscovered, because each cost a session:

- **`vertex=True`.** Our key is an Agent Platform credential; against the public endpoint it returns
  `403 ... are blocked`. See `credentials`.
- **`policy.allow_all()` alongside the denies.** The policy engine is fail-closed, and supplying
  `policies` at all replaces the permissive default — a list of denies denies *everything else too*,
  including `ExecuteScript`, and the agent loops retrying in a way indistinguishable from a hang. A
  specific deny is priority 1 and a global allow is priority 9, so the denies still win.
- **`conversation.send()` + `receive_steps()`.** `chat()` + `await response.text()` hides everything
  until the turn ends, so a stall cannot be told apart from slowness.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from google.antigravity import Agent, LocalAgentConfig, types
from google.antigravity.hooks import policy

from . import director as director_mod
from .credentials import read_api_key
from .events import EventLog, read_events
from .project import Project
from .transcript import Transcript

#: Recorded in full, capped only so one paste cannot dominate the file.
MAX_MESSAGE = 2000


@dataclass
class RunResult:
    """What happened, in a form a caller can act on without re-reading the record."""

    completed: bool
    reply: str = ""
    error: str | None = None
    conversation_id: str | None = None
    agent_events: dict[str, int] = field(default_factory=dict)
    server_events: dict[str, int] = field(default_factory=dict)
    interactive: bool = False
    questions: int = 0

    @property
    def server_closed(self) -> bool:
        """Whether the MCP server got to close its own bracket.

        `run.end` is written from `ApplicationStopping`, which is reached when the server's stdin
        ends — so this is really a statement about whether the host shut the server down or killed
        it. The SDK closes it properly; the .NET reference MCP client does not. Its absence is not
        evidence that the run died, which is why the orchestrator brackets the run in its own file
        regardless.
        """
        return self.server_events.get("run.end", 0) > 0


def build_config(
    project: Project,
    *,
    interactive: bool,
    hooks: list[Any] | None = None,
    public: bool = False,
    model: str | None = None,
    resume: bool = True,
) -> LocalAgentConfig:
    """Assembles the agent configuration from what the project directory already declares.

    Nothing here is invented: the denied tools, the behaviour, and the session directories are all
    generated into `agent.config.json` by `polson create-project`, and the MCP wiring into
    `.mcp.json`. The orchestrator's job is to honour them, not to have its own opinion — a policy
    that lived in Python would be a second source of truth for the one thing that must not drift.
    """
    command, args = project.mcp.resolved(project.root)

    server = types.McpStdioServer(name=project.mcp.name, command=command, args=args)

    policies = [policy.deny(tool) for tool in project.denied_tools]
    policies.append(policy.allow_all())   # see the module docstring — not optional

    behavior = types.AgentBehavior.INTERACTIVE if interactive else types.AgentBehavior.AUTONOMOUS

    project.save_dir.mkdir(parents=True, exist_ok=True)
    project.app_data_dir.mkdir(parents=True, exist_ok=True)

    return LocalAgentConfig(
        system_instructions=(
            f"You are the designer on the Polson project in {project.root}. Your instructions are "
            f"in GEMINI.md in that directory — read it first, and follow it. Use the "
            f"{project.mcp.name} MCP tools to draw. Save every render with outFile, relative to the "
            f"project directory."
        ),
        mcp_servers=[server],
        policies=policies,
        hooks=hooks or [],
        capabilities=types.CapabilitiesConfig(agent_behavior=behavior),
        workspaces=[str(project.root)],
        save_dir=str(project.save_dir),
        app_data_dir=str(project.app_data_dir),
        conversation_id=project.conversation_id if resume else None,
        session_continuation_mode=(
            types.SessionContinuationMode.CREATE_OR_RESUME if resume and project.conversation_id else None
        ),
        model=model,
        api_key=read_api_key(),
        vertex=not public,
    )


async def _consume(agent: Any, transcript: Transcript, *, echo: bool) -> str:
    """Transcribes the turn as it happens and returns the agent's final reply.

    Every step goes to `agent.jsonl`; `echo` only controls whether a person watching also sees it.
    """
    async for step in agent.conversation.receive_steps():
        transcript.observe(step)

        if echo:
            kind = getattr(getattr(step, "type", None), "name", "STEP")
            if calls := getattr(step, "tool_calls", None):
                names = ", ".join(str(getattr(c, "name", "?")) for c in calls)
                print(f"    {kind:<16} -> {names}")

        if getattr(step, "is_complete_response", False):
            return getattr(step, "content", "") or ""

    return ""


async def run_turn(
    project: Project,
    prompt: str,
    *,
    interactive: bool = True,
    timeout: float = 900.0,
    public: bool = False,
    model: str | None = None,
    resume: bool = True,
    echo: bool = True,
) -> RunResult:
    """Runs one turn against a project, writing `agent.jsonl` and `director.jsonl` as it goes."""
    agent_log = EventLog(project.agent_events, "agent")
    director_log = EventLog(project.director_events, "director")

    transcript = Transcript(agent_log)
    director = director_mod.for_run(director_log, interactive=interactive)
    attended = isinstance(director, director_mod.ConsoleDirector)

    config = build_config(
        project,
        interactive=attended,
        hooks=[director],
        public=public,
        model=model,
        resume=resume,
    )

    agent_log.append(
        "run.start",
        project=project.id,
        workflow=project.workflow,
        profile=project.profile,
        behavior="interactive" if attended else "autonomous",
        resumed=bool(resume and project.conversation_id) or None,
    )

    # The director's own words, in the director's own file. It is the same text a visitor will type
    # in Milestone 6, so it is recorded as data and never merged into anything generated.
    director_log.append("message", text=prompt[:MAX_MESSAGE], truncated=len(prompt) > MAX_MESSAGE or None)

    result = RunResult(completed=False, interactive=attended)

    try:
        # Leaving this block shuts the agent down, which is what closes the MCP server's stdin and
        # lets it write its own run.end. Killing it instead would lose that, so the run is never
        # torn down by cancelling around the context manager.
        async with Agent(config) as agent:
            transcript.turn_start(prompt)
            await agent.conversation.send(prompt)

            try:
                result.reply = await asyncio.wait_for(_consume(agent, transcript, echo=echo), timeout=timeout)
                result.completed = True
                transcript.turn_end("done")
            except asyncio.TimeoutError:
                result.error = f"the turn did not complete within {timeout:.0f}s"
                transcript.turn_end("timeout", result.error)

            result.conversation_id = getattr(agent, "conversation_id", None)

    except Exception as exc:  # noqa: BLE001 — reported, not swallowed
        result.error = f"{type(exc).__name__}: {exc}"
        transcript.turn_end("error", result.error)

    agent_log.append("run.end", status="ok" if result.completed else "incomplete", error=result.error)

    project.record_conversation(result.conversation_id)

    result.agent_events = transcript.counts
    result.questions = director.asked
    result.server_events = _tally(project.server_events)

    return result


def _tally(path: Path) -> dict[str, int]:
    counts: dict[str, int] = {}
    for event in read_events(path):
        kind = event.get("type", "?")
        counts[kind] = counts.get(kind, 0) + 1
    return counts
