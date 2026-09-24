# Studio Manual 26: Reference Photography and Likeness

> **Credits & Theoretical Foundation**: No external source. Like Manual 16, this documents a Polson subsystem rather than distilling a book. The position it takes — **a likeness is retrieved, never invented, and never used without its terms** — follows from two things already in the project: the Extended Mind principle that what cannot be synthesised is offloaded to the environment, and the ingestion discipline of `reference/README.md`, which records what an author permitted before anything derived from their work is served to an agent. A photograph is somebody's work in exactly that sense. Signatures at `polson://sdk/core/Photo`.
> **Purpose**: When a real face or place belongs in a graphic, how to get one honestly — how to be sure it is the right subject, how to read the terms it arrives under, how to lay several out when their proportions disagree, and when not to use a photograph at all.

---

## 1. What This Is For

> **Implemented by**: `Photo.of(...)`, `Photo.resolve(...)`.

Some graphics are *about* particular people or places. A ranking of the highest-earning actresses, a timeline of a building's construction, a map keyed to five cities: in each of these the subject is a specific real thing, and a drawn approximation is not a stylistic choice but a failure to identify it.

`Photo` is the surface for that case, and it is the mirror of `Assets`:

| | `Assets` | `Photo` |
| :--- | :--- | :--- |
| Provides | **Substance** — what a shape is made of | **Likeness** — who or what a shape *is* |
| Origin | Generated on demand | Retrieved from a source that states its terms |
| Your code supplies | Form, lighting, composition | Layout, crop, treatment, the credit |
| Fails toward | Draw the material procedurally | **Say the photograph is missing** |

**The last row is the one to remember.** When requisition fails you can draw the oak yourself. When a portrait fails there is no equivalent — a face you draw is not that person — so the honest output is a graphic that says the photograph was unavailable.

> [!CAUTION]
> **Never invent an image URL and never draw a placeholder face.** This is the same rule as `polson://manual/18` §4 on invented figures, and it is worse here, because the failure is undetectable. A fabricated number looks wrong to someone who knows the domain; a photograph of the wrong person looks *exactly* like a photograph of the right person. Nothing downstream — not a reviewer, not `bitmap.diff`, not the director glancing at a render — will catch it.

---

## 2. Identity Is the Failure Mode, Not Coverage

> **Implemented by**: `Photo.resolve(subject, { expect })`, `subject.description`, `subject.alternatives`, `subject.isDisambiguation`, `subject.title`, `subject.query`.

Finding *a* photograph is easy. Finding *the right one* is the whole problem, and it fails silently.

`Photo.resolve(...)` is **free and charges nothing**, so use it whenever you are not certain:

```js
const who = await Photo.resolve('Jordan Baker');
log(`${who.title}: ${who.description}`);          // "British racing driver (born 1991)"
log(`also matched: ${who.alternatives.join(' / ')}`);
```

Three things make a wrong subject visible instead of silent:

- **`subject.description`** — the subject's own one-line description. These are reliably diagnostic: *"American actress (born 1997)"*, *"Volcano in Japan"*, *"Bridge in the San Francisco Bay Area"*.
- **`subject.alternatives`** — the runners-up. This is what turns a refusal into a remedy.
- **`subject.isDisambiguation`** — the name matched a disambiguation page, which is never a subject.

**Assert what you are asking for.** `expect` is a word that must appear in the description:

```js
const photo = await Photo.of('Jordan Baker', { expect: 'actress' });
// failureName: 'WrongSubject'
// error: "'Jordan Baker' is described as 'British racing driver (born 1991)', which does not mention 'actress'."
```

A one-word assertion costs nothing and converts the undetectable failure into a named one. **Set it on every call where you know the category** — which, in a graphic about actresses, is every call.

> [!IMPORTANT]
> **An ambiguous name is refused, not guessed.** Asked for `Georgia`, the surface returns `Ambiguous` and names `Georgia (country)` and `Georgia (U.S. state)` rather than picking one. Qualify the query; do not relax the gate.

**The resolution travels with the delivery.** A successful `Photo.of(...)` carries the same record on `photo.subject`, so a later stage — or the run record, or a caption — can state *which* Zendaya it drew without re-resolving:

```js
Stage.note(`portrait: ${photo.subject.title} — ${photo.subject.description}`);
```

---

## 3. The Terms Arrive With the Picture

> **Implemented by**: `photo.licence`, `photo.creditLine()`, `Photo.credits()`, `licence.name`, `licence.artist`, `licence.attributionRequired`, `licence.restrictions`, `licence.usageTerms`, `licence.credit`, `licence.descriptionUrl`, `licence.isStated`.

A photograph is someone's work, and using one without its terms is the thing `reference/README.md` exists to prevent for books. Every delivery carries them.

```js
const photo = await Photo.of('Zendaya', { expect: 'actress' });
log(photo.licence.name);                  // "CC BY-SA 4.0"
log(photo.licence.artist);                // "PhilipRomano"
log(photo.licence.attributionRequired);   // true
log(photo.creditLine());                  // "Photo: PhilipRomano, CC BY-SA 4.0"
```

**Set the credits as part of the design, not as an afterthought.** `Photo.credits()` returns every distinct credit the artwork owes, deduplicated, ready to stack:

```js
const credits = Photo.credits();
ctx.font = '400 11px sans-serif';
const [block] = Layout.stack(footer, [credits.length * 14]);
credits.forEach((line, i) => ctx.fillText(line, block.x, block.y + i * 14));
```

Three points that decide whether a graphic is publishable:

