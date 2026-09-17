# Studio Manual 20: Staging, Shots & Page Layout

> **Source Reference**: Klaus Janson, *The DC Comics Guide to Pencilling Comics* (Watson-Guptill /
> DC Comics, 2002) — §1 from ch. 9 (pp. 83–86), §2 and §3 from ch. 11 (pp. 100–105), §4 from ch. 10
> (pp. 87–99), §5 from ch. 8 (pp. 63–73). Distilled in our own words and cited by chapter; the book
> is all-rights-reserved and is not reproduced here.  
> **§6** is from Lex Sokolin, *How I Built Procedurally Generated Gothic Comic Compositions*
> (medium.muz.li, 15 January 2019) — distilled in our own words and cited by author and date. The
> post states no terms; handled as the Bokhua row. See the ledger in `reference/README.md`.  
> **Purpose**: How to *stage* a sequential image — where the camera goes, how far away, what the
> panel's shape says before its contents are read, and how the eye is steered through a page. This
> is the discipline the rest of the drawing manuals do not cover: they build and render a figure,
> this one decides what the frame does with it.

---

> **Why this manual is different from 09.** Manual 09 composes a *picture* — armatures, value
> hierarchy, focal emphasis. This one composes a *sequence*. Its unit is not the canvas but the
> panel-in-a-page, and almost every rule here is about relationships between frames rather than
> inside one. A picture can be beautiful and still fail here, by being unreadable in order.

---

## 1. Clarity first, and it is measurable

Janson's standard for sequential art is unusually testable: **the art must carry the story with the
words removed** (ch. 9). If a reader cannot follow what is happening from the pictures alone, the
storytelling has failed however well drawn it is. His analogy is a film watched with the sound off.

The second rule is the one that actually shapes a page: **every page must re-establish who, what,
where and when** — not just the first page of a scene. The reason is physical rather than
aesthetic: turning a page separates it from what came before, and a reader who has to look back has
left the story.

> **How to apply it here.** Two checks, both cheap:
>
> 1. **The silent test.** Render the board with every caption and balloon suppressed. If the
>    sequence no longer reads, the pictures are not doing the work. `ctx.measureWrappedText(...)`
>    already separates text from art, so drawing the board twice — once with, once without — costs
>    one extra render.
> 2. **The re-establish check.** Every page needs at least one panel that states the location. On a
>    generated board this is a countable property, not a matter of taste.

---

## 2. Shots — the distance from camera to subject

A "shot" is only about **how far away the camera is** (ch. 11). Janson's ladder, closest to widest,
with what each one is *for*:

| Shot | What it shows | What it is for |
| :--- | :--- | :--- |
| **Extreme close-up** | the face fills the frame | maximum intimacy and drama; the reader is pushed right up to the character |
| **Close-up** | shoulders to top of head | the whole face; background detail actively unhelpful — flat black or white is usually better |
| **Medium** | about half the body | the character is the subject; background optional, and a shadow or black shape often suffices |
| **Full** | roughly the whole body | still about the character, with a modest amount of location |
| **Long** | figures visible but not dominant | the location is the subject; figures read but do not carry the frame |
| **Extreme long** | figures tiny or absent | locale, scale, isolation — a town, a city, a planet |
| **Establishing** | enough to fix *where* | mandatory at the start of a scene, and re-confirmed at least once per page |

Two consequences worth stating plainly, because both cut against an instinct to draw more:

- **The closer the camera, the less background you should draw.** At close-up, background detail
  competes with the focal point and loses the panel. This is the opposite of the "fill the frame"
  reflex.
- **Staying wide keeps the reader safe, and safe is not what you want.** A sequence of long shots is
  both visually monotonous and emotionally distant. Moving the camera in is how a reader gets
  involved.

### Objective and subjective

Most panels are **objective** — a camera positioned in the scene, and the camera's position *is* the
reader's. A **subjective** shot shows what a *character* sees, which briefly makes the reader that
character. Janson's example: the detective searching a rooftop is objective; the blood he then sees
is subjective. It is the strongest tool for pulling a reader inside the action, and it works because
it is rare.

---

## 3. Angles — where the camera sits relative to the frame

Where a shot is about distance, an **angle** is about the camera's position relative to the panel's
own horizontals and verticals (ch. 11).

- **Tilted** — the frame's axis is diagonal. Reads as instability, movement, dizziness, something
  out of the ordinary. A shock reaction is nearly always drawn tilted.
