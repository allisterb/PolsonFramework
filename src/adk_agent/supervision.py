"""A supervising plugin: notices a role thrashing, and makes it ask for help.

**Why a plugin and not an agent.** The obvious design is that the Facilitator watches the Inker and
steps in. She cannot: ADK's `transfer_to_agent` moves control rather than forking it, so while the
Inker is working the Facilitator is not running at all. There is no agent in a position to observe.
A `BasePlugin` is — its callbacks fire for *every* agent in the app, outside any of them, which is
exactly the vantage point a supervisor needs.

**Why it compels an ask rather than seizing control.** Forcing `transfer_to_agent` back to the
Facilitator is available (`tool_context.actions.transfer_to_agent` is live in these callbacks, and
the flow gates on the action rather than on a function call). It is not what this does, for the same
reason the time budget warns rather than halts: cutting a role off mid-pass loses the pass. Instead
the plugin appends a directive to the tool result the agent is about to read, telling it to call
`ask_facilitator` before its next edit. The agent still decides how to act; it simply can no longer
fail to notice.

That is the `CLAUDE.md` §2 vertical channel in miniature — the environment changes what the agent is
told, in response to what the agent did — with the difference that this one is measured rather than
guessed, which is the failure mode that section warns about.

**What it can actually see.** A tool result from the MCP toolset is not a flat dict. It arrives as
`{"content": [{"type": "text", "text": "<json>"}], "isError": false}` — the payload is a JSON string
nested one level down. Verified by calling `ExecuteScript` directly against the real server rather
than by reading the wrapper; the fields are `success`, `imageFilePath`, `imageSize`, `executionId`,
`logs`, `executionTimeMs`.
"""

from __future__ import annotations

import hashlib
import json
import logging
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from google.adk.plugins.base_plugin import BasePlugin

_LOG = logging.getLogger("polson.watchdog")

#: Tool calls by one role, with no handoff, before it is told to ask for help. Eight rather than a
#: smaller number because a legitimate pass is several calls wide — write the script, run it, peek at
#: it, edit, re-run. The run this exists for made **twenty** script versions.
THRASH_CALLS = 8

#: Consecutive renders that do not move the picture before that counts as not resolving. Two could
#: be a deliberate re-run; three is a loop.
THRASH_REPEAT_RENDERS = 3

#: Similarity at or above which a render is judged not to have moved. Calibrated against real
#: renders through `CompareImages` at its default 256px, not chosen by feel:
#:
#:   re-run of the identical scene   1.0000
#:   one element moved 4px           0.9929
#:   one element moved 160px         0.8171
#:
#: So 0.999 separates "the encoder produced different bytes for the same picture" from even a very
#: small deliberate change. It is deliberately strict: the cost of missing a thrash is one wasted
#: iteration, and the cost of a false positive is interrupting work that was going somewhere.
UNCHANGED_SIMILARITY = 0.999

#: Fraction of a role's own time allowance past which any further editing trips the watchdog. Set to
#: the sharper of the two budget warnings, so this speaks only after the role has been told twice.
THRASH_ELAPSED_SHARE = 0.90

#: Interventions per role per invocation. A supervisor that fires every turn is a second runaway,
#: and the advice stops being read after the first time in any case.
MAX_INTERVENTIONS = 2

#: Tools whose result carries a render worth fingerprinting.
_RENDER_TOOLS = ("ExecuteScript", "RenderSvg")

#: Tools that mean the role is editing rather than looking. Counted toward `THRASH_CALLS`; `peek`
#: and `read_file` are not, because looking is the behaviour we want more of, not less.
_WORK_TOOLS = ("ExecuteScript", "RenderSvg", "write_script", "edit_script")


@dataclass
class _RoleActivity:
    """One role's behaviour within one invocation, reset whenever it hands off."""

    started: float
    calls: int = 0
    last_render: str | None = None
    last_digest: str | None = None
    #: How many consecutive renders have failed to move the picture.
    stalled_renders: int = 0
    interventions: int = 0
    armed: bool = True

    def reset(self, now: float) -> None:
        """Called on handoff. Interventions are *not* reset — they are a per-role budget."""
        self.started = now
        self.calls = 0
        self.last_render = None
        self.last_digest = None
        self.stalled_renders = 0
        self.armed = True


