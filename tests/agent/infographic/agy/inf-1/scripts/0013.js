Stage.begin('Forms');

Stage.note('Form 1: Continuous Linear Scale Vertical Timeline Spine — answers Over time / Progression over time. True year-to-pixel ratio.');
Stage.note('Rejected Form 1: Discrete step timeline — rejected because equal step spacing hides the 20-year and 15-year gaps, destroying the core argument.');

Stage.note('Form 2: Scaled Horizontal Gap Comparison Bars — answers Compared to what / How long were the silent gaps. Zero-based linear scale.');
Stage.note('Rejected Form 2: Pie/Donut chart — rejected because sequential waiting times are comparative durations, not parts of a closed circular budget.');

Stage.note('Form 3: Neo-brutalist High-Impact Metric Badges — answers How big / Key summary magnitudes (53-year total span, 14-year burst cluster).');
Stage.note('Rejected Form 3: Plain bulleted text — rejected because brutalist poster energy requires punchy stamped callouts.');

Stage.note('Form 4: Proportional Area Nodes & Connectors — answers Area/Magnitude using Scale.radiusFor square-root scaling.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Test drawing the forms
ctx.fillStyle = '#f4f0ea';
ctx.fillRect(0, 0, 1080, 1920);

// Header box
ctx.fillStyle = '#000000';
ctx.fillRect(50, 60, 980, 120);
ctx.fillStyle = '#faff00';
ctx.font = '700 36px "Consolas", monospace';
ctx.fillText('STAGE 2: FORM SELECTION MATRIX', 80, 130);

// Render form prototypes
const forms = [
  {
    name: '1. Vertical Continuous Timeline',
    q: 'Q: Over time? When did milestones land?',
    desc: 'Linear scale y(year) from 1969 to 2022. Shows genuine clusters & voids.'
  },
  {
    name: '2. Horizontal Gap Duration Bars',
    q: 'Q: Compared to what? How long was each wait?',
    desc: 'Zero-baseline horizontal bars for 20y, 4y, 5y, 6y, 3y, 15y.'
  },
  {
    name: '3. Neo-Brutalist Big-Number Badges',
    q: 'Q: How big? Key totals and burst ratios.',
    desc: 'Stark offset-box badges for 53-year span and 14-year 5-milestone cluster.'
  },
  {
    name: '4. Proportional Area Step Nodes',
    q: 'Q: What magnitude? Visual node weight.',
    desc: 'Scale.radiusFor area-encoded nodes marking milestone transitions.'
  }
];

let yOffset = 230;
for (const f of forms) {
  // Shadow box
  ctx.fillStyle = '#000000';
  ctx.fillRect(74, yOffset + 14, 940, 200);
  
  // Foreground card
  ctx.fillStyle = '#ffffff';
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 4;
  ctx.fillRect(70, yOffset + 10, 940, 200);
  ctx.strokeRect(70, yOffset + 10, 940, 200);
  
  ctx.fillStyle = '#000000';
  ctx.font = '700 28px "Consolas", monospace';
  ctx.fillText(f.name, 100, yOffset + 60);
  
  ctx.fillStyle = '#ff0055';
  ctx.font = '700 20px "Consolas", monospace';
  ctx.fillText(f.q, 100, yOffset + 105);
  
  ctx.fillStyle = '#333333';
  ctx.font = '400 20px "Consolas", monospace';
  ctx.fillText(f.desc, 100, yOffset + 155);
  
  yOffset += 240;
}

canvas;
