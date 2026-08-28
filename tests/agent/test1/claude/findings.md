# Polson MCP Server — Agent Findings

Run: 2026-08-27/28. Task: a wooden sailing ship on the open ocean, moonlit night.
Agent: Claude (Opus 5), working only through the `polson` MCP server.

Written as things happened, not reconstructed at the end.

---

## 0. Harness verification

- `Read src/Polson.MCPServer/JsDrawingEngine.cs` → **denied** (correct).
- `Read .mcp.json` → **succeeded** (correct).

Harness is sound. No internal source, tests, or on-disk `docs/*.md` were read at any point.

---

## Summary of defects found

| # | Defect | Severity | Cost to me |
|---|---|---|---|
| F1 | `Assets.material()` returns a Promise; every documented example omits `await` and silently burns budget | **Critical** | 2/12 budget units, 4 round trips |
| F5 | `fillStyle = 'rgba(…,a<1)'` leaks its alpha into the *next* gradient/shader `fillStyle` | **Critical** | ~7 round trips, 2 dead renders |
| F4 | `ctx.colorFilter` is silently ignored by `drawImage` | Moderate | 1 wrong render + 1 diagnostic |
| F2 | `CanvasPath` is documented and constructed in examples but does not exist | Moderate | minor |
| F6 | `Recitation` refusal whose `remedy` text contradicts the actual failure | Moderate | 1 round trip |
| F3 | `Assets.classify()` returns `class` as an undocumented bare integer | Minor | none |
| F7 | `Assets.library.forEach` passes `undefined` as the index | Minor | none |

---

## F1 — `Assets.material()` returns a Promise; every documented example is broken. **(critical)**

`polson://sdk/core/Assets` — the only documentation for requisition — shows this, twice, as
the canonical usage:

```javascript
const oak = Assets.material('weathered ship hull planking, tarred caulking between planks');
if (!oak.success) { error(oak.remedy); exit(oak.failure); }
Session.oakUri = oak.toDataUri();
```

That code **cannot work**. `Assets.material(...)` returns a `Promise`. `String(result)` is
`[object Promise]`, `JSON.stringify(result)` is `{}`, and every documented property —
`.success`, `.failureName`, `.remedy`, `.retryable`, `.bytes`, `.toDataUri()` — is `undefined`.

What makes this expensive rather than merely annoying:

1. **It fails silently, disguised as the documented failure path.** `!oak.success` is
   `!undefined` → `true`, so control enters the error branch. I logged
   `${oak.failureName}: ${oak.remedy}` and got **`undefined: undefined`**. The execution
   envelope still reported `"success": true`.
2. **The budget is charged anyway.** The requisition completes in the background. After my two
   "failed" calls, `Assets.budget.spent` had advanced and `tokensSpent` was 1377. Real money,
   zero pixels. (The generated swatches did land in `Assets.library`, so I recovered one — but
   only because I went looking.)
3. **The word `await` appears nowhere** in `polson://sdk/core/Assets`, in the area listing in
   `polson://sdk/index`, or in the execution-model section. The execution model advertises
   ECMAScript 2025 in detail but never mentions async, Promises, or top-level `await`.

`await Assets.material(...)` works correctly and returns the documented `MaterialAsset`.

**Cost: 2 of 12 budget units and 4 round trips.** The fix is one keyword in two code samples.
A better fix: make the failure legible — returning an object whose `remedy` says *"this call is
asynchronous; you did not await it"* would have cost nothing.

## F5 — `fillStyle = 'rgba(…)'` leaks its alpha into the next gradient/shader. **(critical)**

This is the worst bug I hit, because it is **silent, action-at-a-distance, and survives
`save()`/`restore()` scoping intuitions entirely.**

Assigning a CSS `rgba()` string with alpha < 1 to `ctx.fillStyle` leaves that alpha latched.
The *next* `fillStyle` assignment, **if it is a `CanvasGradient` or a Skia shader**, is drawn
multiplied by the stale alpha. Measured on a clean context each time:

