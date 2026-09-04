# ADK runtime — session handoff, 2026-09-04

> **Status: the ADK studio is deployed on Cloud Run and has produced real design work there.**
> A single-agent logo brief ran end to end in the container — 24 tool calls, 166 seconds, a finished
> mark. The multi-agent `comic_studio` pipeline reaches all four roles but does not yet compose;
> that is the open work, and the fixes are written but unverified.
>
> Read `src/adk_agent/README.md` first for what the runtime *is*. This document is what a session
> picking it up needs to know that the code does not say.

---

## 1. Where it is

| | |
| :--- | :--- |
| Service | `polson-studio`, project `polson`, region `us-east4` |
| URL | `https://polson-studio-yhijv6wbxq-uk.a.run.app` |
| Revision | `polson-studio-00015-fm6` |
| Access | **Private.** `-H "Authorization: Bearer $(gcloud auth print-identity-token)"` |
| Config | `--timeout=3600 --memory=2Gi --cpu=2 --concurrency=4 --max-instances=1` |
| Env | `ADK_MAX_LLM_CALLS=200`, `POLSON_SEED_PROJECT=lanternrun`, `POLSON_SEED_WORKFLOW=comic_studio` |

**`gcloud` does not work on this machine without `CLOUDSDK_PYTHON`.** The Windows Store `python`
alias intercepts it and every command dies with *"Python was not found"*:

```bash
export CLOUDSDK_PYTHON=/c/DevTools/gcloud/google-cloud-sdk/platform/bundledpython/python.exe
```

Redeploy with:

```bash
gcloud run deploy polson-studio --source . --region us-east4 --project polson \
  --set-secrets POLSON_AGENT_PLATFORM_KEY=polson-agent-key:latest
```

`--max-instances=1` is deliberate: projects live inside the container and are **ephemeral**, so a
second instance would serve an empty app list. Fixing that means projects in GCS, which needs
`google-cloud-storage` added to `requirements.in` — see §5.

---

## 2. Commit state

The Dockerfile, font and role-file work is committed as `4619bc0 Agent role fixes. Additional
fonts.`. **The role-file changes are deployed but unverified** — revision 15 carries them, and no run
has tested whether they work. See §4a.

Uncommitted at handoff: the time-budget, circuit-breaker and supervision work (§4b, §4bb), and
this document. Unit-tested, **not deployed** — revision 15 predates it, so nothing in §4b is live
until the next `gcloud run deploy`.

---

## 3. What was proven in the container

Each of these was an open risk and is now closed:

- **The stdio subprocess spawns in Cloud Run.** The assessment's largest flagged risk. All 13 tools
  load — 7 Polson MCP tools, `load_mcp_resource`, and the four ADK tools. No HTTP fallback needed,
  though ASP.NET is kept in the image so the fallback stays available.
- **The full perception–action loop works.** Render → `peek` → `load_artifacts` → an accurate
  description of the image. Verified by comparing the agent's words against the actual render.
- **Variable font weight axes instance correctly.** `700 40px "Playfair Display"` differs visibly
  from `400`. This was flagged as unverifiable and is now settled.
- **`transfer_to_agent` moves control across four sub-agents** in one invocation.
- **The shared `Session` scratchpad crosses agents** — one wrote, another read, through one engine
  process. Mechanically sound; see §4 for why that is not the same as the pipeline working.

**Fonts went from 9 families to 64.** `fonts-recommended` + `fonts-texgyre` + `fonts-inter` +
`fonts-solide-mirage` from apt, and Playfair Display + Gelasio fetched at a pinned commit with
SHA-256 verification (`fetch-fonts.py`). Gelasio is metric-compatible with Georgia, so the manuals'
fallback chains now land on real faces.

---

## 4. Open work, in the order I would take it

### 4a. Verify the `comic_studio` role fixes — cheap test first

Three defects were found and two fixed; **none of the fixes has been run.**

1. **Ordering (fixed).** Filenames contradicted stage numbering, so the inker was told to open a
   file the colorist had not made yet. Now `stage1_penciler → stage2_inker → stage3_colorist`.
