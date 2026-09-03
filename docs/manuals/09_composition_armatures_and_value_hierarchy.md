# Studio Manual 09: Compositional Armatures, Value Hierarchy & Visual Emphasis

> **Source Reference**: Andrew Loomis, *Creative Illustration* (Viking Press, 1947) — §1's Informal
> Subdivision from pp. 36–37, §2 from pp. 48–53, §3's key from p. 85; and Andrew Loomis, *The Eye
> of the Painter* (Viking Press, 1961) — §2's fifth device from p. 107, §3's four-value exercise
> from pp. 39–40 and §4's rule from p. 40, §4's variety list from p. 112. **§1's four fixed armature types
> remain unsourced**; treat those as standard convention pending a citation.  
> **Purpose**: Translates visual design theory (classical geometric armatures, focal emphasis rules, Notan value structures, cinematic vignetting, and the 70-20-10 proportional design law) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Classical Geometric Armatures

> **Implemented by**: `Drawing.createCompositionGrid(width, height, type, options)` → `CompositionGrid` with `lines` and `powerPoints`, where `type` is `'ruleOfThirds'`, `'goldenRatio'`, `'dynamicSymmetry'`, or `'triangle'`; render it with `Drawing.drawCompositionGrid(ctx, gridOrType, options)`.

> **Principle**:
> A compositional armature is a scaffold — a few large lines and shapes, usually *implied* rather than
> drawn, that organise where everything else sits. Keep the count low: structural lines are
> individually powerful, and too many of them compete and weaken each other.

> [!NOTE]
> The four `createCompositionGrid` types are standard studio convention — the photographic rule of
> thirds, and dynamic symmetry after Hambidge and Bosanquet. Pending a citation to a specific source.

### Informal Subdivision — a generative armature, not a template

> **Source**: Andrew Loomis, *Creative Illustration* (Viking Press, 1947), p. 36 — "Introducing
> Informal Subdivision", with the worked demonstration on p. 37. Loomis calls it *"a plan of
> subdivision of my own"*, offered because it divides space **unequally and interestingly**.

**Two ways to get an armature, and they suit different work.** The four types above are **templates**:
the same lines on every canvas, fast, shared, and easy to check a placement against — which is exactly
what you want for a logo grid, a repeatable layout, or anything a second person has to follow.
Loomis's method is a **procedure** instead, generating a different scaffold each time, which is what
you want when the composition should not look like every other composition. Neither is more correct;
they fail in opposite directions, one toward sameness and one toward unpredictability.

The procedure:

1. **Divide the whole space with one line**, vertical or horizontal. Deliberately **avoid one-half,
   one-third and one-quarter** — the whole point is an unequal division.
2. **Draw one diagonal of the whole space**, corner to opposite corner.
3. Where that diagonal crosses your first line, **draw a horizontal right across the space**.
4. In any rectangle now produced, **draw one diagonal — never both.** Two crossing as an X would
   halve the rectangle equally, which is exactly what is being avoided.
5. At any intersection, draw a new horizontal or perpendicular. That makes fresh rectangles to
   divide by a single diagonal again.
6. Repeat to taste, then **build the subject onto the structural lines you have created**.

The property that makes it worth the trouble: **no two spaces come out duplicates** — bar the two
halves either side of the first whole-space diagonal. A rule-of-thirds grid gives nine equal cells and
four power points, which is a small, memorable vocabulary; this gives a whole frame of unequal,
non-repeating spaces, each a candidate placement. Fewer decisions against more possibilities.

> [!IMPORTANT]
> **The toolkit has no call for this**, and it is the one compositional method here that is genuinely
> algorithmic — a recursive subdivision with a rule about what *not* to do at each step, which is far
> better suited to code than a fixed template is. `createCompositionGrid` offers four templates and no
> generator. Worth building as a generator alongside them, returning
> the accumulated lines and their intersections as candidate placements. **No such call exists yet;
> do not write one into a script expecting it to resolve.**
>
> The constraint is the interesting part to implement: one diagonal per rectangle, never two, and a
> first cut that avoids the simple fractions. A generator that ignored either would produce a grid of
> duplicate spaces and quietly defeat the method.

