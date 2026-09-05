# Agentic Cinema hackathon — feasibility assessment

> **Status as of 2026-09-05: eligible, technically feasible, four days left. Two of the four items
> are built.** The ADK entry point and container are done and the studio is **deployed and running on
> Cloud Run**, where it has produced real work. What remains is Parallel's Search API — the track's
> hard requirement, and the only item that gates eligibility — public access to the URL, mounting the
> studio UI, and the video. §4 has the detail.
>
> This document is the cold-start brief for a session picking it up, and it carries the verification
> behind each claim so nothing has to be re-litigated.

> [!IMPORTANT]
> **This header said "No work started" until 2026-09-05, three days after it stopped being true**, and
> a session reading it stated as fact that the ADK entry point was missing. §4 said "Four items, none
> started" beneath a tree containing `main.py`, a `Dockerfile`, and four working apps. A cold-start
> brief that is wrong about the state of the work is worse than no brief, because it is trusted.
> **If you build one of these items, update §4 in the same session.**

---

## 1. The contest

- **Devpost**: `https://agentic-cinema.devpost.com` · rules at `/rules`
- **Contest Period**: opens 09:00 PT **2026-07-27**, closes **14:00 PT 2026-09-09**
- **Judging**: 2026-09-23 → 10-07. Winners notified ~10-07.
- **Prize per track**: 1st $7,500 · 2nd $4,500 · 3rd $3,000. Five tracks, $75,000 total.
- **Team size**: max 4.

**The brief, verbatim:**

> Build a functional, production-ready AI agent or multi-agent network—powered by Gemini and Google
> Cloud Agent Builder—that integrates a Partner Entity's product or MCP server to solve critical
> bottlenecks across the entertainment and media value chain, specifically targeting the workflows of
> filmmakers, screenwriters, studio crews, or fans (each a "Project").

**Judging criteria, all equally weighted (25% each):**

1. **Technological Implementation** — how well built, how effectively it uses Google Cloud and the Partner services.
2. **Design** — *"a complete, coherent product experience not just a technical proof of concept."*
3. **Potential Impact** — a credible case for a real problem, and a solution that addresses it.
4. **Quality of Idea** — *"a creative, non-obvious use of Google Cloud and Partner services."*

**Chosen track: Parallel.** Its requirement is *"Must use Parallel's Search API at runtime"*, which is
where the director wanted it anyway — as the retrieval half of the Extended Mind (`CLAUDE.md` §2,
"Agents must actively query external knowledge bases"). That is a genuinely non-obvious use and lands
directly on criterion 4.

**Submission requirements:**

- A **hosted project URL** for judges to test.
- A **public repo** (GitHub/GitLab/Bitbucket) with an OSI-approved licence, demonstrating Google Cloud
  and Partner services **at runtime in actual code**.
- A **demo video, 3 minutes maximum**, on YouTube or Vimeo, English or English-subtitled.
- Text description: features, functionality, technologies, data sources, learnings.

---

## 2. Eligibility — cleared, with the checks

Every row below was verified rather than assumed. **One of them corrects an error I made:** I first
told the director this was blocked because Polson was "months of committed history" and therefore an
extension of existing work. That was wrong and he caught it.

| Rule | Verdict | How it was checked |
| :--- | :--- | :--- |
| *"Projects must be newly created by the entrant during the Contest Period."* | **Passes** | `git log --reverse` → initial commit **2026-08-23**, inside the period that opened 07-27. 112 commits over 11 days. |
| *"…not a modification or extension of Your or anyone else's existing work."* | **Passes** | Polson is an original project. NuGet dependencies (Jint, SkiaSharp, Svg.Skia, Magick.NET) are libraries, not prior work being extended. |
| Submitted to another hackathon concurrently | **No such rule** | Asked the rules page directly; it returned *"No rule exists."* Polson is also an All Things Agentic entry (`CLAUDE.md` §1) and nothing prohibits that. |
| Publicly released before the period | **No such rule** | Same query, same answer. |
| Only Google Cloud AI at runtime | **Passes already** | `Polson.ExtendedMind/ImageGeneration/ImageGenerator.cs` targets `gemini-2.5-flash-image` and `gemini-3-pro-image`. |
| Public repo, OSI licence | **Passes** | AGPL-3.0. |

