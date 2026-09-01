"""Reading a run as interaction over time: the creative sense-making coder.

`docs/creative-sense-making.md` is the specification; this implements it. In short: every recorded
action is coded by its functional role in sense-making, and the codes accumulate into a curve that
rises when the agent is regulating its interaction, falls when it is producing, and holds flat when
it is waiting.

**The spine is `server.jsonl`.** The Polson MCP server writes it under every host — the Antigravity
desktop, Claude Code, this orchestrator — because the server is ours regardless of who runs the
agent. It carries the whole *execute* column and, since the probe instrumentation, the whole
*inspect* column, so the medium-specific actions need no host adapter at all.

**Everything else is enrichment**, and it is host-specific because the transcripts differ in kind
rather than in field names. Only the standalone one is implemented here; the others are adapters
waiting to be written, and a curve built without one says so rather than quietly reporting a run with
no deliberation in it.

Nothing here draws. The coder produces numbers and the web app decides what to do with them, which is
the same division `Layout` and `Scale` keep on the drawing side.
"""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

from .events import read_events
from .project import Project

#: Coded value per mode, from Davis & Rafner (2025) Table 2. Positive is unclamped — the agent
#: regulating how it interacts — and negative is clamped production. The 2017 dissertation uses a
#: different scale; see the doc.
VALUES: dict[str, float] = {
    "communicate": 1.0,
    "gather": 1.0,
    "inspect": 0.5,
    "wait": 0.0,

    # An attempt to produce that the environment refused. Zero, like waiting, because the artifact
    # did not change and a curve that fell here would say a broken engine had produced something.
    # It is a mode of its own rather than folded into `wait` so that a run of them is legible: a
    # failed run is otherwise indistinguishable from a healthy one that has not started drawing.
    "attempt": 0.0,

    "execute": -1.0,
}

#: Server events that need no interpretation beyond their type.
SPINE_MODES: dict[str, str] = {
    "note": "communicate",
    "stage.begin": "communicate",
    "stage.end": "communicate",
    "stage.continue": "communicate",

    # Stating what a render should show, and settling it afterwards. Communication in the CCSM sense:
    # the agent regulating how it is working rather than producing, exactly as a note is.
    "expect": "communicate",
    "check": "communicate",

    "inspect": "inspect",
    "artifact.read": "inspect",

    # Acquiring a resource from outside the drawing, which is what `gather` names. A requisition
    # script renders nothing and succeeds, so until this was coded it contributed nothing to the
    # curve at all — a run could spend half its budget and leave a flat line.
    "asset.requisition": "gather",

    # Refused before the network: an attempt that produced nothing, coded as `script.error` is. The
    # remedy is to reword, and a run of these is a run stuck, which should be visible as one.
    "asset.refused": "attempt",

    # `observe` is deliberately absent. One is written per measurement *within* an execution that
    # already emits a single `inspect`, so coding both would count one looking-episode two to thirty
    # times and drag the curve toward unclamped in proportion to how thorough the checking was —
    # penalising the behaviour the record exists to encourage. The outcomes are for the report and the
    # reader; the tally is what the curve is built on.
    #
    # `budget` is absent for a different reason: it is a state snapshot rather than anything the agent
    # did, and it is written alongside the requisitions that are already coded.
}

#: Orchestrator transcript events, for the enrichment pass.
AGENT_MODES: dict[str, str] = {
    "thinking": "wait",
}

#: Host tools, by what the agent was doing with them. Anything unlisted is left uncoded rather than
#: guessed at — a wrong code is worse than a gap, because a gap is visible.
TOOL_MODES: dict[str, str] = {
    "view_file": "inspect",
    "read_file": "inspect",
    "list_directory": "inspect",
    "grep_search": "inspect",
    "find_by_name": "inspect",
    "codebase_search": "inspect",
    "Search": "gather",
    "History": "inspect",
}

#: MCP tools whose effect the spine already records. Coding them again from the transcript would
#: double every execution — the one mistake that makes an enriched curve worse than an unenriched one.
SPINE_OWNED = frozenset({"ExecuteScript", "ExecuteSvgScript", "RenderSvg", "MeasureSvgPath"})


