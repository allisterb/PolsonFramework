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
import os
import importlib
import re
import shutil
import tempfile
import unittest
from unittest import mock
from pathlib import Path

from fastapi.testclient import TestClient

from orchestrator import director as director_mod
from orchestrator.broker import Broker
from orchestrator.events import EventLog
from studio import app as app_mod
from studio import projects as projects_mod
from studio.runs import Registry, Run, StudioError


def project_dir(root: Path, name: str, *, profile: str = "standalone", workflow: str = "logo",
                conversation: str = "", policy: bool = True) -> Path:
    """A project directory of the shape `create-project` writes, without running the CLI.

    `conversation` stands for a project that has already run: the orchestrator records the id into
    `project.json` when a turn ends, and that is what makes the next run a continuation.

    `policy` writes `agent.config.json`, which every Antigravity project now carries whatever its
    profile — so it defaults on for a managed project too, matching what the generator emits. Pass
    `policy=False` for the case that is genuinely unrunnable: a project generated before that
    change, or for another host, where the orchestrator would have no tool policy at all.
    """
    project = root / name
    (project / "events").mkdir(parents=True)
    (project / "artifacts").mkdir()
    (project / "scripts").mkdir()
    manifest = {"id": name, "workflow": workflow, "sdk": "agy", "profile": profile}
    if conversation:
        manifest["conversationId"] = conversation
    (project / "project.json").write_text(json.dumps(manifest), encoding="utf-8")
    (project / "GEMINI.md").write_text("# instructions", encoding="utf-8")
    if policy:
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

    def test_a_project_with_no_policy_is_listed_with_the_reason_rather_than_hidden(self):
        """A project you cannot run is exactly the thing someone needs to see, and to see why."""
        project_dir(self.root, "desktop", profile="managed", policy=False)
        found = app_mod.discover(self.root)

        self.assertEqual(len(found), 1)
        self.assertIn("agent.config.json", found[0]["why"])

    def test_a_managed_project_is_offered_because_the_file_set_is_the_same(self):
        """The label is not the gate. The file is.

        One file set runs under either host, so a project generated for a desktop host is startable
        here too — the trap being that the page used to read `profile` while the runner read the
        policy file, which is two answers to one question waiting to disagree.
        """
        project_dir(self.root, "desktop", profile="managed")
        found = app_mod.discover(self.root)

        self.assertEqual(found[0]["why"], "")
        self.assertEqual(found[0]["profile"], "managed")

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

    async def test_a_project_with_no_policy_is_refused_with_its_own_explanation(self):
        """The orchestrator's refusal, surfaced rather than turned into a 500."""
        registry = Registry()
        project = project_dir(self.root, "desktop", profile="managed", policy=False)

        with self.assertRaises(StudioError) as caught:
            await registry.start(project, "begin")

        self.assertIn("no tool policy", str(caught.exception))

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
        project_dir(self.root, "desktop", profile="managed", policy=False)
        page = self.client.post("/runs", data={"project": "desktop"}, follow_redirects=False)

        self.assertEqual(page.status_code, 409)
        self.assertIn("no tool policy", page.text)

        # Still the page they were on, keyed on the form rather than on a heading wording.
        self.assertIn('action="/runs"', page.text)

    def test_an_unknown_run_is_a_404(self):
        for path in ("/runs/nope", "/runs/nope/events", "/runs/nope/curve",
                     "/runs/nope/artifacts/a.webp"):
            self.assertEqual(self.client.get(path).status_code, 404, path)

    def test_the_stylesheet_is_served(self):
        self.assertEqual(self.client.get("/static/studio.css").status_code, 200)


