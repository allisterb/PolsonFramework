# Studio Manual 03: Comic Inking & Feathering

> **Source Reference**: Klaus Janson, *The DC Comics Guide to Inking Comics* (Watson-Guptill / DC
> Comics, 2003) — §1 from ch. 6 (p. 80), §2 from ch. 7 (p. 88), §4 from ch. 9 (pp. 110–113), §5 from
> ch. 7 (pp. 90, 96–98). Pixel widths are the studio's calibration; no book gives pixels.
> **Purpose**: Provides line weight hierarchy standards, tapered stroke algorithms, directional cross-hatching recipes, and graphic black ink placement rules for authentic comic book illustration.

---

## 1. Line Weight Hierarchy (The Three Inking Tiers)

> **Implemented by**: the `maxThickness` argument of `Drawing.drawTaperedStroke(...)`, or the `width`
> argument of `Skia.Brush.ink(color, width)` applied with `ctx.useBrush(...)`. Pick the tier here,
> then pass it; the tier *is* the parameter.

> **Source**: Klaus Janson, *The DC Comics Guide to Inking Comics*, ch. 6 (p. 80) — and the rule
> behind the whole hierarchy is one sentence: **it is a general rule of art that the contour of a
> figure should stand out from the background, and one way to accomplish that is a heavy line on the
> contour of the body.** Tier 1 exists to separate the figure from what is behind it; the other two
> exist to not compete with it.

Comic inking derives its punch and depth from **stroke weight contrast**:

| Tier | Line Width | Purpose | Applied To |
|---|---|---|---|
| **Tier 1 (Outer Silhouette)** | **3.5px – 5.0px** | Defines the outer spatial boundary; grounds the figure against the background | Outer jawline, coat collar, outer hair mass contour, bandana underside |
| **Tier 2 (Internal Contours)** | **2.0px – 3.0px** | Defines structural forms and major overlapping planes | Eyelid creases, nose bridge, lip slit, hair lock boundaries, collar seams |
| **Tier 3 (Details & Hatching)** | **0.8px – 1.5px** | Adds volume, surface texture, and shadow transition | Eyelashes, iris fibers, neck tendon hatching, shirt fold wrinkles, rigging cord texture |

> [!IMPORTANT]
> **The widths are the studio's; the hierarchy is Janson's.** No book gives pixel values, and these
> are calibrated for a panel around 1000 px wide — scale them with the artwork rather than treating
> them as constants.
>
> What is his is the *reason* for three tiers rather than one, and he states the failure at each end:
>
> - **Too little variety** and "the shapes in the panel become one gray mass — or mess" (p. 74).
> - **Weight that varies without meaning is worse than none.** On a badly inked sweater neck he asks
>   why the top carries a heavier line than the bottom: *it serves no purpose and does not give the
>   reader any useful information.* **Think before you ink.**
>
> So a tier is not a decoration to distribute evenly. Every change of weight is a claim about the
> subject, and a weight change that claims nothing is a defect you can see.

> [!TIP]
> **Two composition rules from the same chapter that the toolkit cannot check for you** (p. 82), both
> of which cost a panel its depth:
>
> - **Never allow a tangent** — two contours that just touch, rather than clearly overlapping or
>   clearly separating. Janson extends it to lettering: never let a balloon or caption rub against a
>   line inside the panel. **Always overlap.**
> - **Overlap is what creates depth.** His example is a spear crossing the edge of a cliff: drawn
>   overlapping the edge it puts the spear in front, drawn inside the cliff's shape the illusion
>   disappears. Two figures in close proximity get the same treatment.

---

## 2. Inking Rules of Thumb

> **Implemented by**: `Drawing.drawTaperedStroke(...)` for weighted contours and `Drawing.drawFeathering(...)` for shadow transitions. These are judgement rules — the calls execute them, they do not decide them for you.

1. **The Light Rule** — it is light, not gravity, that sets the weight:
   - Lines facing the **light source** should be **thin or broken** ($1.0\text{px} - 1.5\text{px}$).
   - Lines **away from the light** (underside of jaw, bottom of hair curls, coat hem) should be
     **heavy and thick** ($3.5\text{px} - 5.0\text{px}$).
