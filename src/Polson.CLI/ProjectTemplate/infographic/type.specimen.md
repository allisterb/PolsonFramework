## Design language: specimen plate

The director has set the visual frame. The reader is **looking at a naturalist's plate** — a
collection sheet where many small drawn items sit in a strict field, each labelled, the way a
butterfly case or a parts catalogue is arranged. This is Manual 13 §4's *specimen sheet*, and it is
the pattern that turns a taxonomy or a ranking into an object worth looking at.

It needs **at least eight items** to read as a collection. Fewer than that is a grid of pictures, and
the language does not survive it — choose another one rather than padding.

- **Draw the items, do not chart them.** Each cell holds a constructed thing: a silhouette, a
  cross-section, a profile. `Layout.grid(page, columns, rows, gap)` places the cells and
  `Layout.inset(cell, padding)` gives each its own margin. The rigour of the field is the design, so
  the cells are equal and the alignment is exact.
- **One item breaks the pattern.** Larger, tilted, highlighted, or spanning two cells — the specimen
  the plate is really about. A perfectly uniform field is a catalogue; one deliberate exception makes
  it a composition. This is §5's *something crosses a boundary*.
- **Label every item the same way, every time.** A number or letter, the name in italic, the figure
  beneath. Small, quiet, consistent. Where the data is quantitative, a hairline bar under each label
  turns the plate into a ranked comparison without breaking the idiom.
- **Warm paper, aged ink.** An off-white ground with tone and a little grain; a brown-black ink
  rather than pure black; one or two muted accents. `Assets.material('aged laid paper with visible
  fibres')` is a legitimate use of requisition here — it is a *material*, not the graphic — composited
  under everything at low opacity.
- **The title block is plain and large.** A specimen sheet announces itself: a broad heading, a
  subtitle in small caps, and often a rule beneath the whole title before the field begins.

**Where the data goes:** the plate itself is the comparison. Sort the cells by value rather than
alphabetically unless the order is inherent, and say in a `Stage.note` which you chose — a sorted
plate makes an argument, an unsorted one makes an index, and both are legitimate but only
deliberately.

**What defeats it:** cells that vary. The moment the items are drawn at different scales, or the
labels sit at different heights, the plate reads as a collage. Draw each item into the same box, on
the same baseline, at the same optical weight — measure it rather than trusting the eye, since the
whole effect depends on the regularity being real.
