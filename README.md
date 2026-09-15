# Polson

Polson is a framework for agentic co-creative visual art and graphic design, built on the principles of [Enactive AI](https://computationalcreativity.net/iccc24/papers/ICCC24_paper_58.pdf).

An agent does not describe a picture and hand you the result. It writes JavaScript that draws, renders it, **looks at what came out**, and revises — through a .NET MCP server exposing a Snap.svg-compatible vector API, an HTML5-compatible 2D canvas, Skia shaders, filters and path effects, and constructive drawing, logo, typography and layout toolkits built on them. Every script it ran, every render it made and every intent it declared is written down as it happens, so a run can be watched live and read back afterwards.

There are two ways to work with it:

- **Polson Graphics Studio** — the whole studio in a browser. Polson hosts the agent itself, on Google ADK, and you direct it from a web page. This is what the sections below set up.
- **Managed agent** — Polson generates a project directory for an agent host you already use (Google Antigravity, Claude Code) and that host drives.

---

## Requirements

| | |
| :--- | :--- |
| .NET 10 SDK | Builds the drawing engine and MCP server. |
| Python 3.13 | Runs the ADK agent and the web interface. |
| A Google Agent Platform API key | Required. Without it the agent cannot run. |
| A [Parallel](https://parallel.ai) API key | Optional. Without it **research is silently disabled** — figures cannot be sourced. |
| `potrace` on PATH | Optional. Only `bitmap.trace(...)` needs it. `apt-get install potrace`, or set `Tools:Potrace`. |

---

## 1. Build the engine

```bash
git clone https://github.com/allisterb/Polson
cd Polson
```

```bash
[./]build
```

That produces **`bin/cli`**, which is the MCP server every project's wiring points at.

## 2. Configure it

Copy the example config into the built output and edit it:

```bash
cp ./appsettings.json.example ./bin/cli/appsettings.json
```

This is the only place a key lives — both halves of the studio read it, so they cannot disagree about which key. It is gitignored, and a rebuild does not overwrite it.

| Setting | Default | What it does |
| :--- | :--- | :--- |
| `ApiKeys:GoogleAgentPlatform` | — | The Agent Platform key. Also read by the agent runtime. |
| `ApiKeys:Parallel` | — | The research key. **Not in the example file — add it under `ApiKeys` yourself.** Empty means `Research` refuses in a way a script can read, and says so at startup. |
| `Assets:Budget` | `120` | Image generations allowed **per server run** — a hard ceiling, so a stuck retry loop cannot spend without bound. |
| `Documents:Budget` | — | Document reads allowed per run. Listing is always free. |
| `Server:DefaultTimeoutSeconds` | `30` | How long one `ExecuteScript` may run. |

## 3. Install the Python environment

Run from the repository root. **Nothing installs itself** — package installation is always a deliberate act by a person.

Windows:

```bash
python -m venv python-adk
src\adk_agent\install.cmd
```

Linux or macOS:

```bash
python3.13 -m venv python-adk
src/adk_agent/install.sh
```

> **Name the version, not `python3`.** Where `python3` is older — Ubuntu 22.04's is 3.10 — `python3 -m venv` builds the environment with that one silently, and the failure surfaces later and further away, as pip refusing a pinned package. On Debian and Ubuntu install `python3.13-venv` too, or `venv` fails on a missing `ensurepip`.

`requirements.txt` pins every package — transitive ones included — to an exact version and hash, and the install refuses anything that does not match.

> The venv is `python-adk/` and the name matters. ADK caps `websockets<16` where the Antigravity SDK has no upper bound, so a shared environment would let one install silently win and leave neither lock describing what is actually there. See [`src/adk_agent/README.md`](src/adk_agent/README.md).

## 4. Run it

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

## Managed agent interface

Polson generates the project; your host drives the agent.

```bash
[./]polson create-project projects roaster agy --workflow logo --type modern
```

The `sdk` argument decides what the config files are called — `agy` for Google Antigravity, `claude` for Claude Code:

| | `agy` | `claude` |
| :--- | :--- | :--- |
| Instructions | `GEMINI.md` | `CLAUDE.md` |
| MCP wiring | `.agents/mcp_config.json` | `.mcp.json` |
| Tool policy | `.agents/settings.json` | `.claude/settings.local.json` |

Add `--prompt "..."` to write the brief in a line, or `--brief <file>` to supply it from a file. Either is treated as untrusted data and normalised before it is written.

Then fill in `brief.md`, open the directory with your agent host, and tell it to begin. Everything between the `BRIEF-BEGIN` and `BRIEF-END` markers is read as a client's statement, never as instructions to the agent.

**The host owns tool policy while the host is running it.** Polson writes the permissions file your host expects but cannot enforce it or check that it was honoured — which is why image generation is denied there *and* stated again as a rule in the instructions.

### Watching a managed run

The studio's pages work over a project whoever is driving it — the MCP server writes the record regardless of which host called it, so this only puts a reader on it. Nothing is started and nothing is spent.

That needs the **second** virtual environment, `python/`, which is separate from the ADK one for the reason given above:

```bash
python -m venv python
src\studio\install.cmd          # src/studio/install.sh on Linux or macOS
```

Then, from `src/`:

```bash
python -m studio ../projects --observe-only
```

It serves `http://127.0.0.1:8000` over a directory of projects, and gives you the live trace, the renders beside the scripts that made them, and the creative sense-making curve — the things `polson report` can only summarise afterwards.

**That install deliberately carries no agent SDK.** `studio/` has no reference to one and the driver is imported lazily, so serving the pages and reading a record need none of it — 17 packages rather than 52. If you also want the page's Start button, or to drive a turn from the terminal, install the driver lock instead:

```bash
src\studio\install.cmd driver   # ./install.sh driver on Linux or macOS
```

`--observe-only` then becomes optional, and one turn can be driven from the terminal with you answering the agent's questions:

```bash
python -m orchestrator ../projects/acme
```

**Think before binding beyond `127.0.0.1`.** A reachable studio is an open cost surface, and visitor-typed text reaches an agent holding tools.

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

The studio runs as one container: the ADK runtime, the .NET engine, the SDK docs and the web interface. See [`DEPLOY.md`](DEPLOY.md) for the Cloud Run path, and [`src/adk_agent/README.md`](src/adk_agent/README.md) for what the image contains and why.

## Licence

[AGPL-3.0](LICENSE).