2. **The roles did not compose (fixed, unverified).** The inker's script had zero `Image.load`,
   zero `drawImage`. The cause was the word **"Open"** — ambiguous between `peek` (look) and
   `Skia.Image.load` + `drawImage` (load onto canvas). The agent did the first and drew afresh.
   Roles now name the calls and say *"looking is not loading."*
3. **No convergence bound (fixed, unverified).** The inker took **91 turns, 20 script versions, 16
   renders, 58.8 minutes** and never reached the colorist. All roles now carry *"One corrective
   pass, then hand off."*

> **Test the cheap way first.** One turn directed at the inker alone, against an existing
> `stage1_penciler.webp`, checking whether `Skia.Image.load` appears in its script. That answers the
> whole question in about a minute. A full pipeline run takes an hour and can fail for unrelated
> reasons.

### 4b. Time budgets — **built: per-role warnings and a circuit breaker**

The inker ignored "one pass" because **a prompt is advice, not a constraint**.

**Done (2026-09-04, untested against a live run).** `build(project_dir, budget_minutes=90)` splits a
wall-clock allowance across the roles in `roles_in` order, holds back 15% for the facilitator's
review, and warns each role at 75% and 90% of its own share — once per threshold, naming the minutes
left and the agent to `transfer_to_agent` to. `role_weights={...}` skews the split; the default is
even. Falls back to `POLSON_BUDGET_MINUTES`; with neither set, nothing changes. Unit-tested against
a fake clock — thresholds, once-only firing, the both-at-once case, the weighted split, the
unparseable env value — with no model calls.

Three things in it that are not obvious and cost time to work out:

- **The notice goes on `llm_request.contents`, never `append_instructions`.** The latter edits the
  system instruction, which is the head of the prompt-cache prefix; cached input bills at ~1/10th,
  so warning that way can cost more than the overrun it prevents.
- **It works on `run_async` and is silently inert on `run_live`.** The async path hands the callback
  the real request (`base_llm_flow.py:1735`); the bidi path passes a `model_copy` with `contents`
  replaced (`:985`), so the append is discarded with no error. We serve `/run_sse`, so this is fine
  today and would break unannounced if a live transport were added.
- **`llm_request.contents` entries are shallow copies of session events** (`contents.py:570-615`) —
  appending a new `Content` is safe, mutating an existing one's nested fields corrupts history.

**Also done: the circuit breaker.** A separate thing from a graceful stop, and the more important
one. At `budget + 15 min` — or at `POLSON_MAX_MINUTES` on its own — **no agent in that invocation
makes another model call**, and the run unwinds with a `[studio runtime] HALTED` turn naming the
numbers. One `ERROR BREAKER …` line in the container log on the first trip, `WARNING` thereafter, so
a runaway is greppable.

The implementation is not the obvious one, and the two reasons are worth keeping:

- **`end_invocation` alone does not work.** `BaseAgent._create_invocation_context` is a *shallow*
  `model_copy`, so the flag set inside a sub-agent never reaches the facilitator that transferred to
  it — which would simply transfer somewhere else. And the public route to the context,
  `callback_context.get_invocation_context()`, returns a **copy** (its own docstring says so), so
  setting the flag there changes nothing at all.
- **What is shared is `invocation_id`.** So the breaker is a module-level set of tripped ids checked
  at the top of every `before_model_callback`. Every model call in the process passes through there,
  so once an id is in the set no further call can be issued under it — including by an agent
  transferred to *after* the halt, which gets stopped on its first turn. `end_invocation` is still
  set, best-effort through the private attribute, purely so the current agent unwinds this turn
  rather than next; the trip set is the enforcement.

Verified: the halt event's `is_final_response()` is `True` with no function calls, so the agent loop
breaks rather than spinning. **What it does not stop** is a tool call already in flight — the breaker
sits between model calls, so a running `ExecuteScript` finishes first, bounded by the engine's own
script timeout. It halts one invocation, not the process.

