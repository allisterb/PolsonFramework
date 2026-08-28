# Studio Manual 10: Principles of Logo Design & Geometry

> **Credits & Theoretical Foundation**: Synthesized from George Bokhua's *Principles of Logo Design* and Tubik Studio's *Logo Design: Tubik Magazine Issue 2*.
> **Purpose**: Provides mathematical formulas, geometric construction systems, optical balance corrections, and stress-testing protocols for creating iconic, memorable, and mathematically coherent logos and brand marks.

---

## 1. The Core Philosophy of Logo Geometry

> **Implemented by**: the toolkit as a whole — `Logo.*` for raster construction and `VectorLogo.*` / `paper.*` for the vector equivalents. Signatures: `polson://sdk/core/Logo` and `polson://sdk/core/VectorLogo`; returned models: `polson://sdk/schema/Logo`.

A professional logo mark is not a freehand sketch; it is a **rigorous geometric construct** grounded in:
1. **Rational Proportions**: Ratios that the human visual cortex perceives as inherently harmonious (Golden Ratio $\Phi = 1.61803398875$, $\sqrt{2} \approx 1.414$, $1:1$, $1:2$).
2. **Tangent Fillets & Continuity**: Zero broken kinks or unintended curvature discontinuities between lines and arcs ($G^0, G^1, G^2$ continuity).
3. **Optical Illusion Corrections**: Compensations for human perceptual phenomena (irradiation, bone effect narrowing, circular overshoot, visual mass centroids).
4. **Multi-Scale Vector Robustness**: Flawless legibility from a $16\text{px}$ favicon to a billboard.

---

## 2. Golden Ratio Systems & Logarithmic Spirals

> **Implemented by**: `Logo.createGoldenCircles(cx, cy, baseRadius, count, direction)` → `{ circles, phi, bounds }` and `Logo.drawGoldenSpiral(ctx, cx, cy, startRadius, turns, options)`. Vector equivalents: `paper.goldenCircles(...)` and `paper.goldenSpiral(...)`, or `VectorLogo.goldenCircles(paper, ...)` with the paper as first argument.

### A. Golden Circles ($\Phi = 1.61803398875$)
Concentric and tangent circles proportioned by the golden ratio provide the foundational modular curves for cutting and forming marks:

$$R_{n} = R_{0} \cdot \Phi^{-n} = R_{0} \cdot (0.618034)^n$$

```
   ┌────────────────────────────────────────────────────────┐
   │                  R0 = 100.0px (Base)                   │
   │   ┌────────────────────────────────────────────────┐   │
   │   │              R1 = 61.8px (Phi^-1)              │   │
   │   │   ┌────────────────────────────────────────┐   │   │
   │   │   │          R2 = 38.2px (Phi^-2)          │   │   │
   │   │   │   ┌────────────────────────────────┐   │   │   │
   │   │   │   │      R3 = 23.6px (Phi^-3)      │   │   │   │
   │   │   │   │   ┌────────────────────────┐   │   │   │   │
   │   │   │   │   │  R4 = 14.6px (Phi^-4)  │   │   │   │   │
   │   │   │   │   └────────────────────────┘   │   │   │   │
   │   │   │   └────────────────────────────────┘   │   │   │
   │   │   └────────────────────────────────────────┘   │   │
   │   └────────────────────────────────────────────────┘   │
   └────────────────────────────────────────────────────────┘
```

- **Usage in Code**:
  - JavaScript Canvas: `Logo.createGoldenCircles(cx, cy, baseRadius, count)`
  - Snap.svg: `paper.goldenCircles(cx, cy, baseRadius, count)`

### B. The Logarithmic Golden Spiral
The golden spiral expands outward by a factor of $\Phi$ every quarter turn ($90^\circ$ or $\pi/2$ radians):

$$r(\theta) = a \cdot e^{b\theta} \quad \text{where} \quad b = \frac{\ln(\Phi)}{\pi / 2} \approx 0.3063489$$

- **Usage in Code**:
  - JavaScript Canvas: `Logo.drawGoldenSpiral(ctx, cx, cy, startRadius, turns)`
  - Snap.svg: `paper.goldenSpiral(startX, startY, initialRadius, turns)`

