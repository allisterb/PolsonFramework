# Session Handoff — 2026-08-27 (late)

State after the session that built the ExtendedMind asset-requisition layer, replaced markdown-parsing
with a generated symbol index, and OCR'd the drawing reference.

---

## Where things stand

**Tests: 349 of 350 passing.** `Polson.Tests.MCPServer` went 86 → 126; `Polson.Tests.ExtendedMind` is
new at 63; `Polson.Tests.Drawing` is 165/166. The single failure is pre-existing —
`TestEncodePerformanceBenchmark` loads a fixture from the deleted `tests/agent/mcp_server/`.

Three threads ran through the session, and each turned out to be the same problem wearing a different
hat: **an agent cannot tell a confident wrong answer from a right one.**

---

## 1. Asset requisition (`Polson.ExtendedMind`)

Agents can now requisition raw material from a cloud image model — flat tiling textures, background
plates, greyscale mattes. There is deliberately **no call that returns a finished picture**.

The constraint is structural, not advisory. `Assets.material('a wooden ship')` is refused before any
network call, because the descriptor names an object rather than a substance; `'weathered ship hull
planking'` is allowed. A policy in a manual would not survive goal pressure — a type signature does.

What was measured rather than assumed, all verified live:

| Finding | Consequence in the design |
| :--- | :--- |
| Output is **always 1024²** — `ImageSize` offers only 1K/2K/4K and the prompt cannot move it | `size` is a local resample of a cached master, so it costs nothing; caps defend context, not spend |
| An image costs **~1,290 output tokens** regardless of model or size | Budget is a generation count (knowable in advance); `TokensSpent` records what it actually cost |
| A **conditioned** backdrop costs roughly double — the blocking goes up as prompt tokens | Conditioning is the most expensive *and* riskiest requisition |
| "Seamless" is **not honoured** and fails silently | Tiling is verified on receipt and repaired here, never requested |
| Conditioning bakes the blocking silhouette into the plate as black | The plate is welded to that blocking; `BoundTo` records it |
| `IMAGE_RECITATION` — a too-generic descriptor is **refused outright** | Its own failure mode; retrying the same words recites again |

Every call returns a result carrying a classified failure and a `remedy` phrased for the agent that
reads it. Nothing throws. A network fault never charges the budget.

**Async:** requisition is network I/O, so the engine uses Jint's `ExperimentalFeature.TaskInterop` —
CLR `Task<T>` becomes an awaitable promise — following the pattern in `reference/projects/Camel.Server`.
Scripts containing `await` are wrapped in an async IIFE; scripts without it execute unwrapped, because
inside a function body a bare trailing `paper;` is no longer a completion value.

**Configuration:** `ConfigureAssetRequisition` in `Polson.CLI/Program.cs` reads
`ApiKeys:GoogleAgentPlatform`, `Assets:Model`, `Assets:Budget` (default **12** per server run) and
`Assets:CacheDir`. No key is a normal configuration: the global is still registered and returns a
readable `NotConfigured` refusal rather than a `ReferenceError`.

---

## 2. The symbol index — the API docs stop being parsed

The reference could not be read reliably enough to answer *"does this call exist?"*. Five failure
classes were measured: one whole area (`VectorLogo`) invisible to search because of a heading-text
mismatch; three chunks too large to split; 14 bullets carrying two symbols on one line; 22 `ctx.*`
shortcuts documented only in a middle-dot prose run; and `gradient` naming four CLR types with
**reversed argument order** between two of them.

`JsSymbols.cs` now generates a **434-symbol manifest by reflection** over the types the engine
actually registers, served at `polson://sdk/symbols` and `polson://sdk/symbols/{Receiver}`.

`Search` routes symbol-shaped queries there first. The difference that matters:

| Query | Before | Now |
| :--- | :--- | :--- |
| `paper.squircle` | prose hit for the **raster** `Logo` toolkit | `direct` — exact signature |
| `paper.squirkle` | a confident wrong hit | `no-match`, zero results, `nearest: paper.squircle` |
| *"how do I make a cast shadow fall off"* | prose | `related` — prose, correctly |

A `no-match` on a call name is now **definitive**, which similarity search can never be.

`ApiDocumentationTests` already existed and is better than what I would have written; its receiver map
and exclusion list moved into `JsSurface` so the drift test and the manifest share one source. That
widened coverage from 14 to 22 receivers and immediately surfaced four previously invisible gaps. The
tests then caught the undeclared `Assets` global the moment it was registered.

---

## 3. The reference material

**`Imaginative Drawing` is fully OCR'd — 647 pages, 124,091 words.** The PDFs are image-only scans
with zero extractable text; `reference/ocr-book.sh` renders at 300dpi, OCRs to TSV, and
`reference/ocr-reflow.pl` rebuilds reading order from block coordinates so sidebar captions are not
spliced into the body paragraph beside them.

**The manuals are not sourced the way they claim.** Manuals 05–09 cite specific pages; three of four
checked citations do not support their claim, and the 8-head canon that organises Manual 08 is not in
the book at all. Cause: Gemini was given a 53-page sample of a 647-page book and cited only pages it
had seen. Full detail in `docs/memory-design.md` §4.

**Janson** (`reference/books/janson-pencilling/`, 20 files, 171 figures) is extracted and verified but
is **copyright — private testing only**, excluded from any public corpus.

---

## Next session: the audit and coverage diff

The task, in order:

1. **Section map** from the OCR — every `N.M Section Title` across 647 pages. The TOC (pages 4–6) is
   already readable and gives the skeleton.
2. **Coverage diff** — book section → existing toolkit method → manual. Classify each of the 39
   documented `Drawing.*` methods: grounded in the book, invented but useful, or missing.
3. **Re-derive the manuals** from real text with citations that are mechanically checkable now that
   every page is a file. Add a test asserting every `Page N` citation names a page that exists.

**Do not rewrite `ConstructiveDrawingToolkit` blind.** No bad geometry was found — the defect is
provenance and coverage. The mannequin case is the argument: §4.6 *Gesture Line / Box Forms /
Mannequin Form* (pp. 553–556) turns out to be real source for `drawMannequinSolid`, so that method
needs a correct citation, not a reimplementation.

Also open: `ImageGenerator.ProbeModels` is unimplemented (each probe that reaches a live model bills);
the OCR text needs a codepoint scan and a `reference/README.md` ledger entry before it is read into
agent context; and `docs/memory-design.md` still proposes `Citation` and `KnowledgeAnswer` types that
are designed but not built.

---

## Running the harness

`tests/agent/test1/claude` is scaffolded for a moonlit wooden sailing ship, with the requisition rules
stated up front and a `findings.md` brief that asks specifically for *what misled you* and *what you
concluded did not exist*.

A live MCP server holds `bin/cli/*.dll` open, so `dotnet build` fails with MSB3021 while a session is
up. **Build first, then start the agent.**