> **It is off unless armed, and arming it is a deploy flag, not a code change.** The generated
> `apps/*/agent.py` call `build(project_dir)` with no arguments, by design, so one env var covers
> every app with no regeneration:
>
> ```bash
> gcloud run services update polson-studio --region us-east4 --project polson \
>   --update-env-vars POLSON_MAX_MINUTES=50
> ```
>
> **Pick a number below the Cloud Run request timeout (3600s).** Above it, the timeout wins and you
> get a dropped connection with no explanation; below it, you get a clean logged halt that says why.

**Still not done, on purpose: the graceful per-role stop.** Returning an `LlmResponse` short-circuits
the model call, and `callback_context.actions.transfer_to_agent` forces a handoff — the wrapper gates
on the action, not on a function call (`workflow/_llm_agent_wrapper.py:504`, and its comment says
so). Left unwired because a *warned* model can finish well — save the render, say what it shows, hand
off — where a forced transfer cuts it off mid-pass. The breaker covers the runaway case; this would
only cover the merely-slow one. Wire it if the warnings are measured and found to be ignored, and if
you do, set the action *and* return a synthetic `transfer_to_agent` call: ADK 2.8 has a legacy nested
path (`base_llm_flow.py:1692`) that only fires on function calls, and which path a deployment takes
could change with a version bump.

**Time is a proxy and a loose one** (a token or turn budget binds more directly to what is spent),
but it is the right currency anyway, for a concrete reason as well as the romantic one: the hard
external constraint is the Cloud Run request timeout, and it is denominated in seconds. A budget in
the same currency as the wall is the one that stops you hitting it — a token budget would not have
prevented a 58.8-minute inker. And constraint is generative: a storyboard artist with an hour
commits early; one with a week makes sixteen versions of panel two. The deadline is part of the
craft, not overhead on it.

**Still worth adding:** a turn count as a secondary tripwire. A minute-clock is slow to notice a role
thrashing on many short calls.

### 4bb. Supervision — a watchdog plugin and an advisor the roles can call

**Built 2026-09-04, unit-tested against the real engine, not yet run with a model.** This is the part
that uses ADK's own agent capabilities rather than working around them.

**The fact that shapes the whole design: the Facilitator cannot watch the Inker.**
`transfer_to_agent` moves control rather than forking it, so while the Inker works the Facilitator is
not running at all. There is no agent in a position to observe. A `BasePlugin` is — its sixteen
callbacks fire for *every* agent in the app, from outside all of them.

- **`src/adk_agent/supervision.py`** — `StudioWatchdog(BasePlugin)`. Three triggers, all chosen and
  all live: eight working calls with no handoff; three consecutive renders that do not move the
  picture; past 90% of the role's own time allowance and still editing. Capped at two interventions
  per role, and re-armed by a handoff or by asking for help.
- **It compels an ask rather than seizing control.** Forcing `transfer_to_agent` back to the
  Facilitator is available and works (`tool_context.actions` is live in these callbacks). It is not
  what this does: `after_tool_callback` may *replace* a tool result, so the plugin appends a
  directive to the result the role is about to read — *"stop and ask before your next edit"*. The
  role still decides; it can no longer fail to notice.
- **`ask_facilitator`** — an `AgentTool` on every role. This is the one genuinely message-shaped
  thing ADK offers: request in, answer back, control never leaves the caller. It wraps a **separate**
  advisor agent, not the root, because an `AgentTool` runs in a fresh in-memory session seeded with
  state but *not* conversation history (`agent_tool.py:264-289`) — so fresh eyes are what it can give,
  which is the right answer anyway. It holds `peek`, `read_file` and `load_artifacts` and **cannot
  draw**, so it cannot answer by quietly doing the work itself.
- **`build_app()` returns an `App`** carrying the plugin, and the generated `agent.py` now exports
  `app`. The loader checks `app` before `root_agent` (`agent_loader.py:128`), so nothing about
  serving changes and an older generated file still works. The watchdog is handed the **same**
  `McpToolset` instance the agents hold — a second one would spawn a second engine process and
  quietly falsify this module's one-server-per-app argument.

