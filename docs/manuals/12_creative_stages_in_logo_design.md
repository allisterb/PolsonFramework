# Studio Manual 12: Creative Stages in Logo Design

> **Credits & Theoretical Foundation**: Synthesized from Tubik Studio's *Logo Design: Creative Stages* (by Marina Yalanska) and studio practice from leading identity design agencies. Tubik's seven stages are the spine of this manual. **Stage 2.5 is inserted from a different source**: the practice of generating concepts by working a list of rhetorical devices is taken from *Graphic Design and Print Production Fundamentals* §2.5 (Alex Hass, Graphic Communications Open Textbook Collective / BCcampus Open Education), used under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The device definitions and worked examples below are our own — that book's own definitions are its paraphrase of Harris (2013), which carries separate terms. **Stage 2.5b** follows George Bokhua's *Principles of Logo Design* ch. 4 ("Mood Boarding"), and **§4** distils his ch. 3 material on reviewing work — the mirror check, comparing versions side by side, the familiarity rule, and unintended readings. **Stage 4's** archetype table follows his ch. 2 taxonomy. All of it is synthesized rather than reproduced: that title is all rights reserved, so the principles are distilled in our own words and the chapter cited. Nothing here is quoted verbatim, including the rule stated in §4.C.
> **Purpose**: Tubik's seven stages for taking an identity from brief to validated mark, with the human-in-the-loop points marked. **One process among several, not a required sequence** — studios work differently, small jobs collapse stages, and the value here is knowing which decisions belong to the client and which to the designer.

---

## 1. Overview of the Creative Stages

> **Implemented by**: the stages map onto the toolkit in order — Manual 10 §2 for stage 3 geometry, Manual 11 §2 for stage 5 typography, `Logo.generateFaviconScaleTest(...)` / `Logo.generateMonochromeTest(...)` for stage 6, and `Logo.generateBrandPresentationSheet(...)` for stage 7.

```
   ┌─────────────────────────────────────────────────────────────┐
   │            STAGE 1: Setting the Task & Brief                │
   │  Client requirements, brand goals, audience, tone of voice  │
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼
   ┌─────────────────────────────────────────────────────────────┐
   │            STAGE 2: Research & Market Analysis              │
   │   Competitor landscape, category cliches, visual positioning│
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼
   ┌─────────────────────────────────────────────────────────────┐
   │              STAGE 2.5: Concept & Mood                      │
   │  2.5a one sentence: concept, device, what it rejects        │
   │  2.5b mood board: palette, type, shape language, compared   │
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼ [HITL Checkpoint: Concept & Mood]
   ┌─────────────────────────────────────────────────────────────┐
   │         STAGE 3: Creative Search & Geometric Ideation       │
   │  Gridding, Golden Ratio circles, superellipses, silhouettes │
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼ [HITL Checkpoint: Direction]
   ┌─────────────────────────────────────────────────────────────┐
   │      STAGE 4: Style Direction & Logomark Archetype          │
   │    Wordmark, Lettermark, Pictorial, Abstract, Emblem/Badge  │
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼
   ┌─────────────────────────────────────────────────────────────┐
   │       STAGE 5: Color Palette & Typographic Harmony          │
   │  Color psychology, Robin Williams contrast, Doyald Young kerning│
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼ [HITL Checkpoint: Refinement]
   ┌─────────────────────────────────────────────────────────────┐
   │     STAGE 6: Multi-Scale Stress Testing & Environments      │
   │   7-tier favicon ladder (16-256px), 4-way monochrome tests  │
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼
   ┌─────────────────────────────────────────────────────────────┐
   │      STAGE 7: Brand Presentation Board & Style Guide        │
   │  Horizontal/Vertical lockups, X-dimension clear space, usage│
   └─────────────────────────────────────────────────────────────┘
```

---

## 2. Stage-by-Stage Breakdown

> **Implemented by**: each stage below names its own calls. Stages 1, 2, 2.5a and 4 are research and judgement with no API surface — do not look for a call that makes those decisions for you. Stage 2.5b renders a board, but from canvas primitives rather than a dedicated call; there is no `generateMoodBoard`, and the absence is deliberate, since what belongs on the board is a judgement per project.

