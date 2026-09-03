# Studio Manual 06: Linear Perspective & 3D Form Construction

> **Source Reference**: Ernest R. Norling, *Perspective Made Easy* (Macmillan 1939; Dover 1999) — §1 from Step One, §3 from Step Fourteen, §4
> from Steps Twelve and Fifteen, pp. 107–147. Andrew Loomis, *Creative Illustration* (Viking Press, 1947) — §5 and
> §5a from pp. 41–42; and Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — §2 from
> pp. 29–35. **All sections are now cited.** Norling's Steps Five–Six (two vanishing points) and
> the "Dividing the Circle" pages of Step Fourteen are still unread and bear on §1 and §3.  
> **Purpose**: Translates linear perspective theory (horizon lines, vanishing points, 3D box projection, cylinder tangent ellipses, diagonal plane subdivision, and convergence testing) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Linear Perspective Mathematics & Grid Setup

> **Implemented by**: `Drawing.createPerspectiveGrid(options)` → `PerspectiveGrid`, then `Drawing.drawPerspectiveGrid(ctx, grid, options)`. Signatures: `polson://sdk/core/Drawing`; returned model: `polson://sdk/schema/Drawing`.

> **Source**: Ernest R. Norling, *Perspective Made Easy*, Step One — pp. 5, 8, 10.
>
> Four definitions, and the whole of `createPerspectiveGrid` rests on them:
>
> - **The horizon** is the distant line where earth and sky seem to meet.
> - **The vanishing point** is the place *on the horizon* where the rails of a track appear to meet.
> - **The horizon is the height of your eyes**, no matter where you are above the ground.
> - **The eye-level is the height of your eyes**, no matter where you are.
>
> Sit down on the track and your eye-level drops — and the distant horizon drops with it, to meet the
> change. That is the whole mechanism, and it is why `horizonY` is the first thing a scene decides.

> [!TIP]
> **`horizonY` is a camera decision, not a compositional one**, and picking it by where the line looks
> nice in the frame is the commonest way a scene ends up unplaceable. It says where the viewer's eyes
> are: high on the canvas means a low viewpoint looking up, low means a raised one looking down. Decide
> where the viewer is standing, then set it — Manual 06 §5a does this explicitly by giving the camera
> an eye height in metres, and Manual 05 §1 states the constraint that follows (one horizon per
> picture, because there is one observer).
>
> Norling's own exercise is still the fastest way to feel it: look at the same scene from the ground,
> from a window, and from the top of a building, and watch what moves.

> **Principle**:
> In 2-point perspective, the **Station Point ($SP$)** is the camera position, $d$ is the focal distance, and $\theta$ is the camera rotation angle relative to the primary plane.
>
> The Left and Right Vanishing Points are mathematically fixed along the **Horizon Line ($HL_y$)**:
> $$VP_L = (CV_x - d \cot \theta,\; HL_y)$$
> $$VP_R = (CV_x + d \tan \theta,\; HL_y)$$
>
> - **Wide-Angle Lens ($d \approx 400\text{–}600\text{px}$)**: Vanishing points are close together, creating dynamic, exaggerated foreshortening.
> - **Telephoto Lens ($d \approx 1400\text{–}2000\text{px}$)**: Vanishing points are far off-canvas, compressing depth and flattening planes.
> - **Normal Human Vision ($d \approx 800\text{–}1000\text{px}$)**: $60^\circ$ standard cone of vision.

```
       VPL <─────────────────────── CV (Center of Vision) ───────────────────────> VPR
        │                                    │                                      │
   ─────●────────────────────────────────────┼──────────────────────────────────────●───── Horizon Line
         \                                   │                                     /
          \                                  │                                    /
           \                                 │                                   /
            \                                │                                  /
             \───────────────┐               │               ┌─────────────────/
              \    Left Face │               │               │   Right Face   /
               \             │               │               │               /
                \            ▼               ▼               ▼              /
                 \           ●───────────────●───────────────●             /
                  \          │          Front Corner         │            /
```

