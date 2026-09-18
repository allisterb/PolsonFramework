"""The run page's `detail()` renderer, driven from the shapes the producers actually emit.

    python -m unittest discover -s src/tests -t .

**Why this exists, and why it is a node subprocess rather than a Python assertion.** `detail()` is
inline JavaScript in `run.html`, so nothing in `test_studio.py` can reach it: those tests are
server-side and the renderer runs in the browser. That gap is not theoretical. A live run rendered

    0% of the token budget - 1 of NaN

because two unrelated producers both write an event of kind ``budget`` and the branch assumed one of
them. Nothing failed, nothing logged, and the row looked like a working feature reporting a healthy
run - it was caught by eye, by the director, on a run in progress.

**The cases are taken from the producers, not invented.** Every event below is the field set some
call site really writes, cited in its own comment, because a renderer test written against imagined
events tests the imagination. Two producers write into one page:

* ``Events.Append(...)`` in ``Polson.MCPServer`` -> ``events/server.jsonl``
* ``transcript.py`` / ``run.py`` in ``orchestrator`` and ``adk_agent`` -> ``agent.jsonl``

Only ``budget`` and ``run.end`` are written by both, and both had a defect. The rest of the table is
regression cover: the general rule is that **no branch may put `NaN` or `undefined` in front of a
reader**, and that is asserted for every case rather than only the two that were broken.
"""

from __future__ import annotations

import json
import shutil
import subprocess
import textwrap
import unittest
from pathlib import Path

RUN_HTML = Path(__file__).resolve().parents[1] / "studio" / "templates" / "run.html"

# The harness pulls `detail` out of the template by its own source, so the test cannot drift from a
# copy: editing the branch in `run.html` is what this reads. `toolDetail` is stubbed rather than
# extracted - it has its own dependencies, and `tool.call` is not what this is about.
HARNESS = """
const fs = require('fs');
const html = fs.readFileSync(process.argv[2], 'utf8');

const open = html.indexOf('  function detail(kind, e) {');
if (open < 0) { console.error('detail() not found in run.html'); process.exit(2); }
const close = html.indexOf('\\n  }\\n', open);
if (close < 0) { console.error('end of detail() not found'); process.exit(2); }
const source = html.slice(open, close + 4);

const toolDetail = () => '(tool)';
const make = new Function('toolDetail', source + '; return detail;');
const detail = make(toolDetail);

const cases = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const out = cases.map(c => ({ name: c.name, text: String(detail(c.event.type, c.event)) }));
process.stdout.write(JSON.stringify(out));
"""

# Each case is (name, event, expected-substring-or-None). The event is exactly what a producer
# writes, including its `type`.
CASES: list[tuple[str, dict, str | None]] = [
    # --- the collision that was found in the wild -----------------------------------------------
    # DrawingMcpTools.cs: Events.Append("budget", ...) after any execution that requisitioned.
    # Copied from projects/board2/events/server.jsonl, seq 8.
    ("requisition snapshot",
     {"type": "budget", "total": 120, "spent": 1, "remaining": 119, "cacheHits": 0,
      "tokensSpent": 1493},
     "1 of 120 requisitions"),
    ("requisition snapshot with a cache hit",
     {"type": "budget", "total": 120, "spent": 1, "remaining": 119, "cacheHits": 1,
      "tokensSpent": 1493},
     "from cache"),
    # studio.py: transcript.note_budget(invocation, limit="tokens", share=..., spent=..., cap=...)
    ("token budget warning",
     {"type": "budget", "limit": "tokens", "share": 0.75, "spent": 1500000, "cap": 2000000},
     "75% of the token budget"),
    # studio.py: the same call with limit="time", spent and cap in minutes.
    ("time budget warning",
     {"type": "budget", "limit": "time", "share": 0.8, "spent": 24.4, "cap": 30},
     "80% of the time budget"),

    # --- the collision this audit found -----------------------------------------------------------
    # transcript.py: reason completed / failed / halted, the last carrying limit, used and cap.
    ("run ended, completed", {"type": "run.end", "reason": "completed"}, "completed"),
    ("run ended, failed",
     {"type": "run.end", "reason": "failed", "error": "ValueError: no brief"}, "no brief"),
    ("run halted on tokens",
     {"type": "run.end", "reason": "halted", "limit": "tokens", "used": 2005223, "cap": 2000000},
     "halted"),
    ("run halted on time",
     {"type": "run.end", "reason": "halted", "limit": "time", "used": 3600, "cap": 1800},
     "halted"),
    # run.py: status ok / incomplete and an error, with NO `reason` at all. This is the one that
    # rendered as "completed" while the run had not completed.
    ("orchestrator run ended incomplete",
     {"type": "run.end", "status": "incomplete", "error": None}, "incomplete"),
    ("orchestrator run ended ok", {"type": "run.end", "status": "ok", "error": None}, "completed"),

    # --- regression cover for the rest of the surface ---------------------------------------------
    # StageApi.cs
    ("a note", {"type": "note", "message": "cast bought in one call"}, "cast bought"),
    ("a stage beginning", {"type": "stage.begin", "stage": "Cast"}, "Cast"),
    ("a check", {"type": "check", "claim": "accent under 15%", "passed": True}, None),
    # DrawingMcpTools.cs
    ("a render", {"type": "render", "script": "scripts/0001.js",
                  "artifact": "artifacts/board.webp", "format": "webp", "bytes": 51201},
     "artifacts/board.webp"),
    ("a script error", {"type": "script.error", "error": "ReferenceError: run is not defined"},
     "ReferenceError"),
    ("an inspect", {"type": "inspect", "probes": {"absent": 2}}, "absent 2"),
    ("a requisition",
     {"type": "asset.requisition", "kind": "cutout", "descriptor": "a ranch hand"}, None),
    # Research.cs - expectSeconds is what keeps a long silence legible.
    ("research started",
     {"type": "research.started", "description": "box office", "expectSeconds": 292},
     "up to about 5 min"),
    ("research started without an estimate",
     {"type": "research.started", "description": "box office"}, "box office"),
    # broker.py
    ("a dropped stream", {"type": "stream.lag", "dropped": 12}, "12 not shown"),
    # hostlog.py - every field is optional, because the host may report none of them.
    ("usage", {"type": "usage", "inputTokens": 32000, "cacheReadTokens": 28000,
               "outputTokens": 1200}, "cached 28,000"),
    ("usage with nothing populated", {"type": "usage"}, None),
]


