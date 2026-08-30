# Findings — cs-2 developer experience report

What broke, what I could not find, what misled me, what I hand-rolled that the SDK already had, and
whether the staged handover through files actually worked.

Ordered by how much each cost. Several entries are things I got wrong rather than things the SDK got
wrong, and those are marked, because a DX report that only blames the tooling is not useful.

---

## 1. `Drawing.createLoomisHead(originX, originY, …)` — `originY` is the head's **centre**, undocumented

The signature and the prose both say `originY` with no statement of what the origin is anchored to.
The natural reading for a head construction is the crown. It is not: it is the vertical centre.

```javascript
Drawing.createLoomisHead(600, 64, 208, 0, 0).crown   // → { x: 600, y: -40 }
```

Passing the crown y puts the entire construction half a head above the canvas. `crown = originY − H/2`
and `chin = originY + H/2`, so the correct call for a head spanning y=64…272 is `originY = 168`.

Nothing in `polson://sdk/core/Drawing` or `polson://sdk/schema/Drawing` states this. The schema
documents `origin` as a bare `{x, y}` with no description, while every other landmark in the same
object (`crown`, `hairline`, `brow`, `chin`) is self-describing — which actively suggests `origin` is
one of them.

**Suggested fix:** one clause — `originY: number — the head's vertical CENTRE; crown is originY − headHeight/2`.

**Cost:** caught by a probe before rendering, so no wasted render. It would not have been caught by
looking at output, because a head drawn entirely off-canvas and a head drawn correctly both produce a
plausible-looking construction sheet if the rest of the sheet fills the frame.

---

## 2. `eyeLineY` is documented as "half the total head height" and is not used by the reference

Not an SDK defect — a note for anyone building on the constructive toolkit. The canonical Loomis
eye line came back at 52 % of head height, which is right. The **reference** puts it at 63 %. Nose,
mouth and hairline are all similarly low. The toolkit's canon is correct and the subject simply is not
canonical, which is normal for stylised work.

This matters for the workflow rather than the API: an agent that takes `loomisHead.nearEye.center` and
draws there will produce a face that is *proportionally correct and wrong*, and no coverage metric on
the whole figure will flag a 23px feature offset. It has to be caught by measuring the reference. The
Penciler role spec is right to demand written-down measurements; it could usefully say outright that
the canonical head object is a **check**, not a source of coordinates.

---

## 3. The statement cap is documented, but the practical budget is not — and it kills the whole script

`polson://sdk/index` states the limit plainly (2,000,000 statements) and even carries a TIP steering
heavy pixel work to `Skia.Shader` / `Skia.ImageFilter`. Good. What is missing is any sense of what
2,000,000 buys, and the failure is total: no partial results, no logs, no indication of which loop.

```
"error": "The maximum number of statements executed have been reached."
```

I hit it three times. Empirically, one `getPixel` + hex-parse + classify iteration costs roughly
15–40 statements, so the budget is about **60,000–120,000 sampled pixels per script**. Concretely:

| what I tried | iterations | result |
| :--- | ---: | :--- |
| 14 regions plane-fitted at 2px | ~200 k | **killed** |
| same at 4px | ~50 k | fine |
| 61 rows × 4 classes × 2 images, one pass per class | ~250 k | **killed** |
| same, one pass per row collecting all four classes | ~62 k | fine |
| 3 images scored at 4px | ~196 k | **killed** |
| same at 6px | ~87 k | fine |

Note the second row of that table: the fix was not less work, it was a **different loop shape**. The
cap punishes the naive structure (`for each class: for each pixel`) far more than the work justifies.

**Suggested fixes, in order of value:** (a) report the statement count reached and the source line in
the error; (b) put a worked number in the docs — "roughly 100 k `getPixel` iterations per script" is
the sentence I needed; (c) consider whether the cap should surface as a catchable exception so a script
can degrade to a coarser sample rather than losing everything.

This interacts badly with the workflow's central instruction. "Look again at what you rendered" means
a pixel comparison, and pixel comparisons are exactly what the cap punishes. Every verification pass
in this run had to be budgeted.

---

## 4. Missing: any image-comparison primitive

