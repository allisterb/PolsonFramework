"""Fetch the glTF test fixtures that are too large to commit.

Run from anywhere:

    python tests/fixtures/gltf/fetch.py

All three fixtures are Khronos reference models under CC0, so the licence permits committing them
and only their size argues against it. `SimpleSkin.gltf` is 3.5 KB and IS committed, so the core
reader tests always assert something; these two are 49 KB and 428 KB and are fetched instead, the
same bargain `bin/` strikes for potrace and `models/` for the mediapipe weights.

Idempotent: a fixture already present and the right size is left alone.
"""

import sys
import urllib.error
import urllib.request
from pathlib import Path

BASE = "https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models"

# Size is recorded so a truncated or redirected download is caught here rather than surfacing as a
# baffling parse error inside the reader. It is not a hash: these are upstream files that may be
# revised, and a size mismatch is worth a warning rather than a refusal.
FIXTURES = [
    ("RiggedFigure.glb", f"{BASE}/RiggedFigure/glTF-Binary/RiggedFigure.glb", 50_116),
    ("CesiumMan.glb", f"{BASE}/CesiumMan/glTF-Binary/CesiumMan.glb", 438_044),
]


def main() -> int:
    here = Path(__file__).resolve().parent
    failed = False

    for name, url, expect in FIXTURES:
        target = here / name
        if target.exists() and target.stat().st_size == expect:
            print(f"have    {name} ({expect:,} bytes)")
            continue

        print(f"fetch   {name} ...", end=" ", flush=True)
        try:
            with urllib.request.urlopen(url, timeout=60) as response:
                data = response.read()
        except (urllib.error.URLError, TimeoutError) as ex:
            print(f"FAILED: {ex}")
            failed = True
            continue

        target.write_bytes(data)
        note = "" if len(data) == expect else f"  (expected {expect:,} — upstream may have changed)"
        print(f"{len(data):,} bytes{note}")

    if failed:
        print("\nOne or more fixtures are missing. The tests that need them report themselves as",
              file=sys.stderr)
        print("NOT RUN rather than failing, so a green suite is not evidence they passed.",
              file=sys.stderr)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