- **Up-shot** — camera below, subject towering. Reads as power, strength, invincibility; the camera
  is submissive. Conventional for a hero or villain's first appearance, and effective in horror.
- **Down-shot** — camera above, looking down. Reads as defeat, powerlessness, isolation. Lets the
  reader see a beaten hero from the enemy's point of view.

> **These are cultural conventions, not laws — and that is precisely why you cannot ignore them.**
> Janson's caution is worth keeping: readers respond to these associations even unconsciously, so an
> angle chosen purely for looks can contradict what the panel is trying to say. Choose for a reason
> you could state.

### What the SDK gives you

The camera is not a concept the toolkit has, but its two components are:

- **Angle** is the perspective grid's own parameters —
  `Drawing.createPerspectiveGrid({ type, horizonY, cameraAngleDeg, tiltAngleDeg })`. A **horizon
  low in the frame** is an up-shot; **high** is a down-shot; a non-zero `tiltAngleDeg` is the tilted
  angle. That mapping is the studio's, not Janson's — he describes the camera, and this is where
  the camera lives in our API.
- **Distance** is subject height as a fraction of panel height. A workable table, again ours rather
  than his: extreme close-up ≈ 2.0, close-up ≈ 1.2, medium ≈ 0.9, full ≈ 0.8, long ≈ 0.35, extreme
  long ≤ 0.15. Use it to *size the figure to the shot* rather than drawing a figure and hoping.

### Choosing the shot in code

Shot distance and camera height are both numbers before they are drawings — pick them, then
size the figure to the shot rather than drawing a figure and hoping it reads.

```javascript
// Shot distance is subject height as a fraction of panel height, and camera height is where the
// horizon sits. Both are decisions you can make numerically before drawing anything.
const canvas = createCanvas(900, 380);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f1e8'; ctx.fillRect(0, 0, 900, 380);

const SHOTS = [
    { name: 'long',   fraction: 0.35, horizon: 0.42 },   // location is the subject
    { name: 'full',   fraction: 0.80, horizon: 0.42 },   // character, some location
    { name: 'medium', fraction: 1.60, horizon: 0.62 }    // over-tall: waist up, camera lowered
];

const page = Layout.inset(Layout.rect(0, 0, 900, 380), 20, 20, 44, 20);
const panels = Layout.columns(page, SHOTS.length, 16);

for (let i = 0; i < SHOTS.length; i++) {
    const shot = SHOTS[i], p = panels[i];

    ctx.save();
    const frame = new CanvasPath();
    frame.rect(p.x, p.y, p.width, p.height);
    ctx.clip(frame);

    ctx.fillStyle = '#e9e4d7'; ctx.fillRect(p.x, p.y, p.width, p.height);

    // Camera height IS the horizon: low horizon reads as an up-shot, high as a down-shot.
    const horizonY = p.y + p.height * shot.horizon;
    const grid = Drawing.createPerspectiveGrid({
        type: '1point', horizonY: horizonY, centerOfVisionX: p.x + p.width * 0.5 });
    Drawing.drawPerspectiveGrid(ctx, grid, {
        lineColor: '#d3ccbb', horizonColor: '#b9b09c', lineCount: 10, lineWidth: 1 });

    // Size the figure to the shot rather than drawing one and hoping.
    const figureHeight = p.height * shot.fraction;
    const footY = p.y + p.height * 0.92;
    const figure = Drawing.createMannequinFigure(
        p.x + p.width * 0.5, footY - figureHeight, figureHeight);
    Drawing.drawMannequinSolid(ctx, figure, {
        fillColor: '#f2ede2', shadowColor: '#b5ac9b', strokeColor: '#1d1d1b', strokeWidth: 2 });
    ctx.restore();

    ctx.strokeStyle = '#1d1d1b'; ctx.lineWidth = 2.5;
    ctx.strokeRect(p.x, p.y, p.width, p.height);

    ctx.fillStyle = '#39342c'; ctx.font = '400 14px Georgia, serif'; ctx.textBaseline = 'top';
    ctx.fillText(`${shot.name} — subject ${shot.fraction} of panel height`, p.x, p.y + p.height + 12);
}
canvas;
```

---

## 4. Composing the panel

### The centre is free, and that is the problem

Every panel has a centre — the intersection of its diagonals — and the eye goes there first whether
or not you put anything on it (ch. 10). Putting the important thing there guarantees it is seen.
But **a run of panels all centred reaches "critical mass of boredom"**: the reader's engagement
tracks how much their eye has to move.

