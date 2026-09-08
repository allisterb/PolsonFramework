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
        """The command, with any relative `--project-dir` made absolute.

        The generator writes an absolute path now, so this is normally a no-op. It stays for
        hand-made and older projects that say `.`: under a desktop host that `.` resolved to the
        repository root, which put the run record — and `outFile`'s containment root — somewhere
        other than the project, without failing.
        """
        args = [str(root) if a == "." else a for a in self.args]
        return self.command, args


#: Wiring filenames each host uses, in the order to look for them. `.agents/mcp_config.json` is
#: Antigravity's canonical workspace location and the only one the generator writes; the project root
#: stays as a fallback because the host reads it too and older projects put it there.
WIRING_FILES = {
    "agy": (".agents/mcp_config.json", "mcp_config.json"),
    # Google ADK writes Antigravity's files — both runtimes are Gemini and read `GEMINI.md` — and is
    # recorded separately because who *drives* a project is a different question from what the agent
    # *reads*. `session_since` is the reader that needs the distinction.
    "adk": (".agents/mcp_config.json", "mcp_config.json"),
    "claude": (".mcp.json",),
}


@dataclass(frozen=True)
class Project:
    """One design project, read from its own directory."""

    root: Path
    id: str
    workflow: str
    sdk: str
    profile: str
    conversation_id: str | None
    agent_behavior: str
    instructions_file: str
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
        """What the project was generated for — a label, not a capability.

        Every Antigravity project carries `agent.config.json` and can be run either way, so this
        does not decide whether the orchestrator will run it: the presence of that file does, and
        `load` checks it. Use this for display, never as a gate.
        """
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


def read(directory: Path) -> Project:
    """A project as a *reader* needs it, which is far less than a driver does.

    `load` answers "can the orchestrator run an agent here", and every one of its checks is about
    that: the SDK must be Antigravity, there must be a tool policy, instructions, and an MCP server
    with a command. A reader starts nothing, so none of those apply to it — and applying them anyway
    refused exactly the case worth reading. A project generated for Claude Code is the ordinary
    subject of an observation, and `load` turns it away with "the orchestrator builds Antigravity SDK
    configurations only", which is true and beside the point.

    What a reader actually needs is the manifest, for the project's identity, and the `events/`
    directory the record lives in. That is all this requires.

    The safety this gives up is nothing: `Registry.start` calls `load` itself rather than being handed
    a project, so a project read this way cannot reach the driving path without being re-validated
    there.
    """
    root = Path(directory).resolve()

    if not root.is_dir():
        raise ProjectError(f"no such project directory: {root}")

    manifest = _read_json(root / "project.json", "the project manifest")

    return Project(
        root=root,
        id=manifest.get("id") or root.name,
        workflow=manifest.get("workflow") or "logo",
        sdk=(manifest.get("sdk") or "agy").lower(),
        profile=manifest.get("profile") or "standalone",
        conversation_id=manifest.get("conversationId"),

        # Defaults, not discoveries. Nothing that reads a record touches any of these, and a project
        # loaded this way is never given to the agent — see the note above.
        agent_behavior="interactive",
        instructions_file="GEMINI.md",
        denied_tools=(),
        mcp=McpWiring(name="", command="", args=()),
        save_dir=root / "session/save",
        app_data_dir=root / "session/appdata",
    )


def load(directory: Path) -> Project:
    """Reads a project directory, or explains why it cannot be run.

    For reading a project's record rather than running it, use `read` — this refuses several things
    that only matter to a run.
    """
    root = Path(directory).resolve()

    if not root.is_dir():
        raise ProjectError(f"no such project directory: {root}\n"
                           f"       make one with:  polson create-project {root}")

    manifest = _read_json(root / "project.json", "the project manifest")

    # The orchestrator hosts the agent with the Antigravity SDK, so a project generated for another
    # host is not merely inconvenient here — its instructions file, its wiring and its permissions all
    # belong to a host that is not running.
    sdk = (manifest.get("sdk") or "agy").lower()
    if sdk not in WIRING_FILES:
        raise ProjectError(f"{root} names an unknown sdk '{sdk}' in project.json.")
    if sdk != "agy":
        raise ProjectError(f"{root} was generated for '{sdk}', and the orchestrator builds Antigravity "
                           f"SDK configurations only.\n"
                           f"       Open it with that host, or generate an 'agy' project to run here.")

    # Without this file there are no denied tools — including generate_image, the one control the
    # whole studio's premise rests on — so refusing beats running wide open.
    #
    # Every Antigravity project now carries it, whatever profile it was generated for, so this is no
    # longer the managed/standalone line: it is a project generated before that change, or one for
    # another host. Either way the fix is to regenerate, and no flag is needed to get it.
    policy_file = root / "agent.config.json"
    if not policy_file.exists():
        raise ProjectError(f"{root} has no agent.config.json, so the orchestrator has no tool policy "
                           f"for it and would run the agent with none at all.\n"
                           f"       Regenerate it to add one:  polson create-project <parent> {root.name} agy")
    policy = _read_json(policy_file, "the agent config")

    instructions = root / "GEMINI.md"
    if not instructions.exists():
        raise ProjectError(f"{root} has no GEMINI.md — the agent would start with no instructions. "
                           f"Is it a generated project?")

    wiring_file = next((root / n for n in WIRING_FILES[sdk] if (root / n).exists()), None)
    if wiring_file is None:
        raise ProjectError(f"{root} has none of {', '.join(WIRING_FILES[sdk])}, so the agent would "
                           f"have nothing to draw with.")

    wiring = _read_json(wiring_file, "the MCP wiring")
    servers = wiring.get("mcpServers") or {}
    if not servers:
        raise ProjectError(f"{wiring_file} declares no MCP servers, so the agent would have "
                           f"nothing to draw with.")

    name, server = next(iter(servers.items()))
    command = server.get("command")
    if not command:
        raise ProjectError(f"the '{name}' MCP server in {wiring_file} has no command.")

    return Project(
        root=root,
        id=manifest.get("id") or root.name,
        workflow=manifest.get("workflow") or "logo",
        sdk=sdk,
        profile=manifest.get("profile") or "standalone",
        conversation_id=manifest.get("conversationId"),
        agent_behavior=policy.get("agentBehavior") or "interactive",
        # Named by the generator, which knows the host. Defaulting to GEMINI.md keeps older
        # projects working, and this module already refuses anything but an `agy` project.
        instructions_file=policy.get("instructionsFile") or "GEMINI.md",
        denied_tools=tuple(policy.get("deniedTools") or ()),
        mcp=McpWiring(name=name, command=command, args=tuple(server.get("args") or ())),
        save_dir=root / (policy.get("saveDir") or "session/save"),
        app_data_dir=root / (policy.get("appDataDir") or "session/appdata"),
    )