---

## 3. Tangent Fillets & Curve Continuity

> **Implemented by**: `Logo.createTangentBlend(p1, corner, p2, radius)` → `{ arcStart, arcEnd, arcCenter, tangentDistance, cornerAngleDeg, sweepAngleDeg }` — it returns the tangency points, so you stroke the arc yourself and the join stays $G^1$. For a ready-made path string, `Snap.path.tangentFillet(x1, y1, cornerX, cornerY, x2, y2, radius)`.

When two straight lines meet at an angle $\theta$, joining them with a sharp corner creates visual tension. A **tangent fillet** inserts an arc of radius $R$ that smoothly touches both lines:

```
          P1
           \
            \
             T1 (Tangent Point 1)
              \  __-- arc (Radius R)
               \/    \
               /\     T2 (Tangent Point 2)
              /  \___/ \
     Corner Pc          P2
```

1. Unit direction vectors: $\vec{u}_1 = \frac{P_1 - P_c}{\|P_1 - P_c\|}$, $\vec{u}_2 = \frac{P_2 - P_c}{\|P_2 - P_c\|}$
2. Angle between segments: $\alpha = \arccos(\vec{u}_1 \cdot \vec{u}_2)$
3. Distance from corner to tangent points: $d = \frac{R}{\tan(\alpha / 2)}$
4. Tangent points: $T_1 = P_c + d \cdot \vec{u}_1$, $T_2 = P_c + d \cdot \vec{u}_2$

- **Usage in Code**:
  - `Logo.createTangentBlend(p1, corner, p2, radius)`
  - `Snap.path.tangentFillet(x1, y1, cx, cy, x2, y2, radius)`

---

## 4. Optical Balance & Visual Corrections

> **Implemented by**: `Logo.correctBoneEffect(p1, p2, strokeWidth, pinchCorrectionFactor)` → `Point[]`, `Logo.computeOvershoot(baseHeight, shape)` → `number`, and `Logo.computeOpticalCenter(pointsOrBounds, shapeType)` → `Point`. These compute corrections; applying them is still your call.

### A. The Bone Effect (Middle Stem Narrowing)
When two parallel lines or a thick bar connects two larger shapes, human perception makes the middle of the straight bar look **narrower and pinched** (like a dog bone).
- **The Correction**: Add a subtle outward parabolic curve/bulge of $2\%–5\%$ to the middle of the stem.
- **Formula**: Quadratic Bézier control point $P_{\text{ctrl}} = P_{\text{mid}} + \vec{n} \cdot \text{maxBulge}$.
- **Usage in Code**: `Logo.correctBoneEffect(p1, p2, strokeWidth)` or `Snap.path.boneEffect(...)`.

### B. Optical Overshoot (Apex & Curve Alignment)
A circle or pointed apex of height $H$ sitting next to a flat-topped rectangle of height $H$ appears **shorter** because the eye averages the negative space around the point or round top.
- **The Correction**: Round shapes and sharp apexes must overshoot flat baselines and cap-heights by **$1.5\%–3.5\%$** of their total dimension.
- **Usage in Code**: `Logo.computeOvershoot(baseHeight, 'circle' | 'triangle' | 'arch')`.

### C. Visual Center of Gravity (Centroid vs. Bounding Box)
The geometric center (bounding box center $Y = 50\%$) of triangular, teardrop, or tapering marks feels **bottom-heavy**.
- **The Correction**: Shift the mark upward so its visual center of mass rests at **$Y \approx 44\%–48\%$** of the container height.
- **Usage in Code**: `Logo.computeOpticalCenter(pointsOrBounds)`.

---

## 5. Modern Enclosures & Squircles

> **Implemented by**: `Logo.drawSquircle(ctx, x, y, width, height, options)` and `Logo.createSquirclePath(...)` → `SKPath` for clipping. `Logo.drawEmblemBadge(ctx, cx, cy, radius, type, options)` covers shield/hexagon/diamond/scallop/circle crests. Vector: `paper.squircle(...)`, `paper.emblemBadge(...)`.

