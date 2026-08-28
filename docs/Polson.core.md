# Polson JavaScript SDK — Core Reference

This is the **core** reference for the Polson JavaScript (JS) SDK — the typed drawing, graphics, and vector API exposed to scripts executed inside the Polson MCP server's sandboxed JavaScript engine. It covers the **execution model** (the rules every generated script must follow) and the **method signature index** for all top-level objects: each method's purpose, parameter types, and return values.

The **JSON schema for every parameter and return model type** named below lives in the companion schema document (`Polson.schema.md`). Both documents are served **sliced by subject area**: read `polson://sdk/index` for the map, `polson://sdk/core/{Area}` for an area's methods, and `polson://sdk/schema/{Area}` for the fields of what they return. This entire document is also available at `polson://sdk/core/all`.

---

## Execution model

Scripts execute within a secure, sandboxed [Jint](https://github.com/sebastianros/jint) runtime supporting **ECMAScript 2025** (arrow functions, `let`/`const`, destructuring, template literals, optional chaining `?.`, nullish coalescing `??`, `for...of`, spread `...`, `Array`/`Map`/`Set`/`JSON`, etc.). Tailor generated code to modern JavaScript idioms.

- **Sandbox Security:** `eval` and `new Function` are strictly disabled (`Host.StringCompilationAllowed = false`). Arbitrary external types and reflection are prohibited. Scripts can only interact with the explicit Polson drawing APIs.
- **Execution Limits:** Scripts are enforced with statement limits (2,000,000 statements, configurable via `JsDrawingEngine.MaxStatements`), recursion depth limits (100 frames), and execution timeouts ({{SCRIPT_TIMEOUT_SECONDS}} seconds).
  > [!TIP]
  > For heavy pixel-level manipulation (such as procedural textures, blurs, or color grading), use native **`Skia.Shader`** or **`Skia.ImageFilter`** pipelines which execute in native SIMD/C++ in < 1ms, rather than running millions of raw per-pixel loop iterations in interpreted JS.
- **Return Value & Visual Rendering:**
  - Returning a `SnapPaper` (or a `SnapElement`), `CanvasRenderingContext2D`, `SkiaCanvas`, `SkiaBitmapWrapper`, or `ImageData` automatically renders the visual output headlessly to image bytes (`result.ImageBytes`, defaulting to **WebP at quality=85**, with `"png"` and `"jpeg"` options available) and Base64 URI (`result.ImageDataUri`).
  - **Direct-to-Disk Rendering (`outFile`, `outSvg`):** Agents can pass `outFile` (e.g. `'artifacts/stage1.webp'`) to write the rendered image directly to disk, and `outSvg` (e.g. `'artifacts/stage1.svg'`) for vector markup. When `outFile` is supplied, `result.ImageFilePath` contains the saved path and `result.ImageBytes` is omitted by default to eliminate token bloat in LLM contexts (use `includeBytes: true` to force inclusion).
  - For vector scenes (`SnapPaper` / `SnapElement`), `result.SvgXml` contains the serialized SVG XML markup. For 2D canvas raster scripts, `result.SvgXml` retains the last vector image produced by the agent prior to switching to 2D canvas mode.
  - If a script creates one or more canvases or Snap papers without explicitly returning them, the last created canvas/paper is rendered automatically.
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
- `console.info(...args: any[])` — Alias for `console.log`.
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
- `Snap.snapTo(values: number[], val: number, tolerance?: number)` → `number` — Snaps `val` to the closest number in `values` within `tolerance` (default 10).

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
- `paper.clear()` → `void` — Removes all child nodes from the document.
- `paper.toString()` → `string` — Serializes the document tree to an SVG XML string.
- `paper.toImageBytes(width?: number, height?: number, format?: string, quality?: number)` → `byte[]` — Headlessly renders the SVG to image bytes (default: WebP Q=85).
- `paper.toDataUri(format?: string, width?: number, height?: number, quality?: number)` → `string` — Renders to a `data:image/...;base64,...` URI (defaults to `format: 'svg'`).

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

- `element.id` → `string` — Gets or sets the element's id.
- `element.type` → `string` — The SVG tag name.
- `element.parent` → `SnapElement?` — The containing element.
- `element.children` → `SnapElement[]` — Direct child elements.
- `element.paper` → `SnapPaper?` — The document this element belongs to.

### Tree Placement & Lifecycle

- `element.appendTo(parent: SnapElement | SnapPaper)` → `SnapElement` — Moves this element to the end of `parent`.
- `element.prependTo(parent: SnapElement | SnapPaper)` → `SnapElement` — Moves this element to the start of `parent`.
- `element.add(...elements: SnapElement[])` → `SnapElement` — Appends children.
- `element.before(other: SnapElement)` / `element.after(other: SnapElement)` → `SnapElement` — Moves this element immediately before or after `other` in document order, which is how you control z-order after construction.
- `element.remove()` → `SnapElement` — Detaches the element from its parent container.
- `element.clone()` → `SnapElement` — Deep-clones the element and its children.
- `element.clear()` → `void` — Removes all child nodes from this container element.

### Geometry & Measurement

- `element.getBBox()` → `SnapBBox` — Calculates the element's bounding box.
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
- `ctx.fill(path?: CanvasPath)` — Fills the current or specified `CanvasPath`.
- `ctx.stroke(path?: CanvasPath)` — Strokes the current or specified `CanvasPath`.
- `ctx.clip(path?: CanvasPath)` — Intersects the clipping region with the current or specified path.

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
- `ctx.globalAlpha` — Alpha multiplier `[0.0, 1.0]`.
- `ctx.globalCompositeOperation` — Blend mode: `"source-over"`, `"multiply"`, `"screen"`, `"overlay"`, `"darken"`, `"lighten"`, `"color-dodge"`, `"color-burn"`, `"hard-light"`, `"soft-light"`, `"difference"`, `"exclusion"`, `"hue"`, `"saturation"`, `"color"`, `"luminosity"`, `"xor"`, `"destination-over"`, etc.
- `ctx.shadowColor` — Drop shadow color string.
- `ctx.shadowBlur` — Gaussian blur sigma for shadows.
- `ctx.shadowOffsetX` / `ctx.shadowOffsetY` — Horizontal and vertical shadow offset.
- `ctx.filter` — Image filter (e.g. `Skia.ImageFilter.blur(5, 5)`).
- `ctx.colorFilter` — Color filter (e.g. `Skia.ColorFilter.colorMatrix(...)`).
- `ctx.pathEffect` — Path effect (e.g. `Skia.PathEffect.corner(10)` or `Skia.PathEffect.dash([10, 5])`).

### Typography
- `ctx.font` — Font specification string: e.g. `"bold 24px Arial"`, `"italic 16px 'Times New Roman'"`.
- `ctx.textAlign` — Alignment: `"left"`, `"center"`, `"right"`, `"start"`, `"end"`.
- `ctx.textBaseline` — Baseline: `"top"`, `"middle"`, `"bottom"`, `"alphabetic"`, `"hanging"`.
- `ctx.fillText(text: string, x: number, y: number, maxWidth?: number)` — Draws filled text (supports multi-line strings with `\n`).
- `ctx.strokeText(text: string, x: number, y: number, maxWidth?: number)` — Draws stroked text outline (supports multi-line strings with `\n`).
- `ctx.fillWrappedText(text: string, x: number, y: number, maxWidth: number, lineHeight?: number)` — Automatically word-wraps and draws filled paragraph text within `maxWidth`.
- `ctx.strokeWrappedText(text: string, x: number, y: number, maxWidth: number, lineHeight?: number)` — Automatically word-wraps and strokes paragraph text within `maxWidth`.
- `ctx.measureText(text: string)` → `TextMetrics` — Returns `{ width, actualBoundingBoxAscent, actualBoundingBoxDescent, fontBoundingBoxAscent, fontBoundingBoxDescent }`.

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

### Pixel Buffer Access
- `ctx.getImageData(sx: number, sy: number, sw: number, sh: number)` → `ImageData` — Extracts pixel buffer for direct byte manipulation.
- `ctx.putImageData(imageData: ImageData, dx: number, dy: number)` — Writes raw pixel buffer back to canvas.
- `ctx.createImageData(width: number, height: number)` → `ImageData` — Allocates blank RGBA pixel buffer.

### Toolkit Shortcuts on the Context

Every `Drawing.*` and `Logo.*` method whose first parameter is a context is also available directly on `ctx`, with that first argument dropped. `Drawing.drawPerspectiveGrid(ctx, grid, options)` and `ctx.drawPerspectiveGrid(grid, options)` are the same call:

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

- `Skia.PathEffect.dash(intervals: number[], phase?: number)` → `SKPathEffect` — Custom dash patterns (e.g. `[10, 5, 2, 5]`).
- `Skia.PathEffect.corner(radius: number)` → `SKPathEffect` — Rounds sharp polygon corners with specified radius.
- `Skia.PathEffect.discrete(segLength: number, deviation: number, seed?: number)` → `SKPathEffect` — Jittered / sketched line effect.

## `Skia.Image` & `Skia.Bitmap`

- `Skia.Image.load(filePath: string)` → `SkiaBitmapWrapper` — Loads and decodes a raster image from disk (PNG, JPEG, WebP, BMP).
- `Skia.Image.fromDataUrl(dataUrl: string)` → `SkiaBitmapWrapper` — Decodes base64 data URL.
- `Skia.Image.fromBytes(bytes: byte[])` → `SkiaBitmapWrapper` — Decodes raw byte buffer.
- `Skia.Bitmap.create(width: number, height: number)` → `SkiaBitmapWrapper` — Allocates a blank editable bitmap.

## `SkiaBitmapWrapper`

- `bitmap.width` → `number` — Width in pixels.
- `bitmap.height` → `number` — Height in pixels.
- `bitmap.extractSubset(x: number, y: number, width: number, height: number)` → `SkiaBitmapWrapper` — Extracts cropped sub-bitmap.
- `bitmap.resize(width: number, height: number, quality?: string)` → `SkiaBitmapWrapper` — Resamples bitmap (`"linear"`, `"nearest"`).
- `bitmap.rotate(angleDeg: number)` → `SkiaBitmapWrapper` — Rotates bitmap by degrees.
- `bitmap.flip(direction?: string)` → `SkiaBitmapWrapper` — Flips bitmap (`"horizontal"`, `"vertical"`, `"both"`).
- `bitmap.getPixel(x: number, y: number)` → `string` — Returns hex color `"#RRGGBBAA"`.
- `bitmap.setPixel(x: number, y: number, color: string)` — Sets pixel color.
- `bitmap.applyFilter(filter: SKImageFilter)` → `SkiaBitmapWrapper` — Returns new bitmap with image filter applied.
- `bitmap.applyColorFilter(filter: SKColorFilter)` → `SkiaBitmapWrapper` — Returns new bitmap with color filter applied.
- `bitmap.toPngBytes(quality?: number)` → `byte[]` — Encodes to PNG byte array.
- `bitmap.toImageBytes(format?: string, quality?: number)` → `byte[]` — Encodes in any supported format (default WebP Q=85).
- `bitmap.toDataUri(format?: string, quality?: number)` → `string` — Base64 data URI in the given format.
- `bitmap.toDataUrl()` → `string` — Returns `data:image/png;base64,...` URL.
- `bitmap.clone()` → `SkiaBitmapWrapper` — Deep clones bitmap.
- `bitmap.dispose()` — Releases native bitmap memory.

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
- `Logo.computeOpticalCenter(pointsOrBounds: Point[] | Rect, shapeType?: 'triangle' | 'arrow' | 'general')` → `Point` — Computes visual center of gravity ($Y \approx 42\%–48\%$).

## Scale Stress-Testing & Brand Sheets
- `Logo.generateFaviconScaleTest(ctx: CanvasRenderingContext2D, drawMarkFn: (ctx: CanvasRenderingContext2D, size: number) => void, options?: object)` — Side-by-side multi-scale legibility ladder ($16\text{px}, 24\text{px}, 32\text{px}, 48\text{px}, 64\text{px}, 128\text{px}, 256\text{px}$).
- `Logo.generateMonochromeTest(ctx: CanvasRenderingContext2D, drawMarkFn: (ctx: CanvasRenderingContext2D, size: number) => void, width?: number, height?: number)` — 4-way contrast test board (Positive Black, Negative White Knockout, Grayscale, App Icon Squircle).
- `Logo.generateBrandPresentationSheet(ctx: CanvasRenderingContext2D, options: { brandName: string, tagline?: string, primaryColor?: string, secondaryColor?: string, darkColor?: string, lightColor?: string, drawMark: (ctx: CanvasRenderingContext2D, size: number) => void })` — Complete executive brand identity presentation board with logo lockup, color chips, and clear space guides.

---

# VectorLogo & Snap.svg Logo Methods

Retained-mode SVG vector logo construction methods available directly on `SnapPaper` (`paper`), `Snap.path`, and the global `VectorLogo` object.

## SnapPaper Vector Methods
- `paper.squircle(x: number, y: number, width: number, height: number, exponent?: number)` → `SnapPath` — Appends a Lamé superellipse squircle path element to the paper.
- `paper.goldenSpiral(startX: number, startY: number, initialRadius: number, turns?: number, segmentsPerTurn?: number)` → `SnapPath` — Appends a logarithmic golden spiral path ($r = a \cdot e^{b\theta}$) to the paper.
- `paper.emblemBadge(cx: number, cy: number, width: number, height: number, style?: 'shield' | 'hexagon' | 'diamond' | 'scallop' | 'circle')` → `SnapPath` — Appends a geometric badge outline to the paper.
- `paper.goldenCircles(cx: number, cy: number, baseRadius: number, count?: number)` → `SnapGroup` — Appends a group containing $\Phi$-scaled concentric circles.
- `paper.isometricGrid(width: number, height: number, spacing?: number)` → `SnapGroup` — Appends a group containing 30°/60° isometric construction grid lines.
- `paper.polarGrid(cx: number, cy: number, maxRadius: number, ringCount?: number, rayCount?: number)` → `SnapGroup` — Appends a group containing polar concentric rings and radial spokes.
- `paper.monogramMatrix(x: number, y: number, width: number, height: number, type?: '2x2' | '3x3' | '4x4')` → `SnapGroup` — Appends monogram matrix grid guides and node anchor circles.
- `paper.clearSpaceGuide(x: number, y: number, width: number, height: number, margin?: number)` → `SnapGroup` — Appends clear space boundary guides and dimension blocks ($X$).
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

The same constructions as `paper.*` above, but called on the global `VectorLogo` object with the target paper as the **first argument**. Use these when the paper is held in a variable rather than being the receiver; `paper.squircle(...)` and `VectorLogo.squircle(paper, ...)` produce identical elements.

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
- `LogoType.computeWordmarkTracking(fontSize?: number, isAllCaps?: boolean, role?: 'wordmark' | 'tagline')` → `number` — Returns optical letter-spacing (tight tracking for large display marks, wide tracking $+150\text{‰}–+300\text{‰}$ for all-caps taglines).

## Typographic Scale & Font Harmony
- `LogoType.calculateTypographicScale(baseSize?: number, ratio?: 'goldenRatio' | 'perfectFifth' | 'augmentedFourth' | 'perfectFourth' | 'majorThird' | 'minorThird', stepsDown?: number, stepsUp?: number)` → `object` — Generates harmonic font size ladder (`micro`, `caption`, `body`, `h4`, `h3`, `h2`, `h1`, `display`).
- `LogoType.evaluateFontPairing(primaryCategory: string, secondaryCategory: string)` → `{ relationship: 'concordant' | 'conflicting' | 'contrasting', score: number, description: string, recommendations: string[] }` — Evaluates font pairing against the Robin Williams contrast matrix across Size, Weight, Structure, and Form.
- `LogoType.getGlyphShapeType(char: string)` → `string` — Classifies a glyph silhouette as straight, round, diagonal or open, which is what drives the kerning table.
- `LogoType.getRatioFactor(ratioName: string)` → `number` — The numeric multiplier behind a named harmonic ratio.

## Brand Lockups & Letterform Geometry
- `LogoType.createOgeeCurvePath(x1: number, y1: number, x2: number, y2: number, inflectionT?: number, amplitude?: number)` → `string` — Generates classical Doyald Young $S$-curve cubic Bézier path.
- `LogoType.createOgeeCurveSKPath(x1: number, y1: number, x2: number, y2: number, inflectionT?: number, amplitude?: number)` → `SKPath` — The same Ogee curve as a Skia path, for filling or stroking on a canvas.
- `LogoType.drawWordmarkLockup(ctx: CanvasRenderingContext2D, drawMarkFn: Function, brandName: string, tagline?: string, options?: { layout?: 'horizontal' | 'vertical', x?: number, y?: number, markSize?: number, fontSize?: number, taglineSize?: number, primaryColor?: string, taglineColor?: string })` — Renders balanced brand lockup with optical alignment.
- `ctx.drawWordmarkLockup(drawMarkFn, brandName, tagline, options)` — Direct canvas context helper.
- `ctx.drawOgeeCurve(x1, y1, x2, y2, amplitude, inflectionT)` — Direct canvas context helper.
- `paper.ogeeCurve(x1, y1, x2, y2, amplitude, inflectionT)` → `SnapPath` — Direct Snap.svg paper helper.


