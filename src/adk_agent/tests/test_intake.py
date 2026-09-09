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

import re
import sys
import tempfile
import time
import unittest
from unittest import mock
from html import escape, unescape
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


class ObservingOnlyBoundaryTests(unittest.TestCase):
    """What an observer may do to a mounted studio, now that one verb has changed sides.

    The blanket refusal was right when written — the studio's whole write side went through the
    Antigravity SDK, which this runtime does not ship. `adk_agent.interject` gives an ADK run its own
    channel, so speaking to a run needs no driver and is no longer driving.
    """

    def test_reading_is_always_allowed(self):
        self.assertTrue(intake.observing_only("GET", "/studio/runs/x", "/studio"))
        self.assertTrue(intake.observing_only("HEAD", "/studio/", "/studio"))

    def test_registering_a_watch_is_allowed(self):
        self.assertTrue(intake.observing_only("POST", "/studio/observe", "/studio"))

    def test_speaking_to_a_run_is_allowed(self):
        """The change. Without it the box 409s before `say` is ever reached."""
        self.assertTrue(intake.observing_only("POST", "/studio/runs/acme-watch-1/say", "/studio"))

    def test_answering_a_question_is_allowed(self):
        """**This assertion used to be the opposite, and both versions were right when written.**

        Answering settles a question the *host* asked, and while no host on this runtime could ask
        one, refusing here merely saved the request a second refusal at `ObservedRun.answer`. Now
        `adk_agent.ask` holds the future in this process, so the host is the page's own process and
        the refusal would strand the agent for its whole timeout with a click that reached nothing.
        """
        self.assertTrue(intake.observing_only("POST", "/studio/runs/acme-watch-1/answer", "/studio"))

    def test_a_path_that_merely_contains_answer_is_refused(self):
        """Anchored at both ends, as `say` is — a verb must not be a prefix onto something else."""
        for attempt in ("/studio/runs/x/answer/../../projects", "/studio/runs/x/answer/more",
                        "/studio/answering", "/studio/runs/answer"):
            self.assertFalse(intake.observing_only("POST", attempt, "/studio"), attempt)

    def test_creating_and_starting_are_still_refused(self):
        self.assertFalse(intake.observing_only("POST", "/studio/projects", "/studio"))
        self.assertFalse(intake.observing_only("POST", "/studio/runs", "/studio"))

    def test_a_path_that_merely_contains_say_is_refused(self):
        """Anchored at both ends, so `say` cannot be a prefix onto something else."""
        for attempt in ("/studio/runs/x/say/../../projects", "/studio/runs/x/say/more",
                        "/studio/saying", "/studio/runs/say"):
            self.assertFalse(intake.observing_only("POST", attempt, "/studio"), attempt)


class RestartWarningTests(unittest.TestCase):
    """What the form says about the failure a visitor cannot otherwise see coming.

    A Cloud Run instance can be replaced mid-run — it happened twice on 2026-09-08, the second time
    twenty minutes into a live run — and the run dies with it. That is not recoverable: the caps and
    the deadline are keyed on `invocation_id`, so resuming would reset the token budget and un-trip
    the breaker, and a run's own clock would report minutes for work that took an hour.

    So the honest thing is to say so *before* the twenty minutes are spent, which means here rather
    than on the run page.
    """

    def test_the_form_warns_that_a_restart_ends_a_run(self):
        self.assertIn("restarts while your project is running", intake.FORM)
        self.assertIn("start it again", intake.FORM)

    def test_it_says_the_work_survives_and_where_to_find_it(self):
        """A warning that only names the loss reads worse than the situation is.

        The mirror copies the project out while the run happens, so the work *is* kept — and the
        sentence has to carry that, or a visitor concludes a restart costs them everything.
        """
        self.assertIn("work it had already done is kept", intake.FORM)
        self.assertIn("Archived projects", intake.FORM)

    def test_the_warning_sits_with_the_control_that_commits_to_a_run(self):
        """Beside the start checkbox, not at the top: it is about to be acted on, not read past."""
        start = intake.FORM.index('name="start"')
        note = intake.FORM.index('class="note"')
        submit = intake.FORM.index("<button type=\"submit\"")

        self.assertLess(start, note, "the note should follow the start control")
        self.assertLess(note, submit, "and precede the button it is warning about")


