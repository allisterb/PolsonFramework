"""Taking a file from a stranger.

`/projects` is the one endpoint on the hosted URL that accepts bytes from someone who is not the
operator, so its whole job is to be suspicious. Two of its three inputs are attacker-controlled in a
way that is easy to miss:

- **`UploadFile.filename` is a multipart header**, not a fact about a file. Nothing between the
  client and here validates it, and `folder / name` honours an absolute path or a `..` segment
  exactly as written.
- **`content-length` is also a header.** Checking it instead of counting bytes lets a visitor spend
  the server's memory and be refused afterwards, which is the wrong order.

Most of what follows is the filename, because that is where a traversal would go and because the
correct answer is unobvious in both directions: a POSIX server must still reject a *Windows* path,
since the attacker chooses the separator rather than the host.

No network, no ADK, no model - these run in milliseconds and can be run on every change.
"""

from __future__ import annotations

import sys
import tempfile
import time
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from fastapi import HTTPException

import intake
import studio
import newproject


class _Upload:
    """The two members `stage_upload` uses, so the test needs no multipart machinery."""

    def __init__(self, filename: str, data: bytes):
        self.filename = filename
        self._data = data
        self._at = 0

    async def read(self, size: int) -> bytes:
        chunk = self._data[self._at:self._at + size]
        self._at += len(chunk)
        return chunk


class FilenameTests(unittest.TestCase):
    """Every route out of the directory, closed."""

    def test_a_path_is_reduced_to_its_basename_or_refused(self):
        for raw in ("../../etc/passwd.pdf", "..\\..\\windows\\system32\\config.pdf",
                    "/etc/passwd.pdf", "C:\\Windows\\notes.pdf", "folder/nested.pdf"):
            with self.subTest(raw=raw):
                try:
                    name = intake.safe_filename(raw)
                except HTTPException:
                    continue                       # refusing outright is also correct
                self.assertNotIn("/", name)
                self.assertNotIn("\\", name)
                self.assertFalse(name.startswith(".."))
                self.assertEqual(Path(name).name, name)

    def test_a_name_that_is_not_a_name_is_refused(self):
        for raw in ("", "   ", None, ".", "..", "..."):
            with self.subTest(raw=raw), self.assertRaises(HTTPException):
                intake.safe_filename(raw)

    def test_a_trailing_dot_cannot_smuggle_a_type_past_the_check(self):
        """Windows drops a trailing dot, so the checked name must be the name that lands.

        `returns.pdf.` read literally has suffix `.` and would fail the type check; read the way the
        filesystem will read it, it is a PDF. Stripping first makes the two agree.
        """
        self.assertEqual(intake.safe_filename("returns.pdf."), "returns.pdf")
        self.assertEqual(intake.safe_filename("returns.pdf   "), "returns.pdf")

    def test_a_type_the_studio_cannot_read_is_refused(self):
        """Offering a file the agent can see and cannot open is worse than refusing at the door."""
        for raw in ("report.exe", "script.js", "archive.zip", "notes.docx", "page.svg"):
            with self.subTest(raw=raw), self.assertRaises(HTTPException):
                intake.safe_filename(raw)

    def test_an_ordinary_name_survives(self):
        for raw in ("returns.pdf", "box-office 2025.csv", "notes.md",
                    "data.json", "chart.png", "photo.jpeg"):
            with self.subTest(raw=raw):
                self.assertEqual(intake.safe_filename(raw), raw)

    def test_a_name_carrying_a_control_or_separator_character_is_refused(self):
        for raw in ("report:stream.pdf", "re\x00port.pdf", "rep\nort.pdf"):
            with self.subTest(raw=repr(raw)), self.assertRaises(HTTPException):
                intake.safe_filename(raw)


