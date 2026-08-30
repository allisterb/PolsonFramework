# findings — cs-1

Developer-experience report for the `comic_studio` run. Written as the run proceeds.

## Setup

**The source-tree denies were already in place and were verified, not assumed.**
`.claude/settings.local.json` denies `Bash`, `BashOutput`, `KillShell`, `WebFetch` and `WebSearch`,
and additionally denies `Read`/`Grep`/`Glob` on absolute paths under `src/`, `ext/` and each
`tests/Polson.Tests.*` directory. Those absolute-path denies are hand-added — `CLAUDE.md` explains
that the generator cannot add them itself, because it cannot know where the project was placed.

First action of the run was the self-test: `Read` on `src/Polson.Runtime/Polson.Runtime.csproj`,
which was **refused**. A `.csproj` is the right probe — it would have leaked nothing about the
drawing API had the deny failed open.

*Finding, minor:* the deny patterns spell the path `C:/Projects/Polson/...` while the project
actually sits at `C:/projects/Polson/...`, lowercase. The rules still bind, so the matcher is
case-insensitive here — but a harness whose entire validity rests on those denies should not depend
on that being true, and nothing in the docs says which way it goes. The probe is what makes the
run trustworthy; the config alone would not.

## A disclosure that affects how you should read this report

**This run is not a cold read of the API, and it would be dishonest to present it as one.** This
directory held a completed prior run. Deciding what to do with it required reading its
`findings.md`, so before writing a line of code I had seen its four reported defects and its
measured anchors. What I did about it:

- **Every measurement here is my own**, taken from the pixels. None of the prior run's anchors were
  copied; where our numbers agree it is because we measured the same image.
- **Its defects were re-tested rather than repeated**, and one of them **does not reproduce** (below).
- The prior run is archived unmodified at `archive/run-2026-08-29a/`.

The general point for the harness: a run in a directory that already contains a previous run's
`findings.md` cannot measure cold discoverability, because the archiving decision itself requires
reading it. If that property matters, the generator should place a new run in a clean directory.

## Run notes

### Defects

**1. There is an undocumented statement cap on the sandbox, and it kills the whole script.**

A connected-component flood fill over the 620x336 reference — to get pupil bounding boxes without
guessing — died with:

```
The maximum number of statements executed have been reached.
```

`success:false`, no image. The `log()` output written *before* the failure was still returned, which
is the only reason the run did not lose the silhouette scans in the same call.

Three problems, in order of severity:

- **The limit is not documented anywhere I could find** — not in `polson://sdk/core/Globals`, not in
  the `ExecuteScript` tool description. The tool description documents `width`, `height`, `format`,
  `quality`, `outFile`, `outSvg`, `includeBytes` and not this.
- **The message gives no number.** I cannot tell whether I was at 1.1x the limit or 50x, so I cannot
  tell whether to optimise the loop or abandon the approach. I guessed and restructured.
- **There is no parameter to raise it**, and no partial result.

This is a sharp edge for exactly the task this workflow sets. Reproducing a reference means reading
it, and reading a 208,320-pixel image per-pixel in JS is the obvious way to do that. The fix I want
is a documented number and the count in the message; failing that, one sentence in the tool
description saying that per-pixel loops over a full frame will not complete.

**2. `Drawing.drawCompositionGrid` paints colour onto a construction sheet, with no way to stop it.**

`Drawing.drawCompositionGrid(ctx, grid, { opacity: 0.28 })` draws its power points as **pink/salmon
dots**. The Penciler role spec's hard constraint is *"Draw only in non-repro blue (`#4a90e2` /
`#5b8db8`) and graphite (`#444444`)"* — so the one call the spec tells the Penciler to use to
establish the armature is the one call that breaks the Penciler's own colour rule.

`options` is typed `options?: any` and the only key documented anywhere is `opacity`, in the manual
09 example. There is no documented `color`, `pointColor` or `stroke`. Manual 09 and the role spec
were written against each other and this was not caught.

Worked around by ruling the thirds by hand — four lines and four ticks, which took less code than
the call did. That is worth noting on its own: for a plain rule-of-thirds the helper is not saving
anything, and for the comparison the role actually asks for (see defect 3) it cannot be used at all
without reflection.

