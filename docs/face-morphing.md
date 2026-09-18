# Face morphing — one operation for identity, caricature and expression

**Status: steps 1–5 implemented 2026-09-18; steps 6–7 pending the open questions in §7.** Written
after acquiring Brennan 1985.
Read `reference/README.md`'s rows for `papers/project_muse_601398.pdf` and
`articles/procegenresearch (1).pdf` first; the terms and the citation hazards are recorded there
rather than repeated here.

---

## 1. Why this exists

Three separate things in the SDK are the same arithmetic, and none of them is currently written that
way:

- **Identity** — `createParametricHead` displaces landmarks by five named parameters.
- **Caricature** — absent. There is no dial from portrait to comic.
- **Expression** — `applyFacialExpression` ships six named presets.

Brennan states the unification directly, and it is the mechanism she used for *both* halves of her
own system: two faces of identical topology are differenced point-by-point, the difference vectors
are scaled, and the result is added back. Exaggeration scales the difference up; her animation
package generated each frame by comparing a face to *"a stored template cycle (for, say, a generic
smirk)"* — so an expression is just another face in the same topology, reached by the same
subtraction.

Her description of the method is the clearest one available: **the converse of in-betweening —
rather than averaging points together, the distance between them is increased.**

> **What this buys, concretely.** `createParametricHead` takes five parameters because each one had
> to be hand-written as a displacement rule — `MoveEyes`, `StretchNose`, `SquareJaw`, `WidenMouth`.
> A sixth costs another rule. Under a morph, a new character or a new expression is **data** — a head
> built once — and costs no code at all.

---

## 2. What is actually there now, measured

### 2a. The topology already satisfies Brennan's correspondence requirement

Her database needed points *"consistent in number and order for each face"*, plus **virtual lines** —
invisible geometry kept on faces without wrinkles or a moustache — so that correspondence never
breaks. We get that free: every head from `createLoomisHead` has an identical key tree, because one
function builds them all. Correspondence is by **name path** rather than by index, which cannot drift
the way an ordered point list can.

The whole head is six top-level points, six groups and a handful of scalars:

| path | kind |
| :--- | :--- |
| `crown` · `hairline` · `brow` · `noseBase` · `mouthCenter` · `chin` | point |
| `nearEye` / `farEye` → `inner` · `outer` · `center` | point |
| `nearEye` / `farEye` → `width` · `height` | length |
| `noseWedge` → `bridgeTop` · `apex` · `underNose` · `nearNostril` | point |
| `mouthGuides` → `center` · `leftCorner` · `rightCorner` | point |
| `mouthGuides` → `upperLipY` · `lowerLipY` | y-coordinate |
| `jaw` → `ear` · `angle` · `nearAngle` · `farStation` · `nearStation` · `chinFar` · `chinNear` · `chin` · `cheekApex` | point |
| `temporalOval` → `cx` · `cy` | coordinate |
| `temporalOval` → `rx` · `ry` | length |
| `eyeLineY` | y-coordinate |
| `unit` → `H` · `W` · `eyeW` · `thirdH`; `origin` → `x` · `y` | **metadata — never blended** |

**That table is the schema, and writing it down is half the work.** A key present in one head and
absent in the other must be **refused by name** rather than skipped: a silently skipped landmark is a
face that morphs everywhere except one feature, which reads as a drawing bug rather than a
correspondence one.

### 2b. `applyFacialExpression` is weaker than its own manual

Manual 08 §4 describes each preset in muscle terms — *"Zygomaticus major contracts ⟹ mouth corners
pull up & out; Orbicularis oculi contracts ⟹ lower eyelids push up"*. **The code moves one or two
landmarks per preset and never touches an eyelid at all:**

| preset | what it actually displaces |
| :--- | :--- |
| `joy` | mouth corners up. Nothing else. |
| `anger` | the `brow` **point** down. Not the eyes, not the mouth. |
| `fear` | brow up, mouth centre down |
| `sadness` | mouth corners down |
| `surprise` | brow up, mouth centre down — `fear` at a larger amount |
| `disgust` | `upperLipY` raised |

It is also a **shallow** clone where `createParametricHead` is a deep one, so a caller holding the
original head can have a nested group swapped underneath it.

This is not a criticism of the enumeration. Manual 08 already withdraws the six-universal claim and
records Loomis's own refusal to tabulate emotions. It is a statement that **the six presets are a
stub rather than behaviour worth preserving.** What is worth preserving is the six *names*, as an
entry point for a caller who does not want to think in muscles.