class WorkflowTypeTests(unittest.TestCase):
    """The type control, and the table behind it.

    A type is a `type.<name>.md` file in the workflow's template, so this table mirrors a set that
    lives somewhere else. It cannot be read at runtime — the templates are embedded resources in
    `Polson.CLI.dll`, so a container has the DLL and no template tree — which leaves a hardcoded
    mirror, and a mirror drifts silently: a type added to the template simply never appears on the
    form. These compare the two.
    """

    TEMPLATES = Path(__file__).resolve().parents[3] / "src" / "Polson.CLI" / "ProjectTemplate"

    def test_each_workflow_offers_every_type_it_ships_but_the_declined_ones(self):
        """**A subset is allowed, an accidental subset is not.**

        The original assertion was equality, which was right while every offered workflow offered
        everything it shipped. `drawing` broke that deliberately: it ships `seed`, which reads the
        director's opening sketch from `seed/`, and this form stages an upload into `documents/` —
        so a visitor choosing it would get an agent looking at an empty directory.

        Loosening this to "offered is a subset" would have thrown away what the check is for, which
        is catching a type added upstream and never offered here. So a declined type must appear in
        `NOT_OFFERED` with its reason: considered and declined passes, forgotten still fails.
        """
        if not self.TEMPLATES.is_dir():
            self.skipTest("template tree not present; running outside a source checkout")

        for workflow, offered in intake.WORKFLOWS.items():
            with self.subTest(workflow=workflow):
                shipped = {f.name[len("type."):-len(".md")]
                           for f in (self.TEMPLATES / workflow).glob("type.*.md")}
                declined = {kind for (w, kind) in intake.NOT_OFFERED if w == workflow}

                self.assertEqual(sorted(shipped - declined), sorted(offered))
                self.assertTrue(declined <= shipped,
                                f"{workflow} declines a type it does not ship")

    def test_every_offered_workflow_fits_inside_the_request_timeout(self):
        """**Deadline + breaker grace + credited waiting must clear Cloud Run's 3600s ceiling.**

        Past it the platform kills the run mid-flight rather than the breaker halting it cleanly,
        which is the exact failure the breaker exists to replace. `drawing`'s craft default is 45 and
        the grace is 15, so it *is* the ceiling exactly before any waiting is credited — which is why
        this form sets its own allowance rather than taking the CLI's.
        """
        import studio

        ceiling = 3600.0
        for workflow in intake.WORKFLOWS:
            with self.subTest(workflow=workflow):
                minutes = intake.DEADLINES.get(workflow)
                self.assertIsNotNone(minutes, f"{workflow} would take the CLI default unchecked")

                worst = (minutes + studio.BREAKER_GRACE_MINUTES) * 60 + studio.MAX_CREDIT_SECONDS
                self.assertLess(worst, ceiling,
                                f"{workflow} can reach {worst:.0f}s against a {ceiling:.0f}s timeout")

    def test_every_declined_type_says_why(self):
        """The entry is the record. A blank reason is the same as having forgotten."""
        for (workflow, kind), why in intake.NOT_OFFERED.items():
            with self.subTest(workflow=workflow, kind=kind):
                self.assertIn(workflow, intake.WORKFLOWS)
                self.assertGreater(len(why.strip()), 20)

    def test_a_label_renames_a_workflow_on_the_form_but_not_in_the_request(self):
        """**The value must stay the template name.**

        `drawing_partner` is communication — *this one needs you at the keyboard* — and the label map
        is how that is said without renaming a workflow that `ProjectGenerator`, `studio/projects.py`
        and two test suites all refer to by name. If the label leaked into the option's value, the
        POST would refuse it as an unknown workflow.
        """
        page = TestClient(_app()).get("/new").text

        self.assertIn('<option value="drawing"', page)
        self.assertIn(">drawing_partner<", page)
        self.assertNotIn('value="drawing_partner"', page)

        for workflow in intake.LABELS:
            self.assertIn(workflow, intake.WORKFLOWS, "a label for a workflow nobody is offered")

    def test_all_types_is_the_union_of_every_offered_workflows_types(self):
        union = sorted({t for types in intake.WORKFLOWS.values() for t in types})
        self.assertEqual(union, list(intake.ALL_TYPES))