def _payload(result: Any) -> dict[str, Any] | None:
    """The MCP tool's real payload, dug out of `content[0].text`, or None if it is not there.

    Everything here is defensive on purpose: a watchdog that raises inside `after_tool_callback`
    breaks the tool call it was only supposed to observe, which is a far worse failure than not
    noticing a thrash.
    """
    if not isinstance(result, dict):
        return None
    content = result.get("content")
    if not isinstance(content, list) or not content:
        return None
    first = content[0]
    text = first.get("text") if isinstance(first, dict) else None
    if not isinstance(text, str):
        return None
    try:
        parsed = json.loads(text)
    except (ValueError, TypeError):
        return None
    return parsed if isinstance(parsed, dict) else None


def _digest(path: str | None) -> str | None:
    """A render's file hash, or None if it cannot be read.

    Only a **fast path**: identical bytes are certainly the same picture, so the comparison can be
    skipped for free. Differing bytes prove nothing — an encoder can produce them for an identical
    scene — which is why a mismatch falls through to `CompareImages` rather than being read as a
    change. Hashing alone was the first design and was wrong for exactly that reason.
    """
    if not path:
        return None
    try:
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()
    except OSError:
        return None


class StudioWatchdog(BasePlugin):
    """Watches every role in the app and makes a stuck one ask the Facilitator.

    `role_seconds` maps agent name to its time allowance, so the elapsed trigger can be expressed as
    a share of what that role was actually given. Omit it and only the two behavioural triggers are
    live — the plugin stays useful with no budget configured.
    """

    def __init__(
        self,
        role_seconds: dict[str, float] | None = None,
        toolset=None,
        advisor_tool: str | None = "ask_facilitator",
        name: str = "studio_watchdog",
    ):
        super().__init__(name=name)
        self._role_seconds = dict(role_seconds or {})
        self._toolset = toolset
        self._advisor = advisor_tool
        self._activity: dict[tuple[str, str], _RoleActivity] = {}
        self._compare = None

    async def _compare_tool(self):
        """The engine's own `CompareImages`, looked up once and kept.

        Calling it directly rather than through the flow means no plugin callbacks fire for it, so
        the watchdog cannot observe itself. Verified by calling a tool this way against the live
        server.
        """
        if self._compare is None and self._toolset is not None:
            try:
                tools = {t.name: t for t in await self._toolset.get_tools()}
                self._compare = tools.get("CompareImages", False)
            except Exception:  # pragma: no cover - a toolset that will not enumerate
                self._compare = False
        return self._compare or None

    async def _moved(self, previous: str, current: str, tool_context) -> bool | None:
        """Whether the picture changed between two renders. None when it cannot be told.

        None is not False. A comparison that could not run — a missing file, a server that declined
        — must not be read as "nothing changed", or an unrelated failure would start manufacturing
        interventions.
        """
        compare = await self._compare_tool()
        if compare is None:
            return None
        try:
            raw = await compare.run_async(
                args={"pathA": previous, "pathB": current}, tool_context=tool_context
            )
        except Exception as exc:  # pragma: no cover - network/process failure
            _LOG.debug("watchdog comparison failed: %s", exc)
            return None

        payload = _payload(raw)
        if not payload:
            return None
        # Different canvas sizes are reported as not comparable, and a size change is a change.
        if not payload.get("comparable", True):
            return True
        similarity = payload.get("similarity")
        if not isinstance(similarity, (int, float)):
            return None
        return similarity < UNCHANGED_SIMILARITY

    # ------------------------------------------------------------------ observation

    async def after_tool_callback(self, *, tool, tool_args, tool_context, result):
        """Records what the role just did, and intervenes if it has stopped making progress.

        Returns a **replacement** result when it intervenes — ADK's contract for this callback — with
        the directive appended as an extra text part. Returning None leaves the result untouched,
        which is the case on nearly every call.
        """
        now = time.monotonic()
        agent = getattr(tool_context, "agent_name", None) or "?"
        invocation = getattr(tool_context, "invocation_id", None) or "?"
        key = (invocation, agent)
        activity = self._activity.get(key)
        if activity is None:
            activity = self._activity.setdefault(key, _RoleActivity(started=now))

        # A handoff means the role is done and its slate is clean. Checked before anything else so a
        # transfer never counts toward the thrash that a transfer resolves.
        if tool.name == "transfer_to_agent":
            activity.reset(now)
            return None

        # Asking for help is what we wanted; stop nagging until something new happens.
        if tool.name == self._advisor:
            activity.calls = 0
            activity.stalled_renders = 0
            activity.armed = True
            return None

        if tool.name in _WORK_TOOLS:
            activity.calls += 1

        if tool.name in _RENDER_TOOLS:
            await self._record_render(activity, _payload(result), tool_context)

        reason = self._trigger(activity, now, agent)
        if reason is None:
            return None

        # Fires once per episode: re-arming happens on a handoff or once help is asked for, so a
        # role that ignores the directive is not told again on every subsequent call.
        activity.armed = False
        activity.interventions += 1
        _LOG.warning(
            "watchdog %s/%s intervention %d/%d: %s",
            agent, invocation, activity.interventions, MAX_INTERVENTIONS, reason,
        )
        return self._with_directive(result, reason)

    async def _record_render(self, activity, payload, tool_context) -> None:
        """Notes a new render and whether it moved the picture on from the last one."""
        path = payload.get("imageFilePath") if payload else None
        if not path:
            return

        digest = _digest(path)
        previous, previous_digest = activity.last_render, activity.last_digest
        activity.last_render, activity.last_digest = path, digest

        if not previous:
            return

        if digest and previous_digest and digest == previous_digest:
            moved = False  # identical bytes are certainly the same picture; no need to ask
        else:
            moved = await self._moved(previous, path, tool_context)

        if moved is None:
            return  # could not tell; leave the count where it is rather than guessing either way
        activity.stalled_renders = 0 if moved else activity.stalled_renders + 1

    # ------------------------------------------------------------------ judgement

    def _trigger(self, activity: _RoleActivity, now: float, agent: str) -> str | None:
        """Why this role should stop and ask, or None. The first matching reason wins."""
        if not activity.armed or activity.interventions >= MAX_INTERVENTIONS:
            return None

        if activity.stalled_renders >= THRASH_REPEAT_RENDERS:
            return (
                f"your last {activity.stalled_renders + 1} renders are the same picture — the "
                f"edits between them did not change what it looks like"
            )

        allowance = self._role_seconds.get(agent)
        if allowance and (now - activity.started) >= allowance * THRASH_ELAPSED_SHARE:
            return (
                f"you are {(now - activity.started) / 60:.0f} minutes into a "
                f"{allowance / 60:.0f} minute allowance and still editing"
            )

        if activity.calls >= THRASH_CALLS:
            return f"you have made {activity.calls} working calls without handing off"

        return None

    # ------------------------------------------------------------------ intervention

    def _with_directive(self, result: Any, reason: str) -> Any:
        """The tool result the role asked for, with the runtime's directive appended.

        Appended rather than substituted: the role still needs the render path and the logs it
        called for. Taking those away to deliver advice would strand it.

        **Two result shapes, and getting this wrong made the whole mechanism inert.** An MCP tool
        returns `{"content": [{"type": "text", ...}], "isError": bool}`; a `FunctionTool` — which is
        what `write_script`, `edit_script`, `peek` and `read_file` are — returns a plain dict of its
        own fields. The first version only knew the MCP shape, so it logged *"could not attach a
        directive to a dict result"* and delivered nothing. Both live runs tripped exactly that:
        the trigger fired on a `write_script` result, the advice was dropped, and `ask_facilitator`
        was consequently never called by anybody.
        """
        advice = (
            f"Call `{self._advisor}` with what you are trying to achieve, what you have tried, and "
            f"the path of your latest render. You will get a direction back from someone who has "
            f"looked at it with fresh eyes. Do not make another edit first."
            if self._advisor
            # No advisor to call — a single-agent run has nobody to ask, and naming a tool that is
            # not on the agent's list would be worse than saying nothing.
            else "Stop, and say in a `Stage.note` what you are stuck on and what you will do "
                 "differently. Repeating the last edit is not it."
        )
        text = f"[studio runtime] Stop before your next edit — {reason}. {advice}"

        if not isinstance(result, dict):
            _LOG.warning("watchdog could not attach a directive to a %s result", type(result).__name__)
            return None

        amended = dict(result)
        content = amended.get("content")
        if isinstance(content, list):
            amended["content"] = [*content, {"type": "text", "text": text}]
        else:
            # A FunctionTool's own dict. The model reads the whole thing, so a field is enough — and
            # a distinctive key is easier to find in a transcript than prose merged into another.
            amended["studio_directive"] = text
        return amended
