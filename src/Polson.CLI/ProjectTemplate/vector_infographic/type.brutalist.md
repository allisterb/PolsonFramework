## Design language: neo-brutalist

The director has set the visual frame. Flat saturated colour, heavy black rules, hard offset shadows,
oversized type set tight, and no gradient anywhere. Everything is stated at full strength; nothing is
softened.

The language is loud, which makes it unforgiving of imprecision: at this weight a two-pixel
misalignment is plainly visible, and every element's edges are on show. It suits a piece with a
small number of blunt figures, and suits a nuanced one badly.

- **Hard shadows, never soft.** An offset solid rectangle behind the element in the ink colour —
  same shape, moved by a fixed vector, no blur. Use one offset for the whole piece so the light is
  consistent. `ctx.shadowBlur` is the wrong tool here; draw the shadow as geometry.
- **Borders are structural.** A uniform heavy stroke, 3–6px, on every block, in the ink colour. Every
  block gets the same weight — a border that varies reads as an accident rather than a system.
- **Saturated flats, no tints.** Three or four colours at full chroma against black and a paper white.
  Data series take *different* flats rather than shades of one, which is the opposite of the advice in
  most other languages and is what this one wants.
- **Type is oversized and tightly tracked**, often all-caps, in a heavy grotesque. Negative tracking
  on display sizes: `ctx.letterSpacing = LogoType.computeWordmarkTracking(size, true) + 'em'`.
  Headlines can run off the edge — §4's *bleed* pattern belongs to this language naturally.
- **Right angles, or one committed diagonal.** No rounded corners beyond a token radius. If you
  rotate, rotate one family of elements to one angle and leave everything else square.

**Where the data goes:** bars become blocks with borders and shadows, and read well. Circles read
badly — the language has no soft forms, so proportional-area circles look imported from another
piece. Prefer waffle grids, blocks and columns, and if a share must be shown, a segmented bar with
heavy dividers beats a donut.

**What defeats it:** half-commitment. Mid-saturation colour, a 1px border, a slightly soft shadow —
the result reads as a flat design with mistakes rather than as a deliberate language. Also watch
contrast: full-chroma flats behind black text can fail legibility even while looking bold. Check the
worst pairing by reading pixels back rather than trusting the swatches.
