## The type: product

**One product, a set of audiences, one fifteen-second ad each**, each moving in the register of what that
audience's buyers love. This is what advertising calls dynamic creative: one template, a variant per audience.
Here you design both — **the template is the system the set shares** (the product, its name, the claim, the call
to action, the end card, the type), and **each variant is what that audience's evidence decides** (imagery,
setting, palette, pace, grade). An ad that would look the same without the evidence has not done the job.

Where this type and the instructions above disagree, this type wins: there is no poster to carry over unless
`documents/` holds one, and there is no film — the subject is the product.

### What the brief gives

The product's name and what it is; **two or three real comparable brands**; the one claim; the call to action; the
audiences; and, in `documents/`, often a product shot and a logo. Fill the brief table in `brief.md` with these rows
in place of the film ones, and ask for what is missing:

| Parameter | Value |
| :--- | :--- |
| The product — name, and what it is in one sentence | |
| Comparable brands (real, two or three) — and what each resolved to | |
| The claim, worded exactly as it may appear | |
| Call to action, and where it sends people (words only; no real URL unless the brief gives one) | |
| Product shot and logo in `documents/` (file names), or none | |
| Reference video in `documents/` (file name), and what to take from it — or none | |
| Audiences A, B, … — place, age band | |
| Anything explicitly ruled out | |

### The rules of the product

- **Comparables are signals, never imagery.** No logo, wordmark, packaging, trade dress or slogan of a real brand,
  and no colour or shape that reads as one brand's signature. They tell Qloo what kind of product this is; that is
  all they do.
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
  is a question for the director before any design. Qloo measures taste; it knows nothing about the law.

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
  every ad of the set.
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

### The evidence, measured

Probed on a fictitious cold brew in Berlin, Lagos and Seoul (2026-10-19). What separates audiences for a product is
not what separates them for a film, so follow this rather than the film habits above.

1. **Resolve each comparable** (`Taste` op `resolve`, `type: 'brand'`) and **read what it matched**: "Starbucks
   Reserve" resolved to plain Starbucks, a mass-market brand in a specialty set, with no warning. Then
   `demographics` on the ids. **If the brief's age bands sit against the comparables** (an audience of 25–29 against
   brands that skew 35 and over), say so to the director before designing: one of the two is wrong for this product.
2. **The product's buyers there, less its buyers anywhere.** For each audience, `insights` for `artist` and for
   `brand` with the comparables as the signal and the audience's place and age; once for the whole set, the same
   with no audience. **Give every answer a label of its own** — 'audience A buyers: brand', never a label that
   another merely extends — and use `Taste.get(id)` where it matters. What an audience's list holds that the global one does not is **what is local about this
   product's buyers** — the strongest evidence a product has:

   ```
   Taste({ op: 'insights', type: 'brand', take: 50,
           audience: '{"location":"Lagos","age":"25_to_29"}',
           query: '{"signal.interests.entities":["<id>","<id>","<id>"]}',
           label: 'audience A buyers: brand' })
   Taste({ op: 'insights', type: 'brand', take: 50,
           query: '{"signal.interests.entities":["<id>","<id>","<id>"]}', label: 'buyers anywhere: brand' })
   ```

   Measured: Lagos's buyers shared 3 of 50 brands and 0 of 50 artists with the global list; Berlin's and Seoul's
   shared about 20 brands each — and **shared 14 local brands with each other**. A product's buyers can converge
   across cities. When they do, say so and keep the difference where the evidence has one; do not invent one.
3. **Brand tags are the visual register.** Brands carry design-language tags no film has — "Minimalist Typography",
   "Clean Lines", "Neutral Color Palette", "Heritage-Inspired". **Read them in a script**,
   `Taste.find('audience A buyers: brand').entities`: the tool's reply shows only the first eight tags of each, and
   a brand carries forty or more. **Each tag is `{ id, name, type }`**, not a string. The design ones are
   `type: 'urn:tag:aesthetic_property:qloo'` — Minimalist, Clean Typography, Neutral Palette, Clean Packaging; count
   `tag.name` over those. Two more kinds serve a product: `urn:tag:emotional_tone:qloo` (Sophisticated) for the
   voice, and `urn:tag:packaging_format:qloo` (Can) for what the category's packshot holds. When
   every audience's brands carry the same ones, they are the **category's** look — cite them as that, for the shared
   system, and not as a difference between audiences.
4. **The setting comes from local places**, `insights` with `type: 'place'`, the comparables as signal, and
   `"filter.location.query": "<the place>"` in the query. **Without that filter the places are in other countries**
   (Lagos's came back as hotels in Houston and Atlanta). Keep the mood tags — Stylish, Romantic, Late night,
   Bustling, Sophisticated — for where and when the product is shown, and its light. Amenities ("Mini fridge",
   "Paid parking") are not evidence.
5. **Pace comes from the audience profile**, exactly as above (`pace()`, `comparePaceAll`). **Not from the buyers'
   films**: measured, Seoul's buyers read 33% fast at 25 titles and 59% at 50, and no pair of cities differed.
6. **The place's own brands** — the profile's baseline — are the commercial landscape the ad will sit in. Context
   for what would look foreign or familiar there; never marks to borrow.
7. `signal.location.weight` changed nothing when measured. Do not rely on it.

**Cost**: about ten requests per audience (the profile's seven, two for buyers, one for places) and seven for the set.

### The packshot — after the evidence, before the beats

The **packshot** is the product as the ad shows it, on nothing. In a spot, the closing shot of the product with its
name and the call to action is the *end packshot* — the end card. **With a product shot in `documents/`**, read it
before designing (`Documents.ask('documents/<file>', 'Describe the product: its shape, its colours, any text on it')`)
and place it as given. **With none**, make one: the model generates the vessel, and you design and set the label.

1. **The label is design, and it comes from the evidence.** The name, its type and two or three colours rest on the
   category's look — the design tags every audience's buyers share (step 3 above) — and on the brief. Decide them
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
5. **The same packshot in every ad.** It is the template's anchor, held across the set like the end card; the
   variation is everything around it — setting, light, the ground's palette.
6. **`direction.md` says what it is**: generated with `Assets.cutout` (give the sheet's `id`), label designed and
   set by the studio. Never "a photograph of the product", and never anything implying the product is on sale.

### The ad, beat by beat

Fifteen seconds unless the brief says otherwise. A product ad reads in this order, and the beat table names each:

1. **Hook** (0–3 s): the one image from this audience's evidence — the setting, the hour, the object.
2. **The product**: the packshot, named, legible, **with the descriptor** — what it is, in words (the key messages
   above). The name and descriptor hold long enough to read (the reading-time check).
3. **The claim**, worded as the brief gives it, on screen alone.
4. **End card**: product name or logo, **the descriptor**, and the call to action, held for at least the brief's
   minimum. **Identical in layout across the set** — it is the template's anchor.

### Questions, for a product

- **Q1** adds the comparables: what each resolved to, and whether their age skew fits the audiences.
- **Q2** is asked with the pace evidence, as above, and **the setting** from each audience's places.
- **Ask once for the set** when there are three or more audiences, as the instructions say.

### direction.md

**The key messages first** (the table above, with the cold viewing's answer for each film). Then the shared system —
product, descriptor, claim, call to action, end card, type — each cited to the brief or a document.
Then **one section per audience**: its buyers (local-only brands and artists, with counts), its setting (places and
mood tags), its pace line, and every choice with its evidence. Then **"Between the audiences"**: what the set shares
and why, what differs and the evidence for each difference, and which two ads came closest — with whether their
buyers were close too.
