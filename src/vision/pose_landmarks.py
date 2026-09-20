"""Body pose landmark detection for the studio. Image bytes on stdin, JSON on stdout.

SPIKE. Mirrors `face_landmarks.py` in shape and in containment — invoked as a separate process,
never linked; the picture arrives as bytes over a pipe and the answer leaves as JSON over another,
so nothing is written to disk and no path crosses the boundary. It has no C# caller yet: this is a
capability being proven, not a capability shipped.

    python pose_landmarks.py <pose_landmarker.task>  < image-bytes  > result.json

Failure to find a body is a RESULT, not an error: it exits 0 with `found: false` and says what was
tried. An error is an error and goes to stderr with a non-zero exit.

**The one thing here that is not a copy of the face probe is `worldLandmarks`, and it is the whole
reason this route is cheap.** `PoseLandmarkerResult` carries `pose_world_landmarks` — metric 3D
joints in a hip-centred frame — where `FaceLandmarkerResult` carries no world space at all. So a
bone direction is read directly rather than recovered by fitting a body model to 2D points, and
the SMPL-class machinery the commissioned review recommends is not needed to answer "where is the
elbow pointing".
"""
import io
import json
import sys

import numpy as np
from PIL import Image

import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

# The face detector needed a padding ladder because `Assets.cutout` trims a cell to its own extent
# and the face model has a 35-70% scale window. Whether the pose model has the same window is
# UNMEASURED, so the ladder is kept and what it actually used is reported in `pad`. If every real
# image comes back pad=0, this is dead weight and should be cut rather than left in on faith.
PADS = (0.0, 0.25, 0.5)

# BlazePose's 33, named. Only the thirteen a limb rig needs are used downstream, but the whole set
# is emitted: a spike that discards data forces a re-run to ask a new question.
NAMES = [
    "nose", "leftEyeInner", "leftEye", "leftEyeOuter", "rightEyeInner", "rightEye",
    "rightEyeOuter", "leftEar", "rightEar", "mouthLeft", "mouthRight",
    "leftShoulder", "rightShoulder", "leftElbow", "rightElbow", "leftWrist", "rightWrist",
    "leftPinky", "rightPinky", "leftIndex", "rightIndex", "leftThumb", "rightThumb",
    "leftHip", "rightHip", "leftKnee", "rightKnee", "leftAnkle", "rightAnkle",
    "leftHeel", "rightHeel", "leftFootIndex", "rightFootIndex",
]


def detect(det, rgb):
    """Try the padding ladder; return (result, pad, framedW, framedH) or (None, None, w, h)."""
    h, w = rgb.shape[:2]
    for pad in PADS:
        m = int(max(w, h) * pad)
        if m == 0:
            framed = rgb
        else:
            framed = np.full((h + 2 * m, w + 2 * m, 3), 255, dtype=np.uint8)
            framed[m:m + h, m:m + w] = rgb

        r = det.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=framed))
        if r.pose_landmarks:
            return r, m, framed.shape[1], framed.shape[0]
    return None, None, w, h


def main():
    if len(sys.argv) < 2:
        print("usage: pose_landmarks.py <pose_landmarker.task>  (image bytes on stdin)",
              file=sys.stderr)
        return 2

    data = sys.stdin.buffer.read()
    if not data:
        print("no image bytes on stdin", file=sys.stderr)
        return 2

    src = Image.open(io.BytesIO(data))
    # As in the face probe: a cutout carries alpha and the model wants SRGB, so the ground is
    # flattened to a deterministic white rather than left to whatever the conversion decides.
    if src.mode in ("RGBA", "LA", "P"):
        src = src.convert("RGBA")
        flat = Image.new("RGB", src.size, (255, 255, 255))
        flat.paste(src, (0, 0), src.split()[-1])
        src = flat
    rgb = np.asarray(src.convert("RGB"), dtype=np.uint8)

    opts = vision.PoseLandmarkerOptions(
        base_options=mp_python.BaseOptions(model_asset_path=sys.argv[1]),
        num_poses=1,
    )

    with vision.PoseLandmarker.create_from_options(opts) as det:
        r, pad, fw, fh = detect(det, rgb)

        if r is None:
            json.dump({
                "found": False,
                "width": rgb.shape[1], "height": rgb.shape[0],
                "reason": "no body found at any padding",
                "triedPads": list(PADS),
            }, sys.stdout, separators=(",", ":"))
            return 0

        lm = r.pose_landmarks[0]
        # Back into the ORIGINAL image's pixels, which is the space the caller addresses.
        pts = {
            NAMES[i]: {
                "x": round(p.x * fw - pad, 2),
                "y": round(p.y * fh - pad, 2),
                # `visibility` is the model's own confidence that the joint is in frame and not
                # occluded. A rig that ignores it will happily build an arm from a guess, so it is
                # carried through and the mapping is expected to threshold on it.
                "v": round(getattr(p, "visibility", 0.0) or 0.0, 3),
            }
            for i, p in enumerate(lm) if i < len(NAMES)
        }

        out = {
            "found": True,
            "width": rgb.shape[1], "height": rgb.shape[0],
            "pad": pad,
            "landmarks": pts,
        }

        # The metric 3D set, hip-centred, in METRES rather than pixels. This is what face has no
        # counterpart for. Kept separate from `landmarks` rather than merged, because the two are
        # in different spaces and a single dict inviting `p.x` from either would be a trap.
        if r.pose_world_landmarks:
            out["worldLandmarks"] = {
                NAMES[i]: {"x": round(p.x, 4), "y": round(p.y, 4), "z": round(p.z, 4)}
                for i, p in enumerate(r.pose_world_landmarks[0]) if i < len(NAMES)
            }

        json.dump(out, sys.stdout, separators=(",", ":"))
        return 0


if __name__ == "__main__":
    sys.exit(main())
