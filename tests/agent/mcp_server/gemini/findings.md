# Polson SDK — Agent Findings (Gemini Harness)

*Comprehensive evaluation report of the Polson Model Context Protocol (MCP) server, ECMAScript 2025 sandboxed graphics execution engine, and public `polson://sdk/*` documentation resources.*

**Tags:** `[positive]` / `[friction]` / `[bug]` / `[nit]`

---

## 1. Executive Summary

During this test harness session, the Polson Graphics MCP server was evaluated strictly as a black-box system through its MCP tool interfaces (`ExecuteScript`, `MeasureSvgPath`, `History`, `RenderSvg`) and exposed `polson://sdk/*` resources.

The creative task — *"Draw a seagull riding a bicycle"* — was completed end-to-end, producing both the executable JavaScript artwork script (`artwork.js`) and the high-resolution rendered output (`output.png`). The artwork features a full coastal promenade scene with atmospheric sky gradients, layered procedural cumulus clouds, distant headland and lighthouse with a glowing light beam, deep ocean wave crests, wooden boardwalk floor with perspective planking and cast drop shadows, a vintage seafoam-teal beach cruiser bicycle with cream-wall tires and chrome fenders/fittings, and a character-rich seagull captain wearing a navy peaked cap and red sailor bandana, holding the handlebars with one wing and exuberantly saluting with the other while pedaling with webbed feet.

Overall, the Polson MCP server provides an exceptionally capable, high-performance, and versatile graphics environment that uniquely unifies Snap.svg vector graphics, HTML5 2D Canvas raster operations, and Skia procedural shaders/filters within a sandboxed ECMAScript 2025 engine.

---

## 2. API Usability & Documentation (`polson://sdk/*`)

### Strengths
- **`[positive]` Map-then-Areas Architecture:** The documentation layout centered around `polson://sdk/index` acting as the structural index, pointing directly to modular core references (`polson://sdk/core/{Area}`) and model schemas (`polson://sdk/schema/{Area}`), made API discovery fast, predictable, and context-efficient.
- **`[positive]` Complete Callable Inventory:** Listing every method signature with types and arities in the index resource allowed immediate verification of available features without guessing.
- **`[positive]` Accurate Method Signatures:** Methods across Canvas2D, Snap.svg, and Skia matched modern standards (e.g. HTML5 Canvas 2D specification and Snap.svg conventions).

### Friction & Inconsistencies
- **`[nit]` Envelope JSON Casing Discrepancy:** The index documentation (`polson://sdk/index` line 80-118) describes the `DrawingExecutionResult` schema using PascalCase property names (`Success`, `Error`, `SvgXml`, `PngBytes`, `Logs`, `ExecutionTimeMs`, `ReturnValue`), whereas the actual MCP JSON-RPC tool result returned by `ExecuteScript` serializes properties in standard camelCase (`success`, `error`, `svgXml`, `pngBytes`, `pngDataUrl`, `logs`, `executionTimeMs`).
- **`[positive]` Model Property Flexibility:** Object properties returned by C# models into the Jint JavaScript runtime (e.g., `pt.x` vs `pt.X`, `bbox.width` vs `bbox.Width`, `canvas.width` vs `canvas.Width`) correctly resolve case-insensitively / via Jint's camelCase member mapping.

---

## 3. Drawing Engine & Execution Mechanics

### Snap.svg Vector Graphics
- **`[positive]` Rich Vector Primitives & Transforms:** Support for `Snap(w, h)`, `paper.circle`, `paper.rect`, `paper.line`, `paper.path`, `paper.g`, and matrix transforms (`Snap.matrix()`, `matrix.rotate()`, `matrix.toTransformString()`) is robust and serializes cleanly to SVG XML and PNG bytes.
- **`[positive]` Path Geometry Measurement:** Both `Snap.path.getBBox`, `Snap.path.getTotalLength`, and `Snap.path.getPointAtLength` worked reliably inside scripts, and matched the output of the standalone `MeasureSvgPath` tool.
- **`[friction]` Group Primitive Creation vs Paper:** In Snap.svg, attempting to call primitive creation methods directly on a group element (e.g. `g.circle(...)`) throws a runtime exception (`Property 'circle' of object is not a function`). Primitives must be instantiated via `paper.circle(...)` and appended via `element.appendTo(g)` or passed to `paper.g(...)`. While this conforms to canonical Snap.svg behavior, adding factory methods to `SnapElement` or documenting this distinction prominently would reduce friction for developers transitioning from other vector APIs.

