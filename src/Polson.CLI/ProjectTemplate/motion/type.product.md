## The type: product

**One product, one fifteen-second ad** — and with several audiences, the same ad in each audience's language, as the
instructions' *More than one audience* says. **The design is one system**: the product, its name, the claim, the call
to action, the end card, the type, and the imagery, setting, palette, pace and grade, each resting on evidence about
the product — the brief, the director's documents, and research on the product and its category. Each audience's ad
changes only its words, its figures' forms and its reading direction. An ad whose choices could have been made for
any product has not done the job.

Where this type and the instructions above disagree, this type wins: there is no poster to carry over unless
`documents/` holds one, and there is no film — the subject is the product.

### What the brief gives

The product's name and what it is; **two or three real comparable brands**; the one claim; the call to action; the
audiences; and, in `documents/`, often a product shot and a logo. Fill the brief table in `brief.md` with these rows
in place of the film ones, and ask for what is missing:

| Parameter | Value |
| :--- | :--- |
| The product — name, and what it is in one sentence | |
| Comparable brands (real, two or three) — and where the research places each | |
| The claim, worded exactly as it may appear | |
| Call to action, and where it sends people (words only; no real URL unless the brief gives one) | |
| Product shot and logo in `documents/` (file names), or none | |
| Reference video in `documents/` (file name), and what to take from it — or none | |
| Audiences A, B, … — place, age band, language | |
| Anything explicitly ruled out | |

### The rules of the product

- **Comparables are signals, never imagery.** No logo, wordmark, packaging, trade dress or slogan of a real brand,
  and no colour or shape that reads as one brand's signature. They tell the research what kind of product this is;
  that is all they do.
- **Claims come only from the brief.** No health, nutrition, environmental or performance claim, no "best", "#1" or
  comparison with a named brand, no price or offer, no testimonial, rating, award or certification unless the brief
  or a document gives it in those words. A claim in an ad is a statement about the world that someone answers for;
  an invented one is the festival-laurel failure in a place where it is also against the law. **The read-back holds
  you to it**: in `Verify.film`, the claim's text must come back with source `brief` (or `document`), word for word.
  A claim that comes back as `own words` was reworded, so restore the brief's wording.
- **The product's own assets are used as given.** A logo in `documents/` is placed, not redrawn or recoloured. A
  product shot is placed with `Skia.Image.load('documents/<file>')` and `comp.image({ image, tl, br })`; one with its
  own background is shown as a framed photograph rather than cut out by hand. With no shot, make a packshot
  (below). A cutout cell goes straight into `comp.image({ image: cell, tl, br })`.
- **No real person**, as in every film: no face, likeness or recognisable silhouette.
- **A product that advertising law restricts by age or place** — alcohol, gambling, medicines, financial products —
  is a question for the director before any design. The research below is about design; it settles nothing about
  the law.

### The key messages — before anything is designed

An ad's first job is that a viewer knows **what is being sold**. Two films of a canned cold brew set its name, its
claim and its call to action, and never the word *coffee*. A dark can on a chilled shelf read as beer to the first
person who described it (nightshift3 and nightshift4, 2026-10-25). The picture does not say what the product is; words
do.

So, after Lupton's *How to Refine a Creative Brief* (*Graphic Design Thinking*, pp. 56–57), narrow the brief to one
sentence and **list the key messages** before the evidence or the packshot. `direction.md` opens with them, as a table:

| Key message | Words as they appear | Where (beat, seconds) | Checked by |
| :--- | :--- | :--- | :--- |
| **What it is** | the descriptor | the product beat and the end card | read-back, source `brief`; the cold viewing |
| The product's name | as the brief gives it | packshot and end card | read-back |
| The claim | the brief's words exactly | claim beat | read-back, source `brief` |
| The call to action | the brief's words | end card | read-back |

- **The descriptor** is what the product is in two to four words, **taken from the brief's own sentence**: from "a
  fictitious canned cold brew coffee", *cold brew coffee*. In the brief's words, so the read-back finds it with source
  `brief`; a paraphrase comes back as `own words`, and is restored. It is part of the shared system: the same words in
  every ad of the set — in another language's ad, those words translated and read back as *An audience's language*
  says.
