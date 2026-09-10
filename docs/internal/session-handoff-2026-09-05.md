# Session handoff — 2026-09-05

**One sentence:** the studio's documentation was reachable only through a channel that looked broken,
so agents were building from recall; naming a tool fixed it, and the same session added time and
token constraints that turned out to *improve* the work rather than merely bound it.

Everything below is committed as of `a5127bf`. Nothing is outstanding in the working tree.

> **Amend `a5127bf` before anyone reads it.** Its message is *"Add write-write-no render watchdog
> signal"* and that signal was **deliberately not built** — see §5. What landed is
> `SOLO_THRASH_CALLS` plus a re-arm. A commit message describing a feature that does not exist is
> the same defect class as a stale doc.

---

## 1. The thing this session was actually about

The Apollo blueprint run (`inf5`, this morning) hand-rolled a 100-cell waffle as a raw 10×10 grid and
made **zero** `Chart.*` calls, three days after the chart toolkit landed. Chasing that produced four
wrong theories before the right one, and the wrong ones are instructive enough to record.

| I claimed | Actually |
| :--- | :--- |
| "It read `polson://sdk/core/Chart` twice and ignored it" | It never opened it. I had grepped the log for the string and counted **citations inside `Search` results**, not reads. |
| "Search returned zero signatures" | It returned 59. I grepped escaped JSON instead of parsing it. |
| "AGY has no MCP resource support" | AGY Desktop materialises resources to disk. I read the *SDK package* when the runs were on the *Desktop harness*. |
| "The stale lookup list is the root cause" | Manual 13 names `Chart.*` 25 times and **is** in the list. The agent simply read nothing. |

**The real cause:** ADK's `load_mcp_resource` returns only a status line — *"temporarily inserted and
removed… call again"* — and injects the body into the **next** request, where it is never persisted.
One turn of visibility, and an acknowledgement that reads like a failure. The agent tried once, got a
non-answer, and used `Search` for the rest of the run. `Search` gives API **names** without
signatures, and `Chart.createWaffle(rect, parts, options)` is unguessable from its name.

Verified locally, eight checks, no model calls (`scratchpad/verify_resources.py`): server serves the
resources; `run_async` returns no content; body **is** injected next turn; body **is gone** the turn
after; **not** injected when the response is not `parts[0]`; `ReadDoc` returns it in the tool result.

---

## 2. What shipped

**`ReadDoc(uri)`** — an MCP *tool* serving every `polson://` document, so the content lands in the
tool result and persists. Accepts bare forms (`13`, `sdk/core/Chart`). A miss returns the complete
`known` list rather than nothing. `PolsonResources.Read(uri)` is the single resolver.

**`Search`, 26,503 → 3,873 characters (−85%)**, in three steps: one hit per *document* (a long
document was taking five of five slots); 240-char snippets instead of whole passages; signatures
dropped from prose hits, since `ReadDoc` and the dotted-name route both serve that better. Ranking
survived — the query that used to lose `Chart` entirely now returns it 3rd or 4th.

**`doc.read` run events**, with `via=resource` / `via=ReadDoc`. The record had **no** resource-read
event type at all — eighteen types across every run ever — which is why the diagnosis was archaeology.

**`Stage.elapsedMinutes`** — anchored on `SessionContext.StartedUtc`, replacing the
`Session.startedAt ??= Date.now()` recipe, which measured from the *first script* and silently
recorded pre-drawing reading as zero.

**`--budget` / `POLSON_BUDGET_TOKENS`** — a cap on raw input tokens sharing the existing breaker's
trip set, with a 75%/90% warning ladder on `llm_request.contents` (never `append_instructions`,
which is the cache prefix). **Input because it is what spirals**: it grows O(n²) as the conversation
is re-sent, while output stays bounded per turn — measured 1.2M input against 11K output.

**`budget_status`** tool — the agent could not see its own spend; `Date.now()` gave it a clock and
nothing gave it tokens. Reports both allowances plus `cachedSharePercent`.

**Turn log gains `think=` and `tooluse=`.** `think=` is the only signal that sees an agent *thinking*
rather than working — the failure the watchdog structurally cannot observe, since every trigger it
has fires from `after_tool_callback`. (`tooluse=` reads 0 on Gemini 3.7 Flash; the field exists but
this model does not populate it.)

