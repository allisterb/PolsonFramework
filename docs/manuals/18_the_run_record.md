# Studio Manual 18: The Run Record — Stages, Notes and Session

> **Credits & Theoretical Foundation**: The case for declaring intent *while working* is taken from the three limitations Davis, Hsiao, Singh, Lin & Magerko name in their own methods paper, *Creative Sense-Making: Quantifying Interaction Dynamics in Co-Creation*, C&C '17 (ACM, all rights reserved — cited, not quoted; ledger entry 2026-08-31). Deshpande, Trajkova, Knowlton & Magerko's *Observable Creative Sense-Making*, C&C '23 (**CC BY 4.0**) supplies the applicability clause. The globals themselves are documented at `polson://sdk/core/Globals`.
> **Purpose**: How a run makes itself legible to whoever reads it afterwards — declaring stages, writing the notes that survive, logging that helps rather than fills, ending deliberately, and carrying state between scripts.

---

## 1. Who Reads This, and When

> **Implemented by**: `Stage.begin(...)`, `Stage.note(...)`, `log(...)`, `table(...)`. Signatures at `polson://sdk/core/Globals`.

Everything else in these manuals is about making a picture. This one is about making the *making* legible — and it matters because the picture alone cannot answer the questions anyone actually asks of a session: why this direction and not the other one, what was tried and abandoned, what a render was meant to test.

The people who read the record are all reading it **without your context**: the director between turns, a viewer of the streamed run page, a later stage of the same run that has forgotten, and the next run's `Recall`. Write for them, not for yourself.

### What the research had to do the hard way

The CSM methods paper is candid about what it costs to reconstruct a collaboration from the outside, and each limitation is answered differently here.

**Ground truth needs the participant, and by then it is too late.** To check whether the coded cognitive states matched reality, the authors would have gone back to the retrospective protocols — the participants' own accounts. But, as they say plainly, those studies were designed before the CSM framework existed, so the retrospective protocols never solicited the information the framework later needed. The account existed; it was about the wrong thing.

`Stage.note(...)` is that account, solicited at the time, about the thing the framework cares about. **That is the whole argument for writing one.** Nobody can come back and ask you afterwards.

**A human coder lags the action.** They record a delay between an action starting and the analyst moving the coding slider to match — enough, they note, to time-shift events or change their granularity, and to hurt inter-rater reliability. Here the events are stamped by the machine at the moment they happen. There is no coder and no reaction time.

**A slider passes through values nobody meant.** Moving between two codes makes the tool sample the intermediate values on the way. A stage is a discrete declaration: it is one value until you change it, and there is no in-between.

> [!IMPORTANT]
> Be precise about which half is which. The machine-recorded fields — timestamp, sequence, script path, duration, artifact path, byte count — are the **checkable** half. `Stage` and `Stage.note` are **your account of your own intent**, and nothing verifies them. That is not a weakness: OCSM's applicability clause is that this kind of analysis holds for collaborations with *identifiable behaviour markers*, and a code medium has them. But a stage that says `Refine` over a script that redrew everything is a false statement in a record that otherwise does not contain any, so declare what you are actually doing.

---

## 2. Stage — Filing Everything Under What You Were Doing

> **Implemented by**: `Stage.begin(name)`, `Stage.end()`, `Stage.current`, `Stage.note(message)`.

A stage is a heading. Declare it once and **every script, render, note and failure that follows is filed under it** until you change it — the server tags `script.start`, `script.ok` and `render` with the stage in force, so grouping a run by stage costs you one call and nothing else.

```js
Stage.begin('Concept');
Stage.note('synecdoche — the wing, not the bird; rejects the generic globe');
```

**Set it once at the top of a stage, not in every script.** It persists across executions.

### What each call does, observed

| Call | Result | Recorded |
| :--- | :--- | :--- |
| `Stage.begin('Concept')` | returns `'Concept'` | `stage.begin` |
| `Stage.begin('Concept')` again | returns `'Concept'`, stage stays open | `stage.continue` |
| `Stage.begin('CONCEPT')` | returns `'Concept'` — **the original spelling is kept** | `stage.continue` |
| `Stage.begin('Blocking')` | switches | `stage.end` (`reason: superseded`), then `stage.begin` |
| `Stage.end()` | closes it; `current` becomes `null` | `stage.end` |
| `Stage.end()` with none open | harmless no-op | nothing |
| `Stage.note('…')` | — | `note`, tagged with the stage and execution |

Comparison is case-insensitive, so re-declaring the stage you are already in is an **announcement, not a transition**. That is deliberate: it means you can safely restate the stage at the top of every script without fragmenting the record, and `stage.continue` distinguishes the restatement from a real switch.

