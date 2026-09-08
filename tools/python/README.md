# Shared Python install tooling

Two files, used by **both** virtual environments' install scripts:

| | |
| :--- | :--- |
| `check_python.py` | Asks the target venv's own interpreter whether it is new enough for the lock about to be installed into it, reading the floor from the lock's own header. |
| `pip.ini` | pip's settings, copied into each venv root on every install: wheels only, one index, no source distributions. |

## Why they live here and not beside either runtime

**One canonical copy, so the two runtimes cannot drift.** These are generic and security-relevant —
the wheels-only and single-index defaults are what stop a source distribution executing `setup.py`
during an install — and two copies would eventually disagree without anyone noticing.

**Neutral rather than inside a runtime, so neither depends on the other.** They used to live in
`src/webapp/`, which meant `python-adk/` could not be installed without the Antigravity tree present:
an ADK-only checkout had to fetch a directory it never runs in order to install the one it does. The
hackathon entry ships ADK alone, so that coupling was a real obstacle rather than an aesthetic one.

`src/webapp/install.{cmd,sh}` and `src/adk_agent/install.{cmd,sh}` both read from here, and each
copies `pip.ini` into its own venv root — as `pip.ini` on Windows and `pip.conf` elsewhere, which is
the same file under the name pip looks for on each platform.

## Why the copy happens on every install

`python -m venv` rewrites the venv directory, so a `pip.ini` placed there once by hand is destroyed
by the next rebuild — silently taking the wheels-only and single-index defaults with it. Copying on
every install is what makes the settings survive a recreated environment.

The install scripts also pass `--require-hashes --only-binary=:all:` explicitly rather than relying
on that copy. The copy is one `del` from being gone, and the by-hand pip invocation the READMEs
document has no such step; the flags are what still hold when the file is absent.
