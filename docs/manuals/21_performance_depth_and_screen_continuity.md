# Studio Manual 21: Depth, Proximity & Screen Continuity

> **Source Reference**: Francis Glebas, *Directing the Story: Professional Storytelling and
> Storyboarding Techniques for Live Action and Animation* (Focal Press / Elsevier, 2009) — §1 and §2
> from ch. 8 (pp. 191–211), §5 from ch. 9 (pp. 230–237), §3 and §4 from ch. 10 (pp. 241–266).
> Distilled in our own words and cited by chapter. **The book's own text is not in the retrieval
> corpus and must not be put there** — its rights clause names retrieval systems explicitly, so this
> manual (our prose) is served while the source is not. See the ledger row in `reference/README.md`.  
> **Purpose**: How a *sequence* holds together in space: the depth cues that make a flat panel read
> as deep, how camera distance governs the viewer's emotional involvement, and the continuity rules
> that stop consecutive panels contradicting each other. Manual 20 frames a shot; this one places it
> in space and joins it to its neighbours.

---

> **How this sits beside Manual 20.** Janson writes about the *panel on a page* — a reader's eye
> moving over a fixed layout. Glebas writes about the *shot in time* — an audience that cannot look
> ahead or back. They agree on shot distance and on clarity, and they diverge usefully: Janson has
> page geometry and read order, which film has no equivalent of; Glebas has continuity *between*
> shots, which a comics page mostly gets for free from adjacency. A storyboard is the case where
> both apply at once.

---

## 1. The depth cues, and how to spend them

A panel is two dimensions standing in for three. The depth is not lost, it is *suggested*, and
Glebas lists the cues that do the suggesting (ch. 8):

| Cue | What it is |
| :--- | :--- |
| **Linear perspective** | converging lines to a vanishing point on the horizon |
| **Aerial perspective** | distance means more atmosphere: farther things go bluer, lose contrast, lose detail |
| **Overlap** | a nearer object obscures a farther one — the cheapest and most reliable cue there is |
| **Size constancy** | assume two things are the same size, and the smaller reads as farther |
| **Texture gradient** | the same texture compressing with distance; size constancy applied to surface |
| **Line direction** | diagonals recede; horizontals and verticals are spatially neutral |

**The horizon is always at the viewer's eye level.** That is not a stylistic choice, it is what the
horizon *is*, and it produces the single most useful rule in the chapter:

> **The horizon cuts every figure of equal height standing on the same ground plane at the same
> point on the body, regardless of how near or far it is.** If the horizon crosses one figure at the
> knee, it crosses every equal-height figure at the knee. Figures lower than the horizon sit the same
> relative distance below it.

That is a *check*, not just a principle: given a horizon and a set of figures, the intercept can be
computed and compared. A crowd staged by eye almost never satisfies it, and the failure reads as
figures pasted onto a background rather than standing in it.

> **Implemented by**: `Drawing.createPerspectiveGrid({ type, horizonY, ... })` supplies the horizon;
> `Drawing.createMannequinFigure(originX, originY, totalHeight)` places a figure of known height.
> The intercept check is arithmetic on those two and is not a call the toolkit provides — write it.

### Lens, expressed as geometry

Glebas gives a mapping worth having because it turns a photographic idea into a drawable one:
**the separation of the two vanishing points encodes the lens.** Points far apart read as
**telephoto**, which flattens space; points close together read as **wide-angle**, which expands it.
Each gives a distinctly different feeling of space, so the choice is expressive rather than technical.

Camera height carries meaning of its own, and it agrees with Janson's up-shot and down-shot:

- **Low ("mouse-eye")** — dynamic, generates many diagonals.
- **High ("bird's-eye")** — excellent for showing where everything is, and **emotionally detached**.
  You are above it all.
- **Three-point** — the deepest space, the third point vanishing far above or below; the sensation of
  looking up a tall building or down from one.

Two facts that catch people out: **each ground plane has its own vanishing points**, and objects
oriented differently each have their own points *on the same horizon*.

### Layered depth, with numbers

Glebas passes on a planning method from art director Bill Perkins: build the scene as **separate
flat layers** — background, one or more midgrounds, foreground — like stage flats. Then keep each
layer's values inside its own band, which produces aerial perspective by construction:

| Layer | Value range |
| :--- | :--- |
| Foreground | 95% grey up to white — the greatest contrasts |
| Midground | 70% down to 15% grey |
| Background | 40% down to 20% grey — the narrowest band |

This is directly checkable with `bitmap.palette(...)`, which reports dominant colours and their
shares: a background whose values stray into the foreground band is a measurable fault, not a matter
of taste.

