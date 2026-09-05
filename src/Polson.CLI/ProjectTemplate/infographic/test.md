## What this workflow tests

`infographic` is the **data-to-pixels** path: `Chart`, `Scale`, `Layout`, `Css`, and the honesty
rules that make a chart true. Read `polson://sdk/index` first and go where it sends you, rather than
working from what you remember of this SDK — the surface has changed.

Answer these in `findings.md`:

- **Discovery, before anything else.** What did you find on the index, and what did you only find
  after you had already built something by hand? Name the call you would have used had you known it
  existed. This is the question the rest depend on: a toolkit nobody can find is a toolkit nobody
  has.
- **`Chart.*` against a hand-rolled equivalent.** A chart model carries `slots`, `ticks`, `labels`,
  `lieFactor` and `encodingRank`. Where did that save you work, and where did you have to leave it
  and go back to `Scale` and `Layout` directly? A form you wanted and could not find is worth more
  here than one that worked.
- **`slots` as an armature.** The claim is that a chart is a frame you draw whatever you like into,
  and that a mark routine written for one form works unchanged on another. Did it hold on this
  piece, or did you end up drawing rectangles?
- **The three quantitative rules** — zero baseline for bars, area rather than radius for circles, one
  shared scale across small multiples. Were they discoverable *before* you drew something wrong, or
  only afterwards? `lieFactor` and `isZeroBased` are meant to make two of them checkable rather than
  remembered; did you check them?
- **`measureWrappedText` then `Layout.stack`.** Could you place a block under a paragraph without
  guessing its height? Where did the geometry stop being available?
- **`Scale.nice` and `Scale.ticks`.** Did the axis land on round numbers, and did you have to fight it?
- **`Css.parse` / `fromCss`** reads declared rules, not a computed cascade. Did that limitation bite,
  and was it stated where you would find it before relying on inheritance?
- **Where you hand-rolled arithmetic** the toolkit already had. Bar length, band width, tick spacing,
  lane packing on a timeline, icon counts on a pictogram.
