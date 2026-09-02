# Declarative Semantic Memory — corpus map and retrieval shape

Working note for the `Memory.semantic` surface. Covers what is ingestible today, what the
retrieval contract needs beyond what `KnowledgeCorpus` already provides, and what the agent
harness should measure about retrieval quality.

Numbers are measured from the live corpus, not estimated.

---

## 1. What is ingestible today

### 1a. Already indexed — 171 chunks, 196 KB

`KnowledgeCorpus.Build()` chunks two bodies at markdown heading boundaries and
`LocalKnowledgeIndex` serves them. This is the working baseline.

| Scope | Chunks | Chars | min / median / max |
| :--- | ---: | ---: | :--- |
| `Manual` — 12 studio manuals | 103 | 120,738 | 214 / 1,103 / 2,954 |
| `Sdk` — core + schema, 8 areas | 68 | 74,914 | 184 / 962 / 4,131 |

43 of 171 chunks carry no API citation at all — they are pure design theory. That ratio is
worth watching: it is roughly the share of the corpus that answers "how should this look"
rather than "what do I call".

**Three chunks exceed the 2,400-char target and cannot be split further** because they have no
level-3 headings to descend into:

| Chars | Chunk |
| ---: | :--- |
| 4,131 | `polson://sdk/core/Snap` — `SnapElement` |
| 3,406 | `polson://sdk/schema/Drawing` — `LoomisHead` |
| 2,993 | `polson://sdk/core/Drawing` — Volumetric Lighting & Cast Shadows |

`SnapElement` is the worst case and also one of the most-queried surfaces. Adding `###`
subheadings inside it (creation, traversal, geometry, z-order) would fix the chunking without
changing a word of content.

### 1b. A whole area was invisible until this session

`VectorLogo` sliced to `null` and was therefore absent from **both** `polson://sdk/core/VectorLogo`
and the search corpus. The cause: `SdkDocs.Names()` splits a heading on `/`, `—` and `-`, but the
heading is `VectorLogo & Snap.svg Logo Methods`, so nothing matched. Fixed by giving the area its
real heading text in `SdkDocs.PolsonAreas`.

The failure mode is the one that matters here. Search did not return nothing — it returned
confident, plausible, wrong answers:

| Query | Before (top hit) | After |
| :--- | :--- | :--- |
| "squircle path on a paper" | `sdk/core/Snap` → `SnapPaper` | `sdk/core/VectorLogo` |
| "emblem badge shield vector" | `sdk/core/Logo` (raster) | `sdk/core/VectorLogo` |
| "clearSpaceGuide" | `sdk/core/Logo` (raster) | `sdk/core/VectorLogo` |

An agent asking for a vector squircle was told about `Logo.drawSquircle(ctx, …)`, the **raster**
call, and would have written Canvas2D code for a vector job. This is the same failure that once
convinced a harness agent that vector gradients did not exist. It is not a ranking problem that
better embeddings fix — a similarity search has no way to say *"that is not here."* See §3.

### 1c. Janson — extracted, but NOT for the public corpus

**Decision (2026-08-27): the Janson extraction is private-testing material only.** *The DC Comics
Guide to Pencilling Comics* is in copyright, and a retrieval corpus that serves its text to agents
in a publicly available studio would be redistribution. It stays in `reference/` for local
evaluation and is excluded from the ingestion order below.

*Imaginative Drawing* (John Guy) was proposed as the corpus to build on instead, on the strength of
its "intended to be freely shared" line. **That reading was wrong and the book is now excluded.** The
same notice continues: share it *only in its entirety*, do not distribute parts or pages separately,
and — explicitly — no permission is granted for the work or any part of it to be used to train
machine learning or artificial intelligence. A distilled manual served to agents through
`polson://manual/*` is against both clauses. We are not "training" in the technical sense, but the
purpose of the manuals is to let an AI system do what the book teaches, which is plainly what the
author was declining. Withdrawn from the manuals on 2026-09-02; see §4.

