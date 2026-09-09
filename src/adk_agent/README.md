# Polson — the ADK agent runtime

A second Google agent runtime for the studio, alongside the Antigravity SDK. This is the deliverable
for the **Agentic Cinema** hackathon (Parallel track); the feasibility brief and the verification
behind every claim here are in [`docs/agentic-cinema-assessment.md`](../../docs/agentic-cinema-assessment.md).

**Status: deployed and drawing on Cloud Run** (private URL), and running locally. One app per project, created dynamically, single- and
multi-agent both working end to end against the real engine. The Dockerfile exists and is unbuilt;
the `src/webapp` UI mount is not written.

## What is here

| File | What it is |
| :--- | :--- |
| `studio.py` | The factory. Builds a studio agent from a project directory. **A module, not a package** — see below. |
| `newproject.py` | Creates a project *and* the ADK app that serves it. Runnable, and callable from the web layer. |
| `main.py` | ASGI entry point — `get_fast_api_app(...)` returning a plain `FastAPI`. |
| `apps/` | Generated, gitignored. One app package per project; nothing committed lives here. |
| `.env.example` | Every environment variable, with what it is for. Copy to `.env`. |
| `requirements.in` / `.txt` | Direct dependencies, and the generated hash-pinned lock. |
| `install.cmd` / `install.sh` | Install into `python-adk/`. |

### One app per project, created dynamically

ADK scopes everything by app name, and one app means one `McpToolset` — built once and cached — so
one studio server and one project directory for every session it serves. An app per *workflow* would
therefore make everyone drawing a logo share one set of `artifacts/`. An app per *project* gives each
brief its own studio.

```bash
python-adk/Scripts/python.exe src/adk_agent/newproject.py bookbindery --workflow logo --prompt "a mark for a bookbindery"
```

That writes `projects/bookbindery/` and `src/adk_agent/apps/bookbindery/`, and the app is served at
`/apps/bookbindery/...` **with no restart** — `AgentLoader.list_agents` does a fresh `os.listdir` on
every call and only `load_agent` caches, per name. Verified against a running server: `/list-apps`
went from `["bookbindery","nightmarket"]` to `["bookbindery","latenight","nightmarket"]` between two
requests, and the new app immediately accepted session creation.

`studio.py` is a **module, not a package**, and that is load-bearing: ADK lists an app for every
*directory* in the agents dir that looks like an agent, so a shared factory living there as a package
would appear as a broken app. A `.py` file is invisible to that scan, and it sits one level *above*
`apps/` so that directory can be gitignored wholesale. Generated apps reach it through a baked
absolute `sys.path` insert — the same "absolute paths, regenerate after moving" convention
`ProjectGenerator.McpConfig` already documents.

A workflow with `roles/` becomes a root plus one sub-agent per role file, parsed the same way
`ProjectGenerator.Role` parses them so the two cannot disagree. `comic_studio` produces:

```
nightmarket: root=facilitator subs=4
  - penciler   'Composition & Pose Agent (Penciler / Layout)'
  - inker      'Contour & Line Art Agent (Inker)'
  - colorist   'Palette & Lighting Agent (Flatter / Colorist)'
  - critic     'Vision Critic Agent (Adversarial Art Director)'
```

### Peeking, and the two artifact stores

**ADK artifacts are not a file path — they are versioned entities.** `save_artifact` returns an
integer version, and successive saves under one filename accumulate rather than overwrite. They are
scoped to app + user + session by default (a `user:` filename prefix widens that to all of a user's
sessions), and `LoadArtifactsTool` is what puts one into the model's context as a real `inline_data`
Part. The store is chosen by URI: `memory://`, `file://` or `gs://`.

So there are **two** artifact stores here and both are needed:

| | `projects/<id>/artifacts/` | ADK artifact service |
| :--- | :--- | :--- |
| Written by | the sandbox, via `outFile` | the agent, via `peek` |
| Read by | people, the web app, replay | the model |
| Versioned | **no** — same name overwrites | yes, automatically |
| Lives in | the project directory | `memory://`, `file://`, `gs://` |