So the working method is: place the first focal point, then let it decide the second, and the second
decide the third — deliberately varied across a tier, so the eye travels up and down as well as
across.

> **Implemented by**: `Drawing.createCompositionGrid(w, h, 'ruleOfThirds' | 'goldenRatio' | ...)`
> gives the armature and its power points; `Layout.grid(...)` and `Layout.rows/columns(...)` give
> the panels. The variation rule is a constraint *across* panels, which nothing in the toolkit
> enforces — track focal points yourself and check they are not collinear.

### Contrast: equal is not interesting

A panel split into two equal shapes is inert. **Unequal divisions carry tension**, and the same
applies to black against white. This is the sequential-art form of Manual 09's big/medium/small law
(`Drawing.subdivideProportions`), and Janson states it as bluntly as possible: *equal is not as
interesting as unequal*.

### Balance

- **Symmetrical** — one half mirrors the other across the axis.
- **Asymmetrical** — shapes of *unequal* size on either side of the division. Still balanced,
  because balance comes from the panel's structure rather than from matched masses.

### The diagonal is king

A line only counts as a diagonal **because the frame gives it something to be diagonal to** (ch. 10).
Inside a rectangle of horizontals and verticals, anything angled dominates. Four uses:

1. **Emphasis** — to make something noticed, draw it at an angle.
2. **Action** — an action built on a diagonal reads more strongly than the same action parallel to
   the borders.
3. **Leading the eye** — an angled shadow or edge points at the focal point and frames it at once.
4. **Depth** — perspective diagonals pull the viewer in. Blast lines are the minimal form of this:
   they point at the focal point and create depth at once, and are **drawn heavier at the panel
   border and thinner toward the centre**.

> **Implemented by**: `Drawing.drawLeadingLines(ctx, originPoints, focalPoint, options)` is the
> leading-the-eye case directly. Blast lines are the same call with origins on the border and the
> focal point at the centre — but note it draws a constant width, so the border-heavy taper needs
> `ctx.drawTaperedStroke(...)` per ray instead.

### The worked sequence

Janson's cowboy example (ch. 10) is a usable procedure, and it generalises beyond cowboys:

1. **List what the panel is required to contain** — the figures, their setting, the direction of
   movement. Required, not desired.
2. **Break the equal split.** A horizon at the halfway line gives two equal shapes; move it.
3. **Move the subject off centre**, and pick the offset that lets the eye leave the panel toward the
   next one rather than bouncing back into it.
4. **Separate subject from background shape.** A figure overlapping the sun's outline reads as in
   front of it; contained inside, it is swallowed.
5. **Add a foreground element** for depth.
6. **Introduce black** against the white for contrast.
7. **Break artificial regularity** — evenly spaced figures look placed rather than caught.

---

## 5. The page: layout and story flow

A page does two jobs (ch. 8): it arranges panels comprehensibly, and it **pulls the eye along a
designed route** — the *story flow*. The reader takes in the page as a whole before any single
panel, so **panel size and shape communicate before their contents are read**.

### Grid or free-form

- **Grid** — uniform panels. The design recedes and the reader attends to the art. Janson calls it
  the *harder* of the two, because a flashy layout can hide weak drawing and a grid cannot.
- **Free-form** — varied panels. The layout itself carries information: a **big panel means
  important, dramatic, and dwelt on longer**; a row of small panels means a quick sequence of
  lesser beats.

For a storyboard this is worth stating explicitly: a board is normally a grid, so **the panel
sizes are not available as a channel** and everything must be carried by shot, angle and
composition.

### Flow

The default is the **Z**: left to right, then left to right again. Departures are legitimate — down
then down again, or a route that leaves the page and re-enters — but the single requirement is
**clarity about what to read next**. If the order is ambiguous the layout has failed, whatever else
it achieves.

### Devices, and their costs

- **Insert panel** — a small panel inside or bridging larger ones. Placed *within* a panel it reads
  as belonging to it, and compresses elapsed time to near-simultaneous. Placed *between* two panels
  it acts as a directional guide into the next.
- **Breaking the border** — a shape crossing the frame edge pops forward and gains depth. The cost
  is that the reader follows its slant, so it steers the eye whether you meant it to or not.
