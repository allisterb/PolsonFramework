# Studio Manual 14: Vector Construction & the Raster Boundary

> **Credits & Theoretical Foundation**: The reproduction argument is distilled from *Graphic Design and Print Production Fundamentals* (BCcampus / Graphic Communications Open Textbook Collective, CC BY 4.0), Chapter 5 **Pre-press** — §5.2 *Raster Image Processing* for the RIP model and the resolution rule, §5.5 *Transparency* for the rasterisation trade-off. Ledger entry 2026-08-31. The vector surface itself is Snap.svg-compatible and is documented at `polson://sdk/core/Snap`.
> **Purpose**: Decides when a deliverable must be vector rather than raster, teaches construction in the retained-mode SVG document tree — the tree, transforms, paint servers and path measurement — and shows how to get an actual `.svg` file out of the engine.

---

## 1. The Raster Boundary

> **Implemented by**: nothing. This section is the decision you make before the first call, and it decides which of the next nine sections apply.

Every drawing becomes pixels eventually. The question is **where in the pipeline that happens**, because it happens exactly once and cannot be undone.

A raster image processor — the RIP — is the engine that turns a description of a graphic into the one-bit on/off data that drives a physical device. Given the letter **A**, a font hands the RIP a set of points and the vector curves between them; the RIP lays a grid at the *output device's* resolution over that outline and decides which spots to turn on. A lithographic plate-setter images 2,000–3,000 laser spots per inch. The same outline, sent to an inkjet, is rasterised again at 600–1,200. The outline does not change; the grid does.

That is the whole argument. **A vector description is resolution-independent until something rasterises it, and it can be rasterised again, differently, for every device it ever meets.** A raster image has already made that choice, at one resolution, permanently — and a RIP is poor at inventing detail that was never there. Chapter 5 is blunt about it: it does not do a good job of interpolating more data if that information is missing to begin with.

**The resolution rule, when a raster deliverable is genuinely wanted.** Required PPI = 2 × the screening frequency in lines per inch, *at final reproduced size*. Print at 175 lpi and the image must carry 350 ppi at the size it lands on the page; use 400 ppi as a default for FM screening, where lpi does not apply. A 512-pixel mark is a 1.5-inch mark in print. Repurposing a 72-ppi web image onto a book cover is the classic failure, and the file will not tell you it failed — the RIP simply prints what it was given.

### What this means for a brief

| The brief says | The deliverable is | Because |
| :--- | :--- | :--- |
| "an SVG", "vector", "a logo", "an icon" | a `.svg` file, written with `outSvg` | It will be scaled to sizes nobody has chosen yet |
| "for print", "a poster", "a book cover" | vector where the artwork allows; otherwise raster at 2 × lpi at final size | The RIP rasterises once, at the device's grid |
| "a painting", "a texture", "photographic" | raster, and say the pixel dimensions | The content *is* pixels; there is no outline to preserve |
| nothing about output | render raster, and write `outSvg` too when the scene is vector | It costs one argument and forecloses nothing |

> [!IMPORTANT]
> **If the brief names a format, the deliverable is a file in that format.** A run given *"an SVG of a seagull riding a bicycle"* that returns `output.webp` has not answered the brief, however good the picture is. This has happened, which is why the rule is written down: the drawing was built on a raster canvas from the first script, and by the time anyone looked, the only thing that could have been vector was gone.

### The one-way door

§5.5 states the trade-off exactly, in the context of deciding *when* to flatten transparency: **the earlier we rasterize, the less editable the document becomes, and the more consistent the final output will be.**

Both halves are true, and both matter here.

- **Rasterise late** and the work stays editable — a shape can be moved, a colour changed, a curve reshaped — and the output is at the mercy of whatever rasterises it. Two viewers running different RIPs render the same file differently; that is why a PDF can look one way in Preview and another in Acrobat.
- **Rasterise early** and what you see is what ships, at the cost of never being able to change it again.

A studio run is not a print shop, but the same door is in it. Every stage that draws on a canvas has rasterised. Every stage that draws on a paper has not.

---

## 2. Choosing the Surface