Modern brand design has largely replaced sharp rounded rectangles with **Lamé Superellipses** (Squircles), which feature continuous curvature ($G^2$) with no abrupt jump from straight line to circular arc:

$$\left|\frac{2(x - c_x)}{w}\right|^n + \left|\frac{2(y - c_y)}{h}\right|^n = 1 \quad (n \approx 4.0\text{--}5.0)$$

```
     Standard Rounded Rect (G1 Kink)          Lamé Squircle (G2 Smooth)
          ┌─────────────┐                          ╭─────────────╮
          │             │                         │               │
          │             │                         │               │
          └─────────────┘                          ╰─────────────╯
```

- **Usage in Code**:
  - JavaScript Canvas: `Logo.drawSquircle(ctx, x, y, width, height, { exponent: 4.5 })`
  - Snap.svg: `paper.squircle(x, y, width, height, 4.5)`

---

## 6. Verification Suites & Brand Guidelines

> **Implemented by**: `Logo.generateFaviconScaleTest(ctx, drawMarkFn, options)`, `Logo.generateMonochromeTest(ctx, drawMarkFn, width, height, irradiationStrength)`, and `Logo.drawClearSpaceGuide(ctx, markBounds, xDimension, options)`. The two suites take a **mark-drawing function** `(ctx, size) => void`, so write the mark once and let them re-render it at every scale and treatment.
>
> ⚠️ `drawClearSpaceGuide` shades the margin and then **clears the mark rectangle to transparent**, so it erases anything already drawn there. Call it *before* you draw the mark, not after.

### A. 7-Tier Favicon Scale Ladder
A mark must remain distinct across 7 critical digital display sizes:
- $16\text{px}$ (Browser tab favicon)
- $24\text{px}$ (System tray / status bar)
- $32\text{px}$ (Retina browser tab)
- $48\text{px}$ (Desktop app icon)
- $64\text{px}$ (Mobile dock icon)
- $128\text{px}$ (App Store small)
- $256\text{px}$ (Hero splash icon)
- **Usage in Code**: `Logo.generateFaviconScaleTest(ctx, drawMarkFn)`.

### B. 4-Way Monochrome & Contrast Verification
Every professional logo must work in strict 1-color applications without relying on color or gradients:
1. Positive 1-color black on white.
2. Negative knockout white on dark slate.
3. Grayscale neutral — mid-grey ink on light grey.
4. App icon squircle badge with gradient.
- **Usage in Code**: `Logo.generateMonochromeTest(ctx, drawMarkFn)`.

**Irradiation.** A light shape on a dark ground appears *larger* than the same shape dark-on-light — Galileo noticed it in the naked-eye planets before neuroscience explained it. So a knockout logo set at its positive's exact dimensions looks bigger than the positive, and a pair that measures equal does not read equal. The board corrects for it: the two light-on-dark panels are shrunk by `Logo.computeIrradiationCompensation(ink, background)` and their labels report the amount.

The default correction is $1.5\%$ at maximum contrast, scaled by the actual luminance difference. It is a working figure, not a measured constant — the effect depends on contrast, scale and viewing distance, so tune it until the pair looks equal, which is the only test that matters. Apply the same scale in your own artwork whenever you hand a client a reversed lockup:

```js
const { scale } = Logo.computeIrradiationCompensation('#ffffff', '#111827');
drawMark(ctx, markSize * scale);   // the reversed version, sized to look equal
```

> [!NOTE]
> The correction is a uniform scale rather than an erosion of the silhouette. Both work — outlining, expanding and subtracting a stroke is the sounder technique by hand — but erosion operates in device pixels, so the same board rendered at twice the resolution would erode half as much in relative terms. A verification board has to be reproducible.

### C. Clear Space Rule ($X$-Dimension)
A protective exclusion zone around the logo where no other text or graphics may enter:
- $X = \text{Height of the mark} / 4$ (or the cap-height of the initial letter).
- Clear space boundary $= \text{Bounds} + X$ on all 4 sides.
- **Usage in Code**:
  - JavaScript Canvas: `Logo.drawClearSpaceGuide(ctx, markBounds, xDimension)`
  - Snap.svg: `paper.clearSpaceGuide(x, y, width, height, margin)`

