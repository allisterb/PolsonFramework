"""What the orchestrator writes down, and what it refuses to run.

`unittest` rather than pytest: nothing here may install a package (see the project guardrails), and
the standard library is enough for this.

    python -m unittest discover -s src/webapp -t src/webapp

Nothing in this file reaches the network or starts an agent. The parts that need a live model are
covered by an actual run — `run_studio.py` — because a mock of the SDK would only ever confirm the
mock.
"""

from __future__ import annotations

import json
import os
import shutil
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

from tests import HAS_DRIVER, NO_DRIVER
from orchestrator import events, project as project_mod, transcript as transcript_mod


def step(kind: str, **fields) -> SimpleNamespace:
    """A stand-in for an SDK `Step`. The transcript reads it by duck typing, so this is enough."""
    defaults = dict(
        id=fields.pop("id", "s1"),
        step_index=fields.pop("step_index", 0),
        type=SimpleNamespace(name=kind),
        status=SimpleNamespace(name=fields.pop("status", "DONE")),
        source=SimpleNamespace(name=fields.pop("source", "MODEL")),
        content="",
        thinking="",
        tool_calls=[],
        error="",
        is_complete_response=None,
        usage_metadata=None,
        depth=fields.pop("depth", 0),
        trajectory_id=fields.pop("trajectory_id", ""),
    )
    defaults.update(fields)
    return SimpleNamespace(**defaults)


class InteractionRecordTests(unittest.TestCase):
    """What the transcript has to carry for a run to be readable as interaction, not just output.

    Two fields, both cheap, both previously dropped. Without a duration every step is an instant, so
    deliberation cannot be told from a slow tool. Without depth a run with delegated work flattens
    into one undifferentiated sequence, which is the one thing a record of collaboration must not do.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-record-"))
        self.log = events.EventLog(self.root / "agent.jsonl", "agent")
        self.transcript = transcript_mod.Transcript(self.log)

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def written(self) -> list[dict]:
        return events.read_events(self.root / "agent.jsonl")

    def test_a_step_carries_how_long_it_took(self):
        """First sight to settled. The SDK's Step has no timestamp of its own, so this is the only source."""
        self.transcript.observe(step("THINKING", id="a", status="IN_PROGRESS", thinking="considering"))
        self.transcript.observe(step("THINKING", id="a", thinking="considering"))

        record = self.written()[0]
        self.assertEqual(record["type"], "thinking")
        self.assertIn("ms", record)
        self.assertGreaterEqual(record["ms"], 0)

    def test_a_step_seen_only_once_still_reports_a_duration(self):
        """A step that arrives already settled is stamped on arrival, so ms is 0 rather than missing."""
        self.transcript.observe(step("TEXT_RESPONSE", id="b", content="done"))

        self.assertEqual(self.written()[0]["ms"], 0)

    def test_a_subagents_work_is_attributed_to_it(self):
        """depth and trajectory_id are what separate a delegated pass from the main agent's own."""
        self.transcript.observe(step("TEXT_RESPONSE", id="c", content="designing", depth=1,
                                     trajectory_id="traj-designer"))

        record = self.written()[0]
        self.assertEqual(record["depth"], 1)
        self.assertEqual(record["trajectory"], "traj-designer")

    def test_the_main_agents_own_work_carries_neither(self):
        """Absent rather than null, matching the rest of the record: depth 0 is the ordinary case."""
        self.transcript.observe(step("TEXT_RESPONSE", id="d", content="drawing"))

        record = self.written()[0]
        self.assertNotIn("depth", record)
        self.assertNotIn("trajectory", record)

    def test_an_unfinished_step_written_at_turn_end_still_has_its_duration(self):
        """A turn that timed out should still say how long its last step had been running."""
        self.transcript.turn_start("go")
        self.transcript.observe(step("THINKING", id="e", status="IN_PROGRESS", thinking="mid-thought"))
        self.transcript.turn_end("timeout", "the turn did not complete")

        partial = [e for e in self.written() if e["type"] == "thinking"][0]
        self.assertTrue(partial["partial"])
        self.assertIn("ms", partial)


