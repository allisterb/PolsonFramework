# Studio Manual 12: Creative Stages in Logo Design

> **Credits & Theoretical Foundation**: Synthesized from Tubik Studio's *Logo Design: Creative Stages* (by Marina Yalanska) and studio practice from leading identity design agencies.
> **Purpose**: Defines the sequential, collaborative, and human-in-the-loop (HITL) stages required to take a brand identity from an initial client brief to an iconic, mathematically refined, and multi-scale validated visual mark.

---

## 1. Overview of the 7 Creative Stages

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
  - `Logo.drawIsometricGrid(ctx, ...)` & `VectorLogo.IsometricGrid(...)`
  - `Logo.createGoldenCircles(...)` & `VectorLogo.GoldenCircles(...)`
  - `VectorLogo.Squircle(...)` & `VectorLogo.GoldenSpiral(...)`
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
