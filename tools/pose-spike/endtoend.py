"""SPIKE step 3 driver: render -> detect -> map -> rebuild, and report the recovery error.

The mapping is NOT reimplemented here. `pose_map.js` is concatenated with the comparison script so
there is one implementation of the geometry, tested twice - once analytically (`pose_rig.js`,
which puts the figure's own joints in) and once through the detector (here).
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

# Winning render settings from the contrast sweep: dark figure, light ground, unioned silhouette
# with a real head and neck.
LOOK = {"style": "silhouetteFace", "skin": "#2a2a2a", "ground": "#ffffff"}

CASES = [
    dict(LOOK, name="standing"),
    dict(LOOK, name="lean + tilt", tilt={"shoulderTiltDeg": -14, "pelvicTiltDeg": 9},
         pose={"spineDeg": 12}),
    dict(LOOK, name="arm thrown up", pose={"leftArm": {"shoulderDeg": -60, "elbowDeg": -35}}),
    dict(LOOK, name="lunge",
         pose={"spineDeg": 8,
               "leftLeg": {"hipDeg": 62, "kneeDeg": 30}, "rightLeg": {"hipDeg": 108, "kneeDeg": -25},
               "leftArm": {"shoulderDeg": 140, "elbowDeg": -40},
               "rightArm": {"shoulderDeg": 35, "elbowDeg": 50}}),
    dict(LOOK, name="seated-ish", tilt={"pelvicTiltDeg": -4},
         pose={"spineDeg": -6,
               "leftLeg": {"hipDeg": 20, "kneeDeg": 70}, "rightLeg": {"hipDeg": 18, "kneeDeg": 72}}),
]

tmpl = io.open(os.path.join(SCRATCH, "render_case.js"), encoding="utf-8").read()
detected = []

for i, c in enumerate(CASES):
    js = os.path.join(SCRATCH, "e%d.js" % i)
    png = os.path.join(SCRATCH, "e%d.png" % i)
    io.open(js, "w", encoding="utf-8").write(tmpl.replace("__CASE__", json.dumps(c)))
    subprocess.run([CLI, "eval", js, "--width", "700", "--height", "900", "--img", png,
                    "--format", "png", "--svg", os.path.join(SCRATCH, "x.svg")],
                   capture_output=True, cwd=ROOT)
    d = subprocess.run([PY, PROBE, MODEL], input=io.open(png, "rb").read(),
                       capture_output=True, cwd=ROOT)
    res = json.loads(d.stdout.decode("utf-8"))
    if not res["found"]:
        print("NOT DETECTED:", c["name"], file=sys.stderr)
        continue
    detected.append({"case": c, "result": res})

mapjs = io.open(os.path.join(SCRATCH, "pose_map.js"), encoding="utf-8").read()
tail = io.open(os.path.join(SCRATCH, "endtoend_tail.js"), encoding="utf-8").read()
combined = mapjs + "\nconst DETECTED = " + json.dumps(detected) + ";\n" + tail
io.open(os.path.join(SCRATCH, "endtoend.js"), "w", encoding="utf-8").write(combined)

r = subprocess.run([CLI, "eval", os.path.join(SCRATCH, "endtoend.js"),
                    "--width", "1000", "--height", "520",
                    "--img", os.path.join(SCRATCH, "endtoend.png"), "--format", "png",
                    "--svg", os.path.join(SCRATCH, "x.svg")], capture_output=True, cwd=ROOT)
out = r.stdout.decode("utf-8", "replace")
start = out.find("+---")
print(out[start:].encode("ascii", "replace").decode() if start >= 0 else out[-3000:])
