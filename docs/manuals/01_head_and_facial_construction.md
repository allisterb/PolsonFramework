# Studio Manual 01: Head & Facial Construction (The Loomis Method as Code)

> **Purpose**: Provides exact mathematical proportions, geometric construction formulas, and JavaScript Canvas2D algorithms for drawing human and comic heads in 3/4 and dynamic perspective views.

---

## 1. The Core Geometric Concept: Cranium Sphere + Facial Mask

> **Implemented by**: `Drawing.createLoomisHead(originX, originY, headHeight, yawDeg, pitchDeg)` → `LoomisHead`, then `Drawing.drawLoomisWireframe(ctx, head, options)` for the non-repro-blue construction pass. Signatures: `polson://sdk/core/Drawing`; model: `polson://sdk/schema/Drawing`.

Human head anatomy is not a collection of floating 2D shapes; it is a **3D volume** composed of two primary masses:
1. **The Cranial Sphere (Braincase)**: A 3D ball representing the skull.
2. **The Facial Mask & Jaw Plane**: A tapered wedge/cylinder extending downward and forward from the cranial sphere.

```
                    ┌─────────────────────────┐
                    │      Cranial Dome       │
                    │   (Circle/Sphere: R)    │
                    └────────────┬────────────┘
                                 │
           ┌─────────────────────┴─────────────────────┐
           ▼                                           ▼
┌───────────────────────┐                   ┌───────────────────────┐
│   Temporal Slice      │                   │  Central Facial Axis  │
│ (Side oval: 2/3 R)    │                   │  (Rotated 3/4 Yaw θ)  │
└──────────┬────────────┘                   └──────────┬────────────┘
           │                                           │
           └─────────────────────┬─────────────────────┘
                                 ▼
                    ┌─────────────────────────┐
                    │      Loomis Thirds      │
                    │ Hairline → Brow → Nose  │
                    │        → Chin           │
                    └─────────────────────────┘
```

---

## 2. The Loomis Rule of Thirds (Exact Proportions)

> **Implemented by**: these proportions are already resolved on the returned model — `head.unit.thirdH`, `head.unit.eyeW`, and `head.eyeLineY`. Use `Drawing.computeRelativeDistance(headHeight, a, b)` to measure any two landmarks in head-length units rather than pixels.

The vertical face is divided into **three equal segments**, plus the cranial dome:

| Section | Vertical Bounds | Anatomical Landmarks | Canvas Coordinate Formula |
|---|---|---|---|
| **Top Dome** | $Y_{\text{crown}}$ to $Y_{\text{hairline}}$ | Top cranial volume / hair roots | $Y_{\text{hairline}} = Y_{\text{crown}} + 0.35 \cdot R$ |
| **Upper Third** | $Y_{\text{hairline}}$ to $Y_{\text{brow}}$ | Forehead & temples | Height $= H_{\text{third}}$ |
| **Middle Third** | $Y_{\text{brow}}$ to $Y_{\text{nose}}$ | Eyes, eye sockets, nose bridge & apex, ears | Height $= H_{\text{third}}$ |
| **Lower Third** | $Y_{\text{nose}}$ to $Y_{\text{chin}}$ | Upper lip, mouth slit, lower lip, chin ball | Height $= H_{\text{third}}$ |

### Critical Proportions:
- **Eye Line**: The horizontal line through the eyes is located at **exact 1/2 of the total head height** (from crown to chin base).
- **Eye Width & Spacing**:
  - The width of the full face in front view is approximately **5 eye-widths**.
  - The space between the two inner eye corners is **exactly 1 eye-width**.
  - The width of the base of the nose equals **1 eye-width** (aligns with the inner corners of both eyes).
  - The outer corners of the mouth align vertically with the **inner edge of the pupils**.
- **Ear Placement**:
  - In vertical height, the ear spans from the **Brow Line** ($Y_{\text{brow}}$) down to the **Nose Base Line** ($Y_{\text{nose}}$).
  - In 3/4 view, the ear is located at the center-rear quadrant of the temporal oval slice.

---

## 3. Constructing a 3/4 View Head in Canvas2D

> **Implemented by**: `Drawing.createLoomisHead(...)`. The `yawDeg` argument drives the 3/4 turn; `pitchDeg` tilts it.

In a 3/4 view, the head is rotated around the vertical Y-axis by yaw angle $\theta \approx 30^\circ\text{ to }45^\circ$:

`Drawing.createLoomisHead(originX, originY, headHeight, yawDeg, pitchDeg)` performs this construction and returns every landmark below. The yaw term is what produces the 3/4 read: the facial centreline shifts by $\sin\theta$, and far-side features compress by $\cos\theta$.

$$x_{\text{axis}} = x_{\text{center}} + R \sin\theta \cdot 0.35 \qquad w_{\text{eye,far}} = w_{\text{eye,near}} \cdot \cos\theta \cdot 0.85$$

### The Returned `LoomisHead` Landmarks