| sequence | resulting pixel |
|---|---|
| gradient fill on a clean context | `#151F2EFF` ✅ |
| `rgba(236,243,255,0.08)` → **gradient** fill | `#1A263314` ❌ alpha 0x14 |
| `rgba(236,243,255,0.08)` → **Skia shader** fill | `#1A263314` ❌ alpha 0x14 |
| `rgba(236,243,255,0.08)` → `#151F2E` hex fill | `#151F2EFF` ✅ |
| `rgba(236,243,255,0.08)` → `rgb(21,31,46)` fill | `#151F2EFF` ✅ |
| `rgba(236,243,255,1.0)` → gradient fill | `#151F2EFF` ✅ |
| `strokeStyle` rgba → gradient fill | `#151F2EFF` ✅ (fill side only) |

So an **opaque colour resets it, but a gradient or shader does not** — a gradient inherits
whatever alpha the last rgba colour happened to leave behind. And `ctx.globalAlpha` still
reports `1`, so nothing in the script can see it.

### How it presented

I drew a starfield (~1000 stars, each `fillStyle = 'rgba(236,243,255,' + a + ')'` with
`a` around 0.02–0.9), and then, five layers later, filled the sea with an opaque
`createLinearGradient`. The entire ocean came out at **alpha 0x15** — inheriting the *last
star's* alpha. Saved to WebP, the transparent half rendered as flat white.

The symptom is maximally misleading: it looks like a blown-out *brightness* problem in a
completely different part of the picture from the actual cause, and the offending line is
hundreds of lines away from the damage. My first two hypotheses (glint overdraw, then
`save`/`restore` not restoring state) were both wrong, and both were reasonable.

### What found it

`bitmap.getPixel()` readback. The value `#18243115` — correct RGB but alpha 0x15 — is what
turned "the sea is too bright" into "the sea is not opaque", which is a completely different
and tractable question. **`getPixel` deserves a mention in the docs as a QA tool**; it is
currently listed as a bare accessor on `SkiaBitmapWrapper` with no hint that it is the way to
verify a render. Without it I would have kept adjusting colours.

Bisecting cost ~7 round trips: instrument every layer → isolate to the stars loop → isolate to
the fillStyle sequence. Everything else I suspected first (`save`/`restore` of `globalAlpha`,
`globalCompositeOperation`, `ctx.filter`; `beginPath` not resetting; clip leakage; overdraw)
tested **clean**, which is worth recording as a positive: those all behave correctly.

**Workaround:** assign an opaque colour immediately before assigning any gradient or shader:
`ctx.fillStyle = '#000'; ctx.fillStyle = myGradient;`. I now do this everywhere, which is
exactly the kind of superstitious code no API should require.

## F4 — `ctx.colorFilter` is silently ignored by `drawImage`. **(moderate)**

`polson://sdk/core/Canvas2D` documents `ctx.colorFilter` as "Color filter (e.g.
`Skia.ColorFilter.colorMatrix(...)`)" with no scope restriction, sitting in the same
**Styling & Shaders** list as `fillStyle` and `filter`. It applies to `fillRect` but **not** to
`drawImage`. Measured with the same 2.55× brightening matrix:

- `drawImage` with no filter → `#575755FF`
- `drawImage` **with** the filter → `#575755FF` — identical, silently ignored
- `fillRect` with the filter → `#A3A3A3FF` — correctly brightened

I was using it to turn a mid-grey lunar-regolith swatch into a bright full moon. The moon came
out dull grey and I initially blamed my own gain values.

Working alternative, which the docs *do* provide: `bitmap.applyColorFilter(filter)` on
`SkiaBitmapWrapper` returns a new filtered bitmap. That works. But nothing points you there
from `ctx.colorFilter`, and nothing warns that the context property has a narrower scope than
its neighbours in the same list.

