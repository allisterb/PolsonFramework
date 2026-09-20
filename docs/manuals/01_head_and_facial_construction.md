# Studio Manual 01: Head & Facial Construction (The Loomis Method as Code)

> **Source Reference**: Andrew Loomis, *Drawing the Head and Hands* (Viking Press, 1956) — §1 from
> Plates 1–2 (pp. 21–22), §2 from "The Standard Head" and Plates 18–19 (pp. 43–44), corroborated by
> Michael Hampton, *Figure Drawing: Design and Invention* (2009), "Head Drawing, Step 5".
> `Drawing.createLoomisHead(...)` returns these proportions; §2a lists what it carries that neither
> source supplies.

> **Purpose**: Loomis's proportional canon and its construction, as formulas and Canvas2D algorithms, for heads in 3/4 and dynamic perspective. **A canon, not a measurement of humanity** — real heads vary, other schools divide them differently, and §2a says which numbers are Loomis's and which the toolkit added. Manual 23 carries the *comic* idiom for the same subject.

---

## 1. The Core Geometric Concept: Cranium Sphere + Facial Mask

> **Implemented by**: `Drawing.createLoomisHead(originX, originY, headHeight, yawDeg, pitchDeg)` → `LoomisHead`, then `Drawing.drawLoomisWireframe(ctx, head, options)` for the non-repro-blue construction pass. Signatures: `polson://sdk/core/Drawing`; model: `polson://sdk/schema/Drawing`.

Human head anatomy is not a collection of floating 2D shapes; it is a **3D volume** composed of two primary masses:
1. **The Cranial Sphere (Braincase)**: A 3D ball representing the skull.
2. **The Facial Mask & Jaw Plane**: A tapered wedge/cylinder extending downward and forward from the cranial sphere.

```
                    ┌─────────────────────────┐
                    │      Cranial Dome       │
                    │   (Circle/Sphere: R)    │
                    └────────────┬────────────┘
                                 │
           ┌─────────────────────┴─────────────────────┐
           ▼                                           ▼
┌───────────────────────┐                   ┌───────────────────────┐
│   Temporal Slice      │                   │  Central Facial Axis  │
│ (head.temporalOval)   │                   │  (Rotated 3/4 Yaw θ)  │
└──────────┬────────────┘                   └──────────┬────────────┘
           │                                           │
           └─────────────────────┬─────────────────────┘
                                 ▼
                    ┌─────────────────────────┐
                    │      Loomis Thirds      │
                    │ Hairline → Brow → Nose  │
                    │        → Chin           │
                    └─────────────────────────┘
```

### The construction Loomis actually gives

> **Source**: *Drawing the Head and Hands*, Plate 1 (p. 21) — "The basic shape is a flattened ball".

The diagram above is a summary; this is the procedure, and it is worth having in full because every
landmark below is *derived* from it rather than measured independently:

1. The cranium is a **ball**. Establish an axis through it, as if a nail were driven through the top.
2. Divide the ball into **quarters** through the centres the axis establishes, and again at the
   **equator**.
3. **Slice off a fairly thin slice on each side.** That is the cranium's basic shape.
4. **The equator becomes the brow line.** One of the lines through the axis becomes the **middle line
   of the face**.
5. The **hairline** sits about halfway up from the brow line to the axis.
6. **Drop the middle line straight down off the ball.** On it, step off **two intervals each equal to
   the forehead** (brow → hairline): the first gives the **base of the nose**, the second the
   **bottom of the chin**.
7. The **jaw line** connects about halfway around the ball on each side.
8. The **ears** attach along the halfway line, and their height is about the distance from the brows
   to the base of the nose.
9. The ball can be tipped in any direction.

