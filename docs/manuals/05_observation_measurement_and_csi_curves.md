# Studio Manual 05: Observation, Measurement & CSI Curves

> **Source Reference**: Andrew Loomis, *Figure Drawing for All It's Worth* (Viking Press, 1943) — §1
> from pp. 35–37, §2 from pp. 34–37, §3's account of the *edge* from p. 24; Michael Hampton,
> *Figure Drawing: Design and Invention* (2009) — §3's C/straight/S vocabulary and the wrapping
> line, from "Gesture Drawing"; and Andrew Loomis,
> *Successful Drawing* (Viking Press, 1951) — §4's plane sequence and §5's pattern/placement
> distinction from p. 13, with §5's four-value exercise from *The Eye of the Painter* (Viking
> Press, 1961), pp. 39–40. **All five sections are now cited.**
>
> **The C/S/straight vocabulary is Michael Hampton's**, from *Figure Drawing: Design and Invention*
> (2009). The acronym is ours; the three lines and the discipline of using only them are his. Loomis's
> account of the *edge* stands alongside it, because the two answer different questions.
> **Purpose**: Translates foundational drawing techniques (relative distance measurement, plumb lines, the CSI curve grammar, planar forms, and two/three-value light studies) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Relative Distances & The Unit System

> **Implemented by**: `Drawing.createLoomisHead(...)` returns the unit system as `head.unit`, and `Drawing.computeRelativeDistance(headHeight, pointA, pointB)` measures in head-lengths.

> **Principle**: In drawing, artists do NOT memorize absolute global pixel coordinates. They choose a **single fundamental unit of measure** (the **Head Length**, $H_{\text{head}} = Y_{\text{chin}} - Y_{\text{crown}}$) and derive every other distance as a relative proportion.

> **Source**: Loomis, *Figure Drawing for All It's Worth*, pp. 35–37 — "The John and Mary Problems",
> "Finding Proportion at Any Spot in Your Picture", "'Hanging' Figures on the Horizon".
>
> **A picture has one horizon and one station point.** The horizon *is* the eye or lens level of the
> observer, so it rises and falls with them — stand and it rises, lie down and it drops, get beneath
> the subject and it drops below them. You cannot see over it. On open ground or water it is visible;
> among hills or indoors it usually is not, but your eye level fixes it just the same.

> [!IMPORTANT]
> **One horizon per picture is a checkable invariant, and the cheapest structural bug to catch.** Every
> figure and every piece of architecture in a scene must be measured against the *same* `horizonY`. Two
> `createPerspectiveGrid` calls with different horizons in one image is not a style choice — it is two
> observers in one picture, and no amount of redrawing will make it settle.
>
> This is the same fact Manual 06 §5a states in metres (`CAM.eyeHeight` and `horizonY` are one fact in
> two units) and Manual 05 §2 states as a check (the horizon must cut similar figures at the same
> place). Three views of one constraint, which is why it is worth stating three times.

> [!NOTE]
> Loomis sends the reader to Norling for the perspective itself — *"If you do not understand
> perspective, there is a good book on the subject, `Perspective Made Easy`, available at most
> booksellers"* (p. 36). The two sources this studio draws on cite each other, which is a reassuring
> sign that the figure and perspective halves of these manuals are not being stitched together from
> incompatible traditions.

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

> **Source**: Andrew Loomis, *Figure Drawing for All It's Worth* (Viking Press, 1943) — "Proportion in
> Relation to the Horizon" p. 34, "Finding Proportion at Any Spot in Your Picture" p. 36, and
> "'Hanging' Figures on the Horizon" p. 37.

> [!IMPORTANT]
> **The horizon is a level line you already have, and it checks every figure in the scene at once.**
> Loomis's rule: *hang* the figures on the horizon by making it cut through similar figures **at the
> same place on each**. That is what keeps them on one ground plane — the horizon crosses standing men
> at the waist, a seated woman at the chin, and a standing woman placed relative to the men at her own
> matching point.
>
> It is a **verification** as much as a construction, and a cheap one: two standing adults on the same
> floor whose waists sit at different heights relative to the horizon are not standing on the same
> floor, whatever the rest of the drawing says. Unlike a plumb line, which checks one figure's internal
> alignment, this checks the whole cast against each other and against the ground.
>
> Combined with Manual 06 §5a: the horizon *is* eye height, so "cuts a standing adult at the waist"
> also tells you the camera is at waist height — a fact about the scene recoverable from any correct
> figure in it.

