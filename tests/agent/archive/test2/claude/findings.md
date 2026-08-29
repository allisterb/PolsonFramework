# Findings — Polson MCP server, vector-first logo brief

Working log. Entries written as they happened, newest appended at the bottom of each section.

Client: **Sailboat Tours** (romantic sailboat cruises). Role: AI brand designer working only
through the `polson` MCP server's tools and `polson://` resources.

---

## 0. Harness verification

All three checks came out as required:

| check | result |
|---|---|
| `../../../../src/Polson.MCPServer/JsDrawingEngine.cs` | **denied** by permission settings |
| `../gemini/GEMINI.md` | **denied** by permission settings |
| `.mcp.json` in this folder | **read successfully** |

Harness intact — no view of the implementation and no view of another agent's brief.

One note on the second check: my first attempt used the path `tests/agent/gemini/GEMINI.md`
and came back **"File does not exist"**, not "denied". That is the wrong deny-message for the
wrong path — the real sibling harness is `tests/agent/test2/gemini/`, and reading *that* path
correctly returned "denied by your permission settings". Worth knowing that a mistyped path
inside a denied tree can return a not-found rather than a denial, because an agent checking its
own sandbox could read "does not exist" as "nothing there to protect" and move on satisfied.
No tool refusal blocked anything the published API told me to do.

---

## 1. What broke

### 1.1 `Assets.classify` refuses a material and allows the object — inverted for this brief

Probe 1, first script of the session:

| descriptor | verdict | allowed |
|---|---|---|
| `weathered teak boat decking` | **Form** | **false** |
| `a sailboat` | Ambiguous | **true** |
| `sun-bleached canvas sailcloth weave` | Substance | true |
| `a romantic sunset over the sea` | Ambiguous | true |

The refusal reason is: *"'boat' names a thing with an outline. Draw the form with the drawing
toolkit and requisition its surface instead — e.g. the planking, not the ship."*

But `weathered teak boat decking` **is** the planking, not the ship — it is a surface descriptor
that happens to contain the substring `boat`. Meanwhile the literal object `a sailboat` — the exact
example the brief and `polson://sdk/core/Assets` both give as the thing that gets refused — sails
straight through as `allowed=true`.

So the guard is keying on a token blocklist rather than on the grammatical head of the phrase, and
for a marine client it fires on precisely the material vocabulary you need (`boat decking`,
`boat hull planking`) while missing the object it exists to catch. Two distinct bugs:

- **False positive** on a genuine material whose modifier contains an object noun.
- **False negative** on a bare object noun phrase (`a sailboat`), which the documentation
  explicitly promises will be refused. `polson://sdk/core/Assets` says *"Ask for an object rather
  than a material and you will be refused — 'a sailboat' is refused"*. It is not.

Impact on my work: the pre-check is not trustworthy as a gate, so I cannot use `classify` to decide
whether a descriptor is worth spending on. Workaround is to phrase around the blocklist
(`weathered teak decking` without `boat`), which is exactly the wrong incentive — it trains the
agent to launder object requests past the guard.

### 1.2 `bitmap.getPixel` returns `#AARRGGBB`, documented as `#RRGGBBAA`

`polson://sdk/core/Skia` says: *"`bitmap.getPixel(x, y)` → Returns hex color `#RRGGBBAA`"*, and
sells it as **the** way to verify a render — *"Looking at an image tells you a layer is 'too
bright'; reading back a pixel tells you whether it is the colour you asked for, the alpha you asked
for, or drawn at all."*

Filling opaque black and reading a pixel inside the fill returns `#ff000000`. Under the documented
`#RRGGBBAA` that decodes as **fully transparent red**. Under `#AARRGGBB` it decodes as **opaque
black**, which is what was actually drawn. Confirmed separately against a known non-grey colour
(§1.2a below).

This is the worst kind of doc/code drift: the call is advertised as the ground-truth verification
primitive, and an agent that trusts the documented channel order will read every alpha as a red
channel and conclude a correctly-drawn layer is missing.

### 1.3 `polson://sdk/index` under-reports the `Logo` area

The map lists *"Methods (17)"* for `Logo` and enumerates them — the list omits
`Logo.generateFaviconScaleTest`, `Logo.generateMonochromeTest` and
`Logo.generateBrandPresentationSheet`, which are the three calls the brief specifically requires and
which `polson://sdk/core/Logo` (and `polson://sdk/symbols`, authoritative) do carry. It also omits
`Logo.createGoldenSpiralSvgPath`, which the area doc lists.

The index bills itself as *"the complete inventory of callable objects"* and *"replaces reading the
reference documents whole"*. Taking it at its word would have led me to conclude the scale-test and
brand-sheet calls did not exist — the single most expensive kind of wrong answer. I only caught it
because Manual 12 names them.

### 1.4 `ctx.fill('evenodd')` is undocumented in the SDK reference

Manual 12 §3 (Stage 6a) builds its example mark with `c.fill('evenodd')` and explains why the
fill rule matters — a counter cut as real geometry survives 16px honestly, a counter faked with a
background-coloured shape on top does not. That is load-bearing advice for this brief.

`polson://sdk/core/Canvas2D` documents only `ctx.fill(path?: CanvasPath)`. No fill-rule overload is
mentioned anywhere in the SDK reference.