### A. Rule of Thirds
Divides canvas into a $3 \times 3$ grid with 4 primary intersection **Power Points**:
- $P_1 = (W/3, H/3)$, $P_2 = (2W/3, H/3)$, $P_3 = (W/3, 2H/3)$, $P_4 = (2W/3, 2H/3)$.
- Place key focal landmarks (e.g. eyes, vanishing points, horizon lines, dominant character masses) along these axes.

### B. Golden Ratio & Golden Spiral ($\Phi \approx 1.618$)
- Divides canvas by the reciprocal golden ratio $\frac{1}{\Phi} \approx 0.618034$.
- Constructs logarithmic logarithmic spiral coils drawing the viewer's gaze toward the golden eye focal node.

### C. Dynamic Symmetry Harmonic Armature (14 Lines)
- Connects the 4 corner diagonals with perpendicular reciprocal diagonals and the central diamond.
- Any line or form aligned with these 14 harmonic angles immediately resonates with classical structural unity.

---

## 2. Visual Emphasis & The Hierarchical Tools

> **Implemented by**: `Drawing.drawLeadingLines(ctx, originPoints, focalPoint, options)` for tool 2 and `Drawing.drawVignette(ctx, width, height, options)` for tool 3. Tools 1 and 4 are decisions about value and spacing — drive them through `Drawing.createNotanPalette(...)` and `Drawing.subdivideProportions(...)`.

> **Source**: Andrew Loomis, *Creative Illustration* (Viking Press, 1947) — "Attention Devices" p. 48,
> "Get Attention by Building Contrast of Line or Shape" p. 49, "Various Types of Vignettes" p. 52 and
> "A Vignette Is a Design Pure and Simple" p. 53.

> **Principle**:
> To guide the viewer's eye through a scene:
>
> 1. **Contrast of Value**: The point of highest local contrast (purest dark next to purest light) receives priority.
> 2. **Leading Lines**: Directional diagonals, architecture edges, or cast shadow rays that converge upon the hero subject.
> 3. **Framing & Vignetting**: Dark peripheral vignetting or foreground silhouettes that lock the viewer inside the visual container.
> 4. **Isolation / Negative Space**: Surrounding the focal hero with clean breathing room to elevate readability.

**Loomis's own taxonomy of attention devices** (p. 48) splits into two kinds, and the split is the
useful part. **Subjects that catch the eye by what they are**: any kind of conflict, anything showing
speed, falling or flight, impending disaster. **Devices that catch it by where they point**:

| Device | What it is, geometrically |
| :--- | :--- |
| Radiating curves to a focal point | a convergent pencil of curves |
| Spot sequence to a focal point | discrete marks in a path, not a line |
| Flame, explosion, radiation-of-light | a burst — rays from one origin |
| Wing or "sweep" motif | one long convergent curve |
| Spider-web motif | radial *and* concentric together |
| Any spiral motif | a single curve that ends at the point |
| Pointer, "bull's-eye" | an object that simply aims |

Everything in the right-hand column is `Drawing.drawLeadingLines(ctx, origins, focal, options)` with
different origin sets — the call is one primitive and this is the list of things to do with it. The
left-hand column is not a drawing technique at all; it is a reminder that **subject matter does this
work before composition gets a chance to**, which is worth knowing before adding a third leading line
to a scene that has no conflict in it.

### A fifth device: concentrate the brightest colour, and neutralise around it

> **Source**: Andrew Loomis, *The Eye of the Painter* (Viking Press, 1961) — p. 107.

Loomis's word for it is **concentration**. Brilliant colour belongs in the area of greatest interest
and should not be scattered: the brightest colour in a picture should be associated with the dominant
figure or object. Not the whole of it — *some portion* of it in high focus is enough. And the second
half of the instruction is the half that does the work: **surrounding colours may be greyed or
neutralised to keep the emphasis where it is wanted.** A vase of flowers commands a room because
nothing else in the room is that colour.

This is the chroma counterpart to tool 1's contrast of value, and it fails the same way — one
saturated accent reads as a focal point, five read as noise. Two constructions implement it:

- **Push the accent up**, by painting the focal passage at full chroma.
- **Pull everything else down**, which is the stronger move and much the easier to apply late:
  desaturate the surround with a colour filter instead of repainting it.