- **An unstated licence is refused by default.** It is not a permissive one. `allowUnstatedLicence` exists so the decision is explicit and recorded, not so it is convenient.
- **`requireLicence` matters for commercial work.** Cropping a photograph into a graphic is plausibly an *adaptation* rather than a collection, which would pull a share-alike obligation onto the finished artwork. `{ requireLicence: ['CC0', 'CC BY'] }` avoids it. Matching is at a word boundary, so `CC BY` accepts `CC BY 3.0` and refuses `CC BY-SA 4.0`.
- **`licence.restrictions` is not the licence.** A value of `personality` means the subject's own publicity rights bear on the use — which a permissive copyright licence does not settle, because it is not the photographer's to give. Read it before putting a likeness in anything commercial.

> [!TIP]
> `photo.creditLine()` returns an **empty string** when nothing is known, rather than `Photo: unknown`. Test it and omit the line, instead of printing a claim you cannot support.

---

## 4. Laying Out Faces That Do Not Agree

> **Implemented by**: `photo.aspectRatio`, `photo.width`, `photo.height`, `photo.toDataUri()`, `photo.mimeType`, `photo.bytes`, `photo.sourceUrl`, `photo.id`, `photo.fetchedUtc`, `photo.requester`, `photo.source`, `subject.file`, `subject.imageUrl`, `subject.sourceWidth`, `subject.sourceHeight`.

Photographs of different people are not the same shape. Measured across a real set: five portraits ran **0.67 to 0.82** in aspect ratio, and two landmarks ran **1.60 to 2.05**. A row that draws each at its natural proportions reads as an accident.

**Crop to a common frame; never stretch.** Clip to the cell and draw the picture oversized, anchored where the face is:

```js
const bitmap = Skia.Image.fromDataUrl(photo.toDataUri());
const cell = Layout.rect(x, y, 160, 200);

ctx.save();
const frame = new CanvasPath();
frame.rect(cell.x, cell.y, cell.width, cell.height);
ctx.clip(frame);

// Cover the cell: scale on the tighter axis, then bias upward, because a portrait's
// subject sits above centre and a centred crop takes the chin off.
const scale = Math.max(cell.width / bitmap.width, cell.height / bitmap.height);
const w = bitmap.width * scale, h = bitmap.height * scale;
ctx.drawImage(bitmap, cell.cx - w / 2, cell.y - (h - cell.height) * 0.25, w, h);
ctx.restore();
```

> [!WARNING]
> **There is no face detection anywhere in this stack.** The upward bias above is a heuristic that suits most portraits and will eventually decapitate someone. Look at the render — this is exactly what `polson://manual/15` is for — and adjust the anchor per subject when a face lands wrong. A crop is a decision, not arithmetic.

**`width` is a request, not a guarantee.** The source renders at standard sizes and rounds up: ask for 400 and a 500-pixel rendition arrives; ask for 800 and you get 960. `photo.width` and `photo.height` are measured from the decoded bytes and are always true, so size your layout from those rather than from what you asked for.

**Treat a photograph as a layer, not as the drawing.** Duotone it, posterise it, knock it back behind drawn geometry, or trace over it — a photograph dropped unaltered into a drawn graphic reads as pasted in from somewhere else. `ctx.colorFilter` and `bitmap.applyColorFilter(...)` are how a set of photographs from different years and venues is made to look like one set.

> [!WARNING]
> **If the deliverable is an SVG, pass the photograph object to `paper.image(...)` — never a path.**
>
> ```js
> paper.image(photo, x, y, w, h);                 // inlined as a data URI — works everywhere
> paper.image('artifacts/portrait.png', x, y);    // resolves only when the SVG is opened as a document
> ```
>
> An SVG loaded through `<img>` or a CSS background fetches **no** external resources, so a referenced photograph is simply absent — even with the file sitting next to it. This renderer does not fetch them either, so your peek shows a broken-image cross while the run reports success. Full treatment, including what the base64 actually costs, in `polson://manual/14` §8a.

---

## 5. Budget, Cache and Failure

> **Implemented by**: `Photo.budget`, `Photo.library`, `Photo.isAvailable`, `photo.success`, `photo.failure`, `photo.failureName`, `photo.remedy`, `photo.retryable`, `photo.error`, `photo.fromCache`, `photoBudget.total`, `photoBudget.spent`, `photoBudget.remaining`, `photoBudget.cacheHits`, `photoBudget.bytesFetched`, `photoBudget.canAfford(...)`, `subject.success`, `subject.failure`, `subject.failureName`, `subject.remedy`, `subject.retryable`, `subject.error`, `subject.licence`.

Nothing here throws. Check `success`, then act on `remedy`:

```js
const photo = await Photo.of('Zendaya', { expect: 'actress' });
if (!photo.success) {
    error(`${photo.failureName}: ${photo.remedy}`);
    Stage.note(`no portrait for Zendaya (${photo.failureName}) — labelling the cell instead`);
}
```

**Read `retryable` before doing anything twice.** It is false for everything decided locally or by the subject's own data — a refused licence, a missing lead image, an ambiguous name — because those return the same answer however often you ask. Only transport faults are worth repeating.

**What is charged, and what is not:**

- **Resolution is free.** `Photo.resolve(...)` moves no pixels and is never charged. Check identity as often as you like.
- **A refusal is free.** Every gate — ambiguity, `expect`, licence — runs *before* the budget is touched.
- **A repeat is free.** The session cache is keyed on the subject and width, and a hit sets `photo.fromCache`.
- **A failed fetch is charged.** It still consumed a request; an allowance counting only successes could be overrun without limit.

