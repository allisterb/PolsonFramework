# Studio Manual 24: Action — The Centre Line and the Extremes

> **Source Reference**: Stan Lee & John Buscema, *How to Draw Comics the Marvel Way* (Fireside /
> Simon & Schuster, 1978) — ch. 6 *The Name of the Game is Action*, book pp. 63–72, and the
> build-up method from pp. 60–61. Distilled in our own words and cited by page. The copyright page
> is absent from the scanned copy, so its terms could not be read; handled as the Bokhua row.  
> §4's construction is Walt Stanchfield's, *Drawn to Life* (Focal Press, 2013), ch. 25–26 on the
> solid-flexible body; retrieval-system terms, so distilled and cited, never quoted at length.  
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

## 4. The line of action in the toolkit

`Drawing.createMannequinFigure(x, y, h, { pose: { ... } })` takes joint angles — `shoulderDeg`,
`elbowDeg`, `hipDeg`, `kneeDeg` — plus two ways of moving the torso, and the difference between them
is the whole of this chapter:

- **`spineDeg` leans.** It rotates everything above the pelvis as one rigid piece. A rigid rotation
  moves a line without curving it, so a figure at `spineDeg: 30` has exactly the centre line of a
  standing one: **0.06 H of swing either way**. Until 2026-09-25 this was the only torso control, and
  across five stages of a punch the swing measured **1.1, 1.1, 1.1, 1.1, 5.2** px — flat. The figure
  had a **hinge where the book asks for a swing**.
- **`lineOfAction` curves.** `{ shape: 'C' | 'S', turnDeg }` bends the torso through its two flexible
  parts, and that is step 1 of §3 expressed directly.

Where the bend goes is the part worth understanding, and it is not Lee & Buscema's but Walt
Stanchfield's. He teaches the body as **solid-flexible**: head, ribcage and pelvis are solids that keep
their shape, and the neck and the waist between them are where every bend, lean and twist happens
(*Drawn to Life*, ch. 25). So a C is two bends the same way, at the waist and at the neck, and an S
is the neck bending back against the waist. His next chapter adds why it matters: every movement the
body makes is some combination of those few flexible sections, which is why a neck brace and tied
hands send the whole performance down into the waist and knees (ch. 26).

- **`turnDeg` is how far the line turns end to end, counting both bends**, positive toward screen right as
  `spineDeg` is. A C of 30 turns the head 30; an S of 40 bends the waist about 20 one way and the neck
  about 20 back, and the head ends near upright.
- **The split is not a constant.** A chain approximating one even curve turns at each joint in
  proportion to the segments either side of it, so each bend's share comes from the figure's own
  lengths — about half and half on the canon. `figure.lineOfAction.waistDeg` and `neckDeg` report it.
- **The two compose.** Lean with `spineDeg`, curve with `lineOfAction`. A falling figure is mostly
  lean; a figure taking a punch is mostly curve.
- **The torso only.** The legs are still posed by `hipDeg` and `kneeDeg`, so a centre line running
  down the supporting leg is the torso's curve continued by hand — angle the leg to carry it on.

**Every figure reports its centre line**, bent or not, as `figure.lineOfAction` —
`{ shape, turnDeg, waistDeg, neckDeg, swing, points, d }`. `swing` is how far it bows off its own
chord in head units, which turns the book's "more swing" into a number a `Stage.check` can hold; `d`
is SVG path data, so drawing the line is `ctx.stroke(new CanvasPath(figure.lineOfAction.d))`.

## 5. Measuring an action

Measured, with both rules checked: departure from standing runs **40, 23, 8, 20, 53** and the swing
**0.30, 0.15, 0.07, 0.12, 0.38 H**. The ends travel furthest *and* bend hardest, which is the chapter's
point stated twice.

