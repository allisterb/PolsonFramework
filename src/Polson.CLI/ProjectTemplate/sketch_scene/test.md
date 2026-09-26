## What this workflow tests

`sketch_scene` is the **constructed** route through gesture: `pose.lineOfAction`,
`createFigureGeometry`, `createGestureContour` and `findTangents`, run as Stanchfield's procedure
(`polson://manual/28`), with one check per stage.

Answer these in `findings.md`:

- **Did the sentences drive the pose?** Say whether the stage 1 verbs gave you angles, or whether you
  posed first and wrote the sentence to match. Quote one sentence and the pose it produced.
- **Line of action.** Did `lineOfAction` give you the curve the verb needed, and did you find it before
  reaching for `spineDeg`? What `swing` did each figure end at?
- **The joint angles.** Were the screen-direction conventions clear from the instructions, or did you
  learn them by rendering? How many runs did the first pose take?
- **The checks.** Which failed, what did you change, and did the change happen in the pose or in the
  drawing? A check that failed and was fixed in the pose is the workflow working.
- **`findTangents`.** What did it report, and did you agree with each one when you looked? Name any it
  found that you would not call a tangent, and any you can see that it missed.
- **The critique.** Did reading the render against the sentences change anything? Did you push a pose
  further than your first version, and was the second better?
- **Where it ran out.** Mannequins have no clothing, faces or hands beyond what you draw. Say what the
  brief needed that the construction could not give, and what you did instead.
