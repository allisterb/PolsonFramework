#!/usr/bin/env bash
# Runs one turn against a standalone project, with the terminal as the director.
#
#   ./polson_run.sh path/to/project [--prompt "..."] [--fresh] [--unattended]
#
# The developer path: no browser, one turn per invocation, the agent's questions printed and your
# reply typed back. For the browser over the same run_turn, use ./polson_webapp.sh.
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
    echo "usage: ./polson_run.sh <project-dir> [--prompt \"...\"] [--fresh] [--unattended]" >&2
    echo "       one project, not a directory of them" >&2
    echo "       make one with:  dotnet bin/cli/Polson.CLI.dll create-project <parent> <id> agy --standalone" >&2
    echo "       for the browser instead:  ./polson_webapp.sh <projects-dir>" >&2
    exit 1
fi

exec "$venv" "$root/src/webapp/run_studio.py" "$@"