It **does** work — verified: centre pixel of a disc-with-counter came back transparent, so the hole
is genuinely cut. But an agent reading only the SDK reference would not know the rule exists, and
would reach for the fake-counter approach the manual warns against.

### 1.5 `SnapMatrix` cannot actually place anything — three of six documented spellings silently no-op

This was the single most expensive defect of the session. I built a construction plate with
`g.attr({ transform: 'matrix(4.6,0,0,4.6,50,60)' })` and the serialised SVG came back with
`transform="matrix(1, 0, 0, 1, 0, 0)"` — identity. No error, no warning. I only caught it because
`outSvg` let me read the markup; the raster render just showed a tiny mark in the corner, which I
could easily have mistaken for a sizing mistake of my own.

I then tested every documented spelling:

| call | result |
|---|---|
| `el.attr({transform: 'matrix(2,0,0,2,30,40)'})` | **identity — silently dropped** |
| `el.attr('transform', 'matrix(2,0,0,2,30,40)')` | **identity — silently dropped** |
| `el.transform('matrix(2,0,0,2,30,40)')` | **identity — silently dropped** |
| `el.transform(Snap.matrix(2,0,0,2,30,40))` | **identity — silently dropped** |
| `el.transform('t30,40s2')` | works → `matrix(2,0,0,2,20,30)` |
| `el.attr({transform: 't30,40s2'})` | works |
| `el.transform('s2,2,0,0t30,40')` | works → `matrix(2,0,0,2,60,80)` |

So **only Snap's shorthand grammar is parsed**. Standard SVG `matrix(a,b,c,d,e,f)` — which is what
`SnapMatrix.toTransformString()` itself produces, and what every SVG reference in the world tells
you to write — is discarded to identity.

The `SnapMatrix` overload failing is the serious half. `polson://sdk/core/Snap` documents
`element.transform(transformStringOrMatrix: string | SnapMatrix)` and devotes a whole section to
`SnapMatrix` — `translate`, `scale`, `rotate`, `skewX/Y`, `mult`, `multLeft`, `invert`,
`toTransformString`. You can build a matrix with all of it and then **there is no way to apply it
to an element**. The entire `SnapMatrix` surface is decorative unless you hand-convert it back into
Snap shorthand, which the shorthand cannot even express for a general affine.

Related, and it cost me a second detour: **`getBBox()` ignores the transform.** After
`g.transform('t30,40s2')` on a group containing `circle(10,10,5)`, `getBBox()` returned
`x=5 y=5 w=10 h=10` — the untransformed box. Snap.svg's own `getBBox()` is post-transform. This
matters directly for `Logo.drawClearSpaceGuide` / `paper.clearSpaceGuide`, whose whole job is to
fence a mark's measured bounds.

**What I did instead:** abandoned transforms on the vector side entirely and emitted the mark's
path data parametrically — a `markPath(ox, oy, scale)` that writes absolute coordinates. That is
robust, but it is hand-rolling placement, and it means the SVG I ship has no reusable
`<g transform>` structure. On the raster side `ctx.translate` / `ctx.scale` work correctly, so
Canvas2D has a working transform stack and the vector side effectively does not.

### 1.6 `paper.polarGrid` / `paper.goldenCircles` hard-code their own colours

I called `g.attr({ stroke: '#9fb6c9', fill: 'none', 'stroke-width': 0.8, opacity: 0.55 })` on the
groups returned by `paper.polarGrid(...)` and `paper.goldenCircles(...)`. The children came out
indigo `#6366F1` and amber `#F59E0B` regardless, because each child carries its own inline
`style="stroke:#6366F1;"`, and an inline `style` beats an inherited presentation attribute on the
parent. So the group `attr()` is silently ineffective.

There is no way to fix it from the outside, because — unlike their raster twins — the vector
armature helpers take **no options parameter at all**:

| raster | vector |
|---|---|
| `Logo.drawPolarGrid(ctx, cx, cy, rings, slices, {lineColor, lineWidth})` | `paper.polarGrid(cx, cy, maxRadius, ringCount, rayCount)` — no options |
| `Logo.drawIsometricGrid(ctx, w, h, cell, {lineColor, lineWidth})` | `paper.isometricGrid(w, h, spacing)` — no options |

My construction plate therefore has amber and indigo guides I did not choose and cannot change,
in a brand whose palette contains neither.

### 1.7 `Logo.computeOpticalCenter(bounds, 'triangle')` moves the mark the wrong way

`polson://sdk/core/Logo` says the call "Computes visual center of gravity ($Y \approx 42\%–48\%$)",
and Manual 10 §4C is explicit about the direction: triangular and tapering marks *feel
bottom-heavy*, so the correction is to **shift the mark upward** to land at 44–48% of container
height.

For bounds `{x:4, y:4, width:92, height:92}` (geometric centre y = 50):

- `'general'` → **y = 48.16**, i.e. 48.0% of the box. Above centre, inside the documented range. Correct.
- `'triangle'` → **y = 57.36**, i.e. **58.0%** of the box. *Below* the geometric centre, outside the
  call's own documented 42–48% range, and in the opposite direction to the correction Manual 10
  prescribes for exactly this shape class.

My mark's sails are triangular, so `'triangle'` was the natural argument to reach for. Had I used
the returned value as a placement target I would have pushed the mark **down**, making the
bottom-heaviness the call exists to fix measurably worse. I used `'general'` instead.

