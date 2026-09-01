# Polson JavaScript SDK — Core Reference

This is the **core** reference for the Polson JavaScript (JS) SDK — the typed drawing, graphics, and vector API exposed to scripts executed inside the Polson MCP server's sandboxed JavaScript engine. It covers the **execution model** (the rules every generated script must follow) and the **method signature index** for all top-level objects: each method's purpose, parameter types, and return values.

The **JSON schema for every parameter and return model type** named below lives in the companion schema document (`Polson.schema.md`). Both documents are served **sliced by subject area**: read `polson://sdk/index` for the map, `polson://sdk/core/{Area}` for an area's methods, and `polson://sdk/schema/{Area}` for the fields of what they return. This entire document is also available at `polson://sdk/core/all`.

---

## Execution model

Scripts execute within a secure, sandboxed [Jint](https://github.com/sebastianros/jint) runtime supporting **ECMAScript 2025** (arrow functions, `let`/`const`, destructuring, template literals, optional chaining `?.`, nullish coalescing `??`, `for...of`, spread `...`, `Array`/`Map`/`Set`/`JSON`, etc.). Tailor generated code to modern JavaScript idioms.

- **Async & `await`:** `async`/`await`, Promises and **top-level `await`** are supported. Almost the entire SDK is synchronous — the exceptions are the three `Assets.*` requisition calls, which reach a cloud service and **must be awaited**. An unawaited Promise silently reports every property as `undefined`; see the warning under `polson://sdk/core/Assets`.
- **Sandbox Security:** `eval` and `new Function` are strictly disabled (`Host.StringCompilationAllowed = false`). Arbitrary external types and reflection are prohibited. Scripts can only interact with the explicit Polson drawing APIs.
- **A misspelled member is an error, not a new property.** Scripts run in **strict mode**, and reading or writing a member that does not exist on an SDK object **throws**, naming the nearest real member: `ctx.fillStlye = '#f00'` fails with *"Did you mean 'fillStyle'?"* rather than silently doing nothing. Assigning to a real but read-only member (`canvas.width`) says it is read-only. A typo'd **variable** is likewise an error rather than a new global.
  > [!NOTE]
  > This applies to SDK objects only. Plain JavaScript objects, arrays, `Map` and the `Session` scratchpad keep ordinary JS semantics, so `Session.neverSet` is still `undefined`.
  >
  > One consequence worth knowing: `?.` and `typeof` do **not** make a *missing member* safe on an SDK object — `ctx.someFutureThing?.x` throws, because the failure is the unknown member rather than a null value. Optional chaining still works for values that may legitimately be null, which is what it is for here: `result.bounds?.width` is fine, because `bounds` exists and is documented as sometimes null.
- **Execution Limits:** Scripts are enforced with statement limits (2,000,000 statements, configurable via `JsDrawingEngine.MaxStatements`), recursion depth limits (100 frames), and execution timeouts ({{SCRIPT_TIMEOUT_SECONDS}} seconds).
  > [!TIP]
  > For heavy pixel-level manipulation (such as procedural textures, blurs, or color grading), use native **`Skia.Shader`** or **`Skia.ImageFilter`** pipelines which execute in native SIMD/C++ in < 1ms, rather than running millions of raw per-pixel loop iterations in interpreted JS.
- **Return Value & Visual Rendering:**
  - Returning a `SnapPaper` (or a `SnapElement`), `CanvasRenderingContext2D`, `SkiaCanvas`, `SkiaBitmapWrapper`, or `ImageData` automatically renders the visual output headlessly to image bytes (`result.ImageBytes`, defaulting to **WebP at quality=85**, with `"png"` and `"jpeg"` options available) and Base64 URI (`result.ImageDataUri`).
  - **Direct-to-Disk Rendering (`outFile`, `outSvg`):** Agents can pass `outFile` (e.g. `'artifacts/stage1.webp'`) to write the rendered image directly to disk, and `outSvg` (e.g. `'artifacts/stage1.svg'`) for vector markup. **Both are relative to the project directory, and a path resolving outside it is refused** — an absolute path or a `..` traversal fails with a message naming the project root rather than writing somewhere unexpected. Missing intermediate directories are created for you. When `outFile` is supplied, `result.ImageFilePath` contains the saved path and `result.ImageBytes` is omitted by default to eliminate token bloat in LLM contexts (use `includeBytes: true` to force inclusion).
    > [!IMPORTANT]
    > **`outSvg` needs a vector document to write.** It saves `result.SvgXml`, which only exists when the script built a `SnapPaper`. A script that draws entirely on a raster canvas has no markup to save, so `outSvg` writes **no file** and the run still reports success — the response carries a `[WARN] outSvg … wrote nothing` line, but by then the stage is drawn. **If the brief asks for an SVG, build the scene on `Snap(width, height)` from the first script.** Read `polson://manual/14` before choosing the surface.
  - For vector scenes (`SnapPaper` / `SnapElement`), `result.SvgXml` contains the serialized SVG XML markup. For 2D canvas raster scripts, `result.SvgXml` retains the last vector image produced by the agent prior to switching to 2D canvas mode.
  - If a script creates one or more canvases or Snap papers without explicitly returning them, the last created canvas/paper is rendered automatically.
- **Running a file instead of re-sending it (`scriptFile`):** Pass `scriptFile: 'artwork.js'` — a path relative to the project directory, contained exactly as `outFile` is — to execute a file you maintain rather than putting the whole program in the call. Give **either** `script` or `scriptFile`; passing both is refused rather than resolved, because guessing which one you meant would silently run code you did not intend.
  > [!TIP]
  > **This is the difference between editing a drawing and retyping it.** On a measured four-agent run, 850 KB of JavaScript went over the wire in 55 calls; the twenty largest took a mean of **three minutes each to emit**, against a median engine time of **45 ms** — and consecutive large scripts shared **71%** of their lines. Nearly all of that was re-sending a program in order to change part of it.
  >
  > So once a piece is more than a screenful, keep it in a file: write `artwork.js` with your ordinary editor, change the layer you are working on, and run `ExecuteScript(scriptFile: 'artwork.js', outFile: 'artifacts/stage3.webp')`. The record is unaffected — the server still copies **what actually ran** into `scripts/`, so a later edit to the file never rewrites the history of an earlier execution.
- **Logging & Output:** Output via `console.log(...)`, `log(...)`, `error(...)`, or `table(...)`.
- **Early Termination:** Use `exit(message)` to terminate execution immediately and cleanly return the specified message without a failure status.

---

## Image Formats & Encoding Quality Trade-offs

When calling `ExecuteScript` or `RenderSvg`, agents can supply optional `format` (`"webp"`, `"png"`, `"jpeg"`) and `quality` (`1`–`100`, default `85`) parameters to balance visual fidelity against message transfer speed over MCP JSON-RPC:

| Format / Setting | Pure Encode Time | Total Roundtrip | Wire Payload | Compression vs PNG | Best Used For |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`webp` @ Q=85 (Default)** | **~114 ms** | **~200 ms** | **~113 KB** | **3.9× smaller** | **General purpose / balanced.** Pristine lines, gradients, and drop shadows with fast encoding. |
| **`webp` @ Q=90–95** | ~160 ms | ~195 ms | ~135–180 KB | 2.4×–3.3× smaller | **High-precision vector art.** Ultra-fine strokes, sub-pixel path details, or high-contrast hairline illustrations. |
| **`webp` @ Q=75–80** | ~114–160 ms | ~160–175 ms | ~83–97 KB | 4.6×–5.3× smaller | **Heavy procedural scenes.** Dense multi-layer canvas bitmaps, Perlin noise fields, or rapid drafting iterations. |
| **`jpeg` @ Q=85** | **~26 ms** | **~65 ms** | **~200 KB** | 2.2× smaller | **Ultra-fast raster passes.** Opaque photos/textures where transparency (alpha channel) is not required. |
| **`png` @ Q=100** | ~150 ms | ~220 ms | ~445 KB | 1.0× (Baseline) | **Lossless reference.** Bit-exact verification or debugging raw pixel data. |

> [!TIP]
> **Agent Decision Rule**:
> - Use the default **`webp` @ Q=85** for most collaborative iterations.
> - Bump to **Q=90–95** if you notice subtle artifacts in thin hairline vectors or subtle gradient ramps.
> - Drop to **Q=75–80** or switch to **`jpeg`** when generating dense multi-pass textures or complex procedural raster canvases to maximize messaging speed and conserve context window bandwidth.

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

### Toolkit Shortcuts on the Context

Many `Drawing.*` and `Logo.*` methods are also available directly on `ctx`, with the leading context argument dropped: `Drawing.drawPerspectiveGrid(ctx, grid, options)` and `ctx.drawPerspectiveGrid(grid, options)` are the same call.

> [!IMPORTANT]
> **This list is exhaustive — taking a context as its first parameter is not enough.** Six toolkit
> methods that do take one have no shortcut: `drawLoomisWireframe`, `drawMannequinWireframe`,
> `drawMannequinSolid`, `drawComicEye`, `drawComicNose` and `drawComicMouth`. Call those as
> `Drawing.drawLoomisWireframe(ctx, head)`.
>
> The mannequin pair is the trap worth knowing, because the shortcut exists under a *different name*:
> **`ctx.drawMannequin(figure, solid)`** covers both, with `solid` choosing between them. A live run
> lost two scripts guessing `ctx.drawMannequinWireframe` and `ctx.drawLoomisWireframe` from the rule
> this note replaces — which promised every context-taking method had a shortcut, and was wrong for
> six of twenty-one.

`ctx.drawPerspectiveGrid` · `ctx.drawPerspectiveBox` · `ctx.drawPerspectiveCylinder` · `ctx.renderVolumetricSphere` · `ctx.renderVolumetricCylinder` · `ctx.drawCastShadow` · `ctx.drawRimLight` · `ctx.drawMannequin` · `ctx.drawTorsoMusculature` · `ctx.drawCompositionGrid` · `ctx.drawLeadingLines` · `ctx.drawVignette` · `ctx.drawSquircle` · `ctx.drawEmblemBadge` · `ctx.drawGoldenSpiral` · `ctx.drawIsometricGrid` · `ctx.drawPolarGrid` · `ctx.drawClearSpaceGuide` · `ctx.generateFaviconScaleTest` · `ctx.generateMonochromeTest` · `ctx.generateBrandPresentationSheet`

Parameters and semantics are documented under `polson://sdk/core/Drawing` and `polson://sdk/core/Logo`. Use whichever reads better; the shortcut form suits long chains on one context.

### Constructive Drawing & Inking
- `ctx.drawTaperedStroke(start: Point | number, cp1: Point | number, cp2: Point | number, end: Point | number, maxThickness: number, fillOrStrokeStyle?: string | SKShader)` — Subdivides cubic Bézier curve with sine-tapered normal envelope and anti-aliased fill.
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

```javascript
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

Algorithmic drawing and constructive anatomy engine based on classical studio techniques (*Imaginative Drawing* Chapter 1, Loomis method, 3D hair ribbons, and procedural comic shaders).

Also accessible via `Skia.Drawing`.

## Loomis Head & Feature Construction
- `Drawing.createLoomisHead(originX: number, originY: number, headHeight: number, yawDeg?: number, pitchDeg?: number)` → `object` — Computes all 3D head landmarks, proportional ratios (Rule of Thirds, 1/5th eye width), temporal ovals, eye sockets, nose wedge, mouth guides, and jaw angles.
- `Drawing.drawLoomisWireframe(ctx: CanvasRenderingContext2D, headObj: object, options?: { blueLineColor?: string, graphiteColor?: string })` — Renders non-repro blue (`#4a90e2`) and graphite (`#444444`) construction wireframe.
- `Drawing.drawComicEye(ctx: CanvasRenderingContext2D, eyeObj: object, isFar?: boolean, options?: { inkColor?: string, irisColor?: string, scleraColor?: string })` — Renders S-curve upper eyelid, shaded sclera, colored iris, pupil, and white catchlight.
- `Drawing.drawComicNose(ctx: CanvasRenderingContext2D, noseObj: object, options?: { inkColor?: string, shadowColor?: string })` — Renders nose bridge, apex, nostril, and under-plane shadow.
- `Drawing.drawComicMouth(ctx: CanvasRenderingContext2D, mouthObj: object, options?: { inkColor?: string, lipColor?: string, teethColor?: string, cavityColor?: string })` — Renders Cupid's bow upper lip, teeth shelf, mouth cavity, and lower lip shadow.

## Inking, Feathering & Ribbons
- `Drawing.drawTaperedStroke(ctx: CanvasRenderingContext2D, start: Point | number, cp1: Point | number, cp2: Point | number, end: Point | number, maxThickness: number, fillOrStrokeStyle?: string | SKShader)` — Smooth tapered Bézier inking stroke.
- `Drawing.drawFeathering(ctx: CanvasRenderingContext2D, origin: Point | number, angleDeg: number, count: number, length: number, spacing: number, strokeColor?: string, lineWidth?: number)` — Directional feathering hatch lines.
- `Drawing.drawCrossContourHatch(ctx: CanvasRenderingContext2D, cx: number, cy: number, rx: number, ry: number, startAngle: number, endAngle: number, count?: number, strokeColor?: string, lineWidth?: number)` — Cross-contour cylindrical arcs.
- `Drawing.drawHairRibbon(ctx: CanvasRenderingContext2D, root: Point | number, tip: Point | number, bendFactor: number, width: number, fillTop: string | SKShader, fillUnderside: string | SKShader, strokeColor?: string, strokeWidth?: number)` — 3D twisting hair ribbon.

## Material & Shader Presets
- `Drawing.createHalftoneDotShader(options?: { dotSpacing?: number, shadowColor?: string, resolution?: number[] })` → `SKShader` — SkSL Ben-Day halftone dot shader.
- `Drawing.createRopeFiberShader(frequencyX?: number, frequencyY?: number, octaves?: number, seed?: number)` → `SKShader` — Hemp rope/cordage texture shader.
- `Drawing.createAtmosphericCloudShader(frequencyX?: number, frequencyY?: number, octaves?: number, seed?: number)` → `SKShader` — Atmospheric fractal cloud noise shader.

## Measurement & Plumb Checks
- `Drawing.verifyPlumbAlignment(topPoint: Point, bottomPoint: Point, maxTolerance?: number)` → `{ aligned: boolean, deltaX: number, message: string }` — Validates vertical alignment between anatomical landmarks.
- `Drawing.computeRelativeDistance(headHeight: number, pointA: Point, pointB: Point)` → `number` — Computes distance in head-length units ($d / H$).

## Linear Perspective & 3D Forms
- `Drawing.createPerspectiveGrid(options?: { type?: '1point' | '2point' | '3point', horizonY?: number, centerOfVisionX?: number, focalLength?: number, cameraAngleDeg?: number, tiltAngleDeg?: number })` → `object` — Computes vanishing points ($VP_L, VP_R, VP_V$), horizon line, and center of vision.
- `Drawing.drawPerspectiveGrid(ctx: CanvasRenderingContext2D, gridObj: object, options?: { lineColor?: string, horizonColor?: string, lineCount?: number, lineWidth?: number })` — Renders horizon and perspective grid fan lines.
- `Drawing.createPerspectiveBox(gridObj: object, anchorX: number, anchorY: number, width: number, height: number, depth: number)` → `object` — Projects 3D box computing all 8 vertices ($V_0 \dots V_7$) and 6 quadrilateral faces.
- `Drawing.drawPerspectiveBox(ctx: CanvasRenderingContext2D, boxObj: object, options?: { topFill?: string, leftFill?: string, rightFill?: string, strokeColor?: string, strokeWidth?: number, drawHiddenLines?: boolean })` — Renders solid shaded or wireframe 3D perspective box.
- `Drawing.drawPerspectiveCylinder(ctx: CanvasRenderingContext2D, gridObj: object, anchorX: number, anchorY: number, radius: number, height: number, options?: { topFill?: string, sideFill?: string, strokeColor?: string, strokeWidth?: number })` — Projects 3D cylinder with top/bottom tangent ellipses.
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
- `Drawing.drawRimLight(ctx: CanvasRenderingContext2D, boundsOrPts: Rect | Point[], lightAngleDeg: number, rimColor?: string, thickness?: number)` — Renders high-contrast silhouette rim lighting.
- `Drawing.createVolumetricSphereShader(options?: { lightColor?: string, baseColor?: string, shadowColor?: string })` → `SKShader` — SkSL procedural 3D sphere lighting shader.

## Full-Body Anatomy, Mannequins & Expressions
- `Drawing.createMannequinFigure(originX: number, originY: number, totalHeight?: number, options?: { shoulderTiltDeg?: number, pelvicTiltDeg?: number, spineOffset?: number })` → `object` — Computes full 8-head proportional skeletal joint nodes (Head, Clavicles, Sternum, Ribcage, Spine, Pelvis, Hips, Knees, Ankles, Feet, Shoulders, Elbows, Wrists, Hands).
- `Drawing.drawMannequinWireframe(ctx: CanvasRenderingContext2D, figureObj: object, options?: { blueLineColor?: string, graphiteColor?: string, lineWidth?: number })` — Renders non-repro blue gesture and joint circle hinges.
- `Drawing.drawMannequinSolid(ctx: CanvasRenderingContext2D, figureObj: object, options?: { fillColor?: string, shadowColor?: string, strokeColor?: string, strokeWidth?: number })` — Renders shaded volumetric 3D masses (cranial sphere, ribcage egg, pelvic basin, limb cylinders, and box hands/feet).
- `Drawing.drawTorsoMusculature(ctx: CanvasRenderingContext2D, figureObj: object, options?: { strokeColor?: string, strokeWidth?: number })` — Renders pectorals, deltoids, clavicle handlebars, neck sternocleidomastoid cords, and rectus abdominis six-pack grid.
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

```javascript
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
