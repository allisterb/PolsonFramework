# Studio Manual 01: Head & Facial Construction (The Loomis Method as Code)

> **Purpose**: Provides exact mathematical proportions, geometric construction formulas, and JavaScript Canvas2D algorithms for drawing human and comic heads in 3/4 and dynamic perspective views.

---

## 1. The Core Geometric Concept: Cranium Sphere + Facial Mask

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

In a 3/4 view, the head is rotated around the vertical Y-axis by yaw angle $\theta \approx 30^\circ\text{ to }45^\circ$:

### Mathematical Anchor Point Formulas
```javascript
function computeLoomisHeadAnchors(centerX, centerY, radius, yawAngleDeg = 35, tiltAngleDeg = 5) {
    const rad = (yawAngleDeg * Math.PI) / 180;
    const tilt = (tiltAngleDeg * Math.PI) / 180;

    // 1. Cranial center and third height
    const thirdH = radius * 0.65;
    const halfH = radius * 1.35; // Total head half-height

    // 2. Vertical levels
    const yCrown = centerY - radius;
    const yHairline = centerY - radius * 0.55;
    const yBrow = yHairline + thirdH;
    const yEye = yBrow + thirdH * 0.25; // Eye line slightly below brow
    const yNose = yBrow + thirdH;
    const yMouth = yNose + thirdH * 0.35;
    const yChin = yNose + thirdH;

    // 3. Horizontal 3/4 foreshortening (near side vs far side)
    const turnOffset = Math.sin(rad) * radius * 0.35;
    const xCenterAxis = centerX + turnOffset; // Centerline of face

    // Far features are compressed by cos(rad)
    const farScale = Math.cos(rad);
    const eyeWidthNear = radius * 0.32;
    const eyeWidthFar = eyeWidthNear * farScale * 0.85;

    return {
        crown: { x: centerX, y: yCrown },
        hairline: { x: xCenterAxis, y: yHairline },
        browCenter: { x: xCenterAxis, y: yBrow },
        eyeLineY: yEye,
        noseBase: { x: xCenterAxis, y: yNose },
        mouthCenter: { x: xCenterAxis, y: yMouth },
        chin: { x: xCenterAxis + turnOffset * 0.1, y: yChin },
        
        // Eyes
        nearEye: {
            inner: { x: xCenterAxis + radius * 0.10, y: yEye },
            outer: { x: xCenterAxis + radius * 0.10 + eyeWidthNear, y: yEye - 3 },
            center: { x: xCenterAxis + radius * 0.10 + eyeWidthNear * 0.5, y: yEye }
        },
        farEye: {
            inner: { x: xCenterAxis - radius * 0.10, y: yEye },
            outer: { x: xCenterAxis - radius * 0.10 - eyeWidthFar, y: yEye - 2 },
            center: { x: xCenterAxis - radius * 0.10 - eyeWidthFar * 0.5, y: yEye }
        },

        // Jawline anchors
        ear: { x: centerX - radius * 0.65, y: (yBrow + yNose) / 2 },
        nearJawAngle: { x: centerX - radius * 0.40, y: yNose + thirdH * 0.2 },
        farCheekApex: { x: xCenterAxis - eyeWidthFar - radius * 0.12, y: yEye + 10 }
    };
}
```

---

## 4. Drawing Facial Features Step-by-Step

### A. The Comic Eye (Intense 3/4 Gaze)
```javascript
function drawComicEye(ctx, eye, isFar = false, inkColor = '#0a0a0c', irisColor = '#3b6a8a') {
    const { inner, outer, center } = eye;
    const w = Math.abs(outer.x - inner.x);
    const dir = outer.x > inner.x ? 1 : -1;

    // 1. S-Curve Upper Eyelid (Thick Ink Stroke: 3.5px)
    ctx.beginPath();
    ctx.moveTo(inner.x, inner.y);
    ctx.bezierCurveTo(
        inner.x + dir * w * 0.3, inner.y - w * 0.45,
        inner.x + dir * w * 0.7, inner.y - w * 0.40,
        outer.x, outer.y
    );
    ctx.strokeStyle = inkColor;
    ctx.lineWidth = isFar ? 2.5 : 3.8;
    ctx.lineCap = 'round';
    ctx.stroke();

    // 2. Iris & Pupil (Set under upper lid with top catchlight)
    const irisR = w * 0.32;
    const irisX = center.x + dir * w * 0.08; // slight gaze shift
    const irisY = center.y - 1;

    ctx.save();
    // Clip inside eyelid opening
    ctx.beginPath();
    ctx.moveTo(inner.x, inner.y);
    ctx.quadraticCurveTo(center.x, inner.y - w * 0.45, outer.x, outer.y);
    ctx.quadraticCurveTo(center.x, inner.y + w * 0.25, inner.x, inner.y);
    ctx.clip();

    // Sclera (White with soft top shadow)
    ctx.fillStyle = '#f8f8f4';
    ctx.fill();

    // Iris circle
    ctx.beginPath();
    ctx.arc(irisX, irisY, irisR, 0, Math.PI * 2);
    ctx.fillStyle = irisColor;
    ctx.fill();
    ctx.lineWidth = 1.5;
    ctx.stroke();

    // Dark pupil
    ctx.beginPath();
    ctx.arc(irisX, irisY, irisR * 0.45, 0, Math.PI * 2);
    ctx.fillStyle = '#0a0a0c';
    ctx.fill();

    // White catchlight dot
    ctx.beginPath();
    ctx.arc(irisX - irisR * 0.25, irisY - irisR * 0.25, irisR * 0.22, 0, Math.PI * 2);
    ctx.fillStyle = '#ffffff';
    ctx.fill();

    ctx.restore();

    // 3. Lower Eyelid (Delicate thin stroke: 1.5px)
    ctx.beginPath();
    ctx.moveTo(inner.x + dir * w * 0.2, inner.y + w * 0.15);
    ctx.quadraticCurveTo(center.x, inner.y + w * 0.25, outer.x - dir * w * 0.1, outer.y);
    ctx.strokeStyle = inkColor;
    ctx.lineWidth = 1.6;
    ctx.stroke();
}
```

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
