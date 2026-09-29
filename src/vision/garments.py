"""The garment step of a character build: does anything hang, and which of the mesh's vertices are garment. JSON in, JSON out.

One process per character, so each model loads once. Same containment as the other vision scripts: the views and
the mesh arrive over stdin and the answer leaves over stdout; nothing is written to disk.

    python garments.py <pose_landmarker.task> <sam2.1_hiera_small.pt>  < request.json  > result.json

The request:

    {"views": {"front": "<base64 PNG>", "side": ..., "back": ..., "left": ..., "right": ...},   front required
     "positions": [[x, y, z], ...], "triangles": [[a, b, c], ...],   the rigged mesh at rest, Y up
     "segment": "auto"}                                              or "always"

In order:

1. **Pose** on every view (`pose_landmarks.py`).
2. **`hangs`** on the front and back (`garment_prompts.py`): whether the silhouette fills the strip between the
   thighs. It answers whether the character needs cloth at all. Measured right on all 11 test characters with a pose.
3. With `segment: "auto"` (the default) that is where it stops unless something hangs. Otherwise **SAM 2** takes the
   upper garment and whatever covers the thighs on every view, each from its two candidates by top score, the front
   and back first so their hem can guide the profiles (`garment_prompts.py`, `sam2_segment.py`).
4. **The projection** onto the mesh (`project_labels.py`): each vertex becomes `none`, `garment` (the outermost torso
   garment) or `lower` (what covers the thighs: a skirt, a coat's skirts, or trousers). Where the two overlap in a
   view, `garment` wins.

The result:

    {"hangs": true | false | null, "hangsBy": {"front": ..., "back": ...}, "segmented": bool, "reason": "...",
     "views": {"front": {"pose": bool, "garment": {"sleeves", "hemBelow", "score", "share", "pick"},
                         "lower": {"hemBelow", "score", "share", "pick"}}, ...},     or {"reason"} where one was not tried
     "garment": {"sleeves", "hemBelow"},
     "projection": {"names", "labels", "views", "unseen", "counts"},    only when segmented
     "sheet": "<base64 PNG>",   each view with its masks over it, and below it the mesh's vertices coloured by label
     "ms": {"pose": ..., "sam": ..., "project": ...}}

`hangs` is null when neither the front nor the back has a pose with both hips and knees, or when the two disagree.
Timings on CPU, measured on the lastlight3 characters: see `docs/internal/character-rigging-modes.md` section 7.
"""
import base64
import io
import json
import os
import sys
import time

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import garment_prompts as gp  # noqa: E402
import project_labels  # noqa: E402

#: Profile views, which cannot show the gap between the legs and may not show the hem.
PROFILES = ('side', 'left', 'right')
ORDER = ('front', 'back', 'side', 'left', 'right')
#: The margin a cutout cell (trimmed to its figure) gets, so no figure touches the edge of its frame.
MARGIN = 26


def flat(raw):
    img = Image.open(io.BytesIO(base64.b64decode(raw))).convert('RGBA')
    page = Image.new('RGB', (img.width + 2 * MARGIN, img.height + 2 * MARGIN), (255, 255, 255))
    page.paste(img, (MARGIN, MARGIN), img)
    return page


def silhouette(img):
    return np.asarray(img).min(axis=2) < 240


def unpng(b64):
    return np.asarray(Image.open(io.BytesIO(base64.b64decode(b64))).convert('L')) > 127


