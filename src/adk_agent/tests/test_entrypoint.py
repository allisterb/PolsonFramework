"""The settings file the container writes at start.

`docker-entrypoint.sh` only ever runs inside an image, which makes it exactly the code that rots
without anyone noticing: a mistake here does not fail a build, it produces a container that starts
cleanly and is quietly misconfigured. That is not hypothetical — the script wrote the API key and
nothing else for the life of the deployment, so `Assets:Budget`, the models, the cache directories
and the research key were unreachable on every deploy while looking fully configured.

The generator is **extracted from the script itself** rather than copied here. A copy would be a
second thing to keep in step, and the failure it invites is the one this file exists to catch.
"""

from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
ENTRYPOINT = REPO / "docker-entrypoint.sh"

#: Every key `Program.Setting(...)` reads. A setting added to the engine and not to the entrypoint
#: cannot be set on a deployment at all, so this list is the contract between the two.
ENGINE_KEYS = {
    "ApiKeys:GoogleAgentPlatform",
    "ApiKeys:Parallel",
    "Assets:Budget",
    "Assets:CacheDir",
    "Assets:Model",
    "Documents:Budget",
    "Documents:CacheDir",
    "Documents:Model",
    "Photos:AllowedHosts",
    "Photos:Budget",
    "Photos:UserAgent",
    "Research:ArchiveDir",
    "Research:Budget",
    "Research:Processor",
    "Server:DefaultTimeoutSeconds",
}


def generator() -> str:
    """The Python heredoc out of the shell script, so the test runs what the container runs."""
    body = ENTRYPOINT.read_text(encoding="utf-8")
    start = body.index('python - "$CLI_SETTINGS" <<')
    start = body.index("\n", start) + 1
    return body[start:body.index("\nPYTHON\n", start)]


def write_settings(env: dict[str, str]) -> tuple[dict, str, str]:
    """Runs the generator with `env` and returns (settings, stdout, stderr)."""
    with tempfile.TemporaryDirectory() as folder:
        script = Path(folder) / "gen.py"
        script.write_text(generator(), encoding="utf-8")
        target = Path(folder) / "appsettings.json"

        done = subprocess.run([sys.executable, str(script), str(target)],
                              env=env, capture_output=True, text=True, check=True)
        written = json.loads(target.read_text(encoding="utf-8")) if target.exists() else {}
        return written, done.stdout, done.stderr


class SettingsFileTests(unittest.TestCase):
    """What lands in `appsettings.json`, which is the engine's only configuration source."""

    def test_the_credential_alone_still_produces_what_it_always_did(self):
        """Every existing deployment sets exactly this and nothing else. It must not change shape."""
        settings, _, _ = write_settings({"POLSON_AGENT_PLATFORM_KEY": "k"})

        self.assertEqual({"ApiKeys": {"GoogleAgentPlatform": "k"}}, settings)

    def test_the_research_key_is_written_so_research_is_not_silently_disabled(self):
        """The regression this exists for. Absent, the engine logs 'Research disabled' and every
        figure a hosted run draws is unsourced — while the same workflow researches fine locally."""
        settings, _, _ = write_settings({
            "POLSON_AGENT_PLATFORM_KEY": "k", "POLSON_PARALLEL_KEY": "p"})

        self.assertEqual("p", settings["ApiKeys"]["Parallel"])

    def test_a_budget_can_finally_be_set_on_a_deployment(self):
        settings, _, _ = write_settings({
            "POLSON_AGENT_PLATFORM_KEY": "k", "POLSON_ASSETS_BUDGET": "40"})

        self.assertEqual(40, settings["Assets"]["Budget"])

    def test_a_numeric_setting_is_written_as_a_number(self):
        """Matching `appsettings.json.example`. A quoted number parses too, but the file is read by
        people as well as by the engine, and two shapes for one setting invites a wrong edit."""
        settings, _, _ = write_settings({
            "POLSON_AGENT_PLATFORM_KEY": "k", "POLSON_PHOTOS_BUDGET": "12"})

        self.assertIsInstance(settings["Photos"]["Budget"], int)

    def test_a_budget_that_is_not_a_number_is_reported_and_dropped(self):
        """Not written through. `int.TryParse` leaves 0 on failure and a budget of zero disables the
        surface outright, so a typo would present as 'requisition is broken' with nothing saying why.
        """
        settings, _, stderr = write_settings({
            "POLSON_AGENT_PLATFORM_KEY": "k", "POLSON_ASSETS_BUDGET": "lots"})

        self.assertNotIn("Assets", settings)
        self.assertIn("POLSON_ASSETS_BUDGET", stderr)

    def test_an_unset_variable_writes_no_key_rather_than_an_empty_one(self):
        """`Setting()` treats blank as absent, but an empty string in the file would still override
        a default in every other reader — and would resolve a cache directory to nowhere."""
        settings, _, _ = write_settings({
            "POLSON_AGENT_PLATFORM_KEY": "k", "POLSON_ASSETS_MODEL": "   "})

        self.assertNotIn("Assets", settings)

    def test_the_log_names_the_settings_and_never_their_values(self):
        """It goes to a log aggregator and one of the values is a credential."""
        _, stdout, _ = write_settings({
            "POLSON_AGENT_PLATFORM_KEY": "sk-do-not-log-me", "POLSON_PARALLEL_KEY": "p-secret"})

        self.assertIn("ApiKeys:GoogleAgentPlatform", stdout)
        self.assertNotIn("sk-do-not-log-me", stdout)
        self.assertNotIn("p-secret", stdout)

    def test_a_value_carrying_a_quote_survives_as_data(self):
        """The reason this is Python and not `printf`: a key or a prompt with a quote in it would
        otherwise close the string and produce a file the engine cannot parse."""
        settings, _, _ = write_settings({
            "POLSON_AGENT_PLATFORM_KEY": 'a"b\\c', "POLSON_PHOTOS_USER_AGENT": 'Polson/1.0 "beta"'})

        self.assertEqual('a"b\\c', settings["ApiKeys"]["GoogleAgentPlatform"])
        self.assertEqual('Polson/1.0 "beta"', settings["Photos"]["UserAgent"])

    def test_the_entrypoint_can_set_every_key_the_engine_reads(self):
        """The drift guard, and the whole point.

        A key the engine reads and the entrypoint cannot write is a setting that silently cannot be
        configured on a deployment — which is what this fix was for. Adding a `Setting(...)` to the
        engine means adding a variable here.
        """
        source = generator()
        missing = sorted(key for key in ENGINE_KEYS if f'"{key}"' not in source)

        self.assertEqual([], missing,
                         "the engine reads these and the container has no way to set them")


