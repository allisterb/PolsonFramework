# findings — cs-1

Developer-experience report for the `comic_studio` run. Written as the run proceeds.

## Setup

**Source-tree reads denied by hand before starting.** `CLAUDE.md` notes that the generated
`.claude/settings.local.json` denies the shell and the network but not reads outside the project,
because the generator cannot know where the project was placed. This one was generated *inside* the
Polson repo (`C:/Projects/Polson/tests/multi_agent/comic_studio/cs-1`), so the implementation was
one relative path away. Added `Read`/`Grep`/`Glob` denies for the absolute paths of `src/`, `ext/`
and each `tests/Polson.Tests.*` directory — listed individually, since `tests/` also contains this
project and a deny cannot carve an exception out of itself.

Verified rather than assumed: an attempted read of `src/Polson.Runtime/Polson.Runtime.csproj` was
refused. The rules bound in the running session without a restart. (A `.csproj` was chosen as the
probe deliberately — it would have revealed nothing about the drawing API had the deny failed open.)

*Finding, minor:* the harness asks each run to hand-write this, and gets it only if the agent both
reads that paragraph and knows the working path-pattern spelling. `Read(../**)` is the intuitive
try and silently denies nothing.

## Run notes

_(to follow)_

---

## Stage 1 — Penciler

### Defects

**1. `ctx.clip(path, 'evenodd')` permanently changes the path's fill rule. — real bug, silent.**

The SDK reference says of `CanvasPath`: *"A reusable path object, independent of any context —
build it once and pass it to `ctx.fill(path)`, `ctx.stroke(path)` or `ctx.clip(path)` as often as
you like."* It is not reusable across fill rules. One even-odd clip mutates the stored fill type,
and every later call that omits the rule silently inherits even-odd instead of the HTML5 default
`'nonzero'`:

```javascript
function mk(){ const p = new CanvasPath(); p.rect(0,0,300,200);
               const q = new CanvasPath(); q.rect(100,60,100,80); p.addPath(q); return p; }

let p = mk();
clipAndFill(p, 'evenodd');   // centre WHITE  — correct, the inner rect is knocked out
clipAndFill(p, null);        // centre WHITE  — WRONG, default must be nonzero -> BLACK
clipAndFill(p, 'nonzero');   // centre BLACK  — correct again, and it repairs the path

p = mk();
clipAndFill(p, null);        // centre BLACK  — correct on a path that has never seen evenodd
```

Lines 2 and 5 are the same call on the same path. `fill()` and `clip()` both default to nonzero
correctly on a *fresh* path, and `path.rect()`, explicit-winding subpaths and `addPath()` all
produce correct nonzero behaviour — so the bug is specifically that the fill rule is stored on the
path by `clip` rather than being per-call. The failure is silent and shape-dependent, which is the
worst combination: a Colorist who knocks a counter out with even-odd and then reuses the same path
for an ordinary clip loses the interior with no error. **Workaround: always pass the fill rule
explicitly.** Recorded so the Colorist and Inker do not have to rediscover it.

**2. `Drawing.verifyPerspectiveConvergence` is sensitive to the order of each line's two points.**

Same two lines, same vanishing point, answer flips between PASS and a 179.9° failure purely on
which endpoint is listed first:

```javascript
const A=[[{x:174,y:0},{x:168,y:340}],[{x:322,y:0},{x:300,y:340}]];   // top -> bottom
const B=[[{x:168,y:340},{x:174,y:0}],[{x:300,y:340},{x:322,y:0}]];   // bottom -> top
Drawing.verifyPerspectiveConvergence(A, {x:250,y:-4000}, 6);  // DRIFT: 179.9deg
Drawing.verifyPerspectiveConvergence(B, {x:250,y:-4000}, 6);  // PASS: 4.36deg
```

179.9° is the signature of comparing the segment direction against the ray to the vanishing point
without folding by 180°. Geometrically a line converges on a VP regardless of which end you name
first, and nothing in `polson://sdk/core/Drawing` says the points must run toward the VP. The
practical trap is that the *failing* direction is the natural one to write — you list a vertical
from the top of the frame downward, and a VP above the frame then reports total drift. I only
caught it because 179.9° is too round a number to be a real measurement; a subtler wrong answer
would have gone through. **Fix should be a `min(d, 180-d)` fold; documentation alone would not be
enough, because the current behaviour reports a confident wrong answer rather than an error.**

### Misleading, but not broken

**3. `Drawing.createLoomisHead` is a human skull and the docs do not warn you off an animal.**

Not a bug — the manual is explicit that it is the Loomis method — but worth stating, because the
call takes a plain `(originX, originY, headHeight, yaw, pitch)` and will happily produce a full
landmark set for a rabbit. `createLoomisHead(854, 237, 276, 25, 0)` against my measured skull:

| landmark | Loomis | measured | verdict |
| :--- | ---: | ---: | :--- |
| crown y | 99 | 105 | usable |
| eyeLineY | 242.5 | 235 | usable |
| inter-eye distance | 68 | **147** | 2.16x wrong |
| chin, noseBase, jaw.angle | present | do not exist | unusable |

