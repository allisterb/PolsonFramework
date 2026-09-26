# Studio Manual 28: Gesture — Drawing the Pose, Step by Step

> **Source Reference**: Walt Stanchfield, ed. Don Hahn, *Drawn to Life: 20 Golden Years of Disney
> Master Classes*, vol. 1 (Focal Press, 2013) — the weekly handouts from his gesture classes at the
> Disney studio — and vol. 2 (Focal Press, 2009), more of the same handouts. Cited by chapter, vol. 1
> unless marked *vol. 2*; vol. 1's chapters 2–18 are hand-lettered handouts read from their page
> images. The copyright page names retrieval systems, so this is our own distillation, cited and never
> quoted at length, and the book's text is not in the search corpus. Disney's character drawings, which
> fill the book, are not reproduced or described.  
> **Purpose**: A procedure for drawing a figure that *does* something — the order to work in, what to
> check at each step, and which SDK calls carry it. Manual 08 builds a figure, Manual 24 chooses its
> action, Manual 22 dresses it; this is the pass that makes the drawing say one thing clearly.

---

## Why this one is a procedure

Most of the studio's figure sources teach **construction**: where the pieces go and how big they are.
Stanchfield teaches **correction**. His handouts are a teacher standing behind a class, looking at
drawings that are anatomically fine and say nothing, and naming what to change: the pose was
straightened, the look runs into the shoulder, the prop is missing, both elbows were not drawn together.
That makes the book unusually close to a checklist, and most of its checks turn out to be measurable
on a mannequin. The sections below run in the order he works in, and each ends with what the toolkit
does about it.

His one-line summary of the whole method, which runs through every chapter: **draw the story, not the
parts.** A drawing of a woman opening an umbrella that has no umbrella in it has failed, however good
the arm (ch. 39).

## 1. Say it first, in verbs

Before any line, put the pose into words — *he leans on the lance*, *she reaches for the jar*,
*he bends over to look at something on the floor* — and make them **verbs** (ch. 46, *Draw Verbs Not
Nouns*). A noun list (arm, torso, hip) produces a drawing of parts; a verb list (lean, reach, twist,
push) produces a drawing of an action, because each verb tells you which way something is being moved
and against what.

He is explicit that this is not a warm-up but the method: the students whose drawings failed were the
ones who could not have said what the pose was about, and talking the pose through — out loud, if
necessary — was the correction he gave most often (ch. 95, 112, 121). When the sentence is clear, the
angles in the drawing follow from it; when it is vague, no amount of anatomy rescues it.

**In the toolkit** the sentence is a record: write it with `Stage.expect(...)` before the first script
that draws the figure, and every later check is checked *against it*. A figure that measures well and
does not do what the sentence says has passed the wrong test.

## 2. One centre of interest, and a clear path to it

Every pose has a **primary action** and everything else is secondary, arranged around it like rings
of diminishing importance; a secondary action loud enough to compete is a conflict of interest, and the
reader has to fight the drawing to find the story (ch. 39).

The commonest primary action in a story drawing is **looking** — he estimates it as the great majority
of scenes in a feature (ch. 121) — and a look has rules of its own:

- **The path from the eyes to the thing looked at must be clear.** Get the near shoulder out of the
  way, bring the head forward, lean the body in. He calls the space between the eyes and the object the
  *stage* of the drawing, because that is where the drama happens (ch. 77, 93, 95).
- **Interest leans toward what interests it.** Bending forward at the waist and stretching the neck
  says *looking at something*; standing straight says *standing* (ch. 121).
- **The whole figure points at it.** The angle of a magazine, the plane of the face, the line of the
  back — each should send the eye toward the stage rather than off the page (ch. 77).
- **A prop is part of the story, not decoration.** It tells you what the pose is about and must be in
  the drawing, related to the hand and the eye (ch. 39, 134).

**In the toolkit** the look is checkable geometry: sample the segment from `figure.head.center` to the
target against the body's silhouette drawn on a probe canvas, and count the hits (§9's example does
exactly this). Anything above zero means something is in the way of the story.

