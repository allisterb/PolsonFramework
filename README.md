# Polson

Polson is a framework for agentic co-creative visual art and graphic design, built on the principles of [Enactive AI](https://computationalcreativity.net/iccc24/papers/ICCC24_paper_58.pdf).

Polson runs in three modes:

| | What drives the agent | What you watch it through | Python environment |
| :--- | :--- | :--- | :--- |
| **Managed** | An agent harness you already use: either Google Antigravity or Claude Code | The studio, **observing**. Nothing is started and nothing is spent. | `python/`, base lock |
| **Terminal** | Polson, through the Google Antigravity SDK | Nothing. One turn, you answering in the terminal. | `python/`, driver lock |
| **Graphics Studio** | Polson, through Google ADK | Its own web interface — commission form, run pages, ADK console | `python-adk/` |

**Managed is the cheapest and the least ceremony**: your host's subscription pays for the model, and
Polson supplies the drawing engine, the project, the record and a reader for it. **Graphics Studio is
the whole thing in a browser** and the one a visitor can be handed. **Terminal is the smallest**, for
driving a single turn without a page open.

[**Setup**](#setup) is the same whichever you use, bar which Python environment you install.

---

## Requirements


| | |
| :--- | :--- |
| .NET 10 SDK | Builds the drawing engine and MCP server. |
| Python 3.13 | Runs the studio, the orchestrator and the ADK agent. Not needed to build just the MCP server drawing tools. |
| 

## Optional


| | |
| :--- | :--- |
| A [Parallel](https://parallel.ai) API key | Without it research is silently disabled — infographics figures cannot be sourced. |
| A Google Agent Platform API key | For using a Google Cloud-hosted AI model for material asset generation. |
| `potrace` on PATH | For vector tracing bitmaps with`bitmap.trace(...)`. `apt-get install potrace`, or set `Tools:Potrace`. |



## Setup

Once per checkout. All three modes share §1 and §2; §3 depends on which you want.

### 1. Build the engine

```bash
git clone https://github.com/allisterb/Polson
cd Polson
```

```bash
[./]build
```

That produces **`bin/cli`**, which is the MCP server every project's wiring points at.

### 2. Configure it

Copy the example config into the built output and edit it:

```bash
cp ./appsettings.json.example ./bin/cli/appsettings.json
```

This is the only place a key lives — every mode reads this one file, so no two of them can disagree about which key. It is gitignored, and a rebuild does not overwrite it.

| Setting | Default | What it does |
| :--- | :--- | :--- |
| `ApiKeys:GoogleAgentPlatform` | — | The Agent Platform key. Also read by the agent runtime. |
| `ApiKeys:Parallel` | — | The research key. **Not in the example file — add it under `ApiKeys` yourself.** Empty means `Research` refuses in a way a script can read, and says so at startup. |
| `Assets:Budget` | `120` | Image generations allowed **per server run** — a hard ceiling, so a stuck retry loop cannot spend without bound. |
| `Documents:Budget` | — | Document reads allowed per run. Listing is always free. |
| `Server:DefaultTimeoutSeconds` | `30` | How long one `ExecuteScript` may run. |

### 3. Install the Python environment your mode needs

Run from the repository root. **Nothing installs itself** — package installation is always a deliberate act by a person.

There are **two virtual environments and three locks**, and which you need follows from the mode:

| Mode | Environment | Install | What it carries |
| :--- | :--- | :--- | :--- |
| Managed | `python/` | `src\studio\install.cmd` | 17 packages — the studio, and no agent SDK |
| Terminal | `python/` | `src\studio\install.cmd driver` | 52 — the same, plus the Antigravity SDK |
| Graphics Studio | `python-adk/` | `src\adk_agent\install.cmd` | the ADK runtime |

On Linux and macOS each is `install.sh`, taking the same optional `driver` argument. Create the environment first — `python/` for the first two modes, `python-adk/` for the third:

```bash
python3.13 -m venv python
```

> **Name the version, not `python3`.** Where `python3` is older — Ubuntu 22.04's is 3.10 — `python3 -m venv` builds the environment with that one silently, and the failure surfaces later and further away, as pip refusing a pinned package. On Debian and Ubuntu install `python3.13-venv` too, or `venv` fails on a missing `ensurepip`.

`requirements.txt` pins every package — transitive ones included — to an exact version and hash, and the install refuses anything that does not match.

> **The two environments are separate on purpose, and the names matter.** ADK caps `websockets<16` where the Antigravity SDK has no upper bound, so in one shared environment the second install silently wins and neither lock then describes what is actually there. See [`src/adk_agent/README.md`](src/adk_agent/README.md).

---

## Running it

Three modes, and nothing to choose between them permanently — they read and write the
same projects, so the same directory can be driven by your own host today and by Polson
tomorrow.

### Mode 1 — Managed: your own agent host drives

Polson generates the project; your host drives the agent — and pays for the model, which is why this is the cheapest way in.

```bash
[./]polson create-project projects roaster agy --workflow logo --type modern
```

> `[./]polson` is `./polson` on Linux and macOS and `.\polson.ps1` on Windows. Both dispatch the
> verb — .NET for `create-project`, `reset`, `eval`, `report`, `server` and `version`; Python for
> `studio` and `orchestrator` — and both pass the exit code through. `polson.cmd` is gone: cmd cannot do the
> branching, and it reported success whatever the CLI actually returned.

The `sdk` argument decides what the config files are called — `agy` for Google Antigravity, `claude` for Claude Code:

| | `agy` | `claude` |
| :--- | :--- | :--- |
| Instructions | `GEMINI.md` | `CLAUDE.md` |
| MCP wiring | `.agents/mcp_config.json` | `.mcp.json` |
| Tool policy | `.agents/settings.json` | `.claude/settings.local.json` |

Add `--prompt "..."` to write the brief in a line, or `--brief <file>` to supply it from a file. Either is treated as untrusted data and normalised before it is written.

Then fill in `brief.md`, open the directory with your agent host, and tell it to begin. Everything between the `BRIEF-BEGIN` and `BRIEF-END` markers is read as a client's statement, never as instructions to the agent.

**The host owns tool policy while the host is running it.** Polson writes the permissions file your host expects but cannot enforce it or check that it was honoured — which is why image generation is denied there *and* stated again as a rule in the instructions.

#### Running a project again

```bash
[./]polson reset projects/roaster
```

Clears the previous run and leaves everything else exactly as it is — the brief, the instructions
**including your own edits to them**, the MCP wiring, the manifest. It takes the project's own path,
so there is nothing to restate. `--delete` removes the run instead of archiving it, and is the only
irreversible thing here.

**Use `create-project --reset` instead when you want the project rebuilt as well**, which is the
case after the templates have changed:

```bash
[./]polson create-project projects roaster claude --reset
```

That regenerates every generated file — so a hand-edited `CLAUDE.md` goes back to the template — and
needs the parent directory, the id and the sdk again. Both routes clear the run identically.

Either way it **archives rather than deletes**: `events/`, `scripts/`, `artifacts/` and the files the
agent wrote about itself (`findings.md`, `critique_log.md`, `artwork.js`, `output.*`) move to
`previous/<timestamp>/` with a README saying what they are. That default was paid for — an earlier
version deleted, and took with it the only measurements saying whether a change had been worth
making.

Two things stay put: **`brief.md`**, the one file a person authors, and **`.polson/`**, so a reset of
the *work* is not a reset of the *spend* — the research and asset caches survive.

`create-project --reset` keeps what the project *is*, too. The workflow, type and profile recorded in
`project.json` are inherited when you do not restate them, so it cannot quietly turn a `drawing`
project into a `logo` one; naming `--workflow` still changes it, because that is someone asking.
`--force` is the blunter neighbour: it regenerates every generated file **including `brief.md`**,
which has cost a hand-written brief before.

#### Watching it happen

The studio's pages work over a project whoever is driving it — the MCP server writes the record regardless of which host called it, so this only puts a reader on it. Nothing is started and nothing is spent.

With the base lock installed (§3), from the repo root:

```bash
[./]polson studio projects --observe-only
```

`studio` is the one verb the launcher sends to Python rather than to the .NET CLI; everything after
it is forwarded verbatim, so `--host`, `--port` and `--observe-only` are the module's own options and
`[./]polson help studio` prints them. Running it directly still works and is exactly equivalent, from
`src/`: `python -m studio ../projects --observe-only`.

It serves `http://127.0.0.1:8000` over a directory of projects, and gives you the live trace, the renders beside the scripts that made them, and the creative sense-making curve — the things `polson report` can only summarise afterwards.

**The base lock deliberately carries no agent SDK.** `studio/` has no reference to one and the driver is imported lazily, so serving the pages and reading a record need none of it — 17 packages rather than 52. Install the driver lock instead if you also want the page's **Start** button, which turns this from observing into mode 2 with a page attached.

**Think before binding beyond `127.0.0.1`.** A reachable studio is an open cost surface, and visitor-typed text reaches an agent holding tools.

---

### Mode 2 — Terminal: one turn, no interface

With the **driver** lock installed, Polson hosts the agent itself through the Google Antigravity SDK and you direct it from the terminal:

```bash
[./]polson orchestrator projects/acme
```

No page, no port, nothing to watch — the agent asks its questions in the terminal and you answer. The record is written exactly as in every other mode, so `polson report` and the studio read it afterwards without knowing which mode produced it.

`orchestrator` is the launcher's other Python verb, and the only one that needs the driver lock. Run
it on the base install and it says so before doing anything — naming the interpreter it found, what
is in it, and the one command that fixes it — rather than failing on a `ModuleNotFoundError` from
inside the SDK import. (Equivalent directly, from `src/`: `python -m orchestrator ../projects/acme`.)

---

### Mode 3 — Graphics Studio: Polson hosts the agent, with its own interface

Polson runs the agent on Google ADK and serves the whole studio — the commission form, the run pages and ADK's own console. This is the mode a visitor can be handed, and the one [`DEPLOY.md`](DEPLOY.md) deploys.

```bash
python-adk/Scripts/python.exe -m uvicorn main:app --host 127.0.0.1 --port 8000 --app-dir src/adk_agent
```

On Linux or macOS the interpreter is at `python-adk/bin/python`.

Then open:

| | |
| :--- | :--- |
| `http://127.0.0.1:8000/new` | **Start here.** The commission form: a name, a workflow, a house style, a brief, and a document to attach. |
| `http://127.0.0.1:8000/studio` | Projects, and the run pages — the live record, the renders, the sense-making curve. |
| `http://127.0.0.1:8000/` | Google ADK's own console: sessions, tool calls and artifacts as ADK reports them. |

Projects are written to `projects/` beside the checkout. Set `POLSON_PROJECTS_DIR` to put them elsewhere — they hold everything a run produces and are independent of the repo.

Useful environment variables, all optional:

| | |
| :--- | :--- |
| `POLSON_BUDGET_TOKENS` | Billable input-token cap for one run. A circuit breaker stops the run at the allowance plus a grace period. |
| `POLSON_BUDGET_MINUTES` | Wall-clock deadline, where the workflow does not set its own. |
| `POLSON_MODEL` | Defaults to `gemini-3.7-flash`. |
| `POLSON_ASK_SECONDS` | How long the agent waits when it asks you a question. |

**Think before binding to `0.0.0.0`.** A public URL is an open cost surface, and visitor-typed text reaches an agent holding tools.

---

## Workflows

A workflow decides the stages the agent works through and the instructions it is given. A type narrows the visual direction.

| Workflow | Types | What it makes |
| :--- | :--- | :--- |
| `vector_infographic` | `blueprint`, `brutalist`, `editorial`, `specimen`, `swiss` | A data graphic delivered as **editable SVG**, from sourced and cited figures. |
| `infographic` | same | The same, delivered as raster. |
| `drawing` | `review`, `seed` | A drawing made turn by turn with you, in pencil and pen. The one that stops and asks. `seed` opens from a sketch of yours in `seed/`. |
| `logo` | `antique`, `geometric`, `modern` | A logo or wordmark, with scale and monochrome stress tests. |
| `comic` | `review`, `seed` | A comic panel through pencil, colour, ink and self-critique. |
| `comic_studio` | — | A comic page. Multi-agent: penciler, colorist, inker, critic — colour before ink, deliberately. |
| `painting` | — | A painted scene. The only workflow that requisitions material — surfaces from a cloud model, form drawn by the toolkit. |

**Attach a document and the agent will read it.** Anything you upload lands in the project's `documents/` folder; `Documents.list()` is free and `Documents.ask(path, question)` puts the question to Gemini. It reads scanned PDFs with no text layer, so a screenplay or a scanned report works as a brief.

---

## Reading a run back

Everything a run produces stays in the project directory: renders in `artifacts/`, every executed script in `scripts/`, the record in `events/`, and the basis for any sourced figure in `.polson/research/`. [`docs/project-layout.md`](docs/project-layout.md) is the contract for all of it.

```bash
[./]polson report projects/acme
```

```
    events recorded        344
    scripts executed       33
    scripts failed         0
    renders recorded       33
    stage notes            160
    looked before drawing  175 probes across 13 of 33 scripts
    stages declared        32 across 6 sessions

    Nothing unaccounted for: every artifact traces to a render, every script to an execution.
```

The report is the machine-checkable half — script paths, durations, artifact paths, byte counts. The stages and notes beside it are the agent's own account of what it was doing, which is what makes a run legible rather than merely logged.

---

## Deploying it

**Mode 3 is the one that deploys**, because it is the only one that hosts the agent *and* serves a page — the other two need either your own agent host or your own terminal. It runs as one container: the ADK runtime, the .NET engine, the SDK docs and the web interface. See [`DEPLOY.md`](DEPLOY.md) for the Cloud Run path, and [`src/adk_agent/README.md`](src/adk_agent/README.md) for what the image contains and why.

## Licence

[AGPL-3.0](LICENSE).
