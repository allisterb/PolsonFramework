# Session Handoff — 2026-09-01 (fourth session)

State after the session that set out to fix the manuals and found that **writing a manual is how you
find the bugs**. Five manuals were written; each one surfaced a defect in the surface it documented,
and none of those defects was found by reading code.

**Tests: 1,135 .NET, 200 Python — all passing.** 18 manuals. The previous handoff is superseded; its
open items are carried forward at the end, marked with what changed.

---

## What this session was actually about

It started with one observation — *"I asked for an SVG of a seagull riding a bicycle and got a
raster webp"* — and the diagnosis generalised. The studio had three overlapping problems, and they
compound:

1. **The manuals covered 58% of the SDK**, and the measurement said 41% because the instrument was
   counting English words.
2. **The engine let a script invent members on any .NET object.** A typo was not an error; it was a
   new property, and the drawing came out unchanged with nothing anywhere saying why.
3. **The run record held only actions.** A run that measured and was satisfied left the same trace as
   one that measured, found the value wrong, and redrew four times.

Each was invisible in a different way. The through-line is the one the last handoff already named:
**everything looked like it worked.**

---

## 1. Five manuals, and a defect from each

| Manual | Source | The defect writing it found |
| :--- | :--- | :--- |
| **14** Vector Construction & the Raster Boundary | *Graphic Design and Print Production Fundamentals* ch. 5 (CC BY 4.0) | `element.children` was a method documented as a property, so `.length` answered `0`; `matrix.transformPoint` returned a tuple whose `.x` read `undefined`; `element.type` said `"definitionlist"`; `outSvg` wrote nothing and reported success |
| **15** Measuring a Render | OCSM C&C '23 (CC BY 4.0) + CSM C&C '17 | The size-mismatch throw in `bitmap.diff` **cannot be caught** — `try`/`catch` does not run and the script ends |
| **16** Requisitioning Material | none — a Polson subsystem | `classify` reported trigger `"man"` for a descriptor containing `"woman"`, naming a word the caller never typed |
| **17** Drawing Media & the Made Mark | Klaus Janson, *DC Comics Guide to Pencilling* ch. 1, 13 | `BrushPreset.color` **read back as changed and drew the old value** — a script could verify its own change and be wrong |
| **18** The Run Record | CSM C&C '17's three stated limitations | `Stage.current` returned `null` where documented `undefined`; `console.info` was not an alias for `log`; `mina.backin/backout/elastic` leave the `0…1` range |

Manual 16's runnable plate **spends nothing by construction** — `material()` classifies before
touching the network, so a deliberately form-shaped descriptor exercises the whole failure protocol
for free on any machine. There is a `[!CAUTION]` on it saying exactly why, so nobody pastes it with
the descriptor changed.

### Coverage, honestly measured

`ManualCoverageTests` was counting a bare word anywhere in the corpus, which counted ordinary English:
a vector manual that never mentions requisition moved `Assets` from 35% to 58% on the words *bytes*,
*size* and *success*. It now requires a leading dot — `.getPointAtLength`, not `getPointAtLength` — so
it counts the call being **used** rather than the word appearing.

| Area | Session start (inflated) | Honest instrument | Now |
| :--- | ---: | ---: | ---: |
| Skia | 53 | 40 | **100** |
| Globals | 44 | 5 | **100** |
| Drawing | 100 | 97 | 97 |
| Assets | 35 | 17 | **94** |
| Snap | 44 | 70 | **82** |
| Canvas2D | 67 | 64 | 75 |
| Scale | 63 | 50 | 54 |
| **Overall** | **58** | **58** | **82** |

Part of the remaining `Snap` gap turned out **not** to be a manual gap at all — see §3.

---

## 2. A misspelled member is now an error

Jint's default lets a script invent members on a wrapped .NET object. `ctx.fillStlye = 'red'` created
a JS-side property, the fill stayed black, and nothing said so. The same mechanism made
`canvas.width = 999` **read back as 999** on a canvas still 16 wide.

Closed with `Options.Interop.ThrowOnUnresolvedMember` plus `Options.Strict`.

> **The trap, if this ever seems to break `Assets`:** `await x` reads `x.then` to test for a thenable
> and `JSON.stringify` reads `toJSON`. With the setting on, those *internal* probes throw on every
> .NET object — it broke requisition outright. The fix is the two-name `InteropProtocolMembers` set fed
> to `SetMemberAccessor`. **Never add `toString` or `valueOf`** — it would shadow `paper.toString()`.

