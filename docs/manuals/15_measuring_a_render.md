# Studio Manual 15: Measuring a Render

> **Credits & Theoretical Foundation**: The case for measuring rather than judging is taken from two papers in `reference/papers/` (ledger entries 2026-08-31). Deshpande, Trajkova, Knowlton & Magerko, *Observable Creative Sense-Making*, C&C '23 (**CC BY 4.0**, adapted here with attribution) supplies the **newness** dimension and the applicability clause that CSM holds for collaborations *"that have identifiable behavior markers for the corresponding cognitive states"*. Davis, Hsiao, Singh, Lin & Magerko, *Creative Sense-Making*, C&C '17 (ACM, cited not quoted) supplies the coding cost this medium avoids. The measurement primitives are documented at `polson://sdk/core/Skia`.
> **Purpose**: How to check a render against what you intended by measuring it, rather than by looking at it — reading pixels back, comparing two frames, profiling structure, and reading tone — and what each measurement can and cannot tell you.

---

## 1. Why Measure at All

> **Implemented by**: `bitmap.getPixel(...)`, `bitmap.diff(...)`, `bitmap.rowProfile(...)`, `bitmap.palette(...)`. Signatures at `polson://sdk/core/Skia`.

Looking at a render tells you a layer is "too bright". It does not tell you **which** of three quite different things went wrong:

1. the fill is the wrong colour;
2. the fill is right but the alpha is not;
3. nothing was drawn at all, and you are looking at the layer beneath.

All three look identical. One `getPixel` separates them. That gap — between a perception and a fact — is what this manual is about.

**This is not a substitute for looking.** Perception is how you notice that a composition is inert or a face is wrong; measurement is how you find out whether the thing you *think* you fixed actually changed. Use both, in that order: look to form a hypothesis, measure to test it.

### The incident

A painting run reached its critique stage, correctly diagnosed a value-compression fault by eye, applied `Skia.ColorFilter.highContrast` — and stopped. It never read the tonal distribution back, so it could not say whether the correction had moved the values or merely restated the problem more emphatically. `bitmap.palette`, `bitmap.diff` and `bitmap.rowProfile` were in no manual at the time. That is why this one exists.

### What the papers actually say

OCSM replaces creative sense-making's single scale with three observable dimensions, each coded `0`–`3`. The middle one, **newness**, "assesses the variance of movements explored by the individual": state `0` is repeating a previous movement, `1` slightly different, `2` similar but with a significant difference, `3` a new movement. In the study that produced it, an analyst assigns those states by watching video.

The earlier CSM methods paper measured what that costs: roughly **4 analyst-minutes per minute of video** — and reports that as *efficient*, against event-based coding schemes running at 30 to 1.

Here, the same question — *how different is this turn from the last?* — is `previous.diff(current)`, and it costs one call. That is the concrete meaning of OCSM's applicability clause: CSM remains valid where there are identifiable behaviour markers for the states being attributed, and a render is the strongest instance of that clause, because the marker is machine-recorded rather than inferred by a coder from video.

> [!IMPORTANT]
> Be precise about what the analogy licenses. `diff` returns a **similarity number**; OCSM's newness is a **0–3 state assigned by a person**. Mapping one onto the other is your step, not the paper's, and a threshold you pick is a judgement you should record in a `Stage.note` rather than present as a measurement. What the medium gives free is the *observation*. The coding is still coding.

### And it has to be native

A 900 × 700 frame is 630,000 pixels. Reading them in a loop is 630,000 iterations *before* any arithmetic, against a sandbox statement cap that ends the whole script when it is reached — and it is reached silently, with no partial result. Every call in this manual runs natively and costs one statement whatever the resolution. **Never write a per-pixel loop over a full frame.**

---

## 2. Reading a Render Back

> **Implemented by**: `Skia.Image.load(...)`, `Skia.Image.fromDataUrl(...)`, `Skia.Image.fromBytes(...)`, `Skia.Bitmap.create(...)`, `canvas.bitmap`, `canvas.toBitmap()`, `ctx.getImageData(...)`.

Everything here operates on a `SkiaBitmapWrapper`. There are four ways to get one.

```js
const previous = Skia.Image.load('artifacts/03_blocking.webp');  // from disk — the previous stage
const live = canvas.bitmap;                                      // the canvas's own backing bitmap
const snapshot = canvas.toBitmap();                              // an independent copy of it
const decoded = Skia.Image.fromDataUrl(Session.oakUri);          // from a data URI
```

