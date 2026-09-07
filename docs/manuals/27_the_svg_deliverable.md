# Studio Manual 27: The SVG Deliverable — Viewport, Styling and Reuse

> **Credits & Theoretical Foundation**: Distilled from Rob Larsen, *Mastering SVG* (Packt Publishing, 2018) — ch. 2 *viewBox and viewport in SVG* for the coordinate model, ch. 5 *Styles in standalone SVG images* for the self-containment rule, and ch. 5 *Using SVG-specific CSS properties* for the specificity fact that decides whether a kept stylesheet is editable. Ledger entry 2026-09-06; that row records the book's retrieval-system clause, so what is served here is our own prose. **Every behavioural claim below was measured against this engine rather than taken from the book** — the book describes browsers, and where the two disagree the measurement wins and says so. Signatures at `polson://sdk/core/Snap`.
> **Purpose**: Manual 14 builds the vector document. This one is about the **file that leaves the building** — how its coordinate system scales, what it may and may not reference, which CSS actually survives to the render, and how to make one motif appear forty times without forty copies of it.

---

## 1. What This Is For

> **Implemented by**: nothing. This section decides which of the rest apply.

`polson://manual/14` covers *construction*: the tree, transforms, paint servers, filters, measurement. It is what you read while drawing.

This is what you read when the drawing is done and something has to **open the file somewhere else** — a browser, Illustrator, a print workflow, a README, a page you do not control. The two failure modes it exists to prevent are both silent:

- The file renders perfectly in the peek and **loses half its content** when loaded as an `<img>`.
- A stylesheet is applied, the count comes back non-zero, and the property **does nothing**, because this renderer never honoured it.

Neither shows up as an error. Both show up as a deliverable that was wrong all along.

---

## 2. The Viewport and the viewBox

> **Implemented by**: `Snap(width, height)`, `paper.attr({ viewBox })`, `paper.width`, `paper.height`.

Two separate things, and conflating them is why a scaled SVG comes out wrong.

- **The viewport** is `width` and `height`: how big the graphic is where it lands.
- **The `viewBox`** is `min-x min-y width height`: the region of the drawing's own coordinate space that gets mapped onto that viewport.

`Snap(w, h)` sets `viewBox` to `0 0 w h` at construction, so one user unit is one pixel and the distinction never comes up. It comes up the moment you change either one.

**Measured on this engine**, not inferred from the book: a `0 0 50 50` viewBox on a 100 × 100 paper renders the drawing at **2× zoom**, and `paper.attr('viewBox')` reads the value back.

```javascript
const paper = Snap(400, 400);
paper.circle(50, 50, 25).attr({ fill: '#1f6f8b' });
paper.attr({ viewBox: '0 0 100 100' });   // 100 units across 400px: everything drawn 4x larger
```

That is the whole mechanism behind "vector scales without loss": the geometry is untouched and the grid over it changes, which is the same argument Manual 14 §1 makes about the RIP, one level up.

Three things it buys, all of which are otherwise fiddly arithmetic:

| To do this | Change | Because |
| :--- | :--- | :--- |
| Deliver one drawing at any size | `width`/`height` only, leave `viewBox` | The coordinates inside never move |
| Crop to a region | `viewBox` to that region's box | `min-x`/`min-y` move the origin |
| Zoom in on detail | `viewBox` width/height smaller | Fewer user units across the same viewport |

> [!IMPORTANT]
> **A `viewBox` whose aspect ratio differs from the viewport's leaves the difference to `preserveAspectRatio`**, which round-trips into the markup here but is honoured by whatever opens the file rather than by anything in this SDK. Setting it is worth doing for a deliverable and is not worth *testing* against the peek — the peek renders at the size you asked `toImageBytes` for, so it will not show you the letterboxing a browser would apply.

> [!TIP]
> **Crop a document to its own content by measuring it first.** Real Snap.svg's `getBBox()` returns a `vb` field — the box preformatted as a `viewBox` string — and `SnapBBox` does not, so build it from the four numbers you do get:
>
> ```javascript
> const b = paper.getBBox();
> const pad = 8;
> paper.attr({ viewBox: `${b.x - pad} ${b.y - pad} ${b.width + pad * 2} ${b.height + pad * 2}` });
> ```
>
> This is how a mark drawn wherever it landed becomes a file that is *all mark and no margin*, which is what an icon has to be before anyone can place it.

