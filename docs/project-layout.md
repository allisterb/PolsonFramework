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

| `src` | `type` |
| :--- | :--- |
| `server` | `script.start` `script.ok` `script.error` `render` `asset.requisition` `asset.refused` `budget` |
| `agent` | `turn.start` `thinking` `tool.call` `text` `compaction` `turn.end` `usage` |
| `director` | `brief` `message` `question` `answer` `cancel` |

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
