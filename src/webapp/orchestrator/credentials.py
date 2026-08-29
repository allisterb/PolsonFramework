"""The one place that knows where the credential lives, and which endpoint it belongs to.

Both halves of the studio authenticate the same way, so the key has one home: `ApiKeys:GoogleAgentPlatform`
in the CLI's `appsettings.json`. `GEMINI_API_KEY` overrides it for a public key.

**That key is an Agent Platform credential, not a public Gemini one.** Pointed at
`generativelanguage.googleapis.com` it returns `403 ... are blocked`, which is the default the SDK
would otherwise take. `vertex=True` selects the Agent Platform endpoint — the same choice the .NET
side makes with `new Client(enterprise: true, ...)`. Public keys start `AIza`; this one does not.
"""

from __future__ import annotations

import json
import os
from pathlib import Path

#: Repository root, from `src/webapp/orchestrator/credentials.py`.
REPO_ROOT = Path(__file__).resolve().parents[3]

CLI_DIR = REPO_ROOT / "bin" / "cli"
CLI_DLL = CLI_DIR / "Polson.CLI.dll"
CLI_SETTINGS = CLI_DIR / "appsettings.json"


class CredentialError(Exception):
    """No usable credential, with a message saying where to put one."""


def read_api_key() -> str:
    """Returns the API key, preferring `GEMINI_API_KEY` over the CLI's settings file."""
    if env_key := os.environ.get("GEMINI_API_KEY"):
        return env_key

    if not CLI_SETTINGS.exists():
        raise CredentialError(f"no API key: set GEMINI_API_KEY, or put one in {CLI_SETTINGS}")

    try:
        settings = json.loads(CLI_SETTINGS.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        raise CredentialError(f"{CLI_SETTINGS} is not valid JSON: {exc}") from exc

    if key := settings.get("ApiKeys", {}).get("GoogleAgentPlatform"):
        return key

    raise CredentialError(f"{CLI_SETTINGS} has no ApiKeys:GoogleAgentPlatform, and GEMINI_API_KEY is unset")


def is_public_key(key: str) -> bool:
    """Whether this looks like a public Gemini key rather than an Agent Platform one.

    Only a hint for a clearer error message — the endpoints reject the wrong key type themselves,
    and they are the authority.
    """
    return key.startswith("AIza")