class WorkflowCatalogueTests(unittest.TestCase):
    """`WORKFLOWS` is a closed set, so it has to be the *same* closed set the generator ships.

    It is deliberately hardcoded rather than discovered — no visitor string should ever select a
    template by name — but a hardcoded mirror drifts, and it drifts silently: a workflow added to
    `ProjectTemplate/` simply never appears in the form, and nobody finds out until someone asks why
    the option is missing. This compares the two and names the difference.
    """

    #: The generator discovers a workflow by the presence of this file, and its types by `type.*.md`.
    TEMPLATES = Path(__file__).resolve().parents[3] / "src" / "Polson.CLI" / "ProjectTemplate"

    def shipped(self) -> dict[str, tuple[str, ...]]:
        """What `create-project` would offer, read the way the generator reads it."""
        found = {}
        for d in sorted(self.TEMPLATES.iterdir()):
            # `_shared` carries no instructions.md, which is the only thing discovery looks for.
            if not d.is_dir() or not (d / "instructions.md").is_file():
                continue
            types = sorted(f.name[len("type."):-len(".md")] for f in d.glob("type.*.md"))
            found[d.name] = tuple(types)
        return found

    def test_the_form_offers_exactly_the_shipped_workflows(self):
        if not self.TEMPLATES.is_dir():
            self.skipTest("template tree not present; running outside a source checkout")

        self.assertEqual(self.shipped(), dict(projects_mod.WORKFLOWS))

    def test_all_types_is_the_union_of_every_workflows_types(self):
        union = sorted({t for types in projects_mod.WORKFLOWS.values() for t in types})
        self.assertEqual(union, list(projects_mod.ALL_TYPES))


class BriefValidationTests(unittest.TestCase):
    """What the form is allowed to send, checked before anything is spawned."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-brief-"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_a_valid_request_passes(self):
        projects_mod.check(self.root, "acme-mark", "logo", "")

    def test_a_name_that_is_a_path_is_refused(self):
        """The name becomes a directory. Nothing here builds a path out of an unchecked string."""
        for bad in ("../escape", "a/b", "a\\b", "", ".", "..", "x" * 65, "has space", "semi;colon"):
            with self.assertRaises(StudioError, msg=bad):
                projects_mod.check(self.root, bad, "logo", "")

    def test_an_unknown_workflow_is_refused(self):
        """The workflow selects which template becomes the agent's instructions."""
        with self.assertRaises(StudioError):
            projects_mod.check(self.root, "acme", "../_shared", "")

    def test_a_type_the_workflow_does_not_offer_is_refused(self):
        """Only a wrong type is refused — omitting one is legal, as it is for `create-project`."""
        with self.assertRaises(StudioError):
            projects_mod.check(self.root, "acme", "harness", "sculpture")
        with self.assertRaises(StudioError):
            # A real type, but of another workflow: `image` belongs to the harness, not to logo.
            projects_mod.check(self.root, "acme", "logo", "image")
        with self.assertRaises(StudioError):
            # `painting` offers none at all, so naming any is wrong rather than merely unmatched.
            projects_mod.check(self.root, "acme", "painting", "seed")

        projects_mod.check(self.root, "acme", "harness", "infographic")
        projects_mod.check(self.root, "acme", "logo", "geometric")
        projects_mod.check(self.root, "acme", "drawing", "seed")

    def test_omitting_the_type_is_legal_whatever_the_workflow_offers(self):
        """The generator defaults it or renders no type section; neither is an error to refuse."""
        for workflow in ("logo", "harness", "drawing", "comic", "painting"):
            with self.subTest(workflow=workflow):
                projects_mod.check(self.root, "acme", workflow, "")

    def test_an_existing_project_is_not_overwritten(self):
        """Nothing here writes into a directory that already holds someone's work."""
        (self.root / "taken").mkdir()

        with self.assertRaises(StudioError) as caught:
            projects_mod.check(self.root, "taken", "logo", "")

        self.assertIn("already a project", str(caught.exception))


