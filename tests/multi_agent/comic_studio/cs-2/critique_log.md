# Critique log — cs-2

The collaboration trace: what each stage saw when it looked, what it found, what it changed.
Written as the run went, not reconstructed afterwards.

Target: `reference_images/comic1.png` — 1024×1024, **transparent** ground, a flying superhero
figure. It is a flat vector illustration in the Material Design palette with **no line art at all**.
That fact drove most of the decisions below.

Scores quoted throughout use two measures, both computed against the reference in-sandbox:

- **coverage error** — fraction of sampled pixels where my colour *family* (skin / cape red /
  suit blue / hair-dark / empty) disagrees with the reference. Catches silhouette and layering errors.
- **mean RGB distance** — average Euclidean RGB distance over pixels painted in both. Catches tone
  errors that coverage is blind to. Max possible is 441.

| Stage | coverage error | mean RGB distance |
| :--- | ---: | ---: |
| 2 — Colorist | 7.92 % | 26.0 |
| 3 — Inker | 7.67 % | 24.3 |
| 4 — Critic, pass 3 (final) | **7.38 %** | **21.3** |

---

## Stage 1 — Penciler

**Looked at:** the reference, five separate measurement passes before drawing a line. An 8px
classification map of the whole figure, a 3px map of the head, run-length scanlines, a colour census,
and a per-column trace of the hair/skin boundary.

**Found:**

- The figure is **4.52 heads tall** crown-to-toe (940px / 208px), not the 8-head canon. A stylised
  proportion, so `Drawing.createMannequinFigure`'s standing canon was used only as a proportion check,
  never as the limb source.
- **The face departs from Loomis measurably.** With crown at y=64 and chin at y=272 the canon puts the
  eye line at y=172; the reference has it at **y=195** — 11 % of head height lower. Nose 218 vs 210,
  mouth 244 vs 237, hairline 126 vs 110. Every feature sits low in the mask. This was written into the
  handoff note explicitly, because a downstream stage that trusted the canonical head object would
  have drawn an adult face on a stylised head and no metric would have caught it.
- `verifyPlumbAlignment(crown, crotch)` reported 60px drift against a 24px tolerance. **Correct and
  expected** — the figure is airborne and leaning; a vertical plumb is the wrong test for this pose.
  Recorded rather than "fixed", because forcing it into tolerance would have straightened the pose.
- The ground is transparent, not white. Easy to miss from a thumbnail and it would have been baked in
  permanently.

**Changed after looking at its own render:** labels overprinted around the head, exactly where the
measurements were densest — a construction sheet the next stage cannot read is a failed handoff, so
they moved to tags plus a legend. And the suit had volumes but no blocked silhouette: the Colorist
would have inherited cylinders and no measured torso edge, and the 232px-shoulder-to-190px-waist taper
is the proportion most likely to drift. Both fixed before handoff.

---

## Stage 2 — Colorist

**Looked at:** the reference, `artifacts/stage1_penciler.webp`, and the Penciler's anchor set.

**Found — the lighting is not a lighting.** Fitting each region's colour as a linear function of
(x, y) recovered the actual gradient axes:

- The suit ramps **vertically**, `#45a7f6` at the shoulders to `#1e87e4` at the waist — key light from
  above.
- The cape does **not** ramp directionally. All four sampled lobes are bright at their *outer* edges
  (275–430px from the torso centre) and dark where they tuck behind the figure (97–163px). That is a
  radial falloff, and it was modelled as one rather than as a key light.
- Skin and hair measured essentially **flat** — 1.8 and 3.7 RGB units per 100px. They were painted
  flat. Adding a shading ramp there would have been my lighting, not the reference's.
- The reference's palette is Material Design (`#42A5F5`, `#FFCA28`, `#FDD835`, `#543930`), recovered by
  census rather than sampled by eye.

**Changed after looking:** the first painted render read "close" by eye, so it was diffed numerically
instead. Six defects the eye had passed:

1. The collar was a solid band across the chest; the reference splits it, and blue shows from x=496 at
   y=288 in a wedge between the shoulder lobe and the front flap.
2. The right shoulder stopped at x=730; the reference carries to x=754 at y=320.
3. The blue upper arm started at y=208, leaving a gap under the gauntlet — the reference has blue by
   y=192. Fixed by making the gauntlet's lower edge and the arm's upper edge *the same points*, so they
   cannot drift apart again.
4. The torso's lower-left followed an invented curve rather than the measured blue boundary, which
   jumps from x=432 at y=464 to x=528 at y=480 where the arm's shadow cuts across.
5. The emblem's inner mark was a curved band; the reference is a clean triangle
   (582,357)–(650,357)–(616,403), verified against four scanlines.
6. No forehead skin at y=128 where the reference has it at 604–626.

---

## Stage 3 — Inker

**Looked at:** the reference, `artifacts/stage2_colorist.webp`, and the Colorist's script.