2. **Line Tapering**:
   - Comic lines never end in flat, blunt cylindrical cutoffs. Every line starts at a point, swells in the middle, and tapers to a fine point.

> **Source**: Klaus Janson, *The DC Comics Guide to Inking Comics*, ch. 7 (p. 88) — "The Light Source".

> [!IMPORTANT]
> **A heavy ink line is really the beginning of a shadow.** That one sentence is the whole rule.
> The underside of a jaw is heavy because it is *in shadow*, not because of gravity — the two agree
> for a figure lit from above and part company the moment the light comes from below, where a gravity
> rule keeps weighting the underside and the light rule correctly moves the weight to the top.
>
> Janson's example is a man in a room lit from the ceiling: a heavier line than the one on the top of
> his head represents the bottom of his nose and chin. And the light's *distance* is a second control
> — **the farther the light, the subtler the line weight; directly overhead and close, the more
> extreme the facial shadows.**
>
> **If the pencils do not state a light source, the inker decides one before inking**, and whatever
> is decided should be logical. Even an unspectacular scene has a light source of some kind.

> [!NOTE]
> **The medium is inverted relative to nature, which is worth holding onto when translating a lit
> render into line.** Nature is black, and light is imposed on it to extract form. An artist works the
> other way — **introducing darkness onto white**, where the paper is the light and the ink is the
> dark. A render from Manual 07 gives you a lit surface; inking it means deciding which darks to
> *add*, not which lights to keep.

---

## 3. Algorithmic Tapered Inking in Canvas2D

> **Implemented by**: `Drawing.drawTaperedStroke(ctx, start, cp1, cp2, end, maxThickness, fillOrStrokeStyle)`.

`Drawing.drawTaperedStroke(ctx, start, cp1, cp2, end, maxThickness, fillOrStrokeStyle)` samples the cubic Bézier and varies width along its length, so the stroke swells through the middle and closes at both ends.

- Points may be `{ x, y }` objects, or pass ten flat numbers (`sx, sy, cp1x, cp1y, cp2x, cp2y, ex, ey, maxThickness, style`).
- `fillOrStrokeStyle` accepts a colour string or an `SKShader`.

Choose `maxThickness` from the tier table in §1 — contour silhouette at the heavy end, interior detail at the light end. A constant-width stroke reads as a technical drawing, not as inking.

### The mark is a shape, and it is handed back

`drawTaperedStroke` **returns the path it filled**, and `Drawing.createTaperedStrokePath(start, cp1,
cp2, end, maxThickness)` builds the same envelope with no context to paint it on. That is the
difference between a mark that is finished and a mark you can still work on:

```javascript
const canvas = createCanvas(560, 320);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f1e8'; ctx.fillRect(0, 0, 560, 320);

const a = { x: 60, y: 250 }, c1 = { x: 90, y: 70 }, c2 = { x: 420, y: 60 }, b = { x: 500, y: 230 };

// Painted and held. A gradient across the stroke rather than along the path — which a stroke with a
// width cannot do at all, because it has no interior to run a ramp through.
const across = ctx.createLinearGradient(60, 60, 500, 250);
across.addColorStop(0, '#1b3a4b'); across.addColorStop(0.55, '#c2553d'); across.addColorStop(1, '#e6c46a');
const mark = ctx.drawTaperedStroke(a, c1, c2, b, 26, across);

// Now cut it. The bite is real geometry taken out of the mark, not a shape laid on top — so it
// survives a knockout and exports as one path.
const bite = new CanvasPath();
bite.arc(300, 96, 44, 0, Math.PI * 2);
ctx.fillStyle = '#15151a';
ctx.fill(mark.subtract(bite));

// Or build a run of marks first and ink the silhouette once — which is how a set of feathering
// strokes becomes one shape with one contour rather than twelve separate lines.
let run = null;
for (let i = 0; i < 5; i++) {
    const o = i * 12 - 24;
    const q = Drawing.createTaperedStrokePath(
        { x: a.x + o, y: a.y }, { x: c1.x + o, y: c1.y },
        { x: c2.x + o, y: c2.y }, { x: b.x + o, y: b.y }, 9);
    run = run === null ? q : run.union(q);
}
ctx.strokeStyle = '#15151a'; ctx.lineWidth = 2;
ctx.stroke(run.simplify());
canvas;
```

