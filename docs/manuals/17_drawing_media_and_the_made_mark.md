# Studio Manual 17: Drawing Media & the Made Mark

> **Credits & Theoretical Foundation**: Distilled from Klaus Janson, *The DC Comics Guide to Pencilling Comics* (Watson-Guptill / DC Comics, 2002), **ch. 1 Materials** — paper tooth, lead grades, non-repro blue, drawing with the eraser — and **ch. 13 Procedure** for the rough-to-finish sequence. All rights reserved; distilled and cited in our own words, never quoted at length. Ledger entry 2026-08-27. The media primitives are documented at `polson://sdk/core/Skia`.
> **Purpose**: What a mark is *made of*. Choosing a medium, the three layers every medium decomposes into, modifying one, and how to keep a drawn line from reading as a plotted one.

---

## 1. Every Line Here Starts as a Template Line

> **Implemented by**: nothing yet — this is the problem the rest of the manual solves.

Janson makes a small point about templates that turns out to be the whole of this manual when you move into code. Draw an oval freehand, he says, then draw one with a plastic template: the first is shaky and inaccurate, the second smooth and precise. He recommends the template.

**In this medium you only have the template.** `ctx.lineTo` is a perfect line. `ctx.arc` is a perfect arc. Nothing here is shaky, ever, unless you ask for it — so the correction runs the other way round from the drawing board. On paper you reach for a straight-edge to remove the hand; here you reach for `Skia.PathEffect.discrete` to put it back.

That is not decoration. Janson's advice about layouts is that too much detail too early makes work look **stale and stiff** — he keeps his own layouts as rough as he can precisely so the finished drawing stays fresh. A construction pass rendered in immaculate 1px hairlines is the same failure by a different route: it looks finished before it is designed, and it invites nobody, including you, to change it.

> [!NOTE]
> This manual is about what a mark is **made of**. Manual 03 covers what marks are **worth** — the
> three-tier line weight hierarchy, feathering, spotted blacks — and the two compose: Manual 03 tells
> you the silhouette gets the heaviest line, this one tells you what that line is made of.

---

## 2. What a Medium Is Made Of

> **Implemented by**: `ctx.useBrush(...)`, `ctx.pathEffect`, `ctx.maskFilter`, `ctx.strokeStyle`, and the `BrushPreset` parts.

Janson's Figure 1.1 shows the same 2B pencil on two papers — and it is the *paper* you see. On the coarse sheet the tooth shows through the mark; the smooth sheet takes a finer, denser line, and he notes it changes how he thinks, pushing him toward larger and simpler shapes on the rough paper.

A mark is therefore never just a coloured line. It is three separable things, and the SDK exposes exactly those three:

| Layer | What it is | The call | Janson's version |
| :--- | :--- | :--- | :--- |
| **Grain** | What the mark is *made of* — an uneven deposit rather than a flat colour | `SKShader`, on `strokeStyle` | The paper's tooth showing through the graphite |
| **Texture** | What happens to the *path* — jitter, stamping, repetition | `SKPathEffect`, on `ctx.pathEffect` | The hand's wobble; a chisel tip; a dry brush |
| **Edge** | What happens at the mark's *boundary* | `SKMaskFilter`, on `ctx.maskFilter` | Whether the medium bites the sheet or sits on it |

Any of the three may be absent, and which are absent is what makes one medium unlike another.

> [!IMPORTANT]
> Two naive attempts at grain fail in **opposite** directions, which is why the presets exist rather
> than just the primitives. Multiplying a stroke's alpha by noise luminance fades the mark to nothing;
> tinting noise through a colour filter flattens it back into a solid line. The working construction is
> a noise shader used *as the paint*, and it is not discoverable.

---

## 3. The Five Media

`Skia.Brush.*` returns a `BrushPreset`. Here is what each is actually made of — read off the presets themselves, not from the documentation of them:

| Preset | Colour | Width | Cap | Grain | Texture | Edge |
| :--- | :--- | ---: | :--- | :---: | :---: | :---: |
| `pencil()` | `#3a3a3a` | 2.2 | round | ● | ● | ● |
| `ink()` | `#0a0a0c` | 2.6 | round | — | ● | — |
| `chalk()` | `#2e2a26` | 7 | round | ● | — | ● |
| `marker()` | `#1c2530` | 6 | square | — | — | ● |
| `stipple()` | `#3a3a3a` | 1 | round | — | ● | — |

The table *is* the explanation:

- **`pencil(color, width, grain, seed)`** is the only medium with all three layers, because graphite is the only one that is simultaneously granular, wandering and soft-edged. `grain` is `0` for an even deposit, `1` by default, higher for a drier pencil. **Construction and search.**
- **`ink(color, width, steadiness)`** has texture and nothing else — solid, crisp, with just enough irregularity not to read as plotted. `steadiness` of `1` is mechanically exact. **Commitment.** Manual 03's three-tier weight hierarchy is roughly `width` 4, 2 and 1.
- **`chalk(color, width, grain, seed)`** is granular and soft-edged but travels straight: coarse, wide, no hard boundary. **Mass and tone**, not line.
- **`marker(color, width)`** is flat and opaque with only a faint bleed at the edge. **Blocking** — Janson roughs in his layouts with broad-tipped markers for exactly this reason: it is a medium that cannot render detail, so it does not tempt you to.
- **`stipple(color, size, spacing, seed)`** is separate deposits along the path rather than a line at all. `spacing` defaults to three times `size`, because a spacing below the mark's own width overlaps them back into a solid band. **Texture and gradation.**

### Applying one

```js
ctx.save();
ctx.useBrush(Skia.Brush.pencil());     // construction
ctx.stroke(guides);
ctx.restore();

ctx.useBrush(Skia.Brush.ink('#0a0a0c', 4));   // the heavy silhouette
ctx.stroke(outline);
```

> [!IMPORTANT]
> **`useBrush` clears what the preset leaves null**, rather than leaving the previous medium's parts
> standing. Switching from `pencil` to `ink` removes the pencil's soft edge and its grain — verified:
> after `useBrush(pencil())` the mask filter is set; after `useBrush(ink())` it is `null` again.
> Without that, every medium after the first would be contaminated by the one before it.
>
> `useBrush` sets `strokeStyle`, `lineWidth`, `lineCap`, `pathEffect` and `maskFilter` — all five are
> drawing state, so `save()` / `restore()` scopes a medium to one passage.

---

## 4. Modifying a Preset

The preset is readable and every part is settable, so the ordinary move is to take a medium and change one thing:

```js
const soft = Skia.Brush.pencil();
soft.edge = Skia.MaskFilter.blur(3, 'normal');   // softer than the default
soft.texture = null;                              // and let the path run clean
soft.lineWidth = 5;
ctx.useBrush(soft);
```

`useBrush` reads the parts when it is called, so adjust first and apply after.

> [!WARNING]
> This genuinely works now, and it did not before. Assigning `brush.color` used to **read back as the
> new value and draw the old one** — a script could verify its own change and be wrong — while
> `brush.lineWidth` did not even read back. If you are working against an older build and a modified
> preset seems to be ignored, set the context properties directly after `useBrush` instead:
> `ctx.useBrush(b); ctx.lineWidth = 5;`.

---

## 5. Grain — What the Mark Is Made Of

> **Implemented by**: `Skia.Shader.perlinNoiseTurbulence(...)`, `Skia.Shader.perlinNoiseFractal(...)`, `Skia.Shader.bitmap(...)`, `Skia.Shader.radial(...)`, `Skia.Shader.sweep(...)`, `Skia.Shader.twoPointConical(...)`, `Skia.Shader.sksl(...)`.

Grain is an `SKShader` assigned to `strokeStyle` or `fillStyle`, so the mark is painted with a texture rather than a colour. All of them run natively, so a textured stroke costs about what a plain one does.

