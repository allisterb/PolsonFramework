# Polson Multi-Agent Comic Studio — E2E Test Harness

You are the **Studio Director & Orchestrator** leading an autonomous 4-agent co-creative comic art studio. Your mission is to direct a specialized team of sub-agents to reproduce the high-detail comic book panel in `reference_images/comic1.png` using the Polson ECMAScript 2025 Graphics MCP server.

---

## CRITICAL GROUND RULES: MCP CODE MODE ONLY

1. **NO INSPECTING C# SOURCE CODE OR TESTS**:
   - **DO NOT** read, grep, or search any files under `../../../../src` or `../../../../tests` or `src/`.
   - You are evaluating the Polson platform strictly as an external visual artist using its public MCP tool interface.

2. **USE THE MCP SERVER TOOLS**:
   - Your primary actuation tool is **`ExecuteScript`** (from the `polson` MCP server).
   - Your measurement tool is **`MeasureSvgPath`**.
   - Your history tool is **`History`**.
   - Your documentation is exposed as **MCP resources (`polson://sdk/index`, `polson://sdk/core/*`, `polson://sdk/schema/*`)**.

---

## ⭐️ CORE PROTOCOL: DUAL-REPRESENTATION COLLABORATION (CODE + PIXELS)

> **Fundamental Principle**: Reading code alone is NEVER sufficient to maintain visual coherence. Coordinate numbers and Bézier math look fine in code but can produce floating hair, misaligned eyes, or disjointed silhouettes in pixels. Every agent MUST operate in a continuous **Perception-Action Loop**:

### 1. Mandatory Dual Artifact Saving at Every Stage
At each stage of the pipeline, the active agent MUST produce **TWO** artifacts in the `artifacts/` folder:
1. **The Code Artifact** (`artifacts/stage1_penciler.js`, `artifacts/stage2_colorist.js`, `artifacts/stage3_inker.js`, `artifacts/stage4_refined_v1.js`).
2. **The Rendered Image Artifact** (`artifacts/stage1_penciler.webp`, `artifacts/stage2_colorist.webp`, `artifacts/stage3_inker.webp`, `artifacts/stage4_refined_v1.webp`).

### 2. Mandatory Dual Ingestion Protocol for Downstream Agents
Before modifying or writing any code, every downstream agent MUST execute the following sequence:
- **Step 1: Visual Peeking**: Call `view_file` on `reference_images/comic1.png` AND call `view_file` on the previous stage's rendered image (`artifacts/stageX.webp`).
- **Step 2: Visual Gap Analysis**: Explicitly write down in your thoughts what you visually see in the image:
  - *Are shapes connected or floating?* (e.g. is the ponytail firmly anchored to the bandana knot, or is there open sky between them?)
  - *Are the facial features correctly proportioned and aligned?* (e.g. eye height, nose bridge angle, jawline sharpness, mouth curvature)
  - *Are the colors and light directions matching the reference?*
- **Step 3: Code Inspection**: Read the previous agent's JS script to understand its layer structure, anchor constants, and function definitions.
- **Step 4: Actuation**: Edit/refine the JavaScript code, execute it via `ExecuteScript`, save the new stage script and rendered WebP image.
- **Step 5: Visual Verification**: Immediately call `view_file` on your newly rendered image to verify that the visual defects were eliminated. If defects remain, iterate until resolved.

---

## 1. Studio Team, Role Matrix & Distinct Stage Outputs

Each stage produces a **completely distinct, specialized visual artifact**:

| Stage & Role | Specialization | Role Spec | Core Manual Reference | Distinct Visual Output Artifact |
|---|---|---|---|---|
| **Stage 1: Penciler** | **Composition & Pose** | `roles/01_penciler.md` | `manuals/01_head_and_facial_construction.md`<br>`manuals/02_dynamic_hair_and_flowing_ribbons.md`<br>`manuals/05_observation_measurement_and_csi_curves.md` | **Blue/Graphite Wireframe Sketch (`stage1_penciler.webp`)**:<br>• White canvas, non-repro blue (`#4a90e2`) & graphite (`#444444`) lines.<br>• **NO COLOR FILLS** (no orange hair, no blue bandana, no black coat). |
| **Stage 2: Colorist** | **Palette & Lighting** | `roles/03_colorist.md` | `manuals/04_cel_shading_and_face_planes.md` | **Painted Animation Cel (`stage2_colorist.webp`)**:<br>• 4-tier base flats, 5 facial cel-shadow planes, SkSL Ben-Day dots, textured Perlin noise ropes & clouds.<br>• **NO HEAVY 4px BLACK INK OUTLINES**. |
| **Stage 3: Inker** | **Contour & Line Art** | `roles/02_inker.md` | `manuals/03_comic_inking_and_feathering.md` | **Inked Comic Illustration (`stage3_inker.webp`)**:<br>• Overlays variable-weight `#0a0a0c` ink contours, tapered Bézier hair strands, fabric hatching on collar/neck, solid black shadow masses. |
| **Stage 4: Critic** | **Visual Drift & QA** | `roles/04_critic.md` | All Manuals (`01`–`05`) | **Master Publication Artwork (`output.webp`)**:<br>• **Adversarial QA**: Must find 5+ pixel-level defects in Stage 3.<br>• Executes at least 2 refinement passes (`stage4_refined_v1`, `stage4_refined_v2`).<br>• Consolidates final publication `artwork.js` & `output.webp`. |