> **Implemented by**: `Snap(width, height)` for vector, `createCanvas(width, height)` for raster. Signatures at `polson://sdk/core/Snap` and `polson://sdk/core/Canvas2D`.

Two surfaces, and the choice is per *scene*, not per project.

```js
const paper = Snap(800, 600);          // retained: a tree of shapes you can still address
const canvas = createCanvas(800, 600); // immediate: pixels, the moment you draw them
```

**Retained mode** keeps the drawing as a document. A rectangle drawn in the first script is still a rectangle in the fifth: it can be selected, recoloured, moved, cloned, reordered or deleted. Nothing is committed until something renders it.

**Immediate mode** keeps the result. `ctx.fillRect(...)` changes pixels; there is no rectangle afterwards, only the pixels it left. Undoing it means redrawing everything underneath.

| Choose the vector paper when | Choose the raster canvas when |
| :--- | :--- |
| The deliverable is `.svg` | The deliverable is `.webp` / `.png` and always was |
| The mark will be scaled to unknown sizes — logos, icons, marks | The output size is fixed and known |
| A later stage must *edit* what an earlier stage drew | Each stage composites over the last |
| The picture is built from a countable number of shapes | The picture is built from texture, grain, noise or gradient wash |
| You need a real hole in a shape that survives a knockout | You need brushes, shaders, mask filters or per-pixel work |

The honest summary: **the vector paper is the better surface for anything that must be reproduced, and the raster canvas is the better surface for anything that must be rendered.** A mark, a diagram, a chart, a monogram, a piece of lettering, a technical illustration — vector. A painting, a comic panel with inked texture, an atmospheric plate — raster.

### They compose

Nothing forces one choice for a whole project. A vector scene can be composited onto a raster canvas with `ctx.drawSvg(paper, x, y, w, h)`, and any vector element's geometry crosses over with `element.toSkPath()`. The reverse — recovering shapes from pixels — does not exist, which is the point of §1. Build vector first, cross to raster last.

---

## 3. Getting It Out

> **Implemented by**: the `outSvg` and `outFile` arguments of `ExecuteScript`; `paper.toString()`, `paper.toDataUri(...)`, `paper.toImageBytes(...)` inside a script.

`ExecuteScript` takes both output arguments, and they are independent:

```
ExecuteScript(script, outFile: 'artifacts/03_mark.webp', outSvg: 'artifacts/03_mark.svg')
```

- `outFile` writes the **rendered image**. Always available.
- `outSvg` writes the **vector markup**, taken from `result.SvgXml`. Available only when the script built a `SnapPaper`.

Pass both on every vector stage. The raster render is what a director looks at between turns; the SVG is the deliverable and the thing a later stage can reopen and edit. Neither replaces the other.

Inside a script the same markup is `paper.toString()`, which returns the serialized SVG XML — useful to log a length as a sanity check, or to hand to `Snap.parse(...)` in a later script.

> [!WARNING]
> **A raster-only script with `outSvg` writes nothing.** There is no vector document to serialize, so the argument has nothing to act on. The engine now says so on the response — `outSvg … wrote nothing: this script produced no vector document` — but the warning arrives *after* the stage is drawn, and the fix is never small: it means rebuilding the scene on a paper.
>
> Decide in §2, before the first script. That is the cheapest place to make this decision and the only cheap one.

A mixed script — one that builds a paper, then composites it onto a canvas and returns the canvas — keeps the last paper it made in `result.SvgXml`, so `outSvg` still writes the vector half. That is deliberate: a mark built in vector and presented on a raster board should still deliver the mark.

---

## 4. The Document Is a Tree

> **Implemented by**: `element.attr(...)`, `element.children`, `element.parent`, `element.select(...)`, `element.selectAll(...)`, `element.clone()`, `element.remove()`, `element.appendTo(...)`, `element.prependTo(...)`, `element.before(...)`, `element.after(...)`, `element.add(...)`. Signatures at `polson://sdk/core/Snap`.

This is the capability the raster canvas does not have, and it is the reason §5.5's "less editable" matters. Every shape you draw stays addressable.

### Attributes

`attr` is the single most-used call in the vector API. It reads one attribute, sets one, or sets many, and the setting forms return the element so they chain:

```js
const plate = paper.rect(20, 20, 200, 120, 8);
plate.attr({ fill: '#3366cc', stroke: '#12203a', 'stroke-width': 2, opacity: 0.9 });
plate.attr('fill');            // '#3366CC' — hex comes back uppercased
plate.attr({ 'class': 'card' }); // any SVG attribute; unknown names pass through
```

Hyphenated SVG names (`stroke-width`, `font-size`, `text-anchor`) must be quoted as keys. Names `attr` does not specifically handle are still written out, which is how you set anything the toolkit has no opinion about.

### Identity and selection

```js
plate.id = 'plate';                  // ids are yours to assign — an element has none until you do
paper.select('#plate');              // by id
paper.select('.card');               // by class
paper.selectAll('rect');             // every descendant rect, as an array
```

`select` returns the first match or `null`; `selectAll` returns an array. Both search descendants, so calling them on a group scopes the search to that group. **Assign ids to anything a later stage will need to find** — this is the mechanism that lets stage four recolour stage one's work without redrawing it.

### Walking

`element.children` is the direct children, as an array; `element.parent` is the containing element, or `null` at the root. Both are **properties, not methods** — this is one of the few places the surface deliberately departs from Snap.svg, because `el.children()` returning a function whose `.length` is `0` made a wrong spelling look like an empty tree.

One thing to know when walking a paper: `paper.children` includes the `<defs>` container, which is created on demand and holds every gradient, mask and pattern. Filter on `type` rather than assuming index 0 is your first drawn shape.

```js
const shapes = paper.children.filter(el => el.type !== 'defs');
```

`element.type` is the SVG tag name — `rect`, `circle`, `path`, `g`, `text`, `defs`, `mask`, `pattern`, `tspan`, `textPath`, `svg`.

### Structure and z-order

SVG paints in document order: later siblings sit on top. Reordering is therefore a *tree* operation, not a style one.

- `element.appendTo(parent)` — move to the end of `parent`, i.e. to the front
- `element.prependTo(parent)` — move to the start, i.e. to the back
- `element.before(other)` / `element.after(other)` — move immediately before or after a specific sibling
- `element.add(...children)` — append children to a container
- `element.remove()` — detach from the parent
- `element.clone()` — a deep copy

> [!IMPORTANT]
> **A clone is detached.** It has no parent until you place it with `appendTo` or `prependTo`. Snap.svg inserts the copy after the original; here the placement is yours, so a clone made only to measure — or to seed a `<defs>` entry — does not silently double the drawing. The cost is that `clone()` alone appears to do nothing. It has not; the copy exists and is waiting to be put somewhere.

Group with `paper.g()` or `element.g()`, and group early. A group is what makes "the whole bicycle" a thing that can be moved, scaled, hidden or restyled in one call, and a drawing built as a flat list of eighty shapes is one no later stage can edit meaningfully.

---

## 5. Transforms

> **Implemented by**: `element.transform(...)`, `Snap.matrix(...)`, and the `SnapMatrix` operations. Signatures at `polson://sdk/core/Snap`; the matrix model at `polson://sdk/schema/Snap`.

Two spellings, one mechanism.

**The Snap shorthand string**, applied to an element:

```js
mark.transform('t120,40r15s1.5');   // translate, then rotate 15°, then scale 1.5×
mark.transform();                    // read it back as 'matrix(a,b,c,d,e,f)'
```

`t` translate, `r` rotate (degrees, optionally around a pivot `r45,cx,cy`), `s` scale, `m` an explicit matrix. Commands compose left to right. Calling `transform` with no argument returns the resulting matrix as a string, which is the readback — `attr('transform')` also reads it, and the two agree.

**The matrix**, when you need the arithmetic rather than the notation:

```js
const m = Snap.matrix().translate(120, 40).rotate(15).scale(1.5);
const at = m.transformPoint(0, 0);        // { x, y } — where a point lands
mark.transform(m);                        // matrices are accepted wherever a string is
```

