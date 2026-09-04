## What this workflow tests

`comic` is **page layout plus inking** — `Layout` for the grid, and the tapered-stroke, feathering
and hatching calls for the line. Reach for `polson://sdk/core/Drawing` and `polson://manual/03`.

Answer these in `findings.md`:

- **Line weight as a hierarchy.** Could you get a heavy contour, a medium interior and a light detail
  line to read as three distinct weights at final size — and did anything tell you what final size is?
- **`Skia.PathEffect.stamp` / `hatch` / `compose`.** Was it discoverable that jitter goes *under* a
  stamp rather than beside it? Did `sum` versus `compose` read clearly?
- **`Layout.rows` / `columns` / `grid`** take gaps out before dividing. Did your panels sum back to
  the page, and did you have to check?
- **The reduction test.** `bitmap.resize` down to print size and back: did the art still read? Say
  whether you thought to do it before being asked.
- **Format choice.** Flat line art is smaller and faster as PNG than WebP. Did the documentation
  reach you before you had already chosen?
