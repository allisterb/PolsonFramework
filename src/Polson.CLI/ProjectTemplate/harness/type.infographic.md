## The task: an infographic poster

Make a single finished infographic from the data table below. Not a dashboard, not a chart on a page
— a designed poster that argues something, and whose every figure is true.

Unlike the other harness tasks, **the data is supplied**. That is not a supplied answer: the design,
the forms, the composition and the argument are all yours to invent. It is supplied because an
infographic is checkable only against numbers it did not choose, and a piece drawn from invented data
cannot be wrong about anything.

### The data

> These figures are **synthetic — written for this harness**. They are plausible and internally
> consistent, and they are not measurements of the real world. Do not attribute them to a real body,
> and say on the poster that they are illustrative. Inventing a source is the one failure here that
> would matter outside this test.

**A · Daily water use per person, by fixture (litres)**

| Fixture | Litres |
| :--- | ---: |
| Shower | 65 |
| Toilet | 41 |
| Washing machine | 26 |
| Taps (kitchen and bathroom) | 22 |
| Outdoor and other | 8 |
| Dishwasher | 4 |

**B · Average daily use per person, by year (litres)**

| Year | 2015 | 2017 | 2019 | 2021 | 2023 | 2025 |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| Litres | 182 | 176 | 171 | 169 | 167 | 166 |

**C · Single figures**

| Figure | Value |
| :--- | ---: |
| Share of household water that is heated | 39% |
| Average shower length | 8.2 minutes |
| Litres per minute, standard shower head | 7.9 |
| Litres per minute, low-flow shower head | 5.1 |

Derive whatever else you need — totals, shares, the decade change — and show the arithmetic for
anything derived. Every figure that reaches the canvas must trace to a row above or to a derivation
you have written down.

### The visual language: a drafting sheet

The reader is **looking at an engineer's drawing**. Everything on the canvas belongs to that place:

- The ground is **ruled**, never flat — a fine grid with a heavier rule every fifth or tenth line,
  drawn at low contrast.
- **Cyan-white line on a deep ink ground**, or its reverse as a whiteprint. Pick one and hold it.
- **Line weight is a system**: a heavy weight for the subject, a medium for structure, a hairline for
  the grid and extension lines. Three tiers, used consistently.
- **Drafting chrome carries the detail**: registration marks, a stamped title block with a scale and
  a date, dimension lines with extension gaps, callout leaders from a datum to its label.
- The data is drawn as **part of the sheet** — a bar dimensioned like a measured length, a share as a
  hatched region — rather than as panels floating on top of it.

### Non-negotiable requirements

These should drive construction from the first line of code, not be checked at the end:

- **Canvas 1080 × 1620, portrait.** A fixed frame, so composition is a real constraint rather than
  something that expands to fit.
- **At least three genuinely different forms**, chosen by the question each answers. A poster where
  every section is a bar chart has failed this.
- **Length encodes from zero.** Any bar, column or filled area starts at zero. Series B is where this
  bites: its range is narrow, and a cropped axis turns a modest decline into a collapse.
- **Area encodes by area.** Anything sized by value is sized by area, not by radius.
- **Nothing overlaps or overflows by accident.** Text blocks are measured before what follows them is
  placed, and labels that must fit a box are fitted to it.
- **The piece must survive its own audit.** Read the figures back off the finished render and check
  them against the table above.

### Prove it, rather than asserting it

The claims above are measurable, and the SDK has native calls that measure them. **Verify at least
two of them against the rendered pixels**, not against your own code:

- that a bar's baseline sits where the axis zero is, and that bars of equal value are equal length;
- that a colour region's edges are where the geometry says they should be;
- that the palette you intended is the palette that landed.

Keep the measurement scripts — they are as much a part of the deliverable as the poster. If a
measurement contradicts what the code was supposed to do, that is the most valuable thing this run
can produce: say so in `findings.md`.

### Files to leave behind

- **`output.webp`** — the finished poster, 1080 × 1620.
- **`output.svg`** — the vector layer. A drafting sheet is mostly line work, so most of this piece
  should be vector; say in `findings.md` if it was not and what forced the raster path.

### Report specifically on

- **`Scale`.** Did value-to-pixel mapping cover what a chart needs — linear scales, bands, round
  ticks, nice bounds, area radii? What did you have to compute by hand, and should it have been
  there? Were the ticks numbers you would put on an axis?
- **`Layout` and text measurement.** Did `Layout.*` plus `ctx.measureWrappedText(...)` let you place
  a caption under a chart whose height you did not know in advance? Where did you still have to guess
  a coordinate, and why?
- **Typography.** `ctx.font` with numeric weights, quoted families and fallback lists;
  `ctx.letterSpacing` for the tracked labels this language wants; `maxWidth` for fitting a label to a
  box. Did any of them behave differently from the documentation?
- **The measurement primitives.** `bitmap.diff`, `bitmap.rowProfile` and `bitmap.palette` — did they
  let you check the render cheaply, and did they find anything looking did not? Did you hit the
  statement cap, and if so did the error tell you what to do about it?
- **`Css`, if you used it.** Carrying a palette and a type scale as tokens rather than as literals
  scattered through the scripts — did that help, and did the tokens survive into the drawing intact?
- **Manual 13.** It is this task's manual. Did it tell you how to construct the piece, or only what
  to avoid? Name a section that was not actionable.
