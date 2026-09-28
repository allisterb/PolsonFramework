"""Cloth segmentation (U²-Net) for the studio. Image bytes on stdin, JSON on stdout.

SPIKE. Same shape and containment as `segment.py`: a separate process, bytes in over one pipe and JSON out
over another, nothing written to disk unless a debug file is asked for. No C# caller yet.

    python cloth_segment.py <cloth_segm.pth> [--overlay-out <path.png>]  < image-bytes  > result.json

Labels every pixel as background, upper-body clothing, lower-body clothing or full-body clothing. The three
classes are unreliable against each other (one coat splits between upper and full body), so merge upper and
full as "garment", but reliable against the background. Painted character sheets come out clean. Hatched
ink line art mostly fails; use `sam2_segment.py` there.

The result carries the label map itself, as a base64 PNG with one byte per pixel holding the class index
(`names` gives the order), at the input's own size:

    {"found": true, "width": W, "height": H, "names": ["background", "upper", "lower", "full"],
     "shares": {"upper": 0.0, ...}, "labelMap": "<base64 PNG>"}

`found` is false when no pixel is clothing, which is a result, not an error, and exits 0. An error goes to
stderr and exits non-zero.

The checkpoint is a PyTorch pickle, loaded with `weights_only=True`. The network is
`third_party/u2net_cloth/network.py`, unedited; see `third_party/README.md`. The model resizes to
768 x 768, so each image is padded to a square on white first. Otherwise a tall view would be squashed.
"""
import argparse
import base64
import io
import json
import os
import sys

import numpy as np
from PIL import Image

import torch
import torch.nn.functional as F

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'third_party', 'u2net_cloth'))
from network import U2NET  # noqa: E402

NAMES = ['background', 'upper', 'lower', 'full']
COLOURS = np.array([[0, 0, 0], [220, 40, 40], [40, 170, 60], [230, 200, 30]], np.uint8)


def load(checkpoint, device):
    net = U2NET(in_ch=3, out_ch=4)
    state = torch.load(checkpoint, map_location='cpu', weights_only=True)
    net.load_state_dict({k[7:] if k.startswith('module.') else k: v for k, v in state.items()})
    return net.to(device).eval()


def classify(net, img, device):
    """Class index per pixel, at the image's own size."""
    w, h = img.size
    s = max(w, h)
    square = Image.new('RGB', (s, s), (255, 255, 255))
    ox, oy = (s - w) // 2, (s - h) // 2
    square.paste(img, (ox, oy))
    x = torch.from_numpy(np.asarray(square.resize((768, 768), Image.BICUBIC), np.float32) / 255.0).permute(2, 0, 1)
    x = ((x - 0.5) / 0.5).unsqueeze(0).to(device)
    with torch.no_grad():
        out = net(x)[0]
    labels = torch.argmax(F.log_softmax(out, dim=1), dim=1)[0].cpu().numpy().astype(np.uint8)
    full = np.asarray(Image.fromarray(labels).resize((s, s), Image.NEAREST))
    return full[oy:oy + h, ox:ox + w]


def png_base64(array):
    buf = io.BytesIO()
    Image.fromarray(array).save(buf, format='PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def main():
    ap = argparse.ArgumentParser(add_help=False)
    ap.add_argument('checkpoint')
    ap.add_argument('--overlay-out', default=None, help='also write the classes over the image here')
    args = ap.parse_args()

    raw = sys.stdin.buffer.read()
    if not raw:
        print('no image bytes on stdin', file=sys.stderr)
        return 2
    img = Image.open(io.BytesIO(raw)).convert('RGB')
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    labels = classify(load(args.checkpoint, device), img, device)

    counts = np.bincount(labels.ravel(), minlength=4)
    clothed = int(counts[1:].sum())
    if args.overlay_out:
        rgb = np.asarray(img).astype(np.float32)
        on = labels > 0
        rgb[on] = 0.45 * rgb[on] + 0.55 * COLOURS[labels[on]]
        Image.fromarray(rgb.astype(np.uint8)).save(args.overlay_out)

    json.dump({
        'found': clothed > 0,
        'width': img.width, 'height': img.height,
        'names': NAMES,
        'shares': {NAMES[c]: round(float(counts[c]) / labels.size, 5) for c in range(1, 4)},
        'device': device,
        'labelMap': png_base64(labels),
    }, sys.stdout, separators=(',', ':'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
