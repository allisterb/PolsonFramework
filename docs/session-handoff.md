# Session Handoff — 2026-09-02 (fifth and sixth sessions)

State after the session that gave the studio a **window onto runs it does not drive**. It started as
"can we watch a Claude Code run in the browser" and turned into the discovery that most of what the
dashboard needed already existed — and that once you can watch a run, you start finding things.

**Tests: 1,232 .NET — all passing** (Drawing 406, MCPServer 464, CLI 268, ExtendedMind 94).
The previous handoff is superseded; its open items are carried forward at the end.

> **§12 is the current state** and where a new session should start. §§1–8 are the fifth session
> (observability, `scriptFile`, the Claude profile); §§9–12 are the sixth (cs-5 findings, encode
> cost, the Guy exclusion, and re-sourcing the manuals to Loomis, Norling and Faragasso).

---

## What this session was actually about

**Observability, and what it costs to have none.** Nearly every change below was found by looking at
a live run rather than by reading code:

- A finished picture that 404'd in the render panel, found from a screenshot.
- 850 KB of JavaScript re-sent to change part of a program, found by measuring gaps.
- A subagent that could not write the files its own role spec told it to write.
- An agent deleting its own report because `--reset` had left a stale one.
- An hour lost to a permission prompt nobody could see.

None of those is visible from source. All of them were obvious from the record once there was
somewhere to look at it.

---

## 1. The studio watches runs it does not drive

Work a project in Claude Code or Claude Desktop, press **Watch** on the index, and the same run page
opens over it. Nothing extra is captured to make that work: the MCP server writes `server.jsonl`
whoever drives it, and the `preserve-chatlog` hook already preserved the host's transcript.

| Piece | What it does |
| :--- | :--- |
| `orchestrator/hostlog.py` | Transcribes a host transcript into `agent.jsonl` / `director.jsonl`. Idempotent by the transcript's own `uuid`, with the resume point read back out of the record rather than kept in a state file that could disagree with it. |
| `studio/observe.py` | A `Run` with no task: replays the merged record, tails `server.jsonl`, re-syncs the transcript. Read-only by design. |
| `project.read` | Beside `project.load`. Every check in `load` is a *drivability* check, and applying them to a reader refused the feature's whole subject — a Claude project was turned away with "the orchestrator builds Antigravity SDK configurations only", which is true and beside the point. |

**It reads the host's live store, not the hook's copies.** The hook fires at turn end, and a subagent
can work for forty minutes inside one turn — so a page watching the copies shows the spine live and
the conversation frozen, which is backwards from what a person wants while a stage is quiet. The path
is not guessed: `hooks.jsonl` records the transcript each firing resolved.

Three traps worth not rediscovering:

- **`mcp__polson__ExecuteScript` must be normalised before coding.** `SPINE_OWNED` holds bare names,
  so without stripping the prefix every execution in a host-driven run is coded twice — once from the
  spine, once from the transcript. That is the failure the existing comment warns about, and it would
  have read as a run twice as productive as it was.
- **Whether a `thinking` block carries text is not ours to control.** Measured across 409 transcripts:
  present 63–100% per day through August 2026, absent from the 21st (9 of ~3,300 since), tracking
  cached client feature flags. The block is emitted either way, marked `redacted` when empty — `csm`
  codes `thinking` as `wait` and nothing else does, so dropping the empty ones would take every pause
  out of the curve.
- **Subagent transcripts live outside the parent's.** Claude Code writes them to
  `<session>/subagents/agent-*.jsonl` with a `.meta.json` naming the `agentType`. `preserve-chatlog`
  now copies them; earlier I wrongly concluded from an `isSidechain` field that they were inline.
  They are not — the field is always false in a parent.

**The agent's prose now codes as `communicate`, as the director's does.** One participant speaking
directly to another is communication whichever is speaking; the asymmetry was an artefact of where
the two halves of the record came from. `AskUserQuestion` codes the same way — under the orchestrator
a question reaches `director.jsonl`, under a host it arrived as a tool call and fell through uncoded.

---

## 2. `scriptFile`, and what re-sending a program costs

Measured on the first `cs-5` run: **850 KB of JavaScript over 55 calls**, the twenty largest taking a
mean of **three minutes each to emit**, against a **median engine time of 45 ms**. Consecutive large
scripts shared **71%** of their lines. Wall clock tracked bytes *emitted*, not bytes read — the
Critic ingested the most and finished fastest, 11 minutes against the Colorist's 67.

`ExecuteScript(scriptFile: 'artwork.js')` runs a file instead. Both sources given is refused rather
than resolved; the server still copies **what actually ran** into `scripts/`, so a later edit never
rewrites an earlier execution's history; `script.start` names the source.

**It worked.** The second `cs-5` run used it for **25 of 48 executions**, and `Edit` became the
most-used tool at 160 calls.

---

## 3. The Claude profile, audited and repaired

Generated both profiles and diffed them. Current on the things that matter — the MCP allowlist is
reflection-derived and cannot drift, the hooks are wired, and its path denies are *stronger* than
agy's, which still has none. Four gaps found, all closed:

- **Multi-agent was agy-only**, on a premise that had gone stale: the code said Claude Code had
  "nowhere to register" a role, and it reads `.claude/agents/*.md`. A live run then dispatched
  `subagent_type: "penciler"` from a generated definition.
- **`Agent` versus `Task`.** The dispatch tool is named `Agent` in this build; the allowlist named
  `Task`, so the entry was inert and the run prompted. Both spellings now — an unrecognised entry is
  silently inert, so naming one is a rule that looks enforced and is not.
- **Subagents could not write.** Their role specs tell them to write `critique_log.md` and
  `artwork.js`; with `Read` alone, all sixteen of those edits fell to the coordinator and the
  collaboration trace was written second-hand. They now get the main agent's own tools.
- **The shell was denied wholesale**, which was too blunt. `grep`, `sed`, `diff` have nothing to do
  with drawing and everything to do with maintaining a source file. Allowed by command prefix;
  `node`, `dotnet`, `magick`, `curl`, nested shells and — after a live run deleted its own report —
  `rm`, `git` and friends stay denied.

> **An allowlist decides what is auto-approved; it prevents nothing.** An unlisted command falls
> through to a prompt, and a prompt in a long run gets waved through. That is how `rm` ran, and it is
> why destructive verbs are denied rather than merely unlisted.

---

## 4. `--reset` archives instead of deleting

