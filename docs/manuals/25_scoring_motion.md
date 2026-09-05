# Studio Manual 25: Scoring Motion

> **Credits & Theoretical Foundation**: **This manual distils no book in `reference/`, and says so rather than borrowing a citation.** The studio's reference corpus is about drawing, not timing, and there is no animation text in it. What is here comes from three places, each named where it is used: the **position paradigm** is GSAP's, adopted deliberately and credited (it is not from the corpus and no claim is made about the rest of that library); the **cost figures** are our own measurements, recorded in `docs/motion-score-api.md` §3; and the **seek-rather-than-play** model is the studio's own, argued from the medium — an agent renders frames, it does not watch them. Where this manual states a principle of timing craft that animators have long known, it is stated as received practice without attribution to a source we have not read. The calls are documented at `polson://sdk/core/Motion`.
> **Purpose**: How to build a piece of motion that can be inspected, revised and resumed — why a score is a function of time rather than a sequence of events, how to place beats so that editing one does not break the rest, and how to look at a result you cannot watch. **It governs the building of a score, not the decision that the piece should move**; see the scope note below.

---

> [!CAUTION]
> **This manual governs how to build a seekable score. It does not decide that the piece should
> move, and it is not the only way to make something that does.**
>
> **A still is not a lesser deliverable.** Motion earns its place when *sequence itself carries
> meaning* — a process in order, a change over time, a reveal, a before and after. Where the content
> has no sequence, movement is decoration that also takes the reader's control of the pace away.
> Whether this piece has a sequence worth showing is a design judgment upstream of this manual, and
> nothing here answers it.
>
> **Nor does every piece of motion need a timeline.** `Motion.frame(...)` captures whatever is on a
> canvas, so a procedural loop that redraws and captures needs no score at all — §6's render cost is
> what makes rebuilding per frame affordable. A dozen states tiled with `Motion.sheet(...)` may be
> the whole job. Reach for `Motion.timeline(...)` when you want what §1 lists: to look at any moment,
> to reproduce a frame exactly, to render out of order, or to revise one beat without disturbing the
> rest. A twelve-frame logo sting driven by a straight loop needs none of those.
>
> **And read the firmness here differently from `polson://manual/13`.** That manual is prescriptive
> because §2 is about *truth* — a bar's length makes a claim that can be false. **Motion makes no
> such claim, so this manual has no §2 and is mostly craft.** §5 in particular is received practice,
> unsourced and said so in the header; treat it as advice from a tradition rather than as a finding.
> The one part that behaves like a correctness rule is §1's purity property, and even that binds
> **conditionally** — it is what makes seeking work, so it applies exactly when you want seeking.
>
> Where the two manuals meet: an animated infographic is still an infographic. The integrity rules
> in Manual 13 §2 apply to every quantity in every frame, and a bar that grows is a bar the whole way
> up.

---

## 1. A Score Is a Function of Time

> **Implemented by**: `Motion.timeline(...)`, `tl.seek(...)`, `tl.duration`.

The obvious way to animate is to say what happens next: move this, then when it arrives, fade that. It works, and it forecloses almost everything you will want.

A score does the opposite. Every entry declares **when** it happens, and `tl.seek(t)` states the whole scene at `t` — computed from `t` alone, not from anything that ran before it.

```js
tl.seek(4200);        // the scene at 4.2 seconds, with nothing played to get there
```

Four things follow, and each is a capability you would otherwise not have:

1. **You can look at any moment** without rendering the ones before it.
2. **A frame reproduces exactly.** Seek to 4200 twice and get the same picture, so a comparison between two renders means something.
3. **Frames can be rendered in any order**, or in parallel, or resumed after a failure.
4. **Revision is local.** Change beat three and beats one and two are untouched, because they were never a prefix that beat three depended on.

None of that survives callback sequencing, where the only way to know the scene at 4.2 s is to execute everything up to it.

> [!IMPORTANT]
> The property that buys all four: **every entry answers for any `t`, including outside its own window.** Before it starts it applies its *from* value; after it ends it holds its *to*. An entry that simply did nothing outside its window would leave whatever the previous seek happened to write, and a backwards seek would not reproduce.

---

## 2. Place Beats Relative to Each Other

> **Implemented by**: `tl.label(...)`, `tl.labels`, and the `at` option on every entry.

A score written in absolute milliseconds is correct exactly once. Lengthen the opening by 300 ms and every later number is wrong, and you fix them by hand, and you miss one.

So place each beat **against its neighbour or against a named moment**, and let the arithmetic follow:

