# Creative Sense-Making in a Code Medium

How a Polson run is read as **interaction over time** rather than as a pile of output — what each
recorded action says about the agent's cognitive state, and how those readings become a curve.

This implements Nicholas Davis's creative sense-making framework in a medium he did not have. It is
not a port. His **categories** are domain-independent and we adopt them unchanged; his **coding
table** is specific to drawing — pen on paper, editing a brush, rotating the page — and has to be
re-derived for a studio where the fine-grained unit of contribution is a script rather than a stroke.

**Sources.** Davis, *Creative Sense-Making: A Cognitive Framework for Quantifying Interaction
Dynamics in Co-Creation* (dissertation, Georgia Tech, 2017), Ch. VII. Davis & Rafner, *AI Drawing
Partner: Co-Creative Drawing Agent and Research Platform to Model Co-Creation* (2025), §3. Davis et
al., *The Five Pillars of Enaction as a Theoretical Framework for Co-Creative AI* (ICCC'24). Davis,
*Enactive Drift Regulation and the Emergence Machine* (2026). All are third-party and mostly
all-rights-reserved; see `reference/README.md`. Distil and cite — never quote at length.

---

## 1. Why the medium change matters

Davis's systems — Aether, the Drawing Apprentice, the AI Drawing Partner — take the **stroke** as the
atomic contribution. Slowing a collaboration to one-line turns is what makes an expert's style
legible as it emerges, and it is the move that gives his whole method its resolution.

Polson takes the **script execution** as the atomic contribution. That is the same move in a
different medium, and it changes three things in our favour:

| | Drawing medium | Code medium |
| :--- | :--- | :--- |
| Cognitive state | **Inferred** from behavioural markers — hesitation, futzing, stepping back — because a stroke cannot say why it was made | **Stated**: comments, function names, `Stage.note` |
| Action content | Deliberately absent from the curve; it records the mode, not the act | Present — every point on the curve opens the script that produced it |
| Coding cost | ~4 minutes of human video coding per minute of session, one participant per pass | Zero. The actions are already typed events |

The third row is what makes multi-agent measurement possible at all: nobody can hand-code four
agents' curves in parallel, and there is no need to.

The first row deserves care rather than celebration. Davis infers state from behaviour precisely
because behaviour is *evidence* and self-report is not. An agent's stated intent is its own account
of itself. So the coding below leans on **machine-recorded events** — what was executed, measured,
read, rendered — and treats stated intent as annotation on the curve rather than as its input.

---

## 2. The categories (adopted unchanged)

From the CCSM (Davis & Rafner 2025, §3.3). These are the data-collection schema, and they are
domain-independent:

- **Cognitive dynamics** — the clamped/unclamped mode over time. This is the curve.
- **Interaction dynamics** — turn-taking, communication, and *interaction coupling*: successive turns
  that are mutually influential. A coupling has an initiator, a decoupler, a depth in turns, and a
  duration.
- **Collaboration dynamics** — from improv theory: who made an **offer** (introduced new content),
  whether it was **accepted**, **rejected**, or **elaborated**.
- **Domain dynamics** — what kind of creative action occurred, and what content it produced. This is
  the layer that is medium-specific, and it is where our vocabulary differs from his.

---

## 3. The two cognitive modes

- **Clamped** — the agent knows what to do and is doing it. Fluid production. In Polson: writing and
  running a script that draws.
- **Unclamped** — the agent is making sense of the situation rather than acting on it. It unclamps in
  two directions:
  - **Functional unclamp** — changing *how* it is interacting: inspecting, measuring, gathering
    material, communicating, restructuring the environment. Thinking by doing.
  - **Interactional unclamp** — disengaging from the interaction: pausing, deliberating, waiting.
    Thinking.

Creative sense-making is the *oscillation* between these, not a state. A run that never unclamps is
executing a plan; a run that never clamps is never producing anything.

---

## 4. The coding table

Following the 2025 scale (Davis & Rafner, Table 2), which is the one that makes a **cumulative** curve
read correctly.

| Interaction mode | Polson events | Source | Cognitive mode | Value |
| :--- | :--- | :--- | :--- | ---: |
| **Communicate** | `note`, `stage.begin`, `stage.end`, `question` / `answer` | `server.jsonl`, `director.jsonl` | Functional unclamp | **+1** |
| **Gather** | asset requisition, `Search`, `polson://` doc reads | `server.jsonl`, enrichment | Functional unclamp | **+1** |
| **Inspect** | `inspect`, `artifact.read`, `view_file`, `list_directory` | `server.jsonl`, enrichment | Partial functional unclamp | **+0.5** |
| **Wait** | `thinking`, and any gap between events | enrichment, derived | Interactional unclamp | **0** |
| **Execute** | `script.ok` / `script.error` **that produced a render** | `server.jsonl` | Clamped | **−1** |

> [!NOTE]
> The 2017 dissertation uses a different scale — clamped at `0`, waiting at `−0.5`, disengagement at
> `−1`, with sign distinguishing physical from perceptual sense-making. The 2025 scale supersedes it
> and is the one used here. Anyone reading both will notice; the difference is real, not a
> transcription error.

### Rules that are not obvious

**A script that drew nothing is not an execution.** `script.ok` codes as *execute* only when the same
`execution` id also produced a `render`. A probe script — one that measures fonts, checks a palette
and exits — is entirely unclamped work, and counting it as production would flatten exactly the
distinction the curve exists to show.

**One event, one point — not one probe, one point.** An `inspect` event carries a tally that can run
to thousands (`getPixel` in a loop). It contributes **one** coded point at `+0.5`; the tally travels
with the point as magnitude, for annotating the curve. Weighting by probe count would let a single
loop swamp a whole session.

**A failed script still codes as execute.** `script.error` is production that didn't land, not an
absence of production. Its value in the record is as the *surprise* term — see §6.

**Internal machinery is not inspection.** `attr()` and `transform()` call `getBBox` internally; a
group's bounds recurse over its children. None of that is the agent looking at anything, and the
engine deliberately does not count it. See `ProbeScope` and `ProbeRecordingTests`.

---

## 5. The curve

Cumulative sum of coded values:

- **Rising** — the agent is regulating: measuring, reading, gathering, explaining itself.
- **Falling** — the agent is executing: producing marks.
- **Flat** — waiting: deliberating, or blocked on a collaborator.

Useful readings off it:

- **Slope** over a window — the regulate/execute balance in that passage.
- **R²** against a linear fit — how consistent the session was. A straight line is a run in one mode
  throughout; a jagged one is oscillation, which is what sense-making looks like.
- **Turning points** — where a run switched between regulating and producing. These are the moments
  worth clicking on, and in Polson each one opens the script that caused it.

### Two readings, which can disagree

The coder produces the curve twice, because "cumulative" is ambiguous in a way it is not for Davis
and the ambiguity is not resolvable by picking one.

- **`cumulative`** counts **actions**: one step per coded action, whatever it cost. It shows the
  session's rhythm — the saw-tooth of declare, produce, declare, produce — and it makes turning
  points legible.
- **`integral`** counts **time**: each state's value multiplied by how long it held. It shows where
  the session actually went.

Davis's 250 ms sampling of a coder's slider *is* a time integral, so `integral` is the closer
analogue. But it attributes model latency to whichever state preceded it, which is a real distortion
in an agentic setting and has no counterpart in a drawing session where the human is continuously
present.

**They disagree in sign on real runs, and the disagreement is informative.** On the `agy/inf-1`
infographic run:

| Reading | Result |
| :--- | ---: |
| By action count | **+34.5** |
| By time held | **−25.9** |

| Mode | Actions | Time held |
| :--- | ---: | ---: |
| inspect | 13 | 182.3 s |
| execute | 9 | 125.2 s |
| communicate | 37 | 8.2 s |
| wait | 12 | 5.7 s |

Thirty-seven notes occupied eight seconds between them, because an agent writes them in a burst at a
stage boundary; thirteen inspections occupied three minutes. Counting actions therefore describes
that run as mostly deliberation when it mostly looked and drew. Reporting only one number would have
been confidently wrong, so `summary()` reports both alongside `heldMs` per mode.

> [!IMPORTANT]
> **Where we deviate from Davis, and why.** He samples a human coder's slider every 250 ms, producing
> a continuous signal from continuous video. We have discrete typed events, so the curve is a **step
> function over real time**: it changes at events and holds between them. Flat stretches are
> therefore literal — no events means nothing happened, which is what waiting *is*.
>
> This is a fair trade in the code medium but it is a trade. A 40-second script that measured
> constantly for the first 3 seconds and drew for the rest contributes two points, not 160 samples.
> Within-execution resolution is the thing the drawing medium has and we do not.

---

## 6. Surprise, errors, and drift

The free-energy account underneath creative sense-making (Davis 2017, §7.2.3) holds that cognition
works to reduce surprise, and that an agent unclamps when the environment violates its expectations.

In a code medium the surprise term is **explicit and machine-recorded**: `script.error` and
`tool.error` are the environment refusing an action. That is why the run report treats failures as
content rather than as noise, and why any view of a run that hides them destroys the evidence it
exists to show. The expected signature of healthy sense-making is *error → unclamp → inspect → retry*,
and it is directly visible in the coded sequence.

This connects to the Emergence Machine (Davis 2026), where drift is a first-class regulatory signal
rather than a defect. Two terms from that paper are used precisely here and should not be conflated:

- **Clamped / unclamped** is a *measured cognitive state* inferred from behaviour. You detect it. It
  is not a setting.
- A **regime** is a temporally extended mode of internal organisation, with attractors, coherence
  measures and reorganisation dynamics. That *is* something a Facilitator or a human director can
  change.

The design that follows: **detect** clamp state from the curve, **monitor** coherence, and reorganise
the regime when coherence degrades.

---

## 7. Where the data comes from

**`events/server.jsonl` is the spine.** The Polson MCP server writes it in every mode — under the
Antigravity desktop, under Claude Code, under the standalone orchestrator — because the server is
ours regardless of who is hosting the agent. It carries the whole *execute* column and, since the
probe instrumentation, the whole *inspect* column too. The medium-specific actions therefore need no
host adapter at all.

**Everything else is enrichment**, and it is host-specific because the transcripts genuinely differ
in kind, not merely in field names:

| Host | Transcript | Shape |
| :--- | :--- | :--- |
| Standalone orchestrator | `events/agent.jsonl` | Typed. `{"type":"tool.call","tool":"…","args":{…}}`, plus `thinking`, `ms`, `depth` |
| Claude Code | `events/chat-*.jsonl` | Typed. `content: [{type:"tool_use", name, input}, …]` |
| Antigravity desktop | `events/chat-*.jsonl` | **Semi-structured.** Steps are `USER_INPUT / PLANNER_RESPONSE / GENERIC / SYSTEM_MESSAGE / CHECKPOINT`; a tool call is a `GENERIC` step whose `content` is the *rendered human-readable result*. No tool-name field, no args field |

So an adapter for the third has to parse prose, and will be weaker than the other two. That is a good
reason to keep it optional rather than to normalise everything down to its level.

A run coded from the spine alone is **complete for the execute and inspect columns and missing the
wait column**. That is a real limitation and it is reported rather than hidden: a curve built without
enrichment says so.

---

## 8. Multi-agent

The record is already shaped for it and the runner is not yet.

- Every event carries `src`, and the orchestrator's transcript now carries `depth` and `trajectory`,
  which is what separates a delegated subagent's work from the main agent's.
- One curve per agent, stacked on a shared time axis, is where **participatory** sense-making becomes
  visible: coupling is two curves whose turning points track each other.
- Interaction coupling — initiator, decoupler, depth, duration — is computable from the interleaving
  of `src`/`trajectory` across a window.
- Collaboration dynamics need one more inference: an **offer** is a script that introduces a new
  element, **acceptance** is a later script that builds on it, **rejection** is one that removes or
  overrides it. `bitmap.diff` already measures the artifact change that distinguishes these, though
  nothing computes it yet.

---

## 9. Implementation

`src/webapp/orchestrator/csm.py` — the coder, the curve, and the summary. Spine-only by default;
`enrich=True` merges the standalone transcript.

`src/Polson.Runtime/ProbeScope.cs` — the probe instrumentation the *inspect* column depends on.

`polson report` surfaces two headline readings — how much of the run was spent looking, and which
earlier artifacts a later pass read back.
