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

# ---------------------------------------------------------------------------------------------
# 1a. Credentials, normalised before anything reads them.
# ---------------------------------------------------------------------------------------------
# **Secret Manager stores bytes verbatim and neither it nor gcloud trims anything**, so a secret
# created interactively very often carries a trailing newline: `polson-agent-key` measured **55
# bytes for a 54-character key**, because the console recipe is paste, Enter, Ctrl-Z, Enter — and
# that Enter is in the value. It has always worked only because the model client happens to tolerate
# trailing whitespace, which is not a guarantee anyone gave us.
#
# Normalised **once, here**, rather than at each use, because the two halves of the studio read the
# credential by different routes: the engine gets it from the JSON written below, the agent gets it
# from `GOOGLE_API_KEY` exported further down. Trimming in only one of those is worse than trimming
# in neither — it produces a container where the engine authenticates and the agent does not, which
# looks like a broken model rather than a malformed secret.
#
# All whitespace rather than just the trailing newline: an API key contains none, so this also
# covers a CRLF from a Windows-written file and a stray leading space from a copy-paste.
for _credential in POLSON_AGENT_PLATFORM_KEY POLSON_PARALLEL_KEY; do
    eval "_value=\${$_credential:-}"
    [ -n "$_value" ] || continue

    _trimmed="$(printf '%s' "$_value" | tr -d '[:space:]')"
    if [ "$_trimmed" != "$_value" ]; then
        echo "entrypoint: trimmed whitespace from $_credential" >&2
    fi
    eval "$_credential=\$_trimmed"
    export "$_credential"
done
unset _credential _value _trimmed

if [ -n "${POLSON_AGENT_PLATFORM_KEY:-}" ]; then
    umask 077
    # Written via python so every value is JSON-escaped rather than pasted between quotes, which is
    # what stops a key or a prompt containing a quote from producing an unparseable file.
    #
    # **This writes every setting the engine reads, not just the credential.** It used to write the
    # key alone, and the consequence was invisible: `Runtime.LoadConfigFile` builds configuration
    # from `AddJsonFile` and nothing else — there is no `AddEnvironmentVariables` anywhere — so a
    # deployed container had *no way at all* to set the asset budget, the models, the cache
    # directories or the research key. They were not overridden; they were unreachable, sitting at
    # their compiled defaults while a deployment looked fully configured.
    #
    # Two things that cost real money followed. `Assets:Budget` stayed at 120 generations *per
    # server run* — and under ADK the server is spawned once per container, so that ceiling is
    # shared by every project anyone commissions until the instance recycles. And `ApiKeys:Parallel`
    # was never written at all, so research was silently disabled on every deploy: the one
    # difference most likely to make a hosted run behave unlike the same workflow run locally.
    #
    # The single-file rule is kept deliberately. `orchestrator/credentials.py` argues the engine
    # should have exactly one credential home so the two halves of the studio cannot authenticate as
    # different identities; the fix is to widen what the *writer* knows about, not to teach the
    # reader a second source.
    python - "$CLI_SETTINGS" <<'PYTHON'
import json, os, sys

# env var -> dotted configuration key. Every key here is one `Program.Setting(...)` actually reads;
# adding a setting to the engine means adding it here, or it cannot be set on a deployment.
SETTINGS = [
    ("POLSON_AGENT_PLATFORM_KEY",     "ApiKeys:GoogleAgentPlatform", str),
    ("POLSON_PARALLEL_KEY",           "ApiKeys:Parallel",            str),
    ("POLSON_ASSETS_MODEL",           "Assets:Model",                str),
    ("POLSON_ASSETS_BUDGET",          "Assets:Budget",               int),
    ("POLSON_ASSETS_CACHE_DIR",       "Assets:CacheDir",             str),
    ("POLSON_DOCUMENTS_MODEL",        "Documents:Model",             str),
    ("POLSON_DOCUMENTS_BUDGET",       "Documents:Budget",            int),
    ("POLSON_DOCUMENTS_CACHE_DIR",    "Documents:CacheDir",          str),
    ("POLSON_PHOTOS_BUDGET",          "Photos:Budget",               int),
    ("POLSON_PHOTOS_ALLOWED_HOSTS",   "Photos:AllowedHosts",         str),
    ("POLSON_PHOTOS_USER_AGENT",      "Photos:UserAgent",            str),
    ("POLSON_RESEARCH_PROCESSOR",     "Research:Processor",          str),
    ("POLSON_RESEARCH_BUDGET",        "Research:Budget",             int),
    ("POLSON_RESEARCH_ARCHIVE_DIR",   "Research:ArchiveDir",         str),
    ("POLSON_SERVER_TIMEOUT_SECONDS", "Server:DefaultTimeoutSeconds", int),
]

