# Studio Manual 23: The Comic Head — Construction, Simplification & Expression

> **Source Reference**: Stan Lee & John Buscema, *How to Draw Comics the Marvel Way* (Fireside /
> Simon & Schuster, 1978) — ch. 8 *Drawing the Human Head*, book pp. 87–108. Distilled in our own
> words and cited by page. **The copyright page is absent from the scanned copy**, so its terms could
> not be read; treated as ordinary all-rights-reserved and handled as the Bokhua row — distil, cite,
> never reproduce at length. See the ledger row in `reference/README.md`.  
> **§8's craft half** is from Jack Faragasso, *Mastering Drawing the Human Figure* (Stargarden Press, 1998) —
> "Additions and Clarifications of the Structure System: 1. The Head and Shoulders", p. 74 — and Andrew Loomis,
> *Drawing the Head and Hands* (Viking Press, 1956) — p. 24, the text for Plate 7 *Action of the head on the
> neck*, and p. 46. **Faragasso's terms name retrieval systems explicitly**, so it is distilled and cited here
> and its text stays out of the retrieval corpus.  
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

### 4a. The eyebrows, which were the one of the three nothing could draw

The chapter names **mouth, eyes and eyebrows**. The toolkit had `drawComicMouth` and `drawComicEye`
and, until 2026-09-18, nothing at all for the third — the head carried a single `brow` point on the
facial meridian, and that point is not an eyebrow. It is the **ball's equator**: the landmark
`createHeadGeometry` takes the cranium's centre and radius from, and the axis a knit is measured
against. A construction line, drawn only on the sheet.

**So the head now carries `nearBrow` and `farBrow`, each `{ inner, peak, outer, thickness }`, and
`Drawing.drawComicBrow(ctx, brow, isFar, options)` inks them.** Three stations rather than two,
because two cannot carry an arch — and the arch is precisely where an inner lift differs from an
outer one, which is the chapter's own "more arched eyebrows" and the difference between worry and
surprise.

```javascript
const INK = '#15151a';
const ctx = createCanvas(800, 520).getContext('2d');

const head = Drawing.applyFacialExpression(Drawing.createLoomisHead(400, 140, 260), 'sadness', 0.8);
Drawing.drawComicBrow(ctx, head.farBrow, true, { inkColor: INK });
Drawing.drawComicBrow(ctx, head.nearBrow, false, { inkColor: INK });
Drawing.drawComicEye(ctx, head.nearEye, false, { inkColor: INK });

// The construction landmark is untouched by the expression, which is the point of the box below.
log(`brow line ${head.brow.y.toFixed(1)}, drawn inner end ${head.nearBrow.inner.y.toFixed(1)}`);
```

- **Weight is its own measurement, `brow.thickness`, and is derived from neither the span nor the
  arch.** Both derivations are wrong and the second is instructive. The **span** foreshortens, so a
  far brow sized from its projected width returns 45% too thin at the yaw clamp. The **arch** does
  not foreshorten — and that is why it was this call's first implementation — but it is a quantity
  the *Action Units move*: AU1 raises the inner end toward the peak, so `sadness` flattened the arch
  from **15.2px to 0.9px** on a 760px head and drew a hairline. A brow carries its weight the way an
  eye carries `width` and `height`, held apart from anything an expression displaces.

  > **Found by looking at a render, after every assertion passed.** The landmarks were right, the
  > tuples were right, the ordering ladder was right, and the drawing was wrong. It is the same shape
  > as the defect this whole section describes — a drawn quantity taken from something an expression
  > moves — which is worth noticing, because it means the first fix did not teach the lesson
  > thoroughly enough to prevent the second.

### 4b. One brow, not two — asymmetry

The chapter's three features are mouth, eyes and eyebrows, and it is silent on whether they move
together. They do not. **The single most recognisable comic brow is one raised and one not** — the
quizzical, the sceptical, the unimpressed — and until 2026-09-19 no weight of any unit could draw it,
because every unit moved both halves of the face at once. The same was true of the **smirk**: `AU12`
lifted both mouth corners, so a one-sided smile did not exist.

