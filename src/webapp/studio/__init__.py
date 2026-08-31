"""The Polson studio's web layer — Milestone 6.

A UI over `orchestrator`, not a replacement for it. `run_turn` still owns the agent, the tool policy
and the record; this owns the part a browser needs.

    python src/webapp/serve_studio.py <projects-dir>

See `docs/project-layout.md` for the directory contract and `docs/creative-sense-making.md` for what
the coded run means.
"""

from .app import create_app
from .runs import Registry, Run, StudioError

__all__ = ["Registry", "Run", "StudioError", "create_app"]