> **Implemented by**: `Drawing.createNotanPalette(type)` for the value key; `Layout` has no notion of
> depth layers, so the banding is a discipline the caller keeps. `bitmap.palette` and
> `bitmap.rowProfile` are how you audit it after the fact.

### Depth killers

Things that destroy the illusion — and Glebas notes each is *useful* when you want flatness, which
is why they are worth naming rather than merely avoiding (ch. 8):

- Lines **parallel to the frame** flatten space.
- **Ignoring size constancy** — wrong relative sizes — destroys depth or turns the image surreal.
- **Shadows that go fully black** punch holes in the picture; **highlights that go fully white** look
  pasted on top of it.
- **Objects that do not share a horizon** kill depth outright: they feel like they do not belong in
  the same picture.

That last one is the most common failure in a composited board, and it is exactly the check §1 gives.

### The horizon intercept, as an assertion

The rule is worth having precisely because it is arithmetic: fix the fraction of the body the
horizon crosses, derive each figure's drawn height from its ground position, and the check is
then something a run can record rather than something a person has to eyeball.

```javascript
// The horizon cuts every equal-height figure at the same point on the body, however far away it is.
// That makes it a check: choose the fraction, derive each figure's drawn height, then assert it.
const canvas = createCanvas(900, 460);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f1e8'; ctx.fillRect(0, 0, 900, 460);

const HORIZON_Y = 150;
const CUT = 0.55;                 // horizon crosses each figure 55% of the way up from the feet

// Four figures at different depths. Depth is expressed as where the feet meet the ground.
const feet = [{ x: 180, y: 250 }, { x: 350, y: 310 }, { x: 560, y: 380 }, { x: 780, y: 440 }];

// Ground plane and horizon.
ctx.fillStyle = '#e6e1d3'; ctx.fillRect(0, HORIZON_Y, 900, 460 - HORIZON_Y);
ctx.strokeStyle = '#9d9482'; ctx.lineWidth = 1.5;
ctx.beginPath(); ctx.moveTo(0, HORIZON_Y); ctx.lineTo(900, HORIZON_Y); ctx.stroke();

const drawn = [];
for (const foot of feet) {
    // height follows from the rule: (footY - horizonY) / height === CUT
    const height = (foot.y - HORIZON_Y) / CUT;
    const figure = Drawing.createMannequinFigure(foot.x, foot.y - height, height);
    Drawing.drawMannequinSolid(ctx, figure, {
        fillColor: '#f1ece1', shadowColor: '#b3aa99', strokeColor: '#1d1d1b', strokeWidth: 1.6 });
    drawn.push({ foot: foot, height: height });
}

// The check: the same body fraction for every figure, whatever its drawn size.
const cuts = drawn.map(d => (d.foot.y - HORIZON_Y) / d.height);
const spread = Math.max(...cuts) - Math.min(...cuts);
log('drawn heights: ' + drawn.map(d => d.height.toFixed(0)).join(', '));
log('horizon crosses each at: ' + cuts.map(c => c.toFixed(3)).join(', '));
Stage.check('one horizon intercept for every figure', spread < 0.001,
            'spread ' + spread.toFixed(6));

// Depth layers keep to their own value bands, which is aerial perspective by construction.
const BANDS = [{ label: 'background', v: '#b9b3a5' },
               { label: 'midground',  v: '#8a8171' },
               { label: 'foreground', v: '#2e2a24' }];
ctx.font = '400 13px Georgia, serif'; ctx.textBaseline = 'top';
for (let i = 0; i < BANDS.length; i++) {
    ctx.fillStyle = BANDS[i].v;
    ctx.fillRect(20 + i * 130, 20, 118, 26);
    ctx.fillStyle = '#39342c';
    ctx.fillText(BANDS[i].label, 20 + i * 130, 52);
}
canvas;
```

---

## 2. Proximity is emotional, not informational

How close the camera sits changes how the audience *feels*, independently of what is shown (ch. 8):

- **Wide** — the audience is not engaged with these characters. Glebas used it deliberately to keep
  a framing narrator present but not the subject. **Back-lighting into silhouette adds to the same
  distancing effect.**
- **Closer** — the feeling of being with a friend.
- **Too close** — the audience feels inside the character's thoughts.

Set against Janson's ladder in Manual 20, this is the same axis read for a different purpose: Janson
says what each shot *shows*, Glebas says what each shot *does to the viewer*. Neither is a rule about
information; both are about involvement.

**Give characters breathing room** — his own summary point, and the counterweight to the instinct to
crop tight because the subject is what matters.

### Stage from a plan

From Donald Graham's *Composing Pictures*, via Glebas: **work out placement in a simple overhead
plan first, then stage the camera.** Blocking and camera moves go on the plan as arrows, like a
football play. The pay-off is concrete — in plan view it is obvious when one figure will obscure
another, which is invisible while you are drawing the frame itself.

