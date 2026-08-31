"""The studio over HTTP: what it serves, what it refuses, and what it will not spend.

Nothing here starts an agent. `Registry.start` is the only thing that would, and every test that
needs a run substitutes one — a live turn is a billed model call, and a test that makes one is a test
nobody runs.

    python -m unittest discover -s src/webapp -t src/webapp

The containment tests are the ones that matter most. `serve_artifact` opens files by a name a visitor
supplies, which makes it the only genuinely dangerous route in the app.
"""

from __future__ import annotations

import asyncio
import json
import shutil
import tempfile
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

from orchestrator.broker import Broker
from studio import app as app_mod
from studio.runs import Registry, Run, StudioError


def project_dir(root: Path, name: str, *, profile: str = "standalone", workflow: str = "logo") -> Path:
    """A project directory of the shape `create-project` writes, without running the CLI."""
    project = root / name
    (project / "events").mkdir(parents=True)
    (project / "artifacts").mkdir()
    (project / "scripts").mkdir()
    (project / "project.json").write_text(
        json.dumps({"id": name, "workflow": workflow, "sdk": "agy", "profile": profile}),
        encoding="utf-8")
    (project / "GEMINI.md").write_text("# instructions", encoding="utf-8")
    if profile == "standalone":
        (project / "agent.config.json").write_text(
            json.dumps({"deniedTools": ["generate_image", "run_command"]}), encoding="utf-8")
    (project / ".agents").mkdir()
    (project / ".agents" / "mcp_config.json").write_text(
        json.dumps({"mcpServers": {"polson": {"command": "dotnet", "args": ["x.dll"]}}}),
        encoding="utf-8")
    return project


class ContainmentTests(unittest.TestCase):
    """The one route that opens a file by a name a stranger chose."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-contain-"))
        (self.root / "inside").mkdir()
        (self.root / "inside" / "ok.webp").write_bytes(b"not really an image")
        (self.root / "secret.txt").write_text("should never be served", encoding="utf-8")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_a_path_inside_resolves(self):
        resolved = app_mod.contain(self.root / "inside", "ok.webp")

        self.assertEqual(resolved, (self.root / "inside" / "ok.webp").resolve())

    def test_a_nested_path_inside_resolves(self):
        (self.root / "inside" / "deep").mkdir()
        resolved = app_mod.contain(self.root / "inside", "deep/../ok.webp")

        self.assertEqual(resolved, (self.root / "inside" / "ok.webp").resolve())

    def test_traversal_is_refused(self):
        with self.assertRaises(StudioError):
            app_mod.contain(self.root / "inside", "../secret.txt")

    def test_a_deep_traversal_is_refused(self):
        with self.assertRaises(StudioError):
            app_mod.contain(self.root / "inside", "../../../../../../etc/passwd")

    def test_an_absolute_path_is_refused(self):
        """The check that catches this is resolving first — a string test for `..` would not."""
        with self.assertRaises(StudioError):
            app_mod.contain(self.root / "inside", str(self.root / "secret.txt"))

    def test_a_backslash_separator_is_refused_too(self):
        with self.assertRaises(StudioError):
            app_mod.contain(self.root / "inside", "..\\secret.txt")


class DiscoveryTests(unittest.TestCase):
    """What the front page offers, and what it says about what it will not run."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-discover-"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_a_standalone_project_is_offered(self):
        project_dir(self.root, "acme")
        found = app_mod.discover(self.root)

        self.assertEqual(len(found), 1)
        self.assertEqual(found[0]["name"], "acme")
        self.assertEqual(found[0]["why"], "")

    def test_a_managed_project_is_listed_with_the_reason_rather_than_hidden(self):
        """A project you cannot run is exactly the thing someone needs to see, and to see why."""
        project_dir(self.root, "desktop", profile="managed")
        found = app_mod.discover(self.root)

        self.assertEqual(len(found), 1)
        self.assertIn("managed", found[0]["why"])

    def test_an_unreadable_manifest_reports_itself(self):
        project = self.root / "broken"
        project.mkdir()
        (project / "project.json").write_text("{not json", encoding="utf-8")

        self.assertIn("unreadable", app_mod.discover(self.root)[0]["why"])

    def test_a_directory_without_a_manifest_is_not_a_project(self):
        (self.root / "notes").mkdir()

        self.assertEqual(app_mod.discover(self.root), [])


class RegistryTests(unittest.IsolatedAsyncioTestCase):
    """What the server will and will not spend."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-registry-"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    async def test_a_managed_project_is_refused_with_its_own_explanation(self):
        """The orchestrator's refusal, surfaced rather than turned into a 500."""
        registry = Registry()
        project = project_dir(self.root, "desktop", profile="managed")

        with self.assertRaises(StudioError) as caught:
            await registry.start(project, "begin")

        self.assertIn("managed project", str(caught.exception))

    async def test_a_missing_project_is_refused(self):
        with self.assertRaises(StudioError):
            await Registry().start(self.root / "nope", "begin")

    async def test_a_second_run_is_refused_while_one_is_going(self):
        """One at a time: each run is a live agent session, and two would be spending twice."""
        registry = Registry()
        registry._runs["held"] = self._pretend_live_run(project_dir(self.root, "busy"))

        with self.assertRaises(StudioError) as caught:
            await registry.start(project_dir(self.root, "acme"), "begin")

        # Named, so a visitor knows what is holding the slot rather than just that something is.
        self.assertIn("already going", str(caught.exception))
        self.assertIn("busy", str(caught.exception))

    async def test_the_daily_cap_is_a_refusal_not_a_queue(self):
        """A queue would let a page that looks idle be spending money."""
        registry = Registry(daily=1)
        registry._today.append("spent")

        with self.assertRaises(StudioError) as caught:
            await registry.start(project_dir(self.root, "acme"), "begin")

        self.assertIn("daily cap", str(caught.exception))

    def _pretend_live_run(self, project: Path) -> Run:
        """A run whose task never finishes, standing in for one that is actually running.

        A real Run always carries a loaded project — the refusal names it — so this does too rather
        than passing None and finding out at the point the message is built.
        """
        async def forever() -> None:
            await asyncio.Event().wait()

        run = Run(id="held", project=_loaded(project), prompt="", stream=None,
                  started="2026-08-30T00:00:00+00:00")
        run.task = asyncio.get_event_loop().create_task(forever())
        self.addCleanup(run.task.cancel)
        return run