**This is the single highest-value addition for this workflow.** `comic_studio` exists to reproduce a
reference, and the SDK gives you `getPixel` and nothing else. I hand-rolled, in interpreted JS under a
statement cap:

- colour-family classification and a coverage-error metric
- mean RGB distance over the shared-coverage region
- per-scanline edge-delta reporting (the tool that actually found every structural defect)
- a regional error table, which is what caught the shader being a net loss
- least-squares plane and quadratic surface fitting, plus Gaussian elimination

The fitting is fair enough — not a graphics SDK's job. The **comparison** is not. Something like
`Skia.Image.compare(a, b, options?)` returning `{ meanDistance, coverageError, worstRegions[] }`,
running natively, would have removed most of my statement-cap trouble and every one of my hand-rolled
metrics. It would also make the whole workflow's premise measurable rather than eyeballed.

Note that `Drawing` already ships `verifyPlumbAlignment`, `verifyPerspectiveConvergence` and
`computeRelativeDistance` — verification helpers are clearly considered in scope. There is just nothing
for "does this match the target".

---

## 5. `Search` truncates the SkSL example before the entry-point signature

The full resource is fine: `polson://sdk/core/Skia` contains a complete worked example showing
`half4 main(float2 coord)` and the uniform-passing convention. But the `Search` result for
`"Skia.Shader.sksl runtime shader uniforms main coord example"` returns the section **cut off three
lines into the code block**, immediately before the signature:

```
### Custom SkSL Shader Example
    uniform float2 u_resolution;
    uniform float4 u_color1;
    uniform float4 u_color2;
… (section truncated — read the full resource for the rest)
```

This matters because the role specs explicitly tell agents that `Search` is the way in when their host
cannot open MCP resources — `01_penciler.md` says so in as many words. On such a host, SkSL is
undiscoverable: you get the uniform declarations and never the entry point.

**Suggested fix:** let the truncation window fall on a code-block boundary, or hoist the one-line
signature into the prose above the example.

What is genuinely not stated anywhere, full resource included: **the coordinate space of `coord`**. The
example's `coord / u_resolution` implies device pixels, and a probe confirmed device/canvas pixels, but
it is implied rather than documented. For a shader used as a `fillStyle` on an arbitrary path that is
worth one explicit sentence.

---

## 6. Things I probed that were already documented — my error, not the SDK's

Recording these so the report is honest about where the friction actually came from.

- **`bitmap.getPixel` return type.** I spent a probe discovering it returns `"#RRGGBBAA"`. It is
  documented in `polson://sdk/core/Skia`, uppercase-hex and alpha-last called out explicitly, with a
  genuinely good note on *why* you read pixels back. I had only read the index inventory, which lists
  the signature without the return type. The per-area resource is the one to read; the index is a map,
  and it says so.
- **`Skia.MaskFilter` vs `Skia.ImageFilter`.** I used `ctx.filter = Skia.ImageFilter.blur(...)` to
  soften the occlusion shadow. The docs draw exactly this distinction — *"a mask filter softens the
  **shape**, an image filter blurs the **result** — so a soft mask leaves the fill flat, which is what
  an airbrushed edge is"* — and `MaskFilter` was the right tool. The shadow I produced was a smeary
  blob I then had to remove entirely. The guidance existed and was good; I acted before reading it.
- **`Skia.Brush.ink()`** ships a three-tier weight system (*"roughly `width` 4, 2 and 1"*) that maps
  one-to-one onto the Inker's brief, plus `ctx.useBrush` with well-designed state semantics. I did not
  use it, correctly — the reference has no line art — but I found it only while writing this report,
  which means an Inker facing a subject that *did* need ink might not have found it either.

**The generalisable lesson:** `polson://sdk/index` is good enough to write plausible code from and
therefore discourages opening the per-area resources, which is where the reasoning lives. The index
already warns *"This map replaces reading the reference documents whole"* — the risk is that it reads
as "replaces reading them at all".

---

## 7. Missing: a smooth-path-through-points helper

Every traced silhouette in this run is a per-scanline point list, and every one needed the same
quadratic-midpoint interpolation to stop it reading as a staircase. I wrote `tracePath` once and used
it fifteen times.