## F2 — `CanvasPath` is documented but does not exist. **(moderate)**

`polson://sdk/core/Canvas2D` types three methods against it —
`ctx.fill(path?: CanvasPath)`, `ctx.stroke(path?: CanvasPath)`, `ctx.clip(path?: CanvasPath)` —
and `polson://sdk/core/Assets` *constructs* one in its worked example:
`const hull = new CanvasPath();`.

`typeof CanvasPath` is `undefined`. So is `Path2D`. Neither appears in the `Globals:` line of
any area in `polson://sdk/index`, and there is no documented way to obtain one.

Not blocking — `ctx.beginPath()` … `ctx.clip()` works — but it sends you hunting for a
constructor that isn't there, in the one worked example an agent is most likely to copy.

**Related Search limitation.** `Search "CanvasPath"` returned `confidence: "related"`, not
`"no-match"`. The Search tool's description promises that `'no-match'` on a call name is a
**definitive** answer that no such call exists. That guarantee evidently covers *methods* but
not *type names*, so there is no way to definitively disprove a type from inside the harness.
The authoritative signature Search *did* return, `clip(pathOrFillRule?: any, fillRule?: any)`,
is typed `any`, so it hides the mismatch too.

## F6 — A `Recitation` refusal whose remedy contradicts the failure. **(moderate)**

Requisitioning `'coarse woven flax sailcloth, heavy canvas weave, weathered off-white linen,
visible warp and weft threads, faint salt staining'` returned:

```
failureName = Recitation
remedy = "Declined as too close to existing material. The descriptor was too generic —
          add specifics (finish, wear, colour, lighting) rather than retrying the same words."
```

Three problems:

1. **"Too close to existing material" was false.** The only materials in the session were two
   oak-planking swatches. Sailcloth resembles neither. If "existing material" means the
   provider's training corpus rather than my session library, the wording actively misleads —
   `Assets.library` is a documented, session-scoped concept, so "existing material" reads as
   "something already in your library".
2. **"Too generic" was false, and the advice was unfollowable.** The descriptor already carried
   finish, wear, colour and thread structure — all four things the remedy asks for. There was
   nothing left to add along the axis it named.
3. `retryable` was `false`, which was correct and useful.

Rewording to `'heavy duck canvas cloth, tight plain weave, ecru and grey-white, slubbed cotton
threads, faint tide-line staining along one edge, soft matte finish'` succeeded first try. So
the *system* behaved sensibly; only the explanation was wrong. Given that `remedy` is
explicitly sold as "what to do next, in words", a remedy that misdescribes the cause is worse
than a generic one. Good: the refusal **did not** consume budget.

## F3 — `Assets.classify()` returns `class` as a bare integer. **(minor)**

`classify('a wooden sailing ship at night')` → `class=1 allowed=false`;
`classify('weathered oak ship hull planking…')` → `class=0 allowed=true`. The docs describe the
return as `{ class, reason, triggers, allowed }` without saying `class` is numeric or what the
values mean. The same document deliberately provides `failureName` as the readable form of
`failure`; `class` deserves the same treatment.

## F7 — `Assets.library.forEach` passes `undefined` as the index. **(minor)**

`Assets.library.forEach((m, i) => …)` yields `i === undefined` for every element, so
`log(\`[${i}]\`)` prints `[undefined]`. The element argument is fine. `Assets.library` is a
marshalled .NET collection that supports `.length` and index access, so a `for` loop works.
Worth a one-line note that `Assets.library` is array-*like*, not an `Array`.

---

## What worked well (worth keeping)

- **`Assets.classify()` is genuinely well designed.** Free, offline, and the material/form
  boundary was clear on the first read. The refusal reason was specific and actionable —
  *"'ship' names a thing with an outline. Draw the form with the drawing toolkit and requisition
  its surface instead — e.g. the planking, not the ship"* — and the acceptance reason,
  *"Head noun is a material; the object named is an attributive modifier,"* explained the rule
  rather than just the verdict. I tested five descriptors before spending anything.
