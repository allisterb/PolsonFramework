# Studio Manual 08: Full-Body Anatomy, Mannequins & Facial Expressions

> **Source Reference**: Andrew Loomis, *Figure Drawing for All It's Worth* (Viking Press, 1943) —
> §1 from p. 26 and p. 33, §2 from pp. 38–40. Jack Faragasso, *Mastering Drawing the Human Figure*
> (Stargarden Press, 1998) — §3 from pp. 74 and 93–94. **§4 is not yet re-sourced**: its previous
> citation was withdrawn, so treat anything there as pending a citation rather than as verified.  
> **Purpose**: Translates human anatomical construction (3 primary solid masses, dynamic contrapposto spine curves, volumetric mannequin blocking, upper-torso muscle landmarks, and the 6 universal facial expressions) into algorithmic JavaScript Canvas2D / Skia code.

## Two schools, and neither is the answer

There is no single correct way to construct a figure, and this manual does not pretend otherwise. It
carries **two teaching traditions** that disagree in useful ways, and the SDK is built so you can work
in either. Choose per drawing; you can also take the proportions from one and the torso construction
from the other, which is what most working artists actually do.

| | **Loomis** — *Figure Drawing for All It's Worth* (1943) | **Reilly**, via Faragasso — *Mastering Drawing the Human Figure* (1998) |
| :--- | :--- | :--- |
| **Starts from** | proportion. Divide the height into eighths, then hang the forms on it. | relationships and structure lines. Establish axes and how landmarks line up, then build forms between them. |
| **The figure is** | a mannequin of simple naturalistic masses — ribcage egg, pelvic basin, limb cylinders. | an upper and a lower **secondary form** meeting at the waist, each built from the pit of the neck outward. |
| **Shoulder width** | 2⅓ H at the widest, ~2 H for the "cape" | 2⅔ H, measured from the pit of the neck |
| **Best for** | getting a whole figure right quickly, and for heroic or idealised proportion. | precision where a pose has to hold up — foreshortening, difficult angles, careful anatomy. |
| **In the SDK** | `createMannequinFigure` + `drawMannequinWireframe` / `drawMannequinSolid` (§§1–2) | `drawTorsoMusculature` over that foundation, and the landmark order in §3 |

Where they give different numbers, the SDK takes a parameter rather than picking a winner —
`options.shoulderSpanHeads` is the worked example, and §1 lists what each canon puts there.

Loomis's own caution is worth carrying into either: **"Never draw the limbs straight and stiff and
without spring."** Faragasso's is the same warning from the other side — proportions *vary greatly in
real life*, so a canon is a starting position, not a specification.

---

## 1. The 8-Head Proportional Canon & 3 Primary Masses

> **Implemented by**: `Drawing.createMannequinFigure(originX, originY, totalHeight, options)` → `MannequinFigure`, which divides `totalHeight` into eight `headUnit`s and applies `shoulderTiltDeg` / `pelvicTiltDeg` as contrapposto. Verify the weight-bearing line with `Drawing.verifyPlumbAlignment(top, bottom, maxTolerance)` and measure in head units with `Drawing.computeRelativeDistance(headHeight, a, b)`.

> **Source**: Andrew Loomis, *Figure Drawing for All It's Worth* (Viking Press, 1943), p. 26 —
> "Ideal Proportion, Male", with p. 33 "Proportions by Arcs and Head Units".

Take the height you want, mark the crown and the heels, and **divide into eighths**. Loomis counts
from the heels up; the toolkit measures down from the crown, which is the same canon read from the
other end:

```
 0.0 H ─── Top of Head (Crown)                        Loomis: 8
 1.0 H ─── Chin & Jaw Base                                     7
 1⅓ H ─── Shoulders  (one-sixth of the way down)              6⅔
 2.0 H ─── Nipples & Mid-Chest                                 6
 3.0 H ─── Navel & Elbows                                      5
 3⅓ H ─── Hips                                                4⅔
 4.0 H ─── Crotch & Wrists ◄── exact centre of the figure      4
 4⅓ H ─── Bottom of Buttocks                                  3⅔
 6.0 H ─── Bottom of Knees (just above the lower quarter)      2
 8.0 H ─── Soles of Feet (Ground Line)                         0
```

**Widths, which the table above cannot carry** and which the canon is incomplete without:

- The male figure is **2⅓ head units wide** at its widest.
- The space **between the nipples is exactly one head unit**.
- The waist is a little wider than one head unit.
- The wrist drops *just below* the crotch line; the elbows sit *on* the navel line.