```javascript
// Neutralise the surround; the focal passage keeps its chroma.
const painted = createCanvas(900, 700);
const p = painted.getContext('2d');
p.fillStyle = '#4a6b52';
p.fillRect(0, 0, 900, 700);
p.fillStyle = '#7d5a3c';
p.fillRect(0, 430, 900, 270);
p.fillStyle = '#e03a1f';                        // the accent
p.beginPath();
p.arc(600, 300, 70, 0, Math.PI * 2);
p.fill();

const canvas = createCanvas(900, 700);
const ctx = canvas.getContext('2d');
const source = painted.toBitmap();

// Retain 40% of the chroma; luminance is preserved, so no value moves.
const s = 0.4, lr = 0.213, lg = 0.715, lb = 0.072;
ctx.colorFilter = Skia.ColorFilter.colorMatrix([
    lr + s * (1 - lr), lg - s * lg,       lb - s * lb,       0, 0,
    lr - s * lr,       lg + s * (1 - lg), lb - s * lb,       0, 0,
    lr - s * lr,       lg - s * lg,       lb + s * (1 - lb), 0, 0,
    0,                 0,                 0,                 1, 0
]);
ctx.drawImage(source, 0, 0);                    // the whole scene, neutralised
ctx.colorFilter = null;

const focal = new CanvasPath();
focal.arc(600, 300, 96, 0, Math.PI * 2);
ctx.save();
ctx.clip(focal);
ctx.drawImage(source, 0, 0);                    // the accent, back at full chroma
ctx.restore();

canvas;
```

> [!TIP]
> **`bitmap.palette(...)` measures whether you actually did it.** The accent's `share` is what
> "concentrated, not scattered" means numerically — a focal colour occupying a large share of the
> frame has stopped being an accent. Manual 15 covers the call; Manual 07 §3 carries the warm/cool
> half of the same chapter.

---

## 3. Notan Value Structures

> **Implemented by**: `Drawing.createNotanPalette(type)` → a curated palette for `'binary'`, `'classic3'`, `'highKey'`, or `'lowKey'`. Key names differ per palette (`dominantLight` / `secondaryMid` / `accentDark` for `classic3`; `background` / `formDark` / `formMid` / `rimAccent` for `lowKey`), so read the returned object rather than assuming.

> **Principle**:
> Before adding detailed colour, establish a rock-solid value hierarchy:
>
> - **2-Value Binary Notan**: 50% Black, 50% White. Tests whether the graphic silhouette reads at a glance.
> - **3-Value Classic Notan**:
>   - **70% Dominant** (Light or Midtone)
>   - **20% Secondary** (Opposing Tone)
>   - **10% Accent** (Deepest Dark or Blown Highlight)
> - **High-Key**: Scene dominated by values 1–4 (soft, ethereal, bright).
> - **Low-Key**: Scene dominated by values 7–10 (dramatic, moody, noir, mystery).

> **Source for key**: Loomis, *Creative Illustration*, p. 85 — "The Meaning of Key and Value
> Manipulation". **Key is a move, not a category**: the *same* relationships between light and shadow
> are raised or lowered bodily on the value scale. Held at the top it is **high key**; dropped a tone
> or two, **middle key**; dropped to the bottom, **low key**. The relationships do not change — only
> where on the scale they sit.
>
> Two consequences follow, and the second is the one worth acting on:
>
> - `createNotanPalette('highKey')` and `('lowKey')` are the same structure at two heights, which is
>   why their key names differ but their roles do not.
> - **A high-key scene needs its value distinctions kept small on purpose.** Loomis makes the point
>   that when the values are all at the top of the scale there is a real reason for making the
>   differences between them small — widening them to "get contrast" simply drops the scene out of the
>   key you chose. The counterpart is intentional **forcing of dark against light**, which is a
>   deliberate departure rather than the default.

> [!NOTE]
> *Notan* is the Japanese light-dark convention and is our term rather than Loomis's; the tier
> percentages above are the studio's calibration rather than a cited rule.

> **A second Loomis source puts a number on the tiers.** In *The Eye of the Painter* (pp. 39-40) he sets
> an exercise: small abstract pattern sketches, no larger than three by four inches, in **about four
> values** and nothing else. Two details in it are worth carrying over. First, a "four-value pattern"
> does not mean four separated areas — one value may be cut into as many patches as you like, jump
> over another, be surrounded by another, or be simple where another is broken up. The count is of
> *values*, not of *shapes*, which is what makes it a decision you can hold while drawing anything.
> Second, the background counts as one of the four, and may be the dominant pattern. So
> `Drawing.createNotanPalette('classic3')` plus a background *is* the exercise.

