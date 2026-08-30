Stage.begin('Data');
Stage.note('FRESH RESTART. Clearing prior run state. Beginning Stage 1: Data & Claim.');

// CLAIM:  Progress is constant — it never stopped coming, but it never once came on schedule.
// READER: General audience. Knows all seven events; has never seen the gaps drawn to scale.
// PLACE:  At a line printer, watching fanfold paper feed through the platen.

// Data table — every figure on the canvas must trace to this.
const milestones = [
  { year: 1969, label: 'ARPANET', desc: 'First message — crashed after "LO"' },
  { year: 1989, label: 'World Wide Web', desc: 'Tim Berners-Lee proposes HTTP/HTML' },
  { year: 1993, label: 'Mosaic', desc: 'Makes the web visual' },
  { year: 1998, label: 'Google', desc: 'Founded — PageRank indexes the web' },
  { year: 2004, label: 'Facebook', desc: 'Social era begins' },
  { year: 2007, label: 'iPhone', desc: 'Web in every pocket' },
  { year: 2022, label: 'ChatGPT', desc: '100M users in 2 months' },
];

// Derived figures — all arithmetic shown
const gaps = [
  { from: 'ARPANET', to: 'Web',      years: 1989 - 1969 }, // 20
  { from: 'Web',     to: 'Mosaic',   years: 1993 - 1989 }, // 4
  { from: 'Mosaic',  to: 'Google',   years: 1998 - 1993 }, // 5
  { from: 'Google',  to: 'Facebook', years: 2004 - 1998 }, // 6
  { from: 'Facebook',to: 'iPhone',   years: 2007 - 2004 }, // 3
  { from: 'iPhone',  to: 'ChatGPT',  years: 2022 - 2007 }, // 15
];

const span = 2022 - 1969; // 53
const waitYears = gaps[0].years + gaps[5].years; // 20 + 15 = 35
const waitPct = (waitYears / span * 100).toFixed(1); // 66.0%

log('Milestones:', milestones.length);
log('Total span:', span, 'years');
log('Waiting years (2 big lulls):', waitYears, '=', waitPct + '%');
log('Gaps:', gaps.map(g => g.from + '->' + g.to + ':' + g.years + 'y').join(', '));

Stage.note('CLAIM: Progress is constant — it never stopped coming, but it never once came on schedule.');
Stage.note('READER: General. Knows all 7 events; has never seen the gaps drawn to scale.');
Stage.note('PLACE: At a line printer, watching fanfold paper feed through the platen. Composition is a continuous feed — the gaps are literal whitespace on the roll, the milestones are stamps on the paper.');
Stage.note('Data verified: 7 milestones, 6 gaps, 53-year span, two long lulls totalling 35y (66%). All figures from brief.md.');
