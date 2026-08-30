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

const milestones = [
  { year: 1969, event: "ARPANET sends its first message (it crashed after 'LO')" },
  { year: 1989, event: "Tim Berners-Lee proposes the World Wide Web" },
  { year: 1993, event: "Mosaic makes the web visual" },
  { year: 1998, event: "Google is founded" },
  { year: 2004, event: "Facebook launches — the social era begins" },
  { year: 2007, event: "The iPhone puts the web in every pocket" },
  { year: 2022, event: "ChatGPT reaches 100M users in 2 months" }
];

const derived = [
  { from: '1969, 1989', arithmetic: '1989 - 1969', result: 20, unit: 'years', label: 'ARPANET -> Web gap' },
  { from: '1989, 1993', arithmetic: '1993 - 1989', result: 4, unit: 'years', label: 'Web -> Mosaic gap' },
  { from: '1993, 1998', arithmetic: '1998 - 1993', result: 5, unit: 'years', label: 'Mosaic -> Google gap' },
  { from: '1998, 2004', arithmetic: '2004 - 1998', result: 6, unit: 'years', label: 'Google -> Facebook gap' },
  { from: '2004, 2007', arithmetic: '2007 - 2004', result: 3, unit: 'years', label: 'Facebook -> iPhone gap' },
  { from: '2007, 2022', arithmetic: '2022 - 2007', result: 15, unit: 'years', label: 'iPhone -> ChatGPT gap' },
  { from: '1969, 2022', arithmetic: '2022 - 1969', result: 53, unit: 'years', label: 'Total timeline span' }
];

table(milestones);
table(derived);

// Persist data in Session for subsequent scripts
Session.milestones = milestones;
Session.derived = derived;
Session.canvasWidth = 1080;
Session.canvasHeight = 1920;

// Create a minimal validation canvas
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f0ea';
ctx.fillRect(0, 0, 1080, 1920);
ctx.fillStyle = '#111111';
ctx.font = '700 28px monospace';
ctx.fillText('STAGE 1: DATA & CLAIM VALIDATED', 60, 100);

canvas;
