# Studio Manual 03: Comic Inking & Feathering

> **Purpose**: Provides line weight hierarchy standards, tapered stroke algorithms, directional cross-hatching recipes, and graphic black ink placement rules for authentic comic book illustration.

---

## 1. Line Weight Hierarchy (The Three Inking Tiers)

> **Implemented by**: the `maxThickness` argument of `Drawing.drawTaperedStroke(...)`, or the `width`
> argument of `Skia.Brush.ink(color, width)` applied with `ctx.useBrush(...)`. Pick the tier here,
> then pass it; the tier *is* the parameter.

Comic inking derives its punch and depth from **stroke weight contrast**:

| Tier | Line Width | Purpose | Applied To |
|---|---|---|---|
| **Tier 1 (Outer Silhouette)** | **3.5px – 5.0px** | Defines the outer spatial boundary; grounds the figure against the background | Outer jawline, coat collar, outer hair mass contour, bandana underside |
| **Tier 2 (Internal Contours)** | **2.0px – 3.0px** | Defines structural forms and major overlapping planes | Eyelid creases, nose bridge, lip slit, hair lock boundaries, collar seams |
| **Tier 3 (Details & Hatching)** | **0.8px – 1.5px** | Adds volume, surface texture, and shadow transition | Eyelashes, iris fibers, neck tendon hatching, shirt fold wrinkles, rigging cord texture |

---

## 2. Inking Rules of Thumb

> **Implemented by**: `Drawing.drawTaperedStroke(...)` for weighted contours and `Drawing.drawFeathering(...)` for shadow transitions. These are judgement rules — the calls execute them, they do not decide them for you.

1. **The Light vs Gravity Rule**:
   - Lines facing the **light source** (top/left) should be **thin or broken** ($1.0\text{px} - 1.5\text{px}$).
   - Lines facing **away from light or affected by gravity** (underside of jaw, bottom of hair curls, coat hem) should be **heavy and thick** ($3.5\text{px} - 5.0\text{px}$).
2. **Line Tapering**:
   - Comic lines never end in flat, blunt cylindrical cutoffs. Every line starts at a point, swells in the middle, and tapers to a fine point.

---

## 3. Algorithmic Tapered Inking in Canvas2D

> **Implemented by**: `Drawing.drawTaperedStroke(ctx, start, cp1, cp2, end, maxThickness, fillOrStrokeStyle)`.

`Drawing.drawTaperedStroke(ctx, start, cp1, cp2, end, maxThickness, fillOrStrokeStyle)` samples the cubic Bézier and varies width along its length, so the stroke swells through the middle and closes at both ends.

- Points may be `{ x, y }` objects, or pass ten flat numbers (`sx, sy, cp1x, cp1y, cp2x, cp2y, ex, ey, maxThickness, style`).
- `fillOrStrokeStyle` accepts a colour string or an `SKShader`.

Choose `maxThickness` from the tier table in §1 — contour silhouette at the heavy end, interior detail at the light end. A constant-width stroke reads as a technical drawing, not as inking.

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
