# Session handoff — 2026-09-06

**One sentence:** the studio gained a sourced-data pipeline end to end — a hand-written Parallel
client, a `Research` tool that commissions and waits outside the sandbox, a read-only `Research`
global, a per-session budget and a schema pre-flight — and **two live infographic runs proved it**,
each finding defects the previous run's fixes could not have caught.

Committed through `4b1e09e`. **Six files are outstanding in the working tree** — see §7.

---

## 1. What exists now that did not

| | |
| :--- | :--- |
| `src/Polson.ExtendedMind/ParallelSearch/ParallelClient.cs` | Search, Extract and Task against Parallel. Hand-written; failures are values, never exceptions. |
| `…/ParallelTaskModels.cs`, `…/TaskSchema.cs` | Task spec, processors, per-field basis, and the schema dry run. |
| `…/Research.cs` | `ResearchTask`, `ResearchRegistry`, `ResearchBudget`, the read-only `ResearchToolkit`, `JsonInterop`. |
| `src/Polson.Runtime/TextScan.cs` | Codepoint scanning and sanitising. One implementation for the brief, the ledger and research. |
| `Research` MCP tool | Commissions and waits **outside** the JS sandbox. Start-and-wait, start-only, or resume by `runId`. |
| `ScanText` MCP tool | For text the automatic path does not cover. |
| `Scale.checkSeries(...)` | Whether a run of positions can honestly be joined by a line. |
| Prompt amendments | Stage 1 owns goal *and* data; `SUCCESS`; `INFERRED`; measured audits; computed captions. |

**The NSwag binding was deleted.** It could not express `objective` and threw on any response
carrying a real `title` — 20 optional fields had become classes with no value in them. Its
`Defect_*` tests went with it; the story is in `be4fe04`.

---

## 2. Measured facts worth not re-deriving

- **The Task API returns no usage data at all.** Search and Extract both report a `usage` array; a
  task envelope carries `run` and `output` and nothing else — verified against the raw JSON. So
  research spend can only be *inferred* from run count × the published price. The budget count is
  the entire control.
- **Pricing, per 1,000:** search advanced $5 · extract $1 · task lite $5 · **base $10** · core $25 ·
  pro $100 · ultra $300 · Responses low $10 / medium $50 / **high $250**.
  **Responses `high` costs 25× Task `base` and gives strictly less provenance** — URL and title, no
  reasoning, confidence or excerpts. That is why the Task API was built and Responses was not.
- **Latency is job-shaped, not tier-shaped.** Same `base` processor: 13 s for two fields, 96 s for a
  six-mission table, 248 s for six top-level fields. Published p50 is 50 s, p90 2 min.
- **ADK's MCP client times out at 60 s.** This governs `waitSeconds`, not the research duration.
- **Parallel has two error envelopes.** The application returns `{type, error:{ref_id, message}}`;
  the gateway answers a bad key with gRPC-shaped `{code, message}`. Both are parsed.
- **Provenance is asymmetric:** 5 of 5 results carried a title, 1 of 5 a publish date. Titles are
  occasionally a URL rather than a headline.

---

## 3. What the two live runs found

`computerprogress` (thin brief, 9 min) and `apollolm` (specified brief, 16 min). Both produced real
infographics; the artifacts are in their project directories.

| Found | Where it was fixed |
| :--- | :--- |
| `waitSeconds` default 150 > the 60 s host timeout — the reply was discarded **with the run id inside it**, so the agent started a *second* run and one question ate the whole allowance | `DefaultWaitSeconds = 45`, plus tool guidance: a transport timeout is not a failed run, find it in `Research.tasks` and resume |
| Two chips at the same year joined by a line — the trajectory spiked to 208 B and fell to 28 B inside one tick | `Scale.checkSeries`, Manual 13 §2 |
| The audit asserted *"monotonic scaling preserved"* over that zigzag, with **zero `observe` events** | Stage 7: every `check` carries a measurement from a call; a claim you cannot measure is a note |
| `LIE FACTOR: 1.000 (TRUE)` hardcoded in a title block | Non-negotiable #1: a figure *about* the artifact is still a figure on it |
| A check failed four times and shipped anyway — and **the check was wrong, not the graphic** (`drawnPropWidth` held a stage width) | Stage 7 + Manual 18 §3a: settle every failing check; read it before dismissing it |

