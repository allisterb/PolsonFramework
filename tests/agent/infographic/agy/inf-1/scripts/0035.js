Stage.begin('Forms');

Stage.note('Form 1 (Over time): Scaled Vertical Timeline Spine (1969-2022). Maps 53 years down the continuous fanfold page to faithfully show the temporal clusters vs vast deserts.');
Stage.note('Form 2 (What happened): Neo-Brutalist Event Badges & Punched Cards with high-contrast borders, offset leader connectors, and punchy humorous commentary.');
Stage.note('Form 3 (Compared to what): Horizontal Gap Comparison Bars with strictly asserted zero-baseline Scale.linear(0, 20, ...). Compares the 6 inter-milestone gaps (20y, 4y, 5y, 6y, 3y, 15y).');
Stage.note('Form 4 (How big): Big-Number Callouts (53 Years total span, 100M users / 2 mos velocity).');

Stage.note('Rejected Forms: 1. Radar chart rejected per Manual 13 anti-pattern; 2. Pie chart rejected because gaps are distinct intervals, not part-to-whole; 3. Uniform vertical step list rejected because unscaled spacing lies about time.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Neo-brutalist styling for form selection overview
ctx.fillStyle = '#FAF8F0';
ctx.fillRect(0, 0, 1080, 1920);

// Platen header
ctx.fillStyle = '#111111';
ctx.fillRect(40, 40, 1000, 120);
ctx.fillStyle = '#FFE600';
ctx.font = '900 32px monospace';
ctx.fillText('STAGE 2: FORM SELECTION MATRIX', 70, 95);
ctx.fillStyle = '#FFFFFF';
ctx.font = '500 18px monospace';
ctx.fillText('MANUAL 13 §1 COMPLIANCE: 4 DISTINCT FORMS CHOSEN BY QUESTION', 70, 135);

const forms = [
  {
    q: 'OVER TIME? (When & how far apart)',
    chosen: 'Continuous Vertical Timeline Spine',
    why: 'Faithfully encodes 53 years on linear y-scale. Reveals the 20-year desert (1969-1989) and 1993-2007 explosion.',
    rejected: 'Equal-step list (lies about duration), Horizontal line (too cramped for 9:16 portrait).',
    color: '#00E5FF'
  },
  {
    q: 'WHAT HAPPENED? (Events & Milestones)',
    chosen: 'Pinned Neo-Brutalist Event Badges',
    why: 'High-contrast diegetic paper cards attached to exact scale positions with leader lines and status tags.',
    rejected: 'Tooltips (static medium), plain text dump (zero hierarchy).',
    color: '#FFE600'
  },
  {
    q: 'COMPARED TO WHAT? (Gap durations)',
    chosen: 'Horizontal Gap Comparison Bar Chart',
    why: 'Strictly zero-based Scale.linear(0, 20). Directly compares 20y vs 4y vs 5y vs 6y vs 3y vs 15y.',
    rejected: 'Radar/spider chart (Manual 13 anti-pattern), Pie chart (gaps are not shares of a fixed whole).',
    color: '#FF5500'
  },
  {
    q: 'HOW BIG? (Hero milestones & velocity)',
    chosen: 'Big-Number Hero Metric Callouts',
    why: '53 YEARS total span and 100M Users / 2 Mo explosion for instant cognitive anchor.',
    rejected: 'Gauge meter with dial (Manual 13 anti-pattern), Multi-callout shouting wall.',
    color: '#00FF66'
  }
];

let yPos = 190;
for (let i = 0; i < forms.length; i++) {
  const f = forms[i];
  ctx.fillStyle = f.color;
  ctx.fillRect(40, yPos, 1000, 390);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 5;
  ctx.strokeRect(40, yPos, 1000, 390);
  
  // Hard shadow
  ctx.fillStyle = '#111111';
  ctx.fillRect(40 + 8, yPos + 390, 1000, 8);
  ctx.fillRect(40 + 1000, yPos + 8, 8, 390);

  ctx.fillStyle = '#111111';
  ctx.font = '900 24px monospace';
  ctx.fillText('QUESTION: ' + f.q, 70, yPos + 50);

  ctx.fillStyle = '#FFFFFF';
  ctx.fillRect(70, yPos + 75, 940, 60);
  ctx.strokeRect(70, yPos + 75, 940, 60);
  ctx.fillStyle = '#111111';
  ctx.font = '900 24px sans-serif';
  ctx.fillText('FORM: ' + f.chosen, 90, yPos + 115);

  ctx.fillStyle = '#111111';
  ctx.font = '700 20px sans-serif';
  ctx.fillText('RATIONALE:', 70, yPos + 175);
  ctx.font = '500 20px sans-serif';
  ctx.fillWrappedText(f.why, 70, yPos + 205, 940, 28);

  ctx.fillStyle = '#990000';
  ctx.font = '700 20px sans-serif';
  ctx.fillText('REJECTED ALTERNATIVES & WHY:', 70, yPos + 295);
  ctx.font = '500 19px sans-serif';
  ctx.fillStyle = '#333333';
  ctx.fillWrappedText(f.rejected, 70, yPos + 325, 940, 26);

  yPos += 425;
}

canvas;
