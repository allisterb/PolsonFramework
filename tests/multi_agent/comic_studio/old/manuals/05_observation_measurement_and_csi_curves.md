# Studio Manual 05: Observation, Measurement & CSI Curves

> **Source Reference**: *Imaginative Drawing*, Chapter 1: "Observation & Measurement" (`reference/books/chapter1_observation.pdf`)  
> **Purpose**: Translates foundational drawing techniques (relative distance measurement, plumb lines, the CSI curve grammar, planar forms, and two/three-value light studies) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Relative Distances & The Unit System

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
```javascript
/**
 * Computes all facial landmarks parametrically from a single origin and head height.
 */
function createParametricHead(originX, originY, headHeight, yawAngleDeg = 35) {
    const H = headHeight;
    const W = H * 0.72;
    const rad = (yawAngleDeg * Math.PI) / 180;
    
    // 3/4 Perspective Foreshortening Offset
    const turnX = Math.sin(rad) * (W * 0.22);
    const centerAxisX = originX + turnX;
    
    // Vertical Thirds
    const yCrown = originY - H * 0.5;
    const yHairline = originY - H * 0.28;
    const yBrow = originY - H * 0.05;
    const yEye = originY + H * 0.02;
    const yNose = originY + H * 0.20;
    const yMouth = originY + H * 0.33;
    const yChin = originY + H * 0.50;

    const eyeW = W * 0.20;
    const farScale = Math.cos(rad);

    return {
        unit: { H, W, eyeW },
        crown: { x: originX, y: yCrown },
        chin: { x: centerAxisX + turnX * 0.1, y: yChin },
        centerAxisX,
        browY: yBrow,
        eyeY: yEye,
        noseY: yNose,
        mouthY: yMouth,
        
        // Eyes
        nearEye: {
            inner: { x: centerAxisX + eyeW * 0.45, y: yEye },
            outer: { x: centerAxisX + eyeW * 1.45, y: yEye - 3 },
            center: { x: centerAxisX + eyeW * 0.95, y: yEye }
        },
        farEye: {
            inner: { x: centerAxisX - eyeW * 0.35, y: yEye },
            outer: { x: centerAxisX - eyeW * (0.35 + farScale), y: yEye - 2 },
            center: { x: centerAxisX - eyeW * (0.35 + farScale * 0.5), y: yEye }
        },
        
        // Ear & Jaw
        ear: { x: originX - W * 0.45, y: (yBrow + yNose) * 0.5 },
        jawAngle: { x: originX - W * 0.28, y: yNose + H * 0.08 }
    };
}
```

---

## 2. Plumb Lines & Level Lines (Horizontals and Verticals)

> **Core Insight from the Book**: Dropping vertical plumb lines and horizontal level lines allows artists to check anatomical alignment without perspective distortion:
> - *Vertical Plumb Line*: Dropped from the ear crosses the jaw angle and collarbone.
> - *Horizontal Level Line*: Projected from the chin intersects the far shoulder.

### Verification Helper in Code
```javascript
/**
 * Verifies vertical alignment within a tolerance window.
 */
function verifyPlumbAlignment(topPoint, bottomPoint, maxDeltaX = 12) {
    const deltaX = Math.abs(topPoint.x - bottomPoint.x);
    return {
        aligned: deltaX <= maxDeltaX,
        deltaX,
        message: deltaX <= maxDeltaX ? 'PASS' : `DRIFT: offset by ${deltaX.toFixed(1)}px`
    };
}
```

---

## 3. The "CSI Line" Language for Expressive Contours

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
```javascript
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

> **Core Insight from the Book**:
> - **Two-Value Study**: Strictly separates the illuminated half of the figure from the shadow half along the **Shadow Terminator**.
> - **Three-Value Study**: Highlights ($V_1$), Midtones ($V_2$), Core Shadow ($V_3$).

### Mapping Value Studies to Polson Pipeline:
1. **Penciler**: Draws the **Shadow Terminator boundary path** (Two-Value division).
2. **Colorist**: Fills Midtone base ($V_2$), sunlit highlights ($V_1$), and core shadow planes ($V_3$) using `Skia.Shader.sksl` Ben-Day dots.
3. **Inker**: Adds Ambient Occlusion ink masses ($V_4$, `#0a0a0c`) in deep crevices.