It cleared `events/`, `scripts/` and `artifacts/` but **left** `findings.md`, `critique_log.md` and
`artwork.js` — so a project held a 27 KB report describing a record that had just been deleted. The
next agent read it, correctly judged it stale, and reached for `rm`. Both halves of that were ours.

Everything now moves to `previous/<timestamp>/` with a README saying what it is. Nothing is deleted,
the next run still starts clean, and `previous/` is gitignored. The shared instructions carry the
rule for agents too: **never delete — rename aside and say why**, because a stale file that survives
costs a moment's confusion and a deleted one may have been the only copy.

`projects/cs-5-baseline/` holds the first run, recovered after a reset took it: transcripts, 62
scripts extracted from `ExecuteScript` payloads, and `findings.md` / `critique_log.md` replayed from
their `Write` + `Edit` calls (all 14 edits matched, so the replay is exact). That recovery is what
this change makes unnecessary.

---

## 5. The dashboard

Reorganised on the director's own read of it: **curve → render | script → full-width trace.** The
trace was the tall left column and the script a 380px gutter, which had it backwards — the script is
code, and the trace carries the most variable-width content there is. Both top rows are
height-bounded and scroll internally so the trace stays on screen during a live run.

- **Timestamps and gaps** in the trace: `19:18:14 +6s`, warning-coloured past 30 s. Duration by
  reading rather than subtracting.
- **Tool detail** — the argument that says what a call was doing, chosen per tool. A row reading
  `Bash` is indistinguishable from any other; `Bash cat >> critique_log.md << 'EOF'…` is not. Long
  *commands* keep their opening; whole documents stay a bare length.
- **An at-work line**: who is working, what they last did, how long ago. When the last event is a
  tool call and two minutes pass, it says *"may be waiting for your approval in the agent session"* —
  hedged, because the record cannot see a prompt, only that a call has not come back.
- **The curve scrolls** at ~9px per point instead of compressing, and **mode checkboxes** narrow what
  is marked. The line always stays computed from every event: a curve recomputed from a subset would
  be a trajectory the run never had.
- **`expand`** on the script pane, sharing the render's lightbox so Esc and click-to-close are one
  behaviour.

---

## 6. `typeof` works now, and what that cost

An agent cannot ask whether a call exists. Strict resolution throws on an unresolved member — right,
because it is what stops `ctx.fillStlye = 'red'` doing nothing silently — but it also defeated
`typeof`, `in`, `Object.hasOwn` and `Reflect.has`, all of which route through the same accessor. The
first `cs-5` run spent **18 of its 71 renders** on probe scripts, deleting a candidate name at a time
to find out whether it existed.

Reads are now lenient, and three things keep that from being the old silent-typo bug:

- **Writes still throw.** Jint consults the accessor on reads only.
- **Calls still explain.** `MissingCallResolver` uses `IReferenceResolver.TryGetCallable`, which fires
  with a `Reference` carrying both the name and the base — the only hook that can see enough.
- **Reads are recorded** as `absent` probes, so an agent thrashing on names that do not exist is a
  line in the trace rather than something we might notice.

Plus `has(object, name)` and `suggest(object, name)` — the latter returning the same advice a failed
call gives, searching the whole surface, so a foreign name gets pointed at the real one.

> **`'name' in obj` reports every name as present on an SDK object, and that is not a defect.** The
> member accessor is a *value provider*: it can decline, or answer with a value. There is no third
> answer meaning "absent", because .NET has no such state — a type's members are fixed. Answering
> `undefined` to give JavaScript its semantics back also asserts the property exists. `typeof` reads
> the value and is right; `in` asks about existence and cannot be. Documented under the execution
> model with the JS/.NET seam explained, and pinned by a test.
>
> Nothing incorrect follows: every route that could change the artifact still refuses and explains.

**Rejected on the way**: relaxing resolution *without* `TryGetCallable` (loses the suggester),
`TypeResolver` (exposes only `MemberFilter`/`MemberNameComparer`/`MemberNameCreator` — no expression
context), and subclassing `ObjectWrapper` to fix `in` (constructor is `internal`, and overriding
`HasProperty` would contradict the descriptors the same object hands out).

---

## 7. What the run itself produced

`cs-5` finished: **111 scripts, 71 renders, 7 stages** including reopened ones —
Penciler → Colorist → Inker → Critic → Penciler → Colorist → Critic. A late-night noodle stall in the
rain, and the director judged the faces and arms the weak passages.

**`artifact.read`: 45.** The previous handoff recorded that column as empty — *"a whole 11-script run
recorded none: a single agent holds its own context, so it never needs to look back"*, and called it
the one thing the enactive claim most needs to show. Four agents handing over through files is what
filled it.

Its `findings.md` is 28 KB of developer-experience report, 23 numbered findings plus 5 on
orchestration. Closed so far: the `ctx` shortcut rule, `typeof`, the stale-files problem, **#3**, **#9/#10**, **#11**
and the documentation batch **#7**, **#8**, **#12**, **#17**. What remains is missing capability
(**#4** no joint poser, **#14** no closed mouth, **#19** the comic trio below ~100 px, **#13/#18** no
path recorder) and the harness items (**#5** `view_file` in five shipped templates, **A/F**, **B**,
**E**).

**#21 is not ours.** The report presents `pointInHull` as a documented call that returns a plausible
wrong answer and asks for an argument-order guard. It is the agent's own helper, defined in its own
`artwork.js`; nothing in `src/` or `tests/` has ever had that name. The observation still stands as a
run-record point — a predicate that always answers "inside" is invisible in a render, and it was
caught only because a sibling copy culled 260 of 260 rain streaks — but there is nothing to fix here.

### #9/#10 — the Perlin shaders were coloured, and the manuals taught the use that breaks

Confirmed by measurement before anything was written. Over 24 samples of a 64×64 fill:
`perlinNoiseTurbulence` gave **2 grey pixels of 24**, channels spread **109 of 255** apart, alpha
ranging `0..171`; `perlinNoiseFractal` gave 1 of 24 and a spread of 95. Four independent noise
fields, one per channel, faithful to SVG `feTurbulence` and precisely wrong for grain or vapour laid
over a surface with `soft-light`.

- **`Skia.Shader.luminance(shader)`** is the correction — Rec. 709 on RGB, alpha row identity. The
  remedy needed to be one call rather than a 20-float `colorMatrix` an agent transcribes by hand,
  because the hand-written version is where finding #10 gets made: clamping alpha in the same matrix
  greys the noise perfectly and renders atmosphere as an opaque sheet. Both halves are asserted.
