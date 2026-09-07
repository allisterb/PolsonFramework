"""Watching a project this process is not driving.

The regression that matters most is the first one: `project.load` refuses anything not generated for
the Antigravity SDK, because the orchestrator cannot *run* it — and a project generated for Claude
Code is precisely what an observation is for. Applying a driver's checks to a reader refused the
feature's whole subject, and the failure looked like a sound one because the message was true.

Everything here reads the filesystem. Nothing starts an agent, and nothing here can.
"""

from __future__ import annotations

import json
import shutil
import tempfile
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

from orchestrator import project as project_mod
from studio import app as app_mod
from studio import observe as observe_mod
from studio.runs import Registry


def make_project(root: Path, name: str, *, sdk: str = "agy", events: list[dict] | None = None,
                 agent: list[dict] | None = None) -> Path:
    """A project directory as `create-project` writes one, with a record in it."""
    project = root / name
    (project / "events").mkdir(parents=True)
    (project / "artifacts").mkdir()
    (project / "scripts").mkdir()

    (project / "project.json").write_text(
        json.dumps({"id": name, "workflow": "comic", "sdk": sdk, "profile": "standalone"}),
        encoding="utf-8")
    (project / "GEMINI.md").write_text("# instructions", encoding="utf-8")
    (project / "agent.config.json").write_text(json.dumps({"agentBehavior": "interactive"}),
                                               encoding="utf-8")
    (project / "mcp_config.json").write_text(
        json.dumps({"mcpServers": {"polson": {"command": "dotnet", "args": []}}}), encoding="utf-8")

    lines = events if events is not None else [
        {"ts": "2026-09-01T10:00:00.000Z", "seq": 1, "src": "server", "type": "run.start"},
        {"ts": "2026-09-01T10:00:05.000Z", "seq": 2, "src": "server", "type": "stage.begin",
         "stage": "Pencil"},
    ]
    (project / "events" / "server.jsonl").write_text(
        "".join(json.dumps(e) + "\n" for e in lines), encoding="utf-8", newline="")

    if agent is not None:
        (project / "events" / "agent.jsonl").write_text(
            "".join(json.dumps(e) + "\n" for e in agent), encoding="utf-8", newline="")
    return project


class ReadingAProjectTests(unittest.TestCase):
    """`project.read` against `project.load`: what a reader needs versus what a driver does."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-observe-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)

    def test_a_project_for_another_host_can_be_read_but_not_run(self):
        made = make_project(self.root, "acme", sdk="claude")

        with self.assertRaises(project_mod.ProjectError):
            project_mod.load(made)

        project = project_mod.read(made)
        self.assertEqual("acme", project.id)
        self.assertEqual("claude", project.sdk)

    def test_a_project_missing_everything_a_run_needs_is_still_readable(self):
        # The record is what a reader wants, and a record can outlive the wiring that produced it —
        # a project whose MCP config was deleted still has every render it ever made.
        made = make_project(self.root, "acme")
        (made / "agent.config.json").unlink()
        (made / "mcp_config.json").unlink()
        (made / "GEMINI.md").unlink()

        with self.assertRaises(project_mod.ProjectError):
            project_mod.load(made)

        self.assertEqual("acme", project_mod.read(made).id)

    def test_a_directory_that_is_not_a_project_is_still_refused(self):
        (self.root / "empty").mkdir()
        with self.assertRaises(project_mod.ProjectError):
            project_mod.read(self.root / "empty")


class SessionCutTests(unittest.TestCase):
    """Which run an observation shows."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-observe-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)

    def test_the_cut_is_the_last_run_start(self):
        # A project's files are appended to across every session it has had, so without the cut an
        # observation shows the whole history as though it were happening now.
        made = make_project(self.root, "acme", events=[
            {"ts": "2026-08-30T09:00:00.000Z", "seq": 1, "src": "server", "type": "run.start"},
            {"ts": "2026-08-30T09:30:00.000Z", "seq": 2, "src": "server", "type": "render"},
            {"ts": "2026-09-01T10:00:00.000Z", "seq": 3, "src": "server", "type": "run.start"},
            {"ts": "2026-09-01T10:05:00.000Z", "seq": 4, "src": "server", "type": "render"},
        ])

        self.assertEqual("2026-09-01T10:00:00.000Z",
                         observe_mod.session_since(project_mod.read(made)))

    def test_a_record_with_no_start_is_one_session_rather_than_none(self):
        made = make_project(self.root, "acme", events=[
            {"ts": "2026-09-01T10:05:00.000Z", "seq": 1, "src": "server", "type": "render"}])

        self.assertEqual("", observe_mod.session_since(project_mod.read(made)))


