# Studio Manual 12: Creative Stages in Logo Design

> **Credits & Theoretical Foundation**: Synthesized from Tubik Studio's *Logo Design: Creative Stages* (by Marina Yalanska) and studio practice from leading identity design agencies.
> **Purpose**: Defines the sequential, collaborative, and human-in-the-loop (HITL) stages required to take a brand identity from an initial client brief to an iconic, mathematically refined, and multi-scale validated visual mark.

---

## 1. Overview of the 7 Creative Stages

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

> **Implemented by**: each stage below names its own calls. Stages 1, 2 and 4 are research and judgement with no API surface — do not look for a call that makes those decisions for you.

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

### Stage 3: Creative Search & Geometric Ideation
- **Objective**: Explore multiple structural concepts using mathematical toolkits.
- **Tools & Techniques**:
  - Raster (Canvas2D): `Logo.drawIsometricGrid(ctx, ...)`, `Logo.createGoldenCircles(...)`
  - Vector (Snap.svg): `VectorLogo.isometricGrid(paper, ...)`, `VectorLogo.goldenCircles(paper, ...)`, `VectorLogo.squircle(paper, ...)`, `VectorLogo.goldenSpiral(paper, ...)` — note the paper is the **first argument**; the `paper.isometricGrid(...)` receiver form is equivalent.
- **Output**: 2-3 distinct geometric concept candidates rendered in monochrome or wireframe.
- **HITL Interaction**: Present concepts to the client for feedback on emotional resonance and alignment.

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

> ⚠️ `generateMonochromeTest` varies the **background** of its four panels, not the mark. A full-colour mark stays full-colour in all four, so the board tests the mark against different grounds rather than proving it reduces to one ink. For a true single-colour check, draw a deliberately monochrome variant of the mark and run it through the board a second time.

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
