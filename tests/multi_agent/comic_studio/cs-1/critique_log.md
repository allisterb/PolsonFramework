# critique_log — cs-1

The collaboration trace. What each agent saw when it looked, what drift it found, what it changed.

Brief: a **pencil drawing** of `reference_images/panel1.jpg`. Penciler and Critic stages only.

The previous run of this directory is archived at `archive/run-2026-08-29a/`; nothing in it was
read before this run's Penciler began measuring, so the findings here are this run's own.

---

## Stage 1 — Penciler

### What I saw when I looked at the reference

620 x 336, a still from a cel-animated film. Two rabbits, head and shoulders, against a pale
wash. A near-black tree trunk down the left third; dry stems crossing the middle; grass along the
bottom and a dense tuft bottom-left.

The left rabbit is **lower, further back, and in near profile facing right**. The right rabbit is
**higher, nearer, and turned toward the viewer** — and it has **two eyes visible**, the far one
clipped by its own head contour at x 362. Both animals stare off to the viewer-left; neither looks
at the other. The right rabbit's mouth is **open, with teeth showing**. Both of its ears are
**cropped by the top edge** — they do not terminate in frame, and an agent working from memory will
invent tips for them.

### What I measured, and how

Nothing here was eyeballed except where it says so. `Skia.Image.load` + `ctx.getImageData` gave me
the pixel buffer, and I worked it four ways:

- A 10px-cell **luminance Notan** to find the masses at all.
- A **material classification map** — near-black / warm fur / pale ochre / green / wash — calibrated
  against ten named probe pixels whose RGB I printed, so the thresholds are checkable rather than
  asserted.
- **Run-length scans** per row and per column for every silhouette, ear cross-section and contour.
- **6x zoom crops with a 10px grid** for the four feature clusters, after threshold scans saturated
  on the eyes (the ink outlines are as near-black as the pupils, so a bbox of "dark" returns the
  whole window).

Then `Skia.ColorFilter.highContrast(true,'none',0.9)` over the whole panel, which turns it into a
Notan for free and was the single most useful measurement act of the stage — the dry stems, the
grass clumps and the leaf chain all became legible as shapes at once.

Three things fell out that I would not have got by looking:

1. **The right rabbit's cranium is very close to a circle**, centre (430,132) r 70: measured
   half-widths run 45, 52, 58, 62, 66, 67, 63, 61 down rows 64→192, and the centre holds at
   428 ± 2 until y = 176, where it starts drifting left as the muzzle and neck take over.
2. **The fine near-horizontal lines crossing the middle of the panel are whiskers, not stems.** The
   right rabbit's whiskers reach from its muzzle at (382,186) left as far as x 244. I nearly drew
   them as background twigs.
3. **The warm-fur mask fragments the right rabbit** — its darkest fur has r−b ≈ 52, the same warmth
   as the trunk. Fur/trunk separation works on the *left* rabbit and fails on the right, which is
   why the silhouettes came from a figure/ground mask instead.

### Which armature the panel is built on

Measured, not assumed: mean distance from five focal landmarks (three pupils, two muzzles) to the
nearest power point of each armature, at canvas scale.

| armature | power points | mean distance |
| :--- | ---: | ---: |
| `ruleOfThirds` | 4 | **80.8 px** |
| `goldenRatio` | 2 | 168.7 px |
| `dynamicSymmetry` | 3 | 174.6 px |
| `triangle` | 2 | 209.6 px |

`ruleOfThirds` wins, and three of the five landmarks sit within 60px of a power point. I will not
overstate it: the armatures return **different numbers of power points**, so "distance to the
nearest" quietly favours the one with four of them. The result is still the right call — the two
eye clusters land near `topRight` and `bottomLeft` — but it is a biased metric and worth saying so.

The panel is really built on three axes I measured directly: the eye-to-eye diagonal at **−19.3°**,
the left rabbit's far-ear long axis at **+65.8°**, and the right rabbit's chest edge at **+128.5°**.

### Pass 1 → 2: what I saw when I looked at my own render

Rendered, then overlaid on the reference in `multiply` — the parchment ground drops out and only the
lines survive, which separates "in the wrong place" from "drawn badly". They need different fixes,
and this sheet was almost entirely the second kind. **Positions were good; the drawing was not.**

Ten defects, located:

1. **The far ear is too narrow and floats free.** Ellipse rx 38 gives a right edge at x 90 where the
   reference has 107 at the same height; and it is drawn as a closed ellipse with no connection to
   the head it hangs off.
2. **The left near ear's base does not close onto anything.** Base line runs (121,176)→(157,179);
   the back contour starts at (84,200). The gap between them is open.
3. **The left rabbit has no continuous silhouette** — crown, jaw and back are three separate chains,
   and the far-ear ellipse crosses all of them.
4. **Both of the right rabbit's ears are closed off with a straight horizontal line** at y 59 and
   y 53. They read as cut tubes; in the reference they flow into the skull.
5. **The teeth are quadrilaterals** and read as boxes at both mouths.
6. **The grass is three symmetrical starbursts** radiating from single points. Nothing in the
   reference is symmetrical or radiates from a point.
