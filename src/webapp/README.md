# Polson — the Python side

The orchestrator (Milestone 5) and, later, the demo website (Milestone 6). Python 3.13.

Nothing here installs itself. Per the project guardrails in `CLAUDE.md`, package installation is
always a deliberate act by a person — no script, build step, or agent runs `pip install` on your
behalf. The commands below are for you to run and review.

## Running a project

```bash
dotnet bin/cli/Polson.CLI.dll create-project projects acme agy --standalone --brief "..."
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

A run is resumable — the conversation id is written into `project.json`, and the next run continues
where it left off unless you pass `--fresh`.

## What is here

| Path | What it is |
| :--- | :--- |
| `orchestrator/` | The agent lifecycle, the tool policy, the director, and the record. Milestone 5. |
| `run_studio.py` | Entry point. The same program as `python -m orchestrator`, runnable from the repository root. |
| `hello_agent.py` | A probe, not architecture — the cheapest check that the Python side still reaches the .NET side. `--task stages` also checks that one MCP session spans a whole run. |
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
| `src/webapp/pip.ini` | Canonical pip settings — single index, wheels only, venv required. Copied into the venv during setup. |

The virtual environment itself is at `python/` and is not committed; it is rebuildable from
`requirements.txt`, which is why the manifest lives here rather than inside it.

`pip.ini` is kept here rather than only in the venv for the same reason. pip reads it from the venv
root, but `python -m venv` writes a `.gitignore` containing `*` into that directory and rewrites it
on every rebuild — so a copy living only there is both uncommittable and destroyed by the next
rebuild, silently taking the wheels-only and single-index defaults with it.

## First-time setup

Run these from the repository root. Activate the environment:

```bash
python\Scripts\activate
```

### 0. Install the pip settings

Do this first, and again after any venv rebuild — the settings below only apply once this file is
in place:

```bash
copy src\webapp\pip.ini python\pip.ini
```

On Linux or macOS the venv layout and the config filename both differ — executables live in `bin/`
rather than `Scripts/`, and pip reads `pip.conf` rather than `pip.ini`. The contents are identical:

```bash
cp src/webapp/pip.ini python/pip.conf
```

The install scripts (`install.cmd`, `install.sh`) handle the rest of the platform difference and
will tell you if either the environment or the compiled `requirements.txt` is missing.

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
python\Scripts\uv.exe pip compile src\webapp\requirements.in --generate-hashes -o src\webapp\requirements.txt
```

Commit `requirements.txt`. Review its diff whenever it changes — that diff is the supply chain.

### 3. Install

```bash
src\webapp\install.cmd
```

On Linux or macOS, `src/webapp/install.sh`. The script checks that the environment and the compiled
`requirements.txt` both exist and says what to do if either is missing, then runs the install below.

Or run it yourself:

```bash
python\Scripts\pip.exe install --require-hashes --only-binary=:all: -r src\webapp\requirements.txt
```

`--require-hashes` makes pip verify every downloaded artifact against the recorded digest and fail
if any differs. A replaced or tampered release stops the install instead of running.

`--only-binary=:all:` stops pip building any source distribution, which matters because building an
sdist executes its `setup.py` on this machine at install time, before any code has been reviewed —
the most direct way a hostile package gets to act. `pip.ini` already sets it, but the flag is passed
explicitly here and by the scripts as well: `pip.ini` is copied into the venv by hand in step 0, and
if that was missed the command line is what still holds.

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
python\Scripts\uv.exe pip compile src\webapp\requirements.in --generate-hashes --upgrade -o src\webapp\requirements.txt
```

Then reinstall and re-audit. Read the diff.
