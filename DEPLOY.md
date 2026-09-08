# Deploying the ADK studio to Cloud Run

`src/adk_agent/README.md` is the architectural brief. This file is the runbook: the commands, in
order, and every failure that has actually happened, with what it looked like at the time.

**Fill these in from your own deployment** — they are deliberately not written down here:

| placeholder | what it is |
| :--- | :--- |
| `<project>` | the GCP project id |
| `<region>` | the Cloud Run region |
| `<service>` | the Cloud Run service name |
| `<runtime-sa>` | the revision's runtime service account, e.g. `<project-number>-compute@developer.gserviceaccount.com` |
| `<gcloud-python>` | the bundled interpreter inside your gcloud install, `platform/bundledpython/python.exe` on Windows |

The service should be **private** — no `allUsers` binding — and reached with an identity token or a
local proxy. Everything below assumes that.

---

## 0. Before anything

**On Windows, set `CLOUDSDK_PYTHON` or gcloud will not run.** The Store `python` alias intercepts it
and every command dies with *"Python was not found"*, which reads like a broken gcloud install rather
than a PATH problem.

```bash
export CLOUDSDK_PYTHON="<gcloud-python>"
```

Nothing else is needed locally. **The image is built by Cloud Build, not on your machine** —
`--source .` uploads the tree and builds remotely, so no Docker engine is required at any point.

---

## 1. The routine deploy

From the repo root. This is the whole command for an ordinary code change:

```bash
gcloud run deploy <service> --source . --project <project> --region <region> --no-allow-unauthenticated --quiet
```

**It is short because that is what makes it safe.** For an existing service, a deploy that passes no
env, secret or sizing flags **preserves all of them** — every environment variable, every secret
mapping, and the CPU, memory, timeout, concurrency and max-instances settings. Re-stating them is how
you lose one.

`--no-allow-unauthenticated` is passed explicitly rather than relying on the service's current state,
so a deploy can never quietly make the studio public.

Takes several minutes: Cloud Build compiles the .NET engine and then the Python image.

---

## 2. First-time setup

Only needed for a new project or a new secret.

### Secrets

Two credentials, both from Secret Manager:

| secret | env var | reaches |
| :--- | :--- | :--- |
| the platform key | `POLSON_AGENT_PLATFORM_KEY` | the engine's `ApiKeys:GoogleAgentPlatform`, **and** the agent's `GOOGLE_API_KEY` |
| the research key | `POLSON_PARALLEL_KEY` | `ApiKeys:Parallel` — **without it research is silently disabled** |

```bash
gcloud secrets create <secret-name> --project <project> --replication-policy=automatic
```

**Add the value from a file, never from an interactive pipe:**

```bash
gcloud secrets versions add <secret-name> --project <project> --data-file=/path/to/key.txt
```

Then delete the file, and keep it outside the repo while it exists — `.gitignore` covers
`**/*appsettings.json` but nothing about a stray `.key`, so a credential in the working tree is one
`git add -A` from being committed.

### The IAM grant every new secret needs

**A secret is readable only by accounts granted access *on that secret*.** Creating a second one does
not inherit the first one's bindings, and the deploy accepts the flag and then fails minutes later.

```bash
gcloud secrets add-iam-policy-binding <secret-name> --project <project> --member="serviceAccount:<runtime-sa>" --role="roles/secretmanager.secretAccessor"
```

### Wiring the secrets

Needed only when the mapping changes. **Both must be named in one flag** — `--set-secrets` replaces
the entire mapping, so listing only the new one silently unmaps the other and the container boots
with no credential at all.

```bash
gcloud run deploy <service> --source . --project <project> --region <region> --no-allow-unauthenticated --set-secrets "POLSON_AGENT_PLATFORM_KEY=<platform-secret>:latest,POLSON_PARALLEL_KEY=<research-secret>:latest" --quiet
```

---

## 3. Configuration

Two routes, and they are not interchangeable.

**The .NET engine reads `appsettings.json` and nothing else.** `Runtime.LoadConfigFile` uses
`AddJsonFile` with no `AddEnvironmentVariables`, and there is not one `GetEnvironmentVariable` call in
the engine. That file is **not in the image** — `docker-entrypoint.sh` writes it at boot from
`POLSON_*` variables, so a setting the entrypoint does not know about cannot be set at all. The
variables are tabulated in `src/adk_agent/README.md`; `test_entrypoint.py` fails if the engine gains a
setting the container cannot write.

**The Python half reads environment variables directly** and never opens that file:
`POLSON_MODEL`, `POLSON_BUDGET_TOKENS`, `POLSON_BUDGET_RAW_TOKENS`, `ADK_MAX_LLM_CALLS`,
`POLSON_MAX_RUNS_PER_DAY`, `POLSON_ALLOW_ORIGINS`, `POLSON_SEED_*`.

### Changing a value

```bash
gcloud run services update <service> --project <project> --region <region> --update-env-vars POLSON_ASSETS_BUDGET=30
```

**`--update-env-vars` merges. `--set-env-vars` replaces every variable.** Never use the second unless
you are restating the full set: a seed prompt containing commas would be torn apart.