- **`createRopeFiberShader` and `createAtmosphericCloudShader` now default to `luminanceOnly: true`.**
  The line drawn is that the raw primitive stays faithful to `feTurbulence` and the opinionated
  presets do the right thing. This changes their output; pass `false` for the old behaviour.
  Manual 04's runnable example was the "latent bug" the report named, and it is fixed by this default
  rather than by an edit to the example.
- **`Skia.Brush` never had the defect** — its internal grain shader averages the three channels to a
  scalar and paints the brush's own colour through it. Checked rather than assumed, because Manual 17
  now says so in print.
- Warnings in `Polson.core.md`, Manual 04 §5B/§5C and Manual 17 §5, and four tests in
  `HarnessReportedBugTests`. Two of those **pin the raw behaviour** rather than deplore it: the
  warnings are built on it, and a manual that warns about something Skia no longer does is its own
  kind of defect.

### #3 — `drawPerspectiveCylinder` was three defects, and the report named one

Measured before and after, anchor `(400, 450)`, `radius: 60`, i.e. ink expected to span `x 340..460`:

| | width | as a factor | centre drift |
| :--- | ---: | ---: | ---: |
| before, `cameraAngleDeg` 30 / 45 / 60 | 225 / 230 / 225 px | **1.88× / 1.92× / 1.88×** | ≤ 3 px |
| after | 122 px | **1.02×** (the 1.8 px stroke) | 0.5 px |

The report's "roughly 2×" was right. Underneath it were three separate errors, only the second of
which it identified:

1. **The anchor was a corner, not a centre.** The call built a `2r × 2r` perspective box and
   `CreatePerspectiveBox` anchors at the near bottom *corner*, so the base circle sat up-frame of
   where the caller put it by half the footprint depth.
2. **The width came from the footprint's diagonal.** `rx` was half the horizontal span between the
   two *side* corners of that square — the diagonal, not the inscribed circle.
3. **One `ry` served both caps.** Derived from the ground footprint and reused for the top, so a
   cylinder's two ellipses were identically squashed however tall it was.

**The elevation parameter the report asked for is not the answer, and adding it would have been a
mistake.** A cap's foreshortening is fully determined by the directions to the vanishing points *at
its own centre* — nearer the horizon, shallower rays, flatter ellipse. So the rewrite computes each
cap's ellipse from its own screen position and the elevation case dissolves: a pot on a counter is
flatter than the same pot on the floor because it was anchored higher, with nothing told how high the
counter is. `TestCylinderOnARaisedSurfaceIsFlatterThanOnTheGround` is that claim, asserted.

Now built from conjugate semi-diameters along the two ground directions, each pair normalised so the
drawn half-width is exactly `radius` — which is what keeps the contours vertical, as a vertical
cylinder's silhouette must be, and leaves only the flatness free to vary. A one-point grid would
collapse this (both vanishing points coincide), so it falls back to the 45° distance points: a
perpendicular pair in the same plane, and a circle has equal radii along any such pair.

**Contract change, documented in Manual 06 and `Polson.core.md`:** the anchor is the **base circle's
centre** and `radius` is **half the drawn width**. `createPerspectiveBox` still takes a corner with
ray-pixel lengths, so the two calls now differ on purpose — a cylinder has no corner and `radius`
implies a centre. Manual 06's own runnable column was drawing at 1.9× and is fixed by the change.

### #11 — `drawRimLight` drew an outline, and pushed it off the form

Both halves of the finding held, and the point-list branch was worse than reported: it stroked the
whole list at full opacity **and** offset every point two pixels *outward* along the light vector.
The agent measured its rim about 10 px outboard of the figure; two of those were the call's own.

The rect branch was independently broken. It drew one straight line down either the left or the right
edge, chosen by `cos(angle) > 0` — so a light from directly overhead has cosine zero, falls to the
`else`, and lights the **left** edge, the one place an overhead light puts no rim at all.

Now each stretch of contour is weighted by `max(0, n · L) ^ spread` with `n` the outward normal, and
the band is drawn **inside** the contour rather than beside it. Measured on a radius-120 circle at
three light angles: exactly the lit half drawn, the far half exactly zero, quadrant totals symmetric
about the light and agreeing within 0.5% across angles. `spread` is a new trailing option, default 2.

Two things worth knowing for the next call site:

- **Segments are grouped into runs of equal quantised weight and stroked as polylines.** Stroking each
  segment separately is the obvious implementation and is wrong: at any alpha below 1 the round caps
  overlap and the rim comes out beaded. Runs abut at shared vertices with butt caps instead.
- **What cannot be fixed here is the caller's point list.** A list that approximates the silhouette
  still floats clear of the form, because the call never sees the form. Both manuals now say the list
  must *be* the silhouette, and point at the construction the run actually settled on — a gradient
  fill inside a clipped shape — as the sturdier option when a rim must follow a form exactly.

A note on method: the first measurement of this looked like a real asymmetry — one light angle
produced a fifth of the ink of the others. It was the probe sampling the nominal contour radius, which
reads the antialiased outer edge of a band that now sits inside it. Moving the sample to the middle of
the band made it symmetric. The test carries that reasoning, because the same probe written the
obvious way would have failed for a reason that has nothing to do with rim light.

### The documentation batch — #8, #12, #17, #7

- **#8, `ctx.clip` binds toolkit draws.** True, undocumented, now stated in `Polson.core.md` next to
  the shortcut list and pinned by a test. The test's unclipped control is the load-bearing half: a
  mannequin that happened not to cross the boundary would satisfy the clipped assertion on its own.
- **#12, `MaskFilter.blur(σ, 'outer')` was findable only from the API.** It was defined in two places
  and reachable from neither of the problems it solves. Manual 09 §3 now carries *When two adjacent
  masses have merged in value* — the failure a Notan pass exists to expose — with the call, the reason
  it beats moving either mass's value, and a pointer at `bitmap.palette` to check the result.
- **#7, nothing joined perspective to the figure canon.** Manual 06 §5a, *One Scale for the Figure and
  the Architecture*: model in metres, hand the grid the same numbers, and let `feet.y − crown.y` size
  the mannequin rather than choosing a pixel height. Ends with a complete runnable scene, so it is now
  part of `ManualExampleTests`.

  The section's real content is the trap the agent hit. A counter 0.95 m high at 2.6 m and a 1.78 m
  vendor at 3.4 m: the canon puts the hip at `1.78 × (1 − 3.6/8) = 0.98 m`, clearing by **2.9 cm** —
  answerable before a pixel is drawn. On screen the hip is at `y=451` and the counter edge at `y=508`,
  57 px that *look* like clearance and are mostly depth, because the scale is 346 px/m at the counter
  and 265 px/m at the figure. **Height questions get answered in metres; screen `y` cannot compare two
  things at different depths.** Verified end to end: the hand projector's edges converge on the grid's
  own centre of vision to `0.000004°`.

