# Session Handoff — 2026-09-01 (fifth session)

State after the session that gave the studio a **window onto runs it does not drive**. It started as
"can we watch a Claude Code run in the browser" and turned into the discovery that most of what the
dashboard needed already existed — and that once you can watch a run, you start finding things.

**Tests: 1,194 .NET, 248 Python — all passing.** The previous handoff is superseded; its open items
are carried forward at the end.

---

## What this session was actually about

**Observability, and what it costs to have none.** Nearly every change below was found by looking at
a live run rather than by reading code:

- A finished picture that 404'd in the render panel, found from a screenshot.
- 850 KB of JavaScript re-sent to change part of a program, found by measuring gaps.
- A subagent that could not write the files its own role spec told it to write.
- An agent deleting its own report because `--reset` had left a stale one.
- An hour lost to a permission prompt nobody could see.

None of those is visible from source. All of them were obvious from the record once there was
somewhere to look at it.

---

## 1. The studio watches runs it does not drive

Work a project in Claude Code or Claude Desktop, press **Watch** on the index, and the same run page
opens over it. Nothing extra is captured to make that work: the MCP server writes `server.jsonl`
whoever drives it, and the `preserve-chatlog` hook already preserved the host's transcript.

| Piece | What it does |
| :--- | :--- |
| `orchestrator/hostlog.py` | Transcribes a host transcript into `agent.jsonl` / `director.jsonl`. Idempotent by the transcript's own `uuid`, with the resume point read back out of the record rather than kept in a state file that could disagree with it. |
| `studio/observe.py` | A `Run` with no task: replays the merged record, tails `server.jsonl`, re-syncs the transcript. Read-only by design. |
| `project.read` | Beside `project.load`. Every check in `load` is a *drivability* check, and applying them to a reader refused the feature's whole subject — a Claude project was turned away with "the orchestrator builds Antigravity SDK configurations only", which is true and beside the point. |

**It reads the host's live store, not the hook's copies.** The hook fires at turn end, and a subagent
can work for forty minutes inside one turn — so a page watching the copies shows the spine live and
the conversation frozen, which is backwards from what a person wants while a stage is quiet. The path
is not guessed: `hooks.jsonl` records the transcript each firing resolved.

Three traps worth not rediscovering:

- **`mcp__polson__ExecuteScript` must be normalised before coding.** `SPINE_OWNED` holds bare names,
  so without stripping the prefix every execution in a host-driven run is coded twice — once from the
  spine, once from the transcript. That is the failure the existing comment warns about, and it would
  have read as a run twice as productive as it was.
- **Whether a `thinking` block carries text is not ours to control.** Measured across 409 transcripts:
  present 63–100% per day through August 2026, absent from the 21st (9 of ~3,300 since), tracking
  cached client feature flags. The block is emitted either way, marked `redacted` when empty — `csm`
  codes `thinking` as `wait` and nothing else does, so dropping the empty ones would take every pause
  out of the curve.
- **Subagent transcripts live outside the parent's.** Claude Code writes them to
  `<session>/subagents/agent-*.jsonl` with a `.meta.json` naming the `agentType`. `preserve-chatlog`
  now copies them; earlier I wrongly concluded from an `isSidechain` field that they were inline.
  They are not — the field is always false in a parent.

**The agent's prose now codes as `communicate`, as the director's does.** One participant speaking
directly to another is communication whichever is speaking; the asymmetry was an artefact of where
the two halves of the record came from. `AskUserQuestion` codes the same way — under the orchestrator
a question reaches `director.jsonl`, under a host it arrived as a tool call and fell through uncoded.

---

## 2. `scriptFile`, and what re-sending a program costs

Measured on the first `cs-5` run: **850 KB of JavaScript over 55 calls**, the twenty largest taking a
mean of **three minutes each to emit**, against a **median engine time of 45 ms**. Consecutive large
scripts shared **71%** of their lines. Wall clock tracked bytes *emitted*, not bytes read — the
Critic ingested the most and finished fastest, 11 minutes against the Colorist's 67.

`ExecuteScript(scriptFile: 'artwork.js')` runs a file instead. Both sources given is refused rather
than resolved; the server still copies **what actually ran** into `scripts/`, so a later edit never
rewrites an earlier execution's history; `script.start` names the source.

**It worked.** The second `cs-5` run used it for **25 of 48 executions**, and `Edit` became the
most-used tool at 160 calls.

---

## 3. The Claude profile, audited and repaired

Generated both profiles and diffed them. Current on the things that matter — the MCP allowlist is
reflection-derived and cannot drift, the hooks are wired, and its path denies are *stronger* than
agy's, which still has none. Four gaps found, all closed:

- **Multi-agent was agy-only**, on a premise that had gone stale: the code said Claude Code had
  "nowhere to register" a role, and it reads `.claude/agents/*.md`. A live run then dispatched
  `subagent_type: "penciler"` from a generated definition.
