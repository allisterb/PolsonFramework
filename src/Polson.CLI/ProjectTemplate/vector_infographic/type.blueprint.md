## Design language: blueprint

The director has set the visual frame. The reader is **looking at a working drawing** — a drafting
sheet, not a page. Everything on the canvas belongs to that fiction: the ground is ruled, the chrome
is drafting chrome, and the data is drawn as if measured off the sheet.

This is the clearest case of Manual 13 §4's *diegetic data*: a chart here is not a panel placed on a
drawing, it is a detail **of** the drawing. That is what the language is for, and a floating rounded
card destroys it more completely than any colour mistake could.

- **The ground is ruled and never flat.** A fine grid at one spacing, a heavier rule every fifth or
  tenth line. Draw it once into the background, at low contrast — it should be legible when looked
  for and invisible when not.
- **Cyan-white on a deep ink ground**, or its reverse as a whiteprint: warm off-white paper with a
  Prussian-blue line. Pick one and hold it. Values come from line weight and dash pattern far more
  than from hue.
- **Line weight is a system, not a preference.** Three tiers: a heavy outline for the subject, a
  medium for structure, a hairline for the grid and the extension lines. `Skia.Brush.ink(color,
  width)` applied through `ctx.useBrush(...)` keeps them consistent across scripts.
- **Drafting chrome carries the detail density** §4 asks for: registration crosses at the corners, a
  stamped title block with project, scale and date, revision letters in circles, dimension lines with
  arrowheads and extension gaps, section markers, a north arrow. These are the small deliberate
  artifacts that separate a crafted sheet from a blue rectangle.
- **Label with leader lines**, not with captions floating nearby. A leader line from the datum to the
  label is both the drafting idiom and an honest statement of what the number refers to.
- **Annotate in a monospace or a technical sans**, small, tracked wide, upper case. Confirm it with
  `Skia.Font.has(...)`.

**Where the data goes:** dimension a bar as if it were a measured length, with witness lines and a
figure above the line. Draw a share as a hatched region with a callout. `Skia.PathEffect.hatch(width,
spacing, angle)` gives real hatching as geometry, which is the correct texture here — it scales and
exports as vector, unlike a shaded fill.

**What defeats it:** a blue background with ordinary charts on top. If the grid, the title block and
the leader lines were removed and the piece still read the same, the language was never applied — it
was a colour scheme.

> [!IMPORTANT]
> **Vector substitutions.** This variant was written for the raster workflow and names two
> canvas-only calls. Keep the intent, change the tool: `ctx.useBrush(Skia.Brush.ink(w))` becomes
> `.attr({ stroke: colour, 'stroke-width': w })` applied consistently, and
> `Skia.PathEffect.hatch(width, spacing, angle)` becomes drawn hatch lines in a `<g>` or a
> `paper.ptrn(...)` tile referenced as a fill. Both are still real geometry, which is what the
> drafting idiom wants anyway.
>
> **Two vector-only tools suit this language particularly well, and the ruled ground is where both
> earn their place.** A measured run drew that ground as **236 separate `<line>` elements**, each
> carrying its own `stroke`, `stroke-width` and `opacity` — some seven hundred attribute writes for
> what is two rules:
>
> ```js
> for (let x = 0; x <= W; x += 20) grid.line(x, 0, x, H).attr({ class: x % 100 ? 'fine' : 'major' });
> paper.style('.fine { stroke: #0A2347; stroke-width: .5; opacity: .35; } '
>           + '.major { stroke: #133A6E; stroke-width: 1; opacity: .7; }');   // last, as always
> ```
>
> That is also how the three line-weight tiers stay a *system* rather than a habit: declare `.heavy`,
> `.structure` and `.hairline` once, class every element, and the delivered sheet carries one rule a
> designer edits instead of hundreds of baked attributes.
>
> **A very fine `paper.filter()` grain over the ground is what makes it read as a printed sheet
> rather than as a fill**, and that is true of *either* palette — a dark blueprint is as much a print
> as a whiteprint is. Keep it near-invisible; a drafting sheet is printed, not painted. The same run
> that drew 236 lines by hand used no filter at all and its ground reads as a flat colour.
>
> **Brush strokes are the wrong tool here** and are worth naming as such: a working drawing is ruled
> and instrumented, so a brushed mark reads as a different document. The exception is a deliberate
> annotation — a redline, a hand-added revision note — where the contrast with the ruled ground is
> the point.