The *vertical* thirds transfer to a rabbit; the *lateral* placement does not, because a rabbit
carries its eyes on the sides of the skull. Cranial construction was hand-rolled — one circle plus a
muzzle lobe. There is no animal-head constructor in the toolkit, and given that this workflow ships
with a rabbit reference, `createLoomisHead` returning a confident human answer for it is a sharp
edge. A one-line `<remarks>` saying "human proportions; not valid for animal skulls" would cost
nothing.

**4. `Drawing.createCompositionGrid` returns different power-point *key names* per armature.**

`ruleOfThirds` gives `topLeft/topRight/bottomLeft/bottomRight`, `goldenRatio` gives
`goldenEye/secondaryEye`, `dynamicSymmetry` gives `center/harmonicTopLeft/harmonicTopRight`,
`triangle` gives `apex/center`. The schema at `polson://sdk/schema/Drawing` declares
`powerPoints` as bare `{"type": "object"}` and does not enumerate any of them. So code that wants
to *compare* armatures — which is exactly what the role spec asks the Penciler to do — has to
enumerate keys reflectively and cannot be written from the published schema. The counts also differ
(4, 2, 3, 2), which makes any per-armature mean a different denominator; worth a sentence in the
schema doc.

### What worked, and worked well

- **`Skia.Image.load` + `bitmap.getPixel` is the whole game for reproducing a reference.** Every
  anchor on the sheet that matters came out of threshold scans over the reference rather than out
  of my eye. Flood-filling under `L < 45` gave both pupil bounding boxes to the pixel; a
  brightest-pixel search inside each gave the catchlights. The `#RRGGBBAA` string return is
  ergonomic enough that a `parseInt(h.substr(1,2),16)` helper is a two-line job.
- **`Skia.ColorFilter.highContrast(true, 'none', 0.85)` turns a reference into a Notan for free.**
  Rendering the panel through it and reading the result was the single most useful measurement act
  of the stage — the dry stems, the grass clumps and the light muzzle masses all became legible as
  shapes at once. The manual talks about Notan as something you construct; it is also something you
  can *extract*, and that is not written down anywhere I could find.
- **`multiply` compositing is the right way to check a construction sheet against its reference.**
  Draw the reference, then draw the sheet with `globalCompositeOperation = 'multiply'`: the
  parchment ground passes through and only the lines survive. Alpha blending washes both out.
- **`verifyPlumbAlignment` earns its place.** It is a trivial calculation and I would have skipped
  writing it, but having it as a call meant I actually ran three checks I would otherwise have
  eyeballed, and its `deltaX` in the message is what makes the result quotable in a handover.
- **`Skia.Font.has` / `Skia.Font.resolve` did what the docs promise** and made the label typography
  a non-issue.

### What I could not find in the published docs

- **No way to read `polson://manual/NN` or `polson://sdk/...` as whole documents.** The role spec
  says "read `polson://manual/01`", but this session exposes no MCP resource-reading tool — only the
  five `mcp__polson__*` tools. `Search` returns ranked excerpts with the URIs attached, which was
  enough, but it means an agent can never read a manual end to end, only query it. Either the
  harness should expose a resource reader or the role specs should stop instructing agents to read
  URIs they cannot open.
- **`Drawing.createCompositionGrid`'s `options` parameter is undocumented.** The signature index
  gives `options?: any` and neither `polson://sdk/core/Drawing` nor manual 09 says what any armature
  accepts. For `triangle` in particular — where the apex position is the entire point — there is no
  way to find out how to move it.
- **No documented way to get the *outside* of a set of shapes as a clip region.** The even-odd
  rect-plus-shapes trick is standard canvas practice and it does work here, but it is fragile on
  self-intersecting contours (see defect 1's neighbourhood), and nothing in the SDK reference
  mentions the idiom. `Skia.PathEffect` has no boolean-op entry and `SKPath` operations are not
  exposed to scripts, so there is no `Op(Difference)` available either.

### Things I hand-rolled that the SDK already provided

One, and it was cheap: a quadratic-through-midpoints polyline smoother. `Skia.PathEffect.corner(r)`
would have rounded my measured contours with a single call and I did not think of it until the sheet
was finished. Worth noting for the Inker — the measured contour point lists in `ANCHORS` are dense
enough that `corner()` on the raw polyline is probably a better line than my smoothing helper gives.

### Handover risk the next stage should know about

The stage-1 script writes its anchors into `Session.ANCHORS`, but **`Session` does not survive
across MCP sessions** — if the Colorist runs in a fresh connection, `Session.ANCHORS` is
`undefined` and the anchors must be re-read from the `Stage.note` handover or from the saved script
in `scripts/`. The one intermediate script I wrote (pass 4) mutated `Session.ANCHORS` in place
instead of redeclaring it, which would have left a non-self-contained script in `scripts/`; the
final pass redeclares the whole object literal, so the last saved script stands alone. That was a
near miss, and it is a general trap for a file-handover pipeline that also has a session
scratchpad: the scratchpad is the convenient channel and the wrong one.