`translate`, `rotate`, `scale`, `skewX`, `skewY` each post-multiply and return the matrix, so they chain. `mult(other)` post-multiplies an existing matrix and `multLeft(other)` pre-multiplies — the difference between "then do this" and "do this first". `invert()` returns the inverse, `clone()` a copy, `determinant` the scale factor of the whole transform, and `isIdentity` whether it does anything at all. `toTransformString()` is the `matrix(...)` form.

**`transformPoint` is what makes a transform measurable.** A transform moves ink; it does not tell you where the ink went. To place a label at the tip of a rotated arm, or to check that a scaled group still sits inside its clear space, transform the point yourself and read the coordinates:

```js
const tip = Snap.matrix().translate(cx, cy).rotate(angle).transformPoint(armLength, 0);
paper.circle(tip.x, tip.y, 3);
```

Transforms on a **group** apply to everything inside it, and nest. That is the argument for grouping: a bicycle whose frame, wheels and rider are one group is repositioned with one call, and each part keeps its own local coordinates.

---

## 6. Paint Servers — Gradients, Masks, Patterns

> **Implemented by**: `paper.gradient(...)`, `paper.gradientLinear(...)`, `paper.gradientRadial(...)`, `gradient.addStop(...)`, `gradient.setStops(...)`, `gradient.stops()`, `paper.mask(...)`, `paper.ptrn(...)`, `paper.defs`.

A paint server is a fill that is not a flat colour. Each is created on the paper, lands in `<defs>` automatically, is assigned an id, and is referenced by that id.

```js
const g = paper.gradient('l(0,0,1,0)#1b3b6f-#c94f2b');   // linear, left to right
paper.rect(0, 0, 400, 300).attr({ fill: 'url(#' + g.attr('id') + ')' });
```

The shorthand is `l(x1,y1,x2,y2)` for linear or `r(cx,cy,r)` for radial, followed by `-`-separated stops. Coordinates are **fractions of the bounding box**, 0 to 1. A stop may carry an explicit offset with `:` — `'l(0,0,1,0)#000-#f00:30%-#fff'` — and without one the stops space evenly.

The explicit constructors do the same thing with arguments instead of a string, and are easier to build from computed values:

```js
const ramp = paper.gradientLinear(0, 0, 1, 0);
ramp.addStop('#1b3b6f', 0).addStop('#4f86c6', 55).addStop('#c94f2b', 100);
ramp.stops();                        // read them back
ramp.setStops('#000-#fff');          // or replace them all
```

`gradientRadial(cx, cy, r, fx, fy)` takes an optional focal point. Linear gradients expose `x1`, `y1`, `x2`, `y2`; radial ones `cx`, `cy`, `r`.

> [!IMPORTANT]
> **The seam.** By default a gradient is measured against *each shape's own bounding box*, so two shapes sharing one gradient each receive the full ramp and a visible seam appears where they meet. A limb, a hull or a frame built from several overlapping shapes will not read as one form. Anchor the ramp in the paper's coordinates instead:
>
> ```js
> ramp.attr({ gradientUnits: 'userSpaceOnUse', x1: 0, y1: 0, x2: 800, y2: 0 });
> ```
>
> This is the commonest way a vector scene looks subtly wrong for a reason that is hard to see.

**Masks** take elements whose *luminance* becomes transparency — white shows, black hides, grey is partial:

```js
const soft = paper.mask(paper.rect(0, 0, 800, 600).attr({ fill: 'url(#' + fade.attr('id') + ')' }));
scene.attr({ mask: 'url(#' + soft.attr('id') + ')' });
```

**Patterns** tile a motif: `paper.ptrn(x, y, width, height, vx, vy, vw, vh)` returns a `<pattern>` you draw into and reference as a fill. Use it for hatching, screens and repeated devices that must stay vector — the geometric counterpart to a raster texture.

`paper.defs` is the container itself, created on demand. Append to it directly for anything the helpers do not cover — a `<symbol>`, a `<marker>`, a `<clipPath>` — using `paper.defs.el(name, attrs)`.

---

## 7. Geometry and Measurement

> **Implemented by**: `element.getBBox()`, `element.getTotalLength()`, `element.getPointAtLength(...)`, `Snap.path.getTotalLength(...)`, `Snap.path.getPointAtLength(...)`, `Snap.path.getBBox(...)`.

