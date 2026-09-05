# Studio Manual 24: Action — The Centre Line and the Extremes

> **Source Reference**: Stan Lee & John Buscema, *How to Draw Comics the Marvel Way* (Fireside /
> Simon & Schuster, 1978) — ch. 6 *The Name of the Game is Action*, book pp. 63–72, and the
> build-up method from pp. 60–61. Distilled in our own words and cited by page. The copyright page
> is absent from the scanned copy, so its terms could not be read; handled as the Bokhua row.  
> **Purpose**: How a figure is made to *move* — the line drawn before any other, why the extremes of
> an action are the only frames worth drawing, and what separates an adequate pose from a dramatic
> one. Manual 08 constructs a figure; this decides what it is doing.

---

## 1. The centre line comes first

The chapter's central instruction, and it inverts the order our API works in (p. 65):

> **The centre line is drawn through the figure from top to bottom, and it is always drawn first.**
> It gives the curve — the *swing* — that the figure will have. Every pose has a rhythm, and this one
> line determines it; the whole figure is then built around it.

Everything else in the chapter is a consequence. Two figures running: the stronger one is stronger
because **its centre line has more swing**, impelling it forward. Two figures taking a punch: the
weaker is a perfectly clear drawing, and the stronger has the **sharply curved centre line**, legs
bent and thrusting back as the arms jut forward, **the head following the centre line** to complete
one fluid curve (pp. 66–67).

This is the same object Manual 05 calls the C and the S, arriving from a different tradition —
Hampton's gesture line and Buscema's centre line are one idea, and neither book knows about the
other. That agreement is worth more than either statement alone.

## 2. The extremes are the drawing

The working method, and the most directly usable rule in the chapter (p. 64): take an action —
running, throwing a punch — and sketch **a series of stick figures through as many stages of it as
possible**. Then:

> **The first and the last drawings in the sequence have the most impact. A Marvel artist would use
> either of those rather than the tamer ones in between.**

That is a selection rule a program can apply. Draw the action as a sequence, measure how far each
stage departs from rest, and **the extremes are the ones to render** — the middle of an action is
where the drama is not. It also composes directly with Manual 20: choosing which instant to draw is
the same decision as choosing the shot.

**Three or four lines establish the action.** Not a finished drawing — a stick figure with the
swing right. Only once the action reads do you build on it.

**Exaggerate.** Keep the figure loose, supple, always in motion. And even a *standing* figure obeys
this: thrusting the head farther forward, or spreading the legs farther apart, is the whole
difference between an adequate pose and a dramatic one (p. 68).

## 3. Building on it

The five steps (p. 72), which are the chapter's summary of its own method:

1. **Draw the centre line** — this determines the pose and its action curve.
2. **Flesh it out** with the spheres, cubes and cylinders.
3. **Draw through**, adding detail, with loose light graceful strokes; a wrong line is gone over
   lightly rather than erased.
4. **Select** the lines that please and go over them harder — the drawing emerges by choosing.
5. Ink it.

The intermediate technique between 1 and 2 is **scribbling** (pp. 60–61): build the figure up from
the stick line with loose repeated strokes, as a sculptor adds clay, keeping the lines that are right
and losing the rest. Its purpose is explicitly not economy but **loosening** — it is how the feeling
of movement gets into the drawing.

## 4. What the toolkit does and does not do about this

`Drawing.createMannequinFigure(x, y, h, { pose: { ... } })` takes joint angles — `shoulderDeg`,
`elbowDeg`, `hipDeg`, `kneeDeg`, and `spineDeg` to lean the torso. That expresses step 2 onward. It
does **not** express step 1, and the example below measures the gap rather than asserting it:

- **The extremes rule works, and is measurable.** Across five stages of a punch, departure from the
  standing figure runs **37, 22, 8, 22, 53** — the first and last are the extremes by a wide margin,
  exactly as the chapter says, and a `Stage.check` can assert it.
- **There is no line of action.** The centre-line swing across those same five very different poses
  is **1.1, 1.1, 1.1, 1.1, 5.2** — essentially flat. `spineDeg` rotates the upper body *rigidly*
  about the pelvis, and a rigid rotation moves a line without curving it. The figure has a **hinge
  where the book asks for a swing**.

> **The API is built the wrong way round for this chapter, and that is a finding rather than a
> complaint.** Joint angles are how a figure is *described* once posed; a centre line is how a pose
> is *chosen*. A `lineOfAction` option — a C or an S with an amplitude, distributing curvature
> through pelvis, sternum and head rather than rotating them as one body — would let a caller work
> in the order the chapter teaches. Until then, poses are assembled joint by joint, which is step 2
> being asked to do step 1's job.

## 5. Measuring an action

