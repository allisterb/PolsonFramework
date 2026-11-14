# Studio Manual 25: Scoring Motion

> **Credits & Theoretical Foundation**: **§5 distils John Lasseter, "Principles of Traditional Animation Applied to 3D Computer Animation" (*Computer Graphics* 21(4), SIGGRAPH '87, pp. 35–44) and John Colette & Austin Shaw, *Motion Design Toolkit* (Routledge, 2023), ch. 1, 2 and 8; §12 distils Colette & Shaw ch. 1, 7 and 11; both ledgered 2026-10-24 and cited by section or chapter where used. §13 distils Austin Shaw, *Design for Motion: Motion Design Techniques & Fundamentals* (Focal Press, 2016), ch. 2–4, 7 and 8, ledgered 2026-10-24 (the same retrieval clause: methods in our words, never its text); §14 distils its ch. 9, and restates Manual 21 §1's depth rules (Glebas, after Perkins) because no motion workflow loads Manual 21; §4's symptom table and §5j's motion identity are organised after LottieFiles' *Motion Design Skill* (MIT, 2025, ledgered 2026-10-25), whose own content is unsourced and is not adopted; §16 distils its ch. 13 (§16f its ch. 15), and its costs are our measurements; §17 distils its ch. 17 with Rob Carter, Ben Day & Philip Meggs, *Typographic Design: Form and Communication* (Wiley, 5th ed., 2012), pp. 115, 143–147 and 173–175, ledgered 2026-10-16 (the same retrieval clause: our words, cited by folio); §15 distils its ch. 10 (with two points from ch. 11), and its camera mappings (tilt, roll, the laid-down plane) are ours, measured on the engine. §11 distils Walt Stanchfield's *Drawn to Life* (vol. 1 ch. 41 and others), cited by chapter there; §1–§4 and §6–§10 distil no book in `reference/`.** Lasseter's source, Thomas & Johnston's *The Illusion of Life* (1981), is not in `reference/` and is cited only through him. This header used to say the corpus held no animation text. That was wrong: Stanchfield taught at Disney, and his lectures, ledgered on 2026-09-25 and first read only for gesture, include chapters on timing, phrasing and overlap. §5's received practices are corroborated there. The rest comes from three places, each named where it is used: the **position paradigm** is GSAP's, adopted deliberately and credited (it is not from the corpus and no claim is made about the rest of that library); the **cost figures** are our own measurements, recorded in `docs/motion-score-api.md` §3; and the **seek-rather-than-play** model is the studio's own, argued from the medium — an agent renders frames, it does not watch them. Until 2026-10-24 §5 stated its timing craft as received practice without a source; it is now sourced, and anything in it that is the studio's own is marked as ours. The calls are documented at `polson://sdk/core/Motion`.
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
> such claim, so this manual has no §2 and is mostly craft.** §5 in particular is the animation tradition's craft —
> sourced to Lasseter and to Colette & Shaw, corroborated by Stanchfield (§11) — but not measured; treat it as advice
> from a tradition rather than as a finding.
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

> **Implemented by**: `Motion.frame(...)`, `Motion.sheet(...)`, `Motion.save(...)`, `Motion.saveVideo(...)`, `comp.saveVideo(...)`, `Motion.joinVideos(...)`, `Motion.videoToWebp(...)`, `Motion.count`, `Motion.clear()`.

The loop is always the same: seek, capture, repeat.

```js
for (let t = 0; t <= tl.duration; t += 40) { tl.seek(t); Motion.frame(paper); }
```

Then two different artifacts, for two different readers:

- **`Motion.sheet(...)` is what you look at.** A contact sheet is one image, so it is one read, and the eye works *across* cells — which is how you see that a beat lands late or that two moves collide. It is also comparable: `bitmap.diff` can be pointed at it.
- **`Motion.save(...)` is what you ship.** An animated file is the deliverable and close to the worst thing to hand yourself for inspection: you can produce one and still not perceive the motion in it.
- **With sound, ship a video instead: `Motion.saveVideo('output.mp4', { audio })`**, or `comp.saveVideo(...)` straight from a composition, which renders each frame into the encoder and holds none, so a long film does not meet the frame ceiling below. A video has no alpha, so give the film a ground or name a `background`; and read the result's `warnings`, which is the only place a line cut off by the end of the film is reported. **A film too long to encode inside one script's time limit is encoded a span at a time** — `comp.saveVideo(part, { from, to })`, one script each — and the parts joined with `Motion.joinVideos([parts], 'output.mp4', { audio })`, which copies them end to end without re-encoding and lays the sound over the whole.
- **A silent film that has to go where only images go** — a README, a Markdown write-up — is made a video first and then a looping WebP with `Motion.videoToWebp('output.mp4', 'output.webp')`, in a script of its own: 7–9 s for fifteen seconds of film at its default width of 960 or less, 8–16 s at 1280 depending on how much changes from frame to frame, about twice that on a busy machine, and more than a script at 1920. `Motion.save` cannot do this for a long film, because it holds every frame in memory and cannot take one encoded in parts.

> [!CAUTION]
> **Do not hand an animated file to the next stage of work either.** Animated WebP is frame-differenced — measured on a 47-frame file, exactly one frame was independently decodable and the rest chained, so reaching frame 36 cost 13 ms against 1 ms for frame 0. Random access is O(n) and gets worse with length. Pass on **the script**, and re-seek to reach a moment; that is O(1) at any `t`.

`Motion.count` reports how many frames are held and `Motion.clear()` discards them, which matters because frames are uncompressed bitmaps and a long capture at full size will reach the ceiling.

### Mathematics on screen

> **Implemented by**: `comp.equation(...)`, `comp.axes(...)`, `comp.number(...)`, `Equation.equivalent(...)`, `Verify.derivation(...)`, `Verify.claim(...)`.

An equation is set with `comp.equation({ latex, origin, size })`, typeset in the engine from LaTeX, never lettered by hand
with `comp.text` and never pasted in as a picture. It is drawn as outlines in a group of its own, so it moves, fades and
keys as any group does; grow it by scaling a group around it, since its size is fixed when it is typeset.

**Give an equation its reading time, and a little more.** A viewer reads a formula symbol by symbol, more slowly than
words. Hold it still until it has been read, and change one thing at a time.

`comp.texts()` lists each equation by its linear form, the line a reader would write (`x = (−b ± √(b² − 4ac))/(2a)`),
so the read-back below checks equations as it checks words: what the film set against what a watcher read. The handle
keeps both: `equation.latex` is what you wrote, `equation.text` the linear form.

**Check the mathematics before you animate it.** A derivation drawn beautifully with a wrong step teaches the wrong
step. `Equation.equivalent(previous, next)` says whether each step equals the one before: `'proved'` by simplification,
`'agrees'` at every point tried, or `'differs'` with the point where it fails. With one argument it checks an equation
`lhs = rhs`. A counterexample at a negative `x` usually means the step holds only where `x` is positive: say so with
`{ domain: { x: [0, 5] } }` and say so on screen too.

Then check the derivation as a whole, after the read-back: `Verify.derivation(steps)` takes the lines in order and
records the verdict itself, so the run's record shows the mathematics was checked, not claimed. Give it the
`comp.equation` handles you drew, and it also checks the steps as the watcher read them. A misreading that breaks the
chain is the film's problem to fix, not the mathematics'.

**A claim in words is checked by code a second model writes.** "Revenue grew 46%", or "the area under the curve is 9", is
not an equation until someone writes it as one. `Verify.claim(text, { context })` has Nemotron write the JavaScript that
checks it with these same routines, and runs that code: the verdict is the code's, and the code comes back with it. Read
it before trusting a pass. A check of the wrong thing is visible there, and nowhere else. Give it the figures the claim
rests on as `context` when they are not in the sentence. The code can integrate (`integrate(f, a, b)`, limits may be infinite) and find a root in a bracket
(`solve(f, a, b)`), so an area or a crossing point is checked by computing it, not by recognising it.

**A graph is drawn in data, not in pixels.** `comp.axes({ x: [min, max, step], y: [min, max, step], origin, size })` maps the
data onto the frame; `axes.point(x, y)` places anything at a data point. `axes.plot(f)` draws a function, sampled once,
and keying its `trimEnd` from 0 to 1 draws it on. `axes.area(f, a, b)` fills under it, `axes.riemann(f, a, b, n)` stands
rectangles under it and returns their total, and `axes.tangent(f, x)` returns the slope it drew. Print those returned
numbers, never retyped ones: a label computed from the value the picture drew cannot disagree with it. The axes' tick
labels are equations, read back with the film; `axes.x` and `axes.y` hold the ranges they were drawn to.

**A number that counts lands on its value and stays there.** `comp.number({ value, format, family })` shows a value
that follows a node: key it from 0 to the figure and the digits count up. Format it with `{ decimals, prefix, suffix }`
rather than building the string yourself. Hold the start and the end: `comp.texts()` lists only the values held for
half a second or more, and those are the claims the film is checked on. A value caught mid-count is not one.
`counter.textAt(t)` gives the string shown at a time and `counter.valueAt(t)` the value, so a caption can quote what
the counter landed on.

**A figure is written the way its audience writes it.** The same 130.5 billion dollars is `$130.5B` in New York,
`13,049.7 crore` in Mumbai, `1305億` in Tokyo and `130,5 Mrd. $` in Berlin. A viewer reads their own scale words
without effort and has to convert anyone else's, so for an audience in a place, write its figures that place's way:
`Locale.number(value, { locale: 'en-IN', compact: 'long', currency: 'USD' })` returns the string, and a counter takes
the same `locale`, `compact` and `currency` in its `format`. `Locale.locales` lists the places carried. The figure
itself never changes: a dollar amount stays in dollars, since converting it would be a new figure with a rate and a
date the document does not give, so the read-back still finds it on its page. Only the words around it are yours to
put in the audience's language.

### Reading the film back

> **Implemented by**: `comp.texts()`, `Verify.film(...)`, `Verify.stray(...)`.

A contact sheet shows you the film; it does not tell you whether a viewer will read what you set, or whether what you set is true. That takes a third reader with nothing invested in the film.
- **`comp.texts()`** lists every text the composition shows and the seconds it is on screen, read from the composition itself. That is the ground truth.
- **`Verify.film(comp, 'output.mp4')`** has a separate model watch the *encoded* film and transcribe it, holds the transcription against that list, and looks up where each text came from: the brief, a document's page, a Qloo answer, research, or a figure you say you computed. The watcher is Gemini unless the studio is set to Nemotron (`Video.reader` says which); Nemotron cannot be trusted with Hindi or Arabic, so on it a line in either is left to your eye.

What comes back is three findings:
- **A text set but not read** usually held too briefly or was set too small. That is a reading-time fault before it is the watcher's.
- **A reading of something never set** may be the watcher's misreading or a real fault, such as a figure caught half-drawn or a frame of the wrong scene. You cannot tell which from the words. Look at the frame it saved, then `Verify.stray(n, 'misread' | 'real')`.
- **A figure with no source** has no business on screen until you find it in a document or the brief, or give its working.

The watching is a judgment, and the check records it as one itself. Settle a stray in a later script than the check, after you have looked at its frame.

### What the sheet shows, and where to look

A sheet shows symptoms, and the fix for each is usually somewhere specific. Find the row for what you see, check the
cause, and measure where there is a check rather than trusting the sheet:

| You see | Usually | Fix | Measured by |
|---|---|---|---|
| **Mechanical, robotic** motion | one ease everywhere, or none; straight paths; everything starting and stopping together | eases that start, pass and stop (§5b); arcs (§5c); overlap and follow-through (§5g) | `Motion.graph`: evenly spaced dots are a linear move |
| A move **stutters** partway | a middle key left on `'halt'` | `'clamped'` through a key the move carries on past (§5b) | `Motion.graph`'s `stops` |
| Something **dips past where it rests**, or bounces unasked | `'auto'` on resting keys | `'halt'` at rest, or `'clamped'` (§5b) | `Motion.graph`'s `overshoots` |
| **Too much at once**; the eye does not know where to go | two ideas in one beat; competing heroes | one idea at a time (§5f); one focal point (§14a, §17d) | the sheet; the layout and overlap checks |
| A line is **not read**, or reads late | held too briefly; arriving while something else moves | arrive, then hold; a hold is a beat (§5i) | `text.settledAt`; `Verify.film`'s "set but not read" |
| **Jarring**, too fast | no anticipation; no settle; cutting and moving at once | anticipation sized to the action (§5e); cut or move, not both (§12) | the sheet |
| **Slow**, the film drags | holds longer than the audience's pace; a flat story | enough time and no more (§5a); the pace evidence; reshape the beats (§13b) | `pace()`; `Stage.shape`'s `flat` |
| **Flat**, cheap | one plane; no secondary action; type and pictures pasted on | planes and parallax (§14d); secondary action (§5h); one world (§16, §17c) | the sheet |
| **No personality**, or each move unlike the last | a different ease and length for every move | a motion identity (§5j) | the kit's `Motion.graph` against each film's |
| **Flicker** | flashes or a strobe | fewer, softer transitions | `comp.flashes()` |
| **Peppered** grain | grain too strong for a dark frame | 6%, or weaker light specks (§16d) | the sheet, on a dark frame |

The arrangement by symptom follows LottieFiles' *Motion Design Skill* (MIT, 2025). Every fix in the table is this
manual's own, from the sources each section names.

---

## 5. Timing Craft

> **Implemented by**: on a timeline, the `easing` and `each` options and `mina`'s curves; in a `Motion.composition`, each
> waypoint's `ease` (`'clamped'`, `'halt'`, `'linear'`, `'constant'`, `'auto'`), a layer's `scale`, and nested groups.

The mechanics above produce motion that is technically correct and lifeless. The craft that fixes it is old: the Disney
studio named it in the 1930s, Lasseter carried it into keyframed computer animation in 1987, and Colette and Shaw restate
it for motion designers (ch. 8). What follows is that craft, as it lands in this engine.

### 5a. Timing is weight, and it is meaning

**The spacing of a move, not the drawing of it, decides what it weighs** (Lasseter §2.2). A heavy thing is slow to start
and slow to stop; a light one starts at a flick and is stopped by the air. Two identical shapes read as a cannonball and a
balloon by timing alone. The same holds for size — a giant moves slower than a man — and for mood: lethargic, nervous,
relaxed and excited are differences of speed before they are differences of pose.

Timing is also meaning. Lasseter reprints Thomas and Johnston's demonstration: one head, turned from one shoulder to the
other between two fixed drawings. With no inbetween the character has been struck by a huge force; with one, hit by a
thrown pan; with four, he is giving a crisp order; with eight, searching a shelf; with ten, stretching a sore muscle. The
poses never change. **So a move's length in frames is a choice of meaning: count it, and say what it is for**, rather
than taking whatever duration a beat happened to get.

Spend **enough time and no more** on each of the three parts of an action — the preparation, the action, the reaction —
for the audience to read it: too long and attention wanders, too short and the idea is gone before it is noticed
(§2.2). **Rhythm is the alternation** (Colette & Shaw, ch. 1): slower *hero* moments hold the compositions that carry the
information, faster moves bring things in and change the scene. A film that is all one speed has no hero moments.

### 5b. Eases: how a move starts, passes and stops

**Nothing starts and stops abruptly.** Linear motion reads as mechanical because almost nothing in the world moves that
way (Lasseter §2.7, *slow in and out*; Colette & Shaw, ch. 2). On a timeline, `mina.easeinout` is the default worth
reaching for; `mina.easein` for something departing, `mina.easeout` for something arriving.

> [!WARNING]
> **The words are reversed between tools.** `mina` follows the web's naming, where *ease-in* means a slow start. After
> Effects, and Colette and Shaw with it, call a slow start an *ease-out* — easing out of the first key — and a slow
> arrival an *ease-in*. Read a source by what the motion does, not by the word.

In a composition, the ease belongs to each waypoint, and the choice is between five behaviours:

| Want | `ease` | Why |
| :--- | :--- | :--- |
| a move that starts or stops at this key | `'halt'` | slow out of it, slow into it — the classic ease |
| a move that **passes through** this key without stopping | `'clamped'` (default) | smooth, and never overshoots its neighbours. A middle key set to `'halt'` makes the move stop and restart there, a stutter Colette and Shaw single out (ch. 2); ease the ends, pass through the middle |
| constant speed | `'linear'` | for the mechanical only — a conveyor, a ticker, a turning dial |
| a cut on one element | `'constant'` | holds, then jumps: a hold key. A letter or a shape that jumps on the beat while everything else flows reads as an edit inside the frame (ch. 2) |
| a move that may swing past and settle | `'auto'` | a TCB spline: `tension`, `continuity` and `bias` shape it, and it **can overshoot** |

**Overshoot is a choice, never an accident.** Lasseter's Luxo Jr. had a heavy base that should sit, pop up, arc and land
dead; the spline overshot every held key and sent the base through the floor before and after each hop (§2.7). He fixed
it by locking the keys. Here `'clamped'` is that lock: use it for anything resting on a surface or reaching a limit, and
reach for `'auto'` — or, on a timeline, `mina.backout`, which carries a move past its target and brings it back — only
where the overshoot is the point. On an arrival it reads as weight; everywhere, it reads as a toy.

**Move together with one curve.** When position, scale and rotation change as one action, give them the same keys and
the same eases (Colette & Shaw, ch. 2): different curves on parts of one move make it wobble.

**Read the curve, not the frames.** An animator judges a move by the shape of its speed graph (Colette & Shaw, ch. 2)
and the spacing of its frames on a timing chart (Lasseter §2.7); a still shows one point of it. `Motion.graph(path,
{ name: node })` draws both and measures them, so the check is a number rather than an impression:

```javascript
// A slide in three keys: does it pass through the middle, and does anything swing past where it should rest?
const slide = Motion.nodes.animated('real', [
    { time: 0, value: 0, ease: 'halt' }, { time: 0.6, value: 280 }, { time: 1.2, value: 560, ease: 'halt' }]);
const g = Motion.graph('artifacts/slide-graph.png', { slide });
const c = g.curves[0];
Stage.check('the slide passes through its middle key and rests without overshoot',
    c.stops.length === 0 && c.overshoots.length === 0,
    `peak ${c.peakSpeed}/s at ${c.peakAt.toFixed(2)}s; stops ${JSON.stringify(c.stops)}; overshoots ${JSON.stringify(c.overshoots)}`);
Skia.Image.load(g.path);   // the graph itself, to look at
```

`stops` lists middle keys where the move halts though it carries on in the same direction (a `'halt'` that should be
`'clamped'`); `overshoots` lists segments that go past their keys (`'auto'` on resting keys); `holds` lists the jumps.
Look at the image too: bunched frame dots are the ease, and the speed curve's shape is the move's character.

### 5c. The path is not the timing

**A natural move travels on an arc** (Lasseter §2.8). When one spline carries both the path and the timing, a fast move
flattens its arc into a straight line — the faster, the flatter — and the life goes out of it. Colette and Shaw make the
same split, *spatial* against *temporal* interpolation (ch. 2). Here the split is **`Motion.nodes.composite(x, y)`**:
two keyed reals made one point, each with its own eases. For a throw or a hop, key the travel across on `x` and the rise
and fall on `y` — `'halt'` at the top, where the rise turns into the fall — and the path is an arc whatever the speed.
Where a single vector node is simpler, **put a key at the arc's high point** with `'clamped'`, which passes through it
smoothly.

### 5d. Squash and stretch keep their volume

Living and soft things change shape as they move; the rule that makes it read is that **the volume stays the same**
(Lasseter §2.1): a squash that does not widen looks like shrinking, a stretch that does not narrow looks like growing.
With `scale`, stretch along the direction of travel by `s` and narrow across it by `1/s`, turning the layer to its path
first. The more it squashes, the softer the material reads; a rigid object squashes only by folding at its joints, as
Luxo Jr. does.

**Stretch also hides strobing.** In a fast move, successive positions stop overlapping and the eye sees separate copies
(§2.1). Motion blur is the realistic cure; stretching the shape along its path until its positions nearly overlap is the
other. **Never on text**: a stretched or blurred figure is an unreadable one.

### 5e. Anticipation, sized to the action

Every action has three parts: the preparation, the action, and its end (§2.3). The preparation tells the audience what
is about to happen and where to look. **Its size follows the action's speed**: a very fast action needs a larger
anticipation, or the audience misses it — Wally B.'s zip-off lasts three or four frames, after a wind-up long enough that
no one doubts what is coming — and a slow action needs almost none. A small pull back before a figure lands, or a beat
of stillness before a reveal, is anticipation in motion graphics.

### 5f. Staging: one idea at a time

**Present one idea at a time, in the strongest and simplest way, before the next** (§2.4). Where several things move at
once the eye does not know where to look, and the main idea is upstaged. Two facts decide where the eye goes: **in a
still scene it goes to what moves; in a busy scene, to what is still.** In Luxo Jr. only one lamp did anything important
at a time, and the other's motion was turned down to let it. And **stage in silhouette**: an action that would read only
in front of the body is moved to the side, where its outline carries it.

### 5g. Follow-through, overlap, and the order of keying

**Overlap, do not queue.** Beats that abut exactly read as a list of events. Start the next one slightly before the
last has settled — `'-=200'` on a timeline — and they read as one movement. This is the single change that most
improves a mechanical-looking score. Lasseter's account of why (§2.5): a character who knows what he is doing next does
not finish one action before thinking of the next — the door is still closing as the hand reaches for the key — so the
actions of one intention overlap.

**Loose parts follow, by their weight** (§2.5). A move begins at its lead — the hips in a walk, the wrist in a gesture,
the eyes before the head — and the rest follows. Appendages drag behind and keep going after the lead stops; **the
heavier they are, the further they drag and the longer they take to settle.** Give each part its own lag and settle, and
the material reads without a word.

**Stagger anything that repeats.** Four bars arriving together are one event; arriving 90 ms apart they are a sequence
the eye can follow. `each` between 60 and 120 ms is usually right — below that it reads as simultaneous, above it as four
separate events. Colette and Shaw call the composition version *duplicate and offset*: build one passage, then reuse it
later (ch. 1) — a group's `timeOffset` here.

**Key from the trunk down** (§2.6). Lasseter found that keying whole poses at once in a hierarchy made the inbetweens
unpredictable; instead he keyed one transformation at a time, starting at the root and working out along the branches,
with fewer keys on each. Here: key the scene group's move first, then the group inside it, then the parts.

### 5h. Secondary action stays secondary

A secondary action results from the primary one — a cord rippling behind a hop, feet dragging behind a body — and adds
life **only while it stays subordinate** (§2.10). One that competes is the wrong choice or badly staged. A change of
facial expression during a large body move is not seen at all: **put it before the move or after**, and stage it where it
can be read.

### 5i. Appeal, and no twins

Appeal is what the eye wants to stay on: clear design, simple shapes, a readable move (§2.11). Its commonest killer in a
pose is **twins** — both arms or both legs doing the same thing, one side of a face mirroring the other — which reads as
stiff. Vary each side: one arm leads, one shoulder rises, the head turns off the axis.

> [!TIP]
> **A hold is a beat.** Motion that never rests gives the reader nowhere to look. Leave gaps — a `'+=400'` before a reveal is not wasted time, it is the pause that makes the reveal land.

### 5j. A motion identity: one ease, three durations, one entrance

A film whose every move has its own ease and its own length has no character, and a set of films that each found their
own has no family. Decide three things before keying, and hold to them:

- **One signature ease** for most moves: the house curve, the one the kit's `Motion.graph` shows (Manual 30 §3). Depart
  from it on purpose, for a move that means something else (§5a).
- **A palette of three durations**: quick, standard and slow, in frames, so a move's length is a choice among three
  rather than whatever a beat happened to leave. The pace evidence decides where the palette sits; §5a decides which of
  the three a move takes.
- **One entrance pattern**: how things arrive in this film (rising from below, a wipe from the left, focus pulled in),
  the same for every element of a kind.

Write the three in `direction.md` with the kit, and in a set they are fixed parts (Manual 30 §3), held by every film.
This is how brand systems carry motion. The way of organising it into three follows LottieFiles' *Motion Design Skill*
(MIT, 2025); its millisecond tables are for interface motion and are not adopted here.

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

// Seek, capture, repeat. Nothing here depends on the order the frames are taken in. Step at the rate
// the film is saved at (40 ms is 25 fps), or it plays at the wrong speed.
Motion.clear();
for (let t = 0; t <= tl.duration; t += 40) { tl.seek(t); Motion.frame(paper); }
log(`captured ${Motion.count} frames`);

Motion.sheet('artifacts/manual25-score-sheet.png', { count: 6, cols: 6, scale: 0.4, fps: 25 });
Motion.save('artifacts/manual25-score.webp', { fps: 25 });

tl.seek(tl.duration);      // leave the scene at its final state for the still render
paper;
```

## 9. When the Parameters Are the Score (spike)

The score above drives an element's attributes from outside. `Motion.composition()` puts the motion
*inside* the parameters: a circle's `origin` is not a point that a tween moves, it **is** a node — a
list of keys, or a formula such as `offset + slope × t`, or a formula whose terms are keys. The model
is Synfig's, and the craft follows from it:

- **Key what you mean, compute what follows.** A drift is a `Motion.nodes.linear(type, slope, offset)` —
  the rate first, so a record turning 60° a second from 0° is `linear('angle', 60, 0)`, and
  `linear('angle', 0, 60)` is a record standing still at 60° — a sway is a `Motion.nodes.sine(...)`, a
  fall is keys. Keying a drift frame by frame is a hundred numbers saying what one rate says.
- **Choose the ease per key, not per piece.** `halt` is a stop, `constant` a hold, `clamped` passes
  through without overshooting, `auto` may overshoot on purpose. The default is `clamped`, because an
  unasked-for overshoot reads as a wobble.
- **Link rather than copy.** One node passed to two options keeps them together through every later
  change; two equal nodes drift apart the first time one is edited.
- **Reuse with a group's clock.** A group's `timeOffset` and `timeDilation` play the same passage later
  or faster without re-keying it — the follow-through that trails an action by a few frames.
- **Lay a key on a formula with `add`.** `Motion.nodes.add('vector', drift, bob)` is a drift with a bob on
  it; `Motion.nodes.scale('real', swing, 0.5)` is the same swing at half the size. Neither needs a group.
- **Blend between values with `Motion.nodes.weightedAverage(type, [[value, weight], ...])`.** Key the weights and the
  value travels between the others by them: a prop handed from one hand's position to the other's, a colour drifting
  between two lights. It is Synfig's own weighted average and is written to `.sif` as one.
- **Deliver vector motion as SVG.** `comp.toSvg()` and `comp.saveSvg(path)` write the composition as SMIL
  animation that plays in a browser. Keys are written exactly; formulas, colours and shapes are sampled at
  the frame rate. Keep what must be smooth between frames as keys, and draw a growing bar with
  `comp.rectangle(...)`: its keyed corner stays exact, where the same keys inside a region's points are sampled.
- **Key the pose, construct the figure.** A layer made with `comp.drawn((ctx, v, t) => ..., { values })`
  is drawn by the toolkit every frame, from values the nodes give. Key the line of action's `turnDeg`
  and the arm angles, and `Drawing.createMannequinFigure` builds the body at each frame; Manual 28's
  checks then run on any frame you render. Write the function from `v` and `t` alone: frames come in
  any order, so nothing it remembers between calls can be trusted.
- **Cut a drawing into a puppet rather than redrawing it.** `comp.cutout(picture, outline, { origin, offset, angle })`
  is one piece of a picture as a group that turns about its joint; put the child piece inside the parent's
  group and it follows. One drawn sheet — a cutout cell, a character render — becomes a limb that swings,
  with the same pixels in every frame. Overlap the pieces a little at each joint, as cut paper does, so a
  bend never opens a gap. The cut is an eraser (`blend: 'alphaOver'`), and like every blend it reaches only
  its own group, which is why a piece is always a group.
- **Past two pieces, rig it with bones.** `comp.bone({ from, to, parent, turn })` is a joint you key;
  draw every piece in the rest pose and bind it — `comp.cutout(..., { bone })`, `comp.group({ bone })`, a
  point as `Motion.nodes.boneLink(bone, [x, y])` — and it follows. Key the turns, not the pieces: one
  shoulder turn moves the whole arm, and a pose is a handful of angles rather than a dozen transforms.
  A shape that must bend at a joint, a sleeve, takes weighted points (`Motion.nodes.boneInfluence` or a
  spline point's `bone: [[upper, 1], [lower, 1]]`); a rigid piece takes one bone. Check the rig with
  `comp.skeleton()` over a contact sheet, and measure it with `bone.at(t)` — every bone is in `comp.bones` —
  rather than by eye. The skeleton is a guide: take it out before the deliverable.
- **When the drawing must bend rather than hinge, deform it.** Cut pieces tear where one drawn shape crosses
  a joint — a raglan sleeve, a coat hem over a stepping leg. Put the whole picture in a group with
  `group.skeletonDeformation()` over it and the bones bend it as one sheet. Each bone's `width` is its reach:
  set it to cover the artwork it carries, because anything no bone reaches is dropped.
- **For a figure, let `comp.rigFromDrawing(image, Character.detect(image), { poses })` do all of that.** It
  makes the bones from the drawing's landmarks, sizes each reach, and bends the whole picture; you give whole
  poses as keys — `{ time, rightUpperArm: 55, head: 12 }` — and a bone a key leaves out is at rest. Check
  `rig.reach` is 1 and `rig.unsure` is empty before trusting it. It moves a figure within its view; a turn or a
  foreshortened limb is a new key drawing, from the 3D route.
- **For a face, key its channels: `comp.face({ origin, height, yaw, channels, look })`.** The face is rebuilt
  every frame from numbers, so a performance is curves on the vocabulary the head already speaks: Action Units
  (`AU12` a smile, `AU25` the laugh's rising upper lip), expression weights (`joy: 0.6`), and Hamm's eye wheel
  (`upperLid` 3 → 5 → 3 is a blink, `browTop` 1 a raised brow), with `near.` or `far.` for one side. Build a
  character's poses once, as a library in `poses`, and key their weights: a performance is then a handful of
  blend curves (`smile`, `surprised`) rather than a dozen channels, and the same library serves every shot. Treatments and
  shapes are identity: hold them in `look` and `character`, so the character stays the same person. Two timing
  rules from the corpus are worth keying in: the eyes follow the mouth by about three frames in a smile (Essa),
  and the parts should not all stop on one frame (§11). `face.headAt(t)` places a balloon or a prop on the head
  at any time. It is not exported to `.sif` or SVG; capture it.
- **Key an expression by when it lands: `Motion.nodes.envelope({ at: 0.4 })`.** One hit is the whole curve of an
  action as Essa measured it on real faces: it rises *accelerating* into the peak at `at`, falls fast, then
  relaxes slowly to rest. That is the opposite of the ease-out a hand-keyed rise usually gets, and it is why a
  face keyed with `halt` keys looks like it is dialled up rather than performed. `hold` keeps the peak; a second hit in the same
  envelope starts from wherever the first has got to, so a re-take never jumps. `{ from: 3 }` lets it drive an
  eye-wheel setting from its normal. The three-frame lag is then one number: `AU6: n.envelope({ at: 0.55 + 3 / 24 })`.
  `sharpness: 0` is the linear ramp, for comparison.
- **Give a living face blinks: `AU45: Motion.nodes.blinks({ seed: 2 })`.** A face that never blinks reads as a
  mask within a few seconds, and one that blinks on a beat reads as a machine; `blinks` spaces them irregularly,
  closes faster than it opens, and now and then doubles one. Put the ones that matter where they mean something,
  `at: [1.6]` on a change of thought or a turn of the head, and keep a stare clear with `avoid: [[2, 4]]`. A shut
  eye is drawn shut, whatever the eye was doing, so a blink over a laugh or a surprise needs nothing else.
  `near.AU45` is a wink. Give two characters different seeds, or they blink together.
- **Loop an idle rather than keying it to the end: `Motion.nodes.timeLoop(breath, 3)`.** Key one breath, one sway,
  one bob, and the loop plays it for the length of the shot; `localTime` lines it up, and a negative duration
  plays it backwards. Loop the idle and leave the blinks unlooped: a loop repeats exactly, and blinks must not.
- **Let the loose parts follow through on their own: `Motion.nodes.dynamic([0, -80], { origin: crown })`.** Key the
  head; an antenna, an earring, a ponytail's end, a tail's tip hung off it on a spring lags as the head starts, swings
  past as it stops and settles after, which is follow-through and overlapping action without a key of its own. The
  defaults are soft and keep wobbling; raise `spring` and `torsion` for a stiffer part, `damping` and `friction` for
  one that settles sooner, and add `force: [0, 400]` for something heavy that hangs. Base it on the node the head
  moves by, `n.add('vector', origin, offset)`, so it rides the same motion. Any frame renders on its own, as every
  other node does.
- **Swap a drawing rather than morph it: `comp.switch({ active: [[0, 'close'], [1.5, 'wide']] })`.** Some changes are
  replacements, not motions — a mouth chart's shapes, a hand's poses, a face drawn with lashes and toned irises for the
  close-up and plainly for the wide shot, where the same marks would clump into noise. Draw each version once inside
  the switch, name it with `desc`, and key which shows; key the camera's cut on the same times. Drive every version
  from the same channel nodes, so the performance runs on unbroken while only the drawing changes.
- **Animate type with one number, not a layer per letter: `comp.text({ ..., animators: [{ properties, selector }] })`.**
  Kinetic type is overlap and cascade: letters doing the same thing a beat apart, the way a line of dancers does. An
  animator states once what a letter does when fully selected — rise from 80 units below, turn 20°, fade in — and a
  range selector's `offset`, keyed from −100 to 100 with `shape: 'rampUp'`, hands that motion from letter to letter,
  left to right — at −100 every letter is fully selected and waits in its starting pose, at 100 none is and all have
  landed. Key it the other way to send them out. That keeps the timing in one curve the eye reads as a single gesture; per-letter keys drift apart
  and read as noise. Ease the selector (`easeLow`) rather than the key, so each letter settles. Base it on `words` for
  a line that should land a word at a time, and let the title hold still once it lands: the read happens after the
  motion stops, so give it at least as long as the arrival took. Keep the text's handle —
  `const title = comp.text(...)` — and check it rather than guessing: `title.boundsAt(t, { frame: true })` is its ink as drawn,
  animators and all, in the frame's pixels through any group it sits in, which is the title-safe check, and `title.lettersAt(t)` gives each letter's box for placing a
  thing on a word. How long it holds is `title.settledAt(arrival)`: when the letters stop moving and for how many
  seconds they stay still, the number a reading-time check compares against the words.
- **Pull focus rather than cut: `group.blur({ type: 'gaussian', size })` with `size` keyed.** A blur that eases from
  soft to sharp directs the eye the way a camera's focus does — to the title as it resolves, to the second subject as
  the first goes soft — and a cut has no way to say "look here next". It blurs only what is below it in its own group,
  so group what should soften together and leave the rest sharp. Ease it with `halt` at both ends so it settles rather
  than snaps; a focus pull that keeps moving reads as a fault. Keep blur for transitions and depth, not decoration: a
  composition left blurred is a composition the eye cannot rest on. The default `'gaussian'` is the smooth blur and the
  cheapest, about the same cost at any size; `'fastGaussian'` costs half as much again and turns a small bright shape
  square. Key it off when the pull is done, and a blur doing nothing costs nothing.
- **Bring a vector drawing into motion as its own path data: `comp.region({ path: d })`.** A poster's shapes are
  already drawn; re-drawing them by hand loses the curves the designer chose. Paste each `<path>`'s `d` into a group
  whose `offset` and `scale` carry the drawing's coordinates into the frame, and the holes and corners come with it; a
  `<rect rx>` is `comp.rectangle({ point1, point2, radius: rx })`.
  When one point should move — a lid that lifts, a flag that flutters — take the same contours as values with
  `Motion.pathPoints(d)`, key that point, and give the rest back unchanged. A shape that exists only as points —
  a sampled curve, a traced outline, landmarks — becomes a few smooth spline points with `Motion.fitPoints(points,
  { closed: true })`, corners kept: few points are what make a shape keyable, and forty points that each need a key
  are forty chances for the shape to wobble. A drawn gradient is a layer too:
  `comp.linearGradient({ point1, point2, stops })` or `comp.radialGradient({ origin, radius, stops })`, in a group with
  the shape it fills and `blend: 'onto'`, and a sheen is its two points keyed across the shape.
- **Move the camera through the poster, not the poster past the camera: `comp.camera()`, layers at depths.** Give a
  poster's layers `z` — the backdrop far, the object nearer, the type at 0 — and a camera move turns a flat design into
  a space. `keepSize: true` on each plane keeps the poster looking exactly as designed until the camera moves; when
  the move ends on the design rather than starting from it, `keepSize: <second>` makes it exact at that second. Choose
  the move for what it says. A **push-in** (`distance` keyed down) closes on something and raises the
  stakes: near layers grow faster than far ones, which is what makes it read as moving rather than enlarging. A
  **zoom** (`zoom` keyed) magnifies everything alike and reads as a looking-glass, flat and observed; use it when the
  picture should feel watched, not entered. A **pan** (`target` keyed) slides near layers past far ones, and that
  parallax is depth the eye believes without a single shaded form. A camera move is a beat like any other: ease it in
  and out (`halt` at both ends) and let it settle before the type lands, because a title read while the camera still
  drifts is read badly. Keep each layer at its own depth and in front of the camera; layers that cross are not cut
  against each other.
- **Draw a line on rather than fading it in: `comp.outline({ ..., trimEnd })` keyed 0 → 1.** A rule under a title, a
  route on a map, the groove of a record: a line that draws itself has a direction and a moment of arrival, where one
  that fades has neither. Ease it as you would a move — fast out, settling into the end — and let it finish before the
  next thing starts.
- **Change beats with a wipe only when the change means something: `comp.wipe({ type, completion })`.** The hard cut is
  the default and usually right. A wipe says "and meanwhile" or "elsewhere", an iris closing on a face (`type: 'iris',
  invert: true`) says "remember this one", a radial sweep reads as a clock. Pick one kind for the film and keep it.
  A linear wipe's key is the time its edge takes to cross the frame, so on a shape filling only a strip, give it
  `over` (the shape's box), or the edge is on the shape for only a fraction of the key and a slow reveal reads as a snap.
- **Take the pace from the audience, not from the feel of a name.** How long things hold is a choice the
  audience's evidence can make, and Qloo describes it: an audience's distinctive films and shows carry its pace
  tags, "Fast paced", "Action packed", "Kinetic" against "Slow burn", "Deliberate pacing". The profile you
  fetched (`Taste.find('…')`, or any of `Taste.results`) counts them with `tasteResult.pace()`. An audience
  leaning fast motivates more changes on the beat; one leaning slow, longer holds. One evenly split gives no
  lean. With two audiences, `Taste.comparePace(a, b)` says whether they differ at all (Fisher's exact test);
  with three or more, `Taste.comparePaceAll([a, b, c])` tests every pair and adjusts for how many there are, so
  a difference it reports is not one that turns up by chance in a large set. Audiences it groups together may
  share their holds. When they do not differ, the films should not pretend to; put the difference where the
  evidence does differ.
  Measured once: two audiences whose fans' music ran at the same ~100 BPM still differed in their screen
  taste's pace, so **the beat and the holds are separate questions**. And reading an artist's name as
  "steady" gave two different audiences the same pace.
  **Without `Taste`** (every workflow but `motion_qloo`) no audience's pace is measured: take it once for the film from
  the brief's words or from research on the subject (its genre, how it moves, cited), else mark it assumed, and give
  every audience's film the same pace.
- **Grade the film once, at the top: `comp.colorCorrect({ hue, saturation, exposure, ... })` over everything.** One
  correction is one light, and it holds the frames together where per-layer tweaks pull them apart. The same film graded
  warm for one audience and cool for another is a choice the evidence can make; a keyed `exposure` is a flash on a cut.
  A grade moves every colour the poster chose, so see where it takes the important ones before rendering the film:
  `Motion.grade(sameOptions).apply('#C8205A')` is the colour the film will show, and it is the one to check contrast on.
- **Step time on purpose, not by accident: `comp.posterizeTime({ fps: 12 })`.** Motion on twos reads as drawn and
  deliberate; smooth motion reads as machine. Put the drawn layers under it and the camera or the type above, and the
  film reads as hand-made inside a modern frame.
- **Tip, turn and lay type on a surface with a corner pin: `comp.warp({ from, to })`.** Four keyed corners put a card
  on a table, swing a poster into place, or set a line on a wall in perspective; the pinned layers stay vectors, so the
  type stays sharp.
- **Lift a shape off its ground with a shadow: `comp.shadow({ offset, size })`, in a group with what casts it.** A soft
  shadow, offset down and away from the light, separates a layer from the one beneath; keying `offset` as a layer
  rises makes it float. `invert: true, blend: 'onto'` cuts the shape into its ground instead. Use one light for the
  whole frame: shadows cast in different directions make a frame of stickers, not a scene.
- **Check what the eye must not be given, not only what it must read.** More than three flashes in any second can
  trigger seizures (WCAG 2.3.1); `comp.flashes()` measures the whole film in about a second and lists every
  transition, so a run of hard cuts shows up as the number it is. Type must also stand off its ground —
  `Motion.contrast(text, ground)` at 4.5 or more, 3 for display size — and a ground that moves under the type is
  checked at its worst frame, not its first.
- **A screen with nothing on it is time the viewer spends waiting.** A fade to the ground, a pause, a fade up between
  every scene reads as a film that has stopped; `comp.blank()` finds every stretch where the frame is only its ground
  and fails one longer than a second. Cut from scene to scene, or let the next scene's first element arrive as the
  last one leaves.
- **Make a line of dialogue from its text: `Motion.speech('The cat in the hat.', { start: 0.4 })`.** Which sounds a
  sentence has is the dictionary's business, and mouth charts are coarse enough that accents and weak forms mostly
  land on the same mouth; only the timing has to be chosen, and with no recording to match, natural timing is correct
  timing. Key a switch of drawn mouths with `line.mouths`, or let a face speak with its own mouth:
  `comp.face({ poses: line.poses(), channels: line.weights() })`, Blair's ten mouths as the face's own units. Watch the
  closures: `M`, `B` and `P` must close the lips on screen, or the word reads wrong, and the track keeps them however
  short. Draw the mouth a frame before its sound (`lead`) and the eye reads them as one. With the speech backend
  installed, numbers are spelled out and words no dictionary has are guessed; read `line.guessed` before trusting it,
  and give a character's name its sounds with `pronounce`, once, in every line it is spoken. Use `line.wordAt(t)` for a
  caption that follows.
- **With a voice, time the line to it: `Motion.speech(text, { audio: 'line.wav' })`.** Rules are right on average and
  wrong in performance: a speaker lingers, rushes and pauses where the punctuation does not say to, and rule timing
  drifted over half a second within one measured line. The aligner finds each word in the recording, so the mouth
  lands with the sound; the line's text still decides which mouths. Check `line.words[i].confidence` for a word it was
  unsure of, and keep the mouth a frame ahead of the sound (`lead`), which reads as one with it. Deliver it with the
  voice by passing the line itself, `comp.saveVideo('output.mp4', { audio: line })`: the recording plays from the line's
  `start`, so the picture and the sound are placed once, in one place, and cannot drift apart.
- **Or let a performer give the motion: `{ follow: Character.track('Jumping Jacks') }`.** The recorded clips
  that pose the 3D body drive a drawing too, each bone turned to point where the performer's part points on the
  page. It is only as good as the clip is flat: read `track.warnings` first, and choose a clip that moves across
  the page (jumping jacks, a nod, a shake-off, a sidestep) rather than toward it (a walk, a punch).

```javascript
const n = Motion.nodes;
const comp = Motion.composition({ width: 480, height: 270, fps: 24, duration: 2 });
comp.fill({ color: '#f2efe8' });

// The anticipation, the drop and the hold are keys; the drift is a rate.
const y = n.animated('real', [
    { time: 0,    value: 120, ease: 'halt' },
    { time: 0.25, value: 100, ease: 'halt' },       // a small rise before the fall
    { time: 0.8,  value: 230, ease: 'linear' },
    { time: 2,    value: 230, ease: 'constant' }]);
const ball = n.composite(n.linear('real', 140, 60), y);
comp.circle({ origin: ball, radius: 16, color: '#1f6f8b' });

// One colour node in two layers is a link: grade it once and both change.
const ink = n.constant('color', '#15151a');
comp.outline({ width: 3, color: ink, points: [{ point: [40, 250] }, { point: [440, 250] }] });

// A second ball, the same passage played a quarter-second late: follow-through without new keys.
const late = comp.group({ timeOffset: -0.25, offset: [0, 0] });
late.circle({ origin: ball, radius: 9, color: ink });

log(`${comp.layerCount} layers, ${comp.frameCount} frames; the ball is at ${JSON.stringify(ball.at(1))} at 1 s`);
comp.capture({ fps: 6 });
Motion.sheet('artifacts/manual25-graph-sheet.png', { count: 7, cols: 7, scale: 0.4 });   // captured frames carry their own times
comp.saveSif('artifacts/manual25-graph.sif');     // the same graph, readable by Synfig
log(`${comp.toSif().length} characters of .sif`);
comp.render(1);
```

It still renders by seeking, so §4 holds unchanged: capture with `comp.capture(...)`, look at the sheet,
not at a movie. See `polson://sdk/core/Motion` for the calls.

## 10. A Character Beat, Keyed and Checked

> **Implemented by**: `Motion.nodes.animated(...)`, `node.at(t)`, `comp.drawn(...)`, `Drawing.createMannequinFigure`, `Drawing.reachLeg`, `Motion.sheet({ indices })`.

§9 lists the routes; this is one of them taken end to end: a figure throwing a ball, built so that every claim about the motion is a measurement. The `animation` workflow runs this method stage by stage.

**Count in frames, not seconds.** A beat at 2.4 s on a 24 fps film is frame 57.6, which no capture holds, and a sheet asked for it refuses. Put every beat on a frame (`frame: 18`) and divide by the rate where a time is wanted. Animators count this way for the same reason: a frame is the unit the eye receives.

**A pose is a row of numbers, and each number is keyed.** Write each key pose as a flat object (`turn`, `lean`, `rShoulder`, ...), then make one `animated` node per field with a small helper. The whole pose at any time is then a lookup, `node.at(t)` for each field, so a check never has to render to know where the arm was.

**The figure is a pure function of those numbers.** `figureAt(v)` builds the mannequin from `v` alone, which is what `comp.drawn` requires (frames arrive in any order) and what lets the checks call the same function the drawing does. Two things belong inside it rather than in the keys:

- **Contacts are solved every frame, not keyed.** A planted foot is a fixed point, and `Drawing.reachLeg` puts the foot on it at whatever the torso is doing. Keyed leg angles would drift off the mark between keys, because interpolated angles do not keep an end point still. `miss` says when a pose asks for more leg than there is.
- **Anything that leaves the figure is a function of `t`.** The ball rides the palm until the release frame, then flies on a parabola from where the palm was. It is not keyed at all.

> [!IMPORTANT]
> **Angles interpolate as plain numbers, so the winding is yours to choose.** Between a key at `-130` and one at `10` the arm passes through `-60`, up and over: an overhand throw. Write the first key as `230`, the same direction on the page, and the arm passes through `120`, down and under. Both render; only one is the throw you meant. Read the angles between two keys before trusting the move, and when a limb turns the wrong way round, add or subtract 360 on one key.

**Ease per key, by what the key is.** `halt` on an extreme gives slow-in and slow-out on both sides of it. `linear` on a key the motion passes through at speed (the release) keeps it fast. Two identical keys with `halt` hold perfectly still between them.

**Check the motion off the nodes.** Each of these is arithmetic on `figureAt(valuesAt(t))`, and each names the received practice it measures:

| Check | Measured as |
| :--- | :--- |
| anticipation | the wind-up moves the lean the opposite way to the throw |
| arcs | the hand's path through the throw bows off its chord by more than a tenth |
| slow in, slow out | the first and last per-frame steps of a move are under half its largest |
| contact | the largest `miss` of either foot over every frame is under a pixel |
| hold | no field drifts more than half a degree across the hold |
| silhouette at the extreme | the throwing arm is 80% clear of the torso at the wind-up (Manual 28 §7) |

**Look at the beats, not at the film.** Capture every frame, then `Motion.sheet` with `indices` set to the beat frames: one cell per beat, labelled with its film frame and time, which is the drawing to hold against the sentences. A second sheet of evenly spaced frames shows the spacing. Call `Motion.clear()` before capturing again in the same script, or the frames add up. What you decide by looking at a cell is still a check, marked `{ judged: true }` so it cannot be mistaken for a measurement.

**Ink the head.** `Drawing.drawGestureContour` lines the body and leaves the head to you; without it the figure reads as a stick at sheet size. The figure's `head` carries `center`, `rx`, `ry` and `angleDeg`.

**Say whether it loops.** `Motion.save` writes the loop count into the file: `loop: false` plays once and holds the last frame, which is what an action ending in a hold wants. The default plays forever.

```javascript
// A throw: anticipation, action, follow-through, hold. One figure, keyed poses, built every frame.
const n = Motion.nodes;
const W = 640, H = 400, FPS = 24, FRAMES = 58, DURATION = (FRAMES - 1) / FPS;   // frames 0..57

// Beats, in frames, each with the sentence it has to say.
const BEATS = [
    { name: 'ready',   frame: 0,  says: 'she stands, weight even, ball in her right hand' },
    { name: 'wind-up', frame: 11, says: 'she leans back and cocks the arm behind her head' },
    { name: 'throw',   frame: 18, says: 'she drives forward and whips the arm through' },
    { name: 'follow',  frame: 25, says: 'the arm carries on down and across her body' },
    { name: 'hold',    frame: 43, says: 'she holds, watching the ball go' },
    { name: 'settle',  frame: 57, says: 'she straightens a little' }
];

// One pose per beat, as flat numbers so each can be keyed.
const KEYS = [
    { frame: 0,  ease: 'halt',   pose: { turn: 0,   lean: 0,   rShoulder: 100,  rElbow: -30, lShoulder: 80,  lElbow: 20 } },
    { frame: 11, ease: 'halt',   pose: { turn: -28, lean: -12, rShoulder: -130, rElbow: -60, lShoulder: -15, lElbow: 10 } },
    { frame: 18, ease: 'linear', pose: { turn: 32,  lean: 16,  rShoulder: 10,   rElbow: 5,   lShoulder: 150, lElbow: -40 } },
    { frame: 25, ease: 'halt',   pose: { turn: 36,  lean: 20,  rShoulder: 120,  rElbow: 20,  lShoulder: 160, lElbow: -50 } },
    { frame: 43, ease: 'halt',   pose: { turn: 36,  lean: 20,  rShoulder: 120,  rElbow: 20,  lShoulder: 160, lElbow: -50 } },
    { frame: 57, ease: 'halt',   pose: { turn: 20,  lean: 10,  rShoulder: 105,  rElbow: 10,  lShoulder: 120, lElbow: -30 } }
];

// One node per field, so the whole pose is keyed and any time is a lookup.
function keyed(keys) {
    const out = {};
    for (const field of Object.keys(keys[0].pose))
        out[field] = n.animated('real', keys.map(k => ({ time: k.frame / FPS, value: k.pose[field], ease: k.ease })));
    return out;
}
const POSE = keyed(KEYS);
const valuesAt = t => Object.fromEntries(Object.entries(POSE).map(([k, node]) => [k, node.at(t)]));

// The feet stand on fixed marks and are solved onto them every frame.
const FLOOR = 360, FOOT_BACK = { x: 245, y: FLOOR }, FOOT_FRONT = { x: 300, y: FLOOR };

// The figure at one set of values. Pure, so frames can be built in any order.
function figureAt(v) {
    const torso = { lineOfAction: { shape: 'C', turnDeg: v.turn, leanDeg: v.lean },
                    rightArm: { shoulderDeg: v.rShoulder, elbowDeg: v.rElbow },
                    leftArm: { shoulderDeg: v.lShoulder, elbowDeg: v.lElbow } };
    const first = Drawing.createMannequinFigure(270, 70, 300, { pose: torso });
    const back = Drawing.reachLeg(first, 'left', FOOT_BACK, { to: 'foot', bend: 'right' });
    const front = Drawing.reachLeg(first, 'right', FOOT_FRONT, { to: 'foot', bend: 'right' });
    const fig = Drawing.createMannequinFigure(270, 70, 300, { pose: { ...torso, leftLeg: back.pose, rightLeg: front.pose } });
    return { fig, legs: [back, front] };
}

// The ball rides the palm until the release, then flies from where the palm was. A function of t, not a key.
const RELEASE = 18 / FPS;
const releaseAt = figureAt(valuesAt(RELEASE)).fig.rightArm.hand;
const ballAt = (t, hand) => {
    if (t <= RELEASE) return hand;
    const s = t - RELEASE;
    return { x: releaseAt.x + 520 * s, y: releaseAt.y - 260 * s + 300 * s * s };
};

// The checks, off the nodes rather than off the picture.
const at = t => valuesAt(t);
const hand = f => figureAt(at(f / FPS)).fig.rightArm.hand;

const start = at(0), wind = at(11 / FPS), act = at(18 / FPS);
Stage.check('the wind-up moves against the throw', Math.sign(wind.lean - start.lean) !== Math.sign(act.lean - start.lean),
    `lean ${start.lean.toFixed(1)} -> ${wind.lean.toFixed(1)} -> ${act.lean.toFixed(1)}`);

const path = [];
for (let f = 11; f <= 18; f++) path.push(hand(f));
const a = path[0], b = path[path.length - 1], chord = Math.hypot(b.x - a.x, b.y - a.y);
const bow = Math.max(...path.map(p => Math.abs((b.x - a.x) * (a.y - p.y) - (a.x - p.x) * (b.y - a.y)) / chord));
Stage.check('the throwing hand travels on an arc', bow / chord > 0.1, `bow ${(bow / chord).toFixed(2)} of a ${chord.toFixed(0)}px chord`);

const steps = [];
for (let f = 0; f < 11; f++) { const p = hand(f), q = hand(f + 1); steps.push(Math.hypot(q.x - p.x, q.y - p.y)); }
const top = Math.max(...steps), first = steps[0], last = steps[steps.length - 1];
Stage.check('the wind-up eases in and out', first < 0.5 * top && last < 0.5 * top,
    `first ${first.toFixed(1)}, largest ${top.toFixed(1)}, last ${last.toFixed(1)} px a frame`);

let worst = 0, where = 0;
for (let f = 0; f < FRAMES; f++) for (const leg of figureAt(at(f / FPS)).legs) if (leg.miss > worst) { worst = leg.miss; where = f; }
Stage.check('the feet stay planted', worst < 1, `largest miss ${worst.toFixed(2)}px, at frame ${where}`);

const h0 = at(25 / FPS), h1 = at(43 / FPS);
const drift = Math.max(...Object.keys(h0).map(k => Math.abs(h0[k] - h1[k])));
Stage.check('the hold holds', drift < 0.5, `largest drift ${drift.toFixed(2)} deg over frames 25-43`);

const geo = Drawing.createFigureGeometry(figureAt(wind).fig);
const clear = geo.groups.rightArm.subtract(geo.groups.torso).area / geo.groups.rightArm.area;
Stage.check('the wind-up arm reads in silhouette', clear > 0.8, `${(clear * 100).toFixed(0)}% clear of the torso`);

// The film: the composition draws the figure each frame from the keyed values.
const comp = Motion.composition({ width: W, height: H, fps: FPS, duration: DURATION });
comp.fill({ color: '#f2efe8' });
comp.outline({ width: 3, color: '#15151a', points: [[40, FLOOR], [600, FLOOR]] });
comp.drawn((ctx, v, t) => {
    const { fig } = figureAt(v);
    ctx.fillStyle = '#d9d2c3';
    ctx.fill(Drawing.createFigureGeometry(fig).silhouette);
    Drawing.drawGestureContour(ctx, fig, { strokeColor: '#15151a', stretchWidth: 2.4, squashWidth: 1.6 });
    const h = fig.head;                                   // the contour leaves the head to us
    ctx.strokeStyle = '#15151a'; ctx.lineWidth = 1.8;
    ctx.beginPath(); ctx.ellipse(h.center.x, h.center.y, h.rx, h.ry, h.angleDeg * Math.PI / 180, 0, Math.PI * 2); ctx.stroke();
    const ball = ballAt(t, fig.rightArm.hand);
    ctx.fillStyle = '#c9553d';
    ctx.beginPath(); ctx.arc(ball.x, ball.y, 9, 0, Math.PI * 2); ctx.fill();
}, { values: POSE, desc: 'thrower' });

// One cell per beat is the drawing to hold against the sentences; the film is what ships.
comp.capture({ fps: FPS });
const sheet = Motion.sheet('artifacts/manual25-throw-beats.png', { indices: BEATS.map(b => b.frame), cols: 6, scale: 0.4 });

// A verdict read off the sheet is recorded as one. The detail is what was seen, not a number.
Stage.check('the wind-up cell reads as a coil, not a wave', true, 'arm behind the head, body bowed back', { judged: true });

const film = Motion.save('artifacts/manual25-throw.webp', { fps: FPS, loop: false });   // plays once, holds the settle
log(`${film.frames} frames, ${sheet.cells} of ${sheet.held} on the beat sheet, loop ${film.loop}`);
comp.render(11 / FPS);
```

The route is the mannequin's, so the deliverable is raster: `comp.toSvg()` refuses a `drawn` layer, because SMIL cannot run a script. A piece that has to ship as SVG is keyed on Snap elements or on layers the model draws itself (§8, §9), and gives up the constructed figure to do it.

## 11. Phrasing, Texture and the Extreme–Extreme

> **Source**: Walt Stanchfield, *Drawn to Life*, vol. 1 (Focal Press 2013) ch. 41, *Pose and Mood Plus Timing and Phrasing and Texture*, with chs. 14, 57, 100, 107 and 149, and vol. 2 (Elsevier 2009) ch. 9. Distilled in our own words and cited by chapter, under the books' terms. Every threshold below is **ours**, chosen to make a principle checkable, and is marked as ours where it appears. Stanchfield gives principles, not numbers.

§5 and §10 time a move. This section times a **scene**: how its moves are grouped, where the strongest pose goes, and how busy the whole thing is.

**Animate by extremes, and let the extremes carry the story** (ch. 41). The extremes are the poses the audience has to see; what lies between them can be little more than a suggestion. Stanchfield passes on Don Graham's picture of a hummingbird: it darts, and what you see are the moments it hovers. A key that the motion only passes through at speed is never seen at all.

**Phrasing** (ch. 41). People and animals move in phrases: a move ends in a pose, a gesture ends in a pose, an anticipation comes before the next move. That grouping is the beat or rhythm of an action. In life rhythm can happen by chance; in animation it has to be made on purpose. The practical consequence: beats that fall at equal intervals read as a metronome, not a phrase.

**The extreme–extreme** (ch. 41). One pose in a scene will be the most extreme, and it should be the one that carries the story point, usually the pose from the storyboard. When a scene has dialogue, the one or two stressed words decide which drawing is the anticipation, which the extreme and which the extreme–extreme. Without dialogue, the beat the sentence is about plays the stressed word's part. An emphasis that lands a few frames off its beat reads as wrong (ch. 100).

**Texture** (ch. 41). Timing has a texture, like cloth: an even, busy weave, or a few small groupings on a plain field. A crowd cheering a home run wants several violent extremes in very little film; a lazy afternoon wants one tranquil pose held for a long time. Decide the texture before keying, from the mood, because it sets how many extremes the scene has.

**The anticipation holds until it reads** (ch. 107). A character about to rush off to the right first leans left to gather itself, holds that until the audience has it, then goes. The opposition is what accents the move; the hold is what lets it be seen.

**Parts do not all stop together** (vol. 2 ch. 9; vol. 1 chs. 14, 57). Time the parts so they do not all move at one speed or come to rest on one frame: the body arrives, the arm a frame or two after, the hand after that. Attached and flexible parts trail their roots and settle after them; a tail can ease in many frames after the body has stopped.

**A pose needs time on screen to register** (ch. 149). Stanchfield says it of mouth shapes in dialogue, that one or two frames is hardly enough; applying it to every beat pose is our extension.

**Check arcs across several extremes, not two** (ch. 57). An inbetween that looks right between two drawings can break the arc the drawings either side imply. §10's arc check spans a whole move for this reason.

Each of these is a measurement on the keyed values, taken the way §10 takes its checks:

| Principle | Measured as | Threshold |
| :--- | :--- | :--- |
| one extreme–extreme, on the stressed beat | the beat pose furthest from the start, each part measured against its own range, is the stressed beat | within 3 frames (ours) |
| the anticipation opposes and holds | the main part moves against the action, and the whole pose stays within 1° | 3 frames or more (ours) |
| parts stop apart, tips after roots | the last frame each part moves more than 0.1° | no more than half on one frame; each tip after its root (ours) |
| phrasing | the coefficient of variation of the intervals between beats | 0.25 or more (ours) |
| every beat pose registers | frames where the whole pose is within 1.5° of the beat | 3 or more (ours) |
| texture | beats per second against the declared texture | a judgment, recorded with `{ judged: true }` |

**Keep each part on its own track.** Staggered stops need it: a row of numbers per pose (§10) makes every part arrive at every key together. Below, each part is a list of `[frame, value, ease]`, and the settle ends on a different frame for each.

**What the checks taught the example below.** Its first draft failed two of them. The settle carried the head and forearm further out than the strike, so the most extreme pose landed on the settle rather than on the point; and the strike was passed at speed, so it was on screen for one frame. Both are Stanchfield's points exactly, and both were fixed in the keys: the parts settle back from the strike, and the strike holds for two frames before they go.

```javascript
// A point: she draws back, holds, then points hard off to the right. Her body stops first, the arm
// after it, the forearm after that, and the head last. Each part is its own track, so they can stop apart.
const n = Motion.nodes;
const FPS = 24, FRAMES = 48, DURATION = (FRAMES - 1) / FPS;

// Beats. One is stressed: the beat the sentence is about. The texture is declared, not discovered.
const TEXTURE = 'sparse';     // a few clear moves on a quiet field, not a busy weave
const BEATS = [
    { frame: 0,  says: 'she stands, looking ahead' },
    { frame: 10, says: 'she draws back, arm cocked, gathering herself' },
    { frame: 14, says: 'she holds the draw-back long enough to read' },
    { frame: 19, says: 'she points hard off to the right', stress: true },
    { frame: 34, says: 'she eases back from the point, the head settling last' },
    { frame: 47, says: 'she holds it' }
];

// One track per part: [frame, value, ease]. The strike holds two frames; the settles end apart.
const TRACKS = {
    lean:      [[0, 0, 'halt'], [4, 0, 'halt'], [10, -10, 'halt'], [14, -10, 'halt'], [19, 16, 'halt'], [21, 16, 'halt'], [24, 10, 'halt'], [47, 10, 'halt']],
    turn:      [[0, 0, 'halt'], [4, 0, 'halt'], [10, -16, 'halt'], [14, -16, 'halt'], [19, 24, 'halt'], [21, 24, 'halt'], [26, 16, 'halt'], [47, 16, 'halt']],
    rShoulder: [[0, 100, 'halt'], [4, 100, 'halt'], [10, 130, 'halt'], [14, 130, 'halt'], [19, -16, 'halt'], [21, -16, 'halt'], [28, -8, 'halt'], [47, -8, 'halt']],
    rElbow:    [[0, -20, 'halt'], [4, -20, 'halt'], [10, -70, 'halt'], [14, -70, 'halt'], [19, 14, 'halt'], [21, 14, 'halt'], [31, 2, 'halt'], [47, 2, 'halt']],
    lShoulder: [[0, 80, 'halt'], [4, 80, 'halt'], [10, 60, 'halt'], [14, 60, 'halt'], [19, 150, 'halt'], [21, 150, 'halt'], [27, 128, 'halt'], [47, 128, 'halt']],
    neck:      [[0, 0, 'halt'], [4, 0, 'halt'], [10, -6, 'halt'], [14, -6, 'halt'], [19, 14, 'halt'], [21, 14, 'halt'], [34, 8, 'halt'], [47, 8, 'halt']]
};
const ROOT_OF = { rElbow: 'rShoulder', neck: 'lean' };   // a tip settles after its root

const nodes = Object.fromEntries(Object.entries(TRACKS).map(([field, keys]) =>
    [field, n.animated('real', keys.map(([f, v, e]) => ({ time: f / FPS, value: v, ease: e })))]));
const valuesAt = f => Object.fromEntries(Object.entries(nodes).map(([k, node]) => [k, node.at(f / FPS)]));
const frames = Array.from({ length: FRAMES }, (_, f) => valuesAt(f));
const parts = Object.keys(TRACKS);

// 1. One extreme-extreme, on the stressed beat. Each part is measured against its own range, so a
//    large angle cannot outvote a small one.
const span = Object.fromEntries(parts.map(k => {
    const vs = TRACKS[k].map(key => key[1]);
    return [k, Math.max(...vs) - Math.min(...vs) || 1];
}));
const deviation = f => parts.reduce((s, k) => s + Math.abs(frames[f][k] - frames[0][k]) / span[k], 0);
const peak = BEATS.reduce((a, b) => deviation(b.frame) > deviation(a.frame) ? b : a);
const stress = BEATS.find(b => b.stress);
Stage.check('one extreme-extreme, on the stressed beat', Math.abs(peak.frame - stress.frame) <= 3,
    `most extreme pose f${peak.frame} (${deviation(peak.frame).toFixed(2)}), stressed beat f${stress.frame}`);

// 2. The anticipation opposes the action, and holds until it reads.
const a0 = frames[0].rShoulder, aAnt = frames[10].rShoulder, aAct = frames[19].rShoulder;
let held = 0;
for (let f = 10; f < FRAMES && parts.every(k => Math.abs(frames[f][k] - frames[10][k]) < 1); f++) held++;
Stage.check('the draw-back opposes the point and holds', Math.sign(aAnt - a0) !== Math.sign(aAct - a0) && held >= 3,
    `arm ${a0} -> ${aAnt.toFixed(0)} -> ${aAct.toFixed(0)}, held ${held} frames`);

// 3. The parts stop apart, and every tip after its root.
const stops = Object.fromEntries(parts.map(k => {
    let last = 0;
    for (let f = 1; f < FRAMES; f++) if (Math.abs(frames[f][k] - frames[f - 1][k]) > 0.1) last = f;
    return [k, last];
}));
const together = Math.max(...Object.values(stops).map(s => Object.values(stops).filter(t => t === s).length));
const tipsLate = Object.entries(ROOT_OF).every(([tip, root]) => stops[tip] > stops[root]);
Stage.check('the parts stop apart, tips after roots', together <= parts.length / 2 && tipsLate,
    parts.map(k => `${k} f${stops[k]}`).join(', '));

// 4. Phrasing: the beats are not evenly spaced.
const gaps = BEATS.slice(1).map((b, i) => b.frame - BEATS[i].frame);
const mean = gaps.reduce((a, b) => a + b, 0) / gaps.length;
const cv = Math.sqrt(gaps.reduce((s, g) => s + (g - mean) ** 2, 0) / gaps.length) / mean;
Stage.check('the beats are phrased, not metronomic', cv >= 0.25, `intervals ${gaps.join(', ')}; variation ${cv.toFixed(2)}`);

// 5. Every beat pose stays on screen long enough to register.
const onScreen = f => frames.filter(v => parts.every(k => Math.abs(v[k] - frames[f][k]) < 1.5)).length;
const brief = BEATS.filter(b => onScreen(b.frame) < 3).map(b => `f${b.frame}`);
Stage.check('every beat pose registers for 3 frames or more', brief.length === 0, brief.length ? `too brief: ${brief.join(', ')}` : 'all register');

// Texture, as declared and as built: a judgment, recorded as one.
const perSecond = BEATS.length / (FRAMES / FPS);
Stage.check(`the timing reads as ${TEXTURE}`, perSecond < 4, `${BEATS.length} beats in ${(FRAMES / FPS).toFixed(1)} s`, { judged: true });

// The film, constructed every frame from the keyed values, as in §10.
const comp = Motion.composition({ width: 480, height: 320, fps: FPS, duration: DURATION });
comp.fill({ color: '#f2efe8' });
comp.outline({ width: 3, color: '#15151a', points: [[40, 300], [440, 300]] });
comp.drawn((ctx, v) => {
    const fig = Drawing.createMannequinFigure(200, 40, 260, { pose: {
        lineOfAction: { shape: 'C', turnDeg: v.turn, leanDeg: v.lean }, neckDeg: v.neck,
        rightArm: { shoulderDeg: v.rShoulder, elbowDeg: v.rElbow }, leftArm: { shoulderDeg: v.lShoulder, elbowDeg: 15 } } });
    ctx.fillStyle = '#d9d2c3';
    ctx.fill(Drawing.createFigureGeometry(fig).silhouette);
    Drawing.drawGestureContour(ctx, fig, { strokeColor: '#15151a', stretchWidth: 2.4, squashWidth: 1.6 });
    const h = fig.head;
    ctx.strokeStyle = '#15151a'; ctx.lineWidth = 1.8;
    ctx.beginPath(); ctx.ellipse(h.center.x, h.center.y, h.rx, h.ry, h.angleDeg * Math.PI / 180, 0, Math.PI * 2); ctx.stroke();
}, { values: nodes, desc: 'pointer' });

comp.capture();
Motion.sheet('artifacts/manual25-point-beats.png', { indices: BEATS.map(b => b.frame), cols: 6, scale: 0.4 });
Motion.save('artifacts/manual25-point.webp', { fps: FPS, loop: false });
comp.render(stress.frame / FPS);
```

**For the critique, take Stanchfield's order** (chs. 55, 56): before judging anything, name the two or three moments the scene is for; then look at the extremes in order, and again in reverse, which shows an awkward pose the forward order hides.

---

## 12. Cutting, Resolving and Sound

> **Implemented by**: parts joined with `Motion.joinVideos`, `comp.switch` for cuts, a wipe (a keyed `rectangle` with `blend: 'alphaOver'`), groups that stay across a change, and a line of speech placed at the join.

A score decides how things move inside a shot. A film is also decided by how one shot follows another, and by the sound
under them. Colette and Shaw borrow the editor's vocabulary for motion design (ch. 7) and the sound designer's (ch. 11).

### 12a. Continuity or montage

**Continuity** cuts so that the audience feels one uninterrupted flow: a wide shot that establishes, closer shots that
focus, each placed so it follows from the last. **Montage** juxtaposes, without needing a continuous time or place, and
comes in three kinds (ch. 7):

- **Additive** — several shots of one subject building one impression;
- **Comparative** — different places or ideas set side by side for the viewer to resolve the contrast;
- **Rhythmic** — cuts on a fixed interval, so the edit itself becomes a beat the audience anticipates.

An explainer that answers question after question is continuity; a trailer's opening of quick, unrelated images is
montage. Choose which the piece is before the beats, and keep to it.

### 12b. The resolve

**The resolve, or lock-up, is a film's last shot, and the earlier shots are often close-ups of it** (ch. 1). A run of
tight vignettes introduces the colours, the style and the elements; the camera pulls back, or the pieces assemble, and
the final frame — the title, the logo, the product, the ask — answers the question the vignettes posed. Sports openers
and main titles do this constantly (ch. 7). Plan the resolve first and design the shots that lead to it as parts of it;
Manual 29 §8 makes the same point from the concept's side. **Decide its look on a style frame before animating towards
it** — `comp.still('artifacts/03_style_end.png', t)` writes the frame at full size — and hold the finished film to it:
a motion studio shows its client style frames before anything moves (Colette & Shaw, ch. 1).

### 12c. The partial edit

**A cut is not the only change.** In motion, some elements can stay while others change around them — a kind of partial
edit that smooths a transition, keeps one element emphasised across it, or gives a gentler rhythm (ch. 7, *spatial
editing*), as one instrument holds while the others change chords. Here: keep the element in a group outside both scenes'
groups, and change the scenes around it. **A figure carried across a change of scene stays one figure**, which is
clearer than the same figure set again after a wipe.

### 12d. Sound leads and trails

**A J cut brings the sound of the next shot in before its picture; an L cut lets the sound of this shot run on over the
next** (ch. 7). Both bind shots together. With narration, start a scene's first line a beat before its picture cuts in,
or let a closing line finish over the next scene's first frames, rather than cutting voice and picture on the same
frame. Give each line once, at the join, with its own start.

### 12e. Which leads: the sound or the image

**Close your eyes: if the piece still makes sense, it is audio-dominant**, and the images illustrate the soundtrack — a
narrated explainer, a voiced pitch. If not, it is image-dominant, and sound heightens what is seen (ch. 11). Neither is
better, but they are designed differently: an audio-dominant film times its pictures to the voice; an image-dominant
one places its sounds on the movements. Sound born in the scene — a whoosh as a shape passes, an impact — is diegetic;
music added over it is not.

**Many viewers will have the sound off.** Social feeds and screens in public places mostly play silent (ch. 11), so a
film meant for them must make its case with the sound off — every claim on screen, not only in the voice — and should
still be cut to a track, so that even silent it moves to a rhythm. A silent film with no track behind its timing tends
to have none.

---

## 13. What a Motion Studio Delivers Before Anything Moves

> **Implemented by**: `comp.board`, `Stage.shape`, `Stage.borders` and `Stage.book`'s `kind`.

Shaw's account of design for motion is a sequence of artifacts, each closing down what the project could be until it is
one thing (*Design for Motion*, ch. 6: a project starts as "can it be anything?" and tips into "is it becoming
something?"). Most of the early ones the studio already makes — the brief, the mind map (Manual 29 §2a), the thumbnails
(the concept sheets). These four are the ones a client is shown, and they come before the motion.

### 13a. The design board

**A design board is the film told in full style frames**: a sequence read left to right and top to bottom, each frame a
key moment at the finished look, with the camera's changes and the transitions at least suggested (ch. 3). It differs
from a storyboard in that every frame is finished; it differs from a contact sheet in that every frame is captioned for
someone who has not seen the film. It is what the client agrees to before anything moves, because changing the look
after the motion is built costs the work twice.

- **One frame per beat, and enough of them**: Shaw counts nine to twelve for a thirty-second spot (ch. 16), so five or
  six for fifteen seconds. A board thinner than one frame per 3.5 s comes back with a warning.
- **Each caption says what happens and why it matters**; the camera line says how it is seen and how it leaves.
- **Each frame names its shot** (`shot: 'EWS'` to `'ECU'`) and, where it departs from eye level, its `angle` (§15).
- **The board's frames are the film's style frames.** Keep them full size (`stills`) and hold the finished film to every
  one at the end, as §12b holds it to its resolve.

```js
comp.board('artifacts/03_board.png', { title: 'Nightshift — design board', stills: 'artifacts/03_board', frames: [
    { time: 1,    caption: 'Night. An empty teal line across a dark ground: something is waiting.', shot: 'EWS', camera: 'Locked off.' },
    { time: 4.6,  caption: 'The name arrives, as if steeped into the dark.', shot: 'WS', camera: 'Slow push in.' },
    { time: 7.5,  caption: 'The claim settles under it: twenty hours, said once.', shot: 'CU', camera: 'The push ends, and holds.' },
    { time: 11.5, caption: 'Dawn: the ground warms to cream, and the card resolves.', shot: 'MS', camera: 'Cross-fade to the end card.' }] });
```

### 13b. The written treatment, and the shape of the story

**A treatment is the story in words** (ch. 8): a paragraph to a page that tells what happens, in order, and what it
means — what a board cannot say, and what a client reads first. "If you can write down your idea in a paragraph, and it
makes sense", the board has a foundation. Write it as `treatment.md`, in the present tense, once the concept is chosen.
**An interesting story has tension, conflict and resolution** (ch. 11), even at thirty seconds: say what is at stake,
what pushes against it, and how the end settles it.

**Give it a shape.** Shaw teaches story with the graph editor: a velocity that never changes is a straight line and
dull, and a curve that rises and turns is alive. `Stage.shape` draws each beat's intensity — your judgement, 0 to 1 —
over the film's time, and warns when the line is flat. Put the drawing beside the treatment.

```js
Stage.shape(ctx, { duration: 15, beats: [
    { name: 'Night', time: 1, intensity: 0.2 }, { name: 'The name', time: 4.6, intensity: 0.7 },
    { name: 'The claim', time: 7.5, intensity: 0.9 }, { name: 'Dawn', time: 12, intensity: 0.4 }] });
```

### 13c. The DOs & DON'Ts list, checked

**A DOs & DON'Ts list sets the project's creative borders** (ch. 7): what belongs in it and what does not — the palette,
the type, the materials, the camera, how the story unfolds. It is loose enough to grow, and firm enough to say no.

The studio's version (ours): **write each line so it can be checked where it can.** A line ending with a clause in
backticks is measured by `Stage.borders` on the frames you give it; the rest are judged, and listed so you judge them.

```markdown
- DO keep the ground dark until dawn `lightness < 0.35`
- DON'T use pure black anywhere `colour #000000 < 1%`
- DON'T let green into it `hue green < 2%`
- DON'T set more than two typefaces `fonts <= 2`
- DON'T stray off the palette `offPalette #0b1d26 #2a9d8f #f2d9b0 < 3%`
- DO resolve on a balanced end card `balance on last > 0.9`
- DON'T cut on the claim; let it settle.
```

`offPalette` lists the palette in the clause, the ground included, and counts what is far from every colour and every
blend of two, so a gradient between palette colours is on it and a stray accent is not. `on first` and `on last`
measure only the first or the last image given, which in a board's stills or the film's frames is the opening and
the resolve (§14a).

Write it as `dos.md` before the board, check the board's stills against it, and check the finished film's frames again
at the end. A broken line is a decision recorded in `direction.md` or a fault to fix — never a line quietly deleted.

### 13d. The pitch book and the process book

**Shaw keeps two books** (ch. 4). A **process book** documents how the work was arrived at — the thinking, the
methods, the discarded paths — and is what shows a reader how the studio thinks. A **pitch book** sells the chosen
concept: clean, consistent, one typeface, the work given room, and nothing in it that is not the idea being sold.

- `Stage.book('pitch.pdf', { kind: 'pitch', … })`: the treatment and its shape, the design board, the style frames,
  the finished film's frames. It is warned about if it shows a workshop sheet, or has no board or treatment.
- `Stage.book('process.pdf', { kind: 'process', … })`: everything else, in the order it was made — the map, the field,
  the dump, the concepts, the matrix, the boards that changed.

**How a book is made** (ch. 4, and Lauren Hartstone's interview there). The book is a container: clean, organised, and
never competing with what it holds.

- **One typeface**, varied in weight and size, and never set too large. **A neutral ground**, with colour kept for
  accents. **Room on every edge.** Numbered pages under a consistent header.
- **A cover** that is simple and clear: the project, who it is for, the date, the studio. **Contents** when it runs
  long. **A closing page**, a plain word of thanks for a pitch.
- **Carry the idea's tone.** Hartstone says half of winning a pitch is how the presentation is built — the sequence, the
  writing, how the reader is led through the idea — and that a book can reflect the idea's tone on every page rather
  than put six frames on one. The studio's way of doing both: the piece's own typeface (`font`) and accent (`accent`) on
  the neutral ground.
- **Lead the reader through the idea in order**: the idea in a sentence, its story, its look, its frames, what is
  asked. A page is read at a glance; a long file is written as a page of its substance, never pasted.
- **Spell-check every word** (ch. 4: a misspelling reads as unprofessional at once). The studio has no checker: pass
  `proof: 'artifacts/pitch_proof'` and read every page it writes back with your image tool before delivering, the
  words and whether each picture reads at the size its page gives it.

```js
Stage.book('pitch.pdf', { kind: 'pitch', title: 'Nightshift — a fifteen-second film', font: 'Georgia', accent: '#2a9d8f',
    cover: { client: 'Nightshift Cold Brew', subtitle: 'Treatment and design board' }, pages: [
    { title: 'The story', file: 'treatment.md', image: 'artifacts/03_shape.png' },
    { title: 'Design board', image: 'artifacts/03_board.png' }] });            // a cover first, "Thank you." last
```

## 14. The Style Frame Is a Composition, and It Changes

> **Implemented by**: `comp.still` for the frame, `Stage.borders`'s `symmetry` and `offPalette` clauses, `bitmap.diff`
> and `bitmap.palette` (Manual 15) to measure it,
> `comp.group({ z, keepSize })` and `comp.camera()` for planes, and `group.blur` for depth of field.

Every frame on the design board is a style frame, and a style frame is judged as a picture first: Shaw's chapter on
image-making (*Design for Motion*, ch. 9) is composition, value, contrast, colour and depth, the still-image craft that
Manual 09 already carries. This section is what that craft becomes when the composition moves, and the depth rules for
a film built in planes. **Manual 21 holds those depth rules for a panel, and no motion workflow loads it**, so the parts
a film needs are restated here.

### 14a. From mystery to certainty

**Symmetry reads as certainty and asymmetry as mystery** (ch. 9). A balanced, centred frame leaves no doubt what it is
about, which is why posters, print ads and end tags centre their mark. An off-centre frame is open to reading, more
dramatic, and draws a viewer in. A still has to be one or the other. **A film can be both, in order**: it can open in
mystery and travel to certainty, and Shaw notes a commercial almost always does, closing on a near-symmetric frame with
the mark as its focal point for about the last three to five seconds of thirty. Decide on the board which beats are
which, and make the resolve (§12b) the most balanced frame in the film.

Balance can be measured, and `Stage.borders` does it. **`symmetry` blurs the frame until lettering is only weight,
mirrors it, and compares the two**: a centred lock-up comes back near 1, and the further the weight sits to one side,
the lower the number. The method is ours, not Shaw's, and it reads where the weight sits, not whether the frame is
good. Measured: a centred two-line lock-up **0.97**, one title on a thirds point **0.79**, a title top left against a
disc bottom right **0.55**. **It weighs the whole frame, the ground included**, so a single small mark on an empty
ground reads as balanced: nightshift2's opening, one stroke left of centre, measured 0.98. An off-centre opening needs
its weight off-centre, a pool of light or a mass, and that run's fix, the light keyed from off-centre to centre across
the camera pull, made the travel a move the viewer sees.

**`balance` asks the end card's question more directly**: where the frame's weight balances across its width, as a
lever does, 1 on the middle and 0.9 at 5% of the width off it. A frame need not mirror itself to pass. cruelty2's end
card, a centred lock-up over paper scraps strewn unevenly along its foot, measured 0.88 as `symmetry` and 0.99 as
`balance`, and it reads as certain. So the resolve is held to `balance`, and the opening, which should not read as
certain, to `symmetry`. Put the travel in `dos.md`, scoped to the ends of the film:

```markdown
- DO resolve on a balanced end card `balance on last > 0.9`
- DON'T open on certainty: leave the first frame open `symmetry on first < 0.85`
```

Read the opening and the resolve together: an end frame no more balanced than the opening has not travelled anywhere.

### 14b. One change moves every element

**Changing any one element changes the whole frame, and the larger the change, the more it does** (ch. 9), because
everything in a composition is weighed against everything else. After editing a beat, look again at the whole frame
at that beat, not only at the element you edited: `comp.still` it before and after, and read `diff`'s `bounds`
(Manual 15 §4) to see how far the change reached.

**Clutter flattens a frame.** When every element calls for attention, none of them leads, and the eye has no path to
follow. What is left out matters as much as what is put in: the empty space is what frames the focal point. In motion
that applies at every moment, not only at the end. A beat that arrives while the last is still being read crowds it,
which is §5f's one idea at a time seen from the frame's side.

### 14c. Contrast as counterpoint: a critique pass

**Contrast is what gives a frame its tension, and its absence is what makes one flat or washed out** (ch. 9). Shaw
names the beginner's fault as too little difference, and each cure is a counterpoint. Run it over every board frame:

- **A large form?** Set smaller ones against it.
- **A full frame?** Take something out, for negative space.
- **An extreme light?** Put an extreme dark near it.
- **Everything saturated?** Desaturate what is not the point of interest (Manual 09 §2's fifth device).

Then do what Shaw has his students do in critique: **name the strongest and the weakest frame on the board, and say
why.** Write both in `direction.md`, and remake the weakest before anything moves.

### 14d. Depth in planes

A film here is often built in planes: groups at `z` under one `comp.camera()`, which a pan or a push turns into
parallax. **Planes give depth only if each one reads at its distance**, and that comes from value and colour, not from
`z` alone (ch. 9, and Manual 21 §1 after Glebas):

- **At least three planes**: foreground, middle ground, background. Without them the frame is flat. An extreme
  foreground (very close) or an extreme background (barely visible) deepens it further.
- **Contrast comes forward; neutral values recede.** The frame's darkest darks and lightest lights belong on the
  nearest plane, and the farthest plane holds the narrowest range. Glebas passes on Bill Perkins's bands for this:

  | Plane | Value range |
  | :--- | :--- |
  | Foreground | 95% grey up to white |
  | Middle ground | 70% down to 15% grey |
  | Background | 40% down to 20% grey |

  Check a plane's band with Manual 15 §6's luminance palette on a still of that plane alone.
- **Atmospheric perspective**: with distance, contrast and definition fall. **Colour perspective**: with distance,
  saturation falls, and the farthest things take on the colour of the sky.
- **Warm colours come forward and cool ones recede** as a general rule (ch. 9). Treat it as a cue *between* planes,
  not as a law about any one colour: Manual 07 §3's account of temperature as caused by the light is the one to paint
  by.
- **A pure black or a pure white anywhere breaks the depth** (Manual 21 §1): a fully black shadow punches a hole in the
  picture, and a fully white highlight sits on top of it.

### 14e. Depth of field, and moving the focus

**A lens is sharp at one distance and blurs with distance from it** (ch. 9): the plane in focus is crisp, and each
plane is softer the farther it lies in front of or behind it. That is also how eyes work, so it directs attention the
way hierarchy does. In a film it can move: **a rack focus moves the focal point over time**, handing the eye from one
plane to another without a cut.

Put a `blur` inside each plane's group, sized by its distance from the plane in focus, and key the sizes to pull focus.
Key a blur to 0 once it is done, so it costs nothing (`polson://sdk/core/Motion`, *blur*). At 1280 wide, 2 softens an
edge and 12–20 is a strong pull.

```js
// Focus starts on the far plane and racks to the near one over two seconds.
const n = Motion.nodes;
comp.camera();
const far = comp.group({ z: 600, keepSize: true });
far.circle({ origin: [460, 150], radius: 70, color: '#6f8796' });          // neutral and less saturated: it recedes
far.blur({ size: n.animated('real', [{ time: 0, value: 0, ease: 'halt' }, { time: 2, value: 10, ease: 'halt' }]) });
const near = comp.group({ z: 0, keepSize: true });
near.rectangle({ point1: [80, 200], point2: [300, 330], color: '#f2b134' });   // the strongest contrast: it comes forward
near.blur({ size: n.animated('real', [{ time: 0, value: 10, ease: 'halt' }, { time: 2, value: 0, ease: 'halt' }]) });
```

**Never blur what is being read.** A plane carrying a figure or a line of text stays in focus while it is on screen,
as the pitch workflow already asks of the camera ("never while a figure is being read").

## 15. Think Like a Director: Shots, Angles, and Roughing Them Out

> **Implemented by**: `comp.board`'s `shot` and `angle`, `comp.thumbnails`, and `comp.camera()`'s `distance`, `zoom`,
> `tilt`, `roll` and `target`.

**A style frame is the camera's eye, and the design board is the camera's vision over the story** (*Design for Motion*,
ch. 10). Deciding what the viewer sees, from how near and from where, is how a director decides what the viewer thinks
and feels, and a designer of motion has to make the same decisions. Manual 20 §2–3 hold the ladder and the angles for a
drawn figure. Motion design's subject is as often type, a mark, a chart or a product, and Shaw reads the same vocabulary
for those.

### 15a. The shots

| Code | Shot | In motion design |
| :--- | :--- | :--- |
| `EWS` | Extreme wide | Establishes the setting; scale, perspective, distance. Also a reveal: a pull out to a surprise. |
| `WS` | Wide | The subject within its setting, so the action can be taken in; often a transition that moves the story on. |
| `MS` | Medium | A middle distance from the type or the graphic; the bridge between wide and close. |
| `CU` | Close-up | Tight on the subject: detail revealed, and the intensity rises. |
| `ECU` | Extreme close-up | The finest detail; the most intimate and intense. Often a transition: a change of scale, of setting, or of the subject itself (Shaw's example is an eye that becomes a galaxy). |

- **Intensity rises as the camera comes nearer the action**, and the viewer stops observing and starts taking part.
- **Vary the shots**: a sequence of distances is the film's rhythm, like the slow and fast moves of §5a. A board at one
  distance throughout holds one intensity.
- **Give every board frame its `shot`.** `comp.board` warns when none are named, when every frame is the same, and when
  the first frame is closer than wide (§15c).

In the SDK the camera's distance is `comp.camera()`'s `distance` (a push or a pull, keyed) or its lens, `zoom` or
`focalLength`. A shot is also simply how much of the frame the subject fills; for a figure use Manual 20 §3's table, and
for type or a mark, how much of the frame's width it spans is the same measure (ours, not Shaw's).

### 15b. The angles

| `angle` | Where the camera is | What it says in motion design |
| :--- | :--- | :--- |
| `eye` | Level with the subject | Neutral; the default every other angle departs from. |
| `low` | Below, looking up | The subject is larger and monumental. The angle of logo animations and sports graphics. |
| `high` | Above, looking down | The subject is smaller; in film, powerless. In motion design it shows perspective and reveals detail. |
| `dutch` | Rolled, the horizon on a slant | Tension, and diagonals: more dynamic than a frame of only horizontals and verticals. |
| `bird` | High overhead | An unexpected view, and room for sweeping moves: a flight over extruded type or a product. |

Two framings are about relations rather than position: a **two shot** frames two elements to show how they interact,
and an **over-the-shoulder** shot frames what one subject is looking at, which draws the viewer in. In the SDK, `tilt`
raises the camera (positive is above, a high angle; negative is below, a low one), and `roll` gives the Dutch angle. A
plane facing the camera goes edge-on as the camera climbs over it, so **for a bird's eye, lay the plane down** with
`rotateX: -90` (that sign keeps type the right way up seen from above) and raise the camera with a steep `tilt`:

```js
comp.camera({ tilt: n.animated('real', [{ time: 0, value: 25, ease: 'halt' }, { time: 3, value: 60, ease: 'halt' }]) });
const ground = comp.group({ origin: [640, 360], offset: [640, 360], rotateX: -90 });   // the type lies on the ground
```

### 15c. The board's grammar

Shaw names three kinds of board frame (ch. 10), and a board reads better when each frame knows which it is:

- **The establishing frame** comes first. It sets the stage and says where the action will happen, and the same kind
  of frame opens each new scene. A film may open close on purpose, as a mystery it resolves (§14a). Then say so in
  `direction.md`, because `comp.board` will ask.
- **Hero frames** are the moments the viewer must take in: strong compositions, a clear focal point, **type set legibly
  and in balance**, and the film slows down for them.
- **Transitions** connect the heroes, and are much faster. They may be a camera move or a transformation: a pull back
  that reveals what the last frame was part of, or an extreme close-up that becomes the next subject. **The camera is a
  transition device** as well as a point of view.

**A camera move that starts in one frame continues into the next** (ch. 10). Write each frame's `camera` line so it
takes up where the last left off: "the push continues", not a fresh move that ignores the one before. A board whose
camera lines contradict each other describes a film that jumps.

### 15d. Thumbnails before style frames

**Rough the composition before polishing it** (ch. 10). Young designers rush to make finished assets, and however good
those look, the image fails without a strong composition. A thumbnail is the composition worked out small, fast and
cheap: it is meant to be thrown away.

- **At the film's aspect.** A rough at another shape decides nothing about the frame it is for.
- **Decide, in each one**: positive and negative space; symmetric or asymmetric (§14a); the focal point; foreground,
  middle ground and background (§14d); where the light comes from, and the range of values; the shot and the angle.
- **Flat values, no detail.** Three or four greys are enough, and colour waits for the style frame.
- **Several for a frame that matters**: Shaw has designers make a dozen before a strong composition arrives, and roughs
  every hero moment (ch. 11). Two or three for the opening and the resolve at the least.
- **Leave detail out.** A rough that shows the most with the least is one the viewer's imagination can still finish, and
  one you are not yet committed to (ch. 11: Chace Hartman's interview, and Karin Fong quoted at the start of its Part 2). `comp.thumbnails` warns about a set with one rough per beat, and about a
  rough at the wrong shape.
- **A thumbnail differs from a style frame only in polish**: both set the composition and the camera. Build the chosen
  ones into the key poses, and the board follows.

```javascript
// Thumbnails for a fifteen-second 16:9 film: roughs in four flat greys, two for the opening and two for the resolve.
const comp = Motion.composition({ width: 1280, height: 720, duration: 15 });
const rough = draw => {
    const c = createCanvas(320, 180), x = c.getContext('2d');
    x.fillStyle = '#d9d9d9'; x.fillRect(0, 0, 320, 180);              // the ground, the lightest value
    draw(x);
    return c;
};
const opening = [
    rough(x => {
        x.fillStyle = '#262626'; x.fillRect(0, 128, 320, 52);          // foreground: the darkest dark, nearest
        x.fillStyle = '#8c8c8c'; x.fillRect(206, 40, 64, 80);          // the object, off centre: a question
    }),
    rough(x => {
        x.fillStyle = '#8c8c8c'; x.fillRect(0, 104, 320, 76);          // middle ground only: flatter
        x.fillStyle = '#262626'; x.beginPath(); x.arc(72, 60, 30, 0, Math.PI * 2); x.fill();
    }),
];
const resolve = [
    rough(x => { x.fillStyle = '#262626'; x.fillRect(100, 70, 120, 24); x.fillRect(130, 104, 60, 8); }),  // centred lock-up
    rough(x => { x.fillStyle = '#262626'; x.fillRect(0, 0, 320, 180); x.fillStyle = '#d9d9d9'; x.fillRect(110, 80, 100, 20); }),
];
const sheet = comp.thumbnails('artifacts/manual25-thumbs.png', { title: 'Thumbnails', frames: [
    { image: opening[0], beat: 1, shot: 'EWS', angle: 'low', note: 'Asymmetric; the object high right; light from the left.' },
    { image: opening[1], beat: 1, shot: 'WS', note: 'Asymmetric; the dark disc as the focal point; no foreground.' },
    { image: resolve[0], beat: 5, shot: 'MS', angle: 'eye', note: 'Symmetric lock-up on the light ground.' },
    { image: resolve[1], beat: 5, shot: 'MS', angle: 'eye', note: 'Reversed out: the name light on the dark.' }] });
log(sheet.warnings.length ? sheet.warnings.join(' | ') : `${sheet.cells} roughs, no warnings`);
opening[0];
```

Pick from the sheet with your image tool, record why in `direction.md`, and keep the sheet: it belongs in the process
book (§13d).

## 16. Compositing: Making Many Sources One World

> **Implemented by**: `blend: 'onto'` and `'alphaOver'`, `comp.lumaKey`, `comp.blur`, `comp.colorCorrect`, the YUV blends
> (`'color'`, `'hue'`, `'saturation'`, `'luminance'`), `comp.noise`, and `comp.image` for what `Assets` brings in.

A film here is rarely one source. A drawn ground, a generated packshot, a requisitioned texture and set type have to
read as one picture. Shaw calls that **compositing: combining distinct elements so they feel they belong together**
(*Design for Motion*, ch. 13). It runs from **concrete**, where the result must pass as real and the rules of
perspective, light, value and colour are strict, to **abstract**, where the rules bend but a visual pattern must still
hold the image together. Most of this studio's films are towards the abstract end.

### 16a. Read the sources before combining them

Before anything is combined, answer for each source what Shaw asks: **where does its light come from, which way do its
shadows fall, what is its range of values, what is its perspective, is there depth of field?** (ch. 13). A packshot lit
from the left on a ground lit from the right is a collage, not a composite. The answers are §14's: value bands for each
plane, one light, one horizon, blur by depth.

### 16b. Mattes

**A matte isolates part of an image** (ch. 13): the figure lifted from its ground, a landscape held inside a silhouette
(the double exposure of *True Detective*'s titles). Three kinds, each one group:

- **Alpha matte**: a shape, then what should fill it with `blend: 'onto'`. Its inverse is the shape with
  `blend: 'alphaOver'` above what it should cut.
- **Luma matte**: a picture whose greys say where (an `Assets.matte`, a drawn gradient, a photograph of smoke or ink),
  then `comp.lumaKey()`, then what shows through with `blend: 'onto'`.
- **A matte in time**: the matte wipe, `comp.wipe({ type: 'matte', image, completion })` (see *What you can bring in*).

**Feather to match the source** (ch. 13): an edge sharper or softer than the image around it is what gives a composite
away. A `blur` in the matte's group, before what goes onto it, softens the edge. Size it to the softness of the
nearest edge in the picture.

### 16c. Grade to one palette

**Colour correction makes sources match; grading gives them one look** (ch. 13): hue, saturation and value moved,
often only slightly, until each source sits in the same colour space. Shaw's example is a title sequence held to red,
yellow, black and grey at one saturation throughout. Two ways to do it here:

- **A grade over everything**: one `comp.colorCorrect` at the top of the stack, as the films already do.
- **A tint to the palette**: desaturate a source (`colorCorrect({ saturation: 0 })` in its group), then a flat palette
  colour over it with `blend: 'color'`. That takes the colour's hue and chroma and keeps the source's own light and
  shade. `amount` sets how far. A generated picture brought into a drawn film this way stops looking pasted in.

### 16d. Texture and grain unify

**Gradients, noise and texture help a composite read as one surface** (ch. 13). The same grain over drawn and
generated parts is the strongest single cue that they were made together. `comp.noise` is Synfig's noise, matched to it
pixel for pixel. For grain, a new one-pixel pattern every frame, use the native composite: about 25 ms a frame at
1920 × 1080, so a whole film can carry it. **Not `overlay`**: Synfig's overlay is keyed by the layer, not the backdrop as Photoshop's is, so it lifts the
darks by about the square of the noise and peppers a dark ground, and it adds about 135 ms a frame at 1080 (the SDK's
*Blend methods*). The recipe, 6% either way:

```js
comp.noise({ size: 2, detail: 1, smooth: 'nearest', speed: 24, stops: ['#00000010', '#80808000', '#ffffff10'] });
```

For a dark film, weaken the light specks, which show most on a dark ground: `['#00000018', '#80808000', '#ffffff0a']`.
Put it last, above everything, so the type is grained with the picture. **Keep it faint**: 12.5% (`…20`) already read as
peppered on a dark frame. Grain that can be noticed is a style, not a unifier, and stronger grain is a decision to
record in `direction.md`, not the default. A still texture is an `Assets.material` tiled with `blend: 'overlay'`, at the cost of one
full-frame blend.

### 16e. Duplication and restraint

**Digital duplication is free, so it is easy to overuse** (ch. 13): one scanned material can be recoloured into a set,
which is a script loop over a copy with its own `colorCorrect`. But exact repetition is what makes retouching and
composites look fake, so vary each copy (the `Random` toolkit, seeded).

```javascript
// A luma matte, a tint to the palette and grain: a drawn "plate" shown through a soft drawn matte, graded, grained.
const comp = Motion.composition({ width: 640, height: 360, duration: 1 });
comp.linearGradient({ point1: [0, 0], point2: [0, 360], stops: ['#14202b', '#1c2430'] });   // the ground

// The matte: a soft bright disc on black. In a run this could be an Assets.matte or a photograph of ink.
const m = createCanvas(320, 180), mx = m.getContext('2d');
const disc = mx.createRadialGradient(160, 90, 10, 160, 90, 80);
disc.addColorStop(0, '#ffffff'); disc.addColorStop(1, '#000000');
mx.fillStyle = disc; mx.fillRect(0, 0, 320, 180);

// What shows through it, tinted to the palette: desaturated, then the amber laid over by colour.
const inside = comp.group();
inside.image({ image: m, tl: [0, 0], br: [640, 360] });
inside.lumaKey();
const plate = inside.group({ blend: 'onto' });
plate.linearGradient({ point1: [0, 0], point2: [640, 360], stops: ['#3a7ca5', '#f2efe8', '#c8553d'] });
plate.colorCorrect({ saturation: 0 });
plate.fill({ color: '#c9802f', blend: 'color', amount: 0.8 });

// Grain over everything, last, on the native composite.
comp.noise({ size: 2, detail: 1, smooth: 'nearest', speed: 24, stops: ['#00000010', '#80808000', '#ffffff10'] });
comp.render(0.5);
```

### 16f. A scene you own: the matte painter's order

**A matte painting puts the composition and the light in the designer's hands** (*Design for Motion*, ch. 15). A frame
built around a found or generated picture has to take that picture's composition. A frame built from a sketch, with
the material made to fit it, does not. The matte painter's order, in this studio's calls:

1. **Thumbnails, and choose** (§15d): the planes, an unexpected camera, and **where the light comes from**, decided
   before anything is made.
2. **The value sketch**: the chosen rough in flat greys, its masses blocked in. The thumbnail already is one.
3. **A plate built around it.** Draw the foreground masses of the chosen rough as flat `#000000` silhouettes on one
   `#808080` ground, at the film's shape, and pass that as the backdrop's `conditionOn`. The plate is then generated
   around your composition instead of bringing its own. A blocking with shading or colour is refused before anything is
   spent, and `remedy` says what to change. A plate conditioned this way is tied to that blocking (`plate.boundTo`):
   change the composition and it must be made again.
4. **Light the foreground from the plate** (Manual 16 §8): `plate.metrics.keyLightX` and `keyLightY` are where its
   brightest mass is, 0 to 1 across the frame. Shadows fall away from that point and rims face it. A foreground lit from
   the other side of the plate's sun is the commonest way a composite reads as fake.
5. **Paint over** (ch. 15): once the pieces are placed, deepen the shadows and lift the highlights across all of them,
   so they share one light. A translucent dark gradient towards the side away from the light, and a translucent warm glow
   at the key light, both on the native composite, cost a few milliseconds where `multiply` and `screen` over a full
   frame cost a shader pass each.
6. **Grade and texture on top** (§16c–d): the grade, then the grain, last in the stack.

```js
// 3. The blocking: the chosen thumbnail's foreground masses, black on grey, at 16:9.
const b = createCanvas(640, 360), bx = b.getContext('2d');
bx.fillStyle = '#808080'; bx.fillRect(0, 0, 640, 360);
bx.fillStyle = '#000000'; bx.fillRect(0, 270, 640, 90); bx.fillRect(420, 120, 90, 150);   // the quay; the warehouse
const plate = await Assets.backdrop('a harbour at dusk, low cloud, water catching the last light',
    { width: 1920, height: 1080, conditionOn: b.toBitmap().toPngBytes() });
if (!plate.success) { error(plate.remedy); exit(plate.failureName); }
Session.plate = plate.toDataUri();
Session.key = [plate.metrics.keyLightX * 1280, plate.metrics.keyLightY * 720];

// In the next script: the plate, the foreground drawn over it, then 5. the paint-over on the native composite.
comp.image({ image: Skia.Image.fromDataUrl(Session.plate), tl: [0, 0], br: [1280, 720] });
// … the foreground, lit from Session.key …
comp.linearGradient({ point1: Session.key, point2: [1280 - Session.key[0], 720], stops: ['#00000000', '#00000060'] });
comp.radialGradient({ origin: Session.key, radius: 420, stops: ['#ffe2b040', '#ffe2b000'] });
```

Shaw's brief for the exercise asks for **a scene that would be difficult or impossible to photograph**. A composited
landscape alone is called "underwhelming" (ch. 15). The order above is for exactly the frames a brief wants that a
search could never find.

## 17. Type in Motion

> **Implemented by**: `comp.text` (`family`, `weight`, `style`, `size`, `compress`, `vcompress`, `kerning`, `invert`,
> `animators`), `text.settledAt`, `text.boundsAt`, `Motion.contrast`, `blend: 'onto'` and `'alphaOver'`, groups at `z`.

Nearly every commercial carries type, whether a whole spot of kinetic type or only the logo's end tag (*Design for
Motion*, ch. 17). Manual 11 holds the craft of setting it, as for print. This section is what changes when it moves
and is seen on a screen.

### 17a. Cast the typeface

**Choose a typeface the way a casting director chooses an actor** (ch. 17): first name the role, then audition for it.
Write down the adjectives the concept and the treatment give (playful or grave, modern or classical, warm or cold)
before looking at a single face. **Then audition the whole company**: `Skia.Font.families()` lists every installed face,
often hundreds, and `Skia.Font.families(text)` those that can set a line in the audience's own script. A careless choice says something the film did not mean. Carter, Day and Meggs make
the same point from the other side: a face's proportions, serifs and peculiar characters give it a personality, and can
**signal a period, a place or a milieu**, often through a detail too small to name (*Typographic Design*, p. 143).

**Those associations are cultural** (ch. 17): slab-serif wood type reads as the American West, stencil as military,
collegiate letters as sport. They are not the same everywhere. For a set made for several audiences, say in
`direction.md` what the chosen face connotes, and whether that holds for each audience. It is a judgement to state, not
a measurement Qloo can make. Record the adjectives with the choice, and check the finished film against them.

### 17b. Set it for the screen

Manual 11's rules hold. The screen changes a few numbers (*Typographic Design*, pp. 144–147):

| On screen | Why | In `comp.text` |
|---|---|---|
| **Capitals tracked open, to at least 110–120%** | screen light spreads each letter into the next; capitals alone need the room most | `compress` about 1.1–1.2 for a line of capitals |
| **45–65 characters a line** for anything read as a sentence | the eye takes a screen line in at a glance | break the line; a film's lines are usually far shorter |
| **Leading around 140% of the size** | text on screen needs more space between lines than print | `vcompress` multiplies the font's own line height, which already exceeds the size; set it by eye on a still near 1.2 |
| **Flush left reads easiest**; centre what is truly symmetric, such as a lock-up or an end card (§14a) | the ragged right edge guides the eye from line to line | `orient: [0, …]` for ranged left, `[0.5, …]` centred |
| **Moderate stroke contrast, sturdy serifs, simple forms** for anything small | hairline serifs and extreme thick-thin contrast break up as type gets smaller on screen | a display face with hairlines only at display size |
| **A real italic or bold, never a faked one**, and no stretched glyphs | — | `style` and `weight` select real faces |

Size by what the type measures in the frame, not by the number asked for: `text.boundsAt(t)` gives its box. Hold every
line still long enough to read (`text.settledAt`), keep it title-safe, and measure its contrast on the graded colours
(`Motion.contrast`).

### 17c. Integrate it into the picture

**Type pasted on after everything else flattens the frame** (ch. 17). It is an element of the composition like any
other, so give it the picture's space, value, contrast and colour, until it belongs. In this studio's calls:

- **On a plane**: type in a group at its own `z` moves with the camera and finds its depth among the picture's planes,
  rather than floating over them.
- **Graded and grained with everything** (§16c–d): the grade and the grain go over the type as well.
- **A surface inside the letters**: the text, then a material, a noise or a picture with `blend: 'onto'` in the same
  group. The letters become the matte (§16b), for Shaw's tactile type.
- **Letters cut as windows**: `invert: true` fills everything but the letters, so the picture behind shows through them.
  Carter describes the *Altered States* titles this way: words like windows opened in a black ground, overlapping as
  they drift (*Typographic Design*, p. 174). With `blend: 'alphaOver'` in a group, the same knockout erases instead.

### 17d. Make the motion mean something

**A typographic sign works on two levels: its form and its meaning** (*Typographic Design*, p. 115). A form can be made
to act out the word: repeated letters read as speed, a small letter in a large void as isolation (p. 115). Moving type
has more ways to do this, and a title sequence is, in Richard Greenberg's phrase as Carter quotes him, a "visual
metaphor" for the film that tells the audience how to react (p. 173). Carter's examples include a title gathered out of
small rectangles as a dark planet passes (*Alien*), and two opposing words revealed on the faces of turning cubes
(*True Lies*) (pp. 174–175).

So choose each text animator for what the word means, not only for how it enters (§5a, *timing is meaning*). Shaw's
treatments (ch. 17), in this studio's calls:

| Treatment | How |
|---|---|
| **Graphic construction** | the letters' outlines as shapes (`comp.region`, `comp.outline` from a path) recoloured, cut, assembled |
| **Tangible / tactile** | a material or noise inside the letters (17c) |
| **Cut and paste** | letters on separate small planes, slightly misaligned, each entering on its own key |
| **Ephemeral** | wipes (the matte wipe for ink or smoke), `blur` keyed in and out, opacity animators by range |
| **Hand-lettered** | marks drawn with `Skia.Brush`, set into the film with `comp.image` |

Baudenbacher, interviewed in the chapter, puts the rule that holds over all of it: even in a complex frame there is
**one focal point**, and even five or ten seconds have a beginning, a development and a conclusion.

```javascript
// A title integrated into its picture: a material inside the letters, the letters cut as windows over a plate,
// both on planes under one camera move, and grained with everything else.
const comp = Motion.composition({ width: 640, height: 360, duration: 3 });
const n = Motion.nodes;
comp.camera({ distance: n.animated('real', [{ time: 0, value: 889, ease: 'halt' }, { time: 3, value: 760, ease: 'halt' }]) });

// The far plane: a dusk ground, with a windowed word over it. The knockout fills all but the letters, so the
// ground shows through them.
const far = comp.group({ z: 400, keepSize: true });
far.linearGradient({ point1: [0, 0], point2: [0, 360], stops: ['#3a7ca5', '#c8553d'] });
far.text({ text: 'NIGHT', family: 'Arial', weight: 'bold', size: 150, origin: [320, 150], orient: [0.5, 0.5],
    color: '#14202b', invert: true, compress: 1.12 });                       // capitals tracked open for the screen

// The near plane: the title filled with a tactile surface in the picture's own palette.
const near = comp.group({ z: 0, keepSize: true });
near.text({ text: 'shift', family: 'Georgia', style: 'italic', size: 96, origin: [320, 280], orient: [0.5, 0.5], color: '#ffffff' });
near.noise({ stops: ['#c9802f', '#efe6d6'], size: 40, detail: 3, seed: 5, blend: 'onto' });

// One grain over everything, the type included, so nothing reads as pasted on.
comp.noise({ size: 2, detail: 1, smooth: 'nearest', speed: 24, stops: ['#00000010', '#80808000', '#ffffff10'] });
comp.render(1.5);
```
