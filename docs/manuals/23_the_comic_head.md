# Studio Manual 23: The Comic Head — Construction, Simplification & Expression

> **Source Reference**: Stan Lee & John Buscema, *How to Draw Comics the Marvel Way* (Fireside /
> Simon & Schuster, 1978) — ch. 8 *Drawing the Human Head*, book pp. 87–108. Distilled in our own
> words and cited by page. **The copyright page is absent from the scanned copy**, so its terms could
> not be read; treated as ordinary all-rights-reserved and handled as the Bokhua row — distil, cite,
> never reproduce at length. See the ledger row in `reference/README.md`.  
> **Purpose**: The *comic* idiom for a head, as distinct from the portrait idiom every other head
> source here teaches. Where Loomis builds a head that could be a person, this builds one that reads
> instantly at panel size — and the difference between them is measurable, not a matter of feel.

---

> **Why this manual exists beside Manual 01.** Manual 01 and `Drawing.createLoomisHead(...)` give a
> naturalistic head: a cranial ball, a facial axis, landmarks placed by proportion. That is the right
> construction for a portrait and the wrong one for a panel, and the toolkit had no other. This is
> the other. **The two disagree, and neither is wrong** — they are different faces, in the way
> Loomis and Hampton disagree about hands.

---

## 1. The measurable difference

Both schools were put on one head and compared (the example in §5 does it, and records the numbers):

| | Loomis, as implemented | Lee & Buscema |
| :--- | :--- | :--- |
| head width in eye-widths | **6.0** | **5** |
| mouth width | the landmark width | **about twice that** |

Six is not an accident of our code: the construction's own comment cites *Plate 19* for it, and
`head.unit.W / head.unit.eyeW` comes out at exactly 6.00. The comic head is **narrower relative to
its eye** — which is the same statement as *the eye is larger* — and carries **a wider mouth**. Two
numbers, and between them most of what makes a face read as drawn for a panel rather than for a wall.

## 2. Finding the mouth and the chin by construction

Lee & Buscema do not place the mouth by proportion; they *derive* it (p. 87).

- **The mouth.** Drop an **equilateral triangle** from the bridge of the nose, its sides passing the
  outside of the nostrils. **Where those sides cross the mouth line is the width of the mouth.**
- **The chin.** The same trick again, one stage lower: start the triangle under the nose, take the
  sides through the lower lip where it begins to turn up, and **where they meet the bottom of the
  head is the width of the chin**.

Equilateral fixes the sides at 60°, so the half-width at any depth below the apex is
`depth × tan(30°)` — which is why this is a construction a program can run rather than a proportion
it has to be told.

Their other two head rules agree with Loomis exactly and are worth stating for that reason: **the
head is five eyes wide**, and **there is one eye's width between the eyes**.

## 3. The simplification doctrine

This is the through-line of the chapter and the part that most separates it from a portrait manual.
It is stated as instruction rather than taste (pp. 87, 97):

- **No extra lines** in the forehead, around the nose, or around the chin.
- Keep the **nose small**, the **chin strong**.
- The **mouth is a curve for the upper lip and one small simple line** for the lower.
- Give the hair **body and thickness** — never flat on the skull.
- Eyelashes are **a solid mass**, not drawn lash by lash.
- The forehead is **always rounded, never flat**.

And the DO/DON'T list for mouths, which is a checklist rather than a principle: the upper lip always
sits farther forward than the lower; no bow lips; no angular lips; the upper lip neither too far
forward nor too thin; lips not too thick; the lower lip not jutting; the chin neither too weak nor
too prominent.

## 4. Expression is three features, and nothing else

The chapter's claim about expression is narrow and, for our purposes, the most useful thing in it
(p. 97): every expression on the page was built on the **same construction**, and changed only by
**slight alterations to the mouth, the eyes and the eyebrows**. No extra expression lines. The same
rules apply to male and female faces.

> **This bears directly on `Drawing.applyFacialExpression(...)`.** That call takes one of six named
> expressions — `joy`, `anger`, `fear`, `sadness`, `surprise`, `disgust`. **Both sources now in the
> corpus argue against a fixed six.** Loomis withdrew the universal-six claim in favour of a
> per-muscle relaxed/contracted table, and Lee & Buscema reduce the whole problem to three movable
> features. The presets are a convenience; the model underneath them is per-feature, and an API
> shaped to these sources would take **brow, eye and mouth** parameters and let the six be built from
> them. Recorded here rather than acted on, because changing that call is a decision about the
> surface rather than a distillation.