- **Tiling quality is real, not claimed.** `material.tiling` reported
  `wraps=true, repaired=false` and the swatches genuinely tiled seamlessly when I rendered a
  contact sheet at 1/3 scale. `horizontalSeamStep`/`neighbourMax` being measured values rather
  than assertions is the right design.
- **The requisition prompt is visible** in `provenance.prompt`, including the flattening
  instructions the layer adds ("*FLAT TEXTURE SAMPLE ONLY … no object, no scene, no horizon*").
  Being able to read what was actually sent made the `Recitation` failure diagnosable.
- **`Session` persistence across `ExecuteScript` calls works exactly as documented**, which is
  what makes the requisition-then-draw split practical.
- **`outFile` + reading the file back** is a clean perception loop.
- **The asset cache is durable and visible.** Requisitioned materials are written to
  `.polson/assets/<hash>.png` alongside a `.json` sidecar in the working directory. Nothing in
  `polson://sdk/core/Assets` mentions this — it says only that "results are cached by content" —
  but it is what makes the caching claim real across sessions, and it let me confirm that the
  two un-awaited calls in F1 had genuinely produced images. Worth documenting, both so agents
  know the cache survives restarts and so they know requisition writes to the workspace.
- **Canvas state handling is correct** where I stress-tested it: `save`/`restore` properly
  restores `globalAlpha`, `globalCompositeOperation`, `ctx.filter` and `ctx.colorFilter`;
  `beginPath()` resets the path; clips do not leak. All verified by pixel readback.

---

## Search / documentation experience

- `Search` with a **call name** is excellent — `ctx.clip` returned
  `confidence: "direct"` with the authoritative generated signature. This is the single most
  useful affordance in the toolset.
- `Search` with a **technique** returned relevant manual passages with runnable code
  (`polson://manual/07` cast shadows, `polson://manual/06` perspective, `polson://manual/04`
  atmospheric sky). Ranking was sensible.
- **What I searched for and did not find:** nothing in the corpus covers *water* — no sea
  surface, no wave crests, no specular glitter path, no reflection of a light source on a moving
  surface. Manual 04 has a sky gradient and comic clouds; there is no marine equivalent. Every
  technique for the ocean (perspective row spacing, glint distribution across a moonglade,
  swell crests) I derived myself. That is a legitimate gap rather than a bug — the corpus is
  figure/logo/comics oriented — but the manual index's framing as "classical drawing,
  perspective, lighting, composition" led me to expect landscape/seascape coverage.
- The **`Drawing.*` toolkit is heavily figure-oriented** (Loomis heads, mannequins, facial
  expressions, torso musculature). For a seascape the genuinely applicable calls were
  `createNotanPalette`, `createCompositionGrid`/`drawCompositionGrid`, `drawVignette`,
  `createAtmosphericCloudShader`, `drawRimLight`, and the Skia shader/filter layer. That is a
  reasonable subset; I mention it only because `polson://sdk/index` presents `Drawing` as a
  general "constructive drawing engine".

---

## F8 — No seeded RNG anywhere in the SDK. **(gap, not a bug)**

This studio's whole premise is generative art that agents iterate on and hand to each other.
Every scatter in this picture — 1100 stars, ~6000 glade glints, 2600 chop dashes, 300 foam
specks — needs randomness, and it needs to be *the same randomness* next run, or "adjust the
sail tone and re-render" silently reshuffles the entire sea and you can no longer tell what
your edit did.

I hand-rolled a 6-line LCG. That is fine, but it means:

- Every agent will hand-roll a different one, so no two agents' scenes are reproducible
  from each other's scripts.
- `mina.time()` exists (wall clock, non-deterministic) but there is no `mina.seed()` or
  `Skia.random(seed)`. The Skia noise shaders *do* take a `seed` argument, which shows the
  design intent is there — it just stops at the shader boundary.