> **Principle**: Dropping vertical plumb lines and horizontal level lines allows artists to check anatomical alignment without perspective distortion:
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

> **Principle**: Complex organic contours must be distilled into three elemental line primitives:
> 1. **C-Curves**: Single continuous arc in one direction.
> 2. **S-Curves**: Reversing dynamic curves with opposing inflection points.
> 3. **Straights ("I-Lines")**: Stable structural lines.

> **Source**: Michael Hampton, *Figure Drawing: Design and Invention* (2009) — "Gesture Drawing".
> Hampton states it as a closed set and means it: **the lines most crucial to showing a figure are the
> "C" curve, the straight, and the "S" curve**, and *in this drawing process, you will never use any
> other type of line*. That constraint is the point — three primitives, used deliberately, rather than
> whatever the hand produces.

> [!NOTE]
> **The initialism is ours; the set is his.** Hampton writes "C curve, straight, S curve"; "CSI" and
> the `sCurveTo` / `cCurveTo` aliases are the studio's shorthand for it.

> [!NOTE]
> **Two vocabularies, answering different questions — you want both.** C/S/straight describes the
> **mark you make**: what the pen does between two points.
>
> Loomis describes the **edge you are drawing**, which is a different thing and is cited:
>
> > **Source**: Loomis, *Figure Drawing for All It's Worth*, p. 24 — "What Is Line?"
> >
> > *A line and a contour are not the same.* A piece of wire presents a **line**; a **contour is an
> > edge**. That edge is either a **sharp limitation** — the edges of a cube — or a **rounded and
> > disappearing** one, as on a sphere. And contours **pass in front of one another**, which is what
> > gives an undulating form its depth. The painter can dispense with outline entirely, defining
> > contours against adjacent masses or building the form in relief with value instead.
>
> The practical join: **Loomis tells you which edges deserve a line at all, CSI tells you what to draw
> once you have decided.** A rounded, disappearing limitation drawn as a hard even outline is the
> commonest way a construction reads as a cut-out — see the *lost and found* of edges in
> `polson://manual/07` §1, and prefer `ctx.strokeToPath(...)` or `drawTaperedStroke` where the weight
> must vary along the edge. Where an edge should disappear, the answer may be no stroke at all.

```
       C-Curve                  S-Curve                  Straight (I-Line)
      ╭────────╮               ╭─────╮
     │          │             │       ╰─────╮        ──────────────────────
     │          │             │              │
      ╰────────╯               ╰────────────╯
   (Cranium, Eyelid, Jaw)     (Windblown Hair, Tendons)  (Nose Bridge, Shrouds)
```

### Hampton's two additions: asymmetry, and the wrapping line

> **Source**: Michael Hampton, *Figure Drawing: Design and Invention* (2009) — "Gesture Drawing" and
> the negative-space discussion at p. 91.

**The three lines are chosen by side, not sprinkled evenly.** Hampton's rule is that a figure has a
*passive* side and a *pinched* (active) side, and they get different lines:

- The **passive side** is drawn with **S curves**, so the negative shape beside it reads fluid and
  rhythmic.
- The **pinched side** takes the **C curve or the straight**, exaggerated toward a more jagged shape,
  to suggest weight or flexion.

That is a decision procedure rather than a taste, and it is the part most easily lost when the three
lines are treated as a menu: **the curves are used asymmetrically on purpose.** A contour with S
curves on both sides has no active side and therefore no weight.

> [!TIP]
> **It is also a claim about the space you are not drawing.** Hampton's point at p. 91 is that the
> line choice designs the *negative shape* beside the figure as much as the figure itself — which is
> Manual 09 §4's "no two areas the same size or shape" arriving from the other direction. Choosing an
> S for one side and a straight for the other is what stops the two negative shapes matching.

**Wrapping lines are a fourth type, and the SDK already draws them.** Hampton adds them after the
three: curves drawn *across* and around a form to state its perspective, laid on top of the gesture
rather than replacing it, and the decision they encode is whether the form is coming toward the
viewer or receding from it.

