#!/usr/bin/env bash
# Installs the ADK agent's Python dependencies into the repo's SEPARATE python-adk venv.
#
# This mirrors src/webapp/install.sh exactly but for a DIFFERENT environment. The two runtimes are
# kept apart on purpose: ADK caps websockets<16 where google-antigravity has no upper bound, so a
# shared venv would let one install silently win and leave neither lock describing what is there.
# The Antigravity runtime uses the original python/ venv; ADK uses python-adk/. Pick one per run.
#
# This script is run by a person, deliberately. Nothing in the build or any agent invokes it.

set -euo pipefail

# Resolve paths from the script's own location rather than the caller's working directory, so this
# works whether it is run as ./install.sh from src/adk_agent or as src/adk_agent/install.sh from the repo root.
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd -- "${script_dir}/../.." && pwd)

# A POSIX virtualenv puts executables in bin/; only Windows uses Scripts/. The venv is expected at
# <repo>/python-adk either way, so the same relative location works on both and only the leaf differs.
# pip reads its settings from the venv root under a different name on each too: pip.conf here,
# pip.ini on Windows.
venv_pip="${repo_root}/python-adk/bin/pip"
venv_python="${repo_root}/python-adk/bin/python"
venv_config="${repo_root}/python-adk/pip.conf"
requirements="${script_dir}/requirements.txt"
# check_python.py and pip.ini are generic and security-relevant, so they live in tools/python as the
# one canonical copy rather than being duplicated per runtime - the wheels-only / single-index
# defaults and the interpreter-floor check cannot then drift apart.
#
# Neutral rather than inside either runtime: this venv must be installable without src/webapp present
# at all, which is what lets an ADK-only checkout stand on its own.
tools_dir="${repo_root}/tools/python"

if [[ ! -x "${venv_pip}" ]]; then
    echo "error: no virtual environment at ${repo_root}/python-adk" >&2
    echo "       create it with:  python3.13 -m venv \"${repo_root}/python-adk\"" >&2
    echo "       name the version rather than plain python3 -- where python3 is older the venv is" >&2
    echo "       built with that one silently, and pip refuses a pin much later on." >&2
    echo "       then run this script again." >&2
    exit 1
fi

if [[ ! -f "${requirements}" ]]; then
    echo "error: ${requirements} does not exist." >&2
    echo "       it is generated, not written by hand. compile it first:" >&2
    echo "       \"${repo_root}/python/bin/uv\" pip compile \"${script_dir}/requirements.in\" \\" >&2
    echo "           --universal --python-version 3.13 --generate-hashes -o \"${requirements}\"" >&2
    echo "       --universal keeps the environment markers; without it the lock only installs" >&2
    echo "       on the platform it was compiled on." >&2
    exit 1
fi

# Is this environment new enough for the lock about to be installed into it? Asked with the venv's
# own interpreter, and against the floor recorded in the lock's header, so neither half is a constant
# kept in step by hand. See tools/python/check_python.py for why it is worth asking before pip does.
"${venv_python}" "${tools_dir}/check_python.py" "${requirements}"

# The settings, into the venv where pip reads them. Copied on every install rather than once by
# hand: `python -m venv` rewrites this directory on every rebuild, so a copy that lives only here is
# destroyed by the next one — silently taking the wheels-only and single-index defaults with it.
#
# After the checks above, deliberately. Copying first means a missing venv fails on `cp` rather than
# on the message that says how to make one — the error the check exists to give.
cp "${tools_dir}/pip.ini" "${venv_config}"

# Both flags are passed explicitly even though the settings just copied set only-binary. The copy is
# one `rm` from being gone, and the by-hand pip invocation the README documents has no such step —
# these are what still hold when it is absent.
#
#   --require-hashes   every package, transitive ones included, must match its recorded digest
#   --only-binary      no source distributions, so no setup.py executes during install
"${venv_pip}" install --require-hashes --only-binary=:all: -r "${requirements}"