- **`perlinNoiseTurbulence(fx, fy, octaves, seed)`** — the granular one. Low frequencies give a coarse tooth, high a fine one; more octaves add detail at every scale.
- **`perlinNoiseFractal(fx, fy, octaves, seed)`** — smoother, cloud-like. Atmosphere rather than grit. (`Drawing.createAtmosphericCloudShader` is a preset over this, with sea-air defaults — see Manual 04.)
- **`luminance(shader)`** — any of the above as a **value** field: RGB collapsed to luminance, alpha untouched. Read the warning below before using either noise shader as grain.

> [!WARNING]
> **Both noise shaders are coloured, and grain is the use that exposes it.** Each generates four independent noise fields — one per channel, alpha included — so `perlinNoiseTurbulence` assigned straight to `strokeStyle` is not tooth on a mark, it is confetti. Over a coloured base under `soft-light` or `overlay` it tints in random hues; a measured run turned ~700 × 500 px of brick from brown to olive green and lost an iteration to it.
>
> Grain wants **one** field, so wrap it: `Skia.Shader.luminance(Skia.Shader.perlinNoiseTurbulence(0.6, 0.6, 3, 11))`. The `Skia.Brush` presets never hit this, because their internal shader averages the three channels to a scalar and paints the brush's own colour through it — which is why `Skia.Brush.pencil(...)` behaves and a hand-rolled equivalent may not.
>
> **The alpha field must be left alone.** It varies as well, and clamping it in the same colour matrix — the obvious second correction — greys the noise and flattens the medium into an opaque sheet. Luminance on RGB, alpha untouched, is a narrow path with a failure on either side; `luminance` is the path.
- **`bitmap(bmp, tileX, tileY)`** — a real texture as paint. This is where a requisitioned material (Manual 16) becomes a *medium* rather than a fill: clip a shape and stroke it with the surface it is made of.
- **`radial`, `sweep`, `twoPointConical`, `linear`** — gradients as paint. A gradient running **across** a stroke rather than along it is how a mark gets a lit side and a shadow side.
- **`sksl(code, uniforms)`** — anything else, as a compiled pixel shader. `Skia.Shader.custom` is the same call under a second name; prefer `sksl`, which says what the argument is.

> [!IMPORTANT]
> **This is not CanvasKit, and the shader names are the place that bites.** There is no
> `Skia.RuntimeEffect`, no `.make(...)`, no `SkRuntimeEffect` — a live run reached for
> `Skia.RuntimeEffect.make(sksl)` and got nothing, because that is the browser binding's spelling and
> not this one. Everything compiled from SkSL enters through one of three calls:
>
> | You want | Here it is |
> | :--- | :--- |
> | A shader — colour per pixel | `Skia.Shader.sksl(code, uniforms)` → assign to `fillStyle` / `strokeStyle` |
> | A colour filter — transform an existing colour | `Skia.ColorFilter.runtimeEffect(code, uniforms)` → `ctx.colorFilter` |
> | An image filter — read neighbouring pixels | `Skia.ImageFilter.runtimeShader(code, uniforms)` → `ctx.filter` |
>
> There is no compile step and no effect object to hold: each call takes the SkSL source and returns
> the thing you assign. A shader's entry point is `half4 main(float2 coord)`; a colour filter's is
> `half4 main(half4 inColor)`.
>
> The noise presets are worth checking by name too — they are `perlinNoiseTurbulence` and
> `perlinNoiseFractal`, and appending "Noise" to the second one is a mistake a run has already made.

### The grain is measurable, and it is reproducible

Two pencil strokes differing only in `grain`, compared with `bitmap.diff`:

```
grain 0 vs grain 3:  differing = 1357px, similarity = 0.929, identical = false
even deposit:  #3B3B3B covers 4.2% of the frame
dry deposit:   #3B3B3B covers 1.0% of the frame
```