### When two adjacent masses have merged in value

The standard failure a value pass exposes, and the one with a standard answer. A figure and the wall behind it land within a step or two of each other, the silhouette stops reading, and the reflex is to lighten one or darken the other — which changes the scene's value structure to fix a problem that is only at the boundary between the two masses.

**Separate them at the edge instead, with `Skia.MaskFilter.blur(sigma, 'outer')`.** It keeps only the blur and knocks the shape itself out, so atmosphere is lifted *strictly outside* one contour and neither mass's own value moves:

```js
ctx.save();
ctx.maskFilter = Skia.MaskFilter.blur(14, 'outer');   // haze outside the figure only
ctx.fillStyle = 'rgba(210, 226, 238, 0.5)';
ctx.fill(vendorSilhouette);
ctx.restore();
```

It is also honest whenever there is air between the two: three metres of lit rain genuinely do sit between a figure's coat and the facade behind it. Check the result with `bitmap.palette` (Manual 15) rather than by eye — merged masses are exactly the case where the eye is least reliable, since it is the boundary and not the values that has failed.

> [!IMPORTANT]
> **First decide whether the merge is a defect at all.** Loomis (*Creative Illustration* pp. 102–103,
> distilled in Manual 07 §1) treats converged values as an *opportunity*: where two tones are close
> anyway, it is safe to lose the edge further and spend the sharpness where it does more good. Edges
> are lost and found, and a contour that is hard the whole way round reads as cut out.
>
> The test is what the edge is doing for the picture. **A merge on the subject's silhouette against
> its background is a defect** — the reading of the whole image depends on it, so separate them. **A
> merge between two secondary masses is a saving** — a shoulder into shadow, one background plane into
> another — because every sharp edge spends attention and there is only so much to spend. Reach for
> the mask filter when the silhouette has failed, not whenever two values are close.

> The four styles are `'normal'` (softens the whole shape — an airbrush), `'solid'` (crisp shape, blur added outside — a glow), `'outer'` (blur only, shape knocked out — a halo, and the one wanted here), and `'inner'` (blur only, inside — an inward vignette). Full definitions in Manual 17 §7 and `polson://sdk/core/Skia`.

---

## 4. The 70-20-10 Proportional Law ("Big, Medium, Small")

> **Implemented by**: `Drawing.subdivideProportions(bounds, direction, ratios)` → `{ big, medium, small }`, each a `Rect` you can hand straight to `ctx.fillRect(...)` or use as a placement region.

> **Principle**:
> Give the main idea a large shape or space, secondary ideas medium ones, and details that are
> *distinctly* smaller than the medium elements — smaller elements may sit inside larger ones. Varied
> scale is what stops a composition reading as evenly weighted mush. The percentages below are the
> studio's calibration:
> - **70% Big / Dominant**: The major backdrop, environment mass, or sky.
> - **20% Medium / Secondary**: The character figure, vehicle, or architectural structure.
> - **10% Small / Detail**: Micro-flourishes, specular highlights, textures, and facial features.

> **Source**: Andrew Loomis, *The Eye of the Painter* (Viking Press, 1961) — p. 40 for the rule these
> percentages are an instance of, p. 112 for what to vary when repetition is unavoidable.

**The rule behind the ratio is a prohibition, and it is more general than 70-20-10:** *try not to
have any two areas of pattern the same size or shape.* Loomis states it for the abstract pattern
exercise of §3, but it is the reason a three-tier split works at all — 70/20/10 simply guarantees the
prohibition holds for the three largest masses. Any three distinctly unequal shares satisfy it;
70/20/10 is the studio's calibration of *distinctly*.

Two things follow that the ratio alone does not give you:

- **Shape counts as well as size.** Two masses of clearly different area that are the same *shape*
  still fail the rule. `Drawing.subdivideProportions(...)` returns rectangles, so three rectangular
  tiers have satisfied only half of it.
