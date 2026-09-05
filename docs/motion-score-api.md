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

**`mina` easings are delegates**, so the standard binding syntax survives — see §5. (This was
originally planned as a JS prelude; the prelude was measured, found to cost the strict-member
surface, and rejected.)

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
| ~~The spike~~ | `scratchpad/figure/20_spike.js` — **gone.** It lived in a session scratch directory that no longer exists, so build-order step 5 has no original to compare against. The model itself survives in the Motion `[!TIP]` of `docs/Polson.core.md`, which is the same twenty lines; step 5 becomes "build a score on it and assert determinism" rather than a regression check. |

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

## 5. The `mina` fix — DONE 2026-09-05, and not by a prelude

**The problem, measured.** An easing stored on an object and called through it **throws**:

```js
const t = { ease: mina.elastic };
t.ease(0.5);
// Object type Polson.Drawing.Svg.Mina does not match target type System.Dynamic.ExpandoObject
```

JavaScript binds `this` to the containing object and Jint's interop then refuses the receiver. Every
other position works: `mina.elastic(0.5)`, `const f = mina.elastic; f(0.5)`, and `n => mina.elastic(n)`.
The message names neither easings nor the line responsible.

> [!IMPORTANT]
> **Corrected 2026-09-05.** This section previously said the bug *"will bite every user of the score
> API, because `easing: mina.elastic` is the natural spelling and it lands inside an options object."*
> **That is wrong, and it was the stated reason for doing the prelude first.** Measured across all
> four positions plus the bind: passing `mina.elastic` as an **argument** works everywhere — direct,
> from a local, **read off an object property** (`o.e`), and from an array element — and it binds
> correctly into a typed options class's `Func<double, double>` property (§6.1). Only **calling**
> through an object throws: `o.e(0.5)`, never `f(o.e)`.
>
> So the score API's `easing:` option is safe without the prelude, and the prelude is not a blocker
> for anything below it. What the prelude actually fixes is **user-written JavaScript that calls an
> easing through an object it built** — the hand-rolled timeline pattern in `Polson.core.md`'s Motion
> section, whose published example already has to work around this with `ease: n => ease(n)` and a
> `[!CAUTION]` block explaining why. That is still worth removing, and it is still cheap and
> independent; it is just not load-bearing for the timeline.

### 5.1 The prelude was measured and rejected

**The prelude works and costs the strict-member surface.** Rebinding `mina` to plain closures makes
it an `ExpandoObject`, and `MemberIndex.Has` answers **true** for any name on one — the deliberate
"honest maybe" it gives collections, which is the wrong answer here. Measured through the real
engine, before and after:

| Asked | Today | After a prelude |
| :--- | :--- | :--- |
| `has(mina, 'elastic')` | `true` | `true` |
| `has(mina, 'nonsense')` | **`false`** | **`true`** |
| `suggest(mina, 'nonsense')` | *"'Mina' has no property or method 'nonsense'… Read polson://sdk/index"* | ***"'nonsense' exists on ExpandoObject — nothing to correct."*** |
| `{ e: mina.elastic }.e(0.5)` | **throws** | `1.015625` |

The `suggest` answer is the disqualifying one. It is not merely less useful — it is **wrong advice**,
about a misspelling, naming a type the script author has never heard of. That is the same defect
`EnumStringTypeConverter` exists to remove, reintroduced one object over.

### 5.2 What was done instead

**Every easing is a delegate-valued property rather than a method** — `public Func<double, double>
Elastic { get; } = ElasticCore;` — so it carries its own target and `this` never enters into it. The
maths moved to private statics, unchanged apart from `float` → `double`, which is what JS numbers
are anyway.

Measured after the change: all four positions work, plus passing an easing into a JS function that
did not create it; `has`/`suggest`/probe recording are untouched because `mina` is still the CLR
type; no second global, so `JsSymbolManifest.RuntimeGlobals` is unaffected; and the doc tests pass
because `HasMember` accepts a property where a call is documented.

**No prelude, so none of its costs apply** — no line-number shift, nothing executed before the user
script, nothing to keep in step with `Mina`.

`MinaInteropTests` pins all four positions, every published easing through an object property, the
values themselves against Snap's definitions, and the strict-member surface that the prelude would
have cost. `Mina`'s `<remarks>` says why the members are shaped the way they are, because turning
them back into methods reads like tidying and silently reintroduces the bug.

`docs/Polson.core.md`'s Motion section carried a `[!CAUTION]` block teaching the workaround; it is now
a `[!NOTE]` recording that the position works and that it used to throw.