### Stage 1: Setting the Task (The Client Brief)
- **Objective**: Establish clear, unambiguous constraints before touching code.
- **Core Parameters**:
  - **Company & Product Name**: Exact spelling, casing, pronunciation.
  - **Industry & Domain**: Tech, aerospace, bio, finance, luxury, consumer.
  - **Target Audience**: Demographic, technical sophistication, emotional desires.
  - **Core Brand Values / Essence**: 3-5 adjectives (e.g. *Precision, Velocity, Elegance, Security*).
  - **Color & Style Preferences**: Warm vs. cool, minimalist geometric vs. ornate emblem.

### Stage 2: Research & Visual Positioning
- **Objective**: Identify market visual codes and avoid generic category tropes.
- **Rules**:
  - Analyze competitor marks to avoid unintentional resemblance.
  - Reject visual clichés (e.g., generic leaves for eco, generic globes for logistics).
  - Define the unique **metaphorical anchor** for the brand.

### Stage 2.5: Concept & Mood

Two decisions that must both be made *before* geometry: what the mark means, and how it should feel. Neither costs anything to change here and both cost a great deal to change after Stage 5.

#### 2.5a — The concept, in words

- **Objective**: State in words what the mark *means*, before any geometry exists.

**A concept is not a message.** The message is what the brand says — *"we deliver overnight, reliably."* The concept is the idea that carries it — *"the shortest line between two points."* The message is given to you in the brief; the concept is the thing you are actually being paid for, and it is what makes every later decision answerable instead of arbitrary.

Stage 2 ends by naming a metaphorical anchor. This stage is where that anchor becomes a sentence you can be held to. Skip it and Stage 3 begins with grids and golden ratios applied to nothing in particular — the characteristic failure of a mark that is technically immaculate and says nothing, and could carry any name in its category.

**Write one line and log it before drawing:**

```
CONCEPT: <one sentence — the idea, not the message>
DEVICE:  <the rhetorical device it turns on>
REJECTS: <the category cliché from Stage 2 that this avoids>
```

##### Devices, and what each one does to a brief

Rhetoric is the study of persuasion, and design borrows its figures wholesale. Each device below is a different *move* from message to image, so working the list is a way of generating genuinely distinct candidates rather than variations on the first thing you thought of. The right-hand column applies each to one brief — *a courier guaranteeing overnight delivery* — so the moves can be compared against each other rather than admired separately.

| Device | The move | On *"a courier guaranteeing overnight delivery"* |
| :--- | :--- | :--- |
| **Synecdoche** | A part stands for the whole | The wing alone, never the bird; the fletching, not the arrow |
| **Metonymy** | Something *adjacent* stands in — associated with the thing, not part of it | The postmark, the wax seal, the string tie of a parcel |
| **Metaphor** | Asserts one thing *is* another | The route *is* a comet: the parcel as a body with a trailing tail |
| **Analogy** | Explains the unfamiliar through the familiar | The delivery network drawn as a constellation — known shape, new content |
| **Allusion** | A brief nod to a shared cultural reference | Mercury's winged sandal, compressed to one heel and one wing |
| **Amplification** | Repeat an element, adding detail each time, to force notice | Three chevrons, each sharper and more raked than the last |
| **Personification** | Human character given to an object | The parcel leaning into its own motion, as if running |
| **Hyperbole** | Deliberate, *obvious* exaggeration | A speed line so long it wraps the entire mark |
| **Oxymoron** | Two contradictory ideas held in one form | A knot that is also an arrow — bound and released at once |
| **Understatement** | Deliberately says less than expected | One thin line ending in a dot. Arrival, nothing more |
| **Simile** | Both things stay visible, presented as alike | Parcel and paper plane side by side, sharing one silhouette |

Hyperbole is the most overused of these and the quickest to look cheap; understatement is the hardest to execute and the most likely to survive Stage 6, because it has the least to lose when the mark is shrunk to 16px.

##### Quota, not inspiration

Work the list. Do not wait for the idea to arrive.

- **Go broad first**: one candidate per device, judged afterwards, not during. A quota is the point — it keeps you generating past the comfortable stopping point, which is where the first non-obvious candidate usually appears.
- **Then go deep**: take the two strongest and express each several ways — different framing, different emphasis, different weight. The first expression of an idea is rarely its best one.
- **Do both.** Most designers naturally do one or the other; whichever comes easily to you, force yourself through the other as well.

