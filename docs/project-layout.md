# Project Directory Layout

The contract between `polson create-project` (.NET), the MCP server (.NET), and the demo
webapp (Python). Everything about one design project lives in one directory, so a project is
replayable, resumable, readable after the fact, and movable as a single artifact.

## The rule that shapes everything

**The filesystem is the record, not the transport.**

In the standalone profile the Python orchestrator and the web server are the same process, so
the agent session is an asyncio task the web server already holds. Sending the director's
message is `conversation.send(...)`; answering a question is resolving a `Future`. Neither needs
a file. In the managed profile there is no Python at all — Antigravity Desktop is the transport.

What both profiles share is that everything worth keeping gets **written down**. That is what
survives a page refresh, a second viewer, an orchestrator restart, and the end of the session.
Designing the files as a transport as well would buy nothing and cost a concurrency model.

## Layout

```
<project>/
  project.json              manifest: id, workflow, sdk, profile, schema version, created (UTC)
  brief.md                  the director's brief — see "Trust boundary" below
  <instructions>            agent instructions, rendered from the workflow template
  <wiring>                  MCP server wiring
  <permissions>             the host's own permission file

  artifacts/                renders written by the MCP server's outFile; served to the browser by URL
  scripts/                  every JS execution, one file per call — the readable trace

  events/                   append-only JSONL, exactly one writer per file
    server.jsonl              .NET MCP server        (every run)
    agent.jsonl               Python orchestrator    (orchestrator runs only)
    director.jsonl            director actions       (orchestrator runs only)

  run.log                   human-readable rolling log

  ── every Antigravity project; unused when a desktop host is the one running ────
  agent.config.json         tool policy and session directories for the orchestrator

  session/                  SDK-owned; never hand-edited, safe to delete to force a cold start
    save/                     LocalAgentConfig.save_dir     — conversation state, enables resume
    appdata/                  LocalAgentConfig.app_data_dir — agent scratch and media
```

The three angle-bracketed names are what the **SDK** decides, and they are the only thing that
differs between an Antigravity project and a Claude Code one — the contents are the same either way:

| | `agy` (Google Antigravity) | `claude` (Claude Code) |
| :--- | :--- | :--- |
| instructions | `GEMINI.md` | `CLAUDE.md` |
| wiring | `.agents/mcp_config.json` | `.mcp.json` |
| permissions | `.agents/settings.json` | `.claude/settings.local.json` |

The **workflow** picks the templates those instructions render from: `logo` for a client design
project, `harness` for an SDK-evaluation run that also asks the agent for a `findings.md`,
`comic_studio` for a four-role studio reproducing a reference image.

A workflow whose name ends in **`_studio`** is multi-agent. That is a convention for the reader; what
the generator actually keys on is whether the workflow ships a `roles/` directory, which is
checkable rather than inferred from a name. A multi-agent workflow additionally emits its role specs,
and — for Antigravity, which has somewhere to put them — a `.agents/agents.json` registry derived
from those files, so the registry cannot name an agent whose prompt is missing. Claude Code has no
registry, and needs none: the same roles run as sequential personas from the same files. The
optional **type** selects a section within that workflow, and *what a type means is the workflow's
business* — for `harness` it is the task (`image`, the default, or `logo`); for `logo` it is the
stylistic frame of Manual 12 §2.5b (`geometric`, `modern`, `antique`).

A type never sets the **archetype**. That is Stage 4's structural choice, and the manual is explicit
that it reads better once there are candidate forms to look at, so a command line must not settle it
before anything has been drawn.

Both vocabularies are **discovered from the embedded templates** — a workflow is any directory with
an `instructions.md`, and its types are its `type.<name>.md` files — so adding either is adding a
file rather than editing the generator. The only workflow-specific knowledge left in code is which
workflows have a default type.

One paragraph of the harness instructions is supplied by the generator rather than the template,
because the isolation rule is backed differently on each host — Claude Code's permission rules take
paths, Antigravity's name tools and commands only — and a harness that claims an enforcement it does
not have measures nothing.

