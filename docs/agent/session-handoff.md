# Session handoff — 2026-08-28

For the agent picking this up next. Written at the end of a session that took the project from
"the record is a design document" to "a Gemini agent drives the studio end to end and leaves a
legible trace."

Read `docs/project-layout.md` alongside this — it is the contract, and it is current.

---

## Where things stand

**The Python → .NET loop is proven.** `src/webapp/hello_agent.py` runs a real Gemini agent that
attaches the Polson MCP server, calls `ExecuteScript`, and leaves a stage-tagged render in
`events/server.jsonl`. All five things it set out to prove were confirmed on 2026-08-28. Keep that
script working; it is the cheapest check that the two halves still meet.

**634 tests pass** across four projects (`Drawing`, `MCPServer`, `ExtendedMind`, `CLI`).

### Built this session

| Area | State |
| :--- | :--- |
| `polson create-project` | Generates the whole project directory. `logo` workflow, `standalone`/`managed` profiles |
| Path containment | `outFile`/`outSvg` confined to the project; escapes refused with an actionable message |
| Run record | `events/server.jsonl` — run bracket, script lifecycle, renders, stages, notes, tool errors |
| `Stage` JS global | `begin` / `end` / `current` / `note`, persisting across executions |
| Manual 10 | Shape stance, irradiation, balancing pass, symmetry, gridding |
| Manual 12 | Stage 2.5 (concept + mood board), review protocol, 9-archetype taxonomy |
| Irradiation compensation | `Logo.computeIrradiationCompensation`, applied by the monochrome board |
| Python environment | 49 packages installed, hash-locked; SDK confirmed working |

### Not built

- **The orchestrator.** `hello_agent.py` is a spike, not architecture. Milestone 5 has no real code.
- `events/agent.jsonl` — the agent's turn/thinking stream. Only its deliberate `Stage.note`s are
  recorded, not its live reasoning.
- HITL (`WebAskQuestionHook`), `events/director.jsonl`, spend and concurrency caps.
- The web app itself. Nothing in `src/webapp/` but the spike and the dependency manifest.

---

## Traps that cost real time. Do not rediscover these.

**The SDK policy engine is fail-closed, and passing `policies=[...]` replaces the permissive
default.** A list of `deny` rules denies *everything else too* — `ExecuteScript`, `Search`,
`view_file`, all of it. The agent loops retrying and looks like a hang. Always include
`policy.allow_all()`; a specific deny is priority 1 and a global allow is priority 9, so the denies
still win.

**The API key in `bin/cli/appsettings.json` is an Agent Platform credential, not a public Gemini
one.** Against `generativelanguage.googleapis.com` it returns `403 ... are blocked`. Use
`vertex=True` (or `GOOGLE_GENAI_USE_ENTERPRISE=true`), which is the same choice the .NET side makes
with `new Client(enterprise: true, ...)`. Public keys start `AIza`; this one does not.

**Antigravity reads `mcp_config.json`; Claude Code reads `.mcp.json`.** Editing the wrong one
changes nothing and fails silently — a missing `--project-dir` means the whole run goes unrecorded.
Write both when a directory may be opened by either.

**Antigravity permissions name tools, never paths.** `mcp:polson:*` and `bash:node*` work;
`read:C:/...` does nothing. A harness relying on path denials to hide the source fails open. Working
example: `tests/multi_agent/logo_studio/gemini/.agents/settings.json`.

**`chat()` + `await response.text()` hides everything until the turn ends**, so a stall is
indistinguishable from slowness. Use `conversation.send()` + `receive_steps()` with a timeout.
`receive_steps()` re-yields a step as it accumulates — dedupe before printing.

**A JS-reachable property whose runtime type alternates crashes Jint.** `IReadOnlyList<string>`
holding sometimes `string[]` and sometimes `List<string>` threw `Unable to cast … to 'System.Array'`
on the *third* call. Declare such properties as arrays so alternation is unrepresentable.

---

## What the first real agent run told us

A Gemini logo run (`tests/agent/test2/gemini`) produced 92 events, 19 scripts, and a genuine brand
sheet. Its findings drove most of the fixes above. Two things it surfaced are still open:

1. **Stage vocabulary held up.** The agent used all nine names in order and reopened stages —
   exactly the behaviour that makes iteration visible. It reported the granularity "matched the
   real design process cleanly."
2. **It re-declared the same stage at the top of consecutive scripts.** Now handled: re-declaring
   the current stage records `stage.continue` instead of an end/begin cycle.

**`MeasureSvgPath` reportedly failed once and is not reproducible** — pinned by a protocol test, and
`tool.error` will now record it if it recurs.

---

## Suggested next step

Build the orchestrator properly, not the web app. The web app is a UI over something that does not
exist yet; the orchestrator is the thing every remaining milestone sits on, and the spike shows the
shape it should take.

Carry the two traps above into it as fixed constants — `vertex=True` and `allow_all()` — rather than
as things to be rediscovered. The immediate unknown worth retiring is **cross-execution stage
persistence through the SDK**: it passes in unit tests but has never been seen end to end, because
the spike only ever made one `ExecuteScript` call per run.

---

## Uncommitted at handoff

`src/webapp/` only — `hello_agent.py`, `requirements.txt`, and edits to `README.md` and
`requirements.in`. Everything else is committed.
