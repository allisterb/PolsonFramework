"""Promptable segmentation (SAM 2) for the studio. Image bytes on stdin, prompts as an argument, JSON on stdout.

SPIKE. Same shape and containment as `segment.py`: a separate process, bytes in over one pipe and JSON out
over another, nothing written to disk unless a debug folder is asked for. No C# caller yet.

    python sam2_segment.py <sam2.1_hiera_small.pt> --prompts '<json>' [--overlay-dir <dir>]  < image  > result.json

`--prompts` is a list of {"name", "points": [[x, y], ...], "labels": [1 | 0, ...], "box": [x0, y0, x1, y1]} in
the image's own pixels, with points or box or both. Label 1 means "this is the thing", 0 "this is not".
Segments whatever a prompt points at: a garment, a prop, hair. Works on inked line art as well as paint.

- **A box is reliable.**
- **One click is ambiguous**: a click on a coat may mean the coat or the person wearing it.
- **Several positive and negative clicks together settle it.** Four clicks on Tomas's coat and three on
  his beard and trousers gave the coat alone, predicted IoU 0.97.

The result, one entry per prompt in order:

    {"found": true, "width": W, "height": H, "names": ["none", <prompt names>...],
     "prompts": [{"name", "score", "scores", "share", "mask": "<base64 PNG, 0 or 255>"}],
     "labelMap": "<base64 PNG, one byte per pixel: 0 none, i for prompt i>"}

In `labelMap` a later prompt overwrites an earlier one where they overlap, so list the smaller things last
(the crossbow after the coat). `found` is false only when every mask is empty. An error goes to stderr and
exits non-zero.

The model is SAM 2's own code, `third_party/sam2/`, unedited; see `third_party/README.md`. It imports hydra,
iopath and tqdm at module level, and image inference uses none of them, so this script registers stand-ins
before importing. It reads the YAML model config and builds the model itself, which is all hydra did here.
The checkpoint is loaded with `weights_only=True`. On CPU: about 2.5 s to load, about 3 s to encode an
image, then under 0.5 s per prompt.
"""
import argparse
import base64
import importlib
import importlib.machinery
import io
import json
import os
import sys
import types

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SAM2 = os.path.join(HERE, 'third_party', 'sam2')
#: The SAM 2.1 config for each checkpoint, by the size in its file name.
CONFIGS = {'tiny': 'sam2.1_hiera_t', 'small': 'sam2.1_hiera_s', 'base_plus': 'sam2.1_hiera_b+', 'large': 'sam2.1_hiera_l'}


def _stand_in(name, **attrs):
    module = types.ModuleType(name)
    module.__dict__.update(attrs)
    # torch._dynamo calls importlib.util.find_spec on module names it has seen, and a module without a spec fails it.
    module.__spec__ = importlib.machinery.ModuleSpec(name, None)
    sys.modules[name] = module


class _GlobalHydra:
    @staticmethod
    def instance():
        return _GlobalHydra()

    def is_initialized(self):
        return True


_stand_in('hydra', initialize_config_module=lambda *a, **k: None)
_stand_in('hydra.core')
_stand_in('hydra.core.global_hydra', GlobalHydra=_GlobalHydra)
_stand_in('iopath')
_stand_in('iopath.common')
_stand_in('iopath.common.file_io', g_pathmgr=None)
try:
    import tqdm  # noqa: F401
except ImportError:
    _stand_in('tqdm', tqdm=lambda it, *a, **k: it)
sys.path.insert(0, SAM2)

import torch  # noqa: E402


def _scalar(s):
    if s in ('null', '~', ''):
        return None
    if s in ('true', 'True'):
        return True
    if s in ('false', 'False'):
        return False
    if s.startswith('[') and s.endswith(']'):
        inner = s[1:-1].strip()
        return [_scalar(v.strip()) for v in inner.split(',')] if inner else []
    for kind in (int, float):
        try:
            return kind(s)
        except ValueError:
            pass
    return s


def parse_config(text):
    """Nested maps by indentation, inline lists and scalars: the subset SAM 2's model configs use."""
    root = {}
    stack = [(-1, root)]
    for raw in text.splitlines():
        line = raw.split('#', 1)[0].rstrip()
        if not line.strip():
            continue
        indent = len(line) - len(line.lstrip())
        key, _, value = line.strip().partition(':')
        while stack[-1][0] >= indent:
            stack.pop()
        value = value.strip()
        if value:
            stack[-1][1][key] = _scalar(value)
        else:
            stack[-1][1][key] = {}
            stack.append((indent, stack[-1][1][key]))
    return root


