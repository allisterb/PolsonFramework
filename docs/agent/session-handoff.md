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

A Gemini logo run (now `tests/agent/archive/test2/gemini`) produced 92 events, 19 scripts, and a genuine brand
sheet. Its findings drove most of the fixes above. Two things it surfaced are still open:

1. **Stage vocabulary held up.** The agent used all nine names in order and reopened stages —
   exactly the behaviour that makes iteration visible. It reported the granularity "matched the
   real design process cleanly."
2. **It re-declared the same stage at the top of consecutive scripts.** Now handled: re-declaring
   the current stage records `stage.continue` instead of an end/begin cycle.

**`MeasureSvgPath` reportedly failed once and is not reproducible** — pinned by a protocol test, and
`tool.error` will now record it if it recurs.

---

## Settled since — cross-execution stage persistence (2026-08-28, later)

**Retired. It works, through the real host, with nothing to fix.** A three-call live run
(`hello_agent.py --task stages`) declared `Blocking` in script 1, read it back in script 2 without
redeclaring, restated it in script 3, and produced three renders all filed under it. Every
`script.start` carried the same session, and the restatement recorded `stage.continue` rather than
another `stage.begin`.

The server half is now pinned by `StdioTransportTests` (5 tests) — the first tests to exercise the
shipped `bin/cli` build over stdio, which is the transport an agent host actually uses and the one
transport nothing covered. The HTTP protocol tests could not stand in for it: an HTTP connection
carries a session id and stdio has none, so every stdio call lands on `"default"`, and that is
exactly what stage persistence depends on.

`hello_agent.py` now judges continuity from the record on every run, not just this task, so a host
that started reconnecting per tool call would be caught rather than silently losing every stage.

**One real defect surfaced on the way, and it is a host problem, not ours.** `run.end` is written
from `ApplicationStopping`, reached when the server's stdin ends. The .NET reference MCP client never
closes stdin: it waits its five-second shutdown timeout and then kills, so the closing bracket is
lost and the record is indistinguishable from a crashed run. The Antigravity SDK does close it
properly, so real runs are fine — but the guarantee belongs to the host, not to us.
`docs/project-layout.md` now says so, and the orchestrator inherits two obligations from it: close
stdin and wait for exit rather than killing, and write its own closing event in `agent.jsonl`
regardless, since it is the only party that always observes the exit.

## Milestone 5 — the orchestrator exists (2026-08-28, later)

`src/webapp/orchestrator/` is real code, not a spike, and a live run took a brief to a rendered
candidate with the whole thing written down. Run it with:

```bash
python src/webapp/run_studio.py <project>
```

| Module | What it owns |
| :--- | :--- |
| `events.py` | The append-only JSONL writer. Byte-identical line shape to `RunEventLog`, so one grep spans all three files |
| `project.py` | Reads a generated project — manifest, policy, MCP wiring — or explains why it cannot run |
| `transcript.py` | `receive_steps()` → `events/agent.jsonl` |
| `director.py` | The `OnInteractionHook`, terminal-backed, writing `events/director.jsonl` |
| `run.py` | The agent lifecycle, the policy, the run bracket, the conversation id for resume |
| `__main__.py` | The CLI |

26 tests, standard-library `unittest` (nothing may install a package):
`python -m unittest discover -s src/webapp -t src/webapp`.

**The policy lives in the project, not in Python.** `build_config` reads the denied tools, the
behaviour and the session directories out of `agent.config.json` and the wiring out of `.mcp.json`.
A policy with an opinion of its own in Python would be a second source of truth for the one thing
that must not drift.

### Three things a live run taught, all now fixed

1. **The SDK deep-copies its config at startup**, so anything reachable from a hook must be
   copy-safe. A `threading.Lock` inside the event log killed the first run with
   `cannot pickle '_thread.lock'` — reported at agent startup, nowhere near the log. `EventLog`
   now returns itself from `__deepcopy__`, which is also the correct semantics: a copy of a log
   would be a second writer with its own sequence counter on one file.
2. **Steps interleave, and only `status` says a step is finished.** A thinking step and the tool
   call after it alternate, each re-yielded several times. Flushing on "a different step arrived"
   recorded **17 `tool.call` events for the 6 scripts that actually ran**. Transcribe on
   `DONE`/`ERROR`; write anything still unfinished at turn end, marked `partial`. Verified the
   second time by comparing the transcript against `server.jsonl`: 3 `ExecuteScript` events, 3
   `script.start`, 3 scripts on disk, 16 distinct call ids out of 16.
3. **A failed step is not the end of a turn.** The run hit a `429 Resource exhausted`; the SDK
   retried and carried on. Recording that as a `turn.end` would have misreported a completed run as
   a broken one.

Also fixed on the way: `create-project` launched as `dotnet Polson.CLI.dll` wrote a `.mcp.json`
saying `dotnet server --project-dir .` — the assembly argument was dropped, so the wiring could not
start anything. `Environment.ProcessPath` is the apphost only when launched as `Polson.CLI.exe`.

### Still not built

- The web app. `run_turn` is what it will wrap; the director is already a swappable hook.
- `asset.requisition` / `budget` events — `Polson.ExtendedMind` is not wired to a run yet.
- Spend and concurrency caps (§6.4). Nothing bounds a run today.
- `usage` events are implemented but never fired in these runs — the model reported no
  `usage_metadata`. Worth confirming against a longer run before relying on it for cost.
- An SVG `render` event carries no `bytes`, unlike the image one. Harmless, but a reader tallying
  output size undercounts.

## create-project, re-specced (2026-08-29)

The signature changed, and so did the thing it branches on:

```bash
polson create-project <directory> <id> <sdk> [--standalone] [--workflow logo] [--brief …]
polson create-project projects acme agy --standalone
```

`<directory>` is now the **parent** — one directory holds many projects, and the id names the one
being made. `<sdk>` is `agy` or `claude`, and it is required: it decides what the host's three files
are *called*, which is the only real difference between an Antigravity project and a Claude Code one.
`--profile` is gone; `--standalone` replaces it, and managed is now the default.

**Standalone is a strict superset of managed** — it adds `agent.config.json` and `session/` and takes
nothing away. That was the point of the change. The previous version emitted *identical* files for
both profiles and differed only in an `enforced` field inside `agent.config.json` that nothing read,
which contradicted this document and made the profile invisible to anyone looking at the directory.
Now the file set is the signal, and a test asserts the superset property directly.

Two consequences worth knowing:

- **A managed project cannot be run by the orchestrator.** `project.load` refuses it, because a
  managed project deliberately has no `agent.config.json` and running it would mean an empty deny
  list — silently permitting `generate_image`, which is the one control the studio's premise rests
  on. The message says to regenerate with `--standalone`.
- **`claude --standalone` is refused at generation**, reported like every other bad combination
  rather than thrown, since it is a plausible thing to type.

`project.json` now records `sdk`, and the orchestrator reads it to decide which wiring file to open
rather than probing filenames. The instruction template is `ProjectTemplate/logo/instructions.md` —
renamed from `GEMINI.md`, because it now renders as either `GEMINI.md` or `CLAUDE.md`, and
`brief.md` refers to it through a `{{INSTRUCTIONS_FILE}}` token.

The Claude permission file's allowlist is **derived by reflecting over `DrawingMcpTools`**, so it
cannot fall behind a tool being added — a stale allowlist would tell an agent it lacks a capability
it actually has.

### Two workflows now, and the second proved the mechanism

`--workflow harness` joins `logo`. It renders the SDK-evaluation brief the old `tests/agent/test2`
carried by hand: the no-peeking rule, the `outFile` containment check, and the `findings.md` report
that is the whole point of a harness run. `tests/agent/test2/{gemini,claude}` are now generated from
it — the originals are archived at `tests/agent/archive/test2/`, where the first real run's
`events/server.jsonl` (92 events) and 19 scripts remain.

Adding it surfaced something a single-workflow generator could hide: **the two hosts need different
prose, not just different filenames.** The harness's isolation rule is enforced differently — Claude
Code's permission rules take paths, Antigravity's name tools and commands only — so an instruction
file that claims enforcement on Antigravity would be false, and a harness that lies about its own
boundaries measures nothing. That paragraph comes from an `{{ISOLATION}}` token the generator fills
per SDK, and a test asserts each host gets the true version.

Note what the generated Claude permissions do **not** do: they deny the shell and the network, but
not reads outside the project, because what would need denying depends on where the project was
generated. The instructions say so rather than implying a wall that is not there.

### Deliberately not emitted: a `generate_image` deny for Antigravity

`.agents/settings.json` gets `allow: ["mcp:polson:*"]` and the `bash:*` denies, which a working
harness proves. It does **not** get a deny for `generate_image`, because whether the desktop host can
deny a builtin — and under what key — is unverified, and an entry the host does not recognise is
silently inert, which is indistinguishable from one being enforced. That is exactly how the
`read:C:/...` path entries failed last time. In a managed project the prohibition is carried in the
instructions as a rule; the CLI says so on generation. **Allister is verifying the real key against a
running Antigravity Desktop** — adding it is one line once confirmed.

Antigravity also gets the wiring written twice, at the root and in `.agents/`, because the working
harness carries both and which one the host reads is likewise unverified. Same source object, so
they cannot drift. Drop one when confirmed.

## Suggested next step

**The web app (Milestone 6), and it is now a wrapper rather than a foundation.** `run_turn` is the
thing to call; the browser replaces `ConsoleDirector` and nothing else, because the director is
already a hook and the record is already files. Serve `artifacts/` by URL and stream `events/` —
both were designed for exactly that, which is why nothing embeds image bytes.

Two things belong with it rather than after it, because a public URL is what makes them matter:

- **Spend and concurrency caps** (§6.4). Nothing bounds a run today. `types.BudgetConfig` on
  `LocalAgentConfig` takes `max_model_calls` / `max_tool_calls` and is the cheapest half.
- **The trust boundary end to end** (§6.3). `brief.md` is already quoted and the generator already
  refuses to let a brief reach a path or a policy — but no visitor has ever typed into it.

Before either, one cheap thing worth doing: run a **full** logo project through the orchestrator
rather than the two-stage runs used so far. Everything after Stage 3 — the stress tests, the
presentation board — has been exercised by an agent in an IDE but never by this code path.

---

## Uncommitted at handoff

Everything from the 2026-08-28 session was committed as `f5c0b4c`. Uncommitted now is the
stage-persistence work and Milestone 5:

- new: `src/webapp/orchestrator/` (6 modules), `src/webapp/run_studio.py`, `src/webapp/tests/`,
  `tests/Polson.Tests.MCPServer/StdioTransportTests.cs`
- renamed: `src/Polson.CLI/ProjectTemplate/logo/GEMINI.md` → `instructions.md`
- edited: `src/webapp/hello_agent.py`, `src/webapp/README.md`, `src/Polson.CLI/ProjectGenerator.cs`,
  `src/Polson.CLI/Options.cs`, `src/Polson.CLI/ProjectTemplate/logo/brief.md`,
  `tests/Polson.Tests.CLI/ProjectGeneratorTests.cs`, `docs/project-layout.md`, and this file