## 3. Solid and flexible: where a body bends

A figure is built **solid–flexible–solid–flexible**: the head, the ribcage and the pelvis are solids
that keep their shape; the neck and the waist between them are where the bending happens, and the
limbs repeat the pattern at every joint (ch. 25). A cartoon character is more flexible, but the same
parts are there, only caricatured.

The consequence he draws from it is the one that matters: every attitude a body can take is some
combination of those few flexible sections doing a limited set of movements. Tie someone's hands and
brace their neck and they will still tell you a story — with the waist and the knees (ch. 26).

**In the toolkit** this is exactly how `pose.lineOfAction` is built: the curve is carried by two bends
at the waist and the neck, and the three solids keep their shape. A C bends both the same way; an S
bends the neck back against the waist. `spineDeg` is the other kind of motion — a lean of everything
above the pelvis as one piece — and the two compose. See `polson://manual/24` §4 for the
measurement that motivated it.

```js
const fig = Drawing.createMannequinFigure(x, y, 400, { pose: {
    lineOfAction: { shape: 'C', turnDeg: -30 },   // the curve: through the flexible parts
    spineDeg: 6,                                   // the lean: the whole upper body
    rightArm: { shoulderDeg: -72, elbowDeg: 8 } } });
log(fig.lineOfAction.swing.toFixed(2) + ' H of swing');
```

### Keep the whole pose in view

Divide the body into two or three units and draw them as one continuous thought, keeping the unit you
are not drawing in peripheral view — the way a musician reads a bar or two ahead (ch. 29). The
failure it prevents is the drawing assembled part by part, each part plausible, the whole at variance
with the pose. **Never draw one elbow without the other**, or one knee, or one hand: the relationship
between the pair is what makes the angle and the tension, and a lone joint has neither (ch. 79).

**The neck is where students go stiff.** It continues the spine, so the back of it is shorter than the
front, which runs from under the chin down into the chest where the collarbones meet; draw its two sides
parallel and it becomes a pipe (vol. 2, ch. 21). A cartoon neck needs nothing more than a simple shape
that squashes and stretches — which is what the neck bend of `lineOfAction` gives it.

## 4. Angles and tension

A figure pulled off its vertical creates **tension** — with the ground it would fall onto, with the
borders it pushes toward, and inside itself between an outstretched hand and the opposite foot. The
eye is exquisitely sensitive to departures from the vertical, which is exactly why they carry
expression (ch. 79).

The persistent error in his classes was **straightening the pose**: drawing the model more upright,
more frontal and more normal than they were, which irons the gesture out. The correction is always the
same — find the angles the body is actually making and push them a little further (ch. 79, 80, 131).
A tall model drawn back down to average height, soft bulges where there were straight lines — all the
same error (ch. 131).

- **Imagine rubber bands between the joints** at their relaxed positions: hand to hand, knee to knee,
  hand to the opposite knee. The pose is what stretched them, and it is the stretch you are drawing,
  not the limbs (ch. 80, 108).
- **Keep a mental vertical through the figure.** The angles you are drawing are departures from it; a
  vertical beside a straightened drawing shows immediately what was lost (ch. 79).
- **Two parallel lines are static; angle one of them and they move** — in the direction the angle
  opens. Angled lower legs under a leaning body carry the lean on; angled the other way, they read as
  about to stand up (ch. 95).
- **Don't twin the limbs.** Two arms pointing the same way, the same length and the same bend make a
  pose static however well drawn. Make one the stretch and the other the squash (vol. 2, ch. 10).
- **If it needs to lean, lean it.** A figure resting on a knee, a chair back or a table must put its
  weight over the support, or it is not resting. And a figure **leans into** what it is doing —
  offering, pointing, striking, peering — because that shortens the distance and the audience reads the
  intent; the named exception is the drinker, who leans *away* from the bottle (vol. 2, ch. 50). Lifting
  anything in front of you puts the belly out; carrying something on the shoulder leans the body forward
  (vol. 2, ch. 62).