class UploadTests(unittest.IsolatedAsyncioTestCase):
    """What arrives, and what is refused while it is arriving."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp(prefix="polson-intake-"))

    def tearDown(self):
        for path in self.dir.iterdir():
            path.unlink(missing_ok=True)
        self.dir.rmdir()

    async def test_an_upload_keeps_its_own_name(self):
        """The name a visitor uploaded is the name the project gets.

        The regression this pins: an earlier version wrote to a `NamedTemporaryFile` and returned
        that path, so `stage_documents` copied it in as `tmpi7tswuc1.csv`. The sanitised name had
        been computed, checked, and then not used — and that name is what `Documents.list()` reports
        to the agent and what a director reading the brief would fail to recognise.
        """
        staged = await intake.stage_upload(_Upload("returns.csv", b"a,b\n1,2\n"), self.dir)

        self.assertEqual(staged.name, "returns.csv")
        self.assertEqual(staged.parent, self.dir)
        self.assertEqual(staged.read_bytes(), b"a,b\n1,2\n")

    async def test_a_traversing_name_is_written_inside_the_directory(self):
        """The sanitised name is what lands, so the write itself cannot escape."""
        staged = await intake.stage_upload(_Upload("../../escape.csv", b"x\n"), self.dir)

        self.assertEqual(staged.name, "escape.csv")
        self.assertEqual(staged.resolve().parent, self.dir.resolve())

    async def test_an_oversized_upload_is_refused_while_it_streams(self):
        """Counted as it arrives, not measured afterwards.

        The distinction is the attack: reading the whole body first and checking the length after
        lets a visitor exhaust memory before the refusal, so the cap has to bite mid-stream. The
        partial file must not survive either — one left behind is one the project could find.
        """
        original, intake.MAX_UPLOAD_BYTES = intake.MAX_UPLOAD_BYTES, 1024
        try:
            before = set(Path(tempfile.gettempdir()).glob("*.pdf"))
            with self.assertRaises(HTTPException) as caught:
                await intake.stage_upload(_Upload("big.pdf", b"x" * 4096), self.dir)
            self.assertEqual(caught.exception.status_code, 413)
            self.assertEqual(before, set(Path(tempfile.gettempdir()).glob("*.pdf")))
        finally:
            intake.MAX_UPLOAD_BYTES = original

    async def test_an_empty_upload_is_refused(self):
        """A zero-byte document reads as a document with nothing in it, which is worse than none."""
        with self.assertRaises(HTTPException):
            await intake.stage_upload(_Upload("empty.pdf", b""), self.dir)


class ConfigurationTests(unittest.TestCase):
    def test_the_upload_cap_is_below_what_the_reader_accepts(self):
        """Refusing at the door beats accepting a file `Documents.ask` will then refuse.

        18 MB is `Documents.MaxInlineBytes` on the .NET side. A visitor who waits through an upload
        only to be told the studio cannot read it has been failed twice.
        """
        self.assertLess(intake.MAX_UPLOAD_BYTES, 18 * 1024 * 1024)

    def test_only_workflows_that_look_for_documents_are_offered(self):
        """A workflow whose instructions never mention documents would ignore the upload.

        This is the drift guard: the form is one place and the instructions are another, and the
        failure it prevents is silent — an upload staged into a project whose agent never looks.
        """
        templates = (Path(__file__).resolve().parents[3]
                     / "src" / "Polson.CLI" / "ProjectTemplate")

        for workflow in intake.WORKFLOWS:
            with self.subTest(workflow=workflow):
                body = (templates / workflow / "instructions.md").read_text(encoding="utf-8")
                self.assertIn("Documents.list()", body,
                              f"{workflow} never tells the agent to look for a document")


class WorkflowTypeTests(unittest.TestCase):
    """The type control, and the table behind it.

    A type is a `type.<name>.md` file in the workflow's template, so this table mirrors a set that
    lives somewhere else. It cannot be read at runtime — the templates are embedded resources in
    `Polson.CLI.dll`, so a container has the DLL and no template tree — which leaves a hardcoded
    mirror, and a mirror drifts silently: a type added to the template simply never appears on the
    form. These compare the two.
    """

    TEMPLATES = Path(__file__).resolve().parents[3] / "src" / "Polson.CLI" / "ProjectTemplate"

    def test_each_workflow_offers_exactly_the_types_its_template_ships(self):
        if not self.TEMPLATES.is_dir():
            self.skipTest("template tree not present; running outside a source checkout")

        for workflow, offered in intake.WORKFLOWS.items():
            with self.subTest(workflow=workflow):
                shipped = sorted(f.name[len("type."):-len(".md")]
                                 for f in (self.TEMPLATES / workflow).glob("type.*.md"))
                self.assertEqual(shipped, sorted(offered))

    def test_all_types_is_the_union_of_every_offered_workflows_types(self):
        union = sorted({t for types in intake.WORKFLOWS.values() for t in types})
        self.assertEqual(union, list(intake.ALL_TYPES))


class TypeSelectionTests(unittest.TestCase):
    """What the form renders, and what the endpoint accepts.

    The POST cases stop inside the handler's own checks, before anything is generated, so nothing
    here spawns the CLI or writes a project.
    """

    def setUp(self) -> None:
        from fastapi import FastAPI
        from fastapi.testclient import TestClient

        app = FastAPI()
        intake.mount(app)
        self.client = TestClient(app)

    def test_the_form_carries_a_type_control(self):
        body = self.client.get("/new").text

        self.assertIn('id="kind"', body)
        self.assertIn('name="kind"', body)
        for kind in intake.ALL_TYPES:
            self.assertIn(f'<option value="{kind}">{kind}</option>', body)

    def test_every_workflow_option_declares_the_types_it_offers(self):
        """`data-types` is what lets the control narrow itself without a round trip. Without it the
        script reads an empty list and disables a control the workflow does in fact offer."""
        body = self.client.get("/new").text

        for workflow, types in intake.WORKFLOWS.items():
            self.assertIn(f'<option value="{workflow}" data-types="{",".join(types)}">', body)

    def test_a_type_the_workflow_does_not_offer_is_refused(self):
        answer = self.client.post("/projects", data={
            "name": "typecheck", "brief": "chart it", "workflow": "vector_infographic",
            "kind": "antique"})

        self.assertEqual(400, answer.status_code)
        self.assertIn("antique", answer.text)
        self.assertIn("blueprint", answer.text, "the refusal should say what is on offer")

    def test_no_type_is_a_legitimate_answer(self):
        """The control is optional, so an empty one must pass rather than being read as a bad type.

        Sent with an empty *brief*, which is the check immediately after this one — so a 400 naming
        the brief proves the type check was reached and let the blank through. Asserting on some
        earlier failure would prove nothing, and nothing is generated either way.
        """
        answer = self.client.post("/projects", data={
            "name": "typecheck", "brief": "   ", "workflow": "vector_infographic", "kind": ""})

        self.assertEqual(400, answer.status_code)
        self.assertIn("brief is required", answer.text)
        self.assertNotIn("does not offer a type", answer.text)



    def test_the_chosen_type_reaches_the_generator(self):
        """The end of the wire. Everything above proves the control renders and validates; this is
        the only check that the value actually arrives, and a wrong keyword here would be silent —
        the project would generate perfectly, in the workflow's default direction."""
        seen = {}

        def fake_create(name, **kwargs):
            seen.update(kwargs, name=name)
            raise intake.GenerateError("stopped before anything was written")

        original, intake.create = intake.create, fake_create
        self.addCleanup(setattr, intake, "create", original)

        self.client.post("/projects", data={
            "name": "typecheck", "brief": "chart it", "workflow": "vector_infographic",
            "kind": "swiss"})

        self.assertEqual("swiss", seen.get("type_"))
        self.assertEqual("vector_infographic", seen.get("workflow"))

    def test_a_blank_type_reaches_the_generator_as_none(self):
        """Not as `''`. An empty string would become `--type ''` on the command line, which names a
        `type..md` that does not exist."""
        seen = {}

        def fake_create(name, **kwargs):
            seen.update(kwargs, name=name)
            raise intake.GenerateError("stopped before anything was written")

        original, intake.create = intake.create, fake_create
        self.addCleanup(setattr, intake, "create", original)

        self.client.post("/projects", data={
            "name": "typecheck", "brief": "chart it", "workflow": "vector_infographic", "kind": ""})

        self.assertIsNone(seen.get("type_", "missing"))