**Check `Photo.library` before requisitioning a near-duplicate**, and check `Photo.budget.canAfford(n)` before a run of portraits, so you discover a shortfall while you can still change the design rather than halfway through a row.

> [!IMPORTANT]
> `Photo.isAvailable` being `false`, or a `NotConfigured` failure, means the studio has no photograph source at all. That is not licence to substitute something — it is the case where the graphic says the portrait is unavailable, or the design changes to one that does not need likenesses.

**A whole portrait cell, including the case where there is no portrait.** This is the shape to write: one branch draws the likeness, the other states its absence, and both produce a finished cell. Nothing here needs a photograph to be available in order to render.

```javascript
// A portrait cell that tells the truth when there is no portrait to show.
const canvas = createCanvas(360, 300);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f4';
ctx.fillRect(0, 0, 360, 300);

const cell = Layout.inset(Layout.rect(0, 0, 360, 300), 24);
const [frame, caption] = Layout.stack(cell, [190, 56], 14);

const photo = await Photo.of('Zendaya', { expect: 'actress', width: 400 });

ctx.save();
const clip = new CanvasPath();
clip.rect(frame.x, frame.y, frame.width, frame.height);
ctx.clip(clip);

if (photo.success) {
    // Cover the frame, biased upward: a portrait's subject sits above centre.
    const bitmap = Skia.Image.fromDataUrl(photo.toDataUri());
    const scale = Math.max(frame.width / bitmap.width, frame.height / bitmap.height);
    const w = bitmap.width * scale;
    const h = bitmap.height * scale;
    ctx.drawImage(bitmap, frame.cx - w / 2, frame.y - (h - frame.height) * 0.25, w, h);
} else {
    // No likeness available. Say so — never a stand-in face, never an invented URL.
    ctx.fillStyle = '#ece7dd';
    ctx.fillRect(frame.x, frame.y, frame.width, frame.height);
    ctx.fillStyle = '#8a94a0';
    ctx.font = '400 13px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('portrait unavailable', frame.cx, frame.cy);
}
ctx.restore();

ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillStyle = '#1c2733';
ctx.font = '600 18px sans-serif';
ctx.fillText(photo.subject.title ?? photo.subject.query, caption.x, caption.y);

// The credit when there is one; the reason when there is not. Both are worth printing.
ctx.font = '400 11px sans-serif';
ctx.fillStyle = '#8a94a0';
ctx.fillText(photo.success ? photo.creditLine() : photo.failureName, caption.x, caption.y + 24);

return canvas;
```

---

## 6. When Not to Use a Photograph

The surface exists for the case where identity matters. Most graphics are not that case, and reaching for a photograph is often a way of avoiding a design decision.

Prefer a drawn or typographic treatment when:

- **The subject is a category, not an individual.** "A nurse", "a commuter", "a city" — a specific real person or place, used generically, is both a licensing risk and a weaker graphic than a drawn figure. `Drawing.createMannequinFigure(...)` and `polson://manual/08` are for this.
- **The photograph is decorative.** If removing it costs the reader nothing, it was costing them attention for nothing. This is `polson://manual/13` §3's erasing pass applied to pictures.
- **The mark must survive monochrome or a small size.** A face at favicon scale is a smudge. `Logo.generateFaviconScaleTest(...)` will show you.
- **The subject is a living private individual.** Notability is what makes a stated licence and a public description available in the first place; its absence is a signal, not an obstacle to route around.

> [!TIP]
> A ranked list of people is the case that most often *does* justify photographs — the reader is scanning for a face they recognise, and that recognition is the graphic's whole mechanism. Even then, one consistent treatment across all of them beats five faithful but mismatched snapshots.

---

## 7. One Portrait, Every Angle — the Face as a Surface

> **Implemented by**: `Mesh.load(...)`, `mesh.fitTexture(...)`, `mesh.fitOutline(...)`, `mesh.landmark(...)`, `mesh.uvAt(...)`,
> `mesh.vertex(...)`, `mesh.boundary()`, `mesh.clone()`, `mesh.bounds`, `mesh.vertexCount`, `mesh.uvSpace`,
> `mesh.triangleCount`, `mesh.textured`, `mesh.source`, `Mesh.fromObj(...)` and `Mesh.draw(...)`.

> **Source Reference**: Jared Sanson & Richard Green, *Face Replacement Demo using the Kinect Depth
> Sensor* (COSC428 Computer Vision, University of Canterbury) — the pipeline of a parameterised mesh,
> UV-mapped to a face image, deformed and drawn. The mesh itself is yours to supply; see §7d.

### 7a. The problem it solves, which is consistency rather than realism

The studio has two ways to put a face on a page and **neither gives you the same face at a second
angle.**

- The **constructed** head (`polson://manual/23`) is fully articulate and completely consistent —
  a `const` of parameters makes panel 1 and panel 40 the same person. But it draws a *drawing*, and
  its turn is an approximation that its own documentation limits to about 40°.
- The **arranged** route — a requisitioned `Assets.cutout` — gives you a picture you could not draw,
  and then cannot change it. Generation is not deterministic across calls, so **a seventh expression
  is a new man**. You must plan every pose before the first call.

This route takes **one** frontal image and gives it a surface. After that the face turns, reshapes and
takes an expression, and it is the same face every time **because there is only ever one of them.**
Identity stops being something you hope the model holds and becomes structural.

### 7b. Three landmarks, and where they come from

`fitTexture` asks for three points in the image's own pixels — the two eyes and the mouth — and
solves a similarity transform from them, so scale and position fall out of the fit.

