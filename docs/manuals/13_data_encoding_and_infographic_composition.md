# Studio Manual 13: Data Encoding & Infographic Composition

> **Credits & Theoretical Foundation**: Four sources, each named where it is used. **Edward R. Tufte**, *The Visual Display of Quantitative Information* (Graphics Press 1983) for graphical integrity, the lie factor, data-ink and data density; *Envisioning Information* (1990) for micro/macro readings and layering; *Visual Explanations* (1997) for the smallest effective difference. **William S. Cleveland & Robert McGill**, *Graphical Perception: Theory, Experimentation, and Application to the Development of Graphical Methods*, JASA 79:531–554 (1984), with its companion in Science 229:828–833 (1985), for the measured accuracy of perceptual judgments — the reason §1 orders forms the way it does. The form taxonomy and composition patterns are distilled from the *Epic Infographics* skill corpus (MIT-licensed). All are restated in our own words and bound to the Polson SDK; ledger entries and the differing licence terms are in `reference/README.md`.
> **Purpose**: How to choose a form because of how accurately a reader can decode it, prove the picture is not lying, decide what ink to remove, and compose the result — with the checks that make each of those verifiable rather than asserted.

---

> [!IMPORTANT]
> **This manual is more prescriptive than the drawing manuals, deliberately.** Those carry two schools
> where two exist, because a figure can be constructed by Loomis or by Reilly and neither is wrong.
> Most of what follows is not like that. **§2 is about truth** — a bar chart with a truncated axis
> asserts something the data does not, and that is an error, not a style. **§1's ordering is an
> experimental result.** The cost of error is also asymmetric: a mediocre drawing disappoints
> somebody, whereas a chart with a false baseline is a claim a client may publish under their own
> name.
>
> Where the sources hedge, this manual hedges with them and says so — the perceptual ordering is
> partly conjectural (§1), and data-ink is genuinely contested (§3). Everywhere else, the rule is
> stated plainly and, where possible, **stated as a check you can run** rather than as advice. A rule
> that ships with an assertion does not need firm language.

---

## 1. Form Follows the Question

> **Implemented by**: nothing — *choosing* is the decision you make before any call. Two of the forms
> below are then one call: `Chart.createColumnChart(...)` and `Chart.createBarChart(...)`. The rest are
> still built from `Scale.*` for their geometry, `Layout.*` for their placement, and ordinary `ctx`
> drawing.

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

### 1a. Why that table is ordered as it is

The table above is not taste. Cleveland and McGill asked people to read values off graphs and
**measured how accurately they did it**, then ordered the elementary judgments a chart can ask for,
most accurate first:

1. **Position along a common scale** — points on one shared axis
2. **Position along non-aligned scales** — small multiples, each with its own axis
3. **Length, direction, angle**
4. **Area**
5. **Volume, curvature**
6. **Shading, colour saturation**

**The design rule that follows is the one they state: build the graph out of judgments as high in
that list as you can get.** A dot on a shared axis beats a bar's length, which beats a pie's angle,
which beats a bubble's area, which beats a choropleth's shading. When you reach for a lower rank, you
are spending accuracy — sometimes worth it for density or for the picture, but spend it knowingly.

> [!NOTE]
> **Two honesties about this list, both from the authors.**
>
> It is **six ranks, not ten items**: length, direction and angle are *tied*; so are volume and
> curvature; so are shading and saturation. Writing it as a flat ten-step ladder adds precision the
> source does not claim.
>
> And it is **part measured, part reasoned**. Position and length were tested and won. Of the rest
> the authors write that *"aspects of the ordering are partly conjectural in that we have no
> controlled experimentation to support them"* — their own example being that position on a common
> scale is *stipulated* to beat position on non-aligned scales, while their third experiment found
> the two nearly identical. Use the top of the list as fact and the bottom as informed argument.

### 1b. The forms they recommend