Carry two or three concepts into Stage 3, not one. A single concept cannot be compared to anything, so its weaknesses stay invisible until far too late to fix cheaply.

- **HITL Interaction**: This is the cheapest checkpoint in the entire process — a concept can be rejected and replaced for the price of a sentence, whereas the same rejection after Stage 5 discards geometry, palette and typography together. Present the concept lines and let the director kill one *here*.

#### 2.5b — The mood board, rendered

- **Objective**: Fix the *stylistic* direction before geometry, the way 2.5a fixes the semantic one.

A mood board exists to make competing stylistic directions comparable side by side. Bokhua's instruction is to **compartmentalise** it — classic in one region, high-tech in another, colourful kept apart from monochromatic, so there are clear boundaries between directions rather than one undifferentiated wash of things you liked. That separation is the entire function. A board where the directions blur together has told you nothing, because nothing in it can be rejected.

> [!IMPORTANT]
> A traditional mood board is collected reference imagery — photographs of architecture, nature, painting. **You cannot collect those, and you must not generate them.** This is the point in the process where reaching for image generation is most tempting and least defensible: it would spend budget to produce pictures nobody ships, in a workflow whose entire premise is that form comes from code.
>
> Build the board out of the same material as the mark. A direction is legible from its palette, its typography and its shape language, and all three are things you can draw. That is also a stricter board than a collected one — every element on it is something you could actually use.

Each compartment carries:

- **A palette** — four swatches, dominant to lightest. Enough to see temperature and contrast, not so many that the direction becomes vague.
- **A type specimen**, set in a family you have confirmed exists with `Skia.Font.has(...)`. A substituted family makes the whole compartment a lie, since the specimen would show a face you cannot use. A fallback list in `ctx.font` would keep the *drawing* honest, but not the specimen — the board has to print which family it is showing, and only `has`/`resolve` can tell you which one the list landed on. Ask first, then set the font and the caption from the same answer.
- **A shape language** — one form stating the direction's stance on Bokhua's oppositions: sharp against round, solid against line, symmetric against asymmetric.

```javascript
// Stage 2.5b - three style directions, compartmentalised so they can be compared.
const W = 1080, H = 400;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#f4f5f7';
ctx.fillRect(0, 0, W, H);

// An unavailable family is silently substituted, so ask before committing to one.
const pick = (wanted) => wanted.find(f => Skia.Font.has(f)) || 'Arial';

const directions = [
    { name: 'CLASSIC',    palette: ['#1b2a41', '#324a5f', '#8d9db6', '#e6e8e6'], family: pick(['Georgia', 'Garamond']), shape: 'round' },
    { name: 'TECHNICAL',  palette: ['#0b3954', '#087e8b', '#bfd7ea', '#ff5a5f'], family: pick(['Consolas', 'Verdana']), shape: 'sharp' },
    { name: 'MONOCHROME', palette: ['#111111', '#4d4d4d', '#9a9a9a', '#ededed'], family: pick(['Arial', 'Verdana']),    shape: 'line'  }
];

const colW = W / 3;

directions.forEach((d, i) => {
    const x = i * colW;

    // The compartment boundary is the point: directions must not blur into one another.
    if (i > 0) {
        ctx.strokeStyle = '#c9ccd1';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(x, 24);
        ctx.lineTo(x, H - 24);
        ctx.stroke();
    }

    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = '#222222';
    ctx.font = 'bold 15px ' + d.family;
    ctx.fillText(d.name, x + 32, 52);

    d.palette.forEach((c, j) => {
        ctx.fillStyle = c;
        ctx.fillRect(x + 32 + j * 56, 72, 48, 48);
    });

    ctx.fillStyle = d.palette[0];
    ctx.font = '46px ' + d.family;
    ctx.fillText('Aa', x + 32, 196);
    ctx.fillStyle = '#555555';
    ctx.font = '11px ' + d.family;
    ctx.fillText(d.family, x + 124, 196);

    const cx = x + 96, cy = 288, r = 40;
    ctx.strokeStyle = d.palette[1];
    ctx.fillStyle = d.palette[1];
    ctx.lineWidth = 6;
    ctx.beginPath();
    if (d.shape === 'round') {
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fill();
    } else if (d.shape === 'sharp') {
        ctx.moveTo(cx, cy - r);
        ctx.lineTo(cx + r, cy + r);
        ctx.lineTo(cx - r, cy + r);
        ctx.closePath();
        ctx.fill();
    } else {
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.stroke();
    }

    ctx.fillStyle = '#666666';
    ctx.font = '11px ' + d.family;
    ctx.fillText(d.shape, x + 32, 356);
});

log('directions: ' + directions.map(d => d.name + ' -> ' + d.family).join(', '));

canvas;
```