**#17 was largely wrong, and the correction is the useful part.** It claimed the statement cap is
"unnumbered in the published resources" and that `bitmap.diff` / `rowProfile` / `palette` are
undiscoverable through `Search`. Both were checked and both are false: the cap is stated as 2,000,000
under *Execution Limits*, and all three calls resolve at `confidence: direct` against the symbol index,
with Manual 15 — an entire manual on measuring a render — as a top prose hit.

What is real is the ranking underneath the complaint. For *"measure the rendered image"* the top two
hits are `ctx.drawImage` and `ImageData`, and Manual 15 lands fourth — so the retrieval points at the
raw pixel buffer, which is exactly the route to the cap the finding died on. Fixed where the trap is
rather than in the index: the *Pixel Buffer Access* section now warns that `getImageData` is for
writing pixels back and names the three native calls, and the *Execution Limits* tip says the
commonest way to reach the cap is measuring rather than drawing.

This is the second finding in this file that reported an environment defect it had not tested — after
the Critic's sandbox breach in **E**. Worth noticing as a pattern: the drawing findings were measured
and nearly all held, while the findings *about the tooling* were reasoned from a single failed attempt.
An agent can measure a render; it cannot as easily measure why a search disappointed it.

---

## 9. What a render costs, and the format table that was wrong

Prompted by a suspicion that JSON encoding dominated tool time. It does not — but the shape of the
suspicion was right, because the record was under-reporting.

**`ExecutionTimeMs` stopped before the render.** `sw.Stop()` fired immediately after
`EvaluateAsync`, so the one duration in the run record was the script alone and everything after it —
rasterise, encode — was invisible. On a 1200 × 760 scene that is 24 ms reported against 93 ms hidden.
Now split: **`EncodeTimeMs`** on the result, timed across all eight encode sites, recorded as
`encodeMs` beside `ms`. `ExecutionTimeMs` was deliberately *not* widened — it is published in the run
record and the SDK reference, so redefining it would silently change every number already recorded.

**Base64 is not the expense; context is.** Steady-state `JsonSerializer.Serialize` of a 126 KB payload
is **0.22 ms**, and `Convert.ToBase64String` 0.1 ms. The first serialize in a process costs ~28 ms of
metadata warm-up, which is what a naive benchmark reports as "JSON is the bottleneck" — it was the
first number this session measured, and it was wrong. What is expensive is that the base64 lands in
the caller's window **as text**: ~126,000 characters for a routine WebP, ~458,000 for the same frame
as PNG. **There is no MCP image content block anywhere in the server** — the tool returns a typed
object that gets serialised — so those tokens may not even buy a viewable image, which is consistent
with cs-5 finding #5, where the agent used `Read` on the artifact path to see its own work.

`includeBytes` is now marked `AVOID THIS` in the tool schema, carries a `[!CAUTION]` in the SDK
reference, and every inlined render writes a `script.bytesInlined` event with byte and character
counts. Not refused — there are legitimate uses, and a tool that quietly declined would be worse.

### The format table stated one scene's numbers as universal

It claimed WebP is "3.9× smaller" than PNG, flat. Measured across three scenes at 1200 × 760 and one
real 1600 × 1200 artifact, **the ranking inverts with content**:

| | flat graphic | gradient | noise | real panel (1600 × 1200) |
| :--- | :--- | :--- | :--- | :--- |
| `png` | **40 ms · 8 KB** | 53 ms · 199 KB | 317 ms · 955 KB | 160 ms · 672 KB |
| `webp` @ 85 | 53 ms · 16 KB | 84 ms · **34 KB** | 349 ms · **322 KB** | **124 ms · 53 KB** |
| script alone | 3 ms | 19 ms | 73 ms | — |

On flat graphic work — line art, logos, construction sheets — **PNG is half the size and a third
faster**, so the old claim was backwards for the content the studio produces most. On a finished
painterly panel WebP is faster *and* twelve times smaller. Neither is a global default, and the table
now reads by column with a per-content rule instead of pretending there is one answer.

Three things fell out worth keeping:

- **Encode dominates script time in every case measured** — 13× on flat, 3× on gradient, 4× on noise.
  A slow call is almost never a slow script.
- **Decode is never the problem**: 3–16 ms across every format and scene, about a tenth of encode. The
  hypothesis that WebP's decode cost was hurting both sides does not survive measurement.
- **The lever is resolution, not format.** Encode tracks pixel count and the spread between formats is
  much smaller than the spread between sizes. Draft small; render the final large.

The default stays `webp` @ 85. The caveat on the real-panel row is that it re-encodes an already-lossy
WebP, which flatters WebP — the detail PNG would have to store is already gone.

### Uncompressed was the right instinct and the wrong lever

If artifacts move by file, why compress at all? Measured at 1600 × 1200: a hand-written 32-bit BMP
encodes in **4.8 ms against WebP's 144 ms** and decodes in 2.4 ms, the fastest of anything tried. The
encode cost really does almost vanish. Three things stop it being usable:

- **Skia will not write BMP.** `Encode(SKEncodedImageFormat.Bmp, 100)` returns `null`.
- **The agent could not see it.** Tokens scale with the decoded raster rather than the file, so size
  costs no context — but the *host* still has to decode it, and image input is JPEG/PNG/GIF/WebP. A
  `.bmp` artifact is invisible to the peek loop, which is the only reason the file exists.
- **7.68 MB per render.** cs-5 did 71 of them.

