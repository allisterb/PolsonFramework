## Where the panel comes from: seed

**The director has supplied a reference image, in `reference_images/`.** The job is reproduction: your
panel should read as the same picture, made from code.

Open it and look at it **before you plan anything**, and describe what you actually see rather than what the
brief led you to expect. Write that description as a `Stage.note` — it is the measurement everything
after it is checked against, and a reference misread in the first ten minutes is a whole session
spent faithfully reproducing something else.

### Choose your techniques from what the reference is

**The manuals are a menu, not a checklist.** This is the single most expensive mistake available in
this workflow, and it has already been made once: a flat vector reference with no line art has no use
for a three-tier ink hierarchy, feathering or Ben-Day halftone, and applying them because the
pipeline mentions them is a decision to move *away* from the target.

So before the `Pencil` stage, answer these from the image rather than from this document, and log the
answers:

```
LINE:     <heavy contour / uniform weight / none at all>
SHADING:  <flat cel / rendered gradient / hatching / none>
PALETTE:  <how many distinct colours actually appear>
EDGES:    <hard and geometric, or drawn and irregular>
```

Then say which techniques you have **ruled out** and why. That judgement is the most valuable thing
you will write down in this project, and it is invisible unless you write it.

### Measure, do not eyeball

You have the rare luxury of a ground truth, so use it as one:

- **`bitmap.palette(8)` on the reference.** It returns the dominant colours with their share, which
  is a better palette than anything you would pick by eye — and it tells you immediately whether this
  is a four-colour flat image or a rendered one.
- **`bitmap.rowProfile(color)` on both.** Reduces each image to a few hundred rows, so comparing
  structure is a loop over rows rather than over pixels. This is how you find that a mass is fifteen
  pixels too far left without hunting for it.
- **`bitmap.diff(reference)` once you are close**, rendering at the same size. `bounds` names where
  you are furthest off, which is reliably not where you thought.

Set the canvas to the reference's own dimensions in the `Pencil` stage and keep it there. Differently
sized bitmaps make `diff` throw rather than compare a partial overlap, and that is deliberate — a
similarity score over a mismatch would look like an answer.

### What reproduction does not mean

It does not mean tracing. The deliverable is a *script* that draws the panel, and a script made of
ten thousand hand-placed points is not a reproduction of the picture — it is a reproduction of its
pixels, and it teaches nothing and cannot be adjusted. Build the forms as forms: if the reference has
a circle in it, your code should contain a circle.

Where the reference has something the toolkit genuinely cannot express, say so in `findings.md`
rather than approximating it silently. That gap is worth more than the panel.

### If there is no reference

Say so and stop. Do not invent one and proceed — a `seed` project reproducing an image you imagined
is a `review` project whose record claims otherwise. Ask the director for the file, or ask them to
switch the project to `--type review`.