> [!IMPORTANT]
> **Step 6 is why the three divisions are equal — they are *constructed* equal, not observed to be.**
> One interval, the forehead, is stepped off twice. Any implementation that places the brow, nose and
> chin from independent fractions of head height has abandoned the construction even if the result
> looks similar.
>
> **Step 3 says "a fairly thin slice", and gives no fraction.** The diagram above says the temporal
> slice is a *"side oval: 2/3 R"*. That number is not Loomis's and is not in the book; it is also not
> what `head.temporalOval` returns, which is `rx = 0.32·W`, `ry = 0.26·H` with no reference to a ball
> radius. That label has been removed from the diagram; use the returned oval.

### The stoss — the point the whole head hangs from

> **Source**: *Drawing the Head and Hands*, Plate 2 (p. 22) — "The all-important cross on the ball".

Loomis names the point where the **brow line crosses the middle line of the face** the *stoss*, and
calls it the key point in the construction of the whole head: it fixes the position of the facial
plane on the ball, and therefore the angle the head is seen from. This manual has never mentioned it.

It matters here because it is the one landmark that makes a 3/4 view coherent. Yaw is not a property
of the features — it is the stoss moving across the ball, with everything else following. On the
returned model the stoss is `head.brow` (the brow point already sits on the shifted facial axis), and
checking that a rotated head still reads correctly means checking that point, not the eyes.

---

## 2. The Loomis Unit Scale

> **Implemented by**: the returned model resolves these as landmark positions — read `head.crown`, `head.hairline`, `head.brow`, `head.eyeLineY`, `head.noseBase` and `head.chin`, and take differences between them. `Drawing.computeRelativeDistance(headHeight, a, b)` reports any two in head-length units rather than pixels. `head.unit.thirdH` **is** Loomis's unit, so each division is exactly one of them.

The face divides into **three equal segments**, plus the cranial dome above the hairline. Loomis's
unit is the segment: the dome is **½** of one and the head is **3½** tall.

| Section | Vertical bounds | Anatomical landmarks | In units | As a fraction of `H` |
|---|---|---|---|---|
| **Top dome** | crown → hairline | Top cranial volume, hair roots | ½ | 0.1429 |
| **Upper third** | hairline → brow | Forehead and temples | 1 | 0.2857 |
| **Middle third** | brow → nose base | Eyes, sockets, nose bridge and apex, ears | 1 | 0.2857 |
| **Lower third** | nose base → chin | Upper lip, mouth, lower lip, chin ball | 1 | 0.2857 |

`head.unit.thirdH` **is** that unit, so each row above is one `thirdH` and the dome is half of one.
The lip line sits **⅓ of a unit below the nose base** — the half-unit and third-unit marks inside the
lower unit are what Plate 18 uses to place the mouth.

### Critical Proportions

> **Source**: *Drawing the Head and Hands*, "The Standard Head" and Plates 18–19 (pp. 43–44). The
> scale is Loomis's own, "worked out after a great deal of research… simple and practical", and he
> is explicit that **you establish your own unit — it is the proportions that matter.**

**The whole head, in units:**

| Measurement | Loomis |
| :--- | :--- |
| Height | **3½ units** (the half is optional) |
| Width, including the ears | **nearly 3 units** |
| Depth, tip of nose to back of head | **3½ units** |
| Side view | fits exactly into a **square 3½ units each way** |
| Crown → hairline | **½ unit** |
| The three face divisions | **1 unit each** — forehead, nose, jaw |
| Ear height | **1 unit**, spanning brow → base of nose |
| Nose (brow → base) | **1 unit** |
| Lips and chin | **1 unit** |

- **Eye line.** At **exactly half the total head height**, crown to chin — stated outright, and
  defended as what "averages out in a large percentage of actual faces". It falls a quarter-unit below
  the brow line, inside the middle division.
- **Horizontal placement is by half-units, not by eye-widths.** *"The half measurements of these units
  locate the eyes and nose and help in placing the mouth."* Plate 19 makes it concrete: the **face is
  2 units wide**, and the eyes fall at **the quarter points of those two units** — each eye **½ unit
  wide**, with **½ unit between the inner corners**.
- **So the gap between the inner eye corners is exactly one eye-width.** That claim, which this manual
  already made, is correct — but it follows from the half-unit grid rather than standing on its own.