> [!IMPORTANT]
> **There used to be no face detector anywhere in this stack, and that sentence appeared in four
> places in the documentation. It is no longer true.** `Face.detect(image)` returns 478 landmarks,
> and **`mesh.fitDetected(image, detection)` does this whole section in one call** — it resolves each
> of the three anchors by *position* through `landmark(x, y, z)` on the mesh in hand, then reads that
> index out of the detection. Nothing is read by eye and no vertex number is remembered.
>
> **It is optional and it says so.** `Face.available` is the gate, for the reason `Skia.tracer` has
> one: a route that needs landmarks should find that out before it spends its passes. The three
> routes below remain exactly right when it is absent — and route 1, a face the studio drew, is still
> free and exact rather than merely accurate.
>
> **One thing the detector will do that nothing warns you about**: a face filling the whole frame is
> **not found at all**. The window is roughly 35–70% of frame width, and `Assets.cutout` trims every
> cell to its own extent, so a cutout portrait sits at 100% and fails. `detect` pads and retries for
> you, and reports what it needed as `pad`. A face too *small* in frame it cannot rescue, because
> cropping to a face means already knowing where it is.

> [!IMPORTANT]
> **`mesh.landmark(x, y, z)` is not that, and the two are one word apart.** It is a nearest-vertex
> search over the *loaded geometry* — *which vertex of this mesh is closest to this point in its own
> 3D model space* — and it never looks at an image. It exists so that no vertex index is ever quoted
> from memory. The **only** thing we hold from MediaPipe is `canonical_face_model.obj`, a static
> neutral mesh; the ledger's own row for `modules/face_landmark/` records that it is graph wiring
> with **no model binaries and no data of any kind**, the `.tflite` files being fetched at build time.

#### 7b-i. Reading the detector, and what each field is for

```js
if (!Face.available) {
    Stage.note(`no landmark backend here (${Face.missing}) — reading the three points by eye`);
} else {
    Stage.note(`landmarks from ${Face.python} against ${Face.model}`);
    const found = Face.detect(plate);
    if (!found.found) exit(found.reason);
    const fitted = mesh.fitDetected(plate, found);
}
```

**`Face.available` is the gate and `Face.missing` is the explanation.** The two never disagree — one
is exactly the other being null — so a script can branch on the first and print the second. What is
missing is usually the venv or the model rather than both, and saying which saves a guess.
**`Face.python`** and **`Face.model`** name what would actually be used; put them in a stage note, for
the same reason `Skia.tracer.path` is worth recording: a run that used a different backend than you
think is otherwise indistinguishable from one that did not.

**`detection.found` is the first thing to read, and `detection.reason` is why not.** A missed face is
a *result*: only an absent backend throws. **`detection.count`** is 478 with this bundle — the
468-vertex base mesh plus five iris points per eye — which matters the moment you pair it against a
canonical model, because that has 468 and indexing past the end would fit to whatever happened to
exist. **`detection.at(i)`** gives one landmark in the image's own pixels and answers **null** rather
than throwing when `i` is outside the list, because a caller pairing a mesh against a detection is
asking a question. **`detection.bounds`** is the whole extent, and **`detection.width`**,
**`detection.height`** and **`detection.pad`** say what image it measured and how much framing it had
to add.

**`detection.yawDeg`, `detection.pitchDeg` and `detection.rollDeg` are the source's pose, and they are
pose ONLY.** They come off a transformation matrix whose own solver header states its three
components as *uniform scale, rotation, translation* — `R`, `s`, `t` in CANDIDE's
`g' = R·s·(g + S·σ + A·α) + t`, with the deformation terms absent. Their real use here is a check
rather than an input: `fitTexture` has **no rotation term**, so a source much off frontal is one the
fit cannot straighten, and this is how you find that out before spending a texture on it.

**`detection.blendshapes` is 51 ARKit-named coefficients** — the same vocabulary `Mesh.draw`'s
expression units took in §7d-ii, so a reading transfers without translation. Treat it as a *read* of
the source rather than as a control: it says what the face in the photograph is doing.

> [!IMPORTANT]
> **Expect `eyeLookUp` around 0.6 on anything the studio drew, and do not chase it.** Measured on
> three of our own rendered faces and on a generated comic portrait: every one came back
> `eyeLookUpLeft`/`eyeLookUpRight` at 0.4–0.7 while looking straight ahead. It is correct. Our eye
> convention puts the pupil high — Gautier's *"the pupil seems to hang from the upper lid"* — with an
> iris the ledger measures at **1.70× life size**, and a big iris riding high in the aperture *is*
> looking up to a regressor trained on photographs. The detector is reading a deliberate house style.

**So how are the three points actually got?** Three routes, and only the middle one involves looking:

1. **A face the studio drew** — free and exact, below.
2. **A portrait you were handed** — read them off it. An agent can see the image, so this is ordinary
   looking; putting a labelled coordinate grid over it at 4–6× first turns a guess into a reading.
3. **Ask a model** — `Documents.ask` takes a PNG and could be asked for the pixels. Metered, and
   **untested for this**; a route rather than a recommendation.

> [!TIP]
> **Drawing the mesh over the image is how you check a fit, not how you find the points**, and the
> cheaper check needs no render: `uvAt` reports where each vertex landed, so a fit is verifiable as
> arithmetic. `wireframe: true` is for when you want to see it.
>
> **And a misread costs about what it was worth.** Perturbing one landmark at a time across a 5×
> range, the ratio of worst drift elsewhere on the face to the size of the error holds **constant** —
> 1.02× slipping an eye vertically, 1.65× horizontally, 2.05× for the mouth. A similarity transform
> from three points cannot amplify, so there is no cliff: read them to within a few percent of the
> eye separation and the face lands within a few percent. Pinned by a test rather than argued here.

