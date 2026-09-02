"""The host's conversation, transcribed into the record.

Everything here is offline and synthetic, but the entry shapes are taken from a real Claude Code
transcript rather than invented: the housekeeping line types, the `thinking` / `text` / `tool_use`
content blocks, the `mcp__server__Tool` naming, `isSidechain`, and the `usage` key spellings.

The two tests that matter most are the ones about *not* writing: transcribing the same conversation
twice must add nothing, and a Polson call must not be coded from both the spine and the transcript.
Both failures produce a record that looks richer than the run was.
"""

from __future__ import annotations

import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from orchestrator import csm, hostlog
from orchestrator.events import read_events


def entry(uuid: str, kind: str, content, *, ts: str = "2026-09-01T07:04:30.719Z", **extra):
    """One transcript line, in the host's shape."""
    line = {"uuid": uuid, "type": kind, "timestamp": ts, "sessionId": "s1", "isSidechain": False}
    line.update(extra)
    if content is not None:
        line["message"] = {"role": kind, "content": content}
    return line


class FakeProject:
    """Only what `HostTranscript` reaches for: a directory and the three event paths."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.events_dir = root / "events"
        self.events_dir.mkdir(parents=True, exist_ok=True)

    @property
    def server_events(self) -> Path:
        return self.events_dir / "server.jsonl"

    @property
    def agent_events(self) -> Path:
        return self.events_dir / "agent.jsonl"

    @property
    def director_events(self) -> Path:
        return self.events_dir / "director.jsonl"


class HostlogTestCase(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.project = FakeProject(Path(self._tmp.name))

    def write_transcript(self, *lines, name: str = "chat-s1.jsonl") -> Path:
        path = self.project.events_dir / name
        path.write_text("".join(json.dumps(o) + "\n" for o in lines), encoding="utf-8", newline="")
        return path


class TranscribeTests(HostlogTestCase):
    """One entry to the events it becomes."""

    def test_a_typed_user_turn_is_the_director_speaking(self):
        out = hostlog.transcribe(entry("u1", "user", "make the background green"))

        self.assertEqual([("director", "message")], [(s, k) for s, k, _ in out])
        self.assertEqual("make the background green", out[0][2]["text"])

    def test_a_tool_result_is_not_a_director_turn(self):
        # The harness answering the agent is the environment, not a person. Coding it as the
        # director's contribution would invent a participant who said nothing.
        out = hostlog.transcribe(
            entry("u2", "user", [{"type": "tool_result", "content": "ok"}]))

        self.assertEqual([], out)

    def test_one_assistant_entry_becomes_several_events(self):
        out = hostlog.transcribe(entry("a1", "assistant", [
            {"type": "thinking", "thinking": "the accent is too loud"},
            {"type": "text", "text": "Dialling the accent back."},
            {"type": "tool_use", "name": "mcp__polson__ExecuteScript", "input": {"script": "x"}},
        ]))

        self.assertEqual(["thinking", "text", "tool.call"], [k for _, k, _ in out])
        self.assertTrue(all(src == "agent" for src, _, _ in out))

    def test_a_tool_keeps_the_name_the_host_wrote(self):
        # The record says what happened; `csm` is where the prefix is interpreted away.
        out = hostlog.transcribe(entry("a2", "assistant", [
            {"type": "tool_use", "name": "mcp__polson__ExecuteScript", "input": {}}]))

        self.assertEqual("mcp__polson__ExecuteScript", out[0][2]["tool"])

    def test_a_whole_document_argument_is_described_rather_than_quoted(self):
        # A script or a file's content is already saved somewhere better — `scripts/`, or the file
        # itself — so an excerpt of it in the record is noise beside the real thing.
        out = hostlog.transcribe(entry("a3", "assistant", [
            {"type": "tool_use", "name": "Write", "input": {"content": "y" * 5000, "path": "a.js"}}]))

        args = out[0][2]["args"]
        self.assertEqual("a.js", args["path"])
        self.assertEqual("<5000 chars>", args["content"])

    def test_a_long_command_keeps_its_opening(self):
        """A shell command reduced to `<453 chars>` says a tool ran and nothing about what it does.

        That is exactly no help when one has been running for twenty minutes — while its first line
        says everything. A live run stalled on a `cat >> … << 'EOF'` heredoc, which is unreadable as
        a length and obvious as an excerpt.
        """
        command = "cat >> critique_log.md << 'EOF'\n" + ("## Stage 3 — Inker\n" * 60)
        out = hostlog.transcribe(entry("a4", "assistant", [
            {"type": "tool_use", "name": "Bash", "input": {"command": command}}]))

        shown = out[0][2]["args"]["command"]
        self.assertTrue(shown.startswith("cat >> critique_log.md << 'EOF'"), shown[:60])
        self.assertIn(f"<{len(command)} chars>", shown)
        self.assertLess(len(shown), len(command))

    def test_a_signature_only_thinking_block_is_still_recorded(self):
        # Whether the host stores the reasoning varies with the host's own version — present through
        # most of August 2026, absent since the 21st. Dropping the empty ones would take every `wait`
        # out of the curve and make such a run read as an agent that never paused: a systematic
        # distortion, where an acknowledged gap is merely a gap.
        out = hostlog.transcribe(entry("a7", "assistant", [
            {"type": "thinking", "thinking": "", "signature": "CAIS+AQ..."}]))

        self.assertEqual([("agent", "thinking")], [(s, k) for s, k, _ in out])
        self.assertTrue(out[0][2]["redacted"])
        self.assertIsNone(out[0][2]["text"])

    def test_thinking_text_is_kept_when_the_host_does_persist_it(self):
        out = hostlog.transcribe(entry("a8", "assistant", [
            {"type": "thinking", "thinking": "the accent is too loud"}]))

        self.assertEqual("the accent is too loud", out[0][2]["text"])
        self.assertIsNone(out[0][2]["redacted"])

    def test_a_subagents_turn_carries_the_role_it_belongs_to(self):
        out = hostlog.transcribe(
            entry("a9", "assistant", [{"type": "text", "text": "blocking the panel"}]), "penciler")

        self.assertEqual("penciler", out[0][2]["agent"])
        self.assertTrue(out[0][2]["sidechain"])

    def test_the_coordinators_brief_to_a_subagent_is_not_the_director_speaking(self):
        """Inside a subagent transcript the "user" is the coordinator, in one of two forms: the
        dispatch brief, or a relay of something the director said while the subagent worked. One is
        not the human and the other is the human quoted by an agent — recording either as the
        director would put words in a person's mouth. Both already exist in the parent transcript,
        correctly attributed, so nothing is lost."""
        for said in ("You are Stage 1 of the run. Read roles/01_penciler.md.",
                     "The coordinator sent a message while you were working:\nDirector here."):
            self.assertEqual([], hostlog.transcribe(entry("u9", "user", said), "penciler"))

        # The same text in the parent's own transcript is the director, and is kept.
        self.assertEqual(1, len(hostlog.transcribe(entry("u9", "user", "Director here."))))

    def test_a_subagents_turn_is_attributed_to_one(self):
        # Claude Code writes a subagent into the same transcript, which is what makes multi-agent
        # attribution reachable here at all.
        out = hostlog.transcribe(entry("a4", "assistant",
                                       [{"type": "thinking", "thinking": "blocking the panel"}],
                                       isSidechain=True))

        self.assertEqual("subagent", out[0][2]["agent"])

    def test_usage_is_recorded_once_per_message_not_once_per_block(self):
        # Two blocks, one message: the tokens were billed for the message, so counting them per
        # block would report a turn as several times more expensive than it was.
        plain = entry("a5", "assistant",
                      [{"type": "text", "text": "done"}, {"type": "text", "text": "and again"}])
        self.assertEqual(["text", "text"], [k for _, k, _ in hostlog.transcribe(plain)])

        with_usage = entry("a6", "assistant", [{"type": "text", "text": "done"},
                                               {"type": "text", "text": "and again"}])
        with_usage["message"]["usage"] = {"input_tokens": 10, "output_tokens": 3,
                                          "cache_read_input_tokens": 900}
        with_usage["message"]["model"] = "claude-opus-5"

        out = hostlog.transcribe(with_usage)
        self.assertEqual(["text", "text", "usage"], [k for _, k, _ in out])

        _, _, usage = out[-1]
        self.assertEqual(10, usage["inputTokens"])
        self.assertEqual(900, usage["cacheReadTokens"])
        self.assertEqual("claude-opus-5", usage["model"])


class ReadingTests(HostlogTestCase):
    """Which lines and which files are read at all."""

    def test_housekeeping_lines_are_not_turns(self):
        path = self.write_transcript(
            {"type": "queue-operation", "operation": "enqueue", "content": "hi"},
            {"type": "custom-title", "title": "a run"},
            entry("u1", "user", "begin"),
        )

        self.assertEqual(["u1"], [e["uuid"] for e in hostlog.entries(path)])

    def test_an_unparseable_line_is_skipped_not_raised_on(self):
        # A transcript is being appended to by another process; a half-written last line is normal.
        path = self.project.events_dir / "chat-s1.jsonl"
        path.write_text(json.dumps(entry("u1", "user", "begin")) + "\n{\"type\": \"user\"",
                        encoding="utf-8", newline="")

        self.assertEqual(["u1"], [e["uuid"] for e in hostlog.entries(path)])

    def test_the_uncompacted_copy_wins_over_the_compacted_one(self):
        # The compacted file has dropped exactly the early exchanges where a direction was chosen.
        self.write_transcript(entry("late", "user", "carry on"), name="chat-s1.jsonl")
        self.write_transcript(entry("early", "user", "begin"), entry("late", "user", "carry on"),
                              name="chat-s1-full.jsonl")

        chosen = hostlog.transcripts(self.project)
        self.assertEqual(["chat-s1-full.jsonl"], [p.name for p in chosen])

    def test_a_project_with_no_transcript_reads_as_empty(self):
        self.assertEqual([], hostlog.transcripts(self.project))
        self.assertEqual([], hostlog.subagents(self.project))

    def test_a_subagent_transcript_is_found_with_the_role_its_meta_names(self):
        sub = self.project.events_dir / "subagents"
        sub.mkdir()
        (sub / "agent-a7e2.jsonl").write_text("", encoding="utf-8")
        (sub / "agent-a7e2.meta.json").write_text(
            json.dumps({"agentType": "penciler", "spawnDepth": 1}), encoding="utf-8")

        self.assertEqual([("agent-a7e2.jsonl", "penciler")],
                         [(p.name, r) for p, r in hostlog.subagents(self.project)])

    def test_a_subagent_with_no_readable_meta_still_gets_read(self):
        # The attribution degrades to the file's own name; the record does not degrade at all.
        sub = self.project.events_dir / "subagents"
        sub.mkdir()
        (sub / "agent-b1.jsonl").write_text("", encoding="utf-8")

        self.assertEqual([("agent-b1.jsonl", "agent-b1")],
                         [(p.name, r) for p, r in hostlog.subagents(self.project)])


class SyncTests(HostlogTestCase):
    """Writing into the record, and not writing twice."""

    def test_a_conversation_lands_in_the_two_files_that_own_it(self):
        self.write_transcript(
            entry("u1", "user", "begin", ts="2026-09-01T07:00:00.100Z"),
            entry("a1", "assistant", [
                {"type": "thinking", "thinking": "reading the brief"},
                {"type": "tool_use", "name": "Read", "input": {"path": "brief.md"}},
            ], ts="2026-09-01T07:00:02.500Z"),
        )

        written = hostlog.HostTranscript(self.project).sync()
        self.assertEqual(3, written)

        director = read_events(self.project.director_events)
        agent = read_events(self.project.agent_events)

        self.assertEqual(["message"], [e["type"] for e in director])
        self.assertEqual(["thinking", "tool.call"], [e["type"] for e in agent])
        self.assertEqual("director", director[0]["src"])
        self.assertEqual("agent", agent[0]["src"])

    def test_events_keep_the_time_they_happened(self):
        # Stamping them with the transcription time would put a whole session at one instant,
        # collapsing the interleaving with server.jsonl and breaking every `since` cut.
        self.write_transcript(entry("u1", "user", "begin", ts="2026-08-30T11:22:33.444Z"))

        hostlog.HostTranscript(self.project).sync()

        self.assertEqual("2026-08-30T11:22:33.444Z",
                         read_events(self.project.director_events)[0]["ts"])

    def test_transcribing_the_same_conversation_twice_adds_nothing(self):
        # The hook overwrites the copy every turn, so this is the ordinary case rather than an edge.
        self.write_transcript(entry("u1", "user", "begin"),
                              entry("a1", "assistant", [{"type": "text", "text": "starting"}]))

        transcriber = hostlog.HostTranscript(self.project)
        self.assertEqual(2, transcriber.sync())
        self.assertEqual(0, transcriber.sync())
        self.assertEqual(0, hostlog.HostTranscript(self.project).sync())

        self.assertEqual(1, len(read_events(self.project.agent_events)))
        self.assertEqual(1, len(read_events(self.project.director_events)))

    def test_a_grown_transcript_adds_only_what_is_new(self):
        self.write_transcript(entry("u1", "user", "begin"))
        hostlog.HostTranscript(self.project).sync()

        self.write_transcript(entry("u1", "user", "begin"),
                              entry("a1", "assistant", [{"type": "text", "text": "starting"}]))

        self.assertEqual(1, hostlog.HostTranscript(self.project).sync())
        self.assertEqual(["text"], [e["type"] for e in read_events(self.project.agent_events)])

    def test_a_subagents_work_lands_between_the_dispatch_and_the_reply(self):
        """A multi-agent run happens mostly inside the subagents. The parent records that one was
        dispatched and what it returned; ordering by time is what puts the work in between rather
        than in a block after the whole parent session."""
        self.write_transcript(
            entry("p1", "assistant", [{"type": "tool_use", "name": "Agent",
                                       "input": {"subagent_type": "penciler"}}],
                  ts="2026-09-01T10:00:00.000Z"),
            entry("p2", "assistant", [{"type": "text", "text": "the penciler is done"}],
                  ts="2026-09-01T10:30:00.000Z"))

        sub = self.project.events_dir / "subagents"
        sub.mkdir()
        (sub / "agent-x.meta.json").write_text(json.dumps({"agentType": "penciler"}),
                                               encoding="utf-8")
        (sub / "agent-x.jsonl").write_text(json.dumps({
            "uuid": "s1", "type": "assistant", "timestamp": "2026-09-01T10:15:00.000Z",
            "isSidechain": True,
            "message": {"role": "assistant", "content": [
                {"type": "tool_use", "name": "mcp__polson__ExecuteScript", "input": {}}]}}) + "\n",
            encoding="utf-8", newline="")

        hostlog.HostTranscript(self.project).sync()

        recorded = [(e["type"], e.get("agent")) for e in read_events(self.project.agent_events)]
        self.assertEqual([("tool.call", None), ("tool.call", "penciler"), ("text", None)], recorded)

    def test_several_sessions_read_as_one_history_in_time_order(self):
        self.write_transcript(entry("b1", "user", "second session",
                                    ts="2026-09-01T09:00:00.000Z"), name="chat-s2.jsonl")
        self.write_transcript(entry("a1", "user", "first session",
                                    ts="2026-08-31T09:00:00.000Z"), name="chat-s1.jsonl")

        hostlog.HostTranscript(self.project).sync()

        said = [e["text"] for e in read_events(self.project.director_events)]
        self.assertEqual(["first session", "second session"], said)


class CodingTests(unittest.TestCase):
    """What the transcribed events mean to the curve."""

    def test_an_mcp_qualified_name_is_stripped_before_coding(self):
        self.assertEqual("ExecuteScript", csm.tool_name("mcp__polson__ExecuteScript"))
        self.assertEqual("Read", csm.tool_name("Read"))
        self.assertEqual("mcp__odd", csm.tool_name("mcp__odd"))

    def test_a_polson_call_is_not_coded_twice(self):
        # The spine already records what the script did. This is the one mistake that makes an
        # enriched curve worse than an unenriched one, and the MCP prefix is how it gets in.
        call = {"src": "agent", "type": "tool.call", "ts": "2026-09-01T07:00:00.000Z",
                "tool": "mcp__polson__ExecuteScript"}

        self.assertIsNone(csm._code_one(call, set()))

    def test_claude_codes_reading_tools_as_inspection(self):
        for tool in ("Read", "Grep", "Glob"):
            coded = csm._code_one({"src": "agent", "type": "tool.call", "tool": tool,
                                   "ts": "2026-09-01T07:00:00.000Z"}, set())
            self.assertIsNotNone(coded, tool)
            self.assertEqual("inspect", coded.mode, tool)

    def test_asking_the_director_a_question_is_communication(self):
        """The strongest `communicate` there is: the run cannot proceed until the other participant
        answers, which is OCSM's `joint` participation. It fell through uncoded because a host-driven
        question arrives as a tool call rather than through the director channel."""
        coded = csm._code_one({"src": "agent", "type": "tool.call", "tool": "AskUserQuestion",
                               "ts": "2026-09-01T14:27:44.000Z"}, set())

        self.assertIsNotNone(coded)
        self.assertEqual("communicate", coded.mode)

    def test_a_shell_call_is_left_uncoded_rather_than_guessed_at(self):
        # The same call reads a file, runs the tests, or deletes a directory. A gap is visible;
        # a wrong code is not.
        for tool in ("Bash", "PowerShell"):
            self.assertIsNone(csm._code_one({"src": "agent", "type": "tool.call", "tool": tool,
                                             "ts": "2026-09-01T07:00:00.000Z"}, set()), tool)

    def test_a_named_actor_beats_an_inferred_one(self):
        """`_agent_of` inferred the actor from the SDK's depth/trajectory fields and ignored an
        explicit name, so a subagent read from a host transcript coded as the anonymous `agent` —
        discarding the only real attribution the record has. A name is not an inference."""
        named = csm._code_one({"src": "agent", "type": "thinking", "agent": "penciler",
                               "ts": "2026-09-01T14:27:49.000Z"}, set())
        self.assertEqual("penciler", named.agent)

        # With no name, the SDK's own signal still decides, and its absence still means the main agent.
        self.assertEqual("agent", csm._code_one(
            {"src": "agent", "type": "thinking", "ts": "2026-09-01T14:27:49.000Z"}, set()).agent)
        self.assertEqual("agent:abc12345", csm._code_one(
            {"src": "agent", "type": "thinking", "depth": 1, "trajectory": "abc12345ef",
             "ts": "2026-09-01T14:27:49.000Z"}, set()).agent)

    def test_the_directors_typed_turn_is_a_contribution(self):
        coded = csm._code_one({"src": "director", "type": "message", "text": "make it green",
                               "ts": "2026-09-01T07:00:00.000Z"}, set())

        self.assertIsNotNone(coded)
        self.assertEqual("communicate", coded.mode)
        self.assertEqual("director", coded.agent)


if __name__ == "__main__":
    unittest.main()