- **The face is 4 eye-widths wide, and the head is 6.** On Loomis's scale the face is 2 units =
  4 eye-widths (¼ + 1 + 1 + 1 + ¼ across), and the head including ears is 3 units = 6. The widely
  quoted five-eye canon belongs to a different tradition and measures "face" differently; it is not
  in this book.
- **Nose base.** Half a unit — one eye-width — so it aligns with the inner corners of both eyes.
- **Mouth.** Loomis says only that the half-unit marks *help in placing* it, and gives no alignment
  rule — in particular he offers no pupil alignment, and the toolkit does not use one. The nearest
  thing in the book is a construction for **children's** heads, where the corners of the lips sit on
  the third of four division lines.

> [!NOTE]
> **Loomis gives two accounts of the dome and they do not agree.** Plate 1's freehand construction
> puts the hairline *halfway from the brow line up to the axis*, which makes the crown → hairline
> distance **¾ unit**; Plate 18's measured scale makes it **½ unit**. Prefer the measured scale — it is
> the one he calls worked out and practical — but do not present either as exact when the book itself
> carries both.

---

### A second school reaches the same numbers by a different route

> **Source**: Michael Hampton, *Figure Drawing: Design and Invention* (2009) — "Head Drawing, Step 5:
> Proportions".

Hampton does not use a unit scale at all. He finds the **brow line** and the **bottom of the jaw**
first, and then halves repeatedly between landmarks:

1. The **base of the nose** is halfway between the brow and the bottom of the jaw.
2. The **bottom of the eye sockets** is halfway between the nose base and the brow.
3. The **centre of the eyes** is halfway again, between the socket bottoms and the brow — and is also
   the bottom of the *keystone*, the bone between the eyes that he calls the most important area for
   likeness.
4. Nose base to bottom of jaw divides into **three equal parts**, giving the separation of the lips
   and the top of the chin.

**Work those out and they are Loomis's numbers exactly.** Taking the brow as 0 and the chin as 1:

| Landmark | Hampton, by halving | Loomis, by units |
| :--- | ---: | ---: |
| Nose base | ½ | (2.5 − 1.5) / 2 = **0.500** |
| Eye line | ⅛ | (1.75 − 1.5) / 2 = **0.125** |
| Mouth | ⅓ of nose → chin | ⅓ of nose → chin |

Three landmarks, two methods fifty years apart, no shared derivation, identical answers. That is
worth more than either source alone, and it is the reason the mouth's position — read off Plate 18's
hand-lettered ⅓ mark, the least certain number in §2 — can now be relied on.

> [!TIP]
> **Hampton's route is the one to use when the head is at an extreme angle.** He points out that
> proportions found by halving *between landmarks* stay consistent under any perspective, because
> they are ratios along the head's own axis rather than measurements in the picture plane. Loomis's
> unit scale is easier to lay out flat; Hampton's survives the turn. `createLoomisHead` computes the
> unit scale, but every landmark it returns can be halved between in exactly Hampton's way.

---

## 2a. What the Model Carries, and What Comes From Nowhere

`createLoomisHead` follows the scale above exactly: half a unit of dome, three equal units, the eye
line at `H/2`, the lip line a third of a unit below the nose, the head three units wide including the
ears, and each eye half a unit so the inner corners sit one eye-width apart. `unit.thirdH` **is** that
unit. The **ear** spans brow → nose and sits one unit off the cranium axis — which puts its outer edge
on the head's half-width, since Plate 18's three units *include* the ears — foreshortened by
`cos(yaw)` like the far eye.

### How the jaw connects

Plate 1 step 7: the jaw line **connects about halfway around the ball on each side**, and two things
in that sentence do the work.

- **The halfway line is the ball's own silhouette**, and the jaw leaves it at the nose line rather
  than at the equator — so the station has come in from the full radius to `sqrt(R² − unit²)`,
  foreshortened by `cos(yaw)`.