> [!NOTE]
> A script that ignores the return value behaves exactly as it did before. One change worth knowing:
> the call no longer leaves the tapered outline as the context's **current** path, so a `beginPath()`
> you built before calling it survives.

### When the line is not a single cubic

`drawTaperedStroke` tapers one Bézier. For a contour built from many segments, or one whose width
must vary by something other than position along the curve, take the stroke's **outline** and modify
that instead:

```js
ctx.lineWidth = 9;
const mark = ctx.strokeToPath(contour);   // the stroke, now a shape with an interior
ctx.fill(mark.subtract(bite));            // narrow it, cut it, boolean it against anything
```

`ctx.strokeToPath(path?)` returns what `stroke()` would have drawn, as a fillable path — the
difference between a line that has a width and a mark that has a shape. It bakes in the current
`lineWidth`, `lineCap`, `lineJoin` and `pathEffect`, so a textured stroke becomes geometry and
survives into `outSvg` as vector rather than only as pixels.

---

## 4. Directional Cross-Hatching & Feathering Recipes

> **Implemented by**: `Drawing.drawFeathering(ctx, origin, angleDeg, count, length, spacing, strokeColor, lineWidth)`
> and `Drawing.drawCrossContourHatch(ctx, cx, cy, rx, ry, startAngle, endAngle, count, strokeColor, lineWidth)`
> for *placed* fans of lines, and `Skia.PathEffect.hatch(width, spacing, angleDeg)` to fill a whole
> region with them at once.
>
> The two are different tools for different jobs. The `Drawing.*` calls put a specific fan at a
> specific place — you choose the origin, the direction and the count, which is what a terminator
> transition needs. `PathEffect.hatch` fills whatever you then fill or stroke, as geometry, which is
> what a *field* of tone needs. Cross-hatching is two of them summed at opposing angles:
>
> ```js
> ctx.pathEffect = Skia.PathEffect.sum(
>     Skia.PathEffect.hatch(1, 6, 45),
>     Skia.PathEffect.hatch(1, 6, -45));
> ```
>
> Because these are lines rather than pixels, they scale with the artwork and export as vector.

### Linear Feathering (Shadow Terminator Transitions)

> **Source**: Klaus Janson, *The DC Comics Guide to Inking Comics*, ch. 9 (pp. 110–113) — "Feathering".

> **Principle**:
> **To feather means to soften.** The word comes from the feather itself: ribs emerging from a spine
> at an angle, repetitive, all connected to one source. Specifically, feathering is the repetitive
> lines that emerge at an angle from a heavier line — and where they meet that line they create a
> **gray** which softens the mass. The point where black meets white is otherwise dramatic and
> jarring; feathering is what makes it a transition.
>
> **It has two duties, and the second is the one that gets skipped:**
>
> 1. **Soften the transition** between black and white.
> 2. **Communicate form and volume.** The small lines must follow the form of the mass they sit on —
>    feathering across a flexed bicep follows the arm's curve, and the head at a three-quarter angle
>    divides into planes that are each feathered at their own angle (p. 98). *Feathering should
>    respond to the organic shape of the subject.*
>
> Janson's standard for every mark follows from the second duty: **every line has to mean something.**
> A line that does not describe a form, a shape or a direction is extraneous.

**Three shapes of feathered line**, and the toolkit draws them differently:

| Shape | What it is | How to draw it |
| :--- | :--- | :--- |
| **Weighted** (classic) | Thick where it emerges from the black, tapering to a point | `Drawing.drawTaperedStroke(...)` per line, or `ctx.useBrush(Skia.Brush.ink(...))` |
| **Triangular** | A series of triangles, letting more white into the fan | Filled paths — and **use sparingly**: Janson warns it doubles the line count and turns busy fast |
| **Dead** (non-weighted) | Uniform weight throughout, sometimes with no anchoring line at all | `Drawing.drawFeathering(...)` — this is the shape it produces |

