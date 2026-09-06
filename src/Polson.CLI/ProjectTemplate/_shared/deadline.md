## The deadline

**You have {{DEADLINE_MINUTES}} minutes for this commission.** It is a constraint, not a target —
the same kind a studio works to. You are told it now, before you start, because a deadline you learn
about late is only bad news; a deadline you know at the outset is something you can plan against.

**Plan backwards from it before you draw anything.** Decide how many passes you can afford and what
each one is for. Two considered passes inside the time beat six rushed ones that run over.

**Scope is the variable. Finish quality is not.** If the work will not all fit, deliver a smaller
thing that is whole rather than a larger thing that is half-built — a complete simple mark, not an
elaborate one missing its counters. Say in a `Stage.note(...)` what you cut and why. That note is the
professional part of running out of time, and it is what a reader of the run will look for.

**Know where you are.** `Stage.elapsedMinutes` is minutes since the run began:

```javascript
const spent = Stage.elapsedMinutes;
Stage.note(`${spent.toFixed(1)} min spent of {{DEADLINE_MINUTES}}`);
```

Use that rather than stamping your own start. `Date.now()` and `mina.time()` both answer *now*, not
*since when*, so `Session.startedAt ??= Date.now()` measures from your first script — and the time
you spent reading the brief, the manuals and the SDK reference before it is recorded as zero. It
understates silently and gives back a number that looks right.

The runtime will also tell you, unprompted, when about three-quarters and then nine-tenths of your
time is gone. Those notices arrive as `[studio runtime]` lines. Treat them as the studio manager
putting their head round the door: stop, finish the pass you are on, and move.

**Time may not be the only allowance, and the other one you cannot see.** A run can also be given a
budget of **input tokens** — the whole conversation is resent to the model on every turn, so it
climbs steadily whether or not you are making progress, and reading a long document early means
paying for it on every turn that follows. Nothing in the sandbox can count it: `Date.now()` gives you
a clock, and there is no equivalent for tokens.

**If your host offers a `budget_status` tool, call it** — once when you plan, and again before
committing to another full pass. It costs nothing and returns both allowances:

```json
{ "inputTokensSpent": 400000, "inputTokenCap": 1000000, "inputTokensUsedPercent": 40.0,
  "minutesElapsed": 12.3, "minutesRemaining": 17.7 }
```

The binding constraint is whichever is nearer, and it is not always the clock: a run can be half an
hour from its deadline and nearly out of tokens. If the tool is not there, this run has no token
budget or no way to report one — work to the clock and to the `[studio runtime]` notices, which
arrive either way.

Two things worth knowing about the figure. It **excludes the turn now running**, because a turn's
cost is only known once the model has answered — so treat it as a floor. And at 100% the run is
**halted where it stands**: no further model calls, work already written to disk intact, anything in
progress lost. That is a worse ending than a stage you closed yourself, which is the whole reason to
ask before starting a pass rather than after.

**Spend the time on the picture, not on the machinery.** The costs that actually eat a commission:

- **Draft small, render the final large.** Encode time tracks pixel count and dominates a call —
  1600 × 1200 costs about four times what 800 × 600 does, for a draft nobody keeps.
- **Edit the file; do not retype it.** Once a piece is more than a screenful, keep it in `artwork.js`
  and change the part you are working on. Re-sending a whole program to alter ten lines of it is the
  single largest cost in a run, and it is not drawing.
- **Do not render what nobody will look at.** A measurement or probe pass takes `render: false`.
- **Measure with one call, not a loop.** `bitmap.diff`, `bitmap.palette` and `bitmap.rowProfile`
  answer in one native call at any resolution; a per-pixel JavaScript loop will hit the statement cap
  and take your whole script with it.

**If you are behind, do not iterate harder — ask.** Going round again on something that is not
working is how a deadline is missed. Say what is stuck and get a direction back, then act on it.
Being told to stop and ship is a legitimate answer and often the right one.
