# Session Handoff — 2026-09-05 (seventh session)

State after the session that **finished the motion score** and then turned the studio toward
**infographics** — the direction the business case actually rests on, and the first one where the
competition is a diffusion model rather than another drawing tool.

**Tests: 1,645 .NET at the close of the seventh session; 1,987 .NET + 39 Python as of §21;
2,038 .NET as of §22; 2,063 as of §22.3; 2,068 as of §22.4.**
Sections are appended, never rewritten, so everything below §17 is history and remains accurate as
such.

> **§22 is the current state** — the filter and stylesheet surface, now tested and documented, which
> closes §21.7 items 1 and 2, plus vector brushes (§22.3) and grain (§22.4), which close item 3.
> **§22.5 is the pick-up list; start there.**
>
> Earlier: **§21** is reference photography, the raster/vector boundary, and the
> `vector_infographic` workflow. Its §21.6 has since been committed as `591d510`.
>
> Earlier: **§§17–20 were the seventh session.** §17 is the motion score, which lands §16's
> plan and **reverses two of its decisions by measurement** — read it before touching `Mina` or
> `MotionTimeline`. §18 is the infographics thread: the sources and **their two different licence
> classes**, Manual 13, and the `Chart` toolkit. §19 is the prescription/scope distinction, which
> touched every manual. **§20 is the pick-up list** — start there.
>
> Earlier: **§14** is the drawing corpus and **§15** its queue, both still accurate; §15's designs and
> measured constants are worth reading before adding to `Drawing`. **§16 is now history** — its plan
> is built, so read §17 rather than §16 for what the motion surface does. §13 describes the manuals
> and the `pose` parameter; §12 is the session before that. §§1–8 are the fifth session
> (observability, `scriptFile`, the Claude profile); §§9–12 are the sixth (cs-5 findings, encode cost,
> the Guy exclusion, and re-sourcing the manuals to Loomis, Norling, Faragasso, Hampton and Janson).

> [!IMPORTANT]
> **A different thread is open and is time-boxed: the Agentic Cinema hackathon.**
> `docs/agentic-cinema-assessment.md` is the cold-start brief — contest rules, the eligibility checks,
> and what was verified in Google's ADK source. Assessed 2026-09-03, deadline **14:00 PT 2026-09-09**.
>
> The short version: Polson is **eligible** (initial commit 2026-08-23 is inside the contest period,
> and no rule bars a concurrent hackathon entry — both checked, not assumed). ADK plugs `bin/cli` in
> unchanged through `McpToolset` + `StdioConnectionParams`, and **Agent Engine deploys a container
> rather than a pickled agent**, so the .NET engine survives deployment.
>
> **This paragraph said "no work started" for three days after that stopped being true, and a later
> session believed it** — the ADK entry point, the container and the Cloud Run deploy are done. What
> remains is in §20: **Parallel's Search API, which gates eligibility**, public access, mounting the
> studio UI, and the video.
>
> **The container finding outlives the contest** — it is the unlock for Milestone 5, running the studio
> outside the Antigravity IDE.

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

`Imaginative Drawing` is **excluded** (§11b, and `CLAUDE.md` §0 — do not re-add it). Six replacements
are ledger-scanned and clean; the ledger row for each carries its licence and handling. The last three
were added on 2026-09-02 and between them closed every gap the table below had recorded.

| Source | Covers |
| :--- | :--- |
| **Loomis**, *Figure Drawing for All It's Worth* (1943) | proportion canon, mannequin frame, contour vs line, horizon measurement |
| **Loomis**, *Creative Illustration* (1947) | tone intensity, key/value, edges, attention devices, informal subdivision, eye level |
| **Norling**, *Perspective Made Easy* (Dover 1999 / Macmillan 1939) | horizon and VP definitions, cylinders, diagonal division, spacing into depth |
| **Faragasso**, *Mastering Drawing the Human Figure* (1998) | Reilly-method structure lines and torso construction |
| **Loomis**, *Successful Drawing* (1951) | the three laws of light, cast-shadow projection, planes, the five P's, lighting consistency |
| **Loomis**, *The Eye of the Painter* (1961) | shadow colour as cause and effect, the primaries rule, four-value pattern, concentration of chroma |
| **Loomis**, *Drawing the Head and Hands* (1956) | the muscles of expression, the sharp/round mouth corner, hand block forms and proportions |
| **Hampton**, *Figure Drawing: Design and Invention* (2009) | the C/straight/S line set and the wrapping line, head proportions by recursive halving, the 3:2 phalanx ratio, the gesture/shape/volume method and the active/passive squash rule |

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
| 06 Perspective | §1, §3, §4 (Norling), §5, §5a (Loomis), §2 (*Successful Drawing*) | — |
| 07 Lighting | §1 (Loomis, incl. edges, + three laws), §2 (*Successful Drawing*), §3 (*Eye of the Painter*), §4 (*Successful Drawing*) | — |
| 08 Anatomy | §1, §2 (Loomis), §3 (Faragasso), §4 (*Drawing the Head and Hands*) | — |
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

### Where to pick up — manuals, as of 2026-09-02

**All nineteen manuals now carry an attribution line**, and manuals 01–09 plus 19 are cited to the
page. What is left, in the order worth doing it:

1. **The manual index misstates the corpus, and agents read it first.** `PolsonManuals.BuildIndex()`
   (`Knowledge/PolsonManuals.cs`, the literal string near the top) tells every agent the manuals
   distil *"Loomis, How to Draw Comics the Marvel Way, Bokhua's Principles of Logo Design, Tubik"*.
   **No manual cites Marvel Way**, and the list omits Janson (both volumes), Hampton, Norling,
   Faragasso, Robin Williams and Doyald Young — most of what the manuals actually stand on. Same
   defect class this whole section has been clearing, in the highest-traffic place. Minutes of work.
2. **Manuals 10–13: `Credits & Theoretical Foundation` lines with no page citations**, over three
   books with **no ledger row** — Tubik *Magazine Issue 2*, Robin Williams *The Non-Designer's Design
   Book*, Doyald Young *Fonts and Logos*. All three are on disk, so this is a scan-and-cite pass, not
   an acquisition. Bokhua and EpicInfographics are already ledgered, so 10 and 13 are half-covered.
   This is the last of the pre-audit condition.
3. **Coverage floors say where the manuals under-serve the SDK** (`ManualCoverageTests.Floors`):
   Scale **54%**, Logo 65%, LogoType/Layout/Css 66%, VectorLogo 74%. Scale is the numeric spine of
   every chart and Manual 13 is the infographics manual — the widest gap between what the SDK can do
   and what a manual tells an agent to reach for.
4. **Hampton's unread chapters**: the arm, forearm, **leg and foot**, drapery, weight distribution,
   the back, and light and shadow. **The foot is the hand's mirror** — `createMannequinFigure` still
   ends legs in box feet exactly as it ended arms in box hands, and Manual 19 is the template for
   what closing that looks like.
5. **Two capability gaps left open deliberately**, both carrying the standing "no such call exists
   yet" disclaimer so `TestEveryManualBindsToRealSdkCalls` stays green: the **expression muscle
   matrix** (Plate 21's relaxed/contracted table across worry, frown, laugh and anger, which composes
   where six presets cannot), and **informal subdivision** (Manual 09 §1) with **spacing equal
   intervals into depth** (Manual 06 §4).
6. **Still-unread source material**: *Creative Illustration*'s colour part; Norling's Steps Five–Six
   (two vanishing points) for Manual 06 §1 and the "Dividing the Circle" pages of Step Fourteen for
   §3's cap ellipses; and Marvel Way ch. 12's **reduction test** — its overworked/underworked panels
   are judged by whether the art still reads *after reduction to printed size*, which `bitmap.resize`
   makes directly checkable and which Janson has no equivalent for.
7. **cs-5's remaining findings**: #4 (joint poser — Loomis's *"never draw the limbs straight and stiff
   and without spring"* is the design note), #14 + #19 (closed mouth; the trio's proportional data is
   computed by `createLoomisHead` and discarded by the draw methods — one defect, smaller than either
   finding assumed), #13/#18 (path recorder). Harness: **A/F** (subagent `findings.md` refusal — needs
   a design call), **B** (`reference_images/` absent), **#23** (Grep display artifact).

### The manuals carry no editorial history — this is a convention, not an oversight

On 2026-09-02 every audit record was stripped from all nineteen manuals and from `Polson.core.md`:
"audited on <date>", "this manual previously carried no citation", "the previous rule is withdrawn",
"it used to clamp silently", "earlier revisions printed", and Manual 01 §2a's twelve-row *was → now*
table. **They are our notes, and they belong here rather than in what agents read.** Everything
removed is recorded in this section.

**Two categories deliberately survived, and re-adding history should not disturb them:**

- **Provenance labels** — "the pixel widths are the studio's", "Loomis gives no phalanx ratio", "the
  four-stage ribbon is ours", "§1's four fixed armature types remain unsourced". These read like
  audit notes but are a *property of the content*: they tell an agent how much weight a number will
  bear. Stripping them would undo the point of the re-sourcing work.
- **Runtime measurements** — "a QA pass died at roughly 480k sampled pixels", "the Perlin shaders
  turned 700 × 500 px of brick olive green and cost a full iteration". These calibrate how fast a
  trap arrives; they describe the runtime, not the document's past.

The test to apply when adding a note: **does it describe the thing an agent is working with, or what
a file used to say?** Only the second is a changelog.

### What the 2026-09-02 reading changed in code

- **`CreatePerspectiveBox` no longer clamps, it refuses.** `tL = min(0.85, width / lenL)` silently
  shortened any side longer than 85% of the anchor-to-VP distance, so an over-large box came back
  smaller than asked for and looked deliberate — doc/code drift too, since Manual 06 §2's formula
  never mentioned the clamp. It now throws an `ArgumentOutOfRangeException` naming the measured
  fraction, the anchor-to-VP distance, and the largest extent that would have been accepted. The
  0.85 is now a named `MaxRecession` constant, and the two per-axis blocks collapsed into one
  `Recede` helper. Pinned by `TestPerspectiveBoxRefusesASideThatReachesItsVanishingPoint`, which
  checks both sides and asserts on `ParamName` so a future refactor cannot swap width for depth
  silently.
- **The limit is documented and pinned to the doc.** `docs/Polson.core.md` now says a side past 85%
  *throws* — explicitly a script-ending error rather than an `Assets.*`-style failure object, which
  is a distinction the SDK gives an agent no other way to learn — and shows the pre-check, since the
  grid hands back `vpL`/`vpR` as `{ x, y }`. `TestDocumentedBoxRecessionLimitMatchesTheToolkit`
  **parses the percentage out of the reference** and tests the call against it, so changing
  `MaxRecession` without the doc fails. Verified by flipping the constant to 0.75 and watching it
  fail, rather than trusting that it would.

### Two capability gaps the head-and-hands reading opened — one closed

The first is described in the manuals with the standing **"no such call exists yet"** disclaimer, so
`TestEveryManualBindsToRealSdkCalls` stays green. The second was built.

1. **Expression is a muscle matrix, not six presets.** Loomis's Plate 21 marks each muscle *relaxed*
   or *contracted* across worry, frown, laugh and anger — a sparse matrix that **composes**, where
   `applyFacialExpression(head, name, intensity)` takes one name and cannot mix two. Worry and a
   frown share the brow and differ only at its inner corner, and today that is inexpressible.
