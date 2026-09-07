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
