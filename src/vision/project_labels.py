"""Projects 2D label maps of a character's views onto its mesh. JSON on stdin, JSON on stdout.

SPIKE. Same containment as the other vision scripts: data over pipes, nothing written unless a debug folder
is asked for. Pure numpy and Pillow; no model. `garments.py` imports `project` from here.

    python project_labels.py [--debug-dir <dir>]  < request.json  > result.json

The request:

    {"positions": [[x, y, z], ...],          the mesh at rest, glTF units, Y up
     "triangles": [[a, b, c], ...],
     "names": ["background", "upper", ...],  label index -> name; index 0 means "no label"
     "groups": {"garment": ["upper", "full"], "trousers": ["lower"]},   optional; names outside every group -> 0
     "views": [{"name": "front", "image": "<base64 PNG>", "labelMap": "<base64 PNG>",
                "mirrored": false, "weight": 1.0}, ...]}

`labelMap` is the label index per pixel, the `labelMap` `cloth_segment.py` and `sam2_segment.py` return.
`image` is the view itself, drawn on white; its figure's silhouette is what the mesh is aligned to.
`"mirrored": true` also uses that view flipped, at half its weight, for the side of the body it does not
show. That is the usual thing for a sheet's single profile view.

For each view it picks the horizontal axis (x, -x, z or -z) whose projected silhouette best matches the
drawn figure, by IoU of splatted vertices, taking each axis at most once. It aligns by the figure's bounding
box with one scale from its height, rasterises a depth buffer, and labels the vertices that view sees. The IoU
it reports is of that rasterised mesh against the figure, which a coarse mesh's splat would understate. Votes
are summed across views per welded vertex. TRELLIS splits vertices along UV seams, so welding lets the labels
cross them. Vertices no view sees take the label of the nearest labelled vertex, counted in edges.

The result:

    {"names": [...final names, 0 = none...], "labels": [per vertex], "views": [{"name", "axis", "iou", "seen"}],
     "unseen": n, "counts": {"name": vertices}}

Measured on Tomas (lastlight3): IoU 0.82 to 0.83 on front, back and side. 56% of welded vertices were unseen
and filled. The legs modelled inside his coat came out as trousers, spreading up from the visible lower legs.
"""
import argparse
import base64
import io
import json
import os
import sys
from collections import deque

import numpy as np
from PIL import Image

#: (screen right, toward the viewer) for each candidate view axis; right x up = toward the viewer, Y up.
AXES = {'x': (np.array([1, 0, 0.]), np.array([0, 0, 1.])), '-x': (np.array([-1, 0, 0.]), np.array([0, 0, -1.])),
        'z': (np.array([0, 0, 1.]), np.array([-1, 0, 0.])), '-z': (np.array([0, 0, -1.]), np.array([1, 0, 0.]))}
OPPOSITE = {'x': '-x', '-x': 'x', 'z': '-z', '-z': 'z'}


def decode(b64, mode=None):
    img = Image.open(io.BytesIO(base64.b64decode(b64)))
    return img.convert(mode) if mode else img


def figure(img):
    return np.asarray(img.convert('RGB')).astype(np.int32).min(axis=2) < 240


def place(P, right, sil):
    ys, xs = np.nonzero(sil)
    u, v = P @ right, P[:, 1]
    s = (ys.max() - ys.min()) / (v.max() - v.min())
    return (u - (u.min() + u.max()) / 2) * s + (xs.min() + xs.max()) / 2, (v.max() - v) * s + ys.min()


def splat_iou(px, py, sil):
    h, w = sil.shape
    m = np.zeros_like(sil)
    xi, yi = np.clip(px.round().astype(int), 0, w - 1), np.clip(py.round().astype(int), 0, h - 1)
    for dx in range(-3, 4):
        for dy in range(-3, 4):
            m[np.clip(yi + dy, 0, h - 1), np.clip(xi + dx, 0, w - 1)] = True
    return float((m & sil).sum() / max(1, (m | sil).sum()))


def depth_buffer(T, px, py, depth, shape):
    h, w = shape
    z = np.full(shape, -np.inf)
    for a, b, c in T:
        xs, ys = px[[a, b, c]], py[[a, b, c]]
        x0, x1 = max(0, int(np.floor(xs.min()))), min(w - 1, int(np.ceil(xs.max())))
        y0, y1 = max(0, int(np.floor(ys.min()))), min(h - 1, int(np.ceil(ys.max())))
        if x0 > x1 or y0 > y1:
            continue
        den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(den) < 1e-9:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / den
        l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / den
        l2 = 1 - l0 - l1
        inside = (l0 >= -0.01) & (l1 >= -0.01) & (l2 >= -0.01)
        if inside.any():
            sub = z[y0:y1 + 1, x0:x1 + 1]
            np.maximum(sub, np.where(inside, l0 * depth[a] + l1 * depth[b] + l2 * depth[c], -np.inf), out=sub)
    return z