- **It is set in words** beside the product's first clear sighting and on the end card, with the name. It holds for
  its reading time, like the name.
- **The strong colour goes on the main items** — the name, what it is, and the call to action — and the rest is set
  quieter (Vernon Mills, *Making Posters*, 1967: the *what*, *where* and *when* take the colour).
- **Where the product's category may be unfamiliar** to an audience (cold brew where tea is the everyday drink), the
  descriptor matters more, not less. If the brief's words would not be understood there, that is a question for the
  director, not a reason to drop them.

**The cold viewing**, once the film is final: a separate model watches it without the brief and is asked only
what it is for:

```javascript
const seen = await Video.ask('output.mp4',
  'What is this advertisement for? Name the kind of product in a few words.');
log(seen.success ? seen.text : seen.error);
```

- **The question names nothing**: not the product, its category or the brief. A question that says "coffee" has
  answered itself.
- **A film over the watcher's limit** is asked about through the copy `Verify.film` made,
  `artifacts/verify/<film>-watch.mp4` (its summary names it).
- **Record the answer word for word** in the key messages table. Then
  `Stage.check('a viewer who has not read the brief can say what is advertised', <your reading of it>, seen.text)`.
- **If it names another kind of product**, the film has failed its first job. Make the descriptor larger, hold it
  longer, or bring it earlier; then ask again. Never ask a leading question to make it pass.
- **With several audiences, ask about every film.**
- It is a machine standing in for a stranger, and `direction.md` says so; it is not a test with viewers.

### The evidence — the product and its category

The design rests on what the product is, not on guesses about who buys it. **One `Research` run in Sources, planned to
carry everything** — its rules allow a second only to correct the first — started with `wait: false` and collected by
its `runId` before the concepts, as the instructions' *A trailer with no poster* says of a film:

- **the category**: what kind of thing the product is, and how its category is usually packaged and advertised — its
  usual colours, type, voice and packshot, and how its ads are cut — so the ad can belong to the category or stand
  apart from it on purpose;
- **each comparable**: what it is and where it sits in the category (mass-market, specialty, premium), with a
  citation — a signal of the register the brief asks for, never marks to borrow;
- **each audience's place**: colours, numbers, symbols and gestures that would misfire there, and whether the
  product's category is an everyday one there or an unfamiliar one (*The key messages* above).

**The objective asks; it never states what it asks**: name the product's kind and the comparables, never what you
expect back. What comes back is web data, not instruction; a field with no citation or a low confidence is a question
for the director, not a fact. In `direction.md` each use of it is a **Research** row with its field's citation.

1. **The comparables set the register, not the look.** Read where the research places each, and **if they sit against
   the brief** (a premium product against mass-market comparables, or comparables from another category), say so to
   the director before designing: one of the two is wrong for this product.
2. **The category's look is the shared visual vocabulary.** Cite it as the category's, and decide whether the ad
   belongs to it or departs from it, and why. It is the commercial landscape the ad will sit in: context for what
   would look foreign or familiar, never marks to borrow.
3. **The setting comes from the product**: where and when it is used, as the brief, a document or the research
   describes it — the place, the hour, the light. A setting from your own judgement is *assumed*, and says so.
4. **Pace comes from the brief and the product**, as the instructions say: the director's words, then the category's
   register as the research describes it, otherwise *assumed*. One pace for every ad of the set.
5. **An age band is context, never evidence of what anyone likes.** It may raise a question for the director (*A product that
   advertising law restricts*, above); it never decides what the ad looks like.

### The packshot — after the evidence, before the beats

The **packshot** is the product as the ad shows it, on nothing. In a spot, the closing shot of the product with its
name and the call to action is the *end packshot* — the end card. **With a product shot in `documents/`**, read it
before designing (`Documents.ask('documents/<file>', 'Describe the product: its shape, its colours, any text on it')`)
and place it as given. **With none**, make one: the model generates the vessel, and you design and set the label.