- **HITL Interaction**: Show the board, but ask the director to *react* to it rather than vote on it. Bokhua's own caution is worth carrying: an early favourite is not reliably the direction that produces the best final mark, and a client locked to a compartment on first sight has spent a choice they did not yet understand. What you want out of this checkpoint is a direction struck out, not a direction promised.

> [!NOTE]
> Bokhua places mood boarding **before** sketching, whereas Stage 4 below sets style direction **after** geometric ideation. That difference is real and unresolved here. Treat 2.5b as the *stylistic frame* — temperature, weight, shape language — and Stage 4 as the *structural* choice of archetype, which genuinely does read better once there are candidate forms to look at.

### Stage 3: Creative Search & Geometric Ideation
- **Objective**: Give each concept from Stage 2.5 a geometric form, using the mathematical toolkits. This stage supplies *form*; the concept it serves was fixed in words already.
- **Tools & Techniques**:
  - Raster (Canvas2D): `Logo.drawIsometricGrid(ctx, ...)`, `Logo.createGoldenCircles(...)`
  - Vector (Snap.svg): `VectorLogo.isometricGrid(paper, ...)`, `VectorLogo.goldenCircles(paper, ...)`, `VectorLogo.squircle(paper, ...)`, `VectorLogo.goldenSpiral(paper, ...)` — note the paper is the **first argument**; the `paper.isometricGrid(...)` receiver form is equivalent.
- **Output**: 2-3 distinct geometric candidates rendered in monochrome or wireframe — **one per surviving concept**, so what the director compares is competing ideas rather than competing shapes.
- **HITL Interaction**: Present each candidate next to the concept line it came from. A form that can no longer be traced to its concept has drifted, and drift is easier to see when the sentence is sitting beside the render.

### Stage 4: Style Direction & Archetype Selection

- **Objective**: Lock the mark archetype. This is a *structural* choice and it reads better here than earlier, once there are candidate forms to look at — 2.5b fixed the stylistic frame, not this.

Each archetype has conditions under which it works. The conditions matter more than the list: choosing a monogram for a brand whose initials are `I` and `L` fails for reasons that have nothing to do with how well it is drawn.

| Archetype | What it is | Choose it when — and what defeats it |
| :--- | :--- | :--- |
| **Pictorial** | A recognisable object as the identifier | The most widespread and the most versatile carrier of a complex idea. Pair it with **neutral** type — the mark is already visually dense, and ornate type competes. The bar is recognition *without* the name beside it |
| **Letterform** | A single initial, shaped to carry meaning | Carries less information than a pictorial mark, so it stays neutral and ages well — which is why finance and tech favour it. Its restraint is the point; do not load it |
| **Abstract** | A form standing for a phenomenon, not an object | For qualities with no natural picture. Some phenomena have strong visual analogues — *connection*, *speed*, *flow*; others — *reliability*, *trust* — have none, and there the meaning is **assigned** rather than depicted, which the brand must then invest in |
| **Wordmark** | The name alone, as form | Needs type with genuine character. Set in a neutral sans it is forgettable, so it wants a ligature, a substitution, or one deliberate incident. Extreme minimalism is a real strategy here, especially B2B |
| **Monogram** | Two or more initials interlocked | **Depends entirely on which letters you were given.** `S`, `W`, `A`, `R` have structure to interlock; `I` and `L` do not, and no amount of skill will rescue a pairing that has nothing to join |
| **Negative space** | Two silhouettes sharing one form, one positive, one negative | The hardest, and the most striking when it lands. Needs **two** silhouettes that are each unmistakable alone *and* conceptually related. Two unrelated shapes that happen to nest is a puzzle, not a mark |
| **Emblem / crest** | Symbol and type enclosed together | Reads as institutional and traditional. The enclosure binds type to mark, so it resists being taken apart for small sizes — the thing app icons and favicons most need |
| **Pictogram** | A universal picture of a thing or idea | For sets that must read across languages. Build from primitives — line, circle, square, triangle — and accept fluidity: small variations do not destroy a pictogram's meaning the way they would a logo's |
| **Logo system** | A parent mark with subsidiary variants | For brands with sub-brands. The hardest coordination problem here: consistency and repetition of key elements are what hold the family together, and the variation usually lives in colour or naming rather than form |