`--brief` and `--prompt` are two ways into the same channel: `--brief` reads a **file**, `--prompt`
takes the **text**. Both are sanitised and quoted into `brief.md` between the markers, and giving
both is refused rather than silently dropping one. Nothing supplied on the command line reaches the
instructions as instruction.

`--brief` is a path and only a path. It previously accepted either, falling back to treating the
argument as the brief text — so a mistyped path quietly *became* the brief, and the agent read its
client brief as `C:\typo\brief.txt` with nothing to show anything had gone wrong. A path argument
that takes text on failure cannot report a typo, because a typo is indistinguishable from a short
brief.

`.agents/` is Antigravity's canonical workspace configuration directory — the wiring sits alongside
`settings.json`, `rules/` and `skills/` there. The host also reads a copy at the project root, but
only as a fallback for generic MCP tooling, so the generator writes one file. The orchestrator still
looks in both places, because projects generated earlier put it at the root.

Getting this wrong leaves the agent with no MCP server **and no error** — which is how it presented:
an agent reporting it had no `polson:*` tools, with nothing in any log to say why.

`session/` is SDK-shaped internally (`appdata/brain/<conversation_id>/…`). Keep it in its own
subtree so its layout can change without touching ours, and `.gitignore` it — it is regenerable
state, sometimes large, and never the deliverable.

## Events

One writer per file is the whole concurrency model. Nothing merges on disk; readers merge on
read, ordering by `ts` and tie-breaking on `(src, seq)`. That removes interleaved-write races
without a lock, and it means a crashed writer truncates its own file and nobody else's.

```json
{"ts":"2026-08-28T14:22:31.442Z","seq":17,"src":"server","type":"render","script":"scripts/0004.js","artifact":"artifacts/stage2.webp","ms":214}
```

| `src` | `type` | Status |
| :--- | :--- | :--- |
| `server` | `run.start` `run.end` `script.start` `script.ok` `script.error` `render` `render.novector` `stage.begin` `stage.continue` `stage.end` `note` `tool.error` | **written** by `RunEventLog` |
| `server` | `expect` `check` | **written** — the agent's claim about a render, and how it turned out |
| `server` | `observe` `inspect` `artifact.read` | **written** — what a script measured, what it found, and which earlier artifact it opened |
| `server` | `asset.requisition` `asset.refused` `budget` | **written** — collected by `RequisitionScope` in `Polson.ExtendedMind` and drained by `DrawingMcpTools` |
| `agent` | `run.start` `run.end` `turn.start` `thinking` `tool.call` `text` `compaction` `turn.end` `usage` | **written** by the orchestrator's `Transcript` |
| `director` | `message` `question` `answer` | **written** by the orchestrator's `Director` |
| `director` | `brief` `cancel` | specified; needs the webapp |

`scripts/` is populated by the server, not the agent: every executed script is saved there in order
and named from the event that references it. The server has the text already, so recording it there
costs nothing and keeps a run reconstructible even when the agent never wrote anything down.

Each `script.start` is closed by exactly one `script.ok` or `script.error`, including when a tool
call throws rather than returning — a refused output path, for instance. A start without a
terminator means the process died mid-script, and should be read that way.

`run.start` and `run.end` bracket the server's lifetime. `run.end` is written from
`ApplicationStopping`, which is reached when the server's standard input ends — the only shutdown
signal the stdio transport has.

**`run.end` is therefore best-effort, and its absence is not evidence that a run died.** A host that
force-kills the server instead of closing its stdin leaves no closing bracket at all. The Antigravity
SDK closes it properly — a live run on 2026-08-28 left a complete bracket — but the reference MCP
client for .NET does not: disposing it waits its five-second shutdown timeout, never closes stdin,
and then kills. Both halves are pinned by `StdioTransportTests`, which is why the closing event is
asserted against a directly-driven process rather than through that client.

Two things follow, and they are the orchestrator's to honour:

- **Shut the server down by closing its stdin and waiting for exit**, not by killing it. That is
  what makes `server.jsonl` complete, and it costs one flush.
