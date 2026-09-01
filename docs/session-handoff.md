# Session Handoff — 2026-08-31 (third session)

State after the session that went looking for multi-agent support and came back with something more
useful: the answer to *why the director's answers were never reaching the agent*, the studio's first
memory across runs, and a measurement of how much of the SDK an agent can actually find.

**Tests: 1,052 .NET, 200 Python — all passing.** The previous handoff is superseded. Its "where to
pick up: multi-agent" section is **not** done, and is carried forward below with much better
information than it had.

---

## What this session was actually about

It started as multi-agent and became **delivery**. Three separate defects were each silently
discarding the human's contribution or the agent's output, and every one of them returned success at
every layer:

1. The director clicked an option; the page posted an invented id; it settled as a skip.
2. The page was fixed; the runner returned the option's *text*; the SDK's parser dropped it.
3. A painting's critique correctly diagnosed and fixed a value-compression fault, then ended the run
   with the corrected picture in `artifacts/` and no `output.webp`.

The through-line worth keeping: **every one of these looked like it worked.** A run that fails loudly
costs an hour. These cost a whole session each and left artifacts that looked fine.

---

## 1. The director's answers were being thrown away — twice

**First layer.** The question card rendered option buttons with `value="opt1"`, `"opt2"`. The runner
validates a reply against the agent's own option ids *and* the option texts it published, so `opt1`
matched neither, fell through to the free-text branch, found none, and returned `skipped=True`. The
POST returned 200 and the card showed as sent. `drawing-1`'s three setup questions were all recorded
as `skipped` — the subject, the canvas size and the critique cadence were the agent's own choices.

**Second layer, found the same evening.** With the page fixed, replies came back carrying the option
*text*. The SDK's event processor resolves a choice with `int(opt_id) - 1` and silently drops
anything that will not parse, so the agent received a multiple-choice answer with **nothing
selected** — indistinguishable from silence. `drawing-3` has four `Director stepped away` notes
against four answered questions.

It stayed invisible because the agent's fallback is to take the option it marked *recommended*, which
is usually the one the director clicked. Both outcomes produce the same drawing and the same note.

Fixed: the page posts the option's own text, and `WebDirector` maps a reply to that option's `id`.
`inf-4` and `c-9` now show `id=['1']`, `id=['2']` and **zero** stepped-away notes.

> **The test that let this through matters more than the bug.** My first regression test passed while
> the second layer was broken, because its fixture gave options **empty ids** — modelling a world
> where returning text is harmless. `AskQuestionOption.id` is required and the host numbers it from 1.
> The fixture now numbers them, and one test runs the SDK's own `int(opt_id) - 1` parse over the
> response. A fixture that does not model the real contract tests nothing.

---

## 2. Multi-agent under the desktop: what is actually true

`cs-3` ran the four-role comic studio under Antigravity Desktop. It answered every open question from
the last handoff, mostly unfavourably.

- **Subagents get their own `conversationId` and brain directory.** `preserve-chatlog` copies the
  transcript the payload names, and the payload only ever names the *parent's* — so the Penciler's
  entire 23-step trace, including its `ExecuteScript` calls, is absent from `events/`.
- **`manage_subagents` with `Action: list` hands over the mapping**: `role`, `type`,
  `conversationId`, and an absolute transcript path. A hook that parses those results could follow
  and copy each one. That is the cheap fix and it is not built.
- **`send_message` is real agent-to-agent messaging**, addressed by `conversationId`, arriving in the
  recipient as a `SYSTEM_MESSAGE` step. Child→parent is proven; parent→child is presumably the same
  call and unobserved.
- **The orchestrator ignored `.agents/agents.json` entirely.** It called `define_subagent` and built
  *one* generic `comic-artist` with its own synthesised prompt, reused for all four stages. Root
  cause is ours: `GEMINI.md` says "run them as separate subagents" and never mentions that four are
  already registered or how to invoke them. So `roles/*.md` reached nobody, and one subagent name
  across four stages means subagent identity cannot distinguish Penciler from Inker either.

**Consequence for attribution.** `csm.py`'s `_code_server` never passes an `agent`, so every spine
event — the whole execute and inspect column — codes as one actor called `agent`. `Curve.per_agent()`
would produce one fat curve holding all the drawing plus thin per-trajectory curves holding only
text. That is worse than not splitting. The self-declared `Stage` is currently the **only** thing
that can separate roles under a desktop host.