A `Random(seed)` global returning a deterministic stream would cost almost nothing and would
make stigmergic hand-off actually work. Worth noting that `Session` already gives you
cross-script persistence, so the pieces for reproducible multi-pass work are otherwise present.

## F9 — `polson://sdk/symbols` is described as the authority but is hard to use as one.

`ListMcpResources` advertises it as: *"an absent name here means the call genuinely does not
exist"* — exactly what I needed for `CanvasPath`. But it is a single machine-readable JSON blob
covering the entire surface (~300 callables), so reading it to check one name means pulling the
whole thing into context. `Search` with a call name is the practical substitute and works well
for methods — but as noted in F2 it does not give the same definitive answer for *type* names.
A `polson://sdk/symbols/{Area}` slice, matching how `core` and `schema` are already sliced,
would close this.

---

## What I hand-rolled that the SDK already provided

Honest answer: **almost nothing**, because I checked first. Two near-misses:

- I nearly wrote my own vignette before finding `Drawing.drawVignette(ctx, w, h, opts)`, which
  is in the final picture and works exactly as documented.
- I nearly hand-placed the waterline foam by eyeballing coordinates, then realised
  `Snap.path.getTotalLength(d)` / `Snap.path.getPointAtLength(d, len)` let me scatter foam
  along the hull's *measured* wetted edge. That is in the final script and is the cleanest
  thing in it — a genuinely good API used for something it wasn't obviously designed for.

Things I hand-rolled that the SDK genuinely does not have (all confirmed by search first):
seeded RNG (F8), perspective row spacing for a water plane, moonglade glint distribution,
square-sail geometry, hull sheer curves, rigging layout, wake and bow wave.

I also called `Drawing.createNotanPalette('lowKey')` during blockout — it returned a sensible
`{background, formDark, formMid, rimAccent}` — but ended up not using it. Its four tiers are
pitched at figure work on a light ground; a night seascape needs a longer, cooler ramp than
four steps.

---

## Time and iterations

Roughly 30 `ExecuteScript` calls end to end. Where the time actually went:

| Phase | Calls | Notes |
|---|---|---|
| Reading docs, `classify` probing | 4 | smooth |
| Getting requisition to work at all | 4 | **all four wasted on F1 (missing `await`)** |
| Requisitioning 3 materials + contact sheet | 4 | one `Recitation` refusal (F6), reworded, fine |
| **Debugging the transparent sea (F5)** | **7** | the single largest time sink in the run |
| Composition blockout + environment | 5 | 4 visual iterations, each an improvement |
| Ship construction | 4 | sails wrong twice (too round, then too tall) — my error, not the SDK's |
| Final composite + refinement | 4 | |

**About a third of the run was spent on two documentation/implementation defects (F1, F5).**
Neither was a hard problem once identified; both were hard to *identify* because both fail
silently. Time spent on the actual picture — composition, values, ship geometry — was around
13 calls, and every one of those moved it forward.

First correct requisition call: **4 attempts.** First correct render of the environment:
**3 attempts** (2 of those lost to F5).

## Notes on the picture

1440×860. Ship, sea, sky, rigging and light are all constructed in code. The three requisitioned
materials contribute surface only — lunar regolith inside the moon's disc, canvas weave in
`overlay` at 0.26 on the sails, oak planking in `overlay` at 0.50 inside the hull clip. Remove
all three and the composition, forms and lighting are unchanged; that felt like the right side
of the line the task draws.

`Assets.budget`: 4 of 12 units spent (2 of them wasted on F1), 1 cache hit, 3 materials kept.

`artwork.js` was verified to reproduce `output.webp` exactly — both render to 76,852 bytes at
`quality: 92`, from the same seed.