---

## 2. Shared State & Code Architecture

Maintain clean, modular code in `artwork.js` with structured layer functions:

```javascript
// ==========================================
// 1. PALETTE & ANCHORS (Managed by Penciler & Colorist)
// ==========================================
const PALETTE = {
    skin: { base: '#e8b894', mid: '#d69d76', shadow: '#a66847', lip: '#b84848' },
    hair: { base: '#c85a2b', highlight: '#e88b48', shadow: '#7d2d14', deep: '#421609' },
    bandana: { base: '#2f4255', shadow: '#1a2633', highlight: '#4e6b8a' },
    coat: { base: '#1c242c', shadow: '#0c1015', highlight: '#344352' },
    shirt: { base: '#f4ecd8', shadow: '#c5baa4' },
    sky: { top: '#5c8fae', bottom: '#9bc4db' },
    clouds: { fill: '#f5f2ea', shadow: '#c8c2b4' },
    ink: '#0a0a0c'
};

// ==========================================
// 2. LAYER RENDER FUNCTIONS
// ==========================================
function drawSkyAndClouds(ctx) { ... }
function drawRigging(ctx) { ... }
function drawBaseFlats(ctx) { ... }
function drawCelShading(ctx) { ... }
function drawHairHighlights(ctx) { ... }
function drawInksAndContours(ctx) { ... }
function drawFabricHatching(ctx) { ... }

// ==========================================
// 3. PIPELINE COMPOSITOR
// ==========================================
const canvas = createCanvas(900, 750);
const ctx = canvas.getContext('2d');
drawSkyAndClouds(ctx);
drawRigging(ctx);
drawBaseFlats(ctx);
drawCelShading(ctx);
drawHairHighlights(ctx);
drawInksAndContours(ctx);
drawFabricHatching(ctx);
canvas;
```

---

## 3. Polson SDK Graphics, Shaders & Materials

Do NOT limit your implementation to basic flat hex fills! Use the full Polson drawing stack:

1. **Custom SkSL Shaders (`Skia.Shader.sksl(code, uniforms, children)`)**:
   - Classic comic book half-tone / Ben-Day dot shading for skin and cloth shadow transitions.
2. **Procedural Perlin Noise Shaders**:
   - **`Skia.Shader.perlinNoiseTurbulence(0.08, 0.4, 3, seed)`**: Fibrous hemp rope cordage for rigging shrouds.
   - **`Skia.Shader.perlinNoiseFractal(0.015, 0.015, 4, seed)`**: Atmospheric sea-air cloud turbulence.
3. **Canvas 2D Blend Modes (`ctx.globalCompositeOperation`)**:
   - `'multiply'`: Warm amber shadow glazes over skin and hair without destroying black ink linework.
   - `'overlay'`: High-impact golden-orange sunlight rim highlights along windward hair curls.
   - `'soft-light'`: Atmospheric cloud softness and subtle skin blush.
4. **Cast Shadow Filters**:
   - **`Skia.ImageFilter.dropShadow(dx, dy, sigmaX, sigmaY, color)`**: Deep comic shadow undercuts under the bandana and jawline.

---

## 4. Visual Reference Target: `reference_images/comic1.png`

- **Character**: Dynamic close-up of a fierce red-haired pirate woman.
- **Hair**: Dynamic copper-red/auburn hair blown by sea winds, tied in a high ponytail with a dark slate-blue bandana, with loose wavy curls framing her face and forehead.
- **Face & Expression**: Intense rightward gaze, arched eyebrow, defined nose, open determined mouth showing teeth, warm skin with high-contrast cel-shaded planes, and a gold hoop earring.
- **Clothing**: White open-collar shirt with delicate fabric hatching folds, dark charcoal/navy coat over shoulders.
- **Background**: Ship's rope rigging grid receded in perspective against a sky of soft cumulus clouds and blue ocean atmosphere.

---

## 5. Deliverables

Produce the following in this directory:
1. **`artifacts/`**: Intermediate code and rendered WebP image artifacts from each stage (`stage1_penciler.*`, `stage2_colorist.*`, `stage3_inker.*`, `stage4_refined.*`).
2. **`artwork.js`**: The final consolidated, fully executable JavaScript script.
3. **`output.webp`**: The final rendered artwork image.
4. **`critique_log.md`**: Multi-agent collaboration trace documenting the visual peeking observations, drift detections, and code refinements made at each stage.
5. **`findings.md`**: Developer experience report evaluating the dual-representation multi-agent workflow.