```javascript
const INK = '#15151a';
const ctx = createCanvas(760, 420).getContext('2d');
const head = Drawing.createLoomisHead(220, 200, 260, 0, 0);

const quizzical = Drawing.applyActionUnits(head, { AU2: 0.9 },  { side: 'near' });
const smirk     = Drawing.applyActionUnits(head, { AU12: 0.8 }, { side: 'near' });

for (const face of [quizzical, smirk]) {
    const geo = Drawing.createHeadGeometry(face, { neckLength: 0.12 });
    ctx.save();
    ctx.clip(geo.mass);
    Drawing.drawComicBrow(ctx, face.farBrow, true, { inkColor: INK });
    Drawing.drawComicBrow(ctx, face.nearBrow, false, { inkColor: INK });
    Drawing.drawComicMouth(ctx, face.mouthGuides, { inkColor: INK });
    ctx.restore();
    ctx.translate(320, 0);
}

// One brow moved and the other did not, which is the whole of it.
log(`near tail ${quizzical.nearBrow.outer.y.toFixed(1)}, far tail ${quizzical.farBrow.outer.y.toFixed(1)}`);
ctx;
```

- **`near` and `far`, never `left` and `right`.** `nearEye`, `nearBrow` and the mouth's
  `rightCorner` are all the **`+x`** side of the facial axis at every yaw — a side of the *page*, not
  of the character. Left and right would be a claim about the character's own anatomy that a turned
  head cannot keep, so those two spellings are **refused by name** with that reason rather than
  quietly mapped onto whichever half happens to face us.
- **`AU26` ignores it**, because a jaw does not drop on one side. Documented rather than refused: a
  tuple carrying a jaw drop beside brow units is an ordinary thing to ask for one-sided.
- **Omitting the option moves both halves**, so nothing drawn before this changes.

> **This came from outside the corpus, and from three directions at once.** ARKit and MediaPipe both
> split their brow units per side — `browDownLeft`/`browDownRight`, `browOuterUpLeft`/`Right` — and
> CANDIDE-3’s model file carries an *Eyes vertical difference* shape unit — the file rather than
> Ahlberg’s report, which documents twelve shape units and not that one. **Asymmetry was the one axis every one
> of those models had and this construction had nowhere at all**, which is a good argument for
> reading a model built for a completely different purpose: a tracker and a videophone codec both
> found it necessary, and a drawing manual never thought to mention it.

### 4c. The iris is drawn 1.7× life size, and that is the idiom

`drawComicEye` puts the iris radius at **0.32 of the eye's drawn width**. Measured against a real
face it should be **0.19**: on MediaPipe's canonical face model (Apache 2.0, 468 vertices) the iris
diameter is **0.1886 of the interpupillary distance**, read straight off the iris ring vertices — and
this construction places the pupils one eye-width either side of the facial axis, so the
interpupillary distance is exactly twice the drawn width and the two ratios are the same number.

**So the default is 1.70× life size, and it stays.** §1 measures the comic head as *narrower relative
to its eye* than Loomis's; a larger iris is that same finding from the other side, and the two
together are most of why a comic face reads as drawn rather than observed. Pass
`irisRatio: 0.19` for a naturalistic eye; the default is left alone for the reason the aperture's
`0.45` was, because moving it would restyle every face already drawn.
- **The curve passes through `peak`, not toward it.** A quadratic's midpoint is a quarter of each end
  plus half its control, so a control set at the landmark reaches only halfway to it and the arch
  draws at about half the height the landmark states.
- **The mass is blunt at the head and pointed at the tail.** Taper both ends and it reads as a
  moustache set above the eye.

> **The landmark this replaced was doing active harm, and the harm was invisible.** `AU1` and `AU4`
> displaced `head.brow` — the cranium's own centre and radius. So **raising the eyebrows shrank the
> skull and frowning grew it.** Measured on a 240px head: silhouette **208.5px** wide at rest,
> **188.9** under `AU1` at full weight, **225.4** under `AU4` — a **36.5px swing in head width across
> one expression range**, on a character meant to stay the same person from panel to panel.
>
> Nothing downstream could report it, and no single render shows it: a head with raised brows simply
> looks like a slightly narrower head. It is §7's rule from the other direction — **a parameter that
> moves a landmark the construction uses as an *attachment* will break the silhouette** — and the
> brow was the head-level instance of the same mistake the jaw stations made.

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

// The character is a short list of numbers. Keep them at the top of artwork.js and the face keeps.
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

- **It is a pure function.** No clock, no randomness, no memory. The same numbers give identical
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

## 6a. One dial from portrait to caricature

> **Implemented by**: `Drawing.exaggerateHead(head, amount, reference)` and the primitive underneath
> it, `Drawing.blendHead(base, from, to, amount)`.

> **Source Reference**: Susan E. Brennan, *Caricature Generator: The Dynamic Exaggeration of Faces by
> Computer*, **Leonardo 18(3):170–178, 1985** — §IV *A Theory of Caricature* and §VI *Implementation*.

§6 makes a head a **particular** person. This makes that person **more so**, and it is one number:

```js
const canon = Drawing.createLoomisHead(400, 120, 240);
const mort  = Drawing.createParametricHead(canon, MORT);

Drawing.drawLoomisWireframe(ctx, mort);                          // the portrait
Drawing.drawLoomisWireframe(ctx, Drawing.exaggerateHead(mort, 1));  // the caricature
```

**The theory is thriftier than it sounds, and that is what makes it implementable.** Brennan's
premise is that a caricature exaggerates what distinguishes a face from a **norm** — but her program
makes **no judgment at all** about which feature is distinctive. It exaggerates every spatial
relationship in parallel, on the argument that *a relationship becomes a "feature" only when it
differs significantly from the corresponding relationship on a comparison face*. The part that would
otherwise need a trained model is the part being declined, and declining it costs nothing.

Her own description of the arithmetic is the clearest one there is: **the converse of in-betweening —
rather than averaging points together, the distance between them is increased.**

### How far to push it

Her published ladder runs **0, 50, 100, 140 and 160 per cent**, and she names **100** — `amount: 1`
here — as the best caricature in her own sequence. The bound she quotes, from Francis Grose's *Rules
for Drawing Caricaturas*, is the one worth holding: **a modest deviation causes laughter, a great one
incites horror.**

| `amount` | what you get |
| ---: | :--- |
| `0` | the head untouched |
| `0.3`–`0.5` | a face that reads as drawn rather than constructed |
| `1` | Brennan's best — unmistakably a caricature, still the same person |
| `> 1.5` | the region Grose warns about |
| `-1` | the reference itself |

### Three things it is not

**It is not a style setting.** Exaggeration moves landmarks; it does not change line weight, ink
tier or the simplification level §3 governs. A caricatured head still wants §3's decisions made.

**It does not survive a broken silhouette, and you can measure how close you are.** Brennan
deliberately left her lines unconstrained, so at high exaggeration *an eye is free to float above an
eyebrow* — she kept it because her users enjoyed finding the limit. But her users were watching a
screen. `createHeadGeometry` (§7) unions its masses into **one contour**, so the same freedom arrives
there as a broken outline rather than as a style, and it renders perfectly either way.

```js
const check = Drawing.verifyHeadOrdering(Drawing.exaggerateHead(mort, 1.4));
Stage.check('head still reads as a face', check.ordered, `margin ${check.margin.toFixed(3)} H`);
```

**Watch `margin` rather than `ordered`.** It is the tightest gap between consecutive stations — crown,
hairline, brow, eyes, nose, mouth, chin — as a fraction of head height, so it narrows toward zero
*before* anything crosses. That is the difference between seeing the edge and discovering it.

**Nothing is clamped, and the reason is a measurement rather than deference to Brennan.** How far a
head can be pushed varies more than thirty-fold by character: the canon never breaks, a mild
character holds past **12.6**, and one carrying `noseLength: 1` breaks at **0.40** — below the
exaggeration Brennan recommends. No constant could serve both. **`noseLength` is the usual culprit**,
because its full range already spends most of the canon's nose-to-mouth distance and leaves little to
exaggerate.

**One norm is the studio's simplification, not hers.** `exaggerateHead` defaults its reference to the
canon — the same head with no character parameters — because that is the convenient answer. Brennan
found the opposite: her results *"throw into question the idea that there need be only one strong
norm for all human faces"*, and a successful caricature frequently came from comparing against **any
face that simply seemed very different**. The third argument is how you disagree.

### The same call does expression

`exaggerateHead` is one spelling of a more general operation, and the general one is worth knowing
because **an expression is reached the same way a caricature is.** Brennan's own animation package
generated each frame by comparing a face to a stored template cycle — *"a generic smirk"* — which is
the same subtraction with a different second argument:

```js
// A stored expression, applied at a weight. `neutral` and `smirk` are ordinary heads.
const beat = Drawing.blendHead(mort, neutral, smirk, 0.6);
```

So a new expression is a **head you build once and keep**, not a rule someone has to write. That is
the same economy §6 gets from a `const`: the character is data, and so is the performance.

> **Normalisation is what lets a template travel.** Every value is measured in head units before it
> is differenced — a point as `(p − origin) / unit.H` — so a template built once at the origin at
> 240px applies at full strength to a head anywhere on the page at any size. Without that step the
> blend amplifies differences of *size and position* rather than of *shape*, and the failure is
> quiet: the face simply expresses less than it was told to.

