# Polson

## About
Polson is a framework for agentic co-creative visual art and graphic design collaboration, built on the principles of [Enactive AI](https://computationalcreativity.net/iccc24/papers/ICCC24_paper_58.pdf).

An agent does not describe a picture and hand you the result. It writes JavaScript that draws, renders it, **looks at what came out**, and revises — through a .NET MCP server exposing a Snap.svg-compatible vector API, an HTML5-compatible 2D canvas, Skia shaders, filters and path effects, and constructive drawing, logo, typography and layout toolkits built on them. Every script it ran, every render it made and every intent it declared is written down as it happens, so a run can be watched live and read back afterwards.

There are two ways to work with it. The **standalone web interface** is the whole studio — Polson hosts the agent itself and you direct it from a browser. The **managed agent interface** generates a project directory for an agent host you already use (Google Antigravity, Claude Code) and lets that host drive.

## Requirements
* .NET 10 SDK
* Python 3.13 — for the standalone web interface only
* An API key for Google Agent Platform


## Getting started

0. Get the source: `git clone https://github.com/allisterb/Polson`.

1. Build Polson by running the build script from the repo folder: `[./]build`. This produces `bin/cli`, which is what every project's MCP wiring points at.

2. Copy appsettings.json.example from the repo root to `./bin/cli/appsettings.json` (e.g. `cp ./appsettings.json.example ./bin/cli/appsettings.json`).
   Edit the file and set your Google Agent Platform API key and any other preferred settings.
   This is the only place the key lives — the MCP server and the orchestrator both read it, so the two halves of the studio cannot disagree about which key. It is gitignored, and a rebuild does not overwrite it.

   | Setting | Default | What it does |
   | :--- | :--- | :--- |
   | `ApiKeys:GoogleAgentPlatform` | — | The Agent Platform key. Leave it empty and asset requisition is disabled: `Assets.*` refuses in a way a script can read, and the server says so at startup. |
   | `Assets:Model` | `gemini-2.5-flash-image` | The image model requisition calls. |
   | `Assets:Budget` | `120` | **Generations allowed per server run** — a hard ceiling, so a stuck retry loop cannot spend without bound. It is per *run*, not per day. A painting is built from many surfaces, which is why the default is not small; lower it if you are paying per image and want a tighter leash, and note that a value that is not a positive whole number is warned about and ignored rather than silently becoming zero. Cache hits and refusals cost nothing against it. |
   | `Assets:CacheDir` | `<project>/.polson/assets` | Where requisitions are cached by content. A repeated requisition is free and instant. |
   | `Server:DefaultTimeoutSeconds` | `30` | How long one `ExecuteScript` may run. |

3. Make a directory for your projects (e.g. `mkdir projects`). Projects hold everything a run produces and are independent of the repo; keep it outside the checkout, or add it to `.gitignore`.

### Workflows

Both interfaces start from a workflow, which decides the stages the agent works through and the instructions it is given. A type narrows the visual direction.

| Workflow | Types | What it makes |
| :--- | :--- | :--- |
| `logo` | `antique`, `geometric`, `modern` | A logo or wordmark, with scale and monochrome stress tests. |
| `infographic` | `blueprint`, `brutalist`, `editorial`, `specimen`, `swiss` | A data graphic, from the numbers in the brief. |
| `drawing` | `review`, `seed` | A drawing made turn by turn with you, in pencil and pen. No colour. `seed` opens from a sketch of yours in `seed/`. |
| `comic` | `review`, `seed` | A comic panel by one agent through pencil, colour, ink and self-critique. `seed` reproduces a reference in `reference_images/`. |
| `painting` | — | A painted scene. The only workflow that requisitions material — surfaces from a cloud model, form drawn by the toolkit. |
| `comic_studio` | — | A comic page. Multi-agent: penciler, colorist, inker, critic — colour before ink, deliberately. |
| `harness` | `image`, `infographic`, `logo` | A test harness rather than a deliverable — for exercising the toolkit. |

## Standalone web interface

Polson hosts the agent itself and you direct it from a browser. Nothing here needs an IDE, an agent host, or a checkout of anything but this repository.

### One-time Python setup

Run from the repository root. Nothing installs itself — per the project guardrails, package installation is always a deliberate act by a person.

Two steps. Make the environment, then run the install script — it copies pip's settings into the venv itself, under whichever name pip reads on your platform.

On Windows:

```bash
python -m venv python
src\webapp\install.cmd
```

On Linux or macOS the venv puts executables in `bin/` rather than `Scripts/`, which the script handles:

```bash
python3.13 -m venv python
src/webapp/install.sh
```

**Name the version, not `python3`.** On a distribution whose `python3` is older — Ubuntu 22.04's is 3.10 — `python3 -m venv` builds the environment with that one silently, and the failure surfaces later and further away, as pip refusing a pinned package that requires a newer Python. Where 3.13 is not the default it is usually installed alongside rather than over the system one (`ppa:deadsnakes/ppa` on older Ubuntu), which leaves `python3` untouched by design. Install `python3.13-venv` with it — Debian and Ubuntu split `venv` into its own package, and without it `python3.13 -m venv` fails on missing `ensurepip`.

`requirements.txt` is committed with every package — transitive ones included — pinned to an exact version and hash, and the install refuses anything that does not match. See [`src/webapp/README.md`](src/webapp/README.md) for how to change a dependency and what to review when you do.

### Running it

```bash
[./]polson_webapp projects
```

The argument is the directory that **holds** projects, not a project. It serves `http://127.0.0.1:8000`.

The front page has a brief form — a project name, a workflow, a type, and the brief itself: what you want made and anything it must respect. *Create and start* generates the project and begins a run; *Create only* leaves it for later. Projects already in the directory are listed below the form and can be started or continued from there. Nothing starts an agent until you press a button.

Projects made elsewhere show up here too, and a project generated for a desktop host can be started from this page without being regenerated — every Antigravity project carries what the orchestrator needs, whichever host it was made for. Only a project with no tool policy at all is greyed out, with the reason.

### The run page

The run page is the studio. It shows four things at once, all fed by one event stream that replays from the beginning, so a refresh loses nothing and you can open a finished run and read it back the same way.

* **The creative sense-making curve** — the run coded as an interaction trajectory, after Davis: rising while the agent regulates (communicating, gathering, inspecting), falling while it produces. Read it *by action* or *by time held*; the two disagree in sign on real runs, which is why both are offered. Clicking a point opens the pass it came from.
* **The trace** — every stage the agent declared, every note it left, every script it ran, every render it made, and what it looked at before drawing. Thinking is folded to one line; click to open it.
* **The render**, updating as work lands. Click any pass in the trace to pin it and see that render together with the script that produced it; *follow the latest* releases the pin.
* **The script**, syntax-highlighted, beside the render it made.

Two things go the other way, from you to the agent:

* **Questions.** When the agent needs a decision that is yours — and its instructions tell it to ask rather than invent one — the question appears at the top of the trace with its options. Answer it, say something else, or let it decide.
* **Interjections.** The *Say something to the agent* box interrupts a turn already under way. This is a contribution, not a correction, and the agent makes sense of it against the work in progress rather than executing it. Asked mid-run for a green background instead of yellow, it did not swap a hex value: it reopened the design language as **green-bar line-printer paper** — the real alternating-band continuous-feed stock, historically right for the printout the piece was imitating — and the bands appeared in the next render.

**One run at a time, and a daily cap.** Each run is a billed agent session held open in the server process, so a second start is refused with a message rather than queued. The default bind is `127.0.0.1`; `--host` and `--port` change it. Think before exposing it — a public URL is an open cost surface, and visitor-typed text reaches an agent holding tools.

### The same thing in a terminal

```bash
[./]polson_run projects/acme
```

One turn against **one** project, with you as the director: the agent's questions are printed and your reply typed back. Useful flags are `--prompt` to say something other than "read your instructions and begin", `--fresh` to ignore the saved conversation, and `--unattended` to answer nothing. A run is resumable either way — the conversation id is recorded in `project.json` and the next run continues where it left off.

## Managed agent interface
Polson can also be run inside Google Antigravity CLI or Desktop or IDE, or Claude Code. Polson generates the project; your host drives the agent.

1. Generate a project. The `sdk` argument is required and decides what the config files are called — `agy` for Google Antigravity, `claude` for Claude Code:

   ```bash
   [./]polson create-project projects roaster agy --workflow logo --type modern
   ```

   | | `agy` | `claude` |
   | :--- | :--- | :--- |
   | Instructions | `GEMINI.md` | `CLAUDE.md` |
   | MCP wiring | `.agents/mcp_config.json` | `.mcp.json` |
   | Tool policy | `.agents/settings.json` | `.claude/settings.local.json` |

   Add `--prompt "..."` to write the brief in a line, or `--brief <file>` to supply it from a file. Either is treated as untrusted data and normalised before it is written.

2. Fill in `brief.md` — what you want made, and anything it must respect. Everything between the `BRIEF-BEGIN` and `BRIEF-END` markers is data the agent reads as a client's statement, never as instructions to it.

3. Open the project directory with your agent host and tell it to begin. It reads its own instructions file, works through the workflow's stages, and draws through the Polson MCP server, which the generated wiring already points at your `bin/cli`.

**The host owns tool policy while the host is running it.** That is what "managed" means: Polson writes the permissions file your host expects, but cannot enforce it or check that it was honoured. Image generation is denied there and stated again as a rule in the instructions, because an entry a host does not recognise is silently inert and indistinguishable from one being enforced.

**The profile is a label, not a gate.** Every Antigravity project — however it was generated — carries what the orchestrator needs, so you can run the same directory from the browser or the terminal above and Polson will own the policy for that run. `--standalone` only changes which way the project is set up for; it does not close the other door. Nothing needs regenerating to switch.

## Reading a run back

Everything a run produces stays in the project directory: renders in `artifacts/`, every executed script in `scripts/`, and the record in `events/`. [`docs/project-layout.md`](docs/project-layout.md) is the contract for all of it.

```bash
[./]polson report projects/acme
```

```
    events recorded        344
    server sessions        6
    scripts executed       33
    scripts failed         0
    renders recorded       33
    stage notes            160
    looked before drawing  175 probes across 13 of 33 scripts
    artifacts read back    none - no pass built on an earlier pass's render
    stages declared        32 across 6 sessions - listed below
    files in scripts/      33
    files in artifacts/    17
    conversation record    events/agent.jsonl - 6 turns, 191 steps

  Sessions

    1  2026-08-31 04:18:35Z    2m 28s    4 scripts, 4 renders, 19 notes
       Data -> Forms -> Composition -> Encode

    2  2026-08-31 04:58:59Z    4m 54s    7 scripts, 7 renders, 40 notes
       Data -> Forms -> Composition -> Encode -> Palette & Type -> Detail -> Audit

    3  2026-08-31 06:53:56Z    27.8s     0 scripts, 0 renders, 0 notes
       (no stage declared)

    ...

    Nothing unaccounted for: every artifact traces to a render, every script to an execution.
```

A project's event log is appended to across runs, so the report splits it at each server session. The totals are the project's — an artifact with no render is unaccounted for whenever it was written — while the shape of the work belongs to the session it happened in. Session 3 above is what the split is for: a server that connected, ran for 28 seconds and did nothing, invisible inside the totals until it had a row of its own.

The report is the machine-checkable half — script paths, durations, artifact paths, byte counts. The stages and notes beside it are the agent's own account of what it was doing, which is what makes a run legible rather than merely logged.