**New MCP tool: `CompareImages(pathA, pathB, maxDimension=256, tolerance=8)`.** The watchdog's first
version hashed render files, which is the wrong question — an encoder can produce different bytes for
the same picture, so a hash-based watchdog never fires. This exposes `SkiaBitmapWrapper.Diff` as a
tool, downscaled first so the question is *did the picture change* rather than *did any pixel*.
Calibrated on real renders at 256px:

| | similarity |
| :--- | :--- |
| re-run of the identical scene | **1.0000** |
| one element moved 4px | 0.9929 |
| one element moved 160px | 0.8171 |

Hence `UNCHANGED_SIMILARITY = 0.999`. Hashing survives only as a free fast path: identical bytes are
certainly the same picture, so the comparison is skipped; differing bytes prove nothing and fall
through to the real compare. Different canvas sizes return `comparable: false` rather than throwing —
`Diff` throws by design, but for "did this change", a size change *is* the answer. Six tests in
`tests/Polson.Tests.MCPServer/CompareImagesTests.cs`; the suite is 475 green.

> **Two traps this cost.** The plugin sees an MCP result as
> `{"content": [{"type": "text", "text": "<json>"}], "isError": false}` — the payload is a JSON
> **string one level down**, not a flat dict; found by calling the tool against the live server rather
> than by reading the wrapper, and guessing it would have produced a watchdog that silently never
> fired. And the module is `supervision.py`, **not** `watchdog.py`: the PyPI `watchdog` package is
> installed, and `agent.py` does `sys.path.insert(0, runtime_dir)`, so that name would have shadowed
> it for uvicorn's reloader.

**Not done.** The roles' own prompts do not mention `ask_facilitator` — the tool's description carries
it, and the watchdog directive names it explicitly, so voluntary use rests on the model reading its
tool list. Worth a line in the role templates if voluntary asking turns out to be rare. Also: `bin/cli`
was republished locally, so **the container needs a rebuild** before any of this is live.

### 4bc. The deadline as a brief, and `create-project --deadline`

**Built 2026-09-04.** The three mechanisms above are all *enforcement*, and enforcement alone teaches
an agent nothing: it learned about time only when three-quarters of it was gone, which is exactly
when planning is no longer possible. A deadline is only a constraint you can work to if you are told
it at the start.

- **`create-project --deadline <minutes>`**, written into `project.json` as `deadlineMinutes` and
  rendered into the instructions from `_shared/deadline.md`. Omit the flag and the **workflow's own
  default** applies: logo 15, infographic 30, drawing 45, comic 60, comic_studio 90, painting 120,
  harness 0. One number could not serve a 15-minute mark and a two-hour study painting, and it would
  be wrong in the more damaging direction for the painting — a deadline that cannot be met is how an
  agent learns that stated constraints are decorative. `--deadline 0` means none, and then the
  instructions say **nothing** about time rather than claiming a limit nothing enforces.
- **The prompt guardrail** tells the agent to plan backwards, that *scope* is the variable and finish
  quality is not, to record what it cut in a `Stage.note`, and — the practical half — where the time
  actually goes: draft small and render the final large, edit `artwork.js` rather than re-sending it,
  `render: false` for probe passes, one native measurement call instead of a per-pixel loop.
- **It gives the agent a clock, because the sandbox has none.** `Session.startedAt ??= Date.now()`,
  then elapsed against it. Verified in the engine: `??=` works on `Session` and is idempotent, so
  repeating the line in a later script is safe.
- **Python reads it.** `deadline_in(project)` sits in the precedence as **explicit argument >
  `project.json` > `POLSON_BUDGET_MINUTES`** — the project beats the environment because a deadline
  belongs to the commission, not the deployment. `build_app` duplicates that precedence deliberately;
  if the two disagreed, the watchdog would time roles against a different clock from the one they
  were told about, and it would look like a flaky model.
- **Each role is told its own share** in its brief — *"You have about 8 minutes of the project's 40"*
  — naming the successor to hand to and `ask_facilitator` as the thing to do when stuck. This goes in
  the **system instruction**, which is safe here precisely because it is constant for the whole run:
  the same text sent per-turn is what would break the prompt cache.

