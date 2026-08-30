#!/usr/bin/env bash
# Runs the Polson studio against a standalone project directory.
#
#   ./polson_webapp.sh path/to/project [--prompt "..."] [--fresh] [--unattended]
#
# Today this is the terminal orchestrator: one turn per invocation, with the terminal acting as the
# director. Milestone 6 puts a browser over the same run_turn, and this script is where that will
# start from, which is why it is named for the destination rather than the current step.
#
# The project must be a --standalone one. A managed project is refused by name, because its host
# owns tool policy and running it here would apply none at all.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Both layouts, because the same checkout is used from Windows and from a Unix shell: a venv made by
# Windows Python has Scripts/python.exe, one made on Linux or macOS has bin/python.
venv=""
for candidate in "$root/python/bin/python" "$root/python/bin/python3" "$root/python/Scripts/python.exe"; do
    if [ -x "$candidate" ]; then
        venv="$candidate"
        break
    fi
done

if [ -z "$venv" ]; then
    echo "error: no virtual environment at $root/python" >&2
    echo "       create one with:  python3 -m venv python" >&2
    echo "       then install the SDK:  python/bin/python -m pip install google-antigravity" >&2
    exit 1
fi

if [ $# -eq 0 ]; then
    echo "usage: ./polson_webapp.sh <project-dir> [--prompt \"...\"] [--fresh] [--unattended]" >&2
    echo "       make a project with:  dotnet bin/cli/Polson.CLI.dll create-project <parent> <id> agy --standalone" >&2
    exit 1
fi

exec "$venv" "$root/src/webapp/run_studio.py" "$@"