class ThoughtSummaryTests(unittest.TestCase):
    """That the model is asked for the thinking it is already being billed for.

    Gemini returns a thought *part* only when summaries are requested. Without that the model
    reasons, `thoughts_token_count` climbs, and `part.thought` is never true — so the transcript's
    `thinking` branch cannot fire and the record holds none. Measured on the kubrick8 run: 771
    thinking tokens in one turn, zero thinking events across all forty.
    """

    def test_every_agent_asks_for_thought_summaries(self):
        config = studio._retry_config()

        self.assertIsNotNone(config.thinking_config, "thinking is billed either way")
        self.assertTrue(config.thinking_config.include_thoughts)

    def test_the_thinking_budget_is_left_alone(self):
        """Asking to *see* the reasoning must not change how much of it happens. A budget set here
        would silently re-tune every agent while looking like a logging change."""
        self.assertIsNone(studio._retry_config().thinking_config.thinking_budget)

    def test_the_retry_options_survived(self):
        """The config had one job before this and still has it."""
        retry = studio._retry_config().http_options.retry_options

        self.assertIn(429, retry.http_status_codes)
        self.assertEqual(studio.RETRY_ATTEMPTS, retry.attempts)


class SessionUserTests(unittest.TestCase):
    """The id sessions are created under, which decides whether ADK's own console can see them."""

    def test_sessions_are_created_under_the_id_the_dev_ui_reads(self):
        """ADK's console requests `/apps/<app>/users/user/sessions` and offers no way to change it,
        so any other id shows an empty session list for a run that is happily going."""
        self.assertEqual("user", intake.USER)