- **Borders and gutters** — a white gutter with a thin black rule is the invisible default. A
  **black gutter** pulls panels inward and makes the page look smaller and busier, so compensate by
  drawing the interiors less densely. Double borders and hand-drawn borders single a panel out, and
  work *only* while rare.

> **Implemented by**: `Layout.rows/columns/grid(rect, divisions, gap)` for the page; the `gap` is
> the gutter. Gaps are taken out before dividing, so panels always sum back to the page — the
> commonest way a hand-rolled grid drifts. `Layout.bounds(...)` gives the union for a bleed or a
> border-breaking shape. **Read order is not something `Layout` knows**: row-major indexing happens
> to give the Z-flow, and any other route is yours to arrange and yours to verify.

---

## 6. The arranged route — composing from parts

Everything above assumes the picture is **constructed**: a figure from a canon, a head from
landmarks, a panel drawn. There is a second route, and it is a different trade rather than a worse
one — **arrange** a picture from parts that arrive from somewhere else, and spend the effort on where
they go rather than on what they are.

**The trade is measurable, and it is not the one people assume.** Across every run on disk, 599
script executions classified by what they actually call:

| what the script draws | median authoring | mean | p90 | median size |
| :--- | ---: | ---: | ---: | ---: |
| a face | 1.00 min | 2.19 | **5.71** | 22 KB |
| a body | 0.94 min | 1.75 | **5.97** | **52 KB** |
| composition | 0.89 min | 1.47 | 3.05 | 18 KB |
| lighting | 0.90 min | 1.34 | 2.70 | 21 KB |

**At the median there is no difference at all.** A face script takes a minute to write and so does a
lighting script. The difference is entirely in the tail: figure work has roughly **twice the p90** and
runs to **three times the code**. So this route does not buy back a constant tax — it buys out the
variance and the bulk, and it pays for that with articulation. It is the trade between working from a
model sheet and redrawing, and it belongs to storyboards, covers and thumbnails rather than to a
finished panel.

### Janson's sequence and the procedural formula are the same construction

That is the reason this section sits in this manual rather than in one of its own. §4's worked
sequence is craft advice arrived at by drawing; Sokolin's formula is a rule set arrived at by
analysing twenty years of Mignola covers in order to write software that generates them. They land
in almost the same place:

| §4, from the drawing side | the formula, from the code side |
| :--- | :--- |
| Break the equal split — move the horizon off the halfway line | the frame divides into **horizontal thirds**, never halves |
| Move the subject off centre | the **figure** sits low and off axis, in the bottom two thirds |
| Separate subject from background shape | a **diagonal** passes behind the figure rather than being contained by it |
| Add a foreground element for depth | a **foreground** slot that pulls the eye and hands it to the figure |
| Introduce black for contrast | the ground starts **near-black** and colour is cut into it |
| Break artificial regularity | **seeded variation**, so no two arrangements are placed identically |

*"The diagonal is king"* (§4) and the formula's background diagonal are the same observation. The
useful addition from the code side is **where** it goes: the upper two thirds, crossing behind the
figure's shoulder — which is §4's fourth point, *separate the subject from the background shape*,
stated as a placement rather than as a warning.

### The construction

`Scene.createLayeredScene(rect, options)` returns the arrangement as a model — it draws nothing, on
the same split as `createMannequinFigure` against `createFigureGeometry`.

| Layer | What goes in it |
| :--- | :--- |
| **background** | a **texture** mass in the upper reaches — architecture, columns, branches, armour — and the **diagonal** |
| **middle** | the **figure**, low in the frame, and meant to be the **brightest thing in it** |
| **foreground** | an object or decoration that pulls the eye and passes it to the figure, plus the reserved **title** band |

`slots` is one uniform entry per placeable thing (`{ name, layer, rect, depth }`), and **`order` is
depth here rather than construction order** — the one place in the SDK where it is. Paint in that
sequence and the layering is correct.

### The colour system

`Scene.createMoodPalette(options)` is five hues × **dark / mid / highlight**, and the placement rules
are the substance rather than the colours:

- The **figure** carries its hue's `highlight` and is the brightest thing in the frame.
- The **mood** hue is a *different* one and appears **twice** — on the background diagonal and again
  in the foreground — so the figure is held between two uses of it.
- Everything else starts at `ground`, which is near-black. **A composition that begins dark and has
  colour cut into it reads differently from one that begins light and has colour added**, which is
  §4's *introduce black for contrast* turned into a starting condition rather than a later step.

