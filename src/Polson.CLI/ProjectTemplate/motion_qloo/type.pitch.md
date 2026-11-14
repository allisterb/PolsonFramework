## The type: pitch

**A motion-graphics pitch made from a startup's own documents** — its pitch deck, and where it has them a financial
model, a metrics export or a one-pager. Two minutes by default; the deck's case told to investors, in the order
investors expect it, every figure on screen **read from the documents and checked against the page it came from**,
and every projection shown as a projection. A good one is what a founder would send ahead of a meeting: short,
exact, and confident without claiming anything the company did not claim.

Where this type and the instructions above disagree, this type wins: there is no poster to carry over and no film to
invent. The subject is the company's case. **Audiences are optional.** With none, make one film for the investors the
brief describes, and no Qloo profile is read (see *Pace* below). With `Audience A`, `Audience B`… lines — investors in
Singapore, in London — make **one film per audience**: the case the same in every film, read once from the
documents, and the telling of it each audience's own (see *More than one audience* below).

### What the brief gives

The documents, in `documents/` (find them with `Documents.list()`); the company; the stage; the raise and the use of
funds, **when the brief states them**; how it is told (below); the length; narration or none. Fill the brief table in
`brief.md` with these rows in place of the film ones, and ask for what is missing:

| Parameter | Value |
| :--- | :--- |
| The documents — file, what each is (deck, model, metrics), and its date | |
| The company, as its documents name it | |
| Told as — the company's own pitch, or a retelling of a past deck | |
| Stage, raise and use of funds — each from the brief, from the deck, or not stated | |
| Length (2 minutes by default) and narration (Enabled, or Disabled) | |
| Anything explicitly ruled out | |

**Told as** decides the voice, and nothing else changes with it:
- **The company's own pitch** speaks as the company — *"we"*, the present tense — and ends on its ask.
- **A retelling of a past deck** (a famous seed deck, a case study) speaks *about* the company, **dated**: *"In 2008,
  UberCab's founders projected…"*. It is the deck as of its date. **No hindsight**: nothing about what became of the
  company, its valuation or its later rounds — none of it is in the documents, and a retelling that knows the ending
  is a different film. The title says what it is (*"Based on UberCab's 2008 pitch deck"*), and the end card says
  *"A retelling for illustration. Not affiliated with <company>."*

### Stage 1 — the reading: the deck in one call

`Documents.ask` reads a PDF **as text, page by page**, with Nemotron 3.5 Super VL, OCRs a page that is a scan, and
returns **evidence**: the page and the exact figures each answer used, **each already looked for on its page by the
engine**. Read each document in **one** call, with a schema, and **keep the calls at the top of `motion.js`**: the
same question of the same document is served from the cache for nothing, so every later run asks again rather than
copying numbers by hand.