> [!IMPORTANT]
> **Fix the light source before feathering anything.** Janson is unambiguous: *feathering a figure is
> impossible without establishing a light source*, because the light decides both where the thick and
> thin go **and which side of the form gets feathered at all**. Light from above puts the feathering
> on the bottom of the form — on a sphere, the lower half takes the line work.
>
> **Never feather where the light strikes directly.** The fan lives on the shadow mass, worked from
> the edge nearest the light, which is exactly the terminator. `Drawing.drawFeathering(...)` takes an
> `angleDeg`, and that angle is not a style choice — it comes from the same light vector you passed to
> `Drawing.renderVolumetricSphere(...)` or `Drawing.createThreePointLighting(...)`.

> [!TIP]
> **Feathering is also how a body becomes one object.** Janson's fourth tip is *ink the body as an
> organized single shape* — a figure is a stack of masses of different sizes, and letting a single
> light source govern every fan is what pulls the most disorganized shapes together. It is the same
> argument Manual 07 §4 makes for one light rig per scene, applied at the scale of a single figure.

Feathering uses parallel tapered lines extending from solid black shadow masses into the illuminated zones:

`Drawing.drawFeathering(ctx, origin, angleDeg, count, length, spacing, strokeColor, lineWidth)` lays a fan of tapering parallel lines from an origin. `Drawing.drawCrossContourHatch(ctx, cx, cy, rx, ry, startAngle, endAngle, count, strokeColor, lineWidth)` bends them around a cylindrical form instead.

Pick by intent, not by appearance:

- **Feathering** transitions a **shadow terminator**. Lines run perpendicular to the shadow edge, densest at the dark end.
- **Cross-contour hatching** describes **volume**. Lines follow surface curvature, so they read as wrapping around the form.

Both accept a flat-number overload for the origin.

---

## 5. Solid Black Ink Placement (Graphic Chiaroscuro)

> **Implemented by**: no dedicated call — solid blacks are `ctx.fill(...)` decisions. Where a black
> must transition rather than terminate, break it with `Drawing.createHalftoneDotShader(...)` (§5 of
> Manual 04), or `Skia.PathEffect.tile(dot, spacing)` to place the dots as real geometry, instead of
> a gradient. Where a black must *fade* rather than break up — a shadow dissolving into the ground
> rather than stopping at an edge — `ctx.maskFilter = Skia.MaskFilter.blur(sigma)` softens the shape's
> coverage while leaving the fill flat, which a gradient cannot do.

> **Source**: Klaus Janson, *The DC Comics Guide to Inking Comics*, ch. 7 (pp. 90, 96–97).

> **Principle**:
> **Blacks are placed for composition, not for realism.** Janson: the motivation for where the blacks
> go *is not about duplicating reality — it's about creating effective and interesting compositions
> and making them look real*. Comic art is the interpretation of reality, not its recreation.
>
> **Simplify the range, and he gives the number:** a panel with **one black, one white and one gray**
> is more efficient than a panel with a series of grays, because *in a panel of grays, nothing stands
> out*. That is Manual 09 §3's notan arriving from the inking side, and the same count as the
> four-value exercise there.
>
> Note also where gray comes from in this medium: **an artist creates a tone as soon as two ink lines
> sit next to each other.** Gray is not a fill, it is a density of black lines against white paper —
> which is why `Skia.PathEffect.hatch(...)` is a *tone* control and not a texture.

**Two uses of contrast worth naming, because both are placements rather than renderings:**

1. **Framing.** To call attention to part of a panel, surround it with a black shape that isolates it.
   Janson's examples add blacks that were not in the pencils purely to direct the eye — blast lines
   forming a frame around the figure, a black behind a gun so it reads as the threat, a shadow on a
   door that separates a hand from the background so the eye goes to the key.
2. **Direct black-against-white for clarity.** A thin white line is easily lost on white paper; put
   black behind it and *no matter how thin the line, it will always be visible and clear*.

Both are `ctx.fill(...)` decisions about a region, not effects — and both are worth reaching for when
a render measured with `bitmap.palette(...)` shows the accent losing its share (Manual 09 §2).

