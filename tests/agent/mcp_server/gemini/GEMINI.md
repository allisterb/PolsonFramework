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

> **Task: Make Mona Lisa a natural, luminous blonde**

### Image Asset
The high-resolution reference painting is located at:
`reference_images/Mona_Lisa,_retouched.jpg`

You can load this image in JavaScript using:
```javascript
const img = Skia.Image.load('reference_images/Mona_Lisa,_retouched.jpg');
```

### Visual & Aesthetic Objectives
- **Target Hair Transformation**:
  - Transform her dark brown/black locks and framing curls into a rich, natural blonde (e.g. golden blonde, honey/amber highlights, with natural dark-blonde roots and soft shadowed depth).
- **Preserve Painting Integrity**:
  - Do not paint flat cartoon blocks over the hair. The transformation must preserve the **underlying brushstroke textures, hair wave details, chiaroscuro shading, transparent veil / sfumato edges, and craquelure (surface oil paint cracks)**.
- **Isolate Face & Skin**:
  - Ensure her face, forehead, cheeks, hands, neck, and the background renaissance landscape retain their original skin tones, hues, and values without discoloration.

### Recommended Techniques & Capabilities
You have access to the full Polson drawing and shader suite:
1. **Custom SkSL Shaders (`Skia.Shader.sksl(code, uniforms, children)`)**:
   - Write custom SkSL procedural pixel shaders to sample the painting (`u_image.eval(coord)`), perform color space math (e.g. RGB to HSV/HSL), apply spatial falloffs around the hair region, and remap dark hair luminance into golden blonde tones.
2. **Canvas2D Layer Compositing**:
   - Use `ctx.drawImage(img, 0, 0)`, clipping paths (`ctx.save()`, `ctx.clip()`, `ctx.restore()`), and blend modes (`ctx.globalCompositeOperation = 'soft-light' | 'color' | 'overlay' | 'screen'`).
3. **Procedural Shaders & Filters**:
   - Combine with `Skia.ImageFilter.runtimeShader`, `Skia.ColorFilter.runtimeEffect`, or subtle noise/highlight passes.

---

## Deliverables

Upon completing the task, produce the following deliverables in this directory:

1. **`artwork.js`**: The complete, clean JavaScript script that produces your final rendered artwork when executed.
2. **`findings.md`**: Your structured report evaluating the Polson SDK and MCP server experience:
   - Tag each item with: **`[positive]`**, **`[friction]`**, **`[bug]`**, or **`[nit]`**
   - Specifically evaluate the newly added **custom SkSL shader / filter system**, image loading ergonomics, performance, and color manipulation fidelity.
