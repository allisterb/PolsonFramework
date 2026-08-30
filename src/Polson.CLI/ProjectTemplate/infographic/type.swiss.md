## Design language: swiss

The director has set the visual frame. This is the **grid tradition**: a strict modular grid, one
sans-serif family across the whole piece, generous white, and hierarchy carried by scale and weight
rather than by ornament. Nothing is rotated. Nothing is decorated.

This is the one language where the composition rules of Manual 13 §4 bend: there is no drawn scene,
no texture, no diagonal. **The rigour is the design**, and the tension has to come from somewhere
else — which makes it the most demanding of these languages, not the easiest.

- Build the **grid explicitly** and keep everything on it. `Layout.columns(page, 12, gutter)` gives a
  twelve-column field; every zone spans a whole number of columns, and elements align to column
  edges rather than to each other by eye. A single element off the grid is more visible here than in
  any other language.
- **One family, three or four sizes, two weights.** Take the sizes from one ratio
  (`LogoType.calculateTypographicScale`), not by picking them. Confirm the family with
  `Skia.Font.has(...)` and give the fallback list a neutral grotesque.
- **Flush left, ragged right.** No centring except where a form is genuinely symmetrical. Centred
  type is the commonest way this language collapses into a poster pastiche.
- **Colour is an accent, not a palette.** One hue against black, white and a grey or two. Data series
  get tints of the one hue before they get a second hue.
- **Rules do the separating**, not boxes. A hairline above a section reads as Swiss; a rounded card
  with a drop shadow reads as a dashboard.

**Where the tension comes from:** extreme scale contrast, and the empty field. The largest element
should be many times the body text — Manual 13 §5 asks for at least 8× — and at least one region of
the canvas should be left conspicuously empty. A grid filled evenly is wallpaper; a grid with one
enormous element and a large silence is the tradition working.

**What defeats it:** timidity. Swiss done cautiously — mid-sized type, even spacing, everything
comfortable — is indistinguishable from an unstyled document. If you are not slightly uneasy about
how big the headline is or how much space is doing nothing, it is not there yet.