I cannot tell from outside whether 57.36 is a *measurement* (here is where the visual mass of a
triangle actually sits) rather than a *target* (put the mark here). Either reading is defensible in
isolation — but the doc states a single range for the return value, and one of three documented
`shapeType` values violates it. The doc and the call cannot both be right.

### 1.8 `LogoType.evaluateFontPairing` scores nonsense at 90/100

`evaluateFontPairing('nonsense', 'garbage')` returns
`{relationship: 'contrasting', score: 90, description: 'Contrasting display pairing.'}` — no error,
no "unrecognised category". The same 90 comes back for `('oldstyle','sans-serif')`,
`('script','sans-serif')` and `('slab','geometric')`, so 90 is simply the fallthrough value.

Only a couple of combinations are actually recognised: `serif`+`sans-serif` → `contrasting`/95 with
two specific recommendations, and `serif`+`serif` → `concordant`/75 with three genuinely useful
ones (contrast weight, contrast size, wide caps tracking on the secondary). Those two are worth
having. But because an unrecognised pair scores 90 — *higher* than the considered same-family
verdict of 75 — the score cannot be used to compare candidate pairings, which is the only thing a
pairing score is for. A typo in a category name reads as a strong result.

### 1.9 Only six font families resolve; every other one falls back silently

There is no call anywhere on the surface that enumerates available fonts. So I measured
`ctx.measureText` for a fixed string at 40px across 19 families, including a deliberately
nonexistent `NoSuchFontXYZ` as a control. Anything matching the control's metrics exactly has
fallen back:

| resolves distinctly | falls back to the default (width 574.65, asc 43.16, desc 10.04) |
|---|---|
| Georgia 608.96 · Garamond 564.59 · Constantia 588.95 · Arial 611.27 · Verdana 646.25 · Impact 498.85 | **Times New Roman**, **Helvetica**, **Courier New**, Baskerville, Playfair Display, Didot, Bodoni MT, Palatino Linotype, Book Antiqua, Futura, Trebuchet MS, Segoe UI, `NoSuchFontXYZ` |

Two things follow. First, `polson://sdk/core/Canvas2D` documents `ctx.font` with the examples
`"bold 24px Arial"` and `"italic 16px 'Times New Roman'"` — and **the second example's font does not
exist**, so the documented sample silently renders in something else. Second, for a toolkit whose
`LogoType` half is explicitly about logotypes and font pairing, the practical serif choice is
Georgia, Garamond or Constantia and nothing else. Every high-contrast Didone I would actually reach
for to say "romantic" — Didot, Bodoni, Playfair — is absent, and absent *silently*.

This is the one gap that changed my design rather than just costing me time: I set the wordmark in
**Garamond**, an old-style humanist serif, because it is the only romantic serif in the box. That
is a defensible choice on its own merits, but it was made by elimination, not by intent.

### 1.10 `computeWordmarkTracking` returns em fractions; the units are undocumented

`LogoType.computeWordmarkTracking(64, true, 'wordmark')` returns `-0.015`. The doc says only
"Returns optical letter-spacing". It is neither pixels nor per-mille — it is a **fraction of an em**,
so you must multiply by font size to use it (`-0.015 × 64 = -0.96px`). I established this by
checking the tagline case against Manual 11 §6, which prescribes +150‰ to +300‰ for small all-caps
taglines: `computeWordmarkTracking(13, true, 'tagline')` returns `0.22`, i.e. +220‰. That fits, so
the unit is em-fractions.

`computeOpticalKerning`, by contrast, returns **pixels** at the given size (`SA` → `-2.24` at 64px).
So two neighbouring calls in the same toolkit, both about letter-spacing, return different units and
neither says which.

One substantive quibble with the values: for a 64px display wordmark the function gives **−15‰ for
all-caps** and −35‰ for mixed case. Manual 11 §6 says large display wordmarks (≥32px) want −20‰ to
−50‰, so the all-caps figure falls outside the range the manual states, and it is looser than the
mixed-case figure — the reverse of the usual relationship, where caps need more air than lowercase,
not less.

### 1.11 `bitmap.getPixel` does not return a JavaScript string, and `===` against one is always false

Documented as returning `string`. It returns a host object:

```
typeof getPixel(...)        -> "object"
value                        -> #ff3366cc
.length                      -> undefined
=== "#ff3366cc"              -> false
 == "#ff3366cc"              -> true
.toLowerCase()               -> THREW: Property 'toLowerCase' of object is not a function
.substring(1,3)              -> THREW: Property 'substring' of object is not a function
String(px).toLowerCase()     -> "#ff3366cc"          (the workaround)
```

I hit this for real: a loop counting background pixels with
`getPixel(x,y).toLowerCase() === '#fff7f3ea'` aborted the whole script with
*"Property 'toLowerCase' of object is not a function"*.

The `===` behaviour is the dangerous part. `bitmap.getPixel` is sold in the docs as the way to
verify a render — *"reading back a pixel tells you whether it is the colour you asked for"*. The
obvious way to do that is `if (getPixel(x, y) === EXPECTED)`, and that comparison is **permanently
false** with no error. Combined with §1.2 (the channel order is `#AARRGGBB`, documented as
`#RRGGBBAA`), the single call the SDK nominates as its ground-truth verification primitive has two
independent ways to silently report a correct render as wrong.

### 1.12 `Logo.generateBrandPresentationSheet` is not usable as a deliverable