That is the right behaviour and it is worth seeing as a number: a drier pencil lays down **less graphite**, not differently-coloured graphite. If a grain setting is not changing the deposit share, it is not doing anything.

**The `seed` makes a mark repeatable.** Same seed, same stroke, byte for byte — verified with `diff().identical`. Different seed, different stroke. So a texture you liked can be re-made in a later stage, and a stage can be re-run without the drawing shifting under it. Pass a seed explicitly whenever the result has to survive a re-render.

---

## 6. Texture — What Happens to the Path

> **Implemented by**: `Skia.PathEffect.discrete(...)`, `.stamp(...)`, `.hatch(...)`, `.tile(...)`, `.dash(...)`, `.corner(...)`, `.sum(...)`, `.compose(...)`; `ctx.pathEffect`, `ctx.setLineDash(...)`.

- **`discrete(segLength, deviation, seed)`** — the hand. Chops the path into segments and jitters them. This is the direct answer to §1: it is how a plotted line stops being plotted.
- **`stamp(shape, advance, phase, style)`** — the brush primitive. Repeats a shape along the path every `advance` pixels. `'rotate'` (default) turns each mark to the tangent, which is what makes bristles, foliage, grass and stitching read as drawn; `'translate'` keeps them upright; `'morph'` bends them to the curve. An `advance` below the mark's own width overlaps them into a continuous textured band.
- **`hatch(width, spacing, angle)`** — parallel hatch lines **as geometry**, so they scale and export as vector.
- **`tile(shape, spacing, angle)`** — a motif on a square lattice: stipple, screen tone, a repeated device.
- **`dash(intervals, phase)`**, **`corner(radius)`** — mechanical effects. `ctx.setLineDash([...])` composes with `pathEffect` rather than replacing it.

### Summing versus composing

This distinction is the difference between a mechanical stroke and a drawn one, and it is easy to get backwards.

- **`sum(a, b)`** applies both to the *original* path and draws both results. Two hatches at opposing angles give cross-hatching.
- **`compose(outer, inner)`** applies `inner` first, then `outer` to its result.

```js
// Rough the path up first, then stamp along the roughened version — so the marks
// inherit the irregularity instead of marching evenly down a clean curve.
ctx.pathEffect = Skia.PathEffect.compose(
    Skia.PathEffect.stamp(bristle, 4),
    Skia.PathEffect.discrete(6, 2, 7));
```

Stamping a clean curve gives a row of identical marks at identical intervals, which reads as a machine. Composing the jitter underneath is what makes it read as a brush.

---

## 7. Edge — What Happens at the Boundary

> **Implemented by**: `Skia.MaskFilter.blur(sigma, style)`, on `ctx.maskFilter`.

A mask filter softens the **shape's coverage** before it is painted. That is not the same as blurring the result:

| | Softens | Leaves the fill | Reads as |
| :--- | :--- | :--- | :--- |
| `Skia.MaskFilter.blur(...)` | the shape | flat | an airbrushed edge |
| `Skia.ImageFilter.blur(...)` | the rendered pixels | blurred too | an out-of-focus object |

The four styles are genuinely different marks:

- **`'normal'`** — softens the whole shape. Airbrush, chalk, soft pencil.
- **`'solid'`** — keeps the shape crisp and adds the blur *outside* it. A glow around a hard form.
- **`'outer'`** — keeps only the blur and knocks the shape out. A halo.
- **`'inner'`** — keeps only the blur inside. An inward vignette; the shading just in from a contour.

---

## 8. The Rough-to-Finish Sequence

Janson's own procedure is two-tier, and both tiers matter.

**The underdrawing is a different medium, not a lighter version of the same one.** He uses non-repro blue for laying in borders and basic positioning — blue lead does not reproduce, so it never has to be erased, and it keeps the construction from competing visually with the drawing on top of it. That colour is already a parameter across this SDK: `blueLineColor` defaults to `#4a90e2` in `Drawing.drawLoomisWireframe(...)` and `Drawing.drawMannequinWireframe(...)`, against `graphiteColor` `#444444`. It is there for exactly Janson's reason.

