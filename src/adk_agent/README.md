# Polson — the ADK agent runtime

A second Google agent runtime for the studio, alongside the Antigravity SDK. This is the deliverable
for the **Agentic Cinema** hackathon (Parallel track); the feasibility brief and the verification
behind every claim here are in [`docs/agentic-cinema-assessment.md`](../../docs/agentic-cinema-assessment.md).

**Status: runs locally; container authored but never built.** One app per project, created dynamically, single- and
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
| Who owns the loop | the host (Desktop/IDE, or `src/webapp/orchestrator`) | ADK's `Runner` |
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

> [!WARNING]
> **`POLSON_ARTIFACT_SERVICE_URI=gs://<bucket>` does not work yet, and would stop the container
> starting.** `GcsArtifactService.__init__` does `from google.cloud import storage`, and
> `google-cloud-storage` is **not** in `requirements.txt` — we install `google-adk[mcp]`, while GCS
> lives in the `[gcp]` extra. The import is lazy, so the failure arrives at service construction
> during startup rather than at image build, as `ModuleNotFoundError`.
>
> Add `google-cloud-storage` to `requirements.in` and recompile the lock before passing a `gs://`
> URI. Until then the artifact store falls back to `file://` on the container's ephemeral disk, and
> version history dies with the instance.
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
> **`/app/projects` and `/app/adk_agent/apps` are ephemeral.** A project created by a visitor dies
> with the instance. Fine for a demo where one session serves one brief; wrong for anything that
> must persist. Pass `POLSON_ARTIFACT_SERVICE_URI=gs://<bucket>` so at least the version history
> outlives the container, and `POLSON_SESSION_SERVICE_URI` for sessions.

`POLSON_SEED_PROJECT` seeds one project at startup so a visitor handed the URL finds something
rather than an empty app picker. Off by default, since a deployment driven by its own web layer
should create projects on demand.

### Not yet verified

**Nothing here has been built or deployed.** The Dockerfile is authored against what was checked in
the source — target framework `net10.0`, `SkiaSharp.NativeAssets.Linux` already referenced, seven
projects with seven `packages.lock.json`, the entrypoint parsing under `sh -n` with LF endings — but
a remote build is 5–15 minutes per attempt for an image this size, so expect to iterate rather than
succeed first time.

## Dependency changes

See [`src/webapp/README.md`](../webapp/README.md) — its dependency layout, vulnerability scan and
upgrade procedure all apply here unchanged, only with `src/adk_agent/` and `python-adk/` in place of
`src/webapp/` and `python/`.

The install scripts deliberately **reuse** `src/webapp/check_python.py` and `src/webapp/pip.ini`
rather than keeping their own copies. Both are generic and security-relevant — the interpreter-floor
check and the wheels-only / single-index pip defaults — and a duplicated copy is one that can drift.

`uv` lives in the *original* venv, so compile this lock from there:

```bash
python\Scripts\uv.exe pip compile src\adk_agent\requirements.in --universal --python-version 3.13 --generate-hashes -o src\adk_agent\requirements.txt
```

`--universal` keeps the environment markers so the one lock installs on every platform. Commit the
result and review its diff — that diff is the supply chain.