> [!TIP]
> A **pattern** is not an archetype but is often needed alongside one. It should complement the mark without duplicating it — reuse a component of the mark where one is available, or develop a separate visual language where it is not. Grids drive patterns more than colour does: square grids are versatile but everywhere, so triangles or hexagons buy distinctiveness cheaply, and pure tessellation goes monotonous fast.

### Stage 5: Color Palette & Typographic Harmony
- **Objective**: Formulate the color system and typographic lockup.
- **Color Selection**:
  - 1 Dominant Brand Color (emotional anchor).
  - 1 Dynamic Accent Color (high-energy contrast).
  - 2 Neutral Background/Text Tones (slate dark, crisp light).
- **Typography Selection (Robin Williams & Doyald Young)**:
  - `LogoType.evaluateFontPairing(primaryCat, secondaryCat)` to ensure high contrast without conflict.
  - `LogoType.computeWordmarkTracking(fontSize, isAllCaps)` for tight display tracking, applied with
    `ctx.letterSpacing = tracking + 'em'` — the figure comes back as an em fraction and goes on as one.
  - `LogoType.computeOpticalKerning(c1, c2, fontSize)` for character boundary balancing.
  - `ctx.font = '600 21px "Inter Tight", Helvetica, sans-serif'` to *reach* the face: numeric weights,
    quoted multi-word names and fallback lists all parse. End the list in the category's generic, and
    confirm the faces you care about with `Skia.Font.has(...)` — a missing family substitutes in
    silence, which turns a chosen pairing into an accidental one without changing the render's
    appearance of being finished. See Manual 11 §3.
  - `ctx.fillText(name, x, y, maxWidth)` where the lockup must fit a fixed box; it condenses rather
    than shrinking, so a column of names of different lengths keeps one cap height. See Manual 11 §7.

### Stage 6: Multi-Scale Stress Testing & Environments
- **Objective**: Verify that the mark functions in all real-world digital and physical conditions.
- **Protocols**:
  1. **7-Tier Favicon Scale Test**: `Logo.generateFaviconScaleTest(ctx, drawMarkFn)` across $16\text{px}, 24\text{px}, 32\text{px}, 48\text{px}, 64\text{px}, 128\text{px}, 256\text{px}$.
  2. **4-Way Monochrome Test**: `Logo.generateMonochromeTest(ctx, drawMarkFn)` across positive black, negative knockout white, grayscale neutral, and app icon squircle. The two light-on-dark panels are shrunk to cancel the irradiation illusion — see §3.
  3. **Optical Balance Check**: Ensure bone effect narrowing is corrected and apexes have $1.5\%–3.5\%$ overshoot.

### Stage 7: Brand Presentation Board & Style Guide
- **Objective**: Deliver a comprehensive identity sheet for stakeholders.
- **Deliverables**:
  - `Logo.generateBrandPresentationSheet(ctx, options)`:
    - Primary Horizontal Lockup.
    - Secondary Vertical/Stacked Lockup.
    - Color Swatches with Hex codes.
    - $X$-Dimension Clear Space Protective Boundaries ($X = \text{Height} / 4$).
    - Approved vs. Prohibited Use Cases.

---

## 3. The Mark Function: One Definition, Every Test

Stages 1–5 end in decisions; stages 6 and 7 are mechanical, and this is where the toolkit earns its keep. Write the mark **once** as a `(ctx, size) => void` function and every suite re-renders it — because a mark redrawn by hand at 16px is no longer the mark you tested.

