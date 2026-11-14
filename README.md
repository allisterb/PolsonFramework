# Polson

Polson is a framework for agentic co-creative visual art and graphic design, built on the principles of [Enactive AI](https://www.enactive-ai.com/#framework).

![](https://imgur.com/kMxGLm0.png)

![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/trailer5.webp)

![](https://ajb.nyc3.cdn.digitaloceanspaces.com/polson/cocreative.png)
Polson runs in three modes:

| Mode | Orchestrator | Interface | Python environment |
| :--- | :--- | :--- | :--- |
| **Managed** | An agent harness you already use: either  Claude Code or Google Antigravity | The agent harness interface and the studio local web interface. | `python/`, base lock |
| **Terminal** | Polson, through the Google Antigravity SDK | Nothing. One turn, answer in the terminal. | `python/`, driver lock |
| **Graphics Studio** | Polson, through Google ADK | `adk_agent` web interface + ADK console | `python-adk/` |

The screenshot below show Polson running in Claude Desktop:
![](https://imgur.com/R1xnUWJ.png)

Managed mode allows you to use your existing agent subscription while terminal and graphics studio mode require API usage and access key.

[**Setup**](#setup) is the same whichever you use, bar which Python environment you install.

---

## Requirements


### Required
| | |
| :--- | :--- |
| .NET 10 SDK | Builds the drawing engine and MCP server. |
| Python 3.13 | Runs the studio, the orchestrator and the ADK agent. Not needed to build just the MCP server drawing tools. |
| A Google Agent Platform API key | The Graphics Studio's agent model (Gemini), the read-back that checks finished pieces, narration, and generated materials. Managed mode can run without it, but loses those. |

### Optional

Each of these switches a capability on; without it, the capability says so at startup and refuses when asked, rather than failing silently.

| | |
| :--- | :--- |
| A [Qloo](https://www.qloo.com/) API key | Only for the `_qloo` workflows (`poster_qloo`, `motion_qloo`): audience evidence from measured taste — profiles, contrasts, the pace test and visual research. Qloo's API is by contract; a hackathon key works only against `https://hackathon.api.qloo.com`, the default. Every other workflow runs without it. |
| A [Parallel](https://parallel.ai) API key | Research with a citation per field (an infographic's figures, a film's credits, facts outside a filing), and the web check on stated facts in a read-back. |
| An [NVIDIA NIM](https://build.nvidia.com) or [Nebius Token Factory](https://tokenfactory.nebius.com) key | Nemotron 3.5 Super VL, which reads supplied PDFs with every figure checked against its page. Without either, documents are read by Gemini, unchecked. |
| `ffmpeg` on PATH | Films as MP4, and narration joined to them. Set `Tools:FFmpeg` if it is elsewhere. |
| `potrace` on PATH | Vector tracing of bitmaps with `bitmap.trace(...)`. `apt-get install potrace`, or set `Tools:Potrace`. |

The container image ([`Dockerfile`](Dockerfile)) installs everything a deployed studio needs, typefaces included, and is the complete reference for system packages.



## Setup

Once per checkout, step 1 and 2 are required.

### 1. Build the engine

```bash
git clone https://github.com/allisterb/Polson
cd Polson
```

```bash
[./]build
```

That produces **`bin/cli`**, a Release build of the MCP server every project's wiring points at. Only a Release build is copied there: a Debug build or a test run leaves it as it was, and says so, so run `build` again after changing the code.

### 2. Configure it

Copy the example config into the built output and edit it:

```bash
cp ./appsettings.json.example ./bin/cli/appsettings.json
```

This is the only place a key lives — every mode reads this one file, so no two of them can disagree about which key. It is gitignored, and a rebuild does not overwrite it.

**Keys:**

| Setting | What it enables |
| :--- | :--- |
| `ApiKeys:GoogleAgentPlatform` | Gemini: the studio's agent, the read-back (`Verify`), narration (`Assets.speak`) and generated materials. Also read by the agent runtime. |
| `ApiKeys:Qloo` | The `Taste` tool and its audience evidence. |
| `ApiKeys:Parallel` | `Research`, and the web check on stated facts. |
| `ApiKeys:NvidiaNIM` or `ApiKeys:NebiusTokenFactory` | Nemotron for reading documents and, if chosen, films. Token Factory also needs `NebiusTokenFactory:BaseUrl` (its host is regional); with both, `Nemotron:Prefer` (`nim` or `nebius`) picks the first to try. |

An empty key disables its capability; the server says which at startup, and a script asking for it gets a refusal it can read.

**Limits and choices** (all optional):

| Setting | Default | What it does |
| :--- | :--- | :--- |
| `Server:DefaultTimeoutSeconds` | `30` | How long one `ExecuteScript` may run. |
| `Assets:Budget` | `120` | Image generations allowed **per server run**: a hard ceiling, so a stuck retry loop cannot spend without bound. |
| `Assets:Model` · `Assets:SpeechModel` | `gemini-2.5-flash-image` · `gemini-3.8-flash-lite-tts` | The models for generated materials and for narration. |
| `Documents:Budget` | — | Document reads allowed per run. Listing and `Documents.find` are always free. |
| `Research:Processor` · `Research:Budget` | `base` · `2` | Parallel's tier for research (`core` is the deployed choice, about $0.025 a run), and runs allowed per session. |
| `Qloo:Enabled` | `true` | `false` turns Qloo off with the key left in place: the engine refuses `Taste`, and the studio hides the `_qloo` workflows and their "for audiences" cards. Anything but `true` or `false` is off. Restart the studio after changing it. |
| `Qloo:Budget` · `Qloo:MonthlyReserve` | `120` · `1000` | Qloo requests per session, and how much of the key's monthly quota is never spent. |
| `Verify:Reader` | `gemini` | Who reads finished pieces back: `gemini` (Gemini 3.8 Flash) or `nemotron`. Also `POLSON_VERIFY_READER`. |

In a container every setting can come from an environment variable instead (`POLSON_QLOO_KEY` for `ApiKeys:Qloo`, `POLSON_RESEARCH_PROCESSOR` for `Research:Processor`, and so on): [`docker-entrypoint.sh`](docker-entrypoint.sh) holds the full map.

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
python3.13 -m venv python        # Managed and Terminal
python3.13 -m venv python-adk    # Graphics Studio
```

> **Name the version, not `python3`.** Where `python3` is older — Ubuntu 22.04's is 3.10 — `python3 -m venv` builds the environment with that one silently, and the failure surfaces later and further away, as pip refusing a pinned package. On Debian and Ubuntu install `python3.13-venv` too, or `venv` fails on a missing `ensurepip`.

`requirements.txt` pins every package — transitive ones included — to an exact version and hash, and the install refuses anything that does not match.

> **The two environments are separate on purpose, and the names matter.** ADK caps `websockets<16` where the Antigravity SDK has no upper bound, so in one shared environment the second install silently wins and neither lock then describes what is actually there. See [`src/adk_agent/README.md`](src/adk_agent/README.md).

### 4. Install additional tools and typefaces

```bash
py -3.13 tools/bootstrap.py      # python3.13 on Linux and macOS
```


---

## Running 

Each mode read and writes the same project directories

### Mode 1 — Managed: your own agent host drives

Polson generates the project; your host drives the agent — and pays for the model, which is why this is the cheapest way in.

```bash
[./]polson create-project projects roaster claude --workflow logo --type modern
```

The `sdk` argument decides what the config files are called — `agy` for Google Antigravity, `claude` for Claude Code:
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


#### Watching it happen

The studio's pages work over a project whoever is driving it — the MCP server writes the record regardless of which host called it, so this only puts a reader on it. Nothing is started and nothing is spent.

From the repo root:

```bash
[./]polson studio ../PolsonRuns
```

It serves `http://localhost:8010/studio/home` over a directory of projects, which is required and must exist.
- **Client pages:** make a request, follow it, answer the studio's questions, and read what the agent thought and
  did.
- **Developer view** at `/studio/`: the live trace, the renders beside the scripts that made them, and the creative
  sense-making curve, the things `polson report` can only summarise afterwards.

It watches every run in the folder, whoever is driving it, and starts ADK runs from its composer.
- **Options:** `--port` and `--host`; `[./]polson help studio` prints them.
- **Equivalents:** `tools\adk-studio.cmd ../PolsonRuns` and `src/adk_agent/serve.py ../PolsonRuns` do the same.
- **Stopping:** `tools\adk-studio-stop.cmd [port]` stops it. It finds the studio by its port and leaves anything
  else on that port alone.


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

Polson runs the agent on Google ADK and serves the whole studio — the request form, the run pages and ADK's own console.

```bash
[./]polson studio ../PolsonRuns
```

The folder is where projects are written; it must exist, and it is independent of the repo. The verb runs `src/adk_agent/serve.py` on the `python-adk/` interpreter (`--port`, default 8010, and `--host`). Then open:

| | |
| :--- | :--- |
| `http://127.0.0.1:8010/studio/home` | **Start here.** Pick a workflow, write the brief, add audiences and documents, choose sizes and formats. Each request's page then follows the run live: the agent's activity and questions, its evidence (with Qloo on, the Audiences, Contrast and Choices views), the verification, and the deliverables. |
| `http://127.0.0.1:8010/studio/` | The developer view: the live trace, the renders beside the scripts that made them, the sense-making curve. |
| `http://127.0.0.1:8010/dev-ui/` | Google ADK's own console: sessions, tool calls and artifacts as ADK reports them. |

Environment variables, all optional:

| | Default | |
| :--- | :--- | :--- |
| `POLSON_MODEL` | `gemini-3.8-flash` | The agent's model. |
| `POLSON_BUDGET_TOKENS` | `2000000` | Billable input-token cap for one run, before a workflow's own multiple (a narrated report gets four times it). A circuit breaker stops the run at the cap plus a grace period. `0` means uncapped. |
| `POLSON_BUDGET_MINUTES` | — | Wall-clock deadline where the workflow sets none. Each workflow has its own, and more audiences add time. |
| `POLSON_REQUEST_TIMEOUT_SECONDS` | `180` | How long one model call may take before it is retried. The deployed studio uses `300`: late in a long run, turns take over 100 s. Time lost to retries is credited back to the run's clock. |
| `POLSON_ASK_SECONDS` | `120` | How long the agent waits when it asks you a question. |
| `POLSON_MAX_RUNS_PER_DAY` · `POLSON_MAX_CONCURRENT_RUNS` | `25` · `2` | Brakes for a public studio. |
| `POLSON_VERIFY_READER` | `gemini` | Who reads finished pieces back (`gemini` or `nemotron`). |

The deployment adds the variables that only matter on Cloud Run (the keepalive's own URL, the bucket mirror, the artifact service); [`deploy/README.md`](deploy/README.md) lists them with the full deploy command.

**Think before binding to `0.0.0.0`.** A public URL is an open cost surface, and visitor-typed text reaches an agent holding tools.

---

## Workflows

A workflow decides the stages the agent works through and the instructions it is given. A type narrows it. **Audiences** are places with a language and an optional age band (*Tokyo, in Japanese*): where a workflow takes them, it makes one piece per audience as one campaign — one design, with each audience's language, numerals and conventions, every translated line checked. Only the `_qloo` workflows go further and design each audience's piece from its measured taste.

| Workflow | Types | Audiences | What it makes |
| :--- | :--- | :--- | :--- |
| `poster` | `film`, `product` | optional, 1–6 | A film poster or a print ad, designed from the brief, your documents and research on the film or product. |
| `motion` | — (film trailer), `product`, `pitch`, `report`, `report.s1`, `report.10k` | optional | Motion graphics as MP4 and WebP, narrated if asked: a film trailer, a product ad, a startup pitch, or an explainer for a financial document (an S-1, a 10-K) with every figure checked against its page. |
| `poster_qloo`, `motion_qloo` | as above | required for a poster, trailer or product ad | The two above as built for the Qloo hackathon: each audience's image, palette and pace decided by its Qloo taste profile. Need `ApiKeys:Qloo` and `Qloo:Enabled`. |
| `vector_infographic` | `blueprint`, `brutalist`, `editorial`, `specimen`, `swiss` | optional | A data graphic as **editable SVG**, from sourced and cited figures; with audiences, a version in each one's language, numbers written its way. |
| `infographic` | same | optional | The same, delivered as raster. |
| `logo` | `antique`, `geometric`, `modern` | — | A mark and its identity: the mark as SVG and PNG, brand sheet, scale ladder to 16 px, monochrome test, kit of parts and mock-ups. Every word on the mark is checked against the brief. |
| `cover` | — | — | A book cover, composed as one image. |
| `math_explainer` | — | — | A short animated explanation of a theorem or concept, with every equation checked. |
| `animation` | — | — | Character animation: a cartoon figure moving through its beats. |
| `storyboard`, `storyboard_cast`, `storyboard_quick` | — | — | Storyboards: who is where, facing which way, how close the camera is. |
| `sketch_scene` | — | — | A posed figure scene sketched from a sentence. |
| `drawing` | `review`, `seed` | — | A drawing made turn by turn with you, in pencil and pen. The one that stops and asks. `seed` opens from a sketch of yours in `seed/`. |
| `comic` | `review`, `seed` | — | A comic panel through pencil, colour, ink and self-critique. |
| `comic_studio` | — | — | A comic page. Multi-agent: penciler, colorist, inker, critic — colour before ink, deliberately. |
| `painting` | — | — | A painted scene: surfaces requisitioned from a cloud model, form drawn by the toolkit. |

`harness` is the framework's own evaluation workflow, not a commission. In the studio, **"Decide everything"** runs any of these without questions: the agent takes the answer it would have proposed, and lists each decision for review afterwards.

**Attach a document and the agent will read it.** Anything you upload lands in the project's `documents/` folder. `Documents.list()` and `Documents.find(path, phrase)` are free; `Documents.ask(path, question)` reads the pages asked about with Nemotron 3.5 Super VL and checks every figure in its answer against the page's own text. Without a Nemotron key it falls back to Gemini, which reads scanned PDFs too, but its figures are then unchecked.

---


## Licence

[AGPL-3.0](LICENSE).