class BriefCreationTests(unittest.IsolatedAsyncioTestCase):
    """Making a real project by really running the CLI. Local, free, and no agent involved."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-create-"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    @unittest.skipUnless(projects_mod.CLI_DLL.is_file(), "the Polson CLI is not built")
    async def test_a_brief_becomes_a_standalone_project(self):
        made = await projects_mod.create(
            self.root, "ferry", "logo", "",
            "A wordmark for a coastal ferry company. Must work at 16px and in one colour.")

        manifest = json.loads((made / "project.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(manifest["profile"], "standalone")
        self.assertEqual(manifest["workflow"], "logo")

        # Standalone always: the orchestrator refuses a managed project, because running one would
        # apply no tool policy at all - including the denial of generate_image.
        self.assertTrue((made / "agent.config.json").is_file())
        self.assertTrue((made / "GEMINI.md").is_file())

    @unittest.skipUnless(projects_mod.CLI_DLL.is_file(), "the Polson CLI is not built")
    async def test_the_brief_reaches_the_project_as_data(self):
        """It arrives by file, is sanitised on the way in, and lands in brief.md."""
        await projects_mod.create(self.root, "ferry", "logo", "",
                                  "A wordmark. The client insists on a lighthouse.")

        brief = (self.root / "ferry" / "brief.md").read_text(encoding="utf-8")
        self.assertIn("lighthouse", brief)

        # And it is marked as data for whatever reads it next.
        self.assertIn("data", brief.lower())

    async def test_an_empty_brief_is_refused_before_the_cli_is_spawned(self):
        with self.assertRaises(StudioError) as caught:
            await projects_mod.create(self.root, "ferry", "logo", "", "   ")

        self.assertIn("cannot infer", str(caught.exception))
        self.assertFalse((self.root / "ferry").exists())

    async def test_an_oversized_brief_is_refused(self):
        with self.assertRaises(StudioError) as caught:
            await projects_mod.create(self.root, "ferry", "logo", "",
                                      "x" * (projects_mod.MAX_BRIEF + 1))

        self.assertIn("limit", str(caught.exception))

    async def test_a_missing_cli_is_reported_rather_than_raised_as_a_traceback(self):
        original = projects_mod.launcher
        projects_mod.launcher = lambda: ["polson-does-not-exist"]
        self.addCleanup(setattr, projects_mod, "launcher", original)

        with self.assertRaises(StudioError) as caught:
            await projects_mod.create(self.root, "ferry", "logo", "", "a brief")

        self.assertIn("Could not run", str(caught.exception))


class BriefRouteTests(unittest.TestCase):
    """The form, driven the way a browser drives it."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-form-"))
        self.client = TestClient(app_mod.create_app(self.root, Registry()))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_the_form_offers_every_workflow(self):
        page = self.client.get("/")

        for workflow in projects_mod.WORKFLOWS:
            self.assertIn('value="' + workflow + '"', page.text)

    def test_a_refused_brief_is_given_back_rather_than_lost(self):
        """A refusal that clears the textarea costs the visitor their work."""
        brief = "A wordmark for a coastal ferry company, and it must survive a fax machine."
        page = self.client.post("/projects", data={
            "name": "not a valid name", "workflow": "logo", "kind": "", "brief": brief})

        self.assertEqual(page.status_code, 409)
        self.assertIn("coastal ferry", page.text)
        self.assertIn("letters, digits", page.text)

    def test_a_traversing_name_never_reaches_the_filesystem(self):
        page = self.client.post("/projects", data={
            "name": "../../etc", "workflow": "logo", "kind": "", "brief": "anything"})

        self.assertEqual(page.status_code, 409)
        self.assertEqual(list(self.root.iterdir()), [])

    @unittest.skipUnless(projects_mod.CLI_DLL.is_file(), "the Polson CLI is not built")
    def test_create_only_makes_the_project_without_spending_anything(self):
        """The two buttons differ by one field, and only one of them starts a billed session."""
        page = self.client.post("/projects", data={
            "name": "ferry", "workflow": "logo", "kind": "", "brief": "A ferry wordmark."},
            follow_redirects=False)

        self.assertEqual(page.status_code, 303)
        self.assertEqual(page.headers["location"], "/")
        self.assertTrue((self.root / "ferry" / "project.json").is_file())
        self.assertIsNone(self.client.app.state.registry.active)


