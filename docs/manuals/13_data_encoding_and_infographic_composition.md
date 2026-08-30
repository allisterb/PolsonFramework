# Studio Manual 13: Data Encoding & Infographic Composition

> **Credits & Theoretical Foundation**: Distilled from the *Epic Infographics* skill corpus (MIT-licensed; `reference/projects/EpicInfographics-main`, ledger entry 2026-08-29) — its form taxonomy, chart ground rules and composition patterns — restated here in our own terms and bound to the Polson SDK. The quantitative rules it states are the standard ones from the dataviz literature: proportional ink, zero-based length encoding, and shared scales across small multiples.
> **Purpose**: Provides form selection by question, the encoding rules that decide whether a chart tells the truth, and the composition patterns that separate a designed infographic from a dashboard.

---

## 1. Form Follows the Question

> **Implemented by**: nothing — this is the choice you make before any call. The forms themselves are
> built from `Scale.*` for their geometry, `Layout.*` for their placement, and ordinary `ctx` drawing.

Pick the form by the question the data answers, not by habit. Three questions decide it:

1. **What must the reader do** — compare, count, follow, or locate?
2. **How many values?** One value is not a chart. Make it big and label it.
3. **Precision or impression?** Pictograms and waffles give impression; bars and tables give precision.

| The question | Forms that answer it | Notes |
|---|---|---|
| **How big?** (one value) | Big-number callout, progress meter, pictogram count, proportional shapes | More than about four callouts becomes a wall of shouting |
| **Compared to what?** | Horizontal bar, column, ranked list, dot/lollipop, slope, dumbbell | Bars are the workhorse; sort by value unless the order is inherent |
| **What share?** | Waffle grid, donut, single stacked bar, funnel | Waffle is more honest than a donut; a donut wants one dominant share and ≤ 5 segments |
| **Over time?** | Line, area, column series, timeline, sparkline, small multiples | 1–3 series on a line; label the ends directly |
| **How does it work?** | Step flow, cycle, decision tree, quadrant | Keep a decision tree to 2–3 levels in a static image |
| **What overlaps?** | Venn (2–3 sets), mini network | Label regions with values; do not fake proportional areas |

**Mix 3–5 different forms.** One form repeated for every section is a design failure, not a house style.

### Form-level anti-patterns

- A one-bar bar chart, or a two-slice pie → a big-number callout.
- Radar/spider charts → a ranked bar list reads better almost every time.
- 3D anything, exploded pies, gauge dials with needles → never.
- A chart where a sentence and one big number would say it better.

---

## 2. Truthful Geometry

> **Implemented by**: `Scale.linear(...)`, `Scale.band(...)`, `Scale.radiusFor(...)`, `Scale.extent(...)`.
> Three of these rules are encoded in those calls rather than left here as prose, because **each one
> fails silently**: the numbers going in are correct, the picture is wrong, and nothing downstream can
> tell. Whoever drew it has no reason to look.

**Length encodes value, so length must start at zero.** A bar's length *is* its quantity. Truncate the
axis and a 4% difference fills half the panel — the classic distortion, and the easiest to commit by
accident when a library picks the domain for you. Check it:

```js
const y = Scale.linear(0, bounds.max, plot.y2, plot.y);
if (!y.isZeroBased) throw new Error('bars need a zero baseline');
```

Line and dot charts may crop the axis, because they encode *position*, not length. Bars, columns,
areas and anything filled may not.

**Area encodes value, so radius goes as the square root.** The eye reads the ink, not the radius. A
circle sized by value directly shows four times the number as sixteen times the area:

```js
const r = Scale.radiusFor(value, maxValue, 60);   // maxRadius 60
```

**Small multiples share one scale.** Panels drawn to their own extents look comparable and are not —
a lie told by the layout rather than by any single chart. Take the extent once, over everything:

```js
const shared = Scale.extent(seriesA.concat(seriesB, seriesC));
```

**Angle encodes share.** A donut's arc must be proportional to its segment, and the segments must sum
to the whole. If they do not sum to the whole, it is not a part-to-whole form — use bars.

**Never put two scales on one plot.** A dual axis invents a correlation out of two arbitrary ranges.
Draw two charts, or index both series to 100.

---

## 3. Axes, Chrome & Labels

> **Implemented by**: `Scale.ticks(...)` and `Scale.nice(...)` for the numbers, `ctx.fillText(...)` and
> `ctx.measureText(...)` for the labels, `Layout.inset(...)` for the plot's margins.

**A static image has no tooltips.** Every value that matters is directly labelled or readable off a
labelled axis — but label *selectively*: endpoints, extremes, and the hero series. A number glued to
every point is noise, not rigour.