`main.py` defaults the second to `file://src/adk_agent/artifact_versions` rather than memory,
because the versions are the point. Use `gs://` on Cloud Run, whose filesystem does not outlive the
container.

#### Why `peek` had to exist

The first real run wrote a render to disk and then reported *"Here is what I see"* followed by seven
numbered headings with **empty bodies**. It had no way to look. The Polson tools return JSON, and
ADK's MCP bridge ends with `response.model_dump(mode="json")` — so even a proper MCP image content
block would arrive as base64 inside text, costing the window without being viewable. **The fix could
not live in the MCP server.** `peek` is an ADK `FunctionTool` that reads a rendered image from the
project, saves it as an ADK artifact, and lets `LoadArtifactsTool` inject it.

With it, the same brief produced three renders under one filename at versions 0, 1 and 2, and
observations like *"the overlapping outlines of the pages intersected... a busy, distracting grid"*
and a measured *"only 32px"* gap — where before there was nothing.

> [!NOTE]
> **What peeking fixed and what it did not.** It fixed *cannot see at all*. It did not fix the
> agent's willingness to describe its own output in brochure language — the same run called a small
> gold squiggle *"an intentional, craftsman's knot"*. And the mark from the earlier, blind run was
> arguably the better one, on a different prompt. Peeking is necessary for the perception-action
> loop; it is not by itself a critical faculty, and it should not be sold as one.

#### Scripts are artifacts too

`write_script` and `edit_script` author the drawing, and every save is a version in the same store as
the renders. `text/javascript` matters: ADK inlines only image, audio, video and PDF, and falls
through to a UTF-8 text conversion for anything `text/*` — so a loaded script reaches the model as
source rather than as base64.

**Why they had to exist.** `ExecuteScript` accepts `scriptFile`, `InspectScript`'s own parameter
docs give `artwork.js` as the example, and the SDK reference recommends keeping anything longer than
a screenful in a file. None of it was reachable: the sandbox writes only images and SVG, and ADK
supplies no file-writing tool, so the agent could *run* a file but never *create* one. The same
shape as the peek gap — a documented workflow that works elsewhere because the *host* supplies the
missing verb.

**Measured on this runtime, same project and brief, before and after:**

| | inline only | file workflow |
| :--- | :--- | :--- |
| `ExecuteScript` median emission | **22.9s** | **2.0s** |
| executions | 3, costing 80.3s | 15, costing 35.8s |
| where the time went | 61% typing scripts | 37% edits, 19% initial writes |

The script stops travelling with the call. The cost moves into `write_script` (median 14.4s, once per
file) and `edit_script` (median 6.6s), and an edit is roughly a third of what a full re-send was.

> [!IMPORTANT]
> **`write_script` alone would not have delivered this.** It takes whole-file content, so revising
> still means re-emitting the program. `edit_script`'s exact-match replacement is what captures the
> saving, and it requires the match to be **unique** rather than replacing the first hit — a repeated
> fragment edited in the wrong place produces a drawing that is wrong somewhere nobody is looking.

In one run the agent took `master_brand.js` to **version 9** across nine edits, running the file
between each. Those versions are what the ADK console's artifact history renders, which makes the
progression of a working file browsable without reading the project directory.

### Shared and sequential

**All agents in an app share one toolset**, so one `Polson.CLI.dll server` process and one `Session`
scratchpad. That is what lets the Penciler leave `Session.pencils = canvas.toBitmap()` for the Inker
with no encode, no decode and no file — `CLAUDE.md` §2's horizontal stigmergy performed rather than
described.

It is safe **only because transfer is sequential.** `transfer_to_agent` moves control rather than
forking it, so one agent runs at a time.

