"""The director's channel into a run that is already going.

The mechanism is `after_tool_callback` returning a replacement result, which is the same path the
studio watchdog uses — so the tests that matter most are the ones covering the failure that made
*that* mechanism inert on two live runs: it understood only the MCP result shape, silently dropped
its directive on every `FunctionTool` call, and the advice was never delivered by anybody.

    python-adk\\Scripts\\python.exe -m unittest discover -p "test_*.py"
"""

from __future__ import annotations

import asyncio
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import interject


class FakeTool:
    def __init__(self, name="ExecuteScript"):
        self.name = name


class InterjectQueueTests(unittest.TestCase):
    """Queueing, and the bounds on it."""

    def setUp(self) -> None:
        interject.take("acme")
        interject.take("other")

    def test_what_is_offered_is_what_comes_back(self):
        self.assertTrue(interject.offer("acme", "make the background red"))
        self.assertEqual(interject.take("acme"), ["make the background red"])

    def test_taking_drains(self):
        interject.offer("acme", "one")
        interject.take("acme")
        self.assertEqual(interject.take("acme"), [])

    def test_projects_do_not_hear_each_other(self):
        """Two runs can be going at once; a message is for one of them."""
        interject.offer("acme", "for acme")
        interject.offer("other", "for other")

        self.assertEqual(interject.take("acme"), ["for acme"])
        self.assertEqual(interject.take("other"), ["for other"])

    def test_nothing_is_not_queued(self):
        for empty in ("", "   ", None):
            self.assertFalse(interject.offer("acme", empty))
        self.assertEqual(interject.waiting("acme"), 0)

    def test_a_message_is_clipped_rather_than_refused(self):
        interject.offer("acme", "x" * 5000)
        self.assertEqual(len(interject.take("acme")[0]), interject.MAX_CHARS)

    def test_the_queue_is_bounded(self):
        """A director holding the key while an agent thinks is not a reason to grow forever."""
        for i in range(interject.MAX_WAITING + 5):
            interject.offer("acme", f"line {i}")

        lines = interject.take("acme")
        self.assertEqual(len(lines), interject.MAX_WAITING)
        self.assertEqual(lines[-1], f"line {interject.MAX_WAITING + 4}")   # the newest survive

    def test_waiting_reports_the_depth(self):
        interject.offer("acme", "one")
        interject.offer("acme", "two")
        self.assertEqual(interject.waiting("acme"), 2)


class AttachTests(unittest.TestCase):
    """Both result shapes, which is the whole of what went wrong last time."""

    def test_an_mcp_result_gets_another_text_part(self):
        result = {"content": [{"type": "text", "text": "rendered"}], "isError": False}

        amended = interject.attach(result, ["make it red"])

        self.assertEqual(len(amended["content"]), 2)
        self.assertIn("make it red", amended["content"][1]["text"])
        self.assertFalse(amended["isError"])

    def test_a_function_tool_result_gets_a_field(self):
        """**The shape that was silently dropped.** `write_script`, `peek` and `read_file` are these.

        The watchdog's first version knew only the MCP shape, logged that it could not attach, and
        delivered nothing — on two live runs the trigger fired on a `write_script` result and the
        advice went nowhere.
        """
        amended = interject.attach({"ok": True, "path": "artwork.js"}, ["make it red"])

        self.assertIn("make it red", amended["director_says"])
        self.assertEqual(amended["path"], "artwork.js", "the result the agent asked for survives")
        self.assertTrue(amended["ok"])

    def test_the_original_result_is_never_taken_away(self):
        """Appended, not substituted: the agent still needs the render path it called for."""
        result = {"content": [{"type": "text", "text": "artifacts/stage1.webp"}]}

        amended = interject.attach(result, ["red please"])

        self.assertEqual(amended["content"][0]["text"], "artifacts/stage1.webp")
        self.assertIsNot(amended, result, "the caller's dict is not mutated")
        self.assertEqual(len(result["content"]), 1)

    def test_several_lines_arrive_together(self):
        amended = interject.attach({"ok": True}, ["red", "and bigger"])

        self.assertIn("red", amended["director_says"])
        self.assertIn("and bigger", amended["director_says"])

    def test_a_shape_it_cannot_amend_is_reported_rather_than_guessed(self):
        self.assertIsNone(interject.attach("a string", ["red"]))
        self.assertIsNone(interject.attach(None, ["red"]))

    def test_a_question_is_told_where_the_answer_has_to_go(self):
        """**Measured on the first live run of this feature.**

        The director asked "what is your budget?". The agent received it, called `budget_status` —
        the right tool, unprompted, mid-research — and then said nothing. Finding the answer and
        giving it are different acts, and the first wording only asked it to *act*, which is right
        for "make the background red" and wrong for a question.
        """
        text = interject.attach({"ok": True}, ["what is your budget?"])["director_says"]

        self.assertIn("Stage.note", text)
        self.assertIn("only place", text)

    def test_it_is_told_to_carry_on_afterwards(self):
        """An aside, not a new brief. The first run got this right on its own; say it anyway."""
        text = interject.attach({"ok": True}, ["make it red"])["director_says"]

        self.assertIn("carry on", text.lower())

    def test_the_text_says_who_is_speaking(self):
        """The agent has to be able to tell this from a tool's own output."""
        amended = interject.attach({"ok": True}, ["make it red"])

        self.assertIn("director", amended["director_says"].lower())
        self.assertIn("not a tool result", amended["director_says"])