class PerSdkCutTests(unittest.TestCase):
    """Where a run begins is the host's answer, not one constant.

    `run.start` is the MCP *server* starting, and how often that happens is the host's decision.
    Claude Code and Antigravity spawn it per session, so it doubles as the run's start. ADK spawns
    it once for the whole app process and serves every invocation through it — measured on
    `tainted`, two `run.start` events an hour and a half apart, both server boots and neither a run
    beginning. So the cut has to ask the project which host it was generated for.
    """

    #: One server boot, then two runs through it. This is the ADK shape, and the shape that made a
    #: `run.start` cut replay an hour-old run as though it were happening now.
    BOOT = [{"ts": "2026-09-01T09:00:00.000Z", "seq": 1, "src": "server", "type": "run.start"},
            {"ts": "2026-09-01T11:30:00.000Z", "seq": 2, "src": "server", "type": "render"}]
    RUNS = [{"ts": "2026-09-01T09:00:01.000Z", "seq": 1, "src": "agent", "type": "run.begin"},
            {"ts": "2026-09-01T11:00:00.000Z", "seq": 2, "src": "agent", "type": "run.begin"}]

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-observe-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)

    def test_an_adk_project_cuts_at_the_last_invocation_not_the_server_boot(self):
        made = make_project(self.root, "acme", sdk="adk", events=self.BOOT, agent=self.RUNS)

        self.assertEqual("2026-09-01T11:00:00.000Z",
                         observe_mod.session_since(project_mod.read(made)))

    def test_every_other_host_still_cuts_at_the_server_start(self):
        # The same record read as an Antigravity or Claude project: `run.begin` is not their
        # vocabulary, and honouring it here would move the cut for hosts whose server start is
        # already the run's start.
        for sdk in ("agy", "claude"):
            with self.subTest(sdk=sdk):
                made = make_project(self.root, f"acme-{sdk}", sdk=sdk,
                                    events=self.BOOT, agent=self.RUNS)

                self.assertEqual("2026-09-01T09:00:00.000Z",
                                 observe_mod.session_since(project_mod.read(made)))

    def test_an_adk_project_with_no_invocation_marker_widens_rather_than_showing_everything(self):
        # Recorded before the transcript plugin existed, or one where `make_plugin` returned None.
        # The fallback is wider than the run in hand and narrower than the project's whole history,
        # so it can show work that is not this run's but never hide work that is.
        made = make_project(self.root, "acme", sdk="adk", events=self.BOOT, agent=[])

        self.assertEqual("2026-09-01T09:00:00.000Z",
                         observe_mod.session_since(project_mod.read(made)))

    def test_an_unknown_sdk_falls_back_rather_than_failing(self):
        made = make_project(self.root, "acme", sdk="somethingelse", events=self.BOOT)

        self.assertEqual("2026-09-01T09:00:00.000Z",
                         observe_mod.session_since(project_mod.read(made)))


