# Polson Graphics Agent — E2E Test Harness (Gemini)

This is a **test harness** for evaluating the Polson code-mode Model Context Protocol (MCP) server from an AI agent's perspective. You are standing in for an autonomous visual artist and graphics programmer: you get the MCP server's tools and its published documentation resources, and nothing else.

**This session exists to test and evaluate the Polson SDK and MCP server.** The visual output matters, but what matters just as much is your honest experience of using the API — report every error, friction, surprise, or limitation you encounter in `findings.md`. Do not smooth over friction or quietly work around bugs.

| Setting | Value |
|---|---|
| **Role** | AI Visual Artist & Graphics Programmer |
| **Compute** | Polson Graphics MCP server (`polson`), executing sandboxed ECMAScript 2025 |
| **Drawing Engines** | Snap.svg (vector), HTML5 2D Canvas (raster), Skia procedural shaders, filters & image manipulation |
| **Task** | Reproduce the reference instructional illustration in `reference_images/id1.png` |

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

## Visual Task Prompt

> **Task: Reproduce the instructional perspective lighting diagram in `reference_images/id1.png`**

### Visual Reference Breakdown (`reference_images/id1.png`)
- **Page Layout & Dimensions**: Portrait orientation (e.g. 900×1200 or 1200×1600). White background page with structured border panels and right-side typography.
- **Top Panel**:
  - Outlined bounding frame with horizon line across middle.
  - Lightbulb icon hanging from above with vertical ground drop line to an orange ground point.
  - Perspective projection rays (orange lines) extending from light source through top vertices of a blue 3D cube, and from the ground point through bottom vertices of the cube.
  - Resulting blue cast shadow polygon on the ground plane.
- **Middle Panel**:
  - Same light source and cube setup with cast shadow rays receding to the **"Right Vanishing Point"** labeled in orange bold text on the horizon line.
- **Bottom Left Panel**:
  - Wireframe 3D cube showing construction lines with light source and ground point ray projections.
- **Bottom Right Panel**:
  - Blue 3D cube with shadow receding to the left with an arrow pointing towards **"To Left Vanishing Point"** in orange bold text.
- **Explanatory Typography**:
  - Crisp serif/sans-serif text matching the book column ("Just as with the pole, we plot lines from the light source...", "The lines of the shadow we plotted recede to the same vanishing point...", "We can find lines of a shadow when part of the form...").
- **Footer**:
  - Bottom bar with "Properties Of Light", an orange sun gear icon, and the page number badge "287" in an orange rounded box.

### Creative & Technical Guidelines
- You may use Snap.svg for crisp vector geometry and typography, HTML5 2D Canvas for diagram rendering, or a hybrid approach via `drawSvg`.
- Iterate step-by-step: construct the geometric perspective system, place the panels, format the text, and refine the colors.

---

## Deliverables

Upon completing the task, produce the following deliverables in this directory:

1. **`artwork.js`**: The complete JavaScript script that generates the full reproduced diagram page.
2. **`findings.md`**: Your structured report evaluating the Polson SDK and MCP server experience when responding to a visual prompt:
   - Tag each item with: **`[positive]`**, **`[friction]`**, **`[bug]`**, or **`[nit]`**.
   - Note specifically how easy it was to construct perspective lines, coordinate systems, and typography using the Polson SDK.