`Skia.Image.load` takes a path **relative to the project directory, exactly as `outFile` does**, so `Skia.Image.load('artifacts/03_blocking.webp')` reads back what `outFile: 'artifacts/03_blocking.webp'` wrote. A path resolving outside the project is refused.

> [!IMPORTANT]
> `canvas.bitmap` is **live** and `canvas.toBitmap()` is a **copy**. Writing through the live one changes the canvas; the copy is independent, as is `bitmap.clone()`. To hold a "before" while you keep drawing, you must take a copy — a live handle kept across further drawing is not a before, it is the after.

`ctx.getImageData(x, y, w, h)` gives an `ImageData` whose `data` is a `Uint8ClampedArray` of flat RGBA values: `[r0, g0, b0, a0, r1, …]`, length `w × h × 4`, each value `0`–`255`. It is the buffer itself, so writes to it reach the canvas through `ctx.putImageData(...)`. Use it for a small region you genuinely need numerically. Over a whole frame, use the calls below instead.

---

## 3. The Point Check — `getPixel`

`bitmap.getPixel(x, y)` returns an ordinary string in the form `#RRGGBBAA`. Two facts decide whether your comparison works:

- **Hex digits come back uppercase.**
- **Alpha is the last pair**, not the first.

```js
bitmap.getPixel(30, 40)                    // '#1F6F8BFF'
bitmap.getPixel(30, 40) === '#1F6F8BFF'    // true — this is how you assert a colour
```

So the three-way ambiguity from §1 resolves like this:

| What you read back | What it means |
| :--- | :--- |
| `#1F6F8BFF` | Drawn, opaque, the colour you asked for |
| `#1F6F8B80` | Drawn in the right colour at half alpha — a `globalAlpha` or `fill-opacity` you forgot |
| `#FAF8F4FF` | The background. Nothing was drawn here at all |
| `#00000000` | Transparent — the ground was never filled either |

`bitmap.setPixel(x, y, color)` writes one back, which is mostly useful for marking a measurement onto a debug frame.

**Pick the sample point deliberately.** A pixel on an antialiased edge is a blend of both sides and tells you nothing. Sample well inside a region — `bounds.cx, bounds.cy` from a `diff`, or the centre of a rectangle you placed yourself.

---

## 4. The Whole-Frame Check — `diff`

`a.diff(b, options?)` compares two bitmaps and returns:

```js
{ width, height, totalPixels, differingPixels, similarity, meanDelta, maxDelta, identical, bounds }
```

A real comparison of a 200 × 120 frame against the same frame with one rectangle shifted 8px:

```
differingPixels = 960   similarity = 0.96   meanDelta = 8.76   maxDelta = 219   identical = false
bounds = { x: 20, y: 30, width: 98, height: 60, x2: 118, y2: 90, cx: 69, cy: 60 }
```

**`bounds` is the part a score cannot give you.** `similarity = 0.96` says "almost the same" — which is exactly what you would expect both from a small correct change and from a change that failed to happen. `bounds` says *where*: a rectangle enclosing every differing pixel, or **`null`** when the two match. A stage that claims to have repainted the sky and comes back with `bounds` sitting over the foreground has not done what it says.

`options.tolerance` defaults to **8** per channel, because two renders of the same scene differ by a point or two along every antialiased edge and a tolerance of 0 reports thousands of meaningless differences. `options.ignoreAlpha` compares colour only.

`a.diffMap(b, options?)` returns the same comparison **as an image** — differing pixels marked, matching ones dimmed — which is the one to write to `outFile` when you want a person to see what moved.

> [!WARNING]
> **Differently sized bitmaps throw, and the throw cannot be caught.** `try { a.diff(b) } catch (e) { … }` does not run: the exception ends the whole script, and everything after it is lost. This is deliberate — a size mismatch means you are comparing the wrong two images, and a similarity score over a partial overlap would look like an answer. Check first:
>
> ```js
> if (a.width !== b.width || a.height !== b.height) b = b.resize(a.width, a.height);
> ```
>
> The message is good when it fires — it names both sizes and tells you to resize — but it fires by ending the run, so the guard is worth the two lines.

---

## 5. The Structural Check — `rowProfile`

`bitmap.rowProfile(color, options?)` reports where a colour class starts and ends on each row:

```js
[ { index: 30, start: 20, end: 109, extent: 90, count: 90 }, … ]
```

