"""The copy that survives the machine.

Nothing here talks to Google Cloud Storage. `ProjectMirror` takes its bucket from a client built in
its constructor, so the tests substitute a fake bucket and assert on what was handed to it — which is
the only part this module is responsible for. A test that needed a real bucket is a test nobody runs.

    python-adk\\Scripts\\python.exe -m unittest discover -p "test_*.py"
"""

from __future__ import annotations

import asyncio
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import mirror as mirror_mod


class FakeBlob:
    def __init__(self, bucket, name):
        self.bucket, self.name = bucket, name

    def upload_from_filename(self, path):
        self.bucket.uploaded[self.name] = Path(path).read_bytes()


class FakeBucket:
    def __init__(self, name="polson-artifacts"):
        self.name = name
        self.uploaded: dict[str, bytes] = {}
        self.fail_on: set[str] = set()

    def blob(self, name):
        if name in self.fail_on:
            raise RuntimeError(f"pretend upload failure for {name}")
        return FakeBlob(self, name)


def build(root: Path, uri="gs://polson-artifacts/mirror") -> tuple[mirror_mod.ProjectMirror, FakeBucket]:
    """A mirror wired to a fake bucket, with no cloud client constructed."""
    bucket = FakeBucket()
    fake_storage = mock.MagicMock()
    fake_storage.Client.return_value.bucket.return_value = bucket
    with mock.patch.dict(sys.modules, {"google.cloud": mock.MagicMock(storage=fake_storage),
                                       "google.cloud.storage": fake_storage}):
        return mirror_mod.ProjectMirror(root, uri=uri, interval=0.05), bucket


def project(root: Path) -> Path:
    """A project directory of the shape a run leaves behind."""
    for folder in ("artifacts", "scripts", "events", "documents"):
        (root / folder).mkdir(parents=True, exist_ok=True)
    (root / "brief.md").write_text("draw a thing", encoding="utf-8")
    (root / "artwork.js").write_text("const paper = Snap(900, 1350);", encoding="utf-8")
    (root / "project.json").write_text('{"id": "acme"}', encoding="utf-8")
    (root / "GEMINI.md").write_text("x" * 5000, encoding="utf-8")
    (root / "agent.config.json").write_text('{"deniedTools": []}', encoding="utf-8")
    (root / ".agents").mkdir(exist_ok=True)
    (root / ".agents" / "mcp_config.json").write_text('{"mcpServers": {}}', encoding="utf-8")
    (root / "artifacts" / "stage1.webp").write_bytes(b"pretend render")
    (root / "scripts" / "0001.js").write_text("// ran", encoding="utf-8")
    (root / "events" / "agent.jsonl").write_text('{"type":"run.begin"}\n', encoding="utf-8")
    (root / "documents" / "returns.csv").write_text("secret,numbers", encoding="utf-8")
    return root


class MirrorSelectionTests(unittest.TestCase):
    """What is copied, and — the half that matters — what is not."""

    def setUp(self) -> None:
        self.root = project(Path(tempfile.mkdtemp(prefix="polson-mirror-")) / "acme")
        self.mirror, self.bucket = build(self.root)

    def tearDown(self) -> None:
        shutil.rmtree(self.root.parent, ignore_errors=True)

    def test_the_work_is_copied(self):
        asyncio.run(self.mirror.sweep())

        for expected in ("mirror/acme/brief.md", "mirror/acme/artwork.js",
                         "mirror/acme/project.json", "mirror/acme/artifacts/stage1.webp",
                         "mirror/acme/scripts/0001.js", "mirror/acme/events/agent.jsonl"):
            self.assertIn(expected, self.bucket.uploaded)

    def test_the_directors_documents_are_never_copied(self):
        """`documents/` is the client's own material and is not ours to put anywhere."""
        asyncio.run(self.mirror.sweep())

        for key in self.bucket.uploaded:
            self.assertNotIn("documents", key)
        self.assertNotIn(b"secret,numbers", b"".join(self.bucket.uploaded.values()))

    def test_configuration_and_instructions_are_not_copied(self):
        """`.agents/` carries tool policy and MCP command lines; neither is work.

        `GEMINI.md` is excluded for a duller reason — identical in every project, ~75 KB, and it
        would be most of the traffic for none of the value.
        """
        asyncio.run(self.mirror.sweep())
        copied = " ".join(self.bucket.uploaded)

        for absent in ("GEMINI.md", "agent.config.json", "mcp_config.json", ".agents"):
            self.assertNotIn(absent, copied, absent)

    def test_a_file_appearing_later_at_the_root_is_not_copied_by_default(self):
        """The root list is an allowlist, so a new file is not published by accident."""
        (self.root / "scratch.txt").write_text("working notes", encoding="utf-8")
        (self.root / "credentials.json").write_text("{}", encoding="utf-8")
        asyncio.run(self.mirror.sweep())

        self.assertNotIn("mirror/acme/scratch.txt", self.bucket.uploaded)
        self.assertNotIn("mirror/acme/credentials.json", self.bucket.uploaded)

    def test_the_research_record_is_kept(self):
        """It costs one of a run's two research allowances and is what makes a figure checkable."""
        (self.root / ".polson" / "research").mkdir(parents=True)
        (self.root / ".polson" / "research" / "run1.json").write_text("{}", encoding="utf-8")
        asyncio.run(self.mirror.sweep())

        self.assertIn("mirror/acme/.polson/research/run1.json", self.bucket.uploaded)

    def test_an_absurdly_large_file_is_skipped_rather_than_copied(self):
        big = self.root / "artifacts" / "huge.webp"
        big.write_bytes(b"0" * 64)
        with mock.patch.object(mirror_mod, "MAX_BYTES", 8):
            asyncio.run(self.mirror.sweep())

        self.assertNotIn("mirror/acme/artifacts/huge.webp", self.bucket.uploaded)


