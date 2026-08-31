Stage.begin('Forms');

Stage.note('Form 1: True-to-Scale Vertical Timeline (Question: Over time / Follow) — maps 1969-2022 proportionally along the vertical feed paper.');
Stage.note('Rejected Form 1: Horizontal timeline — rejected because 9:16 vertical canvas naturally suits a continuous vertical feed and provides vertical legibility for 53 years.');

Stage.note('Form 2: Horizontal Bar Comparison of Epoch Gaps (Question: Compared to what? — 20y vs 4y vs 5y vs 6y vs 3y vs 15y) with strict zero-baseline.');
Stage.note('Rejected Form 2: Donut / Radar chart — rejected per Manual 13 §1 anti-patterns (no 3D/spider/pie distortions for sequential interval comparisons).');

Stage.note('Form 3: Neo-Brutalist Big-Number Callout Badges (Question: How big? — Hero 53 YEARS span, 20 YR longest drought, 2 MO ChatGPT record).');
Stage.note('Rejected Form 3: Gauge dial with needle — rejected per Manual 13 §1 anti-patterns.');

Stage.note('Form 4: Proportional Dot-Waffle / Sprocket Matrix (Question: What share / Discrete count of 53 years) — 53 distinct tractor-feed punch marks.');
Stage.note('Rejected Form 4: Exploded pie chart — rejected per Manual 13 dataviz rules.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Neo-brutalist base
ctx.fillStyle = '#f4efe6';
ctx.fillRect(0, 0, 1080, 1920);

// Header
ctx.fillStyle = '#000000';
ctx.font = '900 42px "Arial Black", Impact, sans-serif';
ctx.fillText('STAGE 2: FORM TAXONOMY', 60, 100);

ctx.font = '700 20px "Courier New", monospace';
ctx.fillText('SELECTED 4 COMPLEMENTARY FORMS (MINIMUM 3 REQUIRED):', 60, 150);

const forms = [
    { num: '01', name: 'VERTICAL TRUE-SCALE TIMELINE', q: 'OVER TIME / FOLLOW', desc: 'Linear vertical mapping of 1969–2022 to paper feed height with milestone nodes.' },
    { num: '02', name: 'HORIZONTAL EPOCH-GAP BARS', q: 'COMPARED TO WHAT?', desc: 'Zero-baseline horizontal bars comparing the 6 gap intervals (20y, 4y, 5y, 6y, 3y, 15y).' },
    { num: '03', name: 'BIG-NUMBER STAT CALLOUTS', q: 'HOW BIG?', desc: 'Neo-brutalist heavy-border offset badges for 53 YEARS, 20-YR DROUGHT, 2-MO BLITZ.' },
    { num: '04', name: '53-YEAR FEED SPROCKET MATRIX', q: 'WHAT SHARE / COUNT', desc: 'Discrete punch-hole sprocket grid counting every year from 1969 to 2022.' }
];

let y = 220;
for (const f of forms) {
    // Neo-brutalist card with solid black drop shadow
    ctx.fillStyle = '#000000';
    ctx.fillRect(68, y + 8, 944, 200);
    ctx.fillStyle = '#ffffff';
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 4;
    ctx.fillRect(60, y, 944, 200);
    ctx.strokeRect(60, y, 944, 200);

    ctx.fillStyle = '#ff4338';
    ctx.fillRect(80, y + 25, 60, 35);
    ctx.fillStyle = '#ffffff';
    ctx.font = '900 20px "Courier New", monospace';
    ctx.fillText(f.num, 95, y + 50);

    ctx.fillStyle = '#000000';
    ctx.font = '900 24px "Arial Black", Impact, sans-serif';
    ctx.fillText(f.name, 160, y + 50);

    ctx.fillStyle = '#0055ff';
    ctx.font = '700 16px "Courier New", monospace';
    ctx.fillText('QUESTION: ' + f.q, 80, y + 100);

    ctx.fillStyle = '#333333';
    ctx.font = '400 18px "Consolas", monospace';
    ctx.fillText(f.desc, 80, y + 140);

    y += 240;
}

canvas;