For a photograph you read them off it. **For a face the studio drew itself they are free**, which is
the asymmetry worth exploiting:

```javascript
// A mesh is normally loaded — `Mesh.load('models/face.obj')`. Built inline here so the example
// runs anywhere, since the toolkit deliberately ships no face data (see below).
const cols = 9, rows = 11, obj = [];
for (let r = 0; r < rows; r++)
    for (let c = 0; c < cols; c++) {
        const x = -6 + (12 * c) / (cols - 1), y = 8 - (17 * r) / (rows - 1);
        obj.push(`v ${x.toFixed(3)} ${y.toFixed(3)} ${(7 - (x * x + y * y * 0.35) * 0.12).toFixed(3)}`);
    }
for (let r = 0; r < rows - 1; r++)
    for (let c = 0; c < cols - 1; c++) {
        const a = r * cols + c + 1, b = a + 1, d = a + cols, e = d + 1;
        obj.push(`f ${a} ${d} ${b}`, `f ${b} ${d} ${e}`);
    }
const mesh = Mesh.fromObj(obj.join('\n'));

// Any frontal face image will do. Here the studio draws one, which is the case where the three
// landmarks the fit needs are free rather than read off a photograph.
const face = createCanvas(512, 512);
const fctx = face.getContext('2d');
fctx.fillStyle = '#efd9c0';
fctx.fillRect(0, 0, 512, 512);
const head = Drawing.createLoomisHead(256, 268, 420);
Drawing.drawComicBrow(fctx, head.farBrow, true);
Drawing.drawComicBrow(fctx, head.nearBrow, false);
Drawing.drawComicEye(fctx, head.farEye, true);
Drawing.drawComicEye(fctx, head.nearEye, false);
Drawing.drawComicNose(fctx, head.noseWedge);
Drawing.drawComicMouth(fctx, head.mouthGuides);

const fitted = mesh.fitTexture(face.toBitmap(), {
    eyeLeft: head.farEye.center,          // the construction already knows where they are
    eyeRight: head.nearEye.center,
    mouth: head.mouthGuides.center
});

const canvas = createCanvas(760, 340);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, 760, 340);

Mesh.draw(ctx, fitted, { x: 170, y: 190, scale: 15 });
Mesh.draw(ctx, fitted, { x: 400, y: 190, scale: 15, yawDeg: 28 });
Mesh.draw(ctx, fitted, { x: 620, y: 190, scale: 15, pitchDeg: -20, side: 'near',
          expression: { browDown: 1, browKnit: 0.9, eyeSquint: 0.55, mouthFrown: 0.85 } });
log(`${fitted.vertexCount} vertices, ${fitted.triangleCount} triangles, textured ${fitted.textured}`);

canvas;
```

That is a **comic head that turns in real 3D** — the constructed route's articulation, with the mesh
route's rotation.

### 7c. What the fit absorbs, and what it does not

Measured rather than assumed, on one portrait at three framings:

- **Scale and position: absorbed completely.** The same photograph at 100% and at 42% pushed into a
  corner produces the same head at the same size in the same place.
- **The face must be wholly inside the image.** A crop that runs off the edge has no pixels to
  sample and smears — a limit of the picture, not of the fit.
- **No rotation term**, so a tilted head is not straightened.
- **Only two internal ratios are pinned**, eye separation and eye-to-mouth. Every other proportion
  is the mesh's, so a face built to other proportions is redistributed onto this one.

> [!IMPORTANT]
> **A cartoon face needs `fitOutline` or the mesh imposes a human head on it.** Texturing alone puts
> a drawing's features in the right places and then cuts them out with an average human mask.
> Measured on our own comic head: the eyes, brows, nose and mouth all read correctly and turn
> convincingly, and **the jaw and cranium do not survive at all** — the outline belongs to the mesh.
>
> `fitOutline(geo.mass)` moves each boundary vertex out along the ray from the face's centre until it
> meets the drawn outline, and carries the interior with it. Use `mesh.boundary()` to see which
> vertices those are; it is computed from the triangles — an edge belonging to exactly one triangle
> is a boundary edge — rather than listed, so it is right for whatever mesh was loaded.

### 7d. The mesh is yours to supply, and the reason is the licence

`Mesh.load(...)` and `Mesh.fromObj(...)` read an OBJ; **the toolkit ships no face data.** That is a
deliberate refusal rather than an omission, and the reason is a split worth knowing before you go
looking for a model:

**The mesh with the deformation data states no licence, and the mesh with a licence states no
deformation data.** CANDIDE-3's `.wfm` carries shape and animation units — the whole parameterisation
— and Ahlberg's own report grants nothing, saying only that the model is *"publically available"*.
MediaPipe's canonical face model is Apache 2.0 and carries a full UV map, and ships a **neutral**
surface with no units at all. So whichever you pick, **the displacement layer is ours to author** —
which is exactly what `Mesh.draw`'s `shape` and `expression` units are.

> [!IMPORTANT]
> **Those units are the studio's, tuned by eye, and they are not CANDIDE's.** Six shape units against
> that file's 38, and twelve expression units against `applyActionUnits`'s eight named muscles.
> **Anything offering a measured decimal for them is claiming more than any source in `reference/`
> supports.** This route buys rotation and identity from one image; it does not buy the articulation
> the constructed head has, and `Drawing.applyActionUnits(...)` remains the better tool for a
> performance.

### 7d-i. What an angry face needs, and what it took three units to reach