```javascript
const DECK = 'documents/<the deck>.pdf';
const ASK = 'This is a startup pitch deck. Summarise its case section by section, for investors. For each section give '
    + 'the page, one plain sentence of what the deck claims, and every figure the deck prints for it, exactly as printed, '
    + 'with its unit and the period or date it refers to. Mark each figure\'s kind: "actual" for a result already '
    + 'achieved, "projection" for a forecast, "scenario" for one of several outcomes the deck sets side by side, '
    + '"target" for a goal, "estimate" for a market size or other outside estimate. Where the deck states how a figure '
    + 'is reached (an assumption, a multiplication, a source), give that as printed in basis. List only what the deck '
    + 'says: no figure, team member, competitor or claim it does not contain.';
const KINDS = ['actual', 'projection', 'scenario', 'target', 'estimate'];
const SECTIONS = ['problem', 'solution', 'product', 'why_now', 'market', 'traction', 'business_model', 'competition',
    'advantage', 'go_to_market', 'team', 'risks', 'financials', 'ask', 'use_of_funds', 'other'];
const SHAPE = { type: 'object', required: ['sections'], properties: { sections: { type: 'array', items: {
    type: 'object', required: ['section', 'page', 'claim', 'figures'], properties: {
        section: { type: 'string', enum: SECTIONS },
        page: { type: 'integer' },
        claim: { type: 'string', description: 'what the deck claims here, in one plain sentence' },
        figures: { type: 'array', items: { type: 'object', required: ['label', 'printed', 'unit', 'period', 'kind', 'basis'],
            properties: { label: { type: 'string' }, printed: { type: 'string' }, unit: { type: 'string' },
                period: { type: 'string' }, kind: { type: 'string', enum: KINDS }, basis: { type: 'string' } } } } } } } } };

const read = await Documents.ask(DECK, ASK, { schema: SHAPE });
// 'Pending': the read outlived this script and is finishing into the cache. Run the script again, unchanged.
if (!read.success) { error(read.remedy); exit(read.failureName); }
if (read.warnings.length) for (const w of read.warnings) error('SCAN: ' + w);
const sections = JSON.parse(read.text).sections;

// A figure is usable when every number in it, as printed, is in a quote the engine found on its page — each number
// separately: "$20-30M+ per year" is two numbers.
const numbersIn = s => (String(s).match(/\d[\d,]*(?:\.\d+)?/g) || []).map(n => n.replace(/,/g, ''));
const onPage = new Set(read.evidence.filter(e => e.found).flatMap(e => numbersIn(e.quote)));
for (const s of sections) for (const f of s.figures)
    f.found = numbersIn(f.printed).length > 0 && numbersIn(f.printed).every(n => onPage.has(n));
log(`${read.provenance.model} via ${read.provenance.route}, ${read.provenance.pages} pages; unverified: ${JSON.stringify(read.unverified)}`);
```

**A financial model, a metrics export or a one-pager is read the same way, in a call of its own**, asking for the
figures the film will use — revenue, burn, runway, gross margin, unit economics, by period — each with its kind. Where
the model and the deck disagree on a figure, both are the company's: show neither until the director says which (Q1).

- **The check reads digits, not signs.** A figure printed with a minus or in parentheses is negative: carry the sign
  over as printed, and compare it with the page yourself.
- **Find a figure by its section and its place in the answer, never by its label**, which a second reading words
  differently.
- **`read.provenance.model` says who read it.** When it is not Nemotron, `read.provenance.fallback` says why, and
  `read.evidence` is empty: **every figure is then unchecked.** Say so in `direction.md` and to the director in Q1.
- **A figure not `found` stays off the screen.** Ask again, narrowly, naming the page — one figure a call.
- **A reading of a picture is not a checked figure.** A deck carries much of its case in images — a chart, a diagram,
  a slide photographed — and what the model read from them is in `read.fromImages`, **unchecked even when found**.
  Put each in Q1 for the director to confirm, label it in `direction.md` as *read from the image on p. N*, and never
  let it stand as checked. A caption too small or blurred to read is not read: leave it out and say so.
- **Record it**: `Stage.check('every figure on screen was found on its page', …, 'N found, M computed, K from the brief, L left out')`.

### Stage 1a — the questions an investor asks of it

**A deck is read by people deciding whether to back the founders, and they come to it with questions of their own.**
Calacanis (*Angel*, 2017) spends the first half of every founder meeting on four, and the second half on quick
tactical ones; the nine below are his, in our words. Put them to the deck in **one** call, after the reading and before
any scene is planned:

```javascript
const QUESTIONS = [
    'What is the company building: the product, in one sentence?',                          // ch. 18: the four
    'Why are the founders doing this: the personal reason the deck gives, if it gives one?',
    'Why now: what has changed that makes this possible, or needed, at this moment?',
    'What is the founders\' unfair advantage: what they have or know that competitors and fast followers do not?',
    'Who does the deck name as competitors or alternatives?',                               // ch. 19: the tactical five
    'How does the company make money?',
    'What does it charge?',
    'What does an average customer spend, or an average transaction come to?',
    'What reasons does the deck itself give that the company might fail: its risks?'];
const ASKED = await Documents.ask(DECK,
    'An investor reading this deck asks the questions below. For each, say whether the deck answers it; if it does, give '
    + 'the page and the answer in the deck\'s own words, quoting its sentence. If it does not, say so: never answer from '
    + 'general knowledge or from what a deck like this usually says.\n' + QUESTIONS.map((q, i) => `${i + 1}. ${q}`).join('\n'),
    { schema: { type: 'object', required: ['answers'], properties: { answers: { type: 'array', items: { type: 'object',
        required: ['n', 'answered', 'page', 'answer', 'quote'], properties: { n: { type: 'integer' }, answered: { type: 'boolean' },
            page: { type: 'integer' }, answer: { type: 'string' }, quote: { type: 'string' } } } } } } });
if (!ASKED.success) { error(ASKED.remedy); exit(ASKED.failureName); }
const answers = JSON.parse(ASKED.text).answers;
const gaps = answers.filter(a => !a.answered).map(a => QUESTIONS[a.n - 1]);
Stage.note(`an investor's questions: ${answers.length - gaps.length} of ${QUESTIONS.length} answered by the deck; `
    + `not answered: ${gaps.join(' | ') || 'none'}`, { once: true });
```

What the answers are for:

- **They choose the scenes.** An answered question is a scene, or part of one (see *Scenes*); the four founder questions
  make the film's first half. An answer is the model's reading of the deck: anything it puts on screen passes the
  figure check and Stage 1b like every other claim.
- **An unanswered question is a gap, and the film never fills it.** Nothing goes on screen that the deck does not say,
  however obvious the answer. The gaps go to the director in **Q1**: for the company's own pitch they are what an
  investor will ask that the deck does not answer, and the founder can answer them in the brief or the deck before it
  is sent; for a retelling they are noted and left.
- **Some answers are warnings.** Calacanis names the weakest answers he hears: a founder doing it *to make money*, or
  *because a big company does not*; *we have no competition*; and no risks at all, which he reads as a founder unable
  to see their own business plainly (chs. 18–19). Where the deck gives one of these, the film still shows the deck's
  claim as the deck's, and Q1 says what an investor will make of it.
- **They go in `process.pdf`**, as *An investor's read*: the nine questions, each with its page and the deck's answer,
  or *not in the deck*. It is the part of the process book a founder will use most.

### Stage 1b — every label confirmed by the deck

**A figure's words are a claim too, and the page check cannot see them.** It finds the digits on the page, so it passed
*"50% — SF + NYC alone"* when the deck says the **seven** target cities (SF, NYC, LA, Chicago, Houston, PA, Dallas)
cover 50% of the US market. That is the error an investor catches first, and it is in the words, not the number.
So once the scenes are written, and before anything is designed, put **every figure with the exact words it will
carry on screen** back to the deck in one call:

```javascript
const LABELS = SCENES.flatMap(s => s.figures.map(f => ({ figure: f.shown, label: f.label })));   // as they will be set
const CONFIRM = await Documents.ask(DECK,
    'For each pair, say whether this deck supports the figure with exactly that meaning. Watch the scope: who or what it '
    + 'covers, where, when, and words such as "alone", "only", "all", "each", "per year". Quote the deck\'s sentence, and '
    + 'where the label says more or less than the deck, give the deck\'s own wording.\n'
    + LABELS.map((p, i) => `${i + 1}. ${p.figure} — ${p.label}`).join('\n'),
    { schema: { type: 'object', required: ['pairs'], properties: { pairs: { type: 'array', items: { type: 'object',
        required: ['n', 'supported', 'quote', 'deckWording'], properties: { n: { type: 'integer' }, supported: { type: 'boolean' },
            quote: { type: 'string' }, deckWording: { type: 'string' } } } } } } });
if (!CONFIRM.success) { error(CONFIRM.remedy); exit(CONFIRM.failureName); }
const unsupported = JSON.parse(CONFIRM.text).pairs.filter(p => !p.supported);
Stage.check('every figure\'s label is the deck\'s own claim', unsupported.length === 0,
    unsupported.map(p => `${LABELS[p.n - 1].figure} "${LABELS[p.n - 1].label}" -> deck: "${p.deckWording}"`).join('; ') || 'all confirmed');