class SourceOfferTests(unittest.TestCase):
    """AGPL s13 asks a networked version to offer its users the corresponding source.

    Asserted on the *rendered* page rather than on the constant, because the obligation is that a
    visitor can see it — a setting nothing renders honours nothing. The link is configurable so that
    honouring the licence after a fork is one environment variable rather than an edit to a template
    nobody thinks to look at.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-licence-"))
        self.client = TestClient(app_mod.create_app(self.root, Registry()))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_the_page_offers_the_source_and_names_the_licence(self):
        page = self.client.get("/").text

        self.assertIn(app_mod.SOURCE_URL, page)
        self.assertIn("AGPL-3.0", page)
        self.assertIn("agpl-3.0.html", page)

    def test_the_offer_is_a_template_global_so_a_new_page_cannot_omit_it(self):
        """A per-view context entry is one a later route forgets; a global is one it cannot."""
        self.assertEqual(app_mod.SOURCE_URL, app_mod.TEMPLATES.env.globals["source_url"])

    def test_a_fork_can_point_the_offer_at_its_own_source(self):
        with mock.patch.dict(os.environ, {"POLSON_SOURCE_URL": "https://example.invalid/fork"}):
            reloaded = importlib.reload(app_mod)
            try:
                self.assertEqual("https://example.invalid/fork", reloaded.SOURCE_URL)
            finally:
                importlib.reload(reloaded)


class QuestionSelectionTests(unittest.IsolatedAsyncioTestCase):
    """A chosen option has to survive the round trip, and it silently did not.

    The runner validates a reply against the agent's own option ids *and* the option texts it
    published. The page was posting a synthetic `opt1`, which matches neither — so a click validated
    as nothing, fell through to the free-text branch, found none, and settled the question as a skip.
    Every path returned 200 and the card showed as sent, while the agent was told nobody answered and
    chose the subject of the drawing itself.

    Two tests, because the bug lived in the seam: one pins the runner's contract, the other pins that
    the page posts something satisfying it.
    """

    class _Entry:
        """What the SDK hands the hook.

        The ids matter and the first version of this fixture left them blank, which is why it passed
        while the real thing was broken. `AskQuestionOption.id` is a required field the host numbers
        from 1, and the SDK's event processor resolves a choice with `int(opt_id) - 1` — so an id
        that will not parse as an integer is silently dropped downstream. A fixture with empty ids
        models a world where returning the text is harmless. That world does not exist.
        """

        class _Option:
            def __init__(self, index: int, text: str) -> None:
                self.id, self.text = str(index), text

        def __init__(self, question: str, options: list[str]) -> None:
            self.question = question
            self.options = [self._Option(i, o) for i, o in enumerate(options, start=1)]
            self.is_multi_select = False

    async def _settled(self, reply_with: dict):
        """Opens a question, replies to it the way the route does, and returns the response."""
        published = []
        log = EventLog(Path(tempfile.mkdtemp(prefix="polson-q-")) / "director.jsonl", "director")
        director = director_mod.WebDirector(log, published.append, timeout=5.0)
        entry = self._Entry("Which subject?", ["A street scene", "A portrait"])

        task = asyncio.create_task(director.answer(entry, [o.text for o in entry.options]))
        await asyncio.sleep(0)
        opened = next(e for e in published if e["type"] == "question.open")
        self.assertTrue(director.reply(opened["id"], **reply_with))
        return await task

    async def test_an_option_chosen_by_its_text_comes_back_as_that_options_id(self):
        """The page sends text; the agent needs the id. This is the translation that was missing."""
        response = await self._settled({"selected": ["A portrait"]})

        self.assertFalse(response.skipped)
        self.assertEqual(["2"], list(response.selected_option_ids))

    async def test_every_returned_id_survives_the_sdks_own_parse(self):
        """`int(opt_id) - 1` is how the SDK turns an id into a choice; anything else is dropped."""
        response = await self._settled({"selected": ["A street scene"]})

        indices = []
        for opt_id in response.selected_option_ids or []:
            try:
                indices.append(int(opt_id) - 1)
            except ValueError:
                pass

        self.assertEqual([0], indices, "the agent would have received a choice with nothing selected")

    async def test_an_id_sent_directly_is_honoured_too(self):
        """A client that already knows the id — a future one, or a retry — is not refused."""
        response = await self._settled({"selected": ["1"]})

        self.assertEqual(["1"], list(response.selected_option_ids))

    async def test_an_option_the_client_invented_is_still_refused(self):
        """The narrowing that caused this is correct and stays: a made-up id must not choose."""
        response = await self._settled({"selected": ["opt1"]})

        self.assertTrue(response.skipped)

    def test_the_page_posts_the_option_text_rather_than_a_synthetic_id(self):
        source = (Path(app_mod.__file__).parent / "templates" / "run.html").read_text(encoding="utf-8")
        button = re.search(r"<button type=\"submit\" name=\"option\" value=\"([^\"]*)\"", source)

        self.assertIsNotNone(button, "the question card no longer renders an option button")
        self.assertEqual("${esc(text)}", button.group(1),
                         "the option button must post the option's own text; a synthetic id "
                         "validates as nothing and settles the question as a skip")


class DirectorRouteTests(unittest.TestCase):
    """Answering, and interrupting, over HTTP."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-director-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

        project = project_dir(self.root, "acme")
        self.stream = _StubDirectorStream()
        self.run = Run(id="acme-1", project=_loaded(project), prompt="", stream=self.stream,
                       started="2026-08-30T00:00:00+00:00", status="running")
        self.registry._runs["acme-1"] = self.run

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def live(self, yes: bool = True) -> None:
        """A run is live when its task is unfinished, so this gives it one that says so.

        Patching the property would have meant patching the *class*, which leaks into every other
        test in the process — it did, and took an unrelated one down with it.
        """
        self.run.task = _Task(done=not yes)

    def test_an_answer_reaches_the_director(self):
        page = self.client.post("/runs/acme-1/answer",
                                data={"question": "q1", "text": "warmer, and lose the globe"})

        self.assertEqual(page.status_code, 200)
        self.assertEqual(self.stream.answered, [("q1", {"skipped": False, "text": "warmer, and lose the globe"})])

    def test_a_chosen_option_is_sent_as_an_option(self):
        self.client.post("/runs/acme-1/answer", data={"question": "q1", "option": "opt2"})

        self.assertEqual(self.stream.answered[0][1]["selected"], ["opt2"])

    def test_letting_it_decide_is_a_skip(self):
        self.client.post("/runs/acme-1/answer", data={"question": "q1", "skip": "1"})

        self.assertTrue(self.stream.answered[0][1]["skipped"])

    def test_a_settled_question_is_a_conflict_not_a_missing_page(self):
        """404 would have a page retrying forever; 409 says the question existed and is done with."""
        self.stream.settles = False
        page = self.client.post("/runs/acme-1/answer", data={"question": "gone", "text": "hello"})

        self.assertEqual(page.status_code, 409)
        self.assertIn("already settled", page.json()["detail"])

    def test_an_interjection_reaches_the_run(self):
        self.live(True)
        page = self.client.post("/runs/acme-1/say", data={"text": "make the background red"})

        self.assertEqual(page.status_code, 200)
        self.assertEqual(self.stream.said, ["make the background red"])

    def test_nothing_can_be_said_to_a_finished_run(self):
        self.live(False)
        page = self.client.post("/runs/acme-1/say", data={"text": "too late"})

        self.assertEqual(page.status_code, 409)
        self.assertIn("nothing is listening", page.json()["detail"])
        self.assertEqual(self.stream.said, [])

    def test_an_interjection_is_capped(self):
        """It reaches a running agent directly. Longer direction belongs in a brief."""
        self.live(True)
        page = self.client.post("/runs/acme-1/say",
                                data={"text": "x" * (app_mod.MAX_INTERJECTION + 1)})

        self.assertEqual(page.status_code, 413)
        self.assertEqual(self.stream.said, [])

    def test_an_unknown_run_is_still_a_404(self):
        self.assertEqual(self.client.post("/runs/nope/answer",
                                          data={"question": "q1"}).status_code, 404)
        self.assertEqual(self.client.post("/runs/nope/say", data={"text": "hi"}).status_code, 404)