---

## 2. 3D Bounding Box Projection

> **Implemented by**: `Drawing.createPerspectiveBox(grid, anchorX, anchorY, width, height, depth)` → `PerspectiveBox` (all 8 vertices $V_0 \dots V_7$ and the 6 faces), then `Drawing.drawPerspectiveBox(ctx, box, options)` for the shaded or wireframe render.

A 3D perspective box is defined by:
1. **Front-Bottom Anchor Point** $(A_x, A_y)$.
2. **Height** $H$ (vertical upward along the $Y$-axis).
3. **Width** $W$ (receding along the Left Vanishing Point ray $VP_L$).
4. **Depth** $D$ (receding along the Right Vanishing Point ray $VP_R$).

### The 8 Projected 3D Vertices:
- **Bottom 4 Vertices**:
  - $V_0 = \text{Anchor}$ (Front-Bottom)
  - $V_1 = \text{Anchor} + \frac{W}{\|VP_L - \text{Anchor}\|} (VP_L - \text{Anchor})$ (Left-Bottom)
  - $V_2 = \text{Anchor} + \frac{D}{\|VP_R - \text{Anchor}\|} (VP_R - \text{Anchor})$ (Right-Bottom)
  - $V_3 = \text{Intersection of } (V_1 \rightarrow VP_R) \text{ and } (V_2 \rightarrow VP_L)$ (Back-Bottom)
- **Top 4 Vertices**:
  - $V_4 = V_0 - (0, H)$ (Front-Top)
  - $V_5 = \text{Intersection of } (V_4 \rightarrow VP_L) \text{ and vertical from } V_1$ (Left-Top)
  - $V_6 = \text{Intersection of } (V_4 \rightarrow VP_R) \text{ and vertical from } V_2$ (Right-Top)
  - $V_7 = \text{Intersection of } (V_5 \rightarrow VP_R) \text{ and } (V_6 \rightarrow VP_L)$ (Back-Top)

### The 3 Visible Lighting Planes:
1. **Top Plane ($V_4, V_5, V_7, V_6$)**: Sunlit Highlight.
2. **Left Plane ($V_0, V_1, V_5, V_4$)**: Midtone Flat.
3. **Right Plane ($V_0, V_2, V_6, V_4$)**: Core Shadow.

> **Source**: Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — pp. 29–35, *perspective*
> being the third of his five P's (proportion, placement, perspective, planes, pattern).

### The box is a measuring device, not a subject

This is the part that decides how §2 gets used, and it is easy to miss while reading a vertex
formula. Loomis's claim is that **anything can be drawn within the cube or block** — and that even
when you never draw the block, you must *feel* the perspective relationship of the figure or object
as it would be inside one. The working instruction is blunt: **draw the block to equal the height,
width and depth of the form**, then build the form inside it.

So a `PerspectiveBox` earns its place in a scene that contains no boxes. A barrel, a head, a parked
car, a figure — each gets a block first, and the block is what carries the perspective; the form
inherits it. Loomis's suggested exercise is to draw a thing *first with blocks* and see how much more
construction arrives with it.

> [!TIP]
> Two SDK calls exist precisely because the block is a measuring device. `Drawing.createPerspectiveBox(...)`
> returns the eight vertices whether or not you ever call `Drawing.drawPerspectiveBox(...)`, so a block
> can be built, measured against, and never rendered. And `Drawing.subdividePerspectiveQuad(...)` takes
> one of its faces and divides it in true foreshortening — which is how anything laid *on* a face
> (paving, panelling, a window grid) stays in the same perspective as the block that located it.

### Construction order, and the ground plan first

Loomis builds in a fixed order, and each step is a prerequisite rather than a preference:

1. **Establish a horizon and eye level.** Nothing before this is meaningful, because the ray
   directions the box is built from are directions *to* vanishing points that do not yet exist.
   `Drawing.createPerspectiveGrid(...)` is this step.
2. **Lay a square flat on the ground.** *All ground plans begin with this.* The square is the
   footprint; opposite sides recede toward their respective vanishing points.
