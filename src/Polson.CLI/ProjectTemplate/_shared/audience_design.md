## When the brief names audiences: one piece for each, the same figures

A brief with `Audience A: <place>, aged …` lines asks for **an infographic per audience**: the same question, the same
sourced figures, the same layout and the same palette, each written in that audience's language and its locale's
conventions. The audiences change how it reads, never what it says or how it is designed. Without audience lines there
is one piece, as above.

**An audience here is a place and a language, not a measured taste.** Nothing in this workflow measures what an
audience likes, so nothing on the page or in `direction.md` claims to know it. The age band is context for the words
(how plain, how formal), never evidence for a design choice.

**1. The locale's conventions, not a region's supposed tastes.** Write every figure as the audience's locale writes it,
`Locale.number(value, { locale: 'ja-JP' })` and its date forms, and follow the language rules below when an audience line
names one, right to left included. **No style is assumed for a place**: "this region likes dense layouts" or "this
culture reads red as luck" is a claim, and a claim on this page has a source. If a cultural convention matters to the
piece (a colour's meaning, a reading order), commission it with `Research` for that audience and cite it, or leave it
out.

**2. Deliver one piece per audience**: `artifacts/final_a.svg`/`.webp` for audience A, `final_b` for B, and so on,
and run the checks on each: `VerifyFigures` (on the SVG, where there is one) and `Verify.image` on every piece, since
each has its own type and numerals.

A second audience is a variant of the first piece, not a second design: same grid, same forms, same figures, same
palette; the numerals and the language move. That keeps a set inside the run's time.
