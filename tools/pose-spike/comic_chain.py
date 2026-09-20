"""Does the detector see an ILLUSTRATED figure? The photographs were the control; this is the
actual input - a comic character, which is what a director would supply and what the arranged
route generates.

Two styles, one generation each, three poses per generation. `Assets.cutout` variants rather than
separate calls because generation is not deterministic across calls: one generation is the only
way to get the SAME character in three poses.
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

STYLES = [
    {"slug": "ink", "prompt": "bold black ink line art, clean confident outlines, flat white fill, "
                              "no shading, comic book style"},
    {"slug": "flat", "prompt": "flat colour comic illustration, clear dark outlines, simple cel "
                               "shading, saturated palette"},
]

tmpl = io.open(os.path.join(HERE, "gen_characters.js"), encoding="utf-8").read()

for st in STYLES:
    board = os.path.join(HERE, "comic_%s.png" % st["slug"])
    if not os.path.exists(board):
        js = os.path.join(HERE, "gen_%s.js" % st["slug"])
        io.open(js, "w", encoding="utf-8").write(tmpl.replace("__STYLE__", json.dumps(st)))
        r = subprocess.run([CLI, "eval", js, "--img", board, "--format", "png",
                            "--svg", os.path.join(HERE, "x.svg")], capture_output=True, cwd=ROOT)
        out = r.stdout.decode("utf-8", "replace")
        start = out.find("[JS LOG]")
        print(("--- %s ---\n" % st["slug"]) +
              (out[start:] if start >= 0 else out[-2500:]).encode("ascii", "replace").decode()[:2200])
        if not os.path.exists(board):
            print("generation failed for", st["slug"], file=sys.stderr)
            continue
    else:
        print("--- %s --- (board already present)" % st["slug"])

    # The board is three cells side by side; slice it and test each separately, because a detector
    # given three figures at once would report one pose and say nothing about which.
    from PIL import Image
    im = Image.open(board)
    n = round(im.width / 700.0) or 1
    for i in range(n):
        cell = im.crop((i * 700, 0, (i + 1) * 700, im.height))
        p = os.path.join(HERE, "comic_%s_%d.png" % (st["slug"], i))
        cell.save(p)
        d = subprocess.run([PY, PROBE, MODEL], input=io.open(p, "rb").read(),
                           capture_output=True, cwd=ROOT)
        if d.returncode != 0:
            print("  probe failed:", d.stderr.decode("utf-8", "replace")[-400:])
            continue
        res = json.loads(d.stdout.decode("utf-8"))
        lm = res.get("landmarks", {})
        vis = [q["v"] for q in lm.values()] if lm else []
        print("  %s pose %d: found=%-5s pad=%-4s mean visibility=%s" % (
            st["slug"], i, res["found"], res.get("pad"),
            ("%.3f" % (sum(vis) / len(vis))) if vis else "-"))
        io.open(os.path.join(HERE, "comic_%s_%d.json" % (st["slug"], i)), "w",
                encoding="utf-8").write(json.dumps(res))