Asking for the same hue for figure and mood is refused: the figure has to stand against the ground it
is held between. The five defaults carry **no blue and no purple** — the source's selection rather
than a law of colour, and what makes the set read as one world. Pass `hues` to replace them entirely.

Distinct from `Drawing.createNotanPalette`, which is **tonal**: that answers how light and dark are
distributed, this answers which hue goes where.

### Variation has to be reproducible, or the run record is lying

**This is the part a procedural system gets wrong quietly.** The record's promise is that the script
in `scripts/` can be run again and give the picture under discussion — and a script drawing from
`Math.random` gives a different one every time, with nothing saying so. So the seed goes in the
script, where it is saved with everything else.

```javascript
const canvas = createCanvas(640, 900);
const ctx = canvas.getContext('2d');

const run = Random.seeded(1907);
const staging = run.fork('staging');
const palette = run.fork('palette');

const scene = Scene.createLayeredScene(Layout.rect(0, 0, 640, 900), { rng: staging });
const mood = Scene.createMoodPalette({ rng: palette });

Stage.note(`seed ${run.seed}: ${mood.figureName} figure against ${mood.moodName}, ` +
           `${staging.count} draws for the staging`);

ctx.fillStyle = mood.ground;
ctx.fillRect(0, 0, 640, 900);

for (const slot of scene.slots) {
    const r = slot.rect;
    if (slot.name === 'diagonal') {
        const d = scene.layers.background.diagonal;
        ctx.strokeStyle = mood.mood.mid;
        ctx.lineWidth = d.width * 0.5;
        ctx.beginPath();
        ctx.moveTo(d.from.x, d.from.y);
        ctx.lineTo(d.to.x, d.to.y);
        ctx.stroke();
    } else if (slot.name === 'figure') {
        ctx.fillStyle = mood.figure.highlight;
        ctx.fillRect(r.x, r.y, r.width, r.height);
    } else if (slot.name === 'foreground') {
        ctx.fillStyle = mood.mood.dark;
        ctx.fillRect(r.x, r.y, r.width, r.height);
    }
}

canvas;
```

**`Random.seeded(n)` gives a stream; `rng.fork(name)` gives an independent one named off it**, and the
fork is what keeps the composition editable. Drawn from a single stream the numbers are positional:
add one draw to the background and every later number shifts, so the figure moves and the palette
changes because you adjusted a smoke trail. Forking does not advance the parent, so adding a fourth
fork later changes none of the first three.

The rest of the surface is what you would expect — `rng.next()` for a unit number, `rng.range(a, b)`
and `rng.int(a, b)` for a bounded one, `rng.bool(p)` and `rng.sign()` for a coin, `rng.pick(list)` and
`rng.shuffle(list)` for a choice, `rng.gaussian(mean, sd)` for jitter that clusters rather than
spreading evenly, `rng.reset()` to rewind, and `rng.seed` and `rng.count` to report what happened.
`Random.hash(text)` turns a name into a seed when you want one without a parent stream.

> **Both calls draw from the generator a fixed number of times in a fixed order.** So `variation: 0`
> gives the canonical arrangement *without shifting the sequence* — you can tune how much randomness a
> scene carries and still recognise the composition you liked. A system that only drew when it needed
> to vary would make those two knobs interact, which is the commonest way a procedural system becomes
> impossible to tune.

### Tint the part whole; do not separate it into regions

**The temptation is to trace an imported drawing to vector and colour its parts separately. Do not
start there.** The source is explicit about why: cut-outs are kept as raster, **tinted whole**, and
given a deliberately flat light-and-shadow pass, because per-region work *"does not scale"* across
dozens of parts. The flatness is the style, not a compromise — and a flat tint is
`ctx.colorFilter` or `bitmap.applyColorFilter(...)`, which costs nothing.

Staying raster also keeps the cheap route open in the other direction: **an agent can see a region and
cut it** with `bitmap.extractSubset(...)` or a `ctx.clip(...)`, which is a judgment models are good at
and no amount of contour data supplies. Vectorising is worth it only when the **deliverable** is SVG
— see `polson://manual/14` — and then `bitmap.trace(...)` is there.

### Where the parts come from

`Assets.matte(descriptor, { hardEdge: true })` is the one requisition that will answer a **form**
rather than a material, so it is the natural source for a cut-out: a silhouette you place, tint and
light yourself.