### 2c. `eye.height` is written, and read by nothing

`createLoomisHead` sets `nearEye.height = eyeW * 0.45` and `farEye.height = eyeWFar * 0.45`.
**`drawComicEye` never reads it.** It takes `inner` and `outer`, computes `w = |outer.x − inner.x|`,
and derives every vertical extent from `w` — upper lid at `0.45w`, lower at `0.25w`.

So the aperture is hard-coded as a fixed ratio of eye width, and the field that would express it is
dead. This is exactly the gap `polson://sdk/core/Drawing` already admits: *"`eyesOpening` is
deliberately absent — it needs `drawComicEye` to take a lid aperture, which it does not yet."*

**One renderer change unlocks the whole eye half of the Action Unit set**, because the morph is
generic: let `drawComicEye` honour `eye.height`, and AU5 (upper lid raiser), AU7 (lid tightener) and
AU43 (eye closure) become ordinary blends of a number that already exists.

---

## 3. The mechanism

One primitive, three head arguments:

```
result = base + amount × (to − from)
```

Every case in §1 is that expression with different arguments:

| what you want | `base` | `from` | `to` | `amount` |
| :--- | :--- | :--- | :--- | ---: |
| interpolate two faces | `a` | `a` | `b` | `0…1` |
| Brennan caricature | `subject` | `norm` | `subject` | `λ` |
| an Action Unit at weight *w* | `head` | `neutral` | `template` | `w` |
| several Action Units | *fold the above* | | | |

The caricature row is Brennan's formula exactly — `subject + λ(subject − norm)` — where `λ = 0` is
the subject untouched and `λ = 1` is **her own choice of the best caricature** in the Kennedy
sequence (Fig. 6 runs 0 / 50 / **100** / 140 / 160%).

The Action Unit row is FACS's formula exactly, `Pᵢ = Pᵢ⁰ + Σₖ wₖ · Δᵢₖ`, and because each term is
additive several units compose by folding — which is what makes `{ AU4: 0.9, AU7: 0.7 }` mean
something rather than needing a preset per combination.

### 3a. Normalisation is not optional

Brennan normalises before differencing — *"scaled and translated relative to one another by spatially
aligning the pupils"*. The procedural-generation review omits that step, and an implementation
without it would run and be **subtly wrong**: it would amplify differences of size and position
rather than of shape, so morphing a 200px head toward a 180px template would shrink the face and call
it an expression.

Our equivalent is cheaper than hers, because we hold the frame explicitly rather than having to find
it in a photograph. Every value goes to head-relative terms before differencing and back to `base`'s
terms after:

- a **point** becomes `(p − origin) / H`
- a **length** becomes `s / H`
- a **coordinate** becomes `(v − origin.axis) / H`

`unit` and `origin` are metadata: copied from `base`, never blended. The schema in §2a is what says
which of the three kinds a given key is — which is the reason to enumerate it rather than infer it
from the name.

### 3b. Yaw must match, and is recoverable

A morph between a frontal head and a three-quarter head interpolates through a projection
corresponding to no viewing angle, and reads as a face melting rather than turning.
`createHeadGeometry` already recovers yaw from `farEye.width / unit.eyeW` (clamped at 0.45), so the
check is available: **refuse a blend whose heads disagree on yaw** beyond a small tolerance, naming
both.

Pitch is not separately recoverable from the dictionary. That is the caller's responsibility and
should be documented as such rather than silently assumed.

---

## 4. Proposed surface

### `Drawing.blendHead(base, from, to, amount)` → `head`

The primitive. Returns a **new deep-cloned head**; none of the three arguments is modified. Refuses,
by name: a key present in one head and absent in another; a yaw mismatch, naming both angles; a
non-finite `amount`.

### `Drawing.exaggerateHead(head, amount, reference?)` → `head`

`blendHead(head, reference, head, amount)`, where `reference` defaults to the zero-parameter canon
built at the same geometry — which the toolkit can construct itself from `unit.H`, `origin` and the
recovered yaw, so the common call takes one argument.

`amount` is Brennan's λ directly: `0` leaves the head alone, `1.0` is her chosen caricature strength,
and negative values move the face toward and past the reference. Grose's bound, which she quotes,
belongs in the docstring as the guidance it is: **a modest deviation causes laughter, a great one
incites horror.**

### `Drawing.applyActionUnits(head, weights, options?)` → `head`

`weights` is `{ AU1: 0.7, AU4: 0.5, AU15: 0.8 }`. Each unit names a stored **delta** — a
head-relative displacement field — folded in at its weight. An unknown unit is refused by name,
exactly as `createParametricHead` refuses an unknown parameter.

