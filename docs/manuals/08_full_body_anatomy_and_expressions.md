# Studio Manual 08: Full-Body Anatomy, Mannequins & Facial Expressions

> **Source Reference**: Andrew Loomis, *Figure Drawing for All It's Worth* (Viking Press, 1943) —
> §1 from p. 26 and p. 33, §2 from pp. 38–40. Jack Faragasso, *Mastering Drawing the Human Figure*
> (Stargarden Press, 1998) — §3 from pp. 74 and 93–94. Andrew Loomis, *Drawing the Head and Hands*
> (Viking Press, 1956) — §4 from pp. 45–47 and 51, Plates 20–22 and 27–28. Michael Hampton,
> *Figure Drawing: Design and Invention* (2009) — §3a's gesture/shape/volume method and the
> active/passive rule, from "Anatomy and Motion" and the Anatomy chapter. **All sections
> are cited.**  
> **Purpose**: Translates human anatomical construction (3 primary solid masses, dynamic contrapposto spine curves, volumetric mannequin blocking, upper-torso muscle landmarks, and facial expression built from the muscles that make it) into algorithmic JavaScript Canvas2D / Skia code.

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

### 2a. The mannequin drawn, and the mannequin as geometry

The two drawers above render a **construction sheet**: separate masses with their own outlines, which
is what a mannequin is for. A *figure* is one shape, and that is a different thing —
`Drawing.createFigureGeometry(figure, options)` hands the same masses back as paths instead of
painting them.

```javascript
const canvas = createCanvas(520, 520);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f1e8'; ctx.fillRect(0, 0, 520, 520);

const pose = { spineDeg: 22, neckDeg: -8,
    rightArm: { shoulderDeg: -42, elbowDeg: 36 }, leftArm: { shoulderDeg: 154, elbowDeg: -58 },
    rightLeg: { hipDeg: 56, kneeDeg: 38 }, leftLeg: { hipDeg: 124, kneeDeg: -16 } };
const panel = Layout.inset(Layout.rect(0, 0, 520, 520), 30);

// Size to the extent, never to the height. This lunge measures 938 x 954 where the same figure
// standing measures 345 x 1013 — a thrown arm reaches far wider than the canon ever does. One
// trial build at a nominal height gives the answer exactly, because scaling is linear about the
// origin.
const e = Drawing.createMannequinFigure(0, 0, 1000, { pose: pose }).bounds;
const s = Math.min(panel.width / e.width, panel.height / e.height);
const fig = Drawing.createMannequinFigure(
    panel.x - e.x * s, panel.y - e.y * s, 1000 * s, { pose: pose });
log('standing ' + Drawing.createMannequinFigure(0, 0, 1000).bounds.width.toFixed(0)
  + 'px wide; this pose ' + e.width.toFixed(0) + 'px');

const geo = Drawing.createFigureGeometry(fig);
ctx.fillStyle = '#e9dcc4';
ctx.fill(geo.silhouette);          // one contour: no seams, no holes at the joints
ctx.strokeStyle = '#15151a'; ctx.lineWidth = 2;
ctx.stroke(geo.silhouette);        // and one contour to ink, which is what Manual 03 §1 weights

// The coarse groups are what occlusion clips to.
ctx.save();
ctx.clip(geo.groups.rightArm);
ctx.fillStyle = 'rgba(190,70,50,0.5)';
ctx.fillRect(0, 0, 520, 520);
ctx.restore();
canvas;
```

> [!TIP]
> **`padding` inflates every mass, which is the whole of §1 of Manual 22**: cloth covers the figure
> without fitting it, and that gap is where every fold comes from. A sleeve is therefore the padded
> arm group — derived from the arm rather than built beside it, so it cannot drift out of step with
> the pose.
>
> ```javascript
> const cloth = Drawing.createFigureGeometry(fig, { padding: fig.headUnit * 0.08 });
> ctx.fill(cloth.groups.leftArm);                        // a sleeve, already in the right place
> ctx.fill(cloth.silhouette.subtract(geo.silhouette));   // just the cloth, as its own shape
> ```