> [!WARNING]
> **`getBBox()` cannot see a `<use>`, and cropping to it will throw your reused shapes away.** Measured: a rectangle at the origin with a `use` of it translated to x = 200 still reports a box **40 wide**; replace the `use` with a `clone()` at x = 300 and the box becomes **340**. The `use` renders perfectly either way — it simply does not measure.
>
> So §2's crop and §6's reuse are individually correct and combine into a silent bug: a five-star mark built from one star and four `use` elements crops to **the first star**, and the delivered file shows one star of five. Nothing reports it, because both calls did exactly what they promise.
>
> When a document contains `use` elements, compute the extent yourself from the motif's box and the offsets you placed it at. Pinned by `SnapTransformTests.TestGetBBoxIgnoresUseButCountsAClone`.

---

## 3. Self-Containment

> **Implemented by**: `paper.image(object, ...)`, `paper.style(css)`, `outSvg`.

**An SVG loaded as `<img src>` or as a CSS `background-image` fetches nothing.** No external stylesheet, no external image, no font file. Larsen states the rule for stylesheets in ch. 5 — an image that will be used that way *needs to be entirely self-contained* — and `polson://sdk/core/Snap` states the same rule for `<image>` hrefs. They are one rule, and it is the single most important property of a vector deliverable.

The reason it bites is that the file **still works while you are testing it**. Opened directly, or embedded through `<object>`, the SVG is a *document* and its external references resolve. Loaded through `<img>`, it is an *image*, and images fetch nothing. So the same file is correct in one context and half-empty in the other, with no error in either.

Three consequences, each with a spelling that is right and a spelling that is natural:

| | Natural, and quiet | Right |
| :--- | :--- | :--- |
| A raster picture | `paper.image('artifacts/portrait.png', …)` | `paper.image(bitmapOrPhoto, …)` — inlined as a data URI |
| Styling | An external sheet the file links to | `paper.style(css)` — the block is kept **in** the document |
| A typeface | Assuming the opener has it | A fallback list, and `Skia.Font.has(...)` to know what you got |

> [!IMPORTANT]
> **Fonts are the one that cannot be fixed by inlining.** `paper.style('.h1 { font-family: Didot }')` names a face; it does not carry it. On a machine without Didot the text reflows into a substitute, silently, and a wordmark set in a face nobody else has is a deliverable that only looks right on the machine that made it. Either give a real fallback stack, or convert the type to paths so the shapes travel with the file — the second is what makes a logo a logo.

---

## 4. What a Stylesheet Can Actually Say

> **Implemented by**: `paper.style(css)`, `element.attr(...)`. Pinned by `CssSupportMatrixTests`.

`paper.style(...)` returning a non-zero count means the rules **matched elements**. It does not mean the renderer honoured them. Those are different questions and only the first one reports itself.

> [!IMPORTANT]
> **The count is rule-to-element *applications*, not distinct elements styled.** One element matched by two rules counts **2**, exactly as two elements matched by one rule do. So a five-star mark styled by three rules returns **10**, and the obvious sanity check — `styled === 5` — fails while every star is correctly styled. Use the count to tell a typo from an empty sheet, which is what it is for; do not use it as an element census. Pinned by `SnapStylesheetTests.TestTheCountIsRuleApplicationsNotDistinctElements`.

The table below is measured, not asserted: every row renders the same shape with and without the property and compares the two frames, because an unsupported property still serialises perfectly into the file. A row that changes the frame is supported; a row that does not is a renderer limit.

**Honoured — on both routes:**

`fill` · `fill-opacity` · `fill` as `url(#paintServer)` · `opacity` · `stroke` · `stroke-width` · `stroke-opacity` · `stroke-dasharray` · `stroke-dashoffset` · `stroke-linecap` · `stroke-linejoin` · `font-family` · `font-weight` · `font-size` · `text-anchor` · `text-decoration` · `alignment-baseline` · `paint-order` · `marker-end` · `clip-path` (both `url(#id)` and `polygon(...)`) · `mask` · `filter` · `visibility` · `display` · `transform`