### `Drawing.applyFacialExpression(head, name, intensity)` — kept, reimplemented

Becomes a wrapper: each of the six names maps to an Action Unit weight tuple, scaled by `intensity`.
The presets currently move one or two landmarks and would move a coherent set, so this is a
**breaking change to output** while remaining source-compatible.

### `drawComicEye` honours `eye.height`

The one renderer change. `height` becomes the lid aperture in place of the hard-coded `0.45w`, with
the existing ratio as the fallback when the field is absent — so every current script renders
identically.

---

## 5. What this deliberately does not do

**No `createProceduralFace`.** The review proposes one call taking identity, expression, skin tier,
light source and render style together. That collapses the model/geometry/draw split the SDK holds
everywhere — `createMannequinFigure` against `createFigureGeometry`, `createLoomisHead` against
`createHeadGeometry` — and that split is what lets a loop measure twenty heads without allocating a
path, and what lets a caller clip, subtract and relight what it gets back. Everything above returns a
**head**, which every existing call already accepts.

**No statistical shape model.** The PDM lineage the review cites is, once its training step is
dropped, linear displacement from a mean — which is what this is. The eigenvector framing would gain
us nothing and would owe a citation to work we do not hold.

**No feature selection.** Brennan's system makes **no qualitative decision** about which feature is
distinctive; all spatial relationships are exaggerated in parallel, and *"a relationship … becomes a
'feature' only when it differs significantly from the corresponding relationship on a comparison
face."* That is precisely the part that would otherwise need a trained model, and declining it is
what makes this implementable in an afternoon.

---

## 6. Two risks, both real

**The silhouette can break.** Brennan deliberately leaves lines unconstrained — at high exaggeration
*"an eye is free to float above an eyebrow"* — and kept it because users enjoyed finding the limit.
Our `createHeadGeometry` unions cranium, jaw, ear and neck into one contour, so the same freedom
produces a **broken silhouette** rather than a style. Either `exaggerateHead` clamps, or
`createHeadGeometry` reports it. Open question 3.

**One norm is our simplification, not Brennan's finding.** She reports the opposite: her results
*"throw into question the idea that there need be only one strong norm for all human faces"*, and
frequently the best caricature came from comparing against **any face that seems very different**.
A default reference is a convenience we are choosing, and the docstring should say so rather than
imply she endorsed it — the `reference` parameter exists precisely so a caller can disagree.

---

## 7. Open questions for the director

1. ~~**Do the six presets keep their behaviour, or only their names?**~~ **Settled: names and
   signature kept, behaviour replaced, and the tuple exposed** — options B and C together, which cost
   nothing extra because the preset call becomes a one-line wrapper over the tuple.

   **The evidence that decided it.** Only two runs on disk use the call and only one literal
   expression appears anywhere: `'sadness'`, in a script whose own comment reads *"'sadness' at 0.22
   for the inner-brow lift only. Above ~0.3 at this scale it becomes a grimace."* **`sadness` did not
   touch the brow** — it moved the mouth corners and nothing else, where this manual described it as
   doing both. So the run reasoned correctly from a correct manual, asked for one muscle, got
   another, and documented a threshold for an effect it never received; at 0.22 on a 240px head, what
   it *did* get was about two and a half pixels.

   The presets were not merely thin. They were **misleading**, and nothing compared the name to the
   displacement. That check is now a test.
2. ~~**Where do the Action Unit numbers come from?**~~ **Settled: Loomis for the mechanics, Ekman &
   Friesen for the numbering, the studio's own tuning for the magnitudes — stated in that form in
   Manual 08 §4 and in the reference.** The primary Ekman paper was acquired and confirms the
   premise: it contains *"anger"*, *"happy"* and *"surprise"* **zero times**, so the emotion-to-unit
   weights circulating as FACS are not attributable to it. It also corrected us — that paper scores
   **presence**, slight against strong, not the A–E scale we had claimed, which is a later revision.
