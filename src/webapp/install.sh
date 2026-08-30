#!/usr/bin/env bash
# Installs the demo web app's Python dependencies into the repo's virtual environment.
#
# This script is run by a person, deliberately. Nothing in the build or any agent invokes it.

set -euo pipefail

# Resolve paths from the script's own location rather than the caller's working directory, so this
# works whether it is run as ./install.sh from src/webapp or as src/webapp/install.sh from the repo root.
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd -- "${script_dir}/../.." && pwd)

# A POSIX virtualenv puts executables in bin/; only Windows uses Scripts/. The venv is expected at
# <repo>/python either way, so the same relative location works on both and only the leaf differs.
venv_pip="${repo_root}/python/bin/pip"
requirements="${script_dir}/requirements.txt"

if [[ ! -x "${venv_pip}" ]]; then
    echo "error: no virtual environment at ${repo_root}/python" >&2
    echo "       create it with:  python3 -m venv \"${repo_root}/python\"" >&2
    echo "       then copy the pip settings:  cp \"${script_dir}/pip.ini\" \"${repo_root}/python/pip.conf\"" >&2
    exit 1
fi

if [[ ! -f "${requirements}" ]]; then
    echo "error: ${requirements} does not exist." >&2
    echo "       it is generated, not written by hand. compile it first:" >&2
    echo "       \"${repo_root}/python/bin/uv\" pip compile \"${script_dir}/requirements.in\" \\" >&2
    echo "           --generate-hashes -o \"${requirements}\"" >&2
    exit 1
fi

# Both flags are passed explicitly even though pip.conf sets only-binary, because pip.conf lives
# inside the venv and is copied there by hand — if that step was missed, these are what still hold.
#
#   --require-hashes   every package, transitive ones included, must match its recorded digest
#   --only-binary      no source distributions, so no setup.py executes during install
"${venv_pip}" install --require-hashes --only-binary=:all: -r "${requirements}"