| Field | Carries | Notes |
| --- | --- | --- |
| `head.unit` | `{ H, W, eyeW, thirdH }` | Every other value derives from these. `eyeW` is the §2 eye-width module. |
| `head.crown`, `head.hairline` | Top dome bounds | The dome above the upper third. |
| `head.brow`, `head.eyeLineY` | Brow line and eye line | `eyeLineY` is at exactly $H/2$, per §2. |
| `head.noseBase`, `head.mouthCenter`, `head.chin` | Lower two thirds | Each third is `unit.thirdH` tall. |
| `head.nearEye`, `head.farEye` | `{ inner, outer, center, width, height }` | Pass straight to `Drawing.drawComicEye(...)`. |
| `head.noseWedge` | `{ bridgeTop, apex, underNose, nearNostril }` | Pass straight to `Drawing.drawComicNose(...)`. |
| `head.mouthGuides` | `{ center, leftCorner, rightCorner, upperLipY, lowerLipY }` | Pass straight to `Drawing.drawComicMouth(...)`. |
| `head.jaw` | `{ ear, angle, chin, cheekApex }` | Ear spans brow→nose vertically, per §2. |
| `head.temporalOval` | `{ cx, cy, rx, ry }` | The flat temple plane sliced off the sphere (§1). |

> Do not recompute these anchors by hand. The feature renderers consume the sub-objects above directly, so a hand-rolled structure with different key names will not draw.

---

## 4. Drawing Facial Features Step-by-Step

> **Implemented by**: `Drawing.drawComicEye(ctx, eyeObj, isFar, options)`, `Drawing.drawComicNose(ctx, noseObj, options)`, and `Drawing.drawComicMouth(ctx, mouthObj, options)` — each consumes the matching sub-object of the `LoomisHead` directly. For an emotional pose, run the head through `Drawing.applyFacialExpression(head, type, intensity)` first (Manual 08 §4).

### A. The Comic Eye (Intense 3/4 Gaze)

`Drawing.drawComicEye(ctx, eyeObj, isFar, options)` renders the whole feature — S-curve upper lid, shaded sclera, iris, pupil, and catchlight — from a `head.nearEye` / `head.farEye` object.

- Pass `isFar: true` for the far eye so it is drawn compressed and with a lighter lid weight.
- `options`: `{ inkColor, irisColor, scleraColor }`.

**The rules the renderer encodes**, worth knowing so you can judge the output:
1. **Upper lid is the heaviest line on the face** — a thick S-curve, thickest at the outer third.
2. **The iris is clipped by the upper lid**, never a free-floating circle.
3. **The catchlight sits opposite the key light** and is the only pure white in the eye.
4. **The far eye loses detail, not just width** — compress it and drop the catchlight rather than drawing a smaller copy.

### B. The Comic Nose (Bridge & Wing in 3/4)
In 3/4 view, the nose projects out from the far cheek silhouette:
1. **Keystone bridge**: Begins at the eye line level.
2. **Nose Ridge Slope**: Slopes forward down to the apex ($Y_{\text{nose}}$).
3. **Under-Nose Base**: Connects apex inward to the septum base with an angled dark shadow plane.
4. **Near Nostril**: Small curved teardrop stroke positioned at $(X_{\text{center}} + \text{width} \cdot 0.35, Y_{\text{nose}})$.

### C. The Determined / Open Comic Mouth
1. **Upper Lip**: M-shaped Cupid's bow line, darker fill or deep shadow line.
2. **Mouth Opening**: Angled wedge revealing white teeth shelf and dark mouth cavity (`#2c0d0d`).
3. **Lower Lip**: Defined by a **subtle shadow crescent** underneath rather than an outline around the whole lip!

---

## 5. Constructing It: A Runnable Head

One construction call, one wireframe pass, then features fed from the model's own sub-objects.

```javascript
// 3/4 view head: Loomis construction → non-repro-blue guides → inked features.
const canvas = createCanvas(560, 640);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f6f4ef';
ctx.fillRect(0, 0, 560, 640);

// §3 — One call resolves the whole construction at a 35° yaw and 4° pitch.
const head = Drawing.createLoomisHead(280, 90, 420, 35, 4);
log('thirdH=' + head.unit.thirdH.toFixed(1) +
    '  eyeW=' + head.unit.eyeW.toFixed(1) +
    '  eyeLineY=' + head.eyeLineY.toFixed(1));

// §1 — Construction pass: cranial sphere, temporal slice, Loomis thirds.
Drawing.drawLoomisWireframe(ctx, head);

// §4 — Features consume the model's sub-objects directly. The far eye is
// passed with isFar = true so it compresses rather than shrinking.
Drawing.drawComicEye(ctx, head.nearEye, false, { irisColor: '#3b6a8a' });
Drawing.drawComicEye(ctx, head.farEye, true, { irisColor: '#3b6a8a' });
Drawing.drawComicNose(ctx, head.noseWedge);
Drawing.drawComicMouth(ctx, head.mouthGuides);

canvas;
```
