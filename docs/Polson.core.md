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

## `SnapElement`

Represents any SVG node in the document hierarchy:

- `element.attr(name: string)` → `any` — Retrieves the value of an attribute or style property.
- `element.attr(name: string, value: any)` → `SnapElement` — Sets an attribute (e.g. `fill`, `stroke`, `stroke-width`, `opacity`, `d`, `transform`). Supports method chaining.
- `element.attr(attributes: object)` → `SnapElement` — Sets multiple attributes via a dictionary literal: `el.attr({ fill: '#f00', stroke: '#000', 'stroke-width': 2 })`.
- `element.transform(transformStringOrMatrix: string | SnapMatrix)` → `SnapElement` — Applies SVG transform commands (e.g. `t100,50r45s1.5` or a `SnapMatrix`).
- `element.appendTo(parent: SnapElement | SnapPaper)` → `SnapElement` — Moves this element to the end of `parent`.
- `element.prependTo(parent: SnapElement | SnapPaper)` → `SnapElement` — Moves this element to the start of `parent`.
- `element.remove()` → `SnapElement` — Detaches the element from its parent container.
- `element.clone()` → `SnapElement` — Deep-clones the element and its children.
- `element.getBBox()` → `SnapBBox` — Calculates the element's bounding box.
- `element.getTotalLength()` → `number` — Measures path total length (paths only).
- `element.getPointAtLength(length: number)` → `SnapPoint` — Computes coordinates at `length` (paths only).
- `element.select(selector: string)` → `SnapElement?` — Finds the first descendant matching a tag name, `#id`, or `.class`.
- `element.selectAll(selector: string)` → `SnapElement[]` — Finds all matching descendants.
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
- `element.el(name: string, attrs?: object)` → `SnapElement` — Creates and appends an arbitrary SVG child element.
- `element.use(target: SnapElement | string)` → `SnapUse` — Creates and appends a child `<use>` element.
- `element.clear()` → `void` — Removes all child nodes from this container element.

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
- `bitmap.toDataUrl()` → `string` — Returns `data:image/png;base64,...` URL.
- `bitmap.clone()` → `SkiaBitmapWrapper` — Deep clones bitmap.
- `bitmap.dispose()` — Releases native bitmap memory.

## `ImageData`

- `imageData.width` → `number` — Width in pixels.
- `imageData.height` → `number` — Height in pixels.
- `imageData.data` → `byte[]` — Flat array of RGBA byte values `[r0, g0, b0, a0, r1, g1, b1, a1, ...]`.
- `imageData.toPngBytes(quality?: number)` → `byte[]` — Encodes pixel buffer to PNG bytes.
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