- **"On each side"** means the jaw has *two* stations, and the near one is the far one mirrored about
  the cranium axis. The chin between them is a bottom with real width, never a point.

`head.jaw` now carries the whole frame, so a script can draw its own jaw on the same points rather
than reverse-engineering them: `farStation`, `angle`, `chinFar`, `chinNear`, `nearAngle`,
`nearStation`, plus the existing `ear`, `chin` and `cheekApex`. They are ordered left to right, and
the chin corners are taken from the two angles rather than from a fraction of `W` — which is what
keeps the chin inside its own jaw at every yaw.

> **At yaw 0 the frame is an exact mirror, and it was not until 2026-09-19.** `angle` is derived
> from the ear landmark and `nearAngle` is that value mirrored about the cranium axis; the two
> stations are `±√(ballR² − unit²)`; the chin corners are 0.8 of their own angle's span from a chin
> that sits on the axis. Before that date `nearAngle` had its own formula, measured from the
> **station** rather than from the **ear** — 1.118 units against 1.000 — so a head with no turn in it
> came out −0.750 against +0.868 and drew 7.48px wider on one side. Manual 23 §9 has the whole
> account; the lesson for this page is that two spellings of one rule will eventually disagree.

> [!IMPORTANT]
> **Past about 35° the construction runs out, and it holds rather than inverting.** The facial axis
> swings out faster than the near angle comes in, so beyond that the near jaw would cross the chin
> and the path would turn inside out. `nearAngle` is floored just clear of the chin instead: the near
> jaw goes on shortening with the turn, but the left-to-right ordering of the frame holds, which
> `TestLoomisJawStaysOrderedAcrossTheTurn` asserts at 0°, 35°, 55°, 70° and 85°.
>
> **This said "about 60°" until 2026-09-19 and was wrong by twenty degrees.** Measured by sweeping
> the yaw in half-degree steps and watching for the floor to take over, it engaged at **40°** under
> the old near-angle formula and engages at **35.5°** now that the near angle mirrors the far one.
> Nobody had measured it; the figure was an estimate that read as a fact, and it sat two paragraphs
> from a documented working range of 30°-45° that it contradicts.
>
> §3's documented range for a 3/4 head is 30°–45°, so the floor engages **inside** that range rather
> than safely beyond it — which is the practical reason to know the real number. At 40° and beyond
> the near jaw is the floor's output rather than the construction's, so treat it as approximate
> there, not at 60°.

### Still unsourced, and marked as such

- **The temporal slice.** Loomis says only *"a fairly thin slice"* on each side and gives no fraction.
  `head.temporalOval` returns `rx = 0.32·W`, `ry = 0.26·H` — a studio choice, not a canon. The
  §1 diagram's old *"2/3 R"* was invented and has been removed.
- **The jaw's proportions**, though its *construction* is now Loomis's — see below. The angle's depth
  below the nose line and the chin's share of the span between the two angles are the studio's; he
  gives no measurement for either.
- **Mouth width.** Loomis places the mouth vertically and says the half-unit marks *help* with it
  horizontally, but gives no width. The corner offsets are the studio's.
- **`temporalOval`, `cheekApex` and the pitch weights** are all studio construction with no source.

---

## 3. Constructing a 3/4 View Head in Canvas2D

> **Implemented by**: `Drawing.createLoomisHead(...)`. The `yawDeg` argument drives the 3/4 turn; `pitchDeg` tilts it.

In a 3/4 view, the head is rotated around the vertical Y-axis by yaw angle $\theta \approx 30^\circ\text{ to }45^\circ$:

`Drawing.createLoomisHead(originX, originY, headHeight, yawDeg, pitchDeg)` performs this construction and returns every landmark below. The yaw term is what produces the 3/4 read: the facial centreline shifts by $\sin\theta$, and far-side features compress by $\cos\theta$.

The yaw offset is `sin(yaw) · W · 0.22` on the facial axis, and the far eye is scaled by `max(0.45, cos(yaw))` — the floor is what stops the far eye vanishing at a steep turn.

