#!/usr/bin/env bash
# Serves the Polson studio — the browser over a project directory.
#
#   ./polson_webapp.sh path/to/projects [--host 0.0.0.0] [--port 8000]
#
# The argument is the directory that *holds* project directories, not a project: the front page
# lists what it finds there, and a brief typed into the form creates another one beside them.
# Nothing starts an agent until someone presses a button.
#
# For one turn in the terminal instead, with no browser and you as the director, use ./polson_run.sh.

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
    echo "usage: ./polson_webapp.sh <projects-dir> [--host HOST] [--port PORT]" >&2
    echo "       the directory holds projects; it is not itself one" >&2
    echo "       serves http://127.0.0.1:8000 by default" >&2
    echo "       for one turn in the terminal instead:  ./polson_run.sh <project-dir>" >&2
    exit 1
fi

exec "$venv" "$root/src/webapp/serve_studio.py" "$@"