3. **Build the cube on the square**, raising verticals from its corners.

The order matters for a reason the maths hides: the footprint is a *plan*, and a plan can be checked
against the scene's other footprints before anything is raised. Two objects standing on the same
floor share a ground plane, so their plans must agree — which is the same argument §5a makes for
holding one scale across figure and architecture.

### The rule that checks itself

> **The perspective depth of the top side of the block determines the perspective depth of the bottom
> side.** (p. 35.)

This is the cheapest correctness test §2 has, and it is a test rather than a construction: the top
and bottom faces are parallel planes, so their receding edges converge on the *same* vanishing point.
A box whose top reads at one depth and whose bottom reads at another has an edge going to the wrong
place. `Drawing.verifyPerspectiveConvergence(...)` takes the line pairs and the expected vanishing
point and reports the maximum angular error, so this is checkable without eyeballing it.

Loomis pairs it with a second observation that belongs to §3: **ellipses narrow as they near the eye
level.** That is the same rule `Drawing.drawPerspectiveCylinder(...)` applies per cap, stated from
the drawing side rather than the projection side.

### Round forms inside the block

A sphere or any rounded form is placed by **first drawing the plans of the block's middle planes** —
the middle plane lengthwise and the middle plane crosswise. Those two planes give the form its axes
in perspective, and the round form is then drawn to touch the block's faces. An elongated block
around an elongated form works the same way.

This is the construction to reach for when `Drawing.drawPerspectiveCylinder(...)` is not the right
shape — it takes an upright cylinder, and anything tilted, elongated or irregular wants a block plus
middle planes instead.

> [!IMPORTANT]
> **Two things about §2's formula that the geometry above does not say, and both matter.**
>
> **Width and depth are screen distances, not scene dimensions.** The construction steps $W$ *pixels*
> along the normalised ray toward $VP_L$. Two boxes at different depths given the same $W$ are
> therefore **not** the same size in the scene — which is exactly the confusion §5a exists to prevent.
> Where a box has to be a real size, fix its footprint against the scene's ground plan (§5a) and
> derive the pixel extents from that; do not pass a metre count as `width`.
>
> **A side may not reach its vanishing point.** Past 85% of the anchor-to-VP distance the far corner
> arrives at the vanishing point and the box turns inside out. This used to be **silently clamped**,
> so an over-large box came back quietly shortened and looked deliberate; it now throws, naming the
> measured fraction and the largest extent that would have been accepted. If you meant a bigger box,
> move the anchor further from the vanishing point rather than pushing the extent.

> [!NOTE]
> **The three lighting planes above are a convention, not geometry.** Top-sunlit / left-midtone /
> right-shadow is true for one light position and is stated here as though it followed from the
> projection. Manual 07 §1's second law is the actual rule — a plane's value follows the angle it
> presents to the source — so with the key on the left, the *left* face is the lit one. Take the
> three-plane assignment as a default for a high key light off to the right, and override it whenever
> the scene's light says otherwise.
>
> Loomis notes the connection in passing and it is worth taking seriously: lighting and perspective
> are more closely related than most artists realise. Manual 07 §2's cast-shadow construction is the
> other half of that — it needs this grid's horizon to work at all.

---

## 3. Perspective Cylinders & Tangent Ellipses

> **Implemented by**: `Drawing.drawPerspectiveCylinder(ctx, grid, anchorX, anchorY, radius, height, options)` — inscribes both tangent ellipses and joins their outer extremes in one call.