> [!WARNING]
> **Do not put these agents under `ParallelAgent`.** `SessionContext.Storage` is a plain
> `Dictionary<string, object?>`, not a concurrent one, and the engine builds a fresh Jint instance
> per execution — two agents at once would mutate that dictionary from two threads, with SkiaSharp
> bitmaps stashed inside it. If real parallelism is ever needed, give each agent its **own**
> `McpToolset`: that yields one server and one isolated `Session` each. Verified — a write to
> `Session.probe` through one toolset reads back `undefined` through a second.
>
> The costs of sharing, which sequential execution makes survivable: `ScriptHistory` is shared and
> unattributed, `Session` keys are a flat namespace one agent can clobber, and `SessionContext.Stage`
> is a single slot. Under sequential transfer each agent declares its own stage on entry, so `Stage`
> still works as the attribution discriminator.

`build(..., transfer_between_roles=False)` forces every handoff back through the facilitator instead
of allowing peer-to-peer, which is closer to `CLAUDE.md` §3C. Default is peer transfer allowed,
because a penciler handing to an inker is how a pipeline actually runs.

## How ADK differs from the Antigravity path

Same studio, same .NET engine, same MCP server — wired two different ways. Worth understanding
before changing either, because the differences are structural rather than cosmetic.

| | **Antigravity** | **ADK** |
| :--- | :--- | :--- |
| An agent *is* | a generated **directory** | a Python **object** |
| Instructions | `GEMINI.md` in the project | a string on `LlmAgent` |
| Tool wiring | `.agents/mcp_config.json` | `McpToolset` objects in code |
| Permissions | `.agents/settings.json` | not a concept; ADK has no tool-policy file |
| Discovery | the host reads the wiring files | `<agents_dir>/<name>/agent.py` exporting `root_agent` |
| Who owns the loop | the host (Desktop/IDE, or `src/orchestrator`) | ADK's `Runner` |
| Config | JSON files in the project | environment variables and `.env` |
| Serving | our orchestrator + FastAPI app | `adk web` / `adk api_server` / `get_fast_api_app` |
| Deploy | not a deployment target | `adk deploy cloud_run\|agent_engine\|gke`, or our own image |

**The consequence that shaped `agent.py`.** ADK's model would have us restate the instructions and
the MCP launch command in Python — a second copy of both, free to drift from the generated project.
It does not. `agent.py` **reads the generated project**: its `GEMINI.md` becomes the agent's
instruction, and the launch command mirrors `ProjectGenerator.McpConfig` exactly. So a project
behaves the same under either runtime, and the prompt work in
`src/Polson.CLI/ProjectTemplate/*/instructions.md` — including its untrusted-brief boundary — is
inherited rather than reimplemented.

### The director's conversation, and why it is two modules

The Antigravity path gets both directions from the SDK: a trigger coroutine can `ctx.send(...)` into
a running agent, and `OnInteractionHook` lets the agent stop and ask. **ADK offers neither**, so both
are built here — and they are separate modules because they are different mechanisms, not because
the code is long.

| | `interject.py` | `ask.py` |
| :--- | :--- | :--- |
| Who speaks first | the director | the agent |
| Mechanism | `after_tool_callback` returns a **replacement tool result** | a plain tool **awaits a future** |
| Delivered | at the agent's next tool call | as soon as somebody clicks |
| If nobody is there | nothing to deliver; harmless | the agent waits, then is told to decide |
| Blocks the run | no | yes, for at most `POLSON_ASK_SECONDS` |

Both work for one reason: `main.py` mounts the studio **on the ADK app**, so the page and the agent
are in one process and the queue and the future are ordinary local objects. The studio's HTTP routes
(`POST /runs/<id>/say` and `/answer`) and the run page's question cards were already there from
Milestone 6 and are runtime-agnostic — the page draws a card from a `question.open` line in
`events/director.jsonl` without knowing who wrote it. **So the whole ADK side of this was publishing
the event and holding the future**; nothing in the browser changed.

> **`ask_director` is on the root agent only.** A question suspends whoever calls it, so four roles
> holding it is four cards in front of one director. The Facilitator holds the brief and knows what
> is genuinely unsettled; a role that wants a ruling transfers to it.

