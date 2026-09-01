"""The host's own conversation, transcribed into the record.

`transcript.py` does this for a run *we* drive: the Antigravity SDK hands us typed steps and we write
them to `agent.jsonl` as they happen. This does it for a run we do not drive at all — one where the
agent is being worked by Claude Code or Claude Desktop, and the only interface is the host's.

That case is the ordinary one, and until now it left half a record. The MCP server writes
`server.jsonl` whoever is driving it, so the *spine* — scripts, renders, stages, expectations,
measurements — is already there. What was missing is the conversation that produced it: the
director's asks and the agent's reasoning, which under CSM is not decoration but the other
participant in the trajectory. A curve coded from the spine alone is one agent talking to itself.

**Nothing new has to be captured.** `polson create-project --sdk claude` already installs the
`preserve-chatlog` hook on `Stop` and `SessionEnd`, so the whole transcript is copied into
`events/chat-<session>.jsonl` after every turn. This reads that file. The dashboard is a window onto
the record, not a second way into the agent — direction happens in the host's own interface, as it
should, and reaches here as data rather than as a channel.

Four things this has to get right:

- **Idempotence.** The hook overwrites the copy every turn, so this reads the same entries many
  times. Identity is the transcript's own `uuid`, and the resume point is read back out of what was
  already written rather than kept in a state file beside it — the record describes its own position,
  the same way `(src, seq)` rather than a byte offset identifies a line for the tailer.
- **The original timestamps.** These events happened before they were read. Stamping them with the
  transcription time would put a whole session at one instant, destroying the interleaving with
  `server.jsonl` and every `since` cut that scopes a page to one run.
- **Tool names as the host wrote them.** A Polson call arrives as `mcp__polson__ExecuteScript`.
  Normalising it here would be tidier and wrong: the record's job is to say what happened. `csm`
  strips the prefix when it codes, which is where interpretation belongs.
- **One writer per file, still.** This owns `agent.jsonl` and `director.jsonl` for a host-driven
  project exactly as the orchestrator owns them for one it drives. The two never write the same
  project, because each means the opposite thing about where the agent is running.

**A thinking block may or may not carry its text, and both cases have to work.** The shape is always
`{"type": "thinking", "thinking": ..., "signature": ...}`, but whether `thinking` holds the reasoning
or an empty string is not ours to control and has changed over time. Measured across 409 transcripts
on one machine: mostly present through late July and August 2026 (63–100% of blocks per day), and
absent from 2026-08-21 onward — 9 of ~3,300 blocks since. The client carries server-side feature
flags (`tengu_thinking_display_updates`, `tengu_thinking_block_resumption`) that track the change, so
it is a property of the host at the time, not of the session or the model. Signatures are unique per
block, so an empty one's text is not recoverable from some other entry: it was never written.

Either way the block is emitted — with `text` when there is text, marked `redacted` when there is
not. Skipping the empty ones was the first implementation and it is wrong, because `csm` codes
`thinking` as `wait` and nothing else does: a run whose host was not storing reasoning would lose
every pause and read as an agent that never stopped to think. A systematic distortion is worse than
an acknowledged gap, and the two are told apart by a field.

Deliberately **not** emitted: `turn.start` / `turn.end`. A turn's end can only be inferred from the
arrival of the next one, so a snapshot of a live conversation would have to either claim the current
turn had ended or hold back the turn before it. Neither is worth it — `csm` codes neither, and
`RunReport` excludes both from its step counts. The gap is a decision, not an oversight.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Iterator

from .events import EventLog, read_events
from .project import Project
from .transcript import clip, summarize_args

#: Transcript line types that are the host talking to itself: queued input, titles, latches, the
#: last-prompt marker, attachment payloads. None of them is a turn by either participant.
HOUSEKEEPING = frozenset({
    "queue-operation", "custom-title", "atis-latch", "last-prompt", "attachment", "summary", "system",
})

#: The uncompacted sibling `preserve-chatlog` keeps beside the transcript. Reading both would
#: transcribe every entry twice — under different uuids only if the host reissued them, which it does
#: not, so the dedup would hold; but the *file* is a superset and reading it alone is strictly better.
FULL_SUFFIX = "-full.jsonl"


def transcripts(project: Project) -> list[Path]:
    """Every preserved host transcript in the project, uncompacted copy preferred.

    A project can hold several sessions — the hook names each file for its own session id — and all
    of them belong to the project's history. Where both a compacted and an uncompacted copy of the
    same session exist, only the uncompacted one is read: it is a superset, and the compacted one has
    dropped exactly the early exchanges where a direction was usually chosen.
    """
    events = project.events_dir
    if not events.is_dir():
        return []

    full = {p.name[: -len(FULL_SUFFIX)]: p for p in events.glob("chat-*" + FULL_SUFFIX)}
    chosen: dict[str, Path] = dict(full)

    for path in sorted(events.glob("chat-*.jsonl")):
        if path.name.endswith(FULL_SUFFIX):
            continue
        chosen.setdefault(path.stem, path)

    return [chosen[key] for key in sorted(chosen)]


def live_parent(project: Project) -> Path | None:
    """The host's own transcript for this project, still being written.

    The preserved copies only advance when the `preserve-chatlog` hook fires, which is at the end of
    a turn — and a subagent can work for forty minutes inside one turn. So a page watching the copies
    shows the spine live and the conversation frozen, which is exactly backwards from what a person
    wants while a stage is quiet: the renders are already visible, and what is missing is any account
    of what the agent is doing between them.

    The path is not guessed. `hooks.jsonl` records the transcript each hook firing resolved, so the
    project's own record says where the host keeps it — and a wrong guess would be worse than none,
    since a plausible path that does not exist reads as "the agent is idle".
    """
    for event in reversed(read_events(project.events_dir / "hooks.jsonl")):
        named = event.get("transcript")
        if not named:
            continue

        path = Path(named)
        return path if path.is_file() else None

    return None


def live_subagents(project: Project) -> list[tuple[Path, str]]:
    """The subagents' own transcripts in the host's store, with their roles.

    Claude Code keeps them beside the parent, under a directory named for the session:
    `<session>/subagents/agent-*.jsonl`, each with a `.meta.json` naming its `agentType`.
    """
    parent = live_parent(project)
    if parent is None:
        return []

    return _roles_in(parent.parent / parent.stem / "subagents")


def sources(project: Project) -> list[tuple[Path, str | None]]:
    """Every transcript worth reading, live where possible and preserved otherwise.

    Live wins for a session that has both, so the same conversation is never parsed twice — the
    subagent transcripts run to several megabytes and this is re-read every few seconds. Preserved
    copies still carry every earlier session, and remain the whole record once the host's store is
    cleaned up or the project is read on another machine.
    """
    found: list[tuple[Path, str | None]] = []
    seen: set[str] = set()

    if (parent := live_parent(project)) is not None:
        found.append((parent, None))
        seen.add(parent.stem)

    for path, role in live_subagents(project):
        found.append((path, role))
        seen.add(path.stem)

    for path in transcripts(project):
        # `chat-<session>.jsonl` and `chat-<session>-full.jsonl` both stand for one session, and the
        # live file is named for the session alone.
        session = path.stem.removeprefix("chat-").removesuffix("-full")
        if session not in seen:
            found.append((path, None))

    for path, role in subagents(project):
        if path.stem not in seen:
            found.append((path, role))

    return found


def subagents(project: Project) -> list[tuple[Path, str]]:
    """Every preserved subagent transcript, with the role it belongs to.

    A multi-agent run happens mostly *inside* these. The parent transcript records that a subagent
    was dispatched and what it returned; the subagent's own transcript is where the scripts were
    written and the renders looked at — in a real `comic_studio` run, 2.27 MB against the parent's
    578 KB, holding every one of the ten executions the server recorded.

    The role comes from the sidecar `.meta.json` the host writes beside each one (`agentType`:
    `penciler`, `inker`). Without it every subagent would code as one anonymous actor, which is the
    "attribute the spine" gap the record has carried since it was shaped for multiple agents — and
    the file naming it is already there to be read.
    """
    return _roles_in(project.events_dir / "subagents")


def _roles_in(directory: Path) -> list[tuple[Path, str]]:
    """Subagent transcripts in one directory, each with the role its sidecar names."""
    if not directory.is_dir():
        return []

    found: list[tuple[Path, str]] = []
    for path in sorted(directory.glob("agent-*.jsonl")):
        role = path.stem
        meta = path.with_suffix(".meta.json")
        try:
            role = json.loads(meta.read_text(encoding="utf-8")).get("agentType") or role
        except (OSError, ValueError):
            # A transcript with no readable meta is still worth reading; it is the attribution that
            # degrades, not the record, so it falls back to the file's own name.
            pass
        found.append((path, role))

    return found


def entries(path: Path) -> Iterator[dict[str, Any]]:
    """The conversational entries of one transcript, in file order, housekeeping dropped.

    Unparseable lines are skipped rather than raised on, as `read_events` does: a transcript is being
    appended to by another process and its last line is routinely half-written.
    """
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return

    for line in text.splitlines():
        if not line.strip():
            continue
        try:
            entry = json.loads(line)
        except json.JSONDecodeError:
            continue

        if not isinstance(entry, dict):
            continue
        if entry.get("type") in HOUSEKEEPING:
            continue
        if not entry.get("uuid") or not entry.get("timestamp"):
            continue

        yield entry


def transcribe(entry: dict[str, Any], role: str | None = None) -> list[tuple[str, str, dict[str, Any]]]:
    """One transcript entry as `(src, type, fields)` triples in the record's own vocabulary.

    Returns a list because one assistant entry routinely carries several: a thinking block, some
    prose, and two tool calls are four events, and flattening them is what lets the curve see the
    reasoning separately from the acting.
    """
    kind = entry.get("type")
    content = (entry.get("message") or {}).get("content")
    uuid = entry.get("uuid")

    common: dict[str, Any] = {"uuid": uuid}
    if role or entry.get("isSidechain"):
        # A subagent's turn. `role` names which one, read from the `.meta.json` the host writes
        # beside the transcript; a sidechain entry with no role known still says it was one, because
        # "some subagent" is a better reading than attributing its work to the director.
        common["agent"] = role or "subagent"
        common["sidechain"] = True

    if kind == "user":
        # A string is the director speaking. A list is the harness returning tool results, which is
        # the environment answering the agent rather than a person saying anything.
        if not isinstance(content, str) or not content.strip():
            return []

        # ...except inside a subagent's transcript, where the "user" is the coordinator. Its two
        # forms are the dispatch brief and a relay of something the director said while the subagent
        # worked — one is not the human at all, and the other is the human quoted by an agent.
        # Recording either as the director would put words in a person's mouth.
        #
        # Nothing is lost by dropping them: the dispatch is already the parent's `Agent` tool call,
        # and the interjection is already a real `user` turn in the parent transcript, correctly
        # attributed. Transcribing them here would double-count the same exchange.
        if role is not None:
            return []

        text, truncated = clip(content.strip())
        return [("director", "message", {**common, "text": text, "truncated": truncated or None})]

    if kind != "assistant" or not isinstance(content, list):
        return []

    out: list[tuple[str, str, dict[str, Any]]] = []
    for block in content:
        if not isinstance(block, dict):
            continue

        match block.get("type"):
            case "thinking":
                # Emitted even when there is nothing to read. See `Reasoning is not in the
                # transcript` in the module docstring: the block proves the agent deliberated and
                # says when, and dropping it would take every `wait` out of a host-driven curve.
                text, truncated = clip(block.get("thinking") or "")
                out.append(("agent", "thinking", {
                    **common,
                    "text": text or None,
                    "truncated": truncated or None,
                    "redacted": None if text else True,
                }))
            case "text":
                text, truncated = clip(block.get("text") or "")
                if text:
                    out.append(("agent", "text", {**common, "text": text,
                                                  "truncated": truncated or None}))
            case "tool_use":
                # The name is kept as the host wrote it, `mcp__` prefix and all. See the module note.
                out.append(("agent", "tool.call", {
                    **common,
                    "tool": block.get("name") or "?",
                    "args": summarize_args(block.get("input") or {}),
                }))

    if (usage := (entry.get("message") or {}).get("usage")) and out:
        # Attached to the entry rather than to a block, so it is emitted once per assistant message
        # and only when the message said something. `iterations` and `speed` are host telemetry.
        out.append(("agent", "usage", {
            **common,
            "inputTokens": usage.get("input_tokens"),
            "outputTokens": usage.get("output_tokens"),
            "cacheReadTokens": usage.get("cache_read_input_tokens"),
            "cacheWriteTokens": usage.get("cache_creation_input_tokens"),
            "model": (entry.get("message") or {}).get("model"),
        }))

    return out


class HostTranscript:
    """Transcribes a host-driven project's conversation into its record. Idempotent."""

    # region Constructors
    def __init__(self, project: Project, sink: Any = None) -> None:
        self.project = project
        self.agent = EventLog(project.agent_events, "agent", sink=sink)
        self.director = EventLog(project.director_events, "director", sink=sink)
    # endregion

    # region Methods
    def written(self) -> set[str]:
        """Transcript uuids already in the record.

        Read back rather than remembered. A state file beside the record is one more thing that can
        disagree with it, and the disagreement would be invisible: a stale offset re-transcribes a
        whole session, and a lost one drops it. What was written is the only reliable account of what
        was written.
        """
        seen: set[str] = set()
        for path in (self.project.agent_events, self.project.director_events):
            for event in read_events(path):
                if (uuid := event.get("uuid")):
                    seen.add(uuid)
        return seen

    def sync(self) -> int:
        """Transcribes everything not yet in the record. Returns how many events were appended.

        Ordered by the transcript's own timestamp, so a project holding several sessions reads as one
        history rather than as its files happen to be named.
        """
        seen = self.written()
        pending: list[tuple[dict[str, Any], str | None]] = []

        # The director's conversation and each subagent's own, live where the host is still writing
        # them and preserved otherwise. All are read every pass and deduplicated by uuid, so a
        # subagent that finishes between syncs is picked up whole without the earlier part arriving
        # twice — and a live file being read mid-append costs nothing, because a half-written last
        # line fails to parse and is simply picked up on the next pass.
        for path, role in sources(self.project):
            pending += [(e, role) for e in entries(path) if e["uuid"] not in seen]

        # By time across all of them, so a subagent's work lands between the dispatch that asked for
        # it and the reply that reported it, rather than in a block after the parent's whole session.
        pending.sort(key=lambda p: (p[0].get("timestamp") or "", p[0].get("uuid") or ""))

        written = 0
        for entry, role in pending:
            at = _stamp(entry.get("timestamp") or "")
            for src, kind, fields in transcribe(entry, role):
                log = self.agent if src == "agent" else self.director
                log.append(kind, at=at, **{k: v for k, v in fields.items() if v is not None})
                written += 1

        return written
    # endregion


def _stamp(iso: str) -> str | None:
    """The transcript's timestamp in the record's format, or None to fall back to now.

    The host writes `2026-09-01T07:04:30.719Z`, which is already what `events.timestamp()` produces.
    Anything else — a different precision, an offset, a missing `Z` — is normalised rather than
    passed through, because the whole point of the shared format is that one string sort orders all
    three files.
    """
    if not iso:
        return None

    from datetime import datetime, timezone

    try:
        parsed = datetime.fromisoformat(iso.replace("Z", "+00:00"))
    except ValueError:
        return None

    parsed = parsed.astimezone(timezone.utc)
    return f"{parsed.strftime('%Y-%m-%dT%H:%M:%S')}.{parsed.microsecond // 1000:03d}Z"