**In the toolkit** the angles are on the figure: `figure.ribcage.tiltDeg`, `figure.pelvis.tiltDeg` and
`figure.head.angleDeg`, plus every limb's joint positions. A pose whose tilts are all near zero is the
straightened drawing, whatever the limbs are doing. Twins are measurable too:
`Drawing.createGestureContour(fig).parts` reports each limb's `bendDeg`, and the direction of each upper
arm is the angle from its shoulder to its elbow — two arms within a few degrees of each other on both
counts are twins.

## 5. Opposition

Head, chest and hips turned in **different** directions — the chest one way, the head the other, the
feet walking out from under both — is what he calls opposition, borrowing the mime's term (ch. 107).
It is not conflict in the dramatic sense; even a prayer has it, the face lifted while straight arms
press down. Opposition is what makes a figure look alive and deciding rather than posed.

Its dynamic form is **anticipation**: a character about to rush right first gathers itself to the left,
holds it long enough to read, then goes (ch. 26, 107). A still drawing gets the same effect from parts
thrust against the centre line — a hip is not *sticking out*, it has been *pushed* out, and the drawing
should show the effort (ch. 107).

**In the toolkit** opposition is the sign of the tilts. The default mannequin already carries it —
shoulders at `-6`, pelvis at `+6` — which is contrapposto; a line of action that bends the ribcage past
the pelvis's sign has removed it, and the check is one comparison:

```js
const opposed = Math.sign(fig.ribcage.tiltDeg) !== Math.sign(fig.pelvis.tiltDeg);
```

## 6. Straight against curve: stretch and squash

The single most transferable rule in the book, and it is a drawing rule rather than an anatomy one:

> **A straight line is the symbol for a stretch; a bent or folded line is the symbol for a squash**
> (ch. 19). On a limb the straight side is usually the bony one pulled tight, the curved side the
> fleshy one folding in (ch. 13).

Draw a bent arm with a straight line along the outside of the elbow and a curve on the inside of the
bend; draw a figure leaning right with the left side as one long straight line and the right side
folded. Two lines are enough for any limb — one straight, one curved — and a circle marks a joint
(ch. 24). He applies it to faces too: a jaw dropped open stretches the cheeks and pulls the lines under
the eyes down, and a smile pushes the flesh up (ch. 25, 90).

Two corollaries:

- **Show the normal before the stretch.** A stretch reads as a stretch only against the squash that
  anticipated it; drawn cold, a wide-open mouth looks like the character's resting face (ch. 25).
- **A stretch is not made by anatomy.** Drawing the muscles of a stretched arm correctly does not put
  the stretch across; drawing the side long and straight does (ch. 80).

**In the toolkit** this is `Drawing.createGestureContour(...)`, and `Drawing.drawGestureContour(...)`
to ink it. `createFigureGeometry` builds every limb as a symmetric capsule — the right mass, and the
wrong line, since it is the same on both sides of a bent elbow. The contour call finds the inside of
each bend from the joint's own bisector, gives the outside two straight lines meeting in an angle and
the inside one curve through the joint's inner edge, and judges the torso by length: its longer side
is the stretch, and the other folds inward in proportion to how much shorter it is. Draw it over the
faint silhouette, as §9 does, and the line sits on the mass.

## 7. The silhouette test

A line drawing is really a shape drawing: fill it in solid black and ask whether the pose still reads
(ch. 9, 44). It must — the moods in his example silhouettes read with no eyes or mouths at all. When a
drawing has every part and fails the test, the fix is **staging**, not rendering: move the arm out
from in front of the body, turn the prop so its shape shows, open a gap between the elbow and the
waist. Positive shapes matter, and so do the **negative** shapes between the limbs and the body (ch. 43,
44).

The black-in also shows the **shape of the gesture** — not the outline of the parts but the one shape
the whole pose makes, recognisable at a distance by that shape alone. Two figures relating to each other
are two figures and **one** gesture, and their combined shape is what the reader sees first (vol. 2,
ch. 53).

**In the toolkit** the black-in is literal: fill `Drawing.createFigureGeometry(fig).silhouette`. And
the question *does this arm read?* has a number: how much of the arm's group stands outside the
torso's. An arm mostly inside the torso's black shape is an arm the silhouette does not show.

