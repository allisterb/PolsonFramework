# Studio Manual 19: Hands — Block Forms, Proportion & the Arcs

> **Source Reference**: Andrew Loomis, *Drawing the Head and Hands* (Viking Press, 1956) — Part Five,
> Plates 77–81 (pp. 135–139). §1 from Plate 79 (p. 137), §2 from Plate 78 (p. 136), §3 from Plate 79's
> curve note and Plate 81; and Michael Hampton, *Figure Drawing: Design and Invention* (2009) — §1a
> from "Hand Structure and Proportion" (pp. 161–163), which supplies the phalanx ratio Loomis does
> not. Where a number is the studio's reading of a sentence that gives none, it says so on the spot.  
> **Purpose**: Turns Loomis's hand construction — the two-unit proportional scale, the block forms, the
> deepening arcs, and the thumb's separate plane — into `Drawing.createHandFigure(...)` and the two
> renderers that consume it.

---

## Why this exists

Until 2026-09-02 the SDK had **no hand at all**. `Drawing.createMannequinFigure(...)` ends its limbs in
box hands, and a live comic run went looking for something better and found nothing. Hands are the
part of the figure an agent is most likely to need and least able to improvise, because a hand that is
merely *approximately* proportioned reads as wrong immediately — far faster than an approximate torso
does.

---

## 1. The Two-Unit Scale

> **Implemented by**: `Drawing.createHandFigure(originX, originY, handLength, options)` → `HandFigure`.
> `originX`/`originY` is the **centre of the wrist**, and the hand runs toward the fingertips along
> `options.rotationDeg` (0 = up the page). Signatures: `polson://sdk/core/Drawing`.

> **Source**: *Drawing the Head and Hands*, Plate 79 (p. 137) — "Proportions of the hand".

> **Principle**:
> Plate 79 boxes the hand and divides it in **two**: wrist to knuckles, knuckles to fingertips. The
> whole scale hangs off one measurement, which Loomis names outright:
>
> **The middle finger is the key finger from which we determine the length of the hand.** Measured from
> its knuckle *at the back* to its tip, it is **slightly over half the hand**. The palm is the rest.

| Measurement | Loomis | As a fraction of `handLength` |
| :--- | :--- | ---: |
| Middle finger, back knuckle → tip | slightly over half the hand | **0.52** |
| Palm, wrist → knuckle line | the remainder | **0.48** |
| Palm width, measured inside | slightly more than half the hand | **0.55** |

So the palm comes out **slightly wider than it is long**, which is what a real palm does across the
knuckles, and it falls out of Loomis's two sentences rather than being chosen.

**The other three fingers are given as reaches, not lengths:**

- The **index** just about reaches the **fingernail of the middle finger**.
- The **ring** is about **equal to the index**.
- The **little finger** just reaches the **top knuckle of the third finger**.

> [!IMPORTANT]
> **Those three are relations, and turning them into numbers needs something Loomis never gives.**
> "Reaches the fingernail" needs a nail length; "reaches the top knuckle" needs a phalanx length. The
> toolkit reads them as `0.90`, `0.90` and `0.66` of the middle finger, taking a nail as about a tenth
> of a finger and a top knuckle as one distal phalanx down. **Those two conversions are the studio's,
> and the relations are what to trust** — if you build a hand by hand, satisfy the reaches and let the
> ratios fall where they will.
>
> The same applies to the thumb's length (`0.34` of the hand). Loomis draws it; he does not measure it.
>
> **The phalanx split used to be on that list and no longer is** — see §1a.

**Two fingers fall on each side of a line through the middle of the palm** — `hand.midLine`. Loomis
also says the middle finger's tendon just about divides the back of the hand in half, which is the same
statement to within a finger's width; the two agree because "just about" is doing the work.

---

## 1a. A Second School — Hampton's Hand

> **Source**: Michael Hampton, *Figure Drawing: Design and Invention* (2009) — "The Hand: Hand
> Structure and Proportion" (pp. 161–163).

Hampton builds the hand from the skeleton rather than from a drawn measurement, and he **disagrees
with Loomis about the split** while **supplying a number Loomis never gives**. Both are carried here,
because the field genuinely has two answers and the toolkit can express either.

| | Loomis | Hampton |
| :--- | :--- | :--- |
| Palm vs fingers | **0.48 / 0.52** — the middle finger is *slightly over* half | **0.50 / 0.50** — the oval is split at the knuckles |
| Inside the palm | not divided | **⅓ carpus group, ⅔ metacarpals** |
| Within a finger | not given | **3:2 ratio** — each bone two-thirds of the one before |

**The disagreement is small and the toolkit follows Loomis** (`0.48 / 0.52`), because the studio's
head and figure work is on his canon and a mixed scale is worse than either. If you want Hampton's,
pass a `handLength` and treat the knuckle line as the midpoint; the difference is two per cent of the
hand.

