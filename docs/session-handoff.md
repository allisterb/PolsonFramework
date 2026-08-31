# Session Handoff — 2026-08-31

State after the session that built the standalone web app (Milestone 6, phase 2 complete), added
inspection events to the engine, and implemented Davis's creative sense-making curve over a real run.

**Tests: 938 .NET, 178 Python — all passing.** Last commit `980ca11`; uncommitted after it are
this handoff, a moved `appsettings.json.example` (now at the repository root), and a further
`inf-1` run under `tests/agent/`.

---

## The reframing that happened

Polson stopped being "an MCP server plus prompts that Claude or Gemini drives" and became a
**standalone co-creative studio**. A visitor needs a brief, a workflow, and the willingness to say
"make the background green". Not JavaScript, not which model is behind it, not that there is a model.

The boundary that makes this true already existed: a **managed** project carries no
`agent.config.json`, so its host owns tool policy and `project.load` refuses to run it; a
**standalone** project carries its own policy, instructions and session directories. The orchestrator
supplies the human, and the browser supplies the human's hands.

A second reframing, from Davis's *Quantifying* deck: the sense-making curve is not only a viewer for
the director. It is meant to be **machine-consumed** — the agent reading its own interaction model
and adapting. We serve two of his four purposes (analysis, explainability) and not the other two
(adaptability, partnership). That is written down in `docs/creative-sense-making.md` §7.

---

## 1. The web app (`src/webapp/studio/`)

```bash
./polson_webapp projects          # browser, http://127.0.0.1:8000
./polson_run projects/acme        # one turn in the terminal, you as the director
```

The argument means different things — a directory *of* projects versus *one* project — which is why
they are separate scripts rather than one with a flag.

| Route | Does |
| :--- | :--- |
| `GET /` | Brief form; project list with `has a session` / `not started` |
| `POST /projects` | `create-project` as a subprocess → optionally start |
| `POST /runs` | Start a run on an existing project |
| `GET /runs/{id}` | The page: curve, trace, render, script |
| `GET /runs/{id}/events` | SSE — replay then live tail |
| `POST /runs/{id}/answer` | `WebDirector.reply()` |
| `POST /runs/{id}/say` | Mid-run interjection, via an SDK trigger |
| `GET /runs/{id}/artifacts/{path}` | Renders, path-contained |
| `GET /runs/{id}/scripts/{name}` | Source, pygments-highlighted |
| `GET /runs/{id}/curve` | The coded run |

**The demonstration that matters.** Mid-run, the director typed *"instead if yello could you use a
green background"*. 93 seconds later the agent opened a new stage with:

> `Settling Design Language: Neo-brutalist green-bar line printer aesthetic.`

It did not swap a hex value. It reinterpreted the conceit — **green-bar paper** is the real
alternating-band continuous-feed stock, historically correct for the printout the piece imitates, and
the bands appear in the render. The interjection was made sense of against the evolving artifact
rather than executed. That is Davis's "disruption as creative catalyst", observed.

It also finished the script it was mid-way through rather than abandoning it, and applied the change
at the next point where colour was actually decided.

---

## 2. Inspection events (.NET)

The record was **only ever writes**: scripts executed, artifacts rendered, stages declared. It said
what an agent did and never what it perceived — half of the perception–action loop, and the half that
says whether an action was informed or blind.

`ProbeScope` (in `Polson.Runtime`, reachable from both drawing layers) adds two events:

- **`inspect`** — a per-execution tally by kind: `measure`, `sample`, `compare`, `capability`, `read`.
  Counts, not events, because one `getPixel` loop runs thousands of times.
- **`artifact.read`** — one pass reading what an earlier pass left, by the same path the `render`
  event named, so the two join. The only direct evidence in the record that coordination happened
  through the environment.

Three things had to be right or the tally would be worthless: `attr()`/`transform()` call `getBBox`
internally (split the counting boundary from the computation), `Has()` delegates to `Resolve()`
(count once), and probes are written in `finally` so a script that looked and then crashed still
reports what it saw.

---

## 3. Creative sense-making (`orchestrator/csm.py`, `docs/creative-sense-making.md`)

Davis's framework in a medium he did not have. His **categories** are domain-independent and adopted
unchanged; his **coding table** is drawing-specific and re-derived for scripts.

| Interaction mode | Value | Polson |
| :--- | ---: | :--- |
| Communicate | +1 | `note`, `stage.*`, director turns |
| Gather | +1 | asset requisition, `Search` |
| Inspect | +0.5 | `inspect`, `artifact.read`, `view_file` |
| Wait | 0 | `thinking`, gaps |
| Execute | −1 | `script.ok`/`script.error` **that produced a render** |

**`server.jsonl` is the spine** — the MCP server writes it under every host — and the transcript is
host-specific *enrichment*. The three transcript formats differ in kind, not field names: ours and
Claude's are typed; Antigravity's records a tool call as a `GENERIC` step whose content is rendered
prose. So the medium-specific half needs no adapter at all.

**Two readings, which disagree in sign on real runs.** Counting actions gave `+92.5`; counting time
gave `−1159.7`. Eighty-three notes occupied eight seconds between them while eleven executions took
thirty-one minutes. Reporting one number would have been confidently wrong, so `summary()` reports
both plus `heldMs` per mode, and each gets the x-axis that matches it.