**Watchdog fixed for single-agent runs** — see §5.

**`JsType` no longer leaks CLR names.** `Chart.createColumnChart(...) -> Dictionary\`2` in the symbol
index; now `-> object`, and `Assets.material` correctly reads `-> Promise<MaterialAsset>`, which is
the one surface a script must `await`. All 628 symbols clean.

---

## 3. Proven, not asserted

Eight runs, one brief per configuration, same prompt throughout:

| run | host | change under test | doc reads | `Chart.*` |
| :--- | :--- | :--- | ---: | :--- |
| inf5 | Cloud Run | (before) | 0 | **none — hand-rolled** |
| doctest3 | ADK local | (before) | **0** | **none** |
| doctest4 | ADK local | prompt nudge | 3 | ✔ |
| doctest5 | ADK local | `ReadDoc` named in `_shared/` | 2 | ✔ |
| doctest9/10 | ADK local | fixed watchdog | 2 | ✔ |
| **inf6** | **Cloud Run** | **everything** | **6** | **✔ waffle, meter, plot, lieFactor** |

`inf6` is the direct answer to `inf5`: **3.8 minutes against 20.8**, `Chart.createWaffle` where the
old run hand-rolled a grid, no overlapping panels where the old one had five and a clipped title
block. It called `budget_status` as its *first* action and again before deciding to continue — at
which point it was **58% through its tokens and 10% through its clock**. The token budget was six
times tighter than the deadline, and it could see that.

**doctest3 is the control that matters**: local ADK, before the fix, reproduced the Apollo failure
exactly. Not a Cloud Run problem, not gcloud, not deployment.

---

## 4. What did not change, and should worry you

**Data provenance.** Three runs of the Apollo brief have produced three different sets of NASA
figures — `inf-4` in kg citing `MSC-01017`, `inf5` in kg citing four vaguer sources, `inf6` in lb.
All from recall, all with a plausible-looking source column. The brief template's rule — *every
figure on the canvas comes from the table* — is followed in form and the table is filled from the
model's memory. **Nothing built this session touches this, and it is the only defect that makes an
infographic wrong rather than merely expensive.** The `--test` overlay does not catch it: agents
audit their arithmetic, which is internally consistent, not their provenance.

**The staged record thinned.** `inf5` wrote seven staged artifacts; `inf6` wrote **one**. Better
picture, worse record — and Milestone 6 streams intermediate renders, so with one artifact there is
nothing to stream until the end. Whether budget pressure caused it or it is just how this agent
worked is **untested**; it would take a run at the same cap with the staging instruction sharpened.

**A per-day cap does not exist.** `--budget` caps a session. A public demo URL is still unbounded
across sessions — `CLAUDE.md` §4.4's open cost surface. At ~$1.25 a run for a dense brief, a hundred
visitors is ~$125 and nothing stops it.

**`Search` is now barely used.** Once `ReadDoc` is named, agents navigate by URI: 0 Search calls in
three consecutive runs. The 85% Search reduction is measured on the tool and **unexercised by any
live run**. It matters for *discovery* — where the agent does not know the vocabulary — which is
precisely the Apollo case, but testing it needs a brief whose subject does not map onto an area name.

---

## 5. The watchdog, and a signal that failed its own test

**Fixed:** `THRASH_CALLS = 8` fired on *every* run including correct ones, telling a single agent to
"hand off" when `advisor_tool` is `None` and there is nobody to hand to. Worse, any trigger set
`armed = False`, and re-arming was only possible via handoff or advisor call — both unreachable — so
one unactionable sentence silenced the stalled-render and elapsed checks **for the rest of the run**.

Now: `SOLO_THRASH_CALLS = 20` when there is no peer, an actionable message, and a **render that moved
re-arms** (movement only — re-arming on any render would let a stuck agent earn its way back by
re-running the same script). Calibrated, not guessed:

```
inf5  BAD   32 working calls     doctest6  good   9
inf6  good  14                   doctest7  good   9
doctest9 good 13                 doctest8  good   8
```

