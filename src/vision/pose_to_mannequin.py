"""Read a pose off an image and rebuild it as a Polson mannequin.

    python src/vision/pose_to_mannequin.py <image> [--out <image>] [--model <task>]

`<image>` is a path RELATIVE TO THE PROJECT DIRECTORY, because the sandbox resolves
`Skia.Image.load` against the project root and refuses anything outside it -- so a
path this script could open but the renderer could not would fail halfway, after
the detection had already been spent.

Chains the two halves that already exist:

    pose_landmarks.py  ->  33 landmarks  ->  pose_map.js  ->  createMannequinFigure

The landmarks are written into the script as a prelude rather than passed as a
file, because the sandbox cannot read JSON from disk. Output is a three-panel
comparison: source, the recovered mannequin, and the two overlaid.

**What this is for is a STARTING pose an author then corrects, not a
transcription.** On ordinary poses it recovers to 2-6 degrees; on raised or
foreshortened limbs it can be 60 degrees out and confident about it. See
`docs/internal/pose-to-mannequin.md` for what it does and does not survive --
in particular that the detector wants a DARK figure on a LIGHT ground, which is
polarity rather than contrast.

Failure to detect is a RESULT: it prints why and exits 1 without rendering.
"""
import argparse
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))


def main():
    ap = argparse.ArgumentParser(add_help=True, description=__doc__)
    ap.add_argument("image", help="image path, relative to the project directory")
    ap.add_argument("--out", default=None,
                    help="output image (default: alongside the input, as <name>_pose.png)")
    ap.add_argument("--model", default=os.path.join(ROOT, "models", "pose_landmarker_full.task"))
    ap.add_argument("--python", default=sys.executable,
                    help="the interpreter carrying mediapipe (default: the one running this)")
    ap.add_argument("--cli", default=os.path.join(ROOT, "bin", "cli", "Polson.CLI.exe"))
    args = ap.parse_args()

    rel = args.image.replace("\\", "/").lstrip("/")
    src = os.path.join(ROOT, rel.replace("/", os.sep))
    if not os.path.isfile(src):
        print(f"no such image under the project directory: {rel}", file=sys.stderr)
        return 2
    if not os.path.isfile(args.model):
        print(f"no pose model at {args.model}. The fetch command is in .gitignore "
              f"beside the face one.", file=sys.stderr)
        return 2

    with open(src, "rb") as f:
        raw = f.read()

    #: stderr is kept OFF the pipe: mediapipe writes XNNPACK and feedback-manager
    #: warnings there on every run, and merging them corrupts the JSON.
    detect = subprocess.run(
        [args.python, os.path.join(HERE, "pose_landmarks.py"), args.model],
        input=raw, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    if detect.returncode != 0 or not detect.stdout:
        print("pose_landmarks.py produced nothing; run it directly to see its stderr",
              file=sys.stderr)
        return 1

    data = json.loads(detect.stdout)
    if not data.get("found"):
        print(f"no pose found: {data.get('reason', '(no reason given)')}", file=sys.stderr)
        return 1

    out = args.out or f"{os.path.splitext(rel)[0]}_pose.png"
    script = os.path.join(HERE, "_pose_run.js")
    try:
        with open(script, "w", encoding="utf-8") as f:
            f.write(f"const SRC = {json.dumps(rel)};\n")
            f.write(f"const LM = {json.dumps(data['landmarks'])};\n\n")
            with open(os.path.join(HERE, "pose_map.js"), encoding="utf-8") as m:
                f.write(m.read())

        run = subprocess.run([args.cli, "eval", script, "--img", out, "--format", "png"],
                             cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                             text=True, encoding="utf-8", errors="replace")
    finally:
        if os.path.exists(script):
            os.remove(script)

    for line in run.stdout.splitlines():
        if "[JS LOG]" in line:
            print(line.split("[JS LOG]", 1)[1].rstrip())
        elif "saved" in line or "Error" in line or "Failed" in line:
            print(line.strip())
    return run.returncode


if __name__ == "__main__":
    sys.exit(main())
