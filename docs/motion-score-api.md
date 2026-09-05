# The Motion Score API — implementation plan

**Written 2026-09-05, nothing started.** This is a cold-start spec: everything decided, measured or
found during the session that led to it is recorded here so none of it has to be re-derived.

> **Deadline context.** The Agentic Cinema hackathon closes **14:00 PT 2026-09-09**
> (`docs/agentic-cinema-assessment.md`). What already exists is enough to *make* a short animated
> infographic; this API is what makes a long one maintainable. Build it in the order below and it is
> useful at every stopping point.

---

## 1. The decisions, and why

**Use a score/timeline, not Snap's animation API.** Snap's animation layer is interaction machinery
and three properties disqualify it for a long non-interactive piece:

- **Callback sequencing defeats seeking.** Snap sequences with callbacks (`steam(g, function () {…})`
  in the coffee demo). You cannot know the scene at t = 42 s without executing everything before it,
  which kills preview, resume and parallel rendering.
- **Snap has no notion of *when*.** There is no `at`; its model is "start now". Even the spike had to
  invent `{ at, dur }`, so the spike was already not Snap's API.
- **`.stop()` and `inAnim()` exist because a user can click mid-animation.** There is no user.

**Keep Snap's interpolators, drop Snap's scheduler.** The construction API and `mina`'s nine easings
stay as they are. `src/equal.js`'s interpolation — colour, transform-via-matrix, and path-`d`
morphing — is Snap's genuinely valuable animation contribution and is independent of the timing
model; port it when path morphing is actually wanted, not before.

**GSAP's position paradigm**, because it is what makes a long score editable: relative placement and
labels mean cutting 400 ms from beat 3 moves everything after it automatically, where 200 absolute
numbers would each need hand-editing.

**In C#, for testability first and performance second.** The spike proved the model works as ~20
lines of JavaScript, but JS is not discoverable (`has`/`suggest` cannot see it), cannot be documented
as surface, and cannot be unit-tested. C# gets all three.

**A JS prelude for `mina`**, so the standard binding syntax survives — see §5.

---

## 2. What already exists (do not rebuild)

| | |
| :--- | :--- |
| `SvgRenderPipeline.RenderToImage/RenderToBitmap/SaveImage(..., atTime)` | Renders an SMIL-carrying SVG at a chosen time. Absolute, deterministic, order-independent. |
| `SvgRenderPipeline.HasAnimations(xml)` | Tells an animated document from a still one. |
| `Motion.frame(source, w?, h?)` | Rasterises the current state of a paper/canvas/bitmap and keeps a **copy**. |
| `Motion.save(path, { fps, frameMs, quality, lossless })` | Animated WebP; reports `frames`, `storedFrames`, `merged`, `durationMs`. |
| `Motion.sheet(path, { indices, count, cols, scale, labels, fps, … })` | Contact sheet — the artifact an agent can actually perceive. |
| `Motion.count` / `Motion.clear()` | |
| `MotionToolkit` | `src/Polson.Drawing.Skia/MotionToolkit.cs`, registered as the `Motion` global. |
| `MotionToolkitTests` | 9 tests. |
| The spike | `scratchpad/figure/20_spike.js` — the JS timeline this replaces. |

---

## 3. Measurements already taken — do not re-measure

- **Render is cheap; encode dominates.** 1280×720: **2.7 ms/frame** with 20 animated elements,
  **12.2 ms** with 200; PNG/WebP encode **59–68 ms/frame**. Render scales with element count, barely
  with resolution.
- **SMIL seek is O(1) at 0.43 ms** to any time, via `SKSvg.SetAnimationTime`.
- **Animated WebP random access is O(n).** Measured on a 47-frame file: exactly **one** frame is
  independently decodable; reaching frame 36 costs 13 ms against 1 ms for frame 0. It is a delivery
  container, never an interchange one.
- **SkiaSharp encodes exactly three formats** — PNG, JPEG, WebP. No GIF, no APNG, no AVIF. Animated
  WebP is the only multi-frame option, and it is fine to roughly 15–20 s (≈9.7 KB/frame at 720p).
- **`ffmpeg` is on the agent shell deny list** (`ProjectGenerator.ShellDenies()`), categorised as
  "anything that could draw, encode or post-process outside the engine". Video muxing belongs to a
  **non-agent** step — the orchestrator, or the Cloud Run image — never to a script.

---

## 4. The API

Registered on the existing `Motion` global. All times in **milliseconds**.

```js
const tl = Motion.timeline(options?);
```

`options`: `{ defaults?: { dur?: number, easing?: fn }, labels?: object }`.

### 4.1 Adding to the score