**Type is variation on the same construction, not a different one.** To make the sophisticated or
older character: a more **angular jawline**, more **arched eyebrows**, the **outer corners of the
eyes raised**, the **nose straightened** (p. 100). For a villain, any head shape at all — square,
round, wide, narrow, pear — chosen so the shape suits the character.

## 5. Both schools on one head

The comparison is the manual: draw the Loomis construction, drop the Marvel triangle on it, and read
off where they differ. Two things in the code are worth as much as the drawing — the mouth sits on
the **facial axis**, which is not the cranial oval's centre, and `head.unit` is an **object**, not a
number, so reading it as one gives `NaN` in silence.

```javascript
// Lee & Buscema find the mouth by construction: an equilateral triangle dropped from the bridge of
// the nose, its sides passing the outside of the nostrils, its width at the mouth line giving the
// mouth. Our Loomis head carries the landmarks that construction needs — so the two schools can be
// drawn on one head and *compared*. They do not agree, and the disagreement is the point.
const canvas = createCanvas(880, 470);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9'; ctx.fillRect(0, 0, 880, 470);
const INK = '#1a1a18', GUIDE = '#4a90e2', MARVEL = '#c2553d';

const head = Drawing.createLoomisHead(300, 250, 300, 0, 0);
const oval = head.temporalOval, nose = head.noseWedge, mouth = head.mouthGuides;

// The mouth sits on the *facial* axis, which is not the cranial oval's centre — reading the axis off
// a landmark rather than assuming `oval.cx` is the difference between a comparison and a mistake.
const axisX = mouth.center.x;

ctx.save();
ctx.translate(0, 60 - head.crown.y);

ctx.strokeStyle = GUIDE; ctx.lineWidth = 1;
ctx.beginPath(); ctx.ellipse(oval.cx, oval.cy, oval.rx, oval.ry, 0, 0, Math.PI * 2); ctx.stroke();
for (const y of [head.brow.y, head.noseBase.y, mouth.center.y]) {
    ctx.beginPath(); ctx.moveTo(axisX - oval.rx * 1.15, y);
    ctx.lineTo(axisX + oval.rx * 1.15, y); ctx.stroke();
}

// Equilateral: the sides descend at 60 degrees, so the half-width at any depth is depth * tan(30).
const TAN30 = Math.tan(Math.PI / 6);
const marvelHalf = TAN30 * (mouth.center.y - nose.bridgeTop.y);
ctx.strokeStyle = MARVEL; ctx.lineWidth = 1.4;
for (const side of [-1, 1]) {
    ctx.beginPath(); ctx.moveTo(axisX, nose.bridgeTop.y);
    ctx.lineTo(axisX + side * TAN30 * (head.chin.y - nose.bridgeTop.y), head.chin.y); ctx.stroke();
}
ctx.lineWidth = 3;
ctx.beginPath(); ctx.moveTo(axisX - marvelHalf, mouth.center.y);
ctx.lineTo(axisX + marvelHalf, mouth.center.y); ctx.stroke();

// What the Loomis landmarks already say the mouth is.
ctx.strokeStyle = INK; ctx.lineWidth = 3;
ctx.beginPath(); ctx.moveTo(mouth.leftCorner.x, mouth.center.y + 11);
ctx.lineTo(mouth.rightCorner.x, mouth.center.y + 11); ctx.stroke();

Drawing.drawComicEye(ctx, head.nearEye, false, { inkColor: INK, irisColor: '#46545f' });
Drawing.drawComicEye(ctx, head.farEye, true, { inkColor: INK, irisColor: '#46545f' });
Drawing.drawComicNose(ctx, nose, { inkColor: INK, shadowColor: '#7d7466' });
ctx.restore();

const marvelWidth = marvelHalf * 2;
const loomisWidth = Math.abs(mouth.rightCorner.x - mouth.leftCorner.x);
// `head.unit` is an object — {H, W, eyeW, thirdH} — not a number. Reading it as one gives NaN
// silently, which is the trap the execution model warns about: a misspelled *read* never throws.
const eyesAcross = head.unit.W / head.unit.eyeW;

log('mouth width — triangle ' + marvelWidth.toFixed(1) + ', Loomis ' + loomisWidth.toFixed(1)
    + ' (x' + (marvelWidth / loomisWidth).toFixed(2) + ')');
log('head is ' + eyesAcross.toFixed(1) + ' eyes wide; Lee & Buscema say 5');
Stage.note('two head schools compared on one construction: the comic idiom draws a wider mouth '
    + 'and a proportionally larger eye. Neither is wrong; they are different faces.');

ctx.fillStyle = INK; ctx.font = '600 15px Georgia, serif'; ctx.textBaseline = 'top';
ctx.fillText('Two schools, one head', 545, 66);
ctx.font = '400 13px Georgia, serif';
ctx.fillStyle = MARVEL;
ctx.fillWrappedText('Red — Lee & Buscema. An equilateral triangle from the bridge of the nose. Its '
    + 'width where it crosses the mouth line is the mouth.', 545, 94, 296, 19);
ctx.fillStyle = INK;
ctx.fillWrappedText('Black — the width the Loomis landmarks already carry. The comic construction '
    + 'gives a mouth about twice as wide, on a head Loomis makes six eyes across where the comic '
    + 'idiom makes it five. The stylisation is measurable, not a matter of feel.', 545, 158, 296, 19);
canvas;
```