**Prefer generating the part to sourcing a Creative Commons one**, at least until there is a reason
not to. A generated matte carries **no attribution obligation and no share-alike contamination**,
where a traced CC BY-SA engraving arguably makes the whole composition BY-SA. `Photo.of(...)` remains
the route for a real person, with its identity, licence and provenance gates — and a generated
likeness has none of them, which is why `Assets.matte` refuses a descriptor that names somebody and
asks for a face.

### A sequence is one space, not several arrangements

**`createLayeredScene` is for a single frame, and a storyboard is not one.** It gives every panel an
*independent* arrangement, which is the opposite of continuity — and a live `storyboard_quick` run
found that in ten minutes: it used the palette and the seeded generator, declined the scene template,
and hand-rolled a persistent room instead. It was right to.

So the sequence form is a different pair of calls:

```js
const set = Scene.createSet(Layout.rect(0, 0, 100, 60), {
    horizon: 0.55,
    elements: [{ name: 'window', x: 68, y: 8, width: 22, height: 20 },
               { name: 'table',  x: 10, y: 34, width: 26, height: 10 }]
});

const shot = Scene.createShot(set, panelRect, { shot: 'medium', focusOn: 'window' });
const here = shot.element('window');          // the same window, in this panel's coordinates
```

**A shot is a crop and a scale of the same space, never a redrawing of it.** There is one table, in
one set; a shot does not own it and therefore cannot move it. That matters more than it sounds,
because **continuity is the one thing no measurement in this studio can check** — `bitmap.diff` will
not tell you the room changed, and a reader will feel it immediately. Making drift impossible beats
remembering to avoid it.

`shot.point(x, y)` and `shot.place(rect)` project anything else you draw; `shot.shows(name)` asks
whether an element is in frame; `shot.view`, `shot.scale`, `shot.panel`, `shot.coverage` and
`shot.name` report what the camera did. The view is **clamped inside the set**, so a camera cannot pan
into space nobody described.

**The ladder is §2's, and `shot.backgroundDetail` carries §2's rule as a value rather than as prose** —
`none` at the two closest rungs, `full` at the widest. *The closer the camera, the less background you
should draw* cuts against the reflex to fill the frame, so it is worth handing to the caller as a
decision already made.

### What this route does not do

- **A shot cannot turn around.** The set describes what the camera faces, so a **reverse angle** is
  not a crop of it — and shot-reverse-shot is the basic grammar of two people talking, which makes
  this the limit most likely to be met rather than a curiosity. Describe both walls as two sets and
  shoot whichever the panel looks at, or draw the reverse by hand and say in a note that the panel is
  not a view of the set. A live run met this at `extremeCloseUp`, where it cost nothing because
  `backgroundDetail` is `none` there — and said so: *"that the manual's rule and the honest answer
  coincide here is luck, not method."*
- **It cannot repose a part.** A cut-out is fixed; a construction can turn its head. That is the
  whole of the trade, and it is why this suits a cover or a thumbnail better than a sequence where one
  character must be recognisable across eight panels from three angles. Reusing one part across panels
  is *more* consistent than construction — byte-identical geometry every time — and completely static.
- **The source is a cover formula**, generated one frame at a time. `createSet` and `createShot`
  above are this studio's answer for a sequence and are not from it; §5's page flow is addressed by
  neither.
- **Composition is not the same as arrangement.** This places masses. Whether the result *reads* is
  §1's question and still yours: squint at it, and check the silhouettes separate.

> **The one caution worth carrying from the source, in its own words**: *"Too much randomness and the
> output is nonsense. Too much control and the output is boring."* The work moves from making the
> particular image to setting the template and the parameters of variation — which is the same shift
> this studio makes everywhere else, and the reason the seed belongs in the record.

---

## 7. What this manual does not give you

Stated because the gap is real and an agent should not discover it by failing:

- **No posing.** `Drawing.createMannequinFigure(...)` builds a standing figure. Nothing here makes
  it sit, lean, reach or recoil, and shot selection assumes a subject that can be posed.
- **No drapery.** *Pencilling* ch. 5 covers clothing and has not been distilled.
- **No small-scale figure idiom.** These shots put figures at a fraction of panel height, where
  construction and modelling stop helping and silhouette and gesture take over. The toolkit's figure
  work is built for the large, near case.

Until those close, this manual is most useful for what it *does* determine: framing, camera
placement, panel geometry, focal placement, contrast, and read order — all of which are decisions
that can be made correctly in code even when the figure inside the frame is provisional.