**The message is most of the value.** `ExplainMissingMember` reflects on failure (zero cost on the
drawing path) and produces *"Did you mean 'fillStyle'?"*, distinguishes read-only members from missing
ones, and filters suggestions through `JsSurface.NotSurface` so it never proposes `getType`.

That suggester then got two fixes **from a live run that it had misled**:

- A name **extended at the end** is the commonest real mistyping and a length window rejects it. The
  script wrote `perlinNoiseFractalNoise`; the correct `perlinNoiseFractal` is five characters shorter
  and was excluded, while `perlinNoiseTurbulence` fell inside the window and was suggested instead.
  **The agent took the suggestion** — `perlinNoiseFractal` appears in no script in that run, and the
  finished painting used the wrong noise family. Ranking is now by longest shared prefix.
- A name carried in from another library has no near miss on the receiver it was aimed at.
  `Skia.RuntimeEffect.make()` is CanvasKit's spelling; the message now says *"…but
  `Skia.ColorFilter.runtimeEffect` exists elsewhere on the surface."*

**Read this as the cautionary half of §6.** A wrong signal travels down the improvement channel as
readily as a right one, and the agent cannot tell which it received.

---

## 3. The manifest was advertising names nobody should type

`Snap` rose 70% → 82% **with no manual text written**. The rule for capitalising a member was "any
property whose type is a receiver", which asks a different question and answered it wrongly for every
property that merely *returns* one: `Snap.Path`, `element.Paper`, `paper.Defs`, `canvas.Bitmap`,
`Assets.Budget`, `element.Parent` were all published capitalised, spellings the reference does not
document.

A namespace accessor is now one **the registry names in its own right** — `Skia.Shader` is a receiver
entry, so it keeps its capital; `element.paper` merely returns one, so it does not. Exactly nine
symbols are capitalised now, all `Skia.*`, and a test asserts nothing else ever is.

Two more found while verifying: exclusions did not follow inheritance (`element.node` was excluded as
a raw escape hatch but `paper.node` and `gradient.node` leaked through), and `Search` returned
`Skia.Shader` with the signature `shader: SkiaShaderApi`.

---

## 4. The run record can now say whether the result was wanted

Every event was an *action*. Three things changed that.

**`observe` — the measurements record themselves.** `ProbeScope.Record(Kinds.Compare)` was firing on
the way **into** `bitmap.diff`, so the similarity, bounds and `identical` flag were computed twenty
lines later and discarded. They are recorded on the return path now, for `diff`, `palette` and
`rowProfile`. Capped at 32 per execution, with `outcomesDropped` reported rather than truncating
quietly.

> A `rowProfile` that matches nothing returns an empty array, the loop over it never runs, and a
> script sails past a colour that was never drawn. The record now says `no row matched` even when the
> script did not notice.

**`expect` / `check` — the agent's own claims.** `Stage.expect(claim)` before the render;
`Stage.check(claim, passed, detail)` after, returning the verdict so it reads as the test it is. The
division is deliberate: `observe` is machine-recorded and cannot be overstated, `expect`/`check` are
the agent's and can be — which is the only way a record can hold an *intention* at all.

**`asset.requisition` / `asset.refused` / `budget`** — specified in `project-layout.md` and never
written, so a run could not say what it bought. Collected by `RequisitionScope` (mirroring
`ProbeScope`, so `Polson.ExtendedMind` still knows nothing about a run event log) and drained by
`DrawingMcpTools`. Recorded from `Acquire`, which its own comment already called *"the only path that
can spend money"*.

Three things that would have been wrong:

- A **refusal is a separate event**, not a failed requisition — nothing reached, nothing spent, remedy
  is to reword. A run that spent its time rewording should not read like one the service kept refusing.
- `result.Charged` is the wrong source for "was this cached": a cached result is a replay of a charged
  one and carries the flag with it. It reads `Budget.CacheHits` before and after.
- The budget snapshot must be **pushed by the toolkit**, not read from `JsDrawingEngine.Assets` — the
  engine substitutes its own disabled toolkit with `AssetBudget(0)` when none is configured.

