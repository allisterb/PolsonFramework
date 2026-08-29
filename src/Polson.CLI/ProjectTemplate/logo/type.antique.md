## Style direction: antique

The director has set the stylistic frame. This is Manual 12 §2.5b — temperature, weight and shape
language — **not** the archetype. Which archetype carries it (pictorial, letterform, monogram,
negative space…) is still yours to choose at Stage 4, once you have candidate forms to look at.

The reference is engraved and printed work: trade marks, seals, labels and mastheads made when a
mark had to survive being cut into a plate. Heritage is claimed through *craft*, not through age
effects — a mark that reads as old because it is well made ages well; a mark that reads as old
because it was distressed reads as a costume.

- **Weight contrast is the signature.** Thick and thin within one stroke, placed where a cut would
  naturally swell. A uniform-weight outline will not read as antique however ornate the shapes are.
- **Symmetry, and a frame.** Emblem and crest enclosures belong to this language, and the toolkit has
  badge outlines — shield, hexagon, scallop, circle. Bear the cost knowingly: an enclosure binds type
  to mark and resists being taken apart for small sizes, which is exactly what favicons need.
- **Ornament is structural, not sprinkled.** A rule, a scroll, a pair of flourishes answering each
  other across an axis. Every ornament should have a counterpart or a reason.
- **Serif type with real character**, checked with `Skia.Font.has(...)` before you commit — a
  substituted face will still render and still measure, and the wordmark you judge will not be the
  one you chose. Set caps with generous tracking; antique lettering is spaced open, not tight.

**What defeats it:** detail that dies at 16px. This is the style most likely to fail the scale test,
because everything that makes it convincing is fine. Plan for a simplified small-size variant from
the start, and prove it on the favicon ladder rather than discovering it at Stage 6. If the mark only
works above 64px, say so in the presentation rather than quietly shipping it.
