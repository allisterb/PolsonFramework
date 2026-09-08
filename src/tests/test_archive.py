"""Bringing a vanished run back, and refusing to bring back anything else.

Nothing here talks to Google Cloud Storage. `archive` builds its client inside `_bucket()`, so these
substitute a fake bucket and assert on what was asked of it.

**The tests that matter are the refusals.** A blob key becomes a filesystem path, and unlike a URL
path it never passed through a router — so `restore` is the one place in the studio where a string
from outside becomes a file on disk with no framework between the two.
"""

from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from fastapi.testclient import TestClient

from studio import app as app_mod
from studio import archive as archive_mod
from studio.runs import Registry


class FakeBlob:
    def __init__(self, name: str, body: bytes = b"x"):
        self.name, self.body = name, body
        self.size = len(body)

    def download_to_filename(self, path):
        Path(path).write_bytes(self.body)


class FakeListing(list):
    """What `list_blobs` returns: an iterable that only fills `prefixes` once consumed."""

    def __init__(self, blobs, prefixes=()):
        super().__init__(blobs)
        self._prefixes = set(prefixes)
        self.consumed = False
        self.prefixes: set[str] = set()

    def __iter__(self):
        self.consumed = True
        self.prefixes = set(self._prefixes)
        return super().__iter__()


class FakeBucket:
    def __init__(self, blobs=(), prefixes=()):
        self.blobs = list(blobs)
        self.prefixes = list(prefixes)
        self.client = self

    def list_blobs(self, bucket, prefix="", delimiter=None):
        if delimiter:
            return FakeListing([], [p for p in self.prefixes if p.startswith(prefix)])
        return FakeListing([b for b in self.blobs if b.name.startswith(prefix)])


def with_archive(bucket, uri="gs://polson-artifacts/mirror"):
    """Patches the module so `_bucket()` answers with the fake, without a client anywhere."""
    return mock.patch.multiple(archive_mod,
                               ARCHIVE_URI=uri,
                               _bucket=lambda: (bucket, uri.split("/", 3)[3] if uri.count("/") > 2 else ""))


