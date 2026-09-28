"""Fetches the studio's downloaded assets, pinned and verified by hash, and builds the vision venv.

Three things a checkout does not carry, because they are binary, large, or someone else's to publish:

  library  Mesh2Motion's stock bodies and CC0 pose clips (13 MB), from GitHub at a pinned commit, into
           src/Polson.Drawing.Mesh/Library/, where the build copies them next to the assembly.
  models   MediaPipe's face and pose landmarker weights (34 MB), from Google's model storage, into models/.
  venv     python-mediapipe/, the interpreter the face and body detectors run, installed from
           src/vision/requirements.lock.txt with --require-hashes. Needs Python 3.13, which the lock is for.

    py -3.13 tools/fetch-assets.py                     everything, then a smoke test
    py -3.13 tools/fetch-assets.py --only library      just the .glb files
    py -3.13 tools/fetch-assets.py --extras            also the Full pose model and the segmentation spike's
    py -3.13 tools/fetch-assets.py --check             verify what is here and smoke-test it; no network

**Pinned and hashed, not fetched from a branch or a `latest` path**, for the reason fetch-fonts.py gives:
these are untrusted binary data read by native parsers (MediaPipe's TFLite loader), so the control is to
fetch exactly the bytes that were checked and refuse anything else. Every hash below was taken from a copy
checked against its source on 2026-09-26: the .glb files by git blob hash at the pinned commit through
GitHub's API, the models by the MD5 Google's storage reports. A file already present with the right hash is
not fetched again; one present with the wrong hash is refused unless --force, since it may be somebody's own.

Licences: the .glb files are CC0 (see src/Polson.Drawing.Mesh/Library/README.md); the MediaPipe code and
models are Apache 2.0, stated on each model's card rather than beside the weights (see reference/README.md).

Standard library only, so it runs before anything else is installed, in the container build included.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import struct
import subprocess
import sys
import tempfile
import urllib.request
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

#: Mesh2Motion at a fixed commit. Bump deliberately, re-checking the hashes when you do.
MESH2MOTION = "faaebc8c9f60efc72b0d9859006751a09ceb3141"
GITHUB = f"https://raw.githubusercontent.com/Mesh2Motion/mesh2motion-app/{MESH2MOTION}/static/"

#: Google's MediaPipe model storage. The `/1/` in each path is the published version; a new one is a new path.
MEDIAPIPE = "https://storage.googleapis.com/mediapipe-models/"

#: `(group, url, destination relative to its folder, sha256, bytes)`.
LIBRARY = [
    ("library", GITHUB + "models-variation/human/male.glb", "stock/male.glb",
     "c7c445f4309d8883667ca9f85ef6ba226c71f492c827af115c46c52bc450a019", 534004),
    ("library", GITHUB + "models-variation/human/female.glb", "stock/female.glb",
     "2b1c47e5eeebffd5097eb8a52add4ba6556dab85e50fc1c5240d744099bebae1", 1358928),
    ("library", GITHUB + "animations/human-base-animations.glb", "poses/human-base-animations.glb",
     "406eb0a8dc4ab366e623b79b6e3005a4951392e1bda78ae39c1099d31147733c", 5656648),
    ("library", GITHUB + "animations/human-addon-animations.glb", "poses/human-addon-animations.glb",
     "a0d64d555e0d492026b72d58bf8e16c5e86779295f9093e376dcc001915c2c95", 5292804),
]

MODELS = [
    ("models", MEDIAPIPE + "face_landmarker/face_landmarker/float16/1/face_landmarker.task", "face_landmarker.task",
     "64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff", 3758596),
    # Heavy is what Character.detect and the character builder prefer: on a round trip its mean limb error was
    # 20 degrees against Full's 23, and it read an upright figure as closer to upright.
    ("models", MEDIAPIPE + "pose_landmarker/pose_landmarker_heavy/float16/1/pose_landmarker_heavy.task",
     "pose_landmarker_heavy.task", "64437af838a65d18e5ba7a0d39b465540069bc8aae8308de3e318aad31fcbc7b", 30664242),
]

#: Not needed by the studio: Full is only a fallback where Heavy is absent, and the two segmentation models belong
#: to the src/vision/segment.py spike, which found selfie_multiclass out of domain on drawn figures.
EXTRAS = [
    ("models", MEDIAPIPE + "pose_landmarker/pose_landmarker_full/float16/1/pose_landmarker_full.task",
     "pose_landmarker_full.task", "5134a3aad27a58b93da0088d431f366da362b44e3ccfbe3462b3827a839011b1", 9398198),
    ("models", MEDIAPIPE + "image_segmenter/deeplab_v3/float32/1/deeplab_v3.tflite", "deeplab_v3.tflite",
     "ff36e24d40547fe9e645e2f4e8745d1876d6e38b332d39a82f0bf0f5d1d561b3", 2780176),
    ("models", MEDIAPIPE + "image_segmenter/selfie_multiclass_256x256/float32/1/selfie_multiclass_256x256.tflite",
     "selfie_multiclass_256x256.tflite", "c6748b1253a99067ef71f7e26ca71096cd449baefa8f101900ea23016507e0e0", 16371837),
]

#: A glTF binary starts `glTF`; a MediaPipe `.task` is a zip (`PK`); a `.tflite` is a FlatBuffer carrying `TFL3` at 4.
MAGIC = {".glb": (0, b"glTF"), ".task": (0, b"PK"), ".tflite": (4, b"TFL3")}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fetch(url: str, dest: Path, expected: str, size: int, force: bool) -> str:
    """Puts the pinned bytes at `dest`: fetched, present already, or refused. Returns what happened."""
    if dest.exists():
        if sha256(dest) == expected:
            return "present"
        if not force:
            raise SystemExit(
                f"FATAL: {dest} is here but does not match its pinned hash. It may be your own file; move it,\n"
                "       or pass --force to replace it with the pinned one.")

    dest.parent.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256()
    # Written beside the destination and moved into place only once verified, so an interrupted or
    # mismatched download never leaves a file the studio would load.
    fd, tmp = tempfile.mkstemp(dir=dest.parent, prefix=dest.name + ".", suffix=".part")
    try:
        with os.fdopen(fd, "wb") as out, urllib.request.urlopen(url, timeout=300) as response:
            received = 0
            for chunk in iter(lambda: response.read(1 << 20), b""):
                out.write(chunk)
                digest.update(chunk)
                received += len(chunk)
        actual = digest.hexdigest()
        if actual != expected:
            raise SystemExit(
                f"FATAL: {dest.name} does not match its pinned hash.\n"
                f"       url      {url}\n"
                f"       expected {expected} ({size} bytes)\n"
                f"       actual   {actual} ({received} bytes)\n"
                "       Refusing to install bytes that were not the ones reviewed.")
        offset, magic = MAGIC.get(dest.suffix, (0, b""))
        with open(tmp, "rb") as f:
            head = f.read(offset + len(magic))
        if magic and head[offset:] != magic:
            raise SystemExit(f"FATAL: {dest.name} hashes correctly but does not begin like a {dest.suffix} file.")
        os.replace(tmp, dest)
        return "fetched"
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


def venv_python(venv: Path) -> Path:
    return venv / ("Scripts/python.exe" if os.name == "nt" else "bin/python")


def build_venv(venv: Path, lock: Path) -> None:
    """Creates the venv with this interpreter and installs the lock into it, hash-checked, wheels only."""
    if sys.version_info[:2] != (3, 13):
        raise SystemExit(
            f"FATAL: the vision lock is for Python 3.13 and this is {sys.version.split()[0]}.\n"
            "       Run this script with 3.13 (Windows: py -3.13 tools/fetch-assets.py), or pass --only library,models.")
    if not venv_python(venv).exists():
        print(f"  creating {venv}")
        subprocess.run([sys.executable, "-m", "venv", str(venv)], check=True)
    print(f"  installing {lock.name} (hash-checked)")
    subprocess.run([str(venv_python(venv)), "-m", "pip", "install", "--disable-pip-version-check",
                    "--require-hashes", "--only-binary=:all:", "-r", str(lock)], check=True)


def blank_png(width: int = 64, height: int = 64) -> bytes:
    """A white PNG, written with zlib so the smoke test needs nothing installed."""
    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    rows = b"".join(b"\x00" + b"\xff" * (width * 3) for _ in range(height))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))


def smoke(venv: Path, vision: Path, models: Path) -> None:
    """Runs both detectors on a blank image exactly as the studio does: PNG in over stdin, JSON out."""
    python = venv_python(venv)
    if not python.exists():
        raise SystemExit(f"FATAL: no interpreter at {python}. Run without --check, or with --only venv.")
    pose = next((models / m for m in ("pose_landmarker_heavy.task", "pose_landmarker_full.task")
                 if (models / m).exists()), models / "pose_landmarker_heavy.task")
    for script, model in (("face_landmarks.py", models / "face_landmarker.task"), ("pose_landmarks.py", pose)):
        result = subprocess.run([str(python), str(vision / script), str(model)], input=blank_png(),
                                capture_output=True, timeout=300)
        try:
            answer = json.loads(result.stdout)
        except ValueError:
            answer = None
        if result.returncode != 0 or not isinstance(answer, dict) or "found" not in answer:
            err = result.stderr.decode(errors="replace").strip().splitlines()[-5:]
            raise SystemExit(f"FATAL: {script} did not run (exit {result.returncode}).\n       " + "\n       ".join(err))
        print(f"  {script} with {model.name}: ran, found {answer['found']} on a blank image, as it should")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--only", default="library,models,venv",
                        help="comma-separated: library, models, venv (default: all three)")
    parser.add_argument("--extras", action="store_true", help="also the Full pose model and the segmentation models")
    parser.add_argument("--check", action="store_true", help="verify what is present and smoke-test it; no network")
    parser.add_argument("--force", action="store_true", help="replace a present file whose hash is wrong")
    parser.add_argument("--root", type=Path, default=ROOT, help="the checkout (default: the one this script is in)")
    parser.add_argument("--library", type=Path, help="default: <root>/src/Polson.Drawing.Mesh/Library")
    parser.add_argument("--models", type=Path, help="default: <root>/models")
    parser.add_argument("--venv", type=Path, help="default: <root>/python-mediapipe")
    parser.add_argument("--vision", type=Path, help="the lock and detector scripts; default: <root>/src/vision")
    args = parser.parse_args()

    only = {s.strip() for s in args.only.split(",") if s.strip()}
    unknown = only - {"library", "models", "venv"}
    if unknown:
        parser.error(f"--only takes library, models and venv, not {', '.join(sorted(unknown))}")
    library = args.library or args.root / "src" / "Polson.Drawing.Mesh" / "Library"
    models = args.models or args.root / "models"
    venv = args.venv or args.root / "python-mediapipe"
    vision = args.vision or args.root / "src" / "vision"

    wanted = [f for f in LIBRARY if "library" in only] + [f for f in MODELS + (EXTRAS if args.extras else [])
                                                          if "models" in only]
    folders = {"library": library, "models": models}
    missing = []
    for group, url, rel, expected, size in wanted:
        dest = folders[group] / rel
        if args.check:
            state = "ok" if dest.exists() and sha256(dest) == expected else "MISSING" if not dest.exists() else "WRONG HASH"
            if state != "ok":
                missing.append(str(dest))
            print(f"  {state:10} {dest}")
        else:
            print(f"  {fetch(url, dest, expected, size, args.force):10} {dest} ({size / 1e6:.1f} MB)")
    if missing:
        raise SystemExit(f"FATAL: {len(missing)} file(s) missing or wrong. Run without --check to fetch them.")

    if "venv" in only:
        if not args.check:
            build_venv(venv, vision / "requirements.lock.txt")
        smoke(venv, vision, models)
    return 0


if __name__ == "__main__":
    sys.exit(main())
