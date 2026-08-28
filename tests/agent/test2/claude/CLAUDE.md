# Polson Graphics Agent — E2E Test Harness (Claude)

This is a **test harness** for evaluating the Polson code-mode Model Context Protocol (MCP) server from an AI agent's perspective. You are standing in for an autonomous brand designer working in code: you get the MCP server's tools and its published documentation resources, and nothing else.

**This session exists to test and find problems in the Polson SDK and MCP server.** The logo matters, but your honest experience of using the API matters just as much — record every error, friction, surprise and limitation in `findings.md`. Do not smooth over friction or quietly work around bugs.

| Setting | Value |
|---|---|
| **Role** | AI Brand & Logo Designer, working entirely in code |
| **Compute** | Polson Graphics MCP server (`polson`), executing sandboxed ECMAScript 2025 |
| **Toolkits** | `Logo` (geometry, optical tuning, scale testing), `LogoType` (kerning, pairing, lockups), `VectorLogo` / `Snap` (retained-mode SVG), `Canvas2D` + `Skia` (raster presentation), and cloud **asset requisition** |
| **Client** | "Sailboat Tours" — romantic sailboat cruises |

---

## Ground Rule: No Peeking at Internal Source Code

You may read **only** what the MCP server exposes: its tool definitions (`Search`, `ExecuteScript`, `RenderSvg`, `MeasureSvgPath`, `History`) and its `polson://sdk/*` and `polson://manual/*` resources.

**Do not inspect Polson's internal C# source, tests, or implementation files**, and do not read `docs/*.md` from disk — the manuals and reference reach you through MCP resources and the `Search` tool, and reading them any other way defeats the harness. Do not infer method names or parameters from files on disk. If you cannot work out how to use an API from the MCP resources and tool descriptions alone, **that is an API or documentation defect** — record it in `findings.md` and try another documented approach.

### These limits are enforced, not just requested

`.claude/settings.local.json` denies the tools that would let you step outside the harness:

- **No shell.** `Bash` is denied. All code execution goes through `ExecuteScript` — that is the thing under test.
- **No file access outside this folder.** Reads, writes, globs and greps are confined to this directory and below. The other agent harnesses, and this folder's own `archive/`, are denied — they hold unrelated prior runs, and their findings would contaminate yours.
- **No network and no subagents.** `WebFetch`, `WebSearch` and `Task` are denied.

**Before anything else, verify the harness**, in this order:

1. Attempt to read `../../../../src/Polson.MCPServer/JsDrawingEngine.cs` — must be **denied**.
2. Attempt to read `../gemini/GEMINI.md` — must be **denied**.
3. Read `.mcp.json` in this folder — must **succeed**.

Report all three on one line. **If either of the first two succeeds, stop immediately and report it** — do not begin the task. An agent that has seen the implementation, or another agent's brief for the same task, can no longer tell anyone whether the published API is sufficient, which is the only thing this harness measures.

> `ExecuteScript`'s `outFile` writes through the MCP server, not your file tools, so it is **not** covered by those permission rules. Keep every path you pass to it relative to this folder.

---

## The Brief

**Sailboat Tours** runs romantic sailboat cruises — couples, sunset and moonlight sailings, small boats, not cruise liners. They need a primary logo.

There is no reference image and no house style. The mark is yours to invent.

### What you must deliver as artwork

1. **A primary mark**, constructed as **vector geometry** — this is the master artwork.
2. **A wordmark lockup** setting "Sailboat Tours" with the mark, optically spaced.
3. **A brand presentation sheet** showing the identity as it would be presented to the client.

### Non-negotiable brand requirements

These are the constraints a real identity has to survive, and they should drive your construction decisions from the first line of code rather than being checked at the end:

- **It must read at 16px.** The mark becomes a favicon and an app icon. Detail that dissolves at that size is not detail, it is noise.
- **It must reproduce in a single flat colour.** Solid black on white, and knocked out white on a dark ground, with no loss of identity. Anything that only reads in full colour has failed.
- **It must be built on a deliberate geometric armature**, not placed by eye — a golden-ratio construction, a polar grid, an isometric grid, or a monogram matrix. The armature should be visible in your staged artifacts even though it is absent from the final mark.
- **"Romantic" is the brief's operative word.** It has to be legible in the form, the palette and the typography, not asserted in a caption. Search the manuals for what actually encodes that, rather than reaching for a heart shape.