class SpendCapTests(unittest.TestCase):
    """The brake on a public URL.

    Every per-run budget bounds *one* run — `POLSON_BUDGET_TOKENS`, `POLSON_BUDGET_MINUTES`, the
    circuit breaker. None of them bounds how many runs a stranger starts, which on an
    `--allow-unauthenticated` service is an open door onto a metered model and a metered image
    service. This is that bound, and it is a spend brake rather than a security control: global
    rather than per visitor, and defeated by restarting the container.
    """

    def setUp(self) -> None:
        self.runs, self.day = intake.MAX_CONCURRENT_RUNS, intake.MAX_RUNS_PER_DAY
        self.addCleanup(setattr, intake, "MAX_CONCURRENT_RUNS", self.runs)
        self.addCleanup(setattr, intake, "MAX_RUNS_PER_DAY", self.day)
        intake._started.clear()
        self.addCleanup(intake._started.clear)
        intake._running.clear()
        self.addCleanup(intake._running.clear)

    def test_an_idle_host_starts_a_run(self):
        self.assertIsNone(intake._too_many())

    def test_the_day_is_a_rolling_window_not_a_calendar_one(self):
        """Yesterday's runs must not hold today's allowance down, and an hour ago's must still count.

        A calendar day would let a visitor spend the whole allowance at 23:59 and the whole of it
        again at 00:01, which is the failure a daily cap exists to prevent.
        """
        intake.MAX_RUNS_PER_DAY = 3
        intake._started.extend([time.time() - 90_000] * 5)      # yesterday — expired
        self.assertIsNone(intake._too_many())

        intake._started.extend([time.time() - 3_600] * 3)       # an hour ago — still counts
        self.assertIsNotNone(intake._too_many())

    def test_the_refusal_says_it_is_deliberate(self):
        intake.MAX_RUNS_PER_DAY = 1
        intake._started.append(time.time())

        full = intake._too_many()
        self.assertIn("real money", full)
        self.assertIn("still readable", full, "a refused visitor should be told what they can do")

    def test_expired_entries_are_dropped_rather_than_accumulating(self):
        intake.MAX_RUNS_PER_DAY = 5
        intake._started.extend([time.time() - 90_000] * 4)
        intake._too_many()

        self.assertEqual(0, len(intake._started))

    def test_runs_in_flight_are_capped_separately_from_the_day(self):
        """One instance serving several visitors at once is contention, not throughput."""
        intake.MAX_CONCURRENT_RUNS, intake.MAX_RUNS_PER_DAY = 1, 100
        intake._running.add(object())

        self.assertIn("already going", intake._too_many())

    def test_a_cap_of_zero_is_no_cap(self):
        """So a local developer, or a deployment that wants none, can turn it off outright."""
        intake.MAX_CONCURRENT_RUNS, intake.MAX_RUNS_PER_DAY = 0, 0
        intake._running.update({object(), object(), object()})
        intake._started.extend([time.time()] * 500)

        self.assertIsNone(intake._too_many())