**And "PNG with compression off" is not the escape hatch either** — zlib level 0 still costs **68 ms**
for a 7.69 MB file, because the container's filtering, chunking and CRC run over all 7.7 MB regardless.
Level 1 is the only mildly interesting point (85 ms / 597 KB against the default's 112 ms / 513 KB).

### `render: false` — the lever that was actually there

Measuring turned up something better than a format change. **A canvas is rendered whenever the script
created one and returned something else**, so returning `'measured'` from a probe did not avoid it and
neither did `exit(...)`. There was no way to draw without encoding, and a measurement pass draws in
order to measure — so every probe paid a full rasterise and encode for an image nothing read, about
150 ms at 1600 × 1200, on the scripts an agent runs most often. This is the same use case cs-5 finding
**#17** died on.

`render: false` on `ExecuteScript` gates the encode. The script runs normally; logs, measurements and
`Session` writes all survive. Refused together with `outFile` — that asks for the render this
suppresses — and `outSvg` is unaffected, since vector markup is serialized rather than rasterized.

Paired with the scratchpad it removes both halves of a stage handoff: **`Session` holds bitmaps and
canvases, not just data.** `Session.stage1 = canvas.toBitmap()` in one call and `bitmap.diff(prev)`
against it in the next needs no encode on the way out and no decode on the way back. Verified across
executions, and now documented under `Session` with the caveat that artifacts a *reader* will look at
must still be written — `outFile` is what makes a run reviewable, and the scratchpad dies with the
session.

`ExecutionTimeMs` was deliberately left meaning what it always meant. `EncodeTimeMs` reads 0 when the
render is suppressed, which is the honest answer rather than an absent one.

---

## 10. The templates told the agent to use another host's tool, and lied about the sandbox

### #5 — `view_file` was not a mistake, it was the other host's spelling

Fifteen occurrences across eight shipped templates, and **`view_file` is a real Antigravity builtin**
(`BuiltinTools.VIEW_FILE`). The same generator emits for both hosts — `AlwaysDenied` and
`StandaloneDenied` name Antigravity tools, the Claude allowlist names `Read`/`Grep`/`Glob` — so the
templates were written host-first and naming *either* host's tool is wrong for the other. On a Claude
run it is the first instruction of every role spec naming a tool that does not exist.

All fifteen were prose; none was a settings entry or a frontmatter `tools:` list, so all became
capability language — "**Open** the reference image", "**Open** what you just rendered" — with the
imperative kept, since the point was never the tool name but that looking is mandatory. The emitted
`CLAUDE.md` now carries one line saying what "open" means and why it is not named: *the tool differs
by host and an instruction naming the wrong one stalls the loop at its first step.*

The line to hold for anything similar: **prose gets the capability, settings get the exact name.**
Deny lists and frontmatter are matched literally and must stay host-specific.

### E — the harness claimed an enforcement it did not have

The generated `CLAUDE.md` said `settings.local.json` "denies the shell (`Bash`)". It does not, and
**the code already knew** — the remark on `ShellDenies()` states plainly that a shell which can run
`grep` can read Polson's source and no `Read` deny prevents it. The implementation was honest with
itself and the text it emitted to the agent was not.

Rewritten to describe the three rules as they are: network denied by tool; `Read`/`Grep`/`Glob`
denied over Polson's source by absolute path; and the shell **shaped rather than denied** — about
forty reading and file-transforming verbs auto-approved, with interpreters, image tools, network
clients, nested shells, `rm` and `git` denied, so every mark still goes through `ExecuteScript`. Source
isolation is therefore named as **a convention the agent keeps**, with the reason it matters put in
terms the agent can act on: peeking would not fail the run, it would make it worthless.

Two things were added because of what the last run did with the old text:

- The "prove it" instruction is re-pointed at the claim that *is* real and *can* go stale — the path
  denies, whose absolute paths were fixed at generation time and name nothing if the checkout moved.
- An explicit standard: *`echo` succeeding proves the shell runs; it proves nothing about which paths
  are reachable through it.* The Critic filed a breach on exactly that inference. A security finding
  is held to the same standard as a measurement, and an unearned one is worse than none because
  somebody will act on it.

`TestHarnessIsolationTellsTheTruthForEachHost` had pinned the old wording, so it was updated rather
than deleted — its intent was always right. A second test,
`TestTheClaudeHarnessDoesNotClaimTheShellIsDenied`, pins the correction itself: the text must not say
"denies the shell", must name the convention, and must carry the `echo` standard.

### …and then the isolation machinery turned out to be in four workflows that did not want it

`IsIsolated` reads the template for `{{ISOLATION}}`, and **five templates carried it** — `harness`,
but also `comic`, `comic_studio`, `drawing` and `painting`. So four design workflows were emitting an
evaluation harness's absolute-path deny rules *and* instructing the agent to spend its first action
proving a sandbox that exists for someone else's benefit. The generator's own comment asserted the
opposite — *"a client design project has no reason to deny reading anything and does not get these
rules"* — which is how it went unseen: the code documented the intent and the templates did something
else.

`{{ISOLATION}}` is now on `harness` alone. The four design workflows carry **`{{PROJECT_DIR}}`**
instead, a new `_shared` fragment saying where the work lives — positive, general, and phrased as a
reason rather than a prohibition: the run record accounts for this directory, `polson report`
reconciles against it, and a project is archived and replayed as a unit, so a file written outside it
is not part of the run in any sense that survives. It closes with the line that makes it actionable:
*if you need something that is not in this directory, ask the director rather than going to find it.*

Three judgement calls inside that, worth knowing:

- **`ShellDenies()` stays universal**, because it is not an isolation rule. It is provenance — every
  mark through `ExecuteScript` so the record can account for it — plus `rm` and `git`, and a live run
  already proved `rm` necessary by deleting its own `findings.md`.
- **`comic_studio` keeps its own one-paragraph "no peeking" note.** It does collect findings, so the
  sentence earns its place; it was the deny rules and the prove-it ritual that did not.
- **Neither of the narrower rules proposed would help.** Denying execution of `Polson.CLI.dll` is
  already covered more broadly by `Bash(dotnet:*)`, which catches every invocation; and "deny reading
  any `Polson.*` directory" is exactly what `SourceDenies()` already emits for `Read`/`Grep`/`Glob`.
  Neither can be made to cover Bash, because those rules match command strings rather than resolved
  paths — which is why the harness text now calls it a convention.

The direction this settles: **guardrails in the prompt have outperformed permission syntax on this
project.** `_shared/engine_only.md` measurably cut permission prompts where rule-wrangling had not,
and `project_dir.md` is written in the same register deliberately.

**Still open from the harness group:** **A/F** (subagents refused `findings.md` four times for four,
filename-keyed, while `critique_log.md` wrote fine — needs a design call between routing reports
through the orchestrator and not instructing subagents to write files), **B** (`reference_images/`
absent while four of six Critic audit categories compare against it), and **#23** (`Grep` rendering
some `//` comments as `\`).

**Unresolved and worth measuring:** whether Claude Code applies path denies to `Bash` at all. The
cs-5 director probed `ls` against a denied tree three times and was refused; the remark on
`ShellDenies()` asserts the opposite. Both cannot be right. It does not change any decision above —
the scoping is correct either way — but nothing should lean on it until someone runs the probe.

---

## 11b. *Imaginative Drawing* is excluded, and the audit below is withdrawn with it

**Read §11 as history, not as instructions.** The audit it describes was deleted.

Page 3 of the book carries, under "1st edition / © John Guy 2025":

> This work is intended to be freely shared, including for use as teaching material, but only in its
> entirety. Do not share or distribute parts, pages, or images from this work separately.
> Additionally, I do not grant permission for this work or any part of it to be used to train machine
> learning or artificial intelligence.

Both clauses bear on what the manuals were doing. A manual distils **parts** and serves them to
agents through `polson://manual/*`, which is distribution of parts. And while a retrieval corpus is
not *training* in the technical sense, its purpose is to let an AI system do what the book teaches —
which is what the second clause declines. Reading "train" narrowly enough to permit this arrives at
the convenient answer rather than the honest one. The author is being generous, and specific about
the two things he does not want; that deserves to be taken at face value.

**Done on 2026-09-02:**

- `docs/audit-imaginative-drawing-ch4.md` **deleted** — it was a direct distillation including
  near-quotes.
- Manuals 05–09: `Source Reference` lines replaced with a *Sources under revision* notice, every
  `Core Insight from the Book` relabelled `Principle`, and the passages distilling his text removed —
  the mannequin forms and knee/forearm/malleoli note in 08, the compositional-structure and
  Exercise 5.8 passages in 09, the three-point lighting distillation in 07, the withdrawn-page notes
  in 06 and 09. `Book concept`/`Book symbol` map columns became `Concept` in 06–09 only; 10 and 11
  keep theirs, since Bokhua and Williams are cited legitimately.
- Removed from `docs/Polson.core.md`, `PolsonManuals.cs` and `docs/memory-design.md` (§1d, §1e, §1f
  and §4 rewritten — §1d had recommended the book *because of* its "freely shared" line, having
  stopped reading at the first clause).
- `reference/README.md` row replaced with a **DO NOT USE** verdict quoting the notice.
- **`CLAUDE.md` §0 gained a guardrail**: a clean codepoint scan says the bytes are safe and says
  nothing about whether we may use the work. Read the front matter for the author's terms and record
  them in the same ledger row. Where an author has *not* refused, ordinary scholarly use applies —
  distil, own words, cite the chapter, never reproduce at length, as this project already does for
  Bokhua.

**What the manuals are now:** standard studio practice with no attribution. Honest, but unsourced —
every claim in 05–09 is pending a citation and should be treated as unverified until it has one.

**Still carrying a copy:** `tests/multi_agent/comic_studio/cs-1/manuals/07_*.md` is a tracked, stale
copy of the old Guy-citing manual inside a historical run directory. It duplicates `docs/manuals/`
and is an *input* the run was given rather than anything it produced, so deleting it loses nothing —
but it sits in a frozen run directory, so it is flagged rather than removed.

**Replacements to source from**, none carrying a comparable restriction: Loomis's *Figure Drawing for
All It's Worth* (the canonical home of the head canon and the mannequin, and Manual 01 already names
the Loomis method) and *Creative Illustration* (tone, value, composition), and Faragasso's *Mastering
Drawing the Human Figure* (Reilly-method construction). All three are image-only and need OCR, and all
are in copyright — same handling as Bokhua. The gap is perspective for Manual 06, where neither is
primarily a perspective text.

