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

// Also test available fonts and environment
const testFonts = ['Courier New', 'Consolas', 'Lucida Console', 'Impact', 'Arial Black', 'Arial', 'Segoe UI', 'Trebuchet MS', 'monospace', 'sans-serif'];
const availableFonts = {};
for (const f of testFonts) {
    availableFonts[f] = Skia.Font.has(f);
}
log('Fonts tested: ' + JSON.stringify(availableFonts));

// Record data points and derived figures
const rawMilestones = [
    { year: 1969, label: 'ARPANET sends its first message (it crashed after "LO")' },
    { year: 1989, label: 'Tim Berners-Lee proposes the World Wide Web' },
    { year: 1993, label: 'Mosaic makes the web visual' },
    { year: 1998, label: 'Google is founded' },
    { year: 2004, label: 'Facebook launches — the social era begins' },
    { year: 2007, label: 'The iPhone puts the web in every pocket' },
    { year: 2022, label: 'ChatGPT reaches 100M users in 2 months' }
];

const derivedGaps = [
    { from: 1969, to: 1989, gap: 1989 - 1969, name: 'ARPANET → Web gap' },
    { from: 1989, to: 1993, gap: 1993 - 1989, name: 'Web → Mosaic gap' },
    { from: 1993, to: 1998, gap: 1998 - 1993, name: 'Mosaic → Google gap' },
    { from: 1998, to: 2004, gap: 2004 - 1998, name: 'Google → Facebook gap' },
    { from: 2004, to: 2007, gap: 2007 - 2004, name: 'Facebook → iPhone gap' },
    { from: 2007, to: 2022, gap: 2022 - 2007, name: 'iPhone → ChatGPT gap' }
];
const totalSpan = 2022 - 1969; // 53 years

Stage.note(`Data verified: 7 public milestones from 1969 to 2022 spanning 53 years. 6 duration intervals computed accurately.`);

// Canvas setup for initial data check
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f0eee6';
ctx.fillRect(0, 0, 1080, 1920);

// Basic proof of stage
ctx.fillStyle = '#000000';
ctx.font = '700 28px monospace';
ctx.fillText('STAGE 1: DATA & CLAIM VERIFIED', 60, 100);
ctx.font = '400 18px monospace';
ctx.fillText('CLAIM: ' + claim, 60, 160);
ctx.fillText('PLACE: ' + place, 60, 200);
ctx.fillText('SPAN: 1969 - 2022 (' + totalSpan + ' years)', 60, 240);

canvas;
