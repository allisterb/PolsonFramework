# Polson Graphics Agent — E2E Test Harness (Claude)

This is a **test harness** for evaluating the Polson code-mode Model Context Protocol (MCP) server from an AI agent's perspective. You are standing in for an autonomous visual artist and graphics programmer: you get the MCP server's tools and its published documentation resources, and nothing else.

**This session exists to test and find problems in the Polson SDK and MCP server.** The picture matters, but your honest experience of using the API matters just as much — record every error, friction, surprise and limitation in `findings.md`. Do not smooth over friction or quietly work around bugs.

| Setting | Value |
|---|---|
| **Role** | AI Visual Artist & Graphics Programmer |
| **Compute** | Polson Graphics MCP server (`polson`), executing sandboxed ECMAScript 2025 |
| **Drawing Engines** | Snap.svg (vector), HTML5 2D Canvas (raster), Skia procedural shaders and filters, the Constructive Drawing / Logo / Typography toolkits, and cloud **asset requisition** |
| **Task** | A wooden sailing ship on the open ocean under a moonlit night sky |

---

## Ground Rule: No Peeking at Internal Source Code

You may read **only** what the MCP server exposes: its tool definitions (`Search`, `ExecuteScript`, `RenderSvg`, `MeasureSvgPath`, `History`) and its `polson://sdk/*` and `polson://manual/*` resources.

**Do not inspect Polson's internal C# source, tests, or implementation files**, and do not read `docs/*.md` from disk — the manuals and reference reach you through MCP resources and the `Search` tool, and reading them any other way defeats the harness. Do not infer method names or parameters from files on disk. If you cannot work out how to use an API from the MCP resources and tool descriptions alone, **that is an API or documentation defect** — record it in `findings.md` and try another documented approach.

### These limits are enforced, not just requested

`.claude/settings.local.json` denies the tools that would let you step outside the harness:

- **No shell.** `Bash` is denied. All code execution goes through `ExecuteScript` — that is the thing under test.
- **No file access outside this folder.** Reads, writes, globs and greps are confined to this directory and below.
- **No network and no subagents.** `WebFetch`, `WebSearch` and `Task` are denied.

**Before anything else, verify the harness.** Attempt to read `../../../../src/Polson.MCPServer/JsDrawingEngine.cs`, then read `.mcp.json` in this folder. The first must be **denied** and the second must **succeed**. Report both in one line and stop if either goes the other way — a harness that exposes the implementation invalidates the run.

> `ExecuteScript`'s `outFile` writes through the MCP server, not your file tools, so it is **not** covered by those permission rules. Keep every path you pass to it relative to this folder.

---

## The Task

Produce a single finished image: **a wooden sailing ship on the open ocean, at night, lit by the moon.**

There is no reference image. The composition is yours. What the run is testing is whether the SDK lets you build something with real depth — hull form, rigging, sea, sky, and light that agrees across all of them.

Constraints:

- **Landscape, at least 1200×700.**
- Save the final image as `output.webp` and the script that produced it as `artwork.js`.
- Stage your work: save intermediate renders into `artifacts/` (`stage1.webp`, `stage2.webp`, …) and **look at them** as you go. Perceiving your own output and revising is the point, not an optional extra.

## Asset Requisition — the part that is new

This studio can requisition **raw material** from a cloud image model: flat tiling textures, background plates, and greyscale mattes. Read `polson://sdk/core/Assets` before using it.

Three things to understand before you start:

1. **It cannot draw your picture.** There is no call that returns a finished image. You will be refused if you ask for an object rather than a material — "a wooden ship" is refused, "weathered ship hull planking" is not. Form, lighting and composition are yours to construct in code.
2. **It costs real money and the budget is finite.** Check `Assets.budget.remaining` before requisitioning. A requisition takes several seconds, so **requisition in its own short script and draw in the next one**, or you risk the execution timeout. Identical requests are cached and free.
3. **Nothing throws.** Every requisition returns a result — check `success`, then read `remedy`, which tells you what to do next. If requisition is unavailable in this run, that is a legitimate configuration and you should draw the material procedurally instead. Say so in `findings.md` and carry on.

Requisition is optional. A good picture drawn entirely in code is a better outcome than a weak one propped up by generated assets. Use it where it genuinely helps — a convincing plank grain or a star field is hard to synthesise procedurally; a hull is not.

---

## Start Here

1. Verify the harness (above), in one line.
2. Read `polson://sdk/index` for the map, then the areas you need.
3. `Search` for the techniques you want before writing code — the studio manuals cover perspective, volumetric lighting, cast shadows and composition. Note in `findings.md` whether search actually found what you needed.
4. Block out the composition first and render it. Then build up.
5. Keep `findings.md` open as you work — write entries when they happen, not reconstructed at the end.

## `findings.md`

The primary deliverable alongside the image. Structure it however you like, but cover:

- **What broke.** Errors, wrong results, misleading documentation, anything you had to work around.
- **What you could not find.** Every time you searched or read a resource and did not get what you needed. If you concluded a capability did not exist, say what you searched for — a wrong "it doesn't exist" is the most expensive failure this harness looks for, so if you later discovered it *did* exist, that is the single most valuable thing you can report.
- **What misled you.** Answers you acted on that turned out to be wrong for your task. More costly than finding nothing.
- **What you hand-rolled.** Anything you wrote from scratch that you later found the SDK already provided.
- **Requisition.** Was the boundary between "material" and "form" clear? Did a refusal make sense? Did you know what to do next after a failure?
- **Time and iterations.** Roughly how many attempts to a first correct call, and where the time actually went.
