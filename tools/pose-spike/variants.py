"""SPIKE: which rendering of a Polson figure, if any, does BlazePose actually see?

The solid mannequin was not detected at any padding. Before concluding that a drawn figure is
outside the detector's domain, the alternatives have to be tried and the probe itself has to be
shown to work - a probe that never fires looks exactly like a hard target.
"""
import io
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CLI = os.path.join(ROOT, "bin", "cli", "Polson.CLI.exe")
PY = os.path.join(ROOT, "python-mediapipe", "Scripts", "python.exe")
PROBE = os.path.join(ROOT, "src", "vision", "pose_landmarks.py")
MODEL = os.path.join(ROOT, "models", "pose_landmarker_full.task")
SCRATCH = os.path.dirname(os.path.abspath(__file__))

STAND = {"name": "standing"}

VARIANTS = [
    dict(STAND, style="wireframe", tag="wireframe"),
    dict(STAND, style="solid", tag="solid (detached masses)"),
    dict(STAND, style="silhouette", tag="silhouette (unioned)"),
    dict(STAND, style="silhouette", outline=True, tag="silhouette + outline"),
    dict(STAND, style="silhouetteHead", tag="silhouette + real head/neck"),
    dict(STAND, style="silhouetteHead", padding=6, tag="silhouette + head, padded"),
    dict(STAND, style="silhouetteHead", skin="#2a2a2a", ground="#ffffff", tag="black on white"),
    dict(STAND, style="silhouetteHead", fill=0.40, tag="smaller in frame (0.40)"),
    dict(STAND, style="silhouetteHead", fill=0.80, tag="larger in frame (0.80)"),
]

tmpl = io.open(os.path.join(SCRATCH, "render_case.js"), encoding="utf-8").read()
rows = []

for i, v in enumerate(VARIANTS):
    js = os.path.join(SCRATCH, "v%d.js" % i)
    png = os.path.join(SCRATCH, "v%d.png" % i)
    io.open(js, "w", encoding="utf-8").write(tmpl.replace("__CASE__", json.dumps(v)))

    r = subprocess.run([CLI, "eval", js, "--width", "700", "--height", "900",
                        "--img", png, "--format", "png", "--svg", os.path.join(SCRATCH, "x.svg")],
                       capture_output=True, cwd=ROOT)
    if not os.path.exists(png):
        print("render failed:", v["tag"], file=sys.stderr)
        print(r.stdout.decode("utf-8", "replace")[-1500:], file=sys.stderr)
        sys.exit(1)

    d = subprocess.run([PY, PROBE, MODEL], input=io.open(png, "rb").read(),
                       capture_output=True, cwd=ROOT)
    if d.returncode != 0:
        print("detect failed:", v["tag"], file=sys.stderr)
        print(d.stderr.decode("utf-8", "replace")[-1500:], file=sys.stderr)
        sys.exit(1)
    res = json.loads(d.stdout.decode("utf-8"))
    lm = res.get("landmarks", {})
    vis = [p["v"] for p in lm.values()] if lm else []
    rows.append((v["tag"], res["found"], res.get("pad"),
                 (sum(vis) / len(vis)) if vis else None))

# The realism ladder: photograph -> sculpture -> Renaissance drawing. These are the controls, and
# they also say how far from a photograph the detector survives, which is the actual question.
for slug, label in [("photo_bolt", "CONTROL photograph (Bolt)"),
                    ("photo_david", "CONTROL sculpture (David)"),
                    ("photo_vitruvian", "CONTROL drawing (Vitruvian Man)")]:
    p = os.path.join(SCRATCH, slug + ".png")
    if not os.path.exists(p):
        rows.append((label, "missing - NOT run", None, None))
        continue
    d = subprocess.run([PY, PROBE, MODEL], input=io.open(p, "rb").read(),
                       capture_output=True, cwd=ROOT)
    res = json.loads(d.stdout.decode("utf-8"))
    lm = res.get("landmarks", {})
    vis = [q["v"] for q in lm.values()] if lm else []
    rows.append((label, res["found"], res.get("pad"),
                 (sum(vis) / len(vis)) if vis else None))
    io.open(os.path.join(SCRATCH, slug + ".json"), "w", encoding="utf-8").write(json.dumps(res))

w = max(len(r[0]) for r in rows)
print("%-*s  %-8s %-5s %s" % (w, "variant", "found", "pad", "mean visibility"))
print("-" * (w + 34))
for tag, found, pad, vis in rows:
    print("%-*s  %-8s %-5s %s" % (w, tag, found, pad if pad is not None else "-",
                                  ("%.3f" % vis) if vis is not None else "-"))