settings, skipped = {}, []
for variable, key, kind in SETTINGS:
    raw = os.environ.get(variable, "").strip()
    if not raw:
        continue

    if kind is int:
        try:
            value = int(raw)
        except ValueError:
            # Reported and dropped rather than written through. A non-numeric budget would reach
            # `int.TryParse`, leave 0, and a budget of zero disables the surface entirely — so a
            # typo would present as "asset requisition is broken" with nothing saying why.
            skipped.append(f"{variable}={raw!r} is not a number")
            continue
    else:
        value = raw

    section, _, leaf = key.partition(":")
    settings.setdefault(section, {})[leaf] = value

with open(sys.argv[1], "w", encoding="utf-8") as handle:
    json.dump(settings, handle, indent=2)

# Names only. The values include a credential, and this goes to a log aggregator.
print("entrypoint: settings written -> " + ", ".join(
    f"{section}:{leaf}" for section, leaves in settings.items() for leaf in leaves))
for problem in skipped:
    print(f"entrypoint: WARNING - ignored {problem}", file=sys.stderr)
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
        # POLSON_SEED_TEST=1 seeds the workflow as a framework evaluation: the agent reports
        # friction and gaps in findings.md alongside the artwork. Any non-empty value turns it on,
        # because Cloud Run environment values are strings and "0" reads as true to a shell test.
        seed_test=""
        if [ "${POLSON_SEED_TEST:-0}" != "0" ] && [ -n "${POLSON_SEED_TEST:-}" ]; then
            seed_test="--test"
        fi

        # Minutes for the whole commission, overriding the workflow's own default. Worth setting on
        # Cloud Run: a workflow default plus its breaker grace can exceed the **3600s request
        # timeout**, and then the wall kills the run before the breaker halts it cleanly — the exact
        # failure the breaker exists to replace. `comic_studio` defaults to 90, putting its breaker
        # at 105 against a 60-minute ceiling.
        seed_deadline=""
        if [ -n "${POLSON_SEED_DEADLINE:-}" ]; then
            seed_deadline="--deadline ${POLSON_SEED_DEADLINE}"
        fi

        # Narrows the workflow's direction: `blueprint` for an infographic, `review` for a drawing.
        # Without it `{{TYPE}}` renders **empty** rather than defaulting, so a seeded run silently
        # loses the whole type overlay -- and the loss looks like the workflow behaving differently
        # rather than like a missing argument. `newproject.py` has always taken `--type`; only this
        # file could not say it.
        seed_type=""
        if [ -n "${POLSON_SEED_TYPE:-}" ]; then
            seed_type="--type ${POLSON_SEED_TYPE}"
        fi

        python /app/adk_agent/newproject.py "${POLSON_SEED_PROJECT}" \
            --workflow "${POLSON_SEED_WORKFLOW:-logo}" \
            --prompt "${POLSON_SEED_PROMPT:-a mark for a small independent studio}" \
            --projects-dir "${POLSON_PROJECTS_DIR:-/app/projects}" \
            ${seed_test} ${seed_deadline} ${seed_type} \
            || echo "entrypoint: WARNING - seeding failed; the service starts without it" >&2
    fi
fi

# ---------------------------------------------------------------------------------------------
# Debugging note: the engine logs to a FILE, not to stderr.
# ---------------------------------------------------------------------------------------------
# `Runtime.WithFileLogging` writes `<assembly>/Polson-CLI*.log` with no console sink — despite the
# comment beside it in Program.cs saying "logs go to file/stderr". So when the MCP server dies
# during the stdio handshake, the container log shows only the Python side's
# `McpError: Connection closed` and nothing about the cause.
#
# Cloud Run has no `exec` into a running instance. The way to look is a **Cloud Run job** on the
# same image, which needs no rebuild and does not disturb the service:
#
#     gcloud run jobs create polson-debug --region <region> #       --image "$(gcloud run services describe polson-studio --region <region> #                    --format='value(spec.template.spec.containers[0].image)')" #       --command /bin/sh --args="^:^-c:<shell to run>"
#     gcloud run jobs execute polson-debug --region <region> --wait
#
# Then read the job's logs. Delete the job when done.

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