> [!IMPORTANT]
> **The cylinder is anchored differently from the box, and this is the one thing to know before calling it.** `createPerspectiveBox` takes the near bottom **corner** with `width`/`depth` measured along the receding rays. `drawPerspectiveCylinder` takes the **centre of the base circle**, and its `radius` is **half the drawn width** — the silhouette spans `anchorX ± radius`, measurable off the render. A cylinder has no corner to anchor to and `radius` implies a centre, so the two calls differ on purpose.
>
> **Height above the ground needs no parameter.** Each cap's flatness is read from the directions to the vanishing points *at that cap's own centre*: nearer the horizon means shallower rays means a flatter ellipse. So a bowl on a counter comes out flatter than the same bowl on the floor because you anchored it higher in the frame, and the top ellipse of any cylinder is automatically flatter than its base. Do not try to compensate by hand.
>
> This is worth stating because the call used to do neither. It built a `2r × 2r` box, anchored at that box's near corner, and took the width from the footprint's **diagonal** — drawing at a measured **1.9× the requested width**, off centre, with one shared squash for both caps. It never errored; it returned a plausible cylinder of the wrong shape, and a live run lost four iterations to it before anyone looked twice.

> **Source**: Ernest R. Norling, *Perspective Made Easy*, Step Fourteen — pp. 135, 137.
>
> **Think of an upright cylinder as sitting in a square box** with the vanishing point directly
> opposite, as with railway tracks. Both sides of the cylinder are upright and parallel, and redrawing
> the box in two-point perspective does not move the cylinder inside it.
>
> **"The long axis always forms a T with the upright line of the cylinder."** For an upright cylinder
> that makes the cap ellipse's major axis **horizontal** — and the rule generalises to any cylinder:
> on the wheels and lamps of a car the ellipses' long axes point in quite different directions, each
> square to its own cylinder's axis. It is the fastest check available on any drawn ellipse.

> [!NOTE]
> **`drawPerspectiveCylinder` follows that rule, and it took a fix to.** An earlier version built each
> cap from conjugate semi-diameters toward the two vanishing points, which tilted the cap away from the
> centre of vision: on a 140 px cap with the centre of vision at *x* = 400, the apex sat **0.5 px** from
> the anchor on axis but **13.5 px** off at 250 px left and **15.5 px** off at 300 px right — a tenth of
> the cap's width, reading as a leaning bottle.
>
> The caps are now axis-aligned. The foreshortening still comes from the grid — the ground directions
> give the ellipse, and only its **vertical** extent is kept — so a cap nearer the horizon is still
> flatter, and the two caps still differ. Only the tilt is discarded.
>
> Worth knowing that this is a deliberate departure from strict projection: a genuine wide-angle
> projection *does* tilt a circle off axis, and Norling's rule is exact only on the central axis. This
> grid is a screen-space construction rather than a metric camera, so the drawing convention is the
> right one here — but if the toolkit ever grows a real camera, this is a decision to revisit.
> Pinned by `TestCylinderCapsKeepAHorizontalMajorAxis`, whose off-axis cases are the whole point.

> **Principle**:
> An accurate perspective cylinder is constructed by inscribing perspective circles (ellipses) inside the top and bottom square faces of a bounding perspective box.

1. **Top Ellipse**: Fits inside quad $(V_4, V_5, V_7, V_6)$.
2. **Bottom Ellipse**: Fits inside quad $(V_0, V_1, V_3, V_2)$.
3. **Contour Silhouettes**: Connect the outer tangent extremes of the top and bottom ellipses with straight vertical lines.

---

## 4. Perspective Division with Diagonals

> **Implemented by**: `Drawing.subdividePerspectiveQuad(quad, uCount, vCount)` → `Point[][]` — projective interpolation, so the returned cells are correctly foreshortened rather than evenly spaced.

> **Source**: Ernest R. Norling, *Perspective Made Easy* (Macmillan 1939; Dover 1999), Step Twelve —
> "How to Find the Center" p. 107, "The Use of Diagonals" p. 109, "Spacing in Perspective" p. 113.

> **Principle**:
> In perspective, equal spatial increments (e.g. windows along a building, floor tiles, staircase steps) appear progressively compressed.
> To find the exact perspective center of any 4-corner quad $[P_0, P_1, P_2, P_3]$:
> $$\text{Center} = \text{Intersection of diagonal } (P_0 \rightarrow P_2) \text{ and diagonal } (P_1 \rightarrow P_3)$$