class ObserveRouteTests(unittest.TestCase):
    """The route, and what an observed run refuses."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-observe-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

    def observe(self, name: str) -> str:
        response = self.client.post("/observe", data={"project": name}, follow_redirects=False)
        self.assertEqual(303, response.status_code, response.text[:400])
        return response.headers["location"].rsplit("/", 1)[-1]

    def test_a_claude_project_can_be_watched(self):
        """The regression. This is the case the feature exists for, and `load` refused it."""
        make_project(self.root, "acme", sdk="claude")

        run_id = self.observe("acme")
        self.assertEqual(200, self.client.get(f"/runs/{run_id}").status_code)

    def test_the_record_reaches_the_page(self):
        make_project(self.root, "acme")
        run_id = self.observe("acme")

        curve = self.client.get(f"/runs/{run_id}/curve").json()
        self.assertTrue(any(p["kind"] == "stage.begin" for p in curve["trace"]))

    def test_watching_the_same_project_twice_reuses_the_observation(self):
        # Two would put two tailers on one file and show the same run twice on the index.
        make_project(self.root, "acme")
        self.assertEqual(self.observe("acme"), self.observe("acme"))

    def test_an_observation_does_not_make_the_studio_look_busy(self):
        """The cap it must not trip: `start` refuses when a run is active, because a second agent
        session would be spending twice. An observation runs no agent, so it must not count."""
        make_project(self.root, "acme")
        self.observe("acme")

        self.assertIsNone(self.registry.active)

    def test_an_observed_run_says_so(self):
        make_project(self.root, "acme")
        run_id = self.observe("acme")

        self.assertTrue(self.registry.get(run_id).summary()["observed"])

    def test_direction_is_refused_because_the_host_owns_the_conversation(self):
        # Not a failure to route: there is deliberately no way in from here, and a page offering one
        # would be offering a channel that reaches nothing.
        make_project(self.root, "acme")
        run_id = self.observe("acme")

        self.assertEqual(400, self.client.post(f"/runs/{run_id}/say", data={"text": "hi"}).status_code)
        self.assertEqual(409, self.client.post(f"/runs/{run_id}/answer",
                                               data={"question": "q1", "text": "x"}).status_code)

    def test_the_index_says_a_claude_project_cannot_be_started_here(self):
        """`discover` must refuse on the same grounds `load` does, or the button lies.

        It checked only for a tool policy while `load` also checked the host, so a Claude project
        offered an enabled Start that failed on submit. The row now says what can be done with it.
        """
        make_project(self.root, "acme", sdk="claude")

        html = self.client.get("/").text
        self.assertIn('data-runnable="no"', html)
        self.assertIn("Watch it here", html)

    def test_watching_something_outside_the_root_is_refused(self):
        for attempt in ("../elsewhere", "/etc", "acme/../../escape"):
            response = self.client.post("/observe", data={"project": attempt},
                                        follow_redirects=False)
            self.assertNotEqual(303, response.status_code, attempt)


class ObservedRecordTests(unittest.TestCase):
    """The host's conversation joining the server's spine, which is the whole point."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-observe-"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

    def test_the_conversation_and_the_spine_reach_one_curve(self):
        made = make_project(self.root, "acme", sdk="claude", events=[
            {"ts": "2026-09-01T10:00:00.000Z", "seq": 1, "src": "server", "type": "run.start"},
            {"ts": "2026-09-01T10:00:20.000Z", "seq": 2, "src": "server", "type": "stage.begin",
             "stage": "Pencil"},
        ])

        # A host transcript as `preserve-chatlog` leaves one, inside the session window.
        chat = [
            {"uuid": "u1", "type": "user", "timestamp": "2026-09-01T10:00:05.000Z",
             "message": {"role": "user", "content": "begin the comic"}},
            {"uuid": "a1", "type": "assistant", "timestamp": "2026-09-01T10:00:10.000Z",
             "message": {"role": "assistant", "content": [
                 {"type": "thinking", "thinking": "", "signature": "sig"},
                 {"type": "text", "text": "Blocking the first panel."},
                 {"type": "tool_use", "name": "mcp__polson__ExecuteScript", "input": {"script": "x"}},
             ]}},
        ]
        (made / "events" / "chat-s1.jsonl").write_text(
            "".join(json.dumps(e) + "\n" for e in chat), encoding="utf-8", newline="")

        response = self.client.post("/observe", data={"project": "acme"}, follow_redirects=False)
        run_id = response.headers["location"].rsplit("/", 1)[-1]
        trace = self.client.get(f"/runs/{run_id}/curve").json()["trace"]

        # Both participants, which is what makes it a trajectory rather than one agent's log.
        self.assertEqual({"director", "agent"}, {p.get("agent") for p in trace})

        # The director's turn and the agent's prose code the same: one participant speaking to
        # another is communication whichever of them is speaking.
        said = [p for p in trace if p["mode"] == "communicate"]
        self.assertTrue(any(p["kind"] == "message" for p in said))
        self.assertTrue(any(p["kind"] == "text" for p in said))

        # The redacted thinking block still supplies the pause.
        self.assertTrue(any(p["mode"] == "wait" for p in trace))

        # And the Polson call is coded once, from the spine — not again from the transcript.
        self.assertEqual(0, sum(1 for p in trace if p.get("kind") == "tool.call"))

    def test_watching_a_project_twice_does_not_transcribe_it_twice(self):
        made = make_project(self.root, "acme", sdk="claude")
        (made / "events" / "chat-s1.jsonl").write_text(json.dumps({
            "uuid": "u1", "type": "user", "timestamp": "2026-09-01T10:00:05.000Z",
            "message": {"role": "user", "content": "begin"}}) + "\n", encoding="utf-8", newline="")

        self.client.post("/observe", data={"project": "acme"}, follow_redirects=False)
        agent_log = (made / "events" / "director.jsonl").read_text(encoding="utf-8")

        # A fresh registry, so a second server watching the same directory is the case under test.
        second = TestClient(app_mod.create_app(self.root, Registry()))
        second.post("/observe", data={"project": "acme"}, follow_redirects=False)

        self.assertEqual(agent_log, (made / "events" / "director.jsonl").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
