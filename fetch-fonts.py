"""Fetches the typefaces Debian does not package, at a pinned commit, verified by hash.

**Why this exists.** The studio manuals name Playfair Display four times and Georgia six, and Debian
packages neither — nor any conventional Latin didone at all — `fonts-solide-mirage` is a unicase experimental display face and
`fonts-gfs-*` are Greek revivals. So the one gap left after `fonts-recommended`, `fonts-texgyre` and
`fonts-inter` cannot be closed with apt.

**Why it is pinned and hashed rather than fetched from a branch.** `CLAUDE.md`'s guardrails name
this case directly: fonts are *untrusted binary data*, carrying no instructions, so a codepoint scan
says nothing about them — what matters is the robustness of the parser reading them. That reasoning
normally ends with "in managed code a malformed file is a crash rather than a compromise", and here
it does not fully apply: SkiaSharp parses fonts through **native** FreeType. So the control is to
fetch exactly the bytes that were checked, and to fail rather than proceed if they differ.

Pinning to a commit also means a rebuild six months from now installs the same face the design was
made in, rather than whatever `main` has moved to.

Run in the image build; needs nothing but the standard library.
"""

from __future__ import annotations

import hashlib
import sys
import urllib.request
from pathlib import Path

#: Where fontconfig looks for system-wide fonts. Not `~/.local/share/fonts`, which is per-user and
#: would depend on `$HOME` being what we assume for whichever user the container ends up running as.
FONT_DIR = Path("/usr/local/share/fonts")

#: `google/fonts` at a fixed commit. Bump deliberately, re-checking the hashes when you do.
COMMIT = "8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5"
BASE = f"https://raw.githubusercontent.com/google/fonts/{COMMIT}/"

#: `(path within the repo, sha256 of the bytes, name to install as)`.
#: The bracketed filenames are variable fonts — one file carrying a `wght` axis rather than a file
#: per weight. Skia reads them; whether it instances intermediate weights from `ctx.font`'s numeric
#: weight is **not verified here**, so treat bold as something to check in a specimen render.
FONTS = [
    (
        "ofl/playfairdisplay/PlayfairDisplay%5Bwght%5D.ttf",
        "c40f2293766a503bc70cce9e512ef844a4ccb7cbcde792fe2ea31d191917d8d6",
        "PlayfairDisplay[wght].ttf",
    ),
    (
        "ofl/playfairdisplay/PlayfairDisplay-Italic%5Bwght%5D.ttf",
        "a5e26dc5e2e77fb2803a0bf02fd4f81ee136ec8dea863ccdb0c59a263b21378b",
        "PlayfairDisplay-Italic[wght].ttf",
    ),
    # Gelasio is **metrics compatible with Georgia** — the foundry's own words — so a layout
    # measured against Georgia still measures correctly. Georgia is named 6 times across 3 manuals
    # and sits in their fallback chains, and it is a Windows face: without this,
    # `'40px Didot, Georgia, serif'` misses both named faces here and lands on generic serif.
    #
    # It is fetched rather than installed because **`fonts-gelasio` does not exist in Debian** —
    # checked, not assumed, after an earlier note in the Dockerfile recommended exactly that
    # package.
    (
        "ofl/gelasio/Gelasio%5Bwght%5D.ttf",
        "4daecea457258c9ebeb8bc99ed3fd24353618bfad3ea4b93fa0b5d0468fc04e4",
        "Gelasio[wght].ttf",
    ),
    (
        "ofl/gelasio/Gelasio-Italic%5Bwght%5D.ttf",
        "52559e845a4d33514e5f93bb9ae7dbeae1894a53f2c565a15f18af40cd337c09",
        "Gelasio-Italic[wght].ttf",
    ),
]

#: A sanity check on the bytes before they reach FreeType. `0x00010000` is a TrueType outline file
#: and `OTTO` a CFF one; anything else means the download is not a font, whatever its hash.
FONT_MAGIC = (b"\x00\x01\x00\x00", b"OTTO", b"true", b"ttcf")


def fetch(path: str, expected: str, name: str) -> None:
    url = BASE + path
    with urllib.request.urlopen(url, timeout=120) as response:
        payload = response.read()

    actual = hashlib.sha256(payload).hexdigest()
    if actual != expected:
        raise SystemExit(
            f"FATAL: {name} does not match its pinned hash.\n"
            f"       url      {url}\n"
            f"       expected {expected}\n"
            f"       actual   {actual}\n"
            "       Refusing to install bytes that were not the ones reviewed."
        )

    if not payload.startswith(FONT_MAGIC):
        raise SystemExit(
            f"FATAL: {name} hashes correctly but does not begin like a font "
            f"({payload[:4]!r}). Refusing to hand it to FreeType."
        )

    FONT_DIR.mkdir(parents=True, exist_ok=True)
    (FONT_DIR / name).write_bytes(payload)
    print(f"  installed {name} ({len(payload)} bytes, sha256 verified)")


def main() -> int:
    print(f"fetching {len(FONTS)} fonts from google/fonts @ {COMMIT[:12]}")
    for path, expected, name in FONTS:
        fetch(path, expected, name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