Old threshold fired on all six. New one fires on `inf5` alone. Confirmed live: doctest9 (13 calls)
and doctest10 drew **zero** interventions.

**The write-without-render signal was proposed, measured, and rejected.** It looked compelling in
`inf5`'s trace. Across eight runs it does not separate:

```
max consecutive writes without a render:  inf5 BAD 5 · inf6 good 4 · doctest8 good 3 · doctest3 BAD 1
writes-per-render ratio:                  inf5 BAD 1.67 · inf6 good 1.80 · doctest8 good 1.67
```

Good runs bracket the bad ones on **both** sides, and it misses `doctest3` entirely. Building it
would have reintroduced exactly the false-positive defect just removed. **Do not revive it without
new data.**

---

## 6. Open, in the order I would take it

1. **Amend `a5127bf`'s message.** Two minutes; it currently describes §5's rejected signal.
2. **Data provenance.** The largest remaining correctness gap. Options: a retrieval tool for real
   figures, or a `--test` check that flags a table whose sources are not identifiable documents.
3. **Per-day / per-visitor spend cap**, before the demo URL is public.
4. **`changed: false` on the script store.** `inf5` made two byte-identical 32 KB rewrites
   (verified with `cmp` on versions pulled from the artifact store) — ~26,000 output tokens
   producing nothing, invisible to every record we keep. The store holds both texts at write time.
5. **Thinking-loop trigger.** `think=` is logged but no trigger consumes it; `_after_model` runs on
   every model call, so a counter of consecutive model calls with no tool call is the hook.
6. **Cloud Logging alerts** on `% of token cap` at 90 and on `BREAKER`. Console-only, no deploy.
   Alert on the 90% rung, not just `BREAKER` — `inf6` crossed its cap on its **final** turn and the
   breaker never fired, which is the one-turn lag working as designed.
7. **A comment/trace requirement in `_shared/`.** `CLAUDE.md` §6 requires inline comments; no
   template carries it, so an agent that stripped its own broke no rule it had been given.
8. **Missing schema areas** for `Chart`, `Scale`, `Layout`, `Css` — `polson://sdk/schema/Chart` does
   not exist.
9. **`WithFileAndConsoleLogging` is a trap.** It uses Serilog's **stdout** console sink, and on the
   stdio transport stdout is the JSON-RPC channel. Needs `standardErrorFromLevel` before anyone
   reaches for it to get engine logs into Cloud Logging.
10. **`run_studio.py` crashes after a successful run** — `orchestrator/__main__.py:35` writes the
    reply to a cp1252 console and dies on `→`. The work is already done; it just looks like a failure.

---

## 7. Things that will mislead you

- **Do not compare dollars between runs.** Cache rate swings cost fourfold on identical work and is
  not something we control — the run with the *fewest* tokens cost the *most*. Compare token counts.
  I made this mistake repeatedly; see the `token-carry-cost` memory.
- **Grep is not a measurement.** Two of this session's wrong findings came from grepping a log for a
  string (counting citations as reads) and grepping escaped JSON (counting 59 signatures as zero).
  Parse the payload.
- **Check which artifact you are reading.** "AGY has no resource support" came from reading the SDK
  package when the runs were on the Desktop harness.
- **`ADK_MAX_LLM_CALLS=500` has never stopped anything.** At ~47K input per call it is ~23M tokens,
  which is roughly the most expensive run on record. It is a backstop set above the failure.
- **Manual examples are executed.** ` ```javascript ` fences in a manual are run and must render;
  ` ```js ` is the illustrative form. And `ManualCoverageTests` enforces that every JS surface member
  is named in some manual at 100%. Both caught me adding `Stage.elapsedMinutes`.

---

## 8. Where the evidence lives

- `projects/inf6/` — the good Apollo run, artifacts recovered from the container.
- `projects/doctest3`–`doctest10/` — the local ADK ladder. **Scratch; delete when done**, along with
  `src/adk_agent/apps/doctest*`.
- `src/adk_agent/tests/` — 33 tests, new this session (`test_token_budget.py`, `test_watchdog.py`).
  The budget machinery and the watchdog had **none** before.
- Cloud Run: revision `polson-studio-00025-z8w`, `POLSON_BUDGET_TOKENS=2000000`.