> **Both heads must be at the same yaw**, and a mismatch is refused rather than averaged.
> Interpolating between a frontal and a three-quarter head passes through a projection corresponding
> to no viewing angle, and reads as a face melting rather than turning. Pitch is not checked, because
> it cannot be recovered from the head the way yaw can — that one is yours to keep straight.

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
- **`jawShape` widens the angle and the chin, and holds the station nearly still — because the station
  is a joint.** Loomis hangs the jaw off the ball's halfway line *station to station*, so that point is
  where the mandible meets the skull, and a skull does not widen when a character's jaw does. What a
  square jaw actually broadens is the **gonion** and the chin.

  > **This was the other way round until 2026-09-18, and the drawing is what showed it.** The station
  > carried the full displacement, the angle 0.8 and the chin 0.35. Measured on a 240px head, the
  > station sits at dx **76.7** against a cranium reaching **76.6** — exactly on the ball — and a full
  > share took it to **93.1**. The jaw's top edge is a straight line between the two stations and the
  > ball curves inward above them, so past the ball's reach the two met at a **sharp lateral spur**
  > with nothing over it: a bow tie rather than a jaw, at every value above zero. The shares are now
  > station `0.15`, angle `1.0`, chin `0.55`, and the outline stays smooth across the whole range.
  >
  > Worth knowing as a general case rather than as one fix: **a parameter that moves a landmark the
  > construction uses as an *attachment* will break the silhouette**, because the mass it attaches to
  > does not move with it. The stations, `jaw.ear` and the neck's anchor are all of that kind.
- **`chinShape` is the jaw's second axis**, and it exists because one dial could not reach the faces a
  page needs. `jawShape` spreads all six stations together — *how wide is this jaw* — so a broad jaw
  ending in a **point**, or a narrow one ending **square**, were unreachable at every setting. This
  moves the two chin corners against the chin itself, so the two multiply: three jaw widths by three
  chin shapes is nine outlines, not three.

  Negative points and drops the chin; positive squares and lifts it. The vertical part is a quarter of
  the horizontal and is anatomical coupling rather than a length control — a chin coming to a point is
  longer than one ending square.
- **`chinLength` is the third axis, and it is the one that carries the jaw angle with it.** The note
  above used to end *"chin length is still not reachable: lengthening the lower face properly means
  taking the angle down with the chin, and moving the chin alone would only stretch the last inch of
  the outline"*. That sentence is now the specification: the chin takes a full step and the **angle
  takes half of one**, because a mandible that lengthens lengthens in two segments — the ramus from
  the station down to the angle, and the body from the angle forward to the chin. Move the chin alone
  and the jaw still ends where it did, with a spike hung off it.

  **The stations stay exactly where they are**, which is the same rule the box above states for
  `jawShape` read on the other axis: a station is the joint against the skull, and a skull does not
  get *longer* because a jaw does any more than it gets wider. Three jaw widths × three chin shapes ×
  three lengths is twenty-seven outlines from three numbers.

  > **Only the shortening half constrains a caricature, and it constrains it loosely.** Against
  > `verifyHeadOrdering` on 240px heads, `chinLength: -1` holds to λ **2.85** and `chinLength: +1`
  > **never breaks** — a lengthening chin moves *away* from the mouth, so it widens the very gap the
  > ladder measures. Compare `noseLength: 1`, which breaks at **0.40**, below Brennan's own
  > recommended exaggeration. Full scale here is `0.05 H`, about a quarter of the canon's mouth-to-chin
  > gap, deliberately more modest than the nose's `0.07 H`: this one moves the **silhouette**, so it
  > reads at panel size at a fraction of what a mark inside the face needs.

  > **It also shortens the neck, and nothing had to be changed for that.** `createHeadForFigure`
  > measures chin-to-sternum *after* applying the character parameters — an order kept on the argument
  > that a parameter which ever moved the top-level `chin` would otherwise hang the neck off a chin
  > that no longer existed. `chinShape` and `chinLength` are that parameter, so a long-jawed character
  > gets the shorter neck its own chin leaves room for, from a decision taken before there was
  > anything to decide.
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

## 8. Putting the head on a body

§7 makes a head. It does not make a **person** — and the join is exactly where this manual and
Manual 08 stop talking to each other. That one builds a figure with `Drawing.createMannequinFigure`;
this one builds a head with `Drawing.createLoomisHead`. Nothing said how one goes on the other, and
between them sit four conversions, **three of which fail in ways a render does not announce**. A head
too wide for its own shoulders still looks like a head. A neck stopping short still looks like a neck
until you notice the daylight under the chin.

Every one of them starts from the figure's own stations, in head units down from the crown:

| Station | Where |
| :--- | :--- |
| `0.00 H` | crown |
| `1.00 H` | chin — the figure's head egg is `ry = 0.50 H`, so it is exactly one unit tall |
| `1.15 H` | neck |
| `1.40 H` | shoulder line, and the sternal notch with it |