**The event serializer only handled `Dictionary<string,int>`.** Everything else fell to `ToString()`,
so `bounds` would have been written as the dictionary's *type name* — a field that looks present and
says nothing. It recurses properly now.

**CSM coding** (`src/webapp/orchestrator/csm.py`): `expect`/`check` code as `communicate`;
`asset.requisition` as `gather` (a requisition-only script renders nothing and succeeds, so it
contributed nothing to the curve before); `asset.refused` as `attempt`. **`observe` is deliberately
uncoded** — one per measurement inside an execution that already emits a single `inspect`, so coding
both would count one looking-episode up to thirty times and drag the curve toward unclamped *in
proportion to how thorough the checking was*. `budget` is uncoded as a state snapshot.

---

## 5. `painting-4`, re-run — what it proved and what it did not

Its old MCP config had Linux paths (`/home/allisterb/...`, `/mnt/c/...`), so the first launch failed
with `invalid character 'P'` — `dotnet` printing "could not execute" onto stdout where JSON-RPC
expected the `initialize` reply. **Every other project has `C:/Projects/Polson/...`.** The run was
`--reset` and re-run natively.

**The critique produced the thing the original run could not:**

```
expect  Dominant tonal values must remain low-key (>60% in shadows and deep tones)
check   Dominant tonal values low-key >60%   → false   "Measured 57.1%"
        … refinement pass …
check   Low-key tonal hierarchy              → true    "Dark values dominate frame…"
```

Same fault class as the original incident — value compression — found as a number, fixed, re-checked,
recorded. `materials.md` was written during `Requisition` rather than as a closing checklist.

**Two gaps it also showed**, both now warned about in `RunReport`:

- **6 expectations stated, 4 settled.** An unsettled `expect` reads as verification and is not.
- A run whose every check failed has **diagnosed without demonstrating**.