> **Why this suits code particularly well.** A plan is a set of ground-plane coordinates, and
> `Drawing.createPerspectiveGrid` projects the ground plane. Keeping the blocking as plan
> coordinates and projecting it per panel means the *same staging* can be re-shot from a different
> camera without re-authoring the scene — which is the storyboard operation this toolkit is best
> placed to do well.

---

## 3. Screen geography: the line of action

Viewers build a mental map from what the screen shows, and **action is judged by how it appears on
screen, not by how it would be in the world** (ch. 10). Two rules follow:

1. **Direction must stay constant across consecutive shots.** A character travelling left must keep
   travelling left. Reversed direction reads as *turning around*, so contrasting directions are how
   you show a journey out and back.
2. **Run an imaginary line through the plan of the action and stay on one side of it.** Cross it and
   the car appears to have suddenly reversed. This is the rule usually called the 180-degree rule;
   Glebas describes it purely as a consequence of the plan, which is the more useful framing here
   because the plan is a thing you can hold in code.

**Three legitimate ways to change screen direction**, all of them explicit rather than accidental:

- Insert a **neutral shot** — camera head-on along the axis, so no direction is implied. It also adds
  variety.
- Show the change itself in a **longer shot**, so the audience sees it happen.
- Cut to a **reaction shot** of a character watching, which covers the change.

**Eyeline matches.** Characters of different heights looking at each other in separate shots must
have their looks aligned *in the frame* to read as looking at each other. And a shot of a character
looking is normally followed by a shot of what they see — the subjective shot of Manual 20 §2,
arrived at from the continuity side.

**Entrances and exits through doors** demand care: they maintain or break screen direction, and they
draw attention to themselves, so time them so they do not steal a more important beat.

> **Why this matters for a generated board.** Screen direction and the line of action are properties
> of a *sequence*, so no single panel can be judged wrong on its own — which makes them exactly the
> kind of defect a per-panel generator produces and cannot see. They are, however, trivially checked
> against a plan: if the blocking is held as ground coordinates and the camera as a position on one
> side of the action line, consistency is arithmetic.

---

## 4. Causality is what joins shots

Juxtaposing shots is, in Glebas's account, the single most important thing the medium can do that
its parent arts could not. What makes a cut coherent is **causality** — one event causing the next
(ch. 10). He separates three kinds, and the last two are the ones storyboards usually neglect:

- **Physical** — force, momentum, gravity. Reliable, and therefore predictable: once the audience
  knows the rule, there is no surprise left in it.
- **Linguistic** — speech causes action. Someone says a thing and something happens.
- **Emotional** — a feeling causes an action, in the character who feels it *or* in someone
  responding to it. **A reaction shot converts an event into an emotional cause**, and Glebas's point
  is that the same action becomes markedly more involving with the reaction attached.

**Continuity is meant to be invisible.** A gap in space or time catches the viewer's attention like a
glitch and breaks the illusion. The corollary is not to spell everything out: give enough clues to
construct the story and the audience will build it themselves, which involves them more.

**Pacing is part of clarity.** Too fast and the audience cannot process the narrative, and worse,
cannot *feel* it. Emotional beats need room — the moments where the audience reads a character's face
are the ones to give time to.

---

## 5. Speaking indirectly

Once a story can be told clearly and directly, the persuasive power is in saying things *indirectly*
(ch. 9) — showing a significant object, letting an image ask a question, making the audience assemble
the meaning. The classic construction is to move the event off screen and show only its cause and its
consequence, so the audience supplies the middle.

This is the same instinct as Janson's composition chapter, arrived at differently: Janson isolates a
character in a corner of the frame to *show* loneliness rather than draw a sad face. Both are the
rule that an image which makes the viewer do a little work holds them better than one that states
its content.

---

## 6. What this manual still does not give you

Manual 20 ended with three gaps — no posing, no drapery, no small-scale figure idiom. This manual
does not close them, and it is worth being explicit that it does not:

- **Performance is named here, not solved.** Glebas's material on acting and reaction shots tells you
  *that* a reaction shot is a cause and *why* it engages. Drawing one needs a face with an
  expression on a posed body, and the toolkit has landmarks and features but no composed head — see
  Manual 01 and the gap noted there.
- **What it does give**, and this is worth taking seriously, is a set of properties that can be
  **checked** rather than judged: the horizon intercept across figures, value bands per depth layer,
  shared horizons, constant screen direction, camera on one side of the action line. Every one of
  those is arithmetic on a plan, and every one is the kind of error that a picture generated panel by
  panel makes and cannot detect in itself.