This is the call Manual 12 Stage 7 nominates for the final client artefact, so I built the brief's
presentation sheet with it first. Rendered at 1400×900 with a full options object, it has four
problems, one of them disqualifying:

**a. The app icon panel has a contrast ratio of about 1.3 : 1.** It fills a squircle with
`darkColor` and then paints the mark in `primaryColor` on top of it. Read back from the rendered
bitmap:

```
squircle ground at (80,180)  = #ff101a2b   (= darkColor  #101a2b)
mark body      at (240,300)  = #ff22304a   (= primaryColor #22304a)
```

Those two colours differ by roughly 18 levels per channel — a WCAG contrast ratio near **1.3 : 1**,
against a 3 : 1 floor for graphical objects. The mark is effectively invisible. The panel needs to
knock the mark out to `lightColor` on a dark ground (or use `primaryColor` as the ground), and it
does neither. Any palette with a dark primary — which is most identity palettes — hits this.

**b. It does not fill the canvas it is given.** On 1400×900 the entire right half below the swatch
row and the whole middle band are bare ground; roughly 45% of the board is empty. The layout is
positioned at fixed offsets rather than derived from canvas size.

**c. The clear-space block is clipped off the bottom-left**, overlapping the footer rule and running
past the canvas edge.

**d. It delivers less than Manual 12 promises.** Stage 7 lists "Primary Horizontal Lockup,
Secondary Vertical/Stacked Lockup, Color Swatches with Hex codes, X-Dimension Clear Space,
**Approved vs. Prohibited Use Cases**". Delivered: one horizontal lockup, swatches, a clipped
clear-space block. No stacked lockup and no usage panel.

I hand-built the board instead. That is the single largest piece of work the SDK advertised and did
not do.

### 1.13 Neither lockup call lets you choose a typeface

`LogoType.drawWordmarkLockup(ctx, drawMarkFn, brandName, tagline, options)` takes
`{layout, x, y, markSize, fontSize, taglineSize, primaryColor, taglineColor}`. There is **no
`fontFamily`**. Same for `generateBrandPresentationSheet`. Both render in a default sans.

For a toolkit whose stated basis is Robin Williams on contrast and Doyald Young on letterforms, the
one thing a logotype call must expose is the letterform. Rendering "Sailboat Tours" — a romantic
sailing brand — in a default grotesque is not a neutral default, it is the wrong answer, and it is
unreachable from the options object.

Second, smaller point: the SDK lockup sets the tagline at `taglineSize` with **no tracking at all**,
while Manual 11 §6 is explicit that small all-caps taglines want +150‰ to +300‰. The toolkit even
computes that number for you — `computeWordmarkTracking(13, true, 'tagline')` → `0.22` — and its own
lockup does not apply it. Side by side with my hand-set version the difference is obvious: the SDK
tagline reads as cramped small print, the tracked one reads as a tagline.

### 1.14 Things I suspected and was wrong about — `paper.svg()` is fine

Recording this because a false accusation is as costly as a missed bug. Midway through the transform
investigation I ran a sloppy regex over the serialised output and concluded `paper.svg(x,y,w,h)` was
emitting no nested viewport. A clean re-test shows it works exactly as documented:

```
<svg x="20" y="30" width="100" height="100"><circle cx="50" cy="50" r="40" .../></svg>
```

One genuine caveat: the nested `<svg>` carries **no `viewBox`**, so it is a translating and clipping
viewport, not a scaling one. It could not have substituted for the broken transforms in §1.5 — but
that is a documentation nuance, not a defect. `polson://sdk/core/VectorLogo` describes it as having
"its own coordinate space", which is true for origin and clip but not for scale.

---

## 2. What I could not find

### 2.1 Nothing in the manuals encodes emotional tone

The brief's operative word is *romantic*, and the harness told me to search the manuals for what
encodes it rather than reaching for a heart. I searched `manual` scope for
*"what form, palette and typography encode romance, warmth and intimacy in a brand identity"* and
for *"color psychology emotional meaning of hues warm colors"*. Both returned
`confidence: "related"` — i.e. nearest passages, confirming nothing.

The best hits were Manual 12 Stage 5 (*"1 Dominant Brand Color (emotional anchor)"* — names the slot
without saying what fills it), Manual 12 Stage 1 (*"Color & Style Preferences: Warm vs. cool"* — the
same), and Manual 07 §3 on colour *temperature*, which is a lighting-physics rule (warm key ⇒ cool
shadow), not a semantics rule.

Manual 12's own stage diagram literally labels Stage 5 **"Color psychology, Robin Williams contrast,
Doyald Young kerning"** — but the Stage 5 body delivers the Williams and Young halves and nothing at
all on colour psychology. The corpus advertises the topic in its own table of contents and does not
contain it.

Not fatal — Stage 2 and 4 are explicitly flagged as judgement with no API surface, and tone is
reasonably in that category. But the *mapping from adjective to geometry* is the part of this brief
the knowledge base could most usefully have supported, and it is the part that is absent.

### 2.2 No way to enumerate available fonts

Covered as a defect in §1.9, but it belongs here too: I searched the SDK reference and the symbol
index for any font-listing call and there is none. `ctx.measureText` against a control string is
the only way to discover what exists, and it is an inference, not an answer. For a toolkit with a
typography module this is the most conspicuous missing call on the surface.

### 2.3 Search never told me which call implements a technique I did not already know about