**Within the graphite, he shifts grade rather than pressure**: a hard 2H to get rough shapes down, then an H or HB to finish. Hard leads sit light and can score the sheet; softer leads glide and go darker — though he is careful to say hard and light are not strictly the same axis, and the softest pencils are often the darkest.

In code that is two presets and a colour:

```js
ctx.useBrush(Skia.Brush.pencil('#4a90e2', 1.4, 0.4));   // non-repro blue: positioning
ctx.stroke(armature);
ctx.useBrush(Skia.Brush.pencil('#444444', 2.2, 1));     // graphite: the drawing
ctx.stroke(construction);
ctx.useBrush(Skia.Brush.ink('#0a0a0c', 4));             // the committed line
ctx.stroke(silhouette);
```

**How rough to leave the pencils is a decision about who inks them.** Janson stays deliberately loose when he is inking his own work, so the ink pass has something to contribute rather than a line to trace; he tightens up when someone else will ink, to keep control of the result. The same choice exists in a studio run: a stage that hands off to another agent should be tight, and a stage that is going to refine its own work should not be.

**And check the layout by squinting.** Janson's test for whether a page works is to squint at it: if the black-and-white shapes still read and the story is still clear, it works. The code equivalent is in Manual 15 — collapse to luminance and read the bands, which is the same test with a number attached.

---

## 9. Erasing Is a Mark

Janson cuts a sliver off an eraser and *draws* with it — feeling his way around a form, peeling back layers of grey when he has laid down too many lines to see the shape any more. He calls it **scumbling**, and treats it as part of drawing rather than as correction.

`globalCompositeOperation = 'destination-out'` is that eraser: what you draw removes coverage instead of adding it.

```js
ctx.save();
ctx.globalCompositeOperation = 'destination-out';
ctx.useBrush(Skia.Brush.chalk('#000', 10));   // a soft-edged eraser, not a hard one
ctx.stroke(searchPath);
ctx.restore();
```

Verified: a pixel under a hard-edged `destination-out` fill reads back as `#00000000` — genuinely erased to transparent, not painted over in the background colour. That difference matters the moment anything is composited underneath.

**The medium of the eraser is a choice too**, and it changes what the erasure *means*. A hard-edged eraser clears coverage completely and reads as a mistake being corrected. A chalk or pencil eraser has a soft edge and a granular deposit, so it removes coverage **partially** — most affected pixels are lightened rather than cleared — and that reads as a form being searched for, which is what Janson is describing.

> [!TIP]
> Because a soft erasure is partial, sampling one pixel is a poor way to check it — the point you
> pick may be under the soft shoulder of the stroke and barely touched. Keep a copy before the pass
> and `diff` it after (Manual 15 §4): `differingPixels` says how much was lifted and `bounds` says
> where, neither of which depends on guessing a coordinate.

---

## 10. When a Stroke Should Become a Shape

> **Implemented by**: `ctx.strokeToPath(path?)`.

A stroke has one width along its whole length. A drawn mark does not. `strokeToPath` returns the outline of what `stroke()` *would* draw, as a fillable path — after which it is geometry you can work on:

```js
ctx.lineWidth = 14;
const mark = ctx.strokeToPath(spine);   // the stroke, as a shape
ctx.fill(mark.subtract(bite));          // now it can be cut
```

It bakes in the current `lineWidth`, `lineCap`, `lineJoin`, `miterLimit` and `pathEffect` — so a stamped or hatched stroke becomes real geometry and survives into `outSvg` as vector rather than only as pixels (Manual 14 §8). Use it to taper a mark toward one end for pressure, to cut something out of it, or to fill it with a gradient running *across* the stroke.

---

## 11. Filters as Material