**The AI restriction, verbatim, and its scope:**

> Projects may only use Google Cloud artificial intelligence tools … and the built-in AI-powered
> features of the specific Partner's product relevant to your chosen track. No other AI models, agent
> frameworks, or AI APIs are permitted, regardless of vendor — this includes but is not limited to
> AWS, Microsoft, OpenAI, and Anthropic AI tools.

**It applies to the submitted project, not to tools used during development** — confirmed by a targeted
query. So building Polson with Claude Code is fine; what matters is what ships.

> [!IMPORTANT]
> **The consequence for the harnesses.** The submission's agent must be ADK + Gemini. The Antigravity
> SDK is a Google product but is *not* obviously on the Cloud services list the rule points at, and
> the rule separately names `google-adk` among the permitted SDKs. **Use ADK and do not put Antigravity
> in the deliverable.** The Claude Code `.mcp.json` harnesses under `tests/multi_agent/` are
> development tooling and do not ship.

---

## 3. ADK — what was verified in the source

`reference/projects/adk-python-2.8.0`, ledgered 2026-09-02 (see `reference/README.md`). **Scanned
clean**: no suspicious codepoint classes; ten injection-phrase hits, all inspected and all ordinary
agent-SDK vocabulary. **Apache 2.0.** Install `google-adk` from PyPI with a hash — do not build from
the copied tree, same handling as the Antigravity SDK row.

**Two findings decide feasibility.**

### 3a. ADK speaks stdio MCP natively — `bin/cli` plugs in unchanged

`src/google/adk/tools/mcp_tool/` ships a first-class `McpToolset` accepting `StdioConnectionParams`
(which spawns a `command` + `args` subprocess) or `StreamableHTTPConnectionParams`.

```python
McpToolset(connection_params=StdioConnectionParams(
    server_params=StdioServerParameters(command='/app/bin/cli/Polson.CLI')))
```

The whole .NET execution engine — Jint sandbox, SkiaSharp, the drawing toolkits, the manuals as
`polson://manual/*` resources — arrives behind that one object. **Milestones 1–3 need no rewriting.**

### 3b. Agent Engine deploys a container, not a pickled agent

This was the risk, and it clears. `src/google/adk/cli/cli_deploy.py`'s `to_agent_engine` builds from
**the same `_DOCKERFILE_TEMPLATE` as `to_cloud_run`**, differing only in `command='api_server'` and the
session/memory service URIs. A container means we control what is in it, so a .NET runtime alongside
Python is a packaging problem rather than a blocker.

**The constraint that follows:** the generated template is `python:3.11-slim`, and no `--base-image`
or bring-your-own-Dockerfile flag exists on any of the three `adk deploy` commands. So `adk deploy`
cannot be driven end to end — we supply the image.

### 3c. That is not a workaround. `gcloud run deploy` with our own Dockerfile is the documented path

> **Correction, 2026-09-03.** §3b above framed the bring-your-own-image route as a fallback forced by
> a missing flag. The director pointed at ADK's own Cloud Run guide, and it is better than that: a
> user-authored Dockerfile is a **first-class, documented deployment method**, not an escape hatch.
> The guide's words are that it *"requires more manual setup compared to the `adk` command but offers
> flexibility, particularly if you want to embed your agent within a custom FastAPI application."*
> Source: `https://adk.dev/deploy/cloud-run/#python---gcloud-cli`. The deploy guides are **not** in the
> ledgered source tree — they live on the docs site — so this row is fetched documentation rather than
> read source, and is marked as such.

The mechanism is `google.adk.cli.fast_api.get_fast_api_app(...)`, **verified present in the ledgered
2.8.0 tree** (`src/google/adk/cli/fast_api.py:95`). It returns an ordinary `FastAPI` object, which the
guide's `main.py` then serves under plain `uvicorn`. The layout is a normal container project:

```
main.py            # calls get_fast_api_app(agents_dir=..., web=True) -> FastAPI
requirements.txt   # ours: src/adk_agent/requirements.txt
Dockerfile         # ours: multi-stage, .NET runtime + Python + ADK + bin/cli
capital_agent/     # -> our agent package
```

