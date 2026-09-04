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

## 2. Uncommitted work

Nothing in this session was committed. `git status` at handoff:

```
 M Dockerfile
 M fetch-fonts.py
 M src/adk_agent/studio.py
 M src/Polson.CLI/ProjectTemplate/comic_studio/roles/01_penciler.md
 M src/Polson.CLI/ProjectTemplate/comic_studio/roles/02_inker.md
 M src/Polson.CLI/ProjectTemplate/comic_studio/roles/03_colorist.md
 M src/Polson.CLI/ProjectTemplate/comic_studio/roles/04_critic.md
```

**The role-file changes are deployed but unverified** — revision 15 carries them, and no run has
tested whether they work. See §4.

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

### 4b. Time budgets — the fix the prompt cannot make

The inker ignored "one pass" because **a prompt is advice, not a constraint**. The enforcement hook
exists and is half-wired:

- `before_model_callback` **short-circuits the model call** when it returns an `LlmResponse`
  (`base_llm_flow.py`: `if callback_response: return callback_response`)
- `studio.py` already has `_before_model` / `_after_model` for turn timing
- `mina.time()` and `Date` exist in the JS sandbox

So `build(project_dir, budget_minutes=...)` splitting an allowance across roles — warn at ~75%,
hard-stop at 100%, reserve for the facilitator's review — is roughly 60 lines and genuinely binding.

**Time is a proxy and a loose one** (a token or turn budget binds more directly to what is spent),
but it is the right currency anyway: constraint is generative. A storyboard artist with an hour
commits early; one with a week makes sixteen versions of panel two. The deadline is part of the
craft, not overhead on it.

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