**Found — a conflict between the brief and the target, resolved deliberately.** The Inker's role spec
asks for a three-tier line-weight hierarchy, tapered strokes and solid blacks. The reference has
**zero outlines**. Drawing ink on it would have moved the work away from the target, so before
inventing strokes I checked how the reference separates adjacent same-hue masses instead. It does it
**tonally**: there is a genuine dark crease between the legs, and a dark seam band along the
cape/torso boundary measuring `#772519`.

So the hierarchy was mapped onto the reference's own vocabulary — primary = deep occlusion seams that
carry contour, secondary = boundary tones between adjacent reds, tertiary = none, because the
reference has none. This is a deliberate departure from the brief, not an omission.

**Changed after looking — and this stage produced a regression, which is worth recording.** The first
attempt painted the occlusion seam as an opaque dark shape. It put a grey smear across the torso and
was clearly worse than what it replaced. Looking at it rather than at the code caught it. The fix was
subtractive: the reference never paints that seam — it is the **cape** showing through a gap between
the arm and the torso, and the cape's own dark core is already `#6d2414` at that radius, which is the
seam colour. Cutting the gap correctly and letting what is behind do the work removed the smear and
restored the contour. The boot seam was removed for the same reason.

The other correction: the bent arm's upper arm is a **separate blue strip** (448–472 at y=440,
narrowing to 424–448 at y=472) that stage 2 had merged into the torso.

---

## Stage 4 — Critic

Three refinement passes, each scored region by region rather than on the overall average.

**Pass 1 — the shader, and an honest negative result.** The cape's tone was the largest remaining
error. A single radial fit it to 24 units; a global quadratic surface fit did no better (23.3), which
is itself informative: no single surface has that shape, because in the original the cape is several
paths each with its own linear gradient. Replacing it with an SkSL shader blending the four
locally-measured planes improved coverage (7.67 → 7.57) but made mean tone error **worse**
(24.3 → 25.2).

Rather than accept it on the coverage number, the two stages were compared region by region. That
split the result cleanly:

- cape left wing 37.7 → 22.7 **better**, cape upper-left 30.0 → 23.0 **better**
- cape mid-right 20.5 → **44.8 much worse** — no plane centre was near that region
- suit torso 24.6 → 30.4 and legs 18.3 → 25.9 **worse** — the shader loses to plain linear gradients there

**Pass 2 — keep it where it earned its place.** Shader kept for the cape only, suit reverted to linear
gradients, and a fifth plane fitted for the uncovered mid-right region. Then the belt was investigated
— at 36.9 it was the worst non-cape region in *both* prior stages and no stage had looked at it. It is
not a linear ramp: it is dark at **both** ends (`#D02E2E` left, `#D33030` right) and `#F44336` across
the middle, a cylindrical highlight. Its base red was also brighter than what was in use.

That reasoning about the belt then made it **worse** (36.9 → 40.7). So the reasoning was discarded and
the belt boundary traced per scanline like every other shape — top edge peaking at (512,474) and
flattening to y=510 across the right half, buckle a rounded diamond centred (584,526).

**Pass 3 — the weight function.** The remaining shader regression (cape lower/tail 26.0 → 29.9) was
the planes bleeding into each other. Sharpening the distance weight from inverse-square to
inverse-fourth fixed it and improved almost everything else:

| region | after Inker | final | verdict |
| :--- | ---: | ---: | :--- |
| cape left wing | 37.7 | **20.4** | better by 17.4 |
| cape mid-right | 20.5 | **8.8** | better by 11.8 |
| cape upper-left | 30.0 | **21.9** | better by 8.1 |
| belt + buckle | 34.8 | **28.8** | better by 6.0 |
| cape far lobe | 18.9 | **16.4** | better |
| suit legs | 18.3 | **17.0** | better |
| cape lower/tail | 26.0 | 26.2 | regression removed |
| gauntlet, boot, head+hair, raised arm | — | — | unchanged |
| suit torso | 24.6 | 26.2 | slightly worse |

**Verified at the end:** `output.webp` is byte-identical (31 074 bytes) to the stage-4 render, which
confirms `artwork.js` is the file that actually made the picture rather than a description of it. The
ground is transparent, matching the reference. Dimensions 1024×1024, 1:1, no rescale.

---

## What is still wrong

Stated plainly, because a QA stage that reports only its wins is not a QA stage.

- **`head+hair` sits at 26.7 and did not improve across any pass.** The error is dominated by boundary
  placement, not tone: the hair/skin edge is a ~230-unit contrast step, so a 2–3px contour offset
  produces large local error. The hair silhouette is smoother than the reference's, which has more
  structure in the upper-left lock.
- **`suit torso` drifted from 24.6 to 26.2** across the Critic passes. Not diagnosed. The box includes
  the emblem edges and the arm gap, so it is probably boundary rather than fill.
- **The bent forearm is approximate.** It is red-on-red against the cape, distinguishable only by tone
  (`#D64134` fist vs `#B02D1B` cape). Its boundary was estimated from the fist and elbow anchors rather
  than traced, because the classifier cannot separate two reds that close.
- **The cape's internal fold structure is modelled, not reproduced.** Blending five measured planes is
  a good approximation of several vector paths with separate gradients, but it is an approximation; a
  faithful reconstruction would need the fold boundaries traced as real edges.