`Search` was good at confirming and locating things: every call-name query (`Logo.generateFaviconScaleTest`)
resolved with `confidence: "direct"` and an authoritative signature, and the prose hits carry an
`apis` array that maps passages to calls, which is genuinely well done. Manual 10 §4 pointed me
straight at `correctBoneEffect` / `computeOvershoot` / `computeOpticalCenter`, and Manual 12 §3 gave
me the mark-function contract that is nowhere in the SDK reference. That saved real time.

What it never did was surface a capability I had not already guessed at. Every natural-language
query returned `confidence: "related"` — by design, the tool says so — and the ranking is BM25 over
prose, so it rewards vocabulary overlap. My "romance/warmth/intimacy" query returned Manual 12's
*presentation board* section as its top hit at score 20, well above anything about tone, purely on
term frequency. An agent that trusted the ranking would have been led to a deliverable format when
it asked a question about meaning.

The mitigation is documented and works: `notSearched` was empty on my `all`-scope queries, and the
hint text correctly tells you to check `polson://sdk/symbols` before writing a call. I did, and it
was right every time. **The symbol index is trustworthy; the map at `polson://sdk/index` is not**
(§1.3) — those two facts sitting next to each other is the thing worth fixing.

---

## 3. What misled me

Ranked by cost.

1. **`element.attr({transform: ...})` and `element.transform(SnapMatrix)` silently no-op** (§1.5).
   The SDK reference explicitly lists `transform` among the attributes `attr()` accepts and
   documents the `SnapMatrix` overload by name. Both are false. Cost: three scripts and a
   re-architecture of how I place vector geometry. This is the worst kind of wrong answer — not
   absent, but present and incorrect, failing without an error.

2. **`polson://sdk/index` under-reporting the `Logo` inventory** (§1.3). The map claims to be the
   complete inventory and to replace reading the reference. Had I believed it, I would have
   concluded the scale-test and brand-sheet calls — the brief's central requirement — did not
   exist. I only avoided that because Manual 12 names them.

3. **`getPixel`'s documented channel order and return type** (§1.2, §1.11). Both wrong, on the call
   the docs nominate for verification. I checked the format against a known asymmetric colour early
   and by luck; had I not, every subsequent pixel assertion in the session would have been garbage.

4. **`computeOpticalCenter(bounds, 'triangle')` returning 58%** (§1.7), against its own documented
   42–48% and against Manual 10's stated direction of correction. My mark's sails are triangles, so
   this was the natural argument. Using it would have made the mark worse in exactly the way the
   call exists to prevent.

5. **`evaluateFontPairing` scoring garbage at 90** (§1.8). Low cost to me because I only used the
   one pairing I had already reasoned my way to, but it means the score cannot be used for what a
   score is for.

---

## 4. What I hand-rolled that the SDK already provided

Honestly, in both directions.

**Hand-rolled unnecessarily — my fault:**

- **A path-string scaler.** I wrote a regex that multiplied every number in an SVG `d` string by a
  scale factor. It scaled the **arc flags** (`A r,r 0 1 0 ...` → `0 2 0`), producing invalid paths,
  and four of my five sketch variants silently rendered nothing while the fifth — the only one built
  from cubic Béziers — worked. Ten minutes lost to a self-inflicted wound. `ctx.translate` +
  `ctx.scale` already do this correctly on the raster side and are documented; I should have reached
  for them first. Notably the failure mode was *nothing drawn*, not an error.

**Hand-rolled necessarily — the SDK's gap:**

- **The entire brand presentation sheet** (§1.12). `generateBrandPresentationSheet` exists and is
  the nominated Stage 7 call; it was not usable.
- **The wordmark lockup** (§1.13), because no lockup call exposes a typeface.
- **Tagline tracking to span the wordmark exactly.** Manual 11 §1B states the rule — the tagline
  should "span the exact width of the primary wordmark using adjusted letter tracking" — and nothing
  implements it. Twelve lines: measure the wordmark, measure the tagline natural width, distribute
  the difference across the gaps.
- **Per-glyph kerned text drawing.** `computeOpticalKerning` returns the numbers but there is no
  call that *applies* them; `fillText` draws a whole string with default metrics. So the kerning
  data is only reachable by drawing character by character yourself. Same for
  `computeWordmarkTracking`. The toolkit computes spacing it cannot set.
- **Vector placement** (§1.5), for the reasons above.

---

## 5. The vector surface specifically

The brief asked whether `Snap` / `VectorLogo` is as complete and as documented as the raster side.
It is not, and the gap is structural rather than cosmetic.

**What is genuinely there.** Everything the docs list exists and is callable — I probed the whole
surface in my first script and `paper.{squircle, goldenCircles, goldenSpiral, polarGrid,
emblemBadge, monogramMatrix, clearSpaceGuide, isometricGrid, ogeeCurve, gradient, mask}` and
`Snap.path.{boneEffect, tangentFillet, ogeeCurve, squircle}` all resolved. `fill-rule="evenodd"` is
honoured by the renderer, which is what made a negative-space mark possible at all. `outSvg` writing
the last vector document even when the script returns a canvas worked exactly as documented and is
how I produced `output.svg` and `output.webp` from one script.

**Where it falls behind:**

