# The graphic-form gap

*Noted 2026-09-08, from a question about the header illustration on a Méliès infographic — a flat,
four-colour, faceted Moon with a capsule in its eye.*

**There is no way to requisition a stylised graphic form that is neither a person nor a texture.**
A moon, a rocket, a film reel, a lighthouse: the studio can get you the *outline* and nothing else.

This is half deliberate and half accidental, and the two halves want different answers.

## What the surfaces actually offer

| call | returns | on a header illustration |
| :--- | :--- | :--- |
| `Assets.material(...)` | a flat tiling swatch | **refused** — the form/substance classifier catches it, correctly |
| `Assets.matte(...)` | *"One solid shape: no interior detail, no outline, no gradient, no shading, no grey of any kind"* | the silhouette, and nothing inside it |
| `Assets.backdrop(...)` | a photographic background plate | wrong role; it composites *beneath* a scene |
| `Photo.of(...)` | a real photograph, with its licence | the right answer whenever the subject genuinely exists |

The matte prompt is explicit about being single-channel, so the facets, the palette, the rim light
and the stars are all yours to draw. That part is the studio working as intended: **the model
supplies what is hard to synthesise, the code supplies form, colour and composition.** A call that
returned the finished header would be the thing this project exists not to do.

## The half that is not intended

**A matte comes back as a raster, and there is no trace-to-paths anywhere in the stack.** So on a
`vector_infographic` — the workflow we develop most — the one form you *can* requisition arrives as a
bitmap and inlines as base64 inside the SVG deliverable. It renders correctly and it is not vector.

That is a different complaint from "no finished pictures". The refusal to sell a finished picture is
a position; the inability to get a requisitioned *shape* into vector output is a missing conversion.

## What the record shows an agent doing

From `projects/kubrick8`, unprompted, in three steps:

```
matte  "stencil silhouette portrait of Stanley Kubrick"  → RefusedLikeness
photo  "Stanley Kubrick"                                 → refused: described as 'American filmmaker
                                                           and photographer', which does not mention
                                                           'director'
photo  "Stanley Kubrick"                                 → success, licensed, credited
```

The designed route held: a generated likeness was refused, `expect` caught a real Wikidata mismatch,
and the agent finished with a licensed photograph. **For a subject that exists, `Photo` is the
answer and the gap does not bite.** It bites for a subject that does not — an idea, a motif, a
device — which is exactly what a section marker or a masthead usually is.

## Three ways out, and what each costs

1. **Trace a matte into paths.** Fills the accidental half and nothing else: the silhouette becomes
   real geometry, survives `outSvg`, scales, and can be `subtract`ed and filled per region. Costs a
   contour tracer we do not have and would have to write or vendor; a traced bitmap also carries
   pixel-edge noise that needs simplification, and `CanvasPath.simplify()` is already there for it.
   **Does not** get you facets or palette — one shape, cleanly.

2. **Extend the drawing toolkit.** More `Logo.*`-style constructions, or a small stencil library of
   drawn motifs. Everything stays vector and in palette, costs nothing at runtime, and is entirely
   ours. But it is unbounded work: the space of "a moon, a rocket, a reel" has no edge, and a
   library of forty motifs is forty things to maintain and still the wrong forty.

3. **Leave it.** `Photo` for what exists, code for what does not, and say so in `accuracy.md` when a
   piece wanted an illustration it could not have. Cheapest, honest, and the current behaviour.

## Recommendation

**(1), and not yet.** Tracing is the only one that closes a real hole rather than widening a surface,
it is bounded work, and it makes every existing matte more useful rather than adding a new thing to
learn. But nothing is blocked on it today: the runs we care about are data graphics, where the
missing artwork is decorative, and `Photo` covers the subjects that carry meaning.

**What would decide it:** a brief whose deliverable is genuinely illustrative rather than
quantitative. If the next few runs keep reaching for `matte` and settling for a bitmap in an SVG,
that is the signal. The `asset.requisition` events record every attempt, so the question is
answerable from the record rather than from memory.
