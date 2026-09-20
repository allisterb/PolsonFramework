"""SPIKE step 4: a real photograph through the full chain.

No ground truth, so nothing is scored. What this answers is whether the chain survives an input a
director would actually supply - clothing, hair, foreshortening, a background, and a body built to
nothing like an 8-head canon.
"""
import io
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
CLI = os.path.join(ROOT, "bin", "cli", "Polson.CLI.exe")
PY = os.path.join(ROOT, "python-mediapipe", "Scripts", "python.exe")
PROBE = os.path.join(ROOT, "src", "vision", "pose_landmarks.py")
MODEL = os.path.join(ROOT, "models", "pose_landmarker_full.task")

SLUG = sys.argv[1] if len(sys.argv) > 1 else "photo_bolt"
png = os.path.join(HERE, SLUG + ".png")
if not os.path.exists(png):
    print("missing %s - run fetch_photos.py first (it spends from the Photo budget)" % png,
          file=sys.stderr)
    sys.exit(1)

d = subprocess.run([PY, PROBE, MODEL], input=io.open(png, "rb").read(),
                   capture_output=True, cwd=ROOT)
if d.returncode != 0:
    print(d.stderr.decode("utf-8", "replace")[-2000:], file=sys.stderr)
    sys.exit(1)
res = json.loads(d.stdout.decode("utf-8"))
if not res["found"]:
    print("not detected:", res.get("reason"), file=sys.stderr)
    sys.exit(1)

rel = os.path.relpath(png, ROOT).replace(os.sep, "/")
mapjs = io.open(os.path.join(HERE, "pose_map.js"), encoding="utf-8").read()
tail = io.open(os.path.join(HERE, "photo_tail.js"), encoding="utf-8").read()
combined = (mapjs + "\nconst DETECTED = " + json.dumps(res) + ";\n"
            + "const PHOTO = " + json.dumps(rel) + ";\n" + tail)
js = os.path.join(HERE, "photo_chain.js")
io.open(js, "w", encoding="utf-8").write(combined)

w = res["width"] * 3 + 64
h = res["height"] + 62
out = os.path.join(HERE, SLUG + "-chain.png")
r = subprocess.run([CLI, "eval", js, "--width", str(w), "--height", str(h),
                    "--img", out, "--format", "png", "--svg", os.path.join(HERE, "x.svg")],
                   capture_output=True, cwd=ROOT)
o = r.stdout.decode("utf-8", "replace")
start = o.find("[JS LOG]")
print((o[start:] if start >= 0 else o[-3000:]).encode("ascii", "replace").decode())