> [!IMPORTANT]
> **The pose orients the masses, and the figure says by how much.** `figure.head.angleDeg` is
> `spineDeg + neckDeg` and `figure.ribcage.tiltDeg` is `shoulderTiltDeg + spineDeg`;
> `figure.pelvis.tiltDeg` is the pelvic tilt alone, because the pelvis is the pivot the spine leans
> over. Read those rather than recomputing them from a pose object you may no longer hold — features
> drawn onto the head by hand need exactly this angle, or a leaning figure keeps an upright face.
>
> Two limits worth knowing: the canon's `foot` landmark is a short stub, so the silhouette ends at
> the ankle rather than on a foot; and `geo.order` is the order `drawMannequinSolid` paints in,
> **not depth** — the toolkit has no z, so an arm passing behind the torso is still yours to stage.

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
> Five landmarks define the torso's silhouette over the mannequin foundation: the **clavicles**, the
> **deltoids**, the **pectoralis major**, the **sternocleidomastoid**, and the **rectus abdominis**.
> §3a takes each of them through Hampton's three passes, which is a more useful way to hold them than
> a list of shapes.

---

---

## 3a. Gesture, Shape, Volume — Three Passes Per Muscle

> **Implemented by**: `Drawing.drawTorsoMusculature(ctx, figure, options)` draws all five, and reads
> the active side off the figure's own `shoulderTiltDeg` — see the squash rule below. The mannequin it
> draws over comes from `Drawing.createMannequinFigure(...)`.

> **Source**: Michael Hampton, *Figure Drawing: Design and Invention* (2009) — "Anatomy and Motion"
> for the governing rule, then one gesture/shape/volume trio per muscle through the Anatomy chapter.

**Hampton studies every muscle three times, in a fixed order, and the order is the method:**

| Pass | The question it answers | What you draw |
| :--- | :--- | :--- |
| **Gesture** | What does this muscle *do*? | A single line — a C or an S — plus where it starts and ends |
| **Shape** | What does it look like, simplified? | One memorable silhouette you could name |
| **Volume** | How does it sit in space, and how does the action change it? | The form wrapped in perspective |

The sequence matters because each pass constrains the next. **The muscle's action decides whether its
gesture is a C or an S** — Hampton says outright that the description of what the muscle does is what
you look for when deciding between them — and the shape is then that gesture given a body, and the
volume is that shape given depth. Reversed, you get a correctly-shaped muscle doing nothing.

### The rule that governs all of them

> **"S" curve = stretch, or a passive anatomical shape. "C" curve = pinch, or an active one.**

An **active** shape takes its basic design and shows it **squashed** — more extreme action, more
exaggerated compression. A **passive** shape is **stretched**, elongated. Hampton's argument for it is
not decorative: it is what keeps the believable **asymmetry** in a drawing, and it is how the mechanics
of the body get described rather than merely depicted.

> [!IMPORTANT]
> **This is the same C/S vocabulary as Manual 05 §3, applied to anatomy instead of contour** — one
> author, one system, used at two scales. The line you choose for a muscle is the same decision as the
> line you choose for a silhouette, and it is made from the pose either way.
>
> **The toolkit reads the active side off the pose rather than asking for it.** The shoulder line tilts
> down toward the closed side, so `drawTorsoMusculature` derives a compression factor from the
> figure's own clavicle heights and applies it to the pectorals, the abdominal rows and the
> sternomastoid. Set `shoulderTiltDeg` on the mannequin and the musculature follows;
> `TestTorsoMusculatureRespondsToTheShoulderTilt` fails if it stops following.

### The five, through the three passes

**Sternocleidomastoid**

- *Gesture* — pulls the head and neck forward and rotates the head laterally. Runs from the interior
  of the manubrium and clavicle up to the skull **behind the ear**.
- *Shape* — a **baseball bat**, set on a diagonal from the manubrium to the base of the skull. Hampton
  is explicit that it must **not** be drawn symmetrically: *one side of the shape is always higher*.
- *Volume* — wraps around the **cylinder of the neck** while moving back in space, which is what states
  the distance from manubrium to skull base.

**Pectoralis major**

- *Gesture* — pulls the arm forward across the chest and rotates it medially. Origin along the medial
  half of the clavicle, the sternum, and the cartilages of the first six or seven ribs; inserts at the
  bicipital groove on the front of the humerus.
