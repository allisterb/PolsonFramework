"""Fetch the realism ladder. Each CLI run is its own session, so each fetch spends one
photograph from the budget of 24. Three, deliberately: a photograph, a sculpture and a drawing.
"""
import io
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CLI = os.path.join(ROOT, "bin", "cli", "Polson.CLI.exe")
SCRATCH = os.path.dirname(os.path.abspath(__file__))

SUBJECTS = [
    {"name": "Usain Bolt", "slug": "photo_bolt", "width": 800},
    {"name": "David (Michelangelo)", "slug": "photo_david", "width": 800},
    {"name": "Vitruvian Man", "slug": "photo_vitruvian", "width": 800},
]

tmpl = io.open(os.path.join(SCRATCH, "fetch_photo.js"), encoding="utf-8").read()
for s in SUBJECTS:
    png = os.path.join(SCRATCH, s["slug"] + ".png")
    if os.path.exists(png):
        print("have", s["slug"])
        continue
    js = os.path.join(SCRATCH, "fetch_" + s["slug"] + ".js")
    io.open(js, "w", encoding="utf-8").write(tmpl.replace("__SUBJECT__", json.dumps(s)))
    r = subprocess.run([CLI, "eval", js, "--img", png, "--format", "png",
                        "--svg", os.path.join(SCRATCH, "x.svg")], capture_output=True, cwd=ROOT)
    out = r.stdout.decode("utf-8", "replace")
    for line in out.splitlines():
        if "JS LOG" in line or "JS EXIT" in line or "Rendered" in line:
            print(line.strip()[:200].encode("ascii","replace").decode())
    if not os.path.exists(png):
        print("FAILED", s["slug"], file=sys.stderr)