Norling's demonstration is worth keeping in mind because of what it establishes: chalk the diagonals
on the face of a real brick, then sketch that brick from any angle, and the crossing point marks the
centre of the face **regardless of the brick's position**. The centre is preserved by the projection —
which is why the construction works at all, and why `subdividePerspectiveQuad` can interpolate
projectively rather than evenly. Once you have the centre, lines from it to the vanishing points
divide the plane in perspective; Norling's example is turning the top of the brick into a tennis
court.

### Spacing equal intervals into depth

The centre construction *measures* a quad you already have. Repeating a spacing — fence posts,
columns, window bays, floor boards — is the other half, and it has its own construction (p. 113):

1. Draw two posts of the same height, the first two of the run.
2. Draw three lines through them: one along the **tops**, one through the **centres**, one along the
   **bases**. All three converge on the same vanishing point.
3. Draw a line from the **top of the first post through the centre of the second**. Where it meets the
   base line is exactly where the **third** post stands.
4. Repeat from the second post to place the fourth, and so on.

Each new interval is produced from the previous two, so the compression comes out of the construction
rather than being estimated — and it needs no measurement, only the three convergent lines.

> [!NOTE]
> **The toolkit has no call for this.** `subdividePerspectiveQuad` divides a quad you already have
> into a fixed grid; it cannot extend a rhythm outward into depth from two examples, which is what a
> colonnade or a receding fence needs. Building it means returning successive base points until they
> converge on the horizon. Nothing exists yet — do not write one into a script expecting it to resolve.

### Equal divisions, by diagonal transfer

> **Source**: Norling, *Perspective Made Easy*, Step Fifteen — "Dividing a Surface" p. 145, "Dividing
> a Surface in Perspective" p. 146, "Windows or Columns" p. 147.

The construction behind `subdividePerspectiveQuad`, and worth knowing because it explains *why* even
spacing has to be transferred rather than measured. Flat first, on the face of a brick:

1. Divide the face into **N equal horizontal bands** — easy, because nothing is foreshortened.
2. Draw one **diagonal, corner to corner**.
3. Where each horizontal crosses that diagonal, drop an **upright**.

Those uprights divide the face into N equal *vertical* parts. **The same method holds unchanged in
perspective**, and the uprights come out correctly foreshortened rather than evenly spaced.

For a building — Norling's eight rows of windows:

1. Divide the **corner line** into eight equal parts. This is the move that matters: a vertical corner
   edge is parallel to the picture plane, so it is **not foreshortened** and can be measured directly
   with a ruler.
2. Run lines from those points to the wall's vanishing point.
3. One corner-to-corner diagonal across the wall divides it into the eight bays.

> [!TIP]
> **Measure where nothing is foreshortened, then transfer.** That is the whole of perspective
> measurement, and it is why `subdividePerspectiveQuad` interpolates projectively instead of dividing
> the quad's edges evenly: evenly-spaced points along a receding edge are the one thing that is
> definitely wrong. If you ever need to check its output by hand, the diagonal is the check — every
> division must land where a corner-to-corner line crosses its own row.

---

## 5. Convergence Verification (Exercise 2.5)

> **Implemented by**: `Drawing.verifyPerspectiveConvergence(lines, expectedVp, maxToleranceDeg)` → `{ passed, maxAngularErrorDeg, message }`. Run it as a QA gate before committing a perspective scene.

> **Source**: Loomis, *Creative Illustration*, p. 42. His method for checking any picture is exactly
> this call's: **carry the receding straight lines back until they meet**. The lines must be parallel
> to the ground plane — two floorboards, two ceiling lines, the top and bottom of a door — which is
> the one precondition worth remembering, since feeding the call a set of non-parallel edges produces
> a confident answer about nothing.

