## Design language: editorial

The director has set the visual frame. The reader is **leafing through a magazine feature** — a piece
with a byline, a standfirst and a considered opening, not a report. The type does the work here, and
the composition is a spread rather than a grid.

- **Two families in real contrast**, not two that merely differ: a display serif for the headline and
  the numbers, a quiet sans or a text serif for everything else. Run
  `LogoType.evaluateFontPairing(...)` before committing — a `conflicting` verdict means the two are
  too alike, which is the failure this language falls into most often. Confirm both with
  `Skia.Font.has(...)` and give each a fallback list in the right category.
- **The headline is display-sized and tightly tracked.** `LogoType.computeWordmarkTracking(size,
  isAllCaps)` returns an em fraction that goes straight onto `ctx.letterSpacing`. Large type set at
  default tracking is the single clearest tell of a graphic that was not typeset.
- **A standfirst carries the claim** in one or two lines at an intermediate size, italic or a lighter
  weight, measured with `ctx.measureWrappedText(...)` so what follows can be stacked beneath it.
- **Asymmetric split, never 1:1.** Manual 13 §4's *editorial spread*: `Layout.columns(page, [62, 38],
  gutter)` or `[75, 25]`. The narrow column is the anchor and holds the biggest thing on the canvas —
  a drop figure, a vertical rule of statistics, a rotated section label.
- **Pull-quote the hero number.** One figure gets treated as a pull quote: display size, its own
  space, a rule above and below. That is the piece's centre of gravity, and everything else is
  subordinate to it.
- **Paper, not white.** A warm off-white ground with a subtle tone; the ink a near-black with a hint
  of the accent hue rather than `#000`.

**Where the data goes:** charts are set like figures in a feature — a hairline rule above, a small
caps caption below, direct labels rather than a legend. Keep the chrome quiet, per Manual 13 §3: an
axis line and endpoint labels usually beat a full grid here.

**What defeats it:** a headline that is merely large. Editorial depends on the *interval* between
sizes — headline, standfirst, body, caption should read as four distinct registers. If a reader
cannot tell the caption from the body at a glance, the hierarchy has collapsed and no amount of
serif will rescue it.

> [!IMPORTANT]
> **Vector substitution.** `ctx.letterSpacing` and `ctx.measureWrappedText` are canvas-only.
> Use the SVG `letter-spacing` attribute for tracking, and measure with `element.getBBox()`,
> placing wrapped lines yourself.