7. **The leaves are a bead chain** — six identical ellipses at even spacing along one line. The
   reference has irregular pointed leaves at varying angles and sizes.
8. **The whiskers are too long, too many and too even**, and read as construction rays across the
   background rather than as whiskers.
9. **The trunk reads as two thin lines**, not a mass — and it is the heaviest shape in the panel.
10. **`Drawing.drawCompositionGrid` injects pink power-point dots**, putting colour on a sheet whose
    whole constraint is blue and graphite only. Recorded as a finding; fixed by drawing the thirds
    myself.

The three `verifyPlumbAlignment` DRIFT results are **not** defects and I am not treating them as
such: the trunk genuinely leans 16px over 186, and the right rabbit's cranium genuinely sits 32px
right of its muzzle because the head is turned. Those are the measurements, and reporting them as
failures is the tolerance being wrong, not the drawing.

### Pass 2 → 3: a fix that broke a mass

Pass 2 closed all ten. Then it broke something pass 1 had right. To seat the far ear on the head I
sliced its ellipse to an arc — `fe.slice(4,46)` — and drew the inner ridge from `fe[10]`. Both index
choices are arbitrary against a rotated ellipse: the arc left the lobe visibly open, and the ridge
started at a point on the outer contour and ran a line straight across the ear. **The lesson is that
the far ear is a closed lobe in the reference** — its base is hidden not because the contour stops
but because the head is drawn in front of it. Pass 3 draws it closed and draws the head over it,
which is both simpler and correct.

### Pass 3 → 4: measurement over eye

The pass-3 overlay put both eye clusters, both crania, the right chest edge, the crown and jaw
chains, the trunk edges and the grass on the reference. One mass was still wrong: the far ear at
`rx 46` reached x 97 where the reference reaches x 110 at the same height. Widened to
`rx 52, c(58,134)` — computed chord 109.5 against a measured 110.

The right rabbit's ears *look* about 5px too far left in the overlay. I did not move them. The row
scans give 397–445 and 459–525 at y = 0 and hold across three consecutive rows, and a multiply
overlay of a 1.6px line over a soft cel edge is not a 5px instrument. Recorded rather than nudged.

### Where the handover lives

- **`artifacts/stage1_penciler.webp`** — the sheet.
- The final script in `scripts/`, whose `ANCHORS` object is the whole handover; every value is
  marked MEASURED or FITTED.
- `scratch/` holds the measurement renders: the 2x reference grid, the mask visualisation, the two
  feature zoom sheets, the `highContrast` Notan, and the two multiply overlays.

### What I could not resolve

- **The back of the left rabbit's skull is never visible** — hidden behind its own far ear, its own
  near ear and the trunk at once. `L.cranium c(172,232) r62` is fitted from the crown, the eye line
  and the back contour, and is the one primary volume that could be wrong by 15–20px.
- **The trunk below y ≈ 190** dissolves into grass and the left rabbit's shoulder. I stopped the
  construction there rather than invent it.
- **Fur/trunk separation fails on the right rabbit** (equal warmth), so its silhouette rests on a
  figure/ground threshold rather than a material mask. Where it passes near the stems at x ≈ 500 the
  edge is soft and my contour there is the weakest on the sheet.

---

## Stage 2 — Critic

The role spec audits `artifacts/stage3_inker.webp`. **There is no inker in this run** — the brief
asks for a pencil drawing and names the Penciler and Critic only — so the audit target is
`artifacts/stage1_penciler.webp`, and the refinement job is different in kind from the one the spec
describes: it is not "fix the inking", it is "resolve a construction sheet into the finished pencil
drawing the brief actually asked for". Recorded as a deviation, not silently substituted.

### Defect list, written before anything was changed

Reference and sheet opened side by side, not recalled.

1. **It is a construction sheet, not a drawing.** Armature, power-point ticks, four volume circles
   and nine coordinate labels are all over the image. The brief asks for a pencil drawing of the
   panel; as a deliverable this fails it outright. *Should be:* construction dropped or reduced to a
   ghost, contours resolved in graphite.
2. **No value structure whatsoever.** The reference is a clean three-value Notan — near-black trunk,
   mid-value fur, pale wash. In line only, the trunk (the darkest mass in the panel) carries exactly
   the same weight as a background stem. *Should be:* trunk heaviest, right rabbit mid-dark, left
   rabbit light, wash untouched.
3. **Both pupils are outlines.** In the reference they are solid near-black — the left one a 45x32
   disc at (190,206), the right a 22x43 leaf at (441,118) — and they are the strongest accent in the
   frame. Drawn hollow, both animals look blind and the gaze direction disappears.
4. **The left rabbit's head interior is empty** between the crown chain and the jaw: nothing but an
   eye outline across roughly 150 x 130 px. The reference has an ear-root shadow, a cheek-to-muzzle
   transition and a muzzle underplane in that area.
5. **The far ear has no interior.** Two concentric outlines and nothing between them. In the
   reference it is a pale ochre lobe with a distinctly darker rim along its lower-right and a dark
   ear-canal wedge at its base near (95,180).
6. **The trunk is two thin lines** where the reference has ~85px of near-black anchoring the entire
   left third of the composition.
