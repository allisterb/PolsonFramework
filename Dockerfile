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

# `docs/` is a build input, not documentation. Polson.MCPServer embeds `docs/*.md` and
# `docs/manuals/*.md` as resources — the studio manuals the agent queries through
# `polson://manual/*`. A build without them succeeds (an MSBuild glob matching nothing is not
# an error) and then dies at startup in `KnowledgeCorpus`'s type initializer, which is a long
# way from the cause. The guard below turns that into a build failure that names the reason.
COPY docs/ ./docs/
RUN test -n "$(ls docs/manuals/*.md 2>/dev/null)" \
    || { echo 'FATAL: docs/manuals/*.md is empty or missing.' >&2; \
         echo '       These are EmbeddedResource inputs for Polson.MCPServer, not docs.' >&2; \
         echo '       Check .gcloudignore is not excluding docs/.' >&2; exit 1; }

# `RestoreLockedMode=true` matches what CLAUDE.md §7 requires of CI: every project carries a
# committed `packages.lock.json`, and a restore that would have to re-resolve fails here rather
# than silently producing a different graph than the one reviewed.
#
# **There is deliberately no `--runtime linux-x64`, and that is not an oversight.** A first
# build had one, and every project failed restore with:
#
#     error NU1004: The project's runtime identifiers have changed from. Project's runtime
#     identifiers: linux-x64, lock file's runtime identifiers . The packages lock file is
#     inconsistent with the project dependencies so restore can't be run in locked mode.
#
# The committed locks declare one target, `net10.0`, with no RID. Asking for `linux-x64` asks
# NuGet to resolve RID-specific assets the lock does not describe, and locked mode refuses —
# which is the lock doing its job. Regenerating the locks with a RID would make them
# platform-specific for everyone to suit one container; publishing portable costs nothing.
#
# The portable publish then carries `runtimes/` for **every** RID NuGet knows — 550 MB, of
# which `linux-x64` is 15 MB. The prune happens in the same layer because a later layer that
# merely deletes would leave the bytes in the one beneath it. `du` prints the result so the
# build log carries evidence rather than an assumption.
#
# Measured on the first successful build: **48 MB**, against 588 MB for the same publish on a
# developer machine. Roughly 33 MB of managed assemblies plus the one platform's natives.
#
# Framework-dependent (`--self-contained false`) because the runtime arrives in stage 2 from
# Microsoft's own image; bundling a second copy would add ~70 MB for nothing.
RUN dotnet publish src/Polson.CLI/Polson.CLI.csproj \
        --configuration Release \
        --self-contained false \
        -p:RestoreLockedMode=true \
        --output /engine \
    && rm -f /engine/appsettings.json \
    && if [ -d /engine/runtimes ]; then \
           find /engine/runtimes -mindepth 1 -maxdepth 1 -type d ! -name linux-x64 -exec rm -rf {} + ; \
       fi \
    && du -sh /engine


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
#
# **`aspnet`, not `runtime`, and the difference is not cosmetic.** A first deploy used
# `dotnet/runtime:10.0` and the engine would not launch:
#
#     No frameworks were found.
#     framework=Microsoft.AspNetCore.App&framework_version=10.0.0&rid=linux-x64&os=debian.13
#
# `Polson.CLI.runtimeconfig.json` declares **two** frameworks — `Microsoft.NETCore.App` and
# `Microsoft.AspNetCore.App` — even though no project uses the Web SDK. It arrives transitively
# through `ModelContextProtocol.AspNetCore`, the package behind the server's HTTP transport option.
# The base runtime image carries only the first. `aspnet` carries both.
#
# **`aspnet` is a runtime image, not an SDK one** — Microsoft's family runs `sdk` (a full toolchain,
# ~1 GB, build only) → `aspnet` → `runtime` → `runtime-deps`. The cost over `runtime` is one extra
# shared framework: measured per version, `Microsoft.AspNetCore.App` is **~29 MB** against ~75 MB
# for `Microsoft.NETCore.App`. (Measured on a Windows install, so treat it as the right order of
# magnitude rather than the exact Linux figure.)
#
# It is kept rather than trimmed because the HTTP transport is the retained fallback: if a sandbox
# ever refuses the stdio subprocess spawn, `Options.cs` already has `--http` and `--port`, and
# `studio.py` would swap to `StreamableHTTPConnectionParams`. Removing ASP.NET would remove the
# escape hatch.
COPY --from=mcr.microsoft.com/dotnet/aspnet:10.0 /usr/share/dotnet /usr/share/dotnet
ENV DOTNET_ROOT=/usr/share/dotnet \
    PATH="/usr/share/dotnet:${PATH}" \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=1

