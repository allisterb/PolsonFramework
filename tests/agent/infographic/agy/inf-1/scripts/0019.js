Stage.begin('Data');
Stage.note('Data & Claim established from brief.md');

const claim = "Progress is constant — it never stopped coming, but it never once came on schedule.";
const reader = "General. Knows all seven events already; has never seen the gaps between them drawn to scale.";
const place = "At a line printer, watching fanfold paper feed through the platen";

log('CLAIM:   ' + claim);
log('READER:  ' + reader);
log('PLACE:   ' + place);

const data = [
  { year: 1969, event: 'ARPANET sends its first message (it crashed after "LO")', gapAfter: 20 },
  { year: 1989, event: 'Tim Berners-Lee proposes the World Wide Web', gapAfter: 4 },
  { year: 1993, event: 'Mosaic makes the web visual', gapAfter: 5 },
  { year: 1998, event: 'Google is founded', gapAfter: 6 },
  { year: 2004, event: 'Facebook launches — the social era begins', gapAfter: 3 },
  { year: 2007, event: 'The iPhone puts the web in every pocket', gapAfter: 15 },
  { year: 2022, event: 'ChatGPT reaches 100M users in 2 months', gapAfter: 0 }
];

Stage.note('Milestones: ' + data.length + ' points from 1969 to 2022 (53 years total).');

// Inspect available fonts
const families = Skia.Font.families();
log('Available font count: ' + families.length);
const check = ['Courier New', 'Consolas', 'Lucida Console', 'Courier', 'Impact', 'Arial Black', 'Arial', 'Helvetica', 'Trebuchet MS', 'Verdana', 'Segoe UI', 'Times New Roman', 'Georgia'];
log('Font check: ' + check.map(f => f + ': ' + Skia.Font.has(f)).join(', '));

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f0f0eb';
ctx.fillRect(0, 0, 1080, 1920);

ctx.fillStyle = '#111111';
ctx.font = '700 36px monospace';
ctx.fillText('DATA & CLAIM INGESTED', 80, 120);

canvas;