**3. `Drawing.createCompositionGrid` returns a different `powerPoints` shape per armature, and the
schema documents none of it.**

Measured, this run:

| armature | keys returned |
| :--- | :--- |
| `ruleOfThirds` | `topLeft, topRight, bottomLeft, bottomRight` |
| `goldenRatio` | `goldenEye, secondaryEye` |
| `dynamicSymmetry` | `center, harmonicTopLeft, harmonicTopRight` |
| `triangle` | `apex, center` |

`polson://sdk/schema/Drawing` declares `powerPoints` as bare `{"type": "object"}` and enumerates
nothing. The Penciler role spec asks the agent to establish *"which compositional armature the
reference is actually built on … checked against the focal mass"* — i.e. to compare armatures — and
that comparison **cannot be written from the published schema**. I had to enumerate keys reflectively
with `for (const k in g.powerPoints)`.

The counts also differ (4, 2, 3, 2), which quietly biases any "distance to nearest power point"
metric toward `ruleOfThirds` simply for having twice as many candidates. I still chose
`ruleOfThirds` — 80.8px mean against 168.7 for the runner-up is not a margin four-versus-two
explains — but the metric the role spec implies is not a fair one, and neither the schema nor
manual 09 warns you.

**4. `Drawing.verifyPlumbAlignment`'s tolerance is a hard threshold, so a true measurement reports
as a failure.**

Three of my four plumb checks returned `DRIFT`, and all three were correct measurements of things
that genuinely are not plumb: the trunk leans 16px over 186, and the right rabbit's cranium sits
32px right of its muzzle because its head is turned toward the viewer. The call has no way to say
"measure this, I am not asserting it is vertical" — every result is framed as PASS or DRIFT against
a tolerance you must supply in advance.

Minor, but it matters in a workflow where a Critic reads the log: `DRIFT: Offset by 32.0px` in a run
record reads as a defect, and here it is the drawing being right. `deltaX` is returned and is the
genuinely useful part; a `measure`-flavoured variant, or a null tolerance meaning "report only",
would stop a correct measurement looking like a failed check.

### Re-tested, and NOT reproduced

**The archived run's headline defect — "`ctx.clip(path,'evenodd')` permanently changes the path's
fill rule" — does not reproduce.** Run this session on the archived run's own test shape:

```
same path, evenodd  -> centre 255   (knocked out — correct)
same path, default  -> centre 0     (painted — correct; 255 would mean the rule stuck)
same path, nonzero  -> centre 0     (correct)
FRESH path, default -> centre 0     (control)
```

Line 2 is the one that mattered and it behaves correctly: the fill rule is **per-call**, as HTML5
requires, and is not stored on the `CanvasPath`. Either it was fixed between the two runs or the
earlier conclusion was wrong. I have not looked at the source and cannot say which — but if it was
fixed, this is the regression test.

I kept passing the rule explicitly everywhere in `artwork.js` regardless. That is cheap, and it is
what the HTML5 API wants anyway.

### What worked, and worked well

- **`Skia.Image.load` + `ctx.getImageData` is the whole game for reproducing a reference.** Every
  anchor that matters came out of threshold scans rather than my eye. `ImageData.data` as a flat
  RGBA array is the right shape for this — run-length scans over rows and columns gave me every
  silhouette, ear cross-section and contour in one call each.
- **`Skia.ColorFilter.highContrast(true, 'none', 0.9)` turns a reference into a Notan for free**, and
  it was the single most useful measurement act of the run. Rendering the panel through it made the
  dry stems, the grass clumps and the leaf chain legible as shapes at once — and corrected a mistake
  I was about to make, since it showed that the fine near-horizontal lines crossing the middle of the
  panel are the right rabbit's **whiskers**, not background twigs. The manuals treat Notan as
  something you *construct*; it is also something you can *extract*, and that is not written down.
- **`multiply` compositing is the right way to check a construction against its reference.** Draw the
  reference, then the sheet with `globalCompositeOperation = 'multiply'`: the parchment ground drops
  out and only the lines survive. It separates "in the wrong place" from "drawn badly", which need
  different fixes — my pass 1 was almost entirely the second kind and I would have misdiagnosed it
  from the render alone. Alpha blending washes both out and tells you nothing.