class _Task:
    """Just enough of an asyncio.Task for `Run.live`, which only ever asks whether it is done."""

    def __init__(self, *, done: bool) -> None:
        self._done = done

    def done(self) -> bool:
        return self._done


class _StubDirectorStream:
    """A RunStream's director surface, without an agent behind it."""

    def __init__(self) -> None:
        self.answered: list[tuple] = []
        self.said: list[str] = []
        self.settles = True

    def answer(self, question_id: str, **reply) -> bool:
        if not self.settles:
            return False
        self.answered.append((question_id, reply))
        return True

    def say(self, text: str) -> bool:
        text = text.strip()
        if not text:
            return False
        self.said.append(text)
        return True

    def attach(self, *, replay: bool = True):
        raise AssertionError("not used by these tests")


class ContinuationTests(unittest.IsolatedAsyncioTestCase):
    """A project that has run before continues rather than starts over.

    Continuing is right — it is what carrying on with a piece means, and the agent keeps what it
    decided and why. It is also the quiet way to waste a turn: a finished project resumed with the
    opening prompt answers "the project has completed all 7 stages", spends a full agent session, and
    draws nothing. That happened on a real run before any of this existed.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-resume-"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    async def test_continuing_with_the_opening_prompt_is_refused(self):
        project = project_dir(self.root, "acme", conversation="conv-1")

        with self.assertRaises(StudioError) as caught:
            await Registry().start(project, "")

        self.assertIn("already has a session", str(caught.exception))
        self.assertIn("already done", str(caught.exception))

    async def test_a_project_that_never_ran_needs_no_instruction(self):
        """Nothing to continue, so the opening prompt is exactly right."""
        registry = Registry()
        project = project_dir(self.root, "acme")

        run = await registry.start(project, "")
        self.addCleanup(run.task.cancel)

        self.assertFalse(run.resumed)

    async def test_a_fresh_session_ignores_the_recorded_conversation(self):
        """Starting over is allowed, and then the opening prompt is right again."""
        registry = Registry()
        project = project_dir(self.root, "acme", conversation="conv-1")

        run = await registry.start(project, "", resume=False)
        self.addCleanup(run.task.cancel)

        self.assertFalse(run.resumed)

    async def test_continuing_with_something_to_do_is_allowed_and_says_so(self):
        registry = Registry()
        project = project_dir(self.root, "acme", conversation="conv-1")

        run = await registry.start(project, "make the timeline bars thinner")
        self.addCleanup(run.task.cancel)

        self.assertTrue(run.resumed)
        self.assertTrue(run.summary()["resumed"])


class ContinuationRouteTests(unittest.TestCase):
    """What the form shows before a turn is spent."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-resume-route-"))
        self.client = TestClient(app_mod.create_app(self.root, Registry()))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_the_page_says_which_projects_have_a_session(self):
        project_dir(self.root, "started", conversation="conv-1")
        project_dir(self.root, "untouched")

        found = {p["name"]: p["session"] for p in app_mod.discover(self.root)}
        self.assertEqual(found, {"started": True, "untouched": False})

        page = self.client.get("/")
        self.assertIn("has a session", page.text)
        self.assertIn("not started", page.text)

    def test_the_refusal_reaches_the_page_rather_than_a_traceback(self):
        project_dir(self.root, "started", conversation="conv-1")
        page = self.client.post("/runs", data={"project": "started", "prompt": ""},
                                follow_redirects=False)

        self.assertEqual(page.status_code, 409)
        self.assertIn("already has a session", page.text)

    def test_asking_for_a_fresh_session_bypasses_the_refusal(self):
        project_dir(self.root, "started", conversation="conv-1")
        page = self.client.post("/runs", data={"project": "started", "prompt": "", "fresh": "1"},
                                follow_redirects=False)

        self.assertEqual(page.status_code, 303)

        # By its id from the redirect, not via `active`: with no credentials the turn fails at once,
        # so the run is registered but no longer live by the time this looks.
        run_id = page.headers['location'].rsplit('/', 1)[-1]
        run = self.client.app.state.registry.get(run_id)
        self.addCleanup(run.task.cancel)
        self.assertFalse(run.resumed)