The set opened at five units — `mouthStretch`, `jawOpen`, `browRaise`, `browDown`, `eyeSquint` — and
the gap in it was not obvious until somebody asked for anger. **`browDown` on its own lowers a *flat*
brow, which reads as sulking, and nothing in the set turned a lip down at all.** The three that
closed it, on 2026-09-19:

- **`mouthFrown`** — Triangularis, AU15. Loomis's "unhappy muscles", running from beside the
  nose down to the jaw. Its weight rises to the mouth's corner and **falls away again past it**,
  because the mouth band alone reaches the cheeks: weighting purely by distance from the midline
  drags half the face down with the lip.
- **`browKnit`** — the inward half of AU4. Corrugator draws the brow *heads* together, so the weight
  peaks at the head of the brow and is zero at the midline, which has nothing to move toward.
- **`side`** — `'near'`, `'far'` or `'both'`, on `Mesh.draw` rather than in the unit list.

> [!IMPORTANT]
> **`browKnit` is a separate unit from `browDown` rather than folded into it**, which is where this
> surface departs from the constructed head: there, `AU4` lowers *and* knits in one weight. Splitting
> them is what lets a **raised** brow also be knitted — frontalis lifting against corrugator, which is
> the strained flat brow of fear rather than the clean arch of surprise. `applyActionUnits` reaches
> the same expression by a different route; this one reaches it by composition.

> [!IMPORTANT]
> **`near` and `far` are the sides of the *page*, and `left`/`right` are refused by name** — the same
> decision, and the same refusal message, as `applyActionUnits`. `near` is the `+x` side of the mesh's
> own facial axis at every yaw, so calling it the character's left would be a claim a turned head
> cannot keep. `jawOpen` ignores the option, because a jaw does not drop on one side.
>
> It exists because **one raised eyebrow and a one-sided smirk are the two most recognisable comic
> expressions there are**, and neither was reachable at any weight while every unit moved both halves.
> Three lineages split their brow units per side and this one did not: ARKit and MediaPipe carry
> `browDownLeft`/`browDownRight`, and `candide3.wfm` v3.1.6 carries an *Eyes vertical difference*
> shape unit. Asymmetry was the one axis every source had and this route had nowhere.

### 7d-ii. Borrowing ARKit's names, and what borrowing them does not buy

The units took ARKit's blendshape spelling on 2026-09-19 — `browDown` rather than `browLower`,
`eyeSquint` rather than `squint`, `jawOpen` rather than `mouthOpen`. It is the same move this studio
made with FACS's AU numbering: **a published vocabulary that other tools already speak, adopted for
the names alone.** Every old spelling still resolves and draws what it drew.

**The reason to write this down is the claim that comes attached to it.** The suggestion that
prompted the change — a commissioned model review — proposed applying ARKit coefficients to
MediaPipe's canonical mesh through a `blendshapeDeltas` table, which would have made the whole
displacement layer free. **There is no such table, in MediaPipe or anywhere else.** The blendshape
graph's own header states the direction: *"Predicts face blendshapes **from landmarks**"* — inputs
`LANDMARKS`, outputs a `ClassificationList` of 52 coefficients. It is a regressor that **reads** faces,
and what the file holds beside it is `std::array<string_view, 52>`: fifty-two strings. The model
producing the weights is a TFLite binary fetched at build time and not in the tree, and it runs the
wrong way for us regardless.

**ARKit does not close the gap either, and this is the part most often misread.** Apple's 52
blendshapes are an *interface specification*. ARKit hands an application coefficients; the
application's own rigged model carries the deltas. Anyone shipping ARKit-compatible geometry authored
or bought it.

So the asymmetry §7d opens with is unchanged: **the model with usable terms has no units, the model
with units states no terms**, and every magnitude here is still hand-written. Two of ours have no
ARKit counterpart at all — `browRaise`, which ARKit expresses only as inner and outer separately, and
`browKnit`, which ARKit folds into `browDown`.

> [!IMPORTANT]
> **A real ARKit name this geometry cannot show is refused *by name*, with the reason.** `cheekPuff`,
> `noseSneer`, `mouthPucker`, `eyeLookUp`, `jawForward` and the rest are correctly spelled, so a bare
> "not recognised" would send you hunting for a typo that is not there. **Accepting all fifty-two so
> that two thirds could quietly do nothing would be worse** — that is precisely the failure the
> literal-coordinate bug above produced, and it is the reason `applyActionUnits` names AU6, AU9 and
> AU17 in its own documentation rather than binding them to nothing.
>
> **And no unit carries a side in its name, where ARKit's do.** `browDownLeft` is refused and pointed
> at `browDown` with `{ side: 'near' }`, for the reason the note above gives: a left and a right are
> a claim about the character that a turned head cannot keep.

**What the borrowing did buy is resolution where this route was thinnest.** A brow that could only
rise as a whole cannot arch, and a lid that could only tighten cannot widen:

- **`browInnerUp`** (AU1) and **`browOuterUp`** (AU2) are the two ends of the brow, overlapping in the
  middle so the pair composes into an arch and each alone reads as one end lifting.
- **`eyeWide`** (AU5) and **`eyeBlink`** (AU45) act on the **upper lid alone**, which is what separates
  them from `eyeSquint` — orbicularis tightens both lids, so a blink is not a hard squint and a
  widened eye is not an un-squint.
- **`mouthSmile`** (AU12) takes the corners **out as well as up**, which is Loomis's observation rather
  than a detail: a corner lifted straight up reads as a smirk. **This route had no smile unit at
  all** until now — as CANDIDE-3 has none, a speech-coding selection rather than an expressive one.