`Stage.current` is the stage in force, or **`null`** when none is open — so `if (!Stage.current)` is the check to write.

> [!NOTE]
> **Outside a project, `Stage` is a no-op.** `Stage.begin('X')` returns `'X'`, but `Stage.current` still reads `null` and nothing is recorded, because the stage lives on the run's session and an ad-hoc engine has none. That is why the example at the end of this manual declares a stage without reading it back.

### Naming a stage

Name it for what a reader would want to click on. `Concept`, `Blocking`, `Refine`, `Stress test`, `Critique` — a phase of work, at the granularity where the answer to "what was happening here?" is interesting.

The `drawing` workflow does something different and worth knowing about: it uses the **collaboration move** as the stage name — `Ground`, `Offer`, `Accept`, `Elaborate`, `Depart`, `Critique` — so the interaction dynamics land in the record directly instead of being inferred afterwards from artifact deltas. Same mechanism, finer grain, because there the unit of work *is* the turn.

---

## 3. `Stage.note` — the Only Part Written for a Reader

`log(...)` reaches the caller of that one tool call and then it is gone. `Stage.note(...)` persists into the run's record and is what a reader sees afterwards. They are not interchangeable, and the difference is the single most consequential thing in this manual.

**Use a note for what would otherwise be lost**: why a direction was abandoned, what a render was meant to test, what you decided to live with, what the numbers meant.

| Not worth recording | Worth recording |
| :--- | :--- |
| `rendered stage 3` | `third attempt at the counter — the knockout closes below 24px, so the aperture has to open` |
| `added the horizon` | `moved the horizon off centre because the mast was cutting the frame in half` |
| `fixed the colour` | `accent was 40% of the frame by palette, not the 10% the brief implies; cut it back to the rigging only` |
| `starting critique` | `the value plan says 60/25/15 and the render measures 60/25/15 — the compression is in hue, not value` |

The rule of thumb: **a note that only says what the script already shows is not a note.** The script is saved; the render is saved; their existence is machine-recorded. What is not recorded anywhere is your reason, and that is the thing to write.

Write it for someone who cannot see your context — because that is exactly who reads it.

---

## 4. Logging — Where It Goes and What It Is For

> **Implemented by**: `log(...)`, `error(...)`, `console.log/info/warn/error/debug/trace(...)`, `console.clear()`.

Every logging call returns to the caller of the current `ExecuteScript` and appears in `result.Logs`, prefixed by level:

```
console.log('L')   → [LOG] L          log('x')   → [LOG] x
console.info('I')  → [INFO] I         error('x') → [ERROR] x
console.warn('W')  → [WARN] W
console.error('E') → [ERROR] E
console.debug('D') → [DEBUG] D
console.trace('T') → [TRACE] T
```

`console.clear()` empties the accumulated buffer — the lines before it are gone, not merely separated. Use it when a script's diagnostic phase is over and only the conclusion should travel back.

**Logs are for this turn; notes are for the run.** A measurement you acted on belongs in a log line so you can see it now, and its *conclusion* belongs in a note so it survives.

---

## 5. `table` — When a Grid Beats a Sentence

> **Implemented by**: `table(rows)`, `table(headers, rows)`.

Three forms, all rendering an aligned ASCII grid into the log:

```js
table([{ stage: 'Concept', ms: 120 }, { stage: 'Blocking', ms: 340 }]);  // columns from property names
table(['a', 'b'], [[1, 2], [3, 4]]);                                      // explicit headers
table([1, 2, 3]);                                                         // scalars, under a "value" column
```

```
+----------+-----+
| stage    | ms  |
+----------+-----+
| Concept  | 120 |
| Blocking | 340 |
+----------+-----+
```

This is the right shape for anything you are comparing across rows — measured palettes, per-row profile deltas, candidate descriptors and their verdicts, tick values against pixel positions. A table of six numbers is read at a glance; the same six numbers in a sentence are not read at all.

---

## 6. `exit` — Ending on Purpose

`exit(message)` stops the script immediately, **succeeds**, records the message as the return value, and keeps everything produced before it:

```js
const oak = await Assets.material('weathered oak planking');
if (!oak.success) { error(oak.remedy); exit(oak.failureName); }
```

The log gets `[EXIT] <message>` and `result.ReturnValue` is the message. Nothing after the call runs — including any drawing, so **a script that exits before it renders produces no image**. That is usually what you want when a precondition failed, and never what you want as a way of finishing early.