**Ignored by *this* renderer — the declaration reaches the file and changes nothing you can see:**

`font-style` · `letter-spacing` *(but see §4a — `paper.trackedText(...)` does the job)* · `dominant-baseline` · `baseline-shift` · `fill-rule` · `mix-blend-mode` · `vector-effect` · `stroke-miterlimit` · any `em`-valued `font-size`

> [!WARNING]
> **These are renderer limits, not invalid CSS, and the difference is a trap rather than a reassurance.** Every one of them is valid SVG that a browser opening the delivered file would very likely honour. So the tempting move — set the property anyway, since the *file* will be right — is exactly the move to avoid: **the peek would show you one thing and every other viewer another**, and you would have no way to verify the deliverable you actually shipped. This studio's whole method is checking the render.
>
> Worse, the two can compound. Set `letter-spacing` *and* position the glyphs to compensate for a peek that ignores it, and a browser applies both — a wordmark tracked twice. Prefer the construction that renders the same everywhere, which for tracking is §4a.

> [!IMPORTANT]
> **The two routes agree about *which properties* are honoured; they differ about *value syntax*.** Everything above behaves the same whether it arrives through `attr(...)` or through a stylesheet — the sheet is not a weaker channel. What is stylesheet-specific is how a value is written: a `url(...)` reference has to be salvaged from the source text because the CSS parser drops it, and a unit suffix was once silently discarded. Both are pinned by tests. Write `px`, never `em`.

### 4a. The three that change a deliverable

Most of the ignored list is a detail. Three are not.

**`letter-spacing` is canvas-only — use `paper.trackedText(...)` instead.** `ctx.letterSpacing` works; the SVG property serialises perfectly and changes not one pixel. So a wordmark tracked with the attribute is correct on canvas and **untracked as vector**, on the surface a wordmark is most likely to ship on, with no error anywhere.

The remedy converts the tracking into the one thing the renderer does honour — positions — and takes the same value `ctx.letterSpacing` takes, so the em fraction from `LogoType.computeWordmarkTracking(...)` applies unchanged:

```javascript
const paper = Snap(800, 160);
const style = { 'font-family': 'Arial', 'font-size': 48, fill: '#15151a', 'text-anchor': 'middle' };

// The attribute that looks right and does nothing:
//   paper.text(400, 90, 'AURELIA').attr({ ...style, 'letter-spacing': '0.18em' });
paper.trackedText(400, 90, 'AURELIA', LogoType.computeWordmarkTracking(48, true) + 'em', style);
paper;
```

**The two surfaces measure a tracked run to the same width**, which is what lets a lockup designed on canvas be delivered as vector — asserted equal rather than merely non-zero by `VectorTrackedTextTests`. `VectorLogo.measureTrackedText(text, tracking, attrs)` reports that width without drawing, for laying a lockup out before committing it.

Two consequences. **Kerning is lost** — as it is on canvas and in every tool that tracks type, since the pairs are no longer adjacent to kern — so leave tracking at `0` for body text and reach for this for display type. And the run reaches the deliverable as **one `<text>` element per glyph** rather than one string: the price of a renderer that honours positions and not spacing. A designer opening the file gets a row of text objects, which is what "convert tracking to positions" means in any tool.

> [!NOTE]
> Tspans would have been the tidier output — one text object with positioned spans — and they do not work here: this renderer's serialiser **drops child nodes of a `<text>` entirely**, so a span-per-glyph run is absent from both the markup and the render while reporting success. Separate `<text>` elements are what survives.

**`dominant-baseline` does nothing and `alignment-baseline` works.** This is the trap, because it is the wrong way round from every reference: `dominant-baseline` is the property that shifts a `text` element's own baseline and is the one usually recommended, and Larsen introduces the two together in one breath. Here the recommended spelling is the dead one. Use `alignment-baseline`, or place the baseline arithmetically — remember SVG's `y` **is** the baseline, so `getBBox()` already tells you where the ink sits relative to it.