class EventLogTests(unittest.TestCase):
    """The record is merged with the .NET writer's, so its shape is a contract, not a preference."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-events-"))
        self.path = self.root / "events" / "agent.jsonl"
        self.log = events.EventLog(self.path, "agent")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_writes_the_agreed_key_order(self):
        self.log.append("turn.start", stage="Ideation", execution="abc123", chars=42)

        line = self.path.read_text(encoding="utf-8").splitlines()[0]
        self.assertEqual(
            list(json.loads(line)),
            ["ts", "seq", "src", "type", "stage", "execution", "chars"],
        )

    def test_lines_are_shaped_like_the_dotnet_writers(self):
        """One grep has to span all three files, so the separators must match RunEventLog's."""
        self.log.append("render", artifact="artifacts/one.webp")

        line = self.path.read_text(encoding="utf-8").splitlines()[0]
        self.assertIn('"type":"render"', line)
        self.assertNotIn('"type": "render"', line)

    def test_line_ending_is_a_bare_newline(self):
        """A file mixing endings is one a naive reader splits wrongly, and this runs on Windows."""
        self.log.append("turn.start")
        self.assertNotIn(b"\r", self.path.read_bytes())

    def test_none_fields_are_absent_rather_than_null(self):
        self.log.append("turn.end", status="ok", error=None)

        event = json.loads(self.path.read_text(encoding="utf-8"))
        self.assertNotIn("error", event)
        self.assertEqual("ok", event["status"])

    def test_sequence_continues_across_writers(self):
        """A restart must not repeat sequence numbers, or (src, seq) stops working as a tie-break."""
        self.log.append("turn.start")
        self.log.append("turn.end")

        reopened = events.EventLog(self.path, "agent")
        reopened.append("run.end")

        self.assertEqual([1, 2, 3], [e["seq"] for e in events.read_events(self.path)])

    def test_a_truncated_last_line_is_skipped_not_fatal(self):
        """A writer that died mid-append leaves one bad line; a reader must still read the rest."""
        self.log.append("turn.start")
        with open(self.path, "a", encoding="utf-8", newline="") as handle:
            handle.write('{"ts":"2026-01-01T00:00:0')

        self.assertEqual(1, len(events.read_events(self.path)))

    def test_a_copy_of_a_log_is_the_same_writer(self):
        """The SDK deep-copies its config at startup, and a hook reaches a log through it.

        Before this, a run died at agent startup with `cannot pickle '_thread.lock'` — nowhere near
        the log itself. Two copies would also mean two sequence counters on one file, which is the
        thing the one-writer rule exists to prevent.
        """
        import copy

        self.assertIs(self.log, copy.deepcopy(self.log))
        self.assertIs(self.log, copy.deepcopy({"hook": [self.log]})["hook"][0])

    def test_merge_interleaves_sources_by_timestamp(self):
        """Readers merge on read; nothing merges on disk. Written by hand so the clock is not the test."""
        server = self.root / "events" / "server.jsonl"
        self.write_lines(self.path, [("2026-01-01T00:00:01.000Z", 1, "agent", "turn.start"),
                                     ("2026-01-01T00:00:03.000Z", 2, "agent", "turn.end")])
        self.write_lines(server, [("2026-01-01T00:00:02.000Z", 1, "server", "script.start")])

        merged = events.merge(self.path, server)
        self.assertEqual(["turn.start", "script.start", "turn.end"], [e["type"] for e in merged])

    def test_a_same_millisecond_tie_breaks_on_source_then_sequence(self):
        """Two writers can land in the same millisecond; the order still has to be the same for everyone."""
        server = self.root / "events" / "server.jsonl"
        same = "2026-01-01T00:00:01.000Z"
        self.write_lines(self.path, [(same, 2, "agent", "turn.end"), (same, 1, "agent", "turn.start")])
        self.write_lines(server, [(same, 1, "server", "script.start")])

        merged = events.merge(self.path, server)
        self.assertEqual(["turn.start", "turn.end", "script.start"], [e["type"] for e in merged])

    @staticmethod
    def write_lines(path: Path, rows: list[tuple[str, int, str, str]]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="") as handle:
            for ts, seq, src, kind in rows:
                handle.write(json.dumps({"ts": ts, "seq": seq, "src": src, "type": kind}) + "\n")