> **Implemented by**: `Skia.ImageFilter.dilate(...)`, `.erode(...)`, `.dropShadow(...)`, `.colorFilter(...)`, `.runtimeShader(...)`; `Skia.ColorFilter.runtimeEffect(...)`; `ctx.filter`, `ctx.dither`.

- **`dilate(rx, ry)` / `erode(rx, ry)`** — thicken or thin every mark on a layer at once. This is the cheapest way to get a whole pass to read heavier or lighter without redrawing it, and the honest way to test whether a line weight hierarchy is actually doing the work.
- **`dropShadow(dx, dy, sx, sy, color)`** — offset shadow as an image filter.
- **`colorFilter(f)`** — bridges a colour filter into an image-filter pipeline.
- **`runtimeShader(sksl, uniforms)`** and **`ColorFilter.runtimeEffect(sksl, uniforms)`** — custom SkSL where the presets run out.
- **`ctx.dither = true`** — trades fine noise for the absence of banding. Off by default, and worth turning on for a wide shallow gradient — a sky, a soft tonal ramp — where 8-bit steps otherwise show as visible bands.

---

## 12. Traps

| What happens | Why | What to do |
| :--- | :--- | :--- |
| A new medium still has the last one's soft edge | Something was set directly on `ctx` after `useBrush` | `useBrush` clears null parts, but a later `ctx.maskFilter = …` persists; scope with `save()`/`restore()` |
| A stamped stroke looks machine-made | `stamp` on a clean path repeats identically | `compose(stamp, discrete)` — jitter underneath (§6) |
| A textured stroke fades to nothing | Alpha was multiplied by noise luminance | Use the noise **as the paint**, or use a preset |
| A textured stroke is a flat line | Noise was tinted through a colour filter | Same |
| A re-render changes the texture | No `seed` was passed | Pass one; same seed is byte-identical |
| `stipple` draws a solid line | `spacing` is below the mark's width | Leave `spacing` to default, or set it above `size` |
| A soft edge blurs the whole shape's colour | `ImageFilter.blur` blurs the result, not the coverage | `MaskFilter.blur` for an edge (§7) |
| `Object.keys(brush)` shows `Color`, `LineWidth` | It is a typed object; the reference documents the camelCase spelling Jint resolves onto it | Read `brush.color`; do not iterate the keys |
| An erased area still hides what is beneath it | It was painted in the background colour, not erased | `destination-out` (§9) |

---

## 13. Symbol → SDK Map

| What you want | The call |
| :--- | :--- |
| A medium | `Skia.Brush.pencil / ink / chalk / marker / stipple` |
| Apply it | `ctx.useBrush(brush)` — clears what the preset leaves null |
| See what it is made of | `brush.grain`, `brush.texture`, `brush.edge`, `brush.color`, `brush.lineWidth`, `brush.lineCap`, `brush.name` |
| Change one part | Assign to it, then `useBrush` |
| Granular paint | `Skia.Shader.perlinNoiseTurbulence(...)` |
| Cloudy paint | `Skia.Shader.perlinNoiseFractal(...)` |
| A real texture as paint | `Skia.Shader.bitmap(...)` |
| Gradient paint | `Skia.Shader.radial / sweep / twoPointConical / linear` |
| Anything else as paint | `Skia.Shader.sksl(...)` |
| Put the hand back | `Skia.PathEffect.discrete(seg, dev, seed)` |
| A brush mark repeated | `Skia.PathEffect.stamp(shape, advance, phase, style)` |
| Hatching as geometry | `Skia.PathEffect.hatch(w, spacing, angle)` |
| A motif on a lattice | `Skia.PathEffect.tile(shape, spacing, angle)` |
| Both treatments | `Skia.PathEffect.sum(a, b)` |
| One fed into the other | `Skia.PathEffect.compose(outer, inner)` |
| A soft edge | `Skia.MaskFilter.blur(sigma, 'normal' / 'solid' / 'outer' / 'inner')` |
| Thicken / thin a whole pass | `Skia.ImageFilter.dilate(...)` / `.erode(...)` |
| A stroke you can cut | `ctx.strokeToPath(path)` |
| Erase as a mark | `globalCompositeOperation = 'destination-out'` |
| Kill gradient banding | `ctx.dither = true` |
| Did the medium change? | `bitmap.diff(...)`, `bitmap.palette(...)` — Manual 15 |

