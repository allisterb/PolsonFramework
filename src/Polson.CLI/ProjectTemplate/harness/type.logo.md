## The task: a brand identity

### What you must deliver as artwork

1. **A primary mark**, constructed as **vector geometry** — this is the master artwork.
2. **A wordmark lockup** setting the client's name with the mark, optically spaced.
3. **A brand presentation sheet** showing the identity as it would be presented to the client.

### Non-negotiable brand requirements

These are what a real identity has to survive, and they should drive construction from the first line
of code rather than being checked at the end:

- **It must read at 16px.** Detail that dissolves at that size is not detail, it is noise.
- **It must reproduce in a single flat colour** — solid black on white, and knocked out white on a
  dark ground, with no loss of identity.
- **It must be built on a deliberate geometric armature**, not placed by eye: a golden-ratio
  construction, a polar grid, an isometric grid, or a monogram matrix. The armature should be visible
  in your staged artifacts even though it is absent from the final mark.

Prove the first two rather than asserting them — the SDK has calls that generate the multi-scale
legibility ladder and the monochrome/knockout board. **Render them and look at them**, and keep them
as artifacts. If a scale test shows the mark failing at 16px, that is a result: fix the mark and say
so in `findings.md`.

### Files to leave behind

- **`output.svg`** — the primary mark as vector. The master artwork.
- **`output.webp`** — the brand presentation sheet. Landscape, at least 1400×900.

### Report specifically on

- **The vector surface.** This task is vector-first in a way raster work is not. Is
  `Snap`/`VectorLogo` as complete and as documented as the raster side? Where did you have to drop to
  Canvas2D, and did you lose anything crossing over?
- **The `Logo` and `LogoType` toolkits.** Did the optical-tuning calls — bone effect, overshoot,
  optical centre, tangent blends, irradiation — do something you could actually *see*? Did the
  kerning and pairing calls produce spacing you would defend to a client?
- **The stress tests.** Did the favicon ladder and monochrome board tell you something you had not
  already seen by eye, and did they change the mark?