class TranscriptTests(unittest.TestCase):
    """`receive_steps()` re-yields a step as it grows, so the de-duplication is the whole job."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-transcript-"))
        self.path = self.root / "agent.jsonl"
        self.transcript = transcript_mod.Transcript(events.EventLog(self.path, "agent"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def written(self) -> list[dict]:
        return events.read_events(self.path)

    def test_an_accumulating_step_is_written_once_in_its_final_form(self):
        self.transcript.observe(step("TEXT_RESPONSE", content="A dozen", status="ACTIVE"))
        self.transcript.observe(step("TEXT_RESPONSE", content="A dozen progressively", status="ACTIVE"))
        self.transcript.observe(step("TEXT_RESPONSE", content="A dozen progressively longer"))
        self.transcript.turn_end("done")

        texts = [e["text"] for e in self.written() if e["type"] == "text"]
        self.assertEqual(["A dozen progressively longer"], texts)

    def test_interleaved_steps_are_each_written_once(self):
        """Two steps accumulate at the same time, alternating. This is the shape a live run has.

        Taking "a different step arrived" as "the previous one finished" wrote the same tool call
        three times — a first run recorded 17 tool.call events for 6 scripts the server ran.
        """
        call = SimpleNamespace(name="ExecuteScript", server_name="polson", args={}, id="t:4")
        thinking = dict(id="t:3", step_index=3)
        tool = dict(id="t:4", step_index=4, tool_calls=[call])

        self.transcript.observe(step("TOOL_CALL", status="ACTIVE", **tool))
        self.transcript.observe(step("THINKING", status="ACTIVE", thinking="mulling", **thinking))
        self.transcript.observe(step("TOOL_CALL", status="ACTIVE", **tool))
        self.transcript.observe(step("THINKING", thinking="mulling it over", **thinking))
        self.transcript.observe(step("TOOL_CALL", **tool))
        self.transcript.turn_end("done")

        self.assertEqual(
            ["thinking", "tool.call", "turn.end"],
            [e["type"] for e in self.written()],
        )

    def test_a_late_duplicate_of_a_written_step_is_ignored(self):
        self.transcript.observe(step("TEXT_RESPONSE", content="settled"))
        self.transcript.observe(step("TEXT_RESPONSE", content="settled"))
        self.transcript.turn_end("done")

        self.assertEqual(1, len([e for e in self.written() if e["type"] == "text"]))

    def test_a_step_still_running_at_turn_end_is_written_as_partial(self):
        """A timeout must not silently drop what the agent was in the middle of doing."""
        self.transcript.observe(step("TEXT_RESPONSE", content="half a thought", status="ACTIVE"))
        self.transcript.turn_end("timeout", "the turn did not complete")

        text = [e for e in self.written() if e["type"] == "text"][0]
        self.assertTrue(text["partial"])
        self.assertEqual("ACTIVE", text["status"])

    def test_a_failed_step_does_not_end_the_turn(self):
        """The SDK retries a 429 and carries on; recording it as a turn end would misreport the run."""
        self.transcript.observe(step("TEXT_RESPONSE", content="code 429", status="ERROR"))
        self.transcript.observe(step("TEXT_RESPONSE", id="s2", content="recovered"))
        self.transcript.turn_end("done")

        self.assertEqual(1, len([e for e in self.written() if e["type"] == "turn.end"]))
        self.assertEqual("ERROR", [e for e in self.written() if e["type"] == "text"][0]["status"])

    def test_each_tool_call_in_a_step_is_its_own_event(self):
        call = SimpleNamespace(name="ExecuteScript", server_name="polson", args={"script": "x" * 500})
        self.transcript.observe(step("TOOL_CALL", tool_calls=[call, call]))
        self.transcript.turn_end("done")

        calls = [e for e in self.written() if e["type"] == "tool.call"]
        self.assertEqual(2, len(calls))
        self.assertEqual("polson", calls[0]["server"])

    def test_a_long_argument_is_described_rather_than_copied(self):
        """The server already saves every script to scripts/; copying it here doubles the record."""
        call = SimpleNamespace(name="ExecuteScript", server_name="polson",
                               args={"script": "y" * 900, "outFile": "artifacts/one.webp"})
        self.transcript.observe(step("TOOL_CALL", tool_calls=[call]))
        self.transcript.turn_end("done")

        args = [e for e in self.written() if e["type"] == "tool.call"][0]["args"]
        self.assertEqual("<900 chars>", args["script"])
        self.assertEqual("artifacts/one.webp", args["outFile"])

    def test_usage_is_recorded_when_the_model_reports_it(self):
        usage = SimpleNamespace(prompt_token_count=100, cached_content_token_count=None,
                                candidates_token_count=20, thoughts_token_count=5,
                                total_token_count=125)
        self.transcript.observe(step("TEXT_RESPONSE", content="done", usage_metadata=usage))
        self.transcript.turn_end("done")

        recorded = [e for e in self.written() if e["type"] == "usage"][0]
        self.assertEqual(125, recorded["total"])
        self.assertNotIn("cached", recorded)

    def test_compaction_is_recorded(self):
        """Invisible in any UI, and the only thing explaining an agent forgetting its own decisions."""
        self.transcript.observe(step("COMPACTION", id="c"))
        self.transcript.turn_end("done")

        self.assertIn("compaction", [e["type"] for e in self.written()])


class ProjectTests(unittest.TestCase):
    """A project directory is read, never guessed at — and never runs half-configured."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-project-"))
        self.dir = self.root / "acme"
        self.dir.mkdir()
        self.write("project.json", {"schema": 1, "id": "acme", "workflow": "logo", "sdk": "agy",
                                    "profile": "standalone", "conversationId": None})
        self.write("agent.config.json", {"schema": 1, "agentBehavior": "interactive",
                                         "deniedTools": ["generate_image", "run_command"],
                                         "saveDir": "session/save", "appDataDir": "session/appdata"})
        self.write(".agents/mcp_config.json", {"mcpServers": {"polson": {
            "command": "dotnet", "args": ["Polson.CLI.dll", "server", "--project-dir", "."]}}})
        (self.dir / "GEMINI.md").write_text("# instructions", encoding="utf-8")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def write(self, name: str, data: dict) -> None:
        path = self.dir / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data), encoding="utf-8")

    def test_reads_the_manifest_and_the_policy(self):
        p = project_mod.load(self.dir)

        self.assertEqual("acme", p.id)
        self.assertEqual("agy", p.sdk)
        self.assertTrue(p.is_standalone)
        self.assertEqual(("generate_image", "run_command"), p.denied_tools)
        self.assertEqual(self.dir / "session" / "save", p.save_dir)

    def test_the_wiring_file_follows_the_sdk(self):
        """Antigravity's wiring is mcp_config.json; .mcp.json is Claude Code's spelling."""
        p = project_mod.load(self.dir)
        self.assertEqual("polson", p.mcp.name)

        (self.dir / ".agents" / "mcp_config.json").unlink()
        self.write("mcp_config.json", {"mcpServers": {"polson": {"command": "dotnet", "args": []}}})
        self.assertEqual("polson", project_mod.load(self.dir).mcp.name)   # the root copy also serves

    def test_a_project_for_another_host_is_refused(self):
        """Its instructions, wiring and permissions all belong to a host that is not running."""
        self.write("project.json", {"schema": 1, "id": "acme", "workflow": "logo", "sdk": "claude",
                                    "profile": "managed"})

        with self.assertRaises(project_mod.ProjectError) as caught:
            project_mod.load(self.dir)
        self.assertIn("Antigravity", str(caught.exception))

    def test_the_project_dir_placeholder_is_made_absolute(self):
        """The generated file says '.' so the directory stays movable; the server is not launched inside it."""
        p = project_mod.load(self.dir)
        _, args = p.mcp.resolved(p.root)

        self.assertEqual(str(p.root), args[-1])
        self.assertNotIn(".", args[-1:])

    def test_the_instructions_filename_comes_from_the_policy(self):
        """The generator names the instructions file; the orchestrator does not assume it.

        The bootstrap prompt used to hardcode `GEMINI.md` and restate the working rules, which put a
        second copy of policy in Python where it could drift from the template that owns it.
        """
        self.write("agent.config.json", {"schema": 1, "agentBehavior": "interactive",
                                         "instructionsFile": "GEMINI.md",
                                         "deniedTools": ["generate_image"],
                                         "saveDir": "session/save", "appDataDir": "session/appdata"})

        self.assertEqual("GEMINI.md", project_mod.load(self.dir).instructions_file)

    def test_an_older_project_without_the_field_still_loads(self):
        """Projects generated before `instructionsFile` existed keep working.

        The default is safe because this module already refuses anything but an `agy` project, and
        every `agy` project's instructions are `GEMINI.md`.
        """
        self.assertEqual("GEMINI.md", project_mod.load(self.dir).instructions_file)

    def test_a_missing_gemini_md_is_refused(self):
        (self.dir / "GEMINI.md").unlink()

        with self.assertRaises(project_mod.ProjectError) as caught:
            project_mod.load(self.dir)
        self.assertIn("GEMINI.md", str(caught.exception))

    def test_wiring_with_no_server_is_refused(self):
        """Starting an agent with nothing to draw with wastes a whole run to reach the same message."""
        self.write(".agents/mcp_config.json", {"mcpServers": {}})

        with self.assertRaises(project_mod.ProjectError):
            project_mod.load(self.dir)

    def test_a_project_with_no_policy_is_refused_rather_than_run_wide_open(self):
        """No agent.config.json means no denied tools — including generate_image, the one control
        the studio's premise rests on. Running wide open is worse than not running."""
        (self.dir / "agent.config.json").unlink()

        with self.assertRaises(project_mod.ProjectError) as caught:
            project_mod.load(self.dir)
        self.assertIn("no tool policy", str(caught.exception))

    def test_the_managed_label_does_not_decide_whether_it_runs(self):
        """The file decides, not the manifest.

        Every Antigravity project carries `agent.config.json` whatever profile it was generated for,
        so one file set runs under either host. `profile` survives as a record of what it was made
        for, and a project labelled managed is refused only if the policy is genuinely absent —
        which is what stops a label and a file from disagreeing about the same question.
        """
        self.write("project.json", {"schema": 1, "id": "acme", "workflow": "logo", "sdk": "agy",
                                    "profile": "managed"})

        loaded = project_mod.load(self.dir)

        self.assertEqual(loaded.profile, "managed")
        self.assertFalse(loaded.is_standalone)
        self.assertIn("generate_image", loaded.denied_tools)

    def test_the_conversation_id_is_written_back_for_resume(self):
        p = project_mod.load(self.dir)
        p.record_conversation("conv-123")

        manifest = json.loads((self.dir / "project.json").read_text(encoding="utf-8"))
        self.assertEqual("conv-123", manifest["conversationId"])
        self.assertEqual("conv-123", project_mod.load(self.dir).conversation_id)


