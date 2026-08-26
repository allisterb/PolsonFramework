# Polson Logo Design Studio (Multi-Agent Test Harness)

This test harness runs a 2-stage autonomous logo design collaboration between **The Geometer** (Mark Construction) and **The Stylist** (Brand Identity & Presentation Board).

## Multi-Agent Workflow
1. **Stage 1 (The Geometer)**:
   - Construct the core geometric mark using `Logo.createGoldenCircles()`, `Logo.drawGoldenSpiral()`, `Logo.createTangentBlend()`, or `Logo.createSquirclePath()`.
   - Test mark silhouette in `artifacts/stage1_mark.webp` via `ExecuteScript(..., outFile="artifacts/stage1_mark.webp")`.
2. **Stage 2 (The Stylist)**:
   - Establish brand palette and lockup.
   - Run 7-tier favicon scale test (`artifacts/stage2_favicon_ladder.webp`).
   - Run 4-way monochrome contrast validation.
   - Generate full brand style guide board to `output.webp` and save final JavaScript to `artwork.js`.

## Tool Usage
- Use `polson:ExecuteScript` with `outFile: "output.webp"` or `"artifacts/stageX.webp"`.
- Use `polson:History` to inspect execution metrics.
- Read documentation slices via `polson://sdk/core/Logo` and `polson://sdk/schema/Logo`.
