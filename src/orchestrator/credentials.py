"""The one place that knows where the credential lives, and which endpoint it belongs to.

**One home, deliberately: `ApiKeys:GoogleAgentPlatform` in `bin/cli/appsettings.json`** — the copy
beside the built CLI, not the one at the repository root. `appsettings.json.example` there is a
template to copy, read by nothing.

There is no environment override, and that is the point. The .NET MCP server reads only that file, so
an override honoured here and not there would let the orchestrator authenticate with one key while
asset requisition inside the engine used another — or none, silently, with the failure appearing much
later as a refusal from `Assets.material(...)`. Two halves of one studio cannot have two answers to
"which key".

**That key is an Agent Platform credential, not a public Gemini one.** Pointed at
`generativelanguage.googleapis.com` it returns `403 ... are blocked`, which is the default the SDK
would otherwise take. `vertex=True` selects the Agent Platform endpoint — the same choice the .NET
side makes with `new Client(enterprise: true, ...)`. Public keys start `AIza`; this one does not.

The file lives in build output, which is gitignored and therefore never committed — and also never
restored by a fresh clone. A machine that has built Polson but never been given a key fails at the
first run rather than at setup, so the messages below name the path.
"""

from __future__ import annotations

import json
import os
import re
from pathlib import Path

#: Repository root, from `src/orchestrator/credentials.py` — two levels up, not three.
#:
#: This package moved out of `src/webapp/` so the ADK runtime does not depend on the Antigravity
#: tree; the depth moved with it. A wrong count here does not raise — it resolves to a directory
#: that simply has no `bin/cli`, so the CLI reads as *not built* and every test that needs it skips.
REPO_ROOT = Path(__file__).resolve().parents[2]

#: The engine, where `POLSON_CLI_DLL` puts it, else the checkout's build. The container sets it: there this package is
#: `/app/orchestrator`, two levels up is `/`, and every studio deploy looked for `/bin/cli` — so `techniques --json`
#: failed and the composer's "Graphic design" field was missing on every one. The settings file is the one beside the
#: engine either way, so the two halves still read one file.
CLI_DLL = Path(os.environ.get("POLSON_CLI_DLL", "").strip() or REPO_ROOT / "bin" / "cli" / "Polson.CLI.dll")
CLI_DIR = CLI_DLL.parent
CLI_SETTINGS = CLI_DIR / "appsettings.json"

#: The template to copy, for the message that tells someone to copy it.
CLI_SETTINGS_EXAMPLE = REPO_ROOT / "appsettings.json.example"


class CredentialError(Exception):
    """No usable credential, with a message saying where to put one."""


def read_api_key() -> str:
    """Returns the API key from the CLI's settings file, or explains where to put one."""
    if not CLI_SETTINGS.exists():
        raise CredentialError(
            f"No API key. Copy {CLI_SETTINGS_EXAMPLE} to {CLI_SETTINGS} and fill in "
            f"ApiKeys:GoogleAgentPlatform.")

    try:
        settings = json.loads(CLI_SETTINGS.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        raise CredentialError(f"{CLI_SETTINGS} is not valid JSON: {exc}") from exc

    if key := (settings.get("ApiKeys") or {}).get("GoogleAgentPlatform"):
        return key

    raise CredentialError(
        f"{CLI_SETTINGS} has no ApiKeys:GoogleAgentPlatform. The MCP server reads the same file, so "
        f"this is the only place the key goes.")


#: A JSON string, kept as it is; a comment; or a trailing comma, kept as the bracket after it.
_LENIENT = re.compile(r'("(?:\\.|[^"\\])*")|//[^\n]*|/\*.*?\*/|,(\s*[}\]])', re.S)


def read_settings(path: Path | None = None) -> dict | None:
    """The CLI's settings as the engine reads them, or None when the file is missing or unreadable.

    .NET's JSON configuration accepts comments and trailing commas, and the file is hand-edited, so a strict
    `json.loads` disagrees with the engine about a file the engine reads happily (2026-11-13: one trailing comma).
    """
    path = path or CLI_SETTINGS
    try:
        text = path.read_text(encoding="utf-8-sig")
        return json.loads(_LENIENT.sub(lambda m: m.group(1) or m.group(2) or "", text))
    except (OSError, ValueError):
        return None


def qloo_enabled(path: Path | None = None) -> bool:
    """Whether the engine's `Taste` tool is on, by the rule `Program.ConfigureTaste` applies to the same file:
    `Qloo:Enabled` absent, blank or true, and a non-blank `ApiKeys:Qloo`. Anything but true or false in the switch
    is off, as there. A missing or unreadable file is off: the engine would have no key either."""
    settings = read_settings(path)
    if not isinstance(settings, dict):
        return False
    switch = (settings.get("Qloo") or {}).get("Enabled") if isinstance(settings.get("Qloo"), dict) else None
    if switch is not None and str(switch).strip().lower() not in ("", "true"):
        return False
    key = (settings.get("ApiKeys") or {}).get("Qloo") if isinstance(settings.get("ApiKeys"), dict) else None
    return isinstance(key, str) and bool(key.strip())


def is_public_key(key: str) -> bool:
    """Whether this looks like a public Gemini key rather than an Agent Platform one.

    Only a hint for a clearer error message — the endpoints reject the wrong key type themselves,
    and they are the authority.
    """
    return key.startswith("AIza")
