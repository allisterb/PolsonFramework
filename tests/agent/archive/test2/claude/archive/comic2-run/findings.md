# Polson SDK & MCP Server — Findings

**Task:** recreate `reference_images/comic2.png` (flat-vector flying superhero, transparent background).
**Deliverables:** `artwork.js`, `output.webp` (+ `output.svg`), this report.
**Session shape:** 14 measurement passes against the reference, 7 render/look iterations, ~25 `ExecuteScript` calls.

Harness check passed at start: `../../../../src/Polson.Drawing.Skia/ConstructiveDrawingToolkit.cs` was **denied**, `reference_images/comic2.png` **read**. Everything below comes only from `polson://sdk/*`, `polson://manual/*`, `Search`, and the five tools.

---

## 1. The headline problem: the vector API cannot make a gradient

The brief says *"The cape's fold shading is a gradient, not a shadow pass. Look for the gradient factories in the SDK reference."* For the vector engine, there aren't any. This cost roughly the first third of the session.

### `[bug]` `element.attr({fill: 'url(#id)'})` silently destroys the paint-server reference

```js
p.rect(10,10,100,50).attr({ fill: 'url(#g1)' });
// serialises as  style="fill:#g1;"  →  renders solid BLACK, success: true
```

The attribute setter runs the value through a colour parser that strips `url(` … `)` and keeps the fragment as if it were a hex literal. I tried every spelling I could think of:

| form | result |
|---|---|
| `.attr({ fill: 'url(#g1)' })` | `fill:#g1` ✗ |
| `.attr('fill', 'url(#g1)')` | `fill:#g1` ✗ |
| `.attr({ fill: "url('#g1')" })` | `fill:#g1` ✗ |
| `.attr({ fill: 'url(#g1)' })` on a `<path>` | `fill:#g1` ✗ |
| `.attr({ fill: 'url(#g1)' })` on a `<g>`, inherited | `fill:#g1` ✗ |
| **`.attr({ style: 'fill:url(#g1)' })`** | **`style="fill:url(#g1)"` ✓** |

Only the raw `style` string survives, because it bypasses the colour parser entirely. `artwork.js` uses `{ style: 'fill:url(#id)' }` throughout — it works, but no reader of the published docs would arrive at it.

The **silence** is the worst part. `success: true`, no warning in `logs`, and a shape that renders black. Every silent-wrong-output failure in this run cost a full render-and-look cycle to notice. An unparseable colour string should at minimum emit a `console.warn` into `logs`.

### `[bug]` `element.el(name, attrs)` ignores `name` and always emits `<g>`

```js
const defs = p.el('defs');                       // → <defs /> (empty, children go elsewhere)
const grad = defs.el('linearGradient', {...});   // → <g id="capeGrad">
grad.el('stop', { offset: '0%', ... });          // → <g offset="0%" ... />
```

`element.el(name, attrs?)` is documented as *"Creates and appends an arbitrary SVG child element"*. It is the documented escape hatch for anything the typed factories don't cover — and it produces a `<g>` for every name I passed. That closes the only sanctioned route to building `<defs>` / `<linearGradient>` / `<stop>` / `<clipPath>` / `<filter>` / `<mask>` from script.

### `[friction]` No gradient factory anywhere on the Snap surface

`polson://sdk/index` lists **65 Snap methods**. None creates a paint server. Real Snap.svg has `paper.gradient("l(0,0,1,1)#000-#fff")`, which is the API this adapter is modelled on, so its absence reads as an omission rather than a design choice. `Search("linear gradient fill definition on a vector shape", scope:'sdk')` returned only `ctx.createLinearGradient` (Canvas2D) and `Skia.Shader.linear` — both raster-only. Nothing in the corpus says "the vector API has no gradients"; I had to discover it by exhaustion.

### `[positive]` `Snap.parse()` round-trips `<defs>` faithfully, and the renderer handles gradients perfectly

The two facts that unblocked the whole build:

1. `RenderSvg` on hand-written SVG with `<linearGradient gradientUnits="userSpaceOnUse">` renders **correctly** — so the bug is in the Snap adapter, not in the Svg.Skia renderer. Having `RenderSvg` as an independent second path was genuinely valuable for bisecting this.
2. `Snap.parse(xml)` preserves `<defs>`, `<linearGradient>`, `<stop>`, `stop-color`, `gradientUnits`, and *keeps* a `url(#id)` fill on elements that came in through the parse. You can then keep appending to that paper.

So the working pattern — and what `artwork.js` does — is:

```js
const paper = Snap.parse(`<svg …><defs><linearGradient id="gCape" gradientUnits="userSpaceOnUse" …>…</linearGradient></defs></svg>`);
paper.path(d).attr({ style: 'fill:url(#gCape)' });
```

It's a workaround built from two documented calls, which is the best kind, but it is not discoverable and nothing in the docs hints at it.

**Fix priority:** (a) make `attr` pass `url(#…)` through unchanged; (b) make `el(name)` honour `name`; (c) add `paper.gradient(...)`. Any one of the three unblocks vector gradients; (a) is a one-line guard.

---

## 2. Vector vs raster: the decision, and why

**I chose Snap.svg vector, and would again** — but the choice was much closer than it should have been.

For: the subject *is* flat vector art — a dozen closed Bézier shapes, no outlines, transparent background. Vector gave me resolution independence, `result.SvgXml` / `outSvg` for free, and a scene graph whose z-order I control by construction order. Overlapping capsules that share a `userSpaceOnUse` gradient join with **no seam at all**, which is what makes the bent limbs work — that only holds because the gradient is anchored in user space, not per-shape. That property alone paid for the vector route.

Against: the gradient situation above; and Canvas2D would have given me `createLinearGradient`, `globalCompositeOperation`, `ctx.filter` and clipping directly.

`[positive]` **The two engines interoperate cleanly in both directions**, which softened the trade-off a lot:

- `ctx.drawImage(snapPaper, …)` and `ctx.drawSvg(…)` bring vector into raster.
- `Skia.Image.load('artifacts/stage5.png')` let me load *my own previous output* back in and composite reference-vs-mine QA sheets.
- `Skia.Image.fromBytes(paper.toImageBytes(w, h, 'png'))` round-trips a live paper to sampleable pixels **in-process**, no disk hop. (I assumed this wasn't possible and only verified it at the end — it works, and it deserves a mention in the SDK index's execution-model section, because it's the bridge that makes "draw in vector, measure in raster" a single-script operation.)

`[nit]` What's missing is a *vector-side* measurement story. `element.getBBox()` exists, but there's no vector hit-test or "what colour is at this point" — so all my QA went through raster.

---

## 3. `Search` and the design manuals

`[positive]` **`Search` routed correctly on anatomy and composition.** `"eight head figure proportion mannequin construction dynamic action pose"` returned Manual 08 §6 and §1 as the top two hits with the exact 8-head table and `Drawing.createMannequinFigure` bound to it. `"flowing cape cloth ribbon fold silhouette flat shape"` correctly surfaced Manual 02's ribbon principle. The `apis: [...]` array on each hit is the best part of the result shape — it answers "which call implements this" without a second lookup.

`[friction]` **It cannot express a negative.** The gradient query returned raster answers with no signal that the vector API lacks the feature. When an agent asks "how do I do X" and X is unsupported in the engine it's using, a ranked list of adjacent-but-wrong answers is worse than "no match in scope".

`[bug]` **Manual 05 documents a call that doesn't exist.** §3 says:

> *"For vector work, `Snap.path.ogeeCurve(x1, y1, x2, y2, amplitude, inflectionT)` returns an S-curve path string directly."*

Verified: `typeof Snap.path.ogeeCurve === 'undefined'`. The real member is `paper.ogeeCurve(...)` (`typeof === 'function'`), which is where `polson://sdk/index` lists it, under LogoType. Two published docs disagree, and the manual — the one an agent reaches for when it's thinking about curves — is the wrong one.

`[friction]` **The manuals are Canvas2D-first to the point of excluding vector work.** Every "Constructing It: A Runnable …" section in Manuals 02, 03, 05, 08 and 09 opens `createCanvas(w,h)` + `getContext('2d')`. The "Implemented by:" bindings point at `Drawing.*` calls that take a `ctx`. For a flat-vector target this means the design *theory* transfers (the 8-head canon, the CSI curve grammar, the three-value study, the 70-20-10 law) but none of the *code* does. A "doing this in the vector engine" note on those sections would close the gap — especially since the vector engine is the natural fit for the single most common commercial illustration style.

---

## 4. The Drawing / Constructive Drawing toolkit

`[friction]` **`Drawing.createMannequinFigure` cannot make an action pose**, which is exactly what the task needed. Its only pose parameters are `shoulderTiltDeg`, `pelvicTiltDeg`, `spineOffset` — contrapposto modifiers on a standing figure. There is no way to set a limb angle, so a figure in flight, or mid-stride, or reaching, is out of reach. The returned `MannequinFigure` exposes `leftArm.{shoulder,elbow,wrist,hand}` etc. as *outputs*; if those were also accepted as *inputs* (or if there were a `poseLimb(figure, 'leftArm', {elbowDeg, …})`) the model would cover the whole space instead of one stance.

I used Manual 08's canon as **arithmetic** instead — laying out joints along a measured action line and expressing everything in head units. That worked well, and the manual's §1 table is a genuinely good reference. But the SDK call that the manual advertises as its implementation could not be used for the manual's own stated purpose ("heroic illustration").

`[friction]` **`Drawing.verifyPlumbAlignment(sternum, ankle, tol)` presupposes a standing figure.** Manual 08 presents it as *the* QA gate before committing a pose — "Contrapposto only reads if the weight line runs plumb to the standing foot." An airborne figure has no weight-bearing foot and no plumb line, so the documented check is undefined for the task in front of me, and the manual doesn't offer a substitute (an action-line straightness or a centre-of-mass check would be the analogues).

`[nit]` **Manual 08 has no stylised-proportion guidance.** It offers 8H heroic and 7.5H naturalistic. The reference measures **~5 head units** along its action line with a deliberately oversized head — standard emoji/mascot proportion, and probably the most common proportion in flat-vector character work. I recorded this and used the canon as a measuring system rather than a generator, but a "stylised: 4–6H, head 1.3–1.6× canon" row would have told me that in one read instead of one measurement pass.

`[nit] — the toolkit correctly does not apply here, as the brief anticipated.` `drawTaperedStroke`, `drawFeathering`, `drawCrossContourHatch`, `createHalftoneDotShader` and `drawHairRibbon` all encode a pen-and-ink / comic-shaded idiom: variable line weight, hatching, Ben-Day dots, lit-top/dark-underside ribbons. The reference has **no outlines at all**, one soft gradient, and six flat colours. Every one of those calls would have fought the target. Reporting it as intended: knowing when the toolkit doesn't apply is a real answer. The observation worth acting on is that this inking-oriented surface is the *largest and most prominent* part of `Drawing` (roughly a third of its 39 methods), while flat-vector illustration — an equally common house style — has no toolkit support at all.

---

## 5. Execution model and ergonomics

`[positive]` **`outFile` + reading the file back is an excellent perception loop, and it is the backbone of this whole session.** `ExecuteScript(script, outFile:'artifacts/stage5.png')` → `Read(stage5.png)` → look → adjust. Omitting the base64 by default when `outFile` is set is exactly the right call; it kept ten full-resolution iterations affordable. `outSvg` alongside it is a nice touch.

`[positive]` **`Skia.Image.load` + `getPixel` turned the reference into a measurable object**, and this was the single most valuable capability in the run. It let me replace guessing with measurement at every step:

- a quantised **palette histogram** over 65k samples → the exact Material palette (`#FFCA28` skin, `#42A5F5` suit, `#EA4335`/`#A52714` cape, `#543930` hair, `#FDD835` emblem);
- a **64×64 ASCII classification map** of the whole image, which is what actually let me read the pose (and corrected my eyeballed landmark estimates, which were off by 20–30px);
- **per-scanline silhouette extents** for the cape envelope;
- **colour-masked bounding boxes** for eyes / brows / nose / hairline;
- a **perpendicular width profile** along each limb axis, which caught that my arms were ~20% too fat.

I'd call this out in the SDK docs explicitly. "Load the reference and measure it" is a first-class agent workflow and nothing in `polson://sdk/*` suggests it's possible.

`[friction]` **`ExecuteScript` takes only an inline `script` string — there is no `scriptFile`.** Every iteration meant re-sending the entire ~120-line program, including the ~30-line `DEFS` block and the `smooth`/`capsule` helpers that never changed. Across ten iterations that is a large, entirely avoidable token cost, and it's the single change that would most improve the agent experience. Two possible fixes:

- a `scriptFile` parameter (mirroring `outFile`, and it would compose beautifully with `artwork.js` being the deliverable — I could have edited the file and re-run it);
- or letting `Session` persist *functions*. It can't today, because `eval`/`new Function` are correctly disabled, so helper code cannot be cached across calls even though helper *data* can. This is a reasonable consequence of the sandbox, but it means the scratchpad only solves half the problem it looks like it solves.

`[nit]` **Auto-render of the last-created paper makes diagnostic scripts expensive.** "If a script creates one or more canvases or Snap papers without explicitly returning them, the last created canvas/paper is rendered automatically." A pure probe script that happens to construct a paper pays a full encode + base64 in the response. An explicit `includeBytes: false` works, but the default surprises. Suggest: only auto-render when the script's completion value is undefined *and* it produced no log output, or just document the escape hatch next to the auto-render rule.

`[nit]` **`Object.keys()` on SDK objects reports PascalCase.** `Object.keys(Skia.Image.load(...))` → `["Width","Height","Bitmap"]`, while `img.width` is what actually works (and what the docs use). Harmless once you know, but introspection is exactly what an agent does when it's unsure of an API, and here introspection points away from the documented spelling.

`[nit]` **Serialisation details.** Snap writes colours as `style="fill:#3BA0EA;"` rather than a `fill` attribute and uppercases hex, so `parse → toString` isn't byte-stable. `result.svgXml` is pretty-printed with CRLF and returned in full even when `outSvg` wrote it to disk — that's a few KB of duplicated payload per call.

`[positive]` **Performance was a non-issue throughout.** A 1024² document with ~40 gradient-filled Bézier paths rendered in **4–35 ms**. The heaviest measurement pass (65,536 `getPixel` calls plus a Map histogram) ran in **614 ms**. No timeouts, no statement-limit warnings, nothing close to the 2M-statement or 30s ceilings. I never needed SkSL or `Skia.Shader`, so the shader path is untested here — but the tip in the execution-model docs about preferring native shaders over per-pixel JS loops is sound advice that I'd have followed had the artwork needed it.

`[positive]` **ECMAScript 2025 support is real and complete for my purposes.** Arrow functions, destructuring, template literals, spread, `for…of`, `Map`, `Array.from`, optional chaining, default parameters, computed properties — all worked without a single syntax surprise. Writing modern idiomatic JS and having it just run is worth calling out.

---

## 6. Result

The reproduction matches the reference on palette (sampled, not guessed), silhouette envelope, pose geometry (measured joint positions), and the cape's lit/shadow structure. Known gaps, honestly: the limbs are slightly heavier than the reference, the near knee reads a touch blobby, the far foot is under-separated from the cape shadow, and the hair silhouette is smoother than the reference's lumpier curl mass. Another iteration or two would close those; the run was stopped deliberately at stage 7.

**If I could change three things:** (1) let `attr` pass `url(#…)` through — one guard, unblocks vector gradients entirely; (2) add `scriptFile` to `ExecuteScript`; (3) make `el(name)` honour its name argument.