from fastapi.testclient import TestClient


def _app():
    """A bare app with the intake mounted, for the cases that only read the form."""
    from fastapi import FastAPI

    app = FastAPI()
    intake.mount(app)
    return app


class ArchivedNameTests(unittest.TestCase):
    """That a new run cannot be written over an archived one.

    **The CLI's own check does not reach this.** It refuses a non-empty project *directory*, which
    covers a collision with a project still on this container — but after a restart every earlier
    project lives only in the archive, and the directory is gone. `mirror` keys its prefix on the
    project's directory name, so a second run under an archived name sweeps into the first one's
    prefix: same `events/agent.jsonl`, same `brief.md`, same `artwork.js`. The two runs interleave
    and neither is readable afterwards.

    Easy to reach by accident, because the starters offer fixed names — clicking *Wet street, night*
    twice either side of a restart is enough.
    """

    def setUp(self) -> None:
        self.client = TestClient(_app())

    def make(self, name):
        return self.client.post("/projects", data={
            "name": name, "brief": "draw a street", "workflow": "drawing"})

    def test_a_name_already_in_the_archive_is_refused(self):
        with mock.patch.object(intake, "archived_names", return_value=["nightstreet", "earnout1"]):
            answer = self.make("nightstreet")

        self.assertEqual(409, answer.status_code, "409: the request is fine, the name is taken")
        self.assertIn("archived project", answer.text)
        self.assertIn("nightstreet", answer.text)

    def test_a_free_name_is_not_refused_by_this_check(self):
        """It must fail *later*, on something else — never here."""
        with mock.patch.object(intake, "archived_names", return_value=["earnout1"]):
            answer = self.make("brandnew")

        self.assertNotEqual(409, answer.status_code)

    def test_an_unreadable_archive_does_not_block_creation(self):
        """A guard that cannot read the archive must not also stop a visitor working.

        Losing the check degrades to the behaviour that existed before it; refusing every name
        because a bucket is unreachable takes the whole form down.
        """
        self.assertEqual([], intake.archived_names())

    def test_the_archive_is_reached_by_alias_never_by_a_bare_import(self):
        """**Two modules in this tree are called `studio`.**

        `main.py` loads the web layer as `polson_studio` for exactly that reason, and a bare
        `import studio.archive` from here finds this package's agent factory instead. The same
        mistake took the whole intake form down at import time once already.
        """
        import ast, inspect

        tree = ast.parse(inspect.getsource(intake.archived_names).lstrip())
        # The docstring names the mistake it is avoiding, so a plain substring search finds
        # "import studio" in the prose and fails a passing function. Read the code instead.
        code = ast.dump(ast.parse(ast.unparse(
            [n for n in ast.walk(tree) if isinstance(n, ast.FunctionDef)][0].body[1:])))

        self.assertIn("polson_studio.archive", code)
        self.assertNotIn("'studio'", code, "a bare `studio` import finds the agent factory")


