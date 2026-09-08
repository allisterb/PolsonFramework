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
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from fastapi import HTTPException

import intake
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