7. **The right rabbit's whiskers read as straight rays.** Ends at (276,136) and (266,158) with a
   `lift` of 7 over a 120px span is visually straight; real whiskers bow. They currently look like
   construction lines pointing at the muzzle.
8. **The grass has a hole between x 344 and 452**, where the bottom-centre and bottom-right clusters
   stop short of each other. The reference has continuous ground cover across the bottom edge.
9. **The leaves float off their stem.** Each leaf's root is 4–10px clear of the stem line it is
   supposed to hang from, so the chain reads as debris rather than as foliage.
10. **The left rabbit's teeth sit ~8px high and left.** Drawn top edge y 271; the reference's tooth
    mass starts nearer y 278 and sits under the mouth line rather than across it.

### Refinement pass 1 → `artifacts/stage4_refined_v1.webp`

The structural move was to stop drawing outlines over a shared ground and instead go strictly
back-to-front: **each mass fills itself with the paper colour and is then hatched**, so a nearer mass
occludes a farther one by construction. That replaces the standard even-odd "everything outside this
set of shapes" idiom, which is fragile on self-intersecting contours — and the SDK exposes no
boolean path operation to do it properly.

Crescent shadows come from **clipping twice inside one `save()`**: clip to the mass, then clip to a
quad that covers the shadow side. Clips intersect, so the second one cuts the crescent without any
path arithmetic. That is the single most useful thing I worked out this stage.

Closed on this pass: 1 (construction and labels dropped), 3 (both pupils solid, catchlights punched
back to paper), 6 (trunk hatched in three directions), 8 (a fourth grass cluster fills x 340–470),
9 (leaves now parameterised *along* the stem polyline — `onStem(t)` — so a leaf cannot float off it),
10 (teeth dropped 8px).

**Did not close.** Looking at it: **the value range is compressed between 10% and 25% grey.** There
is no true dark anywhere except the pupils. The trunk — which in the reference is the heaviest mass
in the frame — carries the same weight as the right rabbit's fur, so the Notan the whole composition
rests on is simply absent. Two new defects also appeared: the far ear's inner ridge, drawn as a
concentric ellipse, read as a **second outline** and made the ear look like a plate; and the right
rabbit had **no muzzle division**, so its nose floated on an undifferentiated cheek.

### Refinement pass 2 → `artifacts/stage4_refined_v2.webp`

Value first. Trunk to near-black across four hatch directions plus a core-shadow pass; right rabbit
to a mid-dark with a core shadow down its viewer-right; every ear given an interior. Light reads as
coming from the upper front-left in the reference — both catchlights sit on the upper-left of their
pupils — so every shadow accent goes on the viewer-right of its mass.

The far ear's concentric ellipse was deleted outright and replaced with a three-point inner *edge*.
Deleting was right: the defect was not that the ellipse was misplaced, it was that an ear has one
contour and a crease, not two contours.

Closed: 2, 4, 5, 7. **Did not close:** the right rabbit was still not convincingly darker than the
left, and the two right-hand stems plus the third had closed into a shape that read as a **drawn
triangle** rather than as three stems.

### Sign-off → `output.webp`, `artwork.js`

Three changes over v2: a fourth hatch direction and higher alphas on the right rabbit so the two
animals separate in value the way the reference separates them; the right-hand stem pair shortened
and the third stem detached so the group stops closing; and the trunk region carried 16px lower with
a soft foot pass so it no longer ends on a hard horizontal cut.

No vignette. `Drawing.drawVignette` is offered at this point in the role spec, but the reference is
an evenly-lit cel frame with no framing falloff, and adding one would be a flourish the panel does
not have.

### What converged

Both eye clusters, both crania, the ear cross-sections, the right chest edge, the crown and jaw
chains, the trunk edges and the grass all sit on the reference under a multiply overlay. The value
ladder now matches the reference Notan: near-black trunk, mid-dark right rabbit, light left rabbit,
untouched wash. The gaze reads correctly — both animals looking off to the viewer-left, neither at
the other — and the right rabbit's open mouth and teeth survived every pass.

### What did not, and what another pass would do

- **The right rabbit's ears read as one broad mass with a thin notch.** In the reference they are
  two clearly separate ears with 15px of white sky between them from y 0 to y 50. My gap is
  geometrically there but visually closes because both ears are hatched at the same 82° and the
  contour weight either side of the notch is identical. Next pass: hatch the two ears at different
  angles and lighten the near ear's outer edge.
- **The far ear still reads slightly as a flat disc.** It needs its lower-left rim darker and its
  upper-right lighter to turn as a cone rather than sit as an ellipse.
- **The hatching is one angle per region.** Real pencil rendering follows the form, curving around a
  skull. Straight parallel hatch inside a clip is what the toolkit makes easy; curved hatching would
  need the strokes generated along an offset family of the contour, which is a helper I would write
  if there were a third pass.
- **The right rabbit's skull is a little wide and flat across the top** relative to the reference —
  a consequence of `skullTop` being a five-point chain fitted between two ear bases rather than
  measured, since that part of the head is behind the ears in the reference and never visible.