A vector scene measures itself, which is what lets it be composed rather than positioned by guess.

**`getBBox()`** returns `{ x, y, width, height, cx, cy, x2, y2 }` — and for a `<text>` element it is a **real font measurement**, honouring `font-family` (including a fallback list), `font-size`, `font-weight`, `font-style` and `text-anchor`. It agrees with `ctx.measureText(...)` for the same string. Remember SVG's `y` is the **baseline**, so the returned box starts above it.

```js
const heading = paper.text(40, 60, 'Meridian Cycles');
heading.attr({ 'font-family': 'Georgia', 'font-size': 34 });
const box = heading.getBBox();
paper.line(40, box.y2 + 12, 40 + box.width, box.y2 + 12);   // a rule at the measured width
```

**Path length and position.** `getTotalLength()` gives the arc length; `getPointAtLength(d)` gives `{ x, y, alpha }` at a distance along it, where `alpha` is the tangent angle in degrees. Together they are how anything gets distributed along a curve — spokes, teeth, beads, tick marks, letters, repeated motifs:

```js
const spine = paper.path('M40,220 C140,120 260,300 360,200');
const total = spine.getTotalLength();
for (let i = 0; i <= 10; i++) {
    const at = spine.getPointAtLength(total * i / 10);
    paper.circle(at.x, at.y, 3).attr({ fill: '#c94f2b' });
}
```

The same three calls exist on `Snap.path` for a bare `d` string, with no element required — `Snap.path.getTotalLength(d)`, `Snap.path.getPointAtLength(d, len)`, `Snap.path.getBBox(d)`. Use these when planning geometry before committing it to the document.

`Layout` and `Scale` are globals and work identically on either surface, so a measured heading composes on a paper exactly as it does on a canvas.

---

## 8. Crossing Back to Raster

> **Implemented by**: `ctx.drawSvg(...)`, `element.toSkPath()`, `paper.toImageBytes(...)`, `paper.toDataUri(...)`, `Snap.parse(...)`.

- **`ctx.drawSvg(paper, x, y, width, height)`** composites a whole vector scene onto a raster canvas, honouring the current transform, `globalAlpha`, `globalCompositeOperation` and filters. This is how a vector mark gets placed on a textured board.
- **`element.toSkPath()`** converts one element's geometry to a Skia path, for measurement or raster compositing.
- **`element.attr('d')`** hands you the path string, which `new CanvasPath(d)` accepts — the bridge to boolean operations. Cut a real counter with `outer.subtract(inner)` on the raster side, then read the result back into a `path` element if it must return to vector.
- **`paper.toImageBytes(w, h, format, quality)`** and **`paper.toDataUri(format, w, h, quality)`** render a paper directly, without going through a canvas.
- **`Snap.parse(svgXml)`** reads SVG markup back into an editable paper — the reopen half of `outSvg`, and how a later stage picks up an earlier stage's vector file.

---

## 9. What the Vector Surface Cannot Do

An honest list, because the failure mode is reaching for a raster capability halfway through a vector scene and rebuilding everything.

- **No brushes, path effects or mask filters.** `Skia.Brush.*`, `Skia.PathEffect.*` and `Skia.MaskFilter.*` are canvas-side. A pencil line, a stamped bristle stroke, hatching-as-geometry and an airbrushed edge are all raster.
- **No SkSL shaders or image filters.** Procedural noise, halftone shaders and blurs are canvas-side. An SVG gradient is the vector equivalent, and it is much less expressive.
- **No text wrapping.** `ctx.measureWrappedText` and `ctx.fillWrappedText` are canvas-only, because SVG has no flow. A paragraph broken into `<tspan>` lines is your decision, line by line.
- **No pixel measurement.** `bitmap.diff`, `rowProfile`, `getPixel` and `palette` operate on rendered bitmaps. To verify a vector scene, render it and measure the render.
- **No boolean operations on elements directly.** Go through `attr('d')` → `CanvasPath` → the operation, as in §8.

If a scene needs several of these, it is a raster scene. Decide that in §2 rather than at the point of frustration.

---

## 10. Traps

