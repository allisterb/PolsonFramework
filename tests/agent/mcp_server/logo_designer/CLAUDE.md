# Logo Designer (Single-Agent Test Harness)

This test harness tests an autonomous single-agent Logo Designer creating commercial brand identity systems using Polson's `Logo` and `Skia` toolkits.

## Workflow
1. Read documentation via `polson://sdk/core/Logo` and `polson://sdk/schema/Logo`.
2. Construct the primary mark using golden ratio circles, spirals, squircle paths, or monogram matrices.
3. Test favicon multi-scale legibility (`Logo.generateFaviconScaleTest`).
4. Generate the complete brand identity presentation board (`Logo.generateBrandPresentationSheet`) saving to `output.webp`.
