"""Spike: can we drive a Gemini agent from Python with the Polson MCP server attached?

This is a throwaway probe, not architecture. Everything in Milestones 5 and 6 rests on one
untested assumption — that the Antigravity SDK can run an agent headlessly, attach our MCP server,
and leave a usable record behind — and nothing here has ever been executed. This answers that in
about a hundred lines so the answer arrives before a web app is built on top of it.

What it proves, in order:

  1. the SDK imports and its bundled `localharness` binary is found
  2. an agent starts and reaches the model
  3. our MCP server attaches over stdio and its tools are callable
  4. the deny policy actually denies
  5. `events/server.jsonl` is written, and the run is legible afterwards

All five were confirmed on 2026-08-28: the agent explored the project, called ExecuteScript, and
left a stage-tagged render behind. Keep it working — it is the cheapest check that the Python side
still reaches the .NET side at all.

Run it against a directory made by `polson create-project`:

    python src/webapp/hello_agent.py path/to/project

The credential comes from `bin/cli/appsettings.json` (`ApiKeys:GoogleAgentPlatform`), the same one
the .NET side uses; `GEMINI_API_KEY` overrides it. That key is an **Agent Platform** credential, so
the default endpoint here is the Agent Platform one — `--public` switches to the public Gemini API.

Nothing here installs anything; see README.md.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CLI_DLL = REPO_ROOT / "bin" / "cli" / "Polson.CLI.dll"
CLI_SETTINGS = REPO_ROOT / "bin" / "cli" / "appsettings.json"


def fail(message: str) -> None:
    print(f"error: {message}", file=sys.stderr)
    raise SystemExit(1)


def read_api_key() -> str:
    """The same credential the .NET side uses, so there is one place to keep it.

    `ApiKeys:GoogleAgentPlatform` in the CLI's appsettings.json is an **Agent Platform** key, not a
    public Gemini API key. Pointing it at generativelanguage.googleapis.com returns
    `403 ... are blocked`, which is what happens by default because the SDK assumes the public
    endpoint. GEMINI_API_KEY overrides, for a public key.
    """
    if env_key := os.environ.get("GEMINI_API_KEY"):
        return env_key

    if not CLI_SETTINGS.exists():
        fail(f"no API key: set GEMINI_API_KEY, or put one in {CLI_SETTINGS}")

    settings = json.loads(CLI_SETTINGS.read_text(encoding="utf-8-sig"))
    key = settings.get("ApiKeys", {}).get("GoogleAgentPlatform")

    if not key:
        fail(f"{CLI_SETTINGS} has no ApiKeys:GoogleAgentPlatform, and GEMINI_API_KEY is unset")

    return key


def check_preconditions(project: Path) -> None:
    """Everything that can be known before starting an agent, checked before spending a token."""
    if not project.is_dir():
        fail(f"no such project directory: {project}\n"
             f"       make one with:  polson create-project {project}")

    if not (project / "GEMINI.md").exists():
        fail(f"{project} has no GEMINI.md — is it a generated project?")

    if not CLI_DLL.exists():
        fail(f"the MCP server is not built: {CLI_DLL}\n"
             f"       build it with:  dotnet build")

    read_api_key()  # fails with guidance if there is no credential anywhere


async def trace(agent) -> None:
    """Print each step as it arrives.

    `chat()` + `await response.text()` waits for the whole turn, so a stall is indistinguishable
    from slowness — the first attempt showed nothing at all for minutes. Stepping shows where it
    actually stops.
    """
    # receive_steps() yields a step repeatedly as it accumulates, so a streaming answer arrives as
    # a dozen progressively longer TEXT_RESPONSEs. Printing each one buries the tool calls, so
    # partial text is suppressed and identical consecutive lines are collapsed with a count.
    last = None
    repeats = 0

    def flush() -> None:
        if last is not None:
            print(f"    {last}" + (f"  x{repeats}" if repeats > 1 else ""))

    async for step in agent.conversation.receive_steps():
        kind = getattr(getattr(step, "type", None), "name", "STEP")
        complete = getattr(step, "is_complete_response", False)

        if getattr(step, "tool_calls", None):
            names = ", ".join(getattr(call, "name", "?") for call in step.tool_calls)
            line = f"{kind:<16} -> {names}"
        elif kind == "TEXT_RESPONSE" and not complete:
            line = f"{kind:<16} (streaming)"
        elif complete:
            line = None
        else:
            line = kind

        if line is not None:
            if line == last:
                repeats += 1
            else:
                flush()
                last, repeats = line, 1

        if complete:
            flush()
            print(f"\n  agent: {step.content}")
            return

    flush()


def report(project: Path) -> None:
    """Read back the record the run left, which is the actual thing under test."""
    log = project / "events" / "server.jsonl"

    if not log.exists():
        print("\n  NO events/server.jsonl — the MCP server ran without --project-dir,")
        print("  or no tool call reached it. The run is unrecorded.")
        return

    events = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines() if line.strip()]
    kinds: dict[str, int] = {}
    for event in events:
        kinds[event["type"]] = kinds.get(event["type"], 0) + 1

    print(f"\n  events/server.jsonl — {len(events)} event(s)")
    for kind, count in sorted(kinds.items()):
        print(f"    {count:3d}  {kind}")

    for event in events:
        if event["type"] == "render":
            print(f"\n  rendered: {event['artifact']} ({event.get('bytes', '?')} bytes)")


async def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("project", type=Path, help="a directory made by 'polson create-project'")
    parser.add_argument("--public", action="store_true",
                        help="use the public Gemini endpoint instead of the Agent Platform one")
    parser.add_argument("--project-id", help="GCP project, for Agent Platform if your key needs one")
    parser.add_argument("--location", help="GCP location, for Agent Platform if your key needs one")
    parser.add_argument("--timeout", type=float, default=120.0,
                        help="seconds to wait for the turn before giving up (default 120)")
    args = parser.parse_args()

    project = args.project.resolve()
    check_preconditions(project)

    # Imported after the precondition checks so a missing install reports as itself rather than as
    # a confusing failure three steps later.
    try:
        from google.antigravity import Agent, LocalAgentConfig, types
        from google.antigravity.hooks import policy
    except ImportError as exc:
        fail(f"the Antigravity SDK is not installed ({exc}).\n"
             f"       see src/webapp/README.md — it is a deliberate manual step.")

    polson = types.McpStdioServer(
        name="polson",
        command="dotnet",
        args=[str(CLI_DLL), "server", "--project-dir", str(project)],
    )

    config = LocalAgentConfig(
        system_instructions=(
            "You are drawing inside the Polson studio. Use only the polson MCP tools. "
            "Save every render with outFile, relative to the project directory."
        ),
        mcp_servers=[polson],
        policies=[
            # Denied on every profile: it bypasses asset requisition entirely, so an agent holding
            # it can produce a finished picture and the premise of the studio stops being true.
            policy.deny("generate_image"),
            policy.deny("run_command"),

            # The policy engine is FAIL-CLOSED, and supplying `policies` at all replaces the
            # permissive default. Without this catch-all, a list of denies denies *everything* —
            # the first run of this probe had ExecuteScript, Search, list_directory and view_file
            # all refused, and the agent looped retrying until it timed out.
            #
            # Order does not matter: a specific deny is priority 1 and a global allow is priority 9,
            # so the two denies above still win over this.
            policy.allow_all(),
        ],
        capabilities=types.CapabilitiesConfig(
            # No human is attached to this probe, so the agent must finish on its own rather than
            # stopping to ask.
            agent_behavior=types.AgentBehavior.AUTONOMOUS,
        ),
        workspaces=[str(project)],
        save_dir=str(project / "session" / "save"),
        app_data_dir=str(project / "session" / "appdata"),

        # `vertex=True` selects the Agent Platform endpoint, which is the same choice the .NET side
        # makes with `new Client(enterprise: true, ...)`. Our key is an Agent Platform credential,
        # and against the default public endpoint it returns 403 "...are blocked". project and
        # location stay None unless given, matching the C# path, which passes neither.
        api_key=read_api_key(),
        vertex=not args.public,
        project=args.project_id,
        location=args.location,
    )

    prompt = (
        "Declare the stage 'Ideation' with Stage.begin, add a one-line Stage.note saying what you "
        "are testing, then run one ExecuteScript that draws a single filled circle on a 200x200 "
        "canvas and saves it with outFile 'artifacts/hello.webp'. Then stop."
    )

    print(f"  project  : {project}")
    print(f"  server   : {CLI_DLL.name} --project-dir")
    print(f"  endpoint : {'public Gemini API' if args.public else 'Agent Platform (vertex)'}")
    print(f"  prompt   : {prompt[:60]}...\n")

    async with Agent(config) as agent:
        await agent.conversation.send(prompt)

        try:
            await asyncio.wait_for(trace(agent), timeout=args.timeout)
        except asyncio.TimeoutError:
            print(f"\n  TIMED OUT after {args.timeout}s without the turn completing.")
            print("  The steps above are how far it got. No steps at all means the model was")
            print("  never reached; steps but no tool call means it chose not to use one.")

    report(project)


if __name__ == "__main__":
    asyncio.run(main())
