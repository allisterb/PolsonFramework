# Session Handoff — 2026-08-31 (second session)

State after the session that made the studio survive contact with a second machine, taught the run
record to say when it was *blocked* rather than merely quiet, and made the sense-making curve
readable by someone who has not read the papers.

**Tests: 961 .NET, 189 Python — all passing.** The previous handoff (Milestone 6 phase 2) is
superseded; its "where to pick up" list is done except for the two items carried forward below.

---

## What this session was actually about

The last one built the studio. This one ran it **somewhere else** — WSL — and almost everything of
value came out of that. A second machine is a test you cannot fake: it found a Windows-only lock
file, a missing native library, a Python floor nobody had stated, and an error message that cost an
agent 27 tool calls. None of those were visible from the machine the thing was written on.

The second theme was **legibility**. A run's record was complete and still unreadable: failures that
coded as nothing, a curve with no axes, prose in a 112px gutter. The fixes are small individually;
together they are the difference between a record that exists and one a judge can read.

---

## 1. Portability — the WSL run

**The Python lock was Windows-only.** `uv pip compile` without `--universal` resolves for the machine
it runs on and emits pins with the environment markers *stripped* — all 49 packages, no markers. So
Linux dutifully tried to install `pywin32`, which has no Linux build at all, and failed. `mcp`
declares it correctly as `sys_platform == 'win32'`; the lock is what lost the condition.
Recompiled with `--universal --python-version 3.13`: same 49 packages, same versions, three markers
gained. The compile command is documented in four places and all four now carry the flag, because
the next regeneration would otherwise reintroduce it silently.

**SkiaSharp has no Linux native in the meta-package.** `bin/cli/runtimes/` carried
`libHarfBuzzSharp.so` for thirteen architectures and `libSkiaSharp` for **osx and win only**. Every
drawing call died in `SKImageInfo`'s type initializer. Fixed by adding
`SkiaSharp.NativeAssets.Linux` — **take that one, not `.NoDependencies`**, which is built without
fontconfig and would silently give an empty font manager: every family substituted, the whole
`LogoType` surface quietly meaningless, no error anywhere.

**A venv can be built with the wrong interpreter and nothing says so.** `python3 -m venv` on Ubuntu
22.04 makes a 3.10 environment, and the failure surfaces minutes later as pip refusing `rpds-py`.
`src/webapp/check_python.py` now runs before the install, with the venv's own interpreter, against
the floor **read from the lock's `uv` header** — so the guard cannot drift from the file it guards.
It caught a real 3.10 venv on its first outing.

---

## 2. One file set, either host

`--standalone` used to decide at generation time how a project could ever be run, and choosing wrong
was discovered only when the orchestrator refused it. Every Antigravity project now carries
`agent.config.json`, so **the same directory runs under a desktop host or the orchestrator**, and
`profile` survives as a label for what it was made for.

The refinement that made it safe: **the file set is uniform, the two policies are not.**

| File | Read by | Denies |
| :--- | :--- | :--- |
| `.agents/settings.json` | a desktop host — a person at a keyboard | `generate_image` |
| `agent.config.json` | the orchestrator — which serves the web app | `generate_image`, `run_command` |

`run_command` is denied because *a public URL must not reach a shell* — a fact about the runtime, not
about the file set. Each file is read by exactly one runtime, so the right policy now arrives with
whoever runs the project instead of being guessed when it was generated. Copying the standalone
denials into the host file would take the shell from a developer in their IDE; leaving them out of
ours would hand a visitor one. A test fails on either mistake.

`agent.config.json` carries `"readBy": "polson-orchestrator"` because it now ships where a desktop
host will ignore it, and a policy file that reads as enforcement while enforcing nothing is this
project's oldest trap.

**A latent bug fell out:** the web page decided runnability from the `profile` label while
`project.load` decided from the file — two answers to one question, already able to disagree. Both
now key on the file.

---

## 3. The record can now say it was blocked

A Linux run met the missing Skia native: nine scripts, four refused, no artifact at all. **The curve
rose smoothly for its whole length** and read as a long, thoughtful regulation phase.

It was not wrong — the agent genuinely never produced anything — but it was *unable to be right*.
`execute` is the only negative value in the table and it needs a render to be assigned, so with no
renders the curve **cannot** fall, and a blocked run looked identical to one that had not started.
The four most informative events in the session were coded as nothing at all.