@dataclass(frozen=True)
class Coded:
    """One action, coded by its functional role in sense-making."""

    ts: str
    seq: int
    src: str
    mode: str
    value: float
    kind: str
    detail: str = ""
    stage: str | None = None
    execution: str | None = None
    agent: str = "agent"

    #: Probe count for an inspect event, so the curve can show magnitude without weighting by it.
    weight: int = 1

    #: How long this state held before the next action, in milliseconds. The last point holds for 0.
    hold_ms: int = 0

    @property
    def epoch_ms(self) -> int:
        return _epoch_ms(self.ts)


@dataclass
class Curve:
    """A coded run: the points, the cumulative trace, and what can be read off it."""

    points: list[Coded] = field(default_factory=list)
    enriched: bool = False

    #: Columns the source data could not supply, named so a reader is never misled by their absence.
    missing: tuple[str, ...] = ()

    # region Properties
    @property
    def trace(self) -> list[dict[str, Any]]:
        """The cumulative curve, in two readings, because they answer different questions.

        `cumulative` counts **actions**: one step per coded action, whatever it cost. It shows the
        rhythm of a session — the saw-tooth of declare, produce, declare, produce — and it is what
        makes turning points legible.

        `integral` counts **time**: each state's value multiplied by how long it held, in seconds. It
        shows where the session actually went.

        They disagree, and the disagreement is the point. Notes arrive in bursts and cost nothing;
        a drawing script is one action that occupies a minute. Counting actions over-weights
        communication, and counting time over-weights whatever the agent was doing when the model was
        slow. Davis's 250 ms sampling of a human coder's slider is a time integral, so `integral` is
        the closer analogue — but it attributes model latency to whichever state preceded it, which
        is a real distortion in an agentic setting and has no counterpart in his.
        """
        total, integral, start, trace = 0.0, 0.0, None, []
        for point in self.points:
            at = point.epoch_ms
            if start is None:
                start = at
            total += point.value
            integral += point.value * (point.hold_ms / 1000.0)
            trace.append({
                "ts": point.ts,
                "t": max(0, at - start),
                "value": point.value,
                "holdMs": point.hold_ms,
                "cumulative": round(total, 3),
                "integral": round(integral, 3),
                "mode": point.mode,
                "kind": point.kind,
                "detail": point.detail,
                "stage": point.stage,
                "execution": point.execution,
                "agent": point.agent,
                "weight": point.weight,
            })
        return trace

    @property
    def counts(self) -> dict[str, int]:
        counts: dict[str, int] = {}
        for point in self.points:
            counts[point.mode] = counts.get(point.mode, 0) + 1
        return counts

    @property
    def duration_ms(self) -> int:
        if len(self.points) < 2:
            return 0
        return max(0, self.points[-1].epoch_ms - self.points[0].epoch_ms)

    @property
    def agents(self) -> list[str]:
        """Who contributed, in first-appearance order. One curve each is the multi-agent view."""
        seen: list[str] = []
        for point in self.points:
            if point.agent not in seen:
                seen.append(point.agent)
        return seen
    # endregion

    # region Methods
    def summary(self) -> dict[str, Any]:
        """The readings worth surfacing, and an honest note about what the source could not supply."""
        counts = self.counts
        regulating = sum(counts.get(m, 0) for m in ("communicate", "gather", "inspect"))
        executing = counts.get("execute", 0)
        net = round(sum(p.value for p in self.points), 3)

        return {
            "actions": len(self.points),
            "durationMs": self.duration_ms,
            "counts": counts,

            # The coding table itself, so a viewer can show what the colours mean without a second
            # copy of the numbers to keep in step. A legend that disagrees with the curve it explains
            # is worse than none.
            "scale": dict(VALUES),
            "regulating": regulating,
            "executing": executing,

            # Neither regulating nor producing: tried to draw and drew nothing. A count worth its own
            # line, because a run where this is most of the drawing is a broken environment rather
            # than a deliberative one, and the curve alone cannot say so — every attempt is flat.
            "refused": counts.get("attempt", 0),
            # Net displacement over actions: positive means the run spent more of itself making sense
            # of the work than producing it. Neither sign is good or bad on its own — a run that only
            # produces never checked anything, and one that only regulates never made a mark.
            "net": net,
            "slope": round(net / len(self.points), 3) if self.points else 0.0,

            # The same run measured by time rather than by action count. A large gap between the two
            # signs means the session's frequent actions and its long ones pulled in opposite
            # directions, which is worth seeing rather than averaging away.
            "integral": round(sum(p.value * p.hold_ms / 1000.0 for p in self.points), 3),
            "heldMs": {
                mode: sum(p.hold_ms for p in self.points if p.mode == mode)
                for mode in sorted({p.mode for p in self.points})
            },
            "probes": sum(p.weight for p in self.points if p.mode == "inspect"),
            "enriched": self.enriched,
            "missing": list(self.missing),
        }

    def per_agent(self) -> dict[str, "Curve"]:
        """One curve per contributor, which is where participatory sense-making becomes visible."""
        split: dict[str, Curve] = {}
        for point in self.points:
            curve = split.setdefault(
                point.agent, Curve(enriched=self.enriched, missing=self.missing))
            curve.points.append(point)
        return split
    # endregion


