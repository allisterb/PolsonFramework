## What this workflow tests

`storyboard_cast` is the **built-actor** route — `Assets.cutout` for a turnaround, `GenerateCharacter`
to turn it into a posable character, `Character.load` and `Mesh.draw` per panel, and
`Scene.createSet` / `createShot` for continuity. It is the newest route in the studio and this run is
its first real use, so the findings matter more than the board. Reach for `polson://sdk/core/Character`,
`polson://sdk/core/Mesh`, `polson://manual/26` §7f–7i and `polson://manual/20`.

Answer these in `findings.md`, **per character where it applies** — the point is to find out which
parts work for one kind of character and not another:

- **The turnaround.** Did one `Assets.cutout` call give three views of the same person, in the pose you
  asked for? What did `split` report? Did you have to regenerate, and why? If you passed `reference`,
  was the second cutout recognisably the same person?
- **The build.** How long did each character take? Did any fail, and did the error say what to do?
  Did `wait: false` and resuming by `jobId` work as described?
- **The preview.** For each character: does the body read as the character from the front, the
  three-quarter and the profile? Is the face the drawn face, and does it hold up turned? Anything
  broken — a missing limb, a floating piece, a face on the wrong place?
- **The joint names.** Did every name in `jointMap` move the part it names? Which ones did not, on
  which character? Were `Character.info(name).warnings` right about what was wrong?
- **Posing.** How many attempts did a pose take before it looked right? Was the axis guidance (head
  turns on `yDeg`, an A-pose arm drops on `zDeg` with opposite signs) true for every character? What
  pose did you want and could not get?
- **Expressions.** Did the expression units read at panel size? On which characters did they fail?
- **Consistency and continuity.** Did every character pass the consistency check in step 3, and if not,
  what changed — eye colour, face, age, a mark? Did anything on the *must stay the same* list drift
  between panels anyway, and was it the model or something drawn over it?
- **Built or minor.** Which characters did you build and which did you leave as a single cutout? Was
  that the right call once the board was drawn?
- **Placement.** Standing a character in a shot — scale from the set, feet on a set point, facing with
  `yawDeg` — how much did you have to work out that the SDK could have given you?
- **Buying versus building.** For this board, would bought cutouts per expression (the `storyboard`
  route) have been better, worse, or the same — and on which panels?
- **What did you spend?** Requisitions, builds, minutes waited, budget remaining.
