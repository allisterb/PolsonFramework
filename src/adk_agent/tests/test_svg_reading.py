"""Why `read_file` will not open an SVG, and why `peek` will not either.

The MCP server stopped returning `svgXml` in a tool result because nothing consumed it there and it
had become enormous — a vector page carrying one 400px portrait returned a 117,786-character result
of which 109,045 characters were markup, about 29,000 tokens, on a call that had already written
both the render and the SVG to disk.

Removing it from the response is only half the rule. `READABLE_SUFFIXES` used to include `.svg`, so
the same bytes came straight back through the file reader; the comment above `PEEKABLE` actively
recommended it. These tests pin the other half.

**A flat refusal rather than a size or data-URI test**, because no threshold makes reading an SVG
the right move: it cannot be seen by reading, `Snap.load` opens it inside a script for editing and
answers structural questions there without the document crossing into context, and for an SVG the
agent drew, the script that produced it is shorter and says what it meant.

The messages matter as much as the refusals. `peek` used to say *"an .svg is text — read it another
way"*, which after this change would point at a tool that points back — the two-wasted-turns failure
`_make_read_file`'s own docstring records.
"""

from __future__ import annotations

import asyncio
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import studio


class SvgReadingTests(unittest.TestCase):
    #region Fixtures
    def setUp(self) -> None:
        self.project = Path(tempfile.mkdtemp(prefix="polson_svgread_"))
        (self.project / "artifacts").mkdir()
        self.write("plain.svg", "<svg xmlns='http://www.w3.org/2000/svg'><rect width='10' height='10'/></svg>")
        self.write("inlined.svg", "<svg xmlns='http://www.w3.org/2000/svg'><image href='data:image/png;base64,AAAA'/></svg>")
        self.write("artwork.js", "const paper = Snap(10, 10);\n")
        self.write("brief.md", "# The brief\n")
        self.read_file = studio._make_read_file(self.project)
        self.peek = studio._make_peek(self.project)

    def tearDown(self) -> None:
        shutil.rmtree(self.project, ignore_errors=True)

    def write(self, name: str, text: str) -> None:
        (self.project / name).write_text(text, encoding="utf-8")

    @staticmethod
    def run_(coro):
        return asyncio.run(coro)
    #endregion

    #region read_file
    def test_an_svg_is_refused_whatever_it_contains(self):
        """Both files, because the rule is not about size or about inlined images."""
        for name in ("plain.svg", "inlined.svg"):
            with self.subTest(name=name):
                result = self.run_(self.read_file(name))
                self.assertFalse(result["ok"])
                self.assertIn("markup", result["error"])

    def test_the_refusal_names_all_three_routes(self):
        """A refusal that does not say what to do instead costs a turn to discover."""
        error = self.run_(self.read_file("plain.svg"))["error"]

        self.assertIn("RenderSvg(file='plain.svg'", error)   # to see it
        self.assertIn("Snap.load('plain.svg')", error)       # to edit or inspect it
        self.assertIn(".js script", error)                   # to understand one you drew

    def test_the_suffix_is_not_advertised_as_readable(self):
        """The generic message lists what *is* readable, so .svg must be out of that list too."""
        self.assertNotIn(".svg", studio.READABLE_SUFFIXES)

        error = self.run_(self.read_file("preview.png"))["error"]
        self.assertNotIn(".svg", error)

    def test_ordinary_text_files_still_open(self):
        """The guard must not have narrowed anything else — the brief above all."""
        for name in ("brief.md", "artwork.js"):
            with self.subTest(name=name):
                result = self.run_(self.read_file(name))
                self.assertTrue(result["ok"], result.get("error"))
                self.assertGreater(result["bytes"], 0)
    #endregion

    #region peek
    def test_peek_sends_an_svg_to_the_renderer_not_back_to_the_reader(self):
        """
        The circular-advice regression. Before this, peek said "read it another way" while
        read_file said "peek it" — each tool naming the other, and neither naming RenderSvg.
        """
        error = self.run_(self.peek("plain.svg", None))["error"]

        self.assertIn("RenderSvg(file='plain.svg'", error)
        self.assertNotIn("read it another way", error)

    def test_peek_still_refuses_other_non_images_plainly(self):
        """No SVG advice on a file that is not one."""
        error = self.run_(self.peek("artwork.js", None))["error"]

        self.assertIn("not a peekable image", error)
        self.assertNotIn("RenderSvg", error)
    #endregion


if __name__ == "__main__":
    unittest.main()