2. ~~**There is no hand construction at all.**~~ **Built 2026-09-02.** `Drawing.createHandFigure(...)`,
   `drawHandWireframe` and `drawHandSolid`, with `ctx.drawHand(hand, solid)` as the shortcut, plus
   **Manual 19** — which auto-registered, since the manual corpus is an embedded-resource wildcard.

   The scale is Loomis's and hangs off one measurement: the middle finger from its back knuckle is
   **slightly over half the hand** (0.52), the palm is the rest (0.48), and the palm is **slightly more
   than half the hand wide** (0.55) — so it comes out slightly wider than long, which falls out of his
   two sentences rather than being chosen. The other three fingers he gives as *reaches* rather than
   lengths, and converting those needs a nail length and a phalanx length he never states; Manual 19
   says which numbers are his and which are the studio's reading of a sentence.

   **Two things worth carrying:**

   - **"The thumb is turned at right angles to the other fingers" describes the plane it moves in, not
     the angle on the page.** Drawn at a literal 90 it sticks straight out of the side of the wrist,
     which is exactly what the first implementation did. `thumbDeg` is the drawn angle and defaults to
     46; `curlDeg` reaches the thumb at half strength and mirrored, because a curl closes the fingers
     *toward* the palm and has to swing the thumb *across* it.
   - **The arcs are a free check.** Loomis: the knuckles make a flat curve across the back, and the
     curves deepen row by row toward the fingertips. That is a *consequence* of the fingers differing
     in length, so `TestHandArcsDeepenTowardTheFingertips` doubles as a test that the proportions
     survived — verified by flattening the ratios to equal and watching it fail alongside the reaches
     test.

   Both defects were found by rendering rather than by tests, in this order: the thumb sticking out
   sideways, then the thumb floating clear of the palm once its angle was right. The second is why
   `hand.thenar` exists — the thumb muscle Loomis calls by far the most important in the hand, and the
   thing that makes the thumb read as attached rather than stuck on. Both renderers and any script draw
   the same mass.

**~~Also worth knowing: Manual 01 carries no `Source Reference` line at all~~ — audited 2026-09-02.**
It now cites *Drawing the Head and Hands* Plates 1-2 and 18-19, and the audit found more than a
missing citation:

- **Three invented numbers.** The temporal slice as *"2/3 R"* (Loomis says only *"a fairly thin
  slice"* and gives no fraction), `hairline = crown + 0.35R` (the model carries no `R` at all), and
  the yaw/far-eye formulas in §3 (the code uses `sin(yaw)·W·0.22` and `max(0.45, cos(yaw))`, with no
  ball radius and no `0.85`).
- **"The face is 5 eye-widths wide" is not Loomis.** Plate 19 makes the face **2 units** with the eyes
  at the quarter points, each eye ½ unit: so the face is **4** eye-widths and the head **6**. The
  toolkit's `eyeW = 0.20·W` makes the head exactly **5** — a third value. The one horizontal claim the
  manual had right, ½ unit between the inner corners equalling one eye-width, is now cited properly.
- **`head.eyeLineY` is not `H/2`**, though §2 quoted Loomis's "exactly halfway" as if it were. It is
  `H/2 + 0.02H` — 8 px low on a 400 px head.
- **`head.unit.thirdH` is `H/3` and describes none of the three divisions**, which measure 0.230,
  0.250 and 0.300 `H`. Loomis constructs them **equal** by stepping the forehead off twice (Plate 1,
  step 6), so this is a departure from the method, not just from a number.
- **The stoss was missing entirely** — the point where the brow line crosses the facial middle line,
  which Loomis calls the key point in the construction of the whole head.
- **Loomis contradicts himself on the dome** and the manual now says so: Plate 1's freehand
  construction implies ¾ unit, Plate 18's measured scale gives ½.

**The canon was then fixed** (same day, on the director's call). `CreateLoomisHead` now builds from
Loomis's unit — `H/3.5` — rather than from independent fractions of `H`: half a unit of dome, three
equal units, the eye line at exactly `H/2`, the lip line a third of a unit below the nose, the head
three units wide including the ears, and each eye half a unit so the inner corners sit one eye-width
apart. `unit.thirdH` is the unit and is now the height of each division. Two tests pin it —
`TestLoomisHeadFollowsTheThreeAndAHalfUnitScale` and
`TestLoomisHeadEyeAndNoseSpacingIsOnTheHalfUnitGrid`.

**Expect every head to change.** Heads are wider (0.857 H against 0.72 H) and foreheads taller. The
frozen-artifact rule applies: a re-render of any pre-2026-09-02 head will not match, and that is the
fix landing rather than a regression.

**The ear is the story worth carrying.** Its drawn radius was `0.08·H` where Loomis's ear spans
brow-to-nose — one unit, so half a unit of radius. Correcting the size made a *placement* error
obvious that a too-small marker had been hiding: the ear was pinned at `0.45·W` and never moved with
yaw, so on a turned head it floated clear of the skull. Both are now derived from the plates. **The
unit tests passed at every stage; only rendering the three yaw angles and looking at them surfaced
it** — the same lesson as `drawPerspectiveCylinder`, which also returned a plausible wrong answer.
The wireframe's cranial ball and the ear now come off the landmarks (equator = brow line, top =
crown) instead of fractions that happened to land nearby.

**The jaw was then fixed too, and it was the worst of the three.** Plate 1 step 7 says the jaw line
connects about halfway around the ball **on each side**; the toolkit had the angle at a flat
`0.28·W` that never moved with the turn, and the wireframe path ran `ear -> angle -> chin ->
cheekApex` — where `cheekApex` sits on the **far** side, so it doubled back across the face and
closed into a narrow V with a single-point chin. Now: both stations are the ball's silhouette at the
nose line (`sqrt(R^2 - unit^2)`, foreshortened by `cos(yaw)`), the near one is the far one mirrored
about the cranium axis, and the chin corners come from the two angles rather than a fraction of `W`.
`head.jaw` carries the whole frame — `farStation`, `angle`, `chinFar`, `chinNear`, `nearAngle`,
`nearStation` — so a script can draw its own jaw on the same points.

**Past ~60 degrees the construction runs out**, because the facial axis swings out faster than the
near station comes in; unguarded, the near jaw crosses the chin and the path inverts *silently*.
`nearAngle` is floored just clear of the axis, and `TestLoomisJawStaysOrderedAcrossTheTurn` asserts
the left-to-right ordering at 0/35/55/70/85 degrees — verified by removing the floor and watching the
last three fail. §3's documented 3/4 range is 30-45 degrees, where none of it bites.

Still unsourced and marked as such in §2a: the temporal slice depth (Loomis says only "a fairly thin
slice"), the jaw angle's depth below the nose line, the chin's share of the span between the angles,
and the mouth width.

It went unaudited for so long because the re-sourcing work started from the withdrawn *Imaginative
Drawing* citations, and Manual 01 had none to withdraw — a manual that cited nothing never entered
the queue. **That is the lesson worth carrying: an absent citation is easier to miss than a wrong
one.** Part One still has unread material bearing on the manual — the planes of the head, tilting,
and perspective in the head (Plates 9-11).

### Adding one contrasting author, and what it cost the manuals

*Figure Drawing: Design and Invention* (Michael Hampton, 2009) was added on 2026-09-02 **as a
deliberate contrast to Loomis**, not to fill a gap. It found three things in an afternoon, which is
the argument for reading across schools rather than deeper into one.

1. **It withdrew a claim.** Manual 05 said **"CSI is house vocabulary, not a school"** — the acronym
   came in with the withdrawn Guy text and nobody could find the three lines in the library. Hampton
   opens his gesture chapter with *the lines most crucial to showing a figure are the "C" curve, the
   straight (line), and "S" curve… you will never use any other type of line*. The instructive part
   is the shape of the error: the conclusion drawn from "not in the library" was that the **idea**
   was unsourced, when what was actually true is that the **library** was incomplete. Only the
   initialism is ours.
2. **It corroborated three numbers exactly.** Hampton finds head proportions by **recursive halving
   between landmarks** (brow, bottom of jaw) rather than by a unit scale, and lands on Loomis's eye
   line (⅛ of brow→chin below the brow), nose base (½) and mouth (⅓ of nose→chin) precisely. Two
   methods, fifty years apart, no shared derivation. It also matters practically: halving between
   landmarks survives extreme perspective where a flat unit scale does not.
3. **It replaced an invention with a rule.** Manual 19 flagged the phalanx split
   `0.45 / 0.30 / 0.25` as the studio's guess. Hampton gives the finger bones as a **3:2 ratio** — each two-thirds of the
   one before, so `0.474 / 0.316 / 0.211`. Close on the first two bones; the fingertip was a
   fifth too long. `CreateHandFigure` now uses his, and Loomis's little-finger *reach* ("just reaches the top
   knuckle of the third finger") became **computable** rather than guessed — the test asserts an
   equality now instead of a tolerance band.

4. **It supplied a method, not just facts.** Hampton studies every muscle three times in a fixed
   order — **gesture** (what it does, which decides whether its line is a C or an S), **shape** (one
   nameable silhouette: the sternomastoid is a baseball bat, the pectoral a goldfish with its head
   missing, the rectus a bullet), **volume** (how it sits in space and how the action displaces it).
   Manual 08 §3a is that method, applied to the five muscles `drawTorsoMusculature` draws. The order
   is the point: reversed, you get a correctly-shaped muscle doing nothing.

   Writing it up found two defects in the call it documents:

   - **The deltoids and sternomastoid were never drawn.** `Polson.core.md` had promised both since it
     was written; the code rendered clavicles, pectorals and two flat abdominal tiers and stopped.
     Both are now implemented from Hampton's shapes, and
     `TestTorsoMusculatureReachesTheShouldersAndTheNeck` pins the promise — verified by commenting the
     two calls out and watching it fail.
   - **The abdominal grid was a six-pack in two flat tiers.** Hampton has **eight** sections, with the
     row at the navel flat and each row above bowing progressively toward a peak. Flat tiers are a
     six-pack with no gesture in them.

   His governing rule is also now expressible rather than only describable: **S curve = stretch or
   passive, C curve = pinch or active**, with active shapes squashed and passive ones stretched.
   `drawTorsoMusculature` derives the compression from the figure's own clavicle heights, so setting
   `shoulderTiltDeg` moves the musculature with the pose — which is the same C/S decision as Manual 05
   §3, one author, one system, used at two scales.

He also **disagrees** with Loomis on the hand's split (0.50/0.50 against 0.48/0.52) and adds a fourth
line type, the **wrapping line**, which `Drawing.drawCrossContourHatch(...)` already draws. Manual 19
§1a carries both schools; Manual 05 §3 carries the wrapping line.

**Terms:** ordinary all-rights-reserved — *no part of this book can be reproduced in any form
without prior written consent* — with no anti-AI clause and no retrieval clause. **Unlike the IA
Loomis scans this extraction has no reliable page-number lines**, so cite it by section heading.

### Manual 03 sourced — and the wrong book was proposed first

**The proposal was wrong and the correction is the lesson.** Manual 03 (inking) was going to be
sourced from `books/janson-pencilling/`, on the reasoning that Janson is the comics authority in the
library. He is — but *The DC Comics Guide to Pencilling* and *The DC Comics Guide to Inking* are two
books about two crafts. The pencilling volume has **zero** occurrences of line weight, feathering or
hatching across all 21 chapters, and every "inking" mention in it is production workflow: who inks
the pencils, how rough to leave them, the DC-versus-Marvel handoff. **Checking that took one grep and
should have come before the recommendation.**

The director then pointed at `books/jensen-inking.epub`, which is the inking volume. Extracted with
`epub2md.pl` to `books/janson-inking/` (15 files, 167 KB), scanned clean, ledgered. Page markers
survive as `<!-- p.N -->`, so every citation is verifiable.

**What the audit found, beyond the missing citations:**

- **§2's "Light vs Gravity Rule" is withdrawn.** Janson: **a heavy ink line is really the beginning of
  a shadow** (p. 88). The underside of a jaw is heavy because it is in shadow, not because of gravity
  — the two agree for a figure lit from above and diverge the moment the light comes from below,
  where a gravity rule keeps weighting the underside and a light rule correctly moves the weight to
  the top. It is now the Light Rule, with the light's *distance* as a second control.
- **§1's three tiers have a reason, and two named failure modes.** The hierarchy exists because *the
  contour of a figure should stand out from the background* (p. 80). Too little variety and the panel
  becomes "one gray mass — or mess"; weight that varies *without meaning* is worse than none. The
  pixel widths remain the studio's — no book gives pixels.
- **§4 gained the three shapes of a feathered line** (weighted, triangular, dead), feathering's two
  duties, and the rule that decides whether the section can be used at all: **feathering a figure is
  impossible without establishing a light source**, and you never feather where the light strikes
  directly. `drawFeathering`'s `angleDeg` is therefore not a style choice — it comes from the same
  light vector Manual 07 uses.
- **§5 gained a number and a motive.** Blacks are placed *for composition, not realism*, and **one
  black, one white, one gray** beats a series of grays because in a panel of grays nothing stands
  out. That is Manual 09 §3's notan arriving from the inking side.
- **Two composition rules the manual lacked** (p. 82): never allow a **tangent** — two contours that
  merely touch, lettering included — and **overlap is what creates depth**.

**Also ledgered: `How to Draw Comics The Marvel Way`** (sampled: front matter and ch. 12 "The Art of
Inking"). Read while hunting for an inking source, then set aside once the Janson volume appeared.
Worth returning for one idea Janson does not have: its overworked/underworked panels are judged by
whether the art still reads **after reduction to printed size**, which `bitmap.resize` makes directly
checkable. It is also the book `PolsonManuals.cs` has long told agents the manuals distil, while no
manual cited it and nothing had scanned it — that claim is now at least backed by a ledger row.

### 02 and 04 sourced — every manual now carries an attribution line

Both went to *Drawing the Head and Hands*, which was already ledgered and extracted for Manuals 01
and 08, so neither needed a new ingestion.

**Manual 04 (cel shading and face planes)** — Plate 9 (p. 29) and Plates 32-33 (p. 61). The find is
that **cel shading is Loomis's plane exercise with the tones held flat**, and he writes the
instruction outright in 1956: *think in terms of flat areas in varying tones, and forget surface
wrinkles entirely.* Three consequences went into §2. The planes are the **foundation for** lighting
rather than a product of it — you know the planes, and the light picks which are dark, so you do not
go looking for shadow shapes in a render. They are learned in two sets, **basic then secondary**, and
the manual's five polygons are all secondary ones. And **one light**: *more than one light cuts up the
shadow tones*, which bites hardest in cel shading because every extra source multiplies the flat
regions you have to cut — the same warning Manual 07 §4 carries from *Successful Drawing*, arriving
independently in a different book. Plate 33 also gives the order of work: anatomy and construction,
outline, planes, completion.

Two things in Manual 04 are now labelled rather than left to read as theory: the five planes are the
**studio's selection** (Loomis gives a memorised set, not five named shadows), and **§3's palettes are
a character brief** — they were written for one red-haired pirate in a navy coat, and the hex values
are that character's. Keep the structure, replace the values.

**Manual 02 (hair and ribbons)** — pp. 71, 77, 99. Loomis: *simple planes are much more effective than
the photographic representation of every strand or curl*, and *look for the mass effect of forms in
the hair rather than the detail*. The general case behind it is the best line in the chapter — **each
hair in an eyebrow is detail and minor truth, but carries little significance; each blade of grass is
detail, but we may be more interested in the whole hillside.** Hair is a hillside, and the strand is
true and insignificant. That is precisely the trade a per-fibre renderer gets wrong and a ribbon gets
right, so it is the citation the manual most needed.

What is *not* his is now marked: the four-stage ribbon is the studio's construction, and the
15-35 px cushion is a studio pixel range that should scale with `head.unit.H` rather than being
treated as a constant. A note was added that ribbons are for locks that actually move — a static hair
mass is a *plane* in Loomis's sense and wants one filled silhouette with two or three flat tones,
which is Manual 04 §2 applied to hair.

**All nineteen manuals now carry an attribution line.** What remains is quality rather than presence:
Manuals 10-13 have `Credits & Theoretical Foundation` lines that name sources without page citations,
and three of those sources (Tubik, Robin Williams, Doyald Young) still have no ledger row despite
being on disk.

### A second flaky test

`AssetEventTests.TestTheBudgetSnapshotComesFromTheToolkitThatRan` failed once in a full-suite run and
passed both standalone and on an immediate re-run of the same suite. Not diagnosed. Alongside
`IrradiationCompensationTests`, that is two intermittents in the MCP/Drawing suites — re-run before
believing either.

### Two process notes

- **`tests/multi_agent/comic_studio/cs-1/manuals/07_*.md`** is still a tracked, stale copy of the old
  Guy-citing manual. It duplicates `docs/manuals/` and is an input rather than a run output, so
  deleting it loses nothing — flagged rather than removed because it sits in a frozen run directory.
- **Editing `ConstructiveDrawingToolkit.cs` by line number has bitten twice** this session: once from
  stale offsets after an earlier edit, once from a brace-walker running past an expression-bodied
  member into the next method. Both times the script's own guard caught it before writing. Match on
  the signature and handle `=> …;` members.

---

## 13. The comics corpus — staging, drapery, the comic head, action, and a posable figure

**Session of 2026-09-04/05. Tests: 1,292 .NET — Drawing 434, MCPServer 486, CLI 278, ExtendedMind 94.**
This is the state to pick up from for drawing work; §12 remains accurate as the session before it.

### What changed

| | |
| :--- | :--- |
| **Manual 20** | Staging, shots and page layout — Janson, *Pencilling* chs. 8-11 |
| **Manual 21** | Depth, proximity, screen continuity — Glebas, *Directing the Story* chs. 8-10 |
| **Manual 22** | Drapery as geometry — Cliff Young, *Drawing Drapery from Head to Toe* |
| **Manual 23** | The comic head — Lee & Buscema, *Marvel Way* ch. 8 |
| **Manual 24** | Action: the centre line and the extremes — *Marvel Way* ch. 6 |
| **`createMannequinFigure`** | now takes a `pose` — joint angles for four limbs, plus `spineDeg` / `neckDeg` |

### The diagnosis this started from, because it shapes everything after it

The toolkit could not draw a storyboard panel in the idiom of a real one, and the reason was **not**
missing marks — tapered ink, hatching, halftone and tonal modelling were all present. Two things
were missing, and both were found by *trying* rather than by reading:

1. **No composition layer for the head.** `createLoomisHead` gives landmarks and `drawComicEye` and
   its siblings draw features, but **nothing unions them** into a head with a silhouette, an ear and
   a neck. Two attempts at a face failed in different ways. That gap is unchanged and is now named
   in Manual 23 §6.
2. **A corpus skewed to the portrait idiom.** Every figure and head source was naturalistic
   illustration. `janson-pencilling` — the comics *staging* book — had been scanned, ledgered, and
   then cited in exactly **one** manual, not for staging. Across all 19 manuals at the time, "page
   layout", "storyboard" and "thumbnail" appeared **zero** times.

The five manuals close the second gap. The first is still open.

### The measurable findings, which are the useful part

- **Loomis makes a head 6.0 eye-widths wide; Lee & Buscema make it 5.** Our own construction's
  comment cites Plate 19 for six, so this is a documented disagreement between schools rather than a
  bug. The comic head is narrower relative to its eye (*the eye is bigger*) and carries a mouth
  **about twice as wide**. Manual 23 §1.
- **The extremes rule is computable.** Across five stages of a punch, departure from the standing
  figure runs `37, 22, 8, 22, 53` — the first and last *are* the extremes, exactly as *Marvel Way*
  says. Manual 24 §4 asserts it with a `Stage.check`.
- **Glebas's horizon rule is an assertion, not a principle.** The horizon cuts every equal-height
  figure at the same point on the body; fix the fraction, derive each height from its ground
  position, and four figures at 182/291/418/527 px all cut at exactly 0.550. Manual 21 §1.
- **Young's drapery is a cylinder model**, and the cylinder is already the mannequin's unit. Crushed
  gives ring folds; twisted gives folds that *describe action*; bent radiates from the joint, four
  distinct ring folds at the elbow. Manual 22.

### Posing — what it does, and the one thing it does not

`createMannequinFigure(x, y, h, { pose: { spineDeg, neckDeg, leftArm: { shoulderDeg, elbowDeg }, ... } })`.
Screen degrees, **90 is straight down**; root angles absolute, bends relative to the segment above.

**Additive by construction, and tested** (5 tests in `DrawingToolkitTests`). Every omitted angle falls
back to the standing figure's own value, recovered from the points the canon already computed — so an
unposed limb is bit-identical to before, and segment lengths are the canon's, never stretched.
`spineDeg` takes the arms with it, because an arm hangs from a shoulder that moved. The drawers needed
no change: they read joints from the dictionary.

> **The gap, and the next thing worth building.** *Marvel Way* ch. 6 says the **centre line is drawn
> first** and the figure built around it. Our API is the reverse — joints first, no line of action.
> Measured: centre-line swing across five very different poses is `1.1, 1.1, 1.1, 1.1, 5.2`,
> essentially flat, because **`spineDeg` rotates the torso rigidly and a rigid rotation moves a line
> without curving it**. The figure has a hinge where the book asks for a swing. A `lineOfAction`
> option — a C or an S with an amplitude, distributing curvature through pelvis, sternum and head —
> is the highest-value addition the figure toolkit could take. Hampton's C/S/I (Manual 05) and
> Buscema's centre line are the same idea from two traditions, which is unusually strong support.

### Defects found on the way

- **`PolsonManuals.cs` claimed the manuals distil *How to Draw Comics the Marvel Way*** — and no
  manual cited it; it was the only such name in the list. **Fixed:** the index now points at each
  manual's own source line as the authority.
- **`drawFeathering` is *parallel* hatching, not a fan.** It reads as though it radiates from its
  `origin` and does not. Used for drapery folds it silently draws plausible parallel verticals that
  state nothing. **There is no fan primitive** — build one from `drawTaperedStroke`. Manual 22 §2,
  and the ledger row carries the correction because an earlier draft of it was wrong.
- **A malformed colour is silently accepted.** `ctx.strokeStyle = '#6e6counts'` becomes `#000000`
  with no error — hit twice in three scripts by someone who already knew about it. A misspelled
  *member* throws with "did you mean"; a malformed *colour value* does not. **Not fixed.**
- **`head.unit` is an object** `{H, W, eyeW, thirdH}`, not a number. Reading it as one gives `NaN` in
  silence — exactly the trap the execution model warns about.

### Reference ledger — three rows added or settled

`drawing-drapery.pdf` (clean; Dover, no copyright statement, treat as the Bokhua row) ·
`DirectingTheStory.pdf` (clean, but **names retrieval systems explicitly** — Faragasso class, so
distil and cite and **never put its text in the corpus**) · `Marvel Way` (terms settled as far as the
artifact allows — **the copyright page is absent from the scan**; contents now read and recorded,
with the five duplicate chapters marked as not worth re-distilling).

**`reference/` is gitignored including its README**, so the ledger lives only on this machine. The
user is aware and prefers `reference/` excluded by default.

### Where to pick up

1. **`lineOfAction` on the mannequin** — see the box above. The rest of the figure work waits on it.
2. **A composed head** — `drawHeadSolid(ctx, head, options)` unioning cranium and jaw into one
   silhouette, placing the ear on the construction's own ear point, adding a neck, shading from a
   light direction. Specified almost line by line across Loomis and Janson.
3. ***Marvel Way* ch. 7, Foreshortening** — the last of the three non-duplicate chapters, and the
   only figure-in-perspective source in the corpus.
4. **The two silent-failure fixes** — the colour parse, and `drawFeathering`'s misleading `origin`.

---

## 14. Drawing a comic page from the manuals — what held, and what the toolkit still makes you build

**Session of 2026-09-05.** A single exercise: draw a comic figure using Manuals 03, 20, 22, 23 and
24, and record where the manuals carried the work and where the API did not. Deliverable was a
four-panel page — two extremes of one action, a close-up head, and the strip that chose the
extremes. No source changes; this section is the finding list.

### The manuals held. All five were usable as written.

- **M24's extremes rule reproduced on a fresh action.** Departure from the standing figure across
  five stages of a lunge ran **25, 13, 7, 12, 62** — first and last are the extremes by a wide
  margin, as the chapter says, and it is what chose the two figure panels. The manual's own numbers
  were `37, 22, 8, 22, 53` on a different action, so this is a second independent confirmation
  rather than a re-run.
- **M23's two numbers reproduced exactly**: the head is **6.0** eye-widths where Lee & Buscema say
  5, and the triangle mouth is **×2.04** the Loomis landmark width.
- **M22 transfers as claimed.** The hand-built fan from `drawTaperedStroke` reads as radiating
  folds; `drawCrossContourHatch` genuinely is a ring fold; Young's four elbow folds land.
- **M03's tier hierarchy scales.** Widths taken as `4.6 / 2.4 / 1.1` at 1000px and multiplied by
  `panelWidth / 1000` held across a 645px panel and a 390px one.

### A number Manual 23 describes but never measured

M23 §2 gives **two** equilateral-triangle constructions and the manual's example only implements the
first. The second — apex under the nose, sides at 60° through the lower lip, meeting the bottom of
the head — gives the **chin**:

> **The Marvel chin is ×0.89 the Loomis station width** — 11% narrower. Same class of finding as the
> mouth's ×2.04 and the eye's 6-vs-5, and it completes the set. The close-up's jaw contour is built
> to the derived width rather than to `jaw.chinNear`/`jaw.chinFar`.

Worth adding to Manual 23 §1's comparison table, which currently carries two rows.

### Defects found

- **The head never rotates, and neither do the torso masses.** `spineDeg` *translates* head, ribcage
  and pelvis; both `DrawMannequinSolid` and `DrawMannequinWireframe` then call
  `ctx.Ellipse(..., 0f, ...)` with the rotation hardcoded. A figure leaning 18° keeps a perfectly
  upright head, which reads as a bobblehead. **`ribcage.tiltDeg` and `pelvis.tiltDeg` are computed
  at `ConstructiveDrawingToolkit.cs:2264` and `:2266` and read by nothing** — grep finds no consumer.
  Dead output, and the fix for the mass tilt is already sitting in the dictionary.
  This is *separate from* §13's "no line of action": that one is the spine failing to curve, this is
  the masses failing to turn at all.
- **`temporalOval` is not the cranium, and nothing says so.** At a 300px head its top sits **91px
  below `crown`** — it is the side-plane ball. Building a head silhouette on it produces a head that
  begins below its own hairline. Cost two failed attempts before the relationship was worked out:
  the cranium has to be **derived** — `crown` for the top, the two jaw stations for the width,
  `jaw.angle` for the bottom. This is the concrete reason M23 §6's "no composed head" gap is hard,
  and it belongs in the docs whatever else is built.
- **A posed figure's extent is not its height.** A thrown arm reaches further sideways than the
  canon ever does, so sizing by height alone ran an arm and a leg straight off the first panel.
  A `figureExtent`/`fitFigure` pair had to be written; `createMannequinFigure` returning bounds
  would delete that from every caller.
- **Confirmed as documented:** `drawFeathering` is parallel hatching whatever its `origin` suggests.

### What the exercise cost, and why it is a toolkit problem

Roughly **7 of 13 renders were spent fixing things a better base model would have prevented** — four
on the head alone, two on page fitting, one on a silhouette union whose hand-built arc caps punched
white discs at every joint. None of that was creative work.

The through-line: **the toolkit exposes landmarks and finished drawings, and nothing in between.**
A landmark composes with nothing; a draw call composes with nothing. What the work actually needs is
**geometry** — `CanvasPath` — because a path fills, clips, strokes, booleans, converts through
`strokeToPath`, and survives into `outSvg`. Every gap above is an instance of that one shape.

> **Adding a more complex figure model without changing that interface would make it worse, not
> better** — more landmarks to hand-assemble, and more ways to get the assembly wrong.

### Built in this session — items 1 and 2

**Tests: 1,300 .NET — all passing** (Drawing 441, MCPServer 487, CLI 278, ExtendedMind 94).

| | |
| :--- | :--- |
| **`Drawing.createFigureGeometry(figure, { padding })`** | The figure as geometry: `silhouette` (one `CanvasPath`), `parts` (18 named masses), `groups` (the coarse six that occlusion clips to), `bounds`, `order`. |
| **`figure.bounds`** | On the figure itself, unconditional. Closed-form over the same mass table the geometry uses, so it builds no paths and is safe in a loop. |
| **`figure.head.angleDeg`** | `spineDeg + neckDeg`, and **both drawers now honour it**. |
| **`figure.ribcage.tiltDeg`** | now `shoulderTiltDeg + spineDeg` rather than the static tilt, and **read** rather than ignored. `pelvis.tiltDeg` is unchanged, because the pelvis is the pivot. |

**`padding` is the drapery premise made arithmetic** — cloth covers the figure without fitting it, so
a sleeve is the padded arm *group* rather than a second construction to keep in step with the pose.

**Measured against the exercise that motivated it.** The same page redrawn on the new surface:
**514 → 454 lines**, identical output, with the hand-built capsule union, the extent walk and the
head-tilt workaround all deleted. `figure.bounds` alone replaced 35 lines that existed only because
a posed figure's extent is not its height — **345 × 1013 standing against 938 × 954 in a lunge**.

Six tests added, and two are worth keeping in mind because they guard *silent* failures: the
silhouette is sampled **at each joint** for holes (a capsule with hand-swept arc caps subtracts a
bite instead of adding one, and renders cleanly as a white disc that looks like a design), and the
closed-form `bounds` is asserted against the path bounds it claims to describe, because two ways of
measuring one figure that drift apart is worse than having only the slow one.

> **One honest limit found while testing.** Growing both radii of a *rotated* ellipse is not a
> uniform outward offset of its box, so a tilted mass contributes marginally less than the padding at
> the extremes — about a hundredth of a pixel at figure scale. The test asserts a tolerance rather
> than exact addition, because exact addition is not true of ellipses.

**Also noted:** the canon's `foot` landmark is a short stub, so the silhouette ends at the ankle
rather than on a foot. Faithful to the canon, and documented — the canon has no foot to give.

### The general form of the defect, which is worth stating on its own

> **The toolkit's drawing functions paint and return nothing.** `drawMannequinSolid`,
> `drawHandSolid`, `drawLoomisWireframe`, `drawComicEye`/`Nose`/`Mouth`, `drawPerspectiveBox`,
> `drawTaperedStroke`, `drawCastShadow` all build real geometry internally and then throw it away.
> Every one of them forces a caller who wants to *build on* the result to reconstruct it by hand.

`createFigureGeometry` is one instance of the fix, not the fix. The same move applies wherever a
drawer already computes a shape: return the `CanvasPath` alongside painting it. Adding a return value
to a currently-void call is backward compatible in JS — a script that ignores it is unaffected — so
this can be done drawer by drawer as each one is needed.

Ranked by what would have saved the most work in the page exercise: **`drawTaperedStroke`** (the mark
becomes a shape you can cut, fill with a cross-gradient, or export as vector), **the comic feature
drawers** (an eye you can clip a highlight into), **`drawHandSolid`** (which has the identical
boxes-not-form problem at 15px), and **`drawPerspectiveBox`** (a face you can texture).

### The first drawer converted: `drawTaperedStroke`

**Tests: 1,304 .NET — all passing** (Drawing 444, MCPServer 488, CLI 278, ExtendedMind 94).
(Superseded by the 1,311 figure below, once the rest of the drawer list landed.)

`Drawing.drawTaperedStroke(...)` and `ctx.drawTaperedStroke(...)` now **return the `CanvasPath` they
filled**, and `Drawing.createTaperedStrokePath(start, cp1, cp2, end, maxThickness)` builds the same
envelope with no context to paint it on — for laying marks out, measuring them, or combining a run of
them before anything is drawn.

The envelope build was factored out of the drawing path, so both routes are provably the same
geometry (asserted on point count and bounds) and neither duplicates the twenty-five-sample sweep.

**A free fix came with it.** The call used to build its envelope *on the context*, so an inking call
silently replaced whatever path the caller had under construction — cross-talk that surfaces three
calls later as a fill of the wrong shape. It now builds on its own path and leaves the current one
alone. There is a test for that specifically, because it is the kind of thing that gets refactored
back in by accident.

> **The compatibility property is what makes this cheap to repeat.** Adding a return value to a
> `void` call changes nothing for a script that ignores it, so the remaining drawers can be converted
> one at a time as each is needed, with no coordinated migration.

Two things learned while testing, both recorded in the test's own remarks:

- **A tapered mark's bounding box is a bad proxy for its thickness in one axis and a fine one in the
  other.** The taper closes to nothing at both ends, so the endpoints pin the horizontal extent
  whatever `maxThickness` is; the mid-curve bulge does move the vertical one. The unambiguous
  assertion is a point 12px along the normal at the curve's midpoint — outside an 8px mark, inside a
  40px one.
- Growing both radii of a **rotated** ellipse is not a uniform outward offset of its box, so
  `padding` adds fractionally less than asked at the extremes (~0.01px at figure scale).

### The rest of the drawer list, converted

**Tests: 1,311 .NET — all passing** (Drawing 448, MCPServer 491, CLI 278, ExtendedMind 94).

| Call | Now returns |
| :--- | :--- |
| `drawComicEye` | `aperture`, `iris`, `pupil`, `catchlight`, `upperLid`, `lowerLid` |
| `drawComicNose` | `underPlane`, `bridge`, `nostril` |
| `drawComicMouth` | `cavity`, `teeth`, `lipLine`, `lowerLip` |
| `drawHandSolid` / `ctx.drawHand(h, true)` | `silhouette`, `parts` (16, named by bone), `bounds` |
| `drawPerspectiveBox` | `faces` (all six), `silhouette` (the three visible, unioned) |

The choices worth recording, because they are the pattern for the next conversion:

- **Several shapes means named paths, not one path.** A single return only fits a call that draws one
  mark. `createFigureGeometry`'s `parts`/`groups` shape was already the precedent; these follow it.
- **Fill shapes come back closed; stroked ones come back as open centre-lines.** A lid or a `lipLine`
  is a line whose *weight* is a decision (Manual 03's tiers), so handing back a filled mark would
  freeze the decision. An open path can be re-stroked or run through `ctx.strokeToPath(...)`.
- **The box returns its hidden faces even when it did not draw them.** Building a path is not drawing
  it, and a caller staging occlusion needs the back of the box precisely when it is invisible.
- **The hand's wireframe pass returns an empty object** rather than something hollow: it draws guides,
  not masses, so it has no silhouette to give.
- **The thumb has two phalanges**, so its parts are `thumbProximal`/`thumbDistal` and never `Middle`.

### The regression check that made this safe to do quickly

Every one of these was a mechanical refactor of live drawing code, so the question was whether the
pixels moved. They did not, and it was cheap to prove: **`bitmap.diff` at `tolerance: 0` against
renders made by the previous binary** — the head close-up (all three feature drawers) came back
**0 of 384,000 pixels differing**, and a hand-plus-box scene **0 of 432,000**.

> Worth keeping as the method for the remaining conversions: render a reference with the binary in
> `bin/cli` *before* rebuilding it, convert, rebuild, diff at zero tolerance. It is the one check that
> distinguishes "the same drawing" from "a drawing that still looks fine".

**The current-path fix applies to all of them.** Each used to build its shapes on the context, so an
unrelated call silently replaced whatever path the caller had under construction. One test now pins
that down for all four at once.

### Where to pick up — revised, and this supersedes §13's list

1. ~~Geometry-returning figure~~, ~~orientation on the masses~~, ~~`drawTaperedStroke`~~ and
   ~~the comic feature / hand / perspective-box drawers~~ — **done, above.**
2. **Still painting and returning nothing**, in rough order of value: `drawMannequinSolid` and
   `drawMannequinWireframe` (superseded in practice by `createFigureGeometry`, but the drawers
   themselves still hand nothing back), `drawLoomisWireframe`, `drawCastShadow`, `drawRimLight`,
   `renderVolumetricSphere` / `renderVolumetricCylinder`, `drawPerspectiveCylinder`,
   `drawCrossContourHatch`, `drawHairRibbon`, and the `Logo.*` drawers. None is urgent; convert each
   when a piece of work actually wants its geometry, which is how the value stays demonstrable.
3. **`lineOfAction` on the mannequin** — unchanged from §13, still the item that changes *how a pose
   is chosen* rather than how it is drawn.
4. **A composed head**, now specified rather than merely wanted: `createComicHead(...)` returning
   cranium, jaw, hair and neck as paths, with M23's corrections baked in — 5-eye width, triangle
   mouth, triangle chin — and the derived-cranium relationship above applied internally.
5. **Garment derived from the figure.** `padding` gives the cloth *volume*; what is still hand-built
   is the coat body itself and its **fold anchors** — points of pull, belt line, elbow ring centres.
   `createGarment(figure, { type, hemHeads })` returning those alongside the path is M22 made
   executable, and it remains the largest single block of hand-work left in the exercise.
6. ~~Put the bone-capsule boilerplate in a manual~~ — **overtaken.** Manual 08 §2a now documents
   `createFigureGeometry` instead, which is the same need answered properly rather than by a snippet
   every caller has to copy and can get wrong.

**§15 is the detailed queue** — designs, measured constants and acceptance tests for each of items
2 to 5, written so a cold session can start without re-deriving anything.

---

## 15. Toolkit queue — designs and measured constants for the next sessions

Written 2026-09-05, at the point the drawing thread was paused for the Agentic Cinema hackathon
(deadline **14:00 PT 2026-09-09**, `docs/agentic-cinema-assessment.md`). Nothing here is started.
Ordered by value; items are independent except where noted.

**Working state at the pause.** Tests 1,311 (Drawing 448, MCPServer 491, CLI 278, ExtendedMind 94).
`bin/cli` is current. The exercise that drove all of this is `docs/manuals/08` §2a, `03` §3, `06` §2a,
`19` §2a and `01` §4D — those five manual passages are the worked examples, and they execute as tests,
so they cannot rot silently.

---

### 15.1 `lineOfAction` — the highest-value item, and the only one that changes how a pose is *chosen*

**The problem, measured.** *Marvel Way* ch. 6 says the centre line is drawn **first** and the figure
built around it. Our API is the reverse: joints first, no line. Measured across five very different
poses of one action, the centre-line swing (perpendicular bow of `pelvis.center → sternum → head.center`
off the chord) was **1.1, 1.1, 1.1, 1.1, 5.2** — essentially flat, because **`spineDeg` rotates the
upper body rigidly and a rigid rotation moves a line without curving it.** The figure has a hinge where
the book asks for a swing. Manual 24 §4 carries this and Hampton's C/S/I (Manual 05) is the same idea
from a second tradition, which is unusually strong support.

**Proposed shape.**

```javascript
Drawing.createMannequinFigure(x, y, h, {
    pose: { lineOfAction: { shape: 'C' | 'S', amplitudeDeg: 24, phase: 0.5 }, /* joints as now */ }
});
```

**Implementation sketch.** The spine is already a chain of nodes — `pelvis.center`, `navel`,
`sternum`, `neck`, `head.center`. Today they are laid out at fixed offsets and then *all* rotated by
`spineDeg` about the pelvis. Instead, give each node an **incremental** rotation about its predecessor
and accumulate down the chain:

- **C**: `angleᵢ = amplitudeDeg × (i / n)` — curvature increasing steadily toward the head.
- **S**: `angleᵢ = amplitudeDeg × sin(2π × (i / n − phase))` — sign reverses partway, which is the
  reversing curve Hampton calls the S.
- Each node is placed by rotating its offset about the *previous* node by the accumulated angle, so
  the chain bends rather than swinging as one body.
- Shoulders and arms hang off the **sternum's** accumulated transform; the head off the **neck's**.
  `head.angleDeg` and `ribcage.tiltDeg` (both now live and honoured by the drawers — §14) must be fed
  from the accumulated angle at their own node, not from `spineDeg`.

**Compatibility.** Keep `spineDeg` as it is: a rigid lean. Define the order as **`lineOfAction` shapes
the spine, then `spineDeg` leans the result**, and document it. An omitted `lineOfAction` must leave
the figure bit-identical, exactly as the existing `pose` additivity tests require.

**Acceptance test, and it is already written in prose.** Reuse `swingOf` from Manual 24 §5: swing must
be **monotonic in `amplitudeDeg`** and must exceed the flat ~1.1 baseline at any non-zero amplitude.
That turns "more swing" from a judgement into an assertion, which is the whole reason the number was
measured in the first place.

---

### 15.2 A composed head — `createComicHead`

Manual 23 §6 names this gap, and this session hit it hard enough to specify it. **Two attempts failed
before the cause was found**, so the constants below are the expensive part and should not be
re-derived.

> **`temporalOval` is NOT the cranium.** It is the side-plane ball. At a 300px head its top sits
> **91px below `crown`** — build a head outline on it and the skull begins below its own hairline.

**Derive the cranium instead**, which is what finally worked:

```javascript
const cx = head.crown.x;
const rx = (jaw.nearStation.x - jaw.farStation.x) * 0.5 * 1.22;
const bottom = jaw.angle.y + HGT * 0.028;
const cy = (head.crown.y + bottom) * 0.5, ry = (bottom - head.crown.y) * 0.5;
```

Then one contour: the top half of that ellipse (π → 2π), down the near cheekbone through
`jaw.nearStation`, `jaw.nearAngle`, the chin, `jaw.chinFar`, `jaw.angle`, `jaw.farStation`, back up.
**At yaw the near cheekbone sits outside the cranial ellipse**, which is why a union of oval-and-box
squares the head off — the first attempt did exactly that.

**Bake in the Marvel corrections**, all three measured this session and reproducible:

| | Loomis, as implemented | Lee & Buscema | ratio |
| :--- | :--- | :--- | :--- |
| head width in eye-widths | 6.0 | 5 | eye × **6/5** |
| mouth width | landmark width | equilateral triangle from `noseWedge.bridgeTop` | **×2.04** |
| chin width | `jaw.chinNear − jaw.chinFar` | equilateral triangle from `noseWedge.underNose` | **×0.89** |

Half-width at any depth below an equilateral apex is `depth × tan(30°)`. **The chin ratio is new** —
Manual 23 describes the construction but never measured it; add the row to its §1 table when this
lands.

**Return**, following the established shape: `silhouette` (cranium ∪ jaw ∪ neck), `parts`
(`cranium`, `jaw`, `hair`, `neck`, `ear`), `bounds`, and the landmark object it was built from. Hair
that worked: a cap ellipse at `rx × 1.09, ry × 1.10` of the cranium, minus everything below the
hairline, plus a lock crossing the cap edge — Manual 23 §3's *body and thickness, never flat on the
skull*.

---

### 15.3 Garment derived from the figure

`padding` on `createFigureGeometry` already gives the cloth its **volume** (Manual 22 §1: cloth covers
the figure without fitting it). What is still hand-built is the garment body and, more importantly,
its **fold anchors**:

```javascript
Drawing.createGarment(figure, { type: 'coat' | 'tunic', hemHeads: 2, trailing: -1 })
// → { path, anchors: { pulls: [Point], beltLine: {left, right}, elbowRings: [{centre, angle}] } }
```

The anchors are Manual 22 made executable, and each is a rule rather than a shape: folds radiate from
**points of pull** (the two shoulders), ring folds appear where the belt **crushes** the cylinder, and
a **bent** elbow crowds *four* ring folds on the inside — Young's own count. Fold density rises on the
side whose support is lower, which is a relationship the figure already knows.

**Still no fan primitive.** `drawFeathering` is *parallel* hatching whatever its `origin` argument
suggests; used for folds it silently draws plausible verticals that state nothing. Build fans from
`drawTaperedStroke`, which now returns its mark so a fan can be unioned into one shape and inked once.

---

### 15.4 Converting the remaining drawers

The pattern and its justification are in §14; this is the queue and the recipe.

**Still painting and returning nothing**, in rough order of value: `drawMannequinSolid` /
`drawMannequinWireframe` (superseded in practice by `createFigureGeometry`, but they still hand
nothing back), `drawLoomisWireframe`, `drawCastShadow`, `drawRimLight`, `renderVolumetricSphere` /
`renderVolumetricCylinder`, `drawPerspectiveCylinder`, `drawCrossContourHatch`, `drawHairRibbon`, and
the `Logo.*` drawers.

**None is urgent.** Convert each when a piece of work actually wants its geometry — that is what keeps
the value demonstrable instead of converting on spec.

**The recipe, four steps:**

1. **Render a reference with the binary already in `bin/cli`, before rebuilding it.** This is the step
   that is easy to skip and impossible to recover.
2. Move the shape construction out of the context and onto `CanvasPath`s; paint with `ctx.fill(path)`
   / `ctx.stroke(path)` in the same order and with the same state as before.
3. Rebuild, re-render, and **`bitmap.diff` at `tolerance: 0`**. It came back 0 differing pixels for all
   four conversions so far; anything else means the refactor moved something.
4. Return named paths for several shapes, a single path for one mark; fills closed, strokes as open
   centre-lines. Add the doc line (`docs/Polson.core.md`) and a manual passage — the test suite
   enforces both, and manual examples execute, so they must be self-contained.

---

### 15.5 Foreshortening — the quality ceiling, and the one gap with no code yet

**Every limb in the comic page drawn this session lies in the picture plane.** Nothing comes toward
the viewer, because nothing can: there is no per-segment depth. That is the largest single limit on
how the figure work reads, and it is invisible until you look for it.

The source is *Marvel Way* **ch. 7, Foreshortening: the Figure in Perspective** — recorded in the
`reference/` ledger as the last of the three non-duplicate chapters and **still unread**. It is also
the only figure-in-perspective source in the corpus.

Minimum shape: a `zScale` per bone, shortening the drawn segment and widening its capsule as it turns
toward the viewer. `createFigureGeometry` is the natural place for the widening, since it already owns
the radii.

---

### 15.6 Open defects, unchanged

- **A malformed colour is silently accepted.** `ctx.strokeStyle = '#6e6counts'` becomes `#000000` with
  no error — hit twice in three scripts by someone who already knew about it. A misspelled *member*
  throws with "did you mean"; a malformed *colour value* does not. **Not fixed**, and it is the
  cheapest remaining silent failure to close.
- **`drawFeathering`'s `origin` is misleading** — see §15.3.
- **The canon's `foot` is a stub**, so `createFigureGeometry`'s silhouette ends at the ankle rather
  than on a foot. Documented in Manual 08 §2a. Faithful to the canon; the canon has no foot to give.
- **`head.unit` is an object** `{H, W, eyeW, thirdH}`, not a number — reading it as one gives `NaN` in
  silence.


---

## 16. Motion — animated SVG, frame capture, and the score API

**Session of 2026-09-05, second half.** Opened as "does Svg.Skia's animation support actually work",
became a working animated-WebP pipeline and a design decision about the client API.
**Tests: 1,334 .NET** (Drawing 469, MCPServer 493, CLI 278, ExtendedMind 94).

> **`docs/motion-score-api.md` is the plan for what comes next** — the score API, specified cold:
> the position grammar, the semantics that must hold, the test list, the guardrails, and a build
> order. Nothing in it is started. Read that; this section is only what was established getting there.

### It works, and the measurements settle the format questions

**Svg.Skia 5.2.1 carries a full SMIL engine and it is exact.** `SKSvg.SetAnimationTime(t)` seeks to
any time in **0.43 ms**, deterministically — t = 1 s rendered directly and after visiting t = 3 s are
identical. Every feature tested animates: `animateTransform` translate/rotate/scale, `animate` on any
attribute, `values` + `keyTimes` (exact to the pixel), `begin` offsets, `repeatCount="indefinite"`,
`animateMotion`, `<set>`, `stroke-dashoffset` line-draw, and `additive="sum"`.

**SkiaSharp encodes animated WebP natively** — `SKWebpEncoder.EncodeAnimated(frames, options)` with
`SKWebpEncoderFrame(bitmap, duration)`. 50 frames at 25 fps, 480×270, **76 KB**, decoding back as
exactly 50 × 40 ms. No ffmpeg.

| | |
| :--- | :--- |
| Render (SVG document → bitmap) | **2.7 ms/frame** at 1280×720 with 20 animated elements, **12.2 ms** with 200 |
| Encode | **59–68 ms/frame** — dominates render by 5× |
| SMIL seek | **0.43 ms**, O(1) |
| Animated WebP random access | **O(n)** — 1 of 47 frames independently decodable; frame 36 costs 13 ms against 1 ms for frame 0 |
| Skia encoders | PNG, JPEG, WebP. **No GIF, APNG or AVIF.** |

### What was built

`SvgRenderPipeline` gained **`atTime`** on `RenderToImage`, `RenderToBitmap` and `SaveImage`, plus
`HasAnimations(xml)`. Before it, an animated document rendered its opening frame — successfully, with
no error. `Motion` gained `frame`, `save`, `sheet`, `count`, `clear`.

### Three defects found, all by verification rather than by reading

- **`SvgDocument.Write` corrupts SMIL's `fill="freeze"` into `style="fill:freeze;"`**, conflating the
  timing attribute with the paint property of the same name — so an animation snapped back instead of
  freezing. **Fixed** by routing the document overloads through `SKSvg.FromSvgDocument` rather than
  serialising and re-parsing; static output is **byte-identical** between the two routes, and it drops
  a serialise plus full re-parse from every render.
- **`Motion.save` reported a frame count the file did not have.** WebP merges consecutive
  pixel-identical frames and sums their durations — correct, and a real size win. It now decodes what
  it wrote and reports `frames`, `storedFrames` and `merged`.
- **A use-after-free that aborted the whole test run.** The frame-size-mismatch branch disposed the
  bitmap and then read `bitmap.Width` composing the error message. Native memory, so it does not
  throw — it takes the process down, and an agent would lose the run rather than the frame.

### Two findings that shape the API rather than the plumbing

**An agent cannot watch a video.** It reads images, so a moving file is close to the worst artifact to
hand it for inspection — it can produce one and still not perceive the motion. `Motion.sheet` tiles
N instants into one labelled image: a single read, and unlike a video it supports comparison, by eye
and by `bitmap.diff`. **`sheet` is the artifact to look at; `save` is the artifact to ship.**

**An animated file is also the wrong thing to hand the next stage.** Random access is O(n) (above).
Stage transfer should carry the **script** plus a stage SVG/PNG at chosen times, and reach a time by
re-evaluating the timeline — O(1) at any `t`.

### The reference ledger

`projects/Snap.svg-master/src/` scanned and recorded: clean, **Apache 2.0 © 2016 Adobe**, permissive
and one-way compatible with our AGPL-3.0. `dist/`, `demos/`, `test/` and `doc/` remain unscanned
apart from `demos/illustrated-infographic-coffee/`, which was scanned clean for the API read.

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

---

## 17. The motion score landed — `docs/motion-score-api.md` is now history, not a plan

**Everything in §16's plan is built** apart from `Motion.saveFrames`. The plan document has been
updated in place: each step is struck through with what actually happened, so it reads as a record
rather than a queue. Two of its decisions were **reversed by measurement**, and both reversals are
worth carrying.

### 17a. §6.1 resolved — declare the delegate type

The open question was how a JS function reaches C# on a hot path. **Option 1 works**: declare
`Action<double>` / `Func<double, double>` and Jint marshals it. Measured against an engine carrying
the options that matter:

| | |
| :--- | :--- |
| JS arrow → `Action<double>` | binds; **1.5 µs per call** |
| the delegate held and called **after `Execute` returned** | works |
| cost per *conversion* | **17.8 µs — 12× a call**, so convert at add time, never per frame |
| JS object literal → a C# class with settable properties | binds, `Func<double,double>` included |
| a positional `record` | **does not bind** — "No public methods with the specified arguments were found" |

**The consequence that shaped the API:** a function inside an options object read through
`JsInterop.AsDict(...)` arrives as a raw `Func<JsValue, JsValue[], JsValue>` — a Jint type the Skia
assembly must not name. So `MotionEntryOptions` is a **typed class**, and `MotionToolkit`'s existing
`object? options` + `AsDict` pattern must not be copied where a callback is involved.

> **But the reverse holds for the Chart toolkit, and that is not a contradiction.** A typed class
> cannot report a misspelled option — the binding was never told the name exists, so `{ durr: 400 }`
> is silently ignored. A dictionary *can* see it. So: **typed class where a callback must survive,
> dictionary where it must not, and validate the keys.** Both are in the tree and both are right.

### 17b. The `mina` prelude was measured and rejected

§5 planned a JS prelude rebinding `mina` to closures. It works and **costs the strict-member
surface**: `mina` becomes an `ExpandoObject`, so `has(mina, 'nonsense')` answers **true** and
`suggest` answers *"'nonsense' exists on ExpandoObject — nothing to correct"* — wrong advice naming a
type no script author has heard of.

**Built instead:** every easing is a **delegate-valued property** (`public Func<double, double>
Elastic { get; } = ElasticCore;`), so it carries its own target and `this` never enters into it. All
four positions work, `has`/`suggest`/probe recording are untouched, and no prelude runs. `Mina`'s
`<remarks>` says not to tidy them back into methods; `MinaInteropTests` pins all four positions.

§5 also contained a **false claim that was the stated reason to do the prelude first** — that
`easing: mina.elastic` inside an options object would break. It does not; only *calling* through an
object did. Corrected in place.

### 17c. What is built

`MotionTimeline` in `src/Polson.Drawing.Skia/`, registered as `tl`: `tween` · `to` · `set` · `show` ·
`stagger` · `label` · `seek`/`at` · `duration` · `labels` · `count`, with the full GSAP position
grammar (`'+='`, `'-='`, `'<'`, `'>'`, `'<+=100'`, labels, label offsets). 38 engine-free tests.
**Manual 25** written; the `Motion` coverage floor raised **0 → 100**.

Two defects found while building it, both fixed: `SkiaColorParser.Parse` returned black for
unrecognised input with no way to tell (now `TryParse`, with `Parse` as the discarding wrapper), and
a dozen SVG attributes were **settable but not readable** — `display` among them, which a visibility
window needs. A background task then fixed the rest (§18d).

**Not done:** `Motion.saveFrames(dir, options?)` — step 7, trivial, the seam to ffmpeg for anything
over ~20 s. And the spike file `scratchpad/figure/20_spike.js` **is gone** with its session, so step
5 became "build a score and assert determinism" rather than a regression check.

---

## 18. The infographics thread — sources, Manual 13, and a `Chart` toolkit

The session's largest thread, and a **product direction rather than a feature**: for advertising and
promotional work Polson can do what a diffusion model structurally cannot — proportional encoding,
frame-to-frame identity, vector output, and brand compliance from a stylesheet. The argument that
survives scrutiny is not *precision* but **auditability**: the image *is* the data, and every number
traces to a source.

### 18a. Five sources acquired, scanned and ledgered

| | Terms |
| :--- | :--- |
| Tufte, *Visual Display of Quantitative Information* (1983) | Bokhua class |
| Tufte, *Envisioning Information* (1990) | Bokhua class |
| Tufte, *Visual Explanations* (1997, 7th printing 2005) | **Faragasso class** |
| Cleveland & McGill, *Science* 229:828–833 (1985) | JSTOR — **personal, non-commercial** |
| Cleveland & McGill, **JASA 79:531–554 (1984)** | JSTOR — same |

**The Tufte licence split is the thing to remember: it is the printing date, not the author.** The
1983 and 1990 printings carry a bare all-rights-reserved; the 2005 printing of *Visual Explanations*
names *"information storage and retrieval… computer software… now known or developed in the future"*
with an express carve-out for **scholarly analysis**. So a manual distilled from it is fine; its
OCR'd text must not enter the retrieval corpus. **Do not generalise one Tufte volume's terms to
another** — that is why all three were read separately.

**The C&M rows need a decision before anything commercial ships.** JSTOR's terms bind *the copy*, not
the *finding* — so distil and cite as `JASA 79:531–554 (1984)`, never by the JSTOR URL, keep the text
out of the corpus, and do not redistribute the PDF (the 1984 copy carries a per-download stamp with
the accessing IP on every page). Nothing depends on the artifact; the ranking is in Cleveland's own
*Elements of Graphing Data*. **Bertin, Munzner and Wilkinson are still absent** and are the remaining
names on the 2026-08-29 list; none is blocking.

### 18b. Manual 13 rewritten, and three things the sources say that most citations get wrong

- **It is six ranks, not a ten-item ladder.** Length/direction/angle are tied, as are volume/curvature
  and shading/saturation.
- **The ordering is part measured and part asserted, and the authors say so**: *"aspects of the
  ordering are partly conjectural in that we have no controlled experimentation to support them."*
  Position and length were tested and won; much of the rest is reasoned.
- **They name replacement forms**, which is the part usually forgotten — bar charts, divided bars,
  pies and shaded maps need *"radical surgery"*, with **dot charts, grouped dot charts and
  framed-rectangle charts** offered instead. That was a toolkit gap, and it is now closed.

The manual gained graphical integrity with the **lie factor as a computed check**, data-ink and the
**smallest effective difference**, micro/macro readings and layering, and litmus tests upgraded from
prose to five assertions (including the *Marvel Way* **reduction test** via `bitmap.resize`).
`Scale` coverage rose 54% → **63%** on members that earned their mention.

### 18c. The `Chart` toolkit — eleven constructions, and `slots`

New area, new receiver, floor at **100**: `createColumnChart` · `createBarChart` · `createDotChart` ·
`createGroupedDotChart` · `createFramedRectangleChart` · `createSmallMultiples` · `createCallout` ·
`createWaffle` · `createPictogram` · `createProgressMeter` · `createProportionalShapes` ·
`createTimeline`, plus `createChartGeometry` and `drawChart`. **156 tests**, all through the model
rather than the pixels.

> **The reframe that matters, and it came from the director mid-thread: for ad work the chart forms
> are not the deliverable — the armature is.** Every model carries **`slots`**: one entry per mark
> with where it stands, how big it is, which way it grows (`angleDeg` 0 up / 90 right) and how far up
> the scale it got (`fraction`). Draw skyscrapers, droplets, coins, little figures. `slots` means the
> same thing in every form, so a mark routine survives a change of chart form. **This is the feature
> to lead with**, and `drawChart` (plain rectangles) is explicitly a drafting tool.

Each model also carries `encoding`, `encodingRank`, `isZeroBased` and `lieFactor`, so a run record
shows what accuracy a chart spent and whether its ink is honest. Two lie factors are **measured from
the drawn geometry** rather than asserted — proportional shapes compare drawn *areas* against value
ratios, so a sizing error reports itself.

**Rank-0 is ours, not the literature's**: a callout asks the reader to *read a numeral*, and C&M's
ordering starts at 1 and says nothing about text. Said so in the docs.

### 18d. Two background tasks, landed

Both spun off mid-session and both worth keeping:

- **`CoreReferenceExampleTests`** — the nine `docs/Polson.core.md` examples now execute, with a floor
  so demoting fences cannot become the way to pass. Four genuine fragments were demoted to ` ```js `.
- **`SnapAttributeRoundTripTests`** — every settable SVG attribute now reads back, with a test that
  scans the source and asserts **every setter case has a getter arm**, which makes the class of bug
  impossible rather than fixing this instance.

> **Line endings bit both landings, in opposite directions.** A worktree checks out CRLF where the
> main tree holds LF for some files and CRLF for others. Copying a whole file across flipped 1,680
> lines in one case; hand-applying four edits was correct there, and a straight copy was correct for
> the other. **Check `git diff --numstat` after landing worktree work**, always.

---

## 19. Prescription versus scope — the distinction that runs through all of it

Manual 13 was deliberately made **more prescriptive** than the drawing manuals, on the argument that
§2 is about *truth* (a truncated axis asserts something false) and that the cost of error is
asymmetric — a mediocre drawing disappoints somebody; a false baseline is a claim a client publishes
under their own name.

**Then the director caught the over-reach**, and it was real: a manual prescriptive about *how to
encode a quantity* was reading as though it governed *what the graphic should be*, which would have
argued away the Apollo blueprint — the studio's own best work.

**The source had already drawn the line the manual dropped.** Cleveland & McGill, verbatim: *"We do
not argue that this accuracy of quantitative extraction is the only aspect of a graph for which one
might want to develop a theory, but it is an important one."* Restoring that boundary is honesty
about the source, not a softening of it.

So the shape now is:

- **Manual 13** — new scope note above the prescriptive one, bounding it; §1's table is *not a menu of
  what to make*; **new §1d** naming the non-quantitative forms (cutaway, schematic, map, sequence,
  specimen sheet, diegetic scene) as first-class. **Invent the container, then be strict about every
  quantity inside it.**
- **Manual 25** — a different note, because **motion makes no claim that can be false, so it has no
  §2 and is mostly craft**. A still is not a lesser deliverable; `Motion.frame` needs no timeline;
  §1's purity property binds *conditionally*, exactly when you want seeking.
- **Every manual** — a scope note is now **prepended at serve time** by `PolsonManuals.ServedBody(...)`.

> **Why served rather than in the index.** `BuildIndex()` already carried a "not prescriptive"
> paragraph that an agent reading `polson://manual/06` never sees — the same second-place-copy defect
> that let the index claim for two days that no manual cited *Marvel Way* while two were written from
> it. Injected at **serve** time only, so `ManualExampleTests`, `ManualCoverageTests` and the search
> corpus still read a clean `Body`.

**Audited all 23 rather than writing 23 notes.** Over-reach was concentrated: Manual 10 asserted
*"a professional logo mark is **not** a freehand sketch"* (Bokhua's school as fact), Manual 12's
stages were "required", and 01/02/03/04/11 said "exact", "standards", "rigorous rules". Fixed.
**05, 08, 15, 16 and 23 were already properly scoped and were left alone.**

`ManualScopeTests` guards it. Its register lint failed first time on Manual 12's *own disclaimer* —
"not a **required** sequence" — because a substring cannot tell a claim from its negation; it matches
phrases now, and its remarks record the false positive and admit it is a lint rather than a proof.

---

## 20. Where to pick up

**Tests: 1,645 — all passing** (Drawing 736, MCPServer 537, CLI 278, ExtendedMind 94).

### The infographics direction

1. **Wire Parallel's Search API.** It is the Agentic Cinema track's **hard requirement** and it is
   still untouched — and it is the same work the infographics direction needs, so the eligibility
   requirement and the differentiator demo are one task. A run that searches for figures, cites them,
   and renders a chart whose bars are provably those figures satisfies both. **Design the provenance
   path deliberately**: a retrieved number rendered into an authoritative-looking chart is a worse
   failure than one in prose.
2. **Build the demo that shows a revision, not a render.** The same graphic with one number changed
   and everything downstream consistent; or three brand palettes from three stylesheets. Aesthetics
   is the one axis where the argument is uphill; consistency under revision has no contest.
3. **Follow-on toolkit**: an ink-share helper for §3's erasing pass. (`Scale.lieFactor` is moot — the
   models compute it.)

### Agentic Cinema — `docs/agentic-cinema-assessment.md`, corrected this session

Its §4 said "Four items, **none started**" three days after that stopped being true, and a session
believed it. Now accurate: **ADK entry point, container and Cloud Run deploy are done.** Remaining —
**Parallel (gates eligibility)**, public access (needs the Milestone 6 spend caps first, not just the
flag), **mounting the studio UI** (`main.py` says "nothing is mounted yet"; a judge currently reaches
ADK's dev interface, against the 25% Design criterion), and the video. **Repo is AGPL-3.0; whether it
is public was not verified.**

### Carried forward

- **An unidentified intermittent failure in `Polson.Tests.MCPServer`** — seen twice in ~6
  full-solution runs, passes in isolation, never captured. **It is not the recorded flake**, which is
  `IrradiationCompensationTests` in `Polson.Tests.Drawing`. Do not file it there.
- **`reference/README.md` is gitignored** (`.gitignore:432`), so the ingestion ledger has no version
  history and a fresh clone gets none. Worth force-adding even though the books stay out.
- **The fence regex is duplicated** between `CoreReferenceExampleTests` and `ManualExampleTests`,
  described in a comment as "shared".
- **Eighteen callout examples in `Polson.core.md` are untested** — quoted blocks sit off column 0.
  Currently none is a complete program, but nothing enforces that.
- `Motion.saveFrames` (§17c); Bertin/Munzner/Wilkinson (§18a).

---

## §21 — Reference photography, and the vector deliverable (2026-09-06, eighth session)

**Tests: 1,987 .NET — all passing** (Drawing 773, MCPServer 617, CLI 292, ExtendedMind 305), plus
**39 Python** in `src/adk_agent/tests` — which run under `python-adk/`, not `python/`. Two commits
landed during the session (`09d4cab` Photo, `85321cd` vector workflow); **§21.6 is uncommitted.**

The session went: a question about retrieving photographs, which turned into the `Photo` surface;
then the discovery that our SVG deliverables could not carry them, which turned into the raster
boundary work; then a measured finding that vector deliverables were unreachable from the
infographic workflow at all, which turned into `vector_infographic` and the chart port.

---

### 21.1 `Photo` — likenesses, with their terms

`src/Polson.ExtendedMind/Photos/` (4 files), exposed as `Photo`. Manual 26. Shaped like `Assets`:
failure-as-a-value, readable budget, shared library, `RequisitionScope` events (`kind: "photo"`).

**Parallel cannot do this and neither can ADK.** Measured, not assumed:

- Parallel's extract **drops `src` entirely** — 30,000 chars of a photo-heavy page yielded 0 markdown
  images, 0 `<img>` tags. What survives is the `File:` description-page link.
- **ADK 2.8.0 has no image search anywhere** — zero hits for `image_search` / `searchType=image`
  across 1,819 `.py` files. `VertexAiSearchTool` is a **model-side built-in** with no `run_async`:
  it appends `types.Tool(retrieval=…)` and the model searches internally, so there is nothing to
  meter, gate, or record. It also cannot coexist with function calling — *"Gemini API does not allow
  built-in search tools to be combined with function calling"* — and with `bypass_multi_tools_limit`
  the thing you constructed is silently swapped for something else in `_convert_tool_union_to_tools`.
- Google **does** have image search — the Custom Search JSON API, wrappable via
  `GoogleApiToolset(api_name, api_version)` which takes any API from the discovery service. It has
  `searchType=image`, `imgType=face`, and a `rights` **filter** — but its `Result` schema carries
  **no licence field**, so a filtered result still cannot be credited or ledgered.

Wikimedia was chosen because it is the only source measured that returns the terms *with* the bytes:
`LicenseShortName`, `Artist`, `AttributionRequired`, and `Restrictions` (one probe subject carried
`personality`). Coverage was 7/8; the miss was a deliberate ambiguity case that failed **loudly**.

Two traps are pinned in tests because both are silent:

- **MediaWiki normalises `_` to spaces in returned titles.** Keying the batched `imageinfo` lookup
  on the title you *sent* misses every multi-word filename — six of seven probe subjects, reported
  as "no file information", which points nowhere near the cause.
- **`width` is a request, not a guarantee.** The source rounds up to standard renditions (400 → 500,
  800 → 960) *while reporting the width you asked for in its metadata*. `photo.width` is measured
  from the decoded bytes and is always true.

Also: **`SKBitmap.Decode` throws `ArgumentNullException("codec")` on undecodable input** rather than
returning null, so the one guard whose purpose is "a bad file from the open web is a named failure"
would have killed the script. And licence prefix matching must be at a **word boundary**:
`"CC BY-SA 4.0".StartsWith("CC BY")` is `true`, so a caller allowing `CC BY` precisely to avoid
share-alike would have been handed exactly what they excluded.

---

### 21.2 Rasters inside SVG deliverables

An `<image>` resolves an external href **only when the SVG is treated as a document** — opened
directly, or embedded through `<object>`/`<iframe>`. Through `<img src>` or a CSS background it
fetches nothing: verified over HTTP with the sidecar serving 200 beside it. Our renderer fetches
nothing either and draws a broken-image cross while the run reports success.

So `paper.image(...)` now takes the **object** — bitmap, canvas, `PhotoAsset`, `MaterialAsset` —
through `IDataUriSource` in `Polson.Runtime`, and inlines it. **Measured cost: +33.5% on disk, 0.3%
gzipped**, because base64 carries six bits in an eight-bit byte.

`outSvg` warns when it saves an `<image>` whose href will not resolve. It **reports rather than
rewrites** — an external reference is legitimate for a document-mode SVG.

**The trap to keep in mind:** an agent told only "the deliverable must be an SVG" can wrap a finished
bitmap in one `<image>`. Measured, both are valid SVG and both open in Illustrator:

| | `<image>` | `<text>` | `<rect>` |
| :--- | :--- | :--- | :--- |
| wrapped bitmap | 1 | **0** | **0** |
| a real vector page | 5 | 65 | 130 |

---

### 21.3 `svgXml` is off the wire

`DrawingExecutionResult.SvgXml` is `[JsonIgnore]` — kept for the CLI and the href scan, never
serialised. Nothing ever consumed it from the response, and an agent run had flagged the duplication
in 2026 as "a few KB per call". Once a bitmap could be inlined it stopped being a few KB: measured
over a live MCP session, **117,786 chars of which 109,045 were the markup — about 29,000 tokens** on
a call that had already asked for `outFile` and `outSvg`. After: **454 chars.**

Removing it exposed the gap it was masking — there was no file-based way back into a saved SVG. So:
`Snap.load(path)` (contained like `outFile`) and `RenderSvg(file:)`. The round trip is now symmetric:
`outFile` → `Skia.Image.load`, `outSvg` → `Snap.load`. Within one session `Session.svg =
paper.toString()` still costs nothing.

ADK-side, `read_file` now **refuses `.svg` outright** — the field had simply come back through the
file reader. `peek` was saying *"read it another way"* while `read_file` said *"peek it"*; both now
name `RenderSvg(file:)`. Pinned in `src/adk_agent/tests/test_svg_reading.py`.

---

### 21.4 The two agent runs, and what they measured

Both on "Visualize the top 5 earning actresses of 2026" — a deliberate trap, since the year is
9 months old and unreported.

**Run 1** (`projects/actresses2026`) produced a technically sound raster piece — zero baseline, lie
factor computed live, palette verified, a 480px reduction test — that **named five women and showed
no faces**, and headlined *"of 2026"* over 2025 figures with the ambiguity hedged in 8pt type.
Neither failure appeared in `findings.md`, because `--test` surfaces friction the agent *hit*, not
capability it never found.

**Run 2** (`projects/actresses2026b`) after two template fixes: five portraits with `expect: 'actress'`,
5/5 resolved, per-card credits plus a footer from `Photo.credits()`, and *"2026 INDUSTRY FINANCIAL
REPORT · 2025 CALENDAR YEAR PRETAX EARNINGS"* with no year in the headline.

**The lesson is about discoverability, not the surface.** Manual 26 ranked first for *"photograph of
a person"*, *"portrait"* and *"likeness of a real person"*, and `polson://sdk/index` listed `Photo` —
and the agent still never asked, because nothing in its 796-line `GEMINI.md` prompted the question.
One paragraph in Stage 1 closed it.

---

### 21.5 `vector_infographic`, and the chart port

**A new workflow; `infographic` is untouched and remains raster.** Note the **underscore** — embedded
resource names mangle `-` to `_`, which is why `comic_studio` is spelled that way.

`VectorChartToolkit` in `Polson.Drawing.Svg`, as `paper.chart(model, options)`. It reads the model
dictionary only — no `ChartToolkit` reference, no Skia — which is what lets it live in the vector
project without inverting the layering. All twelve forms draw; an unknown `type` falls back to
`slots`. `options.colors` is a per-mark array, which a canvas `fillStyle` structurally cannot be.
`JsInterop` moved to `Polson.Runtime` (namespace `Polson`): **141 call sites, zero changed**, because
C# resolves up the namespace chain.

Two bugs found by *counting elements* rather than looking:

- The model's type string is **`progressMeter`, not `meter`**, so the ring fell through to the slots
  fallback and drew one flat rectangle — which still looks like a chart.
- A **full-turn arc whose endpoints coincide is dropped entirely by SVG.** The track ring silently
  vanished; only the fill rendered. Annuli are now walked in ≤180° segments.

And a doc/code drift: **`Chart.createCallout` has no `valueX/Y/Size` anchors** — the reference
claimed them, `display` is just the formatted string, and a caller following the docs drew at `NaN`.
Corrected.

> **ADK interpolates `{var}` in agent instructions.** The first vector run died at startup with
> `KeyError: Context variable not found: 'colors'` because the substitution table contained
> `paper.chart(model, { colors })`. The pattern is `{+[^{}]*}+` and anything that parses as a valid
> state name is looked up: **`{ bareIdentifier }` is fatal**, `{ label: 'Mar', value: 38 }` is safe
> because the punctuation makes it an invalid name. Scan new templates before running them.

Run 3 produced `projects/vectest/artifacts/final.svg` — **513 KB, 5 inlined portraits, 73 `<text>`,
125 `<rect>`, 20 `<circle>`, 4 `<path>`**, with the acceptance check passing in its own footer.

---

### 21.6 Filters and stylesheets — **uncommitted**

`SnapFilter.cs`, `SnapStylesheet.cs`, and edits to `SnapPaper.cs`, `GlobalUsings.cs`,
`Polson.Drawing.Svg.csproj`, `docs/Polson.core.md`.

**The raster gap was our own whitelist, not the renderer.** Svg.Skia draws the whole filter chain;
`CreateElementByName` simply had no `filter` or `fe*` entries. Verified rendering: `feTurbulence`
(**this *is* Perlin** — the same algorithm `Skia.Shader.perlinNoise*` wraps), `feGaussianBlur` (the
`MaskFilter.blur` equivalent), and **turbulence driving `feDisplacementMap`, which gives a genuinely
roughened contour** — the nearest vector idiom to `PathEffect.discrete`, and the most promising lead
for the brush gap.

`paper.style(css)` applies a stylesheet **and** keeps the `<style>` block. Both halves are necessary:
the first attempt wrote only the block and rendered **black in the peek** while looking correct in
the file, because this library resolves CSS inside its parser, not in memory. It parses with
`Polson.HtmlParser`'s existing `CssToolkit` — one CSS implementation, not two — and
`Drawing.Svg → HtmlParser` is acyclic because HtmlParser is a leaf. **Call it last**; it resolves
against the tree as it then stands. Selectors: `.class`, `#id`, tag, `*`/`:root`, comma-separated.

---

### 21.7 Pick-up list

1. **Commit §21.6**, or review it first — it is the only outstanding code.
2. **Tests and manual coverage for the filter and stylesheet surface.** They have none beyond manual
   renders, unlike the chart port's 20. This is the clearest debt this session leaves.
3. **The brush question**, now precisely bounded: `Skia.Brush`, `Skia.PathEffect`, `Skia.MaskFilter`
   and SkSL shaders are the *entire* vector-side deficit — everything else has a counterpart, and
   `feTurbulence` + `feDisplacementMap` may cover much of what a brush was for.
4. **`SKSvgCanvas` — parked deliberately.** It exists in SkiaSharp 4.148 (`Create(SKRect, Stream)`)
   and emits real `<text>` with per-glyph positions. But it **silently drops the media layer**:
   a Perlin shader became `<rect/>` with no fill, a blur became a hard-edged ellipse. It converts a
   loud constraint into a silent one, and it has no pixels, so the whole Manual 15 measurement suite
   would have nothing to read. Revisit only as a *third* surface with peek-from-SVG.
5. **Photo:** session-only cache (`Id` is a stable content address, so a durable one is a drop-in);
   **no face detection**, so the upward crop bias will eventually decapitate someone.
6. **The ADK agent's instructions still never mention `Snap.load`**, so an ADK-hosted agent learns of
   it from a refusal rather than its brief.
7. **`Program.cs` has an orphaned doc comment** — `ConfigureAssetRequisition`'s `<summary>` sits above
   `Setting()`, ~45 lines from its method. Pre-existing, untouched.

---

## 22. The filter and stylesheet surface, covered — 2026-09-06

**§21.7 items 1 and 2 are done.** §21.6 was committed as `591d510`, and the debt that commit left —
"tests and manual coverage, none beyond manual renders" — is paid. **2,038 .NET tests, all passing**,
up from 1,987.

### 22.1 The tests

`tests/Polson.Tests.Drawing/SnapFilterTests.cs` (30) and `SnapStylesheetTests.cs` (21).

**Both suites assert on sampled pixels wherever a call claims a visual effect**, not on the
serialised markup alone. That is not house style for its own sake: it is the exact failure this
surface invites, and the one the stylesheet half actually committed during §21.6 — a `<style>` block
that serialised perfectly and rendered black. A test reading only `result.SvgXml` would have passed
on the broken version.

- The blur test renders the same square twice, filtered and not, and compares a pixel *outside* the
  rect's own edge. A single-render assertion could not tell a blur from a shape that happens to be
  grey there.
- The turbulence test samples 24 pixels across a row and requires them to disagree. An ignored
  primitive leaves a flat fill, which is a spread of zero.
- The displacement test samples alpha down what was a straight edge. The claim is that the contour
  stopped being straight, so that is what is measured.
- The class-rule test carries an explicit **control**: the identical tree with no `style()` call must
  render SVG's default black. Without it the assertion could pass on a coincidence.

Two adjustments were needed and both were the test's fault, not the code's: `result.Logs` entries
carry a `[LOG]` prefix, so a count logged from JS arrives as `"[LOG] 1"` — there is now a `Logged(...)`
helper in each file — and a filtered shape drawn over a white ground is opaque everywhere, so the
edge assertions read the red channel rather than alpha.

### 22.2 The manual, which was actively wrong

Manual 14 §9 told agents the vector surface has **no procedural noise, no blur and no colour
grading**, and §2's decision table sent any scene needing "texture, grain, noise" to the raster
canvas. Both statements predate `paper.filter()` and neither was true any more. This is worse than an
omission: an agent following §2 rebuilt a vector scene as raster to get a capability the vector
surface already had, and would have had no way to discover the mistake.

Added **§6a Filters** (the primitives, each named against its canvas counterpart; the
`turbulence` → `displacementMap` roughened-contour recipe; why `region` is not optional) and
**§6b Stylesheets** (call it last, both halves, the return count). §9 now lists what is genuinely
absent — `Skia.Brush`, `Skia.PathEffect`, SkSL — and says so much more narrowly. Four trap rows and
three symbol-map rows follow.

### 22.3 Vector brushes — §21.7 item 3, closed

**2,063 .NET tests, all passing.** `src/Polson.Drawing.Svg/SnapBrush.cs` + `SnapBrushApi.cs`,
`Snap.brush(...)`, `paper.brushStroke(...)`, 24 tests, and Manual 14 §6a1.

A **nib** is a closed outline along a straight backbone from `(0,0)` to `(100,0)`: a point's `x` is
where along the stroke it sits, its `y` is how far off the centre line. Bending it onto a path is one
step per point. The output is a filled `d` string, so it reaches `outSvg` as geometry.

**Adapted from `reference/projects/svg-brush-main` (MIT), ledgered the same day.** The approach, not
the code — three things differ and each fixes a defect rather than a preference:

- It walks a **polyline**, so its tangent is piecewise-constant and jumps at every vertex; it hides
  that behind RDP at tolerance 0.3 plus a quadratic smoother that does not interpolate its own
  vertices. `SKPathMeasure` gives position *and* tangent exactly on the curve, so there is nothing to
  hide. We still run RDP, but at 0.08px and **purely for size** — 76 KB of path data to 14.5 KB on
  the preset sheet, no visible change, and a test pins both halves of that claim.
- It flattens the target's sub-paths into **one** point list, which draws a bridging stroke through
  the gaps. Each contour gets its own stroke here.
- Its template is sampled at fixed resolution, so a template edge spanning a long arc emits one
  straight segment. Subdividing against the *target's* length makes that impossible; the source has
  it as an opt-in flag with two binary searches and a documented performance penalty.

**Its brush data is excluded and this is the part to remember**: `FigmaBrushes.ts` is 235 KB of the
381 KB and is traced from Figma Draw. An MIT declaration covers the author's code, not another
party's artwork — the OpenPDN row again. All four presets are generated from formulae instead.

**Three defects were found by the guards, not by review**, which is the argument for them:

1. `Snap.brush('taper')` returned an **empty nib and drew nothing, reporting success** — the sniff
   asked whether the string contained a path command letter, and `taper` contains `t` and `a`.
   Presets are now matched first and path data must begin with a move.
2. `Snap.brush.taper(...)` was silently absent: enumerating a Jint CLR wrapper's own keys yields
   none, because members resolve lazily. Copying members across instead reached **the `mina` trap
   already documented** — JS binds `this` to `Snap.brush`, interop takes it for the CLR receiver.
   Explicit `ClrFunction`s carry their own target.
3. `ApiDocumentationTests`, `ManualCoverageTests` and `CoreReferenceExampleTests` between them caught
   undocumented members, a manual-coverage floor breach, and a reference example that did not run.

**What it does not do:** no medium — `Skia.Brush`'s grain and soft edge are canvas state and have no
vector counterpart, so this gives a mark's *shape*, not its texture. `PathEffect.stamp`/`.hatch`
remain raster. Manual 14 §9 is narrowed accordingly. Self-intersection on a bend tighter than the
nib's half-width is unhandled; `CanvasPath.simplify()` is the fix if it bites.

### 22.4 Grain, and two renderer traps that cost nothing to know and a session to find

**2,068 .NET tests.** Prompted by a grain snippet the director brought in from Gemini. It rendered
flat, and finding out why turned up one bug of ours and two deviations from the SVG specification.
Both deviations are now pinned by tests that **fail if a renderer upgrade fixes them**, so a change
in behaviour is noticed rather than silently re-meaning every chain in the codebase.

**The bug was ours.** `filter.flood(colour)` painted **black whatever colour you passed**.
`SvgFlood.FloodColor` is a typed `SvgPaintServer`, not a string attribute, so writing `flood-color`
through the generic attribute path serialised correctly and rendered wrong — the same shape as the
stylesheet defect in §21.6, and found the same way, by looking at a render rather than at the code.
`dropShadow` had it too. Both now set the typed property.

**Trap 1 — an omitted `in` is `SourceGraphic`, not the previous primitive's result.** The
specification says the opposite for any primitive after the first, so a chain written correctly to
the spec builds its noise branch, never consumes it, and renders the untouched source. No error, no
warning, and the symptom is indistinguishable from the filter being unsupported. **Name every input.**
This is the single most expensive thing to not know about the filter surface.

**Trap 2 — the `0 0 0 19 -9` alpha row renders an empty frame.** It is in nearly every grain recipe
published for the web, it is valid SVG, and it hard-thresholds noise alpha to sharpen speckle. Here
it produces nothing at all. Drop the `colorMatrix`; the composite already clips the noise.

**What actually works**, and it closes the *medium* half of the gap §22.3 left open:

```js
const grain = paper.filter().region(-0.2, -0.5, 1.4, 2)
     .turbulence(0.85, 4, 'fractalNoise', 0, 'noise')
     .composite('in', 'noise', 'SourceGraphic', 'grain')      // speckle inside the mark
     .blend('multiply', 'SourceGraphic', 'grain', 'inked')
     .turbulence(0.9, 4, 'fractalNoise', 3, 'rough')
     .displacementMap(7, 'inked', 'rough');                   // ragged edge
paper.brushStroke(spine, Snap.brush.taper(46, 0.7)).attr({ fill: '#2b4c7e', filter: grain.url });
```

A nib gives the mark its **shape**, this gives it its **medium**. Manual 14 §6a carries the recipe and
both traps; §9's "no drawing media" bullet is narrower again as a result — what remains genuinely
absent is `Skia.PathEffect.stamp`/`.hatch` and SkSL.

⤷ **A note on provenance, since this began with pasted code.** The snippet was **read and rendered
before it was scanned**, which is the wrong order; scanned afterwards and clean (pure ASCII, no
concealment classes, no injection phrasing, no script, event handlers, `DOCTYPE`/`ENTITY`, external
`href` or `data:` URIs). Its two real defects were a wrong `xmlns` — `http://w3.org`, which renders
nothing anywhere — and a filter region *narrower* than the default, which clips the round caps square.
**Its other two "faults" were ours, not its**: the code is correct SVG and this renderer deviates.
Worth separating, because "the model got it wrong" was the comfortable reading and the wrong one.

### 22.5 What is still open from §21.7

Items **4–7 stand unchanged**: `SKSvgCanvas` parked, `Photo`'s session-only cache and absent face
detection, the ADK instructions' silence on `Snap.load`, and the orphaned doc comment in
`Program.cs`. Item 3, the brush question, is §22.3 and §22.4 above.
