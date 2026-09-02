# Studio Manual 08: Full-Body Anatomy, Mannequins & Facial Expressions

> **Sources under revision.** This manual previously cited *Imaginative Drawing* (John Guy, 2025).
> That work's terms ask that it be shared only in its entirety, and withhold permission for it to be
> used for machine learning or AI. Distilling it into a manual that is served to agents is against
> both, so the citations have been withdrawn. The constructions below are standard studio practice
> and are being re-sourced; treat any unattributed claim here as pending a citation, not as verified.
> **Purpose**: Translates human anatomical construction (3 primary solid masses, dynamic contrapposto spine curves, volumetric mannequin blocking, upper-torso muscle landmarks, and the 6 universal facial expressions) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. The 8-Head Proportional Canon & 3 Primary Masses

> **Implemented by**: `Drawing.createMannequinFigure(originX, originY, totalHeight, options)` → `MannequinFigure`, which divides `totalHeight` into eight `headUnit`s and applies `shoulderTiltDeg` / `pelvicTiltDeg` as contrapposto. Verify the weight-bearing line with `Drawing.verifyPlumbAlignment(top, bottom, maxTolerance)` and measure in head units with `Drawing.computeRelativeDistance(headHeight, a, b)`.

**The eight-head canon** — the standard heroic-figure convention (Loomis, Hogarth, Richer), and what
`createMannequinFigure` implements. Pending a citation to a specific source:

```
 0.0 H ─── Top of Head (Crown)
 1.0 H ─── Chin & Jaw Base
 2.0 H ─── Nipples & Mid-Chest
 3.0 H ─── Navel & Elbows
 4.0 H ─── Pubic Bone (Crotch) & Wrists ◄── Exact Center of Figure Height!
 5.0 H ─── Mid-Thigh
 6.0 H ─── Bottom of Knees (Patella)
 7.0 H ─── Mid-Calf / Lower Shin
 8.0 H ─── Soles of Feet (Ground Line)
```

### The 3 Solid Masses & Dynamic Contrapposto

> **Principle**:
> The head, ribcage and pelvis are the three major forms of the figure, and establishing them and
> their orientations in perspective is what makes a figure legible in space.

The human torso is NOT a rigid monolith. It consists of those **3 solid masses**, connected by the flexible **Vertebral Column (Spine S-Curve)**:
1. **Head (Solid Egg/Box, $1.0 H$)**: Tilts with cervical neck vertebrae.
2. **Ribcage (Thorax Egg, $1.5 H$)**: Houses heart/lungs, tilts back and laterally.
3. **Pelvis (Solid Basin/Box, $1.0 H$)**: Tilts in opposition to the ribcage (**Contrapposto**), transferring weight onto the active standing leg.

---

## 2. The Volumetric Mannequin Model

> **Implemented by**: `Drawing.drawMannequinWireframe(ctx, figure, options)` for the non-repro-blue gesture pass, then `Drawing.drawMannequinSolid(ctx, figure, options)` for the cranial sphere, ribcage egg, pelvic basin, tapered limb cylinders, and wedge terminals.

> **Principle**:
> Rather than drawing surface contours directly, block the figure with simplified volumetric
> primitives and add detail over them.

Blocked with volumetric primitives rather than surface contours:

- **Torso**: cranial sphere, ribcage egg, pelvic basin.
- **Limbs**: tapered cylinders for upper arms, thighs and calves.
- **Joints**: spherical hinges at shoulders, elbows, hips and knees.
- **Terminals**: wedge boxes for hands and feet.

---

## 3. Upper-Torso Muscle Landmarks

> **Implemented by**: `Drawing.drawTorsoMusculature(ctx, figure, options)` — draws all five landmarks over an existing `MannequinFigure`, so call it after the solid pass, never instead of it.

> **Principle**:
> When detailing the torso over the mannequin foundation, 5 muscle landmarks define the silhouette:
> 1. **Clavicles (Collarbones)**: S-curved handlebars connecting the sternal notch to the shoulder caps.
> 2. **Deltoids (Shoulder Caps)**: Inverted teardrop muscles wrapping around the upper arm.
> 3. **Pectoralis Major (Chest Plates)**: Square/fan-shaped plates inserting directly into the humerus bone.
> 4. **Sternocleidomastoid (Neck V-Tendons)**: Prominent diagonal cords running from the mastoid process behind the ear down to the sternum.
> 5. **Rectus Abdominis (Core 6-Pack Grid)**: Divided into 3 horizontal tiers by tendinous inscriptions.

---

## 4. The 6 Universal Facial Muscle Expressions

> **Implemented by**: `Drawing.applyFacialExpression(head, expressionType, intensity)` → a modified `LoomisHead`. Build the head with `Drawing.createLoomisHead(...)` first, then render the displaced landmarks with `Drawing.drawComicEye(...)` and `Drawing.drawComicMouth(...)`.