Prove the first two rather than asserting them: the SDK has calls that generate the multi-scale legibility ladder and the monochrome/knockout contrast board. **Render them and look at them**, and keep them as artifacts. If a scale test shows the mark failing at 16px, that is a result — fix the mark and say so in `findings.md`.

---

## Asset Requisition — and a judgment call

This studio can requisition **raw material** from a cloud image model: flat tiling textures, background plates, and greyscale mattes. Read `polson://sdk/core/Assets` before using it.

1. **It cannot draw your logo.** There is no call that returns a finished mark. Ask for an object rather than a material and you will be refused — "a sailboat" is refused, "weathered teak decking" is not. Form is yours to construct in code.
2. **It costs real money and the budget is finite.** Check `Assets.budget.remaining` before requisitioning. A requisition takes several seconds, so **requisition in its own short script and draw in the next one**, or you risk the execution timeout. Identical requests are cached and free.
3. **Nothing throws.** Every requisition returns a result — check `success`, then read `remedy`. If requisition is unavailable in this run, that is a legitimate configuration: draw procedurally instead, say so in `findings.md`, and carry on.

**Then there is a second boundary, and this one the SDK does not enforce for you.** Requisition is available to you on this task, and texture is genuinely useful *somewhere* in a brand identity. Where it belongs — and where it would actively damage the work — is your call to make and to defend. Decide deliberately, state the rule you followed in `findings.md`, and make sure the brand requirements above are still met afterwards. A logo that fails the 16px or single-colour test because of a decision you made here is a failed logo, however good the texture looks at full size.

Requisition is optional throughout. A strong identity drawn entirely in code beats a weak one propped up by generated assets.

---

## Start Here

1. Verify the harness (all three checks above), in one line.
2. Read `polson://sdk/index` for the map, then the areas you need — `Logo`, `LogoType`, `VectorLogo`, `Snap`, `Canvas2D`, `Skia`, `Assets`.
3. `Search` for the technique before reaching for the API. `Search(query, k?, scope?)` returns ranked passages from the studio design manuals *and* the SDK reference, each with the SDK calls that implement it and a resource `uri` to read in full; `scope: 'manual'` restricts to design theory, `scope: 'sdk'` to the API reference. The manuals cover golden-ratio construction, optical correction, negative space, grid systems, type pairing and contrast. Note in `findings.md` whether search actually found what you needed — and whether the passage told you which SDK call implements it.
4. **Sketch several distinct directions before committing to one.** A single idea developed straight to finish is the most common way to arrive at a mediocre mark. Render the alternatives, look at them, then choose and say why.
5. Build up in stages, saving renders into `artifacts/` and **looking at them** as you go. Perceiving your own output and revising is the point, not an optional extra.

Use `Session['myKey'] = ...` to carry palettes, geometry or requisitioned material across successive `ExecuteScript` calls, and `History()` to inspect recent scripts.

---

## Deliverables

In this directory:

1. **`output.svg`** — the primary mark as vector. This is the master artwork.
2. **`output.webp`** — the brand presentation sheet. Landscape, at least 1400×900.
3. **`artwork.js`** — the complete script that produces both.
4. **`findings.md`** — the report. Structure it however you like, but cover:
   - **What broke.** Errors, wrong results, misleading documentation, anything you worked around.
   - **What you could not find.** Every time you searched or read a resource and did not get what you needed. If you concluded a capability did not exist, say what you searched for — a wrong "it doesn't exist" is the most expensive failure this harness looks for, so if you later found it *did* exist, that is the single most valuable thing you can report.
   - **What misled you.** Answers you acted on that turned out to be wrong for your task. Costlier than finding nothing.
   - **What you hand-rolled** that the SDK already provided.
   - **The vector surface specifically.** This task is vector-first in a way previous work has not been. Is `Snap`/`VectorLogo` as complete and as documented as the raster side? Where did you have to drop to Canvas2D, and did you lose anything crossing over?
   - **The `Logo` and `LogoType` toolkits.** Did the optical-tuning calls (bone effect, overshoot, optical centre, tangent blends) do something you could actually see? Did the kerning and pairing calls produce spacing you would defend to a client?
   - **Requisition.** Was the material-versus-form boundary clear? Did a refusal make sense? Did you know what to do next after a failure? And what rule did you settle on for where texture belongs in an identity?
   - **The harness itself.** Report the result of the three verification checks, and any tool refusal that blocked something the published API told you to do.
   - **Time and iterations.** Roughly how many attempts to a first correct call, and where the time actually went.

Keep `findings.md` open as you work — write entries when they happen, not reconstructed at the end.