---

## 6. Implementation notes

### 6.1 RESOLVED 2026-09-05 — declare the delegate type

**How a JS function reaches C# on a hot path.** `tl.tween(from, to, setter, …)` calls `setter` once
per tween per frame, so this is the inner loop.

The existing precedent is `LogoDesignToolkit.InvokeCallback(object? callback, params object?[] args)`
— it reflects into Jint internals (`_function`, `_target`, `function` fields) on **every call**. That
is fragile and too slow for this.

**Option 1 works, so none of that is needed.** Measured with a throwaway probe against an engine
carrying the options that matter (`EnumStringTypeConverter`, `ThrowOnUnresolvedMember`, `Strict`),
Jint 4.9.2:

| Probe | Result |
| :--- | :--- |
| JS arrow → `Action<double>` parameter | **binds**, values correct |
| The delegate held in a field and called **after `engine.Execute` returned** | **works** — 2 later calls, correct values |
| Cost per call, 100,000 calls | **1.5 µs** (0.15 ms per 100 setters) |
| Cost per *conversion*, 2,000 in one script | **17.8 µs** — 12× a call |
| `mina.elastic` → `Func<double, double>` parameter | **binds**, returns 1.015625 at n=0.5 |
| A JS **object literal** → a C# class with settable properties | **binds**, including a `Func<double, double>` easing |
| A non-function (`42`, `'easein'`) in a delegate parameter | throws — see the caveat below |
| A JS `throw` inside the setter | surfaces as `JavaScriptException` carrying the message |

So: **declare `Action<double>` and `Func<double, double>` and let Jint marshal.** It is a direct
delegate call, it needs no reflection, it survives being stored, and the setter still accepts a plain
C# delegate — which is what lets the timeline be unit-tested with no engine at all.

Three consequences that shape the API:

- **Convert at add time, never per frame.** Conversion costs 12× a call, so a delegate parameter on
  `seek` or on any per-frame path would pay it every frame. `tween`/`to` bind once; `seek` takes no
  functions.
- **Options must be a typed class, not a dictionary.** A function inside an options object read
  through `JsInterop.AsDict(...)` arrives as a raw `Func<JsValue, JsValue[], JsValue>` — a **Jint
  type**, which §6.2 forbids `MotionTimeline` from naming. Declaring the options parameter as a class
  with settable properties makes Jint do the conversion at the boundary instead, and `easing`
  arrives as a `Func<double, double>`. This is why `MotionToolkit`'s existing `object? options` +
  `AsDict` pattern must **not** be copied for the timeline.
  - It must be a **class with `set` properties**. A positional `record` does **not** bind —
    *"No public methods with the specified arguments were found."*
  - Unknown properties are ignored silently (`{ nonsense: 1 }` binds fine), and missing ones are
    null. So a misspelled option is quiet — the same defect class as a misspelled *read*, and the
    reason `tween`/`to` should validate what they got rather than trusting the bind.
- **`null` binds happily to a delegate parameter.** `probe.hold(null)` is accepted and holds null,
  so `tl.tween(0, 1, null)` would add an entry that does nothing at every frame. Reject a null
  setter explicitly, naming the argument.

**One defect noted, not fixed here.** A non-function in a delegate parameter fails with Jint's
overload-resolution message — *"No public methods with the specified arguments were found."* — which
names neither the parameter, nor the value, nor what was expected. It is loud, which is the important
half, but it is not guidance. Worth an entry on the open list rather than a fix inside the timeline.

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

**Setter marshalling** — the §6.1 contract, through the **real** engine once `Motion.timeline()`
exists (a throwaway probe had to replicate the engine options to answer it, which is exactly the
weakness these tests remove). Pin: a JS arrow reaches an `Action<double>`; the delegate still works
after the execution that created it; `mina.elastic` binds to a `Func<double, double>` option on a
typed options class; a null setter is refused by name. Without these a Jint upgrade breaks the whole
timeline with an unrelated-looking message.

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

1. ~~**The `mina` prelude + its four tests.**~~ — **done 2026-09-05**, see §5. Fixed by making the
   easings delegate-valued properties; the prelude itself was measured, found to cost the
   strict-member surface, and rejected.
2. ~~**Resolve §6.1**~~ — **done 2026-09-05**, see §6.1. Declare the delegate type; convert at add
   time; options must be a typed class rather than a dictionary.