---

## 3. The MCP server can be rooted at the wrong project

A `cs-3` subagent's `ExecuteScript` with a relative `outFile` returned:

```
imageFilePath: C:\...\tests\agent\infographic\agy\inf-2\artifacts\stage1_penciler.webp
```

`cs-3/.agents/mcp_config.json` correctly names cs-3. An earlier `inf-2` server was still alive and
served the call. **Path containment passed — against the wrong root.** The agent noticed the file was
missing, retried with an absolute path (correctly refused), then used `run_command` to copy the
render across and reported success. Result: an artifact with no render event on one side, a render
event for another project's picture on the other.

Detection is one field: compare `imageFilePath` in the first `ExecuteScript` result against the
project directory. Mechanism unproven — the global MCP config is empty, so it is most likely an
application-level client keyed by server name. **Close the previous session before starting a new
one**, and check the first render.

---

## 4. Local episodic recall — the studio's first memory across runs

`src/Polson.MCPServer/Memory/EpisodicMemory.cs` reads a project's `events/server.jsonl` back into
**episodes**: one stage of one earlier run, carrying its notes, scripts, artifacts and failures. The
unit is deliberate — it is the unit the agent itself declared, and splitting finer recalls a script
without the intent behind it. The current run is excluded; that is `History`'s job, and including it
would let a run rank its own half-written notes as insight.

`Recall(query, k)` ranks them with BM25, notes weighted 2× because they are the only part written for
a reader. It distinguishes **three** outcomes, not two:

| `runs` | `count` | Meaning |
| ---: | ---: | :--- |
| 0 | 0 | This project has never run. Nothing to remember. |
| >0 | 0 | Earlier runs exist and none mentioned this. |
| >0 | >0 | Here is what happened. |

That is aimed at the oldest open item — *"Search cannot express a negative"* — the defect that once
convinced an agent vector gradients did not exist.

`_shared/recall.md` tells every workflow to call it before planning, to **open what it recalls** with
`Skia.Image.load` (which is what finally populates `artifact.read`), and to write notes for the run
that comes after: *"a note saying `rendered stage 3` recalls nothing to anybody."*

**Three agents called it unprompted, and all three got `runs: 0`** — every project was on its first
run. The plumbing is exercised end to end; **the payoff is untested.** The way to see it work is to
re-run `inf-4` or `c-9`, not to start `inf-5`.

One observation for when you do: `inf-4` asked `Recall('initial check on past runs')` — a *status*
query, not a content one. Harmless against an empty memory; against a populated one it would rank on
whatever shares those words. Worth tightening the wording only after a real second run shows how it
actually goes wrong.

---

## 5. Three single-agent workflows, and four shared instruction blocks

New: **`drawing`** (turn-based co-creation, pencil and pen, `Assets` denied), **`comic`** (one agent
through pencil → colour → ink → critique), **`painting`** (the only workflow that requisitions
material). Each with `review` / `seed` types on the first two; `painting` has none.

`drawing` uses the **collaboration move as the stage name** — `Ground`, `Offer`, `Accept`,
`Elaborate`, `Depart`, `Critique` — so the CCSM collaboration-dynamics layer lands in the record
directly instead of being inferred from artifact deltas. `bitmap.diff` is the check on the claim.

Four `_shared/` blocks now render into every workflow through tokens, each with a drift test that
enumerates `ProjectGenerator.KnownWorkflows` rather than listing workflows by hand:

| Block | Why it is shared |
| :--- | :--- |
| `engine_only.md` | The rule that decides whether a run is real |
| `blank_brief.md` | Every workflow can be started from the form with an empty brief |
| `deliverables.md` | The host's artifact-writer refuses project paths; the trap is the host's, not the workflow's |
| `recall.md` | A tool nobody is told to call is never called |

**Three hardcoded mirrors bit us this session** — the webapp's `WORKFLOWS` map (which had never
offered `logo` or `infographic` types at all), the auto-approve list in a test, and the engine-only
theory's `[InlineData]` list of four workflows. All three now derive from the thing they mirror.

