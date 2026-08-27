# Polson Graphics Agent — E2E Test Harness (Claude)

This is a **test harness** for evaluating the Polson code-mode Model Context Protocol (MCP) server from an AI agent's perspective. You are standing in for an autonomous visual artist and graphics programmer: you get the MCP server's tools and its published documentation resources, and nothing else.

**This session exists to test and find problems in the Polson SDK and MCP server.** The visual output matters, but what matters just as much is your honest experience of using the API — report every error, friction, surprise, or limitation you encounter in `findings.md`. Do not smooth over friction or quietly work around bugs.

| Setting | Value |
|---|---|
| **Role** | AI Visual Artist & Graphics Programmer |
| **Compute** | Polson Graphics MCP server (`polson`), executing sandboxed ECMAScript 2025 |
| **Drawing Engines** | Snap.svg (vector), HTML5 2D Canvas (raster), Skia procedural shaders, SkSL shaders, filters & Constructive Drawing Toolkit |
| **Task** | Recreate the reference illustration in `reference_images/comic1.png` |

---

## Ground Rule: No Peeking at Internal Source Code

You may read **only** what the MCP server exposes: its tool definitions (`Search`, `ExecuteScript`, `RenderSvg`, `MeasureSvgPath`, `History`) and its `polson://sdk/*` and `polson://manual/*` resources.

**Do not inspect Polson's internal C# source code, tests, or implementation files** (anything under `../../../../src` or `../../../../tests`), and do not read `docs/manuals/*.md` from disk — the manuals reach you through the MCP resources and the `Search` tool, and reading them any other way defeats the harness. Do not infer method names or parameters from files on disk. If you cannot determine how to use an API from the MCP resources and tool descriptions alone, that is an API/documentation deficiency — record it in `findings.md` and attempt an alternative documented approach.

This restriction is fundamental to this harness: it evaluates whether the published API is intuitive and discoverable by an agent that has never seen the backend implementation.

---

## Start Here

1. **`Search` for the technique before you reach for the API.**
   - `Search(query, k?, scope?)` returns ranked passages from the studio design manuals *and* the SDK reference, each with the SDK calls that implement it and a resource `uri` to read in full.
   - Use it whenever you know what you want to draw but not how the studio does it — "two point perspective box", "cast shadow contact occlusion", "notan value hierarchy", "golden ratio logo grid".
   - `scope: 'manual'` restricts to design theory; `scope: 'sdk'` to the API reference.
   - The design theory is not optional colour: the manuals carry the construction order, the numeric constants, and the QA checks that make the output read as deliberate rather than arbitrary. Prefer a documented toolkit method over hand-rolling the technique from Canvas2D primitives.

2. **Read the MCP resources:**
   - `polson-manual-index` (`polson://manual/index`) is the **design knowledge catalogue** — what each studio manual covers, its source text, and the SDK calls it binds to. Individual manuals are at `polson://manual/{NN}`.
   - `polson-sdk-index` (`polson://sdk/index`) is the **API map**: the execution model, language support (ECMAScript 2025), sandbox rules, global functions, and the complete inventory of callable objects and model schemas.
   - For each area your code will use, read:
     - `polson://sdk/core/{Area}` for method signatures, semantics, and examples (`Snap`, `Canvas2D`, `Skia`, `Drawing`, `Globals`).
     - `polson://sdk/schema/{Area}` for returned model structures and data shapes.
   - Call only methods listed in the inventory and access only documented properties.

3. **Execute your code using `ExecuteScript`:**
   - Code executes in a sandboxed modern JavaScript engine.
   - Pass `outFile: 'output.webp'` (or `artifacts/stageX.webp`) to save rendered image outputs directly to disk.
   - Returning a `SnapPaper` (or element), `CanvasRenderingContext2D`, `SkiaCanvas`, `SkiaBitmapWrapper`, or `ImageData` automatically renders the visual output headlessly to WebP/PNG/JPEG bytes (`result.ImageBytes`, defaulting to **WebP at quality=85**) and SVG XML (`result.SvgXml`).
   - Use `Session['myKey'] = ...` to cache complex intermediate layers, geometries, or color palettes across successive tool calls.
   - Use `console.log(...)` or `log(...)` to record operational notes.
   - Use `History()` to inspect recent scripts sent to the execution engine.

---

## Creative Task: Recreate `reference_images/comic1.png`

Recreate the comic character illustration from `reference_images/comic1.png` ($447 \times 380$ px):
- **Character**: Fierce auburn-haired pirate woman with dynamic high ponytail, slate headband, and loose windblown forehead curls.
- **Anatomy & Expression**: Intense rightward gaze, arched brow, defined nose, open mouth showing upper teeth shelf, warm cel-shaded facial planes, and golden hoop earring.
- **Clothing**: Popped linen shirt collar wings and navy wool coat shoulders with ink cross-hatching.
- **Background**: Diagonal wooden spar/mast, 5 helical-shaded shroud ropes, cross-ratlines, and billowing cel-shaded cumulus clouds in a coastal blue sky.

### Creative & Technical Approach
- Use Canvas 2D / Skia for continuous, volumetric Bézier curves and cel-shadow planes.
- Leverage the **Constructive Drawing Toolkit** (`Drawing.createLoomisHead()`, `Drawing.createPerspectiveGrid()`, `Drawing.drawTaperedStroke()`, `Drawing.drawFeathering()`, `Drawing.createHalftoneDotShader()`, etc.) where appropriate.
- Save intermediate drafts to `output.webp` via `ExecuteScript(script, outFile: 'output.webp')` and inspect them visually.

---

## Deliverables

Upon completing the task, produce the following deliverables in this directory:

1. **`artwork.js`**: The complete, clean JavaScript script that produces your final rendered artwork when executed.
2. **`output.webp`**: The final rendered artwork image.
3. **`findings.md`**: Your structured report evaluating the Polson SDK and MCP server experience:
   - Tag each item with: **`[positive]`**, **`[friction]`**, **`[bug]`**, or **`[nit]`**.
   - Review areas:
     - API discoverability and documentation clarity (`polson://sdk/*`).
     - Whether `Search` surfaced the right design manual for what you were trying to draw, and whether the manual passage told you which SDK call to use (`polson://manual/*`).
     - Execution model ergonomics and direct-to-disk rendering (`outFile`).
     - Vector (Snap.svg) vs Raster (Canvas 2D / Skia) integration.
     - Performance, shaders, and constructive drawing tools.