def png(img):
    buf = io.BytesIO()
    img.save(buf, format='PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def poses(model, images):
    from mediapipe.tasks import python as mp_python
    from mediapipe.tasks.python import vision
    import pose_landmarks

    opts = vision.PoseLandmarkerOptions(base_options=mp_python.BaseOptions(model_asset_path=model), num_poses=1)
    with vision.PoseLandmarker.create_from_options(opts) as det:
        return {k: pose_landmarks.landmarks(det, img) for k, img in images.items()}


def vote(answers):
    got = [a for a in answers if a is not None]
    return got[0] if got and all(a == got[0] for a in got) else None


def segment(checkpoint, images, pose, figs):
    """SAM's upper and lower garment on every view with a pose. Returns ({view: summary}, {view: (garment, lower)})."""
    import sam2_segment
    import torch
    from sam2.sam2_image_predictor import SAM2ImagePredictor

    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    predictor = SAM2ImagePredictor(sam2_segment.build(checkpoint, None, device))
    summary, masks = {}, {}
    hint = None
    # The front and back first: a profile often cannot show the hem, and theirs is passed to it as a hint.
    for view in sorted(images, key=lambda v: v in PROFILES):
        if not pose[view].get('found'):
            summary[view] = {'pose': False}
            continue
        if view in PROFILES and hint is None:
            hems = [summary[v].get('garment', {}).get('hemBelow') for v in summary if v not in PROFILES]
            hint = 'ankle' if 'ankle' in hems else 'knee' if 'knee' in hems else None
        up = gp.prompts(pose[view], hint if view in PROFILES else None, figs[view])
        lo = gp.lower_prompts(pose[view], figs[view])
        given = (up['prompts'] if up['found'] else []) + (lo['prompts'] if lo['found'] else [])
        entry = {'pose': True}
        summary[view] = entry
        if not given:
            entry['garment'], entry['lower'] = {'reason': up['reason']}, {'reason': lo['reason']}
            continue
        rgb = np.asarray(images[view])
        with torch.inference_mode():
            predictor.set_image(rgb)
        results, _, _ = sam2_segment.run_prompts(predictor, given, rgb, want_all=True)
        seg = {'prompts': results}
        pair = []
        for key, found in (('garment', up), ('lower', lo)):
            if not found['found']:
                entry[key] = {'reason': found['reason']}
                pair.append(None)
                continue
            chosen = gp.choose(found['prompts'], seg)
            mask = unpng(chosen['mask'])
            r = gp.reach(pose[view], mask)
            entry[key] = {'hemBelow': r['hemBelow'], 'score': chosen['score'], 'share': chosen['share'],
                          'pick': f"{chosen['name']}#{chosen['index']}"}
            if key == 'garment':
                entry[key]['sleeves'] = r['sleeves']
            pair.append(mask)
        masks[view] = tuple(pair)
    return summary, masks


def sheet(images, masks, overlays, hangs):
    """Each view with its masks over it (garment magenta, lower cyan), and below it the labelled vertices."""
    H = 360
    columns = []
    for view in images:
        arr = np.asarray(images[view]).astype(np.float32) * 0.6
        garment, lower = masks.get(view, (None, None))
        for mask, colour in ((lower, (30, 200, 230)), (garment, (230, 40, 160))):
            if mask is not None:
                arr[mask] = 0.4 * arr[mask] + 0.6 * np.array(colour)
        top = Image.fromarray(arr.astype(np.uint8))
        tiles = [top] + ([overlays[view]] if view in overlays else [])
        tiles = [t.resize((max(1, round(t.width * H / t.height)), H)) for t in tiles]
        col = Image.new('RGB', (tiles[0].width, H * len(tiles)), 'white')
        for i, t in enumerate(tiles):
            col.paste(t, (0, i * H))
        ImageDraw.Draw(col).text((4, 4), view, fill=(0, 0, 0))
        columns.append(col)
    out = Image.new('RGB', (sum(c.width for c in columns) + 8 * (len(columns) + 1), max(c.height for c in columns) + 28),
                    'white')
    x = 8
    for c in columns:
        out.paste(c, (x, 24))
        x += c.width + 8
    ImageDraw.Draw(out).text((8, 6), f'hangs: {hangs}   garment magenta / lower cyan; '
                                     'vertices: garment yellow, lower green, none grey', fill=(0, 0, 0))
    return out


def main():
    if len(sys.argv) != 3:
        print('usage: garments.py <pose_landmarker.task> <sam2 checkpoint>  (request JSON on stdin)', file=sys.stderr)
        return 2
    req = json.load(sys.stdin)
    raw = req.get('views') or {}
    if 'front' not in raw:
        print('the views must include a front', file=sys.stderr)
        return 2
    unknown = set(raw) - set(ORDER)
    if unknown:
        print(f'unknown view(s) {sorted(unknown)}; expected some of {list(ORDER)}', file=sys.stderr)
        return 2
    mode = req.get('segment', 'auto')
    if mode not in ('auto', 'always'):
        print(f"segment must be 'auto' or 'always', not {mode!r}", file=sys.stderr)
        return 2

    ms = {}
    images = {k: flat(raw[k]) for k in ORDER if k in raw}
    figs = {k: silhouette(img) for k, img in images.items()}
    t = time.time()
    pose = poses(sys.argv[1], images)
    ms['pose'] = round((time.time() - t) * 1000)

    hangs_by = {v: gp.hangs(pose[v], figs[v]) if pose[v].get('found') else None for v in ('front', 'back') if v in pose}
    hangs = vote(hangs_by.values())
    result = {'hangs': hangs, 'hangsBy': hangs_by, 'segmented': False, 'ms': ms}

    if mode == 'auto' and hangs is not True:
        result['reason'] = ('nothing hangs between the legs, so the character needs no cloth' if hangs is False else
                            'whether anything hangs could not be read (no pose on the front or back, or they disagree)')
        result['views'] = {v: {'pose': bool(pose[v].get('found'))} for v in images}
        result['sheet'] = png(sheet(images, {}, {}, hangs))
        json.dump(result, sys.stdout, separators=(',', ':'))
        return 0

    t = time.time()
    summary, masks = segment(sys.argv[2], images, pose, figs)
    ms['sam'] = round((time.time() - t) * 1000)
    result['views'] = summary
    fronts = [summary[v]['garment'] for v in ('front', 'back') if 'hemBelow' in summary.get(v, {}).get('garment', {})]
    hems = [g['hemBelow'] for g in fronts]
    result['garment'] = {
        'sleeves': vote(g['sleeves'] for g in fronts),
        'hemBelow': next((h for h in ('ankle', 'knee', 'hip') if h in hems), None)}

    projection, overlays = None, {}
    if masks and req.get('positions') and req.get('triangles'):
        profiles = [v for v in images if v in PROFILES]
        views = []
        for v, (garment, lower) in masks.items():
            lab = np.zeros(figs[v].shape, np.uint8)
            if lower is not None:
                lab[lower] = 2
            if garment is not None:
                lab[garment] = 1
            # A sheet's single profile also stands in, flipped, for the side it does not show.
            views.append({'name': v, 'image': images[v], 'labelMap': lab, 'mirrored': v in PROFILES and len(profiles) == 1})
        t = time.time()
        try:
            projection = project_labels.project({'positions': req['positions'], 'triangles': req['triangles'],
                                                 'names': ['none', 'garment', 'lower'], 'views': views})
        except ValueError as e:
            print(f'projection: {e}', file=sys.stderr)
            return 2
        ms['project'] = round((time.time() - t) * 1000)
        overlays = projection.pop('overlays')
        result['projection'] = projection
        result['segmented'] = True
    elif not masks:
        result['reason'] = 'no view had a pose to place prompts from'
    else:
        result['reason'] = 'no mesh was given, so nothing was projected'

    result['sheet'] = png(sheet(images, masks, overlays, hangs))
    json.dump(result, sys.stdout, separators=(',', ':'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