def code(events: Iterable[dict[str, Any]], *, enriched: bool = False) -> Curve:
    """Codes an ordered event stream. Events it has no rule for are skipped, never guessed at."""
    events = list(events)

    # A script that drew nothing is not an execution. Probe scripts — measure the fonts, check a
    # palette, exit — are entirely unclamped work, and counting them as production would flatten the
    # distinction the curve exists to show.
    rendered = {e.get("execution") for e in events if e.get("type") == "render" and e.get("execution")}

    points = [c for e in events if (c := _code_one(e, rendered)) is not None]
    points.sort(key=lambda c: (c.epoch_ms, c.src, c.seq))
    points = _with_holds(points)

    missing = () if enriched else ("wait",)
    return Curve(points=points, enriched=enriched, missing=missing)


def _with_holds(points: list[Coded]) -> list[Coded]:
    """Gives each point the span until the next, which is how long its state was in force.

    An unreadable timestamp codes as epoch 0 and would otherwise produce an absurd hold, so a
    negative span is clamped rather than trusted.
    """
    held = []
    for i, point in enumerate(points):
        span = points[i + 1].epoch_ms - point.epoch_ms if i + 1 < len(points) else 0
        held.append(replace(point, hold_ms=max(0, span)))
    return held


def read(project: Project, *, enrich: bool = True, since: str | None = None) -> Curve:
    """Codes one project's run: the spine, plus the standalone transcript when it is there.

    `enrich=False` codes the spine alone, which is what a managed run gets until its host has an
    adapter. The result reports the difference rather than hiding it.

    `since` scopes the reading to one run, and a caller showing a single run wants it. **All three
    event files are appended to across runs**, so without it a second run is coded together with
    every run before it, and what comes back is the project's whole history presented as this run's
    curve — the earlier work dominating the plot while today's points arrive too small to see.

    It is a timestamp in the record's own format (`events.timestamp()`), compared as a string. That
    is sound because the Python and .NET writers emit the identical millisecond format, which is
    also what `events.merge` already sorts on.
    """
    events = list(read_events(project.server_events))
    events += list(read_events(project.director_events))

    agent_events = list(read_events(project.agent_events)) if enrich else []
    events += agent_events

    if since:
        events = [e for e in events if (e.get("ts") or "") >= since]

        # Filtered too, not just counted: a run whose transcript all predates the cut is not an
        # enriched run, and reporting it as one would claim a medium-specific reading we do not have.
        agent_events = [e for e in agent_events if (e.get("ts") or "") >= since]

    return code(events, enriched=bool(agent_events))


# region Coding (private)
def _code_one(event: dict[str, Any], rendered: set[Any]) -> Coded | None:
    kind = event.get("type") or ""
    src = event.get("src") or "?"

    if src == "server":
        return _code_server(event, kind, rendered)
    if src == "agent":
        return _code_agent(event, kind)
    if src == "director" and kind in ("question", "answer", "message"):
        # The human's own turns. `message` is the director speaking, which under the enactive account
        # of the novice is a contribution rather than an instruction: a disruption that opens
        # affordances the agent would not have found.
        return _make(event, "communicate", kind, _clip(event.get("text") or ""), agent="director")

    return None


