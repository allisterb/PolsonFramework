"""The Polson orchestrator: runs a design project's agent and writes down what happened.

Milestone 5. The web app (Milestone 6) is a UI over this, not a replacement for it — which is why
the director is a hook that can be swapped, and why everything worth keeping is written to
`events/` rather than held in memory.

    from orchestrator import project, run
    p = project.load('projects/acme')
    result = await run.run_turn(p, 'Read GEMINI.md and begin.')

See `docs/project-layout.md` for the directory contract this implements.
"""

from .events import EventLog, merge, read_events
from .project import Project, ProjectError, load
from .run import RunResult, build_config, run_turn
from .transcript import Transcript

__all__ = [
    "EventLog",
    "Project",
    "ProjectError",
    "RunResult",
    "Transcript",
    "build_config",
    "load",
    "merge",
    "read_events",
    "run_turn",
]