**`Drawing.createHeadForFigure(figure, options)` does the conversions and hands back an ordinary
head** — everything in §1–§7 takes it unchanged — carrying an extra `fit` block that says what it
worked out.

### The four things it settles

**1. Where it goes and how big it is.** `createLoomisHead`'s `originY` is the head's vertical
**centre**, not its crown, where every proportion table in Manual 08 measures from the crown. So the
natural mistake puts the head half a unit low, which on an eight-head figure is a head resting on its
own sternum. The conversion is `figure.head.center` and `2 * figure.head.ry`.

**2. Which skull — and the default here is `comic`, against `loomis` everywhere else.** The figure's
head egg is `2 * rx` = **0.72 H** across. A Loomis head is `3 * (H / 3.5)` = **0.857 H**, so dropped
onto a mannequin it overhangs its own shoulders by 19%. The comic skull of §1 is 5/6 of Loomis =
**0.714 H** — the egg to within one per cent. **The mannequin has been carrying a comic skull all
along**, and nothing had noticed; the surprising default is the one that matches the body.

**3. How long the neck is, measured rather than assumed.** `createHeadGeometry`'s own default
`neckLength` of `0.30` ends at `1.30 H`, and the shoulder line is at `1.40 H` — **a tenth of a head
unit of daylight**. This measures chin to sternal notch on the figure in hand, so a posed figure gets
the length its own pose needs rather than the canon's.

**4. The lean, which is reported and not applied.** `figure.head.angleDeg` is `spineDeg + neckDeg`, a
roll on the page — and `createLoomisHead` takes only yaw and pitch, so there is no parameter for it.
`fit.rollDeg` and `fit.pivot` come back for you to apply with a transform. Skip it and a leaning
figure keeps an upright face, which is the failure Manual 08 §3 warns about without saying what to do.

**Two ordering rules, and both were found by rendering rather than by reasoning.** Neither is
obvious and each produces a picture that is wrong in a way the geometry cannot report:

1. **Draw the head and neck first, and the torso over them.** The neck is a closed shape, so drawn
   on top of the body its base is outlined straight across the chest — a tube laid on a mannequin
   rather than a neck going into it. The shoulders have to cover it, which means they go on last.
2. **Cut the mannequin's own head out of the body.** `createFigureGeometry` includes a head egg at
   exactly the place you just composed a head, so a body drawn afterwards paints over the face.
   `silhouette.subtract(groups.head)` is the whole of it.

```javascript
const canvas = createCanvas(520, 760);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9'; ctx.fillRect(0, 0, 520, 760);
const INK = '#1a1a18';

const MORT = { eyesDistance: 0.78, eyesSize: -0.22, noseLength: -0.72, jawShape: 0.88, mouthWidth: 0.30 };

const fig = Drawing.createMannequinFigure(260, 70, 620, {
    pose: { spineDeg: 14, neckDeg: -6 }, shoulderTiltDeg: -8 });

// Rule 2 - the mannequin carries its own head egg, and it would paint over the face.
const figGeo = Drawing.createFigureGeometry(fig);
const body = figGeo.silhouette.subtract(figGeo.groups.head);

const head = Drawing.createHeadForFigure(fig, { yawDeg: 22, character: MORT });
log(`skull ${head.fit.skull}, neck ${head.fit.neckLength.toFixed(3)} H, lean ${head.fit.rollDeg.toFixed(1)} deg`);

// The lean the figure is already in. Skip it and the face sits upright on a tilted body.
const lean = () => {
    ctx.translate(head.fit.pivot.x, head.fit.pivot.y);
    ctx.rotate(head.fit.rollDeg * Math.PI / 180);
    ctx.translate(-head.fit.pivot.x, -head.fit.pivot.y);
};

// Rule 1 - head and neck first. No options: skull and neckLength come from the fit.
const geo = Drawing.createHeadGeometry(head);
ctx.save(); lean();
ctx.fillStyle = '#efe9dc';
ctx.fill(geo.silhouette);
ctx.strokeStyle = INK; ctx.lineWidth = 2.5;
ctx.stroke(geo.silhouette);
ctx.restore();

// ... then the torso over the neck's base.
ctx.fillStyle = '#d8d2c4';
ctx.fill(body);
ctx.strokeStyle = INK; ctx.lineWidth = 2.5;
ctx.stroke(body);

// Features last, inside the same lean and clipped to the head's mass.
ctx.save(); lean();
ctx.clip(geo.mass);
Drawing.drawComicEye(ctx, head.nearEye, false, { inkColor: INK });
Drawing.drawComicEye(ctx, head.farEye, true, { inkColor: INK });
Drawing.drawComicNose(ctx, head.noseWedge, { inkColor: INK });
Drawing.drawComicMouth(ctx, head.mouthGuides, { inkColor: INK });
ctx.restore();

canvas;
```