Manual 15 §8, Manual 18 §3a and both picture workflows now require every claim to be settled and the
passing check after a fix to be **recorded** — the old stopping rule ("keep going until the check that
failed passes") could be satisfied by a `log(...)` that reaches one tool call and vanishes.

**Two findings from the requisitions**, both now in Manual 16:

- The same backdrop was **charged twice**: `keepQuiet: 'lowerThird'` goes into the prompt, the prompt
  goes into the content hash, so dropping it is a cache miss. The retry was a reasonable judgement
  paid for at full price.
- The cache survives `--reset` (it lives outside the project), so 2 of 5 material calls were free.

**On the speed question, honestly:** the run felt roughly twice as fast, and the surviving brain-step
files support that (~16.5 min for steps 21→86 previously, 9.1 min for six stages now). But engine-side
it is *slower* per script, and `scripts/render` is 3.5 against `painting-2`'s 1.7. The likeliest cause
is not the code: the previous run was under **WSL against `/mnt/c`**, this one native Windows. Cache
hits and the absence of the missing-native-library failures the old Linux run hit account for more of
the rest.

---

## 6. Stigmergy runs in two directions, and the second is ours

`CLAUDE.md` §2 said all five theoretical frameworks were "derived from Nicholas Davis and 4E Cognition
research (available in `@reference/papers/`)". Checked against the corpus:

| Concept | Verdict |
| :--- | :--- |
| Enactive Cognition | Sound — argued throughout |
| Five Pillars | Sound — `ICCC24_paper_58.pdf` names and defines them |
| Creative Sense-Making | Sound — C&C '17 + dissertation |
| Extended Mind | **Weak** — Clark & Chalmers; the corpus reaches it only via a bibliography entry |
| Stigmergic Collaboration | **Absent** — *zero* occurrences across all nine papers |

§2 now states provenance per item, credits Grassé (1959) and Elliott (2006), and marks both as **not
in `reference/`** so nobody hunts for them there.

It also names the second direction, which is not in any cited work. **Horizontally** the trace is read
by a peer working in the environment as it stands — most often the agent's own earlier pass.
**Vertically** it is read by whoever can change what the environment affords, so the next agent does
not need the trace at all. Every defect in §1 arrived that way. `docs/devpost/Polson.md` now carries
the same argument with the worked table.

---

## 7. Where to pick up

**Ordered by what would most improve the next run.**

- **Rebuild `bin/cli`.** Everything this session was built with `-p:SkipCopyToBin=true` because a live
  agent session holds it open. Nothing above reaches an agent until `bin/cli` is current.
- **`CLAUDE.md` is not in version control.** `.gitignore` has `/*.md` with only `!/README.md`, so a
  fresh clone gets `GEMINI.md` and `README.md` and **not** the file governing every Claude session —
  including the §2 correction above. `reference/README.md`, the ingestion ledger, is gitignored for the
  same reason: the previous handoff flagged it and it is still true.
- **The OpenPDN raster port.** Full plan in `docs/openpdn-port-plan.md`. Slice 1 is ~800 lines with no
  dependencies and one decision gate. Short version: take `Surface`/`Selection`/`Effect` and the ~25
  effects; leave `Compositor` and the pointer-driven tools; the channel order (`ColorBgra` is BGRA,
  Polson is `Rgba8888`) decides whether the boundary is free.
- **`Recall` has still never had anything to remember.** `--reset` cleared `painting-4`'s events. Use
  `--force` next time — it regenerates the instructions and keeps `events/`, `scripts/`, `artifacts/`.
- **A passing check is not backed by a number the way a failing one is.** `painting-4` recorded
  *"Measured 57.1%"* on the failure and *"Dark values dominate frame…"* on the pass. The record cannot
  say whether 57.1% became 61% or 58%. Judgement call whether to push harder.
- ~~**`ImageGenerator` has no injectable seam.**~~ **Done 2026-09-01.** `IImageGenerator` carries just
  the two members the toolkit uses — `Model` and `GenerateImage` — and `AssetRequisitionToolkit` takes
  it. `HashOf` and `TryReadPngSize` stay static on the concrete type: a substitutable cache key would
  let two implementations disagree about what "the same request" means.
  `RequisitionSuccessPathTests` covers everything after a successful generation, offline and for
  nothing: budget spend and token count, cache write, the *repair* of a non-wrapping swatch (and that
  `tileable: false` leaves the seam alone), provenance, the library, and — the part that had no
  producer before — the `asset.requisition` record's **success** shape, including a cached repeat
  reporting `fromCache: true`. The fake counts its calls, which is what makes the cache assertion mean
  anything: a second requisition the transport never sees is a cache hit, where a second requisition
  returning an equal asset proves nothing. **1,141 .NET, 200 Python.**
- **The devpost stigmergy section is written; the rest of that document is not reviewed.**

### Carried forward, still open

- **Multi-agent tracing.** `manage_subagents` gives role → conversationId → transcript path; a hook
  that parses it is the cheap fix and is not built. Unchanged.
- **Attribute the spine.** `_code_server` passes no `agent`, so every execute/inspect event codes as
  one actor. Stage-based is the only mechanism that works under every host. Unchanged.
- **Name the registered subagents in `GEMINI.md`**, or stop emitting `agents.json`. Unchanged.
- **A second image provider**; **Agent Memory Bank**; **Claude support** (Agent SDK / plugin).
  Unchanged.
- **Magick.NET is in `CLAUDE.md` §3 and in no `.csproj`.** Unchanged.
- **`painting-4` has no `output.webp`** — was true of the old run; the new run was still going when
  this was written. Check it.

### The flaky one

`Polson.Tests.Drawing.IrradiationCompensationTests` fails intermittently **only** under a
full-solution parallel run, a different test in the class each time, and passes standalone every time.
Not diagnosed; probably contention on a shared Skia font or render resource. Re-run the class alone
before believing you broke it, and **do not bisect by stashing `src/`** — the tree usually carries
uncommitted work and stashing pulls it out from under the untracked tests that depend on it.

---

## What the record now looks like

A two-stage slice of the re-run, which is the shortest way to see what changed:

```
Requisition  note               wanted oak for the hull; the first descriptor named the ship
Requisition  asset.refused      a wooden pirate ship
Requisition  asset.requisition  weathered ship hull planking -> success, fromCache=false
Requisition  budget             1/120 spent, 1363 tokens
Critique     expect             the accent should stay under 15% of the frame
Critique     check              accent under 15%  ->  false   "measured 22.5%"
Critique     render             artifacts/07_critique_1.webp
Critique     observe            diff: 96.0% similar, 960 of 24,000 px differ, within 98x60 at 20,30
Critique     observe            palette: #FAF8F4 77.5%, #1F6F8B 22.5%
```

Every line of that was invisible at the start of the session.