To verify that drawn line segments $[(A_1, B_1), (A_2, B_2), \dots]$ correctly obey perspective:
1. Compute the angular slope of each line: $\theta_i = \text{atan2}(B_{iy} - A_{iy}, B_{ix} - A_{ix})$.
2. Compute the ideal angle from $A_i$ to the true vanishing point $VP$: $\hat{\theta}_i = \text{atan2}(VP_y - A_{iy}, VP_x - A_{ix})$.
3. Angular Error $= |\theta_i - \hat{\theta}_i|$. If $\text{Error} \le 5^\circ$, the line passes perspective QA.

---

## 5a. One Scale for the Figure and the Architecture

This manual gives the projection and Manual 08 gives the eight-head canon, and until you join them the two work in different units: a grid measured in pixels off the horizon, and a body measured in head-lengths of itself. A comic panel almost always contains both — a person and a thing the person is standing at — so the join is the ordinary case rather than an advanced one.

**Model the scene in metres and give the grid the same numbers.** One function turns metres into pixels; the figure, the architecture and the grid all go through it, so none of them can drift from the others.

```js
// One camera, in metres. Nothing in the scene invents its own scale.
const CAM = { eyeHeight: 1.55, focal: 900, horizonY: 300, cvX: 470 };

// x metres right of the view axis, h metres above the ground, z metres away.
function project(x, h, z) {
    const s = CAM.focal / z;                       // pixels per metre at this depth
    return { x: CAM.cvX + x * s, y: CAM.horizonY + (CAM.eyeHeight - h) * s, s: s, z: z, h: h };
}

// The SDK grid gets the same numbers, so its vanishing points agree with the projector.
const grid = Drawing.createPerspectiveGrid({
    type: '2point', horizonY: CAM.horizonY, centerOfVisionX: CAM.cvX,
    focalLength: CAM.focal, cameraAngleDeg: 45
});
```

Two consequences fall out immediately, and both are worth stating.

**The horizon is eye height, not a compositional line.** `CAM.eyeHeight` and `horizonY` are the same fact in two units. Anything in the scene standing exactly 1.55 m tall has its top *on* the horizon, at any distance — which is the fastest sanity check available on a grid.

> **Source**: Andrew Loomis, *Creative Illustration* (Viking Press, 1947), p. 41 — "Everything You
> Draw Is Related to an Eye Level", and p. 42 "Eye Level, Camera Level, and Horizon Mean the Same".
> Loomis puts it flatly: **the horizon *is* the eye level**, and it is impossible to draw correctly
> without settling a viewpoint and an eye level first. He also distinguishes the viewpoint — the
> centre of the field of vision, the station point — from the vanishing points, which is the confusion
> `centerOfVisionX` and `vpL`/`vpR` are named to keep apart.

**A figure's pixel height is a projection, never a choice.** `createMannequinFigure(x, y, totalHeight)` takes the crown as its origin and runs 8 head-lengths down, so:

```js
const BODY_H = 1.78, BODY_Z = 3.4, BODY_X = 0.35;
const crown = project(BODY_X, BODY_H, BODY_Z);
const feet  = project(BODY_X, 0, BODY_Z);
const figure = Drawing.createMannequinFigure(crown.x, crown.y, feet.y - crown.y);
```

`feet.y - crown.y` is 1.78 m at that depth and nothing else. Choosing a pixel height by eye and then placing a counter by eye is how a scene ends up with a vendor whose hip does not clear their own counter.

### The check happens in metres, before anything is projected

The counter is 0.95 m high at 2.6 m; the vendor is 1.78 m at 3.4 m. The canon puts the pelvis 3.6 of 8 head-lengths below the crown, so the hip stands at $1.78 \times (1 - 3.6/8) = 0.98\ \text{m}$ — clearing the counter by **2.9 cm**. That is the answer, and it was available before a pixel was drawn.

> [!IMPORTANT]
> **Screen `y` cannot answer that question when the two things are at different depths.** In the scene above the hip lands at $y = 451$ and the counter's top edge at $y = 508$ — 57 px apart, which looks like a comfortable clearance and is mostly *depth*: the counter is nearer, so it falls further from the horizon. The scale is 346 px/m at the counter and 265 px/m at the figure, and comparing pixel positions across that difference compares two different rulers.
>
> Carry `s`, `z` and `h` on every projected point, as `project` above returns them. Then a later pass can foreshorten a puddle and a counter-top bowl correctly without either caller knowing how the other works, and a height question can always be taken back to metres where it has an answer.