**`fit` travels with the head**, so `createHeadGeometry` reads `neckLength` and `skull` from it unless
you pass your own. One source of truth, and nothing to remember to forward.

`yawDeg` defaults to **0** rather than `createLoomisHead`'s 35: a head on a figure faces where the
figure faces until told otherwise. `character` goes to `createParametricHead` (§6), so the same five
numbers give the same person on every panel of the page.

### The craft — what makes a head sit rather than perch

The arithmetic above puts the head in the right place at the right size. It does not make it **sit**
there, and the two sources agree about why: the join is built from the **pit of the neck**, and the
neck is described by the muscles that turn the head rather than by its own outline.

**Faragasso's order is head, centre line, then shoulder axis** — and it runs opposite to the order the
toolkit makes natural. Draw the head shape; run a centre line down it **to the pit of the neck**; then
draw the shoulder axis *through that point* to the widest points of the shoulders, taking care over
its angle. In our terms the pit of the neck is `figure.sternum`, and it is the anchor rather than a
consequence: everything about the shoulders is measured from it. (The shoulder line is an axis and
not the clavicles — Manual 08 §3 carries that, from the same page of Faragasso.)

**His measure is a head-length.** The distance from the pit of the neck to the widest point of the
shoulder is **one head length in a woman and one and a third in a man**, and he adds at once that
these vary greatly in real life. `createMannequinFigure`'s `shoulderSpanHeads` defaults to `1.8`, so
**0.9 H a side** — narrower than both, deliberately, because it is a *joint* span rather than the
figure's widest point (Manual 08 §1). If you want Faragasso's shoulders, say `2.67`.

**The trapezius is what fills the corner this manual leaves as a seam.** From the widest shoulder
point, go up on an angle to where the clavicle ends, then across to a point on the centre line at the
**back** of the neck; repeat on the other side, then add the clavicles and the sterno-mastoids. That
triangle is precisely the gap between the composed neck and the composed shoulder — so the remedy for
the seam is not a smoothing pass, it is a shape with a name.

**And he gives a check for the whole thing**: draw an **arc around the head** out to the widest points
of the shoulders. If the shoulders do not land on it, the head is the wrong size for the body — which
is the failure §8's `skull` and `headHeight` conversions exist to prevent, arrived at from the
drawing side rather than the arithmetic one.

#### The commonest mistake, and where our neck actually falls

Faragasso names it outright: **the neck in the wrong position as it relates to the head.** He gives
three defensible relations, chosen by the neck's width and the viewpoint — the line of the neck meets
the face at the **side of the eye socket**, at the **edge of the nostril**, or at the **corner of the
mouth**.

Those are landmarks the construction already computes, so the relation is measurable rather than a
matter of eye. Measured on a canon head at yaw 0, as a fraction of head height `H`:

| Faragasso's relation | dx from the facial axis | `neckWidth` to get it |
| :--- | :--- | :--- |
| side of the eye socket — the widest | `0.214 H` | `head.unit.thirdH * 1.50` |
| corner of the mouth | `0.121 H` | `head.unit.thirdH * 0.85` |
| edge of the nostril — the narrowest | `0.071 H` | `head.unit.thirdH * 0.50` |
| **the toolkit's default** | **`0.186 H`** | `head.unit.thirdH * 1.30` |

```js
// A narrower, more classical neck: Faragasso's mouth-corner relation.
const geo = Drawing.createHeadGeometry(head, { neckWidth: head.unit.thirdH * 0.85 });
```

**So the default neck is a wide one** — between the mouth corner and the eye socket and much nearer
the eye socket, not the thin stalk it looks like against a mannequin's shoulders. That impression is
the shoulder span being narrow, not the neck. **`neckWidth` is in pixels**, unlike `neckLength`, which
is a fraction of head height; deriving it from `head.unit.thirdH` is what keeps it scale-free.

#### Loomis on why a tipped head is not a hinge

**The pivot is deep and set back.** It is at the top of the spine and the base of the skull, *well
inside the roundness of the neck and deep under the skull*, a little behind the centre line — a
rotating action, not a hinge. Three things follow that a surface rotation does not give you: tipped
**backward**, the neck is squeezed and bulges into a crease at the base of the skull; tipped
**forward**, the larynx drops and hides itself within the neck; and the head *"drops forward mostly of
its own weight"*, which is a gesture note as much as an anatomical one.

That is what `fit.rollDeg` cannot do for you. Rotating the composed head about its own centre is a
surface rotation — right for a lean, and silent about all three.

