## What this workflow tests

`motion` is the motion-graphics route through the animation model: shapes and groups keyed with eases,
**text with animators** (`comp.text`), **blur** (`group.blur`), the camera and its planes, write-ons, wipes,
the grade, cuts, contact sheets as the agent's only view of the film, and delivery as `comp.saveVideo`.
Reach for `polson://sdk/core/Motion` (*Layers*, *Type*, *Nodes*, *Video with sound*) and `polson://manual/25`.

Answer these in `findings.md`:

- **The audience in the motion.** Quote `pace()`'s counts and the titles behind them, and the grade's
  references (result id and item). If you compared audiences, quote the comparison's summary
  (`comparePace`, or `comparePaceAll` for more than two). Did the evidence give the films a reason to
  move differently, and did yours follow it — or did it show no measurable difference, and where did
  you put the difference instead? If the profile had nothing to say
  about a motion choice, which one, and what did you rest it on?
- **More than one audience** (when the brief named several). Did you fetch every profile and compare them
  before planning any film? Was each later film's motion drafted from its own evidence before you looked back
  at the earlier ones? Name what the set shares and what differs, and whether each differing pair's measured
  holds differ in the direction the comparison gave — or why they did not need to.
- **A product ad** (type `product`). What did each comparable resolve to, and did `demographics` fit the audiences?
  How many of each audience's buyers were local-only against the buyers anywhere, and did that list give you
  anything to design from? Were the brands' design tags the same across audiences — and if so, where did you put the
  difference? Did the places come back in the right city, and which mood tags did you use? Quote the claim as it
  appears on screen, and name anything you wanted to say about the product that the brief did not give you. Qloo
  requests spent, from `status`.
- **The key messages** (product ad). Quote the descriptor as it appears, and when it first appears. Quote the cold
  viewing's answer for each film word for word. Did any film fail it at first, and what did you change?
- **The packshot** (product ad without a supplied shot). Was it one generation, and was it the same can in every
  view? Did lettering appear on it anyway? Did your label read on the band, and on the three-quarter view? If a
  product shot was supplied, what did `Documents.ask` tell you about it, and what did you take from it?
- **A report explainer** (type `report`). Who read the document (`provenance.model`, `route`, pages) and how long
  the one call took; was there a fallback, and why? How many figures did the reading return, how many were `found`
  on their pages, how many did you compute, and how many did you leave out — quote each one left out and what the
  narrower question then found. Did any `unverified` figure come back, and was the misreading the model's or the
  page's (a figure split across lines, a footnote mark)? Which question did the document not answer, and what did
  that scene say? Quote the read-back: every number `Video.ask` read, and any it read that you did not set or missed.
- **Text animators.** Did the *Type* section give you what you needed to build a cascade? Quote the
  animator you used for the title, and say whether it did what you expected the first time. Name any
  property or selector option you wanted and did not find.
- **Camera, write-on, wipe, grade.** For each you used: what for, whether it did what you expected the
  first time, and what it cost per frame. For each you did not: why not.
- **Blur.** Did you use it, for what, and was its cost on the render time noticeable?
- **The sheets as your eyes.** Were the design board and the half-second sheet enough to judge the motion?
  Name anything about the film you could not judge from them.
- **The checks.** Which failed, what did you change, and was the change in the keys, the eases or the
  beats?
- **Delivery.** Did `comp.saveVideo` finish inside the script's time limit? How long did it take, and
  were its `warnings` empty? Is `motion.js` runnable on its own — which mode would an export use, and
  does anything on that path need the session?
- **From the poster.** What carried over unchanged, what did the motion force you to change, and why?
- **Where it ran out.** What did the brief need that the model could not give, and what did you do instead?
