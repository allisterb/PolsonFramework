## What this workflow tests

`animation` is the constructed route through motion: keyed poses (`Motion.nodes.animated`), a figure
built every frame (`comp.drawn` over `createMannequinFigure`), contacts solved every frame
(`reachLeg`, `reachArm`), checks taken off the keyed values, and the contact sheet as the agent's only
view of the film (`polson://manual/25` §4, §10).

Answer these in `findings.md`:

- **Did the beats drive the keys?** Say whether the beat sentences gave you poses and frame numbers,
  or whether you keyed first and wrote the beats to match. Quote one beat and the key it produced.
- **Frames.** Did you count in frames from the start, or meet a beat that fell between frames? Did
  the sheet ever refuse a frame or label one with the wrong time?
- **Manual 25 §10.** Did you start from it? Which parts carried over unchanged, and what did you have
  to work out that it did not show?
- **The winding.** Did any limb turn the wrong way round between two keys? How did you find out:
  from a check, from the sheet, or not until the critique?
- **The checks.** Which failed, what did you change, and did the change happen in the keys, the eases
  or the frames? Name any check you could not take on your route, and why.
- **The sheet as your eyes.** Was one image per beat enough to judge the motion? Name anything about
  the film you could not judge from the sheets and had to infer.
- **Delivery.** Did `output.webp` come from `Motion.save`? Did the server refuse an `outFile` over it?
  Is there an `output.svg`, and if not, did the instructions make the reason clear?
- **Where it ran out.** Say what the brief needed that the mannequin or the rig could not give, and
  what you did instead.