- **Where the subject forces repetition, vary something else.** Loomis's list, from the rhythm
  chapter: size, grouping, colour, value. Rhythms in nature repeat but never in identical shape —
  large waves come with small ones, big stones are strewn with small ones of different shapes. A
  colonnade or a row of windows is the case that tests this, and Manual 06 §4's spacing into depth is
  what keeps such a row from becoming an even beat.

> [!TIP]
> **This one is directly measurable.** `bitmap.palette(4)` returns each dominant colour's `share`;
> two shares within a few points of one another are the failure the rule names, and they show up in
> the numbers before they show up in the picture.

---

## 5. Symbol → SDK Parameter Map

| Concept | SDK parameter or field | Notes |
| --- | --- | --- |
| Rule of thirds | `type: 'ruleOfThirds'` | Power points: `topLeft`, `topRight`, `bottomLeft`, `bottomRight`. |
| Golden ratio armature | `type: 'goldenRatio'` | Power points: `goldenEye`, `secondaryEye`. |
| Dynamic symmetry (14 lines) | `type: 'dynamicSymmetry'` | Power points: `center`, `harmonicTopLeft`, `harmonicTopRight`. |
| Triangular armature | `type: 'triangle'` | Power points: `apex`, `center`. |
| $P_1 \dots P_4$ | `grid.powerPoints.*` | Read the names above — they differ per armature type. |
| Armature lines | `grid.lines` | Array of point pairs, ready to stroke. |
| Leading lines (tool 2) | `Drawing.drawLeadingLines(ctx, origins, focal, opts)` | Origins are the frame edges the eye enters from. |
| Vignette (tool 3) | `Drawing.drawVignette(ctx, w, h, opts)` | `intensity` and `radius` control the falloff. |
| 2-value Notan | `createNotanPalette('binary')` | Keys: `dominant`, `secondary`. |
| 3-value Notan | `createNotanPalette('classic3')` | Keys: `dominantLight`, `secondaryMid`, `accentDark`. |
| High key | `createNotanPalette('highKey')` | Keys: `background`, `formLight`, `formMid`, `darkAccent`. |
| Low key | `createNotanPalette('lowKey')` | Keys: `background`, `formDark`, `formMid`, `rimAccent`. |
| 70 / 20 / 10 | `bands.big`, `.medium`, `.small` | Each a `Rect`: `{ x, y, width, height }`. |

> The palette key names are **not** uniform across types — that is deliberate, because a low-key scene has no "dominant light". Read the object you got back rather than assuming `classic3` keys.

---

## 6. Constructing It: A Runnable Layout

Value hierarchy is decided *before* anything is drawn. Establish the Notan, block the 70-20-10 masses, place the armature, and only then steer the eye.

```javascript
// A composition blocked from value structure outward.
const canvas = createCanvas(960, 600);
const ctx = canvas.getContext('2d');

// §3 — Notan first. Three values, nothing else, before any detail exists.
const notan = Drawing.createNotanPalette('classic3');
ctx.fillStyle = notan.dominantLight;
ctx.fillRect(0, 0, 960, 600);

// §4 — 70% backdrop, 20% subject band, 10% accent.
const bands = Drawing.subdivideProportions({ x: 0, y: 0, width: 960, height: 600 }, 'vertical');
ctx.fillStyle = notan.secondaryMid;
ctx.fillRect(bands.medium.x, bands.medium.y, bands.medium.width, bands.medium.height);
ctx.fillStyle = notan.accentDark;
ctx.fillRect(bands.small.x, bands.small.y, bands.small.width, bands.small.height);

// §1 — The armature and its focal power points.
const grid = Drawing.createCompositionGrid(960, 600, 'ruleOfThirds');
Drawing.drawCompositionGrid(ctx, grid, { opacity: 0.5 });

// §2 — Tool 2: converge the eye on a power point. Tool 3: close the frame.
const focal = grid.powerPoints.topLeft;
Drawing.drawLeadingLines(ctx, [{ x: 0, y: 600 }, { x: 960, y: 600 }, { x: 960, y: 0 }], focal, { opacity: 0.35 });
Drawing.drawVignette(ctx, 960, 600, { intensity: 0.55 });

log('Focal power point: ' + focal.x.toFixed(0) + ', ' + focal.y.toFixed(0));

canvas;
```