class ScriptRouteTests(unittest.TestCase):
    """The half of a run a rendered image cannot carry.

    A bitmap records what the canvas ended up looking like. The script records why — the ratio a
    mast was placed on, the scale a bar was measured against, the direction tried and abandoned in a
    comment. Serving it beside the render is what makes the trace readable as reasoning.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-scripts-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

        project = project_dir(self.root, "acme")
        (project / "scripts" / "0001.js").write_text(
            "// Golden section, not a guess.\nconst mast = beam * 0.618;\n", encoding="utf-8")
        (self.root / "outside.txt").write_text("never", encoding="utf-8")

        self.registry._runs["acme-1"] = Run(
            id="acme-1", project=_loaded(project), prompt="", stream=None,
            started="2026-08-30T00:00:00+00:00", status="done")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_a_script_is_served_highlighted(self):
        page = self.client.get("/runs/acme-1/scripts/0001.js")

        self.assertEqual(page.status_code, 200)
        self.assertIn("createCanvas" if False else "mast", page.text)

        # Highlighted rather than plain: the comment and the keyword are marked up differently.
        self.assertIn('class="code"', page.text)
        self.assertIn("<span", page.text)

    def test_the_source_survives_highlighting(self):
        """A viewer reads this to understand the run, so the text has to be the text."""
        page = self.client.get("/runs/acme-1/scripts/0001.js")

        stripped = re.sub(r"<[^>]+>", "", page.text)
        self.assertIn("Golden section, not a guess.", stripped)
        self.assertIn("0.618", stripped)

    def test_line_numbers_are_present(self):
        """The record names scripts by path; a person names lines within them."""
        self.assertIn("linenos", self.client.get("/runs/acme-1/scripts/0001.js").text)

    def test_a_missing_script_is_a_404(self):
        self.assertEqual(self.client.get("/runs/acme-1/scripts/9999.js").status_code, 404)

    def test_traversal_out_of_the_scripts_directory_is_refused(self):
        """Same containment as artifacts: a script name is a visitor-supplied path too."""
        for attempt in ("../project.json", "../../outside.txt", "../GEMINI.md",
                        "../artifacts/01.webp"):
            page = self.client.get(f"/runs/acme-1/scripts/{attempt}")
            self.assertEqual(page.status_code, 404, attempt)
            self.assertNotIn("never", page.text)

    def test_the_highlighting_styles_are_served(self):
        page = self.client.get("/code.css")

        self.assertEqual(page.status_code, 200)
        self.assertIn("text/css", page.headers["content-type"])
        self.assertIn(".code", page.text)


class CurveRouteTests(unittest.TestCase):
    """The run coded as creative sense-making, for the page to draw."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-curve-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

        project = project_dir(self.root, "acme")
        (project / "events" / "server.jsonl").write_text("\n".join([
            json.dumps({"ts": "2026-08-30T20:00:01.000Z", "seq": 1, "src": "server",
                        "type": "stage.begin", "stage": "Blocking"}),
            json.dumps({"ts": "2026-08-30T20:00:02.000Z", "seq": 2, "src": "server",
                        "type": "inspect", "execution": "e1", "probes": {"measure": 3}, "total": 3}),
            json.dumps({"ts": "2026-08-30T20:00:03.000Z", "seq": 3, "src": "server",
                        "type": "render", "execution": "e1", "artifact": "artifacts/01.webp"}),
            json.dumps({"ts": "2026-08-30T20:00:20.000Z", "seq": 4, "src": "server",
                        "type": "script.ok", "execution": "e1", "script": "scripts/0001.js"}),
        ]) + "\n", encoding="utf-8")

        self.registry._runs["acme-1"] = Run(
            id="acme-1", project=_loaded(project), prompt="", stream=None,
            started="2026-08-30T00:00:00+00:00", status="done")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_the_curve_carries_a_point_per_coded_action(self):
        body = self.client.get("/runs/acme-1/curve").json()

        self.assertEqual([p["mode"] for p in body["trace"]],
                         ["communicate", "inspect", "execute"])

    def test_every_point_can_be_traced_back_to_what_caused_it(self):
        """The thing a stroke-based curve cannot do: each point opens the script behind it."""
        executed = [p for p in self.client.get("/runs/acme-1/curve").json()["trace"]
                    if p["mode"] == "execute"][0]

        self.assertEqual(executed["execution"], "e1")
        self.assertIn("scripts/0001.js", executed["detail"])

    def test_both_readings_are_returned(self):
        """Counting actions and counting time disagree, sometimes in sign. Neither is the answer."""
        body = self.client.get("/runs/acme-1/curve").json()

        self.assertIn("net", body["summary"])
        self.assertIn("integral", body["summary"])
        self.assertIn("heldMs", body["summary"])
        for point in body["trace"]:
            self.assertIn("cumulative", point)
            self.assertIn("integral", point)

    def test_a_spine_only_run_says_what_it_could_not_see(self):
        """No agent.jsonl means no deliberation was recorded, and the page must not imply otherwise."""
        self.assertIn("wait", self.client.get("/runs/acme-1/curve").json()["summary"]["missing"])


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