## 8. Depth, line and the last pass

Once the gesture reads, and only then:

- **Tangents are the enemy of depth.** Two contours that just touch, a line that ends where another
  begins, more than two lines meeting at a point — each flattens the drawing into ambiguity. Overlap
  decisively instead, so it is unmistakable which form is in front (ch. 16, 30). **Alignment is a
  tangent too**: arms lined up with the sides of the body, a hat brim running along the shoulder line,
  a face stacked straight on the body. The plainest statement of the rule is two circles — one tucked
  behind the other reads as space, two kissing at a point reads as a flat pattern — and a row of objects
  touching edge to edge blocks the eye's path back through the scene (vol. 2, ch. 57).

  **In the toolkit** `Drawing.findTangents(shapes)` finds both kinds, given named paths or a
  `createFigureGeometry(...)` result. A **touch** is two outlines closer than a gap without overlapping,
  or overlapping by a sliver thinner than it; a decisive overlap is thicker and is not reported, so an arm
  entering the shoulder is not a tangent. An **alignment** is a stretch of one outline running close and
  parallel to the other's, and `flush: true` marks the case where it lies over the other's hidden edge
  and the silhouette carries on as if it were not there. Each tangent carries a `mark` to stroke. The
  default mannequin, standing, already has two: its hands hang flat against its thighs.
- **The six depth cues in line** (ch. 30, after Bruce McIntyre): overlap; **surface plus size** — feet
  sitting on the ground plane, nearer ones lower and larger; **surface lines** — a sleeve cuff, a belt, a
  collar, the implied line through the eyes, each curving round its form and telling you which way it
  points; and foreshortening. Every limb **points somewhere**, and knowing which way is what tells you
  which cue to use.
- **Thick and thin.** Heavier lines for the parts nearest the viewer and for shadows, finer ones for the
  delicate edges; a drawing all one weight is flat (ch. 9). See `polson://manual/03`.
- **No lazy lines.** A beak and a feather drawn with the same line look traced. The gesture pass may be
  one kind of line; the finishing pass must say what each part is made of — hard, soft, hanging,
  pulled (ch. 90).
- **Detail last, and only what helps.** Basic shapes first; hair, wrinkles and clothing are added when
  the shapes are working, and then only as they further the action (ch. 10, 24). Folds appear at the
  joints under pressure, and you can usually count them on two or three fingers — everything else is
  the model's costume, not the character's (ch. 137). See `polson://manual/22`.

### Lines that carry a mood

His handout *Symbols for Poses* (ch. 42) is a table of line families and the feelings they call up,
offered as a shorthand for staging a scene. In summary: **horizontals** — rest, calm, finality;
**verticals** — dignity, height, austerity; **vertical against horizontal** — solidity, stubbornness;
**conflicting diagonals** — conflict and disturbance; **an unsupported diagonal** — movement across or
into the space; **zigzags** and broken marks — excitement, vibration; **a wave curve** — grace and
rhythm, or at its extreme, turbulence; **flame shapes** — vehemence, aspiration; **pointed shapes** —
alertness; **spheres** — comfort and abundance; **a spiral** — great force, awe; **the Gothic arch** —
contemplation; **a fountain** — carefree release; **a cascade** — swift, playful rhythm; and **the grief
line**, a figure bowed over on itself — fatigue and sorrow.

It pairs with ch. 43: a character who is threatened can be staged inside dominant **negative** shapes,
one who is on top of the situation can fill the frame as the dominant positive. Choose the family before
the figures; it tells you what the set's lines, the line of action and the composition are for. See
`polson://manual/09` for the armatures that carry it.

## 9. The procedure, run

One pose taken through it: the sentence recorded first, the curve put in through the flexible parts,
the black-in tested, and the line drawn straight against curve. Every claim in §§2–7 is a
`Stage.check` with its measurement.

