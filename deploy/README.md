# Deploying the studio to Cloud Run

Everything about deploying is here: setting up a project (§1–5), the routine deploy and its settings (§6–8), reaching,
checking and stopping a service (§9–11), and the failures that have actually happened (§12). `src/adk_agent/README.md`
is the architectural brief: what the image contains and why.

Nothing in the source names a Google Cloud project, region, bucket or service URL. Everything that ties a
deployment to one is configuration, listed here in the order it has to exist. The service, the image repository
and the runtime account's project number all follow from the project; the rest is recreated by hand.

**Keep it that way: real names stay out of the repository**, this file included. The studio can be deployed without
a sign-in, so a project id, region or bucket name in a public repository is a head start for anyone probing it. Every
value below is a placeholder.

The commands are PowerShell. `$P` is the project, `$R` its region, `$S` the service and `$B` its bucket:

```powershell
$P = '<project id>'; $R = '<region>'; $S = '<service name>'; $B = '<bucket name>'
gcloud config set project $P
```

**On Windows, set `CLOUDSDK_PYTHON` first, or gcloud will not run.** The Store `python` alias intercepts it and every
command dies with *"Python was not found"*, which reads like a broken gcloud install rather than a PATH problem. Point
it at the interpreter bundled with gcloud:

```powershell
$env:CLOUDSDK_PYTHON = '<gcloud install>\platform\bundledpython\python.exe'
```

Nothing else is needed locally. **The image is built by Cloud Build, not on your machine**: `--source .` uploads the
tree (less `.gcloudignore`) and builds remotely, so no Docker engine is required.

## 1. APIs

```powershell
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com `
  secretmanager.googleapis.com logging.googleapis.com monitoring.googleapis.com --project $P