`attempt` (**0**, its own mode) now codes a `script.error` that produced no render. Flat, because the
artifact did not change and a falling curve would say a broken engine had produced something — but
visible, in its own colour, with a `refused` count that leads the tally. The rule it narrows is
unchanged where it was aimed: a *probe* that drew nothing deliberately is still regulation, already
spoken for by its own `inspect` event.

**`attempt` is ours, not Davis's**, and `docs/creative-sense-making.md` §4 says so explicitly. Every
other row maps one of his modes onto our events.

---

## 4. Diagnostics, which cost a whole run

The Skia failure reached the agent as `The type initializer for 'SkiaSharp.SKImageInfo' threw an
exception.` — and it spent **27 tool calls and a compaction** investigating fonts, Snap versus
Canvas2D, and the SDK documentation. Every one of them unable to help, because the file was not on
disk. `Explain` was returning `ex.Message` and discarding the `DllNotFoundException` underneath.

Three fixes, all in `JsDrawingEngine.Explain`:

- **The cause is found however deep it is buried**, and a native-library failure says plainly that no
  script can work around it, so the next agent stops instead of hunting.
- **`JavaScriptException` now routes through `Explain` too** — it was formatted bare, so *every*
  script error arrived with no line number even though Jint records one. Now `(line 56)`.
- **Jint's overload failure gets a reading.** "No public methods with the specified arguments were
  found" names neither method nor argument; its commonest cause is `undefined` from a property that
  does not exist. A live run lost a 97-line composition to `rect.w` — the toolkit's rectangles carry
  `width` — making `fillRect(x, y, undefined, undefined)`.

---

## 5. The run page

- **A legend** (`what these mean`) defining every event label in a reader's terms.
- **`said` clipped to five lines**, click to expand, in proportional type.
- **A ■ marker** on lines that lead to a picture. First attempt marked every line of a rendering
  pass — truthful and useless at **84 of 96 rows**. Now on `rendered`/`executed` only: 16 of 99.
- **Renders drawn as stemmed marks** on the curve, so production is findable at a glance.
- **Axes** with ticks, labels and units on both readings. The tick rule is ported from
  `ScaleToolkit.NiceStep`, verified to agree with `Scale.ticks` on five intervals.
- **A `how this is coded` disclosure** built from the server's own `scale`, so the legend cannot
  disagree with the curve.
- **Provider failures are labelled as such.** The SDK yields a platform error on the same channel as
  the agent's words, so a 429 was rendering as `SAID`.

**One layout bug worth remembering:** the stage `<span>` was omitted when a row had no stage, so in a
three-column grid the detail landed in the **7rem stage gutter**. Every stageless event — `said`,
`thinking`, `tool`, `director` — had been reading in a 112px column.

**The curve was project-scoped while the trace was run-scoped.** `csm.read` read all three event
files whole, and they are appended to across runs: 393 points spanning 10.4 hours where the run was
101 points over 6.2 minutes. Scoped by a `since` stamped at registration. Redraws are also coalesced
now — they only fired on `script.ok`, so a stage of nothing but notes looked frozen.

---

## 6. `polson report` splits by session

A project's log is appended to across runs, so the stages read
`Data → … → Encode → Data → … → Encode`. The report now breaks it at each `run.start`, with each
session's own counts and stage list, and the per-session counts **sum back to the project totals**.

It immediately surfaced two sessions that had been invisible inside the totals: one that ran 28
seconds and did nothing, and one of 0.0s in `cs-2`.

---

## 7. SnapPaper — eleven duplicates and one real bug

The twelve `CS0114` warnings were verified mechanically, not by eye: normalising `Document`→`Node`
and `this`→`Paper` (identical for a paper, since the constructor passes the document to `base`) made
**eleven character-identical** to the virtuals they hid. Deleted.

**`Clear` was the real one.** The base wipes every child; a paper keeps its `<defs>`. Because it hid
rather than overrode, the same paper either kept its gradients or silently destroyed them depending
on the static type of the reference. Now an `override`, with a test that calls it **through a
`SnapElement`** deliberately — it fails without the fix.

---

## Reference material read this session

Two more Davis-lineage papers, scanned and in the ledger:

- **`p356-davis.pdf`** — *Creative Sense-Making*, C&C '17. The canonical methods paper. It settles two
  things `docs/creative-sense-making.md` had been arguing unaided: the scale is explicitly
  **domain-remappable** (the states are the framework; their placement on the axis is the analyst's
  mapping), and the **sign convention is inconsequential** if consistent. It also lists as *future
  work* the thing our medium gives away free — tagging the continuous curve with the events behind
  it — and records the cost we avoid: **4 analyst-minutes per minute of video**.
