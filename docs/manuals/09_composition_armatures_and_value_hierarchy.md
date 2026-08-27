# Studio Manual 09: Compositional Armatures, Value Hierarchy & Visual Emphasis

> **Source Reference**: *Imaginative Drawing*, Chapter 5: "Composition" (`reference/books/chapter5_composition.pdf`)  
> **Purpose**: Translates visual design theory (classical geometric armatures, focal emphasis rules, Notan value structures, cinematic vignetting, and the 70-20-10 proportional design law) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Classical Geometric Armatures

> **Implemented by**: `Drawing.createCompositionGrid(width, height, type, options)` → `CompositionGrid` with `lines` and `powerPoints`, where `type` is `'ruleOfThirds'`, `'goldenRatio'`, `'dynamicSymmetry'`, or `'triangle'`; render it with `Drawing.drawCompositionGrid(ctx, gridOrType, options)`.

> **Core Insight from the Book (Page 580 & 600)**:
> Great compositions are anchored on underlying geometric armatures rather than random placement:

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

## 2. Visual Emphasis & The 4 Hierarchical Tools

> **Implemented by**: `Drawing.drawLeadingLines(ctx, originPoints, focalPoint, options)` for tool 2 and `Drawing.drawVignette(ctx, width, height, options)` for tool 3. Tools 1 and 4 are decisions about value and spacing — drive them through `Drawing.createNotanPalette(...)` and `Drawing.subdivideProportions(...)`.

> **Core Insight from the Book (Page 580–595)**:
> To guide the viewer's eye through a scene:
>
> 1. **Contrast of Value**: The point of highest local contrast (purest dark next to purest light) receives priority.
> 2. **Leading Lines**: Directional diagonals, architecture edges, or cast shadow rays that converge upon the hero subject.
> 3. **Framing & Vignetting**: Dark peripheral vignetting or foreground silhouettes that lock the viewer inside the visual container.
> 4. **Isolation / Negative Space**: Surrounding the focal hero with clean breathing room to elevate readability.

---

## 3. Notan Value Structures

> **Implemented by**: `Drawing.createNotanPalette(type)` → a curated palette for `'binary'`, `'classic3'`, `'highKey'`, or `'lowKey'`. Key names differ per palette (`dominantLight` / `secondaryMid` / `accentDark` for `classic3`; `background` / `formDark` / `formMid` / `rimAccent` for `lowKey`), so read the returned object rather than assuming.

> **Core Insight from the Book (Page 618)**:
> Before adding detailed color, establish a rock-solid **Notan value hierarchy**:
>
> - **2-Value Binary Notan**: 50% Black, 50% White. Tests whether the graphic silhouette reads at a glance.
> - **3-Value Classic Notan**:
>   - **70% Dominant** (Light or Midtone)
>   - **20% Secondary** (Opposing Tone)
>   - **10% Accent** (Deepest Dark or Blown Highlight)
> - **High-Key**: Scene dominated by values 1–4 (soft, ethereal, bright).
> - **Low-Key**: Scene dominated by values 7–10 (dramatic, moody, noir, mystery).

---

## 4. The 70-20-10 Proportional Law ("Big, Medium, Small")

> **Implemented by**: `Drawing.subdivideProportions(bounds, direction, ratios)` → `{ big, medium, small }`, each a `Rect` you can hand straight to `ctx.fillRect(...)` or use as a placement region.

> **Core Insight from the Book (Page 627)**:
> Visual appeal demands varied scale:
> - **70% Big / Dominant**: The major backdrop, environment mass, or sky.
> - **20% Medium / Secondary**: The character figure, vehicle, or architectural structure.
> - **10% Small / Detail**: Micro-flourishes, specular highlights, textures, and facial features.

---

## 5. Symbol → SDK Parameter Map

| Book concept | SDK parameter or field | Notes |
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