`index` is the row's `y`; `start` and `end` are the first and last matching pixel on it; `extent` is `end - start`; `count` is how many matched. **Rows matching nothing are omitted**, so the array's length is the number of rows the colour appears on — a 60px-tall rectangle yields 60 entries. `options.axis: 'column'` profiles the other way, and then `index` is `x`. `options.tolerance` widens the colour class; `options.minCount` drops rows with too few matches.

This is what makes a *structural* comparison affordable. It reduces an image to a few hundred rows, so comparing two images becomes a loop over rows rather than over pixels — about 1,400 iterations for two 700-row frames, against nearly a million for the pixel-level equivalent.

```js
const want = reference.rowProfile('#1f6f8b');
const got = render.rowProfile('#1f6f8b');
for (let i = 0; i < Math.min(want.length, got.length); i++) {
    const dx = Math.abs(want[i].start - got[i].start);
    if (dx > 8) log('row ' + want[i].index + ': left edge off by ' + dx + 'px');
}
```

It answers questions a similarity score cannot: is the silhouette the right width at the shoulders? does the horizon run straight? is the left margin consistent down the column? Those are shape questions, and `rowProfile` is the shape instrument.

---

## 6. The Tonal Check — `palette` and Value

`bitmap.palette(count?, options?)` returns the dominant colours as `{ color, share, pixels }`, ordered by share, with fully transparent pixels ignored. Colours are **bucketed before counting**, so antialiasing collapses into the flat colour it surrounds instead of producing thousands of near-duplicates; `options.buckets` controls how coarsely.

```
{ color: '#FAF8F4', share: 0.775, pixels: 18600 }
{ color: '#1F6F8B', share: 0.225, pixels:  5400 }
```

`share` is a fraction of the opaque pixels, so it reads directly as a proportion — which is what makes it the check on Manual 09's 70-20-10 law and on any stated colour hierarchy. A design that claims an accent is an accent and comes back with `share = 0.4` has a dominant colour it did not intend.

**Asking for more colours than exist returns fewer.** `palette(4)` on a two-colour image returns two entries, not four padded ones.

### Reading value rather than colour

Value hierarchy is a question about *lightness*, and colour hides it — a saturated red and a mid grey can carry the same value and look nothing alike. Collapse to value first, then read the palette:

```js
// Rec. 601 luminance: every channel becomes the same weighted grey.
const luminance = Skia.ColorFilter.colorMatrix([
    0.299, 0.587, 0.114, 0, 0,
    0.299, 0.587, 0.114, 0, 0,
    0.299, 0.587, 0.114, 0, 0,
    0,     0,     0,     1, 0]);
const bands = render.applyColorFilter(luminance).palette(6);
```

Now `bands` is the notan: how much of the frame sits in each value band, and whether the picture has the dark, mid and light masses it was supposed to. A value-compressed painting shows as bands clustered together — which is a **measurement**, not an impression, and it can be re-measured after the correction to prove the correction worked.

`Skia.ColorFilter.highContrast(grayscale, invertStyle, contrast)` is the quicker way to *see* the same thing, and `Skia.ColorFilter.blend(color, mode)` tints. Use them to look; use `palette` over the luminance version to know.

> [!IMPORTANT]
> `highContrast` **changes the image so you can see the fault**. It does not tell you whether the fault is still there afterwards. That distinction is the whole of the incident in §1: applying a filter is a way of looking, and a critique that ends with a filter has looked twice and measured nothing. Read the bands before and after, and compare them.

---

## 7. Cropping, Scaling and Copies

- `bitmap.extractSubset(x, y, w, h)` — a cropped copy. Measure one region without the rest of the frame diluting the numbers: a `palette` over a whole page is dominated by the background, a `palette` over the subject's rectangle is not.
- `bitmap.resize(w, h, quality?)` — resample, `'linear'` or `'nearest'`. Needed to make `diff` legal across sizes, and useful for a legibility check: shrink a mark to 16px and read back whether its counter survived.
- `bitmap.clone()` — an independent copy. Take one before a destructive step.
- `bitmap.applyFilter(imageFilter)` / `bitmap.applyColorFilter(colorFilter)` — return **new** bitmaps; the original is untouched.
- `bitmap.toPngBytes()`, `bitmap.toImageBytes(format, quality)`, `bitmap.toDataUri(format, quality)` — encode, for stashing a "before" in `Session` across executions.

---

## 8. A Protocol

Measurement is worth something only when the expectation is written down *before* the render. Four steps:

1. **State the expectation as a number**, with `Stage.expect(...)` — "the accent should be under 15% of the frame", "the horizon should sit within 4px of y=380". Before the render, so you can be wrong about it.
2. **Keep the before.** `const before = canvas.toBitmap()`, or load the previous stage's artifact from disk.
3. **Measure after.** `diff` for *did anything change and where*, `rowProfile` for *is the shape right*, `palette` for *is the tonal balance right*, `getPixel` for *is this specific thing what I said it was*.
4. **Settle every claim with `Stage.check(...)`.** It takes the claim, the verdict and the measured value, returns the verdict so you can branch on it, and puts all three in the record.
5. **Fix what failed, then check it again — and record that check too.** The failing check identifies the fault. The *passing* check afterwards is the only evidence the fix worked.

```js
Stage.expect('the accent should stay under 15% of the frame');
// …render, then measure…
const accent = render.palette(4)[1].share;
if (!Stage.check('accent under 15%', accent < 0.15, 'measured ' + (accent * 100).toFixed(1) + '%')) {
    // the check is the thing that fails, not the run — now fix it
}
```

> [!IMPORTANT]
> **The measurements record themselves; the expectation is the part only you can supply.** `diff`,
> `palette` and `rowProfile` each write an `observe` event carrying what they found, so the record
> holds the outcome whether or not you mention it. What it cannot infer is what you were aiming at —
> and without that, a run that measured and was satisfied looks identical to one that measured, found
> the value wrong, and redrew four times.
>
> A critique stage that ends without a check has not verified anything, however carefully it looked.
> A stage that states four checks and passes three has said exactly where it stands.

> [!WARNING]
> **Two ways to do half of this**, and a live run produced both.
>
> **An `expect` with no `check` is worse than no `expect` at all** — it reads as verification and is
> not. A critique stated three claims (low-key dominance, accent under 15%, palette gamut) and settled
> one; the record then holds two predictions whose outcome nobody knows, including the run that made
> them. If you state it, settle it, even when the answer is uninteresting.
>
> **A failing check with no passing check after the fix leaves the correction unproven.** The same run
> measured 57.1% against a stated 60%, marked it failed, made a refinement pass — and stopped. The
> record shows what was wrong and not that anything was put right, which is one step away from the
> failure this manual exists for. **Re-run the check after the fix**, and let the record carry the
> pass. `checksFailed == checks` in the run report means exactly this happened.

---

## 9. Traps

| What happens | Why | What to do |
| :--- | :--- | :--- |
| `try { a.diff(b) }` does not catch a size mismatch, and the script ends | The throw is not a catchable JS error | Compare `width`/`height` and `resize` first |
| `getPixel(x, y) === '#1f6f8bff'` is false for the right colour | Hex comes back **uppercase** | Compare against uppercase, or lowercase both sides |
| An alpha check reads the wrong pair | The format is `#RRGGBBAA` — alpha **last** | `px.substring(7)` is the alpha |
| A sampled pixel is neither colour | It is on an antialiased edge | Sample well inside the region, e.g. `bounds.cx, bounds.cy` |
| `palette(8)` returns 3 entries | There were only 3 buckets with pixels | Not an error; the count is a maximum |
| `similarity` is high and nothing looks changed | A small correct change and a change that never happened score the same | Read `bounds` — `null` means genuinely identical |
| A "before" bitmap shows the after | `canvas.bitmap` is live | `canvas.toBitmap()` or `bitmap.clone()` |
| The script dies partway with no error you can see | A per-pixel loop hit the statement cap | Use the native calls; never loop a full frame |

---

## 10. Symbol → SDK Map

| The question | The call | What it answers |
| :--- | :--- | :--- |
| Is this exact spot what I said? | `bitmap.getPixel(x, y)` | Colour **and** alpha, as `#RRGGBBAA` |
| Did anything change, and where? | `bitmap.diff(other, opts)` | `similarity`, `differingPixels`, and `bounds` |
| Show me what changed | `bitmap.diffMap(other, opts)` | The same, as an image |
| Is the shape right? | `bitmap.rowProfile(color, opts)` | Per-row `start` / `end` / `extent` / `count` |
| Is the tonal balance right? | `bitmap.palette(count, opts)` | `color` / `share` / `pixels`, transparent ignored |
| Is the *value* right? | `applyColorFilter(colorMatrix(...))` then `palette` | The notan, as numbers |
| Just let me see the values | `Skia.ColorFilter.highContrast(...)` | A look, not a measurement |
| Read back a previous stage | `Skia.Image.load('artifacts/…')` | Project-relative, like `outFile` |
| Hold a "before" | `canvas.toBitmap()`, `bitmap.clone()` | Independent copies |
| Measure one region only | `bitmap.extractSubset(...)` | Crop before measuring |
| Make two frames comparable | `bitmap.resize(w, h)` | Required before `diff` |
| Raw numbers for a small area | `ctx.getImageData(...)`, `imageData.data` | Flat RGBA, `w × h × 4` |

