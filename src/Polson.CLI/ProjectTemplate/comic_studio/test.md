## What this workflow tests

`comic_studio` is the **only multi-agent workflow**, so it is the only one that exercises role
handoff, the shared scratchpad across agents, and the supervision machinery. That is what this run is
for — the artwork is the vehicle.

Answer these in `findings.md`:

- **Did the stages actually compose?** Each role must load the previous stage's render with
  `Skia.Image.load` and `drawImage` and build on it. Looking at it with `peek` is not loading it. If
  a role drew afresh, say so plainly — that is the single most important thing this run can report.
- **The handoff.** Did `transfer_to_agent` arrive with enough context to continue, or did the next
  role have to reconstruct what the last one did? What did you wish had been passed?
- **The shared `Session`.** One engine process serves every role, so a bitmap left in `Session`
  crosses the handoff with no encode. Was that discoverable? Did a key collide?
- **`ask_facilitator`.** If you called it, was the answer useful, and did it arrive with enough to go
  on given it had not seen your working? If you were *told* to call it, was the prompting right — too
  early, too late, or fair?
- **The deadline split.** Each role gets a share. Was yours enough for the stage you were given? Did
  the `[studio runtime]` notices help you land it, or arrive too late to act on?
- **Where roles disagreed** about the same artifact — naming, sizing, palette — and what would have
  prevented it.
