"""Reading a generated project directory.

`polson create-project` writes the directory; this reads it back. The manifest, the tool policy and
the MCP wiring are all generated files, so they are trusted — but `brief.md` is not, and nothing
here ever loads it. The brief reaches the agent by being *referenced* from `GEMINI.md`, which keeps
the boundary between "instructions we wrote" and "text a stranger typed" a file boundary rather
than a paragraph break.

See `docs/project-layout.md`, which this module is a direct reading of.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


class ProjectError(Exception):
    """A project directory that cannot be run, with a message saying what to do about it."""


@dataclass(frozen=True)
class McpWiring:
    """How to start the MCP server for this project, as the generator recorded it."""

    name: str
    command: str
    args: tuple[str, ...]

    def resolved(self, root: Path) -> tuple[str, list[str]]:
        """The command with `--project-dir .` made absolute.

        The generated file says `.` because a project directory has to stay movable; the server is
        not launched from inside it, so the orchestrator substitutes the real path.
        """
        args = [str(root) if a == "." else a for a in self.args]
        return self.command, args


@dataclass(frozen=True)
class Project:
    """One design project, read from its own directory."""

    root: Path
    id: str
    workflow: str
    profile: str
    conversation_id: str | None
    agent_behavior: str
    denied_tools: tuple[str, ...]
    mcp: McpWiring
    save_dir: Path
    app_data_dir: Path

    #: Everything the orchestrator writes. One writer per file — see `events.EventLog`.
    events_dir: Path = field(init=False)

    def __post_init__(self) -> None:
        object.__setattr__(self, "events_dir", self.root / "events")

    @property
    def is_standalone(self) -> bool:
        """Standalone means the Python orchestrator supplies the human; managed means a desktop host does."""
        return self.profile == "standalone"

    @property
    def server_events(self) -> Path:
        return self.events_dir / "server.jsonl"

    @property
    def agent_events(self) -> Path:
        return self.events_dir / "agent.jsonl"

    @property
    def director_events(self) -> Path:
        return self.events_dir / "director.jsonl"

    def record_conversation(self, conversation_id: str | None) -> None:
        """Writes the conversation id into the manifest, which is what makes a run resumable.

        Failing to record it costs the resume, not the run, so this reports rather than raises.
        """
        if not conversation_id:
            return

        manifest = self.root / "project.json"
        try:
            data = json.loads(manifest.read_text(encoding="utf-8-sig"))
            data["conversationId"] = conversation_id
            manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="")
        except Exception as exc:  # noqa: BLE001
            print(f"warning: could not record the conversation id in {manifest}: {exc}")


def _read_json(path: Path, what: str) -> dict[str, Any]:
    if not path.exists():
        raise ProjectError(f"{what} is missing: {path}")
    try:
        # utf-8-sig because the .NET side may write a BOM, and json.loads chokes on one.
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        raise ProjectError(f"{what} is not valid JSON ({path}): {exc}") from exc


def load(directory: Path) -> Project:
    """Reads a project directory, or explains why it cannot be run."""
    root = Path(directory).resolve()

    if not root.is_dir():
        raise ProjectError(f"no such project directory: {root}\n"
                           f"       make one with:  polson create-project {root}")

    manifest = _read_json(root / "project.json", "the project manifest")

    if not (root / "GEMINI.md").exists():
        raise ProjectError(f"{root} has no GEMINI.md — the agent would start with no instructions. "
                           f"Is it a generated project?")

    wiring = _read_json(root / ".mcp.json", "the MCP wiring")
    servers = wiring.get("mcpServers") or {}
    if not servers:
        raise ProjectError(f"{root / '.mcp.json'} declares no MCP servers, so the agent would have "
                           f"nothing to draw with.")

    name, server = next(iter(servers.items()))
    command = server.get("command")
    if not command:
        raise ProjectError(f"the '{name}' MCP server in {root / '.mcp.json'} has no command.")

    # Both profiles get an agent.config.json; they differ in its `enforced` flag, because in the
    # managed profile a desktop host owns tool policy and the file is a record of intent. Here it is
    # always applied — running a managed project through the orchestrator makes us the enforcer, and
    # honouring the weaker managed deny list beats honouring nothing. Tolerating the file's absence
    # is for hand-made and older directories, which then get no denies at all.
    policy_file = root / "agent.config.json"
    policy = _read_json(policy_file, "the agent config") if policy_file.exists() else {}

    return Project(
        root=root,
        id=manifest.get("id") or root.name,
        workflow=manifest.get("workflow") or "logo",
        profile=manifest.get("profile") or "standalone",
        conversation_id=manifest.get("conversationId"),
        agent_behavior=policy.get("agentBehavior") or "interactive",
        denied_tools=tuple(policy.get("deniedTools") or ()),
        mcp=McpWiring(name=name, command=command, args=tuple(server.get("args") or ())),
        save_dir=root / (policy.get("saveDir") or "session/save"),
        app_data_dir=root / (policy.get("appDataDir") or "session/appdata"),
    )