**The two muscles that turn the head run from behind the ears to the breastbone.** Loomis attaches
them to the skull *just behind the ears* at the top and to the breastbone *between the two
collarbones* at the bottom. In our terms that is **`jaw.ear` to `figure.sternum`** — two points the
toolkit already hands you, and a line worth drawing even when nothing else on the neck is:

```js
// The cord that makes a turned head read as turned.
ctx.beginPath();
ctx.moveTo(head.jaw.ear.x, head.jaw.ear.y);
ctx.lineTo(fig.sternum.x, fig.sternum.y);
ctx.stroke();
```

At the back, two more attach under the back of the skull and hold the head up. `createHeadGeometry`
anchors its neck under the ear for exactly this reason, which is §7's note read from its source.

**One rule for the jaw seam**: the crease between neck and jaw *"seldom runs up to the ear but points
below it"*, and is *"seldom sharply defined"*. So the concave step §9 warns about should be inked as a
soft mark aimed **below** the ear — not closed up, and not run to the ear itself.

### What the join still does not do

- **It composes two silhouettes; it does not weld them.** The neck reaches the shoulder line and
  stops there. Where neck meets trapezius a real drawing has a mass bridging them, and this has a
  seam — the same order of defect as the missing cheek in §9, and the same remedy: ink over it, or
  union your own wedge in.
- **The roll is yours to apply, and it is a transform rather than a rotated construction.** The
  landmarks inside the head are still axis-aligned, so anything you measure off them after rotating
  the context is in the unrotated frame. Measure first, draw second.
- **Nothing here is depth.** A figure whose arm crosses in front of the chin still needs a clip to
  say so; `order` on either object is construction order, not z. The two ordering rules above are
  the same fact in its commonest form.