1. **Transforms are broken** (§1.5) — the single biggest gap. The raster side has a correct,
   complete transform stack (`save/restore/translate/scale/rotate/transform/setTransform`). The
   vector side has one shorthand string grammar, three documented spellings that silently do
   nothing, and a whole `SnapMatrix` class that cannot be applied to anything. Composition — the
   core activity of retained-mode vector work — is where the vector side is weakest, which is
   backwards.

2. **`getBBox()` ignores transforms** (§1.5), so measurement and placement disagree.

3. **The armature helpers lost their options** (§1.6). `Logo.drawPolarGrid` takes
   `{lineColor, lineWidth}`; `paper.polarGrid` takes nothing and hard-codes indigo and amber into
   inline styles that a parent `attr()` cannot override. Same for the isometric grid. My
   construction plate is stuck with colours from outside the brand palette.

4. **No vector equivalent of the verification suites.** `generateFaviconScaleTest` and
   `generateMonochromeTest` are Canvas2D-only. For a vector-first brief, proving the master artwork
   means crossing to raster to test it — which is arguably correct (legibility *is* a raster
   question) but it means the vector document is never the thing under test.

**Where I crossed to Canvas2D, and what it cost.** Three places:

- **Small-size proofs.** Unavoidable and correct — 16px legibility is a rasterisation question.
  `new CanvasPath(svgPathData)` is the documented bridge and it worked flawlessly; I authored every
  variant as SVG path data and tested it as pixels without ever maintaining two copies of the
  geometry. This is the best-designed seam in the SDK.
- **The presentation board.** Forced: all typography, all the test suites, and all image
  compositing are raster-only. `paper.text()` exists but there is no vector `measureText`, so
  optically-set type in SVG is not really available.
- **Nothing was lost crossing over** in fidelity terms — the raster renders match the vector exactly
  (I re-rendered `output.svg` standalone through `RenderSvg` at the end and it is identical to the
  in-script render). The cost is one-directional: you can go vector → raster cleanly, and there is
  no route back.

---

## 6. The `Logo` and `LogoType` toolkits

### Did the optical-tuning calls do something I could actually see?

**`correctBoneEffect` — yes, and I used it in the final mark.** For a 14-unit mast it returned a
6-point polygon whose mid-span points sit 0.560u outboard of the ends — a 4.0% bulge, squarely in
Manual 10's stated 2–5%. `pinchCorrectionFactor: 0.05` gave 0.70u (5.0%), so the parameter is the
fraction of stroke width and behaves linearly. The mast of my mark is exactly the case the
correction is for — a dark bar between two larger light shapes — and at 256px and above the
corrected mast reads as parallel where the uncorrected one visibly waists. Below ~64px it is
sub-pixel and does nothing, which is expected for an optical correction.

One integration wrinkle: it returns a **polygon**, but my mast edges are two quadratic segments
inside a larger closed path. I could not use the returned points directly; I had to read the bulge
off `points[1].x` and convert it to a quadratic control offset (`2 × bulge`) myself. A returned
bulge magnitude, or a path-string variant like `Snap.path.boneEffect` gives, would have dropped in.
`Snap.path.boneEffect(50,14,50,72,3,0.5)` → `M 50 14 Q 47 43 50 72` returns a *centreline*, not a
bar outline, so it does not solve the same problem despite the shared name.

**`computeOvershoot` — correct, and honest about its units.** 2.0px on a 100px circle (2.0%),
2.8 for a triangle, 1.5 for an arch; Manual 10's map explicitly warns "Returns pixels, not a
percentage", which is the kind of note the rest of the SDK needs more of. I used it for the disc
against the wordmark cap-height in the lockup.

**`computeOpticalCenter` — half right.** `'general'` returns 48.0% of box height, above the
geometric centre, correct. `'triangle'` returns 58% — see §1.7. I used `'general'`; the boat's
combined visual mass already sits high in the disc because the hull is shallow and the sails are
tall, and the returned value confirmed it rather than moving anything.

**`createTangentBlend` — exactly right.** For `p1=(18,72)`, `corner=(44,72)`, `p2=(44,26)`, `R=6` it
returned `arcStart=(38,72)`, `arcEnd=(44,66)`, `arcCenter=(38,66)`, `tangentDistance=6`,
`cornerAngleDeg=90`, `sweepAngleDeg=90` — all exactly the hand-computed values for a 90° fillet
(`d = R/tan(α/2) = 6`). It is the most trustworthy call in the `Logo` toolkit. I ended up not
needing a fillet in the final mark (every junction is already a tangent-continuous Bézier), but I
would use it without checking next time.

**`generateFaviconScaleTest` and `generateMonochromeTest` — both good, and they earned their keep.**
The Manual 12 §3 contract (one `(ctx, size) => void`, each suite owns its canvas, the mark sets its
own colours, draw into a `size × size` box at the origin) is accurate and worked first time. The
monochrome board's knockout filter — keeping alpha and replacing hue — is a genuinely good test: it
proves the silhouette, not four backgrounds. These two calls did more for the brief than everything
else in `Logo` combined. **Their absence from `polson://sdk/index` (§1.3) is therefore doubly bad.**

### Would I defend the kerning and pairing output to a client?