Worth setting deliberately rather than leaving at defaults:

- **`POLSON_ASSETS_BUDGET`** — image generations, and the default of 120 is **per server run**, which
  under ADK means *per container*, shared by every project until the instance recycles.
- **`POLSON_BUDGET_TOKENS`** — the spend ceiling, measured in **billable** tokens.
- **`POLSON_BUDGET_RAW_TOKENS`** — the runaway guard, measured in raw input. Derived as four times the
  spend cap when unset. Two limits because a stuck agent resends a near-identical prefix, which caches
  beautifully, so a cost-weighted count alone would discount a loop exactly when it is worst.
- **`POLSON_MAX_RUNS_PER_DAY`** / **`POLSON_MAX_CONCURRENT_RUNS`** — the intake brake, default 25 and
  2. Only relevant if the service is ever made public.

---

## 4. Two commas, two different traps

Both bite on the same character, in opposite directions, and getting them the wrong way round fails
either way.

**PowerShell reads `a,b` as an array** and joins the elements with a space before handing them to a
native command. Quote the whole value:

```bash
--set-secrets "POLSON_AGENT_PLATFORM_KEY=<platform-secret>:latest,POLSON_PARALLEL_KEY=<research-secret>:latest"
```

Unquoted, gcloud reports `Invalid secret spec` with the two entries run together by a space.

**gcloud splits env-var values on commas.** When the comma is part of the *value*, change the
delimiter with a `^;^` prefix:

```bash
--update-env-vars "^;^POLSON_ALLOW_ORIGINS=http://127.0.0.1:8080,http://localhost:8080"
```

Without it you get one variable holding the first origin and a second bogus one named after the
second — no error, and the symptom appears somewhere else entirely.

---

## 5. Reaching it

It is private, so a browser needs help. The easiest route is gcloud's own proxy, which handles auth
and serves on `http://localhost:8080`:

```bash
gcloud run services proxy <service> --region <region> --project <project>
```

**Set `POLSON_ALLOW_ORIGINS` or every form submission through the proxy is refused.** ADK's CSRF
middleware compares the request `Origin` against an allowed list, and through the proxy that origin
is `http://127.0.0.1:8080` — genuinely different from the service's own host. The symptom is
particularly misleading: the form at `/new` loads perfectly and submitting it returns
`Forbidden: origin not allowed`, with nothing in the log, because the refusal happens in middleware
before any handler runs. A public deployment reached at its own URL needs none of this.

For a one-off request without the proxy:

```bash
curl -H "Authorization: Bearer $(gcloud auth print-identity-token)" "$(gcloud run services describe <service> --project <project> --region <region> --format='value(status.url)')/new"
```

---

## 6. Verifying a deploy

```bash
gcloud run services get-iam-policy <service> --project <project> --region <region> --format="value(bindings.members)"
```

**Empty output means private.** Anything containing `allUsers` means it is public.

```bash
gcloud run services describe <service> --project <project> --region <region> --format="value(spec.template.spec.containers[0].env)"
```

Check the variables survived and both secrets are still mapped.

Then the boot log, which is where the entrypoint says what it configured:

```bash
gcloud logging read 'resource.labels.service_name="<service>"' --project <project> --limit 50 --format="value(textPayload)" --freshness=15m
```

Expect a line naming the settings written — `ApiKeys:GoogleAgentPlatform, ApiKeys:Parallel,
Assets:Budget` — and, if a secret carries stray whitespace, `trimmed whitespace from …`. It logs key
names only; values never appear.

The token limits are logged when an agent app is built, on the **first run** rather than at boot:

```
token budget: <n> billable input tokens per invocation (runaway guard: <n> raw)
```

---

## 7. Stopping it

**Cloud Run has no stop verb.** A private service with no traffic already scales to zero, but a
background run inside a live container keeps going. To terminate one, force a new revision — the old
instance drains and is replaced:

```bash
gcloud run services update <service> --project <project> --region <region> --update-env-vars POLSON_STOPPED_AT=$(date -u +%Y-%m-%dT%H%MZ)
```

Remove the marker afterwards with `--remove-env-vars POLSON_STOPPED_AT` so it does not accumulate.

Rolling back is ordinary traffic routing — a failed revision never takes traffic in the first place,
so a broken deploy leaves the previous one serving:

```bash
gcloud run services update-traffic <service> --project <project> --region <region> --to-revisions=<revision>=100
```

---

## 8. Things that will surprise you

**Concurrency and the run page are coupled.** `/events` is an SSE stream held open for the whole run,
so one viewer occupies one request slot for the duration — and the page also polls `/curve` and
fetches scripts and artifacts. At a concurrency of 4 with `max-instances=1`, two watchers plus an
inspector exhausted the instance and Cloud Run returned 429 to everything, including the page's own
requests. It looks exactly like a hung agent; the run is unaffected, because the agent drives the app
in-process rather than over the network. Concurrency 30 is a practical ceiling — these connections
are idle, not CPU-bound — but the real fix is for the page not to pin a slot at all.

