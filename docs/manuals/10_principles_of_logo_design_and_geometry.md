# Studio Manual 10: Principles of Logo Design & Geometry

> **Credits & Theoretical Foundation**: Synthesized from George Bokhua's *Principles of Logo Design* and Tubik Studio's *Logo Design: Tubik Magazine Issue 2*.
> **Purpose**: Provides mathematical formulas, geometric construction systems, optical balance corrections, and stress-testing protocols for creating iconic, memorable, and mathematically coherent logos and brand marks.

---

## 1. The Core Philosophy of Logo Geometry

A professional logo mark is not a freehand sketch; it is a **rigorous geometric construct** grounded in:
1. **Rational Proportions**: Ratios that the human visual cortex perceives as inherently harmonious (Golden Ratio $\Phi = 1.61803398875$, $\sqrt{2} \approx 1.414$, $1:1$, $1:2$).
2. **Tangent Fillets & Continuity**: Zero broken kinks or unintended curvature discontinuities between lines and arcs ($G^0, G^1, G^2$ continuity).
3. **Optical Illusion Corrections**: Compensations for human perceptual phenomena (irradiation, bone effect narrowing, circular overshoot, visual mass centroids).
4. **Multi-Scale Vector Robustness**: Flawless legibility from a $16\text{px}$ favicon to a billboard.

---

## 2. Golden Ratio Systems & Logarithmic Spirals

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
3. High-contrast outline / wireframe.
4. App icon squircle badge with gradient.
- **Usage in Code**: `Logo.generateMonochromeTest(ctx, drawMarkFn)`.

### C. Clear Space Rule ($X$-Dimension)
A protective exclusion zone around the logo where no other text or graphics may enter:
- $X = \text{Height of the mark} / 4$ (or the cap-height of the initial letter).
- Clear space boundary $= \text{Bounds} + X$ on all 4 sides.
- **Usage in Code**:
  - JavaScript Canvas: `Logo.drawClearSpaceGuide(ctx, markBounds, xDimension)`
  - Snap.svg: `paper.clearSpaceGuide(x, y, width, height, margin)`
