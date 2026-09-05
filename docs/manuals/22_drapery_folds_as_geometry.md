# Studio Manual 22: Drapery — Folds as Geometry

> **Source Reference**: Cliff Young, *Drawing Drapery from Head to Toe* (Dover, 2007 — unabridged
> republication of House of Little Books, New York, 1947) — §1–§4 from the *From Cloth to Clothes*,
> *Drapery in Action*, *Influence of Gravity*, *Crushed Cylinders* and *Bent Cylinders* sections.
> Distilled in our own words and cited by section. The Dover edition states no copyright and no
> all-rights-reserved clause, and its US status is not asserted here either way — handled as the
> Loomis and Bokhua rows regardless: distil, cite, never reproduce at length.
> **Purpose**: Where folds come from, as a rule rather than an observation — so clothing can be
> constructed on a figure the toolkit already builds from cylinders, instead of copied from a model.

---

> **Why a 1947 drawing-drapery book transfers to comics at all.** Because Young's model is
> *geometric*, not observational. His unit is the cylinder — and the cylinder is already what
> `Drawing.createMannequinFigure(...)` builds a limb from. A rendering-led drapery book would give
> you fold *shapes* to copy; this one gives the causes, and causes are what a simplified idiom needs.
> Comic drapery is drapery with most folds removed, and removing them safely means knowing which are
> structural. **Caveat worth stating**: most of the book is plates, so the specific shapes live in
> drawings no text extraction reaches, and the wardrobe is 1947 — slips, night shirts, vests. The
> mechanics transfer; the garment catalogue does not.

---

## 1. Clothes are cylinders

Young starts by clothing the figure in bare cylinders: one over each arm, one over the body and legs.
They **cover the figure without fitting it**, and that gap is where every fold comes from. Sleeves
are cylinders, a jacket body is a cylinder, trousers and skirts continue the scheme.

This is the sentence that makes the book usable here, because it is already true of our figures:
`createMannequinFigure` gives limbs as cylinders with `hip`/`knee`/`ankle` and shoulder/elbow/wrist
joints, and `Drawing.renderVolumetricCylinder(...)` draws one with its own light. Drapery is
therefore not a new construction — it is a treatment of a construction that already exists.

## 2. Folds radiate from points of pull

The governing rule (*Drapery in Action*): **cloth pulled from a point throws folds that radiate from
that point.** Pull from two points and folds radiate from both, meeting and interfering between them.

**Gravity is the constant second pull.** Cloth hung from two tacks has folds radiating from each,
and gravity deepens them by pulling the whole sheet down — so a fold begins along the direction of
its pull and ends hanging vertically. Lower one support and the folds on the *other* side grow in
both size and number. That is a directly usable relationship: fold density is a function of the
difference in support height.

> **The toolkit has no fan primitive, and this is the trap.** `Drawing.drawFeathering(ctx, origin,
> angleDeg, count, length, spacing, ...)` reads as though it radiates from `origin`; it does not —
> its own documentation says *directional **parallel** hatching*, and it produces parallel lines
> whatever the origin. Used for folds it silently draws the wrong thing: parallel verticals that
> look plausible and state nothing. Build the fan yourself from `ctx.drawTaperedStroke(...)`, one
> stroke per fold, heavy at the point of pull and fading as the pull dissipates — the example below
> does exactly that.

## 3. Crushed, twisted, bent

Three deformations of the cylinder, each with its own fold signature (*Crushed Cylinders*, *Bent
Cylinders*):

| Deformation | Fold signature | Where it shows |
| :--- | :--- | :--- |
| **Crushed** | **ring folds** — rings around the cylinder | sleeves, trousers, stockings; a shirt at the belt |
| **Twisted** | folds follow the direction of the twist | **these folds describe action** — they are how a drawing says a limb rotated |
| **Bent** | folds radiate from the joint, crowding on the inside of the bend | elbow, knee, hip |

Young's own experiment for the bent case is worth keeping because it gives a number: a shirt sleeve
pulled over a bent cardboard tube crushes into **four distinct ring folds pulling from the elbow**.
Four, not a scattering — a countable, drawable answer.

