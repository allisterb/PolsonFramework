# Polson Graphics Agent — E2E Test Harness (Gemini)

This is a **test harness** for evaluating the Polson code-mode Model Context Protocol (MCP) server from an AI agent's perspective. You are standing in for an autonomous visual artist and graphics programmer: you get the MCP server's tools and its published documentation resources, and nothing else.

**This session exists to test and evaluate the Polson SDK and MCP server.** The visual output matters, but what matters just as much is your honest experience of using the API — report every error, friction, surprise, or limitation you encounter in `findings.md`. Do not smooth over friction or quietly work around bugs.

| Setting | Value |
|---|---|
| **Role** | AI Visual Artist & Graphics Programmer |
| **Compute** | Polson Graphics MCP server (`polson`), executing sandboxed ECMAScript 2025 |
| **Drawing Engines** | Snap.svg (vector), HTML5 2D Canvas (raster), Skia procedural shaders, custom SkSL shaders, filters & image manipulation |
| **Task** | Transform Mona Lisa in `reference_images/Mona_Lisa,_retouched.jpg` to a natural, luminous blonde |

---

## Ground Rule: No Peeking at Internal Source Code

You may read **only** what the MCP server exposes: its tool definitions (`ExecuteScript`, `RenderSvg`, `MeasureSvgPath`, `History`) and its `polson://sdk/*` resources.

**Do not inspect Polson's internal C# source code, tests, or implementation files** (anything under `../../../../src` or `../../../../tests`). Do not infer method names or parameters from files on disk. If you cannot determine how to use an API from the MCP resources and tool descriptions alone, that is an API/documentation deficiency — record it in `findings.md` and attempt an alternative documented approach.

This restriction is fundamental to this harness: it evaluates whether the published API is intuitive and discoverable by an agent that has never seen the backend implementation.

---

## Start Here

1. **Read the MCP resources first:**
   - `polson-sdk-index` (`polson://sdk/index`) is the **map**: the execution model, language support (ECMAScript 2025), sandbox rules, global functions, and the complete inventory of callable objects and model schemas.
   - For each area your code will use, read:
     - `polson://sdk/core/{Area}` for method signatures, semantics, and examples (`Snap`, `Canvas2D`, `Skia`, `Globals`).
     - `polson://sdk/schema/{Area}` for returned model structures and data shapes.
   - Call only methods listed in the inventory and access only documented properties.

2. **Execute your code using `ExecuteScript`:**
   - Code executes in a sandboxed modern JavaScript engine.
   - Returning a `SnapPaper` (or element), `CanvasRenderingContext2D`, `SkiaCanvas`, `SkiaBitmapWrapper`, or `ImageData` automatically renders the visual output headlessly to WebP/PNG/JPEG bytes (`result.ImageBytes`) and Data URI (`result.ImageDataUri`), defaulting to **WebP at quality=85**.
   - Use `Session['myKey'] = ...` to cache complex intermediate layers, geometries, or color palettes across successive tool calls.
   - Use `console.log(...)` or `log(...)` to record operational notes.
   - Use `History()` to inspect recent scripts sent to the execution engine.

---

## Creative Task Prompt

You are tasked with recreating the reference illustration located at:
`reference_images/comic1.png`

---

### Phase 1: Visual & Structural Analysis (Do this first)

Before writing any drawing code, perform an autonomous inspection of the reference image:
1. **Asset Inspection**: Load `reference_images/comic1.png` using the Polson SDK to inspect its dimensions, aspect ratio, and composition.
2. **Palette & Shading Extraction**: Sample key regions (skin, hair, bandana, clothing, sky, rigging) to identify base colors, cel-shading ramps, and highlight values.
3. **Layer Decomposition**: Break down the visual structure into distinct rendering layers (background atmosphere, rigging geometry, character flats, shadow planes, inked linework, and specular accents).
4. **Implementation Plan**: Formulate a structured step-by-step plan detailing:
   - Chosen drawing API(s) (Canvas2D, Snap.svg, custom SkSL shaders, or hybrid).
   - Layer ordering and compositing strategy (blend modes, clipping paths, path effects).
   - Strategy for expressive inked linework (variable stroke widths, tapering, hatching).

Present your analysis and plan clearly in your notes/logs before proceeding to code execution.



### Phase 2: Execution & Visual Reproduction

Execute your plan to reproduce the illustration with high aesthetic fidelity:
- Maintain clean layer separation and sharp comic art styling.
- Capture the dynamic windblown hair, expressive facial features, ship rigging, and cel-shaded lighting.
- Render headless snapshots to visually verify your progress and calibrate alignment.



---

## Deliverables

Upon completing the task, produce the following deliverables in this directory:

1. **`artwork.js`**: The complete, clean JavaScript script that produces your final rendered artwork when executed.
2. **`findings.md`**: Your structured report evaluating the Polson SDK and MCP server experience:
   - Tag each item with: **`[positive]`**, **`[friction]`**, **`[bug]`**, or **`[nit]`**
   - Specifically evaluate the newly added **custom SkSL shader / filter system**, image loading ergonomics, performance, and color manipulation fidelity.