- *Shape* — a **fan** of overlapping clavicular, sternocostal and abdominal sections, or as Hampton
  puts it more usefully, **a goldfish with its head missing**: the flat cut sits along the sternum, the
  tail wraps forward to the humerus.
- *Volume* — a **small box sitting on the rib cage**, widest low down near the nipple. **Arm raised:**
  the volume spreads evenly and the corner softens, the tail unwrapping and elongating. **Flexed:** it
  peaks, and the width becomes more noticeable.

**Deltoid**

- *Gesture* — three heads doing three jobs: anterior raises the arm forward, acromial pulls it away
  from the body, posterior pulls it back. Its origin is one continuous line along the last third of the
  clavicle, the acromion, and the lower edge of the spine of the scapula; it inserts on the outside of
  the humerus **about halfway down the upper arm**.
- *Shape* — from the side, an **upside-down triangle**; from front or back, the same triangle much
  thinner.
- *Volume* — wrap the **insertion point in the same perspective as the direction of the arm**, and let
  the origin reflect the perspective of the upper body as it pulls away from the shoulder girdle.

**Rectus abdominis**

- *Gesture* — flexes the trunk at the lumbar vertebrae. From the base of the pubic bone up into the
  surfaces of the fifth, sixth and seventh ribs.
- *Shape* — a **bullet**: the curved end fits into the pelvis, the flat end lies along the ribs above
  the thoracic arch. **Eight sections**, not six — and the row at the navel is the **straight** one,
  with the rows above it progressively **rising to a peak**. As the trunk moves it pinches, stretches,
  or aids a twist.
- *Volume* — a **very thin side plane** is what states its depth; the whole group resolves to a
  flattened box.

**Clavicles** — Faragasso's warning in §3 still governs: they are *seldom horizontal*, so the shoulder
axis and the collarbones are two different lines and must stay separate.

> [!NOTE]
> **Flat tiers are a six-pack with no gesture in them.** The abdominal rows are eight sections, not
> six, and only the row at the navel is straight — each row above it bows further. That, and the
> presence of the deltoids and sternomastoid at all, is what
> `TestTorsoMusculatureReachesTheShouldersAndTheNeck` pins.

> [!TIP]
> **The three passes are a schedule for a multi-stage run, not just a way of studying.** A gesture pass
> is one line per muscle and costs almost nothing to render; a shape pass commits to silhouettes; a
> volume pass is where the drawing gets expensive. Running them as three stages — with
> `Stage.begin('Gesture')` and a cheap draft render at each — is how a figure gets checked before it
> gets costly. Manual 18 covers staging; Manual 05 §5 covers drafting small.

---

## 4. Facial Expression — Muscles, Not a Taxonomy of Emotions

> **Implemented by**: `Drawing.applyActionUnits(head, weights)` — **the call to reach for** — and `Drawing.applyFacialExpression(head, expressionType, intensity)`, which predates it. Build the head with `Drawing.createLoomisHead(...)` first, then render the displaced landmarks with `Drawing.drawComicEye(...)` and `Drawing.drawComicMouth(...)`.

> **Source Reference**: Andrew Loomis, *Drawing the Head and Hands* (Viking Press, 1956), pp. 45–47 and Plate 21, for the mechanics; Paul Ekman & Wallace V. Friesen, *Measuring Facial Movement*, Environmental Psychology and Nonverbal Behavior 1(1):56–75, 1976, Table 1, for the numbering and the muscle names.

### The muscle layer, and why it is the one to use

**`applyActionUnits` takes muscles and the six presets take feelings, which is the whole argument of this section made into an API.** Loomis sets the emotions aside as *too numerous to tabulate* and gives the muscle groups instead; Ekman & Friesen, measuring rather than drawing, arrive at the same anatomy and number it. Two schools, two vocabularies, one set of muscles.

```js
const worried = Drawing.applyActionUnits(head, { AU1: 0.7, AU4: 0.4, AU15: 0.6 });
```

