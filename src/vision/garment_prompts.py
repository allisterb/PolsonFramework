"""Garment prompts for SAM 2 from body pose landmarks, and how far a garment's mask reaches. JSON in, JSON out.

SPIKE. Pure geometry: no model, no image. It sits between `pose_landmarks.py` and `sam2_segment.py`:

    python garment_prompts.py prompts  < {"pose": ..., "hemBelow": ..., "figure": "<b64>"}  > prompts.json
    python garment_prompts.py lower    < {"pose": ..., "figure": "<b64>"}                     > prompts.json
    python garment_prompts.py choose   < {"prompts": [...], "segment": <sam2_segment --all>}  > chosen.json
    python garment_prompts.py reach    < {"pose": ..., "mask": "<b64>"}                       > reach.json
    python garment_prompts.py hangs    < {"pose": ..., "figure": "<b64>"}                     > {"hangs": bool}

`prompts` (the upper garment) and `lower` (whatever covers the thighs) return CANDIDATES, with and without stops on
the shins. Pass them to `sam2_segment.py --all` in one call, then `choose` keeps the one SAM scores highest.
`hemBelow` and `figure` (the view's silhouette, 255 on the figure) are optional. `hangs` needs no segmentation: it
reads from the silhouette whether anything hangs between the legs, which is whether the character needs cloth.

`prompts` turns one view's landmarks (`pose_landmarks.py`'s result) into a SAM 2 prompt for the outermost garment on
the torso: "this" clicks on the chest and belly, either side of the centre line, and "not this" clicks on the face,
the chin, the hands, the feet and low on each shin. **Nothing is placed on the arms, and nothing on the legs above
the shin.** Whether a garment has sleeves, and whether it stops at the waist or the knee, is what this is for
finding out. A fixed stop at the knee would cut a long coat there, and a fixed click on the forearm would pull
bare arms into a sleeveless one. So SAM decides the extent from the drawing, and `reach` reads it back.

`reach` takes the pose and a mask (`sam2_segment.py`'s per-prompt `mask`, or any 0/255 PNG) and reports, for each
limb landmark, whether the mask covers it, and from that whether the garment has sleeves and where its hem falls.

Landmarks below `MIN_VISIBILITY` are not used. A prompt needs at least both shoulders or both hips, or it is
refused (`found: false`), which is a result, not an error.
"""
import base64
import io
import json
import sys

import numpy as np
from PIL import Image

MIN_VISIBILITY = 0.5


def _point(lm, name):
    p = lm.get(name)
    return np.array([p['x'], p['y']]) if p and p.get('v', 0) >= MIN_VISIBILITY else None


def _mean(*points):
    got = [p for p in points if p is not None]
    return np.mean(got, axis=0) if got else None


def _inside(p, towards, figure):
    """`p`, pulled toward `towards` until it sits inside `figure` with a few pixels to spare; `p` if no figure."""
    if figure is None:
        return p
    h, w = figure.shape
    for t in np.linspace(0.0, 1.0, 21):
        q = p + t * (towards - p)
        x, y = int(round(q[0])), int(round(q[1]))
        if 3 <= x < w - 3 and 3 <= y < h - 3 and figure[y - 3:y + 4, x - 3:x + 4].all():
            return q
    return towards