```javascript
// One pose, taken through Stanchfield's procedure: say it, bend it, black it in, then line it.
// The sentence comes first because everything after it is checked against it.
const STORY = 'she stretches up and right for a jar on a high shelf, weight on her left leg';
Stage.expect(STORY);

const W = 900, H = 520;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9';
ctx.fillRect(0, 0, W, H);
const INK = '#1a1a18', BLUE = '#6f9fd8', RED = '#c2553d';
const cells = Layout.columns(Layout.inset(Layout.rect(0, 0, W, H), 20, 20, 44, 20), 3, 16);

// 1-2. The pose as verbs. The reaching side stretches, so the torso bows toward it: a C whose
// top turns away from the reach. Shoulders and hips tilt against each other - opposition.
const pose = {
    lineOfAction: { shape: 'C', turnDeg: -30 },
    rightArm: { shoulderDeg: -72, elbowDeg: 8 },
    leftArm: { shoulderDeg: 104, elbowDeg: 18 },
    leftLeg: { hipDeg: 92, kneeDeg: 0 },
    rightLeg: { hipDeg: 80, kneeDeg: 14 }
};

// Fit to a panel from the posed extent, never from the height: a reach runs past the head.
function fitted(cell) {
    const e = Drawing.createMannequinFigure(0, 0, 1000, { pose, pelvicTiltDeg: 8 }).bounds;
    const s = Math.min(cell.width / e.width, cell.height / e.height) * 0.92;
    const ox = cell.x + (cell.width - e.width * s) / 2 - e.x * s;
    return Drawing.createMannequinFigure(ox, cell.y - e.y * s, 1000 * s, { pose, pelvicTiltDeg: 8 });
}

// Panel 1 - construction: the masses, and the line of action read back out of the figure.
const fig = fitted(cells[0]);
Drawing.drawMannequinWireframe(ctx, fig, { blueLineColor: BLUE, graphiteColor: '#9a9282', lineWidth: 1.2 });
ctx.strokeStyle = RED; ctx.lineWidth = 3;
ctx.stroke(new CanvasPath(fig.lineOfAction.d));

// Tension and opposition, measured rather than admired.
const rib = fig.ribcage.tiltDeg, pel = fig.pelvis.tiltDeg;
Stage.check('shoulders and hips tilt against each other', Math.sign(rib) !== Math.sign(pel),
    'ribcage ' + rib.toFixed(1) + ', pelvis ' + pel.toFixed(1));
Stage.check('the line of action bows', fig.lineOfAction.swing > 0.15,
    `swing ${fig.lineOfAction.swing.toFixed(2)} H`);

// Twins: two arms pointing the same way with the same bend make a pose static.
const aim = arm => Math.atan2(arm.elbow.y - arm.shoulder.y, arm.elbow.x - arm.shoulder.x) * 180 / Math.PI;
const bends = Drawing.createGestureContour(fig).parts;
const aimGap = Math.abs(((aim(fig.leftArm) - aim(fig.rightArm)) % 360 + 540) % 360 - 180);   // wrapped, 0-180
const bendGap = Math.abs(bends.leftArm.bendDeg - bends.rightArm.bendDeg);
Stage.check('the arms are not twins', aimGap > 20 || bendGap > 20,
    `aims ${aimGap.toFixed(0)} deg apart, bends ${bendGap.toFixed(0)} deg apart`);

// Panel 2 - the silhouette test: black it in and ask whether it still says the sentence.
const fig2 = fitted(cells[1]);
const geo = Drawing.createFigureGeometry(fig2);
ctx.fillStyle = INK;
ctx.fill(geo.silhouette);

// How much of the reaching arm stands clear of the torso. An arm lost inside the body's black
// shape is an arm the silhouette does not show.
const reach = geo.groups.rightArm;
const reachClear = reach.subtract(geo.groups.torso).area / reach.area;
Stage.check('the reaching arm reads in silhouette', reachClear > 0.8,
    `${(reachClear * 100).toFixed(0)}% of it clear of the torso`);

// Tangents: no two groups kissing, and no limb lined up along another.
const tangents = Drawing.findTangents(geo);
Stage.check('no tangents', tangents.count === 0,
    tangents.tangents.map(t => `${t.kind} ${t.a}/${t.b}`).join(', ') || `${tangents.pairs} pairs clear`);
ctx.strokeStyle = RED; ctx.lineWidth = 4;
for (const t of tangents.tangents) ctx.stroke(t.mark);

// The look: the path from the eyes to the jar must be clear of the body, sampled along the line.
const jar = { x: fig2.rightArm.hand.x + fig2.headUnit * 0.4, y: fig2.rightArm.hand.y - fig2.headUnit * 0.5 };
const body = geo.groups.torso.union(geo.groups.leftArm).union(geo.groups.leftLeg).union(geo.groups.rightLeg);
const eye = fig2.head.center;
let blocked = 0;
for (let i = 1; i <= 40; i++) {
    const t = i / 40;
    if (body.contains(eye.x + (jar.x - eye.x) * t, eye.y + (jar.y - eye.y) * t)) blocked++;
}
Stage.check('the look travels clear to the jar', blocked === 0, `${blocked} of 40 samples blocked`);
ctx.strokeStyle = RED; ctx.lineWidth = 1.5; ctx.setLineDash([5, 4]);
ctx.beginPath(); ctx.moveTo(eye.x, eye.y); ctx.lineTo(jar.x, jar.y); ctx.stroke();
ctx.setLineDash([]);

// Panel 3 - line: straight on the stretch side, curved on the squash side, over the masses.
const fig3 = fitted(cells[2]);
ctx.fillStyle = '#e4ddcc';
ctx.fill(Drawing.createFigureGeometry(fig3).silhouette);
const lines = Drawing.drawGestureContour(ctx, fig3, { stretchWidth: 2.6, squashWidth: 1.6 });
const torso = lines.parts.torso;
Stage.check('the stretch is on the reaching side', torso.stretchSide === 'right',
    `right ${torso.rightLength.toFixed(0)}px against left ${torso.leftLength.toFixed(0)}px`);
ctx.strokeStyle = INK; ctx.lineWidth = 2;
ctx.beginPath();
ctx.ellipse(fig3.head.center.x, fig3.head.center.y, fig3.head.rx, fig3.head.ry,
    fig3.head.angleDeg * Math.PI / 180, 0, Math.PI * 2);
ctx.stroke();

// Captions.
ctx.fillStyle = INK; ctx.font = '400 14px Georgia, serif'; ctx.textAlign = 'center';
['1  bend it: line of action', '2  black it in: silhouette', '3  line it: straight / curve']
    .forEach((t, i) => ctx.fillText(t, cells[i].x + cells[i].width / 2, H - 18));
canvas;
```