- **Record the run's completion in `agent.jsonl` regardless.** The orchestrator owns the child
  process, so it always observes the exit even when the server got no chance to say so itself. A
  reader should treat the orchestrator's closing event as authoritative and `run.end` as
  corroboration.

`tool.error` records a failure in any tool other than `ExecuteScript`, which has its own
`script.error`. A tool that fails and is worked around otherwise leaves the record claiming nothing
went wrong.

## Stage and execution

Two optional fields sit between `type` and the payload, and both exist so a viewer can group without
joining:

- **`execution`** — identifies one tool call. Every event it produced shares it, so a render ties
  back to the `script.start` that made it without matching on script paths. Returned to the agent as
  `executionId`, so it can cite a specific render.
- **`stage`** — the agent's declared stage, set from a script with `Stage.begin(...)`. It **spans
  executions**: it lives on the session and is re-read on every call, because a property pushed onto
  the ambient log context inside an async tool handler never reaches the next request. Filtering the
  log by `stage` yields every artifact produced during it.

  A stage runs from its `stage.begin` to the matching `stage.end`. `stage.continue` in between is the
  agent restating the stage it is already in — an announcement at the top of a script — and does not
  break the group. Restating was previously a full end/begin cycle, which chopped one stage into
  several and hid the real transitions among them.

The same three properties (`Project`, `Stage`, `ExecutionId`) are pushed onto the Serilog context per
call, so ordinary log lines carry the same attribution when debugging.

A stage is a **claim**, not a fact — the agent names its own. That is the point: it records intent,
which is what makes drift visible when the intent and the artifacts disagree. It also means this log
is not evidence against the agent; the machine-recorded fields beside it are.

The `agent` vocabulary deliberately mirrors the SDK's own `StepType`, so the orchestrator
transcribes `receive_steps()` rather than inventing a parallel taxonomy. Log `compaction` even
though it is invisible in the UI — it is the only thing that explains an agent apparently
forgetting its own earlier decisions.

**Transcribe a step when it settles, never when it arrives.** `receive_steps()` re-yields a step as
it accumulates, and steps *interleave* — a thinking step and the tool call after it alternate, each
re-yielded several times, before either finishes. Treating "a different step arrived" as "the
previous one is done" duplicates everything: a first run recorded 17 `tool.call` events for the 6
scripts `server.jsonl` shows actually ran. `status` is the signal — `ACTIVE` while accumulating,
`DONE` when final — and anything still unfinished when the turn ends is written then and marked
`partial`. A step that failed carries its own `status` and `error`; it is not a `turn.end`, because
the SDK retries a 429 and carries on.

The two writers produce byte-identical line shapes — compact separators, no ASCII escaping, `\n`
endings on every platform — so one `grep '"type":"render"'` spans all three files. The record is
meant to be read by people as well as parsed.

Artifacts are referenced by **path, never by bytes**. Base64 in an event log destroys the log's
readability and re-sends the image on every replay.

Multi-agent runs split the per-agent files by id (`server.<agent>.jsonl`), preserving the
one-writer rule. Single-agent runs stay flat.

## Trust boundary

`brief.md` holds visitor-supplied text. The instructions file is generated from a workflow template
and is trusted. **They are separate files, and the instructions reference the brief rather than
inlining it** — so the boundary between "instructions we wrote" and "text a stranger typed" is a
file boundary, which is checkable, rather than a paragraph break, which is not.

Nothing derived from visitor input may influence the wiring, `agent.config.json`, hook commands,
policy entries, or any path. Those come from the template alone. The project id is validated to
`[A-Za-z0-9._-]` before it becomes a directory name.

## Denied tools

Generated into `agent.config.json` (standalone) and the managed profile's settings:

