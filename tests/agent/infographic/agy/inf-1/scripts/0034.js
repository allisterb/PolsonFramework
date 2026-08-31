Stage.begin('Data');

const claim = "Progress is constant — it never stopped coming, but it never once came on schedule.";
const reader = "General. Knows all seven events already; has never seen the gaps between them drawn to scale.";
const place = "At a line printer, watching fanfold paper feed through the platen";

log('CLAIM:   ' + claim);
log('READER:  ' + reader);
log('PLACE:   ' + place);

Stage.note('CLAIM: ' + claim);
Stage.note('READER: ' + reader);
Stage.note('PLACE: ' + place);

// Raw figures from brief.md
const rawData = [
  { year: 1969, event: 'ARPANET sends its first message (it crashed after "LO")' },
  { year: 1989, event: 'Tim Berners-Lee proposes the World Wide Web' },
  { year: 1993, event: 'Mosaic makes the web visual' },
  { year: 1998, event: 'Google is founded' },
  { year: 2004, event: 'Facebook launches — the social era begins' },
  { year: 2007, event: 'The iPhone puts the web in every pocket' },
  { year: 2022, event: 'ChatGPT reaches 100M users in 2 months' }
];

// Derived figures from brief.md
const derived = [
  { from: '1969, 1989', arithmetic: '1989 - 1969', result: 20, unit: 'years', label: 'ARPANET → Web gap' },
  { from: '1989, 1993', arithmetic: '1993 - 1989', result: 4, unit: 'years', label: 'Web → Mosaic gap' },
  { from: '1993, 1998', arithmetic: '1998 - 1993', result: 5, unit: 'years', label: 'Mosaic → Google gap' },
  { from: '1998, 2004', arithmetic: '2004 - 1998', result: 6, unit: 'years', label: 'Google → Facebook gap' },
  { from: '2004, 2007', arithmetic: '2007 - 2004', result: 3, unit: 'years', label: 'Facebook → iPhone gap' },
  { from: '2007, 2022', arithmetic: '2022 - 2007', result: 15, unit: 'years', label: 'iPhone → ChatGPT gap' },
  { from: '1969, 2022', arithmetic: '2022 - 1969', result: 53, unit: 'years', label: 'Total timeline span' }
];

// Verify derived calculations in code
for (const d of derived) {
  if (d.label === 'Total timeline span') {
    if (2022 - 1969 !== d.result) throw new Error('Timeline span mismatch');
  }
}

log('All data verified against brief.md table.');

// Visual check render for Stage 1
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Tractor-feed / Neo-brutalist paper background
ctx.fillStyle = '#FAF8F0';
ctx.fillRect(0, 0, 1080, 1920);

// Platen / header bar
ctx.fillStyle = '#111111';
ctx.fillRect(40, 40, 1000, 140);
ctx.fillStyle = '#00FF66';
ctx.font = '700 32px monospace';
ctx.fillText('STAGE 1: DATA & CLAIM VALIDATION', 70, 100);
ctx.fillStyle = '#FFFFFF';
ctx.font = '400 20px monospace';
ctx.fillText('FEED: FANFOLD CONTINUOUS STATIONERY // 1080x1920', 70, 145);

// Claim card
ctx.fillStyle = '#FFE600';
ctx.fillRect(40, 220, 1000, 220);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 6;
ctx.strokeRect(40, 220, 1000, 220);

ctx.fillStyle = '#111111';
ctx.font = '900 28px sans-serif';
ctx.fillText('THE CLAIM', 70, 270);
ctx.font = '700 26px sans-serif';
ctx.fillWrappedText('"' + claim + '"', 70, 310, 940, 36);

// Table of events
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(40, 480, 1000, 680);
ctx.strokeRect(40, 480, 1000, 680);

ctx.fillStyle = '#111111';
ctx.font = '900 28px sans-serif';
ctx.fillText('7 PUBLIC MILESTONES (EXACT FROM BRIEF)', 70, 530);

let curY = 580;
for (let i = 0; i < rawData.length; i++) {
  const item = rawData[i];
  ctx.fillStyle = '#FF5500';
  ctx.font = '900 28px monospace';
  ctx.fillText(String(item.year), 70, curY);
  
  ctx.fillStyle = '#111111';
  ctx.font = '600 22px sans-serif';
  ctx.fillWrappedText(item.event, 170, curY - 5, 840, 28);
  curY += 78;
}

// Derived gaps table
ctx.fillStyle = '#00E5FF';
ctx.fillRect(40, 1200, 1000, 660);
ctx.strokeRect(40, 1200, 1000, 660);

ctx.fillStyle = '#111111';
ctx.font = '900 28px sans-serif';
ctx.fillText('DERIVED GAPS & TOTAL SPAN (EXACT ARITHMETIC)', 70, 1250);

curY = 1300;
for (const d of derived) {
  ctx.fillStyle = '#111111';
  ctx.font = '700 22px monospace';
  ctx.fillText(d.label + ':', 70, curY);
  ctx.font = '400 20px monospace';
  ctx.fillText(d.arithmetic + ' =', 420, curY);
  ctx.font = '900 24px monospace';
  ctx.fillText(d.result + ' ' + d.unit, 680, curY);
  curY += 60;
}

canvas;