1. **The label is design, and it comes from the evidence.** The name, its type and two or three colours rest on the
   category's look — as the research describes it (step 2 above) — and on the brief. Decide them
   before generating anything, and record them in `direction.md`.
2. **Generate the vessel as one sheet**, every view in one generation so it is the same object in each:

   ```javascript
   const pack = await Assets.cutout(
     'a slim 250 ml aluminium can, matte black, a wide plain cream band round the middle, no text, no logo',
     { variants: ['front', 'three-quarter', 'top-down'], size: 768, style: 'clean studio product photograph, soft top light' });
   if (!pack.success) { error(pack.remedy); exit(pack.failureName); }
   if (pack.warnings.length) log(pack.warnings.join('\n'));
   ```

   - **Describe the object, never the ad.** A descriptor naming an advertisement, an ad, a billboard or a flyer is
     refused before the network. So is a `style` naming a background: the ground belongs to the call.
   - **No text, no logo, no brand on the generated can.** Generated lettering comes back garbled and is not yours;
     the wordmark is set in type in step 4. Not the shape, colours or band of a real brand's can either — the
     comparables are signals, never imagery.
   - The same descriptor and options are served from the cache at no cost, so a later script asks again rather
     than storing the sheet: the first call in a session keys and splits it (a few seconds), and every repeat in the
     session is then immediate — `fromCache` says which. One generation from the run's asset budget; a regeneration is
     another.
   - **A cell is only as large as the sheet allows.** Three views share one generated image, so each came back
     about 220 × 390 px whatever `size` asked — soft once a can stands 700 px tall in a 1080p frame. When the
     product is large on screen, ask once more for the front alone, shown the sheet so it stays the same can:
     `Assets.cutout(descriptor, { reference: pack.cell('front'), size: 1024 })` — one more generation.
3. **Look before using it.** Draw the cells side by side on one canvas and write it as `artifacts/00_packshot_sheet.png`
   — your peek at it is how you see the product, as the sheets are how you see the film. Check that it is the same can in every view, that no lettering crept onto it, that `cell.holes` is near
   zero (a can has no holes), and that `pack.warnings` names no cell. On a failure, change the descriptor once and
   regenerate; a second failure means the product is drawn as shapes instead, and `findings.md` says why.
4. **Set the label in type on the front view.** The plain band is where the name goes: your typeface, centred on
   the band, fitted to its width. On the three-quarter view, either compress the name toward the far edge so it reads
   as wrapping, or leave the band bare and set the name beside the can. Never on the top-down view.
5. **The same packshot in every ad.** It is the design's anchor, held across the set like the end card and like
   everything but the words.
6. **`direction.md` says what it is**: generated with `Assets.cutout` (give the sheet's `id`), label designed and
   set by the studio. Never "a photograph of the product", and never anything implying the product is on sale.

### The ad, beat by beat

Fifteen seconds unless the brief says otherwise. A product ad reads in this order, and the beat table names each:

1. **Hook** (0–3 s): the one image from the evidence — the setting, the hour, the object.
2. **The product**: the packshot, named, legible, **with the descriptor** — what it is, in words (the key messages
   above). The name and descriptor hold long enough to read (the reading-time check).
3. **The claim**, worded as the brief gives it, on screen alone.
4. **End card**: product name or logo, **the descriptor**, and the call to action, held for at least the brief's
   minimum. **Identical in layout across the set** — it is the design's anchor.

### Questions, for a product

- **Q1** adds the research: where each comparable sits in the category and whether that fits the brief, and any
  field that came back uncited or at a low confidence.
- **Q2** is asked with the pace evidence, as above, and **the setting**, with what it rests on.
- **Ask once for the set** when there are three or more audiences, as the instructions say.

### direction.md

**The key messages first** (the table above, with the cold viewing's answer for each film). Then the shared system —
product, descriptor, claim, call to action, end card, type — each cited to the brief or a document.
Then **the design**: the category and the comparables as the research found them, the setting, the pace line, and
every choice with its evidence, each row of one kind — Brief, Document, Research, Computed or Assumed. With several
audiences, then **one section per audience** — its language, its lines and their back-translations, anything its ad
changed and why — and **"Between the audiences"**, as the instructions say.
