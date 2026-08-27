# Session Handoff — 2026-08-27

State of the integration work after the session that connected the studio manuals to the drawing SDK and ran the first agent harness against it.

---

## Where things stand

**Tests: 252 passing** (166 `Polson.Tests.Drawing`, 86 `Polson.Tests.MCPServer`), up from 171 at the start of the session. Build clean.

The original complaint — *"it isn't fully integrating the art and graphic design reference material with the drawing APIs"* — turned out to be literally true in a way that wasn't visible from the outside: the manuals were never served to agents at all. The MSBuild glob embedded `docs/*.md` but not `docs/manuals/*.md`, so no agent had ever seen one.

That is fixed, and the fix grew into three layers:

1. **Manuals are served** — `polson://manual/{NN}` plus a catalogue at `polson://manual/index` that cross-checks every cited SDK call against the reference and flags ones that don't exist.
2. **`Search` exists** — ranked retrieval over manuals *and* SDK docs, behind `IKnowledgeIndex` so a Vertex backend can replace the local BM25 without changing the tool signature or any agent prompt.
3. **All 12 manuals bind to real calls** — 56 per-section `Implemented by` bindings and 17 runnable examples, every one executed in CI.

---

## The through-line worth remembering

Nearly every serious defect this session was a **silent failure** — something that returned `success: true` and drew the wrong thing, or nothing.

- Every `options` object passed from JavaScript was discarded (`ExpandoObject` implements `IDictionary<string,object>`, not the non-generic `IDictionary` the toolkits cast to). Colours, angles, tilts, grid types: all inert, no error. 66 call sites.
- `{x, y}` point literals became `(0, 0)`, so `drawTaperedStroke` and `drawHairRibbon` drew nothing at all.
- `ctx.fill('evenodd')` accepted the fill rule and ignored it.
- `arc()` silently dropped full circles appended to an existing path.
- `attr({fill: 'url(#id)'})` degraded a paint-server reference into a colour literal and rendered black.
- `el('linearGradient')` returned a `<g>`.
- `projectCastShadow` returned a zero-area polygon that filled to nothing.

Each cost a full render-and-look cycle to notice. The remedy applied throughout: **fail loudly**. `el()` now throws on an unknown tag, `drawCastShadow` throws on a degenerate polygon, and the perspective shadow refuses configurations that would land at infinity.

When adding to this codebase, prefer a thrown exception over a silent default. An agent cannot debug what doesn't complain.

---

## Verification habits that paid off

- **Assert on pixels, not on call success.** Byte-length comparison misled me twice; sampling an actual pixel did not.
- **Execute the documentation.** `ManualExampleTests` runs all 17 manual examples through the real engine. A manual that teaches code an agent can't run is worse than one that stays silent.
- **Check the docs against reflection, not against other docs.** `ApiDocumentationTests` compares `core.md` to the real .NET surface in both directions. The moment it existed it found 7 documented-but-missing calls and ~50 implemented-but-undocumented members.

---

## Open items

Detail and rationale in the memory file `polson-open-work`. In rough priority:

1. **`Search` cannot express a negative.** Asking for an unsupported capability returns adjacent-but-wrong results with no "not in this engine" signal. This is what convinced a harness agent that vector gradients didn't exist. Highest value.
2. **Manuals are Canvas2D-first.** Every runnable example in Manuals 02, 03, 05, 08, 09 opens `createCanvas` + `getContext('2d')`. The theory transfers to vector work; the code doesn't.
3. **`createMannequinFigure` cannot pose a limb** — only contrapposto modifiers on a standing figure, so action poses are unreachable. `verifyPlumbAlignment` is undefined for an airborne figure, and Manual 08 has no stylised-proportion guidance (~5H with an oversized head is the common flat-vector mascot canon).
4. **`ExecuteScript` has no `scriptFile`** — every iteration re-sends the whole program.
5. **Gemini harness config unverified** — `.agents/settings.json` follows the repo convention, but Antigravity's permission syntax was never proven to bite. Needs a throwaway run before any Claude-vs-Gemini comparison is meaningful.
6. **`tests/agent/mcp_server/` deletions are uncommitted** — a perf benchmark still loads a fixture from there, restored from git rather than repointed. Decide whether that harness is gone.

Further out: `projectCastShadow` currently takes either a ground line (`groundDepth` scalar) or a `PerspectiveGrid`; the grid path is the principled one and could become the default once more scenes use it.

---

## Running the harness

```bash
dotnet build src/Polson.CLI/Polson.CLI.csproj
cd tests/agent/comics/claude && claude
```

Two things that will otherwise cost you a run:

- **Close the agent session before rebuilding.** A running MCP server locks `bin/cli/*.dll` and the build fails with MSB3021.
- **Read the self-test line first.** The prompt opens by requiring a denied read of the toolkit source and a successful read of the reference image. If the source is readable the run is invalid — the agent has seen the implementation and can no longer tell you whether the docs suffice. That happened once and the session was discarded.

Permission-rule syntax is a trap in both directions; the `README.md` in each harness has the table, and it's in the memory file `harness-permission-traps`.
