# Polson — the Python side

The orchestrator (Milestone 5) and, later, the demo website (Milestone 6). Python 3.13.

Nothing here installs itself. Per the project guardrails in `CLAUDE.md`, package installation is
always a deliberate act by a person — no script, build step, or agent runs `pip install` on your
behalf. The commands below are for you to run and review.

## Two ways in

```bash
./polson_webapp projects            # the browser, at http://127.0.0.1:8000
./polson_run projects/acme          # one turn in the terminal, you as the director
```

They differ in what the argument means, which is why they are separate scripts rather than one with
a flag. `polson_webapp` takes the directory that **holds** projects: it lists what it finds, and a
brief typed into its form creates another one beside them. `polson_run` takes **one** project and
runs a single turn against it.

Nothing starts an agent until you ask it to — the web app not until a button is pressed.

On Windows use `polson_webapp.cmd` and `polson_run.cmd`.

## Running a project from the terminal

```bash
dotnet bin/cli/Polson.CLI.dll create-project projects acme agy --standalone --prompt "..."
python src/webapp/run_studio.py projects/acme
```

`agy` targets the Google Antigravity SDK, which is the one the orchestrator builds configurations
for. `--standalone` is what adds `agent.config.json` and `session/`; without it you get a project
for a desktop host to run, and the orchestrator refuses it rather than running with no tool policy.

That runs one turn: the agent reads the project's `GEMINI.md`, draws through the Polson MCP server,
and the orchestrator writes down what happened. Everything it produces stays in the project
directory — renders in `artifacts/`, every script in `scripts/`, and three event files in `events/`.
`docs/project-layout.md` is the contract for all of it.

Useful flags: `--prompt` to say something other than "read your instructions and begin",
`--unattended` to answer nothing (questions are recorded and skipped rather than waited on),
`--fresh` to ignore the saved conversation, `--timeout`. Run it from a terminal and you *are* the
director: the agent's questions are printed and your reply goes back to it, recorded in
`events/director.jsonl`. Milestone 6 replaces that terminal with a browser and nothing else.

## Watching a project you are driving somewhere else

The studio does not have to be the thing running the agent. Work a project in Claude Code or Claude
Desktop as you normally would, then press **Watch** on the index — or post its name to `/observe` —
and the same run page opens over it: the sense-making curve, every render beside the script that made
it, and the conversation that produced them.

Nothing extra has to be captured for that to work. The MCP server writes `events/server.jsonl`
whoever is driving it, so the renders, stages, expectations and measurements are already on disk, and
the `preserve-chatlog` hook that `create-project` installs preserves the host's own transcript beside
them. `orchestrator/hostlog.py` transcribes that into `agent.jsonl` and `director.jsonl`, so both
participants appear on one curve.

```bash
dotnet bin/cli/Polson.CLI.dll create-project projects acme claude --prompt "..."
python src/webapp/serve_studio.py projects        # then press Watch
```

No `--standalone`: that flag adds the orchestrator's own tool policy and session directories, and
`create-project` refuses it for a host it does not drive. A Claude project is managed by definition —
the host owns the policy — which is why the row on the index offers Watch and not Start.

It is **read-only, by design rather than by limitation.** Direction happens in the host's interface,
which is what that interface is for; a page offering its own answer box would be offering a way in
that reaches nothing. What it adds is the reading the host cannot give — and a record that outlives
the session.

Two things worth knowing. It shows the **current server session**, cut at the last `run.start`,
because a project's event files are appended to across every session it has ever had. And whether it
calls a run *live* is an inference from the record still moving, deliberately generous: a finished run
may show as live for a few minutes, which is the better way to be wrong.

A run is resumable — the conversation id is written into `project.json`, and the next run continues
where it left off unless you pass `--fresh`.

## Watching a run

The record is three files, and they do not all belong to this process. `agent.jsonl` and
`director.jsonl` are written here; `server.jsonl` belongs to the .NET MCP server, and it is where
renders are announced — so nothing on this side learns that an artifact exists except by reading it.
`RunStream` puts both on one broker:

```python
async with RunStream(project) as stream:
    watcher = stream.attach()                       # backlog first, then the live tail
    await run_turn(project, prompt, sink=stream.sink,
                   director=stream.director(EventLog(project.director_events, "director")))
```

Two properties are worth knowing before building on it. **Attaching late is the same as reading
back** — a watcher gets the replay window and then the tail from one iterator, which is what makes a
browser refresh survivable without a second mechanism. And **the stream is immediate while the files
are true**: events reach a watcher roughly as they happen, but the canonical ordering is
`(ts, src, seq)`, which `events.merge` applies to the files. Anything the broker has to drop — a run
outliving the replay window, a client that stops reading — is announced in the stream as
`stream.truncated` or `stream.lag` rather than passed over in silence.

## What is here

| Path | What it is |
| :--- | :--- |
| `orchestrator/` | The agent lifecycle, the tool policy, the director, and the record. Milestone 5. |
| `orchestrator/broker.py` | In-process fan-out of one run's events, with replay. Milestone 6. |
| `orchestrator/tail.py` | Follows `server.jsonl`, which the .NET MCP server owns and we can only read. |
| `orchestrator/watch.py` | `RunStream` — both halves of the record on one broker, for a watcher to attach to. |
| `orchestrator/hostlog.py` | The host's own conversation, transcribed into the record. For runs driven by Claude Code or Claude Desktop rather than by us. |
| `studio/observe.py` | Watching a project this process is not driving. Read-only: the host's interface is where direction happens. |
| `run_studio.py` | Terminal entry point. The same program as `python -m orchestrator`, runnable from the repository root. |
| `serve_studio.py` | Web entry point. Serves `studio/` over HTTP. Milestone 6. |
| `studio/` | The browser over `orchestrator`: routes, the run registry, the brief form, templates. |
| `hello_agent.py` | A probe, not architecture — the cheapest check that the Python side still reaches the .NET side. `--task stages` also checks that one MCP session spans a whole run. |
| `check_python.py` | Run by the install scripts before pip: refuses a venv older than the lock's recorded floor, so a wrong interpreter is one sentence rather than a pinned package failing three minutes in. |
| `tests/` | Standard-library `unittest`. Nothing here installs a package, so there is no pytest. |

```bash
python -m unittest discover -s src/webapp -t src/webapp
```

Nothing in the tests reaches the network or starts an agent. The parts that need a live model are
checked by actually running one, because a mock of the SDK only ever confirms the mock.

## Dependency layout

| File | What it is |
| :--- | :--- |
| `src/webapp/requirements.in` | The direct dependencies. **Edit this one.** |
| `src/webapp/requirements.txt` | Generated. Every package pinned to an exact version and hash, transitive ones included. **Do not hand-edit.** |
| `src/webapp/pip.ini` | Canonical pip settings — single index, wheels only, venv required. **Edit this one.** The install scripts copy it into the venv on every run, under whichever name pip reads there. |

The virtual environment itself is at `python/` and is not committed; it is rebuildable from
`requirements.txt`, which is why the manifest lives here rather than inside it.

> The studio has a **second agent runtime**, ADK, in its own venv (`python-adk/`) with its own lock at
> `src/adk_agent/requirements.txt`. It is deliberately separate — ADK caps `websockets<16` where
> `google-antigravity` does not — and it reuses this directory's `check_python.py` and `pip.ini` as
> the one canonical copy. See [`src/adk_agent/README.md`](../adk_agent/README.md). You pick one
> runtime per run; you do not need both.

`pip.ini` is kept here rather than only in the venv for the same reason. pip reads it from the venv
root, but `python -m venv` writes a `.gitignore` containing `*` into that directory and rewrites it
on every rebuild — so a copy living only there is both uncommittable and destroyed by the next
rebuild, silently taking the wheels-only and single-index defaults with it.

## First-time setup

Run these from the repository root. Activate the environment:

```bash
python\Scripts\activate
```

### 0. The pip settings — nothing to do

`install.cmd` and `install.sh` copy `pip.ini` into the venv themselves, on **every** run rather than
once. That is the point: `python -m venv` rewrites the venv root on every rebuild, so settings put
there by hand disappear at the next one, silently taking the wheels-only and single-index defaults
with them. Re-copying each install means the settings cannot drift out of the environment.