| Tool | Profile | Why |
| :--- | :--- | :--- |
| `GENERATE_IMAGE` | **both** | Bypasses `Polson.ExtendedMind` entirely — no budget, no content-addressed cache, no form-versus-substance classifier. An agent that can generate a finished picture directly dissolves the project's central claim, that the model supplies material and code supplies form. This is an integrity control, not a security one, and it is the most important line the generator emits. |
| `RUN_COMMAND` | standalone | Two reasons that happen to agree. A public URL must not reach a shell (§6); and the instructions forbid reaching the drawing engine or the docs through a command line, which this is the profile that can *enforce* rather than request. Polson's own CLI can draw and can search, and a script run that way leaves an image with no record of how it came to exist. |

The names above are the `BuiltinTools` **member** names, for readability. The generator emits the
enum's **values** — `generate_image`, `run_command` — which is what the SDK matches on.

`CREATE_FILE` and `EDIT_FILE` are **not** denied, and an earlier version of this table wrongly
implied they were. The harness workflow writes `findings.md`, so denying them would break it. What
constrains them is `workspaces=[project.root]` in `LocalAgentConfig`, which confines file access to
the project directory rather than removing the tools — and the agent's own scratch goes to
`session/appdata/` via `app_data_dir` regardless.

## Profiles

**Every Antigravity project has the same file set, and either host can run it.** The profile is a
label for what the project was generated for, not a gate on what it can do.

This has changed twice, and the reasons are worth keeping. First both profiles emitted identical
files and differed only in a field inside one of them, which nothing enforced and no one could see.
Then standalone became a strict superset, adding `agent.config.json` and `session/` — which made the
file set legible but meant **choosing at generation time how the project would be run**, and getting
it wrong was only discovered later, when the orchestrator refused the project and it had to be
regenerated to flip a flag. The file set is now uniform and the runtime decides.

- **managed** (default) — generated for a desktop or IDE host, which runs the agent and renders
  `ask_question` in its own UI. That host owns tool policy while it is the one running.
- **standalone** (`--standalone`) — generated for the Polson orchestrator, which hosts the agent
  itself: it applies the deny policies, registers an `OnInteractionHook` for the director, and owns
  `session/` and the `conversationId` that makes a run resumable.

Either project runs either way. What differs is which event files get written — a desktop host
produces only `events/server.jsonl`, because the other two are the orchestrator's.

### Two policies, one file set

The file set is uniform; the tool policies are not, and must not become so. `run_command` is denied
because **a public URL must not reach a shell** — a fact about the runtime, not about the file set.
Each policy file is read by exactly one runtime:

| File | Read by | Denies |
| :--- | :--- | :--- |
| the host's settings | a desktop host, driven by a person at a keyboard | `generate_image` |
| `agent.config.json` | the orchestrator, which serves the web app | `generate_image`, `run_command` |

So the denial that matters arrives with the runtime that needs it. Copying the standalone denials
into the host file would take the shell from a developer in their IDE for a reason that does not
apply to them; leaving them out of `agent.config.json` would hand a visitor a shell.

`agent.config.json` carries `"readBy": "polson-orchestrator"` because it now ships in projects a
desktop host will run, where it is inert — and a policy file that reads as enforcement while
enforcing nothing is this project's oldest trap. The field says which runtime reads it rather than
leaving that to be inferred from its presence.

`project.load` still refuses a project with **no** `agent.config.json` rather than running with an
empty deny list, which would silently permit `generate_image`. That now means a project generated
before this change, or one for another host — not a managed one. Regenerating adds it; no flag is
needed.

`claude --standalone` is refused at generation, and a Claude project gets no `agent.config.json`,
`session/` or `conversationId`: the orchestrator builds Antigravity SDK configurations only, so
those files would be ones nothing ever reads.

`AgentBehavior` is not this axis. It distinguishes *is a human attached* — a standalone run with a
terminal or browser attached is `INTERACTIVE`; harness and unattended runs are `AUTONOMOUS`.

## Resume

`session/save/` plus the `conversation_id` in `project.json` restore a conversation after the
orchestrator restarts. Resume replays `events/` to rebuild the browser view and hands the SDK the
saved conversation to rebuild the agent's — two independent reconstructions of the same run, which
is why both must be written down rather than held in memory.
