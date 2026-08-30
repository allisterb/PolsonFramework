Stage.begin('Forms');

Stage.note('FORM 1: Continuous Vertical Timeline — Answers "Over time / When did breakthroughs happen and what did the temporal spacing look like?". Rejected uniform-spaced list because equal spacing lies about the 20-year and 15-year quiet periods.');
Stage.note('FORM 2: Big-Number Callout Badge — Answers "How big? / What is the total span of the internet revolution (53 Years)?". Rejected gauge dials with needles (explicit anti-pattern in Manual 13 §1).');
Stage.note('FORM 3: Ranked Horizontal Bar Chart — Answers "Compared to what? / How do the interval gaps compare against each other (20y, 15y, 6y, 5y, 4y, 3y)?". Rejected radar charts (anti-pattern) and donut charts (intervals are durations, not parts of a single closed pie).');
Stage.note('FORM 4: Segmented Metric Strip / Era Breakdown — Answers "What share? / How much time was spent waiting in incubation vs rapid-fire delivery?". Segmented bar with heavy neo-brutalist dividers chosen over donut (Manual neo-brutalist notes: segmented bar beats donut).');

log('Form selection complete: 4 distinct forms mapped to explicit analytical questions.');

// Render a form selection diagram
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f2';
ctx.fillRect(0, 0, 1080, 1920);

// Header block
ctx.fillStyle = '#000000';
ctx.fillRect(40, 40, 1000, 120);
ctx.fillStyle = '#ffffff';
ctx.font = '900 36px monospace';
ctx.fillText('STAGE 2: FORM SELECTION MATRIX', 70, 110);

const forms = [
  { q: 'Over time? (Chronology & Spacing)', form: 'Continuous Vertical Timeline', why: 'True temporal scale vs deceptive equal steps' },
  { q: 'How big? (Total Scope)', form: 'Big-Number Callout Badge (53 YRS)', why: 'Blunt single-metric impact at canvas head' },
  { q: 'Compared to what? (Gap Durations)', form: 'Ranked Horizontal Bar Chart', why: 'Exact linear length comparison (zero-based)' },
  { q: 'What share? (Incubation vs Boom)', form: 'Segmented Heavy-Border Bar', why: 'Neo-brutalist part-to-whole without soft arcs' }
];

let y = 220;
for (const f of forms) {
  // Shadow
  ctx.fillStyle = '#000000';
  ctx.fillRect(66, y + 6, 940, 200);
  // Box
  ctx.fillStyle = '#ffde59';
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 4;
  ctx.fillRect(60, y, 940, 200);
  ctx.strokeRect(60, y, 940, 200);

  ctx.fillStyle = '#000000';
  ctx.font = '900 24px monospace';
  ctx.fillText('QUESTION: ' + f.q, 90, y + 50);
  ctx.font = '700 28px sans-serif';
  ctx.fillText('FORM: ' + f.form, 90, y + 105);
  ctx.font = '500 20px monospace';
  ctx.fillText('RATIONALE: ' + f.why, 90, y + 155);

  y += 260;
}

canvas;