Corroborated on two sources for the scale, and the structural argument settles it: only under the
2025 table does a cumulative curve rise while regulating and fall while producing. Under the 2017
scale `execute = 0` and producing would not move the curve at all.

---

## Bugs found and fixed, worth remembering

**`TypeError: cannot pickle '_thread.lock' object` — every browser run failed at startup.**
`Agent.__init__` does `config.model_copy(deep=True)`, and hooks and triggers are fields on it, so
everything reachable from them is copied. `WebDirector` held `broker.publish`, a bound method whose
`__self__` is a `Broker` holding a lock. `EventLog` already carried a `__deepcopy__` guard **and its
docstring names this exact error**; three new handle objects were added on the same path without it.
Terminal runs never broke, because a terminal director holds only a log.

**A second run replayed the first run's events as its own.** A project's `server.jsonl` is appended
to across runs, and the tailer followed from byte zero. The offset is now fixed *synchronously* at
`start()`, not on the first poll.

**A resumed run spent a full session being told the work was done.** `project.json` records a
`conversationId`; `run_turn` defaults `resume=True`; the web layer passed nothing. Given the opening
prompt, the agent correctly answered "the project has completed all 7 stages". One place now decides
what a blank prompt means, because it means opposite things.

**The Antigravity hook, after three sessions.** The CLI log at
`~/.gemini/antigravity-cli/log/cli-*.log` had the answer the whole time — `hooks_manager` says what
loaded, `command_hook_executor` gives the command's stderr verbatim. Hooks were firing all along.
Three separate command-level faults: a live MCP server holding `bin/cli/*.dll` so a build left it
stale; `cmd /c` mangling a command that *begins* with a quoted path; and
`NoDefaultCurrentDirectoryInExePath=1`, which stops cmd finding a bare filename in the working
directory. Generated projects now carry `.agents/preserve-chatlog.cmd` and the hook invokes
`.\preserve-chatlog.cmd` — no quotes, no spaces, no path.

**`--reset` silently downgraded a standalone project to managed**, rewriting `project.json`, dropping
`agent.config.json` from the plan, and taking `session/` out of `.gitignore` — which exposed SDK
session state. A reset clears the run; it does not decide what the project is.

**`polson_webapp.cmd` failed with `'M' is not recognized`** — an em dash in a `REM` comment. Batch
files must be ASCII; all four `.cmd` files are now clean.

---

## Environment traps that cost real time

- **`bash` heredocs silently eat backslashes**, even with a quoted delimiter. It corrupted a C#
  string literal and three Python ones this session. Build escapes from `chr(92)` or use the Write
  tool; never a heredoc for content containing `\n` or `\\`.
- **Extensionless scripts need a `.gitattributes` line each.** No pattern catches them, they fall
  back to `* text=auto`, and on Windows that means a CRLF shebang and `bad interpreter`. `build`,
  `polson`, `polson_webapp`, `polson_run` are listed; a new one gets no protection until added.
- **`git-bash /tmp` and Python's `/tmp` are different directories** (`C:\Users\…\Temp` vs `C:\tmp`).
- **The credential lives in `bin/cli/appsettings.json`**, which is build output: gitignored, never
  committed, and never restored by a fresh clone. There is deliberately **no** `GEMINI_API_KEY`
  override — the .NET MCP server reads only that file, and two halves of one studio cannot have two
  answers to "which key".

---

## Where to pick up

**Immediately next — two UI changes the director asked for, mid-sentence when the session ended:**

1. **Collapse `thinking` entries by default**, with click to expand. They are verbose and they bury
   the trace.
2. **Make the right pane show *any* execution's artifact**, not just the latest render. Clicking an
   `ExecuteScript` entry should show that render *and* its script together — the script panel already
   does half of this.

**Then, in rough order of value:**

- **`artifacts read back: none`** across a whole run. The stigmergy column is still empty: a single
  agent holds its own context so it never needs to look back. Worth pushing in the workflow templates
  — looking is what makes the trace legible to anyone else.
- **Group the trace by server session.** The stage list reads `Data → … → Encode → Data → … → Encode`
  when a project has run twice. `polson report` counts sessions; the page does not use it.
- **Builder vocabulary in a visitor's view** — the front page shows `sdk: agy`, `profile: standalone`.
- **Multi-agent.** The record is shaped for it (`src`, `depth`, `trajectory`); the runner is not.
  Two curves on a shared axis is where *participatory* sense-making becomes visible, and it is the
  claim the theory rests on. Choose it deliberately; it is the largest remaining piece.
- **Adaptability** — the agent consuming its own curve. `docs/creative-sense-making.md` §7 names the
  gap; the 2014 Creative Trajectory Monitor is the precedent.

**Also open:** the devpost draft (`docs/devpost/Polson.md`) still carries Camel leftovers — "a DFIR
investigation", "the Camel JavaScript interpreter", a link to `allisterb/Camel`, "case session",
"When an investigation starts" — plus `bake-report` (the verb is `report`), `create-poject`, a
missing `eval`, and six sentences that stop mid-thought. The director is editing it.

**One unexplained flake:** `Polson.Tests.Drawing` failed once in a full-solution run and passed on
five consecutive re-runs. The only output was a deliberate negative test's log line. Not reproduced,
not diagnosed.
