"""Face landmark detection for the studio. Image bytes on stdin, JSON on stdout.

Invoked as a separate process by `FaceDetector`, never linked. **Nothing is written to disk and
no path crosses the boundary**: the picture arrives as bytes over a pipe and the answer leaves as
JSON over another, which keeps the sandbox's containment rules irrelevant to this path rather
than merely satisfied by it. The one argument is the model bundle, supplied by the host.

    python face_landmarks.py <model.task>  < image-bytes  > result.json

Failure to find a face is a RESULT, not an error: it exits 0 with `found: false` and says what
was tried. An error is an error and goes to stderr with a non-zero exit.
"""
import io
import json
import math
import sys

import numpy as np
from PIL import Image

import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

# **The detector has a scale window and this is the whole reason for a retry loop.** Measured on a
# generated comic portrait: a face filling 100% of the frame is NOT found, 39-66% is, and 32% is
# not either. `Assets.cutout` trims every cell to its own extent, so its output always arrives at
# 100% and would always fail — while an ordinary photograph usually sits inside the window
# already. So zero padding is tried first and the frame is only widened if that finds nothing.
#
# Note the asymmetry: padding rescues a face that is too LARGE in frame and can only hurt one that
# is too small. A face below the window needs cropping, which cannot be done without first knowing
# where it is, so that case is reported rather than guessed at.
PADS = (0.0, 0.25, 0.5, 0.75)


def detect(det, rgb):
    """Try the padding ladder; return (result, pad, width, height) or (None, None, w, h)."""
    h, w = rgb.shape[:2]
    for pad in PADS:
        m = int(max(w, h) * pad)
        if m == 0:
            framed = rgb
        else:
            framed = np.full((h + 2 * m, w + 2 * m, 3), 255, dtype=np.uint8)
            framed[m:m + h, m:m + w] = rgb

        r = det.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=framed))
        if r.face_landmarks:
            return r, m, framed.shape[1], framed.shape[0]
    return None, None, w, h


def main():
    if len(sys.argv) < 2:
        print("usage: face_landmarks.py <model.task>  (image bytes on stdin)", file=sys.stderr)
        return 2

    data = sys.stdin.buffer.read()
    if not data:
        print("no image bytes on stdin", file=sys.stderr)
        return 2

    src = Image.open(io.BytesIO(data))
    # A cutout carries alpha; the model wants SRGB, so the ground is flattened to white rather
    # than left to become whatever the conversion decides. Measured: alpha is not what defeats
    # detection, but a deterministic ground beats an accidental one.
    if src.mode in ("RGBA", "LA", "P"):
        src = src.convert("RGBA")
        flat = Image.new("RGB", src.size, (255, 255, 255))
        flat.paste(src, (0, 0), src.split()[-1])
        src = flat
    rgb = np.asarray(src.convert("RGB"), dtype=np.uint8)

    opts = vision.FaceLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=sys.argv[1]),
        output_face_blendshapes=True,
        output_facial_transformation_matrixes=True,
        num_faces=1,
    )

    with vision.FaceLandmarker.create_from_options(opts) as det:
        r, pad, fw, fh = detect(det, rgb)

        if r is None:
            json.dump({
                "found": False,
                "width": rgb.shape[1], "height": rgb.shape[0],
                # Two causes, and the second was misreported as the first until 2026-09-24: a drawn
                # profile was taken for a small face, when it is not found at any crop at all.
                "reason": "no face found at any padding: either the face is turned too far - a "
                          "strict profile is not detected, whatever its size - or it is too small "
                          "in the frame, which needs cropping this cannot do without first "
                          "locating it",
                "triedPads": list(PADS),
            }, sys.stdout, separators=(",", ":"))
            return 0

        # Back into the ORIGINAL image's pixels, which is the space a texture is addressed in.
        #
        # **The third value is depth, and it was discarded until 2026-09-24.** The landmark model
        # predicts a z for every point, on roughly the same scale as x and negative toward the
        # camera, so `-z * fw` is depth in the same pixels as x — positive toward the viewer. It is
        # the network's human-face prior rather than a measurement, but it is what turns 468 points
        # into a face that can be turned to profile: a nose that stands out, a brow over the eyes.
        pts = [[round(p.x * fw - pad, 3), round(p.y * fh - pad, 3), round(-p.z * fw, 3)]
               for p in r.face_landmarks[0]]

        out = {
            "found": True,
            "width": rgb.shape[1], "height": rgb.shape[0],
            "pad": pad,
            "landmarks": pts,
        }

        if r.facial_transformation_matrixes:
            m = r.facial_transformation_matrixes[0]
            # Pose only: the solver's own header states uniform scale, rotation and translation,
            # so there is no shape term here and none should be read into it.
            out["yawDeg"] = round(math.degrees(math.atan2(-m[2, 0], m[0, 0])), 3)
            out["pitchDeg"] = round(math.degrees(math.asin(max(-1.0, min(1.0, m[2, 1])))), 3)
            out["rollDeg"] = round(math.degrees(math.atan2(-m[0, 1], m[1, 1])), 3)

        if r.face_blendshapes:
            out["blendshapes"] = {c.category_name: round(c.score, 4)
                                  for c in r.face_blendshapes[0] if c.category_name != "_neutral"}

        json.dump(out, sys.stdout, separators=(",", ":"))
        return 0


if __name__ == "__main__":
    sys.exit(main())
