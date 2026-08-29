"""Runs the studio against a project directory, from the repository root.

    python src/webapp/run_studio.py path/to/project

The same program as `python -m orchestrator`, reachable without changing directory first. Python
puts this file's directory on `sys.path`, so the package next to it imports with no path juggling.
"""

from __future__ import annotations

import asyncio

from orchestrator.__main__ import main

if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