The scripts also handle the platform difference: pip reads `pip.ini` from the venv root on Windows
and `pip.conf` on Linux and macOS, from the same source file. `src/webapp/pip.ini` is the canonical
copy — edit that one; the copy in `python/` is derived and is overwritten each install.

You only need this by hand for the by-hand pip invocation in step 3:

```bash
copy src\webapp\pip.ini python\pip.ini
cp src/webapp/pip.ini python/pip.conf
```

### 1. Install the lock tool

`uv` compiles the lock file. It is the one package that cannot itself be hash-pinned before it has
run once, so install it on its own and be aware that this step is the bootstrap you are trusting:

```bash
python\Scripts\pip.exe install uv
```

### 2. Compile the lock file

This resolves `requirements.in` into `requirements.txt` with every transitive package pinned to an
exact version and cryptographic hash:

```bash
python\Scripts\uv.exe pip compile src\webapp\requirements.in --universal --python-version 3.13 --generate-hashes -o src\webapp\requirements.txt
```

Commit `requirements.txt`. Review its diff whenever it changes — that diff is the supply chain.

**`--universal` is not optional, and leaving it off fails on a machine you are not sitting at.**
Without it `uv` resolves for the platform it is run on and emits the pins with their environment
markers *stripped* — so a lock compiled on Windows carries `pywin32` unconditionally, and installing
it on Linux or macOS dies with `Could not find a version that satisfies the requirement pywin32`.
Nothing actually wants `pywin32` there; `mcp` asks for it as `sys_platform == 'win32'` and the
stripped lock is what loses the condition. `--universal` keeps the markers, so each platform installs
what applies to it from the one file.

`--python-version 3.13` sets the floor the resolution may assume, matching the requirement at the top
of this file. Lowering it is a real widening of the dependency graph rather than a formality: at
3.10 the same input additionally pins `exceptiongroup` and a second, older `rpds-py` for the
sub-3.11 range.

### 3. Install

```bash
src\webapp\install.cmd
```

On Linux or macOS, `src/webapp/install.sh`. The script checks that the environment and the compiled
`requirements.txt` both exist and says what to do if either is missing, then runs the install below.

It also checks the venv is new enough — the floor read from the lock's own header, so the two cannot
disagree. pip would catch a too-old interpreter anyway, but part-way through and phrased as a pinned
package rejecting the Python, which reads as a problem with the lock rather than with the environment
it is going into.

Or run it yourself — no checks, no settings copied, so the flags below are doing the work alone:

```bash
python\Scripts\pip.exe install --require-hashes --only-binary=:all: -r src\webapp\requirements.txt
```

`--require-hashes` makes pip verify every downloaded artifact against the recorded digest and fail
if any differs. A replaced or tampered release stops the install instead of running.

`--only-binary=:all:` stops pip building any source distribution, which matters because building an
sdist executes its `setup.py` on this machine at install time, before any code has been reviewed —
the most direct way a hostile package gets to act. `pip.ini` already sets it, but the flag is passed
explicitly here and by the scripts as well: the copy in the venv is one `del` from being gone, and
this by-hand invocation does not make it — so the command line is what still holds when it is absent.

## Checking for known vulnerabilities

Run after any dependency change:

```bash
python\Scripts\pip.exe install pip-audit
python\Scripts\pip-audit.exe -r src\webapp\requirements.txt
```

## Adding a dependency

1. Add the name to `requirements.in`, with its project URL in a comment.
2. **Check the name against the real repository before installing.** Typosquats differ by one
   character or a plausible synonym, and the install is where they win.
3. Recompile (step 2) and reinstall (step 3).
4. Review the `requirements.txt` diff — including packages you did not add, which is where an
   unexpected transitive dependency shows up.

## Upgrading

```bash
python\Scripts\uv.exe pip compile src\webapp\requirements.in --universal --python-version 3.13 --generate-hashes --upgrade -o src\webapp\requirements.txt
```

Then reinstall and re-audit. Read the diff. Keep `--universal` and the floor the same as step 2 —
an upgrade that quietly drops either produces a lock that installs here and nowhere else.