### The Returned `LoomisHead` Landmarks

| Field | Carries | Notes |
| --- | --- | --- |
| `head.unit` | `{ H, W, eyeW, thirdH }` | `thirdH` is Loomis's unit, `H/3.5`; `eyeW` is half of one, so the head is 6 eye-widths wide. |
| `head.crown`, `head.hairline` | Top dome bounds | The dome above the upper third. |
| `head.brow`, `head.eyeLineY` | Brow line and eye line | `eyeLineY` is **exactly** `H/2`, at 1¾ units. **`head.brow` is the ball's equator, not an eyebrow** — `createHeadGeometry` takes the cranium's centre and radius from it, and nothing but the construction sheet draws it. |
| `head.noseBase`, `head.mouthCenter`, `head.chin` | Lower two thirds | Each division is one `unit.thirdH`; the lip line is ⅓ of one below the nose. |
| `head.nearEye`, `head.farEye` | `{ inner, outer, center, width, height }` | Pass straight to `Drawing.drawComicEye(...)`. |
| `head.nearBrow`, `head.farBrow` | `{ inner, peak, outer, thickness }` | The **drawn** eyebrows, one per eye — pass straight to `Drawing.drawComicBrow(...)`. Three stations because two cannot carry an arch, and the arch is where AU1 differs from AU2. |
| `head.noseWedge` | `{ bridgeTop, apex, underNose, nearNostril }` | Pass straight to `Drawing.drawComicNose(...)`. |
| `head.mouthGuides` | `{ center, leftCorner, rightCorner, upperLipY, lowerLipY }` | Pass straight to `Drawing.drawComicMouth(...)`. |
| `head.jaw` | `{ ear, angle, nearAngle, farStation, nearStation, chinFar, chinNear, chin, cheekApex }` | The whole jaw frame, ordered left to right. Both stations are the ball's halfway line, foreshortened by the turn — §2a. |
| `head.temporalOval` | `{ cx, cy, rx, ry }` | The temple plane sliced off the ball. `rx = 0.32·W`, `ry = 0.26·H`; the diagram's "2/3 R" is not this and is not Loomis's. |

> Do not recompute these anchors by hand. The feature renderers consume the sub-objects above directly, so a hand-rolled structure with different key names will not draw.

---

## 4. Drawing Facial Features Step-by-Step

> **Implemented by**: `Drawing.drawComicBrow(ctx, browObj, isFar, options)`, `Drawing.drawComicEye(ctx, eyeObj, isFar, options)`, `Drawing.drawComicNose(ctx, noseObj, options)`, and `Drawing.drawComicMouth(ctx, mouthObj, options)` — each consumes the matching sub-object of the `LoomisHead` directly. For an emotional pose, run the head through `Drawing.applyFacialExpression(head, type, intensity)` first (Manual 08 §4).

### A. The Comic Eye (Intense 3/4 Gaze)

`Drawing.drawComicEye(ctx, eyeObj, isFar, options)` renders the whole feature — S-curve upper lid, shaded sclera, iris, pupil, and catchlight — from a `head.nearEye` / `head.farEye` object.

- Pass `isFar: true` for the far eye so it is drawn compressed and with a lighter lid weight.
- `options`: `{ inkColor, irisColor, scleraColor }`.

**The rules the renderer encodes**, worth knowing so you can judge the output:
1. **Upper lid is the heaviest line on the face** — a thick S-curve, thickest at the outer third.
2. **The iris is clipped by the upper lid**, never a free-floating circle.
3. **The catchlight sits opposite the key light** and is the only pure white in the eye.
4. **The far eye loses detail, not just width** — compress it and drop the catchlight rather than drawing a smaller copy.

### B. The Comic Nose (Bridge & Wing in 3/4)
In 3/4 view, the nose projects out from the far cheek silhouette:
1. **Keystone bridge**: Begins at the eye line level.
2. **Nose Ridge Slope**: Slopes forward down to the apex ($Y_{\text{nose}}$).
3. **Under-Nose Base**: Connects apex inward to the septum base with an angled dark shadow plane.
4. **Near Nostril**: Small curved teardrop stroke positioned at $(X_{\text{center}} + \text{width} \cdot 0.35, Y_{\text{nose}})$.

