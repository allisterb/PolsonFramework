"""Does the detector see a DRAWN figure? This is the case that matters - the photographs were
only ever the control.

`Assets.cutout` would be the right source (it is what the arranged route actually produces) but
asset requisition reports a budget of 0 in this CLI context, so these come from `Photo` instead:
public-domain lead images that happen to be drawn full figures rather than photographs.
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

SUBJECTS = [
    # A pure cartoon: flat colour, heavy outline, no anatomy to speak of.
    {"name": "Popeye", "slug": "art_popeye", "width": 800},
    # Painted full figures: drawn rather than photographed, but rendered realistically.
    {"name": "The Blue Boy", "slug": "art_blueboy", "width": 800},
    {"name": "Pinkie (painting)", "slug": "art_pinkie", "width": 800},
    # A poster illustration, half figure, with a distinctive arm.
    {"name": "Uncle Sam", "slug": "art_unclesam", "width": 800},
    # A newspaper comic panel - the literal case, though it may hold several figures.
    {"name": "Little Nemo", "slug": "art_nemo", "width": 800},
]

tmpl = io.open(os.path.join(HERE, "fetch_photo.js"), encoding="utf-8").read()
rows = []

for s in SUBJECTS:
    png = os.path.join(HERE, s["slug"] + ".png")
    if not os.path.exists(png):
        js = os.path.join(HERE, "fetch_" + s["slug"] + ".js")
        io.open(js, "w", encoding="utf-8").write(tmpl.replace("__SUBJECT__", json.dumps(s)))
        subprocess.run([CLI, "eval", js, "--img", png, "--format", "png",
                        "--svg", os.path.join(HERE, "x.svg")], capture_output=True, cwd=ROOT)
    if not os.path.exists(png):
        rows.append((s["name"], "FETCH FAILED", None, None))
        continue

    d = subprocess.run([PY, PROBE, MODEL], input=io.open(png, "rb").read(),
                       capture_output=True, cwd=ROOT)
    if d.returncode != 0:
        rows.append((s["name"], "probe error", None, None))
        continue
    res = json.loads(d.stdout.decode("utf-8"))
    lm = res.get("landmarks", {})
    vis = [q["v"] for q in lm.values()] if lm else []
    rows.append((s["name"], res["found"], res.get("pad"),
                 (sum(vis) / len(vis)) if vis else None))
    io.open(os.path.join(HERE, s["slug"] + ".json"), "w", encoding="utf-8").write(json.dumps(res))

w = max(len(str(r[0])) for r in rows)
print("%-*s  %-8s %-6s %s" % (w, "drawn subject", "found", "pad", "mean visibility"))
print("-" * (w + 34))
for name, found, pad, vis in rows:
    print("%-*s  %-8s %-6s %s" % (w, name, found, pad if pad is not None else "-",
                                  ("%.3f" % vis) if vis is not None else "-"))