Two things about the contract are worth knowing before you write it:

- **Each suite owns the whole canvas.** `generateFaviconScaleTest` and `generateMonochromeTest` both fill the entire canvas before drawing, and `generateBrandPresentationSheet` lays out a full board. Give each its own canvas, or the later one paints over the earlier.
- **The mark sets its own colours.** The suites invoke it with `ctx.fillStyle` left at their own background tone, so a mark that draws nothing but `ctx.fill()` comes out invisible. Set the colours you want inside the function.
- **Draw the mark into a `size × size` box anchored at the origin.** The suites `translate` to position it and pass the size; they do not scale for you.

> `generateMonochromeTest` composites the mark through a knockout filter that keeps its alpha and replaces its hue, so each panel forces it to a single ink — black, white, mid-grey, and near-white on the app icon — **whatever colours the mark sets for itself**. That is what makes it a real one-colour test rather than four backgrounds: the silhouette survives, the palette does not. If the mark stops reading once its colour is gone, the problem is the mark.
>
> The two light-on-dark panels are also drawn **fractionally smaller**, and say so in their labels. A light shape on a dark ground appears larger than the same shape dark-on-light — the irradiation illusion — so a knockout at identical dimensions reads bigger, and a positive/negative pair that measures equal does not look equal. The board compensates so you are comparing what the eye sees. Pass `irradiationStrength: 0` for the uncompensated comparison, or a larger value if your mark still looks swollen in reverse; `Logo.computeIrradiationCompensation(ink, background)` returns the same scale for use in your own artwork.

### Stage 6a — The 7-tier scale ladder

```javascript
// Stage 6 — does the mark survive 16px? The counter is what usually fails first.
const canvas = createCanvas(1000, 420);
const ctx = canvas.getContext('2d');

// The mark, written once. Its counter is cut with the 'evenodd' fill rule, so the
// negative space is genuine geometry and closes up honestly as the mark shrinks —
// rather than being faked with a background-coloured shape on top, which would
// keep "working" at 16px while the real mark would not.
const drawMark = (c, size) => {
    const r = size / 2;
    c.fillStyle = '#2f7fd4';
    c.beginPath();
    c.arc(r, r, r, 0, Math.PI * 2);            // outer disc
    c.moveTo(r + r * 0.42, r);
    c.arc(r, r, r * 0.42, 0, Math.PI * 2);     // counter
    c.fill('evenodd');
};

Logo.generateFaviconScaleTest(ctx, drawMark);

canvas;
```

### Stage 6b — The 4-way treatment board

```javascript
// Stage 6 — the same mark against positive, knockout, greyscale and app-icon grounds.
const canvas = createCanvas(1000, 640);
const ctx = canvas.getContext('2d');

const drawMark = (c, size) => {
    const r = size / 2;
    c.fillStyle = '#2f7fd4';
    c.beginPath();
    c.arc(r, r, r, 0, Math.PI * 2);
    c.moveTo(r + r * 0.42, r);
    c.arc(r, r, r * 0.42, 0, Math.PI * 2);
    c.fill('evenodd');
};

Logo.generateMonochromeTest(ctx, drawMark, 1000, 640);

canvas;
```

### Stage 7 — The presentation board

```javascript
// Stage 7 — lockups, colour chips and clear space, from the same mark definition.
const canvas = createCanvas(1200, 850);
const ctx = canvas.getContext('2d');

const drawMark = (c, size) => {
    const r = size / 2;
    c.fillStyle = '#2f7fd4';
    c.beginPath();
    c.arc(r, r, r, 0, Math.PI * 2);
    c.moveTo(r + r * 0.42, r);
    c.arc(r, r, r * 0.42, 0, Math.PI * 2);
    c.fill('evenodd');
};

Logo.generateBrandPresentationSheet(ctx, {
    brandName: 'NEXUS',
    tagline: 'ADVANCED SYSTEMS',
    primaryColor: '#2f7fd4',
    secondaryColor: '#f0b429',
    darkColor: '#0f1720',
    lightColor: '#f4f7fa',
    drawMark: drawMark
});

log('Stage 7 complete — one mark definition, three deliverables.');

canvas;
```

---

## 4. Reviewing Your Own Work