**The reassuring part:** `ConstructiveDrawingToolkit` barely depended on him, and §11's audit is what
proves it — the eight-head canon, the armatures and "Notan" are all absent from the book, the Loomis
head method is Loomis, three-point lighting is standard, and diagonal division is Renaissance. The
book was the citation stapled to those things, mostly wrongly. The code needs re-sourcing, not
rewriting.

---

## 11. The *Imaginative Drawing* audit — WITHDRAWN, see §11b — `docs/audit-imaginative-drawing-ch4.md`

Opened before writing #14, #19 and #4, on the grounds that all three are Chapter 4 material and the
Chapter 4 citations were already known to be bad. Ledger row recorded. The four things that change
what gets built:

- **The book has no text layer.** 214 pages of `chapter4_anatomy.pdf` are 214 JPEGs with zero fonts;
  extracting the lot yields 428 bytes of newlines. OCR (`bin/tesseract`, 200 dpi) is the only route,
  and it is good. This is why every previous citation was unverifiable rather than merely wrong.
- **The chapter extracts stop one page short of the models.** `chapter4_anatomy.pdf` page *N* is book
  page *N* + 338; its last page is book p552 and ends mid-sentence, immediately before §4.6. Gesture
  Lines, Box Forms and Mannequin Form (pp. 553–558) — the material `createMannequinFigure` cites —
  are in the full PDF only.
- **The eight-head canon is not in this book.** *Proportion* appears in the TOC only at pp. 583 and
  624, both composition, and the head model ends by telling the reader to experiment with proportions.
  Eight heads is fine convention; the citation in Manual 08 is not.
- **`DrawComicMouth` has the book's model inverted, and the trio's proportional data is computed and
  then discarded.** `createLoomisHead` hands over `eye.width`, `eye.height`, `mouthGuides.upperLipY`
  and `mouthGuides.lowerLipY`; `DrawComicEye` reads none of the first two and `DrawComicMouth`
  references the lip guides **zero times**, using `center.Y + 12/16` instead. **#14 and #19 are one
  defect**, and smaller than either finding assumed.

Order recommended in the audit: fix the citations first (cheap, and the manuals are what the next
agent reads), then #14 + #19 as one pass, then #4 — for which the book supplies the poser interface
directly, as a joint cross-stroke carrying position, **width and angle**.

---

## 12. Re-sourcing the manuals — the state to pick up from

**Start here.** This section supersedes §11/§11b for anything about the manuals' sources, and is the
live picture as of 2026-09-02.

### The intent, because it changes how the manuals get written

Polson exists so an agent can be **creative in ways a diffusion model cannot** — by working in
procedures it can reason about, adapt and combine, rather than in a prompt. So the manuals are
**not prescriptive**. Where the field has more than one school, carry both, say what each is for, and
name the SDK calls that implement each path. The agent chooses; it can also take proportions from one
school and construction from another, which is what working artists do.

The corollary is a rule with teeth: **a manual must not describe a choice the SDK cannot express.**
Manual 08 presented Loomis-vs-Reilly shoulder widths while `shoulderSpan` was hardcoded — a manual
inviting judgement the code forbade. That is worse than a prescriptive manual, and the fix is a
parameter, not a paragraph.