| You write | You mean |
| :--- | :--- |
| *(nothing)* | after everything so far — the common case |
| `'+=200'` | a 200 ms breath after the current end |
| `'-=300'` | start 300 ms early, overlapping what precedes it |
| `'<'` / `'>'` | together with the previous beat / straight after it |
| `'<+=120'` | 120 ms after the previous beat began |
| `'reveal'` | at the moment you named `reveal` |
| `'reveal+=400'` | 400 ms after it |

Name the moments a reader would want to talk about — `open`, `reveal`, `settle` — with `tl.label(...)`, and read them back from `tl.labels`. A label costs nothing and turns "1,840 ms" into "the reveal", which is what you are actually reasoning about.

> [!CAUTION]
> **A mistyped label throws, and that is the feature.** `'reveaI'` with a capital i is not a near miss that lands somewhere sensible — resolving it to zero would place the beat at the start of the film, animate happily, and produce a piece that is wrong in a way no error reports. The call names the label it could not find and lists the ones that exist.

---

## 3. Everything That Changes Belongs in the Score

> **Implemented by**: `tl.tween(...)`, `tl.to(...)`, `tl.set(...)`, `tl.show(...)`, `tl.stagger(...)`.

This is the rule that catches people, and it follows from §1 rather than being an extra restriction: **a change made outside the score does not come back on a backwards seek.** The score can only restore what it knows about.

It binds **as far as you want seeking to work**, which is the conditional in the scope note above. The alternative is not to cheat it but to step outside it: **rebuild the scene each frame and capture that**, which §6's render cost makes affordable and which needs no score at all. What does not work is a half-measure — some state scored and some mutated behind its back — because that reproduces going forwards and quietly does not going back.

Five ways to put a change into it, and the choice is about what kind of change it is:

- **`tl.to(element, attrs)`** — attributes that interpolate: positions, radii, widths, opacities, colours.
- **`tl.tween(from, to, setter)`** — a number handed to your own function. **This is the case a declarative animation format cannot express**: geometry recomputed per frame, a path rebuilt from an angle, a value fed through your own arithmetic. Reach for it whenever what changes is not an attribute.
- **`tl.set(element, attrs)`** — a step, not a tween. Takes what could never be interpolated: a font, a dash pattern, a gradient reference.
- **`tl.show(element, { from, to })`** — a visibility window. Use this rather than creating and destroying elements. An element removed by a callback is *gone*, and seeking backwards cannot bring it back.
- **`tl.stagger(elements, attrs, { each })`** — the same move across many elements, offset. Bars arriving in sequence, letters landing one after another.

> [!IMPORTANT]
> **A `to` captures its starting value when you add it, not when it first runs.** So construct the scene, then score it — in that order. If you change an attribute after scoring it, the tween still starts from what it captured, which is the only deterministic choice available: a seekable score has no "first run" to capture at.
>
> A tween also needs *somewhere to start from*, and refuses rather than guessing when there is none. SVG's defaults differ per attribute — `opacity` begins at 1 and `cx` at 0 — so one assumption would be wrong half the time, and wrong here means the move runs from the opposite end. Give the element the attribute first.

---

## 4. Looking at Something You Cannot Watch

> **Implemented by**: `Motion.frame(...)`, `Motion.sheet(...)`, `Motion.save(...)`, `Motion.count`, `Motion.clear()`.

The loop is always the same: seek, capture, repeat.

```js
for (let t = 0; t <= tl.duration; t += 40) { tl.seek(t); Motion.frame(paper); }
```

Then two different artifacts, for two different readers:

- **`Motion.sheet(...)` is what you look at.** A contact sheet is one image, so it is one read, and the eye works *across* cells — which is how you see that a beat lands late or that two moves collide. It is also comparable: `bitmap.diff` can be pointed at it.
- **`Motion.save(...)` is what you ship.** An animated file is the deliverable and close to the worst thing to hand yourself for inspection: you can produce one and still not perceive the motion in it.

> [!CAUTION]
> **Do not hand an animated file to the next stage of work either.** Animated WebP is frame-differenced — measured on a 47-frame file, exactly one frame was independently decodable and the rest chained, so reaching frame 36 cost 13 ms against 1 ms for frame 0. Random access is O(n) and gets worse with length. Pass on **the script**, and re-seek to reach a moment; that is O(1) at any `t`.

`Motion.count` reports how many frames are held and `Motion.clear()` discards them, which matters because frames are uncompressed bitmaps and a long capture at full size will reach the ceiling.

---

## 5. Timing Craft

> **Implemented by**: the `easing` and `each` options, and `mina`'s curves.

The mechanics above will produce motion that is technically correct and lifeless. Four received practices fix most of it:

**Nothing starts and stops abruptly.** Linear motion reads as mechanical because almost nothing in the world moves that way. `mina.easeinout` is the default worth reaching for; `mina.easein` for something departing, `mina.easeout` for something arriving.