| What happens | Why | What to do |
| :--- | :--- | :--- |
| `outSvg` writes no file, and the run reports success | The script built a canvas, not a paper; there is no markup to write | Build on `Snap(w, h)`. The engine now warns, but only after the stage is drawn |
| `clone()` seems to do nothing | The copy is detached | `clone().appendTo(paper)` |
| A cloned element serializes with an id like `spine#1` | The copy carries the original's id and the serializer disambiguates it | Assign the clone a new `id` before placing it |
| `select('#name')` returns `null` on a shape you just drew | Elements have no id until assigned | `el.id = 'name'` first |
| `paper.children[0]` is not your first shape | `<defs>` is a child | Filter on `type`, or read `paper.defs` explicitly |
| Two shapes sharing a gradient show a seam between them | Gradient units default to each shape's own bounding box | `gradientUnits: 'userSpaceOnUse'` with absolute coordinates (§6) |
| Calling `children` or `parent` as a method throws "not a function" | They are properties here, not the Snap.svg methods of the same name | Drop the parentheses |
| A hex colour reads back as different text than you set | `attr` normalises and uppercases | Compare case-insensitively, or keep your own value |

---

## 11. Symbol → SDK Map

| What you want | The call | Notes |
| :--- | :--- | :--- |
| A vector document | `Snap(width, height)` | Returns a `SnapPaper` |
| Reopen saved markup | `Snap.parse(svgXml)` | The counterpart to `outSvg` |
| Set / read style | `element.attr(...)` | Chainable when setting; hyphenated keys quoted |
| Find something | `element.select(sel)`, `element.selectAll(sel)` | `#id`, `.class`, or a tag name |
| Walk the tree | `element.children`, `element.parent` | Properties, not methods |
| Reorder | `appendTo`, `prependTo`, `before`, `after` | Document order *is* z-order |
| Copy | `element.clone()` | Detached; place it yourself |
| Move / rotate / scale | `element.transform('t..r..s..')` | Read back with `transform()` |
| Transform arithmetic | `Snap.matrix()`, `mult`, `multLeft`, `invert`, `skewX`, `skewY` | `determinant`, `isIdentity` for checks |
| Where did a point go? | `matrix.transformPoint(x, y)` | Returns `{ x, y }` |
| Gradient | `paper.gradient(desc)`, `gradientLinear`, `gradientRadial` | Reference by `attr('id')`; watch the seam |
| Gradient stops | `gradient.addStop(...)`, `setStops(...)`, `stops()` | `addStop` chains |
| Mask / pattern / defs | `paper.mask(...)`, `paper.ptrn(...)`, `paper.defs` | Luminance masks: white shows |
| Measure | `element.getBBox()` | Real font metrics for `<text>`; `y` is the baseline |
| Along a curve | `getTotalLength()`, `getPointAtLength(d)` | `alpha` is the tangent in degrees |
| Same, on a bare string | `Snap.path.getTotalLength/getPointAtLength/getBBox` | No element needed |
| To raster | `ctx.drawSvg(...)`, `element.toSkPath()`, `paper.toImageBytes(...)`, `paper.toDataUri(...)` | One-way |
| The markup | `paper.toString()` | What `outSvg` writes |

---

## 12. Constructing It: A Runnable Vector Plate

Tree first, style second, measurement third. Everything below stays addressable after it is drawn — which is the whole reason to work this way.