3. ~~**`MotionTimeline` with `tween` + position grammar + `seek`/`duration`/`label`.**~~ —
   **done 2026-09-05.** `src/Polson.Drawing.Skia/MotionTimeline.cs`, registered as `tl`.
4. ~~**`to` / `set` / `show` / `stagger`**, with number and colour interpolation.~~ — **done
   2026-09-05.** 38 tests in `tests/Polson.Tests.Drawing/MotionTimelineTests.cs`, all engine-free.
5. **Rebuild the spike on it**, render, and compare — ~~against the JS version~~ **against itself**:
   the spike file is gone (§2), so this is "build a score, render it, assert determinism" rather
   than a regression check. The documented example in `Polson.core.md` has been run through the real
   engine by hand and produces a contact sheet; **it is not yet a test**, see below.
6. **Docs**: ~~`Polson.core.md` Motion section~~ **done** — a Score section with the position
   grammar, the purity note and a runnable example. **Still to do:** a manual passage, and raise the
   `Motion` floor in `ManualCoverageTests` (still 0, and its stated reason — "the authoring model
   above it is still an open question" — has now expired).
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

---

## 11. A GSAP-compatible surface — assessed 2026-09-05, deferred

**Decision: build the native API first (§4), then consider a GSAP-shaped layer over it.** The
assessment below is recorded so it does not have to be redone.

**Why it is worth wanting.** GSAP is the largest corpus of animation-scoring code on the web, and a
model writing GSAP-shaped script from its own weights is the Extended Mind thesis paying out
directly. §1 already borrowed the position paradigm from it.

**What transfers unchanged:** the position grammar (already adopted), `to` / `from` / `fromTo` /
`set`, labels, `stagger`, `delay`, chaining, and `repeat` / `yoyo` — the last two are pure functions
of `t`, so they survive a seek model. `attr: {}` maps onto `element.attr()`. `x` / `y` / `rotation` /
`scale` / `transformOrigin` compose into a `SnapMatrix`.

**What cannot, and must refuse loudly:** anything driven by a clock or a user — `play` / `pause` /
`reverse` / `timeScale`, ScrollTrigger, Draggable — and the `onUpdate` / `onComplete` callbacks,
which are ill-defined under a backwards seek. A snippet that silently drops its `scrollTrigger` and
animates anyway is the worst available outcome.

### 11.1 The measurement that decides the shape

A GSAP `vars` object mixes animatable properties with config. Measured through the marshalling
boundary:

| `{ … }` written in JS | arrives in C# as |
| :--- | :--- |
| `x: 100`, `duration: 1` | `Double` |
| `attr: { cx: 100, r: 20 }` | `ExpandoObject` — nested reads fine |
| `ease: 'power2.out'` | **`String`** |
| `ease: mina.elastic` | `Func<JsValue, JsValue[], JsValue>` — **a Jint type** |
| `ease: n => n * n` | same — **a Jint type** |

**Jint re-wraps a delegate that is a property of a JS object**, so even `mina.elastic` — a genuine
`Func<double, double>` on the CLR side since the §5 fix — is unreadable from a vars dictionary
without naming Jint types, which §6.2 forbids.

**So a single vars object is viable if and only if eases are strings** — which is GSAP's own idiom.
Adopting the convention removes the problem rather than creating it. This is also why §4's split
signature (`tl.to(target, attrs, opts)`) is right for the native API: `opts` is a **typed class**, so
it can carry a real `Func<double, double>`, which a vars dictionary never can.

### 11.2 The trap to avoid when it is built

**Do not map GSAP ease names onto `mina`.** `mina`'s eight curves are sine- and quadratic-based;
GSAP's `power1`–`power4` are `t^n`, and `back` / `elastic` / `bounce` / `circ` / `expo` / `steps` are
different curves again. `power2.out` → `mina.easeout` produces animation that runs, looks plausible
and is wrong, with no error and no signal. The families are closed-form and cheap — implement them
properly, and leave `mina` alone for Snap compatibility.

The same care applies to GSAP's **defaults** — duration, default ease, the `back` and `elastic`
overshoot constants, `transformOrigin`. Verify each against GSAP's own documentation at
implementation time; that is where silent divergence lives.

**Units are the other silent 1000×.** GSAP is in seconds and §4 is in milliseconds, so a pasted
`duration: 1` read as 1 ms collapses a beat into a single frame and presents as "nothing animates".
Any compatibility layer must convert at its own boundary and say so.

**Provenance.** Implement from documented behaviour. Do not pull GSAP source into `reference/` and
distil it without a ledger row and a reading of its licence terms first — the same rule the book rows
follow.
