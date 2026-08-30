"""The Polson orchestrator: runs a design project's agent and writes down what happened.

Milestone 5. The web app (Milestone 6) is a UI over this, not a replacement for it — which is why
the director is a hook that can be swapped, and why everything worth keeping is written to
`events/` rather than held in memory.

`RunStream` is where a run becomes watchable: it carries both halves of the record — the files this
process writes, and the one the .NET MCP server owns — onto a single broker, and it is what an HTTP
handler will attach to. It adds nothing to the record and replaces none of it.

    from orchestrator import project, run
    p = project.load('projects/acme')
    result = await run.run_turn(p, 'Read GEMINI.md and begin.')

See `docs/project-layout.md` for the directory contract this implements.
"""

from .broker import Broker, Subscription
from .csm import Coded, Curve
from .director import Reply, WebDirector
from .events import EventLog, merge, read_events
from .project import Project, ProjectError, load
from .run import RunResult, build_config, run_turn
from .tail import Tailer
from .transcript import Transcript
from .watch import RunStream

__all__ = [
    "Broker",
    "Coded",
    "Curve",
    "EventLog",
    "Project",
    "ProjectError",
    "Reply",
    "RunResult",
    "RunStream",
    "Subscription",
    "Tailer",
    "Transcript",
    "WebDirector",
    "build_config",
    "load",
    "merge",
    "read_events",
    "run_turn",
]