A hallmark of professional comic art is the confident use of **solid black ink shapes** (`#0a0a0c`):
- **Inside the mouth cavity** (behind teeth).
- **Under the jaw and chin** (cast shadow onto the throat).
- **In the deepest crevices between hair clumps**.
- **In the fold troughs of the coat and collar**.

---

## 6. Constructing It: A Runnable Inking Sheet

The tiers of §1 are literally the `maxThickness` argument. This sheet draws all three, then both hatching modes side by side.

```javascript
// Inking reference sheet: three weight tiers, then feathering vs cross-contour.
const canvas = createCanvas(760, 540);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f3';
ctx.fillRect(0, 0, 760, 540);

// §1 — Tier selection IS the parameter. Same curve, three weights.
const tiers = [
    { y: 110, w: 9.0, label: 'contour silhouette' },
    { y: 230, w: 4.5, label: 'interior form' },
    { y: 350, w: 1.8, label: 'detail' }
];
for (const t of tiers) {
    Drawing.drawTaperedStroke(ctx,
        { x: 90, y: t.y },
        { x: 260, y: t.y - 66 },
        { x: 470, y: t.y + 66 },
        { x: 660, y: t.y },
        t.w, '#0a0a0c');
    log(t.label + ' -> maxThickness ' + t.w);
}

// §4 — Feathering transitions a terminator: lines perpendicular to the edge.
Drawing.drawFeathering(ctx, { x: 120, y: 470 }, 285, 14, 58, 9, '#0a0a0c', 1.2);

// §4 — Cross-contour describes volume: arcs follow the form's curvature.
Drawing.drawCrossContourHatch(ctx, 520, 470, 110, 42, -0.5, 3.2, 10, '#0a0a0c', 1.2);

canvas;
```

---

## 7. The Same Tiers as a Medium

§1 chooses a weight; a **brush** chooses a weight *and* what the mark is made of. `ctx.useBrush(...)`
applies one, and clears whatever the previous medium set — so an ink line after a pencil passage does
not inherit the pencil's jitter.

This sheet inks the three tiers as a medium rather than as a number, fills a region with
cross-hatching, and cuts a stroke into a shape.

```javascript
// Media, hatching as geometry, and a stroke that became a shape.
const canvas = createCanvas(760, 460);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f3';
ctx.fillRect(0, 0, 760, 460);

// §1 as a medium: the tier is the brush's width argument.
const tiers = [
    { y: 70,  w: 4.5, label: 'tier 1 — outer silhouette' },
    { y: 130, w: 2.4, label: 'tier 2 — internal contour' },
    { y: 190, w: 1.1, label: 'tier 3 — detail' }
];
for (const t of tiers) {
    ctx.save();
    ctx.useBrush(Skia.Brush.ink('#0a0a0c', t.w));
    ctx.beginPath();
    ctx.moveTo(90, t.y);
    ctx.bezierCurveTo(260, t.y - 26, 470, t.y + 26, 660, t.y);
    ctx.stroke();
    ctx.restore();
    log(t.label + ' -> width ' + t.w);
}

// §4 as a field: cross-hatching is two hatches summed at opposing angles.
ctx.save();
ctx.fillStyle = '#0a0a0c';
ctx.pathEffect = Skia.PathEffect.sum(
    Skia.PathEffect.hatch(1, 7, 45),
    Skia.PathEffect.hatch(1, 7, -45));
ctx.fillRect(90, 250, 260, 120);
ctx.restore();

// §3 — the stroke as a shape: outline it, then cut it.
ctx.save();
ctx.lineWidth = 26;
ctx.lineCap = 'round';
const spine = new CanvasPath();
spine.moveTo(430, 310);
spine.lineTo(660, 310);
const mark = ctx.strokeToPath(spine);
const bite = new CanvasPath();
bite.arc(545, 310, 26, 0, Math.PI * 2);
ctx.fillStyle = '#0a0a0c';
ctx.fill(mark.subtract(bite));
ctx.restore();

// A black that fades rather than terminates.
ctx.save();
ctx.fillStyle = '#0a0a0c';
ctx.maskFilter = Skia.MaskFilter.blur(9);
ctx.fillRect(90, 400, 570, 26);
ctx.restore();

canvas;
```