> **Waiting is credited back, up to a ceiling.** `studio.credit_wait` pushes `_invocation_started`
> and the role clock forward by the wait, so a director thinking for ninety seconds does not spend
> ninety seconds of the agent's deadline — otherwise asking is a tool with a cost the agent cannot control, which teaches
> it to guess. It cannot reach `Stage.elapsedMinutes`, which the .NET side anchors on its own
> `StartedUtc`, so the sandbox clock reads higher than `budget_status` on a run that asked. That is
> the reason the timeout is 120s rather than the Antigravity path's 600.
>
> **The ceiling is `MAX_CREDIT_SECONDS`, 300 by default, and it guards a platform kill rather than a
> budget.** Crediting moves the breaker's anchor and not the wall, so an invocation that waits over
> and over drifts away from real time — and Cloud Run ends a request at 3600s whatever the breaker
> thinks. `drawing` is the case to watch: it asks at every turn boundary by design, and its
> 45-minute default plus the 15-minute grace already *is* that ceiling. Past the cap the clock runs
> again, which is the right incentive: `ask_director` is for the few decisions that shape everything
> after them, not for every turn.

Two ADK details worth knowing, both of which fail *quietly* if you get them wrong:

- **MCP resources are off by default.** Polson serves the studio manuals as `polson://manual/*`
  resources, not tools. Without `use_mcp_resources=True` the agent gets the drawing surface and none
  of the design knowledge — the Extended Mind half of `CLAUDE.md` §2 simply absent, with no error.
- **The stdio connect timeout defaults to 5 seconds.** A cold .NET start can exceed that, and the
  symptom is an agent with an empty toolset rather than a stack trace. `agent.py` sets 60.

### Multiple agents: two levels, often conflated

**Multiple apps in one container.** `agents_dir` may hold several agent packages. `/list-apps`
enumerates them, every route is `/apps/{app_name}/...`, and each is loaded lazily and cached on first
request with its own `.env` and its own sessions. They are peers with no relationship. We have one.

**Multiple agents inside one app.** The agent tree — `root_agent.sub_agents`, with control moving
when the model calls `transfer_to_agent`, constrained by `disallow_transfer_to_parent` and
`disallow_transfer_to_peers`. `root_agent` is a computed property that walks `parent_agent` upward,
so "root" means only "the one with no parent". `AgentTool` is the alternative: wrap an agent as a
callable tool rather than transferring control to it.

This is where `comic_studio`'s four role files (`roles/01_penciler.md` … `04_critic.md`) land — as
`sub_agents`, each an `Agent` whose `instruction` is read from its role file, the same way the root's
is read from `GEMINI.md`.

> [!IMPORTANT]
> **`root_agent` does not have "the most permissions" — ADK has no permission model.** `tools` is
> documented as *"Tools available to this agent"*: strictly per-agent, no inheritance, and a
> sub-agent may hold **more** tools than the root. Nothing in `google/adk/` reads a `settings.json`,
> an `mcp_config.json` or any project policy file; the only related surfaces are
> `require_confirmation` on a tool or toolset (default `False`), `before_tool_callback`, and plugins.
> Permissions in ADK *are* the tools list — allowlist by construction rather than deny by policy.
>
> **The consequence for a generated project.** `.agents/settings.json` denies `generate_image` and
> auto-approves the polson tools. Under ADK the auto-approve half is moot (nothing prompts by
> default) and the deny half is **inert** — nothing reads the file. The agent lacks `generate_image`
> because we never attached it, which is the stronger position, but it means create-project's claim
> that image generation is *"refused two ways"* holds only one way here: the rule in `GEMINI.md`.
> Re-read that policy before attaching any built-in tool.

### `adk web` vs `adk api_server` vs `main.py`

All three call the same `get_fast_api_app(...)`. `adk web` serves ADK's bundled console — an 8.3 MB
pre-built Angular SPA shipped inside the wheel at `google/adk/cli/browser/` — alongside the REST API;
`adk api_server` serves the API alone. `main.py` calls the function directly, which is the only route
that hands you the `FastAPI` object, so `src/webapp`'s studio UI can be mounted on the same app and
port. ADK's Cloud Run guide names exactly that as the reason to take it.

## Two runtimes, two virtual environments — pick one per run

**You decide which; you do not need both.**