class ArchiveListingTests(unittest.TestCase):
    """What is on offer, and what is quietly not."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-archive-"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def test_names_are_read_from_the_folder_prefixes(self):
        bucket = FakeBucket(prefixes=["mirror/kubrick1/", "mirror/apollo/"])
        with with_archive(bucket):
            self.assertEqual(archive_mod.names(), ["apollo", "kubrick1"])

    def test_the_listing_is_consumed_before_prefixes_is_read(self):
        """The API gotcha this would otherwise hit.

        `prefixes` is empty until the iterator has been walked, so reading it first returns nothing
        and is indistinguishable from an empty archive — a bug that would present as "recovery does
        not work" rather than as an error.
        """
        bucket = FakeBucket(prefixes=["mirror/kubrick1/"])
        with with_archive(bucket):
            self.assertEqual(archive_mod.names(), ["kubrick1"])

    def test_a_name_that_is_not_a_safe_segment_is_not_offered(self):
        """A blob prefix becomes a directory name; anything path-shaped is dropped, not sanitised."""
        bucket = FakeBucket(prefixes=["mirror/../escape/", "mirror/ok/", "mirror/a b/"])
        with with_archive(bucket):
            self.assertEqual(archive_mod.names(), ["ok"])

    def test_only_projects_missing_from_disk_are_offered(self):
        """A project still here needs no restoring, and offering it invites overwriting a live run."""
        (self.root / "here").mkdir()
        (self.root / "here" / "project.json").write_text("{}", encoding="utf-8")
        bucket = FakeBucket(prefixes=["mirror/here/", "mirror/gone/"])
        with with_archive(bucket):
            self.assertEqual(archive_mod.restorable(self.root), ["gone"])

    def test_no_uri_means_nothing_is_offered_and_nothing_is_asked(self):
        """A local checkout with no bucket renders the index exactly as it did before."""
        with mock.patch.object(archive_mod, "ARCHIVE_URI", ""):
            self.assertFalse(archive_mod.available())
            self.assertEqual(archive_mod.restorable(self.root), [])

    def test_an_unreachable_archive_is_not_an_exception(self):
        """A studio that will not start because an *archive* is unreachable is the wrong trade."""
        with mock.patch.object(archive_mod, "ARCHIVE_URI", "gs://nope"), \
             mock.patch.object(archive_mod, "_bucket", side_effect=RuntimeError("no creds")):
            self.assertFalse(archive_mod.available())
            self.assertEqual(archive_mod.names(), [])


class ArchiveRestoreTests(unittest.TestCase):
    """Writing a stranger's strings to disk, carefully."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-restore-"))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def _bucket(self, extra=()):
        blobs = [
            FakeBlob("mirror/kubrick1/project.json", b'{"id": "kubrick1", "sdk": "adk"}'),
            FakeBlob("mirror/kubrick1/brief.md", b"draw a thing"),
            FakeBlob("mirror/kubrick1/artifacts/stage1.webp", b"pretend render"),
            FakeBlob("mirror/kubrick1/events/agent.jsonl", b'{"type":"run.begin"}\n'),
        ]
        return FakeBucket(blobs=list(blobs) + list(extra))

    def test_a_project_comes_back_whole(self):
        with with_archive(self._bucket()):
            target = archive_mod.restore("kubrick1", self.root)

        self.assertEqual(target, (self.root / "kubrick1").resolve())
        self.assertEqual((target / "brief.md").read_bytes(), b"draw a thing")
        self.assertEqual((target / "artifacts" / "stage1.webp").read_bytes(), b"pretend render")
        self.assertTrue((target / "events" / "agent.jsonl").is_file())

    def test_a_blob_whose_key_traverses_upward_is_skipped(self):
        """**The one that matters.** A key is not a URL and never passed through a router."""
        escapes = [
            FakeBlob("mirror/kubrick1/../../evil.txt", b"nope"),
            FakeBlob("mirror/kubrick1/a/../../../evil2.txt", b"nope"),
        ]
        with with_archive(self._bucket(escapes)):
            target = archive_mod.restore("kubrick1", self.root)

        self.assertFalse((self.root / "evil.txt").exists())
        self.assertFalse((self.root.parent / "evil2.txt").exists())
        self.assertTrue((target / "brief.md").is_file(), "the good files still arrived")

    def test_a_blob_whose_key_is_absolute_is_skipped_too(self):
        """The case that separates resolve-then-verify from a string test for `..`.

        Written after mutating `restore` to the string test and finding the sibling test above still
        passed — both of its keys literally contain `..`, so it could not tell the two
        implementations apart. This key contains none, and `Path(target) / "/etc/passwd"` discards
        the left operand entirely, which is precisely what makes an absolute path dangerous and
        invisible to a substring check.
        """
        absolute = [
            FakeBlob("mirror/kubrick1//etc/passwd", b"nope"),
            FakeBlob("mirror/kubrick1/C:/evil3.txt", b"nope"),
        ]
        with with_archive(self._bucket(absolute)):
            target = archive_mod.restore("kubrick1", self.root)

        for stray in target.parent.rglob("passwd"):
            self.fail(f"an absolute key was written to {stray}")
        self.assertFalse(Path("/etc/passwd_polson_test").exists())
        for stray in target.parent.rglob("evil3.txt"):
            self.fail(f"an absolute key was written to {stray}")
        self.assertTrue((target / "brief.md").is_file(), "the good files still arrived")

    def test_a_name_that_is_not_a_safe_segment_is_refused(self):
        for attempt in ("../escape", "a/b", "", ".", "..", "/etc/passwd", "x" * 200):
            with self.assertRaises(archive_mod.ArchiveError, msg=attempt):
                with with_archive(self._bucket()):
                    archive_mod.restore(attempt, self.root)

    def test_an_empty_prefix_is_refused_rather_than_making_an_empty_directory(self):
        with with_archive(FakeBucket()):
            with self.assertRaises(archive_mod.ArchiveError):
                archive_mod.restore("kubrick1", self.root)

    def test_an_archive_without_a_manifest_is_refused(self):
        """A directory that looks like a project and is not is worse than an honest refusal."""
        bucket = FakeBucket(blobs=[FakeBlob("mirror/kubrick1/brief.md", b"only this")])
        with with_archive(bucket):
            with self.assertRaises(archive_mod.ArchiveError) as caught:
                archive_mod.restore("kubrick1", self.root)

        self.assertIn("project.json", str(caught.exception))

    def test_too_many_files_is_refused_before_anything_is_written(self):
        many = [FakeBlob(f"mirror/kubrick1/artifacts/{i}.webp", b"x") for i in range(5)]
        with with_archive(self._bucket(many)), \
             mock.patch.object(archive_mod, "MAX_FILES", 3):
            with self.assertRaises(archive_mod.ArchiveError):
                archive_mod.restore("kubrick1", self.root)

        self.assertFalse((self.root / "kubrick1").exists())

    def test_too_large_is_refused_before_anything_is_written(self):
        with with_archive(self._bucket()), mock.patch.object(archive_mod, "MAX_TOTAL_BYTES", 4):
            with self.assertRaises(archive_mod.ArchiveError):
                archive_mod.restore("kubrick1", self.root)

        self.assertFalse((self.root / "kubrick1").exists())