class MirrorSweepTests(unittest.TestCase):
    """How it behaves over a run rather than at one instant."""

    def setUp(self) -> None:
        self.root = project(Path(tempfile.mkdtemp(prefix="polson-mirror-")) / "acme")
        self.mirror, self.bucket = build(self.root)

    def tearDown(self) -> None:
        shutil.rmtree(self.root.parent, ignore_errors=True)

    def test_a_second_sweep_copies_nothing_when_nothing_changed(self):
        first = asyncio.run(self.mirror.sweep())
        second = asyncio.run(self.mirror.sweep())

        self.assertGreater(first, 0)
        self.assertEqual(second, 0)

    def test_a_file_the_engine_rewrites_is_copied_again(self):
        """The case a per-writer hook misses.

        `outFile` overwrites, and the write happens inside the .NET engine — this process is told a
        path and nothing else. Sweeping is what makes the second version arrive at all.
        """
        asyncio.run(self.mirror.sweep())
        render = self.root / "artifacts" / "stage1.webp"
        render.write_bytes(b"the second pass")
        os.utime(render, (render.stat().st_atime, render.stat().st_mtime + 10))

        self.assertEqual(asyncio.run(self.mirror.sweep()), 1)
        self.assertEqual(self.bucket.uploaded["mirror/acme/artifacts/stage1.webp"],
                         b"the second pass")

    def test_a_new_file_mid_run_is_picked_up_without_being_announced(self):
        asyncio.run(self.mirror.sweep())
        (self.root / "accuracy.md").write_text("| figure | source |", encoding="utf-8")

        self.assertEqual(asyncio.run(self.mirror.sweep()), 1)
        self.assertIn("mirror/acme/accuracy.md", self.bucket.uploaded)

    def test_a_failed_upload_is_retried_on_the_next_sweep(self):
        """A file left out of the sent-set is a file the next sweep tries again.

        Recording it as sent on failure would lose it permanently, which is the one outcome this
        module exists to prevent.
        """
        self.bucket.fail_on = {"mirror/acme/brief.md"}
        asyncio.run(self.mirror.sweep())
        self.assertNotIn("mirror/acme/brief.md", self.bucket.uploaded)

        self.bucket.fail_on = set()
        asyncio.run(self.mirror.sweep())
        self.assertIn("mirror/acme/brief.md", self.bucket.uploaded)

    def test_a_sweep_never_raises(self):
        """It protects against infrastructure misbehaving, so it must not fail when it does."""
        self.bucket.fail_on = {f"mirror/acme/{n}" for n in ("brief.md", "artwork.js")}

        try:
            asyncio.run(self.mirror.sweep())
        except Exception as exc:                                # pragma: no cover
            self.fail(f"sweep raised {exc!r}")

    def test_finish_takes_a_final_copy_after_the_timer_stops(self):
        """A run ending between ticks still lands whole."""
        async def run():
            self.mirror.start()
            await asyncio.sleep(0)
            (self.root / "findings.md").write_text("what happened", encoding="utf-8")
            await self.mirror.finish()

        asyncio.run(run())

        self.assertIn("mirror/acme/findings.md", self.bucket.uploaded)