class CredentialNormalisationTests(unittest.TestCase):
    """Trailing whitespace on a secret, removed before either half of the studio reads it.

    Secret Manager stores bytes verbatim and gcloud trims nothing, so a secret created at a console
    very often ends in a newline — the recipe is paste, Enter, Ctrl-Z, Enter, and that Enter is in
    the value. `polson-agent-key` measured **55 bytes for a 54-character key** and had always
    "worked", because the model client tolerates trailing whitespace. That is not a guarantee.

    Trimming in one place only would be worse than not trimming: the engine reads the credential
    from the generated `appsettings.json` and the agent reads it from `GOOGLE_API_KEY`, so a
    half-fix yields a container where one authenticates and the other does not.
    """

    def normalise(self, env: dict[str, str]) -> dict[str, str]:
        """Runs the entrypoint's normalisation block under a real POSIX shell.

        Values come back **inside markers** rather than one per line. Printing them with a trailing
        newline and splitting on newlines is the obvious harness and it is blind to exactly the
        defect under test: an untrimmed `par456\n` and a trimmed `par456` produce the same field
        once you split. An earlier version of this did that, and a mutation removing the second
        credential from the loop passed all eight tests.
        """
        body = ENTRYPOINT.read_text(encoding="utf-8")
        start = body.index("for _credential in ")
        block = body[start:body.index("\nunset _credential", start)]

        script = (block + "\nunset _credential _value _trimmed\n"
                  + 'printf "agent=<%s>\n" "${POLSON_AGENT_PLATFORM_KEY:-}"\n'
                  + 'printf "parallel=<%s>\n" "${POLSON_PARALLEL_KEY:-}"\n')

        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "norm.sh"
            path.write_text(script, encoding="utf-8", newline="\n")
            done = subprocess.run(["sh", str(path)], env=env,
                                  capture_output=True, text=True, check=True)

        return {"out": done.stdout, "stderr": done.stderr}

    def assertValue(self, out: dict[str, str], name: str, expected: str) -> None:
        self.assertIn(f"{name}=<{expected}>", out["out"],
                      f"{name} was not exactly {expected!r} — got {out['out']!r}")

    def test_a_trailing_newline_is_removed(self):
        """The measured case: 55 bytes stored for a 54-character key."""
        self.assertValue(self.normalise({"POLSON_AGENT_PLATFORM_KEY": "abc123\n"}), "agent", "abc123")

    def test_both_credentials_are_normalised_not_just_the_first(self):
        """The whole point. One trimmed and one not is the split-brain this prevents."""
        out = self.normalise({
            "POLSON_AGENT_PLATFORM_KEY": "abc123\n", "POLSON_PARALLEL_KEY": "par456\n"})

        self.assertValue(out, "agent", "abc123")
        self.assertValue(out, "parallel", "par456")

    def test_a_windows_written_file_leaves_no_carriage_return(self):
        self.assertValue(self.normalise({"POLSON_PARALLEL_KEY": "par456\r\n"}), "parallel", "par456")

    def test_a_pasted_leading_space_goes_too(self):
        self.assertValue(self.normalise({"POLSON_AGENT_PLATFORM_KEY": "  abc123  "}), "agent", "abc123")

    def test_a_clean_key_is_left_exactly_alone_and_says_nothing(self):
        """No message, so the log line means something when it does appear."""
        out = self.normalise({"POLSON_AGENT_PLATFORM_KEY": "abc123"})

        self.assertValue(out, "agent", "abc123")
        self.assertNotIn("trimmed", out["stderr"])

    def test_trimming_is_reported_so_a_malformed_secret_is_visible(self):
        out = self.normalise({"POLSON_AGENT_PLATFORM_KEY": "abc123\n"})

        self.assertIn("trimmed whitespace from POLSON_AGENT_PLATFORM_KEY", out["stderr"])

    def test_the_message_never_carries_the_credential(self):
        out = self.normalise({"POLSON_AGENT_PLATFORM_KEY": "sk-do-not-log-me\n"})

        self.assertNotIn("sk-do-not-log-me", out["stderr"])

    def test_an_unset_credential_is_not_an_error(self):
        """A deployment may legitimately have no research key, and the container must still boot."""
        out = self.normalise({})

        self.assertValue(out, "agent", "")
        self.assertValue(out, "parallel", "")
if __name__ == "__main__":
    unittest.main()