### The 3:2 ratio, which replaced an invention

> **This is the part worth having.** Divide the proximal phalanx into three; two of those parts are
> the middle phalanx. Divide the middle into three; two of those are the distal. So the bones run
> **1 : ⅔ : 4⁄9**, which normalises to **0.474 / 0.316 / 0.211**.

Until Hampton was read, `createHandFigure` used `0.45 / 0.30 / 0.25` — a studio guess this manual
flagged as such. Hampton was close on the first two bones and shows the fingertip was **a fifth too
long**. The toolkit now uses his ratio.

It also makes one of Loomis's *reaches* computable rather than guessed. The little finger "just
reaches the top knuckle of the third finger" — one distal phalanx down — which is now exactly
`4⁄19` of the ring finger's length, and `TestHandFingerLengthsSatisfyLoomisReaches` asserts that
equality instead of a tolerance band.

> [!TIP]
> **The carpus group is Hampton's design theme, and the toolkit does not model it.** He treats the
> eight wrist bones as a **bridge** — an arch with a hollow beneath it, the carpal tunnel — and says
> it should be carried through every stage of the drawing, because the hand's shape depends on it.
> `hand.palm` is a flat plate; a wrapping line across it, drawn to suggest the arch's apex, is what
> would state the bridge. That is Manual 05 §3's wrapping line applied to the hand.

> [!NOTE]
> **His process is worth knowing even though it is not implemented:** skeleton and proportion first,
> then render those informed designs with **variations of the box, cylinder and sphere** to create
> space, and only then lay **contours** over the forms for an organic description. §2's block forms
> are the middle step of that sequence; the contour pass is the caller's.

---

## 2. Block Forms

> **Implemented by**: `Drawing.drawHandSolid(ctx, hand, options)` → the palm slab, the thenar mass, and
> every phalanx as its own tapering box. `options`: `{ fillColor, shadowColor, strokeColor, strokeWidth }`.

> **Source**: *Drawing the Head and Hands*, Plate 78 (p. 136) — "Block forms of the hand".

> **Principle**:
> Loomis blocks the hand before he draws it, and the blocks are the reason the construction survives
> foreshortening:
>
> - The **palm is a slab** — one flat mass, not a soft shape.
> - Every **finger is three boxes**, hinged, narrowing toward the tip.
> - The **thumb is two boxes** on its own mass.
> - The **thenar** — the thumb's muscle — is a wedge off the radial side of the palm. Loomis: *the big
>   muscle of the thumb is by far the most important one in the hand*, because it opposes the fingers
>   and is what makes the grip.

> [!TIP]
> **The thenar is what stops the thumb reading as detached**, and it is the first thing to go wrong when
> a hand is built joint-by-joint. The model carries it as `hand.thenar` (`wrist`, `crest`, `base`,
> `web`), so the wireframe and the solid renderer draw the same mass and a script can fill or shade it
> separately.

**Padding is a palm-side fact.** Loomis notes the bones and tendons across the *back* are close to the
surface, while the palm and the insides of the fingers are thoroughly padded — a pad at the base of each
finger, combining into one pad across the top of the palm; the thumb muscle and the heel; a pad on each
fingertip. There are **no pads on the back of the hand**. Nothing in the toolkit models the pads; if you
are drawing a palm, they are the difference between a hand and a glove.

---

## 3. The Arcs, and the Rule That Checks Them

> **Implemented by**: the rows come off the model — `hand.fingers[i].knuckle`, then
> `hand.fingers[i].joints[0..2]`. `Drawing.drawHandWireframe(ctx, hand, options)` strokes them as arcs;
> `options`: `{ blueLineColor, graphiteColor, lineWidth }`.

> **Source**: *Drawing the Head and Hands*, Plate 79 (p. 137).

> **Principle**:
> **Note the flat curve of the knuckles across the back of the hand, with the curves getting deeper as
> they cross the knuckles toward the fingertips.**
>
> That is one sentence and it is the whole of hand gesture. The knuckle row is nearly straight; each
> row further out bows more; the fingertip row is the deepest curve of all. A hand drawn with parallel
> fingers of equal length has no arcs at all, which is exactly why it reads as a rake.

> [!TIP]
> **This is a check, not just a construction.** The arcs are a *consequence* of the fingers differing in
> length, so they are also a test of whether the proportions were applied: measure the sagitta of the
> knuckle row and of the tip row, and the second must exceed the first.
> `TestHandArcsDeepenTowardTheFingertips` asserts exactly that, at several spreads, and it is the one
> assertion in the hand suite that would fail if the finger ratios were flattened to equal.

**The knuckles sit slightly above their creases on the inside of the fingers** — a back-versus-palm
offset Loomis notes and the toolkit does not model. It matters when you draw a fist: the knuckle row on
the back and the crease row on the palm are not the same line.

---

## 4. The Thumb Moves in a Different Plane