class CapEnforcementTests(unittest.TestCase):
    """That the check is on the door, and that it refuses before it spends."""

    def setUp(self) -> None:
        from fastapi import FastAPI
        from fastapi.testclient import TestClient

        app = FastAPI()
        intake.mount(app)
        self.client = TestClient(app)
        self.day = intake.MAX_RUNS_PER_DAY
        self.addCleanup(setattr, intake, "MAX_RUNS_PER_DAY", self.day)
        intake._started.clear()
        self.addCleanup(intake._started.clear)

    def test_a_commission_past_the_cap_is_refused_with_429(self):
        intake.MAX_RUNS_PER_DAY = 1
        intake._started.append(time.time())

        answer = self.client.post("/projects", data={
            "name": "capped", "brief": "chart it", "workflow": "vector_infographic", "start": "1"})

        self.assertEqual(429, answer.status_code,
                         "429 says 'later', where 400 would say the commission was wrong")

    def test_nothing_is_created_when_the_cap_refuses(self):
        """Checked before the project is written and before the upload is staged, so a refusal costs
        nothing and leaves no half-made project behind."""
        intake.MAX_RUNS_PER_DAY = 1
        intake._started.append(time.time())

        called = []
        original, intake.create = intake.create, lambda *a, **k: called.append(k)
        self.addCleanup(setattr, intake, "create", original)

        self.client.post("/projects", data={
            "name": "capped", "brief": "chart it", "workflow": "vector_infographic", "start": "1"})

        self.assertEqual([], called, "the generator ran for a commission that was refused")

    def test_creating_without_starting_is_not_capped(self):
        """The cap is on *spending*, and an unstarted project spends nothing. Refused later for a
        reason of its own, which is past this check."""
        intake.MAX_RUNS_PER_DAY = 1
        intake._started.append(time.time())

        answer = self.client.post("/projects", data={
            "name": "capped", "brief": "   ", "workflow": "vector_infographic"})

        self.assertEqual(400, answer.status_code)
        self.assertIn("brief is required", answer.text)


if __name__ == "__main__":
    unittest.main()


