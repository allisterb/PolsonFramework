# Polson JavaScript SDK — Core Reference

This is the **core** reference for the Polson JavaScript (JS) SDK — the typed drawing, graphics, and vector API exposed to scripts executed inside the Polson MCP server's sandboxed JavaScript engine. It covers the **execution model** (the rules every generated script must follow) and the **method signature index** for all top-level objects: each method's purpose, parameter types, and return values.

The **JSON schema for every parameter and return model type** named below lives in the companion schema document (`Polson.schema.md`). Both documents are served **sliced by subject area**: read `polson://sdk/index` for the map, `polson://sdk/core/{Area}` for an area's methods, and `polson://sdk/schema/{Area}` for the fields of what they return. This entire document is also available at `polson://sdk/core/all`.

---

## Execution model

Scripts execute within a secure, sandboxed [Jint](https://github.com/sebastianros/jint) runtime supporting **ECMAScript 2025** (arrow functions, `let`/`const`, destructuring, template literals, optional chaining `?.`, nullish coalescing `??`, `for...of`, spread `...`, `Array`/`Map`/`Set`/`JSON`, etc.). Tailor generated code to modern JavaScript idioms.

- **Async & `await`:** `async`/`await`, Promises and **top-level `await`** are supported. Almost the entire SDK is synchronous — the exceptions are the three `Assets.*` requisition calls, which reach a cloud service and **must be awaited**. An unawaited Promise silently reports every property as `undefined`; see the warning under `polson://sdk/core/Assets`.
- **Sandbox Security:** `eval` and `new Function` are strictly disabled (`Host.StringCompilationAllowed = false`). Arbitrary external types and reflection are prohibited. Scripts can only interact with the explicit Polson drawing APIs.
- **A misspelled member fails loudly when you *use* it, and reads as `undefined` when you *ask* about it.** Scripts run in **strict mode**. Writing a member that does not exist **throws**, naming the nearest real one — `ctx.fillStlye = '#f00'` fails with *"Did you mean 'fillStyle'?"* rather than silently doing nothing — and so does **calling** one. Assigning to a real but read-only member (`canvas.width`) says it is read-only. A typo'd **variable** is likewise an error rather than a new global.

  **Reading a member that is not there gives `undefined`**, so the ordinary JavaScript checks work:

  ```javascript
  if (typeof ctx.drawLoomisWireframe === 'function') { /* … */ }
  const grid = ctx.drawPerspectiveGrid ?? null;
  ```

  > [!IMPORTANT]
  > **When you get `undefined`, ask why before guessing.** `suggest(object, 'name')` returns the same advice a failed call gives, and it searches the whole surface — so a name carried in from another library gets pointed at the real one:
  >
  > ```javascript
  > if (typeof ctx.drawLoomisWireframe === 'undefined') log(suggest(ctx, 'drawLoomisWireframe'));
  > // → Did you mean 'drawCompositionGrid', 'drawPerspectiveGrid', 'drawTorsoMusculature'? …
  > ```
  >
  > Two consequences follow, and both are worth knowing:
  >
  > - **A misspelled read is silent.** `ctx.lineWidht * 2` is `NaN`, not an error. If a value is unexpectedly `NaN`, `undefined` or a blank string, suspect the spelling and run `suggest` on it. Every such read is recorded in the run as an `absent` probe, so `polson report` shows them even when the script did not notice.
  > - **`'name' in obj` reports every name as present** on an SDK object, and `Object.getOwnPropertyDescriptor` returns a descriptor for every name. Use `typeof`, `=== undefined`, or `has(object, 'name')`.

  <details>
  <summary>Why <code>in</code> behaves that way</summary>

  Not a defect awaiting a fix. It is the price of `typeof` working at all, and the two cannot both be had.

  **An SDK object is not a plain JavaScript object.** A plain one is open: reading a property that was never set is legal and yields `undefined`, and the language keeps *absent* and *present-but-`undefined`* as different states — `in` and `hasOwnProperty` exist precisely to tell them apart.

  ```javascript
  const a = { x: undefined };
  'x' in a;        // true — present, holding undefined
  typeof a.x;      // "undefined"
  ```

  An SDK object is a view onto something typed and closed, whose members are fixed when it is built. Asking it for a member it does not have is an error rather than a value, and it has no way to express *"absent, so here is `undefined`"* — that state does not exist on its side of the boundary.

  The boundary offers exactly one hook, and it is a **value provider**: asked "what is this member's value?", it can decline — in which case the strict behaviour applies and an unknown member throws — or it can answer with a value. There is no third answer meaning *absent*.

  So to give JavaScript back its `undefined`, the engine answers `undefined`; and answering with a value asserts that the property **exists** and holds `undefined`. Every SDK object therefore behaves like `a` above for any name you ask about. `typeof` reads the value and is right; `in` asks about existence and is told exactly what the engine now states.

  **No incorrect drawing results from this.** Every route that could change the artifact still refuses and explains itself — calling a member that is not there throws with *"Did you mean…?"*, and so does assigning to one. Acting on `in`'s answer costs an execution and returns a suggestion, which is what the stricter behaviour cost anyway. Only a *read* is quiet, and reads are recorded.
  </details>

  > [!NOTE]
  > This applies to SDK objects only. Plain JavaScript objects, arrays, `Map` and the `Session` scratchpad keep ordinary JS semantics, so `Session.neverSet` is still `undefined` — and `in` is trustworthy on them.
  >
  > One consequence worth knowing: `?.` and `typeof` do **not** make a *missing member* safe on an SDK object — `ctx.someFutureThing?.x` throws, because the failure is the unknown member rather than a null value. Optional chaining still works for values that may legitimately be null, which is what it is for here: `result.bounds?.width` is fine, because `bounds` exists and is documented as sometimes null.
- **Execution Limits:** Scripts are enforced with statement limits (2,000,000 statements, configurable via `JsDrawingEngine.MaxStatements`), recursion depth limits (100 frames), and execution timeouts ({{SCRIPT_TIMEOUT_SECONDS}} seconds).
  > [!TIP]
  > For heavy pixel-level manipulation (such as procedural textures, blurs, or color grading), use native **`Skia.Shader`** or **`Skia.ImageFilter`** pipelines which execute in native SIMD/C++ in < 1ms, rather than running millions of raw per-pixel loop iterations in interpreted JS.
  >
  > **The cap is reached sooner than it sounds, and the commonest way to reach it is *measuring* rather than drawing** — a per-pixel loop asking a question about the render. It dies at a few hundred thousand pixels, which is a fraction of one frame, and it takes the whole script with it. Use **`bitmap.diff`**, **`bitmap.rowProfile`** and **`bitmap.palette`**, which answer in one native call whatever the resolution; see *Measuring an Image* under `polson://sdk/core/Skia` and `polson://manual/15`.
- **Return Value & Visual Rendering:**
  - Returning a `SnapPaper` (or a `SnapElement`), `CanvasRenderingContext2D`, `SkiaCanvas`, `SkiaBitmapWrapper`, or `ImageData` automatically renders the visual output headlessly to image bytes (`result.ImageBytes`, defaulting to **WebP at quality=85**, with `"png"` and `"jpeg"` options available) and Base64 URI (`result.ImageDataUri`).
  > [!CAUTION]
  > **Do not ask for `includeBytes: true`. Use `outFile` and open the file.** Inlined bytes are delivered as **base64 text in your context window** — not as an image. Base64 inflates the image by a third, and a routine 1200 × 760 WebP arrives as **~126,000 characters**, tens of thousands of tokens; the same frame as PNG is ~458,000 characters, which is larger than many context windows on its own.
  >
  > It is not an MCP image content block, so it costs the window **without necessarily being viewable at all**. The reliable way to *see* a render is to write it with `outFile` and open that path with your host's file or image reader — which is cheaper and actually shows you the picture. `outFile` already sets `includeBytes` to `false` for you; leave it that way.
  >
  > Every inlined render is recorded as a `script.bytesInlined` event carrying the byte and character counts, so a run that spent its context this way says so afterwards.

  - **Direct-to-Disk Rendering (`outFile`, `outSvg`):** Agents can pass `outFile` (e.g. `'artifacts/stage1.webp'`) to write the rendered image directly to disk, and `outSvg` (e.g. `'artifacts/stage1.svg'`) for vector markup. **Both are relative to the project directory, and a path resolving outside it is refused** — an absolute path or a `..` traversal fails with a message naming the project root rather than writing somewhere unexpected. Missing intermediate directories are created for you. When `outFile` is supplied, `result.ImageFilePath` contains the saved path and `result.ImageBytes` is omitted by default to eliminate token bloat in LLM contexts (use `includeBytes: true` to force inclusion).
    > [!IMPORTANT]
    > **`outSvg` needs a vector document to write.** It saves `result.SvgXml`, which only exists when the script built a `SnapPaper`. A script that draws entirely on a raster canvas has no markup to save, so `outSvg` writes **no file** and the run still reports success — the response carries a `[WARN] outSvg … wrote nothing` line, but by then the stage is drawn. **If the brief asks for an SVG, build the scene on `Snap(width, height)` from the first script.** Read `polson://manual/14` before choosing the surface.
  - For vector scenes (`SnapPaper` / `SnapElement`), `result.SvgXml` contains the serialized SVG XML markup. For 2D canvas raster scripts, `result.SvgXml` retains the last vector image produced by the agent prior to switching to 2D canvas mode.
  - If a script creates one or more canvases or Snap papers without explicitly returning them, the last created canvas/paper is rendered automatically.
- **Measuring without rendering (`render: false`):** Suppresses the rasterise-and-encode step for a script whose picture nobody will look at — a probe that samples pixels, a pass that diffs against an earlier stage, a script that stashes a canvas in `Session` for the next call. The script runs normally and its logs, measurements and `Session` writes all survive; only the image is not produced.
  > [!IMPORTANT]
  > **Without this there is no way to draw and not encode.** A canvas is rendered whenever the script created one, *even when the script returns something else* — so returning `'measured'` from a probe does not avoid it, and neither does `exit(...)`. Every measurement pass was therefore paying a full render for an image nothing read: roughly **150 ms at 1600 × 1200**, on exactly the scripts an agent runs most often.
  >
  > `render: false` with `outFile` is **refused** rather than reconciled, since `outFile` asks for the render `render: false` suppresses. `outSvg` is unaffected — vector markup is serialized, not rasterized, so a measurement pass can still save its `d` data.
- **Running a file instead of re-sending it (`scriptFile`):** Pass `scriptFile: 'artwork.js'` — a path relative to the project directory, contained exactly as `outFile` is — to execute a file you maintain rather than putting the whole program in the call. Give **either** `script` or `scriptFile`; passing both is refused rather than resolved, because guessing which one you meant would silently run code you did not intend.
  > [!TIP]
  > **This is the difference between editing a drawing and retyping it.** On a measured four-agent run, 850 KB of JavaScript went over the wire in 55 calls; the twenty largest took a mean of **three minutes each to emit**, against a median engine time of **45 ms** — and consecutive large scripts shared **71%** of their lines. Nearly all of that was re-sending a program in order to change part of it.
  >
  > So once a piece is more than a screenful, keep it in a file: write `artwork.js` with your ordinary editor, change the layer you are working on, and run `ExecuteScript(scriptFile: 'artwork.js', outFile: 'artifacts/stage3.webp')`. The record is unaffected — the server still copies **what actually ran** into `scripts/`, so a later edit to the file never rewrites the history of an earlier execution.
- **Logging & Output:** Output via `console.log(...)`, `log(...)`, `error(...)`, or `table(...)`.
- **Early Termination:** Use `exit(message)` to terminate execution immediately and cleanly return the specified message without a failure status.

---

## Image Formats & Encoding Quality Trade-offs

`ExecuteScript` and `RenderSvg` take an optional `format` (`"webp"`, `"png"`, `"jpeg"`) and `quality` (`1`–`100`, default `85`).

> [!IMPORTANT]
> **Encoding costs several times what the script does, and that is the number worth acting on.** `result.executionTimeMs` is the **script** alone; `result.encodeTimeMs` is turning what it returned into pixels — rasterising a paper where there is one, then encoding. Measured at 1200 × 760: **3 ms of script against 40 ms of encode** on a flat graphic, 19 against 53 on a gradient, 73 against 317 on a Perlin field. A slow call is almost never a slow script, so reaching for a cheaper algorithm will not help.
>
> **The lever is resolution, not format.** Encode time tracks pixel count, and the spread between formats is far smaller than the spread between sizes: a 1600 × 1200 frame costs 124–160 ms whatever you choose, and drafting the same scene at 800 × 600 costs about a quarter of that. Draft small, render the final large.
>
> Both timings are recorded per execution as `ms` and `encodeMs`.

### What it actually costs

Measured, one machine, 1200 × 760, median of nine runs. **Read down the column that matches your content, not across the table** — the ranking inverts between rows, which is the whole point.

| | flat graphic<br><sub>line art, logos, guides</sub> | gradient + alpha<br><sub>skies, shading, glazes</sub> | procedural noise<br><sub>grain, vapour, texture</sub> |
| :--- | :--- | :--- | :--- |
| *script alone* | *3 ms* | *19 ms* | *73 ms* |
| **`png`** | **40 ms · 8 KB** | 53 ms · 199 KB | 317 ms · 955 KB |
| **`webp` @ 85** | 53 ms · 16 KB | 84 ms · **34 KB** | 349 ms · **322 KB** |
| **`webp` @ 90** | 56 ms · 18 KB | 75 ms · 46 KB | 341 ms · 341 KB |
| **`jpeg` @ 85** | 10 ms · 60 KB | **12 ms** · 64 KB | **12 ms** · 88 KB |
| *decode, any format* | *3–5 ms* | *5–7 ms* | *6–16 ms* |

**On flat graphic content PNG is both smaller and faster than WebP** — half the bytes and a third less time. Any blanket claim that WebP compresses better than PNG is false for the kind of work this studio does most.

**On a finished raster panel the ranking flips back.** Re-encoding a real 1600 × 1200 comic panel: PNG 160 ms / 672 KB, **WebP @ 85 124 ms / 53 KB** — faster *and* twelve times smaller. Painterly content is what WebP is built for.

**Decoding is never the problem.** It is 3–16 ms across every format and every scene, roughly a tenth of encoding. Do not choose a format to make reading artifacts back cheaper.

> [!TIP]
> **Which to pick**
> - **Flat, graphic, few colours** — line art, logos, construction sheets, monochrome tests: **`png`**. Smaller, faster, and lossless, so a hairline stays a hairline.
> - **Painterly, gradient, atmospheric** — a finished panel or plate: **`webp` @ 85**, the default. Bump to 90 if you can see artifacts in fine strokes or a shallow ramp.
> - **A rapid draft you will throw away**, and alpha does not matter: **`jpeg`**, which encodes in ~12 ms whatever the scene contains. Never for a deliverable, and never for flat colour, which is what it degrades worst.
> - **Bit-exact verification** — anything `bitmap.diff` will compare: **`png`**. A lossy round trip changes pixels that a comparison will then report as differences.
>
> These are measurements from one machine and three scenes, not constants. If a choice matters, `encodeMs` tells you what your scene actually cost.

---

## Global Functions

The following global functions and objects are injected directly into the script scope:

### `console`
The logging console:
- `console.log(...args: any[])` — Record an informational line.
- `console.info(...args: any[])` — Record an informational line at `[INFO]`. **Not an alias for `console.log`** — the two are distinguishable in the log, which is `[LOG]` for `log` and `console.log`.
- `console.warn(...args: any[])` — Record a warning log line.
- `console.error(...args: any[])` — Record an error log line.
- `console.debug(...args: any[])` — Record a debug log line.
- `console.trace(...args: any[])` — Record a trace message with timestamp.
- `console.clear()` — Clear the accumulated execution log buffer.

### `has(object: any, name: string)` → `boolean`
Whether `object` really has a member called `name`, **without touching it**. Use it to check a call exists before writing a script around it.

```js
const draw = has(ctx, 'drawMannequinWireframe') ? 'drawMannequinWireframe'
           : has(ctx, 'drawMannequin') ? 'drawMannequin' : null;
```

> [!TIP]
> `typeof ctx.foo === 'function'` works too and is often more natural. Prefer `has` when you want the *documented* surface rather than whatever reflection finds — `has(ctx, 'getType')` is `false` where `typeof` would say `function` — and when you are about to pair it with `suggest`. **`'foo' in ctx` cannot answer this**: on an SDK object every name reports as present, for the reason set out under the execution model.

It answers for the **documented surface**, so `has(ctx, 'getType')` is `false` even though the CLR method is there. On `Session` it answers for keys, since that is what a scratchpad has. On a list it answers `true` for anything, because array methods are attached rather than declared and a false *no* would be worse than an honest *maybe* — just call it, and a real absence still says so.

### `suggest(object: any, name: string)` → `string`
What to write instead. Same advice a failed access gives, without having to fail:

```js
if (!has(ctx, 'drawLoomisWireframe')) log(suggest(ctx, 'drawLoomisWireframe'));
// 'CanvasRenderingContext2D' has no property or method 'drawLoomisWireframe'.
// Did you mean 'drawCompositionGrid', 'drawPerspectiveGrid', 'drawTorsoMusculature'? …
```

It searches the **whole surface**, not just the object you asked about, which is what catches a name carried in from another library: `suggest(Skia, 'RuntimeEffect')` answers *"there is no such member here, but `Skia.ColorFilter.runtimeEffect` exists elsewhere on the surface"*. Asking about a name that is already correct says so rather than staying silent — if you are here about a call that exists, the bug is somewhere else.

### `log(message: string)`
Writes an informational line to the execution log.

### `error(message: string)`
Writes an error line to the execution log.

### `exit(message: string)`
Immediately terminates script execution with success status, recording `message` and returning any output generated prior to `exit`.

### `table(rows: any[])` / `table(headers: string[], rows: any[])`
Renders an array of records or scalar values as an aligned ASCII grid in the log output:
- `table(records)`: Formats an array of objects where columns match property names.
- `table(headers, rows)`: Formats with explicit column titles.
- `table(values)`: Formats an array of primitive values under a single column.

### `mina`
Animation and easing curve generators compatible with Snap.svg:
- `mina.linear(n: number)` → `number`
- `mina.easein(n: number)` → `number`
- `mina.easeout(n: number)` → `number`
- `mina.easeinout(n: number)` → `number`
- `mina.backin(n: number)` → `number`
- `mina.backout(n: number)` → `number`
- `mina.bounce(n: number)` → `number`
- `mina.elastic(n: number)` → `number`
- `mina.time()` → `number` (current timestamp in milliseconds)

### `Stage`
Declares which stage of work you are in, so every script, render and note that follows is filed under it. The declaration **persists across executions** until you change or end it — set it once at the top of a stage, not in every script.

- `Stage.begin(name: string)` → `string` — Declares the stage and returns the name as recorded. Beginning a **different** stage closes the previous one first, so two never overlap. Re-declaring the stage you are already in is an announcement, not a transition: it records a continuation and leaves the stage running, so you can safely restate it at the top of each script. Case is ignored when comparing, and the originally recorded spelling is kept.
- `Stage.end()` — Ends the current stage. Harmless when none is open.
- `Stage.current` → `string?` — The stage in effect, or **`null`** if none, so `if (!Stage.current)` is the check to write. Outside a project — an ad-hoc engine with no run session — this always reads `null`, even directly after `Stage.begin(...)`, because the stage lives on the session.
- `Stage.note(message: string)` — Records a note under the current stage.
- `Stage.expect(claim: string)` — Records what you expect the next render to show, **before** you make it.
- `Stage.check(claim: string, passed: boolean, detail?: string)` → `boolean` — Records the verdict on a claim and returns `passed`, so it reads as the test it is: `if (!Stage.check('accent under 15%', share < 0.15, 'measured ' + pct)) { … }`. A failing check is not a failing run — it is the most useful thing the record can hold.

`log(...)` reaches only the caller of the one tool call that produced it. `Stage.note(...)` persists into the run's record, and is what a reader sees afterwards — use it for the reasoning that would otherwise be lost, such as why a direction was abandoned or what a render was meant to test.

```javascript
Stage.begin('Concept');
Stage.note('synecdoche — the wing, not the bird; rejects the generic globe');
```

> [!NOTE]
> A stage is **your account of your own intent**, not something the server verified — that is what makes a run legible rather than merely logged. The machine-recorded fields beside it (script path, duration, artifact path, byte count) remain the checkable half. Name stages for what a reader would want to click on: `Concept`, `Blocking`, `Refine`, `Stress test`.

> [!TIP]
> **The measurements record themselves.** `bitmap.diff`, `bitmap.palette` and `bitmap.rowProfile` write an `observe` event carrying what they *found* — the similarity and the changed region, the dominant colours and their shares, how many rows matched — so the record shows the outcome of a check without you restating it. A `rowProfile` that matched nothing is recorded as such, which is the case most easily missed by a loop that simply does not run.
>
> What the server cannot know is what you were hoping for. That is what `Stage.expect(...)` and `Stage.check(...)` are for, and together with the automatic `observe` events they are what lets a reader tell a run that measured and was satisfied from one that measured, found the value wrong, and redrew four times.

### `Session`
Per-session scratchpad dictionary that persists across multiple script executions on the same MCP session:
- `Session[key] = value` — Cache an intermediate computation, configuration, or data structure.
- `Session[key]` — Retrieve a previously cached value (returns `undefined` if key is not set).
- `delete Session[key]` — Evict a key from session scratchpad.

> [!TIP]
> **It holds bitmaps and canvases, not just data — and that is the cheapest way to hand work between stages.** Writing a stage to disk and loading it back in the next call costs an encode (~150 ms at 1600 × 1200) and a decode, for a picture only the machine will read. Stashing the bitmap costs neither:
>
> ```javascript
> // Stage 1 — keep it, do not encode it.
> Session.stage1 = canvas.toBitmap();
> ```
> ```javascript
> // Stage 2 — the previous stage is already a bitmap, ready to draw and to measure against.
> const prev = Session.stage1;
> ctx.drawImage(prev, 0, 0);
> const changed = canvas.bitmap.diff(prev);      // no file, no decode
> log(`changed region ${changed.bounds.width}×${changed.bounds.height}`);
> ```
>
> Pair it with `render: false` and a measurement pass costs neither an encode nor a decode. **Still write the artifacts a reader will look at** — `outFile` is what makes a run reviewable and replayable, and the scratchpad dies with the session. This is for the machine-only round trips in between.

---

# Snap

Snap.svg-compatible retained-mode vector graphics API.

## `Snap` Namespace & Factory

- `Snap(width: number, height: number)` → `SnapPaper` — Creates a new root SVG document with the specified viewport dimensions.
- `Snap.Create(width: number, height: number)` → `SnapPaper` — Alias for `Snap(w, h)`.
- `Snap.parse(svgXml: string)` → `SnapPaper` — Parses an SVG XML string into an editable `SnapPaper` document tree.
- `Snap.matrix(a?: number, b?: number, c?: number, d?: number, e?: number, f?: number)` → `SnapMatrix` — Constructs a 2D affine transformation matrix.
- `Snap.path` → `SnapPathApi` — Path measurement and geometry utility namespace.
- `Snap.rgb(r: number, g: number, b: number, a?: number)` → `string` — Returns a formatted CSS `rgb()` or `rgba()` string.
- `Snap.hsl(h: number, s: number, l: number, a?: number)` → `string` — Returns a formatted CSS `hsl()` or `hsla()` string.
- `Snap.format(template: string, ...args: any[])` → `string` — Replaces `{0}`, `{1}`, etc. placeholders in `template`.
- `Snap.rad(deg: number)` → `number` — Converts degrees to radians.
- `Snap.deg(rad: number)` → `number` — Converts radians to degrees.
- `Snap.angle(x1: number, y1: number, x2: number, y2: number)` → `number` — Calculates the angle in degrees between two points.
- `Snap.snapTo(values: number[], val: number, tolerance?: number)` → `number` — Snaps `val` to the closest number in `values` within `tolerance` (default 10), or returns `val` unchanged if nothing is close enough. To test whether it matched, check the result **is one of your candidates** — the return is single-precision, so an unsnapped `33.46` comes back as `33.459999084472656` and `result !== val` reports a match that never happened.

## `SnapPathApi` (`Snap.path`)

- `Snap.path.getTotalLength(pathData: string)` → `number` — Calculates the total arc length of an SVG path definition.
- `Snap.path.getPointAtLength(pathData: string, length: number)` → `SnapPoint` — Returns the `{ x, y, alpha }` coordinates and tangent angle at a given distance along the path.
- `Snap.path.getBBox(pathData: string)` → `SnapBBox` — Calculates the tight bounding box `{ x, y, width, height, cx, cy, x2, y2 }` of an SVG path definition.

## `SnapPaper`

Represents the root SVG canvas surface:

- `paper.circle(cx: number, cy: number, r: number)` → `SnapElement` — Appends a `<circle>`.
- `paper.rect(x: number, y: number, width: number, height: number, rx?: number, ry?: number)` → `SnapElement` — Appends a `<rect>`.
- `paper.ellipse(cx: number, cy: number, rx: number, ry: number)` → `SnapElement` — Appends an `<ellipse>`.
- `paper.line(x1: number, y1: number, x2: number, y2: number)` → `SnapElement` — Appends a `<line>`.
- `paper.polyline(...points: number[] | number[][])` → `SnapElement` — Appends a `<polyline>`.
- `paper.polygon(...points: number[] | number[][])` → `SnapElement` — Appends a `<polygon>`.
- `paper.path(d?: string)` → `SnapElement` — Appends a `<path>`.
- `paper.text(x: number, y: number, text: string)` → `SnapElement` — Appends a `<text>` element.
- `paper.image(src: string, x: number, y: number, width: number, height: number)` → `SnapElement` — Appends an `<image>`.
- `paper.g(...elements: SnapElement[])` / `paper.group(...)` → `SnapElement` — Creates and appends a container `<g>`.
- `paper.svg(x: number, y: number, width: number, height: number)` → `SnapElement` — Creates a nested `<svg>` element.
- `paper.use(element: SnapElement)` → `SnapElement` — Creates a `<use>` element referencing another element.
- `paper.clear()` → `void` — Removes every drawn element, **keeping `<defs>`**. Gradients, masks and patterns survive a clear and their ids stay valid, so a redraw can reference the paint servers it already made. To drop those too, start a new `Snap(w, h)`.
- `paper.toString()` → `string` — Serializes the document tree to an SVG XML string.
- `paper.toImageBytes(width?: number, height?: number, format?: string, quality?: number)` → `byte[]` — Headlessly renders the SVG to image bytes (default: WebP Q=85).
- `paper.toDataUri(format?: string, width?: number, height?: number, quality?: number)` → `string` — Renders to a `data:image/...;base64,...` URI (defaults to `format: 'svg'`).

> [!NOTE]
> **A paper is itself a `SnapElement`**, so everything under [`SnapElement`](#snapelement) works on it — most usefully `paper.select(...)`, `paper.selectAll(...)`, `paper.children`, `paper.attr(...)`, `paper.getBBox()` and the tree-placement calls. `paper.select('#mark')` searching the whole document is the ordinary way to find something a previous stage drew.

## Paint Servers — Gradients, Masks & Patterns (`SnapPaper`)

Vector fills are not limited to flat colour. A paint server is created on the paper, lands in `<defs>` automatically, and is referenced by id:

```javascript
const paper = Snap(400, 300);
const g = paper.gradient('l(0,0,1,0)#ff0000-#0000ff');   // horizontal red → blue
paper.rect(0, 0, 400, 300).attr({ fill: 'url(#' + g.attr('id') + ')' });
paper;
```

- `paper.gradient(descriptor: string)` → `SnapGradient` — Snap.svg shorthand. `l(x1,y1,x2,y2)` for linear, `r(cx,cy,r)` for radial, followed by `-`-separated stops. Coordinates are fractions of the bounding box (`0`–`1`). A stop may carry an explicit offset with `:` — `'l(0,0,1,0)#000-#f00:30%-#fff'`; without one, stops are spaced evenly.
- `paper.gradientLinear(x1: number, y1: number, x2: number, y2: number)` → `SnapLinearGradient` — Explicit linear gradient; coordinates are fractions of the bounding box.
- `paper.gradientRadial(cx: number, cy: number, r: number, fx?: number, fy?: number)` → `SnapRadialGradient` — Explicit radial gradient with optional focal point.
- `paper.mask(...elements: SnapElement[])` → `SnapMask` — Creates a `<mask>` in `<defs>`; reference it with `attr({ mask: 'url(#id)' })`.
- `paper.ptrn(x: number, y: number, width: number, height: number, vx?: number, vy?: number, vw?: number, vh?: number)` → `SnapPattern` — Creates a tiling `<pattern>`.
- `paper.defs` → `SnapElement` — The document's `<defs>` container, created on demand. Append to it directly for anything the helpers above do not cover.

### `SnapGradient`

- `gradient.attr('id')` → `string` — The generated id. **This is how you reference the gradient** — every paint server is auto-assigned one.
- `gradient.addStop(color: string, offsetPercent: number)` → `SnapGradient` — Appends one stop. Chainable.
- `gradient.setStops(descriptor: string)` → `SnapGradient` — Replaces all stops from a `-`-separated descriptor.
- `gradient.stops()` → `SnapGradientStop[]` — The current stops.
- Linear gradients also expose `gradient.x1`, `gradient.y1`, `gradient.x2`, `gradient.y2`; radial ones expose `gradient.cx`, `gradient.cy`, `gradient.r`.

> [!IMPORTANT]
> By default a gradient is measured against **each shape's own bounding box**, so two shapes sharing one gradient each get the full colour ramp and a visible seam appears where they meet. To anchor the ramp in the **paper's** coordinate space instead — which is what makes a limb built from several overlapping shapes read as one continuous form — switch the gradient to user space and give it absolute coordinates:
>
> ```javascript
> const g = paper.gradient('l(0,0,1,0)#ff0000-#0000ff');
> g.attr({ gradientUnits: 'userSpaceOnUse', x1: 0, y1: 0, x2: 400, y2: 0 });
> ```
>
> Any SVG attribute the paint server accepts can be set this way; `attr` passes through names it does not specifically handle.

## `SnapElement`

Represents any SVG node in the document hierarchy:

### Attributes & Transforms

- `element.attr(name: string)` → `any` — Retrieves the value of an attribute or style property.
- `element.attr(name: string, value: any)` → `SnapElement` — Sets an attribute (e.g. `fill`, `stroke`, `stroke-width`, `opacity`, `d`, `transform`). Supports method chaining.
- `element.attr(attributes: object)` → `SnapElement` — Sets multiple attributes via a dictionary literal: `el.attr({ fill: '#f00', stroke: '#000', 'stroke-width': 2 })`.
- `element.transform(transformStringOrMatrix: string | SnapMatrix)` → `SnapElement` — Applies SVG transform commands (e.g. `t100,50r45s1.5` or a `SnapMatrix`).

### Node Properties

- `element.id` → `string` — Gets or sets the element's id. **An element has none until you assign one**, so `select('#name')` finds nothing on a shape you just drew unless you named it first.
- `element.type` → `string` — The SVG tag name: `rect`, `circle`, `ellipse`, `path`, `g`, `text`, `tspan`, `textPath`, `line`, `polyline`, `polygon`, `image`, `use`, `svg`, `defs`, `mask`, `clipPath`, `pattern`, `linearGradient`, `radialGradient`, `stop`, `symbol`, `marker`.
- `element.parent` → `SnapElement?` — The containing element.
- `element.children` → `SnapElement[]` — Direct child elements. **A paper's children include its `<defs>`**, so filter on `type` rather than assuming index 0 is the first shape you drew.
- `element.paper` → `SnapPaper?` — The document this element belongs to.

> [!NOTE]
> `parent` and `children` are **properties, not methods** — this is the one place the surface departs
> from Snap.svg, where both are calls. Written as methods they answered `el.children.length` with the
> delegate's arity, `0`, so a wrong spelling was indistinguishable from an empty tree. `el.children()`
> now fails loudly instead.

### Tree Placement & Lifecycle

- `element.appendTo(parent: SnapElement | SnapPaper)` → `SnapElement` — Moves this element to the end of `parent`.
- `element.prependTo(parent: SnapElement | SnapPaper)` → `SnapElement` — Moves this element to the start of `parent`.
- `element.add(...elements: SnapElement[])` → `SnapElement` — Appends children.
- `element.before(other: SnapElement)` / `element.after(other: SnapElement)` → `SnapElement` — Moves this element immediately before or after `other` in document order, which is how you control z-order after construction.
- `element.remove()` → `SnapElement` — Detaches the element from its parent container.
- `element.clone()` → `SnapElement` — Deep-clones the element and its children. The copy is **detached** — it appears in nothing until `appendTo`/`prependTo` places it, unlike Snap.svg which inserts it after the original — and it carries the original's `id`, so give it a new one before placing it.
- `element.clear()` → `void` — Removes all child nodes from this container element.

### Geometry & Measurement

- `element.getBBox()` → `SnapBBox` — Calculates the element's bounding box. **For a `<text>` element this is a real font measurement**, honouring `font-family` (including a fallback list), `font-size`, `font-weight`, `font-style` and `text-anchor` — so it agrees with `ctx.measureText(...)` for the same string. Remember SVG's `y` is the **baseline**, so the returned box starts above it.

> [!TIP]
> This is how a vector layout measures before it places. `Layout`, `Scale` and `Css` are globals and work the same on either side of the SDK, so a measured heading composes exactly as it does on canvas:
>
> ```javascript
> const heading = paper.text(40, 60, 'A heading of some length');
> heading.attr({ 'font-family': 'Georgia', 'font-size': 34 });
> const box = heading.getBBox();
> paper.line(40, box.y2 + 12, 40 + box.width, box.y2 + 12);   // a rule under the measured width
> ```
>
> `ctx.measureWrappedText(...)` remains canvas-only: SVG has no wrapping, so a paragraph broken into `<tspan>` lines is the caller's decision rather than the toolkit's.
- `element.getTotalLength()` → `number` — Measures path total length (paths only).
- `element.getPointAtLength(length: number)` → `SnapPoint` — Computes coordinates at `length` (paths only).
- `element.toSkPath()` → `SKPath` — Converts the element's geometry to a Skia path, for measurement or raster compositing.

### Finding Descendants

- `element.select(selector: string)` → `SnapElement?` — Finds the first descendant matching a tag name, `#id`, or `.class`.
- `element.selectAll(selector: string)` → `SnapElement[]` — Finds all matching descendants.

### Creating Child Elements

- `element.rect(x: number, y: number, width: number, height: number, rx?: number, ry?: number)` → `SnapRect` — Creates and appends a child `<rect>`.
- `element.circle(cx: number, cy: number, r: number)` → `SnapCircle` — Creates and appends a child `<circle>`.
- `element.ellipse(cx: number, cy: number, rx: number, ry: number)` → `SnapEllipse` — Creates and appends a child `<ellipse>`.
- `element.path(d?: string)` → `SnapPath` — Creates and appends a child `<path>`.
- `element.line(x1: number, y1: number, x2: number, y2: number)` → `SnapLine` — Creates and appends a child `<line>`.
- `element.polyline(...points: number[])` → `SnapPolyline` — Creates and appends a child `<polyline>`.
- `element.polygon(...points: number[])` → `SnapPolygon` — Creates and appends a child `<polygon>`.
- `element.text(x: number, y: number, text: any)` → `SnapText` — Creates and appends a child `<text>`.
- `element.image(src: string, x: number, y: number, width: number, height: number)` → `SnapImage` — Creates and appends a child `<image>`.
- `element.g(...elements: SnapElement[])` / `element.group(...)` → `SnapGroup` — Creates and appends a nested `<g>`.
- `element.use(target: SnapElement | string)` → `SnapUse` — Creates and appends a child `<use>` element.
- `element.el(name: string, attrs?: object)` → `SnapElement` — Creates and appends an SVG child element by tag name. Supported: `rect`, `circle`, `ellipse`, `path`, `g`, `image`, `text`, `tspan`, `textPath`, `line`, `polyline`, `polygon`, `mask`, `clipPath`, `pattern`, `use`, `defs`, `linearGradient`, `radialGradient`, `stop`, `symbol`, `marker`, `svg`. **An unrecognised name throws** rather than silently producing a `<g>`. For gradients prefer `paper.gradient(...)`.

## `SnapMatrix`

2D affine transform matrix `[ a c e ; b d f ; 0 0 1 ]`:

- `matrix.translate(dx: number, dy: number)` → `SnapMatrix` — Post-multiplies a translation.
- `matrix.scale(sx: number, sy?: number, cx?: number, cy?: number)` → `SnapMatrix` — Post-multiplies a scaling matrix around optional pivot `(cx, cy)`.
- `matrix.rotate(angleDeg: number, cx?: number, cy?: number)` → `SnapMatrix` — Post-multiplies a rotation in degrees around optional pivot `(cx, cy)`.
- `matrix.skewX(angleDeg: number)` → `SnapMatrix` — Post-multiplies an X-skew.
- `matrix.skewY(angleDeg: number)` → `SnapMatrix` — Post-multiplies a Y-skew.
- `matrix.mult(other: SnapMatrix)` → `SnapMatrix` — Multiplies by another matrix.
- `matrix.invert()` → `SnapMatrix` — Returns the inverted matrix.
- `matrix.clone()` → `SnapMatrix` — Clones the matrix.
- `matrix.toTransformString()` → `string` — Formats matrix as standard SVG `matrix(a,b,c,d,e,f)`.
- `matrix.a`, `matrix.b`, `matrix.c`, `matrix.d`, `matrix.e`, `matrix.f` → `number` — The six affine components, read-only.
- `matrix.determinant` → `number`, `matrix.isIdentity` → `boolean` — Matrix state checks.
- `matrix.add(other: SnapMatrix)` → `SnapMatrix` — Snap.svg's spelling of `mult`; identical behaviour.
- `matrix.multLeft(other: SnapMatrix)` → `SnapMatrix` — Pre-multiplies instead of post-multiplying.
- `matrix.transformPoint(x: number, y: number)` → `SnapPoint` — Applies the matrix to a single point.

---

# Canvas2D

Immediate-mode 2D raster canvas API compatible with HTML5 Canvas 2D.

## Canvas Factory

- `createCanvas(width?: number, height?: number)` → `SkiaCanvas` — Creates a new raster canvas surface.
- `new Canvas(width?: number, height?: number)` → `SkiaCanvas` — Constructor alias for `createCanvas`.

## `SkiaCanvas`

- `canvas.getContext("2d")` → `CanvasRenderingContext2D` — Obtains the 2D drawing context.
- `canvas.width` → `number` — Canvas width in pixels.
- `canvas.height` → `number` — Canvas height in pixels.
- `canvas.clear(color?: string)` → `void` — Clears canvas with transparent or specified color.
- `canvas.toBitmap()` → `SkiaBitmapWrapper` — Extracts an editable `SkiaBitmapWrapper` copy.
- `canvas.toImageData()` → `ImageData` — Extracts a full-canvas `ImageData` pixel buffer.
- `canvas.toImageBytes(format?: string, quality?: number)` → `byte[]` — Encodes canvas to image bytes (default: WebP Q=85).
- `canvas.toDataUri(format?: string, quality?: number)` → `string` — Returns base64 `data:image/...;base64,...` data URI.
- `canvas.bitmap` → `SkiaBitmapWrapper` — The canvas's live backing bitmap (unlike `toBitmap()`, which copies).

## `CanvasPath`

A reusable path object, independent of any context — build it once and pass it to `ctx.fill(path)`, `ctx.stroke(path)` or `ctx.clip(path)` as often as you like. Also available under the DOM name `Path2D`; the two are the same constructor.

- `new CanvasPath()` → `CanvasPath` — An empty path.
- `new CanvasPath(svgPathData: string)` → `CanvasPath` — A path parsed from an SVG `d` string, e.g. `new CanvasPath('M20,20 L180,20 Z')`. This is the bridge from vector geometry — `Snap.path.*` helpers and `element.attr('d')` both hand you a `d` string.
- `new CanvasPath(other: CanvasPath)` → `CanvasPath` — A copy, independent of its source.

Segment methods mirror the context's own path construction and take the same arguments:

- `path.moveTo(x, y)` · `path.lineTo(x, y)` · `path.closePath()` · `path.beginPath()`
- `path.rect(x, y, width, height)` · `path.roundRect(x, y, width, height, radii?)`
- `path.arc(x, y, radius, startAngle, endAngle, anticlockwise?)` · `path.arcTo(x1, y1, x2, y2, radius)`
- `path.ellipse(x, y, radiusX, radiusY, rotation, startAngle, endAngle, anticlockwise?)`
- `path.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, x, y)` / `path.sCurveTo(...)` — CSI grammar alias
- `path.quadraticCurveTo(cpx, cpy, x, y)` / `path.cCurveTo(...)` — CSI grammar alias
- `path.addPath(other: CanvasPath)` — Appends another path's segments to this one.

### Boolean Operations

Each returns a **new** path and leaves both operands untouched, so a shape can be combined repeatedly
and the results chain: `a.union(b).subtract(c)`.

- `path.union(other: CanvasPath)` → `CanvasPath` — Everything covered by either path.
- `path.subtract(other: CanvasPath)` → `CanvasPath` — What is left once `other` is cut out. **This is how you cut a counter as real geometry** — a genuine hole in one path, rather than an even-odd sub-path that depends on winding, or a background-coloured shape laid on top that only works on a plain ground.
- `path.intersect(other: CanvasPath)` → `CanvasPath` — Only what both cover.
- `path.xor(other: CanvasPath)` → `CanvasPath` — What either covers, but not both.
- `path.simplify()` → `CanvasPath` — Resolves self-intersections into simple contours. Worth doing to a hand-built contour before combining it; a stroke that crosses itself has regions covered twice, and what that means depends on the fill rule rather than on the shape you intended.

> [!NOTE]
> The result is normalised, so it draws the same under either fill rule and you never have to know
> which one a boolean operation happened to produce.
- `path.dispose()` — Releases the native path.

## `CanvasRenderingContext2D`

### Shapes & Rectangles
- `ctx.fillRect(x: number, y: number, width: number, height: number)` — Draws a filled rectangle using `fillStyle`.
- `ctx.strokeRect(x: number, y: number, width: number, height: number)` — Draws a stroked rectangle using `strokeStyle` and `lineWidth`.
- `ctx.clearRect(x: number, y: number, width: number, height: number)` — Erases pixels in rectangle to transparent black.

### Path Construction
- `ctx.beginPath()` — Resets the current path.
- `ctx.closePath()` — Adds a straight line back to the start of the current sub-path.
- `ctx.moveTo(x: number, y: number)` — Moves the pen to `(x, y)` without drawing.
- `ctx.lineTo(x: number, y: number)` — Adds a straight line segment to `(x, y)`.
- `ctx.rect(x: number, y: number, width: number, height: number)` — Adds a rectangle sub-path.
- `ctx.roundRect(x: number, y: number, width: number, height: number, radii?: number | number[])` — Adds a rounded rectangle with uniform or per-corner corner radii `[tl, tr, br, bl]`.
- `ctx.arc(x: number, y: number, radius: number, startAngle: number, endAngle: number, anticlockwise?: boolean)` — Adds an arc given center, radius, and angles in radians.
- `ctx.arcTo(x1: number, y1: number, x2: number, y2: number, radius: number)` — Adds a tangent arc between two control points.
- `ctx.ellipse(x: number, y: number, radiusX: number, radiusY: number, rotation: number, startAngle: number, endAngle: number, anticlockwise?: boolean)` — Adds an elliptical arc.
- `ctx.bezierCurveTo(cp1x: number, cp1y: number, cp2x: number, cp2y: number, x: number, y: number)` — Adds a cubic Bézier curve segment.
- `ctx.sCurveTo(cp1x: number, cp1y: number, cp2x: number, cp2y: number, x: number, y: number)` — CSI grammar alias for `bezierCurveTo` (reversing S-curve).
- `ctx.quadraticCurveTo(cpx: number, cpy: number, x: number, y: number)` — Adds a quadratic Bézier curve segment.
- `ctx.cCurveTo(cpx: number, cpy: number, x: number, y: number)` — CSI grammar alias for `quadraticCurveTo` (single-direction C-curve).
- `ctx.fill(path?: CanvasPath, fillRule?: 'nonzero' | 'evenodd')` — Fills the current or specified `CanvasPath`. Either argument may be given alone, so `ctx.fill('evenodd')` and `ctx.fill(path, 'evenodd')` both work.
- `ctx.stroke(path?: CanvasPath)` — Strokes the current or specified `CanvasPath`.
- `ctx.clip(path?: CanvasPath, fillRule?: 'nonzero' | 'evenodd')` — Intersects the clipping region with the current or specified path. Takes the same fill-rule argument as `fill`.

> [!TIP]
> `'evenodd'` is one way to cut a **counter** — the enclosed hole in a mark or a letterform — as real geometry, by adding the inner sub-path to the same path and filling once. `path.subtract(inner)` is the other, and it is the sturdier one: it produces a path that *is* the shape with the hole, rather than a path that only reads as one under a particular fill rule. Either beats laying a background-coloured shape on top, which looks identical on a white ground and fails everywhere else: over a photograph, in a one-colour knockout, or exported as a single vector path. Anything that has to survive a monochrome test wants the real hole.
>
> The rule is an argument to `fill` and `clip`, never a property of the path. Omitting it always means `'nonzero'`, whatever the path was used for before.

### State & Transformations
- `ctx.save()` — Pushes current drawing state onto the state stack.
- `ctx.restore()` — Restores the most recently saved drawing state.
- `ctx.translate(x: number, y: number)` — Translates the coordinate space.
- `ctx.rotate(angleRadians: number)` — Rotates coordinate space by radians.
- `ctx.scale(sx: number, sy: number)` — Scales coordinate space.
- `ctx.transform(a: number, b: number, c: number, d: number, e: number, f: number)` — Multiplies current transform matrix by `[ a c e ; b d f ; 0 0 1 ]`.
- `ctx.setTransform(a?: number, b?: number, c?: number, d?: number, e?: number, f?: number)` — Resets and sets transform matrix.
- `ctx.resetTransform()` — Resets transform to identity.

### Styling & Shaders
- `ctx.fillStyle` — Fill style: CSS color string (`"#ff0000"`, `"rgba(0,0,0,0.5)"`, `"hsl(200, 50%, 50%)"`), `CanvasGradient`, `CanvasPattern`, or shader.
- `ctx.strokeStyle` — Stroke style: color string, `CanvasGradient`, `CanvasPattern`, or shader.
- `ctx.lineWidth` — Stroke thickness in pixels (default 1).
- `ctx.lineCap` — Cap style: `"butt"`, `"round"`, `"square"`.
- `ctx.lineJoin` — Join style: `"miter"`, `"round"`, `"bevel"`.
- `ctx.miterLimit` — Miter limit ratio (default 10).
- `ctx.setLineDash(segments: number[])` — Dash pattern for subsequent strokes, e.g. `[4, 4]`. An empty array clears it; an odd-length array repeats to become even, so `[6]` means 6 on, 6 off. Composes with `ctx.pathEffect` rather than replacing it.
- `ctx.getLineDash()` → `number[]` — The current pattern, empty when none is set.
- `ctx.lineDashOffset` — Phase offset into the pattern, in pixels.
- `ctx.globalAlpha` — Alpha multiplier `[0.0, 1.0]`.
- `ctx.globalCompositeOperation` — Blend mode: `"source-over"`, `"multiply"`, `"screen"`, `"overlay"`, `"darken"`, `"lighten"`, `"color-dodge"`, `"color-burn"`, `"hard-light"`, `"soft-light"`, `"difference"`, `"exclusion"`, `"hue"`, `"saturation"`, `"color"`, `"luminosity"`, `"xor"`, `"destination-over"`, etc.
- `ctx.shadowColor` — Drop shadow color string.
- `ctx.shadowBlur` — Gaussian blur sigma for shadows.
- `ctx.shadowOffsetX` / `ctx.shadowOffsetY` — Horizontal and vertical shadow offset.
- `ctx.filter` — Image filter (e.g. `Skia.ImageFilter.blur(5, 5)`).
- `ctx.colorFilter` — Color filter (e.g. `Skia.ColorFilter.colorMatrix(...)`).
- `ctx.pathEffect` — Path effect: what the stroke or fill is *made of* (e.g. `Skia.PathEffect.stamp(bristle, 4)` for a brush, `Skia.PathEffect.hatch(1, 6, 45)` for hatching, `Skia.PathEffect.corner(10)`, `Skia.PathEffect.dash([10, 5])`).
- `ctx.maskFilter` — Mask filter applied to the shape's coverage (e.g. `Skia.MaskFilter.blur(6)` for a soft edge, `Skia.MaskFilter.blur(8, 'outer')` for a halo).
- `ctx.dither` — `true` to dither, trading fine noise for the absence of banding. Off by default. Worth it on a wide, shallow gradient — a sky, a soft tonal ramp — where 8-bit steps otherwise show as visible bands.

### Stroke Geometry

- `ctx.strokeToPath(path?: CanvasPath)` → `CanvasPath` — The outline of what `stroke()` would draw, as a fillable path. Defaults to the current path; returns an empty path where the stroke would be a hairline.

> [!TIP]
> This is how a stroke stops being a line with a width and becomes a shape you can work on — the
> difference between a constant-width ribbon and a drawn mark. Outline the stroke, then modify the
> outline: narrow it toward one end for pressure, `subtract` another shape out of it, or fill it with
> a gradient running *across* the stroke rather than along the path. It bakes in the current
> `lineWidth`, `lineCap`, `lineJoin`, `miterLimit` and `pathEffect`, so a stamped or hatched stroke
> becomes geometry and survives into `outSvg` as vector rather than only as pixels.
>
> ```javascript
> ctx.lineWidth = 14;
> const mark = ctx.strokeToPath(spine);       // the stroke, as a shape
> ctx.fill(mark.subtract(bite));              // now it can be cut
> ```

### Typography
- `ctx.font` — CSS font shorthand: `"bold 24px Arial"`, `"600 21px Inter Tight"`, `"italic 400 46px 'Source Serif 4', Georgia, serif"`. Parsed by the shorthand's grammar, so **numeric weights** (`100`–`900`), **multi-word and quoted family names**, and **comma-separated fallback lists** all work, as do the generic families (`serif`, `sans-serif`, `monospace`, `cursive`, `fantasy`, `system-ui`). Sizes may be `px` or `pt`. Weight also accepts the foundry names — `semibold`, `medium`, `black` — which a browser would reject.
- `ctx.textAlign` — Alignment: `"left"`, `"center"`, `"right"`, `"start"`, `"end"`.
- `ctx.textBaseline` — Baseline: `"top"`, `"middle"`, `"bottom"`, `"alphabetic"`, `"hanging"`.
- `ctx.letterSpacing` — Tracking between glyphs: `"3px"`, `"0.15em"`, or a bare number read as px. `em` resolves against the font size in force when the text is drawn, which is why `LogoType.computeWordmarkTracking(...)` — an em fraction — applies directly: `ctx.letterSpacing = LogoType.computeWordmarkTracking(48, true) + 'em'`. Spacing goes **between** glyphs and not after the last, so a tracked run stays centred under `textAlign`. It is part of the drawing state, so `save()`/`restore()` scope it.

> [!IMPORTANT]
> A family Skia does not have is **silently substituted** — no error, no signal — so a fallback list is the difference between the face you chose and one you did not. `ctx.font = '40px "Didot", Georgia, serif'` uses Georgia when Didot is missing, which is a decision you made; `'40px Didot'` alone renders in whatever the platform substitutes, which is not. Check the face you actually care about with `Skia.Font.has(...)` and let the list handle the rest.
>
> Any non-zero `letterSpacing` places glyphs one at a time, which loses kerning — the trade tracking always makes, in any tool. Leave it at `0` for body text, where the shaper's kerning is worth more than tracking is.
- `ctx.fillText(text: string, x: number, y: number, maxWidth?: number)` — Draws filled text (supports multi-line strings with `\n`).
- `ctx.strokeText(text: string, x: number, y: number, maxWidth?: number)` — Draws stroked text outline (supports multi-line strings with `\n`).

> [!TIP]
> `maxWidth` **condenses** the type horizontally rather than shrinking it, so the cap height survives what the width does not — which is what keeps a row of fitted labels reading as one row. Tracking is condensed with it. A multi-line string is condensed once, by its widest line, so the block stays internally consistent. A `maxWidth` of zero or less draws nothing, so a bad value shows up rather than silently lifting the limit.
>
> Use it to *fit* a label into a known box. To *flow* a paragraph, use `fillWrappedText`, whose `maxWidth` breaks lines instead of narrowing glyphs.
- `ctx.fillWrappedText(text: string, x: number, y: number, maxWidth: number, lineHeight?: number)` → `TextBlock` — Word-wraps and draws filled paragraph text within `maxWidth`, returning the box it occupied.
- `ctx.strokeWrappedText(text: string, x: number, y: number, maxWidth: number, lineHeight?: number)` → `TextBlock` — The same, stroked.
- `ctx.measureWrappedText(text: string, maxWidth: number, lineHeight?: number)` → `TextBlock` — Wraps and reports the box the paragraph *would* occupy, **without drawing it**.
- `ctx.measureText(text: string)` → `TextMetrics` — Returns `{ width, actualBoundingBoxAscent, actualBoundingBoxDescent, fontBoundingBoxAscent, fontBoundingBoxDescent }`.

A `TextBlock` is a rectangle plus what was needed to produce it: `{ x, y, width, height, x2, y2, cx, cy, lines, lineCount, lineHeight, ascent, descent, positioned }`. `lines` is the wrapped text as an array, `width` is the widest line, and `height` is `lineCount × lineHeight`.

> [!IMPORTANT]
> `measureWrappedText` is what makes a stacked layout possible: **until a block's height is knowable in advance, nothing can be placed beneath it except by guessing.** It shares its wrapping and measurement with the drawing path, so the box it reports is the box that will be drawn.
>
> A measurement has no anchor, so its `x`/`y` are `0` and `positioned` is `false` — only the size is meaningful. The block returned by `fillWrappedText` *is* anchored (`positioned: true`), and its `x`/`y` account for `textAlign` and `textBaseline`, so it is where the ink landed rather than where you aimed. With `textBaseline = 'top'` the two coincide, which is the predictable way to stack.
>
> ```javascript
> const intro = ctx.measureWrappedText(body, 380);
> const [head, para, note] = Layout.stack(panel, [48, intro.height, 24], 12);
> ```

### Gradients & Patterns
- `ctx.createLinearGradient(x0: number, y0: number, x1: number, y1: number)` → `CanvasGradient` — Creates a linear gradient.
- `ctx.createRadialGradient(x0: number, y0: number, r0: number, x1: number, y1: number, r1: number)` → `CanvasGradient` — Creates a two-circle radial gradient.
- `ctx.createConicGradient(startAngle: number, x: number, y: number)` → `CanvasGradient` — Creates a sweep / conic gradient.
- `ctx.createPattern(image: SkiaBitmapWrapper | SkiaCanvas, repetition?: string)` → `CanvasPattern` — Creates repeating pattern (`"repeat"`, `"repeat-x"`, `"repeat-y"`, `"no-repeat"`).
- `gradient.addColorStop(offset: number, color: string)` — Adds a color stop where `offset` is between `0.0` and `1.0`.

### Image & SVG Compositing
- `ctx.drawImage(image: SkiaBitmapWrapper | SkiaCanvas | SnapPaper, dx: number, dy: number)` — Draws image at `(dx, dy)`.
- `ctx.drawImage(image: SkiaBitmapWrapper | SkiaCanvas | SnapPaper, dx: number, dy: number, dw: number, dh: number)` — Draws image scaled to `(dw, dh)`.
- `ctx.drawImage(image: SkiaBitmapWrapper | SkiaCanvas, sx: number, sy: number, sw: number, sh: number, dx: number, dy: number, dw: number, dh: number)` — 9-parameter source cropping: extracts `(sx, sy, sw, sh)` from source image and draws scaled onto destination `(dx, dy, dw, dh)`.
- `ctx.drawSvg(paperOrXml: SnapPaper | string, x?: number, y?: number, width?: number, height?: number)` — Composites vector SVG directly onto raster canvas layers.

Both honour `globalAlpha`, `globalCompositeOperation`, `filter`, `colorFilter`, the shadow properties and the current transform. They do **not** use `fillStyle` or `strokeStyle` — the source pixels supply the colour. To tint or grade an image, set `ctx.colorFilter` before drawing, or use `bitmap.applyColorFilter(filter)` to bake the change into a new bitmap.

### Pixel Buffer Access
- `ctx.getImageData(sx: number, sy: number, sw: number, sh: number)` → `ImageData` — Extracts pixel buffer for direct byte manipulation.
- `ctx.putImageData(imageData: ImageData, dx: number, dy: number)` — Writes raw pixel buffer back to canvas.
- `ctx.createImageData(width: number, height: number)` → `ImageData` — Allocates blank RGBA pixel buffer.

> [!WARNING]
> **To *measure* a render, do not loop over this.** `getImageData` is for writing pixels back; a JS loop that reads it to answer a question about the image is the reliable way to hit the 2,000,000-statement cap, which kills the whole script rather than the loop. A QA pass in a live run died at roughly 480k sampled pixels — well short of a single 700 × 700 frame.
>
> The questions have native answers that cost one call at any resolution: **`bitmap.diff`** (what changed, and where), **`bitmap.rowProfile`** (where a colour starts and ends on each row), **`bitmap.palette`** (the dominant colours and their shares). See `polson://sdk/core/Skia` under *Measuring an Image*, and `polson://manual/15` for how to use them as checks.

### Toolkit Shortcuts on the Context

Many `Drawing.*` and `Logo.*` methods are also available directly on `ctx`, with the leading context argument dropped: `Drawing.drawPerspectiveGrid(ctx, grid, options)` and `ctx.drawPerspectiveGrid(grid, options)` are the same call.

> [!IMPORTANT]
> **This list is exhaustive — taking a context as its first parameter is not enough.** Six toolkit
> methods that do take one have no shortcut: `drawLoomisWireframe`, `drawMannequinWireframe`,
> `drawMannequinSolid`, `drawComicEye`, `drawComicNose` and `drawComicMouth`. Call those as
> `Drawing.drawLoomisWireframe(ctx, head)`.
>
> The mannequin and hand pairs are the traps worth knowing, because each shortcut exists under a
> *different name* — **`ctx.drawHand(hand, solid)`** covers `drawHandWireframe` and `drawHandSolid`
> exactly as the mannequin one does:
> **`ctx.drawMannequin(figure, solid)`** covers both, with `solid` choosing between them. A live run
> lost two scripts guessing `ctx.drawMannequinWireframe` and `ctx.drawLoomisWireframe`, so it is worth
> checking the list above rather than assuming a shortcut exists.

`ctx.drawHand` · `ctx.drawPerspectiveGrid` · `ctx.drawPerspectiveBox` · `ctx.drawPerspectiveCylinder` · `ctx.renderVolumetricSphere` · `ctx.renderVolumetricCylinder` · `ctx.drawCastShadow` · `ctx.drawRimLight` · `ctx.drawMannequin` · `ctx.drawTorsoMusculature` · `ctx.drawCompositionGrid` · `ctx.drawLeadingLines` · `ctx.drawVignette` · `ctx.drawSquircle` · `ctx.drawEmblemBadge` · `ctx.drawGoldenSpiral` · `ctx.drawIsometricGrid` · `ctx.drawPolarGrid` · `ctx.drawClearSpaceGuide` · `ctx.generateFaviconScaleTest` · `ctx.generateMonochromeTest` · `ctx.generateBrandPresentationSheet`

Parameters and semantics are documented under `polson://sdk/core/Drawing` and `polson://sdk/core/Logo`. Use whichever reads better; the shortcut form suits long chains on one context.

> [!TIP]
> **Several of these hand geometry back, and the shortcut passes it through.** `ctx.drawTaperedStroke(...)` returns the mark it filled; `ctx.drawHand(hand, true, ...)` returns `{ silhouette, parts, bounds }`; `ctx.drawPerspectiveBox(...)` returns `{ faces, silhouette }`. Ignoring the return value behaves exactly as before, so nothing written against the old signatures changes.

> [!IMPORTANT]
> **`ctx.clip()` binds the toolkit too, not just the primitive canvas calls — and this is how you stage occlusion.** A `Drawing.*` or `Logo.*` call made inside a clip is constrained by it exactly as `fillRect` would be, so clipping to a region and then drawing a mannequin cuts the figure off at the boundary.
>
> The intuitive alternative does not work. Drawing the occluding form *after* the form it hides relies on the near shape painting over the far one, and on a construction sheet it does not: an outline hides nothing behind it. Without clip, there is no way to make a counter cut off a figure's legs — which is the commonest occlusion in any interior scene.
>
> ```javascript
> ctx.save();
> const counter = new CanvasPath();
> counter.rect(0, 0, 900, 470);          // everything above the counter edge
> ctx.clip(counter);
> Drawing.drawMannequinSolid(ctx, vendor);   // legs stop at the boundary
> ctx.restore();
> ```

### Constructive Drawing & Inking
- `ctx.drawTaperedStroke(start: Point | number, cp1: Point | number, cp2: Point | number, end: Point | number, maxThickness: number, fillOrStrokeStyle?: string | SKShader)` → `CanvasPath` — Subdivides cubic Bézier curve with sine-tapered normal envelope and anti-aliased fill, **returning the mark it filled**.
- `ctx.drawFeathering(origin: Point | number, angleDeg: number, count: number, length: number, spacing: number, strokeColor?: string, lineWidth?: number)` — Directional parallel hatching lines along shadow boundaries.
- `ctx.drawCrossContourHatch(cx: number, cy: number, rx: number, ry: number, startAngle: number, endAngle: number, count?: number, strokeColor?: string, lineWidth?: number)` — Elliptical cross-contour hatching arcs for cylindrical anatomical volumes.
- `ctx.drawHairRibbon(root: Point | number, tip: Point | number, bendFactor: number, width: number, fillTop: string | SKShader, fillUnderside: string | SKShader, strokeColor?: string, strokeWidth?: number)` — Twisting 3D hair ribbon with highlight facet, shaded underside streak, and contour outline.

---

# Skia

Skia procedural shaders, image filters, color matrix transforms, path effects, and bitmap manipulation.

## `Skia.Shader`

- `Skia.Shader.sksl(skslCode: string, uniforms?: object, children?: object)` → `SKShader` — Compiles and instantiates a custom **SkSL (Skia Shading Language)** procedural pixel shader executed headlessly on CPU/SIMD.
- `Skia.Shader.custom(skslCode: string, uniforms?: object, children?: object)` → `SKShader` — Alias for `sksl`.
- `Skia.Shader.perlinNoiseTurbulence(baseFreqX: number, baseFreqY: number, octaves: number, seed: number, tileSizeX?: number, tileSizeY?: number)` → `SKShader` — Generates procedural Perlin turbulence noise.
- `Skia.Shader.perlinNoiseFractal(baseFreqX: number, baseFreqY: number, octaves: number, seed: number, tileSizeX?: number, tileSizeY?: number)` → `SKShader` — Generates procedural fractal noise.
- `Skia.Shader.luminance(shader: SKShader)` → `SKShader` — The same shader with its RGB collapsed to Rec. 709 luminance and **its alpha left untouched**. This is what turns a noise field into a *value* field.

> [!WARNING]
> **The two Perlin shaders emit four independent noise fields — one per channel, alpha included — so the output is coloured noise, not a value field.** This is faithful to SVG `feTurbulence`, and it is precisely wrong for the commonest use, which is laying grain or vapour over a surface with `soft-light` or `overlay`: instead of modulating value, it **tints in random hues**. Measured on a 64×64 fill, 22 of 24 sampled pixels were non-grey with channels spread 109 apart out of 255. In a live run it turned roughly 700 × 500 px of brick from brown to olive green and cost a full iteration.
>
> Route every noise fill through `Skia.Shader.luminance(...)`:
>
> ```javascript
> ctx.fillStyle = Skia.Shader.luminance(Skia.Shader.perlinNoiseFractal(0.015, 0.015, 4, 101));
> ```
>
> **Do not "fix" the alpha in the same breath.** The natural next thought — clamp alpha to 1 in the colour matrix, since it is varying too — greys the noise correctly and turns atmosphere into an **opaque sheet**: the varying alpha is what makes vapour wispy. The correct transform is luminance on RGB, alpha untouched, which is a narrow path with a failure on either side of it. `luminance` is that path; a hand-written `colorMatrix` is where the second mistake gets made.
>
> `Drawing.createRopeFiberShader` and `Drawing.createAtmosphericCloudShader` apply it for you by default.
- `Skia.Shader.twoPointConical(x0: number, y0: number, r0: number, x1: number, y1: number, r1: number, colors: string[], positions?: number[], tileMode?: string)` → `SKShader` — 2-point conical gradient.
- `Skia.Shader.sweep(cx: number, cy: number, colors: string[], positions?: number[], startAngle?: number, endAngle?: number)` → `SKShader` — Sweep angular gradient.
- `Skia.Shader.linear(x0: number, y0: number, x1: number, y1: number, colors: string[], positions?: number[], tileMode?: string)` → `SKShader` — Linear gradient shader.
- `Skia.Shader.radial(cx: number, cy: number, radius: number, colors: string[], positions?: number[], tileMode?: string)` → `SKShader` — Radial gradient shader.
- `Skia.Shader.bitmap(bitmap: SkiaBitmapWrapper | SkiaCanvas, tileX?: string, tileY?: string)` → `SKShader` — Bitmap texture shader (`tileMode`: `"clamp"`, `"repeat"`, `"mirror"`, `"decal"`).

### Custom SkSL Shader Example
```javascript
const canvas = createCanvas(800, 600);
const ctx = canvas.getContext('2d');

// SkSL shader with uniforms
const sksl = `
    uniform float2 u_resolution;
    uniform float4 u_color1;
    uniform float4 u_color2;

    half4 main(float2 coord) {
        float2 uv = coord / u_resolution;
        float d = length(uv - 0.5) * 2.0;
        float ring = sin(d * 12.0) * 0.5 + 0.5;
        return mix(u_color1, u_color2, ring);
    }
`;

ctx.fillStyle = Skia.Shader.sksl(sksl, {
    u_resolution: [800, 600],
    u_color1: [0.1, 0.2, 0.8, 1.0],
    u_color2: [1.0, 0.6, 0.0, 1.0]
});
ctx.fillRect(0, 0, 800, 600);
```

## `Skia.ImageFilter`

- `Skia.ImageFilter.runtimeShader(skslCode: string, uniforms?: object)` → `SKImageFilter` — Compiles a custom SkSL image filter evaluating input pixels.
- `Skia.ImageFilter.blur(sigmaX: number, sigmaY: number)` → `SKImageFilter` — Gaussian blur.
- `Skia.ImageFilter.dropShadow(dx: number, dy: number, sigmaX: number, sigmaY: number, color: string)` → `SKImageFilter` — Drop shadow image filter.
- `Skia.ImageFilter.dilate(radiusX: number, radiusY: number)` → `SKImageFilter` — Morphological dilation.
- `Skia.ImageFilter.erode(radiusX: number, radiusY: number)` → `SKImageFilter` — Morphological erosion.
- `Skia.ImageFilter.colorFilter(filter: SKColorFilter)` → `SKImageFilter` — Bridges color filters into image filter pipelines.

## `Skia.ColorFilter`

- `Skia.ColorFilter.runtimeEffect(skslCode: string, uniforms?: object)` → `SKColorFilter` — Compiles a custom SkSL color filter (`half4 main(half4 inColor)`).
- `Skia.ColorFilter.colorMatrix(matrix20: number[])` → `SKColorFilter` — 4x5 color transformation matrix (20 floats).
- `Skia.ColorFilter.blend(color: string, blendMode?: string)` → `SKColorFilter` — Color tint blend filter.
- `Skia.ColorFilter.highContrast(grayscale?: boolean, invertStyle?: string, contrast?: number)` → `SKColorFilter` — High contrast filter (`invertStyle`: `"none"`, `"brightness"`, `"lightness"`).

## `Skia.PathEffect`

Assign to `ctx.pathEffect` to change what a stroke or fill is *made of*. These run natively, so a
textured stroke costs about what a plain one does.

- `Skia.PathEffect.dash(intervals: number[], phase?: number)` → `SKPathEffect` — Custom dash patterns (e.g. `[10, 5, 2, 5]`).
- `Skia.PathEffect.corner(radius: number)` → `SKPathEffect` — Rounds sharp polygon corners with specified radius.
- `Skia.PathEffect.discrete(segLength: number, deviation: number, seed?: number)` → `SKPathEffect` — Jittered / sketched line effect.
- `Skia.PathEffect.stamp(shape: CanvasPath | string, advance: number, phase?: number, style?: 'rotate' | 'translate' | 'morph')` → `SKPathEffect` — **The brush primitive.** Repeats `shape` along the path being stroked, every `advance` pixels. `'rotate'` (the default) turns each mark to follow the tangent, which is what makes bristles, foliage, grass and stitching read as drawn rather than pasted; `'translate'` keeps every mark upright; `'morph'` bends the mark to the curve. An `advance` below the mark's own width overlaps them into a continuous textured band; above it they read as separate marks. `shape` may be a `CanvasPath` or an SVG path string.
- `Skia.PathEffect.hatch(width: number, spacing: number, angleDeg?: number)` → `SKPathEffect` — Fills with parallel hatch lines **as geometry**, so they scale and export as vector. Cross-hatching is two of these summed at opposing angles.
- `Skia.PathEffect.tile(shape: CanvasPath | string, spacing: number, angleDeg?: number)` → `SKPathEffect` — Tiles `shape` on a square lattice: stipple, screen tone, a repeated motif. The geometric counterpart to `Drawing.createHalftoneDotShader` — a shader colours pixels, this places real shapes.
- `Skia.PathEffect.sum(first: SKPathEffect, second: SKPathEffect)` → `SKPathEffect` — Applies both to the original path and draws both results. Additive.
- `Skia.PathEffect.compose(outer: SKPathEffect, inner: SKPathEffect)` → `SKPathEffect` — Applies `inner` first, then `outer` to its result. Sequential.

> [!TIP]
> `sum` and `compose` are the difference between a mechanical stroke and a drawn one. `sum` places
> two treatments side by side — two hatches at opposing angles give cross-hatching. `compose` feeds
> one into the other, so jittering *underneath* a stamp makes the marks inherit the irregularity
> instead of marching evenly down a clean curve:
>
> ```javascript
> ctx.pathEffect = Skia.PathEffect.compose(
>     Skia.PathEffect.stamp(bristle, 4),      // then stamp along it
>     Skia.PathEffect.discrete(6, 2, 7));     // rough the path up first
> ```

## `Skia.MaskFilter`

Applied to a shape's coverage before it is painted, via `ctx.maskFilter`. Distinct from
`Skia.ImageFilter.blur`: a mask filter softens the **shape**, an image filter blurs the **result** —
so a soft mask leaves the fill flat, which is what an airbrushed edge is. Saved and restored with the
rest of the drawing state.

- `Skia.MaskFilter.blur(sigma: number, style?: 'normal' | 'solid' | 'outer' | 'inner')` → `SKMaskFilter` — Blurs the coverage mask. `'normal'` softens the whole shape (airbrush); `'solid'` keeps the shape crisp and adds the blur outside it (a glow around a hard form); `'outer'` keeps only the blur and knocks the shape out (a halo); `'inner'` keeps only the blur inside (an inward vignette).

## `Skia.Brush`

Drawing media, assembled from the primitives above. Nothing here is new capability — each preset is a
shader, a path effect and a mask filter you could put together yourself. They exist because that
assembly is not discoverable, and the naive attempts fail in both directions: multiplying a stroke's
alpha by noise luminance fades it to nothing, and tinting noise through a colour filter flattens it
back to a solid line.

- `Skia.Brush.pencil(color?, width?, grain?, seed?)` → `BrushPreset` — Graphite: granular deposit, a wandering line, no hard edge. `grain` is 0 for an even deposit, 1 by default, higher for a drier pencil.
- `Skia.Brush.ink(color?, width?, steadiness?)` → `BrushPreset` — Solid and crisp, with just enough irregularity not to read as a plotted line. `steadiness` 1 is mechanically exact. The three-tier weight hierarchy is roughly `width` 4, 2 and 1.
- `Skia.Brush.chalk(color?, width?, grain?, seed?)` → `BrushPreset` — Coarse, wide, soft-edged.
- `Skia.Brush.marker(color?, width?)` → `BrushPreset` — Flat, opaque, wide, faintly bled edge.
- `Skia.Brush.stipple(color?, size?, spacing?, seed?)` → `BrushPreset` — Separate deposits along the path. `spacing` defaults to three times `size`, because a spacing below the mark's own width overlaps the marks back into a solid band.

### `BrushPreset`

A preset says what it is made of, so you can take one and change a single part rather than starting over:

- `brush.name` → `string`, `brush.color` → `string`, `brush.lineWidth` → `number`, `brush.lineCap` → `string`
- `brush.grain` → `SKShader?` — what the mark is made of, when the medium deposits unevenly. Null for a solid medium.
- `brush.texture` → `SKPathEffect?` — what happens to the path: jitter, stamping. Null for a clean path.
- `brush.edge` → `SKMaskFilter?` — what happens at the mark's edge. Null for a hard edge.

### `ctx.useBrush(brush: BrushPreset)`

Applies a medium: its colour or grain, its width and cap, its path texture and its edge. Anything the
preset leaves null is **cleared** rather than left standing, so switching media never inherits the
previous one's texture. Wrap it in `save()`/`restore()` to scope a medium to one passage.

```js
ctx.save();
ctx.useBrush(Skia.Brush.pencil());     // construction lines
ctx.stroke(guides);
ctx.restore();

ctx.useBrush(Skia.Brush.ink('#0a0a0c', 4));   // heavy silhouette
ctx.stroke(outline);
```

## `Skia.Image` & `Skia.Bitmap`

- `Skia.Image.load(filePath: string)` → `SkiaBitmapWrapper` — Loads and decodes a raster image from disk (PNG, JPEG, WebP, BMP). **The path is relative to the project directory, exactly as `outFile` is**, so `Skia.Image.load('artifacts/stage1.webp')` reads back what `outFile: 'artifacts/stage1.webp'` wrote. A path resolving outside the project is refused with the same message a write gets.
- `Skia.Image.fromDataUrl(dataUrl: string)` → `SkiaBitmapWrapper` — Decodes base64 data URL.
- `Skia.Image.fromBytes(bytes: byte[])` → `SkiaBitmapWrapper` — Decodes raw byte buffer.
- `Skia.Bitmap.create(width: number, height: number)` → `SkiaBitmapWrapper` — Allocates a blank editable bitmap.

## `Skia.Font`

Which typefaces this machine can actually render. **Check before you commit to a typeface.** Skia substitutes a default face for a family it does not have — silently, with no error and no signal — so `ctx.font = '40px Didot'` renders and measures as something else entirely, and a wordmark can end up set in a face you never chose.

A **fallback list** in `ctx.font` is the other half of this: `'40px Didot, "Playfair Display", serif'` degrades to a face still in the right category, so the drawing survives a missing family without you asking. Ask here when you need to *know* which face you got — to caption a specimen, to pick between two directions, or to decide whether a design that depends on one face is viable at all.

- `Skia.Font.families()` → `string[]` — Every installed family, sorted.
- `Skia.Font.has(family: string)` → `boolean` — Whether `family` resolves to itself rather than a substitute.
- `Skia.Font.resolve(family: string)` → `string` — The family that would actually be used. When it differs from what you asked for, the request fell back.

```javascript
const wanted = ['Didot', 'Bodoni MT', 'Playfair Display', 'Garamond', 'Georgia'];
const usable = wanted.filter(f => Skia.Font.has(f));
log(`usable serifs: ${usable.join(', ')}`);   // pick from these, not from what you hoped for
```

## `SkiaBitmapWrapper`

- `bitmap.width` → `number` — Width in pixels.
- `bitmap.height` → `number` — Height in pixels.
- `bitmap.extractSubset(x: number, y: number, width: number, height: number)` → `SkiaBitmapWrapper` — Extracts cropped sub-bitmap.
- `bitmap.resize(width: number, height: number, quality?: string)` → `SkiaBitmapWrapper` — Resamples bitmap (`"linear"`, `"nearest"`).
- `bitmap.rotate(angleDeg: number)` → `SkiaBitmapWrapper` — Rotates bitmap by degrees.
- `bitmap.flip(direction?: string)` → `SkiaBitmapWrapper` — Flips bitmap (`"horizontal"`, `"vertical"`, `"both"`).
- `bitmap.getPixel(x: number, y: number)` → `string` — Returns hex color `"#RRGGBBAA"`. **This is how you verify a render.** Looking at an image tells you a layer is "too bright"; reading back a pixel tells you whether it is the colour you asked for, the alpha you asked for, or drawn at all — three very different problems that look identical on screen. Use `canvas.bitmap` (live) or `canvas.toBitmap()` (a copy) to get one from a canvas. The returned value is an ordinary string, so `getPixel(x, y) === '#3366CCFF'` is the way to assert a colour — note the alpha is the **last** pair, and hex digits come back uppercase.
- `bitmap.setPixel(x: number, y: number, color: string)` — Sets pixel color.
- `bitmap.applyFilter(filter: SKImageFilter)` → `SkiaBitmapWrapper` — Returns new bitmap with image filter applied.
- `bitmap.applyColorFilter(filter: SKColorFilter)` → `SkiaBitmapWrapper` — Returns new bitmap with color filter applied.
- `bitmap.toPngBytes(quality?: number)` → `byte[]` — Encodes to PNG byte array.
- `bitmap.toImageBytes(format?: string, quality?: number)` → `byte[]` — Encodes in any supported format (default WebP Q=85).
- `bitmap.toDataUri(format?: string, quality?: number)` → `string` — Base64 data URI in the given format.
- `bitmap.toDataUrl()` → `string` — Returns `data:image/png;base64,...` URL.
- `bitmap.clone()` → `SkiaBitmapWrapper` — Deep clones bitmap.
- `bitmap.dispose()` — Releases native bitmap memory.

### Measuring an Image

Verification primitives. **"Look again at what you rendered" means comparing it to something**, and doing that in script means looping over every pixel — slow, and the surest way to hit the statement cap. These run natively, so a full-frame measurement costs one call whatever the resolution.

- `bitmap.diff(other: SkiaBitmapWrapper, options?: { tolerance?: number, ignoreAlpha?: boolean })` → `object` — Compares two bitmaps. Returns `{ width, height, totalPixels, differingPixels, similarity, meanDelta, maxDelta, identical, bounds }`. `bounds` is the rectangle containing every differing pixel, or `null` when they match — **that is the part a score cannot tell you**. `tolerance` (default `8`) is per channel, because two renders of the same scene differ by a point or two along every antialiased edge.
- `bitmap.diffMap(other: SkiaBitmapWrapper, options?: { tolerance?: number, ignoreAlpha?: boolean, color?: string })` → `SkiaBitmapWrapper` — Where they differ, as an image: differing pixels marked, matching ones dimmed so the differences read at a glance.
- `bitmap.rowProfile(color: string, options?: { tolerance?: number, axis?: 'row' | 'column', minCount?: number })` → `object[]` — Where a colour class starts and ends on each row: `{ index, start, end, extent, count }`, rows matching nothing omitted. Pass `axis: 'column'` to profile the other way.
- `bitmap.palette(count?: number, options?: { buckets?: number })` → `object[]` — The dominant colours as `{ color, share, pixels }`. Colours are bucketed before counting, so antialiasing collapses into the flat colour it surrounds instead of producing thousands of near-duplicates. Fully transparent pixels are ignored.

> [!TIP]
> `rowProfile` is what makes a structural comparison affordable. It reduces an image to a few hundred rows of measurement, so comparing two images becomes a loop over **rows** rather than over pixels — roughly 1,400 iterations for two 700-row images, against nearly a million for the pixel-level equivalent.
>
> ```javascript
> const want = reference.rowProfile('#1f6f8b');
> const got = render.rowProfile('#1f6f8b');
> for (let i = 0; i < Math.min(want.length, got.length); i++) {
>     const dx = Math.abs(want[i].start - got[i].start);
>     if (dx > 8) log(`row ${want[i].index}: left edge off by ${dx}px`);
> }
> ```
>
> Differently sized bitmaps make `diff` and `diffMap` **throw** rather than comparing what overlaps — a size mismatch is a mistake about which images are being compared, and a similarity score over a partial overlap would look like an answer. Resize one first.

## `ImageData`

- `imageData.width` → `number` — Width in pixels.
- `imageData.height` → `number` — Height in pixels.
- `imageData.data` → `byte[]` — Flat array of RGBA byte values `[r0, g0, b0, a0, r1, g1, b1, a1, ...]`.
- `imageData.toPngBytes(quality?: number)` → `byte[]` — Encodes pixel buffer to PNG bytes.
- `imageData.toImageBytes(format?: string, quality?: number)` → `byte[]` — Encodes the buffer in any supported format (default WebP Q=85).
- `imageData.toDataUri(format?: string, quality?: number)` → `string` — Base64 data URI in the given format.
- `imageData.toDataUrl()` → `string` — Returns base64 data URL.

---

# Drawing (Constructive Drawing Toolkit)

Algorithmic drawing and constructive anatomy engine based on classical studio techniques (the Loomis method, 3D hair ribbons, and procedural comic shaders).

Also accessible via `Skia.Drawing`.

## Loomis Head & Feature Construction
- `Drawing.createLoomisHead(originX: number, originY: number, headHeight: number, yawDeg?: number, pitchDeg?: number)` → `object` — Computes all 3D head landmarks, proportional ratios (Rule of Thirds, 1/5th eye width), temporal ovals, eye sockets, nose wedge, mouth guides, and jaw angles.
- `Drawing.drawLoomisWireframe(ctx: CanvasRenderingContext2D, headObj: object, options?: { blueLineColor?: string, graphiteColor?: string })` — Renders non-repro blue (`#4a90e2`) and graphite (`#444444`) construction wireframe.
- `Drawing.drawComicEye(ctx: CanvasRenderingContext2D, eyeObj: object, isFar?: boolean, options?: { inkColor?: string, irisColor?: string, scleraColor?: string })` → `{ aperture, iris, pupil, catchlight, upperLid, lowerLid }` — Renders S-curve upper eyelid, shaded sclera, colored iris, pupil, and white catchlight, **returning each as a `CanvasPath`**.
- `Drawing.drawComicNose(ctx: CanvasRenderingContext2D, noseObj: object, options?: { inkColor?: string, shadowColor?: string })` → `{ underPlane, bridge, nostril }` — Renders nose bridge, apex, nostril, and under-plane shadow, **returning each as a `CanvasPath`**.
- `Drawing.drawComicMouth(ctx: CanvasRenderingContext2D, mouthObj: object, options?: { inkColor?: string, lipColor?: string, teethColor?: string, cavityColor?: string })` → `{ cavity, teeth, lipLine, lowerLip }` — Renders Cupid's bow upper lip, teeth shelf, mouth cavity, and lower lip shadow, **returning each as a `CanvasPath`**.

> [!TIP]
> **`aperture` is the one to reach for.** It is both the sclera fill and the clip the interior is drawn inside, so it is what a highlight, a reflected window, or a hard-edged shadow from the brow gets clipped to — and rebuilding it means re-deriving the eyelid curve from `inner`, `outer` and the eye's own width.
>
> ```javascript
> const eye = Drawing.drawComicEye(ctx, head.nearEye, false, { inkColor: '#15151a' });
> ctx.save();
> ctx.clip(eye.aperture);
> ctx.fillStyle = 'rgba(18,14,8,0.5)';
> ctx.fillRect(0, 0, W, head.brow.y);        // a brow shadow that stops exactly at the lid
> ctx.restore();
> ```
>
> `underPlane` and `cavity` are the face's two shadow shapes, so `nose.underPlane.union(mouth.cavity)` is one mass to re-fill when the light moves. The lids and `lipLine` come back as **open centre-lines** rather than filled marks, so they can be re-stroked at another tier's weight or run through `ctx.strokeToPath(...)` to be tapered.

## Inking, Feathering & Ribbons
- `Drawing.drawTaperedStroke(ctx: CanvasRenderingContext2D, start: Point | number, cp1: Point | number, cp2: Point | number, end: Point | number, maxThickness: number, fillOrStrokeStyle?: string | SKShader)` → `CanvasPath` — Smooth tapered Bézier inking stroke. Fills the mark **and returns it**.
- `Drawing.createTaperedStrokePath(start: Point | number, cp1: Point | number, cp2: Point | number, end: Point | number, maxThickness: number)` → `CanvasPath` — The same envelope with no context to paint it on, for laying marks out, measuring them, or combining a run of them before anything is drawn.

> [!TIP]
> **A mark you hold is geometry; a mark that was only painted is finished.** The returned path is what
> makes a stroke something to build on — `subtract` a bite out of it, fill it with a gradient running
> *across* the stroke rather than along the path, clip inside it, union a run of them into one
> silhouette and ink that once, or let it reach `outSvg` as vector rather than only as pixels.
>
> ```javascript
> const mark = ctx.drawTaperedStroke(a, c1, c2, b, 26, '#15151a');
> ctx.fill(mark.subtract(bite));            // the mark, cut
>
> let run = null;                            // or build first, draw once
> for (const s of strokes) {
>     const q = Drawing.createTaperedStrokePath(s.a, s.c1, s.c2, s.b, 14);
>     run = run === null ? q : run.union(q);
> }
> ctx.fill(run.simplify());
> ```
>
> **A script that ignores the return value behaves exactly as before.** One change worth knowing: the
> call no longer leaves the tapered outline as the context's *current* path, so a `beginPath()` you
> built before calling it survives.
- `Drawing.drawFeathering(ctx: CanvasRenderingContext2D, origin: Point | number, angleDeg: number, count: number, length: number, spacing: number, strokeColor?: string, lineWidth?: number)` — Directional feathering hatch lines.
- `Drawing.drawCrossContourHatch(ctx: CanvasRenderingContext2D, cx: number, cy: number, rx: number, ry: number, startAngle: number, endAngle: number, count?: number, strokeColor?: string, lineWidth?: number)` — Cross-contour cylindrical arcs.
- `Drawing.drawHairRibbon(ctx: CanvasRenderingContext2D, root: Point | number, tip: Point | number, bendFactor: number, width: number, fillTop: string | SKShader, fillUnderside: string | SKShader, strokeColor?: string, strokeWidth?: number)` — 3D twisting hair ribbon.

## Material & Shader Presets
- `Drawing.createHalftoneDotShader(options?: { dotSpacing?: number, shadowColor?: string, resolution?: number[] })` → `SKShader` — SkSL Ben-Day halftone dot shader.
- `Drawing.createRopeFiberShader(frequencyX?: number, frequencyY?: number, octaves?: number, seed?: number, luminanceOnly?: boolean)` → `SKShader` — Hemp rope/cordage texture shader.
- `Drawing.createAtmosphericCloudShader(frequencyX?: number, frequencyY?: number, octaves?: number, seed?: number, luminanceOnly?: boolean)` → `SKShader` — Atmospheric fractal cloud noise shader.

> [!NOTE]
> Both presets are **value fields by default** (`luminanceOnly: true`) — greyed on RGB with their alpha left varying, which is what the two-pass and `soft-light` recipes above assume. Pass `false` for the raw per-channel field; `Skia.Shader.perlinNoise*` is always raw. See the warning under `polson://sdk/core/Skia` for why the default is what it is.

## Measurement & Plumb Checks
- `Drawing.verifyPlumbAlignment(topPoint: Point, bottomPoint: Point, maxTolerance?: number)` → `{ aligned: boolean, deltaX: number, message: string }` — Validates vertical alignment between anatomical landmarks.
- `Drawing.computeRelativeDistance(headHeight: number, pointA: Point, pointB: Point)` → `number` — Computes distance in head-length units ($d / H$).

## Linear Perspective & 3D Forms
- `Drawing.createPerspectiveGrid(options?: { type?: '1point' | '2point' | '3point', horizonY?: number, centerOfVisionX?: number, focalLength?: number, cameraAngleDeg?: number, tiltAngleDeg?: number })` → `object` — Computes vanishing points ($VP_L, VP_R, VP_V$), horizon line, and center of vision.
- `Drawing.drawPerspectiveGrid(ctx: CanvasRenderingContext2D, gridObj: object, options?: { lineColor?: string, horizonColor?: string, lineCount?: number, lineWidth?: number })` — Renders horizon and perspective grid fan lines.
- `Drawing.createPerspectiveBox(gridObj: object, anchorX: number, anchorY: number, width: number, height: number, depth: number)` → `object` — Projects 3D box computing all 8 vertices ($V_0 \dots V_7$) and 6 quadrilateral faces. **`width` and `depth` are screen distances stepped along the rays to $VP_L$ and $VP_R$, not scene dimensions** — the same value at two depths is not the same size in the world.

> [!IMPORTANT]
> **A `width` or `depth` longer than 85% of the distance from the anchor to its vanishing point
> *throws*.** Past that the far corner reaches the vanishing point and the box turns inside out. This
> is a thrown error that **ends the script** — not a failure object like the `Assets.*` calls return,
> so there is no `success` field to check afterwards and nothing downstream runs. The message names
> the measured fraction, the anchor-to-vanishing-point distance, and the largest extent that would
> have been accepted.
>
> **The limit is computable before you call**, because the grid hands you the vanishing points as
> `{ x, y }`:
>
> ```js
> const reach = Math.hypot(grid.vpL.x - anchorX, grid.vpL.y - anchorY);
> const width = Math.min(wanted, reach * 0.85);      // or move the anchor away from the VP
> ```
>

- `Drawing.drawPerspectiveBox(ctx: CanvasRenderingContext2D, boxObj: object, options?: { topFill?: string, leftFill?: string, rightFill?: string, strokeColor?: string, strokeWidth?: number, drawHiddenLines?: boolean })` → `{ faces: { top, left, right, bottom, backLeft, backRight }, silhouette }` — Renders solid shaded or wireframe 3D perspective box, **returning every face as a `CanvasPath`** plus the three visible ones unioned.

> [!TIP]
> **A face is what you clip a texture to**, which is most of what a box in a scene is for — a crate's planking, a wall's brick, a floor's tiling all want the quad the projection produced:
>
> ```javascript
> const box = ctx.drawPerspectiveBox(grid, x, y, w, h, d, { strokeColor: '#15151a' });
> ctx.save();
> ctx.clip(box.faces.right);                 // planking on one face only, in its own perspective
> for (let i = 0; i < 20; i++) { /* … */ }
> ctx.restore();
> ```
>
> The **hidden faces come back whether or not `drawHiddenLines` drew them** — building a path is not drawing it, and a caller staging occlusion needs the back of the box precisely when it is not visible. `silhouette` is what a cast shadow, a rim light or an occluding clip wants instead of a single face.
- `Drawing.drawPerspectiveCylinder(ctx: CanvasRenderingContext2D, gridObj: object, anchorX: number, anchorY: number, radius: number, height: number, options?: { topFill?: string, sideFill?: string, strokeColor?: string, strokeWidth?: number })` — Draws an upright cylinder. **`anchorX`/`anchorY` is the centre of the base circle** and **`radius` is half the drawn width**, so the silhouette spans `anchorX ± radius` and you can check it with a ruler. Each cap is foreshortened at its own height, so the top ellipse is the flatter of the two, and both are drawn axis-aligned — an upright cylinder’s cap has a horizontal major axis, so its apex sits over the anchor wherever in the frame you put it.

> [!TIP]
> **There is no elevation parameter, and none is needed — put the anchor where the base actually is.** A cap's flatness is read from the directions to the vanishing points *at its own centre*, and a point nearer the horizon has shallower rays. So a bowl on a counter is flatter than the same bowl on the floor simply because you anchored it higher up the frame; nothing has to be told how high the counter is.
- `Drawing.subdividePerspectiveQuad(quadObj: Point[], uCount: number, vCount: number)` → `Point[][][]` — Subdivides a 4-point quadrilateral into foreshortened perspective cells using projective interpolation.
- `Drawing.verifyPerspectiveConvergence(linesList: Point[][], expectedVp: Point, maxToleranceDeg?: number)` → `{ passed: boolean, maxAngularErrorDeg: number, message: string }` — Tests whether drawn lines correctly converge to the vanishing point.

## Volumetric Lighting & Cast Shadows
- `Drawing.projectCastShadow(lightSource: Point, groundYOrGrid: number | PerspectiveGrid, objectVerticesOrBounds: PerspectiveBox | Rect | Point[] | Array<{ top: Point, base: Point }>, options?: { shadowColor?: string, opacity?: number, penumbraBlur?: number, groundDepth?: number })` → `object` — Projects the ground shadow footprint polygon. Argument 2 selects the ground model:
  - **A number** — a ground *line* at that Y (2D elevation). Every ray meets it at the same Y, so the footprint is given its recession by `options.groundDepth`, defaulting to ~20% of the contact width. Returns `{ shadowPolygon, model: 'groundLine', groundY, groundDepth, shadowColor, opacity, penumbraBlur }`.
  - **A `PerspectiveGrid`** — a ground *plane*. The light is read as the vanishing point of the light rays, so their ground projections converge on the horizon directly beneath it; each shadow vertex is the true intersection of the light ray with the ground ray through its contact point. No depth hint is used. Returns `{ shadowPolygon, model: 'perspective', horizonY, groundY, vpShadow, shadowColor, opacity, penumbraBlur }`.
  - In perspective mode the caster must carry contact points: pass a `PerspectiveBox`, a `Rect`, or `{ top, base }` pairs. A bare point list has tops only and throws. A light placed so the shadow lands at or beyond the horizon also throws, rather than returning coordinates at infinity.
- `Drawing.drawCastShadow(ctx: CanvasRenderingContext2D, shadowPolygonOrResult: object, options?: { shadowColor?: string, opacity?: number, blur?: number })` — Renders grounded cast shadow with contact occlusion crevice.
- `Drawing.renderVolumetricSphere(ctx: CanvasRenderingContext2D, cx: number, cy: number, radius: number, lightDirection?: Point, options?: { baseColor?: string, shadowColor?: string, highlightColor?: string, bounceColor?: string, drawGroundShadow?: boolean })` — Renders 6-zone tonal sphere (highlight, halftone, core shadow, ambient bounce, occlusion, and ground shadow).
- `Drawing.renderVolumetricCylinder(ctx: CanvasRenderingContext2D, x: number, y: number, width: number, height: number, lightDirection?: Point, options?: { baseColor?: string, shadowColor?: string, highlightColor?: string, bounceColor?: string })` — Renders cylindrical volume with longitudinal core shadow and ambient bounce.
- `Drawing.createThreePointLighting(options?: { keyAngleDeg?: number, fillAngleDeg?: number, rimAngleDeg?: number, keyColor?: string, fillColor?: string, rimColor?: string })` → `object` — Three-point studio lighting setup (Key, Fill, Rim/Kicker).
- `Drawing.drawRimLight(ctx: CanvasRenderingContext2D, boundsOrPts: Rect | Point[], lightAngleDeg: number, rimColor?: string, thickness?: number, options?: { spread?: number })` — Renders high-contrast silhouette rim lighting. `lightAngleDeg` points **from the form toward the light**, and the angle does the selecting: each stretch of contour is weighted by `max(0, n · L) ^ spread`, so the side facing away is not drawn and the lit arc fades toward the terminator. `spread` (default `2`) tightens the rim; `1` is a broad falloff across the whole lit half.

> [!IMPORTANT]
> **The point list must *be* the silhouette, not run near it.** The band is drawn just inside the contour you pass, and nothing here knows the form — a list that sits 10 px outboard of the figure produces a pale wire floating clear of it, which at 100% zoom looks like a rim and at full size is obviously wrong. Take the list from the geometry you actually drew.
>
> For a rim that has to follow a form exactly, a gradient fill inside a clipped shape is the sturdier construction: `ctx.clip(silhouette)` then fill a gradient running inward from the lit edge. `drawRimLight` is for the case where you already hold the contour as points.
- `Drawing.createVolumetricSphereShader(options?: { lightColor?: string, baseColor?: string, shadowColor?: string })` → `SKShader` — SkSL procedural 3D sphere lighting shader.

## Full-Body Anatomy, Mannequins & Expressions
- `Drawing.createMannequinFigure(originX: number, originY: number, totalHeight?: number, options?: { shoulderTiltDeg?: number, pelvicTiltDeg?: number, spineOffset?: number, shoulderSpanHeads?: number })` → `object` — Computes full 8-head proportional skeletal joint nodes (Head, Clavicles, Sternum, Ribcage, Spine, Pelvis, Hips, Knees, Ankles, Feet, Shoulders, Elbows, Wrists, Hands).
> [!TIP]
> **`shoulderSpanHeads` is in head units and defaults to `1.8`, which is narrower than any published canon** — it is a shoulder-*joint* span. The figure canons measure different things and all are usable: Loomis gives `2.33` for the figure at its widest and about `2.0` for the shoulder "cape"; Faragasso, after Reilly, gives `2.67` across. Pick one to suit the build you are drawing. See `polson://manual/08` §1.

The figure also reports what the pose did to it, which is what a later pass reads instead of keeping the pose object around:

- `figure.bounds` → `Rect` — the extent of every mass, as `{ x, y, width, height, x2, y2, cx, cy }`. Closed-form, so it costs no paths and is safe in a loop.
- `figure.head.angleDeg` → `number` — how far the head turned, `spineDeg + neckDeg`.
- `figure.ribcage.tiltDeg` → `number` — `shoulderTiltDeg + spineDeg`. `figure.pelvis.tiltDeg` is the pelvic tilt alone, because the pelvis is the pivot the spine leans over.

> [!IMPORTANT]
> **A posed figure's extent is not its height, and this is the commonest way a figure runs off a panel.** A thrown arm reaches far wider than the canon ever does: the same pose measured 345 × 1013 standing and **938 × 954** in a lunge. Size to `bounds`, never to `totalHeight`.
>
> ```javascript
> const e = Drawing.createMannequinFigure(0, 0, 1000, { pose }).bounds;   // one trial at a nominal height
> const s = Math.min(panel.width / e.width, panel.height / e.height);     // scaling is linear about the origin
> const fig = Drawing.createMannequinFigure(panel.x - e.x * s, panel.y - e.y * s, 1000 * s, { pose });
> ```

- `Drawing.createFigureGeometry(figureObj: object, options?: { padding?: number })` → `{ silhouette: CanvasPath, parts: object, groups: object, bounds: Rect, padding: number, order: string[] }` — The figure as **geometry** rather than as a drawing.
  - `silhouette` — every mass unioned into one contour. This is what you fill, clip to, stroke, or subtract from.
  - `parts` — a `CanvasPath` per mass: `neck`, `spine`, `shoulders`, `head`, `ribcage`, `pelvis`, and `leftUpperArm` / `leftForearm` / `leftHand` / `leftThigh` / `leftShin` / `leftFoot` and their `right` counterparts.
  - `groups` — the coarse six those masses belong to: `head`, `torso`, `leftArm`, `rightArm`, `leftLeg`, `rightLeg`. **These are what occlusion clips to.**
  - `order` — the order `drawMannequinSolid` paints in. **Construction order, not depth** — the toolkit has no z, so a pose where an arm passes behind the torso still needs you to say so.

> [!TIP]
> **`padding` is how a garment is derived.** Manual 22's premise is that cloth covers the figure *without fitting it*, and that gap is where every fold comes from — so a sleeve is the padded arm group rather than a second construction to keep in step with the pose:
>
> ```javascript
> const skin = Drawing.createFigureGeometry(fig);
> const cloth = Drawing.createFigureGeometry(fig, { padding: fig.headUnit * 0.08 });
> ctx.fill(skin.silhouette);
> ctx.fill(cloth.groups.leftArm);                       // a sleeve, already in the right place
> ctx.fill(cloth.silhouette.subtract(skin.silhouette)); // just the cloth, as its own shape
> ```
>
> Separate from `createMannequinFigure` because paths are not free: this builds about twenty native paths and some fifty boolean operations. A loop that only measures poses should call `createMannequinFigure` and read `bounds`, which costs neither.
>
> **Two known limits.** The `foot` landmark is a short stub in the canon, so the silhouette ends at the ankle rather than on a foot — add one if the shot shows it. And `order` is not depth, as above.

- `Drawing.drawMannequinWireframe(ctx: CanvasRenderingContext2D, figureObj: object, options?: { blueLineColor?: string, graphiteColor?: string, lineWidth?: number })` — Renders non-repro blue gesture and joint circle hinges.
- `Drawing.drawMannequinSolid(ctx: CanvasRenderingContext2D, figureObj: object, options?: { fillColor?: string, shadowColor?: string, strokeColor?: string, strokeWidth?: number })` — Renders shaded volumetric 3D masses (cranial sphere, ribcage egg, pelvic basin, limb cylinders, and box hands/feet).
- `Drawing.drawTorsoMusculature(ctx: CanvasRenderingContext2D, figureObj: object, options?: { strokeColor?: string, strokeWidth?: number })` — Renders clavicle handlebars, pectorals, deltoids, sternocleidomastoid cords, and the rectus abdominis as **eight** sections whose rows rise toward a peak above a flat row at the navel. The active side is read off the figure's own `shoulderTiltDeg` and compressed, per Hampton's squash/stretch rule — see `polson://manual/08` §3a.

### Hands

> **The hand is Loomis's two-unit scale** (*Drawing the Head and Hands*, Plates 78–79): the middle
> finger from its back knuckle is slightly over half the hand, the palm is the rest, and the palm is
> slightly more than half the hand wide. Everything derives from `handLength`. Full treatment,
> including which numbers are his and which are the studio's, in `polson://manual/19`.

- `Drawing.createHandFigure(originX: number, originY: number, handLength?: number, options?: { side?: 'right' | 'left', spreadDeg?: number, curlDeg?: number, thumbDeg?: number, rotationDeg?: number })` → `object` — Computes the whole hand: `unit`, `wrist`, `palm`, `midLine`, `thenar`, `fingers` (index, middle, ring, little — each with `knuckle`, three `joints` and a `tip`) and `thumb` (two joints). **`originX`/`originY` is the centre of the wrist**, and the hand runs toward the fingertips along `rotationDeg` (`0` points them up). `handLength` is wrist to middle fingertip.
- `Drawing.drawHandWireframe(ctx: CanvasRenderingContext2D, handObj: object, options?: { blueLineColor?: string, graphiteColor?: string, lineWidth?: number })` — Renders the construction: palm plate, the line through the middle of the palm, the thenar mass, the knuckle and joint arcs, and each digit jointed.
- `Drawing.drawHandSolid(ctx: CanvasRenderingContext2D, handObj: object, options?: { fillColor?: string, shadowColor?: string, strokeColor?: string, strokeWidth?: number })` → `{ silhouette, parts, bounds }` — Renders Plate 78's block forms: the palm as a slab, the thumb muscle as a wedge, every phalanx as its own tapering box. **Returns the hand as one contour** plus a `CanvasPath` per mass.

> [!IMPORTANT]
> **What it draws is a construction sheet; what you usually want is the silhouette.** Eleven boxes each with their own outline is the right picture for studying a hand and the wrong one for drawing it — at the fifteen pixels a hand occupies in a panel the interior lines are noise and the silhouette is the whole drawing.
>
> ```javascript
> const hand = Drawing.createHandFigure(x, y, 250, { side: 'right', curlDeg: 20 });
> const geo = ctx.drawHand(hand, true, { fillColor: 'rgba(0,0,0,0)', strokeColor: 'rgba(0,0,0,0)' });
> ctx.fillStyle = '#15151a';
> ctx.fill(geo.silhouette);                  // one shape, no interior seams
> ```
>
> `parts` is named by bone: `palm`, `thenar`, then `indexProximal` / `indexMiddle` / `indexDistal` and the same for `middle`, `ring` and `little`, plus `thumbProximal` and `thumbDistal` — the thumb has two phalanges, so it never gets a `Middle`.
>
> **The wireframe pass returns an empty object**, since it draws guides rather than masses; only `solid` has geometry worth building on.

> [!IMPORTANT]
> **`thumbDeg` defaults to `46`, not `90`, and that is deliberate.** Loomis says the thumb is turned at
> right angles to the other fingers — which describes the **plane it moves in** (in and out from the
> palm, where the fingers close toward it), not the angle it makes on the page. Drawn at a literal 90°
> it sticks straight out of the side of the wrist. `curlDeg` is applied to the thumb at half strength
> and mirrored, for the same reason.

> [!TIP]
> **The arcs are the check.** Loomis: the knuckles make a flat curve across the back, and the curves
> deepen row by row toward the fingertips. That falls out of the fingers differing in length, so
> comparing the sagitta of `fingers[i].knuckle` against `fingers[i].joints[2]` tests whether the
> proportions survived whatever you did to them. A hand whose rows are equally flat is a rake.

- `Drawing.applyFacialExpression(headObj: object, expressionType: 'joy' | 'anger' | 'fear' | 'sadness' | 'surprise' | 'disgust', intensity?: number)` → `object` — Modifies Loomis head brow, eye, and mouth landmarks according to the 6 universal muscle expressions.

## Compositional Armatures, Notan & Visual Emphasis
- `Drawing.createCompositionGrid(width: number, height: number, type?: 'ruleOfThirds' | 'goldenRatio' | 'dynamicSymmetry' | 'triangle', options?: object)` → `object` — Computes harmonic grid lines and focal power points.
- `Drawing.drawCompositionGrid(ctx: CanvasRenderingContext2D, gridObjOrType: object | string, options?: { lineColor?: string, pointColor?: string, lineWidth?: number, opacity?: number })` — Renders non-repro blue / cyan guide armatures on canvas.
- `Drawing.drawVignette(ctx: CanvasRenderingContext2D, width: number, height: number, options?: { vignetteColor?: string, intensity?: number, radius?: number })` — Renders smooth radial inverse falloff vignette.
- `Drawing.drawLeadingLines(ctx: CanvasRenderingContext2D, originPoints: Point[], focalPoint: Point, options?: { lineColor?: string, lineWidth?: number, opacity?: number })` — Renders directional guide vectors steering viewer gaze toward the subject.
- `Drawing.createNotanPalette(type?: 'binary' | 'classic3' | 'highKey' | 'lowKey')` → `object` — Returns curated tonal palettes for value keying.
- `Drawing.subdivideProportions(bounds: Rect, direction?: 'horizontal' | 'vertical', ratios?: object)` → `{ big: Rect, medium: Rect, small: Rect }` — Subdivides spatial layout according to the 70-20-10 ("Big, Medium, Small") proportional design law.

---

# Logo (Logo Design Toolkit)

Algorithmic logo design and brand identity toolkit based on George Bokhua's *Principles of Logo Design* (golden ratio geometry, optical balance, negative space, bone effect, rational gridding) and Tubik Studio's *Logo Design* (taxonomies, squircle app icon containers, multi-scale favicon stress-testing, and brand style boards).

Also accessible via `Skia.Logo`.

## Golden Ratio & Geometric Construction
- `Logo.createGoldenCircles(cx: number, cy: number, baseRadius: number, count?: number, direction?: 'growing' | 'shrinking')` → `{ circles: Array<{ cx: number, cy: number, radius: number, phiFactor: number }>, phi: number, bounds: Rect }` — Calculates concentric/tangent circle sets scaled by the Fibonacci ratio $\Phi = 1.61803398875$.
- `Logo.drawGoldenSpiral(ctx: CanvasRenderingContext2D, cx: number, cy: number, startRadius?: number, turns?: number, options?: { strokeColor?: string, lineWidth?: number, clockwise?: boolean, drawGoldenRectangles?: boolean, rectStrokeColor?: string })` — Renders logarithmic golden spiral ($r = a \cdot e^{b\theta}$).
- `Logo.createTangentBlend(p1: Point, corner: Point, p2: Point, radius: number)` → `{ arcStart: Point, arcEnd: Point, arcCenter: Point, tangentDistance: number, cornerAngleDeg: number, sweepAngleDeg: number }` — Calculates exact circular fillet tangency between two rays without broken kinks.

## Grids & Monograms
- `Logo.createIsometricGrid(width: number, height: number, cellSize?: number)` → `{ width: number, height: number, cellSize: number, dx: number, dy: number, nodeRows: Point[][] }` — Generates 30°/60° isometric construction grid.
- `Logo.drawIsometricGrid(ctx: CanvasRenderingContext2D, width: number, height: number, cellSize?: number, options?: { lineColor?: string, lineWidth?: number })` — Renders isometric grid guide lines.
- `Logo.createPolarGrid(cx: number, cy: number, rings?: number[], radialSlices?: number)` → `{ cx: number, cy: number, radii: number[], rayAngles: number[] }` — Polar concentric guide grid for roundels and circular crests.
- `Logo.drawPolarGrid(ctx: CanvasRenderingContext2D, cx: number, cy: number, rings?: number[], radialSlices?: number, options?: { lineColor?: string, lineWidth?: number })` — Renders polar guide grid.
- `Logo.createMonogramGrid(type?: '2x2' | '3x3' | 'diamond' | 'hex', size?: number, originX?: number, originY?: number)` → `{ type: string, size: number, nodes: Record<string, Point> }` — Node matrices for interlocking lettermarks and monograms.

## Graphic Devices & Enclosures
- `Logo.createSquirclePath(x: number, y: number, width: number, height: number, exponent?: number)` → `SKPath` — Generates continuous-curvature superellipse path ($|2x/w|^n + |2y/h|^n = 1$) for modern app icon containers.
- `Logo.createSquircleSvgPath(x, y, width, height, exponent?)` → `string`, `Logo.createGoldenSpiralSvgPath(startX, startY, initialRadius, turns?, segmentsPerTurn?)` → `string` — SVG `d` string variants of the squircle and spiral, for vector use.
- `Logo.createEmblemBadgeSvgPath(cx: number, cy: number, width: number, height: number, style?: string)` → `string` — The badge outline as an SVG `d` string rather than an `SKPath`.
- `Logo.drawSquircle(ctx: CanvasRenderingContext2D, x: number, y: number, width: number, height: number, options?: { fill?: string, stroke?: string, strokeWidth?: number, exponent?: number })` — Renders filled/stroked continuous curvature squircle.
- `Logo.drawEmblemBadge(ctx: CanvasRenderingContext2D, cx: number, cy: number, radius: number, type?: 'shield' | 'hexagon' | 'diamond' | 'scallop' | 'circle', options?: { fill?: string, stroke?: string, strokeWidth?: number, points?: number })` — Renders geometric emblem crest containers.
- `Logo.drawClearSpaceGuide(ctx: CanvasRenderingContext2D, markBounds: Rect, xDimension?: number, options?: { color?: string, fill?: string, showLabels?: boolean })` — Visualizes protective clear space boundary and dimension blocks ($X$).

## Optical Tuning & Perception Fixes
- `Logo.correctBoneEffect(p1: Point, p2: Point, strokeWidth: number, pinchCorrectionFactor?: number)` → `Point[]` — Computes 6-point outward parabolic polygon correcting optical dumbbell/bone narrowing illusion in connecting bars.
- `Logo.computeOvershoot(baseHeight: number, shape?: 'circle' | 'triangle' | 'arch')` → `number` — Computes $1.5\%–3.0\%$ vertical overshoot offset for circular and pointed apexes.
- `Logo.computeOpticalCenter(pointsOrBounds: Point[] | Rect, shapeType?: 'triangle' | 'arrow' | 'general')` → `Point` — Where to **place** the mark so it reads as centred, not where its mass currently sits. Returns a point above the geometric centre ($Y \approx 42\%–48\%$), because a shape centred by measurement looks low. `'triangle'` corrects hardest ($44\%$) since a tapering mark is the most bottom-heavy; `'arrow'` corrects on the $X$ axis instead, for a shape pointing right.
- `Logo.computeIrradiationCompensation(inkColor: string, backgroundColor: string, strength?: number)` → `{ lighterOnDarker: boolean, scale: number, deltaLuminance: number, inkLuminance: number, backgroundLuminance: number }` — How much to shrink a mark drawn **light on dark** so it reads the same size as its dark-on-light counterpart. A light shape on a dark ground appears larger than the same shape reversed, so a knockout set at identical dimensions looks bigger. Multiply your mark's size by `scale`. Dark-on-light is the reference case and always returns `1`. `strength` (default $1.5\%$) is the shrink at maximum contrast and is scaled by the actual luminance difference, so a near-equal pair is barely touched — tune it until the pair looks equal rather than trusting the default.

## Scale Stress-Testing & Brand Sheets
- `Logo.generateFaviconScaleTest(ctx: CanvasRenderingContext2D, drawMarkFn: (ctx: CanvasRenderingContext2D, size: number) => void, options?: object)` — Side-by-side multi-scale legibility ladder ($16\text{px}, 24\text{px}, 32\text{px}, 48\text{px}, 64\text{px}, 128\text{px}, 256\text{px}$).
- `Logo.generateMonochromeTest(ctx: CanvasRenderingContext2D, drawMarkFn: (ctx: CanvasRenderingContext2D, size: number) => void, width?: number, height?: number, irradiationStrength?: number)` — 4-way contrast test board (Positive Black, Negative White Knockout, Grayscale, App Icon Squircle). The two light-on-dark panels are shrunk slightly to cancel the irradiation illusion, and their labels report by how much; pass `irradiationStrength: 0` to see the uncompensated comparison.
- `Logo.generateBrandPresentationSheet(ctx: CanvasRenderingContext2D, options: { brandName: string, tagline?: string, primaryColor?: string, secondaryColor?: string, darkColor?: string, lightColor?: string, drawMark: (ctx: CanvasRenderingContext2D, size: number) => void })` — Complete executive brand identity presentation board with logo lockup, color chips, and clear space guides.

---

# VectorLogo & Snap.svg Logo Methods

Retained-mode SVG vector logo construction methods available directly on `SnapPaper` (`paper`), `Snap.path`, and the global `VectorLogo` object.

## SnapPaper Vector Methods
- `paper.squircle(x: number, y: number, width: number, height: number, exponent?: number)` → `SnapPath` — Appends a Lamé superellipse squircle path element to the paper.
- `paper.goldenSpiral(startX: number, startY: number, initialRadius: number, turns?: number, segmentsPerTurn?: number)` → `SnapPath` — Appends a logarithmic golden spiral path ($r = a \cdot e^{b\theta}$) to the paper.
- `paper.emblemBadge(cx: number, cy: number, width: number, height: number, style?: 'shield' | 'hexagon' | 'diamond' | 'scallop' | 'circle')` → `SnapPath` — Appends a geometric badge outline to the paper.
- `paper.goldenCircles(cx: number, cy: number, baseRadius: number, count?: number, options?: { lineColor?: string, lineWidth?: number, opacity?: number })` → `SnapGroup` — Appends a group containing $\Phi$-scaled concentric circles.
- `paper.isometricGrid(width: number, height: number, spacing?: number, options?: { lineColor?: string, lineWidth?: number, opacity?: number })` → `SnapGroup` — Appends a group containing 30°/60° isometric construction grid lines.
- `paper.polarGrid(cx: number, cy: number, maxRadius: number, ringCount?: number, rayCount?: number, options?: { lineColor?: string, lineWidth?: number, opacity?: number })` → `SnapGroup` — Appends a group containing polar concentric rings and radial spokes.
- `paper.monogramMatrix(x: number, y: number, width: number, height: number, type?: '2x2' | '3x3' | '4x4', options?: { lineColor?: string, lineWidth?: number, opacity?: number, nodeColor?: string })` → `SnapGroup` — Appends monogram matrix grid guides and node anchor circles.
- `paper.clearSpaceGuide(x: number, y: number, width: number, height: number, margin?: number, options?: { lineColor?: string, lineWidth?: number, innerColor?: string, fill?: string, fillOpacity?: number })` → `SnapGroup` — Appends clear space boundary guides and dimension blocks ($X$).
- `paper.svg(x: number, y: number, width: number, height: number)` → `SnapElement` — Appends a nested `<svg>` viewport with its own coordinate space.
- `paper.width` / `paper.height` → `number` — Document dimensions; both are settable.

### Path-String Variants (`VectorLogo`)

The same constructions as strings rather than elements, for when you want to compose or transform the `d` data yourself:

- `VectorLogo.createSquirclePath(x, y, width, height, exponent?)` → `string`
- `VectorLogo.createGoldenSpiralPath(startX, startY, initialRadius, turns?, segmentsPerTurn?)` → `string`
- `VectorLogo.createEmblemBadgePath(cx, cy, width, height, style?)` → `string`
- `VectorLogo.createTangentFilletPath(x1, y1, cornerX, cornerY, x2, y2, radius)` → `string`
- `VectorLogo.createBoneEffectPath(startX, startY, endX, endY, maxBulge?, controlT?)` → `string`
- `VectorLogo.createOgeeCurvePath(x1, y1, x2, y2, inflectionT?, amplitude?)` → `string`

## `VectorLogo` Paper Methods

The same constructions as `paper.*` above, but called on the global `VectorLogo` object with the target paper as the **first argument**. Use these when the paper is held in a variable rather than being the receiver; `paper.squircle(...)` and `VectorLogo.squircle(paper, ...)` produce identical elements, including the trailing `options` argument the armature helpers accept.

- `VectorLogo.squircle(paper: SnapPaper, x: number, y: number, width: number, height: number, exponent?: number)` → `SnapPath` — Appends a Lamé superellipse squircle.
- `VectorLogo.goldenSpiral(paper: SnapPaper, startX: number, startY: number, initialRadius: number, turns?: number, segmentsPerTurn?: number)` → `SnapPath` — Appends a logarithmic golden spiral.
- `VectorLogo.ogeeCurve(paper: SnapPaper, x1: number, y1: number, x2: number, y2: number, amplitude?: number, inflectionT?: number)` → `SnapPath` — Appends an Ogee S-curve.
- `VectorLogo.emblemBadge(paper: SnapPaper, cx: number, cy: number, width: number, height: number, style?: 'shield' | 'hexagon' | 'diamond' | 'scallop' | 'circle')` → `SnapPath` — Appends a geometric badge outline.
- `VectorLogo.goldenCircles(paper: SnapPaper, cx: number, cy: number, baseRadius: number, count?: number)` → `SnapGroup` — Appends $\Phi$-scaled concentric circles.
- `VectorLogo.isometricGrid(paper: SnapPaper, width: number, height: number, spacing?: number)` → `SnapGroup` — Appends 30°/60° isometric construction guides.
- `VectorLogo.polarGrid(paper: SnapPaper, cx: number, cy: number, maxRadius: number, ringCount?: number, rayCount?: number)` → `SnapGroup` — Appends polar rings and radial spokes.
- `VectorLogo.monogramMatrix(paper: SnapPaper, x: number, y: number, width: number, height: number, type?: '2x2' | '3x3' | '4x4')` → `SnapGroup` — Appends monogram matrix guides and node anchors.
- `VectorLogo.clearSpaceGuide(paper: SnapPaper, x: number, y: number, width: number, height: number, margin?: number)` → `SnapGroup` — Appends clear space boundary guides and $X$ dimension blocks.

## Path Shorthand Helpers (`Snap.path` & `VectorLogo`)
- `Snap.path.squircle(x: number, y: number, width: number, height: number, exponent?: number)` → `string` — Returns SVG `d` path string.
- `Snap.path.goldenSpiral(startX: number, startY: number, initialRadius: number, turns?: number, segmentsPerTurn?: number)` → `string` — Returns SVG `d` path string.
- `Snap.path.emblemBadge(cx: number, cy: number, width: number, height: number, style?: string)` → `string` — Returns SVG `d` path string.
- `Snap.path.tangentFillet(x1: number, y1: number, cornerX: number, cornerY: number, x2: number, y2: number, radius: number)` → `string` — Returns SVG `d` fillet path string.
- `Snap.path.boneEffect(startX: number, startY: number, endX: number, endY: number, maxBulge?: number, controlT?: number)` → `string` — Returns SVG `d` stem bulge path string.
- `Snap.path.ogeeCurve(x1: number, y1: number, x2: number, y2: number, amplitude?: number, inflectionT?: number)` → `string` — Returns SVG `d` Ogee S-curve path string.

---

# LogoType (Logotype & Typography Toolkit)

Algorithmic typography, font pairing, optical kerning, and automated brand lockup toolkit synthesized from Robin Williams (*The Non-Designer's Design Book*) and Doyald Young (*Fonts and Logos*).

Also accessible via `Skia.LogoType` and global `LogoType`.

## Optical Kerning & Spacing Mechanics
- `LogoType.computeOpticalKerning(charLeft: string, charRight: string, fontSize?: number, fontCategory?: string)` → `number` — Computes optimal character spacing in pixels based on glyph boundary silhouettes (straight-to-straight, straight-to-round, round-to-round, diagonal tucking).
- `LogoType.computeWordmarkTracking(fontSize?: number, isAllCaps?: boolean, role?: 'wordmark' | 'tagline')` → `number` — Optical letter-spacing for a whole run, as a **fraction of an em** — multiply by the font size for pixels. Tight for large display marks ($-20\text{‰}$ to $-50\text{‰}$), wide for all-caps taglines ($+150\text{‰}$ to $+300\text{‰}$).

> [!IMPORTANT]
> The two spacing calls return **different units**. `computeWordmarkTracking` is an em fraction and scales with the size you apply it at; `computeOpticalKerning` is already in **pixels** at the size you passed it. `tracking * fontSize` is comparable to a kerning value; `tracking` alone is not.
>
> Apply tracking with `ctx.letterSpacing`, which takes either unit — so the em fraction goes on as it comes back, with no multiplication to get wrong: `ctx.letterSpacing = LogoType.computeWordmarkTracking(48, true) + 'em'`.

## Typographic Scale & Font Harmony
- `LogoType.calculateTypographicScale(baseSize?: number, ratio?: 'goldenRatio' | 'perfectFifth' | 'augmentedFourth' | 'perfectFourth' | 'majorThird' | 'minorThird', stepsDown?: number, stepsUp?: number)` → `object` — Generates harmonic font size ladder (`micro`, `caption`, `body`, `h4`, `h3`, `h2`, `h1`, `display`).
- `LogoType.evaluateFontPairing(primaryCategory: string, secondaryCategory: string)` → `{ relationship: 'concordant' | 'conflicting' | 'contrasting', score: number, description: string, recommendations: string[] }` — Evaluates font pairing against the Robin Williams contrast matrix across Size, Weight, Structure, and Form.
- `LogoType.getGlyphShapeType(char: string)` → `string` — Classifies a glyph silhouette as straight, round, diagonal or open, which is what drives the kerning table.
- `LogoType.getRatioFactor(ratioName: string)` → `number` — The numeric multiplier behind a named harmonic ratio.

## Brand Lockups & Letterform Geometry
- `LogoType.createOgeeCurvePath(x1: number, y1: number, x2: number, y2: number, inflectionT?: number, amplitude?: number)` → `string` — Generates classical Doyald Young $S$-curve cubic Bézier path.
- `LogoType.createOgeeCurveSKPath(x1: number, y1: number, x2: number, y2: number, inflectionT?: number, amplitude?: number)` → `SKPath` — The same Ogee curve as a Skia path, for filling or stroking on a canvas.
- `LogoType.drawWordmarkLockup(ctx: CanvasRenderingContext2D, drawMarkFn: Function, brandName: string, tagline?: string, options?: { layout?: 'horizontal' | 'vertical', x?: number, y?: number, markSize?: number, fontSize?: number, taglineSize?: number, primaryColor?: string, taglineColor?: string, fontFamily?: string, taglineFontFamily?: string })` — Renders balanced brand lockup with optical alignment. `fontFamily` sets the wordmark's typeface (`taglineFontFamily` follows it unless given separately) — check it with `Skia.Font.has(...)` first, or it will be silently substituted. The tagline is automatically tracked using `computeWordmarkTracking`, so small all-caps taglines get the wide spacing they need.
- `ctx.drawWordmarkLockup(drawMarkFn, brandName, tagline, options)` — Direct canvas context helper.
- `ctx.drawOgeeCurve(x1, y1, x2, y2, amplitude, inflectionT)` — Direct canvas context helper.
- `paper.ogeeCurve(x1, y1, x2, y2, amplitude, inflectionT)` → `SnapPath` — Direct Snap.svg paper helper.

---

# Layout (Page Composition)

Rectangle arithmetic for composing a page: dividing a canvas into panels, padding them, and stacking measured blocks inside them.

Every method takes rectangles and returns rectangles. A rectangle is any `{ x, y, width, height }` object, which is what `element.getBBox()`, `Drawing.subdivideProportions(...)` and `ctx.measureWrappedText(...)` already return — so they compose without conversion. Returned rectangles also carry `x2`, `y2`, `cx` and `cy`.

> [!NOTE]
> This is deliberately **not** a layout engine: no document, no flow, no cascade, and nothing measures itself. Content-driven sizes come from `ctx.measureWrappedText(...)`; `Layout` only does the geometry. Keeping the two apart is what lets the same code place panels on a canvas, panels on an SVG paper, or panels of a comic page.

- `Layout.rect(x: number, y: number, width: number, height: number)` → `Rect` — Builds a rectangle.
- `Layout.inset(rect: Rect, top: number, right?: number, bottom?: number, left?: number)` → `Rect` — Shrinks inward, CSS-shorthand style: one value for all sides, two for vertical then horizontal, three for top / horizontal / bottom, four clockwise from the top. Clamps at zero rather than inverting.
- `Layout.outset(rect: Rect, top: number, right?: number, bottom?: number, left?: number)` → `Rect` — Expands outward; the inverse of `inset`.
- `Layout.rows(rect: Rect, divisions: number | number[], gap?: number)` → `Rect[]` — Divides into horizontal bands. A number gives equal bands; an array gives proportional weights, so `[70, 20, 10]` is Manual 09's big/medium/small law. Weights need not sum to anything in particular.
- `Layout.columns(rect: Rect, divisions: number | number[], gap?: number)` → `Rect[]` — The same, vertically.
- `Layout.grid(rect: Rect, columns: number, rows: number, gap?: number, rowGap?: number)` → `Rect[]` — Row-major, so cell `(row, column)` is at index `row * columns + column`.
- `Layout.stack(rect: Rect, heights: number[], gap?: number)` → `Rect[]` — Stacks boxes of **known heights** down the rectangle. The counterpart to `measureWrappedText`: measure each block, stack the heights, draw into what comes back.
- `Layout.center(rect: Rect, width: number, height: number)` → `Rect` — Centres a box of that size inside the rectangle.
- `Layout.place(rect: Rect, width: number, height: number, align?: string)` → `Rect` — Places a box at one of the nine anchors: `'topLeft'`, `'top'`, `'topRight'`, `'left'`, `'center'`, `'right'`, `'bottomLeft'`, `'bottom'`, `'bottomRight'`. Case and separators are ignored.
- `Layout.bounds(rects: Rect[])` → `Rect` — The smallest rectangle containing them all; what a clear-space guide or lockup box is measured from once the pieces are placed. Empty input returns a zero rectangle rather than throwing.

> [!TIP]
> Gaps are taken out of the total **before** dividing, so bands always sum back to the container. Dividing first and inserting gaps afterwards overflows by exactly the gap total, and is the commonest way a hand-rolled grid drifts off its page.
>
> `stack` places items even when they run past the bottom — compare the last rectangle's `y2` with the container's to detect it. Overflow is a fact worth seeing rather than something to clip silently.

```javascript
// A panel divided, padded, and filled with measured text.
const canvas = createCanvas(900, 420);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f4';
ctx.fillRect(0, 0, 900, 420);

const page = Layout.inset(Layout.rect(0, 0, 900, 420), 32);
const [left, right] = Layout.columns(page, [62, 38], 28);

ctx.textBaseline = 'top';
ctx.fillStyle = '#1c2733';
ctx.font = '700 30px sans-serif';
const heading = ctx.measureWrappedText('Measuring before placing', left.width);

ctx.font = '400 15px sans-serif';
const body = ctx.measureWrappedText(
    'Every block reports the box it will occupy, so the next one knows where to begin. ' +
    'Nothing here guesses at a height.', left.width, 23);

// Heights come from the content; the stack turns them into positions.
const [headBox, bodyBox] = Layout.stack(left, [heading.height, body.height], 18);
ctx.font = '700 30px sans-serif';
ctx.fillWrappedText('Measuring before placing', headBox.x, headBox.y, headBox.width);
ctx.font = '400 15px sans-serif';
ctx.fillWrappedText(
    'Every block reports the box it will occupy, so the next one knows where to begin. ' +
    'Nothing here guesses at a height.', bodyBox.x, bodyBox.y, bodyBox.width, 23);

// The right column: a grid of cells, each inset for its own padding.
for (const cell of Layout.grid(right, 2, 3, 10)) {
    const pad = Layout.inset(cell, 6);
    ctx.fillStyle = '#e7e2d8';
    ctx.fillRect(pad.x, pad.y, pad.width, pad.height);
}

canvas;
```

---

# Scale (Data to Pixels)

Maps data values onto pixels — the numeric spine of a chart. `Layout` answers where a *panel* goes; this answers where a *value* goes.

It draws nothing. Marks are ordinary `CanvasPath` and `ctx.fill` work, axis labels are ordinary text. Keeping the arithmetic separate from the drawing is what lets one scale serve a bar chart on a canvas, a sparkline in SVG, and an assertion in a test.

- `Scale.linear(domainStart: number, domainEnd: number, rangeStart: number, rangeEnd: number)` → `LinearScale` — A linear mapping. The **range may run backwards** (`rangeStart > rangeEnd`), which is the normal case for a vertical axis where larger values sit at smaller `y`.
- `Scale.band(count: number, rangeStart: number, rangeEnd: number, padding?: number)` → `BandScale` — Evenly spaced categorical bands. `padding` is the share of each step given to the gap (`0`–`1`); bars usually want `0.2`–`0.4`.
- `Scale.ticks(min: number, max: number, count?: number)` → `number[]` — Round tick values: steps of 1, 2 or 5 times a power of ten, so ticks land on `0 / 50 / 100` rather than on `0 / 47.5 / 95`. `count` is a target, not a promise — honouring it exactly is what forces ugly steps.
- `Scale.nice(min: number, max: number, count?: number)` → `{ min, max, span }` — Widens an interval outward to round numbers, so the first and last tick sit at the ends of the plot.
- `Scale.extent(values: number[])` → `{ min, max, span }` — The interval containing every value. **Feed every series through this once when drawing small multiples** — panels drawn to their own extents look comparable and are not.
- `Scale.radiusFor(value: number, maxValue: number, maxRadius: number)` → `number` — The radius that makes a circle's **area** proportional to its value.

## `LinearScale`

- `scale.map(value)` → `number` — The pixel position. **Not clamped**: a point off the plot is a fact about the data, and pinning it to the axis would hide the outlier worth seeing.
- `scale.clamp(value)` → `number` — The same, held inside the range.
- `scale.invert(position)` → `number` — The value at a pixel position.
- `scale.extent(from, to)` → `number` — Pixel distance between two values, always positive. This is what a bar's length is.
- `scale.ticks(count?)` → `number[]` — Round ticks across this scale's domain.
- `scale.domainStart` · `scale.domainEnd` · `scale.rangeStart` · `scale.rangeEnd` → `number`
- `scale.isZeroBased` → `boolean` — Whether the domain starts at zero, which bars and columns require.

## `BandScale`

- `band.map(index)` → `number` — The start of a band, its share of the gap already taken.
- `band.center(index)` → `number` — The band's centre, where its label belongs.
- `band.bandwidth` → `number` — The drawn width of one band. Let this do the division; it is where a hand-rolled bar chart drifts off its axis.
- `band.step` · `band.count` · `band.padding` · `band.rangeStart` · `band.rangeEnd` → `number`

> [!IMPORTANT]
> Three quantitative rules are worth knowing before drawing anything, because each fails **silently** — the numbers going in are correct and the picture still lies.
>
> - **Bars and columns need a zero baseline.** A bar's length *is* its value, so a non-zero baseline makes the ink claim something the data does not. `scale.isZeroBased` exists to be asserted on.
> - **Area is what the eye reads, not radius.** Sizing a circle by value directly shows a fourfold difference as sixteenfold. `Scale.radiusFor(...)` takes the square root for you.
> - **Small multiples must share one scale.** Per-panel extents make unlike things look alike — a lie told by the layout rather than by any single chart. `Scale.extent(...)` over all series, once.

```javascript
// A column chart: nice bounds, a zero baseline, bands for the categories.
const canvas = createCanvas(720, 380);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f4';
ctx.fillRect(0, 0, 720, 380);

const data = [
    { label: 'Mar', value: 38 }, { label: 'Apr', value: 61 },
    { label: 'May', value: 47 }, { label: 'Jun', value: 92 },
    { label: 'Jul', value: 74 }
];

const plot = Layout.inset(Layout.rect(0, 0, 720, 380), 40, 40, 56, 64);
const bounds = Scale.nice(0, Scale.extent(data.map(d => d.value)).max);

// Range runs bottom-to-top, so larger values sit higher up the canvas.
const y = Scale.linear(bounds.min, bounds.max, plot.y2, plot.y);
const x = Scale.band(data.length, plot.x, plot.x2, 0.3);
log('zero-based: ' + y.isZeroBased + ', ticks: ' + y.ticks(4).join(', '));

// Gridlines and axis labels come off the ticks, not off guesswork.
ctx.textAlign = 'right';
ctx.textBaseline = 'middle';
ctx.font = '400 12px sans-serif';
for (const tick of y.ticks(4)) {
    const ty = y.map(tick);
    ctx.strokeStyle = '#e3ded3';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(plot.x, ty);
    ctx.lineTo(plot.x2, ty);
    ctx.stroke();
    ctx.fillStyle = '#8a94a0';
    ctx.fillText(tick.toFixed(0), plot.x - 10, ty);
}

ctx.textAlign = 'center';
for (let i = 0; i < data.length; i++) {
    // The bar's length is the distance from the baseline to the value.
    const height = y.extent(bounds.min, data[i].value);
    ctx.fillStyle = '#1f6f8b';
    ctx.fillRect(x.map(i), y.map(data[i].value), x.bandwidth, height);

    ctx.fillStyle = '#1c2733';
    ctx.textBaseline = 'top';
    ctx.fillText(data[i].label, x.center(i), plot.y2 + 10);
}

canvas;
```

---

# Chart (Whole Charts as Constructions)

Builds a chart the way `Drawing.createMannequinFigure(...)` builds a figure: one call returns a **model** you can read, measure, restyle and animate. `Scale` maps values to pixels and `Layout` divides a page; this is the layer above them, and it exists because writing that loop by hand was sixty lines every time.

- `Chart.createColumnChart(rect, data, options?)` → `object` — Categories across, values up.
- `Chart.createBarChart(rect, data, options?)` → `object` — Categories down, values across. Usually the better of the two: horizontal bars give category labels room to be words.
- `Chart.createDotChart(rect, data, options?)` → `object` — Value as **position on one shared axis**, categories down. The most accurately decoded form there is, and the one Cleveland & McGill offer in place of a bar chart. Adds `radius` and `sort` (`'none'`, `'asc'`, `'desc'`) to the options.
- `Chart.createGroupedDotChart(rect, data, options?)` → `object` — The same, with rows gathered into labelled groups from a `group` field, **still against one axis**. Adds `groupGap` and `headingHeight`.
- `Chart.createFramedRectangleChart(rect, data, options?)` → `object` — A value as a **level inside an identical box**, placed anywhere. What Cleveland & McGill offer in place of a **shaded map**. Rows carry `x` and `y` in the plot's own coordinate space; rows without them are laid out on a grid. Adds `frameWidth`, `frameHeight`, `columns` and `gap`.
- `Chart.createCallout(rect, value, options?)` → `object` — One number, made big, with an optional `label` and `caption`. The right answer when there is only one value — a reader *reads* it rather than judging it. `compact: true` turns 1,234,567 into `1.2M`. Options: `label`, `caption`, `unit`/`suffix`, `prefix`, `decimals`, `compact`, `valueSize`, `labelSize`, `captionSize`, `align`.
- `Chart.createWaffle(rect, parts, options?)` → `object` — A grid of cells divided between parts. More honest than a donut, because a waffle can be *counted*. Options: `columns` (10), `rows` (10), `gap`, `total`, `labels`.
- `Chart.createSmallMultiples(rect, series, options?)` → `object` — A grid of panels **sharing one scale**, computed across every series before any panel is built. `series` is `[{ label, data }]` or an array of arrays; `form` picks what each panel is (`'column'`, `'bar'`, `'dot'`, `'groupedDot'`, `'framedRectangle'`). Adds `columns`, `gap`, `rowGap`, `titleHeight`.
- `Chart.createChartGeometry(chartModel)` → `{ marks: CanvasPath[], frames: CanvasPath[], silhouette: CanvasPath, bounds: Rect }` — The marks as real geometry. `frames` is populated for framed rectangles and empty otherwise.
- `Chart.drawChart(ctx, chartModel)` → the same geometry — Fills the marks with the context's current `fillStyle` and returns what it drew.

`rect` is any `{ x, y, width, height }`, so a `Layout` rectangle fits. `data` is an array of numbers, or of objects carrying `value` and optionally `label`.

`options`: `{ baseline, max, min, padding, tickCount, labels, labelGap, tickGap }`. **An unrecognised option is refused by name**, listing what is accepted — unlike an options object bound to a typed shape, a dictionary can see the misspelling and report it.

### What the model carries

| Field | |
| :--- | :--- |
| `type` | `'column'` or `'bar'` |
| `plot` · `bounds` | the rectangle the marks occupy |
| `scale` · `band` | the `LinearScale` and `BandScale` used, so you can place anything else against them |
| `bars` | *(bar/column)* one rectangle per datum, each with `x`, `y`, `width`, `height`, `x2`, `y2`, `cx`, `cy`, plus `index`, `value` and `label` |
| `dots` | *(dot)* one per datum with `cx`, `cy`, `radius`, `value`, `label`, `index`, `sourceIndex`, and `leaderX1/Y1/X2/Y2` for the line from the axis; grouped charts add `group` and `groupIndex` |
| `groups` | *(grouped dot)* `{ name, index, count, y, y2, height, headingX, headingY, min, max, mean }` — the block each group occupies, and its own summary |
| `items` | *(framed rectangle)* `{ label, value, anchorX, anchorY, frame, fill, fraction, index }` — the reference box, the filled part, and how full it is from 0 to 1 |
| `display` | *(callout)* the formatted number, plus `valueX/Y/Size`, `labelX/Y/Size`, `captionX/Y/Size` as anchors to draw at |
| `cells` · `parts` | *(waffle)* every cell with its `partIndex` and `filled`, and each part's `share`, `cells` and `firstCell` |
| `ticks` | `{ value, position, label, x, y }` — **data, not ink** |
| `labels` | `{ text, x, y, align, baseline, index }` for the categories |
| `baseline` · `baselinePosition` | the value, and the pixel it maps to |
| `encoding` · `encodingRank` | which perceptual judgment the chart spends — `'length'`, rank 3 |
| `isZeroBased` · `lieFactor` | whether the ink is proportional to the numbers |

> [!IMPORTANT]
> **`ticks` and `labels` are data rather than drawn marks, and `drawChart` deliberately draws neither.** The caller owns their typography, and declining to draw them *is* the erasing pass of `polson://manual/13` §3 — a decision rather than an edit to a function.
>
> **`lieFactor` is computed for you.** On a zero baseline it is exactly `1`, which is the point: it is not a rule to remember but the number that says whether the rules held. A truncated baseline reports the distortion — 100 and 104 drawn from a baseline of 96 gives `1.92` — and a baseline that clips a bar away entirely reports `Infinity` rather than some large finite number that might be mistaken for a measurement.

> [!IMPORTANT]
> **A dot chart's axis does not have to start at zero, and that is a consequence rather than a licence.** A bar claims a **ratio**, because its length *is* the quantity, so cropping the axis makes the ink assert something false. A dot claims a **difference**, because only its position carries meaning — and under any linear mapping the ratio of pixel distances equals the ratio of value differences wherever the axis begins.
>
> So `lieFactor` is `1` for a dot chart by the nature of the encoding, not by the baseline, and `isZeroBased` may be `false` without anything being wrong. Cropping to the data's own range is the ordinary thing to do: it is exactly what lets a dot chart show a spread that a zero-based bar chart flattens into five bars of nearly equal length.
>
> **`sort` is usually worth setting.** A form whose whole advantage is comparison gets most of that advantage from putting the values in order. Sorting reorders the rows but never the data — every dot keeps a `sourceIndex` back to the row it came from, so a parallel array of colours still lines up. On a **grouped** chart it sorts *within* each group, because that is the comparison grouping exists to support.

> [!TIP]
> **A grouped dot chart is one chart, not several — and that is the reason to prefer it over a panel per group.** Small multiples give each panel its own axis unless you force a shared one, which is the failure mode §2 warns about: panels that look comparable and are not. Here the scale is computed across every group at once and cannot drift apart.
>
> Rows carry their group in the data — `{ group: 'Nordics', label: 'Norway', value: 74.1 }` — and group order is first-seen, so a deliberate arrangement survives. Each group reports its own `min`, `max` and `mean`, which are awkward to recover once the rows have been sorted.
>
> **`Chart.createDotChart(...)` ignores a `group` field rather than grouping by it.** Worth knowing, because the alternative is silent: data shaped for the grouped call, passed to the plain one, draws a correct ungrouped chart and loses the structure without complaint. Ungrouped data passed the *other* way is fine — it becomes a single group rather than an error.
>
> A plot too short for its headings and gaps is **refused with the arithmetic** — how many rows, how many groups, and how much height went to chrome before any row was drawn — rather than silently stacking rows on top of each other.

> [!IMPORTANT]
> **On a framed-rectangle chart the frame is the mechanism, not decoration — do not draw the fills without them.** Cleveland & McGill are explicit: without frames these are "located bars" and the reader's task drops to perceiving **length**, rank 3. The identical frames are *"one step higher in the hierarchy"* — rank 2, position along identical but non-aligned scales. `createChartGeometry(...)` returns `frames` alongside `marks` for exactly this reason.
>
> It also fixes two faults of a shaded map that have nothing to do with the hierarchy. Shading a region makes its total ink the value **times its area** — on a US map Texas is imposing and Rhode Island is hard to see whatever the numbers say — and contiguous shaded regions merge into clusters the eye reads as structure whether or not any exists. Identical frames can do neither.
>
> **Each item reports `fraction`, not an absolute fill height, and the difference matters.** Frames sit at different places, so the fill's absolute `y` says more about where the item is than what it holds. An earlier version of this model reported the absolute level and a test caught it immediately: Texas at `y: 340` read as "lower" than North Dakota at `y: 90` while holding nine times the value. `fraction` is what the reader actually judges, and it is comparable between frames.
>
> **Positions are all-or-nothing.** If any row lacks `x`/`y`, the whole set is laid out on a grid — half a set on a map with the rest gridded over the top of it is a picture nobody wants and a mistake nothing downstream could report.

> [!IMPORTANT]
> **`createSmallMultiples(...)` exists to make one rule unbreakable rather than merely stated.** Panels drawn to their own extents look comparable and are not — the lie is told by the layout rather than by any single chart, and it is the one failure in `polson://manual/13` §2 that **no individual panel can detect**, because each is correct on its own terms. Assembled by hand it takes one forgotten `max` to get wrong:
>
> ```javascript
> const grid = Chart.createSmallMultiples(page, [
>     { label: 'North', data: north }, { label: 'South', data: south }, { label: 'West', data: west }
> ], { columns: 3, form: 'column' });
>
> for (const panel of grid.panels) {
>     ctx.fillStyle = '#1f6f8b';
>     Chart.drawChart(ctx, panel.chart);          // each panel is a whole chart model
>     ctx.fillText(panel.label, panel.titleX, panel.titleY);
> }
> ```
>
> **It returns charts, not pictures.** Every panel carries a complete model of the form you asked for, so `drawChart`, `createChartGeometry`, the ticks, the labels and the integrity fields all work on a panel exactly as on a standalone chart.
>
> **A shared scale does not excuse truncating it.** For `'column'` and `'bar'` the zero is forced into the shared domain, because sharing one scale across panels that each individually lie is not an improvement. For `'dot'` the domain crops to the union of the data, which is the whole advantage of the form.

> [!IMPORTANT]
> **A callout is not a chart, and that is why it wins.** It asks the reader to *read a numeral*, so no perceptual decoding happens at all and the answer is exact. That is the argument against a one-bar bar chart and a two-slice pie: both take a number the reader could have read and turn it into a judgment. Its `encodingRank` is **0**, which is this toolkit's marker for "no perceptual judgment" and **not** a rank from the literature — Cleveland & McGill's ordering starts at 1 and says nothing about reading text.
>
> The model gives **anchors and sizes**, not drawn text: measuring glyphs needs a context, and every other model here is closed-form arithmetic. What it saves you is the formatting and the size ladder.
>
> **A waffle reports itself as `area`, rank 4, deliberately.** A reader who counts cells gets an exact answer — that is why it beats a donut, whose angle is rank 3 but uncountable — but you cannot assume anyone will count. Rank 4 is what the graphic is worth if nobody does, and claiming the exactness of counting would be the flattering assumption rather than the safe one.
>
> **Cells are whole, so shares are apportioned by largest remainder.** Rounding each share on its own is the obvious approach and does not add up: three parts at a third each floor to 33 cells apiece and leave one of a hundred unassigned. Every cell is assigned and the counts sum to exactly the grid.

> [!TIP]
> **Each call accepts only its own options.** A single shared list would let `frameWidth` through on a bar chart and `compact` through on a waffle — names that mean nothing there — and the misspelling this check exists to catch would slip past whenever it happened to be another call's option. The error names what was passed and lists what is accepted **here**.

> [!TIP]
> **The model is closed-form and allocates no paths**, so measuring twenty-four panels is arithmetic. `createChartGeometry(...)` is the one that builds native geometry — the same split as `createMannequinFigure` and `createFigureGeometry`, and for the same reason.

```javascript
// One call for the chart; the drawing is what you choose to do with it.
const canvas = createCanvas(760, 380);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f4';
ctx.fillRect(0, 0, 760, 380);

const plot = Layout.inset(Layout.rect(0, 0, 760, 380), 40, 40, 56, 64);
const chart = Chart.createColumnChart(plot, [
    { label: 'Mar', value: 38 }, { label: 'Apr', value: 61 },
    { label: 'May', value: 47 }, { label: 'Jun', value: 92 }, { label: 'Jul', value: 74 }
]);

log(`lie factor ${chart.lieFactor}, zero-based ${chart.isZeroBased}, rank ${chart.encodingRank}`);

// Ticks are data — draw as much or as little of the chrome as the design wants.
ctx.font = '400 12px sans-serif';
ctx.textAlign = 'right';
ctx.textBaseline = 'middle';
for (const tick of chart.ticks) {
    ctx.strokeStyle = '#e3ded3';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(chart.plot.x, tick.position);
    ctx.lineTo(chart.plot.x2, tick.position);
    ctx.stroke();
    ctx.fillStyle = '#8a94a0';
    ctx.fillText(tick.label, tick.x, tick.y);
}

ctx.fillStyle = '#1f6f8b';
const geometry = ctx.fill ? Chart.drawChart(ctx, chart) : null;

// The marks came back as geometry, so one of them can be picked out without redrawing the rest.
const hero = chart.bars.reduce((a, b) => (b.value > a.value ? b : a));
ctx.fillStyle = '#c9553d';
ctx.fillRect(hero.x, hero.y, hero.width, hero.height);

ctx.fillStyle = '#1c2733';
ctx.textAlign = 'center';
ctx.textBaseline = 'top';
for (const label of chart.labels) ctx.fillText(label.text, label.x, label.y);

canvas;
```

---

# Css (Design Languages)

Reads a stylesheet as a **design language** — its tokens and its text styling — in a form the drawing context takes directly. It answers *what does `.h1` look like*: family, size, weight, colour, tracking.

It does **not** answer *where does it go*. There is no box model here; geometry stays with `Layout` and `ctx.measureWrappedText(...)`, which measure real glyphs.

- `Css.parse(html: string)` → `StyleSheetView` — Reads an HTML document, using the CSS in its `<style>` blocks.
- `Css.fromCss(css: string)` → `StyleSheetView` — Reads a bare stylesheet.
- `sheet.tokens()` → `object` — The CSS custom properties, already resolved, so a token defined as `var(--ink)` comes back as the colour. This is the palette, the type scale and the canvas size — the part of a design language most worth taking.
- `sheet.selectors()` → `string[]` — Every selector the sheet defines a rule for, in source order.
- `sheet.rule(selector: string)` → `Style?` — The declared style for that selector, or **`null`** when the sheet has no such rule; `if (!style)` is the check to write. The selector must match **as written** — this is a lookup, not a query.
- `sheet.rules()` → `Style[]` — Every rule, as declared styles.

A `Style` carries `{ font, fontFamily, fontSize, fontWeight, fontStyle, color, backgroundColor, letterSpacing, lineHeight, textAlign, textTransform, opacity, selector, properties }`. `font` is assembled as a CSS shorthand ready for `ctx.font`; `letterSpacing` keeps the unit it was written in, because `ctx.letterSpacing` resolves `em` against the size actually in force; `lineHeight` is resolved to **pixels**, including the unitless ratio form (`line-height: 1.5` at 20px → `30`). `properties` holds every declared property, for anything the named fields do not cover. The numeric fields are 32-bit floats, so they widen in JS — a line height of `15.6` reads back as `15.600000381469727`. Format before drawing or logging.

> [!IMPORTANT]
> This reads **declared rules, not a computed cascade** — so there is no inheritance and no specificity, and only rules with an identical selector are merged (later wins). A rule that sets no `color` reports an empty one rather than the colour it would inherit.
>
> That narrowing is deliberate and measured. AngleSharp's computed style *throws* on `font: … 46px/1.1 …`, on `%`, `vw` and `calc()`; it silently drops any shorthand containing `var()`; and it resolves `em` tracking against 16px rather than the element's own size. At the declared-rule level none of that happens. Custom properties are substituted in the source text first, so every `var()` is gone before the CSS is parsed.
>
> For the flat, class-per-element stylesheets a design language is written in, that is the whole of it. For a page leaning on inheritance, this is the wrong tool.

> [!NOTE]
> No resource loader is configured, so a `<link rel="stylesheet">`, an `@import` or an `<img src>` is **inert** — parsing untrusted markup never causes a network request from inside the sandbox. Only the CSS present in the string is read.

```javascript
// Take a design language's tokens and type, then draw with them.
const sheet = Css.fromCss(`
  :root { --ink: #1c2733; --accent: #f0b429; --font-body: 'Inter Tight', Helvetica, sans-serif; }
  .kicker { font: 600 13px/1 var(--font-body); letter-spacing: .18em;
            text-transform: uppercase; color: var(--accent); }
  .lede   { font: 400 17px/1.5 var(--font-body); color: var(--ink); }
`);

const tokens = sheet.tokens();
log('ink = ' + tokens['--ink'] + ', accent = ' + tokens['--accent']);

const canvas = createCanvas(720, 260);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f4';
ctx.fillRect(0, 0, 720, 260);
ctx.textBaseline = 'top';

const page = Layout.inset(Layout.rect(0, 0, 720, 260), 36);
const kicker = sheet.rule('.kicker');
const lede = sheet.rule('.lede');

// The style carries a ready-made ctx.font, so applying it is assignment, not translation.
ctx.font = kicker.font;
ctx.fillStyle = kicker.color;
ctx.letterSpacing = kicker.letterSpacing;
const kickerBox = ctx.fillWrappedText('DESIGN LANGUAGE', page.x, page.y, page.width);

ctx.letterSpacing = '0px';
ctx.font = lede.font;
ctx.fillStyle = lede.color;
const body = 'The stylesheet supplies the type and the palette; the toolkit supplies the geometry, ' +
             'because only it can measure what the glyphs actually do.';
const measured = ctx.measureWrappedText(body, page.width, lede.lineHeight);
const [lower] = Layout.stack(Layout.rect(page.x, kickerBox.y2 + 18, page.width, measured.height),
    [measured.height]);
ctx.fillWrappedText(body, lower.x, lower.y, lower.width, lede.lineHeight);

canvas;
```

---

# Assets (Cloud Asset Requisition)

Requisitions **raw material** from a cloud image model: flat tiling textures, background plates, and single-channel mattes. Everything returned needs code to become art — there is no call that produces a finished picture, by design. The model supplies what is hard to synthesise (the look of weathered oak); the drawing toolkits supply form, lighting and composition.

> [!IMPORTANT]
> Generation is metered and takes several seconds per call. **Requisition in its own short script, then draw in the next one** — a script that requisitions three assets can exceed the {{SCRIPT_TIMEOUT_SECONDS}}-second execution limit. Results are cached by content, so re-running an identical requisition is free and instant.

## Requisition Methods

> [!WARNING]
> **All three requisition calls are asynchronous — you must `await` them.** They return a `Promise`, so without `await` you get a `Promise` object on which `success`, `failureName`, `remedy` and every other documented property is `undefined`. That reads as a failure (`!undefined` is `true`) while the requisition still completes and **still spends budget** in the background. If a requisition appears to fail with `undefined: undefined`, you forgot the `await`. Top-level `await` is supported.

- `Assets.material(descriptor: string, options?: object)` → `Promise<MaterialAsset>` — A flat, seamlessly tiling swatch. Safest and most reusable: independent of geometry, so it survives any amount of redrawing. `options`: `{ size?: number (32–1024, default 512), tileable?: boolean (default true), format?: 'webp'|'png'|'jpeg', quality?: number, model?: string }`.
- `Assets.backdrop(descriptor: string, options?: object)` → `Promise<BackdropPlate>` — A background plate composited beneath the scene. `options`: `{ width?: number, height?: number, keepQuiet?: 'lowerThird'|'upperThird'|'leftHalf'|'rightHalf'|'center'|'none', noHorizon?: boolean, noForeground?: boolean, conditionOn?: byte[], format?: string, quality?: number, model?: string }`.
- `Assets.matte(descriptor: string, options?: object)` → `Promise<MatteAsset>` — A greyscale mask, height field, or displacement source for use as a shader input. `options`: `{ size?: number, invert?: boolean, model?: string }`.

## `Assets` Properties

- `Assets.budget` → `AssetBudget` — Remaining allowance. **Check this before requisitioning.**
- `Assets.classify(descriptor: string)` → `RequisitionVerdict` — `{ className, class, reason, triggers, allowed }`. `className` is the readable verdict — `'Substance'`, `'Form'` or `'Ambiguous'`; `class` is the same value as a number, so prefer `className`. The form-versus-substance pre-check, free and offline. `material()` applies it automatically; call it yourself to test a descriptor before spending.
- `Assets.library` → `MaterialAsset[]` — Every material requisitioned this session, by any agent. Reuse from here rather than requisitioning a near-duplicate. It is array-*like*, not a JS `Array`: `.length` and `library[i]` work, but `forEach` passes `undefined` as the index and the `Array.prototype` methods are absent. Use a `for` loop, or `Array.from(Assets.library)` to get a real array.

## Every Result Reports Its Own Failure

Requisition is a metered network call and can fail. Nothing throws — check `success`, then act on `remedy`:

- `result.success` → `boolean`
- `result.failureName` → `string` — the failure as a readable name: `'None'`, `'NotConfigured'`, `'BudgetExhausted'`, `'RefusedFormRequest'`, `'SafetyBlocked'`, `'Recitation'`, `'RateLimited'`, `'Quota'`, `'ConstraintNotMet'`, `'Network'`, `'Timeout'`, `'Auth'`, `'ModelNotFound'`, `'NoImageReturned'`, `'ServiceError'`, `'InvalidRequest'`, `'Cancelled'`. (`result.failure` is the same value as a number — prefer `failureName`.)
- `result.remedy` → `string` — What to do next, in words. Read this before retrying anything.
- `result.retryable` → `boolean` — Whether repeating the identical request could succeed. `false` means reword or give up.
- `result.error` → `string?` — The underlying message.

> [!TIP]
> `refusedFormRequest` means the descriptor named an **object** rather than a **material** — "a wooden ship" is refused, "weathered ship hull planking" is not. Draw the form with the drawing toolkit and requisition its surface.

## `MaterialAsset`

- `material.bytes` → `byte[]`, `material.size` → `number`, `material.mimeType` → `string`
- `material.toDataUri()` → `string` — Feed straight to `Skia.Image.fromDataUrl(...)` to get a bitmap.
- `material.tiling` → `TilingMetrics` — `{ horizontalSeamStep, verticalSeamStep, neighbourMedian, neighbourMax, wraps, repaired }`. `wraps` is guaranteed true on success; `repaired` says whether this layer had to fix it.
- `material.provenance` → `Provenance` — `{ model, hash, blockingHash, requester, generatedUtc, fromCache, prompt }`.
- `material.id` → `string`

## `BackdropPlate`

- `plate.bytes` → `byte[]`, `plate.width` / `plate.height` → `number`, `plate.mimeType` → `string`, `plate.toDataUri()` → `string`, `plate.id` → `string`, `plate.provenance` → `Provenance`
- `plate.metrics` → `PlateMetrics` — measured from the returned image, not assumed:
  - `metrics.keyLightX` / `metrics.keyLightY` → `number` — brightest mass in normalised [0,1] coordinates. **Feed this to `Drawing.drawRimLight` and `Drawing.projectCastShadow` so the foreground matches the plate's light.**
  - `metrics.bandLuminance` → `number[]` — mean luminance of the top, middle and lower thirds.
  - `metrics.quietRegionHonoured` → `boolean`, `metrics.hasBakedMask` → `boolean`
- `plate.boundTo` → `string?` — Set when the plate was conditioned on a blocking. A non-null value means the plate is welded to that silhouette and must be re-requisitioned if the foreground changes.
- `plate.isReusable` → `boolean`

## `MatteAsset`

- `matte.bytes` → `byte[]`, `matte.size` → `number`, `matte.id` → `string`, `matte.provenance` → `Provenance`

## `AssetBudget`

- `budget.total` / `budget.spent` / `budget.remaining` → `number`
- `budget.cacheHits` → `number` — Requisitions served from cache, which cost nothing.
- `budget.tokensSpent` → `number` — Actual tokens billed, as reported by the service.
- `budget.canAfford(count?: number)` → `boolean`

## Applying a Material

```javascript
// Script 1 — requisition only. Short, so it cannot hit the execution timeout.
const oak = await Assets.material('weathered ship hull planking, tarred caulking between planks');
if (!oak.success) { error(oak.remedy); exit(oak.failureName); }
Session.oakUri = oak.toDataUri();
log(`oak ${oak.size}px, wraps=${oak.tiling.wraps}, ${Assets.budget.remaining} left`);
```

```js
// Script 2 — draw. The material is already in the session scratchpad.
const canvas = createCanvas(1200, 760);
const ctx = canvas.getContext('2d');
const plank = Skia.Image.fromDataUrl(Session.oakUri);

const hull = new CanvasPath();          // the form is yours to construct
hull.moveTo(230, 400); /* … */
ctx.save();
ctx.clip(hull);
ctx.fillStyle = Skia.Shader.bitmap(plank, 'repeat', 'repeat');
ctx.fillRect(0, 0, 1200, 760);
ctx.restore();
canvas;
```

---

# Motion (Frame Capture & Animated Encoding)

> [!WARNING]
> **This is a spike.** It proves one path end to end — a script drives its own timeline, captures a frame per step, and saves an animated WebP — so that the authoring API above it can be designed against something that works rather than something imagined. The calls below may change. Nothing else in this reference depends on them.

- `Motion.frame(source: SnapPaper | SkiaCanvas | SkiaBitmapWrapper, width?: number, height?: number)` → `number` — Rasterises the **current state** of a paper, canvas or bitmap and keeps it as the next frame. Returns the frame count so far. A **copy** is taken, so a caller may keep drawing on the same paper.
- `Motion.save(filePath: string, options?: { fps?: number, frameMs?: number, quality?: number, lossless?: boolean })` → `{ path, frames, storedFrames, merged, width, height, frameMs, durationMs, bytes }` — Encodes the held frames as one animated WebP. `filePath` is contained exactly as `outFile` is. Frames are **kept** afterwards, so the same sequence can be saved twice at different qualities without redrawing it.
- `Motion.sheet(filePath: string, options?: { indices?: number[], count?: number, cols?: number, scale?: number, labels?: boolean, fps?: number, background?: string, labelColor?: string, fontFamily?: string, gap?: number, padding?: number, format?: string, quality?: number })` → `{ path, cells, indices, cols, rows, width, height, bytes }` — Tiles a selection of the held frames into one labelled image. Frames are named by `indices`, or spread evenly across what is held — **always including the first and last**, because the ends of a movement are what a reader checks first.
- `Motion.count` → `number` — How many frames are held.
- `Motion.clear()` — Discards them.

## The Score — `Motion.timeline()`

A **seekable score**. Entries are placed on a timeline and every one is a pure function of the time you ask for, so `tl.seek(t)` states the whole scene at `t` without having played anything before it.

- `Motion.timeline(options?)` → `tl` — A new score. `options` is `{ defaults?: { dur?: number, easing?: fn } }`; `dur` defaults to 500 ms and `easing` to linear.

### Adding to the score

- `tl.tween(from: number, to: number, setter: (v: number) => void, opts?)` → `tl` — A scalar tween: `setter` is called with the eased value on every seek. **This is the case an animated SVG cannot express** — procedural geometry recomputed per frame rather than an attribute a renderer knows how to interpolate.
- `tl.to(element: SnapElement, attrs: object, opts?)` → `tl` — Tweens attributes from their current values to the ones given. Numbers and colours interpolate; anything else throws, naming the attribute.
- `tl.set(element: SnapElement, attrs: object, opts?)` → `tl` — A step: the base value before `at`, the given value at and after it. Takes values no tween could interpolate — a font, a dash pattern, a gradient reference.
- `tl.show(element: SnapElement, opts?)` → `tl` — A visibility window, `{ from, to }`. Omit `to` and it stays visible.
- `tl.stagger(elements: SnapElement[], attrs: object, opts?)` → `tl` — One `to` per element, each offset by `each` milliseconds (default 100).
- `tl.label(name: string, at?)` → `tl` — Names a position, defaulting to the current end.

`opts` is `{ at?: number | string, dur?: number, easing?: (n: number) => number, each?: number, from?: number, to?: number }`. All times are **milliseconds**.

### Reading and seeking

- `tl.seek(ms: number)` → `tl` — Applies every entry at `ms`. Chainable.
- `tl.at(ms: number)` → `tl` — Alias for `seek`; reads better in a render loop.
- `tl.duration` → `number` — The end of the last entry to finish: the **max** of every `at + dur`, not their sum.
- `tl.count` → `number` — How many entries.
- `tl.labels` → `object` — Every label and the millisecond it names.

### The position grammar (`at`)

**Omitted means "at the end of the timeline"** — sequential append, which is what you want most of the time. This is what makes a long score editable: cutting 400 ms from one beat moves everything after it, where absolute numbers would each need hand-editing.

| Form | Meaning |
| :--- | :--- |
| `1200` | absolute, 1200 ms |
| `'+=200'` | 200 ms after the timeline's current end |
| `'-=200'` | 200 ms before it — an overlap |
| `'<'` / `'>'` | at the **start** / **end** of the previous entry |
| `'<+=100'` / `'>-=50'` | offset from the previous start / end |
| `'chartIn'` | at that label |
| `'chartIn+=200'` | offset from a label |

`'<'` and `'>'` on an empty timeline are `0`. **An unknown label throws and names itself**, listing the labels that do exist — resolving it to 0 would place the beat at the start of the film and animate happily.

```javascript
const paper = Snap(480, 270);
const disc = paper.circle(90, 135, 30).attr({ fill: '#1f6f8b' });
const bar = paper.rect(60, 200, 0, 14).attr({ fill: '#c9553d' });

const tl = Motion.timeline({ defaults: { dur: 400, easing: mina.easeinout } });
tl.label('open');
tl.to(disc, { cx: 240, r: 55 }, { at: 'open' });
tl.to(disc, { fill: '#c9553d' }, { at: '<+=150', dur: 250 });
tl.to(bar, { width: 360 }, { at: '>', dur: 500, easing: mina.backout });

for (let i = 0; i * 40 <= tl.duration; i++) { tl.seek(i * 40); Motion.frame(paper); }
Motion.sheet('artifacts/score-sheet.png', { count: 6, cols: 6, scale: 0.5, fps: 25 });
paper;
```

> [!IMPORTANT]
> **Every entry answers for any `t`, including outside its own window** — before it starts it applies its *from* value, after it ends it holds its *to*. That is what makes seeking absolute: frames can be rendered in any order, a visited time reproduces exactly, and a run can be resumed. Two consequences follow, and both bite in practice:
>
> - **Anything you mutate outside the score does not revert on a backwards seek.** Put every state change in the score, or rebuild the scene per frame.
> - **`to(...)` captures its base value when it is *added*, not when it first runs.** Change the attribute afterwards and the tween still starts from what it captured. This is the only deterministic choice a seekable score has — there is no "first run" to capture at.
>
> **Later entries win** where two touch the same property at the same time; they apply in the order you added them.

> [!TIP]
> **A tween needs somewhere to start from, and it will tell you when it has none.** `tl.to(circle, { r: 40 })` on an element with no `r` throws rather than guessing — SVG's defaults differ per attribute (`opacity` starts at 1, `cx` at 0), so a single guess would be wrong half the time and would start the move at the wrong end. Give the element the attribute first, or use `tl.set(...)`.
>
> An **unknown option is ignored silently** — `{ durr: 400 }` binds and does nothing — because the binding cannot report a name it was never told about. If an entry runs for the default 500 ms when you asked for something else, check the spelling of `dur`.

> [!IMPORTANT]
> **`sheet` is the artifact to look at; `save` is the artifact to ship.** An agent cannot watch a video — it reads images — so a moving file is close to the worst thing to hand it for inspection: it can produce one and still not perceive the motion. A contact sheet is a **single read**, and unlike a video it supports comparison: the eye works across cells, and so does `bitmap.diff`.
>
> ```javascript
> for (let i = 0; i < frames; i++) { seek(i * 40); Motion.frame(paper); }
> Motion.sheet('artifacts/stage3-sheet.png', { count: 6, cols: 6, scale: 0.6, fps: 25 });
> Motion.save('artifacts/stage3.webp', { fps: 25 });
> ```
>
> **This is also why an animated file is the wrong thing to hand the *next stage*.** Animated WebP is frame-differenced: measured on a 47-frame file, exactly **one** frame was independently decodable and the rest chained, so reaching frame 36 cost 13 ms against 1 ms for frame 0 — random access is O(n) and gets worse with length. Transfer the script and a stage SVG instead, and re-evaluate the timeline to reach a time; that is O(1) at any `t`.

> [!IMPORTANT]
> **`frames` is what you handed in; `storedFrames` is what the file holds, and they differ legitimately.** The WebP encoder merges consecutive **pixel-identical** frames and sums their durations — a real size win, and lossless — so a sequence that holds still for half a second stores one long frame rather than twelve short ones. `durationMs` is unaffected. A 57-frame capture whose opening is motionless stored 47 frames and played for exactly the same 2280 ms.
>
> Every frame must be the same size; a mismatch is refused with both sizes named rather than silently letterboxed. Frames are uncompressed bitmaps, so there is a ceiling on retained pixels — save, `clear()`, or draw a smaller board.

> [!TIP]
> **The timeline is not here, and does not need to be.** A tween is an easing — a pure function of `0..1`, and `mina` already supplies all nine — applied to a setter. So the whole model is a few lines of JavaScript over calls that already exist, and the status comes from a seek instead of a clock:
>
> ```javascript
> const tweens = [];
> function tween(from, to, set, at, dur, easing) {
>     const ease = easing || mina.linear;          // a local, so the default reads plainly
>     tweens.push({ from, to, set, at, dur, ease: n => ease(n) });
> }
> function seek(ms) {
>     for (const t of tweens) {
>         const raw = (ms - t.at) / t.dur;
>         const s = raw < 0 ? 0 : raw > 1 ? 1 : raw;   // clamped, so a finished tween holds
>         t.set(t.from + (t.to - t.from) * t.ease(s));
>     }
> }
>
> for (let i = 0; i < 57; i++) { seek(i * 40); Motion.frame(paper); }
> Motion.save('artifacts/shot.webp', { fps: 25 });
> ```
>
> Because every tween is a pure function of its own status, **seeking is order-independent**: frames can be rendered in any order, and re-seeking a time already visited reproduces it exactly.

> [!NOTE]
> **An easing may be stored on an object and called through it.** `{ ease: mina.elastic }` followed by `t.ease(0.5)` works, as does every other position — called directly, from a local, from an array element, and passed into a function that did not create it.
>
> This is worth stating because it used to **throw**: *"Object type Polson.Drawing.Svg.Mina does not match target type System.Dynamic.ExpandoObject"*, a message naming neither easings nor the line responsible. JavaScript binds `this` to the containing object, and the interop layer then took that object for the CLR receiver. Every easing is now a delegate, which carries its own target, so `this` never enters into it. The `ease: n => ease(n)` wrapper in the example above is therefore no longer necessary — it is left as written because capturing into a local is still the clearer way to apply a default.