> **Source**: *Drawing the Head and Hands*, Plate 79 (p. 137).

> **Principle**:
> **The thumb is turned at right angles to the other fingers.** The thumb operates mostly *in and out
> from* the palm; the fingers open and close *toward* it.

> [!WARNING]
> **"At right angles" is about the plane it moves in, not the angle it makes on the page.** Drawn at a
> literal 90° the thumb sticks straight out of the side of the wrist, which is what the first
> implementation did and what a first reading of the sentence invites. `options.thumbDeg` is the
> **drawn** angle off the hand's long axis and defaults to `46`; the right-angle fact is about which way
> the thumb *travels* when you animate or repose it.
>
> The consequence for posing: a curl applied to the fingers closes them toward the palm, and the same
> curl applied to the thumb has to swing it across the palm instead. `createHandFigure` applies
> `curlDeg` to the thumb at half strength and in the mirrored direction for that reason.

---

## 5. Symbol → SDK Parameter Map

| Concept | SDK parameter or field | Notes |
| --- | --- | --- |
| Hand length (wrist → middle tip) | `handLength` argument | Everything else is a fraction of it. |
| The two-unit scale | `hand.unit` | `{ length, palmLength, palmWidth, middleLength, side }`. |
| Wrist anchor | `originX`, `originY` | The centre of the wrist, not a corner. |
| Palm plate | `hand.palm` | `{ wristInner, wristOuter, knuckleInner, knuckleOuter, centre }`. |
| Line through the middle of the palm | `hand.midLine` | `{ from, to }` — two fingers each side. |
| Thumb muscle | `hand.thenar` | `{ wrist, crest, base, web }`. |
| The four fingers | `hand.fingers` | Index, middle, ring, little — each `{ name, knuckle, joints, tip, length, width }`. |
| A finger's three phalanges | `finger.joints` | `[first, second, tip]`, so `joints[2]` is the fingertip. |
| Thumb | `hand.thumb` | `{ base, joints, tip, length, width }` — two phalanges. |
| Which hand | `options.side` | `'right'` (default) or `'left'`; mirrors the thumb. |
| Fan of the fingers | `options.spreadDeg` | Default `7`. |
| Curl at every joint | `options.curlDeg` | Default `0` (flat). Half strength on the thumb — §4. |
| Thumb's drawn angle | `options.thumbDeg` | Default `46`, **not** 90 — §4. |
| Whole-hand rotation | `options.rotationDeg` | About the wrist; `0` points the fingers up. |
| Either renderer, from the context | `ctx.drawHand(hand, solid)` | `solid` picks between the two. The wireframe and solid calls have no shortcut of their own — this covers both, exactly as `ctx.drawMannequin(figure, solid)` does. |

> `hand.fingers` is a list, so `hand.fingers.length` and `hand.fingers[i]` work; iterate it with a `for`
> loop rather than `forEach`, for the reason given under `Assets.library` in `polson://sdk/core/Assets`.

---

## 6. Constructing It: A Runnable Hand

Build the frame, look at the construction, then block it in — the same order Loomis's plates run in.

```javascript
// A hand sheet: construction, blocks, and a posed pair.
const canvas = createCanvas(900, 520);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#fbfaf6';
ctx.fillRect(0, 0, 900, 520);

// §1 — the scale. One number drives the whole hand.
const open = Drawing.createHandFigure(150, 430, 250);
Drawing.drawHandWireframe(ctx, open);

// §3 — the arcs are a consequence of the finger lengths, so they can be measured.
const knuckles = [], tips = [];
for (let i = 0; i < open.fingers.length; i++) {
    knuckles.push(open.fingers[i].knuckle);
    tips.push(open.fingers[i].joints[2]);
}
const sag = (row) => Math.abs(
    row[Math.floor(row.length / 2)].y - (row[0].y + row[row.length - 1].y) / 2);
log('knuckle arc ' + sag(knuckles).toFixed(1) + 'px, tip arc ' + sag(tips).toFixed(1) + 'px');

// §2 — the same frame as block forms.
const blocked = Drawing.createHandFigure(450, 430, 250, { spreadDeg: 10 });
Drawing.drawHandSolid(ctx, blocked);

// §4 — a left hand, curled, turned. The thumb swings the other way.
const posed = Drawing.createHandFigure(760, 400, 230, {
    side: 'left', curlDeg: 16, rotationDeg: -20
});
Drawing.drawHandSolid(ctx, posed, { fillColor: '#efe4d6', shadowColor: '#c9ab8b', strokeColor: '#6b4f36' });

ctx.fillStyle = '#3b3b3b';
ctx.font = '13px sans-serif';
ctx.textAlign = 'center';
ctx.fillText('construction', 150, 500);
ctx.fillText('block forms', 450, 500);
ctx.fillText('left, curled, turned', 760, 500);

canvas;
```