3. ~~**Clamp λ, or let it break?**~~ **Settled: report, do not clamp — `Drawing.verifyHeadOrdering`.**
   The decision was made by measurement rather than by preference. How far a head can be pushed
   before its stations cross varies **more than thirty-fold**: the canon never breaks, a mild
   character holds past **12.65**, the documented `MORT` to **5.15**, and one carrying
   `noseLength: 1` breaks at **0.40** — *below* Brennan's recommended 1. A constant clamp low enough
   for the last would cap the first at a twelfth of its range and still not protect it at the
   recommended setting.

   **The finding underneath is not about λ at all.** `noseLength: 1` alone breaks at exactly the same
   0.40 as all six parameters at maximum, because `StretchNose` moves `noseBase` by
   `noseLength × H × 0.07` — about 17px on a 240px head against a canon nose-to-mouth gap of roughly
   27. So the parameter layer's documented range and the exaggeration layer **do not compose**, and
   clamping λ would have treated the symptom. That is question 5.

4. **Should `noseLength`'s full range leave room to exaggerate?** Its maximum currently spends most
   of the distance it has, which is why it is the binding constraint on every caricature. Reducing
   the constant would make the two layers compose — and would change what every existing character
   carrying a `noseLength` renders as. Deferred deliberately; nothing is broken, one combination is
   simply tighter than it reads.

   **Still open — and the two chin parameters added since are the evidence for how it should be
   answered.** `chinShape` and `chinLength` were both sized against the gap they spend rather than
   against how large they looked: `chinLength`'s full scale is `0.05 H` against a mouth-to-chin gap
   of `0.19 H`, about a quarter, where `noseLength` spends roughly three quarters of its own. The
   result is measurable — `chinLength: -1` holds to λ **2.85** and `chinLength: +1` never breaks at
   all, against the nose's **0.40**. So the answer to this question is now known to be *yes, and a
   quarter of the gap is enough to read at panel size*; what remains is only whether to spend the
   redraw on the characters already carrying a `noseLength`.
5. ~~**How many Action Units?**~~ **Settled: seven** — AU1, AU4, AU5, AU7, AU12, AU15, AU26. The set
   is bounded by what the construction carries landmarks for rather than by the coding system, and
   the four notable absences each have a reason recorded beside them. The sharpest is **AU2**: the
   head has one `brow` point, so an outer arch and an inner lift would be one displacement under two
   names — and the difference between them *is* the difference between surprise and worry. Giving
   the brow inner and outer stations is the obvious next increment, and it is a change to
   `createLoomisHead` rather than to this layer.

---

## 8. Order and cost

| | change | status |
| ---: | :--- | :--- |
| 1 | `drawComicEye` honours `eye.height`, default unchanged | **done** — as a ratio against `width`, so the canon's 0.45 reproduces the old constants exactly |
| 2 | the head schema, as data | **done** — `HeadSchema`, 38 paths |
| 3 | `blendHead`, with normalisation and refusals | **done** — three refusals, each tested |
| 4 | `exaggerateHead` | **done** — default reference rebuilt from the head's own frame |
| 5 | `eyesOpening` on `createParametricHead` | **done** — the aperture parameter step 1 made reachable |
| 6 | Action Unit deltas and `applyActionUnits` | **done** — seven units, Loomis for the mechanics and Ekman & Friesen for the numbering |
| 7 | reimplement `applyFacialExpression` | **done** — B and C together: `expressionUnits` exposes the tuple, the preset call wraps it |
| 8 | `chinShape`, then `chinLength` | **done** — the jaw becomes three axes: how wide, how pointed, how long |

> **The parameter count is deliberately not stated in this table any more.** It was written as "six
> parameters now" at step 5 and was wrong twice within the week. The count is discoverable from the
> refusal message `createParametricHead` already produces, and `TestEveryParameterAtZeroIsAlsoTheCanon`
> now reads it from there rather than carrying a hand-kept copy — a list whose whole job is to be
> exhaustive cannot afford to be remembered.

Steps 1–5 were additive and broke nothing: the full suite passed unchanged, which for step 1 is the
assertion that matters — the aperture change is invisible to every script that does not use it.

**One thing found in implementation and worth recording.** `blendHead(a, a, a, k)` must be a no-op
for *any* `k`, because `to − from` is zero — and it is the only case that detects an **asymmetric**
normalisation, where the route out to head-relative terms and back is not its own inverse. A drift
there would be invisible in every other test, because a small drift is indistinguishable from a
blend. It is pinned at `k` of 0, 1, −3 and 40.

Documentation landed with the code: `Polson.core.md` for all three calls and the corrected
`eyesOpening` note, and **Manual 23 §6a** for the craft — which is where `createParametricHead`
already lives, so the caricature dial reads as the continuation of character consistency that it is.

**Documentation owed**: `Polson.core.md` for each new call; Manual 08 §4 rewritten around the Action
Unit layer, which completes an argument it already starts; and a Manual 08 source line for Brennan.
