# Studio Manual 05: Observation, Measurement & CSI Curves

> **Source Reference**: *Imaginative Drawing*, Chapter 1: "Observation & Measurement" (`reference/books/chapter1_observation.pdf`)  
> **Purpose**: Translates foundational drawing techniques (relative distance measurement, plumb lines, the CSI curve grammar, planar forms, and two/three-value light studies) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Relative Distances & The Unit System

> **Implemented by**: `Drawing.createLoomisHead(...)` returns the unit system as `head.unit`, and `Drawing.computeRelativeDistance(headHeight, pointA, pointB)` measures in head-lengths.

> **Core Insight from the Book**: In drawing, artists do NOT memorize absolute global pixel coordinates. They choose a **single fundamental unit of measure** (the **Head Length**, $H_{\text{head}} = Y_{\text{chin}} - Y_{\text{crown}}$) and derive every other distance as a relative proportion.

```
┌────────────────────────────────────────────────────────┐
│                   UNIT SYSTEM (Head Length)            │
├───────────────────────────────┬────────────────────────┤
│ Total Head Height ($H$)       │ $1.0 \times H$         │
│ Head Width ($W$)              │ $0.72 \times H$        │
│ Eye Width ($W_{\text{eye}}$)  │ $0.20 \times W$ (1/5)  │
│ Eye Spacing                   │ $1.0 \times W_{\text{eye}}$ │
│ Nose Width at Base            │ $1.0 \times W_{\text{eye}}$ │
│ Mouth Width                   │ $1.5 \times W_{\text{eye}}$ │
│ Brow Line to Chin Base        │ $0.66 \times H$ (2/3)  │
│ Ear Height (Brow to Nose)     │ $0.33 \times H$ (1/3)  │
└───────────────────────────────┴────────────────────────┘
```

### Algorithmic Parametric Unit Generator
`Drawing.createLoomisHead(originX, originY, headHeight, yawDeg, pitchDeg)` performs this parametric construction and returns the unit system with it. Read `head.unit` rather than recomputing:

| Field | Meaning |
| --- | --- |
| `head.unit.H` | Total head height — the master unit everything else is expressed in. |
| `head.unit.W` | Head width. |
| `head.unit.eyeW` | One eye-width. The face is ~5 of these across (Manual 01 §2). |
| `head.unit.thirdH` | `H / 3` — one Loomis third. |

Every landmark on the returned model is already placed against these units, so measurements taken from it are consistent by construction. See Manual 01 §3 for the full landmark table, and `polson://sdk/schema/Drawing` for the exact model.

---

## 2. Plumb Lines & Level Lines (Horizontals and Verticals)

> **Implemented by**: `Drawing.verifyPlumbAlignment(topPoint, bottomPoint, maxTolerance)` → `{ aligned, deltaX, message }`.

> **Core Insight from the Book**: Dropping vertical plumb lines and horizontal level lines allows artists to check anatomical alignment without perspective distortion:
> - *Vertical Plumb Line*: Dropped from the ear crosses the jaw angle and collarbone.
> - *Horizontal Level Line*: Projected from the chin intersects the far shoulder.

### Verification Helper in Code
`Drawing.verifyPlumbAlignment(topPoint, bottomPoint, maxTolerance)` → `{ aligned, deltaX, message }` performs this check. Run it on the landmark pairs above before committing a pose — it is cheap, and a figure that fails it will look wrong in a way that is hard to diagnose later.

```js
const plumb = Drawing.verifyPlumbAlignment(head.chin, figure.rightLeg.ankle, 12);
if (!plumb.aligned) log(plumb.message);   // "DRIFT: offset by 18.4px"
```

`Drawing.computeRelativeDistance(headHeight, pointA, pointB)` is the companion measurement: it returns the distance between two landmarks **in head-length units**, which is how §1 wants you to reason about proportion.

---

## 3. The "CSI Line" Language for Expressive Contours

> **Implemented by**: raster C/S/I curves are `ctx.quadraticCurveTo` / `ctx.bezierCurveTo` / `ctx.lineTo`, weighted by `Drawing.drawTaperedStroke(...)`. For vector work, `Snap.path.ogeeCurve(x1, y1, x2, y2, amplitude, inflectionT)` returns an S-curve path string directly.

> **Core Insight from the Book**: Complex organic contours must be distilled into three elemental line primitives:
> 1. **C-Curves**: Single continuous arc in one direction.
> 2. **S-Curves**: Reversing dynamic curves with opposing inflection points.
> 3. **Straights ("I-Lines")**: Stable structural lines.