Candidate replacements, none of which carries a comparable restriction: Loomis's *Figure Drawing for
All It's Worth* and *Creative Illustration*, and Faragasso's *Mastering Drawing the Human Figure*.
They are in copyright and get the same handling as Bokhua — distil, write it in our own words, cite
the chapter, never reproduce at length.

The extraction detail is retained here because the chunking problem it exposes applies to any
scanned book, and all three candidates are scanned too.

### 1d. The Janson chunking problem, which generalises

`reference/books/janson-pencilling/` — 20 files, 220 KB, 171 figures, 122 captions, 77 page
markers. Verbatim EPUB extraction, verified against source, codepoint-scanned clean.

**Its structure does not match the manuals, and the current chunker would handle it badly.**
54 headings across 37,311 words, and several chapters carry almost none:

| Chapter | Headings | Words |
| :--- | ---: | ---: |
| 02 Shapes | 1 | 1,201 |
| 04 Anatomy | 10 | 4,538 |
| 05 Clothing | 2 | 652 |
| 07 Juxtaposition | 2 | 1,651 |

Chapter 2 would become one ~7,000-char chunk with no subheading to split on — nearly three times
the target and larger than anything currently in the corpus.

The book has different natural boundaries than the manuals do:

- **Figure + caption** is a self-contained instructional unit and there are 122 of them. Captions
  carry a large share of the actual teaching ("Notice how the hanging lamp draws the eye into the
  first panel"). A caption must never be separated from its figure reference.
- **Page markers** (`<!-- p.63 -->`) give 77 more boundaries and are the citation anchor.

So Janson needs its own chunk strategy: paragraph groups bounded by page markers, with
figure-and-caption kept atomic. Neither the default RAG splitter nor the current heading splitter
is right for it.

### 1e. *Imaginative Drawing* is excluded (§1d), and was never ingestible anyway

The source PDFs are **scanned images with zero extractable text** — `mutool draw -F txt` yields 0
words from all five chapter files. What exists is a **53-page PNG sample** of a ~640-page book:

| Chapter | Pages sampled | Range |
| :--- | ---: | :--- |
| 1 Observation | 25 | 34–75 |
| 2 Perspective | 9 | 154–246 |
| 3 Light | 6 | 274–323 |
| 4 Anatomy | 7 | 350–551 |
| 5 Composition | 6 | 580–627 |

Chapter 4 covers roughly 200 pages of anatomy with seven images. Before any of this can be a
retrieval corpus it needs OCR or a full page render, and then a fidelity pass — see §4.

### 1f. Ingestion order

1. **SDK core + schema** — already indexed; `SnapElement` subheadings done.
2. **Studio manuals** — indexed, but see §4: their sourcing is not trustworthy yet.
3. *Imaginative Drawing* — **excluded** on the author's stated terms (§1d), not merely blocked.
   Loomis and Faragasso are the replacement candidates; all are image-only and need OCR.
4. Janson — **excluded** from the public corpus on copyright grounds (§1c).
5. Remaining `reference/books/*.pdf` — **blocked**: no codepoint-scan verdict in
   `reference/README.md`, extraction quality unverified.

## 2. Retrieval shape

The existing contract is close and should be extended rather than replaced:

```csharp
KnowledgeChunk(Uri, Scope, Title, Section, Text, Apis)
KnowledgeHit(Uri, Title, Section, Source, Score, Apis, Text)
IKnowledgeIndex.SearchAsync(query, k, scope, ct) → IReadOnlyList<KnowledgeHit>
```

Three additions.

### 2a. `Citation` — provenance on every hit

Third-party material needs attribution, and the Facilitator needs to audit where a claim came
from when evaluating a pull request.

```csharp
public sealed record Citation(
    string Work,        // "The DC Comics Guide to Pencilling Comics"
    string? Author,     // "Klaus Janson"
    string? Locator,    // "Chapter 6, p.63"  — from the <!-- p.NN --> markers
    string? Figure);    // "Figure 6.5"       — when the hit is a figure unit
```

For SDK and manual chunks this is derivable and cheap; for `Reference` chunks it is mandatory.

### 2b. `KnowledgeAnswer` — an explicit no-match signal

This is the important one. `SearchAsync` returning a bare list can only ever say "here are the
nearest things", which is what produced §1b. The result must be able to say *nothing here matches*
and say where to look instead.

```csharp
public enum KnowledgeConfidence
{
    Direct,    // an exact symbol match, or a strong prose hit well clear of the runners-up
    Related,   // plausible but not clearly on-target — treat as background, not as an answer
    NoMatch,   // nothing in the searched scopes answers this
}

public sealed record KnowledgeAnswer(
    IReadOnlyList<KnowledgeHit> Hits,
    KnowledgeConfidence Confidence,
    string? Advice,                            // "No such call. Nearest: paper.squircle, Logo.drawSquircle."
    IReadOnlyList<KnowledgeScope> Searched,
    IReadOnlyList<KnowledgeScope> NotSearched); // so an agent knows what it did NOT ask
```

`NotSearched` matters: an agent that queried `Sdk` and found nothing should be told the manuals
and reference works were never consulted, rather than concluding the knowledge does not exist.

### 2c. `SymbolIndex` — the mechanism that can say "no"

Similarity search cannot answer *"does `paper.squircle` exist?"* — it will always return the
nearest neighbour with a plausible score. That question needs an exact index, not an embedding.

```csharp
public sealed record Symbol(
    string Name,        // "paper.squircle"
    string Receiver,    // "SnapPaper"
    string Area,        // "VectorLogo"
    string Uri,         // "polson://sdk/core/VectorLogo"
    string Signature);  // the documented line, verbatim

public interface ISymbolIndex
{
    Symbol? Resolve(string name);                  // exact — null is a real answer
    IReadOnlyList<Symbol> Nearest(string name, int k);
    IReadOnlyList<Symbol> OnReceiver(string receiver);  // "what can I call on a paper?"
    IReadOnlyList<Symbol> InArea(string area);
}
```

Built by parsing the `- \`X.y(args)\` → Return` bullets already in `Polson.core.md`, so it stays
in step with the reference by construction. It gives three things similarity search cannot:

- **A definitive negative.** `Resolve` returning null means the call does not exist.
- **Enumeration.** "What methods exist on `SnapPaper`?" is a listing, not a search.
- **Drift detection.** A symbol documented but absent from the assembly (or the reverse) is a
  doc/code drift bug, findable in CI rather than by an agent at runtime.

**Routing rule:** a query that looks like a symbol (`identifier.identifier`, camelCase, or
backticked) goes to the `SymbolIndex` first. Everything else goes to prose retrieval. A symbol
miss returns `NoMatch` with nearest names — never a prose hit dressed as an answer.

---

## 3. What the harness should measure

Retrieval quality is currently invisible: the harness measures whether the drawing came out, not
whether the agent could find what it needed. These are cheap to instrument and directly target the
"agent retries because it lacks knowledge" problem.

**Logged automatically per session:**

| Metric | Why |
| :--- | :--- |
| Queries issued, with scope, top score, and `Confidence` | Baseline. A session full of `Related` answers is a corpus gap. |
| `NoMatch` rate, and the query text of each | The direct list of what the corpus does not cover. |
| Searched-then-used: did the agent call an API from the area it retrieved? | Distinguishes retrieval that helped from retrieval that was ignored. |
| Calls written that do not resolve in `SymbolIndex` | A hallucinated call is a knowledge miss with a precise cause. |
| Hand-rolled code duplicating an existing toolkit call | The expensive failure: the agent did not know `Drawing.projectCastShadow` existed and wrote 40 lines instead. |
| Iterations and wall-clock to first correct call | The number the whole memory effort is meant to move. |

**Asked of the agent at session end** — self-report is cheap and unusually informative:

1. What did you look for and fail to find?
2. Which answers were misleading — you acted on them and they were wrong for your task?
3. What did you have to work out by trial and error that you would have expected to be documented?

Question 2 is the one that catches §1b-class faults. A confident wrong answer costs far more than
a null one, and it is invisible to any metric that only counts whether the query returned rows.

**Suggested acceptance targets** once the corpus is complete: `NoMatch` under 10% of queries, zero
unresolvable calls written, and no hand-rolled duplication of a documented toolkit call.

---

## 4. The manuals are not sourced the way they claim

Manuals 05–09 each opened with `> **Source Reference**: *Imaginative Drawing*, Chapter N` and then
attributed specific claims to specific pages — `> **Core Insight from the Book (Page 350 & 550)**`.
Nine of those citations were eventually checked by OCR. **Four do not support the claim**, including
the eight-head canon, which is not in the book at all.

**All of them have since been withdrawn** — see §1d — because the book's terms exclude it regardless
of whether a given citation was accurate. What remains in the manuals is standard studio practice
carrying no attribution, which is honest but unsourced: every such claim is pending a citation to
Loomis or Faragasso and should be treated as unverified until it has one. The table below is kept as
the record of how the citations failed, since the failure mode is the point.

| Manual | Cites | Claim attributed | What the page actually is |
| :--- | :--- | :--- | :--- |
| 08 Anatomy | p350 & p550 | the 8-head proportional canon | p350 = "4.3 Anatomical Information: The Skeleton"; p550 = "4.5 Simplification". Neither mentions head units. |
| 08 Anatomy | p550–551 | blocking from volumetric primitives | p551 is four stylisation examples (naturalistic → cartoon). No volumetric blocking. |
| 07 Lighting | p323 | three-point key/fill/rim | **Correct** — p323 is "3.6 Common Lighting Setups". But the intensities and angles are invented. |
| 09 Composition | p580 & p600 | geometric armatures, rule of thirds, golden ratio | p580 = "5.2 Emphasis" — visual weight, focal point, attentional hierarchy. No armatures. |

Manual 09 also cites **page 595, which was never extracted** — a citation to a page nobody read.

The p323 case is the subtlest and the most instructive. The concept is faithfully carried over, but
the manual adds `~70%` / `~30%` / `~90%` intensities and `-45°` / `+60°` angles, and
`Drawing.createThreePointLighting` ships those as defaults (`0.75f`, `0.30f`, `0.90f`, `-45f`,
`60f`). The page itself says these setups are *"jumping-off points rather than formulas to be
followed exactly."* Invented precision was attributed to a source that explicitly disclaims it.

**The content is not necessarily wrong.** The 8-head canon, the rule of thirds and Φ are real,
standard art instruction, and sensible API defaults are sensible regardless of provenance. The
problem is that the manuals present themselves as a distillation of *this book* and are not one.
A citation nobody can check is worse than no citation, because it manufactures confidence — the
same failure class as an API reference that documents a call which does not exist.

The reverse is also true: the manuals **miss what the source does teach**. Page 580's visual-weight
and attentional-hierarchy material is genuinely useful and absent from Manual 09; page 551's
stylised-proportion material is absent from Manual 08, which was separately flagged as a gap.

### What to do

- **Do not ship the manuals as sourced references.** Either strip the page citations — leaving them
  as what they are, generic studio guidance, which is still useful — or re-derive them from pages
  actually read.
- **Add the cheap mechanical guard now**: assert every `Page N` citation names a page present under
  `reference/books/chapter*_pages/`. It cannot check semantic fidelity, but it stops citations to
  pages nobody has seen, and it would have caught page 595.
- **Then re-derive properly**: OCR or render the full chapters, and check each manual section
  against the pages it cites. A 53-page sample of a 640-page book cannot support chapter-level
  claims.

---
