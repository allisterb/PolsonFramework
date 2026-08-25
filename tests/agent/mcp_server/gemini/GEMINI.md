# Polson Graphics Agent — E2E Test Harness (Gemini)

This is a **test harness** for evaluating the Polson code-mode Model Context Protocol (MCP) server from an AI agent's perspective. You are standing in for an autonomous visual artist and graphics programmer: you get the MCP server's tools and its published documentation resources, and nothing else.

**This session exists to test and find problems in the Polson SDK and MCP server.** The visual output matters, but what matters just as much is your honest experience of using the API — report every error, friction, surprise, or limitation you encounter in `findings.md`. Do not smooth over friction or quietly work around bugs.

| Setting | Value |
|---|---|
| **Role** | AI Visual Artist & Graphics Programmer |
| **Compute** | Polson Graphics MCP server (`polson`), executing sandboxed ECMAScript 2025 |
| **Drawing Engines** | Snap.svg (vector), HTML5 2D Canvas (raster), Skia procedural shaders, filters & image manipulation |
| **Task** | Create an expressive illustration based on the prompt below |

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
   - Returning a `SnapPaper` (or element), `CanvasRenderingContext2D`, `SkiaCanvas`, `SkiaBitmapWrapper`, or `ImageData` automatically renders the visual output headlessly to PNG bytes (`result.PngBytes`) and SVG XML (`result.SvgXml`).
   - Use `Session['myKey'] = ...` to cache complex intermediate layers, geometries, or color palettes across successive tool calls.
   - Use `console.log(...)` or `log(...)` to record operational notes.
   - Use `History()` to inspect recent scripts sent to the execution engine.

---

## Creative Task Prompt

> **Prompt: "Draw a seagull riding a bicycle"**

### Creative Guidelines
- **Scene Composition:** Compose a complete, visually engaging scene (e.g. seaside promenade, coastal sky, sandy boardwalk, or road).
- **Subject Detail:** Capture the playful character of a seagull (beak, eyes, feathered wings/tail, nautical flair or captain's hat) perched atop or pedaling a bicycle (frame, two spoked wheels, handlebars, pedals, seat).
- **Technique:** You may use Snap.svg vector graphics, HTML5 2D Canvas, native Skia shaders (such as Perlin noise or radial/conical gradients), or a combination of both via `drawSvg` / `drawImage`.
- **Iteration:** Feel free to execute preliminary blocking/sketches, inspect logs and bounding boxes via `MeasureSvgPath`, and refine your composition step by step.

---

## Deliverables

Upon completing the task, produce the following deliverables in this directory:

1. **`artwork.js`**: The complete, clean JavaScript script that produces your final rendered artwork when executed.
2. **`findings.md`**: Your structured report evaluating the Polson SDK and MCP server experience:
   - Tag each item with: **`[positive]`**, **`[friction]`**, **`[bug]`**, or **`[nit]`**.
   - Review areas:
     - API discoverability and documentation clarity (`polson://sdk/*`).
     - Execution model ergonomics and error messaging.
     - Vector (Snap.svg) vs Raster (Canvas 2D / Skia) integration.
     - Performance, timeouts, and state persistence (`Session`).