Their conclusion is blunter than the ranking is usually reported to be: bar charts, divided bar
charts, pie charts and shaded maps need *"radical surgery"*, and they offer replacements — the **dot
chart**, the **dot chart with grouping**, and the **framed-rectangle chart** (which replaces a
choropleth's shading, rank 6, with position in a frame, rank 1–2).

**`Chart.createDotChart(...)` is the first of the three, and where a bar chart would do, it is the
better default.** Same data, same shape of call, and the reader's task drops from rank 3 to rank 1:

```js
const chart = Chart.createDotChart(panel, rows, { sort: 'desc' });
```

Sort it. A form whose advantage is comparison gets most of that advantage from ordering the values.

**Its axis need not start at zero, and that is the point rather than a concession.** A bar's length
*is* its quantity, so it claims a ratio and must start at zero (§2). A dot claims only a difference,
and a linear mapping preserves the ratios of differences wherever the axis begins — so a dot chart
can crop to the data's own range and show a spread that a zero-based bar chart flattens into five
bars of nearly equal length. The model reports `lieFactor: 1` and `isZeroBased: false` together, and
both are correct.

**`Chart.createGroupedDotChart(...)` is the second**, and it is the one to reach for when the rows
have structure — regions, product lines, cohorts. Grouping buys a second comparison (within a group,
and between groups) without spending any accuracy, because every dot still reads against **one**
axis:

```js
const chart = Chart.createGroupedDotChart(panel, rows, { sort: 'desc' });   // rows carry a `group`
```

> [!IMPORTANT]
> **Prefer this to a panel per group.** The obvious alternative — small multiples, one panel per
> region — gives each panel its own axis unless you remember to force a shared one, and that is
> precisely the failure §2 names: panels that look comparable and are not. A grouped dot chart cannot
> make that mistake, because the scale is computed across every group at once.

**`Chart.createFramedRectangleChart(...)` is the third, and it replaces the shaded map.** A choropleth
asks the reader to judge *shading* — rank 6, the very bottom. Put an identical small frame at each
location and fill it to the value, and the task becomes reading a level inside a box:

```js
const chart = Chart.createFramedRectangleChart(panel, rows);   // rows carry x, y, value
```

> [!IMPORTANT]
> **Draw the frames.** Without them these are "located bars" and the task falls back to perceiving
> length, rank 3; the frames are what buy the step up to rank 2. `createChartGeometry(...)` returns
> them separately from the fills so you can stroke them lightly and fill the data solidly.

It also fixes two faults of a shaded map that are nothing to do with the hierarchy, and both are worth
knowing because they are invisible until named. **Shading a region makes its total ink the value times
its area**, so on a US map Texas is imposing and Rhode Island is hard to see whatever the numbers say.
And **contiguous shaded regions merge into clusters** the eye reads as structure whether or not any
exists. Identical frames can do neither.

**All three forms now exist.** Where you use a lower-ranked form anyway — and §1's table still lists
plenty — the discipline is unchanged: use it knowingly, and say so in a `Stage.note`.

### 1c. Two more forms, and why they are where they are

**`Chart.createCallout(...)` — one number, made big.** It sits *above* the whole hierarchy, because a
reader **reads** a numeral rather than judging it: no decoding, no error. That is the argument behind
the first two anti-patterns below — a one-bar bar chart and a two-slice pie both take a number the
reader could simply have read and convert it into a judgment. `compact: true` handles the formatting
that makes a big number legible (`1234567` → `1.2M`).

**`Chart.createWaffle(...)` — a share, as countable cells.** Ten by ten with each cell worth a percent
is why the manual prefers it to a donut: a donut's angle is rank 3 and cannot be counted, while a
waffle **can** be. But the model reports itself as **area, rank 4**, which is deliberately the
pessimistic reading — a reader who counts gets an exact answer and you cannot assume anyone will.
Claiming counting's exactness for a graphic most people eyeball would be the flattering assumption
rather than the safe one.

### Form-level anti-patterns

- A one-bar bar chart, or a two-slice pie → a big-number callout (§1c).
- Radar/spider charts → a ranked bar list reads better almost every time.
- 3D anything, exploded pies, gauge dials with needles → never.
- A chart where a sentence and one big number would say it better.

> [!TIP]
> **When a brief demands a pie anyway** — and in brand work it sometimes will — the encoding rule
> does not change, so make the angle do as little work as possible: order segments by size from
> twelve o'clock, keep them to five or fewer, and **label every segment with its value**, which
> converts the reader's task from judging angles into reading numbers. Then record the trade in a
> `Stage.note`. Refusing outright leaves the agent with no move; conceding silently loses the reason.

---

## 2. Graphical Integrity

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
a lie told by the layout rather than by any single chart, and **the one rule here that no individual
panel can detect**, because each is correct on its own terms. `Chart.createSmallMultiples(...)` takes
the extent across every series before it builds a single panel, so there is no argument to forget:

```js
const grid = Chart.createSmallMultiples(page, [
    { label: 'North', data: north }, { label: 'South', data: south }
], { columns: 2 });
```

Assembling the panels yourself is still fine — take the extent once, over everything, and pass it in:

```js
const shared = Scale.extent(seriesA.concat(seriesB, seriesC));
```

**Angle encodes share.** A donut's arc must be proportional to its segment, and the segments must sum
to the whole. If they do not sum to the whole, it is not a part-to-whole form — use bars.

**Never put two scales on one plot.** A dual axis invents a correlation out of two arbitrary ranges.
Draw two charts, or index both series to 100.

### 2a. The lie factor: the measurement that proves the rest

Tufte gives distortion a number rather than a name:

```
              size of effect shown in the graphic
lie factor = ────────────────────────────────────
                 size of effect in the data
```

A lie factor of **1** means the ink is telling the truth. He treats anything outside **0.95–1.05** as
substantial distortion — beyond plotting slop — and notes that distortions in the wild almost always
*overstate*, with factors of two to five common and one published example reaching **14.8**.

**The point for us is that it is computable, so it can be asserted rather than believed.** Take any
two data points and compare the ratio of what was drawn to the ratio of what is true:

```js
const shown = y.extent(bounds.min, big) / y.extent(bounds.min, small);
const actual = big / small;
const lieFactor = shown / actual;
Stage.check('lie factor within tolerance', Math.abs(lieFactor - 1) <= 0.05, lieFactor.toFixed(3));
```

> [!IMPORTANT]
> **With a zero-based linear scale this comes out at 1 by construction — which is exactly why it is
> worth computing.** It is not a sixth rule to remember; it is the check that proves the five above
> actually held in the finished picture. It catches what they cannot: a rectangle placed by hand, an
> icon scaled by eye, a baseline nudged during a later revision, an image resized non-uniformly. Those
> are the ways a correct scale still yields a lying graphic.
>
> `scale.invert(...)` lets you close the loop from the other end — read a drawn pixel back into a
> value and compare it with the datum it was supposed to encode.

---

## 3. Data-Ink and the Smallest Effective Difference

> **Implemented by**: no single call — this governs how you set `ctx.lineWidth`, `ctx.strokeStyle`
> and fills throughout. `bitmap.palette(...)` is what turns it from taste into a reading.

Tufte separates the ink that carries information from the ink that does not. **Data-ink is the
non-erasable core of a graphic** — the marks that change when the numbers change. Everything else is
frame, grid, rule, shadow, bevel, and decoration.

The principle, in his words and including his own hedge: **maximise the data-ink ratio, within
reason.** Every mark needs a reason to exist, and nearly always that reason should be that it carries
information a reader could not otherwise get.

The practical form is an editing pass, not a design rule: **draw it, then erase.** Take out the grid,
the frame, the tick marks, the second decimal, the drop shadow, the legend that a direct label would
replace — and keep each removal that costs the reader nothing.

### 3a. How subtle is subtle enough

*Visual Explanations* gives the rule that stops the erasing pass from going too far, and it is the
most directly usable sentence in any of these books:

> **Make all visual distinctions as subtle as possible, but still clear and effective.**

Tufte calls it the Occam's razor of information design. It applies to every difference you draw: the
weight step between a data line and a gridline, the value step between a layer and its ground, the
size step between a heading and a label. The failure it names is the one nobody looks for — a
distinction *louder than its job*, where the emphasis is real but disproportionate to what it
signals. A hairline would have separated those two layers; a 3px rule separates them and also shouts.

Concretely, in this SDK: gridlines one step off the ground rather than mid-grey, hairline rather than
1px where the surface allows, and layer separation attempted first with **value**, then weight, then
colour — in that order, because value costs the least attention.

### 3b. And it is measurable

`bitmap.palette(...)` reports each dominant colour and its share of the image, so **ink share is a
number you can read** — and the erasing pass becomes an experiment rather than an opinion:

```js
const before = canvas.toBitmap().palette(6);
// …remove the gridlines, redraw…
const after = canvas.bitmap.palette(6);
Stage.note(`ink share ${before[0].share.toFixed(3)} → ${after[0].share.toFixed(3)} after erasing the grid`);
```

The same call checks the smallest effective difference from the other side: if two layers you
intended to separate collapse into one bucket, the difference was too subtle and the palette says so.

> [!NOTE]
> **This is the one section where the sources genuinely disagree, so treat it as a knob rather than a
> law.** Tufte's case for minimal non-data ink is an argument for analytic efficiency — the fastest,
> least-mediated path from mark to number. There is a real counter-literature arguing that
> embellishment aids recall and engagement, and that a graphic nobody looks at communicates nothing.
> **We do not hold that literature and it is not in `reference/`, so nothing here cites it** — but the
> disagreement exists and you should know it does.
>
> The studio's own position: for an analytic display, erase hard. For **advertising and brand work,
> where the job may be attention rather than efficiency**, ink beyond the data can be doing real work
> — and §6's "the ground participates" is that case argued deliberately. Tufte's own *"within reason"*
> concedes the point further than he is usually quoted as doing. What is never defensible is
> decoration that *distorts* — that is §2, and §2 does not bend.

---

## 4. Axes, Chrome & Labels

> **Implemented by**: `Scale.ticks(...)` and `Scale.nice(...)` for the numbers, `ctx.fillText(...)` and
> `ctx.measureText(...)` for the labels, `Layout.inset(...)` for the plot's margins.
>
> This section is §3 applied to the furniture of a chart: each rule below is either data-ink
> reduction or the smallest effective difference, made specific.

**A static image has no tooltips.** Every value that matters is directly labelled or readable off a
labelled axis — but label *selectively*: endpoints, extremes, and the hero series. A number glued to
every point is noise, not rigour.

**Ticks are round numbers.** `Scale.nice(min, max)` widens the bounds outward so the first and last
tick sit at the ends of the plot; `scale.ticks(n)` gives steps of 1, 2 or 5 times a power of ten. An
axis reading 0 / 50 / 100 is legible; one reading 0 / 47.5 / 95 is arithmetic showing through.

**Chrome is quiet or absent.** Gridlines hairline and one step off the background, or none at all —
an axis line with direct labels usually beats a grid in an infographic. Bars 24–32px thick with air
between them; lines 2–3px.

**A direct label beats a legend.** A legend makes the reader hold a colour in memory and carry it to
the mark; a label at the end of the line removes the task. This is data-ink reduction and rank-1
positioning at the same time.

**Text never wears the series colour.** Labels and values take the ink colour; the coloured mark
beside them carries the identity. Text set *inside* a filled shape picks white or ink by that fill's
luminance, and only goes inside if it fits with padding — otherwise it goes outside the bar's end.
Never clip a label.

---

## 5. Micro/Macro Readings and Layering

> **Implemented by**: `ctx.lineWidth` tiers and `Skia.Brush.ink(...)` for weight, `Layout.*` for
> zones, `bitmap.palette(...)` to check a layer actually separates. Judgment governs this section
> more than any other here — it is the one place where the checks run out.

Most advice about dense information says *simplify*. Tufte's is the opposite, and it is the theory of
every technical drawing that rewards a second look:

> **To clarify, add detail.**

A **micro/macro** display works at two distances at once. Stand back and it resolves into pattern —
the shape of the trajectory, the balance of the mass budget, where the weight sits. Lean in and it
holds particulars — this callout, that figure, this annotated waypoint. Neither reading is a
concession to the other; the detail *is* what makes the pattern legible, because a pattern with
nothing beneath it is a shape rather than a finding.

The practical consequence is that **data density is usually too low, not too high**. Tufte measures
it as the number of entries in the data matrix divided by the area of the graphic. Four numbers
across a poster is a decoration with figures on it. When a panel feels thin, the fix is more often
another layer of real information than more white space.

### 5a. Layering and separation

Density only works if the layers come apart. Tufte is worth quoting exactly here because the sentence
reassigns blame:

> **Confusion and clutter are failures of design, not attributes of information.**

So a busy graphic is not a reason to remove information — it is a reason to stratify it. Give each
layer its own visual register and let the reader attend to one at a time:

- **Value first.** A ground layer at low contrast, data at full ink. Value separates with the least
  attention and the least colour.
- **Weight second.** A three-tier line hierarchy is enough for most drawings — structure, data,
  annotation. Manual 03 §1's ink weights are the same idea in a different craft.
- **Colour last, and sparingly.** Colour is rank 6 for quantity (§1) but excellent for *identity*.
  Use it to say which, not how much.
- **Per §3a, the smallest step that works.** Layering fails in both directions: too little separation
  and it is mud, too much and the layers stop reading as one picture.

`bitmap.palette(...)` will tell you whether a separation you intended actually landed — if the layer
and its ground fall in one bucket, it did not.

> [!NOTE]
> **This is the softest section in the manual and knows it.** There is no assertion that proves a
> micro/macro reading succeeded; the honest check is Manual 03's, applied at both distances — look at
> the whole at final size, then look at a detail at 100%, and ask whether each is doing its job. §8's
> reduction test is the closest mechanical proxy.

---

## 6. The Canvas Is a Place

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

### 6a. Diegetic data, in practice: the slot armature

> **Implemented by**: `model.slots` on every `Chart.*` construction.

This is the bullet above made buildable, and for advertising and brand work it is the most useful
thing in the toolkit. **A chart model is an armature, not a picture.** Every one carries `slots` — one
per mark, saying where it stands (`baseX`, `baseY`), how big it is (`thickness`, `length`), which way
it grows (`angleDeg`), and how far up the scale it reached (`fraction`, 0 to 1).

A rectangle is the dullest thing you can put in that box. Put a skyscraper there and a column chart of
record heights becomes a skyline; put a filling bottle, a stack of coins, a tree, a rocket. The
graphic still has a zero baseline and a lie factor of 1 underneath, which is the point — **the
audience gets the picture and the client gets a defensible chart, from the same construction.**

```js
for (const slot of chart.slots) {
    const floors = Math.round(4 + slot.fraction * 22);       // taller value, more storeys
    drawTower(ctx, slot.baseX, slot.baseY, slot.thickness, slot.length, floors);
}
```

Three things make it worth using rather than positioning marks by hand:

- **It is the same in every form.** `angleDeg` is 0 for up and 90 for right, so a mark routine written
  for a column chart runs unchanged on a bar chart or a dot chart. A house style survives a change of
  form.
- **`fraction` is a scale position, not a rank.** It answers *how full*, which is what a custom mark
  needs. Note that with a niced maximum above the data, the largest value does not reach 1 — compare
  values if you need "the biggest", or pass an explicit `max`.
- **A waffle's slots are its cells**, which is the isotype idiom: a hundred little figures rather than
  a hundred squares.

> [!TIP]
> `Chart.drawChart(...)` fills plain rectangles. It is for a draft, to see the numbers land. It is not
> what a finished piece should be using.

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

## 7. Tension Rules

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

## 8. Litmus Tests Before Rendering

Run these against the render, not the plan. **The first five are assertions**; the last three are
perceptual and stay judgment.

| Test | How | Fails when |
| :--- | :--- | :--- |
| Zero baseline | `scale.isZeroBased` | anything whose *length* or *area* encodes value |
| Lie factor | §2a, `Stage.check(...)` | outside 0.95–1.05 |
| One shared scale | a single `Scale.extent(...)` over every series | small multiples drawn to their own extents |
| Layers separate | `bitmap.palette(...)` | a layer and its ground land in one bucket |
| Legible at final size | `bitmap.resize(...)` to the delivered size, then read it | the smallest type or thinnest rule disappears |

**The reduction test is borrowed from a different craft and is the most useful of the five.** Lee and
Buscema judge inked comic art by whether it still reads *after reduction to printed size*; a chart is
judged the same way, because almost nothing is viewed at the size it was drawn. `bitmap.resize(...)`
makes it a real check rather than a squint:

```js
const delivered = canvas.toBitmap().resize(480, 260);
Stage.expect('axis labels and hairlines survive reduction to 480px');
```

Then the three that need eyes:

- **Could this layout hold a SaaS dashboard's data without looking odd?** Then it is a dashboard.
  Recompose (§6).
- **Cover the text — is the topic still recognisable from the shapes alone?** If not, there is no
  visual identity yet.
- **Are all four corners doing the same amount of work?** Then there is no focal point.

---

## 9. Symbol → SDK Parameter Map

| Concept | SDK call or field | Notes |
| --- | --- | --- |
| A whole bar or column chart | `Chart.createColumnChart(rect, data, options)`, `Chart.createBarChart(...)` | Returns a model — bars, ticks, labels, scale — and draws nothing. Start here rather than with `Scale`. |
| Its marks as geometry | `Chart.createChartGeometry(model)` | One `CanvasPath` per bar plus a silhouette, for clipping, subtracting, texturing or animating. |
| Drawing the marks | `Chart.drawChart(ctx, model)` | Fills with the current `fillStyle` and returns the geometry. Draws **no** chrome — see §3. |
| Integrity, carried | `model.lieFactor`, `model.isZeroBased`, `model.encodingRank` | Computed at construction; §1a and §2a rather than a checklist. |
| Value → pixel | `Scale.linear(d0, d1, r0, r1)` | Range may run backwards; that is a vertical axis. |
| Pixel → value | `scale.invert(position)` | Reads a drawn coordinate back to a datum — the other half of a lie-factor check. |
| Bar length | `scale.extent(baseline, value)` | Always positive, whichever way the range runs. |
| Zero-baseline check | `scale.isZeroBased` | Assert it for bars, columns, areas. |
| Category positions | `Scale.band(count, r0, r1, padding)` | `band.bandwidth` is the drawn width; `band.center(i)` is the label anchor; `band.step` includes the gap. |
| Round axis numbers | `Scale.nice(min, max)`, `scale.ticks(n)` | `nice` first, then build the scale from its bounds. |
| Shared scale | `Scale.extent(allValues)` | Once, over every series. Non-negotiable for small multiples. |
| Area encoding | `Scale.radiusFor(value, maxValue, maxRadius)` | Square-rooted, so area carries the value. |
| Ink share / layer separation | `bitmap.palette(count)` | Dominant colours and their shares — §3b and §5a. |
| Reduction test | `bitmap.resize(w, h)` | Redraw the judgment at delivered size — §8. |
| Zones and margins | `Layout.rows/columns(rect, weights, gap)`, `Layout.inset(...)` | Weighted, not equal — see §7. |
| Measured text blocks | `ctx.measureWrappedText(text, maxWidth)` | Height before placement, so captions can stack. |
| Design language | `Css.fromCss(...)`, `sheet.tokens()`, `sheet.rule('.h1')` | Palette and type from a stylesheet; see Manual 11 §3. |
| Recording a trade-off | `Stage.note(...)`, `Stage.check(...)` | An accuracy traded for a form (§1b) is a decision a reader should find. |

---

## 10. Constructing It: A Runnable Panel

Two column series and an area-encoded set, with §2a's lie factor asserted on the result. **The
columns are one call each** — `Chart.createColumnChart(...)` returns the model and draws nothing, so
the drawing below is the part you actually chose. The circles are still built from `Scale.radiusFor`,
because area encoding has no construction of its own.

```javascript
// Encoding demo: a shared scale across two panels, sqrt-scaled circles, lie factor asserted.
const canvas = createCanvas(900, 460);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f7f4ee';
ctx.fillRect(0, 0, 900, 460);

const page = Layout.inset(Layout.rect(0, 0, 900, 460), 34);
// §7 — weighted, not equal: one dense zone and one that breathes.
const [left, right] = Layout.columns(page, [62, 38], 34);

// §2 — one extent over BOTH series, passed to both charts as `max`. This is the rule no single
// panel can detect, because each panel is individually correct.
const north = [38, 61, 47, 92, 74];
const south = [22, 35, 29, 58, 44];
const shared = Scale.extent(north.concat(south));
const bounds = Scale.nice(0, shared.max);
const labels = ['Mar', 'Apr', 'May', 'Jun', 'Jul'];

const [plotN, plotS] = Layout.rows(left, 2, 22);
const charts = [
    { model: Chart.createColumnChart(plotN, north, { max: bounds.max, labels: labels }), fill: '#1f6f8b', title: 'NORTH' },
    { model: Chart.createColumnChart(plotS, south, { max: bounds.max, labels: labels }), fill: '#c96a2e', title: 'SOUTH' }
];

for (const panel of charts) {
    // §2a — carried by the model, not recomputed by hand. Zero-based, so it is 1 by construction.
    if (!panel.model.isZeroBased) throw new Error('columns need a zero baseline');
    if (Math.abs(panel.model.lieFactor - 1) > 0.05) throw new Error('graphic distorts the data');

    ctx.fillStyle = panel.fill;
    Chart.drawChart(ctx, panel.model);

    ctx.font = '600 12px sans-serif';
    ctx.fillStyle = '#6b7684';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'top';
    ctx.fillText(panel.title, panel.model.plot.x, panel.model.plot.y - 12);

    // §3 — the labels are data, so drawing them is a decision. Here we want them; the gridlines
    // the ticks would carry are left undrawn, which is the erasing pass as a choice.
    ctx.font = '400 10px sans-serif';
    ctx.fillStyle = '#8a94a0';
    ctx.textAlign = 'center';
    for (const label of panel.model.labels) ctx.fillText(label.text, label.x, label.y);
}

log('lie factor ' + charts[0].model.lieFactor.toFixed(4) + ', rank ' + charts[0].model.encodingRank);

// The marks came back as geometry, so the hero bar is picked out without redrawing the panel.
const hero = charts[0].model.bars.reduce((a, b) => (b.value > a.value ? b : a));
ctx.fillStyle = '#15384a';
ctx.fillRect(hero.x, hero.y, hero.width, hero.height);

// §2 — area encodes value: four times the number is twice the radius. No construction for this one.
const circles = [{ v: 25, t: '25' }, { v: 50, t: '50' }, { v: 100, t: '100' }];
const slot = Layout.rows(right, [22, 78], 10)[1];
const band = Scale.band(circles.length, slot.x, slot.x2, 0.1);

ctx.font = '600 12px sans-serif';
ctx.fillStyle = '#6b7684';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
// Plain ASCII in *drawn* text: the maths glyphs in this manual's prose are not in every installed
// face, and a missing glyph renders as a tofu box rather than failing loudly.
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

// §5a — did the layers actually separate? The palette answers; the eye guesses.
const palette = canvas.toBitmap().palette(4);
log('dominant ink ' + palette[0].color + ' at ' + (palette[0].share * 100).toFixed(1) + '%');

canvas;
```