**Ticks are round numbers.** `Scale.nice(min, max)` widens the bounds outward so the first and last
tick sit at the ends of the plot; `scale.ticks(n)` gives steps of 1, 2 or 5 times a power of ten. An
axis reading 0 / 50 / 100 is legible; one reading 0 / 47.5 / 95 is arithmetic showing through.

**Chrome is quiet or absent.** Gridlines hairline and one step off the background, or none at all —
an axis line with direct labels usually beats a grid in an infographic. Bars 24–32px thick with air
between them; lines 2–3px.

**Text never wears the series colour.** Labels and values take the ink colour; the coloured mark
beside them carries the identity. Text set *inside* a filled shape picks white or ink by that fill's
luminance, and only goes inside if it fits with padding — otherwise it goes outside the bar's end.
Never clip a label.

---

## 4. The Canvas Is a Place

> **Implemented by**: `Layout.*` for the zones, and the whole drawing toolkit for what fills them.
> No call chooses a composition — this section is the decision you make first.

The strongest tell of a generated infographic is **rounded cards in a symmetric grid on a flat
background**. That is a dashboard. A designed composition has a dominant object, asymmetry, overlap,
and tension between dense and empty.

Before choosing a pattern, answer in one phrase: **where is the reader standing?** *Looking at an
engineer's drawing. Floating in the launch plume. Leafing through a naturalist's field book.*
Everything on the canvas — ground, texture, chrome, the way the data itself is drawn — must belong to
that place. If the answer is "looking at a well-designed page", start again.

What separates a crafted piece from a flat one:

- **The ground participates** — grid paper, a starfield, paper grain, a horizon — rather than being a
  coloured void.
- **Small deliberate detail**: registration marks, ticks, particles, stamps, flight paths. Sparse
  reads as machine-made; layered small detail reads as crafted.
- **Something travels** — an arc, a dashed path, a narrowing beam — connecting the facts in sequence.
- **Diegetic data**: charts drawn as instruments *of* the scene — a dial on the sheet, a constellation,
  a measuring cup — rather than panels floating over it.

### Pick one named pattern, and say which

```
1. Big Object      One drawn object at 40–70% of the canvas; data lives ON and AROUND it,
                   labels pinned to its parts. Best for single-subject topics.

2. Bleed           The hero element is cropped by the canvas edge — a window onto something
                   bigger — with generous space opposite. Best for making one value feel enormous.

3. Overlap stack   Elements sit ON each other, not beside. 2–4 meaningful overlaps, each with a
                   depth cue. Best for poster energy.

4. Diagonal drive  One diagonal carries the composition; everything else stays calm. Two diagonals
                   is chaos. Best for momentum subjects.

5. Editorial       A hard asymmetric split — 1:2 or 1:3, never 1:1. One column is a single towering
   spread          element and holds the largest type. Best for sober subjects that must not be dull.

6. Specimen sheet  Many small labelled items in a strict grid; the rigour is the design. Needs ≥ 8
                   items, and one may break the grid. Best for taxonomies and rankings.
```

A card grid is permitted as a **sub-zone** — one zone, no more than a third of the canvas — never as
the whole layout.

---

## 5. Tension Rules

> **Implemented by**: `Layout.rows(...)` / `Layout.columns(...)` with **weighted** divisions —
> `[62, 38]`, not `2`. Equal division is what produces the wallpaper this section warns about.

Apply to every pattern:

1. **One dense zone, one empty zone.** Somewhere the content packs tight; somewhere the ground
   breathes for at least ~15% of the canvas. Even spacing everywhere reads as wallpaper.
2. **Three sizes minimum.** The largest element at least 8× the body text. If everything sits between
   16px and 40px, the hierarchy is dead — see Manual 11 §4 for building the ladder from one ratio.
3. **Something crosses a boundary.** At least one element breaks its container, a section line, or the
   canvas edge. Nothing crossing reads as a template.
4. **The ground is never uniform.** A large tinted shape, a texture, a ghosted giant numeral, a
   gradient of the paper tone. Subtle, but not flat white.
5. **Rotate at most one system of elements.** Everything slightly rotated is chaos; nothing rotated is
   static. A strict grid style is the exception — its tension comes from scale instead.

---

## 6. Litmus Tests Before Rendering

Run these against the render, not the plan:

- **Could this layout hold a SaaS dashboard's data without looking odd?** Then it is a dashboard.
  Recompose.
- **Cover the text — is the topic still recognisable from the shapes alone?** If not, there is no
  visual identity yet.
- **Are all four corners doing the same amount of work?** Then there is no focal point.
- **Is any bar's baseline non-zero, or any circle sized by radius?** Then the picture is lying even
  though the data is right (§2).

---

## 7. Symbol → SDK Parameter Map