And the everyday case that ties §2 and §3 together: **a shirt hangs loose from the shoulders and is
crushed at the waist by the belt**, producing exactly the folds a string tied tightly around a paper
cylinder produces. Support at the top, constriction at the bottom, ring folds where it is gripped.

> **Implemented by**: `Drawing.drawCrossContourHatch(ctx, cx, cy, rx, ry, startAngle, endAngle,
> count, strokeColor, lineWidth)` draws an arc across a cylindrical form — which *is* a ring fold,
> arrived at from the cross-contour side. Vary `ry` to set how far round the cylinder the fold reads.

## 4. Drawing it

Two things in this example are worth more than the drapery: the fan is hand-built because no call
provides one, and the sleeve's folds are clipped to `ctx.strokeToPath(sleeve)` — the stroked arm
turned into a fillable outline — so a fold cannot stray outside the cloth it belongs to.

```javascript
// Three fold mechanics, each derived from geometry rather than observed.
//
// Note what the toolkit does NOT have: nothing draws a fan of lines from a point. `drawFeathering`
// is *parallel* hatching whatever its origin suggests, so a radiating fold set is built here from
// tapered strokes — heavy at the point of pull, fading as the pull dissipates.
const canvas = createCanvas(900, 430);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f5f2e9'; ctx.fillRect(0, 0, 900, 430);

const INK = '#1b1b19', CLOTH = '#e9e3d5', SHADE = '#a79e8c';
const cells = Layout.columns(Layout.inset(Layout.rect(0, 0, 900, 430), 24, 24, 52, 24), 3, 22);
ctx.font = '400 13px Georgia, serif'; ctx.textBaseline = 'top';

/** A fan of folds spreading from one point of pull, bending toward vertical as gravity takes over. */
function foldsFrom(origin, centreDeg, spreadDeg, count, length) {
    for (let i = 0; i < count; i++) {
        const t = count === 1 ? 0.5 : i / (count - 1);
        const deg = centreDeg - spreadDeg / 2 + spreadDeg * t;
        const rad = deg * Math.PI / 180;
        const end = { x: origin.x + Math.cos(rad) * length, y: origin.y + Math.sin(rad) * length };
        // Control points pulled toward vertical: a fold starts along the pull and ends hanging.
        const c1 = { x: origin.x + Math.cos(rad) * length * 0.35, y: origin.y + Math.sin(rad) * length * 0.4 };
        const c2 = { x: origin.x + Math.cos(rad) * length * 0.5,  y: origin.y + length * 0.8 };
        ctx.drawTaperedStroke(origin, c1, c2, end, 3.4, SHADE);
    }
}

// --- 1. Cloth hung from two tacks: folds radiate from each point of pull -----------
const a = cells[0];
const tacks = [{ x: a.x + a.width * 0.3, y: a.y + 40 }, { x: a.x + a.width * 0.74, y: a.y + 52 }];
ctx.fillStyle = CLOTH;
ctx.beginPath();
ctx.moveTo(tacks[0].x, tacks[0].y);
ctx.quadraticCurveTo(a.x + a.width * 0.52, a.y + 118, tacks[1].x, tacks[1].y);
ctx.lineTo(tacks[1].x + 24, a.y + a.height - 16);
ctx.quadraticCurveTo(a.x + a.width * 0.5, a.y + a.height + 6, tacks[0].x - 34, a.y + a.height - 26);
ctx.closePath(); ctx.fill();
ctx.strokeStyle = INK; ctx.lineWidth = 1.6; ctx.stroke();
for (const tack of tacks) {
    foldsFrom(tack, 90, 78, 6, a.height * 0.62);      // 90 deg is straight down: gravity
    ctx.fillStyle = INK;
    ctx.beginPath(); ctx.arc(tack.x, tack.y, 3.2, 0, Math.PI * 2); ctx.fill();
}
ctx.fillStyle = '#3a352c';
ctx.fillText('folds radiate from each point of pull', a.x, a.y + a.height + 14);

// --- 2. Straight cylinder crushed: even ring folds ---------------------------------
const b = cells[1];
const bx = b.x + b.width * 0.5, rx = b.width * 0.23;
Drawing.renderVolumetricCylinder(ctx, bx - rx, b.y + 30, rx * 2, b.height - 74,
    { x: -0.6, y: -0.5 }, { baseColor: CLOTH, shadowColor: SHADE, highlightColor: '#ffffff' });
for (let i = 0; i < 5; i++) {
    // A ring fold is an arc across the cylinder — the cross-contour construction, reused.
    const cy = b.y + 68 + i * (b.height - 138) / 4;
    Drawing.drawCrossContourHatch(ctx, bx, cy, rx * 0.95, 14, 0.12, Math.PI - 0.12, 2, INK, 1.4);
}
ctx.fillStyle = '#3a352c';
ctx.fillText('crushed cylinder — ring folds', b.x, b.y + b.height + 14);

// --- 3. Bent cylinder: folds crowd at the joint and radiate from it ----------------
const c = cells[2];
const shoulder = { x: c.x + c.width * 0.26, y: c.y + 32 };
const elbow    = { x: c.x + c.width * 0.74, y: c.y + c.height * 0.44 };
const wrist    = { x: c.x + c.width * 0.36, y: c.y + c.height - 46 };
ctx.strokeStyle = CLOTH; ctx.lineWidth = 44; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
ctx.beginPath(); ctx.moveTo(shoulder.x, shoulder.y);
ctx.lineTo(elbow.x, elbow.y); ctx.lineTo(wrist.x, wrist.y); ctx.stroke();
ctx.strokeStyle = INK; ctx.lineWidth = 1.6;
ctx.beginPath(); ctx.moveTo(shoulder.x, shoulder.y);
ctx.lineTo(elbow.x, elbow.y); ctx.lineTo(wrist.x, wrist.y); ctx.stroke();

// Young's sleeve over a bent tube: the folds pull from the elbow, crowding on the inside of the bend.
// Clipped to the sleeve's own outline — `strokeToPath` turns the stroked arm into a fillable shape,
// so a fold cannot stray outside the cloth it belongs to.
const sleeve = new CanvasPath();
sleeve.moveTo(shoulder.x, shoulder.y);
sleeve.lineTo(elbow.x, elbow.y);
sleeve.lineTo(wrist.x, wrist.y);
ctx.save();
ctx.lineWidth = 44; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
ctx.clip(ctx.strokeToPath(sleeve));

const towardWrist = Math.atan2(wrist.y - elbow.y, wrist.x - elbow.x) * 180 / Math.PI;
const towardShoulder = Math.atan2(shoulder.y - elbow.y, shoulder.x - elbow.x) * 180 / Math.PI;
foldsFrom(elbow, (towardWrist + towardShoulder) / 2 + 180, 54, 5, 52);
for (let i = 0; i < 4; i++) {
    // Four distinct ring folds, closest together nearest the joint.
    const t = 0.12 + i * i * 0.075;
    Drawing.drawCrossContourHatch(ctx,
        elbow.x + (wrist.x - elbow.x) * t, elbow.y + (wrist.y - elbow.y) * t,
        22, 8, 0.15, Math.PI - 0.15, 1, INK, 1.3);
}
ctx.restore();
ctx.fillStyle = '#3a352c';
ctx.fillText('bent cylinder — folds pull from the joint', c.x, c.y + c.height + 14);
canvas;
```

## 5. What this does not give you

- **No garment construction.** Young's later sections — the shirt, blouse, jacket, trousers, skirt,
  and their action folds — are almost entirely plates. The mechanics above are the transferable part;
  a jacket lapel is not in the text.
- **No cloth simulation, and none wanted.** These are rules for *drawing* a fold, not for solving
  one. The output is a small number of deliberate lines, which is what a panel needs.
- **The gap named in Manual 20 §6 and Manual 21 §6 is now partly closed.** Drapery has a model.
  Posing still does not: `createMannequinFigure` builds a standing figure, and folds that describe
  action need a limb that moved. That remains the blocking gap for figure work.