```bash
gcloud run deploy <service> --source . --region $GOOGLE_CLOUD_LOCATION   --project $GOOGLE_CLOUD_PROJECT --allow-unauthenticated   --set-env-vars="GOOGLE_CLOUD_PROJECT=...,GOOGLE_CLOUD_LOCATION=...,GOOGLE_GENAI_USE_ENTERPRISE=..."
```

**Three consequences, and the third is the one worth having.**

1. **The image is entirely ours**, so .NET runtime + SkiaSharp natives + `bin/cli` alongside Python is
   a Dockerfile we write rather than a template we fight. `--source .` builds via Cloud Build, and a
   `Dockerfile` in the directory takes precedence over buildpacks.
2. **`get_fast_api_app` takes far more than the guide shows.** The 2.8.0 signature carries
   `url_prefix`, `lifespan`, `allow_origins`, `session_service_uri`, `logo_text` / `logo_image_url`
   and `web: bool`. `lifespan` is the hook that starts the MCP server as a co-process if stdio
   spawning turns out to be constrained in the sandbox — which §5 lists as unverified, and this is its
   remedy.
3. **It collapses submission requirement #3 into the same artifact.** `src/webapp` is already FastAPI.
   *"Embed your agent within a custom FastAPI application"* is exactly that case: one container can
   serve the Polson studio UI at `/` and the ADK API under `url_prefix`, so the hosted judge URL and
   the agent runtime stop being two deployments. That feeds criterion 2 (Design, 25%) directly.

### 3d. Cloud Run is explicitly sanctioned. The hosting question is closed

§3c left open whether *"powered by Gemini and Google Cloud Agent Builder"* constrained the hosting
surface as well as the SDK. **It does not.** Two independent checks, both against the contest's own
pages rather than inference:

**The resources page lists Cloud Run under deployment, describing our exact case.** Under the heading
*"🚀 Phase 5: Deployment & Safety"*:

> **Logic Hosting: Cloud Run Quickstart** — Fast, serverless deployment for custom agent backends,
> APIs, and tool servers.

A custom agent backend with a tool server is a literal description of ADK + `bin/cli`. Agent Engine
appears too (Phase 4, *"Deploying ADK Agents to Agent Engine"*), so both are sanctioned and the choice
is ours.

**The rules impose no hosting surface on this track.** The only platform requirement is
*"A submitted Project must run on at least one of the following platforms: web, Android, or iOS."*
The rules **do** mandate a hosting surface for exactly one track — Replit, whose projects *"must be
hosted and deployed directly on Replit … Projects not deployed on Replit's platform will not meet this
requirement"*. That they named one where they meant one, and named none for Parallel, is the argument:
the silence is deliberate rather than an omission.

> [!NOTE]
> **Provenance.** §3c and §3d rest on fetched web pages, not on the ledgered source tree. The
> `get_fast_api_app` mechanism in §3c *was* verified in source; the deployment guidance and these rules
> quotations were not, and cannot be. Re-check before the submission if anything hinges on them.

**A credential convergence worth noting.** The resources page's Phase 1 points at the *"Gemini
Enterprise Agent Platform API"*, and ADK's own Dockerfile template sets `GOOGLE_GENAI_USE_ENTERPRISE=1`.
The only key on this machine — `ApiKeys:GoogleAgentPlatform` in `bin/cli/appsettings.json`, prefix
`AQ.A`, not a public `AIza` Gemini key — is an Agent Platform credential, which
`src/webapp/orchestrator/credentials.py` already documents as requiring the enterprise endpoint. So the
credential we hold is probably the one this contest expects. **Probably: untested against ADK.**

---

## 4. What is missing, in the order to attempt it

**Updated 2026-09-05.** Two done, two not, and one of the two done is done only in part.

### Built

1. ~~**ADK entry point + container.**~~ **Done.** `src/adk_agent/main.py` calls
   `get_fast_api_app(...)` and returns an **ordinary `FastAPI`**, with a `Dockerfile` at the repo
   root. It was the item that might have been *blocked* rather than laborious; it was not. The
   Dockerfile records every deploy failure beside the line that fixes it.