---

## 6. Delivery: the fixes that make a finished run findable

- **The critique now says how it ends.** All three picture workflows state that the corrected render
  *is* `output.webp`, written with `outFile` as part of the stage — the staged renders are the trace,
  and the newest is not the deliverable just because it is newest. `painting-4` ended with its fix in
  `artifacts/07_critique_1.webp` and no `output.webp`, so a viewer saw the frame *before* the fix.
- **"Fix at least two things" became "keep going until the check that failed passes."** The old
  wording gave a stopping rule an agent could satisfy while the defect survived.
- **`materials.md` moved to the `Requisition` stage.** Provenance written while the facts are in
  front of you, not recalled at the end — and a closing checklist is the part a run that ends early
  never reaches.

---

## 7. Discoverability: 41% of the SDK is not in any manual

`ManualCoverageTests` measures `JsSymbolManifest.Symbols` against the manual corpus and **ratchets**:
floors set *at* the measured value, so improving a manual always passes and adding unreachable
surface fails, with a message naming the missing symbols.

| Area | Coverage | Notable absences |
| :--- | ---: | :--- |
| `Drawing` | 100% | manuals 01–09 are built on it |
| `Canvas2D` | 67% | `roundRect`, `drawSvg`, `dither`, radial/conic gradients |
| `Skia` | 54% | **`bitmap.diff`, `rowProfile`, `getPixel`, `palette`**, `Brush.chalk/marker/stipple` |
| `Snap` | 44% | `element.attr`, `transform`, `select`, `clone` — most of the vector API |
| `Assets` | 35% | the newest and only metered surface |

The measurement is generous — a word-boundary match on the bare member name — so every figure is an
upper bound.

**The incident that prompted it.** `painting-4` reached its `Atmosphere` stage, was pointed by the
workflow at Manual 07, found one shader mention there, and drew haze as flat ellipses at low alpha.
`Drawing.createAtmosphericCloudShader` — *"the isotropic fractal preset for air and vapour"*, with
sea-air defaults — is documented in **Manual 04, cel shading and facial planes.** A landscape painter
has no reason to open the face manual. Fixed with an `Atmosphere` section in the painting workflow
and a cross-reference from Manual 07; the presets really want their own short manual.

**A real defect fell out of the audit.** `AssetRequisitionToolkit : Runtime`, and the symbol walk went
into the base class — publishing 45 .NET infrastructure members as SDK calls, including
`Assets.downloadFile` and `Assets.camelDir`, a name from another project. `Search` promises a
`direct` verdict means the call exists and its signature is authoritative; for those it was false.
`Ancestry` now stops at `Runtime`.

---

## 8. Engine and configuration

- **`keepQuiet: 'lowerThird'` could not be called at all** — documented as a string, bound to a C#
  enum, dying with `Invalid cast from 'System.String'`. Fixed with a Jint type converter.
  **Overriding `TryConvert` alone did nothing**; Jint calls `Convert` on this path.
- **Asset budget 12 → 120**, with the rationale rewritten: a painting is built from many surfaces,
  and a budget that runs out mid-piece is worse than one never reached. A non-positive or unparseable
  value is now warned about and ignored rather than silently becoming 0, which *disabled requisition*.
- **`create-project`'s SDK argument is optional**, defaulting to `agy`; a workflow name in that slot
  gets a hint rather than a bare rejection.
- **`Program.cs` hook block** carried a committed `Console.Write("ff")` that made every Antigravity
  hook reply malformed, a hardcoded `C:\Projects\Polson\bin` log path, and a misbound `else`.
- **`ChatlogPreserver`** compared paths as strings where one side had forward slashes and the other a
  backslash, so it copied the same transcript under both names.
- **`dotnet build -p:SkipCopyToBin=true`** now skips the copy to `bin/cli`, which a live agent session
  holds open. It blocked builds and tests three times in one session.

---

## 9. Licence: MIT → AGPL-3.0

Root `LICENSE` is the verbatim FSF text. `docs/LICENSE` is CC BY-SA 4.0 with a provenance table,
because the manuals distil third-party sources with their own terms that a software licence does not
express. The README gives the reason rather than the name: the studio's headline deliverable is
*hosted*, and running a modified copy as a service triggers no obligation under a permissive licence
or under plain GPL — §13 is written for exactly that.