`Drawing.drawCrossContourHatch(ctx, cx, cy, rx, ry, startAngle, endAngle, count, strokeColor, lineWidth)`
is exactly this mark — Manual 05 §4 already uses it for planar form, and Manual 03 for inking. What
Hampton adds is *when*: a wrapping line is how a gesture states depth before any tone exists.

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

> **Principle**: Curved organic surfaces should be conceived as **discrete planes** with **cross-contour lines** wrapping around them to define cylindrical depth.

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

### The order the planes are found in

> **Source**: Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — p. 13, *planes* being the
> fourth of his five P's (proportion, placement, perspective, planes, pattern).

Loomis's account of why planes matter is short and worth taking literally: **it is through the effect
of light on planes that we arrive at the solid appearance of the form.** Having got the perspective
right, separate the effect into planes, and look for them in this order:

1. **Planes of full light** — found first, because everything after is defined relative to them.
2. **Halftone planes**, where the form turns away from the light.
3. **Shadow planes**, beyond the halftone.
4. **Reflected light *within* the shadow** — which is part of the shadow, and still defines form.

Step 4 is the one most often mishandled. Reflected light is not a fifth value competing with the
light side; it belongs to the shadow, and once it climbs to the value of a halftone the form goes
flat. Manual 07 §1 is the same sequence rendered as six zones, with the caveat that a scene with
nothing to bounce has no step 4 at all.

> [!TIP]
> **The order is a working order, not just a taxonomy.** Blocking the full-light planes first gives
> every later decision something to be measured against; starting from the shadow shapes — the
> tempting order, because they are usually the largest — leaves the halftones with no reference. In
> practice that means the lit planes get their flat fill *before* any
> `Drawing.drawCrossContourHatch(...)` pass, not after it.

---

## 5. Light, Shadow & Two/Three-Value Studies

> **Implemented by**: `Drawing.createNotanPalette(type)` for the value sets, and `Drawing.createCompositionGrid(...)` when the study is about placement as well as value (Manual 09).

> **Principle**:
> - **Two-Value Study**: Strictly separates the illuminated half of the figure from the shadow half along the **Shadow Terminator**.
> - **Three-Value Study**: Highlights ($V_1$), Midtones ($V_2$), Core Shadow ($V_3$).

### Mapping Value Studies to Polson Pipeline:
1. **Penciler**: Draws the **Shadow Terminator boundary path** (Two-Value division).
2. **Colorist**: Fills Midtone base ($V_2$), sunlit highlights ($V_1$), and core shadow planes ($V_3$) using `Skia.Shader.sksl` Ben-Day dots.
3. **Inker**: Adds Ambient Occlusion ink masses ($V_4$, `#0a0a0c`) in deep crevices.

### Pattern is placement in tone, and four values are enough to decide it

> **Source**: Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — p. 13; and *The Eye of the
> Painter* (Viking Press, 1961) — pp. 39-40.

Loomis separates two things a value study is doing, and the distinction is worth keeping:
**placement relates to composition in terms of line; pattern relates to it in terms of tonal areas.**
A two- or three-value study is a *pattern* decision. It answers where the tonal masses go, and it can
be right or wrong independently of where the drawn lines go — which is why it is worth making before
there is anything to draw lines on.

*The Eye of the Painter* turns that into an exercise concrete enough to run as a script: abstract
pattern sketches no larger than three by four inches, in **about four values** and nothing else, made
without a subject in mind — they will often suggest one. Three details in it change how a value
study is built:

- **A value is not an area.** One value may be cut into as many separate patches as you like, jump
  over another, be surrounded by another, or be simple where another is broken up. The count is of
  *values*, not of shapes.
- **The background is one of the four**, and may be the dominant one. So
  `Drawing.createNotanPalette('classic3')` plus a ground *is* the four.
- **Try not to have any two areas the same size or shape.** Manual 09 §4 carries this as the rule the
  70-20-10 split is one instance of.

> [!TIP]
> **Draft the study small, for the reason Loomis gives and one he does not.** His is that a small
> rectangle forces the decision to be about masses. The other is measured: encode time tracks pixel
> count, so a study at 320 × 240 costs a fraction of the same study at full size, and a value pass is
> exactly the kind of render nobody needs at full resolution. Pair it with `render: false` and a
> stashed bitmap when the study is only feeding the next stage.

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