- **Nested `ctx.clip()` calls intersect**, which is how every crescent shadow in the final drawing is
  cut: clip to the mass, then clip to a quad covering its shadow side. No boolean path op needed.
  This is standard canvas behaviour but it is not mentioned in `polson://sdk/core/Canvas2D`, and it
  is the workaround for the missing path-difference operation.
- **`ctx.scale(S, S)` once at the top, then draw in reference coordinates.** Obvious in hindsight and
  it removed a whole class of transcription error: every measured number goes into the code exactly
  as measured and can be checked against the reference by eye.
- **`Search` with a bare call name is genuinely good.** `Search('Drawing.verifyPlumbAlignment')`
  returned `confidence: "direct"` with an authoritative signature. Being told plainly that a
  `no-match` on a call name is *definitive* is the thing that makes it trustworthy.

### What I could not find in the published docs

- **No way to read a `polson://` resource as a whole document.** The role specs say "read
  `polson://manual/01`", and this session exposes no MCP resource-reading tool — only the five
  `mcp__polson__*` tools. `Search` returns ranked excerpts and was enough, but an agent can only ever
  query a manual, never read one. Either the harness should expose a resource reader or the role
  specs should stop instructing agents to open URIs they cannot open. (Unchanged from the archived
  run, which reported the same thing.)
- **No boolean path operations.** `Skia.PathEffect` has no entry for them and `SKPath.Op` is not
  exposed, so "the outside of a set of shapes" has no clean expression. The back-to-front
  fill-with-ground technique in `artwork.js` is the workaround and is arguably better practice, but
  it is not documented as the idiom anywhere.
- **`createCompositionGrid`'s `options` is undocumented** beyond `opacity`. For `triangle`, where the
  apex position is the entire point, there is no published way to move it.
- **Nothing documents how to hatch.** Clipped parallel strokes is the classic technique and it is
  what every value in the final drawing is made of; I wrote the helper. `Drawing.createHalftoneDotShader`
  exists for Ben-Day dots, which is the comic-book answer, but there is no pencil-hatching equivalent
  and no worked example of hatching a clipped region.

### Things I hand-rolled that the SDK already provided

- **A quadratic-through-midpoints polyline smoother.** `Skia.PathEffect.corner(r)` would have rounded
  the measured contours with one call. My helper is fine and gives more control over which contours
  get smoothed — the dry stems must *not*, which cost a pass to discover — but I did not know
  `corner()` existed until late.
- **Ellipse-as-polyline.** I generated ellipse points by hand rather than trust a path transform
  idiom. That was a deliberate risk-avoidance choice rather than an oversight, and it made the
  rotated ear ellipses trivially checkable — I could compute the predicted chord (109.5) and compare
  it against the measured one (110).

### Did the file handover work?

This run was one agent moving through two roles rather than two agents, so the handover was not
stressed the way a real multi-agent run would stress it. What I can report:

**The handover that mattered was `ANCHORS`, and it worked because it is a single self-contained
object literal of named constants.** The Critic stage needed to add regions and shadow sub-regions,
and could do that purely by *composing* the Penciler's contour chains — `cat(A.R.faceLeft,
A.R.chestLeft.slice(1), ...)` builds the right rabbit's whole body outline out of chains the
Penciler had already named. Had those contours been buried in call arguments, every region would
have had to be re-measured.

**The one thing that did not survive is the stage boundary itself.** The Critic role spec audits
`artifacts/stage3_inker.webp`, which this run never produces, and prescribes "fix the inking" when
the actual job was "resolve a construction sheet into the pencil drawing the brief asked for". The
role specs assume the full four-stage pipeline; the brief can ask for a subset; nothing reconciles
them. A stage that receives something it cannot use is exactly what the findings brief asks about,
and this is that case — mild, because I could adapt, but a two-agent run where the Critic was a
separate agent given only `roles/04_critic.md` would have looked for a file that does not exist.

**`Session` was not used for handover.** It does not survive a new MCP session, and the durable
channel is the saved script in `scripts/` plus `Stage.note`. Worth stating plainly because the
session scratchpad is the convenient channel and the wrong one.