**Overlap, do not queue.** Beats that abut exactly read as a list of events. Start the next one slightly before the last has settled — `'-=200'` — and they read as one movement. This is the single change that most improves a mechanical-looking score.

**Let things overshoot and settle.** `mina.backout` carries a move past its target and brings it back; `mina.elastic` does it repeatedly. Used on an arrival it reads as weight. Used everywhere it reads as a toy.

**Stagger anything that repeats.** Four bars arriving together are one event; arriving 90 ms apart they are a sequence the eye can follow. `each` between 60 and 120 ms is usually right — below that it reads as simultaneous, above it as four separate events.

> [!TIP]
> **A hold is a beat.** Motion that never rests gives the reader nowhere to look. Leave gaps — a `'+=400'` before a reveal is not wasted time, it is the pause that makes the reveal land.

---

## 6. What It Costs, and Where

Measured on this stack, and the ranking is not what you would guess:

- **Rendering a frame is cheap**: 2.7 ms at 1280 × 720 with 20 animated elements, 12.2 ms with 200. It scales with element count, barely with resolution.
- **Encoding dominates**: 59–68 ms per frame, five to twenty times the render.

Two consequences. First, **rebuilding the scene per frame is affordable** — if scoring every change is awkward, redrawing from scratch each frame is a legitimate alternative and costs milliseconds. Second, **the lever is resolution and frame count, not algorithmic cleverness**: draft at a small size and a coarse step, and render the final pass large.

---

## 7. What a Score Will Not Do

Stated plainly, so it is not discovered late:

- **No path morphing.** Numbers and colours interpolate. A path's `d` does not — the two shapes would need matching segment structure. Rebuild the path in a `tl.tween(...)` setter instead, which is more work and gives you exactly what you asked for.
- **No nested timelines.** One score, one flat list of beats.
- **No clock, and no playback controls.** There is no `play`, `pause` or `reverse`, because there is no viewer sitting in front of it. There is only `tl.seek(...)`, which is strictly more powerful.
- **An unknown option is ignored in silence.** `{ durr: 400 }` is accepted and does nothing. If a beat runs for the default duration when you asked for something else, check the spelling before you check anything else.

---

## 8. A Complete Score

Four beats, a procedural tween, a staggered arrival and a contact sheet — the whole loop.

```javascript
// Build the scene first: a `to` captures its starting value the moment it is scored.
const paper = Snap(900, 700);
paper.rect(0, 0, 900, 700).attr({ fill: '#f7f4ee' });

const bar = paper.rect(120, 400, 0, 40).attr({ fill: '#1f6f8b' });
const disc = paper.circle(180, 260, 30).attr({ fill: '#c9553d', opacity: 0 });
const caption = paper.text(120, 560, 'Q3 revenue').attr({ 'font-size': 30, fill: '#1c2733' });
const needle = paper.path('M760,560 L760,480').attr({ stroke: '#1c2733', 'stroke-width': 6 });
const ticks = [0, 1, 2, 3].map(i =>
    paper.rect(140 + i * 150, 620, 8, 0).attr({ fill: '#8a94a0' }));

const tl = Motion.timeline({ defaults: { dur: 420, easing: mina.easeinout } });

// Name the moment, then hang the beats off each other rather than off the clock.
tl.label('open');
tl.to(bar, { width: 640 }, { at: 'open', dur: 620 });
tl.to(disc, { opacity: 1, cy: 220 }, { at: '-=300' });          // overlaps, so it reads as one move

// The procedural case: geometry recomputed per frame, which no attribute tween could state.
tl.tween(0, 75, deg => {
    const rad = deg * Math.PI / 180;
    const x = (760 + Math.sin(rad) * 80).toFixed(1);
    const y = (560 - Math.cos(rad) * 80).toFixed(1);
    needle.attr({ d: `M760,560 L${x},${y}` });
}, { at: 'open', dur: 900, easing: mina.backout });

tl.show(caption, { from: 700 });                                 // a window, not a create/destroy
tl.stagger(ticks, { height: 70 }, { at: '>+=200', dur: 260, each: 90 });

log(`${tl.count} beats over ${tl.duration} ms; 'open' is at ${tl.labels.open} ms`);

// Seek, capture, repeat. Nothing here depends on the order the frames are taken in.
Motion.clear();
for (let t = 0; t <= tl.duration; t += 60) { tl.seek(t); Motion.frame(paper); }
log(`captured ${Motion.count} frames`);

Motion.sheet('artifacts/manual25-score-sheet.png', { count: 6, cols: 6, scale: 0.4, fps: 25 });
Motion.save('artifacts/manual25-score.webp', { fps: 25 });

tl.seek(tl.duration);      // leave the scene at its final state for the still render
paper;
```
