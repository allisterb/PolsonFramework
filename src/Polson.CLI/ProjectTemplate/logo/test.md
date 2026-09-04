## What this workflow tests

`logo` is the **retained-mode vector** path and the optical-correction toolkits. Reach for
`polson://sdk/index`, then `Logo`, `LogoType`, `VectorLogo` and `Snap`.

Answer these in `findings.md`:

- **Did the mark survive `outSvg`?** A scene built on a raster canvas has no markup to save and the
  run still reports success. If you got no `.svg`, say at which point you found out.
- **The optical corrections** — `correctBoneEffect`, `computeOvershoot`, `computeOpticalCenter`,
  `computeIrradiationCompensation`. Could you tell from the documentation *what to multiply by what*,
  and did the result look right, or only measure right?
- **Counters as real geometry.** `path.subtract` against an even-odd fill: was the difference between
  them clear before you had to find out?
- **The scale ladder and the monochrome board.** Did `generateFaviconScaleTest` and
  `generateMonochromeTest` tell you something you had not already seen by looking?
- **Kerning and tracking return different units** — one em fraction, one pixels. Did that catch you?