def _code_server(event: dict[str, Any], kind: str, rendered: set[Any]) -> Coded | None:
    if kind in ("script.ok", "script.error"):
        detail = event.get("script") or ""

        if event.get("execution") not in rendered:
            # Nothing was drawn. Which of two very different things that means depends on whether the
            # script was trying to: one that succeeded is a probe — measure the fonts, check a
            # colour, exit — and is regulation already spoken for by its own `inspect` event, so
            # coding it again would double it. One that *failed* drew nothing because it could not,
            # and until this was coded it vanished from the record entirely.
            #
            # A Linux run made the cost plain: nine scripts, four refused by a missing native
            # library, and a curve that rose smoothly throughout because the only negative value in
            # the table needs a render to be assigned. The run read as a long thoughtful regulation
            # phase. What it was, was blocked.
            if kind != "script.error":
                return None
            return _make(event, "attempt", kind, f"{detail} — failed, nothing drawn")

        if kind == "script.error":
            # Production that did not land. In the free-energy account this is the surprise term, and
            # it is what the next unclamp is a response to.
            detail = f"{detail} — failed"
        return _make(event, "execute", kind, detail)

    if (mode := SPINE_MODES.get(kind)) is None:
        return None

    if kind == "inspect":
        # One point, whatever the tally: a getPixel loop runs thousands of times, and weighting by
        # count would let one loop swamp a session. The count travels as magnitude instead.
        return _make(event, mode, kind, _probe_detail(event), weight=int(event.get("total") or 1))

    if kind == "artifact.read":
        return _make(event, mode, kind, event.get("artifact") or "")

    return _make(event, mode, kind, _clip(event.get("message") or event.get("stage") or ""))


def _code_agent(event: dict[str, Any], kind: str) -> Coded | None:
    agent = _agent_of(event)

    if (mode := AGENT_MODES.get(kind)) is not None:
        return _make(event, mode, kind, _clip(event.get("text") or ""), agent=agent)

    if kind != "tool.call":
        return None

    tool = event.get("tool") or ""
    if tool in SPINE_OWNED:
        # Already on the spine. Coding it again would double every execution, which is the one way an
        # enriched curve can be worse than an unenriched one.
        return None

    if (mode := TOOL_MODES.get(tool)) is None:
        return None

    return _make(event, mode, kind, tool, agent=agent)


def _agent_of(event: dict[str, Any]) -> str:
    """Who acted. A subagent's steps carry depth and trajectory; the main agent's carry neither."""
    if not event.get("depth"):
        return "agent"
    trajectory = event.get("trajectory") or ""
    return f"agent:{trajectory[:8]}" if trajectory else f"agent:depth{event['depth']}"


def _make(event: dict[str, Any], mode: str, kind: str, detail: str, *,
          agent: str = "agent", weight: int = 1) -> Coded:
    return Coded(
        ts=event.get("ts") or "",
        seq=int(event.get("seq") or 0),
        src=event.get("src") or "?",
        mode=mode,
        value=VALUES[mode],
        kind=kind,
        detail=detail,
        stage=event.get("stage"),
        execution=event.get("execution"),
        agent=agent,
        weight=max(1, weight),
    )


def _probe_detail(event: dict[str, Any]) -> str:
    probes = event.get("probes")
    if not isinstance(probes, dict) or not probes:
        return ""
    return ", ".join(f"{k} {v}" for k, v in probes.items())


def _clip(text: str, limit: int = 120) -> str:
    text = " ".join((text or "").split())
    return text if len(text) <= limit else text[:limit - 1] + "…"


def _epoch_ms(ts: str) -> int:
    """Milliseconds since the epoch, or 0 for an unreadable timestamp.

    Both writers produce `2026-08-30T20:00:00.000Z`, which `fromisoformat` accepts only once the `Z`
    is spelled as an offset.
    """
    if not ts:
        return 0
    try:
        parsed = datetime.fromisoformat(ts.replace("Z", "+00:00"))
    except ValueError:
        return 0
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return int(parsed.timestamp() * 1000)
# endregion
