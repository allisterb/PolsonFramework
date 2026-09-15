"""Serves the studio over a directory of projects. Run from `src/`:

    python -m studio ../projects
    python -m studio ../tests/agent/infographic/agy --port 8080

The argument holds project *directories*, one per run — the same layout `create-project` writes
into. Nothing starts an agent until someone presses a button.

**This is the managed-mode window, and it is the reason this file exists again.** The studio's UI is
mounted by `adk_agent/main.py` when the ADK runtime hosts the agent itself; for a director driving
from Antigravity or Claude Code on their own subscription, nothing was launching it, so the live
trace, the sense-making curve and the render-beside-script view were unreachable and `polson report`
was the only way to read a run. The entry point was lost with `src/webapp`, not the capability.

A module rather than a loose script, to match `python -m orchestrator <project>` — which runs one
turn against one project with you as the director, and is the other half of this.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(prog="python -m studio", description="Serve the Polson studio.")
    parser.add_argument("root", help="directory holding the project directories")
    parser.add_argument("--host", default="127.0.0.1",
                        help="bind address (default: localhost only - see the warning below)")
    parser.add_argument("--port", type=int, default=8000)
    parser.add_argument("--observe-only", action="store_true",
                        help="read runs without offering to start one. What a director driving from "
                             "their own agent host wants: the record is already being written, and "
                             "this only puts a reader on it.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.is_dir():
        print(f"error: no such directory: {root}", file=sys.stderr)
        return 1

    # Imported here rather than at module scope so that `--help` and a bad path both answer without
    # paying uvicorn's import, and so a missing dependency names itself against a real command.
    import uvicorn

    from studio import create_app

    print(f"  projects in {root}")
    print(f"  http://{args.host}:{args.port}{'  (observing only)' if args.observe_only else ''}\n")

    if args.host != "127.0.0.1":
        # The same cost surface `intake.py` guards with its daily cap: a public URL drives a metered
        # model, and visitor-typed text reaches an agent holding tools.
        print("  WARNING: bound beyond localhost. A reachable studio is an open cost surface,\n"
              "  and anything typed into it reaches an agent that holds tools.\n", file=sys.stderr)

    uvicorn.run(create_app(root, observe_only=args.observe_only),
                host=args.host, port=args.port, log_level="warning")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
