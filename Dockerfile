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
# **The fonts are the product, not a dependency.** The studio manuals name 30 typeface
# references; a slim image serves none of them, and a design tool that can only set DejaVu is
# crippled in a way a missing library would never be tolerated. Package names were verified
# against Debian trixie's own index rather than guessed, because a wrong one costs a build.
#
#   recommended   Debian's own curated set: an 8 KB metapackage whose entries are Depends, so
#                 --no-install-recommends does not skip them. Brings Caladea (Cambria metrics),
#                 Carlito (Calibri metrics), League Spartan (geometric display), Cantarell,
#                 Courier Prime, and Comic Neue — which the comic and comic_studio workflows
#                 can actually use — plus Noto Color Emoji, Symbola and FreeFont for the symbol
#                 and Unicode coverage an infographic needs. Its last entry is
#                 `fonts-urw-base35 | fonts-texgyre`, and naming TeX Gyre explicitly below is
#                 what makes apt satisfy that alternation with the OpenType family rather than
#                 the Type 1 ancestor. Preferring a maintained set to a hand-picked one is also
#                 one fewer thing to curate.
#
#                 It also pulls `fonts-liberation` (v1) where we ask for `fonts-liberation2`,
#                 so both generations register the same family names. Harmless — fontconfig
#                 picks one — and visible in the family list the build prints.
#   texgyre       the find: 14 MB for eight OpenType families covering most classical
#                 categories — Termes (Times/transitional), Pagella (Palatino/humanist),
#                 Schola (Century Schoolbook), Bonum (Bookman), Heros (Helvetica — named 3x
#                 in the manuals), Adventor (Avant Garde — the geometric sans we had none of),
#                 Chorus (Chancery script), Cursor (Courier).
#   inter         named 3x in the manuals; the modern screen sans.
#   ebgaramond    the old-style serif, and the one direct hit against the manuals' list.
#   liberation2   metric-compatible with Arial / Times / Courier, so a layout that assumes
#                 those widths still measures sensibly.
#   dejavu-core   the fallback of last resort.
#
# `fonts-urw-base35` was considered and rejected as redundant: it is the Type 1 ancestor of the
# same URW lineage TeX Gyre extends, so it would add 15 MB of near-duplicates.
#
# > **The didone category is served, and by the face the manual itself nominates.** An earlier
# > version of this note called it a gap. That was wrong in its premise rather than merely out
# > of date, and reading manual 11 rather than counting keyword hits is what showed it.
# >
# > Of its seven Didot mentions, **one** defines the Modern category (vertical stress, flat thin
# > serifs, radical contrast, for luxury and fashion). The other six are the manual teaching
# > *font fallback*, using Didot as its canonical example of an absent face — and every one of
# > them names Playfair Display as the answer:
# >
# >     ctx.font = 'italic 400 46px "Playfair Display", Didot, serif';   // Modern
# >
# > So the manual never expected Didot to be installed. It prescribes Playfair Display for that
# > register, `fetch-fonts.py` installs it, and the variable weight axis is confirmed working —
# > a specimen at 400 against 700 shows the heavy cut reaching the modern-serif register the
# > category asks for. `fonts-solide-mirage` remains as a genuine, if experimental and unicase,
# > didone for display marks.
# >
# > **Georgia is handled the same way, and by the same mechanism.** It is named 6 times across
# > 3 manuals and sits in the same fallback chains, so without it `'40px Didot, Georgia, serif'`
# > missed both named faces and landed on generic serif. `fetch-fonts.py` now also installs
# > **Gelasio**, which its foundry states is *metrics compatible with Georgia* in Regular, Bold,
# > Italic and Bold Italic — so a layout measured against Georgia still measures correctly.
# >
# > An earlier version of this note recommended `fonts-gelasio` from apt. **That package does
# > not exist in Debian** — the page returns "No such package". Checking every name against the
# > real repository is the rule `requirements.in` states for PyPI, and it earns its keep here
# > too: this is the second recommendation in this file that verification caught.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        libicu-dev \
        libfontconfig1 \
        fontconfig \
        fonts-recommended \
        fonts-dejavu-core \
        fonts-liberation2 \
        fonts-ebgaramond \
        fonts-texgyre \
        fonts-inter \
        fonts-solide-mirage \
    && echo "ICU: $(dpkg-query -W -f='${Version}' libicu-dev 2>/dev/null || echo MISSING)" \
    && rm -rf /var/lib/apt/lists/*

# The one face apt cannot supply. Fetched at a pinned commit and verified by SHA-256 — see
# fetch-fonts.py for why that matters more for a font than for a package: `CLAUDE.md` treats
# fonts as untrusted *binary* data whose risk is the parser, and SkiaSharp's parser is native
# FreeType rather than managed code.
#
# It runs with the interpreter already in the image and the standard library only, so nothing
# is added to fetch it — no curl, no wget.
#
# `fc-cache` runs here rather than in the step above so one rebuild covers both the packaged
# fonts and these, and the family list printed afterwards is the complete one.
COPY fetch-fonts.py /tmp/fetch-fonts.py
RUN python /tmp/fetch-fonts.py \
    && rm -f /tmp/fetch-fonts.py \
    && fc-cache --force \
    && echo "font families available to the studio:" \
    && fc-list : family | tr ',' '\n' | sort -u

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
# The console writes a runtime-config file into its own package directory at every startup, and
# as a non-root user that fails:
#
#     Failed to write runtime config file .../adk/cli/browser/assets/config/runtime-config.json
#     [Errno 13] Permission denied
#
# The console still loads, so this looks harmless and is not: that file is how ADK hands UI
# settings to the SPA, so `logo_text` and `logo_image_url` would have been accepted by
# `get_fast_api_app` and then silently never reached the browser. The directory is located by
# asking the installed package rather than by hardcoding a `python3.13` path that a base-image
# bump would invalidate.
RUN adduser --disabled-password --gecos "" polson \
    && mkdir -p /app/adk_agent/apps /app/adk_agent/artifact_versions /app/projects \
    && chown -R polson:polson /app \
    && ADK_BROWSER="$(python -c 'import os, google.adk.cli as c; print(os.path.join(os.path.dirname(c.__file__), "browser"))')" \
    && echo "adk console assets: $ADK_BROWSER" \
    && chown -R polson:polson "$ADK_BROWSER"
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
