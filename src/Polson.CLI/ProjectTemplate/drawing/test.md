## What this workflow tests

`drawing` is the **constructive-anatomy and perspective** toolkit — the largest and least
mechanically verifiable surface in the SDK. Reach for `polson://sdk/core/Drawing` and the manuals.

Answer these in `findings.md`:

- **Which calls have a `ctx.` shortcut and which do not.** Six take a context and have none. Did you
  guess wrong, and did the error tell you the right name?
- **`createPerspectiveBox` throws** past 85% of the anchor-to-vanishing-point distance, and it ends
  the script. Did you hit it? Was the limit computable from the documentation before you called?
- **`drawRimLight` needs the actual silhouette**, not a list running near it. Could you tell from the
  docs, or only from the render?
- **Cast shadows** take either a ground line or a grid, with different requirements. Was the choice
  clear, and did the error say which you had given it?
- **Occlusion needs `ctx.clip`** — drawing the near form later does not hide the far one on a
  construction sheet. Did you work that out, or discover it?
- **`Search` against the manuals.** Did it return the technique *and* the call that implements it?