def instantiate(node):
    """What hydra's instantiate does with `_target_`: import the class and call it with the rest, recursively."""
    if isinstance(node, dict):
        args = {k: instantiate(v) for k, v in node.items() if k != '_target_'}
        if '_target_' not in node:
            return args
        module, _, name = node['_target_'].rpartition('.')
        if not module.startswith('sam2.'):
            raise ValueError(f'config names {node["_target_"]!r}, outside the sam2 package')
        return getattr(importlib.import_module(module), name)(**args)
    if isinstance(node, list):
        return [instantiate(v) for v in node]
    return node


def build(checkpoint, config=None, device='cpu'):
    if config is None:
        stem = os.path.basename(checkpoint)
        config = next((c for size, c in CONFIGS.items() if f'hiera_{size}' in stem), None)
        if config is None:
            raise SystemExit(f'cannot tell the model size from {stem!r}; pass --config (one of {", ".join(CONFIGS.values())})')
    path = os.path.join(SAM2, 'sam2', 'configs', 'sam2.1', config + '.yaml')
    cfg = parse_config(open(path, encoding='utf8').read())['model']
    # build_sam2's own additions for image prediction (its apply_postprocessing=True).
    cfg['sam_mask_decoder_extra_args'] = {'dynamic_multimask_via_stability': True,
                                          'dynamic_multimask_stability_delta': 0.05,
                                          'dynamic_multimask_stability_thresh': 0.98}
    model = instantiate(cfg)
    state = torch.load(checkpoint, map_location='cpu', weights_only=True)['model']
    missing, unexpected = model.load_state_dict(state)
    if missing or unexpected:
        raise SystemExit(f'checkpoint does not fit {config}: missing {missing[:5]}, unexpected {unexpected[:5]}')
    return model.to(device).eval()


def png_base64(array):
    buf = io.BytesIO()
    Image.fromarray(array).save(buf, format='PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def main():
    ap = argparse.ArgumentParser(add_help=False)
    ap.add_argument('checkpoint')
    ap.add_argument('--prompts', required=True, help='JSON list of prompts')
    ap.add_argument('--config', default=None, help='a SAM 2.1 config name; read from the checkpoint name otherwise')
    ap.add_argument('--overlay-dir', default=None, help='also write each mask over the image here')
    args = ap.parse_args()

    prompts = json.loads(args.prompts)
    if not isinstance(prompts, list) or not prompts:
        print('--prompts must be a non-empty JSON list', file=sys.stderr)
        return 2
    raw = sys.stdin.buffer.read()
    if not raw:
        print('no image bytes on stdin', file=sys.stderr)
        return 2
    image = Image.open(io.BytesIO(raw)).convert('RGB')
    rgb = np.asarray(image)

    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    from sam2.sam2_image_predictor import SAM2ImagePredictor
    predictor = SAM2ImagePredictor(build(args.checkpoint, args.config, device))
    with torch.inference_mode():
        predictor.set_image(rgb)

    names = ['none']
    label_map = np.zeros(rgb.shape[:2], np.uint8)
    results = []
    for i, p in enumerate(prompts, start=1):
        name = str(p.get('name', f'prompt{i}'))
        points = np.array(p['points'], np.float32) if p.get('points') else None
        labels = np.array(p.get('labels', [1] * len(points)), np.int32) if points is not None else None
        box = np.array(p['box'], np.float32) if p.get('box') else None
        if points is None and box is None:
            print(f'prompt {name!r} has neither points nor a box', file=sys.stderr)
            return 2
        with torch.inference_mode():
            # One mask for a box, three to choose from for clicks, which are ambiguous about extent.
            masks, scores, _ = predictor.predict(point_coords=points, point_labels=labels, box=box,
                                                 multimask_output=box is None)
        best = int(np.argmax(scores))
        mask = masks[best] > 0
        label_map[mask] = i
        names.append(name)
        results.append({'name': name, 'score': round(float(scores[best]), 4),
                        'scores': [round(float(s), 4) for s in scores],
                        'share': round(float(mask.mean()), 5), 'mask': png_base64((mask * 255).astype(np.uint8))})
        if args.overlay_dir:
            os.makedirs(args.overlay_dir, exist_ok=True)
            out = rgb.astype(np.float32).copy()
            out[mask] = 0.45 * out[mask] + 0.55 * np.array([230, 40, 160])
            out[~mask] *= 0.55
            Image.fromarray(out.astype(np.uint8)).save(os.path.join(args.overlay_dir, f'{name}.png'))

    json.dump({'found': any(r['share'] > 0 for r in results), 'width': image.width, 'height': image.height,
               'device': device, 'names': names, 'prompts': results, 'labelMap': png_base64(label_map)},
              sys.stdout, separators=(',', ':'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
