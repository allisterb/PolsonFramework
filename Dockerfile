# Polson — the ADK studio runtime, as one container.
#
# Built by Cloud Build, not locally:
#
#     gcloud run deploy polson-studio --source . --region <region> --project <project>
#
# **Nothing here needs a local Docker engine.** `--source .` uploads the tree (see `.gcloudignore`)
# and builds remotely.
#
# It lives at the repository root because Cloud Build looks for `Dockerfile` at the root of the
# build context, and the context has to be the root anyway: the .NET build needs `nuget.config`,
# `Directory.Build.props` and the whole of `src/`.
#
# ---------------------------------------------------------------------------------------------
# Why the engine is built rather than copied
# ---------------------------------------------------------------------------------------------
# `bin/` is gitignored, so `--source .` would not upload it. That turns out to be the right
# constraint rather than an obstacle: the local build is **588 MB**, of which **550 MB is
# `runtimes/`** — native assets for every RID NuGet knows about. Publishing for `linux-x64` keeps
# one platform's natives and discards the rest.
#
# ---------------------------------------------------------------------------------------------
# What will differ from your machine, and it is not subtle
# ---------------------------------------------------------------------------------------------
# **Fonts.** A slim image ships none. `Skia.Font.families()` would return an empty list and every
# `ctx.font` would resolve to nothing. That is not hypothetical — a local run chose Georgia, and
# another chose Garamond as "the highest grade serif available in the environment". Those are
# Windows faces. The set installed below is the deliberate replacement; the studio's typographic
# choices here will differ from the same brief run locally, and that is expected rather than a bug.


# ============================================================================================
# Stage 1 — the .NET engine
# ============================================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS engine

WORKDIR /src

# The build inputs, in the order that keeps the layer cache useful: pinning files first, then
# source. A source-only edit then re-uses the restored package graph.
COPY nuget.config Directory.Build.props Polson.sln ./
COPY src/ ./src/

# `RestoreLockedMode=true` matches what CLAUDE.md §7 requires of CI: every project carries a
# committed `packages.lock.json`, and a restore that would have to re-resolve fails here rather
# than silently producing a different graph than the one reviewed.
#
# Framework-dependent (`--self-contained false`) because the runtime arrives in stage 2 from
# Microsoft's own image; bundling a second copy would add ~70 MB for nothing.
RUN dotnet publish src/Polson.CLI/Polson.CLI.csproj \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained false \
        -p:RestoreLockedMode=true \
        --output /engine \
    && rm -f /engine/appsettings.json


# ============================================================================================
# Stage 2 — the runtime
# ============================================================================================
# `python:3.13-slim` rather than a .NET base image with Python added: `requirements.txt` was
# compiled with `--python-version 3.13`, and `check_python.py` exists precisely because installing
# it into an older interpreter fails late and confusingly. Debian's own Python is older, so
# starting from the Python image and adding .NET is the way round that keeps the lock honest.
FROM python:3.13-slim AS runtime

# The .NET runtime, copied from Microsoft's published image rather than fetched with
# `dotnet-install.sh`. A piped install script is an unreviewed download executing at build time;
# a multi-stage COPY takes the same bits from a tagged image with none of that.
COPY --from=mcr.microsoft.com/dotnet/runtime:10.0 /usr/share/dotnet /usr/share/dotnet
ENV DOTNET_ROOT=/usr/share/dotnet \
    PATH="/usr/share/dotnet:${PATH}" \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=1

# `libfontconfig1` is required by `SkiaSharp.NativeAssets.Linux` (the plain package, not
# `.NoDependencies`) — without it the native library fails to load at startup rather than at the
# first render, which is at least a loud failure.
#
# The fonts are a design decision, not a dependency:
#   dejavu-core      a workmanlike sans/serif/mono, and the usual fallback of last resort
#   liberation2      metric-compatible with Arial / Times / Courier, so layouts that assume those
#                    widths still measure sensibly
#   ebgaramond       a real oldstyle serif — the face a local run reached for by name
RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        libfontconfig1 \
        fonts-dejavu-core \
        fonts-liberation2 \
        fonts-ebgaramond \
    && fc-cache -f \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Python dependencies, hash-locked exactly as the install scripts do it.
#
# `src/webapp/pip.ini` is deliberately NOT copied to `/etc/pip.conf`: it sets
# `require-virtualenv = true`, which is correct on a developer's machine and would refuse every
# install in a container that has no virtualenv. The two flags that matter are passed explicitly.
COPY src/adk_agent/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir --require-hashes --only-binary=:all: --requirement requirements.txt

# The engine, then the runtime code.
COPY --from=engine /engine ./bin/cli
COPY src/adk_agent/ ./adk_agent/
COPY docker-entrypoint.sh /usr/local/bin/polson-entrypoint
RUN chmod +x /usr/local/bin/polson-entrypoint

# Non-root, and owning the directories written at runtime. Cloud Run does not require this, but a
# process that never needs root should not have it.
RUN adduser --disabled-password --gecos "" polson \
    && mkdir -p /app/adk_agent/apps /app/adk_agent/artifact_versions /app/projects \
    && chown -R polson:polson /app
USER polson

ENV POLSON_CLI_DLL=/app/bin/cli/Polson.CLI.dll \
    POLSON_PROJECTS_DIR=/app/projects \
    PYTHONUNBUFFERED=1 \
    PORT=8080

# ---------------------------------------------------------------------------------------------
# What does not survive an instance
# ---------------------------------------------------------------------------------------------
# `/app/projects` and `/app/adk_agent/apps` are written at runtime and are **ephemeral**: a project
# created by a visitor dies with the container. That is acceptable for a demo where one session
# does one brief, and wrong for anything else.
#
# The artifact store is the part worth fixing at deploy time rather than living with. `main.py`
# defaults it to `file://`, which here means the same ephemeral disk — so pass
#     --set-env-vars POLSON_ARTIFACT_SERVICE_URI=gs://<bucket>
# and the version history outlives the instance. Sessions likewise: `POLSON_SESSION_SERVICE_URI`.

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/polson-entrypoint"]