### Sources now in use

`Imaginative Drawing` is **excluded** (§11b, and `CLAUDE.md` §0 — do not re-add it). Five replacements
are ledger-scanned and clean; the ledger row for each carries its licence and handling. The last two
were added on 2026-09-02 and closed the two largest gaps between them.

| Source | Covers |
| :--- | :--- |
| **Loomis**, *Figure Drawing for All It's Worth* (1943) | proportion canon, mannequin frame, contour vs line, horizon measurement |
| **Loomis**, *Creative Illustration* (1947) | tone intensity, key/value, edges, attention devices, informal subdivision, eye level |
| **Norling**, *Perspective Made Easy* (Dover 1999 / Macmillan 1939) | horizon and VP definitions, cylinders, diagonal division, spacing into depth |
| **Faragasso**, *Mastering Drawing the Human Figure* (1998) | Reilly-method structure lines and torso construction |
| **Loomis**, *Successful Drawing* (1951) | the three laws of light, cast-shadow projection, planes, the five P's, lighting consistency |
| **Loomis**, *The Eye of the Painter* (1961) | shadow colour as cause and effect, the primaries rule, four-value pattern, concentration of chroma |

**Loomis cites Norling by name** (*Figure Drawing* p. 36), which is real evidence the figure and
perspective halves are not being stitched from incompatible traditions.

**Faragasso's rights clause names "information storage and retrieval system"** — so distil and cite,
but **do not put its OCR into a retrieval corpus**. Same line already drawn for Janson.

The first four books are **image-only, no text layer**. OCR (`bin/tesseract`, 200 dpi) is the only route,
and it is good on prose pages. **Hand-lettered plates defeat it** — Loomis's diagrams especially — so
render the page and *look* at it rather than trusting the OCR. Every number in Manual 08 §1 was read
that way.

**The two 2026-09-02 additions are different and much cheaper to work with:** both are Internet
Archive scans carrying a **text layer**, so `bin/mutool.exe draw -F txt` extracts each whole book in
one command (262 KB and 135 KB) and the ledger scan covers the entire text rather than a sample.
Page numbers survive as bare-integer lines in the extraction, so a citation can be *verified* rather
than estimated — `awk '/^[0-9]{1,3}$/ {print NR": "$0}'` over the extraction gives the page markers,
and every page number added to a manual this session was checked against the marker either side of
the passage. Three of the first-draft citations were off by one page and were corrected that way.

### Where each manual stands

| Manual | Sourced | Still pending |
| :--- | :--- | :--- |
| 05 Observation | §1, §2 (Loomis), §3 contour (Loomis), §4 planes + §5 pattern (*Successful Drawing*, *Eye of the Painter*) | — |
| 06 Perspective | §1, §3, §4 (Norling), §5, §5a (Loomis) | §2 |
| 07 Lighting | §1 (Loomis, incl. edges, + three laws), §2 (*Successful Drawing*), §3 (*Eye of the Painter*), §4 (*Successful Drawing*) | — |
| 08 Anatomy | §1, §2 (Loomis), §3 (Faragasso) | §4 expressions |
| 09 Composition | §1, §2 (+ fifth device), §3 key, §4 (*Eye of the Painter*) | §1's four fixed armature types |

Every header states which sections are cited and which are pending, so a partial file cannot be
mistaken for a finished one.

**~~Negative result worth keeping:~~ overturned on 2026-09-02, and the way it was overturned is the
point.** This section previously recorded that there is *no cast-shadow construction in any of the
four books*, so Manual 07 §2 had none and none had been invented. **Successful Drawing pp. 83–88 has
it**, and it is the construction `Drawing.projectCastShadow` already implements: three things to
establish — the light source, the angle of light, and the vanishing point of the shadows on the
horizon directly beneath the source — then lines from the source through the top corners crossed with
lines from that VP through the bottom corners. It also carries the sphere (central ray through the
centre, shadow centred where it meets the ground, always an ellipse) and the cone.

A negative result over a four-book corpus was a claim about the corpus, not about the field, and it
read as the stronger thing. Norling's Steps 18–20 remain unusual perspective, uphill/downhill and
mechanical perspective.

### Code changed by the reading

- **`drawPerspectiveCylinder` caps are axis-aligned.** Norling p. 137 — the long axis forms a T with
  the cylinder's upright line. The previous conjugate-diameter construction tilted the cap away from
  the centre of vision: measured 0.5 px on axis, **13.5 px** at 250 px left, **15.5 px** at 300 px
  right on a 140 px cap, which reads as a leaning bottle. Foreshortening still comes from the grid —
  only the tilt is discarded — and the fix **deleted five helpers**, because an axis-aligned ellipse is
  what `ctx.Ellipse` draws natively. Pinned by `TestCylinderCapsKeepAHorizontalMajorAxis`, whose
  off-axis cases are the whole point; the on-axis case passed throughout and caught nothing.
  A deliberate departure from strict projection — a real wide-angle projection *does* tilt a circle
  off axis. Right for a screen-space grid; revisit if the toolkit grows a metric camera.
- **`createMannequinFigure` takes `options.shoulderSpanHeads`** (head units, default `1.8`
  unchanged). Loomis `2.33` figure-widest / `2.0` cape; Faragasso `2.67`. Three canons, three
  different measurements, none wrong — and Faragasso adds that proportions *vary greatly in real
  life*. `TestShoulderSpanFollowsTheChosenCanon` covers all three.

### Two findings that change advice already written

- **Edges are "lost and found"** (Loomis, *Creative Illustration* pp. 102–103). Manual 09 §3 had
  treated merged values as a defect with a fix; Loomis treats convergence as an *opportunity* to lose
  an edge and spend the sharpness elsewhere. Both manuals now cross-reference, with the test being
  what the edge is doing: a merge on the subject's silhouette is a defect, a merge between secondary
  masses is a saving. Every sharp edge spends attention and there is a fixed amount to spend.
- **"CSI" is house vocabulary, not a school**, and it is baked into the API as `sCurveTo` / `cCurveTo`.
  It describes the **mark**; Loomis p. 24 describes the **edge** — a line is a wire, a contour is an
  edge, either a sharp limitation or a rounded and disappearing one. Manual 05 §3 now carries both,
  because Loomis tells you which edges deserve a line at all and CSI tells you what to draw once you
  have decided.

### Capabilities the reading identified and the SDK lacks

Both are described in the manuals with an explicit **"no such call exists yet — do not write one into
a script expecting it to resolve"**, because `TestEveryManualBindsToRealSdkCalls` failed when a
proposal was written in call syntax. That guard works; respect it.