def prompts(pose, hem_below=None, figure=None):
    """Candidate SAM prompts for the outermost torso garment; the caller keeps the answer SAM scores highest.

    `hem_below` ('knee' or 'ankle') is a hint from views that can see the hem, for one that cannot. `figure` is the
    view's silhouette as a boolean array, which keeps a profile's clicks on the body.
    """
    lm = pose.get('landmarks') or {}
    shoulders = _mean(_point(lm, 'leftShoulder'), _point(lm, 'rightShoulder'))
    hips = _mean(_point(lm, 'leftHip'), _point(lm, 'rightHip'))
    if shoulders is None or hips is None:
        return {'found': False, 'reason': 'needs a visible shoulder and a visible hip'}
    torso = hips - shoulders
    # Not on the centre line: that is where a garment's closure is (a button placket, a zip) and where a crossbody
    # strap crosses, and a click there returns the placket, one panel or the strap. Measured on the first run: Tomas's
    # front gave his placket (0.8% of the image), Kit's one half of her jacket, Rio's her strap. So a front or back
    # view gets a click on each side, one per panel; a profile, whose shoulders nearly coincide, keeps the centre.
    across = _point(lm, 'rightShoulder') - _point(lm, 'leftShoulder') \
        if _point(lm, 'leftShoulder') is not None and _point(lm, 'rightShoulder') is not None else None
    length = np.linalg.norm(torso)
    if across is not None and np.linalg.norm(across) > 0.3 * length:
        positives = [shoulders + t * torso + s * across for t in (0.35, 0.65) for s in (-0.22, 0.22)]
    else:
        # A profile. In an A- or T-pose the near arm crosses the middle of the torso here, and a click on the centre
        # line lands on the sleeve with the hand's stop beside it: Tomas's side view gave a fragment of sleeve. So the
        # clicks move toward the back, the side away from where the nose points, and lower.
        nose = _point(lm, 'nose')
        back = -np.sign(nose[0] - shoulders[0]) if nose is not None and nose[0] != shoulders[0] else 0.0
        positives = [shoulders + 0.5 * torso + np.array([back * 0.2 * length, 0.0]),
                     shoulders + 0.9 * torso + np.array([back * 0.15 * length, 0.0])]
        # A profile often cannot show where the hem is: the warden's coat darkens below the waist in profile and SAM
        # stopped there, though her front and back both reach the knee. Given that from the views that can see it, a
        # click goes on the back of the thigh, halfway from hip to knee.
        knee = _mean(_point(lm, 'leftKnee'), _point(lm, 'rightKnee'))
        if hem_below in ('knee', 'ankle') and knee is not None:
            positives.append(hips + 0.5 * (knee - hips) + np.array([back * 0.15 * length, 0.0]))
        # On a slim figure the push toward the back leaves the body: the shorts boy's profile clicks landed on the
        # background (score 0.03). Pull each back toward the centre line until it is on the figure.
        positives = [_inside(p, shoulders + ((p - shoulders) @ torso / length ** 2) * torso, figure) for p in positives]
    negatives = {}
    nose = _point(lm, 'nose')
    if nose is not None:
        negatives['face'] = nose
        # Below the nose, toward the shoulders: the chin, or a beard, which a click on the torso would otherwise
        # flood into. 0.4 of the way still sits above the collar on the lastlight3 characters.
        negatives['chin'] = nose + 0.4 * (shoulders - nose)
    for side in ('left', 'right'):
        hand = _mean(_point(lm, f'{side}Wrist'), _point(lm, f'{side}Index'), _point(lm, f'{side}Pinky'))
        if hand is not None:
            negatives[f'{side}Hand'] = hand
        foot = _mean(_point(lm, f'{side}Heel'), _point(lm, f'{side}FootIndex'))
        if foot is not None:
            negatives[f'{side}Foot'] = foot
        # Low on the shin, just above a boot: where a coat and trousers of similar tone would otherwise read as one
        # garment. Tomas's back view took coat, trousers and boots together without it (score 0.15). The price is
        # that a floor-length coat is cut here; `reach` still reports it as below the knee.
        knee, ankle = _point(lm, f'{side}Knee'), _point(lm, f'{side}Ankle')
        if knee is not None and ankle is not None:
            negatives[f'{side}Shin'] = knee + 0.75 * (ankle - knee)
            # Told the hem is below the knee but above the ankle, halfway down the shin is also not the garment. With
            # only the thigh click, the warden's profile ran down her dark trousers to the ankle.
            if hem_below == 'knee':
                negatives[f'{side}MidShin'] = knee + 0.5 * (ankle - knee)
    # A "this" click close to a "not this" click contradicts it, and SAM answers with a fragment; drop the "this".
    positives = [p for p in positives if all(np.linalg.norm(p - q) > 0.12 * length for q in negatives.values())]
    if not positives:
        return {'found': False, 'reason': 'every torso click lands on a hand, face or foot stop'}
    def prompt(name, stops):
        pts = [p.round(1).tolist() for p in positives] + [q.round(1).tolist() for q in stops.values()]
        return {'name': name, 'points': pts, 'labels': [1] * len(positives) + [0] * len(stops)}

    # Two candidates, with and without the shin stops. The stops separate a coat from trousers of the same tone, and
    # contradict a floor-length garment, where they land inside it: the robe's front scored 0.27 with them. SAM scores
    # its own masks, and the contradicted one scores low, so the caller keeps whichever it scores higher.
    legs = {k: v for k, v in negatives.items() if 'Shin' in k}
    candidates = [prompt('garment', negatives)]
    if legs:
        candidates.append(prompt('garment-free', {k: v for k, v in negatives.items() if k not in legs}))
    return {'found': True, 'prompts': candidates, 'stops': list(negatives)}


