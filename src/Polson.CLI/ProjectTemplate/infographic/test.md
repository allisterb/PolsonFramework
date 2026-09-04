## What this workflow tests

`infographic` is the **data-to-pixels** path: `Scale`, `Layout`, `Css`, and the honesty rules that
make a chart true. Reach for `polson://sdk/core/Scale`, `Layout` and `Css`.

Answer these in `findings.md`:

- **The three quantitative rules** — zero baseline for bars, area rather than radius for circles, one
  shared scale across small multiples. Were they discoverable *before* you drew something wrong, or
  only afterwards?
- **`measureWrappedText` then `Layout.stack`.** Could you place a block under a paragraph without
  guessing its height? Where did the geometry stop being available?
- **`Scale.nice` and `Scale.ticks`.** Did the axis land on round numbers, and did you have to fight it?
- **`Css.parse` / `fromCss`** reads declared rules, not a computed cascade. Did that limitation bite,
  and was it stated where you would find it before relying on inheritance?
- **Where you hand-rolled arithmetic** the toolkit already had. Bar length, band width, tick spacing.
