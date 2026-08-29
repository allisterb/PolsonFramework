# Studio Manual 02: Dynamic Hair & Flowing Ribbons

> **Purpose**: Provides geometric modeling principles, 3D ribbon algorithms, volume offset rules, and Bézier strand formulas for drawing expressive, windswept comic hair and headbands.

---

## 1. The Cardinal Rule: Hair Has 3D Volume

> **Implemented by**: `Drawing.drawHairRibbon(ctx, root, tip, bendFactor, width, fillTop, fillUnderside, strokeColor, strokeWidth)` — the two-fill signature is what enforces this rule; there is no single-colour form.

> **Rule of Volume**: Hair is NOT painted directly onto the skull line. Hair is composed of thousands of overlapping fibers that create a buoyant cushion. The outer boundary of the hair must float **$15\text{ to }35\text{ px}$ ABOVE and OUTSIDE** the cranial sphere line.

```
                   ╭───────────────────────────────╮  ◄── Outer Hair Volume Line
                ╭──╯                               ╰──╮   (15-35px above skull)
             ╭──╯       ┌─────────────────────┐       ╰──╮
             │          │    Cranial Dome     │          │
             │          │      (Skull)        │          │
             │          └─────────────────────┘          │
```

---

## 2. The 3D Ribbon Principle

> **Implemented by**: `Drawing.drawHairRibbon(...)`, where `fillTop` and `fillUnderside` are the two surfaces of the twist and `bendFactor` is the twist amount.

Every dynamic hair lock or curl is drawn as a **twisting 3D ribbon** with 4 continuous stages:

1. **Origin / Root Anchor**: An anchor point along the hairline, part line, crown whorl, or ponytail gather point.
2. **Arch / Wave Body**: A primary cubic Bézier curve representing the top ridge of the lock driven by velocity/gravity.
3. **Twist / Underside Plane**: The ribbon turns in 3D space, exposing its darker underside facet.
4. **Tapered Tip**: The return Bézier curve sweeps back to meet the lead curve at a sharp point $(X_{\text{tip}}, Y_{\text{tip}})$.

```
   Root Anchor
      (x0, y0) ───────────╮
                           \  ◄── Leading Upper Curve: bezierCurveTo(cp1, cp2, tip)
                            \
                             ╰───────────────╮
                             /               (xtip, ytip) ◄── Sharp Tapered Tip
            ╭───────────────╯               /
           /  ◄── Trailing Underside Curve /
          /
     (x1, y1)
```

---

## 3. Algorithmic Hair Lock Generator in Canvas2D

> **Implemented by**: `Drawing.drawHairRibbon(...)`. See `polson://sdk/core/Drawing` for the flat-number overload.

`Drawing.drawHairRibbon(ctx, root, tip, bendFactor, width, fillTop, fillUnderside, strokeColor, strokeWidth)` draws one lock as a bent ribbon: a lit top surface, a darker underside where the ribbon twists away from the light, and an ink contour.

- `root` / `tip` — `{ x, y }` points. A flat-number overload (`rx, ry, tx, ty, …`) is also accepted.
- `bendFactor` — perpendicular displacement of the Bézier control points, as a fraction of lock length. `0` is a straight lock; `0.25`–`0.5` reads as windblown.
- `fillTop` / `fillUnderside` — a colour string or an `SKShader`.

**Rules the renderer encodes** (§2), worth knowing so you can judge the result:
1. The underside is a **different value**, not merely a darker outline — that opposition is what makes the lock read as a twisting surface rather than a flat noodle.
2. The ribbon **tapers closed toward the tip**; locks never end on a blunt edge.
3. Overlapping locks must **vary in `bendFactor`**, or the mass reads as a comb rather than as hair.

---

## 4. Constructing a Windswept Ponytail & Curls