The §13 offer is live in the studio footer as `POLSON_SOURCE_URL`, a setting rather than a hardcode,
so honouring the licence after a fork is one environment variable. It is a Jinja global so a page
added later cannot ship without it.

Every dependency is permissive and one-way compatible. The hackathon rules mandated no licence type
— though note the entry terms grant the sponsor a broad licence independent of the public one.

---

## Where to pick up

Ordered by what would most improve the next run.

- **Run an existing project a second time.** `Recall` has never had anything to remember. This is one
  command and it is the only way to find out whether episodic memory helps or produces noise.
- **Follow the subagent transcripts.** `manage_subagents` gives role → conversationId → path in the
  parent's own transcript; a hook that parses it and copies each one turns multi-agent tracing from
  impossible into wired. Under Claude Code this is simpler still: `SubagentStart` / `SubagentStop`
  are first-class hook events.
- **Attribute the spine.** `_code_server` needs an actor. Stage-based is the only mechanism that works
  under every host; the transcript join is the machine-recorded cross-check where a transcript exists.
- **Name the registered subagents in `GEMINI.md`**, or stop emitting `agents.json` and let the
  orchestrator define them from `roles/`. Today it does the second by accident and the role specs
  reach nobody.
- **A manual on measuring a render.** `bitmap.diff`, `rowProfile`, `palette`, `getPixel` are in no
  manual at all, which is exactly why `painting-4`'s critique used only `highContrast`. This is the
  highest-value gap the coverage audit found.
- **Asset requisition writes no events.** `docs/project-layout.md` specifies `asset.requisition` /
  `asset.refused` / `budget` and none are wired, so **the run record cannot say what was
  requisitioned** — the exact question asked of `painting-4`. The cache and the scripts had to be
  read instead.
- **Claude support.** The Agent SDK (Python/TypeScript) is the direct analogue of the Antigravity SDK
  that `run.py` uses. Separately, Polson maps almost one-to-one onto a Claude Code **plugin**:
  `.mcp.json`, workflows as `skills/`, roles as `agents/`, the chatlog hook as `hooks/hooks.json`.
  Decide the SDK question first so the plugin and the orchestrator share one definition of a workflow.
- **A second image provider.** `AssetRequisitionToolkit` takes an `ImageGenerator?` by injection and
  the classifier, budget, cache and failure taxonomy all sit above it. Making `ImageGenerator` an
  interface is the one refactor it wants. Check whether MidJourney has an official API before
  designing for it.
- **Agent Memory Bank.** `Google.Cloud.AIPlatform.V1Beta1` is referenced and unused.
  `MemoryBankServiceClient` has `GenerateMemories` and `RetrieveMemories`, both parented on
  `reasoningEngines/` — so it needs an Agent Engine provisioned purely as a memory container. The
  `Recall` contract was shaped so a cloud backend can sit behind it without the workflows changing.

### Smaller, still real

- `painting-4` has no `output.webp`; `07_critique_1.webp` is the finished piece.
- **`bin/cli` goes stale silently.** Three separate confusions this session traced to a template or a
  fix that was built but not deployed. Rebuild after closing agent sessions.
- **Magick.NET is in `CLAUDE.md` §3 and in no `.csproj`.** Raster post-processing is Skia filters.
- The `reference/README.md` ingestion ledger is still gitignored, so a fresh clone has no scan
  verdicts — the outcome the ledger exists to prevent.
- Two artifacts published this session: an architecture reference, and a page for Nicholas Davis
  carrying the real curves, the five-session sign disagreement, the `attempt` row, and the green-bar
  disruption. Both private until shared.

---

## What the runs are actually producing

Ten finished runs. The infographic workflow produced an Apollo 11 LM telemetry blueprint that gave
`45 SEC` — the hover fuel margin — the largest type on the page, and barred the 60–100% region of the
throttle envelope for injector cavitation, which is a real constraint rather than a plausible chart.
`c-9` answered six director questions, three of them at the stage boundaries the comic `review` type
mandates. Since `painting-2`'s `keepQuiet` crash, not one engine failure across any run.

The record is good enough that this handoff was written from it rather than from memory.
