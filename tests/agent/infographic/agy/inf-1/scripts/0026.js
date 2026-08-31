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

// Check fonts
const testFamilies = ['Inter', 'Inter Tight', 'Roboto', 'Arial', 'Courier New', 'Consolas', 'Menlo', 'Monaco', 'Space Mono', 'JetBrains Mono', 'IBM Plex Mono', 'Courier', 'Georgia', 'Times New Roman', 'Impact', 'Trebuchet MS'];
const available = testFamilies.filter(f => Skia.Font.has(f));
log('Available test fonts: ' + available.join(', '));

// Record data
const milestones = [
  { year: 1969, label: 'ARPANET sends its first message (it crashed after "LO")', gap: null },
  { year: 1989, label: 'Tim Berners-Lee proposes the World Wide Web', gap: 20 },
  { year: 1993, label: 'Mosaic makes the web visual', gap: 4 },
  { year: 1998, label: 'Google is founded', gap: 5 },
  { year: 2004, label: 'Facebook launches — the social era begins', gap: 6 },
  { year: 2007, label: 'The iPhone puts the web in every pocket', gap: 3 },
  { year: 2022, label: 'ChatGPT reaches 100M users in 2 months', gap: 15 }
];

Stage.note('Data verified: 7 milestones spanning 1969 to 2022 (53 years total). Gaps: 20, 4, 5, 6, 3, 15 years.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f0ea';
ctx.fillRect(0, 0, 1080, 1920);

// Simple verification render
ctx.fillStyle = '#111111';
ctx.font = '700 24px monospace';
ctx.fillText('STAGE 1: DATA & CLAIM INITIALIZED', 60, 100);

canvas;