**`font-style: italic` does nothing.** An italic is a different *face*, not a slanted roman, so ask for it by family — `font-family: 'Georgia Italic'` where the machine has it, checked with `Skia.Font.has(...)` — rather than by style.

> [!NOTE]
> **Half of the pinned matrix asserts that a property is ignored, which is a strange thing to want until you consider the alternative.** These are renderer limits nobody wrote down, so an author reaching for `font-style: italic` got silence and no way to tell a typo from an unsupported feature. Pinned, a renderer upgrade that starts honouring one **fails the suite** and gets documented, instead of quietly changing what every existing sheet means. If a row here contradicts what you observe, run the test — it, not this table, is the authority.

---

## 5. Why the Kept Stylesheet Is Editable

> **Implemented by**: `paper.style(css)`.

`paper.style` does two things: it writes each declaration onto the matching elements **as presentation attributes**, so the render is right, and it keeps the `<style>` block in the document, so the deliverable carries one rule instead of ninety baked attributes. Manual 14 §6b explains why both halves are necessary.

What neither manual said is why the second half *works*. It rests on a specificity rule (Larsen, ch. 5): **CSS overrides presentation attributes, but does not override `style` attributes.**

So when a designer opens the file and edits `.mark { fill: … }` in the kept block, that rule beats the `fill="#1f6f8b"` sitting on all ninety elements, and the edit takes effect everywhere. Had `paper.style` written `style="fill:#1f6f8b"` instead — the spelling that looks more like CSS and is the obvious thing to reach for — the inline style would **beat** the block, and editing the stylesheet would appear to do nothing at all. The whole point of keeping the sheet would be lost, and lost silently.

> [!TIP]
> **You do not need `<![CDATA[ ]]>`, and the book will tell you that you do.** Hand-authored standalone SVG conventionally wraps stylesheet contents in a CDATA section so that `>` and `&` are not read as markup. **Measured here**: `paper.style` XML-escapes the sheet instead — a `>` in a comment or a selector comes back as `&gt;` — and the result re-parses through `Snap.parse` cleanly. Escaping and CDATA are both valid XML and this one is automatic, so a stylesheet containing hostile characters is already safe. Do not hand-build a `<style>` element to add CDATA yourself.

---

## 6. Reuse — One Motif, Many Places

> **Implemented by**: `paper.use(element)`, `element.use(target)`, `paper.el('symbol')`, `paper.defs`.

A `<use>` element draws another element again, elsewhere, without copying its geometry. **Measured on this engine**: a referenced circle and its `<use>` both render, and the copy honours a transform of its own.

```javascript
const paper = Snap(400, 120);
const star = paper.path('M20,0 L26,14 L40,14 L29,22 L34,36 L20,28 L6,36 L11,22 L0,14 L14,14 Z');
star.id = 'star';                                  // no id, nothing to reference
star.attr({ fill: '#e5a93c' });

for (let i = 1; i < 8; i++) {
    paper.use(star).attr({ transform: `t${i * 46},0` });
}
```

This is the difference between a file that states a shape once and a file that states it eight times. On a repeated motif — an icon set, a pattern of marks, a pictogram row, a chart's repeated symbol — it is the single largest thing you can do to the size of the deliverable, and it is the *right* thing structurally: the eight stars are one star, so recolouring the original recolours all of them.

**`clone()` is the opposite tool and both have their place.** A clone is independent geometry that can then differ; a `use` is one shape shown repeatedly. Reach for `clone()` when the copies need different *geometry*, `use` when they share it.

### 6a. Making instances differ — the rule that decides the whole idiom

Instances **can** differ in paint, but only under one condition, and getting it wrong is silent.

**A `<use>` paints only what the referenced element leaves unset.** The reference clones the source *including its presentation attributes*, so a `fill` on the source wins and a `fill` on the instance is inert. Measured: a gold source with an instance asking for `#e6e8ec` renders **gold twice**. Leave the source's fill unset and the two instances render gold and grey as asked.

That has an awkward consequence — an element with no `fill` renders in SVG's default **black** — and the consequence is exactly why the `defs` idiom exists:

```javascript
const paper = Snap(120, 60);

// The motif is parked where it does not draw, and sets no paint of its own.
const star = paper.defs.path('M22,0 L28,15 L44,15 L32,25 L37,41 L22,32 L7,41 L12,25 L0,15 L16,15 Z');
star.id = 'star';

paper.use(star).attr({ fill: '#e5a93c' });                          // each instance paints itself
paper.use(star).attr({ transform: 't54,0', fill: '#e6e8ec' });
paper;
```

Measured: an element inside `paper.defs` does not draw at its definition site, and each instance carries its own fill. Both halves pinned by `SnapPaintServerTests` — `TestAUseRecoloursOnlyWhatTheSourceLeavesUnset` and `TestAnElementInDefsDoesNotDraw`.

**So the choice is made once, at definition time.** A motif whose instances will all look alike can be drawn normally and referenced; a motif whose instances must differ has to be parked in `defs` with its paint left off. Deciding late means rebuilding, because moving the fill off the source changes where the first instance comes from.

> [!IMPORTANT]
> **An element has no `id` until you assign one**, so `paper.use(el)` on a freshly drawn shape has nothing to point at. Assign `el.id` first — the same trap Manual 14 §10 records for `select('#name')`, failing the same quiet way.
>
> **A `<symbol>` is a definition that does not draw itself**, which is what you want for an icon library: define once in `defs`, `use` it where needed, and nothing appears at the definition site. A plain element referenced by `use` draws in *both* places — fine for the star row above, wrong for a library.

> [!WARNING]
> **A `<use>` draws but does not measure.** `getBBox()` is blind to it, so anything computed from the document's extent — a crop, a clear-space guide, a lockup box, a `Layout.bounds` over measured children — sees only the originals. This is the one real cost of choosing `use` over `clone()`, and it is invisible until a crop silently discards four fifths of the drawing. See the warning in §2.

---

## 7. What the Deliverable Cannot Carry

An honest list, because each of these is discovered late otherwise.

- **No `<title>` or `<desc>`, so no accessible SVG.** `paper.el('title')` **throws** — neither element is in the factory. An SVG carrying no accessible name is the accessibility equivalent of an unlabelled button. This is a toolkit gap, not a technique to work around: there is currently no way to emit one, and a brief that asks for an accessible graphic cannot be fully met on this surface today.
- **No external anything** — see §3. Less a limitation than the format's contract.
- **No optimisation pass.** SVGO and svgcleaner are external Node and Rust binaries the sandbox cannot run. This matters less than it sounds: their premise is that *editor-generated* SVG carries authoring cruft — Inkscape metadata, editor namespaces — and markup we emit ourselves has none of that. The two levers we do have are real: `<use>` instead of repetition (§6), and the RDP `tolerance` on brush strokes, which took a preset sheet from 17.4 KB of path data to under 3 KB with no visible change.
- **No text wrapping, no pixel measurement, no drawing media, no SkSL** — all canvas-side. Manual 14 §9 has the full list and the reasoning.

---

## 8. Traps

| What happens | Why | What to do |
| :--- | :--- | :--- |
| The file is right in a browser tab and half-empty in an `<img>` | An `<img>` is an *image*: it fetches no external resource | Inline everything — pass objects to `paper.image`, keep the sheet in the document (§3) |
| `paper.style(...)` returns a healthy count and the property does nothing | The count means *matched*, not *honoured* | Check §4's list; the two questions are different |
| The style count is higher than the number of elements | It counts rule-to-element applications; three rules over five elements can return 10 | Do not compare it to an element count (§4) |
| Cropping to `getBBox()` leaves one copy of a repeated motif | `getBBox()` is blind to `<use>` | Compute the extent from the motif's box and your offsets (§2) |
| A wordmark's tracking vanishes when built as vector | The `letter-spacing` attribute is inert | `paper.trackedText(x, y, text, tracking, attrs)` (§4a) |
| `dominant-baseline` does nothing | It is the dead one of the pair, contrary to every reference | Use `alignment-baseline` (§4a) |
| A stylesheet edit in Illustrator has no effect | The declarations were written as `style` attributes, which beat CSS | Nothing to do here — `paper.style` writes presentation attributes for exactly this reason (§5) |
| `font-size: 2.5em` renders at the wrong size | `em` survives parsing and is dropped by the renderer | Write `px` |
| `paper.use(el)` references nothing | The element has no `id` yet | `el.id = 'name'` before the `use` |
| A `<symbol>`'s contents never appear | That is what a symbol is | `use` it; or use a plain element if it should draw where defined |
| Scaling the viewport distorts the drawing | `viewBox` was changed alongside `width`/`height` | Change one. The viewport sizes it; the viewBox frames it (§2) |
| The peek looks right and the delivered file is letterboxed | `preserveAspectRatio` is applied by the opener, not by the peek | Reason from the two aspect ratios; the peek cannot show it |

