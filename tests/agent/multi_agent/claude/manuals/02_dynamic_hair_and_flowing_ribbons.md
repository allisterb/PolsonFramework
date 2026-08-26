# Studio Manual 02: Dynamic Hair & Flowing Ribbons

> **Purpose**: Provides geometric modeling principles, 3D ribbon algorithms, volume offset rules, and Bézier strand formulas for drawing expressive, windswept comic hair and headbands.

---

## 1. The Cardinal Rule: Hair Has 3D Volume

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

```javascript
/**
 * Draws an organic, tapering 3D hair lock with highlight surface and shadow underside.
 */
function drawHairRibbon(ctx, root, tip, bendFactor, width, fillTop, fillUnderside, strokeColor = '#0a0a0c') {
    const dx = tip.x - root.x;
    const dy = tip.y - root.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    
    // Perpendicular normal vector for curve bending
    const nx = -dy / dist;
    const ny = dx / dist;

    // 1. Control points for Upper Leading Curve
    const cp1x = root.x + dx * 0.35 + nx * bendFactor;
    const cp1y = root.y + dy * 0.35 + ny * bendFactor;
    const cp2x = root.x + dx * 0.75 + nx * (bendFactor * 0.8);
    const cp2y = root.y + dy * 0.75 + ny * (bendFactor * 0.8);

    // 2. Control points for Lower Trailing Curve (offset by ribbon width)
    const rootLowerX = root.x + nx * width;
    const rootLowerY = root.y + ny * width;
    const cp3x = rootLowerX + dx * 0.70 + nx * (bendFactor * 0.5);
    const cp3y = rootLowerY + dy * 0.70 + ny * (bendFactor * 0.5);
    const cp4x = rootLowerX + dx * 0.30 + nx * (bendFactor * 0.7);
    const cp4y = rootLowerY + dy * 0.30 + ny * (bendFactor * 0.7);

    // Render Upper Ribbon Surface
    ctx.beginPath();
    ctx.moveTo(root.x, root.y);
    ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, tip.x, tip.y);
    ctx.bezierCurveTo(cp3x, cp3y, cp4x, cp4y, rootLowerX, rootLowerY);
    ctx.closePath();

    ctx.fillStyle = fillTop;
    ctx.fill();
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = 2.0;
    ctx.lineJoin = 'round';
    ctx.stroke();

    // Render Inner Highlight Strand Streak
    ctx.beginPath();
    ctx.moveTo(root.x + dx * 0.15 + nx * (width * 0.3), root.y + dy * 0.15 + ny * (width * 0.3));
    ctx.bezierCurveTo(
        cp1x + nx * (width * 0.2), cp1y + ny * (width * 0.2),
        cp2x + nx * (width * 0.2), cp2y + ny * (width * 0.2),
        tip.x - dx * 0.1, tip.y - dy * 0.1
    );
    ctx.strokeStyle = fillUnderside;
    ctx.lineWidth = 1.5;
    ctx.stroke();
}
```

---

## 4. Constructing a Windswept Ponytail & Curls

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

A bandana wraps around the skull as an **elliptical cylinder**:
1. **Compression Effect**: The headband pulls tight against the skull, flattening the hair beneath it.
2. **Puffing Effect**: Hair immediately *above* and *behind* the band billows out dramatically.
3. **Knot & Flowing Tails**:
   - Knot drawn as a circular/oval fold with radial wrinkle lines.
   - Two tapered fabric tails flowing horizontally with the wind behind the neck.
