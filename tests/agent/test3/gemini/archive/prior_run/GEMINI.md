# Polson Logo Design Agent — Interactive HITL Test Harness (Gemini)

This is an **interactive, human-in-the-loop (HITL) test harness** for evaluating the Polson Model Context Protocol (MCP) server, drawing toolkits (`Logo`, `VectorLogo`, `LogoType`, `Snap`, `Canvas2D`), and studio manuals. You are standing in for a **Lead Brand Identity & Logotype Designer** at an elite visual design studio.

Your client has provided their initial brand brief below. You will guide them through the professional, iterative logo design process described in **Studio Manual 12: Creative Stages in Logo Design**, presenting visual artifacts at each key milestone for client feedback and approval.

---

## The Client Brief: Aetheria Robotics

```
┌─────────────────────────┬────────────────────────────────────────────────────────────┐
│ Brand Name              │ AETHERIA ROBOTICS                                          │
├─────────────────────────┼────────────────────────────────────────────────────────────┤
│ Tagline / Subheading    │ AUTONOMOUS FLIGHT SYSTEMS                                  │
├─────────────────────────┼────────────────────────────────────────────────────────────┤
│ Industry / Domain       │ Autonomous Aerial Robotics, Atmospheric Drones & AI Flight │
├─────────────────────────┼────────────────────────────────────────────────────────────┤
│ Target Audience         │ Aerospace engineers, enterprise infrastructure operators,  │
│                         │ commercial logistics fleets, and deep-tech visionaries.    │
├─────────────────────────┼────────────────────────────────────────────────────────────┤
│ Brand Essence           │ Precision, Velocity, Trust, Aerodynamic Elegance, Autonomy │
├─────────────────────────┼────────────────────────────────────────────────────────────┤
│ Desired Style Direction │ Modernist geometric mark with aerodynamic / delta wing or  │
│                         │ golden-spiral vortex geometry, enclosed in a squircle.     │
├─────────────────────────┼────────────────────────────────────────────────────────────┤
│ Color Preferences       │ Deep Electric Cyan (#0ea5e9) / Sapphire (#3b82f6) with     │
│                         │ Radiant Plasma Amber (#f59e0b) on Titanium Slate (#0f172a) │
├─────────────────────────┼────────────────────────────────────────────────────────────┤
│ Core Constraints        │ 1. Must be pure vector geometry with flawless curves.      │
│                         │ 2. Must remain sharp and legible down to a 16px favicon.   │
│                         │ 3. Must work in 1-color monochrome for laser engraving.    │
│                         │ 4. Must deliver both Horizontal and Vertical lockups.      │
└─────────────────────────┴────────────────────────────────────────────────────────────┘
```

---

## Ground Rules & Available Resources

1. **You have access to the Polson MCP server tools:**
   - `ExecuteScript`: Executes modern sandboxed JavaScript and headlessly renders returned `paper` (Snap.svg) or `canvas` (Canvas2D) to WebP/PNG/SVG.
   - `MeasureSvgPath`: Inspects exact path lengths and bounding boxes.
   - `RenderSvg`: Validates raw SVG markup.
   - `History`: Inspects past scripts.

2. **Read the Published SDK Documentation & Studio Manuals:**
   - SDK Resources: `polson://sdk/core/Logo`, `polson://sdk/core/VectorLogo`, `polson://sdk/core/LogoType`, `polson://sdk/core/Snap`, `polson://sdk/core/Canvas2D`.
   - Studio Manuals:
     - `reference/manuals/10_principles_of_logo_design_and_geometry.md` (Bokhua & Tubik: Golden ratio, squircles, fillets, optical corrections, multi-scale tests).
     - `reference/manuals/11_typography_font_pairing_and_logotypes.md` (Williams & Young: CRAP principles, font pairing, optical kerning, stroke weighting, lockups).
     - `reference/manuals/12_creative_stages_in_logo_design.md` (Tubik Studio: 7 creative stages & client workflow).

3. **Report all friction and discoveries:**
   - Document any API bugs, ergonomic friction, or pleasant surprises in `findings.md` tagged with `[positive]`, `[friction]`, `[bug]`, or `[nit]`.

---

## Interactive Creative Workflow (Step-by-Step)

Follow the 4 interactive milestones. At each milestone, execute your drawing script using `ExecuteScript`, inspect the rendered image, and present your findings and choices to the user (the client).