`eyeWide` is worth one more line, because a second source lands on it. Gautier puts the **whole**
difference between surprise and fear in the sclera — the eyes widen more in fear, so more white shows
round the pupil — and warns on the same page that too much white *beneath* the pupil reads as
sinister (*Drawing and Cartooning 1,001 Faces*, Perigee 1993, pp. 28 and 80). That is one mechanism
read twice: the share of visible sclera is expressive, and it has a defect at one end.

> [!IMPORTANT]
> **Every station is a fraction of the mesh's own frame, and until 2026-09-19 it was a literal
> coordinate.** The brow band sat at `y = 3.6` — MediaPipe's number and nobody else's — so **a mesh
> authored at another scale got no deformation at all.** Every band fell outside its own geometry, and
> a call asking for anger returned a neutral face with no error and no warning, which is the worst
> shape a defect can take on this surface: the render is perfect and the expression is simply absent.
>
> Two things follow that are worth knowing before you load a model. The frame assumes the mesh's
> extent **is a face** — chin at the bottom, brow or forehead at the top — which is what every face
> mesh in `reference/` actually is, and is the only assumption available without semantic landmarks an
> OBJ does not carry. **Check it with the wireframe.** And the bands are measured against the geometry
> *as loaded*, never as deformed, which is what makes them survive `fitOutline`: that call moves
> vertices, and which vertex is a brow vertex is a fact about the anatomy rather than about where the
> brow has been pushed.

> [!IMPORTANT]
> **A likeness on a surface is still a likeness, and §2 and §3 of this manual still apply in full.**
> Nothing about mapping a face onto a mesh changes whose face it is. `Photo.of(...)` gates identity
> and terms before any bytes arrive, and it is the route to a real person; `Assets.cutout(...)` is the
> route to a generated one. **A borrowed portrait with no stated terms is not an input**, however
> convenient it is to have on disk — and a face that can now be turned and deformed is a stronger
> reason to be careful about that, not a weaker one.

### 7e. Wireframe first

`Mesh.draw(ctx, mesh, { wireframe: true })` draws the triangles as lines, which is how you check a fit
before spending a texture on it — the role `drawLoomisWireframe` plays for the constructed head. A
mesh with no texture draws as a wireframe whatever you pass, rather than silently drawing nothing.
> [!IMPORTANT]
> **`mesh.uvSpace` is the other thing to put in that note, and it is the one that was wrong.** A mesh
> holds its texture coordinates as **pixels** when `fitTexture` wrote them and as an **atlas** —
> `0…1`, `v` up from the bottom — when they came from an OBJ. Until 2026-09-19 the draw path assumed
> pixels, so a mesh drawn straight from its own file sampled the rectangle from `(0,0)` to `(1,1)`:
> **the whole head in one flat colour**, no error, nothing in the picture to say a mapping had
> failed. The atlas had been loaded correctly the whole time and nothing read it.
>
> **The atlas is the better surface for a plate the studio draws**, because a front projection cannot
> represent the sides of a head — triangles at the silhouette project to near-zero area, which is the
> smear on a turned panel. It is also the reachable half of the **shape-free texture** this route's
> literature is built on (Ahlberg, *EURASIP JASP* 2002:6, 566–571, crediting Ström et al. 1997): that
> warp exists to drag a *captured* image into canonical shape, and **a plate authored in atlas space
> is already there**. For a photograph it still needs a full landmark set, and §7b's first line
> stands — there is no face detector anywhere in this stack.

### 7c-i. Painting into the atlas, and the one thing that goes wrong

The whole route in four steps: ask the mesh where each feature sits with
`uvAt(landmark(...))`, draw the feature with the ordinary comic drawers, stamp it at that place, and
draw the mesh with the plate as its texture. Tested end to end with `drawComicEye`, `drawComicBrow`,
`drawComicNose` and `drawComicMouth` — the same head turns to **yaw 70**, near profile, and still
takes an expression and a one-sided brow.

> [!IMPORTANT]
> **An atlas is not an isotropic picture of the face, and stamping a plate as though it were is the
> mistake to expect.** An unwrap is laid out for texel budget and seam placement, not to look like a
> portrait, so *across the face* and *down the face* are not in the ratio a frontal view has.
>
> Measured on MediaPipe's canonical model at a 768px plate: eye separation **240.6px**, eye-to-mouth
> **193.5px**. The same two distances on a Loomis head are 114.3 and 123.8 — so the atlas runs
> **about 26% flatter**, and a feature scaled uniformly off the eye span arrives that much too tall.
> Drawn that way the first render put the **lower lip below the chin line** and stretched the nose
> into a streak; nothing errored, and it looked like the drawers were at fault rather than the
> stamping.
>
> Take **two** metrics — eye separation for `x`, eye-to-mouth for `y` — and scale each axis on its
> own. Both are free from `uvAt`. And **measure the atlas you loaded** rather than carrying those
> numbers across, for the same reason §7d gives for never quoting a vertex index from memory.

`mesh.textured`, `mesh.vertexCount`, `mesh.triangleCount`, `mesh.bounds` and `mesh.source` are what a
`Stage.note` should carry when a run uses this route, because none of it is visible in the render.

`mesh.uvAt(i)` is the finer check: it reports the pixel a vertex samples, so a fit can be verified
without rendering anything. Front projection is monotonic, so the mesh’s leftmost vertex must sample
left of its rightmost one — and drawing every `uvAt` over the source image is how you see, rather
than assume, that the map landed on the features.

### 7f. A skeleton, which a face does not have

> **Implemented by**: `mesh.posable`, `mesh.joints` and `mesh.pose(...)`.

