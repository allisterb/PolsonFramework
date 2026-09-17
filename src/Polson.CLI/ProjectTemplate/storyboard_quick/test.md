## What this workflow tests

`storyboard_quick` is the **arranged** route — `Scene`, `Random`, and a clock short enough that the
choice between arranging and constructing actually costs something. Reach for `polson://manual/20`
§6, then `polson://sdk/core/Scene` and `polson://sdk/core/Random`.

Answer these in `findings.md`:

- **Did the route hold under the clock?** You were told to arrange rather than construct. If you
  reached for `createLoomisHead` or `createMannequinFigure` anyway, say why — the instruction being
  ignorable is a more useful result than the instruction being followed.
- **Did `createSet` and `createShot` carry the sequence?** The set describes the space once and a
  shot is a crop and a scale of it, so the room cannot drift between panels. Did you find that pair
  from the published resources, did the rung you asked for frame what you wanted, and **where did the
  crop model stop being enough?** The reverse angle is the limit we already know about — name any
  other you met, and say what the call would have had to take.
- **Continuity is unchecked.** Did anything in the run tell you whether the space held still between
  panels, or did you have to look?
- **Seeds and forks.** Could you tell from the documentation why `rng.fork(name)` exists rather than
  drawing everything from one stream? Did you record the seed where a reader would find it?
- **Is `order` depth?** It is on this model and is *not* on a figure or a head. Did that difference
  reach you before you painted in the wrong sequence?
- **The palette's placement rules** — the figure on `highlight`, the mood hue twice, the ground
  near-black. Did the board read as one sequence, or as panels that merely share colours?
- **What did you spend?** Requisitions made, seconds waited, and whether shapes alone would have said
  the same thing.
