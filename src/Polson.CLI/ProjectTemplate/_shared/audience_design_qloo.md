## When the brief names audiences: one piece for each, the same figures

A brief with `Audience A: <place>, aged …` lines asks for **an infographic per audience**: the same question, the same
sourced figures and the same layout, each set in that audience's colour and its locale's conventions. The audiences
change how it looks and reads, never what it says. Without audience lines there is one piece, as above.

**1. Each audience's taste, measured.** `Taste({ op: 'profile', audience: '{"location":"Lagos","age":"25_to_29"}',
label: 'audience A' })` returns, per domain, the titles distinctive to that audience. A location is required; check the
answer's `locality` is the place you meant. With an age band, distinctive is against the place (what this group favours
that the city does not); with a place alone, `against` is `'world'` and it is what the city favours over the world's
popular list. Say which in `direction.md`. Ask once per audience and reuse the answer by id: requests are metered.

**2. The field: what that audience already looks at.** In one script per audience,
`await Taste.images(profile, { type: 'film', take: 16 })` fetches the posters of its distinctive films, and
`Taste.colourField(...)` maps their colours by hue, chroma and lightness. Draw `artifacts/01_field_a.png`, the wheel
with a card per film (name, rank, the colours its poster measured) and **no poster on it**. Then write one sentence
per audience in `direction.md`: where the field crowds, where it is empty, and whether this piece's accent **belongs**
(a colour this audience already looks at) or **stands apart** (an open sector), citing the profile's id and the field's
`summary`. Never draw, trace or imitate a fetched poster: it is research, not material.

The field is the colour of what an audience **loves to watch**, a proxy for its visual taste. It is not "the colours
popular in Lagos", and nothing on the page or in `direction.md` says it is.

**3. The palette from the field.** The structure (ground, ink, data colours, contrast) is the design language's; the
accent and the data hue for the key series are chosen against the field as `direction.md` said. Contrast rules hold
regardless: a field that crowds dark reds does not excuse a chart that fails WCAG.

**4. The locale's conventions, not a region's supposed tastes.** Write every figure as the audience's locale writes it,
`Locale.number(value, { locale: 'ja-JP' })` and its date forms, and follow the language rules below when an audience line
names one, right to left included. **No style is assumed for a place**: "this region likes dense layouts" or "this
culture reads red as luck" is a claim, and a claim on this page has a source. If a cultural convention matters to the
piece (a colour's meaning, a reading order), commission it with `Research` for that audience and cite it, or leave it
out.

**5. Deliver one piece per audience**: `artifacts/final_a.svg`/`.webp` for audience A, `final_b` for B, and so on,
and run the checks on each: `VerifyFigures` (on the SVG, where there is one) and `Verify.image` on every piece, since
each has its own type and numerals. A figure drawn from a Qloo answer is tagged with its id and path, as
`Taste.get('t3')` shows it (`'t3:profile.domains.0.distinctive.2.name'`), and `VerifyFigures` checks it as it checks
research. At the end, `const r = Taste.checkReferences(); Stage.check('every Qloo reference was returned by its
query', r.passed, r.summary);`.

**6. Measure that each piece stands where `direction.md` said.** Read the final's leading accent with `bitmap.palette`,
place it with `Taste.oklch(color).sector`, and `Stage.check` per audience that it is in the field's crowd if it was to
belong, in an open sector or apart in lightness if it was to stand apart.

A second audience is a variant of the first piece, not a second design: same grid, same forms, same figures; the
palette, the numerals and the language move. That keeps a set inside the run's time.
