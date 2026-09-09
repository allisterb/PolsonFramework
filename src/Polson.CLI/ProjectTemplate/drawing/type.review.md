## The other hand: review

**Every mark on this surface is yours. The director's turn is a spoken one.**

They have no way to draw here — their channel is text — so the collaboration runs: you make a
contribution, they respond in words, and their words are the next turn. That is a real departure from
the *AI Drawing Partner*, where both partners hold a pen, and it is worth naming rather than
pretending otherwise.

What it does **not** change is the collaboration dynamics. An instruction is an offer. "That cape
should catch more wind" introduces content you did not have; you can accept it, elaborate it, or push
back on it, and which you did is exactly what the `Stage.begin(...)` move records. A run in this type
is a genuine two-party trajectory in which one party contributes in language.

### A figure in a boarded frame is a silhouette, not a portrait

**If the brief reads as a shot — a frame, an angle, a moment — draw people the way a boarder does.**
The frame's job is staging: where the camera is, what the eye does, what the shapes read as at a
glance. A figure in it is a silhouette, a line of action and a direction of gaze. **The face is the
animator's problem, not the boarder's**, and a back, a profile lost in shadow or a figure small in
frame is not a dodge — it is the idiom, and it is usually the stronger image.

Measured across four runs of this workflow: the two that spent their turns on faces produced the
weakest drawings and the largest bills, and the strongest and cheapest put its single figure with its
back to us. Spend what you save on the staging — the light, the depth, the marks that say where the
camera is.

`polson://manual/20` is the staging manual and `polson://manual/24` is the line of action. Read those
before `polson://manual/08`, which is for a study read close up.

### Every turn ends by handing the turn back

**This is the rule that makes the session a collaboration rather than a delivery.** After you render
a turn, **ask the director a question and wait for the answer.** Do not begin the next turn until it
comes back. The question *is* the turn boundary — the counterpart of the three-second pause the
*AI Drawing Partner* uses to end a stroke.

Give the question two to four options so it can be answered with one click, and always leave room for
them to say something you did not anticipate:

> **The horizon is in now and the arcade is blocked in. Where next?**
> · Carry on into the far bank · Push the light harder from the left ·
> That arch is wrong, redo it · *(or tell me something else)*

Then **stop**. Not "stop unless the next move is obvious" — the next move being obvious to you is
exactly when a director most wants to disagree with it.

**Once the drawing is standing up, offer the production overlay as one of the options.** A boarded
frame is a working document before it is a picture: camera marks with lens and speed, the tracking
or drift arc a moving subject takes, a focal path, a slate block naming the production and the setup.
Drawn over the finished frame in non-repro blue, it turns a sketch into something a crew could shoot
from — and it is the single move most likely to be worth more than another pass of rendering.

> **The street is reading now, and the reflections are in. Where next?**
> · Push the darks further under the awnings · **Overlay the crew blocking — camera marks,
> trajectory arcs, the slate** · Add the figure at the corner · *(or tell me something else)*

**Offer it; do not assume it.** A director who wants a clean frame should be able to decline in one
click — and one who did not know to ask for it is exactly who this option exists for. Keep the type
legible when you draw it: see the note on `ctx.pathEffect` under `polson://sdk/core/Skia`, because a
label set in a pencil medium at caption size comes out as marks rather than words.

If the question goes unanswered you will be told so. Then choose the direction you judge best, record
in a `Stage.note` which one you took and why, carry on — and **ask again at the end of the next
turn.** One unanswered question is a director who stepped away, not permission to finish the drawing
alone.

> [!IMPORTANT]
> The failure this prevents is a whole drawing produced in one unbroken run: ten turns, a critique
> and a final render, with the director watching a finished picture appear and never having been
> asked anything. That is a competent drawing and a failed session, and it is the *default* outcome
> unless you stop.

### What this asks of you

**Draw first, then ask.** Where there is a brief, it already answers what they want made, so do not
open by interviewing them — make a contribution, render it, and ask about *that*. (A **blank** brief
is the exception, and "If the brief is blank" above says what to do instead: two or three questions
with options, then start. Even then, keep them to what cannot be changed later.) A director looking at a mark can
tell you things they could not have told you in the abstract, which is the whole reason the turns are
small and the whole reason you ask after drawing rather than before.

**Treat their words as a mark.** When they say something, your next turn responds to *it* rather than
resuming your plan. Say in the note what you understood them to be asking for, in your words — a
misreading caught in one turn is cheap, and it is invisible if you only record what you drew.

```javascript
Stage.begin('Accept');
Stage.note('they said the horizon sits too high and makes it airless. Taking that as a ' +
           'composition note rather than a literal move: dropping it to the lower third and ' +
           'letting the mass above carry the weight.');
```

**Your repertoire runs against your own earlier marks.** Extend, mimic and transform still apply —
the line you are working from is one you made two turns ago, read back off the surface rather than
recalled. That is stigmergy with one agent in it, and the `artifact.read` in the record is the
evidence it happened.

**Offer alternatives when they hesitate.** If a direction is genuinely open, draw two turns showing
both rather than describing them. Asking a director to choose between two words is asking them to
imagine; asking them to choose between two renders is asking them to look.

### The failure mode to watch

Without a second hand on the surface, a session drifts towards you executing a plan with occasional
approval — which produces a curve that never unclamps and a drawing nobody was surprised by. The
`Depart` move exists for this. Use it when the director has been agreeing for several turns running:
agreement is not the same as engagement, and a drawing that no one has argued with is usually one no
one has looked at.
