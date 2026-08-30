// STAGE 2 - recording the form decisions taken from the 02_form_test render.
Stage.begin('Forms');

Stage.note('FORM A - VERTICAL TIMELINE, LINEAR YEAR AXIS. Question: "over time / follow" (Manual 13 s1). ' +
    'This is the spine. Critically it answers a SECOND question for free - "what share?" - because on a ' +
    'linear axis the 20-year silence occupies 20/53 = 37.7% of the rail as drawn ink. That is why no ' +
    'separate part-to-whole form is needed. s2 note: a timeline encodes POSITION, not length, so an ' +
    'origin of 1969 rather than year zero is permitted; the rule it must obey instead is linearity in ' +
    'years, which is asserted rather than intended.');

Stage.note('FORM C - RANKED HORIZONTAL BARS of the six intervals, sorted by length, zero-based ' +
    '(scale.isZeroBased asserted in script 02). Question: "compared to what - which wait was longest?" ' +
    'This is NOT a restatement of A. Re-sorting drops time order, which is precisely the thing the ' +
    'timeline cannot do, and it separates 20 and 15 from the 3/4/5/6 pack at a glance. If it were drawn ' +
    'in chronological order it WOULD be A rotated, and would be cut.');

Stage.note('FORM D - BIG-NUMBER CALLOUT. Question: "how big?" - one value, and s1 is explicit that one ' +
    'value is not a chart. Carries the only non-year figure in the brief (100M users in 2 months, ' +
    '= 50M/month) and possibly a second callout for the 47-of-54 quiet-years figure. Read well in the ' +
    'test at 112px Impact.');

Stage.note('BADGES (requested in the brief) are the LABELLING SYSTEM, not a data form. Being honest ' +
    'about that matters: a badge per milestone carries identity and the year stamp, but it answers no ' +
    'question from s1 and must not be counted toward the three-form minimum. Three genuine data forms ' +
    'above; badges are the fourth element and are chrome.');

Stage.note('REJECTED - WAFFLE GRID, 1 cell = 1 calendar year (54 cells, 9x6). Rendered in 02_form_test ' +
    'and cut on the evidence. Layout.grid is row-major, so the seven marked cells scatter across the ' +
    'field in an order the reader has no way of knowing is chronological. Cell position LOOKS like it ' +
    'encodes something and encodes nothing - a worse failure than redundancy, because it invites a ' +
    'reading that is not there. A single row of 54 cells would fix the order but would then just be ' +
    'form A rotated. The insight it was carrying (47 of 54 years had no milestone) survives as a ' +
    'big-number callout instead.');

Stage.note('REJECTED - SEGMENTED BAR of the 53-year span divided by interval. Endorsed by the design ' +
    'language over a donut, and it would answer "what share?" - but in time order it is form A rotated ' +
    'through 90 degrees with the same six lengths. Cutting it rather than letting the piece become ' +
    'three bar charts, which s1 calls a design failure rather than a house style. Share is already ' +
    'carried by A\'s linear axis.');

Stage.note('REJECTED - DONUT / PIE of interval share. Two reasons, either sufficient: the design ' +
    'language has no soft forms and circles read as imported from another piece; and s1 wants a donut ' +
    'to have one dominant share and <=5 segments, where this has 6 and no dominant one.');

Stage.note('REJECTED - PROPORTIONAL CIRCLES per interval. The language forbids circles, and area ' +
    'encoding invites the radius error that s2 exists to prevent. Nothing gained over bars.');

Stage.note('REJECTED - LINE OR AREA CHART OF INTERNET GROWTH. The most tempting form for this topic ' +
    'and the one that would break non-negotiable 1. There is NO growth series in brief.md - no host ' +
    'counts, no user curves, no traffic volume. Drawing a swooping adoption curve would mean inventing ' +
    'every point on it. Recorded here because a reader of this run should see it was considered and ' +
    'refused, not simply never thought of.');

Stage.note('REJECTED - LOGARITHMIC TIME AXIS. Would compress the 20-year silence and make the ' +
    'milestones look evenly paced - i.e. it would draw the "progress is continuous" claim the Data ' +
    'stage corrected. The distortion would be invisible in the finished piece, which is exactly why ' +
    'it is refused in writing.');

Stage.note('REJECTED - SMALL MULTIPLES and SLOPE CHART. Neither has input: one series, one date per ' +
    'milestone, nothing to compare across panels or between two time points.');

Stage.note('FORM COUNT: three genuine data forms (timeline / ranked bars / big number), which meets the ' +
    'three-form minimum and sits at the bottom of s1\'s "mix 3-5". Not padding to five. The data is ' +
    'seven dates and one figure; a fourth form would either restate one of the three or need numbers ' +
    'the brief does not contain.');

Stage.note('DEFECTS FOUND IN THE TEST RENDER, to fix in Composition/Encode: (1) gap labels rotated ' +
    'into 26px ribbons are illegible at 3-6 years - the short gaps need their labels outside the ribbon ' +
    'or dropped in favour of the ranked bars carrying those figures; (2) the 1989-2007 milestone labels ' +
    'nearly collide at this scale - the cluster needs either more canvas height or a lateral stagger.');

null;