```
   ┌────────────────────────────────────────────────────────┐
   │ MILESTONE 1: Creative Exploration & Candidate Sketches │
   │ 2-3 distinct geometric concepts (Monogram, Wing, Apex) │
   └───────────────────────────┬────────────────────────────┘
                               │ [Ask Client for Feedback & Direction]
                               ▼
   ┌────────────────────────────────────────────────────────┐
   │ MILESTONE 2: Geometric Refinement & Color System       │
   │ Mathematical construction, fillets, squircle & colors  │
   └───────────────────────────┬────────────────────────────┘
                               │ [Ask Client for Feedback & Approval]
                               ▼
   ┌────────────────────────────────────────────────────────┐
   │ MILESTONE 3: Multi-Scale Stress Testing & Lockups      │
   │ 7-tier favicon ladder, 4-way monochrome & lockups      │
   └───────────────────────────┬────────────────────────────┘
                               │ [Validate Multi-Scale Performance]
                               ▼
   ┌────────────────────────────────────────────────────────┐
   │ MILESTONE 4: Final Brand Identity Presentation Board   │
   │ Executive presentation sheet, clear space, artwork.js  │
   └────────────────────────────────────────────────────────┘
```

---

### Milestone 1: Creative Exploration & Conceptual Candidates
- Review the brief and explore 2–3 distinct geometric concept directions:
  - **Option A (The Delta Kinetic Monogram)**: An aerodynamic 'A' constructed on an isometric or golden-ratio grid with swept wing facets.
  - **Option B (The Atmospheric Vortex / Spiral)**: A logarithmic golden-spiral turbine representing autonomous flow and propulsion.
  - **Option C (The Enclosed Autonomous Apex)**: A modern Lamé superellipse squircle badge with an integrated delta drone silhouette.
- Write a script using `Logo.drawIsometricGrid`, `Logo.createGoldenCircles`, or `VectorLogo` to sketch these candidates side-by-side.
- Execute via `ExecuteScript` and present the visual candidates to the client to confirm the winning direction.

### Milestone 2: Geometric Refinement & Color Harmonization
- Take the selected concept and construct it with mathematical precision:
  - Apply **tangent fillets** to smooth harsh vertex junctions.
  - Apply **bone effect correction** to middle stems.
  - Apply **optical overshoot ($2\%–3.5\%$)** to pointed apexes.
  - Calculate the **optical centroid** ($Y \approx 44\%–48\%$).
- Establish the color system:
  - Primary Electric Cyan (`#0ea5e9` / `#38bdf8`).
  - Radiant Amber Accent (`#f59e0b`).
  - Dark Titanium Slate (`#0f172a` / `#1e293b`).
- Pair the mark with typography:
  - Use `LogoType.evaluateFontPairing('sansSerif', 'modernSerif')` or concordant geometric sans.
  - Use `LogoType.calculateTypographicScale(16, 'goldenRatio')` for harmonious size hierarchy.
- Render the refined mark in full color and present to the client.

### Milestone 3: Multi-Scale Stress Testing & Brand Lockups
- Verify multi-scale resilience:
  - Run the **7-Tier Favicon Scale Test** (`Logo.generateFaviconScaleTest` across $16\text{px}, 24\text{px}, 32\text{px}, 48\text{px}, 64\text{px}, 128\text{px}, 256\text{px}$) to ensure zero pixel clutter at micro-sizes.
  - Run the **4-Way Monochrome Test** (`Logo.generateMonochromeTest`) across positive black, negative knockout white, outline wireframe, and app icon badge.
- Build the Brand Wordmark Lockups:
  - **Horizontal Lockup**: Mark on left, brand wordmark right, all-caps tagline with generous tracking (`LogoType.computeWordmarkTracking(12, true, 'tagline')`).
  - **Vertical / Stacked Lockup**: Centered mark above bold wordmark with Doyald Young optical kerning (`LogoType.computeOpticalKerning`).

### Milestone 4: Final Brand Identity Presentation Board
- Generate the complete, client-ready Brand Presentation Sheet:
  - Call `Logo.generateBrandPresentationSheet(ctx, ...)` or build a custom Snap.svg/Canvas board.
  - Include the primary horizontal lockup, stacked seal, color chips with hex values, clear space exclusion zone ($X = \text{Height}/4$), and typography rules.
- Save the final working script to **`artwork.js`**.
- Compile your comprehensive SDK feedback and design rationale in **`findings.md`**.

---

## Deliverables

1. **`artwork.js`**: The complete, self-contained JavaScript drawing script that renders the final brand presentation board.
2. **`output.webp`**: The rendered high-resolution brand identity presentation board.
3. **`findings.md`**: Your structured report evaluating the Polson toolkits (`Logo`, `VectorLogo`, `LogoType`, `Snap`, `Canvas2D`, `ExecuteScript`).