## 6. The same face twice — character consistency across panels

A comic asks something a portrait never does: the head in panel 40 has to be recognisably the person
from panel 1, drawn at a different size, at a different angle, months of reading apart. Construction
alone will not do it. `createLoomisHead` gives you the *canon*, and the canon is by definition the
average — every character built from it comes out as the same generically proportioned person.

**`Drawing.createParametricHead(head, parameters)` is what makes one head a particular person**, and
it works by displacing the landmarks the canon already computes rather than by drawing anything new:

```javascript
// One character, three panels: different size, different angle, same person.
const canvas = createCanvas(900, 340);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9'; ctx.fillRect(0, 0, 900, 340);
const INK = '#1a1a18';

// The character is five numbers. Keep them at the top of artwork.js and the face keeps.
const MORT = { eyesDistance: -0.4, eyesSize: 0.3, noseLength: -0.5, jawShape: 0.8, mouthWidth: 0.2 };

const panels = [
    { cx: 150, cy: 70, headH: 180, yaw: 0 },     // mid shot, square on
    { cx: 430, cy: 40, headH: 250, yaw: 20 },    // closer, turning
    { cx: 730, cy: 95, headH: 130, yaw: -15 },   // further off, turned back
];

for (const panel of panels) {
    const head = Drawing.createLoomisHead(panel.cx, panel.cy, panel.headH, panel.yaw, 0);
    const face = Drawing.createParametricHead(head, MORT);

    Drawing.drawLoomisWireframe(ctx, face, { graphiteColor: '#b9b3a4' });
    Drawing.drawComicEye(ctx, face.nearEye, false, { inkColor: INK });
    Drawing.drawComicNose(ctx, face.noseWedge, { inkColor: INK });
    Drawing.drawComicMouth(ctx, face.mouthGuides, { inkColor: INK });
}

// The proof is arithmetic rather than impression: every panel's eye spacing is the same fraction of
// its own head height. A character that drifts would show up here before a reader ever noticed it.
for (const panel of panels) {
    const face = Drawing.createParametricHead(
        Drawing.createLoomisHead(panel.cx, panel.cy, panel.headH, 0, 0), MORT);
    const gap = Math.abs(face.nearEye.inner.x - face.farEye.inner.x) / panel.headH;
    log(`head ${panel.headH}px — eye gap ${(gap * 100).toFixed(2)}% of head height`);
}

canvas;
```

Three things about it that matter for a page rather than for a single drawing:

- **It is a pure function.** No clock, no randomness, no memory. The same five numbers give identical
  landmarks every time, which is the entire mechanism — consistency lives in *you keeping the
  numbers*, not in anything remembering a face. A `const` at the top of the file is the whole of it.
- **Displacements are fractions of the head's own height**, so the character survives a change of
  scale. A 40px head in a long shot and a 600px head in a close-up get the same face, which is
  exactly the case §3's simplification doctrine is otherwise silent about.