---

## 7. Symbol → SDK Parameter Map

| Book symbol | SDK parameter or field | Notes |
| --- | --- | --- |
| $\Phi = 1.61803398875$ | `golden.phi` | Returned on the result; do not retype the constant. |
| $R_n = R_0 \Phi^{-n}$ | `golden.circles[n].radius` | Each entry also carries `phiFactor`. |
| $R_0$ | `baseRadius` | Argument 3 of `createGoldenCircles`. |
| growing / shrinking | `direction` | Argument 5: `'growing'` or `'shrinking'`. |
| $b = \ln\Phi / (\pi/2)$ | — | Internal to `drawGoldenSpiral`; control it via `turns`. |
| Fillet radius $R$ | `radius` | Argument 4 of `createTangentBlend`. |
| Tangency points | `blend.arcStart`, `blend.arcEnd`, `blend.arcCenter` | Stroke the arc between these to keep $G^1$. |
| Overshoot % | `Logo.computeOvershoot(baseHeight, shape)` | Returns pixels, not a percentage. |
| Visual centroid | `Logo.computeOpticalCenter(bounds, shapeType)` | Sits above the geometric centre. |
| Squircle exponent $n$ | `options.exponent` | ~4.5 is the iOS-style curvature. |
| $X$ dimension | `xDimension` | Argument 3 of `drawClearSpaceGuide`. |

---

## 8. Constructing It: A Runnable Construction Plate

Geometry first, mark second, corrections third. The golden circles are the substrate the mark is *cut from* — drawing them after the fact is decoration, not construction.

```javascript
// Golden construction plate: circles → spiral → squircle container → optical checks.
const canvas = createCanvas(900, 620);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f7f5f0';
ctx.fillRect(0, 0, 900, 620);

// §2A — Φ-scaled circles. Read phi off the result rather than retyping it.
const golden = Logo.createGoldenCircles(280, 320, 150, 5, 'shrinking');
log('phi = ' + golden.phi);
ctx.strokeStyle = '#c9c2b2';
ctx.lineWidth = 1;
for (let i = 0; i < golden.circles.length; i++) {
    const c = golden.circles[i];
    ctx.beginPath();
    ctx.arc(c.cx, c.cy, c.radius, 0, Math.PI * 2);
    ctx.stroke();
    log('R' + i + ' = ' + c.radius.toFixed(1) + 'px  (Φ^-' + i + ')');
}

// §2B — The spiral shares the same centre, so the two systems agree. Keep `turns`
// low: the radius multiplies by Φ every quarter turn, so 2.5 turns is Φ^10 ≈ 122x
// the start radius and runs off any canvas you give it.
Logo.drawGoldenSpiral(ctx, 280, 320, 8, 1.5, {
    strokeColor: '#b0793a',
    lineWidth: 2.5,
    drawGoldenRectangles: true,
    rectStrokeColor: '#ded6c4'
});

const icon = { x: 600, y: 190, width: 220, height: 220 };

// §6C — Guides BEFORE the mark. drawClearSpaceGuide clears the mark rectangle to
// transparent as it shades the margin, so calling it afterwards erases the mark.
Logo.drawClearSpaceGuide(ctx, icon, icon.height / 4, { showLabels: true });

// §5 — A squircle container: continuous curvature, not a rounded rectangle.
Logo.drawSquircle(ctx, icon.x, icon.y, icon.width, icon.height, {
    fill: '#1f3b57',
    stroke: '#0d1f2f',
    strokeWidth: 2,
    exponent: 4.5
});

// §4 — Corrections are computed, then applied by you. A circle set flush to a
// cap line reads short; it must overshoot to look aligned.
const overshoot = Logo.computeOvershoot(icon.height, 'circle');
const optical = Logo.computeOpticalCenter(icon, 'general');
log('circle overshoot = ' + overshoot.toFixed(2) + 'px');
log('optical centre y = ' + optical.y.toFixed(1) +
    ' vs geometric ' + (icon.y + icon.height / 2).toFixed(1) + ' — place the mark on the former');

canvas;
```
