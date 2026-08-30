Stage.begin('Forms');

// FORM SELECTION — one form per question, at least 3 genuinely different forms.
//
// The claim: progress is constant but never on schedule.
// The structural insight: 53 years, but 35 of them (66%) were silence between two bursts of activity.
// The reader has never seen the GAPS drawn to scale.
//
// Questions the data answers:
//
// Q1. When did each event happen? (locate in time)
//    -> TRUE-SCALE TIMELINE  (vertical, 1:1 linear)
//    Rejected: step flow — implies causation rather than locating events in time
//    Rejected: icon parade — doesn't show the gaps, which IS the story
//
// Q2. How long was each gap? (compare durations)
//    -> HORIZONTAL BAR CHART — ranked by gap length
//    Rejected: circles — no soft forms in neo-brutalist; area encoding of years is harder to read
//    Rejected: waffle — 1 block per year would work but is better for share, not comparison
//
// Q3. What share of 53 years was waiting vs. activity? (what share?)
//    -> SEGMENTED SINGLE BAR — 3 eras: Lull / Burst / Lull
//    (53 years, 3 segments. Waffle would need 53 cells — readable but not strong enough here)
//    Rejected: donut — neo-brutalist language has no soft forms
//
// Q4. How fast was ChatGPT's adoption vs. the 20yr wait for the web? (how big? one big callout)
//    -> BIG-NUMBER CALLOUT — '20 YRS → 2 MOS'
//    This is the punchline of the claim. Needs its own moment.
//
// That is 4 genuinely different forms:
//   1. True-scale vertical timeline (locate)
//   2. Horizontal bar chart (compare gaps)
//   3. Segmented bar (share of time)
//   4. Big-number callout (how big)
//
// COMPOSITION — the PLACE is a line printer / fanfold paper.
// Pattern chosen: EDITORIAL SPREAD (§4, pattern 5)
//   Hard asymmetric split: left column = tall true-scale timeline (the fanfold 'feed'),
//   right column = supplementary panels stacked.
//   Ratio: [42, 58] — timeline column is narrower, dense with events;
//   right side has air between panels.
//   TENSION RULES (§5):
//   1. Dense zone: timeline column (events packed, year ticks continuous)
//      Empty zone: gap annotations — blank stretches of the timeline ARE the design
//   2. Three sizes: giant gap-year numbers (~80px), milestone labels (~18px), body copy (~11px)
//   3. Something crosses: the 20-yr gap bracket bleeds across the column divider
//   4. Ground: tractor-feed hole strip on left and right margins (fanfold paper)
//      Monospace font for labels = teletype aesthetic
//   5. One rotation system: none — strict verticals only (correct for neo-brutalist)
//
// REJECTED compositions:
//   Big-Object: the internet has no single drawable icon
//   Bleed: works for one massive value, but we have 7 events across 53 years
//   Overlap stack: too chaotic for a timeline that must be readable in sequence

Stage.note('FORMS: True-scale vertical timeline | Horizontal ranked bar | Segmented era bar | Big-number callout. Four genuinely different forms, chosen by question.');
Stage.note('COMPOSITION: Editorial Spread (§4 pattern 5). Left column: fanfold timeline (dense). Right: supplementary panels. Ratio ~40:60. Tractor-feed holes frame the piece — the ground IS fanfold paper.');
Stage.note('TENSION §5: dense left / breathing right; 3 size tiers; 20yr gap bracket crosses the column divider; ground = tractor feed holes + monospace aesthetic; no rotations.');
log('Forms selected. Ready for Stage 3: Composition.');