| Concept | SDK call or field | Notes |
| --- | --- | --- |
| Value → pixel | `Scale.linear(d0, d1, r0, r1)` | Range may run backwards; that is a vertical axis. |
| Bar length | `scale.extent(baseline, value)` | Always positive, whichever way the range runs. |
| Zero-baseline check | `scale.isZeroBased` | Assert it for bars, columns, areas. |
| Category positions | `Scale.band(count, r0, r1, padding)` | `band.bandwidth` is the drawn width; `band.center(i)` is the label anchor. |
| Round axis numbers | `Scale.nice(min, max)`, `scale.ticks(n)` | `nice` first, then build the scale from its bounds. |
| Shared scale | `Scale.extent(allValues)` | Once, over every series. Non-negotiable for small multiples. |
| Area encoding | `Scale.radiusFor(value, maxValue, maxRadius)` | Square-rooted, so area carries the value. |
| Zones and margins | `Layout.rows/columns(rect, weights, gap)`, `Layout.inset(...)` | Weighted, not equal — see §5. |
| Measured text blocks | `ctx.measureWrappedText(text, maxWidth)` | Height before placement, so captions can stack. |
| Design language | `Css.fromCss(...)`, `sheet.tokens()`, `sheet.rule('.h1')` | Palette and type from a stylesheet; see Manual 11 §3. |

---

## 8. Constructing It: A Runnable Panel

Three of §2's rules in one panel: a zero baseline, an area-encoded circle set, and a shared scale
across two series. Every coordinate comes from `Scale` or `Layout`; none is written by hand.

```javascript
// Encoding demo: zero-based columns, √-scaled circles, one shared scale.
const canvas = createCanvas(900, 460);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f7f4ee';
ctx.fillRect(0, 0, 900, 460);

const page = Layout.inset(Layout.rect(0, 0, 900, 460), 34);
// §5 — weighted, not equal: one dense zone and one that breathes.
const [left, right] = Layout.columns(page, [62, 38], 34);

// §2 — one extent over BOTH series, so the two panels are comparable.
const north = [38, 61, 47, 92, 74];
const south = [22, 35, 29, 58, 44];
const shared = Scale.extent(north.concat(south));
const bounds = Scale.nice(0, shared.max);
log('shared extent ' + shared.min + '–' + shared.max + ', axis to ' + bounds.max);

const [plotN, plotS] = Layout.rows(left, 2, 22);
const labels = ['Mar', 'Apr', 'May', 'Jun', 'Jul'];

const drawSeries = (rect, values, fill, title) => {
    const y = Scale.linear(bounds.min, bounds.max, rect.y2 - 18, rect.y + 16);
    const x = Scale.band(values.length, rect.x, rect.x2, 0.34);
    if (!y.isZeroBased) throw new Error('columns need a zero baseline');

    ctx.font = '600 12px sans-serif';
    ctx.fillStyle = '#6b7684';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'top';
    ctx.fillText(title, rect.x, rect.y);

    for (let i = 0; i < values.length; i++) {
        ctx.fillStyle = fill;
        ctx.fillRect(x.map(i), y.map(values[i]), x.bandwidth, y.extent(bounds.min, values[i]));
        ctx.fillStyle = '#8a94a0';
        ctx.font = '400 10px sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(labels[i], x.center(i), rect.y2 - 14);
        ctx.textAlign = 'left';
    }
};

drawSeries(plotN, north, '#1f6f8b', 'NORTH');
drawSeries(plotS, south, '#c96a2e', 'SOUTH');

// §2 — area encodes value: four times the number is twice the radius.
ctx.textAlign = 'center';
const circles = [{ v: 25, t: '25' }, { v: 50, t: '50' }, { v: 100, t: '100' }];
const slot = Layout.rows(right, [22, 78], 10)[1];
const band = Scale.band(circles.length, slot.x, slot.x2, 0.1);

ctx.font = '600 12px sans-serif';
ctx.fillStyle = '#6b7684';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
// Plain ASCII in *drawn* text: the maths glyphs in this manual's prose (∝, √) are not in every
// installed face, and a missing glyph renders as a tofu box rather than failing loudly.
ctx.fillText('AREA SCALES WITH VALUE', right.x, right.y);

const biggest = Scale.radiusFor(100, 100, band.bandwidth / 2);
for (let i = 0; i < circles.length; i++) {
    const r = Scale.radiusFor(circles[i].v, 100, band.bandwidth / 2);
    const cx = band.center(i);
    const cy = slot.cy;
    ctx.fillStyle = 'rgba(31, 111, 139, 0.85)';
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fill();

    // One baseline for every label, set by the largest circle — labels that follow each radius
    // read as a third encoding and compete with the one being demonstrated.
    ctx.fillStyle = '#1c2733';
    ctx.font = '400 11px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    ctx.fillText(circles[i].t, cx, cy + biggest + 10);
}

canvas;
```