- **Zero is the canon, exactly.** A head with no parameters is byte-identical to the one that went
  in, so adding the call to an existing page changes nothing until you give it a number.

**Two characters on one page is two constants, not two constructions.** The head you pass in is never
modified, so one canon head can serve a whole cast in the same script.

> **It works in screen space, so it has a yaw limit.** `createLoomisHead` applies the turn before
> this sees the head, and past roughly 40° "outward" on the page stops being "outward" on the face —
> a widened jaw widens the wrong way. For a character who turns that far, build the extreme angles as
> their own construction. The failure is quiet: the face stops being the same person rather than
> erroring, which is precisely the failure a reader notices and cannot name.

The parameter set follows Schwind et al., *FaceMaker* (Springer 2017), whose five most identity-bearing
controls were drawn from surveying nine commercial character creators. Their system morphs 3D meshes;
this moves Loomis landmarks, which is the same idea in the medium we actually draw in.

## 7. Composing the head — a silhouette to put the features on

Everything above places marks. Nothing above makes a **head**: the cranium, the jaw, the ear and the
neck are all implied by the construction and none of them was ever a shape you could fill.

That is not a cosmetic gap. A run drawing this manual's own brief — two characters in a diner booth,
faces carrying the whole panel — produced two recognisably different faces that read as **masks on
undifferentiated dark shoulder-masses**. Every feature was at the right landmark. There was nothing
for them to sit on, and no call composed one.

**`Drawing.createHeadGeometry(head, options)` is that call.** It takes the head the construction
already computed — canon or parametric — and returns real geometry:

```javascript
const canvas = createCanvas(760, 420);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9'; ctx.fillRect(0, 0, 760, 420);
const INK = '#1a1a18';

const MORT  = { eyesDistance: 0.78, eyesSize: -0.22, noseLength: -0.72, jawShape: 0.88, mouthWidth: 0.30 };
const ELENA = { eyesDistance: -0.76, eyesSize: 0.26, noseLength: 0.80, jawShape: -0.86, mouthWidth: -0.34 };

for (const who of [{ face: MORT, x: 215, yaw: 28 }, { face: ELENA, x: 545, yaw: -22 }]) {
    const head = Drawing.createParametricHead(
        Drawing.createLoomisHead(who.x, 170, 200, who.yaw, 0), who.face);
    const geo = Drawing.createHeadGeometry(head, { skull: 'comic' });

    // A collar is the padded head minus the bare one - Manual 22's derivation, on a neck.
    const padded = Drawing.createHeadGeometry(head, { padding: 14, skull: 'comic' });
    ctx.fillStyle = '#d8d2c4';
    ctx.fill(padded.parts.neck.subtract(geo.parts.neck));

    ctx.fillStyle = '#efe9dc';
    ctx.fill(geo.silhouette);                 // one shape: cranium, jaw, ear and neck
    ctx.strokeStyle = INK; ctx.lineWidth = 2.5;
    ctx.stroke(geo.silhouette);

    // The features now sit ON something, so they can be clipped to it.
    ctx.save();
    ctx.clip(geo.mass);
    Drawing.drawComicEye(ctx, head.nearEye, false, { inkColor: INK });
    Drawing.drawComicEye(ctx, head.farEye, true, { inkColor: INK });
    Drawing.drawComicNose(ctx, head.noseWedge, { inkColor: INK });
    Drawing.drawComicMouth(ctx, head.mouthGuides, { inkColor: INK });
    ctx.restore();

    log(`head ${geo.bounds.width.toFixed(0)}x${geo.bounds.height.toFixed(0)}, neck reaches ` +
        `${(geo.bounds.y2 - head.chin.y).toFixed(0)}px below the chin`);
}

canvas;
```

**`silhouette`** is everything unioned. **`mass`** is the head without the neck, which is what a
feature clips to and what a hat sits on. **`parts`** carries `cranium`, `jaw`, `ear` and `neck`
separately, and **`bounds`** is closed-form, so a head can be sized without building any of it.

### Where each mass comes from, and which numbers are whose

Two of the four are Loomis's own and cost nothing to derive, which is the reason this is a small call
rather than a second construction to keep in step with the first:

- **The cranium is the ball the head is already built on.** Its radius is `brow.y − crown.y`, which is
  the *same* radius `createLoomisHead` uses to find the jaw stations. The ball drawn here and the jaw
  hung off it therefore cannot disagree — a property tested rather than hoped for.
- **The ear straddles the ball's own silhouette, and is a unit tall** (Plate 18). Plate 1 attaches the
  ears along the same halfway line round the ball that the jaw hangs from, and **that halfway line is
  the silhouette** — centred on the `jaw.ear` landmark instead, the mass sits at one unit from the axis
  against a ball 1.41 units wide there, wholly inside the cranium and invisible on anything but a
  profile. The landmark is the attachment; it is left untouched and supplies the side. The ear also
  **widens as the head turns**, because an ear is seen edge-on frontally and full-face in profile — the
  opposite of the far eye, and the one place here where foreshortening runs the other way.
- **The jaw is the polygon through the six stations** `createParametricHead` displaces, so a squared or
  tapered jaw reaches the silhouette for free. Two characters get two outlines, not one outline with
  different marks inside it.
- **The neck's *attachment* is cited; its length and thickness are the studio's.** Loomis puts the
  turning muscles on the skull *just behind the ears* at the top and on the breastbone between the
  collarbones at the bottom, and places the pivot *well inside the roundness of the neck and deep
  under the skull*. That is why the column is anchored **under the ear rather than under the chin** —
  and it is the single fact that makes a head sit rather than float. He gives no measurement for how
  long or how thick, so `neckLength` defaults to **0.30 of the head height** and `neckWidth` to 1.3
  units — both the studio's. `neckLength` is the **whole** extent below the chin, base cap included:
  measured to the capsule's centre instead it overshot by a third and rendered as a light-bulb stem.
  The offset behind the face is scaled by the turn, so a frontal head gets a centred neck.
- **`skull: 'comic'` narrows the cranium to five eye-widths**, which is §1's measurement of this
  manual's own source against Loomis's construction — 6.0 against 5, so the comic skull is 5/6 of the
  ball, at the same height. It is the one departure from the construction this call offers, and it is
  **opt-in**: the landmarks were laid out for a six-eye head, and silently narrowing every head
  already drawn is not a default's job. On a comic page it is usually what you want.

> **Cite that neck passage by passage, not by page.** It is in Part One of *Drawing the Head and
> Hands*, in the discussion of the head's action on the neck. The scan's page numbers do not survive
> text extraction reliably, and a page number nobody can check is worse than none — see the ledger
> row in `reference/README.md`.

> **The ear's turn is recovered, not passed in.** `createHeadGeometry` reads the yaw back out of
> `farEye.width / unit.eyeW`, which the construction clamps at 0.45 — so **past roughly 63° the ear
> stops widening**. Beyond that angle, build the ear yourself.

## 8. What this does not give you

- **A composed head is not a rendered one.** §7 closed the gap this section used to name: there is
  now a silhouette, an ear and a neck. What it gives you is *shape* — four masses and their union.
  It does not shade them, does not join the head to a body, and knows nothing about hair, which
  remains the largest untouched thing on a comic head. `Drawing.drawHairRibbon(...)` draws one strand
  and nothing decides where strands go.
- **Nothing in it is depth.** `order` is the sequence the masses are built in, not a z-order, exactly
  as on a figure. A head turned far enough that the far jaw passes behind the neck still needs you to
  say so with a clip.
- **There is no cheek, so the ball and the jaw meet at a corner.** The ball's outline curves inward
  while the jaw's curves outward, and where they cross the union shows a shallow concave step. A real
  head has a mass bridging them and this does not; at panel size it reads as a cheekbone, at portrait
  size it reads as a seam. Ink over it, or union your own wedge in.
- **One ear, on the side `jaw.ear` names.** The construction carries a single ear landmark, which is
  right for a three-quarter view — the far ear is hidden by the head — and wrong for a frontal one,
  where a reader expects two. Mirror `parts.ear` about `crown.x` when the head is square on.
- **The plates carry what the text cannot.** The five- and six-step figures for the female head and
  profile are drawings; the text gives their order and their rules, which is what is distilled above.
- **Nothing here is about likeness.** Neither school offers it, and no construction will.