class StarterTests(unittest.TestCase):
    """The one-click commissions on the form.

    They exist because a visitor handed a URL and an empty textarea has to invent a brief before
    they can see anything, and because the demo is aimed at advertising, film and television — so
    the subjects steer there while the field stays free.
    """

    def test_every_starter_is_a_commission_the_endpoint_would_accept(self):
        """**The failure this prevents is a chip that 400s in front of a judge.**

        A starter fills the same controls a person would, so an unoffered workflow or a type its
        workflow does not ship is refused by the POST handler exactly as a hand-typed one would be —
        and the chip would look broken rather than wrong.
        """
        for starter in intake.STARTERS:
            with self.subTest(starter=starter["label"]):
                self.assertIn(starter["workflow"], intake.WORKFLOWS)
                self.assertIn(starter["kind"], intake.WORKFLOWS[starter["workflow"]],
                              "the type control hides types the workflow does not offer")
                self.assertTrue(intake.VALID_APP_NAME.match(starter["name"]))
                self.assertLessEqual(len(starter["name"]), intake.MAX_NAME)
                self.assertTrue(starter["brief"].strip())

    def test_no_starter_hands_the_agent_a_figure(self):
        """**A brief carrying invented numbers is what the research apparatus exists to prevent.**

        A starter that supplied them would be teaching the agent to draw a sourced-looking graphic
        from nothing, which is the one failure a reader cannot detect — a wrong number renders
        perfectly. Each of these asks for something to be researched or read from a document.
        """
        money = re.compile(r"[$£€]\s?\d|\d+(\.\d+)?\s?(m|bn|million|billion|per ?cent|%)", re.I)
        for starter in intake.STARTERS:
            with self.subTest(starter=starter["label"]):
                self.assertIsNone(money.search(starter["brief"]),
                                  "state the question, not an answer nobody sourced")

    def test_a_workflow_offering_one_type_selects_it(self):
        """An em-dash the server quietly turns into `review` tells the visitor nothing.

        `ProjectGenerator.DefaultTypes` already maps drawing to review, so a blank submits fine —
        this is about the control showing what is going to happen.
        """
        single = [w for w, types in intake.WORKFLOWS.items() if len(types) == 1]
        self.assertTrue(single, "no single-type workflow to check")

        body = TestClient(_app()).get("/new").text
        self.assertIn("if (types.length === 1) kind.value = types[0];", body)

    def test_switching_workflow_cannot_leave_an_unoffered_type_selected(self):
        """**Hiding an option does not clear a value already on it.**

        Choosing drawing_partner (review) then switching to infographic left `review` selected on a
        hidden option — submitted, and refused by the server for a type the visitor could no longer
        see. Only reachable now that one workflow offers a type no other does.
        """
        body = TestClient(_app()).get("/new").text

        self.assertIn("if (kind.disabled || !types.includes(kind.value)) kind.value = '';", body)

    def test_a_drawing_starter_asks_for_nothing_the_workflow_refuses_to_draw(self):
        """`drawing`'s first non-negotiable is pencil and pen: no colour, no flats, no fills.

        A starter asking for a palette or a graded sky would commission something the workflow
        declines, and the visitor would read the refusal as the studio being broken.
        """
        banned = re.compile(r"\b(colour|color|palette|hue|gradient|wash|flats?|paint(ed|ing)?)\b", re.I)
        for starter in intake.STARTERS:
            if starter["workflow"] != "drawing":
                continue
            with self.subTest(starter=starter["label"]):
                self.assertIsNone(banned.search(starter["brief"]))

    def test_every_offered_workflow_has_at_least_one_starter(self):
        """A workflow with no starter is one a visitor has to invent a brief for, which is the
        friction the chips exist to remove — and the new one needs it most."""
        covered = {s["workflow"] for s in intake.STARTERS}
        self.assertEqual(covered, set(intake.WORKFLOWS))

    def test_the_names_are_distinct(self):
        """Two chips writing one name is a collision the second click discovers."""
        names = [s["name"] for s in intake.STARTERS]
        self.assertEqual(len(names), len(set(names)))

    def test_the_chips_reach_the_page_with_their_commission_attached(self):
        page = TestClient(_app()).get("/new").text

        self.assertNotIn("__STARTERS__", page, "the placeholder was not substituted")
        for starter in intake.STARTERS:
            with self.subTest(starter=starter["label"]):
                # Escaped, so an apostrophe in a label arrives as `&#x27;` rather than verbatim.
                self.assertIn(escape(starter["label"]), page)
                self.assertIn(f'data-workflow="{starter["workflow"]}"', page)

    def test_a_brief_carrying_a_quotation_mark_does_not_end_its_attribute(self):
        """Prose acquires quotes the moment anyone edits one, and an unescaped quote in an attribute
        ends the attribute — filling half a brief, which is worse than not working at all.
        """
        awkward = ({"label": "Quote & <script>", "name": "quoted", "workflow": "infographic",
                    "kind": "swiss", "brief": 'A "platform" release & what it costs'},)
        with mock.patch.object(intake, "STARTERS", awkward):
            page = TestClient(_app()).get("/new").text

        # Scoped to the chips: the page has a `<script>` of its own, so asserting on the whole
        # document would pass for the wrong reason today and fail for the wrong reason tomorrow.
        chips = page.split('class="chips"')[1].split("</div>")[0]
        self.assertIn("&lt;script&gt;", chips, "the label is escaped")
        self.assertNotIn("<script>", chips)

        # `quoteattr` switches to single-quote delimiters rather than emitting `&quot;`, which is
        # equally valid and is why the escaping is delegated rather than hand-rolled: the two cases
        # a hand-rolled version gets wrong are a value containing one kind of quote and a value
        # containing both.
        self.assertIn("""data-brief='A "platform" release &amp; what it costs'""", page)

        found = re.search(r"data-brief=(\"[^\"]*\"|'[^']*')", page)
        self.assertIsNotNone(found)
        self.assertEqual(unescape(found.group(1)[1:-1]), 'A "platform" release & what it costs',
                         "what the browser reads back is what the starter said")

    def test_the_form_offers_a_way_back_to_the_studio(self):
        """**And to `/studio/`, not `/`.**

        The root of this app is ADK's own dev UI, so a back link pointing there drops a visitor
        somewhere that looks like the studio having vanished. The run page's own link had exactly
        that bug and was fixed; this is the same trap one page over.
        """
        page = TestClient(_app()).get("/new").text

        self.assertIn('href="/studio/"', page)
        self.assertIn("projects", page.split("<h1>")[0], "the link sits above the heading")

    def test_the_chips_do_not_submit_the_form(self):
        """They fill it. One button on this page commissions a run and the rest must not look
        like it, or a visitor exploring the starters starts five.
        """
        page = TestClient(_app()).get("/new").text
        chips = "".join(block.split("</div>")[0] for block in page.split('class="chips"')[1:])

        self.assertEqual(chips.count('type="button"'), len(intake.STARTERS))
        self.assertNotIn("submit", chips)

    def test_the_starters_are_grouped_by_workflow(self):
        """Thirteen chips in one row reads as a wall, and hides the distinction that matters most:
        one of these sets makes a graphic on its own, the other needs somebody at the keyboard.
        """
        page = TestClient(_app()).get("/new").text

        groups = [w for w in intake.WORKFLOWS if any(e["workflow"] == w for e in intake.STARTERS)]
        self.assertEqual(page.count('class="chips"'), len(groups))
        for workflow in groups:
            with self.subTest(workflow=workflow):
                self.assertIn(f'<p class="group">{escape(intake.LABELS.get(workflow, workflow))}</p>',
                              page)


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
        script reads an empty list and disables a control the workflow does in fact offer.

        Matched per attribute rather than as one exact string: the previous version asserted the
        option's whole opening tag, so adding `data-deadline` beside it failed a test about types.
        """
        body = self.client.get("/new").text

        for workflow, types in intake.WORKFLOWS.items():
            with self.subTest(workflow=workflow):
                tag = re.search(rf'<option value="{workflow}"[^>]*>', body)
                self.assertIsNotNone(tag, f"{workflow} has no option")
                self.assertIn(f'data-types="{",".join(types)}"', tag.group(0))

    def test_the_deadline_field_is_bounded_by_what_the_host_can_finish(self):
        body = self.client.get("/new").text

        self.assertIn(f'min="{intake.MIN_DEADLINE_MINUTES}"', body)
        self.assertIn(f'max="{intake.MAX_DEADLINE_MINUTES}"', body)

        # Said as well as enforced. A ceiling a visitor can only discover by being refused is a
        # ceiling they will meet at the worst moment — and both numbers come from the constants, so
        # the sentence cannot drift from the attribute it describes.
        label = body.split("<label>Deadline")[1].split("</label>")[0]
        self.assertIn(str(intake.MIN_DEADLINE_MINUTES), label)
        self.assertIn(str(intake.MAX_DEADLINE_MINUTES), label)
        for workflow, minutes in intake.DEADLINES.items():
            with self.subTest(workflow=workflow):
                tag = re.search(rf'<option value="{workflow}"[^>]*>', body)
                self.assertIn(f'data-deadline="{minutes}"', tag.group(0),
                              "the placeholder shows the workflow's own allowance")

    def test_a_deadline_past_the_ceiling_is_refused_with_the_reason(self):
        """**The input's own `max` is a convenience, not a check** — this endpoint is reachable
        without a browser, and the ceiling is what keeps a run halted cleanly by the breaker rather
        than cut off mid-drawing by the platform.
        """
        answer = self.client.post("/projects", data={
            "name": "toolong", "brief": "draw it", "workflow": "drawing",
            "deadline": str(intake.MAX_DEADLINE_MINUTES + 1)})

        self.assertEqual(answer.status_code, 400)
        self.assertIn(str(intake.MAX_DEADLINE_MINUTES), answer.text)

    def test_a_deadline_below_the_floor_is_refused(self):
        answer = self.client.post("/projects", data={
            "name": "tooshort", "brief": "draw it", "workflow": "drawing", "deadline": "0"})

        self.assertEqual(answer.status_code, 400)

    def test_a_deadline_that_is_not_a_number_is_refused_rather_than_ignored(self):
        """Silently falling back to the default would give a visitor a run they did not ask for."""
        answer = self.client.post("/projects", data={
            "name": "notanum", "brief": "draw it", "workflow": "drawing", "deadline": "half an hour"})

        self.assertEqual(answer.status_code, 400)
        self.assertIn("not a number", answer.text)

    def test_the_ceiling_is_derived_and_does_not_land_on_the_timeout(self):
        """**Writing this caught a real off-by-a-margin.**

        Deriving the maximum from the grace and the credit alone gave exactly 40 minutes, and
        40 + 15 + 5 is precisely 3600s — the breaker's last model call and the platform's cutoff at
        the same instant, which is the fault `drawing`'s 45-minute default has. A run still has to
        encode its final render, write its deliverables and let the mirror sweep after the breaker
        fires, so the margin is not decoration.
        """
        import studio

        worst = (intake.MAX_DEADLINE_MINUTES * 60 + studio.MAX_CREDIT_SECONDS
                 + studio.BREAKER_GRACE_MINUTES * 60)
        self.assertLessEqual(worst, intake.REQUEST_TIMEOUT_SECONDS - intake.DEADLINE_MARGIN_SECONDS)

        # And derived, not typed: a hardcoded number would be right today and quietly wrong the
        # first time the grace or the credit is tuned.
        spare = (intake.REQUEST_TIMEOUT_SECONDS - studio.MAX_CREDIT_SECONDS
                 - studio.BREAKER_GRACE_MINUTES * 60 - intake.DEADLINE_MARGIN_SECONDS)
        self.assertEqual(intake.MAX_DEADLINE_MINUTES, int(spare // 60))

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
        """Each of these needs the Antigravity driver, which this runtime does not ship.

        **`/say` and `/answer` were both on this list and have moved off it**, which is a change of
        fact rather than of policy: `adk_agent.interject` and `adk_agent.ask` give an ADK run both
        directions of the director's conversation in this process, so neither needs a driver any
        more. Creating a project and starting a run still do. See `ObservingOnlyBoundaryTests`.
        """
        for path in ("/studio/projects", "/studio/runs"):
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