Staged renders are in `artifacts/`: `stage0` swatch contact sheet, `stage1` notan blockout with
the composition armature, `stage2b`–`stage2e` the environment iterations (2b and the earlier
`stage2` show the F5 transparency failure), `stage3`/`stage3b` ship shape studies, `stage4`–`stage6`
the composites, plus the four `diag-*` scripts from the F4/F5 hunt.

---

# Maintainer note — 2026-08-28

*Added after the run by the SDK maintainer, not by the agent. Everything above is left exactly as
written; this section records what was done about it and one consequence for the artifacts.*

## Disposition

All seven defects were reproduced against the source and fixed or documented. Two were worse than
reported:

- **F4 was not specific to `colorFilter`.** All three `drawImage` overloads — and both `drawSvg`
  branches — passed a `null` paint, so `globalAlpha`, `globalCompositeOperation`, `filter` and the
  shadow properties were dropped as well. `colorFilter` was simply the face of it you happened to
  hit. Fixed by giving image and SVG compositing a shared paint that honours all of the above and
  deliberately excludes `fillStyle`.
- **F5 sat next to a second alpha bug.** `globalAlpha` was baked into the gradient's colour stops
  *and* applied to the paint colour, so a gradient drawn at `globalAlpha = 0.5` rendered at 0.25.
  You would only have seen it by drawing a gradient at reduced alpha. One change fixes both: with
  any shader attached the paint's base colour now carries `globalAlpha` alone. The same latch
  existed on the **stroke** side, which your table did not cover.

F2 also uncovered a third defect it takes with it: `CanvasPath` was missing because it was never
registered, but the documented `new Canvas(w, h)` alias was registered as a Jint `ClrFunction`,
which carries no `[[Construct]]` — so `new Canvas(...)` threw `Canvas is not a constructor` too.
Nothing tested it. Both are now constructible, and `Path2D` is accepted as an alias of `CanvasPath`.

Your two suggestions were both taken: `getPixel` is worth calling out as the way to verify a
render, and `classify()` now returns `className` alongside the numeric `class`, mirroring
`failureName`. `Assets.library` is documented as array-like; `Array.from(Assets.library)` was
tested, and works.

## Consequence for the artifacts in this folder

**`artwork.js` no longer reproduces `output.webp`, and both are being kept as they are.**

Re-rendering the script against the fixed engine yields ~54.2 KB rather than 76,852 bytes. This is
not a regression — it is the fixes taking effect on a script written against the broken behaviour:

- The moon is drawn under `globalAlpha = 0.58`, which `drawImage` used to ignore. It now composites
  at the strength the script actually asked for, so the moon reads softer and flatter.
- Gradients drawn under a reduced `globalAlpha` are no longer double-darkened.
- The sail and hull textures are unaffected: they go through `Skia.Shader.bitmap` + `fillRect`,
  which always honoured canvas state.

The difference is not an artifact of which oak swatch is loaded from `.polson/assets` — swapping
between the two moves the output by about 100 bytes.

`output.webp` is the image this run actually produced, and `findings.md` is the record of what the
agent experienced; re-rendering either would erase the evidence the harness exists to collect. They
are therefore preserved as a snapshot of the pre-fix engine. Two things follow for anyone reading
them later:

1. The claim above that "`artwork.js` was verified to reproduce `output.webp` exactly" was true
   when written and is no longer true against current `main`.
2. The `setPaint` / `setStroke` helpers in `artwork.js`, and its `applyColorFilter` workaround for
   the moon, are now inert — harmless, but no longer necessary. They are left in place because they
   are part of the evidence: they are what working around F5 and F4 actually cost.

Regression tests covering all of the above are in `tests/Polson.Tests.Drawing/CanvasPaintAlphaTests.cs`,
`tests/Polson.Tests.Drawing/CanvasImageCompositingTests.cs`, and
`tests/Polson.Tests.MCPServer/CanvasPathGlobalTests.cs`. Each was checked against the pre-fix build
to confirm it fails there.