The checks in the middle panel are the part to reuse, and each is asked of the geometry rather than
of the picture: `path.area` for *how much*, `path.contains(x, y)` for *whether this point*, and
`Drawing.findTangents(...)` for *does anything touch or line up*.

## 10. What this does not give you

- **Stretch and squash on a straight limb, or on the face.** `createGestureContour` reads the bend, so
  a limb bent less than 8° is stretched on both sides; Stanchfield's relaxed-pose rule — bony side
  straight, fleshy side curved (ch. 13) — needs anatomy the mannequin does not carry. The face's
  stretches (ch. 25, 90) belong to `polson://manual/23`.
- **Every tangent.** `findTangents` finds shapes that touch and edges that line up. It does not find
  a line ending where another begins, more than two lines meeting at a point, or a face stacked
  straight over the body — that last is an alignment of centre lines, not of edges. And it judges the
  shapes you give it: the mannequin's groups, not a costume or a prop, unless you pass those too.
- **A line of action through the legs.** `lineOfAction` curves the torso; carrying the curve down the
  supporting leg is still posed joint by joint.
- **Feeling the pose.** His deepest instruction — act it, feel which muscles pull and where the weight
  falls, then draw from that memory rather than from the model (ch. 31, 38) — has no counterpart in a
  script. The nearest substitute is §1: say the pose in verbs precisely enough that the angles follow.
- **The drawings.** Most of the book's teaching is in its correction sketches, which are Disney's and
  are not reproduced here. What survives distillation is the rule each sketch demonstrates.