# `libfontconfig1` is required by `SkiaSharp.NativeAssets.Linux` (the plain package, not
# `.NoDependencies`) — without it the native library fails to load at startup rather than at the
# first render, which is at least a loud failure.
#
# **`libicu-dev` is here because of the COPY above, and its absence is not obvious.** Copying
# only `/usr/share/dotnet` out of the aspnet image brings the runtime but none of the OS
# libraries that image would have had around it. .NET needs ICU for globalization, and a
# deploy crashed before reaching `Main`:
#
#     at System.Globalization.CultureInfo..cctor()
#     at System.Reflection.RuntimeAssembly.GetLocale()
#     at Polson.Runtime..cctor()
#
# The alternative is `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, which needs no package and is
# the wrong trade here: this is a typography tool, and invariant mode changes string
# comparison, casing outside ASCII, and number and date formatting. Correct text is worth more
# than the megabytes.
#
# `libicu-dev` rather than a versioned `libicuNN`: the name is stable across Debian releases,
# so a base-image bump cannot silently break the build. It costs headers we do not need — the
# honest price of not pinning a version that would go stale.
#
# **`fontconfig` is a separate package from `libfontconfig1`, and both are needed.** A first
# build installed only the library and died with `fc-cache: not found` (exit 127) — the tools
# ship separately. It pays for itself twice: `fc-cache` warms the cache so the first render
# does not build it, and `fc-list` prints the families actually present, so the build log
# states what type the studio can set instead of leaving it to be inferred from a
# disappointing render.
#
# The fonts are a design decision, not a dependency:
#   dejavu-core      a workmanlike sans/serif/mono, and the usual fallback of last resort
#   liberation2      metric-compatible with Arial / Times / Courier, so layouts that assume those
#                    widths still measure sensibly
#   ebgaramond       a real oldstyle serif — the face a local run reached for by name
RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        libicu-dev \
        libfontconfig1 \
        fontconfig \
        fonts-dejavu-core \
        fonts-liberation2 \
        fonts-ebgaramond \
    && fc-cache --force \
    && echo "ICU: $(dpkg-query -W -f='${Version}' libicu-dev 2>/dev/null || echo MISSING)" \
    && echo "font families available to the studio:" \
    && fc-list : family | tr ',' '\n' | sort -u \
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
# The artifact store has the same problem: `main.py` defaults it to `file://`, which here is that
# same ephemeral disk, so version history dies with the instance too.
#
# `gs://<bucket>` is the fix and is **not usable yet**: `GcsArtifactService.__init__` does
# `from google.cloud import storage`, and `google-cloud-storage` is not in `requirements.txt` — we
# install `google-adk[mcp]`, while GCS lives in ADK's `[gcp]` extra. The import is lazy, so passing
# a `gs://` URI fails at startup with ModuleNotFoundError rather than at build. Add the package to
# `requirements.in` and recompile the lock before reaching for it. (Note too that only the bucket
# name is read — a path after it is silently discarded.)
#
# Sessions have the same shape: `POLSON_SESSION_SERVICE_URI`, and `sqlite://` on ephemeral disk buys
# nothing here.

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/polson-entrypoint"]