| Runtime | venv | Install | Manifest |
| :--- | :--- | :--- | :--- |
| **Antigravity SDK** (the original) | `python/` | `src/webapp/install.{cmd,sh}` | `src/webapp/requirements.txt` |
| **ADK** (this one) | `python-adk/` | `src/adk_agent/install.{cmd,sh}` | `src/adk_agent/requirements.txt` |

**They are separate environments on purpose.** ADK caps `websockets<16`; `google-antigravity` sets no
upper bound, and the webapp lock resolves it to 16.1.1. In one shared venv the second install
silently wins and neither lock describes what is actually there. Two venvs also mirror the container
we ship, which carries ADK and not Antigravity.

`python-adk/` is not committed; `python -m venv` writes its own `.gitignore` (`*`) into it, so it
self-ignores. It is rebuildable from the committed, hash-pinned `requirements.txt`.

## First-time setup

Run from the repository root. Nothing installs itself — per the project guardrails, package
installation is always a deliberate act by a person.

On Windows:

```bash
python -m venv python-adk
src\adk_agent\install.cmd
```

On Linux or macOS the venv puts executables in `bin/` rather than `Scripts/`, which the script
handles:

```bash
python3.13 -m venv python-adk
src/adk_agent/install.sh
```

**Name the version, not `python3`.** Same trap as the webapp venv: where `python3` is older than 3.13
(Ubuntu 22.04's is 3.10) the venv is built with it silently, and pip refuses a pinned package much
later and further away.

## Running it

**1. Build the MCP server** if `bin/cli/Polson.CLI.dll` is stale or missing.

**2. Configure credentials.** Copy `.env.example` to `.env`. The key already in
`bin/cli/appsettings.json` under `ApiKeys:GoogleAgentPlatform` is a Gemini Enterprise Agent Platform
credential (prefix `AQ.A`, not a public `AIza` one), so it needs `GOOGLE_GENAI_USE_ENTERPRISE=1`.

`POLSON_PROJECT_DIR` is **not** needed — each generated app carries its project path baked in, which
also sidesteps ADK's `.env` resolution being first-found-wins rather than layered.

**3. Create a project.** This writes both the project and the app that serves it:

```bash
python-adk/Scripts/python.exe src/adk_agent/newproject.py bookbindery --workflow logo --prompt "a mark for a bookbindery"
```

> **A test run (`--test`) must carry both a deadline and a token budget.** They fail in opposite
> directions and neither defaults to anything useful for an evaluation.
>
> - No **deadline** → no clock, and `findings.md` is written from the allowance left at the end, so it
>   is never reached. A workflow missing from the CLI's deadline table resolves to *no deadline at
>   all* rather than to a wrong one.
> - No **token budget** → nothing stops a run that will not converge. Input is the whole conversation
>   resent each turn, growing O(n²) while output stays flat.
>
> Measured on `apollovec3`: **2,030,346 input tokens against 30,240 of output** — 67:1 — in 30 turns
> and 10.8 minutes, comfortably inside its 30-minute deadline. **The clock would never have caught
> it**; the token breaker did. Set both:
>
> ```bash
> python-adk/Scripts/python.exe src/adk_agent/newproject.py <id> --workflow <w> --test --deadline 30 --prompt "..."
> POLSON_BUDGET_TOKENS=2000000 python-adk/Scripts/python.exe -m uvicorn main:app --app-dir src/adk_agent
> ```
>
> The cap counts **raw** input on purpose. On that run 61% was cached and billed at about a tenth, so
> the bill was well under the number that halted it — it is a runaway alarm, not a budget in money.

**4. Serve.** Through `main.py`, which is what the container will run, so local and deployed behave
the same:

```bash
python-adk/Scripts/python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000 --app-dir src/adk_agent
```

`adk web src/adk_agent/apps` serves the same thing through the CLI instead. Either way `/` redirects
to ADK's console at `/dev-ui/`, and the REST API is under `/apps/{app_name}/...`, with `/list-apps`
enumerating what is available. Projects created while it runs appear without a restart.

## Deploying

The image is built by **Cloud Build, not locally** — `--source .` uploads the tree and builds
remotely, so no Docker engine is needed on a developer machine at any point.

```bash
gcloud run deploy polson-studio --source . \
  --project <project> --region <region> --allow-unauthenticated \
  --set-secrets POLSON_AGENT_PLATFORM_KEY=polson-agent-key:latest
```

> [!IMPORTANT]
> **The engine's settings are configured through `POLSON_*` variables, which the entrypoint writes
> into `appsettings.json` at start.** The .NET side reads that file and nothing else —
> `Runtime.LoadConfigFile` uses `AddJsonFile` with no `AddEnvironmentVariables` — so this is the only
> route, and until 2026-09-08 the entrypoint wrote the credential alone. Everything below was
> therefore *unreachable* on a deployment rather than merely defaulted, which is a failure that looks
> exactly like a working configuration.
>
> | variable | setting | note |
> | :--- | :--- | :--- |
> | `POLSON_AGENT_PLATFORM_KEY` | `ApiKeys:GoogleAgentPlatform` | the secret; also feeds the Python half |
> | `POLSON_PARALLEL_KEY` | `ApiKeys:Parallel` | **without it research is silently disabled** |
> | `POLSON_ASSETS_BUDGET` | `Assets:Budget` | generations **per server run** — one container, every project |
> | `POLSON_ASSETS_MODEL` · `POLSON_ASSETS_CACHE_DIR` | `Assets:*` | |
> | `POLSON_DOCUMENTS_BUDGET` · `_MODEL` · `_CACHE_DIR` | `Documents:*` | |
> | `POLSON_PHOTOS_BUDGET` · `_ALLOWED_HOSTS` · `_USER_AGENT` | `Photos:*` | |
> | `POLSON_RESEARCH_BUDGET` · `_PROCESSOR` · `_ARCHIVE_DIR` | `Research:*` | |
> | `POLSON_SERVER_TIMEOUT_SECONDS` | `Server:DefaultTimeoutSeconds` | |
>
> An unset variable writes no key, so defaults still apply. A numeric one that is not a number is
> **reported to stderr and dropped** rather than written through — `int.TryParse` would leave zero,
> and a budget of zero disables the surface outright, so a typo would present as "requisition is
> broken" with nothing saying why. `test_entrypoint.py` runs the real generator extracted from the
> script, and fails if the engine gains a setting the container cannot set.

> [!IMPORTANT]
> **If you reach a private service through `gcloud run services proxy`, set `POLSON_ALLOW_ORIGINS`
> or every form submission is refused.** ADK's CSRF middleware compares the request's `Origin`
> against the allowed list, and through the proxy the browser's origin is `http://127.0.0.1:8080` —
> genuinely a different origin from the service's own host, so it is refused with
> `Forbidden: origin not allowed`.
>
> **It looks like a broken deploy rather than a config gap**, because a *browser* only sends `Origin`
> on the POST: the form loads perfectly at `/new`, and submitting it fails. Nothing in the log
> explains it, because the refusal happens in middleware before any of our handlers run.
>
> ```bash
> gcloud run services update polson-studio --project <project> --region <region> \
>   --update-env-vars "^;^POLSON_ALLOW_ORIGINS=http://127.0.0.1:8080,http://localhost:8080"
> ```
>
> **`^;^` is not decoration.** `--update-env-vars` splits on commas, so without it this value becomes
> one variable holding `http://127.0.0.1:8080` and a second bogus one named `http://localhost:8080`.
> The prefix makes `;` the separator for the whole argument, leaving commas inside the value alone.
>
> A public deployment reached at its own `run.app` URL needs none of this — the origin is then the
> service's own host. This is a proxy-only requirement, which is why it is easy to meet for the first
> time long after the deploy that "worked".

> [!NOTE]
> **`POLSON_ARTIFACT_SERVICE_URI=gs://<bucket>` works, and on Cloud Run it should be set.**
> `GcsArtifactService.__init__` does a lazy `from google.cloud import storage`, so this used to be a
> `ModuleNotFoundError` at service construction — during startup, not at image build. It is no longer:
> `google-cloud-storage` is pinned in `requirements.in` and in the lock.
>
> Left `file://` on Cloud Run, the version history dies with the instance, and instances are replaced
> without warning — see `mirror.py`, which exists because that happened twice in one day.
>
> Note also that only the **bucket name** is read — `bucket_name = parsed_uri.netloc` — so any path
> after it (`gs://bucket/prefix`) is silently discarded rather than honoured.

Three files make that work, and each exists for a reason worth knowing.

**[`Dockerfile`](../../Dockerfile)** at the repository root, because Cloud Build looks for it at the
root of the context — and the context must be the root anyway, since the .NET build needs
`nuget.config`, `Directory.Build.props` and all of `src/`.

It **builds the engine from source** rather than copying `bin/cli`. Two reasons, either sufficient:
`bin/` is gitignored so `--source .` would never upload it, and the local build is **588 MB** of
which **550 MB is `runtimes/`** — native assets for every RID NuGet knows. Publishing for
`linux-x64` keeps one platform's. Restore runs with `RestoreLockedMode=true`, matching what
`CLAUDE.md` §7 asks of CI.

The .NET runtime arrives by `COPY --from=mcr.microsoft.com/dotnet/runtime:10.0`, not by piping
`dotnet-install.sh` into a shell — same bits from a tagged image, without an unreviewed download
executing at build time. The base is `python:3.13-slim` rather than a .NET image with Python added,
because `requirements.txt` was compiled `--python-version 3.13` and Debian's own Python is older.

**[`.gcloudignore`](../../.gcloudignore)** is not optional. Without it gcloud generates one that
includes `.gitignore` — which would still upload **`reference/`, 1.4 GB** of books and third-party
source, because those are tracked and therefore not gitignored.

**[`docker-entrypoint.sh`](../../docker-entrypoint.sh)** does the two things the image cannot.

### The credential cannot be baked in

The .NET engine reads its key from `appsettings.json` beside the DLL and **nowhere else** —
`Runtime.LoadConfigFile` builds its configuration from `AddJsonFile` alone, with no
`AddEnvironmentVariables`, which `orchestrator/credentials.py` explains is deliberate: two halves of
one studio must not be able to authenticate as different identities.

So the entrypoint writes that file at start from `POLSON_AGENT_PLATFORM_KEY`, JSON-escaped, mode
600, and exports the same value under the names google-genai expects. One secret feeds both halves
and they cannot disagree. A missing key **warns rather than aborts** — the console still serves, and
the first thing needing a model fails with a message naming the variable, because a misconfigured
deploy should not look like a broken image.

### What the container will do differently

> [!IMPORTANT]
> **Fonts.** A slim image ships none, so `Skia.Font.families()` would return empty and every
> `ctx.font` resolve to nothing. Local runs chose Georgia, and Garamond as *"the highest grade serif
> available in the environment"* — both Windows faces. The image installs `fonts-dejavu-core`,
> `fonts-liberation2` (metric-compatible with Arial/Times/Courier) and `fonts-ebgaramond`. **The
> same brief will therefore be set in different type here than on a developer machine.** That is a
> consequence of deploying, not a defect.
>
> `libfontconfig1` is installed alongside because `SkiaSharp.NativeAssets.Linux` requires it; absent
> it, the native load fails at startup rather than at the first render.

> [!WARNING]
> **`/app/projects` and `/app/adk_agent/apps` are ephemeral, and the instance can be replaced
> mid-run.** This is not a scale-to-zero-between-sessions problem: on 2026-09-08 a run twenty minutes
> in was cut when Cloud Run recycled the instance under it, with memory at 19% and CPU under 4%. It
> happened twice that day. A project created by a visitor dies with the instance, and so does
> everything the agent made in it.
>
> Three settings, each saving something different:
>
> | | |
> | :--- | :--- |
> | `POLSON_ARTIFACT_SERVICE_URI=gs://<bucket>` | ADK's versioned blobs — the renders the model is shown |
> | `POLSON_MIRROR_URI=gs://<bucket>/mirror` | the project directory a person opens, swept every `POLSON_MIRROR_SECONDS` **while the run is still going** |
> | `POLSON_SESSION_SERVICE_URI` | sessions, which are otherwise in-memory on Cloud Run and cannot be resumed |
>
> The middle one is `mirror.py`. It sweeps rather than hooking the writers because the largest writer
> is the .NET engine — `outFile` and `outSvg` land on disk without this process seeing the write.

`POLSON_SEED_PROJECT` seeds one project at startup so a visitor handed the URL finds something
rather than an empty app picker. Off by default, since a deployment driven by its own web layer
should create projects on demand.

### What it took, and what each failure taught

Seven deploys. Every failure is recorded in the Dockerfile beside the line that fixes it, because
each was invisible until it ran:

| failure | cause |
| :--- | :--- |
| `gcloud crashed (PermissionError)` on `.vs/*.vsidx` | `.gcloudignore` did not inherit `.gitignore`; Visual Studio held its index open |
| `error NU1004: runtime identifiers have changed` | `--runtime linux-x64` against RID-less locks; locked mode correctly refused |
| `fc-cache: not found` | `libfontconfig1` is the library; the tools ship in `fontconfig` |
| `No frameworks were found: Microsoft.AspNetCore.App` | copied `dotnet/runtime`; `ModelContextProtocol.AspNetCore` needs `dotnet/aspnet` |
| `CultureInfo..cctor()` crash | copying `/usr/share/dotnet` brings the runtime but not ICU |
| `KnowledgeCorpus` type initializer threw | `.gcloudignore` excluded `docs/`, which are **EmbeddedResource inputs**, not documentation |
| `Permission denied on secret` | the runtime service account needed `roles/secretmanager.secretAccessor` |

Three of those were mine misjudging a file by its extension or its name. The `docs/` one is the
sharpest: an MSBuild glob that matches nothing is not an error, so the build succeeded, embedded zero
manuals, and died at startup a long way from the cause. There is now a guard in stage 1 that fails
the build instead.

**Verified end to end on Cloud Run**: all 13 tools present (so the stdio subprocess spawns in the
sandbox — the risk flagged in the assessment, now closed), `read_file` used first as instructed, a
render written, peeked, loaded into context, and described accurately. The agent reported the type
as **DejaVu Serif**, having checked `Skia.Font.has` first.

## Dependency changes

See [`src/webapp/README.md`](../webapp/README.md) — its dependency layout, vulnerability scan and
upgrade procedure all apply here unchanged, only with `src/adk_agent/` and `python-adk/` in place of
`src/webapp/` and `python/`.

The install scripts read `check_python.py` and `pip.ini` from **`tools/python/`** — one canonical
copy, shared with the webapp's installer, because both are generic and security-relevant (the
interpreter-floor check, and the wheels-only / single-index pip defaults) and a duplicate is one that
can drift. See `tools/python/README.md`.

> [!NOTE]
> **They are in a neutral directory so this venv stands alone.** They used to live in `src/webapp/`,
> which meant `python-adk/` could not be installed without the Antigravity tree present — an ADK-only
> checkout had to fetch a runtime it never uses in order to install the one it does. The same applied
> to `uv`, which existed only in the other venv, so this runtime could not even regenerate its own
> lock. Both are fixed; nothing here now reaches into `src/webapp/`.

`uv` is pinned in `requirements-dev.in` — tooling for working *on* the agent, kept out of
`requirements.in` because the Dockerfile installs that file into the image and a lock compiler has no
place in a production container. Compile this runtime's lock with this runtime's `uv`:

```bash
python-adk\Scripts\uv.exe pip compile src\adk_agent\requirements.in --universal --python-version 3.13 --generate-hashes -o src\adk_agent\requirements.txt
```

On a brand-new venv `uv` is not there yet — compiling its own lock would require the thing that lock
installs — so bootstrap it once with `python-adk\Scripts\pip.exe install uv`, then use the pinned
lock from then on.

`--universal` keeps the environment markers so the one lock installs on every platform. Commit the
result and review its diff — that diff is the supply chain.