Units are **additive and order-independent**, so a handful covers a wide range of faces without a preset per combination — which is exactly Loomis's point about the emotions being uncountable while the muscles are not. Seven are implemented; `polson://sdk/core/Drawing` lists them, and lists what is absent and why, because the set is bounded by what this construction carries landmarks for rather than by the coding system.

> **Neither source supplies a magnitude.** Loomis gives directions and a relaxed/contracted table; the 1976 code scores **presence** — slight against strong — rather than a continuous intensity. The displacement numbers are the studio's, tuned by eye, and **a published table of emotion-to-unit weights is claiming more than either source says**. Tune them against a render rather than trusting a decimal.

> **Principle**:
> The six presets are **Action Unit tuples**, and `Drawing.expressionUnits(name)` hands you the tuple
> so you can read it, change a unit and pass it on. What each is doing, in muscle terms:
>
> 1. **`"joy"`**: Zygomaticus major contracts $\implies$ mouth corners pull up & out; Orbicularis oculi contracts $\implies$ lower eyelids push up, crinkling crow's feet.
> 2. **`"anger"`**: Corrugator supercilii contracts $\implies$ eyebrows pull sharply down and inward into a fierce V-shape; eyes narrow; mouth squares.
> 3. **`"fear"`**: Frontalis contracts $\implies$ inner and outer eyebrows raise high and flatten; eyes pop wide with upper sclera visible; mouth drops open.
> 4. **`"sadness"`**: Frontalis (medial) contracts while Corrugator relaxes $\implies$ inner eyebrow tips pull up into an inverted peak ($\land$ shape); mouth corners pull down (Depressor anguli oris).
> 5. **`"surprise"`**: Eyebrows arch high in uniform curves; eyes widen in circles; jaw drops open into a relaxed vertical oval.
> 6. **`"disgust"`**: Levator labii superioris contracts $\implies$ upper lip curls upward in a sneer, wrinkling the bridge of the nose; eyebrows lower slightly.

### What the six actually resolve to, and how far to trust each

| preset | units | how well served |
| :--- | :--- | :--- |
| `joy` | `AU12` 0.85, `AU7` 0.25 | **well** — Zygomatic Major is implemented and the mouth is where it lives |
| `sadness` | `AU1` 0.70, `AU15` 0.75 | **well** — Triangularis and the inner-brow lift both reachable |
| `anger` | `AU4` 0.90, `AU7` 0.60, `AU15` 0.25 | good at the brow; Loomis's *squaring* mouth has no unit, so AU15 stands in |
| `fear` | `AU1` 0.80, `AU5` 0.80, `AU26` 0.45 | differs from `surprise` **only in amount** — see below |
| `surprise` | `AU1` 0.95, `AU5` 0.65, `AU26` 0.75 | as above |
| `disgust` | `AU4` 0.40, `AU7` 0.45, `AU15` 0.50 | **a placeholder** — its real action is AU9/AU10, unimplemented |

**These weights are the studio's and no source supplies them.** Loomis gives directions and a
relaxed/contracted table; Ekman & Friesen score presence rather than amount. Published
emotion-to-unit tables are somebody's interpretation. Tune against a render, not against a decimal.

> **Two of the six are limited by one missing landmark, and it is the same one.** The head carries a
> single `brow` centre point. So **`fear` and `surprise` are nearly the same face**, because what
> separates them in life is AU4 knitting an already-raised brow; and **`sadness` names AU1 without
> AU4**, because the canonical oblique sad brow is both at once and on one landmark they cancel
> exactly. Giving `createLoomisHead` inner and outer brow stations fixes both at a stroke, and is the
> obvious next increment on this section.

> **The defect this replaced is worth keeping, because it survived everything.** Until 2026-09-18 the
> presets displaced landmarks directly, and `'sadness'` — described in this manual as lifting the
> inner brow **and** dropping the mouth corners — moved only the mouth. A live run asked for it
> *"at 0.22 for the inner-brow lift only"*, wrote a careful note about the intensity above which that
> lift would become a grimace, and got about two and a half pixels of a movement it had not wanted.
> The manual was right, the agent reasoned correctly, the render was fine. **Nothing compared the
> name to the displacement**, and for a face that is all it takes.

### Loomis works from muscles, and declines to tabulate the emotions