`exit` is for a *decided* stop: a check that failed, a requisition that cannot be retried, a condition that makes the rest of the script pointless. An actual fault should be allowed to throw, because a failure recorded as a success is a lie the record cannot recover from.

---

## 7. `Session` — Carrying State Between Scripts

`Session` is a scratchpad that persists across executions within one MCP session:

```js
Session.oakUri = oak.toDataUri();       // script 1
const plank = Skia.Image.fromDataUrl(Session.oakUri);   // script 2
```

`Session[key]` returns `undefined` for a key never set, and `delete Session[key]` removes one. It keeps ordinary JavaScript semantics — unlike an SDK object, a missing key is not an error.

**What belongs in it**: a requisitioned material's data URI (Manual 16 §5), a measured constant an earlier script computed, a palette or layout you do not want to recompute, a "before" bitmap encoded for a later comparison.

**What does not**: anything a reader will need. `Session` is invisible in the record and disappears with the session. A decision stored only in `Session` is a decision nobody will ever see — that is what `Stage.note` is for.

---

## 8. `mina` — Easing Curves as Distributions

> **Implemented by**: `mina.linear / easein / easeout / easeinout / backin / backout / bounce / elastic(n)`, `mina.time()`.

These are Snap.svg's animation easings, and this is a still-image studio — so read them as **shaping functions** rather than as animation. Each maps `0…1` to `0…1` along a different curve, which is exactly what you want when a row of elements should not be evenly spaced.

Measured at `t = 0, .25, .5, .75, 1`:

| Curve | 0 | .25 | .5 | .75 | 1 | Reads as |
| :--- | ---: | ---: | ---: | ---: | ---: | :--- |
| `mina.linear(t)` | 0.000 | 0.250 | 0.500 | 0.750 | 1.000 | Even — a comb |
| `mina.easein(t)` | 0.000 | 0.076 | 0.293 | 0.617 | 1.000 | Crowded at the start |
| `mina.easeout(t)` | 0.000 | 0.383 | 0.707 | 0.924 | 1.000 | Crowded at the end |
| `mina.easeinout(t)` | 0.000 | 0.146 | 0.500 | 0.854 | 1.000 | Crowded at both ends |
| `mina.backin(t)` | 0.000 | **−0.064** | **−0.088** | 0.183 | 1.000 | Pulls back before starting |
| `mina.backout(t)` | 0.000 | 0.817 | **1.088** | **1.064** | 1.000 | Overshoots, then settles |
| `mina.bounce(t)` | 0.000 | 0.473 | 0.766 | 0.973 | 1.000 | Settles in decaying steps |
| `mina.elastic(t)` | 0.000 | 0.912 | **1.016** | 1.006 | 1.000 | Snaps past, oscillates in |

Use them for perspective-like recession (marks crowding toward the horizon), a gradient ramp that is not linear, tick spacing on a non-linear axis, or the density of hatching across a form.

> [!WARNING]
> **`backin`, `backout` and `elastic` leave the `0…1` range** — the table shows `backin` going to −0.088 and `backout` to 1.088. That is what gives them their overshoot, and it means anything you interpolate with them lands *outside* the interval you thought you were filling. Clamp, or leave room for the overshoot, or use one of the five that stay inside.

`mina.time()` returns the current timestamp in milliseconds. It is genuinely a clock, so anything derived from it differs between runs — never use it as a seed for something that must be reproducible (Manual 17 §5).

---

## 9. Traps

| What happens | Why | What to do |
| :--- | :--- | :--- |
| The record has no stages | `Stage.begin` was never called | Declare one at the top of each phase; it persists |
| The run is one giant stage | It was declared once and never changed | A phase, not a session |
| Restating the stage fragments the record | It does not — a re-declaration is `stage.continue` | Restate freely |
| `Stage.current === undefined` is false when no stage is open | It is `null` | `if (!Stage.current)` |
| `Stage.current` is null right after `begin` | No project: `Stage` is a no-op outside a run | Expected in an ad-hoc engine |
| The reasoning is gone after the run | It was in `log(...)`, which is per-call | `Stage.note(...)` persists |
| A decision cannot be found anywhere | It was in `Session` | `Session` is invisible in the record |
| A script "succeeded" but drew nothing | `exit(...)` ran before the render | Exit on failed preconditions only |
| A textured mark changes every run | Seeded from `mina.time()` | Pass a fixed seed |
| Distributed marks overshoot their box | `backin` / `backout` / `elastic` leave `0…1` | Clamp, or use `easeinout` |

---

## 10. Symbol → SDK Map

