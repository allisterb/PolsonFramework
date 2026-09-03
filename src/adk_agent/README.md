# Polson — the ADK agent runtime

A second Google agent runtime for the studio, alongside the Antigravity SDK. This is the deliverable
for the **Agentic Cinema** hackathon (Parallel track); the feasibility brief and the verification
behind every claim here are in [`docs/agentic-cinema-assessment.md`](../../docs/agentic-cinema-assessment.md).

**Status: setup only.** The venv and its install scripts exist and are what this README covers. The
agent itself — `main.py`, the agent package wrapping `bin/cli` over MCP, the Dockerfile — is not
written yet. Do not expect `adk web` to serve anything until it is.

## Two runtimes, two virtual environments — pick one per run

The studio can be driven by either Google agent runtime. **You decide which; you do not need both.**

| Runtime | venv | Install | Manifest |
| :--- | :--- | :--- | :--- |
| **Antigravity SDK** (the original) | `python/` | `src/webapp/install.{cmd,sh}` | `src/webapp/requirements.txt` |
| **ADK** (this one) | `python-adk/` | `src/adk_agent/install.{cmd,sh}` | `src/adk_agent/requirements.txt` |

**They are separate environments on purpose, not by accident.** ADK caps `websockets<16`;
`google-antigravity` sets no upper bound, and the webapp lock currently resolves it to 16.1.1. In one
shared venv the second install silently wins and neither lock describes what is actually there. Two
venvs also mirror the container we ship — which carries ADK and not Antigravity, because the contest
rules name `google-adk` among the permitted SDKs and say nothing about Antigravity.

The `python-adk/` directory is not committed; `python -m venv` writes its own `.gitignore` (`*`) into
it, so it self-ignores. It is rebuildable from the committed, hash-pinned `requirements.txt`.

## First-time setup

Run from the repository root. Nothing installs itself — per the project guardrails, package
installation is always a deliberate act by a person.

Make the environment, then run the install script. It copies pip's settings into the venv, checks the
interpreter is new enough for the lock, and installs every package under `--require-hashes
--only-binary=:all:`.

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
later and further away. See [`src/webapp/README.md`](../webapp/README.md) — its setup notes,
dependency layout, vulnerability scan, and dependency-change procedure all apply here unchanged, only
with `src/adk_agent/` and `python-adk/` in place of `src/webapp/` and `python/`.

The install scripts deliberately **reuse** `src/webapp/check_python.py` and `src/webapp/pip.ini`
rather than keeping their own copies. Both are generic and security-relevant — the interpreter-floor
check and the wheels-only / single-index pip defaults — and a duplicated copy is one that can drift.
One canonical copy, referenced from both runtimes.

## Compiling the lock

Same tool and flags as the webapp lock; only the paths differ. `uv` lives in the *original* venv
(`python/`), so compile from there:

```bash
python\Scripts\uv.exe pip compile src\adk_agent\requirements.in --universal --python-version 3.13 --generate-hashes -o src\adk_agent\requirements.txt
```

`--universal` keeps the environment markers so the one lock installs correctly on every platform;
without it a Windows-compiled lock carries `pywin32` unconditionally and dies on Linux. Commit the
result and review its diff — that diff is the supply chain.