class InterjectPluginTests(unittest.IsolatedAsyncioTestCase):
    """Delivery, at the agent's next tool call."""

    def setUp(self) -> None:
        interject.take("acme")
        self.plugin = interject.make_plugin(Path("/wherever/acme"))

    async def _call(self, result):
        return await self.plugin.after_tool_callback(
            tool=FakeTool(), tool_args={}, tool_context=None, result=result)

    async def test_an_empty_queue_leaves_the_result_untouched(self):
        """None is ADK's 'unchanged', and it is the answer on nearly every call."""
        self.assertIsNone(await self._call({"ok": True}))

    async def test_a_queued_line_is_delivered_at_the_next_tool_call(self):
        interject.offer("acme", "make the background red")

        amended = await self._call({"ok": True, "path": "artwork.js"})

        self.assertIsNotNone(amended)
        self.assertIn("make the background red", amended["director_says"])

    async def test_it_is_delivered_once(self):
        interject.offer("acme", "make it red")
        await self._call({"ok": True})

        self.assertIsNone(await self._call({"ok": True}), "a second call should carry nothing")

    async def test_an_undeliverable_shape_keeps_the_message_for_the_next_call(self):
        """**A dropped interjection is invisible**, so it goes back on the queue instead.

        The director saw it accepted; there is no second signal to say it evaporated.
        """
        interject.offer("acme", "make it red")

        self.assertIsNone(await self._call("not a dict"))
        self.assertEqual(interject.waiting("acme"), 1)

        amended = await self._call({"ok": True})
        self.assertIn("make it red", amended["director_says"])

    async def test_order_is_kept_when_a_delivery_is_put_back(self):
        interject.offer("acme", "first")
        interject.offer("acme", "second")
        await self._call("not a dict")

        self.assertEqual(interject.take("acme"), ["first", "second"])

    async def test_the_plugin_only_hears_its_own_project(self):
        interject.offer("other", "not for you")

        self.assertIsNone(await self._call({"ok": True}))
        self.assertEqual(interject.waiting("other"), 1)


class InterjectWiringTests(unittest.TestCase):
    """That the plugin is attached to the app, which every other test here assumes."""

    def test_the_channel_is_attached_alongside_the_other_plugins(self):
        import shutil, tempfile, json
        root = Path(tempfile.mkdtemp(prefix="polson-wire-")) / "acme"
        try:
            (root / "events").mkdir(parents=True)
            (root / "project.json").write_text(json.dumps({"id": "acme", "workflow": "logo"}),
                                               encoding="utf-8")
            (root / "GEMINI.md").write_text("# instructions", encoding="utf-8")

            import studio
            names = [getattr(p, "name", "") for p in studio.build_app(root, budget_minutes=5).plugins]
        finally:
            shutil.rmtree(root.parent, ignore_errors=True)

        self.assertIn("polson_interject", names)
        # It must not have displaced the record or the watchdog on its way in.
        self.assertIn("polson_transcript", names)
        self.assertIn("studio_watchdog", names)


if __name__ == "__main__":
    unittest.main()
