# Polson — the ADK agent runtime

A second Google agent runtime for the studio, alongside the Antigravity SDK. This is the deliverable
for the **Agentic Cinema** hackathon (Parallel track); the feasibility brief and the verification
behind every claim here are in [`docs/agentic-cinema-assessment.md`](../../docs/agentic-cinema-assessment.md).

**Status: runs locally, not containerised.** One app per project, created dynamically, single- and
multi-agent both working end to end against the real engine. The Dockerfile and the `src/webapp` UI
mount are not written yet.

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