class RoutingTests(unittest.TestCase):
    """The pages themselves, driven the way a browser drives them."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-routes-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_the_front_page_lists_what_is_there(self):
        project_dir(self.root, "acme", workflow="infographic")
        page = self.client.get("/")

        self.assertEqual(page.status_code, 200)
        self.assertIn("acme", page.text)
        self.assertIn("infographic", page.text)

    def test_an_empty_directory_says_how_to_make_a_project(self):
        page = self.client.get("/")

        self.assertIn("create-project", page.text)

    def test_starting_a_project_outside_the_root_is_refused(self):
        """The same containment as the artifact route: a project name is a path segment too."""
        page = self.client.post("/runs", data={"project": "../elsewhere"}, follow_redirects=False)

        self.assertEqual(page.status_code, 409)
        self.assertIn("outside", page.text)

    def test_a_refusal_re_renders_the_page_rather_than_raising(self):
        project_dir(self.root, "desktop", profile="managed")
        page = self.client.post("/runs", data={"project": "desktop"}, follow_redirects=False)

        self.assertEqual(page.status_code, 409)
        self.assertIn("managed project", page.text)
        self.assertIn("Start a run", page.text)   # still the page they were on

    def test_an_unknown_run_is_a_404(self):
        for path in ("/runs/nope", "/runs/nope/events", "/runs/nope/curve",
                     "/runs/nope/artifacts/a.webp"):
            self.assertEqual(self.client.get(path).status_code, 404, path)

    def test_the_stylesheet_is_served(self):
        self.assertEqual(self.client.get("/static/studio.css").status_code, 200)


class ArtifactRouteTests(unittest.TestCase):
    """Renders reach the page as URLs, which is what makes a refresh survivable."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-artifacts-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

        project = project_dir(self.root, "acme")
        (project / "artifacts" / "01.webp").write_bytes(b"pretend webp")
        (self.root / "outside.txt").write_text("never", encoding="utf-8")

        # A registered run, without starting one: this route reads the filesystem, not the agent.
        self.registry._runs["acme-1"] = Run(
            id="acme-1", project=_loaded(project), prompt="", stream=None,
            started="2026-08-30T00:00:00+00:00", status="done")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_a_render_is_served_by_the_path_the_record_names(self):
        page = self.client.get("/runs/acme-1/artifacts/01.webp")

        self.assertEqual(page.status_code, 200)
        self.assertEqual(page.content, b"pretend webp")

    def test_a_missing_render_is_a_404_not_a_500(self):
        self.assertEqual(self.client.get("/runs/acme-1/artifacts/nope.webp").status_code, 404)

    def test_traversal_out_of_the_artifacts_directory_is_refused(self):
        for attempt in ("../project.json", "../../outside.txt", "../GEMINI.md"):
            page = self.client.get(f"/runs/acme-1/artifacts/{attempt}")
            self.assertEqual(page.status_code, 404, attempt)
            self.assertNotIn(b"never", page.content)


class StreamTests(unittest.TestCase):
    """The record, as it is written, over SSE."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-stream-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

        project = project_dir(self.root, "acme")
        self.broker = Broker()
        self.registry._runs["acme-1"] = Run(
            id="acme-1", project=_loaded(project), prompt="", stream=_StubStream(self.broker),
            started="2026-08-30T00:00:00+00:00", status="done")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_a_watcher_gets_the_backlog_and_then_the_close(self):
        """Replay is what a refresh depends on: attaching late must still show the whole run."""
        self.broker.publish({"src": "server", "seq": 1, "type": "run.start"})
        self.broker.publish({"src": "server", "seq": 2, "type": "render",
                             "artifact": "artifacts/01.webp"})
        self.broker.close()

        with self.client.stream("GET", "/runs/acme-1/events") as response:
            self.assertEqual(response.status_code, 200)
            body = "".join(response.iter_text())

        self.assertIn("event: run.start", body)
        self.assertIn("event: render", body)
        self.assertIn("artifacts/01.webp", body)

        # And it says the run is over, so the page stops reconnecting.
        self.assertIn("event: run.closed", body)


class _StubStream:
    """A RunStream's surface, without the tailer or the agent behind it."""

    def __init__(self, broker: Broker) -> None:
        self.broker = broker

    def attach(self, *, replay: bool = True):
        return self.broker.attach(replay=replay)


def _loaded(project: Path):
    from orchestrator import project as project_mod
    return project_mod.load(project)


if __name__ == "__main__":
    unittest.main()
