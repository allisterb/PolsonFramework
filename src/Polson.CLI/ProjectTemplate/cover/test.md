## What this workflow tests

`cover` is the **composed** route at full strength — `Scene.createLayeredScene`,
`Scene.createMoodPalette`, and requisition as the intended way to fill a slot rather than as an
indulgence. Reach for `polson://manual/20` §6 and `polson://manual/16`, then
`polson://sdk/core/Scene` and `polson://sdk/core/Assets`.

Answer these in `findings.md`:

- **Did the slots take what you had?** The model hands you a rectangle per part. Was a requisitioned
  matte the right shape for it, and where did you have to fight the geometry — aspect, margin,
  detail lost at size?
- **Was the palette usable as given?** The five defaults are tuned for a saturated cover. If you
  replaced them with `hues`, say whether the shape of that option was findable from the published
  resources, and what you would have needed to see.
- **Which register did you choose, and did the instructions reach you before you chose it?** The
  source's flat treatment is defended in a saturated, high-contrast idiom; a muted palette takes that
  support away and makes the seam-closing passes load-bearing rather than optional. Say whether that
  trade was clear at the point you picked the palette, or only afterwards when you were looking at
  the render.
- **`order` is depth here and is not on a figure or a head.** Did that difference reach you before
  you painted in the wrong sequence?
- **The diagonal and the twice-used mood colour.** Did the formula produce a composition you would
  defend, or one you overrode? Overriding it is a result; say what you did instead.
- **What did a requisition actually buy?** Per call: seconds waited, budget spent, and whether a
  drawn shape would have said the same thing. A cover where every matte could have been a polygon is
  the finding this workflow most wants.
- **Did you find `Assets.cutout` and did it hold?** It is the only requisition that depicts, so it is
  the one most likely to be reached for wrongly. Say whether the published resources made the matte /
  cutout choice clear, whether `variants` gave you one consistent subject, and what `split` reported.
- **Did the cut-out sit in the frame, or on it?** The previous run on this workflow produced a figure
  with a pale halo all round her and no shadow under her, and only the flatness was the technique's
  trade — the rest was a hand-rolled rim and a missing contact shadow. Say whether the contact
  shadow, `drawRimLight` and the single whole-frame grain pass were findable when you needed them,
  what `cell.holes` read, and whether anything still reads as pasted on.
- **Where did the route run out?** A cutout is an opaque asset with no regions, so nothing in it can
  be parameterised. Did that bite, and if so, would constructing that one element have been cheaper
  than working around it? Name any other limit you met, and say what the call should have been named
  and taken.
- **The title band.** Was the reserved space the right size and in the right place once you had
  something to put in it?