| What you want | The call |
| :--- | :--- |
| Declare a phase | `Stage.begin(name)` — returns the name as recorded |
| Which phase am I in? | `Stage.current` — `null` when none |
| Close a phase | `Stage.end()` — harmless if none is open |
| Say why, for the record | `Stage.note(message)` |
| Say something for this call only | `log(msg)`, `error(msg)` |
| Levelled logging | `console.log / info / warn / error / debug / trace` |
| Discard the log so far | `console.clear()` |
| Compare across rows | `table(rows)`, `table(headers, rows)` |
| Stop deliberately, successfully | `exit(message)` |
| Carry state to the next script | `Session.key = value` |
| Forget it | `delete Session.key` |
| Shape a distribution | `mina.easeinout(t)` and the seven others |
| A clock | `mina.time()` — never a seed |

---

## 11. Recording It: A Runnable Plate

A stage, a note, a measurement logged as a table, and an easing curve doing real work — marks distributed by `easeinout` against the same marks distributed evenly, so the difference is visible rather than asserted.

```javascript
// The run record, exercised: declare the phase, measure, table the measurement,
// note the conclusion. The drawing is an easing curve used as a distribution.
Stage.begin('Distribution study');
Stage.note('testing whether easeinout reads as recession — even spacing looked mechanical ' +
           'against the horizon, and crowding toward it is what perspective actually does');

const canvas = createCanvas(820, 360);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#fbfaf7';
ctx.fillRect(0, 0, 820, 360);
ctx.textBaseline = 'top';

const page = Layout.inset(Layout.rect(0, 0, 820, 360), 30);
ctx.font = '700 19px sans-serif';
ctx.fillStyle = '#1c2733';
ctx.fillText('Even spacing against an eased distribution', page.x, page.y);

const [evenBand, easedBand] = Layout.rows(
    Layout.rect(page.x, page.y + 42, page.width, 230), 2, 24);

// One row of posts, placed two ways. Only the mapping from index to x differs.
const count = 14;
const rows = [];

function posts(band, label, curve) {
    ctx.font = '600 13px sans-serif';
    ctx.fillStyle = '#2b3742';
    ctx.fillText(label, band.x, band.y);

    // The posts get their own strip below the label, so a mark at x = band.x cannot
    // strike through the caption — the first post always sits exactly there.
    const strip = Layout.inset(band, 26, 0, 0, 0);

    ctx.save();
    ctx.useBrush(Skia.Brush.ink('#1b3b6f', 2.5));
    for (let i = 0; i < count; i++) {
        const t = i / (count - 1);
        const eased = curve(t);
        const x = band.x + eased * band.width;
        // Height recedes with the same curve, so the row reads as depth rather than as a comb.
        const h = strip.height - 8 - eased * (strip.height * 0.55);
        ctx.beginPath();
        ctx.moveTo(x, strip.y2);
        ctx.lineTo(x, strip.y2 - h);
        ctx.stroke();
        if (label.startsWith('eased')) {
            rows.push({ i: i, t: t.toFixed(2), eased: eased.toFixed(3),
                        x: Math.round(x), gap: i === 0 ? 0 : Math.round(x - rows[i - 1].x) });
        }
    }
    ctx.restore();
}

posts(evenBand, 'linear — even, and it reads as a comb', mina.linear);
posts(easedBand, 'eased — easeinout, crowding at both ends', mina.easeinout);

// §5 — a table is the right shape for something compared across rows.
table(rows.filter(r => r.i % 3 === 0));

// §8 — the overshoot curves leave 0..1, which is the trap worth seeing as a number.
const overshoot = ['backin', 'backout', 'elastic'].map(name => ({
    curve: name,
    'at .5': mina[name](0.5).toFixed(3),
    outside: mina[name](0.5) < 0 || mina[name](0.5) > 1
}));
table(overshoot);

// §4 — levels. The measurement goes to the log; its conclusion goes to the record.
const gaps = rows.map(r => r.gap).filter(g => g > 0);
const widest = Math.max.apply(null, gaps);
const narrowest = Math.min.apply(null, gaps);
console.info('eased gaps run from ' + narrowest + 'px to ' + widest + 'px');
if (widest / narrowest < 1.5) console.warn('the curve is barely doing anything at this count');

// §7 — Session carries a value forward; it is not a substitute for the note below.
Session.postSpacingRatio = widest / narrowest;

Stage.note('easeinout gives a ' + (widest / narrowest).toFixed(1) +
           ':1 spread between the widest and narrowest gap at 14 posts — enough to read as ' +
           'recession without the end posts colliding');

canvas;
```