def lower_prompts(pose, figure=None):
    """Candidate SAM prompts for whatever covers the thighs: a skirt, a dress's skirt, a coat's skirts, or trousers.

    "This" clicks go on each thigh; "not this" clicks go on the chest, so the upper garment stays out, and on the
    face, hands and feet. `reach`'s `spansGap` then tells a skirt from trousers. The gown showed why this pass is
    needed: a click on the torso stops at a dress's waist seam, and the skirt below it is the part that hangs.
    """
    lm = pose.get('landmarks') or {}
    shoulders = _mean(_point(lm, 'leftShoulder'), _point(lm, 'rightShoulder'))
    hips = _mean(_point(lm, 'leftHip'), _point(lm, 'rightHip'))
    if shoulders is None or hips is None:
        return {'found': False, 'reason': 'needs a visible shoulder and a visible hip'}
    torso = hips - shoulders
    length = np.linalg.norm(torso)
    positives = []
    for side in ('left', 'right'):
        hip, knee = _point(lm, f'{side}Hip'), _point(lm, f'{side}Knee')
        if hip is not None and knee is not None:
            positives += [_inside(hip + t * (knee - hip), hips, figure) for t in (0.4, 0.7)]
    if not positives:
        return {'found': False, 'reason': 'needs a visible hip and knee on one side'}
    negatives = {'chest': shoulders + 0.35 * torso}
    nose = _point(lm, 'nose')
    if nose is not None:
        negatives['face'] = nose
    for side in ('left', 'right'):
        hand = _mean(_point(lm, f'{side}Wrist'), _point(lm, f'{side}Index'), _point(lm, f'{side}Pinky'))
        if hand is not None:
            negatives[f'{side}Hand'] = hand
        foot = _mean(_point(lm, f'{side}Heel'), _point(lm, f'{side}FootIndex'))
        if foot is not None:
            negatives[f'{side}Foot'] = foot
        knee, ankle = _point(lm, f'{side}Knee'), _point(lm, f'{side}Ankle')
        if knee is not None and ankle is not None:
            negatives[f'{side}Shin'] = knee + 0.75 * (ankle - knee)
    # In an A-pose the hands hang beside the thighs; a thigh click beside a hand stop is dropped, as for the torso.
    positives = [p for p in positives if all(np.linalg.norm(p - q) > 0.12 * length for q in negatives.values())]
    if not positives:
        return {'found': False, 'reason': 'every thigh click lands on a hand stop'}

    def prompt(name, stops):
        pts = [p.round(1).tolist() for p in positives] + [q.round(1).tolist() for q in stops.values()]
        return {'name': name, 'points': pts, 'labels': [1] * len(positives) + [0] * len(stops)}

    candidates = [prompt('lower', negatives)]
    if any('Shin' in k for k in negatives):
        candidates.append(prompt('lower-free', {k: v for k, v in negatives.items() if 'Shin' not in k}))
    return {'found': True, 'prompts': candidates, 'stops': list(negatives)}


def _covers(mask, point):
    h, w = mask.shape
    x, y = int(round(point[0])), int(round(point[1]))
    patch = mask[max(0, y - 2):min(h, y + 3), max(0, x - 2):min(w, x + 3)]
    return bool(patch.size and patch.mean() > 0.5)


def choose(prompts_given, segment, min_score=0.5, rule='top'):
    """One mask from every mask SAM offered for these prompts (`sam2_segment.py --all`).

    `rule='top'`, the default, keeps the mask SAM scores highest. `rule='largest'` keeps the largest mask that leaves
    each of its own prompt's "not this" clicks outside, holds at least half its "this" clicks and scores at least
    `min_score`, falling back to the top score.

    'largest' was tried because on a seamed garment a small part can outscore the garment (the overalls' front gave
    its bib pocket). Measured on 11 characters it was WORSE for the upper garment: right on 6 against 'top''s 9. The
    stops do not fence off everything, so the largest mask clearing them ran onto bare arms (the shorts boy's T-shirt
    read as sleeved), down a skirt (the sweater read as long) and down trousers (the warden's coat to the ankle).
    """
    by_name = {p['name']: p for p in prompts_given}
    best, fallback = None, None
    for result in segment['prompts']:
        given = by_name.get(result['name'])
        if given is None:
            continue
        pts = np.array(given['points'], float)
        labels = np.array(given['labels'])
        for i, cand in enumerate(result.get('candidates') or [{'score': result['score'], 'share': result['share'],
                                                                 'mask': result['mask']}]):
            entry = {'name': result['name'], 'index': i, 'score': cand['score'], 'share': cand['share'], 'mask': cand['mask']}
            if fallback is None or cand['score'] > fallback['score']:
                fallback = entry
            if cand['score'] < min_score:
                continue
            mask = np.asarray(Image.open(io.BytesIO(base64.b64decode(cand['mask']))).convert('L')) > 127
            if any(_covers(mask, p) for p in pts[labels == 0]):
                continue
            if sum(_covers(mask, p) for p in pts[labels == 1]) * 2 < (labels == 1).sum():
                continue
            if best is None or cand['share'] > best['share']:
                best = entry
    if rule == 'top':
        return dict(fallback, reason='top score')
    chosen = best or fallback
    return dict(chosen, reason='largest respecting every stop' if best else 'no mask respected the stops; top score')