---

## 11. Measuring It: A Runnable Verification Plate

Two versions of the same scene, and every question from §10 asked of them.

```javascript
// A verification plate: draw twice, then measure the difference rather than describe it.
function scene(shift, accent) {
    const c = createCanvas(320, 200);
    const x = c.getContext('2d');
    x.fillStyle = '#faf8f4'; x.fillRect(0, 0, 320, 200);
    x.fillStyle = '#2d3a45'; x.fillRect(0, 150, 320, 50);      // the dark mass
    x.fillStyle = accent;    x.fillRect(40 + shift, 50, 120, 80); // the subject
    return c;
}

// §2 — toBitmap() gives an independent copy, so "before" stays "before".
const before = scene(0, '#1f6f8b').toBitmap();
const after = scene(14, '#1f6f8b').toBitmap();

// §3 — the point check. Uppercase hex, alpha last.
log('subject pixel = ' + before.getPixel(100, 90) + '  (opaque? ' +
    (before.getPixel(100, 90).substring(7) === 'FF') + ')');
log('same point after the move = ' + after.getPixel(100, 90));

// §4 — guard the sizes before diffing: the mismatch throw cannot be caught.
if (before.width !== after.width || before.height !== after.height) {
    exit('frames are not comparable');
}
const d = before.diff(after);
log('similarity = ' + d.similarity.toFixed(3) + ', differing = ' + d.differingPixels +
    ' of ' + d.totalPixels + ', identical = ' + d.identical);

// bounds is the part the score cannot tell you: where the change actually landed.
log('changed region = ' + d.bounds.width + 'x' + d.bounds.height +
    ' at ' + d.bounds.x + ',' + d.bounds.y + ' (centre ' + d.bounds.cx + ',' + d.bounds.cy + ')');
log('the dark mass was untouched: ' + (d.bounds.y2 <= 150));

// A frame compared with itself: identical, and bounds is null rather than empty.
const self = before.diff(before.clone());
log('self-diff identical = ' + self.identical + ', bounds = ' + self.bounds);

// §5 — structural: where the subject's left edge sits on each row it occupies.
const wasProfile = before.rowProfile('#1f6f8b');
const isProfile = after.rowProfile('#1f6f8b');
log('subject spans ' + wasProfile.length + ' rows, left edge x=' + wasProfile[0].start +
    ' -> ' + isProfile[0].start + ', width ' + wasProfile[0].extent + ' -> ' + isProfile[0].extent);
log('width preserved by the move: ' + (wasProfile[0].extent === isProfile[0].extent));

// §6 — tonal: share reads directly as a proportion of the frame.
for (const band of before.palette(4)) {
    log('  ' + band.color + '  ' + (band.share * 100).toFixed(1) + '%  (' + band.pixels + 'px)');
}

// §6 — value rather than colour. Collapse to luminance, then read the notan.
const luminance = Skia.ColorFilter.colorMatrix([
    0.299, 0.587, 0.114, 0, 0,
    0.299, 0.587, 0.114, 0, 0,
    0.299, 0.587, 0.114, 0, 0,
    0,     0,     0,     1, 0]);
const values = before.applyColorFilter(luminance);
// Ordered by share, not by lightness — the biggest mass first, whatever value it is.
log('value bands by share:');
for (const band of values.palette(4)) {
    log('  ' + band.color + '  ' + (band.share * 100).toFixed(1) + '%');
}

// §7 — measure one region without the background diluting it.
const subject = before.extractSubset(40, 50, 120, 80);
log('subject crop ' + subject.width + 'x' + subject.height +
    ', dominant ' + subject.palette(1)[0].color + ' at ' +
    (subject.palette(1)[0].share * 100).toFixed(0) + '%');

// §4 — the difference as an image, which is what a person should be shown.
const board = createCanvas(320, 200);
board.getContext('2d').drawImage(before.diffMap(after), 0, 0);
board;
```