```
       C-Curve                  S-Curve                  Straight (I-Line)
      ╭────────╮               ╭─────╮
     │          │             │       ╰─────╮        ──────────────────────
     │          │             │              │
      ╰────────╯               ╰────────────╯
   (Cranium, Eyelid, Jaw)     (Windblown Hair, Tendons)  (Nose Bridge, Shrouds)
```

### The CSI Helper Library in Canvas2D
```js
const CSI = {
    // 1. C-Curve: Single-direction quadratic or cubic arc
    C(ctx, start, cp, end) {
        ctx.moveTo(start.x, start.y);
        ctx.quadraticCurveTo(cp.x, cp.y, end.x, end.y);
    },

    // 2. S-Curve: Reversing cubic curve where CP1 and CP2 lie on opposite sides of the chord
    S(ctx, start, cp1, cp2, end) {
        ctx.moveTo(start.x, start.y);
        ctx.bezierCurveTo(cp1.x, cp1.y, cp2.x, cp2.y, end.x, end.y);
    },

    // 3. Straight (I-Line): Clean structural line
    I(ctx, start, end) {
        ctx.moveTo(start.x, start.y);
        ctx.lineTo(end.x, end.y);
    }
};
```

---

## 4. Planar Form & Cross-Contours

> **Implemented by**: `Drawing.drawCrossContourHatch(ctx, cx, cy, rx, ry, startAngle, endAngle, count, strokeColor, lineWidth)` — the arcs follow the form's curvature, which is what makes a plane read as curved rather than flat.

> **Core Insight from the Book**: Curved organic surfaces should be conceived as **discrete planes** with **cross-contour lines** wrapping around them to define cylindrical depth.

- **Cheek Plane**: Triangular planar facet connecting the cheekbone apex, nose wing, and mouth corner.
- **Mandible Plane**: Rectangular plane connecting the jaw angle, under-chin, and neck.
- **Cross-Contour Hatching**:
  ```javascript
  function drawCrossContourHatch(ctx, centerX, centerY, radiusX, radiusY, startAngle, endAngle, count = 8) {
      for (let i = 0; i < count; i++) {
          const t = i / (count - 1);
          const y = centerY - radiusY * 0.5 + t * radiusY;
          ctx.beginPath();
          ctx.ellipse(centerX, y, radiusX, radiusY * 0.25, 0, startAngle, endAngle);
          ctx.stroke();
      }
  }
  ```

---

## 5. Light, Shadow & Two/Three-Value Studies

> **Implemented by**: `Drawing.createNotanPalette(type)` for the value sets, and `Drawing.createCompositionGrid(...)` when the study is about placement as well as value (Manual 09).

> **Core Insight from the Book**:
> - **Two-Value Study**: Strictly separates the illuminated half of the figure from the shadow half along the **Shadow Terminator**.
> - **Three-Value Study**: Highlights ($V_1$), Midtones ($V_2$), Core Shadow ($V_3$).

### Mapping Value Studies to Polson Pipeline:
1. **Penciler**: Draws the **Shadow Terminator boundary path** (Two-Value division).
2. **Colorist**: Fills Midtone base ($V_2$), sunlit highlights ($V_1$), and core shadow planes ($V_3$) using `Skia.Shader.sksl` Ben-Day dots.
3. **Inker**: Adds Ambient Occlusion ink masses ($V_4$, `#0a0a0c`) in deep crevices.

---

## 6. Constructing It: A Runnable Measurement Pass

Measurement is a pass you run, not an intuition you hope for. Build the figure, then interrogate it in its own units.

```javascript
// Measurement pass: build → measure in head units → plumb check.
const canvas = createCanvas(520, 760);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f6f4ef';
ctx.fillRect(0, 0, 520, 760);

// §1 — The unit system arrives with the model. Never re-derive it.
const figure = Drawing.createMannequinFigure(260, 60, 640, {
    shoulderTiltDeg: -6,
    pelvicTiltDeg: 5
});
Drawing.drawMannequinWireframe(ctx, figure);
log('one head unit = ' + figure.headUnit.toFixed(1) + 'px');

// §1 — Measure in head-lengths, not pixels. The crotch sits at 4.0H.
const heads = Drawing.computeRelativeDistance(figure.headUnit, figure.head.center, figure.crotch);
log('head centre -> crotch = ' + heads.toFixed(2) + ' head lengths');

// §2 — Plumb check before committing the pose: sternum over the standing ankle.
const plumb = Drawing.verifyPlumbAlignment(figure.sternum, figure.rightLeg.ankle, 14);
log(plumb.message);

canvas;
```
