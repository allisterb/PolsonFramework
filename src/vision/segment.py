"""Image segmentation for the studio. Image bytes on stdin, JSON on stdout.

SPIKE. Mirrors `face_landmarks.py` and `pose_landmarks.py` in shape and in
containment -- invoked as a separate process, never linked; the picture arrives as
bytes over a pipe and the answer leaves as JSON over another, so nothing is written
to disk and no path crosses the boundary. No C# caller yet: a capability being
proven, not one shipped.

    python segment.py <model.tflite> [--mask-out <path.png>]  < image-bytes  > result.json

Two models are usable and they answer different questions:

  selfie_multiclass_256x256  background / hair / body-skin / face-skin / clothes /
                             others. Trained on SELFIES, so a full-body drawn figure
                             is outside its domain -- that is the thing to measure
                             rather than assume.
  deeplab_v3                 Pascal VOC, 21 classes including `person`. A whole-figure
                             matte with no sub-regions.

Failure to segment is a RESULT, not an error: exits 0 saying what was tried. An
error is an error, goes to stderr, and exits non-zero.

**mediapipe writes XNNPACK and feedback-manager warnings to stderr on every run**,
so a caller must keep stderr OFF the JSON pipe. Merging them corrupts stdout and
looks like total failure.
"""
import argparse
import io
import json
import sys

import numpy as np
from PIL import Image

import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

#: Pascal VOC and the selfie set share no palette, so one table keyed by label
#: name keeps a mask readable whichever model produced it.
COLOURS = {
    "background": (245, 242, 235), "hair": (40, 30, 70), "body-skin": (235, 175, 130),
    "body_skin": (235, 175, 130), "face-skin": (250, 205, 160), "face_skin": (250, 205, 160),
    "clothes": (60, 110, 190), "others": (200, 70, 60), "others (accessories)": (200, 70, 60),
    "person": (60, 150, 110),
}
FALLBACK = (150, 150, 150)


def main():
    ap = argparse.ArgumentParser(add_help=False)
    ap.add_argument("model")
    ap.add_argument("--mask-out", default=None,
                    help="also write a colourised PNG of the category mask here")
    args = ap.parse_args()

    raw = sys.stdin.buffer.read()
    if not raw:
        print("no image bytes on stdin", file=sys.stderr)
        return 2

    rgb = np.asarray(Image.open(io.BytesIO(raw)).convert("RGB"))
    image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)

    options = vision.ImageSegmenterOptions(
        base_options=mp_python.BaseOptions(model_asset_path=args.model),
        output_category_mask=True,
        output_confidence_masks=False)

    with vision.ImageSegmenter.create_from_options(options) as segmenter:
        labels = list(segmenter.labels)
        result = segmenter.segment(image)

        if result.category_mask is None:
            json.dump({"found": False, "reason": "model returned no category mask",
                       "labels": labels}, sys.stdout, separators=(",", ":"))
            return 0

        #: mediapipe 1.0 returns the category mask as (H, W, 1), not (H, W). Left
        #: as-is it broadcasts to (H, W, 1, 3) the moment a colour is written into
        #: it, which PIL rejects with a shape error naming no cause.
        mask = np.squeeze(result.category_mask.numpy_view())
        total = mask.size
        counts = np.bincount(mask.reshape(-1), minlength=len(labels))

        classes = [
            {"index": i, "label": labels[i] if i < len(labels) else f"class{i}",
             "pixels": int(c), "share": round(float(c) / total, 5)}
            for i, c in enumerate(counts) if c > 0
        ]
        classes.sort(key=lambda c: -c["pixels"])

        if args.mask_out:
            out = np.zeros((*mask.shape, 3), dtype=np.uint8)
            for i in range(len(counts)):
                name = labels[i] if i < len(labels) else ""
                out[mask == i] = COLOURS.get(name.lower(), FALLBACK)
            Image.fromarray(out).save(args.mask_out)

        json.dump({
            "found": True,
            "width": int(rgb.shape[1]), "height": int(rgb.shape[0]),
            "maskWidth": int(mask.shape[1]), "maskHeight": int(mask.shape[0]),
            "labels": labels,
            "classes": classes,
        }, sys.stdout, separators=(",", ":"))
        return 0


if __name__ == "__main__":
    sys.exit(main())
