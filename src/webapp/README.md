# Polson demo web app — Python environment

Dependency manifest and setup for the demo website (Milestone 6). Python 3.13.

Nothing here installs itself. Per the project guardrails in `CLAUDE.md`, package installation is
always a deliberate act by a person — no script, build step, or agent runs `pip install` on your
behalf. The commands below are for you to run and review.

## Layout

| File | What it is |
| :--- | :--- |
| `src/web/requirements.in` | The direct dependencies. **Edit this one.** |
| `src/web/requirements.txt` | Generated. Every package pinned to an exact version and hash, transitive ones included. **Do not hand-edit.** |
| `src/web/pip.ini` | Canonical pip settings — single index, wheels only, venv required. Copied into the venv during setup. |

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
copy src\web\pip.ini python\pip.ini
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
python\Scripts\uv.exe pip compile src\web\requirements.in --generate-hashes -o src\web\requirements.txt
```

Commit `requirements.txt`. Review its diff whenever it changes — that diff is the supply chain.

### 3. Install

```bash
python\Scripts\pip.exe install --require-hashes -r src\web\requirements.txt
```

`--require-hashes` makes pip verify every downloaded artifact against the recorded digest and fail
if any differs. A replaced or tampered release stops the install instead of running.

`pip.ini` already forces `--only-binary=:all:`, so no source distribution is built. That matters
because building an sdist executes its `setup.py` on this machine at install time, before any code
has been reviewed — it is the most direct way a hostile package gets to act.

## Checking for known vulnerabilities

Run after any dependency change:

```bash
python\Scripts\pip.exe install pip-audit
python\Scripts\pip-audit.exe -r src\web\requirements.txt
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
python\Scripts\uv.exe pip compile src\web\requirements.in --generate-hashes --upgrade -o src\web\requirements.txt
```

Then reinstall and re-audit. Read the diff.