**The kerning numbers, yes — after damping.** `computeOpticalKerning` classifies glyph pairs
sensibly (`getGlyphShapeType`: S→round, A→diagonal, I/L/B→straight, matching Manual 11's table) and
the signs and relative magnitudes are right: `AT` most negative at −5.76, `IL`/`LB` positive at
+2.56, `BO` +1.60. That is correct old-style logic — diagonals tuck, straight pairs need air.

But the magnitudes are tuned for display sizes far above where I used them. Applied raw at 46px,
"Sailboat Tours" came apart — `AT` pulling 5.76px is a visible collision in a 46px word. I applied
them at **0.3×** and the result is spacing I would put in front of a client. So: right model, right
signs, wrong scale, and no guidance in the docs about the size regime the figures assume.

**The tracking numbers, mostly.** The tagline figure (+220‰ at 13px caps) is exactly right and I used
it unmodified. The wordmark figure is questionable — −15‰ for all-caps at 64px is outside Manual
11's own −20‰ to −50‰ band, and it is *looser* than the −35‰ it gives for mixed case, which inverts
the normal relationship. I used the mixed-case value, which is what my wordmark is.

**The pairing call, no.** §1.8 — it cannot distinguish a considered pairing from a typo.

---

## 7. Requisition, and where texture belongs

**Was the material-versus-form boundary clear?** The *concept* is clear and well explained — the
docs and the refusal text both say the same thing crisply: draw the form, requisition the surface;
"the planking, not the ship". I never had to guess what was being asked of me.

**Did a refusal make sense?** The one I got did not, and it was inverted (§1.1):
`weathered teak boat decking` was **refused as Form**, while `a sailboat` was **allowed**. The guard
is matching the token `boat` rather than the head noun of the phrase, so for a marine client it
fires on exactly the material vocabulary the brief needs and misses the object the docs promise it
will catch. I did not test the live refusal path by actually requisitioning `a sailboat` — the free
offline classifier already demonstrates the inconsistency, and spending real budget to generate a
picture of a sailboat is precisely the misuse the boundary exists to prevent.

**Did I know what to do next after a failure?** Yes, and this part is well designed. Nothing throws;
every result carries `success`, `failureName`, `remedy`, `retryable` and `error`, and the docs push
you to read `remedy` before retrying. I wrote my requisition with a full failure branch that logs
`remedy` and exits cleanly to a procedural fallback, and I would have known exactly what to do had
it fired. The `Assets.budget` object (`total`/`spent`/`remaining`/`cacheHits`/`canAfford`) is
similarly good — I could check affordability before committing.

The one requisition I made succeeded: `sun-bleached canvas sailcloth weave, tight warp and weft,
natural linen fibres, flat even lighting` → 512px WebP, `wraps=true`, `repaired=false`, seam steps
~15, `gemini-2.5-flash-image`, 1369 tokens, **1 of 12 budget spent**. It took **9.5 seconds**, which
confirms the docs' warning: three of those in one script would have blown the 30s limit. Requisition
in its own short script is correct advice and should stay prominent.

### The rule I settled on

> **Texture may touch the presentation. It may never touch the mark.**

The reasoning is not aesthetic, it is a consequence of the brief's own non-negotiables. The mark has
to reproduce in a single flat colour — black on white, knocked out white on dark — and it has to
hold at 16px. A raster fill cannot survive either test: it has no meaning in one ink, and at 16px it
is four pixels of noise. So anything raster inside the mark is not a risk to be managed, it is a
guaranteed failure of two stated requirements. The mark is 100% vector, one closed path, one flat
colour, and would survive a sign-painter with one tin of paint.

A presentation board is a different object. It is a document, and documents are printed on a stock.
The requisitioned sailcloth is composited as the board's ground at **13% opacity** — enough to give
the sheet the tooth of canvas, which is materially apt for a sailing brand, and far too subtle to
carry any load. Delete it and the identity is unchanged; that is the test I applied. Every mark
instance on the board — including the 16px one and the knockout one — is drawn as flat vector on
top of it, untouched.

The corollary, which I want to state because it is the part that would tempt a weaker decision:
texture would have made the primary-mark panel look richer. It would also have made the mark a lie,
because the thing shown to the client would no longer be the thing that ships to the favicon. Where
texture *would* legitimately belong beyond the ground is in mockups — a sail, a hull, an awning with
the mark applied — and that is a brand-application exercise, not a logo.

---

## 8. Time and iterations

**Attempts to a first correct call: one.** My opening probe script — budget check, classifier,
surface reflection over ~30 members, and an `evenodd` pixel test — ran clean first time. The
documented spellings were right, the sandbox behaved as described, and ECMAScript 2025 features
(destructuring, spread, template literals, `for...of`, optional chaining) all worked as promised.
That is a good result and worth saying plainly: the *execution model* documentation is accurate.

**Total: 18 `ExecuteScript` calls, 1 `RenderSvg`, 4 `Search` calls, 6 resource reads.**

Where the time actually went:

| | share | notes |
|---|---|---|
| **Design iteration** — 7 sketch rounds, rendered and looked at | ~50% | The intended work. Five directions → disc concept → hull treatments → Φ mast placement → 16px failure → fix → lock. Four of these rounds changed the mark materially. |
| **SDK defects** | ~30% | The transform investigation (§1.5) was the big one: 3 scripts. Discovering the font situation (§1.9): 1 script. Verifying the brand-sheet contrast and `getPixel` type claims: 2 scripts. Probing the optical calls: 1 script. |
| **My own bugs** | ~10% | One: the arc-flag-corrupting path scaler. Cost one wasted render of five variants. |
| **Reading and searching** | ~10% | The manuals are dense and useful; Manual 10 and Manual 12 §3 were both worth reading in full. |

The notable shape of it: **the design work and the defect-hunting were roughly the same size.** In a
session where the SDK behaved as documented, this mark would have taken about half as long — and
almost all of the recoverable time is in one defect (§1.5) and one documentation gap (§1.9).

The most expensive single characteristic across everything above is not any individual bug, it is
the **failure mode**. Transforms reduced to identity, an unsupported font, a `===` that is always
false, a path with a bad arc flag — none of these raise. They all produce a plausible-looking
render that is quietly wrong, which for an agent that cannot see its own output except by rendering
and looking is the most costly way to fail.

---

## 9. The mark, and why

Recorded here so the design decisions are auditable against the artefacts in `artifacts/`.

**The idea.** A sun-or-moon disc at sea, with a two-sailed sloop cut *out* of it as negative space.
Sketched five directions first (`artifacts/sketch-01-directions.webp`): twin sails, moon disc, ogee
sea, an ST monogram, and a merged silhouette. The monogram died on sight — the ogee `S` reads as a
worm and the `T` as a slab. The merged silhouette read as a mountain. The ogee-sea version left the
sails floating disconnected above the waterline. The twin-sail version was competent and completely
generic — the stock sailboat icon, with nothing to defend.

The disc won because it is the only one with an argument behind it: making the boat negative space
means the mark is **monochrome-native by construction**. There is no colour version to degrade from;
positive and knockout are the same geometry. It is also app-icon shaped without a container being
bolted on.

**How "romantic" is encoded**, given that the manual corpus has nothing to say about it (§2.1):
- **Pairing, not a heart.** Two sails, not one — unequal, sharing a single hull, leaning together.
  A couple, structurally.
- **The disc reads as sunset or moonrise** — the brief's own words for what this company sells.
- **Convex, wind-filled leeches** on both sails. Both outer edges bow outward; the mark is full of
  air rather than taut. Straight leeches tested as clinical.
- **Palette**: Sunset Coral against Dusk Indigo — warm low light against deep blue water — on a warm
  Sailcloth ground rather than white. Candlelight, not daylight.
- **Typography**: Garamond, an old-style humanist serif with genuine grace, against a widely tracked
  neutral sans. (Chosen partly by elimination — see §1.9.)

**The armature.** The mast sits on the **golden section of the boat's beam** (beam 16→84, mast
centre at 41.97), which makes the two sails Φ-unequal while their combined visual mass still lands
essentially on the centreline — the mainsail is bigger and to the right, the jib smaller and to the
left, and the two balance. That is a single, checkable claim rather than Φ sprinkled decoratively.
The armature is visible in `artifacts/stage-04-construction.webp` and absent from the final mark.