| Call | Purpose |
| :--- | :--- |
| `tl.tween(from, to, setter, opts?)` | A scalar tween whose **setter is called with the eased value**. This is Snap's `Snap.animate(from, to, setter, …)` minus the clock, and it is the case SMIL cannot express — procedural geometry recomputed per frame. |
| `tl.to(target, attrs, opts?)` | Attribute tween on a `SnapElement`. Numbers and colours interpolate. |
| `tl.set(target, attrs, opts?)` | Zero-duration step: the base value before `at`, the given value at and after it. |
| `tl.show(target, opts?)` | Visibility window — `{ from, to }`. Replaces the demo's create-in-callback / remove-in-callback, which cannot survive a backwards seek. |
| `tl.stagger(targets, attrs, opts?)` | One `to` per target, offset by `each` ms. |
| `tl.label(name, at?)` | Names a position. |

`opts`: `{ at?: number \| string, dur?: number, easing?: fn, each?: number }`.

### 4.2 The position grammar (`at`)

This is the GSAP paradigm and the reason the whole thing is maintainable. **Omitted means "at the
end of the timeline"** — sequential append, GSAP's default.

| Form | Meaning |
| :--- | :--- |
| `1200` | absolute, 1200 ms |
| `"+=200"` | 200 ms after the timeline's current end |
| `"-=200"` | 200 ms before it (overlap) |
| `"<"` | at the **start** of the previous tween |
| `">"` | at the **end** of the previous tween |
| `"<+=100"` / `">-=50"` | offset from the previous start / end |
| `"chartIn"` | at that label |
| `"chartIn+=200"` | offset from a label |

An unknown label must **throw and name it**, never silently resolve to 0 — a mistyped label that
quietly places a beat at the start of the film is the exact class of silent failure this project
treats as worst.

### 4.3 Reading and seeking

- `tl.seek(ms)` → the timeline, chainable.
- `tl.duration` → number.
- `tl.labels` → `{ name: ms }`.
- `tl.count` → how many entries.
- `tl.at(ms)` — alias for `seek`, since the spike used it and it reads well in a render loop.

### 4.4 The property that must hold

**Every entry is a pure function of `t`, including outside its own window.**

- before its start → apply the **from** value
- after its end → apply the **to** value (frozen)

That is what makes `seek` absolute and order-independent, so frames can be rendered in any order,
in parallel, or resumed. It is already true of the spike and there is a test for it in
`SvgAnimationTests` at the SMIL layer; the score needs its own.

Two consequences to document loudly:

- **Anything mutated outside the timeline will not revert on a backwards seek.** Either put every
  state change in the score, or rebuild the scene per frame — which the 2.7–12 ms render cost makes
  affordable.
- **`to()` captures the base value when it is added, not when it first runs.** That is the only
  deterministic choice; GSAP captures at first run, which a seekable score cannot do.

**Later entries win** on the same property at the same time — apply in insertion order, and say so.

---

## 5. The `mina` prelude

**The problem, measured.** An easing stored on an object and called through it **throws**:

```js
const t = { ease: mina.elastic };
t.ease(0.5);
// Object type Polson.Drawing.Svg.Mina does not match target type System.Dynamic.ExpandoObject
```

JavaScript binds `this` to the containing object and Jint's interop then refuses the receiver. Every
other position works: `mina.elastic(0.5)`, `const f = mina.elastic; f(0.5)`, and `n => mina.elastic(n)`.
The message names neither easings nor the line responsible.

This will bite **every** user of the score API, because `easing: mina.elastic` is the natural spelling
and it lands inside an options object.

**The fix.** Inject a JS prelude that rebinds `mina` to plain closures:

```js
mina = { linear: n => _m.linear(n), easein: n => _m.easein(n), /* … all nine … */, time: () => _m.time() };
```

**Execute it as a separate `engine.Execute(prelude)` call before the user script** — not by
prepending to the script text. Prepending shifts every reported line number, and the engine's error
messages carry `(line N)`, which is one of the few navigational aids a script author has.

Keep the CLR `Mina` type registered as-is so `JsSurface.Receivers` and the doc tests are unaffected;
the prelude wraps it rather than replacing the registration.

**Test it in all four positions** — direct call, local variable, object property, array element — so
the regression is pinned where it actually failed.

---

## 6. Implementation notes

### 6.1 The one unresolved question — resolve it first

**How a JS function reaches C# on a hot path.** `tl.tween(from, to, setter, …)` calls `setter` once
per tween per frame, so this is the inner loop.

The existing precedent is `LogoDesignToolkit.InvokeCallback(object? callback, params object?[] args)`
— it reflects into Jint internals (`_function`, `_target`, `function` fields) on **every call**. That
is fragile and too slow for this.

Try, in order:

1. Declare the parameter as a **delegate type** (`Action<double>` or `Func<double, object?>`) and let
   Jint marshal the function. Verify `EnumStringTypeConverter` (which extends `DefaultTypeConverter`)
   does not break delegate conversion. If this works it is a direct call and the problem is over.
2. Failing that, accept `object?` and **normalise once at add time** into an `Action<double>`,
   resolving the Jint function then rather than per frame.

Either way the setter must also accept a plain C# delegate, so the timeline can be unit-tested with
no engine at all — which is the stated reason for building it in C#.

### 6.2 Where the code goes

