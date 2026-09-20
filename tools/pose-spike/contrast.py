"""SPIKE: contrast against anatomy. Which one is the detector actually gated on?

The first sweep found that `silhouette + real head/neck` fails at #b9ad99 on #e8e4dc and succeeds
at #2a2a2a on #ffffff. Same geometry both times, so the variable is not the drawing - but "black
on white" changed BOTH the figure and the ground, so this separates them and then re-tests every
geometry at the winning contrast.
"""
import io
import json
import os
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CLI = os.path.join(ROOT, "bin", "cli", "Polson.CLI.exe")
PY = os.path.join(ROOT, "python-mediapipe", "Scripts", "python.exe")
PROBE = os.path.join(ROOT, "src", "vision", "pose_landmarks.py")
MODEL = os.path.join(ROOT, "models", "pose_landmarker_full.task")
SCRATCH = os.path.dirname(os.path.abspath(__file__))

POSES = {
    "standing": {},
    "lunge": {"pose": {"spineDeg": 8,
                       "leftLeg": {"hipDeg": 62, "kneeDeg": 30},
                       "rightLeg": {"hipDeg": 108, "kneeDeg": -25},
                       "leftArm": {"shoulderDeg": 140, "elbowDeg": -40},
                       "rightArm": {"shoulderDeg": 35, "elbowDeg": 50}}},
    "arm up": {"pose": {"leftArm": {"shoulderDeg": -60, "elbowDeg": -35}}},
}


def lum(hexcol):
    r, g, b = (int(hexcol[i:i + 2], 16) / 255 for i in (1, 3, 5))
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)


def contrast(a, b):
    la, lb = lum(a), lum(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


tmpl = io.open(os.path.join(SCRATCH, "render_case.js"), encoding="utf-8").read()


def run(tag, case):
    js = os.path.join(SCRATCH, "c.js")
    png = os.path.join(SCRATCH, "c_%s.png" % tag.replace(" ", "_").replace("/", "-"))
    io.open(js, "w", encoding="utf-8").write(tmpl.replace("__CASE__", json.dumps(case)))
    subprocess.run([CLI, "eval", js, "--width", "700", "--height", "900", "--img", png,
                    "--format", "png", "--svg", os.path.join(SCRATCH, "x.svg")],
                   capture_output=True, cwd=ROOT)
    d = subprocess.run([PY, PROBE, MODEL], input=io.open(png, "rb").read(),
                       capture_output=True, cwd=ROOT)
    res = json.loads(d.stdout.decode("utf-8"))
    lm = res.get("landmarks", {})
    vis = [p["v"] for p in lm.values()] if lm else []
    return res["found"], (sum(vis) / len(vis)) if vis else None, png


print("A. one geometry (silhouette + head, standing), contrast swept")
print("%-22s %-22s %6s  %-6s %s" % ("figure", "ground", "ratio", "found", "visibility"))
print("-" * 74)
pairs = [("#b9ad99", "#e8e4dc"), ("#9c8f79", "#e8e4dc"), ("#6d6353", "#e8e4dc"),
         ("#4a4338", "#e8e4dc"), ("#2a2a2a", "#e8e4dc"), ("#2a2a2a", "#ffffff"),
         ("#b9ad99", "#ffffff"), ("#ffffff", "#2a2a2a")]
for skin, ground in pairs:
    case = dict(name="standing", style="silhouetteHead", skin=skin, ground=ground)
    found, vis, _ = run("A_%s_%s" % (skin[1:], ground[1:]), case)
    print("%-22s %-22s %6.2f  %-6s %s" % (skin, ground, contrast(skin, ground), found,
                                          ("%.3f" % vis) if vis else "-"))

print()
print("B. every geometry at the winning contrast (#2a2a2a on #ffffff)")
print("%-32s %-6s %s" % ("geometry", "found", "visibility"))
print("-" * 56)
for style, label in [("wireframe", "wireframe"), ("solid", "solid (detached masses)"),
                     ("silhouette", "silhouette (unioned)"),
                     ("silhouetteHead", "silhouette + real head/neck")]:
    case = dict(name="standing", style=style, skin="#2a2a2a", ground="#ffffff")
    found, vis, _ = run("B_" + style, case)
    print("%-32s %-6s %s" % (label, found, ("%.3f" % vis) if vis else "-"))

print()
print("C. poses, at the winning contrast and geometry")
print("%-32s %-6s %s" % ("pose", "found", "visibility"))
print("-" * 56)
keep = {}
for name, extra in POSES.items():
    case = dict(name=name, style="silhouetteHead", skin="#2a2a2a", ground="#ffffff", **extra)
    found, vis, png = run("C_" + name, case)
    print("%-32s %-6s %s" % (name, found, ("%.3f" % vis) if vis else "-"))
    keep[name] = {"case": case, "png": png}
io.open(os.path.join(SCRATCH, "cases_kept.json"), "w", encoding="utf-8").write(json.dumps(keep))
