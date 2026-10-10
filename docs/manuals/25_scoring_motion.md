# Studio Manual 25: Scoring Motion

> **Credits & Theoretical Foundation**: **§11 distils Walt Stanchfield's *Drawn to Life* (vol. 1 ch. 41 and others), cited by chapter there; §1–§10 distil no book in `reference/`.** This header used to say the corpus held no animation text. That was wrong: Stanchfield taught at Disney, and his lectures, ledgered on 2026-09-25 and first read only for gesture, include chapters on timing, phrasing and overlap. §5's received practices are corroborated there. The rest comes from three places, each named where it is used: the **position paradigm** is GSAP's, adopted deliberately and credited (it is not from the corpus and no claim is made about the rest of that library); the **cost figures** are our own measurements, recorded in `docs/motion-score-api.md` §3; and the **seek-rather-than-play** model is the studio's own, argued from the medium — an agent renders frames, it does not watch them. Where this manual states a principle of timing craft that animators have long known, it is stated as received practice without attribution to a source we have not read. The calls are documented at `polson://sdk/core/Motion`.
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
> corroborated by Stanchfield (§11) but not measured; treat it as advice from a tradition rather than as a finding.
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

- **Key what you mean, compute what follows.** A drift is a `Motion.nodes.linear(...)`, a sway is a
  `Motion.nodes.sine(...)`, a fall is keys. Keying a drift frame by frame is a hundred numbers saying
  what one rate says.
- **Choose the ease per key, not per piece.** `halt` is a stop, `constant` a hold, `clamped` passes
  through without overshooting, `auto` may overshoot on purpose. The default is `clamped`, because an
  unasked-for overshoot reads as a wobble.
- **Link rather than copy.** One node passed to two options keeps them together through every later
  change; two equal nodes drift apart the first time one is edited.
- **Reuse with a group's clock.** A group's `timeOffset` and `timeDilation` play the same passage later
  or faster without re-keying it — the follow-through that trails an action by a few frames.
- **Lay a key on a formula with `add`.** `Motion.nodes.add('vector', drift, bob)` is a drift with a bob on
  it; `Motion.nodes.scale('real', swing, 0.5)` is the same swing at half the size. Neither needs a group.
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
  (`upperLid` 3 → 5 → 3 is a blink, `browTop` 1 a raised brow), with `near.` or `far.` for one side. Treatments and
  shapes are identity: hold them in `look` and `character`, so the character stays the same person. Two timing
  rules from the corpus are worth keying in: the eyes follow the mouth by about three frames in a smile (Essa),
  and the parts should not all stop on one frame (§11). `face.headAt(t)` places a balloon or a prop on the head
  at any time. It is not exported to `.sif` or SVG; capture it.
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