Work on one mark for long enough and you stop being able to see it. The eye accommodates: the shape becomes the shape you expect, and defects that are obvious to anyone else become invisible to you. Bokhua's remedy is rest — put it down, come back tomorrow, look with a fresh eye.

**You cannot do that.** There is no overnight here, and a session that has been iterating on one form for forty turns has the accommodation problem in its most acute form, with none of the cure. What follows are the mechanical substitutes: ways of making the mark unfamiliar again without waiting.

### A. The mirror check

Flip the mark horizontally and look at the pair. Portrait painters kept an actual mirror beside the easel for this — a reversed image defeats the eye's accommodation instantly, and proportion errors that had become invisible reappear. It costs one render.

```javascript
// A fresh-eyes check: the mark beside its own mirror image.
const drawMark = (c, size) => {
    const u = size / 100;
    c.fillStyle = '#1f3a5f';
    c.beginPath();
    c.moveTo(18 * u, 78 * u);
    c.quadraticCurveTo(30 * u, 22 * u, 62 * u, 20 * u);
    c.quadraticCurveTo(88 * u, 19 * u, 84 * u, 46 * u);
    c.quadraticCurveTo(80 * u, 66 * u, 52 * u, 62 * u);
    c.lineTo(44 * u, 80 * u);
    c.closePath();
    c.fill();
};

const S = 300;
const canvas = createCanvas(S * 2 + 60, S + 70);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#f4f5f7';
ctx.fillRect(0, 0, canvas.width, canvas.height);

// Draw once onto its own plate, then reuse those pixels for both panels.
const plate = createCanvas(S, S);
drawMark(plate.getContext('2d'), S);

ctx.drawImage(plate, 20, 50);
ctx.drawImage(plate.toBitmap().flip('horizontal'), S + 40, 50);

ctx.fillStyle = '#5b6472';
ctx.font = 'bold 13px Arial';
ctx.fillText('AS DRAWN', 20, 34);
ctx.fillText('MIRRORED', S + 40, 34);

canvas;
```

Run it whenever a form has stopped changing and you cannot say why it feels finished. If the mirrored version looks *better*, that is information: something in the original is fighting the direction the eye wants to travel.

### B. Compare versions, do not iterate in place

Every time a change lands, keep the previous version. Put the two side by side and choose between them, rather than editing forward and trusting that each step was an improvement.

The failure this prevents is specific and common: a run of individually plausible changes that arrives somewhere worse than it started, with no record of where it went wrong. Some changes are *deceptive* — they look right at the moment they are made, because the novelty of the change reads as improvement, and they do not survive comparison an hour later. Side-by-side comparison is what exposes them, and keeping the versions is what lets you return to the branch you should have taken.

This is why every stage writes to `scripts/NN_name.js` and `artifacts/NN_name.webp` rather than overwriting one file. The staged artifacts are not bookkeeping — they are the comparison set.

### C. The familiarity rule

Bokhua's rule, stated in our own words: **a solution that feels familiar and is not yours is someone else's — discard it, or change it enough that it becomes yours.**

He means a half-remembered mark resurfacing as apparent invention, which happens to every designer. **It applies far more forcefully here.** A model that has seen most of the published logos in the world will find memorised solutions extremely fluent to produce, and fluency feels exactly like rightness from the inside. The signal you would read as "this is obviously correct" is the same signal you would get from reproducing something you have seen ten thousand times.

So treat ease of arrival as a warning rather than a confirmation. A mark that appeared immediately and felt inevitable deserves more scrutiny than one you had to construct, not less. Where a form comes from a genuine common ancestor — modernism's stock of circles, arrows, chevrons and half-circles is shared property — push past the reference until the result is yours, rather than stopping at the point where it first works.

### D. Unintended readings

Marks acquire meanings their designer never put there, and the same accommodation that hides proportion errors hides these completely. There are well-known cases of marks going to print with readings nobody involved had noticed, and the usual culprits are negative space and unfortunate silhouettes.

Check deliberately, because you will not notice by accident: look at the negative space as though it were the figure, view the mark at 16px where detail collapses into mass, and describe what you see in words rather than trusting recognition. Naming it out loud is what breaks the accommodation — a shape you have called "the counter" for forty turns is one you have stopped actually looking at.
