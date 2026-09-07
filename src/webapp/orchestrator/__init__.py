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
from .events import EventLog, merge, read_events
from .project import Project, ProjectError, load
from .tail import Tailer
from .transcript import Transcript
from .watch import RunStream

#: Names that need the Antigravity SDK, resolved on first use rather than on import.
#:
#: **Importing this package must not require the SDK.** Two thirds of what is here reads a record
#: someone else wrote — the tailer, the broker, the CSM coding, the observe page — and the ADK
#: runtime serves exactly that while deliberately not shipping `google-antigravity`
#: (`src/adk_agent/requirements.in` gives the reason). Re-exporting the driver eagerly made
#: `from orchestrator import anything` fail there, which is a packaging accident rather than a
#: decision anyone took.
#:
#: PEP 562 module `__getattr__`, so `orchestrator.run_turn` still resolves for a caller that has the
#: SDK and still raises `ModuleNotFoundError` for one that does not — at the point of use, naming
#: what is missing, instead of at import naming nothing.
_DRIVER = {
    "Reply": ".director",
    "WebDirector": ".director",
    "RunResult": ".run",
    "build_config": ".run",
    "run_turn": ".run",
}


def __getattr__(name: str):
    if (module := _DRIVER.get(name)) is None:
        raise AttributeError(f"module {__name__!r} has no attribute {name!r}")

    from importlib import import_module

    return getattr(import_module(module, __name__), name)


def __dir__() -> list[str]:
    return sorted([*globals(), *_DRIVER])

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