**Secrets are stored verbatim.** Neither Secret Manager nor gcloud trims anything, and a secret
created at a console usually ends in a newline: the recipe is paste, Enter, Ctrl-Z, Enter, and that
Enter is in the value. It will appear to work, because the model client tolerates trailing
whitespace. The entrypoint now strips whitespace from both credentials before either half of the
studio reads them, so this is no longer load-bearing — but check a new secret anyway, and compare
against the key's real length:

```bash
gcloud secrets versions access latest --secret=<secret-name> --project <project> | wc -c
```

**EOF differs by shell.** `--data-file=-` with nothing piped in leaves gcloud reading the console.
`Ctrl-D` ends it in bash; `Ctrl-Z` then Enter in PowerShell — and Ctrl-Z in bash *suspends* the
process instead, which looks exactly like a hang. Using a file avoids the question entirely.

**A run's record dies with the container, and this is not hypothetical.** Projects live on the
instance's ephemeral disk. Two runs were lost this way on 2026-09-08 — the second, `kubrick1`, was
mid-flight and roughly twenty minutes in when Cloud Run replaced the instance underneath it.

Both fixes are now in place and both are needed, because they save different things:

```bash
--update-env-vars POLSON_ARTIFACT_SERVICE_URI=gs://polson-artifacts        # ADK's versioned blobs
--update-env-vars POLSON_MIRROR_URI=gs://polson-artifacts/mirror          # the project directory
```

The first is ADK's own artifact store — the versioned renders the *model* is shown, under ADK's key
layout. Without it that store is `file://` and dies with the instance. The second is `mirror.py`,
which copies the project directory a *person* opens — `artifacts/`, `scripts/`, `events/`, the
authored `.md` and `.js` files — every `POLSON_MIRROR_SECONDS` (default 20) for as long as a run is in
flight. **Continuously rather than at the end, because the end may never arrive**, which is exactly
what happened to `kubrick1`.

They may share a bucket and must not share a prefix. `documents/` — the director's own supplied
material — is excluded from the mirror by two independent rules; see the module docstring.

**Getting a lost run back.** The studio reads the same `POLSON_MIRROR_URI`, so a run whose directory
is gone appears on the index under **Recover a run**. Choosing it downloads the project back onto the
container and opens it as an observation: the trace, the curve, the renders, the scripts and the
delivered documents all work, because the restore puts the files exactly where every existing route
already looks. It is a record rather than a runnable project — the mirror does not copy `GEMINI.md`,
`.agents/` or `agent.config.json`, so a recovered run can be read and not driven, which is the right
shape for one that is over.

Restoring lands it on the same ephemeral disk, deliberately: that copy is a **cache**, and if the
instance is replaced again the archive is still the archive. By hand, if the page is not available:

```bash
gcloud storage cp -r "gs://polson-artifacts/mirror/<project>" ./projects/ --project <project-id>
```

> [!NOTE]
> **The mirror needs Application Default Credentials, which the container has and a workstation
> usually does not.** On Cloud Run the metadata server supplies them and nothing has to be set up. Run
> the same code locally with `POLSON_MIRROR_URI` set and it fails at `storage.Client()` with
> `DefaultCredentialsError` — `gcloud auth login` is not enough, since that authenticates the CLI
> rather than the libraries. `gcloud auth application-default login` is the one that does. Leaving
> `POLSON_MIRROR_URI` unset locally is the ordinary case and costs nothing: the plugin is simply not
> registered and the index offers no recovery section.

**What `max-instances=1` actually guarantees, which is less than it sounds.** It caps steady state,
not the transition. During the replacement above the service reported **two active instances** for
several minutes: the new one served the browser while the old one drained with the agent still
running on it. There is no session affinity, so requests split between them, and the run page 404'd
for three minutes while the run itself was alive and working. If a deployed run vanishes from the
project list, that is the first thing to check — and the mirror is what makes it recoverable rather
than merely explicable.

**Diagnosing one of these afterwards.** The instance is gone, so the logs are all there is:

```bash
gcloud logging read 'resource.type="cloud_run_revision" AND resource.labels.service_name=<service> AND textPayload:"Starting new instance"' --project <project> --freshness=1d
```

Check memory and CPU before blaming the container — for `kubrick1` they were 19% and under 4%, so it
was the platform recycling the instance rather than anything the run did. The per-turn `turn polson/…`
lines survive in Cloud Logging and are the only trace of a lost run: they carry the timings and the
token counts and nothing of the work.

**There is no `exec`.** A Cloud Run *job* on the same image runs arbitrary commands, but it is a
separate ephemeral container: it can answer "what is in this image" and never "what is in the running
instance's project directory".

**One instance, deliberately.** `max-instances=1`, because projects live in the container and a
second instance would serve an empty app list. Normally Cloud Run answers saturation by scaling out;
here it cannot, so concurrency is the only dial. Read it together with the note above on what that
setting does *not* guarantee during a replacement.

**The container seeds a project but does not run it.** `POLSON_SEED_*` calls `newproject.py` at boot,
so a fresh container shows one unstarted project rather than an empty picker. Nothing is spent until
someone starts it.