class ProjectNameTests(unittest.TestCase):
    """What a project may be called, which is an ADK constraint rather than a form preference."""

    def test_a_dash_is_refused_because_adk_will_not_run_it(self):
        """The defect this exists for, found by a director uploading a real file.

        `intake` kept its own copy of the name rule, described in a comment as mirroring
        `newproject.VALID_APP_NAME`. It did not mirror it: the copy allowed a dash. ADK requires an
        app name to be a Python identifier and — the part that made this expensive — enforces it
        when the agent is **run**, not when it is loaded. So `boxoffice-2` was accepted by the form,
        had the visitor's PDF staged into it, appeared in `/list-apps`, opened in the console,
        accepted a session, and answered the first message with a 404.
        """
        self.assertIsNone(newproject.VALID_APP_NAME.match("boxoffice-2"))
        self.assertIsNone(newproject.VALID_APP_NAME.match("box-office"))

    def test_a_dot_is_refused_for_the_same_reason(self):
        """The dot was already excluded, correctly. Kept as a test so the pair stay together."""
        self.assertIsNone(newproject.VALID_APP_NAME.match("box.office"))

    def test_ordinary_names_survive(self):
        for name in ("boxoffice2", "box_office", "Kubrick3", "a"):
            with self.subTest(name=name):
                self.assertIsNotNone(newproject.VALID_APP_NAME.match(name))

    def test_a_name_must_start_with_a_letter(self):
        for name in ("2boxoffice", "_private", ""):
            with self.subTest(name=name):
                self.assertIsNone(newproject.VALID_APP_NAME.match(name))

    def test_the_form_and_the_app_writer_share_one_rule(self):
        """The regression guard proper.

        Two copies of a rule is how this happened, so what is asserted is that there is **one** —
        `intake` must be reading `newproject`'s pattern object, not a pattern of its own that
        happens to agree today.
        """
        self.assertIs(intake.VALID_APP_NAME, newproject.VALID_APP_NAME)
        self.assertFalse(hasattr(intake, "VALID_NAME"),
                         "intake should no longer carry its own name rule")


class ObservingOnlyTests(unittest.TestCase):
    """What the mounted studio may do here, and the path subtlety that decides it."""

    def test_a_watch_may_be_registered(self):
        """The one mutation reading needs, and the case the first version broke.

        Middleware on a mounted app sees `/studio/observe` with `root_path` of `/studio`, not the
        `/observe` the route sees. Comparing the raw path refused exactly the request this boundary
        exists to permit — caught by trying it against a live server rather than by reasoning.
        """
        self.assertTrue(intake.observing_only("POST", "/studio/observe", "/studio"))
        self.assertTrue(intake.observing_only("POST", "/observe", ""))
        self.assertTrue(intake.observing_only("POST", "/studio/observe/", "/studio"))

    def test_creating_or_driving_is_refused(self):
        """Each of these needs the Antigravity driver, which this runtime does not ship."""
        for path in ("/studio/projects", "/studio/runs", "/studio/runs/x-1/answer",
                     "/studio/runs/x-1/say"):
            with self.subTest(path=path):
                self.assertFalse(intake.observing_only("POST", path, "/studio"))

    def test_reading_is_always_allowed(self):
        for method in ("GET", "HEAD"):
            for path in ("/studio/", "/studio/runs/x-1", "/studio/runs/x-1/curve"):
                with self.subTest(method=method, path=path):
                    self.assertTrue(intake.observing_only(method, path, "/studio"))

    def test_a_path_that_merely_starts_with_observe_is_not_a_watch(self):
        """`/observed` is not `/observe`, and a prefix test would have said it was."""
        self.assertFalse(intake.observing_only("POST", "/studio/observed", "/studio"))
        self.assertFalse(intake.observing_only("POST", "/studio/observe/x", "/studio"))


class OpeningMessageTests(unittest.TestCase):
    """Creating a project is not starting one, and the form now does both."""

    def test_the_opening_message_points_at_the_brief_rather_than_restating_it(self):
        """The director's words reach the agent by reference, not by being copied into a prompt.

        That boundary is the whole reason `brief.md` is a file: instructions we wrote on one side,
        text a stranger typed on the other. An opening message carrying the brief inline would erase
        it.
        """
        self.assertIn("brief.md", intake.OPENING)
        self.assertNotIn("{", intake.OPENING, "the opening must not interpolate anything")
