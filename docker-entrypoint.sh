#!/bin/sh
# Container entrypoint for the Polson ADK studio.
#
# Two jobs the image itself cannot do, then hand off to uvicorn.

set -eu

# ---------------------------------------------------------------------------------------------
# 1. The credential.
# ---------------------------------------------------------------------------------------------
# The .NET engine reads its key from `appsettings.json` beside the DLL and **from nowhere else** —
# `Runtime.LoadConfigFile` builds its configuration from `AddJsonFile` alone, with no
# `AddEnvironmentVariables`. `orchestrator/credentials.py` documents why that single home is
# deliberate: two halves of one studio must not be able to authenticate as different identities.
#
# So the file has to exist, and it must not be in an image layer. Cloud Run injects the secret as
# an environment variable and this writes it out at start, into a file mode 600 owned by the
# process that reads it.
#
#     gcloud run deploy ... --set-secrets POLSON_AGENT_PLATFORM_KEY=polson-agent-key:latest
#
# The Python side reads the same value under the names google-genai expects, so one secret feeds
# both halves and they cannot disagree.
CLI_SETTINGS="/app/bin/cli/appsettings.json"

if [ -n "${POLSON_AGENT_PLATFORM_KEY:-}" ]; then
    umask 077
    # Written with printf rather than a heredoc so the key cannot be word-split or globbed, and
    # via python so it is JSON-escaped rather than pasted between quotes.
    python - "$CLI_SETTINGS" <<'PYTHON'
import json, os, sys
key = os.environ["POLSON_AGENT_PLATFORM_KEY"]
with open(sys.argv[1], "w", encoding="utf-8") as handle:
    json.dump({"ApiKeys": {"GoogleAgentPlatform": key}}, handle)
PYTHON
    # The Python half of the studio wants the same credential under google-genai's own names.
    export GOOGLE_API_KEY="${GOOGLE_API_KEY:-$POLSON_AGENT_PLATFORM_KEY}"
    export GOOGLE_GENAI_USE_ENTERPRISE="${GOOGLE_GENAI_USE_ENTERPRISE:-1}"
    echo "entrypoint: wrote $CLI_SETTINGS from POLSON_AGENT_PLATFORM_KEY"
else
    # Not fatal. The server starts, lists its apps and serves the console; the first thing that
    # needs the model fails with a message naming the missing variable. Refusing to boot would
    # make a misconfigured deploy look like a broken image.
    echo "entrypoint: WARNING - POLSON_AGENT_PLATFORM_KEY is not set." >&2
    echo "entrypoint:           the engine will refuse asset requisition and the agent cannot" >&2
    echo "entrypoint:           reach a model. Set it with --set-secrets." >&2
fi

# ---------------------------------------------------------------------------------------------
# 2. Something for a visitor to open.
# ---------------------------------------------------------------------------------------------
# Apps are generated, so a fresh container serves an empty list and the console shows a picker with
# nothing in it — a poor first ten seconds for someone handed a URL. Seeding one project makes the
# service self-demonstrating. Off unless asked for, because a deployment driven by its own web
# layer should create projects on demand rather than find one already there.
if [ -n "${POLSON_SEED_PROJECT:-}" ]; then
    if [ ! -d "/app/adk_agent/apps/${POLSON_SEED_PROJECT}" ]; then
        echo "entrypoint: seeding project '${POLSON_SEED_PROJECT}'"
        python /app/adk_agent/newproject.py "${POLSON_SEED_PROJECT}" \
            --workflow "${POLSON_SEED_WORKFLOW:-logo}" \
            --prompt "${POLSON_SEED_PROMPT:-a mark for a small independent studio}" \
            --projects-dir "${POLSON_PROJECTS_DIR:-/app/projects}" \
            || echo "entrypoint: WARNING - seeding failed; the service starts without it" >&2
    fi
fi

# ---------------------------------------------------------------------------------------------
# 3. Serve.
# ---------------------------------------------------------------------------------------------
# Cloud Run supplies $PORT and expects the process to listen on it and on 0.0.0.0 — binding
# 127.0.0.1 is the classic way to get a container that passes locally and fails its health check.
#
# `exec` so uvicorn becomes PID 1 and receives SIGTERM directly. Without it the shell holds PID 1,
# swallows the signal, and every revision takes the full shutdown grace period to go away.
exec python -m uvicorn main:app \
    --host 0.0.0.0 \
    --port "${PORT:-8080}" \
    --app-dir /app/adk_agent \
    "$@"