class MirrorConfigurationTests(unittest.TestCase):
    """Off unless told otherwise, and loud about a destination it cannot use."""

    def setUp(self) -> None:
        self.root = project(Path(tempfile.mkdtemp(prefix="polson-mirror-")) / "acme")

    def tearDown(self) -> None:
        shutil.rmtree(self.root.parent, ignore_errors=True)

    def test_no_uri_means_no_plugin(self):
        """A local checkout registers nothing and behaves exactly as it did before."""
        with mock.patch.object(mirror_mod, "MIRROR_URI", ""):
            self.assertFalse(mirror_mod.configured())
            self.assertIsNone(mirror_mod.make_plugin(self.root))

    def test_a_uri_that_is_not_gs_is_refused_by_name(self):
        for bad in ("file:///tmp/x", "polson-artifacts", "https://example.com/x", "gs://"):
            with self.assertRaises(ValueError, msg=bad):
                build(self.root, uri=bad)

    def test_the_prefix_is_the_uri_path_plus_the_project_name(self):
        m, _ = build(self.root, uri="gs://polson-artifacts/mirror")
        self.assertEqual(m.prefix, "mirror/acme")

    def test_a_bucket_with_no_prefix_puts_the_project_at_the_root(self):
        m, _ = build(self.root, uri="gs://polson-artifacts")
        self.assertEqual(m.prefix, "acme")

    def test_a_plugin_that_cannot_be_built_is_none_rather_than_an_exception(self):
        """An app that will not start because a *backup* failed is the worse outcome."""
        with mock.patch.object(mirror_mod, "MIRROR_URI", "gs://polson-artifacts"), \
             mock.patch.object(mirror_mod, "ProjectMirror", side_effect=RuntimeError("no creds")):
            self.assertIsNone(mirror_mod.make_plugin(self.root))


class MirrorWiringTests(unittest.TestCase):
    """That the plugin is actually attached to the app, which is the part a unit test can miss.

    Every other test here proves the mirror *works*. This one proves it is *installed* — the failure
    the deployed service could not be made to demonstrate cheaply, because ADK builds an app lazily
    on the first run and confirming it in the container would mean paying for one.
    """

    def setUp(self) -> None:
        self.root = project(Path(tempfile.mkdtemp(prefix="polson-wiring-")) / "acme")

    def tearDown(self) -> None:
        shutil.rmtree(self.root.parent, ignore_errors=True)

    def _plugin_names(self) -> list[str]:
        import studio
        app = studio.build_app(self.root, budget_minutes=5)
        return [getattr(p, "name", "") for p in app.plugins]

    def test_the_mirror_is_attached_when_a_destination_is_configured(self):
        fake_storage = mock.MagicMock()
        fake_storage.Client.return_value.bucket.return_value = FakeBucket()
        with mock.patch.object(mirror_mod, "MIRROR_URI", "gs://polson-artifacts/mirror"), \
             mock.patch.dict(sys.modules, {"google.cloud": mock.MagicMock(storage=fake_storage),
                                           "google.cloud.storage": fake_storage}):
            self.assertIn("polson_mirror", self._plugin_names())

    def test_nothing_is_attached_when_it_is_not_configured(self):
        """A local checkout builds exactly the app it built before this module existed."""
        with mock.patch.object(mirror_mod, "MIRROR_URI", ""):
            self.assertNotIn("polson_mirror", self._plugin_names())

    def test_the_transcript_is_still_attached_alongside_it(self):
        """The two observe the same run for different reasons; neither may displace the other."""
        fake_storage = mock.MagicMock()
        fake_storage.Client.return_value.bucket.return_value = FakeBucket()
        with mock.patch.object(mirror_mod, "MIRROR_URI", "gs://polson-artifacts/mirror"), \
             mock.patch.dict(sys.modules, {"google.cloud": mock.MagicMock(storage=fake_storage),
                                           "google.cloud.storage": fake_storage}):
            names = self._plugin_names()

        self.assertIn("polson_transcript", names)
        self.assertIn("polson_mirror", names)


if __name__ == "__main__":
    unittest.main()