---

## 14. Making It: A Runnable Specimen Sheet

The thing an artist actually makes when trying materials: the same stroke in every medium, on one sheet, at the same size — plus the sequence from §8 and the measurement from §5.

```javascript
// A media specimen sheet: one stroke, five media, then the rough-to-finish sequence.
const canvas = createCanvas(820, 560);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#fbfaf7';
ctx.fillRect(0, 0, 820, 560);
ctx.textBaseline = 'top';

// The same path for every medium, so the only variable is the medium itself.
function spine(y) {
    const p = new CanvasPath();
    p.moveTo(180, y);
    p.bezierCurveTo(320, y - 26, 480, y + 26, 700, y - 6);
    return p;
}

// §3 — the five presets, and what each is made of, read off the preset itself.
const media = [
    Skia.Brush.pencil('#3a3a3a', 4, 1, 11),
    Skia.Brush.ink('#0a0a0c', 4),
    Skia.Brush.chalk('#2e2a26', 9, 1, 11),
    Skia.Brush.marker('#1c2530', 7),
    Skia.Brush.stipple('#3a3a3a', 2.5, 8, 11)
];

const page = Layout.inset(Layout.rect(0, 0, 820, 560), 28);
ctx.font = '700 20px sans-serif';
ctx.fillStyle = '#1c2733';
ctx.fillText('Media specimen — one path, five media', page.x, page.y);

const bands = Layout.stack(Layout.rect(page.x, page.y + 40, page.width, 300), 
    media.map(() => 52), 8);

for (let i = 0; i < media.length; i++) {
    const brush = media[i];
    const y = bands[i].cy;

    // The label reports the decomposition, not just the name — grain / texture / edge.
    ctx.font = '600 13px sans-serif';
    ctx.fillStyle = '#2b3742';
    ctx.fillText(brush.name, bands[i].x, bands[i].y + 8);
    ctx.font = '400 11px sans-serif';
    ctx.fillStyle = '#8a94a0';
    ctx.fillText([brush.grain ? 'grain' : null, brush.texture ? 'texture' : null,
                  brush.edge ? 'edge' : null].filter(Boolean).join(' · ') || 'plain',
                 bands[i].x, bands[i].y + 26);

    ctx.save();
    ctx.useBrush(brush);          // §3 — clears whatever the previous medium left set
    ctx.stroke(spine(y));
    ctx.restore();
}

// §8 — the sequence: non-repro blue, then graphite, then the committed line.
const seq = Layout.rect(page.x, page.y + 356, page.width, 150);
ctx.font = '700 15px sans-serif';
ctx.fillStyle = '#1c2733';
ctx.fillText('Rough to finish — blue armature, graphite search, inked commitment', seq.x, seq.y);

// Each pass sits slightly off the one under it, which is the point: the armature is
// positioning, the graphite is a search, and only the ink commits. Drawing all three
// on identical coordinates would hide the first two under the last.
function ridge(dx, dy) {
    const p = new CanvasPath();
    p.moveTo(seq.x + 30 + dx, seq.y + 116 + dy);
    p.lineTo(seq.x + 140 + dx, seq.y + 42 + dy);
    p.lineTo(seq.x + 290 + dx, seq.y + 104 + dy);
    return p;
}

ctx.save();
ctx.useBrush(Skia.Brush.pencil('#4a90e2', 3, 0.3, 3));   // non-repro blue: positioning
ctx.stroke(ridge(-7, 7));
ctx.restore();

// §6 — the search pass: jitter composed under the medium's own texture, so the
// marks inherit the wobble instead of marching evenly down a clean path.
ctx.save();
ctx.useBrush(Skia.Brush.pencil('#444444', 2.4, 1, 5));
ctx.pathEffect = Skia.PathEffect.compose(ctx.pathEffect, Skia.PathEffect.discrete(7, 2.2, 5));
ctx.stroke(ridge(-3, 3));
ctx.restore();

ctx.save();
ctx.useBrush(Skia.Brush.ink('#0a0a0c', 4));              // the committed line
ctx.stroke(ridge(0, 0));
ctx.restore();

// §9 — erasing as a mark. It needs something to erase FROM, so lay a chalk mass
// down first: destination-out removes that coverage rather than painting over it.
const mass = Layout.rect(seq.x + 430, seq.y + 34, 250, 78);
ctx.save();
// Spacing above the stroke width, so the strokes read as a laid tone rather than
// overlapping into a solid block — the same rule stipple's spacing follows.
ctx.useBrush(Skia.Brush.chalk('#2e2a26', 16, 1, 4));
for (let y = mass.y + 18; y < mass.y2 - 8; y += 20) {
    ctx.beginPath();
    ctx.moveTo(mass.x + 12, y);
    ctx.lineTo(mass.x2 - 12, y);
    ctx.stroke();
}
ctx.restore();

const beforeErase = canvas.toBitmap();          // §5 of Manual 15: keep a copy, not a live handle

ctx.save();
ctx.globalCompositeOperation = 'destination-out';
ctx.useBrush(Skia.Brush.chalk('#000000', 22, 1, 2));
const scumble = new CanvasPath();
scumble.moveTo(mass.x + 34, mass.y2 - 20);
scumble.bezierCurveTo(mass.cx - 30, mass.y + 8, mass.cx + 40, mass.y2 - 10, mass.x2 - 28, mass.y + 16);
ctx.stroke(scumble);
ctx.restore();

ctx.font = '400 11px sans-serif';
ctx.fillStyle = '#8a94a0';
ctx.fillText('chalk mass, scumbled back', mass.x, mass.y2 + 14);

// Prove the erasure by measuring it, rather than by sampling one pixel and hoping.
// A soft-edged eraser removes coverage PARTIALLY, so most affected pixels are lightened
// rather than cleared — which is exactly what makes it read as searching for a form
// instead of correcting a mistake.
const erased = beforeErase.diff(canvas.toBitmap());
log('scumble removed coverage from ' + erased.differingPixels + ' pixels, within ' +
    erased.bounds.width + 'x' + erased.bounds.height + ' at ' + erased.bounds.x + ',' + erased.bounds.y);

// §5 — the grain is measurable. Two pencils differing only in grain, compared.
function sample(grain) {
    const c = createCanvas(240, 60);
    const x = c.getContext('2d');
    x.fillStyle = '#ffffff';
    x.fillRect(0, 0, 240, 60);
    x.useBrush(Skia.Brush.pencil('#3a3a3a', 6, grain, 1));
    x.beginPath();
    x.moveTo(20, 30);
    x.lineTo(220, 30);
    x.stroke();
    return c.toBitmap();
}
const even = sample(0);
const dry = sample(3);
const delta = even.diff(dry);
log('grain 0 vs 3: differing=' + delta.differingPixels +
    ', similarity=' + delta.similarity.toFixed(3) + ', identical=' + delta.identical);
log('even deposit: ' + even.palette(2)[1].color + ' over ' +
    (even.palette(2)[1].share * 100).toFixed(1) + '% of the sample');
log('dry deposit:  ' + dry.palette(2)[1].color + ' over ' +
    (dry.palette(2)[1].share * 100).toFixed(1) + '% — a drier pencil lays down less graphite');

// The seed makes a mark repeatable, which is what lets a stage be re-run.
log('same seed reproduces exactly: ' + sample(1).diff(sample(1)).identical);

canvas;
```