Finish by proving the hand projector and the SDK grid actually agree, rather than assuming it — feed the architecture's depth-running edges to §5:

```js
const check = Drawing.verifyPerspectiveConvergence(
    [[project(-1.1, 0.95, 2.6), project(-1.1, 0.95, 6.0)],
     [project(1.1, 0, 2.6), project(1.1, 0, 6.0)]],
    grid.cv, 5);
log(check.message);   // PASS, max angular error 0.000 deg - two constructions, one answer
```

Edges built by the projector converge on the grid's own centre of vision because both were given `CAM.focal`, `CAM.horizonY` and `CAM.cvX`. Change one of the three in one place only and this is the line that tells you.

### The whole scene, end to end

Everything above in one program: the projector, the grid built from the same numbers, the architecture, the figure sized by projection, and the check stated in metres.

```javascript
// A figure and a piece of architecture in one scene, at one scale.
const canvas = createCanvas(940, 760);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f1ea';
ctx.fillRect(0, 0, 940, 760);

// One camera, in metres. Nothing in the scene invents its own scale.
const CAM = { eyeHeight: 1.55, focal: 900, horizonY: 300, cvX: 470 };

// x metres right of the view axis, h metres above the ground, z metres away.
function project(x, h, z) {
    const s = CAM.focal / z;                            // pixels per metre at this depth
    return { x: CAM.cvX + x * s, y: CAM.horizonY + (CAM.eyeHeight - h) * s, s: s, z: z, h: h };
}

// The SDK grid is given the same numbers, so its vanishing points agree with the projector.
const grid = Drawing.createPerspectiveGrid({
    type: '2point', horizonY: CAM.horizonY, centerOfVisionX: CAM.cvX,
    focalLength: CAM.focal, cameraAngleDeg: 45
});
Drawing.drawPerspectiveGrid(ctx, grid, { lineCount: 10, lineWidth: 0.6 });

// --- Architecture: a counter 0.95 m high, 2.2 m wide, at 2.6 m -------------------
const COUNTER_H = 0.95, COUNTER_Z = 2.6;
const topL = project(-1.1, COUNTER_H, COUNTER_Z);
const topR = project(1.1, COUNTER_H, COUNTER_Z);
const footL = project(-1.1, 0, COUNTER_Z);
const footR = project(1.1, 0, COUNTER_Z);

// --- Figure: 1.78 m, standing behind the counter at 3.4 m -----------------------
const BODY_H = 1.78, BODY_Z = 3.4, BODY_X = 0.35;
const crown = project(BODY_X, BODY_H, BODY_Z);
const feet = project(BODY_X, 0, BODY_Z);
const bodyPx = feet.y - crown.y;                        // 1.78 m at this depth

const figure = Drawing.createMannequinFigure(crown.x, crown.y, bodyPx);

// The figure is drawn first, then the counter over it — the counter is nearer.
Drawing.drawMannequinSolid(ctx, figure, {
    fillColor: '#c9cfd8', shadowColor: '#98a2b0', strokeColor: '#2f3946', strokeWidth: 1.4
});

ctx.fillStyle = '#8d7a63';
ctx.strokeStyle = '#3b3229';
ctx.lineWidth = 1.6;
ctx.beginPath();
ctx.moveTo(footL.x, footL.y);
ctx.lineTo(topL.x, topL.y);
ctx.lineTo(topR.x, topR.y);
ctx.lineTo(footR.x, footR.y);
ctx.closePath();
ctx.fill();
ctx.stroke();

// --- The check happens in metres, before anything is projected ------------------
// The canon puts the pelvis 3.6 of 8 head-lengths below the crown.
const hipH = BODY_H * (1 - 3.6 / 8);
log('counter ' + COUNTER_H.toFixed(2) + ' m, hip ' + hipH.toFixed(2) + ' m, clears by '
    + ((hipH - COUNTER_H) * 100).toFixed(1) + ' cm');

// Screen y cannot answer this: the two are at different depths.
const hipScreenY = crown.y + bodyPx * (3.6 / 8);
log('on screen the hip is at y=' + hipScreenY.toFixed(0) + ' and the counter edge at y='
    + topL.y.toFixed(0) + ' — ' + (topL.y - hipScreenY).toFixed(0) + ' px apart, which is depth, not height');

log('scale: ' + topL.s.toFixed(0) + ' px/m at the counter, ' + crown.s.toFixed(0) + ' px/m at the figure');
canvas;
```