> **Principle**:
> All complex emotional expressions decompose into 6 universal muscular activation patterns:
>
> 1. **`"joy"`**: Zygomaticus major contracts $\implies$ mouth corners pull up & out; Orbicularis oculi contracts $\implies$ lower eyelids push up, crinkling crow's feet.
> 2. **`"anger"`**: Corrugator supercilii contracts $\implies$ eyebrows pull sharply down and inward into a fierce V-shape; eyes narrow; mouth squares.
> 3. **`"fear"`**: Frontalis contracts $\implies$ inner and outer eyebrows raise high and flatten; eyes pop wide with upper sclera visible; mouth drops open.
> 4. **`"sadness"`**: Frontalis (medial) contracts while Corrugator relaxes $\implies$ inner eyebrow tips pull up into an inverted peak ($\land$ shape); mouth corners pull down (Depressor anguli oris).
> 5. **`"surprise"`**: Eyebrows arch high in uniform curves; eyes widen in circles; jaw drops open into a relaxed vertical oval.
> 6. **`"disgust"`**: Levator labii superioris contracts $\implies$ upper lip curls upward in a sneer, wrinkling the bridge of the nose; eyebrows lower slightly.

---

## 5. Symbol → SDK Parameter Map

| Concept | SDK parameter or field | Notes |
| --- | --- | --- |
| $H$ (one head unit) | `figure.headUnit` | `totalHeight / 8`. Measure everything in these. |
| $8H$ figure height | `totalHeight` | Argument 3; heroic proportion is $8H$, naturalistic $7.5H$. |
| Ground line ($8.0H$) | `originY + totalHeight` | Feet land here. |
| Contrapposto | `options.shoulderTiltDeg`, `options.pelvicTiltDeg` | Give them **opposite signs** — that opposition *is* contrapposto (§1). |
| Spine S-curve | `options.spineOffset` | Lateral displacement of the column between the masses. |
| Head mass ($1.0H$) | `figure.head` | `{ center, rx, ry }`. |
| Ribcage egg ($1.5H$) | `figure.ribcage` | Carries its own `tiltDeg`. |
| Pelvic basin ($1.0H$) | `figure.pelvis` | `leftHip` / `rightHip` anchor the legs. |
| Crotch ($4.0H$) | `figure.crotch` | The exact vertical midpoint of the figure. |
| Limb chains | `figure.leftArm`, `.rightArm`, `.leftLeg`, `.rightLeg` | Each `{ shoulder\|hip, elbow\|knee, wrist\|ankle, hand\|foot }`. |
| Weight-bearing plumb | `Drawing.verifyPlumbAlignment(top, bottom, tol)` | Sternum must sit over the standing foot. |

---

## 6. Constructing It: A Runnable Figure

Gesture, then volume, then muscle — each pass draws *over* the last. Skipping the wireframe and going straight to musculature is how figures end up anatomically detailed but structurally wrong.

```javascript
// Standing figure in contrapposto: canon → volumes → muscle → plumb check.
const canvas = createCanvas(700, 760);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f6f4ef';
ctx.fillRect(0, 0, 700, 760);

// §1 — Eight head units, ribcage and pelvis tilted in opposition.
const figure = Drawing.createMannequinFigure(350, 60, 640, {
    shoulderTiltDeg: -8,
    pelvicTiltDeg: 6,
    spineOffset: 10
});
log('One head unit = ' + figure.headUnit.toFixed(1) + 'px; crotch sits at 4.0H.');

// §2 — Gesture pass in non-repro blue, then the solid volumetric masses.
Drawing.drawMannequinWireframe(ctx, figure, { lineWidth: 1.2 });
Drawing.drawMannequinSolid(ctx, figure, {
    fillColor: '#d8cec1',
    shadowColor: '#9b8f7f',
    strokeColor: '#2b2b2b'
});

// §3 — The five landmarks go over the solid pass, never instead of it.
Drawing.drawTorsoMusculature(ctx, figure, { strokeColor: '#7a6a58', strokeWidth: 1.1 });

// §1 — Contrapposto only reads if the weight line runs plumb to the standing foot.
const plumb = Drawing.verifyPlumbAlignment(figure.sternum, figure.rightLeg.ankle, 14);
log(plumb.message);

canvas;
```

### Driving an expression

`Drawing.applyFacialExpression(...)` returns a *modified copy* of a `LoomisHead` — the landmarks move, so render from the returned object, not the original.

```javascript
// §4 — One head, two emotional states, from the same construction.
const canvas = createCanvas(720, 380);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f6f4ef';
ctx.fillRect(0, 0, 720, 380);

for (const [index, emotion] of [[0, 'joy'], [1, 'anger']]) {
    const head = Drawing.createLoomisHead(140 + (index * 340), 60, 240, 20, 0);
    const posed = Drawing.applyFacialExpression(head, emotion, 1.0);

    Drawing.drawLoomisWireframe(ctx, posed);
    Drawing.drawComicEye(ctx, posed.nearEye, false);
    Drawing.drawComicEye(ctx, posed.farEye, true);
    Drawing.drawComicNose(ctx, posed.noseWedge);
    Drawing.drawComicMouth(ctx, posed.mouthGuides);
    log('Rendered expression: ' + emotion);
}

canvas;
```