- **`3591196.3593514.pdf`** — *Observable Creative Sense-Making (OCSM)*, C&C '23, **CC BY 4.0**, the
  only adaptable paper in `papers/`. **Read it before defending the curve to anyone.** From Magerko's
  own lab, it names three conceptual limitations in CSM. Its applicability clause is what matters
  here: CSM remains valid for collaborations *"that have identifiable behavior markers for the
  corresponding cognitive states"* — and a code medium is the strongest instance of that clause,
  because the markers are machine-recorded rather than inferred from video by a coder.

---

## Where to pick up: multi-agent

This is what the director asked for next, and it is the largest remaining piece.

**What already exists.** `comic_studio` is the multi-agent workflow: four roles
(`01_penciler`, `02_inker`, `03_colorist`, `04_critic`), and `create-project` emits
`.agents/agents.json` naming a Studio Director orchestrator plus one subagent per role, each with the
Polson tools and `view_file`. Every event carries `src`; the transcript carries `depth` and
`trajectory`, which is what separates a delegated subagent's work from the main agent's. `csm.py`
already has `Curve.agents` and `Curve.per_agent()`.

**What does not.** `run_turn` hosts **one** agent. Nothing splits the curve onto a shared axis in the
UI, and nothing computes coupling.

**The recipe is specified**, in `p356-davis.pdf`: the *creative trajectory* is the **elementwise sum
of participants' cumulative integrals**, with trends read off it like stock-market buy/sell/hold
signals. §9 of `docs/creative-sense-making.md` says "one curve per agent, stacked" — that is half of
it; the summed third curve is the other half.

**And a warning, from OCSM.** Its second critique is that CSM treats interaction as a *result* of
individual mental states, where participatory sense-making holds interaction to be an irreducible
unit analysed whole. Two per-agent curves summed is exactly the reduction it objects to. OCSM's
`participation` dimension is the counter-proposal: level 3 is *joint* sense-making, and in our medium
its marker is `artifact.read` — one agent reading what another left.

**Known problems in `comic_studio` before running it** (from the parked `cs-2` review):

- `roles/` numbering contradicts the pipeline: files are `02_inker` / `03_colorist` while the
  instructions mandate **pencil → colour → ink** and explain why.
- `roles/` assumes a painterly subject. A flat-vector reference made `02_inker.md`'s three-tier
  weights, feathering and Ben-Day halftone into instructions to move *away* from the target.
- The agy profile's no-peeking rule is prose in `GEMINI.md` only, with no source-path denies.

### Also open

- **`artifacts read back: none`** across whole runs. `artifact.read` exists and is instrumented; a
  single agent holds its own context, so it never looks back. This is the column the enactive claim
  most needs, and multi-agent is the setting where it should finally populate — one agent reading
  another's render is stigmergy with nowhere else to happen.
- **Newness, from OCSM.** `bitmap.diff` between consecutive renders gives *repeat / slight /
  significant / new* as a measurement, where the dance study could only have a coder's judgement.
  It answers a question the curve cannot: eleven executions that each changed almost nothing look
  identical to eleven that transformed the piece.
- **Adaptability.** Davis names four purposes; we serve analysis and explainability, not
  adaptability or partnership. §7 records the gap.
- **`SKPathBuilder` migration** — nine `CS0618` warnings, no behaviour change, will become errors on
  a future SkiaSharp major.
- **The devpost draft** still carries Camel leftovers.
- **`reference/README.md` is gitignored** (`.gitignore:432`, `reference/*`), so the ingestion ledger
  exists only on this machine. CLAUDE.md loads it labelled "checked into the codebase"; a fresh clone
  has no verdicts, which is the outcome the ledger exists to prevent. One negation line would fix it.

---

## Environment traps confirmed again

- **Read a live run's files by snapshotting them first.** Reading `projects/img-1` mid-run gave a
  5-event file that was 34 events by the next command, and a recode that returned zero points and
  looked like a regression in code I had just written.
- **`bash -c` and heredocs eat backslashes.** A regex built in `python -c` lost its escape and threw
  `unterminated character set`. Build escapes from `chr(92)`, or write the script to a file.
- **Batch files must be ASCII.** `src/webapp/install.cmd` still had an em dash in a `rem`, the same
  thing that broke `polson_webapp.cmd` last session. The four root `.cmd` files were cleaned then;
  this one was missed.
- **`TaskStop` does not kill a uvicorn child.** A test server survived the wrapper and kept serving a
  deleted directory. Check the port, not the task.