> **One test needed changing and the reason is worth keeping.** `--deadline` did not break it; the new
> `CompareImages` tool did. A Claude subagent's frontmatter names its tools in sorted order, and the
> test asserted `tools: mcp__polson__ExecuteScript` — pinning whichever tool sorts first. Adding a
> tool beginning with C failed a test about whether a subagent can execute scripts. It now asserts
> membership. Worth remembering that the generated subagent tool list is derived from the real MCP
> surface, so **every new MCP tool reaches Claude Code subagents automatically**.

### 4c. Mount `src/webapp` on the ADK FastAPI app

Agreed direction, not started. `get_fast_api_app` returns a plain `FastAPI`, so the studio UI and
the ADK API can share one container and one port — which is why we call the function directly rather
than using `adk web`.

**Use `/run_sse`, not `/run`.** `/run` buffers the whole invocation and returns at the end, so a
six-minute turn shows nothing and is indistinguishable from a hang. This bit twice in one session:
once as a design observation, once when a monitor watched a file that could not fill until the run
finished.

Decided against customising ADK's console (A2UI or MCP Apps): the console is a debugging tool, our
own UI has no primitive ceiling, and the bundled console has **no renderer for MCP App widgets** at
all — the Python side pushes them and the frontend ignores them.

### 4d. Persistence

`gs://` for artifacts needs `google-cloud-storage` in `requirements.in` — `GcsArtifactService.__init__`
does `from google.cloud import storage`, a lazy import, so a `gs://` URI currently fails at
**startup**, not at build. Projects themselves are ephemeral; that is the bigger gap.

### 4e. Public access

One flag, no rebuild: `--allow-unauthenticated`. Deliberately not done — verify behaviour first, and
`CLAUDE.md` Milestone 6 §4 warns about an open cost surface on a metered service.

---

## 5. Things that will mislead you

- **`gcloud ... --set-env-vars` splits on commas, not spaces.** A value with spaces survives; one
  with a comma is silently torn apart. Cloud Run job `--args` needs `^:^` for the same reason.
- **Cloud Run has no `exec`.** A Cloud Run *job* on the same image runs arbitrary commands with no
  rebuild — but it is a **separate ephemeral container**, so it cannot inspect the running service's
  filesystem, and overriding its entrypoint skips seeding.
- **The engine logs to a file, not stderr.** `Runtime.WithFileLogging` writes
  `<assembly>/Polson-CLI*.log` with no console sink, despite a comment in `Program.cs` claiming
  "file/stderr". When the MCP server dies during the stdio handshake, the container log shows only
  the Python side's `McpError: Connection closed`. Adding a stderr sink would make the engine
  debuggable in any container and is a small C# change worth doing.
- **A client timeout below the server's does not stop the spend.** A 3400s client gave up on a 3600s
  request; the server kept working for minutes with nobody listening.
- **Cost from the turn log was an upper bound.** Cached input bills at ~$0.15/M against $1.50/M.
  `studio.py` now logs `cached=`, so the next figure is exact. Billing lags about a day.
- **Do not infer a model id from an invoice.** `Gemini 3.0 / 3.1 Pro` is a SKU name; no spelling of
  it resolves. Measured availability is in the `adk-model-and-cost-reality` memory.

---

## 6. Recurring mistakes worth not repeating

Three of this session's deploy failures had one cause: **judging a file by its extension or name
rather than by what the build does with it.**

- `.gcloudignore` excluded `*.md` — but `Polson.CLI.csproj` embeds `ProjectTemplate/**/*.md`, so the
  39 workflow templates are *source*.
- It then excluded `docs/` — but `Polson.MCPServer.csproj` embeds `docs/*.md` and
  `docs/manuals/*.md`, so the manuals are the agent's knowledge base, compiled in.
- **An MSBuild glob that matches nothing is not an error**, so both built cleanly and died at
  runtime in a type initializer, far from the cause. There is now a guard in stage 1 that fails the
  build and names the reason.

And one about testing: **read the prompt before designing a brief that tests it.** A run concluded
"the roles do not compose" when the brief had itself overridden the roles' handoff instructions.