@unittest.skipUnless(HAS_DRIVER, NO_DRIVER)
class ConfigTests(unittest.TestCase):
    """The policy is the one thing that must not drift from what the generator wrote.

    Skipped without the SDK: `setUp` imports `orchestrator.run`, which imports the agent runtime at
    module scope. Everything else in this file reads records and needs no driver.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-config-"))
        self.dir = self.root / "acme"
        self.dir.mkdir()
        (self.dir / "GEMINI.md").write_text("# instructions", encoding="utf-8")
        (self.dir / "project.json").write_text(
            json.dumps({"id": "acme", "workflow": "logo", "sdk": "agy", "profile": "standalone"}),
            encoding="utf-8")
        (self.dir / "agent.config.json").write_text(
            json.dumps({"deniedTools": ["generate_image", "run_command"]}), encoding="utf-8")
        (self.dir / "mcp_config.json").write_text(
            json.dumps({"mcpServers": {"polson": {"command": "dotnet", "args": ["x.dll", "server"]}}}),
            encoding="utf-8")

        # `build_config` reads the credential, and there is deliberately no environment override:
        # the .NET MCP server reads only bin/cli/appsettings.json, and two halves of one studio
        # cannot have two answers to "which key". So the seam is patched instead — on `run`, which
        # imported the name, not on `credentials`, which no longer owns the lookup at that point.
        from orchestrator import run as run_mod

        self.previous_reader = run_mod.read_api_key
        run_mod.read_api_key = lambda: "test-key-not-used"

    def tearDown(self) -> None:
        from orchestrator import run as run_mod

        run_mod.read_api_key = self.previous_reader
        shutil.rmtree(self.root, ignore_errors=True)

    def test_a_global_allow_accompanies_every_deny(self):
        """The policy engine is fail-closed: denies alone deny ExecuteScript too, and the run hangs."""
        from orchestrator.run import build_config

        config = build_config(project_mod.load(self.dir), interactive=False)

        self.assertEqual(3, len(config.policies), "expected two denies and one global allow")

    def test_the_agent_is_confined_to_the_project(self):
        from orchestrator.run import build_config

        p = project_mod.load(self.dir)
        config = build_config(p, interactive=False)

        self.assertEqual([str(p.root)], config.workspaces)


if __name__ == "__main__":
    unittest.main()