1. **Informal subdivision** (Manual 09 §1, Loomis p. 36). A generative armature: cut the space
   unequally avoiding ½/⅓/¼, one whole-space diagonal, a horizontal where it crosses, then **one
   diagonal per rectangle, never two** — two would halve it equally, which is what the method avoids.
   No two spaces come out duplicates. The only genuinely algorithmic composition method here, and
   `createCompositionGrid` offers four fixed templates and no generator.
2. **Spacing equal intervals into depth** (Manual 06 §4, Norling p. 113). From two posts, a line from
   the top of the first through the centre of the second lands where the third goes. Generates a
   receding rhythm — colonnades, fences, window bays — where `subdividePerspectiveQuad` can only
   divide a quad you already have.

### Where to pick up

1. ~~**Manual 07 §§2–4 and Manual 09 §4**~~ — **closed 2026-09-02** by the two new books, along with
   Manual 05 §§4–5. *Creative Illustration*'s colour part is still unread, but §3's warm/cool rule is
   now sourced from *The Eye of the Painter* instead, and its account is better: a shadow's colour is
   the colour of whatever light reaches it, so one shadow can be warm at the bottom from ground
   bounce and cool at the top from the sky. The complementary-hue rule the manual used to state was a
   heuristic standing in for that.
2. **Manual 08 §4 (expressions)** — still no source identified, now across six books.
3. **Norling Steps Five–Six** (two vanishing points) for Manual 06 §1, and the "Dividing the Circle"
   pages of Step Fourteen for §3's cap ellipses.
4. **The two missing capabilities above**, if a run wants them.
5. **cs-5's remaining findings**: #4 (joint poser — Loomis's *"never draw the limbs straight and stiff
   and without spring"* is the design note), #14 + #19 (closed mouth; the trio's proportional data is
   computed by `createLoomisHead` and discarded by the draw methods — one defect, smaller than either
   finding assumed), #13/#18 (path recorder). Harness: **A/F** (subagent `findings.md` refusal — needs
   a design call), **B** (`reference_images/` absent), **#23** (Grep display artifact).

### Two process notes

- **`tests/multi_agent/comic_studio/cs-1/manuals/07_*.md`** is still a tracked, stale copy of the old
  Guy-citing manual. It duplicates `docs/manuals/` and is an input rather than a run output, so
  deleting it loses nothing — flagged rather than removed because it sits in a frozen run directory.
- **Editing `ConstructiveDrawingToolkit.cs` by line number has bitten twice** this session: once from
  stale offsets after an earlier edit, once from a brace-walker running past an expression-bodied
  member into the next method. Both times the script's own guard caught it before writing. Match on
  the signature and handle `=> …;` members.

---

## 8. Where to pick up — *earlier session; see §12 first*

**Ordered by what would most improve the next run.** Items here predate the manual
re-sourcing work in §12; where the two disagree, §12 is current.

- **Rebuild `bin/cli`.** Most of this session was built with `-p:SkipCopyToBin=true` because a live
  agent session holds it open. `scriptFile`, `InspectScript`, `has`/`suggest`, the shell policy, the
  `Agent` allow entry and `--reset` archiving reach a project only after a rebuild.
- **Read `cs-5`'s `findings.md`.** 28 KB written by four agents that had just spent five hours in the
  API. It is the cheapest source of real defects available.
- **Watch for `absent` probes.** The leniency in §6 is on probation: if agents thrash on names that
  do not exist, the record will now say so, and that is the signal to reconsider.
- **`--comment`/compound shell matching is unverified.** Whether Claude Code matches a compound
  command against every segment or only the first decides whether the `rm` and `node` denials are as
  strong as they look. `cd … && sed …` ran unprompted while `cat … ; echo done` prompted, which is
  consistent with per-segment matching and does not prove it.
- **The AST edit side**, deliberately not built. `InspectScript` answers structural questions without
  reading a file — outline, find, references as resolved identifiers, so `SHAFT` does not match
  `SHAFT_TOP`. The *write* side would compete with `sed`, which has fifty years of understood failure
  modes, and a patch tool that resolves the wrong node and returns success is the `BrushPreset.color`
  scar with a bigger blast radius. Revisit only if `sed` demonstrably fails a run.
- **Two `run.start` events per session**, reproducibly. Two MCP servers briefly exist at startup and
  one exits; sequence numbers do not collide, but every host-driven record shows a duplicated open
  and close, and the UI shows `RUN ENDED` twice.

### Carried forward, still open

- **Attribute the spine.** `_code_server` passes no `agent`, so every execute/inspect event codes as
  one actor — even now that the transcript half is attributed by role. Stage-based is the only
  mechanism that works under every host.
- **Peer-to-peer subagent coordination.** Parked deliberately; the facilitator-directed pipeline in
  `comic_studio` is a different thing and now works.
- **Name the registered subagents in `GEMINI.md`**, or stop emitting `agents.json`.
- **A second image provider**; **Agent Memory Bank**.
- **Magick.NET is in `CLAUDE.md` §3 and in no `.csproj`.**
- **`reference/README.md` is gitignored** by `reference/*`, deliberately: a scan verdict describes the
  bytes on one machine, so shipping it would invite trusting a match nobody checked. A clone starts an
  empty ledger, which is what the guardrail asks for anyway.

### The flaky one

`Polson.Tests.Drawing.IrradiationCompensationTests` fails intermittently **only** under a
full-solution parallel run, a different test each time, and passes standalone every time. Not
diagnosed; probably contention on a shared Skia font or render resource. Re-run the class alone before
believing you broke it, and **do not bisect by stashing `src/`** — the tree usually carries
uncommitted work and stashing pulls it out from under the untracked tests that depend on it.

---

## What the record now looks like

A slice of `cs-5` as the dashboard shows it, which is the shortest way to see what changed:

```
19:11:22  +3s   inker  TOOL      ExecuteScript  artwork.js
19:11:27  +3s   inker  TOOL      Read  …/artifacts/stage3_inker.webp
19:11:51  +18s  inker  RENDERED  artifacts/stage3_greyscale_check.webp
19:12:53  +55s  inker  EXECUTING scripts/0070.js
19:14:41  +1m   inker  TOOL      Bash  cat >> critique_log.md << 'EOF'…
                       ↑ 24 minutes, no result — waiting on a permission prompt
```

The timestamps, the gaps, the tool arguments, the role attribution and the at-work line were all
invisible at the start of the session. The last line cost an hour before any of them existed.