**What testing changed.** Three things, all caught by rendering and looking:
1. The first disc version burst its hull out through the disc edge, leaving fragile spurs that
   aliased away at small sizes (`sketch-02b`, `sketch-06`). Fixed by an explicit containment audit —
   every vertex is now checked against the disc radius at build time, and `artwork.js` errors if one
   escapes.
2. **The mark failed at 16px** on the SDK's own scale ladder (`stage-06a`): the two sails merged
   into a single slit and the hull vanished. This is the failure the brief anticipated. Fixed by
   enlarging the counter, widening the mast from 10 to 14 units, and deepening the hull — compared
   across four variants at true 16/20/24px in `sketch-07`. The final mark holds all three elements
   at 16px; the board's scale row shows it at actual size.
3. The hull was initially a fat bowl that swallowed the sails at full size while looking fine small
   (`stage-04`). Only visible at 240px+. Both failure directions — large and small — needed their
   own look.

**Proof artifacts**, all generated by the SDK's own suites from the same single mark definition that
produces `output.svg`:

| artifact | what it shows |
|---|---|
| `stage-06a-scale-ladder.webp` | the **failing** 7-tier ladder — the earlier mark, whose sails merge into one slit at 16px. Kept deliberately as the evidence. |
| `stage-08-scale-ladder-final.webp` | the **shipped** mark on the same ladder: both sails and the mast survive at 16px, crisp from 24px up. |
| `stage-06b-monochrome.webp` | the 4-way board — positive black, knockout white on dark, greyscale, app-icon squircle. The silhouette holds in all four with the palette stripped. |
| `sketch-01`…`sketch-07` | the seven iteration rounds, in order: five directions → disc concept → hull treatments → Φ mast placement → 16px failure → fix → lock. |
| `stage-04-construction.webp` / `.svg` | the armature (polar grid + Φ circles) with the mark over it, and the mark alone. |

---

## 10. Notes to carry forward

- Requisition **is** configured this run: `Assets.budget` = 12 total, 0 spent.
- `paper.squircle / goldenCircles / goldenSpiral / polarGrid / emblemBadge / monogramMatrix /
  clearSpaceGuide / isometricGrid / ogeeCurve / gradient / mask` all present on `SnapPaper`, and
  `Snap.path.{boneEffect, tangentFillet, ogeeCurve, squircle}` all present. The documented vector
  surface is really there.
- Manual 12 §3 gives the contract for the test suites and it is worth restating because it is not in
  the SDK reference: each suite **owns its whole canvas**, the mark function **must set its own
  colours**, and it must draw into a `size × size` box **at the origin** — the suites translate but
  do not scale.