**The second run's audit measured.** Four `observe` events, details carrying sampled pixels and
computed factors, `Scale.checkSeries` called, and one **failing** check recorded rather than passed.
That is the instruction change working.

---

## 4. Corrections — things claimed in this session that were wrong

| Claimed | Actually |
| :--- | :--- |
| The Perl codepoint scanner was sound and I was porting it faithfully | Its `next if $c < 0x80` made its own `C0/C1 control` class **unreachable** — a file carrying `U+0001` scanned clean. Found by a test on the C# port; fixed in both. |
| `studio.py`: *"gemini-2.5-flash OK 17.6s ← seven times slower"* | It is the **fastest** of the four (1.0 s median of three). One unrepeated probe became a documented property and nearly decided a model choice. |
| I bet the agent would not write `INFERRED` lines from a one-sentence brief | It filled every row of the framing table with a reason. Wrong, and pleasingly so. |
| `waitSeconds` 150 "covers the base processor's observed p90" | Right number, wrong axis. The host timeout governs, not the research. |

---

## 5. Design decisions with reasons, so they are not silently undone

- **The processor is not a tool parameter.** The tiers span 30× on price; that lever lives in
  `Research:Processor`, like the image model.
- **Two research runs, and they are not equal.** The first carries the whole requirement; the second
  is for *correcting* it. A failed run is refunded; `attempts` (capped at `total + 2`) stops a loop.
  One was the first choice — two is deliberate, so an agent that can see its answer is unusable is
  not denied the review step.
- **Over-capacity schemas warn, they do not refuse.** With the processor fixed, the caller cannot
  answer by moving up a tier, and complexity matters more than count. Structural faults still refuse.
- **Research is scanned and sanitised on the way in**, not by a tool the agent calls — by then the
  text is in its context and reading it *is* the injection. Findings land in `task.warnings`.
- **A script can read research and can never write it.** The mutators live on the registry, which the
  toolkit does not expose, enforced by a reflection test.
- **Capacity refusals name the *form* fix, never "drop a point".** Both chips are real.

---

## 6. The open task

**`AssetEventTests.TestTheBudgetSnapshotComesFromTheToolkitThatRan` is intermittent.** Failed one
full-suite run, passed the next, passes in isolation (9/9). It sets the **static**
`JsDrawingEngine.Assets` and asserts a budget of 7; the research tests added this session run more
scripts concurrently, which makes that pre-existing static-sharing hazard fire more often.

Not caused here, but made visible here. Cheapest fix is a shared xUnit collection so it cannot run
beside anything executing scripts — the same remedy already applied to `ResearchCollection`.

---

## 7. State

**Uncommitted (six files)** — the last two fixes, both post-`4b1e09e`:

- `src/Polson.MCPServer/DrawingMcpTools.cs` — `description` made optional with a derived label;
  `DefaultWaitSeconds`; the settle-your-failures and computed-caption guidance.
- `tests/Polson.Tests.MCPServer/ResearchToolLiveTests.cs` — no-required-arguments and derived-label
  tests.
- `src/Polson.CLI/ProjectTemplate/infographic/instructions.md`, `docs/manuals/18_the_run_record.md` —
  the audit and failing-check rules.
- `src/adk_agent/studio.py`, `src/adk_agent/.env.example` — corrected latency table.

**`reference/` is gitignored entirely.** The Perl scanner fix and the two new ledger rows for
`articles/` are **local only** and will not survive a fresh clone. Worth deciding whether a file
`CLAUDE.md` treats as load-bearing should be tracked.

**Tests:** 1,885 across four projects. Two known flakes, both static-sharing under parallel runs:
`IrradiationCompensationTests` (pre-existing) and the one in §6.

**`bin/cli` is current.** Rebuild it after touching the MCP server or the templates, or a run uses
the old prompt and the old tools — this bit twice today.

**Model:** back on `gemini-3.7-flash`. A `gemini-2.5-flash` trial was abandoned: it omitted a
required argument, retried the identical mistake twice, and reported the tool broken. Spend is
~$6/day, which buys the capability gap cheaply.

---

## 8. Where to pick up

1. **§6**, which is small.
2. **A third live run** would likely show diminishing returns — the last two defects were craft
   variance, not systematic gaps. Better spent on the agent-facing layer for Search/Extract, which
   still has no budget, cache or content boundary.
3. **The Responses API remains unbuilt, deliberately.** §2 has the numbers; revisit only if a
   latency-bound interactive path appears.