class ArchiveRouteTests(unittest.TestCase):
    """Recovering through the page a visitor actually uses."""

    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="polson-archroute-"))
        self.registry = Registry()
        self.client = TestClient(app_mod.create_app(self.root, self.registry))

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def _project(self, name: str) -> Path:
        project = self.root / name
        (project / "events").mkdir(parents=True)
        (project / "project.json").write_text(
            json.dumps({"id": name, "workflow": "logo", "sdk": "adk"}), encoding="utf-8")
        return project

    def test_the_index_offers_an_archived_run_that_is_not_on_disk(self):
        with mock.patch.object(app_mod.archive, "restorable", return_value=["kubrick1"]):
            page = self.client.get("/")

        self.assertIn("kubrick1", page.text)
        self.assertIn("Recover", page.text)

    def test_the_index_says_nothing_about_recovery_when_there_is_no_archive(self):
        with mock.patch.object(app_mod.archive, "restorable", return_value=[]):
            page = self.client.get("/")

        self.assertNotIn("Recover a run", page.text)

    def test_observing_a_missing_project_restores_it_first(self):
        """The project must be genuinely absent when the request arrives — that is the whole case.

        Creating it up front and *then* asserting a restore is the version of this test that passes
        for the wrong reason: the route would skip the restore and the redirect would still be right.
        So the fake restore is what brings the directory into existence, exactly as the real one does.
        """
        def bring_it_back(name, root):
            return self._project(name)

        with mock.patch.object(app_mod.archive, "available", return_value=True), \
             mock.patch.object(app_mod.archive, "restore", side_effect=bring_it_back) as restore:
            page = self.client.post("/observe", data={"project": "kubrick1"},
                                    follow_redirects=False)

        restore.assert_called_once_with("kubrick1", self.root)
        self.assertEqual(page.status_code, 303)
        self.assertIn("/runs/kubrick1-watch-1", page.headers["location"])

    def test_a_project_on_disk_is_never_replaced_by_an_older_copy(self):
        """Restoring over a live run would lose work rather than recover it."""
        self._project("live")

        with mock.patch.object(app_mod.archive, "available", return_value=True), \
             mock.patch.object(app_mod.archive, "restore") as restore:
            self.client.post("/observe", data={"project": "live"}, follow_redirects=False)

        restore.assert_not_called()

    def test_a_refused_restore_is_shown_rather_than_raised(self):
        with mock.patch.object(app_mod.archive, "available", return_value=True), \
             mock.patch.object(app_mod.archive, "restore",
                               side_effect=archive_mod.ArchiveError("nothing archived under gone")):
            page = self.client.post("/observe", data={"project": "gone"}, follow_redirects=False)

        self.assertEqual(page.status_code, 409)
        self.assertIn("nothing archived under gone", page.text)

    def test_a_traversing_name_never_reaches_the_archive(self):
        """`contain` refuses it before `restore` is consulted, as it does for every other route."""
        with mock.patch.object(app_mod.archive, "available", return_value=True), \
             mock.patch.object(app_mod.archive, "restore") as restore:
            page = self.client.post("/observe", data={"project": "../escape"},
                                    follow_redirects=False)

        restore.assert_not_called()
        self.assertEqual(page.status_code, 409)


if __name__ == "__main__":
    unittest.main()
