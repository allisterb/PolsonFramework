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
  project.json              manifest: id, workflow, profile, schema version, created (UTC)
  brief.md                  the director's brief — see "Trust boundary" below
  GEMINI.md                 agent instructions, generated from the workflow template
  .mcp.json                 MCP server wiring; --director web|none baked in at generation
  agent.config.json         standalone only: hooks, policies, capabilities for the orchestrator

  artifacts/                renders written by the MCP server's outFile; served to the browser by URL
  scripts/                  every JS execution, one file per call — the readable trace

  events/                   append-only JSONL, exactly one writer per file
    server.jsonl              .NET MCP server        (both profiles)
    agent.jsonl               Python orchestrator    (standalone only)
    director.jsonl            director actions       (standalone only)

  session/                  SDK-owned; never hand-edited, safe to delete to force a cold start
    save/                     LocalAgentConfig.save_dir     — conversation state, enables resume
    appdata/                  LocalAgentConfig.app_data_dir — agent scratch and media

  run.log                   human-readable rolling log
```

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
| `server` | `run.start` `run.end` `script.start` `script.ok` `script.error` `render` `stage.begin` `stage.continue` `stage.end` `note` `tool.error` | **written** by `RunEventLog` |
| `server` | `asset.requisition` `asset.refused` `budget` | specified; requisition lives in `Polson.ExtendedMind` and is not yet wired |
| `agent` | `turn.start` `thinking` `tool.call` `text` `compaction` `turn.end` `usage` | specified; needs the Python orchestrator |
| `director` | `brief` `message` `question` `answer` `cancel` | specified; needs the webapp |

`scripts/` is populated by the server, not the agent: every executed script is saved there in order
and named from the event that references it. The server has the text already, so recording it there
costs nothing and keeps a run reconstructible even when the agent never wrote anything down.

Each `script.start` is closed by exactly one `script.ok` or `script.error`, including when a tool
call throws rather than returning — a refused output path, for instance. A start without a
terminator means the process died mid-script, and should be read that way.

`run.start` and `run.end` bracket the server's lifetime. **A log with no `run.end` is a run that did
not finish** — the process was killed, or is still going. Without it a reader cannot tell a completed
run from an abandoned one, since the file simply stops in both cases.

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

Artifacts are referenced by **path, never by bytes**. Base64 in an event log destroys the log's
readability and re-sends the image on every replay.

Multi-agent runs split the per-agent files by id (`server.<agent>.jsonl`), preserving the
one-writer rule. Single-agent runs stay flat.

## Trust boundary

`brief.md` holds visitor-supplied text. `GEMINI.md` is generated from a workflow template and is
trusted. **They are separate files, and `GEMINI.md` references the brief rather than inlining
it** — so the boundary between "instructions we wrote" and "text a stranger typed" is a file
boundary, which is checkable, rather than a paragraph break, which is not.

Nothing derived from visitor input may influence `.mcp.json`, `agent.config.json`, hook commands,
policy entries, or any path. Those come from the template alone. The project id is validated to
`[A-Za-z0-9._-]` before it becomes a directory name.

## Denied tools

Generated into `agent.config.json` (standalone) and the managed profile's settings:

| Tool | Profile | Why |
| :--- | :--- | :--- |
| `GENERATE_IMAGE` | **both** | Bypasses `Polson.ExtendedMind` entirely — no budget, no content-addressed cache, no form-versus-substance classifier. An agent that can generate a finished picture directly dissolves the project's central claim, that the model supplies material and code supplies form. This is an integrity control, not a security one, and it is the most important line the generator emits. |
| `RUN_COMMAND` | standalone | A public URL must not reach a shell (§6). |
| `CREATE_FILE` / `EDIT_FILE` | standalone | Constrained to the project directory; the agent's own scratch already goes to `session/appdata/` via `app_data_dir`. |

## Profiles

Both emit the same core — `project.json`, `brief.md`, `GEMINI.md`, `.mcp.json`, `artifacts/`,
`scripts/`, `events/`. They differ only in who supplies the human:

- **managed** — Antigravity Desktop hosts the agent and renders `ask_question` in its own UI.
  No Python, no hooks, no `agent.config.json`. Only `events/server.jsonl` is written.
- **standalone** — the orchestrator registers a web-backed `OnInteractionHook`, applies the deny
  policies, and sets `AgentBehavior.INTERACTIVE`. All three event files are written.

`AgentBehavior` is not this axis. It distinguishes *is a human attached* — both profiles above are
`INTERACTIVE`; harness and test runs are `AUTONOMOUS`.

## Resume

`session/save/` plus the `conversation_id` in `project.json` restore a conversation after the
orchestrator restarts. Resume replays `events/` to rebuild the browser view and hands the SDK the
saved conversation to rebuild the agent's — two independent reconstructions of the same run, which
is why both must be written down rather than held in memory.