This section is about a face, and the three calls above are about a body. They live here because they
arrived with the **file format** rather than with the subject: `Mesh.load(...)` reads glTF as well as
OBJ now, and a glTF can carry `JOINTS_0`, `WEIGHTS_0`, `inverseBindMatrices` and a node hierarchy —
things an OBJ has no way to express. One reader serves both, so one section documents both.

**Ask `mesh.posable` before planning a route that needs posing**, in exactly the spirit §7e asks you
to look at the wireframe first, and `Skia.tracer.available` asks you to check for potrace. **Most
assets answer no**, and the ones that answer no are the ones you are most likely to have: every OBJ,
and every mesh from a single-image generator, because those reconstruct a surface and do not rig it.
A script that commits to a posed figure and discovers this on its fourth pass has spent them.

**Read `mesh.joints` rather than guessing a name.** They come from whoever exported the file —
`mixamorig:LeftForeArm`, `J_Bip_L_UpperArm`, `bone_012` — and no convention spans the exporters, so
a guessed name is a guess about somebody else's tool. A name the file does not have is refused and
the nearest real ones are named, which is the same treatment §7d-ii gives an ARKit unit this geometry
cannot show, and for the same reason: silently accepting it leaves you looking at an unchanged figure
and blaming the renderer.

> [!IMPORTANT]
> **glTF does not require a node to be named, and a rigged file may have none.** Khronos's own
> `SimpleSkin` is two working joints and no names at all. Those come back as `node:1`, `node:2` (the
> number is the node's position in the scene), so `mesh.joints` always hands you something you can
> pass straight back. It also means **an empty `mesh.joints` is a statement about the rig, never
> about the naming**, which is precisely the confusion that would arise if the handles were not there.
>
> **Only the skin's bones are listed.** A scene node that is not a bone (the root, the armature
> object, the mesh's own node) is left out, and naming one to `pose(...)` is refused by name, because
> rotating it would move the whole model or nothing rather than a limb.

**Every pose is measured from the bind pose**, never from wherever the mesh already is. So posing a
posed mesh re-poses the original rather than accumulating, two poses taken from one character cannot
interfere, and the mesh you posed *from* is untouched. That is the same guarantee §7d-ii gives the
expression units — identity and performance stay separate — and it matters for the same reason: a
panel loop that drifted would drift silently, because every individual frame still looks reasonable.

> [!IMPORTANT]
> **This is linear blend skinning, and it does what linear blend skinning does.** A joint bent hard
> pinches on the inside of the bend. Clothing modelled onto a body deforms *with* the body rather
> than draping over it — so **none of Manual 22's fold mechanics are reachable this way**: no ring
> folds at a crushed sleeve, no folds radiating from a point of pull, because the cloth is not
> separate from the limb it is painted onto. Neither is a defect in the asset or in this toolkit;
> both are what the technique is, and knowing that is what stops an afternoon spent hunting a bug
> that is a method.

### 7g. A drawn face on a generated character

> **Implemented by**: `mesh.faceSheet(...)`, `mesh.withFace(...)`, `mesh.withExpression(...)`, and
> on the sheet `faceSheet.context`, `faceSheet.head`, `faceSheet.canvas`, `faceSheet.skin`,
> `faceSheet.greyscale` and `faceSheet.anchors`.

A body posed by §7f still has the face its generator painted, and **that face cannot act**. Measured
on a TRELLIS figure rigged by UniRig, the whole face is about 280 triangles carrying about 124 px of
texture: the eyes and mouth are paint on a few dozen triangles, so there is no geometry to move into
an expression, and a close-up shows a blur. This is the same limit that gives such a figure mitten
hands — the generator's voxel grid is coarser than a finger — and a bigger input image does not
change it.

**So the face is drawn and baked into the model's atlas rather than deformed.** `faceSheet()` finds
the eyes and mouth on the model, erases the old painted features over the character's own skin and
shading, and hands back a canvas whose context already carries the alignment. Draw the features of
`faceSheet.head` on it with the calls §7a–§7d use for any comic head, and `withFace(...)` writes the
result into a copy of the atlas. From then on the face belongs to the head: it turns, nods and tilts
with the rig, and the hair and a raised hand occlude it as the model does. There is nothing to line
up per panel, which is the whole difference from drawing a face over a rendered body.

**`withExpression(...)` is that in one call**, and it is the right default under a deadline:
`withExpression('anger')`, or Action Units such as `{ AU12: 0.9, AU2: 0.6 }` scaled by the second
argument. The features are drawn with the toolkit's comic calls, in greyscale when `faceSheet.greyscale`
says the character is — the drawers' colour defaults read as make-up, or as a moustache, on a
greyscale figure. Draw your own on the sheet when the character wants a face those calls cannot give.

> [!IMPORTANT]
> **Bake once, draw many times.** A bake is a new atlas, about 64 MB at the default 4096 px, and
> takes a fraction of a second; finding the face takes a few seconds the first time and is then
> cached. So bake the handful of expressions a sequence needs up front and keep them, then pose and
> draw per panel exactly as §7f does. **A pose keeps the baked face.**
>
> **Stage the head at three-quarters or nearer.** Measured on the character above, a baked face is
> sound to 60° of turn, including under a nod and a tilt; at 75° the far eye smears, and at 90°
> nothing is left. **The model sets that limit, not the bake**: its own painted face fails at the
> same angles, because a generated face is close to a flat plane with no nose standing out of it, and
> a profile is read from a silhouette that texture cannot give. For a true profile, draw that panel's
> head by construction (Manual 23). And a baked face changes the picture, never the shape — a dropped
> jaw moves no silhouette, and a scream that needs the chin to fall is beyond this route.