```

## 2. The runtime account

Cloud Run runs as the project's default compute account, `<project number>-compute@developer.gserviceaccount.com`:

```powershell
$N = gcloud projects describe $P --format='value(projectNumber)'
$SA = "$N-compute@developer.gserviceaccount.com"
```

## 3. Secrets

One per credential, each readable by the runtime account. The Gemini key belongs to the project it was issued in,
so its usage and billing stay there unless a new one is made in `$P`; the Qloo and Parallel keys are not Google
Cloud's and move as they are.

| secret | env var | reaches |
| :--- | :--- | :--- |
| `polson-agent-key` | `POLSON_AGENT_PLATFORM_KEY` | the engine's `ApiKeys:GoogleAgentPlatform`, **and** the agent's `GOOGLE_API_KEY`; the API gate's token is derived from it (§5) |
| `polson-parallel-key` | `POLSON_PARALLEL_KEY` | `ApiKeys:Parallel`: research and fact checks. **Without it research is silently disabled** |
| `polson-qloo-key` | `POLSON_QLOO_KEY` | `ApiKeys:Qloo`: audience taste |

```powershell
foreach ($s in 'polson-agent-key', 'polson-parallel-key', 'polson-qloo-key') {
    Get-Clipboard | gcloud secrets create $s --project $P --replication-policy automatic --data-file=-
    gcloud secrets add-iam-policy-binding $s --project $P --member "serviceAccount:$SA" --role roles/secretmanager.secretAccessor
}
```

(Copy each value to the clipboard before its turn of the loop, or run the two lines once per secret.) A new version of
an existing secret is `gcloud secrets versions add <secret> --project $P --data-file=-` the same way.

- **The grant is per secret.** A new secret does not inherit an old one's bindings, and a deploy that maps it accepts
  the flag and then fails minutes later.
- **Secrets are stored verbatim.** The pipe above ends the value with a newline, as does a console paste. The model
  client tolerates it and the entrypoint strips whitespace from every credential before either half of the studio
  reads it, and says so in the boot log (`trimmed whitespace from POLSON_QLOO_KEY`).
- **Never type a value at `--data-file=-` with nothing piped in.** gcloud then reads the console: Ctrl-Z, Enter ends it
  in PowerShell, Ctrl-D in bash, and Ctrl-Z in bash *suspends* the process, which looks exactly like a hang.
- **Keep a key file out of the working tree.** `.gitignore` covers `**/*appsettings.json` but nothing about a stray
  `.key`, which is one `git add -A` from being committed.

**For a public service behind basic auth only:** `polson-basic-auth`, holding `user:password`, the shared sign-in for
everyone who opens it (§5). Create it the same way. A private or an open service does not need it.

**Optional — Nemotron, for `Video.ask` and the first reading of `Documents.ask`.** Nemotron 3.5 Super VL runs on
NVIDIA's NIM API or Nebius Token Factory, not Google. With neither key, `Video.ask` refuses every call with
`NotConfigured`, `Documents.ask` reads with Gemini alone, and nothing else changes. Either key turns both on; with
both, the preferred service is tried first and the other is its backup.

**A deployment uses Token Factory, not NIM.** The engine prefers NIM by default, which suits local development, but
NIM's free tier is governed by the [NVIDIA API Trial Terms of Service](https://assets.ngc.nvidia.com/products/api-catalog/legal/NVIDIA%20API%20Trial%20Terms%20of%20Service.pdf)
(v. September 19, 2025, read 2026-10-21), and a deployed service is outside them:

- §1.2 and §1.4: trial use only — "internal testing and evaluation purposes, not in production" — without a paid
  subscription.
- §4.2: the API Service and its Generated Content may not be made available to others, which is what a service that
  judges or the public can drive does.
- §3.3(iv): NVIDIA may use what is sent and returned to improve its products, AI models included, so a supplied
  document that is confidential must never reach it. §2.6(a) and §4.3 exclude confidential, personal and regulated data
  outright.
- The rate limit is unpublished and the responses carry no rate-limit headers; developer-forum reports put it at
  40 requests a minute per key, shared by every visitor.

So on Cloud Run:

- **Token Factory**: create a `polson-nebius-key` secret, add `POLSON_NEBIUS_KEY=polson-nebius-key:latest` to
  `--set-secrets`, and add `POLSON_NEBIUS_BASE_URL=<your Token Factory base URL>` **and
  `POLSON_NEMOTRON_PREFER=nebius`** to `--set-env-vars`. The base URL has no default, because the
  host is regional.
- **Leave `POLSON_NVIDIA_NIM_KEY` unset.** Kept as the backup, NIM would serve every read Token Factory failed — the
  same terms, reached whenever the paid service has a bad minute. With no backup, a failed Token Factory read falls
  back to Gemini for documents, and `Video.ask` answers with the failure.
- NIM on a deployment only under a paid NVIDIA subscription: then create a `polson-nvidia-nim-key` secret the same
  way and add `POLSON_NVIDIA_NIM_KEY=polson-nvidia-nim-key:latest` to `--set-secrets`. Its base URL defaults to
  `https://integrate.api.nvidia.com/v1`.

## 4. The bucket

One per service, private, readable and writable only by the runtime account. Bucket names are global, so choose
one that is not derived from the project id:

```powershell
gcloud storage buckets create "gs://$B" --project $P --location $R --uniform-bucket-level-access --public-access-prevention
gcloud storage buckets add-iam-policy-binding "gs://$B" --member "serviceAccount:$SA" --role roles/storage.objectAdmin
```

It holds two things, and both are needed, because they save different things (§12, *a run's record*):

- `POLSON_ARTIFACT_SERVICE_URI=gs://$B`: ADK's own artifact store, the versioned renders the *model* is shown. Without
  it the store is `file://` and dies with the instance.
- `POLSON_MIRROR_URI=gs://$B/studio`: `src/adk_agent/mirror.py` copies the project directory a *person* opens
  (`artifacts/`, `scripts/`, `events/`, the authored `.md` and `.js` files) every `POLSON_MIRROR_SECONDS` (20) while a
  run is in flight. **Continuously rather than at the end, because the end may never arrive.** `documents/`, the
  director's own supplied material, is excluded by two independent rules; see the module docstring.

They share the bucket and must not share a prefix.

**Two services never share a bucket.** The studio lists every run under `POLSON_MIRROR_URI` and restores whichever
one is opened (`src/studio/archive.py`), so a shared mirror shows each service's visitors the other's work. A
separate prefix would fix only that: `POLSON_ARTIFACT_SERVICE_URI` reads the bucket name and drops any path, so two
services with a project of the same name would write the same artifact versions.

**Before deleting a bucket, repoint or delete every service that names it.** A deleted name can be created again
by anyone, and a service still configured for it would then write its runs into their bucket.

## 5. The service

The service URL contains the project number, and `POLSON_SELF_URL` must be that URL: pointing it anywhere else
leaves the keepalive quietly holding nothing, and a run nobody watches stops (`src/adk_agent/keepalive.py`). The URL
is fixed per service, project and region, not per revision, so a redeploy keeps it.

```powershell
$URL = "https://$S-$N.$R.run.app"
gcloud run deploy $S --source . --project $P --region $R `
  --no-allow-unauthenticated --cpu 4 --memory 4Gi --max-instances 1 --timeout 3600 --session-affinity `
  --set-secrets "POLSON_AGENT_PLATFORM_KEY=polson-agent-key:latest,POLSON_PARALLEL_KEY=polson-parallel-key:latest,POLSON_QLOO_KEY=polson-qloo-key:latest" `
  --set-env-vars "^;^POLSON_SELF_URL=$URL;POLSON_ARTIFACT_SERVICE_URI=gs://$B;POLSON_MIRROR_URI=gs://$B/studio;POLSON_ASK_SECONDS=60;POLSON_MAX_CREDIT_SECONDS=900;POLSON_BUDGET_TOKENS=4500000;POLSON_REQUEST_TIMEOUT_SECONDS=300;POLSON_QLOO_BUDGET=360;POLSON_ASSETS_BUDGET=30;POLSON_RESEARCH_PROCESSOR=core;ADK_MAX_LLM_CALLS=500;POLSON_PEEK_STABLE_NAME=1;POLSON_ALLOW_ORIGINS=http://127.0.0.1:8080,http://localhost:8080"
gcloud run services add-iam-policy-binding $S --project $P --region $R --member "serviceAccount:$SA" --role roles/run.invoker
```

- `^;^` makes `;` the separator, because `POLSON_ALLOW_ORIGINS` contains a comma (§8).
- `--session-affinity` keeps a browser on the instance running its run: Cloud Run sometimes starts a second instance
  despite `--max-instances 1`, and without affinity the run page's requests went to the new one, which knew nothing
  of the run (§12).
- `POLSON_ALLOW_ORIGINS` is what lets a run start through the proxy (§9).
- The invoker grant is what lets the keepalive reach a private service.
- `--max-instances 1`, because projects and ADK sessions live in the container: a second instance serves an empty
  list, and the run brakes below are counted per instance.

This deploy is **private**: only accounts with `roles/run.invoker` reach it, so it runs without basic auth and needs
no `polson-basic-auth` secret. The startup log says `basic auth OFF`, which is expected here.

The image carries everything the engine shells out to, built and smoke-tested by `tools/bootstrap.py` during the
build, so a missing piece fails the build rather than a visitor's run: the mesh library and MediaPipe venv (about
400 MB, `--build-arg POLSON_VISION=0` leaves them out), and the speech backend (`python-speech/`, CPU-only torch and
the 378 MB wav2vec 2.0 aligner, about a gigabyte, `--build-arg POLSON_SPEECH=0` leaves it out). Without the speech
backend `Motion.speech` cannot time an English narration line, and every narrated English film fails there.

### A public deploy

A public service is open to anyone with the URL, and behind it an agent that spends metered models, a Qloo key and
a research key on whatever a visitor types. It is opened one of two ways.

**Open, with no sign-in**, which is what a hackathon entry needs: judges are given a URL and nowhere to put a
password. Then nothing stands between a visitor and a run but the spend brakes — `POLSON_MAX_RUNS_PER_DAY` (25) and
`POLSON_MAX_CONCURRENT_RUNS` (2), counted in memory per instance (another reason for `--max-instances 1`), and each
run's `POLSON_BUDGET_TOKENS`. Set those to what you will pay for a day before opening it, and put a budget on the
billing account. The brakes are global, so a stranger who finds the URL can spend the day's runs before a judge
does; finished runs stay readable when the allowance is gone.

ADK's own API is served beside the studio and checks no allowance: `/run_sse` alone would start runs past it. On
Cloud Run `src/adk_agent/api_gate.py` answers it, the console and everything else outside the studio and `/new` with
a 404, and the console is off. The service's own requests (the run launch, the keepalive) carry a token derived from
the agent key, one-way, so every instance of a deployment shares it and the key itself is never sent; rotating the key
rotates the token. The startup log must say `API gate on`. Deploy as above, then:

```powershell
gcloud run services add-iam-policy-binding $S --project $P --region $R --member allUsers --role roles/run.invoker
```

**Behind basic auth.** Create the `polson-basic-auth` secret first (§3), then deploy in two steps:

1. Deploy as above, still with `--no-allow-unauthenticated`, but with
   `POLSON_BASIC_AUTH=polson-basic-auth:latest` added to `--set-secrets`. The startup log must then say
   `basic auth on: every request needs the password for user '…'`. A value that is not `user:password` stops the
   revision starting, so the last good one keeps serving.
2. Only once the log says so, open it with the same `allUsers` binding.

Never the other way round: a public revision without the secret is open to everyone.

Every route then needs the shared `user:password`: the studio, `/new`, ADK's API and its console at `/dev-ui/`.
Judges need only the user name and password. The keepalive sends the same credential with its own request
(`src/adk_agent/keepalive.py`), so runs nobody is watching keep their CPU.

**To close a public service again**, remove the binding:

```powershell
gcloud run services remove-iam-policy-binding $S --project $P --region $R --member allUsers --role roles/run.invoker
```

### More than one service from one image

Two entries that differ only in configuration — one reading films with Gemini, one with Nemotron — are two
services on the same image, each with its own `$S`, `$B` (§4) and `POLSON_SELF_URL`. Deploy the first with
`--source .`, then the second from the image the first built, with `--image` in place of `--source .`:

```powershell
$IMAGE = gcloud run services describe $S --project $P --region $R --format='value(spec.template.spec.containers[0].image)'
```

`POLSON_VERIFY_READER=nemotron` makes Nemotron the reader of finished films; unset, it is Gemini. Each service keeps
its own run allowance, so two services can spend twice what one can.

## 6. The routine deploy

From the repository root, for an ordinary code change:

```powershell
gcloud run deploy $S --source . --project $P --region $R --quiet
```

**It is short because that is what makes it safe.** For an existing service, a deploy that passes no env, secret or
sizing flags **keeps all of them**: every variable, every secret mapping, the CPU, memory, timeout, concurrency,
max-instances, affinity and IAM bindings. Restating them is how you lose one. It takes several minutes: Cloud Build
compiles the .NET engine, then builds the Python image and runs the bootstrap's smoke tests.

**A new revision cuts off every run in progress**, an env change as much as a code change. Wait for runs to end
(`run.end` in the run's `events/agent.jsonl`, mirrored to the bucket) before deploying. The run is not lost — the
mirror has it — but it does not continue.

## 7. Configuration

Two routes, and they are not interchangeable.

**The .NET engine reads `appsettings.json` and nothing else.** That file is **not in the image**:
`docker-entrypoint.sh` writes it at boot from `POLSON_*` variables, so a setting the entrypoint does not know about
cannot be set at all. The variables are tabulated in `src/adk_agent/README.md`; `test_entrypoint.py` fails if the
engine gains a setting the container cannot write.

**The Python half reads environment variables directly** and never opens that file: `POLSON_MODEL`,
`POLSON_BUDGET_TOKENS`, `POLSON_BUDGET_RAW_TOKENS`, `ADK_MAX_LLM_CALLS`, `POLSON_MAX_RUNS_PER_DAY`,
`POLSON_ALLOW_ORIGINS`, `POLSON_ASK_SECONDS`, `POLSON_MAX_CREDIT_SECONDS`, `POLSON_PEEK_STABLE_NAME`, among others.

To change one value:

```powershell
gcloud run services update $S --project $P --region $R --update-env-vars POLSON_ASSETS_BUDGET=30
```

**`--update-env-vars` merges; `--set-env-vars` replaces every variable**, so use the second only when restating the
full set. `--remove-env-vars NAME` drops one.

Worth setting deliberately rather than leaving at defaults:

- **`POLSON_ASSETS_BUDGET`**: image generations. The default of 120 is **per server run**, which here means per
  container, shared by every project until the instance recycles.
- **`POLSON_BUDGET_TOKENS`**: the spend ceiling per run, in **billable** tokens (cached input at a tenth). 4.5M
  since 2026-10-29: at 3M a one-audience trailer (Intolerable Cruelty) had spent 2.58M by its encoded film, and a
  three-audience one (barbershop) ran out at its boards with each turn resending 509k tokens. The intake
  scales it for workflows that need more (`TOKEN_SCALE` in `src/adk_agent/intake.py`): a pitch, a report and a maths
  explainer get twice the base, a narrated report four times.
- **`POLSON_BUDGET_RAW_TOKENS`**: the runaway guard, in raw input. **Leave it unset.** It is then 8 × each run's own
  spend cap (`RAW_CAP_MULTIPLE` in `src/adk_agent/studio.py`). Two limits because a stuck agent resends a
  near-identical prefix, which caches well, so a cost-weighted count alone discounts a loop exactly when it is worst.
  A fixed value does not scale with the intake: set to 24M under a 3M base, it halted a pitch with a 6M cap at 24.4M
  raw and 4.6M billable (the Trello run, 2026-10-29).
- **`POLSON_MAX_RUNS_PER_DAY`** / **`POLSON_MAX_CONCURRENT_RUNS`**: the intake brake, 25 and 2. They matter when the
  service is public (§5).
- **`POLSON_ASK_SECONDS`**: how long the agent waits on a question it asked the director, default 120. The wait is
  credited back to the token deadline and the role clock, so raising it costs the agent no budget, but it **cannot**
  be credited back to the sandbox's own `Stage.elapsedMinutes`. Raise it for a demo somebody is watching; leave it low
  for an unattended run, where a long timeout only makes a run sit still. The public service uses 60: long enough
  for a visitor browsing other pages to come back and find a question, short enough that one who has left loses a
  minute rather than five.
- **`POLSON_MAX_CREDIT_SECONDS`**: how much director-waiting an invocation may have back in total, default 300.
  **The ceiling exists because crediting moves the breaker's anchor and not the wall, and Cloud Run kills a request at
  3600 s whatever the breaker thinks.** Uncapped, a workflow that asks at every turn into an empty room drifts far
  enough that the platform kills the run mid-flight rather than the breaker halting it cleanly. Set it to `0` for a
  fully unattended deployment.
- **`POLSON_MAX_MODEL_CREDIT_SECONDS`**: how much of the model's failed attempts (a 504 timeout, a 429, and the backoff
  after each) an invocation may have back, default 1800, apart from the director's allowance above. Each retry also
  shows on the request page as *The model timed out (504). Asking again …*. A Trello pitch lost 25–30 of its 90 minutes
  to nine 504s before this; past the ceiling the clock runs, so a model that never answers still ends in a halt.
- **`POLSON_REQUEST_TIMEOUT_SECONDS`**: how long one model call may take before it is abandoned and retried, default
  180; **set to 300 here**. A long run's turns grow with its context: anthropic_s_1 (2026-10-30, ~280k tokens) answered
  in 108–111 s and then 504'd at exactly 180 s on attempt after attempt, each retry starting the turn over. 300 s still
  turns a hang into a retry within five minutes.
- **`POLSON_PEEK_STABLE_NAME`**: save every peek under one artifact name per file type rather than the render's own
  path, on since 2026-10-20 (`0` turns it off). ADK writes the artifact *name list* into the instructions, the head of
  the prompt-cache prefix, so each new name re-bills the whole conversation at full price while a new *version* costs
  nothing. Cache misses had taken 57% and 62% of two drawing runs' budgets.
- **`POLSON_RESEARCH_PROCESSOR`**: the Parallel processor research runs on; `core` (about $0.025 a run) is enough.

**A warm instance for a judging window.** Scaled to zero, the first visit waits for a cold start, long enough for a
visitor to take the site for down. Keep one instance up for the window, at the service level so no revision is deployed
and a running request is untouched, and take it off afterwards; it bills an idle instance around the clock:

```powershell
gcloud run services update $S --project $P --region $R --min=1          # the window opens
gcloud run services update $S --project $P --region $R --min=default    # the window closes
```

Use `--min`, not `--min-instances`: the second is set on the revision and deploys a new one. CPU during a run needs no
setting of its own: the keepalive (`src/adk_agent/keepalive.py`) holds a request open while a run works, so
`--no-cpu-throttling` would only add cost.

## 8. Two commas, two traps

Both bite on the same character, in opposite directions.

**PowerShell reads `a,b` as an array** and joins the elements with a space before handing them to a native
command. Quote the whole value, as §5 does. Unquoted, gcloud reports `Invalid secret spec` with the two entries run
together by a space.

**gcloud splits env-var values on commas.** When the comma is part of the *value*, change the delimiter with a `^;^`
prefix:

```powershell
--update-env-vars "^;^POLSON_ALLOW_ORIGINS=http://127.0.0.1:8080,http://localhost:8080"
```

Without it you get one variable holding the first origin and a second bogus one named after the second: no error,
and the symptom appears somewhere else entirely.

**`--set-secrets` replaces the entire mapping** like `--set-env-vars`: name every secret in one flag, or the
container boots without the ones left out. `--update-secrets` merges.

## 9. Reaching a private service

gcloud's proxy handles the sign-in and serves on `http://localhost:8080`:

```powershell
gcloud run services proxy $S --project $P --region $R --port 8080
```

**Set `POLSON_ALLOW_ORIGINS`, or every form submission through the proxy is refused.** ADK's CSRF middleware compares
the request `Origin` with an allowed list, and through the proxy the origin is `http://127.0.0.1:8080`. The symptom
misleads: the form loads and submitting it returns `Forbidden: origin not allowed`, with nothing in the log, because
the refusal happens in middleware. A public service reached at its own URL needs none of this.

Stop the proxy when you are done: while it runs, anything on the machine can reach the service through it.

For one request without the proxy:

```powershell
curl.exe -H "Authorization: Bearer $(gcloud auth print-identity-token)" "$(gcloud run services describe $S --project $P --region $R --format='value(status.url)')/health"
```

## 10. Verifying a deploy

```powershell
gcloud run services get-iam-policy $S --project $P --region $R --format="value(bindings.members)"
```

Only the runtime account (and any you granted) means private. **`allUsers` means public.**

```powershell
gcloud run services describe $S --project $P --region $R --format=export
```

Check the variables survived, every secret is still mapped, and `run.googleapis.com/sessionAffinity: 'true'`. Keep a
copy of this export outside the repository: it is what recreates the service as it was.

Then the boot log, where the entrypoint says what it configured:

```powershell
gcloud logging read "resource.labels.service_name=$S" --project $P --limit 50 --format="value(textPayload)" --freshness=15m
```

Expect `entrypoint: settings written -> ApiKeys:GoogleAgentPlatform, ApiKeys:Parallel, ApiKeys:Qloo, …` (key names
only; values never appear), `engine=/app/bin/cli/Polson.CLI.dll (present)`, `API gate on`, and the keepalive's
`keepalive holds https://… while a run works` naming this service's own URL. The token limits are logged on the
**first run** rather than at boot: `token budget: <n> billable input tokens per invocation (runaway guard: <n> raw)`.

## 11. Stopping and rolling back

**Cloud Run has no stop verb.** A service with no traffic scales to zero, but a run inside a live container keeps going,
and the keepalive keeps its instance. To end one, force a new revision; the old instance drains:

```powershell
gcloud run services update $S --project $P --region $R --update-env-vars "POLSON_STOPPED_AT=$((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHHmmZ'))"
```

Remove the marker afterwards with `--remove-env-vars POLSON_STOPPED_AT`. The drained instance can stay up to the
request timeout (3600 s) if its keepalive hold was open when the revision changed; it costs, but runs nothing new.

**Deleting a request** has no button. A request is two folders in the bucket, the mirror the studio lists and ADK's
artifact versions, and both go:

```powershell
gcloud storage rm -r "gs://$B/studio/<name>/" "gs://$B/<name>/"
```

An instance that is up keeps its own copy on its disk, restored from the mirror when it started, and goes on listing
the request until it is replaced: force a new revision as above. Nothing brings a deleted request back.

A failed revision never takes traffic, so a broken deploy leaves the previous one serving. To go back to an earlier
one:

```powershell
gcloud run services update-traffic $S --project $P --region $R --to-revisions=<revision>=100
```

## 12. Things that will surprise you

**A run's record dies with the container, and this is not hypothetical.** Projects live on the instance's ephemeral
disk. Two runs were lost this way on 2026-09-08; the second, `kubrick1`, was twenty minutes in when Cloud Run replaced
the instance underneath it. The mirror (§4) is the fix: the studio lists every run in `POLSON_MIRROR_URI`, and opening
one whose directory is gone downloads it back onto the container. The trace, the renders, the scripts and the
delivered documents all work. What is gone is the **conversation**: ADK sessions are in memory, so a run can be read
but not resumed. By hand:

```powershell
gcloud storage cp -r "gs://$B/studio/<project>" ./projects/ --project $P
```

The mirror needs Application Default Credentials, which the container has from the metadata server. Locally, with
`POLSON_MIRROR_URI` set, it fails at `storage.Client()` with `DefaultCredentialsError` until
`gcloud auth application-default login`; `gcloud auth login` authenticates only the CLI. Unset locally, it is simply
not registered.

**`--max-instances 1` caps the steady state, not the transition.** During an instance replacement, and at times
under load ("Starting new instance. Reason: AUTOSCALING"), the service runs two instances for minutes. Without
session affinity the run page 404'd while the run was alive on the other instance, and the keepalive's hold, landing
on the newcomer, was refused, so the run lost its CPU. Affinity (§5) keeps the browser on the run's instance; the
keepalive keeps the affinity cookie from a hold that reached its own instance and asks again when it reaches a sibling.
If a run vanishes from the list, check this first:

```powershell
gcloud logging read "resource.type=cloud_run_revision AND resource.labels.service_name=$S AND textPayload:`"Starting new instance`"" --project $P --freshness=1d
```

Check memory and CPU before blaming the container: for `kubrick1` they were 19% and under 4%, the platform recycling
the instance rather than anything the run did. The per-turn `turn polson/…` lines survive in Cloud Logging and carry
each turn's timing and token counts; with the mirror, they are what is left of a lost run.

**The run page holds a request open for the whole run.** Its event stream is a long-lived request, and the page also
polls and fetches artifacts. At a concurrency of 4, two watchers and an inspector exhausted the one instance and Cloud
Run returned 429 to everything; it looked exactly like a hung agent. Leave concurrency at Cloud Run's default, which is
far above that; these connections are idle, not CPU-bound.

**There is no `exec`.** A Cloud Run *job* on the same image runs arbitrary commands, but in a separate container: it
answers "what is in this image", never "what is in the running instance's project directory".

## 13. Monitoring

`monitoring/` holds the log-based metrics, alert policies and an uptime check, exported without project ids, service
names or addresses: each file has placeholders that `apply.py` fills in. Make an email notification channel in the
console (Monitoring › Alerting › Edit notification channels), then:

```powershell
python deploy/monitoring/apply.py $P $S "projects/$P/notificationChannels/<id>" "$S-<hash>-<region code>.a.run.app"
```

The last argument is the service's host, from its URL without `https://`; leave it out to skip the uptime check.

| What | Fires when |
| :--- | :--- |
| A single turn over 300k input tokens | A run's context has grown past the point where each turn is slow and costly. |
| Circuit breaker halted a run | A run hit its token or time budget and was stopped. |
| Model turns over 150 in an hour | Something is calling the model far more than a run should. |
| Request rate spike | Over 2 requests a second for 5 minutes: scraping or enumeration, which the turn alerts never see. |
| Memory over 85% for 5 minutes | The instance is near its limit. |
| Billed continuously for 4 hours | The service has been billed nearly every minute for 4 hours. |
| Studio home page is down | More than one checker region has failed for 5 minutes. |

**Every filter names one service.** All six alerts watched `polson-studio` until 2026-10-30, a day after the entry
moved to a new service, so nothing watched the live one. A new service means running `apply.py` for it, or editing
the existing filters to name it.

**A minimum instance trips the billing alert.** With `--min=1` (§7, for a judging window) the service is billed around
the clock, so "billed continuously" fires every four hours. Leave that one on the old service, or disable it, until the
minimum comes off.

Budgets belong to the billing account, not the project, and are not here.

## 14. Locally

`gcloud config set project $P`, and anything that names the old project: the assistant's memory notes, and the
`cloud-studio-proxy` entry in `.claude/launch.json`.