### HTML5 2D Canvas & Gradients
- **`[positive]` Native Canvas Compatibility:** Full support for `createCanvas(w, h)`, `ctx.createLinearGradient()`, `ctx.createRadialGradient()`, `ctx.createConicGradient()`, `ctx.beginPath()`, `ctx.arc()`, `ctx.quadraticCurveTo()`, `ctx.bezierCurveTo()`, `ctx.roundRect()`, and state stack methods (`ctx.save()`, `ctx.restore()`).
- **`[positive]` Smooth Headless Rendering:** High quality anti-aliasing and sub-pixel path rendering without visual artifacts or edge degradation.

### Skia Shaders, Filters & Hybrid Compositing
- **`[positive]` Procedural Shaders:** `Skia.Shader.perlinNoiseTurbulence(0.02, 0.02, 3, 42)` and other procedural noise shaders plug directly into `ctx.fillStyle` and render immediately.
- **`[positive]` Image Filters & Gaussian Blur:** Direct assignment of Skia image filters like `ctx.filter = Skia.ImageFilter.blur(12, 12)` and `Skia.ImageFilter.dropShadow(...)` to canvas context produces smooth, realistic shadows and glow effects without needing manual convolution passes.
- **`[positive]` Vector/Raster Interop (`drawSvg`):** `ctx.drawSvg(snapPaper, x, y, w, h)` seamlessly embeds vector scenes onto raster canvases.

---

## 4. State & Session Management (`Session`, `History`)

### Session Scratchpad
- **`[positive]` Multi-Step Persistence:** `Session['key'] = value` persisted objects, palettes, geometries, and intermediate computations seamlessly across subsequent `ExecuteScript` tool calls on the same MCP connection.
- **`[positive]` Fast In-Memory State:** Zero overhead observed when retrieving cached state across tool executions.

### MCP Tools Integration
- **`[positive]` `ExecuteScript` Tool:** Immediate execution, clear error logs with line numbers, and fast feedback loops (execution times between 5ms and 440ms).
- **`[positive]` `MeasureSvgPath` Tool:** Returns path length, tight bounding boxes, and interpolated coordinates along path contours.
- **`[positive]` `History` Tool:** Recalls past script executions accurately with `History({ n })`.
- **`[positive]` `RenderSvg` Tool:** Direct headless rendering from SVG XML strings to PNG buffers.

### Execution Safeguards
- **`[positive]` Infinite Loop Protection:** Statement limits (500,000 statements) cleanly catch runaway `while(true)` loops and return informative diagnostics (`The maximum number of statements executed have been reached`) without hanging or crashing the server process.
- **`[positive]` Clean Early Exit:** Global `exit(message)` terminates execution gracefully, logging the exit reason and preserving outputs created up to that point.

---

## 5. Detailed Findings & Bug Matrix

| ID | Tag | Area | Finding / Description |
|---|---|---|---|
| **F-01** | `[positive]` | Documentation | The map-then-areas structure (`polson://sdk/index` -> `core/*`, `schema/*`) is well organized and comprehensive. |
| **F-02** | `[nit]` | Documentation | Result schema in `polson://sdk/index` lists PascalCase properties (`Success`, `PngBytes`), but JSON payload returns camelCase (`success`, `pngBytes`, `pngDataUrl`). |
| **F-03** | `[positive]` | Engine | ECMAScript 2025 features (destructuring, arrow functions, template literals, optional chaining) work smoothly in Jint runtime. |
| **F-04** | `[friction]` | Snap.svg | `SnapElement` (e.g. `<g>`) does not have primitive creation methods (`circle`, `rect`), requiring creation on `paper` and `appendTo()`. |
| **F-05** | `[positive]` | Skia / Canvas | `Skia.ImageFilter.blur` integrates with `ctx.filter` for high-quality soft shadow generation. |
| **F-06** | `[positive]` | Canvas 2D | `ctx.roundRect` correctly supports array syntax for per-corner radii `[tl, tr, br, bl]`. |
| **F-07** | `[positive]` | Performance | Execution times are consistently under 500ms even for complex scenes with thousands of draw commands. |
| **F-08** | `[positive]` | State | `Session` scratchpad dictionary provides reliable cross-invocation memory. |
| **F-09** | `[positive]` | Diagnostics | Syntax errors and undefined property access return clear line and column diagnostics. |
| **F-10** | `[positive]` | Safety | Sandboxed execution enforces recursion limits and statement caps against infinite loops. |

---
