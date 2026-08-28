# Studio Manual 12: Creative Stages in Logo Design

> **Credits & Theoretical Foundation**: Synthesized from Tubik Studio's *Logo Design: Creative Stages* (by Marina Yalanska) and studio practice from leading identity design agencies. Tubik's seven stages are the spine of this manual. **Stage 2.5 is inserted from a different source**: the practice of generating concepts by working a list of rhetorical devices is taken from *Graphic Design and Print Production Fundamentals* §2.5 (Alex Hass, Graphic Communications Open Textbook Collective / BCcampus Open Education), used under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The device definitions and worked examples below are our own — that book's own definitions are its paraphrase of Harris (2013), which carries separate terms.
> **Purpose**: Defines the sequential, collaborative, and human-in-the-loop (HITL) stages required to take a brand identity from an initial client brief to an iconic, mathematically refined, and multi-scale validated visual mark.

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
   │            STAGE 2.5: Concept Articulation                  │
   │   One sentence: the concept, its device, what it rejects    │
   └──────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼ [HITL Checkpoint: Concept]
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

> **Implemented by**: each stage below names its own calls. Stages 1, 2, 2.5 and 4 are research and judgement with no API surface — do not look for a call that makes those decisions for you.

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

### Stage 2.5: Concept Articulation

- **Objective**: State in words what the mark *means*, before any geometry exists.

**A concept is not a message.** The message is what the brand says — *"we deliver overnight, reliably."* The concept is the idea that carries it — *"the shortest line between two points."* The message is given to you in the brief; the concept is the thing you are actually being paid for, and it is what makes every later decision answerable instead of arbitrary.

Stage 2 ends by naming a metaphorical anchor. This stage is where that anchor becomes a sentence you can be held to. Skip it and Stage 3 begins with grids and golden ratios applied to nothing in particular — the characteristic failure of a mark that is technically immaculate and says nothing, and could carry any name in its category.

**Write one line and log it before drawing:**

```
CONCEPT: <one sentence — the idea, not the message>
DEVICE:  <the rhetorical device it turns on>
REJECTS: <the category cliché from Stage 2 that this avoids>
```

#### Devices, and what each one does to a brief

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

#### Quota, not inspiration

Work the list. Do not wait for the idea to arrive.

- **Go broad first**: one candidate per device, judged afterwards, not during. A quota is the point — it keeps you generating past the comfortable stopping point, which is where the first non-obvious candidate usually appears.
- **Then go deep**: take the two strongest and express each several ways — different framing, different emphasis, different weight. The first expression of an idea is rarely its best one.
- **Do both.** Most designers naturally do one or the other; whichever comes easily to you, force yourself through the other as well.

Carry two or three concepts into Stage 3, not one. A single concept cannot be compared to anything, so its weaknesses stay invisible until far too late to fix cheaply.

- **HITL Interaction**: This is the cheapest checkpoint in the entire process — a concept can be rejected and replaced for the price of a sentence, whereas the same rejection after Stage 5 discards geometry, palette and typography together. Present the concept lines and let the director kill one *here*.

### Stage 3: Creative Search & Geometric Ideation
- **Objective**: Give each concept from Stage 2.5 a geometric form, using the mathematical toolkits. This stage supplies *form*; the concept it serves was fixed in words already.
- **Tools & Techniques**:
  - Raster (Canvas2D): `Logo.drawIsometricGrid(ctx, ...)`, `Logo.createGoldenCircles(...)`
  - Vector (Snap.svg): `VectorLogo.isometricGrid(paper, ...)`, `VectorLogo.goldenCircles(paper, ...)`, `VectorLogo.squircle(paper, ...)`, `VectorLogo.goldenSpiral(paper, ...)` — note the paper is the **first argument**; the `paper.isometricGrid(...)` receiver form is equivalent.
- **Output**: 2-3 distinct geometric candidates rendered in monochrome or wireframe — **one per surviving concept**, so what the director compares is competing ideas rather than competing shapes.
- **HITL Interaction**: Present each candidate next to the concept line it came from. A form that can no longer be traced to its concept has drifted, and drift is easier to see when the sentence is sitting beside the render.

### Stage 4: Style Direction & Archetype Selection
- **Objective**: Lock the mark archetype based on client feedback:
  1. **Wordmark / Logotype**: Distinctive typographic treatment of brand name.
  2. **Lettermark / Monogram**: Initial letters interlocked via geometric matrices.
  3. **Pictorial / Iconic Mark**: Stylized representation of a recognizable object.
  4. **Abstract Geometric Mark**: Pure geometric form symbolizing dynamic concepts.
  5. **Emblem / Crest**: Enclosed badge with integrated symbol and typography.

### Stage 5: Color Palette & Typographic Harmony
- **Objective**: Formulate the color system and typographic lockup.
- **Color Selection**:
  - 1 Dominant Brand Color (emotional anchor).
  - 1 Dynamic Accent Color (high-energy contrast).
  - 2 Neutral Background/Text Tones (slate dark, crisp light).
- **Typography Selection (Robin Williams & Doyald Young)**:
  - `LogoType.evaluateFontPairing(primaryCat, secondaryCat)` to ensure high contrast without conflict.
  - `LogoType.computeWordmarkTracking(fontSize, isAllCaps)` for tight display tracking.
  - `LogoType.computeOpticalKerning(c1, c2, fontSize)` for character boundary balancing.

### Stage 6: Multi-Scale Stress Testing & Environments
- **Objective**: Verify that the mark functions in all real-world digital and physical conditions.
- **Protocols**:
  1. **7-Tier Favicon Scale Test**: `Logo.generateFaviconScaleTest(ctx, drawMarkFn)` across $16\text{px}, 24\text{px}, 32\text{px}, 48\text{px}, 64\text{px}, 128\text{px}, 256\text{px}$.
  2. **4-Way Monochrome Test**: `Logo.generateMonochromeTest(ctx, drawMarkFn)` across positive black, negative knockout white, outline wireframe, and app icon badge.
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
