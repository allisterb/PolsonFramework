# Agentic Cinema hackathon — feasibility assessment

> **Status as of 2026-09-03: eligible, technically feasible, six days left.** No work started. This
> document is the cold-start brief for a session picking it up, and it carries the verification behind
> each claim so nothing has to be re-litigated.

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
or bring-your-own-Dockerfile flag was found. So write our own Dockerfile (multi-stage: .NET runtime +
Python + ADK + `bin/cli`) and deploy the image, rather than driving `adk deploy` end to end.

---

## 4. What is missing, in the order to attempt it

Four items, none started.

1. **ADK entry point + container.** Smallest, and **do it first** — it is the only item that could turn
   out to be *blocked* rather than merely laborious, and discovering that on day six is the bad
   outcome.
2. **Parallel Search API at runtime.** The track's hard requirement. `Polson.ExtendedMind` is the right
   home; it is already built around an `IImageGenerator` seam, so there is an established shape for
   adding a second external service.
3. **A hosted project URL.** A submission requirement, not optional. `src/webapp` is ~30 Python files
   with an `orchestrator/` and a `hello_agent.py` — **how far it actually runs was not established.**
   Size this early; it feeds criterion 2 (Design), which is 25% of the score.
4. **The 3-minute video.**

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