```javascript
// Lee & Buscema draw the centre line first and build the figure around it. Our API has no such
// thing — a pose is joint angles — so the centre line has to be measured back *out* of a posed
// figure. Doing that turns "more swing" from a judgement into a number.
const canvas = createCanvas(940, 470);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9'; ctx.fillRect(0, 0, 940, 470);
const INK = '#1a1a18', SWING = '#c2553d', DIM = '#b9b1a0';

// Five stages of one action: a character taking a punch. The middle three are the tame ones.
const STAGES = [
    { label: 'windup', pose: { spineDeg: -16,
        leftArm: { shoulderDeg: 44, elbowDeg: 74 }, rightArm: { shoulderDeg: 128, elbowDeg: 52 },
        leftLeg: { hipDeg: 74, kneeDeg: 18 }, rightLeg: { hipDeg: 104, kneeDeg: -8 } } },
    { label: '', pose: { spineDeg: -6,
        leftArm: { shoulderDeg: 62, elbowDeg: 48 }, rightArm: { shoulderDeg: 112, elbowDeg: 30 },
        leftLeg: { hipDeg: 82, kneeDeg: 10 }, rightLeg: { hipDeg: 98, kneeDeg: -4 } } },
    { label: '', pose: { spineDeg: 4,
        leftArm: { shoulderDeg: 84, elbowDeg: 20 }, rightArm: { shoulderDeg: 96, elbowDeg: 12 },
        leftLeg: { hipDeg: 88, kneeDeg: 4 }, rightLeg: { hipDeg: 92, kneeDeg: 0 } } },
    { label: '', pose: { spineDeg: 16,
        leftArm: { shoulderDeg: 104, elbowDeg: -14 }, rightArm: { shoulderDeg: 74, elbowDeg: -20 },
        leftLeg: { hipDeg: 96, kneeDeg: 12 }, rightLeg: { hipDeg: 84, kneeDeg: 16 } } },
    { label: 'recoil', pose: { spineDeg: 34, neckDeg: -14,
        leftArm: { shoulderDeg: 128, elbowDeg: -48 }, rightArm: { shoulderDeg: 42, elbowDeg: -56 },
        leftLeg: { hipDeg: 112, kneeDeg: 26 }, rightLeg: { hipDeg: 68, kneeDeg: 34 } } }
];

/** How far the centre line bows off the straight — the "swing" the book asks for, as a number. */
function swingOf(figure) {
    const a = figure.pelvis.center, b = figure.sternum, c = figure.head.center;
    const dx = c.x - a.x, dy = c.y - a.y, len = Math.hypot(dx, dy);
    if (len === 0) return 0;
    // Perpendicular distance from the middle landmark to the chord through the outer two.
    return Math.abs((c.x - a.x) * (a.y - b.y) - (a.x - b.x) * (c.y - a.y)) / len;
}

const cells = Layout.columns(Layout.inset(Layout.rect(0, 0, 940, 470), 20, 20, 52, 20), 5, 10);
ctx.font = '400 13px Georgia, serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'top';
const swings = [];

for (let i = 0; i < STAGES.length; i++) {
    const c = cells[i], stage = STAGES[i];
    const extreme = i === 0 || i === STAGES.length - 1;
    const figure = Drawing.createMannequinFigure(
        c.x + c.width * 0.5, c.y + 30, 290, { pose: stage.pose });

    Drawing.drawMannequinSolid(ctx, figure, {
        fillColor: extreme ? '#efe8da' : '#f2efe7',
        shadowColor: extreme ? '#a89e8b' : '#cfc8bb',
        strokeColor: extreme ? INK : DIM, strokeWidth: extreme ? 1.9 : 1.1 });

    // The centre line, drawn back out of the figure: pelvis -> sternum -> head.
    const a = figure.pelvis.center, b = figure.sternum, h = figure.head.center;
    ctx.strokeStyle = extreme ? SWING : '#ded6c6'; ctx.lineWidth = extreme ? 2.4 : 1.4;
    ctx.beginPath(); ctx.moveTo(a.x, a.y);
    ctx.quadraticCurveTo(b.x + (b.x - (a.x + h.x) / 2), b.y, h.x, h.y); ctx.stroke();

    const s = swingOf(figure); swings.push(s);
    ctx.fillStyle = extreme ? INK : '#9a9282';
    ctx.fillText(stage.label || '(tamer)', c.x + c.width * 0.5, c.y + c.height + 8);
    ctx.fillText('swing ' + s.toFixed(1), c.x + c.width * 0.5, c.y + c.height + 26);
}

// How far a pose has travelled from the standing figure — the thing the API *can* express.
function departure(pose) {
    const neutral = Drawing.createMannequinFigure(0, 0, 290);
    const posed = Drawing.createMannequinFigure(0, 0, 290, { pose: pose });
    let total = 0;
    for (const limb of ['leftArm', 'rightArm', 'leftLeg', 'rightLeg']) {
        for (const j of Object.keys(neutral[limb])) {
            total += Math.hypot(posed[limb][j].x - neutral[limb][j].x,
                                posed[limb][j].y - neutral[limb][j].y);
        }
    }
    return total / 16;
}
const moved = STAGES.map(s => departure(s.pose));
log('departure from standing: ' + moved.map(v => v.toFixed(0)).join(', '));
Stage.check('the first and last stages are the extremes of the action',
    Math.min(moved[0], moved[4]) > Math.max(moved[1], moved[2], moved[3]),
    'ends >= ' + Math.min(moved[0], moved[4]).toFixed(0)
    + ' vs middles <= ' + Math.max(moved[1], moved[2], moved[3]).toFixed(0));

// And the thing it cannot. The centre line barely bends across five very different poses, because
// `spineDeg` rotates the upper body *rigidly* about the pelvis. A rigid rotation moves a line; it
// does not curve one — so the figure has a hinge where the book asks for a swing.
log('centre-line swing: ' + swings.map(v => v.toFixed(1)).join(', ') + '  (near-constant)');
Stage.note('the toolkit poses joints but has no line of action: spineDeg hinges the torso, and the '
    + 'centre line stays straight through poses that should read as a C and an S.');
canvas;
```

## 6. What this does not give you

- **No timing.** The chapter is about which instant to draw, not how long it lasts. Sequence and
  pacing belong to Manual 21.
- **No scribble idiom.** Step 1's loose build-up is a hand technique; the nearest thing here is
  `Skia.PathEffect.discrete(...)` under a stamp, which roughens a line rather than searching for one.
- **The line of action itself.** Named in §4 as the gap, and the most valuable single addition the
  figure toolkit could take.