> **Implemented by**: repeated `Drawing.drawHairRibbon(...)` calls for the mass, plus
> `Drawing.drawTaperedStroke(ctx, start, cp1, cp2, end, maxThickness, style)` for the loose forehead
> curls that are too thin to read as ribbons.
>
> For fine texture *within* a mass — the many short strands that make a lock read as hair rather than
> as a shape — repeat a mark along the stroke instead of drawing each one:
> `ctx.pathEffect = Skia.PathEffect.stamp(strand, advance)`, where `'rotate'` (the default) turns each
> mark to follow the curve. The advance must exceed the mark's own width or the stamps overlap back
> into a solid band. This is the same call that gives foliage, grass and fur their density; a mass
> built from four wedges where the reference has twenty strands is the commonest way generated hair
> reads as cartoon.

### Anatomical Structure of a High Ponytail:
1. **Gather Point / Bandana Knot**:
   - High at the upper-rear crown of the skull $(X \approx C_x - 0.45 \cdot R, Y \approx C_y - 0.5 \cdot R)$.
   - All scalp hair lines flow radially *toward* this knot anchor.
2. **The Ponytail Bulb (Main Mass)**:
   - Hair erupts upward and backward before gravity and wind catch it.
   - Drawn as **3 to 4 overlapping cascading ribbon layers**:
     - *Back Layer (Darkest Shadow)*: Deep rust/brown under-mass.
     - *Mid Layer (Main Volume)*: Rich copper-red body mass.
     - *Front Layer (Sunlit Highlights)*: Crisp amber curls and wisps whipping in the wind.
3. **Forehead Framing Bangs & Stray Tendrils**:
   - Single S-curve curls that cross over the forehead and overlap the bandana wrap.
   - Tendrils hanging in front of the ear and sweeping across the neck.

---

## 5. Bandana & Headband Wrapping Geometry

> **Implemented by**: no dedicated call — compose it. Draw the band as a wide, low-`bendFactor` `Drawing.drawHairRibbon(...)`, then shade the wrap with `Drawing.drawCrossContourHatch(...)` so the cloth reads as bending around the cranial sphere.

A bandana wraps around the skull as an **elliptical cylinder**:
1. **Compression Effect**: The headband pulls tight against the skull, flattening the hair beneath it.
2. **Puffing Effect**: Hair immediately *above* and *behind* the band billows out dramatically.
3. **Knot & Flowing Tails**:
   - Knot drawn as a circular/oval fold with radial wrinkle lines.
   - Two tapered fabric tails flowing horizontally with the wind behind the neck.

---

## 6. Constructing It: A Runnable Ponytail

A hair mass is many ribbons with *varied* bend, plus thin strokes for what is too fine to be a ribbon.

```javascript
// Windswept ponytail: head → ribbon mass → loose forehead curl.
const canvas = createCanvas(640, 620);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, 640, 620);

const head = Drawing.createLoomisHead(300, 110, 340, 30, 0);
Drawing.drawLoomisWireframe(ctx, head);

// §2/§3 — Each lock gets its own bendFactor. Uniform bend reads as a comb (§3).
const root = { x: head.crown.x + 34, y: head.crown.y + 44 };
const bends = [0.45, 0.30, 0.52, 0.22, 0.38];
for (let i = 0; i < bends.length; i++) {
    const tip = { x: root.x + 200 + (i * 14), y: root.y + 70 + (i * 36) };
    // fillTop and fillUnderside must differ, or the lock reads flat (§1).
    Drawing.drawHairRibbon(ctx, root, tip, bends[i], 34 - (i * 3),
        '#c96a2e', '#7a3410', '#2a1206', 2.0);
}

// §4 — A loose curl is too thin to be a ribbon; ink it as a tapered stroke.
Drawing.drawTaperedStroke(ctx,
    { x: head.hairline.x - 62, y: head.hairline.y },
    { x: head.hairline.x - 28, y: head.hairline.y - 34 },
    { x: head.hairline.x + 18, y: head.hairline.y + 12 },
    { x: head.hairline.x + 44, y: head.hairline.y - 18 },
    6, '#7a3410');

canvas;
```