```javascript
// Lee & Buscema draw the centre line first and build the figure around it. `lineOfAction` is that
// line: a C or an S bent through the waist and the neck, with the limbs posed on top of it.
const canvas = createCanvas(940, 470);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9'; ctx.fillRect(0, 0, 940, 470);
const INK = '#1a1a18', SWING = '#c2553d', DIM = '#b9b1a0';

// Five stages of one action: a character taking a punch. The middle three are the tame ones.
// Each stage leans with spineDeg and curves with lineOfAction — two different things.
const STAGES = [
    { label: 'windup', pose: { spineDeg: -10, lineOfAction: { shape: 'C', turnDeg: -34 },
        leftArm: { shoulderDeg: 44, elbowDeg: 74 }, rightArm: { shoulderDeg: 128, elbowDeg: 52 },
        leftLeg: { hipDeg: 74, kneeDeg: 18 }, rightLeg: { hipDeg: 104, kneeDeg: -8 } } },
    { label: '', pose: { spineDeg: -4, lineOfAction: { shape: 'C', turnDeg: -12 },
        leftArm: { shoulderDeg: 62, elbowDeg: 48 }, rightArm: { shoulderDeg: 112, elbowDeg: 30 },
        leftLeg: { hipDeg: 82, kneeDeg: 10 }, rightLeg: { hipDeg: 98, kneeDeg: -4 } } },
    { label: '', pose: { spineDeg: 2, lineOfAction: { shape: 'S', turnDeg: 8 },
        leftArm: { shoulderDeg: 84, elbowDeg: 20 }, rightArm: { shoulderDeg: 96, elbowDeg: 12 },
        leftLeg: { hipDeg: 88, kneeDeg: 4 }, rightLeg: { hipDeg: 92, kneeDeg: 0 } } },
    { label: '', pose: { spineDeg: 8, lineOfAction: { shape: 'C', turnDeg: 16 },
        leftArm: { shoulderDeg: 104, elbowDeg: -14 }, rightArm: { shoulderDeg: 74, elbowDeg: -20 },
        leftLeg: { hipDeg: 96, kneeDeg: 12 }, rightLeg: { hipDeg: 84, kneeDeg: 16 } } },
    { label: 'recoil', pose: { spineDeg: 16, lineOfAction: { shape: 'C', turnDeg: 46 },
        leftArm: { shoulderDeg: 128, elbowDeg: -48 }, rightArm: { shoulderDeg: 42, elbowDeg: -56 },
        leftLeg: { hipDeg: 112, kneeDeg: 26 }, rightLeg: { hipDeg: 68, kneeDeg: 34 } } }
];

const cells = Layout.columns(Layout.inset(Layout.rect(0, 0, 940, 470), 20, 20, 52, 20), 5, 10);
ctx.font = '400 13px Georgia, serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'top';
const swings = [];

for (let i = 0; i < STAGES.length; i++) {
    const c = cells[i], stage = STAGES[i];
    const extreme = i === 0 || i === STAGES.length - 1;
    const figure = Drawing.createMannequinFigure(c.x + c.width * 0.5, c.y + 30, 290, { pose: stage.pose });

    Drawing.drawMannequinSolid(ctx, figure, {
        fillColor: extreme ? '#efe8da' : '#f2efe7',
        shadowColor: extreme ? '#a89e8b' : '#cfc8bb',
        strokeColor: extreme ? INK : DIM, strokeWidth: extreme ? 1.9 : 1.1 });

    // The figure reports its own centre line, so drawing it is one stroke.
    ctx.strokeStyle = extreme ? SWING : '#ded6c6'; ctx.lineWidth = extreme ? 2.4 : 1.4;
    ctx.stroke(new CanvasPath(figure.lineOfAction.d));

    const s = figure.lineOfAction.swing; swings.push(s);
    ctx.fillStyle = extreme ? INK : '#9a9282';
    ctx.fillText(stage.label || '(tamer)', c.x + c.width * 0.5, c.y + c.height + 8);
    ctx.fillText('swing ' + s.toFixed(2) + ' H', c.x + c.width * 0.5, c.y + c.height + 26);
}

// How far a pose has travelled from the standing figure.
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
log('centre-line swing: ' + swings.map(v => v.toFixed(2)).join(', '));

// Both extremes rules, checked: the ends have travelled furthest AND bow hardest.
Stage.check('the first and last stages are the extremes of the action',
    Math.min(moved[0], moved[4]) > Math.max(moved[1], moved[2], moved[3]),
    'ends >= ' + Math.min(moved[0], moved[4]).toFixed(0)
    + ' vs middles <= ' + Math.max(moved[1], moved[2], moved[3]).toFixed(0));
Stage.check('the extremes carry the most swing',
    Math.min(swings[0], swings[4]) > Math.max(swings[1], swings[2], swings[3]),
    'ends >= ' + Math.min(swings[0], swings[4]).toFixed(2)
    + ' H vs middles <= ' + Math.max(swings[1], swings[2], swings[3]).toFixed(2) + ' H');
canvas;
```

## 6. What this does not give you

- **No timing.** The chapter is about which instant to draw, not how long it lasts. Sequence and
  pacing belong to Manual 21.
- **No scribble idiom.** Step 1's loose build-up is a hand technique; the nearest thing here is
  `Skia.PathEffect.discrete(...)` under a stamp, which roughens a line rather than searching for one.
- **A line of action through the legs.** `lineOfAction` curves the torso; the rest of a centre line
  that runs to the supporting foot is posed joint by joint, as §4 says.