- **`Agent` versus `Task`.** The dispatch tool is named `Agent` in this build; the allowlist named
  `Task`, so the entry was inert and the run prompted. Both spellings now — an unrecognised entry is
  silently inert, so naming one is a rule that looks enforced and is not.
- **Subagents could not write.** Their role specs tell them to write `critique_log.md` and
  `artwork.js`; with `Read` alone, all sixteen of those edits fell to the coordinator and the
  collaboration trace was written second-hand. They now get the main agent's own tools.
- **The shell was denied wholesale**, which was too blunt. `grep`, `sed`, `diff` have nothing to do
  with drawing and everything to do with maintaining a source file. Allowed by command prefix;
  `node`, `dotnet`, `magick`, `curl`, nested shells and — after a live run deleted its own report —
  `rm`, `git` and friends stay denied.

> **An allowlist decides what is auto-approved; it prevents nothing.** An unlisted command falls
> through to a prompt, and a prompt in a long run gets waved through. That is how `rm` ran, and it is
> why destructive verbs are denied rather than merely unlisted.

---

## 4. `--reset` archives instead of deleting

It cleared `events/`, `scripts/` and `artifacts/` but **left** `findings.md`, `critique_log.md` and
`artwork.js` — so a project held a 27 KB report describing a record that had just been deleted. The
next agent read it, correctly judged it stale, and reached for `rm`. Both halves of that were ours.

Everything now moves to `previous/<timestamp>/` with a README saying what it is. Nothing is deleted,
the next run still starts clean, and `previous/` is gitignored. The shared instructions carry the
rule for agents too: **never delete — rename aside and say why**, because a stale file that survives
costs a moment's confusion and a deleted one may have been the only copy.

`projects/cs-5-baseline/` holds the first run, recovered after a reset took it: transcripts, 62
scripts extracted from `ExecuteScript` payloads, and `findings.md` / `critique_log.md` replayed from
their `Write` + `Edit` calls (all 14 edits matched, so the replay is exact). That recovery is what
this change makes unnecessary.

---

## 5. The dashboard

Reorganised on the director's own read of it: **curve → render | script → full-width trace.** The
trace was the tall left column and the script a 380px gutter, which had it backwards — the script is
code, and the trace carries the most variable-width content there is. Both top rows are
height-bounded and scroll internally so the trace stays on screen during a live run.

- **Timestamps and gaps** in the trace: `19:18:14 +6s`, warning-coloured past 30 s. Duration by
  reading rather than subtracting.
- **Tool detail** — the argument that says what a call was doing, chosen per tool. A row reading
  `Bash` is indistinguishable from any other; `Bash cat >> critique_log.md << 'EOF'…` is not. Long
  *commands* keep their opening; whole documents stay a bare length.
- **An at-work line**: who is working, what they last did, how long ago. When the last event is a
  tool call and two minutes pass, it says *"may be waiting for your approval in the agent session"* —
  hedged, because the record cannot see a prompt, only that a call has not come back.
- **The curve scrolls** at ~9px per point instead of compressing, and **mode checkboxes** narrow what
  is marked. The line always stays computed from every event: a curve recomputed from a subset would
  be a trajectory the run never had.
- **`expand`** on the script pane, sharing the render's lightbox so Esc and click-to-close are one
  behaviour.

---

## 6. `typeof` works now, and what that cost

An agent cannot ask whether a call exists. Strict resolution throws on an unresolved member — right,
because it is what stops `ctx.fillStlye = 'red'` doing nothing silently — but it also defeated
`typeof`, `in`, `Object.hasOwn` and `Reflect.has`, all of which route through the same accessor. The
first `cs-5` run spent **18 of its 71 renders** on probe scripts, deleting a candidate name at a time
to find out whether it existed.

Reads are now lenient, and three things keep that from being the old silent-typo bug:

- **Writes still throw.** Jint consults the accessor on reads only.
- **Calls still explain.** `MissingCallResolver` uses `IReferenceResolver.TryGetCallable`, which fires
  with a `Reference` carrying both the name and the base — the only hook that can see enough.
- **Reads are recorded** as `absent` probes, so an agent thrashing on names that do not exist is a
  line in the trace rather than something we might notice.

Plus `has(object, name)` and `suggest(object, name)` — the latter returning the same advice a failed
call gives, searching the whole surface, so a foreign name gets pointed at the real one.

> **`'name' in obj` reports every name as present on an SDK object, and that is not a defect.** The
> member accessor is a *value provider*: it can decline, or answer with a value. There is no third
> answer meaning "absent", because .NET has no such state — a type's members are fixed. Answering
> `undefined` to give JavaScript its semantics back also asserts the property exists. `typeof` reads
> the value and is right; `in` asks about existence and cannot be. Documented under the execution
> model with the JS/.NET seam explained, and pinned by a test.
>
> Nothing incorrect follows: every route that could change the artifact still refuses and explains.