```javascript
// A vector plate: groups, ids, paint servers, transforms, measurement, reordering.
// Returns the paper, so ExecuteScript can write both outFile and outSvg.
const paper = Snap(820, 560);

// §6 — Paint servers first: they land in <defs> and are referenced by id.
// userSpaceOnUse anchors the ramp to the paper, so shapes sharing it show no seam.
const sky = paper.gradient('l(0,0,0,1)#eef3f8-#cdd9e5');
sky.attr({ gradientUnits: 'userSpaceOnUse', x1: 0, y1: 0, x2: 0, y2: 560 });
paper.rect(0, 0, 820, 560).attr({ fill: 'url(#' + sky.attr('id') + ')' });

const metal = paper.gradientLinear(0, 0, 1, 0);
metal.addStop('#1b3b6f', 0).addStop('#4f86c6', 55).addStop('#c94f2b', 100);
metal.attr({ gradientUnits: 'userSpaceOnUse', x1: 160, y1: 0, x2: 520, y2: 0 });
log('metal stops = ' + metal.stops().length + ', defs holds ' + paper.defs.children.length);

// §4 — Group early and give ids. A later stage edits what it can find.
const frame = paper.g();
frame.id = 'frame';

const spine = frame.path('M160,380 L330,250 L520,380 Z');
spine.id = 'spine';
spine.attr({ fill: 'none', stroke: 'url(#' + metal.attr('id') + ')', 'stroke-width': 10,
             'stroke-linejoin': 'round' });

for (const cx of [160, 520]) {
    const wheel = frame.circle(cx, 380, 78);
    wheel.attr({ fill: 'none', stroke: '#1b3b6f', 'stroke-width': 6, 'class': 'wheel' });
}
log('frame holds ' + frame.children.length + ' children; spine parent = ' + spine.parent.type);

// §7 — Spokes distributed along the rim by arc length, not by guessed angles.
// The rim is a measuring guide, so it is removed once it has been read: a guide left
// in the tree is a guide that ships in the .svg.
const rim = paper.path('M82,380 A78,78 0 1,1 238,380 A78,78 0 1,1 82,380');
const total = rim.getTotalLength();
const spokes = paper.g();
spokes.id = 'spokes';
for (let i = 0; i < 12; i++) {
    const at = rim.getPointAtLength(total * i / 12);
    spokes.line(160, 380, at.x, at.y).attr({ stroke: '#8fa6c2', 'stroke-width': 1.5 });
}
rim.remove();
log('rim length ' + total.toFixed(1) + 'px; paths left at top level: ' +
    paper.children.filter(el => el.type === 'path').length);

// §4 — Selection, cloning and placement. A clone is detached until you place it,
// and it carries the original's id, so rename it before it collides.
const shadow = paper.select('#spine').clone();
shadow.id = 'spine-shadow';
shadow.attr({ stroke: '#0d1a2e', 'stroke-width': 14, opacity: 0.16 });
shadow.transform('t6,10');
shadow.appendTo(paper);
shadow.before(frame);                       // document order is z-order: behind the frame
log('wheels found by class: ' + paper.selectAll('.wheel').length);

// §5 — Matrix arithmetic: transformPoint reports where the ink actually landed,
// which is what lets a label be placed against a rotated part rather than beside it.
// Created last, so it sits on top of the frame it is mounted on.
// Radius 78 is the wheel's own radius, so the lamp lands exactly on the rim —
// a placement you can check by eye against a number you never had to guess.
const lamp = Snap.matrix().translate(520, 380).rotate(-38);
const tip = lamp.transformPoint(78, 0);
paper.circle(tip.x, tip.y, 9).attr({ fill: '#f0b429', stroke: '#8a6410', 'stroke-width': 2 });
log('lamp at ' + tip.x.toFixed(1) + ',' + tip.y.toFixed(1) +
    ' | matrix ' + lamp.toTransformString() + ' det=' + lamp.determinant.toFixed(2));

// §7 — Text measures itself, so the rule under it is the width of the actual glyphs.
const title = paper.text(120, 110, 'Meridian Cycles');
title.attr({ 'font-family': 'Georgia, serif', 'font-size': 44, fill: '#12203a' });
const box = title.getBBox();
paper.line(120, box.y2 + 14, 120 + box.width, box.y2 + 14)
     .attr({ stroke: '#c94f2b', 'stroke-width': 3 });
log('title measures ' + box.width.toFixed(1) + ' x ' + box.height.toFixed(1) +
    ' (baseline y=110, box starts at y=' + box.y.toFixed(1) + ')');

// §4 — What a later stage would walk. <defs> is a child of the paper; filter it out.
const drawn = paper.children.filter(el => el.type !== 'defs').map(el => el.type);
log('top level: ' + drawn.join(', '));

// §3 — The markup is what outSvg writes. Log its size as a check that it exists.
log('SVG markup: ' + paper.toString().length + ' bytes');

paper;
```