---

## 9. Symbol → SDK Map

| What you want | The call | Notes |
| :--- | :--- | :--- |
| Size the graphic | `Snap(w, h)`, `paper.width`, `paper.height` | Both settable |
| Frame or crop the content | `paper.attr({ viewBox: 'x y w h' })` | Set to `0 0 w h` at construction |
| Crop to the drawing itself | `paper.getBBox()` → build the string | No `vb` field here, unlike real Snap |
| Inline a picture | `paper.image(bitmap \| photo \| canvas, …)` | Pass the object, never a path |
| Keep styling in the file | `paper.style(css)` | Presentation attributes **and** a kept `<style>` block |
| Know whether a property will render | §4, and `CssSupportMatrixTests` | Matched ≠ honoured |
| Draw one shape many times | `paper.use(element)` | Assign `element.id` first |
| Define without drawing | `paper.el('symbol')` inside `paper.defs` | For icon libraries |
| Independent copies | `element.clone()` | Detached; give it a new id |
| Write the file | `outSvg: 'artifacts/mark.svg'` | Needs a paper; a canvas has no markup |
| Read one back | `Snap.load(path)` | The reopen half of `outSvg` |

---

## 10. Constructing It: A Deliverable That Survives Leaving

Viewport first, content second, stylesheet last, crop at the end.

```javascript
// A five-star rating mark: one star, reused; one rule, editable; cropped to its own ink.
const paper = Snap(480, 120);
const STEP = 54;                       // kept as a name because the crop below needs it too

// The motif is parked in <defs> and sets no paint of its own — §6a — because the fifth instance
// has to differ. Defined out here it would draw a sixth star, and its fill would win over every use.
const star = paper.defs.path('M22,0 L28,15 L44,15 L32,25 L37,41 L22,32 L7,41 L12,25 L0,15 L16,15 Z');
star.id = 'star';

// Five instances, all sharing one geometry. The fifth is dimmed, for a 4-of-5 rating.
for (let i = 0; i < 5; i++) {
    paper.use(star)
         .attr({ transform: `t${i * STEP},0`, 'class': i === 4 ? 'star empty' : 'star filled' });
}

// Last, so it resolves against the finished tree. One rule a designer can edit in the file.
const styled = paper.style(`
    .star   { stroke: #8a6d1f; stroke-width: 1.5; }
    .filled { fill: #e5a93c; }
    .empty  { fill: #e6e8ec; }
`);
// 3 rules over 5 elements: .star matches all 5, .filled 4, .empty 1 — 10 applications, not 5 elements.
Stage.check('all three rules applied', styled === 10, `${styled} rule applications`);

// Crop to the ink. getBBox() cannot see the four <use> copies, so measure the motif and add the
// offsets deliberately — cropping to paper.getBBox() here would deliver one star of five.
const b = star.getBBox();
const pad = 6;
const spanned = STEP * 4 + b.width;
paper.attr({ viewBox: `${b.x - pad} ${b.y - pad} ${spanned + pad * 2} ${b.height + pad * 2}` });

paper;   // render it, and pass outSvg to write the file
```

Five things make it a deliverable rather than a picture: the motif is stated once, the styling is one editable rule that will still win when edited, the crop is measured rather than guessed, the crop accounts for geometry the measurement cannot see, and nothing in the file points anywhere outside it.