The SDK has `path.sCurveTo`, `path.cCurveTo`, `Skia.PathEffect.corner` and Snap's path measurement,
but nothing that takes `[[x,y], …]` and produces a smooth closed curve. For a toolkit whose stated
workflow is "trace the reference, then draw it", that is the missing primitive — something like
`ctx.smoothPath(points, closed?)` or `Snap.path.throughPoints(points)`.

---

## 8. What worked well, specifically

- **`Skia.Image.load` path semantics.** Documented as *"relative to the project directory, exactly as
  `outFile` is"*, and it behaves exactly so. Reading back `artifacts/stage2_colorist.webp` to diff
  against the reference is the mechanism the whole perception–action loop runs on, and it was
  frictionless.
- **`outFile` with `imageBytes` omitted by default.** The right default. This run made ~30 renders; had
  each returned base64 the context would have been unusable.
- **`Stage.begin` / `Stage.note` persistence.** Worked as documented, survived across executions, and
  the event log in `events/` shows the stage attribution correctly including the two reopened stages.
- **`Skia.Font.has` / `families`.** The warning about silent typeface substitution is real and the
  check is one line. `sans-serif` is *not* a resolvable family here — `Skia.Font.has('sans-serif')`
  returns false — so the CSS-ish habit of writing `'15px sans-serif'` silently substitutes. Worth
  knowing before setting type.
- **SkSL uniform marshalling.** Arrays map to `float2`/`float4` exactly as expected, values are
  straight 0–1 sRGB, and a probe round-tripped to the exact expected hex.

---

## 9. Did the staged handover through files work?

**Yes, and the file-based handover specifically is what made the failures recoverable.** Each stage
read the previous render and the previous script rather than a summary, so nothing depended on
remembering. Two concrete cases:

- The Inker read the Colorist's script, saw the torso's left edge was one continuous path, and could
  tell from the reference scanlines that it must be two walls with a gap. That is a code-level
  observation that a rendered image alone would not support.
- The Critic re-ran the Colorist's own edge-delta measurement against a later artifact. Because the
  measurement was a script in `scripts/` rather than a conclusion in prose, it was re-runnable.

### Where a stage received something it could not use

**The Inker's brief.** `roles/02_inker.md` asks for a three-tier line-weight hierarchy, tapered
strokes, form-following hatching and solid blacks. The reference has **no line art whatsoever** — it is
flat vector shapes with gradients. Every instruction in that role spec, executed literally, moves the
work away from the target.

I resolved it by mapping the hierarchy onto the reference's own vocabulary (tone rather than stroke)
and recording that as a deliberate departure. But the pipeline gave no mechanism for a stage to say
"my brief does not fit this subject" other than doing it anyway and writing a note. In a multi-agent
run with a Facilitator this is exactly the case the Facilitator should arbitrate; in this staged form
it fell to the stage itself to notice.

Related, and worth flagging to whoever maintains the workflow: **the whole `roles/` set assumes a
painterly comic subject.** The Colorist is asked for procedural texture, atmosphere and rim light; the
Inker for feathering and Ben-Day halftone. The `Drawing` toolkit's shader presets
(`createHalftoneDotShader`, `createRopeFiberShader`, `createAtmosphericCloudShader`) are built for the
same assumption. Applying any of them to this reference would have been actively wrong. `comic_studio`
should either state that the role specs are a menu rather than a checklist, or the brief should route
flat-vector references to a different set of roles.

### The pipeline's ordering rule earned itself

`GEMINI.md` insists on pencil → colour → ink and explains why. Here the ink stage turned out to be
mostly subtractive, and having colour already correct is what made it obvious that the occlusion seam
should be *cut* rather than *painted*. Had I inked first I would have drawn a seam that the flats then
buried, exactly as the file predicts.

---

## 10. One methodological note

The most useful thing I built was not a drawing technique but the **per-scanline edge-delta report** —
for each row, the left and right edge of each colour region in the reference versus in my render,
printed only where they differ by more than 8px. Every structural defect in this run was found by it
and none were found by looking.

Looking at the render was still essential, and caught the one thing the metrics missed: the Inker's
painted seam scored acceptably and looked obviously wrong. The two are not substitutes. The metric
finds what you would not think to check; the eye finds what you would not think to measure.
