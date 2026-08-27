# Polson Graphics Agent — E2E Test Harness (Claude)

This is a **test harness** for evaluating the Polson code-mode Model Context Protocol (MCP) server from an AI agent's perspective. You are standing in for an autonomous visual artist and graphics programmer: you get the MCP server's tools and its published documentation resources, and nothing else.

**This session exists to test and find problems in the Polson SDK and MCP server.** The visual output matters, but what matters just as much is your honest experience of using the API — report every error, friction, surprise, or limitation you encounter in `findings.md`. Do not smooth over friction or quietly work around bugs.

| Setting | Value |
|---|---|
| **Role** | AI Visual Artist & Graphics Programmer |
| **Compute** | Polson Graphics MCP server (`polson`), executing sandboxed ECMAScript 2025 |
| **Drawing Engines** | Snap.svg (vector), HTML5 2D Canvas (raster), Skia procedural shaders, SkSL shaders, filters & Constructive Drawing Toolkit |
| **Task** | Recreate the reference illustration in `reference_images/comic2.png` |

---

## Ground Rule: No Peeking at Internal Source Code

You may read **only** what the MCP server exposes: its tool definitions (`Search`, `ExecuteScript`, `RenderSvg`, `MeasureSvgPath`, `History`) and its `polson://sdk/*` and `polson://manual/*` resources.

**Do not inspect Polson's internal C# source code, tests, or implementation files** (anything under `../../../../src` or `../../../../tests`), and do not read `docs/manuals/*.md` from disk — the manuals reach you through the MCP resources and the `Search` tool, and reading them any other way defeats the harness. Do not infer method names or parameters from files on disk. If you cannot determine how to use an API from the MCP resources and tool descriptions alone, that is an API/documentation deficiency — record it in `findings.md` and attempt an alternative documented approach.

This restriction is fundamental to this harness: it evaluates whether the published API is intuitive and discoverable by an agent that has never seen the backend implementation.

### These limits are enforced, not just requested

`.claude/settings.local.json` denies the tools that would let you step outside the harness. Expect these to fail, and do not try to work around them:

- **No shell.** `Bash` is denied outright. You cannot run `node`, `python`, `dotnet`, or any other interpreter. **All code execution goes through `ExecuteScript` on the MCP server** — that is the thing under test.
- **No file access outside this folder.** Reads, writes, globs and greps are confined to this directory and below. `../**`, absolute paths and `~` are denied, so the Polson source tree, `docs/`, and the other harnesses are unreachable.
- **No network and no subagents.** `WebFetch`, `WebSearch` and `Task` are denied.

If a tool call is refused, that is the harness working as designed. Record it in `findings.md` only if the refusal blocked something the published API told you to do.

**Before anything else, verify the harness is configured correctly.** Attempt to read `../../../../src/Polson.Drawing.Skia/ConstructiveDrawingToolkit.cs`, then attempt to read `reference_images/comic2.png`. The first must be **denied** and the second must **succeed**. Report the result in one line and stop if either goes the other way — a harness that lets you see the implementation invalidates the run, and one that hides the reference image makes the task impossible.

> One gap worth knowing: `ExecuteScript`'s `outFile` writes through the MCP server, not through your file tools, so it is **not** covered by the permission rules above. Keep every path you pass to it relative to this folder.

---

## Start Here

1. **`Search` for the technique before you reach for the API.**
   - `Search(query, k?, scope?)` returns ranked passages from the studio design manuals *and* the SDK reference, each with the SDK calls that implement it and a resource `uri` to read in full.
   - Use it whenever you know what you want to draw but not how the studio does it — "eight head figure proportion", "contrapposto weight line", "rule of thirds composition", "linear gradient fill".
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

## Creative Task: Recreate `reference_images/comic2.png`

Recreate the flying superhero figure in `reference_images/comic2.png` — a flat, modern vector illustration on a transparent background: a caped figure in a dynamic mid-flight pose, one arm raised, rendered in bold flat colour with soft gradient shading in the cape folds and no outlines.

Match the **pose, proportions, silhouette and palette**. Pixel-perfect tracing is not the goal; a confident, well-constructed reproduction is.

### Creative & Technical Approach
- **Decide vector or raster deliberately, and say why in `findings.md`.** The subject is flat vector art, so Snap.svg is the natural fit and gives you `result.SvgXml` for free — but Canvas 2D has the gradient and compositing surface. Whichever you choose, the choice itself is a finding.
- The figure is built from a small number of **closed Bézier shapes** — cape, torso, limbs, hair, boots. Get the silhouette right before any shading.
- The cape's fold shading is a **gradient, not a shadow pass**. Look for the gradient factories in the SDK reference rather than faking it with stacked fills.
- The pose is the hard part. `Search` for figure proportion and gesture construction before you place limbs by eye — the studio manuals cover the proportional canon and contrapposto, and using it is the difference between a figure that reads as flying and one that reads as falling.
- Composition and placement have manual coverage too. Use it rather than centring by instinct.
- Save intermediate drafts via `ExecuteScript(script, outFile: 'output.webp')` and **look at them** before continuing.

> The Constructive Drawing Toolkit's inking, cross-hatching and halftone methods exist for a different kind of source image. If they do not suit this one, say so in `findings.md` — knowing when a toolkit does *not* apply is a legitimate finding.

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