```

- **An unsupported label is rewritten in the deck's wording**, then the whole list is asked again — a fix is a new
  claim. Never drop the check to make it pass.
- **Put the quotes in `direction.md`** beside each figure: the label, the deck's sentence, and the page.

### The rules of a pitch

- **Every number on screen is printed in the documents and found on its page, computed in the script from ones that
  were, or stated in the brief** (the raise, when the form gave it). Compute a share or a multiple in `motion.js`,
  show the working in `direction.md`, and **never take the model's arithmetic**.
- **A projection looks like a projection.** Actuals are solid; a projection, target or scenario is drawn open — an
  outlined bar, a dashed line continuing a solid one — **and labelled with its kind and its date in words on screen**:
  *"Projected, 2011"*, *"Target"*, *"Best case"*. A viewer must never be able to read a forecast as a result, with the
  sound off, from a single frame. Record it: `Stage.check('every forward-looking figure is marked as one', …)`.
- **Scenarios travel together.** Where the deck sets outcomes side by side — best, realistic, worst — show them side
  by side, each labelled as the deck labels it. The best case alone is a different claim from the deck's.
- **Scenarios in different units are cards, not bars.** "$1B+ revenue", "5% of five cities" and "10 cars" share no
  scale, so bars of them would measure nothing — and an empty outlined box beside each, as one film drew, is a mark
  that carries no value. Draw each scenario as a **card**: a panel with a dashed or outlined border (the open mark of a
  projection), its label, its figure set large inside, and the deck's one-line description under it. Bars are for
  figures in one unit on one scale.
- **The basis comes with the figure.** A projection the deck derives — *10.6M trips × $20 average fee* — shows its
  derivation, because an investor discounts a number with no assumptions behind it. Where the deck prints none, the
  figure stands as the company's, attributed.
- **The company's arithmetic stays the company's.** Where a derivation and its printed result disagree (10.6M × $20 is
  $212M; the deck says $200M), the screen shows **the figure the deck printed**, attributed, and the working stays
  off it. Record the difference in `direction.md` and put it to the director in Q1: correcting a founder's number on
  screen is a decision, not a fix.
- **The company's claims stay the company's.** *"Patent-pending"*, *"first to market"*, a market size from an outside
  estimate — each is the deck's claim, in its words. **Add nothing**: no market figure, competitor, trend, customer
  or team member the documents do not name, however well known. The film argues the case the documents make; it
  does not make a better one.
- **The ask is as stated.** The amount, the instrument and the use of funds come from the brief or the deck, in their
  words. Where neither gives an amount — *"raise a few million"* — the film says that, not a number.
- **The ask carries its basis.** An investor's first question of a valuation is how the company arrived at it, and the
  good answers are traction — revenue, users, growth (Calacanis, ch. 21). Where the deck states a valuation or a
  raise and what it rests on, the ask scene shows both, as a projection shows its derivation. Where it states no basis,
  the ask stands alone and Q1 says that an investor will ask for one.
- **The founders' reason is theirs, in their words.** Calacanis backs founders rather than ideas, and weighs most
  why *these* people are doing this (ch. 17–18). Where the deck says — an origin, a problem they lived — the founders'
  scene carries it, attributed; where it does not, the scene is names, roles and the advantage the deck claims, and
  nothing is supplied. **Never write a founder's motive the documents do not give**: an invented origin story is the
  most persuasive thing a pitch film could add, and the most false.
- **Risks as the deck states them.** Where the deck names reasons it might fail — regulation, supply, a dependency — they
  get a scene of their own, set as plainly as the rest: an investor trusts a founder who names their risks more than one
  who names none (ch. 19). Where it names none, there is no risk scene and the gap goes to Q1. Never add a risk the
  documents do not name.
- **Name how far along the company is.** Investors place a startup on a ladder before they read its numbers: an idea,
  a plan, mock-ups, a prototype, a minimum viable product, a beta, then users, then paying customers (Calacanis,
  ch. 21). The traction scene says which rung the deck shows, in the deck's terms, and its evidence; a company with
  revenue says revenue, never *traction* alone.
- **Market size honestly.** Total, serviceable and obtainable markets as the deck gives them, each with its kind
  (*estimate*). Nested circles are drawn **by area** (`Scale.radiusFor`): 10.6M inside 1.9B is a dot, and that is the
  point. A dot too small to see gets a label, never a larger radius.
- **People in type, never in pictures.** The team by name and role as the deck gives them; no faces, no photographs, no
  likeness of a real person, unless supplied in `documents/` and placed as given. The company's name in type is
  fine; its logo and marks are not unless supplied, and in a retelling not even then.
- **Honest charts** (Manual 13 is in front of you): bars start at zero; two figures compared share one scale; a
  single figure needs no chart — set it large. **Never red and green for up and down — and a loss is never red**:
  showing investors losses in red rather than black measurably cuts their risk-taking (Bazley, Cronqvist & Mormann
  2021, *Management Science* 67(9)), and which colour means a rise is a market's convention (red in mainland China:
  Zhang & Han 2014, *PLoS ONE* 9(2)). Direction is carried by sign and shape. Colour is kept for the answer, in the
  accent, and means one thing for the whole film.

### Scenes

The deck has more than the film can hold. **Choose by an investor's questions, in an investor's order** — Stage 1a's
answers say which the deck can carry — and drop a scene rather than rush one. The first half answers Calacanis's four
founder questions, as his meetings do (ch. 18); the second, his tactical ones (ch. 19):

1. **Title** (3–4 s): the company and one line of what it does — and, for a retelling, *"Based on <company>'s <year>
   pitch deck"*.
2. **The problem** — who has it and what it costs them, with the deck's figure for it.
3. **The solution** — what the company is building, shown as simply as the deck shows it. *What are you working on?*
4. **Why now**, if the deck says — what changed that makes this possible or needed now: a cost that fell, a device that
   spread, a rule that changed. Calacanis counts it among the most important questions of all, since most ideas have
   been tried before and win only when the market is ready (ch. 18).
5. **The founders**, if the deck names them — names and roles in type, the reason they give for doing this, and the
   advantage they claim over competitors and fast followers. *Why are you doing this?* and *What's your unfair
   advantage?*
6. **The market** — total, serviceable, obtainable, by area.
7. **Traction or progress** — the rung the company is on, and results so far, solid.
8. **The business model** — how it makes money, with its basis (fee × volume), and what it charges and what an average
   customer spends where the deck gives them.
9. **Competition**, if the deck has it — named as the deck names it.
10. **Risks**, if the deck names them — as plainly as the rest.
11. **The projections** — open marks, labelled, scenarios together, basis shown.
12. **The ask** — the amount, what it rests on, and what it buys, in the company's words. The film ends on it.
13. **End card** (4 s): the sources (the documents and the pages used), *"Projections are the company's estimates."*,
    and for a retelling *"A retelling for illustration. Not affiliated with <company>."*

**About ten seconds a scene**, a little more where a projection's basis must be read. Ninety seconds holds about seven
of these; two minutes about nine; three minutes all of them. **What to drop, when the length will not hold them all**:
the second half before the first, and within it competition, then the market; the four founder questions and the ask
stay while the deck answers them. Put the scene list, with its length per scene and what was dropped, in Q1.

**The length is the brief's.** Cut it only on a measurement, never on an estimate: encode one scene first, time it,
and multiply. A run once cut a two-minute brief to 87 seconds on an estimate of 65 minutes' encoding, then finished
with 37 minutes unused — the parts encode one per script, so the film's length costs scripts, not a single long wait.
If the measurement says it will not fit, say so in Q1 with the numbers.

**One scene per encoded part.** A two-minute film will not encode inside one script: encode each scene with
`comp.saveVideo('artifacts/part1.mp4', { from, to })` in a script of its own, the parts silent, then
`Motion.joinVideos([...], 'output.mp4', { audio })`, as stage 4 says. Plan the beats so every part boundary falls on
a cut.

### Narration

**A voice, unless the brief says none (`Narration: Disabled`).** A pitch is told; the screen carries the figures and the voice the argument.
Speak each scene's lines with `Assets.speak`, one line a call, in the language of the film's words, and time the
scene to the recording:

```javascript
const spoken = await Assets.speak('Cabs in most cities still run on radio dispatch.', { voice: 'Charon', path: 'audio/s2-1.wav' });
if (!spoken.success) error(spoken.remedy);
const line = Motion.speech(spoken.text, { audio: spoken.path, start: 1.0 });
if (line.unsaid.length) error('the recording says more than the line: ' + line.unsaid.join(' '));
```

- **The scene's length is its narration's length**, plus the holds: a figure is on screen before it is spoken and
  stays after. About 140 words a minute; a two-minute film is under 300 words. Never cut a line to fit a beat.
- **Say the figure on screen, as it is on screen.** A voice that says "two hundred million" over "$200M" is one claim;
  a voice that rounds differently, or names a figure the screen does not show, is two — and the second is unchecked.
- **The voice makes no claim the screen could not carry.** Every sentence is the deck's, attributed in a retelling.
- **The film must make its case with the sound off.** A narrated pitch is audio-dominant — the pictures illustrate the
  voice (Manual 25 §12e) — but investors often watch in a feed, muted. Every claim and figure the voice makes is on
  screen too, in fewer words; the voice adds the argument between them, never a fact of its own.
- **Let the voice lead the cut.** Start a scene's first line a beat before its picture cuts in, or let a last line run
  over the next scene's first frames — a J or an L cut (§12d) — rather than cutting voice and picture on one frame.
- The same line in the same voice is served from the cache for nothing. Give every line once, at the join.
- **A new line takes about 13 seconds to record**, so speak **at most two new lines a script**: eight in one script
  ran out of time with two written. Pass which lines to record in `ARGS` and run the script until all are cached.

### Visual style — produced, not plain

A pitch is judged in seconds by people who see a great many. **Exact is the floor, not the finish.** Decide the style
once, before the beats, and record it in `direction.md` with its reasons.

- **A ground with depth**, not a flat fill: a `comp.radialGradient` vignette under everything, and one slow move per
  scene with `comp.camera()`, eased `halt` at both ends, **never while a figure is being read**.
- **Light on the one thing that matters**: one glow a scene, on its figure, keyed up as it arrives and down to 0 after.
- **Every scene has a picture, made of the deck's own facts.** A scene of words alone is a slide, and the first pitch
  film was exactly that: headings, figures and empty space. Give each scene one diagram the viewer reads before the
  words — two figures in one unit as bars on one scale (a medallion's ~$500k against a driver's $31k), a share as a
  filled bar or ring, a market by area, steps as a timeline, places as labelled dots, a product as the few shapes of
  its flow (*tap → car assigned → pickup*). **Nothing in a picture that the deck does not say**: no invented icons
  of customers, no map detail, no logos.
- **Fill the frame.** Lay each scene on a two-column grid — the picture on one side, the figure and its words on the
  other — so no more than about a third of the frame is empty at the scene's key moment. Type that sits in the top
  left of an empty frame reads as unfinished.
- **Figures arrive whole.** A figure rises, fades or scales into place as the number it is — **never a counter that
  ticks up** and **no character cascade on a figure**: both put numbers on screen the documents never printed.
- **Type as the design.** One family; figures heavy, units smaller beside them; the claim in a calmer weight; the
  source line a footnote — about 22–24 px at 1080p, the muted ink at 4.5 : 1 or better, steady — naming the deck and its
  page (*"Pitch deck, p. 7"*), with `desc: 'source'` so `Verify.film` counts it as a citation, not grading it at a size
  the watcher misreads.
- **Cuts and wipes, not dissolves**: a dissolve lays one scene's figures over the next one's. No motion blur on text.
- **Carry a figure across a change of scene instead of setting it twice.** Where the next scene builds on a figure —
  the market size the business model then divides — keep it in a group outside both scenes and change the scene around
  it: a partial edit (Manual 25 §12c), which says *the same number* without a word.
- **The ask is the resolve.** Design the end on the ask first, and let the earlier scenes lead to it (§12b).
- **The design board before motion, the books at the end.** The case is the deck's, so there is no concept to choose;
  the look is yours. Draw the design board as the instructions say — a frame per scene, the figure scenes, the
  projections and the ask among them — with `treatment.md` and `dos.md`, and hold the film to the board in Stage 5.
  `pitch.pdf` is the case as a client reads it, in the order Shaw's pitch books lead a reader through an idea
  (*Design for Motion*, ch. 4): a cover naming the company as the client; the case in a sentence and the treatment
  beside its shape; the design board; a frame of each scene with its figure and the page it came from; the ask, as
  the resolve frame; what happens next; and *Thank you.* Set it in the film's typeface and accent on a neutral ground.
  `process.pdf` carries the scene list, the label confirmations and *An investor's read* (Stage 1a) — the evidence a
  client can check, kept out of the pitch's way.
- **Your palette, not the company's.** One accent and its tints on a neutral; contrast 4.5 : 1 for text. Do not rebuild
  the company's identity — its colour as the whole palette, its typeface, its devices. For a retelling this is the
  whole difference between a film *about* the deck and one passing as the company's.
- **Budget the frame**: time `comp.render(t)` on each scene's busiest moment before encoding.

### Layout — measured, not eyeballed

**Two texts must never touch.** The first pitch film set "22%" and "of all four biggest players" so close they read as
one word, and "35%" ran into its label the same way: the figure's width depends on its digits, so a label placed at
a fixed x collides whenever the figure is wider than planned. **Place a label from the figure's measured box**, not a
guess — `boundsAt` gives it — and check every scene at its key moment before encoding:

```javascript
// TEXTS: every comp.text handle by name, kept as each is made. KEY: each scene's key moment, in seconds.
const GAP = 24;   // px at 1080p: a word space at the label's size, at least
const clashes = [];
for (const [scene, t] of Object.entries(KEY)) {
    const boxes = Object.entries(TEXTS).map(([name, h]) => [name, h.boundsAt(t, { frame: true })])
        .filter(([, b]) => b && b.width > 0 && b.height > 0);
    for (let i = 0; i < boxes.length; i++) for (let j = i + 1; j < boxes.length; j++) {
        const [[na, a], [nb, b]] = [boxes[i], boxes[j]];
        const apart = a.x + a.width + GAP <= b.x || b.x + b.width + GAP <= a.x || a.y + a.height + GAP / 2 <= b.y || b.y + b.height + GAP / 2 <= a.y;
        if (!apart) clashes.push(`${scene}: ${na} / ${nb}`);
    }
    for (const [name, b] of boxes)
        if (b.x < 0.05 * W || b.y < 0.05 * H || b.x + b.width > 0.95 * W || b.y + b.height > 0.95 * H) clashes.push(`${scene}: ${name} outside title-safe`);
}
Stage.check('no two texts touch and every text is title-safe at each key moment', clashes.length === 0, clashes.join('; ') || 'clear');
```

- **A check that fails is fixed in the layout, never by loosening the check.** One run "fixed" a title-safe check to
  pass a film whose text was off the frame; one rendered still would have shown it. **When a check and a still
  disagree, render the still** (`comp.sheet` at the key moments, or `comp.render(t)`) and believe it.
- **`offset` is where a group's `origin` lands**, not a displacement: a scene group with `origin: [W/2, H/2]` rests
  at `offset: [W/2, H/2]`. Resting it at `[0, 0]` moved every scene of one film off the top left of the frame.

### More than one audience

The instructions' *More than one audience* holds: every profile first, compared, `Taste.contrast` for a pair, one
`build(audience)` and the films `artifacts/final_a.mp4`, `final_b.mp4`… What a pitch adds is the rule that keeps a
set honest: **the case is shared and only its telling is the audience's.**

- **One reading, every film.** The same scenes, the same figures, the same projections with the same marks, the same
  ask. A film that drops a risk, a scenario or a figure for its audience makes a different claim to different
  investors — the one thing a pitch must never do.
- **Figures written as the audience writes them, in the language of the film's words.** `Locale.number(value, {
  locale, compact: 'long' })` gives `20 crore` (`en-IN`), `2億` (`ja-JP`), `200 Millionen` (`de-DE`) for 200 million;
  with `currency: 'USD'` the currency's own compact form, `$20Cr`, `$2億`, `200 Mio. $`, whatever the length. Where the
  long word reads better, keep `compact: 'long'` and set the currency in the words around it. Choose the locale from
  the place **and the film's language**: an English film for Tokyo writes `$200M`. `Locale.locales` lists what is
  carried.
- **Every figure in every text, headings and narration included.** A London film that set `US$4.2bn` under the heading
  *"A $4.2B market"* wrote one figure two ways in one frame. Build each heading and line from the same `Locale.number`
  call as its figure, never type a figure into a sentence by hand.
- **Never convert a currency.** A dollar raise stays in dollars in every film: a conversion is a figure with a rate and
  a date the documents do not give.
- **Tell the read-back how each written figure was written**: `{ computed: { '$2億': '200,000,000 / 100,000,000 = 2
  (an oku is a hundred million; the deck prints $200M)' } }`.
- **Pace, grade and voice are each film's own**, from its profile; the holds never drop below the reading time a figure
  needs.

### Pace

**With no place in the brief, there is no Qloo profile to read**: pace from Manual 25 and the reading time, and mark it
*assumed* in `direction.md`. **With a place and an age band**, profile the audience for pace and grade exactly as the
instructions above say. Investors read a figure slower than a word: hold a number at least a second longer than its
words would need.

### The read-back — after delivery

**`Verify.film(comp, 'output.mp4', { computed })`** is the instructions' read-back. **Every figure on screen must come
back with a source**: for `document`, its digits on a page of the deck or the model; for `computed`, each derived
value with its working, exactly as set; a figure from the brief goes in `computed` too, its working *"stated in the
brief"*. An `unsourced figure` comes off the screen. A `'different figure'` stray is a line you set, read with another
number: look at its frame before deciding. The check records its own verdict; do not write one for it.

### Questions, for a pitch

- **Q1** (after the reading, before any design): the scene list with its length per scene; each scene's claim and
  figures, each marked found, computed, from the brief, from an image, or left out, **with its kind and its label's
  confirmation from Stage 1b**; every
  disagreement between a derivation and its printed result, and between the deck and the model; and the sections
  dropped for length. **Then an investor's read** (Stage 1a): the questions the deck does not answer, and any answer an
  investor will take as a warning, each with what they will make of it. The director confirms the case before anything
  moves, and may answer a gap in the brief; an answer given there is the brief's, marked as such, never the deck's.
- **Q2** the tone, pace and voice.
- **Q3** and **Q4** as the instructions say: the design board, then the sheet.

### direction.md

The documents (file, what each is, its date, pages), **who read them** (`provenance.model`, `route`, any fallback),
how the film is told and why, **an investor's read** — the nine questions as a `Question | Answered | Page | The deck's
answer` table, gaps and warnings included — then **one section per scene**: the claim, every figure with its kind, page and the quote
it was found in, every computed value with its working, every figure from the brief, anything left out and why. Then
the shared system — type, palette, the marks for actual and projected — with its reasons. The read-back's result goes
last.

**The audience gets a section of its own, before the scenes**: who is watching and where, as the brief has it, then
one row per choice made for them in the `Choice | Evidence | Kind` table the instructions describe — **Brief**,
**Qloo** (pace and grade from a profile), **Research** (Bazley et al., Zhang & Han; Calacanis, *Angel*, for the
scene order and the investor's questions), **Rule** (this type's: projections
marked, scenarios together, figures in the audience's conventions with Unicode CLDR as the evidence), and **Assumed**.
A choice the audience did not change says so. **With several audiences, one such section per audience**, then the
instructions' *Between the audiences*, which for a pitch says first what every film shares: the scenes, the figures,
the projections and the ask.