---

## 6. Symbol → SDK Parameter Map

The notation below maps directly onto the toolkit; nothing here needs re-deriving by hand.

| Concept | SDK parameter or field | Notes |
| --- | --- | --- |
| $HL_y$ | `options.horizonY` | Eye level. Above it, planes are seen from below. |
| $CV_x$ | `options.centerOfVisionX` | Principal point of projection. |
| $d$ | `options.focalLength` | The lens (§1): ~500 wide-angle, ~900 normal, ~1600 telephoto. |
| $\theta$ | `options.cameraAngleDeg` | Yaw. $45^\circ$ is a symmetric corner view. |
| $VP_L$, $VP_R$ | `grid.vpL`, `grid.vpR` | Computed for you; read them, do not re-derive. |
| $VP_V$ | `grid.vpV` | Present only when `type: '3point'`. |
| $(A_x, A_y)$ | `anchorX`, `anchorY` | Front-bottom corner of the box. |
| $W$, $H$, $D$ | `width`, `height`, `depth` | Along $VP_L$, vertical, and along $VP_R$ respectively. |
| $V_0 \dots V_7$ | `box.vertices[0..7]` | Same ordering as §2. |
| Top / Left / Right plane | `box.faces.top`, `.left`, `.right` | The three lit planes of §2. |

---

## 7. Constructing It: A Runnable Scene

Grid, then form, then verification — the order matters, because the box is projected *from* the grid and the QA pass reads back the box's own vertices.

```javascript
// Two-point perspective street corner: camera → block → column → QA.
const canvas = createCanvas(900, 600);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f1ea';
ctx.fillRect(0, 0, 900, 600);

// §1 — The camera. focalLength is the lens choice; 900 ≈ normal human vision.
const grid = Drawing.createPerspectiveGrid({
    type: '2point',
    horizonY: 260,
    centerOfVisionX: 450,
    focalLength: 900,
    cameraAngleDeg: 35
});
Drawing.drawPerspectiveGrid(ctx, grid, { lineCount: 14, lineWidth: 0.6 });
log('VP_L x=' + grid.vpL.x.toFixed(1) + '  VP_R x=' + grid.vpR.x.toFixed(1));

// §2 — A block anchored on its near-bottom corner. The three fills are the
// three visible lighting planes: sunlit top, midtone left, core-shadow right.
const box = Drawing.createPerspectiveBox(grid, 430, 470, 210, 190, 240);
Drawing.drawPerspectiveBox(ctx, box, {
    topFill: '#d9dee6',
    leftFill: '#9aa5b4',
    rightFill: '#5d6878',
    strokeColor: '#232a34',
    strokeWidth: 1.6
});

// §3 — A column: tangent ellipses inscribed in the top and bottom faces.
Drawing.drawPerspectiveCylinder(ctx, grid, 700, 470, 46, 150, {
    topFill: '#cdd4dd',
    sideFill: '#8b95a4',
    strokeColor: '#232a34'
});

// §5 — QA the depth-running edges against VP_R before committing the scene.
const check = Drawing.verifyPerspectiveConvergence(
    [[box.vertices[0], box.vertices[2]], [box.vertices[4], box.vertices[6]]],
    grid.vpR,
    5
);
log(check.message);

canvas;
```
