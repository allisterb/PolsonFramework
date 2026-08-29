## The task: a finished picture

Make the scene described in `brief.md` as a single finished image. Not a sketch, not a study — a
picture you would put in front of someone.

### Non-negotiable requirements

These are what the image has to survive, and they should drive construction from the first line of
code rather than being checked at the end:

- **The form is constructed, not asserted.** Volumes are built and lit — spheres, cylinders, boxes in
  perspective — rather than drawn as flat outlines with a colour poured in. The SDK has calls for
  exactly this; find them before you hand-roll one.
- **One light source, and everything obeys it.** Decide where the light is, then make the shading,
  the rim light and the cast shadows all agree with that decision. A cast shadow that disagrees with
  its own key light is the single most visible failure in a rendered scene.
- **The composition sits on a deliberate armature** — rule of thirds, golden ratio, dynamic symmetry
  or a triangle — chosen and stated, not arrived at. Keep a staged artifact showing the armature over
  the blocking, even though it is absent from the final image.
- **The tonal structure reads in greyscale.** Value carries a picture; colour decorates it. If the
  image collapses into mush when desaturated, the values are wrong and no amount of colour will fix
  it. Render a greyscale pass and look at it.

### Files to leave behind

- **`output.webp`** — the finished image. Landscape, at least 1400×900.
- **`output.svg`** — the vector layer, if any part of the scene was built as vector geometry. Say in
  `findings.md` if the work was entirely raster and why that was the right call.

### Report specifically on

- **The constructive drawing toolkit.** Perspective grids and boxes, volumetric spheres and
  cylinders, cast-shadow projection, rim light, three-point lighting: did they produce something you
  could use, or did you end up drawing the form by hand anyway? Where did a call's output not match
  what its documentation led you to expect?
- **Shaders and filters.** Did `Skia.Shader`, `Skia.ImageFilter` and `Skia.ColorFilter` cover the
  atmosphere, texture and grading you wanted? Did you write an SkSL shader, and was the documentation
  enough to get it compiling?
- **The raster/vector crossover.** Where did you move between `Snap` and `Canvas2D`, what did it
  cost, and did anything not survive the trip?
- **Composition and tone.** Did the armature and notan helpers change what you drew, or did you use
  them to confirm a decision already made by eye?