#: The limb landmarks a garment's reach is read from, top to bottom.
REACH = ['Shoulder', 'Elbow', 'Wrist', 'Hip', 'Knee', 'Ankle']


def reach(pose, mask):
    lm = pose.get('landmarks') or {}
    h, w = mask.shape
    covered = {}
    for part in REACH:
        for side in ('left', 'right'):
            p = _point(lm, side + part)
            if p is None:
                continue
            x, y = int(round(p[0])), int(round(p[1]))
            # A 5 px neighbourhood, majority, so a joint on the garment's edge is not decided by one pixel.
            patch = mask[max(0, y - 2):min(h, y + 3), max(0, x - 2):min(w, x + 3)]
            covered[side + part] = bool(patch.size and patch.mean() > 0.5)

    def any_of(part):
        seen = [covered[s + part] for s in ('left', 'right') if s + part in covered]
        return any(seen) if seen else None

    hem = next((part.lower() for part in reversed(REACH[3:]) if any_of(part)), None)
    return {'covered': covered,
            'sleeves': 'full' if any_of('Wrist') else 'long' if any_of('Elbow') else
                       'none' if any_of('Elbow') is False else None,
            'hemBelow': hem,
            'share': round(float(mask.mean()), 5)}


def hangs(pose, figure):
    """Whether something hangs between the legs (a skirt, a dress, a coat's skirts): True, False, or None.

    Read from the silhouette, not from any mask. The sheet is drawn with the legs apart, so trousers or bare legs
    leave background between the thighs, and anything hanging fills it. Tested along the middle third of the line
    between the thighs, at 35% and 60% of the way from hips to knees. Measured on 12 characters: right on all 11 with
    a pose, front and back agreeing every time, including the gown and the robe, whose hidden legs defeated a
    mask-based version. It answers whether a character needs cloth at all, before any segmentation. Use a front or
    back view; a profile has no gap to test, and neither does a figure with its legs together.
    """
    lm = pose.get('landmarks') or {}
    names = ('leftHip', 'rightHip', 'leftKnee', 'rightKnee')
    lh, rh, lk, rk = (_point(lm, n) for n in names)
    if any(p is None for p in (lh, rh, lk, rk)):
        return None
    h, w = figure.shape
    for t in (0.35, 0.6):
        a, b = lh + t * (lk - lh), rh + t * (rk - rh)
        xs, ys = np.linspace(a[0], b[0], 30)[10:20], np.linspace(a[1], b[1], 30)[10:20]
        xi, yi = np.clip(xs.round().astype(int), 0, w - 1), np.clip(ys.round().astype(int), 0, h - 1)
        if not figure[yi, xi].all():
            return False
    return True


def _figure(req):
    if not req.get('figure'):
        return None
    return np.asarray(Image.open(io.BytesIO(base64.b64decode(req['figure']))).convert('L')) > 127


def main():
    commands = ('prompts', 'lower', 'choose', 'reach', 'hangs')
    if len(sys.argv) != 2 or sys.argv[1] not in commands:
        print(f'usage: garment_prompts.py {"|".join(commands)}  (JSON on stdin)', file=sys.stderr)
        return 2
    req = json.load(sys.stdin)
    if sys.argv[1] == 'prompts':
        # Either a pose result, or {"pose": ..., "hemBelow": "knee", "figure": "<b64 PNG, figure 255>"}.
        result = prompts(req.get('pose', req), req.get('hemBelow'), _figure(req))
    elif sys.argv[1] == 'lower':
        result = lower_prompts(req.get('pose', req), _figure(req))
    elif sys.argv[1] == 'choose':
        # {"prompts": [the prompt definitions], "segment": sam2_segment.py --all's result}
        result = choose(req['prompts'], req['segment'], req.get('minScore', 0.5), req.get('rule', 'top'))
    elif sys.argv[1] == 'hangs':
        # {"pose": ..., "figure": "<b64 PNG, figure 255>"} for a front or back view
        result = {'hangs': hangs(req['pose'], _figure(req))}
    else:
        mask = np.asarray(Image.open(io.BytesIO(base64.b64decode(req['mask']))).convert('L')) > 127
        result = reach(req['pose'], mask)
    json.dump(result, sys.stdout, separators=(',', ':'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
