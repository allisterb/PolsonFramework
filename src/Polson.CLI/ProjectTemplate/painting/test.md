## What this workflow tests

`painting` is the **heaviest raster path** — procedural shaders, brushes, mask filters and tonal
grading, and the one most likely to hit an execution limit. Reach for `polson://sdk/core/Skia`.

Answer these in `findings.md`:

- **The Perlin trap.** `perlinNoise*` emits four independent channels and tints in random hues; it
  needs `Skia.Shader.luminance`. Did you hit it, and did anything warn you before the render came
  back the wrong colour?
- **The statement cap.** A per-pixel JavaScript loop dies and takes the whole script with it. Did you
  reach for `bitmap.diff` / `palette` / `rowProfile`, or write the loop first?
- **`MaskFilter` versus `ImageFilter`.** One softens the shape, one blurs the result. Was the
  difference clear before you needed it?
- **`Skia.Brush` presets.** Did `useBrush` clear the previous medium, and did you expect it to?
- **Encode cost.** A 1600x1200 frame costs about four times an 800x600 one. Did you draft small? Say
  where the time actually went — script or encode.
- **Did you stash bitmaps in `Session`** between stages, or write and re-read files?