`src/Polson.Drawing.Skia/MotionTimeline.cs`, beside `MotionToolkit`. It needs `SnapElement`
(`Polson.Drawing.Svg`, already referenced) and `SkiaColorParser`. It must **not** reference Jint —
that is what §6.1 is protecting.

`Motion.timeline(...)` returns a `MotionTimeline`. Add `new("tl", typeof(MotionTimeline), "Motion", true)`
to `JsSurface.Receivers`.

### 6.3 Interpolation

- **Numbers** — read `element.Attr(name)`, parse, lerp.
- **Colours** — `SkiaColorParser.Parse` both ends, lerp in RGBA, emit `#RRGGBB`. Cheap and high value;
  a fade or a tint is the second-commonest infographic move after position.
- **Anything else** — refuse with a message naming the attribute and its value, rather than producing
  `NaN` and drawing nothing. A malformed colour is *already* accepted silently elsewhere in the
  toolkit (`ctx.strokeStyle = '#6e6counts'` → `#000000`), and that defect is on the open list; do not
  add a second instance of it.
- **Path `d` morphing** — out of scope. It needs `equal.js`'s `path2array`/`getPath` and equal segment
  structure. Note it as not supported rather than half-supporting it.

### 6.4 Performance

Not the constraint — encode dominates by 5×. Prefer clarity. The one thing worth doing is avoiding
per-frame allocation in `seek`.

---

## 7. Tests

Testability is the reason this is C#, so the test list is part of the spec. All of these run with no
JS engine, passing C# delegates as setters.

**Position grammar** — one test per form in §4.2, plus: an unknown label throws and names it; `"<"`
and `">"` with no previous entry resolve to 0 rather than throwing.

**Purity and seek-safety**
- Seeking ascending and descending produces identical values at every t.
- Re-seeking a visited time reproduces it exactly.
- Before an entry's start its `from` is applied; after its end its `to` holds.
- A timeline with no entries has duration 0 and `seek` is a no-op.

**Composition**
- `duration` is the max of `at + dur`, not the sum.
- Two entries on the same property: the later one wins.
- `stagger` places n entries at `at + i*each` and extends duration correctly.
- `set` is a step function — base before, value at and after.
- `show` hides outside its window and restores inside it, in both seek directions.

**Interpolation**
- A number attribute lerps and lands exactly on `to` at the end.
- A colour attribute lerps channel-wise; the midpoint of `#000000`→`#ffffff` is mid-grey.
- An uninterpolatable attribute throws with the attribute named.

**The mina prelude** — the four positions (§5), through the engine.

**End to end** — rebuild `20_spike.js` on the C# score and assert the same frame count, duration and
determinism the JS version produced.

---

## 8. Guardrails this must satisfy

Found the hard way when `Motion` was added — three test failures before it would build green:

1. **`JsSymbolManifest.RuntimeGlobals`** — every engine global must appear in `JsSurface.Receivers`.
2. **`ApiDocumentationTests.TestEveryPublicMemberIsDocumented`** — every public member must be named
   in `docs/Polson.core.md`, or listed in `JsSurface.Excluded` with a reason.
3. **`ManualCoverageTests`** — every area needs a floor. `Motion` currently sits at **0 with a stated
   reason** ("a spike; a manual would document a decision nobody has made"). **When the score lands,
   that reason expires** — write the manual passage and raise the floor.
4. **Manual examples execute as tests.** Any ```javascript block in `docs/manuals/*.md` is run, so it
   must be self-contained: create its own canvas/paper and end with the thing to render.

---

## 9. Build order

Each step leaves the tree green and useful.

1. **The `mina` prelude + its four tests.** Independent of everything else, and it unblocks the
   natural spelling.
2. **Resolve §6.1**, with a throwaway probe if needed.
3. **`MotionTimeline` with `tween` + position grammar + `seek`/`duration`/`label`.** This alone
   reproduces the spike.
4. **`to` / `set` / `show` / `stagger`**, with number and colour interpolation.
5. **Rebuild the spike on it**, render, and compare against the JS version.
6. **Docs**: `Polson.core.md` Motion section, and a manual passage; raise the `Motion` floor.
7. **`Motion.saveFrames(dir, options?)`** — trivial (the existing encoder writing N files), no new
   dependencies, and it is the seam to ffmpeg for anything longer than ~20 s.

---

## 10. Deliberately not in scope

- **Path `d` morphing** (§6.3).
- **Nested timelines.** GSAP has them; a first score does not need them, and they complicate the
  position grammar. Add only when a real piece wants one.
- **Baking the score to SMIL for a self-contained animated SVG.** Attractive — `values` + `keyTimes`
  is exact, measured — but it loses intent: `values="M150,240 Q…"` × 38 tells the next agent nothing
  where `liquid(v)` with a comment tells it everything. Transfer the **script** plus a stage SVG/PNG
  at chosen times; see the Motion section of `Polson.core.md`.
- **Video encoding in-engine.** §3.