def project(req, debug_dir=None):
    """Labels every vertex of `req`'s mesh from its views; see the module docstring. Raises ValueError on bad input."""
    P = np.array(req['positions'], np.float64)
    T = np.array(req['triangles'], np.int64)
    n = len(P)
    if n == 0 or len(T) == 0 or not req.get('views'):
        raise ValueError('need positions, triangles and at least one view')
    height = P[:, 1].max() - P[:, 1].min()

    # Final label names, and a map from each input label index to its final index.
    names_in = req.get('names') or []
    groups = req.get('groups')
    if groups:
        names = ['none'] + list(groups)
        remap = {i: next((g for g, (_, members) in enumerate(groups.items(), start=1) if nm in members), 0)
                 for i, nm in enumerate(names_in)}
    else:
        names = ['none'] + list(names_in[1:])
        remap = {i: i for i in range(len(names_in))}
    lut = np.zeros(256, np.int64)
    for i, j in remap.items():
        lut[i] = j

    # Weld by position: seams split vertices, and labels must cross them.
    q = np.round(P / height * 1e5).astype(np.int64)
    _, first, canon = np.unique(q, axis=0, return_index=True, return_inverse=True)
    canon = first[canon.ravel()]

    views = []
    for v in req['views']:
        img = v['image'] if isinstance(v['image'], Image.Image) else decode(v['image'])
        lab = v['labelMap'] if isinstance(v['labelMap'], np.ndarray) else np.asarray(decode(v['labelMap'], 'L'))
        if img.size != (lab.shape[1], lab.shape[0]):
            raise ValueError(f'view {v.get("name")!r}: image {img.size} and label map {lab.shape[::-1]} differ in size')
        weight = float(v.get('weight', 1.0))
        views.append((v.get('name', f'view{len(views)}'), img, lab, weight, None))
        if v.get('mirrored'):
            views.append((views[-1][0] + '-mirrored', img.transpose(Image.FLIP_LEFT_RIGHT), lab[:, ::-1], weight / 2,
                          views[-1][0]))

    votes = np.zeros((n, len(names)))
    used, chosen, report = set(), {}, []
    for name, img, lab, weight, mirror_of in views:
        sil = figure(img)
        if not sil.any():
            raise ValueError(f'view {name!r} has no figure: nothing darker than near-white')
        if mirror_of is not None:
            axis = OPPOSITE[chosen[mirror_of]]
        else:
            scores = {a: splat_iou(*place(P, r, sil), sil) for a, (r, _) in AXES.items() if a not in used}
            axis = max(scores, key=scores.get)
        right, toward = AXES[axis]
        px, py = place(P, right, sil)
        used.add(axis)
        chosen[name] = axis
        depth = P @ toward
        z = depth_buffer(T, px, py, depth, sil.shape)
        # Reported from the rasterised triangles, not the splat: a coarse mesh's splat leaves gaps, which would read as
        # a poor fit (the warden's 18k-vertex mesh splatted to 0.61 against her front view).
        covered = z > -np.inf
        iou = float((covered & sil).sum() / max(1, (covered | sil).sum()))
        h, w = sil.shape
        xi, yi = np.clip(px.round().astype(int), 0, w - 1), np.clip(py.round().astype(int), 0, h - 1)
        visible = depth >= z[yi, xi] - 0.01 * height
        got = lut[lab[yi, xi]]
        votes[visible, got[visible]] += weight
        report.append({'name': name, 'axis': axis, 'iou': round(iou, 3), 'seen': int(visible.sum())})

    per_weld = np.zeros_like(votes)
    np.add.at(per_weld, canon, votes)
    seen = per_weld.sum(axis=1) > 0
    label = np.full(n, -1)
    label[seen] = per_weld[seen].argmax(axis=1)
    neighbours = [[] for _ in range(n)]
    for a, b, c in T:
        for u, v in ((a, b), (b, c), (c, a)):
            u, v = canon[u], canon[v]
            neighbours[u].append(v)
            neighbours[v].append(u)
    reps = np.unique(canon)
    queue = deque(r for r in reps if label[r] >= 0)
    while queue:
        u = queue.popleft()
        for v in neighbours[u]:
            if label[v] < 0:
                label[v] = label[u]
                queue.append(v)
    unseen = int((~seen[reps]).sum())
    label[label < 0] = 0
    final = label[canon]

    overlays = {}
    palette = np.array([[150, 150, 150], [230, 180, 20], [40, 160, 60], [40, 120, 230], [220, 50, 150],
                        [120, 70, 200], [240, 120, 40]], np.uint8)
    for name, img, lab, weight, mirror_of in views:
        if mirror_of is not None:
            continue
        right, toward = AXES[chosen[name]]
        px, py = place(P, right, figure(img))
        out = np.asarray(img.convert('RGB')).copy() // 3 + 170
        h, w = out.shape[:2]
        for i in np.argsort(P @ toward):
            x, y = int(round(px[i])), int(round(py[i]))
            out[max(0, y - 1):min(h, y + 2), max(0, x - 1):min(w, x + 2)] = palette[final[i] % len(palette)]
        overlays[name] = Image.fromarray(out)
        if debug_dir:
            os.makedirs(debug_dir, exist_ok=True)
            overlays[name].save(os.path.join(debug_dir, f'labels-{name}.png'))

    return {'names': names, 'labels': final.tolist(), 'views': report, 'unseen': unseen,
            'counts': {nm: int((final == i).sum()) for i, nm in enumerate(names)}, 'overlays': overlays}


def main():
    ap = argparse.ArgumentParser(add_help=False)
    ap.add_argument('--debug-dir', default=None, help='also write each view with every vertex coloured by its label')
    args = ap.parse_args()
    try:
        result = project(json.load(sys.stdin), args.debug_dir)
    except ValueError as e:
        print(e, file=sys.stderr)
        return 2
    result.pop('overlays')
    json.dump(result, sys.stdout, separators=(',', ':'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