> [!TIP]
> Loomis gives the same proportions **in feet** as well as head units, expressly so a figure can be
> related to furniture and interiors — a 6 ft figure has a 9 in head and its shoulders at 5 ft. That is
> the same problem Manual 06 §5a solves with a metre-based projector, and the two agree: pick the
> real-world height first, and let both the figure and the architecture derive from it.

> [!TIP]
> **Shoulder width is a choice between canons, and `options.shoulderSpanHeads` is how you make it.**
> The number is in head units, so it survives any figure height. The sources disagree, and each is
> measuring a slightly different thing — none is wrong:
>
> | Canon | `shoulderSpanHeads` | What it measures |
> | :--- | :--- | :--- |
> | **Loomis**, *Figure Drawing* p. 26 | `2.33` | the figure at its widest, arms hanging |
> | **Loomis**, p. 40 | `2.0` | the shoulder "cape" over the ball of the chest |
> | **Faragasso / Reilly**, p. 74 | `2.67` | pit of the neck to each shoulder, ×2, male figure |
> | *toolkit default* | `1.8` | a shoulder-*joint* span, narrower than any of them |
>
> ```javascript
> // A heroic figure on Loomis's full width.
> const figure = Drawing.createMannequinFigure(350, 60, 640, { shoulderSpanHeads: 2.33 });
> ```
>
> Faragasso's is the widest and most specific, and he immediately adds that **these proportions vary
> greatly in real life** — which is the honest note to end on. Pick the canon that suits the figure
> you are drawing; a slight build and a heavyweight boxer are not the same number, and neither is
> wrong.

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

> **Source**: Loomis, *Figure Drawing for All It's Worth*, pp. 38–40 — "First the Mannikin Frame",
> "Movement in the Mannikin Frame", "Details of the Mannikin Frame".

The frame is built **on the proportion line of §1**, not invented beside it: head, shoulders, nipple,
navel, crotch, bottom of knees, heels are the same marks. Loomis calls what follows a simplified
version of the real frame and says it is all you need to start.

- **Torso**: cranial sphere, ribcage egg, pelvic basin. The shoulder girdle is a **"cape"** laid over
  the ball of the chest, spanning about two head units — it is not a bar between two joints, and
  drawing it as one is why mechanical mannequins look like coat hangers.
- **Pelvis**: two discs is enough at this stage.
- **Limbs**: tapered cylinders for upper arms, thighs and calves.
- **Joints**: spherical hinges at shoulders, elbows, hips and knees.
- **Terminals**: wedge boxes for hands and feet.

> [!IMPORTANT]
> **"Never draw the limbs straight and stiff and without spring."** Loomis's instruction, and it is
> the single most useful sentence here: the legs are *curved*, and a limb drawn as a straight segment
> between two joints reads as dead however correct its proportions are. Whatever poses the figure has
> to bend limbs rather than only rotate them about their joints.
>
> He also drills the frame in **five views — front, back, ¾ back, side, ¾ front** — which is the right
> shape for a construction-sheet test: a figure that only reads from the front has not been built in
> three dimensions.

---

## 3. Upper-Torso Muscle Landmarks

> **Implemented by**: `Drawing.drawTorsoMusculature(ctx, figure, options)` — draws all five landmarks over an existing `MannequinFigure`, so call it after the solid pass, never instead of it.

> **Source**: Jack Faragasso, *Mastering Drawing the Human Figure* (Stargarden Press, 1998) —
> "Additions and Clarifications of the Structure System: The Head and Shoulders" p. 74, and
> "Planes of the Torso" pp. 93–94. Faragasso teaches the Frank J. Reilly method.

**The shoulder line is an axis, not the clavicles.** It runs through the **pit of the neck** to the
widest points of the shoulders. Faragasso is explicit that it only sometimes follows the clavicles,
because *clavicles are seldom horizontal* — so a construction that draws the shoulder line along the
collarbones will tilt when it should not, and the two are worth keeping separate.

**Building the torso from the pit of the neck outward**, which is the order that keeps it coherent:

1. The neck hole and the **oval of the rib cage** are drawn inside an **inverted triangle**.
2. A dot on the centre line at the **pit of the neck** and another at the **bottom of the rib cage**
   gives the *upper secondary form*.
3. Connect the hips: a line from the widest point of the hip, through the widest point of the waist,
   arching over to the opposite waist and hip. That is the *lower secondary form*.
4. A **semicircle starting at the navel** is the top of the pelvis form; the two ends of it mark the
   **iliac crest**.
5. The **nipples** sit on the lines running from the neck to the widest points of the hips.
6. The first big **side planes** come from connecting the dots at the widest points of the torso.

The two secondary forms are the useful idea for us: the torso is not one mass but an upper and a
lower one meeting at the waist, which is what lets it bend and twist without the parts sliding apart.

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