**Rejected on the way**: relaxing resolution *without* `TryGetCallable` (loses the suggester),
`TypeResolver` (exposes only `MemberFilter`/`MemberNameComparer`/`MemberNameCreator` — no expression
context), and subclassing `ObjectWrapper` to fix `in` (constructor is `internal`, and overriding
`HasProperty` would contradict the descriptors the same object hands out).

---

## 7. What the run itself produced

`cs-5` finished: **111 scripts, 71 renders, 7 stages** including reopened ones —
Penciler → Colorist → Inker → Critic → Penciler → Colorist → Critic. A late-night noodle stall in the
rain, and the director judged the faces and arms the weak passages.

**`artifact.read`: 45.** The previous handoff recorded that column as empty — *"a whole 11-script run
recorded none: a single agent holds its own context, so it never needs to look back"*, and called it
the one thing the enactive claim most needs to show. Four agents handing over through files is what
filled it.

Its `findings.md` is 28 KB of developer-experience report, 23 numbered findings plus 5 on
orchestration. Two are already closed by this session (the `ctx` shortcut rule, the stale-files
problem). The rest are unread and worth a session of their own — particularly **#3**
(`drawPerspectiveCylinder` disagrees with its own grid's ground model), **#21** (`pointInHull` returns
true for every point when its arguments are reversed), and **#9/#10** (the Perlin shaders emit
per-channel colour noise, and the manuals recommend the use that breaks).

---

## 8. Where to pick up

**Ordered by what would most improve the next run.**

- **Rebuild `bin/cli`.** Most of this session was built with `-p:SkipCopyToBin=true` because a live
  agent session holds it open. `scriptFile`, `InspectScript`, `has`/`suggest`, the shell policy, the
  `Agent` allow entry and `--reset` archiving reach a project only after a rebuild.
- **Read `cs-5`'s `findings.md`.** 28 KB written by four agents that had just spent five hours in the
  API. It is the cheapest source of real defects available.
- **Watch for `absent` probes.** The leniency in §6 is on probation: if agents thrash on names that
  do not exist, the record will now say so, and that is the signal to reconsider.
- **`--comment`/compound shell matching is unverified.** Whether Claude Code matches a compound
  command against every segment or only the first decides whether the `rm` and `node` denials are as
  strong as they look. `cd … && sed …` ran unprompted while `cat … ; echo done` prompted, which is
  consistent with per-segment matching and does not prove it.
- **The AST edit side**, deliberately not built. `InspectScript` answers structural questions without
  reading a file — outline, find, references as resolved identifiers, so `SHAFT` does not match
  `SHAFT_TOP`. The *write* side would compete with `sed`, which has fifty years of understood failure
  modes, and a patch tool that resolves the wrong node and returns success is the `BrushPreset.color`
  scar with a bigger blast radius. Revisit only if `sed` demonstrably fails a run.
- **Two `run.start` events per session**, reproducibly. Two MCP servers briefly exist at startup and
  one exits; sequence numbers do not collide, but every host-driven record shows a duplicated open
  and close, and the UI shows `RUN ENDED` twice.

### Carried forward, still open

- **Attribute the spine.** `_code_server` passes no `agent`, so every execute/inspect event codes as
  one actor — even now that the transcript half is attributed by role. Stage-based is the only
  mechanism that works under every host.
- **Peer-to-peer subagent coordination.** Parked deliberately; the facilitator-directed pipeline in
  `comic_studio` is a different thing and now works.
- **Name the registered subagents in `GEMINI.md`**, or stop emitting `agents.json`.
- **A second image provider**; **Agent Memory Bank**.
- **Magick.NET is in `CLAUDE.md` §3 and in no `.csproj`.**
- **`reference/README.md` is gitignored** by `reference/*`, deliberately: a scan verdict describes the
  bytes on one machine, so shipping it would invite trusting a match nobody checked. A clone starts an
  empty ledger, which is what the guardrail asks for anyway.

### The flaky one

`Polson.Tests.Drawing.IrradiationCompensationTests` fails intermittently **only** under a
full-solution parallel run, a different test each time, and passes standalone every time. Not
diagnosed; probably contention on a shared Skia font or render resource. Re-run the class alone before
believing you broke it, and **do not bisect by stashing `src/`** — the tree usually carries
uncommitted work and stashing pulls it out from under the untracked tests that depend on it.

---

## What the record now looks like

A slice of `cs-5` as the dashboard shows it, which is the shortest way to see what changed:

```
19:11:22  +3s   inker  TOOL      ExecuteScript  artwork.js
19:11:27  +3s   inker  TOOL      Read  …/artifacts/stage3_inker.webp
19:11:51  +18s  inker  RENDERED  artifacts/stage3_greyscale_check.webp
19:12:53  +55s  inker  EXECUTING scripts/0070.js
19:14:41  +1m   inker  TOOL      Bash  cat >> critique_log.md << 'EOF'…
                       ↑ 24 minutes, no result — waiting on a permission prompt
```

The timestamps, the gaps, the tool arguments, the role attribution and the at-work line were all
invisible at the start of the session. The last line cost an hour before any of them existed.