### C. The Determined / Open Comic Mouth
1. **Upper Lip**: M-shaped Cupid's bow line, darker fill or deep shadow line.
2. **Mouth Opening**: Angled wedge revealing white teeth shelf and dark mouth cavity (`#2c0d0d`).
3. **Lower Lip**: Defined by a **subtle shadow crescent** underneath rather than an outline around the whole lip!

### D. The three drawers hand their parts back

Each returns the shapes it built, so a feature can be worked on after it is drawn rather than only
looked at:

| Call | Returns |
| :--- | :--- |
| `drawComicEye` | `aperture`, `iris`, `pupil`, `catchlight`, `upperLid`, `lowerLid` |
| `drawComicBrow` | `mass`, `spine` |
| `drawComicNose` | `underPlane`, `bridge`, `nostril` |
| `drawComicMouth` | `cavity`, `teeth`, `lipLine`, `lowerLip` |

**`aperture` is the one that changes what you can do.** It is both the sclera fill and the clip the
interior is drawn inside — so it is what a highlight, a reflected window, or a hard-edged shadow from
the brow gets clipped to, and it stops exactly at the lid without anyone re-deriving the eyelid curve
from `inner`, `outer` and the eye's width:

```javascript
const canvas = createCanvas(520, 300);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f1e8'; ctx.fillRect(0, 0, 520, 300);

const head = Drawing.createLoomisHead(150, 150, 620, 10, 0);

const eye = Drawing.drawComicEye(ctx, head.nearEye, false, { inkColor: '#15151a', irisColor: '#3d5763' });
ctx.save();
ctx.clip(eye.aperture);
ctx.fillStyle = 'rgba(18,14,8,0.5)';
ctx.fillRect(0, 0, 520, head.nearEye.center.y - 4);   // a shadow that ends at the lid, not near it
ctx.restore();

log('eye parts: ' + Object.keys(eye).join(', '));
canvas;
```

`underPlane` and `cavity` are the two shadow shapes on a face, so `nose.underPlane.union(mouth.cavity)`
is a single mass to re-fill when the light moves — which is Manual 03's rule that *a heavy ink line is
the beginning of a shadow*, made actionable by the shadow being a shape you hold.

The lids and `lipLine` come back as **open centre-lines** rather than filled marks, so they can be
re-stroked at a different tier's weight — an eye at panel size wants a different weight from an eye in
close-up — or run through `ctx.strokeToPath(...)` to be tapered.

---

## 5. Constructing It: A Runnable Head

One construction call, one wireframe pass, then features fed from the model's own sub-objects.

```javascript
// 3/4 view head: Loomis construction → non-repro-blue guides → inked features.
const canvas = createCanvas(560, 640);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f6f4ef';
ctx.fillRect(0, 0, 560, 640);

// §3 — One call resolves the whole construction at a 35° yaw and 4° pitch.
const head = Drawing.createLoomisHead(280, 90, 420, 35, 4);
log('thirdH=' + head.unit.thirdH.toFixed(1) +
    '  eyeW=' + head.unit.eyeW.toFixed(1) +
    '  eyeLineY=' + head.eyeLineY.toFixed(1));

// §1 — Construction pass: cranial sphere, temporal slice, Loomis thirds.
Drawing.drawLoomisWireframe(ctx, head);

// §4 — Features consume the model's sub-objects directly. The far eye is
// passed with isFar = true so it compresses rather than shrinking.
Drawing.drawComicEye(ctx, head.nearEye, false, { irisColor: '#3b6a8a' });
Drawing.drawComicEye(ctx, head.farEye, true, { irisColor: '#3b6a8a' });
Drawing.drawComicNose(ctx, head.noseWedge);
Drawing.drawComicMouth(ctx, head.mouthGuides);

canvas;
```