> **Source**: Andrew Loomis, *Drawing the Head and Hands* (Viking Press, 1956) — pp. 45–47 and 51;
> Plates 20 ("Anatomy of the head"), 21 ("How the muscles function"), 27 ("Expression — the laugh")
> and 28 ("Various expressions").

> [!IMPORTANT]
> **The six above are the SDK's enumeration, not a claim about the world.** Reading them as *six
> universal patterns* would be asserting Ekman's basic-emotion theory as settled, and it is
> contested. Loomis takes the opposite approach explicitly: **setting aside the
> psychological and emotional phases of expression**, he gives the technical mechanics, and lists
> guilty, ashamed, frightened, content, angry, smug, confident, frustrated "and a host of other ways
> **too numerous to tabulate**." What is finite here is the muscles, not the emotions.
>
> So read `'joy'`, `'anger'`, `'fear'`, `'sadness'`, `'surprise'` and `'disgust'` as the six presets
> `Drawing.applyFacialExpression(...)` happens to ship, each a named landmark displacement. A useful
> set; not a basis.

**Two antagonist groups do most of the work**, with a handful of wrinkle muscles for the rest.
Loomis's instruction is to hold the first two as a pair, because they are the basis of most facial
expressions:

| Group | Attachment | Action |
| :--- | :--- | :--- |
| **"Happy muscles"** | cheekbones, running diagonally down the cheeks to the muscles around the lips | pull the mouth corners **out and diagonally upward**; puff the cheeks by contracting within the flesh |
| **"Unhappy muscles"** | bone beside the nose at one end, the jaw at the other, passing the mouth corners | pull the lips **up into a snarl or down into a leer**; working from both ends they bare the teeth; also pull the inside corner of the brow down into a frown |
| **"Wrinkle muscles"** | inside corner of the brows near the nose; two above the brows; two at the point of the chin | lift the inner brow corner (worry, pleading); wrinkle the forehead; buckle the chin into humps, and dimple it |

**The mouth corner is the primary variable, and its *shape* is the distinction.** Loomis separates a
**sharp-cornered smile** from a **round-cornered laugh**, and names the failure mode plainly: *a round
corner badly drawn can easily become a leer.* That is a geometric decision an algorithm can hold — the
corner is either an angular junction pulled out and up, or a curve — and it is the one most worth
getting right, because **the basis of most expressions is usually in the mouth**. The unhappy muscles
make round corners; the smile pulls them out and upward.

**Most of the rest is consequence rather than an independent control**, which is what makes it
implementable as an ordered pass:

1. The happy muscles pull, so the cheeks bulge.
2. The bulging cheek flesh buckles at the eye corners — **that is what crow's feet are**, not a
   separate wrinkle to be drawn on its own.
3. The same bulge raises the fold of flesh under the eye in a smile, more pronounced on some faces.
4. The nostrils flare a little, which Loomis counts among *the things that help to make a face smile*.
5. The dimple in the lower part of the smiling cheek is the small open space between the unhappy
   muscle and the jaw muscle — a young face's dimple and an old face's depression are the same
   feature at different ages.

A smile is therefore one pull with four consequences, and drawing the consequences without the pull
is what produces a face that reads as assembled from parts.

> [!NOTE]
> **Plate 21 is a per-muscle state table, and it is the shape this API should have had.** Loomis
> captions it with four states — **worry, frown, laugh, anger** — and marks each muscle *relaxed* or
> *contracted* under each. That is a sparse matrix of muscles against expressions, not six
> independent landmark recipes, and it composes where the six presets do not: worry and a frown share
> the brow and differ at its inner corner, so they should be expressible together.
> `Drawing.applyFacialExpression(...)` takes one name and one intensity, so today they cannot be.
> **No muscle-level call exists yet — do not write one into a script expecting it to resolve.**

> [!TIP]
> **Loomis's own method for getting these right is a mirror**, and the studio equivalent is a
> specimen sheet: render the same head at one size across all six presets and several intensities in
> a single frame, and compare them side by side rather than one per run. `Layout.grid(...)` lays the
> cells out, and one `Drawing.createLoomisHead(...)` reused across them keeps everything but the
> expression constant — which is the only way a difference between two of them means anything.

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