2. ~~**A hosted project URL.**~~ **Deployed, with two gaps** — see below. The studio runs on Cloud
   Run as `polson-studio` (project `polson`, region `us-east4`) and **has produced real work there**;
   four apps exist under `src/adk_agent/apps/`. `src/webapp` turned out to matter less than feared,
   because a plain `FastAPI` app means the studio UI mounts on the same app and port rather than
   needing its own service.
   > **`src/adk_agent/README.md` is the deployment brief** — read it before touching the service.
   > Some environment facts live only in session memory rather than the repo, notably that `gcloud`
   > on the build machine needs `CLOUDSDK_PYTHON` set or every command dies with *"Python was not
   > found"*, and that `--set-env-vars` splits on commas so a value containing one is silently torn
   > in half.

### Remaining

1. **Parallel Search API at runtime.** The track's **hard requirement**, so it is the only item that
   gates eligibility — and it is untouched. Checked 2026-09-05: the sole "parallel" matches in
   `src/adk_agent` are a warning against ADK's `ParallelAgent`, which is unrelated.
   `Polson.ExtendedMind` remains the right home; it is already built around an `IImageGenerator`
   seam, so there is an established shape for a second external service.
   > This is also the piece the **infographics direction** needs — retrieving real-world figures to
   > construct the points a graphic makes — so the eligibility requirement and the differentiator
   > demo are one task rather than two competing ones. A run that searches for figures, cites them,
   > and renders a chart whose bars are provably those figures satisfies both.
2. **Public access to the URL.** Deployed **private** — no `--allow-unauthenticated` — so a judge
   cannot open it, and "a hosted project URL for judges to test" is a submission requirement. **Not
   merely a flag:** `CLAUDE.md` Milestone 6 requires per-session and per-day caps on concurrency and
   asset-requisition spend before a public URL drives a metered image service. Open the caps first,
   then the door.
3. **Mount the studio UI.** `main.py` says plainly: *"Nothing is mounted yet; that is the next piece
   of work. Until then this is `adk web` with a seam."* So what a judge would currently reach is
   **ADK's own developer interface**, not a product. That is criterion 2 (Design), which asks for
   *"a complete, coherent product experience not just a technical proof of concept"* and is 25% of
   the score — the single place where the gap between what exists and what is judged is widest.
4. **The 3-minute video.**

### Already satisfied, worth not re-checking

- **Public repo with an OSI-approved licence.** `LICENSE` is **AGPL-3.0**, which is OSI-approved, and
  the remote is `github.com/allisterb/Polson`. **Whether that repo is public was not verified** —
  confirm before submitting.

### The open question that shapes everything else

**Polson is a graphic-design studio; the brief is film.** The honest bridge is **storyboarding and
previz**, and it is a real one rather than a stretch — the manuals already teach cinematography:

- **Manual 06** — eye level, vanishing points, one eye level per picture
- **Manual 07** — three-point lighting, cast shadows, key/fill ratios
- **Manual 09** — shot composition, value hierarchy, leading the eye
- **Manual 03/08/19** — figures, hands, expression for character staging

A storyboard agent that reasons in *procedures it can adapt* rather than in prompts is exactly the
enactive argument in `CLAUDE.md` §1, aimed at a filmmaker's workflow. **Decide this before writing
code** — it shapes the demo and the video more than the implementation does.

---

## 5. Risks

- **Six days**, and items 2–4 are the expensive ones.
- **Criterion 2 (Design) is 25%.** A half-working demo site costs a quarter of the score. If `src/webapp`
  is further from done than it looks, that is the item to cut scope around.
- **Image size and cold start.** .NET runtime + SkiaSharp natives + Python + ADK is not a small image.
- **Subprocess in the sandbox.** The stdio transport spawns `bin/cli` as a child process. If the Agent
  Runtime blocks `fork`/`exec`, fall back to running the MCP server as a second process in the
  container and connecting over localhost with `StreamableHTTPConnectionParams` — supported by ADK,
  but a different wiring. **Unverified.**
- **`bin/cli` must be rebuilt.** `docs/agent/session-handoff.md` §8 notes most recent work built with
  `-p:SkipCopyToBin=true` because a live agent session held it open, so `bin/cli` may be stale.

---

## 6. Independent of the contest

The ADK work is worth doing whether or not anything is entered. **Agent Engine deploying a container
means Polson can run as a hosted service with the .NET engine intact** — which is Milestone 5 ("allow
the agentic studio to run outside the Antigravity IDE") and unblocks Milestone 6's demo site. ADK is a
credible second orchestrator alongside Antigravity, and `CLAUDE.md` §3A already anticipates more than
one transport for run events.
