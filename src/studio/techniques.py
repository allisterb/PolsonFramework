"""The design techniques a request can ask for or rule out: the composer's "Graphic design" field.

**One catalogue, the CLI's.** `create-project --request/--deny` checks every id against
`ProjectTemplate/_shared/techniques.json`, and `polson techniques --json` prints it; the form reads that, once, rather
than a second copy here that could drift — the workflow table in `projects.py` has drifted twice already. A CLI that
cannot be run leaves the field out of the form, and the request is made without it.

**Three states, not two checkboxes.** Each technique is *the agent decides* (the default), *use* or *don't use*: one
radio group, so the two choices are exclusive by construction and the default is a state the visitor can see and return
to. A request means the agent uses it in the delivered piece, or in a concept and says why that concept lost; a denial
overrides the workflow's own defaults (the generator's `Techniques.Section` words both).

**What a visitor controls.** Only ids from this catalogue, for the request's workflow, reach the command line: the form's
values are matched against it here before anything is spawned, as `projects.check` matches the workflow and the type.
"""

from __future__ import annotations

import json
import logging
import subprocess
from dataclasses import dataclass
from typing import Any

from . import projects
from .runs import StudioError

_logger = logging.getLogger(__name__)

#: The form field's name for one technique: `tech_<id>`, its value '' (the agent decides), 'request' or 'deny'.
PREFIX = "tech_"
STATES = ("", "request", "deny")


@dataclass(frozen=True)
class Technique:
    id: str
    name: str
    workflows: tuple[str, ...]
    source: str
    summary: str
    #: Whether the run report can see it used; a judged one is reported as judged, never as used.
    measured: bool = True


def catalogue() -> tuple[Technique, ...]:
    """The CLI's catalogue, read once it reads. Empty when the CLI cannot say, which hides the field rather than failing
    the form; a failure is not kept, so a CLI being rebuilt does not hide the field until the server restarts."""
    global _catalogue
    if _catalogue is not None:
        return _catalogue
    try:
        done = subprocess.run([*projects.launcher(), "techniques", "--json"], capture_output=True, text=True,
                              encoding="utf-8", errors="replace", timeout=30)
        if done.returncode != 0:
            raise RuntimeError(done.stderr.strip() or f"exit {done.returncode}")
        rows = json.loads(done.stdout)
        _catalogue = tuple(Technique(r["id"], r["name"], tuple(r["workflows"]), r["source"], r["summary"],
                                     bool(r.get("measured", True))) for r in rows)
        return _catalogue
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as exc:
        _logger.warning("techniques: the CLI's catalogue could not be read, so the form offers none: %s", exc)
        return ()


def reset() -> None:
    """Forgets the catalogue, so the next call reads the CLI again: for tests, and after a rebuild."""
    global _catalogue
    _catalogue = None


#: The catalogue once read; None until then.
_catalogue: tuple[Technique, ...] | None = None


def for_workflow(workflow: str) -> tuple[Technique, ...]:
    """The techniques that apply to `workflow`, in the catalogue's order; a `_qloo` twin takes its base workflow's."""
    workflow = projects.base_workflow(workflow)
    return tuple(t for t in catalogue() if workflow in t.workflows)


def states(form: Any) -> dict[str, str]:
    """{id: state} from a posted form's `tech_*` fields, unchecked: kept to show the form again as it was sent."""
    return {str(k)[len(PREFIX):]: str(v) for k, v in form.items() if str(k).startswith(PREFIX)}


def parse(workflow: str, chosen: dict[str, str]) -> tuple[list[str], list[str]]:
    """(requested, denied) ids from {id: state}, checked against `workflow`'s techniques. Raises `StudioError`."""
    offered = {t.id: t for t in for_workflow(workflow)}
    request, deny = [], []
    for tid, value in chosen.items():
        state = (value or "").strip()
        if state not in STATES:
            raise StudioError("A design technique is left to the studio, used, or not used.")
        if not state:
            continue
        if tid not in offered:
            raise StudioError("That design technique is not one this workflow offers.")
        (request if state == "request" else deny).append(tid)
    order = list(offered)
    return sorted(request, key=order.index), sorted(deny, key=order.index)


def names(ids: list[str]) -> list[str]:
    """The techniques' names, for the request page; an id the catalogue no longer has is shown as it is."""
    known = {t.id: t.name for t in catalogue()}
    return [known.get(i, i) for i in ids]