- **There is no trapezius**, so the corner between neck and shoulder stays a seam. That is the one
  shape the craft section names and the toolkit does not build; union your own in. (An earlier draft
  of this list called the neck *thin* — measured, it is nearer Faragasso's **widest** relation, and
  what looks thin is the mannequin's narrow shoulder span.)
- **The ear is easy to lose at a small yaw.** It straddles the ball's silhouette, so at the yaws a
  figure shot usually wants it is a few pixels wide. Check `parts.ear` rather than assuming it drew —
  and note that **`parts.nearEar` is meant to vanish**: it stops altering the outline at 10°, which
  is the near ear going behind the head rather than a mass failing to build.

## 9. What this does not give you

- **A composed head is not a rendered one.** §7 closed the gap this section used to name: there is
  now a silhouette, an ear and a neck, and §8 joins it to a body. What it gives you is *shape* — four
  masses and their union. It does not shade them, and knows nothing about hair, which is now the
  largest untouched thing on a comic head. `Drawing.drawHairRibbon(...)` draws one strand
  and nothing decides where strands go.
- **Nothing in it is depth.** `order` is the sequence the masses are built in, not a z-order, exactly
  as on a figure. A head turned far enough that the far jaw passes behind the neck still needs you to
  say so with a clip.
- **~~There is no cheek.~~ Fixed 2026-09-19 — `parts.farCheek` and `parts.nearCheek`.** This limit
  read: *"the ball's outline curves inward while the jaw's curves outward, and where they cross the
  union shows a shallow concave step. A real head has a mass bridging them and this does not; at
  panel size it reads as a cheekbone, at portrait size it reads as a seam. Ink over it, or union
  your own wedge in."*

  > **Every clause of that was wrong, and the sequence of wrong answers is the lesson.** Three
  > constructions were built and measured before the right one, and two of them appeared to work.
  >
  > The note said *shallow*. Scored the way the note implied — how far the edge falls inside its own
  > 40px chord, which a convex outline never does — the dip measures 6.5px on a 480px head, and a
  > wedge that reduced that number looked like a fix. **Printing the outline row by row instead**
  > said something else entirely: the far edge holds 205–211px off the axis from the brow down to
  > y=564 and then reads **153 at y=572**. A 45px cliff in eight rows, with everything above and
  > below it already smooth and monotonic.
  >
  > The note said *where the ball crosses the jaw*. It is neither: it is **the foot of the ear**. An
  > ear here is a tall narrow ellipse riding the ball's silhouette, so its lower half hangs a long
  > way outboard of a ball that is collapsing under it — and then it simply stops at the nose line.
  >
  > **A capsule from the cheekbone to the jaw angle moved the notch rather than removing it**, from
  > 62% of brow-to-chin to 70% — its own toe, landing on a jawline it was not tangent to. **A wedge
  > carried on to the chin corner** removed more of it and added **23–34px of width** to the lower
  > face, five times the defect it was correcting. **A straight line from the ear's widest point to
  > the jaw angle did nothing at all**: it lies inside the ball for its whole length. The A/B render
  > is what said so, after a metric had implied it worked.

  The cheek is the **outer tangent from the jaw angle to the ear** — the masseter's own run, from the
  zygomatic arch to the angle of the mandible. Tangency is what makes the join seamless: the outline
  never leaves the ear, it rolls off it. Both ends are landmarks the construction already carries, so
  **the call gained no constant** — nothing to tune, nothing to defend by eye. The worst single-row
  drop goes from **38px to 4px** on a 240px head, and 4px is the floor: a head turned far enough that
  no cheek is acting measures the same.

  It **cannot widen a head**, being strung between two things already on the outline, so every head
  drawn before this keeps its width and stops having a bite taken out of it. The **near cheek empties
  on its own** as the head turns, with no factor applied — it hangs off the near ear, which is already
  narrowing and riding inboard, so past about 35° the triangle falls inside the cranium. Frontal and
  near-frontal heads are what change.

  > **And it found a defect upstream that nothing was looking for — now fixed, same day.** A frontal
  > head's jaw was not symmetric: `jaw.angle` sat at **−0.750 units** off the axis and
  > `jaw.nearAngle` at **+0.868**, because the far angle was derived from the ear landmark
  > (`originX − unit × cos`) and the near one from the jaw station (`originX + √(ballR² − unit²)`) —
  > 1.000 units against 1.118. The chin corners inherited it, −0.600 against +0.694, and a 240px
  > frontal silhouette measured **7.48px wider on one side** through the jaw, 3% of head height on a
  > head with no turn in it.
  >
  > **`nearAngleX` is now written as the far angle mirrored about the axis** —
  > `MathF.Max((2f * originX) - jawAnglePt.X, …)` — rather than as a second formula for the same
  > rule, so the two cannot restate it differently again. The chin guard that stops the near jaw
  > crossing the chin at high yaw is untouched.
  >
  > **Measured, the change reaches nothing past 40°.** Diffing every jaw landmark between the two
  > builds across the canon and two parametric characters: the near angle moves **8.09px at yaw 0**
  > on a 240px head, 6.63px at 35°, and **0.000px from 40° on** — the chin guard already governed it
  > there, so every turned head renders exactly as it did. `chinShape`, `chinLength` and `jawShape`
  > displace on top and move by the same amount as the canon, so no character is affected
  > differently from any other.
  >
  > **What let it hide is worth more than the fix.** `TestAFrontalHeadIsSymmetricAboutItsOwnAxis`
  > compared the silhouette's **bounds**, and a head's width is set by its ears — so the box is
  > symmetric whatever the jaw does underneath it. One number at the widest point cannot see a
  > shape. That test now walks the outline from brow to chin with a sub-pixel bisection and asserts
  > both sides agree within 0.5px, and `TestAFrontalHeadsJawMirrorsItself` asserts the landmarks
  > directly. Both fail against the old code; the first reports *"7.48px wider on one side at
  > y 210"*, which is the defect in the words of the test that should have caught it.
- **~~One ear, on the side `jaw.ear` names.~~ Fixed 2026-09-19 — both are drawn.** This limit read:
  *"the construction carries a single ear landmark, which is right for a three-quarter view — the far
  ear is hidden by the head — and wrong for a frontal one, where a reader expects two. Mirror
  `parts.ear` about `crown.x` when the head is square on."* `createHeadGeometry` now returns
  `parts.nearEar` beside `parts.ear`, so a caller still mirroring by hand gets **three**.

  > **Worth keeping as a lesson about defaults rather than about ears.** The limit was documented,
  > accurately, in two places — and the person who wrote both of them then drew a frontal expression
  > sheet with this toolkit and shipped eight one-eared heads without noticing. A documented defect
  > is still a defect: it was anatomy rather than style, so nobody had chosen it, and the workaround
  > asked every caller to remember something on every frontal head forever. **The director spotted it
  > in the render.** If a note has to tell callers to correct the output, the output is wrong.

  The near ear **narrows where the far one widens** — an ear is edge-on frontally and full-face in
  profile, so that is one fact read from both sides. Measured on a 240px head it stops altering the
  outline at **10°** (2.71px frontally, 0.57px at 8°, nothing beyond), which is the physics rather
  than a fudge: a frontal ear sits exactly *on* the ball's silhouette, so any turn toward it puts it
  behind the head's own edge. Turned heads render byte-identically to before.
- **The plates carry what the text cannot.** The five- and six-step figures for the female head and
  profile are drawings; the text gives their order and their rules, which is what is distilled above.
- **Nothing here is about likeness.** Neither school offers it, and no construction will.