class CurveScopeTests(unittest.TestCase):
    """The curve is a reading of *this run*, not of the project's whole history.

    A project's three event files are appended to across runs, exactly as `server.jsonl` is for the
    tailer. Reading them whole put an earlier run's work on this run's plot — and because the old
    points dominated the axes, the live ones arrived too small to see, so the curve looked frozen
    rather than wrong. That is the failure worth pinning: it is invisible on a project's first run
    and appears only on its second.
    """

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-curve-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

        self.project = project_dir(self.root, "acme")
        events = self.project / "events"

        # Yesterday's run and today's, in the one file, as a real project accumulates them.
        lines = [
            _event("2026-08-30T09:00:00.000Z", 1, "stage.begin", stage="Yesterday"),
            _event("2026-08-30T09:00:01.000Z", 2, "note", stage="Yesterday", message="an old note"),
            _event("2026-08-31T15:00:00.000Z", 3, "stage.begin", stage="Today"),
            _event("2026-08-31T15:00:01.000Z", 4, "note", stage="Today", message="a new note"),
        ]
        (events / "server.jsonl").write_text("".join(lines), encoding="utf-8")

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def _register(self, since: str) -> None:
        self.registry._runs["acme-1"] = Run(
            id="acme-1", project=_loaded(self.project), prompt="", stream=None,
            started="2026-08-31T15:00:00+00:00", since=since, status="done")

    def test_only_this_runs_events_are_coded(self):
        self._register("2026-08-31T14:59:59.000Z")
        trace = self.client.get("/runs/acme-1/curve").json()["trace"]

        self.assertEqual([p["stage"] for p in trace], ["Today", "Today"])

    def test_the_earlier_run_is_not_merely_ordered_last(self):
        """Off-by-one on the cut would keep the old points and only move them."""
        self._register("2026-08-31T14:59:59.000Z")
        body = self.client.get("/runs/acme-1/curve").json()

        self.assertEqual(body["summary"]["actions"], 2)
        self.assertNotIn("an old note", [p["detail"] for p in body["trace"]])

    def test_without_a_cut_the_whole_project_is_read(self):
        """The fallback a caller with no run gets, and what the bug looked like."""
        self._register("")
        trace = self.client.get("/runs/acme-1/curve").json()["trace"]

        self.assertEqual([p["stage"] for p in trace], ["Yesterday", "Yesterday", "Today", "Today"])

    def test_the_first_point_is_relative_to_this_run_not_the_project(self):
        """The x-axis starts when the run did; otherwise a resumed run opens hours along it."""
        self._register("2026-08-31T14:59:59.000Z")
        trace = self.client.get("/runs/acme-1/curve").json()["trace"]

        self.assertEqual(trace[0]["t"], 0)
        self.assertEqual(trace[-1]["t"], 1000)


def _event(ts: str, seq: int, kind: str, **fields) -> str:
    return json.dumps({"ts": ts, "seq": seq, "src": "server", "type": kind, **fields}) + "\n"


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
