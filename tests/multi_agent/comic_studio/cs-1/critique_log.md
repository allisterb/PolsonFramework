# critique_log — cs-1

The collaboration trace. What each agent saw when it looked, what drift it found, what it changed.

---

## Stage 1 — Penciler

### What I saw when I looked at the reference

`reference_images/panel1.jpg`, 620 x 336. A still from a cel-animated film: two rabbits, head
and shoulders, against a washed pale ground. A near-black tree trunk down the left third; dry
stems crossing the middle; grass tufts along the bottom and a tall clump bottom-left.

The left rabbit is **lower and further back**, in near-profile facing right, one eye visible. The
right rabbit is **higher, nearer and turned more toward the viewer**, both eyes visible. They do
not look at each other — both pupils sit left of their own socket centres. Both animals are
staring off to the viewer-left. That is the content of the panel, and it is a thing a later stage
can easily destroy by centring a pupil.

### What I measured, and how

I did not eyeball any of it. `Skia.Image.load('reference_images/panel1.jpg')` plus
`bitmap.getPixel(x, y)` gave me a luminance/saturation probe, and I ran threshold scans over it:

- A coarse luminance census (10px grid) to find the masses at all.
- Dark-run scans per row (`L < 135` / `L < 150`) to get both rabbits' silhouettes, the trunk edges,
  and the four ear cross-sections.
- A flood fill under `L < 45` to get each pupil's exact bounding box.
- A brightest-pixel search inside each pupil to locate the catchlights.
- A warm-fur mask (`r - b > 24`, `g < r`) to separate fur from the green grass and the neutral trunk.
- An ochre mask to isolate the left rabbit's big pale far ear from the trunk behind it.
- A `Skia.ColorFilter.highContrast` render of the whole frame, which turned the panel into a Notan
  and made the stems, the grass and the light muzzle masses readable as shapes.

Three things fell out of that which I would not have got by looking:

1. **The right rabbit's cranium is very nearly a circle.** Its silhouette spans x 716..992 at
   y = 237 — centre (854, 237), r = 138 — and the same circle predicts the width at y = 160 and
   y = 300 to within about 20px. That is a measured primary volume, not a guess.
2. **The left rabbit's head is 236 wide against the right one's 276, a ratio of 0.855.** That is
   the only quantified depth cue in the panel and it is what puts the left animal behind.
3. **Both of the right rabbit's ear tips are cropped by the top edge.** They do not terminate in
   frame. An agent working from memory will invent tips.

### Which armature the panel is actually built on

I tested all four with `Drawing.createCompositionGrid` and measured, for each, the distance from
each of the four focal landmarks (two eyes, two noses) to the nearest power point:

| armature | mean distance |
| :--- | ---: |
| `ruleOfThirds` | **104 px** |
| `dynamicSymmetry` | 174 px |
| `goldenRatio` | 181 px |
| `triangle` | 231 px |

`ruleOfThirds` wins, and it is what I drew, but 104px on a 1240-wide canvas is a loose fit and I am
not going to pretend otherwise. The panel is really composed on three axes I measured directly:
the rising eye-to-eye diagonal at -17.5°, the left rabbit's far-ear axis at +56.1°, and the right
rabbit's chest edge at +128.2°. The latter two converge at (433, 727) — just below the bottom edge
— which makes the composition a V funnel with both heads sitting on its rising arm, bookended by
the vertical trunk on the left and the vertical ear pair on the right.

### The Loomis decision

I ran `Drawing.createLoomisHead(854, 237, 276, 25, 0)` against my measured right-hand skull before
deciding. Its **vertical** ratios fit a rabbit surprisingly well — it puts the crown at y = 99
against a measured 105, and the eye line at y = 242.5 against a measured 235. Its **lateral** ones
do not: it places the two eyes 68px apart where the measured rabbit pair is 147px, a 2.16x error,
because a rabbit carries its eyes on the sides of the skull rather than on the front. It also
posits a chin, a nose base and a jaw angle that a rabbit has not got.

So the cranial construction is hand-rolled: one circle per skull plus a separate muzzle lobe. This
is recorded as a finding, not a complaint — the toolkit's head is a human head and says so.

### What I saw when I looked at my own render, pass by pass

**Pass 1 → 2.** Every secondary mass floated. The left rabbit's near ear had no base seated on the
crown, the far ear's lower end stopped in mid-air, the head silhouette was open between the neck
and the crown, and neither rabbit had a muzzle contour at all — just a dot labelled `noseTip`.
The blue stems ran straight across both faces because nothing held them behind the figures.
Overlaying the sheet on the reference with `globalCompositeOperation = 'multiply'` — which lets the
parchment ground pass through and keeps only the lines — showed the *positions* were largely right;
what was wrong was connectivity. Fixed: closed every silhouette onto the mass it hangs off, added
both muzzle lobes, and held the background back.

**Pass 2 → 3.** The stems were being destroyed by my own smoothing helper: a quadratic-through-
midpoints curve turns a 6-point polyline into a soft S, and a dry stem is straight. Switched them
to plain segments. The left rabbit had grown a long "cheek line" from (266,450) to (378,504) that
exists nowhere in the reference and read as a bag under the eye — deleted. The right rabbit's
muzzle had been stretched out to (940,404), which is the jaw, not the muzzle — pulled back to
(904,378). The right rabbit's head-left contour had a concave dent at (718,245) that came from a
single noisy scan row — smoothed to (724,248).

**Pass 3 → 4.** The trunk's solid edges were vanishing. I had been holding the background back with
`ctx.clip(path, 'evenodd')` over a rect plus both figure outlines; the technique is correct
(I verified it in isolation) but my figure polygons self-intersect around the ear/skull junctions,
and even-odd on a self-intersecting contour flips regions back to "inside" unpredictably. Replaced
with a deterministic knock-back: draw the background in full, then fill the two figure regions with
the parchment ground under `'nonzero'`. Same result, no dependence on winding.

**Pass 4 → final.** Added ear inner ridges for the Inker, moved the labels off the eye line, and
re-checked the overlay: cranial circles, both eye pupils, both catchlights, both nose tips, all
four ear edges and both head contours now sit on the reference.

### Where the handover lives

- **`scripts/0028.js`** — the final, self-contained construction script. Its `ANCHORS` object is
  the whole handover; every value is either marked MEASURED or FITTED.
- **`artifacts/stage1_penciler.webp`** — the sheet.
- **`Stage.note`** in `events/` — the same anchors in prose, for anyone who cannot run the script.
- `scratch/` holds the measurement renders: the 2x reference grid, the two head zooms, the ear
  zooms, the `highContrast` Notan, and the two multiply overlays used to check drift.

`Session.ANCHORS` is also set, but do not rely on it — it does not survive a new MCP session.

### What I could not resolve

- **The back of the left rabbit's skull is never visible.** It is hidden behind its own far ear,
  its own near ear and the tree trunk simultaneously. `L.craniumC (355,445) r118` is fitted from
  the crown and the eye, not measured, and it is the one primary volume in the sheet that could be
  wrong by 15-20px.
- **The left rabbit's near ear against the trunk.** For y < 80 the ear is silhouetted against
  near-black trunk, so no luminance threshold separates them. The ear tip at (285,76) comes from a
  saturation mask (brown ear against neutral trunk) and is the weakest anchor on the sheet.
- **The lower trunk.** Below y ≈ 470 it dissolves into grass and the left rabbit's shoulder. I
  stopped the construction there rather than invent it.
- **Whiskers, leaves and grass blades are blocked, not measured.** Directions and clump positions
  come from the Notan render; individual strokes are mine.
