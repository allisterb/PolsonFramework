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
from pathlib import Path

#: Repository root, from `src/orchestrator/credentials.py` — two levels up, not three.
#:
#: This package moved out of `src/webapp/` so the ADK runtime does not depend on the Antigravity
#: tree; the depth moved with it. A wrong count here does not raise — it resolves to a directory
#: that simply has no `bin/cli`, so the CLI reads as *not built* and every test that needs it skips.
REPO_ROOT = Path(__file__).resolve().parents[2]

CLI_DIR = REPO_ROOT / "bin" / "cli"
CLI_DLL = CLI_DIR / "Polson.CLI.dll"
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


def is_public_key(key: str) -> bool:
    """Whether this looks like a public Gemini key rather than an Agent Platform one.

    Only a hint for a clearer error message — the endpoints reject the wrong key type themselves,
    and they are the authority.
    """
    return key.startswith("AIza")