def _node() -> str | None:
    return shutil.which("node")


@unittest.skipIf(_node() is None, "node is not on PATH; the run page's JS cannot be exercised")
class DetailRendererTests(unittest.TestCase):
    """Every branch of `detail()`, against the field sets its producers really write."""

    rendered: dict[str, str]

    @classmethod
    def setUpClass(cls) -> None:
        payload = [{"name": name, "event": event} for name, event, _ in CASES]

        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            harness = Path(tmp) / "harness.js"
            cases = Path(tmp) / "cases.json"
            harness.write_text(textwrap.dedent(HARNESS), encoding="utf-8")
            cases.write_text(json.dumps(payload), encoding="utf-8")

            done = subprocess.run(
                [_node(), str(harness), str(RUN_HTML), str(cases)],
                capture_output=True, text=True, timeout=60)

        if done.returncode != 0:
            raise AssertionError(f"harness failed ({done.returncode}): {done.stderr.strip()}")

        cls.rendered = {row["name"]: row["text"] for row in json.loads(done.stdout)}

    def test_every_case_rendered(self) -> None:
        self.assertEqual(len(self.rendered), len(CASES))

    def test_no_row_shows_a_coercion_artifact(self) -> None:
        """The rule the budget row broke, asserted for the whole surface.

        `NaN` and `undefined` reach a reader only through arithmetic or interpolation on a field the
        producer did not write - which is precisely what happens when one branch is asked to render
        another producer's event. Nothing here should ever be able to print either.
        """
        for name, text in sorted(self.rendered.items()):
            with self.subTest(case=name):
                self.assertNotIn("NaN", text, f"{name!r} rendered as {text!r}")
                self.assertNotIn("undefined", text, f"{name!r} rendered as {text!r}")

    def test_each_case_says_what_it_should(self) -> None:
        for name, _, expected in CASES:
            if expected is None:
                continue
            with self.subTest(case=name):
                self.assertIn(expected, self.rendered[name])

    def test_a_requisition_snapshot_is_not_reported_as_a_token_budget(self) -> None:
        """The exact defect: the asset allowance named as the token one, with an invented ceiling."""
        text = self.rendered["requisition snapshot"]
        self.assertNotIn("token budget", text)
        self.assertIn("requisitions", text)

    def test_an_incomplete_run_is_not_reported_as_completed(self) -> None:
        """The defect this audit found: `run.py` carries `status`, never `reason`."""
        self.assertNotEqual("completed", self.rendered["orchestrator run ended incomplete"])


class ProducerCollisionTests(unittest.TestCase):
    """The audit itself, kept as a test so a third collision cannot arrive unnoticed.

    Both defects above are the same shape: one event kind, two producers, a renderer that assumed
    one of them. That is not a bug to fix once - it is a hazard of having two runtimes write into a
    single page, and it recurs whenever a kind is added on one side that already exists on the other.
    """

    def test_the_kinds_written_by_both_runtimes_are_the_ones_the_page_handles_by_shape(self) -> None:
        root = Path(__file__).resolve().parents[1]

        dotnet = _emitted(root / "Polson.MCPServer", 'Events.Append("', '"')
        python = set()
        for area in ("orchestrator", "adk_agent"):
            python |= _emitted(root / area, '.append("', '"', skip="tests")

        shared = dotnet & python
        # Both are rendered by branching on which producer's fields are present. A kind arriving
        # here that is not in this set means a new collision: either give it a distinct name, or
        # teach `detail()` to tell the two shapes apart, and add it below with its cases above.
        self.assertEqual({"budget"}, shared,
                         "an event kind is written by both runtimes; see this test's docstring")


def _emitted(folder: Path, opener: str, closer: str, skip: str | None = None) -> set[str]:
    """Event kinds emitted as string literals under `folder`."""
    found: set[str] = set()
    for path in folder.rglob("*"):
        if path.suffix not in {".cs", ".py"} or not path.is_file():
            continue
        if skip and skip in path.parts:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        start = 0
        while (at := text.find(opener, start)) >= 0:
            start = at + len(opener)
            end = text.find(closer, start)
            if end < 0:
                break
            kind = text[start:end]
            if kind and all(c.islower() or c == "." for c in kind):
                found.add(kind)
    return found


if __name__ == "__main__":
    unittest.main()
