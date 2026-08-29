# Polson Multi-Agent Comic Studio — Autonomous Co-Creative Pipeline (Claude)

This is an **autonomous multi-agent test harness** for evaluating the Polson MCP server with specialized artistic roles. You are the **Studio Director & Orchestrator**. You manage a specialized team of 4 AI subagents (or sequential stage personas) to recreate a master comic illustration from eference_images/comic1.png through a disciplined **dual-representation workflow (code + pixels)**.

---

## ⭐️ CORE PROTOCOL: DUAL-REPRESENTATION COLLABORATION (CODE + PIXELS)

> **Fundamental Principle**: Reading code alone is NEVER sufficient to maintain visual coherence. Every agent MUST operate in a continuous **Perception-Action Loop**:

### 1. Mandatory Dual Artifact Saving at Every Stage
At each stage of the pipeline, produce **TWO** artifacts in rtifacts/:
1. **The Code Artifact** (rtifacts/stage1_penciler.js, rtifacts/stage2_colorist.js, rtifacts/stage3_inker.js, rtifacts/stage4_refined_v1.js).
2. **The Rendered Image Artifact** (rtifacts/stage1_penciler.webp, rtifacts/stage2_colorist.webp, rtifacts/stage3_inker.webp, rtifacts/stage4_refined_v1.webp) via ExecuteScript(script, outFile: 'artifacts/stageX.webp').

### 2. Mandatory Dual Ingestion Protocol for Downstream Agents
Before modifying or writing any code, execute the following sequence:
- **Step 1: Visual Peeking**: Call iew_file on eference_images/comic1.png AND call iew_file on the previous stage's rendered image (rtifacts/stageX.webp).
- **Step 2: Visual Gap Analysis**: Note concrete geometric gaps:
  - *Are shapes connected or floating?* (e.g. is the ponytail firmly anchored to the bandana knot?)
  - *Are anatomical proportions accurate?* (eye height, nose bridge angle, jawline sharpness, mouth curvature)
  - *Are cel-shadow planes and color temperatures matching the reference?*
- **Step 3: Code Inspection**: Read the previous agent's JS script to understand its layer structure and constants.
- **Step 4: Actuation**: Edit/refine the JavaScript code, execute via ExecuteScript(script, outFile: 'artifacts/stageX.webp').
- **Step 5: Visual Verification**: Call iew_file on the newly rendered image to verify that defects were eliminated.

---

## 1. Studio Team & Role Matrix

| Stage & Role | Specialization | Role Spec | Core Task & Output |
|---|---|---|---|
| **Stage 1: Penciler** | **Composition & Contours** | oles/01_penciler.md | **rtifacts/stage1_penciler.webp**:  \times 380$ canvas. Establishes solid, connected Bézier boundary contours for cranium, 4-lock ponytail mass, bandana wrap & tails, face silhouette, popped linen collar, wooden spar/mast, and 5 diagonal shroud ropes. |
| **Stage 2: Colorist** | **Palette & Lighting** | oles/03_colorist.md | **rtifacts/stage2_colorist.webp**: Applies 4-tier base flats, 5 anatomical facial cel-shadow planes, warm amber ambient glazes, SkSL Ben-Day dot halftone shading, helical rope shading, and golden rim highlights. |
| **Stage 3: Inker** | **Contour & Line Art** | oles/02_inker.md | **rtifacts/stage3_inker.webp**: Overlays variable-weight #0a0a0c ink contours, ctx.drawTaperedStroke() calligraphic hair strands, directional jaw/neck feathering, fabric cross-hatching, and solid shadow blacks. |
| **Stage 4: Critic** | **Visual Drift & QA** | oles/04_critic.md | **output.webp & rtwork.js**: Side-by-side gap analysis vs comic1.png. Executes refinement passes, applies cinematic framing, and outputs consolidated publication code and image. |

---

## 2. Shared Code Architecture

Maintain clean, modular code in rtwork.js with structured layer functions on a  \times 380$ canvas:

`javascript
const canvas = createCanvas(447, 380);
const ctx = canvas.getContext('2d');

drawSkyAndClouds(ctx);
drawRiggingAndMast(ctx);
drawBandanaTailsAndRearHair(ctx);
drawPonytailMass(ctx);
drawCrownHairMass(ctx);
drawBandanaWrap(ctx);
drawFaceAndNeckAnatomy(ctx);
drawFacialFeaturesAndExpression(ctx);
drawForeheadCurls(ctx);
drawCollarAndCoat(ctx);
drawInksAndCrosshatching(ctx);

canvas;
`

---

## 3. Polson SDK Tools & Execution

- **ExecuteScript(script, outFile: 'artifacts/stageX.webp')**: Primary drawing tool. Saves WebP image directly to disk.
- **MeasureSvgPath**: Vector measurement tool for bounding boxes and path lengths.
- **History(n)**: Inspects recent scripts in the session.
- **Documentation Resources**: polson://sdk/index, polson://sdk/core/*, polson://sdk/schema/*.

---

## 4. Deliverables

Produce the following in this directory:
1. **rtifacts/**: Intermediate scripts and rendered WebP images from each stage.
2. **rtwork.js**: Complete, consolidated master JavaScript script.
3. **output.webp**: Final rendered master image.
4. **critique_log.md**: Multi-agent collaboration trace documenting visual peeking observations and refinements.
5. **indings.md**: Evaluation of the Polson MCP server and SDK developer experience.
