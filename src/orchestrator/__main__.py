"""Command line entry: run one turn against a project directory.

    python -m orchestrator <project> [--prompt "..."]        # from src/webapp
    python src/webapp/run_studio.py <project>                # from the repository root

Both are the same program. There is no web server here yet — Milestone 6 adds one over the same
`run_turn`, and until then the terminal is the director.
"""

from __future__ import annotations

import argparse
import asyncio
import sys
from pathlib import Path

from . import project as project_mod
from . import run as run_mod
from .credentials import CLI_DLL, CredentialError, read_api_key

#: The agent's instructions are already in the project's GEMINI.md; this only starts it reading.
DEFAULT_PROMPT = (
    "Read GEMINI.md in this project directory, then brief.md, and begin the project. "
    "Work through the stages it names, declaring each one with Stage.begin, and save every render "
    "to artifacts/ with outFile."
)


def summarize(result: run_mod.RunResult, project: project_mod.Project) -> None:
    """Prints what the record now says, which is the thing worth checking after a run."""
    print(f"\n  {'completed' if result.completed else 'DID NOT COMPLETE'}"
          f"{'' if not result.error else ' — ' + result.error}")

    if result.reply:
        print(f"\n  agent: {result.reply.strip()[:1500]}")

    for name, counts in (("agent.jsonl", result.agent_events), ("server.jsonl", result.server_events)):
        if not counts:
            print(f"\n  {name} — nothing recorded")
            continue
        print(f"\n  events/{name} — {sum(counts.values())} event(s)")
        for kind, count in sorted(counts.items()):
            print(f"    {count:3d}  {kind}")

    if result.questions:
        print(f"\n  the director answered {result.questions} question(s) — events/director.jsonl")

    if not result.server_closed:
        print("\n  note: the MCP server left no run.end, so it was killed rather than closed.")
        print("  The orchestrator's own run.end in agent.jsonl is the authoritative one.")

    artifacts = sorted((project.root / "artifacts").glob("*"))
    if artifacts:
        print(f"\n  artifacts/ — {len(artifacts)} file(s)")
        for path in artifacts[-8:]:
            print(f"    {path.name}  ({path.stat().st_size} bytes)")


async def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="orchestrator", description=__doc__)
    parser.add_argument("project", type=Path, help="a directory made by 'polson create-project'")
    parser.add_argument("--prompt", default=DEFAULT_PROMPT, help="what to say to the agent")
    parser.add_argument("--timeout", type=float, default=900.0,
                        help="seconds to wait for the turn (default 900)")
    parser.add_argument("--model", help="override the model")
    parser.add_argument("--public", action="store_true",
                        help="use the public Gemini endpoint instead of the Agent Platform one")
    parser.add_argument("--fresh", action="store_true",
                        help="ignore the saved conversation and start a new one")
    parser.add_argument("--unattended", action="store_true",
                        help="answer nothing; questions are recorded and skipped")
    args = parser.parse_args(argv)

    try:
        project = project_mod.load(args.project)
    except project_mod.ProjectError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1

    if not CLI_DLL.exists():
        print(f"error: the MCP server is not built: {CLI_DLL}\n       build it with:  dotnet build",
              file=sys.stderr)
        return 1

    try:
        read_api_key()   # fails now, with guidance, rather than three steps into a run
    except CredentialError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1

    print(f"  project  : {project.root}")
    print(f"  workflow : {project.workflow} · sdk {project.sdk} · {project.profile}")
    print(f"  denied   : {', '.join(project.denied_tools) or '(nothing)'}")
    print(f"  endpoint : {'public Gemini API' if args.public else 'Agent Platform (vertex)'}")
    print(f"  resuming : {project.conversation_id or '(new conversation)'}\n")

    result = await run_mod.run_turn(
        project,
        args.prompt,
        interactive=not args.unattended,
        timeout=args.timeout,
        public=args.public,
        model=args.model,
        resume=not args.fresh,
    )

    summarize(result, project)
    return 0 if result.completed else 2


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
